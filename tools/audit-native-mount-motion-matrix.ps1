param(
    [string]$GameExe = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe',
    [string]$CatalogPath
)

# Metadata-only, fail-closed audit for the vanilla 1.4.5.8 non-minecart
# mount motion matrix. This script does not load Terraria into the CLR, invoke
# game code, start the game, inspect saves, or write to the game installation.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedGameHash = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
$expectedCecilHash = 'C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B'
$expectedMvid = '2c29f6c3-4bd9-4add-9c58-da159804e083'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$gamePath = [IO.Path]::GetFullPath($GameExe)
$cecilPath = Join-Path $projectRoot 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'
$catalogAuditPath = Join-Path $PSScriptRoot 'audit-native-mount-catalog.ps1'
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = Join-Path $projectRoot 'src\Chaite.Core\VanillaMountCatalog.cs'
}
$catalogFullPath = [IO.Path]::GetFullPath($CatalogPath)
$thisScriptPath = [IO.Path]::GetFullPath($PSCommandPath)

function Assert-Condition([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

function Get-I4($Instruction) {
    if ($null -eq $Instruction) { return $null }
    switch ($Instruction.OpCode.Code.ToString()) {
        'Ldc_I4_M1' { return -1 }
        'Ldc_I4_0' { return 0 }
        'Ldc_I4_1' { return 1 }
        'Ldc_I4_2' { return 2 }
        'Ldc_I4_3' { return 3 }
        'Ldc_I4_4' { return 4 }
        'Ldc_I4_5' { return 5 }
        'Ldc_I4_6' { return 6 }
        'Ldc_I4_7' { return 7 }
        'Ldc_I4_8' { return 8 }
        'Ldc_I4_S' { return [int]$Instruction.Operand }
        'Ldc_I4' { return [int]$Instruction.Operand }
        default { return $null }
    }
}

function Get-LiteralText($Instruction) {
    $integer = Get-I4 $Instruction
    if ($null -ne $integer) {
        return ([int]$integer).ToString([Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_R4) {
        return ([single]$Instruction.Operand).ToString(
            'R', [Globalization.CultureInfo]::InvariantCulture)
    }
    if ($Instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_R8) {
        return ([double]$Instruction.Operand).ToString(
            'R', [Globalization.CultureInfo]::InvariantCulture)
    }
    return $null
}

function Get-UniqueMethod($Type, [string]$Name, [string[]]$ParameterTypes) {
    $signature = $ParameterTypes -join ','
    $matches = @($Type.Methods | Where-Object {
        $_.Name -ceq $Name -and $_.HasBody -and
        $_.Parameters.Count -eq $ParameterTypes.Count -and
        (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -ceq $signature
    })
    Assert-Condition ($matches.Count -eq 1) (
        "Expected one metadata method $($Type.FullName)::$Name($signature); found $($matches.Count).")
    return $matches[0]
}

function Get-MethodFingerprint($Method) {
    $text = (@($Method.Body.Instructions | ForEach-Object { $_.ToString() }) -join "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return ([BitConverter]::ToString(
            $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)))).Replace('-', '')
    } finally {
        $sha.Dispose()
    }
}

function Get-HelperCallText($Instructions, [int]$CallIndex) {
    $call = $Instructions[$CallIndex]
    $name = $call.Operand.Name
    $argumentStart = -1
    for ($i = $CallIndex - 1; $i -ge 0; $i--) {
        if ($Instructions[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldloc_0 -and
            $i + 1 -lt $CallIndex -and $null -ne (Get-I4 $Instructions[$i + 1])) {
            $argumentStart = $i
            break
        }
    }
    Assert-Condition ($argumentStart -ge 0) "Unreviewed argument expression for $name."
    $buff = Get-I4 $Instructions[$argumentStart + 1]
    Assert-Condition ($null -ne $buff) "Nonliteral buff argument for $name."
    if ($name -ceq 'SetAsChillet') {
        $hardmode = Get-I4 $Instructions[$CallIndex - 1]
        Assert-Condition ($hardmode -in @(0, 1)) 'Nonliteral SetAsChillet hardmode argument.'
        return "CALL:SetAsChillet(buff=$buff,hardmode=$hardmode)"
    }
    return "CALL:$name(buff=$buff)"
}

function Get-StaticIntArray($Method, [string]$TargetField) {
    $instructions = @($Method.Body.Instructions)
    $storeIndex = -1
    for ($i = 0; $i -lt $instructions.Count; $i++) {
        if ($instructions[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld -and
            $instructions[$i].Operand -is [Mono.Cecil.FieldReference] -and
            $instructions[$i].Operand.Name -ceq $TargetField) {
            $storeIndex = $i
            break
        }
    }
    Assert-Condition ($storeIndex -ge 0) "Missing MountID.Sets::$TargetField initializer."
    $token = $null
    $length = $null
    for ($i = $storeIndex - 1; $i -ge 0; $i--) {
        if ($instructions[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldtoken) {
            $token = $instructions[$i]
            for ($j = $i - 1; $j -ge [Math]::Max(0, $i - 5); $j--) {
                if ($instructions[$j].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newarr) {
                    $length = Get-I4 $instructions[$j - 1]
                    break
                }
            }
            break
        }
        if ($instructions[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld) { break }
    }
    Assert-Condition ($null -ne $token -and $null -ne $length) (
        "Unreviewed static-array form for MountID.Sets::$TargetField.")
    $field = $token.Operand.Resolve()
    Assert-Condition ($null -ne $field) (
        "Missing initial-value field for MountID.Sets::$TargetField.")
    $bytes = $field.InitialValue
    Assert-Condition ($null -ne $bytes) (
        "Missing initial-value blob for MountID.Sets::$TargetField.")
    Assert-Condition ($bytes.Length -eq ([int]$length * 4)) (
        "Unexpected byte length for MountID.Sets::$TargetField.")
    $values = [Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt [int]$length; $i++) {
        $values.Add([BitConverter]::ToInt32($bytes, $i * 4))
    }
    return $values.ToArray()
}

foreach ($required in @($gamePath, $cecilPath, $catalogAuditPath,
    $catalogFullPath, $thisScriptPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing required read-only input: $required"
    }
}

$gameHashBefore = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
$cecilHashBefore = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
$catalogHashBefore = (Get-FileHash -LiteralPath $catalogFullPath -Algorithm SHA256).Hash
$scriptHashBefore = (Get-FileHash -LiteralPath $thisScriptPath -Algorithm SHA256).Hash
Assert-Condition ($gameHashBefore -ceq $expectedGameHash) 'Unreviewed Terraria.exe hash.'
Assert-Condition ($cecilHashBefore -ceq $expectedCecilHash) 'Unreviewed Mono.Cecil binary hash.'

# Reuse the narrower identity proof before interpreting any motion row.
$identityOutput = @(& $catalogAuditPath -GameExe $gamePath -CatalogPath $catalogFullPath)
Assert-Condition ($identityOutput.Count -ge 6 -and
    $identityOutput[-1] -ceq
        'PASS metadata-only mount catalog audit. Terraria code was not loaded or executed; audited files are unchanged.') (
    'The prerequisite native mount identity audit did not complete as reviewed.')

$cecilAssembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $cecilPath).Path)
Assert-Condition ($cecilAssembly.GetName().Version.ToString() -ceq '0.11.6.0') (
    "Unreviewed Mono.Cecil assembly version: $($cecilAssembly.GetName().Version).")

$oldCulture = [Threading.Thread]::CurrentThread.CurrentCulture
$oldUiCulture = [Threading.Thread]::CurrentThread.CurrentUICulture
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture
$reader = [Mono.Cecil.ReaderParameters]::new()
$reader.InMemory = $true
$reader.ReadWrite = $false
$module = [Mono.Cecil.ModuleDefinition]::ReadModule(
    (Resolve-Path -LiteralPath $gamePath).Path, $reader)

try {
    Assert-Condition ($module.Mvid.ToString() -ceq $expectedMvid) 'Unreviewed Terraria module MVID.'
    Assert-Condition ($module.Assembly.Name.Version.ToString() -ceq '1.4.5.8') (
        "Unreviewed Terraria assembly version: $($module.Assembly.Name.Version).")
    Assert-Condition ($module.Architecture -eq [Mono.Cecil.TargetArchitecture]::I386) (
        "Unreviewed Terraria architecture: $($module.Architecture).")

    $mountType = $module.GetType('Terraria.Mount')
    $mountDataType = $module.GetType('Terraria.Mount/MountData')
    $playerType = $module.GetType('Terraria.Player')
    $setsType = $module.GetType('Terraria.ID.MountID/Sets')
    $delegateMountType = $module.GetType('Terraria.DelegateMethods/Mount')
    foreach ($type in @($mountType, $mountDataType, $playerType, $setsType,
        $delegateMountType)) {
        Assert-Condition ($null -ne $type) 'A required Terraria metadata type is missing.'
    }

    # Instruction fingerprints are locators and drift alarms for the manually
    # reviewed branch semantics. They do not by themselves authorize input.
    $methodSpecs = @(
        @($mountType, 'Initialize', @(), 6430, 17213, '61B157EFFB00ABCD467418BA571CAB44F27B2DB8E96D1B6F06D56F39FA03C4C1'),
        @($mountDataType, '.ctor', @(), 33, 117, '4D821E349CB0A95EDAB1EA03A6F4B13AEB70A702C69C8F4B04DBB010568AE822'),
        @($mountType, 'SetAsHorse', @('Terraria.Mount/MountData','System.Int32','ReLogic.Content.Asset`1<Microsoft.Xna.Framework.Graphics.Texture2D>'), 219, 511, 'A099F2F2FA688F634D3489DB9A7566FA419DE190424F23E988973C2D4268B481'),
        @($mountType, 'SetAsRollerSkate', @('Terraria.Mount/MountData','System.Int32'), 103, 259, '350B46DBE240437055976808CE45FE8531D44737103DFBEB73BC913BAA7AED44'),
        @($mountType, 'SetAsChillet', @('Terraria.Mount/MountData','System.Int32','ReLogic.Content.Asset`1<Microsoft.Xna.Framework.Graphics.Texture2D>','System.Boolean'), 246, 588, 'CEF02F0FF9157AC8F3D8DE0EB834194AFD919D438A253E4EC900AE77613D938F'),
        @($mountType, 'get_RunSpeed', @(), 107, 276, '68729B7A13438B34D3223EC99C35F4D2EFA2B3F4206F7A444A943235616AD238'),
        @($mountType, 'JumpHeight', @('System.Single'), 46, 117, 'FF594056D3C04941A41403305831B578E619781BB07164A91EA69179E677FC04'),
        @($mountType, 'JumpSpeed', @('System.Single'), 40, 84, 'B9C506AEA474F953987A4CEC55A3D6E8BE3384801B0789716538B7455200BCB6'),
        @($mountType, 'CanFly', @('Terraria.Player'), 30, 69, '3C8A1C3ACFAB22C5133E5AD33A7E38092B63C9F886255EA711039B0E64658AA9'),
        @($mountType, 'CanHover', @(), 26, 60, '86201B4A0075095CBD5F97D00E2689F2C6C4E6B2492DE3486F38112DB6A6BF51'),
        @($mountType, 'Flight', @(), 14, 27, 'D1236A7129E4245CDBE80DBCCA89FDA29192F59DAC1E1EC00A9672CBDBEF6F44'),
        @($mountType, 'UpdateAfterEquips', @('Terraria.Player'), 106, 322, 'A8432B8B2754126F3E3F0A7965F482861416E9B41D6CA6ED196E391DA58FF4FF'),
        @($mountType, 'ResetFlightTime', @('Terraria.Player'), 34, 95, '12FC5347B55AA556111B2105EA969E595D2A9848B550BADBEDA98AA57D0C12F3'),
        @($mountType, 'FatigueRecovery', @(), 15, 44, '0C3B66EAE8297A647B998B91313CE637DF0E4D718BB7514D8506DE2839419E69'),
        @($mountType, 'UseAbility', @('Terraria.Player','Microsoft.Xna.Framework.Vector2','System.Boolean'), 380, 1129, '8109921F9584A29B6446CEF058933230C7FDF456222B3635213821213BEB4974'),
        @($mountType, 'Hover', @('Terraria.Player'), 636, 1684, 'A97327EACD9217F7B9A566A89DE1FEBB990BCD582159ECA5331417E26F557A7A'),
        @($mountType, 'DoesHoverIgnoresFatigue', @(), 35, 81, '6107B2283FAFA2C53A6E30B61B4AFA9AC2B4051007AB5205026E1809E24534DA'),
        @($mountType, 'UpdateFrame', @('Terraria.Player','System.Int32','Microsoft.Xna.Framework.Vector2'), 3084, 9265, 'E306CA4956F2290712367BF874E8581B4EF0BAF6B903620E1A6D04CD75EAA711'),
        @($mountType, 'UpdateFrame_Velociraptor', @('Terraria.Player','System.Int32&'), 67, 119, '070169C12F96D55934898DA81CAFA8349C8BC548A61B6FEC4711D3AF2CEA450E'),
        @($mountType, 'TryBeginningFlight', @('Terraria.Player','System.Int32'), 98, 238, '24D85A5D3A366C4BDE7416CB5287DDA6A1FE123EB9642C52AE7B3C22DB9E829D'),
        @($mountType, 'TryLanding', @('Terraria.Player'), 58, 146, 'D95D1342D4D1BC8E8D3686F2A73D0E622028CD43DA95D1E4B5DF986D6F7EA642'),
        @($mountType, 'UpdateEffects', @('Terraria.Player'), 1383, 4554, '65FC314DEC9ED00A32BBECC797A5D1708EAC5D8A84DF8130512EA782DF27EDA9'),
        @($mountType, 'AimAbility', @('Terraria.Player','Microsoft.Xna.Framework.Vector2'), 463, 1224, 'DF16542EB4700B3D823D84349AD9C502FD1EB6FB7EA7983F420A67F3581218FA'),
        @($mountType, 'SetMount', @('System.Int32','Terraria.Player'), 424, 1060, 'ED5F8721DCFABF305ED77E550F2203F8B135D6249B6CBB336DCA08FCAEA8D757'),
        @($mountType, 'DismountsOnItemUse', @('System.Int32'), 5, 13, 'FF3D4B3D322A883CC2B15AD7601E2114CD3DD3BC866CE2FE069B5D6566D370B7'),
        @($playerType, 'HorizontalMovement', @(), 1606, 4862, 'CC431C516284E1A53A836D5FDF3502DD1EEC8C548984889CC8D8CEA5273CB083'),
        @($playerType, 'JumpMovement', @(), 2605, 7892, '6348124FB208197CE825EE8FE6276CB8A81CEB8E881061744F127A08551CEAB3'),
        @($playerType, 'DashMovement', @(), 2787, 8086, '658EC89165B30D7DB358566950557C2977D32793CCCA0B67A29E3F39E7E0F110'),
        @($playerType, 'WetCollision', @('System.Boolean','System.Boolean','System.Single'), 49, 145, '2ACAB564231EA3B6F858B97E32F473954878C4B592E84414AD980F73EACD9478'),
        @($playerType, 'DryCollision', @('System.Boolean','System.Boolean'), 385, 1073, '480D6E8E06BDA95E81F96788E74872A83C70F060B964CB14EB69DE7A41C462DB'),
        @($playerType, 'Update', @('System.Int32'), 12757, 36687, '00020A380C43B674738C68F15BDB6CB479B1ED7E2FB316E4660CAF6F3F4D1816'),
        @($setsType, '.cctor', @(), 100, 402, '8C5F6056A02D0AA94D465F5388C5667A7D6C088947E6B3F8487DE5D1BC9C9304'),
        @($delegateMountType, 'RatPlayerSize', @('Terraria.Player','System.Nullable`1<Microsoft.Xna.Framework.Vector2>&'), 8, 28, 'AA29F2A55BE0EC46F8A3DF47D08F54CE35CEE6D934635C7A6C98C4E2BB7D0AB9'),
        @($delegateMountType, 'BatPlayerSize', @('Terraria.Player','System.Nullable`1<Microsoft.Xna.Framework.Vector2>&'), 8, 28, '991D90E6334AF79FA08AB2B2AC9402006ED0137101C5659D5C5BFC6BE49D60B6'),
        @($delegateMountType, 'PixiePlayerSize', @('Terraria.Player','System.Nullable`1<Microsoft.Xna.Framework.Vector2>&'), 8, 28, 'E333A2BAA7364ED3D943AEDB291E4180E9F950239DA666503FC129FFB01D1AA5')
    )
    foreach ($spec in $methodSpecs) {
        $method = Get-UniqueMethod $spec[0] $spec[1] $spec[2]
        Assert-Condition ($method.Body.Instructions.Count -eq $spec[3] -and
            $method.Body.CodeSize -eq $spec[4]) "$($method.FullName) body shape changed."
        Assert-Condition ((Get-MethodFingerprint $method) -ceq $spec[5]) (
            "$($method.FullName) reviewed IL fingerprint changed.")
    }

    $setsCctor = Get-UniqueMethod $setsType '.cctor' @()
    $expectedSets = [ordered]@{
        CanUseHooks = '54,57,58,59,60'
        CanDash = '56,57,58,59,60,61,62,63,64,65'
        DoesNotOverrideBodyFrames = '57,58,59,60'
        DoesNotOverrideLegFrames = '57,58,59,60'
        DoesNotOverrideBackpackDraw = '57,58,59,60'
        DoesNotOverrideWings = '57,58,59,60'
        IsRollerSkates = '57,58,59,60'
        Cart = '6,13,11,15,16,18,19,20,21,22,24,25,26,27,28,29,30,31,32,33,34,35,36,38,39,51,53'
        IsTransformationMount = '52,54,55,56,61'
        PlayerIsHidden = '52,54,55,56,61'
        DontHoldItems = '55,56,61'
        DontDismountWhenCCed = '55,56,61'
    }
    foreach ($entry in $expectedSets.GetEnumerator()) {
        $actual = (Get-StaticIntArray $setsCctor $entry.Key) -join ','
        Assert-Condition ($actual -ceq $entry.Value) (
            "MountID.Sets::$($entry.Key) changed: $actual")
    }
    $cartIds = @((Get-StaticIntArray $setsCctor 'Cart') | Sort-Object)
    $nonMinecartIds = @(0..65 | Where-Object { $_ -notin $cartIds })
    $expectedNonMinecartIds = @(0,1,2,3,4,5,7,8,9,10,12,14,17,23,37,40,41,
        42,43,44,45,46,47,48,49,50,52,54,55,56,57,58,59,60,61,62,63,64,65)
    Assert-Condition ($nonMinecartIds.Count -eq 39 -and
        ($nonMinecartIds -join ',') -ceq ($expectedNonMinecartIds -join ',')) (
        'The reviewed non-minecart MountID scope is no longer exactly 39 identities.')

    # Canonical direct initializer stores. Missing fields are CLR-zero because
    # the separately fingerprinted MountData constructor does not initialize
    # any motion numeric/boolean field. COPY records the two reviewed same-row
    # field-forwarding expressions rather than guessing a value.
    $expectedInitializers = [ordered]@{
        0='buff=90;heightBoost=20;flightTimeMax=160;runSpeed=5.5;dashSpeed=12;acceleration=0.09;jumpHeight=17;jumpSpeed=5.31'
        1='buff=128;heightBoost=20;flightTimeMax=0;fallDamage=0.8;runSpeed=4;dashSpeed=7.8;acceleration=0.13;jumpHeight=15;jumpSpeed=5.01'
        2='buff=129;heightBoost=20;flightTimeMax=160;runSpeed=5;dashSpeed=9;acceleration=0.08;jumpHeight=10;jumpSpeed=6.01'
        3='buff=130;heightBoost=20;flightTimeMax=0;fallDamage=0.5;extraFall=10;runSpeed=4;dashSpeed=4;acceleration=0.18;jumpHeight=12;jumpSpeed=8.25;constantJump=1'
        4='buff=131;heightBoost=26;flightTimeMax=0;fallDamage=1;runSpeed=2;dashSpeed=5;swimSpeed=10;acceleration=0.08;jumpHeight=12;jumpSpeed=3.7'
        5='buff=132;heightBoost=16;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;runSpeed=2;dashSpeed=2;acceleration=0.16;jumpHeight=10;jumpSpeed=4;blockExtraJumps=1'
        7='buff=141;heightBoost=16;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;runSpeed=8;dashSpeed=8;acceleration=0.16;jumpHeight=10;jumpSpeed=4;blockExtraJumps=1'
        8='buff=142;heightBoost=16;flightTimeMax=320;fatigueMax=320;fallDamage=1;usesHover=1;swimSpeed=4;runSpeed=6;dashSpeed=4;acceleration=0.16;jumpHeight=10;jumpSpeed=4;blockExtraJumps=1'
        9='buff=143;heightBoost=16;flightTimeMax=0;fatigueMax=0;fallDamage=0;runSpeed=8;dashSpeed=8;acceleration=0.4;jumpHeight=22;jumpSpeed=10.01;blockExtraJumps=0'
        10='buff=162;heightBoost=34;flightTimeMax=0;fallDamage=0.2;runSpeed=4;dashSpeed=12;acceleration=0.3;jumpHeight=10;jumpSpeed=8.01'
        12='buff=168;heightBoost=14;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;runSpeed=2;dashSpeed=1;acceleration=0.2;jumpHeight=4;jumpSpeed=3;swimSpeed=16;blockExtraJumps=1'
        14='buff=193;heightBoost=6;flightTimeMax=0;fallDamage=0.2;runSpeed=8;acceleration=0.25;jumpHeight=20;jumpSpeed=8.01'
        17='buff=212;heightBoost=16;flightTimeMax=0;fallDamage=0.2;runSpeed=8;acceleration=0.25;jumpHeight=20;jumpSpeed=8.01'
        23='buff=230;heightBoost=0;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;runSpeed=9;dashSpeed=9;acceleration=0.16;jumpHeight=10;jumpSpeed=4;blockExtraJumps=1'
        37='buff=265;heightBoost=12;flightTimeMax=0;fallDamage=0.2;runSpeed=6;acceleration=0.15;jumpHeight=14;jumpSpeed=6.01'
        40='CALL:SetAsHorse(buff=275)'
        41='CALL:SetAsHorse(buff=276)'
        42='CALL:SetAsHorse(buff=277)'
        43='buff=278;heightBoost=12;flightTimeMax=0;fallDamage=0.25;extraFall=20;runSpeed=5;acceleration=0.1;jumpHeight=8;jumpSpeed=8;constantJump=1'
        44='buff=279;heightBoost=24;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;runSpeed=3;dashSpeed=6;acceleration=0.12;jumpHeight=3;jumpSpeed=1;swimSpeed=COPY:runSpeed;blockExtraJumps=1'
        45='buff=280;heightBoost=25;flightTimeMax=0;fallDamage=0.1;runSpeed=12;dashSpeed=16;acceleration=0.5;jumpHeight=14;jumpSpeed=7'
        46='buff=281;heightBoost=0;flightTimeMax=0;fatigueMax=0;fallDamage=0;runSpeed=8;dashSpeed=8;acceleration=0.4;jumpHeight=8;jumpSpeed=9.01;blockExtraJumps=0'
        47='buff=282;heightBoost=34;flightTimeMax=0;fallDamage=0.2;runSpeed=4;dashSpeed=12;acceleration=0.3;jumpHeight=10;jumpSpeed=8.01'
        48='buff=283;heightBoost=14;flightTimeMax=320;fallDamage=0;usesHover=1;runSpeed=8;dashSpeed=8;acceleration=0.2;jumpHeight=5;jumpSpeed=6;swimSpeed=COPY:runSpeed'
        49='buff=305;heightBoost=8;runSpeed=2;dashSpeed=1;acceleration=0.4;jumpHeight=4;jumpSpeed=3;swimSpeed=14;blockExtraJumps=1;flightTimeMax=0;fatigueMax=320;usesHover=1'
        50='buff=318;heightBoost=20;flightTimeMax=80;fallDamage=0.5;runSpeed=5.5;dashSpeed=5.5;acceleration=0.2;jumpHeight=10;jumpSpeed=7.25;constantJump=1'
        52='buff=342;flightTimeMax=0;fallDamage=0.1;runSpeed=9.5;acceleration=0.18;jumpHeight=18;jumpSpeed=9.01'
        54='buff=370;flightTimeMax=0;fallDamage=0.1;runSpeed=4.5;dashSpeed=7.5;acceleration=0.15;jumpHeight=15;jumpSpeed=6.01'
        55='buff=374;flightTimeMax=0;fallDamage=0.1;dismountsOnItemUse=1;runSpeed=4.5;dashSpeed=7.5;acceleration=0.15;jumpHeight=15;jumpSpeed=6.01'
        56='buff=377;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;dismountsOnItemUse=1;blockExtraJumps=1;dashSpeed=4.5;runSpeed=COPY:dashSpeed;acceleration=0.2;jumpHeight=8;jumpSpeed=5'
        57='CALL:SetAsRollerSkate(buff=378)'
        58='CALL:SetAsRollerSkate(buff=379)'
        59='CALL:SetAsRollerSkate(buff=380)'
        60='CALL:SetAsRollerSkate(buff=381)'
        61='buff=384;flightTimeMax=320;fatigueMax=320;fallDamage=0;usesHover=1;dismountsOnItemUse=1;blockExtraJumps=1;dashSpeed=4.5;runSpeed=COPY:dashSpeed;acceleration=0.2;jumpHeight=8;jumpSpeed=5'
        62='CALL:SetAsChillet(buff=387,hardmode=0)'
        63='CALL:SetAsChillet(buff=388,hardmode=0)'
        64='CALL:SetAsChillet(buff=391,hardmode=1)'
        65='CALL:SetAsChillet(buff=392,hardmode=1)'
    }

    $motionFields = @('buff','heightBoost','flightTimeMax','fatigueMax','usesHover',
        'runSpeed','dashSpeed','swimSpeed','acceleration','jumpHeight','jumpSpeed',
        'fallDamage','extraFall','constantJump','blockExtraJumps',
        'dismountsOnItemUse','CanUseWings','MovementStatsAreAdditive',
        'CanRideMinecartTracks')
    $initialize = Get-UniqueMethod $mountType 'Initialize' @()
    $initializeIl = @($initialize.Body.Instructions)
    $constructors = [Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $initializeIl.Count; $i++) {
        if ($initializeIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newobj -and
            $initializeIl[$i].Operand -is [Mono.Cecil.MethodReference] -and
            $initializeIl[$i].Operand.DeclaringType.FullName -ceq 'Terraria.Mount/MountData' -and
            $initializeIl[$i].Operand.Name -ceq '.ctor') {
            $constructors.Add($i)
        }
    }
    Assert-Condition ($constructors.Count -eq 66) 'Mount.Initialize no longer constructs 66 MountData rows.'
    $actualInitializers = @{}
    for ($block = 0; $block -lt $constructors.Count; $block++) {
        $start = $constructors[$block]
        $end = if ($block + 1 -lt $constructors.Count) {
            $constructors[$block + 1] - 1
        } else { $initializeIl.Count - 1 }
        $mountId = $null
        for ($i = $start; $i -le [Math]::Min($end, $start + 12); $i++) {
            if ($initializeIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stelem_Ref) {
                $mountId = Get-I4 $initializeIl[$i - 2]
                break
            }
        }
        Assert-Condition ($null -ne $mountId) 'Lost a reviewed mounts[] construction store.'
        if ([int]$mountId -notin $expectedNonMinecartIds) { continue }
        $parts = [Collections.Generic.List[string]]::new()
        for ($i = $start; $i -le $end; $i++) {
            $instruction = $initializeIl[$i]
            if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
                $instruction.Operand -is [Mono.Cecil.FieldReference] -and
                $instruction.Operand.DeclaringType.FullName -ceq 'Terraria.Mount/MountData' -and
                $instruction.Operand.Name -in $motionFields) {
                $value = Get-LiteralText $initializeIl[$i - 1]
                if ($null -eq $value -and
                    $initializeIl[$i - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldfld -and
                    $initializeIl[$i - 1].Operand.DeclaringType.FullName -ceq 'Terraria.Mount/MountData') {
                    $value = "COPY:$($initializeIl[$i - 1].Operand.Name)"
                }
                Assert-Condition ($null -ne $value) (
                    "Mount $mountId has an unreviewed initializer for $($instruction.Operand.Name).")
                $parts.Add("$($instruction.Operand.Name)=$value")
            }
            if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
                $instruction.Operand -is [Mono.Cecil.MethodReference] -and
                $instruction.Operand.DeclaringType.FullName -ceq 'Terraria.Mount' -and
                $instruction.Operand.Name -in @('SetAsHorse','SetAsRollerSkate','SetAsChillet')) {
                $parts.Add((Get-HelperCallText $initializeIl $i))
            }
        }
        $actualInitializers[[int]$mountId] = $parts -join ';'
    }
    Assert-Condition ($actualInitializers.Count -eq 39) 'Did not recover all 39 non-minecart initializer rows.'
    foreach ($entry in $expectedInitializers.GetEnumerator()) {
        Assert-Condition ($actualInitializers.ContainsKey([int]$entry.Key)) (
            "Missing initializer row for mount $($entry.Key).")
        Assert-Condition ($actualInitializers[[int]$entry.Key] -ceq $entry.Value) (
            "Mount $($entry.Key) initializer changed.`nExpected: $($entry.Value)`nActual:   $($actualInitializers[[int]$entry.Key])")
    }

    # These sets are semantically important enough to state explicitly even
    # though their containing methods are also fingerprinted.
    $hoverIgnore = Get-UniqueMethod $mountType 'DoesHoverIgnoresFatigue' @()
    $hoverIgnoreIds = [Collections.Generic.List[int]]::new()
    $hoverIl = @($hoverIgnore.Body.Instructions)
    for ($i = 0; $i -lt $hoverIl.Count - 1; $i++) {
        if ($hoverIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldfld -and
            $hoverIl[$i].Operand.FullName -ceq 'System.Int32 Terraria.Mount::_type') {
            $value = Get-I4 $hoverIl[$i + 1]
            Assert-Condition ($null -ne $value) 'Unreviewed hover-ignore type comparison.'
            $hoverIgnoreIds.Add([int]$value)
        }
    }
    Assert-Condition (($hoverIgnoreIds -join ',') -ceq '7,8,12,23,44,49,56,61') (
        'DoesHoverIgnoresFatigue type set changed.')

    $expectedSizeConstants = [ordered]@{
        RatPlayerSize = '14,14'
        BatPlayerSize = '20,18'
        PixiePlayerSize = '8,14'
    }
    foreach ($entry in $expectedSizeConstants.GetEnumerator()) {
        $method = Get-UniqueMethod $delegateMountType $entry.Key @(
            'Terraria.Player','System.Nullable`1<Microsoft.Xna.Framework.Vector2>&')
        $values = @($method.Body.Instructions | Where-Object {
            $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_R4
        } | ForEach-Object { Get-LiteralText $_ })
        Assert-Condition (($values -join ',') -ceq $entry.Value) (
            "$($entry.Key) delegated player size changed.")
    }

    $gameHashAfter = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
    $cecilHashAfter = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
    $catalogHashAfter = (Get-FileHash -LiteralPath $catalogFullPath -Algorithm SHA256).Hash
    $scriptHashAfter = (Get-FileHash -LiteralPath $thisScriptPath -Algorithm SHA256).Hash
    Assert-Condition ($gameHashAfter -ceq $gameHashBefore) 'Terraria.exe changed during audit.'
    Assert-Condition ($cecilHashAfter -ceq $cecilHashBefore) 'Mono.Cecil changed during audit.'
    Assert-Condition ($catalogHashAfter -ceq $catalogHashBefore) 'VanillaMountCatalog.cs changed during audit.'
    Assert-Condition ($scriptHashAfter -ceq $scriptHashBefore) 'This audit script changed while it was running.'

    Write-Output "PASS pinned read-only module: Terraria 1.4.5.8 x86; SHA-256 $gameHashAfter; MVID $expectedMvid"
    Write-Output 'PASS prerequisite identity proof: 66 MountIDs, 64 items, 66 buffs, and 27 minecarts'
    Write-Output 'PASS scope: exactly 39 non-minecart mount identities'
    Write-Output 'PASS initialization: 39/39 direct/helper motion rows match reviewed native IL'
    Write-Output 'PASS native capability sets: hooks, dash, roller-skate, transformation, item-hold, and cart sets match'
    Write-Output 'PASS branch evidence: hover/flight/frame/effects/ability/set-mount/player movement/collision fingerprints match'
    Write-Output 'PASS evidence boundary: this proves identity and reviewed native facts only; it grants no production input authority'
    Write-Output 'PASS metadata-only mount motion audit. Terraria code was not loaded or executed; audited files are unchanged.'
} finally {
    $module.Dispose()
    [Threading.Thread]::CurrentThread.CurrentCulture = $oldCulture
    [Threading.Thread]::CurrentThread.CurrentUICulture = $oldUiCulture
}

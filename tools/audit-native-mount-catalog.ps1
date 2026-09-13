param(
    [string]$GameExe = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe',
    [string]$CatalogPath
)

# Static, metadata-only audit. Mono.Cecil reads the PE/IL; this script never
# loads Terraria into the CLR, invokes game code, starts Terraria, or writes to
# the live installation. CatalogPath is likewise read only.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedGameHash = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
$expectedCecilHash = 'C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$gamePath = [IO.Path]::GetFullPath($GameExe)
$cecilPath = Join-Path $projectRoot 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = Join-Path $projectRoot 'src\Chaite.Core\VanillaMountCatalog.cs'
}
$catalogFullPath = [IO.Path]::GetFullPath($CatalogPath)

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

function Test-ArgumentForwarding($Method, [string]$FieldFullName) {
    $instructions = @($Method.Body.Instructions)
    $matches = 0
    for ($i = 2; $i -lt $instructions.Count; $i++) {
        if ($instructions[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
            $instructions[$i].Operand -is [Mono.Cecil.FieldReference] -and
            $instructions[$i].Operand.FullName -ceq $FieldFullName -and
            $instructions[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0 -and
            $instructions[$i - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_1) {
            $matches++
        }
    }
    Assert-Condition ($matches -eq 1) (
        "$($Method.FullName) no longer forwards parameter 1 exactly once to $FieldFullName.")
}

function Resolve-MountTypeStore($Instructions, [int]$StoreIndex, [int]$ItemType) {
    # All native mount-item stores in the pinned binary use one of these three
    # transparent expressions. Reject any new expression instead of guessing.
    $direct = Get-I4 $Instructions[$StoreIndex - 1]
    if ($null -ne $direct -and
        $Instructions[$StoreIndex - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0) {
        return [int]$direct
    }

    if ($StoreIndex -ge 6 -and
        $Instructions[$StoreIndex - 6].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0 -and
        $Instructions[$StoreIndex - 4].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_1) {
        $base = Get-I4 $Instructions[$StoreIndex - 5]
        $offset = Get-I4 $Instructions[$StoreIndex - 3]
        if ($null -ne $base -and $null -ne $offset -and
            $Instructions[$StoreIndex - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Sub -and
            $Instructions[$StoreIndex - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Add) {
            return [int]$base + ($ItemType - [int]$offset)
        }
    }

    if ($StoreIndex -ge 6 -and
        $Instructions[$StoreIndex - 6].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0 -and
        $Instructions[$StoreIndex - 4].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_1) {
        $base = Get-I4 $Instructions[$StoreIndex - 5]
        $offset = Get-I4 $Instructions[$StoreIndex - 2]
        if ($null -ne $base -and $null -ne $offset -and
            $Instructions[$StoreIndex - 3].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Add -and
            $Instructions[$StoreIndex - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Sub) {
            return [int]$base + $ItemType - [int]$offset
        }
    }

    throw "Unreviewed mountType expression before $($Instructions[$StoreIndex])."
}

function Resolve-ItemMount($EntryPoint, [int]$ItemType) {
    $method = $EntryPoint.Method
    $instructions = @($method.Body.Instructions)
    $startIndex = -1
    for ($i = 0; $i -lt $instructions.Count; $i++) {
        if ([object]::ReferenceEquals($instructions[$i], $EntryPoint.Start)) {
            $startIndex = $i
            break
        }
    }
    Assert-Condition ($startIndex -ge 0) "Lost item $ItemType entry point in $($method.FullName)."

    $resolved = [Collections.Generic.List[int]]::new()
    for ($i = $startIndex; $i -lt $instructions.Count; $i++) {
        $instruction = $instructions[$i]
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
            $instruction.Operand -is [Mono.Cecil.FieldReference] -and
            $instruction.Operand.FullName -ceq 'System.Int32 Terraria.Item::mountType') {
            $resolved.Add((Resolve-MountTypeStore $instructions $i $ItemType))
        }
        if ($instruction.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call, [Mono.Cecil.Cil.Code]::Callvirt) -and
            $instruction.Operand -is [Mono.Cecil.MethodReference] -and
            $instruction.Operand.DeclaringType.FullName -ceq 'Terraria.Item' -and
            $instruction.Operand.Name -ceq 'DefaultToMinecart') {
            Assert-Condition ($i -ge 2 -and
                $instructions[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0) (
                "Item $ItemType has an unreviewed DefaultToMinecart receiver expression.")
            $minecartType = Get-I4 $instructions[$i - 1]
            Assert-Condition ($null -ne $minecartType) (
                "Item $ItemType has an unreviewed DefaultToMinecart argument expression.")
            $resolved.Add([int]$minecartType)
        }
        if ($instruction.OpCode.FlowControl -in @(
            [Mono.Cecil.Cil.FlowControl]::Branch,
            [Mono.Cecil.Cil.FlowControl]::Cond_Branch) -and $resolved.Count -eq 0) {
            throw "Item $ItemType branches before its mount binding at $instruction; refusing a guessed path."
        }
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret) { break }
    }
    Assert-Condition ($resolved.Count -eq 1) (
        "Expected exactly one native mount binding for item $ItemType; found $($resolved.Count).")
    return $resolved[0]
}

foreach ($required in @($gamePath, $cecilPath, $catalogFullPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Missing required read-only input: $required"
    }
}

$gameHashBefore = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
$cecilHashBefore = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
$catalogHashBefore = (Get-FileHash -LiteralPath $catalogFullPath -Algorithm SHA256).Hash
Assert-Condition ($gameHashBefore -ceq $expectedGameHash) 'Unreviewed Terraria.exe hash.'
Assert-Condition ($cecilHashBefore -ceq $expectedCecilHash) 'Unreviewed Mono.Cecil binary hash.'

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
    # Parse the checked-in C# table itself, not a second shadow table. The
    # grammar is deliberately narrow so an unfamiliar constructor expression
    # fails instead of being silently skipped.
    $catalogSource = Get-Content -LiteralPath $catalogFullPath -Raw -Encoding UTF8
    $hashMatch = [regex]::Match($catalogSource,
        'VerifiedTerrariaSha256\s*=\s*"(?<hash>[0-9A-Fa-f]{64})"\s*;')
    Assert-Condition $hashMatch.Success 'Catalog does not declare VerifiedTerrariaSha256.'
    Assert-Condition ($hashMatch.Groups['hash'].Value.ToUpperInvariant() -ceq $expectedGameHash) (
        'Catalog VerifiedTerrariaSha256 does not match the audited game.')

    $entryPattern = @'
E\(\s*(?<mount>\d+)\s*,\s*(?<item>\d+)\s*,\s*(?<buff>\d+)\s*,\s*"(?<key>[^"]+)"
(?:\s*,\s*(?<minecart>true|false))?
(?:\s*,\s*VanillaMountModelEvidence\.(?<evidence>[A-Za-z0-9_]+))?\s*\)
'@
    $entryMatches = [regex]::Matches($catalogSource, $entryPattern,
        [Text.RegularExpressions.RegexOptions]::IgnorePatternWhitespace)
    Assert-Condition ($entryMatches.Count -eq 66) (
        "Expected 66 literal E(...) catalog rows; parsed $($entryMatches.Count).")
    $catalog = @($entryMatches | ForEach-Object {
        $minecart = $_.Groups['minecart'].Success -and
            $_.Groups['minecart'].Value -ceq 'true'
        $evidence = if ($_.Groups['evidence'].Success) {
            $_.Groups['evidence'].Value
        } else {
            'IdentityOnly'
        }
        [pscustomobject]@{
            Mount = [int]$_.Groups['mount'].Value
            Item = [int]$_.Groups['item'].Value
            Buff = [int]$_.Groups['buff'].Value
            Key = $_.Groups['key'].Value
            Minecart = [bool]$minecart
            Evidence = $evidence
        }
    })

    $catalogMounts = @($catalog | ForEach-Object { $_.Mount } | Sort-Object)
    Assert-Condition (($catalogMounts -join ',') -ceq ((0..65) -join ',')) (
        'Catalog mount IDs are not exactly one continuous 0..65 sequence.')
    $duplicateItems = @($catalog | Where-Object { $_.Item -gt 0 } |
        Group-Object Item | Where-Object Count -ne 1)
    Assert-Condition ($duplicateItems.Count -eq 0) 'Catalog contains duplicate nonzero summon item IDs.'
    Assert-Condition (@($catalog | Group-Object Buff | Where-Object Count -ne 1).Count -eq 0) (
        'Catalog contains duplicate buff IDs.')
    Assert-Condition (@($catalog | Group-Object Key | Where-Object Count -ne 1).Count -eq 0) (
        'Catalog contains duplicate stable keys.')
    Assert-Condition (@($catalog | Where-Object { $_.Key -cnotmatch '^[a-z0-9]+(?:-[a-z0-9]+)*$' }).Count -eq 0) (
        'Catalog contains a malformed stable key.')
    $motionEntries = @($catalog | Where-Object Evidence -ceq 'ExactDryMotion')
    Assert-Condition ($motionEntries.Count -eq 1 -and $motionEntries[0].Mount -eq 23) (
        'Only Witch''s Broom mount 23 may currently claim ExactDryMotion.')
    Assert-Condition (@($catalog | Where-Object {
        $_.Evidence -notin @('IdentityOnly', 'ExactDryMotion')
    }).Count -eq 0) 'Catalog contains an unreviewed evidence level.'

    # MountID.Count is initialized at runtime in this build, while the named
    # IDs are literal constants. Verify both representations.
    $mountIdType = $module.GetType('Terraria.ID.MountID')
    Assert-Condition ($null -ne $mountIdType) 'Missing Terraria.ID.MountID metadata type.'
    $countFields = @($mountIdType.Fields | Where-Object Name -ceq 'Count')
    Assert-Condition ($countFields.Count -eq 1 -and
        $countFields[0].FieldType.FullName -ceq 'System.Int32') 'MountID.Count field contract changed.'
    $mountIdCctor = Get-UniqueMethod $mountIdType '.cctor' @()
    $countIl = @($mountIdCctor.Body.Instructions)
    Assert-Condition ($countIl.Count -eq 3 -and (Get-I4 $countIl[0]) -eq 66 -and
        $countIl[1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld -and
        $countIl[1].Operand.FullName -ceq 'System.Int32 Terraria.ID.MountID::Count' -and
        $countIl[2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ret) (
        'MountID.Count is no longer the reviewed 66-entry initializer.')

    $nativeIdFields = @($mountIdType.Fields | Where-Object {
        $_.HasConstant -and $_.FieldType.FullName -ceq 'System.Int32' -and
        [int]$_.Constant -ge 0
    })
    Assert-Condition ($nativeIdFields.Count -eq 66) (
        "Expected 66 nonnegative MountID constants; found $($nativeIdFields.Count).")
    $nativeIds = @($nativeIdFields | ForEach-Object { [int]$_.Constant } | Sort-Object)
    Assert-Condition (($nativeIds -join ',') -ceq ((0..65) -join ',')) (
        'Native MountID constants are not exactly one continuous 0..65 sequence.')

    $criticalNames = @{
        0 = 'Rudolph'; 6 = 'Minecart'; 7 = 'UFO'; 11 = 'MinecartMech'
        13 = 'MinecartWood'; 23 = 'WitchBroom'; 44 = 'PirateShip'
        49 = 'LavaShark'; 56 = 'Bat'; 61 = 'Pixie'; 62 = 'Chillet'
        63 = 'ChilletIgnis'; 64 = 'TrustyChillet'; 65 = 'TrustyChilletIgnis'
    }
    foreach ($pair in $criticalNames.GetEnumerator()) {
        $named = @($nativeIdFields | Where-Object {
            [int]$_.Constant -eq [int]$pair.Key -and $_.Name -ceq [string]$pair.Value
        })
        Assert-Condition ($named.Count -eq 1) (
            "Critical MountID $($pair.Key) is no longer named $($pair.Value).")
    }

    $mountType = $module.GetType('Terraria.Mount')
    Assert-Condition ($null -ne $mountType) 'Missing Terraria.Mount metadata type.'
    $initialize = Get-UniqueMethod $mountType 'Initialize' @()
    Assert-Condition ($initialize.IsStatic -and
        $initialize.Body.Instructions.Count -eq 6430 -and
        $initialize.Body.CodeSize -eq 17213) 'Mount.Initialize body shape changed.'

    $helperSpecs = @(
        @('SetAsMinecart', @('Terraria.Mount/MountData', 'System.Int32',
            'ReLogic.Content.Asset`1<Microsoft.Xna.Framework.Graphics.Texture2D>',
            'System.Int32', 'System.Int32')),
        @('SetAsHorse', @('Terraria.Mount/MountData', 'System.Int32',
            'ReLogic.Content.Asset`1<Microsoft.Xna.Framework.Graphics.Texture2D>')),
        @('SetAsRollerSkate', @('Terraria.Mount/MountData', 'System.Int32')),
        @('SetAsChillet', @('Terraria.Mount/MountData', 'System.Int32',
            'ReLogic.Content.Asset`1<Microsoft.Xna.Framework.Graphics.Texture2D>',
            'System.Boolean'))
    )
    foreach ($spec in $helperSpecs) {
        $helper = Get-UniqueMethod $mountType $spec[0] $spec[1]
        Assert-Condition ($helper.IsStatic -and $helper.ReturnType.FullName -ceq 'System.Void') (
            "$($helper.FullName) static/return contract changed.")
        Test-ArgumentForwarding $helper 'System.Int32 Terraria.Mount/MountData::buff'
        if ($spec[0] -ceq 'SetAsMinecart') {
            $helperIl = @($helper.Body.Instructions)
            $setsMinecart = 0
            for ($i = 2; $i -lt $helperIl.Count; $i++) {
                if ($helperIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
                    $helperIl[$i].Operand.FullName -ceq 'System.Boolean Terraria.Mount/MountData::Minecart' -and
                    $helperIl[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_0 -and
                    (Get-I4 $helperIl[$i - 1]) -eq 1) { $setsMinecart++ }
            }
            Assert-Condition ($setsMinecart -eq 1) 'SetAsMinecart no longer marks MountData.Minecart true.'
        }
    }

    $initializeIl = @($initialize.Body.Instructions)
    $allocationMatches = 0
    for ($i = 2; $i -lt $initializeIl.Count; $i++) {
        if ($initializeIl[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsfld -and
            $initializeIl[$i - 2].Operand.FullName -ceq 'System.Int32 Terraria.ID.MountID::Count' -and
            $initializeIl[$i - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newarr -and
            $initializeIl[$i - 1].Operand.FullName -ceq 'Terraria.Mount/MountData' -and
            $initializeIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld -and
            $initializeIl[$i].Operand.FullName -ceq 'Terraria.Mount/MountData[] Terraria.Mount::mounts') {
            $allocationMatches++
        }
    }
    Assert-Condition ($allocationMatches -eq 1) (
        'Mount.Initialize no longer allocates mounts exactly once from MountID.Count.')

    $constructors = [Collections.Generic.List[int]]::new()
    for ($i = 0; $i -lt $initializeIl.Count; $i++) {
        if ($initializeIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Newobj -and
            $initializeIl[$i].Operand -is [Mono.Cecil.MethodReference] -and
            $initializeIl[$i].Operand.DeclaringType.FullName -ceq 'Terraria.Mount/MountData' -and
            $initializeIl[$i].Operand.Name -ceq '.ctor' -and
            $initializeIl[$i].Operand.Parameters.Count -eq 0) {
            $constructors.Add($i)
        }
    }
    Assert-Condition ($constructors.Count -eq 66) (
        "Mount.Initialize constructs $($constructors.Count), not 66, MountData records.")

    $nativeMounts = @{}
    for ($block = 0; $block -lt $constructors.Count; $block++) {
        $start = $constructors[$block]
        $end = if ($block + 1 -lt $constructors.Count) {
            $constructors[$block + 1] - 1
        } else {
            $initializeIl.Count - 1
        }

        $storedIds = [Collections.Generic.List[int]]::new()
        for ($i = $start + 3; $i -le [Math]::Min($end, $start + 12); $i++) {
            if ($initializeIl[$i].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stelem_Ref -and
                $initializeIl[$i - 3].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldsfld -and
                $initializeIl[$i - 3].Operand.FullName -ceq 'Terraria.Mount/MountData[] Terraria.Mount::mounts' -and
                $initializeIl[$i - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldloc_0) {
                $storedId = Get-I4 $initializeIl[$i - 2]
                if ($null -ne $storedId) { $storedIds.Add([int]$storedId) }
            }
        }
        Assert-Condition ($storedIds.Count -eq 1) (
            "MountData block at IL_$('{0:X4}' -f $initializeIl[$start].Offset) has $($storedIds.Count) reviewed mounts[] stores.")
        $mountId = $storedIds[0]
        Assert-Condition (-not $nativeMounts.ContainsKey($mountId)) (
            "Mount.Initialize initializes mount $mountId more than once.")

        $buffs = [Collections.Generic.List[int]]::new()
        $isMinecart = $false
        for ($i = $start; $i -le $end; $i++) {
            $instruction = $initializeIl[$i]
            if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
                $instruction.Operand -is [Mono.Cecil.FieldReference] -and
                $instruction.Operand.FullName -ceq 'System.Int32 Terraria.Mount/MountData::buff') {
                Assert-Condition ($i -ge 2 -and
                    $initializeIl[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldloc_0) (
                    "Mount $mountId has an unreviewed direct buff receiver expression.")
                $buff = Get-I4 $initializeIl[$i - 1]
                Assert-Condition ($null -ne $buff) "Mount $mountId has a nonliteral direct buff."
                $buffs.Add([int]$buff)
            }
            if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
                $instruction.Operand -is [Mono.Cecil.FieldReference] -and
                $instruction.Operand.FullName -ceq 'System.Boolean Terraria.Mount/MountData::Minecart') {
                Assert-Condition ($i -ge 2 -and
                    $initializeIl[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldloc_0 -and
                    (Get-I4 $initializeIl[$i - 1]) -eq 1) (
                    "Mount $mountId has an unreviewed direct Minecart assignment.")
                $isMinecart = $true
            }
            if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
                $instruction.Operand -is [Mono.Cecil.MethodReference] -and
                $instruction.Operand.DeclaringType.FullName -ceq 'Terraria.Mount' -and
                $instruction.Operand.Name -in @('SetAsMinecart', 'SetAsHorse',
                    'SetAsRollerSkate', 'SetAsChillet')) {
                $argumentStart = -1
                for ($j = $i - 1; $j -ge $start; $j--) {
                    if ($initializeIl[$j].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldloc_0 -and
                        $null -ne (Get-I4 $initializeIl[$j + 1])) {
                        $argumentStart = $j
                        break
                    }
                }
                Assert-Condition ($argumentStart -ge 0) (
                    "Mount $mountId has an unreviewed $($instruction.Operand.Name) call expression.")
                $buffs.Add([int](Get-I4 $initializeIl[$argumentStart + 1]))
                if ($instruction.Operand.Name -ceq 'SetAsMinecart') { $isMinecart = $true }
            }
        }
        Assert-Condition ($buffs.Count -eq 1) (
            "Mount $mountId resolves to $($buffs.Count), not one, native buff assignment.")
        $nativeMounts[$mountId] = [pscustomobject]@{
            Buff = $buffs[0]
            Minecart = $isMinecart
        }
    }
    $initializedIds = @($nativeMounts.Keys | ForEach-Object { [int]$_ } | Sort-Object)
    Assert-Condition (($initializedIds -join ',') -ceq ((0..65) -join ',')) (
        'Mount.Initialize does not cover every mount ID 0..65 exactly once.')

    # Recover the native item-case entry points. Older item ranges use a chain
    # of equality branches; the current range uses base-subtracted switches.
    $itemType = $module.GetType('Terraria.Item')
    Assert-Condition ($null -ne $itemType) 'Missing Terraria.Item metadata type.'
    $setDefaultsMethods = @()
    foreach ($name in @('SetDefaults1', 'SetDefaults2', 'SetDefaults3',
        'SetDefaults4', 'SetDefaults5')) {
        $setDefaultsMethods += Get-UniqueMethod $itemType $name @('System.Int32')
    }
    $catalogItemIds = [Collections.Generic.HashSet[int]]::new()
    foreach ($catalogEntry in $catalog) {
        if ($catalogEntry.Item -gt 0) { [void]$catalogItemIds.Add($catalogEntry.Item) }
    }
    # A SetDefaults method can contain nested range switches, so retain every
    # structurally valid candidate for a catalog item. Resolve only straight,
    # side-effect-free paths below and require all such resolutions to agree.
    $itemEntryPoints = @{}
    foreach ($method in $setDefaultsMethods) {
        $instructions = @($method.Body.Instructions)
        for ($i = 0; $i -lt $instructions.Count; $i++) {
            $instruction = $instructions[$i]
            if ($instruction.OpCode.Code -in @(
                [Mono.Cecil.Cil.Code]::Bne_Un,
                [Mono.Cecil.Cil.Code]::Bne_Un_S) -and $i -ge 2 -and
                $instructions[$i - 2].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_1) {
                $itemId = Get-I4 $instructions[$i - 1]
                if ($null -ne $itemId -and $catalogItemIds.Contains([int]$itemId)) {
                    if (-not $itemEntryPoints.ContainsKey([int]$itemId)) {
                        $itemEntryPoints[[int]$itemId] =
                            [Collections.Generic.List[object]]::new()
                    }
                    $candidate = [pscustomobject]@{
                        Method = $method; Start = $instruction.Next
                    }
                    $candidateKey = "$($method.MetadataToken.ToInt32()):$($instruction.Next.Offset)"
                    $existingKeys = @($itemEntryPoints[[int]$itemId] | ForEach-Object {
                        "$($_.Method.MetadataToken.ToInt32()):$($_.Start.Offset)"
                    })
                    if ($candidateKey -notin $existingKeys) {
                        $itemEntryPoints[[int]$itemId].Add($candidate)
                    }
                }
            }
            if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Switch) {
                $base = $null
                if ($i -ge 3 -and
                    $instructions[$i - 3].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_1 -and
                    $instructions[$i - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Sub) {
                    $base = Get-I4 $instructions[$i - 2]
                } elseif ($i -ge 1 -and
                    $instructions[$i - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldarg_1) {
                    $base = 0
                }
                Assert-Condition ($null -ne $base) (
                    "Unreviewed item switch expression in $($method.FullName).")
                for ($case = 0; $case -lt $instruction.Operand.Count; $case++) {
                    $itemId = [int]$base + $case
                    if (-not $catalogItemIds.Contains($itemId)) { continue }
                    if (-not $itemEntryPoints.ContainsKey($itemId)) {
                        $itemEntryPoints[$itemId] =
                            [Collections.Generic.List[object]]::new()
                    }
                    $candidate = [pscustomobject]@{
                        Method = $method; Start = $instruction.Operand[$case]
                    }
                    $candidateKey = "$($method.MetadataToken.ToInt32()):$($instruction.Operand[$case].Offset)"
                    $existingKeys = @($itemEntryPoints[$itemId] | ForEach-Object {
                        "$($_.Method.MetadataToken.ToInt32()):$($_.Start.Offset)"
                    })
                    if ($candidateKey -notin $existingKeys) {
                        $itemEntryPoints[$itemId].Add($candidate)
                    }
                }
            }
        }
    }

    $defaultToMinecart = Get-UniqueMethod $itemType 'DefaultToMinecart' @('System.Int32')
    Test-ArgumentForwarding $defaultToMinecart 'System.Int32 Terraria.Item::mountType'

    foreach ($entry in $catalog) {
        $native = $nativeMounts[$entry.Mount]
        Assert-Condition ($entry.Buff -eq $native.Buff) (
            "Catalog mount $($entry.Mount) buff $($entry.Buff) != native $($native.Buff).")
        Assert-Condition ($entry.Minecart -eq $native.Minecart) (
            "Catalog mount $($entry.Mount) Minecart=$($entry.Minecart) != native $($native.Minecart).")
        if ($entry.Item -gt 0) {
            Assert-Condition ($itemEntryPoints.ContainsKey($entry.Item)) (
                "No reviewed native Item.SetDefaults entry point for summon item $($entry.Item).")
            $resolvedCandidates = [Collections.Generic.List[int]]::new()
            $rejectedCandidates = [Collections.Generic.List[string]]::new()
            foreach ($candidate in $itemEntryPoints[$entry.Item]) {
                try {
                    $resolvedCandidates.Add((Resolve-ItemMount $candidate $entry.Item))
                } catch {
                    $rejectedCandidates.Add($_.Exception.Message)
                }
            }
            $distinctMounts = @($resolvedCandidates | Sort-Object -Unique)
            Assert-Condition ($distinctMounts.Count -eq 1) (
                "Item $($entry.Item) yielded $($distinctMounts.Count) agreed native mount bindings from " +
                "$($itemEntryPoints[$entry.Item].Count) candidates; rejected: $($rejectedCandidates -join ' | ')")
            $nativeItemMount = $distinctMounts[0]
            Assert-Condition ($nativeItemMount -eq $entry.Mount) (
                "Catalog summon item $($entry.Item) maps to mount $($entry.Mount), native maps to $nativeItemMount.")
        }
    }
    Assert-Condition (@($catalog | Where-Object Item -eq 0).Count -eq 2) (
        'Expected exactly the two native no-item defaults in the catalog.')

    $criticalMappings = @(
        @([int]7, [int]2769, [int]141, 'ufo'),
        @([int]23, [int]4444, [int]230, 'witch-broom'),
        @([int]56, [int]5597, [int]377, 'bat'),
        @([int]61, [int]5662, [int]384, 'pixie'),
        @([int]62, [int]5665, [int]387, 'chillet'),
        @([int]65, [int]6151, [int]392, 'trusty-chillet-ignis')
    )
    foreach ($expected in $criticalMappings) {
        $actual = @($catalog | Where-Object Mount -eq $expected[0])
        Assert-Condition ($actual.Count -eq 1 -and
            $actual[0].Item -eq $expected[1] -and
            $actual[0].Buff -eq $expected[2] -and
            $actual[0].Key -ceq $expected[3]) (
            "Critical catalog mapping for mount $($expected[0]) changed.")
    }

    $gameHashAfter = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
    $cecilHashAfter = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
    $catalogHashAfter = (Get-FileHash -LiteralPath $catalogFullPath -Algorithm SHA256).Hash
    Assert-Condition ($gameHashAfter -ceq $gameHashBefore) 'Terraria.exe changed during audit.'
    Assert-Condition ($cecilHashAfter -ceq $cecilHashBefore) 'Mono.Cecil changed during audit.'
    Assert-Condition ($catalogHashAfter -ceq $catalogHashBefore) 'VanillaMountCatalog.cs changed during audit.'

    Write-Output "PASS pinned read-only inputs: Terraria $gameHashAfter; Mono.Cecil $cecilHashAfter (0.11.6.0)"
    Write-Output 'PASS native MountID contract: Count=66 and named constants cover 0..65'
    Write-Output 'PASS Mount.Initialize: 66/66 construction slots, buffs, and minecart identities recovered'
    Write-Output 'PASS catalog parity: 66 buffs, 27 minecart flags, and 64 summon item mappings match native IL'
    Write-Output 'PASS control evidence boundary: only mount 23 Witch''s Broom claims ExactDryMotion'
    Write-Output 'PASS metadata-only mount catalog audit. Terraria code was not loaded or executed; audited files are unchanged.'
} finally {
    $module.Dispose()
    [Threading.Thread]::CurrentThread.CurrentCulture = $oldCulture
    [Threading.Thread]::CurrentThread.CurrentUICulture = $oldUiCulture
}

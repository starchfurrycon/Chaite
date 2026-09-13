param(
    [string]$GameExe = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe'
)

# Metadata-only audit. Mono.Cecil reads the PE/IL without loading Terraria into
# the CLR, invoking game code, starting Terraria, or writing to the installation.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$expectedGameHash = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
$expectedCecilHash = 'C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B'
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$gamePath = [IO.Path]::GetFullPath($GameExe)
$cecilPath = Join-Path $projectRoot 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'

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

function Get-R4($Instruction) {
    if ($null -eq $Instruction -or
        $Instruction.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Ldc_R4) {
        return $null
    }
    return [single]$Instruction.Operand
}

function Get-AllTypes($Module) {
    $result = [Collections.Generic.List[object]]::new()
    $pending = [Collections.Generic.Stack[object]]::new()
    foreach ($type in $Module.Types) { $pending.Push($type) }
    while ($pending.Count -gt 0) {
        $type = $pending.Pop()
        $result.Add($type)
        foreach ($nested in $type.NestedTypes) { $pending.Push($nested) }
    }
    return @($result)
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

function Assert-MethodShape($Method, [int]$InstructionCount, [int]$CodeSize) {
    Assert-Condition ($Method.Body.Instructions.Count -eq $InstructionCount -and
        $Method.Body.CodeSize -eq $CodeSize) (
        "$($Method.FullName) no longer has the reviewed body shape " +
        "$InstructionCount instructions/$CodeSize bytes.")
}

function Assert-ConstantField($Type, [string]$Name, [int]$Value,
    [string]$FieldType) {
    $matches = @($Type.Fields | Where-Object {
        $_.Name -ceq $Name -and $_.HasConstant -and
        $_.FieldType.FullName -ceq $FieldType -and [int]$_.Constant -eq $Value
    })
    Assert-Condition ($matches.Count -eq 1) (
        "$($Type.FullName)::$Name is no longer constant $Value ($FieldType).")
}

function Get-FieldStores($Method, [string]$FieldFullName) {
    $instructions = @($Method.Body.Instructions)
    $result = [Collections.Generic.List[object]]::new()
    for ($index = 0; $index -lt $instructions.Count; $index++) {
        $instruction = $instructions[$index]
        if ($instruction.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
            $instruction.Operand -is [Mono.Cecil.FieldReference] -and
            $instruction.Operand.FullName -ceq $FieldFullName) {
            $result.Add([pscustomobject]@{
                Method = $Method
                Instructions = $instructions
                Index = $index
                Instruction = $instruction
                Value = Get-I4 $instructions[$index - 1]
            })
        }
    }
    return @($result)
}

function Assert-ArmorSetRegistration($Initialize, [string]$BenefitName,
    [string]$LocalizationKey, [int[]]$Items) {
    $instructions = @($Initialize.Body.Instructions)
    $matches = 0
    for ($index = 7; $index -lt $instructions.Count; $index++) {
        $call = $instructions[$index]
        if ($call.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Call -or
            -not ($call.Operand -is [Mono.Cecil.MethodReference]) -or
            $call.Operand.FullName -cne
                'System.Void Terraria.DataStructures.ArmorSetBonuses::Add(Terraria.DataStructures.ArmorSetBonus/ArmorSetEffect,System.String,System.Int32,System.Int32,System.Int32)') {
            continue
        }
        $function = $instructions[$index - 6]
        $key = $instructions[$index - 4]
        if ($function.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldftn -and
            $function.Operand.FullName -ceq
                "System.Void Terraria.DataStructures.ArmorSetBonuses/Benefits::$BenefitName(Terraria.Player)" -and
            $key.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and
            [string]$key.Operand -ceq $LocalizationKey -and
            (Get-I4 $instructions[$index - 3]) -eq $Items[0] -and
            (Get-I4 $instructions[$index - 2]) -eq $Items[1] -and
            (Get-I4 $instructions[$index - 1]) -eq $Items[2]) {
            $matches++
        }
    }
    Assert-Condition ($matches -eq 1) (
        "Armor set $BenefitName is not registered exactly once to $($Items -join '/').")
}

function Get-DirectNumericStores($Method, [string]$FieldFullName) {
    $instructions = @($Method.Body.Instructions)
    $values = [Collections.Generic.List[object]]::new()
    for ($index = 1; $index -lt $instructions.Count; $index++) {
        if ($instructions[$index].OpCode.Code -ne [Mono.Cecil.Cil.Code]::Stfld -or
            -not ($instructions[$index].Operand -is [Mono.Cecil.FieldReference]) -or
            $instructions[$index].Operand.FullName -cne $FieldFullName) {
            continue
        }
        $integer = Get-I4 $instructions[$index - 1]
        if ($null -ne $integer) {
            $values.Add([int]$integer)
            continue
        }
        $single = Get-R4 $instructions[$index - 1]
        if ($null -ne $single) { $values.Add([single]$single) }
    }
    return @($values)
}

function Assert-NumericSet($Actual, $Expected, [string]$Message) {
    $actualText = (@($Actual | ForEach-Object {
        ([double]$_).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    }) | Sort-Object) -join ','
    $expectedText = (@($Expected | ForEach-Object {
        ([double]$_).ToString('R', [Globalization.CultureInfo]::InvariantCulture)
    }) | Sort-Object) -join ','
    Assert-Condition ($actualText -ceq $expectedText) (
        "$Message Expected [$expectedText], found [$actualText].")
}

Assert-Condition (Test-Path -LiteralPath $gamePath -PathType Leaf) "Missing Terraria executable: $gamePath"
Assert-Condition (Test-Path -LiteralPath $cecilPath -PathType Leaf) (
    "Missing Mono.Cecil build input: $cecilPath. Run tools/build.ps1 -Configuration Release first.")

$gameHashBefore = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
$cecilHashBefore = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
Assert-Condition ($gameHashBefore -ceq $expectedGameHash) (
    "Unsupported Terraria.exe SHA-256 $gameHashBefore; expected $expectedGameHash.")
Assert-Condition ($cecilHashBefore -ceq $expectedCecilHash) (
    "Unsupported Mono.Cecil SHA-256 $cecilHashBefore; expected $expectedCecilHash.")

$version = (Get-Item -LiteralPath $gamePath).VersionInfo
Assert-Condition ($version.FileVersion -ceq '1.4.5.8' -and
    $version.ProductVersion -ceq '1.4.5.8') 'Terraria file/product version is not 1.4.5.8.'

$oldCulture = [Threading.Thread]::CurrentThread.CurrentCulture
$oldUiCulture = [Threading.Thread]::CurrentThread.CurrentUICulture
$invariant = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentCulture = $invariant
[Threading.Thread]::CurrentThread.CurrentUICulture = $invariant

Add-Type -Path $cecilPath
$module = $null
try {
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($gamePath)
    Assert-Condition ($module.Architecture.ToString() -ceq 'I386') 'Terraria PE is no longer x86/I386.'
    Assert-Condition ($module.Assembly.Name.Name -ceq 'Terraria' -and
        $module.Assembly.Name.Version.ToString() -ceq '1.4.5.8') (
        'Terraria assembly identity/version changed.')

    $player = $module.GetType('Terraria.Player')
    $mount = $module.GetType('Terraria.Mount')
    $itemId = $module.GetType('Terraria.ID.ItemID')
    $mountId = $module.GetType('Terraria.ID.MountID')
    $mountSets = $module.GetType('Terraria.ID.MountID/Sets')
    $armorSets = $module.GetType('Terraria.DataStructures.ArmorSetBonuses')
    $armorBenefits = $module.GetType('Terraria.DataStructures.ArmorSetBonuses/Benefits')
    foreach ($required in @($player, $mount, $itemId, $mountId, $mountSets,
        $armorSets, $armorBenefits)) {
        Assert-Condition ($null -ne $required) 'A required Terraria metadata type is missing.'
    }

    $itemConstants = @(
        @('Tabi', 977), @('MasterNinjaGear', 984), @('EoCShield', 3097),
        @('SolarFlareHelmet', 2763), @('SolarFlareBreastplate', 2764),
        @('SolarFlareLeggings', 2765), @('CrystalNinjaHelmet', 4982),
        @('CrystalNinjaChestplate', 4983), @('CrystalNinjaLeggings', 4984),
        @('DeadCellsRamRune', 5465), @('PalworldPetChillet', 5665),
        @('PalworldPetChilletIgnis', 5666), @('PalworldMountTrustyChillet', 6150),
        @('PalworldMountTrustyChilletIgnis', 6151), @('OldStyleParkourBook', 6190),
        @('OldStyleParkourBookInactive', 6195)
    )
    foreach ($entry in $itemConstants) {
        Assert-ConstantField $itemId $entry[0] $entry[1] 'System.Int16'
    }
    $mountConstants = @(
        @('Bat', 56), @('RollerSkates', 57), @('RollerSkatesGreen', 58),
        @('RollerSkatesWhite', 59), @('RollerSkatesPink', 60), @('Pixie', 61),
        @('Chillet', 62), @('ChilletIgnis', 63), @('TrustyChillet', 64),
        @('TrustyChilletIgnis', 65)
    )
    foreach ($entry in $mountConstants) {
        Assert-ConstantField $mountId $entry[0] $entry[1] 'System.Int32'
    }

    # Exhaust every stfld Player::dashType in every native type and method.
    $dashTypeField = 'System.Int32 Terraria.Player::dashType'
    $allStores = [Collections.Generic.List[object]]::new()
    foreach ($type in (Get-AllTypes $module)) {
        foreach ($method in $type.Methods) {
            if (-not $method.HasBody) { continue }
            foreach ($store in (Get-FieldStores $method $dashTypeField)) {
                Assert-Condition ($null -ne $store.Value) (
                    "Nonliteral dashType write found in $($method.FullName) at $($store.Instruction).")
                $allStores.Add($store)
            }
        }
    }
    Assert-Condition ($allStores.Count -eq 9) (
        "Expected 9 exhaustive dashType stores; found $($allStores.Count).")
    $actualStoreSignatures = @($allStores | ForEach-Object {
        "$($_.Method.DeclaringType.FullName)::$($_.Method.Name)|$($_.Value)"
    } | Sort-Object)
    $expectedStoreSignatures = @(
        'Terraria.DataStructures.ArmorSetBonuses/Benefits::CrystalAssassin|5',
        'Terraria.Mount::SetMount|0',
        'Terraria.Player::ApplyEquipFunctional|1',
        'Terraria.Player::ApplyEquipFunctional|1',
        'Terraria.Player::ApplyEquipFunctional|2',
        'Terraria.Player::ApplySetBonus_Solar|3',
        'Terraria.Player::DashMovement|6',
        'Terraria.Player::ResetEffects|0',
        'Terraria.Player::UpdateBuffs|0'
    ) | Sort-Object
    Assert-Condition (($actualStoreSignatures -join "`n") -ceq
        ($expectedStoreSignatures -join "`n")) (
        "dashType write identity set changed:`n$($actualStoreSignatures -join "`n")")
    $dashValues = @($allStores | ForEach-Object { [int]$_.Value } | Sort-Object -Unique)
    Assert-Condition (($dashValues -join ',') -ceq '0,1,2,3,5,6') (
        "Reachable dashType literal set changed to [$($dashValues -join ',')].")
    Assert-Condition (4 -notin $dashValues) 'Legacy dashType 4 unexpectedly gained a native source.'

    $applyEquip = Get-UniqueMethod $player 'ApplyEquipFunctional' @('System.Int32', 'Terraria.Item')
    Assert-MethodShape $applyEquip 4012 11619
    $equipStores = @(Get-FieldStores $applyEquip $dashTypeField)
    $equipMappings = [Collections.Generic.List[string]]::new()
    foreach ($store in $equipStores) {
        $sourceItem = $null
        for ($index = $store.Index - 2; $index -ge [Math]::Max(0, $store.Index - 12); $index--) {
            if ($store.Instructions[$index].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldfld -and
                $store.Instructions[$index].Operand.FullName -ceq 'System.Int32 Terraria.Item::type' -and
                $index + 2 -lt $store.Instructions.Count -and
                $null -ne (Get-I4 $store.Instructions[$index + 1]) -and
                $store.Instructions[$index + 2].OpCode.FlowControl -eq
                    [Mono.Cecil.Cil.FlowControl]::Cond_Branch) {
                $sourceItem = Get-I4 $store.Instructions[$index + 1]
                break
            }
        }
        Assert-Condition ($null -ne $sourceItem) (
            "Could not resolve equip identity for dashType store $($store.Instruction).")
        $equipMappings.Add("$sourceItem|$($store.Value)")
    }
    Assert-Condition ((@($equipMappings | Sort-Object) -join ',') -ceq
        '3097|2,977|1,984|1') (
        "Functional equip dash mappings changed: $($equipMappings -join ',').")

    $initializeSets = Get-UniqueMethod $armorSets 'Initialize' @()
    Assert-MethodShape $initializeSets 909 3539
    Assert-ArmorSetRegistration $initializeSets 'CrystalAssassin' `
        'ArmorSetBonus.CrystalNinja' @(4982, 4983, 4984)
    Assert-ArmorSetRegistration $initializeSets 'Solar' `
        'ArmorSetBonus.Solar' @(2763, 2764, 2765)

    $crystal = Get-UniqueMethod $armorBenefits 'CrystalAssassin' @('Terraria.Player')
    $solarBenefit = Get-UniqueMethod $armorBenefits 'Solar' @('Terraria.Player')
    Assert-MethodShape $crystal 46 125
    Assert-MethodShape $solarBenefit 3 7
    $solarBenefitCalls = @($solarBenefit.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call,
            [Mono.Cecil.Cil.Code]::Callvirt) -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.FullName -ceq 'System.Void Terraria.Player::ApplySetBonus_Solar()'
    })
    Assert-Condition ($solarBenefitCalls.Count -eq 1) (
        'Solar armor benefit no longer forwards exactly once to ApplySetBonus_Solar.')

    # MountID.Sets.CanDash is the exact static array 56..65. Only DashMovement's
    # separately audited 62..65 branch creates type 6.
    $mountSetsCctor = Get-UniqueMethod $mountSets '.cctor' @()
    $setsIl = @($mountSetsCctor.Body.Instructions)
    $canDashStores = @()
    for ($index = 0; $index -lt $setsIl.Count; $index++) {
        if ($setsIl[$index].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stsfld -and
            $setsIl[$index].Operand.FullName -ceq
                'System.Boolean[] Terraria.ID.MountID/Sets::CanDash') {
            $canDashStores += $index
        }
    }
    Assert-Condition ($canDashStores.Count -eq 1) 'MountID.Sets.CanDash store count changed.'
    $canDashStore = $canDashStores[0]
    $arrayField = $null
    for ($index = $canDashStore - 1; $index -ge [Math]::Max(0, $canDashStore - 12); $index--) {
        if ($setsIl[$index].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldtoken -and
            $setsIl[$index].Operand -is [Mono.Cecil.FieldReference]) {
            $arrayField = $setsIl[$index].Operand.Resolve()
            break
        }
    }
    Assert-Condition ($null -ne $arrayField -and $arrayField.HasFieldRVA) (
        'Could not resolve native CanDash static-array data.')
    $canDashValues = [Collections.Generic.List[int]]::new()
    for ($offset = 0; $offset -lt $arrayField.InitialValue.Length; $offset += 4) {
        $canDashValues.Add([BitConverter]::ToInt32($arrayField.InitialValue, $offset))
    }
    Assert-Condition (($canDashValues -join ',') -ceq '56,57,58,59,60,61,62,63,64,65') (
        "MountID.Sets.CanDash changed to [$($canDashValues -join ',')].")

    $setAsChillet = Get-UniqueMethod $mount 'SetAsChillet' @(
        'Terraria.Mount/MountData', 'System.Int32',
        'ReLogic.Content.Asset`1<Microsoft.Xna.Framework.Graphics.Texture2D>',
        'System.Boolean')
    Assert-MethodShape $setAsChillet 246 588
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Int32 Terraria.Mount/MountData::heightBoost') @(4) 'Chillet heightBoost changed.'
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Single Terraria.Mount/MountData::fallDamage') @([single]0.5) 'Chillet fallDamage changed.'
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Single Terraria.Mount/MountData::runSpeed') @([single]3, [single]3) 'Chillet runSpeed changed.'
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Single Terraria.Mount/MountData::dashSpeed') @([single]6.5, [single]9) 'Chillet dashSpeed changed.'
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Single Terraria.Mount/MountData::acceleration') @([single]0.32, [single]0.32) 'Chillet acceleration changed.'
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Int32 Terraria.Mount/MountData::jumpHeight') @(6, 11) 'Chillet jumpHeight changed.'
    Assert-NumericSet (Get-DirectNumericStores $setAsChillet `
        'System.Single Terraria.Mount/MountData::jumpSpeed') @([single]8.01, [single]8.1) 'Chillet jumpSpeed changed.'

    $dashMovement = Get-UniqueMethod $player 'DashMovement' @()
    $commonDash = Get-UniqueMethod $player 'DoCommonDashHandle' @(
        'System.Int32&', 'System.Boolean&', 'Terraria.Player/DashStartAction')
    Assert-MethodShape $dashMovement 2787 8086
    Assert-MethodShape $commonDash 157 312
    $dashIl = @($dashMovement.Body.Instructions)

    # These are only the constants that immediately feed velocity.X stores,
    # excluding unrelated dust/frame constants in the same method.
    $startSpeeds = [Collections.Generic.List[single]]::new()
    for ($index = 0; $index + 4 -lt $dashIl.Count; $index++) {
        $speed = Get-R4 $dashIl[$index]
        if ($null -ne $speed -and
            $dashIl[$index + 4].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and
            $dashIl[$index + 4].Operand.FullName -ceq
                'System.Single Microsoft.Xna.Framework.Vector2::X' -and
            $dashIl[$index - 1].OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldflda -and
            $dashIl[$index - 1].Operand.FullName -ceq
                'Microsoft.Xna.Framework.Vector2 Terraria.Entity::velocity') {
            $startSpeeds.Add([single]$speed)
        }
    }
    Assert-NumericSet $startSpeeds @([single]14.5, [single]16,
        [single]16.9, [single]16.9, [single]21.9) 'Horizontal dash start speeds changed.'

    $solidProbeCalls = @($dashIl | Where-Object {
        $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Call -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.FullName -ceq
            'System.Boolean Terraria.WorldGen::SolidOrSlopedTile(System.Int32,System.Int32,System.Boolean)'
    })
    Assert-Condition ($solidProbeCalls.Count -eq 10) (
        "Expected two native forward probes for five dash starts; found $($solidProbeCalls.Count).")
    $negativeDelayStores = @((Get-FieldStores $dashMovement `
        'System.Int32 Terraria.Player::dashDelay') | Where-Object Value -eq -1)
    Assert-Condition ($negativeDelayStores.Count -eq 5) (
        "Expected five dashDelay=-1 start stores; found $($negativeDelayStores.Count).")

    # Resource and down-dash methods are shape-pinned in addition to the whole
    # executable hash so extraction bugs cannot silently select a similarly
    # named method.
    $solarSet = Get-UniqueMethod $player 'ApplySetBonus_Solar' @()
    $solarStart = Get-UniqueMethod $player 'SolarDashStart' @('System.Int32')
    $consumeSolar = Get-UniqueMethod $player 'ConsumeSolarFlare' @()
    Assert-MethodShape $solarSet 275 736
    Assert-MethodShape $solarStart 7 15
    Assert-MethodShape $consumeSolar 63 142

    $jumpMovement = Get-UniqueMethod $player 'JumpMovement' @()
    $groundPound = Get-UniqueMethod $player 'DoDeadCellsGroundPoundEffect' @()
    $update = Get-UniqueMethod $player 'Update' @('System.Int32')
    $refreshMovement = Get-UniqueMethod $player 'RefreshMovementAbilities' @('System.Boolean')
    $refreshJumps = Get-UniqueMethod $player 'RefreshDoubleJumps' @()
    $grappleMovement = Get-UniqueMethod $player 'GrappleMovement' @()
    Assert-MethodShape $jumpMovement 2605 7892
    Assert-MethodShape $groundPound 124 321
    Assert-MethodShape $update 12757 36687

    $ramStores = @(Get-FieldStores $applyEquip `
        'System.Boolean Terraria.Player::hasDeadCellsDownDash')
    Assert-Condition ($ramStores.Count -eq 1 -and $ramStores[0].Value -eq 1) (
        'Ram Rune no longer uniquely grants hasDeadCellsDownDash=true.')
    $ramStore = $ramStores[0]
    $ramIdentityFound = $false
    for ($index = [Math]::Max(0, $ramStore.Index - 12); $index -lt $ramStore.Index; $index++) {
        if ((Get-I4 $ramStore.Instructions[$index]) -eq 5465 -and
            $ramStore.Instructions[$index + 1].OpCode.FlowControl -eq
                [Mono.Cecil.Cil.FlowControl]::Cond_Branch) {
            $ramIdentityFound = $true
        }
    }
    Assert-Condition $ramIdentityFound 'Ram Rune item 5465 grant branch changed.'

    $grappleRefreshCalls = @($grappleMovement.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call,
            [Mono.Cecil.Cil.Code]::Callvirt) -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.FullName -ceq
            'System.Void Terraria.Player::RefreshMovementAbilities(System.Boolean)'
    })
    Assert-Condition ($grappleRefreshCalls.Count -eq 1) (
        'GrappleMovement no longer refreshes/cancels the reviewed jump state exactly once.')
    $refreshCalls = @($refreshMovement.Body.Instructions | Where-Object {
        $_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call,
            [Mono.Cecil.Cil.Code]::Callvirt) -and
        $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.FullName -ceq 'System.Void Terraria.Player::RefreshDoubleJumps()'
    })
    Assert-Condition ($refreshCalls.Count -eq 1) (
        'RefreshMovementAbilities no longer reaches RefreshDoubleJumps exactly once.')
    $refreshDownDashStores = @(Get-FieldStores $refreshJumps `
        'System.Boolean Terraria.Player::isPerformingJump_DownDash')
    Assert-Condition ($refreshDownDashStores.Count -eq 1 -and
        $refreshDownDashStores[0].Value -eq 0) (
        'RefreshDoubleJumps no longer cancels the Ram Rune down-dash state.')

    $gameHashAfter = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
    $cecilHashAfter = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
    Assert-Condition ($gameHashAfter -ceq $gameHashBefore) 'Terraria.exe changed during the audit.'
    Assert-Condition ($cecilHashAfter -ceq $cecilHashBefore) 'Mono.Cecil changed during the audit.'

    Write-Output "PASS pinned read-only inputs: Terraria $gameHashAfter; Mono.Cecil $cecilHashAfter (0.11.6.0)"
    Write-Output 'PASS exhaustive dashType stores: 9 writes; reachable literals are exactly 0/1/2/3/5/6; type 4 has no source'
    Write-Output 'PASS source identities: Tabi/MNG/EoC Shield, Crystal Assassin, Solar, Chillet 62..65, and Ram Rune 5465'
    Write-Output 'PASS horizontal state machine: five starts, two solid probes each, exact start speeds, active/cooldown method shapes'
    Write-Output 'PASS joint-state evidence: CanDash mounts 56..65, Chillet native stats, Solar resource methods, grapple cancellation of Ram down-dash'
    Write-Output 'PASS metadata-only dash audit. Terraria code was not loaded or executed; audited inputs are unchanged.'
} finally {
    if ($null -ne $module) { $module.Dispose() }
    [Threading.Thread]::CurrentThread.CurrentCulture = $oldCulture
    [Threading.Thread]::CurrentThread.CurrentUICulture = $oldUiCulture
}

param(
    [string]$GameExe = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe',
    [switch]$AsJson
)

# Read-only, hash-locked metadata audit for the first special-ranged tranche.
# Mono.Cecil parses PE/IL only: this script never loads Terraria into the CLR,
# starts the game, invokes PickAmmo, consumes ammo, or touches a player/world save.
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

function New-Weapon([int]$Id, [string]$Name, [int]$Damage,
    [int]$UseTime, [int]$UseAnimation, [single]$ShootSpeed,
    [bool]$Channel, [int]$HeldProjectile, [string]$Model) {
    [pscustomobject]@{
        id = $Id; name = $Name; damage = $Damage; useTime = $UseTime
        useAnimation = $UseAnimation; shootSpeed = $ShootSpeed
        channel = $Channel; heldProjectile = $HeldProjectile; model = $Model
    }
}

function New-Ammo([int]$Id, [string]$Name, [int]$Damage,
    [string]$NativeWorldEffect) {
    [pscustomobject]@{
        id = $Id; name = $Name; damage = $Damage
        shootSpeed = [single]0; nativeWorldEffect = $NativeWorldEffect
    }
}

function New-Mapping([int]$Launcher, [int]$Ammo, [int]$Projectile) {
    [pscustomobject]@{ launcher = $Launcher; ammo = $Ammo; projectile = $Projectile }
}

Assert-Condition (Test-Path -LiteralPath $gamePath -PathType Leaf) "Missing Terraria executable: $gamePath"
Assert-Condition (Test-Path -LiteralPath $cecilPath -PathType Leaf) (
    "Missing Mono.Cecil build input: $cecilPath. Run tools/build.ps1 -Configuration Release first.")

$gameHashBefore = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
$cecilHash = (Get-FileHash -LiteralPath $cecilPath -Algorithm SHA256).Hash
Assert-Condition ($gameHashBefore -ceq $expectedGameHash) (
    "Unsupported Terraria.exe SHA-256 $gameHashBefore; expected $expectedGameHash.")
Assert-Condition ($cecilHash -ceq $expectedCecilHash) (
    "Unsupported Mono.Cecil SHA-256 $cecilHash; expected $expectedCecilHash.")

$version = (Get-Item -LiteralPath $gamePath).VersionInfo
Assert-Condition ($version.FileVersion -ceq '1.4.5.8' -and
    $version.ProductVersion -ceq '1.4.5.8') 'Terraria file/product version is not 1.4.5.8.'

Add-Type -Path $cecilPath
$module = $null
try {
    $module = [Mono.Cecil.ModuleDefinition]::ReadModule($gamePath)
    Assert-Condition ($module.Architecture.ToString() -ceq 'I386') 'Terraria PE is no longer x86/I386.'
    Assert-Condition ($module.Assembly.Name.Name -ceq 'Terraria' -and
        $module.Assembly.Name.Version.ToString() -ceq '1.4.5.8') (
        'Terraria assembly identity/version changed.')

    $item = $module.GetType('Terraria.Item')
    $player = $module.GetType('Terraria.Player')
    $projectile = $module.GetType('Terraria.Projectile')
    $ammoSets = $module.GetType('Terraria.ID.AmmoID/Sets')
    $itemSets = $module.GetType('Terraria.ID.ItemID/Sets')
    foreach ($required in @($item, $player, $projectile, $ammoSets, $itemSets)) {
        Assert-Condition ($null -ne $required) 'A required Terraria metadata type is missing.'
    }

    # Whole-method shapes are deliberately strict. Together with the PE hash,
    # they prevent a later game build from silently inheriting this catalog.
    foreach ($shape in @(
        @($item, 'SetDefaults1', @('System.Int32'), 33202, 87746),
        @($item, 'SetDefaults2', @('System.Int32'), 24525, 63906),
        @($item, 'SetDefaults3', @('System.Int32'), 17363, 45191),
        @($item, 'SetDefaults4', @('System.Int32'), 22207, 58068),
        @($item, 'SetDefaults5', @('System.Int32'), 32250, 90972),
        @($player, 'ItemCheck_Shoot', @('System.Int32', 'Terraria.Item', 'System.Int32', 'System.Boolean'), 12829, 34007),
        @($player, 'PickAmmo', @('Terraria.Item', 'System.Int32&', 'System.Single&', 'System.Boolean&', 'System.Int32&', 'System.Single&', 'System.Int32&', 'System.Boolean'), 617, 1473),
        @($player, 'DestroyOldestProximityMinesOverMinesCap', @('System.Int32'), 118, 331),
        @($projectile, 'SetDefaults', @('System.Int32'), 27110, 75578),
        @($projectile, 'AI', @(), 71166, 194201),
        @($projectile, 'AI_016_Bombs', @(), 3666, 11787),
        @($projectile, 'AI_075', @(), 5041, 14454),
        @($projectile, 'PrepareBombToBlow', @(), 457, 1499),
        @($projectile, 'Damage_CanDealDamage', @(), 644, 2033),
        @($projectile, 'Kill', @(), 52134, 150321),
        @($projectile, 'Kill_ExplodeTiles', @(), 1392, 3820),
        @($projectile, 'Kill_Bombs_DoUsualKillCode', @(), 5458, 16488),
        @($ammoSets, '.cctor', @(), 337, 1243),
        @($itemSets, '.cctor', @(), 7521, 22968)
    )) {
        $method = Get-UniqueMethod $shape[0] $shape[1] $shape[2]
        Assert-MethodShape $method $shape[3] $shape[4]
    }

    $weapons = @(
        (New-Weapon 758 'Grenade Launcher' 60 20 20 10 $false 0 'delayed-gravity-grenade'),
        (New-Weapon 759 'Rocket Launcher' 55 30 30 5 $false 0 'component-capped-exponential-rocket'),
        (New-Weapon 760 'Proximity Mine Launcher' 80 50 50 12 $false 0 'damped-gravity-placement'),
        (New-Weapon 1946 'Snowman Cannon' 67 15 15 15 $false 0 'delayed-los-homing-rocket'),
        (New-Weapon 2796 'Electrosphere Launcher' 40 12 12 12 $false 0 'target-tile-area-placement'),
        (New-Weapon 2797 'Xenopopper' 45 21 21 12 $false 0 'delayed-cursor-release'),
        (New-Weapon 3475 'Vortex Beater' 50 20 20 20 $true 615 'held-five-update-sequence'),
        (New-Weapon 3930 'Celebration Mk2' 50 6 6 17 $true 714 'held-seven-state-sequence')
    )

    $rocketAmmo = @(
        (New-Ammo 771 'Rocket I' 40 'none'),
        (New-Ammo 772 'Rocket II' 40 'destroy-tiles'),
        (New-Ammo 773 'Rocket III' 65 'none'),
        (New-Ammo 774 'Rocket IV' 65 'destroy-tiles'),
        (New-Ammo 4445 'Cluster Rocket I' 50 'cluster-fragments'),
        (New-Ammo 4446 'Cluster Rocket II' 50 'destroy-tiles-and-cluster-fragments'),
        (New-Ammo 4447 'Wet Rocket' 40 'add-water'),
        (New-Ammo 4448 'Lava Rocket' 40 'add-lava'),
        (New-Ammo 4449 'Honey Rocket' 40 'add-honey'),
        (New-Ammo 4457 'Mini Nuke I' 75 'none'),
        (New-Ammo 4458 'Mini Nuke II' 75 'destroy-tiles'),
        (New-Ammo 4459 'Dry Rocket' 40 'remove-liquid')
    )

    # Exact AmmoID.Sets.SpecificLauncherAmmoProjectileMatches rows.
    $mappings = @(
        (New-Mapping 759 771 134), (New-Mapping 759 772 137),
        (New-Mapping 759 773 140), (New-Mapping 759 774 143),
        (New-Mapping 759 4445 776), (New-Mapping 759 4446 780),
        (New-Mapping 759 4457 793), (New-Mapping 759 4458 796),
        (New-Mapping 759 4459 799), (New-Mapping 759 4447 784),
        (New-Mapping 759 4448 787), (New-Mapping 759 4449 790),

        (New-Mapping 758 771 133), (New-Mapping 758 772 136),
        (New-Mapping 758 773 139), (New-Mapping 758 774 142),
        (New-Mapping 758 4445 777), (New-Mapping 758 4446 781),
        (New-Mapping 758 4457 794), (New-Mapping 758 4458 797),
        (New-Mapping 758 4459 800), (New-Mapping 758 4447 785),
        (New-Mapping 758 4448 788), (New-Mapping 758 4449 791),

        (New-Mapping 760 771 135), (New-Mapping 760 772 138),
        (New-Mapping 760 773 141), (New-Mapping 760 774 144),
        (New-Mapping 760 4445 778), (New-Mapping 760 4446 782),
        (New-Mapping 760 4457 795), (New-Mapping 760 4458 798),
        (New-Mapping 760 4459 801), (New-Mapping 760 4447 786),
        (New-Mapping 760 4448 789), (New-Mapping 760 4449 792),

        (New-Mapping 1946 771 338), (New-Mapping 1946 772 339),
        (New-Mapping 1946 773 340), (New-Mapping 1946 774 341),
        (New-Mapping 1946 4445 803), (New-Mapping 1946 4446 804),
        (New-Mapping 1946 4457 808), (New-Mapping 1946 4458 809),
        (New-Mapping 1946 4459 810), (New-Mapping 1946 4447 805),
        (New-Mapping 1946 4448 806), (New-Mapping 1946 4449 807),

        (New-Mapping 3930 771 715), (New-Mapping 3930 772 716),
        (New-Mapping 3930 773 717), (New-Mapping 3930 774 718),
        (New-Mapping 3930 4445 717), (New-Mapping 3930 4446 718),
        (New-Mapping 3930 4457 717), (New-Mapping 3930 4458 718),
        (New-Mapping 3930 4459 717), (New-Mapping 3930 4447 717),
        (New-Mapping 3930 4448 717), (New-Mapping 3930 4449 717)
    )

    Assert-Condition ($weapons.Count -eq 8) 'Reviewed weapon count changed.'
    Assert-Condition ($rocketAmmo.Count -eq 12) 'Reviewed rocket-ammo count changed.'
    Assert-Condition ($mappings.Count -eq 60) 'Launcher mapping count changed.'
    foreach ($launcher in @(758, 759, 760, 1946, 3930)) {
        $rows = @($mappings | Where-Object { $_.launcher -eq $launcher })
        Assert-Condition ($rows.Count -eq 12) "Launcher $launcher no longer has 12 explicit rows."
        Assert-Condition ((@($rows.ammo | Sort-Object -Unique)).Count -eq 12) (
            "Launcher $launcher contains duplicate or missing ammo identities.")
    }

    $facts = [ordered]@{
        grenadeGravityBeginsProjectileUpdate = 16
        straightRocketVelocityMultiplier = [single]1.1
        straightRocketComponentThreshold = [single]15
        proximityGravityBeforeRetention = [single]0.2
        proximityVelocityRetention = [single]0.97
        proximityOwnedMineCap = 20
        grenadeFuseGameTicks = 180
        snowmanHomingBeginsProjectileUpdate = 31
        snowmanAcquireManhattanPixels = [single]600
        snowmanRetainManhattanPixels = [single]1000
        snowmanTargetSpeedPerUpdate = [single]16
        snowmanHomingLerp = [single](1.0 / 12.0)
        xenopopperGuaranteedBubbles = 4
        xenopopperFifthBubbleChance = [single]0.25
        xenopopperLifetimeProjectileUpdates = 60
        xenopopperProjectileExtraUpdates = 1
        xenopopperInitialSpeedFactorMinimum = [single]0.05
        xenopopperInitialSpeedFactorMaximumExclusive = [single]0.25
        xenopopperInitialHalfConeRadians = [single]([Math]::PI / 3.0)
        xenopopperVelocityRetention = [single]0.96
        xenopopperAngularErrorRetention = [single]0.95
        xenopopperBubbleDealsDamage = $false
        electrosphereTravelProjectile = 442
        electrosphereTravelDealsDirectDamage = $false
        electrosphereAreaProjectile = 443
        electrosphereTravelFailsafeCounter = 120
        electrosphereAreaSpawnedLifetimeUpdates = 300
        electrosphereAreaNpcOwnerImmunityUpdates = 8
        electrosphereAreaMinimumIntegerSize = 62
        electrosphereAreaMaximumIntegerSize = 80
        vortexHeldProjectile = 615
        vortexFireIntervalUpdates = 5
        vortexBonusRocketEveryEvents = 7
        vortexBonusRocketProjectile = 616
        vortexBonusRocketDamageAddition = 20
        vortexIntrinsicNoConsumeProbability = [single](2.0 / 3.0)
        vortexBonusRocketLifetimeGameTicks = 90
        celebrationHeldProjectile = 714
        celebrationFireIntervalUpdates = 8
        celebrationSequenceLengthEvents = 7
        celebrationProjectilesPerFullSequence = 10
        celebrationIntrinsicNoConsumeProbability = [single]0.5
        clusterChildCount = 6
        clusterChildLifetimeUpdates = 60
        snowmanMiniNukeTwoPrepareHitboxRegression = 'projectile 809 is omitted; 808 is compared twice'
    }

    $report = [pscustomobject]@{
        schema = 'chaite-common-special-ranged-native-audit/v1'
        evidence = [pscustomobject]@{
            gamePath = $gamePath
            fileVersion = $version.FileVersion
            productVersion = $version.ProductVersion
            sha256 = $gameHashBefore
            architecture = $module.Architecture.ToString()
            assemblyVersion = $module.Assembly.Name.Version.ToString()
            readOnlyMetadataAudit = $true
        }
        weapons = $weapons
        rocketAmmo = $rocketAmmo
        launcherMappings = $mappings
        facts = [pscustomobject]$facts
        evidenceLimit = 'Static native IL contract only; no game launch, shot, boss fight, hit-rate, or win-rate evidence.'
    }
} finally {
    if ($null -ne $module) { $module.Dispose() }
}

$gameHashAfter = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
Assert-Condition ($gameHashAfter -ceq $gameHashBefore) 'Terraria.exe changed during the read-only audit.'

if ($AsJson) {
    $report | ConvertTo-Json -Depth 8
} else {
    $report
}

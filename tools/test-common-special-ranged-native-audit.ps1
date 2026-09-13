$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$auditScript = Join-Path $PSScriptRoot 'audit-common-special-ranged-native.ps1'
$passed = 0
$failed = 0

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $script:passed++
        Write-Output "PASS $Name"
    } catch {
        $script:failed++
        Write-Output "FAIL $Name -- $($_.Exception.Message)"
    }
}

function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
}

if (-not (Test-Path -LiteralPath $auditScript -PathType Leaf)) {
    throw 'Missing common special-ranged native audit script.'
}

$terrariaBefore = @(Get-Process -Name Terraria -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
$report = & $auditScript
$terrariaAfter = @(Get-Process -Name Terraria -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })

Test-Case 'hash-version-and-read-only-evidence' {
    Assert-True ($report.schema -ceq 'chaite-common-special-ranged-native-audit/v1') 'Wrong report schema.'
    Assert-True ($report.evidence.sha256 -ceq '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3') 'Wrong game hash.'
    Assert-True ($report.evidence.fileVersion -ceq '1.4.5.8' -and
        $report.evidence.productVersion -ceq '1.4.5.8') 'Wrong game version.'
    Assert-True ($report.evidence.readOnlyMetadataAudit -eq $true) 'Audit is not marked metadata-only.'
}

Test-Case 'audit-did-not-start-or-stop-Terraria' {
    Assert-True (($terrariaBefore -join ',') -ceq ($terrariaAfter -join ',')) (
        'Terraria process identity set changed during audit.')
}

Test-Case 'exact-reviewed-weapon-identities' {
    $ids = @($report.weapons.id | Sort-Object)
    Assert-True (($ids -join ',') -ceq '758,759,760,1946,2796,2797,3475,3930') (
        "Weapon identities changed: $($ids -join ',').")
    Assert-True (@($report.weapons).Count -eq 8) 'Weapon count is not eight.'
}

Test-Case 'exact-rocket-ammo-identities-and-default-zero-speed' {
    $ids = @($report.rocketAmmo.id | Sort-Object)
    Assert-True (($ids -join ',') -ceq '771,772,773,774,4445,4446,4447,4448,4449,4457,4458,4459') (
        "Rocket-ammo identities changed: $($ids -join ',').")
    Assert-True (@($report.rocketAmmo | Where-Object { $_.shootSpeed -ne 0 }).Count -eq 0) (
        'A reviewed rocket ammo unexpectedly has non-zero default shoot speed.')
}

Test-Case 'all-five-launchers-have-the-exact-projectile-vector' {
    $expected = @{
        758 = '771:133,772:136,773:139,774:142,4445:777,4446:781,4447:785,4448:788,4449:791,4457:794,4458:797,4459:800'
        759 = '771:134,772:137,773:140,774:143,4445:776,4446:780,4447:784,4448:787,4449:790,4457:793,4458:796,4459:799'
        760 = '771:135,772:138,773:141,774:144,4445:778,4446:782,4447:786,4448:789,4449:792,4457:795,4458:798,4459:801'
        1946 = '771:338,772:339,773:340,774:341,4445:803,4446:804,4447:805,4448:806,4449:807,4457:808,4458:809,4459:810'
        3930 = '771:715,772:716,773:717,774:718,4445:717,4446:718,4447:717,4448:717,4449:717,4457:717,4458:718,4459:717'
    }
    foreach ($launcher in $expected.Keys) {
        $actual = @($report.launcherMappings | Where-Object { $_.launcher -eq $launcher } |
            Sort-Object ammo | ForEach-Object { "$($_.ammo):$($_.projectile)" }) -join ','
        Assert-True ($actual -ceq $expected[$launcher]) (
            "Launcher $launcher map changed to $actual.")
    }
}

Test-Case 'special-cadence-and-conservation-contracts' {
    $f = $report.facts
    Assert-True ($f.vortexHeldProjectile -eq 615 -and
        $f.vortexFireIntervalUpdates -eq 5 -and
        $f.vortexBonusRocketEveryEvents -eq 7 -and
        [Math]::Abs($f.vortexIntrinsicNoConsumeProbability - (2.0 / 3.0)) -lt 0.000001) (
        'Vortex held-projectile contract changed.')
    Assert-True ($f.celebrationHeldProjectile -eq 714 -and
        $f.celebrationFireIntervalUpdates -eq 8 -and
        $f.celebrationSequenceLengthEvents -eq 7 -and
        $f.celebrationProjectilesPerFullSequence -eq 10 -and
        $f.celebrationIntrinsicNoConsumeProbability -eq 0.5) (
        'Celebration sequence contract changed.')
}

Test-Case 'xenopopper-is-delayed-nondamaging-four-plus-one-release' {
    $f = $report.facts
    Assert-True ($f.xenopopperGuaranteedBubbles -eq 4 -and
        $f.xenopopperFifthBubbleChance -eq 0.25 -and
        $f.xenopopperLifetimeProjectileUpdates -eq 60 -and
        $f.xenopopperProjectileExtraUpdates -eq 1 -and
        $f.xenopopperBubbleDealsDamage -eq $false) 'Xenopopper release contract changed.'
}

Test-Case 'electrosphere-is-area-placement-not-a-straight-dps-projectile' {
    $f = $report.facts
    Assert-True ($f.electrosphereTravelProjectile -eq 442 -and
        $f.electrosphereTravelDealsDirectDamage -eq $false -and
        $f.electrosphereAreaProjectile -eq 443 -and
        $f.electrosphereAreaSpawnedLifetimeUpdates -eq 300 -and
        $f.electrosphereAreaNpcOwnerImmunityUpdates -eq 8 -and
        $f.electrosphereAreaMinimumIntegerSize -eq 62 -and
        $f.electrosphereAreaMaximumIntegerSize -eq 80) (
        'Electrosphere placement/area contract changed.')
}

Test-Case 'world-mutating-ammo-is-explicit' {
    $mutating = @($report.rocketAmmo | Where-Object {
        $_.nativeWorldEffect -match 'destroy|add-|remove-'
    } | Sort-Object id | ForEach-Object { $_.id })
    Assert-True (($mutating -join ',') -ceq '772,774,4446,4447,4448,4449,4458,4459') (
        "World-mutating ammo set changed: $($mutating -join ',').")
}

Test-Case 'snowman-mini-nuke-two-asymmetry-is-not-normalized-away' {
    Assert-True ($report.facts.snowmanMiniNukeTwoPrepareHitboxRegression -ceq
        'projectile 809 is omitted; 808 is compared twice') (
        'The exact projectile-809 native asymmetry was lost.')
}

Test-Case 'missing-binary-fails-closed' {
    $threw = $false
    try { $null = & $auditScript -GameExe (Join-Path $PSScriptRoot 'definitely-missing-Terraria.exe') }
    catch { $threw = $_.Exception.Message.Contains('Missing Terraria executable') }
    Assert-True $threw 'Missing binary did not fail closed.'
}

Test-Case 'wrong-binary-hash-fails-closed' {
    $threw = $false
    try { $null = & $auditScript -GameExe $auditScript }
    catch { $threw = $_.Exception.Message.Contains('Unsupported Terraria.exe SHA-256') }
    Assert-True $threw 'Wrong binary hash did not fail closed.'
}

Write-Output "Common special-ranged native audit: $passed passed, $failed failed. No game launch, input injection, ammo consumption, or save mutation occurred."
if ($failed -gt 0) { exit 1 }
exit 0

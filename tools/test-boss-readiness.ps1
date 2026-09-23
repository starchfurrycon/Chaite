# Synthetic, in-memory regression tests. No files, builds, games or GUI operations.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
. (Join-Path $PSScriptRoot 'evaluate-boss-readiness.ps1') -FunctionsOnly
$script:checks = 0
function Assert-ReadyTest([bool]$Condition, [string]$Message) {
    $script:checks++
    if (-not $Condition) { throw "Readiness regression failed: $Message" }
}
function Assert-ReadyThrows([scriptblock]$Action, [string]$Message) {
    $threw = $false
    try { & $Action | Out-Null } catch { $threw = $true }
    Assert-ReadyTest $threw $Message
}
function New-SyntheticBossRecord([int]$Seed, [string]$Boss = 'eye', [string]$Status = 'win', [string]$Difficulty = 'classic') {
    $mode = @('classic', 'expert', 'master').IndexOf($Difficulty)
    $deaths = if ($Status -eq 'loss') { 1 } else { 0 }
    $exitCode = if ($Status -eq 'win') { 0 } else { 20 }
    $summonType = switch -CaseSensitive ($Boss) {
        'eye' { 43 }; 'king-slime' { 560 }; 'queen-slime' { 4988 }; 'destroyer' { 556 }
        'twins' { 544 }; 'prime' { 557 }; 'deerclops' { 5120 }; 'queen-bee' { 1133 }
        default { 0 }
    }
    return [pscustomobject]@{
        Id = "synthetic-$Boss-$Difficulty-$Seed"; Scenario = $Boss; Difficulty = $Difficulty; Seed = $Seed; TakeoverTick = 120
        Classification = $Status; ValidBattle = $true; BattleStarted = $true; Deaths = $deaths
        HostExitCode = $exitCode; ChildExitCode = $exitCode; DesktopSafe = $true
        InputPluginSha256 = 'A' * 64; InputCoreSha256 = 'B' * 64; ProbeSourceSha256 = 'C' * 64; ProbePatcherSourceSha256 = 'D' * 64
        Result = [pscustomobject]@{
            schema = 'chaite-boss-result/v1'; scenario = $Boss; difficulty = $Difficulty; variant = 'standard'; seed = $Seed
            status = $Status; win = $Status -eq 'win'; validBattle = $true; deaths = $deaths; processExitCode = $exitCode
            evidenceKind = 'isolated-native-encounter'; readinessEligible = $true
            directSpawn = $false; directSpawnTick = -1; directSpawnAttempted = $false; directSpawnCompleted = $false
            phaseStageAttempted = $false; phaseStaged = $false; phaseVerifiedAtTakeover = $false
            phaseStage = $null; takeoverNativeSnapshot = $null; encounterFixtureReady = $true; summonConsumed = $true
            variantEvidence = [pscustomobject]@{
                schema = 'chaite-boss-variant-evidence/v1'; scenario = $Boss
                expectedVariant = 'standard'; observedVariant = 'standard'; reportedVariant = 'standard'
                capturedAtActivation = $true; captureTick = 120; requestedTakeoverTick = 120
                nativeFlagsVerified = $true; variantMatchesScenario = $true; mechanicalTrioExpected = $false
                mechanicalTrioObserved = $false; allExpectedBossesSeen = $true
                native = [pscustomobject]@{
                    gameMode = $mode; difficulty = $mode + 1; dayTime = $false; hardMode = $Boss -notin @('eye', 'king-slime')
                    forTheWorthy = $false; zenithWorld = $false; drunkWorld = $false; notTheBeesWorld = $false
                    remixWorld = $false; celebrationWorld = $false; constantWorld = $false; noTrapsWorld = $false; skyblockWorld = $false
                }
            }
            allExpectedBossesSeen = $true; unexpectedBossTypes = @(); nativeFrames = 1000; nativeDifficultyVerified = $true
            nativeDifficulty = [pscustomobject]@{ gameMode = $mode; worldFileGameMode = $mode; difficulty = $mode + 1; worldFileSeed = $Seed; forTheWorthy = $false; zenithWorld = $false; drunkWorld = $false; notTheBeesWorld = $false; remixWorld = $false; celebrationWorld = $false; constantWorld = $false; noTrapsWorld = $false; skyblockWorld = $false }
            firstObservedBosses = @([pscustomobject]@{ npcDifficulty = $mode + 1; lifeMax = 1000 })
            battleRandom = [pscustomobject]@{ installedAfterSetup = $true; actualAndNativeNamedColdStateVerified = $true; actualStreamConsumedForFingerprint = $false; seed = $Seed; unpausedUpdateSeedInitial = $Seed; unpausedUpdateSeedAdvances = 1000; referenceChecks = 2000; independentTwinFingerprint = @(1,2,3,4,5,6,7,8) }
            equipment = [pscustomobject]@{ life = 400; mana = 200; armorAndAccessories = @(1,2,3); weaponType = 98; summonType = $summonType; summonCount = 1; ammoType = 97; ammoCount = 9999; healingType = 188; healingCount = 20; grappleType = 84; consumablesReplenished = $false }
            arena = [pscustomobject]@{ kind = 'synthetic-test-only'; groundTop = 500; platformRows = @(); nativeSceneMetricRefreshes = 1000 }
            limits = [pscustomobject]@{ ticks = 24000; wallSeconds = 90 }; scope = 'synthetic'; randomScope = 'synthetic independent test data, no engine execution'
        }
    }
}
function New-SyntheticTarget($Record, [int[]]$Seeds = (1..40), [string]$Id = '') {
    $identity = Get-BossEvidenceIdentity $Record
    if ([string]::IsNullOrWhiteSpace($Id)) { $Id = $identity.boss + '-' + $identity.difficulty }
    return [pscustomobject]@{ id = $Id; boss = $identity.boss; difficulty = $identity.difficulty; variant = $identity.variant; build = $identity.build; profileSha256 = $identity.profileSha256; plannedSeeds = $Seeds }
}
function New-SyntheticTargets([object[]]$Cells) {
    return [pscustomobject]@{ schema = 'chaite-boss-readiness-targets/v1'; completeTargetList = $true; coverageDescription = 'Explicit synthetic required cells only'; samplingPlan = [pscustomobject]@{ id = 'synthetic-preregistered'; description = 'Independent synthetic seeds, plan fixed before observation'; independentSeedsDeclared = $true; fixedBeforeEvaluation = $true }; targets = $Cells }
}
function New-SyntheticV2Targets([object[]]$Cells) {
    return [pscustomobject]@{ schema = 'chaite-boss-readiness-targets/v2'; completeTargetList = $true; coverageDescription = 'Explicit synthetic two-tier required cells only'; samplingPlan = [pscustomobject]@{ id = 'synthetic-v2-preregistered'; description = 'Independent synthetic seeds, plan fixed before observation'; independentSeedsDeclared = $true; fixedBeforeEvaluation = $true }; targets = $Cells }
}
function Set-SyntheticGoal($Target, [string]$Goal) {
    $Target | Add-Member -NotePropertyName goal -NotePropertyValue $Goal
    return $Target
}
function New-SyntheticOutcomeSet([string]$Boss, [int]$Wins,
    [int]$Samples = 40) {
    return @(1..$Samples | ForEach-Object {
        New-SyntheticBossRecord $_ $Boss $(if ($_ -le $Wins) {
            'win'
        } else { 'loss' })
    })
}
function New-SyntheticSummary([object[]]$Records, [bool]$Invalidated = $false) {
    return [pscustomobject]@{ Source = 'synthetic:memory'; Invalidated = $Invalidated; Document = [pscustomobject]@{ Schema = 'chaite-boss-batch-summary/v1'; Attempted = $Records.Count; Cases = $Records } }
}
function Copy-ReadyJsonObject($Value) {
    return (ConvertTo-Json -InputObject $Value -Depth 50 | ConvertFrom-Json)
}
function New-CatalogBoundSyntheticProfiles($CoverageCatalog) {
    $profiles = [Collections.Generic.List[object]]::new()
    foreach ($cell in @($CoverageCatalog.cells)) {
        $profiles.Add([pscustomobject]@{
            boss = [string]$cell.boss
            difficulty = [string]$cell.difficulty
            variant = [string]$cell.variant
            build = [pscustomobject]@{
                pluginSha256 = 'A' * 64; coreSha256 = 'B' * 64
                probeSha256 = 'C' * 64; probePatcherSha256 = 'D' * 64
            }
            profileSha256 = 'E' * 64
            profile = [pscustomobject]@{ synthetic = $true }
        })
    }
    return [pscustomobject]@{
        schema = 'chaite-boss-readiness-profiles/v1'
        ready = $false
        note = 'Synthetic catalog-bound profile discovery.'
        profiles = @($profiles.ToArray())
    }
}
function New-CatalogBoundSyntheticPlan($CoverageCatalog) {
    return New-BrCatalogBoundTargetDocument -CoverageCatalog $CoverageCatalog `
        -ProfileDocument (New-CatalogBoundSyntheticProfiles $CoverageCatalog) `
        -PlannedSeeds (1..40) -SamplingPlanId 'synthetic-v3-preregistered' `
        -SamplingPlanDescription 'Independent synthetic seeds, plan fixed before observation'
}

Assert-ReadyTest ((Get-Wilson95LowerBound 0 0) -eq 0) 'empty Wilson bound'
Assert-ReadyTest ([Math]::Abs((Get-Wilson95LowerBound 1 1) - 0.2065493144) -lt 0.000000001) '1/1 Wilson reference'
Assert-ReadyTest ((Get-Wilson95LowerBound 40 40) -gt 0.90) '40/40 Wilson passes'
Assert-ReadyTest ((Get-Wilson95LowerBound 39 40) -lt 0.90) '39/40 Wilson fails despite 97.5 percent point estimate'
Assert-ReadyTest ((Get-Wilson95LowerBound 95 100) -lt 0.90) '95/100 lower bound fails'
Assert-ReadyTest ((Get-Wilson95LowerBound 96 100) -ge 0.90) '96/100 lower bound passes'
Assert-ReadyThrows { Get-Wilson95LowerBound 2 1 } 'invalid Wilson counts rejected'

$project = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$coverageCatalog = (Get-Content -LiteralPath (Join-Path $project 'docs\boss-readiness-coverage-v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json)
Assert-BrCoverageCatalog -CoverageCatalog $coverageCatalog | Out-Null
Assert-ReadyTest ($coverageCatalog.cells.Count -eq 57) 'fixed catalog declares every 57 required non-event core-Boss cells'
Assert-ReadyTest (@($coverageCatalog.cells | Where-Object goal -ceq 'priority-near-certain').Count -eq 18) 'six priority encounter variants are present at all three difficulties'
Assert-ReadyTest (@($coverageCatalog.cells | Where-Object goal -ceq 'secondary-majority').Count -eq 39) 'every remaining declared Boss/variant/difficulty cell has its independent majority gate'
Assert-ReadyTest (@($coverageCatalog.cells | Where-Object {
    $_.boss -ceq 'eater-of-worlds'
}).Count -eq 3 -and @($coverageCatalog.cells | Where-Object {
    $_.boss -ceq 'brain-of-cthulhu'
}).Count -eq 3) 'both world-evil Boss alternatives remain independent'
$missingCatalogCell = Copy-ReadyJsonObject $coverageCatalog
$missingCatalogCell.cells = @($missingCatalogCell.cells | Select-Object -Skip 1)
Assert-ReadyThrows { Assert-BrCoverageCatalog -CoverageCatalog $missingCatalogCell } 'catalog cannot hide a required Boss stratum by shortening the declared list'
$wrongCatalogGoal = Copy-ReadyJsonObject $coverageCatalog
$wrongCatalogGoal.cells[0].goal = 'priority-near-certain'
Assert-ReadyThrows { Assert-BrCoverageCatalog -CoverageCatalog $wrongCatalogGoal } 'catalog cannot upgrade or downgrade a fixed product goal'

$catalogProfiles = New-CatalogBoundSyntheticProfiles $coverageCatalog
$catalogPlan = New-BrCatalogBoundTargetDocument -CoverageCatalog $coverageCatalog `
    -ProfileDocument $catalogProfiles -PlannedSeeds (1..40) `
    -SamplingPlanId 'synthetic-v3-preregistered' `
    -SamplingPlanDescription 'Independent synthetic seeds, plan fixed before observation'
Assert-ReadyTest ($catalogPlan.targets.Count -eq 57 -and
    $catalogPlan.coverageCatalogSha256 -ceq (Get-BrHash $coverageCatalog)) `
    'shared catalog target factory generates exactly one preregistered target per required cell'
$missingProfile = Copy-ReadyJsonObject $catalogProfiles
$missingProfile.profiles = @($missingProfile.profiles | Select-Object -Skip 1)
Assert-ReadyThrows {
    New-BrCatalogBoundTargetDocument -CoverageCatalog $coverageCatalog `
        -ProfileDocument $missingProfile -PlannedSeeds (1..40) `
        -SamplingPlanId 'synthetic' -SamplingPlanDescription 'synthetic'
} 'target factory refuses profile discovery with a missing catalog cell'
$duplicateProfile = Copy-ReadyJsonObject $catalogProfiles
$duplicateProfile.profiles += $duplicateProfile.profiles[0]
Assert-ReadyThrows {
    New-BrCatalogBoundTargetDocument -CoverageCatalog $coverageCatalog `
        -ProfileDocument $duplicateProfile -PlannedSeeds (1..40) `
        -SamplingPlanId 'synthetic' -SamplingPlanDescription 'synthetic'
} 'target factory refuses ambiguous duplicate profile choices'
$multiBuildProfiles = Copy-ReadyJsonObject $catalogProfiles
$multiBuildProfiles.profiles[1].build.pluginSha256 = 'F' * 64
Assert-ReadyThrows {
    New-BrCatalogBoundTargetDocument -CoverageCatalog $coverageCatalog `
        -ProfileDocument $multiBuildProfiles -PlannedSeeds (1..40) `
        -SamplingPlanId 'synthetic' -SamplingPlanDescription 'synthetic'
} 'target factory refuses profiles from multiple builds'
Assert-ReadyThrows {
    New-BrCatalogBoundTargetDocument -CoverageCatalog $coverageCatalog `
        -ProfileDocument $catalogProfiles -PlannedSeeds (1..39) `
        -SamplingPlanId 'synthetic' -SamplingPlanDescription 'synthetic'
} 'target factory refuses fewer than forty preregistered seeds'
$catalogSmoke = Measure-BossReadiness -TargetDocument $catalogPlan -Summaries @() -CoverageCatalog $coverageCatalog
Assert-ReadyTest (-not $catalogSmoke.ready -and $catalogSmoke.schema -ceq 'chaite-boss-readiness/v3' -and $catalogSmoke.cells.Count -eq 57) 'catalog-bound V3 reports every unverified Boss stratum rather than a partial aggregate'
Assert-ReadyTest ($catalogSmoke.coverageCatalog.id -ceq 'terraria-1.4.5.8-core-bosses-v1' -and $catalogSmoke.coverageCatalog.cells -eq 57) 'V3 report carries the exact validated coverage catalog identity'
Assert-ReadyTest (@($catalogSmoke.cells | Where-Object {
    $_.goal -ceq 'priority-near-certain' -and $_.requiredObservedSuccessFraction -eq .95 -and
    $_.requiredWilson95TwoSidedLowerBound -eq .90
}).Count -eq 18) 'every catalog priority cell retains its near-certain statistical gate in V3'
Assert-ReadyTest (@($catalogSmoke.cells | Where-Object {
    $_.goal -ceq 'secondary-majority' -and $_.observedThresholdIsStrict -and
    $_.wilsonThresholdIsStrict
}).Count -eq 39) 'every catalog secondary cell retains its strict independent-majority gate in V3'
$missingTarget = Copy-ReadyJsonObject $catalogPlan
$missingTarget.targets = @($missingTarget.targets | Select-Object -Skip 1)
Assert-ReadyThrows {
    Measure-BossReadiness -TargetDocument $missingTarget -Summaries @() -CoverageCatalog $coverageCatalog
} 'catalog-bound V3 refuses a target plan with a missing Boss stratum'
$wrongCatalogHash = Copy-ReadyJsonObject $catalogPlan
$wrongCatalogHash.coverageCatalogSha256 = '0' * 64
Assert-ReadyThrows {
    Measure-BossReadiness -TargetDocument $wrongCatalogHash -Summaries @() -CoverageCatalog $coverageCatalog
} 'catalog-bound V3 refuses an unbound or stale catalog hash'
$wrongTargetGoal = Copy-ReadyJsonObject $catalogPlan
$wrongTargetGoal.targets[12].goal = 'secondary-majority'
Assert-ReadyThrows {
    Measure-BossReadiness -TargetDocument $wrongTargetGoal -Summaries @() -CoverageCatalog $coverageCatalog
} 'catalog-bound V3 refuses a target whose goal differs from the fixed catalog'
$mixedBuilds = Copy-ReadyJsonObject $catalogPlan
$mixedBuilds.targets[1].build.pluginSha256 = 'F' * 64
Assert-ReadyThrows {
    Measure-BossReadiness -TargetDocument $mixedBuilds -Summaries @() -CoverageCatalog $coverageCatalog
} 'catalog-bound V3 cannot combine Boss evidence from different builds'

$eye = New-SyntheticBossRecord 1
$implicitVariant = New-SyntheticBossRecord 1
$implicitVariant.Result.PSObject.Properties.Remove('variant')
Assert-ReadyThrows { Get-BossEvidenceIdentity $implicitVariant $true } 'catalog-bound V3 evidence cannot silently treat an unreported variant as standard'
$implicitVariant | Add-Member -NotePropertyName Variant -NotePropertyValue 'standard'
Assert-ReadyThrows { Get-BossEvidenceIdentity $implicitVariant $true } 'catalog-bound V3 requires the variant inside native result evidence, not only a summary label'
$target = New-SyntheticTarget $eye
$plan = New-SyntheticTargets @($target)
$forty = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
$pass = Measure-BossReadiness $plan @(New-SyntheticSummary $forty)
Assert-ReadyTest $pass.ready '40 independent verified wins in the complete single-cell plan pass'
Assert-ReadyTest ($pass.cells[0].independentSeedCount -eq 40) '40 independent seeds counted'
Assert-ReadyTest ($pass.cells[0].wilson95TwoSidedLowerBound -gt 0.90) 'report exposes confidence bound'

$priorityTarget = Set-SyntheticGoal (New-SyntheticTarget $eye) `
    'priority-near-certain'
$priorityPlan = New-SyntheticV2Targets @($priorityTarget)
$priorityPass = Measure-BossReadiness $priorityPlan `
    @(New-SyntheticSummary $forty)
Assert-ReadyTest ($priorityPass.ready -and
    $priorityPass.schema -ceq 'chaite-boss-readiness/v2') `
    'V2 priority 40/40 passes the near-certain gate'
Assert-ReadyTest ($priorityPass.cells[0].goal -ceq
    'priority-near-certain' -and
    $priorityPass.cells[0].requiredObservedSuccessFraction -eq .95 -and
    $priorityPass.cells[0].requiredWilson95TwoSidedLowerBound -eq .90) `
    'V2 priority report exposes both fixed thresholds'
$priority39 = Measure-BossReadiness $priorityPlan `
    @(New-SyntheticSummary (New-SyntheticOutcomeSet 'eye' 39))
Assert-ReadyTest (-not $priority39.ready -and
    $priority39.cells[0].observedSuccessFraction -eq .975 -and
    $priority39.cells[0].reasons -contains
        'confidence-lower-bound-below-target') `
    '39/40 priority samples cannot claim near certainty from point estimate'
$priority100Target = Set-SyntheticGoal `
    (New-SyntheticTarget $eye (1..100)) 'priority-near-certain'
$priority100Plan = New-SyntheticV2Targets @($priority100Target)
$priority95 = Measure-BossReadiness $priority100Plan `
    @(New-SyntheticSummary (New-SyntheticOutcomeSet 'eye' 95 100))
Assert-ReadyTest (-not $priority95.ready) `
    '95/100 priority samples still fail their confidence lower bound'
$priority96 = Measure-BossReadiness $priority100Plan `
    @(New-SyntheticSummary (New-SyntheticOutcomeSet 'eye' 96 100))
Assert-ReadyTest $priority96.ready `
    '96/100 priority samples pass both predeclared thresholds'

$secondaryRecord = New-SyntheticBossRecord 1 'prime'
$secondaryTarget = Set-SyntheticGoal `
    (New-SyntheticTarget $secondaryRecord) 'secondary-majority'
$secondaryPlan = New-SyntheticV2Targets @($secondaryTarget)
$secondary26 = Measure-BossReadiness $secondaryPlan `
    @(New-SyntheticSummary (New-SyntheticOutcomeSet 'prime' 26))
Assert-ReadyTest (-not $secondary26.ready -and
    $secondary26.cells[0].observedSuccessFraction -gt .50 -and
    $secondary26.cells[0].reasons -contains
        'confidence-lower-bound-below-target') `
    'secondary point estimate above half is insufficient without confidence'
$secondary27 = Measure-BossReadiness $secondaryPlan `
    @(New-SyntheticSummary (New-SyntheticOutcomeSet 'prime' 27))
Assert-ReadyTest ($secondary27.ready -and
    $secondary27.cells[0].wilsonThresholdIsStrict) `
    '27/40 secondary samples pass the strict majority confidence gate'
$secondary20 = Measure-BossReadiness $secondaryPlan `
    @(New-SyntheticSummary (New-SyntheticOutcomeSet 'prime' 20))
Assert-ReadyTest (-not $secondary20.ready -and
    $secondary20.cells[0].reasons -contains
        'observed-success-fraction-below-target') `
    'exactly half is not greater than fifty percent'
$missingGoalPlan = New-SyntheticV2Targets @((New-SyntheticTarget $eye))
Assert-ReadyThrows {
    Measure-BossReadiness $missingGoalPlan @(New-SyntheticSummary $forty)
} 'V2 target without an explicit goal is rejected'

$oneLoss = @(1..40 | ForEach-Object { if ($_ -eq 40) { New-SyntheticBossRecord $_ 'eye' 'loss' } else { New-SyntheticBossRecord $_ } })
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $oneLoss)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].independentWins -eq 39) 'death is not a win and confidence fails'
Assert-ReadyTest ($fail.cells[0].deathRecords -eq 1) 'death retained'

$timeout = @(1..40 | ForEach-Object { if ($_ -eq 40) { New-SyntheticBossRecord $_ 'eye' 'timeout' } else { New-SyntheticBossRecord $_ } })
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $timeout)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].timeoutRecords -eq 1 -and $fail.cells[0].nonWins -eq 1) 'combat timeout remains a non-win in denominator'

$afterDeath = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
$afterDeath[39].Deaths = 1; $afterDeath[39].Result.deaths = 1
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $afterDeath)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].independentWins -eq 39) 'reported post-death victory does not qualify as death-free success'

$dupes = @(1..40 | ForEach-Object { New-SyntheticBossRecord 1 })
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $dupes)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].independentSeedCount -eq 1) 'same seed 40 times is one non-independent observation'
Assert-ReadyTest ($fail.cells[0].duplicateSeeds.Count -eq 1 -and $fail.cells[0].independentWins -eq 0) 'duplicates neither inflate wins nor select best attempt'
$fail = Measure-BossReadiness $plan @((New-SyntheticSummary $forty), (New-SyntheticSummary @((New-SyntheticBossRecord 1 'eye' 'loss'))))
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].independentSeedCount -eq 40 -and $fail.cells[0].deathRecords -eq 1) 'losing retry across summaries cannot be hidden by first winning attempt'

$other = New-SyntheticBossRecord 1 'prime'
$twoPlan = New-SyntheticTargets @($target, (New-SyntheticTarget $other))
$fail = Measure-BossReadiness $twoPlan @(New-SyntheticSummary $forty)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[1].status -eq 'unverified') 'entire missing Boss blocks readiness'
$primeLosses = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ 'prime' 'loss' })
$fail = Measure-BossReadiness $twoPlan @(New-SyntheticSummary @($forty + $primeLosses))
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].ready -and -not $fail.cells[1].ready) 'good Boss cannot compensate failed Boss'
$manyEye = @(1..400 | ForEach-Object { New-SyntheticBossRecord $_ })
$largePlan = New-SyntheticTargets @((New-SyntheticTarget $eye (1..400)), (New-SyntheticTarget $other))
$fail = Measure-BossReadiness $largePlan @(New-SyntheticSummary @($manyEye + $primeLosses))
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].ready -and $fail.cells[1].independentWins -eq 0) '400 good Boss wins cannot mask another Boss 0/40'

$changed = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
foreach ($record in $changed) { $record.InputPluginSha256 = 'E' * 64 }
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $changed)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].independentSeedCount -eq 0 -and $fail.unmatchedRecords -eq 40) 'different build never merged'
$changed = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
foreach ($record in $changed) { $record.Result.equipment.ammoType = 515 }
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $changed)
Assert-ReadyTest (-not $fail.ready -and $fail.unmatchedRecords -eq 40) 'different ammo profile never merged'
$changed = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
foreach ($record in $changed) { $record.Result.arena.groundTop = 450 }
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $changed)
Assert-ReadyTest (-not $fail.ready -and $fail.unmatchedRecords -eq 40) 'different arena never merged'
$changed = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
foreach ($record in $changed) { $record.Result.variant = 'for-the-worthy' }
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $changed)
Assert-ReadyTest (-not $fail.ready -and $fail.unmatchedRecords -eq 40) 'Boss variant never merged'
$changed = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ 'eye' 'win' 'expert' })
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $changed)
Assert-ReadyTest (-not $fail.ready -and $fail.unmatchedRecords -eq 40) 'difficulty never merged'

$identityA = Get-BossEvidenceIdentity $eye
$counterChange = New-SyntheticBossRecord 1
$counterChange.Result.arena.nativeSceneMetricRefreshes = 9999
$identityB = Get-BossEvidenceIdentity $counterChange
Assert-ReadyTest ($identityA.profileSha256 -ceq $identityB.profileSha256) 'measured frame counter is not an arena configuration'
$counterChange.Result.equipment.healingCount = 999
Assert-ReadyTest ($identityA.profileSha256 -cne (Get-BossEvidenceIdentity $counterChange).profileSha256) 'initial consumable resources are configuration'
Assert-ReadyTest ((Get-BrHash ([ordered]@{ b = 2; a = 1 })) -ceq (Get-BrHash ([ordered]@{ a = 1; b = 2 }))) 'JSON property ordering is immaterial'
Assert-ReadyTest ((Get-BrHash ([ordered]@{ a = @(1) })) -cne (Get-BrHash ([ordered]@{ a = 1 }))) 'single-element array must not collapse into scalar'
Assert-ReadyTest ((Get-BrHash ([ordered]@{ a = @() })) -cne (Get-BrHash ([ordered]@{ a = $null }))) 'empty array must not collapse into null'

$smoke = Measure-BossReadiness $plan @(New-SyntheticSummary @($eye))
Assert-ReadyTest (-not $smoke.ready -and $smoke.cells[0].status -eq 'smoke-only') 'single historical smoke victory is not ready'
$thirtyNine = Measure-BossReadiness $plan @(New-SyntheticSummary @($forty | Select-Object -First 39))
Assert-ReadyTest (-not $thirtyNine.ready -and $thirtyNine.cells[0].missingSeeds.Count -eq 1) 'unfinished declared seed plan cannot pass at a convenient stopping point'
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary @($forty + (New-SyntheticBossRecord 41)))
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].unplannedRecords -eq 1) 'unplanned same-stratum samples require review'
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $forty $true)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].invalidEvidenceRecords -eq 40) 'invalidated historical batch never qualifies'

# The producer labels below deliberately claim eligibility, matching the
# historical evaluator's trust boundary.  Their independently reported origin
# still says direct-spawn + phase staging, so they must remain 40 non-wins and
# cannot be upgraded into a readiness result by relabeling a batch summary.
$relabeledStaged = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
foreach ($record in $relabeledStaged) {
    $record.Result.directSpawn = $true
    $record.Result.directSpawnTick = 100
    $record.Result.directSpawnAttempted = $true
    $record.Result.directSpawnCompleted = $true
    $record.Result.phaseStageAttempted = $true
    $record.Result.phaseStaged = $true
    $record.Result.phaseVerifiedAtTakeover = $true
    $record.Result.phaseStage = [pscustomobject]@{ schema = 'chaite-priority-phase-stage/v1' }
    $record.Result.takeoverNativeSnapshot = [pscustomobject]@{ schema = 'chaite-takeover-native-snapshot/v1' }
    $record.Result.summonConsumed = $false
}
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $relabeledStaged)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].independentWins -eq 0 -and
    $fail.cells[0].invalidEvidenceRecords -eq 40 -and
    $fail.cells[0].reasons -contains 'invalid-evidence') `
    'a staged direct-spawn result cannot enter the campaign merely by claiming isolated readiness eligibility'

# Exercise each raw-origin gate separately so no other staged field can mask a
# regression. Producer labels intentionally retain their valid-looking values.
foreach ($originCase in @(
    @{ Name = 'directSpawn=true'; Field = 'directSpawn'; Value = $true },
    @{ Name = 'directSpawnAttempted=true'; Field = 'directSpawnAttempted'; Value = $true },
    @{ Name = 'directSpawnCompleted=true'; Field = 'directSpawnCompleted'; Value = $true },
    @{ Name = 'phaseStageAttempted=true'; Field = 'phaseStageAttempted'; Value = $true },
    @{ Name = 'phaseStaged=true'; Field = 'phaseStaged'; Value = $true },
    @{ Name = 'phaseVerifiedAtTakeover=true'; Field = 'phaseVerifiedAtTakeover'; Value = $true },
    @{ Name = 'non-null phaseStage'; Field = 'phaseStage'; Value = [pscustomobject]@{ schema = 'forged' } },
    @{ Name = 'non-null takeoverNativeSnapshot'; Field = 'takeoverNativeSnapshot'; Value = [pscustomobject]@{ schema = 'forged' } },
    @{ Name = 'summonConsumed=false'; Field = 'summonConsumed'; Value = $false }
)) {
    $forged = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
    foreach ($record in $forged) {
        $record.Result.PSObject.Properties[$originCase.Field].Value = $originCase.Value
    }
    $originFailure = Measure-BossReadiness $plan @(New-SyntheticSummary $forged)
    Assert-ReadyTest (-not $originFailure.ready -and
        $originFailure.cells[0].independentWins -eq 0 -and
        $originFailure.cells[0].invalidEvidenceRecords -eq 40) `
        "raw origin gate rejects $($originCase.Name) despite forged eligibility labels"
}
$eligibleProfiles = @(Get-BrReadinessProfiles @(New-SyntheticSummary @($eye)))
Assert-ReadyTest ($eligibleProfiles.Count -eq 1) `
    'profile discovery retains a valid non-staged campaign profile'
$stagedProfiles = @(Get-BrReadinessProfiles @(New-SyntheticSummary @($relabeledStaged[0])) 3>$null)
Assert-ReadyTest ($stagedProfiles.Count -eq 0) `
    'profile discovery cannot seed a target plan from staged direct-spawn evidence'
$missingOrigin = New-SyntheticBossRecord 1
$missingOrigin.Result.PSObject.Properties.Remove('directSpawn')
$missingOriginQuality = Test-BrRecordEvidence $missingOrigin $false
Assert-ReadyTest (-not $missingOriginQuality.ValidEvidence -and
    $missingOriginQuality.Errors -contains 'encounter-origin-direct-spawn-or-missing') `
    'missing encounter-origin fields fail closed rather than being treated as a clean organic path'
$missingStageReport = New-SyntheticBossRecord 1
$missingStageReport.Result.PSObject.Properties.Remove('phaseStage')
$missingStageReportQuality = Test-BrRecordEvidence $missingStageReport $false
Assert-ReadyTest (-not $missingStageReportQuality.ValidEvidence -and
    $missingStageReportQuality.Errors -contains 'encounter-origin-staging-report-present') `
    'missing staging-report declarations fail closed rather than being treated as explicit null'
$wrongSummonSource = New-SyntheticBossRecord 1
$wrongSummonSource.Result.equipment.summonType = 5120
$wrongSummonSourceQuality = Test-BrRecordEvidence $wrongSummonSource $false
Assert-ReadyTest (-not $wrongSummonSourceQuality.ValidEvidence -and
    $wrongSummonSourceQuality.Errors -contains 'encounter-origin-hotbar-summon-unverified') `
    'readiness origin gate rejects a mismatched hotbar summon item despite valid-looking eligibility labels'
$stringSummonSource = New-SyntheticBossRecord 1
$stringSummonSource.Result.equipment.summonType = '43'
$stringSummonSourceQuality = Test-BrRecordEvidence $stringSummonSource $false
Assert-ReadyTest (-not $stringSummonSourceQuality.ValidEvidence -and
    $stringSummonSourceQuality.Errors -contains 'encounter-origin-hotbar-summon-unverified') `
    'readiness origin gate rejects a string masquerading as a numeric hotbar summon item'

$broken = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
$broken[39].Result.battleRandom.actualAndNativeNamedColdStateVerified = $false
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $broken)
Assert-ReadyTest (-not $fail.ready -and $fail.cells[0].invalidEvidenceRecords -eq 1) 'missing native RNG validation blocks readiness'
$broken[39].Result.equipment = $null
$fail = Measure-BossReadiness $plan @(New-SyntheticSummary $broken)
Assert-ReadyTest (-not $fail.ready -and $fail.unassignableEvidence.Count -eq 1) 'missing bad profile cannot silently shrink denominator'

$incomplete = New-SyntheticTargets @($target); $incomplete.completeTargetList = $false
Assert-ReadyThrows { Measure-BossReadiness $incomplete @(New-SyntheticSummary $forty) } 'implicit incomplete scope rejected'
$incomplete.completeTargetList = 1
Assert-ReadyThrows { Measure-BossReadiness $incomplete @(New-SyntheticSummary $forty) } 'numeric truthy declaration is not explicit JSON true'
$adaptive = New-SyntheticTargets @($target); $adaptive.samplingPlan.fixedBeforeEvaluation = $false
Assert-ReadyThrows { Measure-BossReadiness $adaptive @(New-SyntheticSummary $forty) } 'adaptive after-the-fact target plan rejected'
Assert-ReadyThrows { Measure-BossReadiness $plan @(New-SyntheticSummary $forty) 1 0.90 } 'cannot lower sample policy to a smoke test'
Assert-ReadyThrows { Measure-BossReadiness $plan @(New-SyntheticSummary $forty) 40 0.50 } 'cannot weaken confidence target'
$duplicateTarget = New-SyntheticTargets @($target, $target)
Assert-ReadyThrows { Measure-BossReadiness $duplicateTarget @(New-SyntheticSummary $forty) } 'duplicate target rejected'

Assert-ReadyThrows { Assert-BrProjectPath 'C:\ExternalReadinessTest\readiness.json' (Join-Path $project 'artifacts') $false } 'output outside project artifacts rejected'
Assert-ReadyThrows { Assert-BrProjectPath (Join-Path $project 'README.md') (Join-Path $project 'artifacts') $false } 'non-artifact and non-JSON output rejected'
Write-Output "Boss-readiness synthetic regressions: $script:checks passed; no files or game processes created."
exit 0

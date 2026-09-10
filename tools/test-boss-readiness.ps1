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
    return [pscustomobject]@{
        Id = "synthetic-$Boss-$Difficulty-$Seed"; Scenario = $Boss; Difficulty = $Difficulty; Seed = $Seed
        Classification = $Status; ValidBattle = $true; BattleStarted = $true; Deaths = $deaths
        HostExitCode = $exitCode; ChildExitCode = $exitCode; DesktopSafe = $true
        InputPluginSha256 = 'A' * 64; InputCoreSha256 = 'B' * 64; ProbeSourceSha256 = 'C' * 64; ProbePatcherSourceSha256 = 'D' * 64
        Result = [pscustomobject]@{
            schema = 'chaite-boss-result/v1'; scenario = $Boss; difficulty = $Difficulty; variant = 'standard'; seed = $Seed
            status = $Status; win = $Status -eq 'win'; validBattle = $true; deaths = $deaths; processExitCode = $exitCode
            allExpectedBossesSeen = $true; unexpectedBossTypes = @(); nativeFrames = 1000; nativeDifficultyVerified = $true
            nativeDifficulty = [pscustomobject]@{ gameMode = $mode; worldFileGameMode = $mode; difficulty = $mode + 1; worldFileSeed = $Seed; forTheWorthy = $false }
            firstObservedBosses = @([pscustomobject]@{ npcDifficulty = $mode + 1; lifeMax = 1000 })
            battleRandom = [pscustomobject]@{ installedAfterSetup = $true; actualAndNativeNamedColdStateVerified = $true; actualStreamConsumedForFingerprint = $false; seed = $Seed; unpausedUpdateSeedInitial = $Seed; unpausedUpdateSeedAdvances = 1000; referenceChecks = 2000; independentTwinFingerprint = @(1,2,3,4,5,6,7,8) }
            equipment = [pscustomobject]@{ life = 400; mana = 200; armorAndAccessories = @(1,2,3); weaponType = 98; ammoType = 97; ammoCount = 9999; healingType = 188; healingCount = 20; grappleType = 84; consumablesReplenished = $false }
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
function New-SyntheticSummary([object[]]$Records, [bool]$Invalidated = $false) {
    return [pscustomobject]@{ Source = 'synthetic:memory'; Invalidated = $Invalidated; Document = [pscustomobject]@{ Schema = 'chaite-boss-batch-summary/v1'; Attempted = $Records.Count; Cases = $Records } }
}

Assert-ReadyTest ((Get-Wilson95LowerBound 0 0) -eq 0) 'empty Wilson bound'
Assert-ReadyTest ([Math]::Abs((Get-Wilson95LowerBound 1 1) - 0.2065493144) -lt 0.000000001) '1/1 Wilson reference'
Assert-ReadyTest ((Get-Wilson95LowerBound 40 40) -gt 0.90) '40/40 Wilson passes'
Assert-ReadyTest ((Get-Wilson95LowerBound 39 40) -lt 0.90) '39/40 Wilson fails despite 97.5 percent point estimate'
Assert-ReadyTest ((Get-Wilson95LowerBound 95 100) -lt 0.90) '95/100 lower bound fails'
Assert-ReadyTest ((Get-Wilson95LowerBound 96 100) -ge 0.90) '96/100 lower bound passes'
Assert-ReadyThrows { Get-Wilson95LowerBound 2 1 } 'invalid Wilson counts rejected'

$eye = New-SyntheticBossRecord 1
$target = New-SyntheticTarget $eye
$plan = New-SyntheticTargets @($target)
$forty = @(1..40 | ForEach-Object { New-SyntheticBossRecord $_ })
$pass = Measure-BossReadiness $plan @(New-SyntheticSummary $forty)
Assert-ReadyTest $pass.ready '40 independent verified wins in the complete single-cell plan pass'
Assert-ReadyTest ($pass.cells[0].independentSeedCount -eq 40) '40 independent seeds counted'
Assert-ReadyTest ($pass.cells[0].wilson95TwoSidedLowerBound -gt 0.90) 'report exposes confidence bound'

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

$project = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
Assert-ReadyThrows { Assert-BrProjectPath 'C:\ExternalReadinessTest\readiness.json' (Join-Path $project 'artifacts') $false } 'output outside project artifacts rejected'
Assert-ReadyThrows { Assert-BrProjectPath (Join-Path $project 'README.md') (Join-Path $project 'artifacts') $false } 'non-artifact and non-JSON output rejected'
Write-Output "Boss-readiness synthetic regressions: $script:checks passed; no files or game processes created."
exit 0

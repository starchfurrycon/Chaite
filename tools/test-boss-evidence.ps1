# Offline regression for the ACTUAL runner evidence gate. This file never
# dot-sources the runner or loads game code. It writes only fresh, bounded
# fixtures below project artifacts and removes that exact directory in finally.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# The launch binding must be factored as a production validator so these
# regressions can exercise physical prepared-run fixtures without ever
# reaching DesktopHost or Terraria process creation.
$startPath = Join-Path $PSScriptRoot 'start-isolated-test.ps1'
$startTokens = $null
$startParseErrors = $null
$startAst = [Management.Automation.Language.Parser]::ParseFile($startPath, [ref]$startTokens, [ref]$startParseErrors)
if ($startParseErrors.Count -ne 0) { throw 'Cannot test a launcher containing parse errors.' }
$preparedProbeValidators = @($startAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -ceq 'Read-AndValidatePreparedProbe'
}, $true))
if ($preparedProbeValidators.Count -ne 1) {
    throw 'Expected exactly one production Read-AndValidatePreparedProbe definition.'
}

$runnerPath = Join-Path $PSScriptRoot 'run-boss-validation.ps1'
$tokens = $null
$parseErrors = $null
$runnerAst = [Management.Automation.Language.Parser]::ParseFile($runnerPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'Cannot test a runner containing parse errors.' }
$runnerFunctions = @{}
foreach ($functionName in @('Assert-NoReparse', 'Read-Field', 'Require-Integer', 'Require-Boolean',
    'Require-ObjectFields', 'Require-ExactObjectFields', 'Require-String', 'Require-Sha256', 'Assert-OrdinalString',
    'Require-ResultVariant', 'Get-CaseVariant', 'Test-StandardWorldRules', 'Assert-VariantEvidence',
    'Require-FiniteNumber', 'Read-StrictUtf8File', 'Read-JsonObjectFile', 'Get-PhysicalSha256',
    'Get-HurtEvidenceInput', 'Measure-HurtBufferReplay', 'Read-DesktopExitCode', 'Read-AndValidateLaunchEvidence',
    'Assert-BossLifeEvidence', 'Read-ValidatedResultEnvelope', 'Assert-HurtEvidence', 'Read-AndValidateCaseEvidence')) {
    $definitions = @($runnerAst.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $functionName
    }, $true))
    if ($definitions.Count -ne 1) { throw "Expected exactly one actual $functionName definition." }
    $runnerFunctions[$functionName] = $definitions[0]
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}
# The extracted production envelope validator references this catalog while
# running outside the full runner script scope. Keep the offline contract test
# aligned with the production priority set.
$priorityScenarios = @('deerclops','skeletron','queen-bee','wall-of-flesh',
    'duke-fishron','moon-lord')
$hardModeScenarios = @('queen-slime','destroyer','twins','prime',
    'duke-fishron','moon-lord')
# Read-ValidatedResultEnvelope is intentionally extracted and invoked in an
# isolated test scope. Mirror the production's fixed native type catalog here
# so valid fixtures exercise the same identity check instead of a missing
# variable path.
$expectedBossTypes = @{
    'eye'=@(4); 'king-slime'=@(50); 'queen-slime'=@(657); 'destroyer'=@(134); 'twins'=@(125,126); 'prime'=@(127)
    'deerclops'=@(668); 'skeletron'=@(35); 'queen-bee'=@(222); 'wall-of-flesh'=@(113,114)
    'duke-fishron'=@(370); 'moon-lord'=@(396,397,398)
}
$expectedSummonTypes = @{
    'eye'=43; 'king-slime'=560; 'queen-slime'=4988; 'destroyer'=556; 'twins'=544; 'prime'=557
    'deerclops'=5120; 'queen-bee'=1133
}
$scenarioVariants = @{
    'eye'='standard'; 'king-slime'='standard'; 'queen-slime'='standard'; 'destroyer'='standard'; 'twins'='standard'; 'prime'='standard'
    'deerclops'='standard'; 'skeletron'='standard'; 'queen-bee'='standard'; 'wall-of-flesh'='standard'; 'duke-fishron'='standard'; 'moon-lord'='standard'
    'mechanical-mayhem'='simultaneous-mechanical-trio'; 'mechdusa'='getfixedboi-mechdusa'
}
$knownResultVariants = @('standard','night','day','simultaneous-mechanical-trio','getfixedboi-mechdusa')
$preparePath = Join-Path $PSScriptRoot 'prepare-game-probe.ps1'
$prepareTokens = $null
$prepareParseErrors = $null
$prepareAst = [Management.Automation.Language.Parser]::ParseFile($preparePath, [ref]$prepareTokens, [ref]$prepareParseErrors)
if ($prepareParseErrors.Count -ne 0) { throw 'Cannot test a probe preparer containing parse errors.' }
$prepareFunctions = @{}
foreach ($functionName in @('Get-RequiredFileSha256', 'New-ProbeStaticEvidence')) {
    $definitions = @($prepareAst.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $functionName
    }, $true))
    if ($definitions.Count -ne 1) { throw "Expected exactly one actual prepare $functionName definition." }
    $prepareFunctions[$functionName] = $definitions[0]
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}
$startFunctions = @{}
foreach ($functionName in @('Assert-NoReparse', 'Get-RequiredFileSha256', 'Require-ProbeJsonString',
    'Require-ProbeJsonBoolean', 'Require-ProbeJsonArray', 'Require-ProbeSha256', 'Assert-ProbeStringEquals',
    'Require-ProbeExactFields', 'Read-ProbeStrictUtf8JsonObject', 'Read-AndValidatePreparedProbe')) {
    $definitions = @($startAst.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $functionName
    }, $true))
    if ($definitions.Count -ne 1) { throw "Expected exactly one actual launcher $functionName definition." }
    $startFunctions[$functionName] = $definitions[0]
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}

function Get-CommandCalls($SearchAst, [string]$Name) {
    return @($SearchAst.FindAll({
        param($node)
        $node -is [Management.Automation.Language.CommandAst] -and $node.GetCommandName() -ceq $Name
    }, $true))
}
function Assert-CommandArguments($Call, [string[]]$Expected, [string]$Name) {
    $actual = @($Call.CommandElements | Select-Object -Skip 1 | ForEach-Object { $_.Extent.Text.Trim() })
    if ($actual.Count -ne $Expected.Count) { throw "$Name production argument count changed." }
    for ($index = 0; $index -lt $Expected.Count; $index++) {
        if ($actual[$index] -cne $Expected[$index]) { throw "$Name production argument $index changed." }
    }
}
function Assert-AssignedCall($Call, [string]$ExpectedLeft, [string]$Name) {
    $assignment = $Call.Parent.Parent
    if ($assignment -isnot [Management.Automation.Language.AssignmentStatementAst] -or
        $assignment.Left.Extent.Text.Trim() -cne $ExpectedLeft) { throw "$Name production assignment changed." }
}
function Get-EnclosingIf($Node) {
    $ancestor = $Node.Parent
    while ($null -ne $ancestor -and $ancestor -isnot [Management.Automation.Language.IfStatementAst]) { $ancestor = $ancestor.Parent }
    return $ancestor
}
function Get-EnclosingTry($Node) {
    $ancestor = $Node.Parent
    while ($null -ne $ancestor -and $ancestor -isnot [Management.Automation.Language.TryStatementAst]) { $ancestor = $ancestor.Parent }
    return $ancestor
}

$lockCalls = @(Get-CommandCalls $startAst 'Open-ProbeReadLocks')
if ($lockCalls.Count -ne 1) { throw 'Expected exactly one production Open-ProbeReadLocks call.' }
$lockTry = Get-EnclosingTry $lockCalls[0]
if ($null -eq $lockTry -or $null -eq $lockTry.Finally) { throw 'Prepared launch locks are not protected by an encompassing finally.' }
if (@(Get-CommandCalls $lockTry.Body 'Start-Process').Count -ne 1 -or
    @(Get-CommandCalls $lockTry.Body 'Write-NewProbeUtf8TextFile').Count -ne 1 -or
    @(Get-CommandCalls $lockTry.Body 'Write-NewProbeUtf8JsonFile').Count -lt 2) {
    throw 'The encompassing launch-lock try no longer contains pin-plan, binding/marker, and Host startup operations.'
}
$finallyText = $lockTry.Finally.Extent.Text
if ($finallyText -cnotmatch '\$readLocks' -or $finallyText -cnotmatch '\.Dispose\(\)') {
    throw 'The encompassing launch-lock finally no longer disposes every acquired read lock.'
}

$desktopHostPath = Join-Path $PSScriptRoot 'Chaite.DesktopHost.cs'
$desktopHostSource = [IO.File]::ReadAllText($desktopHostPath, [Text.UTF8Encoding]::new($false, $true))
foreach ($requiredHostFragment in @(
    'if (!string.Equals(executable, expectedTarget, StringComparison.OrdinalIgnoreCase))',
    'The harmless probe child must be this exact DesktopHost executable in its own directory.',
    'QueryInformationJobObject(job, 1, out accounting',
    'if (!WaitForJobEmpty(job, 1000))',
    'StopAndDrainJob(job, process, 124, "timeout")',
    'VerifyPinnedHandles(pins);',
    'WriteHostCompletion(output, pins, exitCode);',
    'FileMode.CreateNew',
    'File.Move(temporaryPath, finalPath)')) {
    if ($desktopHostSource.IndexOf($requiredHostFragment, [StringComparison]::Ordinal) -lt 0) {
        throw "DesktopHost lost required lock-completion structure: $requiredHostFragment"
    }
}
$rootExitIndex = $desktopHostSource.IndexOf('Check(GetExitCodeProcess(process, out exitCode)', [StringComparison]::Ordinal)
$jobEmptyIndex = $desktopHostSource.IndexOf('if (!WaitForJobEmpty(job, 1000))', [StringComparison]::Ordinal)
$finalPinIndex = $desktopHostSource.IndexOf('VerifyPinnedHandles(pins);', $jobEmptyIndex, [StringComparison]::Ordinal)
$completionIndex = $desktopHostSource.IndexOf('WriteHostCompletion(output, pins, exitCode);', [StringComparison]::Ordinal)
if ($rootExitIndex -lt 0 -or $jobEmptyIndex -le $rootExitIndex -or $finalPinIndex -le $jobEmptyIndex -or $completionIndex -le $finalPinIndex) {
    throw 'DesktopHost no longer orders root exit, job-empty proof, handle revalidation, and completion atomically.'
}

$desktopCalls = @(Get-CommandCalls $runnerAst 'Read-DesktopExitCode')
if ($desktopCalls.Count -ne 1) { throw 'Expected exactly one production Read-DesktopExitCode call.' }
Assert-AssignedCall $desktopCalls[0] '$record.HostExitCode' 'Read-DesktopExitCode'
Assert-CommandArguments $desktopCalls[0] @('$exitRecord') 'Read-DesktopExitCode'

$caseReaderCalls = @(Get-CommandCalls $runnerAst 'Read-AndValidateCaseEvidence')
if ($caseReaderCalls.Count -ne 1) { throw 'Expected exactly one production Read-AndValidateCaseEvidence call.' }
Assert-AssignedCall $caseReaderCalls[0] '$validatedCase' 'Read-AndValidateCaseEvidence'
Assert-CommandArguments $caseReaderCalls[0] @('$runDirectory', '$case', '$record.BattleStarted', '$record.HostExitCode',
    '$record.ChildExitCode', '$record.DesktopSafe', '$record') 'Read-AndValidateCaseEvidence'

$launchReaderCalls = @(Get-CommandCalls $runnerAst 'Read-AndValidateLaunchEvidence')
if ($launchReaderCalls.Count -ne 1) { throw 'Expected exactly one production Read-AndValidateLaunchEvidence call.' }
Assert-AssignedCall $launchReaderCalls[0] '$launchEvidence' 'Read-AndValidateLaunchEvidence'
Assert-CommandArguments $launchReaderCalls[0] @("(Join-Path `$hostDirectory 'launch-binding.json')", '$runDirectory', '$record') 'Read-AndValidateLaunchEvidence'

$caseReaderAst = $runnerFunctions['Read-AndValidateCaseEvidence']
$resultCalls = @(Get-CommandCalls $caseReaderAst 'Read-JsonObjectFile')
if ($resultCalls.Count -ne 1) { throw 'Expected exactly one result reader inside Read-AndValidateCaseEvidence.' }
Assert-AssignedCall $resultCalls[0] '$result' 'Read-JsonObjectFile'
Assert-CommandArguments $resultCalls[0] @('$resultPath', '1MB', "'result evidence'") 'Read-JsonObjectFile'

$envelopeCalls = @(Get-CommandCalls $caseReaderAst 'Read-ValidatedResultEnvelope')
if ($envelopeCalls.Count -ne 1) { throw 'Expected exactly one envelope validator inside Read-AndValidateCaseEvidence.' }
Assert-AssignedCall $envelopeCalls[0] '$envelope' 'Read-ValidatedResultEnvelope'
Assert-CommandArguments $envelopeCalls[0] @('$result', '$Case', '$BattleStartedFromLog', '$HostExitCode', '$ChildExitCode', '$DesktopSafe') 'Read-ValidatedResultEnvelope'

$hurtLoaderCalls = @(Get-CommandCalls $caseReaderAst 'Get-HurtEvidenceInput')
if ($hurtLoaderCalls.Count -ne 1) { throw 'Expected exactly one Hurt loader inside Read-AndValidateCaseEvidence.' }
Assert-AssignedCall $hurtLoaderCalls[0] '$hurtEvidence' 'Get-HurtEvidenceInput'
Assert-CommandArguments $hurtLoaderCalls[0] @('$RunDirectory') 'Get-HurtEvidenceInput'
$hurtLoaderGuard = Get-EnclosingIf $hurtLoaderCalls[0]
if ($null -eq $hurtLoaderGuard -or $hurtLoaderGuard.Clauses.Count -ne 1 -or
    $hurtLoaderGuard.Clauses[0].Item1.Extent.Text.Trim() -cne '$validBattle -eq $true') {
    throw 'Production Hurt loader lost its strict validBattle guard.'
}

$hurtAssertCalls = @(Get-CommandCalls $caseReaderAst 'Assert-HurtEvidence')
if ($hurtAssertCalls.Count -ne 1) { throw 'Expected exactly one Hurt assertion inside Read-AndValidateCaseEvidence.' }
Assert-CommandArguments $hurtAssertCalls[0] @('$result', '$hurtEvidence') 'Assert-HurtEvidence'
$hurtAssertGuard = Get-EnclosingIf $hurtAssertCalls[0]
if ($null -eq $hurtAssertGuard -or $hurtAssertGuard.Clauses.Count -ne 1 -or
    $hurtAssertGuard.Clauses[0].Item1.Extent.Text.Trim() -cne '$validBattle') {
    throw 'Production Hurt assertion lost its validBattle gate.'
}
$staticEvidenceCalls = @(Get-CommandCalls $prepareAst 'New-ProbeStaticEvidence')
if ($staticEvidenceCalls.Count -ne 1) { throw 'Expected exactly one production New-ProbeStaticEvidence call.' }
Assert-AssignedCall $staticEvidenceCalls[0] '$staticEvidence' 'New-ProbeStaticEvidence'
Assert-CommandArguments $staticEvidenceCalls[0] @('$RunName', '$runId', '$ilValidationPassed', '$manifestPath',
    '$preparedTerrariaPath', '$probe', '$desktopHost', '$prepareScript', '$startScript', '$probeSource',
    '$patcherSource', '$desktopHostSource', '$runnerScript', '$evidenceTestScript') 'New-ProbeStaticEvidence'
$atomicWrites = @(Get-CommandCalls $prepareAst 'Write-NewUtf8JsonFile')
$manifestWrites = @($atomicWrites | Where-Object { $_.CommandElements[1].Extent.Text.Trim() -ceq '$manifestPath' })
$staticEvidenceWrites = @($atomicWrites | Where-Object { $_.CommandElements[1].Extent.Text.Trim() -ceq '$staticEvidencePath' })
if ($manifestWrites.Count -ne 1 -or $staticEvidenceWrites.Count -ne 1 -or
    @(Get-CommandCalls $prepareAst 'Set-Content').Count -ne 0) {
    throw 'Manifest/static evidence production writes are ambiguous.'
}
Assert-CommandArguments $manifestWrites[0] @('$manifestPath', '$manifest', '8', "'probe manifest'") 'manifest atomic write'
Assert-CommandArguments $staticEvidenceWrites[0] @('$staticEvidencePath', '$staticEvidence', '6', "'probe static evidence'") 'static evidence atomic write'
if ($manifestWrites[0].Extent.EndOffset -ge $staticEvidenceCalls[0].Extent.StartOffset -or
    $staticEvidenceCalls[0].Extent.EndOffset -ge $staticEvidenceWrites[0].Extent.StartOffset) {
    throw 'Static evidence is not built from the real helper after manifest write and before its sidecar write.'
}
$sentinelAssignments = @($prepareAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.AssignmentStatementAst] -and
        $node.Left.Extent.Text.Trim() -ceq '$ilValidationPassed'
}, $true))
if ($sentinelAssignments.Count -ne 2 -or $sentinelAssignments[0].Right.Extent.Text.Trim() -cne '$false' -or
    $sentinelAssignments[1].Right.Extent.Text.Trim() -cne '$true' -or
    $sentinelAssignments[1].Extent.EndOffset -ge $manifestWrites[0].Extent.StartOffset) {
    throw 'IL-validation sentinel is not a unique false-to-true gate before evidence production.'
}
$script:passed = 0
$script:failed = 0
$script:fixtureCounter = 0

function New-HurtSummary([int]$Rows = 0) {
    [pscustomobject]@{
        schema = 'chaite-hurt-observation-summary/v1'
        file = if ($Rows -gt 0) { 'hurt-observations.jsonl' } else { $null }
        calls = $Rows; returns = $Rows; rows = $Rows; serializedRows = $Rows
        maximumRows = 2048; droppedRows = 0; maximumDepth = if ($Rows -gt 0) { 1 } else { 0 }
        depthCapacity = 16; depthOverflows = 0; unpairedObservations = 0; pendingDepth = 0
        suppressedPendingDepth = 0; observerErrors = 0; sourceReadFailures = 0
        flushes = 0; bufferFlushRows = 16; bufferFlushCharacters = 65536
        maximumBufferedCharacters = 0; charactersWritten = 0; bufferedRowsAfterFinalFlush = 0
    }
}

function New-HurtRowLine {
    [ordered]@{
        schema = 'chaite-hurt-observation/v1'; sequence = 1; tickBefore = 100; tickAfter = 100
        reason = [ordered]@{ custom = $null; sourceOtherIndex = $null; declaredProjectileType = 100 }
        request = [ordered]@{ damage = 40; hitDirection = -1; pvp = $false; quiet = $false; crit = $false; cooldownCounter = 0; dodgeable = $true }
        actualReturn = 27.5
        player = [ordered]@{ index = 0; lifeBefore = 400; lifeAfter = 373; lifeDelta = 27 }
        source = [ordered]@{
            kind = 'projectile'; entityIndex = 12; type = 100; owner = 255; active = $true
            life = $null; lifeMax = $null; damage = 40; hostile = $true; friendly = $false
            position = [ordered]@{ x = 120.5; y = 250.25 }; velocity = [ordered]@{ x = -4.0; y = 1.5 }
        }
    } | ConvertTo-Json -Depth 8 -Compress
}

function New-Evidence([string]$Difficulty, [string]$Scenario = 'eye') {
    $mode = @('classic', 'expert', 'master').IndexOf($Difficulty)
    # The production envelope now requires the native NPC identity for every
    # first observation. Keep the offline fixture aligned with that contract
    # instead of weakening the validator for legacy test data.
    $bossType = if ($Scenario -eq 'destroyer') { 134 } else { 4 }
    $bossLife = if ($Scenario -eq 'destroyer') { 80000 } else { 2800 }
    $summonType = if ($Scenario -eq 'destroyer') { 556 } else { 43 }
    [pscustomobject]@{
        # v2 cases carry the same phase/takeover identity that the production
        # reader validates.  These fixtures exercise the ordinary eye route,
        # so the legacy synthetic spawn is represented by the reviewed
        # `summon` phase at the earliest legal takeover tick.
        Case = [pscustomobject]@{
            Difficulty = $Difficulty; Scenario = $Scenario; Seed = 20260910
            Variant = 'standard'; Phase = 'summon'; TakeoverTick = 120
        }
        Result = [pscustomobject]@{
            schema = 'chaite-boss-result/v1'; schemaVersion = 1; scenario = $Scenario; seed = 20260910
            difficulty = $Difficulty; variant = 'standard'; status = 'loss'; processExitCode = 20; win = $false
            validBattle = $true; battleStarted = $true; deaths = 0; ticks = 150; hits = 0
            requestedPhase = 'summon'; requestedTakeoverTick = 120
            actualTakeoverTick = 120; encounterFixtureReady = $true
            directSpawn = $false; evidenceKind = 'isolated-native-encounter'; readinessEligible = $true
            directSpawnTick = -1; directSpawnAttempted = $false; directSpawnCompleted = $false
            phaseStageAttempted = $false; phaseStaged = $false; phaseVerifiedAtTakeover = $false
            phaseStage = $null; takeoverNativeSnapshot = $null; summonConsumed = $true
            variantEvidence = [pscustomobject]@{
                schema = 'chaite-boss-variant-evidence/v1'; scenario = $Scenario
                expectedVariant = 'standard'; observedVariant = 'standard'; reportedVariant = 'standard'
                capturedAtActivation = $true; captureTick = 120; requestedTakeoverTick = 120
                nativeFlagsVerified = $true; variantMatchesScenario = $true
                mechanicalTrioExpected = $false; mechanicalTrioObserved = $false; allExpectedBossesSeen = $true
                native = [pscustomobject]@{
                    gameMode = $mode; difficulty = $mode + 1; dayTime = $false
                    hardMode = $Scenario -notin @('eye', 'king-slime'); forTheWorthy = $false; zenithWorld = $false
                    drunkWorld = $false; notTheBeesWorld = $false; remixWorld = $false; celebrationWorld = $false
                    constantWorld = $false; noTrapsWorld = $false; skyblockWorld = $false
                }
            }
            nativeDifficultyVerified = $true
            nativeFrames = 150
            bossLifeRemaining = $bossLife
            bossLifeObservation = [pscustomobject]@{
                schema = 'chaite-boss-life-observation/v1'; life = $bossLife; observedTick = 150
                kind = 'active-expected-roots'; activeAtTermination = $true; activeExpectedRootCountAtTermination = 1
                lastActiveExpectedRootTick = 150; lastActiveExpectedRootLife = $bossLife; lastActiveExpectedRootCount = 1
                lastActiveExpectedRoots = @([pscustomobject]@{ slot = 10; type = $bossType; life = $bossLife; lifeMax = $bossLife })
            }
            nativeDifficulty = [pscustomobject]@{
                gameMode = $mode; worldFileGameMode = $mode; difficulty = $mode + 1
                worldFileSeed = 20260910; expertMode = $mode -gt 0; masterMode = $mode -eq 2
                hardMode = $Scenario -notin @('eye', 'king-slime'); forTheWorthy = $false; zenithWorld = $false
                drunkWorld = $false; notTheBeesWorld = $false; remixWorld = $false; celebrationWorld = $false
                constantWorld = $false; noTrapsWorld = $false; skyblockWorld = $false
            }
            battleRandom = [pscustomobject]@{
                installedAfterSetup = $true; actualAndNativeNamedColdStateVerified = $true
                actualStreamConsumedForFingerprint = $false; seed = 20260910
                unpausedUpdateSeedInitial = 20260910; unpausedUpdateSeedAdvances = 150
                referenceChecks = 151; independentTwinFingerprint = @(10, 20, 30, 40, 50, 60, 70, 80)
            }
            firstObservedBosses = @([pscustomobject]@{
                type = $bossType; lifeMax = $bossLife; gameMode = $mode; difficulty = $mode + 1; npcDifficulty = $mode + 1
            })
            diagnostics = [pscustomobject]@{ poisonedFrames = 0; lifeLossFramesWhilePoisoned = 0; hurt = New-HurtSummary }
            equipment = [pscustomobject]@{ summonType = $summonType; summonCount = 1 }
        }
        DesktopExit = [pscustomobject]@{ Schema = 'chaite-desktop-exit/v1'; HostExitCode = 20 }
        ChildExitCode = 20; DesktopSafe = $true; BattleStartedFromLog = $false
        HurtEvidence = [pscustomobject]@{ Exists = $false; Lines = @(); RawCharacters = 0 }
    }
}

function Sync-HurtPhysicalCounters($Evidence) {
    [long]$charactersWritten = 0
    [long]$bufferedCharacters = 0
    [long]$maximumBufferedCharacters = 0
    [int]$bufferedRows = 0
    [int]$flushes = 0
    foreach ($line in @($Evidence.HurtEvidence.Lines)) {
        [long]$serializedCharacters = $line.Length + [Environment]::NewLine.Length
        if ($bufferedCharacters -gt 0 -and $bufferedCharacters + $serializedCharacters -gt 65536) {
            $charactersWritten += $bufferedCharacters; $bufferedCharacters = 0; $bufferedRows = 0; $flushes++
        }
        $bufferedCharacters += $serializedCharacters
        $bufferedRows++
        $maximumBufferedCharacters = [Math]::Max($maximumBufferedCharacters, $bufferedCharacters)
        if ($bufferedRows -ge 16 -or $bufferedCharacters -ge 65536) {
            $charactersWritten += $bufferedCharacters; $bufferedCharacters = 0; $bufferedRows = 0; $flushes++
        }
    }
    if ($bufferedCharacters -gt 0) {
        $charactersWritten += $bufferedCharacters; $bufferedCharacters = 0; $bufferedRows = 0; $flushes++
    }
    $Evidence.Result.diagnostics.hurt.flushes = $flushes
    $Evidence.Result.diagnostics.hurt.maximumBufferedCharacters = $maximumBufferedCharacters
    $Evidence.Result.diagnostics.hurt.charactersWritten = $charactersWritten
    $Evidence.Result.diagnostics.hurt.bufferedRowsAfterFinalFlush = $bufferedRows
    $Evidence.HurtEvidence.RawCharacters = $charactersWritten
}

function New-OneHurtEvidence {
    $evidence = New-Evidence 'classic'
    $evidence.Result.diagnostics.hurt = New-HurtSummary 1
    $evidence.HurtEvidence.Exists = $true
    $evidence.HurtEvidence.Lines = @(New-HurtRowLine)
    Sync-HurtPhysicalCounters $evidence
    return $evidence
}

function New-ManyHurtEvidence([ValidateRange(1, 2048)][int]$Rows) {
    $evidence = New-Evidence 'classic'
    $evidence.Result.diagnostics.hurt = New-HurtSummary $Rows
    $lines = @()
    for ($sequence = 1; $sequence -le $Rows; $sequence++) {
        $row = New-HurtRowLine | ConvertFrom-Json
        $row.sequence = $sequence
        $lines += ($row | ConvertTo-Json -Depth 8 -Compress)
    }
    $evidence.HurtEvidence.Exists = $true
    $evidence.HurtEvidence.Lines = $lines
    Sync-HurtPhysicalCounters $evidence
    return $evidence
}

function Set-HurtRowField($Evidence, [string]$Path, $Value, [switch]$Remove) {
    $row = $Evidence.HurtEvidence.Lines[0] | ConvertFrom-Json
    $segments = $Path.Split('.')
    $parent = $row
    for ($index = 0; $index -lt $segments.Count - 1; $index++) { $parent = $parent.PSObject.Properties[$segments[$index]].Value }
    $name = $segments[$segments.Count - 1]
    if ($Remove) { $parent.PSObject.Properties.Remove($name) }
    else { $parent.PSObject.Properties[$name].Value = $Value }
    $Evidence.HurtEvidence.Lines[0] = $row | ConvertTo-Json -Depth 8 -Compress
    Sync-HurtPhysicalCounters $Evidence
}

function Set-EvidenceField($Evidence, [string]$Path, $Value, [switch]$Remove) {
    $segments = $Path.Split('.')
    $parent = $Evidence.Result
    for ($index = 0; $index -lt $segments.Count - 1; $index++) {
        $segment = $segments[$index]
        if ($segment -match '^\d+$') { $parent = $parent[[int]$segment] }
        else { $parent = $parent.PSObject.Properties[$segment].Value }
    }
    $name = $segments[$segments.Count - 1]
    if ($Remove) { $parent.PSObject.Properties.Remove($name) }
    else { $parent.PSObject.Properties[$name].Value = $Value }
}

function Write-EvidenceFixture($Evidence) {
    $script:fixtureCounter++
    $runDirectory = Join-Path $script:fixtureRoot ('case-{0:D4}' -f $script:fixtureCounter)
    if (Test-Path -LiteralPath $runDirectory) { throw 'Fresh evidence fixture unexpectedly already exists.' }
    [void][IO.Directory]::CreateDirectory($runDirectory)
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $resultJson = $Evidence.Result | ConvertTo-Json -Depth 16 -Compress
    [IO.File]::WriteAllText((Join-Path $runDirectory 'result.json'), $resultJson, $utf8)
    if ($Evidence.HurtEvidence.Exists -eq $true) {
        $raw = [Text.StringBuilder]::new()
        foreach ($line in @($Evidence.HurtEvidence.Lines)) {
            [void]$raw.Append([string]$line)
            [void]$raw.Append([Environment]::NewLine)
        }
        [IO.File]::WriteAllText((Join-Path $runDirectory 'hurt-observations.jsonl'), $raw.ToString(), $utf8)
    }
    return $runDirectory
}

function Test-Evidence([string]$Name, $Evidence, [bool]$ShouldAccept,
    [scriptblock]$MutateFixture = $null, [string]$ExpectedFailurePattern = $null) {
    # Apply the same JSON boundary as result.json, including Int32/Int64 and
    # Boolean deserialization, instead of validating a convenient hashtable.
    $roundTrip = $Evidence | ConvertTo-Json -Depth 16 -Compress | ConvertFrom-Json
    $case = $roundTrip.Case
    $runDirectory = Write-EvidenceFixture $roundTrip
    if ($null -ne $MutateFixture) { & $MutateFixture $runDirectory }
    $accepted = $true
    $failure = $null
    try {
        $hostExitCode = Read-DesktopExitCode $roundTrip.DesktopExit
        $record = [pscustomobject]@{ Result = $null }
        $validatedCase = Read-AndValidateCaseEvidence $runDirectory $case $roundTrip.BattleStartedFromLog $hostExitCode $roundTrip.ChildExitCode $roundTrip.DesktopSafe $record
        if ($null -eq $validatedCase.Result -or $null -eq $validatedCase.Envelope -or $null -eq $record.Result -or
            $validatedCase.Envelope.Variant -cne $roundTrip.Case.Variant -or
            $validatedCase.Envelope.ReadinessEligible -ne $roundTrip.Result.readinessEligible) {
            throw 'Production case reader did not preserve its validated result/envelope.'
        }
    }
    catch { $accepted = $false; $failure = $_.Exception.Message }
    $failureMatched = [string]::IsNullOrWhiteSpace($ExpectedFailurePattern) -or
        (-not $accepted -and $failure -match $ExpectedFailurePattern)
    if ($accepted -eq $ShouldAccept -and $failureMatched) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        if ($ShouldAccept) { Write-Output "FAIL $Name -- valid evidence rejected: $failure" }
        elseif ($accepted) { Write-Output "FAIL $Name -- malformed evidence was accepted" }
        else { Write-Output "FAIL $Name -- rejected for the wrong reason: $failure" }
    }
}

function Test-VariantEvidenceContract([string]$Name, [string]$Scenario, [string]$Variant,
    [bool]$DayTime, [bool]$ZenithWorld, [bool]$MechanicalTrio, [bool]$ShouldAccept) {
    $evidence = New-Evidence 'classic'
    $evidence.Case.Scenario = $Scenario
    $evidence.Case.Variant = $Variant
    $evidence.Result.scenario = $Scenario
    $evidence.Result.variant = $Variant
    $variantEvidence = $evidence.Result.variantEvidence
    $variantEvidence.scenario = $Scenario
    $variantEvidence.expectedVariant = $Variant
    $variantEvidence.observedVariant = $Variant
    $variantEvidence.reportedVariant = $Variant
    $variantEvidence.native.dayTime = $DayTime
    $variantEvidence.native.zenithWorld = $ZenithWorld
    $variantEvidence.mechanicalTrioExpected = $MechanicalTrio
    $variantEvidence.mechanicalTrioObserved = $MechanicalTrio
    $accepted = $true
    try { Assert-VariantEvidence $evidence.Result $evidence.Case $true | Out-Null }
    catch { $accepted = $false }
    if ($accepted -eq $ShouldAccept) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        Write-Output "FAIL $Name -- variant contract acceptance was $accepted"
    }
}

function Test-StaticEvidenceProducer {
    $directory = Join-Path $script:fixtureRoot 'game-probe-offline-provenance'
    if (Test-Path -LiteralPath $directory) { throw 'Fresh static-provenance fixture unexpectedly already exists.' }
    [void][IO.Directory]::CreateDirectory($directory)
    $paths = [ordered]@{
        ManifestSha256 = Join-Path $directory 'probe-manifest.json'
        PreparedTerrariaSha256 = Join-Path $directory 'Terraria.exe'
        PreparedGameProbeSha256 = Join-Path $directory 'Chaite.GameProbe.dll'
        PreparedDesktopHostSha256 = Join-Path $directory 'Chaite.DesktopHost.exe'
        PrepareGameProbeScriptSha256 = Join-Path $directory 'prepare-game-probe.ps1'
        StartIsolatedTestScriptSha256 = Join-Path $directory 'start-isolated-test.ps1'
        GameProbeSourceSha256 = Join-Path $directory 'GameProbe.cs'
        GameProbePatcherSourceSha256 = Join-Path $directory 'GameProbePatcher.cs'
        DesktopHostSourceSha256 = Join-Path $directory 'Chaite.DesktopHost.cs'
        RunBossValidationScriptSha256 = Join-Path $directory 'run-boss-validation.ps1'
        TestBossEvidenceScriptSha256 = Join-Path $directory 'test-boss-evidence.ps1'
    }
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $index = 0
    foreach ($path in $paths.Values) {
        $index++
        [IO.File]::WriteAllText($path, "physical provenance fixture $index", $utf8)
    }
    $runId = [guid]::NewGuid().ToString()
    $evidence = New-ProbeStaticEvidence 'game-probe-offline-provenance' $runId $true $paths.ManifestSha256 `
        $paths.PreparedTerrariaSha256 $paths.PreparedGameProbeSha256 $paths.PreparedDesktopHostSha256 `
        $paths.PrepareGameProbeScriptSha256 $paths.StartIsolatedTestScriptSha256 $paths.GameProbeSourceSha256 `
        $paths.GameProbePatcherSourceSha256 $paths.DesktopHostSourceSha256 $paths.RunBossValidationScriptSha256 `
        $paths.TestBossEvidenceScriptSha256
    $requiredFields = @('Schema', 'RunId', 'RunName', 'IlValidationPassed') + @($paths.Keys)
    $declared = @($evidence.Keys)
    if ($declared.Count -ne $requiredFields.Count -or $evidence.Schema -cne 'chaite-probe-static-evidence/v2' -or
        $evidence.RunId -cne $runId -or $evidence.RunName -cne 'game-probe-offline-provenance' -or $evidence.IlValidationPassed -ne $true) {
        throw 'Actual static-evidence producer emitted the wrong schema, identity, status, or field set.'
    }
    foreach ($name in $requiredFields) {
        if ($declared -cnotcontains $name) { throw "Actual static-evidence producer omitted $name." }
    }
    foreach ($name in $paths.Keys) {
        $actualHash = (Get-FileHash -LiteralPath $paths[$name] -Algorithm SHA256).Hash
        if ($evidence[$name] -cne $actualHash) { throw "Actual static-evidence producer did not hash $name from its physical file." }
    }
    $script:passed++
    Write-Output 'PASS accept-physical-static-provenance'

    $before = $evidence.GameProbeSourceSha256
    [IO.File]::WriteAllText($paths.GameProbeSourceSha256, 'mutated physical GameProbe source', $utf8)
    $mutated = New-ProbeStaticEvidence 'game-probe-offline-provenance' $runId $true $paths.ManifestSha256 `
        $paths.PreparedTerrariaSha256 $paths.PreparedGameProbeSha256 $paths.PreparedDesktopHostSha256 `
        $paths.PrepareGameProbeScriptSha256 $paths.StartIsolatedTestScriptSha256 $paths.GameProbeSourceSha256 `
        $paths.GameProbePatcherSourceSha256 $paths.DesktopHostSourceSha256 $paths.RunBossValidationScriptSha256 `
        $paths.TestBossEvidenceScriptSha256
    if ($mutated.GameProbeSourceSha256 -ceq $before -or
        $mutated.GameProbeSourceSha256 -cne (Get-FileHash -LiteralPath $paths.GameProbeSourceSha256 -Algorithm SHA256).Hash) {
        throw 'Actual static-evidence producer reused a declared/stale GameProbe hash.'
    }
    $script:passed++
    Write-Output 'PASS rehash-mutated-physical-static-provenance'

    $missingRejected = $false
    try {
        [void](New-ProbeStaticEvidence 'game-probe-offline-provenance' $runId $true $paths.ManifestSha256 `
            $paths.PreparedTerrariaSha256 $paths.PreparedGameProbeSha256 $paths.PreparedDesktopHostSha256 `
            $paths.PrepareGameProbeScriptSha256 $paths.StartIsolatedTestScriptSha256 $paths.GameProbeSourceSha256 `
            $paths.GameProbePatcherSourceSha256 $paths.DesktopHostSourceSha256 $paths.RunBossValidationScriptSha256 `
            (Join-Path $directory 'missing-test-boss-evidence.ps1'))
    } catch { $missingRejected = $_.Exception.Message -match 'Missing test-boss-evidence script file' }
    if (-not $missingRejected) { throw 'Actual static-evidence producer did not reject a missing physical provenance file.' }
    $script:passed++
    Write-Output 'PASS reject-missing-physical-static-provenance'

    foreach ($badSentinel in @($false, 'true', 1, (,@($true)))) {
        $rejected = $false
        try {
            [void](New-ProbeStaticEvidence 'game-probe-offline-provenance' $runId $badSentinel $paths.ManifestSha256 `
                $paths.PreparedTerrariaSha256 $paths.PreparedGameProbeSha256 $paths.PreparedDesktopHostSha256 `
                $paths.PrepareGameProbeScriptSha256 $paths.StartIsolatedTestScriptSha256 $paths.GameProbeSourceSha256 `
                $paths.GameProbePatcherSourceSha256 $paths.DesktopHostSourceSha256 $paths.RunBossValidationScriptSha256 `
                $paths.TestBossEvidenceScriptSha256)
        } catch { $rejected = $_.Exception.Message -match 'successful IL validation' }
        if (-not $rejected) { throw 'Static-evidence producer accepted a false or non-Boolean IL-validation sentinel.' }
        $script:passed++
        Write-Output 'PASS reject-invalid-il-validation-sentinel'
    }
}

function Write-TestUtf8Json([string]$Path, $Value) {
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    [IO.File]::WriteAllText($Path, (($Value | ConvertTo-Json -Depth 12) + [Environment]::NewLine), $utf8)
}
function New-PreparedProbeFixture([string]$Name) {
    $project = Join-Path $script:fixtureRoot ('prepared-' + $Name)
    $tools = Join-Path $project 'tools'
    $runName = 'game-probe-' + $Name
    $run = Join-Path (Join-Path $project 'artifacts') $runName
    foreach ($directory in @($tools, $run, (Join-Path $run 'Save'), (Join-Path $run 'Save\Players'),
        (Join-Path $run 'Save\Worlds'), (Join-Path $run 'Chaite'))) {
        [void][IO.Directory]::CreateDirectory($directory)
    }
    $sourceNames = @('prepare-game-probe.ps1', 'start-isolated-test.ps1', 'GameProbe.cs',
        'GameProbePatcher.cs', 'Chaite.DesktopHost.cs', 'run-boss-validation.ps1', 'test-boss-evidence.ps1')
    $sources = [ordered]@{}
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    foreach ($sourceName in $sourceNames) {
        $path = Join-Path $tools $sourceName
        [IO.File]::WriteAllText($path, "physical source $sourceName", $utf8)
        $sources[$sourceName] = $path
    }
    $required = @('Terraria.exe', 'Chaite.Plugin.dll', 'Chaite.Core.dll', 'Chaite.GameProbe.dll',
        'Chaite.DesktopHost.exe', 'GameProbePatcher.exe', 'Chaite.Patcher.exe', 'Mono.Cecil.dll',
        'ReLogic.Native.dll', 'nfd.dll', 'Microsoft.Xna.Framework.Video.dll',
        'Microsoft.Xna.Framework.Content.Pipeline.dll', 'Chaite/config.json')
    foreach ($relative in $required) {
        $path = Join-Path $run $relative
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
        [IO.File]::WriteAllText($path, "physical runtime $relative", $utf8)
    }
    $files = @(foreach ($relative in $required) {
        [ordered]@{ Path = $relative.Replace('\', '/'); Sha256 = (Get-FileHash -LiteralPath (Join-Path $run $relative) -Algorithm SHA256).Hash }
    })
    $runId = [guid]::NewGuid().ToString()
    $manifest = [ordered]@{
        Schema = 'chaite-isolated-probe/v2'; RunId = $runId; CreatedUtc = [DateTime]::UtcNow.ToString('o')
        RunName = $runName; StaticEvidenceFile = 'probe-static-evidence.json'; Mode = 'headless'
        GameVersion = '1.4.5.8'; SourceGameSha256 = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
        SaveDirectory = 'Save'; SocialDisabled = $true; LaunchGuardBeforeLogging = $true; SyntheticHotkeysOnly = $true
        InputPluginSha256 = (Get-FileHash -LiteralPath (Join-Path $run 'Chaite.Plugin.dll') -Algorithm SHA256).Hash
        InputCoreSha256 = (Get-FileHash -LiteralPath (Join-Path $run 'Chaite.Core.dll') -Algorithm SHA256).Hash
        ProbeSourceSha256 = (Get-FileHash -LiteralPath $sources['GameProbe.cs'] -Algorithm SHA256).Hash
        ProbePatcherSourceSha256 = (Get-FileHash -LiteralPath $sources['GameProbePatcher.cs'] -Algorithm SHA256).Hash
        PrepareGameProbeScriptSha256 = (Get-FileHash -LiteralPath $sources['prepare-game-probe.ps1'] -Algorithm SHA256).Hash
        StartIsolatedTestScriptSha256 = (Get-FileHash -LiteralPath $sources['start-isolated-test.ps1'] -Algorithm SHA256).Hash
        RunBossValidationScriptSha256 = (Get-FileHash -LiteralPath $sources['run-boss-validation.ps1'] -Algorithm SHA256).Hash
        TestBossEvidenceScriptSha256 = (Get-FileHash -LiteralPath $sources['test-boss-evidence.ps1'] -Algorithm SHA256).Hash
        DesktopHostSourceSha256 = (Get-FileHash -LiteralPath $sources['Chaite.DesktopHost.cs'] -Algorithm SHA256).Hash
        Files = $files
    }
    $manifestPath = Join-Path $run 'probe-manifest.json'
    Write-TestUtf8Json $manifestPath $manifest
    $sidecar = New-ProbeStaticEvidence $runName $runId $true $manifestPath (Join-Path $run 'Terraria.exe') `
        (Join-Path $run 'Chaite.GameProbe.dll') (Join-Path $run 'Chaite.DesktopHost.exe') `
        $sources['prepare-game-probe.ps1'] $sources['start-isolated-test.ps1'] $sources['GameProbe.cs'] `
        $sources['GameProbePatcher.cs'] $sources['Chaite.DesktopHost.cs'] $sources['run-boss-validation.ps1'] `
        $sources['test-boss-evidence.ps1']
    $sidecarPath = Join-Path $run 'probe-static-evidence.json'
    Write-TestUtf8Json $sidecarPath $sidecar
    return [pscustomobject]@{
        Project = $project; Tools = $tools; Run = $run; RunName = $runName; RunId = $runId
        Target = Join-Path $run 'Terraria.exe'; ManifestPath = $manifestPath; SidecarPath = $sidecarPath
        Sources = $sources
    }
}
function Test-PreparedProbeConsumer([string]$Name, [scriptblock]$Mutate = $null, [bool]$ShouldAccept = $false,
    [string]$ExpectedFailurePattern = $null) {
    $script:fixtureCounter++
    $fixture = New-PreparedProbeFixture (('consumer-{0:D4}' -f $script:fixtureCounter))
    if ($null -ne $Mutate) { & $Mutate $fixture }
    $accepted = $true
    $failure = $null
    try {
        $binding = Read-AndValidatePreparedProbe $fixture.Target $fixture.Project $fixture.Tools
        if ($null -eq $binding -or $binding.RunId -cne $fixture.RunId) { throw 'Production consumer returned the wrong binding.' }
    } catch { $accepted = $false; $failure = $_.Exception.Message }
    $matched = [string]::IsNullOrWhiteSpace($ExpectedFailurePattern) -or (-not $accepted -and $failure -match $ExpectedFailurePattern)
    if ($accepted -eq $ShouldAccept -and $matched) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        if ($accepted) { Write-Output "FAIL $Name -- tampered prepared probe was accepted" }
        else { Write-Output "FAIL $Name -- rejected unexpectedly: $failure" }
    }
}
function Set-TestJsonField([string]$Path, [string]$Field, $Value, [switch]$Remove, [switch]$FileEntry) {
    $document = Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json
    $target = if ($FileEntry) { $document.Files[0] } else { $document }
    if ($Remove) { $target.PSObject.Properties.Remove($Field) }
    else { $target.PSObject.Properties[$Field].Value = $Value }
    Write-TestUtf8Json $Path $document
}
function Sync-TestManifestSidecarHash($Fixture) {
    $sidecar = Get-Content -LiteralPath $Fixture.SidecarPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $sidecar.ManifestSha256 = (Get-FileHash -LiteralPath $Fixture.ManifestPath -Algorithm SHA256).Hash
    Write-TestUtf8Json $Fixture.SidecarPath $sidecar
}
function Test-PreparedProbeConsumerMatrix {
    Test-PreparedProbeConsumer 'accept-physical-prepared-probe-consumer' $null $true

    foreach ($relative in @('Terraria.exe', 'Chaite.GameProbe.dll', 'Chaite.DesktopHost.exe')) {
        Test-PreparedProbeConsumer "reject-mutated-$relative" {
            param($fixture) [IO.File]::AppendAllText((Join-Path $fixture.Run $relative), 'tamper')
        } $false 'modified|binding changed'
    }
    Test-PreparedProbeConsumer 'reject-mutated-manifest-after-sidecar' {
        param($fixture) [IO.File]::AppendAllText($fixture.ManifestPath, ' ')
    } $false 'ManifestSha256|binding changed'
    foreach ($source in @('prepare-game-probe.ps1', 'start-isolated-test.ps1', 'GameProbe.cs',
        'GameProbePatcher.cs', 'Chaite.DesktopHost.cs', 'run-boss-validation.ps1', 'test-boss-evidence.ps1')) {
        Test-PreparedProbeConsumer "reject-mutated-source-$source" {
            param($fixture) [IO.File]::AppendAllText($fixture.Sources[$source], 'tamper')
        } $false 'provenance differs'
    }

    $sidecarScalarFields = @('Schema', 'RunId', 'RunName', 'ManifestSha256', 'PreparedTerrariaSha256',
        'PreparedGameProbeSha256', 'PreparedDesktopHostSha256', 'PrepareGameProbeScriptSha256',
        'StartIsolatedTestScriptSha256', 'GameProbeSourceSha256', 'GameProbePatcherSourceSha256',
        'DesktopHostSourceSha256', 'RunBossValidationScriptSha256', 'TestBossEvidenceScriptSha256')
    foreach ($field in $sidecarScalarFields) {
        Test-PreparedProbeConsumer "reject-sidecar-empty-array-$field" {
            param($fixture) Set-TestJsonField $fixture.SidecarPath $field ([object[]]@())
        } $false 'JSON string scalar'
    }
    Test-PreparedProbeConsumer 'reject-sidecar-single-array-hash' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.SidecarPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Set-TestJsonField $fixture.SidecarPath 'PreparedTerrariaSha256' ([object[]]@([string]$document.PreparedTerrariaSha256))
    } $false 'JSON string scalar'
    Test-PreparedProbeConsumer 'reject-sidecar-multiple-array-hash' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.SidecarPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $expected = [string]$document.PreparedTerrariaSha256
        Set-TestJsonField $fixture.SidecarPath 'PreparedTerrariaSha256' ([object[]]@($expected, $expected))
    } $false 'JSON string scalar'
    Test-PreparedProbeConsumer 'reject-sidecar-array-boolean' {
        param($fixture) Set-TestJsonField $fixture.SidecarPath 'IlValidationPassed' ([object[]]@($true))
    } $false 'JSON Boolean scalar'

    foreach ($field in @('Schema', 'RunId', 'CreatedUtc', 'RunName', 'StaticEvidenceFile', 'Mode', 'GameVersion',
        'SourceGameSha256', 'SaveDirectory', 'InputPluginSha256', 'InputCoreSha256', 'ProbeSourceSha256',
        'ProbePatcherSourceSha256', 'PrepareGameProbeScriptSha256', 'StartIsolatedTestScriptSha256',
        'RunBossValidationScriptSha256', 'TestBossEvidenceScriptSha256', 'DesktopHostSourceSha256')) {
        Test-PreparedProbeConsumer "reject-manifest-empty-array-$field" {
            param($fixture) Set-TestJsonField $fixture.ManifestPath $field ([object[]]@())
        } $false 'JSON string scalar'
    }
    foreach ($field in @('SocialDisabled', 'LaunchGuardBeforeLogging', 'SyntheticHotkeysOnly')) {
        Test-PreparedProbeConsumer "reject-manifest-array-boolean-$field" {
            param($fixture) Set-TestJsonField $fixture.ManifestPath $field ([object[]]@($true))
        } $false 'JSON Boolean scalar'
    }
    foreach ($shape in @(
        @{ Name = 'empty'; Values = [object[]]@() },
        @{ Name = 'single'; Values = [object[]]@('Terraria.exe') },
        @{ Name = 'multiple'; Values = [object[]]@('Terraria.exe', 'Terraria.exe') }
    )) {
        Test-PreparedProbeConsumer "reject-manifest-file-path-$($shape.Name)-array" {
            param($fixture)
            Set-TestJsonField $fixture.ManifestPath 'Path' $shape.Values -FileEntry
            Sync-TestManifestSidecarHash $fixture
        } $false 'JSON string scalar'
    }
    foreach ($shapeName in @('empty', 'single', 'multiple')) {
        Test-PreparedProbeConsumer "reject-manifest-file-hash-$shapeName-array" {
            param($fixture)
            $document = Get-Content -LiteralPath $fixture.ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $expected = [string]$document.Files[0].Sha256
            if ($shapeName -ceq 'empty') { $value = [object[]]@() }
            elseif ($shapeName -ceq 'single') { $value = [object[]]@($expected) }
            else { $value = [object[]]@($expected, $expected) }
            Set-TestJsonField $fixture.ManifestPath 'Sha256' $value -FileEntry
            Sync-TestManifestSidecarHash $fixture
        } $false 'JSON string scalar'
    }
    foreach ($case in @(
        @{ Name = 'sidecar-extra-field'; File = 'SidecarPath'; Action = 'extra' },
        @{ Name = 'sidecar-missing-field'; File = 'SidecarPath'; Action = 'missing' },
        @{ Name = 'manifest-extra-field'; File = 'ManifestPath'; Action = 'extra' },
        @{ Name = 'manifest-missing-field'; File = 'ManifestPath'; Action = 'missing' },
        @{ Name = 'file-entry-extra-field'; File = 'ManifestPath'; Action = 'file-extra' },
        @{ Name = 'file-entry-missing-field'; File = 'ManifestPath'; Action = 'file-missing' }
    )) {
        Test-PreparedProbeConsumer "reject-$($case.Name)" {
            param($fixture)
            $path = $fixture.($case.File)
            $document = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
            if ($case.Action -ceq 'extra') { $document | Add-Member -NotePropertyName Unexpected -NotePropertyValue 1 }
            elseif ($case.Action -ceq 'missing') { $document.PSObject.Properties.Remove('Schema') }
            elseif ($case.Action -ceq 'file-extra') { $document.Files[0] | Add-Member -NotePropertyName Unexpected -NotePropertyValue 1 }
            else { $document.Files[0].PSObject.Properties.Remove('Sha256') }
            Write-TestUtf8Json $path $document
            if ($case.Action -like 'file-*') { Sync-TestManifestSidecarHash $fixture }
        } $false 'field set changed|Missing'
    }
    foreach ($fileField in @('ManifestPath', 'SidecarPath')) {
        Test-PreparedProbeConsumer "reject-invalid-utf8-$fileField" {
            param($fixture) [IO.File]::WriteAllBytes($fixture.$fileField, [byte[]](0x7B, 0xC3, 0x28, 0x7D))
        } $false 'strict UTF-8'
    }

    $script:fixtureCounter++
    $reparseFixture = New-PreparedProbeFixture (('consumer-{0:D4}' -f $script:fixtureCounter))
    $junctionTarget = Join-Path $reparseFixture.Project 'junction-target'
    $junction = Join-Path $reparseFixture.Run 'unexpected-junction'
    [void][IO.Directory]::CreateDirectory($junctionTarget)
    try {
        [void](New-Item -ItemType Junction -Path $junction -Target $junctionTarget -ErrorAction Stop)
        $rejected = $false
        try { [void](Read-AndValidatePreparedProbe $reparseFixture.Target $reparseFixture.Project $reparseFixture.Tools) }
        catch { $rejected = $_.Exception.Message -match 'Linked probe descendants|Reparse' }
        if (-not $rejected) { throw 'Production prepared-probe consumer accepted a descendant reparse point.' }
        $script:passed++
        Write-Output 'PASS reject-prepared-probe-descendant-reparse'
    } finally {
        if (Test-Path -LiteralPath $junction) { Remove-Item -LiteralPath $junction -Force }
    }
}
function New-LaunchEvidenceFixture([string]$Name) {
    $fixture = New-PreparedProbeFixture $Name
    $binding = Read-AndValidatePreparedProbe $fixture.Target $fixture.Project $fixture.Tools
    $startHash = (Get-FileHash -LiteralPath $fixture.Sources['start-isolated-test.ps1'] -Algorithm SHA256).Hash
    $hostSourceHash = (Get-FileHash -LiteralPath $fixture.Sources['Chaite.DesktopHost.cs'] -Algorithm SHA256).Hash
    $pinPlanPath = Join-Path $fixture.Project 'launch-pin-plan.txt'
    [IO.File]::WriteAllText($pinPlanPath, "offline pin fixture`n", [Text.UTF8Encoding]::new($false, $true))
    $pinPlanSha256 = (Get-FileHash -LiteralPath $pinPlanPath -Algorithm SHA256).Hash
    $created = [DateTime]::UtcNow
    $launchBinding = [ordered]@{
        Schema = 'chaite-launch-binding/v1'; RunId = $binding.RunId; RunName = $binding.RunName
        CreatedUtc = $created.ToString('o'); PrelaunchValidationPassed = $true
        LockIntent = 'launcher-and-desktop-host-fileshare-read-until-job-empty'
        PinnedPreparedFileCount = @($binding.PinnedEntries).Count + 2
        PinPlanSha256 = $pinPlanSha256
        ManifestSha256 = $binding.ManifestSha256; StaticEvidenceSha256 = $binding.StaticEvidenceSha256
        PreparedTerrariaSha256 = $binding.PreparedTerrariaSha256
        PreparedGameProbeSha256 = $binding.PreparedGameProbeSha256
        PreparedDesktopHostSha256 = $binding.PreparedDesktopHostSha256
        StartIsolatedTestScriptSha256 = $startHash; DesktopHostSourceSha256 = $hostSourceHash
    }
    $launchBindingPath = Join-Path $fixture.Project 'launch-binding.json'
    Write-TestUtf8Json $launchBindingPath $launchBinding
    $launchBindingSha256 = (Get-FileHash -LiteralPath $launchBindingPath -Algorithm SHA256).Hash
    $completion = [ordered]@{
        Schema = 'chaite-host-lock-completion/v1'; RunId = $binding.RunId
        CompletedUtc = $created.AddSeconds(1).ToString('o'); CompletionStatus = 'child-exited-job-empty'
        LockStrategy = 'desktop-host-fileshare-read-through-job-empty'
        LaunchBindingSha256 = $launchBindingSha256; PinPlanSha256 = $pinPlanSha256
        PinnedPreparedFileCount = @($binding.PinnedEntries).Count + 2; ChildExitCode = 20
        RootProcessSignaled = $true; JobActiveProcesses = 0; PinnedHandlesRevalidated = $true
    }
    $completionPath = Join-Path $fixture.Project 'host-lock-completion.json'
    Write-TestUtf8Json $completionPath $completion
    $fixture | Add-Member -NotePropertyName LaunchBindingPath -NotePropertyValue $launchBindingPath
    $fixture | Add-Member -NotePropertyName CompletionPath -NotePropertyValue $completionPath
    $fixture | Add-Member -NotePropertyName ExpectedRecord -NotePropertyValue ([pscustomobject]@{
        StartIsolatedTestScriptSha256 = $startHash; DesktopHostSourceSha256 = $hostSourceHash
        HostExitCode = 20; ChildExitCode = 20
    })
    return $fixture
}
function Test-LaunchEvidenceConsumer([string]$Name, [scriptblock]$Mutate = $null, [bool]$ShouldAccept = $false,
    [string]$ExpectedFailurePattern = $null) {
    $script:fixtureCounter++
    $fixture = New-LaunchEvidenceFixture (('launch-evidence-{0:D4}' -f $script:fixtureCounter))
    if ($null -ne $Mutate) { & $Mutate $fixture }
    $accepted = $true; $failure = $null
    try {
        $result = Read-AndValidateLaunchEvidence $fixture.LaunchBindingPath $fixture.Run $fixture.ExpectedRecord
        if ($null -eq $result -or $result.RunId -cne $fixture.RunId) { throw 'Production attestation consumer returned the wrong binding.' }
    } catch { $accepted = $false; $failure = $_.Exception.Message }
    $matched = [string]::IsNullOrWhiteSpace($ExpectedFailurePattern) -or (-not $accepted -and $failure -match $ExpectedFailurePattern)
    if ($accepted -eq $ShouldAccept -and $matched) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        if ($accepted) { Write-Output "FAIL $Name -- tampered launch evidence was accepted" }
        else { Write-Output "FAIL $Name -- rejected unexpectedly: $failure" }
    }
}
function Test-LaunchEvidenceConsumerMatrix {
    Test-LaunchEvidenceConsumer 'accept-physical-launch-evidence' $null $true
    foreach ($field in @('Schema', 'RunId', 'RunName', 'CreatedUtc', 'LockIntent', 'PinPlanSha256', 'ManifestSha256',
        'StaticEvidenceSha256', 'PreparedTerrariaSha256', 'PreparedGameProbeSha256',
        'PreparedDesktopHostSha256', 'StartIsolatedTestScriptSha256', 'DesktopHostSourceSha256')) {
        Test-LaunchEvidenceConsumer "reject-launch-binding-empty-array-$field" {
            param($fixture) Set-TestJsonField $fixture.LaunchBindingPath $field ([object[]]@())
        } $false 'JSON string scalar'
    }
    Test-LaunchEvidenceConsumer 'reject-launch-binding-single-array-hash' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.LaunchBindingPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Set-TestJsonField $fixture.LaunchBindingPath 'ManifestSha256' ([object[]]@([string]$document.ManifestSha256))
    } $false 'JSON string scalar'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-multiple-array-hash' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.LaunchBindingPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $expected = [string]$document.ManifestSha256
        Set-TestJsonField $fixture.LaunchBindingPath 'ManifestSha256' ([object[]]@($expected, $expected))
    } $false 'JSON string scalar'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-array-boolean' {
        param($fixture) Set-TestJsonField $fixture.LaunchBindingPath 'PrelaunchValidationPassed' ([object[]]@($true))
    } $false 'expected JSON boolean'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-array-integer' {
        param($fixture) Set-TestJsonField $fixture.LaunchBindingPath 'PinnedPreparedFileCount' ([object[]]@(15))
    } $false 'expected integer'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-extra-field' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.LaunchBindingPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $document | Add-Member -NotePropertyName Unexpected -NotePropertyValue 1
        Write-TestUtf8Json $fixture.LaunchBindingPath $document
    } $false 'field set changed'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-missing-field' {
        param($fixture) Set-TestJsonField $fixture.LaunchBindingPath 'ManifestSha256' $null -Remove
    } $false 'field set changed|Missing'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-invalid-utf8' {
        param($fixture) [IO.File]::WriteAllBytes($fixture.LaunchBindingPath, [byte[]](0x7B, 0xC3, 0x28, 0x7D))
    } $false 'strict UTF-8'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-after-sidecar-tamper' {
        param($fixture) [IO.File]::AppendAllText($fixture.SidecarPath, ' ')
    } $false 'StaticEvidenceSha256|physical binding'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-after-pin-plan-tamper' {
        param($fixture) [IO.File]::AppendAllText((Join-Path $fixture.Project 'launch-pin-plan.txt'), 'tamper')
    } $false 'PinPlanSha256|physical binding'
    Test-LaunchEvidenceConsumer 'reject-launch-binding-after-terraria-tamper' {
        param($fixture) [IO.File]::AppendAllText($fixture.Target, 'tamper')
    } $false 'PreparedTerrariaSha256|physical binding'

    foreach ($field in @('Schema', 'RunId', 'CompletedUtc', 'CompletionStatus', 'LockStrategy',
        'LaunchBindingSha256', 'PinPlanSha256')) {
        Test-LaunchEvidenceConsumer "reject-host-completion-empty-array-$field" {
            param($fixture) Set-TestJsonField $fixture.CompletionPath $field ([object[]]@())
        } $false 'JSON string scalar'
    }
    foreach ($field in @('RootProcessSignaled', 'PinnedHandlesRevalidated')) {
        Test-LaunchEvidenceConsumer "reject-host-completion-array-boolean-$field" {
            param($fixture) Set-TestJsonField $fixture.CompletionPath $field ([object[]]@($true))
        } $false 'expected JSON boolean'
    }
    foreach ($field in @('PinnedPreparedFileCount', 'ChildExitCode', 'JobActiveProcesses')) {
        Test-LaunchEvidenceConsumer "reject-host-completion-array-integer-$field" {
            param($fixture) Set-TestJsonField $fixture.CompletionPath $field ([object[]]@(0))
        } $false 'expected integer'
    }
    Test-LaunchEvidenceConsumer 'reject-host-completion-single-array-hash' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.CompletionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Set-TestJsonField $fixture.CompletionPath 'LaunchBindingSha256' ([object[]]@([string]$document.LaunchBindingSha256))
    } $false 'JSON string scalar'
    Test-LaunchEvidenceConsumer 'reject-host-completion-multiple-array-hash' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.CompletionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $expected = [string]$document.LaunchBindingSha256
        Set-TestJsonField $fixture.CompletionPath 'LaunchBindingSha256' ([object[]]@($expected, $expected))
    } $false 'JSON string scalar'
    Test-LaunchEvidenceConsumer 'reject-host-completion-correct-single-array-count' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.CompletionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        Set-TestJsonField $fixture.CompletionPath 'PinnedPreparedFileCount' ([object[]]@([int]$document.PinnedPreparedFileCount))
    } $false 'expected integer'
    Test-LaunchEvidenceConsumer 'reject-host-completion-correct-single-array-exit' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'ChildExitCode' ([object[]]@(20))
    } $false 'expected integer'
    Test-LaunchEvidenceConsumer 'reject-host-completion-correct-single-array-job-empty' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'JobActiveProcesses' ([object[]]@(0))
    } $false 'expected integer'
    Test-LaunchEvidenceConsumer 'reject-host-completion-missing-file' {
        param($fixture) [IO.File]::Delete($fixture.CompletionPath)
    } $false 'Missing host lock completion'
    Test-LaunchEvidenceConsumer 'reject-host-completion-extra-field' {
        param($fixture)
        $document = Get-Content -LiteralPath $fixture.CompletionPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $document | Add-Member -NotePropertyName Unexpected -NotePropertyValue 1
        Write-TestUtf8Json $fixture.CompletionPath $document
    } $false 'field set changed'
    Test-LaunchEvidenceConsumer 'reject-host-completion-missing-field' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'PinPlanSha256' $null -Remove
    } $false 'field set changed|Missing'
    Test-LaunchEvidenceConsumer 'reject-host-completion-invalid-utf8' {
        param($fixture) [IO.File]::WriteAllBytes($fixture.CompletionPath, [byte[]](0x7B, 0xC3, 0x28, 0x7D))
    } $false 'strict UTF-8'
    Test-LaunchEvidenceConsumer 'reject-host-completion-launch-binding-hash-mismatch' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'LaunchBindingSha256' ('0' * 64)
    } $false 'control-file hashes'
    Test-LaunchEvidenceConsumer 'reject-host-completion-pin-plan-hash-mismatch' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'PinPlanSha256' ('0' * 64)
    } $false 'control-file hashes'
    Test-LaunchEvidenceConsumer 'reject-host-completion-count-mismatch' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'PinnedPreparedFileCount' 1
    } $false 'pinned-file count'
    Test-LaunchEvidenceConsumer 'reject-host-completion-child-exit-mismatch' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'ChildExitCode' 0
    } $false 'exit code'
    Test-LaunchEvidenceConsumer 'reject-host-completion-root-not-signaled' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'RootProcessSignaled' $false
    } $false 'did not prove'
    Test-LaunchEvidenceConsumer 'reject-host-completion-job-not-empty' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'JobActiveProcesses' 1
    } $false 'JobActiveProcesses'
    Test-LaunchEvidenceConsumer 'reject-host-completion-pins-not-revalidated' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'PinnedHandlesRevalidated' $false
    } $false 'did not prove'
    Test-LaunchEvidenceConsumer 'reject-host-completion-before-binding' {
        param($fixture) Set-TestJsonField $fixture.CompletionPath 'CompletedUtc' ([DateTime]::UtcNow.AddDays(-1).ToString('o'))
    } $false 'CompletedUtc'
}

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$artifactPrefix = $artifactRoot + [IO.Path]::DirectorySeparatorChar
$script:fixtureRoot = [IO.Path]::GetFullPath((Join-Path $artifactRoot ('boss-evidence-fixture-' + [guid]::NewGuid().ToString('N'))))
if (-not $script:fixtureRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or
    [IO.Path]::GetFileName($script:fixtureRoot) -cnotmatch '^boss-evidence-fixture-[a-f0-9]{32}$') {
    throw 'Unsafe offline evidence fixture path.'
}
Assert-NoReparse $script:fixtureRoot
[void][IO.Directory]::CreateDirectory($script:fixtureRoot)
try {
Test-StaticEvidenceProducer
Test-PreparedProbeConsumerMatrix
Test-LaunchEvidenceConsumerMatrix
foreach ($difficulty in @('classic', 'expert', 'master')) {
    foreach ($scenario in @('eye', 'destroyer')) {
        Test-Evidence "accept-$difficulty-$scenario" (New-Evidence $difficulty $scenario) $true
    }
}
Test-VariantEvidenceContract 'accept-standard-variant-native-flags' 'eye' 'standard' $false $false $false $true
Test-VariantEvidenceContract 'accept-mechanical-trio-variant-native-topology' 'mechanical-mayhem' 'simultaneous-mechanical-trio' $false $false $true $true
Test-VariantEvidenceContract 'accept-mechdusa-variant-zenith-native-topology' 'mechdusa' 'getfixedboi-mechdusa' $false $true $true $true
Test-VariantEvidenceContract 'reject-mechdusa-without-native-zenith' 'mechdusa' 'getfixedboi-mechdusa' $false $false $true $false
Test-VariantEvidenceContract 'reject-mechanical-trio-without-native-topology' 'mechanical-mayhem' 'simultaneous-mechanical-trio' $false $false $false $false

$winningEnvelope = New-Evidence 'classic'
$winningEnvelope.Result.status = 'win'
$winningEnvelope.Result.processExitCode = 0
$winningEnvelope.Result.win = $true
$winningEnvelope.Result.bossLifeRemaining = 0
$winningEnvelope.Result.bossLifeObservation.life = 0
$winningEnvelope.Result.bossLifeObservation.observedTick = $winningEnvelope.Result.ticks
$winningEnvelope.Result.bossLifeObservation.kind = 'confirmed-victory'
$winningEnvelope.Result.bossLifeObservation.activeAtTermination = $false
$winningEnvelope.Result.bossLifeObservation.activeExpectedRootCountAtTermination = 0
$winningEnvelope.DesktopExit.HostExitCode = 0
$winningEnvelope.ChildExitCode = 0
Test-Evidence 'accept-strict-winning-result-envelope' $winningEnvelope $true

$inactiveLoss = New-Evidence 'classic'
$inactiveLoss.Result.bossLifeObservation.kind = 'last-active-expected-roots'
$inactiveLoss.Result.bossLifeObservation.activeAtTermination = $false
$inactiveLoss.Result.bossLifeObservation.activeExpectedRootCountAtTermination = 0
$inactiveLoss.Result.bossLifeObservation.observedTick = 149
$inactiveLoss.Result.bossLifeObservation.lastActiveExpectedRootTick = 149
Test-Evidence 'accept-inactive-loss-retaining-last-active-expected-root-life' $inactiveLoss $true

$despawnZeroOverwrite = New-Evidence 'classic'
$despawnZeroOverwrite.Result.bossLifeRemaining = 0
$despawnZeroOverwrite.Result.bossLifeObservation.life = 0
$despawnZeroOverwrite.Result.bossLifeObservation.kind = 'last-active-expected-roots'
$despawnZeroOverwrite.Result.bossLifeObservation.activeAtTermination = $false
$despawnZeroOverwrite.Result.bossLifeObservation.activeExpectedRootCountAtTermination = 0
$despawnZeroOverwrite.Result.bossLifeObservation.observedTick = 149
$despawnZeroOverwrite.Result.bossLifeObservation.lastActiveExpectedRootTick = 149
Test-Evidence 'reject-inactive-loss-zero-overwrite' $despawnZeroOverwrite $false $null 'did not preserve'

$slotReuse = New-Evidence 'classic'
$slotReuse.Result.bossLifeObservation.lastActiveExpectedRoots[0].type = 1
Test-Evidence 'reject-last-active-root-slot-reused-by-unexpected-type' $slotReuse $false $null 'unexpected type'

$additiveV1 = New-Evidence 'classic'
$additiveV1.Result | Add-Member -NotePropertyName futureResultField -NotePropertyValue 'allowed'
$additiveV1.Result.bossLifeObservation | Add-Member -NotePropertyName futureObservationField -NotePropertyValue 1
Test-Evidence 'accept-v1-additive-result-and-boss-life-fields' $additiveV1 $true

$poisonedCounters = New-Evidence 'classic'
$poisonedCounters.Result.hits = 2
$poisonedCounters.Result.diagnostics.poisonedFrames = 10
$poisonedCounters.Result.diagnostics.lifeLossFramesWhilePoisoned = 1
Test-Evidence 'accept-bounded-poisoned-life-loss-counters' $poisonedCounters $true

$poisonedLossWithoutPoison = New-Evidence 'classic'
$poisonedLossWithoutPoison.Result.hits = 1
$poisonedLossWithoutPoison.Result.diagnostics.lifeLossFramesWhilePoisoned = 1
Test-Evidence 'reject-poisoned-life-loss-without-poisoned-frame' $poisonedLossWithoutPoison $false $null 'exceeds'

# Keep every origin condition independent: a failure here must be caused by
# the field under test even though the producer labels still claim this is
# readiness-eligible isolated encounter evidence.
foreach ($originCase in @(
    @{ Name = 'direct-spawn'; Field = 'directSpawn'; Value = $true; Pattern = 'direct-spawn mode' },
    @{ Name = 'direct-spawn-attempted'; Field = 'directSpawnAttempted'; Value = $true; Pattern = 'staging provenance' },
    @{ Name = 'direct-spawn-completed'; Field = 'directSpawnCompleted'; Value = $true; Pattern = 'staging provenance' },
    @{ Name = 'phase-stage-attempted'; Field = 'phaseStageAttempted'; Value = $true; Pattern = 'staging provenance' },
    @{ Name = 'phase-staged'; Field = 'phaseStaged'; Value = $true; Pattern = 'staging provenance' },
    @{ Name = 'phase-verified-at-takeover'; Field = 'phaseVerifiedAtTakeover'; Value = $true; Pattern = 'staging provenance' },
    @{ Name = 'phase-stage-report'; Field = 'phaseStage'; Value = [pscustomobject]@{ schema = 'forged' }; Pattern = 'staging report' },
    @{ Name = 'takeover-native-snapshot'; Field = 'takeoverNativeSnapshot'; Value = [pscustomobject]@{ schema = 'forged' }; Pattern = 'staging report' },
    @{ Name = 'summon-not-consumed'; Field = 'summonConsumed'; Value = $false; Pattern = 'summon consumption' },
    @{ Name = 'missing-summon-source'; Field = 'equipment'; Value = $null; Pattern = 'result equipment' },
    @{ Name = 'wrong-summon-type'; Field = 'equipment'; Value = [pscustomobject]@{ summonType = 5120; summonCount = 1 }; Pattern = 'hotbar summon source' },
    @{ Name = 'wrong-summon-count'; Field = 'equipment'; Value = [pscustomobject]@{ summonType = 43; summonCount = 2 }; Pattern = 'hotbar summon source' },
    @{ Name = 'string-summon-type'; Field = 'equipment'; Value = [pscustomobject]@{ summonType = '43'; summonCount = 1 }; Pattern = 'equipment summonType' }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $originCase.Field $originCase.Value
    Test-Evidence "reject-relabeled-$($originCase.Name)-as-formal-evidence" $evidence $false $null $originCase.Pattern
}

Test-Evidence 'reject-invalid-utf8-result-file' (New-Evidence 'classic') $false {
    param($runDirectory)
    [IO.File]::WriteAllBytes((Join-Path $runDirectory 'result.json'), [byte[]](0x7B, 0xC3, 0x28, 0x7D))
} 'strict UTF-8'
Test-Evidence 'reject-invalid-utf8-hurt-file' (New-OneHurtEvidence) $false {
    param($runDirectory)
    [IO.File]::WriteAllBytes((Join-Path $runDirectory 'hurt-observations.jsonl'), [byte[]](0x7B, 0xC3, 0x28, 0x7D))
} 'strict UTF-8'

foreach ($path in @('schema','schemaVersion','scenario','seed','difficulty','variant','variantEvidence','evidenceKind','readinessEligible','status','processExitCode',
    'battleStarted','validBattle','win','deaths','ticks','hits','bossLifeRemaining','bossLifeObservation')) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $path $null -Remove
    Test-Evidence "reject-missing-result-envelope-$path" $evidence $false
}
foreach ($path in @('bossLifeObservation.schema','bossLifeObservation.life','bossLifeObservation.observedTick',
    'bossLifeObservation.kind','bossLifeObservation.activeAtTermination',
    'bossLifeObservation.activeExpectedRootCountAtTermination','bossLifeObservation.lastActiveExpectedRootTick',
    'bossLifeObservation.lastActiveExpectedRootLife','bossLifeObservation.lastActiveExpectedRootCount',
    'bossLifeObservation.lastActiveExpectedRoots')) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $path $null -Remove
    Test-Evidence "reject-missing-$path" $evidence $false
}
foreach ($change in @(
    @{ Path = 'bossLifeRemaining'; Value = 2799 },
    @{ Path = 'bossLifeObservation.schema'; Value = 'chaite-boss-life-observation/v0' },
    @{ Path = 'bossLifeObservation.life'; Value = 2799 },
    @{ Path = 'bossLifeObservation.observedTick'; Value = 149 },
    @{ Path = 'bossLifeObservation.kind'; Value = 'terminal-zero' },
    @{ Path = 'bossLifeObservation.activeAtTermination'; Value = 'true' },
    @{ Path = 'bossLifeObservation.activeExpectedRootCountAtTermination'; Value = 0 },
    @{ Path = 'bossLifeObservation.lastActiveExpectedRootTick'; Value = 149 },
    @{ Path = 'bossLifeObservation.lastActiveExpectedRootLife'; Value = 2799 },
    @{ Path = 'bossLifeObservation.lastActiveExpectedRootCount'; Value = 0 },
    @{ Path = 'bossLifeObservation.lastActiveExpectedRoots'; Value = @() }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $change.Path $change.Value
    Test-Evidence "reject-malformed-$($change.Path)" $evidence $false
}
foreach ($change in @(
    @{ Path = 'schema'; Value = 'chaite-boss-result/v0' },
    @{ Path = 'schemaVersion'; Value = '1' },
    @{ Path = 'schemaVersion'; Value = 1.5 },
    @{ Path = 'scenario'; Value = 'destroyer' },
    @{ Path = 'seed'; Value = '20260910' },
    @{ Path = 'seed'; Value = 20260911 },
    @{ Path = 'difficulty'; Value = 'expert' },
    @{ Path = 'variant'; Value = 'day' },
    @{ Path = 'variant'; Value = 'legacy-standard-fixture' },
    @{ Path = 'evidenceKind'; Value = 'organic-win-rate' },
    @{ Path = 'readinessEligible'; Value = 'true' },
    @{ Path = 'status'; Value = 'LOSS' },
    @{ Path = 'processExitCode'; Value = '20' },
    @{ Path = 'battleStarted'; Value = 'true' },
    @{ Path = 'battleStarted'; Value = 1 },
    @{ Path = 'validBattle'; Value = 'true' },
    @{ Path = 'validBattle'; Value = 1 },
    @{ Path = 'win'; Value = 'false' },
    @{ Path = 'win'; Value = 0 },
    @{ Path = 'deaths'; Value = '0' }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $change.Path $change.Value
    Test-Evidence "reject-malformed-result-envelope-$($change.Path)-[$($change.Value)]" $evidence $false
}

foreach ($hostCase in @(
    @{ Name = 'missing-desktop-exit-schema'; Apply = { param($e) $e.DesktopExit.PSObject.Properties.Remove('Schema') } },
    @{ Name = 'wrong-desktop-exit-schema'; Apply = { param($e) $e.DesktopExit.Schema = 'chaite-desktop-exit/v0' } },
    @{ Name = 'missing-host-exit-code'; Apply = { param($e) $e.DesktopExit.PSObject.Properties.Remove('HostExitCode') } },
    @{ Name = 'string-host-exit-code'; Apply = { param($e) $e.DesktopExit.HostExitCode = '20' } },
    @{ Name = 'boolean-host-exit-code'; Apply = { param($e) $e.DesktopExit.HostExitCode = $true } },
    @{ Name = 'string-child-exit-code'; Apply = { param($e) $e.ChildExitCode = '20' } },
    @{ Name = 'missing-child-exit-code'; Apply = { param($e) $e.ChildExitCode = $null } },
    @{ Name = 'unsafe-desktop'; Apply = { param($e) $e.DesktopSafe = $false } }
)) {
    $evidence = New-Evidence 'classic'
    & $hostCase.Apply $evidence
    Test-Evidence "reject-$($hostCase.Name)" $evidence $false
}
foreach ($difficulty in @('classic', 'expert', 'master')) {
    $mode = @('classic', 'expert', 'master').IndexOf($difficulty)
    $missing = @('nativeDifficultyVerified', 'nativeDifficulty',
        'nativeDifficulty.gameMode', 'nativeDifficulty.worldFileGameMode', 'nativeDifficulty.difficulty',
        'nativeDifficulty.worldFileSeed', 'nativeDifficulty.expertMode', 'nativeDifficulty.masterMode',
        'nativeDifficulty.hardMode', 'nativeDifficulty.forTheWorthy', 'nativeFrames', 'battleRandom',
        'battleRandom.installedAfterSetup', 'battleRandom.actualAndNativeNamedColdStateVerified',
        'battleRandom.actualStreamConsumedForFingerprint', 'battleRandom.seed',
        'battleRandom.unpausedUpdateSeedInitial', 'battleRandom.unpausedUpdateSeedAdvances',
        'battleRandom.referenceChecks', 'battleRandom.independentTwinFingerprint', 'firstObservedBosses',
        'firstObservedBosses.0.lifeMax', 'firstObservedBosses.0.gameMode',
        'firstObservedBosses.0.difficulty', 'firstObservedBosses.0.npcDifficulty')
    foreach ($path in $missing) {
        $evidence = New-Evidence $difficulty
        Set-EvidenceField $evidence $path $null -Remove
        Test-Evidence "reject-$difficulty-missing-$path" $evidence $false
    }
    $wrong = @(
        @{ Path = 'nativeDifficultyVerified'; Value = $false },
        @{ Path = 'nativeDifficulty.gameMode'; Value = ($mode + 1) % 3 },
        @{ Path = 'nativeDifficulty.worldFileGameMode'; Value = ($mode + 1) % 3 },
        @{ Path = 'nativeDifficulty.difficulty'; Value = $mode + 2 },
        @{ Path = 'nativeDifficulty.worldFileSeed'; Value = 20260911 },
        @{ Path = 'nativeDifficulty.expertMode'; Value = $mode -eq 0 },
        @{ Path = 'nativeDifficulty.masterMode'; Value = $mode -ne 2 },
        @{ Path = 'nativeDifficulty.hardMode'; Value = $true },
        @{ Path = 'nativeDifficulty.forTheWorthy'; Value = $true },
        @{ Path = 'battleRandom.installedAfterSetup'; Value = $false },
        @{ Path = 'battleRandom.actualAndNativeNamedColdStateVerified'; Value = $false },
        @{ Path = 'battleRandom.actualStreamConsumedForFingerprint'; Value = $true },
        @{ Path = 'battleRandom.seed'; Value = 20260911 },
        @{ Path = 'battleRandom.unpausedUpdateSeedInitial'; Value = 20260911 },
        @{ Path = 'battleRandom.unpausedUpdateSeedAdvances'; Value = 149 },
        @{ Path = 'battleRandom.referenceChecks'; Value = 149 },
        @{ Path = 'battleRandom.independentTwinFingerprint'; Value = @(1, 2, 3, 4, 5, 6, 7) },
        @{ Path = 'firstObservedBosses'; Value = @() },
        @{ Path = 'firstObservedBosses.0.lifeMax'; Value = 0 },
        @{ Path = 'firstObservedBosses.0.gameMode'; Value = ($mode + 1) % 3 },
        @{ Path = 'firstObservedBosses.0.difficulty'; Value = $mode + 2 },
        @{ Path = 'firstObservedBosses.0.npcDifficulty'; Value = $mode + 2 }
    )
    foreach ($change in $wrong) {
        $evidence = New-Evidence $difficulty
        Set-EvidenceField $evidence $change.Path $change.Value
        Test-Evidence "reject-$difficulty-wrong-$($change.Path)" $evidence $false
    }
}

foreach ($path in @('variantEvidence.schema', 'variantEvidence.scenario', 'variantEvidence.expectedVariant',
    'variantEvidence.observedVariant', 'variantEvidence.reportedVariant', 'variantEvidence.capturedAtActivation',
    'variantEvidence.captureTick', 'variantEvidence.requestedTakeoverTick', 'variantEvidence.nativeFlagsVerified',
    'variantEvidence.variantMatchesScenario', 'variantEvidence.mechanicalTrioExpected',
    'variantEvidence.mechanicalTrioObserved', 'variantEvidence.allExpectedBossesSeen', 'variantEvidence.native',
    'variantEvidence.native.gameMode', 'variantEvidence.native.difficulty', 'variantEvidence.native.dayTime',
    'variantEvidence.native.zenithWorld', 'variantEvidence.native.skyblockWorld')) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $path $null -Remove
    Test-Evidence "reject-missing-$path" $evidence $false
}
foreach ($change in @(
    @{ Path = 'variantEvidence.schema'; Value = 'chaite-boss-variant-evidence/v0' },
    @{ Path = 'variantEvidence.observedVariant'; Value = 'night' },
    @{ Path = 'variantEvidence.reportedVariant'; Value = 'night' },
    @{ Path = 'variantEvidence.capturedAtActivation'; Value = $false },
    @{ Path = 'variantEvidence.captureTick'; Value = 121 },
    @{ Path = 'variantEvidence.nativeFlagsVerified'; Value = $false },
    @{ Path = 'variantEvidence.variantMatchesScenario'; Value = $false },
    @{ Path = 'variantEvidence.native.forTheWorthy'; Value = $true },
    @{ Path = 'variantEvidence.native.zenithWorld'; Value = $true },
    @{ Path = 'variantEvidence.native.dayTime'; Value = 'false' }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $change.Path $change.Value
    Test-Evidence "reject-variant-evidence-$($change.Path)" $evidence $false
}

# Require-Integer is also extracted from production; exercise both valid limits
# and JSON number types that must not silently truncate into a valid tick count.
foreach ($frames in @(121, 24000)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence 'nativeFrames' $frames
    Set-EvidenceField $evidence 'battleRandom.unpausedUpdateSeedAdvances' $frames
    Set-EvidenceField $evidence 'battleRandom.referenceChecks' $frames
    Test-Evidence "accept-nativeFrames-boundary-$frames" $evidence $true
}
foreach ($frames in @(120, 24001, 121.5, '150', $true, $null, [long]2147483648)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence 'nativeFrames' $frames
    Test-Evidence "reject-nativeFrames-value-[$frames]" $evidence $false
}
foreach ($change in @(
    @{ Path = 'battleRandom.independentTwinFingerprint'; Value = @(1, 2, 3, 4, 5, 6, 7, 8, 9) },
    @{ Path = 'battleRandom.independentTwinFingerprint'; Value = '12345678' },
    @{ Path = 'battleRandom.unpausedUpdateSeedAdvances'; Value = 151 },
    @{ Path = 'battleRandom.referenceChecks'; Value = -1 },
    @{ Path = 'nativeDifficultyVerified'; Value = 'true' },
    @{ Path = 'nativeDifficulty.worldFileSeed'; Value = '20260910' },
    @{ Path = 'firstObservedBosses.0.npcDifficulty'; Value = '1' },
    @{ Path = 'battleRandom.actualAndNativeNamedColdStateVerified'; Value = 'true' },
    @{ Path = 'battleRandom.referenceChecks'; Value = '150' },
    @{ Path = 'battleRandom.independentTwinFingerprint'; Value = @('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h') }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $change.Path $change.Value
    Test-Evidence "reject-malformed-$($change.Path)-$($change.Value)" $evidence $false
}

Test-Evidence 'accept-empty-hurt-observation' (New-Evidence 'classic') $true
Test-Evidence 'accept-one-public-projectile-hurt-observation' (New-OneHurtEvidence) $true
$seventeenRows = New-ManyHurtEvidence 17
Test-Evidence 'accept-seventeen-hurt-rows-with-two-flushes' $seventeenRows $true
$unicodeRow = New-OneHurtEvidence
Set-HurtRowField $unicodeRow 'reason.custom' ('unicode-' + [char]0x4F24 + [char]0x5BB3 + [char]::ConvertFromUtf32(0x1F600))
Test-Evidence 'accept-unicode-hurt-row-with-utf16-character-counters' $unicodeRow $true
$unresolvedSource = New-OneHurtEvidence
foreach ($path in @('kind','entityIndex','type','owner','active','life','lifeMax','damage','hostile','friendly','position','velocity')) {
    Set-HurtRowField $unresolvedSource "source.$path" $null
}
Test-Evidence 'accept-hurt-row-with-explicit-null-unresolved-source' $unresolvedSource $true
$zeroDamageCall = New-OneHurtEvidence
Set-HurtRowField $zeroDamageCall 'actualReturn' 0
Set-HurtRowField $zeroDamageCall 'player.lifeAfter' 400
Set-HurtRowField $zeroDamageCall 'player.lifeDelta' 0
Test-Evidence 'accept-zero-damage-hurt-call-with-observation-file' $zeroDamageCall $true

foreach ($path in @('diagnostics','diagnostics.poisonedFrames','diagnostics.lifeLossFramesWhilePoisoned',
    'diagnostics.hurt','diagnostics.hurt.schema','diagnostics.hurt.file',
    'diagnostics.hurt.calls','diagnostics.hurt.returns','diagnostics.hurt.rows','diagnostics.hurt.serializedRows',
    'diagnostics.hurt.maximumRows','diagnostics.hurt.maximumDepth','diagnostics.hurt.depthCapacity',
    'diagnostics.hurt.droppedRows','diagnostics.hurt.depthOverflows','diagnostics.hurt.unpairedObservations',
    'diagnostics.hurt.pendingDepth','diagnostics.hurt.suppressedPendingDepth','diagnostics.hurt.observerErrors',
    'diagnostics.hurt.sourceReadFailures','diagnostics.hurt.flushes','diagnostics.hurt.bufferFlushRows',
    'diagnostics.hurt.bufferFlushCharacters','diagnostics.hurt.maximumBufferedCharacters',
    'diagnostics.hurt.charactersWritten','diagnostics.hurt.bufferedRowsAfterFinalFlush')) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $path $null -Remove
    Test-Evidence "reject-missing-$path" $evidence $false
}

foreach ($change in @(
    @{ Path = 'diagnostics.poisonedFrames'; Value = '0' },
    @{ Path = 'diagnostics.poisonedFrames'; Value = 151 },
    @{ Path = 'diagnostics.lifeLossFramesWhilePoisoned'; Value = '0' },
    @{ Path = 'diagnostics.lifeLossFramesWhilePoisoned'; Value = 1 },
    @{ Path = 'diagnostics.hurt.schema'; Value = 'chaite-hurt-observation-summary/v0' },
    @{ Path = 'diagnostics.hurt.calls'; Value = 1 },
    @{ Path = 'diagnostics.hurt.returns'; Value = 1 },
    @{ Path = 'diagnostics.hurt.rows'; Value = 1 },
    @{ Path = 'diagnostics.hurt.serializedRows'; Value = 1 },
    @{ Path = 'diagnostics.hurt.maximumRows'; Value = 4096 },
    @{ Path = 'diagnostics.hurt.maximumDepth'; Value = 2 },
    @{ Path = 'diagnostics.hurt.depthCapacity'; Value = 32 },
    @{ Path = 'diagnostics.hurt.droppedRows'; Value = 1 },
    @{ Path = 'diagnostics.hurt.depthOverflows'; Value = 1 },
    @{ Path = 'diagnostics.hurt.unpairedObservations'; Value = 1 },
    @{ Path = 'diagnostics.hurt.pendingDepth'; Value = 1 },
    @{ Path = 'diagnostics.hurt.suppressedPendingDepth'; Value = 1 },
    @{ Path = 'diagnostics.hurt.observerErrors'; Value = 1 },
    @{ Path = 'diagnostics.hurt.sourceReadFailures'; Value = 1 },
    @{ Path = 'diagnostics.hurt.bufferedRowsAfterFinalFlush'; Value = 1 },
    @{ Path = 'diagnostics.hurt.bufferFlushRows'; Value = 32 },
    @{ Path = 'diagnostics.hurt.bufferFlushCharacters'; Value = 32768 },
    @{ Path = 'diagnostics.hurt.file'; Value = 'other.jsonl' }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $change.Path $change.Value
    Test-Evidence "reject-malformed-$($change.Path)-$($change.Value)" $evidence $false
}

$zeroRowsPositiveDepth = New-Evidence 'classic'
$zeroRowsPositiveDepth.Result.diagnostics.hurt.maximumDepth = 1
Test-Evidence 'reject-zero-hurt-rows-with-positive-maximum-depth' $zeroRowsPositiveDepth $false
$oneRowZeroDepth = New-OneHurtEvidence
$oneRowZeroDepth.Result.diagnostics.hurt.maximumDepth = 0
Test-Evidence 'reject-hurt-row-with-zero-maximum-depth' $oneRowZeroDepth $false
$oneRowExtraFlush = New-OneHurtEvidence
$oneRowExtraFlush.Result.diagnostics.hurt.flushes++
Test-Evidence 'reject-hurt-row-with-extra-flush' $oneRowExtraFlush $false
$seventeenRowsOneFlush = New-ManyHurtEvidence 17
$seventeenRowsOneFlush.Result.diagnostics.hurt.flushes = 1
Test-Evidence 'reject-seventeen-hurt-rows-with-one-flush' $seventeenRowsOneFlush $false
$wrongMaximumBuffer = New-OneHurtEvidence
$wrongMaximumBuffer.Result.diagnostics.hurt.maximumBufferedCharacters++
Test-Evidence 'reject-wrong-hurt-maximum-buffered-characters' $wrongMaximumBuffer $false
$wrongWrittenCharacters = New-OneHurtEvidence
$wrongWrittenCharacters.Result.diagnostics.hurt.charactersWritten++
Test-Evidence 'reject-wrong-hurt-characters-written' $wrongWrittenCharacters $false
$wrongRawCharacters = New-OneHurtEvidence
Test-Evidence 'reject-hurt-file-raw-character-mismatch' $wrongRawCharacters $false {
    param($runDirectory)
    $path = Join-Path $runDirectory 'hurt-observations.jsonl'
    $utf8 = [Text.UTF8Encoding]::new($false, $true)
    $raw = [IO.File]::ReadAllText($path, $utf8)
    [IO.File]::WriteAllText($path, $raw.Substring(0, $raw.Length - [Environment]::NewLine.Length), $utf8)
} 'deterministic buffer counters disagree'

$unexpectedEmptyFile = New-Evidence 'classic'
$unexpectedEmptyFile.HurtEvidence.Exists = $true
$unexpectedEmptyFile.HurtEvidence.Lines = @('{}')
Test-Evidence 'reject-empty-summary-with-physical-file' $unexpectedEmptyFile $false

$missingOneFile = New-OneHurtEvidence
$missingOneFile.HurtEvidence.Exists = $false
Test-Evidence 'reject-declared-hurt-file-missing' $missingOneFile $false
$missingOneLine = New-OneHurtEvidence
$missingOneLine.HurtEvidence.Lines = @()
Test-Evidence 'reject-hurt-physical-row-count-mismatch' $missingOneLine $false
$invalidJson = New-OneHurtEvidence
$invalidJson.HurtEvidence.Lines = @('{')
Sync-HurtPhysicalCounters $invalidJson
Test-Evidence 'reject-invalid-hurt-jsonl' $invalidJson $false
$arrayWrappedRow = New-OneHurtEvidence
$arrayWrappedRow.HurtEvidence.Lines[0] = '[' + $arrayWrappedRow.HurtEvidence.Lines[0] + ']'
Sync-HurtPhysicalCounters $arrayWrappedRow
Test-Evidence 'reject-array-wrapped-hurt-jsonl-row' $arrayWrappedRow $false

foreach ($path in @('reason','source','reason.custom','reason.sourceOtherIndex','reason.declaredProjectileType',
    'source.kind','source.entityIndex','source.type','source.owner','source.active','source.life','source.lifeMax',
    'source.damage','source.hostile','source.friendly','source.position','source.velocity',
    'source.position.x','source.position.y','source.velocity.x','source.velocity.y')) {
    $evidence = New-OneHurtEvidence
    Set-HurtRowField $evidence $path $null -Remove
    Test-Evidence "reject-missing-hurt-row-$path" $evidence $false
}

foreach ($change in @(
    @{ Path = 'schema'; Value = 'chaite-hurt-observation/v0' },
    @{ Path = 'sequence'; Value = 2 },
    @{ Path = 'tickAfter'; Value = 101 },
    @{ Path = 'actualReturn'; Value = '27.5' },
    @{ Path = 'request.damage'; Value = '40' },
    @{ Path = 'request.hitDirection'; Value = 2 },
    @{ Path = 'request.pvp'; Value = 'false' },
    @{ Path = 'player.index'; Value = 1 },
    @{ Path = 'player.lifeDelta'; Value = 26 },
    @{ Path = 'reason.custom'; Value = ('x' * 241) },
    @{ Path = 'source.kind'; Value = 'enemy' },
    @{ Path = 'source.active'; Value = 1 },
    @{ Path = 'source.position.x'; Value = 'NaN' }
)) {
    $evidence = New-OneHurtEvidence
    Set-HurtRowField $evidence $change.Path $change.Value
    Test-Evidence "reject-malformed-hurt-row-$($change.Path)" $evidence $false
}
} finally {
    $resolvedFixture = [IO.Path]::GetFullPath($script:fixtureRoot)
    if (-not $resolvedFixture.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($resolvedFixture) -cnotmatch '^boss-evidence-fixture-[a-f0-9]{32}$') {
        throw 'Refusing to clean an unsafe offline evidence fixture path.'
    }
    if (Test-Path -LiteralPath $resolvedFixture) { Remove-Item -LiteralPath $resolvedFixture -Recurse -Force }
}

Write-Output "Offline native-evidence regression: $script:passed passed, $script:failed failed."
if ($script:failed -gt 0) { exit 1 }
# This script makes no native process calls. Explicit success is necessary in
# a fresh CI PowerShell process, where LASTEXITCODE would otherwise be null (or
# inherited from an unrelated prior command), despite every assertion passing.
exit 0

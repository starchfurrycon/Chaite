# Offline adversarial regressions for audit-destroyer-native.ps1. Synthetic
# files live only in one GUID-named artifacts child and are precisely removed.
# Existing evidence, Terraria processes and user saves are never opened/written.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$auditScript = Join-Path $PSScriptRoot 'audit-destroyer-native.ps1'
$testRoot = Join-Path $artifactsRoot ('destroyer-native-audit-test-' + [guid]::NewGuid().ToString('N'))
$junctionPath = Join-Path $testRoot 'junction-run'
$script:passed = 0; $script:failed = 0; $script:caseNumber = 0

function Measure-TestBuffer($Lines) {
    [long]$written = 0; [long]$buffered = 0; [long]$maximum = 0
    [int]$rows = 0; [int]$flushes = 0
    foreach ($line in @($Lines)) {
        $serialized = [long]$line.Length + [Environment]::NewLine.Length
        if ($buffered -gt 0 -and $buffered + $serialized -gt 65536) { $written += $buffered; $buffered = 0; $rows = 0; $flushes++ }
        $buffered += $serialized; $rows++; $maximum = [Math]::Max($maximum, $buffered)
        if ($rows -ge 16 -or $buffered -ge 65536) { $written += $buffered; $buffered = 0; $rows = 0; $flushes++ }
    }
    if ($buffered -gt 0) { $written += $buffered; $buffered = 0; $rows = 0; $flushes++ }
    [pscustomobject]@{ Flushes = $flushes; Maximum = $maximum; Written = $written; Remaining = $rows }
}

function New-Player([double]$Wing = 100, [int]$WingsLogic = 1, [double]$WingMax = 100, [bool]$Dead = $false) {
    [ordered]@{
        position = [ordered]@{ x = 33600.0; y = 7958.0 }; velocity = [ordered]@{ x = 4.0; y = 0.0 }
        life = if ($Dead) { 0 } else { 240 }; dead = $Dead; wingTime = $Wing; wingTimeMax = $WingMax
        wingsLogic = $WingsLogic; grapCount = 0; controlUseItem = $true; controlJump = $false
        controlHook = $false; selectedItem = 0
    }
}

function New-Plan([long]$Tick, [string]$Phase, [int]$H, [bool]$Jump = $false, [string]$JumpAction = 'Release') {
    [ordered]@{
        tick = $Tick; strategy = 'destroyer'; phase = $Phase; target = 10; fire = $true; hook = $false
        horizontal = $H; jump = $Jump; jumpAction = $JumpAction; drop = $false; dash = $false
        toggleMount = $false; quickHeal = $false; quickMana = $false; gravityControl = 0
        preferredWeaponSlot = 0; aim = [ordered]@{ x = 33700.0; y = 7900.0 }
        hookAim = [ordered]@{ x = 0.0; y = 0.0 }; riskScore = 60.0
        tacticalMode = 'StablePattern'; weaponIssue = $null
    }
}

function New-Actual([long]$Tick, [int]$H, [bool]$Jump = $false) {
    [ordered]@{
        tick = $Tick; controlUseItem = $true; controlJump = $Jump; controlHook = $false
        controlLeft = $H -eq -1; controlRight = $H -eq 1; controlUp = $false; controlDown = $false
        controlDash = $false; controlMount = $false; controlQuickHeal = $false; controlQuickMana = $false
        controlUseTile = $false; controlThrow = $false; selectedItem = 0; itemTime = 1; itemAnimation = 1
        mouseScreen = [ordered]@{ x = 960; y = 540 }
    }
}

function New-Observation(
    [long]$Tick,
    [string]$Phase = 'classic-supported-pressure',
    [int]$H = 1,
    [double]$Wing = 100,
    [string]$State = 'EngagedAlive',
    [long]$Calls = $Tick,
    [bool]$Jump = $false,
    [string]$JumpAction = 'Release',
    [bool]$WithPlan = $true,
    [int]$WingsLogic = 1,
    [double]$WingMax = 100,
    [int]$ReasonMask = -1
) {
    if ($ReasonMask -lt 0) { $ReasonMask = 2 + $(if ($Tick % 60 -eq 0) { 1 } else { 0 }) }
    [ordered]@{
        schema = 'chaite-boss-observation/v1'; tick = $Tick; nativeFrames = $Tick; reasonMask = $ReasonMask
        transitionsSincePreviousRow = 1; sessionState = $State
        plan = if ($WithPlan) { New-Plan $Tick $Phase $H $Jump $JumpAction } else { $null }
        actualAtApplyReturn = if ($WithPlan) { New-Actual $Tick $H $Jump } else { $null }
        applyPending = $false; applyCalls = $Calls; applyReturns = $Calls
        player = New-Player -Wing $Wing -WingsLogic $WingsLogic -WingMax $WingMax
        npcs = @(); omittedNpcs = 0
    }
}

function New-Terminal([long]$Tick, [long]$Calls) {
    New-Observation -Tick $Tick -State 'SuccessNoDeath' -Calls $Calls -WithPlan $false -ReasonMask 32
}

function New-HurtRow {
    [ordered]@{
        schema = 'chaite-hurt-observation/v1'; sequence = 1; tickBefore = 180; tickAfter = 180
        reason = [ordered]@{ custom = $null; sourceOtherIndex = $null; declaredProjectileType = 100 }
        request = [ordered]@{ damage = 40; hitDirection = -1; pvp = $false; quiet = $false; crit = $false; cooldownCounter = 0; dodgeable = $true }
        actualReturn = 20.0; player = [ordered]@{ index = 0; lifeBefore = 260; lifeAfter = 240; lifeDelta = 20 }
        source = [ordered]@{ kind = 'projectile'; entityIndex = 4; type = 100; owner = 255; active = $true; life = $null; lifeMax = $null; damage = 40; hostile = $true; friendly = $false; position = [ordered]@{ x = 33700.0; y = 7900.0 }; velocity = [ordered]@{ x = -5.0; y = 0.0 } }
    }
}

function New-HurtSummary {
    [ordered]@{
        schema = 'chaite-hurt-observation-summary/v1'; file = 'hurt-observations.jsonl'
        calls = 1; returns = 1; rows = 1; serializedRows = 1; maximumRows = 2048; droppedRows = 0
        maximumDepth = 1; depthCapacity = 16; depthOverflows = 0; unpairedObservations = 0
        pendingDepth = 0; suppressedPendingDepth = 0; observerErrors = 0; sourceReadFailures = 0
        flushes = 0; bufferFlushRows = 16; bufferFlushCharacters = 65536
        maximumBufferedCharacters = 0; charactersWritten = 0; bufferedRowsAfterFinalFlush = 0
    }
}

function New-Summary {
    [ordered]@{
        schema = 'chaite-boss-observation-summary/v1'; file = 'boss-observations.jsonl'; rows = 0
        maximumRows = 2048; droppedRows = 0; reservedTerminalRows = 1; periodicRows = 0
        transitionRows = 0; terminalRows = 0; maximumNpcsPerRow = 48; maximumOmittedNpcs = 0
        totalOmittedNpcs = 0; periodicTicks = 60; edgeMinimumTicks = 15; applyCalls = 0
        applyReturns = 0; unpairedApplyObservations = 0; applyPending = $false; planRows = 0
        actualControlRows = 0; flushes = 0; bufferFlushRows = 16; bufferFlushCharacters = 65536
        maximumBufferedCharacters = 0; charactersWritten = 0; bufferedRowsAfterFinalFlush = 0
        hurt = New-HurtSummary
    }
}

function New-Evidence {
    $summary = New-Summary
    $evidence = [pscustomobject]@{
        Result = [ordered]@{
            schema = 'chaite-boss-result/v1'; schemaVersion = 1; scenario = 'destroyer'; seed = 20260910
            difficulty = 'classic'; difficultyCode = 0; status = 'win'; processExitCode = 0
            outcome = 'SuccessNoDeath'; win = $true; failure = $null; validBattle = $true
            battleStarted = $true; allExpectedBossesSeen = $true; death = $false; deaths = 0
            hits = 1; ticks = 361; nativeFrames = 361; minLife = 40; bossDamage = 80000
            bossLifeRemaining = 0; firstObservedBosses = @([ordered]@{ type = 134; key = 10; tick = 121; life = 80000; lifeMax = 80000 })
            diagnostics = $summary
        }
        Observations = @(
            (New-Observation -Tick 100 -Phase 'classic-acquire-anchor' -H 0),
            (New-Observation -Tick 120 -Phase 'classic-supported-pressure' -H 1),
            (New-Observation -Tick 180 -Phase 'classic-head-exit-committed' -H 1),
            (New-Observation -Tick 240 -Phase 'classic-recover-anchor' -H -1),
            (New-Observation -Tick 300 -Phase 'classic-recover-anchor-complete' -H 0),
            (New-Observation -Tick 360 -Phase 'classic-supported-pressure' -H 1),
            (New-Terminal -Tick 361 -Calls 360)
        )
        Hurt = @((New-HurtRow))
    }
    Sync-Evidence $evidence
    return $evidence
}

function Sync-Evidence($Evidence) {
    $lines = @($Evidence.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    $summary = $Evidence.Result.diagnostics
    $summary.rows = $lines.Count
    $summary.periodicRows = @($Evidence.Observations | Where-Object { ([int]$_.reasonMask -band 1) -ne 0 }).Count
    $summary.transitionRows = @($Evidence.Observations | Where-Object { ([int]$_.reasonMask -band 30) -ne 0 }).Count
    $summary.terminalRows = @($Evidence.Observations | Where-Object { ([int]$_.reasonMask -band 32) -ne 0 }).Count
    $summary.planRows = @($Evidence.Observations | Where-Object { $null -ne $_.plan }).Count
    $summary.actualControlRows = @($Evidence.Observations | Where-Object { $null -ne $_.actualAtApplyReturn }).Count
    $last = $Evidence.Observations[$Evidence.Observations.Count - 1]
    $summary.applyCalls = $last.applyCalls; $summary.applyReturns = $last.applyReturns
    $Evidence.Result.ticks = $last.tick; $Evidence.Result.nativeFrames = $last.nativeFrames
    $buffer = Measure-TestBuffer $lines
    $summary.flushes = $buffer.Flushes; $summary.maximumBufferedCharacters = $buffer.Maximum
    $summary.charactersWritten = $buffer.Written; $summary.bufferedRowsAfterFinalFlush = $buffer.Remaining

    $hurtLines = @($Evidence.Hurt | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    $hurt = $summary.hurt
    $hurt.calls = $hurtLines.Count; $hurt.returns = $hurtLines.Count; $hurt.rows = $hurtLines.Count; $hurt.serializedRows = $hurtLines.Count
    $hurt.file = if ($hurtLines.Count -eq 0) { $null } else { 'hurt-observations.jsonl' }
    $hurt.maximumDepth = if ($hurtLines.Count -eq 0) { 0 } else { 1 }
    $hurtBuffer = Measure-TestBuffer $hurtLines
    $hurt.flushes = $hurtBuffer.Flushes; $hurt.maximumBufferedCharacters = $hurtBuffer.Maximum
    $hurt.charactersWritten = $hurtBuffer.Written; $hurt.bufferedRowsAfterFinalFlush = $hurtBuffer.Remaining
}

function Set-Rows($Evidence, $Rows) { $Evidence.Observations = @($Rows); Sync-Evidence $Evidence }
function Copy-Evidence($Evidence) { return ($Evidence | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json) }
function Copy-Rows($Evidence) { return ,@($Evidence.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress | ConvertFrom-Json }) }
function Write-NoBom([string]$Path, [string]$Text) { [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false)) }

function Write-Evidence($Evidence, [string]$Directory) {
    if (Test-Path -LiteralPath $Directory) { throw "Synthetic directory exists: $Directory" }
    New-Item -ItemType Directory -Path $Directory | Out-Null
    Write-NoBom (Join-Path $Directory 'result.json') ($Evidence.Result | ConvertTo-Json -Depth 20 -Compress)
    $lines = @($Evidence.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    Write-NoBom (Join-Path $Directory 'boss-observations.jsonl') ([string]::Join([Environment]::NewLine, $lines) + [Environment]::NewLine)
    if (@($Evidence.Hurt).Count -gt 0) {
        $hurtLines = @($Evidence.Hurt | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
        Write-NoBom (Join-Path $Directory 'hurt-observations.jsonl') ([string]::Join([Environment]::NewLine, $hurtLines) + [Environment]::NewLine)
    }
}

function Invoke-Directory([string]$Name, [string]$Directory, [bool]$Accept, [string]$Expected = '') {
    $accepted = $true; $failure = $null; $output = $null
    try { $output = & $auditScript -RunDirectory $Directory }
    catch { $accepted = $false; $failure = $_.Exception.Message }
    $detail = [string]::IsNullOrWhiteSpace($Expected) -or ($null -ne $failure -and $failure.Contains($Expected))
    $validOutput = -not $Accept -or ($null -ne $output -and $output.Accepted -eq $true -and $output.EvidenceScope.Contains('sampling proxies'))
    if ($accepted -eq $Accept -and $detail -and $validOutput) { $script:passed++; Write-Output "PASS $Name" }
    else { $script:failed++; Write-Output "FAIL $Name expectedAccept=$Accept actualAccept=$accepted detail=$failure" }
}

function Test-Evidence([string]$Name, $Evidence, [bool]$Accept, [string]$Expected = '') {
    $script:caseNumber++
    $directory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-Evidence $Evidence $directory
    Invoke-Directory $Name $directory $Accept $Expected
}

function New-SinglePhaseEvidence([string]$Phase, [int]$H, [double]$Wing = 100) {
    $e = New-Evidence
    Set-Rows $e @((New-Observation -Tick 100 -Phase $Phase -H $H -Wing $Wing), (New-Terminal -Tick 101 -Calls 100))
    return $e
}

if (-not (Test-Path -LiteralPath $auditScript -PathType Leaf)) { throw 'Missing Destroyer audit script.' }
if (-not (Test-Path -LiteralPath $artifactsRoot -PathType Container)) { New-Item -ItemType Directory -Path $artifactsRoot | Out-Null }
New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    Test-Evidence 'accept-complete-Destroyer-envelope' (New-Evidence) $true
    $emptyHurt = New-Evidence; $emptyHurt.Hurt = @(); Sync-Evidence $emptyHurt
    Test-Evidence 'accept-empty-Hurt-stream' $emptyHurt $true

    $preEngagement = New-Evidence
    Set-Rows $preEngagement @(
        (New-Observation -Tick 80 -Phase 'not-a-destroyer-phase' -H 0 -State 'WaitingForBoss'),
        (New-Observation -Tick 100 -Phase 'classic-acquire-anchor' -H 0),
        (New-Observation -Tick 120 -Phase 'classic-supported-pressure' -H 1),
        (New-Observation -Tick 180 -Phase 'classic-head-exit-committed' -H 1),
        (New-Observation -Tick 240 -Phase 'classic-recover-anchor' -H -1),
        (New-Observation -Tick 300 -Phase 'classic-recover-anchor-complete' -H 0),
        (New-Observation -Tick 360 -Phase 'classic-supported-pressure' -H 1),
        (New-Terminal -Tick 361 -Calls 360)
    )
    Test-Evidence 'accept-action-filter-only-EngagedAlive-nondead' $preEngagement $true

    $headBlocked = New-Evidence
    Set-Rows $headBlocked @(
        (New-Observation -Tick 100 -Phase 'classic-head-exit-committed' -H 1),
        (New-Observation -Tick 120 -Phase 'classic-head-exit-committed-blocked' -H 0),
        (New-Observation -Tick 140 -Phase 'classic-head-exit-committed' -H 1),
        (New-Terminal -Tick 141 -Calls 140)
    )
    Test-Evidence 'accept-head-exit-blocked-without-reversal-or-jump' $headBlocked $true

    $recoverBlocked = New-Evidence
    Set-Rows $recoverBlocked @(
        (New-Observation -Tick 100 -Phase 'classic-recover-anchor' -H -1),
        (New-Observation -Tick 120 -Phase 'classic-recover-anchor-route-blocked' -H 0),
        (New-Observation -Tick 140 -Phase 'classic-recover-anchor' -H -1),
        (New-Observation -Tick 160 -Phase 'classic-recover-anchor-complete' -H 0),
        (New-Terminal -Tick 161 -Calls 160)
    )
    Test-Evidence 'accept-recover-blocked-with-saved-proxy-direction' $recoverBlocked $true

    $reversal61 = New-Evidence
    Set-Rows $reversal61 @(
        (New-Observation -Tick 100 -H 1), (New-Observation -Tick 130 -H -1),
        (New-Observation -Tick 161 -H 1), (New-Terminal -Tick 162 -Calls 161)
    )
    Test-Evidence 'accept-a-minusA-A-over-61-ticks' $reversal61 $true

    $lowBoundary = New-Evidence
    Set-Rows $lowBoundary @(
        (New-Observation -Tick 100 -Phase 'classic-supported-pressure' -H 1 -Wing 32),
        (New-Observation -Tick 220 -Phase 'classic-recover-anchor' -H -1 -Wing 20),
        (New-Observation -Tick 700 -Phase 'classic-recover-anchor-complete' -H 0 -Wing 90),
        (New-Terminal -Tick 701 -Calls 700)
    )
    Test-Evidence 'accept-low-wing-entry-120-and-restore-600-boundaries' $lowBoundary $true

    $aboveLow = New-Evidence
    Set-Rows $aboveLow @((New-Observation -Tick 100 -Wing 33), (New-Observation -Tick 160 -Wing 33), (New-Terminal -Tick 161 -Calls 160))
    Test-Evidence 'accept-wing-33-outside-low-proxy-policy' $aboveLow $true

    $allPhases = New-Evidence
    Set-Rows $allPhases @(
        (New-Observation -Tick 100 -Phase 'classic-acquire-anchor' -H 0),
        (New-Observation -Tick 115 -Phase 'classic-acquire-anchor-no-safe-support' -H 0),
        (New-Observation -Tick 130 -Phase 'classic-reacquire-anchor-airborne-closed' -H 0),
        (New-Observation -Tick 145 -Phase 'classic-reacquire-anchor-no-safe-support' -H 0),
        (New-Observation -Tick 160 -Phase 'classic-reacquire-anchor' -H 0),
        (New-Observation -Tick 175 -Phase 'classic-supported-pressure-brake' -H 0),
        (New-Observation -Tick 190 -Phase 'classic-supported-pressure' -H 1),
        (New-Observation -Tick 205 -Phase 'classic-supported-probe-pressure' -H 1),
        (New-Observation -Tick 220 -Phase 'classic-head-exit-committed-blocked' -H 0),
        (New-Observation -Tick 235 -Phase 'classic-head-exit-committed' -H 1),
        (New-Observation -Tick 250 -Phase 'classic-recover-anchor-route-blocked' -H 0),
        (New-Observation -Tick 265 -Phase 'classic-recover-anchor-recharge' -H 0),
        (New-Observation -Tick 280 -Phase 'classic-recover-anchor' -H 1),
        (New-Observation -Tick 295 -Phase 'classic-recover-anchor-complete' -H 0),
        (New-Observation -Tick 310 -Phase 'classic-supported-pressure' -H 1),
        (New-Terminal -Tick 311 -Calls 310)
    )
    Test-Evidence 'accept-complete-reviewed-phase-catalog' $allPhases $true

    foreach ($phase in @('classic-supported-pressure','classic-supported-probe-pressure','classic-head-exit-committed','classic-recover-anchor')) {
        Test-Evidence "reject-moving-H0-$phase" (New-SinglePhaseEvidence $phase 0) $false 'emitted H=0'
    }
    foreach ($phase in @('classic-acquire-anchor','classic-supported-pressure-brake','classic-head-exit-committed-blocked','classic-recover-anchor-recharge')) {
        Test-Evidence "reject-zero-axis-H1-$phase" (New-SinglePhaseEvidence $phase 1) $false 'zero-axis phase'
    }

    $blockedJump = New-SinglePhaseEvidence 'classic-head-exit-committed-blocked' 0
    $rows = Copy-Rows $blockedJump; $rows[0].plan.jump = $true; $rows[0].plan.jumpAction = 'Hold'; $rows[0].actualAtApplyReturn.controlJump = $true
    Set-Rows $blockedJump $rows
    Test-Evidence 'reject-blocked-head-exit-jump' $blockedJump $false 'zero-axis phase'

    Test-Evidence 'reject-unsupported-fixture-phase' (New-SinglePhaseEvidence 'classic-unsupported-destroyer-fixture' 0) $false 'unsupported/unreviewed'
    Test-Evidence 'reject-recover-invalid-state-phase' (New-SinglePhaseEvidence 'classic-recover-anchor-invalid-state' 0) $false 'unsupported/unreviewed'
    Test-Evidence 'reject-unprefixed-phase' (New-SinglePhaseEvidence 'supported-pressure' 1) $false 'unsupported/unreviewed'

    foreach ($field in @('dash','toggleMount','hook','drop')) {
        $e = New-Evidence; $rows = Copy-Rows $e; $rows[1].plan.$field = $true; Set-Rows $e $rows
        Test-Evidence "reject-closure-mobility-$field" $e $false 'OwnsMovementClosure proxy'
    }
    $gravity = New-Evidence; $rows = Copy-Rows $gravity; $rows[1].plan.gravityControl = 1; Set-Rows $gravity $rows
    Test-Evidence 'reject-closure-gravity-control' $gravity $false 'OwnsMovementClosure proxy'

    $directionMismatch = New-Evidence; $rows = Copy-Rows $directionMismatch
    $rows[1].actualAtApplyReturn.controlLeft = $true; $rows[1].actualAtApplyReturn.controlRight = $false; Set-Rows $directionMismatch $rows
    Test-Evidence 'reject-final-plan-returned-H-mismatch' $directionMismatch $false 'contradict plan'

    $tickMismatch = New-Evidence; $rows = Copy-Rows $tickMismatch; $rows[1].plan.tick = 119; Set-Rows $tickMismatch $rows
    Test-Evidence 'reject-final-plan-tick-mismatch' $tickMismatch $false 'plan/actual tick differs'

    $headReverse = New-Evidence
    Set-Rows $headReverse @(
        (New-Observation -Tick 100 -Phase 'classic-head-exit-committed' -H 1),
        (New-Observation -Tick 120 -Phase 'classic-head-exit-committed-blocked' -H 0),
        (New-Observation -Tick 140 -Phase 'classic-head-exit-committed' -H -1),
        (New-Terminal -Tick 141 -Calls 140)
    )
    Test-Evidence 'reject-head-exit-recorded-side-reversal' $headReverse $false 'head-exit reversed'

    $recoverReverse = New-Evidence
    Set-Rows $recoverReverse @(
        (New-Observation -Tick 100 -Phase 'classic-recover-anchor' -H 1),
        (New-Observation -Tick 120 -Phase 'classic-recover-anchor-route-blocked' -H 0),
        (New-Observation -Tick 140 -Phase 'classic-recover-anchor' -H -1),
        (New-Observation -Tick 160 -Phase 'classic-recover-anchor-complete' -H 0),
        (New-Terminal -Tick 161 -Calls 160)
    )
    Test-Evidence 'reject-recover-recorded-direction-reversal' $recoverReverse $false 'left its recorded return direction'

    $rapid60 = New-Evidence
    Set-Rows $rapid60 @((New-Observation -Tick 100 -H 1), (New-Observation -Tick 130 -H -1), (New-Observation -Tick 160 -H 1), (New-Terminal -Tick 161 -Calls 160))
    Test-Evidence 'reject-a-minusA-A-at-60-ticks' $rapid60 $false 'spans only 60 ticks'

    $lowEntry121 = New-Evidence
    Set-Rows $lowEntry121 @(
        (New-Observation -Tick 100 -Wing 32), (New-Observation -Tick 221 -Phase 'classic-recover-anchor' -H -1 -Wing 20),
        (New-Observation -Tick 700 -Phase 'classic-recover-anchor-complete' -H 0 -Wing 90), (New-Terminal -Tick 701 -Calls 700)
    )
    Test-Evidence 'reject-low-wing-recovery-entry-121' $lowEntry121 $false 'entry took 121 ticks'

    $lowRestore601 = New-Evidence
    Set-Rows $lowRestore601 @(
        (New-Observation -Tick 100 -Wing 32), (New-Observation -Tick 200 -Phase 'classic-recover-anchor' -H -1 -Wing 20),
        (New-Observation -Tick 701 -Phase 'classic-recover-anchor-complete' -H 0 -Wing 90), (New-Terminal -Tick 702 -Calls 701)
    )
    Test-Evidence 'reject-low-wing-restore-601' $lowRestore601 $false 'did not restore within 600 ticks'

    $lowUnresolved = New-Evidence
    Set-Rows $lowUnresolved @((New-Observation -Tick 100 -Wing 32), (New-Observation -Tick 160 -Phase 'classic-recover-anchor' -H -1 -Wing 20), (New-Terminal -Tick 161 -Calls 160))
    Test-Evidence 'reject-low-wing-unresolved-at-end' $lowUnresolved $false 'without bounded low-wing restoration'

    $recovery601 = New-Evidence
    Set-Rows $recovery601 @((New-Observation -Tick 100 -Phase 'classic-recover-anchor' -H 1), (New-Observation -Tick 701 -Phase 'classic-recover-anchor-complete' -H 0), (New-Terminal -Tick 702 -Calls 701))
    Test-Evidence 'reject-recovery-episode-601' $recovery601 $false 'exceeds 600 ticks'

    $recoveryUnresolved = New-Evidence
    Set-Rows $recoveryUnresolved @((New-Observation -Tick 100 -Phase 'classic-recover-anchor' -H 1), (New-Observation -Tick 160 -Phase 'classic-recover-anchor' -H 1), (New-Terminal -Tick 161 -Calls 160))
    Test-Evidence 'reject-recovery-unresolved-at-end' $recoveryUnresolved $false 'unresolved recovery episode'

    $wrongWingMax = New-Evidence; $rows = Copy-Rows $wrongWingMax; $rows[1].player.wingTimeMax = 99; Set-Rows $wrongWingMax $rows
    Test-Evidence 'reject-unreviewed-wingTimeMax' $wrongWingMax $false 'reviewed Demon-wing proxy'
    $wrongWingsLogic = New-Evidence; $rows = Copy-Rows $wrongWingsLogic; $rows[1].player.wingsLogic = 2; Set-Rows $wrongWingsLogic $rows
    Test-Evidence 'reject-unreviewed-wingsLogic' $wrongWingsLogic $false 'reviewed Demon-wing proxy'

    $dropped = New-Evidence; $dropped.Result.diagnostics.droppedRows = 1
    Test-Evidence 'reject-dropped-observation' $dropped $false 'droppedRows must equal 0'
    $omitted = New-Evidence; $rows = Copy-Rows $omitted; $rows[1].omittedNpcs = 1; Set-Rows $omitted $rows
    Test-Evidence 'reject-omitted-NPC-row' $omitted $false 'omitted NPCs'
    $unpairedApply = New-Evidence; $unpairedApply.Result.diagnostics.applyReturns--
    Test-Evidence 'reject-unpaired-ApplyPlan-summary' $unpairedApply $false 'not one-to-one'
    $unpairedHurt = New-Evidence; $unpairedHurt.Result.diagnostics.hurt.returns = 0
    Test-Evidence 'reject-unpaired-Hurt' $unpairedHurt $false 'not one-to-one'
    $badFlush = New-Evidence; $badFlush.Result.diagnostics.charactersWritten++
    Test-Evidence 'reject-observation-flush-counter-mismatch' $badFlush $false 'flush/buffer counters disagree'

    foreach ($resultCase in @(
        @{ Name = 'historical-loss-signature'; Apply = { param($e) $e.Result.status = 'loss'; $e.Result.outcome = 'FailedAfterDeath'; $e.Result.win = $false; $e.Result.death = $true; $e.Result.deaths = 1; $e.Result.minLife = 0; $e.Result.bossDamage = 42117 }; Expected = "status must equal 'win'" },
        @{ Name = 'death'; Apply = { param($e) $e.Result.death = $true }; Expected = 'records a death' },
        @{ Name = 'minLife-39'; Apply = { param($e) $e.Result.minLife = 39 }; Expected = 'below 40' },
        @{ Name = 'rootDamage-79999'; Apply = { param($e) $e.Result.bossDamage = 79999 }; Expected = 'below 80000' },
        @{ Name = 'wrong-root-lifeMax'; Apply = { param($e) $e.Result.firstObservedBosses[0].lifeMax = 79999 }; Expected = 'lifeMax 80000' }
    )) {
        $e = New-Evidence; & $resultCase.Apply $e
        Test-Evidence "reject-$($resultCase.Name)" $e $false $resultCase.Expected
    }

    $wrongSchema = New-Evidence; $rows = Copy-Rows $wrongSchema; $rows[0].schema = 'chaite-boss-observation/v0'; Set-Rows $wrongSchema $rows
    Test-Evidence 'reject-wrong-observation-schema' $wrongSchema $false 'wrong schema'
    $duplicate = New-Evidence
    Set-Rows $duplicate @((New-Observation -Tick 100), (New-Observation -Tick 100), (New-Terminal -Tick 101 -Calls 100))
    Test-Evidence 'reject-duplicate-observation-tick' $duplicate $false 'strictly increasing'

    $invalid = New-Evidence; $script:caseNumber++; $invalidDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-Evidence $invalid $invalidDirectory
    $tail = @($invalid.Observations | Select-Object -Skip 1 | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    Write-NoBom (Join-Path $invalidDirectory 'boss-observations.jsonl') ('{' + [Environment]::NewLine + [string]::Join([Environment]::NewLine, $tail) + [Environment]::NewLine)
    Invoke-Directory 'reject-invalid-observation-JSON' $invalidDirectory $false 'invalid JSON'

    $emptyLine = New-Evidence; $script:caseNumber++; $emptyDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-Evidence $emptyLine $emptyDirectory
    $serialized = @($emptyLine.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    Write-NoBom (Join-Path $emptyDirectory 'boss-observations.jsonl') ($serialized[0] + [Environment]::NewLine + [Environment]::NewLine + [string]::Join([Environment]::NewLine, $serialized[1..($serialized.Count - 1)]) + [Environment]::NewLine)
    Invoke-Directory 'reject-empty-JSONL-row' $emptyDirectory $false 'contains an empty row'

    Invoke-Directory 'reject-directory-outside-artifacts' $projectRoot $false 'strictly inside project artifacts'
    $junctionTarget = Join-Path $testRoot 'junction-target'; Write-Evidence (New-Evidence) $junctionTarget
    $null = New-Item -ItemType Junction -Path $junctionPath -Target $junctionTarget
    Invoke-Directory 'reject-reparse-run-directory' $junctionPath $false 'reparse point'
} finally {
    $junction = Get-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
    if ($null -ne $junction) {
        if (($junction.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0) { throw 'Synthetic junction unexpectedly changed type.' }
        [IO.Directory]::Delete($junctionPath)
    }
    $full = [IO.Path]::GetFullPath($testRoot)
    $prefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $entry = Get-Item -LiteralPath $full -Force -ErrorAction SilentlyContinue
    $resolved = if ($null -eq $entry) { $null } else { [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $full).Path) }
    if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 -and
        $resolved -eq $full -and $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($full).StartsWith('destroyer-native-audit-test-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $full -Recurse -Force
    } elseif ($null -ne $entry) { throw 'Refusing unsafe Destroyer audit cleanup target.' }
}

Write-Output "Offline Destroyer native-audit regression: $script:passed passed, $script:failed failed. No game process, existing evidence or user save was touched."
if ($script:failed -gt 0) { exit 1 }
exit 0

# Offline filesystem regressions for audit-twins-native.ps1. Every synthetic
# fixture is created under one fresh project artifacts directory and removed in
# finally. No game executable, foreground window, user save or network is used.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$auditScript = Join-Path $PSScriptRoot 'audit-twins-native.ps1'
$testRoot = Join-Path $artifactsRoot ('twins-native-audit-test-' + [guid]::NewGuid().ToString('N'))
$junctionPath = Join-Path $testRoot 'junction-run'
$script:passed = 0
$script:failed = 0
$script:caseNumber = 0

function Measure-TestBuffer($Lines) {
    [long]$charactersWritten = 0
    [long]$bufferedCharacters = 0
    [long]$maximumBufferedCharacters = 0
    [int]$bufferedRows = 0
    [int]$flushes = 0
    foreach ($line in @($Lines)) {
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
    [pscustomobject]@{
        Flushes = $flushes; MaximumBufferedCharacters = $maximumBufferedCharacters
        CharactersWritten = $charactersWritten; BufferedRowsAfterFinalFlush = $bufferedRows
    }
}

function New-TestPlayer([double]$WingTime = 100, [bool]$Dead = $false) {
    [ordered]@{
        position = [ordered]@{ x = 33600.0; y = 7958.0 }
        velocity = [ordered]@{ x = 4.0; y = 0.0 }
        life = if ($Dead) { 0 } else { 200 }
        dead = $Dead; wingTime = $WingTime; wingTimeMax = 100.0; wingsLogic = 1; grapCount = 0
        controlUseItem = $true; controlJump = $false; controlHook = $false; selectedItem = 0
    }
}

function New-TestPlan([long]$Tick, [int]$Horizontal, [string]$Phase) {
    [ordered]@{
        tick = $Tick; strategy = 'twins'; phase = $Phase; target = 2; fire = $true; hook = $false
        horizontal = $Horizontal; jump = $false; jumpAction = 'Release'; drop = $false; dash = $false
        toggleMount = $false; quickHeal = $false; quickMana = $false; gravityControl = 0
        preferredWeaponSlot = 0; aim = [ordered]@{ x = 33700.0; y = 7900.0 }
        hookAim = [ordered]@{ x = 0.0; y = 0.0 }; riskScore = 40.0; tacticalMode = 'Sustain'
        weaponIssue = $null
    }
}

function New-TestActual([long]$Tick, [int]$Horizontal) {
    [ordered]@{
        tick = $Tick; controlUseItem = $true; controlJump = $false; controlHook = $false
        controlLeft = $Horizontal -eq -1; controlRight = $Horizontal -eq 1
        controlUp = $false; controlDown = $false; controlDash = $false; controlMount = $false
        controlQuickHeal = $false; controlQuickMana = $false; controlUseTile = $false; controlThrow = $false
        selectedItem = 0; itemTime = 1; itemAnimation = 1; mouseScreen = [ordered]@{ x = 960; y = 540 }
    }
}

function New-TestObservation(
    [long]$Tick,
    [int]$Horizontal = 1,
    [string]$Phase = 'classic-spaz-first-hover-runway-supported-run',
    [double]$WingTime = 100,
    [string]$SessionState = 'EngagedAlive',
    [long]$ApplyCalls = $Tick,
    [int]$ReasonMask = 1,
    [bool]$WithPlan = $true
) {
    $plan = if ($WithPlan) { New-TestPlan $Tick $Horizontal $Phase } else { $null }
    $actual = if ($WithPlan) { New-TestActual $Tick $Horizontal } else { $null }
    [ordered]@{
        schema = 'chaite-boss-observation/v1'; tick = $Tick; nativeFrames = $Tick
        reasonMask = $ReasonMask; transitionsSincePreviousRow = 0; sessionState = $SessionState
        plan = $plan; actualAtApplyReturn = $actual; applyPending = $false
        applyCalls = $ApplyCalls; applyReturns = $ApplyCalls; player = New-TestPlayer -WingTime $WingTime
        npcs = @(); omittedNpcs = 0
    }
}

function New-TerminalObservation([long]$Tick, [long]$ApplyCalls) {
    New-TestObservation -Tick $Tick -SessionState 'SuccessNoDeath' -ApplyCalls $ApplyCalls -ReasonMask 32 -WithPlan $false
}

function New-TestHurtRow {
    [ordered]@{
        schema = 'chaite-hurt-observation/v1'; sequence = 1; tickBefore = 120; tickAfter = 120
        reason = [ordered]@{ custom = $null; sourceOtherIndex = $null; declaredProjectileType = 100 }
        request = [ordered]@{ damage = 40; hitDirection = -1; pvp = $false; quiet = $false; crit = $false; cooldownCounter = 0; dodgeable = $true }
        actualReturn = 20.0
        player = [ordered]@{ index = 0; lifeBefore = 220; lifeAfter = 200; lifeDelta = 20 }
        source = [ordered]@{
            kind = 'projectile'; entityIndex = 4; type = 100; owner = 255; active = $true
            life = $null; lifeMax = $null; damage = 40; hostile = $true; friendly = $false
            position = [ordered]@{ x = 33700.0; y = 7900.0 }; velocity = [ordered]@{ x = -5.0; y = 0.0 }
        }
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

function New-ObservationSummary {
    [ordered]@{
        schema = 'chaite-boss-observation-summary/v1'; file = 'boss-observations.jsonl'
        rows = 0; maximumRows = 2048; droppedRows = 0; reservedTerminalRows = 1
        periodicRows = 0; transitionRows = 0; terminalRows = 0; maximumNpcsPerRow = 48
        maximumOmittedNpcs = 0; totalOmittedNpcs = 0; periodicTicks = 60; edgeMinimumTicks = 15
        applyCalls = 0; applyReturns = 0; unpairedApplyObservations = 0; applyPending = $false
        planRows = 0; actualControlRows = 0; flushes = 0; bufferFlushRows = 16
        bufferFlushCharacters = 65536; maximumBufferedCharacters = 0; charactersWritten = 0
        bufferedRowsAfterFinalFlush = 0; hurt = New-HurtSummary
    }
}

function New-TestEvidence {
    $diagnostics = New-ObservationSummary
    $result = [ordered]@{
        schema = 'chaite-boss-result/v1'; schemaVersion = 1; scenario = 'twins'; seed = 20260910
        difficulty = 'classic'; difficultyCode = 0; status = 'win'; processExitCode = 0
        outcome = 'SuccessNoDeath'; win = $true; failure = $null; validBattle = $true
        battleStarted = $true; allExpectedBossesSeen = $true; death = $false; deaths = 0
        hits = 1; ticks = 161; nativeFrames = 161; minLife = 40; bossDamage = 43000
        bossLifeRemaining = 0
        firstObservedBosses = @(
            [ordered]@{ type = 125; key = 1; tick = 121; life = 20000; lifeMax = 20000 },
            [ordered]@{ type = 126; key = 2; tick = 121; life = 23000; lifeMax = 23000 }
        )
        diagnostics = $diagnostics
    }
    $evidence = [pscustomobject]@{
        Result = $result
        Observations = @(
            (New-TestObservation -Tick 100 -ApplyCalls 100),
            (New-TestObservation -Tick 160 -ApplyCalls 160),
            (New-TerminalObservation -Tick 161 -ApplyCalls 160)
        )
        Hurt = @((New-TestHurtRow))
    }
    Sync-TestEvidence $evidence
    return $evidence
}

function Copy-TestEvidence($Evidence) {
    return ($Evidence | ConvertTo-Json -Depth 20 -Compress | ConvertFrom-Json)
}

function Sync-TestEvidence($Evidence) {
    $observationLines = @($Evidence.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    if ($observationLines.Count -eq 0) { throw 'Synthetic evidence must contain observations.' }
    $summary = $Evidence.Result.diagnostics
    $summary.rows = $observationLines.Count
    $summary.periodicRows = @($Evidence.Observations | Where-Object { ([int]$_.reasonMask -band 1) -ne 0 }).Count
    $summary.transitionRows = @($Evidence.Observations | Where-Object { ([int]$_.reasonMask -band 30) -ne 0 }).Count
    $summary.terminalRows = @($Evidence.Observations | Where-Object { ([int]$_.reasonMask -band 32) -ne 0 }).Count
    $summary.planRows = @($Evidence.Observations | Where-Object { $null -ne $_.plan }).Count
    $summary.actualControlRows = @($Evidence.Observations | Where-Object { $null -ne $_.actualAtApplyReturn }).Count
    $last = $Evidence.Observations[$Evidence.Observations.Count - 1]
    $summary.applyCalls = $last.applyCalls
    $summary.applyReturns = $last.applyReturns
    $Evidence.Result.ticks = $last.tick
    $Evidence.Result.nativeFrames = $last.nativeFrames
    $buffer = Measure-TestBuffer $observationLines
    $summary.flushes = $buffer.Flushes
    $summary.maximumBufferedCharacters = $buffer.MaximumBufferedCharacters
    $summary.charactersWritten = $buffer.CharactersWritten
    $summary.bufferedRowsAfterFinalFlush = $buffer.BufferedRowsAfterFinalFlush

    $hurtLines = @($Evidence.Hurt | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    $hurt = $summary.hurt
    $hurt.calls = $hurtLines.Count; $hurt.returns = $hurtLines.Count; $hurt.rows = $hurtLines.Count; $hurt.serializedRows = $hurtLines.Count
    $hurt.file = if ($hurtLines.Count -eq 0) { $null } else { 'hurt-observations.jsonl' }
    $hurt.maximumDepth = if ($hurtLines.Count -eq 0) { 0 } else { 1 }
    $hurtBuffer = Measure-TestBuffer $hurtLines
    $hurt.flushes = $hurtBuffer.Flushes
    $hurt.maximumBufferedCharacters = $hurtBuffer.MaximumBufferedCharacters
    $hurt.charactersWritten = $hurtBuffer.CharactersWritten
    $hurt.bufferedRowsAfterFinalFlush = $hurtBuffer.BufferedRowsAfterFinalFlush
}

function Set-TestObservations($Evidence, $Rows) {
    $Evidence.Observations = @($Rows)
    Sync-TestEvidence $Evidence
}

function Write-NoBom([string]$Path, [string]$Text) {
    [IO.File]::WriteAllText($Path, $Text, [Text.UTF8Encoding]::new($false))
}

function Write-TestEvidence($Evidence, [string]$Directory) {
    if (Test-Path -LiteralPath $Directory) { throw "Synthetic case directory already exists: $Directory" }
    New-Item -ItemType Directory -Path $Directory | Out-Null
    Write-NoBom (Join-Path $Directory 'result.json') ($Evidence.Result | ConvertTo-Json -Depth 20 -Compress)
    $observationLines = @($Evidence.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    Write-NoBom (Join-Path $Directory 'boss-observations.jsonl') ([string]::Join([Environment]::NewLine, $observationLines) + [Environment]::NewLine)
    if (@($Evidence.Hurt).Count -gt 0) {
        $hurtLines = @($Evidence.Hurt | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
        Write-NoBom (Join-Path $Directory 'hurt-observations.jsonl') ([string]::Join([Environment]::NewLine, $hurtLines) + [Environment]::NewLine)
    }
}

function Invoke-TestDirectory([string]$Name, [string]$Directory, [bool]$ShouldAccept, [string]$ExpectedFailure = '') {
    $accepted = $true
    $failure = $null
    $failureLocation = $null
    $output = $null
    try { $output = & $auditScript -RunDirectory $Directory }
    catch { $accepted = $false; $failure = $_.Exception.Message; $failureLocation = $_.InvocationInfo.PositionMessage }
    $detailMatches = [string]::IsNullOrWhiteSpace($ExpectedFailure) -or ($null -ne $failure -and $failure.Contains($ExpectedFailure))
    $validSuccess = -not $ShouldAccept -or ($null -ne $output -and $output.Accepted -eq $true -and
        $output.EvidenceScope.Contains('not full-frame proof'))
    if ($accepted -eq $ShouldAccept -and $detailMatches -and $validSuccess) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        Write-Output "FAIL $Name expectedAccept=$ShouldAccept actualAccept=$accepted detail=$failure location=$failureLocation"
    }
}

function Test-Evidence([string]$Name, $Evidence, [bool]$ShouldAccept, [string]$ExpectedFailure = '') {
    $script:caseNumber++
    $directory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-TestEvidence $Evidence $directory
    Invoke-TestDirectory $Name $directory $ShouldAccept $ExpectedFailure
}

function Get-ObservationCopies($Evidence) {
    return ,@($Evidence.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress | ConvertFrom-Json })
}

if (-not (Test-Path -LiteralPath $auditScript -PathType Leaf)) { throw 'Missing production Twins audit script.' }
if (-not (Test-Path -LiteralPath $artifactsRoot -PathType Container)) { New-Item -ItemType Directory -Path $artifactsRoot | Out-Null }
New-Item -ItemType Directory -Path $testRoot | Out-Null

try {
    Test-Evidence 'accept-complete-synthetic-native-envelope' (New-TestEvidence) $true

    $emptyHurt = New-TestEvidence
    $emptyHurt.Hurt = @()
    Sync-TestEvidence $emptyHurt
    Test-Evidence 'accept-zero-Hurt-call-file-absence' $emptyHurt $true

    $reversal61 = New-TestEvidence
    Set-TestObservations $reversal61 @(
        (New-TestObservation -Tick 100 -Horizontal 1),
        (New-TestObservation -Tick 130 -Horizontal -1),
        (New-TestObservation -Tick 161 -Horizontal 1),
        (New-TerminalObservation -Tick 162 -ApplyCalls 161)
    )
    Test-Evidence 'accept-direction-A-minusA-A-over-61-ticks' $reversal61 $true

    $recovery600 = New-TestEvidence
    Set-TestObservations $recovery600 @(
        (New-TestObservation -Tick 100 -Phase 'classic-spaz-first-hover-runway-recover-runway'),
        (New-TestObservation -Tick 700 -Phase 'classic-spaz-first-hover-runway-supported-run'),
        (New-TerminalObservation -Tick 701 -ApplyCalls 700)
    )
    Test-Evidence 'accept-recover-runway-at-600-tick-boundary' $recovery600 $true

    $routeClosed90 = New-TestEvidence
    Set-TestObservations $routeClosed90 @(
        (New-TestObservation -Tick 100 -Horizontal 0 -Phase 'classic-spaz-first-hover-runway-supported-run-route-closed'),
        (New-TestObservation -Tick 190 -Horizontal 0 -Phase 'classic-spaz-first-hover-runway-supported-run-route-closed-control-return'),
        (New-TerminalObservation -Tick 191 -ApplyCalls 190)
    )
    Test-Evidence 'accept-route-closed-control-return-at-90-tick-boundary' $routeClosed90 $true

    $grace12 = New-TestEvidence
    Set-TestObservations $grace12 @(
        (New-TestObservation -Tick 100 -Horizontal 0 -Phase 'classic-unsupported-twins-native-phase'),
        (New-TestObservation -Tick 112 -Horizontal 0 -Phase 'classic-unsupported-twins-native-phase-control-return'),
        (New-TerminalObservation -Tick 113 -ApplyCalls 112)
    )
    Test-Evidence 'accept-observation-loss-control-return-at-12-tick-boundary' $grace12 $true

    $wingBoundaries = New-TestEvidence
    Set-TestObservations $wingBoundaries @(
        (New-TestObservation -Tick 100 -WingTime 0),
        (New-TestObservation -Tick 280 -WingTime 0),
        (New-TestObservation -Tick 340 -WingTime 1),
        (New-TerminalObservation -Tick 341 -ApplyCalls 340)
    )
    Test-Evidence 'accept-zero-wing-span-180-and-recovery-240-boundaries' $wingBoundaries $true

    $recoveryStop = New-TestEvidence
    $rows = Get-ObservationCopies $recoveryStop
    $rows[0].plan.phase = 'classic-spaz-first-hover-runway-recover-runway'
    $rows[0].plan.horizontal = 0; $rows[0].actualAtApplyReturn.controlLeft = $false; $rows[0].actualAtApplyReturn.controlRight = $false
    Set-TestObservations $recoveryStop $rows
    Test-Evidence 'reject-H0-recovery-without-route-closed' $recoveryStop $false 'stopped outside an explicit closed/grace phase'

    $ordinaryStop = New-TestEvidence
    $rows = Get-ObservationCopies $ordinaryStop
    $rows[0].plan.phase = 'classic-spaz-first-hover-runway-supported-run'
    $rows[0].plan.horizontal = 0; $rows[0].actualAtApplyReturn.controlLeft = $false; $rows[0].actualAtApplyReturn.controlRight = $false
    Set-TestObservations $ordinaryStop $rows
    Test-Evidence 'reject-H0-ordinary-engaged-proxy' $ordinaryStop $false 'stopped outside an explicit closed/grace phase'

    $launchStop = New-TestEvidence
    $rows = Get-ObservationCopies $launchStop
    $rows[0].plan.phase = 'classic-spaz-first-launch-committed-exit-committed-escape'
    $rows[0].plan.horizontal = 0; $rows[0].actualAtApplyReturn.controlLeft = $false; $rows[0].actualAtApplyReturn.controlRight = $false
    Set-TestObservations $launchStop $rows
    Test-Evidence 'reject-H0-launch-charge-brake-proxy' $launchStop $false 'stopped outside an explicit closed/grace phase'

    $flameStop = New-TestEvidence
    $rows = Get-ObservationCopies $flameStop
    $rows[0].plan.phase = 'classic-spaz-second-flame-spacing-runway-supported-run'
    $rows[0].plan.horizontal = 0; $rows[0].actualAtApplyReturn.controlLeft = $false; $rows[0].actualAtApplyReturn.controlRight = $false
    Set-TestObservations $flameStop $rows
    Test-Evidence 'reject-H0-Spaz-second-flame-proxy' $flameStop $false 'stopped outside an explicit closed/grace phase'

    $directionMismatch = New-TestEvidence
    $rows = Get-ObservationCopies $directionMismatch
    $rows[0].actualAtApplyReturn.controlLeft = $true; $rows[0].actualAtApplyReturn.controlRight = $false
    Set-TestObservations $directionMismatch $rows
    Test-Evidence 'reject-plan-returned-direction-mismatch' $directionMismatch $false 'returned horizontal controls disagree'

    $reversal60 = New-TestEvidence
    Set-TestObservations $reversal60 @(
        (New-TestObservation -Tick 100 -Horizontal 1),
        (New-TestObservation -Tick 130 -Horizontal -1),
        (New-TestObservation -Tick 160 -Horizontal 1),
        (New-TerminalObservation -Tick 161 -ApplyCalls 160)
    )
    Test-Evidence 'reject-direction-A-minusA-A-at-60-tick-boundary' $reversal60 $false 'spans only 60 ticks'

    $recovery601 = New-TestEvidence
    Set-TestObservations $recovery601 @(
        (New-TestObservation -Tick 100 -Phase 'classic-ret-first-hover-runway-recover-runway'),
        (New-TestObservation -Tick 701 -Phase 'classic-ret-first-hover-runway-supported-run'),
        (New-TerminalObservation -Tick 702 -ApplyCalls 701)
    )
    Test-Evidence 'reject-recover-runway-at-601-ticks' $recovery601 $false 'recover-runway took 601 ticks'

    $recoveryUnresolved = New-TestEvidence
    Set-TestObservations $recoveryUnresolved @(
        (New-TestObservation -Tick 100 -Phase 'classic-ret-first-hover-runway-recover-runway'),
        (New-TestObservation -Tick 160 -Phase 'classic-ret-first-hover-runway-recover-runway'),
        (New-TerminalObservation -Tick 161 -ApplyCalls 160)
    )
    Test-Evidence 'reject-stream-ending-in-recover-runway' $recoveryUnresolved $false 'ended while still in recover-runway'

    $routeClosed91 = New-TestEvidence
    Set-TestObservations $routeClosed91 @(
        (New-TestObservation -Tick 100 -Horizontal 0 -Phase 'classic-spaz-first-hover-runway-supported-run-route-closed'),
        (New-TestObservation -Tick 191 -Horizontal 0 -Phase 'classic-spaz-first-hover-runway-supported-run-route-closed'),
        (New-TerminalObservation -Tick 192 -ApplyCalls 191)
    )
    Test-Evidence 'reject-route-closed-at-91-ticks' $routeClosed91 $false 'route-closed episode exceeds 90 ticks'

    $grace13 = New-TestEvidence
    Set-TestObservations $grace13 @(
        (New-TestObservation -Tick 100 -Horizontal 0 -Phase 'classic-unsupported-twins-native-phase'),
        (New-TestObservation -Tick 113 -Horizontal 0 -Phase 'classic-unsupported-twins-native-phase'),
        (New-TerminalObservation -Tick 114 -ApplyCalls 113)
    )
    Test-Evidence 'reject-observation-loss-grace-at-13-ticks' $grace13 $false 'observation-loss grace exceeds 12 ticks'

    $continuedReturn = New-TestEvidence
    Set-TestObservations $continuedReturn @(
        (New-TestObservation -Tick 100 -Horizontal 0 -Phase 'classic-spaz-first-hover-runway-supported-run-route-closed-control-return'),
        (New-TestObservation -Tick 104 -Horizontal 1 -Phase 'classic-spaz-first-hover-runway-supported-run'),
        (New-TerminalObservation -Tick 105 -ApplyCalls 104)
    )
    Test-Evidence 'reject-engaged-continuation-after-control-return' $continuedReturn $false 'continued more than 3 ticks'

    $movingReturn = New-TestEvidence
    $rows = Get-ObservationCopies $movingReturn
    $rows[0].plan.phase = 'classic-spaz-first-hover-runway-supported-run-route-closed-control-return'
    Set-TestObservations $movingReturn $rows
    Test-Evidence 'reject-moving-control-return' $movingReturn $false 'moved during an explicit closed/control-return/grace phase'

    $zero181 = New-TestEvidence
    Set-TestObservations $zero181 @(
        (New-TestObservation -Tick 100 -WingTime 0),
        (New-TestObservation -Tick 281 -WingTime 0),
        (New-TestObservation -Tick 340 -WingTime 1),
        (New-TerminalObservation -Tick 341 -ApplyCalls 340)
    )
    Test-Evidence 'reject-zero-wing-proxy-span-181' $zero181 $false 'zero-wing proxy span is 181 ticks'

    $recover241 = New-TestEvidence
    Set-TestObservations $recover241 @(
        (New-TestObservation -Tick 100 -WingTime 0),
        (New-TestObservation -Tick 280 -WingTime 0),
        (New-TestObservation -Tick 341 -WingTime 1),
        (New-TerminalObservation -Tick 342 -ApplyCalls 341)
    )
    Test-Evidence 'reject-wing-recovery-at-241' $recover241 $false 'observed 241'

    $zeroUnresolved = New-TestEvidence
    Set-TestObservations $zeroUnresolved @(
        (New-TestObservation -Tick 100 -WingTime 0),
        (New-TestObservation -Tick 160 -WingTime 0),
        (New-TerminalObservation -Tick 161 -ApplyCalls 160)
    )
    Test-Evidence 'reject-stream-ending-with-zero-wing' $zeroUnresolved $false 'without observing wingTime recovery'

    $dropped = New-TestEvidence; $dropped.Result.diagnostics.droppedRows = 1
    Test-Evidence 'reject-dropped-observation-row' $dropped $false 'droppedRows must be zero'

    $omitted = New-TestEvidence
    $rows = Get-ObservationCopies $omitted; $rows[0].omittedNpcs = 1
    Set-TestObservations $omitted $rows
    Test-Evidence 'reject-per-row-omitted-NPC' $omitted $false 'omitted NPC snapshots'

    $omittedSummary = New-TestEvidence; $omittedSummary.Result.diagnostics.maximumOmittedNpcs = 1
    Test-Evidence 'reject-summary-omitted-NPC' $omittedSummary $false 'maximumOmittedNpcs must be zero'

    $unpairedApply = New-TestEvidence; $unpairedApply.Result.diagnostics.unpairedApplyObservations = 1
    Test-Evidence 'reject-unpaired-ApplyPlan-summary' $unpairedApply $false 'unpairedApplyObservations must be zero'

    $pendingApply = New-TestEvidence
    $rows = Get-ObservationCopies $pendingApply; $rows[0].applyPending = $true
    Set-TestObservations $pendingApply $rows
    Test-Evidence 'reject-pending-ApplyPlan-row' $pendingApply $false 'unpaired ApplyPlan call'

    $unpairedHurt = New-TestEvidence; $unpairedHurt.Result.diagnostics.hurt.returns = 0
    Test-Evidence 'reject-unpaired-Hurt-return' $unpairedHurt $false 'not one-to-one'

    $droppedHurt = New-TestEvidence; $droppedHurt.Result.diagnostics.hurt.droppedRows = 1
    Test-Evidence 'reject-dropped-Hurt-row' $droppedHurt $false 'hurt.droppedRows must be zero'

    foreach ($resultCase in @(
        @{ Name = 'status'; Value = 'loss'; Failure = 'status must equal' },
        @{ Name = 'outcome'; Value = 'SuccessAfterDeath'; Failure = 'outcome must equal' },
        @{ Name = 'win'; Value = $false; Failure = 'win must be true' },
        @{ Name = 'validBattle'; Value = $false; Failure = 'validBattle must be true' },
        @{ Name = 'death'; Value = $true; Failure = 'records a player death' },
        @{ Name = 'deaths'; Value = 1; Failure = 'one or more deaths' },
        @{ Name = 'minLife'; Value = 39; Failure = 'below the 40-life safety floor' },
        @{ Name = 'bossDamage'; Value = 42999; Failure = 'must be exactly 43000' }
    )) {
        $evidence = New-TestEvidence
        $evidence.Result.($resultCase.Name) = $resultCase.Value
        Test-Evidence "reject-result-$($resultCase.Name)" $evidence $false $resultCase.Failure
    }

    $wrongRoots = New-TestEvidence; $wrongRoots.Result.firstObservedBosses[1].lifeMax = 22999
    Test-Evidence 'reject-wrong-Twins-root-lifeMax' $wrongRoots $false 'uniquely identify type 125'

    $missingField = New-TestEvidence; $missingField.Result.Remove('bossDamage')
    Test-Evidence 'reject-missing-required-result-field' $missingField $false "missing field 'bossDamage'"

    $wrongType = New-TestEvidence; $wrongType.Result.minLife = '40'
    Test-Evidence 'reject-wrong-JSON-number-type' $wrongType $false 'must be an integer'

    $invalidResult = New-TestEvidence
    $script:caseNumber++
    $invalidResultDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-TestEvidence $invalidResult $invalidResultDirectory
    Write-NoBom (Join-Path $invalidResultDirectory 'result.json') '{'
    Invoke-TestDirectory 'reject-invalid-result-JSON' $invalidResultDirectory $false 'result.json is invalid JSON'

    $missingObservation = New-TestEvidence
    $script:caseNumber++
    $missingObservationDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-TestEvidence $missingObservation $missingObservationDirectory
    Remove-Item -LiteralPath (Join-Path $missingObservationDirectory 'boss-observations.jsonl') -Force
    Invoke-TestDirectory 'reject-missing-observation-file' $missingObservationDirectory $false "required file 'boss-observations.jsonl' is missing"

    $missingHurt = New-TestEvidence
    $script:caseNumber++
    $missingHurtDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-TestEvidence $missingHurt $missingHurtDirectory
    Remove-Item -LiteralPath (Join-Path $missingHurtDirectory 'hurt-observations.jsonl') -Force
    Invoke-TestDirectory 'reject-declared-Hurt-file-missing' $missingHurtDirectory $false 'must name an existing hurt-observations.jsonl'

    $invalidObservation = New-TestEvidence
    $script:caseNumber++
    $invalidObservationDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-TestEvidence $invalidObservation $invalidObservationDirectory
    $validTail = @($invalidObservation.Observations | Select-Object -Skip 1 | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    Write-NoBom (Join-Path $invalidObservationDirectory 'boss-observations.jsonl') ('{' + [Environment]::NewLine + [string]::Join([Environment]::NewLine, $validTail) + [Environment]::NewLine)
    Invoke-TestDirectory 'reject-invalid-observation-JSONL' $invalidObservationDirectory $false 'invalid JSON'

    $wrongSchema = New-TestEvidence
    $rows = Get-ObservationCopies $wrongSchema; $rows[0].schema = 'chaite-boss-observation/v0'
    Set-TestObservations $wrongSchema $rows
    Test-Evidence 'reject-wrong-observation-schema' $wrongSchema $false 'wrong schema'

    $duplicateTick = New-TestEvidence
    Set-TestObservations $duplicateTick @(
        (New-TestObservation -Tick 100), (New-TestObservation -Tick 100), (New-TerminalObservation -Tick 101 -ApplyCalls 100)
    )
    Test-Evidence 'reject-duplicate-observation-tick' $duplicateTick $false 'strictly increasing'

    $backwardsTick = New-TestEvidence
    Set-TestObservations $backwardsTick @(
        (New-TestObservation -Tick 100), (New-TestObservation -Tick 99), (New-TerminalObservation -Tick 101 -ApplyCalls 99)
    )
    Test-Evidence 'reject-backwards-observation-tick' $backwardsTick $false 'strictly increasing'

    $emptyLine = New-TestEvidence
    $script:caseNumber++
    $emptyLineDirectory = Join-Path $testRoot ('case-{0:D3}' -f $script:caseNumber)
    Write-TestEvidence $emptyLine $emptyLineDirectory
    $lines = @($emptyLine.Observations | ForEach-Object { $_ | ConvertTo-Json -Depth 12 -Compress })
    Write-NoBom (Join-Path $emptyLineDirectory 'boss-observations.jsonl') ($lines[0] + [Environment]::NewLine + [Environment]::NewLine + $lines[1] + [Environment]::NewLine + $lines[2] + [Environment]::NewLine)
    Invoke-TestDirectory 'reject-empty-observation-JSONL-row' $emptyLineDirectory $false 'empty JSONL row'

    Invoke-TestDirectory 'reject-run-directory-outside-artifacts' $projectRoot $false 'strictly inside project artifacts'

    $junctionTarget = Join-Path $testRoot 'junction-target'
    Write-TestEvidence (New-TestEvidence) $junctionTarget
    $null = New-Item -ItemType Junction -Path $junctionPath -Target $junctionTarget
    Invoke-TestDirectory 'reject-reparse-point-run-directory' $junctionPath $false 'reparse point'

    $historicalRun = Join-Path $artifactsRoot 'game-probe-batch-20260910-225306-3b475050-case002'
    if (Test-Path -LiteralPath (Join-Path $historicalRun 'result.json') -PathType Leaf) {
        Invoke-TestDirectory 'reject-actual-historical-Twins-loss' $historicalRun $false "status must equal 'win'"
    } else {
        $historicalEquivalent = New-TestEvidence
        $historicalEquivalent.Result.status = 'loss'; $historicalEquivalent.Result.outcome = 'FailedAfterDeath'
        $historicalEquivalent.Result.win = $false; $historicalEquivalent.Result.death = $true
        $historicalEquivalent.Result.deaths = 1; $historicalEquivalent.Result.minLife = 0
        $historicalEquivalent.Result.bossDamage = 27902
        Test-Evidence 'reject-historical-Twins-loss-signature' $historicalEquivalent $false "status must equal 'win'"
    }
} finally {
    # Remove the junction itself before the bounded recursive cleanup so no
    # cleanup operation can traverse a reparse point.
    $junction = Get-Item -LiteralPath $junctionPath -Force -ErrorAction SilentlyContinue
    if ($null -ne $junction -and ($junction.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        # Directory.Delete removes the junction entry itself and does not walk
        # its target; PowerShell 5.1 Remove-Item has a junction null-reference
        # bug on some current Windows builds.
        [IO.Directory]::Delete($junctionPath)
    }
    $fullTestRoot = [IO.Path]::GetFullPath($testRoot)
    $artifactPrefix = $artifactsRoot.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $testRootEntry = Get-Item -LiteralPath $fullTestRoot -Force -ErrorAction SilentlyContinue
    $resolvedTestRoot = if ($null -eq $testRootEntry) { $null } else { [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $fullTestRoot).Path) }
    if ($null -ne $testRootEntry -and ($testRootEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -eq 0 -and
        $resolvedTestRoot -eq $fullTestRoot -and $fullTestRoot.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($fullTestRoot).StartsWith('twins-native-audit-test-', [StringComparison]::Ordinal)) {
        Remove-Item -LiteralPath $fullTestRoot -Recurse -Force -ErrorAction SilentlyContinue
    } elseif ($null -ne $testRootEntry) {
        throw 'Refusing unsafe synthetic-audit cleanup target.'
    }
}

Write-Output "Offline Twins native-audit regression: $script:passed passed, $script:failed failed. No game process or user save was touched."
if ($script:failed -gt 0) { exit 1 }
exit 0

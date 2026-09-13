# Pure in-memory envelope regressions. Synthetic rows deliberately do not claim
# physically valid trajectories; only the separate native x86 oracle does that.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
foreach ($source in @('test-native-motion.ps1', 'test-native-flight.ps1')) {
    $tokens = $null
    $errors = $null
    $ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot $source), [ref]$tokens, [ref]$errors)
    if ($errors.Count -ne 0) { throw "Parse errors in $source" }
    $names = if ($source -eq 'test-native-motion.ps1') { @('Motion-Field', 'Motion-Integer', 'Motion-Boolean', 'Motion-Number') } else { @('Flight-JumpRequested', 'Flight-UpRequested', 'Flight-DownRequested', 'Flight-Positive', 'Assert-FlightEvidence') }
    foreach ($name in $names) {
        $definitions = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name }, $true))
        if ($definitions.Count -ne 1) { throw "Ambiguous evidence helper $name" }
        . ([scriptblock]::Create($definitions[0].Extent.Text))
    }
}
$script:passed = 0
$flightCases = @(
    'demon-exhaust-release', 'demon-lightning-exhaust-release',
    'demon-early-repress', 'demon-lightning-early-repress',
    'demon-held-landing', 'demon-lightning-held-landing',
    'demon-cloud-repress', 'demon-lightning-cloud-repress',
    'demon-feather-neutral', 'demon-feather-up', 'demon-feather-down'
)
function New-FlightEvidence([string]$Case = 'demon-exhaust-release') {
    $lightning = $Case.Contains('-lightning-')
    $cloud = $Case.Contains('-cloud-')
    $feather = $Case.Contains('-feather-')
    $profile = 'demon' + $(if ($lightning) { '-lightning' } else { '' }) + $(if ($cloud) { '-cloud' } else { '' }) + $(if ($feather) { '-featherfall' } else { '' })
    $wingTotal = if ($lightning) { 142 } else { 100 }
    [decimal]$lcg = 20260910
    for ($index = 0; $index -lt 600; $index++) { $lcg = ($lcg * [decimal]25214903917 + [decimal]11) % [decimal]281474976710656 }
    $result = [pscustomobject]@{
        schema = 'chaite-native-flight-result/v1'; scenario = 'motion-flight'; flightCase = $Case; flightProfile = $profile
        status = 'complete'; difficulty = 'classic'; frameFile = 'flight-frames.jsonl'; seed = 20260910
        difficultyCode = 0; processExitCode = 0; ticks = 600; nativeFrames = 600; recordedFrames = 600
        totalFrames = 600; warmupFrames = 20; validFlight = $true; nativeDifficultyVerified = $true
        nativeDifficulty = [pscustomobject]@{ gameMode = 0; worldFileGameMode = 0; difficulty = 1; worldFileSeed = 20260910; expertMode = $false; masterMode = $false; hardMode = $false; forTheWorthy = $false }
        nativeRandom = [pscustomobject]@{ seed = 20260910; unpausedUpdateSeedInitial = 20260910; unpausedUpdateSeedAdvances = 600; referenceChecks = 1200; installedAfterSetup = $true; actualAndNativeNamedColdStateVerified = $true; actualStreamConsumedForFingerprint = $false; independentTwinFingerprint = @(1, 2, 3, 4, 5, 6, 7, 8); unpausedUpdateSeedFinal = [long]$lcg }
        arena = [pscustomobject]@{ nativeSceneMetricRefreshes = 600; groundTop = 500; groundTile = 'GrayBrick'; platformRows = @() }
        equipment = [pscustomobject]@{ flightProfile = $profile; wingItemType = 492; bootsItemType = $(if ($lightning) { 898 } else { 0 }); cloudItemType = $(if ($cloud) { 53 } else { 0 }); wingPrefix = 0; bootsPrefix = 0; cloudPrefix = 0; lightningEquipped = $lightning; cloudEquipped = $cloud; featherfallActive = $feather; featherfallBuffType = $(if ($feather) { 8 } else { 0 }); featherfallSourceItemType = $(if ($feather) { 295 } else { 0 }); featherfallSetupViaNativeAddBuff = $feather; noWeaponsAmmoConsumablesOrMount = $true; noDirectJumpStateOverrides = $true; noDirectFlightStateOverrides = $true }
        coverage = [pscustomobject]@{ sawAirborne = $true; returnedGround = $true; cloudConsumed = $cloud; wingMovementCalls = $wingTotal; maximumWingTime = $wingTotal; rocketConversions = $(if ($lightning) { 1 } else { 0 }); firstRocketConversionTick = $(if ($lightning) { 21 } else { -1 }); firstExhaustionTick = 180; emptyWingHeldDescentFrames = 10; releasedGroundFullWingFrames = 20; heldGroundEmptyWingFrames = 10; releasedAirborneFrames = 10; poweredAfterRepressFrames = 10; featherPoweredFrames = $(if ($feather) { 10 } else { 0 }); featherNeutralFrames = $(if ($feather) { 10 } else { 0 }); featherUpFrames = $(if ($Case -ceq 'demon-feather-up') { 10 } else { 0 }); featherDownBypassFrames = $(if ($Case -ceq 'demon-feather-down') { 10 } else { 0 }) }
    }
    $wingsRemaining = $wingTotal
    $frames = @(for ($tick = 1; $tick -le 600; $tick++) {
        $jump = Flight-JumpRequested $Case $tick
        $up = Flight-UpRequested $Case $tick
        $down = Flight-DownRequested $Case $tick
        $called = $tick -ge 36 -and $jump -and $wingsRemaining -gt 0
        if ($called) { $wingsRemaining-- }
        $snapshot = [pscustomobject]@{
            tick = $tick; gameUpdateCount = $tick + 1; nativeFramesCompleted = $tick - 1; jump = 0; jumpHeight = 15
            jumpSpeed = 5.01; jumpSpeedBoost = 0; gravity = 0.4; maxFallSpeed = 10; gravDir = 1; bottomY = 8000
            releaseJump = $true; canJumpAgain_Cloud = $cloud; hasJumpOption_Cloud = $cloud; isPerformingJump_Cloud = $false
            wings = 1; wingsLogic = 1; wingTime = $wingsRemaining; wingTimeMax = 100
            rocketBoots = $(if ($lightning) { 2 } else { 0 }); rocketTime = 0; rocketTimeMax = 7; rocketDelay = 0; rocketDelay2 = 0
            canRocket = $false; rocketRelease = $true; autoJump = $false; justJumped = $false; noFallDmg = $true
            wingAccRunSpeed = 6.25; wingRunAccelerationMult = 1
            sliding = $false; slowFall = $feather; wet = $false; honeyWet = $false; lavaWet = $false; shimmerWet = $false
            pulley = $false; frozen = $false; webbed = $false; stoned = $false
            position = [pscustomobject]@{ x = 33600; y = 7958 }; velocity = [pscustomobject]@{ x = 0; y = 0 }; life = 400; dead = $false
            controls = [pscustomobject]@{ jump = $jump; left = $false; right = $false; up = $up; down = $down; useItem = $false; useTile = $false; hook = $false; mount = $false; dash = $false }
        }
        [pscustomobject]@{
            schema = 'chaite-native-flight-frame/v1'; productionSessionState = 'Idle'; tick = $tick
            nativeFrameBefore = $tick - 1; nativeFrameAfter = $tick; playerUpdateCalls = 1; jumpMovementCalls = 1
            requestedJump = $jump; requestedUp = $up; requestedDown = $down; wingMovementCalls = $(if ($called) { 1 } else { 0 })
            prePlayer = $snapshot; preJump = $snapshot; postJump = $snapshot; postPlayer = $snapshot
            preWing = $(if ($called) { $snapshot } else { $null }); postWing = $(if ($called) { $snapshot } else { $null })
        }
    })
    $evidence = [pscustomobject]@{ Case = $Case; Result = $result; Frames = $frames } | ConvertTo-Json -Depth 12 -Compress | ConvertFrom-Json
    if ($feather) { $evidence.Frames[0].prePlayer.slowFall = $false }
    if ($cloud) { $evidence.Frames[26].postJump.canJumpAgain_Cloud = $false }
    return $evidence
}
function Check-FlightGate([string]$Name, $Evidence, [bool]$Accept) {
    $accepted = $true
    $message = $null
    try { Assert-FlightEvidence $Evidence.Result $Evidence.Frames $Evidence.Case 20260910 }
    catch { $accepted = $false; $message = $_.Exception.Message }
    if ($accepted -ne $Accept) { throw "Flight gate regression: $Name expected=$Accept actual=$accepted detail=$message" }
    $script:passed++
    Write-Output "PASS $Name"
}
foreach ($case in $flightCases) { Check-FlightGate "valid-envelope-$case" (New-FlightEvidence $case) $true }
foreach ($wrong in @($false, 'true', 1, $null)) { $e = New-FlightEvidence; $e.Result.validFlight = $wrong; Check-FlightGate "reject-validFlight-$wrong" $e $false }
foreach ($field in @('ticks', 'nativeFrames', 'recordedFrames', 'totalFrames')) {
    foreach ($wrong in @(599, '600', 600.5, $null)) { $e = New-FlightEvidence; $e.Result.$field = $wrong; Check-FlightGate "reject-$field-$wrong" $e $false }
}
foreach ($field in @('firstExhaustionTick', 'emptyWingHeldDescentFrames', 'releasedGroundFullWingFrames')) {
    foreach ($wrong in @(0, '1', $null)) { $e = New-FlightEvidence; $e.Result.coverage.$field = $wrong; Check-FlightGate "reject-coverage-$field-$wrong" $e $false }
}
foreach ($moment in @('prePlayer', 'preJump', 'postJump', 'postPlayer')) {
    $e = New-FlightEvidence; $e.Frames[0].PSObject.Properties.Remove($moment); Check-FlightGate "reject-missing-$moment" $e $false
    $e = New-FlightEvidence; $e.Frames[0].$moment.rocketRelease = 'true'; Check-FlightGate "reject-string-rocket-release-$moment" $e $false
}
$e = New-FlightEvidence; $e.Frames[35].preWing = $null; Check-FlightGate 'reject-missing-native-wing-entry' $e $false
$e = New-FlightEvidence; $e.Frames[35].postWing = $null; Check-FlightGate 'reject-missing-native-wing-return' $e $false
$e = New-FlightEvidence; $e.Frames[0].preWing = $e.Frames[0].preJump; Check-FlightGate 'reject-fabricated-wing-observation' $e $false
$e = New-FlightEvidence; $e.Frames[35].wingMovementCalls = 2; Check-FlightGate 'reject-double-wing-call' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.gameUpdateCount = 1; Check-FlightGate 'reject-omitted-native-inner-counter-advance' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.wingsLogic = 2; Check-FlightGate 'reject-Angel-as-Demon' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.slowFall = $true; Check-FlightGate 'reject-undeclared-featherfall' $e $false
$e = New-FlightEvidence 'demon-feather-neutral'; $e.Frames[0].preJump.slowFall = $false; Check-FlightGate 'reject-lost-declared-featherfall' $e $false
$e = New-FlightEvidence 'demon-feather-up'; $e.Frames[229].requestedUp = $false; Check-FlightGate 'reject-lost-featherfall-Up-input' $e $false
$e = New-FlightEvidence 'demon-feather-down'; $e.Frames[229].preJump.controls.down = $false; Check-FlightGate 'reject-lost-featherfall-Down-control' $e $false
$e = New-FlightEvidence 'demon-feather-up'; $e.Result.coverage.featherUpFrames = 0; Check-FlightGate 'reject-missing-featherfall-Up-coverage' $e $false
$e = New-FlightEvidence 'demon-feather-down'; $e.Result.coverage.featherDownBypassFrames = 0; Check-FlightGate 'reject-missing-featherfall-Down-coverage' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.wet = $true; Check-FlightGate 'reject-wet-profile' $e $false
$e = New-FlightEvidence; $e.Result.nativeRandom.unpausedUpdateSeedFinal++; Check-FlightGate 'reject-wrong-independent-LCG' $e $false
$e = New-FlightEvidence; $e.Frames = @($e.Frames | Select-Object -Skip 20); Check-FlightGate 'reject-omitted-warmup' $e $false
$e = New-FlightEvidence 'demon-lightning-exhaust-release'; $e.Result.coverage.maximumWingTime = 100; Check-FlightGate 'reject-lost-rocket-conversion-resource' $e $false
$e = New-FlightEvidence 'demon-held-landing'; $e.Result.coverage.heldGroundEmptyWingFrames = 0; Check-FlightGate 'reject-missing-held-landing-boundary' $e $false
$e = New-FlightEvidence 'demon-cloud-repress'; $e.Frames[26].postJump.canJumpAgain_Cloud = $true; Check-FlightGate 'reject-missing-cloud-consumption' $e $false
# Inline validation must retain the exact JSON type/presence contract. In
# particular, the value 0 cannot substitute for a missing false/zero field.
foreach ($field in @('schema', 'productionSessionState', 'tick', 'nativeFrameBefore', 'requestedJump', 'requestedUp', 'requestedDown', 'wingMovementCalls', 'preWing', 'postWing')) {
    $e = New-FlightEvidence; $e.Frames[0].PSObject.Properties.Remove($field); Check-FlightGate "reject-missing-frame-$field" $e $false
}
foreach ($field in @('tick', 'gameUpdateCount', 'nativeFramesCompleted', 'life', 'jump', 'gravity', 'rocketRelease', 'wet', 'position', 'controls')) {
    $e = New-FlightEvidence; $e.Frames[0].preJump.PSObject.Properties.Remove($field); Check-FlightGate "reject-missing-snapshot-$field" $e $false
}
foreach ($wrong in @($null, $false, '0', 0.5, [double]0, [decimal]0, -1, 1001)) {
    $e = New-FlightEvidence; $e.Frames[0].preJump.jump = $wrong; Check-FlightGate "reject-integer-type-or-range-$wrong" $e $false
}
foreach ($wrong in @($null, $false, '0.4', [double]::NaN, [double]::PositiveInfinity, [double]::NegativeInfinity)) {
    $e = New-FlightEvidence; $e.Frames[0].preJump.gravity = $wrong; Check-FlightGate "reject-number-type-or-finite-$wrong" $e $false
}
foreach ($wrong in @($null, 0, 'false')) {
    $e = New-FlightEvidence; $e.Frames[0].preJump.wet = $wrong; Check-FlightGate "reject-false-state-type-$wrong" $e $false
    $e = New-FlightEvidence; $e.Frames[0].preJump.controls.left = $wrong; Check-FlightGate "reject-false-control-type-$wrong" $e $false
}
$e = New-FlightEvidence; $e.Frames[0].tick = [double]1; Check-FlightGate 'reject-double-frame-counter' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.nativeFramesCompleted = $false; Check-FlightGate 'reject-Boolean-snapshot-counter' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.velocity.PSObject.Properties.Remove('x'); Check-FlightGate 'reject-missing-axis' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.position.y = '7958'; Check-FlightGate 'reject-string-axis' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.controls.PSObject.Properties.Remove('jump'); Check-FlightGate 'reject-missing-jump-control' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.controls.PSObject.Properties.Remove('left'); Check-FlightGate 'reject-missing-false-control' $e $false
$e = New-FlightEvidence; $e.Frames[35].postWing.gravity = '0.4'; Check-FlightGate 'reject-malformed-optional-wing-snapshot' $e $false
$e = New-FlightEvidence; $e.Frames[599].postPlayer.controls.hook = 0; Check-FlightGate 'reject-final-frame-control-type' $e $false
$e = New-FlightEvidence; $e.Frames[0].preJump.jump = [long]0; $e.Frames[0].tick = [long]1; $e.Frames[0].preJump.gravity = [decimal]0.4; Check-FlightGate 'accept-long-integer-decimal-finite' $e $true
$e = New-FlightEvidence 'demon-lightning-held-landing'; $e.Frames[560].requestedJump = $false; Check-FlightGate 'reject-obsolete-held-release-at-561' $e $false
$e = New-FlightEvidence 'demon-lightning-held-landing'; $e.Frames[580].requestedJump = $true; Check-FlightGate 'reject-held-without-final-release-at-581' $e $false
Write-Output "PASS $script:passed native flight envelope regressions. No files, games, builds or physics claims."
exit 0

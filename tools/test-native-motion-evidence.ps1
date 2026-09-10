# Pure in-memory tests of the ACTUAL motion runner evidence functions.
# No runner execution, files, compilation, game assembly loading or processes.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'test-native-motion.ps1'), [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'Motion runner has parse errors.' }
foreach ($name in @('Motion-Field', 'Motion-Integer', 'Motion-Boolean', 'Motion-Number', 'Assert-MotionEvidence')) {
    $found = @($ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name }, $true))
    if ($found.Count -ne 1) { throw "Ambiguous motion gate function: $name" }
    . ([scriptblock]::Create($found[0].Extent.Text))
}
$script:passed = 0
function New-MotionEvidence([string]$Case = 'no-cloud-hold') {
    $cloud = $Case.StartsWith('cloud-', [StringComparison]::Ordinal)
    [decimal]$lcg = 20260910
    for ($index = 0; $index -lt 180; $index++) { $lcg = ($lcg * [decimal]25214903917 + [decimal]11) % [decimal]281474976710656 }
    $result = [pscustomobject]@{
        schema = 'chaite-native-motion-result/v1'; scenario = 'motion-jump'; motionCase = $Case
        status = 'complete'; difficulty = 'classic'; frameFile = 'motion-frames.jsonl'; seed = 20260910
        difficultyCode = 0; processExitCode = 0; ticks = 180; nativeFrames = 180; recordedFrames = 180
        totalFrames = 180; warmupFrames = 20; validMotion = $true; nativeDifficultyVerified = $true
        sawAirborne = $true; returnedGroundAfterRelease = $true; cloudConsumed = $Case -ceq 'cloud-release-press'
        nativeDifficulty = [pscustomobject]@{ gameMode = 0; worldFileGameMode = 0; difficulty = 1; worldFileSeed = 20260910; expertMode = $false; masterMode = $false; hardMode = $false; forTheWorthy = $false }
        nativeRandom = [pscustomobject]@{ seed = 20260910; unpausedUpdateSeedInitial = 20260910; unpausedUpdateSeedAdvances = 180; referenceChecks = 360; installedAfterSetup = $true; actualAndNativeNamedColdStateVerified = $true; actualStreamConsumedForFingerprint = $false; independentTwinFingerprint = @(1, 2, 3, 4, 5, 6, 7, 8); unpausedUpdateSeedFinal = [long]$lcg }
        arena = [pscustomobject]@{ nativeSceneMetricRefreshes = 180; groundTop = 500; groundTile = 'GrayBrick'; platformRows = @() }
        equipment = [pscustomobject]@{ cloudEquipped = $cloud; noWeaponsAmmoConsumablesOrMount = $true; noDirectJumpStateOverrides = $true; cloudItemType = $(if ($cloud) { 53 } else { 0 }); cloudPrefix = 0 }
    }
    $frames = @(for ($tick = 1; $tick -le 180; $tick++) {
        $jump = $tick -ge 21 -and $tick -le 80
        if ($Case.EndsWith('-tap', [StringComparison]::Ordinal)) { $jump = $tick -eq 21 }
        elseif ($Case.EndsWith('-release-press', [StringComparison]::Ordinal) -and $tick -eq 26) { $jump = $false }
        $snapshot = [pscustomobject]@{
            tick = $tick; gameUpdateCount = $tick + 1; nativeFramesCompleted = $tick - 1; jump = 0; jumpHeight = 15
            jumpSpeed = 5.01; jumpSpeedBoost = 0; gravity = 0.4; maxFallSpeed = 10; gravDir = 1
            bottomY = $(if ($tick -eq 27 -and $Case.EndsWith('-release-press', [StringComparison]::Ordinal)) { 7900 } else { 8000 })
            releaseJump = $true; canJumpAgain_Cloud = $cloud; hasJumpOption_Cloud = $cloud; isPerformingJump_Cloud = $false
            position = [pscustomobject]@{ x = 33600; y = 7958 }; velocity = [pscustomobject]@{ x = 0; y = 0 }
            life = 400; dead = $false
            controls = [pscustomobject]@{ jump = $jump; left = $false; right = $false; up = $false; down = $false; useItem = $false; useTile = $false; hook = $false; mount = $false; dash = $false }
        }
        [pscustomobject]@{
            schema = 'chaite-native-motion-frame/v1'; productionSessionState = 'Idle'; tick = $tick
            nativeFrameBefore = $tick - 1; nativeFrameAfter = $tick; playerUpdateCalls = 1; jumpMovementCalls = 1
            requestedJump = $jump; prePlayer = $snapshot; preJump = $snapshot; postJump = $snapshot; postPlayer = $snapshot
        }
    })
    # Synthetic samples test envelope/type gates only, never the physics oracle.
    [pscustomobject]@{ Case = $Case; Result = $result; Frames = $frames } | ConvertTo-Json -Depth 12 -Compress | ConvertFrom-Json
}
function Check-MotionGate([string]$Name, $Evidence, [bool]$Accept) {
    $accepted = $true
    $message = $null
    try { Assert-MotionEvidence $Evidence.Result $Evidence.Frames $Evidence.Case 20260910 }
    catch { $accepted = $false; $message = $_.Exception.Message }
    if ($accepted -ne $Accept) { throw "Motion gate regression: $Name expected=$Accept actual=$accepted detail=$message" }
    $script:passed++
    Write-Output "PASS $Name"
}
foreach ($case in @('no-cloud-hold', 'no-cloud-tap', 'no-cloud-release-press', 'cloud-hold', 'cloud-tap', 'cloud-release-press')) { Check-MotionGate "valid-envelope-$case" (New-MotionEvidence $case) $true }
foreach ($field in @('validMotion', 'nativeDifficultyVerified', 'sawAirborne', 'returnedGroundAfterRelease')) {
    foreach ($wrong in @($false, 'true', 1, $null)) {
        $e = New-MotionEvidence
        $e.Result.$field = $wrong
        Check-MotionGate "reject-$field-$wrong" $e $false
    }
}
foreach ($field in @('ticks', 'nativeFrames', 'recordedFrames', 'totalFrames')) {
    foreach ($wrong in @(179, '180', 180.5, $null)) {
        $e = New-MotionEvidence
        $e.Result.$field = $wrong
        Check-MotionGate "reject-$field-$wrong" $e $false
    }
}
foreach ($wrong in @(0, 359, '360', $null)) {
    $e = New-MotionEvidence
    $e.Result.nativeRandom.referenceChecks = $wrong
    Check-MotionGate "reject-RNG-checks-$wrong" $e $false
}
foreach ($snapshot in @('prePlayer', 'preJump', 'postJump', 'postPlayer')) {
    $e = New-MotionEvidence
    $e.Frames[25].PSObject.Properties.Remove($snapshot)
    Check-MotionGate "reject-missing-$snapshot" $e $false
    $e = New-MotionEvidence
    $e.Frames[25].$snapshot.velocity.y = '0'
    Check-MotionGate "reject-string-velocity-$snapshot" $e $false
}
$e = New-MotionEvidence
$e.Frames = @($e.Frames | Select-Object -Skip 20)
Check-MotionGate 'reject-omitted-warmup' $e $false
$e = New-MotionEvidence
$e.Frames[21].preJump.controls.jump = $false
Check-MotionGate 'reject-lost-native-input' $e $false
$e = New-MotionEvidence
$e.Frames[0].jumpMovementCalls = 2
Check-MotionGate 'reject-duplicate-native-jump-call' $e $false
$e = New-MotionEvidence
$e.Frames[0].productionSessionState = 'EngagedAlive'
Check-MotionGate 'reject-production-takeover' $e $false
$e = New-MotionEvidence
$e.Result.nativeRandom.unpausedUpdateSeedFinal++
Check-MotionGate 'reject-wrong-independent-LCG' $e $false
$e = New-MotionEvidence 'cloud-release-press'
$e.Frames[26].preJump.releaseJump = $false
Check-MotionGate 'reject-missing-airborne-release-edge' $e $false
Write-Output "PASS $script:passed native motion envelope regressions. No game execution or physics claim."
exit 0

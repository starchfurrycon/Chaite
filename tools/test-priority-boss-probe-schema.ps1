param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactRoot = (Join-Path $root 'artifacts') + [IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactRoot ('priority-probe-schema-test-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($artifactRoot, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $OutputDirectory)) {
    throw 'Schema-test output must be a new named project artifacts subdirectory.'
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null

$catalog = [ordered]@{
    'deerclops' = @('summon','spawn-settle','opening','forward-spikes','rubble','slow-roar','double-spikes','shadow-hands','return-home','teleport-home')
    'skeletron' = @('hover','pre-spin','spin-imminent','spin','spin-pursuit','spin-exit','hand-vertical-imminent','hand-vertical-locking','hand-vertical','hand-horizontal-imminent','hand-horizontal-locking','hand-horizontal')
    'queen-bee' = @('summon','choose','charge-align','charge','charge-brake','bee-wave','move-above','stinger','reacquire')
    'wall-of-flesh' = @('runway','accelerating','low-health','critical','eye-laser')
    # 'summon' and 'monitor' lead the Fishron catalog in the probe, runner and
    # launcher alike: 'summon' drives the native summon path and 'monitor' is
    # the passive F8 arrival fixture. This copy had drifted without them since
    # the monitor fixture landed.
    'duke-fishron' = @('summon','monitor','spawn-fade','spawn-emerge','p1-hover','p1-dash','p1-bubbles','p1-sharknado','p2-transition-fade','p2-transition-emerge','p2-hover','p2-dash','p2-bubbles','p2-sharknado','p3-transition-fade','p3-transition-hidden','p3-reposition','p3-dash','p3-teleport')
    'moon-lord' = @('intro','synchronize-eyes','head-bolts','head-tongue','head-deathray-telegraph','left-sphere-release','right-sphere-release')
}
$expectedIds = @{
    'deerclops'=668; 'skeletron'=35; 'queen-bee'=222; 'wall-of-flesh'=113
    'duke-fishron'=370; 'moon-lord'=398
}

function Write-NewJson([string]$Path, $Value) {
    $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes(($Value | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
    $stream = [IO.File]::Open($Path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
}
function Invoke-Plan([string]$Path, [bool]$ShouldPass, [string]$Label) {
    $log = Join-Path $OutputDirectory ($Label + '.log')
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'run-boss-validation.ps1') `
            -Cases $Path -MaximumCases 180 2>&1
        $code = $LASTEXITCODE
    } finally { $ErrorActionPreference = $previousPreference }
    [IO.File]::WriteAllLines($log, @($output | ForEach-Object { [string]$_ }), [Text.UTF8Encoding]::new($false))
    if ($ShouldPass -and $code -ne 0) { throw "$Label unexpectedly failed; see $log" }
    if (-not $ShouldPass -and $code -eq 0) { throw "$Label unexpectedly passed; see $log" }
}

$probe = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'GameProbe.cs'))
$launcher = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'start-isolated-test.ps1'))
$runner = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'run-boss-validation.ps1'))
# The v1 probe schemas are additive, but these identity/lifecycle fields are
# mandatory for newly produced evidence. Target identity must be copied at the
# ApplyPlan edge so a later NPC-slot reuse cannot rewrite history.
foreach ($fragment in @(
    'selectedTargetAtPlan', 'Type=target.type', 'Active=target.active',
    'npc.type==observedPlanTarget.Type', '{"poisoned",p.poisoned}',
    '{"poisonedFrames",poisonedFrames}', '{"lifeLossFramesWhilePoisoned",lifeLossFramesWhilePoisoned}',
    'chaite-boss-life-observation/v1', 'last-active-expected-roots', 'confirmed-victory',
    'BuildBossLifeObservation(win,out reportedBossLife)', 'Assert-BossLifeEvidence')) {
    if (-not $probe.Contains($fragment) -and -not $runner.Contains($fragment)) {
        throw "GameProbe/runner lost required additive telemetry contract: $fragment"
    }
}
if ($probe -match '\.poisoned\s*=') { throw 'GameProbe must only read Player.poisoned.' }
$afterNativeStart = $probe.IndexOf('static void AfterNativeUpdate()', [StringComparison]::Ordinal)
$afterNativeEnd = $probe.IndexOf('static bool MotionJumpRequested()', $afterNativeStart, [StringComparison]::Ordinal)
if ($afterNativeStart -lt 0 -or $afterNativeEnd -le $afterNativeStart) { throw 'Cannot isolate AfterNativeUpdate telemetry contract.' }
$afterNativeBody = $probe.Substring($afterNativeStart, $afterNativeEnd - $afterNativeStart)
if (-not $afterNativeBody.Contains('CaptureActiveExpectedRootLife();') -or $afterNativeBody.Contains('lastBossLife=bossLife')) {
    throw 'AfterNativeUpdate may update retained Boss life only from active expected roots.'
}
# Projectile.Kill runs for every native projectile. The isolated harness may
# retain a patch point for ABI validation, but it must never synchronously log
# or construct a call stack there: doing so invalidates wall-clock combat
# evidence by making probe diagnostics dominate the native frame.
$killHook = [regex]::Match($probe,
    'public\s+static\s+void\s+ProjectileKilled\s*\(\s*Projectile\s+projectile\s*\)\s*\{(?<body>.*?)\r?\n\s*\}',
    [Text.RegularExpressions.RegexOptions]::Singleline)
if (-not $killHook.Success -or $killHook.Groups['body'].Value -match
        '(?i)\bLog\s*\(|StackTrace') {
    throw 'Projectile.Kill probe hook must remain a no-allocation, no-log ABI stub.'
}
# Variant identity is derived from the native state captured at activation;
# it is not a convenient runner-side default. Keep all declared V3 variants
# visible in the source contract, including the reserved native mechanical
# identities that future reviewed scenarios must prove with topology/flags.
foreach ($fragment in @(
    'ExpectedVariantForScenario', 'CaptureVariantAtActivation()', 'BuildVariantEvidence',
    '"standard"', '"night"', '"day"', '"simultaneous-mechanical-trio"',
    '"getfixedboi-mechdusa"', 'NativeWorldFlag("zenithWorld")',
    '"staged-native-phase-regression"', '"readinessEligible"')) {
    if (-not $probe.Contains($fragment)) { throw "GameProbe lost required variant contract: $fragment" }
}
foreach ($fragment in @(
    '$scenarioVariants', 'Assert-VariantEvidence', 'ReadinessEligible',
    'StagedFixtureRegressionWins', 'staged-native-phase-regression',
    'simultaneous-mechanical-trio', 'getfixedboi-mechdusa')) {
    if (-not $runner.Contains($fragment)) { throw "Runner lost required variant/readiness contract: $fragment" }
}
foreach ($scenario in $catalog.Keys) {
    $id = $expectedIds[$scenario]
    if ($probe -notmatch ('case\s+"' + [regex]::Escape($scenario) + '"') -or
        $probe -notmatch ('(?:DirectScenario|PriorityItemScenario)\([^\r\n]*' + $id + ',') -or
        $launcher -notmatch ("'" + [regex]::Escape($scenario) + "'") -or
        $runner -notmatch ("'" + [regex]::Escape($scenario) + "'")) {
        throw "Scenario $scenario or its native root ID is absent from one validation layer."
    }
    foreach ($phase in $catalog[$scenario]) {
        foreach ($text in @($probe,$launcher,$runner)) {
            if ($text -notmatch ('["'']' + [regex]::Escape($phase) + '["'']')) { throw "Phase $scenario/$phase is absent from one validation layer." }
        }
    }
}
foreach ($argument in @('-phase','-takeovertick')) {
    if ($probe -notmatch [regex]::Escape($argument) -or $launcher -notmatch [regex]::Escape($argument) -or
        $runner -notmatch [regex]::Escape($argument)) { throw "Argument $argument is not wired through every validation layer." }
}

$allCases = @()
# The runner requires a reviewed formulaRoute on every 'monitor' case: the
# monitor phase is the passive F8 arrival fixture, and it has to know which
# loadout to equip before it can check route admission. Emitting monitor cases
# without one made this gate fail on a plan the runner is right to reject, so
# the route is supplied here exactly as a real monitor run does.
$monitorRoute = @{
    'duke-fishron' = 'fishron-strong-wing'
}
foreach ($scenario in $catalog.Keys) {
    foreach ($phase in $catalog[$scenario]) {
        $difficulty = if ($scenario -eq 'duke-fishron' -and $phase -like 'p3-*') { 'expert' } else { 'classic' }
        $case = [ordered]@{ scenario=$scenario; phase=$phase; takeoverTick=240; seed=20260910; difficulty=$difficulty; maxTicks=600 }
        if ($phase -ceq 'monitor') { $case['formulaRoute'] = $monitorRoute[$scenario] }
        $allCases += $case
    }
}
$validPath = Join-Path $OutputDirectory 'all-reviewed-phases.json'
Write-NewJson $validPath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=$allCases })
Invoke-Plan $validPath $true 'all-reviewed-phases'

$unknownPath = Join-Path $OutputDirectory 'unknown-phase.json'
Write-NewJson $unknownPath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='deerclops'; phase='invented'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $unknownPath $false 'reject-unknown-phase'

$classicP3Path = Join-Path $OutputDirectory 'classic-fishron-p3.json'
Write-NewJson $classicP3Path ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; phase='p3-dash'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $classicP3Path $false 'reject-classic-fishron-p3'

$latePath = Join-Path $OutputDirectory 'late-takeover.json'
Write-NewJson $latePath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='moon-lord'; phase='intro'; takeoverTick=480; seed=1; difficulty='master'; maxTicks=600 }) })
Invoke-Plan $latePath $false 'reject-late-takeover'

$correctVariantPath = Join-Path $OutputDirectory 'correct-variant.json'
Write-NewJson $correctVariantPath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; variant='standard'; phase='p1-hover'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $correctVariantPath $true 'accept-correct-explicit-variant'

$wrongVariantPath = Join-Path $OutputDirectory 'wrong-variant.json'
Write-NewJson $wrongVariantPath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; variant='night'; phase='p1-hover'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $wrongVariantPath $false 'reject-scenario-variant-mismatch'

$unknownVariantPath = Join-Path $OutputDirectory 'unknown-variant.json'
Write-NewJson $unknownVariantPath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='eye'; variant='legacy-standard-fixture'; phase='summon'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $unknownVariantPath $false 'reject-unknown-variant'

# The monitor phase's route requirement is pinned from both sides: a monitor
# case without one, and a monitor case carrying an unreviewed route, must both be
# refused. Without these the requirement would only be exercised by the positive
# plan above, so a future edit could drop it silently.
$monitorNoRoutePath = Join-Path $OutputDirectory 'monitor-without-route.json'
Write-NewJson $monitorNoRoutePath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; phase='monitor'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $monitorNoRoutePath $false 'reject-monitor-without-route'

$monitorUnreviewedRoutePath = Join-Path $OutputDirectory 'monitor-unreviewed-route.json'
Write-NewJson $monitorUnreviewedRoutePath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; phase='monitor'; formulaRoute='fishron-unreviewed-route'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $monitorUnreviewedRoutePath $false 'reject-monitor-unreviewed-route'

$summonWithRoutePath = Join-Path $OutputDirectory 'summon-with-route.json'
Write-NewJson $summonWithRoutePath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; phase='summon'; formulaRoute='fishron-strong-wing'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $summonWithRoutePath $false 'reject-non-monitor-with-route'

# startSide selects which end of the Duke Fishron runway the fight opens from.
# The real fight starts about twenty tiles in from one end and which end varies,
# so both values are legal and nothing else is, and the field means nothing on a
# fixture that has no runway.
$badSidePath = Join-Path $OutputDirectory 'invalid-startside.json'
Write-NewJson $badSidePath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='duke-fishron'; phase='monitor'; formulaRoute='fishron-strong-wing'; startSide='middle'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $badSidePath $false 'reject-invalid-startside'

$sideOnNonRunwayPath = Join-Path $OutputDirectory 'startside-on-non-runway.json'
Write-NewJson $sideOnNonRunwayPath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
    scenario='deerclops'; phase='opening'; startSide='left'; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
Invoke-Plan $sideOnNonRunwayPath $false 'reject-startside-on-non-runway'

# Both legal values must be admitted, or the mirror the owner asked for is only
# half reachable.
foreach ($side in @('left', 'right')) {
    $sidePath = Join-Path $OutputDirectory ("startside-$side.json")
    Write-NewJson $sidePath ([ordered]@{ schema='chaite-boss-cases/v2'; cases=@([ordered]@{
        scenario='duke-fishron'; phase='monitor'; formulaRoute='fishron-strong-wing'; startSide=$side; takeoverTick=240; seed=1; difficulty='classic'; maxTicks=600 }) })
    Invoke-Plan $sidePath $true "accept-startside-$side"
}

Write-NewJson (Join-Path $OutputDirectory 'result.json') ([ordered]@{
    schema='chaite-priority-probe-schema-test/v1'; passed=$true; scenarios=$catalog.Count
    reviewedPhaseCases=$allCases.Count; negativeCases=10; nativeProcessesStarted=0
})
Write-Output "PASS priority Boss probe schema: $($catalog.Count) scenarios, $($allCases.Count) reviewed phase tuples, 10 fail-closed negatives."
Write-Output "Evidence: $OutputDirectory"
# This gate deliberately ends on a case that must be refused, so the last
# native call left a non-zero LASTEXITCODE behind. build.ps1 reads that value
# after invoking this script in-process, so without an explicit success the
# gate reported failure while printing PASS — and could never pass at all.
exit 0

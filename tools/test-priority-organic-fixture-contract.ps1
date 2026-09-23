$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Pure source-contract test. It reads project text only and never builds or
# starts Terraria, touches a save, or writes an artifact.
$tools = [IO.Path]::GetFullPath($PSScriptRoot)
$probe = [IO.File]::ReadAllText((Join-Path $tools 'GameProbe.cs'))
$runner = [IO.File]::ReadAllText((Join-Path $tools 'run-boss-validation.ps1'))
$launcher = [IO.File]::ReadAllText((Join-Path $tools 'start-isolated-test.ps1'))
$evaluator = [IO.File]::ReadAllText((Join-Path $tools 'evaluate-boss-readiness.ps1'))
$planner = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $tools) 'src\Chaite.Core\BossStartPlanner.cs'))
$policy = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $tools) 'src\Chaite.Core\SupportedBossPolicy.cs'))
$facade = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $tools) 'src\Chaite.Plugin\TerrariaFacade.cs'))
$runtime = [IO.File]::ReadAllText((Join-Path (Split-Path -Parent $tools) 'src\Chaite.Plugin\Runtime.cs'))
$coverage = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $tools) 'docs\boss-readiness-coverage-v1.json') -Raw -Encoding UTF8 | ConvertFrom-Json

$organic = [ordered]@{
    'deerclops' = [ordered]@{ Item = 5120; Boss = 668; PlannerId = 'deer-thing' }
    'queen-bee' = [ordered]@{ Item = 1133; Boss = 222; PlannerId = 'abeemination' }
}

foreach ($scenario in $organic.Keys) {
    $spec = $organic[$scenario]
    $probeCase = [regex]::Match($probe,
        'case\s+"' + [regex]::Escape($scenario) + '"\s*:(?<body>.*?)(?=\r?\n\s*case\s+"|\r?\n\s*default\s*:)',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $probeCase.Success -or $probeCase.Groups['body'].Value -notmatch
            ('PriorityItemScenario\(' + $spec.Item + ',' + $spec.Boss + ',') -or
        $probeCase.Groups['body'].Value -notmatch 'new\[\]\{"summon",') {
        throw "$scenario is not bound to its reviewed organic item, native Boss root, and summon phase."
    }
    if ($planner -notmatch ('case\s+' + $spec.Item + ':') -or
        $planner -notmatch ('Direct\(item,\s*' + $spec.Boss + ',\s*"' + [regex]::Escape($spec.PlannerId) + '"')) {
        throw "$scenario is missing from the offline generic BossStartPlanner fixture path."
    }
    foreach ($source in @($runner, $launcher)) {
        if ($source -notmatch ("'" + [regex]::Escape($scenario) + "'\s*=\s*@\('summon',")) {
            throw "$scenario summon phase is missing from a runner/launcher catalog."
        }
    }
    foreach ($difficulty in @('classic','expert','master')) {
        $cell = @($coverage.cells | Where-Object {
            $_.boss -ceq $scenario -and $_.difficulty -ceq $difficulty -and $_.variant -ceq 'standard'
        })
        if ($cell.Count -ne 1) { throw "$scenario/$difficulty does not map exactly once to its readiness identity." }
    }
}

foreach ($fragment in @(
    'if(requestedPhase=="summon") scenario.Summon=summonItem;',
    'else scenario.DirectSpawn=true;',
    'if(scenario.Summon>0)',
    'sawSummonConsumed |= p.inventory[1].type!=scenario.Summon || p.inventory[1].stack<1;',
    'if(scenario.DirectSpawn && ticks==directSpawnTick) SpawnDirectEncounter();',
    # The passive monitor fixture must never stage a phase: it exists to observe
    # the real arrival, so staging would fabricate the very thing it measures.
    # The guard gained '!IsMonitorFixture' when that fixture landed; this
    # expectation had drifted without it.
    'if(scenario.DirectSpawn && !IsScopeNegative && !IsMonitorFixture && ticks==takeoverTick) StageRequestedPhase();',
    'bool stagedPhaseFixture=scenario!=null && scenario.DirectSpawn;',
    '"isolated-native-encounter"')) {
    if (-not $probe.Contains($fragment)) { throw "Organic/staged producer boundary is missing: $fragment" }
}
foreach ($fragment in @(
    "'priority-organic6'", "'deerclops'=5120", "'queen-bee'=1133",
    '$organicPriority', '$expectedDirectSpawn', 'result summonConsumed',
    'equipment summonType', 'equipment summonCount')) {
    if (-not $runner.Contains($fragment)) { throw "Runner organic source gate is missing: $fragment" }
}
foreach ($fragment in @(
    'encounter-origin-direct-spawn-or-missing', 'encounter-origin-staging-flag-or-missing',
    'encounter-origin-staging-report-present', 'encounter-origin-summon-not-observed',
    'encounter-origin-hotbar-summon-unverified', "'deerclops' { return 5120 }",
    "'queen-bee' { return 1133 }")) {
    if (-not $evaluator.Contains($fragment)) { throw "Readiness origin gate is missing: $fragment" }
}
foreach ($fragment in @('ExecuteBossStart', 'case BossSummonKind.DirectItem:',
    'ExecuteSummonPulse(player, plan', 'SetSelectedItem(player, plan.SummonSlot)',
    'SetControl(player, tileUse ? "controlUseTile" : "controlUseItem", pulse)')) {
    if (-not $facade.Contains($fragment)) { throw "Production F8 item-use path is missing: $fragment" }
}
# The takeover is MOVEMENT ONLY. This block used to require the post-summon
# weapon handoff -- EnsureCombatWeaponSelected, the hotbar advance, and the
# can-change gate -- which meant the test asserted the opposite of the shipped
# rule once that handoff was deleted. What survives is the movement decision
# path, and it must still latch its frame.
foreach ($fragment in @(
    'if (decision.Rejected)',
    '_game.ClearCombatControls(player);',
    '_frameApplied = true;')) {
    if (-not $runtime.Contains($fragment)) { throw "Post-summon movement-decision path is missing: $fragment" }
}

# Deerclops and Queen Bee remain useful native-engine fixture producers, but
# neither item may cross the shipped runtime's one-Boss admission boundary.
foreach ($fragment in @(
    'public static BossStartPlan SelectProduction(BossStartContext context)',
    'if (item.Type != 2673',
    'SupportedBossPolicy.TryValidateStartPlan(plan')) {
    if (-not $planner.Contains($fragment)) { throw "Production one-Boss selector gate is missing: $fragment" }
}
foreach ($fragment in @(
    'public const int DukeFishronType = 370;',
    'return type == DukeFishronType;',
    'plan.Kind == BossSummonKind.TruffleWormFishing')) {
    if (-not $policy.Contains($fragment)) { throw "Production allowlist contract is missing: $fragment" }
}
# Production no longer owns a summon path at all. The owner summons (truffle
# worm) and F8 only arms a passive monitor, so the
# runtime must not retain the item-consuming start chain even as dead code: a
# leftover FindBossStartPlan/ExecuteBossStart call is exactly how an
# unsupported offline plan could consume an item again. The facade keeps those
# entry points for the offline native fixtures, which is asserted above; what
# matters here is that production never reaches them.
foreach ($fragment in @(
    'FindBossStartPlan', 'ExecuteBossStart', 'TryValidateStartPlan')) {
    if ($runtime.Contains($fragment)) {
        throw "Production runtime must not retain the item-consuming start chain, found: $fragment"
    }
}
# Three stages can continue an authorized control session, and each one must
# revalidate the Boss scope before it does: the live tick, the deferred native
# mobility admission, and the input-blocked keep-alive. One definition plus
# three guarded call sites is the whole chain.
$authorizedScopeMentions = [regex]::Matches($runtime,
    'TryValidateAuthorizedBossScope\s*\(').Count
if ($authorizedScopeMentions -ne 4) {
    throw "The authorization chain must be one definition plus three guarded call sites; found $authorizedScopeMentions mentions."
}
foreach ($fragment in @(
    'if (!TryValidateAuthorizedBossScope(liveObservation,',
    'if (!TryValidateAuthorizedBossScope(observation,',
    'return TryValidateAuthorizedBossScope(observation, out reason);')) {
    if (-not $runtime.Contains($fragment)) {
        throw "A production stage stopped validating Boss authorization: $fragment"
    }
}
$liveScope = $runtime.IndexOf(
    'if (!TryValidateAuthorizedBossScope(liveObservation,',
    [StringComparison]::Ordinal)
$liveUpdate = $runtime.IndexOf(
    'var update = _encounter.Update(liveObservation);',
    [StringComparison]::Ordinal)
if ($liveScope -lt 0 -or $liveUpdate -lt 0 -or $liveScope -gt $liveUpdate) {
    throw 'The live tick must validate Boss authorization before it advances the encounter state.'
}
if ($runtime -match
    'if\s*\(\s*\w+\.HasEncounter\s*&&\s*!TryValidateAuthorizedBossScope') {
    throw 'Boss authorization must not be conditional on HasEncounter; a missing root must break continuity.'
}
$tick = [regex]::Match($runtime,
    'public static void Tick\(object player, int playerIndex\)(?<body>.*?)(?=\r?\n\s*public static void ApplyPendingInput)',
    [Text.RegularExpressions.RegexOptions]::Singleline)
$clearPending = if ($tick.Success) {
    $tick.Groups['body'].Value.IndexOf('_pendingInput = false;',
        [StringComparison]::Ordinal)
} else { -1 }
$clearPlayer = if ($tick.Success) {
    $tick.Groups['body'].Value.IndexOf('_pendingPlayer = null;',
        [StringComparison]::Ordinal)
} else { -1 }
$localGate = if ($tick.Success) {
    $tick.Groups['body'].Value.IndexOf('if (!_game.IsLocalPlayer(player, playerIndex))',
        [StringComparison]::Ordinal)
} else { -1 }
$bindPlayer = if ($tick.Success) {
    $tick.Groups['body'].Value.IndexOf('_pendingPlayer = player;',
        [StringComparison]::Ordinal)
} else { -1 }
$sessionGate = if ($tick.Success) {
    $tick.Groups['body'].Value.IndexOf(
        '!MatchesAuthorizedSessionIdentity(player)',
        [StringComparison]::Ordinal)
} else { -1 }
$cancelGate = if ($tick.Success) {
    $tick.Groups['body'].Value.IndexOf('if (keys.CancelPressed)',
        [StringComparison]::Ordinal)
} else { -1 }
if (-not $tick.Success -or $clearPending -lt 0 -or $clearPlayer -lt 0 -or
    $localGate -lt 0 -or $bindPlayer -lt 0 -or $sessionGate -lt 0 -or
    $cancelGate -lt 0 -or
    $clearPending -gt $localGate -or $clearPlayer -gt $localGate -or
    $bindPlayer -lt $localGate -or $sessionGate -lt $localGate -or
    $sessionGate -gt $cancelGate) {
    throw 'Tick must invalidate the previous replay identity before every local-player early return.'
}
foreach ($fragment in @(
    'CaptureAuthorizedSessionIdentity(',
    'MatchesAuthorizedSessionIdentity(player)',
    'MatchesAuthorizedSessionIdentity(null)',
    '_authorizedWorldToken',
    '_authorizedPlayerToken',
    '_authorizedWorldUniqueId',
    '_authorizedWorldId',
    '_authorizedNetMode',
    '_authorizedPlayerIndex',
    '_game.TryGetSupportedBossIdentity(npc, out key,',
    'generation != _authorizedBossGeneration',
    'QueueKilledBossScopeRejection(')) {
    if (-not $runtime.Contains($fragment)) {
        throw "Production session/kill identity gate is missing: $fragment"
    }
}
if (-not $facade.Contains('localPlayerIndex >= 255')) {
    throw 'Production session identity must reject the vanilla no-local-player sentinel.'
}
# Movement-only takeover, stated as a prohibition rather than a requirement.
# The removed handoff is exactly the kind of code that comes back by accident:
# a single _game.SetSelectedItem call on the planner's behalf would suppress
# whatever the user is attacking with. A bare mention in a comment is fine; a
# call is not. (CombatWeaponSelectionHandoff.cs is itself dead now -- nothing
# outside its own file references it -- but it lives in Chaite.Core, and
# deleting it would change InputCoreSha256 in the middle of a measurement wave,
# so it is deferred to the end of the wave rather than done here.)
foreach ($forbidden in @(
    'EnsureCombatWeaponSelected(player)',
    'CombatWeaponSelection.Advance(',
    '_game.CanChangeSelectedItemImmediately(',
    '_game.SetSelectedItem(player, desired)')) {
    if ($runtime.Contains($forbidden)) {
        throw "Runtime must not take over weapon selection (movement-only takeover): $forbidden"
    }
}

Write-Output 'PASS priority organic fixture source contract: Deer Thing and Abeemination remain isolated offline producers, production admission accepts only the Fishron route before any item use, and the runtime takes over movement only (never weapon selection); no native process.'
exit 0

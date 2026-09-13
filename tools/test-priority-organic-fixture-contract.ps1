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
    'if(scenario.DirectSpawn && !IsScopeNegative && ticks==takeoverTick) StageRequestedPhase();',
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
foreach ($fragment in @(
    'if (!EnsureCombatWeaponSelected(player))',
    'CombatWeaponSelection.Advance(',
    '_game.CanChangeSelectedItemImmediately(player)',
    'if (decision.Rejected)',
    '_game.ClearCombatControls(player);',
    '_frameApplied = true;')) {
    if (-not $runtime.Contains($fragment)) { throw "Post-summon weapon-selection handoff is missing: $fragment" }
}

# Deerclops and Queen Bee remain useful native-engine fixture producers, but
# neither item may cross the shipped runtime's two-Boss admission boundary.
foreach ($fragment in @(
    'public static BossStartPlan SelectProduction(BossStartContext context)',
    'if (item.Type != 2673 && item.Type != 4961)',
    'SupportedBossPolicy.TryValidateStartPlan(plan')) {
    if (-not $planner.Contains($fragment)) { throw "Production two-Boss selector gate is missing: $fragment" }
}
foreach ($fragment in @(
    'public const int DukeFishronType = 370;',
    'public const int EmpressOfLightType = 636;',
    'return type == DukeFishronType || type == EmpressOfLightType;',
    'plan.Kind == BossSummonKind.TruffleWormFishing',
    'plan.Kind == BossSummonKind.PrismaticLacewing')) {
    if (-not $policy.Contains($fragment)) { throw "Production allowlist contract is missing: $fragment" }
}
$findPlan = $runtime.IndexOf('_game.FindBossStartPlan(player)', [StringComparison]::Ordinal)
$validatePlan = $runtime.IndexOf('!SupportedBossPolicy.TryValidateStartPlan(_startPlan,', [StringComparison]::Ordinal)
$executePlan = $runtime.IndexOf('_game.ExecuteBossStart(player, _startPlan,', [StringComparison]::Ordinal)
if ($findPlan -lt 0 -or $validatePlan -lt 0 -or $executePlan -lt 0 -or
    $findPlan -gt $validatePlan -or $validatePlan -gt $executePlan) {
    throw 'Production start-plan admission must reject unsupported offline plans before ExecuteBossStart can consume an item.'
}
$authorizedScopeMentions = [regex]::Matches($runtime,
    'TryValidateAuthorizedBossScope\s*\(').Count
if ($authorizedScopeMentions -ne 6 -or $runtime -match
    'if\s*\(\s*\w+\.HasEncounter\s*&&\s*!TryValidateAuthorizedBossScope') {
    throw 'All five production stages must validate Boss authorization unconditionally so a missing root breaks continuity.'
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
$handoff = [regex]::Match($runtime,
    'private static bool EnsureCombatWeaponSelected\(object player\)(?<body>.*?)(?=\r?\n\s*private static void ResetSessionAutomation)',
    [Text.RegularExpressions.RegexOptions]::Singleline)
$selectionIndex = if ($handoff.Success) { $handoff.Groups['body'].Value.IndexOf('_game.SetSelectedItem(player, desired);', [StringComparison]::Ordinal) } else { -1 }
$neutralIndex = if ($handoff.Success) { $handoff.Groups['body'].Value.IndexOf('_game.ClearCombatControls(player);', [StringComparison]::Ordinal) } else { -1 }
if (-not $handoff.Success -or $selectionIndex -lt 0 -or $neutralIndex -lt 0 -or
    $selectionIndex -gt $neutralIndex) {
    throw 'Post-summon selection must be requested before the neutral transition frame is captured.'
}

Write-Output 'PASS priority organic fixture source contract: Deer Thing and Abeemination remain isolated offline producers, while production admission accepts only Fishron/Empress routes before any item use; no native process.'

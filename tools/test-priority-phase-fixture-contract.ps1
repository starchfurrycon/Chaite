$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# Pure source-contract test: no build, Terraria process, save, artifact, or
# desktop is created. The tuple catalog is intentionally duplicated here so a
# review must explicitly update both the 1.4.5.8 audit and its executable
# fixture rather than silently changing one magic number.
$tools = [IO.Path]::GetFullPath($PSScriptRoot)
$probePath = Join-Path $tools 'GameProbe.cs'
$runnerPath = Join-Path $tools 'run-boss-validation.ps1'
$launcherPath = Join-Path $tools 'start-isolated-test.ps1'
foreach ($path in @($probePath, $runnerPath, $launcherPath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing fixture source: $path" }
}
$probe = [IO.File]::ReadAllText($probePath)
$runner = [IO.File]::ReadAllText($runnerPath)
$launcher = [IO.File]::ReadAllText($launcherPath)

$phases = [ordered]@{
    'deerclops' = @('summon','spawn-settle','opening','forward-spikes','rubble','slow-roar','double-spikes','shadow-hands','return-home','teleport-home')
    'skeletron' = @('hover','pre-spin','spin-imminent','spin','spin-pursuit','spin-exit','hand-vertical-imminent','hand-vertical-locking','hand-vertical','hand-horizontal-imminent','hand-horizontal-locking','hand-horizontal')
    'queen-bee' = @('summon','choose','charge-align','charge','charge-brake','bee-wave','move-above','stinger','reacquire')
    # These four phase catalogs do not use a compact integer-tuple table like
    # the movement-heavy scenarios below, but the runner, launcher and probe
    # must nevertheless agree exactly.  In particular, a fixture-only phase
    # must never disappear from one layer and make a mid-fight takeover look
    # tested when the test copy cannot actually stage it.
    #
    # The Fishron catalog below carries the two shared leading phases 'summon'
    # and 'monitor'. They are the probe's two non-staged entries: 'summon'
    # drives the native summon path and 'monitor' is the passive F8 arrival
    # fixture. Both are real production phases, so the probe, runner and
    # launcher all list them; this catalog had drifted without them since the
    # monitor fixture landed.
    'wall-of-flesh' = @('runway','accelerating','low-health','critical','eye-laser')
    'duke-fishron' = @('summon','monitor','spawn-fade','spawn-emerge','p1-hover','p1-dash','p1-bubbles','p1-sharknado','p2-transition-fade','p2-transition-emerge','p2-hover','p2-dash','p2-bubbles','p2-sharknado','p3-transition-fade','p3-transition-hidden','p3-reposition','p3-dash','p3-teleport')
    'moon-lord' = @('intro','synchronize-eyes','head-bolts','head-tongue','head-deathray-telegraph','left-sphere-release','right-sphere-release')
}

$tuples = [ordered]@{
    DeerclopsStageTuples = [ordered]@{
        'spawn-settle'=@(-1,0); 'opening'=@(0,0); 'forward-spikes'=@(1,19)
        'rubble'=@(2,21); 'slow-roar'=@(3,27); 'double-spikes'=@(4,35)
        'shadow-hands'=@(5,20); 'return-home'=@(6,0); 'teleport-home'=@(7,30)
    }
    SkeletronStageTuples = [ordered]@{
        'hover'=@(0,600,-1,0); 'pre-spin'=@(0,730,-1,0); 'spin-imminent'=@(0,790,-1,0)
        'spin'=@(1,0,-1,0); 'spin-pursuit'=@(1,200,-1,0); 'spin-exit'=@(1,370,-1,0)
        'hand-vertical-imminent'=@(0,200,0,270); 'hand-vertical-locking'=@(0,200,1,0)
        'hand-vertical'=@(0,200,2,0); 'hand-horizontal-imminent'=@(0,200,3,270)
        'hand-horizontal-locking'=@(0,200,4,0); 'hand-horizontal'=@(0,200,5,0)
    }
    QueenBeeStageTuples = [ordered]@{
        'choose'=@(-1,0,0); 'charge-align'=@(0,0,0); 'charge'=@(0,1,0)
        'charge-brake'=@(0,1,1); 'bee-wave'=@(1,0,0); 'move-above'=@(2,0,0)
        'stinger'=@(3,31,0); 'reacquire'=@(4,0,0)
    }
    FishronStageTuples = [ordered]@{
        'spawn-fade'=@(-1,20,0); 'spawn-emerge'=@(-1,60,0); 'p1-hover'=@(0,0,0)
        'p1-dash'=@(1,0,0); 'p1-bubbles'=@(2,0,1); 'p1-sharknado'=@(3,50,0)
        'p2-transition-fade'=@(4,60,0); 'p2-transition-emerge'=@(4,140,0)
        'p2-hover'=@(5,0,0); 'p2-dash'=@(6,0,0); 'p2-bubbles'=@(7,0,1)
        'p2-sharknado'=@(8,50,0); 'p3-transition-fade'=@(9,60,0)
        'p3-transition-hidden'=@(9,140,0); 'p3-reposition'=@(10,0,1)
        'p3-dash'=@(11,0,0); 'p3-teleport'=@(12,10,1)
    }
}

function Get-QuotedValues([string]$Text, [string]$QuotePattern) {
    return @([regex]::Matches($Text, $QuotePattern) | ForEach-Object { $_.Groups['value'].Value })
}

function Assert-ExactSequence([string]$Name, [string[]]$Actual, [string[]]$Expected) {
    $actualText = [string]::Join("`n", $Actual)
    $expectedText = [string]::Join("`n", $Expected)
    if ($Actual.Count -ne $Expected.Count -or
        -not [string]::Equals($actualText, $expectedText, [StringComparison]::Ordinal)) {
        throw "$Name drifted. Expected [$($Expected -join ', ')], actual [$($Actual -join ', ')]."
    }
}

function Get-ProbePhases([string]$Scenario) {
    $case = [regex]::Match($probe, 'case\s+"' + [regex]::Escape($Scenario) + '"\s*:(?<body>.*?)(?=\r?\n\s*case\s+"|\r?\n\s*default\s*:)',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $case.Success) { throw "GameProbe scenario case is missing: $Scenario" }
    $arrays = [regex]::Matches($case.Groups['body'].Value, 'new\[\]\{(?<list>[^}]*)\}')
    if ($arrays.Count -lt 2) { throw "GameProbe phase array is missing: $Scenario" }
    return Get-QuotedValues $arrays[$arrays.Count - 1].Groups['list'].Value '"(?<value>[a-z0-9-]+)"'
}

function Get-PowerShellPhases([string]$Source, [string]$Scenario, [string]$Layer) {
    $match = [regex]::Match($Source, "'" + [regex]::Escape($Scenario) + "'\s*=\s*@\((?<list>[^)]*)\)")
    if (-not $match.Success) { throw "$Layer phase array is missing: $Scenario" }
    return Get-QuotedValues $match.Groups['list'].Value "'(?<value>[a-z0-9-]+)'"
}

foreach ($scenario in $phases.Keys) {
    $expected = [string[]]$phases[$scenario]
    Assert-ExactSequence "GameProbe $scenario phases" (Get-ProbePhases $scenario) $expected
    Assert-ExactSequence "runner $scenario phases" (Get-PowerShellPhases $runner $scenario 'runner') $expected
    Assert-ExactSequence "launcher $scenario phases" (Get-PowerShellPhases $launcher $scenario 'launcher') $expected
}

function Get-TupleTable([string]$Name) {
    $match = [regex]::Match($probe,
        'Dictionary<string,int\[\]>\s+' + [regex]::Escape($Name) +
        '\s*=\s*new\s+Dictionary<string,int\[\]>\(StringComparer\.Ordinal\)\s*\{(?<body>.*?)\r?\n\s*\};',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $match.Success) { throw "Tuple table is missing or no longer ordinal: $Name" }
    $result = [ordered]@{}
    foreach ($entry in [regex]::Matches($match.Groups['body'].Value,
        '\{"(?<phase>[a-z0-9-]+)",new\[\]\{(?<values>-?[0-9]+(?:,-?[0-9]+)*)\}\}')) {
        $phase = $entry.Groups['phase'].Value
        if ($result.Contains($phase)) { throw "Duplicate $Name phase: $phase" }
        $result[$phase] = @($entry.Groups['values'].Value.Split(',') | ForEach-Object {
            [int]::Parse($_, [Globalization.CultureInfo]::InvariantCulture)
        })
    }
    return $result
}

foreach ($tableName in $tuples.Keys) {
    $actual = Get-TupleTable $tableName
    $expected = $tuples[$tableName]
    Assert-ExactSequence "$tableName keys" ([string[]]$actual.Keys) ([string[]]$expected.Keys)
    foreach ($phase in $expected.Keys) {
        Assert-ExactSequence "$tableName/$phase tuple" ([string[]]@($actual[$phase] | ForEach-Object { [string]$_ })) `
            ([string[]]@($expected[$phase] | ForEach-Object { [string]$_ }))
    }
}

$requiredSourceContracts = @(
    'StageSkeletronHandGeometry(root,hand,tuple[2])',
    'SkeletronHandGeometryMatches(root,hand,skeletonTuple[2])',
    'StageQueenBeeGeometry(root,tuple[0],tuple[1],tuple[2])',
    'QueenBeeGeometryMatches(root,queenTuple[0],queenTuple[1],queenTuple[2])',
    'StageWall(root,mutation)',
    'StageFishronMotion(root,state)',
    'FishronMotionMatches(root,fishTuple[0])',
    'StageMoonLord(root,mutation)',
    'test-copy NPC life/ai/localAI/position/velocity only'
)
foreach ($contract in $requiredSourceContracts) {
    if (-not $probe.Contains($contract)) { throw "Required position/velocity fixture contract is missing: $contract" }
}
# A hand-staged native phase is a recovery regression, not an organic sample
# for a victory-rate campaign. Keep the boundary in both producer and runner
# source so a later summary field cannot silently rebrand those wins.
foreach ($contract in @('bool stagedPhaseFixture=scenario!=null && scenario.DirectSpawn;',
    'bool readinessEligible=!stagedPhaseFixture;', '"staged-native-phase-regression"')) {
    if (-not $probe.Contains($contract)) { throw "Priority fixture readiness boundary is missing from GameProbe: $contract" }
}
foreach ($contract in @('A staged priority phase fixture must not be labeled as readiness-eligible evidence.',
    'StagedFixtureRegressionWins', 'ReadinessEligibleSuccessPercent')) {
    if (-not $runner.Contains($contract)) { throw "Priority fixture readiness boundary is missing from runner: $contract" }
}
foreach ($tableName in $tuples.Keys) {
    $uses = [regex]::Matches($probe, [regex]::Escape($tableName + '.TryGetValue')).Count
    if ($uses -ne 2) { throw "$tableName tuple lookup count drifted; expected 2, actual $uses" }
}

Write-Output ("PASS priority phase fixture source contract: {0} scenarios, {1} exact tuples, synchronized runner/launcher catalogs, no native process." -f `
    $phases.Count, (($tuples.Values | ForEach-Object { $_.Count } | Measure-Object -Sum).Sum))
exit 0

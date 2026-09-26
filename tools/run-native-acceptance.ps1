<#
.SYNOPSIS
    Native acceptance run: prepares a fresh isolated probe, launches it, and prints the verdict.

.DESCRIPTION
    This is the acceptance path for the Duke Fishron state machines. It exists
    because the surrogate lab (tests/Chaite.Tests/FishronNoHitLab.cs) diverged
    from the real game in ways that invalidated conclusions, so acceptance is
    native or it is nothing.

    What "native" means here, concretely: the real Terraria.exe, with the real
    AI_069, the real player physics, the real damage and hurt paths, running a
    real Duke Fishron, on an in-memory ocean-biome arena. Nothing about the
    fight is modelled -- the only thing the harness controls is which input bits
    the player receives.

    Three things are checked against the game rather than assumed:

      * the arena really is the ocean fixture, one long flat floor, because the
        owner's constraint is that terrain or environment problems can break the
        state machine rather than the strategy;
      * the loadout really is the declared formula fixture;
      * hits is read from result.json, where it counts native update frames whose
        player life decreased. A zero there is the only thing that may be called
        a no-hit result.

    Every run lands in its own artifacts\<RunName> directory with the pin plan,
    launch binding, probe log and observation streams retained.
#>
param(
    [Parameter(Mandatory = $true)][string]$RunName,

    # prepare reuses an existing prepared run; without it a fresh one is built,
    # which is the only safe default when the probe sources may have changed.
    [switch]$Prepare,

    # duke-fishron direct phases, as enforced by GameProbe.ReadSettings. A
    # summon or monitor phase never hands movement to a route, so an acceptance
    # run must use an engaged phase.
    [ValidateSet('summon', 'monitor', 'p1-hover', 'p2-dash', 'p3-dash',
        'p3-escape', 'p3-counter', 'p3-full')]
    [string]$Phase = 'p1-hover',

    [int]$TakeoverTick = 120,
    [ValidateSet('classic', 'expert', 'master')][string]$Difficulty = 'expert',
    [int]$MaxTicks = 6000,
    [int]$WallSeconds = 300,
    [ValidateSet('left', 'right')][string]$StartSide = 'left',

    # A prebuilt route makes RouteReplay own movement: this is the replay half
    # of native acceptance. Without one the production planner owns movement.
    [string]$RouteFile,

    # Skip preparing and use this already-prepared run directory.
    [string]$ReuseRun,

    # A reviewed formula route runs the circuit that already exists in
    # src/Chaite.Core/FishronWingScript.cs instead of the generic planner. The
    # probe only accepts it on the monitor phase, by its own validation.
    [ValidateSet('fishron-fairy-wing', 'fishron-strong-wing', 'fishron-trusty-chillet',
        'fishron-trusty-chillet-ignis', 'fishron-lilith-wolf')]
    [string]$FormulaRoute,

    [switch]$SkipBeam,
    [switch]$StopOnHit
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))

function Write-Section([string]$Text) {
    Write-Host ''
    Write-Host ('=' * 68)
    Write-Host $Text
    Write-Host ('=' * 68)
}

# ------------------------------------------------------------- preparation ---
# prepare-game-probe enforces its own 'game-probe-<alnum-and-dash>' run naming,
# so a caller-supplied label is folded into that shape rather than rejected.
function ConvertTo-ProbeRunName([string]$Label) {
    $clean = ($Label -replace '[^a-zA-Z0-9\-]', '-').Trim('-')
    if ($clean -like 'game-probe-*') { return $clean }
    return "game-probe-$clean"
}

$targetRun = $ReuseRun
if (-not $ReuseRun) {
    $preparedName = ConvertTo-ProbeRunName $RunName
    Write-Section "PREPARE $preparedName"
    & (Join-Path $PSScriptRoot 'prepare-game-probe.ps1') -RunName $preparedName -Headless
    if ($LASTEXITCODE -ne 0) { throw "prepare-game-probe.ps1 failed with $LASTEXITCODE" }
    $targetRun = $preparedName
}

$runDir = Join-Path $root ('artifacts\' + $targetRun)
if (-not (Test-Path -LiteralPath $runDir -PathType Container)) {
    throw "No prepared run at $runDir"
}
$exe = Join-Path $runDir 'Terraria.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) {
    throw "Prepared run has no Terraria.exe: $runDir"
}

# ------------------------------------------------------------------ launch ---
Write-Section "LAUNCH phase=$Phase takeover=$TakeoverTick ticks=$MaxTicks"

$arguments = @(
    '-scenario', 'duke-fishron',
    '-phase', $Phase,
    '-takeovertick', $TakeoverTick,
    '-difficulty', $Difficulty,
    '-maxticks', $MaxTicks,
    '-wallseconds', $WallSeconds,
    '-startside', $StartSide
)
if ($SkipBeam) { $arguments += '-skipbeam' }
if ($StopOnHit) { $arguments += '-stoponhit' }
if ($FormulaRoute) { $arguments += @('-formularoute', $FormulaRoute) }

if ($RouteFile) {
    $resolved = [IO.Path]::GetFullPath($RouteFile)
    if (-not (Test-Path -LiteralPath $resolved -PathType Leaf)) {
        throw "Route file not found: $resolved"
    }
    $env:CHAITE_ROUTE_FILE = $resolved
    Write-Host "Route replay: $resolved"
}

$timeout = [Math]::Min(86400, [Math]::Max(120, $WallSeconds + 300))

# start-isolated-test throws on any non-zero child exit, but the probe's own
# exit codes are part of its contract: 20 is "test-time-limit", which is what a
# fight bounded by -maxticks returns even when nothing went wrong, and a
# finished fight reports its outcome in result.json rather than in the status.
# The throw is therefore caught and the exit code kept, because the verdict has
# to be read from result.json, not inferred from the process status.
$launchExit = 0
try {
    & (Join-Path $PSScriptRoot 'start-isolated-test.ps1') `
        -TargetExe $exe `
        -TargetArguments $arguments `
        -TimeoutSeconds $timeout
    $launchExit = $LASTEXITCODE
} catch {
    $launchExit = if ($LASTEXITCODE) { $LASTEXITCODE } else { 1 }
    Write-Host "Launch reported: $($_.Exception.Message)"
}
Write-Host "Probe process exit code: $launchExit"

# ----------------------------------------------------------------- verdict ---
Write-Section 'VERDICT (read from the native result.json)'

$resultPath = Join-Path $runDir 'result.json'
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    Write-Host "No result.json: the run did not reach a fight."
    exit 2
}

$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json

$hits = [int]$result.hits
$ticks = [int]$result.ticks
$death = [bool]$result.death
$validBattle = [bool]$result.validBattle
$bossSeen = [bool]$result.allExpectedBossesSeen
$outcome = [string]$result.outcome
$bossDamage = [int]$result.bossDamage

Write-Host ("scenario        : {0} / {1} / phase={2}" -f $result.scenario, $result.difficulty, $Phase)
Write-Host ("valid battle    : {0}   boss seen: {1}" -f $validBattle, $bossSeen)
Write-Host ("ticks           : {0}" -f $ticks)
Write-Host ("boss damage     : {0}   boss life left: {1}" -f $bossDamage, $result.bossLifeRemaining)
Write-Host ("death           : {0}   outcome: {1}" -f $death, $outcome)
Write-Host ("HITS            : {0}" -f $hits)

# The arena is the owner's constraint, so it is asserted rather than trusted.
$arena = $result.arena
if ($null -ne $arena) {
    $width = [int]$arena.groundRightExclusive - [int]$arena.groundLeft
    Write-Host ''
    Write-Host ("arena ground    : tiles {0}..{1} exclusive = {2} tiles" -f $arena.groundLeft, $arena.groundRightExclusive, $width)
    Write-Host ("platform rows   : {0} (spacing {1})" -f ($arena.platformRows -join ','), $arena.platformRowSpacingTiles)
    Write-Host ("start side      : {0} at tile {1}" -f $arena.startSide, $arena.playerStartTileX)
    if ($width -lt 300) {
        Write-Host "WARNING: arena is under the owner's 300-tile minimum."
    }
}

$equipment = $result.equipment
if ($null -ne $equipment) {
    Write-Host ("loadout         : {0}" -f $equipment.label)
    Write-Host ("armor+accessory : {0}" -f ($equipment.armorAndAccessories -join ','))
}

# The shield stream is what makes an i-frame claim checkable: a dash that never
# reaches native dashDelay = -1 cannot have granted anything.
$shieldPath = Join-Path $runDir 'shield-events.jsonl'
if (Test-Path -LiteralPath $shieldPath -PathType Leaf) {
    $rows = Get-Content -LiteralPath $shieldPath
    $started = 0; $contact = 0; $dashing = 0
    foreach ($row in $rows) {
        if ($row -notmatch '"started":true') { } else { $started++ }
        if ($row -match '"contact":true') { $contact++ }
        if ($row -match '"dashDelay":-1') { $dashing++ }
    }
    Write-Host ''
    Write-Host ("shield rows     : {0}   dash started: {1}   dash-active ticks: {2}   npc contact: {3}" -f $rows.Count, $started, $dashing, $contact)
    if ($started -eq 0) {
        Write-Host "NOTE: no dash ever started, so no i-frame can have been granted in this run."
    }
}

Write-Host ''
if (-not $validBattle -or -not $bossSeen) {
    Write-Host 'NOT ACCEPTED: the run was not a valid battle against the expected boss.'
    exit 3
}
# OWNER RULING 2026-09-26: acceptance is EITHER a no-hit run at the cap, OR a
# run that kills the Boss while surviving, screened across the DPS band. The
# second form is what the owner asked for explicitly: "只要能在对BOSS的DPS在
# 300-2000情况下都能稳定存活击杀猪鲨也算验收成功".
#
# A kill is read from the native accounting, not from the outcome string alone:
# `bossLifeRemaining` reaching 0 with the player alive is the engine's own
# statement that the Boss died and the player did not. `SuccessNoDeath` is
# recorded alongside so the two agree, but the life/death pair is what decides.
$bossLifeLeft = $result.bossLifeRemaining
$killed = ($null -ne $bossLifeLeft -and [int]$bossLifeLeft -le 0)
if ($hits -eq 0 -and -not $death) {
    Write-Host 'ACCEPTED: zero hits in the native engine.'
    exit 0
}
if ($killed -and -not $death) {
    Write-Host ("ACCEPTED (kill): the Boss died and the player survived, with {0} hit(s) taken." -f $hits)
    Write-Host '  This satisfies the owner''s survival-and-kill criterion; it is NOT a no-hit claim.'
    exit 0
}
if ($death -and $killed) {
    Write-Host 'NOT ACCEPTED: the Boss died but so did the player.'
    exit 4
}
Write-Host ("NOT ACCEPTED: {0} hit(s), Boss still alive with {1} life." -f $hits, $bossLifeLeft)
exit 1

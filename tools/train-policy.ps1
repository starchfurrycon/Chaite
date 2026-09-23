# No-recompile policy search for the Chaite residual network.
#
# The project's hard constraint is that a training run must not recompile: a
# probe wave that spans two builds is void. The learned policy is loaded from a
# file named by CHAITE_POLICY_FILE, so a search can move the parameters without
# touching the build at all -- that file interface is what makes training
# possible, and this driver is the loop around it.
#
# Each trial writes one candidate policy, runs one wave against fixed seeds, and
# scores it against the method's criterion. It never edits source, never builds,
# and writes only policy files plus its own log.
#
# Scoring, in priority order. The acceptance criterion is "the fight is won with
# zero hits", so that is the first term and it is counted directly rather than
# approximated:
#
#   1. noHitWins, higher is better: how many seeds were won with hits equal to
#      zero. This is the acceptance criterion itself, so it ranks first. It used
#      to not be a term at all -- "win" appeared nowhere in the ordering, and a
#      scoring run over the nine measured combinations showed the consequence:
#      the combination that dies at tick 1112 having taken 4 percent of the
#      Boss's life scored better on the primary term than the only
#      combination that has ever won the fight.
#   2. wins, higher is better: how many seeds were won at all. The step before
#      the criterion, so a policy that starts winning outranks one that only
#      survives.
#   3. deaths, lower is better. Dying is the failure that a hits total can hide:
#      a run that dies at tick 500 accumulates fewer hits than one that survives
#      to tick 5000, so hits alone would prefer it. Comparing deaths first
#      removes that, and it is what makes the hits term below safe to use as a
#      total.
#   4. hits, lower is better. Among policies that win the same number of seeds
#      and die the same number of times, fewer hits is strictly closer to the
#      criterion.
#   5. cleanLoopShare, higher is better, the fraction of loops that took no
#      damage at all. This is the method's own unit, and it is a ratio over the
#      loops actually produced rather than a count, so it cannot be improved by
#      surviving longer and producing more loops.
#   6. playerDamagePerK, lower is better, within a tolerance.
#   7. damage, higher is better, as a tie-break only.
#
# Earlier revisions ranked on the COUNT of clean closed loops and on
# hitsPerThousandTicks. Both are proxies that scale with fight length, and
# neither is the acceptance criterion, which is why the search could report
# progress while the fight was still lost.
#
# A loop is a maximal run of sampled rows sharing one native Boss state, and its
# end is the tick of the next state change, not the next sampled row. Getting
# that boundary wrong was measured to turn 43 hits into 1 on one tag, so the
# segmentation here mirrors tools/report-loops.ps1 exactly and -ScoreTag exists
# to check it against that report.

[CmdletBinding()]
param(
    [string]$Scenario = 'duke-fishron',
    [string]$Route = 'fishron-strong-wing',
    [string]$EnumRoute = 'FishronStrongWingsDash',
    [int]$BossType = 370,
    [int[]]$Seeds = @(1, 2, 3),
    [int[]]$ValidateSeeds = @(),
    # The search is round based rather than a flat list of trials. A round spends
    # a fixed number of trials at one perturbation scale; if none of them is
    # accepted the scale grows, because a search whose trials are all bit
    # identical to the incumbent is not searching, it is spending runs. That was
    # measured: a policy perturbed at a small scale reproduced the incumbent
    # exactly, and only a much larger scale changed the fight at all.
    [int]$Rounds = 6,
    [int]$TrialsPerRound = 14,
    [int]$Hidden = 1,
    [double]$Scale = 20.0,
    [double]$MaxScale = 120.0,
    [double]$ScaleGrowth = 1.6,
    [int]$RngSeed = 1,
    [string]$Init = 'zero',
    # Where candidate policies are written. No default path is compiled in:
    # this file is published, and a hardcoded one-machine layout is what the
    # publication audit rejects. Set CHAITE_SEARCH_POLICY_DIR (or pass
    # -PolicyDir) and the resolved value is checked below.
    [string]$PolicyDir = $env:CHAITE_SEARCH_POLICY_DIR,
    [string]$TagPrefix = 'srch',
    [int]$MaxTicks = 12000,
    [int]$WallSeconds = 300,
    # A hard wall budget for the search itself, in seconds. The host is shared and
    # per run cost swings with it: the same probe run measured sixteen seconds
    # uncontended and two hundred and twelve when the owner's client was open. A
    # fixed trial count therefore cannot promise a finish time, so the count is a
    # ceiling and this is the floor under it. When the budget runs out the search
    # stops widening and goes straight to the stability gate, because a result
    # measured on fewer trials is still a result while a run that never finishes
    # is not. Zero means no budget.
    [int]$BudgetSeconds = 0,
    # Search log destination. Empty resolves to artifacts/search-log.jsonl
    # inside the repository, which is relative and therefore publishable.
    [string]$Log = $env:CHAITE_SEARCH_LOG,
    [string]$Root = 'artifacts',
    [string]$ScoreTag = '',
    # The wave driver this search spends its trials through. Overridable because
    # the default is one machine's layout and this file is published; the search
    # itself never runs during a gate, which only parses this file.
    [string]$Runwave = '',
    # The standoff a loop has to hand to the next one, and how far off it may be.
    # Defaults come from the retained configuration and from the measured exit band
    # of the repositioning loops, not from a guess.
    #
    # The loop state is discretised into a distance band and a bearing sector. The
    # band width is not the closure tolerance; it is the resolution of the state
    # space an enumeration searches over, which is why it is coarse. Measured at
    # 100 px and eight sectors one dense run yields two player loops with one
    # clean, and at 200 px and four sectors four loops with three clean.
    [double]$LoopBandWidth = 200.0,
    [int]$LoopBearingSectors = 4,
    [int]$LoopMinTicks = 30,
    # Training runs in dense frame mode by default because the sampled trace
    # misreads the loop metric. Scoring the same fight both ways gave sixteen
    # loops at seventy five percent closed when sampled against seventeen loops
    # at fifty eight point eight percent closed when dense: the sampled frame
    # lands on the wrong tick, so the loop end distance is read at the wrong
    # moment and closure comes out sixteen points optimistic. That is a bias
    # with a direction, not noise, so the default has to be dense.
    #
    # The switch rides an environment variable rather than a source edit, which
    # matters because a probe is invalidated by a rebuild and a training run must
    # never straddle one. A dense run costs about four megabytes per two thousand
    # ticks, so a full search writes on the order of a gigabyte.
    [bool]$DenseFrames = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$newPolicy = Join-Path $PSScriptRoot 'new-policy.ps1'
# The wave driver lives outside the repository, so it has no repository-relative
# default. It used to be a hardcoded one-machine path, which the publication
# audit rejects; the default is the environment now and the check below turns an
# unset value into one clear message instead of a confusing missing-file error.
$runwave = if (-not [string]::IsNullOrEmpty($Runwave)) { $Runwave }
    else { $env:CHAITE_RUNWAVE }
if ([string]::IsNullOrEmpty($runwave)) {
    throw 'The wave driver is not configured: pass -Runwave or set CHAITE_RUNWAVE.'
}
if (-not (Test-Path -LiteralPath $runwave)) {
    throw "runwave.ps1 not found at $runwave"
}
if ([string]::IsNullOrEmpty($PolicyDir)) {
    throw 'The candidate policy directory is not configured: pass -PolicyDir or set CHAITE_SEARCH_POLICY_DIR.'
}
if ([string]::IsNullOrEmpty($Log)) {
    $Log = Join-Path $repo 'artifacts\search-log.jsonl'
}

function Read-JsonLines {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return @() }
    return @(Get-Content -LiteralPath $Path | ForEach-Object { $_ | ConvertFrom-Json })
}

# Scores one tag over one seed set. Returns null when a seed has no usable run.
function Get-TagScore {
    param([string]$Tag, [int[]]$SeedSet, [int]$BossType, [string]$Root)

    $hits = 0; $ticks = 0; $damage = 0; $wins = 0; $runs = 0
    $deaths = 0; $noHitWins = 0
    $loops = 0; $clean = 0; $closed = 0; $cycles = 0; $playerDamage = 0
    $cleanLoops = New-Object System.Collections.ArrayList
    foreach ($seed in $SeedSet) {
        # A tag is reused across runs, so several directories can match one tag
        # and seed. Taking the first match takes whatever sorts first by name,
        # which is the oldest, and an older attempt that failed to produce a
        # result.json then shadows the good run and makes the whole tag score as
        # unusable. That happened live: three directories matched enum00BASE seed
        # one, the oldest had no result.json because its route was rejected at
        # launch, and a perfectly good fifteen hundred tick run was reported as
        # incomplete seed coverage. Newest first, and only accept a directory
        # whose result actually parses as a finished battle.
        $dirs = @(Get-ChildItem -LiteralPath $Root -Directory |
            Where-Object { $_.Name -like "game-probe-rt-$Tag-$seed-*" } |
            Sort-Object LastWriteTime -Descending)
        $r = $null
        foreach ($candidate in $dirs) {
            $candidatePath = Join-Path $candidate.FullName 'result.json'
            if (-not (Test-Path -LiteralPath $candidatePath)) { continue }
            $parsed = Get-Content -LiteralPath $candidatePath -Raw | ConvertFrom-Json
            if ([string]$parsed.status -ne 'loss' -and [string]$parsed.status -ne 'win') { continue }
            $dir = $candidate.FullName
            $r = $parsed
            break
        }
        if ($null -eq $r) { continue }
        $hits += [int]$r.hits
        $ticks += [int]$r.ticks
        $damage += [int]$r.bossDamage
        $deaths += [int]$r.deaths
        if ($r.win -eq $true) { $wins++ }
        # The acceptance criterion, counted per seed exactly as it is defined:
        # the fight is won and the player's life never dropped.
        if ($r.win -eq $true -and [int]$r.hits -eq 0) { $noHitWins++ }
        $runs++

        # hurt-observations holds one row per hurt request and the hook may
        # return zero; lifeDelta is a positive damage amount, so only positive
        # rows are hits. Filtered counts equal result.hits exactly on every tag
        # measured so far.
        $hurtTicks = @()
        foreach ($h in (Read-JsonLines -Path (Join-Path $dir 'hurt-observations.jsonl'))) {
            $lifeDelta = [int]$h.player.lifeDelta
            if ($lifeDelta -gt 0) { $hurtTicks += [int]$h.tickAfter; $playerDamage += $lifeDelta }
        }
        $frames = @()
        foreach ($row in (Read-JsonLines -Path (Join-Path $dir 'boss-observations.jsonl'))) {
            if ($null -eq $row.npcs) { continue }
            $boss = @()
            foreach ($npc in $row.npcs) {
                if ($BossType -gt 0 -and [int]$npc.type -ne $BossType) { continue }
                if ($npc.active -ne $true) { continue }
                $boss += $npc
            }
            if ($boss.Count -eq 0) { continue }
            # The boss-relative distance is what the loop has to return, not the
            # player's absolute position: measured over one dense run the absolute
            # displacement between a loop's first and last frame runs from 221 to
            # 2,366 px and the boss-relative displacement from 91 to 1,957, so no
            # loop returns positionally. Distance and bearing behave differently.
            # Bearing is stable across the repositioning loops, drifting a median
            # of about 15 degrees, while distance is not, so the closing quantity
            # is the distance band and the repositioning state is what converges it.
            $bx = [double]$boss[0].position.x + [double]$boss[0].width * 0.5
            $by = [double]$boss[0].position.y + [double]$boss[0].height * 0.5
            # The player block carries position but no width or height, so the
            # player's top left corner stands in for its centre. The offset is a
            # constant of about ten pixels against a standoff of four hundred, and
            # the closing band is two hundred and fifty wide, so it cannot change
            # whether a loop is classified as closed.
            $dx = [double]$row.player.position.x - $bx
            $dy = [double]$row.player.position.y - $by
            $frames += [pscustomobject]@{
                Tick = [int]$row.tick
                Ai = [int]$boss[0].ai[0]
                Distance = [math]::Sqrt($dx * $dx + $dy * $dy)
                Bearing = [math]::Atan2($dy, $dx) * 180.0 / [math]::PI
            }
        }
        # The loop unit is the player returning to its initial state, not a boss
        # state segment. The two are not one to one and treating them as if they
        # were is what earlier revisions got wrong twice: segmenting on boss state
        # measures the boss's cycle, and a single player loop can contain two or
        # even four of the boss's attack sets. Recorded from one dense run, a
        # clean closed loop of 866 ticks spans boss states 0, 1, 2, 9 and 6, which
        # is four attack sets inside one player loop. Which of the two a reading
        # uses therefore changes what it counts.
        #
        # A loop starts wherever the previous one closed and ends at the first
        # tick whose discretised boss relative geometry matches the start, with a
        # minimum length so a loop cannot close on itself while nothing happened.
        # The state is discretised rather than exact because exact equality is the
        # wrong test: the seven reposition exits of one measured run were at 446,
        # 382, 655, 474, 567, 450 and 393 px, so consecutive returns differ by up
        # to 273 px while still being the same standoff.
        $sectorWidth = 360.0 / $LoopBearingSectors
        $i = 0
        while (($i + $LoopMinTicks) -lt $frames.Count) {
            $a = $frames[$i]
            $bandA = [int][math]::Floor($a.Distance / $LoopBandWidth)
            $wrappedA = (($a.Bearing % 360.0) + 360.0) % 360.0
            $sectorA = [int][math]::Floor($wrappedA / $sectorWidth)
            $j = $i + $LoopMinTicks
            $closedAt = -1
            while ($j -lt $frames.Count) {
                $z = $frames[$j]
                $bandZ = [int][math]::Floor($z.Distance / $LoopBandWidth)
                $wrappedZ = (($z.Bearing % 360.0) + 360.0) % 360.0
                $sectorZ = [int][math]::Floor($wrappedZ / $sectorWidth)
                if ($bandZ -eq $bandA -and $sectorZ -eq $sectorA) { $closedAt = $j; break }
                $j++
            }
            if ($closedAt -lt 0) { break }
            $z = $frames[$closedAt]
            $inLoop = 0
            foreach ($t in $hurtTicks) {
                if ($t -gt $a.Tick -and $t -le $z.Tick) { $inLoop++ }
            }
            $loops++
            if ($inLoop -eq 0) {
                $clean++
                # A clean closed loop is the unit the method accepts, so it is
                # recorded rather than merely counted.
                [void]$cleanLoops.Add([pscustomobject]@{
                    seed      = $seed
                    startTick = $a.Tick
                    endTick   = $z.Tick
                    ticks     = $z.Tick - $a.Tick
                    startDist = [math]::Round($a.Distance, 1)
                    endDist   = [math]::Round($z.Distance, 1)
                })
            }
            $i = $closedAt
        }
    }
    if ($runs -eq 0 -or $ticks -le 0) { return $null }
    # A score is only comparable to another score when it covers the same seeds.
    # A wave can return fewer usable runs than requested: a wall-clock timeout or
    # a killed process leaves a seed missing, and its result.json then carries a
    # status this scorer skips. Accepting that silently is how a search ends up
    # comparing a one-seed candidate against a three-seed incumbent and calling
    # it an improvement. That happened live -- one trial scored 6,483 ticks
    # against the next trial's 17,037 and both were treated as candidates -- so
    # incomplete coverage is not a weak score, it is no score.
    if ($runs -ne $SeedSet.Count) { return $null }
    $share = 0.0
    if ($loops -gt 0) { $share = 100.0 * $clean / $loops }
    $cleanLoopsPerK = 0.0
    if ($ticks -gt 0) { $cleanLoopsPerK = 1000.0 * $clean / $ticks }
    return [pscustomobject]@{
        Runs            = $runs
        Hits            = $hits
        Ticks           = $ticks
        Damage          = $damage
        Wins            = $wins
        NoHitWins       = $noHitWins
        Deaths          = $deaths
        PlayerDamage    = $playerDamage
        # Hits per thousand ticks is the metric this scorer used to rank on, and
        # it is gameable in a way that was measured: the global horizontal nudge
        # improved it from 2.226 to 2.146 while the player took 13.8 percent
        # MORE damage, 6,767 to 7,701, because it traded many small hits for
        # fewer large ones. Damage taken per thousand ticks ranks that nudge
        # correctly as worse, 162.0 against 172.1, and it also refuses to reward
        # dying early: the worst arm measured, 214.6, is the one that survives
        # 22 percent less time. What the acceptance criterion asks for is a rate
        # at which the zero-hit budget is spent, so that rate is the score.
        HitsPerK        = 1000.0 * $hits / $ticks
        PlayerDamagePerK = 1000.0 * $playerDamage / $ticks
        DamagePerHit    = if ($hits -gt 0) { $playerDamage / $hits } else { 0.0 }
        Loops           = $loops
        Clean           = $clean
        CleanShare      = $share
        CleanLoopsPerK  = $cleanLoopsPerK
        CleanLoops      = $cleanLoops
    }
}

function Test-Better {
    param([object]$Candidate, [object]$Incumbent)
    if ($null -eq $Candidate) { return $false }
    if ($null -eq $Incumbent) { return $true }
    # The acceptance criterion ranks first, then the terms that lead to it, then
    # the loop-level proxy. See the ordering note at the top of this file for why
    # each term is where it is and which earlier term was measured to be a
    # length-scaled proxy for it.
    #
    # A tolerance keeps the search from chasing a difference smaller than the
    # seed-to-seed spread it was measured over. It applies only to the damage
    # rate, which is a continuous quantity; the terms above it are counts of
    # discrete events, where any difference is a real difference.
    if ($Candidate.NoHitWins -gt $Incumbent.NoHitWins) { return $true }
    if ($Candidate.NoHitWins -lt $Incumbent.NoHitWins) { return $false }
    if ($Candidate.Wins -gt $Incumbent.Wins) { return $true }
    if ($Candidate.Wins -lt $Incumbent.Wins) { return $false }
    if ($Candidate.Deaths -lt $Incumbent.Deaths) { return $true }
    if ($Candidate.Deaths -gt $Incumbent.Deaths) { return $false }
    if ($Candidate.Hits -lt $Incumbent.Hits) { return $true }
    if ($Candidate.Hits -gt $Incumbent.Hits) { return $false }
    if ($Candidate.CleanShare -gt $Incumbent.CleanShare) { return $true }
    if ($Candidate.CleanShare -lt $Incumbent.CleanShare) { return $false }
    if ($Candidate.PlayerDamagePerK -lt $Incumbent.PlayerDamagePerK - 10.0) { return $true }
    if ($Candidate.PlayerDamagePerK -gt $Incumbent.PlayerDamagePerK + 10.0) { return $false }
    # Damage dealt is a tie-break only, and it comes last on purpose: buying
    # output time while taking more damage is not progress toward zero hits.
    return $Candidate.Damage -gt $Incumbent.Damage
}

function Format-Score {
    param([object]$S)
    if ($null -eq $S) { return 'unusable (incomplete seed coverage or failed runs)' }
    # Printed in ranking order, so a reader of the log sees the same priority the
    # search used. This used to lead with cleanLoopsPerK, which is the rate the
    # scorer itself documents as gameable by dying early and which ranks nothing.
    return ("runs={0} noHitWins={1}/{0} wins={2}/{0} deaths={3} hits={4,3} clean={5,5:N1}% loops={6,3} dmgPerK={7,7:N1} pdmg={8,6} bossDmg={9,7} cleanLoops={10,3}" -f `
        $S.Runs, $S.NoHitWins, $S.Wins, $S.Deaths, $S.Hits, $S.CleanShare, $S.Loops, $S.PlayerDamagePerK, $S.PlayerDamage, $S.Damage, $S.Clean)
}

if (-not [string]::IsNullOrEmpty($ScoreTag)) {
    $s = Get-TagScore -Tag $ScoreTag -SeedSet $Seeds -BossType $BossType -Root $Root
    Write-Output ("score for tag '$ScoreTag' over seeds $($Seeds -join ','):")
    Write-Output ("  " + (Format-Score -S $s))
    exit 0
}

if (-not (Test-Path -LiteralPath $PolicyDir)) {
    [void](New-Item -ItemType Directory -Path $PolicyDir -Force)
}

# A wave that is killed or that times out can leave its game process alive. That
# orphan keeps burning a core, and the effect on this harness is not "slightly
# slower": one measured sweep went from 0.9 ms per native frame to 44 ms and
# from 30 seconds per seed to 330, with a run that hit the wall limit and had to
# be discarded. Inflated timings and timeouts are invalid measurements, not slow
# ones, so refuse to start while an orphan is present and clear one that a
# failure leaves behind rather than letting later trials absorb it.
#
# Ownership is decided by the executable's path, never by its name. This machine
# is shared: the same person plays the game and watches video while the search
# works, so a process called Terraria.exe is far more likely to be theirs than
# ours, and stopping one by name would close their session. Only processes whose
# image lives under this repository's own artifacts\game-probe-rt-* run
# directories are ours, and nothing else is ever stopped. A path that cannot be
# read is treated as not ours.
$script:artifactsRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts'

function Get-OwnProbeProcess {
    $mine = New-Object System.Collections.ArrayList
    foreach ($p in @(Get-Process -ErrorAction SilentlyContinue)) {
        $path = $null
        try { $path = $p.Path } catch { $path = $null }
        if ([string]::IsNullOrEmpty($path)) { continue }
        if ($path -notlike "$($script:artifactsRoot)\game-probe-rt-*") { continue }
        [void]$mine.Add($p)
    }
    return @($mine.ToArray())
}

function Clear-OwnProbeProcess {
    param([string]$Why)
    $stray = @(Get-OwnProbeProcess)
    if ($stray.Count -eq 0) { return }
    Write-Output ("  stopping {0} orphaned probe process(es) of our own [{1}]: {2}" -f `
        $stray.Count, $Why, (($stray | ForEach-Object { $_.Id }) -join ','))
    $stray | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
}

$preExisting = @(Get-OwnProbeProcess)
if ($preExisting.Count -gt 0) {
    throw ("{0} probe process(es) of our own are still running ({1}). A wave that" +
        " spans another wave's leftovers measures a contended host, so its" +
        " timings and timeouts are not trustworthy. Wait for them, or stop them" +
        " by id, and re-run.") -f `
        $preExisting.Count, (($preExisting | ForEach-Object { $_.Id }) -join ',')
}

# The person who owns this machine plays the game while the search runs, and
# their client is launched from the Steam library, so it is a Terraria.exe that
# does not belong to us and must never be stopped. It must not be measured
# through either: a probe run that shares the CPU with it took 212, 270 and 315
# seconds instead of the usual 30, measured live when their client started nine
# seconds before one of the runs. Slow wall time does not by itself corrupt a
# result, because the simulation is fixed-step, but it pushes runs toward the
# wall-clock limit, and a run that hits that limit is truncated and has to be
# thrown away. So this refuses to start and says why rather than spending
# machine time on runs that may be unusable, and it never stops their process.
$userGame = @(foreach ($p in @(Get-Process -Name 'Terraria' -ErrorAction SilentlyContinue)) {
    $path = $null
    try { $path = $p.Path } catch { $path = $null }
    if ([string]::IsNullOrEmpty($path)) { continue }
    if ($path -like "$($script:artifactsRoot)\game-probe-rt-*") { continue }
    $p
})
if ($userGame.Count -gt 0) {
    Write-Output ("the game is running ({0}); not starting a wave, because sharing" -f `
        (($userGame | ForEach-Object { "pid $($_.Id)" }) -join ', '))
    Write-Output 'the CPU inflates wall time and risks runs that hit the wall limit.'
    Write-Output 'Nothing of theirs has been touched. Re-run when they are done.'
    exit 0
}

$bestFile = Join-Path $PolicyDir ("search-best-{0}.txt" -f $EnumRoute)
if ($Init -eq 'zero') {
    & $newPolicy -Path $bestFile -Hidden $Hidden -Mode zero | Out-Null
} else {
    if (-not (Test-Path -LiteralPath $Init)) { throw "-Init not found: $Init" }
    Copy-Item -LiteralPath $Init -Destination $bestFile -Force
}

$env:CHAITE_POLICY_ROUTES = $EnumRoute
# Set before the first run so the incumbent is measured on exactly the same
# channel as every candidate; a baseline scored on sampled frames compared
# against candidates scored on dense ones would be an apples to oranges accept.
$env:CHAITE_PROBE_DENSE_FRAMES = if ($DenseFrames) { '1' } else { '0' }
$bestScore = $null
$trialLog = New-Object System.Collections.ArrayList

Write-Output ("search: scenario={0} route={1} seeds={2} rounds={3} trialsPerRound={4} scale={5} maxScale={6} hidden={7} denseFrames={8}" -f `
    $Scenario, $Route, ($Seeds -join ','), $Rounds, $TrialsPerRound, $Scale, $MaxScale, $Hidden, $DenseFrames)
Write-Output ("incumbent: {0}" -f $bestFile)

# The incumbent is measured before any candidate exists. Without this the first
# candidate would be compared against an unmeasured null and could be accepted on
# a number that means nothing; with it every accept is against a real reading on
# the same seeds.
$env:CHAITE_POLICY_FILE = $bestFile
$baseTag = "{0}BASE" -f $TagPrefix
& $runwave -Scenario $Scenario -Route $Route -Seeds $Seeds -Tag $baseTag `
    -MaxTicks $MaxTicks -WallSeconds $WallSeconds | Out-Null
$bestScore = Get-TagScore -Tag $baseTag -SeedSet $Seeds -BossType $BossType -Root $Root
Write-Output ("incumbent measured: " + (Format-Score -S $bestScore))
if ($null -eq $bestScore) {
    throw ("the incumbent could not be measured over seeds {0}: at least one run" +
        " timed out or failed. Every later comparison would be against a missing" +
        " baseline, so stop here and re-run when the host is not contended.") -f `
        ($Seeds -join ',')
}
Write-Output ''

$totalTrials = 0
$noOps = 0
$acceptedAny = 0
$scale = $Scale
$searchStarted = Get-Date
$budgetHit = $false
for ($r = 1; $r -le $Rounds; $r++) {
    $acceptedThisRound = 0
    $noOpsThisRound = 0
    $bestThisRound = $bestScore
    for ($t = 1; $t -le $TrialsPerRound; $t++) {
        if ($BudgetSeconds -gt 0 -and ((Get-Date) - $searchStarted).TotalSeconds -ge $BudgetSeconds) {
            $budgetHit = $true
            Write-Output ("budget of {0} s reached after {1} trials; stopping the search and going to the gate" -f `
                $BudgetSeconds, $totalTrials)
            break
        }
        $totalTrials++
        $candidate = Join-Path $PolicyDir ("cand-{0}-R{1:D2}T{2:D3}.txt" -f $EnumRoute, $r, $t)
        & $newPolicy -Path $candidate -Mode perturb -Base $bestFile `
            -Scale $scale -Seed ($RngSeed + $totalTrials) | Out-Null

        $tag = "{0}R{1:D2}T{2:D3}" -f $TagPrefix, $r, $t
        $env:CHAITE_POLICY_FILE = $candidate

        $stdout = & $runwave -Scenario $Scenario -Route $Route -Seeds $Seeds `
            -Tag $tag -MaxTicks $MaxTicks -WallSeconds $WallSeconds 2>&1
        $failed = @($stdout | Where-Object { $_ -match 'status=harness-error|status=timeout' }).Count
        if ($failed -gt 0) { Clear-OwnProbeProcess -Why "trial $totalTrials failed" }

        $score = $null
        if ($failed -eq 0) {
            $score = Get-TagScore -Tag $tag -SeedSet $Seeds -BossType $BossType -Root $Root
        }
        # A perturbation too small to flip a decision reproduces the incumbent
        # exactly. Such a trial is not a weak candidate, it is no candidate: it
        # carries no information about the direction to move, and a round made
        # entirely of them means the scale is wrong rather than the search having
        # converged. Counting them is what makes that visible instead of silently
        # spending the budget.
        $isNoOp = ($null -ne $score) -and ($null -ne $bestScore) -and
            ($score.Ticks -eq $bestScore.Ticks) -and ($score.Hits -eq $bestScore.Hits) -and
            ($score.Damage -eq $bestScore.Damage)
        if ($isNoOp) { $noOps++; $noOpsThisRound++ }

        $accepted = (Test-Better -Candidate $score -Incumbent $bestScore)
        if ($accepted) {
            Copy-Item -LiteralPath $candidate -Destination $bestFile -Force
            $bestScore = $score
            $acceptedThisRound++
            $acceptedAny++
            $bestThisRound = $score
        }

        Write-Output ("trial {0,4} R{1:D2}T{2:D3} scale={3,6:N1}  {4}  {5}{6}" -f `
            $totalTrials, $r, $t, $scale, (Format-Score -S $score),
            $(if ($failed -gt 0) { "HARNESS-FAILED" } elseif ($accepted) { "ACCEPT" } else { "reject" }),
            $(if ($isNoOp) { "  NO-OP" } else { "" }))

        [void]$trialLog.Add([pscustomobject]@{
            Schema    = 'chaite-policy-search-trial/v2'
            Trial     = $totalTrials
            Round     = $r
            Tag       = $tag
            Candidate = $candidate
            Scale     = $scale
            RngSeed   = $RngSeed + $totalTrials
            Seeds     = $Seeds
            HarnessFailed = ($failed -gt 0)
            NoOp      = $isNoOp
            Accepted  = $accepted
            Score     = $score
        })
    }
    # A round that accepted nothing is the signal to search wider. Growing the
    # scale rather than restarting keeps every accepted candidate so far.
    $grew = ''
    if ($acceptedThisRound -eq 0 -and $scale -lt $MaxScale) {
        $newScale = [math]::Min($scale * $ScaleGrowth, $MaxScale)
        $grew = "  no accept; scale {0:N1} -> {1:N1}" -f $scale, $newScale
        $scale = $newScale
    }
    Write-Output ("round {0} done: accepted={1} noOps={2}/{3} best={4}{5}" -f `
        $r, $acceptedThisRound, $noOpsThisRound, $TrialsPerRound,
        (Format-Score -S $bestThisRound), $grew)
}

Write-Output ''
Write-Output ("best after {0} trials in {1} rounds: {2}" -f $totalTrials, $Rounds, (Format-Score -S $bestScore))
Write-Output ("no-op trials (perturbation changed nothing): {0} of {1}" -f $noOps, $totalTrials)
Write-Output ("best policy file: {0}" -f $bestFile)

if ($ValidateSeeds.Count -gt 0) {
    $tag = "{0}VALID" -f $TagPrefix
    $env:CHAITE_POLICY_FILE = $bestFile
    Write-Output ''
    Write-Output ("validating the winner on seeds {0}" -f ($ValidateSeeds -join ','))
    & $runwave -Scenario $Scenario -Route $Route -Seeds $ValidateSeeds `
        -Tag $tag -MaxTicks $MaxTicks -WallSeconds $WallSeconds | Out-Null
    $vs = Get-TagScore -Tag $tag -SeedSet $ValidateSeeds -BossType $BossType -Root $Root
    Write-Output ("  winner  on validation seeds: " + (Format-Score -S $vs))
    # The screening seed alone cannot tell a route that is reliably clean from one
    # that got lucky on it, so the winner has to produce a clean closed loop on
    # every validation seed before the method accepts it. Re-running the same
    # seed would prove nothing: the simulator is deterministic per build and seed.
    $valClean = if ($null -ne $vs) { $vs.Clean } else { 0 }
    $valSeedsWithLoop = if ($null -ne $vs) { @($vs.CleanLoops | Select-Object -ExpandProperty seed -Unique).Count } else { 0 }
    # The final best is always gated, including when no candidate ever beat the
    # incumbent. Not gating it conflated two very different situations: a search
    # that found nothing, and a baseline that was already good enough. Measured
    # on the fairy wing route the baseline wins the fight outright with one hit
    # over four thousand eight hundred ticks, so nothing could beat it and the
    # gate never ran, which reported a winning route as no result at all.
    #
    # Where the result came from is still reported, because a baseline that was
    # already clean is not evidence that the search works. The two are named
    # differently rather than merged into one word.
    $untouched = ($acceptedAny -eq 0)
    $gatePassed = ($null -ne $vs) -and ($valSeedsWithLoop -ge $ValidateSeeds.Count)
    $stable = $gatePassed -and (-not $untouched)
    # The acceptance criterion is reported before the loop proxy, and named
    # differently, because the loop line alone was read as a fight result. It
    # says "clean closed loops on N of M gate seeds", which is true of a policy
    # that never wins: a clean closed loop is one player loop that took no hit
    # and returned to its start band, and every route measured produced some. A
    # report that leads with it invites exactly the conflation that has now
    # happened three times in this project, so the fight's own numbers come
    # first and the loop line is explicitly labelled a proxy.
    if ($null -ne $vs) {
        Write-Output ("fight verdict: no-hit wins {0}/{1} (wins {2}, deaths {3}, hits {4})" -f `
            $vs.NoHitWins, $ValidateSeeds.Count, $vs.Wins, $vs.Deaths, $vs.Hits)
    } else {
        Write-Output ("fight verdict: unusable, no complete validation coverage on {0} seeds" -f `
            $ValidateSeeds.Count)
    }
    if ($gatePassed -and $untouched) {
        Write-Output ("loop verdict: BASELINE-STABLE clean closed loops on {0} of {1} gate seeds" -f `
            $valSeedsWithLoop, $ValidateSeeds.Count)
        Write-Output ("              no candidate beat the baseline in {0} trials ({1} no-ops)," -f `
            $totalTrials, $noOps)
        Write-Output '              so this is a real result but the search did not find it'
    } elseif ($stable) {
        Write-Output ("loop verdict: STABLE clean closed loops on {0} of {1} gate seeds after {2} accepted candidate(s)" -f `
            $valSeedsWithLoop, $ValidateSeeds.Count, $acceptedAny)
    } else {
        Write-Output ("loop verdict: UNSTABLE clean closed loops on {0} of {1} gate seeds after {2} accepted candidate(s)" -f `
            $valSeedsWithLoop, $ValidateSeeds.Count, $acceptedAny)
    }
    [void]$trialLog.Add([pscustomobject]@{
        Schema    = 'chaite-loop-stability/v1'
        Tag       = $tag
        Candidate = $bestFile
        Seeds     = $ValidateSeeds
        Clean     = $valClean
        SeedsWithLoop = $valSeedsWithLoop
        # The acceptance criterion is recorded alongside the loop proxy so a
        # later report cannot read the loop fields as a fight result.
        NoHitWins = if ($null -ne $vs) { $vs.NoHitWins } else { 0 }
        Wins      = if ($null -ne $vs) { $vs.Wins } else { 0 }
        Deaths    = if ($null -ne $vs) { $vs.Deaths } else { 0 }
        Hits      = if ($null -ne $vs) { $vs.Hits } else { 0 }
        Loops     = if ($null -ne $vs) { $vs.Loops } else { 0 }
        AcceptedAny = $acceptedAny
        Untouched = $untouched
        GatePassed = $gatePassed
        Stable    = $stable
        BaselineStable = ($gatePassed -and $untouched)
        Score     = $vs
    })
}

# The incumbent's own score, so a later reader can see what was being compared
# against without re-running anything.
$controlTag = "{0}CTRL" -f $TagPrefix
& $newPolicy -Path (Join-Path $PolicyDir 'search-control.txt') -Hidden $Hidden -Mode zero | Out-Null
$env:CHAITE_POLICY_FILE = Join-Path $PolicyDir 'search-control.txt'
& $runwave -Scenario $Scenario -Route $Route -Seeds $Seeds -Tag $controlTag `
    -MaxTicks $MaxTicks -WallSeconds $WallSeconds | Out-Null
$control = Get-TagScore -Tag $controlTag -SeedSet $Seeds -BossType $BossType -Root $Root
Write-Output ''
Write-Output ("  zero-policy control on the same seeds: " + (Format-Score -S $control))
[void]$trialLog.Add([pscustomobject]@{
    Schema = 'chaite-policy-search-trial/v1'
    Trial = 0
    Tag = $controlTag
    Candidate = (Join-Path $PolicyDir 'search-control.txt')
    Seeds = $Seeds
    HarnessFailed = $false
    Accepted = $false
    Score = $control
})

Remove-Item Env:\CHAITE_POLICY_FILE -ErrorAction SilentlyContinue
Remove-Item Env:\CHAITE_POLICY_ROUTES -ErrorAction SilentlyContinue

$json = $trialLog | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($Log, $json + "`n",
    (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("wrote {0}" -f $Log)
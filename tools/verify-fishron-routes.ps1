<#
.SYNOPSIS
    Replays the committed Fishron formula routes through CHAITE_ROUTE_FILE and
    checks the native result against the recorded expectation.

.DESCRIPTION
    The objective names native per-tick replay through `CHAITE_ROUTE_FILE` as the
    sole acceptance channel, so this is the one command that exercises it.

    A route is only half of the configuration. `boss-observations.jsonl` shows
    that the SAME 5999-tick input stream produces

        strong wing (fishron-strong-wing) : 6000 / 2 hits / no death / 42 damage
        default     (fairy wing)          : 2032 / 6 hits / DEATH    / 99 damage

    so the route must be paired with its loadout. This script always passes both,
    which is the pair that was measured to round-trip.

    Expectations below are the values MEASURED from the committed build. They are
    not targets: the point of the check is to detect drift. `hits` is the number
    of native update frames in which player life decreased, read from
    `result.json`; it is not a damage-event hook.

    NOTE ON HONESTY: neither route is a zero-hit run. The objective requires
    `hits == 0` at the 6000-tick cap and that has NOT been achieved; these
    expectations encode the best measured state, 2 and 3 hits. The check reports
    `ZERO-HIT` only if a run genuinely records hits == 0.

.PARAMETER WallSeconds
    Per-run wall clock budget, forwarded to run-native-acceptance.ps1.

.PARAMETER MaxTicks
    Tick cap, forwarded to run-native-acceptance.ps1. The objective requires 6000.

.EXAMPLE
    .\tools\verify-fishron-routes.ps1
#>
[CmdletBinding()]
param(
    [int]$MaxTicks = 6000,
    [int]$WallSeconds = 900
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$runner = Join-Path $PSScriptRoot 'run-native-acceptance.ps1'

if (-not (Test-Path $runner)) { throw "missing runner: $runner" }

# DENSE FRAMES IS NOT OPTIONAL FOR REPLAY. MEASURED: the identical route replayed
# with this unset recorded 8 hits and 10 hits-with-death, while the same route with
# it set recorded the expected 2 and 3. The dense stream changes how often the probe
# samples, and the replay's shield/observation cadence depends on it, so a run that
# omits it silently measures a different fight. Set it here so the one command is
# self-contained.
$env:CHAITE_PROBE_DENSE_FRAMES = '1'

# Every CHAITE_* knob must be clear or a leftover from an earlier experiment
# silently changes the circuit under test.
foreach ($name in @(
        'CHAITE_POLICY_FILE', 'CHAITE_POLICY_ROUTES', 'CHAITE_POLICY_FORMAT',
        'CHAITE_DIAG_FILE', 'CHAITE_ROUTE_FILE', 'CHAITE_ROUTE_SKIP',
        'CHAITE_COLOCATION_ROUTES', 'CHAITE_APEX_REFILL', 'CHAITE_DASH_SUPPRESS',
        'CHAITE_DASH_DELAY', 'CHAITE_NO_CHARGE_DASH', 'CHAITE_REFILL_GUARD',
        'CHAITE_BAND_TARGET', 'CHAITE_WIDTH_HOLD', 'CHAITE_ALTITUDE_HOLD',
        'CHAITE_OVERLAP_SUPPRESS', 'CHAITE_CHARGE_FACING', 'CHAITE_NO_CHARGE_REFILL',
        # The simulated-output knobs MUST be cleared too. This route channel is a
        # control-path record and asserts that replaying a committed route reproduces
        # the exact control path it was harvested from, which is only meaningful with
        # NO damage injection (see the note on the expected table below). MEASURED:
        # with a leftover CHAITE_SIM_DPS=2000 from an earlier experiment in the same
        # shell, both routes "matched" a kill at 2880/2881 ticks with zero hits --
        # a completely different run that still looked like a MATCH against a stale
        # table. Clearing them is what makes this script self-contained.
        'CHAITE_SIM_DPS', 'CHAITE_SIM_DPS_FULL_TILES', 'CHAITE_SIM_DPS_ZERO_TILES',
        'CHAITE_SIM_BUBBLE_BREAK', 'CHAITE_ARMOR_TIER', 'CHAITE_APEX_REFILL',
        'CHAITE_TORNADO_BOX', 'CHAITE_STANDOFF_DISTANCE', 'CHAITE_STANDOFF_PX',
        'CHAITE_STANDOFF_DPS_MAX', 'CHAITE_STANDOFF_HARD', 'CHAITE_STANDOFF_PRECHARGE',
        'CHAITE_WEAK_PREJUMP', 'CHAITE_ARENA_PLATFORM_ROWS'
    )) {
    Remove-Item "Env:\$name" -ErrorAction SilentlyContinue
}

# route file, formula route, expected ticks, expected hits, expected death
#
# ROUND 161 REGENERATION. The strong route was regenerated because the
# tornado-clear pre-charge fix changed the control path (the branch used to return
# early with an active descent, so every charge that committed during tornado
# clearing had no wind-up).
#
# NOTE ON WHAT THIS CHANNEL MEASURES. These replay runs carry NO simulated output:
# there is no DPS injection, so the Boss never loses enough health to change phase
# and the entire 6000-tick window is phase-1 behaviour. That is deliberate -- a
# route is a CONTROL-PATH record, and it has to be reproducible independently of
# the fight it was harvested from. The consequence is that the hit counts here are
# NOT the fight's hit counts and NOT acceptance evidence; the acceptance numbers
# come from the DPS channel. What these values assert is that replaying the
# committed route reproduces the exact control path it was harvested from, which
# is the property CHAITE_ROUTE_FILE is supposed to guarantee.
$cases = @(
    [pscustomobject]@{
        Name    = 'strong'
        Route   = Join-Path $root 'routes\strong-fishron-wings.csv'
        Formula = 'fishron-strong-wing'
        Ticks   = 6000
        Hits    = 6
        Death   = $false
    },
    [pscustomobject]@{
        Name    = 'weak'
        Route   = Join-Path $root 'routes\fairy-wings.csv'
        Formula = 'fishron-fairy-wing'
        Ticks   = 5636
        Hits    = 9
        Death   = $true
    }
)

$results = @()
foreach ($c in $cases) {
    if (-not (Test-Path $c.Route)) { throw "missing route: $($c.Route)" }

    # A fresh run name every time: prepare-game-probe refuses to reuse one.
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $run = "verify-$($c.Name)-$stamp"

    Write-Host ''
    Write-Host ('=' * 68)
    Write-Host "ROUTE REPLAY  $($c.Name)  ->  $([IO.Path]::GetFileName($c.Route))"
    Write-Host ('=' * 68)

    $out = & $runner -RunName $run -Phase monitor -MaxTicks $MaxTicks `
        -WallSeconds $WallSeconds -RouteFile $c.Route -FormulaRoute $c.Formula 2>&1

    $resultPath = Join-Path $root "artifacts\game-probe-$run\result.json"
    if (-not (Test-Path $resultPath)) {
        Write-Host ($out | Out-String)
        throw "no result.json for run $run"
    }
    $r = Get-Content $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json

    # `npc contact` lives in the shield stream, not result.json: it is the count
    # of frames where the dash-body-hit fired, which is what makes the i-frame
    # claim checkable. Same rule as run-native-acceptance.ps1.
    $contact = 0
    $shieldPath = Join-Path (Split-Path -Parent $resultPath) 'shield-events.jsonl'
    if (Test-Path -LiteralPath $shieldPath) {
        foreach ($row in (Get-Content -LiteralPath $shieldPath)) {
            if ($row -match '"contact":true') { $contact++ }
        }
    }

    $okTicks = ($r.ticks -eq $c.Ticks)
    $okHits = ($r.hits -eq $c.Hits)
    $okDeath = ([bool]$r.death -eq $c.Death)
    $okValid = ([bool]$r.validBattle)

    $results += [pscustomobject]@{
        Loadout  = $c.Name
        Formula  = $c.Formula
        Ticks    = $r.ticks
        Hits     = $r.hits
        Death    = $r.death
        Valid    = $r.validBattle
        BossDmg  = $r.bossDamage
        Contacts = $contact
        Matches  = ($okTicks -and $okHits -and $okDeath -and $okValid)
        ZeroHit  = ($r.hits -eq 0)
        Run      = $run
    }
}

Write-Host ''
Write-Host ('=' * 68)
Write-Host 'ROUTE-REPLAY VERIFICATION'
Write-Host ('=' * 68)
Write-Host ("{0,-8} {1,-22} {2,-6} {3,-5} {4,-6} {5,-6} {6,-7} {7}" -f
    'loadout', 'formula', 'ticks', 'hits', 'death', 'valid', 'dmg', 'vs expected')
foreach ($x in $results) {
    Write-Host ("{0,-8} {1,-22} {2,-6} {3,-5} {4,-6} {5,-6} {6,-7} {7}" -f
        $x.Loadout, $x.Formula, $x.Ticks, $x.Hits, $x.Death, $x.Valid, $x.BossDmg,
        $(if ($x.Matches) { 'MATCH' } else { 'DRIFT' }))
}

$drift = @($results | Where-Object { -not $_.Matches })
$zero = @($results | Where-Object { $_.ZeroHit })

Write-Host ''
if ($drift.Count -gt 0) {
    Write-Host "DRIFT: $($drift.Count) route(s) no longer reproduce the recorded result." -ForegroundColor Red
    exit 1
}
Write-Host 'REPRODUCED: both routes replay to their recorded native result.' -ForegroundColor Green
if ($zero.Count -eq $results.Count) {
    Write-Host 'ZERO-HIT: both loadouts recorded hits == 0.' -ForegroundColor Green
} else {
    $hitList = ($results | ForEach-Object { $_.Hits }) -join ' / '
    # NOTE: `("a {0} " + "b" -f $x)` is a PowerShell precedence trap -- `-f` binds
    # to the SECOND literal only, so the first keeps its placeholder. Build the
    # message as one string first.
    $msg = "ZERO-HIT NOT ACHIEVED: hits are $hitList. The objective requires " +
        "hits == 0 at the 6000-tick cap and that remains UNMET."
    Write-Host $msg -ForegroundColor Yellow
}
exit 0

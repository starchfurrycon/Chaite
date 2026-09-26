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
        'CHAITE_OVERLAP_SUPPRESS', 'CHAITE_CHARGE_FACING', 'CHAITE_NO_CHARGE_REFILL'
    )) {
    Remove-Item "Env:\$name" -ErrorAction SilentlyContinue
}

# route file, formula route, expected ticks, expected hits, expected death
$cases = @(
    [pscustomobject]@{
        Name    = 'strong'
        Route   = Join-Path $root 'routes\strong-fishron-wings.csv'
        Formula = 'fishron-strong-wing'
        Ticks   = 6000
        Hits    = 2
        Death   = $false
    },
    [pscustomobject]@{
        Name    = 'weak'
        Route   = Join-Path $root 'routes\fairy-wings.csv'
        Formula = 'fishron-fairy-wing'
        Ticks   = 6000
        Hits    = 3
        Death   = $false
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
    Write-Host ("ZERO-HIT NOT ACHIEVED: hits are {0}. The objective requires hits == 0 at the " +
        "6000-tick cap and that remains UNMET." -f $hitList) -ForegroundColor Yellow
}
exit 0

<#
Peek at a running training tag every N minutes and print one compact status line.

Read-only by construction. It never builds, never edits, never starts a wave and
never touches the Release output, so it cannot disturb the experiment it watches.
That constraint is the whole point: the reason progress has been invisible is
that every other action available during a run was unsafe.

Each cycle classifies the run so a bad state is obvious at a glance:
  OK                progress since the last cycle
  SLOW / STALL      no new completed runs, once or StallCycles times in a row
  GUARD-MISMATCH    build.txt differs from the live DLL: the experiment is void
  BREAKTHROUGH      a no-hit result exists, which is the actual objective
  DEAD              no training directory or no probe activity at all
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Tag,
    [int]$IntervalMinutes = 30,
    [int]$StallCycles = 2
)

$ErrorActionPreference = 'Stop'
$Root = Split-Path -Parent $PSScriptRoot
$TrainDir = Join-Path $Root "artifacts\training\$Tag"
$LogFile = Join-Path $TrainDir 'log.csv'
$WatchLog = Join-Path $TrainDir 'watch.log'
$GuardFile = Join-Path $TrainDir 'build.txt'
$LiveDll = Join-Path $Root 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'

function Write-Line([string]$text) {
    Add-Content -Path $WatchLog -Value $text -Encoding ASCII
    Write-Output $text
    [Console]::Out.Flush()
}

if (-not (Test-Path $TrainDir)) { Write-Line "no training directory for tag $Tag"; return }

$lastRuns = -1
$stall = 0
$cycle = 0

while ($true) {
    $cycle++
    try {
        $guard = 'MISSING'
        if (Test-Path $GuardFile) {
            $want = (Get-Content $GuardFile -Raw).Trim()
            $have = (Get-FileHash $LiveDll -Algorithm SHA256).Hash.Substring(0, 16)
            if ($want -eq $have) { $guard = 'OK' } else { $guard = "MISMATCH($want/$have)" }
        }

        $gen = 0; $parentLast = ''; $candScored = 0; $best = ''; $noHit = 0; $wins = 0
        if (Test-Path $LogFile) {
            foreach ($row in @(Get-Content $LogFile | Select-Object -Skip 1)) {
                if ($row -match '^(\d+),(parent|\d+),.*,(-?\d+\.?\d*),wins=(\d+),noHit=(\d+)$') {
                    $g = [int]$Matches[1]
                    if ($g -gt $gen) { $gen = $g }
                    $wins += [int]$Matches[4]
                    $noHit += [int]$Matches[5]
                    if ($Matches[2] -eq 'parent') { $parentLast = $Matches[3] }
                    else {
                        $candScored++
                        if ($best -eq '' -or [double]$Matches[3] -gt [double]$best) { $best = $Matches[3] }
                    }
                }
            }
        }

        $runs = @(Get-ChildItem (Join-Path $Root 'artifacts') -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like "game-probe-rt-$Tag-*" -and $_.Name -notlike '*desktop*' } |
            Where-Object { Test-Path (Join-Path $_.FullName 'result.json') }).Count
        $evals = @(Get-ChildItem $TrainDir -Filter 'eval-*.json' -ErrorAction SilentlyContinue).Count
        $procs = @(Get-Process -Name Terraria -ErrorAction SilentlyContinue).Count
        $accepted = Test-Path (Join-Path $TrainDir 'params-gen001.txt')

        # The first cycle has no previous sample, so it cannot judge progress.
        # DEAD deliberately needs sustained inactivity with no probe alive: a
        # single zero reading is normal, because probes are torn down and
        # rebuilt between batches and between seeds inside a batch. Crying wolf
        # on those gaps would make the verdict worthless.
        $newRuns = if ($lastRuns -lt 0) { -1 } else { $runs - $lastRuns }
        $verdict = 'OK'
        if ($guard -ne 'OK') { $verdict = 'GUARD-MISMATCH' }
        elseif ($noHit -gt 0) { $verdict = 'BREAKTHROUGH' }
        elseif ($newRuns -lt 0) { $verdict = 'BASELINE' }
        elseif ($newRuns -le 0) {
            $stall++
            if ($stall -ge $StallCycles) { $verdict = if ($procs -eq 0) { 'DEAD' } else { 'STALL' } }
            else { $verdict = 'SLOW' }
        }
        else { $stall = 0 }

        $lastRuns = $runs
        $stamp = Get-Date -Format 'MM-dd HH:mm:ss'
        Write-Line ("[{0}] cyc={1} {2} gen={3} candScored={4} best={5} parent={6} noHit={7} wins={8} runs={9} new={10} evals={11} accepted={12} guard={13} procs={14}" -f `
            $stamp, $cycle, $verdict, $gen, $candScored, $(if ($best -eq '') { 'na' } else { $best }), `
            $(if ($parentLast -eq '') { 'na' } else { $parentLast }), $noHit, $wins, $runs, $newRuns, $evals, $accepted, $guard, $procs)
    }
    catch {
        Write-Line ("[{0}] cyc={1} ERROR {2}" -f (Get-Date -Format 'MM-dd HH:mm:ss'), $cycle, $_.Exception.Message)
    }
    Start-Sleep -Seconds ($IntervalMinutes * 60)
}
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
        $hits = 0; $sigma = ''; $step = ''; $scoredInGen = 0
        if (Test-Path $LogFile) {
            foreach ($row in @(Get-Content $LogFile | Select-Object -Skip 1)) {
                # The note field gained hits= for candidates and the update rows
                # carry sigma/step, so match the prefix and read the note
                # separately rather than anchoring on the exact tail. Anchoring
                # on wins=..,noHit=.. silently stopped matching once hits= was
                # appended, which would have read as a stalled run.
                $parts = $row -split ','
                if ($parts.Count -lt 5) { continue }
                $g = 0
                if (-not [int]::TryParse($parts[0], [ref]$g)) { continue }
                if ($g -gt $gen) {
                    # Counters describe the newest generation only; totalling
                    # across the whole run would make every number grow forever
                    # and hide whether this generation is going anywhere.
                    $gen = $g; $wins = 0; $noHit = 0; $hits = 0; $scoredInGen = 0; $best = ''
                }
                if ($parts[1] -eq 'update') {
                    $sigma = if ($parts[4] -match 'sigma=([\d.]+)') { $Matches[1] } else { $sigma }
                    $step = if ($parts[4] -match 'step=([\d.]+)') { $Matches[1] } else { $step }
                    continue
                }
                if ($g -ne $gen) { continue }
                $note = $parts[4]
                if ($note -match 'wins=(\d+)') { $wins += [int]$Matches[1] }
                if ($note -match 'noHit=(\d+)') { $noHit += [int]$Matches[1] }
                if ($note -match 'hits=(\d+)') { $hits += [int]$Matches[1] }
                if ($parts[1] -eq 'parent') { $parentLast = $parts[3] }
                elseif ($note -match '^(wins=|noHit=|hits=)') {
                    $candScored++
                    $scoredInGen++
                    if ($best -eq '' -or [double]$parts[3] -gt [double]$best) { $best = $parts[3] }
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
        Write-Line ("[{0}] cyc={1} {2} gen={3} scored={4}/{5} best={6} parent={7} noHit={8} wins={9} hits={10} runs={11} new={12} evals={13} accepted={14} guard={15} procs={16} sigma={17} step={18}" -f `
            $stamp, $cycle, $verdict, $gen, $scoredInGen, $candScored, $(if ($best -eq '') { 'na' } else { $best }), `
            $(if ($parentLast -eq '') { 'na' } else { $parentLast }), $noHit, $wins, $hits, $runs, $newRuns, $evals, $accepted, $guard, $procs, `
            $(if ($sigma -eq '') { 'na' } else { $sigma }), $(if ($step -eq '') { 'na' } else { $step }))
    }
    catch {
        Write-Line ("[{0}] cyc={1} ERROR {2}" -f (Get-Date -Format 'MM-dd HH:mm:ss'), $cycle, $_.Exception.Message)
    }
    Start-Sleep -Seconds ($IntervalMinutes * 60)
}
# Chaite per-route small-model trainer.
#
# The learner owns ONLY the movement decision. The fixed state machine keeps
# route/form/mount admission, aiming, firing, weapon selection and healing.
#
# Parameters live in a plain text file read by the engine at process start, so
# a generation never recompiles anything: InputCoreSha256 stays constant for the
# whole training run, which makes every generation comparable by construction.
#
# Usage:
#   tools/train-policy.ps1 -Tag empress-v1                      # train
#   tools/train-policy.ps1 -Tag empress-v1 -Mode eval -Params <file> -Seeds 1001..1020

[CmdletBinding()]
param(
    [string]$Tag = 'empress-v1',
    [ValidateSet('train', 'eval')][string]$Mode = 'train',
    [string]$Scenario = 'empress-night',
    [string]$Route = 'empress-broom',
    [string]$PolicyRoute = 'EmpressBroom',
    [double]$BossMaxLife = 98000,
    [double]$HitPenalty = 0.5,
    [double]$HitsToDie = 14.0,
    # Hits a win may absorb before its score floors at 0.5. The objective is
    # no-hit, so this only shapes the gradient inside the win band.
    [double]$WinHitBudget = 25.0,
    [int[]]$TrainSeeds = (1..40),
    [int[]]$EvalSeeds = (1001..1020),
    [int]$Population = 16,
    [int]$SeedsPerCandidate = 4,
    [double]$Sigma = 0.10,
    [int]$MaxGenerations = 100,
    [int]$Parallel = 1,
    [int]$ValidationEvery = 5,
    [string]$Params = '',
    [int[]]$Seeds = @(),
    [string]$Runwave = 'D:\personal tasks\_chaite-tools\runwave.ps1'
)

$ErrorActionPreference = 'Stop'
$InputCount = 38
$HiddenCount = 32
$WeightCount = $HiddenCount * $InputCount + $HiddenCount + 10 * $HiddenCount + 10

$Root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$TrainDir = Join-Path $Root "artifacts\training\$Tag"
New-Item -ItemType Directory -Force -Path $TrainDir | Out-Null
$BuildFile = Join-Path $TrainDir 'build.txt'
$LogFile = Join-Path $TrainDir 'log.csv'

function Get-BuildHash {
    $dll = Join-Path $Root 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'
    (Get-FileHash $dll -Algorithm SHA256).Hash.Substring(0, 16)
}

# The build identity must not move during a training run; a change means the
# engine was rebuilt under us and every comparison across that boundary is void.
$script:BuildHash = Get-BuildHash
if (Test-Path $BuildFile) {
    $recorded = (Get-Content $BuildFile -Raw).Trim()
    if ($recorded -ne $script:BuildHash) {
        throw "Core build changed from $recorded to $($script:BuildHash): refusing to continue, because results on either side of a rebuild are not comparable."
    }
} else {
    Set-Content -Path $BuildFile -Value $script:BuildHash -Encoding ASCII
}
Write-Host "build $($script:BuildHash)  tag $Tag"

function Write-Params([string]$Path, [double[]]$Vector) {
    if ($Vector.Count -ne $WeightCount) {
        throw "expected $WeightCount weights, got $($Vector.Count)"
    }
    $parts = New-Object System.Collections.Generic.List[string]
    foreach ($v in $Vector) { $parts.Add($v.ToString('G9', [System.Globalization.CultureInfo]::InvariantCulture)) }
    $text = "chaite-policy 1`r`n$InputCount`r`n$HiddenCount`r`n" + ($parts -join ' ')
    # ASCII, no BOM: a BOM would glue itself to the magic token and break the
    # loader's format check.
    Set-Content -Path $Path -Value $text -Encoding ASCII
}

function Read-Params([string]$Path) {
    $tokens = (Get-Content $Path -Raw).Split([char[]]" `t`r`n", [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($tokens.Count -ne 4 + $WeightCount) { throw "malformed params file $Path" }
    $out = New-Object double[] $WeightCount
    for ($i = 0; $i -lt $WeightCount; $i++) {
        $out[$i] = [double]::Parse($tokens[4 + $i], [System.Globalization.CultureInfo]::InvariantCulture)
    }
    return , $out
}

function Get-ResultFor([string]$RunTag) {
    $dirs = Get-ChildItem (Join-Path $Root 'artifacts') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like "game-probe-rt-$RunTag*" }
    $rows = @()
    foreach ($d in $dirs) {
        $f = Join-Path $d.FullName 'result.json'
        if (-not (Test-Path $f)) { continue }
        $j = Get-Content $f -Raw | ConvertFrom-Json
        if ($null -eq $j.seed) { continue }
        $rows += [pscustomobject]@{
            seed = [int]$j.seed; status = $j.status
            win = ($j.win -eq $true -or $j.status -eq 'win')
            damage = [double]$j.bossDamage; hits = [double]$j.hits; ticks = [double]$j.ticks
        }
    }
    return , $rows
}

function Get-Fitness($row) {
    # The objective the user set is to take no damage at all. A win therefore
    # has to be worth strictly more than any loss, but inside a win the only
    # way to reach the maximum is to be untouched: zero hits scores 1.0, a win
    # that ate WinHitBudget hits scores 0.5, and beyond that it floors at 0.5
    # rather than dropping under a loss. Without the hit term a sloppy win and
    # a flawless one would both score 1.0 and the search would have no reason
    # to prefer the flawless one.
    if ($row.win) {
        $decay = [double]$row.hits / [double]$WinHitBudget
        if ($decay -gt 1.0) { $decay = 1.0 }
        return 0.5 + 0.5 * (1.0 - $decay)
    }
    $dealt = $row.damage / $BossMaxLife
    $penalty = $HitPenalty * ($row.hits / $HitsToDie)
    return 0.5 * $dealt - $penalty
}

# One candidate on one seed. Each candidate needs its own parameter file, so
# each evaluation is its own helper process carrying its own environment.
function Invoke-Evaluation([string]$ParamsPath, [int[]]$SeedList, [string]$RunTag) {
    if ($RunTag.Length -gt 0) { Remove-Item (Join-Path $Root 'artifacts') -Recurse -Force -ErrorAction SilentlyContinue -Filter "game-probe-rt-$RunTag*" }
    $csv = ($SeedList -join ',')
    $log = Join-Path $TrainDir "wave-$RunTag.log"
    $cmd = "`$env:CHAITE_POLICY_FILE='$ParamsPath'; `$env:CHAITE_POLICY_ROUTES='$PolicyRoute'; & '$Runwave' -Scenario $Scenario -Route $Route -Seeds $csv -Tag $RunTag *> '$log'"
    $p = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-Command', $cmd -PassThru -WindowStyle Hidden
    $p.WaitForExit()
    $rows = Get-ResultFor $RunTag
    if ($rows.Count -eq 0) { throw "evaluation $RunTag produced no results (see $log)" }
    return , $rows
}

function Measure-Candidate([string]$ParamsPath, [int[]]$SeedList, [string]$RunTag) {
    $rows = Invoke-Evaluation $ParamsPath $SeedList $RunTag
    $sum = 0.0
    $noHit = 0
    $wins = 0
    foreach ($r in $rows) {
        $sum += (Get-Fitness $r)
        if ($r.win) {
            $wins++
            if ([int]$r.hits -eq 0) { $noHit++ }
        }
    }
    # The real objective, reported alongside the scalar so the curve can be
    # read directly as no-hit wins rather than only as fitness.
    $script:LastWins = $wins
    $script:LastNoHit = $noHit
    return $sum / $rows.Count
}

function Next-Gaussian($Rng) {
    if ($null -ne $script:gaussianSpare) {
        $v = $script:gaussianSpare
        $script:gaussianSpare = $null
        return $v
    }
    $u1 = 1.0 - $Rng.NextDouble()
    $u2 = 1.0 - $Rng.NextDouble()
    $mag = [math]::Sqrt(-2.0 * [math]::Log($u1))
    $script:gaussianSpare = $mag * [math]::Sin(2.0 * [math]::PI * $u2)
    return $mag * [math]::Cos(2.0 * [math]::PI * $u2)
}

function Wilson-Lower([int]$Wins, [int]$Total) {
    if ($Total -le 0) { return 0.0 }
    $n = [double]$Total; $p = [double]$Wins / $n; $z = 1.96
    $den = 1.0 + $z * $z / $n
    $centre = ($p + $z * $z / (2.0 * $n)) / $den
    $half = $z * [math]::Sqrt($p * (1.0 - $p) / $n + $z * $z / (4.0 * $n * $n)) / $den
    $low = $centre - $half
    if ($low -lt 0.0) { return 0.0 }
    return $low
}

# ---------------------------------------------------------------- eval mode
if ($Mode -eq 'eval') {
    if ($Params.Length -eq 0) { throw '-Params is required in eval mode' }
    if ($Seeds.Count -eq 0) { $Seeds = $EvalSeeds }
    $rows = Invoke-Evaluation ((Resolve-Path $Params).Path) $Seeds "$Tag-eval"
    $wins = @($rows | Where-Object { $_.win }).Count
    $lower = Wilson-Lower $wins $rows.Count
    $json = [pscustomobject]@{
        tag = $Tag; build = $script:BuildHash; seeds = @($Seeds)
        runs = $rows.Count; wins = $wins
        rate = [math]::Round(100.0 * $wins / $rows.Count, 2)
        wilsonLower = [math]::Round(100.0 * $lower, 2)
        meanDamage = [math]::Round((($rows | Measure-Object -Property damage -Average).Average), 1)
        meanHits = [math]::Round((($rows | Measure-Object -Property hits -Average).Average), 2)
    }
    $json | ConvertTo-Json | Set-Content -Path (Join-Path $TrainDir "eval-$Tag.json") -Encoding ASCII
    $json | Format-List
    return
}

# --------------------------------------------------------------- train mode
$rng = New-Object System.Random 20260915
$mean = New-Object double[] $WeightCount
for ($i = 0; $i -lt $WeightCount; $i++) { $mean[$i] = ($rng.NextDouble() * 2.0 - 1.0) * 0.05 }
Write-Params (Join-Path $TrainDir 'params-gen000.txt') $mean
if (Test-Path $LogFile) { } else { Set-Content -Path $LogFile -Value 'gen,candidate,seeds,fitness,note' -Encoding ASCII }

$parentPath = Join-Path $TrainDir 'params-parent.txt'
$candidatePath = Join-Path $TrainDir 'params-candidate.txt'
$bestFitness = [double]::NegativeInfinity
$bestPath = Join-Path $TrainDir 'params-best.txt'

for ($gen = 1; $gen -le $MaxGenerations; $gen++) {
    # Fresh seeds every generation, identical across candidates so the
    # comparison inside a generation is fair and the policy cannot memorise one
    # fixed seed set.
    $picked = New-Object System.Collections.Generic.List[int]
    while ($picked.Count -lt $SeedsPerCandidate) {
        $s = $TrainSeeds[$rng.Next(0, $TrainSeeds.Count)]
        if (-not $picked.Contains($s)) { $picked.Add($s) }
    }
    $seedList = $picked.ToArray()
    $seedCsv = ($seedList -join ',')

    Write-Params $parentPath $mean
    $parentFitness = Measure-Candidate $parentPath $seedList "$Tag-g$gen-p"
    $parentRows = Get-ResultFor "$Tag-g$gen-p"
    $parentWins = @($parentRows | Where-Object { $_.win }).Count
    $parentNoHit = @($parentRows | Where-Object { $_.win -and ([int]$_.hits -eq 0) }).Count
    Add-Content -Path $LogFile -Value "$gen,parent,$seedCsv,$([math]::Round($parentFitness,5)),wins=$parentWins,noHit=$parentNoHit"
    Write-Host "gen $gen parent fitness $([math]::Round($parentFitness,4)) wins $parentWins/$($seedList.Count) noHit $parentNoHit"

    $bestCandidate = $null
    $bestCandidateFitness = [double]::NegativeInfinity
    $batch = New-Object System.Collections.Generic.List[object]
    $pending = New-Object System.Collections.Generic.List[object]

    for ($c = 1; $c -le $Population; $c++) {
        # Mirrored sampling: draw one Gaussian vector for each odd candidate and
        # reuse the same vector negated for the next one, so every pair brackets
        # the mean symmetrically.
        if (($c % 2) -eq 1) {
            $g = New-Object double[] $WeightCount
            for ($i = 0; $i -lt $WeightCount; $i++) { $g[$i] = Next-Gaussian $rng }
            $script:pairG = $g
            $sign = 1.0
        } else {
            $g = $script:pairG
            $sign = -1.0
        }
        $vector = New-Object double[] $WeightCount
        for ($i = 0; $i -lt $WeightCount; $i++) {
            $vector[$i] = $mean[$i] + $sign * $Sigma * $g[$i]
        }
        $path = Join-Path $TrainDir ("params-c$c.txt")
        Write-Params $path $vector
        $tagC = "$Tag-g$gen-c$c"
        $cmd = "`$env:CHAITE_POLICY_FILE='$path'; `$env:CHAITE_POLICY_ROUTES='$PolicyRoute'; & '$Runwave' -Scenario $Scenario -Route $Route -Seeds $seedCsv -Tag $tagC *> '$(Join-Path $TrainDir "wave-$tagC.log")'"
        $proc = Start-Process -FilePath 'powershell.exe' -ArgumentList '-NoProfile', '-Command', $cmd -PassThru -WindowStyle Hidden
        $pending.Add([pscustomobject]@{ c = $c; proc = $proc; tag = $tagC })
        if ($pending.Count -ge $Parallel) {
            foreach ($item in $pending) { $item.proc.WaitForExit() }
            foreach ($item in $pending) { $batch.Add($item) }
            $pending.Clear()
        }
    }
    foreach ($item in $pending) { $item.proc.WaitForExit(); $batch.Add($item) }

    foreach ($item in $batch) {
        $rows = Get-ResultFor $item.tag
        if ($rows.Count -eq 0) { Add-Content -Path $LogFile -Value "$gen,$($item.c),$seedCsv,,no-result"; continue }
        $sum = 0.0
        $wins = @($rows | Where-Object { $_.win }).Count
        # Computed from this candidate's own rows. Measure-Candidate is not in
        # this path, so its script-scoped counters would have reported the
        # parent's numbers here.
        $noHit = @($rows | Where-Object { $_.win -and ([int]$_.hits -eq 0) }).Count
        $hitsum = [int](($rows | Measure-Object -Property hits -Sum).Sum)
        foreach ($r in $rows) { $sum += (Get-Fitness $r) }
        $fit = $sum / $rows.Count
        Add-Content -Path $LogFile -Value "$gen,$($item.c),$seedCsv,$([math]::Round($fit,5)),wins=$wins,noHit=$noHit,hits=$hitsum"
        if ($fit -gt $bestCandidateFitness) { $bestCandidateFitness = $fit; $bestCandidate = $item.c }
    }

    if ($bestCandidate -ne $null -and $bestCandidateFitness -gt $parentFitness) {
        $mean = Read-Params (Join-Path $TrainDir "params-c$bestCandidate.txt")
        Write-Params (Join-Path $TrainDir 'params-parent.txt') $mean
        $Sigma = [math]::Min(0.5, $Sigma * 1.2)
        Write-Host "  accepted candidate $bestCandidate ($([math]::Round($bestCandidateFitness,4)) > $([math]::Round($parentFitness,4))), sigma $([math]::Round($Sigma,4))"
    } else {
        $Sigma = [math]::Max(0.01, $Sigma * 0.85)
        Write-Host "  no improvement, sigma $([math]::Round($Sigma,4))"
    }

    if ($parentFitness -gt $bestFitness) {
        $bestFitness = $parentFitness
        Write-Params $bestPath $mean
    }
    Set-Content -Path (Join-Path $TrainDir ("params-gen{0:D3}.txt" -f $gen)) -Value (Get-Content (Join-Path $TrainDir 'params-parent.txt') -Raw) -Encoding ASCII

    if ($ValidationEvery -gt 0 -and ($gen % $ValidationEvery) -eq 0) {
        Write-Host "  validating on held-out seeds..."
        & $MyInvocation.MyCommand.Path -Mode eval -Tag "$Tag-gen$gen" -Params $bestPath -Seeds $EvalSeeds -Scenario $Scenario -Route $Route -PolicyRoute $PolicyRoute -Runwave $Runwave -Parallel $Parallel
    }
}

Write-Host "training finished; best fitness $([math]::Round($bestFitness,4)) in $bestPath"
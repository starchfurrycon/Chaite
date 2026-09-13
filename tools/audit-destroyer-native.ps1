[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$RunDirectory
)

# Read-only post-audit for one completed classic Destroyer P1 native run. It
# never prepares, launches or controls Terraria and never writes to evidence.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$rapidReversalTicks = 60
$lowWingThreshold = 32.0
$recoveredWingThreshold = 90.0
$maximumLowWingToRecoveryTicks = 120
$maximumLowWingRestoreTicks = 600
$maximumRecoveryEpisodeTicks = 600

$zeroAxisPhases = @(
    'classic-acquire-anchor',
    'classic-acquire-anchor-no-safe-support',
    'classic-reacquire-anchor',
    'classic-reacquire-anchor-airborne-closed',
    'classic-reacquire-anchor-no-safe-support',
    'classic-supported-pressure-brake',
    'classic-head-exit-committed-blocked',
    'classic-recover-anchor-route-blocked',
    'classic-recover-anchor-recharge',
    'classic-recover-anchor-complete'
)
$movingPhases = @(
    'classic-supported-pressure',
    'classic-supported-probe-pressure',
    'classic-head-exit-committed',
    'classic-recover-anchor'
)
$allowedPhases = @($zeroAxisPhases + $movingPhases)

function Reject-DestroyerAudit([string]$Message) {
    throw "Destroyer native audit rejected: $Message"
}

function Test-StrictChild([string]$Candidate, [string]$Parent) {
    $prefix = $Parent.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    return $Candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparseChain([string]$FullPath, [string]$Context) {
    $candidate = [IO.Path]::GetFullPath($FullPath)
    $root = [IO.Path]::GetPathRoot($candidate)
    if ([string]::IsNullOrWhiteSpace($root)) { Reject-DestroyerAudit "$Context has no filesystem root." }
    $cursor = $root
    $parts = @($candidate.Substring($root.Length) -split '[\\/]+' | Where-Object { $_.Length -gt 0 })
    foreach ($part in @('') + $parts) {
        if ($part.Length -gt 0) { $cursor = Join-Path $cursor $part }
        $entry = Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Reject-DestroyerAudit "$Context traverses a reparse point: $cursor"
        }
    }
}

function Require-Object($Value, [string]$Context) {
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { Reject-DestroyerAudit "$Context must be a JSON object." }
    return $Value
}

function Read-Field($Object, [string]$Name, [string]$Context, [switch]$AllowNull) {
    $null = Require-Object $Object $Context
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { Reject-DestroyerAudit "$Context is missing field '$Name'." }
    if (-not $AllowNull -and $null -eq $property.Value) { Reject-DestroyerAudit "$Context.$Name must not be null." }
    return ,$property.Value
}

function Require-String($Value, [string]$Context) {
    if ($Value -isnot [string]) { Reject-DestroyerAudit "$Context must be a JSON string." }
    return [string]$Value
}

function Require-Boolean($Value, [string]$Context) {
    if ($Value -isnot [bool]) { Reject-DestroyerAudit "$Context must be a JSON boolean." }
    return [bool]$Value
}

function Require-Integer($Value, [long]$Minimum, [long]$Maximum, [string]$Context) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) {
        Reject-DestroyerAudit "$Context must be an integer in $Minimum..$Maximum."
    }
    return [long]$Value
}

function Require-Number($Value, [double]$Minimum, [double]$Maximum, [string]$Context) {
    if ($null -eq $Value -or $Value -is [bool] -or $Value -is [string] -or $Value -is [char]) {
        Reject-DestroyerAudit "$Context must be a finite JSON number."
    }
    try { $number = [Convert]::ToDouble($Value, [Globalization.CultureInfo]::InvariantCulture) }
    catch { Reject-DestroyerAudit "$Context must be a finite JSON number." }
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt $Minimum -or $number -gt $Maximum) {
        Reject-DestroyerAudit "$Context must be a finite JSON number in $Minimum..$Maximum."
    }
    return $number
}

function Require-Array($Value, [string]$Context) {
    if ($Value -isnot [Array]) { Reject-DestroyerAudit "$Context must be a JSON array." }
    return ,$Value
}

function Get-CheckedFile([string]$Directory, [string]$Name, [long]$MaximumBytes, [switch]$Optional) {
    $path = [IO.Path]::GetFullPath((Join-Path $Directory $Name))
    if (-not (Test-StrictChild $path $Directory)) { Reject-DestroyerAudit "file '$Name' escaped the run directory." }
    Assert-NoReparseChain $path "file '$Name'"
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        if ($Optional) { return $null }
        Reject-DestroyerAudit "required file '$Name' is missing."
    }
    $item = Get-Item -LiteralPath $path -Force
    if ($item.PSIsContainer -or $item.Length -gt $MaximumBytes) { Reject-DestroyerAudit "file '$Name' is not a bounded regular file." }
    return $item
}

function Read-Utf8([IO.FileInfo]$File, [string]$Context) {
    try { $text = [IO.File]::ReadAllText($File.FullName, [Text.UTF8Encoding]::new($false, $true)) }
    catch { Reject-DestroyerAudit "$Context is not readable strict UTF-8." }
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    return $text
}

function Convert-JsonObject([string]$Text, [string]$Context) {
    if ([string]::IsNullOrWhiteSpace($Text)) { Reject-DestroyerAudit "$Context is empty." }
    try { $value = $Text | ConvertFrom-Json -ErrorAction Stop }
    catch { Reject-DestroyerAudit "$Context is invalid JSON." }
    return Require-Object $value $Context
}

function Read-Jsonl([IO.FileInfo]$File, [string]$Context) {
    $raw = Read-Utf8 $File $Context
    $reader = [IO.StringReader]::new($raw)
    $lines = [Collections.Generic.List[string]]::new()
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ([string]::IsNullOrWhiteSpace($line)) { Reject-DestroyerAudit "$Context contains an empty row." }
            $lines.Add($line)
        }
    } finally { $reader.Dispose() }
    if ($lines.Count -eq 0) { Reject-DestroyerAudit "$Context contains no rows." }
    return [pscustomobject]@{ Raw = $raw; Lines = @($lines.ToArray()) }
}

function Measure-Buffer($Lines) {
    [long]$written = 0; [long]$buffered = 0; [long]$maximum = 0
    [int]$rows = 0; [int]$flushes = 0
    foreach ($line in @($Lines)) {
        $serialized = [long]$line.Length + [Environment]::NewLine.Length
        if ($buffered -gt 0 -and $buffered + $serialized -gt 65536) {
            $written += $buffered; $buffered = 0; $rows = 0; $flushes++
        }
        $buffered += $serialized; $rows++; $maximum = [Math]::Max($maximum, $buffered)
        if ($rows -ge 16 -or $buffered -ge 65536) {
            $written += $buffered; $buffered = 0; $rows = 0; $flushes++
        }
    }
    if ($buffered -gt 0) { $written += $buffered; $buffered = 0; $rows = 0; $flushes++ }
    return [pscustomobject]@{ Flushes = $flushes; Maximum = $maximum; Written = $written; RemainingRows = $rows }
}

function Assert-ExactInteger($Object, [string]$Name, [long]$Expected, [string]$Context) {
    $actual = Require-Integer (Read-Field $Object $Name $Context) 0 2147483647 "$Context.$Name"
    if ($actual -ne $Expected) { Reject-DestroyerAudit "$Context.$Name must equal $Expected, observed $actual." }
}

function Assert-Buffer($Summary, $EvidenceInput, [string]$Context) {
    Assert-ExactInteger $Summary 'bufferFlushRows' 16 $Context
    Assert-ExactInteger $Summary 'bufferFlushCharacters' 65536 $Context
    $replay = Measure-Buffer $EvidenceInput.Lines
    $flushes = Require-Integer (Read-Field $Summary 'flushes' $Context) 0 2048 "$Context.flushes"
    $maximum = Require-Integer (Read-Field $Summary 'maximumBufferedCharacters' $Context) 0 131072 "$Context.maximumBufferedCharacters"
    $written = Require-Integer (Read-Field $Summary 'charactersWritten' $Context) 0 33554432 "$Context.charactersWritten"
    $remaining = Require-Integer (Read-Field $Summary 'bufferedRowsAfterFinalFlush' $Context) 0 2048 "$Context.bufferedRowsAfterFinalFlush"
    if ($EvidenceInput.Raw.Length -ne $replay.Written -or $flushes -ne $replay.Flushes -or
        $maximum -ne $replay.Maximum -or $written -ne $replay.Written -or $remaining -ne $replay.RemainingRows) {
        Reject-DestroyerAudit "$Context flush/buffer counters disagree with physical JSONL."
    }
}

if ([string]::IsNullOrWhiteSpace($RunDirectory) -or -not [IO.Path]::IsPathRooted($RunDirectory)) {
    Reject-DestroyerAudit '-RunDirectory must be one explicit absolute directory.'
}
if (-not (Test-Path -LiteralPath $artifactsRoot -PathType Container)) { Reject-DestroyerAudit 'project artifacts directory is absent.' }
$runFull = [IO.Path]::GetFullPath($RunDirectory)
if (-not (Test-StrictChild $runFull $artifactsRoot)) { Reject-DestroyerAudit '-RunDirectory must be strictly inside project artifacts.' }
Assert-NoReparseChain $artifactsRoot 'artifacts root'
Assert-NoReparseChain $runFull 'run directory'
if (-not (Test-Path -LiteralPath $runFull -PathType Container)) { Reject-DestroyerAudit 'run directory does not exist.' }
$resolvedArtifacts = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $artifactsRoot).Path)
$resolvedRun = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $runFull).Path)
if (-not (Test-StrictChild $resolvedRun $resolvedArtifacts)) { Reject-DestroyerAudit 'resolved run directory escaped artifacts.' }

$resultFile = Get-CheckedFile $resolvedRun 'result.json' 1MB
$result = Convert-JsonObject (Read-Utf8 $resultFile 'result.json') 'result.json'
foreach ($expected in @(
    @{ Name = 'schema'; Value = 'chaite-boss-result/v1' }, @{ Name = 'scenario'; Value = 'destroyer' },
    @{ Name = 'difficulty'; Value = 'classic' }, @{ Name = 'status'; Value = 'win' },
    @{ Name = 'outcome'; Value = 'SuccessNoDeath' }
)) {
    $actual = Require-String (Read-Field $result $expected.Name 'result.json') "result.$($expected.Name)"
    if ($actual -cne $expected.Value) { Reject-DestroyerAudit "result.$($expected.Name) must equal '$($expected.Value)', observed '$actual'." }
}
Assert-ExactInteger $result 'schemaVersion' 1 'result.json'
Assert-ExactInteger $result 'difficultyCode' 0 'result.json'
Assert-ExactInteger $result 'processExitCode' 0 'result.json'
foreach ($name in @('win','validBattle','battleStarted','allExpectedBossesSeen')) {
    if (-not (Require-Boolean (Read-Field $result $name 'result.json') "result.$name")) { Reject-DestroyerAudit "result.$name must be true." }
}
if (Require-Boolean (Read-Field $result 'death' 'result.json') 'result.death') { Reject-DestroyerAudit 'result records a death.' }
Assert-ExactInteger $result 'deaths' 0 'result.json'
$minimumLife = Require-Integer (Read-Field $result 'minLife' 'result.json') 0 2147483647 'result.minLife'
if ($minimumLife -lt 40) { Reject-DestroyerAudit "minimum life $minimumLife is below 40." }
$rootDamage = Require-Integer (Read-Field $result 'bossDamage' 'result.json') 0 2147483647 'result.bossDamage'
if ($rootDamage -lt 80000) { Reject-DestroyerAudit "Destroyer root damage $rootDamage is below 80000." }
$resultTicks = Require-Integer (Read-Field $result 'ticks' 'result.json') 0 24000 'result.ticks'
$resultFrames = Require-Integer (Read-Field $result 'nativeFrames' 'result.json') 0 24000 'result.nativeFrames'
$roots = Require-Array (Read-Field $result 'firstObservedBosses' 'result.json') 'result.firstObservedBosses'
if ($roots.Count -ne 1) { Reject-DestroyerAudit 'firstObservedBosses must contain exactly one Destroyer root.' }
$root = Require-Object $roots[0] 'firstObservedBosses[0]'
if ((Require-Integer (Read-Field $root 'type' 'firstObservedBosses[0]') 0 2147483647 'root.type') -ne 134 -or
    (Require-Integer (Read-Field $root 'lifeMax' 'firstObservedBosses[0]') 1 2147483647 'root.lifeMax') -ne 80000) {
    Reject-DestroyerAudit 'classic Destroyer root must be type 134 with lifeMax 80000.'
}

$summary = Require-Object (Read-Field $result 'diagnostics' 'result.json') 'result.diagnostics'
if ((Require-String (Read-Field $summary 'schema' 'diagnostics') 'diagnostics.schema') -cne 'chaite-boss-observation-summary/v1') {
    Reject-DestroyerAudit 'unsupported observation summary schema.'
}
if ((Require-String (Read-Field $summary 'file' 'diagnostics') 'diagnostics.file') -cne 'boss-observations.jsonl') {
    Reject-DestroyerAudit 'observation summary must name boss-observations.jsonl.'
}
foreach ($pair in @(
    @{ Name = 'maximumRows'; Value = 2048 }, @{ Name = 'reservedTerminalRows'; Value = 1 },
    @{ Name = 'maximumNpcsPerRow'; Value = 48 }, @{ Name = 'periodicTicks'; Value = 60 },
    @{ Name = 'edgeMinimumTicks'; Value = 15 }
)) { Assert-ExactInteger $summary $pair.Name $pair.Value 'diagnostics' }
foreach ($name in @('droppedRows','maximumOmittedNpcs','totalOmittedNpcs','unpairedApplyObservations')) {
    Assert-ExactInteger $summary $name 0 'diagnostics'
}
if (Require-Boolean (Read-Field $summary 'applyPending' 'diagnostics') 'diagnostics.applyPending') { Reject-DestroyerAudit 'ApplyPlan remained pending.' }
$summaryCalls = Require-Integer (Read-Field $summary 'applyCalls' 'diagnostics') 1 2147483647 'diagnostics.applyCalls'
$summaryReturns = Require-Integer (Read-Field $summary 'applyReturns' 'diagnostics') 0 2147483647 'diagnostics.applyReturns'
if ($summaryCalls -ne $summaryReturns) { Reject-DestroyerAudit 'ApplyPlan calls/returns are not one-to-one.' }

$observationFile = Get-CheckedFile $resolvedRun 'boss-observations.jsonl' 32MB
$observationInput = Read-Jsonl $observationFile 'boss-observations.jsonl'
$lines = @($observationInput.Lines)
$declaredRows = Require-Integer (Read-Field $summary 'rows' 'diagnostics') 1 2048 'diagnostics.rows'
if ($lines.Count -ne $declaredRows) { Reject-DestroyerAudit 'observation row count disagrees with diagnostics.rows.' }

$eligible = [Collections.Generic.List[object]]::new()
$lastTick = -1L; $lastFrame = -1L; $lastCalls = -1L
$periodicRows = 0; $transitionRows = 0; $terminalRows = 0; $planRows = 0; $actualRows = 0
for ($index = 0; $index -lt $lines.Count; $index++) {
    $row = Convert-JsonObject $lines[$index] "observation row $($index + 1)"
    if ((Require-String (Read-Field $row 'schema' 'observation') 'observation.schema') -cne 'chaite-boss-observation/v1') { Reject-DestroyerAudit "observation row $($index + 1) has wrong schema." }
    $tick = Require-Integer (Read-Field $row 'tick' 'observation') 0 24000 'observation.tick'
    $frame = Require-Integer (Read-Field $row 'nativeFrames' 'observation') 0 24000 'observation.nativeFrames'
    if ($tick -le $lastTick -or $frame -le $lastFrame) { Reject-DestroyerAudit 'observation tick/nativeFrames must be strictly increasing.' }
    if ($tick -ne $frame) { Reject-DestroyerAudit "tick/nativeFrames disagree at $tick." }
    $lastTick = $tick; $lastFrame = $frame
    $mask = Require-Integer (Read-Field $row 'reasonMask' 'observation') 0 63 'observation.reasonMask'
    $null = Require-Integer (Read-Field $row 'transitionsSincePreviousRow' 'observation') 0 2147483647 'observation.transitionsSincePreviousRow'
    if (($mask -band 1) -ne 0) { $periodicRows++ }
    if (($mask -band 30) -ne 0) { $transitionRows++ }
    if (($mask -band 32) -ne 0) { $terminalRows++; if ($index -ne $lines.Count - 1) { Reject-DestroyerAudit 'only final observation may be terminal.' } }
    $state = Require-String (Read-Field $row 'sessionState' 'observation') 'observation.sessionState'
    if (Require-Boolean (Read-Field $row 'applyPending' 'observation') 'observation.applyPending') { Reject-DestroyerAudit "row $tick has pending ApplyPlan." }
    $calls = Require-Integer (Read-Field $row 'applyCalls' 'observation') 0 2147483647 'observation.applyCalls'
    $returns = Require-Integer (Read-Field $row 'applyReturns' 'observation') 0 2147483647 'observation.applyReturns'
    if ($calls -ne $returns -or $calls -lt $lastCalls) { Reject-DestroyerAudit "row $tick has inconsistent ApplyPlan counters." }
    $lastCalls = $calls
    if ((Require-Integer (Read-Field $row 'omittedNpcs' 'observation') 0 2147483647 'observation.omittedNpcs') -ne 0) { Reject-DestroyerAudit "row $tick omitted NPCs." }
    $null = Require-Array (Read-Field $row 'npcs' 'observation') 'observation.npcs'
    $player = Require-Object (Read-Field $row 'player' 'observation') 'observation.player'
    $dead = Require-Boolean (Read-Field $player 'dead' 'observation.player') 'player.dead'
    $wing = Require-Number (Read-Field $player 'wingTime' 'observation.player') 0 142 'player.wingTime'
    $wingMax = Require-Number (Read-Field $player 'wingTimeMax' 'observation.player') 0 1000000 'player.wingTimeMax'
    $wingsLogic = Require-Integer (Read-Field $player 'wingsLogic' 'observation.player') 0 2147483647 'player.wingsLogic'
    $null = Require-Integer (Read-Field $player 'life' 'observation.player') -2147483648 2147483647 'player.life'
    $plan = Read-Field $row 'plan' 'observation' -AllowNull
    $actual = Read-Field $row 'actualAtApplyReturn' 'observation' -AllowNull
    if ($null -ne $plan) { $null = Require-Object $plan 'observation.plan'; $planRows++ }
    if ($null -ne $actual) { $null = Require-Object $actual 'observation.actual'; $actualRows++ }
    if (($null -eq $plan) -ne ($null -eq $actual)) { Reject-DestroyerAudit "plan/actual presence differs at $tick." }
    if ($state -ceq 'EngagedAlive' -and -not $dead) {
        if ($null -eq $plan -or $null -eq $actual) { Reject-DestroyerAudit "engaged/alive row $tick lacks plan/actual." }
        if ($wingMax -ne 100 -or $wingsLogic -ne 1) { Reject-DestroyerAudit "row $tick does not expose the reviewed Demon-wing proxy (wingTimeMax=100, wingsLogic=1)." }
        $eligible.Add([pscustomobject]@{ Tick = $tick; Wing = $wing; Plan = $plan; Actual = $actual })
    }
}
if ($eligible.Count -eq 0) { Reject-DestroyerAudit 'no EngagedAlive && !dead proxy rows exist.' }
if ($terminalRows -ne 1 -or $lastTick -ne $resultTicks -or $lastFrame -ne $resultFrames) { Reject-DestroyerAudit 'terminal observation/result counters are inconsistent.' }
if ($lastCalls -ne $summaryCalls) { Reject-DestroyerAudit 'final ApplyPlan counters disagree with summary.' }
foreach ($counter in @(
    @{ Name = 'periodicRows'; Value = $periodicRows }, @{ Name = 'transitionRows'; Value = $transitionRows },
    @{ Name = 'terminalRows'; Value = $terminalRows }, @{ Name = 'planRows'; Value = $planRows },
    @{ Name = 'actualControlRows'; Value = $actualRows }
)) {
    $actualCounter = Require-Integer (Read-Field $summary $counter.Name 'diagnostics') 0 2048 "diagnostics.$($counter.Name)"
    if ($actualCounter -ne $counter.Value) { Reject-DestroyerAudit "diagnostics.$($counter.Name) disagrees with physical rows." }
}
Assert-Buffer $summary $observationInput 'diagnostics'

$actions = [Collections.Generic.List[object]]::new()
foreach ($entry in $eligible) {
    $plan = $entry.Plan; $actual = $entry.Actual; $tick = $entry.Tick
    $planTick = Require-Integer (Read-Field $plan 'tick' 'plan') 0 24000 'plan.tick'
    $actualTick = Require-Integer (Read-Field $actual 'tick' 'actual') 0 24000 'actual.tick'
    if ($planTick -ne $tick -or $actualTick -ne $tick) { Reject-DestroyerAudit "plan/actual tick differs at $tick." }
    if ((Require-String (Read-Field $plan 'strategy' 'plan') 'plan.strategy') -cne 'destroyer') { Reject-DestroyerAudit "row $tick is not Destroyer strategy." }
    $phase = Require-String (Read-Field $plan 'phase' 'plan') 'plan.phase'
    if ($phase.Contains('unsupported') -or $allowedPhases -cnotcontains $phase) { Reject-DestroyerAudit "unsupported/unreviewed Destroyer phase '$phase' at $tick." }
    $horizontal = Require-Integer (Read-Field $plan 'horizontal' 'plan') -1 1 'plan.horizontal'
    $jump = Require-Boolean (Read-Field $plan 'jump' 'plan') 'plan.jump'
    $jumpAction = Require-String (Read-Field $plan 'jumpAction' 'plan') 'plan.jumpAction'
    $drop = Require-Boolean (Read-Field $plan 'drop' 'plan') 'plan.drop'
    $dash = Require-Boolean (Read-Field $plan 'dash' 'plan') 'plan.dash'
    $hook = Require-Boolean (Read-Field $plan 'hook' 'plan') 'plan.hook'
    $mount = Require-Boolean (Read-Field $plan 'toggleMount' 'plan') 'plan.toggleMount'
    $gravity = Require-Integer (Read-Field $plan 'gravityControl' 'plan') -1 1 'plan.gravityControl'
    $left = Require-Boolean (Read-Field $actual 'controlLeft' 'actual') 'actual.controlLeft'
    $right = Require-Boolean (Read-Field $actual 'controlRight' 'actual') 'actual.controlRight'
    if ($left -ne ($horizontal -eq -1) -or $right -ne ($horizontal -eq 1)) { Reject-DestroyerAudit "returned horizontal controls contradict plan at $tick." }
    if ($drop -or $dash -or $hook -or $mount -or $gravity -ne 0) { Reject-DestroyerAudit "OwnsMovementClosure proxy was bypassed by a mobility tool at $tick." }
    $zeroAxis = $zeroAxisPhases -ccontains $phase
    if ($zeroAxis) {
        if ($horizontal -ne 0 -or $jump -or $jumpAction -cne 'Release') { Reject-DestroyerAudit "zero-axis phase '$phase' emitted movement at $tick." }
        if (Require-Boolean (Read-Field $actual 'controlJump' 'actual') 'actual.controlJump') { Reject-DestroyerAudit "zero-axis phase '$phase' returned jump control at $tick." }
    } else {
        if ($horizontal -eq 0) { Reject-DestroyerAudit "moving phase '$phase' emitted H=0 at $tick." }
        if ($phase.StartsWith('classic-supported-', [StringComparison]::Ordinal) -and ($jump -or $jumpAction -cne 'Release')) {
            Reject-DestroyerAudit "supported phase emitted vertical movement at $tick."
        }
        if ($jump -and $jumpAction -cnotin @('Default','Hold')) { Reject-DestroyerAudit "moving jump action is inconsistent at $tick." }
    }
    $actions.Add([pscustomobject]@{ Tick = $tick; Wing = $entry.Wing; Phase = $phase; H = [int]$horizontal; Zero = $zeroAxis })
}

$directionChanges = [Collections.Generic.List[object]]::new()
$headExitDirection = 0
$recoverDirection = 0
foreach ($action in $actions) {
    if ($action.H -ne 0 -and ($directionChanges.Count -eq 0 -or $directionChanges[$directionChanges.Count - 1].H -ne $action.H)) {
        $directionChanges.Add([pscustomobject]@{ Tick = $action.Tick; H = $action.H })
        if ($directionChanges.Count -ge 3) {
            $a = $directionChanges[$directionChanges.Count - 3]; $b = $directionChanges[$directionChanges.Count - 2]; $c = $directionChanges[$directionChanges.Count - 1]
            if ($a.H -eq $c.H -and $a.H -eq -$b.H -and $c.Tick - $a.Tick -le $rapidReversalTicks) {
                Reject-DestroyerAudit "recorded a,-a,a reversal spans only $($c.Tick - $a.Tick) ticks."
            }
        }
    }
    if ($action.Phase.StartsWith('classic-head-exit-', [StringComparison]::Ordinal)) {
        if ($action.H -ne 0) {
            if ($headExitDirection -eq 0) { $headExitDirection = $action.H }
            elseif ($headExitDirection -ne $action.H) { Reject-DestroyerAudit "head-exit reversed its recorded committed side at $($action.Tick)." }
        }
    } else { $headExitDirection = 0 }
    if ($action.Phase.StartsWith('classic-recover-anchor', [StringComparison]::Ordinal)) {
        if ($action.H -ne 0) {
            if ($recoverDirection -eq 0) { $recoverDirection = $action.H }
            elseif ($recoverDirection -ne $action.H) { Reject-DestroyerAudit "recover-anchor left its recorded return direction at $($action.Tick)." }
        }
    } else { $recoverDirection = 0 }
}

$lowStart = $null; $lowRecoveryEntry = $null; $recoveryStart = $null
$maximumObservedLowEntry = 0L; $maximumObservedLowRestore = 0L; $maximumObservedRecovery = 0L
foreach ($action in $actions) {
    $isRecovery = $action.Phase.StartsWith('classic-recover-anchor', [StringComparison]::Ordinal) -or
        $action.Phase.StartsWith('classic-reacquire-anchor', [StringComparison]::Ordinal) -or
        $action.Phase.StartsWith('classic-acquire-anchor', [StringComparison]::Ordinal)
    $recoveryClosed = $action.Phase -ceq 'classic-recover-anchor-complete' -or
        $action.Phase.StartsWith('classic-supported-', [StringComparison]::Ordinal)

    if ($null -eq $lowStart -and $action.Wing -le $lowWingThreshold) {
        $lowStart = $action.Tick
        if ($isRecovery) { $lowRecoveryEntry = $action.Tick }
    } elseif ($null -ne $lowStart -and $null -eq $lowRecoveryEntry -and $isRecovery) {
        $lowRecoveryEntry = $action.Tick
    }
    if ($null -ne $lowStart) {
        if ($null -eq $lowRecoveryEntry -and $action.Tick - $lowStart -gt $maximumLowWingToRecoveryTicks) {
            Reject-DestroyerAudit "low-wing proxy did not enter a recorded recovery phase within $maximumLowWingToRecoveryTicks ticks."
        }
        if ($action.Tick - $lowStart -gt $maximumLowWingRestoreTicks) {
            Reject-DestroyerAudit "low-wing proxy did not restore within $maximumLowWingRestoreTicks ticks."
        }
        if ($action.Wing -ge $recoveredWingThreshold -and $recoveryClosed) {
            if ($null -eq $lowRecoveryEntry) { Reject-DestroyerAudit 'low-wing proxy restored without a recorded recovery phase.' }
            $entryDelay = $lowRecoveryEntry - $lowStart
            if ($entryDelay -gt $maximumLowWingToRecoveryTicks) { Reject-DestroyerAudit "low-wing recovery entry took $entryDelay ticks." }
            $restoreDelay = $action.Tick - $lowStart
            $maximumObservedLowEntry = [Math]::Max($maximumObservedLowEntry, $entryDelay)
            $maximumObservedLowRestore = [Math]::Max($maximumObservedLowRestore, $restoreDelay)
            $lowStart = $null; $lowRecoveryEntry = $null
        }
    }

    $plainRecovery = $action.Phase.StartsWith('classic-recover-anchor', [StringComparison]::Ordinal) -and
        $action.Phase -cne 'classic-recover-anchor-complete'
    if ($null -eq $recoveryStart -and $plainRecovery) { $recoveryStart = $action.Tick }
    if ($null -ne $recoveryStart) {
        if ($action.Tick - $recoveryStart -gt $maximumRecoveryEpisodeTicks) { Reject-DestroyerAudit "recorded recovery episode exceeds $maximumRecoveryEpisodeTicks ticks." }
        if ($recoveryClosed) {
            $duration = $action.Tick - $recoveryStart
            $maximumObservedRecovery = [Math]::Max($maximumObservedRecovery, $duration)
            $recoveryStart = $null
        }
    }
}
if ($null -ne $lowStart) { Reject-DestroyerAudit 'proxy stream ended without bounded low-wing restoration.' }
if ($null -ne $recoveryStart) { Reject-DestroyerAudit 'proxy stream ended in an unresolved recovery episode.' }

$hurt = Require-Object (Read-Field $summary 'hurt' 'diagnostics') 'diagnostics.hurt'
if ((Require-String (Read-Field $hurt 'schema' 'diagnostics.hurt') 'hurt.schema') -cne 'chaite-hurt-observation-summary/v1') { Reject-DestroyerAudit 'unsupported Hurt summary schema.' }
$hurtCalls = Require-Integer (Read-Field $hurt 'calls' 'diagnostics.hurt') 0 2048 'hurt.calls'
$hurtReturns = Require-Integer (Read-Field $hurt 'returns' 'diagnostics.hurt') 0 2048 'hurt.returns'
$hurtRows = Require-Integer (Read-Field $hurt 'rows' 'diagnostics.hurt') 0 2048 'hurt.rows'
$hurtSerialized = Require-Integer (Read-Field $hurt 'serializedRows' 'diagnostics.hurt') 0 2048 'hurt.serializedRows'
if ($hurtCalls -ne $hurtReturns -or $hurtCalls -ne $hurtRows -or $hurtCalls -ne $hurtSerialized) { Reject-DestroyerAudit 'Hurt call/return/row counts are not one-to-one.' }
Assert-ExactInteger $hurt 'maximumRows' 2048 'diagnostics.hurt'
Assert-ExactInteger $hurt 'depthCapacity' 16 'diagnostics.hurt'
$expectedDepth = if ($hurtRows -eq 0) { 0 } else { 1 }
Assert-ExactInteger $hurt 'maximumDepth' $expectedDepth 'diagnostics.hurt'
foreach ($name in @('droppedRows','depthOverflows','unpairedObservations','pendingDepth','suppressedPendingDepth','observerErrors','sourceReadFailures','bufferedRowsAfterFinalFlush')) {
    Assert-ExactInteger $hurt $name 0 'diagnostics.hurt'
}
$hurtDeclaration = Read-Field $hurt 'file' 'diagnostics.hurt' -AllowNull
$hurtFile = Get-CheckedFile $resolvedRun 'hurt-observations.jsonl' 16MB -Optional
if ($hurtRows -eq 0) {
    if ($null -ne $hurtDeclaration -or $null -ne $hurtFile) { Reject-DestroyerAudit 'empty Hurt summary disagrees with file presence.' }
    foreach ($name in @('flushes','maximumBufferedCharacters','charactersWritten')) { Assert-ExactInteger $hurt $name 0 'diagnostics.hurt' }
    Assert-ExactInteger $hurt 'bufferFlushRows' 16 'diagnostics.hurt'; Assert-ExactInteger $hurt 'bufferFlushCharacters' 65536 'diagnostics.hurt'
} else {
    if ($hurtDeclaration -isnot [string] -or $hurtDeclaration -cne 'hurt-observations.jsonl' -or $null -eq $hurtFile) { Reject-DestroyerAudit 'nonempty Hurt summary/file is inconsistent.' }
    $hurtInput = Read-Jsonl $hurtFile 'hurt-observations.jsonl'
    if ($hurtInput.Lines.Count -ne $hurtRows) { Reject-DestroyerAudit 'Hurt row count disagrees with summary.' }
    for ($index = 0; $index -lt $hurtInput.Lines.Count; $index++) {
        $row = Convert-JsonObject $hurtInput.Lines[$index] "Hurt row $($index + 1)"
        if ((Require-String (Read-Field $row 'schema' 'Hurt row') 'Hurt.schema') -cne 'chaite-hurt-observation/v1') { Reject-DestroyerAudit "Hurt row $($index + 1) has wrong schema." }
        if ((Require-Integer (Read-Field $row 'sequence' 'Hurt row') 1 2048 'Hurt.sequence') -ne ($index + 1)) { Reject-DestroyerAudit 'Hurt sequence is duplicate/out of order.' }
        $before = Require-Integer (Read-Field $row 'tickBefore' 'Hurt row') 0 24000 'Hurt.tickBefore'
        $after = Require-Integer (Read-Field $row 'tickAfter' 'Hurt row') 0 24000 'Hurt.tickAfter'
        if ($before -ne $after) { Reject-DestroyerAudit 'Hurt observation crossed ticks.' }
        $null = Require-Object (Read-Field $row 'request' 'Hurt row') 'Hurt.request'
        $null = Require-Object (Read-Field $row 'player' 'Hurt row') 'Hurt.player'
        $null = Require-Object (Read-Field $row 'reason' 'Hurt row') 'Hurt.reason'
        $null = Require-Object (Read-Field $row 'source' 'Hurt row') 'Hurt.source'
        $null = Require-Number (Read-Field $row 'actualReturn' 'Hurt row') 0 ([double]::MaxValue) 'Hurt.actualReturn'
    }
    Assert-Buffer $hurt $hurtInput 'diagnostics.hurt'
}

[pscustomobject]@{
    Schema = 'chaite-destroyer-native-audit/v1'; Accepted = $true; RunDirectory = $resolvedRun
    Scenario = 'destroyer'; Difficulty = 'classic'; Outcome = 'SuccessNoDeath'; Deaths = 0
    MinimumLife = $minimumLife; RootDamage = $rootDamage; ObservationRows = $lines.Count
    EligibleProxyRows = $eligible.Count; HurtRows = $hurtRows; RapidReversalTicks = $rapidReversalTicks
    LowWingProxyThreshold = $lowWingThreshold; RecoveredWingProxyThreshold = $recoveredWingThreshold
    MaximumLowWingToRecoveryTicks = $maximumLowWingToRecoveryTicks; MaximumLowWingRestoreTicks = $maximumLowWingRestoreTicks
    MaximumRecoveryEpisodeTicks = $maximumRecoveryEpisodeTicks; ObservedMaximumLowWingEntryTicks = $maximumObservedLowEntry
    ObservedMaximumLowWingRestoreTicks = $maximumObservedLowRestore; ObservedMaximumRecoveryEpisodeTicks = $maximumObservedRecovery
    EvidenceScope = 'Recorded sampled/coalesced proxy rows only (60-tick periodic; selected edges have a 15-tick minimum). Horizontal changes do not trigger rows. OwnsMovementClosure, saved anchor/return direction, total wing+rocket resource and native OnGround are not serialized; phase/final-plan/wingTime checks are sampling proxies, not full-frame or hidden-state proof.'
}

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$RunDirectory
)

# Read-only post-audit for one already completed native Twins run. This script
# neither prepares nor starts Terraria, and it never writes to the run folder.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$directionReversalWindowTicks = 60
$maximumRecoveryEpisodeTicks = 600
$maximumObservationGraceTicks = 12
$maximumRouteClosedEpisodeTicks = 90
$maximumControlReturnContinuationTicks = 3
$maximumZeroWingProxySpanTicks = 180
$maximumWingRecoveryTicks = 240

function Reject-Audit([string]$Message) {
    throw "Twins native audit rejected: $Message"
}

function Test-StrictChildPath([string]$Candidate, [string]$Parent) {
    $prefix = $Parent.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) +
        [IO.Path]::DirectorySeparatorChar
    return $Candidate.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase)
}

function Assert-NoReparseChain([string]$FullPath, [string]$Context) {
    $candidate = [IO.Path]::GetFullPath($FullPath)
    $root = [IO.Path]::GetPathRoot($candidate)
    if ([string]::IsNullOrWhiteSpace($root)) { Reject-Audit "$Context has no filesystem root." }
    $cursor = $root
    $rootEntry = Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
    if ($null -ne $rootEntry -and ($rootEntry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        Reject-Audit "$Context traverses a reparse point: $cursor"
    }
    $relative = $candidate.Substring($root.Length)
    foreach ($segment in @($relative -split '[\\/]+' | Where-Object { $_.Length -gt 0 })) {
        $cursor = Join-Path $cursor $segment
        $entry = Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            Reject-Audit "$Context traverses a reparse point: $cursor"
        }
    }
}

function Require-Object($Value, [string]$Context) {
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { Reject-Audit "$Context must be a JSON object." }
    return $Value
}

function Read-Field($Object, [string]$Name, [string]$Context, [switch]$AllowNull) {
    $null = Require-Object $Object $Context
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { Reject-Audit "$Context is missing field '$Name'." }
    if (-not $AllowNull -and $null -eq $property.Value) { Reject-Audit "$Context.$Name must not be null." }
    # Prevent PowerShell's success-stream enumeration from turning a declared
    # empty JSON array into $null (or flattening a one-element array).
    return ,$property.Value
}

function Require-String($Value, [string]$Context) {
    if ($Value -isnot [string]) { Reject-Audit "$Context must be a JSON string." }
    return [string]$Value
}

function Require-Boolean($Value, [string]$Context) {
    if ($Value -isnot [bool]) { Reject-Audit "$Context must be a JSON boolean." }
    return [bool]$Value
}

function Require-Integer($Value, [long]$Minimum, [long]$Maximum, [string]$Context) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) {
        Reject-Audit "$Context must be an integer in $Minimum..$Maximum."
    }
    return [long]$Value
}

function Require-FiniteNumber($Value, [double]$Minimum, [double]$Maximum, [string]$Context) {
    if ($null -eq $Value -or $Value -is [bool] -or $Value -is [string] -or $Value -is [char]) {
        Reject-Audit "$Context must be a finite JSON number."
    }
    try { $number = [Convert]::ToDouble($Value, [Globalization.CultureInfo]::InvariantCulture) }
    catch { Reject-Audit "$Context must be a finite JSON number." }
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt $Minimum -or $number -gt $Maximum) {
        Reject-Audit "$Context must be a finite JSON number in $Minimum..$Maximum."
    }
    return $number
}

function Require-Array($Value, [string]$Context) {
    if ($Value -isnot [Array]) { Reject-Audit "$Context must be a JSON array." }
    return $Value
}

function Get-CheckedFile([string]$Directory, [string]$Name, [long]$MaximumBytes, [switch]$Optional) {
    $path = [IO.Path]::GetFullPath((Join-Path $Directory $Name))
    if (-not (Test-StrictChildPath $path $Directory)) { Reject-Audit "file '$Name' escaped the run directory." }
    Assert-NoReparseChain $path "file '$Name'"
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    if (-not $exists) {
        if ($Optional) { return $null }
        Reject-Audit "required file '$Name' is missing."
    }
    $item = Get-Item -LiteralPath $path -Force
    if ($item.PSIsContainer -or $item.Length -gt $MaximumBytes) {
        Reject-Audit "file '$Name' is not a bounded regular file (maximum $MaximumBytes bytes)."
    }
    return $item
}

function Read-StrictUtf8([IO.FileInfo]$File, [string]$Context) {
    try {
        $utf8 = [Text.UTF8Encoding]::new($false, $true)
        $text = [IO.File]::ReadAllText($File.FullName, $utf8)
    } catch { Reject-Audit "$Context is not readable strict UTF-8 text." }
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { $text = $text.Substring(1) }
    return $text
}

function ConvertFrom-StrictJsonObject([string]$Text, [string]$Context) {
    if ([string]::IsNullOrWhiteSpace($Text)) { Reject-Audit "$Context is empty." }
    try { $value = $Text | ConvertFrom-Json -ErrorAction Stop }
    catch { Reject-Audit "$Context is invalid JSON." }
    return Require-Object $value $Context
}

function Read-JsonLines([IO.FileInfo]$File, [string]$Context) {
    $raw = Read-StrictUtf8 $File $Context
    $reader = [IO.StringReader]::new($raw)
    $lines = [Collections.Generic.List[string]]::new()
    try {
        while ($null -ne ($line = $reader.ReadLine())) {
            if ([string]::IsNullOrWhiteSpace($line)) { Reject-Audit "$Context contains an empty JSONL row." }
            $lines.Add($line)
        }
    } finally { $reader.Dispose() }
    if ($lines.Count -eq 0) { Reject-Audit "$Context contains no JSONL rows." }
    return [pscustomobject]@{ Raw = $raw; Lines = @($lines.ToArray()) }
}

function Measure-BufferReplay($Lines) {
    [long]$charactersWritten = 0
    [long]$bufferedCharacters = 0
    [long]$maximumBufferedCharacters = 0
    [int]$bufferedRows = 0
    [int]$flushes = 0
    foreach ($line in @($Lines)) {
        [long]$serializedCharacters = $line.Length + [Environment]::NewLine.Length
        if ($bufferedCharacters -gt 0 -and $bufferedCharacters + $serializedCharacters -gt 65536) {
            $charactersWritten += $bufferedCharacters
            $bufferedCharacters = 0
            $bufferedRows = 0
            $flushes++
        }
        $bufferedCharacters += $serializedCharacters
        $bufferedRows++
        $maximumBufferedCharacters = [Math]::Max($maximumBufferedCharacters, $bufferedCharacters)
        if ($bufferedRows -ge 16 -or $bufferedCharacters -ge 65536) {
            $charactersWritten += $bufferedCharacters
            $bufferedCharacters = 0
            $bufferedRows = 0
            $flushes++
        }
    }
    if ($bufferedCharacters -gt 0) {
        $charactersWritten += $bufferedCharacters
        $bufferedCharacters = 0
        $bufferedRows = 0
        $flushes++
    }
    return [pscustomobject]@{
        Flushes = $flushes
        MaximumBufferedCharacters = $maximumBufferedCharacters
        CharactersWritten = $charactersWritten
        BufferedRowsAfterFinalFlush = $bufferedRows
    }
}

function Assert-FixedSummaryField($Summary, [string]$Name, [long]$Expected, [string]$Context) {
    $actual = Require-Integer (Read-Field $Summary $Name $Context) 0 2147483647 "$Context.$Name"
    if ($actual -ne $Expected) { Reject-Audit "$Context.$Name changed; expected $Expected, observed $actual." }
}

function Assert-BufferSummary($Summary, $EvidenceInput, [string]$Context) {
    Assert-FixedSummaryField $Summary 'bufferFlushRows' 16 $Context
    Assert-FixedSummaryField $Summary 'bufferFlushCharacters' 65536 $Context
    $replay = Measure-BufferReplay $EvidenceInput.Lines
    $flushes = Require-Integer (Read-Field $Summary 'flushes' $Context) 0 2048 "$Context.flushes"
    $maximum = Require-Integer (Read-Field $Summary 'maximumBufferedCharacters' $Context) 0 131072 "$Context.maximumBufferedCharacters"
    $written = Require-Integer (Read-Field $Summary 'charactersWritten' $Context) 0 33554432 "$Context.charactersWritten"
    $remaining = Require-Integer (Read-Field $Summary 'bufferedRowsAfterFinalFlush' $Context) 0 2048 "$Context.bufferedRowsAfterFinalFlush"
    if ($EvidenceInput.Raw.Length -ne $replay.CharactersWritten -or $flushes -ne $replay.Flushes -or
        $maximum -ne $replay.MaximumBufferedCharacters -or $written -ne $replay.CharactersWritten -or
        $remaining -ne $replay.BufferedRowsAfterFinalFlush) {
        Reject-Audit "$Context buffer counters do not match the physical JSONL text."
    }
}

if ([string]::IsNullOrWhiteSpace($RunDirectory) -or -not [IO.Path]::IsPathRooted($RunDirectory)) {
    Reject-Audit '-RunDirectory must be one explicit absolute directory.'
}
if (-not (Test-Path -LiteralPath $artifactsRoot -PathType Container)) {
    Reject-Audit 'the project artifacts directory does not exist.'
}
$runFull = [IO.Path]::GetFullPath($RunDirectory)
if (-not (Test-StrictChildPath $runFull $artifactsRoot)) {
    Reject-Audit '-RunDirectory must be a named directory strictly inside project artifacts.'
}
Assert-NoReparseChain $artifactsRoot 'artifacts root'
Assert-NoReparseChain $runFull 'run directory'
if (-not (Test-Path -LiteralPath $runFull -PathType Container)) { Reject-Audit 'the run directory does not exist.' }
$resolvedArtifacts = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $artifactsRoot).Path)
$resolvedRun = [IO.Path]::GetFullPath((Resolve-Path -LiteralPath $runFull).Path)
if (-not (Test-StrictChildPath $resolvedRun $resolvedArtifacts)) {
    Reject-Audit 'the resolved run directory escaped project artifacts.'
}

$resultFile = Get-CheckedFile $resolvedRun 'result.json' 1MB
$result = ConvertFrom-StrictJsonObject (Read-StrictUtf8 $resultFile 'result.json') 'result.json'

$identity = @{
    schema = 'chaite-boss-result/v1'; scenario = 'twins'; difficulty = 'classic'; status = 'win'; outcome = 'SuccessNoDeath'
}
foreach ($name in $identity.Keys) {
    $value = Require-String (Read-Field $result $name 'result.json') "result.json.$name"
    if ($value -cne $identity[$name]) { Reject-Audit "result.json.$name must equal '$($identity[$name])', observed '$value'." }
}
if ((Require-Integer (Read-Field $result 'schemaVersion' 'result.json') 1 1 'result.json.schemaVersion') -ne 1) { Reject-Audit 'unsupported result schema version.' }
if ((Require-Integer (Read-Field $result 'difficultyCode' 'result.json') 0 0 'result.json.difficultyCode') -ne 0) { Reject-Audit 'Twins audit accepts classic difficulty only.' }
if ((Require-Integer (Read-Field $result 'processExitCode' 'result.json') 0 0 'result.json.processExitCode') -ne 0) { Reject-Audit 'the native result process exit code is not success.' }
foreach ($name in @('win','validBattle','battleStarted','allExpectedBossesSeen')) {
    if (-not (Require-Boolean (Read-Field $result $name 'result.json') "result.json.$name")) { Reject-Audit "result.json.$name must be true." }
}
if (Require-Boolean (Read-Field $result 'death' 'result.json') 'result.json.death') { Reject-Audit 'the run records a player death.' }
if ((Require-Integer (Read-Field $result 'deaths' 'result.json') 0 2147483647 'result.json.deaths') -ne 0) { Reject-Audit 'the run records one or more deaths.' }
$minimumLife = Require-Integer (Read-Field $result 'minLife' 'result.json') 0 2147483647 'result.json.minLife'
if ($minimumLife -lt 40) { Reject-Audit "minimum life $minimumLife is below the 40-life safety floor." }
$bossDamage = Require-Integer (Read-Field $result 'bossDamage' 'result.json') 0 2147483647 'result.json.bossDamage'
if ($bossDamage -ne 43000) { Reject-Audit "bossDamage must be exactly 43000 for classic Twins roots, observed $bossDamage." }
$resultTicks = Require-Integer (Read-Field $result 'ticks' 'result.json') 0 24000 'result.json.ticks'
$resultNativeFrames = Require-Integer (Read-Field $result 'nativeFrames' 'result.json') 0 24000 'result.json.nativeFrames'

$firstObserved = Require-Array (Read-Field $result 'firstObservedBosses' 'result.json') 'result.json.firstObservedBosses'
if ($firstObserved.Count -ne 2) { Reject-Audit 'firstObservedBosses must contain exactly the two Twins roots.' }
$expectedLife = @{ 125 = 20000; 126 = 23000 }
$seenRootTypes = [Collections.Generic.HashSet[int]]::new()
foreach ($boss in $firstObserved) {
    $null = Require-Object $boss 'result.json.firstObservedBosses[]'
    $type = Require-Integer (Read-Field $boss 'type' 'result.json.firstObservedBosses[]') 0 2147483647 'firstObservedBosses.type'
    $lifeMax = Require-Integer (Read-Field $boss 'lifeMax' 'result.json.firstObservedBosses[]') 1 2147483647 'firstObservedBosses.lifeMax'
    if (-not $expectedLife.ContainsKey([int]$type) -or $lifeMax -ne $expectedLife[[int]$type] -or -not $seenRootTypes.Add([int]$type)) {
        Reject-Audit "firstObservedBosses must uniquely identify type 125 at 20000 HP and type 126 at 23000 HP."
    }
}

$diagnostics = Require-Object (Read-Field $result 'diagnostics' 'result.json') 'result.json.diagnostics'
if ((Require-String (Read-Field $diagnostics 'schema' 'result.json.diagnostics') 'diagnostics.schema') -cne 'chaite-boss-observation-summary/v1') {
    Reject-Audit 'unsupported Boss observation summary schema.'
}
$declaredObservationFile = Require-String (Read-Field $diagnostics 'file' 'result.json.diagnostics') 'diagnostics.file'
if ($declaredObservationFile -cne 'boss-observations.jsonl') { Reject-Audit 'Boss observation summary must name exactly boss-observations.jsonl.' }
Assert-FixedSummaryField $diagnostics 'maximumRows' 2048 'diagnostics'
Assert-FixedSummaryField $diagnostics 'reservedTerminalRows' 1 'diagnostics'
Assert-FixedSummaryField $diagnostics 'maximumNpcsPerRow' 48 'diagnostics'
Assert-FixedSummaryField $diagnostics 'periodicTicks' 60 'diagnostics'
Assert-FixedSummaryField $diagnostics 'edgeMinimumTicks' 15 'diagnostics'
foreach ($name in @('droppedRows','maximumOmittedNpcs','totalOmittedNpcs','unpairedApplyObservations')) {
    if ((Require-Integer (Read-Field $diagnostics $name 'result.json.diagnostics') 0 2147483647 "diagnostics.$name") -ne 0) {
        Reject-Audit "diagnostics.$name must be zero."
    }
}
if (Require-Boolean (Read-Field $diagnostics 'applyPending' 'result.json.diagnostics') 'diagnostics.applyPending') {
    Reject-Audit 'ApplyPlan observation remained pending at completion.'
}
$applyCalls = Require-Integer (Read-Field $diagnostics 'applyCalls' 'result.json.diagnostics') 1 2147483647 'diagnostics.applyCalls'
$applyReturns = Require-Integer (Read-Field $diagnostics 'applyReturns' 'result.json.diagnostics') 0 2147483647 'diagnostics.applyReturns'
if ($applyCalls -ne $applyReturns) { Reject-Audit 'ApplyPlan calls and returns are not one-to-one.' }

$observationFile = Get-CheckedFile $resolvedRun 'boss-observations.jsonl' 32MB
$observationInput = Read-JsonLines $observationFile 'boss-observations.jsonl'
$observationLines = @($observationInput.Lines)
$declaredRows = Require-Integer (Read-Field $diagnostics 'rows' 'result.json.diagnostics') 1 2048 'diagnostics.rows'
if ($observationLines.Count -ne $declaredRows) { Reject-Audit 'Boss observation physical row count disagrees with diagnostics.rows.' }

$observationRows = [Collections.Generic.List[object]]::new()
$previousTick = -1L
$previousNativeFrame = -1L
$previousCalls = -1L
$periodicRows = 0
$transitionRows = 0
$terminalRows = 0
$planRows = 0
$actualRows = 0
$omittedTotal = 0L
$omittedMaximum = 0L
for ($index = 0; $index -lt $observationLines.Count; $index++) {
    $row = ConvertFrom-StrictJsonObject $observationLines[$index] "boss-observations.jsonl row $($index + 1)"
    if ((Require-String (Read-Field $row 'schema' "observation row $($index + 1)") 'observation.schema') -cne 'chaite-boss-observation/v1') {
        Reject-Audit "Boss observation row $($index + 1) has the wrong schema."
    }
    $tick = Require-Integer (Read-Field $row 'tick' "observation row $($index + 1)") 0 24000 'observation.tick'
    $nativeFrame = Require-Integer (Read-Field $row 'nativeFrames' "observation row $($index + 1)") 0 24000 'observation.nativeFrames'
    if ($tick -le $previousTick -or $nativeFrame -le $previousNativeFrame) { Reject-Audit 'Boss observation ticks/nativeFrames must be strictly increasing; duplicates are rejected.' }
    if ($tick -ne $nativeFrame) { Reject-Audit "Boss observation tick/nativeFrames disagree at tick $tick." }
    $previousTick = $tick
    $previousNativeFrame = $nativeFrame
    $reasonMask = Require-Integer (Read-Field $row 'reasonMask' "observation row $($index + 1)") 0 63 'observation.reasonMask'
    $null = Require-Integer (Read-Field $row 'transitionsSincePreviousRow' "observation row $($index + 1)") 0 2147483647 'observation.transitionsSincePreviousRow'
    if (($reasonMask -band 1) -ne 0) { $periodicRows++ }
    if (($reasonMask -band 30) -ne 0) { $transitionRows++ }
    if (($reasonMask -band 32) -ne 0) {
        $terminalRows++
        if ($index -ne $observationLines.Count - 1) { Reject-Audit 'only the final Boss observation row may be terminal.' }
    }
    $sessionState = Require-String (Read-Field $row 'sessionState' "observation row $($index + 1)") 'observation.sessionState'
    $pending = Require-Boolean (Read-Field $row 'applyPending' "observation row $($index + 1)") 'observation.applyPending'
    if ($pending) { Reject-Audit "Boss observation row at tick $tick has an unpaired ApplyPlan call." }
    $rowCalls = Require-Integer (Read-Field $row 'applyCalls' "observation row $($index + 1)") 0 2147483647 'observation.applyCalls'
    $rowReturns = Require-Integer (Read-Field $row 'applyReturns' "observation row $($index + 1)") 0 2147483647 'observation.applyReturns'
    if ($rowCalls -ne $rowReturns -or $rowCalls -lt $previousCalls) { Reject-Audit "Boss observation ApplyPlan counters are inconsistent at tick $tick." }
    $previousCalls = $rowCalls
    $omitted = Require-Integer (Read-Field $row 'omittedNpcs' "observation row $($index + 1)") 0 2147483647 'observation.omittedNpcs'
    if ($omitted -ne 0) { Reject-Audit "Boss observation row at tick $tick omitted NPC snapshots." }
    $omittedTotal += $omitted
    $omittedMaximum = [Math]::Max($omittedMaximum, $omitted)
    $null = Require-Array (Read-Field $row 'npcs' "observation row $($index + 1)") 'observation.npcs'

    $player = Require-Object (Read-Field $row 'player' "observation row $($index + 1)") 'observation.player'
    $dead = Require-Boolean (Read-Field $player 'dead' 'observation.player') 'observation.player.dead'
    $null = Require-Integer (Read-Field $player 'life' 'observation.player') -2147483648 2147483647 'observation.player.life'
    $wingTime = Require-FiniteNumber (Read-Field $player 'wingTime' 'observation.player') 0 1000000 'observation.player.wingTime'
    $null = Require-FiniteNumber (Read-Field $player 'wingTimeMax' 'observation.player') 0 1000000 'observation.player.wingTimeMax'

    $plan = Read-Field $row 'plan' "observation row $($index + 1)" -AllowNull
    $actual = Read-Field $row 'actualAtApplyReturn' "observation row $($index + 1)" -AllowNull
    if ($null -ne $plan) { $null = Require-Object $plan 'observation.plan'; $planRows++ }
    if ($null -ne $actual) { $null = Require-Object $actual 'observation.actualAtApplyReturn'; $actualRows++ }
    if (($null -eq $plan) -ne ($null -eq $actual)) { Reject-Audit "plan/actual presence differs at tick $tick." }

    $observationRows.Add([pscustomobject]@{
        Index = $index + 1; Tick = $tick; NativeFrame = $nativeFrame; SessionState = $sessionState
        Dead = $dead; WingTime = $wingTime; Plan = $plan; Actual = $actual
    })
}
if ($terminalRows -ne 1) { Reject-Audit 'Boss observations must contain exactly one final terminal row.' }
if ($previousTick -ne $resultTicks -or $previousNativeFrame -ne $resultNativeFrames) { Reject-Audit 'terminal observation tick/nativeFrames disagree with result.json.' }
if ($previousCalls -ne $applyCalls) { Reject-Audit 'final observation ApplyPlan counters disagree with diagnostics.' }
foreach ($comparison in @(
    @{ Name = 'periodicRows'; Actual = $periodicRows }, @{ Name = 'transitionRows'; Actual = $transitionRows },
    @{ Name = 'terminalRows'; Actual = $terminalRows }, @{ Name = 'planRows'; Actual = $planRows },
    @{ Name = 'actualControlRows'; Actual = $actualRows }
)) {
    $declared = Require-Integer (Read-Field $diagnostics $comparison.Name 'result.json.diagnostics') 0 2048 "diagnostics.$($comparison.Name)"
    if ($declared -ne $comparison.Actual) { Reject-Audit "diagnostics.$($comparison.Name) disagrees with the physical JSONL rows." }
}
if ($omittedMaximum -ne 0 -or $omittedTotal -ne 0) { Reject-Audit 'physical omitted-NPC counters are nonzero.' }
Assert-BufferSummary $diagnostics $observationInput 'diagnostics'

$eligible = [Collections.Generic.List[object]]::new()
foreach ($entry in $observationRows) {
    if ($entry.SessionState -cne 'EngagedAlive' -or $entry.Dead) { continue }
    if ($null -eq $entry.Plan -or $null -eq $entry.Actual) { Reject-Audit "engaged/alive proxy row at tick $($entry.Tick) lacks plan or returned controls." }
    $planTick = Require-Integer (Read-Field $entry.Plan 'tick' 'observation.plan') 0 24000 'observation.plan.tick'
    $actualTick = Require-Integer (Read-Field $entry.Actual 'tick' 'observation.actualAtApplyReturn') 0 24000 'observation.actualAtApplyReturn.tick'
    if ($planTick -ne $entry.Tick -or $actualTick -ne $entry.Tick) { Reject-Audit "plan/actual tick does not match engaged proxy row tick $($entry.Tick)." }
    $strategy = Require-String (Read-Field $entry.Plan 'strategy' 'observation.plan') 'observation.plan.strategy'
    if ($strategy -cne 'twins') { Reject-Audit "engaged proxy row at tick $($entry.Tick) is not the Twins strategy." }
    $phase = Require-String (Read-Field $entry.Plan 'phase' 'observation.plan') 'observation.plan.phase'
    if ([string]::IsNullOrWhiteSpace($phase)) { Reject-Audit "engaged proxy row at tick $($entry.Tick) has an empty phase." }
    $horizontal = Require-Integer (Read-Field $entry.Plan 'horizontal' 'observation.plan') -1 1 'observation.plan.horizontal'
    $left = Require-Boolean (Read-Field $entry.Actual 'controlLeft' 'observation.actualAtApplyReturn') 'actual.controlLeft'
    $right = Require-Boolean (Read-Field $entry.Actual 'controlRight' 'observation.actualAtApplyReturn') 'actual.controlRight'
    $expectedLeft = $horizontal -eq -1
    $expectedRight = $horizontal -eq 1
    if ($left -ne $expectedLeft -or $right -ne $expectedRight) { Reject-Audit "returned horizontal controls disagree with plan at tick $($entry.Tick)." }
    $supportedRun = $phase.Contains('-supported-run')
    $committedEscape = $phase.Contains('-committed-escape')
    $recoverRunway = $phase.Contains('-recover-runway')
    $routeClosed = $phase.Contains('-route-closed')
    $controlReturn = $phase.EndsWith('-control-return', [StringComparison]::Ordinal)
    $observationGrace = $phase.Contains('-unsupported-twins-fixture') -or
        $phase.Contains('-unsupported-twins-native-phase') -or
        $phase.Contains('-acquire-runway-no-safe-support') -or
        $phase.Contains('-anchor-lost-closed')
    $acquireRunway = $phase.Contains('-acquire-runway')
    if (-not ($supportedRun -or $committedEscape -or $recoverRunway -or
        $routeClosed -or $controlReturn -or $observationGrace -or $acquireRunway)) {
        Reject-Audit "engaged/alive proxy row at tick $($entry.Tick) has an unreviewed Twins movement phase."
    }
    if ($horizontal -eq 0 -and -not ($routeClosed -or $controlReturn -or $observationGrace)) {
        Reject-Audit "engaged/alive proxy row at tick $($entry.Tick) stopped outside an explicit closed/grace phase."
    }
    if ($horizontal -ne 0 -and ($routeClosed -or $controlReturn -or $observationGrace)) {
        Reject-Audit "engaged/alive proxy row at tick $($entry.Tick) moved during an explicit closed/control-return/grace phase."
    }
    $eligible.Add([pscustomobject]@{
        Tick = $entry.Tick; Direction = [int]$horizontal; Phase = $phase
        SupportedRun = $supportedRun; CommittedEscape = $committedEscape
        RecoverRunway = $recoverRunway; RouteClosed = $routeClosed
        ControlReturn = $controlReturn; ObservationGrace = $observationGrace
        WingTime = $entry.WingTime
    })
}
if ($eligible.Count -eq 0) { Reject-Audit 'no EngagedAlive, non-dead proxy rows were present.' }

$directionChanges = [Collections.Generic.List[object]]::new()
foreach ($entry in $eligible) {
    if ($entry.Direction -eq 0) { continue }
    if ($directionChanges.Count -eq 0 -or $directionChanges[$directionChanges.Count - 1].Direction -ne $entry.Direction) {
        $directionChanges.Add([pscustomobject]@{ Tick = $entry.Tick; Direction = $entry.Direction })
        if ($directionChanges.Count -ge 3) {
            $a = $directionChanges[$directionChanges.Count - 3]
            $b = $directionChanges[$directionChanges.Count - 2]
            $c = $directionChanges[$directionChanges.Count - 1]
            if ($a.Direction -eq $c.Direction -and $a.Direction -eq -$b.Direction -and
                $c.Tick - $a.Tick -le $directionReversalWindowTicks) {
                Reject-Audit "recorded direction sequence $($a.Direction),$($b.Direction),$($c.Direction) spans only $($c.Tick - $a.Tick) ticks."
            }
        }
    }
}

$recoveryStart = $null
$observationGraceStart = $null
$routeClosedStart = $null
$controlReturnTick = $null
$zeroWingStart = $null
$zeroWingLast = $null
$maximumObservedRecovery = 0L
$maximumObservedObservationGrace = 0L
$maximumObservedRouteClosed = 0L
$maximumObservedZeroWingSpan = 0L
$maximumObservedWingRecovery = 0L
foreach ($entry in $eligible) {
    if ($null -ne $controlReturnTick -and $entry.Tick - $controlReturnTick -gt $maximumControlReturnContinuationTicks) {
        Reject-Audit "EngagedAlive continued more than $maximumControlReturnContinuationTicks ticks after control return."
    }
    if ($entry.ControlReturn) {
        if ($null -eq $controlReturnTick) { $controlReturnTick = $entry.Tick }
    }

    if ($entry.RecoverRunway) {
        if ($null -eq $recoveryStart) { $recoveryStart = $entry.Tick }
        if ($entry.Tick - $recoveryStart -gt $maximumRecoveryEpisodeTicks) {
            Reject-Audit "recorded recover-runway episode exceeds $maximumRecoveryEpisodeTicks ticks."
        }
    } elseif ($null -ne $recoveryStart) {
        $duration = $entry.Tick - $recoveryStart
        if ($duration -gt $maximumRecoveryEpisodeTicks) { Reject-Audit "recover-runway took $duration ticks." }
        $maximumObservedRecovery = [Math]::Max($maximumObservedRecovery, $duration)
        $recoveryStart = $null
    }

    if ($entry.ObservationGrace) {
        if ($null -eq $observationGraceStart) { $observationGraceStart = $entry.Tick }
        if ($entry.Tick - $observationGraceStart -gt $maximumObservationGraceTicks) {
            Reject-Audit "recorded observation-loss grace exceeds $maximumObservationGraceTicks ticks."
        }
    } elseif ($null -ne $observationGraceStart) {
        $duration = $entry.Tick - $observationGraceStart
        if ($duration -gt $maximumObservationGraceTicks) { Reject-Audit "observation-loss grace took $duration ticks." }
        $maximumObservedObservationGrace = [Math]::Max($maximumObservedObservationGrace, $duration)
        $observationGraceStart = $null
    }

    if ($entry.RouteClosed) {
        if ($null -eq $routeClosedStart) { $routeClosedStart = $entry.Tick }
        $span = $entry.Tick - $routeClosedStart
        if ($span -gt $maximumRouteClosedEpisodeTicks) {
            Reject-Audit "recorded route-closed episode exceeds $maximumRouteClosedEpisodeTicks ticks."
        }
        $maximumObservedRouteClosed = [Math]::Max($maximumObservedRouteClosed, $span)
        # The same plan explicitly hands control back at the threshold. Treat
        # that edge as a resolved closed episode; the next native row must be
        # terminal or leave EngagedAlive within the separate 3-tick bound.
        if ($entry.ControlReturn) { $routeClosedStart = $null }
    } elseif ($null -ne $routeClosedStart) {
        $duration = $entry.Tick - $routeClosedStart
        if ($duration -gt $maximumRouteClosedEpisodeTicks) { Reject-Audit "route-closed recovery took $duration ticks." }
        $maximumObservedRouteClosed = [Math]::Max($maximumObservedRouteClosed, $duration)
        $routeClosedStart = $null
    }

    if ($entry.WingTime -eq 0) {
        if ($null -eq $zeroWingStart) { $zeroWingStart = $entry.Tick }
        $zeroWingLast = $entry.Tick
        $span = $zeroWingLast - $zeroWingStart
        if ($span -gt $maximumZeroWingProxySpanTicks) { Reject-Audit "recorded zero-wing proxy span is $span ticks." }
        $maximumObservedZeroWingSpan = [Math]::Max($maximumObservedZeroWingSpan, $span)
    } elseif ($null -ne $zeroWingStart) {
        $recovery = $entry.Tick - $zeroWingStart
        if ($recovery -gt $maximumWingRecoveryTicks) { Reject-Audit "wingTime recovery was not observed within $maximumWingRecoveryTicks ticks (observed $recovery)." }
        $maximumObservedWingRecovery = [Math]::Max($maximumObservedWingRecovery, $recovery)
        $zeroWingStart = $null
        $zeroWingLast = $null
    }
}
if ($null -ne $recoveryStart -and $null -eq $controlReturnTick) { Reject-Audit 'the recorded proxy stream ended while still in recover-runway.' }
if ($null -ne $observationGraceStart -and $null -eq $controlReturnTick) { Reject-Audit 'the recorded proxy stream ended while still in observation-loss grace.' }
if ($null -ne $routeClosedStart -and $null -eq $controlReturnTick) { Reject-Audit 'the recorded proxy stream ended while still route-closed.' }
if ($null -ne $zeroWingStart) { Reject-Audit 'the recorded proxy stream ended without observing wingTime recovery.' }

$hurt = Require-Object (Read-Field $diagnostics 'hurt' 'result.json.diagnostics') 'result.json.diagnostics.hurt'
if ((Require-String (Read-Field $hurt 'schema' 'diagnostics.hurt') 'diagnostics.hurt.schema') -cne 'chaite-hurt-observation-summary/v1') {
    Reject-Audit 'unsupported Hurt observation summary schema.'
}
$hurtCalls = Require-Integer (Read-Field $hurt 'calls' 'diagnostics.hurt') 0 2048 'diagnostics.hurt.calls'
$hurtReturns = Require-Integer (Read-Field $hurt 'returns' 'diagnostics.hurt') 0 2048 'diagnostics.hurt.returns'
$hurtRows = Require-Integer (Read-Field $hurt 'rows' 'diagnostics.hurt') 0 2048 'diagnostics.hurt.rows'
$hurtSerialized = Require-Integer (Read-Field $hurt 'serializedRows' 'diagnostics.hurt') 0 2048 'diagnostics.hurt.serializedRows'
if ($hurtCalls -ne $hurtReturns -or $hurtCalls -ne $hurtRows -or $hurtCalls -ne $hurtSerialized) {
    Reject-Audit 'Hurt calls, returns, rows and serializedRows are not one-to-one.'
}
Assert-FixedSummaryField $hurt 'maximumRows' 2048 'diagnostics.hurt'
Assert-FixedSummaryField $hurt 'depthCapacity' 16 'diagnostics.hurt'
$maximumDepth = Require-Integer (Read-Field $hurt 'maximumDepth' 'diagnostics.hurt') 0 16 'diagnostics.hurt.maximumDepth'
$expectedMaximumDepth = if ($hurtRows -eq 0) { 0 } else { 1 }
if ($maximumDepth -ne $expectedMaximumDepth) { Reject-Audit 'Hurt maximumDepth disagrees with its completed rows.' }
foreach ($name in @('droppedRows','depthOverflows','unpairedObservations','pendingDepth','suppressedPendingDepth',
    'observerErrors','sourceReadFailures','bufferedRowsAfterFinalFlush')) {
    if ((Require-Integer (Read-Field $hurt $name 'diagnostics.hurt') 0 2147483647 "diagnostics.hurt.$name") -ne 0) {
        Reject-Audit "diagnostics.hurt.$name must be zero."
    }
}
$hurtFileDeclaration = Read-Field $hurt 'file' 'diagnostics.hurt' -AllowNull
$physicalHurt = Get-CheckedFile $resolvedRun 'hurt-observations.jsonl' 16MB -Optional
if ($hurtRows -eq 0) {
    if ($null -ne $hurtFileDeclaration -or $null -ne $physicalHurt) { Reject-Audit 'empty Hurt summary disagrees with physical file presence.' }
    foreach ($name in @('flushes','maximumBufferedCharacters','charactersWritten')) {
        if ((Require-Integer (Read-Field $hurt $name 'diagnostics.hurt') 0 33554432 "diagnostics.hurt.$name") -ne 0) {
            Reject-Audit "empty Hurt summary has nonzero $name."
        }
    }
    Assert-FixedSummaryField $hurt 'bufferFlushRows' 16 'diagnostics.hurt'
    Assert-FixedSummaryField $hurt 'bufferFlushCharacters' 65536 'diagnostics.hurt'
} else {
    if ($hurtFileDeclaration -isnot [string] -or $hurtFileDeclaration -cne 'hurt-observations.jsonl' -or $null -eq $physicalHurt) {
        Reject-Audit 'nonempty Hurt summary must name an existing hurt-observations.jsonl file.'
    }
    $hurtInput = Read-JsonLines $physicalHurt 'hurt-observations.jsonl'
    if ($hurtInput.Lines.Count -ne $hurtRows) { Reject-Audit 'Hurt physical row count disagrees with its summary.' }
    for ($index = 0; $index -lt $hurtInput.Lines.Count; $index++) {
        $row = ConvertFrom-StrictJsonObject $hurtInput.Lines[$index] "hurt-observations.jsonl row $($index + 1)"
        if ((Require-String (Read-Field $row 'schema' 'Hurt row') 'Hurt row.schema') -cne 'chaite-hurt-observation/v1') { Reject-Audit "Hurt row $($index + 1) has the wrong schema." }
        if ((Require-Integer (Read-Field $row 'sequence' 'Hurt row') 1 2048 'Hurt row.sequence') -ne $index + 1) { Reject-Audit "Hurt row $($index + 1) has a duplicate/out-of-order sequence." }
        $before = Require-Integer (Read-Field $row 'tickBefore' 'Hurt row') 0 24000 'Hurt row.tickBefore'
        $after = Require-Integer (Read-Field $row 'tickAfter' 'Hurt row') 0 24000 'Hurt row.tickAfter'
        if ($before -ne $after) { Reject-Audit "Hurt row $($index + 1) crossed native ticks." }
        $null = Require-FiniteNumber (Read-Field $row 'actualReturn' 'Hurt row') 0 ([double]::MaxValue) 'Hurt row.actualReturn'
        $request = Require-Object (Read-Field $row 'request' 'Hurt row') 'Hurt row.request'
        $null = Require-Integer (Read-Field $request 'damage' 'Hurt row.request') 0 2147483647 'Hurt request.damage'
        $null = Require-Integer (Read-Field $request 'hitDirection' 'Hurt row.request') -1 1 'Hurt request.hitDirection'
        $null = Require-Integer (Read-Field $request 'cooldownCounter' 'Hurt row.request') -2147483648 2147483647 'Hurt request.cooldownCounter'
        foreach ($flag in @('pvp','quiet','crit','dodgeable')) { $null = Require-Boolean (Read-Field $request $flag 'Hurt row.request') "Hurt request.$flag" }
        $player = Require-Object (Read-Field $row 'player' 'Hurt row') 'Hurt row.player'
        if ((Require-Integer (Read-Field $player 'index' 'Hurt row.player') 0 255 'Hurt player.index') -ne 0) { Reject-Audit 'Hurt row is not for the isolated local player.' }
        $lifeBefore = Require-Integer (Read-Field $player 'lifeBefore' 'Hurt row.player') -2147483648 2147483647 'Hurt player.lifeBefore'
        $lifeAfter = Require-Integer (Read-Field $player 'lifeAfter' 'Hurt row.player') -2147483648 2147483647 'Hurt player.lifeAfter'
        $lifeDelta = Require-Integer (Read-Field $player 'lifeDelta' 'Hurt row.player') 0 2147483647 'Hurt player.lifeDelta'
        if ($lifeBefore - $lifeAfter -ne $lifeDelta) { Reject-Audit 'Hurt player life delta is inconsistent.' }
        $null = Require-Object (Read-Field $row 'reason' 'Hurt row') 'Hurt row.reason'
        $null = Require-Object (Read-Field $row 'source' 'Hurt row') 'Hurt row.source'
    }
    Assert-BufferSummary $hurt $hurtInput 'diagnostics.hurt'
}

[pscustomobject]@{
    Schema = 'chaite-twins-native-audit/v1'
    Accepted = $true
    RunDirectory = $resolvedRun
    Scenario = 'twins'
    Difficulty = 'classic'
    Outcome = 'SuccessNoDeath'
    Deaths = 0
    MinimumLife = $minimumLife
    BossDamage = $bossDamage
    ObservationRows = $observationLines.Count
    EligibleProxyRows = $eligible.Count
    HurtRows = $hurtRows
    DirectionReversalWindowTicks = $directionReversalWindowTicks
    MaximumRecoveryEpisodeTicks = $maximumRecoveryEpisodeTicks
    MaximumObservationGraceTicks = $maximumObservationGraceTicks
    MaximumRouteClosedEpisodeTicks = $maximumRouteClosedEpisodeTicks
    MaximumControlReturnContinuationTicks = $maximumControlReturnContinuationTicks
    MaximumZeroWingProxySpanTicks = $maximumZeroWingProxySpanTicks
    MaximumWingRecoveryTicks = $maximumWingRecoveryTicks
    ObservedMaximumRecoveryTicks = $maximumObservedRecovery
    ObservedMaximumObservationGraceTicks = $maximumObservedObservationGrace
    ObservedMaximumRouteClosedTicks = $maximumObservedRouteClosed
    ObservedMaximumZeroWingProxySpanTicks = $maximumObservedZeroWingSpan
    ObservedMaximumWingRecoveryTicks = $maximumObservedWingRecovery
    EvidenceScope = 'Recorded sampled/coalesced proxy rows only: periodic sampling is 60 ticks and selected plan/target/native/fire edges have a 15-tick minimum. Horizontal changes do not trigger rows. This is not full-frame proof.'
}

param(
    [string]$Cases,
    [ValidateSet('smoke6', 'standard18', 'priority24', 'priority-organic6')][string]$Suite = 'smoke6',
    [switch]$Run,
    [string]$OutputDirectory,
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria',
    [ValidateRange(1, 180)][int]$MaximumCases = 36,
    [ValidateRange(120, 960)][int]$TimeoutSeconds = 120
)

# Default is a read-only plan. Actual native-engine execution requires -Run.
# This is a serial, bounded fixture suite, never a proof of general win rate.
# Legacy plan: {"schema":"chaite-boss-cases/v1","cases":[
#   {"scenario":"eye","seed":20260910,"difficulty":"classic"}]}
# Priority/mid-fight plan: {"schema":"chaite-boss-cases/v2","cases":[
#   {"scenario":"deerclops","phase":"forward-spikes","takeoverTick":360,
#    "seed":20260910,"difficulty":"expert"}]}
# Optional case fields: maxTicks (600..24000), wallSeconds (15..900). A staged
# phase is a disclosed test-only native-field fixture, never an organic phase or
# a win-rate claim. Unsupported scenario/phase/difficulty tuples fail closed.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
$scenarioPhases = [ordered]@{
    'eye' = @('summon'); 'king-slime' = @('summon'); 'queen-slime' = @('summon')
    'destroyer' = @('summon'); 'twins' = @('summon'); 'prime' = @('summon')
    'deerclops' = @('summon','spawn-settle','opening','forward-spikes','rubble','slow-roar','double-spikes','shadow-hands','return-home','teleport-home')
    'skeletron' = @('hover','pre-spin','spin-imminent','spin','spin-pursuit','spin-exit','hand-vertical-imminent','hand-vertical-locking','hand-vertical','hand-horizontal-imminent','hand-horizontal-locking','hand-horizontal')
    'queen-bee' = @('summon','choose','charge-align','charge','charge-brake','bee-wave','move-above','stinger','reacquire')
    'wall-of-flesh' = @('runway','accelerating','low-health','critical','eye-laser')
    'duke-fishron' = @('summon','spawn-fade','spawn-emerge','p1-hover','p1-dash','p1-bubbles','p1-sharknado','p2-transition-fade','p2-transition-emerge','p2-hover','p2-dash','p2-bubbles','p2-sharknado','p3-transition-fade','p3-transition-hidden','p3-reposition','p3-dash','p3-teleport')
    'empress-night' = @('summon','p1-reposition','p1-bolts','p1-rainbow','p1-sun-dance','p1-dash','transition','p2-reposition','p2-lance-wall','p2-predictive-lances','p2-spiral')
    'empress-day' = @('summon','p1-reposition','p1-bolts','p1-rainbow','p1-sun-dance','p1-dash','transition','p2-reposition','p2-lance-wall','p2-predictive-lances','p2-spiral')
    'moon-lord' = @('intro','synchronize-eyes','head-bolts','head-tongue','head-deathray-telegraph','left-sphere-release','right-sphere-release')
}
$scenarios = @($scenarioPhases.Keys)
$priorityScenarios = @('deerclops','skeletron','queen-bee','wall-of-flesh','duke-fishron','empress-night','empress-day','moon-lord')
$hardModeScenarios = @('queen-slime','destroyer','twins','prime','duke-fishron','empress-night','empress-day','moon-lord')
$expectedBossTypes = @{
    'eye'=@(4); 'king-slime'=@(50); 'queen-slime'=@(657); 'destroyer'=@(134); 'twins'=@(125,126); 'prime'=@(127)
    'deerclops'=@(668); 'skeletron'=@(35); 'queen-bee'=@(222); 'wall-of-flesh'=@(113,114)
    'duke-fishron'=@(370); 'empress-night'=@(636); 'empress-day'=@(636); 'moon-lord'=@(396,397,398)
}
$expectedSummonTypes = @{
    'eye'=43; 'king-slime'=560; 'queen-slime'=4988; 'destroyer'=556; 'twins'=544; 'prime'=557
    'deerclops'=5120; 'queen-bee'=1133
}
$scenarioVariants = @{
    'eye'='standard'; 'king-slime'='standard'; 'queen-slime'='standard'; 'destroyer'='standard'; 'twins'='standard'; 'prime'='standard'
    'deerclops'='standard'; 'skeletron'='standard'; 'queen-bee'='standard'; 'wall-of-flesh'='standard'; 'duke-fishron'='standard'; 'moon-lord'='standard'
    'empress-night'='night'; 'empress-day'='day'
    # Reserved native encounter identities. They are not silently accepted as
    # ordinary standard evidence when future reviewed fixtures are added.
    'mechanical-mayhem'='simultaneous-mechanical-trio'; 'mechdusa'='getfixedboi-mechdusa'
}
$knownResultVariants = @('standard','night','day','simultaneous-mechanical-trio','getfixedboi-mechdusa')

function Assert-NoReparse([string]$Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($candidate)) {
        $entry = Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked batch paths are not allowed: $candidate" }
        $parent = [IO.Directory]::GetParent($candidate)
        if ($null -eq $parent) { break }
        $candidate = $parent.FullName
    }
}
function Read-Field($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    # Case records remain mutable OrderedDictionary instances until their
    # evidence is finalized. PSObject.Properties does not expose dictionary
    # keys as properties on every supported Windows PowerShell build.
    if ($Object -is [Collections.IDictionary] -and $Object.Contains($Name)) {
        return $Object[$Name]
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $Default }
    return $property.Value
}
function Require-Integer($Value, [long]$Minimum, [long]$Maximum, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt $Minimum -or $Value -gt $Maximum) { throw "Invalid $Name; expected integer $Minimum..$Maximum." }
    return [int]$Value
}
function Require-Boolean($Value, [string]$Name) {
    if ($Value -isnot [bool]) { throw "Invalid $Name; expected JSON boolean." }
    return $Value
}
function Require-ObjectFields($Value, [string[]]$Fields, [string]$Name) {
    if ($null -eq $Value) { throw "Missing $Name object." }
    $declared = @($Value.PSObject.Properties.Name)
    foreach ($field in $Fields) {
        if ($declared -cnotcontains $field) { throw "Missing $Name.$field declaration." }
    }
    return $Value
}
function Require-ExactObjectFields($Value, [string[]]$Fields, [string]$Name) {
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { throw "Missing or invalid $Name object." }
    $declared = @($Value.PSObject.Properties.Name)
    if ($declared.Count -ne $Fields.Count) { throw "$Name field set changed." }
    foreach ($field in $Fields) {
        if ($declared -cnotcontains $field) { throw "Missing $Name.$field declaration." }
    }
    return $Value
}
function Require-String($Value, [string]$Name) {
    if ($Value -isnot [string]) { throw "Invalid $Name; expected JSON string scalar." }
    return [string]$Value
}
function Require-Sha256($Value, [string]$Name) {
    $text = Require-String $Value $Name
    if ($text -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid $Name; expected uppercase SHA256 string." }
    return $text
}
function Assert-OrdinalString($Value, [string]$Expected, [string]$Name) {
    $text = Require-String $Value $Name
    if (-not [string]::Equals($text, $Expected, [StringComparison]::Ordinal)) { throw "Invalid $Name value." }
    return $text
}
function Require-ResultVariant($Value, [string]$Name) {
    $variant = Require-String $Value $Name
    if ($variant -cnotin $knownResultVariants) { throw "Invalid $Name; unsupported encounter variant." }
    return $variant
}
function Get-CaseVariant($Case) {
    $scenario = Require-String (Read-Field $Case 'Scenario') 'case scenario'
    if (-not $scenarioVariants.ContainsKey($scenario)) { throw 'Case scenario has no reviewed variant identity.' }
    $variant = Require-ResultVariant (Read-Field $Case 'Variant') 'case variant'
    if ($variant -cne $scenarioVariants[$scenario]) { throw 'Case variant does not match the reviewed scenario identity.' }
    return $variant
}
function Test-StandardWorldRules($Native) {
    foreach ($field in @('forTheWorthy', 'zenithWorld', 'drunkWorld', 'notTheBeesWorld',
        'remixWorld', 'celebrationWorld', 'constantWorld', 'noTrapsWorld', 'skyblockWorld')) {
        if ((Require-Boolean (Read-Field $Native $field) "variant native $field") -ne $false) { return $false }
    }
    return $true
}
function Assert-VariantEvidence($Result, $Case, [bool]$ValidBattle) {
    $expected = Get-CaseVariant $Case
    $reported = Require-ResultVariant (Read-Field $Result 'variant') 'result variant'
    if ($reported -cne $expected) { throw 'Result variant does not match the reviewed case variant.' }
    $evidence = Require-ObjectFields (Read-Field $Result 'variantEvidence') @(
        'schema', 'scenario', 'expectedVariant', 'observedVariant', 'reportedVariant',
        'capturedAtActivation', 'captureTick', 'requestedTakeoverTick', 'nativeFlagsVerified',
        'variantMatchesScenario', 'mechanicalTrioExpected', 'mechanicalTrioObserved',
        'allExpectedBossesSeen', 'native') 'variant evidence'
    if ((Read-Field $evidence 'schema') -cne 'chaite-boss-variant-evidence/v1' -or
        (Read-Field $evidence 'scenario') -cne (Read-Field $Case 'Scenario') -or
        (Require-ResultVariant (Read-Field $evidence 'expectedVariant') 'variant expectedVariant') -cne $expected -or
        (Require-ResultVariant (Read-Field $evidence 'observedVariant') 'variant observedVariant') -cne $reported -or
        (Require-ResultVariant (Read-Field $evidence 'reportedVariant') 'variant reportedVariant') -cne $reported) {
        throw 'Variant evidence identity does not match its result/case.'
    }
    $captureTick = Require-Integer (Read-Field $evidence 'captureTick') -1 23880 'variant captureTick'
    $requestedTakeoverTick = Require-Integer (Read-Field $evidence 'requestedTakeoverTick') 120 23880 'variant requestedTakeoverTick'
    $captured = Require-Boolean (Read-Field $evidence 'capturedAtActivation') 'variant capturedAtActivation'
    $flagsVerified = Require-Boolean (Read-Field $evidence 'nativeFlagsVerified') 'variant nativeFlagsVerified'
    $matchesScenario = Require-Boolean (Read-Field $evidence 'variantMatchesScenario') 'variant variantMatchesScenario'
    $mechanicalExpected = Require-Boolean (Read-Field $evidence 'mechanicalTrioExpected') 'variant mechanicalTrioExpected'
    $mechanicalObserved = Require-Boolean (Read-Field $evidence 'mechanicalTrioObserved') 'variant mechanicalTrioObserved'
    $allExpected = Require-Boolean (Read-Field $evidence 'allExpectedBossesSeen') 'variant allExpectedBossesSeen'
    $native = Require-ObjectFields (Read-Field $evidence 'native') @(
        'gameMode', 'difficulty', 'dayTime', 'hardMode', 'forTheWorthy', 'zenithWorld',
        'drunkWorld', 'notTheBeesWorld', 'remixWorld', 'celebrationWorld', 'constantWorld',
        'noTrapsWorld', 'skyblockWorld') 'variant native'
    $mode = @('classic', 'expert', 'master').IndexOf([string](Read-Field $Case 'Difficulty'))
    if ($mode -lt 0 -or (Require-Integer (Read-Field $native 'gameMode') 0 2 'variant native gameMode') -ne $mode -or
        (Require-Integer (Read-Field $native 'difficulty') 1 3 'variant native difficulty') -ne ($mode + 1)) {
        throw 'Variant native difficulty does not match the case.'
    }
    # Validate all Boolean values even for a non-winning result. PowerShell's
    # coercion must never turn strings such as "false" into native evidence.
    foreach ($field in @('dayTime', 'hardMode', 'forTheWorthy', 'zenithWorld', 'drunkWorld',
        'notTheBeesWorld', 'remixWorld', 'celebrationWorld', 'constantWorld', 'noTrapsWorld', 'skyblockWorld')) {
        $null = Require-Boolean (Read-Field $native $field) "variant native $field"
    }
    if ($ValidBattle) {
        if (-not $captured -or $captureTick -ne (Read-Field $Case 'TakeoverTick') -or
            $requestedTakeoverTick -ne (Read-Field $Case 'TakeoverTick') -or -not $flagsVerified -or
            -not $matchesScenario -or -not $allExpected) {
            throw 'Valid battle lacks a matching native variant observation at takeover.'
        }
        switch ($expected) {
            'standard' {
                if (-not (Test-StandardWorldRules $native) -or $mechanicalExpected -or $mechanicalObserved) {
                    throw 'Standard evidence has special-world or mechanical-encounter markers.'
                }
            }
            'night' {
                if ((Read-Field $Case 'Scenario') -cne 'empress-night' -or
                    (Require-Boolean (Read-Field $native 'dayTime') 'variant native dayTime') -or
                    -not (Test-StandardWorldRules $native) -or $mechanicalExpected -or $mechanicalObserved) {
                    throw 'Night Empress variant evidence is inconsistent with the native world state.'
                }
            }
            'day' {
                if ((Read-Field $Case 'Scenario') -cne 'empress-day' -or
                    -not (Require-Boolean (Read-Field $native 'dayTime') 'variant native dayTime') -or
                    -not (Test-StandardWorldRules $native) -or $mechanicalExpected -or $mechanicalObserved) {
                    throw 'Day Empress variant evidence is inconsistent with the native world state.'
                }
            }
            'simultaneous-mechanical-trio' {
                if ((Read-Field $Case 'Scenario') -cne 'mechanical-mayhem' -or
                    -not (Test-StandardWorldRules $native) -or -not $mechanicalExpected -or -not $mechanicalObserved) {
                    throw 'Mechanical-trio variant evidence lacks its native world/topology proof.'
                }
            }
            'getfixedboi-mechdusa' {
                if ((Read-Field $Case 'Scenario') -cne 'mechdusa' -or
                    -not (Require-Boolean (Read-Field $native 'zenithWorld') 'variant native zenithWorld') -or
                    -not $mechanicalExpected -or -not $mechanicalObserved) {
                    throw 'Mechdusa variant evidence lacks its Zenith-world/topology proof.'
                }
            }
        }
    }
    return [pscustomobject]@{ Variant = $reported; Evidence = $evidence }
}
function Require-FiniteNumber($Value, [double]$Minimum, [double]$Maximum, [string]$Name) {
    if ($null -eq $Value -or $Value -is [bool] -or $Value -is [string] -or $Value -is [char]) {
        throw "Invalid $Name; expected finite JSON number."
    }
    try { $number = [Convert]::ToDouble($Value, [Globalization.CultureInfo]::InvariantCulture) }
    catch { throw "Invalid $Name; expected finite JSON number." }
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number) -or $number -lt $Minimum -or $number -gt $Maximum) {
        throw "Invalid $Name; expected finite JSON number in $Minimum..$Maximum."
    }
    return $number
}
function Read-StrictUtf8File([string]$Path, [long]$MaximumBytes, [string]$Name) {
    Assert-NoReparse $Path
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing $Name." }
    $item = Get-Item -LiteralPath $Path -Force
    if ($item.Length -gt $MaximumBytes) { throw "$Name exceeds its bounded maximum file size." }
    try {
        $utf8 = [Text.UTF8Encoding]::new($false, $true)
        return [IO.File]::ReadAllText($Path, $utf8)
    } catch { throw "$Name is not readable strict UTF-8 text." }
}
function Read-JsonObjectFile([string]$Path, [long]$MaximumBytes, [string]$Name) {
    $raw = Read-StrictUtf8File $Path $MaximumBytes $Name
    $trimmed = $raw.Trim()
    if ($trimmed.Length -lt 2 -or $trimmed[0] -cne '{' -or $trimmed[$trimmed.Length - 1] -cne '}') {
        throw "$Name must contain one root JSON object."
    }
    try { $value = ConvertFrom-Json -InputObject $trimmed }
    catch { throw "$Name is not valid JSON." }
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "$Name must contain one root JSON object." }
    return $value
}
function Get-HurtEvidenceInput([string]$RunDirectory) {
    $path = Join-Path $RunDirectory 'hurt-observations.jsonl'
    Assert-NoReparse $path
    $exists = Test-Path -LiteralPath $path -PathType Leaf
    $lines = @()
    $rawCharacters = 0
    if ($exists) {
        $raw = Read-StrictUtf8File $path 16MB 'Hurt observation evidence'
        $rawCharacters = $raw.Length
        $reader = [IO.StringReader]::new($raw)
        try {
            $lineList = [Collections.Generic.List[string]]::new()
            while ($null -ne ($line = $reader.ReadLine())) { $lineList.Add($line) }
            $lines = @($lineList.ToArray())
        } finally { $reader.Dispose() }
    }
    return [pscustomobject]@{ Exists = $exists; Lines = $lines; RawCharacters = $rawCharacters }
}
function Measure-HurtBufferReplay($Lines) {
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
function Read-DesktopExitCode($ExitRecord) {
    if ((Read-Field $ExitRecord 'Schema') -cne 'chaite-desktop-exit/v1') { throw 'Invalid host-exit schema.' }
    return Require-Integer (Read-Field $ExitRecord 'HostExitCode') 0 2147483647 'host exit code'
}
function Get-PhysicalSha256([string]$Path, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Missing $Name file." }
    $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
    if ($hash -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid physical $Name SHA256." }
    return $hash
}
function Read-AndValidateLaunchEvidence([string]$LaunchBindingPath, [string]$RunDirectory, $ExpectedRecord) {
    $launchBinding = Read-JsonObjectFile $LaunchBindingPath 64KB 'launch binding'
    $launchBindingFields = @('Schema', 'RunId', 'RunName', 'CreatedUtc', 'PrelaunchValidationPassed', 'LockIntent',
        'PinnedPreparedFileCount', 'PinPlanSha256', 'ManifestSha256', 'StaticEvidenceSha256', 'PreparedTerrariaSha256',
        'PreparedGameProbeSha256', 'PreparedDesktopHostSha256', 'StartIsolatedTestScriptSha256',
        'DesktopHostSourceSha256')
    $null = Require-ExactObjectFields $launchBinding $launchBindingFields 'launch binding'
    $null = Assert-OrdinalString $launchBinding.Schema 'chaite-launch-binding/v1' 'launch binding Schema'
    $run = [IO.Path]::GetFullPath($RunDirectory)
    Assert-NoReparse $run
    $runName = [IO.Path]::GetFileName($run)
    $null = Assert-OrdinalString $launchBinding.RunName $runName 'launch binding RunName'
    $runId = Require-String $launchBinding.RunId 'launch binding RunId'
    $parsedRunId = [guid]::Empty
    if (-not [guid]::TryParseExact($runId, 'D', [ref]$parsedRunId) -or $parsedRunId -eq [guid]::Empty) { throw 'Invalid launch binding RunId.' }
    $createdText = Require-String $launchBinding.CreatedUtc 'launch binding CreatedUtc'
    $created = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($createdText, [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind, [ref]$created)) { throw 'Invalid launch binding CreatedUtc.' }
    if ((Require-Boolean $launchBinding.PrelaunchValidationPassed 'launch binding PrelaunchValidationPassed') -ne $true) {
        throw 'Launch binding did not complete prelaunch validation.'
    }
    $null = Assert-OrdinalString $launchBinding.LockIntent 'launcher-and-desktop-host-fileshare-read-until-job-empty' 'launch binding LockIntent'
    $pinnedPreparedFileCount = Require-Integer $launchBinding.PinnedPreparedFileCount 1 1000000 'launch binding PinnedPreparedFileCount'
    foreach ($field in @($launchBindingFields | Where-Object { $_ -like '*Sha256' })) {
        $null = Require-Sha256 $launchBinding.$field "launch binding $field"
    }

    $manifestPath = Join-Path $run 'probe-manifest.json'
    $sidecarPath = Join-Path $run 'probe-static-evidence.json'
    $hostDirectory = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($LaunchBindingPath))
    $pinPlanPath = Join-Path $hostDirectory 'launch-pin-plan.txt'
    $manifest = Read-JsonObjectFile $manifestPath 64MB 'prepared probe manifest'
    $manifestFields = @('Schema', 'RunId', 'CreatedUtc', 'RunName', 'StaticEvidenceFile', 'Mode', 'GameVersion',
        'SourceGameSha256', 'SaveDirectory', 'SocialDisabled', 'LaunchGuardBeforeLogging', 'SyntheticHotkeysOnly',
        'InputPluginSha256', 'InputCoreSha256', 'ProbeSourceSha256', 'ProbePatcherSourceSha256',
        'PrepareGameProbeScriptSha256', 'StartIsolatedTestScriptSha256', 'RunBossValidationScriptSha256',
        'TestBossEvidenceScriptSha256', 'DesktopHostSourceSha256', 'Files')
    $null = Require-ExactObjectFields $manifest $manifestFields 'prepared probe manifest'
    $null = Assert-OrdinalString $manifest.Schema 'chaite-isolated-probe/v2' 'prepared probe manifest Schema'
    $null = Assert-OrdinalString $manifest.RunId $runId 'prepared probe manifest RunId'
    $null = Assert-OrdinalString $manifest.RunName $runName 'prepared probe manifest RunName'
    $null = Assert-OrdinalString $manifest.StaticEvidenceFile 'probe-static-evidence.json' 'prepared probe manifest StaticEvidenceFile'
    if ($manifest.Files -isnot [Array]) { throw 'Prepared probe manifest Files must be a JSON array.' }
    foreach ($file in $manifest.Files) {
        $null = Require-ExactObjectFields $file @('Path', 'Sha256') 'prepared probe manifest Files entry'
        $null = Require-String $file.Path 'prepared probe manifest Files.Path'
        $null = Require-Sha256 $file.Sha256 'prepared probe manifest Files.Sha256'
    }
    if ($pinnedPreparedFileCount -ne ($manifest.Files.Count + 2)) { throw 'Launch binding pinned-file count disagrees with the manifest plus control files.' }

    $sidecar = Read-JsonObjectFile $sidecarPath 64KB 'prepared probe static evidence'
    $staticFields = @('Schema', 'RunId', 'RunName', 'IlValidationPassed', 'ManifestSha256', 'PreparedTerrariaSha256',
        'PreparedGameProbeSha256', 'PreparedDesktopHostSha256', 'PrepareGameProbeScriptSha256',
        'StartIsolatedTestScriptSha256', 'GameProbeSourceSha256', 'GameProbePatcherSourceSha256',
        'DesktopHostSourceSha256', 'RunBossValidationScriptSha256', 'TestBossEvidenceScriptSha256')
    $null = Require-ExactObjectFields $sidecar $staticFields 'prepared probe static evidence'
    $null = Assert-OrdinalString $sidecar.Schema 'chaite-probe-static-evidence/v2' 'prepared probe static evidence Schema'
    $null = Assert-OrdinalString $sidecar.RunId $runId 'prepared probe static evidence RunId'
    $null = Assert-OrdinalString $sidecar.RunName $runName 'prepared probe static evidence RunName'
    if ((Require-Boolean $sidecar.IlValidationPassed 'prepared probe static evidence IlValidationPassed') -ne $true) {
        throw 'Prepared probe static evidence did not record successful IL validation.'
    }
    foreach ($field in @($staticFields | Where-Object { $_ -like '*Sha256' })) {
        $null = Require-Sha256 $sidecar.$field "prepared probe static evidence $field"
    }

    $physical = [ordered]@{
        PinPlanSha256 = Get-PhysicalSha256 $pinPlanPath 'launch pin plan'
        ManifestSha256 = Get-PhysicalSha256 $manifestPath 'prepared probe manifest'
        StaticEvidenceSha256 = Get-PhysicalSha256 $sidecarPath 'prepared probe static evidence'
        PreparedTerrariaSha256 = Get-PhysicalSha256 (Join-Path $run 'Terraria.exe') 'prepared Terraria'
        PreparedGameProbeSha256 = Get-PhysicalSha256 (Join-Path $run 'Chaite.GameProbe.dll') 'prepared GameProbe'
        PreparedDesktopHostSha256 = Get-PhysicalSha256 (Join-Path $run 'Chaite.DesktopHost.exe') 'prepared DesktopHost'
    }
    foreach ($field in $physical.Keys) {
        $declared = Require-Sha256 $launchBinding.$field "launch binding $field"
        if (-not [string]::Equals($declared, $physical[$field], [StringComparison]::Ordinal)) {
            throw "Launch binding physical hash changed: $field"
        }
    }
    foreach ($field in @('ManifestSha256', 'PreparedTerrariaSha256', 'PreparedGameProbeSha256', 'PreparedDesktopHostSha256')) {
        $declared = Require-Sha256 $sidecar.$field "prepared probe static evidence $field"
        if (-not [string]::Equals($declared, $physical[$field], [StringComparison]::Ordinal)) {
            throw "Prepared probe static evidence physical binding changed: $field"
        }
    }
    $sourceMap = [ordered]@{
        StartIsolatedTestScriptSha256 = 'StartIsolatedTestScriptSha256'
        DesktopHostSourceSha256 = 'DesktopHostSourceSha256'
    }
    foreach ($field in $sourceMap.Keys) {
        $expected = Require-Sha256 (Read-Field $ExpectedRecord $field) "fixed batch $field"
        $manifestValue = Require-Sha256 $manifest.($sourceMap[$field]) "prepared probe manifest $field"
        $sidecarValue = Require-Sha256 $sidecar.$field "prepared probe static evidence $field"
        $launchBindingValue = Require-Sha256 $launchBinding.$field "launch binding $field"
        foreach ($actual in @($manifestValue, $sidecarValue, $launchBindingValue)) {
            if (-not [string]::Equals($actual, $expected, [StringComparison]::Ordinal)) { throw "Launch provenance changed: $field" }
        }
    }
    $launchBindingSha256 = Get-PhysicalSha256 $LaunchBindingPath 'launch binding'
    $completionPath = Join-Path $hostDirectory 'host-lock-completion.json'
    $completion = Read-JsonObjectFile $completionPath 64KB 'host lock completion'
    $completionFields = @('Schema', 'RunId', 'CompletedUtc', 'CompletionStatus', 'LockStrategy',
        'LaunchBindingSha256', 'PinPlanSha256', 'PinnedPreparedFileCount', 'ChildExitCode',
        'RootProcessSignaled', 'JobActiveProcesses', 'PinnedHandlesRevalidated')
    $null = Require-ExactObjectFields $completion $completionFields 'host lock completion'
    $null = Assert-OrdinalString $completion.Schema 'chaite-host-lock-completion/v1' 'host lock completion Schema'
    $null = Assert-OrdinalString $completion.RunId $runId 'host lock completion RunId'
    $completedText = Require-String $completion.CompletedUtc 'host lock completion CompletedUtc'
    $completed = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($completedText, [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind, [ref]$completed) -or $completed -lt $created) {
        throw 'Invalid host lock completion CompletedUtc.'
    }
    $null = Assert-OrdinalString $completion.CompletionStatus 'child-exited-job-empty' 'host lock completion CompletionStatus'
    $null = Assert-OrdinalString $completion.LockStrategy 'desktop-host-fileshare-read-through-job-empty' 'host lock completion LockStrategy'
    $completionLaunchBindingSha256 = Require-Sha256 $completion.LaunchBindingSha256 'host lock completion LaunchBindingSha256'
    $completionPinPlanSha256 = Require-Sha256 $completion.PinPlanSha256 'host lock completion PinPlanSha256'
    if (-not [string]::Equals($completionLaunchBindingSha256, $launchBindingSha256, [StringComparison]::Ordinal) -or
        -not [string]::Equals($completionPinPlanSha256, $physical.PinPlanSha256, [StringComparison]::Ordinal)) {
        throw 'Host lock completion control-file hashes disagree with the physical launch binding.'
    }
    $completionPinnedCount = Require-Integer $completion.PinnedPreparedFileCount 1 1000000 'host lock completion PinnedPreparedFileCount'
    if ($completionPinnedCount -ne $pinnedPreparedFileCount) { throw 'Host lock completion pinned-file count disagrees with the launch binding.' }
    $completionChildExitCode = Require-Integer $completion.ChildExitCode 0 2147483647 'host lock completion ChildExitCode'
    $expectedChildExitCode = Require-Integer (Read-Field $ExpectedRecord 'ChildExitCode') 0 2147483647 'observed child exit code'
    $expectedHostExitCode = Require-Integer (Read-Field $ExpectedRecord 'HostExitCode') 0 2147483647 'observed host exit code'
    if ($completionChildExitCode -ne $expectedChildExitCode -or $completionChildExitCode -ne $expectedHostExitCode) {
        throw 'Host lock completion exit code disagrees with the observed child or host exit.'
    }
    if ((Require-Boolean $completion.RootProcessSignaled 'host lock completion RootProcessSignaled') -ne $true -or
        (Require-Integer $completion.JobActiveProcesses 0 0 'host lock completion JobActiveProcesses') -ne 0 -or
        (Require-Boolean $completion.PinnedHandlesRevalidated 'host lock completion PinnedHandlesRevalidated') -ne $true) {
        throw 'Host lock completion did not prove root exit, an empty job, and pinned-handle revalidation.'
    }
    return [pscustomobject]@{
        RunId = $runId; PinPlanSha256 = $physical.PinPlanSha256; ManifestSha256 = $physical.ManifestSha256; StaticEvidenceSha256 = $physical.StaticEvidenceSha256
        LaunchBindingSha256 = $launchBindingSha256
        HostLockCompletionSha256 = (Get-PhysicalSha256 $completionPath 'host lock completion')
        PreparedTerrariaSha256 = $physical.PreparedTerrariaSha256
        PreparedGameProbeSha256 = $physical.PreparedGameProbeSha256
        PreparedDesktopHostSha256 = $physical.PreparedDesktopHostSha256
        PinnedPreparedFileCount = $pinnedPreparedFileCount
        HostCompletedFileCount = $completionPinnedCount
    }
}
function Assert-BossLifeEvidence($Result, $Case, [bool]$IsWin) {
    $null = Require-ObjectFields $Result @('bossLifeRemaining','bossLifeObservation','ticks') 'result'
    $ticks = Require-Integer (Read-Field $Result 'ticks') 0 24000 'result ticks'
    $remaining = Require-Integer (Read-Field $Result 'bossLifeRemaining') 0 2147483647 'result bossLifeRemaining'
    $observation = Require-ObjectFields (Read-Field $Result 'bossLifeObservation') @(
        'schema','life','observedTick','kind','activeAtTermination','activeExpectedRootCountAtTermination',
        'lastActiveExpectedRootTick','lastActiveExpectedRootLife','lastActiveExpectedRootCount','lastActiveExpectedRoots'
    ) 'result bossLifeObservation'
    if ((Read-Field $observation 'schema') -cne 'chaite-boss-life-observation/v1') {
        throw 'Unsupported Boss-life observation schema.'
    }
    $life = Require-Integer (Read-Field $observation 'life') 0 2147483647 'Boss-life observation life'
    if ($life -ne $remaining) { throw 'Boss-life observation disagrees with bossLifeRemaining.' }
    $observedTick = Require-Integer (Read-Field $observation 'observedTick') -1 $ticks 'Boss-life observedTick'
    $lastTick = Require-Integer (Read-Field $observation 'lastActiveExpectedRootTick') -1 $ticks 'Boss-life last active tick'
    $lastLife = Require-Integer (Read-Field $observation 'lastActiveExpectedRootLife') 0 2147483647 'Boss-life last active life'
    $lastCount = Require-Integer (Read-Field $observation 'lastActiveExpectedRootCount') 0 1000 'Boss-life last active root count'
    $activeCount = Require-Integer (Read-Field $observation 'activeExpectedRootCountAtTermination') 0 1000 'Boss-life terminal active root count'
    $activeAtTermination = Require-Boolean (Read-Field $observation 'activeAtTermination') 'Boss-life activeAtTermination'
    if ($activeAtTermination -ne ($activeCount -gt 0)) { throw 'Boss-life terminal active flag/count disagree.' }
    $kind = Require-String (Read-Field $observation 'kind') 'Boss-life observation kind'
    if ($kind -cnotin @('confirmed-victory','active-expected-roots','last-active-expected-roots','not-observed')) {
        throw 'Unknown Boss-life observation kind.'
    }
    $scenarioName = Require-String (Read-Field $Case 'Scenario') 'case scenario'
    $expectedTypes = @($expectedBossTypes[$scenarioName])
    if ($expectedTypes.Count -eq 0) { throw 'Case has no expected Boss-root identity for life validation.' }
    $roots = @(Read-Field $observation 'lastActiveExpectedRoots')
    if ($roots.Count -ne $lastCount) { throw 'Boss-life last active root count disagrees with its snapshots.' }
    $slots = [Collections.Generic.HashSet[int]]::new()
    [long]$rootLife = 0
    foreach ($root in $roots) {
        $root = Require-ObjectFields $root @('slot','type','life','lifeMax') 'Boss-life root snapshot'
        $slot = Require-Integer (Read-Field $root 'slot') 0 999 'Boss-life root slot'
        $type = Require-Integer (Read-Field $root 'type') 0 65535 'Boss-life root type'
        $rootCurrentLife = Require-Integer (Read-Field $root 'life') 0 2147483647 'Boss-life root life'
        $rootLifeMax = Require-Integer (Read-Field $root 'lifeMax') 1 2147483647 'Boss-life root lifeMax'
        if (-not $slots.Add($slot) -or $type -notin $expectedTypes -or $rootCurrentLife -gt $rootLifeMax) {
            throw 'Boss-life root snapshot has a reused slot, unexpected type, or invalid life bound.'
        }
        $rootLife += $rootCurrentLife
    }
    if ($rootLife -ne $lastLife) { throw 'Boss-life root snapshots disagree with last active life.' }
    if ($lastTick -lt 0 -and ($lastCount -ne 0 -or $lastLife -ne 0)) {
        throw 'Boss-life not-observed sentinel contains an active-root snapshot.'
    }
    if ($lastTick -ge 0 -and $lastCount -eq 0) { throw 'Boss-life active observation has no root identities.' }
    if ($IsWin) {
        if ($kind -cne 'confirmed-victory' -or $remaining -ne 0 -or $observedTick -ne $ticks -or
            $activeAtTermination -or $activeCount -ne 0 -or $lastTick -lt 0) {
            throw 'Winning Boss-life evidence is not a confirmed inactive-root zero.'
        }
    } elseif ($activeAtTermination) {
        if ($kind -cne 'active-expected-roots' -or $observedTick -ne $ticks -or $lastTick -ne $ticks -or
            $activeCount -ne $lastCount -or $remaining -ne $lastLife) {
            throw 'Terminal active Boss-life evidence is internally inconsistent.'
        }
    } elseif ($lastTick -ge 0) {
        if ($kind -cne 'last-active-expected-roots' -or $observedTick -ne $lastTick -or $remaining -ne $lastLife -or $activeCount -ne 0) {
            throw 'Inactive loss/timeout did not preserve the last active expected roots.'
        }
    } elseif ($kind -cne 'not-observed' -or $observedTick -ne -1 -or $remaining -ne 0 -or $activeCount -ne 0) {
        throw 'Never-observed Boss-life evidence has invalid sentinel values.'
    }
}
function Read-ValidatedResultEnvelope($Result, $Case, [bool]$BattleStartedFromLog,
    $HostExitCode, $ChildExitCode, [bool]$DesktopSafe) {
    if ((Read-Field $Result 'schema') -cne 'chaite-boss-result/v1' -or
        (Require-Integer (Read-Field $Result 'schemaVersion') 1 1 'result schemaVersion') -ne 1 -or
        (Read-Field $Result 'scenario') -cne $Case.Scenario -or
        (Require-Integer (Read-Field $Result 'seed') 0 2147483647 'result seed') -ne $Case.Seed -or
        (Read-Field $Result 'difficulty') -cne $Case.Difficulty -or
        (Read-Field $Result 'requestedPhase') -cne $Case.Phase -or
        (Require-Integer (Read-Field $Result 'requestedTakeoverTick') 120 23880 'requested takeover tick') -ne $Case.TakeoverTick) {
        throw 'Result identity/schema does not match its case.'
    }
    $status = Read-Field $Result 'status'
    if ($status -isnot [string] -or $status -cnotin @('win', 'loss', 'timeout', 'rejected', 'harness-error')) {
        throw 'Unrecognized result status.'
    }
    $battleStarted = Require-Boolean (Read-Field $Result 'battleStarted') 'result battleStarted'
    $validBattle = Require-Boolean (Read-Field $Result 'validBattle') 'result validBattle'
    $isWin = Require-Boolean (Read-Field $Result 'win') 'result win'
    Assert-BossLifeEvidence $Result $Case $isWin
    $deaths = Require-Integer (Read-Field $Result 'deaths') 0 2147483647 'result deaths'
    $processExitCode = Require-Integer (Read-Field $Result 'processExitCode') 0 2147483647 'result processExitCode'
    $actualTakeoverTick = Require-Integer (Read-Field $Result 'actualTakeoverTick') -1 23880 'actual takeover tick'
    $null = Require-ObjectFields $Result @(
        'directSpawn', 'directSpawnTick', 'directSpawnAttempted', 'directSpawnCompleted',
        'phaseStageAttempted', 'phaseStaged', 'phaseVerifiedAtTakeover',
        'phaseStage', 'takeoverNativeSnapshot', 'encounterFixtureReady', 'summonConsumed'
    ) 'result'
    $directSpawn = Require-Boolean (Read-Field $Result 'directSpawn') 'result directSpawn'
    $organicPriority = $Case.Scenario -cin @('deerclops','queen-bee') -and $Case.Phase -ceq 'summon'
    $expectedDirectSpawn = $Case.Scenario -cin $priorityScenarios -and -not $organicPriority
    if ($directSpawn -ne $expectedDirectSpawn) { throw 'Result direct-spawn mode disagrees with the reviewed scenario catalog.' }
    $variant = Assert-VariantEvidence $Result $Case $validBattle
    $evidenceKind = Require-String (Read-Field $Result 'evidenceKind') 'result evidenceKind'
    $readinessEligible = Require-Boolean (Read-Field $Result 'readinessEligible') 'result readinessEligible'
    if ($expectedDirectSpawn) {
        if ($evidenceKind -cne 'staged-native-phase-regression' -or $readinessEligible) {
            throw 'A staged priority phase fixture must not be labeled as readiness-eligible evidence.'
        }
    } elseif ($evidenceKind -cne 'isolated-native-encounter' -or -not $readinessEligible) {
        throw 'An unstaged isolated encounter has an invalid readiness-evidence label.'
    }
    if (-not $DesktopSafe -or $null -eq $HostExitCode -or $null -eq $ChildExitCode) {
        throw 'Desktop safety or process-exit evidence is incomplete/inconsistent.'
    }
    $hostCode = Require-Integer $HostExitCode 0 2147483647 'host exit code'
    $childCode = Require-Integer $ChildExitCode 0 2147483647 'child exit code'
    if ($childCode -ne $hostCode) { throw 'Desktop safety or process-exit evidence is incomplete/inconsistent.' }
    $expectedExit = if ($status -eq 'win') { 0 } elseif ($status -eq 'harness-error') { 10 } else { 20 }
    if ($childCode -ne $expectedExit -or $processExitCode -ne $expectedExit) {
        throw 'Result status and actual process exit disagree.'
    }
    $battleStarted = $BattleStartedFromLog -or $battleStarted
    if ($isWin -ne ($status -eq 'win') -or
        ($status -eq 'win' -and (-not $validBattle -or -not $battleStarted)) -or
        ($validBattle -and -not $battleStarted) -or
        ($status -in @('rejected', 'harness-error') -and $validBattle)) {
        throw 'Contradictory battle/status flags in result.'
    }
    if ($validBattle -and ($actualTakeoverTick -ne $Case.TakeoverTick -or
        (Require-Boolean (Read-Field $Result 'encounterFixtureReady') 'encounterFixtureReady') -ne $true)) {
        throw 'Valid battle did not prove the requested takeover edge and complete fixture.'
    }
    if ($expectedDirectSpawn) {
        foreach ($field in @('directSpawnAttempted','directSpawnCompleted','phaseStageAttempted','phaseStaged','phaseVerifiedAtTakeover')) {
            $value = Require-Boolean (Read-Field $Result $field) "result $field"
            if ($validBattle -and -not $value) { throw "Valid priority Boss battle lacks $field evidence." }
        }
        $spawnTick = Require-Integer (Read-Field $Result 'directSpawnTick') 1 23879 'direct spawn tick'
        if ($spawnTick -ge $Case.TakeoverTick) { throw 'Priority Boss was not active before the requested takeover tick.' }
        $stage = Read-Field $Result 'phaseStage'
        $snapshot = Read-Field $Result 'takeoverNativeSnapshot'
        if ($validBattle -and ((Read-Field $stage 'schema') -cne 'chaite-priority-phase-stage/v1' -or
            (Read-Field $stage 'scenario') -cne $Case.Scenario -or (Read-Field $stage 'phase') -cne $Case.Phase -or
            (Require-Integer (Read-Field $stage 'tick') 120 23880 'phase stage tick') -ne $Case.TakeoverTick -or
            (Require-Boolean (Read-Field $stage 'verifiedBeforeActivation') 'phase verification') -ne $true -or
            (Read-Field $snapshot 'schema') -cne 'chaite-takeover-native-snapshot/v1' -or
            (Read-Field $snapshot 'scenario') -cne $Case.Scenario -or
            (Read-Field $snapshot 'requestedPhase') -cne $Case.Phase -or
            (Require-Integer (Read-Field $snapshot 'tick') 120 23880 'takeover snapshot tick') -ne $Case.TakeoverTick)) {
            throw 'Priority Boss phase-stage/takeover snapshot evidence is missing or inconsistent.'
        }
    } else {
        if ((Require-Integer (Read-Field $Result 'directSpawnTick') -1 -1 'direct spawn tick') -ne -1) {
            throw 'Unstaged readiness evidence has an invalid direct-spawn tick.'
        }
        foreach ($field in @('directSpawnAttempted','directSpawnCompleted','phaseStageAttempted','phaseStaged','phaseVerifiedAtTakeover')) {
            if (Require-Boolean (Read-Field $Result $field) "result $field") {
                throw "Unstaged readiness evidence contains staging provenance: $field."
            }
        }
        foreach ($field in @('phaseStage','takeoverNativeSnapshot')) {
            if ($null -ne (Read-Field $Result $field)) {
                throw "Unstaged readiness evidence contains a staging report: $field."
            }
        }
        if (-not (Require-Boolean (Read-Field $Result 'summonConsumed') 'result summonConsumed')) {
            throw 'Unstaged readiness evidence did not observe native summon consumption.'
        }
        if (-not $expectedSummonTypes.ContainsKey($Case.Scenario)) {
            throw 'Unstaged readiness scenario has no reviewed native summon item.'
        }
        $equipment = Require-ObjectFields (Read-Field $Result 'equipment') @('summonType','summonCount') 'result equipment'
        if ((Require-Integer (Read-Field $equipment 'summonType') 1 65535 'equipment summonType') -ne $expectedSummonTypes[$Case.Scenario] -or
            (Require-Integer (Read-Field $equipment 'summonCount') 1 9999 'equipment summonCount') -ne 1) {
            throw 'Unstaged readiness evidence does not identify the reviewed one-item hotbar summon source.'
        }
    }
    return [pscustomobject]@{
        Status = $status; BattleStarted = $battleStarted; ValidBattle = $validBattle; Win = $isWin
        Deaths = $deaths; HostExitCode = $hostCode; ChildExitCode = $childCode; ProcessExitCode = $processExitCode
        ActualTakeoverTick = $actualTakeoverTick; Variant = $variant.Variant; EvidenceKind = $evidenceKind
        ReadinessEligible = $readinessEligible
    }
}
function Assert-HurtEvidence($Result, $EvidenceInput) {
    $diagnostics = Read-Field $Result 'diagnostics'
    $nativeFrames = Require-Integer (Read-Field $Result 'nativeFrames') 0 24000 'result nativeFrames'
    $hits = Require-Integer (Read-Field $Result 'hits') 0 $nativeFrames 'result hits'
    $poisonedFrames = Require-Integer (Read-Field $diagnostics 'poisonedFrames') 0 $nativeFrames 'diagnostics poisonedFrames'
    $lifeLossFramesWhilePoisoned = Require-Integer (Read-Field $diagnostics 'lifeLossFramesWhilePoisoned') 0 $nativeFrames 'diagnostics lifeLossFramesWhilePoisoned'
    if ($lifeLossFramesWhilePoisoned -gt $poisonedFrames -or $lifeLossFramesWhilePoisoned -gt $hits) {
        throw 'Poisoned life-loss frame count exceeds its poisoned-frame or total life-loss bounds.'
    }
    $hurt = Read-Field $diagnostics 'hurt'
    if ((Read-Field $hurt 'schema') -cne 'chaite-hurt-observation-summary/v1') {
        throw 'Missing or unsupported Hurt observation summary.'
    }
    if ($null -eq $hurt.PSObject.Properties['file']) { throw 'Hurt observation summary omitted its file declaration.' }
    $calls = Require-Integer (Read-Field $hurt 'calls') 0 2048 'Hurt calls'
    $returns = Require-Integer (Read-Field $hurt 'returns') 0 2048 'Hurt returns'
    $rows = Require-Integer (Read-Field $hurt 'rows') 0 2048 'Hurt rows'
    $serialized = Require-Integer (Read-Field $hurt 'serializedRows') 0 2048 'Hurt serializedRows'
    if ($calls -ne $returns -or $calls -ne $rows -or $calls -ne $serialized) {
        throw 'Hurt observations are not one-to-one from call through serialized return.'
    }
    if ((Require-Integer (Read-Field $hurt 'maximumRows') 0 4096 'Hurt maximumRows') -ne 2048 -or
        (Require-Integer (Read-Field $hurt 'depthCapacity') 0 64 'Hurt depthCapacity') -ne 16 -or
        (Require-Integer (Read-Field $hurt 'bufferFlushRows') 0 1024 'Hurt bufferFlushRows') -ne 16 -or
        (Require-Integer (Read-Field $hurt 'bufferFlushCharacters') 0 1048576 'Hurt bufferFlushCharacters') -ne 65536) {
        throw 'Hurt observation fixed bounds changed.'
    }
    $maximumDepth = Require-Integer (Read-Field $hurt 'maximumDepth') 0 16 'Hurt maximumDepth'
    $expectedMaximumDepth = if ($rows -eq 0) { 0 } else { 1 }
    if ($maximumDepth -ne $expectedMaximumDepth) { throw 'Hurt maximum depth disagrees with its completed row count.' }
    foreach ($zeroField in @('droppedRows','depthOverflows','unpairedObservations','pendingDepth',
        'suppressedPendingDepth','observerErrors','sourceReadFailures','bufferedRowsAfterFinalFlush')) {
        if ((Require-Integer (Read-Field $hurt $zeroField) 0 2147483647 "Hurt $zeroField") -ne 0) {
            throw "Hurt observation integrity counter is nonzero: $zeroField"
        }
    }
    $flushes = Require-Integer (Read-Field $hurt 'flushes') 0 2048 'Hurt flushes'
    $maximumBuffered = Require-Integer (Read-Field $hurt 'maximumBufferedCharacters') 0 131072 'Hurt maximumBufferedCharacters'
    $charactersWritten = Require-Integer (Read-Field $hurt 'charactersWritten') 0 16777216 'Hurt charactersWritten'
    $exists = Require-Boolean (Read-Field $EvidenceInput 'Exists') 'Hurt evidence file existence'
    $lines = @(Read-Field $EvidenceInput 'Lines' @())
    $rawCharacters = Require-Integer (Read-Field $EvidenceInput 'RawCharacters') 0 16777216 'Hurt raw character count'
    $declaredFile = Read-Field $hurt 'file'
    # Rows count Player.Hurt calls (including zero-damage returns); result.hits
    # instead counts native frames with a net life decrease. They are not equal.
    if ($rows -eq 0) {
        if ($null -ne $declaredFile -or $exists -or $lines.Count -ne 0 -or $flushes -ne 0 -or
            $maximumBuffered -ne 0 -or $charactersWritten -ne 0 -or $rawCharacters -ne 0) {
            throw 'Empty Hurt observation summary disagrees with its file/buffer evidence.'
        }
        return
    }
    if ($declaredFile -cne 'hurt-observations.jsonl' -or -not $exists -or $lines.Count -ne $rows -or
        $flushes -lt 1 -or $maximumBuffered -lt 1 -or $charactersWritten -lt 1) {
        throw 'Hurt observation file declaration, row count or final flush is inconsistent.'
    }
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        if ($line -isnot [string] -or [string]::IsNullOrWhiteSpace($line)) { throw "Hurt JSONL row $index is empty or not text." }
        if ($line[0] -cne '{' -or $line[$line.Length - 1] -cne '}') { throw "Hurt JSONL row $index is not a root JSON object." }
        try { $row = $line | ConvertFrom-Json }
        catch { throw "Hurt JSONL row $index is not valid JSON." }
        $row = Require-ObjectFields $row @('schema','sequence','tickBefore','tickAfter','reason','request','actualReturn','player','source') "Hurt JSONL row $index"
        if ((Read-Field $row 'schema') -cne 'chaite-hurt-observation/v1' -or
            (Require-Integer (Read-Field $row 'sequence') 1 2048 'Hurt sequence') -ne ($index + 1)) {
            throw "Hurt JSONL row $index has an invalid schema or sequence."
        }
        $tickBefore = Require-Integer (Read-Field $row 'tickBefore') 0 2147483647 'Hurt tickBefore'
        $tickAfter = Require-Integer (Read-Field $row 'tickAfter') 0 2147483647 'Hurt tickAfter'
        if ($tickBefore -ne $tickAfter) { throw "Hurt JSONL row $index crossed native update ticks." }
        $actualReturn = Require-FiniteNumber (Read-Field $row 'actualReturn') 0 ([double]::MaxValue) 'Hurt actualReturn'
        $request = Require-ObjectFields (Read-Field $row 'request') @('damage','hitDirection','pvp','quiet','crit','cooldownCounter','dodgeable') "Hurt JSONL row $index.request"
        $null = Require-Integer (Read-Field $request 'damage') 0 2147483647 'Hurt requested damage'
        $null = Require-Integer (Read-Field $request 'hitDirection') -1 1 'Hurt hit direction'
        $null = Require-Integer (Read-Field $request 'cooldownCounter') -2147483648 2147483647 'Hurt cooldown counter'
        foreach ($flag in @('pvp','quiet','crit','dodgeable')) { $null = Require-Boolean (Read-Field $request $flag) "Hurt request $flag" }
        $player = Require-ObjectFields (Read-Field $row 'player') @('index','lifeBefore','lifeAfter','lifeDelta') "Hurt JSONL row $index.player"
        if ((Require-Integer (Read-Field $player 'index') 0 255 'Hurt player index') -ne 0) { throw 'Hurt observation is not for the isolated local player.' }
        $lifeBefore = Require-Integer (Read-Field $player 'lifeBefore') -2147483648 2147483647 'Hurt lifeBefore'
        $lifeAfter = Require-Integer (Read-Field $player 'lifeAfter') -2147483648 2147483647 'Hurt lifeAfter'
        $lifeDelta = Require-Integer (Read-Field $player 'lifeDelta') 0 2147483647 'Hurt lifeDelta'
        if ([long]$lifeBefore - [long]$lifeAfter -ne [long]$lifeDelta) { throw "Hurt JSONL row $index has an inconsistent life delta." }
        $reason = Require-ObjectFields (Read-Field $row 'reason') @('custom','sourceOtherIndex','declaredProjectileType') "Hurt JSONL row $index.reason"
        $custom = Read-Field $reason 'custom'
        if ($null -ne $custom -and ($custom -isnot [string] -or $custom.Length -gt 240)) { throw "Hurt JSONL row $index has an invalid custom reason." }
        foreach ($name in @('sourceOtherIndex','declaredProjectileType')) {
            $value = Read-Field $reason $name
            if ($null -ne $value) { $null = Require-Integer $value -2147483648 2147483647 "Hurt reason $name" }
        }
        $source = Require-ObjectFields (Read-Field $row 'source') @('kind','entityIndex','type','owner','active','life','lifeMax','damage','hostile','friendly','position','velocity') "Hurt JSONL row $index.source"
        $kind = Read-Field $source 'kind'
        if ($null -ne $kind -and ($kind -isnot [string] -or $kind -cnotin @('player','npc','projectile'))) {
            throw "Hurt JSONL row $index has an invalid public source kind."
        }
        foreach ($name in @('entityIndex','type','owner','life','lifeMax','damage')) {
            $value = Read-Field $source $name
            if ($null -ne $value) { $null = Require-Integer $value -2147483648 2147483647 "Hurt source $name" }
        }
        foreach ($name in @('active','hostile','friendly')) {
            $value = Read-Field $source $name
            if ($null -ne $value) { $null = Require-Boolean $value "Hurt source $name" }
        }
        foreach ($vectorName in @('position','velocity')) {
            $vector = Read-Field $source $vectorName
            if ($null -ne $vector) {
                $vector = Require-ObjectFields $vector @('x','y') "Hurt JSONL row $index.source.$vectorName"
                $null = Require-FiniteNumber (Read-Field $vector 'x') (-[double]::MaxValue) ([double]::MaxValue) "Hurt source $vectorName.x"
                $null = Require-FiniteNumber (Read-Field $vector 'y') (-[double]::MaxValue) ([double]::MaxValue) "Hurt source $vectorName.y"
            }
        }
    }
    $replay = Measure-HurtBufferReplay $lines
    if ($rawCharacters -ne $replay.CharactersWritten -or
        $flushes -ne $replay.Flushes -or
        $maximumBuffered -ne $replay.MaximumBufferedCharacters -or
        $charactersWritten -ne $replay.CharactersWritten -or
        (Require-Integer (Read-Field $hurt 'bufferedRowsAfterFinalFlush') 0 2147483647 'Hurt bufferedRowsAfterFinalFlush') -ne $replay.BufferedRowsAfterFinalFlush) {
        throw 'Hurt observation file text and deterministic buffer counters disagree.'
    }
}
function Read-AndValidateCaseEvidence([string]$RunDirectory, $Case, [bool]$BattleStartedFromLog,
    $HostExitCode, $ChildExitCode, [bool]$DesktopSafe, $Record) {
    $resultPath = Join-Path $RunDirectory 'result.json'
    $result = Read-JsonObjectFile $resultPath 1MB 'result evidence'
    if ($null -ne $Record) { $Record.Result = $result }
    $envelope = Read-ValidatedResultEnvelope $result $Case $BattleStartedFromLog $HostExitCode $ChildExitCode $DesktopSafe
    $validBattle = $envelope.ValidBattle
    $hurtEvidence = $null
    if ($validBattle -eq $true) { $hurtEvidence = Get-HurtEvidenceInput $RunDirectory }
    if ($validBattle) {
        Assert-HurtEvidence $result $hurtEvidence
        # Declared command-line difficulty/seed is not evidence that the
        # native world and its named RNG streams actually use those values.
        $mode = @('classic', 'expert', 'master').IndexOf($Case.Difficulty)
        $native = Read-Field $result 'nativeDifficulty'
        $random = Read-Field $result 'battleRandom'
        $variantNative = Read-Field (Read-Field $result 'variantEvidence') 'native'
        $expectedVariant = Get-CaseVariant $Case
        # Validate JSON types before comparing: PowerShell otherwise accepts
        # strings such as "true" / "1" as genuine Boolean/integer evidence.
        if ((Require-Boolean (Read-Field $result 'nativeDifficultyVerified') 'nativeDifficultyVerified') -ne $true -or
            (Require-Integer (Read-Field $native 'gameMode') 0 2 'native gameMode') -ne $mode -or
            (Require-Integer (Read-Field $native 'worldFileGameMode') 0 2 'native worldFileGameMode') -ne $mode -or
            (Require-Integer (Read-Field $native 'difficulty') 1 3 'native difficulty') -ne ($mode + 1) -or
            (Require-Integer (Read-Field $native 'worldFileSeed') 0 2147483647 'native worldFileSeed') -ne $Case.Seed -or
            (Require-Boolean (Read-Field $native 'expertMode') 'native expertMode') -ne ($mode -gt 0) -or
            (Require-Boolean (Read-Field $native 'masterMode') 'native masterMode') -ne ($mode -eq 2) -or
            (Require-Boolean (Read-Field $native 'hardMode') 'native hardMode') -ne ($Case.Scenario -cin $hardModeScenarios) -or
            (Require-Boolean (Read-Field $native 'forTheWorthy') 'native forTheWorthy') -ne
                (Require-Boolean (Read-Field $variantNative 'forTheWorthy') 'variant/native forTheWorthy') -or
            (Require-Boolean (Read-Field $native 'zenithWorld') 'native zenithWorld') -ne
                (Require-Boolean (Read-Field $variantNative 'zenithWorld') 'variant/native zenithWorld')) {
            throw 'Native difficulty/world seed evidence is missing or disagrees with the requested fixture.'
        }
        foreach ($field in @('drunkWorld', 'notTheBeesWorld', 'remixWorld', 'celebrationWorld',
            'constantWorld', 'noTrapsWorld', 'skyblockWorld')) {
            if ((Require-Boolean (Read-Field $native $field) "native $field") -ne
                (Require-Boolean (Read-Field $variantNative $field) "variant/native $field")) {
                throw 'Native world-rule evidence disagrees with the variant observation.'
            }
        }
        if ($expectedVariant -ne 'getfixedboi-mechdusa' -and -not (Test-StandardWorldRules $variantNative)) {
            throw 'A standard/day/night/mechanical-trio fixture cannot contain secret-world native rules.'
        }
        $nativeFrames = Require-Integer (Read-Field $result 'nativeFrames') 121 24000 'native frames'
        $fingerprint = @(Read-Field $random 'independentTwinFingerprint')
        if ((Require-Boolean (Read-Field $random 'installedAfterSetup') 'random installedAfterSetup') -ne $true -or
            (Require-Boolean (Read-Field $random 'actualAndNativeNamedColdStateVerified') 'random actualAndNativeNamedColdStateVerified') -ne $true -or
            (Require-Boolean (Read-Field $random 'actualStreamConsumedForFingerprint') 'random actualStreamConsumedForFingerprint') -ne $false -or
            (Require-Integer (Read-Field $random 'seed') 0 2147483647 'random seed') -ne $Case.Seed -or
            (Require-Integer (Read-Field $random 'unpausedUpdateSeedInitial') 0 2147483647 'random unpausedUpdateSeedInitial') -ne $Case.Seed -or
            (Require-Integer (Read-Field $random 'unpausedUpdateSeedAdvances') 0 24000 'random unpausedUpdateSeedAdvances') -ne $nativeFrames -or
            (Require-Integer (Read-Field $random 'referenceChecks') 0 2147483647 'random referenceChecks') -lt $nativeFrames -or
            $fingerprint.Count -ne 8) { throw 'Native battle random-stream verification is incomplete/inconsistent.' }
        foreach ($sample in $fingerprint) { $null = Require-Integer $sample 0 2147483647 'random fingerprint sample' }
        $observed = @(Read-Field $result 'firstObservedBosses')
        if ($observed.Count -eq 0) { throw 'No native Boss difficulty observations were supplied.' }
        $observedTypes = [Collections.Generic.HashSet[int]]::new()
        foreach ($boss in $observed) {
            $null = $observedTypes.Add((Require-Integer (Read-Field $boss 'type') 0 9999 'Boss type'))
            if ((Require-Integer (Read-Field $boss 'lifeMax') 1 2147483647 'Boss lifeMax') -le 0 -or
                (Require-Integer (Read-Field $boss 'gameMode') 0 2 'Boss gameMode') -ne $mode -or
                (Require-Integer (Read-Field $boss 'difficulty') 1 3 'Boss difficulty') -ne ($mode + 1) -or
                (Require-Integer (Read-Field $boss 'npcDifficulty') 1 3 'Boss npcDifficulty') -ne ($mode + 1)) {
                throw 'Native spawned Boss difficulty is inconsistent with the case.'
            }
        }
        foreach ($expectedType in @($expectedBossTypes[$Case.Scenario])) {
            if (-not $observedTypes.Contains([int]$expectedType)) { throw "Expected native Boss type $expectedType was never observed." }
        }
    }
    return [pscustomobject]@{ Result = $result; Envelope = $envelope }
}
function Get-Rate([int]$Wins, [int]$Denominator) {
    if ($Denominator -eq 0) { return $null }
    return [Math]::Round(100.0 * $Wins / $Denominator, 2)
}

if ($PSBoundParameters.ContainsKey('Cases')) {
    if ($PSBoundParameters.ContainsKey('Suite')) { throw 'Choose a case file or a built-in suite, not both.' }
    if ([string]::IsNullOrWhiteSpace($Cases) -or -not [IO.Path]::IsPathRooted($Cases)) { throw '-Cases must be an explicit absolute project JSON path.' }
    $Cases = [IO.Path]::GetFullPath($Cases)
    if (-not $Cases.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($Cases) -ine '.json') { throw '-Cases must identify a JSON file inside this project.' }
    Assert-NoReparse $Cases
    $caseFile = Get-Item -LiteralPath $Cases
    if ($caseFile.PSIsContainer -or $caseFile.Length -gt 256KB) { throw 'The case plan must be a file no larger than 256 KiB.' }
    $document = Get-Content -LiteralPath $Cases -Raw -Encoding UTF8 | ConvertFrom-Json
    $planSchema = Read-Field $document 'schema'
    if ($planSchema -cnotin @('chaite-boss-cases/v1','chaite-boss-cases/v2')) { throw 'Unsupported case-plan schema.' }
    foreach ($property in $document.PSObject.Properties.Name) { if ($property -cnotin @('schema', 'cases')) { throw "Unknown plan field: $property" } }
    $rawCases = @(Read-Field $document 'cases')
    $planSource = $Cases
} else {
    $planSchema = 'chaite-boss-cases/v2'
    if ($Suite -eq 'priority24') {
        $priorityOpening = @{
            'deerclops'='opening'; 'skeletron'='hover'; 'queen-bee'='choose'; 'wall-of-flesh'='runway'
            'duke-fishron'='p1-hover'; 'empress-night'='p1-reposition'; 'empress-day'='p1-reposition'; 'moon-lord'='intro'
        }
        $rawCases = @(foreach ($scenario in $priorityScenarios) { foreach ($difficulty in @('classic','expert','master')) {
            [pscustomobject]@{ scenario=$scenario; phase=$priorityOpening[$scenario]; takeoverTick=240; seed=20260910; difficulty=$difficulty }
        } })
    } elseif ($Suite -eq 'priority-organic6') {
        $rawCases = @(foreach ($scenario in @('deerclops','queen-bee')) { foreach ($difficulty in @('classic','expert','master')) {
            [pscustomobject]@{ scenario=$scenario; phase='summon'; takeoverTick=120; seed=20260910; difficulty=$difficulty }
        } })
    } else {
        $suiteScenarios = @('eye','king-slime','queen-slime','destroyer','twins','prime')
        $seeds = if ($Suite -eq 'smoke6') { @(20260910) } else { @(20260910, 20260911, 20260912) }
        $rawCases = @(foreach ($scenario in $suiteScenarios) { foreach ($seed in $seeds) {
            [pscustomobject]@{ scenario=$scenario; phase='summon'; takeoverTick=120; seed=$seed; difficulty='classic' }
        } })
    }
    $planSource = 'builtin:' + $Suite
}
if ($rawCases.Count -lt 1 -or $rawCases.Count -gt $MaximumCases) { throw "Plan must contain 1..$MaximumCases cases; larger batches require an explicit bounded MaximumCases (at most 180)." }
$plan = @(for ($index = 0; $index -lt $rawCases.Count; $index++) {
    $case = $rawCases[$index]
    foreach ($property in $case.PSObject.Properties.Name) { if ($property -cnotin @('scenario', 'variant', 'phase', 'takeoverTick', 'seed', 'difficulty', 'maxTicks', 'wallSeconds')) { throw "Unknown case field: $property" } }
    $scenario = Read-Field $case 'scenario'
    if ($scenario -cnotin $scenarios) { throw 'Case scenario is not in the reviewed fixture catalog; legacy eye-baseline is deliberately excluded.' }
    $seed = Require-Integer (Read-Field $case 'seed') 0 2147483647 'seed'
    $difficulty = Read-Field $case 'difficulty' 'classic'
    if ($difficulty -cnotin @('classic', 'expert', 'master')) { throw 'Invalid case difficulty.' }
    if (-not $scenarioVariants.ContainsKey($scenario)) { throw 'Case scenario has no reviewed variant identity.' }
    $variant = Require-ResultVariant (Read-Field $case 'variant' $scenarioVariants[$scenario]) 'case variant'
    if ($variant -cne $scenarioVariants[$scenario]) { throw 'Case variant does not match the reviewed scenario identity.' }
    $isPriority = $scenario -cin $priorityScenarios
    if ($planSchema -ceq 'chaite-boss-cases/v1' -and $isPriority) { throw 'Priority Boss fixtures require chaite-boss-cases/v2.' }
    if ($isPriority -and ($null -eq $case.PSObject.Properties['phase'] -or $null -eq $case.PSObject.Properties['takeoverTick'])) {
        throw 'Priority Boss cases require explicit phase and takeoverTick fields.'
    }
    $phase = Read-Field $case 'phase' 'summon'
    if ($phase -isnot [string] -or $phase -cnotin $scenarioPhases[$scenario]) { throw 'Case phase is not reviewed for the selected scenario.' }
    $takeoverTick = Require-Integer (Read-Field $case 'takeoverTick' 120) 120 23880 'takeoverTick'
    $maxTicks = Require-Integer (Read-Field $case 'maxTicks' 24000) 600 24000 'maxTicks'
    if ($takeoverTick -ge $maxTicks - 120) { throw 'takeoverTick must leave at least 120 native frames before maxTicks.' }
    if ($scenario -ceq 'duke-fishron' -and $phase -clike 'p3-*' -and $difficulty -ceq 'classic') {
        throw 'Duke Fishron phase 3 is not a Classic native phase.'
    }
    [pscustomobject]@{
        Id = ('case{0:D3}' -f ($index + 1)); Scenario = $scenario; Phase = $phase; TakeoverTick = $takeoverTick
        Seed = $seed; Difficulty = $difficulty; Variant = $variant; MaxTicks = $maxTicks
        WallSeconds = (Require-Integer (Read-Field $case 'wallSeconds' 90) 15 900 'wallSeconds')
    }
})
Write-Output ("Plan: $($plan.Count) serial cases from $planSource")
$plan | Format-Table Id, Scenario, Variant, Phase, TakeoverTick, Seed, Difficulty, MaxTicks, WallSeconds | Out-String | Write-Output
Write-Output 'Results measure only these fixture/loadout/seed combinations. Overall attempted success and started-battle success use separate denominators.'
if (-not $Run) {
    Write-Output 'PLAN ONLY: no output files, preparation, game processes or desktop windows were created. Pass -Run to execute this explicit plan.'
    return
}

$batchId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $artifactPrefix ('boss-validation-' + $batchId) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Batch outputs must be a named project artifacts subdirectory.' }
Assert-NoReparse $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Batch output already exists; preserve evidence and choose a fresh directory.' }
$inputPaths = [ordered]@{
    InputPluginSha256 = 'src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll'
    InputCoreSha256 = 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'
    ProbeSourceSha256 = 'tools\GameProbe.cs'; ProbePatcherSourceSha256 = 'tools\GameProbePatcher.cs'
    PrepareGameProbeScriptSha256 = 'tools\prepare-game-probe.ps1'
    StartIsolatedTestScriptSha256 = 'tools\start-isolated-test.ps1'
    RunBossValidationScriptSha256 = 'tools\run-boss-validation.ps1'
    TestBossEvidenceScriptSha256 = 'tools\test-boss-evidence.ps1'
    DesktopHostSourceSha256 = 'tools\Chaite.DesktopHost.cs'
}
$expectedHashes = [ordered]@{}
foreach ($key in $inputPaths.Keys) {
    $inputPath = Join-Path $projectRoot $inputPaths[$key]
    Assert-NoReparse $inputPath
    $expectedHashes[$key] = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
@{ Schema = 'chaite-boss-batch-plan/v1'; CreatedUtc = [DateTime]::UtcNow.ToString('o'); PlanSource = $planSource; Provenance = $expectedHashes; Cases = $plan } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'plan.json') -Encoding UTF8
$records = [Collections.Generic.List[object]]::new()

function Save-Summary {
    $wins = @($records | Where-Object { $_.Classification -eq 'win' }).Count
    $started = @($records | Where-Object { $_.BattleStarted }).Count
    $valid = @($records | Where-Object { $_.ValidBattle }).Count
    $readinessEligible = @($records | Where-Object { $_.ReadinessEligible }).Count
    $readinessEligibleWins = @($records | Where-Object { $_.ReadinessEligible -and $_.Classification -eq 'win' }).Count
    $stagedFixtureWins = @($records | Where-Object { -not $_.ReadinessEligible -and $_.Classification -eq 'win' }).Count
    $summary = [ordered]@{
        Schema = 'chaite-boss-batch-summary/v1'; UpdatedUtc = [DateTime]::UtcNow.ToString('o'); PlanSource = $planSource
        Planned = $plan.Count; Attempted = $records.Count; Pending = $plan.Count - $records.Count
        Wins = $wins; Losses = @($records | Where-Object { $_.Classification -eq 'loss' }).Count
        Rejected = @($records | Where-Object { $_.Classification -eq 'rejected' }).Count
        HarnessErrors = @($records | Where-Object { $_.Classification -in @('harness-error', 'prepare-error', 'build-changed') }).Count
        InvalidEvidence = @($records | Where-Object { $_.Classification -eq 'invalid-evidence' }).Count
        HostErrors = @($records | Where-Object { $_.Classification -eq 'host-error' }).Count
        HostTimeouts = @($records | Where-Object { $_.Classification -eq 'host-timeout' }).Count
        Timeouts = @($records | Where-Object { $_.Classification -eq 'timeout' }).Count
        CombatTimeouts = @($records | Where-Object { $_.Classification -eq 'timeout' -and $_.BattleStarted }).Count
        PreBattleTimeouts = @($records | Where-Object { $_.Classification -eq 'timeout' -and -not $_.BattleStarted }).Count
        StartedBattles = $started; ValidBattles = $valid; CasesWithDeaths = @($records | Where-Object { $_.Deaths -gt 0 }).Count
        OverallAttemptSuccessPercent = (Get-Rate $wins $records.Count)
        StartedBattleSuccessPercent = (Get-Rate $wins $started)
        ValidBattleSuccessPercent = (Get-Rate $wins $valid)
        ReadinessEligibleCases = $readinessEligible; ReadinessEligibleWins = $readinessEligibleWins
        ReadinessEligibleSuccessPercent = (Get-Rate $readinessEligibleWins $readinessEligible)
        StagedFixtureRegressionWins = $stagedFixtureWins
        ByScenarioPhase = @(foreach ($group in ($records | Group-Object Scenario, Variant, Difficulty, Phase, TakeoverTick)) {
            $groupWins = @($group.Group | Where-Object { $_.Classification -eq 'win' }).Count
            $groupStarted = @($group.Group | Where-Object { $_.BattleStarted }).Count
            $groupEligible = @($group.Group | Where-Object { $_.ReadinessEligible }).Count
            $groupEligibleWins = @($group.Group | Where-Object { $_.ReadinessEligible -and $_.Classification -eq 'win' }).Count
            [pscustomobject]@{
                Scenario = $group.Group[0].Scenario; Variant = $group.Group[0].Variant; Difficulty = $group.Group[0].Difficulty
                Phase = $group.Group[0].Phase; TakeoverTick = $group.Group[0].TakeoverTick
                Attempted = $group.Count; Started = $groupStarted; Wins = $groupWins
                OverallAttemptSuccessPercent = (Get-Rate $groupWins $group.Count)
                StartedBattleSuccessPercent = (Get-Rate $groupWins $groupStarted)
                ReadinessEligibleCases = $groupEligible; ReadinessEligibleWins = $groupEligibleWins
                ReadinessEligibleSuccessPercent = (Get-Rate $groupEligibleWins $groupEligible)
            }
        })
        DenominatorNote = 'Overall includes EVERY attempted case, including rejected, harness/host errors and timeouts. Started includes any observed boss arrival, even if later evidence fails. Valid excludes harness errors, never ordinary losses or combat timeouts.'
        GeneralizationNote = 'Small, fixed synthetic arena/loadout/seed samples are not general gameplay win rates or guaranteed victories. Staged native phase fixtures are regression evidence only and are excluded from readiness-eligible wins. No automatic retries or successful-case selection.'
        Cases = @($records.ToArray())
    }
    $summary | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
    $records | Select-Object Id, Scenario, Variant, EvidenceKind, ReadinessEligible, Phase, TakeoverTick, ActualTakeoverTick, Seed, Difficulty, Classification, BattleStarted, ValidBattle, Deaths, HostExitCode, ChildExitCode, DesktopSafe, `
        InputPluginSha256, InputCoreSha256, StartIsolatedTestScriptSha256, DesktopHostSourceSha256, RunId, ManifestSha256, StaticEvidenceSha256, `
        PinPlanSha256, LaunchBindingSha256, HostLockCompletionSha256, PreparedTerrariaSha256, PreparedGameProbeSha256, PreparedDesktopHostSha256, `
        PinnedPreparedFileCount, HostCompletedFileCount, HostLockedFileCount, Failure, RunDirectory |
        Export-Csv -LiteralPath (Join-Path $OutputDirectory 'summary.csv') -NoTypeInformation -Encoding UTF8
}

foreach ($case in $plan) {
    $runName = 'game-probe-batch-' + $batchId + '-' + $case.Id
    $runDirectory = Join-Path $artifactPrefix $runName
    $hostDirectory = Join-Path $OutputDirectory ($case.Id + '-desktop')
    $record = [ordered]@{
        Id = $case.Id; Scenario = $case.Scenario; Variant = $case.Variant; EvidenceKind = $null; ReadinessEligible = $false
        Phase = $case.Phase; TakeoverTick = $case.TakeoverTick; ActualTakeoverTick = $null
        Seed = $case.Seed; Difficulty = $case.Difficulty
        Classification = 'prepare-error'; BattleStarted = $false; ValidBattle = $false; Deaths = 0
        HostExitCode = $null; ChildExitCode = $null; DesktopSafe = $false; Failure = $null
        RunDirectory = $runDirectory; HostDirectory = $hostDirectory; Result = $null
        InputPluginSha256 = $null; InputCoreSha256 = $null; ProbeSourceSha256 = $null; ProbePatcherSourceSha256 = $null
        PrepareGameProbeScriptSha256 = $null; StartIsolatedTestScriptSha256 = $null; RunBossValidationScriptSha256 = $null
        TestBossEvidenceScriptSha256 = $null; DesktopHostSourceSha256 = $null
        RunId = $null; ManifestSha256 = $null; StaticEvidenceSha256 = $null; PinPlanSha256 = $null
        LaunchBindingSha256 = $null; HostLockCompletionSha256 = $null
        PreparedTerrariaSha256 = $null; PreparedGameProbeSha256 = $null; PreparedDesktopHostSha256 = $null
        PinnedPreparedFileCount = $null; HostCompletedFileCount = $null; HostLockedFileCount = $null
    }
    Write-Output ("Starting $($case.Id): $($case.Scenario)/$($case.Phase), takeover $($case.TakeoverTick), seed $($case.Seed), $($case.Difficulty) (one isolated process at a time)")
    try {
        foreach ($key in $inputPaths.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $inputPaths[$key]) -Algorithm SHA256).Hash -cne $expectedHashes[$key]) {
                $record.Classification = 'build-changed'
                throw 'Build or harness changed during this batch; this case was not launched.'
            }
        }
        & (Join-Path $PSScriptRoot 'prepare-game-probe.ps1') -GameDirectory $GameDirectory -RunName $runName -Headless |
            Tee-Object -FilePath (Join-Path $OutputDirectory ($case.Id + '-prepare.log')) | Write-Output
        $manifest = Read-JsonObjectFile (Join-Path $runDirectory 'probe-manifest.json') 64MB 'prepared batch manifest'
        $batchManifestFields = @('Schema', 'RunId', 'CreatedUtc', 'RunName', 'StaticEvidenceFile', 'Mode', 'GameVersion',
            'SourceGameSha256', 'SaveDirectory', 'SocialDisabled', 'LaunchGuardBeforeLogging', 'SyntheticHotkeysOnly',
            'InputPluginSha256', 'InputCoreSha256', 'ProbeSourceSha256', 'ProbePatcherSourceSha256',
            'PrepareGameProbeScriptSha256', 'StartIsolatedTestScriptSha256', 'RunBossValidationScriptSha256',
            'TestBossEvidenceScriptSha256', 'DesktopHostSourceSha256', 'Files')
        $null = Require-ExactObjectFields $manifest $batchManifestFields 'prepared batch manifest'
        $null = Assert-OrdinalString $manifest.Schema 'chaite-isolated-probe/v2' 'prepared batch manifest Schema'
        foreach ($key in $expectedHashes.Keys) {
            $declared = Require-Sha256 $manifest.$key "prepared batch manifest $key"
            $record[$key] = $declared
            if (-not [string]::Equals($declared, $expectedHashes[$key], [StringComparison]::Ordinal)) {
                $record.Classification = 'build-changed'
                throw 'Prepared case differs from fixed batch provenance.'
            }
        }
        # The 55,680-sample native beam audit is an independent geometry
        # regression and must not be repeated during every Boss case. Skipping
        # it here changes no world, AI, physics, control, or combat input.
        $arguments = @('-scenario', $case.Scenario, '-phase', $case.Phase, '-takeovertick', [string]$case.TakeoverTick,
            '-seed', [string]$case.Seed, '-difficulty', $case.Difficulty, '-maxticks', [string]$case.MaxTicks, '-wallseconds', [string]$case.WallSeconds,
            '-skipbeam')
        $record.Classification = 'host-error'
        try {
            & (Join-Path $PSScriptRoot 'start-isolated-test.ps1') -TargetExe (Join-Path $runDirectory 'Terraria.exe') -TargetArguments $arguments -TimeoutSeconds $TimeoutSeconds -OutputDirectory $hostDirectory |
                Tee-Object -FilePath (Join-Path $OutputDirectory ($case.Id + '-launch.log')) | Write-Output
        } catch { $record.Failure = $_.Exception.Message }

        $exitPath = Join-Path $hostDirectory 'desktop-exit.json'
        if (Test-Path -LiteralPath $exitPath -PathType Leaf) {
            $exitRecord = Get-Content -LiteralPath $exitPath -Raw -Encoding UTF8 | ConvertFrom-Json
            $record.HostExitCode = Read-DesktopExitCode $exitRecord
        }
        $hostLogPath = Join-Path $hostDirectory 'desktop-host.log'
        $hostLog = if (Test-Path -LiteralPath $hostLogPath -PathType Leaf) { Get-Content -LiteralPath $hostLogPath -Raw -Encoding UTF8 } else { '' }
        $lockMatches = [regex]::Matches($hostLog, 'Verified and locked launch pin plan plus ([0-9]+) prepared files through child exit\.')
        if ($lockMatches.Count -eq 1) { $record.HostLockedFileCount = [int]::Parse($lockMatches[0].Groups[1].Value, [Globalization.CultureInfo]::InvariantCulture) }
        $record.DesktopSafe = $hostLog -match 'Input desktop unchanged; no test process took foreground;' -and
            $hostLog -notmatch '\bFAIL\b' -and $lockMatches.Count -eq 1
        $exitMatches = [regex]::Matches($hostLog, 'Child exit=(\d+);')
        if ($exitMatches.Count -eq 1) { $record.ChildExitCode = [long]::Parse($exitMatches[0].Groups[1].Value) }
        $gameLogPath = Join-Path $runDirectory 'game-probe.log'
        if (Test-Path -LiteralPath $gameLogPath -PathType Leaf) {
            $gameLog = Get-Content -LiteralPath $gameLogPath -Raw -Encoding UTF8
            $record.BattleStarted = $gameLog -match 'state=EngagedAlive|STATE EngagedAlive|bosses=[1-9]'
        }
        if ($record.HostExitCode -eq 124 -or $hostLog -match 'TIMEOUT:') { $record.Classification = 'host-timeout'; throw 'Isolated host timed out; this is not a completed valid combat observation.' }
        $record.Classification = 'invalid-evidence'
        $launchEvidence = Read-AndValidateLaunchEvidence (Join-Path $hostDirectory 'launch-binding.json') $runDirectory $record
        foreach ($field in @('RunId', 'ManifestSha256', 'StaticEvidenceSha256', 'PinPlanSha256', 'LaunchBindingSha256',
            'HostLockCompletionSha256', 'PreparedTerrariaSha256', 'PreparedGameProbeSha256', 'PreparedDesktopHostSha256',
            'PinnedPreparedFileCount', 'HostCompletedFileCount')) {
            $record[$field] = $launchEvidence.$field
        }
        if ($record.HostLockedFileCount -ne $record.PinnedPreparedFileCount -or
            $record.HostCompletedFileCount -ne $record.PinnedPreparedFileCount) {
            throw 'DesktopHost log or completion lock count disagrees with the launch binding.'
        }
        $validatedCase = Read-AndValidateCaseEvidence $runDirectory $case $record.BattleStarted $record.HostExitCode $record.ChildExitCode $record.DesktopSafe $record
        $result = $validatedCase.Result
        $envelope = $validatedCase.Envelope
        $status = $envelope.Status
        $record.BattleStarted = $envelope.BattleStarted
        $record.Deaths = $envelope.Deaths
        $record.HostExitCode = $envelope.HostExitCode
        $record.ChildExitCode = $envelope.ChildExitCode
        $record.ActualTakeoverTick = $envelope.ActualTakeoverTick
        $record.Variant = $envelope.Variant
        $record.EvidenceKind = $envelope.EvidenceKind
        $record.ReadinessEligible = $envelope.ReadinessEligible
        $validBattle = $envelope.ValidBattle
        $record.ValidBattle = $validBattle
        $record.Classification = $status
        $record.Failure = Read-Field $result 'failure'
    } catch {
        $record.ValidBattle = $false
        $record.Failure = $_.Exception.Message
    } finally {
        $records.Add([pscustomobject]$record)
        Save-Summary
        Write-Output ("$($case.Id): $($record.Classification); started=$($record.BattleStarted); valid=$($record.ValidBattle); deaths=$($record.Deaths)")
    }
}
Write-Output ("Batch complete: $OutputDirectory")
Write-Output 'Read summary.json for both overall-attempt and started-battle denominators; failures and rejected cases are retained, never retried away.'

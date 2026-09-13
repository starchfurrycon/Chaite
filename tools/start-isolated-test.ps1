param(
    [string]$TargetExe,
    [string[]]$TargetArguments = @(),
    [ValidateRange(120, 960)][int]$TimeoutSeconds = 120,
    [switch]$Probe,
    [string]$OutputDirectory
)

# Does not install or select a real Terraria.exe. Only a freshly prepared,
# manifest-checked harness can run. A private desktop alone does NOT isolate
# filesystem/network/audio; the harness and strict launch arguments do that.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Assert-NoReparse([string]$Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($candidate)) {
        $entry = Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Reparse points are not allowed in probe paths: $candidate" }
        $parent = [IO.Directory]::GetParent($candidate)
        if ($null -eq $parent) { break }
        $candidate = $parent.FullName
    }
}
function Get-RequiredFileSha256([string]$Path, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Missing $Name file." }
    $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
    if ($hash -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid $Name SHA256." }
    return $hash
}

function Require-ProbeJsonString($Value, [string]$Name) {
    if ($Value -isnot [string]) { throw "Invalid $Name; expected a JSON string scalar." }
    return [string]$Value
}
function Require-ProbeJsonBoolean($Value, [string]$Name) {
    if ($Value -isnot [bool]) { throw "Invalid $Name; expected a JSON Boolean scalar." }
    return [bool]$Value
}
function Require-ProbeJsonArray($Value, [string]$Name) {
    if ($Value -isnot [Array]) { throw "Invalid $Name; expected a JSON array." }
    return $Value
}
function Require-ProbeSha256($Value, [string]$Name) {
    $text = Require-ProbeJsonString $Value $Name
    if ($text -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid $Name; expected an uppercase SHA256 string." }
    return $text
}
function Assert-ProbeStringEquals($Value, [string]$Expected, [string]$Name) {
    $text = Require-ProbeJsonString $Value $Name
    if (-not [string]::Equals($text, $Expected, [StringComparison]::Ordinal)) {
        throw "Invalid $Name value."
    }
    return $text
}
function Require-ProbeExactFields($Value, [string[]]$Fields, [string]$Name) {
    if ($null -eq $Value -or $Value -isnot [pscustomobject]) { throw "Invalid $Name; expected one JSON object." }
    $declared = @($Value.PSObject.Properties.Name)
    if ($declared.Count -ne $Fields.Count) { throw "$Name field set changed." }
    foreach ($field in $Fields) {
        if ($declared -cnotcontains $field) { throw "Missing $Name field: $field" }
    }
    return $Value
}
function Read-ProbeStrictUtf8JsonObject([string]$Path, [long]$MaximumBytes, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Missing $Name file." }
    $stream = [IO.File]::Open($fullPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
    try {
        if ($stream.Length -lt 2 -or $stream.Length -gt $MaximumBytes) { throw "$Name file has an invalid bounded size." }
        $utf8 = [Text.UTF8Encoding]::new($false, $true)
        $reader = [IO.StreamReader]::new($stream, $utf8, $true, 4096, $true)
        try { $raw = $reader.ReadToEnd() }
        catch { throw "$Name is not strict UTF-8 text." }
        finally { $reader.Dispose() }
    } finally { $stream.Dispose() }
    $trimmed = $raw.Trim()
    if ($trimmed.Length -lt 2 -or $trimmed[0] -cne '{' -or $trimmed[$trimmed.Length - 1] -cne '}') {
        throw "$Name must contain one root JSON object."
    }
    try { $value = ConvertFrom-Json -InputObject $trimmed }
    catch { throw "$Name is not valid JSON." }
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "$Name must contain one root JSON object." }
    return $value
}

function Read-AndValidatePreparedProbe([string]$PreparedTerrariaPath, [string]$ProjectRoot, [string]$ToolsRoot) {
    $project = [IO.Path]::GetFullPath($ProjectRoot)
    $tools = [IO.Path]::GetFullPath($ToolsRoot)
    $artifacts = [IO.Path]::GetFullPath((Join-Path $project 'artifacts'))
    $artifactPrefix = $artifacts + [IO.Path]::DirectorySeparatorChar
    $target = [IO.Path]::GetFullPath($PreparedTerrariaPath)
    Assert-NoReparse $project
    Assert-NoReparse $tools
    Assert-NoReparse $target
    if (-not $target.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or
        [IO.Path]::GetFileName($target) -cne 'Terraria.exe' -or
        -not (Test-Path -LiteralPath $target -PathType Leaf)) {
        throw 'Only a prepared Terraria.exe inside project artifacts may run.'
    }
    $run = [IO.Path]::GetDirectoryName($target)
    $runName = [IO.Path]::GetFileName($run)
    if ($runName -cnotmatch '^game-probe-[a-zA-Z0-9-]+$') { throw 'Invalid prepared run directory identity.' }
    $manifestPath = Join-Path $run 'probe-manifest.json'
    $manifest = Read-ProbeStrictUtf8JsonObject $manifestPath 64MB 'probe manifest'
    $manifestFields = @('Schema', 'RunId', 'CreatedUtc', 'RunName', 'StaticEvidenceFile', 'Mode', 'GameVersion',
        'SourceGameSha256', 'SaveDirectory', 'SocialDisabled', 'LaunchGuardBeforeLogging', 'SyntheticHotkeysOnly',
        'InputPluginSha256', 'InputCoreSha256', 'ProbeSourceSha256', 'ProbePatcherSourceSha256',
        'PrepareGameProbeScriptSha256', 'StartIsolatedTestScriptSha256', 'RunBossValidationScriptSha256',
        'TestBossEvidenceScriptSha256', 'DesktopHostSourceSha256', 'Files')
    $null = Require-ProbeExactFields $manifest $manifestFields 'probe manifest'
    $null = Assert-ProbeStringEquals $manifest.Schema 'chaite-isolated-probe/v2' 'probe manifest Schema'
    $null = Assert-ProbeStringEquals $manifest.GameVersion '1.4.5.8' 'probe manifest GameVersion'
    $null = Assert-ProbeStringEquals $manifest.SourceGameSha256 '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3' 'probe manifest SourceGameSha256'
    $null = Assert-ProbeStringEquals $manifest.RunName $runName 'probe manifest RunName'
    $null = Assert-ProbeStringEquals $manifest.StaticEvidenceFile 'probe-static-evidence.json' 'probe manifest StaticEvidenceFile'
    $mode = Require-ProbeJsonString $manifest.Mode 'probe manifest Mode'
    if ($mode -cnotin @('headless', 'client')) { throw 'Invalid probe manifest Mode value.' }
    $null = Assert-ProbeStringEquals $manifest.SaveDirectory 'Save' 'probe manifest SaveDirectory'
    foreach ($field in @('SocialDisabled', 'LaunchGuardBeforeLogging', 'SyntheticHotkeysOnly')) {
        if ((Require-ProbeJsonBoolean $manifest.$field "probe manifest $field") -ne $true) {
            throw "Probe manifest $field must be true."
        }
    }
    $runIdText = Require-ProbeJsonString $manifest.RunId 'probe manifest RunId'
    $runId = [guid]::Empty
    if (-not [guid]::TryParse($runIdText, [ref]$runId) -or $runId -eq [guid]::Empty) { throw 'Invalid probe manifest RunId.' }
    $createdUtc = Require-ProbeJsonString $manifest.CreatedUtc 'probe manifest CreatedUtc'
    $created = [DateTimeOffset]::MinValue
    if (-not [DateTimeOffset]::TryParse($createdUtc, [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::RoundtripKind, [ref]$created)) { throw 'Invalid probe manifest CreatedUtc.' }

    foreach ($field in @('InputPluginSha256', 'InputCoreSha256', 'ProbeSourceSha256', 'ProbePatcherSourceSha256',
        'PrepareGameProbeScriptSha256', 'StartIsolatedTestScriptSha256', 'RunBossValidationScriptSha256',
        'TestBossEvidenceScriptSha256', 'DesktopHostSourceSha256')) {
        $null = Require-ProbeSha256 $manifest.$field "probe manifest $field"
    }

    $staticEvidencePath = Join-Path $run 'probe-static-evidence.json'
    $staticEvidence = Read-ProbeStrictUtf8JsonObject $staticEvidencePath 64KB 'probe static evidence'
    $staticFields = @('Schema', 'RunId', 'RunName', 'IlValidationPassed', 'ManifestSha256', 'PreparedTerrariaSha256',
        'PreparedGameProbeSha256', 'PreparedDesktopHostSha256', 'PrepareGameProbeScriptSha256',
        'StartIsolatedTestScriptSha256', 'GameProbeSourceSha256', 'GameProbePatcherSourceSha256',
        'DesktopHostSourceSha256', 'RunBossValidationScriptSha256', 'TestBossEvidenceScriptSha256')
    $null = Require-ProbeExactFields $staticEvidence $staticFields 'probe static evidence'
    $null = Assert-ProbeStringEquals $staticEvidence.Schema 'chaite-probe-static-evidence/v2' 'probe static evidence Schema'
    $null = Assert-ProbeStringEquals $staticEvidence.RunId $runIdText 'probe static evidence RunId'
    $null = Assert-ProbeStringEquals $staticEvidence.RunName $runName 'probe static evidence RunName'
    if ((Require-ProbeJsonBoolean $staticEvidence.IlValidationPassed 'probe static evidence IlValidationPassed') -ne $true) {
        throw 'Probe static evidence did not record successful IL validation.'
    }
    foreach ($field in @($staticFields | Where-Object { $_ -like '*Sha256' })) {
        $null = Require-ProbeSha256 $staticEvidence.$field "probe static evidence $field"
    }

    $sourceBindings = [ordered]@{
        PrepareGameProbeScriptSha256 = Join-Path $tools 'prepare-game-probe.ps1'
        StartIsolatedTestScriptSha256 = Join-Path $tools 'start-isolated-test.ps1'
        GameProbeSourceSha256 = Join-Path $tools 'GameProbe.cs'
        GameProbePatcherSourceSha256 = Join-Path $tools 'GameProbePatcher.cs'
        DesktopHostSourceSha256 = Join-Path $tools 'Chaite.DesktopHost.cs'
        RunBossValidationScriptSha256 = Join-Path $tools 'run-boss-validation.ps1'
        TestBossEvidenceScriptSha256 = Join-Path $tools 'test-boss-evidence.ps1'
    }
    $manifestSourceNames = [ordered]@{
        PrepareGameProbeScriptSha256 = 'PrepareGameProbeScriptSha256'
        StartIsolatedTestScriptSha256 = 'StartIsolatedTestScriptSha256'
        GameProbeSourceSha256 = 'ProbeSourceSha256'
        GameProbePatcherSourceSha256 = 'ProbePatcherSourceSha256'
        DesktopHostSourceSha256 = 'DesktopHostSourceSha256'
        RunBossValidationScriptSha256 = 'RunBossValidationScriptSha256'
        TestBossEvidenceScriptSha256 = 'TestBossEvidenceScriptSha256'
    }
    foreach ($field in $sourceBindings.Keys) {
        $actual = Get-RequiredFileSha256 $sourceBindings[$field] $field
        if (-not [string]::Equals((Require-ProbeSha256 $staticEvidence.$field "probe static evidence $field"), $actual, [StringComparison]::Ordinal) -or
            -not [string]::Equals((Require-ProbeSha256 $manifest.($manifestSourceNames[$field]) "probe manifest $($manifestSourceNames[$field])"), $actual, [StringComparison]::Ordinal)) {
            throw "Prepared provenance differs from the current physical file: $field"
        }
    }

    $manifestSha = Get-RequiredFileSha256 $manifestPath 'probe manifest'
    $preparedTerrariaSha = Get-RequiredFileSha256 $target 'prepared Terraria'
    $preparedProbePath = Join-Path $run 'Chaite.GameProbe.dll'
    $preparedProbeSha = Get-RequiredFileSha256 $preparedProbePath 'prepared GameProbe'
    $desktopHostPath = Join-Path $run 'Chaite.DesktopHost.exe'
    $preparedDesktopHostSha = Get-RequiredFileSha256 $desktopHostPath 'prepared DesktopHost'
    foreach ($comparison in @(
        @($staticEvidence.ManifestSha256, $manifestSha, 'ManifestSha256'),
        @($staticEvidence.PreparedTerrariaSha256, $preparedTerrariaSha, 'PreparedTerrariaSha256'),
        @($staticEvidence.PreparedGameProbeSha256, $preparedProbeSha, 'PreparedGameProbeSha256'),
        @($staticEvidence.PreparedDesktopHostSha256, $preparedDesktopHostSha, 'PreparedDesktopHostSha256')
    )) {
        $declared = Require-ProbeSha256 $comparison[0] "probe static evidence $($comparison[2])"
        if (-not [string]::Equals($declared, [string]$comparison[1], [StringComparison]::Ordinal)) {
            throw "Probe static evidence physical binding changed: $($comparison[2])"
        }
    }

    foreach ($entry in Get-ChildItem -LiteralPath $run -Recurse -Force) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked probe descendants are not allowed: $($entry.FullName)" }
    }
    foreach ($name in @('game-probe.log', 'probe-started.json')) {
        if (Test-Path -LiteralPath (Join-Path $run $name)) { throw 'Probe was already used. Prepare a new run; previous evidence must not mix with this test.' }
    }
    $save = [IO.Path]::GetFullPath((Join-Path $run 'Save'))
    foreach ($name in @('Save', 'Save\Players', 'Save\Worlds')) {
        $directory = Join-Path $run $name
        Assert-NoReparse $directory
        if (-not (Test-Path -LiteralPath $directory -PathType Container)) { throw "Missing isolated directory: $name" }
    }
    if (@(Get-ChildItem -LiteralPath $save -File -Recurse -Force).Count -ne 0) { throw 'The isolated Save directory must be empty before first launch.' }
    $audioPath = Join-Path $run 'Chaite\Audio'
    if ((Test-Path -LiteralPath $audioPath) -and @(Get-ChildItem -LiteralPath $audioPath -File -Recurse -Force).Count -ne 0) {
        throw 'User audio is not allowed in unattended probes.'
    }

    $required = @('Terraria.exe', 'Chaite.Plugin.dll', 'Chaite.Core.dll', 'Chaite.GameProbe.dll',
        'Chaite.DesktopHost.exe', 'GameProbePatcher.exe', 'Chaite.Patcher.exe', 'Mono.Cecil.dll',
        'ReLogic.Native.dll', 'nfd.dll', 'Microsoft.Xna.Framework.Video.dll',
        'Microsoft.Xna.Framework.Content.Pipeline.dll', 'Chaite/config.json')
    $files = Require-ProbeJsonArray $manifest.Files 'probe manifest Files'
    $pinned = @{}
    $pinnedEntries = [Collections.Generic.List[object]]::new()
    foreach ($file in $files) {
        $null = Require-ProbeExactFields $file @('Path', 'Sha256') 'probe manifest Files entry'
        $relative = Require-ProbeJsonString $file.Path 'probe manifest Files.Path'
        if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('\') -or $relative.Contains(':') -or
            $relative.StartsWith('/') -or @($relative.Split('/') | Where-Object { $_ -eq '..' -or $_ -eq '.' -or $_ -eq '' }).Count -ne 0) {
            throw 'Unsafe path in probe manifest.'
        }
        if ($pinned.ContainsKey($relative)) { throw "Duplicate manifest file: $relative" }
        if ($relative -cnotin $required -and -not ($mode -ceq 'client' -and $relative.StartsWith('Content/', [StringComparison]::Ordinal))) {
            throw "Unreviewed runtime file: $relative"
        }
        $declaredSha = Require-ProbeSha256 $file.Sha256 "probe manifest file hash: $relative"
        $path = [IO.Path]::GetFullPath((Join-Path $run $relative))
        if (-not $path.StartsWith($run + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Manifest path escaped run directory.'
        }
        $actualSha = Get-RequiredFileSha256 $path "prepared runtime $relative"
        if (-not [string]::Equals($actualSha, $declaredSha, [StringComparison]::Ordinal)) { throw "Prepared runtime was modified: $relative" }
        $pinned[$relative] = $true
        $pinnedEntries.Add([pscustomobject]@{ RelativePath = $relative; FullPath = $path; Sha256 = $actualSha })
    }
    foreach ($relative in $required) { if (-not $pinned.ContainsKey($relative)) { throw "Missing required manifest file: $relative" } }
    foreach ($file in Get-ChildItem -LiteralPath $run -File -Recurse -Force) {
        $relative = $file.FullName.Substring($run.Length + 1).Replace('\', '/')
        if ($relative -cnotin @('probe-manifest.json', 'probe-static-evidence.json') -and -not $pinned.ContainsKey($relative)) {
            throw "Unmanifested file in prepared run: $relative"
        }
    }
    return [pscustomobject]@{
        Run = $run; RunName = $runName; RunId = $runIdText; Manifest = $manifest; ManifestPath = $manifestPath
        ManifestSha256 = $manifestSha; StaticEvidence = $staticEvidence; StaticEvidencePath = $staticEvidencePath
        StaticEvidenceSha256 = (Get-RequiredFileSha256 $staticEvidencePath 'probe static evidence')
        PreparedTerrariaSha256 = $preparedTerrariaSha; PreparedGameProbeSha256 = $preparedProbeSha
        PreparedDesktopHostSha256 = $preparedDesktopHostSha; DesktopHostPath = $desktopHostPath; Save = $save
        PinnedEntries = @($pinnedEntries.ToArray())
    }
}
function Write-NewProbeUtf8JsonFile([string]$Path, $Value, [int]$Depth, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    $json = $Value | ConvertTo-Json -Depth $Depth
    $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($json + [Environment]::NewLine)
    try { $stream = [IO.File]::Open($fullPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None) }
    catch { throw "Refusing to overwrite or race an existing $Name file." }
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}
function Write-NewProbeUtf8TextFile([string]$Path, [string]$Text, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($Text)
    try { $stream = [IO.File]::Open($fullPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None) }
    catch { throw "Refusing to overwrite or race an existing $Name file." }
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}
function Assert-ProbeBindingsEqual($Before, $After) {
    foreach ($field in @('Run', 'RunName', 'RunId', 'ManifestSha256', 'StaticEvidenceSha256',
        'PreparedTerrariaSha256', 'PreparedGameProbeSha256', 'PreparedDesktopHostSha256', 'DesktopHostPath', 'Save')) {
        if ($Before.$field -isnot [string] -or $After.$field -isnot [string] -or
            -not [string]::Equals($Before.$field, $After.$field, [StringComparison]::Ordinal)) {
            throw "Prepared launch binding changed during final validation: $field"
        }
    }
    $beforeEntries = @($Before.PinnedEntries)
    $afterEntries = @($After.PinnedEntries)
    if ($beforeEntries.Count -ne $afterEntries.Count) { throw 'Prepared runtime file set changed during final validation.' }
    for ($index = 0; $index -lt $beforeEntries.Count; $index++) {
        foreach ($field in @('RelativePath', 'FullPath', 'Sha256')) {
            if ($beforeEntries[$index].$field -isnot [string] -or $afterEntries[$index].$field -isnot [string] -or
                -not [string]::Equals($beforeEntries[$index].$field, $afterEntries[$index].$field, [StringComparison]::Ordinal)) {
                throw "Prepared runtime file binding changed during final validation: $field"
            }
        }
    }
}
function Open-ProbeReadLocks($Binding) {
    $paths = [Collections.Generic.List[string]]::new()
    $paths.Add([string]$Binding.ManifestPath)
    $paths.Add([string]$Binding.StaticEvidencePath)
    foreach ($entry in @($Binding.PinnedEntries)) { $paths.Add([string]$entry.FullPath) }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $streams = [Collections.Generic.List[IO.FileStream]]::new()
    try {
        foreach ($path in $paths) {
            if (-not $seen.Add($path)) { continue }
            Assert-NoReparse $path
            $streams.Add([IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read))
        }
        return $streams
    } catch {
        foreach ($stream in $streams) { $stream.Dispose() }
        throw
    }
}
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactPrefix ('desktop-test-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Outputs must be in a named project artifacts subdirectory.' }
Assert-NoReparse $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new output directory; existing test evidence is never overwritten.' }

$binding = $null
$run = $null
$manifest = $null
$save = $null
$hostExe = $null
if ($Probe) {
    if (-not [string]::IsNullOrWhiteSpace($TargetExe) -or $TargetArguments.Count -ne 0) { throw '-Probe does not accept a target executable or custom arguments.' }
} else {
    if ([string]::IsNullOrWhiteSpace($TargetExe)) { throw 'Specify a prepared Terraria.exe, or -Probe.' }
    $binding = Read-AndValidatePreparedProbe $TargetExe $projectRoot $PSScriptRoot
    $TargetExe = [IO.Path]::GetFullPath($TargetExe)
    $run = $binding.Run
    $manifest = $binding.Manifest
    $save = $binding.Save
    $hostExe = $binding.DesktopHostPath
    if ($OutputDirectory.StartsWith($run + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Host output must be separate from the prepared run directory.' }

    # No logfile/minidump/cloud/world/config switches. Future fixture switches
    # require an explicit review here AND in ChaiteGameProbe.ValidateLaunch.
    $savedirSeen = $false
    $skipBeam = $false
    $fixtureArguments = [ordered]@{}
    $priorityPhases = @{
        'deerclops' = @('summon','spawn-settle','opening','forward-spikes','rubble','slow-roar','double-spikes','shadow-hands','return-home','teleport-home')
        'skeletron' = @('hover','pre-spin','spin-imminent','spin','spin-pursuit','spin-exit','hand-vertical-imminent','hand-vertical-locking','hand-vertical','hand-horizontal-imminent','hand-horizontal-locking','hand-horizontal')
        'queen-bee' = @('summon','choose','charge-align','charge','charge-brake','bee-wave','move-above','stinger','reacquire')
        'wall-of-flesh' = @('runway','accelerating','low-health','critical','eye-laser')
        'duke-fishron' = @('summon','spawn-fade','spawn-emerge','p1-hover','p1-dash','p1-bubbles','p1-sharknado','p2-transition-fade','p2-transition-emerge','p2-hover','p2-dash','p2-bubbles','p2-sharknado','p3-transition-fade','p3-transition-hidden','p3-reposition','p3-dash','p3-teleport')
        'empress-night' = @('summon','p1-reposition','p1-bolts','p1-rainbow','p1-sun-dance','p1-dash','transition','p2-reposition','p2-lance-wall','p2-predictive-lances','p2-spiral')
        'empress-day' = @('summon','p1-reposition','p1-bolts','p1-rainbow','p1-sun-dance','p1-dash','transition','p2-reposition','p2-lance-wall','p2-predictive-lances','p2-spiral')
        'moon-lord' = @('intro','synchronize-eyes','head-bolts','head-tongue','head-deathray-telegraph','left-sphere-release','right-sphere-release')
    }
    for ($index = 0; $index -lt $TargetArguments.Count; $index++) {
        switch -CaseSensitive ($TargetArguments[$index]) {
            '-savedirectory' {
                if ($savedirSeen -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -savedirectory.' }
                $suppliedSave = [IO.Path]::GetFullPath($TargetArguments[$index])
                if (-not $suppliedSave.Equals($save, [StringComparison]::OrdinalIgnoreCase)) { throw '-savedirectory must exactly match the prepared run Save directory.' }
                $savedirSeen = $true
            }
            '-skipbeam' {
                if ($skipBeam) { throw 'Duplicate -skipbeam.' }
                $skipBeam = $true
            }
            '-scenario' {
                if ($fixtureArguments.Contains('-scenario') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -scenario.' }
                $value = $TargetArguments[$index]
                if ($value -cnotin @('eye-baseline', 'eye', 'king-slime', 'queen-slime', 'destroyer', 'twins', 'prime',
                    'deerclops', 'skeletron', 'queen-bee', 'wall-of-flesh', 'duke-fishron', 'empress-night', 'empress-day', 'moon-lord',
                    'scope-negative-unsupported-summon', 'scope-negative-existing-unsupported',
                    'scope-negative-fishron-mixed', 'scope-negative-empress-mixed',
                    'scope-negative-fishron-duplicate', 'scope-negative-empress-duplicate',
                    'motion-jump', 'motion-flight')) { throw 'Unreviewed fixture scenario.' }
                $fixtureArguments['-scenario'] = $value
            }
            '-phase' {
                if ($fixtureArguments.Contains('-phase') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -phase.' }
                $value = $TargetArguments[$index]
                if ($value -cnotmatch '^[a-z0-9][a-z0-9-]{0,39}$') { throw 'Invalid bounded phase identifier.' }
                $fixtureArguments['-phase'] = $value
            }
            '-takeovertick' {
                if ($fixtureArguments.Contains('-takeovertick') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -takeovertick.' }
                $value = $TargetArguments[$index]
                $takeover = 0
                if ($value -cnotmatch '^[1-9][0-9]{2,4}$' -or -not [int]::TryParse($value, [ref]$takeover) -or $takeover -lt 120 -or $takeover -gt 23880) {
                    throw '-takeovertick must be an integer in 120..23880.'
                }
                $fixtureArguments['-takeovertick'] = $takeover.ToString([Globalization.CultureInfo]::InvariantCulture)
            }
            '-motioncase' {
                if ($fixtureArguments.Contains('-motioncase') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -motioncase.' }
                $value = $TargetArguments[$index]
                if ($value -cnotin @('no-cloud-hold', 'no-cloud-tap', 'no-cloud-release-press', 'cloud-hold', 'cloud-tap', 'cloud-release-press')) { throw 'Unreviewed motion case.' }
                $fixtureArguments['-motioncase'] = $value
            }
            '-flightcase' {
                if ($fixtureArguments.Contains('-flightcase') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -flightcase.' }
                $value = $TargetArguments[$index]
                if ($value -cnotin @('demon-exhaust-release', 'demon-lightning-exhaust-release', 'demon-early-repress', 'demon-lightning-early-repress', 'demon-held-landing', 'demon-lightning-held-landing', 'demon-cloud-repress', 'demon-lightning-cloud-repress')) { throw 'Unreviewed flight case.' }
                $fixtureArguments['-flightcase'] = $value
            }
            '-seed' {
                if ($fixtureArguments.Contains('-seed') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -seed.' }
                $value = $TargetArguments[$index]
                $seed = 0
                if ($value -cnotmatch '^(0|[1-9][0-9]{0,9})$' -or -not [int]::TryParse($value, [ref]$seed) -or $seed -lt 0) { throw '-seed must be an integer in 0..2147483647.' }
                $fixtureArguments['-seed'] = $seed.ToString([Globalization.CultureInfo]::InvariantCulture)
            }
            '-difficulty' {
                if ($fixtureArguments.Contains('-difficulty') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -difficulty.' }
                $value = $TargetArguments[$index]
                if ($value -cnotin @('classic', 'expert', 'master')) { throw 'Unreviewed difficulty.' }
                $fixtureArguments['-difficulty'] = $value
            }
            '-maxticks' {
                if ($fixtureArguments.Contains('-maxticks') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -maxticks.' }
                $value = $TargetArguments[$index]
                $limit = 0
                if ($value -cnotmatch '^[1-9][0-9]{2,4}$' -or -not [int]::TryParse($value, [ref]$limit) -or $limit -lt 600 -or $limit -gt 24000) { throw '-maxticks must be an integer in 600..24000.' }
                $fixtureArguments['-maxticks'] = $limit.ToString([Globalization.CultureInfo]::InvariantCulture)
            }
            '-wallseconds' {
                if ($fixtureArguments.Contains('-wallseconds') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -wallseconds.' }
                $value = $TargetArguments[$index]
                $limit = 0
                if ($value -cnotmatch '^[1-9][0-9]{1,2}$' -or -not [int]::TryParse($value, [ref]$limit) -or $limit -lt 15 -or $limit -gt 900) { throw '-wallseconds must be an integer in 15..900.' }
                $fixtureArguments['-wallseconds'] = $limit.ToString([Globalization.CultureInfo]::InvariantCulture)
            }
            default { throw "Unreviewed target argument: $($TargetArguments[$index])" }
        }
    }
    $motion = $fixtureArguments.Contains('-scenario') -and $fixtureArguments['-scenario'] -ceq 'motion-jump'
    $flight = $fixtureArguments.Contains('-scenario') -and $fixtureArguments['-scenario'] -ceq 'motion-flight'
    $scenarioId = if ($fixtureArguments.Contains('-scenario')) { $fixtureArguments['-scenario'] } else { 'eye-baseline' }
    $scopeNegative = $scenarioId -clike 'scope-negative-*'
    $phaseSpecified = $fixtureArguments.Contains('-phase')
    $takeoverSpecified = $fixtureArguments.Contains('-takeovertick')
    if ($motion) {
        if ($manifest.Mode -cne 'headless' -or -not $fixtureArguments.Contains('-motioncase')) { throw 'Motion microtests require a headless manifest and explicit reviewed -motioncase.' }
        if ($fixtureArguments.Contains('-difficulty') -and $fixtureArguments['-difficulty'] -cne 'classic') { throw 'Motion microtests require classic difficulty.' }
    } elseif ($fixtureArguments.Contains('-motioncase')) { throw '-motioncase requires -scenario motion-jump.' }
    if ($flight) {
        if ($manifest.Mode -cne 'headless' -or -not $fixtureArguments.Contains('-flightcase')) { throw 'Flight microtests require a headless manifest and explicit reviewed -flightcase.' }
        if ($fixtureArguments.Contains('-difficulty') -and $fixtureArguments['-difficulty'] -cne 'classic') { throw 'Flight microtests require classic difficulty.' }
    } elseif ($fixtureArguments.Contains('-flightcase')) { throw '-flightcase requires -scenario motion-flight.' }
    if ($motion -or $flight) {
        if ($phaseSpecified -or $takeoverSpecified) { throw 'Motion/flight microtests do not accept Boss phase or takeover arguments.' }
    } elseif ($scopeNegative) {
        if (-not $phaseSpecified -or $fixtureArguments['-phase'] -cne 'reject' -or -not $takeoverSpecified) {
            throw 'Scope-negative Boss fixtures require explicit -phase reject and -takeovertick.'
        }
        $maxTicks = if ($fixtureArguments.Contains('-maxticks')) { [int]$fixtureArguments['-maxticks'] } else { 600 }
        $takeover = [int]$fixtureArguments['-takeovertick']
        if ($takeover -ge $maxTicks - 120) { throw '-takeovertick must leave at least 120 native frames before -maxticks.' }
    } else {
        if ($priorityPhases.ContainsKey($scenarioId)) {
            if (-not $phaseSpecified -or -not $takeoverSpecified) { throw 'Priority Boss fixtures require explicit -phase and -takeovertick.' }
            if ($fixtureArguments['-phase'] -cnotin $priorityPhases[$scenarioId]) { throw 'Unreviewed phase for the selected priority Boss fixture.' }
        } else {
            if ($phaseSpecified -and $fixtureArguments['-phase'] -cne 'summon') { throw 'Legacy summon fixtures accept only -phase summon.' }
        }
        $maxTicks = if ($fixtureArguments.Contains('-maxticks')) { [int]$fixtureArguments['-maxticks'] } else { 24000 }
        $takeover = if ($takeoverSpecified) { [int]$fixtureArguments['-takeovertick'] } else { 120 }
        if ($takeover -ge $maxTicks - 120) { throw '-takeovertick must leave at least 120 native frames before -maxticks.' }
        $caseDifficulty = if ($fixtureArguments.Contains('-difficulty')) { $fixtureArguments['-difficulty'] } else { 'classic' }
        if ($scenarioId -ceq 'duke-fishron' -and $fixtureArguments['-phase'] -clike 'p3-*' -and $caseDifficulty -ceq 'classic') {
            throw 'Duke Fishron phase 3 is not a Classic native phase.'
        }
    }
    $TargetArguments = @('-savedirectory', $save)
    if ($skipBeam) { $TargetArguments += '-skipbeam' }
    foreach ($key in $fixtureArguments.Keys) { $TargetArguments += @($key, $fixtureArguments[$key]) }
}

# All path, manifest and argument checks happen before creating output. Normal
# native runs use the reviewed DesktopHost binary compiled and pinned by
# prepare-game-probe; only the harmless -Probe path compiles a temporary host.
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
if ($Probe) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $compiler)) { throw 'Framework 4.x x64 C# compiler not found.' }
    $hostExe = Join-Path $OutputDirectory 'Chaite.DesktopHost.exe'
    & $compiler /nologo /target:exe /platform:anycpu /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$hostExe" (Join-Path $PSScriptRoot 'Chaite.DesktopHost.cs')
    if ($LASTEXITCODE -ne 0) { throw "Desktop host compile failed: $LASTEXITCODE" }
    $TargetExe = $hostExe
    $TargetArguments = @('--probe-child')
}

function Quote-NativeArgument([string]$Value) {
    # CommandLineToArgvW-compatible quoting, including trailing backslashes.
    if ($null -eq $Value) { return '""' }
    return '"' + ([regex]::Replace(([regex]::Replace($Value, '(\\*)"', '$1$1\"')), '(\\+)$', '$1$1')) + '"'
}
$childCommand = ($TargetArguments | ForEach-Object { Quote-NativeArgument $_ }) -join ' '
# Probe recognition needs the canonical spelling for the host's window check.
if ($Probe) { $childCommand = '--probe-child' }
$encodedCommand = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($childCommand))
$hostArguments = @('--root', $projectRoot, '--exe', $TargetExe, '--output', $OutputDirectory, '--working', (Split-Path -Parent $TargetExe), '--timeout', [string]$TimeoutSeconds, '--args64', $encodedCommand)
$hostCommand = ($hostArguments | ForEach-Object { Quote-NativeArgument $_ }) -join ' '
$readLocks = @()
try {
    if (-not $Probe) {
        # Revalidate immediately before locking, then once more while every
        # manifest file is held read-only/no-delete through host completion.
        # The encompassing finally starts before the first lock acquisition.
        $preLockBinding = Read-AndValidatePreparedProbe $TargetExe $projectRoot $PSScriptRoot
        Assert-ProbeBindingsEqual $binding $preLockBinding
        $readLocks = @(Open-ProbeReadLocks $preLockBinding)
        $lockedBinding = Read-AndValidatePreparedProbe $TargetExe $projectRoot $PSScriptRoot
        Assert-ProbeBindingsEqual $preLockBinding $lockedBinding
        $binding = $lockedBinding
        $pinEntries = [Collections.Generic.List[object]]::new()
        $pinEntries.Add([pscustomobject]@{ RelativePath = 'probe-manifest.json'; Sha256 = $binding.ManifestSha256 })
        $pinEntries.Add([pscustomobject]@{ RelativePath = 'probe-static-evidence.json'; Sha256 = $binding.StaticEvidenceSha256 })
        foreach ($entry in @($binding.PinnedEntries)) {
            $pinEntries.Add([pscustomobject]@{ RelativePath = [string]$entry.RelativePath; Sha256 = [string]$entry.Sha256 })
        }
        $pinLines = [Collections.Generic.List[string]]::new()
        $pinLines.Add('CHAITE-LAUNCH-PINS/V1')
        $pinLines.Add('RUNID' + "`t" + $binding.RunId)
        $pinLines.Add('COUNT' + "`t" + $pinEntries.Count.ToString([Globalization.CultureInfo]::InvariantCulture))
        foreach ($entry in $pinEntries) {
            $encodedRelative = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($entry.RelativePath))
            $pinLines.Add($entry.Sha256 + "`t" + $encodedRelative)
        }
        $pinPlanPath = Join-Path $OutputDirectory 'launch-pin-plan.txt'
        Write-NewProbeUtf8TextFile $pinPlanPath (($pinLines -join "`n") + "`n") 'launch pin plan'
        $pinPlanSha256 = Get-RequiredFileSha256 $pinPlanPath 'launch pin plan'
        $readLocks += [IO.File]::Open($pinPlanPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $launchBinding = [ordered]@{
            Schema = 'chaite-launch-binding/v1'
            RunId = $binding.RunId
            RunName = $binding.RunName
            CreatedUtc = [DateTime]::UtcNow.ToString('o')
            PrelaunchValidationPassed = $true
            LockIntent = 'launcher-and-desktop-host-fileshare-read-until-job-empty'
            PinnedPreparedFileCount = $pinEntries.Count
            PinPlanSha256 = $pinPlanSha256
            ManifestSha256 = $binding.ManifestSha256
            StaticEvidenceSha256 = $binding.StaticEvidenceSha256
            PreparedTerrariaSha256 = $binding.PreparedTerrariaSha256
            PreparedGameProbeSha256 = $binding.PreparedGameProbeSha256
            PreparedDesktopHostSha256 = $binding.PreparedDesktopHostSha256
            StartIsolatedTestScriptSha256 = (Get-RequiredFileSha256 $PSCommandPath 'start-isolated-test script')
            DesktopHostSourceSha256 = (Get-RequiredFileSha256 (Join-Path $PSScriptRoot 'Chaite.DesktopHost.cs') 'DesktopHost source')
        }
        $launchBindingPath = Join-Path $OutputDirectory 'launch-binding.json'
        Write-NewProbeUtf8JsonFile $launchBindingPath $launchBinding 5 'launch binding'
        $launchBindingSha256 = Get-RequiredFileSha256 $launchBindingPath 'launch binding'
        $readLocks += [IO.File]::Open($launchBindingPath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        $hostArguments += @('--pinplan', $pinPlanPath, '--pinsha256', $pinPlanSha256,
            '--binding', $launchBindingPath, '--bindingsha256', $launchBindingSha256)
        $hostCommand = ($hostArguments | ForEach-Object { Quote-NativeArgument $_ }) -join ' '
        # CreateNew atomically reserves a single-use run, even with two launchers.
        Write-NewProbeUtf8JsonFile (Join-Path $run 'probe-started.json') ([ordered]@{
            Schema = 'chaite-probe-start/v1'; RunId = $binding.RunId; StartedUtc = [DateTime]::UtcNow.ToString('o')
            OutputDirectory = $OutputDirectory; LaunchBindingSha256 = $launchBindingSha256
        }) 4 'probe start marker'
    }
    $hostProcess = Start-Process -FilePath $hostExe -ArgumentList $hostCommand -WindowStyle Hidden -PassThru -Wait
    $exitCode = $hostProcess.ExitCode
} finally {
    foreach ($stream in @($readLocks)) { if ($null -ne $stream) { $stream.Dispose() } }
}
Write-NewProbeUtf8JsonFile (Join-Path $OutputDirectory 'desktop-exit.json') ([ordered]@{
    Schema = 'chaite-desktop-exit/v1'; CompletedUtc = [DateTime]::UtcNow.ToString('o')
    HostExitCode = $exitCode; ProbeOnly = [bool]$Probe; TargetExe = $TargetExe
}) 4 'desktop exit evidence'
Write-Output "Isolated test output: $OutputDirectory"
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'desktop-host.log')) { Get-Content -LiteralPath (Join-Path $OutputDirectory 'desktop-host.log') -Encoding UTF8 }
if ($exitCode -ne 0) { throw "Isolated test returned $exitCode. Only its own job was terminated; see logs." }
Write-Output 'PASS verified probe process/desktop. Battle and beam coverage is limited to the exact assertions recorded in the probe log.'

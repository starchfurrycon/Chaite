param(
    [string]$TargetExe,
    [string[]]$TargetArguments = @(),
    [ValidateRange(120, 240)][int]$TimeoutSeconds = 120,
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
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $artifactPrefix ('desktop-test-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Outputs must be in a named project artifacts subdirectory.' }
Assert-NoReparse $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Choose a new output directory; existing test evidence is never overwritten.' }

$run = $null
$manifest = $null
if ($Probe) {
    if (-not [string]::IsNullOrWhiteSpace($TargetExe) -or $TargetArguments.Count -ne 0) { throw '-Probe does not accept a target executable or custom arguments.' }
} else {
    if ([string]::IsNullOrWhiteSpace($TargetExe)) { throw 'Specify a prepared Terraria.exe, or -Probe.' }
    $TargetExe = [IO.Path]::GetFullPath($TargetExe)
    Assert-NoReparse $TargetExe
    if (-not $TargetExe.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($TargetExe) -cne 'Terraria.exe' -or -not (Test-Path -LiteralPath $TargetExe -PathType Leaf)) { throw 'Only a prepared Terraria.exe inside project artifacts may run.' }
    $run = Split-Path -Parent $TargetExe
    if ($OutputDirectory.StartsWith($run + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Host output must be separate from the prepared run directory.' }
    $manifestPath = Join-Path $run 'probe-manifest.json'
    Assert-NoReparse $manifestPath
    if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw 'Missing probe manifest. Re-run prepare-game-probe.ps1 using a fresh directory.' }
    $manifest = Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($manifest.Schema -cne 'chaite-isolated-probe/v1' -or $manifest.GameVersion -cne '1.4.5.8' -or $manifest.SourceGameSha256 -cne '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3') { throw 'Unrecognized probe manifest/version.' }
    if ($manifest.Mode -cnotin @('headless', 'client') -or $manifest.SaveDirectory -cne 'Save' -or $manifest.SocialDisabled -ne $true -or $manifest.LaunchGuardBeforeLogging -ne $true -or $manifest.SyntheticHotkeysOnly -ne $true) { throw 'Manifest does not establish required isolation invariants.' }
    $runId = [guid]::Empty
    if (-not [guid]::TryParse([string]$manifest.RunId, [ref]$runId)) { throw 'Invalid probe run identity.' }
    foreach ($name in @('InputPluginSha256', 'InputCoreSha256', 'ProbeSourceSha256', 'ProbePatcherSourceSha256')) {
        if ($manifest.$name -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid provenance hash: $name" }
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
    if ((Test-Path -LiteralPath $audioPath) -and @(Get-ChildItem -LiteralPath $audioPath -File -Recurse -Force).Count -ne 0) { throw 'User audio is not allowed in unattended probes.' }

    $required = @('Terraria.exe', 'Chaite.Plugin.dll', 'Chaite.Core.dll', 'Chaite.GameProbe.dll', 'GameProbePatcher.exe', 'Chaite.Patcher.exe', 'Mono.Cecil.dll', 'ReLogic.Native.dll', 'nfd.dll', 'Microsoft.Xna.Framework.Video.dll', 'Microsoft.Xna.Framework.Content.Pipeline.dll', 'Chaite/config.json')
    $pinned = @{}
    foreach ($file in @($manifest.Files)) {
        $relative = [string]$file.Path
        if ([string]::IsNullOrWhiteSpace($relative) -or $relative.Contains('\') -or $relative.Contains(':') -or $relative.StartsWith('/') -or @($relative.Split('/') | Where-Object { $_ -eq '..' -or $_ -eq '.' -or $_ -eq '' }).Count -ne 0) { throw 'Unsafe path in probe manifest.' }
        if ($pinned.ContainsKey($relative)) { throw "Duplicate manifest file: $relative" }
        if ($relative -cnotin $required -and -not ($manifest.Mode -ceq 'client' -and $relative.StartsWith('Content/', [StringComparison]::Ordinal))) { throw "Unreviewed runtime file: $relative" }
        if ($file.Sha256 -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid file hash: $relative" }
        $path = [IO.Path]::GetFullPath((Join-Path $run $relative))
        if (-not $path.StartsWith($run + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Manifest path escaped run directory.' }
        Assert-NoReparse $path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $file.Sha256) { throw "Prepared runtime was modified: $relative" }
        $pinned[$relative] = $true
    }
    foreach ($relative in $required) { if (-not $pinned.ContainsKey($relative)) { throw "Missing required manifest file: $relative" } }
    foreach ($file in Get-ChildItem -LiteralPath $run -File -Recurse -Force) {
        $relative = $file.FullName.Substring($run.Length + 1).Replace('\', '/')
        if ($relative -cne 'probe-manifest.json' -and -not $pinned.ContainsKey($relative)) { throw "Unmanifested file in prepared run: $relative" }
    }

    # No logfile/minidump/cloud/world/config switches. Future fixture switches
    # require an explicit review here AND in ChaiteGameProbe.ValidateLaunch.
    $savedirSeen = $false
    $skipBeam = $false
    $fixtureArguments = [ordered]@{}
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
                if ($value -cnotin @('eye-baseline', 'eye', 'king-slime', 'queen-slime', 'destroyer', 'twins', 'prime', 'motion-jump')) { throw 'Unreviewed fixture scenario.' }
                $fixtureArguments['-scenario'] = $value
            }
            '-motioncase' {
                if ($fixtureArguments.Contains('-motioncase') -or ++$index -ge $TargetArguments.Count) { throw 'Duplicate or incomplete -motioncase.' }
                $value = $TargetArguments[$index]
                if ($value -cnotin @('no-cloud-hold', 'no-cloud-tap', 'no-cloud-release-press', 'cloud-hold', 'cloud-tap', 'cloud-release-press')) { throw 'Unreviewed motion case.' }
                $fixtureArguments['-motioncase'] = $value
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
                if ($value -cnotmatch '^[1-9][0-9]$' -or -not [int]::TryParse($value, [ref]$limit) -or $limit -lt 15 -or $limit -gt 90) { throw '-wallseconds must be an integer in 15..90.' }
                $fixtureArguments['-wallseconds'] = $limit.ToString([Globalization.CultureInfo]::InvariantCulture)
            }
            default { throw "Unreviewed target argument: $($TargetArguments[$index])" }
        }
    }
    $motion = $fixtureArguments.Contains('-scenario') -and $fixtureArguments['-scenario'] -ceq 'motion-jump'
    if ($motion) {
        if ($manifest.Mode -cne 'headless' -or -not $fixtureArguments.Contains('-motioncase')) { throw 'Motion microtests require a headless manifest and explicit reviewed -motioncase.' }
        if ($fixtureArguments.Contains('-difficulty') -and $fixtureArguments['-difficulty'] -cne 'classic') { throw 'Motion microtests require classic difficulty.' }
    } elseif ($fixtureArguments.Contains('-motioncase')) { throw '-motioncase requires -scenario motion-jump.' }
    $TargetArguments = @('-savedirectory', $save)
    if ($skipBeam) { $TargetArguments += '-skipbeam' }
    foreach ($key in $fixtureArguments.Keys) { $TargetArguments += @($key, $fixtureArguments[$key]) }
}

# All path, manifest and argument checks happen before creating output or
# compiling a process. Preparation and a harmless -Probe remain separate.
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Framework 4.x x64 C# compiler not found.' }
$hostExe = Join-Path $OutputDirectory 'Chaite.DesktopHost.exe'
& $compiler /nologo /target:exe /platform:anycpu /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$hostExe" (Join-Path $PSScriptRoot 'Chaite.DesktopHost.cs')
if ($LASTEXITCODE -ne 0) { throw "Desktop host compile failed: $LASTEXITCODE" }
if ($Probe) {
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
if (-not $Probe) {
    Assert-NoReparse $run
    # CreateNew atomically reserves a single-use run, even with two launchers.
    $marker = [IO.File]::Open((Join-Path $run 'probe-started.json'), [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $record = [Text.Encoding]::UTF8.GetBytes((@{ RunId = $manifest.RunId; StartedUtc = [DateTime]::UtcNow.ToString('o'); OutputDirectory = $OutputDirectory } | ConvertTo-Json))
        $marker.Write($record, 0, $record.Length)
    } finally { $marker.Dispose() }
}
$hostProcess = Start-Process -FilePath $hostExe -ArgumentList $hostCommand -WindowStyle Hidden -PassThru -Wait
$exitCode = $hostProcess.ExitCode
@{
    Schema = 'chaite-desktop-exit/v1'; CompletedUtc = [DateTime]::UtcNow.ToString('o')
    HostExitCode = $exitCode; ProbeOnly = [bool]$Probe; TargetExe = $TargetExe
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $OutputDirectory 'desktop-exit.json') -Encoding UTF8
Write-Output "Isolated test output: $OutputDirectory"
if (Test-Path -LiteralPath (Join-Path $OutputDirectory 'desktop-host.log')) { Get-Content -LiteralPath (Join-Path $OutputDirectory 'desktop-host.log') -Encoding UTF8 }
if ($exitCode -ne 0) { throw "Isolated test returned $exitCode. Only its own job was terminated; see logs." }
Write-Output 'PASS verified probe process/desktop. Battle and beam coverage is limited to the exact assertions recorded in the probe log.'

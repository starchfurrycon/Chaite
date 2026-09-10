param(
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria',
    [string]$RunName = ('game-probe-' + (Get-Date -Format 'yyyyMMdd-HHmmss')),
    [switch]$Headless
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# A private desktop is not a filesystem or network sandbox. Preparation must
# establish these invariants before a process is allowed to start.
function Assert-NoReparse([string]$Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($candidate)) {
        $entry = Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
            throw "Reparse points are not allowed in probe paths: $candidate"
        }
        $parent = [IO.Directory]::GetParent($candidate)
        if ($null -eq $parent) { break }
        $candidate = $parent.FullName
    }
}
function Copy-FreshFile([string]$Source, [string]$Destination) {
    Assert-NoReparse $Source
    Assert-NoReparse $Destination
    if (Test-Path -LiteralPath $Destination) { throw "Refusing to overwrite prepared file: $Destination" }
    [IO.File]::Copy($Source, $Destination, $false)
}
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($RunName -notmatch '^game-probe-[a-zA-Z0-9-]+$') { throw 'Invalid run directory name' }
$run = Join-Path $root ('artifacts\' + $RunName)
Assert-NoReparse $run
if (Test-Path -LiteralPath $run) { throw 'Run already exists; use a fresh RunName' }
$GameDirectory = [IO.Path]::GetFullPath($GameDirectory)
$exe = Join-Path $GameDirectory 'Terraria.exe'
Assert-NoReparse $exe
$expected = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
if ((Get-FileHash -LiteralPath $exe).Hash -ne $expected) { throw 'Only verified vanilla 1.4.5.8 supported' }
# Never copy *.dll: a prior installation can contain stale Chaite assemblies,
# Steam integration and unrelated SDKs. These are the reviewed dependencies.
$dependencies = [ordered]@{
    'ReLogic.Native.dll' = '0C132A8967C4CF437B3EDBDDB00F8E224B989CA8B90252FD965F73DCA7F531A0'
    'nfd.dll' = '1610AAFA510FA776DB62686F3BA7AD55ABB535EC97F9D18A40FA13B548E1B22C'
    'Microsoft.Xna.Framework.Video.dll' = '17538B1CA9D48A993E2CD88C96B436DF08E7ABB4AEC5D4758EB21FEB580D6E06'
    'Microsoft.Xna.Framework.Content.Pipeline.dll' = '1EA481DA62EB9E66BAD8589DF83F173725E62D22362801E1264CD02347721C69'
}
foreach ($name in $dependencies.Keys) {
    $source = Join-Path $GameDirectory $name
    Assert-NoReparse $source
    if ((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $dependencies[$name]) { throw "Unreviewed dependency: $name" }
}
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$vs = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
$compiler = Join-Path $vs 'MSBuild\Current\Bin\Roslyn\csc.exe'
$xnaRoot = 'C:\Windows\Microsoft.NET\assembly\GAC_32'
$xna = (Get-ChildItem -LiteralPath (Join-Path $xnaRoot 'Microsoft.Xna.Framework') -Recurse -Filter Microsoft.Xna.Framework.dll | Select-Object -First 1).FullName
$xnaGame = (Get-ChildItem -LiteralPath (Join-Path $xnaRoot 'Microsoft.Xna.Framework.Game') -Recurse -Filter Microsoft.Xna.Framework.Game.dll | Select-Object -First 1).FullName
$xnaGraphics = (Get-ChildItem -LiteralPath (Join-Path $xnaRoot 'Microsoft.Xna.Framework.Graphics') -Recurse -Filter Microsoft.Xna.Framework.Graphics.dll | Select-Object -First 1).FullName
$plugin = Join-Path $root 'src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll'
$core = Join-Path $root 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'
$patcher = Join-Path $root 'src\Chaite.Patcher\bin\Release\net48\Chaite.Patcher.exe'
$cecil = Join-Path $root 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'
$probeSource = Join-Path $PSScriptRoot 'GameProbe.cs'
$patcherSource = Join-Path $PSScriptRoot 'GameProbePatcher.cs'
$config = Join-Path $root 'src\Chaite.Plugin\config.json'
$inputHashes = @{}
foreach ($inputPath in @($exe, $plugin, $core, $patcher, $cecil, $probeSource, $patcherSource, $config)) {
    Assert-NoReparse $inputPath
    if (-not (Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw "Missing build input: $inputPath" }
    $inputHashes[$inputPath] = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
}
$content = Join-Path $GameDirectory 'Content'
if (-not $Headless) {
    Assert-NoReparse $content
    if (-not (Test-Path -LiteralPath $content -PathType Container)) { throw 'Client probe requires original Content directory.' }
    # Inspect links before recursive copying can follow an unexpected target.
    foreach ($entry in Get-ChildItem -LiteralPath $content -Recurse -Force) {
        if ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Linked game content is not allowed: $($entry.FullName)" }
    }
}
New-Item -ItemType Directory -Path $run | Out-Null
foreach ($sub in @('Save','Save\Players','Save\Worlds','Chaite')) { New-Item -ItemType Directory -Path (Join-Path $run $sub) | Out-Null }
$probe = Join-Path $run 'Chaite.GameProbe.dll'
& $compiler /nologo /target:library /platform:x86 /optimize+ /langversion:latest "/out:$probe" "/r:$exe" "/r:$plugin" "/r:$core" "/r:$xna" "/r:$xnaGame" "/r:$xnaGraphics" $probeSource
if ($LASTEXITCODE -ne 0) { throw 'Probe compile failed' }
$patcherTool = Join-Path $run 'GameProbePatcher.exe'
& $compiler /nologo /target:exe /platform:x86 /optimize+ "/out:$patcherTool" "/r:$cecil" "/r:$patcher" $patcherSource
if ($LASTEXITCODE -ne 0) { throw 'Probe patcher compile failed' }
foreach ($inputPath in @($cecil, $patcher, $core)) { Copy-FreshFile $inputPath (Join-Path $run ([IO.Path]::GetFileName($inputPath))) }
& $patcherTool $exe $plugin $probe $run $(if ($Headless) { 'headless' } else { 'client' })
if ($LASTEXITCODE -ne 0) { throw 'Probe patcher failed' }
foreach ($name in $dependencies.Keys) { Copy-FreshFile (Join-Path $GameDirectory $name) (Join-Path $run $name) }
if (-not $Headless) { Copy-Item -LiteralPath $content -Destination (Join-Path $run 'Content') -Recurse }
Copy-FreshFile $config (Join-Path $run 'Chaite\config.json')
foreach ($inputPath in $inputHashes.Keys) {
    if ((Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash -ne $inputHashes[$inputPath]) {
        throw "Preparation input changed concurrently; this run must not launch: $inputPath"
    }
}

# Metadata-only validation of the final harness, not Assembly.Load of Terraria.
[void][Reflection.Assembly]::LoadFrom($cecil)
$parameters = New-Object Mono.Cecil.ReaderParameters
$parameters.InMemory = $true
$prepared = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $run 'Terraria.exe'), $parameters)
$preparedPlugin = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $run 'Chaite.Plugin.dll'), $parameters)
try {
    $program = $prepared.GetType('Terraria.Program')
    $launch = @($program.Methods | Where-Object { $_.Name -eq 'LaunchGame' })
    $runGame = @($program.Methods | Where-Object { $_.Name -eq 'RunGame' })
    if ($launch.Count -ne 1 -or $runGame.Count -ne 1) { throw 'Probe entry methods are ambiguous.' }
    $launchCalls = @($launch[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] })
    $guard = @($launchCalls | Where-Object { $_.Operand.DeclaringType.Name -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'ValidateLaunch' })
    $logging = @($launchCalls | Where-Object { $_.Operand.DeclaringType.FullName -eq 'Terraria.Program' -and $_.Operand.Name -eq 'SetupLogging' })
    if ($guard.Count -ne 1 -or $logging.Count -ne 1 -or $guard[0].Offset -ge $logging[0].Offset) { throw 'Mandatory launch guard must execute before logging and Main construction.' }
    $saveAssignment = @($launch[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'stsfld' -and $_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.Name -eq 'SavePath' })
    if ($saveAssignment.Count -ne 1 -or $saveAssignment[0].Next -ne $guard[0]) { throw 'Launch guard must be immediately after the SavePath assignment.' }
    $runCalls = @($runGame[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] })
    if (@($runCalls | Where-Object { $_.Operand.DeclaringType.FullName -eq 'Terraria.Social.SocialAPI' -and $_.Operand.Name -eq 'Initialize' }).Count -ne 0) { throw 'Native Steam/social initialization remains in probe.' }
    $expectedEntry = if ($Headless) { 'RunHeadless' } else { 'InitializeSocial' }
    if (@($runCalls | Where-Object { $_.Operand.DeclaringType.Name -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq $expectedEntry }).Count -ne 1) { throw 'Missing isolated probe initialization.' }
    $poll = @($preparedPlugin.GetType('Chaite.Plugin.HotkeyPoller').Methods | Where-Object { $_.Name -eq 'Poll' })
    if ($poll.Count -ne 1) { throw 'Synthetic hotkey entry is ambiguous.' }
    foreach ($hook in @('ActivateDown', 'CancelDown')) {
        if (@($poll[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq $hook }).Count -ne 1) { throw "Missing synthetic hotkey: $hook" }
    }
} finally {
    $preparedPlugin.Dispose()
    $prepared.Dispose()
}
$files = @(Get-ChildItem -LiteralPath $run -Recurse -File | Sort-Object FullName | ForEach-Object {
    Assert-NoReparse $_.FullName
    [pscustomobject]@{ Path = $_.FullName.Substring($run.Length + 1).Replace('\', '/'); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$manifest = [ordered]@{
    Schema = 'chaite-isolated-probe/v1'; RunId = [guid]::NewGuid().ToString(); CreatedUtc = [DateTime]::UtcNow.ToString('o')
    Mode = $(if ($Headless) { 'headless' } else { 'client' }); GameVersion = '1.4.5.8'; SourceGameSha256 = $expected
    SaveDirectory = 'Save'; SocialDisabled = $true; LaunchGuardBeforeLogging = $true; SyntheticHotkeysOnly = $true
    InputPluginSha256 = $inputHashes[$plugin]; InputCoreSha256 = $inputHashes[$core]
    ProbeSourceSha256 = $inputHashes[$probeSource]; ProbePatcherSourceSha256 = $inputHashes[$patcherSource]
    Files = $files
}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $run 'probe-manifest.json') -Encoding UTF8
Write-Output "Prepared isolated game (NOT launched): $run"
Write-Output 'Launch ONLY through start-isolated-test.ps1; it validates the manifest and forces the isolated Save path.'

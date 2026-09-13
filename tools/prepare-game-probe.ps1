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
function Get-RequiredFileSha256([string]$Path, [string]$Name) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw "Missing $Name path." }
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) { throw "Missing $Name file." }
    $hash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash
    if ($hash -cnotmatch '^[A-F0-9]{64}$') { throw "Invalid $Name SHA256." }
    return $hash
}
function Write-NewUtf8JsonFile([string]$Path, $Value, [int]$Depth, [string]$Name) {
    $fullPath = [IO.Path]::GetFullPath($Path)
    Assert-NoReparse $fullPath
    $json = $Value | ConvertTo-Json -Depth $Depth
    $bytes = [Text.UTF8Encoding]::new($false, $true).GetBytes($json + [Environment]::NewLine)
    try {
        $stream = [IO.File]::Open($fullPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    } catch { throw "Refusing to overwrite or race an existing $Name file." }
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) }
    finally { $stream.Dispose() }
}
function New-ProbeStaticEvidence(
    [string]$RunName,
    $RunId,
    $IlValidationPassed,
    [string]$ManifestPath,
    [string]$PreparedTerrariaPath,
    [string]$PreparedGameProbePath,
    [string]$PreparedDesktopHostPath,
    [string]$PrepareGameProbeScriptPath,
    [string]$StartIsolatedTestScriptPath,
    [string]$GameProbeSourcePath,
    [string]$GameProbePatcherSourcePath,
    [string]$DesktopHostSourcePath,
    [string]$RunBossValidationScriptPath,
    [string]$TestBossEvidenceScriptPath
) {
    if ($RunName -notmatch '^game-probe-[a-zA-Z0-9-]+$') { throw 'Invalid static-evidence run name.' }
    if ($RunId -isnot [string]) { throw 'Static-evidence RunId must be a JSON string scalar.' }
    $parsedRunId = [guid]::Empty
    if (-not [guid]::TryParse($RunId, [ref]$parsedRunId) -or $parsedRunId -eq [guid]::Empty) { throw 'Invalid static-evidence RunId.' }
    if ($IlValidationPassed -isnot [bool] -or $IlValidationPassed -ne $true) {
        throw 'Static evidence may only be produced after successful IL validation.'
    }
    $manifestFullPath = [IO.Path]::GetFullPath($ManifestPath)
    $preparedTerrariaFullPath = [IO.Path]::GetFullPath($PreparedTerrariaPath)
    $preparedGameProbeFullPath = [IO.Path]::GetFullPath($PreparedGameProbePath)
    $preparedDesktopHostFullPath = [IO.Path]::GetFullPath($PreparedDesktopHostPath)
    $runDirectory = [IO.Path]::GetDirectoryName($manifestFullPath)
    if ([IO.Path]::GetFileName($runDirectory) -cne $RunName -or
        [IO.Path]::GetFileName($manifestFullPath) -cne 'probe-manifest.json' -or
        [IO.Path]::GetFileName($preparedTerrariaFullPath) -cne 'Terraria.exe' -or
        [IO.Path]::GetDirectoryName($preparedTerrariaFullPath) -cne $runDirectory -or
        [IO.Path]::GetFileName($preparedGameProbeFullPath) -cne 'Chaite.GameProbe.dll' -or
        [IO.Path]::GetDirectoryName($preparedGameProbeFullPath) -cne $runDirectory -or
        [IO.Path]::GetFileName($preparedDesktopHostFullPath) -cne 'Chaite.DesktopHost.exe' -or
        [IO.Path]::GetDirectoryName($preparedDesktopHostFullPath) -cne $runDirectory) {
        throw 'Static evidence paths are not bound to the declared prepared run.'
    }
    return [ordered]@{
        Schema = 'chaite-probe-static-evidence/v2'
        RunId = $RunId
        RunName = $RunName
        IlValidationPassed = $true
        ManifestSha256 = Get-RequiredFileSha256 $manifestFullPath 'probe manifest'
        PreparedTerrariaSha256 = Get-RequiredFileSha256 $preparedTerrariaFullPath 'prepared Terraria'
        PreparedGameProbeSha256 = Get-RequiredFileSha256 $preparedGameProbeFullPath 'prepared GameProbe'
        PreparedDesktopHostSha256 = Get-RequiredFileSha256 $preparedDesktopHostFullPath 'prepared DesktopHost'
        PrepareGameProbeScriptSha256 = Get-RequiredFileSha256 $PrepareGameProbeScriptPath 'prepare-game-probe script'
        StartIsolatedTestScriptSha256 = Get-RequiredFileSha256 $StartIsolatedTestScriptPath 'start-isolated-test script'
        GameProbeSourceSha256 = Get-RequiredFileSha256 $GameProbeSourcePath 'GameProbe source'
        GameProbePatcherSourceSha256 = Get-RequiredFileSha256 $GameProbePatcherSourcePath 'GameProbePatcher source'
        DesktopHostSourceSha256 = Get-RequiredFileSha256 $DesktopHostSourcePath 'DesktopHost source'
        RunBossValidationScriptSha256 = Get-RequiredFileSha256 $RunBossValidationScriptPath 'run-boss-validation script'
        TestBossEvidenceScriptSha256 = Get-RequiredFileSha256 $TestBossEvidenceScriptPath 'test-boss-evidence script'
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
$desktopHostSource = Join-Path $PSScriptRoot 'Chaite.DesktopHost.cs'
$prepareScript = [IO.Path]::GetFullPath($PSCommandPath)
$startScript = Join-Path $PSScriptRoot 'start-isolated-test.ps1'
$runnerScript = Join-Path $PSScriptRoot 'run-boss-validation.ps1'
$evidenceTestScript = Join-Path $PSScriptRoot 'test-boss-evidence.ps1'
$staticEvidenceFile = 'probe-static-evidence.json'
$config = Join-Path $root 'src\Chaite.Plugin\config.json'
$inputHashes = @{}
foreach ($inputPath in @($exe, $plugin, $core, $patcher, $cecil, $probeSource, $patcherSource, $desktopHostSource,
    $prepareScript, $startScript, $runnerScript, $evidenceTestScript, $config)) {
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
$desktopHost = Join-Path $run 'Chaite.DesktopHost.exe'
& $compiler /nologo /target:exe /platform:anycpu /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$desktopHost" $desktopHostSource
if ($LASTEXITCODE -ne 0) { throw 'Desktop host compile failed' }
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
$preparedTerrariaPath = Join-Path $run 'Terraria.exe'
$validatedPreparedTerrariaSha256 = Get-RequiredFileSha256 $preparedTerrariaPath 'prepared Terraria before static validation'
$validatedPreparedGameProbeSha256 = Get-RequiredFileSha256 $probe 'prepared GameProbe before static validation'
$validatedPreparedDesktopHostSha256 = Get-RequiredFileSha256 $desktopHost 'prepared DesktopHost before static validation'
$ilValidationPassed = $false

# Metadata-only validation of the final harness, not Assembly.Load of Terraria.
[void][Reflection.Assembly]::LoadFrom($cecil)
$parameters = New-Object Mono.Cecil.ReaderParameters
$parameters.InMemory = $true
$sourceGame = [Mono.Cecil.ModuleDefinition]::ReadModule($exe, $parameters)
$prepared = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $run 'Terraria.exe'), $parameters)
$preparedPlugin = [Mono.Cecil.ModuleDefinition]::ReadModule((Join-Path $run 'Chaite.Plugin.dll'), $parameters)
$sourcePlugin = [Mono.Cecil.ModuleDefinition]::ReadModule($plugin, $parameters)
$preparedProbe = [Mono.Cecil.ModuleDefinition]::ReadModule($probe, $parameters)
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
    $sourceApplyPlan = @($sourcePlugin.GetType('Chaite.Plugin.TerrariaFacade').Methods | Where-Object { $_.Name -eq 'ApplyPlan' })
    $preparedApplyPlan = @($preparedPlugin.GetType('Chaite.Plugin.TerrariaFacade').Methods | Where-Object { $_.Name -eq 'ApplyPlan' })
    if ($sourceApplyPlan.Count -ne 1 -or $preparedApplyPlan.Count -ne 1) { throw 'ApplyPlan observer entry point is ambiguous.' }
    $sourceApplyReturns = @($sourceApplyPlan[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })
    $preparedApplyReturns = @($preparedApplyPlan[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })
    if ($sourceApplyReturns.Count -eq 0) { throw 'Source ApplyPlan has no normal return to observe.' }
    $beforeApplyPlan = @($preparedApplyPlan[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'ObserveApplyPlanBefore'
    })
    $afterApplyPlan = @($preparedApplyPlan[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'ObserveApplyPlanAfter'
    })
    $allApplyPlanProbeReferences = @($preparedApplyPlan[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe'
    })
    if ($beforeApplyPlan.Count -ne 1 -or $afterApplyPlan.Count -ne $sourceApplyReturns.Count -or
        $allApplyPlanProbeReferences.Count -ne (1 + $sourceApplyReturns.Count) -or
        $preparedApplyReturns.Count -ne $sourceApplyReturns.Count -or
        $preparedApplyPlan[0].Body.Instructions.Count -ne ($sourceApplyPlan[0].Body.Instructions.Count + 3 + 2 * $sourceApplyReturns.Count)) {
        throw 'ApplyPlan observer call count or exact instruction delta is invalid.'
    }
    if ($preparedApplyPlan[0].Body.Instructions[0].OpCode.Name -ne 'ldarg.1' -or
        $preparedApplyPlan[0].Body.Instructions[1].OpCode.Name -ne 'ldarg.2' -or
        $preparedApplyPlan[0].Body.Instructions[2] -ne $beforeApplyPlan[0]) {
        throw 'ApplyPlan pre-observer must precede the complete original body.'
    }
    foreach ($ret in $preparedApplyReturns) {
        if ($ret.Previous.Operand -isnot [Mono.Cecil.MethodReference] -or
            $ret.Previous.Operand.DeclaringType.FullName -ne 'ChaiteGameProbe' -or
            $ret.Previous.Operand.Name -ne 'ObserveApplyPlanAfter' -or
            $ret.Previous.Previous.OpCode.Name -ne 'ldarg.1') {
            throw 'A prepared ApplyPlan return lacks its paired post-observer.'
        }
    }
    $sourceAudioPlay = @($sourcePlugin.GetType('Chaite.Plugin.AudioCuePlayer').Methods | Where-Object { $_.Name -eq 'Play' })
    $preparedAudioPlay = @($preparedPlugin.GetType('Chaite.Plugin.AudioCuePlayer').Methods | Where-Object { $_.Name -eq 'Play' })
    if ($sourceAudioPlay.Count -ne 1 -or $preparedAudioPlay.Count -ne 1) { throw 'Audio cue observer entry point is ambiguous.' }
    $audioCueObservers = @($preparedAudioPlay[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'ObserveAudioCue'
    })
    if ($audioCueObservers.Count -ne 1 -or
        $preparedAudioPlay[0].Body.Instructions.Count -ne ($sourceAudioPlay[0].Body.Instructions.Count + 2) -or
        $preparedAudioPlay[0].Body.Instructions[0].OpCode.Name -ne 'ldarg.1' -or
        $preparedAudioPlay[0].Body.Instructions[1] -ne $audioCueObservers[0]) {
        throw 'Audio cue observer must be one value-preserving prefix in the workspace plugin copy.'
    }
    $probeApi = $preparedProbe.GetType('ChaiteGameProbe')
    $probeBefore = @($probeApi.Methods | Where-Object { $_.Name -eq 'ObserveApplyPlanBefore' })
    $probeAfter = @($probeApi.Methods | Where-Object { $_.Name -eq 'ObserveApplyPlanAfter' })
    if ($probeBefore.Count -ne 1 -or $probeAfter.Count -ne 1 -or
        -not $probeBefore[0].IsPublic -or -not $probeBefore[0].IsStatic -or
        -not $probeAfter[0].IsPublic -or -not $probeAfter[0].IsStatic) {
        throw 'ApplyPlan observers must remain unique public static test entry points.'
    }
    $probeAudioCue = @($probeApi.Methods | Where-Object { $_.Name -eq 'ObserveAudioCue' })
    if ($probeAudioCue.Count -ne 1 -or -not $probeAudioCue[0].IsPublic -or -not $probeAudioCue[0].IsStatic -or
        $probeAudioCue[0].ReturnType.FullName -cne 'System.Void' -or
        ($probeAudioCue[0].Parameters.ParameterType.FullName -join '|') -cne 'Chaite.Core.AudioCue') {
        throw 'Audio cue observer must remain one public static read-only entry point.'
    }
    $planTargetNativeReads = @($probeBefore[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.FieldReference]
    } | ForEach-Object { $_.Operand.FullName })
    foreach ($requiredRead in @(
        'Terraria.NPC[] Terraria.Main::npc', 'System.Int32 Terraria.NPC::type',
        'System.Boolean Terraria.NPC::active', 'System.Boolean Terraria.NPC::boss',
        'System.Int32 Terraria.NPC::life', 'System.Int32 Terraria.NPC::lifeMax')) {
        if ($planTargetNativeReads -cnotcontains $requiredRead) {
            throw "ApplyPlan entry observer no longer copies native target identity field: $requiredRead"
        }
    }
    $observerNativeWrites = @($probeBefore[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -in @('stfld','stsfld') -and $_.Operand -is [Mono.Cecil.FieldReference] -and
        $_.Operand.DeclaringType.Namespace.StartsWith('Terraria', [StringComparison]::Ordinal)
    })
    if ($observerNativeWrites.Count -ne 0) { throw 'ApplyPlan entry observer writes native Terraria state.' }
    foreach ($methodName in @('AfterNativeUpdate','CaptureBattleObservation')) {
        $method = @($probeApi.Methods | Where-Object { $_.Name -eq $methodName })
        $poisonReads = @($method[0].Body.Instructions | Where-Object {
            $_.OpCode.Name -eq 'ldfld' -and $_.Operand -is [Mono.Cecil.FieldReference] -and
            $_.Operand.FullName -ceq 'System.Boolean Terraria.Player::poisoned'
        })
        $poisonWrites = @($method[0].Body.Instructions | Where-Object {
            $_.OpCode.Name -eq 'stfld' -and $_.Operand -is [Mono.Cecil.FieldReference] -and
            $_.Operand.FullName -ceq 'System.Boolean Terraria.Player::poisoned'
        })
        if ($method.Count -ne 1 -or $poisonReads.Count -ne 1 -or $poisonWrites.Count -ne 0) {
            throw "Test-only poison telemetry must read Player.poisoned exactly once without writes: $methodName"
        }
    }
    $battleScope = @($probeApi.Methods | Where-Object { $_.Name -eq 'get_IsBattleObservation' })
    if ($battleScope.Count -ne 1) { throw 'Boss-observation runtime scope guard is ambiguous.' }
    $battleScopeFields = @($battleScope[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe/ScenarioSpec'
    } | ForEach-Object { $_.Operand.Name })
    foreach ($requiredScopeField in @('Motion', 'Flight', 'BossTypes')) {
        if ($battleScopeFields -notcontains $requiredScopeField) { throw "Boss-observation runtime scope no longer checks $requiredScopeField." }
    }
    foreach ($observerName in @('ObserveApplyPlanBefore', 'ObserveApplyPlanAfter', 'ObserveBattleAfterNative', 'CaptureBattleObservation')) {
        $observer = @($probeApi.Methods | Where-Object { $_.Name -eq $observerName })
        $scopeCalls = @($observer[0].Body.Instructions | Where-Object {
            $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
            $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'get_IsBattleObservation'
        })
        if ($observer.Count -ne 1 -or $scopeCalls.Count -ne 1) { throw "Test observer lacks the explicit Motion/Flight/Boss runtime scope: $observerName" }
    }
    $playerType = $prepared.GetType('Terraria.Player')
    $sourcePlayerType = $sourceGame.GetType('Terraria.Player')
    $sourceHurt = @($sourcePlayerType.Methods | Where-Object { $_.Name -eq 'Hurt' })
    $preparedHurt = @($playerType.Methods | Where-Object { $_.Name -eq 'Hurt' })
    $hurtParameters = 'Terraria.DataStructures.PlayerDeathReason|System.Int32|System.Int32|System.Boolean|System.Boolean|System.Boolean|System.Int32|System.Boolean'
    if ($sourceHurt.Count -ne 1 -or $preparedHurt.Count -ne 1 -or
        ($sourceHurt[0].Parameters.ParameterType.FullName -join '|') -cne $hurtParameters -or
        ($preparedHurt[0].Parameters.ParameterType.FullName -join '|') -cne $hurtParameters -or
        $sourceHurt[0].ReturnType.FullName -cne 'System.Double' -or $preparedHurt[0].ReturnType.FullName -cne 'System.Double' -or
        -not $sourceHurt[0].IsPublic -or $sourceHurt[0].IsStatic -or -not $sourceHurt[0].HasThis -or
        -not $preparedHurt[0].IsPublic -or $preparedHurt[0].IsStatic -or -not $preparedHurt[0].HasThis -or
        $sourceHurt[0].Body.ExceptionHandlers.Count -ne 0 -or $preparedHurt[0].Body.ExceptionHandlers.Count -ne 0 -or
        $sourceHurt[0].Body.Variables.Count -ne 56 -or $preparedHurt[0].Body.Variables.Count -ne 56 -or
        $sourceHurt[0].Body.Instructions.Count -ne 1475) { throw 'Unreviewed native Player.Hurt signature/body.' }
    $sourceHurtReturns = @($sourceHurt[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })
    $preparedHurtReturns = @($preparedHurt[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })
    if ($sourceHurtReturns.Count -ne 8 -or $preparedHurtReturns.Count -ne 8 -or
        $preparedHurt[0].Body.Instructions.Count -ne ($sourceHurt[0].Body.Instructions.Count + 10 + 2 * $sourceHurtReturns.Count)) {
        throw 'Player.Hurt return count or exact observer instruction delta changed.'
    }
    foreach ($ret in $sourceHurtReturns) {
        foreach ($instruction in $sourceHurt[0].Body.Instructions) {
            $direct = $instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and [Object]::ReferenceEquals($instruction.Operand, $ret)
            $switched = $instruction.Operand -is [Mono.Cecil.Cil.Instruction[]] -and @($instruction.Operand | Where-Object { [Object]::ReferenceEquals($_, $ret) }).Count -gt 0
            if ($direct -or $switched) { throw 'A source Player.Hurt return has an incoming branch; post-observation is not proven safe.' }
        }
    }
    $sourceFirst = $sourceHurt[0].Body.Instructions[0]
    foreach ($instruction in $sourceHurt[0].Body.Instructions) {
        $direct = $instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and [Object]::ReferenceEquals($instruction.Operand, $sourceFirst)
        $switched = $instruction.Operand -is [Mono.Cecil.Cil.Instruction[]] -and @($instruction.Operand | Where-Object { [Object]::ReferenceEquals($_, $sourceFirst) }).Count -gt 0
        if ($direct -or $switched) { throw 'The source Player.Hurt first instruction has an incoming branch; entry observation can be bypassed.' }
    }
    $hurtBeforeCalls = @($preparedHurt[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'ObservePlayerHurtBefore'
    })
    $hurtAfterCalls = @($preparedHurt[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'ObservePlayerHurtAfter'
    })
    if ($hurtBeforeCalls.Count -ne 1 -or $hurtAfterCalls.Count -ne $sourceHurtReturns.Count -or
        $preparedHurt[0].Body.Instructions[0].OpCode.Name -ne 'ldarg.0' -or
        $preparedHurt[0].Body.Instructions[9] -ne $hurtBeforeCalls[0]) { throw 'Player.Hurt entry observer does not receive the untouched this/argument sequence.' }
    for ($index = 0; $index -lt 8; $index++) {
        $load = $preparedHurt[0].Body.Instructions[$index + 1]
        if ($load.OpCode.Name -ne 'ldarg' -or -not [Object]::ReferenceEquals($load.Operand, $preparedHurt[0].Parameters[$index])) {
            throw "Player.Hurt argument $index is not passed unchanged to the entry observer."
        }
    }
    foreach ($ret in $preparedHurtReturns) {
        if ($ret.Previous -notin $hurtAfterCalls -or $ret.Previous.Previous.OpCode.Name -ne 'ldarg.0') {
            throw 'A native Player.Hurt double return lacks its value-preserving post-observer.'
        }
        foreach ($instruction in $preparedHurt[0].Body.Instructions) {
            $direct = $instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and [Object]::ReferenceEquals($instruction.Operand, $ret)
            $switched = $instruction.Operand -is [Mono.Cecil.Cil.Instruction[]] -and @($instruction.Operand | Where-Object { [Object]::ReferenceEquals($_, $ret) }).Count -gt 0
            if ($direct -or $switched) { throw 'A prepared Player.Hurt return can bypass its post-observer.' }
        }
    }
    $injectedHurtInstructions = [Collections.Generic.HashSet[Mono.Cecil.Cil.Instruction]]::new()
    for ($index = 0; $index -lt 10; $index++) { [void]$injectedHurtInstructions.Add($preparedHurt[0].Body.Instructions[$index]) }
    foreach ($ret in $preparedHurtReturns) { [void]$injectedHurtInstructions.Add($ret.Previous); [void]$injectedHurtInstructions.Add($ret.Previous.Previous) }
    $preservedHurtInstructions = @($preparedHurt[0].Body.Instructions | Where-Object { -not $injectedHurtInstructions.Contains($_) })
    if ($preservedHurtInstructions.Count -ne $sourceHurt[0].Body.Instructions.Count) { throw 'Player.Hurt original instruction set was not preserved.' }
    for ($index = 0; $index -lt $preservedHurtInstructions.Count; $index++) {
        $sourceInstruction = $sourceHurt[0].Body.Instructions[$index]
        $preparedInstruction = $preservedHurtInstructions[$index]
        $sourceOp = $sourceInstruction.OpCode.Name
        if ($sourceInstruction.OpCode.OperandType -eq [Mono.Cecil.Cil.OperandType]::ShortInlineBrTarget) { $sourceOp = $sourceOp.Substring(0, $sourceOp.Length - 2) }
        if ($sourceOp -cne $preparedInstruction.OpCode.Name) { throw "Player.Hurt original opcode changed at instruction $index." }
        $left = $sourceInstruction.Operand; $right = $preparedInstruction.Operand; $sameOperand = $false
        if ($null -eq $left -or $null -eq $right) { $sameOperand = $null -eq $left -and $null -eq $right }
        elseif ($left -is [Mono.Cecil.Cil.Instruction] -and $right -is [Mono.Cecil.Cil.Instruction]) {
            $sameOperand = $sourceHurt[0].Body.Instructions.IndexOf($left) -eq [Array]::IndexOf($preservedHurtInstructions, $right)
        }
        elseif ($left -is [Mono.Cecil.Cil.Instruction[]] -and $right -is [Mono.Cecil.Cil.Instruction[]]) {
            $leftTargets = @($left | ForEach-Object { $sourceHurt[0].Body.Instructions.IndexOf($_) })
            $rightTargets = @($right | ForEach-Object { [Array]::IndexOf($preservedHurtInstructions, $_) })
            $sameOperand = ($leftTargets -join ',') -ceq ($rightTargets -join ',')
        }
        elseif ($left -is [Mono.Cecil.MemberReference] -and $right -is [Mono.Cecil.MemberReference]) { $sameOperand = $left.FullName -ceq $right.FullName }
        elseif ($left -is [Mono.Cecil.Cil.VariableDefinition] -and $right -is [Mono.Cecil.Cil.VariableDefinition]) { $sameOperand = $left.Index -eq $right.Index -and $left.VariableType.FullName -ceq $right.VariableType.FullName }
        elseif ($left -is [Mono.Cecil.ParameterDefinition] -and $right -is [Mono.Cecil.ParameterDefinition]) { $sameOperand = $left.Index -eq $right.Index -and $left.ParameterType.FullName -ceq $right.ParameterType.FullName }
        else { $sameOperand = $left.GetType() -eq $right.GetType() -and $left.Equals($right) }
        if (-not $sameOperand) { throw "Player.Hurt original operand/control-flow target changed at instruction $index." }
    }
    $hurtProbeBefore = @($probeApi.Methods | Where-Object { $_.Name -eq 'ObservePlayerHurtBefore' })
    $hurtProbeAfter = @($probeApi.Methods | Where-Object { $_.Name -eq 'ObservePlayerHurtAfter' })
    if ($hurtProbeBefore.Count -ne 1 -or $hurtProbeAfter.Count -ne 1 -or
        -not $hurtProbeBefore[0].IsPublic -or -not $hurtProbeBefore[0].IsStatic -or
        -not $hurtProbeAfter[0].IsPublic -or -not $hurtProbeAfter[0].IsStatic -or
        $hurtProbeBefore[0].ReturnType.FullName -cne 'System.Void' -or $hurtProbeAfter[0].ReturnType.FullName -cne 'System.Double' -or
        ($hurtProbeBefore[0].Parameters.ParameterType.FullName -join '|') -cne ('Terraria.Player|' + $hurtParameters) -or
        ($hurtProbeAfter[0].Parameters.ParameterType.FullName -join '|') -cne 'System.Double|Terraria.Player') {
        throw 'Player.Hurt observer public static signatures changed.'
    }
    $hurtAfterReturns = @($hurtProbeAfter[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })
    if ($hurtAfterReturns.Count -ne 1 -or $hurtAfterReturns[0].Previous.OpCode.Name -ne 'ldarg.0' -or
        @($hurtProbeAfter[0].Body.Instructions | Where-Object { $_.OpCode.Name -in @('starg', 'starg.s', 'ldarga', 'ldarga.s') }).Count -ne 0) {
        throw 'Player.Hurt post-observer no longer returns the untouched double argument.'
    }
    foreach ($instruction in $hurtProbeAfter[0].Body.Instructions) {
        $direct = $instruction.Operand -is [Mono.Cecil.Cil.Instruction] -and [Object]::ReferenceEquals($instruction.Operand, $hurtAfterReturns[0])
        $switched = $instruction.Operand -is [Mono.Cecil.Cil.Instruction[]] -and @($instruction.Operand | Where-Object { [Object]::ReferenceEquals($_, $hurtAfterReturns[0]) }).Count -gt 0
        if ($direct -or $switched) { throw 'Player.Hurt post-observer has a branch that can bypass its value-preserving ldarg.0.' }
    }
    foreach ($observer in @($hurtProbeBefore[0], $hurtProbeAfter[0])) {
        $scopeCalls = @($observer.Body.Instructions | Where-Object {
            $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
            $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'get_IsBattleObservation'
        })
        if ($scopeCalls.Count -ne 1 -or $observer.Body.ExceptionHandlers.Count -ne 1 -or
            $observer.Body.ExceptionHandlers[0].HandlerType -ne [Mono.Cecil.Cil.ExceptionHandlerType]::Catch -or
            $observer.Body.ExceptionHandlers[0].CatchType.FullName -cne 'System.Exception') {
            throw 'Player.Hurt observer lost its Boss-only guard or non-throwing boundary.'
        }
    }
    $hurtCapture = @($probeApi.Methods | Where-Object { $_.Name -eq 'CaptureHurtReason' })
    if ($hurtCapture.Count -ne 1 -or -not $hurtCapture[0].IsStatic -or $hurtCapture[0].HasThis -or
        $hurtCapture[0].ReturnType.FullName -cne 'System.Void' -or
        ($hurtCapture[0].Parameters.ParameterType.FullName -join '|') -cne 'Terraria.DataStructures.PlayerDeathReason|Terraria.Player|ChaiteGameProbe/HurtObservationPending&') {
        throw 'Player.Hurt public source reader signature is ambiguous.'
    }
    $hurtPending = @($probeApi.NestedTypes | Where-Object { $_.FullName -eq 'ChaiteGameProbe/HurtObservationPending' })
    if ($hurtPending.Count -ne 1 -or @($hurtPending[0].Fields | Where-Object { $_.Name -eq 'ReasonText' }).Count -ne 0 -or
        @($probeApi.Fields | Where-Object { $_.Name -eq 'hurtObservationReasonTextFailures' }).Count -ne 0) {
        throw 'Randomized death-text state must not exist in Hurt observations.'
    }
    $forbiddenDeathTextCalls = @($probeApi.Methods | Where-Object { $_.HasBody } | ForEach-Object { $_.Body.Instructions } | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -in @('GetDeathText','CreateDeathMessage','RandomFromCategory')
    })
    if ($forbiddenDeathTextCalls.Count -ne 0) { throw 'The probe must not generate randomized native death text.' }
    $beforeCaptureCalls = @($hurtProbeBefore[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and
        $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'CaptureHurtReason'
    })
    $afterCaptureCalls = @($hurtProbeAfter[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'CaptureHurtReason'
    })
    $captureLocalCalls = @($hurtCapture[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe'
    })
    $guardCalls = @($battleScope[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] })
    if ($beforeCaptureCalls.Count -ne 1 -or $afterCaptureCalls.Count -ne 0 -or $captureLocalCalls.Count -ne 0 -or $guardCalls.Count -ne 0) {
        throw 'Player.Hurt hot path gained an unreviewed local helper/call edge.'
    }
    $allowedNativeHurtMethods = @(
        'System.String Terraria.DataStructures.PlayerDeathReason::get_CustomReason()',
        'System.Nullable`1<System.Int32> Terraria.DataStructures.PlayerDeathReason::get_SourceOtherIndex()',
        'System.Nullable`1<System.Int32> Terraria.DataStructures.PlayerDeathReason::get_SourceProjectileType()',
        'System.Boolean Terraria.DataStructures.PlayerDeathReason::TryGetCausingEntity(Terraria.Entity&)'
    )
    $allowedNativeHurtFields = @(
        'System.Int32 Terraria.Entity::whoAmI',
        'Microsoft.Xna.Framework.Vector2 Terraria.Entity::position','Microsoft.Xna.Framework.Vector2 Terraria.Entity::velocity',
        'System.Int32 Terraria.Player::statLife','System.Boolean Terraria.Player::active','System.Boolean Terraria.Player::hostile',
        'System.Int32 Terraria.NPC::type','System.Boolean Terraria.NPC::active','System.Int32 Terraria.NPC::life',
        'System.Int32 Terraria.NPC::lifeMax','System.Int32 Terraria.NPC::damage','System.Boolean Terraria.NPC::friendly',
        'System.Int32 Terraria.Projectile::type','System.Int32 Terraria.Projectile::owner','System.Boolean Terraria.Projectile::active',
        'System.Int32 Terraria.Projectile::damage','System.Boolean Terraria.Projectile::hostile','System.Boolean Terraria.Projectile::friendly'
    )
    $allowedVectorHurtFields = @('System.Single Microsoft.Xna.Framework.Vector2::X','System.Single Microsoft.Xna.Framework.Vector2::Y')
    foreach ($reader in @($hurtProbeBefore[0], $hurtProbeAfter[0], $hurtCapture[0], $battleScope[0])) {
        foreach ($instruction in $reader.Body.Instructions) {
            if ($instruction.OpCode.Name -in @('newarr','box','localloc','calli','ldftn','ldvirtftn')) {
                throw "Player.Hurt hot path gained allocation/indirect-call IL: $($reader.Name) $instruction"
            }
            if ($instruction.Operand -is [Mono.Cecil.FieldReference] -and $instruction.Operand.DeclaringType.FullName.StartsWith('Terraria.', [StringComparison]::Ordinal)) {
                $definition = @($sourceGame.GetType($instruction.Operand.DeclaringType.FullName).Fields | Where-Object { $_.FullName -ceq $instruction.Operand.FullName })
                if ($instruction.Operand.DeclaringType.Scope.Name -cne 'Terraria' -or $instruction.OpCode.Name -ne 'ldfld' -or $definition.Count -ne 1 -or -not $definition[0].IsPublic -or
                    $allowedNativeHurtFields -notcontains $instruction.Operand.FullName) { throw "Hurt observer uses a non-public/write/unreviewed native field: $($instruction.Operand.FullName)" }
            }
            if ($instruction.Operand -is [Mono.Cecil.FieldReference] -and $instruction.Operand.DeclaringType.FullName -eq 'Microsoft.Xna.Framework.Vector2' -and
                ($instruction.OpCode.Name -ne 'ldfld' -or $allowedVectorHurtFields -notcontains $instruction.Operand.FullName)) {
                throw "Hurt observer uses an unreviewed vector field: $($instruction.Operand.FullName)"
            }
            if ($instruction.Operand -is [Mono.Cecil.FieldReference] -and
                -not $instruction.Operand.DeclaringType.FullName.StartsWith('ChaiteGameProbe', [StringComparison]::Ordinal) -and
                -not $instruction.Operand.DeclaringType.FullName.StartsWith('Terraria.', [StringComparison]::Ordinal) -and
                $instruction.Operand.DeclaringType.FullName -cne 'Microsoft.Xna.Framework.Vector2') {
                throw "Player.Hurt hot path gained an unreviewed external field access: $($reader.Name) -> $($instruction.Operand.FullName)"
            }
            if ($instruction.Operand -is [Mono.Cecil.MethodReference]) {
                $called = $instruction.Operand
                $allowed = $false
                if ($called.DeclaringType.FullName -eq 'ChaiteGameProbe') {
                    $allowed = ([Object]::ReferenceEquals($reader, $hurtProbeBefore[0]) -and $called.Name -in @('get_IsBattleObservation','CaptureHurtReason')) -or
                        ([Object]::ReferenceEquals($reader, $hurtProbeAfter[0]) -and $called.Name -eq 'get_IsBattleObservation')
                } elseif ($called.DeclaringType.FullName.StartsWith('Terraria.', [StringComparison]::Ordinal)) {
                    $definition = @($sourceGame.GetType($called.DeclaringType.FullName).Methods | Where-Object { $_.FullName -ceq $called.FullName })
                    $allowed = [Object]::ReferenceEquals($reader, $hurtCapture[0]) -and $called.DeclaringType.Scope.Name -ceq 'Terraria' -and $instruction.OpCode.Name -in @('call','callvirt') -and
                        $definition.Count -eq 1 -and $definition[0].IsPublic -and $allowedNativeHurtMethods -contains $called.FullName
                } elseif ($called.FullName -ceq 'System.Int32 System.Math::Max(System.Int32,System.Int32)') {
                    $allowed = [Object]::ReferenceEquals($reader, $hurtProbeBefore[0]) -and $instruction.OpCode.Name -eq 'call'
                } elseif ($called.Name -eq '.ctor' -and $called.DeclaringType.FullName.StartsWith('System.Nullable`1<', [StringComparison]::Ordinal)) {
                    $allowed = [Object]::ReferenceEquals($reader, $hurtCapture[0]) -and $instruction.OpCode.Name -eq 'newobj' -and
                        $called.DeclaringType.IsValueType -and $called.DeclaringType.Scope.Name -ceq 'mscorlib'
                }
                if (-not $allowed) { throw "Player.Hurt hot path gained an unreviewed call: $($reader.Name) -> $($called.FullName)" }
            }
        }
    }
    $hurtReport = @($probeApi.Methods | Where-Object { $_.Name -eq 'HurtObservationReport' })
    if ($hurtReport.Count -ne 1) { throw 'Hurt observation summary writer is ambiguous.' }
    $hurtFlushKeys = @($hurtReport[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'ldstr' -and $_.Operand -ceq 'flushes'
    })
    $hurtFlushFields = @($hurtReport[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'ldsfld' -and $_.Operand -is [Mono.Cecil.FieldReference] -and
        $_.Operand.FullName -ceq 'System.Int32 ChaiteGameProbe::hurtObservationFlushes'
    })
    if ($hurtFlushKeys.Count -ne 1 -or $hurtFlushFields.Count -ne 1 -or
        $hurtReport[0].Body.Instructions.IndexOf($hurtFlushFields[0]) -le $hurtReport[0].Body.Instructions.IndexOf($hurtFlushKeys[0]) -or
        $hurtReport[0].Body.Instructions.IndexOf($hurtFlushFields[0]) - $hurtReport[0].Body.Instructions.IndexOf($hurtFlushKeys[0]) -gt 2) {
        throw 'Hurt observation summary no longer serializes its real flush counter.'
    }
    $battleReport = @($probeApi.Methods | Where-Object { $_.Name -eq 'BattleObservationReport' })
    $writeResult = @($probeApi.Methods | Where-Object { $_.Name -eq 'WriteResult' })
    if ($battleReport.Count -ne 1 -or $writeResult.Count -ne 1) {
        throw 'Hurt observations are not included and finally flushed in result diagnostics.'
    }
    $battleHurtKeys = @($battleReport[0].Body.Instructions | Where-Object {
        $_.OpCode.Name -eq 'ldstr' -and $_.Operand -ceq 'hurt'
    })
    $battleHurtCalls = @($battleReport[0].Body.Instructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and
        $_.Operand.Name -eq 'HurtObservationReport'
    })
    $writeInstructions = $writeResult[0].Body.Instructions
    $drainCalls = @($writeInstructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and
        $_.Operand.Name -eq 'DrainHurtObservations'
    })
    $flushCalls = @($writeInstructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and
        $_.Operand.Name -eq 'FlushHurtObservations'
    })
    $diagnosticsKeys = @($writeInstructions | Where-Object {
        $_.OpCode.Name -eq 'ldstr' -and $_.Operand -ceq 'diagnostics'
    })
    $battleReportCalls = @($writeInstructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'ChaiteGameProbe' -and
        $_.Operand.Name -eq 'BattleObservationReport'
    })
    $resultWrites = @($writeInstructions | Where-Object {
        $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'System.IO.File' -and
        $_.Operand.Name -eq 'WriteAllText'
    })
    if ($battleHurtKeys.Count -ne 1 -or $battleHurtCalls.Count -ne 1 -or
        -not [Object]::ReferenceEquals($battleHurtKeys[0].Next, $battleHurtCalls[0]) -or
        $drainCalls.Count -ne 1 -or $flushCalls.Count -ne 1 -or
        -not [Object]::ReferenceEquals($drainCalls[0].Next, $flushCalls[0]) -or
        $diagnosticsKeys.Count -ne 1 -or $battleReportCalls.Count -ne 1 -or
        -not [Object]::ReferenceEquals($diagnosticsKeys[0].Next, $battleReportCalls[0]) -or
        $resultWrites.Count -ne 1 -or
        $writeInstructions.IndexOf($drainCalls[0]) -ge $writeInstructions.IndexOf($flushCalls[0]) -or
        $writeInstructions.IndexOf($flushCalls[0]) -ge $writeInstructions.IndexOf($diagnosticsKeys[0]) -or
        $writeInstructions.IndexOf($battleReportCalls[0]) -ge $writeInstructions.IndexOf($resultWrites[0])) {
        throw 'Hurt observations are not drained, flushed, bound to diagnostics, and written in the required order.'
    }
    $playerUpdate = @($playerType.Methods | Where-Object { $_.Name -eq 'Update' -and $_.Parameters.Count -eq 1 })
    $jumpMovement = @($playerType.Methods | Where-Object { $_.Name -eq 'JumpMovement' -and $_.Parameters.Count -eq 0 })
    if ($playerUpdate.Count -ne 1 -or $jumpMovement.Count -ne 1) { throw 'Motion observation entry points are ambiguous.' }
    foreach ($hook in @('MotionBeforePlayerUpdate', 'MotionAfterInput')) {
        if (@($playerUpdate[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq $hook }).Count -ne 1) { throw "Missing native motion observer: $hook" }
    }
    $inputReplay = @($playerUpdate[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'Chaite.Plugin.Runtime' -and $_.Operand.Name -eq 'ApplyPendingInput' })
    if ($inputReplay.Count -ne 1 -or $inputReplay[0].Next.OpCode.Name -ne 'ldarg.0' -or $inputReplay[0].Next.Next.Operand.Name -ne 'MotionAfterInput') { throw 'Motion control replay must immediately follow the production input replay.' }
    foreach ($productionHook in @('Tick','ApplyPendingInput','ApplyPendingSelection')) {
        if (@($playerUpdate[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'Chaite.Plugin.Runtime' -and $_.Operand.Name -eq $productionHook }).Count -ne 1) {
            throw "Production Player.Update hook changed while adding Hurt diagnostics: $productionHook"
        }
    }
    $npcLoot = @($prepared.GetType('Terraria.NPC').Methods | Where-Object { $_.Name -eq 'NPCLoot' -and $_.Parameters.Count -eq 0 })
    if ($npcLoot.Count -ne 1 -or @($npcLoot[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'call' -and $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.FullName -eq 'Chaite.Plugin.Runtime' -and $_.Operand.Name -eq 'OnNpcKilled' }).Count -ne 1) {
        throw 'Production NPC.NPCLoot hook changed while adding Hurt diagnostics.'
    }
    $beforeJump = @($jumpMovement[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'MotionBeforeJump' })
    if ($beforeJump.Count -ne 1 -or $jumpMovement[0].Body.Instructions[0].OpCode.Name -ne 'ldarg.0' -or $jumpMovement[0].Body.Instructions[1] -ne $beforeJump[0]) { throw 'Motion preJump observer must precede the original JumpMovement body.' }
    foreach ($ret in @($jumpMovement[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })) {
        if ($ret.Previous.Operand -isnot [Mono.Cecil.MethodReference] -or $ret.Previous.Operand.DeclaringType.Name -ne 'ChaiteGameProbe' -or $ret.Previous.Operand.Name -ne 'MotionAfterJump') { throw 'A native JumpMovement return lacks its paired postJump observer.' }
    }
    $wingMovement = @($playerType.Methods | Where-Object { $_.Name -eq 'WingMovement' -and $_.Parameters.Count -eq 0 })
    if ($wingMovement.Count -ne 1) { throw 'Flight observation entry point is ambiguous.' }
    $beforeWing = @($wingMovement[0].Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.DeclaringType.Name -eq 'ChaiteGameProbe' -and $_.Operand.Name -eq 'FlightBeforeWing' })
    if ($beforeWing.Count -ne 1 -or $wingMovement[0].Body.Instructions[0].OpCode.Name -ne 'ldarg.0' -or $wingMovement[0].Body.Instructions[1] -ne $beforeWing[0]) { throw 'Flight preWing observer must precede the original WingMovement body.' }
    foreach ($ret in @($wingMovement[0].Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ret' })) {
        if ($ret.Previous.Operand -isnot [Mono.Cecil.MethodReference] -or $ret.Previous.Operand.DeclaringType.Name -ne 'ChaiteGameProbe' -or $ret.Previous.Operand.Name -ne 'FlightAfterWing') { throw 'A native WingMovement return lacks its paired postWing observer.' }
    }
} finally {
    $preparedProbe.Dispose()
    $sourcePlugin.Dispose()
    $preparedPlugin.Dispose()
    $prepared.Dispose()
    $sourceGame.Dispose()
}
$ilValidationPassed = $true
$files = @(Get-ChildItem -LiteralPath $run -Recurse -File | Sort-Object FullName | ForEach-Object {
    Assert-NoReparse $_.FullName
    [pscustomobject]@{ Path = $_.FullName.Substring($run.Length + 1).Replace('\', '/'); Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
})
$runId = [guid]::NewGuid().ToString()
$manifest = [ordered]@{
    Schema = 'chaite-isolated-probe/v2'; RunId = $runId; CreatedUtc = [DateTime]::UtcNow.ToString('o')
    RunName = $RunName; StaticEvidenceFile = $staticEvidenceFile
    Mode = $(if ($Headless) { 'headless' } else { 'client' }); GameVersion = '1.4.5.8'; SourceGameSha256 = $expected
    SaveDirectory = 'Save'; SocialDisabled = $true; LaunchGuardBeforeLogging = $true; SyntheticHotkeysOnly = $true
    InputPluginSha256 = $inputHashes[$plugin]; InputCoreSha256 = $inputHashes[$core]
    ProbeSourceSha256 = $inputHashes[$probeSource]; ProbePatcherSourceSha256 = $inputHashes[$patcherSource]
    PrepareGameProbeScriptSha256 = $inputHashes[$prepareScript]
    StartIsolatedTestScriptSha256 = $inputHashes[$startScript]
    RunBossValidationScriptSha256 = $inputHashes[$runnerScript]
    TestBossEvidenceScriptSha256 = $inputHashes[$evidenceTestScript]
    DesktopHostSourceSha256 = $inputHashes[$desktopHostSource]
    Files = $files
}
$manifestPath = Join-Path $run 'probe-manifest.json'
$staticEvidencePath = Join-Path $run $staticEvidenceFile
Write-NewUtf8JsonFile $manifestPath $manifest 8 'probe manifest'
$staticEvidence = New-ProbeStaticEvidence $RunName $runId $ilValidationPassed $manifestPath $preparedTerrariaPath $probe $desktopHost `
    $prepareScript $startScript $probeSource $patcherSource $desktopHostSource $runnerScript $evidenceTestScript
if ($staticEvidence.PreparedTerrariaSha256 -cne $validatedPreparedTerrariaSha256 -or
    $staticEvidence.PreparedGameProbeSha256 -cne $validatedPreparedGameProbeSha256 -or
    $staticEvidence.PreparedDesktopHostSha256 -cne $validatedPreparedDesktopHostSha256 -or
    $staticEvidence.PrepareGameProbeScriptSha256 -cne $inputHashes[$prepareScript] -or
    $staticEvidence.StartIsolatedTestScriptSha256 -cne $inputHashes[$startScript] -or
    $staticEvidence.GameProbeSourceSha256 -cne $inputHashes[$probeSource] -or
    $staticEvidence.GameProbePatcherSourceSha256 -cne $inputHashes[$patcherSource] -or
    $staticEvidence.DesktopHostSourceSha256 -cne $inputHashes[$desktopHostSource] -or
    $staticEvidence.RunBossValidationScriptSha256 -cne $inputHashes[$runnerScript] -or
    $staticEvidence.TestBossEvidenceScriptSha256 -cne $inputHashes[$evidenceTestScript]) {
    throw 'A statically validated binary or provenance script changed before evidence binding.'
}
Write-NewUtf8JsonFile $staticEvidencePath $staticEvidence 6 'probe static evidence'
Write-Output "Prepared isolated game (NOT launched): $run"
Write-Output 'Launch ONLY through start-isolated-test.ps1; it validates the manifest and forces the isolated Save path.'

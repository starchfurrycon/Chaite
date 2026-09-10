param(
    [switch]$Run,
    [switch]$FunctionsOnly,
    [string]$OutputDirectory,
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria',
    [ValidateRange(0, 2147483647)][int]$Seed = 20260910
)

# Default is a read-only plan. -Run explicitly opts into SIX serial, bounded,
# isolated native-engine processes. No production build or installation occurs.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
$motionCases = @('no-cloud-hold', 'no-cloud-tap', 'no-cloud-release-press', 'cloud-hold', 'cloud-tap', 'cloud-release-press')

function Assert-NoMotionReparse([string]$Path) {
    $candidate = [IO.Path]::GetFullPath($Path)
    while (-not [string]::IsNullOrWhiteSpace($candidate)) {
        $entry = Get-Item -LiteralPath $candidate -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Linked motion-test paths are not allowed: $candidate" }
        $parent = [IO.Directory]::GetParent($candidate)
        if ($null -eq $parent) { break }
        $candidate = $parent.FullName
    }
}
function Motion-Field($Object, [string]$Name) {
    if ($null -eq $Object -or $null -eq $Object.PSObject.Properties[$Name]) { throw "Missing motion evidence: $Name" }
    return $Object.PSObject.Properties[$Name].Value
}
function Motion-Integer($Value, [long]$Expected, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -ne $Expected) { throw "Invalid integer motion evidence: $Name" }
}
function Motion-Boolean($Value, [bool]$Expected, [string]$Name) {
    if ($Value -isnot [bool] -or $Value -ne $Expected) { throw "Invalid Boolean motion evidence: $Name" }
}
function Motion-Number($Value, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long] -and $Value -isnot [double] -and $Value -isnot [decimal]) -or
        [double]::IsNaN([double]$Value) -or [double]::IsInfinity([double]$Value)) { throw "Invalid finite motion number: $Name" }
}
function Assert-MotionEvidence($Result, [object[]]$Frames, [string]$Case, [int]$ExpectedSeed) {
    if ((Motion-Field $Result 'schema') -cne 'chaite-native-motion-result/v1' -or
        (Motion-Field $Result 'scenario') -cne 'motion-jump' -or (Motion-Field $Result 'motionCase') -cne $Case -or
        (Motion-Field $Result 'status') -cne 'complete' -or (Motion-Field $Result 'difficulty') -cne 'classic' -or
        (Motion-Field $Result 'frameFile') -cne 'motion-frames.jsonl') { throw 'Motion result identity/status mismatch.' }
    if ($Case -cnotin @('no-cloud-hold', 'no-cloud-tap', 'no-cloud-release-press', 'cloud-hold', 'cloud-tap', 'cloud-release-press')) { throw 'Unreviewed motion evidence case.' }
    Motion-Integer (Motion-Field $Result 'seed') $ExpectedSeed 'seed'
    Motion-Integer (Motion-Field $Result 'difficultyCode') 0 'difficultyCode'
    Motion-Integer (Motion-Field $Result 'processExitCode') 0 'processExitCode'
    foreach ($key in @('ticks', 'nativeFrames', 'recordedFrames', 'totalFrames')) { Motion-Integer (Motion-Field $Result $key) 180 $key }
    Motion-Integer (Motion-Field $Result 'warmupFrames') 20 'warmupFrames'
    foreach ($key in @('validMotion', 'nativeDifficultyVerified', 'sawAirborne', 'returnedGroundAfterRelease')) { Motion-Boolean (Motion-Field $Result $key) $true $key }
    Motion-Boolean (Motion-Field $Result 'cloudConsumed') ($Case -ceq 'cloud-release-press') 'cloudConsumed'
    $native = Motion-Field $Result 'nativeDifficulty'
    foreach ($key in @('gameMode', 'worldFileGameMode')) { Motion-Integer (Motion-Field $native $key) 0 $key }
    Motion-Integer (Motion-Field $native 'difficulty') 1 'native difficulty'
    Motion-Integer (Motion-Field $native 'worldFileSeed') $ExpectedSeed 'worldFileSeed'
    foreach ($key in @('expertMode', 'masterMode', 'hardMode', 'forTheWorthy')) { Motion-Boolean (Motion-Field $native $key) $false $key }
    $rng = Motion-Field $Result 'nativeRandom'
    Motion-Integer (Motion-Field $rng 'seed') $ExpectedSeed 'RNG seed'
    Motion-Integer (Motion-Field $rng 'unpausedUpdateSeedInitial') $ExpectedSeed 'RNG initial'
    Motion-Integer (Motion-Field $rng 'unpausedUpdateSeedAdvances') 180 'RNG advances'
    Motion-Integer (Motion-Field $rng 'referenceChecks') 360 'RNG reference checks'
    foreach ($key in @('installedAfterSetup', 'actualAndNativeNamedColdStateVerified')) { Motion-Boolean (Motion-Field $rng $key) $true $key }
    Motion-Boolean (Motion-Field $rng 'actualStreamConsumedForFingerprint') $false 'fingerprint consumption'
    $fingerprint = @(Motion-Field $rng 'independentTwinFingerprint')
    if ($fingerprint.Count -ne 8) { throw 'Motion RNG fingerprint length mismatch.' }
    foreach ($sample in $fingerprint) { if (($sample -isnot [int] -and $sample -isnot [long]) -or $sample -lt 0 -or $sample -gt 2147483647) { throw 'Invalid motion RNG fingerprint sample.' } }
    [decimal]$expectedLcg = $ExpectedSeed
    for ($index = 0; $index -lt 180; $index++) { $expectedLcg = ($expectedLcg * [decimal]25214903917 + [decimal]11) % [decimal]281474976710656 }
    Motion-Integer (Motion-Field $rng 'unpausedUpdateSeedFinal') ([long]$expectedLcg) 'independently calculated final RNG'
    $arena = Motion-Field $Result 'arena'
    Motion-Integer (Motion-Field $arena 'nativeSceneMetricRefreshes') 180 'native scene refreshes'
    Motion-Integer (Motion-Field $arena 'groundTop') 500 'groundTop'
    if ((Motion-Field $arena 'groundTile') -cne 'GrayBrick' -or @(Motion-Field $arena 'platformRows').Count -ne 0) { throw 'Wrong motion terrain profile.' }
    $equipment = Motion-Field $Result 'equipment'
    $hasCloud = $Case.StartsWith('cloud-', [StringComparison]::Ordinal)
    Motion-Boolean (Motion-Field $equipment 'cloudEquipped') $hasCloud 'cloud equipment'
    Motion-Boolean (Motion-Field $equipment 'noWeaponsAmmoConsumablesOrMount') $true 'no other equipment'
    Motion-Boolean (Motion-Field $equipment 'noDirectJumpStateOverrides') $true 'no jump state overrides'
    Motion-Integer (Motion-Field $equipment 'cloudItemType') $(if ($hasCloud) { 53 } else { 0 }) 'cloud item ID'
    Motion-Integer (Motion-Field $equipment 'cloudPrefix') 0 'cloud prefix'
    if ($Frames.Count -ne 180) { throw 'Motion trace must contain every warmup and measured frame exactly once.' }
    for ($index = 0; $index -lt $Frames.Count; $index++) {
        $frame = $Frames[$index]
        $tick = $index + 1
        if ((Motion-Field $frame 'schema') -cne 'chaite-native-motion-frame/v1' -or (Motion-Field $frame 'productionSessionState') -cne 'Idle') { throw 'Wrong frame schema or unexpected production takeover.' }
        Motion-Integer (Motion-Field $frame 'tick') $tick 'sequential tick'
        Motion-Integer (Motion-Field $frame 'nativeFrameBefore') $index 'native frame before'
        Motion-Integer (Motion-Field $frame 'nativeFrameAfter') $tick 'native frame after'
        Motion-Integer (Motion-Field $frame 'playerUpdateCalls') 1 'Player.Update count'
        Motion-Integer (Motion-Field $frame 'jumpMovementCalls') 1 'JumpMovement count'
        $expectedJump = $tick -ge 21 -and $tick -le 80
        if ($Case.EndsWith('-tap', [StringComparison]::Ordinal)) { $expectedJump = $tick -eq 21 }
        elseif ($Case.EndsWith('-release-press', [StringComparison]::Ordinal) -and $tick -eq 26) { $expectedJump = $false }
        Motion-Boolean (Motion-Field $frame 'requestedJump') $expectedJump 'declared input schedule'
        foreach ($moment in @('prePlayer', 'preJump', 'postJump', 'postPlayer')) {
            $snapshot = Motion-Field $frame $moment
            Motion-Integer (Motion-Field $snapshot 'tick') $tick "$moment tick"
            # The headless outer loop sets _gameUpdateCount=tick before its
            # scene refresh. Native DoUpdateInWorld_Inner increments it again
            # BEFORE Player.Update (verified 1.4.5.8 IL 0000..0007).
            Motion-Integer (Motion-Field $snapshot 'gameUpdateCount') ($tick + 1) "$moment native game count"
            Motion-Integer (Motion-Field $snapshot 'nativeFramesCompleted') $index "$moment native completed count"
            foreach ($key in @('jump', 'jumpHeight')) {
                $number = Motion-Field $snapshot $key
                if (($number -isnot [int] -and $number -isnot [long]) -or $number -lt 0 -or $number -gt 1000) { throw "Invalid $moment $key" }
            }
            foreach ($key in @('jumpSpeed', 'jumpSpeedBoost', 'gravity', 'maxFallSpeed', 'gravDir', 'bottomY')) { Motion-Number (Motion-Field $snapshot $key) "$moment $key" }
            foreach ($key in @('releaseJump', 'canJumpAgain_Cloud', 'hasJumpOption_Cloud', 'isPerformingJump_Cloud')) {
                if ((Motion-Field $snapshot $key) -isnot [bool]) { throw "Invalid $moment $key Boolean" }
            }
            foreach ($vector in @('position', 'velocity')) {
                foreach ($axis in @('x', 'y')) { Motion-Number (Motion-Field (Motion-Field $snapshot $vector) $axis) "$moment $vector.$axis" }
            }
            Motion-Integer (Motion-Field $snapshot 'life') 400 "$moment life"
            Motion-Boolean (Motion-Field $snapshot 'dead') $false "$moment dead"
            $controls = Motion-Field $snapshot 'controls'
            if ((Motion-Field $controls 'jump') -isnot [bool]) { throw 'Invalid snapshot jump control Boolean.' }
            foreach ($key in @('left', 'right', 'up', 'down', 'useItem', 'useTile', 'hook', 'mount', 'dash')) { Motion-Boolean (Motion-Field $controls $key) $false "unrequested control $key" }
        }
        Motion-Boolean (Motion-Field (Motion-Field $frame.preJump 'controls') 'jump') $expectedJump 'effective preJump control'
        if ($tick -gt 20) { Motion-Boolean (Motion-Field $frame.preJump 'hasJumpOption_Cloud') $hasCloud 'native cloud capability' }
        if ($tick -le 20 -and ([Math]::Abs([double]$frame.postPlayer.bottomY - 8000) -gt 0.01 -or [double]$frame.postPlayer.velocity.y -ne 0)) { throw 'Warmup failed to stabilize on real ground.' }
        if ($tick -eq 27 -and $Case.EndsWith('-release-press', [StringComparison]::Ordinal)) {
            Motion-Boolean (Motion-Field $frame.preJump 'releaseJump') $true 'native airborne release edge'
            if ([double]$frame.preJump.bottomY -ge 7999.9) { throw 'Repress must occur while naturally airborne.' }
        }
    }
}

if ($FunctionsOnly) {
    if ($Run) { throw '-FunctionsOnly cannot execute native processes.' }
    return
}
Write-Output 'Native motion plan: classic; 20 released warmup + 160 measured frames per case; fixed naked/Cloud-only equipment.'
$motionCases | ForEach-Object { Write-Output "  $_; seed=$Seed; no Boss, no F8, no OS input" }
if (-not $Run) { Write-Output 'PLAN ONLY: no files, builds, preparation, games or desktop windows created. Use -Run after freezing the intended production build.'; return }

$batchId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $artifactPrefix ('native-motion-' + $batchId) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Motion output must be a named project artifacts subdirectory.' }
Assert-NoMotionReparse $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Motion output already exists; preserve evidence and choose a fresh directory.' }
$inputPaths = [ordered]@{
    InputPluginSha256 = 'src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll'
    InputCoreSha256 = 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'
    ProbeSourceSha256 = 'tools\GameProbe.cs'; ProbePatcherSourceSha256 = 'tools\GameProbePatcher.cs'
}
$hashes = [ordered]@{}
foreach ($key in $inputPaths.Keys) {
    $inputPath = Join-Path $projectRoot $inputPaths[$key]
    Assert-NoMotionReparse $inputPath
    $hashes[$key] = (Get-FileHash -LiteralPath $inputPath -Algorithm SHA256).Hash
}
$oracleExe = Join-Path $projectRoot 'tests\Chaite.Tests\bin\Release\net48\Chaite.Tests.exe'
$oracleCore = Join-Path (Split-Path -Parent $oracleExe) 'Chaite.Core.dll'
$oracleHashes = [ordered]@{}
foreach ($oraclePath in @($oracleExe, $oracleCore)) {
    Assert-NoMotionReparse $oraclePath
    $oracleHashes[$oraclePath] = (Get-FileHash -LiteralPath $oraclePath -Algorithm SHA256).Hash
}
if ($oracleHashes[$oracleCore] -cne $hashes.InputCoreSha256) { throw 'The x86 model oracle must reference the same frozen Core as the native fixture.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
@{ Schema = 'chaite-native-motion-plan/v1'; Seed = $Seed; Cases = $motionCases; Provenance = $hashes; OracleProvenance = $oracleHashes; CreatedUtc = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'plan.json') -Encoding UTF8
$records = [Collections.Generic.List[object]]::new()
foreach ($case in $motionCases) {
    $runName = 'game-probe-motion-' + $batchId + '-' + $case
    $runDirectory = Join-Path $artifactPrefix $runName
    $hostDirectory = Join-Path $OutputDirectory ($case + '-desktop')
    $record = [ordered]@{ Case = $case; Seed = $Seed; ValidMotion = $false; NativeEvidenceValid = $false; ModelCompared = $false; ModelExitCode = $null; Failure = $null; RunDirectory = $runDirectory; HostDirectory = $hostDirectory; Result = $null; ResultSha256 = $null; TraceSha256 = $null; DesktopSafe = $false }
    try {
        foreach ($key in $inputPaths.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $inputPaths[$key]) -Algorithm SHA256).Hash -cne $hashes[$key]) { throw 'Frozen motion build/probe changed; no further case may launch.' }
        }
        & (Join-Path $PSScriptRoot 'prepare-game-probe.ps1') -GameDirectory $GameDirectory -RunName $runName -Headless |
            Tee-Object -FilePath (Join-Path $OutputDirectory ($case + '-prepare.log')) | Write-Output
        $manifest = Get-Content -LiteralPath (Join-Path $runDirectory 'probe-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($key in $hashes.Keys) { if ($manifest.$key -cne $hashes[$key]) { throw 'Prepared motion case differs from frozen provenance.' } }
        $arguments = @('-scenario', 'motion-jump', '-motioncase', $case, '-difficulty', 'classic', '-seed', [string]$Seed, '-maxticks', '600', '-wallseconds', '90', '-skipbeam')
        & (Join-Path $PSScriptRoot 'start-isolated-test.ps1') -TargetExe (Join-Path $runDirectory 'Terraria.exe') -TargetArguments $arguments -OutputDirectory $hostDirectory |
            Tee-Object -FilePath (Join-Path $OutputDirectory ($case + '-launch.log')) | Write-Output
        $hostExit = Get-Content -LiteralPath (Join-Path $hostDirectory 'desktop-exit.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $hostLog = Get-Content -LiteralPath (Join-Path $hostDirectory 'desktop-host.log') -Raw -Encoding UTF8
        $record.DesktopSafe = $hostLog -match 'Input desktop unchanged; no test process took foreground;' -and $hostLog -notmatch '\bFAIL\b|TIMEOUT:'
        if (-not $record.DesktopSafe -or $hostExit.Schema -cne 'chaite-desktop-exit/v1' -or $hostExit.HostExitCode -ne 0 -or
            [regex]::Matches($hostLog, 'Child exit=0;').Count -ne 1) { throw 'Incomplete motion desktop/result exit chain.' }
        foreach ($file in $manifest.Files) {
            $path = Join-Path $runDirectory $file.Path
            Assert-NoMotionReparse $path
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $file.Sha256) { throw "Manifest file changed during motion test: $($file.Path)" }
        }
        $resultPath = Join-Path $runDirectory 'result.json'
        $tracePath = Join-Path $runDirectory 'motion-frames.jsonl'
        Assert-NoMotionReparse $resultPath
        Assert-NoMotionReparse $tracePath
        if ((Get-Item -LiteralPath $resultPath).Length -gt 128KB -or (Get-Item -LiteralPath $tracePath).Length -gt 4MB) { throw 'Unexpectedly large motion evidence.' }
        $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $record.Result = $result
        $frames = @(Get-Content -LiteralPath $tracePath -Encoding UTF8 | ForEach-Object { if ([string]::IsNullOrWhiteSpace($_)) { throw 'Blank line in motion trace.' }; $_ | ConvertFrom-Json })
        Assert-MotionEvidence $result $frames $case $Seed
        $record.ResultSha256 = (Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).Hash
        $record.TraceSha256 = (Get-FileHash -LiteralPath $tracePath -Algorithm SHA256).Hash
        $record.NativeEvidenceValid = $true
        foreach ($oraclePath in $oracleHashes.Keys) {
            if ((Get-FileHash -LiteralPath $oraclePath -Algorithm SHA256).Hash -cne $oracleHashes[$oraclePath]) { throw 'The frozen x86 model oracle changed during the motion batch.' }
        }
        $oraclePreviousErrorPreference = $ErrorActionPreference
        try {
            # Windows PowerShell wraps native STDERR in ErrorRecord. With the
            # outer Stop preference, a useful oracle mismatch previously threw
            # before Tee persisted it or the true process exit was captured.
            # This relaxation is LOCAL to this already-pinned read-only oracle.
            $ErrorActionPreference = 'Continue'
            & $oracleExe --native-motion-trace $tracePath 2>&1 |
                Tee-Object -FilePath (Join-Path $OutputDirectory ($case + '-model-comparison.log')) | Write-Output
            $record.ModelExitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $oraclePreviousErrorPreference }
        if ($record.ModelExitCode -ne 0) { throw 'Native evidence is preserved, but Core jump-model comparison failed.' }
        $record.ModelCompared = $true
        $record.ValidMotion = $true
    } catch { $record.Failure = $_.Exception.Message }
    finally {
        $records.Add([pscustomobject]$record)
        @{ Schema = 'chaite-native-motion-summary/v1'; Planned = 6; Attempted = $records.Count; Pending = 6 - $records.Count; Valid = @($records | Where-Object { $_.ValidMotion }).Count; Cases = $records.ToArray(); Provenance = $hashes; OracleProvenance = $oracleHashes; Scope = 'Native motion trajectories and frozen Core oracle only, not Boss wins, supported loadouts, or latency acceptance.' } |
            ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
        Write-Output "Motion $case valid=$($record.ValidMotion) failure=$($record.Failure)"
    }
    if (-not $record.ValidMotion) { throw 'Motion suite stopped at first invalid case. Evidence retained; no retries or silent skips.' }
}
$byCase = @{}
foreach ($record in $records) { $byCase[$record.Case] = $record.Result }
foreach ($prefix in @('no-cloud-', 'cloud-')) {
    if ($byCase[$prefix + 'hold'].maximumRisePixels -le $byCase[$prefix + 'tap'].maximumRisePixels) { throw 'Native hold did not exceed one-frame tap height; inspect complete retained traces.' }
}
if ($byCase['cloud-release-press'].maximumRisePixels -le $byCase['no-cloud-release-press'].maximumRisePixels) { throw 'Cloud repress did not exceed no-Cloud repress height; inspect complete retained traces.' }
Write-Output "PASS all six native motion trajectories and isolation chains. Evidence: $OutputDirectory"
Write-Output 'The same frozen x86 Core oracle agreed with every paired preJump/postJump and postPlayer sample; scope is only these six declared trajectories.'

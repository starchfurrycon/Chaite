param(
    [switch]$Run,
    [switch]$FunctionsOnly,
    [string]$OutputDirectory,
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria',
    [ValidateRange(0, 2147483647)][int]$Seed = 20260910
)

# Default: read-only plan. -Run explicitly executes eight serial isolated
# 600-frame native tests, never a production build, installation or user save.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
$flightCases = @(
    'demon-exhaust-release', 'demon-lightning-exhaust-release',
    'demon-early-repress', 'demon-lightning-early-repress',
    'demon-held-landing', 'demon-lightning-held-landing',
    'demon-cloud-repress', 'demon-lightning-cloud-repress',
    'demon-feather-neutral', 'demon-feather-up', 'demon-feather-down'
)

# Import pure helpers only, not the other runner or its parameter scope. In
# particular, dot-sourcing the whole runner could overwrite this -Run switch.
$flightTokens = $null
$flightParseErrors = $null
$motionAst = [Management.Automation.Language.Parser]::ParseFile((Join-Path $PSScriptRoot 'test-native-motion.ps1'), [ref]$flightTokens, [ref]$flightParseErrors)
if ($flightParseErrors.Count -ne 0) { throw 'Cannot import motion evidence helpers from invalid source.' }
foreach ($name in @('Assert-NoMotionReparse', 'Motion-Field', 'Motion-Integer', 'Motion-Boolean', 'Motion-Number')) {
    $definitions = @($motionAst.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $name }, $true))
    if ($definitions.Count -ne 1) { throw "Ambiguous shared motion helper: $name" }
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}

function Flight-JumpRequested([string]$Case, [int]$Tick) {
    if ($Tick -le 20) { return $false }
    if ($Case.EndsWith('-held-landing', [StringComparison]::Ordinal)) { return $Tick -le 580 }
    if ($Tick -gt 220) { return $false }
    if ($Case.EndsWith('-early-repress', [StringComparison]::Ordinal)) { return $Tick -le 45 -or $Tick -ge 66 }
    if ($Case.Contains('-cloud-')) { return $Tick -ne 26 }
    return $true
}
function Flight-UpRequested([string]$Case, [int]$Tick) {
    return $Case -ceq 'demon-feather-up' -and $Tick -ge 230 -and $Tick -le 330
}
function Flight-DownRequested([string]$Case, [int]$Tick) {
    return $Case -ceq 'demon-feather-down' -and $Tick -ge 230 -and $Tick -le 270
}
function Flight-Positive($Value, [string]$Name) {
    if (($Value -isnot [int] -and $Value -isnot [long]) -or $Value -lt 1 -or $Value -gt 600) { throw "Invalid positive flight coverage: $Name" }
}
function Assert-FlightEvidence($Result, [object[]]$Frames, [string]$Case, [int]$ExpectedSeed) {
    if ($Case -cnotin $flightCases) { throw 'Unreviewed flight evidence case.' }
    $lightning = $Case.Contains('-lightning-')
    $cloud = $Case.Contains('-cloud-')
    $feather = $Case.Contains('-feather-')
    $profile = 'demon' + $(if ($lightning) { '-lightning' } else { '' }) + $(if ($cloud) { '-cloud' } else { '' }) + $(if ($feather) { '-featherfall' } else { '' })
    if ((Motion-Field $Result 'schema') -cne 'chaite-native-flight-result/v1' -or (Motion-Field $Result 'scenario') -cne 'motion-flight' -or
        (Motion-Field $Result 'flightCase') -cne $Case -or (Motion-Field $Result 'flightProfile') -cne $profile -or
        (Motion-Field $Result 'status') -cne 'complete' -or (Motion-Field $Result 'difficulty') -cne 'classic' -or
        (Motion-Field $Result 'frameFile') -cne 'flight-frames.jsonl') { throw 'Flight result identity/status mismatch.' }
    Motion-Boolean (Motion-Field $Result 'validFlight') $true 'validFlight'
    Motion-Integer (Motion-Field $Result 'processExitCode') 0 'flight exit'
    Motion-Integer (Motion-Field $Result 'seed') $ExpectedSeed 'flight seed'
    Motion-Integer (Motion-Field $Result 'difficultyCode') 0 'difficultyCode'
    foreach ($key in @('ticks', 'nativeFrames', 'recordedFrames', 'totalFrames')) { Motion-Integer (Motion-Field $Result $key) 600 $key }
    Motion-Integer (Motion-Field $Result 'warmupFrames') 20 'warmupFrames'
    Motion-Boolean (Motion-Field $Result 'nativeDifficultyVerified') $true 'nativeDifficultyVerified'
    $native = Motion-Field $Result 'nativeDifficulty'
    foreach ($key in @('gameMode', 'worldFileGameMode')) { Motion-Integer (Motion-Field $native $key) 0 $key }
    Motion-Integer (Motion-Field $native 'difficulty') 1 'native difficulty'
    Motion-Integer (Motion-Field $native 'worldFileSeed') $ExpectedSeed 'worldFileSeed'
    foreach ($key in @('expertMode', 'masterMode', 'hardMode', 'forTheWorthy')) { Motion-Boolean (Motion-Field $native $key) $false $key }
    $rng = Motion-Field $Result 'nativeRandom'
    foreach ($key in @('seed', 'unpausedUpdateSeedInitial')) { Motion-Integer (Motion-Field $rng $key) $ExpectedSeed $key }
    Motion-Integer (Motion-Field $rng 'unpausedUpdateSeedAdvances') 600 'RNG advances'
    Motion-Integer (Motion-Field $rng 'referenceChecks') 1200 'RNG reference checks'
    foreach ($key in @('installedAfterSetup', 'actualAndNativeNamedColdStateVerified')) { Motion-Boolean (Motion-Field $rng $key) $true $key }
    Motion-Boolean (Motion-Field $rng 'actualStreamConsumedForFingerprint') $false 'fingerprint consumption'
    $fingerprint = @(Motion-Field $rng 'independentTwinFingerprint')
    if ($fingerprint.Count -ne 8) { throw 'Flight RNG fingerprint length mismatch.' }
    foreach ($sample in $fingerprint) { if (($sample -isnot [int] -and $sample -isnot [long]) -or $sample -lt 0 -or $sample -gt 2147483647) { throw 'Invalid flight RNG fingerprint sample.' } }
    [decimal]$lcg = $ExpectedSeed
    for ($index = 0; $index -lt 600; $index++) { $lcg = ($lcg * [decimal]25214903917 + [decimal]11) % [decimal]281474976710656 }
    Motion-Integer (Motion-Field $rng 'unpausedUpdateSeedFinal') ([long]$lcg) 'independent final flight RNG'
    $arena = Motion-Field $Result 'arena'
    Motion-Integer (Motion-Field $arena 'nativeSceneMetricRefreshes') 600 'scene refreshes'
    Motion-Integer (Motion-Field $arena 'groundTop') 500 'ground top'
    if ((Motion-Field $arena 'groundTile') -cne 'GrayBrick' -or @(Motion-Field $arena 'platformRows').Count -ne 0) { throw 'Flight terrain profile mismatch.' }
    $equipment = Motion-Field $Result 'equipment'
    if ((Motion-Field $equipment 'flightProfile') -cne $profile) { throw 'Flight equipment profile mismatch.' }
    Motion-Integer (Motion-Field $equipment 'wingItemType') 492 'Demon item ID'
    Motion-Integer (Motion-Field $equipment 'bootsItemType') $(if ($lightning) { 898 } else { 0 }) 'Lightning item ID'
    Motion-Integer (Motion-Field $equipment 'cloudItemType') $(if ($cloud) { 53 } else { 0 }) 'Cloud item ID'
    foreach ($key in @('wingPrefix', 'bootsPrefix', 'cloudPrefix')) { Motion-Integer (Motion-Field $equipment $key) 0 $key }
    Motion-Boolean (Motion-Field $equipment 'lightningEquipped') $lightning 'Lightning equipped'
    Motion-Boolean (Motion-Field $equipment 'cloudEquipped') $cloud 'Cloud equipped'
    Motion-Boolean (Motion-Field $equipment 'featherfallActive') $feather 'Featherfall active'
    Motion-Integer (Motion-Field $equipment 'featherfallBuffType') $(if ($feather) { 8 } else { 0 }) 'Featherfall buff ID'
    Motion-Integer (Motion-Field $equipment 'featherfallSourceItemType') $(if ($feather) { 295 } else { 0 }) 'Featherfall potion ID'
    Motion-Boolean (Motion-Field $equipment 'featherfallSetupViaNativeAddBuff') $feather 'native featherfall setup'
    foreach ($key in @('noWeaponsAmmoConsumablesOrMount', 'noDirectJumpStateOverrides', 'noDirectFlightStateOverrides')) { Motion-Boolean (Motion-Field $equipment $key) $true $key }
    $coverage = Motion-Field $Result 'coverage'
    foreach ($key in @('sawAirborne', 'returnedGround')) { Motion-Boolean (Motion-Field $coverage $key) $true $key }
    Motion-Boolean (Motion-Field $coverage 'cloudConsumed') $cloud 'cloud consumption'
    Motion-Integer (Motion-Field $coverage 'wingMovementCalls') $(if ($lightning) { 142 } else { 100 }) 'total wing calls'
    Motion-Integer (Motion-Field $coverage 'maximumWingTime') $(if ($lightning) { 142 } else { 100 }) 'maximum wing resource'
    Motion-Integer (Motion-Field $coverage 'rocketConversions') $(if ($lightning) { 1 } else { 0 }) 'rocket conversion count'
    foreach ($key in @('firstExhaustionTick', 'emptyWingHeldDescentFrames', 'releasedGroundFullWingFrames')) { Flight-Positive (Motion-Field $coverage $key) $key }
    if ($lightning) { Flight-Positive (Motion-Field $coverage 'firstRocketConversionTick') 'first rocket conversion' }
    else { Motion-Integer (Motion-Field $coverage 'firstRocketConversionTick') -1 'no rocket conversion' }
    if ($Case.EndsWith('-held-landing', [StringComparison]::Ordinal)) { Flight-Positive (Motion-Field $coverage 'heldGroundEmptyWingFrames') 'held unrefilled landing' }
    else { Flight-Positive (Motion-Field $coverage 'releasedAirborneFrames') 'released airborne tail' }
    if ($Case.EndsWith('-early-repress', [StringComparison]::Ordinal)) { Flight-Positive (Motion-Field $coverage 'poweredAfterRepressFrames') 'repress resumes flight' }
    foreach ($key in @('featherPoweredFrames', 'featherNeutralFrames', 'featherUpFrames', 'featherDownBypassFrames')) {
        $value = Motion-Field $coverage $key
        if (($value -isnot [int] -and $value -isnot [long]) -or $value -lt 0 -or $value -gt 600) { throw "Invalid feather coverage: $key" }
    }
    if ($feather) {
        Flight-Positive (Motion-Field $coverage 'featherPoweredFrames') 'powered wing precedes featherfall'
        Flight-Positive (Motion-Field $coverage 'featherNeutralFrames') 'neutral featherfall'
        if ($Case -ceq 'demon-feather-up') { Flight-Positive (Motion-Field $coverage 'featherUpFrames') 'Up featherfall' }
        elseif ((Motion-Field $coverage 'featherUpFrames') -ne 0) { throw 'Unexpected feather Up coverage.' }
        if ($Case -ceq 'demon-feather-down') { Flight-Positive (Motion-Field $coverage 'featherDownBypassFrames') 'Down bypass' }
        elseif ((Motion-Field $coverage 'featherDownBypassFrames') -ne 0) { throw 'Unexpected feather Down coverage.' }
    } elseif ((Motion-Field $coverage 'featherPoweredFrames') -ne 0 -or (Motion-Field $coverage 'featherNeutralFrames') -ne 0 -or
        (Motion-Field $coverage 'featherUpFrames') -ne 0 -or (Motion-Field $coverage 'featherDownBypassFrames') -ne 0) {
        throw 'Non-feather profile reported feather coverage.'
    }
    if ($Frames.Count -ne 600) { throw 'Flight trace must retain all 600 warmup/measured frames.' }
    $observedWingCalls = 0
    $observedCloudConsumption = 0
    # Cache field lists/property bags and validate inline in this hot loop.
    # Do not cast before checking the JSON value's type: missing/null, strings,
    # booleans and fractional numbers must never silently become 0/false/int.
    $frameCounters = @('tick', 'nativeFrameBefore', 'nativeFrameAfter', 'playerUpdateCalls', 'jumpMovementCalls')
    $snapshotCounters = @('tick', 'gameUpdateCount', 'nativeFramesCompleted', 'life')
    $integerFields = @('jump', 'jumpHeight', 'wings', 'wingsLogic', 'wingTimeMax', 'rocketBoots', 'rocketTime', 'rocketTimeMax', 'rocketDelay', 'rocketDelay2')
    $numberFields = @('jumpSpeed', 'jumpSpeedBoost', 'wingTime', 'gravity', 'maxFallSpeed', 'gravDir', 'bottomY', 'wingAccRunSpeed', 'wingRunAccelerationMult')
    $booleanFields = @('releaseJump', 'canJumpAgain_Cloud', 'hasJumpOption_Cloud', 'isPerformingJump_Cloud', 'canRocket', 'rocketRelease', 'autoJump', 'justJumped', 'noFallDmg')
    $falseFields = @('sliding', 'wet', 'honeyWet', 'lavaWet', 'shimmerWet', 'pulley', 'frozen', 'webbed', 'stoned', 'dead')
    $falseControls = @('left', 'right', 'useItem', 'useTile', 'hook', 'mount', 'dash')
    $vectors = @('position', 'velocity')
    $axes = @('x', 'y')
    $baseMoments = @('prePlayer', 'preJump', 'postJump', 'postPlayer')
    $wingMoments = @('prePlayer', 'preJump', 'postJump', 'postPlayer', 'preWing', 'postWing')
    $expectedRocketBoots = if ($lightning) { 2 } else { 0 }
    for ($index = 0; $index -lt $Frames.Count; $index++) {
        $frame = $Frames[$index]
        $tick = $index + 1
        if ($null -eq $frame) { throw 'Missing flight frame.' }
        $frameProperties = $frame.PSObject.Properties
        $schemaProperty = $frameProperties['schema']
        $sessionProperty = $frameProperties['productionSessionState']
        if ($null -eq $schemaProperty -or $schemaProperty.Value -isnot [string] -or $schemaProperty.Value -cne 'chaite-native-flight-frame/v1' -or
            $null -eq $sessionProperty -or $sessionProperty.Value -isnot [string] -or $sessionProperty.Value -cne 'Idle') { throw 'Flight frame schema or production takeover mismatch.' }
        $frameExpected = @($tick, $index, $tick, 1, 1)
        for ($counterIndex = 0; $counterIndex -lt $frameCounters.Count; $counterIndex++) {
            $key = $frameCounters[$counterIndex]
            $property = $frameProperties[$key]
            if ($null -eq $property) { throw "Missing flight frame counter: $key" }
            $value = $property.Value
            if (($value -isnot [int] -and $value -isnot [long]) -or $value -ne $frameExpected[$counterIndex]) { throw "Invalid flight frame counter: $key" }
        }
        $property = $frameProperties['wingMovementCalls']
        if ($null -eq $property) { throw 'Missing per-frame WingMovement count.' }
        $wingCalls = $property.Value
        if (($wingCalls -isnot [int] -and $wingCalls -isnot [long]) -or $wingCalls -lt 0 -or $wingCalls -gt 1) { throw 'Invalid per-frame WingMovement count.' }
        $observedWingCalls += $wingCalls
        $preWingProperty = $frameProperties['preWing']
        $postWingProperty = $frameProperties['postWing']
        if ($null -eq $preWingProperty -or $null -eq $postWingProperty) { throw 'Missing optional WingMovement observation fields.' }
        $preWing = $preWingProperty.Value
        $postWing = $postWingProperty.Value
        if (($wingCalls -eq 0 -and ($null -ne $preWing -or $null -ne $postWing)) -or ($wingCalls -eq 1 -and ($null -eq $preWing -or $null -eq $postWing))) { throw 'Optional WingMovement observations contradict native invocation count.' }
        $requested = Flight-JumpRequested $Case $tick
        $requestedUp = Flight-UpRequested $Case $tick
        $requestedDown = Flight-DownRequested $Case $tick
        $property = $frameProperties['requestedJump']
        if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $requested) { throw 'Invalid declared flight input.' }
        foreach ($declaration in @(@('requestedUp', $requestedUp), @('requestedDown', $requestedDown))) {
            $property = $frameProperties[$declaration[0]]
            if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $declaration[1]) { throw "Invalid declared flight input: $($declaration[0])" }
        }
        if ($wingCalls -eq 1 -and -not $requested) { throw 'WingMovement cannot be invoked by this profile while jump is released.' }
        $moments = if ($wingCalls -eq 1) { $wingMoments } else { $baseMoments }
        $snapshotExpected = @($tick, ($tick + 1), $index, 400)
        foreach ($moment in $moments) {
            $property = $frameProperties[$moment]
            if ($null -eq $property -or $null -eq $property.Value) { throw "Missing flight snapshot: $moment" }
            $snapshot = $property.Value
            $snapshotProperties = $snapshot.PSObject.Properties
            for ($counterIndex = 0; $counterIndex -lt $snapshotCounters.Count; $counterIndex++) {
                $key = $snapshotCounters[$counterIndex]
                $property = $snapshotProperties[$key]
                if ($null -eq $property) { throw "Missing flight snapshot counter: $moment.$key" }
                $value = $property.Value
                if (($value -isnot [int] -and $value -isnot [long]) -or $value -ne $snapshotExpected[$counterIndex]) { throw "Invalid flight snapshot counter: $moment.$key" }
            }
            foreach ($key in $integerFields) {
                $property = $snapshotProperties[$key]
                if ($null -eq $property) { throw "Missing flight integer: $moment.$key" }
                $value = $property.Value
                if (($value -isnot [int] -and $value -isnot [long]) -or $value -lt 0 -or $value -gt 1000) { throw "Invalid flight integer $moment.$key" }
            }
            foreach ($key in $numberFields) {
                $property = $snapshotProperties[$key]
                if ($null -eq $property) { throw "Missing flight number: $moment.$key" }
                $value = $property.Value
                if (($value -isnot [int] -and $value -isnot [long] -and $value -isnot [double] -and $value -isnot [decimal]) -or
                    [double]::IsNaN([double]$value) -or [double]::IsInfinity([double]$value)) { throw "Invalid finite flight number: $moment.$key" }
            }
            foreach ($vector in $vectors) {
                $property = $snapshotProperties[$vector]
                if ($null -eq $property -or $null -eq $property.Value) { throw "Missing flight vector: $moment.$vector" }
                $vectorProperties = $property.Value.PSObject.Properties
                foreach ($axis in $axes) {
                    $property = $vectorProperties[$axis]
                    if ($null -eq $property) { throw "Missing flight axis: $moment.$vector.$axis" }
                    $value = $property.Value
                    if (($value -isnot [int] -and $value -isnot [long] -and $value -isnot [double] -and $value -isnot [decimal]) -or
                        [double]::IsNaN([double]$value) -or [double]::IsInfinity([double]$value)) { throw "Invalid finite flight axis: $moment.$vector.$axis" }
                }
            }
            foreach ($key in $booleanFields) {
                $property = $snapshotProperties[$key]
                if ($null -eq $property -or $property.Value -isnot [bool]) { throw "Invalid flight Boolean: $moment.$key" }
            }
            foreach ($key in $falseFields) {
                $property = $snapshotProperties[$key]
                if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $false) { throw "Invalid flight unsupported state: $moment.$key" }
            }
            $property = $snapshotProperties['slowFall']
            $expectedSlowFall = $feather -and -not ($moment -ceq 'prePlayer' -and $tick -eq 1)
            if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $expectedSlowFall) { throw "Invalid featherfall state: $moment.slowFall" }
            $property = $snapshotProperties['controls']
            if ($null -eq $property -or $null -eq $property.Value) { throw "Missing flight controls: $moment" }
            $controlProperties = $property.Value.PSObject.Properties
            $property = $controlProperties['jump']
            if ($null -eq $property -or $property.Value -isnot [bool]) { throw 'Invalid flight jump control Boolean.' }
            foreach ($key in $falseControls) {
                $property = $controlProperties[$key]
                if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $false) { throw "Invalid unrequested flight control: $moment.$key" }
            }
            foreach ($direction in @(@('up', $requestedUp), @('down', $requestedDown))) {
                $property = $controlProperties[$direction[0]]
                if ($null -eq $property -or $property.Value -isnot [bool] -or $property.Value -ne $direction[1]) { throw "Invalid flight direction control: $moment.$($direction[0])" }
            }
        }
        # Presence and types of these fields were checked for every snapshot above.
        if ($frame.preJump.controls.jump -ne $requested -or $frame.preJump.wingsLogic -ne 1 -or $frame.preJump.wingTimeMax -ne 100 -or
            $frame.preJump.rocketBoots -ne $expectedRocketBoots -or $frame.preJump.hasJumpOption_Cloud -ne $cloud) { throw 'Actual flight input or equipment profile mismatch.' }
        if ($frame.preJump.canJumpAgain_Cloud -and -not $frame.postJump.canJumpAgain_Cloud) { $observedCloudConsumption++ }
        if ($tick -le 20 -and ([Math]::Abs([double]$frame.postPlayer.bottomY - 8000) -gt .01 -or [double]$frame.postPlayer.velocity.y -ne 0)) { throw 'Flight warmup did not stabilize on real ground.' }
    }
    Motion-Integer $observedWingCalls (Motion-Field $coverage 'wingMovementCalls') 'trace/result wing calls'
    Motion-Integer $observedCloudConsumption $(if ($cloud) { 1 } else { 0 }) 'trace cloud consumption count'
}

if ($FunctionsOnly) { if ($Run) { throw '-FunctionsOnly cannot launch flight tests.' }; return }
Write-Output "Flight plan: $($flightCases.Count) cases x 600 native frames; classic, 20 released warmup frames; naked Demon Wings +/- Lightning Boots / Cloud / native Featherfall buff."
$flightCases | ForEach-Object { Write-Output "  $_; seed=$Seed; no Boss/F8/OS input; resources are never directly assigned" }
if (-not $Run) { Write-Output 'PLAN ONLY: no files, builds, preparation, games or desktop windows created. Use -Run only after freezing production and the x86 oracle.'; return }
$batchId = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $artifactPrefix ('native-flight-' + $batchId) }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not $OutputDirectory.StartsWith($artifactPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Flight output must be a named project artifacts subdirectory.' }
Assert-NoMotionReparse $OutputDirectory
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Flight output exists; preserve evidence and choose a fresh directory.' }
$inputPaths = [ordered]@{
    InputPluginSha256 = 'src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll'
    InputCoreSha256 = 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'
    ProbeSourceSha256 = 'tools\GameProbe.cs'; ProbePatcherSourceSha256 = 'tools\GameProbePatcher.cs'
}
$hashes = [ordered]@{}
foreach ($key in $inputPaths.Keys) { $path = Join-Path $projectRoot $inputPaths[$key]; Assert-NoMotionReparse $path; $hashes[$key] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
$oracleExe = Join-Path $projectRoot 'tests\Chaite.Tests\bin\Release\net48\Chaite.Tests.exe'
$oracleCore = Join-Path (Split-Path -Parent $oracleExe) 'Chaite.Core.dll'
$oracleHashes = [ordered]@{}
foreach ($path in @($oracleExe, $oracleCore)) { Assert-NoMotionReparse $path; $oracleHashes[$path] = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
if ($oracleHashes[$oracleCore] -cne $hashes.InputCoreSha256) { throw 'The x86 flight oracle must use the same frozen Core as the native fixture.' }
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
@{ Schema = 'chaite-native-flight-plan/v1'; Seed = $Seed; Cases = $flightCases; Provenance = $hashes; OracleProvenance = $oracleHashes; CreatedUtc = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'plan.json') -Encoding UTF8
$records = [Collections.Generic.List[object]]::new()
foreach ($case in $flightCases) {
    $runName = 'game-probe-flight-' + $batchId + '-' + $case
    $runDirectory = Join-Path $artifactPrefix $runName
    $hostDirectory = Join-Path $OutputDirectory ($case + '-desktop')
    $record = [ordered]@{ Case = $case; Seed = $Seed; ValidFlight = $false; NativeEvidenceValid = $false; ModelCompared = $false; ModelExitCode = $null; Failure = $null; RunDirectory = $runDirectory; HostDirectory = $hostDirectory; Result = $null; ResultSha256 = $null; TraceSha256 = $null; DesktopSafe = $false }
    try {
        foreach ($key in $inputPaths.Keys) { if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $inputPaths[$key]) -Algorithm SHA256).Hash -cne $hashes[$key]) { throw 'Frozen flight build/probe changed; no further case may launch.' } }
        & (Join-Path $PSScriptRoot 'prepare-game-probe.ps1') -GameDirectory $GameDirectory -RunName $runName -Headless |
            Tee-Object -FilePath (Join-Path $OutputDirectory ($case + '-prepare.log')) | Write-Output
        $manifest = Get-Content -LiteralPath (Join-Path $runDirectory 'probe-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($key in $hashes.Keys) { if ($manifest.$key -cne $hashes[$key]) { throw 'Prepared flight case differs from frozen provenance.' } }
        $arguments = @('-scenario', 'motion-flight', '-flightcase', $case, '-difficulty', 'classic', '-seed', [string]$Seed, '-maxticks', '600', '-wallseconds', '90', '-skipbeam')
        & (Join-Path $PSScriptRoot 'start-isolated-test.ps1') -TargetExe (Join-Path $runDirectory 'Terraria.exe') -TargetArguments $arguments -OutputDirectory $hostDirectory |
            Tee-Object -FilePath (Join-Path $OutputDirectory ($case + '-launch.log')) | Write-Output
        $hostExit = Get-Content -LiteralPath (Join-Path $hostDirectory 'desktop-exit.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        $hostLog = Get-Content -LiteralPath (Join-Path $hostDirectory 'desktop-host.log') -Raw -Encoding UTF8
        $record.DesktopSafe = $hostLog -match 'Input desktop unchanged; no test process took foreground;' -and $hostLog -notmatch '\bFAIL\b|TIMEOUT:'
        if (-not $record.DesktopSafe -or $hostExit.Schema -cne 'chaite-desktop-exit/v1' -or $hostExit.HostExitCode -ne 0 -or [regex]::Matches($hostLog, 'Child exit=0;').Count -ne 1) { throw 'Incomplete flight desktop/process exit chain.' }
        foreach ($file in $manifest.Files) { $path = Join-Path $runDirectory $file.Path; Assert-NoMotionReparse $path; if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $file.Sha256) { throw "Manifest file changed during flight test: $($file.Path)" } }
        $resultPath = Join-Path $runDirectory 'result.json'
        $tracePath = Join-Path $runDirectory 'flight-frames.jsonl'
        Assert-NoMotionReparse $resultPath
        Assert-NoMotionReparse $tracePath
        if ((Get-Item -LiteralPath $resultPath).Length -gt 128KB -or (Get-Item -LiteralPath $tracePath).Length -gt 12MB) { throw 'Unexpectedly large flight evidence.' }
        $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $record.Result = $result
        $frames = @(Get-Content -LiteralPath $tracePath -Encoding UTF8 | ForEach-Object { if ([string]::IsNullOrWhiteSpace($_)) { throw 'Blank flight trace line.' }; $_ | ConvertFrom-Json })
        Assert-FlightEvidence $result $frames $case $Seed
        $record.ResultSha256 = (Get-FileHash -LiteralPath $resultPath -Algorithm SHA256).Hash
        $record.TraceSha256 = (Get-FileHash -LiteralPath $tracePath -Algorithm SHA256).Hash
        $record.NativeEvidenceValid = $true
        foreach ($path in $oracleHashes.Keys) { if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -cne $oracleHashes[$path]) { throw 'The frozen x86 flight oracle changed during the batch.' } }
        $flightPreviousErrorPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & $oracleExe --native-flight-trace $tracePath 2>&1 |
                Tee-Object -FilePath (Join-Path $OutputDirectory ($case + '-model-comparison.log')) | Write-Output
            $record.ModelExitCode = $LASTEXITCODE
        } finally { $ErrorActionPreference = $flightPreviousErrorPreference }
        if ($record.ModelExitCode -ne 0) { throw 'Native flight evidence retained, but the frozen Core flight-model comparison failed.' }
        $record.ModelCompared = $true
        $record.ValidFlight = $true
    } catch { $record.Failure = $_.Exception.Message }
    finally {
        $records.Add([pscustomobject]$record)
        @{ Schema = 'chaite-native-flight-summary/v1'; Planned = $flightCases.Count; Attempted = $records.Count; Pending = $flightCases.Count - $records.Count; Valid = @($records | Where-Object { $_.ValidFlight }).Count; Cases = $records.ToArray(); Provenance = $hashes; OracleProvenance = $oracleHashes; Scope = 'Fixed native vertical-flight traces and frozen Core oracle, not Boss success, horizontal mobility, live adapter or latency acceptance.' } |
            ConvertTo-Json -Depth 16 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
        Write-Output "Flight $case valid=$($record.ValidFlight) failure=$($record.Failure)"
    }
    if (-not $record.ValidFlight) { throw 'Flight suite stopped at first invalid case. All evidence retained; no retries or skips.' }
}
Write-Output "PASS $($flightCases.Count) native flight trajectories, frozen Core comparisons and isolation chains. Evidence: $OutputDirectory"

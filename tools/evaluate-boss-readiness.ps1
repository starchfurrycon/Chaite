param(
    [string[]]$Summary,
    [string]$Targets,
    [string]$Output,
    [ValidateRange(40, 1000000)][int]$MinimumSamples = 40,
    [ValidateRange(0.90, 1.0)][double]$MinimumLowerBound = 0.90,
    [switch]$DescribeProfiles,
    [switch]$FunctionsOnly
)

# Read-only by default: print JSON, never start Terraria, build, or alter evidence.
# -Targets requires chaite-boss-readiness-targets/v1 with completeTargetList=true,
# coverageDescription, samplingPlan={id,description,independentSeedsDeclared:true,
# fixedBeforeEvaluation:true}, and targets=[{id,boss,difficulty,variant,build,
# profileSha256,plannedSeeds:[...]}]. All planned seeds must finish; >=40 distinct
# seeds and Wilson TWO-SIDED 95% lower bound >=0.90 are necessary in EVERY cell.
# -DescribeProfiles prints exact identities for authoring a target plan, not readiness.
# Build = pluginSha256/coreSha256/probeSha256/probePatcherSha256. Profile includes
# the complete initial equipment (including weapon/ammo), arena, limits and scope;
# only the arena's measured nativeSceneMetricRefreshes counter is omitted.
# The caller must register the complete plan BEFORE sampling. Declarations/unique
# seeds cannot prove independence or that omitted evidence does not exist. This is
# a conservative summary evaluator, not a replacement for native/manifest audits.
# CLI exit codes: 0 = ready (or profile discovery), 2 = evaluated but not ready;
# invalid input throws. -FunctionsOnly exposes pure helpers without exiting.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-BrField($Object, [string]$Name, $Default = $null) {
    if ($null -eq $Object) { return $Default }
    if ($Object -is [Collections.IDictionary]) {
        if ($Object.Contains($Name)) { return $Object[$Name] }
        return $Default
    }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $Default }
    return $property.Value
}

function Test-BrTrue($Value) { return $Value -is [bool] -and $Value }
function Test-BrFalse($Value) { return $Value -is [bool] -and -not $Value }

function ConvertTo-BrCanonicalValue($Value) {
    if ($null -eq $Value) { return $null }
    if ($Value -is [Collections.IDictionary] -or $Value -is [pscustomobject]) {
        $names = if ($Value -is [Collections.IDictionary]) { @($Value.Keys | ForEach-Object { [string]$_ }) } else { @($Value.PSObject.Properties.Name) }
        [Array]::Sort($names, [StringComparer]::Ordinal)
        $ordered = [ordered]@{}
        foreach ($name in $names) {
            # Read directly: a pipeline accessor would collapse [] or [one] into
            # null/scalar and incorrectly merge distinct profile JSON shapes.
            $child = if ($Value -is [Collections.IDictionary]) { ,$Value[$name] } else { ,$Value.PSObject.Properties[$name].Value }
            $ordered[$name] = ConvertTo-BrCanonicalValue $child
        }
        return $ordered
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = @(foreach ($item in $Value) { ConvertTo-BrCanonicalValue $item })
        return ,$items
    }
    return $Value
}

function Get-BrHash($Object) {
    $json = ConvertTo-Json -InputObject (ConvertTo-BrCanonicalValue $Object) -Depth 40 -Compress
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($json))).Replace('-', '') }
    finally { $sha.Dispose() }
}

function Get-BrBuild($Record) {
    return [ordered]@{
        pluginSha256 = [string](Get-BrField $Record 'InputPluginSha256' '')
        coreSha256 = [string](Get-BrField $Record 'InputCoreSha256' '')
        probeSha256 = [string](Get-BrField $Record 'ProbeSourceSha256' '')
        probePatcherSha256 = [string](Get-BrField $Record 'ProbePatcherSourceSha256' '')
    }
}

function Assert-BrBuild($Build) {
    foreach ($name in @('pluginSha256', 'coreSha256', 'probeSha256', 'probePatcherSha256')) {
        if ([string](Get-BrField $Build $name '') -cnotmatch '^[A-F0-9]{64}$') { throw "Missing/invalid uppercase build SHA256: $name" }
    }
}

function Get-BossEvidenceIdentity($Record) {
    $result = Get-BrField $Record 'Result'
    $build = Get-BrBuild $Record
    Assert-BrBuild $build
    $equipment = Get-BrField $result 'equipment'
    $rawArena = Get-BrField $result 'arena'
    $limits = Get-BrField $result 'limits'
    if ($null -eq $equipment -or $null -eq $rawArena -or $null -eq $limits) { throw 'Missing initial equipment/arena/limits profile.' }
    foreach ($field in @('life', 'mana', 'armorAndAccessories', 'weaponType', 'ammoType', 'ammoCount', 'healingType', 'healingCount', 'grappleType', 'consumablesReplenished')) {
        if ($null -eq (Get-BrField $equipment $field)) { throw "Missing equipment profile field: $field" }
    }
    $arena = [ordered]@{}
    $names = if ($rawArena -is [Collections.IDictionary]) { @($rawArena.Keys) } else { @($rawArena.PSObject.Properties.Name) }
    foreach ($name in $names) { if ($name -cne 'nativeSceneMetricRefreshes') { $arena[$name] = Get-BrField $rawArena $name } }
    $native = Get-BrField $result 'nativeDifficulty'
    $variant = [string](Get-BrField $result 'variant' (Get-BrField $Record 'Variant' ''))
    # Old reviewed fixtures have no variant field; keep that legacy identity
    # explicit instead of silently calling it every normal/special-seed variant.
    if ([string]::IsNullOrWhiteSpace($variant)) {
        if (Test-BrFalse (Get-BrField $native 'forTheWorthy')) { $variant = 'legacy-standard-fixture' }
        else { throw 'Unreported/ambiguous variant; provide explicit variant evidence.' }
    }
    $worldSettings = [ordered]@{}
    if ($null -eq $native) { throw 'Missing observed native world settings.' }
    $nativeNames = if ($native -is [Collections.IDictionary]) { @($native.Keys) } else { @($native.PSObject.Properties.Name) }
    foreach ($name in $nativeNames) { if ($name -cne 'worldFileSeed') { $worldSettings[$name] = Get-BrField $native $name } }
    $profile = [ordered]@{
        equipment = $equipment; arena = $arena; limits = $limits; nativeWorldSettings = $worldSettings
        scope = Get-BrField $result 'scope'; randomScope = Get-BrField $result 'randomScope'
    }
    return [pscustomobject]@{
        boss = [string](Get-BrField $Record 'Scenario' ''); difficulty = [string](Get-BrField $Record 'Difficulty' '')
        variant = $variant; build = $build; profileSha256 = Get-BrHash $profile; profile = $profile
    }
}

function Get-BrIdentityKey($Identity) {
    return Get-BrHash ([ordered]@{
        boss = Get-BrField $Identity 'boss'; difficulty = Get-BrField $Identity 'difficulty'
        variant = Get-BrField $Identity 'variant'; build = Get-BrField $Identity 'build'
        profileSha256 = Get-BrField $Identity 'profileSha256'
    })
}

function Get-Wilson95LowerBound([int]$Wins, [int]$Samples) {
    if ($Samples -lt 0 -or $Wins -lt 0 -or $Wins -gt $Samples) { throw 'Invalid Wilson win/sample counts.' }
    if ($Samples -eq 0) { return 0.0 }
    $z = 1.959963984540054
    $z2 = $z * $z
    $p = [double]$Wins / $Samples
    return [Math]::Max(0.0, ($p + $z2 / (2 * $Samples) - $z * [Math]::Sqrt($p * (1 - $p) / $Samples + $z2 / (4 * $Samples * $Samples))) / (1 + $z2 / $Samples))
}

function Test-BrRecordEvidence($Record, [bool]$Invalidated) {
    $result = Get-BrField $Record 'Result'
    $classification = [string](Get-BrField $Record 'Classification' '')
    $errors = [Collections.Generic.List[string]]::new()
    if ($Invalidated) { $errors.Add('source-invalidated') }
    if ($null -eq $result -or (Get-BrField $result 'schema') -cne 'chaite-boss-result/v1') { $errors.Add('missing-result-schema') }
    if ((Get-BrField $result 'scenario') -cne (Get-BrField $Record 'Scenario') -or
        (Get-BrField $result 'difficulty') -cne (Get-BrField $Record 'Difficulty') -or
        (Get-BrField $result 'seed') -ne (Get-BrField $Record 'Seed')) { $errors.Add('identity-mismatch') }
    if ($classification -cnotin @('win', 'loss', 'timeout', 'rejected', 'harness-error')) { $errors.Add('incomplete-attempt') }
    if ((Get-BrField $result 'status') -cne $classification) { $errors.Add('status-mismatch') }
    if (-not (Test-BrTrue (Get-BrField $Record 'DesktopSafe'))) { $errors.Add('desktop-evidence-missing') }
    $expectedExit = if ($classification -ceq 'win') { 0 } elseif ($classification -ceq 'harness-error') { 10 } else { 20 }
    foreach ($exit in @((Get-BrField $Record 'HostExitCode'), (Get-BrField $Record 'ChildExitCode'), (Get-BrField $result 'processExitCode'))) {
        if ($null -eq $exit -or $exit -ne $expectedExit) { $errors.Add('exit-mismatch'); break }
    }
    if (-not (Test-BrTrue (Get-BrField $Record 'ValidBattle')) -or -not (Test-BrTrue (Get-BrField $result 'validBattle'))) { $errors.Add('not-valid-battle') }
    $native = Get-BrField $result 'nativeDifficulty'
    $mode = @('classic', 'expert', 'master').IndexOf([string](Get-BrField $Record 'Difficulty'))
    if ($mode -lt 0 -or -not (Test-BrTrue (Get-BrField $result 'nativeDifficultyVerified')) -or
        (Get-BrField $native 'gameMode') -ne $mode -or (Get-BrField $native 'worldFileGameMode') -ne $mode -or
        (Get-BrField $native 'worldFileSeed') -ne (Get-BrField $Record 'Seed')) { $errors.Add('native-settings-unverified') }
    $observed = @(Get-BrField $result 'firstObservedBosses' @())
    if ($observed.Count -eq 0 -or -not (Test-BrTrue (Get-BrField $result 'allExpectedBossesSeen'))) { $errors.Add('boss-observation-missing') }
    foreach ($boss in $observed) {
        if ((Get-BrField $boss 'npcDifficulty') -ne (Get-BrField $native 'difficulty') -or (Get-BrField $boss 'lifeMax' 0) -le 0) { $errors.Add('boss-difficulty-mismatch'); break }
    }
    if (@(Get-BrField $result 'unexpectedBossTypes' @()).Count -gt 0) { $errors.Add('unexpected-boss') }
    $random = Get-BrField $result 'battleRandom'
    $frames = Get-BrField $result 'nativeFrames' 0
    if ($frames -le 120 -or -not (Test-BrTrue (Get-BrField $random 'installedAfterSetup')) -or
        -not (Test-BrTrue (Get-BrField $random 'actualAndNativeNamedColdStateVerified')) -or
        -not (Test-BrFalse (Get-BrField $random 'actualStreamConsumedForFingerprint')) -or
        (Get-BrField $random 'seed') -ne (Get-BrField $Record 'Seed') -or
        (Get-BrField $random 'unpausedUpdateSeedInitial') -ne (Get-BrField $Record 'Seed') -or
        (Get-BrField $random 'unpausedUpdateSeedAdvances') -ne $frames -or
        (Get-BrField $random 'referenceChecks') -ne (2 * $frames) -or
        @(Get-BrField $random 'independentTwinFingerprint' @()).Count -ne 8) { $errors.Add('random-evidence-unverified') }
    $deaths = Get-BrField $result 'deaths'
    if ($null -eq $deaths -or $deaths -lt 0 -or (Get-BrField $Record 'Deaths') -ne $deaths) { $errors.Add('death-evidence-missing') }
    $reportedWin = Test-BrTrue (Get-BrField $result 'win')
    if ($reportedWin -ne ($classification -ceq 'win')) { $errors.Add('contradictory-win') }
    return [pscustomobject]@{
        ValidEvidence = $errors.Count -eq 0
        Win = $errors.Count -eq 0 -and $classification -ceq 'win' -and $reportedWin -and $deaths -eq 0
        Death = $null -ne $deaths -and $deaths -gt 0
        Timeout = $classification -cin @('timeout', 'host-timeout')
        Errors = @($errors.ToArray())
    }
}

function Measure-BossReadiness($TargetDocument, [object[]]$Summaries, [int]$MinimumSamples = 40, [double]$MinimumLowerBound = 0.90) {
    if ($MinimumSamples -lt 40 -or $MinimumLowerBound -lt 0.90 -or $MinimumLowerBound -gt 1) { throw 'Readiness policy cannot be weaker than 40 samples / 0.90 lower bound.' }
    if ((Get-BrField $TargetDocument 'schema') -cne 'chaite-boss-readiness-targets/v1' -or
        -not (Test-BrTrue (Get-BrField $TargetDocument 'completeTargetList')) -or
        [string]::IsNullOrWhiteSpace([string](Get-BrField $TargetDocument 'coverageDescription'))) { throw 'An explicit complete target list and coverage description are required.' }
    $sampling = Get-BrField $TargetDocument 'samplingPlan'
    if (-not (Test-BrTrue (Get-BrField $sampling 'independentSeedsDeclared')) -or -not (Test-BrTrue (Get-BrField $sampling 'fixedBeforeEvaluation')) -or
        [string]::IsNullOrWhiteSpace([string](Get-BrField $sampling 'id')) -or [string]::IsNullOrWhiteSpace([string](Get-BrField $sampling 'description'))) { throw 'Require an explicit independently sampled, fixed-before-evaluation seed plan.' }
    $targets = @(Get-BrField $TargetDocument 'targets' @())
    if ($targets.Count -eq 0) { throw 'The complete target list cannot be empty.' }
    $ids = @{}; $keys = @{}
    foreach ($target in $targets) {
        foreach ($field in @('id', 'boss', 'difficulty', 'variant', 'profileSha256')) {
            if ([string]::IsNullOrWhiteSpace([string](Get-BrField $target $field))) { throw "Missing target field: $field" }
        }
        if ((Get-BrField $target 'difficulty') -cnotin @('classic', 'expert', 'master') -or
            [string](Get-BrField $target 'profileSha256') -cnotmatch '^[A-F0-9]{64}$') { throw 'Invalid target difficulty/profile hash.' }
        Assert-BrBuild (Get-BrField $target 'build')
        $id = [string](Get-BrField $target 'id'); $key = Get-BrIdentityKey $target
        if ($ids.ContainsKey($id) -or $keys.ContainsKey($key)) { throw 'Duplicate target id or identical stratum.' }
        $ids[$id] = $true; $keys[$key] = $true
        $seen = @{}; $seeds = @(Get-BrField $target 'plannedSeeds' @())
        if ($seeds.Count -eq 0) { throw 'Every target needs an explicit, predeclared seed list.' }
        foreach ($seed in $seeds) {
            if (($seed -isnot [int] -and $seed -isnot [long]) -or $seed -lt 0 -or $seed -gt 2147483647 -or $seen.ContainsKey([string]$seed)) { throw 'Planned seeds must be distinct nonnegative int32 values.' }
            $seen[[string]$seed] = $true
        }
    }
    $evidence = [Collections.Generic.List[object]]::new()
    $unassignable = [Collections.Generic.List[object]]::new()
    foreach ($source in $Summaries) {
        $document = Get-BrField $source 'Document'
        if ((Get-BrField $document 'Schema') -cne 'chaite-boss-batch-summary/v1') { throw 'Unsupported input summary schema.' }
        $records = @(Get-BrField $document 'Cases' @())
        if ((Get-BrField $document 'Attempted' -1) -ne $records.Count) { throw 'Summary attempted count disagrees with its case records.' }
        foreach ($record in $records) {
            $quality = Test-BrRecordEvidence $record ([bool](Get-BrField $source 'Invalidated' $false))
            try {
                $identity = Get-BossEvidenceIdentity $record
                $seed = Get-BrField $record 'Seed'
                if (($seed -isnot [int] -and $seed -isnot [long]) -or $seed -lt 0 -or $seed -gt 2147483647) { throw 'Invalid evidence seed.' }
                $evidence.Add([pscustomobject]@{ Source = Get-BrField $source 'Source'; CaseId = Get-BrField $record 'Id'; Identity = $identity; Key = Get-BrIdentityKey $identity; Seed = $seed; Quality = $quality })
            } catch {
                # Missing profile/build evidence must never disappear into a better
                # denominator. Conservatively block the whole evaluation for review.
                $unassignable.Add([pscustomobject]@{ source = Get-BrField $source 'Source'; caseId = Get-BrField $record 'Id'; reason = $_.Exception.Message })
            }
        }
    }
    $cells = @(foreach ($target in $targets) {
        $key = Get-BrIdentityKey $target
        $matched = @($evidence | Where-Object Key -CEQ $key)
        $planned = @(Get-BrField $target 'plannedSeeds')
        $plannedLookup = @{}; foreach ($seed in $planned) { $plannedLookup[[string]$seed] = $true }
        $unplanned = @($matched | Where-Object { -not $plannedLookup.ContainsKey([string]$_.Seed) })
        $selected = @($matched | Where-Object { $plannedLookup.ContainsKey([string]$_.Seed) })
        $groups = @($selected | Group-Object Seed)
        $wins = 0; $duplicates = @(); $invalidCount = 0
        foreach ($group in $groups) {
            if ($group.Count -gt 1) { $duplicates += [long]$group.Name }
            # Do not select the best retry or count repeated seeds as independent.
            if ($group.Count -eq 1 -and $group.Group[0].Quality.Win) { $wins++ }
            $invalidCount += @($group.Group | Where-Object { -not $_.Quality.ValidEvidence }).Count
        }
        $observedLookup = @{}; foreach ($group in $groups) { $observedLookup[$group.Name] = $true }
        $missing = @($planned | Where-Object { -not $observedLookup.ContainsKey([string]$_) })
        $n = $groups.Count
        $lower = Get-Wilson95LowerBound $wins $n
        $issues = [Collections.Generic.List[string]]::new()
        if ($n -lt $MinimumSamples) { $issues.Add('insufficient-independent-seeds') }
        if ($missing.Count -gt 0) { $issues.Add('planned-seeds-unverified') }
        if ($duplicates.Count -gt 0) { $issues.Add('duplicate-seeds-require-review') }
        if ($unplanned.Count -gt 0) { $issues.Add('unplanned-seed-evidence-require-review') }
        if ($invalidCount -gt 0) { $issues.Add('invalid-evidence') }
        if ($unassignable.Count -gt 0) { $issues.Add('unassignable-evidence-require-review') }
        if ($lower -lt $MinimumLowerBound) { $issues.Add('confidence-lower-bound-below-target') }
        $ready = $issues.Count -eq 0
        $status = if ($n -eq 0) { 'unverified' } elseif ($n -lt $MinimumSamples) { 'smoke-only' } elseif ($ready) { 'ready' } else { 'not-ready' }
        [pscustomobject]@{
            id = Get-BrField $target 'id'; boss = Get-BrField $target 'boss'; difficulty = Get-BrField $target 'difficulty'
            variant = Get-BrField $target 'variant'; build = Get-BrField $target 'build'; profileSha256 = Get-BrField $target 'profileSha256'
            status = $status; ready = $ready; plannedSeeds = $planned.Count; attemptedRecords = $selected.Count
            independentSeedCount = $n; independentWins = $wins; nonWins = $n - $wins
            deathRecords = @($selected | Where-Object { $_.Quality.Death }).Count; timeoutRecords = @($selected | Where-Object { $_.Quality.Timeout }).Count
            invalidEvidenceRecords = $invalidCount; duplicateSeeds = $duplicates; unplannedRecords = $unplanned.Count; missingSeeds = $missing
            observedSuccessFraction = $(if ($n -gt 0) { [double]$wins / $n } else { $null })
            wilson95TwoSidedLowerBound = $lower; reasons = @($issues.ToArray())
        }
    })
    $unmatched = @($evidence | Where-Object { -not $keys.ContainsKey($_.Key) })
    return [pscustomobject]@{
        schema = 'chaite-boss-readiness/v1'; ready = @($cells | Where-Object { -not $_.ready }).Count -eq 0
        coverageDescription = Get-BrField $TargetDocument 'coverageDescription'; samplingPlanId = Get-BrField $sampling 'id'
        minimumIndependentSeedsPerCell = $MinimumSamples; requiredLowerBound = $MinimumLowerBound
        confidenceMethod = 'Wilson score, two-sided 95% interval, lower endpoint; per stratum, NOT simultaneous joint 95% coverage.'
        decisionRule = 'Every explicitly required stratum must pass; no overall win-rate compensation; deaths/timeouts/retries are never wins.'
        scope = 'Conditional on the declared complete, fixed, independent sampling plan and supplied audited summaries. Unique seeds do not themselves prove independence. Profiles/builds are not pooled. This is not a guarantee for all gameplay.'
        cells = $cells; unmatchedRecords = $unmatched.Count
        unmatchedStrata = @($unmatched | Group-Object Key | ForEach-Object { [pscustomobject]@{ identity = $_.Group[0].Identity; records = $_.Count } })
        unassignableEvidence = @($unassignable.ToArray())
    }
}

function Assert-BrProjectPath([string]$Path, [string]$Root, [bool]$MustExist) {
    if ([string]::IsNullOrWhiteSpace($Path) -or -not [IO.Path]::IsPathRooted($Path)) { throw 'Use an explicit absolute project JSON path.' }
    $full = [IO.Path]::GetFullPath($Path)
    if (-not $full.StartsWith($Root.TrimEnd('\', '/') + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetExtension($full) -ine '.json') { throw 'JSON path must stay inside the allowed project directory.' }
    $cursor = $full
    while (-not [string]::IsNullOrWhiteSpace($cursor)) {
        $entry = Get-Item -LiteralPath $cursor -Force -ErrorAction SilentlyContinue
        if ($null -ne $entry -and ($entry.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Linked paths are not accepted.' }
        $parent = [IO.Directory]::GetParent($cursor)
        if ($null -eq $parent) { break }
        $cursor = $parent.FullName
    }
    if ($MustExist) {
        $entry = Get-Item -LiteralPath $full
        if ($entry.PSIsContainer -or $entry.Length -gt 128MB) { throw 'Input must be a JSON file no larger than 128 MiB.' }
    }
    return $full
}

if ($FunctionsOnly) { return }
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($null -eq $Summary -or $Summary.Count -eq 0) { throw 'Supply -Summary with the explicit batch summary JSON paths.' }
$sources = @(foreach ($path in $Summary) {
    $full = Assert-BrProjectPath $path $root $true
    [pscustomobject]@{ Source = $full; Document = Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json; Invalidated = Test-Path -LiteralPath (Join-Path (Split-Path -Parent $full) 'INVALIDATED.md') }
})
if ($DescribeProfiles) {
    if (-not [string]::IsNullOrWhiteSpace($Targets)) { throw 'Choose profile discovery or target evaluation, not both.' }
    $profiles = @(foreach ($source in $sources) {
        if ((Get-BrField $source.Document 'Schema') -cne 'chaite-boss-batch-summary/v1') { throw 'Unsupported input summary schema.' }
        foreach ($record in @(Get-BrField $source.Document 'Cases' @())) {
            try { Get-BossEvidenceIdentity $record } catch { Write-Warning "Profile unavailable: $($source.Source) / $(Get-BrField $record 'Id'): $($_.Exception.Message)" }
        }
    })
    $report = [pscustomobject]@{ schema = 'chaite-boss-readiness-profiles/v1'; ready = $false; note = 'Discovery only; not a complete target declaration or a readiness decision.'; profiles = @($profiles | Group-Object { Get-BrIdentityKey $_ } | ForEach-Object { $_.Group[0] }) }
} else {
    $targetPath = Assert-BrProjectPath $Targets $root $true
    $targetDocument = Get-Content -LiteralPath $targetPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $report = Measure-BossReadiness $targetDocument $sources $MinimumSamples $MinimumLowerBound
}
$json = ConvertTo-Json -InputObject $report -Depth 40
if (-not [string]::IsNullOrWhiteSpace($Output)) {
    $outputPath = Assert-BrProjectPath $Output (Join-Path $root 'artifacts') $false
    if (Test-Path -LiteralPath $outputPath) { throw 'Output already exists; never overwrite previous evidence.' }
    if (-not (Test-Path -LiteralPath (Split-Path -Parent $outputPath) -PathType Container)) { throw 'Explicit output parent must already exist under project artifacts.' }
    $stream = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $bytes = [Text.Encoding]::UTF8.GetBytes($json); $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
}
Write-Output $json
if (-not $DescribeProfiles -and -not $report.ready) { exit 2 }
exit 0

param(
    [string[]]$Summary,
    [string]$Targets,
    [string]$Catalog,
    [string]$Output,
    [ValidateRange(40, 1000000)][int]$MinimumSamples = 40,
    [ValidateRange(0.90, 1.0)][double]$MinimumLowerBound = 0.90,
    [switch]$DescribeProfiles,
    [switch]$FunctionsOnly
)

# Read-only by default: print JSON, never start Terraria, build, or alter evidence.
# -Targets accepts the legacy chaite-boss-readiness-targets/v1 policy or the
# product-target chaite-boss-readiness-targets/v2 policy. V2 and V3 require
# every target to declare goal=priority-near-certain or secondary-majority.
# V3 additionally binds every evidence target to the versioned, checked-in
# Terraria core-Boss coverage catalog supplied through -Catalog. V3 is the
# product acceptance format: it cannot call a conveniently small subset a
# "complete" list.
# Both schemas require completeTargetList=true,
# coverageDescription, samplingPlan={id,description,independentSeedsDeclared:true,
# fixedBeforeEvaluation:true}, and targets=[{id,boss,difficulty,variant,build,
# profileSha256,plannedSeeds:[...]}]. All planned seeds must finish. V1 keeps
# its historical >=40 / Wilson lower bound >=0.90 rule. V2 priority cells also
# require an observed fraction >=0.95; secondary cells require both observed
# success and the Wilson lower endpoint to be strictly greater than 0.50.
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

function Test-BrFieldDeclared($Object, [string]$Name) {
    if ($null -eq $Object) { return $false }
    if ($Object -is [Collections.IDictionary]) { return $Object.Contains($Name) }
    return $null -ne $Object.PSObject.Properties[$Name]
}

function Test-BrTrue($Value) { return $Value -is [bool] -and $Value }
function Test-BrFalse($Value) { return $Value -is [bool] -and -not $Value }
function Test-BrKnownVariant($Value) {
    return $Value -is [string] -and $Value -cin @('standard', 'night', 'day',
        'simultaneous-mechanical-trio', 'getfixedboi-mechdusa')
}
function Get-BrExpectedSummonItem([string]$Scenario) {
    switch -CaseSensitive ($Scenario) {
        'eye' { return 43 }
        'king-slime' { return 560 }
        'queen-slime' { return 4988 }
        'destroyer' { return 556 }
        'twins' { return 544 }
        'prime' { return 557 }
        'deerclops' { return 5120 }
        'queen-bee' { return 1133 }
        default { return 0 }
    }
}
function Add-BrVariantEvidenceErrors($Record, $Result, $Errors) {
    $variant = Get-BrField $Result 'variant'
    if (-not (Test-BrKnownVariant $variant)) {
        $Errors.Add('variant-missing-or-unknown')
        return
    }
    if (-not (Test-BrTrue (Get-BrField $Result 'readinessEligible')) -or
        (Get-BrField $Result 'evidenceKind') -cne 'isolated-native-encounter') {
        # A real native staged-phase result remains useful regression evidence,
        # but it must never become a campaign win-rate observation.
        $Errors.Add('not-readiness-eligible')
    }
    $evidence = Get-BrField $Result 'variantEvidence'
    if ($null -eq $evidence -or (Get-BrField $evidence 'schema') -cne
            'chaite-boss-variant-evidence/v1' -or
        (Get-BrField $evidence 'scenario') -cne (Get-BrField $Record 'Scenario') -or
        (Get-BrField $evidence 'expectedVariant') -cne $variant -or
        (Get-BrField $evidence 'observedVariant') -cne $variant -or
        (Get-BrField $evidence 'reportedVariant') -cne $variant -or
        -not (Test-BrTrue (Get-BrField $evidence 'capturedAtActivation')) -or
        -not (Test-BrTrue (Get-BrField $evidence 'nativeFlagsVerified')) -or
        -not (Test-BrTrue (Get-BrField $evidence 'variantMatchesScenario')) -or
        -not (Test-BrTrue (Get-BrField $evidence 'allExpectedBossesSeen'))) {
        $Errors.Add('variant-evidence-unverified')
        return
    }
    $takeover = Get-BrField $Record 'TakeoverTick'
    $captureTick = Get-BrField $evidence 'captureTick'
    if (($takeover -isnot [int] -and $takeover -isnot [long]) -or
        ($captureTick -isnot [int] -and $captureTick -isnot [long]) -or
        $captureTick -ne $takeover -or (Get-BrField $evidence 'requestedTakeoverTick') -ne $takeover) {
        $Errors.Add('variant-capture-tick-mismatch')
        return
    }
    $native = Get-BrField $evidence 'native'
    $mode = @('classic', 'expert', 'master').IndexOf([string](Get-BrField $Record 'Difficulty'))
    if ($null -eq $native -or $mode -lt 0 -or (Get-BrField $native 'gameMode') -ne $mode -or
        (Get-BrField $native 'difficulty') -ne ($mode + 1)) {
        $Errors.Add('variant-native-difficulty-mismatch')
        return
    }
    $flags = @('dayTime', 'hardMode', 'forTheWorthy', 'zenithWorld', 'drunkWorld',
        'notTheBeesWorld', 'remixWorld', 'celebrationWorld', 'constantWorld',
        'noTrapsWorld', 'skyblockWorld')
    foreach ($flag in $flags) {
        if ((Get-BrField $native $flag) -isnot [bool]) {
            $Errors.Add('variant-native-flags-malformed')
            return
        }
    }
    $standardRules = @('forTheWorthy', 'zenithWorld', 'drunkWorld', 'notTheBeesWorld',
        'remixWorld', 'celebrationWorld', 'constantWorld', 'noTrapsWorld', 'skyblockWorld')
    $hasSpecialRule = @(
        $standardRules | Where-Object {
            Test-BrTrue (Get-BrField $native $_)
        }
    ).Count -gt 0
    $mechanicalExpected = Test-BrTrue (Get-BrField $evidence 'mechanicalTrioExpected')
    $mechanicalObserved = Test-BrTrue (Get-BrField $evidence 'mechanicalTrioObserved')
    switch ($variant) {
        'standard' {
            if ($hasSpecialRule -or $mechanicalExpected -or $mechanicalObserved) { $Errors.Add('standard-variant-native-mismatch') }
        }
        'night' {
            if ((Test-BrTrue (Get-BrField $native 'dayTime')) -or
                $hasSpecialRule -or $mechanicalExpected -or $mechanicalObserved) { $Errors.Add('night-variant-native-mismatch') }
        }
        'day' {
            if (-not (Test-BrTrue (Get-BrField $native 'dayTime')) -or
                $hasSpecialRule -or $mechanicalExpected -or $mechanicalObserved) { $Errors.Add('day-variant-native-mismatch') }
        }
        'simultaneous-mechanical-trio' {
            if ((Get-BrField $Record 'Scenario') -cne 'mechanical-mayhem' -or $hasSpecialRule -or
                -not $mechanicalExpected -or -not $mechanicalObserved) { $Errors.Add('mechanical-trio-variant-native-mismatch') }
        }
        'getfixedboi-mechdusa' {
            if ((Get-BrField $Record 'Scenario') -cne 'mechdusa' -or -not (Test-BrTrue (Get-BrField $native 'zenithWorld')) -or
                -not $mechanicalExpected -or -not $mechanicalObserved) { $Errors.Add('mechdusa-variant-native-mismatch') }
        }
    }
}

# `evidenceKind` and `readinessEligible` are producer declarations, not an
# origin proof on their own.  The current native probe writes the following
# fields for every result, including the deliberately staged priority fixtures.
# Keep the campaign evaluator independently fail-closed here so a staged
# result cannot become a win-rate observation merely by being copied into a
# summary with more favourable labels.  A future organic-priority probe that
# intentionally changes this contract must use a new result schema and update
# this verifier together with its evidence tests; silently accepting missing
# origin fields would re-open that downgrade path.
function Add-BrUnstagedEncounterOriginErrors($Result, $Errors) {
    $directSpawn = Get-BrField $Result 'directSpawn'
    if (-not (Test-BrFalse $directSpawn)) {
        $Errors.Add('encounter-origin-direct-spawn-or-missing')
    }

    $directSpawnTick = Get-BrField $Result 'directSpawnTick'
    if (($directSpawnTick -isnot [int] -and $directSpawnTick -isnot [long]) -or
        $directSpawnTick -ne -1) {
        $Errors.Add('encounter-origin-direct-spawn-tick-invalid')
    }

    foreach ($field in @('directSpawnAttempted', 'directSpawnCompleted',
            'phaseStageAttempted', 'phaseStaged', 'phaseVerifiedAtTakeover')) {
        if (-not (Test-BrFalse (Get-BrField $Result $field))) {
            $Errors.Add('encounter-origin-staging-flag-or-missing')
            break
        }
    }

    # These reports are produced only by the reviewed direct/staged path in
    # result schema v1.  Require their explicit null form rather than treating
    # an omitted field as indistinguishable from a clean encounter.
    foreach ($field in @('phaseStage', 'takeoverNativeSnapshot')) {
        if (-not (Test-BrFieldDeclared $Result $field) -or
            $null -ne (Get-BrField $Result $field)) {
            $Errors.Add('encounter-origin-staging-report-present')
            break
        }
    }

    if (-not (Test-BrTrue (Get-BrField $Result 'encounterFixtureReady'))) {
        $Errors.Add('encounter-origin-fixture-not-ready')
    }
    if (-not (Test-BrTrue (Get-BrField $Result 'summonConsumed'))) {
        $Errors.Add('encounter-origin-summon-not-observed')
    }
    $expectedSummon = Get-BrExpectedSummonItem ([string](Get-BrField $Result 'scenario'))
    $equipment = Get-BrField $Result 'equipment'
    $summonType = Get-BrField $equipment 'summonType'
    $summonCount = Get-BrField $equipment 'summonCount'
    if ($expectedSummon -le 0 -or $null -eq $equipment -or
        ($summonType -isnot [int] -and $summonType -isnot [long]) -or
        ($summonCount -isnot [int] -and $summonCount -isnot [long]) -or
        $summonType -ne $expectedSummon -or $summonCount -ne 1) {
        $Errors.Add('encounter-origin-hotbar-summon-unverified')
    }
}

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

function Get-BossEvidenceIdentity($Record, [bool]$RequireExplicitVariant = $false) {
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
    $reportedVariant = Get-BrField $result 'variant' ''
    if ($RequireExplicitVariant -and $reportedVariant -isnot [string]) {
        throw 'Catalog-bound V3 evidence result.variant must be a nonempty string.'
    }
    $variant = [string]$reportedVariant
    # Old reviewed fixtures have no variant field; keep that legacy identity
    # explicit instead of silently calling it every normal/special-seed variant.
    if ([string]::IsNullOrWhiteSpace($variant)) {
        if ($RequireExplicitVariant) {
            throw 'Catalog-bound V3 evidence requires an explicit result.variant declaration.'
        }
        $variant = [string](Get-BrField $Record 'Variant' '')
    }
    if ([string]::IsNullOrWhiteSpace($variant)) {
        if (Test-BrFalse (Get-BrField $native 'forTheWorthy')) { $variant = 'legacy-standard-fixture' }
        else { throw 'Unreported/ambiguous variant; provide explicit variant evidence.' }
    }
    if ($RequireExplicitVariant) {
        $variantEvidence = Get-BrField $result 'variantEvidence'
        if ($null -eq $variantEvidence -or (Get-BrField $variantEvidence 'schema') -cne
                'chaite-boss-variant-evidence/v1' -or
            (Get-BrField $variantEvidence 'scenario') -cne (Get-BrField $Record 'Scenario') -or
            (Get-BrField $variantEvidence 'expectedVariant') -cne $variant -or
            (Get-BrField $variantEvidence 'observedVariant') -cne $variant -or
            (Get-BrField $variantEvidence 'reportedVariant') -cne $variant) {
            throw 'Catalog-bound V3 evidence requires result.variantEvidence to corroborate result.variant.'
        }
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

# Profile discovery feeds immutable V3 targets.  It must use the same
# non-staged campaign-origin gate as final scoring; otherwise a staged phase
# fixture could be smuggled into the target plan even though its later wins
# would correctly be rejected.  Losses remain useful for profile discovery as
# long as their raw evidence is valid: a profile describes the pre-battle
# configuration, not a selected winning outcome.
function Get-BrReadinessProfiles([object[]]$Sources) {
    $profiles = [Collections.Generic.List[object]]::new()
    foreach ($source in $Sources) {
        $document = Get-BrField $source 'Document'
        if ((Get-BrField $document 'Schema') -cne 'chaite-boss-batch-summary/v1') {
            throw 'Unsupported input summary schema.'
        }
        foreach ($record in @(Get-BrField $document 'Cases' @())) {
            $quality = Test-BrRecordEvidence $record ([bool](Get-BrField $source 'Invalidated' $false))
            if (-not $quality.ValidEvidence) {
                $reason = (@($quality.Errors) -join ',')
                Write-Warning "Profile unavailable: $(Get-BrField $source 'Source') / $(Get-BrField $record 'Id'): invalid campaign evidence ($reason)"
                continue
            }
            try {
                $profiles.Add((Get-BossEvidenceIdentity $record $true))
            } catch {
                Write-Warning "Profile unavailable: $(Get-BrField $source 'Source') / $(Get-BrField $record 'Id'): $($_.Exception.Message)"
            }
        }
    }
    return @($profiles | Group-Object { Get-BrIdentityKey $_ } |
        ForEach-Object { $_.Group[0] })
}

function Get-Wilson95LowerBound([int]$Wins, [int]$Samples) {
    if ($Samples -lt 0 -or $Wins -lt 0 -or $Wins -gt $Samples) { throw 'Invalid Wilson win/sample counts.' }
    if ($Samples -eq 0) { return 0.0 }
    $z = 1.959963984540054
    $z2 = $z * $z
    $p = [double]$Wins / $Samples
    return [Math]::Max(0.0, ($p + $z2 / (2 * $Samples) - $z * [Math]::Sqrt($p * (1 - $p) / $Samples + $z2 / (4 * $Samples * $Samples))) / (1 + $z2 / $Samples))
}

function Get-BossReadinessGoalPolicy([string]$Schema, $Target,
    [int]$MinimumSamples, [double]$MinimumLowerBound) {
    if ($Schema -ceq 'chaite-boss-readiness-targets/v1') {
        return [pscustomobject]@{
            Goal = 'legacy-high-confidence'
            MinimumSamples = $MinimumSamples
            MinimumObservedSuccessFraction = 0.0
            MinimumWilsonLowerBound = $MinimumLowerBound
            StrictObserved = $false
            StrictWilson = $false
        }
    }
    if ($Schema -cnotin @('chaite-boss-readiness-targets/v2',
            'chaite-boss-readiness-targets/v3')) {
        throw 'Unsupported Boss readiness target schema.'
    }
    $goal = [string](Get-BrField $Target 'goal' '')
    if ($goal -ceq 'priority-near-certain') {
        return [pscustomobject]@{
            Goal = $goal
            MinimumSamples = $MinimumSamples
            MinimumObservedSuccessFraction = 0.95
            MinimumWilsonLowerBound = [Math]::Max(0.90,
                $MinimumLowerBound)
            StrictObserved = $false
            StrictWilson = $false
        }
    }
    if ($goal -ceq 'secondary-majority') {
        return [pscustomobject]@{
            Goal = $goal
            MinimumSamples = $MinimumSamples
            MinimumObservedSuccessFraction = 0.50
            MinimumWilsonLowerBound = 0.50
            StrictObserved = $true
            StrictWilson = $true
        }
    }
    throw 'V2/V3 targets require goal=priority-near-certain or secondary-majority.'
}

# The catalog deliberately covers non-event vanilla Boss encounters. It is a
# product-scope declaration, not a claim that every row already has a native
# implementation or passing evidence. The two mechanical multi-Boss rows are
# included because they have their own runtime strategies; the event waves that
# contain enemies called "event bosses" are outside the user's Boss-only scope.
function Get-BrOfficialCoverageCells() {
    $definitions = @(
        [pscustomobject]@{ boss = 'king-slime'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'eye'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'eater-of-worlds'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'brain-of-cthulhu'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'queen-bee'; variant = 'standard'; goal = 'priority-near-certain' },
        [pscustomobject]@{ boss = 'deerclops'; variant = 'standard'; goal = 'priority-near-certain' },
        [pscustomobject]@{ boss = 'skeletron'; variant = 'standard'; goal = 'priority-near-certain' },
        [pscustomobject]@{ boss = 'wall-of-flesh'; variant = 'standard'; goal = 'priority-near-certain' },
        [pscustomobject]@{ boss = 'queen-slime'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'destroyer'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'twins'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'prime'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'plantera'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'golem'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'duke-fishron'; variant = 'standard'; goal = 'priority-near-certain' },
        [pscustomobject]@{ boss = 'lunatic-cultist'; variant = 'standard'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'moon-lord'; variant = 'standard'; goal = 'priority-near-certain' },
        [pscustomobject]@{ boss = 'mechanical-mayhem'; variant = 'simultaneous-mechanical-trio'; goal = 'secondary-majority' },
        [pscustomobject]@{ boss = 'mechdusa'; variant = 'getfixedboi-mechdusa'; goal = 'secondary-majority' }
    )
    $cells = [Collections.Generic.List[object]]::new()
    foreach ($definition in $definitions) {
        foreach ($difficulty in @('classic', 'expert', 'master')) {
            $cells.Add([pscustomobject]@{
                id = ($definition.boss + '-' + $difficulty + '-' + $definition.variant)
                boss = $definition.boss; difficulty = $difficulty
                variant = $definition.variant; goal = $definition.goal
            })
        }
    }
    return @($cells.ToArray())
}

function Get-BrCoverageCellKey($Cell) {
    return ([string](Get-BrField $Cell 'boss') + '|' +
        [string](Get-BrField $Cell 'difficulty') + '|' +
        [string](Get-BrField $Cell 'variant'))
}

function Assert-BrExactFields($Object, [string[]]$Expected,
    [string]$Name) {
    if ($null -eq $Object) { throw "Missing $Name object." }
    # An if-expression otherwise unwraps a one-property result into a scalar.
    # Keep it as an array so StrictMode can safely read Count for every object.
    $names = @(if ($Object -is [Collections.IDictionary]) {
            $Object.Keys | ForEach-Object { [string]$_ }
        } else {
            $Object.PSObject.Properties.Name
        })
    if ($names.Count -ne $Expected.Count) { throw "$Name field set changed." }
    foreach ($field in $Expected) {
        if ($names -cnotcontains $field) { throw "Missing $Name.$field declaration." }
    }
}

function Assert-BrNonEmptyString($Value, [string]$Name) {
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace($Value)) {
        throw "Missing or invalid $Name string."
    }
    return [string]$Value
}

function Assert-BrCoverageCatalog($CoverageCatalog) {
    Assert-BrExactFields -Object $CoverageCatalog -Expected @('schema', 'id', 'gameVersion',
        'completeTargetList', 'coverageDescription', 'scope', 'excludedScope',
        'cells') -Name 'coverage catalog'
    if ([string](Get-BrField $CoverageCatalog 'schema') -cne
            'chaite-boss-readiness-coverage/v1' -or
        [string](Get-BrField $CoverageCatalog 'id') -cne
            'terraria-1.4.5.8-core-bosses-v1' -or
        [string](Get-BrField $CoverageCatalog 'gameVersion') -cne '1.4.5.8' -or
        -not (Test-BrTrue (Get-BrField $CoverageCatalog 'completeTargetList'))) {
        throw 'Coverage catalog identity or completeTargetList declaration is invalid.'
    }
    foreach ($field in @('coverageDescription', 'scope', 'excludedScope')) {
        $null = Assert-BrNonEmptyString (Get-BrField $CoverageCatalog $field) "coverage catalog $field"
    }
    $cellsValue = Get-BrField $CoverageCatalog 'cells'
    if ($cellsValue -isnot [Array]) { throw 'Coverage catalog cells must be a JSON array.' }
    $expected = @{}; $expectedIds = @{}
    foreach ($cell in Get-BrOfficialCoverageCells) {
        $key = Get-BrCoverageCellKey $cell
        $expected[$key] = $cell
        $expectedIds[[string]$cell.id] = $key
    }
    if ($cellsValue.Count -ne $expected.Count) {
        throw 'Coverage catalog does not declare every required Boss/difficulty/variant cell.'
    }
    $seenKeys = @{}; $seenIds = @{}
    foreach ($cell in @($cellsValue)) {
        Assert-BrExactFields -Object $cell -Expected @('id', 'boss', 'difficulty', 'variant',
            'goal') -Name 'coverage catalog cell'
        foreach ($field in @('id', 'boss', 'difficulty', 'variant', 'goal')) {
            $null = Assert-BrNonEmptyString (Get-BrField $cell $field) "coverage catalog cell $field"
        }
        $key = Get-BrCoverageCellKey $cell
        $id = [string](Get-BrField $cell 'id')
        if ($seenKeys.ContainsKey($key) -or $seenIds.ContainsKey($id)) {
            throw 'Coverage catalog contains a duplicate Boss stratum or id.'
        }
        if (-not $expected.ContainsKey($key) -or -not $expectedIds.ContainsKey($id) -or
            $expectedIds[$id] -cne $key) {
            throw 'Coverage catalog has an unknown or mislabeled Boss stratum.'
        }
        $required = $expected[$key]
        if ([string](Get-BrField $cell 'goal') -cne [string]$required.goal) {
            throw 'Coverage catalog assigns the wrong product goal to a Boss stratum.'
        }
        $seenKeys[$key] = $true; $seenIds[$id] = $true
    }
    foreach ($key in $expected.Keys) {
        if (-not $seenKeys.ContainsKey($key)) {
            throw 'Coverage catalog omits a required Boss stratum.'
        }
    }
    return $CoverageCatalog
}

function Assert-BrTargetCoverageAgainstCatalog($TargetDocument,
    $CoverageCatalog) {
    Assert-BrCoverageCatalog $CoverageCatalog | Out-Null
    Assert-BrExactFields -Object $TargetDocument -Expected @('schema', 'completeTargetList',
        'coverageDescription', 'coverageCatalogId', 'coverageCatalogSha256',
        'samplingPlan', 'targets') -Name 'V3 target document'
    if ([string](Get-BrField $TargetDocument 'schema') -cne
            'chaite-boss-readiness-targets/v3') {
        throw 'A catalog-bound product target document must use V3.'
    }
    if ([string](Get-BrField $TargetDocument 'coverageCatalogId') -cne
            [string](Get-BrField $CoverageCatalog 'id')) {
        throw 'Target document coverage catalog id does not match the supplied catalog.'
    }
    $declaredHash = [string](Get-BrField $TargetDocument 'coverageCatalogSha256')
    if ($declaredHash -cnotmatch '^[A-F0-9]{64}$' -or
        $declaredHash -cne (Get-BrHash $CoverageCatalog)) {
        throw 'Target document coverage catalog hash does not match the supplied catalog.'
    }
    $catalogById = @{}
    foreach ($cell in @(Get-BrField $CoverageCatalog 'cells')) {
        $catalogById[[string](Get-BrField $cell 'id')] = $cell
    }
    $targets = @(Get-BrField $TargetDocument 'targets' @())
    if ($targets.Count -ne $catalogById.Count) {
        throw 'V3 target document does not have exactly one target for every catalog cell.'
    }
    $seenCellIds = @{}; $seenKeys = @{}; $builds = @{}
    foreach ($target in $targets) {
        $coverageCellId = Assert-BrNonEmptyString (Get-BrField $target 'coverageCellId') 'target coverageCellId'
        if ($seenCellIds.ContainsKey($coverageCellId) -or
            -not $catalogById.ContainsKey($coverageCellId)) {
            throw 'V3 target document has a duplicate or unknown coverage cell id.'
        }
        $cell = $catalogById[$coverageCellId]
        foreach ($field in @('boss', 'difficulty', 'variant', 'goal')) {
            if ([string](Get-BrField $target $field) -cne
                    [string](Get-BrField $cell $field)) {
                throw 'V3 target does not match its declared coverage catalog cell.'
            }
        }
        $key = Get-BrCoverageCellKey $target
        if ($seenKeys.ContainsKey($key)) {
            throw 'V3 target document duplicates a Boss/difficulty/variant stratum.'
        }
        $build = Get-BrField $target 'build'
        Assert-BrBuild $build
        $builds[(Get-BrHash $build)] = $true
        $seenCellIds[$coverageCellId] = $true; $seenKeys[$key] = $true
    }
    foreach ($id in $catalogById.Keys) {
        if (-not $seenCellIds.ContainsKey($id)) {
            throw 'V3 target document omits a required coverage catalog cell.'
        }
    }
    if ($builds.Count -ne 1) {
        throw 'V3 product readiness must bind every Boss stratum to one common build.'
    }
    return $true
}

# Construct, but do not persist, a catalog-bound V3 target document from the
# read-only profile-discovery report. Keeping this pure lets the generator and
# the offline regression test exercise the identical fail-closed rules.
function New-BrCatalogBoundTargetDocument($CoverageCatalog, $ProfileDocument,
    [int[]]$PlannedSeeds, [string]$SamplingPlanId,
    [string]$SamplingPlanDescription) {
    Assert-BrCoverageCatalog -CoverageCatalog $CoverageCatalog | Out-Null
    Assert-BrExactFields -Object $ProfileDocument -Expected @('schema', 'ready',
        'note', 'profiles') -Name 'profile discovery document'
    if ([string](Get-BrField $ProfileDocument 'schema') -cne
            'chaite-boss-readiness-profiles/v1' -or
        -not (Test-BrFalse (Get-BrField $ProfileDocument 'ready'))) {
        throw 'Profiles must be direct output from readiness profile discovery, not a readiness decision.'
    }
    if ((Get-BrField $ProfileDocument 'profiles') -isnot [Array]) {
        throw 'Profile discovery profiles must be a JSON array.'
    }
    $null = Assert-BrNonEmptyString $SamplingPlanId 'sampling plan id'
    $null = Assert-BrNonEmptyString $SamplingPlanDescription 'sampling plan description'
    if ($null -eq $PlannedSeeds -or $PlannedSeeds.Count -lt 40) {
        throw 'A product readiness plan requires at least 40 predeclared seeds per cell.'
    }
    $seedLookup = @{}
    foreach ($seed in $PlannedSeeds) {
        if ($seed -lt 0 -or $seed -gt 2147483647 -or
            $seedLookup.ContainsKey([string]$seed)) {
            throw 'Planned seeds must be distinct nonnegative int32 values.'
        }
        $seedLookup[[string]$seed] = $true
    }

    $catalogByKey = @{}
    foreach ($cell in @($CoverageCatalog.cells)) {
        $catalogByKey[(Get-BrCoverageCellKey $cell)] = $cell
    }
    $profilesByKey = @{}
    $builds = @{}
    foreach ($profile in @($ProfileDocument.profiles)) {
        Assert-BrExactFields -Object $profile -Expected @('boss', 'difficulty',
            'variant', 'build', 'profileSha256', 'profile') -Name 'discovered profile'
        foreach ($field in @('boss', 'difficulty', 'variant', 'profileSha256')) {
            $null = Assert-BrNonEmptyString (Get-BrField $profile $field) "discovered profile $field"
        }
        if ([string](Get-BrField $profile 'difficulty') -cnotin @('classic', 'expert', 'master') -or
            [string](Get-BrField $profile 'profileSha256') -cnotmatch '^[A-F0-9]{64}$' -or
            $null -eq (Get-BrField $profile 'profile')) {
            throw 'Discovered profile has an invalid difficulty, hash, or profile body.'
        }
        Assert-BrBuild (Get-BrField $profile 'build')
        $key = Get-BrCoverageCellKey $profile
        if (-not $catalogByKey.ContainsKey($key)) {
            throw 'Profile discovery contains a Boss/difficulty/variant outside the fixed coverage catalog.'
        }
        if ($profilesByKey.ContainsKey($key)) {
            throw 'Profile discovery contains more than one profile for a required coverage cell; resolve the profile choice before preregistration.'
        }
        $profilesByKey[$key] = $profile
        $builds[(Get-BrHash (Get-BrField $profile 'build'))] = $true
    }
    if ($builds.Count -ne 1) {
        throw 'All product readiness profile discoveries must use one common build.'
    }

    $targets = [Collections.Generic.List[object]]::new()
    foreach ($cell in @($CoverageCatalog.cells)) {
        $key = Get-BrCoverageCellKey $cell
        if (-not $profilesByKey.ContainsKey($key)) {
            throw "Profile discovery is missing required coverage cell: $($cell.id)"
        }
        $profile = $profilesByKey[$key]
        $targets.Add([ordered]@{
            id = ('target-' + [string]$cell.id)
            coverageCellId = [string]$cell.id
            boss = [string]$cell.boss
            difficulty = [string]$cell.difficulty
            variant = [string]$cell.variant
            goal = [string]$cell.goal
            build = Get-BrField $profile 'build'
            profileSha256 = [string](Get-BrField $profile 'profileSha256')
            plannedSeeds = @($PlannedSeeds)
        })
    }
    $targetDocument = [ordered]@{
        schema = 'chaite-boss-readiness-targets/v3'
        completeTargetList = $true
        coverageDescription = 'Generated catalog-bound Terraria 1.4.5.8 core-Boss readiness campaign. Every catalog cell is mandatory; no aggregate score can compensate for another cell.'
        coverageCatalogId = [string]$CoverageCatalog.id
        coverageCatalogSha256 = Get-BrHash $CoverageCatalog
        samplingPlan = [ordered]@{
            id = $SamplingPlanId
            description = $SamplingPlanDescription
            independentSeedsDeclared = $true
            fixedBeforeEvaluation = $true
        }
        targets = @($targets.ToArray())
    }
    Assert-BrTargetCoverageAgainstCatalog -TargetDocument $targetDocument `
        -CoverageCatalog $CoverageCatalog | Out-Null
    return $targetDocument
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
    Add-BrVariantEvidenceErrors $Record $result $errors
    Add-BrUnstagedEncounterOriginErrors $result $errors
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

function Measure-BossReadiness($TargetDocument, [object[]]$Summaries,
    [int]$MinimumSamples = 40, [double]$MinimumLowerBound = 0.90,
    $CoverageCatalog = $null) {
    if ($MinimumSamples -lt 40 -or $MinimumLowerBound -lt 0.90 -or $MinimumLowerBound -gt 1) { throw 'Readiness policy cannot be weaker than 40 samples / 0.90 lower bound.' }
    $targetSchema = [string](Get-BrField $TargetDocument 'schema')
    if ($targetSchema -cnotin @('chaite-boss-readiness-targets/v1',
            'chaite-boss-readiness-targets/v2',
            'chaite-boss-readiness-targets/v3') -or
        -not (Test-BrTrue (Get-BrField $TargetDocument 'completeTargetList')) -or
        [string]::IsNullOrWhiteSpace([string](Get-BrField $TargetDocument 'coverageDescription'))) { throw 'An explicit complete target list and coverage description are required.' }
    if ($targetSchema -ceq 'chaite-boss-readiness-targets/v3') {
        if ($null -eq $CoverageCatalog) {
            throw 'Catalog-bound V3 targets require the checked-in coverage catalog.'
        }
        Assert-BrTargetCoverageAgainstCatalog $TargetDocument $CoverageCatalog | Out-Null
    } elseif ($null -ne $CoverageCatalog) {
        throw 'The coverage catalog is only valid with a V3 target document.'
    }
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
        $null = Get-BossReadinessGoalPolicy $targetSchema $target `
            $MinimumSamples $MinimumLowerBound
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
                $identity = Get-BossEvidenceIdentity $record ($targetSchema -ceq 'chaite-boss-readiness-targets/v3')
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
        $goalPolicy = Get-BossReadinessGoalPolicy $targetSchema $target `
            $MinimumSamples $MinimumLowerBound
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
        $observedFraction = if ($n -gt 0) {
            [double]$wins / $n
        } else { 0.0 }
        $issues = [Collections.Generic.List[string]]::new()
        if ($n -lt $goalPolicy.MinimumSamples) { $issues.Add('insufficient-independent-seeds') }
        if ($missing.Count -gt 0) { $issues.Add('planned-seeds-unverified') }
        if ($duplicates.Count -gt 0) { $issues.Add('duplicate-seeds-require-review') }
        if ($unplanned.Count -gt 0) { $issues.Add('unplanned-seed-evidence-require-review') }
        if ($invalidCount -gt 0) { $issues.Add('invalid-evidence') }
        if ($unassignable.Count -gt 0) { $issues.Add('unassignable-evidence-require-review') }
        $observedPass = if ($goalPolicy.StrictObserved) {
            $observedFraction -gt
                $goalPolicy.MinimumObservedSuccessFraction
        } else {
            $observedFraction -ge
                $goalPolicy.MinimumObservedSuccessFraction
        }
        if (-not $observedPass) {
            $issues.Add('observed-success-fraction-below-target')
        }
        $wilsonPass = if ($goalPolicy.StrictWilson) {
            $lower -gt $goalPolicy.MinimumWilsonLowerBound
        } else {
            $lower -ge $goalPolicy.MinimumWilsonLowerBound
        }
        if (-not $wilsonPass) {
            $issues.Add('confidence-lower-bound-below-target')
        }
        $ready = $issues.Count -eq 0
        $status = if ($n -eq 0) { 'unverified' } elseif ($n -lt
            $goalPolicy.MinimumSamples) { 'smoke-only' } elseif ($ready) {
            'ready' } else { 'not-ready' }
        [pscustomobject]@{
            id = Get-BrField $target 'id'; boss = Get-BrField $target 'boss'; difficulty = Get-BrField $target 'difficulty'
            variant = Get-BrField $target 'variant'; build = Get-BrField $target 'build'; profileSha256 = Get-BrField $target 'profileSha256'
            coverageCellId = if ($targetSchema -ceq 'chaite-boss-readiness-targets/v3') {
                Get-BrField $target 'coverageCellId'
            } else { $null }
            goal = $goalPolicy.Goal
            status = $status; ready = $ready; plannedSeeds = $planned.Count; attemptedRecords = $selected.Count
            requiredIndependentSeeds = $goalPolicy.MinimumSamples
            requiredObservedSuccessFraction =
                $goalPolicy.MinimumObservedSuccessFraction
            observedThresholdIsStrict = $goalPolicy.StrictObserved
            requiredWilson95TwoSidedLowerBound =
                $goalPolicy.MinimumWilsonLowerBound
            wilsonThresholdIsStrict = $goalPolicy.StrictWilson
            independentSeedCount = $n; independentWins = $wins; nonWins = $n - $wins
            deathRecords = @($selected | Where-Object { $_.Quality.Death }).Count; timeoutRecords = @($selected | Where-Object { $_.Quality.Timeout }).Count
            invalidEvidenceRecords = $invalidCount; duplicateSeeds = $duplicates; unplannedRecords = $unplanned.Count; missingSeeds = $missing
            observedSuccessFraction = $(if ($n -gt 0) {
                $observedFraction } else { $null })
            wilson95TwoSidedLowerBound = $lower; reasons = @($issues.ToArray())
        }
    })
    $unmatched = @($evidence | Where-Object { -not $keys.ContainsKey($_.Key) })
    return [pscustomobject]@{
        schema = if ($targetSchema -ceq
            'chaite-boss-readiness-targets/v3') {
            'chaite-boss-readiness/v3'
        } elseif ($targetSchema -ceq
            'chaite-boss-readiness-targets/v2') {
            'chaite-boss-readiness/v2'
        } else { 'chaite-boss-readiness/v1' }
        targetSchema = $targetSchema
        ready = @($cells | Where-Object { -not $_.ready }).Count -eq 0
        coverageDescription = Get-BrField $TargetDocument 'coverageDescription'; samplingPlanId = Get-BrField $sampling 'id'
        minimumIndependentSeedsPerCell = $MinimumSamples
        legacyOrPriorityRequiredLowerBound = $MinimumLowerBound
        confidenceMethod = 'Wilson score, two-sided 95% interval, lower endpoint; per stratum, NOT simultaneous joint 95% coverage.'
        decisionRule = 'Every explicitly required stratum must pass its declared goal; no overall win-rate compensation; deaths/timeouts/retries are never wins. Candidate result schema v1 records must independently prove a non-staged origin: directSpawn=false, directSpawnTick=-1, all staging flags=false, staging reports=null, encounterFixtureReady=true, summonConsumed=true. V2/V3 priority-near-certain means observed >=0.95 and Wilson lower >=0.90; secondary-majority means both observed and Wilson lower >0.50.'
        scope = 'Conditional on the declared complete, fixed, independent sampling plan and supplied audited summaries. Unique seeds do not themselves prove independence. Profiles/builds are not pooled. The summary evaluator is not a replacement for raw launcher/manifest/desktop-lock evidence review. This is not a guarantee for all gameplay.'
        coverageCatalog = if ($targetSchema -ceq 'chaite-boss-readiness-targets/v3') {
            [pscustomobject]@{
                id = Get-BrField $CoverageCatalog 'id'
                sha256 = Get-BrHash $CoverageCatalog
                cells = @(Get-BrField $CoverageCatalog 'cells').Count
            }
        } else { $null }
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
    [pscustomobject]@{ Source = $full; Document = (Get-Content -LiteralPath $full -Raw -Encoding UTF8 | ConvertFrom-Json); Invalidated = Test-Path -LiteralPath (Join-Path (Split-Path -Parent $full) 'INVALIDATED.md') }
})
if ($DescribeProfiles) {
    if (-not [string]::IsNullOrWhiteSpace($Targets) -or
        -not [string]::IsNullOrWhiteSpace($Catalog)) {
        throw 'Choose profile discovery or catalog-bound target evaluation, not both.'
    }
    $profiles = Get-BrReadinessProfiles $sources
    $report = [pscustomobject]@{ schema = 'chaite-boss-readiness-profiles/v1'; ready = $false; note = 'Discovery only; not a complete target declaration or a readiness decision. Only valid non-staged campaign-origin evidence is included.'; profiles = $profiles }
} else {
    $targetPath = Assert-BrProjectPath $Targets $root $true
    $targetDocument = (Get-Content -LiteralPath $targetPath -Raw -Encoding UTF8 | ConvertFrom-Json)
    $coverageCatalog = $null
    if (-not [string]::IsNullOrWhiteSpace($Catalog)) {
        $catalogPath = Assert-BrProjectPath $Catalog $root $true
        $coverageCatalog = (Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 | ConvertFrom-Json)
    }
    $report = Measure-BossReadiness $targetDocument $sources $MinimumSamples $MinimumLowerBound $coverageCatalog
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

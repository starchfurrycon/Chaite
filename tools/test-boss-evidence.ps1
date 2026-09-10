# Offline regression for the ACTUAL runner evidence gate. This file never
# dot-sources the runner, prepares a fixture, loads game code, or writes files.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$runnerPath = Join-Path $PSScriptRoot 'run-boss-validation.ps1'
$tokens = $null
$parseErrors = $null
$runnerAst = [Management.Automation.Language.Parser]::ParseFile($runnerPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) { throw 'Cannot test a runner containing parse errors.' }
foreach ($functionName in @('Read-Field', 'Require-Integer', 'Require-Boolean')) {
    $definitions = @($runnerAst.FindAll({
        param($node)
        $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -ceq $functionName
    }, $true))
    if ($definitions.Count -ne 1) { throw "Expected exactly one actual $functionName definition." }
    . ([scriptblock]::Create($definitions[0].Extent.Text))
}
$gates = @($runnerAst.FindAll({
    param($node)
    $node -is [Management.Automation.Language.IfStatementAst] -and
        $node.Clauses.Count -eq 1 -and $node.Clauses[0].Item1.Extent.Text.Trim() -ceq '$validBattle'
}, $true))
if ($gates.Count -ne 1) { throw 'Expected exactly one if ($validBattle) evidence gate.' }
$gate = [scriptblock]::Create($gates[0].Extent.Text)
$script:passed = 0
$script:failed = 0

function New-Evidence([string]$Difficulty, [string]$Scenario = 'eye') {
    $mode = @('classic', 'expert', 'master').IndexOf($Difficulty)
    [pscustomobject]@{
        Case = [pscustomobject]@{ Difficulty = $Difficulty; Scenario = $Scenario; Seed = 20260910 }
        Result = [pscustomobject]@{
            nativeDifficultyVerified = $true
            nativeFrames = 150
            nativeDifficulty = [pscustomobject]@{
                gameMode = $mode; worldFileGameMode = $mode; difficulty = $mode + 1
                worldFileSeed = 20260910; expertMode = $mode -gt 0; masterMode = $mode -eq 2
                hardMode = $Scenario -notin @('eye', 'king-slime'); forTheWorthy = $false
            }
            battleRandom = [pscustomobject]@{
                installedAfterSetup = $true; actualAndNativeNamedColdStateVerified = $true
                actualStreamConsumedForFingerprint = $false; seed = 20260910
                unpausedUpdateSeedInitial = 20260910; unpausedUpdateSeedAdvances = 150
                referenceChecks = 151; independentTwinFingerprint = @(10, 20, 30, 40, 50, 60, 70, 80)
            }
            firstObservedBosses = @([pscustomobject]@{
                lifeMax = 2800; gameMode = $mode; difficulty = $mode + 1; npcDifficulty = $mode + 1
            })
        }
    }
}

function Set-EvidenceField($Evidence, [string]$Path, $Value, [switch]$Remove) {
    $segments = $Path.Split('.')
    $parent = $Evidence.Result
    for ($index = 0; $index -lt $segments.Count - 1; $index++) {
        $segment = $segments[$index]
        if ($segment -match '^\d+$') { $parent = $parent[[int]$segment] }
        else { $parent = $parent.PSObject.Properties[$segment].Value }
    }
    $name = $segments[$segments.Count - 1]
    if ($Remove) { $parent.PSObject.Properties.Remove($name) }
    else { $parent.PSObject.Properties[$name].Value = $Value }
}

function Test-Evidence([string]$Name, $Evidence, [bool]$ShouldAccept) {
    # Apply the same JSON boundary as result.json, including Int32/Int64 and
    # Boolean deserialization, instead of validating a convenient hashtable.
    $roundTrip = $Evidence | ConvertTo-Json -Depth 12 -Compress | ConvertFrom-Json
    $case = $roundTrip.Case
    $result = $roundTrip.Result
    $validBattle = $true
    $accepted = $true
    $failure = $null
    try { & $gate | Out-Null }
    catch { $accepted = $false; $failure = $_.Exception.Message }
    if ($accepted -eq $ShouldAccept) {
        $script:passed++
        Write-Output "PASS $Name"
    } else {
        $script:failed++
        if ($ShouldAccept) { Write-Output "FAIL $Name -- valid evidence rejected: $failure" }
        else { Write-Output "FAIL $Name -- malformed evidence was accepted" }
    }
}

foreach ($difficulty in @('classic', 'expert', 'master')) {
    foreach ($scenario in @('eye', 'destroyer')) {
        Test-Evidence "accept-$difficulty-$scenario" (New-Evidence $difficulty $scenario) $true
    }
}
foreach ($difficulty in @('classic', 'expert', 'master')) {
    $mode = @('classic', 'expert', 'master').IndexOf($difficulty)
    $missing = @('nativeDifficultyVerified', 'nativeDifficulty',
        'nativeDifficulty.gameMode', 'nativeDifficulty.worldFileGameMode', 'nativeDifficulty.difficulty',
        'nativeDifficulty.worldFileSeed', 'nativeDifficulty.expertMode', 'nativeDifficulty.masterMode',
        'nativeDifficulty.hardMode', 'nativeDifficulty.forTheWorthy', 'nativeFrames', 'battleRandom',
        'battleRandom.installedAfterSetup', 'battleRandom.actualAndNativeNamedColdStateVerified',
        'battleRandom.actualStreamConsumedForFingerprint', 'battleRandom.seed',
        'battleRandom.unpausedUpdateSeedInitial', 'battleRandom.unpausedUpdateSeedAdvances',
        'battleRandom.referenceChecks', 'battleRandom.independentTwinFingerprint', 'firstObservedBosses',
        'firstObservedBosses.0.lifeMax', 'firstObservedBosses.0.gameMode',
        'firstObservedBosses.0.difficulty', 'firstObservedBosses.0.npcDifficulty')
    foreach ($path in $missing) {
        $evidence = New-Evidence $difficulty
        Set-EvidenceField $evidence $path $null -Remove
        Test-Evidence "reject-$difficulty-missing-$path" $evidence $false
    }
    $wrong = @(
        @{ Path = 'nativeDifficultyVerified'; Value = $false },
        @{ Path = 'nativeDifficulty.gameMode'; Value = ($mode + 1) % 3 },
        @{ Path = 'nativeDifficulty.worldFileGameMode'; Value = ($mode + 1) % 3 },
        @{ Path = 'nativeDifficulty.difficulty'; Value = $mode + 2 },
        @{ Path = 'nativeDifficulty.worldFileSeed'; Value = 20260911 },
        @{ Path = 'nativeDifficulty.expertMode'; Value = $mode -eq 0 },
        @{ Path = 'nativeDifficulty.masterMode'; Value = $mode -ne 2 },
        @{ Path = 'nativeDifficulty.hardMode'; Value = $true },
        @{ Path = 'nativeDifficulty.forTheWorthy'; Value = $true },
        @{ Path = 'battleRandom.installedAfterSetup'; Value = $false },
        @{ Path = 'battleRandom.actualAndNativeNamedColdStateVerified'; Value = $false },
        @{ Path = 'battleRandom.actualStreamConsumedForFingerprint'; Value = $true },
        @{ Path = 'battleRandom.seed'; Value = 20260911 },
        @{ Path = 'battleRandom.unpausedUpdateSeedInitial'; Value = 20260911 },
        @{ Path = 'battleRandom.unpausedUpdateSeedAdvances'; Value = 149 },
        @{ Path = 'battleRandom.referenceChecks'; Value = 149 },
        @{ Path = 'battleRandom.independentTwinFingerprint'; Value = @(1, 2, 3, 4, 5, 6, 7) },
        @{ Path = 'firstObservedBosses'; Value = @() },
        @{ Path = 'firstObservedBosses.0.lifeMax'; Value = 0 },
        @{ Path = 'firstObservedBosses.0.gameMode'; Value = ($mode + 1) % 3 },
        @{ Path = 'firstObservedBosses.0.difficulty'; Value = $mode + 2 },
        @{ Path = 'firstObservedBosses.0.npcDifficulty'; Value = $mode + 2 }
    )
    foreach ($change in $wrong) {
        $evidence = New-Evidence $difficulty
        Set-EvidenceField $evidence $change.Path $change.Value
        Test-Evidence "reject-$difficulty-wrong-$($change.Path)" $evidence $false
    }
}

# Require-Integer is also extracted from production; exercise both valid limits
# and JSON number types that must not silently truncate into a valid tick count.
foreach ($frames in @(121, 24000)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence 'nativeFrames' $frames
    Set-EvidenceField $evidence 'battleRandom.unpausedUpdateSeedAdvances' $frames
    Set-EvidenceField $evidence 'battleRandom.referenceChecks' $frames
    Test-Evidence "accept-nativeFrames-boundary-$frames" $evidence $true
}
foreach ($frames in @(120, 24001, 121.5, '150', $true, $null, [long]2147483648)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence 'nativeFrames' $frames
    Test-Evidence "reject-nativeFrames-value-[$frames]" $evidence $false
}
foreach ($change in @(
    @{ Path = 'battleRandom.independentTwinFingerprint'; Value = @(1, 2, 3, 4, 5, 6, 7, 8, 9) },
    @{ Path = 'battleRandom.independentTwinFingerprint'; Value = '12345678' },
    @{ Path = 'battleRandom.unpausedUpdateSeedAdvances'; Value = 151 },
    @{ Path = 'battleRandom.referenceChecks'; Value = -1 },
    @{ Path = 'nativeDifficultyVerified'; Value = 'true' },
    @{ Path = 'nativeDifficulty.worldFileSeed'; Value = '20260910' },
    @{ Path = 'firstObservedBosses.0.npcDifficulty'; Value = '1' },
    @{ Path = 'battleRandom.actualAndNativeNamedColdStateVerified'; Value = 'true' },
    @{ Path = 'battleRandom.referenceChecks'; Value = '150' },
    @{ Path = 'battleRandom.independentTwinFingerprint'; Value = @('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h') }
)) {
    $evidence = New-Evidence 'classic'
    Set-EvidenceField $evidence $change.Path $change.Value
    Test-Evidence "reject-malformed-$($change.Path)-$($change.Value)" $evidence $false
}
Write-Output "Offline native-evidence regression: $script:passed passed, $script:failed failed."
if ($script:failed -gt 0) { exit 1 }

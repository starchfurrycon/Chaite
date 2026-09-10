param(
    [string]$Cases,
    [ValidateSet('smoke6', 'standard18')][string]$Suite = 'smoke6',
    [switch]$Run,
    [string]$OutputDirectory,
    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria',
    [ValidateRange(1, 36)][int]$MaximumCases = 18,
    [ValidateRange(120, 240)][int]$TimeoutSeconds = 120
)

# Default is a read-only plan. Actual native-engine execution requires -Run.
# This is a serial, bounded fixture suite, never a proof of general win rate.
# Plan format: {"schema":"chaite-boss-cases/v1","cases":[
#   {"scenario":"eye","seed":20260910,"difficulty":"classic"}]}
# Optional case fields: maxTicks (600..24000), wallSeconds (15..90).
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
$scenarios = @('eye', 'king-slime', 'queen-slime', 'destroyer', 'twins', 'prime')

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
    if ((Read-Field $document 'schema') -cne 'chaite-boss-cases/v1') { throw 'Unsupported case-plan schema.' }
    foreach ($property in $document.PSObject.Properties.Name) { if ($property -cnotin @('schema', 'cases')) { throw "Unknown plan field: $property" } }
    $rawCases = @(Read-Field $document 'cases')
    $planSource = $Cases
} else {
    $seeds = if ($Suite -eq 'smoke6') { @(20260910) } else { @(20260910, 20260911, 20260912) }
    $rawCases = @(foreach ($scenario in $scenarios) { foreach ($seed in $seeds) { [pscustomobject]@{ scenario = $scenario; seed = $seed; difficulty = 'classic' } } })
    $planSource = 'builtin:' + $Suite
}
if ($rawCases.Count -lt 1 -or $rawCases.Count -gt $MaximumCases) { throw "Plan must contain 1..$MaximumCases cases; larger batches require an explicit bounded MaximumCases (at most 36)." }
$plan = @(for ($index = 0; $index -lt $rawCases.Count; $index++) {
    $case = $rawCases[$index]
    foreach ($property in $case.PSObject.Properties.Name) { if ($property -cnotin @('scenario', 'seed', 'difficulty', 'maxTicks', 'wallSeconds')) { throw "Unknown case field: $property" } }
    $scenario = Read-Field $case 'scenario'
    if ($scenario -cnotin $scenarios) { throw 'Case scenario must be one of the six reviewed fixtures; legacy eye-baseline is deliberately excluded.' }
    $seed = Require-Integer (Read-Field $case 'seed') 0 2147483647 'seed'
    $difficulty = Read-Field $case 'difficulty' 'classic'
    if ($difficulty -cnotin @('classic', 'expert', 'master')) { throw 'Invalid case difficulty.' }
    [pscustomobject]@{
        Id = ('case{0:D3}' -f ($index + 1)); Scenario = $scenario; Seed = $seed; Difficulty = $difficulty
        MaxTicks = (Require-Integer (Read-Field $case 'maxTicks' 24000) 600 24000 'maxTicks')
        WallSeconds = (Require-Integer (Read-Field $case 'wallSeconds' 90) 15 90 'wallSeconds')
    }
})
Write-Output ("Plan: $($plan.Count) serial cases from $planSource")
$plan | Format-Table Id, Scenario, Seed, Difficulty, MaxTicks, WallSeconds | Out-String | Write-Output
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
        ByScenario = @(foreach ($group in ($records | Group-Object Scenario, Difficulty)) {
            $groupWins = @($group.Group | Where-Object { $_.Classification -eq 'win' }).Count
            $groupStarted = @($group.Group | Where-Object { $_.BattleStarted }).Count
            [pscustomobject]@{
                Scenario = $group.Group[0].Scenario; Difficulty = $group.Group[0].Difficulty
                Attempted = $group.Count; Started = $groupStarted; Wins = $groupWins
                OverallAttemptSuccessPercent = (Get-Rate $groupWins $group.Count)
                StartedBattleSuccessPercent = (Get-Rate $groupWins $groupStarted)
            }
        })
        DenominatorNote = 'Overall includes EVERY attempted case, including rejected, harness/host errors and timeouts. Started includes any observed boss arrival, even if later evidence fails. Valid excludes harness errors, never ordinary losses or combat timeouts.'
        GeneralizationNote = 'Small, fixed synthetic arena/loadout/seed samples are not general gameplay win rates or guaranteed victories. No automatic retries or successful-case selection.'
        Cases = @($records.ToArray())
    }
    $summary | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'summary.json') -Encoding UTF8
    $records | Select-Object Id, Scenario, Seed, Difficulty, Classification, BattleStarted, ValidBattle, Deaths, HostExitCode, ChildExitCode, DesktopSafe, InputPluginSha256, InputCoreSha256, Failure, RunDirectory |
        Export-Csv -LiteralPath (Join-Path $OutputDirectory 'summary.csv') -NoTypeInformation -Encoding UTF8
}

foreach ($case in $plan) {
    $runName = 'game-probe-batch-' + $batchId + '-' + $case.Id
    $runDirectory = Join-Path $artifactPrefix $runName
    $hostDirectory = Join-Path $OutputDirectory ($case.Id + '-desktop')
    $record = [ordered]@{
        Id = $case.Id; Scenario = $case.Scenario; Seed = $case.Seed; Difficulty = $case.Difficulty
        Classification = 'prepare-error'; BattleStarted = $false; ValidBattle = $false; Deaths = 0
        HostExitCode = $null; ChildExitCode = $null; DesktopSafe = $false; Failure = $null
        RunDirectory = $runDirectory; HostDirectory = $hostDirectory; Result = $null
        InputPluginSha256 = $null; InputCoreSha256 = $null; ProbeSourceSha256 = $null; ProbePatcherSourceSha256 = $null
    }
    Write-Output ("Starting $($case.Id): $($case.Scenario), seed $($case.Seed), $($case.Difficulty) (one isolated process at a time)")
    try {
        foreach ($key in $inputPaths.Keys) {
            if ((Get-FileHash -LiteralPath (Join-Path $projectRoot $inputPaths[$key]) -Algorithm SHA256).Hash -cne $expectedHashes[$key]) {
                $record.Classification = 'build-changed'
                throw 'Build or harness changed during this batch; this case was not launched.'
            }
        }
        & (Join-Path $PSScriptRoot 'prepare-game-probe.ps1') -GameDirectory $GameDirectory -RunName $runName -Headless |
            Tee-Object -FilePath (Join-Path $OutputDirectory ($case.Id + '-prepare.log')) | Write-Output
        $manifest = Get-Content -LiteralPath (Join-Path $runDirectory 'probe-manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        foreach ($key in $expectedHashes.Keys) { $record[$key] = $manifest.$key }
        foreach ($key in $expectedHashes.Keys) { if ($manifest.$key -cne $expectedHashes[$key]) { $record.Classification = 'build-changed'; throw 'Prepared case differs from fixed batch provenance.' } }
        $arguments = @('-scenario', $case.Scenario, '-seed', [string]$case.Seed, '-difficulty', $case.Difficulty, '-maxticks', [string]$case.MaxTicks, '-wallseconds', [string]$case.WallSeconds)
        $record.Classification = 'host-error'
        try {
            & (Join-Path $PSScriptRoot 'start-isolated-test.ps1') -TargetExe (Join-Path $runDirectory 'Terraria.exe') -TargetArguments $arguments -TimeoutSeconds $TimeoutSeconds -OutputDirectory $hostDirectory |
                Tee-Object -FilePath (Join-Path $OutputDirectory ($case.Id + '-launch.log')) | Write-Output
        } catch { $record.Failure = $_.Exception.Message }

        $exitPath = Join-Path $hostDirectory 'desktop-exit.json'
        if (Test-Path -LiteralPath $exitPath -PathType Leaf) {
            $exitRecord = Get-Content -LiteralPath $exitPath -Raw -Encoding UTF8 | ConvertFrom-Json
            if ((Read-Field $exitRecord 'Schema') -cne 'chaite-desktop-exit/v1') { throw 'Invalid host-exit schema.' }
            $record.HostExitCode = Read-Field $exitRecord 'HostExitCode'
        }
        $hostLogPath = Join-Path $hostDirectory 'desktop-host.log'
        $hostLog = if (Test-Path -LiteralPath $hostLogPath -PathType Leaf) { Get-Content -LiteralPath $hostLogPath -Raw -Encoding UTF8 } else { '' }
        $record.DesktopSafe = $hostLog -match 'Input desktop unchanged; no test process took foreground;' -and $hostLog -notmatch '\bFAIL\b'
        $exitMatches = [regex]::Matches($hostLog, 'Child exit=(\d+);')
        if ($exitMatches.Count -eq 1) { $record.ChildExitCode = [long]::Parse($exitMatches[0].Groups[1].Value) }
        $gameLogPath = Join-Path $runDirectory 'game-probe.log'
        if (Test-Path -LiteralPath $gameLogPath -PathType Leaf) {
            $gameLog = Get-Content -LiteralPath $gameLogPath -Raw -Encoding UTF8
            $record.BattleStarted = $gameLog -match 'state=EngagedAlive|STATE EngagedAlive|bosses=[1-9]'
        }
        if ($record.HostExitCode -eq 124 -or $hostLog -match 'TIMEOUT:') { $record.Classification = 'host-timeout'; throw 'Isolated host timed out; this is not a completed valid combat observation.' }
        $record.Classification = 'invalid-evidence'
        $resultPath = Join-Path $runDirectory 'result.json'
        if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) { throw 'Missing result.json; no inferred win/loss.' }
        $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
        $record.Result = $result
        $status = Read-Field $result 'status'
        if ((Read-Field $result 'schema') -cne 'chaite-boss-result/v1' -or (Read-Field $result 'scenario') -cne $case.Scenario -or (Read-Field $result 'seed') -ne $case.Seed -or (Read-Field $result 'difficulty') -cne $case.Difficulty) { throw 'Result identity/schema does not match its case.' }
        if ($status -cnotin @('win', 'loss', 'timeout', 'rejected', 'harness-error')) { throw 'Unrecognized result status.' }
        $record.BattleStarted = $record.BattleStarted -or (Read-Field $result 'battleStarted' $false) -eq $true
        $record.Deaths = Require-Integer (Read-Field $result 'deaths') 0 2147483647 'result deaths'
        if (-not $record.DesktopSafe -or $null -eq $record.ChildExitCode -or $record.ChildExitCode -ne $record.HostExitCode) { throw 'Desktop safety or process-exit evidence is incomplete/inconsistent.' }
        $expectedExit = if ($status -eq 'win') { 0 } elseif ($status -eq 'harness-error') { 10 } else { 20 }
        if ($record.ChildExitCode -ne $expectedExit -or (Read-Field $result 'processExitCode') -ne $expectedExit) { throw 'Result status and actual process exit disagree.' }
        $validBattle = (Read-Field $result 'validBattle' $false) -eq $true
        $isWin = (Read-Field $result 'win' $false) -eq $true
        # A normal loss/timeout can occur while waiting for the boss. Retain it
        # as an attempted failure without inventing a valid started battle.
        if ($isWin -ne ($status -eq 'win') -or ($status -eq 'win' -and (-not $validBattle -or -not $record.BattleStarted)) -or ($validBattle -and -not $record.BattleStarted) -or ($status -in @('rejected', 'harness-error') -and $validBattle)) { throw 'Contradictory battle/status flags in result.' }
        if ($validBattle) {
            # Declared command-line difficulty/seed is not evidence that the
            # native world and its named RNG streams actually use those values.
            $mode = @('classic', 'expert', 'master').IndexOf($case.Difficulty)
            $native = Read-Field $result 'nativeDifficulty'
            $random = Read-Field $result 'battleRandom'
            # Validate JSON types before comparing: PowerShell otherwise accepts
            # strings such as "true" / "1" as genuine Boolean/integer evidence.
            if ((Require-Boolean (Read-Field $result 'nativeDifficultyVerified') 'nativeDifficultyVerified') -ne $true -or
                (Require-Integer (Read-Field $native 'gameMode') 0 2 'native gameMode') -ne $mode -or
                (Require-Integer (Read-Field $native 'worldFileGameMode') 0 2 'native worldFileGameMode') -ne $mode -or
                (Require-Integer (Read-Field $native 'difficulty') 1 3 'native difficulty') -ne ($mode + 1) -or
                (Require-Integer (Read-Field $native 'worldFileSeed') 0 2147483647 'native worldFileSeed') -ne $case.Seed -or
                (Require-Boolean (Read-Field $native 'expertMode') 'native expertMode') -ne ($mode -gt 0) -or
                (Require-Boolean (Read-Field $native 'masterMode') 'native masterMode') -ne ($mode -eq 2) -or
                (Require-Boolean (Read-Field $native 'hardMode') 'native hardMode') -ne ($case.Scenario -notin @('eye', 'king-slime')) -or
                (Require-Boolean (Read-Field $native 'forTheWorthy') 'native forTheWorthy') -ne $false) { throw 'Native difficulty/world seed evidence is missing or disagrees with the requested fixture.' }
            $nativeFrames = Require-Integer (Read-Field $result 'nativeFrames') 121 24000 'native frames'
            $fingerprint = @(Read-Field $random 'independentTwinFingerprint')
            if ((Require-Boolean (Read-Field $random 'installedAfterSetup') 'random installedAfterSetup') -ne $true -or
                (Require-Boolean (Read-Field $random 'actualAndNativeNamedColdStateVerified') 'random actualAndNativeNamedColdStateVerified') -ne $true -or
                (Require-Boolean (Read-Field $random 'actualStreamConsumedForFingerprint') 'random actualStreamConsumedForFingerprint') -ne $false -or
                (Require-Integer (Read-Field $random 'seed') 0 2147483647 'random seed') -ne $case.Seed -or
                (Require-Integer (Read-Field $random 'unpausedUpdateSeedInitial') 0 2147483647 'random unpausedUpdateSeedInitial') -ne $case.Seed -or
                (Require-Integer (Read-Field $random 'unpausedUpdateSeedAdvances') 0 24000 'random unpausedUpdateSeedAdvances') -ne $nativeFrames -or
                (Require-Integer (Read-Field $random 'referenceChecks') 0 2147483647 'random referenceChecks') -lt $nativeFrames -or
                $fingerprint.Count -ne 8) { throw 'Native battle random-stream verification is incomplete/inconsistent.' }
            foreach ($sample in $fingerprint) { $null = Require-Integer $sample 0 2147483647 'random fingerprint sample' }
            $observed = @(Read-Field $result 'firstObservedBosses')
            if ($observed.Count -eq 0) { throw 'No native Boss difficulty observations were supplied.' }
            foreach ($boss in $observed) {
                if ((Require-Integer (Read-Field $boss 'lifeMax') 1 2147483647 'Boss lifeMax') -le 0 -or
                    (Require-Integer (Read-Field $boss 'gameMode') 0 2 'Boss gameMode') -ne $mode -or
                    (Require-Integer (Read-Field $boss 'difficulty') 1 3 'Boss difficulty') -ne ($mode + 1) -or
                    (Require-Integer (Read-Field $boss 'npcDifficulty') 1 3 'Boss npcDifficulty') -ne ($mode + 1)) { throw 'Native spawned Boss difficulty is inconsistent with the case.' }
            }
        }
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

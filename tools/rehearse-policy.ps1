# Chaite mounted-rehearsal driver (rebuilt 2026-09-25).
#
# This is the acceptance-measurement entry point. It measures ONE configuration:
# the hand-written Duke Fishron wing movement script with NO learned residual,
# over N real fights inside the patched engine, and reports the master-difficulty
# win rate together with the equivalent proxy metric (share of fights with
# hits <= 2, the exact hit tolerance at 210 master damage against 480 player life).
#
# Call chain (nothing here launches the game itself):
#
#   tools\rehearse-policy.ps1
#     -> tools\prepare-game-probe.ps1 -Headless        (compiles the probe, patches
#        the vanilla exe copy, writes probe-manifest.json + static evidence)
#     -> tools\start-isolated-test.ps1 -TargetExe <prepared Terraria.exe>
#        -TargetArguments @(...) -OutputDirectory <rehearsal>\host
#        (validates the manifest, pins every file, then runs Chaite.DesktopHost.exe)
#     -> the probe appends one JSON line per finished fight to
#        <CHAITE_PROBE_OUT>.episodes.jsonl
#
# Denominator: GameProbe.Finish only records an episode when another one follows
# (`episodeLimit>0 && episodeIndex+1<episodeLimit`, tools\GameProbe.cs:5090), so a
# session of N fights writes exactly N-1 lines. 40 fights -> 39 lines.
#
# Controller wiring, and why it is what it is:
#   CHAITE_POLICY_FILE   = <repo>\policies\zero.policy.txt   (declared intent)
#   CHAITE_POLICY_ROUTES = <the CLI route name, e.g. fishron-strong-wing>
#   CHAITE_POLICY_FORMAT = CLEARED  (so ExportedPolicy.ResolveFormat() is Residual)
#
# The lost training\mount-policy.ps1 -UseZeroResidual used exactly these three
# values; artifacts\fishron-state-machine-report.md:14-16,191-202 records its gate
# line "Residual policy configured: ... routes=[fishron-strong-wing] format=residual".
# Two facts about the RECOVERED source make the real mechanism different from
# "the zero weights reproduce the script":
#
#   (a) policies\zero.policy.txt is stale. Its header declares 38 inputs and 1
#       hidden unit (63 tokens). LearnedPolicy.InputCount is 40 and HeadCount is
#       11 in this build, so LearnedPolicy.Load would throw InvalidDataException
#       ("declares 38 inputs ... but this build expects 40"), and
#       FishronWingScript.DecideMovement deliberately does NOT catch that.
#   (b) CHAITE_POLICY_ROUTES takes a FormulaRoute ENUM member to claim a route,
#       and ForRoute is `_routes.Contains(route.ToString())`. The CLI name
#       "fishron-strong-wing" is not the enum member "FishronStrongWingsDash", so
#       ForRoute returns null and the file is never read at all.
#
# Net effect: the script flies because NO policy owns the route. That is still
# exactly "the hand-written state machine with zero residual", which is the thing
# under measurement, but the driver asserts it instead of assuming it: it parses
# FormulaRouteCatalog.cs and LearnedPolicy.cs, refuses to launch if the CLI name
# would ever collide with an enum member (which would make the stale file load and
# throw mid-fight), and records in summary.json that the residual was provably
# never evaluated.
#
# Fail-closed: missing episodes file, a line count other than Episodes-1, a
# malformed row, no plugin-init evidence, a probe compile/launch rejection marker,
# a faulted session, an empty spin, or a leftover game process all produce a
# non-zero exit code. A sub-bar WIN RATE does not: it is a measurement, not an
# evidence failure, and it is reported loudly as BAR lines plus summary.json.
#
# ASCII ONLY. PowerShell 5.1 mis-parses a Chinese .ps1 when run with -File.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('fishron-strong-wing', 'fishron-fairy-wing')]
    [string]$Route,

    [ValidateSet('classic', 'expert', 'master')]
    [string]$Difficulty = 'master',

    # Fights requested. The episodes file gets Episodes-1 lines (see header).
    [ValidateRange(2, 400)]
    [int]$Episodes = 40,

    [string]$Tag,

    # Skip both the staleness check and the build step.
    [switch]$SkipBuild,

    # 0 derives WallSeconds + 300, clamped to the host's 120..86400 range.
    [ValidateRange(0, 86400)]
    [int]$TimeoutSeconds = 0,

    [string]$GameDirectory = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria',

    # Wall budget for the whole session, in seconds. The probe ends the run
    # itself when it expires (Finish "test-time-limit" per episode).
    [ValidateRange(15, 86400)]
    [int]$WallSeconds = 5400,

    # Per-episode native tick ceiling (-maxticks).
    [ValidateRange(600, 24000)]
    [int]$MaxTicks = 24000,

    [ValidateRange(120, 23880)]
    [int]$TakeoverTick = 120,

    [ValidateRange(0, 2147483647)]
    [int]$Seed = 20260910,

    # Simulated player DPS. MUST be pinned: with CHAITE_SIM_DPS unset the probe
    # rolls a fresh 600..1200 per episode (GameProbe.cs:4099-4100), so the kill
    # clock -- and therefore the win rate -- would not be comparable between runs.
    # 1200 is the value recorded for the fsw121d arm in training\sessions.json.
    [ValidateRange(1, 100000)]
    [int]$SimDps = 1200,

    # Process-wide native tick budget (CHAITE_RUN_MAX_TICKS, 1000..2000000). Kept
    # below the ~661k tick 32-bit address-space ceiling and far above a realistic
    # 40-fight need (about 240k at 6k ticks per fight). Reaching it makes the probe
    # write the remaining episodes in a rapid-fire spin, which the empty-spin gate
    # below detects.
    [ValidateRange(1000, 2000000)]
    [int]$RunMaxTicks = 600000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$script:LogPath = $null

function Write-DriverLog {
    param([string]$Message, [string]$Level = 'INFO')
    $stamp = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffK')
    $line = $stamp + ' ' + $Level.PadRight(5) + ' ' + $Message
    Write-Host $line
    if (-not [string]::IsNullOrEmpty($script:LogPath)) {
        Add-Content -LiteralPath $script:LogPath -Value $line -Encoding UTF8
    }
}

function Write-Utf8File([string]$Path, [string]$Text) {
    $encoding = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($Path, $Text, $encoding)
}

function Write-JsonFile([string]$Path, $Value, [int]$Depth) {
    $json = $Value | ConvertTo-Json -Depth $Depth
    Write-Utf8File $Path ($json + [Environment]::NewLine)
}

function Read-JsonObjectFile([string]$Path, [string]$Name) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { throw "Missing $Name file: $Path" }
    $raw = Get-Content -LiteralPath $Path -Raw -Encoding UTF8
    try { $value = ConvertFrom-Json -InputObject $raw }
    catch { throw "$Name is not valid JSON: $Path" }
    if ($null -eq $value -or $value -isnot [pscustomobject]) { throw "$Name is not one JSON object: $Path" }
    return $value
}

# Strict JSON field readers. A status-like field arriving as a STRING is exactly
# how this project once lost 1862 onset matches to a silent no-op (`'0' in (0,5)`
# is False in PowerShell), so a string is rejected here rather than coerced.
function Get-RequiredNumber($Object, [string]$Field, [string]$Where) {
    $prop = $Object.PSObject.Properties[$Field]
    if ($null -eq $prop) { throw "$Where lacks the '$Field' field" }
    $value = $prop.Value
    if ($null -eq $value) { throw "$Where field '$Field' is null" }
    if ($value -is [string] -or $value -is [bool]) { throw "$Where field '$Field' is not a JSON number" }
    if (-not ($value -is [int] -or $value -is [long] -or $value -is [double] -or
              $value -is [single] -or $value -is [decimal])) {
        throw "$Where field '$Field' is not a JSON number"
    }
    $number = [double]$value
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) {
        throw "$Where field '$Field' is not finite"
    }
    return $number
}

function Get-RequiredBoolean($Object, [string]$Field, [string]$Where) {
    $prop = $Object.PSObject.Properties[$Field]
    if ($null -eq $prop) { throw "$Where lacks the '$Field' field" }
    if ($prop.Value -isnot [bool]) { throw "$Where field '$Field' is not a JSON boolean" }
    return [bool]$prop.Value
}

function Get-RequiredString($Object, [string]$Field, [string]$Where) {
    $prop = $Object.PSObject.Properties[$Field]
    if ($null -eq $prop) { throw "$Where lacks the '$Field' field" }
    if ($prop.Value -isnot [string]) { throw "$Where field '$Field' is not a JSON string" }
    return [string]$prop.Value
}

function Get-FileText([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8)
}

function Get-MatchCount([string]$Text, [string]$Pattern) {
    if ([string]::IsNullOrEmpty($Text)) { return 0 }
    return ([regex]::Matches($Text, $Pattern)).Count
}

# Runs a child .ps1 with a relaxed error preference, merging every stream into
# the log, and returns $null on success or the terminating error message. The
# preference is restored here so a benign stderr line in a native tool cannot be
# promoted into a failure by this script's own -Stop preference.
function Invoke-ChildScript([string]$ScriptPath, [hashtable]$Arguments, [string]$LogPath) {
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & $ScriptPath @Arguments 2>&1 | Tee-Object -FilePath $LogPath | Out-Null
        return $null
    } catch {
        return $_.Exception.Message
    } finally {
        $ErrorActionPreference = $previous
    }
}

function Get-NewestSourceWriteTime([string[]]$Roots) {
    $newest = [DateTime]::MinValue
    $found = $false
    foreach ($root in $Roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File -Filter '*.cs') {
            if ($file.FullName -match '\\obj\\' -or $file.FullName -match '\\bin\\') { continue }
            $found = $true
            if ($file.LastWriteTime -gt $newest) { $newest = $file.LastWriteTime }
        }
    }
    if (-not $found) { return $null }
    return $newest
}

# Reads the shapes the residual loader will enforce, straight out of the source
# that ships with the run, so this check cannot drift from the build.
function Get-LearnedPolicyShape([string]$CoreRoot) {
    $path = Join-Path $CoreRoot 'LearnedPolicy.cs'
    $text = Get-FileText $path
    if ([string]::IsNullOrEmpty($text)) { throw "Missing LearnedPolicy source: $path" }
    $inputs = [regex]::Match($text, 'public\s+const\s+int\s+InputCount\s*=\s*([0-9]+)\s*;')
    $heads = [regex]::Match($text, 'public\s+const\s+int\s+HeadCount\s*=\s*([0-9]+)\s*;')
    if (-not $inputs.Success -or -not $heads.Success) {
        throw "Could not read InputCount/HeadCount out of $path"
    }
    return [pscustomobject]@{
        Path = $path
        InputCount = [int]$inputs.Groups[1].Value
        HeadCount = [int]$heads.Groups[1].Value
    }
}

function Get-FormulaRouteEnumMembers([string]$CoreRoot) {
    $path = Join-Path $CoreRoot 'FormulaRouteCatalog.cs'
    $text = Get-FileText $path
    if ([string]::IsNullOrEmpty($text)) { throw "Missing FormulaRouteCatalog source: $path" }
    $match = [regex]::Match($text, 'enum\s+FormulaRoute\s*\{(?<body>[^}]*)\}')
    if (-not $match.Success) { throw "Could not find 'enum FormulaRoute' in $path" }
    $members = New-Object System.Collections.Generic.List[string]
    foreach ($piece in $match.Groups['body'].Value -split ',') {
        $name = ($piece -replace '//.*$', '').Trim()
        if ($name.Length -eq 0) { continue }
        foreach ($token in ($name -split '\s+')) {
            if ($token -match '^[A-Za-z_][A-Za-z0-9_]*$') { [void]$members.Add($token) }
        }
    }
    if ($members.Count -eq 0) { throw "Parsed no members out of enum FormulaRoute in $path" }
    return $members.ToArray()
}

function Get-ProcessResidue {
    $names = @('Terraria', 'Chaite.DesktopHost')
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($name in $names) {
        foreach ($process in @(Get-Process -Name $name -ErrorAction SilentlyContinue)) {
            $rows.Add([pscustomobject]@{ Name = $name; Id = $process.Id; Started = $process.StartTime })
        }
    }
    return $rows.ToArray()
}

# ---------------------------------------------------------------------------
# 0. Identity and output layout
# ---------------------------------------------------------------------------
$root = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$artifactsRoot = Join-Path $root 'artifacts'

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = $Route + '-' + $Difficulty + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')
}
if ($Tag -notmatch '^[a-zA-Z0-9][a-zA-Z0-9-]{0,80}$') {
    throw "Tag must match ^[a-zA-Z0-9][a-zA-Z0-9-]{0,80}$ (letters, digits and dashes only): '$Tag'"
}
if ($TimeoutSeconds -eq 0) {
    $TimeoutSeconds = [Math]::Min(86400, $WallSeconds + 300)
}
if ($TimeoutSeconds -lt 120 -or $TimeoutSeconds -le $WallSeconds) {
    throw "-TimeoutSeconds must be at least 120 and greater than -WallSeconds ($WallSeconds); got $TimeoutSeconds"
}

$rehearsalDir = Join-Path $artifactsRoot ('rehearsal-' + $Tag)
$hostDir = Join-Path $rehearsalDir 'host'
$episodeBase = Join-Path $rehearsalDir $Tag
$episodePath = $episodeBase + '.episodes.jsonl'
$runName = 'game-probe-rehearsal-' + $Tag
$runDir = Join-Path $artifactsRoot $runName

if (Test-Path -LiteralPath $rehearsalDir) {
    throw "Rehearsal evidence already exists and is never overwritten: $rehearsalDir (choose a new -Tag)"
}
if (Test-Path -LiteralPath $runDir) {
    throw "Prepared run directory already exists; prepare a fresh RunName: $runDir"
}
New-Item -ItemType Directory -Path $rehearsalDir | Out-Null
$script:LogPath = Join-Path $rehearsalDir 'driver.log'

$zeroPolicyPath = Join-Path $root 'policies\zero.policy.txt'
# Duke Fishron (NPC 370) lifeMax by difficulty: 60000 classic, x1.3 expert,
# x1.275 master. Used only to print the damage context; never a gate.
$bossLifeMax = switch ($Difficulty) { 'classic' { 60000 } 'expert' { 78000 } 'master' { 99450 } }
$expectedEpisodesFileLines = $Episodes - 1
$exitCode = 0
$gateFailures = New-Object System.Collections.Generic.List[string]
$launchError = $null
$prepareError = $null
$buildPerformed = $false
$buildSkippedReason = $null
$envSnapshot = @{}
$envNames = @(
    'CHAITE_EPISODES', 'CHAITE_PROBE_OUT', 'CHAITE_POLICY_FILE', 'CHAITE_POLICY_ROUTES',
    'CHAITE_POLICY_FORMAT', 'CHAITE_BRIDGE_FILE', 'CHAITE_ROUTE_FILE', 'CHAITE_ROUTE_SKIP',
    'CHAITE_RUN_MAX_TICKS', 'CHAITE_SIM_DPS', 'CHAITE_SIM_DPS_FULL_TILES',
    'CHAITE_SIM_DPS_ZERO_TILES', 'CHAITE_PROJ_SLOTS', 'CHAITE_PROJ_SORT',
    'CHAITE_PROJ_COLLAPSE', 'CHAITE_OBS_AGG', 'CHAITE_OBS_BOSS_PROX',
    'CHAITE_OBS_WORLD_BOUND', 'CHAITE_DEFAULT_ACTION_MARGIN', 'CHAITE_TRACE_FILE'
)

Write-DriverLog ("rehearsal dir    : " + $rehearsalDir)
Write-DriverLog ("route            : " + $Route)
Write-DriverLog ("difficulty       : " + $Difficulty)
Write-DriverLog ("episodes         : requested=" + $Episodes + " expected episodes lines=" + $expectedEpisodesFileLines)
Write-DriverLog ("seed / maxTicks  : " + $Seed + " / " + $MaxTicks)
Write-DriverLog ("wall / timeout   : " + $WallSeconds + "s / " + $TimeoutSeconds + "s")
Write-DriverLog ("simDps           : " + $SimDps + " (pinned; unset would randomise 600..1200 per fight)")
Write-DriverLog ("runMaxTicks      : " + $RunMaxTicks)
Write-DriverLog ("episodes base    : " + $episodeBase)

try {
    # -----------------------------------------------------------------------
    # 1. Preflight: the residual configuration must be provably inert.
    # -----------------------------------------------------------------------
    if (-not (Test-Path -LiteralPath $zeroPolicyPath -PathType Leaf)) {
        throw "Missing residual policy file: $zeroPolicyPath"
    }
    $coreRoot = Join-Path $root 'src\Chaite.Core'
    $shape = Get-LearnedPolicyShape $coreRoot
    $enumMembers = @(Get-FormulaRouteEnumMembers $coreRoot)

    $isEnumMember = $false
    foreach ($member in $enumMembers) { if ($member -ceq $Route) { $isEnumMember = $true } }
    Write-DriverLog ("FormulaRoute members: " + ($enumMembers -join ', '))
    if ($isEnumMember) {
        throw ("CHAITE_POLICY_ROUTES='" + $Route + "' IS a FormulaRoute enum member on this build, " +
            "so LearnedPolicy.ForRoute would claim the route and Load the residual file. " +
            "policies\zero.policy.txt is stale and Load would throw mid-fight, and the run would " +
            "no longer be the pure hand-written state machine. Refusing to launch.")
    }
    Write-DriverLog ("residual route name '" + $Route + "' is NOT a FormulaRoute member: ForRoute returns null, " +
        "so the residual file is never read and no policy exception can occur.")

    $zeroTokens = @(((Get-Content -LiteralPath $zeroPolicyPath -Raw -Encoding UTF8) -split '\s+') |
        Where-Object { $_ -ne '' })
    $zeroHeaderInputs = -1
    $zeroHeaderHidden = -1
    if ($zeroTokens.Count -ge 4) {
        $parsedInputs = 0
        $parsedHidden = 0
        if ([int]::TryParse($zeroTokens[2], [ref]$parsedInputs)) { $zeroHeaderInputs = $parsedInputs }
        if ([int]::TryParse($zeroTokens[3], [ref]$parsedHidden)) { $zeroHeaderHidden = $parsedHidden }
    }
    $zeroExpectedTokens = $null
    if ($zeroHeaderHidden -ge 0) {
        $zeroExpectedTokens = 4 + $zeroHeaderHidden * $shape.InputCount + $zeroHeaderHidden +
            $shape.HeadCount * $zeroHeaderHidden + $shape.HeadCount
    }
    $zeroLoadable = ($zeroTokens.Count -ge 4) -and ($zeroTokens[0] -ceq 'chaite-policy') -and
        ($zeroTokens[1] -ceq '1') -and ($zeroHeaderInputs -eq $shape.InputCount) -and
        ($zeroHeaderHidden -ge 1) -and ($zeroHeaderHidden -le 64) -and
        ($zeroTokens.Count -eq $zeroExpectedTokens)
    $zeroPolicySha = (Get-FileHash -LiteralPath $zeroPolicyPath -Algorithm SHA256).Hash
    Write-DriverLog ("residual file    : " + $zeroPolicyPath)
    Write-DriverLog ("residual sha256  : " + $zeroPolicySha)
    $headerInputsText = if ($zeroHeaderInputs -ge 0) { $zeroHeaderInputs.ToString([Globalization.CultureInfo]::InvariantCulture) } else { 'unreadable' }
    $headerHiddenText = if ($zeroHeaderHidden -ge 0) { $zeroHeaderHidden.ToString([Globalization.CultureInfo]::InvariantCulture) } else { 'unreadable' }
    Write-DriverLog ("residual header  : tokens=" + $zeroTokens.Count + " inputs=" + $headerInputsText +
        " hidden=" + $headerHiddenText + "; LearnedPolicy expects inputs=" + $shape.InputCount +
        " heads=" + $shape.HeadCount + " -> " + $zeroExpectedTokens + " tokens")
    if ($zeroLoadable) {
        Write-DriverLog ("residual file IS loadable by this build, but it is still never read for this route.")
    } else {
        Write-DriverLog ("residual file is STALE and NOT loadable by this build (LearnedPolicy.Load would throw); " +
            "it is inert only because the route is unowned. Measured controller = hand-written script.") 'WARN'
    }

    # -----------------------------------------------------------------------
    # 2. Build freshness. Never rebuild silently, never measure a stale binary.
    # -----------------------------------------------------------------------
    $pluginDll = Join-Path $root 'src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll'
    $coreDll = Join-Path $root 'src\Chaite.Core\bin\Release\net48\Chaite.Core.dll'
    $patcherExe = Join-Path $root 'src\Chaite.Patcher\bin\Release\net48\Chaite.Patcher.exe'
    $cecilDll = Join-Path $root 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'
    $pluginConfig = Join-Path $root 'src\Chaite.Plugin\config.json'
    foreach ($required in @($pluginDll, $coreDll, $patcherExe, $cecilDll, $pluginConfig)) {
        if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
            throw "Missing build input required by prepare-game-probe.ps1: $required"
        }
    }
    if ($SkipBuild) {
        $buildSkippedReason = '-SkipBuild'
        Write-DriverLog 'build            : skipped (-SkipBuild)'
    } else {
        $newestSource = Get-NewestSourceWriteTime @((Join-Path $root 'src\Chaite.Core'), (Join-Path $root 'src\Chaite.Plugin'))
        $oldestBinary = (Get-Item -LiteralPath $pluginDll).LastWriteTime
        if ((Get-Item -LiteralPath $coreDll).LastWriteTime -lt $oldestBinary) {
            $oldestBinary = (Get-Item -LiteralPath $coreDll).LastWriteTime
        }
        if ($null -ne $newestSource -and $newestSource -gt $oldestBinary) {
            Write-DriverLog ("build            : sources are newer (" + $newestSource.ToString('o') +
                ") than the assemblies (" + $oldestBinary.ToString('o') + "); building") 'WARN'
            & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release
            if ($LASTEXITCODE -ne 0) { throw "tools\build.ps1 failed with exit code $LASTEXITCODE" }
            $buildPerformed = $true
        } else {
            $buildSkippedReason = 'assemblies are newer than every Core/Plugin source file'
            Write-DriverLog ("build            : not needed, " + $buildSkippedReason)
        }
    }
    Write-DriverLog ("core sha256      : " + (Get-FileHash -LiteralPath $coreDll -Algorithm SHA256).Hash)
    Write-DriverLog ("plugin sha256    : " + (Get-FileHash -LiteralPath $pluginDll -Algorithm SHA256).Hash)

    $residueBefore = @(Get-ProcessResidue)
    if ($residueBefore.Count -ne 0) {
        throw ("A game or host process is already running; only one game instance is allowed at a time: " +
            (($residueBefore | ForEach-Object { $_.Name + '#' + $_.Id }) -join ', '))
    }

    # -----------------------------------------------------------------------
    # 3. Environment. Saved, replaced, and restored in finally.
    # -----------------------------------------------------------------------
    foreach ($name in $envNames) {
        $envSnapshot[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
        [Environment]::SetEnvironmentVariable($name, $null, 'Process')
    }
    [Environment]::SetEnvironmentVariable('CHAITE_EPISODES', $Episodes.ToString([Globalization.CultureInfo]::InvariantCulture), 'Process')
    [Environment]::SetEnvironmentVariable('CHAITE_PROBE_OUT', $episodeBase, 'Process')
    [Environment]::SetEnvironmentVariable('CHAITE_POLICY_FILE', $zeroPolicyPath, 'Process')
    [Environment]::SetEnvironmentVariable('CHAITE_POLICY_ROUTES', $Route, 'Process')
    [Environment]::SetEnvironmentVariable('CHAITE_RUN_MAX_TICKS', $RunMaxTicks.ToString([Globalization.CultureInfo]::InvariantCulture), 'Process')
    [Environment]::SetEnvironmentVariable('CHAITE_SIM_DPS', $SimDps.ToString([Globalization.CultureInfo]::InvariantCulture), 'Process')
    Write-DriverLog 'environment      : cleared every CHAITE_* the probe or plugin reads, then set:'
    foreach ($name in @('CHAITE_EPISODES', 'CHAITE_PROBE_OUT', 'CHAITE_POLICY_FILE', 'CHAITE_POLICY_ROUTES',
        'CHAITE_RUN_MAX_TICKS', 'CHAITE_SIM_DPS')) {
        Write-DriverLog ('  ' + $name + '=' + [Environment]::GetEnvironmentVariable($name, 'Process'))
    }
    Write-DriverLog ('  CHAITE_POLICY_FORMAT=<cleared> -> ExportedPolicy.ResolveFormat() = Residual')
    Write-DriverLog ('  CHAITE_BRIDGE_FILE=<cleared> -> bridgeDriven=false, no trainer lockstep, no replay owns movement')

    # -----------------------------------------------------------------------
    # 4. Prepare then launch, through the existing pinned path only.
    # -----------------------------------------------------------------------
    $prepareLog = Join-Path $rehearsalDir 'prepare.log'
    Write-DriverLog ("prepare          : prepare-game-probe.ps1 -RunName " + $runName + " -Headless")
    $prepareError = Invoke-ChildScript -ScriptPath (Join-Path $PSScriptRoot 'prepare-game-probe.ps1') `
        -Arguments @{ GameDirectory = $GameDirectory; RunName = $runName; Headless = $true } -LogPath $prepareLog
    if ($null -ne $prepareError) {
        Write-DriverLog ("prepare FAILED: " + $prepareError) 'ERROR'
        throw ("Probe preparation failed; no game was launched. See " + $prepareLog + " :: " + $prepareError)
    }
    $preparedExe = Join-Path $runDir 'Terraria.exe'
    if (-not (Test-Path -LiteralPath $preparedExe -PathType Leaf)) {
        throw "prepare-game-probe.ps1 reported success but produced no prepared exe: $preparedExe"
    }
    Write-DriverLog 'prepare          : OK'

    $targetArguments = @(
        '-scenario', 'duke-fishron',
        '-phase', 'monitor',
        '-formularoute', $Route,
        '-takeovertick', $TakeoverTick.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-seed', $Seed.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-difficulty', $Difficulty,
        '-maxticks', $MaxTicks.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-wallseconds', $WallSeconds.ToString([Globalization.CultureInfo]::InvariantCulture),
        '-skipbeam')
    $launchLog = Join-Path $rehearsalDir 'launch.log'
    Write-DriverLog ("launch           : start-isolated-test.ps1 -TargetArguments " + ($targetArguments -join ' '))
    $launchStarted = Get-Date
    $launchError = Invoke-ChildScript -ScriptPath (Join-Path $PSScriptRoot 'start-isolated-test.ps1') `
        -Arguments @{
            TargetExe = $preparedExe
            TargetArguments = $targetArguments
            TimeoutSeconds = $TimeoutSeconds
            OutputDirectory = $hostDir
        } -LogPath $launchLog
    $launchSeconds = [Math]::Round(((Get-Date) - $launchStarted).TotalSeconds, 1)
    if ($null -ne $launchError) {
        # A rehearsal that loses its last fight ends with a non-zero child exit
        # code by design, so a throw here is recorded and the evidence below
        # decides whether the measurement is usable.
        Write-DriverLog ("launch reported     : " + $launchError) 'WARN'
    }
    Write-DriverLog ("launch finished : " + $launchSeconds + "s wall")

    # -----------------------------------------------------------------------
    # 5. Collect every artifact. Failed data is preserved, never deleted.
    # -----------------------------------------------------------------------
    $gameProbeLogPath = Join-Path $runDir 'game-probe.log'
    $pluginLogPath = Join-Path $runDir 'Chaite\chaite.log'
    $resultPath = Join-Path $runDir 'result.json'
    $manifestPath = Join-Path $runDir 'probe-manifest.json'
    $desktopExitPath = Join-Path $hostDir 'desktop-exit.json'
    $desktopHostLogPath = Join-Path $hostDir 'desktop-host.log'
    $gameProbeLog = Get-FileText $gameProbeLogPath
    $pluginLog = Get-FileText $pluginLogPath
    $desktopHostLog = Get-FileText $desktopHostLogPath
    $desktopExit = $null
    if (Test-Path -LiteralPath $desktopExitPath -PathType Leaf) {
        try { $desktopExit = Read-JsonObjectFile $desktopExitPath 'desktop exit evidence' } catch { $desktopExit = $null }
    }

    # -----------------------------------------------------------------------
    # 6. Gates. Each one fails closed with a named reason.
    # -----------------------------------------------------------------------
    $episodeLines = @()
    if (-not (Test-Path -LiteralPath $episodePath -PathType Leaf)) {
        $gateFailures.Add("episodes file was never written: $episodePath")
    } else {
        $episodeLines = @(Get-Content -LiteralPath $episodePath -Encoding UTF8 | Where-Object { $_.Trim().Length -ne 0 })
        if ($episodeLines.Count -ne $expectedEpisodesFileLines) {
            $gateFailures.Add("episodes file has " + $episodeLines.Count + " lines but Episodes-1 = " +
                $expectedEpisodesFileLines + " was required (the probe wrote one line per finished fight that had a successor)")
        }
    }
    if ([string]::IsNullOrEmpty($gameProbeLog)) {
        $gateFailures.Add("game-probe.log is missing or empty: $gameProbeLogPath")
    } else {
        if ($gameProbeLog -match 'Probe compile failed') {
            $gateFailures.Add('game-probe.log contains "Probe compile failed"')
        }
        if ($gameProbeLog -match 'LAUNCH_REJECTED') {
            $gateFailures.Add('game-probe.log contains LAUNCH_REJECTED')
        }
        if ($gameProbeLog -match 'state=Faulted' -or $gameProbeLog -match 'STATE Faulted') {
            $gateFailures.Add('the plugin session entered the Faulted state')
        }
        $resetCount = Get-MatchCount $gameProbeLog 'BRIDGE_EPISODE_RESET'
        if ($resetCount -lt $expectedEpisodesFileLines -or $resetCount -lt 1) {
            $gateFailures.Add("game-probe.log has $resetCount BRIDGE_EPISODE_RESET lines; at least $expectedEpisodesFileLines were required")
        }
        if ((Get-MatchCount $gameProbeLog 'FINISH ') -lt 1) {
            $gateFailures.Add('game-probe.log has no FINISH line')
        }
    }
    if ([string]::IsNullOrEmpty($pluginLog)) {
        $gateFailures.Add("plugin log is missing or empty: $pluginLogPath")
    } else {
        if ((Get-MatchCount $pluginLog 'Initialized against Terraria') -lt 1) {
            $gateFailures.Add('plugin log has no "Initialized against Terraria" line (the plugin never initialized)')
        }
        if ((Get-MatchCount $pluginLog 'Exported policy loaded:') -gt 0) {
            $gateFailures.Add('plugin log shows "Exported policy loaded:", so an exported policy replaced the hand-written script')
        }
    }
    if ($null -eq $desktopExit) {
        $gateFailures.Add("missing or unreadable desktop exit evidence: $desktopExitPath")
    } else {
        try {
            $hostExitCode = Get-RequiredNumber $desktopExit 'HostExitCode' 'desktop exit evidence'
            if ([int]$hostExitCode -eq 124) {
                $gateFailures.Add('the isolated host timed out (HostExitCode 124); the session was cut short')
            }
        } catch {
            $gateFailures.Add('desktop exit evidence is unusable: ' + $_.Exception.Message)
        }
    }
    if ([string]::IsNullOrEmpty($desktopHostLog)) {
        $gateFailures.Add("host log is missing or empty: $desktopHostLogPath")
    } else {
        if ($desktopHostLog -match 'TIMEOUT:') { $gateFailures.Add('desktop-host.log contains TIMEOUT:') }
        if ($desktopHostLog -match '\bFAIL\b') { $gateFailures.Add('desktop-host.log contains a FAIL line') }
    }

    $rows = New-Object System.Collections.Generic.List[object]
    for ($index = 0; $index -lt $episodeLines.Count; $index++) {
        $where = 'episodes line ' + ($index + 1)
        $parsed = $null
        try { $parsed = ConvertFrom-Json -InputObject $episodeLines[$index] }
        catch { $gateFailures.Add("$where is not valid JSON"); continue }
        if ($null -eq $parsed -or $parsed -isnot [pscustomobject]) {
            $gateFailures.Add("$where is not one JSON object"); continue
        }
        try {
            $rows.Add([pscustomobject]@{
                E = [int](Get-RequiredNumber $parsed 'e' $where)
                Outcome = Get-RequiredString $parsed 'outcome' $where
                Win = Get-RequiredBoolean $parsed 'win' $where
                Hits = [int](Get-RequiredNumber $parsed 'hits' $where)
                Deaths = [int](Get-RequiredNumber $parsed 'deaths' $where)
                Ticks = [int](Get-RequiredNumber $parsed 'ticks' $where)
                BossDamage = Get-RequiredNumber $parsed 'bossDamage' $where
                BossLifeRemaining = Get-RequiredNumber $parsed 'bossLifeRemaining' $where
                SimulatedDps = Get-RequiredNumber $parsed 'simulatedDps' $where
                ElapsedWallMs = Get-RequiredNumber $parsed 'elapsedWallMs' $where
            })
        } catch {
            $gateFailures.Add("$where is unusable: " + $_.Exception.Message)
        }
    }
    if ($rows.Count -eq $episodeLines.Count -and $rows.Count -gt 0) {
        for ($index = 0; $index -lt $rows.Count; $index++) {
            if ($rows[$index].E -ne $index) {
                $gateFailures.Add("episodes row " + $index + " carries e=" + $rows[$index].E + "; rows must be e=0..N-1 in order")
                break
            }
        }
    }

    $residueAfter = @(Get-ProcessResidue)
    if ($residueAfter.Count -ne 0) {
        $gateFailures.Add('a game or host process survived the run: ' +
            (($residueAfter | ForEach-Object { $_.Name + '#' + $_.Id }) -join ', '))
    }

    # -----------------------------------------------------------------------
    # 7. Metrics
    # -----------------------------------------------------------------------
    $metrics = $null
    if ($rows.Count -gt 0) {
        $wins = 0; $hitsLe2 = 0; $totalHits = 0; $totalTicks = 0; $totalBossDamage = 0.0
        $histogram = @{}
        $outcomes = @{}
        $dpsValues = @{}
        $ticksList = New-Object System.Collections.Generic.List[int]
        $rowsUnder300 = 0
        foreach ($row in $rows) {
            if ($row.Win) { $wins++ }
            if ($row.Hits -le 2) { $hitsLe2++ }
            $totalHits += $row.Hits
            $totalTicks += $row.Ticks
            $totalBossDamage += $row.BossDamage
            $ticksList.Add($row.Ticks)
            if ($row.Ticks -lt 300) { $rowsUnder300++ }
            $key = $row.Hits.ToString([Globalization.CultureInfo]::InvariantCulture)
            if ($histogram.ContainsKey($key)) { $histogram[$key]++ } else { $histogram[$key] = 1 }
            if ($outcomes.ContainsKey($row.Outcome)) { $outcomes[$row.Outcome]++ } else { $outcomes[$row.Outcome] = 1 }
            $dpsKey = $row.SimulatedDps.ToString('R', [Globalization.CultureInfo]::InvariantCulture)
            if ($dpsValues.ContainsKey($dpsKey)) { $dpsValues[$dpsKey]++ } else { $dpsValues[$dpsKey] = 1 }
        }
        $sorted = $ticksList.ToArray()
        [Array]::Sort($sorted)
        $count = $sorted.Length
        $medianTicks = if ($count % 2 -eq 1) { [double]$sorted[[int](($count - 1) / 2)] }
            else { ([double]$sorted[$count / 2 - 1] + [double]$sorted[$count / 2]) / 2.0 }
        # The historical training\mount-policy.ps1 used [int](count/2) on the sorted
        # array (index 20 for 39 rows), i.e. an UPPER median. Reported next to the
        # statistical median so the two conventions cannot be confused again.
        $upperMedianTicks = [double]$sorted[[int]($count / 2)]
        $maxTicksSeen = $sorted[$count - 1]
        $metrics = [pscustomobject]@{
            Rows = $count
            Wins = $wins
            WinRate = [Math]::Round(100.0 * $wins / $count, 2)
            HitsLe2 = $hitsLe2
            HitsLe2Rate = [Math]::Round(100.0 * $hitsLe2 / $count, 2)
            HitsHistogram = $histogram
            Outcomes = $outcomes
            SimulatedDpsValues = $dpsValues
            TotalHits = $totalHits
            TotalTicks = $totalTicks
            HitsPer1kTick = if ($totalTicks -gt 0) { [Math]::Round(1000.0 * $totalHits / $totalTicks, 3) } else { 0 }
            MedianTicks = [Math]::Round($medianTicks, 1)
            MedianTicksUpperIndexConvention = $upperMedianTicks
            MinTicks = $sorted[0]
            MaxTicks = $maxTicksSeen
            RowsUnder300Ticks = $rowsUnder300
            MeanBossDamage = [Math]::Round($totalBossDamage / $count, 1)
            MaxBossDamage = [Math]::Round((($rows | ForEach-Object { $_.BossDamage }) | Measure-Object -Maximum).Maximum, 1)
            MaxBossLifeRemaining = [Math]::Round((($rows | ForEach-Object { $_.BossLifeRemaining }) | Measure-Object -Maximum).Maximum, 1)
        }

        # Empty-spin gate. If CHAITE_RUN_MAX_TICKS is reached mid-session, Finish
        # appends a row and resets, and the very next tick reaches the limit
        # again, so the rest of the session writes near-zero-tick rows in seconds.
        if ($metrics.MaxTicks -lt 600) {
            $gateFailures.Add('every episode ended before 600 ticks (max ' + $metrics.MaxTicks + '): the session did not fight')
        }
        if ($metrics.RowsUnder300Ticks * 10 -gt $metrics.Rows) {
            $gateFailures.Add($metrics.RowsUnder300Ticks.ToString([Globalization.CultureInfo]::InvariantCulture) +
                ' of ' + $metrics.Rows + ' episodes ended before 300 ticks; that is the CHAITE_RUN_MAX_TICKS / compile-failure spin signature')
        }
    }

    # The final fight is deliberately never written to the episodes file; its
    # outcome only exists in result.json. Reported as context, never counted.
    $finalEpisode = $null
    if (Test-Path -LiteralPath $resultPath -PathType Leaf) {
        try {
            $result = Read-JsonObjectFile $resultPath 'probe result'
            $sessionDamage = $null
            $sessionDamageProperty = $result.PSObject.Properties['bossDamage']
            if ($null -ne $sessionDamageProperty -and $sessionDamageProperty.Value -isnot [string] -and
                $null -ne $sessionDamageProperty.Value) {
                $sessionDamage = [double]$sessionDamageProperty.Value
            }
            $finalEpisode = [pscustomobject]@{
                Status = $result.status
                Outcome = $result.outcome
                Win = $result.win
                Hits = $result.hits
                Ticks = $result.ticks
                # result.json's bossDamage is the SESSION cumulative total, not the
                # final fight's damage: only the episode rows subtract their own
                # baseline (episodeBossDamageStart). Both numbers are reported and
                # the derived per-fight value is labelled as derived.
                SessionBossDamageCumulative = $sessionDamage
            }
            $priorDamage = 0.0
            foreach ($row in $rows) { $priorDamage += $row.BossDamage }
            $derivedDamage = $null
            if ($null -ne $sessionDamage) { $derivedDamage = [Math]::Round($sessionDamage - $priorDamage, 1) }
            $finalEpisode | Add-Member -NotePropertyName PriorEpisodesBossDamage -NotePropertyValue ([Math]::Round($priorDamage, 1))
            $finalEpisode | Add-Member -NotePropertyName DerivedFinalFightBossDamage -NotePropertyValue $derivedDamage
            $finalEpisode | Add-Member -NotePropertyName DerivedFinalFightNote -NotePropertyValue (
                'result.json carries the SESSION cumulative bossDamage; the per-fight value here is ' +
                'derived as sessionCumulative - sum(recorded rows), which holds only while episode damage ' +
                'accounting stays contiguous. Informational; never counted in the denominator.')
        } catch { $finalEpisode = $null }
    }

    # -----------------------------------------------------------------------
    # 8. Provenance + summary.json
    # -----------------------------------------------------------------------
    $provenance = $null
    if (Test-Path -LiteralPath $manifestPath -PathType Leaf) {
        try {
            $manifest = Read-JsonObjectFile $manifestPath 'probe manifest'
            $provenance = [pscustomobject]@{
                RunName = $manifest.RunName
                RunId = $manifest.RunId
                Mode = $manifest.Mode
                InputCoreSha256 = $manifest.InputCoreSha256
                InputPluginSha256 = $manifest.InputPluginSha256
                ProbeSourceSha256 = $manifest.ProbeSourceSha256
                SourceGameSha256 = $manifest.SourceGameSha256
            }
        } catch { $provenance = $null }
    }

    $evidenceValid = ($gateFailures.Count -eq 0)
    if (-not $evidenceValid) { $exitCode = 2 }

    $summary = [ordered]@{
        Schema = 'chaite-rehearsal/v1'
        CreatedUtc = [DateTime]::UtcNow.ToString('o')
        Tag = $Tag
        Route = $Route
        Difficulty = $Difficulty
        Controller = 'hand-written Fishron wing script, zero residual'
        EpisodesRequested = $Episodes
        EpisodesLinesExpected = $expectedEpisodesFileLines
        EpisodesLinesObserved = $episodeLines.Count
        EpisodesFile = $episodePath
        EvidenceValid = $evidenceValid
        GateFailures = @($gateFailures.ToArray())
        ExitCode = $exitCode
        WallSecondsElapsed = $launchSeconds
        ResidualPolicyFile = $zeroPolicyPath
        ResidualPolicyFileSha256 = $zeroPolicySha
        ResidualPolicyFileTokens = $zeroTokens.Count
        ResidualPolicyFileDeclaredInputs = $zeroHeaderInputs
        ResidualPolicyFileDeclaredHidden = $zeroHeaderHidden
        ResidualPolicyFileLoadable = $zeroLoadable
        ResidualPolicyExpectedTokens = $zeroExpectedTokens
        LearnedPolicyInputCount = $shape.InputCount
        LearnedPolicyHeadCount = $shape.HeadCount
        PolicyRoutesValue = $Route
        PolicyRoutesIsEnumMember = $isEnumMember
        ResidualEvaluated = $false
        ResidualInertReason = if ($zeroLoadable) {
                'policy routes name is not a FormulaRoute enum member, so LearnedPolicy.ForRoute returns null for this route and the file is never read'
            } else {
                'policy routes name is not a FormulaRoute enum member (so the file is never read) AND the file itself is stale for this build (declared inputs / token count do not match LearnedPolicy.InputCount / HeadCount), so LearnedPolicy.Load would throw if it were ever claimed'
            }
        PolicyFormatVariable = 'cleared -> residual (ExportedPolicy.ResolveFormat() = Residual; ForConfiguredFile() returns null)'
        BridgeFileVariable = 'cleared (bridgeDriven=false; no trainer lockstep, no replay owns movement)'
        ProbeOutVariable = $episodeBase
        RunMaxTicks = $RunMaxTicks
        SimDps = $SimDps
        SimDpsNote = 'pinned; with CHAITE_SIM_DPS unset the probe randomises 600..1200 per episode'
        Seed = $Seed
        MaxTicks = $MaxTicks
        TakeoverTick = $TakeoverTick
        BossLifeMax = $bossLifeMax
        WallSeconds = $WallSeconds
        TimeoutSeconds = $TimeoutSeconds
        Headless = $true
        SkipBeam = $true
        BuildPerformed = $buildPerformed
        BuildSkippedReason = $buildSkippedReason
        PrepareError = $prepareError
        LaunchError = $launchError
        HostExitCode = if ($null -ne $desktopExit) { $desktopExit.HostExitCode } else { $null }
        HostExitCodeNote = 'the child exit code is battlePassed?0 : reportedSuccess?10 : 20 for the FINAL fight only; a lost final fight is 20 by design and is NOT an evidence failure. Only 124 (host timeout) is gated.'
        Provenance = $provenance
        Metrics = $metrics
        FinalEpisodeNotInEpisodesFile = $finalEpisode
    }
    $summaryPath = Join-Path $rehearsalDir 'summary.json'
    Write-JsonFile $summaryPath $summary 8

    # -----------------------------------------------------------------------
    # 9. Report
    # -----------------------------------------------------------------------
    Write-Host ''
    if ($null -ne $metrics) {
        Write-Host ('=== rehearsal ' + $Tag + ' : ' + $metrics.Rows + ' recorded fights of ' +
            $Episodes + ' requested (' + $Route + ', ' + $Difficulty + ') ===')
        Write-Host ('  win            {0,6:F2}%  ({1}/{2})' -f $metrics.WinRate, $metrics.Wins, $metrics.Rows)
        Write-Host ('  hits <= 2      {0,6:F2}%  ({1}/{2})   <- proxy for the 2-hit master tolerance' -f
            $metrics.HitsLe2Rate, $metrics.HitsLe2, $metrics.Rows)
        $histKeys = @($metrics.HitsHistogram.Keys | ForEach-Object { [int]$_ } | Sort-Object)
        $histText = ($histKeys | ForEach-Object { ($_.ToString([Globalization.CultureInfo]::InvariantCulture) + ':' +
            $metrics.HitsHistogram[$_.ToString([Globalization.CultureInfo]::InvariantCulture)].ToString([Globalization.CultureInfo]::InvariantCulture)) }) -join ', '
        Write-Host ('  hits histogram {' + $histText + '}')
        $outcomeText = (@($metrics.Outcomes.Keys | Sort-Object | ForEach-Object { $_ + ':' + $metrics.Outcomes[$_] }) -join ', ')
        Write-Host ('  outcomes       {' + $outcomeText + '}')
        Write-Host ('  median ticks   {0}   (upper-index convention {1}; min {2} max {3})' -f
            $metrics.MedianTicks, $metrics.MedianTicksUpperIndexConvention, $metrics.MinTicks, $metrics.MaxTicks)
        Write-Host ('  hits/1k tick   {0}' -f $metrics.HitsPer1kTick)
        Write-Host ('  mean bossDamage{0,9}  of {1} ({2} lifeMax, max remaining {3})' -f
            $metrics.MeanBossDamage, $bossLifeMax, $Difficulty, $metrics.MaxBossLifeRemaining)
        Write-Host ('  simulatedDps values {' + ((@($metrics.SimulatedDpsValues.Keys | Sort-Object | ForEach-Object {
            $_ + ':' + $metrics.SimulatedDpsValues[$_] }) -join ', ')) + '}')
        $winBar = if ($metrics.WinRate -ge 80.0) { 'PASS' } else { 'FAIL' }
        $proxyBar = if ($metrics.HitsLe2Rate -ge 80.0) { 'PASS' } else { 'FAIL' }
        Write-Host ('  BAR win >= 80% : ' + $winBar + '   BAR hits<=2 >= 80% : ' + $proxyBar +
            '   (bar result does not change the exit code; EvidenceValid does)')
    } else {
        Write-Host ('=== rehearsal ' + $Tag + ' : NO usable episode rows ===')
    }
    if ($null -ne $finalEpisode) {
        Write-Host ('  final fight (never written to the episodes file, not counted): status=' +
            $finalEpisode.Status + ' outcome=' + $finalEpisode.Outcome + ' hits=' + $finalEpisode.Hits +
            ' ticks=' + $finalEpisode.Ticks + ' derivedDamage=' + $finalEpisode.DerivedFinalFightBossDamage)
    }
    if (-not $evidenceValid) {
        Write-Host ''
        Write-Host 'EVIDENCE GATE FAILED (exit 2). Raw data is preserved, nothing was deleted:'
        foreach ($failure in $gateFailures) { Write-Host ('  - ' + $failure) }
    }
    Write-Host ''
    Write-Host ('summary.json : ' + $summaryPath)
    Write-DriverLog ('evidenceValid=' + $evidenceValid + ' exitCode=' + $exitCode +
        ' lines=' + $episodeLines.Count + '/' + $expectedEpisodesFileLines)
} catch {
    $exitCode = 1
    Write-DriverLog ('FATAL: ' + $_.Exception.Message) 'ERROR'
    Write-DriverLog ($_.ScriptStackTrace) 'ERROR'
    Write-Host ''
    Write-Host ('REHEARSAL ABORTED (exit 1): ' + $_.Exception.Message)
    if (-not (Test-Path -LiteralPath (Join-Path $rehearsalDir 'summary.json'))) {
        try {
            Write-JsonFile (Join-Path $rehearsalDir 'summary.json') ([ordered]@{
                Schema = 'chaite-rehearsal/v1'
                CreatedUtc = [DateTime]::UtcNow.ToString('o')
                Tag = $Tag; Route = $Route; Difficulty = $Difficulty
                EpisodesRequested = $Episodes; EpisodesLinesExpected = $expectedEpisodesFileLines
                EvidenceValid = $false; Fatal = $_.Exception.Message
                PrepareError = $prepareError; LaunchError = $launchError
                ExitCode = 1
            }) 6
        } catch { }
    }
} finally {
    foreach ($name in $envNames) {
        $saved = $envSnapshot[$name]
        [Environment]::SetEnvironmentVariable($name, $saved, 'Process')
    }
    $left = @(Get-ProcessResidue)
    if ($left.Count -eq 0) {
        Write-DriverLog 'process check    : no Terraria or Chaite.DesktopHost process remains'
    } else {
        Write-DriverLog ('process check    : RESIDUE ' +
            (($left | ForEach-Object { $_.Name + '#' + $_.Id }) -join ', ')) 'ERROR'
    }
}

exit $exitCode

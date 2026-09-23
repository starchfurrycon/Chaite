# Writes a learned-policy file for the residual network in LearnedPolicy.cs.
#
# The format, read back by LearnedPolicy.Load, is a whitespace-separated token
# stream:
#
#   chaite-policy 1 <inputs> <hidden>
#   <hidden * inputs>  hidden weights, row major, index h * inputs + i
#   <hidden>           hidden biases
#   <10 * hidden>      head weights, index k * hidden + h
#   <10>               head biases
#
# The header order is inputs before hidden. The loader parses tokens[2] as the
# declared input count and tokens[3] as the declared width, so writing them the
# other way round produces "declares 1 inputs and 38 hidden units" and the
# plugin fails closed at takeover rather than quietly running the fixed
# machine. That is the intended behaviour -- a configured-but-broken policy must
# be loud -- and it is how this ordering was found.
#
# Total = 4 + hidden * 38 + hidden + 10 * hidden + 10. With hidden 1 that is 63
# tokens, the smallest width the loader accepts, and one hidden unit is already
# enough to express a gated action because tanh is monotone: a weighted sum of
# the state one-hot at 11..24 and the timer at 25 crossing a threshold is
# exactly "in this state, during this part of the clock, nudge this axis". The
# loader rejects any width outside 1..64 and any token count that does not
# match, so a malformed file fails loudly rather than silently disabling the
# policy.
#
# Class zero of each head means "leave the scripted decision alone" and receives
# the action margin, so an all-zero file reproduces the fixed circuit exactly.
# That is the control this script exists to provide: a candidate is only
# believed once the zero file has been shown to reproduce the script bit for
# bit.
#
# This writes files only. It never touches the game, the probe, or the network.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Path,
    [int]$Hidden = 1,
    [ValidateSet('zero', 'random', 'head-bias', 'perturb')][string]$Mode = 'zero',
    [double]$Scale = 0.5,
    [int]$Seed = 1,
    [double[]]$HeadBias,
    [string]$Base,
    # Targeted construction, for a hypothesis that is about one weight rather
    # than a random walk. Each entry is "<group>[<index>]=<value>" with group one
    # of w (hidden weights, index h * <inputs> + i), b (hidden bias), hw (head
    # weights, index k * hidden + h) or hb (head bias). Example, nudge horizontal
    # -1 only while the native state is 8 or 9:
    #   -Set 'w[19]=1','w[20]=1','b[0]=-0.5','hb[1]=1','hw[1]=4'
    # State one-hots occupy feature slots 11..24, so state 8 is slot 19 and state
    # 9 is slot 20. Applied after the mode, so it composes with -Mode perturb.
    [string[]]$Set
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ($Hidden -lt 1 -or $Hidden -gt 64) {
    throw "hidden width $Hidden is outside the loader's 1..64 range"
}

# These must equal LearnedPolicy.InputCount and LearnedPolicy.HeadCount in
# src/Chaite.Core/LearnedPolicy.cs. The loader rejects a file whose header
# disagrees, so a drift here fails closed rather than silently training a
# network the engine reads as something else.
$input_ = 40
$heads = 11

$rng = New-Object System.Random($Seed)

# Box-Muller, so the perturbation scale means the same thing across widths.
$spare = [double]::NaN
function Get-Normal {
    if (-not [double]::IsNaN($script:spare)) {
        $v = $script:spare
        $script:spare = [double]::NaN
        return $v
    }
    $u1 = 0.0
    while ($u1 -le 0.0) { $u1 = $script:rng.NextDouble() }
    $u2 = $script:rng.NextDouble()
    $magnitude = [math]::Sqrt(-2.0 * [math]::Log($u1))
    $angle = 2.0 * [math]::PI * $u2
    $script:spare = $magnitude * [math]::Sin($angle)
    return $magnitude * [math]::Cos($angle)
}

function Get-Value {
    param([string]$Group)
    if ($Mode -eq 'zero') { return 0.0 }
    if ($Mode -eq 'head-bias' -and $Group -ne 'headBias') { return 0.0 }
    return (Get-Normal) * $Scale
}

# Hill climbing needs to move away from the incumbent rather than start over, so
# perturb reads the incumbent's payload and adds a normal offset to every weight.
# The header is taken from the incumbent too, so a perturbation can never change
# the declared shape.
$basePayload = $null
if ($Mode -eq 'perturb') {
    if ([string]::IsNullOrEmpty($Base)) {
        throw "-Mode perturb requires -Base <policy file>"
    }
    if (-not (Test-Path -LiteralPath $Base)) {
        throw "-Base does not exist: $Base"
    }
    $baseTokens = [System.IO.File]::ReadAllText($Base).Split(
        [char[]]" `t`r`n", [System.StringSplitOptions]::RemoveEmptyEntries)
    if ($baseTokens.Length -lt 4 -or $baseTokens[0] -ne 'chaite-policy') {
        throw "-Base is not a chaite-policy file: $Base"
    }
    $Hidden = [int]$baseTokens[3]
    if ($baseTokens.Length -ne (4 + $Hidden * $input_ + $Hidden + $heads * $Hidden + $heads)) {
        throw "-Base has $($baseTokens.Length) tokens, not the count its header declares"
    }
    $basePayload = $baseTokens[4..($baseTokens.Length - 1)]
}

$payload = New-Object System.Collections.ArrayList
function Add-Payload {
    param([string]$Group, [double]$Value)
    $v = $Value
    if ($null -ne $script:basePayload) {
        $v = [double]$script:basePayload[$script:payloadIndex] + $Value
    }
    $script:payloadIndex++
    [void]$script:payload.Add($v)
}

$script:payloadIndex = 0
for ($h = 0; $h -lt $Hidden; $h++) {
    for ($i = 0; $i -lt $input_; $i++) {
        Add-Payload -Group 'hiddenWeights' -Value (Get-Value -Group 'hiddenWeights')
    }
}
for ($h = 0; $h -lt $Hidden; $h++) {
    Add-Payload -Group 'hiddenBias' -Value (Get-Value -Group 'hiddenBias')
}
for ($k = 0; $k -lt $heads; $k++) {
    for ($h = 0; $h -lt $Hidden; $h++) {
        Add-Payload -Group 'headWeights' -Value (Get-Value -Group 'headWeights')
    }
}
for ($k = 0; $k -lt $heads; $k++) {
    $value = Get-Value -Group 'headBias'
    if ($null -ne $HeadBias -and $k -lt @($HeadBias).Count) {
        $value = [double]@($HeadBias)[$k]
    }
    Add-Payload -Group 'headBias' -Value $value
}

# Overrides address each group in its own indexing, which is how the loader reads
# them back, so a targeted hypothesis never has to know the flat layout.
if ($null -ne $Set) {
    $groups = @{
        'w'  = @{ Offset = 0; Length = $Hidden * $input_ }
        'b'  = @{ Offset = $Hidden * $input_; Length = $Hidden }
        'hw' = @{ Offset = $Hidden * $input_ + $Hidden; Length = $heads * $Hidden }
        'hb' = @{ Offset = $Hidden * $input_ + $Hidden + $heads * $Hidden; Length = $heads }
    }
    foreach ($entry in @($Set)) {
        if ($entry -notmatch '^(w|b|hw|hb)\[(\d+)\]=(-?[0-9.eE+]+)$') {
            throw "cannot parse -Set '$entry'; expected <w|b|hw|hb>[<index>]=<value>"
        }
        $group = $Matches[1]
        $index = [int]$Matches[2]
        $value = [double]::Parse($Matches[3], [System.Globalization.CultureInfo]::InvariantCulture)
        $spec = $groups[$group]
        if ($index -lt 0 -or $index -ge $spec.Length) {
            throw ("-Set '{0}' indexes {1} out of range: group '{2}' holds {3} value(s)" -f `
                $entry, $index, $group, $spec.Length)
        }
        $script:payload[$spec.Offset + $index] = $value
    }
}

$tokens = New-Object System.Collections.ArrayList
[void]$tokens.Add('chaite-policy')
[void]$tokens.Add('1')
[void]$tokens.Add([string]$input_)
[void]$tokens.Add([string]$Hidden)
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
foreach ($v in $payload) {
    [void]$tokens.Add(([double]$v).ToString('R', $invariant))
}

$expected = 4 + $Hidden * $input_ + $Hidden + $heads * $Hidden + $heads
if ($tokens.Count -ne $expected) {
    throw "internal error: wrote $($tokens.Count) tokens but the loader expects $expected"
}

$directory = Split-Path -Parent $Path
if ($directory -and -not (Test-Path -LiteralPath $directory)) {
    [void](New-Item -ItemType Directory -Path $directory -Force)
}
[System.IO.File]::WriteAllText($Path, ($tokens -join "`n") + "`n",
    (New-Object System.Text.UTF8Encoding($false)))

Write-Output ("wrote {0}: mode={1} hidden={2} scale={3} seed={4} tokens={5}" -f `
    $Path, $Mode, $Hidden, $Scale, $Seed, $tokens.Count)
# Native acceptance for several parameter points inside ONE invocation.
#
# Why this exists: the session environment is re-created between tool calls and
# does not reliably carry what the previous call left behind. Measured this way,
# two runs of the same build and config reported 9 hits and 4 hits respectively,
# which is not a property of the build. Every arm of a comparison therefore has
# to be launched from the same process with the environment pinned explicitly.
#
# The policy environment is pinned to "off" by clearing all three variables
# individually. Clearing them as one comma-separated Remove-Item list is a trap:
# CHAITE_POLICY_ROUTES is normally unset, and Remove-Item terminates on the first
# missing name, silently leaving the later variables in place.
#
# Usage:
#   .\tools\sweep-native.ps1 -FormulaRoute fishron-fairy-wing -Values 0,10,20 `
#       -Variable CHAITE_REFILL_GUARD -MaxTicks 3000
param(
    [Parameter(Mandatory=$true)][string]$FormulaRoute,
    [Parameter(Mandatory=$true)][string[]]$Values,
    [string]$Variable = "CHAITE_REFILL_GUARD",
    [int]$MaxTicks = 3000,
    [int]$WallSeconds = 900,
    [string]$Tag = "sweep",
    [switch]$Dense
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
Push-Location $repo

# Pin the environment: one statement per name so a missing name cannot abort the rest.
foreach ($name in 'CHAITE_POLICY_FILE', 'CHAITE_POLICY_ROUTES', 'CHAITE_POLICY_FORMAT') {
    Remove-Item "Env:\$name" -ErrorAction SilentlyContinue
}
Remove-Item "Env:\CHAITE_PROBE_DENSE_FRAMES" -ErrorAction SilentlyContinue
if ($Dense) { $env:CHAITE_PROBE_DENSE_FRAMES = "1" }

Write-Output "sweep variable : $Variable"
Write-Output "formula route  : $FormulaRoute"
Write-Output "max ticks      : $MaxTicks"
Write-Output "policy         : off (CHAITE_POLICY_FILE='$($env:CHAITE_POLICY_FILE)')"
Write-Output ""

$rows = @()
foreach ($value in $Values) {
    Set-Item -Path "Env:\$Variable" -Value "$value"
    $runName = "$Tag-$value"
    $output = (& "$repo\tools\run-native-acceptance.ps1" -RunName $runName `
        -Phase monitor -MaxTicks $MaxTicks -WallSeconds $WallSeconds `
        -FormulaRoute $FormulaRoute 2>&1 | Out-String)

    # Read the fields out of the verdict block. Matching on the whole text at
    # once, line-anchored, is what the block's own labels allow; pulling the
    # individual Select-String matches and indexing them fails whenever a run
    # omits a line, which is exactly when the row matters most.
    function Field([string]$pattern) {
        $match = [regex]::Match($output, $pattern)
        if ($match.Success) { return $match.Groups[1].Value }
        return "?"
    }
    $rows += [pscustomobject]@{
        Value   = $value
        Hits    = Field '(?m)^HITS\s+: (\d+)'
        Ticks   = Field '(?m)^ticks\s+: (\d+)'
        BossDmg = Field '(?m)^boss damage\s+: (\d+)'
        Death   = Field '(?m)^death\s+: (\S+)'
        Contact = Field 'npc contact: (\d+)'
    }
}

Remove-Item "Env:\$Variable" -ErrorAction SilentlyContinue
Pop-Location

Write-Output ""
Write-Output "==================== SWEEP RESULT ($Variable) ===================="
$rows | Format-Table -AutoSize | Out-String | Write-Output

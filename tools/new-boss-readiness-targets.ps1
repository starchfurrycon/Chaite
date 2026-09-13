param(
    [Parameter(Mandatory = $true)][string]$Catalog,
    [Parameter(Mandatory = $true)][string]$Profiles,
    [Parameter(Mandatory = $true)][int[]]$PlannedSeeds,
    [Parameter(Mandatory = $true)][string]$SamplingPlanId,
    [Parameter(Mandatory = $true)][string]$SamplingPlanDescription,
    [string]$Output
)

# Build a V3, catalog-bound acceptance plan from profile discovery output.
# It never starts Terraria, writes no evidence by default, and will not invent
# profile hashes, variants, or seeds for unsupported cells. Callers must supply
# their independently chosen seed list before collecting the campaign evidence.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# The evaluator is dot-sourced for its pure schema helpers. Preserve inputs
# whose names overlap its own CLI parameters before doing so.
$catalogArgument = $Catalog
$profilesArgument = $Profiles
$outputArgument = $Output
. (Join-Path $PSScriptRoot 'evaluate-boss-readiness.ps1') -FunctionsOnly

$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$catalogPath = Assert-BrProjectPath $catalogArgument $projectRoot $true
$profilesPath = Assert-BrProjectPath $profilesArgument $projectRoot $true
$coverageCatalog = (Get-Content -LiteralPath $catalogPath -Raw -Encoding UTF8 |
    ConvertFrom-Json)
$profileDocument = (Get-Content -LiteralPath $profilesPath -Raw -Encoding UTF8 |
    ConvertFrom-Json)
$targetDocument = New-BrCatalogBoundTargetDocument `
    -CoverageCatalog $coverageCatalog -ProfileDocument $profileDocument `
    -PlannedSeeds $PlannedSeeds -SamplingPlanId $SamplingPlanId `
    -SamplingPlanDescription $SamplingPlanDescription
$json = ConvertTo-Json -InputObject $targetDocument -Depth 40
if (-not [string]::IsNullOrWhiteSpace($outputArgument)) {
    $artifactRoot = Join-Path $projectRoot 'artifacts'
    $outputPath = Assert-BrProjectPath $outputArgument $artifactRoot $false
    if (Test-Path -LiteralPath $outputPath) {
        throw 'Target plan output already exists; preserve the preregistered plan and choose a new path.'
    }
    $parent = Split-Path -Parent $outputPath
    if (-not (Test-Path -LiteralPath $parent -PathType Container)) {
        throw 'Target plan output parent must already exist under project artifacts.'
    }
    $stream = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew,
        [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.UTF8Encoding]::new($false).GetBytes($json)
        $stream.Write($bytes, 0, $bytes.Length)
    } finally {
        $stream.Dispose()
    }
}
Write-Output $json
exit 0

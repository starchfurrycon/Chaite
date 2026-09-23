param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [switch]$Package,
    [ValidatePattern('^v[0-9]+\.[0-9]+\.[0-9]+(?:-[a-zA-Z0-9.-]+)?$')]
    [string]$PackageVersion = 'v0.7.0-alpha'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) {
    throw 'Visual Studio Installer/vswhere.exe was not found.'
}
$visualStudio = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationPath
if (-not $visualStudio) {
    throw 'Visual Studio or Build Tools with MSBuild was not found.'
}
$msbuild = Join-Path $visualStudio 'MSBuild\Current\Bin\MSBuild.exe'
# The API/patch verification scripts load Mono.Cecil from the Patcher output.
# SDK-style PackageReference projects do not consistently copy that dependency
# for a plain legacy-net48 build unless CopyLocalLockFileAssemblies is explicit.
& $msbuild (Join-Path $projectRoot 'Chaite.sln') /restore /m /p:Configuration=$Configuration /p:CopyLocalLockFileAssemblies=true /verbosity:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed with exit code $LASTEXITCODE" }

if ($Package) {
    if ($Configuration -ne 'Release') { throw 'Packaging requires the optimized Release configuration.' }
    # Never remove an earlier package: it may be the user's working fallback.
    $release = Join-Path $projectRoot ("artifacts\Chaite-" + $PackageVersion)
    if (Test-Path -LiteralPath $release) {
        throw "Package already exists; preserve it and choose a new -PackageVersion: $release"
    }
    $managerBin = Join-Path $projectRoot "src\Chaite.Manager\bin\$Configuration\net48"
    $patcherBin = Join-Path $projectRoot "src\Chaite.Patcher\bin\$Configuration\net48"
    $pluginBin = Join-Path $projectRoot "src\Chaite.Plugin\bin\$Configuration\net48"
    $verification = Join-Path $projectRoot ("artifacts\package-check-" + $PackageVersion + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    New-Item -ItemType Directory -Path $verification | Out-Null
    & (Join-Path $projectRoot "tests\Chaite.Tests\bin\$Configuration\net48\Chaite.Tests.exe") |
        Tee-Object -FilePath (Join-Path $verification 'tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline regression tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-boss-evidence.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'native-evidence-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline native-evidence validation tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-boss-readiness.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'boss-readiness-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline per-Boss readiness tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-priority-phase-fixture-contract.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'priority-phase-fixture-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Priority Boss phase fixture contract failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-priority-organic-fixture-contract.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'priority-organic-fixture-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Priority Boss organic fixture contract failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-priority-boss-probe-schema.ps1') `
        -OutputDirectory (Join-Path $verification 'priority-probe-schema') |
        Tee-Object -FilePath (Join-Path $verification 'priority-probe-schema-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Priority Boss probe schema failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-training-objective.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'training-objective-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Policy search objective contract failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-native-motion-evidence.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'motion-evidence-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline motion evidence validation tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-native-flight-evidence.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'flight-evidence-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline flight evidence validation tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-twins-native-audit.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'twins-native-audit-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline Twins native post-audit tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-destroyer-native-audit.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'destroyer-native-audit-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Offline Destroyer native post-audit tests failed; no package was created.' }
    & (Join-Path $PSScriptRoot 'test-common-special-ranged-native-audit.ps1') |
        Tee-Object -FilePath (Join-Path $verification 'common-special-ranged-native-audit-tests.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Common special-ranged native metadata audit failed; no package was created.' }
    $uiOutput = Join-Path $verification 'ui'
    $uiCheck = Start-Process -FilePath (Join-Path $managerBin 'Chaite.Manager.exe') `
        -ArgumentList @('--ui-smoke', ('"' + $uiOutput + '"')) -WindowStyle Hidden -PassThru -Wait
    if ($uiCheck.ExitCode -ne 0) { throw "UI smoke failed ($($uiCheck.ExitCode)); no package was created." }
    New-Item -ItemType Directory -Path (Join-Path $release 'Audio') | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $release 'Memes') | Out-Null
    Copy-Item -LiteralPath (Join-Path $managerBin 'Chaite.Manager.exe') -Destination $release
    Copy-Item -LiteralPath (Join-Path $managerBin 'Chaite.Manager.exe.config') -Destination $release
    Copy-Item -LiteralPath (Join-Path $patcherBin 'Chaite.Patcher.exe') -Destination $release
    Copy-Item -LiteralPath (Join-Path $patcherBin 'Chaite.Patcher.exe.config') -Destination $release -ErrorAction SilentlyContinue
    Copy-Item -LiteralPath (Join-Path $patcherBin 'Mono.Cecil.dll') -Destination $release
    Copy-Item -LiteralPath (Join-Path $pluginBin 'Chaite.Plugin.dll') -Destination $release
    Copy-Item -LiteralPath (Join-Path $pluginBin 'Chaite.Core.dll') -Destination $release
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Chaite.Plugin\config.json') -Destination $release
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Chaite.Plugin\Audio\README.txt') -Destination (Join-Path $release 'Audio')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'src\Chaite.Plugin\Memes\README.txt') -Destination (Join-Path $release 'Memes')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'README.md') -Destination $release
    Copy-Item -LiteralPath (Join-Path $projectRoot 'CHANGELOG.md') -Destination $release
    Copy-Item -LiteralPath (Join-Path $projectRoot 'VERIFICATION.md') -Destination $release
    $packageDocs = Join-Path $release 'docs'
    New-Item -ItemType Directory -Path $packageDocs | Out-Null
    foreach ($document in @('formula-routes.md',
        'formula-source-review-20260914.md', 'weapon-profile-policy.md',
        'native-flight-policy.md',
        'native-dash-source-audit.md', 'workflow-and-state-machine.md',
        'meme-register.md')) {
        Copy-Item -LiteralPath (Join-Path $projectRoot ('docs\' + $document)) -Destination $packageDocs
    }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination $release
    Write-Host "Release: $release"
    Write-Host "Verification: $verification"
}

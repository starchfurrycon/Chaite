param(
    [switch]$Publish,
    [switch]$CreateRepository,
    [ValidateSet('starchfurrycon')][string]$Owner = 'starchfurrycon',
    [ValidateSet('Chaite')][string]$Repository = 'Chaite',
    [string]$CommitMessage = 'Publish validated Chaite source'
)

# Default is a read-only local publication audit. Remote creation / git writes /
# push require -Publish, and creating a missing repository ALSO requires
# -CreateRepository. Credentials come from existing Git Credential Manager only;
# never store them in URLs, git configuration, a file, an argument or output.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$remoteUrl = "https://github.com/$Owner/$Repository.git"
$apiRoot = "https://api.github.com/repos/$Owner/$Repository"
$env:GCM_INTERACTIVE = 'Never'
$env:GIT_TERMINAL_PROMPT = '0'

function Invoke-Git([string[]]$Arguments) {
    $output = @(& git -C $projectRoot @Arguments)
    if ($LASTEXITCODE -ne 0) { throw ('git command failed: ' + ($Arguments[0..([Math]::Min(1, $Arguments.Length - 1))] -join ' ')) }
    return $output
}

function Get-PublicSourceManifest {
    $paths = [Collections.Generic.List[string]]::new()
    foreach ($name in @('.gitignore', 'Chaite.sln', 'README.md', 'CHANGELOG.md', 'VERIFICATION.md', 'LICENSE')) {
        if (-not (Test-Path -LiteralPath (Join-Path $projectRoot $name) -PathType Leaf)) { throw "Missing public source input: $name" }
        $paths.Add($name)
    }
    foreach ($area in @('src', 'tests')) {
        Get-ChildItem -LiteralPath (Join-Path $projectRoot $area) -Recurse -File | ForEach-Object {
            $relative = $_.FullName.Substring($projectRoot.Length + 1).Replace('\', '/')
            if ($relative -match '/(bin|obj|packages|\.vs)/') { return }
            if ($_.Extension -notin @('.cs', '.csproj', '.resx', '.config', '.manifest')) {
                if ($relative -ne 'src/Chaite.Plugin/config.json' -and $relative -ne 'src/Chaite.Plugin/Audio/README.txt') { return }
            }
            $paths.Add($relative)
        }
    }
    # Only directly reviewed maintenance scripts are included. Research scripts,
    # decompiled outputs and live-game test artifacts are intentionally excluded.
    foreach ($name in @('build.ps1', 'verify-api-contract.ps1', 'publish-github.ps1',
        'GameProbe.cs', 'GameProbePatcher.cs', 'prepare-game-probe.ps1', 'Chaite.DesktopHost.cs', 'start-isolated-test.ps1', 'run-boss-validation.ps1', 'test-boss-evidence.ps1')) {
        if (Test-Path -LiteralPath (Join-Path $PSScriptRoot $name) -PathType Leaf) { $paths.Add('tools/' + $name) }
    }
    $workflows = Join-Path $projectRoot '.github\workflows'
    if (Test-Path -LiteralPath $workflows -PathType Container) {
        Get-ChildItem -LiteralPath $workflows -File | Where-Object { $_.Extension -in @('.yml', '.yaml') } | ForEach-Object {
            $paths.Add('.github/workflows/' + $_.Name)
        }
    }
    return @($paths | Sort-Object -Unique)
}

function Assert-PublicSource([string[]]$Manifest) {
    foreach ($relative in $Manifest) {
        $absolute = [IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
        if (-not $absolute.StartsWith($projectRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Publication source escaped the project root.' }
        $entry = Get-Item -LiteralPath $absolute
        $ancestor = $entry
        while ($null -ne $ancestor -and $ancestor.FullName -ne $projectRoot) {
            if (($ancestor.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw "Refusing linked publication source: $relative" }
            $ancestor = if ($ancestor -is [IO.FileInfo]) { $ancestor.Directory } else { $ancestor.Parent }
        }
        if ($entry.Length -gt 2MB) { throw "Unexpectedly large source input requires manual review: $relative" }
        if ($relative -match '(?i)(^|/)(artifacts|research|\.research|\.validation|bin|obj|packages)/|MEMORY|DEVELOPMENT_NOTES|\.(exe|dll|wav|mp3|ogg|wld|plr|map|bak|zip|7z|pfx|p12|key)$') {
            throw "Forbidden publication path: $relative"
        }
        $content = [IO.File]::ReadAllText($absolute)
        $credentialPattern = '(?:gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk-(?:proj-)?[A-Za-z0-9_-]{24,}|-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----)'
        $personalPathPattern = '(?i)[A-Z]:[\\/](?:Users|Documents and Settings)[\\/][^\\/\s"'']+|personal tasks[\\/]'
        if ($content -match $credentialPattern) { throw "Possible credential in $relative; refusing to publish (matching content is not printed)." }
        if ($content -match $personalPathPattern) { throw "Private local path in $relative; parameterize it before publication." }
    }
}

$manifest = Get-PublicSourceManifest
Assert-PublicSource $manifest
Write-Output "PUBLIC SOURCE AUDIT: $($manifest.Count) allowlisted files for $remoteUrl"
$manifest | ForEach-Object { Write-Output ('  ' + $_) }
if (-not $Publish) {
    Write-Output 'DRY RUN ONLY: no git initialization, staging, commit, repository creation, credential retrieval or push occurred.'
    Write-Output 'After final verification, explicitly pass -Publish; add -CreateRepository only for the first publication.'
    return
}

$credentialLines = $null
$credentialValues = @{}
$headers = $null
try {
    $inputText = [string]::Join([Environment]::NewLine, @('protocol=https', 'host=github.com', ('username=' + $Owner), ''))
    $credentialLines = @($inputText | git credential-manager get 2>$null)
    if ($LASTEXITCODE -ne 0) { throw 'Existing protected GitHub credential was unavailable; no login prompt will be opened.' }
    foreach ($line in $credentialLines) {
        $separator = $line.IndexOf('=')
        if ($separator -gt 0) { $credentialValues[$line.Substring(0, $separator)] = $line.Substring($separator + 1) }
    }
    if ([string]::IsNullOrWhiteSpace($credentialValues['password'])) { throw 'Protected GitHub credential did not contain a usable token.' }
    $headers = @{ 'User-Agent' = 'Chaite-Source-Publisher'; 'Accept' = 'application/vnd.github+json'; Authorization = ('Bearer ' + $credentialValues['password']) }
    $identity = Invoke-RestMethod -Uri 'https://api.github.com/user' -Headers $headers
    if ($identity.login -cne $Owner) { throw 'Authenticated GitHub account differs from the explicitly authorized repository owner.' }

    $repositoryInfo = $null
    try { $repositoryInfo = Invoke-RestMethod -Uri $apiRoot -Headers $headers }
    catch { if ($null -eq $_.Exception.Response -or [int]$_.Exception.Response.StatusCode -ne 404) { throw 'Could not verify the remote repository without ambiguity.' } }
    if ($null -eq $repositoryInfo -and -not $CreateRepository) { throw 'The repository does not exist; explicit -CreateRepository is required.' }
    if ($null -ne $repositoryInfo -and ($repositoryInfo.full_name -cne "$Owner/$Repository" -or $repositoryInfo.private)) {
        throw 'Existing repository identity/visibility differs from the planned public source project; refusing to change it.'
    }

    $gitDirectory = Join-Path $projectRoot '.git'
    if (-not (Test-Path -LiteralPath $gitDirectory)) {
        if ($null -ne $repositoryInfo -and $repositoryInfo.size -gt 0) { throw 'Remote repository already has content; clone/reconcile it before initializing unrelated local history.' }
        [void](Invoke-Git @('init', '-b', 'main'))
    }
    $repositoryRoot = (Invoke-Git @('rev-parse', '--show-toplevel')) -join ''
    if ([IO.Path]::GetFullPath($repositoryRoot) -ne $projectRoot) { throw 'The current folder is not the exact git worktree root.' }
    $branch = (Invoke-Git @('branch', '--show-current')) -join ''
    if ($branch -ne 'main') { throw 'Publication requires the main branch; do not silently switch a user branch.' }
    $allowed = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($path in $manifest) { [void]$allowed.Add($path) }
    foreach ($tracked in @(Invoke-Git @('ls-files'))) {
        if (-not $allowed.Contains($tracked)) { throw "Existing tracked file is outside the public allowlist: $tracked" }
    }
    foreach ($staged in @(Invoke-Git @('diff', '--cached', '--name-only'))) {
        if (-not $allowed.Contains($staged)) { throw "Unrelated staged file prevents publication: $staged" }
    }
    $gitUserName = @(& git -C $projectRoot config --local --get user.name)
    if ($gitUserName.Count -eq 0) { [void](Invoke-Git @('config', '--local', 'user.name', $identity.login)) }
    $gitUserEmail = @(& git -C $projectRoot config --local --get user.email)
    if ($gitUserEmail.Count -eq 0) { [void](Invoke-Git @('config', '--local', 'user.email', "$($identity.id)+$($identity.login)@users.noreply.github.com")) }
    $remoteNames = @(Invoke-Git @('remote'))
    if ($remoteNames -notcontains 'origin') { [void](Invoke-Git @('remote', 'add', 'origin', $remoteUrl)) }
    elseif (((Invoke-Git @('remote', 'get-url', 'origin')) -join '') -ne $remoteUrl) { throw 'Origin differs from the approved credential-free HTTPS URL; refusing to replace it.' }

    # Re-scan immediately before staging to avoid publishing a changed local file.
    Assert-PublicSource $manifest
    [void](Invoke-Git (@('add', '--') + $manifest))
    & git -C $projectRoot diff --cached --quiet
    $hasStagedChanges = $LASTEXITCODE -eq 1
    if ($LASTEXITCODE -notin @(0, 1)) { throw 'Could not inspect staged changes.' }
    if ($hasStagedChanges) { [void](Invoke-Git @('commit', '-m', $CommitMessage)) }
    $head = (Invoke-Git @('rev-parse', 'HEAD')) -join ''

    if ($null -eq $repositoryInfo) {
        $body = @{ name = $Repository; description = 'Chaite: version-locked vanilla Terraria boss automation experiment with reversible installation and offline regression tests.'; private = $false; auto_init = $false } | ConvertTo-Json -Compress
        $repositoryInfo = Invoke-RestMethod -Method Post -Uri 'https://api.github.com/user/repos' -Headers $headers -ContentType 'application/json; charset=utf-8' -Body ([Text.Encoding]::UTF8.GetBytes($body))
        if ($repositoryInfo.full_name -cne "$Owner/$Repository" -or $repositoryInfo.private) { throw 'Created repository identity did not match the explicitly authorized public destination.' }
    }
    $remoteRefs = @(Invoke-Git @('ls-remote', '--heads', 'origin', 'main'))
    if ($remoteRefs.Count -gt 0) {
        [void](Invoke-Git @('fetch', '--no-tags', 'origin', 'main'))
        & git -C $projectRoot merge-base --is-ancestor FETCH_HEAD HEAD
        if ($LASTEXITCODE -ne 0) { throw 'Remote main is not an ancestor of local HEAD; reconcile it manually. Force-push is never used.' }
    }
    [void](Invoke-Git @('push', '--set-upstream', 'origin', 'main'))
    $publishedRef = (Invoke-Git @('ls-remote', '--heads', 'origin', 'main')) -join ''
    if (-not $publishedRef.StartsWith($head + [char]9, [StringComparison]::Ordinal)) { throw 'Remote main verification did not match the published local commit.' }
    Write-Output "VERIFIED: https://github.com/$Owner/$Repository/tree/$head"
} finally {
    $headers = $null
    $credentialValues.Clear()
    $credentialLines = $null
    $inputText = $null
}

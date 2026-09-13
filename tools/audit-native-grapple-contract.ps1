param(
    [string]$GameExe = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$expectedGameHash = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
$expectedCecilHash = 'C41BDB9FFD3C5F6E17D2382C1012D73703E035E3F1100245FDD4E08C8DC6EB5B'
$projectRoot = Split-Path -Parent $PSScriptRoot
$cecilPath = Join-Path $projectRoot 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'

if (-not (Test-Path -LiteralPath $GameExe -PathType Leaf)) { throw "Terraria executable not found: $GameExe" }
if (-not (Test-Path -LiteralPath $cecilPath -PathType Leaf)) { throw 'Build Chaite.Patcher Release first so the pinned Mono.Cecil 0.11.6 binary is available.' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $GameExe).Hash -cne $expectedGameHash) { throw 'Unreviewed Terraria.exe hash.' }
if ((Get-FileHash -Algorithm SHA256 -LiteralPath $cecilPath).Hash -cne $expectedCecilHash) { throw 'Unreviewed Mono.Cecil binary hash.' }

[void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $cecilPath).Path)
$oldCulture = [Threading.Thread]::CurrentThread.CurrentCulture
$oldUiCulture = [Threading.Thread]::CurrentThread.CurrentUICulture
[Threading.Thread]::CurrentThread.CurrentCulture = [Globalization.CultureInfo]::InvariantCulture
[Threading.Thread]::CurrentThread.CurrentUICulture = [Globalization.CultureInfo]::InvariantCulture
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $GameExe).Path)
$hasher = [Security.Cryptography.SHA256]::Create()
try {
    $contracts = @(
        @('Terraria.Player', 'QuickGrapple', 0, 553, 1438, '0B21B7FB02040FBC6CE644852B509F863E898C900EBB8DB5A972A5060B2B04E9'),
        @('Terraria.Player', 'QuickGrapple_GetItemToUse', 0, 43, 82, '4DEBBE16514F4DB1DA4320617EE9A00CF02E4256393BE6ACC83896189E16B708'),
        @('Terraria.Player', 'GetGrapplingForces', 4, 372, 980, '3AFEF2F013A6139B2B325B4E49F846D9DEB0E81CEF3AFCADE6DD91D0104DD47E'),
        @('Terraria.Player', 'GrappleMovement', 0, 473, 1295, 'D93767AAB0E3BBA24FE6F8F62623E9F560BD90C6F6FC4BCF99085E7B10CAEE1B'),
        @('Terraria.Player', 'Update', 1, 12757, 36687, '00020A380C43B674738C68F15BDB6CB479B1ED7E2FB316E4660CAF6F3F4D1816'),
        @('Terraria.Projectile', 'AI_007_GrapplingHooks', 0, 1128, 3312, '85C4D3447E6BCE2CF7250E25F37D1CBE2A65A18EA26F2D8B67176CC7507EE26F'),
        @('Terraria.Projectile', 'AI_007_GrapplingHooks_CanTileBeLatchedOnTo', 1, 47, 124, '2017051B0E26DAC2FFD856D9C9FC33747D65A1D31371985CFCBDCC831AE27249'),
        @('Terraria.Item', 'SetDefaults1', 1, 33202, 87746, '3386DF0ADE1787078F979C8A641501AB4F0EB9656034407E206E6C479FEBBA13'),
        @('Terraria.Projectile', 'SetDefaults', 1, 27110, 75578, '6D5C30D11359B9204A59ABDC347D92EFDE23F55BFE3CB27552D8F80D544CBA02')
    )
    foreach ($contract in $contracts) {
        $types = @($assembly.MainModule.Types | Where-Object FullName -ceq $contract[0])
        if ($types.Count -ne 1) { throw "Expected one native type: $($contract[0])" }
        $methods = @($types[0].Methods | Where-Object {
            $_.Name -ceq $contract[1] -and $_.Parameters.Count -eq $contract[2] -and $_.HasBody
        })
        if ($methods.Count -ne 1) { throw "Expected one native method: $($contract[0])::$($contract[1])/$($contract[2])" }
        $method = $methods[0]
        if ($method.Body.Instructions.Count -ne $contract[3] -or $method.Body.CodeSize -ne $contract[4]) {
            throw "Native body shape changed: $($method.FullName)"
        }
        $canonical = [string]::Join("`n", @($method.Body.Instructions | ForEach-Object { $_.ToString() }))
        $bytes = [Text.Encoding]::UTF8.GetBytes($canonical)
        $actualHash = [BitConverter]::ToString($hasher.ComputeHash($bytes)).Replace('-', '')
        if ($actualHash -cne $contract[5]) { throw "Native IL fingerprint changed: $($method.FullName)" }
        Write-Output "PASS $($method.FullName) [$($contract[3]) instructions]"
    }
    Write-Output 'PASS pinned Terraria 1.4.5.8 grapple metadata contract. Game code was not loaded or executed.'
} finally {
    $hasher.Dispose()
    $assembly.Dispose()
    [Threading.Thread]::CurrentThread.CurrentCulture = $oldCulture
    [Threading.Thread]::CurrentThread.CurrentUICulture = $oldUiCulture
}

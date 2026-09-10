param(
    [string]$TerrariaExe = 'D:\Program Files (x86)\Steam\steamapps\common\Terraria\Terraria.exe',
    [string]$OutputDirectory,
    [switch]$PatchCopy
)

# Metadata-only validation: Mono.Cecil reads PE/IL without loading Terraria into
# the CLR. Never Assembly.Load/LoadFrom the game, invoke its methods, install,
# start a process, or write to the live game directory from this script.
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$projectRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$gamePath = [IO.Path]::GetFullPath($TerrariaExe)
$facadePath = Join-Path $projectRoot 'src\Chaite.Plugin\TerrariaFacade.cs'
$pluginPath = Join-Path $projectRoot 'src\Chaite.Plugin\bin\Release\net48\Chaite.Plugin.dll'
$patcherPath = Join-Path $projectRoot 'src\Chaite.Patcher\bin\Release\net48\Chaite.Patcher.exe'
$cecilPath = Join-Path $projectRoot 'src\Chaite.Patcher\bin\Release\net48\Mono.Cecil.dll'
foreach ($required in @($gamePath, $facadePath, $pluginPath, $patcherPath, $cecilPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Missing required input: $required" }
}

$sourceHashBefore = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
$facadeSourceHash = (Get-FileHash -LiteralPath $facadePath -Algorithm SHA256).Hash
$expectedSourceHash = '960A03BFF6050CF7BE16DFC1A7B19E10FC2C4F8F835A6A3B135A50DD9E6BA2F3'
if ($sourceHashBefore -ne $expectedSourceHash) { throw 'Live executable does not match the verified vanilla 1.4.5.8 SHA-256; refusing to guess an API contract.' }
if ($PatchCopy -and [string]::IsNullOrWhiteSpace($OutputDirectory)) { throw '-PatchCopy requires a workspace artifacts OutputDirectory.' }
if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    $artifactsPrefix = (Join-Path $projectRoot 'artifacts') + [IO.Path]::DirectorySeparatorChar
    if (-not $OutputDirectory.StartsWith($artifactsPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Verification outputs must be a named subdirectory under this project artifacts directory.'
    }
    if (-not (Test-Path -LiteralPath $OutputDirectory)) { [void][IO.Directory]::CreateDirectory($OutputDirectory) }
}

# These two assemblies are our tooling, never the actual game assembly.
[void][Reflection.Assembly]::LoadFrom($cecilPath)
$resolver = New-Object Mono.Cecil.DefaultAssemblyResolver
$resolver.AddSearchDirectory((Split-Path -Parent $gamePath))
$parameters = New-Object Mono.Cecil.ReaderParameters
$parameters.InMemory = $true
$parameters.ReadWrite = $false
$parameters.AssemblyResolver = $resolver
$module = [Mono.Cecil.ModuleDefinition]::ReadModule($gamePath, $parameters)
$results = [Collections.Generic.List[object]]::new()
$hookResults = [Collections.Generic.List[object]]::new()
$contractKeys = [Collections.Generic.HashSet[string]]::new()

function Add-ContractResult([string]$Kind, [string]$Owner, [string]$Name, [string]$Expected, [string]$Actual, [bool]$Passed) {
    $results.Add([pscustomobject]@{ Kind = $Kind; Owner = $Owner; Name = $Name; Expected = $Expected; Actual = $Actual; Passed = $Passed })
}

function Find-MetadataMember([string]$Owner, [string]$Name, [string]$Collection) {
    $definition = $module.GetType($Owner)
    while ($null -ne $definition) {
        $members = @($definition.$Collection | Where-Object { $_.Name -eq $Name })
        if ($members.Count -gt 0) { return $members[0] }
        if ($null -eq $definition.BaseType) { break }
        # All inherited members used by the adapter are in Terraria.Entity.
        # Do not resolve external base types merely to search for a missing name.
        $definition = $module.GetType($definition.BaseType.FullName)
    }
    return $null
}

function Test-Field([string]$Owner, [string]$Name, [string]$ExpectedType, [bool]$Static, [bool]$Writable = $false) {
    $key = "field:$Owner`:$Name`:$ExpectedType`:$Static`:$Writable"
    if (-not $contractKeys.Add($key)) { return }
    $field = Find-MetadataMember $Owner $Name 'Fields'
    $expected = "$ExpectedType; static=$Static; writable=$Writable"
    if ($null -eq $field) { Add-ContractResult 'Field' $Owner $Name $expected 'missing' $false; return }
    $actual = "$($field.FieldType.FullName); static=$($field.IsStatic); initonly=$($field.IsInitOnly)"
    $pass = $field.FieldType.FullName -eq $ExpectedType -and $field.IsStatic -eq $Static -and (-not $Writable -or -not $field.IsInitOnly)
    Add-ContractResult 'Field' $Owner $Name $expected $actual $pass
}

function Test-Property([string]$Owner, [string]$Name, [string]$ExpectedType, [bool]$Static, [bool]$Setter) {
    $property = Find-MetadataMember $Owner $Name 'Properties'
    $expected = "$ExpectedType; static=$Static; setter=$Setter"
    if ($null -eq $property) { Add-ContractResult 'Property' $Owner $Name $expected 'missing' $false; return }
    $accessor = if ($Setter) { $property.SetMethod } else { $property.GetMethod }
    $pass = $property.PropertyType.FullName -eq $ExpectedType -and $null -ne $accessor -and $accessor.IsStatic -eq $Static
    Add-ContractResult 'Property' $Owner $Name $expected $property.FullName $pass
}

function Test-Method([string]$Owner, [string]$Name, [string[]]$ParameterTypes, [string]$ReturnType, [bool]$Static, [bool]$PublicOnly = $false) {
    $definition = $module.GetType($Owner)
    $signature = $ParameterTypes -join ','
    $matches = @($definition.Methods | Where-Object {
        $_.Name -eq $Name -and $_.Parameters.Count -eq $ParameterTypes.Count -and
        (($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ',') -eq $signature
    })
    $expected = "$ReturnType ($signature); static=$Static; publicOnly=$PublicOnly"
    $pass = $matches.Count -eq 1 -and $matches[0].ReturnType.FullName -eq $ReturnType -and $matches[0].IsStatic -eq $Static -and (-not $PublicOnly -or $matches[0].IsPublic)
    $actual = if ($matches.Count -eq 1) { $matches[0].FullName } else { "matching overloads=$($matches.Count)" }
    Add-ContractResult 'Method' $Owner $Name $expected $actual $pass
}

function Is-MetadataCall($Instruction, [string]$Owner, [string]$Name) {
    return $Instruction.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call, [Mono.Cecil.Cil.Code]::Callvirt) -and
        $Instruction.Operand -is [Mono.Cecil.MethodReference] -and
        $Instruction.Operand.DeclaringType.FullName -eq $Owner -and $Instruction.Operand.Name -eq $Name
}

try {
    $source = Get-Content -LiteralPath $facadePath -Raw -Encoding UTF8
    $owners = @{
        _mainType = 'Terraria.Main'; playerType = 'Terraria.Player'; npcType = 'Terraria.NPC'
        projectileType = 'Terraria.Projectile'; itemType = 'Terraria.Item'; entityType = 'Terraria.Entity'
        worldGenType = 'Terraria.WorldGen'; mountType = 'Terraria.Mount'; tileType = 'Terraria.Tile'
        mountDataType = 'Terraria.Mount/MountData'
    }
    $types = @{ bool = 'System.Boolean'; byte = 'System.Byte'; int = 'System.Int32'; float = 'System.Single'; double = 'System.Double'
        ushort = 'System.UInt16'; 'bool[]' = 'System.Boolean[]'; 'float[]' = 'System.Single[]' }
    $pattern = 'ReflectionAccess\.(?<op>Getter|Setter|StaticGetter|StaticSetter|PropertyGetter|PropertySetter|StaticPropertyGetter|MethodGetter)<(?<type>[^>]+)>\((?<owner>\w+),\s*"(?<name>[^"]+)"\)'
    $bindings = [regex]::Matches($source, $pattern)
    $checkedBindings = 0
    foreach ($binding in $bindings) {
        $ownerVariable = $binding.Groups['owner'].Value
        if (-not $owners.ContainsKey($ownerVariable)) { throw "Unmapped literal reflection binding owner: $ownerVariable" }
        $owner = $owners[$ownerVariable]
        $name = $binding.Groups['name'].Value
        $operation = $binding.Groups['op'].Value
        $type = $binding.Groups['type'].Value
        $expectedType = $types[$type]
        if ($type -eq 'object') {
            $expectedType = if ($name -eq 'mount') { 'Terraria.Mount' } else { 'Terraria.Item' }
        }
        if ([string]::IsNullOrWhiteSpace($expectedType)) { throw "Unmapped reflection generic type: $type" }
        if ($operation -eq 'MethodGetter') {
            Test-Method -Owner $owner -Name $name -ParameterTypes @() -ReturnType $expectedType -Static $false
        } elseif ($operation -match 'Property') {
            Test-Property $owner $name $expectedType ($operation.StartsWith('Static')) ($operation.EndsWith('Setter'))
        } else {
            Test-Field $owner $name $expectedType ($operation.StartsWith('Static')) ($operation.EndsWith('Setter'))
        }
        $checkedBindings++
    }
    if ($checkedBindings -lt 90) { throw "Unexpectedly low source-derived binding coverage: $checkedBindings" }

    # Remaining constructor bindings are arrays, vector fields and dynamic control names.
    Test-Field 'Terraria.Main' 'npc' 'Terraria.NPC[]' $true
    Test-Field 'Terraria.Main' 'projectile' 'Terraria.Projectile[]' $true
    Test-Field 'Terraria.Main' 'tile' 'Terraria.Tile[0...,0...]' $true
    Test-Field 'Terraria.Player' 'inventory' 'Terraria.Item[]' $false
    Test-Field 'Terraria.Player' 'selectedItemState' 'Terraria.Player/SelectedItemState' $false $true
    Test-Field 'Terraria.Mount' 'mounts' 'Terraria.Mount/MountData[]' $true
    foreach ($entry in @(@('Terraria.Main','npc'), @('Terraria.Main','projectile'), @('Terraria.Player','inventory'), @('Terraria.Mount','mounts'))) {
        $array = Find-MetadataMember $entry[0] $entry[1] 'Fields'
        $element = $array.FieldType.ElementType.Resolve()
        Add-ContractResult 'ReferenceArray' $entry[0] $entry[1] 'reference-type elements for object[] covariance' $element.FullName (-not $element.IsValueType)
    }
    Test-Field 'Terraria.Entity' 'position' 'Microsoft.Xna.Framework.Vector2' $false
    Test-Field 'Terraria.Entity' 'velocity' 'Microsoft.Xna.Framework.Vector2' $false
    Test-Field 'Terraria.Main' 'screenPosition' 'Microsoft.Xna.Framework.Vector2' $true
    Test-Field 'Terraria.Item' 'stack' 'System.Int32' $false $true
    Test-Field 'Terraria.Item' 'favorited' 'System.Boolean' $false $true
    Test-Field 'Terraria.Player' 'releaseThrow' 'System.Boolean' $false $true
    Test-Field 'Terraria.Tile' 'liquid' 'System.Byte' $false
    foreach ($name in @('controlLeft','controlRight','controlUp','controlDown','controlJump','controlUseItem',
        'controlUseTile','controlHook','controlQuickHeal','controlQuickMana','controlThrow','controlMount','controlDash')) {
        Test-Field 'Terraria.Player' $name 'System.Boolean' $false $true
    }
    $vectorField = Find-MetadataMember 'Terraria.Entity' 'position' 'Fields'
    $vectorDefinition = $vectorField.FieldType.Resolve() # Metadata resolution only, not CLR loading.
    foreach ($component in @('X', 'Y')) {
        $field = @($vectorDefinition.Fields | Where-Object { $_.Name -eq $component })
        $pass = $field.Count -eq 1 -and $field[0].FieldType.FullName -eq 'System.Single' -and -not $field[0].IsStatic
        Add-ContractResult 'VectorComponent' $vectorDefinition.FullName $component 'System.Single; instance' ($field.FullName -join ';') $pass
    }
    Test-Method -Owner 'Terraria.Item' -Name 'SetDefaults' -ParameterTypes @('System.Int32','Terraria.GameContent.Items.ItemVariant') -ReturnType 'System.Void' -Static $false -PublicOnly $true
    Test-Method -Owner 'Terraria.Collision' -Name 'CanHitLine' -ParameterTypes @('Microsoft.Xna.Framework.Vector2','System.Int32','System.Int32','Microsoft.Xna.Framework.Vector2','System.Int32','System.Int32') -ReturnType 'System.Boolean' -Static $true -PublicOnly $true
    Test-Method -Owner 'Terraria.Main' -Name 'NewText' -ParameterTypes @('System.String','System.Byte','System.Byte','System.Byte') -ReturnType 'System.Void' -Static $true -PublicOnly $true
    Test-Method -Owner 'Terraria.Player' -Name 'Update' -ParameterTypes @('System.Int32') -ReturnType 'System.Void' -Static $false
    Test-Method -Owner 'Terraria.Player' -Name 'HandleHotbarControls' -ParameterTypes @() -ReturnType 'System.Void' -Static $false
    Test-Method -Owner 'Terraria.Player/SelectedItemState' -Name 'Select' -ParameterTypes @('System.Int32') -ReturnType 'System.Void' -Static $false
    Test-Method -Owner 'Terraria.Player' -Name 'PickAmmo_PickAmmoItem' -ParameterTypes @('Terraria.Item') -ReturnType 'Terraria.Item' -Static $false
    Test-Field 'Terraria.ID.ContentSamples' 'ProjectilesByType' 'System.Collections.Generic.Dictionary`2<System.Int32,Terraria.Projectile>' $true $false
    Test-Method -Owner 'Terraria.GameInput.TriggersSet' -Name 'CopyInto' -ParameterTypes @('Terraria.Player') -ReturnType 'System.Void' -Static $false
    Test-Method -Owner 'Terraria.NPC' -Name 'NPCLoot' -ParameterTypes @() -ReturnType 'System.Void' -Static $false
    $selectionType = $module.GetType('Terraria.Player/SelectedItemState')
    Add-ContractResult 'ValueType' 'Terraria.Player' 'selectedItemState' 'struct mutated by address, not boxed copy' ([string]$selectionType.IsValueType) $selectionType.IsValueType
    $selectionBridge = [regex]::IsMatch($source, 'ReflectionAccess\.StructMethodSetter<int>\(\s*playerType,\s*"selectedItemState",\s*"Select"\s*\)')
    Add-ContractResult 'SourceBridge' 'TerrariaFacade' '_setSelectedItem' 'StructMethodSetter<int> through selectedItemState.Select' ([string]$selectionBridge) $selectionBridge
    if ($source.Contains('"DropSelectedItem"')) {
        Test-Method -Owner 'Terraria.Player' -Name 'DropSelectedItem' -ParameterTypes @() -ReturnType 'System.Void' -Static $false -PublicOnly $true
    }

    $update = @($module.GetType('Terraria.Player').Methods | Where-Object { $_.Name -eq 'Update' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Int32' })[0]
    foreach ($entry in @(@('Terraria.GameInput.TriggersSet','CopyInto'), @('Terraria.Player','HandleHotbarControls'))) {
        $calls = @($update.Body.Instructions | Where-Object { Is-MetadataCall $_ $entry[0] $entry[1] })
        Add-ContractResult 'CallSite' 'Terraria.Player.Update' ($entry -join '.') 'exactly one native call' ([string]$calls.Count) ($calls.Count -eq 1)
    }
    $failed = @($results | Where-Object { -not $_.Passed })
    if ($failed.Count -gt 0) {
        $failed | Format-Table Kind, Owner, Name, Expected, Actual -AutoSize | Out-String | Write-Output
        throw "API contract failures: $($failed.Count) / $($results.Count)"
    }
    Write-Output "PASS API contract: $($results.Count) checks; $checkedBindings literal bindings extracted from current TerrariaFacade.cs"

    if ($PatchCopy) {
        $copyPath = Join-Path $OutputDirectory 'Terraria.vanilla.readonly-copy.exe'
        $patchedPath = Join-Path $OutputDirectory 'Terraria.chaite.four-hook-copy.exe'
        foreach ($output in @($copyPath, $patchedPath)) {
            if (Test-Path -LiteralPath $output) { throw "Verification output already exists; preserve it and choose a fresh directory: $output" }
        }
        Copy-Item -LiteralPath $gamePath -Destination $copyPath
        if ((Get-FileHash -LiteralPath $copyPath -Algorithm SHA256).Hash -ne $sourceHashBefore) { throw 'Source copy SHA-256 mismatch.' }
        [void][Reflection.Assembly]::LoadFrom($patcherPath)
        $patcher = New-Object Chaite.Patcher.InstallationService
        $patcher.PatchCopyForTest($copyPath, $pluginPath, $patchedPath)
        $patched = [Mono.Cecil.ModuleDefinition]::ReadModule($patchedPath)
        try {
            $patchedUpdate = @($patched.GetType('Terraria.Player').Methods | Where-Object { $_.Name -eq 'Update' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Int32' })[0]
            $patchedLoot = @($patched.GetType('Terraria.NPC').Methods | Where-Object { $_.Name -eq 'NPCLoot' -and $_.Parameters.Count -eq 0 })[0]
            foreach ($name in @('Tick','ApplyPendingInput','ApplyPendingSelection','OnNpcKilled')) {
                $method = if ($name -eq 'OnNpcKilled') { $patchedLoot } else { $patchedUpdate }
                $calls = @($method.Body.Instructions | Where-Object { Is-MetadataCall $_ 'Chaite.Plugin.Runtime' $name })
                if ($calls.Count -ne 1) { throw "Hook count mismatch: $name = $($calls.Count)" }
                $hookResults.Add([pscustomobject]@{ Hook = $name; Count = $calls.Count; Method = $method.FullName; Offset = ('IL_{0:X4}' -f $calls[0].Offset) })
            }
            foreach ($entry in @(@('Terraria.GameInput.TriggersSet','CopyInto','ApplyPendingInput'), @('Terraria.Player','HandleHotbarControls','ApplyPendingSelection'))) {
                $call = @($patchedUpdate.Body.Instructions | Where-Object { Is-MetadataCall $_ $entry[0] $entry[1] })[0]
                if ($call.Next.OpCode.Code -ne [Mono.Cecil.Cil.Code]::Ldarg_0 -or -not (Is-MetadataCall $call.Next.Next 'Chaite.Plugin.Runtime' $entry[2])) {
                    throw "Replay hook is not immediately after native $($entry[1])"
                }
            }
            if (@($patched.AssemblyReferences | Where-Object { $_.Name -eq 'Chaite.Plugin' }).Count -ne 1) { throw 'Plugin reference is not unique.' }
            if ((Get-FileHash -LiteralPath $copyPath -Algorithm SHA256).Hash -ne $sourceHashBefore) { throw 'Read-only source copy changed during patching.' }
            Write-Output 'PASS workspace patch copy: entry Tick + post-input + post-hotbar + loot, each exactly once; source copy unchanged'
        } finally { $patched.Dispose() }
    }

    $sourceHashAfter = (Get-FileHash -LiteralPath $gamePath -Algorithm SHA256).Hash
    if ($sourceHashBefore -ne $sourceHashAfter) { throw 'Live game SHA-256 changed during verification.' }
    $report = [pscustomobject]@{
        VerifiedUtc = [DateTime]::UtcNow.ToString('o'); MetadataOnly = $true; GameExecuted = $false
        LiveGamePath = $gamePath; SourceSha256Before = $sourceHashBefore; SourceSha256After = $sourceHashAfter
        SourceUnchanged = $sourceHashBefore -eq $sourceHashAfter; FacadeSourceSha256 = $facadeSourceHash
        PluginSha256 = (Get-FileHash -LiteralPath $pluginPath -Algorithm SHA256).Hash
        ContractChecks = $results.Count; LiteralSourceBindings = $checkedBindings; Contracts = $results.ToArray()
        FourHookCopyVerified = [bool]$PatchCopy; Hooks = $hookResults.ToArray()
    }
    if (-not [string]::IsNullOrWhiteSpace($OutputDirectory)) {
        $reportPath = Join-Path $OutputDirectory 'api-contract-and-hooks.json'
        [IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
        Write-Output "Report: $reportPath"
    }
    Write-Output "PASS live source unchanged: $sourceHashAfter"
} finally {
    $module.Dispose()
    $resolver.Dispose()
}

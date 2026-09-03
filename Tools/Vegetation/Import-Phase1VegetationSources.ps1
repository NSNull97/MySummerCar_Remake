[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SourceProjectAssets,

    [Parameter(Mandatory = $true)]
    [string]$SpruceFbxDirectory,

    [Parameter(Mandatory = $true)]
    [string]$SpruceTextureDirectory,

    [Parameter(Mandatory = $false)]
    [string]$DestinationProject = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
)

$ErrorActionPreference = 'Stop'

function Resolve-ExistingDirectory {
    param([string]$Path, [string]$Label)

    $resolved = [IO.Path]::GetFullPath($Path)
    if (-not (Test-Path -LiteralPath $resolved -PathType Container)) {
        throw "$Label does not exist: $resolved"
    }

    return $resolved.TrimEnd(
        [IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar)
}

function Get-GuidMap {
    param([string]$AssetsRoot)

    $result = @{}
    Get-ChildItem -LiteralPath $AssetsRoot -Recurse -File -Filter '*.meta' |
        ForEach-Object {
            $guidLine = Get-Content -LiteralPath $_.FullName -TotalCount 2 |
                Where-Object { $_ -match '^guid:\s*([0-9a-fA-F]{32})' } |
                Select-Object -First 1
            if ($guidLine -match '^guid:\s*([0-9a-fA-F]{32})') {
                $assetPath = $_.FullName.Substring(0, $_.FullName.Length - 5)
                $result[$Matches[1].ToLowerInvariant()] = $assetPath
            }
        }

    return $result
}

function Get-DependencyClosure {
    param(
        [string[]]$Seeds,
        [hashtable]$GuidMap
    )

    $queue = [Collections.Generic.Queue[string]]::new()
    foreach ($seed in $Seeds) {
        $queue.Enqueue($seed)
    }

    $seen = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $unresolved = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    $textExtensions = [Collections.Generic.HashSet[string]]::new(
        [StringComparer]::OrdinalIgnoreCase)
    foreach ($extension in @(
        '.prefab',
        '.mat',
        '.asset',
        '.meta',
        '.shader',
        '.shadergraph',
        '.shadersubgraph',
        '.controller',
        '.anim',
        '.overridecontroller',
        '.compute',
        '.cginc',
        '.hlsl')) {
        [void]$textExtensions.Add($extension)
    }

    while ($queue.Count -gt 0) {
        $assetPath = $queue.Dequeue()
        if (-not $seen.Add($assetPath)) {
            continue
        }
        if (-not (Test-Path -LiteralPath $assetPath -PathType Leaf)) {
            throw "Selected vegetation asset is missing: $assetPath"
        }

        foreach ($candidate in @($assetPath, "$assetPath.meta")) {
            if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) {
                continue
            }
            if (-not $textExtensions.Contains(
                    [IO.Path]::GetExtension($candidate))) {
                continue
            }

            $content = Get-Content -LiteralPath $candidate -Raw
            foreach ($match in [regex]::Matches(
                    $content,
                    'guid:\s*([0-9a-fA-F]{32})')) {
                $guid = $match.Groups[1].Value.ToLowerInvariant()
                if ($GuidMap.ContainsKey($guid)) {
                    $queue.Enqueue($GuidMap[$guid])
                }
                else {
                    [void]$unresolved.Add($guid)
                }
            }
        }
    }

    return @{
        Assets = @($seen)
        UnresolvedGuids = @($unresolved)
    }
}

function Copy-AssetWithMeta {
    param(
        [string]$SourceAsset,
        [string]$SourceAssetsRoot,
        [string]$DestinationAssetsRoot
    )

    $normalizedSourceRoot = $SourceAssetsRoot.TrimEnd('\', '/') + '\'
    if (-not $SourceAsset.StartsWith(
            $normalizedSourceRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Asset escapes the configured source Assets root: $SourceAsset"
    }
    $relative = $SourceAsset.Substring($normalizedSourceRoot.Length)
    $destination = Join-Path $DestinationAssetsRoot $relative
    $destinationDirectory = Split-Path -Parent $destination
    [IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
    Copy-Item -LiteralPath $SourceAsset -Destination $destination -Force

    $sourceMeta = "$SourceAsset.meta"
    if (Test-Path -LiteralPath $sourceMeta -PathType Leaf) {
        Copy-Item -LiteralPath $sourceMeta -Destination "$destination.meta" -Force
    }

    return $destination
}

function Get-Sha256 {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$sourceAssets = Resolve-ExistingDirectory $SourceProjectAssets 'Source project Assets'
$spruceFbx = Resolve-ExistingDirectory $SpruceFbxDirectory 'Spruce FBX directory'
$spruceTextures = Resolve-ExistingDirectory $SpruceTextureDirectory 'Spruce texture directory'
$projectRoot = Resolve-ExistingDirectory $DestinationProject 'Destination project'
$destinationAssets = Resolve-ExistingDirectory (Join-Path $projectRoot 'Assets') 'Destination Assets'

$chernobylVegetation = Join-Path $sourceAssets 'Chernobyl\Prefabs\Vegetation'
$natureManufacture = Join-Path $sourceAssets 'NatureManufacture Assets'
if (-not (Test-Path -LiteralPath $chernobylVegetation -PathType Container)) {
    throw "Chernobyl vegetation package is missing: $chernobylVegetation"
}
if (-not (Test-Path -LiteralPath $natureManufacture -PathType Container)) {
    throw "NatureManufacture package is missing: $natureManufacture"
}

$seeds = @()
$seeds += Get-ChildItem -LiteralPath (Join-Path $chernobylVegetation 'Tree') `
    -File -Filter '*.prefab' |
    Where-Object { $_.BaseName -match '^(Pine|Birch|Aspen)_' } |
    Select-Object -ExpandProperty FullName
$seeds += Get-ChildItem -LiteralPath $chernobylVegetation -File -Filter '*.prefab' |
    Where-Object { $_.BaseName -match '^(Small_Tree|Bushes|Fern|Grass)_' } |
    Select-Object -ExpandProperty FullName

$natureSeeds = @(
    'Forest Environment Dynamic Nature\Foliage and Grass\Prefabs\prefab_fern_01_1.prefab',
    'Forest Environment Dynamic Nature\Foliage and Grass\Prefabs\prefab_fern_01_2.prefab',
    'Forest Environment Dynamic Nature\Foliage and Grass\Prefabs\prefab_grass_01_1.prefab',
    'Forest Environment Dynamic Nature\Foliage and Grass\Prefabs\prefab_grass_02_1.prefab',
    'Forest Environment Dynamic Nature\Foliage and Grass\Prefabs\prefab_grass_03_1.prefab',
    'Meadow Environment Dynamic Nature\Bushes\Prefabs\prefab_grey_willow_01.prefab',
    'Meadow Environment Dynamic Nature\Bushes\Prefabs\prefab_grey_willow_02.prefab',
    'Meadow Environment Dynamic Nature\Grass\Prefabs Grass\prefab_grass_meadow_01_1.prefab',
    'Meadow Environment Dynamic Nature\Grass\Prefabs Grass\prefab_grass_meadow_02_2.prefab',
    'Meadow Environment Dynamic Nature\Details\Prefabs\prefab_detail_meadow_clover_01.prefab',
    'Meadow Environment Dynamic Nature\Details\Prefabs\prefab_detail_meadow_dead_grass_01.prefab',
    'Meadow Environment Dynamic Nature\Details\Prefabs\prefab_detail_meadow_grass_01.prefab'
)
$seeds += $natureSeeds | ForEach-Object {
    Join-Path $natureManufacture $_
}
$seeds += Join-Path $natureManufacture `
    'Foliage Shaders\NM_Foliage_VSPro_Indirect.cginc'

$guidMap = Get-GuidMap $sourceAssets
$closure = Get-DependencyClosure $seeds $guidMap
$copied = @()
foreach ($asset in $closure.Assets | Sort-Object) {
    $copied += Copy-AssetWithMeta $asset $sourceAssets $destinationAssets
}

$spruceDestination = Join-Path $destinationAssets `
    'Game\Presentation\Vegetation\ThirdParty\SprucePack\Source'
[IO.Directory]::CreateDirectory((Join-Path $spruceDestination 'Models')) | Out-Null
[IO.Directory]::CreateDirectory((Join-Path $spruceDestination 'Textures')) | Out-Null

foreach ($model in Get-ChildItem -LiteralPath $spruceFbx -File -Filter '*.fbx') {
    $target = Join-Path (Join-Path $spruceDestination 'Models') $model.Name
    Copy-Item -LiteralPath $model.FullName -Destination $target -Force
    $copied += $target
}
foreach ($texture in Get-ChildItem -LiteralPath $spruceTextures -File |
        Where-Object { $_.Extension -in '.png', '.tga', '.TGA' }) {
    $target = Join-Path (Join-Path $spruceDestination 'Textures') $texture.Name
    Copy-Item -LiteralPath $texture.FullName -Destination $target -Force
    $copied += $target
}

$manifestDirectory = Join-Path $projectRoot 'Artifacts\VegetationImport'
[IO.Directory]::CreateDirectory($manifestDirectory) | Out-Null
$manifestPath = Join-Path $manifestDirectory 'Phase1VegetationSourceInventory.json'
$records = foreach ($path in $copied | Sort-Object -Unique) {
    $normalizedProjectRoot = $projectRoot.TrimEnd('\', '/') + '\'
    if (-not $path.StartsWith(
            $normalizedProjectRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "Imported path escapes the destination project: $path"
    }
    $relativeProjectPath = $path.Substring($normalizedProjectRoot.Length)
    [ordered]@{
        projectRelativePath = $relativeProjectPath.Replace('\', '/')
        bytes = (Get-Item -LiteralPath $path).Length
        sha256 = Get-Sha256 $path
    }
}

[ordered]@{
    schemaVersion = 1
    classification = 'LicensedThirdPartyPhase1Presentation'
    sourceSeedCount = $seeds.Count
    importedAssetCount = $records.Count
    unresolvedExternalGuidCount = $closure.UnresolvedGuids.Count
    unresolvedExternalGuids = @($closure.UnresolvedGuids | Sort-Object)
    assets = @($records)
} | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath $manifestPath -Encoding UTF8

Write-Host (
    'PHASE1_VEGETATION_SOURCE_IMPORT_OK ' +
    "seeds=$($seeds.Count) assets=$($records.Count) " +
    "unresolvedExternalGuids=$($closure.UnresolvedGuids.Count) " +
    "manifest=$manifestPath")

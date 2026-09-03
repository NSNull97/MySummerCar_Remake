param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [string]$ProjectRoot = '',

    [string]$ExpectedSha256 =
        '1BF7B7B503BF2DD5B648C306E21230FBDC41D13610961F0A75D65EA30D8A1063'
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
}

if (-not (Test-Path -LiteralPath $PackagePath -PathType Leaf)) {
    throw "Fire001 package was not found: $PackagePath"
}

$actualHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash
if (-not [string]::Equals(
        $actualHash,
        $ExpectedSha256,
        [StringComparison]::OrdinalIgnoreCase)) {
    throw "Fire001 package hash mismatch. Expected $ExpectedSha256; actual $actualHash."
}

$temporaryRoot = Join-Path (
    [IO.Path]::GetTempPath()) (
    'msc-fire001-' + $actualHash.Substring(0, 12).ToLowerInvariant())
if (-not (Test-Path -LiteralPath $temporaryRoot)) {
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    & tar -xf $PackagePath -C $temporaryRoot
    if ($LASTEXITCODE -ne 0) {
        throw "tar failed to extract Fire001 package with exit code $LASTEXITCODE."
    }
}

$entriesByPath = @{}
foreach ($directory in Get-ChildItem -LiteralPath $temporaryRoot -Directory) {
    $pathnameFile = Join-Path $directory.FullName 'pathname'
    if (-not (Test-Path -LiteralPath $pathnameFile -PathType Leaf)) {
        continue
    }

    $sourcePath = (Get-Content -Raw -LiteralPath $pathnameFile).Trim()
    $entriesByPath[$sourcePath] = $directory.FullName
}

$destinationRoot = Join-Path $ProjectRoot (
    'Assets\Game\Presentation\Fire\ThirdParty\N2StudioFire001')
$resourceRoot = Join-Path $destinationRoot 'Resources\MSC\Fire'
New-Item -ItemType Directory -Force -Path $resourceRoot | Out-Null

function Copy-UnityPackageAsset {
    param(
        [Parameter(Mandatory = $true)][string]$SourcePath,
        [Parameter(Mandatory = $true)][string]$DestinationPath
    )

    $entryRoot = $entriesByPath[$SourcePath]
    if ([string]::IsNullOrWhiteSpace($entryRoot)) {
        throw "Required Fire001 package entry is missing: $SourcePath"
    }

    Copy-Item -LiteralPath (Join-Path $entryRoot 'asset') `
        -Destination $DestinationPath -Force
    Copy-Item -LiteralPath (Join-Path $entryRoot 'asset.meta') `
        -Destination ($DestinationPath + '.meta') -Force
}

Copy-UnityPackageAsset `
    -SourcePath 'Assets/N2Studio/Textures/FireSeq1.png' `
    -DestinationPath (Join-Path $resourceRoot 'FireSeq1.png')
Copy-UnityPackageAsset `
    -SourcePath 'Assets/N2Studio/Textures/Smoke1.png' `
    -DestinationPath (Join-Path $resourceRoot 'Smoke1.png')
Copy-UnityPackageAsset `
    -SourcePath 'Assets/N2Studio/Textures/PointGlow.png' `
    -DestinationPath (Join-Path $resourceRoot 'PointGlow.png')
Copy-UnityPackageAsset `
    -SourcePath 'Assets/N2Studio/Third-Party Notices.txt' `
    -DestinationPath (Join-Path $destinationRoot 'Third-Party Notices.txt')

$prefabSourcePath = 'Assets/N2Studio/Prefabs/Fire001.prefab'
$prefabEntry = $entriesByPath[$prefabSourcePath]
if ([string]::IsNullOrWhiteSpace($prefabEntry)) {
    throw "Required Fire001 prefab is missing: $prefabSourcePath"
}

$prefabText = [IO.File]::ReadAllText((Join-Path $prefabEntry 'asset'))
foreach ($materialGuid in @(
        'ae239902338cb504d993c27397815a0b',
        'c791ab1d220711e499d2487e0b3e1524',
        '1beada38001c57045ac8001aa23003c6',
        'e823cd5b5d27c0f4b8256e7c12ee3e6d')) {
    $prefabText = $prefabText.Replace(
        "{fileID: 2100000, guid: $materialGuid, type: 2}",
        '{fileID: 0}')
}

$prefabDestination = Join-Path $resourceRoot 'Fire001.prefab'
$utf8WithoutBom = New-Object Text.UTF8Encoding($false)
[IO.File]::WriteAllText($prefabDestination, $prefabText, $utf8WithoutBom)
Copy-Item -LiteralPath (Join-Path $prefabEntry 'asset.meta') `
    -Destination ($prefabDestination + '.meta') -Force

$remainingMaterialReferences = [regex]::Matches(
    $prefabText,
    'guid: (ae239902338cb504d993c27397815a0b|c791ab1d220711e499d2487e0b3e1524|1beada38001c57045ac8001aa23003c6|e823cd5b5d27c0f4b8256e7c12ee3e6d)')
if ($remainingMaterialReferences.Count -ne 0) {
    throw 'Fire001 prefab still contains references to incompatible Nova materials.'
}

$particleSystemCount = [regex]::Matches(
    $prefabText,
    '(?m)^--- !u!198 ').Count
if ($particleSystemCount -ne 6) {
    throw "Fire001 prefab particle-layer count changed: $particleSystemCount (expected 6)."
}

Write-Output "FIRE001_IMPORT_OK hash=$actualHash destination=$destinationRoot"
Write-Output "Selected payload: $particleSystemCount-layer prefab + FireSeq1/Smoke1/PointGlow + third-party notice."
Write-Output 'Nova shader/editor/runtime package intentionally excluded; HDRP materials are runtime-owned.'

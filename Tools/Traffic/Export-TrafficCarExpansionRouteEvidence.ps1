[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $DllPath,

    [Parameter(Mandatory = $true)]
    [string] $ExportedRoutesPrefabPath,

    [string] $ManifestPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$LockedDllSha256 =
    "7b626ca489a4abd7acdd34ca987199e6a27dd5bcd6cd398bab441360471313f5"
$LockedBundleSha256 =
    "adc45a6b161834ab88cb507d51d529ffff09687e0781c84b7ac5749b10e2bcaa"
$LockedPrefabSha256 =
    "76854020dff8d9562a88d9fd77e36155aa8c185f8a11b71e436db7a5af878752"
$EmbeddedResourcesName =
    "TrafficCarExpansion.Properties.Resources.resources"
$EmbeddedBundleKey = "bundle"
$ToolVersion = "traffic-car-expansion-route-evidence-v2"

$InvariantCulture = [Globalization.CultureInfo]::InvariantCulture
$Utf8WithoutBom = [Text.UTF8Encoding]::new($false)
$HeaderPattern =
    [Text.RegularExpressions.Regex]::new(
        '^--- !u!(?<classId>\d+) &(?<fileId>-?\d+)$',
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)

function Get-NormalizedSha256 {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    return (Get-FileHash -Algorithm SHA256 -LiteralPath $LiteralPath).
        Hash.ToLowerInvariant()
}

function Assert-Hash {
    param(
        [Parameter(Mandatory = $true)][string] $Description,
        [Parameter(Mandatory = $true)][string] $Actual,
        [Parameter(Mandatory = $true)][string] $Expected
    )

    if (-not [string]::Equals(
            $Actual,
            $Expected,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description hash mismatch. Expected $Expected, found $Actual."
    }
}

function Get-EmbeddedBundleBytes {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $assembly = [Reflection.Assembly]::LoadFile($LiteralPath)
    $resourceNames = @($assembly.GetManifestResourceNames())
    if ($resourceNames.Count -ne 1 -or
        $resourceNames[0] -ne $EmbeddedResourcesName) {
        throw "Locked TrafficCarExpansion managed-resource table changed."
    }

    $stream = $assembly.GetManifestResourceStream($EmbeddedResourcesName)
    if ($null -eq $stream) {
        throw "Embedded TrafficCarExpansion resource stream is unavailable."
    }

    try {
        $reader = [Resources.ResourceReader]::new($stream)
        try {
            $entries = @{}
            $enumerator = $reader.GetEnumerator()
            while ($enumerator.MoveNext()) {
                $entries[[string] $enumerator.Key] = $enumerator.Value
            }

            if ($entries.Count -ne 1 -or
                -not $entries.ContainsKey($EmbeddedBundleKey) -or
                $entries[$EmbeddedBundleKey] -isnot [byte[]]) {
                throw "Locked TrafficCarExpansion embedded bundle changed."
            }

            return [byte[]] $entries[$EmbeddedBundleKey]
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

function Get-ByteArraySha256 {
    param([Parameter(Mandatory = $true)][byte[]] $Bytes)

    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        return [BitConverter]::ToString($sha.ComputeHash($Bytes)).
            Replace("-", "").ToLowerInvariant()
    }
    finally {
        $sha.Dispose()
    }
}

function Get-RequiredMatch {
    param(
        [Parameter(Mandatory = $true)][string] $Text,
        [Parameter(Mandatory = $true)][string] $Pattern,
        [Parameter(Mandatory = $true)][string] $Description
    )

    $match = [Text.RegularExpressions.Regex]::Match(
        $Text,
        $Pattern,
        [Text.RegularExpressions.RegexOptions]::Multiline -bor
        [Text.RegularExpressions.RegexOptions]::CultureInvariant)
    if (-not $match.Success) {
        throw "Could not read $Description from the locked route prefab."
    }

    return $match
}

function ConvertTo-InvariantDouble {
    param([Parameter(Mandatory = $true)][string] $Value)

    return [double]::Parse(
        $Value,
        [Globalization.NumberStyles]::Float,
        $InvariantCulture)
}

function Read-PrefabRecords {
    param([Parameter(Mandatory = $true)][string] $LiteralPath)

    $gameObjects = @{}
    # AssetRipper 1.3.14 preserves two duplicate Transform file IDs in this
    # old serialized prefab. A dictionary would silently discard route points,
    # so retain records in serialized order and resolve by hierarchy/name.
    $transforms = [Collections.ArrayList]::new()
    $currentClassId = 0
    $currentFileId = [long] 0
    $block = [Text.StringBuilder]::new()

    function Add-Block {
        param(
            [int] $ClassId,
            [long] $FileId,
            [string] $Text
        )

        if ([string]::IsNullOrEmpty($Text)) {
            return
        }

        # StreamReader/AppendLine uses CRLF under Windows. Strip CR so the
        # anchored Unity-YAML expressions behave identically in pwsh and in
        # Windows PowerShell 5.1.
        $Text = $Text.Replace("`r", "")

        if ($ClassId -eq 1) {
            $name = Get-RequiredMatch `
                -Text $Text `
                -Pattern '^  m_Name: (?<value>.*)$' `
                -Description "GameObject name"
            $gameObjects[$FileId] = [ordered]@{
                fileId = $FileId
                name = $name.Groups['value'].Value.Trim()
            }
            return
        }

        if ($ClassId -ne 4) {
            return
        }

        $gameObject = Get-RequiredMatch `
            -Text $Text `
            -Pattern '^  m_GameObject: \{fileID: (?<value>-?\d+)\}$' `
            -Description "Transform GameObject reference"
        $position = Get-RequiredMatch `
            -Text $Text `
            -Pattern '^  m_LocalPosition: \{x: (?<x>[^,]+), y: (?<y>[^,]+), z: (?<z>[^}]+)\}$' `
            -Description "Transform local position"
        $rotation = Get-RequiredMatch `
            -Text $Text `
            -Pattern '^  m_LocalRotation: \{x: (?<x>[^,]+), y: (?<y>[^,]+), z: (?<z>[^,]+), w: (?<w>[^}]+)\}$' `
            -Description "Transform local rotation"
        $scale = Get-RequiredMatch `
            -Text $Text `
            -Pattern '^  m_LocalScale: \{x: (?<x>[^,]+), y: (?<y>[^,]+), z: (?<z>[^}]+)\}$' `
            -Description "Transform local scale"
        $father = Get-RequiredMatch `
            -Text $Text `
            -Pattern '^  m_Father: \{fileID: (?<value>-?\d+)\}$' `
            -Description "Transform parent reference"
        $rootOrder = Get-RequiredMatch `
            -Text $Text `
            -Pattern '^  m_RootOrder: (?<value>-?\d+)$' `
            -Description "Transform root order"

        [void] $transforms.Add([ordered]@{
            fileId = $FileId
            gameObjectId = [long] $gameObject.Groups['value'].Value
            fatherId = [long] $father.Groups['value'].Value
            rootOrder = [int] $rootOrder.Groups['value'].Value
            position = [ordered]@{
                x = ConvertTo-InvariantDouble $position.Groups['x'].Value
                y = ConvertTo-InvariantDouble $position.Groups['y'].Value
                z = ConvertTo-InvariantDouble $position.Groups['z'].Value
            }
            rotation = [ordered]@{
                x = ConvertTo-InvariantDouble $rotation.Groups['x'].Value
                y = ConvertTo-InvariantDouble $rotation.Groups['y'].Value
                z = ConvertTo-InvariantDouble $rotation.Groups['z'].Value
                w = ConvertTo-InvariantDouble $rotation.Groups['w'].Value
            }
            scale = [ordered]@{
                x = ConvertTo-InvariantDouble $scale.Groups['x'].Value
                y = ConvertTo-InvariantDouble $scale.Groups['y'].Value
                z = ConvertTo-InvariantDouble $scale.Groups['z'].Value
            }
        })
    }

    foreach ($line in [IO.File]::ReadLines($LiteralPath)) {
        $header = $HeaderPattern.Match($line)
        if ($header.Success) {
            Add-Block `
                -ClassId $currentClassId `
                -FileId $currentFileId `
                -Text $block.ToString()
            $currentClassId = [int] $header.Groups['classId'].Value
            $currentFileId = [long] $header.Groups['fileId'].Value
            [void] $block.Clear()
            continue
        }

        if ($currentClassId -eq 1 -or $currentClassId -eq 4) {
            [void] $block.AppendLine($line)
        }
    }

    Add-Block `
        -ClassId $currentClassId `
        -FileId $currentFileId `
        -Text $block.ToString()

    foreach ($transform in $transforms) {
        if (-not $gameObjects.ContainsKey($transform.gameObjectId)) {
            throw "Transform $($transform.fileId) refers to an unknown GameObject."
        }

        $transform.name = $gameObjects[$transform.gameObjectId].name
    }

    return [ordered]@{
        gameObjects = $gameObjects
        transforms = $transforms
    }
}

function Test-NearlyEqual {
    param([double] $Left, [double] $Right)
    return [Math]::Abs($Left - $Right) -le 0.000001
}

function Assert-IdentityTransform {
    param(
        [Parameter(Mandatory = $true)] $Transform,
        [Parameter(Mandatory = $true)][string] $Description
    )

    if (-not (Test-NearlyEqual $Transform.position.x 0.0) -or
        -not (Test-NearlyEqual $Transform.position.y 0.0) -or
        -not (Test-NearlyEqual $Transform.position.z 0.0) -or
        -not (Test-NearlyEqual $Transform.rotation.x 0.0) -or
        -not (Test-NearlyEqual $Transform.rotation.y 0.0) -or
        -not (Test-NearlyEqual $Transform.rotation.z 0.0) -or
        -not (Test-NearlyEqual $Transform.rotation.w 1.0) -or
        -not (Test-NearlyEqual $Transform.scale.x 1.0) -or
        -not (Test-NearlyEqual $Transform.scale.y 1.0) -or
        -not (Test-NearlyEqual $Transform.scale.z 1.0)) {
        throw "$Description is no longer an identity transform. " +
            "The source-to-project coordinate contract must be re-audited."
    }
}

function Get-UniqueTransform {
    param(
        [Parameter(Mandatory = $true)] $Records,
        [Parameter(Mandatory = $true)][string] $Name,
        [Parameter(Mandatory = $true)][long] $FatherId
    )

    $matches = @($Records.transforms | Where-Object {
        $_.fatherId -eq $FatherId -and $_.name -eq $Name
    })
    if ($matches.Count -ne 1) {
        throw "Expected one '$Name' Transform under $FatherId; found $($matches.Count)."
    }

    return $matches[0]
}

function Get-PlanarDistance {
    param($Left, $Right)
    $dx = [double] $Right.x - [double] $Left.x
    $dz = [double] $Right.z - [double] $Left.z
    return [Math]::Sqrt(($dx * $dx) + ($dz * $dz))
}

function Get-SpatialDistance {
    param($Left, $Right)
    $dx = [double] $Right.x - [double] $Left.x
    $dy = [double] $Right.y - [double] $Left.y
    $dz = [double] $Right.z - [double] $Left.z
    return [Math]::Sqrt(($dx * $dx) + ($dy * $dy) + ($dz * $dz))
}

function Get-PointToPlanarSegmentDistance {
    param($Point, $Start, $End)
    $abx = [double] $End.x - [double] $Start.x
    $abz = [double] $End.z - [double] $Start.z
    $apx = [double] $Point.x - [double] $Start.x
    $apz = [double] $Point.z - [double] $Start.z
    $lengthSquared = ($abx * $abx) + ($abz * $abz)
    if ($lengthSquared -le 0.000000000001) {
        return Get-PlanarDistance $Point $Start
    }

    $t = [Math]::Max(
        0.0,
        [Math]::Min(1.0, (($apx * $abx) + ($apz * $abz)) / $lengthSquared))
    $closest = [ordered]@{
        x = [double] $Start.x + ($abx * $t)
        z = [double] $Start.z + ($abz * $t)
    }
    return Get-PlanarDistance $Point $closest
}

function Get-PolylineLength {
    param([Parameter(Mandatory = $true)] [object[]] $Points)
    $length = 0.0
    for ($index = 1; $index -lt $Points.Count; $index++) {
        $length += Get-SpatialDistance $Points[$index - 1] $Points[$index]
    }
    return $length
}

function Get-MaximumSegmentLength {
    param([Parameter(Mandatory = $true)] [object[]] $Points)
    $maximum = 0.0
    for ($index = 1; $index -lt $Points.Count; $index++) {
        $maximum = [Math]::Max(
            $maximum,
            (Get-SpatialDistance $Points[$index - 1] $Points[$index]))
    }
    return $maximum
}

function Get-LegacyStrideFourMetrics {
    param([Parameter(Mandatory = $true)] [object[]] $Points)

    $keptIndices = [Collections.Generic.List[int]]::new()
    for ($index = 0; $index -lt $Points.Count; $index += 4) {
        $keptIndices.Add($index)
    }
    if ($keptIndices[$keptIndices.Count - 1] -ne $Points.Count - 1) {
        $keptIndices.Add($Points.Count - 1)
    }

    $decimated = @($keptIndices | ForEach-Object { $Points[$_] })
    $maximumDeviation = 0.0
    $maximumDeviationPointIndex = 0
    for ($segment = 1; $segment -lt $keptIndices.Count; $segment++) {
        $startIndex = $keptIndices[$segment - 1]
        $endIndex = $keptIndices[$segment]
        for ($pointIndex = $startIndex + 1;
             $pointIndex -lt $endIndex;
             $pointIndex++) {
            $deviation = Get-PointToPlanarSegmentDistance `
                $Points[$pointIndex] `
                $Points[$startIndex] `
                $Points[$endIndex]
            if ($deviation -gt $maximumDeviation) {
                $maximumDeviation = $deviation
                $maximumDeviationPointIndex = $pointIndex
            }
        }
    }

    return [ordered]@{
        pointCount = $decimated.Count
        polylineLengthMeters = Get-PolylineLength $decimated
        maximumSegmentLengthMeters = Get-MaximumSegmentLength $decimated
        maximumPlanarChordDeviationMeters = $maximumDeviation
        maximumPlanarChordDeviationSourcePointIndex =
            $maximumDeviationPointIndex
    }
}

function ConvertTo-RouteEvidence {
    param(
        [Parameter(Mandatory = $true)] $Records,
        [Parameter(Mandatory = $true)] $Root,
        [Parameter(Mandatory = $true)][string] $RouteId,
        [Parameter(Mandatory = $true)][string] $SourceObjectName,
        [Parameter(Mandatory = $true)][int] $ExpectedPointCount,
        [Parameter(Mandatory = $true)][double] $RoadWidthMeters
    )

    $route = Get-UniqueTransform `
        -Records $Records `
        -Name $SourceObjectName `
        -FatherId $Root.fileId
    Assert-IdentityTransform $route "Route '$SourceObjectName'"

    $children = @($Records.transforms | Where-Object {
        $_.fatherId -eq $route.fileId
    })
    $indexed = @($children | ForEach-Object {
        [pscustomobject][ordered]@{
            # m_RootOrder is authoritative here. AssetRipper's YAML contains
            # duplicate legacy file IDs, so resolving every numeric name via
            # a file-ID dictionary would drop or mislabel three valid points.
            Index = $_.rootOrder
            transform = $_
        }
    } | Sort-Object -Property Index)

    if ($indexed.Count -ne $ExpectedPointCount) {
        throw "Route '$SourceObjectName' contains $($indexed.Count) points; " +
            "expected $ExpectedPointCount."
    }
    for ($index = 0; $index -lt $indexed.Count; $index++) {
        if ($indexed[$index].Index -ne $index) {
            throw "Route '$SourceObjectName' point names are not contiguous " +
                "at $index; found '$($indexed[$index].Index)'."
        }
    }

    $points = @($indexed | ForEach-Object {
        [ordered]@{
            x = $_.transform.position.x
            y = $_.transform.position.y
            z = $_.transform.position.z
        }
    })
    $legacyStrideFour = Get-LegacyStrideFourMetrics $points
    $fullLength = Get-PolylineLength $points

    return [ordered]@{
        routeId = $RouteId
        sourceObjectName = $SourceObjectName
        closesLoop = $false
        surface = "Paved"
        nominalRoadWidthMeters = $RoadWidthMeters
        sourcePointCount = $points.Count
        sourcePointStride = 1
        sourceWorldPoints = $points
        extractionMetrics = [ordered]@{
            firstLastPlanarDistanceMeters =
                Get-PlanarDistance $points[0] $points[$points.Count - 1]
            fullPolylineLengthMeters = $fullLength
            maximumSourceSegmentLengthMeters =
                Get-MaximumSegmentLength $points
            legacyStrideFourPointCount = $legacyStrideFour.pointCount
            legacyStrideFourPolylineLengthMeters =
                $legacyStrideFour.polylineLengthMeters
            legacyStrideFourMaximumSegmentLengthMeters =
                $legacyStrideFour.maximumSegmentLengthMeters
            legacyStrideFourMaximumPlanarChordDeviationMeters =
                $legacyStrideFour.maximumPlanarChordDeviationMeters
            legacyStrideFourMaximumPlanarChordDeviationSourcePointIndex =
                $legacyStrideFour.maximumPlanarChordDeviationSourcePointIndex
        }
    }
}

$resolvedDllPath = (Resolve-Path -LiteralPath $DllPath).Path
$resolvedPrefabPath = (Resolve-Path -LiteralPath $ExportedRoutesPrefabPath).Path
if ([string]::IsNullOrWhiteSpace($ManifestPath)) {
    $repositoryRoot = [IO.Path]::GetFullPath(
        [IO.Path]::Combine($PSScriptRoot, "..", ".."))
    $ManifestPath = [IO.Path]::Combine(
        $repositoryRoot,
        "Assets",
        "Game",
        "LegacyImport",
        "Manifests",
        "TrafficCarExpansionBehaviorEvidence.json")
}
$resolvedManifestPath = [IO.Path]::GetFullPath($ManifestPath)

$dllHash = Get-NormalizedSha256 $resolvedDllPath
Assert-Hash "TrafficCarExpansion DLL" $dllHash $LockedDllSha256
$bundleBytes = Get-EmbeddedBundleBytes $resolvedDllPath
$bundleHash = Get-ByteArraySha256 $bundleBytes
Assert-Hash "TrafficCarExpansion embedded bundle" `
    $bundleHash `
    $LockedBundleSha256
$prefabHash = Get-NormalizedSha256 $resolvedPrefabPath
Assert-Hash "AssetRipper Routes_Modded.prefab" `
    $prefabHash `
    $LockedPrefabSha256

$records = Read-PrefabRecords $resolvedPrefabPath
$root = Get-UniqueTransform `
    -Records $records `
    -Name "Routes_Modded" `
    -FatherId 0
Assert-IdentityTransform $root "Routes_Modded root"

$routes = @(
    ConvertTo-RouteEvidence `
        -Records $records `
        -Root $root `
        -RouteId "route.traffic.mod-town-entry" `
        -SourceObjectName "Route_IntoTown" `
        -ExpectedPointCount 68 `
        -RoadWidthMeters 6.4
    ConvertTo-RouteEvidence `
        -Records $records `
        -Root $root `
        -RouteId "route.traffic.mod-town-loop" `
        -SourceObjectName "Route_TownLoop" `
        -ExpectedPointCount 321 `
        -RoadWidthMeters 6.4
    ConvertTo-RouteEvidence `
        -Records $records `
        -Root $root `
        -RouteId "route.traffic.mod-gas-pump" `
        -SourceObjectName "Route_GasPump" `
        -ExpectedPointCount 112 `
        -RoadWidthMeters 5.5
)

$manifest = [ordered]@{
    schemaVersion = 1
    manifestId = "traffic-car-expansion-behavior-evidence-v1"
    classification = "BehavioralReference;ConfigurationTransferred"
    source = [ordered]@{
        fileName = "TrafficCarExpansion.dll"
        sha256 = $dllHash
        embeddedResourceName = $EmbeddedBundleKey
        extractedBundleSha256 = $bundleHash
        routePrefabName = "Routes_Modded.prefab"
        routePrefabSha256 = $prefabHash
        extractionToolVersion = $ToolVersion
        note = "Third-party mod supplied by the user; coordinates are editor-only configuration evidence. No DLL, code, prefab, or asset-bundle dependency exists at runtime."
    }
    sourceToProjectTranslation = [ordered]@{
        x = 169.98
        y = 1.611
        z = -1040.625
    }
    coordinateContract = [ordered]@{
        sourceSpace = "AssetRipper prefab world space"
        sourceRootIsIdentity = $true
        routeRootsAreIdentity = $true
        projectPointFormula = "sourceWorldPoint + sourceToProjectTranslation"
        pointOrder = "direct-child serialized m_RootOrder in ascending contiguous order; numeric child names are corroborating evidence"
    }
    routes = $routes
    behavior = [ordered]@{
        decisionIntervalSeconds = 90
        townExcursionChancePercent = 4
        eligibleLanePosition = -2
        townLoopChancePercent = 65
        gasPumpChancePercent = 35
        gasPumpOccupancyCheck = $true
        gasPumpWaitUntilFree = $true
        gasPumpApproachSpeedKmh = "10-25"
        gasPumpLaneSpeedKmh = "5-10"
        townLoopSpeedKmh = "25-55"
        turnSignalsAtJunctions = $true
    }
    provenanceNote = "All contiguous source points were extracted from the embedded Routes_Modded prefab. Runtime behavior is independently reimplemented in project-owned code."
}

$json = $manifest | ConvertTo-Json -Depth 20 -Compress
[IO.Directory]::CreateDirectory(
    [IO.Path]::GetDirectoryName($resolvedManifestPath)) | Out-Null
[IO.File]::WriteAllText(
    $resolvedManifestPath,
    $json + [Environment]::NewLine,
    $Utf8WithoutBom)

Write-Output "Wrote $resolvedManifestPath"
foreach ($route in $routes) {
    $metrics = $route.extractionMetrics
    $summaryFormat =
        "{0}: {1} points, length {2:F3} m, old stride-4 max " +
        "segment {3:F3} m, old chord deviation {4:F3} m"
    Write-Output ($summaryFormat -f
        $route.routeId,
        $route.sourcePointCount,
        $metrics.fullPolylineLengthMeters,
        $metrics.legacyStrideFourMaximumSegmentLengthMeters,
        $metrics.legacyStrideFourMaximumPlanarChordDeviationMeters)
}

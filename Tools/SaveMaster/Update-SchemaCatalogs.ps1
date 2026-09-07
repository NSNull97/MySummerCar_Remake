param([switch]$Check)
$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))

# Only project-owned scalar authoring. No extraction, binary import, Unity launch or donor access.
function Read-Scalar([string]$source, [string]$key) {
    $value = [regex]::Match($source, '(?m)^\s+' + [regex]::Escape($key) + ':\s*([^\r\n]+)')
    if (!$value.Success) { throw "Required scalar missing: $key" }
    return $value.Groups[1].Value.Trim().Trim("'").Trim('"')
}
function Read-List([string]$source, [string]$key) {
    $match = [regex]::Match($source, '(?m)^([ ]+)' + [regex]::Escape($key) + ':\s*\r?\n((?:\1- [^\r\n]*\r?\n)+)')
    if (!$match.Success) { return @() }
    return @([regex]::Matches($match.Groups[2].Value, '(?m)^\s*- ([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value.Trim() })
}
function Relative-Source([string]$path) { return [IO.Path]::GetRelativePath($repoRoot, $path).Replace('\', '/') }
function Source-Hash([string]$path) { return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Store-Catalog([string]$relativePath, $data) {
    $path = Join-Path $repoRoot $relativePath
    $source = Get-Content -LiteralPath $path -Raw
    $pattern = '(?s)(internal const string Data = """\r?\n)(.*?)(\r?\n""";)'
    $existing = [regex]::Match($source, $pattern)
    if (!$existing.Success) { throw "Embedded Data block missing in $relativePath" }
    $json = $data | ConvertTo-Json -Depth 10 -Compress
    if ($Check) {
        if ($existing.Groups[2].Value -cne $json) { throw "Catalog differs from current source hashes/ranges: $relativePath. Run this script without -Check and review changes." }
        Write-Output "Verified source hashes and scalar snapshot: $relativePath"
    } else {
        $updated = [regex]::Replace($source, $pattern, [System.Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + $json + $m.Groups[3].Value })
        [IO.File]::WriteAllText($path, $updated, [Text.UTF8Encoding]::new($false))
        Write-Output "Updated scalar snapshot: $relativePath"
    }
}

$files = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Assets/Game/Vehicle/Content') -Recurse -Filter '*.asset')
foreach ($relative in @('Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/FastenerDefinitions', 'Assets/Game/LegacyImport/RuntimeBaseline/Vehicles/Satsuma/MountDefinitions')) {
    $files += @(Get-ChildItem -LiteralPath (Join-Path $repoRoot $relative) -Filter '*.asset')
}
$bolts = @{}
$guids = @{}
$mounts = @{}
foreach ($file in $files) {
    $source = Get-Content -LiteralPath $file.FullName -Raw
    if ($source -notmatch '::MSC.Vehicle.Assembly.FastenerDefinition' -or $source -notmatch 'maximumStage:') { continue }
    $id = Read-Scalar $source 'definitionId'
    $entry = [ordered]@{
        id = $id
        max = [int](Read-Scalar $source 'maximumStage')
        size = [int](Read-Scalar $source 'size')
        required = (Read-Scalar $source 'requiredForRemoval') -eq '1'
        hash = Source-Hash $file.FullName
        source = Relative-Source $file.FullName
    }
    if ($bolts.ContainsKey($id)) { throw "Duplicate fastener definition ID: $id" }
    if ($entry.max -le 0) { throw "Invalid maximumStage: $id" }
    $bolts[$id] = $entry
    $meta = Get-Content -LiteralPath ($file.FullName + '.meta') -Raw
    $guid = [regex]::Match($meta, '(?m)^guid: ([a-f0-9]+)').Groups[1].Value
    if ($guid.Length -ne 32 -or $guids.ContainsKey($guid)) { throw "Missing or duplicated asset GUID: $id" }
    $guids[$guid] = $id
}
foreach ($file in $files) {
    $source = Get-Content -LiteralPath $file.FullName -Raw
    if ($source -notmatch '::MSC.Vehicle.Assembly.MountPointDefinition') { continue }
    $id = Read-Scalar $source 'definitionId'
    $fasteners = @()
    foreach ($reference in (Read-List $source 'fasteners')) {
        $guid = [regex]::Match($reference, 'guid: ([a-f0-9]+)').Groups[1].Value
        if (!$guids.ContainsKey($guid)) { throw "Unresolved project fastener GUID: $id / $guid" }
        $fasteners += $guids[$guid]
    }
    $groupIds = @(Read-List $source 'fastenerDefinitionIds')
    if ($groupIds.Count -eq 0) {
        # Exact existing FastenerGroupDefinition.CreateCompatibility behavior.
        $groupIds = @($fasteners | Where-Object { $bolts[$_].required })
        $maximum = 0
        foreach ($groupId in $groupIds) { $maximum += $bolts[$groupId].max }
        $on = [int]($maximum -gt 0)
        $off = 0
    } else {
        $maximum = [int](Read-Scalar $source 'aggregateMaximumTightness')
        $on = [int](Read-Scalar $source 'boltedOnThreshold')
        $off = [int](Read-Scalar $source 'boltedOffThreshold')
        foreach ($groupId in $groupIds) { if ($fasteners -notcontains $groupId) { throw "Group references foreign fastener: $id / $groupId" } }
    }
    if ($mounts.ContainsKey($id)) { throw "Duplicate mount definition ID: $id" }
    $mounts[$id] = [ordered]@{
        id = $id
        fasteners = $fasteners
        groupIds = $groupIds
        max = $maximum
        on = $on
        off = $off
        accepted = @(Read-List $source 'acceptedPartDefinitionIds')
        owner = Read-Scalar $source 'ownerPartDefinitionId'
        hash = Source-Hash $file.FullName
        source = Relative-Source $file.FullName
    }
}
Store-Catalog 'Tools/SaveMaster/Core/SchemaVehicleDefinitions.cs' ([ordered]@{
    bolts = @($bolts.Values | Sort-Object { $_.id })
    mounts = @($mounts.Values | Sort-Object { $_.id })
})

$itemPath = Join-Path $repoRoot 'Assets/Game/Items/Content/Definitions/Phase1ItemDefinitionCatalog.asset'
$itemSource = Get-Content -LiteralPath $itemPath -Raw
$definitions = @()
foreach ($block in [regex]::Matches($itemSource, '(?ms)^  - definitionId: ([^\r\n]+)\r?\n(.*?)(?=^  - definitionId:|\z)')) {
    $body = $block.Groups[2].Value
    $variants = [regex]::Match($body, '(?m)^    toolVariants:\r?\n((?:    - [^\r\n]*\r?\n)+)').Groups[1].Value
    $ranges = @()
    $scalars = [regex]::Match($body, '(?ms)^    scalarStates:\r?\n(.*?)(?=^    [a-zA-Z]|\z)').Groups[1].Value
    foreach ($range in [regex]::Matches($scalars, '(?m)^    - stateId: ([^\r\n]+)\r?\n      minimum: ([^\r\n]+)\r?\n      maximum: ([^\r\n]+)')) {
        $ranges += [ordered]@{
            id = $range.Groups[1].Value
            min = [double]::Parse($range.Groups[2].Value, [cultureinfo]::InvariantCulture)
            max = [double]::Parse($range.Groups[3].Value, [cultureinfo]::InvariantCulture)
        }
    }
    $definitions += [ordered]@{
        id = $block.Groups[1].Value
        maximum = [double]::Parse((Read-Scalar $body 'maximumContent'), [cultureinfo]::InvariantCulture)
        childCount = [int](Read-Scalar $body 'initialChildCount')
        liquid = (Read-Scalar $body 'supportsLiquidTransfer') -eq '1'
        variants = [Math]::Max(1, [regex]::Matches($variants, '(?m)^    - ').Count)
        scalarRanges = @($ranges | Sort-Object { $_.id })
    }
}
if ($definitions.Count -eq 0) { throw 'No item definitions parsed.' }
Store-Catalog 'Tools/SaveMaster/Core/SchemaItemDefinitions.cs' ([ordered]@{
    source = Relative-Source $itemPath
    sha256 = Source-Hash $itemPath
    definitions = @($definitions | Sort-Object { $_.id })
})
Write-Output "Catalog totals: $($bolts.Count) fasteners, $($mounts.Count) mounts, $($definitions.Count) item types."

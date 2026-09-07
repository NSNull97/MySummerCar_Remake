<#
.SYNOPSIS
Read explicitly selected Unity YAML objects without loading a whole scene.
.DESCRIPTION
Read-only inspection: no asset import, code execution, file generation, donor
patching or runtime use. Returned Body is private donor evidence and must not be
committed. Object IDs are provenance selectors, never project runtime identity.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string]$SourcePath,

    [Parameter(Mandatory = $true)]
    [ValidateCount(1, 32)]
    [long[]]$ObjectId,

    [ValidateRange(1024, 67108864)]
    [int]$MaximumObjectCharacters = 8388608
)

$ErrorActionPreference = 'Stop'
$yamlSource = (Resolve-Path -LiteralPath $SourcePath).ProviderPath
if (-not [IO.File]::Exists($yamlSource)) {
    throw 'SourcePath must identify one existing file.'
}
if (@($ObjectId | Select-Object -Unique).Count -ne $ObjectId.Count) {
    throw 'Specify each requested object ID exactly once.'
}
$yamlRg = Get-Command rg -CommandType Application -ErrorAction Stop

foreach ($yamlId in $ObjectId) {
    $yamlPattern = '^--- !u![0-9]+ &' + $yamlId.ToString(
        [Globalization.CultureInfo]::InvariantCulture) + '(?: stripped)?\r?$'
    $yamlMatches = @(& $yamlRg.Source --no-config --byte-offset --no-heading --no-filename -- $yamlPattern $yamlSource)
    $yamlExit = $LASTEXITCODE
    if ($yamlExit -gt 1) { throw "rg failed with exit code $yamlExit." }
    if ($yamlMatches.Count -ne 1) {
        throw "Expected exactly one Unity YAML object $yamlId; found $($yamlMatches.Count)."
    }
    $yamlMatch = [regex]::Match($yamlMatches[0],
        '^(?<offset>[0-9]+):--- !u!(?<type>[0-9]+) &')
    if (-not $yamlMatch.Success) { throw 'Unexpected rg byte-offset result.' }
    $yamlOffset = [long]::Parse($yamlMatch.Groups['offset'].Value,
        [Globalization.CultureInfo]::InvariantCulture)
    $yamlStream = [IO.File]::OpenRead($yamlSource)
    $yamlReader = $null
    try {
        $null = $yamlStream.Seek($yamlOffset, [IO.SeekOrigin]::Begin)
        $yamlReader = [IO.StreamReader]::new($yamlStream,
            [Text.UTF8Encoding]::new($false, $true), $false)
        $yamlHeader = $yamlReader.ReadLine()
        if ($yamlHeader -notmatch $yamlPattern) {
            throw 'Source header changed between selection and read.'
        }
        $yamlBody = [Text.StringBuilder]::new()
        $null = $yamlBody.AppendLine($yamlHeader)
        while (($yamlLine = $yamlReader.ReadLine()) -ne $null) {
            if ($yamlLine.StartsWith('--- !u!')) { break }
            if ($yamlBody.Length + $yamlLine.Length + [Environment]::NewLine.Length -gt $MaximumObjectCharacters) {
                throw "Object $yamlId exceeds the explicit inspection size limit."
            }
            $null = $yamlBody.AppendLine($yamlLine)
        }
        [pscustomobject]@{
            SourcePath = $yamlSource
            ObjectId = $yamlId
            TypeId = [int]::Parse($yamlMatch.Groups['type'].Value,
                [Globalization.CultureInfo]::InvariantCulture)
            ByteOffset = $yamlOffset
            Body = $yamlBody.ToString()
        }
    }
    finally {
        if ($null -ne $yamlReader) { $yamlReader.Dispose() }
        else { $yamlStream.Dispose() }
    }
}

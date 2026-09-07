param(
    [string]$OutputDirectory = '',
    [switch]$SkipTests
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectRoot 'Builds/SaveMaster'
}
$outputPath = [System.IO.Path]::GetFullPath($OutputDirectory)
$compilePath = Join-Path $projectRoot 'Artifacts/SaveMaster/Compile'

if (-not $SkipTests) {
    & dotnet run --project (Join-Path $PSScriptRoot 'Tests/SaveMaster.Tests.csproj') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Save Master regression tests failed.' }
}

& dotnet publish (Join-Path $PSScriptRoot 'App/SaveMaster.App.csproj') --configuration Release --no-self-contained --output $outputPath "-p:OutputPath=$compilePath/" --nologo
if ($LASTEXITCODE -ne 0) { throw 'Save Master publish failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README_RU.md') -Destination (Join-Path $outputPath 'README_RU.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SCHEMA.md') -Destination (Join-Path $outputPath 'SCHEMA.md')
Get-ChildItem -LiteralPath $outputPath -File | Where-Object Name -ne 'SHA256.json' | Get-FileHash -Algorithm SHA256 |
    Select-Object @{Name='File';Expression={ [System.IO.Path]::GetFileName($_.Path) }},Hash |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $outputPath 'SHA256.json') -Encoding UTF8
Write-Host "Save Master ready: $outputPath"
Write-Host 'Run My Summer Remake Save Master.exe. Requires the installed .NET 9 Desktop Runtime (x64); Unity is not needed.'

[CmdletBinding()]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\Config\DonorPaths.local.json')
)

$ErrorActionPreference = 'Stop'

function Write-Check([string]$Name, [bool]$Passed, [string]$Details) {
    $status = if ($Passed) { 'OK' } else { 'FAIL' }
    Write-Host ("[{0}] {1}: {2}" -f $status, $Name, $Details)
}

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json

$original = [string]$config.OriginalGameDirectory
$project = [string]$config.UnityProjectDirectory
$staging = [string]$config.DonorStagingDirectory
$legacy = [string]$config.LegacyReferenceDirectory

Write-Check 'Original game directory' (Test-Path -LiteralPath $original -PathType Container) $original
Write-Check 'Unity project directory' (Test-Path -LiteralPath $project -PathType Container) $project

$assets = Join-Path $project 'Assets'
$packages = Join-Path $project 'Packages'
$settings = Join-Path $project 'ProjectSettings'
Write-Check 'Unity Assets' (Test-Path -LiteralPath $assets -PathType Container) $assets
Write-Check 'Unity Packages' (Test-Path -LiteralPath $packages -PathType Container) $packages
Write-Check 'Unity ProjectSettings' (Test-Path -LiteralPath $settings -PathType Container) $settings

$projectVersion = Join-Path $settings 'ProjectVersion.txt'
if (Test-Path -LiteralPath $projectVersion) {
    $versionText = Get-Content -LiteralPath $projectVersion -Raw
    Write-Check 'Unity project version file' $true $versionText.Trim()
} else {
    Write-Check 'Unity project version file' $false $projectVersion
}

$originalResolved = if (Test-Path -LiteralPath $original) { (Resolve-Path -LiteralPath $original).Path } else { $original }
$projectResolved = if (Test-Path -LiteralPath $project) { (Resolve-Path -LiteralPath $project).Path } else { $project }

$badNesting = $projectResolved.StartsWith($originalResolved, [System.StringComparison]::OrdinalIgnoreCase) -or
              $originalResolved.StartsWith($projectResolved, [System.StringComparison]::OrdinalIgnoreCase)
Write-Check 'Donor/project separation' (-not $badNesting) 'Directories must not contain one another.'

$git = Get-Command git -ErrorAction SilentlyContinue
Write-Check 'Git' ($null -ne $git) ($(if ($git) { (& git --version) } else { 'not found' }))

$gitLfsOk = $false
$gitLfsDetails = 'not found'
if ($git) {
    try {
        $gitLfsDetails = (& git lfs version 2>$null)
        $gitLfsOk = $LASTEXITCODE -eq 0
    } catch { }
}
Write-Check 'Git LFS' $gitLfsOk $gitLfsDetails

foreach ($pair in @(
    @('AssetRipper', [string]$config.AssetRipperExecutable),
    @('ILSpyCmd', [string]$config.ILSpyCmdExecutable),
    @('UnityEditor', [string]$config.UnityEditorExecutable)
)) {
    $name = $pair[0]
    $path = $pair[1]
    if ([string]::IsNullOrWhiteSpace($path)) {
        Write-Check $name $false 'path not configured (optional at Milestone 0)'
    } else {
        Write-Check $name (Test-Path -LiteralPath $path -PathType Leaf) $path
    }
}

Write-Host ''
Write-Host "Planned donor staging: $staging"
Write-Host "Planned legacy reference: $legacy"
Write-Host 'Validation completed. No donor files were modified.'

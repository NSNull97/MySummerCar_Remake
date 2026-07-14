[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$ConfigPath = (Join-Path $PSScriptRoot '..\Config\DonorPaths.local.json')
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Config not found: $ConfigPath"
}

$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
$original = [string]$config.OriginalGameDirectory
$staging = [string]$config.DonorStagingDirectory
$legacy = [string]$config.LegacyReferenceDirectory

if (-not (Test-Path -LiteralPath $original -PathType Container)) {
    throw "Original game directory not found: $original"
}

$targets = @(
    $staging,
    (Join-Path $staging 'raw'),
    (Join-Path $staging 'normalized'),
    (Join-Path $staging 'manifests'),
    (Join-Path $staging 'logs'),
    (Join-Path $staging 'captures'),
    $legacy
)

foreach ($target in $targets) {
    if ($target.StartsWith($original, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to create output inside the donor installation: $target"
    }

    if (-not (Test-Path -LiteralPath $target)) {
        if ($PSCmdlet.ShouldProcess($target, 'Create directory')) {
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            Write-Host "Created: $target"
        }
    } else {
        Write-Host "Exists:  $target"
    }
}

Write-Host 'Done. Original game directory was not modified.'

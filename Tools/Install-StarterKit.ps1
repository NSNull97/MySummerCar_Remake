[CmdletBinding()]
param(
    [string]$TargetProject = 'E:\GAYmDev_Studio\MySummerCar_Remake_Game',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$kitRoot = Split-Path -Parent $PSScriptRoot

if (-not (Test-Path -LiteralPath $TargetProject -PathType Container)) {
    throw "Target project does not exist: $TargetProject. Create the Unity 6 HDRP project first."
}

$excluded = @('KitManifest.json')
$conflicts = New-Object System.Collections.Generic.List[string]

Get-ChildItem -LiteralPath $kitRoot -Force | Where-Object { $excluded -notcontains $_.Name } | ForEach-Object {
    $destination = Join-Path $TargetProject $_.Name
    if ((Test-Path -LiteralPath $destination) -and -not $Force) {
        $conflicts.Add($destination)
    }
}

if ($conflicts.Count -gt 0) {
    Write-Host 'Existing top-level paths detected. Review before copying or rerun with -Force:'
    $conflicts | ForEach-Object { Write-Host " - $_" }
    exit 2
}

Get-ChildItem -LiteralPath $kitRoot -Force | Where-Object { $excluded -notcontains $_.Name } | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination $TargetProject -Recurse -Force:$Force
}

Write-Host "Starter kit copied to: $TargetProject"

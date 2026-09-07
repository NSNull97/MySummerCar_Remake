[CmdletBinding()]
param(
    [ValidateSet('Generate', 'Verify', 'All')][string]$Mode = 'All',
    [string]$UnityEditor = '',
    [string]$ArtifactRoot = '',
    [string]$InputDirectory = ''
)
$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
if (-not $ArtifactRoot) { $ArtifactRoot = Join-Path $repositoryRoot 'Artifacts/SaveMaster/CodecCompatibility' }
$ArtifactRoot = [IO.Path]::GetFullPath($ArtifactRoot)
if (-not $UnityEditor) {
    $localConfig = Join-Path $repositoryRoot 'Config/DonorPaths.local.json'
    if (Test-Path -LiteralPath $localConfig) {
        $UnityEditor = (Get-Content -LiteralPath $localConfig -Raw | ConvertFrom-Json).UnityEditorExecutable
    }
}
if (-not $UnityEditor -or -not (Test-Path -LiteralPath $UnityEditor)) {
    throw 'Pass -UnityEditor with the installed pinned Unity editor executable.'
}
$projectRoot = Join-Path $ArtifactRoot 'UnityCodecProbe'
$editorScripts = Join-Path $projectRoot 'Assets/Editor'
New-Item -ItemType Directory -Force -Path $editorScripts, (Join-Path $projectRoot 'Packages'), (Join-Path $projectRoot 'ProjectSettings') | Out-Null
# Only project-owned codec files are copied; no donor content, game project, scene, or asset is touched.
foreach ($source in @('SaveDocument.cs', 'SaveDocumentCodec.cs', 'SaveValidation.cs', 'SaveSlotId.cs')) {
    Copy-Item -LiteralPath (Join-Path $repositoryRoot "Assets/Game/Save/Runtime/$source") -Destination $editorScripts -Force
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'UnityCodecProbe.cs') -Destination $editorScripts -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'ProjectSettings/ProjectVersion.txt') -Destination (Join-Path $projectRoot 'ProjectSettings/ProjectVersion.txt') -Force
[IO.File]::WriteAllText((Join-Path $projectRoot 'Packages/manifest.json'), '{"dependencies":{"com.unity.modules.jsonserialize":"1.0.0"}}')
$fixtureDirectory = Join-Path $ArtifactRoot 'Generated'
$editedDirectory = Join-Path $ArtifactRoot 'Edited'

function Invoke-CodecProbe([string]$Method, [string]$InputPath, [string]$OutputPath, [string]$LogName) {
    # Start-Process arguments contain only resolved paths and constant switches; no shell evaluation.
    $arguments = @('-batchmode', '-nographics', '-projectPath', ('"' + $projectRoot + '"'),
        '-executeMethod', ('SaveMasterUnityCodecProbe.' + $Method), '-logFile', ('"' + (Join-Path $ArtifactRoot $LogName) + '"'),
        '-saveMasterOutput', ('"' + $OutputPath + '"'))
    if ($InputPath) { $arguments += @('-saveMasterInput', ('"' + $InputPath + '"')) }
    $process = Start-Process -FilePath $UnityEditor -ArgumentList $arguments -PassThru -WindowStyle Hidden -Wait
    if ($process.ExitCode -ne 0) { throw "Unity probe failed ($($process.ExitCode)); inspect $(Join-Path $ArtifactRoot $LogName)" }
}

if ($Mode -in @('Generate', 'All')) {
    Invoke-CodecProbe 'Generate' '' $fixtureDirectory 'unity-generate.log'
    Get-Content -LiteralPath (Join-Path $fixtureDirectory 'generated.txt')
}
if ($Mode -eq 'All') {
    $roundTripReport = Join-Path $ArtifactRoot 'dotnet-roundtrip.txt'
    & dotnet run --project (Join-Path $PSScriptRoot 'SaveMaster.Tests.csproj') --configuration Release -- --roundtrip-unity $fixtureDirectory $editedDirectory *> $roundTripReport
    $roundTripExit = $LASTEXITCODE
    if ($roundTripExit -ne 0) { throw "Save Master round-trip failed with exit code $roundTripExit; inspect $roundTripReport" }
    Get-Content -LiteralPath $roundTripReport | Select-Object -Last 1
}
if ($Mode -in @('Verify', 'All')) {
    if (-not $InputDirectory) { $InputDirectory = $editedDirectory }
    $InputDirectory = [IO.Path]::GetFullPath($InputDirectory)
    Invoke-CodecProbe 'Verify' $InputDirectory $ArtifactRoot 'unity-verify.log'
    Get-Content -LiteralPath (Join-Path $ArtifactRoot 'unity-verification.txt') | Select-Object -Last 1
}

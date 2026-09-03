param([string]$ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path)
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $ProjectRoot
$config = Get-Content -LiteralPath 'Config/DonorPaths.local.json' -Raw | ConvertFrom-Json
$editorData = Join-Path (Split-Path -Parent $config.UnityEditorExecutable) 'Data'
$runtime = Join-Path $editorData 'NetCoreRuntime/dotnet.exe'
$compiler = Join-Path $editorData 'DotNetSdkRoslyn/csc.dll'
$outputRoot = 'Artifacts/VegetationRebuild/Compile'
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
$responseRoot = 'Library/Bee/artifacts/1900b0aE.dag'
foreach ($assemblyName in @('MSC.World.Runtime', 'MSC.Editor')) {
    $responsePath = Join-Path $responseRoot ($assemblyName + '.rsp')
    if (-not (Test-Path -LiteralPath $responsePath)) { throw "Unity response file is missing: $responsePath" }
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($line in Get-Content -LiteralPath $responsePath) {
        if ($line.StartsWith('-out:')) { $lines.Add('-out:"' + $outputRoot + '/' + $assemblyName + '.dll"'); continue }
        if ($line.StartsWith('-refout:')) { $lines.Add('-refout:"' + $outputRoot + '/' + $assemblyName + '.ref.dll"'); continue }
        if ($assemblyName -eq 'MSC.Editor' -and $line -match '^-r:.*MSC.World.Runtime.ref.dll') {
            $lines.Add('-r:"' + $outputRoot + '/MSC.World.Runtime.ref.dll"'); continue
        }
        $lines.Add($line)
    }
    $additions = if ($assemblyName -eq 'MSC.World.Runtime') {
        @((Get-ChildItem -LiteralPath 'Assets/Game/World/Runtime/Vegetation' -Filter '*.cs').FullName) + @('Assets/Game/World/Runtime/Streaming/ProductionWorldCellLayerScene.cs')
    } else {
        @((Get-ChildItem -LiteralPath 'Assets/Game/Editor/Vegetation' -Filter '*.cs').FullName) + @('Assets/Game/Editor/WorldStreaming/ProductionWorldCellLayerBuilder.cs')
    }
    foreach ($source in $additions) {
        $relative = if ([System.IO.Path]::IsPathRooted($source)) { [System.IO.Path]::GetRelativePath($ProjectRoot, $source).Replace('\', '/') } else { $source }
        if (-not (Test-Path -LiteralPath $relative)) { continue }
        $entry = '"' + $relative + '"'
        if (-not $lines.Contains($entry)) { $lines.Add($entry) }
    }
    $targetResponse = $outputRoot + '/' + $assemblyName + '.rsp'
    [System.IO.File]::WriteAllLines((Join-Path $ProjectRoot $targetResponse), $lines, [System.Text.UTF8Encoding]::new($false))
    & $runtime $compiler ('@' + $targetResponse)
    if ($LASTEXITCODE -ne 0) { throw "$assemblyName compilation failed with exit code $LASTEXITCODE" }
}
Write-Output 'VEGETATION_ISOLATED_CSHARP_COMPILE_OK (not a substitute for Unity scene/tests validation)'

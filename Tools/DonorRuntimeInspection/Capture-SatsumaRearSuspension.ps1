param(
    [string]$ConfigurationPath = "Config/DonorPaths.local.json",
    [string]$CaptureCategory = "satsuma-rear"
)

$ErrorActionPreference = "Stop"

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$configurationFile = (Resolve-Path (Join-Path $repositoryRoot $ConfigurationPath)).Path
$configuration = Get-Content -LiteralPath $configurationFile -Raw | ConvertFrom-Json
$originalRoot = [IO.Path]::GetFullPath($configuration.OriginalGameDirectory)
$managedRoot = Join-Path $originalRoot "mysummercar_Data\Managed"
$monoPath = Join-Path $originalRoot "mysummercar_Data\Mono\mono.dll"
$payloadSource = Join-Path $PSScriptRoot "RuntimeTransformProbe.cs"
$injectorProject = Join-Path $PSScriptRoot "MonoManagedInjector\MonoManagedInjector.csproj"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$captureRoot = Join-Path $configuration.DonorStagingDirectory "runtime-inspection\$CaptureCategory\$timestamp"
$payloadPath = Join-Path $captureRoot "MSC.DonorRuntimeInspection.V3.$timestamp.dll"
$injectorOutput = Join-Path $captureRoot "injector"
$intermediateOutput = Join-Path $captureRoot "obj\"
$dumpPath = Join-Path ([IO.Path]::GetTempPath()) "msc-runtime-suspension-dump-v3.txt"

New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null
if (Test-Path -LiteralPath $dumpPath)
{
    Remove-Item -LiteralPath $dumpPath -Force
}

$sdk = & dotnet --list-sdks |
    ForEach-Object { ($_ -split '\s+')[0] } |
    Sort-Object { [Version]$_ } -Descending |
    Select-Object -First 1
if (-not $sdk)
{
    throw "No .NET SDK is installed."
}

$dotnetRoot = Split-Path (Get-Command dotnet).Source -Parent
$csc = Join-Path $dotnetRoot "sdk\$sdk\Roslyn\bincore\csc.dll"
if (-not (Test-Path -LiteralPath $csc))
{
    throw "Roslyn compiler not found: $csc"
}

$references = @(
    "mscorlib.dll",
    "System.dll",
    "System.Core.dll",
    "UnityEngine.dll",
    "0Harmony.dll"
) | ForEach-Object { "/reference:$([IO.Path]::Combine($managedRoot, $_))" }

& dotnet $csc /noconfig /nostdlib+ /target:library /optimize+ /debug- `
    /langversion:7.3 "/out:$payloadPath" $references $payloadSource
if ($LASTEXITCODE -ne 0)
{
    throw "Runtime probe compilation failed with exit code $LASTEXITCODE."
}

& dotnet build $injectorProject -c Release -o $injectorOutput `
    "/p:BaseIntermediateOutputPath=$intermediateOutput" --nologo
if ($LASTEXITCODE -ne 0)
{
    throw "Mono injector build failed with exit code $LASTEXITCODE."
}

$processes = @(Get-Process -Name "mysummercar" -ErrorAction SilentlyContinue)
if ($processes.Count -ne 1)
{
    throw "Expected exactly one running mysummercar process; found $($processes.Count)."
}

$process = $processes[0]
$injector = Join-Path $injectorOutput "MonoManagedInjector.dll"
& dotnet $injector $process.Id $monoPath $payloadPath `
    "MSC.DonorRuntimeInspection" "RuntimeTransformProbe" "Install"
if ($LASTEXITCODE -ne 0)
{
    throw "Mono injection failed with exit code $LASTEXITCODE."
}

$deadline = [DateTime]::UtcNow.AddSeconds(20)
while (-not (Test-Path -LiteralPath $dumpPath) -and
       [DateTime]::UtcNow -lt $deadline)
{
    Start-Sleep -Milliseconds 100
}

if (-not (Test-Path -LiteralPath $dumpPath))
{
    throw "The main-thread runtime probe produced no dump within 20 seconds."
}

$capturePath = Join-Path $captureRoot "$CaptureCategory-runtime-dump.txt"
Copy-Item -LiteralPath $dumpPath -Destination $capturePath
$firstLine = Get-Content -LiteralPath $capturePath -TotalCount 1
if ($firstLine -ne "MSC_DONOR_RUNTIME_TRANSFORM_DUMP_V3")
{
    Get-Content -LiteralPath $capturePath -Raw | Write-Error
    throw "Runtime probe returned an error payload."
}

Get-FileHash -Algorithm SHA256 -LiteralPath $capturePath |
    Select-Object Path, Hash |
    ConvertTo-Json |
    Set-Content -LiteralPath (Join-Path $captureRoot "capture.sha256.json") -Encoding utf8

Write-Output $capturePath

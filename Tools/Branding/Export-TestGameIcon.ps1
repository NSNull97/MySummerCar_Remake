param(
    [Parameter(Mandatory = $true)]
    [string] $SourcePng,
    [string] $ProjectRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$iconDirectory = Join-Path $ProjectRoot 'Assets/Game/UI/Presentation/Content/ApplicationIcon'
$exportDirectory = Join-Path $ProjectRoot 'References/Branding'
[System.IO.Directory]::CreateDirectory($iconDirectory) | Out-Null
[System.IO.Directory]::CreateDirectory($exportDirectory) | Out-Null

# Packaging only: retain the generated alpha and resample the same artwork.
function Convert-IconSize([System.Drawing.Image] $Image, [int] $Size) {
    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.DrawImage($Image, [System.Drawing.Rectangle]::new(0, 0, $Size, $Size),
            0, 0, $Image.Width, $Image.Height, [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally { $graphics.Dispose() }
    return $bitmap
}

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $SourcePng).Path)
try {
    if ($source.Width -ne $source.Height -or $source.GetPixel(0, 0).A -ne 0) {
        throw 'The icon source must be square with a transparent exterior.'
    }
    $png = Convert-IconSize $source 1024
    try { $png.Save((Join-Path $iconDirectory 'TestGameIcon.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    finally { $png.Dispose() }

    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $frames = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $frame = Convert-IconSize $source $size
        $stream = [System.IO.MemoryStream]::new()
        try {
            $frame.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($stream.ToArray())
        }
        finally { $stream.Dispose(); $frame.Dispose() }
    }

    $icoPath = Join-Path $exportDirectory 'MySummerCar_Remake_Test.ico'
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($icoPath))
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $dimension = if ($sizes[$index] -eq 256) { 0 } else { $sizes[$index] }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length)
            $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frameBytes in $frames) { $writer.Write($frameBytes) }
    }
    finally { $writer.Dispose() }
    Write-Output "Created 1024px RGBA PNG and ICO with sizes $($sizes -join ', ')."
}
finally { $source.Dispose() }

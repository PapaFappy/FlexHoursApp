# Reproducible, multi-resolution clock mark. Run from Windows PowerShell.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$destination = Join-Path $PSScriptRoot 'FlexHours.ico'
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 256.0), ($size / 256.0))
    $graphics.Clear([System.Drawing.Color]::Transparent)
    $background = New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml('#152239'))
    $ring = New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml('#58DBBA'), 17)
    $hands = New-Object System.Drawing.Pen([System.Drawing.Color]::White, 16)
    $hands.StartCap = $hands.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $graphics.FillEllipse($background, 5, 5, 246, 246)
    $graphics.DrawArc($ring, 41, 41, 174, 174, 135, 295)
    $graphics.DrawLine($hands, 128, 128, 128, 77)
    $graphics.DrawLine($hands, 128, 128, 169, 151)
    $stream = New-Object System.IO.MemoryStream
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += ,@($size, $stream.ToArray())
    $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose(); $background.Dispose(); $ring.Dispose(); $hands.Dispose()
}
$output = [System.IO.File]::Create($destination)
$writer = New-Object System.IO.BinaryWriter($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame[0] -eq 256) { 0 } else { $frame[0] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame[1].Length); $writer.Write([uint32]$offset)
        $offset += $frame[1].Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame[1]) }
} finally { $writer.Dispose(); $output.Dispose() }

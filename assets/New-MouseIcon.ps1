$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Rasterize the simple geometry from mouse.svg at Windows icon sizes.
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = foreach ($size in $sizes) {
    $bitmap = New-Object Drawing.Bitmap($size, $size)
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size / 32.0), ($size / 32.0))
    $outline = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#34495e'), 2)
    $body = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#eef5f7'))
    $wheel = New-Object Drawing.SolidBrush([Drawing.ColorTranslator]::FromHtml('#00b8c8'))
    $wheelOutline = New-Object Drawing.Pen([Drawing.ColorTranslator]::FromHtml('#34495e'), 1)
    $shape = New-Object Drawing.Drawing2D.GraphicsPath
    $wheelShape = New-Object Drawing.Drawing2D.GraphicsPath
    $stream = New-Object IO.MemoryStream
    try {
        $graphics.Clear([Drawing.Color]::Transparent)
        $shape.AddArc(6, 2, 20, 20, 180, 180)
        $shape.AddArc(6, 10, 20, 20, 0, 180)
        $shape.CloseFigure()
        $graphics.FillPath($body, $shape)
        $graphics.DrawPath($outline, $shape)
        $graphics.DrawLine($outline, 16, 3, 16, 13)
        $graphics.DrawLine($outline, 7, 13, 25, 13)
        $wheelShape.AddArc(14, 5, 4, 4, 180, 180)
        $wheelShape.AddArc(14, 7, 4, 4, 0, 180)
        $wheelShape.CloseFigure()
        $graphics.FillPath($wheel, $wheelShape)
        $graphics.DrawPath($wheelOutline, $wheelShape)
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
    }
    finally {
        $stream.Dispose(); $wheelShape.Dispose(); $shape.Dispose()
        $wheelOutline.Dispose(); $wheel.Dispose(); $body.Dispose(); $outline.Dispose()
        $graphics.Dispose(); $bitmap.Dispose()
    }
}

$file = [IO.File]::Create((Join-Path $PSScriptRoot 'mouse.ico'))
$writer = New-Object IO.BinaryWriter($file)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
}
finally { $writer.Dispose(); $file.Dispose() }

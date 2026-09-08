Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 64, 128, 256)
$images = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    
    # Background rounded badge
    $rect = New-Object System.Drawing.Rectangle 1, 1, ($sz - 2), ($sz - 2)
    $brushBg = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(15, 23, 42)), ([System.Drawing.Color]::FromArgb(30, 41, 59)), 45.0
    $g.FillEllipse($brushBg, $rect)
    $brushBg.Dispose()
    
    # Border
    $penWidth = [float][Math]::Max(1.0, ($sz / 20.0))
    $penColor = [System.Drawing.Color]::FromArgb(16, 185, 129)
    $pen = New-Object System.Drawing.Pen ($penColor, $penWidth)
    $g.DrawEllipse($pen, $rect)
    $pen.Dispose()
    
    # Lightning / Power Bolt
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $scale = $sz / 100.0
    $pts = @(
        (New-Object System.Drawing.PointF (52 * $scale), (18 * $scale)),
        (New-Object System.Drawing.PointF (30 * $scale), (52 * $scale)),
        (New-Object System.Drawing.PointF (48 * $scale), (52 * $scale)),
        (New-Object System.Drawing.PointF (44 * $scale), (82 * $scale)),
        (New-Object System.Drawing.PointF (72 * $scale), (44 * $scale)),
        (New-Object System.Drawing.PointF (54 * $scale), (44 * $scale))
    )
    $path.AddPolygon($pts)
    $brushBolt = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, ([System.Drawing.Color]::FromArgb(52, 211, 153)), ([System.Drawing.Color]::FromArgb(16, 185, 129)), 90.0
    $g.FillPath($brushBolt, $path)
    $brushBolt.Dispose()
    $path.Dispose()
    
    $g.Dispose()
    $images += $bmp
}

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms

$bw.Write([UInt16]0) # Reserved
$bw.Write([UInt16]1) # Type 1 = Icon
$bw.Write([UInt16]$images.Count)

$offset = 6 + (16 * $images.Count)
$pngStreams = @()

foreach ($img in $images) {
    $pms = New-Object System.IO.MemoryStream
    $img.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $pms.ToArray()
    $pngStreams += $pngBytes
    $pms.Dispose()
    
    $w = if ($img.Width -ge 256) { 0 } else { [byte]$img.Width }
    $h = if ($img.Height -ge 256) { 0 } else { [byte]$img.Height }
    
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $bw.Write([UInt32]$pngBytes.Length)
    $bw.Write([UInt32]$offset)
    
    $offset += $pngBytes.Length
}

foreach ($bytes in $pngStreams) {
    $bw.Write($bytes)
}

$targetIco = Join-Path $PSScriptRoot "app.ico"
[System.IO.File]::WriteAllBytes($targetIco, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
foreach ($img in $images) { $img.Dispose() }
Write-Host "app.ico generated successfully at $targetIco"

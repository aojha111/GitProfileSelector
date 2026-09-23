# Draws the GitProfile branch glyph at every icon size and writes a multi-frame .ico.
#   powershell -File tools/make-icon.ps1 [-Out src\GitProfile.App\GitProfile.ico]
param([string]$Out = (Join-Path (Split-Path -Parent $PSScriptRoot) 'src\GitProfile.App\GitProfile.ico'))

Add-Type -AssemblyName System.Drawing

$Sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256

function New-Glyph([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # One design unit is 1/32 of the 32px reference drawing.
    $u = $size / 32.0
    $accent = [System.Drawing.Color]::FromArgb(255, 15, 108, 189)
    $white = [System.Drawing.Color]::FromArgb(255, 255, 255, 255)

    $disc = New-Object System.Drawing.SolidBrush($accent)
    $g.FillEllipse($disc, 0, 0, $size, $size)

    $thin = [Math]::Max(1.0, 2.2 * $u)
    $pen = New-Object System.Drawing.Pen($white, [single]$thin)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    $g.DrawLine($pen, [single](12 * $u), [single](15 * $u), [single](12 * $u), [single](22 * $u))
    $g.DrawCurve($pen, @(
            [System.Drawing.PointF](New-Object System.Drawing.PointF([single](12 * $u), [single](16 * $u))),
            [System.Drawing.PointF](New-Object System.Drawing.PointF([single](16 * $u), [single](15 * $u))),
            [System.Drawing.PointF](New-Object System.Drawing.PointF([single](20 * $u), [single](11 * $u)))
        ))

    $dot = New-Object System.Drawing.SolidBrush($white)
    $r = [single][Math]::Max(2.5, 3.0 * $u)
    foreach ($node in @{ x = 12; y = 9 }, @{ x = 20; y = 9 }, @{ x = 12; y = 23 }) {
        $g.FillEllipse($dot, [single]($node.x * $u - $r), [single]($node.y * $u - $r), [single](2 * $r), [single](2 * $r))
    }

    $g.Dispose()
    return $bmp
}

function Set-Bytes([byte[]]$target, [int]$at, [byte[]]$value) {
    for ($j = 0; $j -lt $value.Length; $j++) { $target[$at + $j] = $value[$j] }
}

$frames = foreach ($size in $Sizes) {
    $bmp = New-Glyph $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    @{ Size = $size; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

$header = New-Object byte[] 6
$header[2] = 1                       # type: icon
$header[4] = [byte]$frames.Count     # frame count
$offset = 6 + (16 * $frames.Count)

$directory = New-Object byte[] (16 * $frames.Count)
for ($i = 0; $i -lt $frames.Count; $i++) {
    $frame = $frames[$i]
    $base = $i * 16
    $directory[$base] = [byte]($(if ($frame.Size -ge 256) { 0 } else { $frame.Size }))
    $directory[$base + 1] = [byte]($(if ($frame.Size -ge 256) { 0 } else { $frame.Size }))
    Set-Bytes $directory ($base + 4) ([BitConverter]::GetBytes([uint16]1))    # planes
    Set-Bytes $directory ($base + 6) ([BitConverter]::GetBytes([uint16]32))   # bit depth
    Set-Bytes $directory ($base + 8) ([BitConverter]::GetBytes([uint32]$frame.Bytes.Length))
    Set-Bytes $directory ($base + 12) ([BitConverter]::GetBytes([uint32]$offset))
    $offset += $frame.Bytes.Length
}

$stream = New-Object System.IO.FileStream($Out, [System.IO.FileMode]::Create)
$stream.Write($header, 0, $header.Length)
$stream.Write($directory, 0, $directory.Length)
foreach ($frame in $frames) { $stream.Write($frame.Bytes, 0, $frame.Bytes.Length) }
$stream.Close()

Write-Output "wrote $Out ($((Get-Item $Out).Length) bytes, $($frames.Count) frames)"

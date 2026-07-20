param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$textureRoot = Join-Path $root "assets\textures"
$overlayRoot = Join-Path $textureRoot "overlays"

function New-TextureCanvas {
    param(
        [int]$Size,
        [System.Drawing.Color]$BaseColor,
        [bool]$Transparent = $false
    )

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $graphics.Clear($(if ($Transparent) { [System.Drawing.Color]::Transparent } else { $BaseColor }))
    [pscustomobject]@{ Bitmap = $bitmap; Graphics = $graphics; Size = $Size }
}

function Add-WrappedEllipse {
    param($Canvas, [System.Drawing.Brush]$Brush, [float]$X, [float]$Y, [float]$Width, [float]$Height)
    foreach ($offsetX in @(-$Canvas.Size, 0, $Canvas.Size)) {
        foreach ($offsetY in @(-$Canvas.Size, 0, $Canvas.Size)) {
            $Canvas.Graphics.FillEllipse($Brush, $X + $offsetX, $Y + $offsetY, $Width, $Height)
        }
    }
}

function Add-WrappedLine {
    param($Canvas, [System.Drawing.Pen]$Pen, [float]$X1, [float]$Y1, [float]$X2, [float]$Y2)
    foreach ($offsetX in @(-$Canvas.Size, 0, $Canvas.Size)) {
        foreach ($offsetY in @(-$Canvas.Size, 0, $Canvas.Size)) {
            $Canvas.Graphics.DrawLine($Pen, $X1 + $offsetX, $Y1 + $offsetY, $X2 + $offsetX, $Y2 + $offsetY)
        }
    }
}

function Add-WrappedPolygon {
    param($Canvas, [System.Drawing.Brush]$Brush, [System.Drawing.PointF[]]$Points)
    foreach ($offsetX in @(-$Canvas.Size, 0, $Canvas.Size)) {
        foreach ($offsetY in @(-$Canvas.Size, 0, $Canvas.Size)) {
            $shifted = [System.Drawing.PointF[]]::new($Points.Length)
            for ($index = 0; $index -lt $Points.Length; $index++) {
                $shifted[$index] = [System.Drawing.PointF]::new($Points[$index].X + $offsetX, $Points[$index].Y + $offsetY)
            }
            $Canvas.Graphics.FillPolygon($Brush, $shifted)
        }
    }
}

function Get-DiamondPoints {
    param([float]$CenterX, [float]$CenterY, [float]$Length, [float]$Width, [float]$AngleDegrees, [float]$OffsetX = 0, [float]$OffsetY = 0)
    $angle = $AngleDegrees * [Math]::PI / 180.0
    $ux = [Math]::Cos($angle)
    $uy = [Math]::Sin($angle)
    $vx = -$uy
    $vy = $ux
    $halfLength = $Length * 0.5
    $halfWidth = $Width * 0.5
    [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($CenterX + $OffsetX + ($ux * $halfLength), $CenterY + $OffsetY + ($uy * $halfLength)),
        [System.Drawing.PointF]::new($CenterX + $OffsetX + ($vx * $halfWidth), $CenterY + $OffsetY + ($vy * $halfWidth)),
        [System.Drawing.PointF]::new($CenterX + $OffsetX - ($ux * $halfLength), $CenterY + $OffsetY - ($uy * $halfLength)),
        [System.Drawing.PointF]::new($CenterX + $OffsetX - ($vx * $halfWidth), $CenterY + $OffsetY - ($vy * $halfWidth))
    )
}

function Save-TextureCanvas {
    param($Canvas, [string]$Path)
    $Canvas.Graphics.Dispose()
    $Canvas.Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $Canvas.Bitmap.Dispose()
}

function New-ConcreteTexture {
    $size = 2048
    $random = [System.Random]::new(17011)
    $canvas = New-TextureCanvas $size ([System.Drawing.Color]::FromArgb(255, 99, 105, 104))
    $mottleDark = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(5, 31, 40, 41))
    $mottleLight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(4, 220, 216, 204))
    for ($index = 0; $index -lt 1800; $index++) {
        $width = $random.Next(10, 50)
        $height = $random.Next(6, 32)
        Add-WrappedEllipse $canvas $(if (($index % 2) -eq 0) { $mottleDark } else { $mottleLight }) ($random.NextDouble() * $size) ($random.NextDouble() * $size) $width $height
    }

    $aggregateBrushes = @(
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(68, 35, 44, 45)),
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(58, 54, 62, 61)),
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(60, 199, 196, 186)),
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(50, 230, 226, 214))
    )
    for ($index = 0; $index -lt 30000; $index++) {
        $diameter = 0.45 + ($random.NextDouble() * 1.2)
        Add-WrappedEllipse $canvas $aggregateBrushes[$random.Next(0, $aggregateBrushes.Count)] ($random.NextDouble() * $size) ($random.NextDouble() * $size) $diameter $diameter
    }

    $poreHalo = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(32, 30, 38, 39))
    $poreCore = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(95, 17, 25, 27))
    for ($index = 0; $index -lt 1800; $index++) {
        $diameter = 0.8 + ($random.NextDouble() * 2.5)
        $x = $random.NextDouble() * $size
        $y = $random.NextDouble() * $size
        Add-WrappedEllipse $canvas $poreHalo $x $y $diameter $diameter
        Add-WrappedEllipse $canvas $poreCore ($x + ($diameter * 0.3)) ($y + ($diameter * 0.3)) ($diameter * 0.28) ($diameter * 0.28)
    }

    $hairlinePens = @(
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(42, 24, 32, 34), 0.35),
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(34, 225, 221, 211), 0.3),
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(32, 42, 51, 51), 0.45)
    )
    for ($index = 0; $index -lt 2600; $index++) {
        $x = $random.NextDouble() * $size
        $y = $random.NextDouble() * $size
        $length = 1.0 + ($random.NextDouble() * 8.0)
        $angle = $random.NextDouble() * [Math]::PI * 2.0
        Add-WrappedLine $canvas $hairlinePens[$random.Next(0, $hairlinePens.Count)] $x $y ($x + ([Math]::Cos($angle) * $length)) ($y + ([Math]::Sin($angle) * $length))
    }

    Save-TextureCanvas $canvas (Join-Path $textureRoot "industrial_concrete.png")
    @($mottleDark, $mottleLight, $poreHalo, $poreCore) + $aggregateBrushes + $hairlinePens | ForEach-Object { $_.Dispose() }
}

function New-DiamondPlateTexture {
    $size = 2048
    $random = [System.Random]::new(29617)
    $canvas = New-TextureCanvas $size ([System.Drawing.Color]::FromArgb(255, 73, 86, 91))
    $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(190, 20, 30, 35))
    $diamondLight = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 110, 126, 129))
    $diamondWorn = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 84, 98, 102))
    $highlightPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(90, 190, 199, 196), 0.35)
    $spacing = 14
    $row = 0
    for ($y = 0; $y -lt $size; $y += $spacing) {
        $offset = if (($row % 2) -eq 0) { 0 } else { $spacing * 0.5 }
        $column = 0
        for ($x = -$spacing; $x -lt $size + $spacing; $x += $spacing) {
            $centerX = $x + $offset
            $angle = if ((($row + $column) % 2) -eq 0) { 45.0 } else { -45.0 }
            Add-WrappedPolygon $canvas $shadow (Get-DiamondPoints $centerX $y 6.5 2.4 $angle 0.45 0.65)
            Add-WrappedPolygon $canvas $(if ($random.NextDouble() -lt 0.14) { $diamondWorn } else { $diamondLight }) (Get-DiamondPoints $centerX $y 6.0 2.0 $angle)
            $shineLength = 1.8
            $angleRadians = $angle * [Math]::PI / 180.0
            Add-WrappedLine $canvas $highlightPen ($centerX - ([Math]::Cos($angleRadians) * $shineLength)) ($y - ([Math]::Sin($angleRadians) * $shineLength) - 0.35) ($centerX + ([Math]::Cos($angleRadians) * $shineLength)) ($y + ([Math]::Sin($angleRadians) * $shineLength) - 0.35)
            $column++
        }
        $row++
    }

    $abrasionPens = @(
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(60, 220, 224, 219), 0.3),
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(65, 18, 29, 34), 0.35),
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(48, 37, 49, 53), 0.45)
    )
    for ($index = 0; $index -lt 15000; $index++) {
        $x = $random.NextDouble() * $size
        $y = $random.NextDouble() * $size
        $length = 0.5 + ($random.NextDouble() * 4.5)
        $angle = (-0.24 + ($random.NextDouble() * 0.48))
        Add-WrappedLine $canvas $abrasionPens[$random.Next(0, $abrasionPens.Count)] $x $y ($x + ([Math]::Cos($angle) * $length)) ($y + ([Math]::Sin($angle) * $length))
    }

    Save-TextureCanvas $canvas (Join-Path $textureRoot "diamond_plate.png")
    @($shadow, $diamondLight, $diamondWorn, $highlightPen) + $abrasionPens | ForEach-Object { $_.Dispose() }
}

function New-BrushedMetalTexture {
    $size = 2048
    $random = [System.Random]::new(41131)
    $canvas = New-TextureCanvas $size ([System.Drawing.Color]::FromArgb(255, 75, 88, 92))
    $brushPens = @(
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(78, 213, 219, 215), 0.28),
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(102, 20, 31, 36), 0.32),
        [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(66, 42, 55, 59), 0.45)
    )
    for ($index = 0; $index -lt 36000; $index++) {
        $x = $random.NextDouble() * $size
        $y = $random.NextDouble() * $size
        $length = 0.5 + ($random.NextDouble() * 8.0)
        $verticalDrift = -0.6 + ($random.NextDouble() * 1.2)
        Add-WrappedLine $canvas $brushPens[$random.Next(0, $brushPens.Count)] $x $y ($x + $length) ($y + $verticalDrift)
    }

    $pitBrushes = @(
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(105, 16, 27, 32)),
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(72, 218, 222, 216)),
        [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(80, 39, 51, 55))
    )
    for ($index = 0; $index -lt 14000; $index++) {
        $diameter = 0.3 + ($random.NextDouble() * 0.9)
        Add-WrappedEllipse $canvas $pitBrushes[$random.Next(0, $pitBrushes.Count)] ($random.NextDouble() * $size) ($random.NextDouble() * $size) $diameter $diameter
    }

    Save-TextureCanvas $canvas (Join-Path $textureRoot "brushed_metal.png")
    $brushPens + $pitBrushes | ForEach-Object { $_.Dispose() }
}

function New-MicroOverlay {
    param([string]$Kind, [int]$Seed, [string]$OutputName)
    $size = 512
    $random = [System.Random]::new($Seed)
    $canvas = New-TextureCanvas $size ([System.Drawing.Color]::Transparent) $true
    if ($Kind -eq "Concrete") {
        $brushes = @(
            [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(150, 18, 27, 29)),
            [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(122, 235, 231, 220)),
            [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(108, 50, 59, 58))
        )
        for ($index = 0; $index -lt 12000; $index++) {
            $diameter = 0.25 + ($random.NextDouble() * 0.75)
            Add-WrappedEllipse $canvas $brushes[$random.Next(0, $brushes.Count)] ($random.NextDouble() * $size) ($random.NextDouble() * $size) $diameter $diameter
        }
        $pens = @(
            [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(105, 20, 29, 31), 0.28),
            [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(80, 231, 226, 216), 0.25)
        )
    }
    else {
        $brushes = @(
            [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(126, 18, 29, 34)),
            [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(92, 231, 233, 226))
        )
        for ($index = 0; $index -lt 8000; $index++) {
            $diameter = 0.25 + ($random.NextDouble() * 0.65)
            Add-WrappedEllipse $canvas $brushes[$random.Next(0, $brushes.Count)] ($random.NextDouble() * $size) ($random.NextDouble() * $size) $diameter $diameter
        }
        $pens = @(
            [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(126, 14, 25, 31), 0.28),
            [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(104, 235, 236, 229), 0.24),
            [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(88, 43, 55, 59), 0.38)
        )
    }
    for ($index = 0; $index -lt 12000; $index++) {
        $x = $random.NextDouble() * $size
        $y = $random.NextDouble() * $size
        $length = 0.45 + ($random.NextDouble() * $(if ($Kind -eq "Concrete") { 2.8 } else { 4.0 }))
        $angle = if ($Kind -eq "Concrete") { $random.NextDouble() * [Math]::PI * 2.0 } else { -0.28 + ($random.NextDouble() * 0.56) }
        Add-WrappedLine $canvas $pens[$random.Next(0, $pens.Count)] $x $y ($x + ([Math]::Cos($angle) * $length)) ($y + ([Math]::Sin($angle) * $length))
    }
    Save-TextureCanvas $canvas (Join-Path $overlayRoot $OutputName)
    $brushes + $pens | ForEach-Object { $_.Dispose() }
}

New-ConcreteTexture
New-DiamondPlateTexture
New-BrushedMetalTexture
New-MicroOverlay "Concrete" 58109 "micro_concrete.png"
New-MicroOverlay "Metal" 69061 "micro_metal_wear.png"

Write-Output "MICRO_TEXTURE_GENERATION_PASS: concrete, diamond plate, brushed metal and two material-specific wear overlays generated."

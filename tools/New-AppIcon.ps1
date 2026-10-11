param(
    [string] $OutputPath = (Join-Path $PSScriptRoot '..\BlueH\Assets\BlueH.ico'),
    [string] $PreviewPath = ''
)

Add-Type -AssemblyName PresentationCore, WindowsBase

$designSize = 256.0
$sizes = @(16, 24, 32, 48, 64, 256)
$smallestSizeWithRune = 48

function New-FrozenBrush([string] $hex) {
    $brush = [Windows.Media.SolidColorBrush]::new([Windows.Media.ColorConverter]::ConvertFromString($hex))
    $brush.Freeze()
    $brush
}

$background = New-FrozenBrush '#15191E'
$edge = New-FrozenBrush '#2C343E'
$headphonesBlue = New-FrozenBrush '#0082FC'
$runeBlue = New-FrozenBrush '#4DA8FF'

function New-RoundPen($brush, [double] $thickness) {
    $pen = [Windows.Media.Pen]::new($brush, $thickness)
    $pen.StartLineCap = 'Round'
    $pen.EndLineCap = 'Round'
    $pen.LineJoin = 'Round'
    $pen.Freeze()
    $pen
}

function Render-Icon([int] $size) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $scale = $size / $designSize
    $context.PushTransform([Windows.Media.ScaleTransform]::new($scale, $scale))

    $edgePen = [Windows.Media.Pen]::new($edge, 6)
    $context.DrawRoundedRectangle($background, $edgePen, [Windows.Rect]::new(8, 8, 240, 240), 52, 52)

    $headband = [Windows.Media.Geometry]::Parse('M 64,138 A 64,64 0 0 1 192,138')
    $context.DrawGeometry($null, (New-RoundPen $headphonesBlue 20), $headband)
    $context.DrawRoundedRectangle($headphonesBlue, $null, [Windows.Rect]::new(42, 122, 44, 76), 16, 16)
    $context.DrawRoundedRectangle($headphonesBlue, $null, [Windows.Rect]::new(170, 122, 44, 76), 16, 16)

    if ($size -ge $smallestSizeWithRune) {
        $rune = [Windows.Media.Geometry]::Parse('M 108,114 L 148,150 L 128,168 L 128,92 L 148,110 L 108,146')
        $context.DrawGeometry($null, (New-RoundPen $runeBlue 10), $rune)
    }

    $context.Pop()
    $context.Close()

    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [IO.MemoryStream]::new()
    $encoder.Save($stream)
    $stream.ToArray()
}

$images = foreach ($size in $sizes) { , @{ Size = $size; Png = (Render-Icon $size) } }

$directory = Split-Path $OutputPath -Parent
[IO.Directory]::CreateDirectory($directory) | Out-Null

$output = [IO.MemoryStream]::new()
$writer = [IO.BinaryWriter]::new($output)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$images.Count)

$offset = 6 + 16 * $images.Count
foreach ($image in $images) {
    $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }
    $writer.Write([byte]$dimension)
    $writer.Write([byte]$dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$image.Png.Length)
    $writer.Write([uint32]$offset)
    $offset += $image.Png.Length
}

foreach ($image in $images) { $writer.Write([byte[]]$image.Png) }
$writer.Flush()
[IO.File]::WriteAllBytes($OutputPath, $output.ToArray())
"wrote $OutputPath ($($images.Count) sizes, $($output.Length) bytes)"

if ($PreviewPath) {
    [IO.Directory]::CreateDirectory($PreviewPath) | Out-Null
    foreach ($image in $images) {
        [IO.File]::WriteAllBytes((Join-Path $PreviewPath "icon-$($image.Size).png"), $image.Png)
    }
}

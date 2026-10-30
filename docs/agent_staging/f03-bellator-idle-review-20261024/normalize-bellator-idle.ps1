param(
    [string]$SourcePath = (Join-Path $PSScriptRoot 'source_generated.png'),
    [string]$OutputPath = (Join-Path $PSScriptRoot 'BellatorIdle_Prototype.png'),
    [string]$PreviewPath = (Join-Path $PSScriptRoot 'BellatorIdle_Prototype_preview.png')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$paletteHex = @(
    '#10111B','#1B1D2B','#2A2E40','#3F465C','#59647A','#8290A5','#BDC6CC','#E8E3CD',
    '#172C28','#24463A','#38654A','#59935C','#94BC74','#32251F','#50382B','#765137',
    '#A7794D','#54232A','#83333A','#BC5148','#E98664','#2D2445','#493363','#74528F',
    '#AA7DC2','#5E3F22','#9A6026','#D68F32','#F0C15A','#294C69','#4C86A0','#8FC1C1'
)
$palette = foreach ($hex in $paletteHex) { [System.Drawing.ColorTranslator]::FromHtml($hex) }
$source = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $SourcePath).Path)
$output = [System.Drawing.Bitmap]::new(128,48,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    if ($source.Width -ne 2033 -or $source.Height -ne 773) { throw "Unexpected source size $($source.Width)x$($source.Height). Review the crop contract before normalizing." }
    $frameStats = @()
    for ($frame=0; $frame -lt 4; $frame++) {
        $cellLeft = [int][Math]::Floor($source.Width * $frame / 4.0)
        $cellRight = [int][Math]::Floor($source.Width * ($frame + 1) / 4.0)
        $frameWidth = $cellRight - $cellLeft
        $minX = $cellRight; $maxX = $cellLeft - 1; $minY = $source.Height; $maxY = -1
        for ($y=0; $y -lt $source.Height; $y++) {
            for ($x=$cellLeft; $x -lt $cellRight; $x++) {
                if ($source.GetPixel($x,$y).A -ge 128) {
                    if ($x -lt $minX) {$minX=$x}; if ($x -gt $maxX) {$maxX=$x}
                    if ($y -lt $minY) {$minY=$y}; if ($y -gt $maxY) {$maxY=$y}
                }
            }
        }
        if ($maxX -lt $minX -or $maxY -lt $minY) { throw "Frame $frame has no opaque subject." }
        $bodyCenter = ($minX + $maxX) / 2.0
        $cellCenter = ($cellLeft + $cellRight - 1) / 2.0
        $cropLeft = $cellLeft - ($cellCenter - $bodyCenter)
        $cropTop = 5.0
        $cropHeight = 762.0
        for ($y=0; $y -lt 48; $y++) {
            $sy = [int][Math]::Floor($cropTop + (($y + 0.5) * $cropHeight / 48.0))
            for ($x=0; $x -lt 32; $x++) {
                $sx = [int][Math]::Floor($cropLeft + (($x + 0.5) * $frameWidth / 32.0))
                if ($sx -lt 0 -or $sx -ge $source.Width -or $sy -lt 0 -or $sy -ge $source.Height) {
                    $output.SetPixel(($frame * 32 + $x),$y,[System.Drawing.Color]::Transparent)
                    continue
                }
                $sample = $source.GetPixel($sx,$sy)
                if ($sample.A -lt 128) {
                    $output.SetPixel(($frame * 32 + $x),$y,[System.Drawing.Color]::Transparent)
                    continue
                }
                $best = $null; $bestDistance = [int]::MaxValue
                foreach ($candidate in $palette) {
                    $dr = [int]$sample.R - [int]$candidate.R
                    $dg = [int]$sample.G - [int]$candidate.G
                    $db = [int]$sample.B - [int]$candidate.B
                    $distance = $dr*$dr + $dg*$dg + $db*$db
                    if ($distance -lt $bestDistance) {$bestDistance=$distance; $best=$candidate}
                }
                $output.SetPixel(($frame * 32 + $x),$y,[System.Drawing.Color]::FromArgb(255,$best.R,$best.G,$best.B))
            }
        }
        $frameStats += [pscustomobject]@{Frame=$frame;OpaqueSourceBounds="$minX,$minY-$maxX,$maxY";HorizontalRecenterPixels=[Math]::Round($cellCenter-$bodyCenter,2);OutputCell="$($frame*32),0,32,48"}
    }
    $output.Save($OutputPath,[System.Drawing.Imaging.ImageFormat]::Png)
    $preview = [System.Drawing.Bitmap]::new(1024,384,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        for ($y=0; $y -lt 384; $y++) { for ($x=0; $x -lt 1024; $x++) { $preview.SetPixel($x,$y,$output.GetPixel([int][Math]::Floor($x/8.0),[int][Math]::Floor($y/8.0))) } }
        $preview.Save($PreviewPath,[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $preview.Dispose() }
    $unique = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $alphaValues = [System.Collections.Generic.HashSet[int]]::new()
    $opaque = 0
    for ($y=0; $y -lt 48; $y++) { for ($x=0; $x -lt 128; $x++) { $c=$output.GetPixel($x,$y); [void]$alphaValues.Add([int]$c.A); if($c.A -gt 0){$opaque++;[void]$unique.Add(('#{0:X2}{1:X2}{2:X2}' -f $c.R,$c.G,$c.B))} } }
    $allowed = [System.Collections.Generic.HashSet[string]]::new([string[]]$paletteHex,[System.StringComparer]::OrdinalIgnoreCase)
    $outside = @($unique | Where-Object { -not $allowed.Contains($_) })
    if ($outside.Count -gt 0 -or $alphaValues.Count -gt 2 -or $opaque -eq 0) { throw 'Output failed palette/alpha validation.' }
    [pscustomobject]@{Source="$($source.Width)x$($source.Height)";Output='128x48 RGBA PNG';Preview='1024x384 nearest-neighbor';Frames=$frameStats;OpaquePixels=$opaque;PaletteColors=$unique.Count;PaletteViolations=$outside.Count;AlphaValues=(@($alphaValues) -join ',')}
} finally { $output.Dispose(); $source.Dispose() }
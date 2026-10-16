$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$sourcePath = Join-Path $root 'docs\art\F01_bellator_generated.png'
$outputPath = Join-Path $root 'docs\art\F01_bellator_32x48.png'
$paletteHex = @(
    '10111B','1B1D2B','2A2E40','3F465C','59647A','8290A5','BDC6CC','E8E3CD',
    '172C28','24463A','38654A','59935C','94BC74','32251F','50382B','765137',
    'A7794D','54232A','83333A','BC5148','E98664','2D2445','493363','74528F',
    'AA7DC2','5E3F22','9A6026','D68F32','F0C15A','294C69','4C86A0','8FC1C1'
)
$palette = @($paletteHex | ForEach-Object { [System.Drawing.ColorTranslator]::FromHtml("#$_") })
$paletteSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$paletteHex | ForEach-Object { [void]$paletteSet.Add($_) }

function Get-PngHeader([string]$path) {
    [byte[]]$bytes = [IO.File]::ReadAllBytes($path)
    if ($bytes.Length -lt 26 -or [BitConverter]::ToString($bytes[0..7]).Replace('-','') -ne '89504E470D0A1A0A') { throw "Not a PNG: $path" }
    if ($bytes[24] -ne 8 -or $bytes[25] -ne 6) { throw "Expected an 8-bit RGBA PNG: $path" }
    return $bytes
}

[void](Get-PngHeader $sourcePath)
$source = [System.Drawing.Bitmap]::new($sourcePath)
$sourceWidth = $source.Width; $sourceHeight = $source.Height
if ($sourceWidth * 48 -ne $sourceHeight * 32) { throw "Source aspect ratio is not 32:48: $($sourceWidth)x$($sourceHeight)" }
$output = [System.Drawing.Bitmap]::new(32, 48, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
try {
    $sourceAlpha = [System.Collections.Generic.HashSet[int]]::new()
    $sourceTransparent = 0
    for ($y = 0; $y -lt $source.Height; $y++) {
        for ($x = 0; $x -lt $source.Width; $x++) {
            $alpha = $source.GetPixel($x, $y).A
            [void]$sourceAlpha.Add([int]$alpha)
            if ($alpha -eq 0) { $sourceTransparent++ }
        }
    }
    if ($sourceTransparent -eq 0) { throw 'Generated source has no transparent pixels' }

    for ($y = 0; $y -lt 48; $y++) {
        $sy = [Math]::Min($source.Height - 1, [int][Math]::Floor(((2 * $y + 1) * $source.Height) / 96.0))
        for ($x = 0; $x -lt 32; $x++) {
            $sx = [Math]::Min($source.Width - 1, [int][Math]::Floor(((2 * $x + 1) * $source.Width) / 64.0))
            $pixel = $source.GetPixel($sx, $sy)
            if ($pixel.A -lt 128) { $output.SetPixel($x, $y, [System.Drawing.Color]::Transparent); continue }
            $best = $palette[0]
            $bestDistance = [int]::MaxValue
            foreach ($candidate in $palette) {
                $dr = [int]$pixel.R - $candidate.R; $dg = [int]$pixel.G - $candidate.G; $db = [int]$pixel.B - $candidate.B
                $distance = $dr * $dr + $dg * $dg + $db * $db
                if ($distance -lt $bestDistance) { $best = $candidate; $bestDistance = $distance }
            }
            $output.SetPixel($x, $y, [System.Drawing.Color]::FromArgb(255, $best.R, $best.G, $best.B))
        }
    }
    $output.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $source.Dispose()
    $output.Dispose()
}

[byte[]]$outputBytes = Get-PngHeader $outputPath
if ($outputBytes[16..19] -join ',' -ne '0,0,0,32' -or $outputBytes[20..23] -join ',' -ne '0,0,0,48') { throw 'Output PNG dimensions are not 32x48' }
$image = [System.Drawing.Bitmap]::new($outputPath)
$used = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$occupied = [System.Collections.Generic.HashSet[int]]::new()
$opaqueCount = 0
try {
    for ($y = 0; $y -lt 48; $y++) {
        for ($x = 0; $x -lt 32; $x++) {
            $pixel = $image.GetPixel($x, $y)
            if ($pixel.A -notin @(0, 255)) { throw "Antialiased alpha at ($x,$y): $($pixel.A)" }
            if ($pixel.A -eq 255) {
                $color = '{0:X2}{1:X2}{2:X2}' -f $pixel.R, $pixel.G, $pixel.B
                if (-not $paletteSet.Contains($color)) { throw "Color outside palette at ($x,$y): #$color" }
                [void]$used.Add($color); [void]$occupied.Add($y * 32 + $x); $opaqueCount++
            }
        }
    }
}
finally { $image.Dispose() }
if ($opaqueCount -eq 0 -or $opaqueCount -eq 1536) { throw "Expected transparent and opaque pixels, got $opaqueCount opaque" }
$components = 0
while ($occupied.Count -gt 0) {
    $components++
    $start = 0; foreach ($entry in $occupied) { $start = $entry; break }
    [void]$occupied.Remove($start)
    $pending = [System.Collections.Generic.Stack[int]]::new(); $pending.Push($start)
    while ($pending.Count -gt 0) {
        $index = $pending.Pop(); $x = $index % 32; $y = [int][Math]::Floor($index / 32)
        if ($x -gt 0 -and $occupied.Remove($index - 1)) { $pending.Push($index - 1) }
        if ($x -lt 31 -and $occupied.Remove($index + 1)) { $pending.Push($index + 1) }
        if ($y -gt 0 -and $occupied.Remove($index - 32)) { $pending.Push($index - 32) }
        if ($y -lt 47 -and $occupied.Remove($index + 32)) { $pending.Push($index + 32) }
    }
}
if ($components -ne 1) { throw "Expected one 4-connected silhouette, got $components components" }
$sourceHash = (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.ToLowerInvariant()
$outputHash = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
"source=$sourcePath format=PNG size=$($sourceWidth)x$($sourceHeight) mode=RGBA distinct_alpha=$($sourceAlpha.Count) transparent_pixels=$sourceTransparent sha256=$sourceHash"
"output=$outputPath format=PNG size=32x48 mode=RGBA colors=$($used.Count) opaque_pixels=$opaqueCount connectivity=4 sha256=$outputHash"
'PASS: 8-bit RGBA PNG, exact 32x48 dimensions, binary alpha, fixed 32-color palette, and one 4-connected silhouette'

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = $PSScriptRoot
$pngPath = Join-Path $root 'BellatorIdle_Prototype_v2.png'
$recordPath = Join-Path $root 'RESOURCE_RECORD.v1.json'
$allowed = [System.Collections.Generic.HashSet[string]]::new([string[]]@(
    '#10111B','#1B1D2B','#2A2E40','#3F465C','#59647A','#8290A5','#BDC6CC','#E8E3CD',
    '#32251F','#50382B','#765137','#A7794D','#54232A','#83333A','#BC5148','#E98664',
    '#5E3F22','#9A6026','#D68F32','#F0C15A','#294C69','#4C86A0','#8FC1C1'
),[System.StringComparer]::OrdinalIgnoreCase)
$record = Get-Content -LiteralPath $recordPath -Raw | ConvertFrom-Json
$actualHash = (Get-FileHash -LiteralPath $pngPath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($actualHash -ne $record.sha256) { throw 'Resource record SHA-256 does not match the normalized sprite sheet.' }
$bitmap = [System.Drawing.Bitmap]::new((Resolve-Path -LiteralPath $pngPath).Path)
try {
    if ($bitmap.Width -ne 128 -or $bitmap.Height -ne 48) { throw 'Sprite sheet must be exactly 128x48.' }
    $frames = @()
    $globalColors = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    for ($frame=0; $frame -lt 4; $frame++) {
        $mask = New-Object 'bool[,]' 32,48
        $seen = New-Object 'bool[,]' 32,48
        $signature = [System.Text.StringBuilder]::new()
        $opaque = 0; $minY = 48; $maxY = -1
        for ($y=0; $y -lt 48; $y++) {
            for ($x=0; $x -lt 32; $x++) {
                $c = $bitmap.GetPixel($frame*32+$x,$y)
                if ($c.A -ne 0 -and $c.A -ne 255) { throw "Frame $frame has non-binary alpha." }
                [void]$signature.Append(('{0:X2}{1:X2}{2:X2}{3:X2}' -f $c.R,$c.G,$c.B,$c.A))
                if ($c.A -eq 255) {
                    $opaque++; $mask[$x,$y]=$true
                    if ($y -lt $minY) {$minY=$y}; if ($y -gt $maxY) {$maxY=$y}
                    $hex = '#{0:X2}{1:X2}{2:X2}' -f $c.R,$c.G,$c.B
                    if (-not $allowed.Contains($hex)) { throw "Frame $frame uses color outside its approved character palette: $hex" }
                    [void]$globalColors.Add($hex)
                }
            }
        }
        if ($opaque -eq 0) { throw "Frame $frame is empty." }
        $components = 0
        for ($y=0; $y -lt 48; $y++) {
            for ($x=0; $x -lt 32; $x++) {
                if ($mask[$x,$y] -and -not $seen[$x,$y]) {
                    $components++
                    $queue = [System.Collections.Generic.Queue[System.Drawing.Point]]::new()
                    $queue.Enqueue([System.Drawing.Point]::new($x,$y)); $seen[$x,$y]=$true
                    while ($queue.Count -gt 0) {
                        $point = $queue.Dequeue()
                        foreach ($delta in @(@(1,0),@(-1,0),@(0,1),@(0,-1))) {
                            $nx=$point.X+[int]$delta[0]; $ny=$point.Y+[int]$delta[1]
                            if ($nx -ge 0 -and $nx -lt 32 -and $ny -ge 0 -and $ny -lt 48 -and $mask[$nx,$ny] -and -not $seen[$nx,$ny]) {
                                $seen[$nx,$ny]=$true; $queue.Enqueue([System.Drawing.Point]::new($nx,$ny))
                            }
                        }
                    }
                }
            }
        }
        if ($components -ne 1) { throw "Frame $frame silhouette has $components four-neighbor components; expected 1." }
        $frames += [pscustomobject]@{Index=$frame;OpaquePixels=$opaque;Components=$components;MinY=$minY;MaxY=$maxY;Signature=$signature.ToString()}
    }
    if (($frames | Select-Object -ExpandProperty MaxY | Select-Object -Unique).Count -ne 1) { throw 'Frame baseline changed.' }
    for ($a=0; $a -lt 4; $a++) {
        for ($b=$a+1; $b -lt 4; $b++) {
            if ($frames[$a].Signature -ceq $frames[$b].Signature) { throw "Frames $a and $b are identical." }
        }
    }
    [pscustomobject]@{Result='PASS';Dimensions='128x48 RGBA';Frames=4;OpaquePixelsByFrame=(($frames | ForEach-Object OpaquePixels) -join ',');BaselineY=$frames[0].MaxY;ConnectedComponentsPerFrame='1';DistinctFrames='6/6 pair comparisons differ';UsedColors=$globalColors.Count;Alpha='0/255';Sha256=$actualHash}
} finally { $bitmap.Dispose() }
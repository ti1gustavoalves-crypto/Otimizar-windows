param([string]$Destination=(Join-Path $PSScriptRoot 'assets'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
New-Item -ItemType Directory -Force $Destination | Out-Null
$images=@()
foreach($size in @(16,24,32,48,64,128,256)) {
    $bitmap=New-Object Drawing.Bitmap($size,$size)
    $g=[Drawing.Graphics]::FromImage($bitmap);$g.SmoothingMode='AntiAlias';$g.Clear([Drawing.Color]::FromArgb(13,19,25));$g.ScaleTransform($size/100.0,$size/100.0)
    $accent=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(153,237,190));$ink=New-Object Drawing.SolidBrush([Drawing.Color]::FromArgb(13,19,25));$pen=New-Object Drawing.Pen($ink,4)
    $points=[Drawing.Point[]]@((New-Object Drawing.Point(18,8)),(New-Object Drawing.Point(76,8)),(New-Object Drawing.Point(88,20)),(New-Object Drawing.Point(88,39)),(New-Object Drawing.Point(57,39)),(New-Object Drawing.Point(57,52)),(New-Object Drawing.Point(77,52)),(New-Object Drawing.Point(77,70)),(New-Object Drawing.Point(43,70)),(New-Object Drawing.Point(43,92)),(New-Object Drawing.Point(18,92)))
    $g.FillPolygon($accent,$points);$g.FillRectangle($accent,5,33,9,18);$g.FillRectangle($accent,89,33,7,18);$g.DrawLine($pen,25,26,73,26)
    foreach($x in @(35,48,61)){$g.DrawLine($pen,$x,20,$x,32)}
    $g.FillRectangle($ink,27,43,9,9)
    $g.FillPolygon($ink,[Drawing.Point[]]@((New-Object Drawing.Point(47,43)),(New-Object Drawing.Point(37,62)),(New-Object Drawing.Point(46,62)),(New-Object Drawing.Point(33,83)),(New-Object Drawing.Point(58,56)),(New-Object Drawing.Point(47,56))))
    $stream=New-Object IO.MemoryStream;$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$images+=,@{Size=$size;Bytes=$stream.ToArray()}
    if($size -eq 256){$bitmap.Save((Join-Path $Destination 'frankenstein.png'),[Drawing.Imaging.ImageFormat]::Png)}
    $g.Dispose();$bitmap.Dispose();$stream.Dispose();$pen.Dispose();$accent.Dispose();$ink.Dispose()
}
$file=[IO.File]::Create((Join-Path $Destination 'frankenstein.ico'));$writer=New-Object IO.BinaryWriter($file)
try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$images.Count);$offset=6+16*$images.Count
    foreach($image in $images){$dimension=if($image.Size -eq 256){0}else{$image.Size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$image.Bytes.Length);$writer.Write([uint32]$offset);$offset+=$image.Bytes.Length}
    foreach($image in $images){$writer.Write([byte[]]$image.Bytes)}
}finally{$writer.Dispose();$file.Dispose()}

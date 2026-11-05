param([string]$PreviewRoot = 'docs/previews/DC-M4', [switch]$Workbench)
# Rasterizes only our generated rect/text SVG subset. Text uses the same
# Segoe UI / Consolas families measured by UiText; native glyphs come from a
# GDI-rasterized atlas at exact pixel sizes (see DC-M4.2 report). Not gameplay.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$files = if ($Workbench) { Get-ChildItem -LiteralPath $PreviewRoot -Filter 'workbench-*.svg' | Select-Object -ExpandProperty FullName }
    else { @('classic', 'fancy', 'compact', 'inspector') | ForEach-Object { Join-Path $PreviewRoot ($_ + '-completion.svg') } }
foreach ($file in $files) {
    [xml]$svg = Get-Content -LiteralPath $file -Raw -Encoding UTF8
    $width = [int]$svg.DocumentElement.GetAttribute('width'); $height = [int]$svg.DocumentElement.GetAttribute('height')
    $bitmap = New-Object System.Drawing.Bitmap($width, $height)
    $g = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#26343b'))
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
        foreach ($node in $svg.DocumentElement.ChildNodes) {
            if ($node.Name -notin @('rect', 'text')) { continue }
            $color = [System.Drawing.ColorTranslator]::FromHtml([string]$node.GetAttribute('fill'))
            $alpha = if ($node.HasAttribute('opacity')) { [double]::Parse($node.GetAttribute('opacity'), [Globalization.CultureInfo]::InvariantCulture) } else { 1.0 }
            $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb([int](255 * $alpha), $color))
            try {
                $x = [single]$node.GetAttribute('x'); $y = [single]$node.GetAttribute('y')
                if ($node.Name -eq 'rect') {
                    $w = if ($node.GetAttribute('width') -eq '100%') { $width } else { [single]$node.GetAttribute('width') }
                    $h = if ($node.GetAttribute('height') -eq '100%') { $height } else { [single]$node.GetAttribute('height') }
                    $g.FillRectangle($brush, $x, $y, $w, $h)
                } else {
                    $size = [single]$node.GetAttribute('font-size')
                    $family = if ($node.GetAttribute('font-family').StartsWith('Segoe')) { 'Segoe UI' } else { 'Consolas' }
                    $style = if ($node.GetAttribute('font-weight') -eq '600') { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
                    $font = New-Object System.Drawing.Font($family, $size, $style, [System.Drawing.GraphicsUnit]::Pixel)
                    try { $g.DrawString($node.InnerText, $font, $brush, $x, ($y - $size)) }
                    finally { $font.Dispose() }
                }
            } finally { $brush.Dispose() }
        }
        $name = [System.IO.Path]::GetFileNameWithoutExtension($file)
        $out = Join-Path $PreviewRoot ($(if ($Workbench) { $name } else { $name -replace '-completion$', '-comparison' }) + '.png')
        $bitmap.Save([System.IO.Path]::GetFullPath($out), [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "Rendered $name offline comparison."
    } finally { $g.Dispose(); $bitmap.Dispose() }
}

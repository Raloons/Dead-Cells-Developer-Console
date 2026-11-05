param(
    [string]$Version = "0.1.0-alpha",
    [string]$OutputRoot = "dist"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
Push-Location $repo
try {
    & dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false
    if ($LASTEXITCODE -ne 0) { throw "release build failed" }

    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot 'Privacy-Audit.ps1')
    if ($LASTEXITCODE -ne 0) { throw "privacy audit failed" }

    $build = Join-Path $repo 'src\DeveloperConsole\bin\Release\net10.0\output\DeveloperConsole'
    foreach ($required in @('DeveloperConsole.dll', 'modinfo.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $build $required) -PathType Leaf)) {
            throw "build output missing: $required"
        }
    }

    $out = Join-Path $repo $OutputRoot
    $pkg = Join-Path $out ("DeveloperConsole-" + $Version)
    if (Test-Path -LiteralPath $pkg) { Remove-Item -LiteralPath $pkg -Recurse -Force }
    New-Item -ItemType Directory -Path (Join-Path $pkg 'DeveloperConsole') -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $pkg 'docs') -Force | Out-Null

    Copy-Item -LiteralPath (Join-Path $build 'DeveloperConsole.dll') -Destination (Join-Path $pkg 'DeveloperConsole') -Force
    Copy-Item -LiteralPath (Join-Path $build 'modinfo.json') -Destination (Join-Path $pkg 'DeveloperConsole') -Force
    Copy-Item -LiteralPath (Join-Path $repo 'README.md') -Destination $pkg -Force
    Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination $pkg -Force
    Copy-Item -LiteralPath (Join-Path $repo 'THIRD_PARTY_NOTICES.md') -Destination $pkg -Force
    Copy-Item -LiteralPath (Join-Path $repo 'CONTRIBUTING.md') -Destination $pkg -Force
    foreach ($doc in @(
        'INSTALL.md', 'COMMANDS.md', 'TROUBLESHOOTING.md',
        'EQUIPMENT_NAMES.md', 'AUTOCOMPLETE.md', 'UI_MODES.md',
        'WORKBENCH.md', 'STEAM_LAUNCH_COMPATIBILITY.md',
        'DC-M5_PUBLIC_RELEASE_REPORT.md',
        'RELEASE_NOTES_v0.1.0-alpha.md')) {
        Copy-Item -LiteralPath (Join-Path $repo ('docs\' + $doc)) `
            -Destination (Join-Path $pkg 'docs') -Force
    }
    Copy-Item -LiteralPath (Join-Path $repo 'docs\RELEASE_NOTES_v0.1.0-alpha.md') -Destination (Join-Path $pkg 'RELEASE_NOTES.md') -Force
    # No preview galleries ship: workbench/sampler artifacts are obsolete UI
    # evidence. Historical reports and galleries remain in git as archives.

    $sums = @()
    foreach ($f in (Get-ChildItem -LiteralPath $pkg -Recurse -File | Sort-Object FullName)) {
        $rel = $f.FullName.Substring($pkg.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        $h = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
        $sums += ($h + '  ' + ($rel -replace '\\', '/'))
    }
    $sums | Set-Content -LiteralPath (Join-Path $pkg 'SHA256SUMS.txt') -Encoding ASCII

    $zip = Join-Path $out ("DeveloperConsole-" + $Version + '.zip')
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $fixedTime = [DateTimeOffset]::new(2026, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
    $zipStream = [System.IO.File]::Open($zip, [System.IO.FileMode]::Create)
    try {
        $writer = [System.IO.Compression.ZipArchive]::new(
            $zipStream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            foreach ($f in (Get-ChildItem -LiteralPath $pkg -Recurse -File | Sort-Object FullName)) {
                $rel = $f.FullName.Substring($pkg.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
                $entryName = $rel -replace '\\', '/'
                if ($entryName -eq 'SHA256SUMS.txt') { continue } # added below, first
                $entry = $writer.CreateEntry($entryName, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $fixedTime
                $src = $f.OpenRead()
                try { $dst = $entry.Open(); try { $src.CopyTo($dst) } finally { $dst.Dispose() } }
                finally { $src.Dispose() }
            }
            $sumsEntry = $writer.CreateEntry('SHA256SUMS.txt', [System.IO.Compression.CompressionLevel]::Optimal)
            $sumsEntry.LastWriteTime = $fixedTime
            $sumsBytes = [System.Text.Encoding]::ASCII.GetBytes(($sums -join "`n") + "`n")
            $sumsStream = $sumsEntry.Open()
            try { $sumsStream.Write($sumsBytes, 0, $sumsBytes.Length) } finally { $sumsStream.Dispose() }
        } finally { $writer.Dispose() }
    } finally { $zipStream.Dispose() }
    $zipHash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
    ($zipHash + '  ' + (Split-Path $zip -Leaf)) | Set-Content -LiteralPath ($zip + '.sha256') -Encoding ASCII

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try { $entries = @($archive.Entries | ForEach-Object { $_.FullName }) }
    finally { $archive.Dispose() }

    $bad = @($entries | Where-Object {
        $_ -match '\.(exe|pak|dat|pdb|hlboot|sav)$' -or
        ($_ -match '\.dll$' -and $_ -notmatch '(^|[\\/])DeveloperConsole[\\/]DeveloperConsole\.dll$')
    })
    if ($bad.Count -gt 0) { throw "release package contains unexpected binary: $($bad -join ', ')" }

    # The private-content denylist lives outside the public tree.
    $privateTerms = @()
    $denyPath = $env:DC_PRIVATE_DENYLIST
    if (-not [string]::IsNullOrWhiteSpace($denyPath) -and (Test-Path -LiteralPath $denyPath -PathType Leaf)) {
        $privateTerms = @(Get-Content -LiteralPath $denyPath |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_.Length -gt 0 })
    }
    if ($privateTerms.Count -gt 0) {
        $nameHits = @($entries | Where-Object { $entry = $_; ($privateTerms | Where-Object { $entry -match [regex]::Escape($_) }).Count -gt 0 })
        if ($nameHits.Count -gt 0) { throw "release package contains private-content names: $($nameHits -join ', ')" }
        Write-Host "Private-content name scan: PASS"
    } else {
        Write-Host "Private-content name scan: skipped (DC_PRIVATE_DENYLIST not configured)."
    }

    # Text scans alone miss compiler CodeView paths and embedded strings. Check
    # the sole shipped binary in both common string encodings before sealing.
    $dllBytes = [System.IO.File]::ReadAllBytes((Join-Path $pkg 'DeveloperConsole\DeveloperConsole.dll'))
    $binaryStrings = @([System.Text.Encoding]::ASCII.GetString($dllBytes), [System.Text.Encoding]::Unicode.GetString($dllBytes))
    foreach ($term in (@('C:\Users\', 'D:\') + $privateTerms)) {
        foreach ($text in $binaryStrings) {
            if ($text.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw 'release DLL contains a prohibited path or external private-content marker'
            }
        }
    }
    Write-Host 'Release binary privacy: PASS (ASCII/UTF-16 path and external denylist scan)'

    Write-Host "Release package: $zip"
    Write-Host "SHA-256: $zipHash"
    Write-Host "Entries:"
    $entries | Sort-Object | ForEach-Object { Write-Host "  $_" }
    Write-Host "No game binaries or fixture data were included."
}
finally {
    Pop-Location
}

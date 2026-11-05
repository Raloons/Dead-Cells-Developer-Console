param(
    [string]$Destination = "dist/public-source",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$dest = [System.IO.Path]::GetFullPath((Join-Path $repo $Destination))
$repoFull = [System.IO.Path]::GetFullPath($repo)

if ($dest.Equals($repoFull, [StringComparison]::OrdinalIgnoreCase)) {
    throw "destination must not be the repository root"
}
if ($dest.StartsWith($repoFull + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
    -not $dest.StartsWith((Join-Path $repoFull 'dist') + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "destination inside the working tree must be under dist/"
}

if (Test-Path -LiteralPath $dest) {
    if (-not $Force) { throw "destination exists; use -Force to replace: $dest" }
    Remove-Item -LiteralPath $dest -Recurse -Force
}
New-Item -ItemType Directory -Path $dest -Force | Out-Null

Push-Location $repo
try {
    $tracked = @(& git ls-files)
    foreach ($rel in $tracked) {
        $src = Join-Path $repo $rel
        if (-not (Test-Path -LiteralPath $src -PathType Leaf)) { continue }
        $out = Join-Path $dest $rel
        New-Item -ItemType Directory -Path (Split-Path -Parent $out) -Force | Out-Null
        Copy-Item -LiteralPath $src -Destination $out -Force
    }
} finally { Pop-Location }

$manifest = @()
foreach ($f in (Get-ChildItem -LiteralPath $dest -Recurse -File | Sort-Object FullName)) {
    $rel = $f.FullName.Substring($dest.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar) -replace '\\', '/'
    $manifest += [ordered]@{
        file = $rel
        bytes = $f.Length
        sha256 = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash
    }
}
$manifest | ConvertTo-Json -Depth 5 |
    Set-Content -LiteralPath (Join-Path $dest 'PUBLIC_EXPORT_MANIFEST.json') -Encoding ASCII

Push-Location $dest
try {
    & git init -b main | Out-Null
    & git add . | Out-Null
    & powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $dest 'tools/Privacy-Audit.ps1')
    if ($LASTEXITCODE -ne 0) { throw "public export privacy audit failed" }
    & git -c user.name="Raloons" -c user.email="raloons@users.noreply.github.com" `
        commit -m "Developer Console v0.1.0-alpha public source" | Out-Null
    $commit = (& git rev-parse --short HEAD).Trim()
    Write-Host "Public source export: $dest"
    Write-Host "Files: $($manifest.Count)"
    Write-Host "Commit: $commit"
    Write-Host "No remote configured. Nothing pushed."
} finally { Pop-Location }

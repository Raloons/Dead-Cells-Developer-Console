param(
    [Parameter(Mandatory = $true)]
    [string]$DeadCellsRoot,
    [string]$Configuration = "Release",
    [switch]$Force
)

$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent $PSScriptRoot
$source = Join-Path $repo "src\DeveloperConsole\bin\$Configuration\net10.0\output\DeveloperConsole"
$modsRoot = Join-Path $DeadCellsRoot "coremod\mods"
$destination = Join-Path $modsRoot "DeveloperConsole"

if (-not (Test-Path -LiteralPath $source -PathType Container)) {
    throw "Build output not found: $source. Build the mod first."
}
if (-not (Test-Path -LiteralPath (Join-Path $source "modinfo.json") -PathType Leaf)) {
    throw "Build output is missing modinfo.json: $source"
}
if (-not (Test-Path -LiteralPath $DeadCellsRoot -PathType Container)) {
    throw "Target game root does not exist: $DeadCellsRoot"
}
if (-not (Test-Path -LiteralPath (Join-Path $DeadCellsRoot "DeadCellsModding.exe") -PathType Leaf)) {
    throw "Target does not contain DeadCellsModding.exe: $DeadCellsRoot"
}
if (-not (Test-Path -LiteralPath $modsRoot -PathType Container)) {
    throw "Target does not contain coremod/mods: $modsRoot"
}
if ((Test-Path -LiteralPath $destination) -and -not $Force) {
    throw "Destination already exists: $destination. Use -Force to replace only this mod folder."
}

if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}

New-Item -ItemType Directory -Path $destination | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $destination -Recurse -Force

$expected = @("DeveloperConsole.dll", "DeveloperConsole.pdb", "modinfo.json")
$actual = Get-ChildItem -LiteralPath $destination -File | Select-Object -ExpandProperty Name
$missing = $expected | Where-Object { $actual -notcontains $_ }
if ($missing) {
    throw "Staged mod is missing expected file(s): $($missing -join ', ')"
}
$unexpectedDlls = Get-ChildItem -LiteralPath $destination -Filter *.dll |
    Where-Object { $_.Name -ne "DeveloperConsole.dll" }
if ($unexpectedDlls) {
    throw "Unexpected DLL(s) staged: $($unexpectedDlls.Name -join ', ')"
}

$manifest = Get-ChildItem -LiteralPath $destination -File |
    Where-Object { $_.Name -ne "staged-manifest.json" } |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            file = $_.Name
            bytes = $_.Length
            sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
$manifest | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $destination "staged-manifest.json") -Encoding UTF8

Write-Host "Staged DeveloperConsole $Configuration into:"
Write-Host "  $destination"
Write-Host "Files:"
$actual | ForEach-Object { Write-Host "  $_" }
Write-Host ""
Write-Host "No game process was started and no save files were touched."
Write-Host "Launch the disposable tree with DeadCellsModding.exe, enter a run, press '='."

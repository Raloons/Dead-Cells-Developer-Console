param(
    [Parameter(Mandatory = $true)]
    [string]$SourceGameRoot,
    [Parameter(Mandatory = $true)]
    [string]$FixtureRoot,
    [Parameter(Mandatory = $true)]
    [string]$ModBuildDir,
    [switch]$SkipCopy,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

function Normalize([string]$p) {
    $s = $p
    if ($s.StartsWith('\\?\')) { $s = $s.Substring(4) }
    try { $s = [System.IO.Path]::GetFullPath($s) } catch { }
    return $s.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}
function Test-Inside([string]$c, [string]$r) {
    $cc = Normalize $c
    $rr = Normalize $r
    return (($cc.Length -gt $rr.Length -and $cc.StartsWith($rr + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) -or $cc.Equals($rr, [StringComparison]::OrdinalIgnoreCase))
}
function Sha256([string]$path) {
    $h = [System.Security.Cryptography.SHA256]::Create()
    try {
        $s = [System.IO.File]::OpenRead($path)
        try { return ([BitConverter]::ToString($h.ComputeHash($s))).Replace('-', '').ToUpperInvariant() }
        finally { $s.Close() }
    } finally { $h.Dispose() }
}

$src = Normalize $SourceGameRoot
$dst = Normalize $FixtureRoot
$build = Normalize $ModBuildDir

if (-not (Test-Path -LiteralPath $src -PathType Container)) { throw "source-missing: $src" }
if (-not (Test-Path -LiteralPath $build -PathType Container)) { throw "build-missing: $build" }
if ($dst.Equals($src, [StringComparison]::OrdinalIgnoreCase)) { throw "fixture-equals-source: refused" }
if (-not (Test-Path -LiteralPath (Join-Path $src 'coremod\core\host\startup\DeadCellsModding.exe') -PathType Leaf)) {
    throw "source is not a DCCM game tree: $src"
}
if (-not (Test-Path -LiteralPath (Join-Path $build 'modinfo.json') -PathType Leaf)) {
    throw "build is missing modinfo.json: $build"
}

$protected = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Dead Cells'),
    [System.IO.Path]::Combine($env:ProgramFilesX86, 'Steam')
)
foreach ($p in $protected) {
    if ((Test-Inside $dst $p) -or (Test-Inside $p $dst)) { throw "protected-overlap: $p" }
}
if ((Test-Inside $src $dst) -or (Test-Inside $dst $src)) { throw "source/fixture nesting refused" }

if (-not $SkipCopy) {
    if (Test-Path -LiteralPath $dst) {
        if (-not $Force) { throw "fixture-root-exists: $dst (use -Force to replace)" }
        Remove-Item -LiteralPath $dst -Recurse -Force
    }
    New-Item -ItemType Directory -Path $dst -Force | Out-Null
    $rc = Start-Process -FilePath 'robocopy.exe' `
        -ArgumentList ('"' + $src + '" "' + $dst + '" /E /NFL /NDL /NJH /NJS /R:2 /W:1 /XD save logs dccm') `
        -Wait -PassThru -NoNewWindow
    if ($rc.ExitCode -ge 8) { throw "robocopy-failed: $($rc.ExitCode)" }
} else {
    if (-not (Test-Path -LiteralPath (Join-Path $dst 'coremod\core\host\startup\DeadCellsModding.exe') -PathType Leaf)) {
        throw "existing fixture is not a DCCM tree: $dst"
    }
}

# No production mods, plugins, config, saves, logs, Workshop, or stale caches.
$coreMods = Join-Path $dst 'coremod\mods'
if (Test-Path -LiteralPath $coreMods) {
    Get-ChildItem -LiteralPath $coreMods -Force | Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $coreMods -Force | Out-Null
}
$corePlugins = Join-Path $dst 'coremod\plugins'
if (Test-Path -LiteralPath $corePlugins) {
    Get-ChildItem -LiteralPath $corePlugins -Force |
        Where-Object { $_.Name -ne 'ModCore.ModLoader.Default' } |
        Remove-Item -Recurse -Force
} else {
    New-Item -ItemType Directory -Path $corePlugins -Force | Out-Null
}
$cfgDir = Join-Path $dst 'coremod\config'
if (Test-Path -LiteralPath $cfgDir) {
    Get-ChildItem -LiteralPath $cfgDir -Force | Remove-Item -Recurse -Force
}
# coremod\data belongs to production mods (private mod data lives here).
$dataDir = Join-Path $dst 'coremod\data'
if (Test-Path -LiteralPath $dataDir) {
    Get-ChildItem -LiteralPath $dataDir -Force | Remove-Item -Recurse -Force
}
# Stale extra-mod path caches can point at production directories.
$cacheDir = Join-Path $dst 'coremod\cache'
foreach ($cacheFile in @(
    'last_load_extra_mods_paths',
    'last_load_extra_mods_paths.cache',
    'last_load_extra_plugins_paths',
    'last_load_extra_plugins_paths.cache'
)) {
    $cp = Join-Path $cacheDir $cacheFile
    if (Test-Path -LiteralPath $cp) { Remove-Item -LiteralPath $cp -Force }
}
foreach ($sd in @('save', 'GameRoot\save')) {
    $sp = Join-Path $dst $sd
    if (Test-Path -LiteralPath $sp) {
        Get-ChildItem -LiteralPath $sp -Force | Remove-Item -Recurse -Force
    } else {
        New-Item -ItemType Directory -Path $sp -Force | Out-Null
    }
}
foreach ($wd in @('workshop', 'dccm\workshop')) {
    if (Test-Path -LiteralPath (Join-Path $dst $wd)) {
        Remove-Item -LiteralPath (Join-Path $dst $wd) -Recurse -Force
    }
}

# The override roots used by the session launcher.
$overrideMods = Join-Path $dst 'dccm\mods'
$overridePlugins = Join-Path $dst 'dccm\plugins'
$overrideCache = Join-Path $dst 'dccm\cache'
$overrideLogs = Join-Path $dst 'dccm\logs'
$overrideConfig = Join-Path $dst 'dccm\config'
$overrideData = Join-Path $dst 'dccm\data'
$overrideTemp = Join-Path $dst 'dccm\temp'
foreach ($d in @($overrideMods, $overridePlugins, $overrideCache, $overrideLogs, $overrideConfig, $overrideData, $overrideTemp)) {
    New-Item -ItemType Directory -Path $d -Force | Out-Null
}
foreach ($d in @($overrideMods, $overridePlugins, $overrideLogs, $overrideConfig, $overrideData, $overrideTemp)) {
    Get-ChildItem -LiteralPath $d -Force | Remove-Item -Recurse -Force
}

# Install the console package into the override mod root.
$modDst = Join-Path $overrideMods 'DeveloperConsole'
New-Item -ItemType Directory -Path $modDst -Force | Out-Null
Copy-Item -Path (Join-Path $build '*') -Destination $modDst -Recurse -Force
$unexpected = Get-ChildItem -LiteralPath $modDst -Filter *.dll |
    Where-Object { $_.Name -ne 'DeveloperConsole.dll' }
if ($unexpected) { throw "unexpected-dll-in-package: $($unexpected.Name -join ', ')" }

# Pin the framework loader and hlboot cache from the source install.
$loaderSrc = Join-Path $src 'coremod\plugins\ModCore.ModLoader.Default\ModCore.ModLoader.Default.dll'
if (-not (Test-Path -LiteralPath $loaderSrc -PathType Leaf)) { throw "loader-missing: $loaderSrc" }
$loaderDst = Join-Path $overridePlugins 'ModCore.ModLoader.Default'
New-Item -ItemType Directory -Path $loaderDst -Force | Out-Null
Copy-Item -LiteralPath $loaderSrc -Destination (Join-Path $loaderDst 'ModCore.ModLoader.Default.dll') -Force
$hlbootSrc = Join-Path $src 'coremod\cache\hlboot.dat'
if (-not (Test-Path -LiteralPath $hlbootSrc -PathType Leaf)) { throw "hlboot-missing: $hlbootSrc" }
Copy-Item -LiteralPath $hlbootSrc -Destination (Join-Path $overrideCache 'hlboot.dat') -Force

# Record versions and the exact staged package hashes.
$modVersion = (Get-Content -LiteralPath (Join-Path $build 'modinfo.json') -Raw | ConvertFrom-Json)
$dccmVersion = 'unknown'
$coreVersionFile = Join-Path $dst 'coremod\ModCoreVersion.txt'
if (Test-Path -LiteralPath $coreVersionFile -PathType Leaf) {
    $dccmVersion = (Get-Content -LiteralPath $coreVersionFile -Raw).Trim()
}
$package = Get-ChildItem -LiteralPath $modDst -File |
    Sort-Object Name |
    ForEach-Object {
        [pscustomobject]@{
            file = $_.Name
            bytes = $_.Length
            sha256 = Sha256 $_.FullName
        }
    }
$manifest = [ordered]@{
    schema = 'dc-m2-fixture/1'
    fixtureRoot = $dst
    game = 'Dead Cells v35'
    dccm = $dccmVersion
    mod = [ordered]@{
        name = $modVersion.name
        version = $modVersion.version
        main = $modVersion.main
        assemblies = @($modVersion.assemblies)
    }
    package = $package
    loader = [ordered]@{
        file = 'dccm\plugins\ModCore.ModLoader.Default\ModCore.ModLoader.Default.dll'
        sha256 = Sha256 (Join-Path $loaderDst 'ModCore.ModLoader.Default.dll')
    }
    hlboot = [ordered]@{
        file = 'dccm\cache\hlboot.dat'
        sha256 = Sha256 (Join-Path $overrideCache 'hlboot.dat')
    }
    saveRoots = @('save', 'GameRoot\save')
    note = 'Local disposable fixture. Contains no private expansion content. Do not launch without explicit authorization.'
}
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $dst 'fixture-manifest.json') -Encoding UTF8

Write-Host "Fixture prepared: $dst"
Write-Host "DCCM: $dccmVersion"
Write-Host "Console: $($modVersion.name) $($modVersion.version)"
Write-Host "Package files:"
$package | ForEach-Object { Write-Host ("  {0}  {1} bytes  {2}" -f $_.file, $_.bytes, $_.sha256) }
Write-Host "No game process was started and no production save or installation files were modified."

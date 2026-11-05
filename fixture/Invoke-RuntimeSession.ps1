param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureRoot,
    [Parameter(Mandatory = $true)]
    [string]$ReceiptPath,
    [Parameter(Mandatory = $true)]
    [string]$AuthorizedBy,
    [Parameter(Mandatory = $true)]
    [string]$SessionName,
    [int]$RunSeconds = 120,
    [string]$OutputDir = '',
    [string]$ExpectedBuildDir = '',
    [switch]$AllowSaves,
    [switch]$Authorize
)

$ErrorActionPreference = "Stop"

function Normalize([string]$p) {
    $s = $p
    if ($s.StartsWith('\\?\')) { $s = $s.Substring(4) }
    try { $s = [System.IO.Path]::GetFullPath($s) } catch { }
    return $s.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}
function Sha256([string]$path) {
    $h = [System.Security.Cryptography.SHA256]::Create()
    try {
        $s = [System.IO.File]::OpenRead($path)
        try { return ([BitConverter]::ToString($h.ComputeHash($s))).Replace('-', '').ToUpperInvariant() }
        finally { $s.Close() }
    } finally { $h.Dispose() }
}
function Get-TreeHash([string]$root) {
    if (-not (Test-Path -LiteralPath $root)) { return 'ABSENT' }
    $lines = @()
    foreach ($f in (Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction SilentlyContinue | Sort-Object FullName)) {
        $rel = $f.FullName.Substring($root.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        $lines += ($rel + '|' + $f.Length + '|' + (Sha256 $f.FullName))
    }
    $joined = ($lines -join "`n")
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($joined)
    $h = [System.Security.Cryptography.SHA256]::Create()
    try { return ([BitConverter]::ToString($h.ComputeHash($bytes))).Replace('-', '').ToUpperInvariant() }
    finally { $h.Dispose() }
}
function Get-DirSignature([string]$root) {
    if (-not (Test-Path -LiteralPath $root)) { return 'ABSENT' }
    $lines = @()
    foreach ($f in (Get-ChildItem -LiteralPath $root -Recurse -Force -File -ErrorAction SilentlyContinue | Sort-Object FullName)) {
        $rel = $f.FullName.Substring($root.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
        $lines += ($rel + '|' + $f.Length + '|' + $f.LastWriteTimeUtc.Ticks)
    }
    return ($lines -join "`n")
}

if (-not $Authorize) {
    Write-Output 'BLOCKED: explicit -Authorize switch is required for a live session.'
    Write-Output 'No process was started.'
    exit 2
}
if ($RunSeconds -lt 30 -or $RunSeconds -gt 300) { throw 'window-out-of-bounds: 30-300 seconds' }
if ([string]::IsNullOrWhiteSpace($AuthorizedBy)) { throw 'authorizedBy is required' }
if ([string]::IsNullOrWhiteSpace($SessionName)) { throw 'session name is required' }

$R = Normalize $FixtureRoot
if (-not (Test-Path -LiteralPath $R -PathType Container)) { throw "fixture-missing: $R" }
$receipt = Normalize $ReceiptPath
if (-not (Test-Path -LiteralPath $receipt -PathType Leaf)) { throw "authorization-receipt-missing: $receipt" }

# Admission is fail-closed and re-checks the receipt, hashes, package set,
# private-content absence, save roots, and DCCM version.
$admissionArgs = @(
    '-NoProfile', '-ExecutionPolicy', 'Bypass',
    '-File', (Join-Path $PSScriptRoot 'Test-FixtureAdmission.ps1'),
    '-FixtureRoot', $R,
    '-ReceiptPath', $receipt,
    '-Mode', 'Run'
)
if ($ExpectedBuildDir) { $admissionArgs += @('-ExpectedBuildDir', (Normalize $ExpectedBuildDir)) }
if ($AllowSaves) { $admissionArgs += '-AllowSaves' }
$admission = & powershell @admissionArgs 2>&1
$admission | ForEach-Object { Write-Output $_ }
if ($LASTEXITCODE -ne 0) {
    Write-Output 'REFUSED: fixture admission failed; no process was started.'
    exit 3
}

$running = @(Get-Process -Name steam, steamwebhelper, GameOverlayUI, DeadCells, deadcells_gl, DeadCellsModding -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    Write-Output ('REFUSED: game/Steam process already running: ' + (($running | ForEach-Object { $_.Name }) -join ', '))
    exit 4
}

$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
if ([string]::IsNullOrWhiteSpace($OutputDir)) { $OutputDir = Join-Path $R ('dc-m2-run-' + $stamp) }
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

$prodGame = (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Dead Cells')
$prodSave = Join-Path $prodGame 'save'
$prodConfig = Join-Path $prodGame 'coremod\config'
$prodMods = Join-Path $prodGame 'coremod\mods'
$prodPlugins = Join-Path $prodGame 'coremod\plugins'
$preSave = Get-TreeHash $prodSave
$preConfig = Get-TreeHash $prodConfig
$preMods = Get-DirSignature $prodMods
$prePlugins = Get-DirSignature $prodPlugins
Write-Output 'PASS production-preflight-snapshot'

$exe = Join-Path $R 'coremod\core\host\startup\DeadCellsModding.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { throw "framework-entry-absent: $exe" }
$stdout = Join-Path $OutputDir 'stdout.log'
$stderr = Join-Path $OutputDir 'stderr.log'
$traceDir = Join-Path $R 'dccm\logs\console'
New-Item -ItemType Directory -Path $traceDir -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $R 'dccm\temp') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $R 'dccm\config') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $R 'dccm\cache') -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $R 'dccm\data') -Force | Out-Null

$envMap = [ordered]@{
    DEAD_CELLS_GAME_PATH = $R
    DCCM_OverridePath_CORE_ROOT = (Join-Path $R 'coremod')
    DCCM_OverridePath_CORE_NATIVE_ROOT = (Join-Path $R 'coremod\core\native')
    DCCM_OverridePath_CORE_CURRENT_NATIVE_ROOT = (Join-Path $R 'coremod\core\native\win-x64')
    DCCM_OverridePath_GAME_ROOT = $R
    DCCM_OverridePath_SAVE_ROOT = (Join-Path $R 'save')
    DCCM_OverridePath_CORE_MODS = (Join-Path $R 'dccm\mods')
    DCCM_OverridePath_CORE_PLUGINS = (Join-Path $R 'dccm\plugins')
    DCCM_OverridePath_CORE_LOGS = (Join-Path $R 'dccm\logs')
    DCCM_OverridePath_CORE_CONFIG = (Join-Path $R 'dccm\config')
    DCCM_OverridePath_CORE_CACHE = (Join-Path $R 'dccm\cache')
    DCCM_OverridePath_CORE_DATA = (Join-Path $R 'dccm\data')
    DCCM_HLBOOT_PATH = (Join-Path $R 'dccm\cache\hlboot.dat')
    DCCM_EXTRA_MODS_PATHS = ';'
    DCCM_EXTRA_PLUGINS_PATHS = ';'
    TEMP = (Join-Path $R 'dccm\temp')
    TMP = (Join-Path $R 'dccm\temp')
    SystemRoot = $env:SystemRoot
    OS = $env:OS
    Path = ($env:SystemRoot + ';' + (Join-Path $env:SystemRoot 'System32'))
}

$savedEnv = @{}
foreach ($k in $envMap.Keys) {
    $savedEnv[$k] = [Environment]::GetEnvironmentVariable($k, 'Process')
    [Environment]::SetEnvironmentVariable($k, [string]$envMap[$k], 'Process')
}

$startUtc = [DateTime]::UtcNow
$proc = $null
try {
    $proc = Start-Process -FilePath $exe -WorkingDirectory $R `
        -RedirectStandardOutput $stdout -RedirectStandardError $stderr `
        -PassThru
} finally {
    foreach ($k in $savedEnv.Keys) {
        [Environment]::SetEnvironmentVariable($k, $savedEnv[$k], 'Process')
    }
}

Write-Output ("PASS launch pid=" + $proc.Id + " session=" + $SessionName)
$timedOut = $false
try {
    $proc | Wait-Process -Timeout $RunSeconds -ErrorAction SilentlyContinue
} catch { }
if (-not $proc.HasExited) {
    $timedOut = $true
    try { & taskkill.exe /PID $proc.Id /T /F | Out-Null } catch { }
}
Start-Sleep -Seconds 3
$remnants = @(Get-Process -Name DeadCellsModding, DeadCells, deadcells_gl -ErrorAction SilentlyContinue)
if ($remnants.Count -gt 0) {
    foreach ($p in $remnants) { try { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue } catch { } }
}
$exitCode = if ($proc.HasExited) { $proc.ExitCode } else { $null }
$endUtc = [DateTime]::UtcNow

$postSave = Get-TreeHash $prodSave
$postConfig = Get-TreeHash $prodConfig
$postMods = Get-DirSignature $prodMods
$postPlugins = Get-DirSignature $prodPlugins
$prodDelta = @()
if ($preSave -ne $postSave) { $prodDelta += 'save' }
if ($preConfig -ne $postConfig) { $prodDelta += 'config' }
if ($preMods -ne $postMods) { $prodDelta += 'mods' }
if ($prePlugins -ne $postPlugins) { $prodDelta += 'plugins' }

# Collect fixture-local evidence. Production trees are only read.
$fixtureLogs = Join-Path $OutputDir 'logs'
New-Item -ItemType Directory -Path $fixtureLogs -Force | Out-Null
foreach ($rel in @('dccm\logs', 'coremod\logs', 'save', 'GameRoot\save', 'dccm\mods\DeveloperConsole')) {
    $src = Join-Path $R $rel
    if (-not (Test-Path -LiteralPath $src)) { continue }
    $dest = Join-Path $fixtureLogs ($rel -replace '[\\/:*?"<>|]', '_')
    if (Test-Path -LiteralPath $src -PathType Container) {
        Copy-Item -LiteralPath $src -Destination $dest -Recurse -Force -ErrorAction SilentlyContinue
    } else {
        Copy-Item -LiteralPath $src -Destination $dest -Force -ErrorAction SilentlyContinue
    }
}

$manifest = Get-Content -LiteralPath (Join-Path $R 'fixture-manifest.json') -Raw | ConvertFrom-Json
$modHash = Sha256 (Join-Path $R 'dccm\mods\DeveloperConsole\DeveloperConsole.dll')
. (Join-Path $PSScriptRoot 'Runtime-Outcome.ps1')
$result = Get-RuntimeOutcome $timedOut $remnants.Count $prodDelta.Count $exitCode

$evidence = [ordered]@{
    schema = 'dc-m2-runtime-evidence/1'
    session = $SessionName
    authorizedBy = $AuthorizedBy
    fixtureRoot = $R
    outputDir = $OutputDir
    startUtc = $startUtc.ToString('o')
    endUtc = $endUtc.ToString('o')
    durationMs = [int]($endUtc - $startUtc).TotalMilliseconds
    requestedWindowSec = $RunSeconds
    timedOut = $timedOut
    exitCode = $exitCode
    remnants = @($remnants | ForEach-Object { $_.Name })
    result = $result
    game = $manifest.game
    dccm = $manifest.dccm
    mod = $manifest.mod
    modSha256 = $modHash
    authorizationReceipt = $receipt
    productionDelta = $prodDelta
    productionPre = [ordered]@{ save = $preSave; config = $preConfig; mods = $preMods; plugins = $prePlugins }
    productionPost = [ordered]@{ save = $postSave; config = $postConfig; mods = $postMods; plugins = $postPlugins }
    logs = @(Get-ChildItem -LiteralPath $fixtureLogs -Recurse -File -ErrorAction SilentlyContinue |
        ForEach-Object { $_.FullName.Substring($OutputDir.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar) })
    note = 'Production trees are read-only evidence. The fixture save root is the only writable save root.'
}
$evidence | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $OutputDir 'evidence.json') -Encoding UTF8
Write-Output ("RESULT " + $result)
Write-Output ("Evidence: " + (Join-Path $OutputDir 'evidence.json'))
if ($result -eq 'EXITED_CLEANLY_UNVERIFIED') { exit 0 }
exit 1

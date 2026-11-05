param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureRoot,
    [string]$ExpectedBuildDir = '',
    [string]$ManifestPath = '',
    [string]$ReceiptPath = '',
    [ValidateSet('VerifyOnly', 'Run')]
    [string]$Mode = 'VerifyOnly',
    [switch]$AllowSaves,
    [switch]$SelfTest
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
function Add-Failure([string]$message) {
    $script:Failures += $message
    Write-Output ("FAIL " + $message)
}
function Add-Pass([string]$message) {
    Write-Output ("PASS " + $message)
}

$Failures = @()

if ($SelfTest) {
    # Offline self-test of the two path guards and the synthetic-receipt rule.
    $root = Join-Path $env:TEMP ("dc-m2-admission-selftest-" + [guid]::NewGuid().ToString('N'))
    try {
        New-Item -ItemType Directory -Path $root -Force | Out-Null
        if (-not (Test-Inside (Join-Path $root 'child') $root)) { Add-Failure 'selftest-child-not-inside' }
        if (Test-Inside $root (Join-Path $root 'child')) { Add-Failure 'selftest-parent-inside-child' }
        $synthetic = Join-Path $root 'receipt.json'
        '{"schema":"dc-m2-authorization/1","synthetic":true}' | Set-Content -LiteralPath $synthetic -Encoding UTF8
        $obj = Get-Content -LiteralPath $synthetic -Raw | ConvertFrom-Json
        if (-not $obj.synthetic) { Add-Failure 'selftest-synthetic-not-detected' }
    } finally {
        if (Test-Path -LiteralPath $root) { Remove-Item -LiteralPath $root -Recurse -Force }
    }
    if ($Failures.Count -eq 0) { Write-Output 'PASS admission-self-test'; exit 0 }
    Write-Output ("FAIL admission-self-test: " + ($Failures -join '; '))
    exit 1
}

$R = Normalize $FixtureRoot
if (-not (Test-Path -LiteralPath $R -PathType Container)) { throw "fixture-missing: $R" }
$protected = @(
    (Join-Path ${env:ProgramFiles(x86)} 'Steam\steamapps\common\Dead Cells'),
    [System.IO.Path]::Combine($env:ProgramFilesX86, 'Steam')
)
foreach ($p in $protected) {
    if ((Test-Inside $R $p) -or (Test-Inside $p $R)) { Add-Failure "protected-overlap: $p" }
}
if (Test-Path -LiteralPath (Join-Path $R '.git') -PathType Container) { Add-Failure 'fixture-is-a-git-repo' }

$manifestFile = if ($ManifestPath) { $ManifestPath } else { Join-Path $R 'fixture-manifest.json' }
if (-not (Test-Path -LiteralPath $manifestFile -PathType Leaf)) {
    Add-Failure "manifest-missing: $manifestFile"
    $manifest = $null
} else {
    try { $manifest = Get-Content -LiteralPath $manifestFile -Raw | ConvertFrom-Json }
    catch { Add-Failure "manifest-unparseable: $manifestFile"; $manifest = $null }
}
if ($manifest -and $manifest.schema -ne 'dc-m2-fixture/1') { Add-Failure "manifest-schema: $($manifest.schema)" }

$exe = Join-Path $R 'coremod\core\host\startup\DeadCellsModding.exe'
if (-not (Test-Path -LiteralPath $exe -PathType Leaf)) { Add-Failure 'framework-entry-absent' } else { Add-Pass 'framework-entry-present' }

$versionFile = Join-Path $R 'coremod\ModCoreVersion.txt'
if (-not (Test-Path -LiteralPath $versionFile -PathType Leaf)) {
    Add-Failure 'dccm-version-file-missing'
} else {
    $dccm = (Get-Content -LiteralPath $versionFile -Raw).Trim()
    if ($dccm -ne '35.13.8') { Add-Failure "dccm-version: $dccm" } else { Add-Pass 'dccm-version=35.13.8' }
    if ($manifest -and $manifest.dccm -ne $dccm) { Add-Failure 'manifest-dccm-mismatch' }
}

$pkg = Join-Path $R 'dccm\mods\DeveloperConsole'
$pkgDll = Join-Path $pkg 'DeveloperConsole.dll'
$pkgInfo = Join-Path $pkg 'modinfo.json'
foreach ($f in @($pkgDll, $pkgInfo)) {
    if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { Add-Failure "package-file-missing: $f" }
}
if ((Test-Path -LiteralPath $pkgDll) -and (Test-Path -LiteralPath $pkgInfo)) {
    $extraDlls = Get-ChildItem -LiteralPath $pkg -Filter *.dll |
        Where-Object { $_.Name -ne 'DeveloperConsole.dll' }
    if ($extraDlls) { Add-Failure "package-extra-dlls: $($extraDlls.Name -join ', ')" }
    else { Add-Pass 'package-file-set' }
    if ($manifest) {
        foreach ($entry in @($manifest.package)) {
            $target = Join-Path $pkg $entry.file
            if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { Add-Failure "manifest-file-missing: $($entry.file)" }
            elseif ((Sha256 $target) -ne $entry.sha256) { Add-Failure "manifest-hash-mismatch: $($entry.file)" }
            else { Add-Pass "package-hash: $($entry.file)" }
        }
    }
}

if ($ExpectedBuildDir) {
    $expected = Normalize $ExpectedBuildDir
    $expectedDll = Join-Path $expected 'DeveloperConsole.dll'
    if (-not (Test-Path -LiteralPath $expectedDll -PathType Leaf)) {
        Add-Failure "expected-build-missing: $expectedDll"
    } else {
        if ((Sha256 $expectedDll) -ne (Sha256 $pkgDll)) { Add-Failure 'package-dll-does-not-match-build' }
        else { Add-Pass 'package-dll-matches-build' }
    }
}

# The fixture must not carry production or private mod content.
$coreMods = Join-Path $R 'coremod\mods'
if ((Get-ChildItem -LiteralPath $coreMods -Force -ErrorAction SilentlyContinue | Measure-Object).Count -ne 0) {
    Add-Failure 'coremod-mods-not-empty'
} else { Add-Pass 'coremod-mods-empty' }

$overrideMods = Get-ChildItem -LiteralPath (Join-Path $R 'dccm\mods') -Force -Directory -ErrorAction SilentlyContinue
$overrideMods = @($overrideMods | Where-Object { $_.Name -ne 'DeveloperConsole' })
if ($overrideMods.Count -ne 0) { Add-Failure "override-extra-mods: $($overrideMods.Name -join ', ')" }
else { Add-Pass 'override-mods-only-developer-console' }

$badPlugins = @(Get-ChildItem -LiteralPath (Join-Path $R 'coremod\plugins') -Force -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne 'ModCore.ModLoader.Default' })
if ($badPlugins.Count -ne 0) { Add-Failure "coremod-extra-plugins: $($badPlugins.Name -join ', ')" }
else { Add-Pass 'coremod-plugins-stock-loader-only' }

$badOverridePlugins = @(Get-ChildItem -LiteralPath (Join-Path $R 'dccm\plugins') -Force -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne 'ModCore.ModLoader.Default' })
if ($badOverridePlugins.Count -ne 0) { Add-Failure "override-extra-plugins: $($badOverridePlugins.Name -join ', ')" }
else { Add-Pass 'override-plugins-stock-loader-only' }

$dataDir = Join-Path $R 'coremod\data'
if ((Get-ChildItem -LiteralPath $dataDir -Force -ErrorAction SilentlyContinue | Measure-Object).Count -ne 0) {
    Add-Failure 'coremod-data-not-empty'
} else { Add-Pass 'coremod-data-empty' }

$cfgDir = Join-Path $R 'coremod\config'
if ((Get-ChildItem -LiteralPath $cfgDir -Force -ErrorAction SilentlyContinue | Measure-Object).Count -ne 0) {
    Add-Failure 'coremod-config-not-empty'
} else { Add-Pass 'coremod-config-empty' }

foreach ($stale in @(
    'last_load_extra_mods_paths',
    'last_load_extra_mods_paths.cache',
    'last_load_extra_plugins_paths',
    'last_load_extra_plugins_paths.cache'
)) {
    if (Test-Path -LiteralPath (Join-Path $R ('coremod\cache\' + $stale))) {
        Add-Failure "stale-cache-present: $stale"
    }
}

$privateTerms = @()
$denyPath = $env:DC_PRIVATE_DENYLIST
if (-not [string]::IsNullOrWhiteSpace($denyPath) -and (Test-Path -LiteralPath $denyPath -PathType Leaf)) {
    $privateTerms = @(Get-Content -LiteralPath $denyPath |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_.Length -gt 0 })
}
if ($privateTerms.Count -eq 0) {
    Write-Output 'SKIP private-content-name scan (DC_PRIVATE_DENYLIST not configured).'
} else {
    $scanRoots = @('coremod\mods', 'coremod\plugins', 'coremod\data', 'coremod\config', 'dccm')
    $privateHits = @()
    foreach ($rel in $scanRoots) {
        $root = Join-Path $R $rel
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $privateHits += @(Get-ChildItem -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue |
            Where-Object { $name = $_.Name; ($privateTerms | Where-Object { $name -match [regex]::Escape($_) }).Count -gt 0 })
    }
    if ($privateHits.Count -ne 0) {
        Add-Failure "private-content-names: $((@($privateHits | Select-Object -First 8 | ForEach-Object { $_.Name }) -join ', '))"
    } else { Add-Pass 'no-private-content-names' }
}

foreach ($saveRel in @('save', 'GameRoot\save')) {
    $saveDir = Join-Path $R $saveRel
    if (-not (Test-Path -LiteralPath $saveDir)) { Add-Failure "save-root-missing: $saveRel"; continue }
    if (-not $AllowSaves -and (Get-ChildItem -LiteralPath $saveDir -Force -ErrorAction SilentlyContinue | Measure-Object).Count -ne 0) {
        Add-Failure "save-root-not-empty: $saveRel"
    }
}
if ($AllowSaves) { Add-Pass 'save-roots-allowed' } else { Add-Pass 'save-roots-empty' }

foreach ($workshop in @('workshop', 'dccm\workshop')) {
    if (Test-Path -LiteralPath (Join-Path $R $workshop)) { Add-Failure "workshop-present: $workshop" }
}

if ($Mode -eq 'Run') {
    if (-not $ReceiptPath -or -not (Test-Path -LiteralPath $ReceiptPath -PathType Leaf)) {
        Add-Failure 'authorization-receipt-missing'
    } else {
        try { $receipt = Get-Content -LiteralPath $ReceiptPath -Raw | ConvertFrom-Json }
        catch { Add-Failure 'authorization-receipt-unparseable'; $receipt = $null }
        if ($receipt) {
            if ($receipt.schema -ne 'dc-m2-authorization/1') { Add-Failure "authorization-schema: $($receipt.schema)" }
            if ($receipt.synthetic -eq $true) { Add-Failure 'authorization-is-synthetic-template' }
            if ((Normalize ([string]$receipt.fixtureRoot)) -ne $R) { Add-Failure 'authorization-fixture-mismatch' }
            if (-not $receipt.authorizedBy -or $receipt.authorizedBy -eq 'USER-EXPLICIT-AUTHORIZATION') {
                Add-Failure 'authorization-not-issued'
            }
            if (-not $receipt.session) { Add-Failure 'authorization-session-missing' }
            if ($receipt.modSha256 -ne (Sha256 $pkgDll)) { Add-Failure 'authorization-mod-hash-mismatch' }
            if ([int]$receipt.runSeconds -lt 30 -or [int]$receipt.runSeconds -gt 300) {
                Add-Failure 'authorization-window-out-of-bounds'
            }
            try {
                $issued = [DateTime]::Parse([string]$receipt.issuedUtc).ToUniversalTime()
                $age = ([DateTime]::UtcNow - $issued).TotalHours
                if ($age -lt -0.25 -or $age -gt [double]$receipt.validHours) {
                    Add-Failure 'authorization-expired-or-not-yet-valid'
                }
            } catch { Add-Failure 'authorization-timestamp-invalid' }
            $a = $receipt.attestations
            if (-not $a -or $a.noPrivateContent -ne $true -or $a.disposableOnly -ne $true -or $a.productionSaveBackedUp -ne $true) {
                Add-Failure 'authorization-attestations-incomplete'
            }
        }
    }
}

Write-Output ("--- " + $Failures.Count + " failure(s) ---")
if ($Failures.Count -eq 0) { exit 0 }
exit 1

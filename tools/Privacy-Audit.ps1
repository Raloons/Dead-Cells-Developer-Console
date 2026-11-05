# Public-release privacy audit.
# Scans tracked text content for private project names, private content IDs,
# machine-specific paths, and credential markers. Exits non-zero on a hit.
#
# The audit report and this scanner are excluded from their own scan so the
# denylist itself is not reported as a leak.

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot

Push-Location $repo
try {
    if (Test-Path -LiteralPath (Join-Path $repo ".git")) {
        $tracked = & git ls-files --cached --others --exclude-standard | Sort-Object -Unique
    } else {
        $tracked = Get-ChildItem -LiteralPath $repo -Recurse -File -Force |
            ForEach-Object { $_.FullName.Substring($repo.Length + 1) }
    }
} finally {
    Pop-Location
}

$excluded = @(
    "docs/PRIVACY_AUDIT.md",
    "tools/Privacy-Audit.ps1",
    "tools/Build-Release.ps1",
    "fixture/Test-FixtureAdmission.ps1",
    "fixture/staged/"
)
$files = $tracked | Where-Object {
    $path = $_ -replace '\\', '/'
    ($path -notmatch '(^|/)(bin|obj|\.git)/') -and
    ($excluded | Where-Object { $path.StartsWith($_) }).Count -eq 0 -and
    $path -match '\.(cs|csproj|md|json|ps1|txt|gitignore|yml|yaml|svg|html)$'
}

$terms = @(
    "C:\Users\",
    "D:\",
    "BEGIN RSA PRIVATE KEY",
    "BEGIN OPENSSH PRIVATE KEY",
    "client_secret",
    "api_key",
    "apikey",
    "password=",
    "authorization:"
)

# Private-content names are intentionally not stored in the public source.
# An authorised local audit can supply them through a file outside the tree.
$denyPath = $env:DC_PRIVATE_DENYLIST
if (-not [string]::IsNullOrWhiteSpace($denyPath) -and (Test-Path -LiteralPath $denyPath -PathType Leaf)) {
    $terms += @(Get-Content -LiteralPath $denyPath |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_.Length -gt 0 })
}

$hits = @()
foreach ($file in $files) {
    $full = Join-Path $repo $file
    if (-not (Test-Path -LiteralPath $full -PathType Leaf)) { continue }
    $text = Get-Content -LiteralPath $full -Raw -ErrorAction SilentlyContinue
    if ($null -eq $text) { continue }
    foreach ($term in $terms) {
        # A drive prefix must start a path token. Otherwise a C# message such
        # as "native ID:\n" falsely matches the drive term at its final D.
        $found = if ($term -match '^[A-Za-z]:\\') {
            [regex]::IsMatch($text, '(?<![A-Za-z0-9_])' + [regex]::Escape($term), [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
        } else { $text.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0 }
        if ($found) {
            $hits += [pscustomobject]@{ File = $file; Term = $term }
        }
    }
    # Private milestone identifiers (M + 2-4 digits) are not expected here.
    foreach ($m in [regex]::Matches($text, '\bM\d{2,4}\b')) {
        $hits += [pscustomobject]@{ File = $file; Term = $m.Value }
    }
}

Write-Host "Scanned files: $($files.Count)"
if ($hits.Count -eq 0) {
    Write-Host "PASS: no prohibited terms or private identifiers found."
    exit 0
}

Write-Host "FAIL: $($hits.Count) hit(s)"
$hits | Sort-Object File, Term | Format-Table -AutoSize
exit 1

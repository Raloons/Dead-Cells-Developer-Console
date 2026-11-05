$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$stage = Join-Path $PSScriptRoot 'Stage-Fixture.ps1'
$failures = 0
function Check([string]$name, [bool]$cond, [string]$detail = "") {
    Write-Host ("[{0}] {1} {2}" -f $(if ($cond) { "PASS" } else { "FAIL" }), $name, $detail)
    if (-not $cond) { $script:failures++ }
}
function Run-Stage([string]$root, [switch]$Force) {
    $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$stage,'-DeadCellsRoot',$root)
    if ($Force) { $args += '-Force' }
    & powershell @args | Out-Null
    return $LASTEXITCODE
}
function New-FakeTree {
    $root = Join-Path $env:TEMP ("dc-installer-test-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path (Join-Path $root 'coremod\mods') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $root 'DeadCellsModding.exe') -Value 'dummy' -Encoding ASCII
    Set-Content -LiteralPath (Join-Path $root 'coremod\mods\keep-me.txt') -Value 'keep' -Encoding ASCII
    return $root
}

$root = New-FakeTree
try {
    Check 'install-first' ((Run-Stage -root $root -Force) -eq 0)
    $mod = Join-Path $root 'coremod\mods\DeveloperConsole'
    Check 'install-files' ((Test-Path (Join-Path $mod 'DeveloperConsole.dll')) -and
        (Test-Path (Join-Path $mod 'modinfo.json')) -and
        (Test-Path (Join-Path $mod 'staged-manifest.json')))
    Check 'install-no-extra-dlls' ((@(Get-ChildItem -LiteralPath $mod -Filter *.dll).Count -eq 1))
    Check 'install-preserves-sibling' (Test-Path (Join-Path $root 'coremod\mods\keep-me.txt'))
    $before = (Get-FileHash -LiteralPath (Join-Path $mod 'DeveloperConsole.dll') -Algorithm SHA256).Hash
    Check 'install-refuses-existing' ((Run-Stage -root $root) -ne 0)
    Check 'install-existing-unchanged' ((Get-FileHash -LiteralPath (Join-Path $mod 'DeveloperConsole.dll') -Algorithm SHA256).Hash -eq $before)
    Check 'install-force-replaces' ((Run-Stage -root $root -Force) -eq 0)
    Remove-Item -LiteralPath $mod -Recurse -Force
    Check 'uninstall-removes-mod' (-not (Test-Path -LiteralPath $mod))
    Check 'uninstall-preserves-sibling' (Test-Path (Join-Path $root 'coremod\mods\keep-me.txt'))
    Check 'uninstall-empty-mods' ((@(Get-ChildItem -LiteralPath (Join-Path $root 'coremod\mods')).Count -eq 1))
} finally {
    Remove-Item -LiteralPath $root -Recurse -Force -ErrorAction SilentlyContinue
}

$bad = Join-Path $env:TEMP ("dc-installer-bad-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $bad -Force | Out-Null
try {
    Check 'install-missing-mods-dir' ((Run-Stage -root $bad) -ne 0)
} finally {
    Remove-Item -LiteralPath $bad -Recurse -Force -ErrorAction SilentlyContinue
}

$bad2 = New-FakeTree
try {
    Remove-Item -LiteralPath (Join-Path $bad2 'DeadCellsModding.exe') -Force
    Check 'install-missing-launcher' ((Run-Stage -root $bad2) -ne 0)
} finally {
    Remove-Item -LiteralPath $bad2 -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host ("--- {0} failure(s) ---" -f $failures)
exit $(if ($failures -eq 0) { 0 } else { 1 })

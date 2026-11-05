param(
    [Parameter(Mandatory = $true)]
    [string]$FixtureRoot
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$session = Join-Path $PSScriptRoot 'Invoke-RuntimeSession.ps1'
$template = Join-Path $PSScriptRoot 'authorization.template.json'
$failures = 0

function Check([string]$name, [bool]$cond, [string]$detail = "") {
    Write-Host ("[{0}] {1} {2}" -f $(if ($cond) { "PASS" } else { "FAIL" }), $name, $detail)
    if (-not $cond) { $script:failures++ }
}
function Game-Running {
    return @(Get-Process -Name DeadCellsModding, deadcells, deadcells_gl -ErrorAction SilentlyContinue).Count -gt 0
}
function Run-Session([string]$receipt, [switch]$Authorize) {
    $args = @('-NoProfile','-ExecutionPolicy','Bypass','-File',$session,
        '-FixtureRoot',$FixtureRoot,'-ReceiptPath',$receipt,
        '-AuthorizedBy','SELF-TEST','-SessionName','SELF-TEST','-RunSeconds','60')
    if ($Authorize) { $args += '-Authorize' }
    & powershell @args 2>&1 | Out-Null
    return $LASTEXITCODE
}

Check 'preflight-no-game' (-not (Game-Running))
Check 'missing-authorize-refused' ((Run-Session -receipt $template) -eq 2)
Check 'missing-authorize-no-game' (-not (Game-Running))

Check 'synthetic-receipt-refused' ((Run-Session -receipt $template -Authorize) -eq 3)
Check 'synthetic-receipt-no-game' (-not (Game-Running))

$malformed = Join-Path $env:TEMP ("dc-bad-receipt-" + [guid]::NewGuid().ToString('N') + '.json')
'{ not-json' | Set-Content -LiteralPath $malformed -Encoding ASCII
try {
    Check 'malformed-receipt-refused' ((Run-Session -receipt $malformed -Authorize) -eq 3)
    Check 'malformed-receipt-no-game' (-not (Game-Running))
} finally {
    Remove-Item -LiteralPath $malformed -Force -ErrorAction SilentlyContinue
}

Write-Host ("--- {0} failure(s) ---" -f $failures)
exit $(if ($failures -eq 0) { 0 } else { 1 })

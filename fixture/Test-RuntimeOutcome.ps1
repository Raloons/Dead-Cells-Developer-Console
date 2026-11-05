$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'Runtime-Outcome.ps1')
$cases = @(
    @($false, 0, 0, 0, 'EXITED_CLEANLY_UNVERIFIED'),
    @($false, 0, 0, 1, 'FAIL_PROCESS_EXIT'),
    @($false, 0, 0, -1073741819, 'FAIL_PROCESS_EXIT'),
    @($false, 0, 0, $null, 'INCOMPLETE_EXIT_UNKNOWN'),
    @($true, 0, 0, 1, 'INCOMPLETE_TIMEOUT'),
    @($true, 1, 0, 1, 'INCOMPLETE_REMNANTS'),
    @($true, 1, 1, 1, 'FAIL_PRODUCTION_DELTA')
)
$failures = 0
foreach ($c in $cases) {
    $actual = Get-RuntimeOutcome $c[0] $c[1] $c[2] $c[3]
    if ($actual -ne $c[4]) { $failures++; Write-Host "FAIL expected $($c[4]), got $actual" }
    else { Write-Host "PASS $actual" }
}
Write-Host "$($cases.Count) offline runner outcome checks, $failures failures; no process launched."
exit $failures

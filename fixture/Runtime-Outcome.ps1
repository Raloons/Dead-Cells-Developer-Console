# Pure result classification. Never invokes a process or grants authorization.
function Get-RuntimeOutcome([bool]$TimedOut, [int]$RemnantCount, [int]$ProductionDeltaCount, $ExitCode) {
    if ($ProductionDeltaCount -gt 0) { return 'FAIL_PRODUCTION_DELTA' }
    if ($RemnantCount -gt 0) { return 'INCOMPLETE_REMNANTS' }
    if ($TimedOut) { return 'INCOMPLETE_TIMEOUT' }
    if ($null -eq $ExitCode) { return 'INCOMPLETE_EXIT_UNKNOWN' }
    if ($ExitCode -ne 0) { return 'FAIL_PROCESS_EXIT' }
    return 'EXITED_CLEANLY_UNVERIFIED'
}

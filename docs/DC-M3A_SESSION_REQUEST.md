# DC-M3A live session request

**Status: PENDING DIRECTOR AUTHORIZATION.** This is a request, not a valid
authorization receipt. No game process has been started.

## Scope requested

- One bounded session in `<fixture root>/DC-M2-R4`.
- Dead Cells v35 / DCCM 35.13.8.
- Console DLL SHA-256
  `4733254175C0E49786F88ED1D4F496F7E68937826EA48750BD6B72BB7FF21877`.
- Session name: `DC-M3A-LIVE-1`.
- Window: 300 seconds.
- Disposable fixture only; production saves and Steam Cloud are out of scope.

## Exact runner command

```text
powershell -ExecutionPolicy Bypass -File fixture/Invoke-RuntimeSession.ps1 `
  -FixtureRoot "<fixture root>" `
  -ReceiptPath "<real receipt.json>" `
  -AuthorizedBy "<explicit director authorization>" `
  -SessionName "DC-M3A-LIVE-1" `
  -RunSeconds 300 `
  -ExpectedBuildDir "src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole" `
  -Authorize
```

A local request with the exact fixture path and current hash is prepared under
`dist/DC-M3A/` but is not tracked and is not a valid receipt. It is synthetic
and will be refused until the director issues a real receipt with a non-synthetic
flag, a real `authorizedBy` value, and a current issue time.

## Required authorization content

- `schema: dc-m2-authorization/1`
- `synthetic: false`
- exact fixture root `<fixture root>/DC-M2-R4`
- current `DeveloperConsole.dll` SHA-256
- `issuedUtc` within the validity window
- `validHours` and `runSeconds` (300 seconds requested)
- `session: DC-M3A-LIVE-1`
- non-placeholder `authorizedBy`
- attestations: no private content, disposable only, production saves backed up

## Tests requested

Execute the full R01–R20 matrix in `docs/RUNTIME_VERIFICATION.md`, plus H01 and
the scroll controls A–F in `fixture/Runtime-Checklist.md`.

The runner refuses without `-Authorize`, refuses a synthetic receipt, refuses a
malformed receipt, and checks production mutable paths before and after a
session. See `docs/RUNTIME_VERIFICATION.md` for the evidence procedure.

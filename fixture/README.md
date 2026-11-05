# Disposable runtime fixture

This folder prepares and validates a standalone DCCM game tree for the DC-M2
runtime matrix. No game process is started by any script unless the explicit
authorization gate is satisfied.

## Current local fixture

A clean standalone fixture was prepared at a local disposable path outside the
repository:

```text
<fixture root>/DC-M2-R4
```

The prior `<fixture root>/DC-M2-R1`, R2, and R3 are retained as sealed
fixtures. R4 is the final DC-M2.2 fixture for the current binary.

It contains:

- the stock Dead Cells v35 game tree;
- DCCM 35.13.8;
- only the `DeveloperConsole` mod under `dccm/mods/DeveloperConsole`;
- the stock default loader;
- an empty fixture-local save root;
- no production mods, plugins, data, config, logs, Workshop content, or private
  expansion content.

`<fixture root>/DC-M2-R4/fixture-manifest.json` records the exact package
hashes and versions.

## Build a fixture

From the repository root:

```text
powershell -ExecutionPolicy Bypass -File fixture/New-DisposableFixture.ps1 `
  -SourceGameRoot "<Dead Cells install>" `
  -FixtureRoot "<new fixture root>" `
  -ModBuildDir "src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole"
```

The builder copies the game tree with `save`, `logs`, and `dccm` excluded,
prunes production mods/plugins/data/config, installs the console package, pins
the stock loader and hlboot cache, empties the fixture saves, and writes
`fixture-manifest.json`. It never launches the game.

To re-apply the pruning/staging step to an existing copy, add `-SkipCopy`.

## Validate a fixture

```text
powershell -ExecutionPolicy Bypass -File fixture/Test-FixtureAdmission.ps1 `
  -FixtureRoot "<fixture root>" `
  -ExpectedBuildDir "src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole" `
  -Mode VerifyOnly
```

It checks the package hashes, expected build hash, DCCM version, mod/plugin
sets, empty production data/config/save roots, absence of private-content
names, and the presence of the framework entry point. It never launches.

`-SelfTest` exercises the path guards and the synthetic-receipt rule.

## Authorization gate

The session launcher requires a real authorization receipt and the
`-Authorize` switch:

```text
powershell -ExecutionPolicy Bypass -File fixture/Invoke-RuntimeSession.ps1 `
  -FixtureRoot "<fixture root>" `
  -ReceiptPath "<real receipt.json>" `
  -AuthorizedBy "<explicit user authorization>" `
  -SessionName "DC-M2-LIVE-1" `
  -RunSeconds 120 `
  -ExpectedBuildDir "src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole" `
  -Authorize
```

`fixture/authorization.template.json` is a **synthetic template** and is
refused by the admission validator. A real receipt must be issued from the
user's explicit authorization, with the exact fixture root, current
`DeveloperConsole.dll` SHA-256, issue time, bounded window, session name, and
the three attestations.

Without a valid receipt and `-Authorize`, the launcher prints
`BLOCKED: explicit -Authorize switch is required` and starts nothing.

The launcher:

- refuses if Steam or a game process is already running;
- takes a preflight hash/signature of production saves and mutable coremod
  directories;
- launches exactly once with fixture-local override roots;
- stops the process tree at the bounded timeout;
- checks for remnants;
- re-checks production hashes/signatures and fails on any delta;
- copies fixture logs and saves into an evidence directory;
- writes `evidence.json` with the result, versions, hashes, and production
  deltas.

It never restores or writes production files.

## Offline tooling tests

```text
powershell -ExecutionPolicy Bypass -File fixture/Test-Installer.ps1
powershell -ExecutionPolicy Bypass -File fixture/Test-AuthorizationGate.ps1 -FixtureRoot "<fixture root>"
```

`Test-Installer.ps1` builds fake disposable game trees and checks install,
refusal of an existing target, forced replacement, uninstall, sibling
preservation, missing launcher, and missing mods directory.

`Test-AuthorizationGate.ps1` checks that a missing `-Authorize` switch, a
synthetic receipt, and a malformed receipt are all refused without starting a
game process.

## Authorized session checklist

`fixture/Runtime-Checklist.md` contains the exact command sequence and
observation checklist for R01-R20 after a real receipt and `-Authorize` are
supplied.

## Status

The fixture is prepared and offline-validated. The 20-point runtime matrix has
not been executed because a live session requires explicit user authorization.
See `docs/RUNTIME_VERIFICATION.md` for the matrix and status categories.

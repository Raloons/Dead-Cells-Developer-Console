# DC-M2 - engineering report

**Project:** Dead Cells Developer Console
**Version:** 0.1.0-alpha
**Milestone:** DC-M2 - independent runtime verification and public alpha preparation
**Status:** offline qualification complete; **native boot and live gameplay
verification are blocked by authorization**. No game process was launched in
this milestone.

---

## 1. What was done

- Re-read the DC-M1 project, documentation, source, tests, Git history, and
  staging scripts.
- Preserved the DC-M1 history and working tree.
- Located the existing disposable-game infrastructure; confirmed the old
  fixtures are private-content fixtures and were not reused.
- Created a new standalone fixture at `<fixture root>/DC-M2-R1`:
  - Dead Cells v35;
  - DCCM 35.13.8;
  - only `DeveloperConsole` under `dccm/mods/DeveloperConsole`;
  - stock default loader;
  - empty fixture-local save roots;
  - no production mods, plugins, data, config, logs, Workshop content, or
    private expansion content.
- Added a reproducible fixture builder, admission validator, authorization
  template, and authorization-gated session launcher.
- Added a static native API audit and expanded the authoritative 20-check
  runtime matrix with per-check fields and status categories.
- Built and verified the public release package and checksum.
- Reran the privacy audit on tracked content and the release package.

## 2. Fixture

`fixture/New-DisposableFixture.ps1` copies and prunes a game tree, installs
the console package, pins the stock loader and hlboot cache, empties the
fixture save roots, and writes `fixture-manifest.json`.

`fixture/Test-FixtureAdmission.ps1` verifies:

- framework entry point present;
- DCCM version 35.13.8;
- package hashes match the manifest;
- package DLL matches the current Release build;
- `coremod/mods` empty;
- override mods contain only `DeveloperConsole`;
- plugins contain only the stock loader;
- `coremod/data` and `coremod/config` empty;
- no stale extra-mod path cache;
- no private-content names in mod/plugin/data/config/override roots;
- save roots empty;
- authorization receipt valid in `Run` mode.

The real fixture passed `VerifyOnly` with **0 failures**. The admission
self-test passed. A synthetic template was refused in `Run` mode with the
expected failures and no process started.

## 3. Authorization gate

`fixture/Invoke-RuntimeSession.ps1` requires:

- an explicit `-Authorize` switch;
- a real `dc-m2-authorization/1` receipt;
- exact fixture root;
- current `DeveloperConsole.dll` SHA-256;
- issue time and bounded window;
- the three attestations.

Without the switch it prints `BLOCKED: explicit -Authorize switch is required`
and starts nothing. The synthetic template was exercised and refused. No live
session was launched.

## 4. Offline verification

| Gate | Result |
|---|---|
| Release build | 0 warnings, 0 errors |
| Debug build | 0 warnings, 0 errors |
| Offline acceptance suite | 118 checks, 0 failures |
| Assembly references | System.*, GameProxy, ModCore, ModCore.Common, HaxeProxy, HashlinkSharp, Serilog only |
| Private assembly reference | none |
| Fixture admission | 0 failures |
| Fixture private-content name scan | PASS |
| Admission self-test | PASS |
| Synthetic authorization refusal | PASS, no process |
| Privacy audit | PASS, 42 tracked text files |
| Release package entry audit | PASS |

### Native API audit

`docs/NATIVE_API_AUDIT.md` records the installed proxy signatures for the
console hooks, hero/stat methods, item factory/forge, item drop constructor,
boot update, assets/constants, runtime item-name helper, and mod-loader
reflection. Residual risks that only a live session can settle are listed
there.

## 5. Scroll-persistence hardening

A release-hardening issue was found in the legacy console: console-granted
scrolls were applied as a temporary `StatBonus` override and reverted after
save/resume. Decompiled v35 bytecode established the native authority:

- `Hero.computeTiers` counts `inventory.count*Up(null)`.
- Those counters use `countItemKind("BrutalityUp"/"TacticUp"/"SurvivalUp")`.
- Native scroll pickup creates scroll `BagItem`s and adds them to
  `inventory.items`.
- `Inventory.serialize`/`unserialize` persists `items`.

`player scroll` now creates/removes those native items and calls
`onEquipedItemsChange` with HUD refresh. A temporary `player stat` override
remains explicitly documented as session-only. The fix adds 4 regression tests
for native counts, simulated resume, no duplication, and temporary-override
loss. Details: `docs/SCROLL_PERSISTENCE_INVESTIGATION.md`.

Live save/resume confirmation remains blocked by authorization.

## 6. Runtime matrix

`docs/RUNTIME_VERIFICATION.md` contains the full 20-check matrix. Every check
is marked **Blocked by authorization** because no game process was launched.
The document preserves the original 20 checks and adds objective,
prerequisites, method, expected result, observed result, status, evidence, and
uncertainty for each.

No check was marked passed without an execution observation.

## 7. Release artifact

Built with:

```text
powershell -ExecutionPolicy Bypass -File tools/Build-Release.ps1
```

Prepared locally at `dist/DeveloperConsole-0.1.0-alpha.zip`.

- Package SHA-256: `450F01001F96FC0E53AC30C7FFD02442BF5F94F7E63984E931A0241E9E0354D7`
- Mod DLL SHA-256: `F5EDE6C2321AA6CDACDEBED993C7F67260899927C17339472DD7FDC18FC746C7`
- Contents: `DeveloperConsole/DeveloperConsole.dll`, `DeveloperConsole/modinfo.json`,
  `README.md`, `LICENSE`, `RELEASE_NOTES.md`, `SHA256SUMS.txt`, and public docs.
- No game executable, game database, framework distribution, save, fixture, or
  private content is included.

The package can be rebuilt from the public source tree. The local fixture
package matches the same DLL hash.

## 8. Git

- Local repository only.
- No remote configured.
- No push performed.
- DC-M1 history preserved.
- DC-M2 changes committed locally.

## 9. Remaining blocker

The only remaining blocker to runtime-complete status is an explicitly
authorized live session in the prepared fixture. The launcher and admission
gate are ready. After authorization is granted:

1. Issue a real `dc-m2-authorization/1` receipt for the fixture and current DLL.
2. Run `fixture/Invoke-RuntimeSession.ps1` with `-Authorize`.

3. Execute R01-R20 and update the matrix with observed evidence.
4. Fix defects, add regression tests, rebuild, and revalidate.
5. Rerun the privacy audit and rebuild the release package.
6. Commit the final checkpoint.
7. Do not publish without explicit publication authorization.

## 10. Honest status

The console builds, its pure logic is tested, its assembly references are
clean, its fixture is admitted, and its release package is prepared. The native
hooks, live catalog enumeration, localization helper, factory/forge/drop, UI,
transitions, save/resume, and uninstall behavior remain **unverified** until
the authorized runtime matrix is executed. No runtime-pass claim is made.

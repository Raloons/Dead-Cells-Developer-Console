# DC-M2.3 â€” Public alpha release engineering report

**Status:** offline release engineering complete; **native runtime verification
and publication remain blocked**.

**Starting HEAD:** `5b94a4d`
**Product:** Developer Console v0.1.0-alpha
**Target:** Dead Cells v35 / DCCM 35.13.8

---

## 1. Current project state

The project is a standalone DCCM mod with a live runtime equipment catalog,
command registry, native console integration, item spawning, player utilities,
diagnostics, scroll persistence, configuration, offline tests, fixture tooling,
and release packaging.

The DC-M2.1 native-scroll fix and DC-M2.2 reliability work are preserved.

## 2. Independent release-status statements

| Statement | Status |
|---|---|
| 1. Source compiles successfully | **Passed** â€” Release and Debug, 0 warnings/errors |
| 2. Offline tests pass | **Passed** â€” 139 checks, 0 failures |
| 3. Standalone assembly dependencies are clean | **Passed** â€” no private assembly refs |
| 4. Release packaging is reproducible | **Passed** â€” deterministic ZIP built twice with the same hash |
| 5. Fixture isolation passes | **Passed** â€” R4 admission, no private content |
| 6. Native game boot verified | **Blocked** â€” no authorized launch |
| 7. Console UI verified in gameplay | **Blocked** â€” no authorized launch |
| 8. Item spawning verified in gameplay | **Blocked** â€” no authorized launch |
| 9. Scroll persistence verified through save/resume | **Blocked** â€” no authorized launch |
| 10. Installation/uninstallation verified with real game | **Blocked** â€” only synthetic install trees tested |

## 3. Release blockers

1. Authorized native boot, UI, catalog, spawn, player, transition, save/resume,
   and uninstall verification.
2. Director authorization to create the public repository and publish the
   release.
3. Final publication audit of the exact commit being published.

No other offline engineering blocker was found.

## 4. DC-M2.3 improvements

- Sanitized the public privacy/release scripts: private-content names are no
  longer embedded in the public tree; an external `DC_PRIVATE_DENYLIST` is used
  for authorized local audits.
- Made the release ZIP deterministic with fixed entry timestamps and sorted
  entries; verified two consecutive builds produce the same SHA-256.
- Added `THIRD_PARTY_NOTICES.md`, `CONTRIBUTING.md`, CI scope documentation,
  issue/PR templates, a command-to-test matrix, and a public release checklist.
- Added `tools/Export-PublicSource.ps1` to prepare a clean source export with a
  fresh initial Git history.
- Added `fixture/Test-Installer.ps1` and `fixture/Test-AuthorizationGate.ps1`
  for synthetic install/uninstall and authorization-refusal tests.
- Polished README, installation, release notes, and package documentation.
- Added a source-hygiene GitHub Actions workflow that does not require game
  files and does not attempt an unsupported full build.

## 5. Documentation completed

- `README.md` â€” product overview, release status, quick start, installation,
  safety, compatibility, development, license, disclaimer.
- `docs/INSTALL.md` â€” requirements, package layout, install, verify, uninstall,
  backup.
- `docs/COMMANDS.md` â€” command reference and safety/persistence semantics.
- `docs/TROUBLESHOOTING.md` â€” common failures, parser/config/search/scroll
  guidance.
- `docs/DEVELOPER.md` â€” architecture, parser/result rules, catalog, scroll
  authority, tests, release process.
- `CONTRIBUTING.md` â€” environment, build/test, native caution, contribution
  rules.
- `docs/CI.md` â€” CI scope and the game/DCCM dependency limitation.
- `docs/COMMAND_TEST_MATRIX.md` â€” command-to-test coverage.
- `docs/PUBLIC_ALPHA_RELEASE_CHECKLIST.md` â€” engineering, privacy, legal,
  documentation, native, distribution, and approval gates.
- `docs/GITHUB_RELEASE_DRAFT.md` â€” local-only release text.
- `THIRD_PARTY_NOTICES.md` â€” licensing and dependency boundaries.

## 6. Package manifest

Package: `dist/DeveloperConsole-0.1.0-alpha.zip`

Contents:

- `DeveloperConsole/DeveloperConsole.dll`
- `DeveloperConsole/modinfo.json`
- `README.md`
- `LICENSE`
- `THIRD_PARTY_NOTICES.md`
- `CONTRIBUTING.md`
- `RELEASE_NOTES.md`
- `SHA256SUMS.txt`
- `docs/INSTALL.md`, `docs/COMMANDS.md`, `docs/TROUBLESHOOTING.md`,
  `docs/RUNTIME_VERIFICATION.md`, `docs/PRIVACY_AUDIT.md`,
  `docs/NATIVE_API_AUDIT.md`, `docs/DEVELOPER.md`, `docs/CI.md`,
  `docs/COMMAND_TEST_MATRIX.md`, `docs/PUBLIC_ALPHA_RELEASE_CHECKLIST.md`,
  `docs/SCROLL_PERSISTENCE_INVESTIGATION.md`.

- ZIP SHA-256: `3AAA96C67283CCD6412A418E69CB4532D16C9F36C0F8BEA32316D0408CFC8C81`
- Mod DLL SHA-256:
  `4733254175C0E49786F88ED1D4F496F7E68937826EA48750BD6B72BB7FF21877`

The package contains no game executables, game databases, saves, DCCM
distribution, private expansion content, or fixture data.

## 7. Build and test results

- Release build: 0 warnings, 0 errors.
- Debug build: 0 warnings, 0 errors.
- Offline suite: 139 checks, 0 failures.
- Assembly references: `System.*`, `GameProxy`, `ModCore`,
  `ModCore.Common`, `HaxeProxy`, `HashlinkSharp`, `Serilog` only.
- Deterministic ZIP: two builds, identical SHA-256.
- Fixture R4 admission: 0 failures.
- Installer test: 10/10 checks passed (synthetic trees).
- Authorization gate: 7/7 checks passed; no game process started.
- Privacy audit: PASS (external denylist; tracked text count recorded in the
  privacy report).

## 8. Privacy findings

- The working tree contains no private source, private item/content records,
  game assets, saves, catalogs, or private paths.
- Git history inspection found no tracked binaries, saves, assets, or large
  blobs. `git log -S` found private terms only in the initial commit's
  `tools/Privacy-Audit.ps1` denylist, not in source, documentation, or data.
  The current public scanner uses an external denylist, and the clean export
  has a fresh initial commit with no development history.
- No proprietary game data is tracked or packaged.
- The clean public export is prepared locally at `dist/public-source` with a
  fresh single initial commit, no remote, and no private history.
  It is not pushed.

## 9. Licensing findings

- The project's own code and documentation are MIT-licensed (`LICENSE`).
- The code is maintainer-owned adaptation, not a copy of third-party source.
- DCCM, MDK, GameProxy, HaxeProxy, HashlinkSharp, ModCore, Serilog, .NET, and
  Dead Cells are external dependencies and are not redistributed.
- `THIRD_PARTY_NOTICES.md` records the distribution boundary.
- No third-party license notice was removed.

## 10. Version coherence

The numeric mod/assembly core is `0.1.0`; the public release identifier is
`0.1.0-alpha`. The package filename, README, release notes, and release draft
use `0.1.0-alpha`. Assembly informational version is `0.1.0-alpha`. This is a
deliberate semver core plus prerelease label mapping; no stable-version claim
is made.

## 11. Current runtime status

Native boot, console UI, live catalog/localization, spawning, player effects,
level transitions, save/resume, and real uninstall remain **Blocked by
authorization**. The runtime matrix is authoritative:
`docs/RUNTIME_VERIFICATION.md`.

## 12. Remaining authorized gameplay tests

- Control A: ordinary scroll preservation.
- Control B: console scroll grant preservation.
- Control C: temporary `player stat` behavior across resume.
- Control D: multiple scroll colors and exact counts.
- Control E: repeated resume without duplication.
- Control F: health/HUD/scaling invariants.
- Full R01â€“R20 matrix, including baseline boot, UI focus, spawn, player
  commands, level transitions, save/resume, and uninstall.

## 13. Remaining publication approvals

- Director authorization for one bounded live test session.
- Director authorization to create/push the public repository.
- Director authorization to publish the release and upload assets.
- Final privacy audit on the exact publication commit.

## 14. Proposed next milestone

**DC-M3 â€” Authorized runtime verification and publication.** Execute the
authorized disposable-session matrix, record evidence, fix only real runtime
defects, rebuild/repackage, rerun privacy/package checks, and then create/push
the public repository and release with explicit approval. Do not add another
offline-only preparation pass unless a concrete blocker appears.

## 15. Local checkpoint and clean export

- Development repository: final local commit recorded after this report and
  the final package build; no remote configured; nothing pushed.
- Clean public source export: `dist/public-source`, fresh single initial commit, no remote, no private
  history.
- The export is a candidate only. Creating the public repository or uploading
  the package still requires explicit director authorization.

# Public alpha release checklist

**Release candidate:** Developer Console v0.1.0-alpha / DC-M5 public release (v0.1.0-alpha)
**Generated:** 2026-11-04
**Publication:** NOT AUTHORIZED

Status values: **Passed**, **Failed**, **Pending**, **Blocked**, **N/A**.

## Engineering

| Item | Status | Evidence |
|---|---|---|
| Release build | Passed | 0 warnings, 0 errors |
| Debug build | Passed | 0 warnings, 0 errors |
| Offline regression suite | Passed | 326 checks; registry/docs audit included |
| Standalone assembly review | Passed | no private assembly refs |
| Reproducible release package | Passed | deterministic ZIP built twice, same hash |
| Package/fixture DLL match | Passed | one SHA-256 across build, R16, package |
| Version mapping | Passed | core 0.1.0; release label 0.1.0-alpha |
| Fixture admission | Passed | R16, 0 failures; all priors preserved |

## Privacy

| Item | Status | Evidence |
|---|---|---|
| Working-tree privacy scan | Passed | tracked/new source and SVG/HTML; external denylist |
| Tracked binary/save scan | Passed | only original offline preview PNGs, no game/save binaries |
| Git history review | Passed with note | no private source/assets; early scanner revisions contained the denylist names only |
| Clean public export | Passed | prepared locally with fresh history, not published |
| Private denylist kept out of public tree | Passed | external `DC_PRIVATE_DENYLIST` only |
| Production integrity | Passed | R15 pre/post snapshots unchanged; isolated R16 not launched |

## Legal

| Item | Status | Evidence |
|---|---|---|
| Project license | Passed | MIT |
| Third-party notices | Passed | `THIRD_PARTY_NOTICES.md` |
| Game assets/binaries not bundled | Passed | package entry scan |
| DCCM/MDK not redistributed | Passed | runtime dependency only |
| Attribution for adapted code | Passed | maintainer-owned adaptation documented |

## Documentation

| Item | Status | Evidence |
|---|---|---|
| README | Passed | current release-oriented README |
| Installation | Passed | `docs/INSTALL.md` |
| Command reference | Passed | `docs/COMMANDS.md` |
| Troubleshooting | Passed | `docs/TROUBLESHOOTING.md` |
| Developer guide | Passed | `docs/DEVELOPER.md` |
| Contributing | Passed | `CONTRIBUTING.md` |
| CI limitations | Passed | `docs/CI.md` |
| Runtime matrix | Passed | `docs/RUNTIME_VERIFICATION.md` |
| Release notes | Passed | `docs/RELEASE_NOTES_v0.1.0-alpha.md` |
| GitHub release draft | Passed | `docs/GITHUB_RELEASE_DRAFT.md` |

## Native runtime

| Item | Status | Evidence |
|---|---|---|
| Baseline boot | Blocked | requires authorization |
| Console initialization | Blocked | requires authorization |
| Open/close/input restoration | Blocked | requires authorization |
| Live catalog/search | Blocked | requires authorization |
| Item inspection/spawn | Blocked | requires authorization |
| Player commands | Blocked | requires authorization |
| Level transitions | Blocked | requires authorization |
| Scroll save/resume (R13/R18/H01) | Blocked | requires authorization |
| Uninstall from real installation | Blocked | requires authorization |

## Distribution

| Item | Status | Evidence |
|---|---|---|
| Release ZIP | Passed | `dist/DC-M4.6/DeveloperConsole-0.1.0-alpha.zip` |
| SHA-256 | Passed | recorded in the `.sha256` file and release draft |
| Package manifest | Passed | `SHA256SUMS.txt` in ZIP |
| Clean public source export | Passed | `dist/DC-M4.6/public-source`, local only |
| GitHub repository creation | Blocked | not authorized |
| Push/release upload | Blocked | not authorized |

## Director approval

| Item | Status |
|---|---|
| Authorized native test | Blocked |
| Public publication | Blocked |
| Final release approval | Blocked |

**Do not mark this checklist complete while native runtime items are blocked.**

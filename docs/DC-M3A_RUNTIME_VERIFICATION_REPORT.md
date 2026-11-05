# DC-M3A — native runtime verification report

> **Historical report correction (DC-M4):** the preflight report below was
> written before the later authorized R4/R5 sessions. It is preserved as that
> checkpoint, not the current project status. R4 reproduced a native item-name
> access violation. Removing the unsafe helper allowed R5 to reach gameplay;
> manual screenshots showed console/catalog/player commands and a 0→2 native
> Brutality grant. English names failed in R5; `StartSword` is the correct
> starting-sword ID. R5 ended at the runner timeout. Its Haxe stack originates
> from a missing fixture `save/user_0.dat`, not an established console crash.
> The earlier runner's `PASS` ignored exit codes and is not gameplay proof.
> R6 preserves a newer unlaunched GetText candidate. Save/resume remains unverified.
> Current status: `RUNTIME_VERIFICATION.md` and `DC-M4_CONSOLE_OVERHAUL_REPORT.md`.

## Original preflight checkpoint

**Completion classification: Offline-complete / runtime pending — BLOCKED BY
AUTHORIZATION.**

**Starting/current development HEAD:** `637b27e`
**Fixture:** `<fixture root>/DC-M2-R4`
**Target:** Dead Cells v35 / DCCM 35.13.8
**Console DLL:** `4733254175C0E49786F88ED1D4F496F7E68937826EA48750BD6B72BB7FF21877`
**Release ZIP:** `dist/DeveloperConsole-0.1.0-alpha.zip`
**ZIP SHA-256:** `3AAA96C67283CCD6412A418E69CB4532D16C9F36C0F8BEA32316D0408CFC8C81`

No game process was launched. No production save, Steam Cloud data, or private
project was accessed or modified.

## 1. Why native verification did not run

The DC-M3A prompt explicitly says a development prompt is not a valid
live-session authorization receipt. The repository contains only the synthetic
authorization template; no real `dc-m2-authorization/1` receipt exists for
`DC-M2-R4` and the current DLL.

The runner was not bypassed. Negative tests confirm the gate:

- missing `-Authorize`: refused, no process;
- synthetic template: refused, no process;
- malformed receipt: refused, no process.

The exact one-session request is recorded in
`docs/DC-M3A_SESSION_REQUEST.md`. A local untracked request with the exact
fixture path and current hash is under `dist/DC-M3A/`, but it is synthetic and
is not a valid receipt.

## 2. Fixture preflight

`fixture/Test-FixtureAdmission.ps1` was run in `VerifyOnly` mode against R4:

| Check | Result |
|---|---|
| Framework entry present | PASS |
| DCCM 35.13.8 | PASS |
| Package file set/hashes | PASS |
| Package DLL matches build | PASS |
| `coremod/mods` empty | PASS |
| Override mods only DeveloperConsole | PASS |
| Stock loader only | PASS |
| `coremod/data` empty | PASS |
| `coremod/config` empty | PASS |
| No private-content names | PASS (external denylist) |
| Fixture save roots empty | PASS |
| **Total** | **0 failures** |

No production configuration, logs, saves, Workshop content, or unrelated
gameplay mods were found. R1–R3 remain preserved; R4 is the current candidate.

## 3. Offline evidence

- Clean Release build: 0 warnings, 0 errors.
- Clean Debug build: 0 warnings, 0 errors.
- Offline regression suite: 139 checks, 0 failures.
- Assembly references: `System.*`, `GameProxy`, `ModCore`,
  `ModCore.Common`, `HaxeProxy`, `HashlinkSharp`, `Serilog` only.
- Privacy audit: PASS over 59 tracked text files with the external denylist.
- Release package: reproducible; ZIP SHA-256 matches the recorded value.
- Build DLL, R4 fixture DLL, and package DLL are identical.
- Installer test: 0 failures across install/refuse/replace/uninstall/sibling/
  missing-target cases.
- Authorization gate: 7/7 checks pass; no game process started.
- Clean public source export remains available under `dist/public-source`.

## 4. Runtime matrix status

Every native check remains **Blocked by authorization**. No result was observed
and no native pass is claimed.

| ID | Check | Status |
|---|---|---|
| R01 | Game startup | Blocked |
| R02 | Console initialization | Blocked |
| R03 | Console open | Blocked |
| R04 | Console close | Blocked |
| R05 | Normal input after close | Blocked |
| R06 | Help/discovery/suggestions | Blocked |
| R07 | Runtime catalog | Blocked |
| R08 | Search/item information | Blocked |
| R09 | Stock equipment spawning | Blocked |
| R10 | Level selection | Blocked |
| R11 | Quality/legendary | Blocked |
| R12 | Player inspection | Blocked |
| R13 | Run-state commands | Blocked |
| R14 | Debug commands | Blocked |
| R15 | Invalid command handling | Blocked |
| R16 | Repeated rapid submissions | Blocked |
| R17 | Level transition | Blocked |
| R18 | Save/resume | Blocked |
| R19 | UI presentation | Blocked |
| R20 | Uninstall/baseline return | Blocked |
| H01 | Scroll persistence | Blocked |
| A–F | Scroll controls | Blocked |

The full matrix and evidence procedure are in `docs/RUNTIME_VERIFICATION.md`;
the command checklist is `fixture/Runtime-Checklist.md`.

## 5. Native tests still required

1. Baseline boot and DCCM/console discovery.
2. Console open/close/reopen, focus, input restoration.
3. Live catalog initialization and localization.
4. Search, item info, categories, typo hints.
5. Stock melee/ranged/shield/skill spawns.
6. Level, Roman numeral, quality, and legendary behavior.
7. Player stats/status/position/mutations/effects/health/heal/gold.
8. Debug snapshot/player/level/equipment/status/catalog/mods.
9. Invalid input and parser bounds.
10. Config/UI/help/suggest/history.
11. Level transitions and post-transition spawn.
12. Ordinary save/resume and repeated resume.
13. Scroll controls A–F, including no duplication and health invariants.
14. Uninstall and fresh-launch baseline.
15. Performance/stability and log volume.

## 6. Next action

The director must issue a real `dc-m2-authorization/1` receipt for
`<fixture root>/DC-M2-R4` and DLL
`4733254175C0E49786F88ED1D4F496F7E68937826EA48750BD6B72BB7FF21877`, with a
current issue time, `runSeconds: 300`, session `DC-M3A-LIVE-1`, and the three
attestations. Then run the exact command in
`docs/DC-M3A_SESSION_REQUEST.md`.

No further offline preparation is required to start that session.

## 7. Publication

Publication remains unauthorized. No remote was configured and nothing was
pushed. The existing release package and public-source export remain local
candidates.

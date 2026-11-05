# Runtime verification matrix

**Current candidate: DC-M4.6 simple console (R16 DLL), native retest pending fresh explicit authorization.**
The R01–R20/H01 matrix below applies to the current candidate. Prior sessions
below are historical evidence, not verification of this binary. Keep the full
matrix and scroll controls A–F in
`SCROLL_PERSISTENCE_INVESTIGATION.md` for the next separately authorized session.

## Historical live findings and current retest scope

| Evidence | Finding |
|---|---|
| R4 first live session | Startup native access violation (`0xC0000005`) in the old item-name helper; helper removed |
| R5 manual session | Reached title/gameplay; console rendered; version/catalog/player commands used; 645 CDB rows, 218 policy-eligible, 12 internal |
| R5 manual scroll grant | Brutality native count 0→2, effective tier 3, HUD 3, health 228/228 visible |
| R5 item-name tests | English lookup failed; the tested native ID was also incorrect: starting sword is `StartSword` |
| R5 Haxe stack | Missing fixture `save/user_0.dat` during native load attempt; not proof of a console crash |
| R5 session end | Runner timeout (~303 seconds), not an established gameplay crash |
| R6 | Prepared GetText candidate; preserved and never launched for DC-M4 |
| DC-M4 | New candidate requires all native retests below plus M4-01…M4-06 |
| DC-M4.1 | Fancy replaced in place by Workbench; requires the full inherited matrix plus M4.1-01…M4.1-08 below |
| R9 first launch | Failed before gameplay at Steam initialization; default fixture `EnableGoldberg=false`; exit 1, no production delta |
| R9 authorized retry | Reached gameplay after fixture-only startup correction; user screenshot shows Workbench Home, 645 catalog rows and see-through/misplaced backgrounds; clean exit is not full UI verification |
| R10 current candidate | Opaque backdrop and explicit pooled scene-transform invalidation; offline qualified, native retest pending fresh authorization |
| DC-M4.2 current candidate | Exact-size system-font atlases, `Console` identity, concise copy; offline qualified (849 checks), native retest pending fresh authorization |
| DC-M4.3 current candidate | White-gutter atlas, nearest sampling, sampler harness + display-chain diagnostic; offline qualified, native retest pending fresh authorization |
| DC-M4.4 | Workbench removed, simple `Console` overlay restored; all commands kept |
| R13 session (M4.4) | 86s, clean exit 0, no delta; catalog linked, panel built, one open/close cycle, no errors |
| DC-M4.5 | Compact type, honest spawn messages, integer-field write fix from R14 log evidence |
| R14 session (M4.5) | 303s timeout with active console use to the end; exposed pre-existing cohesive-restore double→int bug (fixed); no delta |
| R15 | Integer-fix build staged, admitted, never launched |
| DC-M4.6 | Separator/tofu fix, scroll model, help pages, `uimode` removal, `itemid`/`>` alignment, `mutation` family; offline qualified (326 checks) |
| R16 session (M4.6) | 202s, clean exit 0, no delta; 5 open/close cycles, no errors; command/mutation effect behavior per director notes, not session logs |
| Steam/Workshop compatibility | R9 Steam-init failure anatomy, Goldberg role, three launch paths, session safety policy, pending Workshop end-to-end |

No historical observation proves save/resume, mutation scaling or the new UI.
Earlier runner `PASS` values ignored nonzero exits and must not be used as
gameplay success. DC-M4 classifies nonzero exits as `FAIL_PROCESS_EXIT` and
zero exits as `EXITED_CLEANLY_UNVERIFIED`; manual evidence is still required.

## Status categories

| Status | Meaning |
|---|---|
| **Offline verified** | Proven without launching the game (build, static audit, pure tests, fixture file checks). |
| **Native boot verified** | A real DCCM process reached a playable state in the disposable fixture. |
| **Live gameplay verified** | The check was executed in an authorized live run with evidence. |
| **Not tested** | Prepared but not executed for a reason other than authorization. |
| **Failed** | Executed and did not meet the expected result. |
| **Blocked by authorization** | Prepared and ready, but requires an explicitly authorized live session. |

## Offline qualification (not a substitute for runtime)

| Gate | Status | Evidence |
|---|---|---|
| Release build | Offline verified | 0 warnings, 0 errors |
| Debug build | Offline verified | 0 warnings, 0 errors |
| Offline acceptance suite | Offline verified | Command/catalog/scroll/mutation/history/layout/config contracts; exact final count in the DC-M4.6 report |
| DC-M2.2 reliability pass | Offline verified | Parser limits, safety tags, token search/typo hints, atomic config, heal/gold bounds, internal-error isolation |
| Scroll authority and native allocation path | Offline verified | Decompiled v35 bytecode plus native-path tests |
| Assembly reference audit | Offline verified | `System.*`, `GameProxy`, `ModCore`, `ModCore.Common`, `HaxeProxy`, `HashlinkSharp`, `Serilog` only |
| No private assembly/catalog reference | Offline verified | `modinfo.json` has no dependencies; `deps.json` only MDK |
| Fixture package hash matches build | Offline verified | `Test-FixtureAdmission.ps1` PASS |
| Fixture has no private-content names | Offline verified | `Test-FixtureAdmission.ps1` PASS |
| Fixture save roots empty | Offline verified | `Test-FixtureAdmission.ps1` PASS |
| Synthetic authorization refused | Offline verified | Launcher refused with `synthetic-template`; no process started |
| Missing `-Authorize` refused | Offline verified | Launcher printed `BLOCKED`; no process started |
| Privacy audit | Offline verified | `docs/PRIVACY_AUDIT.md` PASS |

## Fixture identity

The current DC-M4.6 fixture is `<fixture root>/DC-M2-R16`, outside the repository.
Its exact path and package hashes are recorded in its local
`fixture-manifest.json`. All prior fixtures and session evidence folders are preserved.
No prior authorization carries forward to the changed DLL. Fixture-local
`EnableGoldberg=true` is staged explicitly for the previously observed Steam-closed
startup path; it is separate from launch authorization.

- Game: Dead Cells v35.
- Framework: DCCM 35.13.8.
- Mod: DeveloperConsole 0.1.0, main `DeveloperConsole.DeveloperConsoleMod`.
- Package SHA-256: see the R10 fix report and sealed fixture manifest; validate
  the current build and receipt again before any future launch.
- Loader: stock `ModCore.ModLoader.Default` pinned by SHA-256.
- Save roots: fixture-local `save` and `GameRoot\save`, both empty.
- No production mods, plugins, data, config, logs, Workshop content, or
  private expansion content.

Admission and negative gate results are recorded in the DC-M4.4 report. Offline
admission does not grant permission to launch.

## Matrix

### R01 - Game startup

- **Objective:** Launch the disposable DCCM tree to a playable state with only the standalone console installed.
- **Prerequisites:** Fixture admitted; no Steam/game process running; authorized receipt.
- **Method:** `fixture/Invoke-RuntimeSession.ps1 ... -Authorize`; observe process exit, logs, and window.
- **Expected:** The game reaches a playable state; DCCM version 35.13.8 loads; no private assembly loads.
- **Observed:** R13/R14/R16 all reached gameplay with DCCM v35 (2026-06-16 build); admission file set clean; no production delta in any session.
- **Status:** **Live gameplay verified.**
- **Evidence:** Fixture manifests, admission outputs, session `evidence.json` (R13/R14/R16).
- **Uncertainty:** Whether the stock DCCM entry point and fixture-local save overrides behave as expected on this machine.

### R02 - Console initialization

- **Objective:** Verify mod discovery, entry-point execution, hook attachment, and native console capture.
- **Prerequisites:** R01 starts.
- **Method:** Inspect the DCCM log for `DeveloperConsole initialized`, `console captured (starts hidden)`, and `live item catalog linked`.
- **Expected:** All three initialization lines appear once; no hook or entry-point exception.
- **Observed:** `DeveloperConsole initialized (30 commands)`, `console captured (starts hidden)`, `live item catalog linked` (645 rows) in R13/R14/R16 logs; no hook exceptions.
- **Status:** **Live gameplay verified.**
- **Evidence:** DCCM log under the fixture evidence directories.
- **Uncertainty:** Compile-time hook signatures match the installed proxy, but dispatch/lifetime is unproven.

### R03 - Console open

- **Objective:** Open the console with the default `=` toggle during active gameplay.
- **Prerequisites:** R01 and R02.
- **Method:** Enter a run, press `=` once.
- **Expected:** The custom panel appears; the native input field accepts text; gameplay input is locked while open.
- **Observed:** Open/close cycles logged without errors in R13 (1), R14 (several, incl. cohesive fallback), R16 (5); director operated the console across sessions (spawns, scrolls). Input-lock and caret behavior not log-proven.
- **Status:** **Live gameplay verified** for open/close; input-lock specifics remain **pending**.
- **Uncertainty:** Native focus/input-lock behavior in the standalone fixture.

### R04 - Console close

- **Objective:** Close with `=` on an empty input line and with `Escape`.
- **Prerequisites:** R03.
- **Method:** Open, close with `=`, reopen, close with `Escape`.
- **Expected:** Panel and native bar hide; no residual input capture.
- **Observed:** Not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots and log lines `explicit close`.
- **Uncertainty:** Native auto-show suppression across frames.

### R05 - Normal game input after close

- **Objective:** Confirm movement and normal controls work after closing.
- **Prerequisites:** R04.
- **Method:** Walk, jump, attack, and open the pause menu after closing.
- **Expected:** Normal input is restored; typing does not move the player.
- **Observed:** Not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshot/video and input log.
- **Uncertainty:** The native input lock is preserved by keeping `bg.visible`; this must be observed live.

### R06 - Help, discovery, and suggestions

- **Objective:** Verify `help`, `help <command>`, `suggest`, and did-you-mean messages.
- **Prerequisites:** R03.
- **Method:** Run `help`, `help spawn`, `help player`, `suggest spa`, and a misspelled command.
- **Expected:** Readable command list; usage/examples; command and equipment suggestions; typo suggestion.
- **Observed:** Offline command tests pass; live rendering not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Offline suite plus live screenshot.
- **Uncertainty:** Long help text fit and scroll behavior in the panel.

### R07 - Runtime equipment catalog

- **Objective:** Verify catalog initialization and live record resolution.
- **Prerequisites:** R02; CDB loaded.
- **Method:** Run `debug catalog`, `item categories`, and `search` against known stock rows.
- **Expected:** Non-zero entry count; `live CDB` source; category counts; known stock IDs resolve; non-equipment rows are lookup-only.
- **Observed:** Offline catalog model tests pass; native enumeration not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** `debug catalog` output and DCCM log.
- **Uncertainty:** `item.all[]`/`byId.keys()` shape, localized-name helper, and CDB timing.

### R08 - Search and item information

- **Objective:** Exercise `search`, `item info`, `item list`, and `item categories` with valid and invalid arguments.
- **Prerequisites:** R07.
- **Method:** Search by ID and display name; filter by category; request a limit; request an unknown item.
- **Expected:** Ranked results, bounded output, accurate metadata, clear failures.
- **Observed:** Offline command tests pass; live output not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots of each command and offline suite.
- **Uncertainty:** Live display-name quality and result readability.

### R09 - Stock equipment spawning

- **Objective:** Verify representative stock melee, ranged, shield, and skill drops.
- **Prerequisites:** R07; active hero.
- **Method:** `spawn StartSword`, `spawn "Quick Bow"`, `spawn DashShield`, `spawn FireBomb`; walk over each drop.
- **Expected:** A usable native item appears and can be picked up.
- **Observed:** Not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots, pickup behavior, DCCM log.
- **Uncertainty:** Native factory/drop path and ownership in the standalone environment.

### R10 - Level selection

- **Objective:** Verify numeric and Roman level arguments and read-back.
- **Prerequisites:** R09.
- **Method:** `spawn StartSword level=4`, `level=IV`, `level=99`; inspect the dropped item.
- **Expected:** Actual level matches the request or the command fails loudly.
- **Observed:** Offline parser tests pass; native finalize/read-back not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Item tooltip/screenshot and command output.
- **Uncertainty:** Native finalize arithmetic and read-back in this build.

### R11 - Quality and legendary selection

- **Objective:** Verify `base`, `+`, `++`, `S`, and `L` behavior.
- **Prerequisites:** R09.
- **Method:** Spawn the same item at each quality; spawn a legendary-capable item with `quality=L`; attempt `L` on a row without a legendary form.
- **Expected:** Quality read-back matches; unsupported legendary forms fail clearly.
- **Observed:** Offline parser and policy tests pass; native forge not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Item tooltip/screenshots and command output.
- **Uncertainty:** Legendary affix/form behavior and native quality read-back.

### R12 - Player inspection

- **Objective:** Verify `player stats`, `position`, `status`, `mutations`, and `effects`.
- **Prerequisites:** R01; active hero.
- **Method:** Run each read-only command in a live run.
- **Expected:** Values correspond to the visible/native state; unavailable fields say `unavailable`.
- **Observed:** Offline mock tests pass; live reads not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots and DCCM log.
- **Uncertainty:** Hero/inventory/affect field reads and output readability.

### R13 - Run-state commands

- **Objective:** Verify `player heal`, `health`, `gold`, `stat`, and `scroll` with bounded values, including the native scroll-persistence fix.
- **Prerequisites:** R12.
- **Method:** Heal to full and by a partial amount; set health to 50; add 50 gold; set a temporary stat override; run `player scroll brutality 2`, `player scroll tactics -1`, and an over-removal; record native counts and tiers.
- **Expected:** Visible state changes; native scroll counts change and persist; over-removal and invalid values fail without changing counts; `player stat` remains a labeled temporary override.
- **Observed:** Offline mock tests pass, including native count/resume/no-duplicate cases; live writes and save/resume not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots, command output, `player status` native counts, save compare.
- **Uncertainty:** Native `Inventory.add`/`BagItem` behavior, HUD refresh, and actual save/resume persistence.

### R14 - Debug commands

- **Objective:** Verify `debug snapshot`, `player`, `level`, `equipment`, `status`, `catalog`, and `mods`.
- **Prerequisites:** R07; active hero.
- **Method:** Run each debug subcommand.
- **Expected:** Accurate, bounded, readable output; unavailable data reported honestly.
- **Observed:** Offline mock tests pass; live reads and mod-loader reflection not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots and DCCM log.
- **Uncertainty:** `ModLoader` reflection and level identity fields.

### R15 - Invalid command handling

- **Objective:** Verify graceful handling of empty, unknown, misspelled, and malformed commands.
- **Prerequisites:** R03.
- **Method:** Submit empty input, `nope`, `spwan`, `spawn`, invalid IDs, invalid numbers, and missing arguments.
- **Expected:** Local error messages; no crash; native commands still fall through.
- **Observed:** Offline tests pass; live dispatch not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots and DCCM log.
- **Uncertainty:** Native fall-through and output formatting.

### R16 - Repeated rapid submissions

- **Objective:** Verify repeated commands and rapid toggling do not destabilize the console.
- **Prerequisites:** R03.
- **Method:** Open/close 10 times; run `debug snapshot` 20 times; submit an invalid command repeatedly.
- **Expected:** Stable UI, bounded history, no duplicate panels, no exceptions.
- **Observed:** Offline history/layout tests pass; live repetition not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** DCCM log and screenshots.
- **Uncertainty:** UI object lifetime and native log/history behavior.

### R17 - Level transition and lifecycle

- **Objective:** Verify the console after leaving and entering a level, including catalog/player refresh.
- **Prerequisites:** R01; a run with a level transition available.
- **Method:** Open/close before transition; transition; reopen; run `player status` and `debug catalog`.
- **Expected:** No stale hero; no duplicate panel; catalog still available; no exceptions.
- **Observed:** Not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots and DCCM log across the transition.
- **Uncertainty:** Native object lifetime across level transitions.

### R18 - Save/resume

- **Objective:** Verify console operation and native scroll persistence after save and resume, where separately authorized.
- **Prerequisites:** R17; save/resume authorization; disposable save.
- **Method:** Run `player scroll brutality 2`, record native count/tier, save, return to title, resume, run `player status` and `player stats`, repeat the resume once; separately test a temporary `player stat` override across the same cycle.
- **Expected:** Native scroll count and tier survive both resumes with no duplication; `player stat` is gone after resume; no production save touched.
- **Observed:** Offline model tests cover resume/no-duplicate; live save/resume not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Evidence JSON, screenshots of `player status`, log, save tree hashes.
- **Uncertainty:** Actual `Inventory.serialize`/`unserialize` round-trip and sidecar/CDB lifecycle.

### R19 - UI presentation and usability

- **Objective:** Review the console as a public-facing tool.
- **Prerequisites:** R03.
- **Method:** Review help, errors, long output, search results, `consize`, `config`, `version`, `clear`, and history scroll.
- **Expected:** Readable text, usable input, bounded output, no permanent gameplay obstruction.
- **Observed:** Offline layout/history tests pass; visual review not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Screenshots at representative resolutions.
- **Uncertainty:** Font/line-height fit and native panel text rendering.

### R20 - Uninstallation and baseline return

- **Objective:** Remove the console and verify the disposable tree returns to baseline.
- **Prerequisites:** A completed session; fixture copy.
- **Method:** Remove `dccm/mods/DeveloperConsole`, clear fixture config/logs, relaunch, verify no console and no errors.
- **Expected:** Clean baseline; production saves/install untouched.
- **Observed:** Not executed.
- **Status:** **Blocked by authorization.**
- **Evidence:** Post-uninstall log and production hash comparison.
- **Uncertainty:** DCCM cache/history cleanup and config persistence.

## Release-hardening check H01 - Scroll persistence

This check is part of the DC-M2 scroll-persistence requirement. It does not
replace R13 or R18.

- **Objective:** Prove that `player scroll` uses the native scroll-allocation
  authority and survives save/resume without duplication.
- **Prerequisites:** R13 and R18; disposable save; save/resume authorization.
- **Method:**
  1. Record `player status` native scroll counts and tiers.
  2. Run `player scroll brutality 2`, then `player scroll tactics -1`.
  3. Save, return to title, continue, and run `player status`/`player stats`.
  4. Repeat the resume once.
  5. Run `player stat brutality 10`, save/resume again, and verify the
     temporary override is gone while the native count remains.
- **Expected:** Native counts change by exactly the requested delta, survive
  both resumes, and do not duplicate scrolls or health bonuses. `player stat`
  is temporary and labeled as such.
- **Observed:** Director-observed (R13 era, production run): setting and adding
  scrolls survived leaving to the menu and continuing the same run. Removal,
  multiple colors, repeated resumes and health/scaling invariants not observed.
- **Status:** **Partially observed; remainder blocked by authorization.**
- **Evidence:** `docs/SCROLL_PERSISTENCE_INVESTIGATION.md`, command screenshots,
  session `evidence.json`, save-tree hashes.
- **Uncertainty:** Actual native `Inventory.serialize`/`unserialize` round-trip
  and `BagItem` restore behavior.

## DC-M4 additional native acceptance (all pending)

| Check | Manual method and expected evidence |
|---|---|
| M4-01 Natural/English names | In English and another selected language: `spawn assault shield`, `spawnitem assault shield`, `spawn Rusty Sword`, `spawn StartSword`, `item info assault shield`, `search assault shield`; inspect canonical IDs and pickups |
| M4-02 Ambiguity and late registration | `spawn sword` lists choices without a drop; typo only suggests; register a supported mod row, `catalog refresh`, inspect/resolve it; unsupported factory fails honestly |
| M4-03 Scroll shortcuts | Compare ss/as/rs to player scroll; test base/equipment floor, temporary override refusal/clear, insufficient removal, exact tier read-back; record counts/HP/HUD/mutation effects and execute A–F/save-resume |
| M4-04 Completion | Command/subcommand/item/stat/mutation contexts; Tab/Shift+Tab cycles without mutation; Enter submits once; native history/focus and gameplay input lock survive |
| M4-05 Presentation lifecycle | Single overlay; config reload, height/width/scale/opacity; resize 720p–ultrawide; rapid toggle, resource/focus/fallback behavior and level transition |
| M4-06 Session utilities | build/equipped/mods/catalog/history/errors/recent/debug last; repeat remains read-only after mutations; spawn last causes exactly one explicit spawn; no persistence of session journals |

## Mutation native acceptance (all pending)

Offline contracts cover resolution, equip/remove validation, duplicates, slot
refusals, rollback and output classification against a mock inventory. Native
effect behavior is unverified. When a separately authorized session is
available, verify in order:

1. `mutation list` loads group-12 rows with display names.
2. Names/IDs/aliases resolve, including multiword and ambiguous cases.
3. `mutation equip <name>` flips native equipped state (`isEquiped`, rescan).
4. The equipped effect actually activates in gameplay.
5. Slot limits are enforced; no silent replacement (replacement is reported).
6. Duplicate equip reports already-equipped without duplicating effects.
7. `mutation remove <name>` detaches effects and clears HUD/scaling state.
8. Level transitions preserve expected behavior.
9. Save/resume preserves expected mutation state (persistence NOT claimed).
10. Invalid operations fail without destabilizing gameplay.

Do not launch the game merely for this list; it runs inside a future
authorized session alongside R01–R20/H01 and M4-01…M4-06/M4.4-01…M4.4-04.

## Retired UI acceptance (M4.1–M4.3, superseded by DC-M4.4 removal)

M4.1-01…M4.1-08 (Workbench shell, pointer/click, catalog builder, pins,
Preferences pages, dock/problems) no longer apply: the Workbench, its commands
(`workbench`, `browse`, `pin`, `unpin`) and its custom typography were removed.
The inherited R01–R20/H01 and M4-01…M4-06 checks above still apply to the
simple overlay. Historical detail remains in the archived milestone reports.

## DC-M4.4 simple-console acceptance (all pending)

| Check | Manual method and expected evidence |
|---|---|
| M4.4-01 Open/close | `=` opens the `Console` overlay in a run; `=`/Esc closes; unavailable on menus; gameplay input locked while open |
| M4.4-02 Output/input | Command results, warnings and errors readable; native input accepts/edit text; history recall; Page Up/Down scrolls; `clear`; Tab completion without execution |
| M4.4-03 Lifecycle | `consize`, `config reload`; 720p–ultrawide resize; rapid toggle; level/hero/console replacement; renderer-failure fallback; no stuck keys |
| M4.4-04 Inherited behavior | Full R01–R20/H01 and M4-01…M4-06 still required, including native drops/forge/pickups, HUD/HP/mutations and save/resume without duplicate grants |

## DC-M4.1 workbench acceptance (retired; see above)

Superseded by the DC-M4.4 removal; retained here only as a historical pointer.
Do not execute M4.1-01…M4.1-08.

Use exact M4.6 fixture/build hashes from the M4.6 report/manifest when requesting a
future manual session. No M4.6 authorization receipt has been created. Prior
receipts/fixtures do not authorize this candidate. Prepare an explicit bounded
session request with manual input and evidence collection after offline review.

## Authorization and evidence procedure

1. The fixture is prepared and admitted. Do not use production saves.
2. Create a real `dc-m2-authorization/1` receipt from the user's explicit
   authorization. The template is synthetic and is refused.
3. Run the launcher with `-Authorize`, the receipt, and a bounded window:

   ```text
   powershell -ExecutionPolicy Bypass -File fixture/Invoke-RuntimeSession.ps1 `
     -FixtureRoot "<fixture root>" `
     -ReceiptPath "<receipt.json>" `
     -AuthorizedBy "<explicit authorization>" `
     -SessionName "DC-M2-LIVE-1" `
     -RunSeconds 120 `
     -ExpectedBuildDir "src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole" `
     -Authorize
   ```

4. Record the result and evidence path. The launcher writes
   `evidence.json`, stdout/stderr, fixture logs, and copied fixture save data.
5. Update the status of each executed check from **Blocked by authorization**
   to **Native boot verified** or **Live gameplay verified**, or to **Failed**
   with the exact error.
6. Do not mark a check passed without an executed observation.

## Honest status statement

DC-M4.1 has offline implementation, contract/layout coverage, local artifacts and
a sealed fixture. Its new native localization, spawn inputs, UI/completion,
resource/focus/lifecycle behavior and persistence are **not verified** until an
explicitly authorized manual session executes this matrix. Earlier R5 evidence
is retained with its narrower scope above. No receipt is created for R10.

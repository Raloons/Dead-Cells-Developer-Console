# DC-M5 — Public release, final QA & community distribution

**Milestone:** DC-M5, following DC-M4.6 (`7987983`).
**Version:** 0.1.0-alpha (assembly/mod 0.1.0); conservative prerelease kept —
evidence does not support a stable label.
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Status:** release candidate prepared; public release NOT authorized.

## 1. Reconciliation

HEAD at start: `7987983`. Milestone numbering continues sequentially
(DC-M4.4 → M4.5 → M4.6 → DC-M5); the brief's "DC-M4.3 checkpoint" warning was
stale. All accepted work preserved; no feature development undertaken.

## 2. Feature inventory (30 registered commands)

- Core: `help` (overview/pages/detail), `suggest`, `config`, `version`,
  `consize`, `clear`, `history`, `errors`, `aliases`, `repeat`.
- Equipment: `search`, `item` (info/list/categories), `spawn` (`spawnitem`),
  `itemid` (`id`), `catalog`, `recent`, `spawn last`.
- Player: `player` (stats/heal/health/position/status/mutations/effects/gold/
  stat/scroll), `ss`/`as`/`rs` + long aliases, `heal`, `hp`, `gold`, `pos`,
  `stats`, `equipped`, `mods`.
- Mutations: `mutation` (`mut`) list/info/equipped/equip/remove — implemented
  with verified inventory verbs and rollback; effect activation pending live
  proof (stated, not claimed).
- Debug: `debug` (snapshot/player/level/equipment/status/catalog/mods/last).
- Inspect: `build`. Docs verified to name every command (`COMMANDS.md`
  machine-checked); retired names (`workbench`, `browse`, `pin`, `unpin`,
  `uimode`) return ordinary unknown-command responses.

Key flows kept: `spawn assault shield`, `spawnitem assault shield`,
`ss tactics 20`, `as brutality 5`, `rs survival 3`, natural names throughout.

## 3. Final QA

- Suite: **326 checks, 0 failures** (+2 installed-template contracts).
- Release + Debug builds, 0 warnings/errors.
- Assembly references: `System.*`, GameProxy, ModCore stack, HaxeProxy,
  HashlinkSharp, Serilog only (per `Qualification` output path).
- No BTD/private references: privacy audit PASS; public export independently
  re-scanned — single commit, no remotes, no binaries, external-denylist
  clean. One apparent hit triaged as a scanner false positive: a drive-prefix
  denylist term matching inside a C# escape inside a code sample (the audit's
  own path-token rule already excludes it; the committed audit passes).
- Deterministic packaging: rebuilt identically, checksums verified.
- Fixture: R16 (M4.6 binary, unchanged in M5) re-admitted VerifyOnly with
  **0 failures** after clearing post-session disposable saves. Finding during
  M5: the R16 live session had left `save/user_0.modded.json` etc. in the
  fixture — correct per design (disposable saves), admission correctly
  refused until hygiene; session evidence untouched in its run directory.
- Auth-gate/installer/outcome negatives: pass (M4.6 logs; scripts unchanged).
- Triage: no release blockers. Nonblocking: wheel-attach unverified (keyboard
  fallback solid), mutation effects unverified, save/resume partial
  (director-observed set/add only), quest/key rows refused by design.

## 4. Native evidence summary (honest scope)

- Boot/init/open-close: verified across R13/R14/R16 logs.
- Spawning, scroll set/add persistence: director-observed, recorded with
  scope; session logs corroborate catalog/panel/command flow, not visuals.
- Mutation equip/remove, level transitions, full save/resume, uninstall:
  pending. Matrix marks exactly this — nothing beyond evidence is claimed.

## 5. Distribution artifacts

- Package: `dist/DC-M5/DeveloperConsole-0.1.0-alpha.zip` (+`.sha256`),
  28 files (docs only, no previews), DLL identical to validated R16 build.
- Public source: `dist/DC-M5/public-source`, 263 files, single commit
  `e1ec0d0`, no remote, privacy-audited. Original dev history preserved
  locally and never exported.
- Docs finalized: restructured README (features/requirements/install/
  quick-start/compatibility/safety/building), verified INSTALL paths,
  rewritten release draft, updated matrix/notes/checklist.
- Local tag: `v0.1.0-alpha` on the M5 checkpoint (local only).

## 6. Publication authorization status

**NOT RECEIVED.** The director approved beginning the milestone, not the
external act. Awaiting explicit authorization for destination
(`Raloons/Dead-Cells-Developer-Console`) and artifacts before any remote
creation, push, or upload. `gh` CLI is not installed; no existence check was
possible offline — also pending. When authorized: create repo, push the
exported history, upload ZIP + checksum, publish the drafted release as
prerelease, verify the download link.

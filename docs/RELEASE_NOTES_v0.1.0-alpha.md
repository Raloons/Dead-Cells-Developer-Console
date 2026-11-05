# Developer Console v0.1.0-alpha — DC-M5 release candidate

No source changes since the validated R16 binary; this release refreshes documentation, the release draft, and the public export. See `DC-M5_PUBLIC_RELEASE_REPORT.md`.

**Local pre-publication candidate. Offline-complete; native retest pending.**
Target: Dead Cells v35 / DCCM 35.13.8. Mod/assembly version remains 0.1.0.

## Major changes

### DC-M4.6 console usability and mutations

- Readable separators: em-dash/mojibake tofu replaced with `-`/`...`
  throughout console format strings (game/item text untouched).
- Scrollable output: sticky read position, Page Up/Down viewport paging,
  best-effort wheel scrolling, `help` overview + `help N` pages; Up/Down stay
  on history, editing keys untouched.
- `uimode` removed; `mutation` (`mut`) family added: `list`, `info`,
  `equipped`, `equip`, `remove` with verified native inventory verbs,
  post-state checks and rollback. Effect activation: native-pending.
- `itemid` returns one green `Display (ID)` line; `>` prompt shares the
  input baseline by construction.

- Compact classic typography (title/body 13, denser lines) and a concise
  one-line-per-command Help listing; `help <command>` detail unchanged.
- Spawn pipeline audited release-blocker-style: `spawn assault shield` is
  director-confirmed working; success is only reported after the native
  `ItemDrop` loot entity completes, and messages now say "at your feet".
  Non-equipment rows (`walljumpkey`, `biggem`, …) stay refused with specific
  reasons; no `spawnpickup` added (no verified generic path; quest objects
  must not be guessed into existence).
- Director-observed scroll persistence (set/add survive menu/continue)
  recorded in H01 with exact scope; implementation untouched.

### DC-M4.4 simplification (retained)

Per director decision, the desktop-style Workbench is removed and the console
is a single traditional overlay titled `Console`: dark background, scrollable
output, native input row, history, lightweight completion, `consize` sizing.

- Deleted: Workbench shell/state/presentation, Explorer, dashboards, catalog
  builder, pins, Preferences pages, Inspector, pointer navigation, custom
  GDI font atlases, sampler harness, preview generators, `UiLayout`.
- Retired commands: `workbench` (`desk`), `browse`, `pin`, `unpin`.
- Retired config keys (`UiMode`, `Accent`, `Density`) are ignored when present
  in old files; nothing crashes.
- `uimode` (`ui`, `theme`) was removed in DC-M4.6; the transplant input
  layout stays on with no user-facing switch.
- Preserved: every gameplay command, natural-name resolution, English aliases,
  equipment search/inspection/spawning, level/quality/legendary options,
  `ss`/`as`/`rs` inventory-backed scrolls, player/debug utilities, history,
  errors, recent spawns, repeat, aliases, help, config, completion, hooks,
  transplant/cohesive input, panel lifecycle and save-safety boundaries.

### Retained foundations

- Recognizable multiword equipment names with shared ID/current/English/alias
  resolution. English strings are read locally from the installed optional POT
  template, never distributed as a database. Ambiguous matches list choices;
  fuzzy suggestions cannot execute a spawn.
- `spawnitem` compatibility, named level/quality options, Roman numerals,
  trailing `legendary`, native summary and bounded canonical spawn history.
- `ss`/`setstat`, `as`/`addstat`, `rs`/`removestat`, plus scroll aliases, through
  the existing native inventory-backed scroll service. Setstat targets effective
  tiers and refuses inconsistent state/modifier-floor/temporary conflicts.
- Contextual suggestions for commands, subcommands, items and stats. Guarded
  Tab/Shift+Tab text completion; explicit Enter submission remains required.
- Quick heal/health/gold/position/stat controls, ID lookup, build views, catalog
  refresh, session history/errors/recent spawns, aliases and read-only repeat.
- No copied game textures or external art dependencies.
- Runner outcome classification rejects nonzero process exits as success;
  even a clean exit is explicitly unverified gameplay.

## Compatibility

All remaining registered commands keep their behavior. Old `workbench`,
`browse`, `pin`, `unpin` and multi-mode `uimode` invocations now fail with a
usage message instead of falling through silently. Old configuration files
use defaults for new fields and ignore retired keys. See
`docs/COMMAND_CHANGELOG_DC-M4.md` in the package.

## Verification and limits

- The command/catalog/scroll/history/layout/config suite passes (294 checks);
  exact scope is recorded in `DC-M5_PUBLIC_RELEASE_REPORT.md`. Two
  installed-template file contracts confirm Rusty Sword/Assault Shield identities.
- Release/Debug builds, assembly-reference/privacy/package audits, synthetic
  installer tests and negative authorization tests are recorded in the DC-M5 report.
- Earlier R5 manual gameplay demonstrated the foundation, not this candidate.
- GetText, native spawning by the new inputs, UI layering/font/focus/completion,
  lifecycle/resize behavior and native save/resume all require manual retesting.
- No one-key mutations, scripts, memory writes, save edits, progression unlocks,
  unsafe mutation equipping, teleportation or enemy spawning were added.
- Equipment group eligibility is not a guarantee of another mod's factory path.
- Native scroll persistence is partially director-observed (set/add survive
  menu/continue); removal, multi-color, repeats and invariants remain
  unverified. Run the full R01–R20,
  H01, controls A–F, M4-01…M4-06 and M4.4-01…M4.4-04 when separately authorized.

## Local artifacts

The current package is under `dist/DC-M4.6/DeveloperConsole-0.1.0-alpha.zip` with
a SHA-256 sidecar and fixed ZIP timestamps. Its only DLL is the console; no PDB,
framework/game binaries, saves or localization database are included. A new
disposable R14 fixture is staged outside the repository. All prior fixtures are
preserved.

No M4.6 launch or receipt has occurred. No remote, push, upload or release
publication was performed.

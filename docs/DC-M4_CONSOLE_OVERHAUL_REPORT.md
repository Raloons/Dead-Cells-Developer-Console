# DC-M4 — console overhaul engineering and design report

**Milestone identity:** DC-M4, following DC-M3A; no numbering conflict found.
**Starting HEAD:** `147e23b`. The three uncommitted R6 localization/test files
were preserved and incorporated as the starting point.
**Version:** 0.1.0-alpha (assembly/mod 0.1.0).
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Completion:** offline-complete candidate; native retest pending authorization.
**Publication:** not authorized; local artifacts/checkpoint only.

## 1. Investigation and scope

Reviewed the current README, commands, developer/install/troubleshooting guides,
native audit, runtime matrix and DC-M3A report; catalog/index, live adapter,
factory/forge, parser/registry, player/debug commands, config/history, rendering,
input hooks, tests, fixture/release/privacy/export scripts and local history.
The inherited R6 GetText candidate was not discarded. No private research
repository or expansion dataset was needed or copied.

R5 evidence was narrower than the old reports implied: it demonstrated console
operation, 645 catalog rows and a visible inventory-backed scroll grant, but
failed English-name resolution and did not prove save/resume. The native starting
sword ID is `StartSword`. The Haxe stack's root was a missing fixture save read;
the session ended at its time limit. Earlier runner `PASS` outputs ignored
nonzero process exits, so they cannot establish native success.

## 2. Catalog and natural equipment names

One snapshot/index now serves search, execution resolution, inspection, ID lookup
and contextual completion. Records keep canonical IDs, display/raw/English names,
registered aliases, group/category, policy eligibility and legendary metadata.
The resolver returns structured exact/normalized/partial/ambiguous/missing/
unavailable/not-spawnable outcomes. Exact ID wins; exact displayed name, English
name and registered alias follow; then unique normalized/prefix/complete-token
matches. Ambiguity lists choices. Typo/substring discovery never performs a
fuzzy spawn.

Normalization folds case, accents, whitespace and punctuation while preserving
Unicode letters/digits and original display strings. English aliases are read
from the installed optional `lang/main-en.pot`, bounded and filtered to item-name
entries keyed by canonical IDs. Offline installed-file contracts confirmed:

- `DashShield → Assault Shield`;
- `StartSword → Rusty Sword`;
- 598 usable item-name identities in the available template, with one
  conflicting identity excluded.

No extracted dictionary/template is distributed. Absent/conflicting English
entries fall back to current/raw names and IDs. Current-language GetText is
deferred until gameplay/explicit refresh; the crashing ItemTools helper is absent.
That native translation path still needs runtime validation.

The runtime sheet supplies mod/DLC rows. English entries cannot create rows or
grant spawnability. `catalog refresh` rereads registered data and replaces lookup
indexes; it does not reload other mods or guess custom factory implementations.
Equipment groups 0–6 are eligible except known internal IDs. Other records are
lookup-only; native factory rejection remains authoritative.

## 3. Commands and scroll semantics

Natural unquoted names work through `spawn`/`spawnitem`; quotes, native IDs,
named level/quality options, Roman numerals and existing quality aliases remain.
Trailing `legendary` is supported. Bare numeric suffixes are not guessed as
levels. Basic spawns report available native level/quality; forge requests keep
the existing read-back validation. Failed/ambiguous requests do not retry.
Canonical successful requests enter bounded session spawn history for explicit
`spawn last` and read-only `spawn recent`/`recent`.

`ss`/`setstat`/`setscroll`, `as`/`addstat`/`addscroll`, and
`rs`/`removestat`/`removescroll` share handlers. All mutations use the existing
DC-M2.1 `TryAdjustNativeScrolls` service: native items, ownership, recomputation,
health and HUD handling remain intact. Setstat targets the effective tier with
`target − current`, checks native counts/modifier floors and rejects active
temporary overrides or changes larger than 100 allocations. `player stat clear`
clears a selected temporary override. Setstat reports post-change mismatches
without retrying. Save/resume and native mutation scaling remain unverified.

Useful additions include heal/fullheal/hp/gold, pos/whereami/stats, equipped,
mods, itemid/id, catalog stats/refresh, build views, aliases, history, errors,
recent, read-only repeat, and debug last. Quick commands route to existing
handlers rather than duplicating game operations. There are 30 canonical
registered commands, up from 12, with safety metadata and documented aliases.
No command was removed. `gold` now has explicit additive bounded semantics.
See [command changelog](COMMAND_CHANGELOG_DC-M4.md) for compatibility details.

## 4. Four presentations and original artwork

- **Classic:** minimal full-width dark bottom console, default/fallback.
- **Fancy:** inset iron/brass framing, layered title/category strap and original
  geometric rule/rivet details. No copied textures or expensive shaders.
- **Compact:** right-aligned width/height-capped command overlay, few recent
  lines and at most two completions.
- **Inspector:** output plus a read-only build/catalog rail; narrow layouts
  collapse the rail. Data is a snapshot on open/command, not per-frame inspection.

`Presentation.Build` produces surfaces/captions/input placement for both h2d
rendering and SVG export. `DevPanel` uses pooled native nodes and cached colors,
hides unused nodes, detaches partial resources on failures and resets on console
replacement. Mode changes add no hooks; history is preserved. Managed failure
attempts Classic and then native-bar placement. Native focus, cleanup, fallback,
layering and resize behavior still require manual testing.

24 SVG state previews and four raster comparison images are under
`docs/previews/DC-M4/`, with a gallery. They use implemented layout/command code
and synthetic state. Raster images were inspected for distinct geometry,
readability and bounds. Fonts approximate the native font. These are explicitly
offline previews, not gameplay evidence. See [UI comparison](UI_MODES.md).

Configuration adds validated UiMode/WidthPercent/Opacity/TextScale/
SuggestionLimit/TabCompletion settings; old files retain defaults. Atomic file
writing is preserved. Session mode changes do not rewrite configuration.
Output/history capacities remain bounded; native command history stays available.

## 5. Completion, architecture and performance

Completion understands command/subcommand/item/stat/UI contexts and inserts
canonical text. Tab/Shift+Tab cycles; Enter must submit. Selection has no mutation
callback. Suggestions are bounded by config and available panel height. Option
text is not rewritten. Native editing interactions remain pending.

Normalized keys/tokens are built once per snapshot. No CDB or translation-file
enumeration occurs per keystroke. Input is sampled every six ticks, with fresh
sampling on Tab; plans update on meaningful changes. Hidden panels avoid redundant
visibility work. Inspector snapshots are not polled per-frame; scene size is
guardedly checked at a low cadence while open.

An initial synthetic 1,000-row sample exposed avoidable normalization/allocation
in search. Caching ID keys and using allocation-light rank loops reduced the
observed completion allocation from approximately 666 KB to **68 KB/request**.
A subsequent local run measured **2.86 ms** to construct a 1,000-row index and
**125.27 ms for 1,000 completion requests** (~0.125 ms/request). These are
environment-specific offline .NET measurements, not native frame/GPU timings.
The remaining bounded allocations occur on changed text, not every frame.

## 6. Validation

| Check | Result / scope |
|---|---|
| Regression/contract/layout suite | 328 checks, 0 failures |
| Installed English file contracts | Two additional checks, not native gameplay |
| Release and Debug builds | 0 warnings/errors |
| Layout matrix | Four modes × four resolutions × three text scales; crowded suggestion state also checked |
| Runner outcome classifier | Seven offline cases, including access violation, exit 1, timeout/remnants and production delta |
| Installer/uninstaller | Synthetic tree tests; existing/sibling content preserved, invalid targets refused |
| Authorization negative tests | Missing switch, synthetic and malformed receipts refused; no game process |
| Fixture admission | R8 VerifyOnly, 0 failures; external private denylist enabled |
| Assembly references | .NET/Microsoft.CSharp, GameProxy, ModCore/Common, HaxeProxy, HashlinkSharp and Serilog only |
| Asset/localization policy | Code-drawn geometry; no copied artwork or localization payload |
| Privacy/package | External denylist, source/export/package scans; deterministic ZIP rebuilt and compared |

Privacy scanning includes nonignored new files and SVG/HTML previews. Drive-path
matches require a path-token boundary so `ID:` followed by a C# newline escape
does not falsely match a drive prefix. Private terms remain external. The
binary-content audit found a compiler CodeView local build path in the initial
R7 candidate. Path mapping now gives source/debug records generic `/_/` names;
a PE debug-directory contract and ASCII/UTF-16 release binary scans enforce the
correction. R7 is preserved and the corrected binary is sealed separately as R8.

Local raw build/test/audit outputs are retained under `dist/` and `dist/DC-M4/`;
they are not included in public source/package artifacts. No tests were removed
to obtain a pass. Foundation mocks were corrected to use `StartSword` too.
Mock serialization tests are not proof of native persistence.

## 7. Artifacts and isolation

- Package: `dist/DC-M4/DeveloperConsole-0.1.0-alpha.zip` and SHA-256 sidecar.
- Source export: `dist/DC-M4/public-source/`, fresh single-commit history,
  no remote. The development checkpoint remains local.
- New disposable fixture: `<fixture root>/DC-M2-R8`; R1–R7 preserved.
- R8 DLL SHA-256: `A0DE9C7AC3C2AC1FB9D7B0255CB5A7E86F8F0581544F5CCC8F536D2A6BCD6FB8`.
- R8 PDB SHA-256: `C3B92CE3BE56AD30C28DBEAECA67973FB4AE92CF2DA9C1D201C9187218C7A485`.
- modinfo SHA-256: `3A84598DF4A7AD39F4D3786422A3F767298562DDB109A95B64E012196CADFEBF`.
- R6 DLL remains `232010BADB07FE7DB4B0896D8F365FD7F97FE142475A2E685F36FF0CE42B0214`.

R8 was copied from the isolated R6 tree, not installed into production. Its mod
package is replaced only in the new fixture; saves are empty, data/config/mod
roots are isolated, and the stock loader is pinned by manifest. Fixture-only
startup configuration is inherited; it does not confer launch authorization.
No new real receipt or session was created, and R6 authorization is not inherited.

## 8. Exact remaining native requirements

Execute the full R01–R20/H01 matrix, scroll controls A–F and M4-01…M4-06 in
`RUNTIME_VERIFICATION.md`, only with a fresh explicit fixture/hash-matching
authorization. All gameplay input is manual.

Priority checks: native boot/GetText; Assault Shield/Rusty Sword names in English
and a non-English game; actual drops/pickups and forge read-back; ambiguity and
unsupported mod rows; ss/as/rs count/tier/HP/HUD/mutation behavior; save/resume
twice without duplicate grants; four layouts at real resolutions; native Tab,
history, focus/input restoration; rapid switching, resize, transitions and fallback;
read-only repeat and exactly-one explicit spawn-last; uninstall/baseline.

No game was launched for DC-M4; no production saves/install, Steam Cloud or private
project was modified. Publication, UI approval and native testing remain with
the director. No remote configuration, push, release upload or Workshop action
was performed.

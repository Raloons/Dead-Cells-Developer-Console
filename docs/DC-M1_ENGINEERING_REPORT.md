# DC-M1 — engineering report

**Project:** Dead Cells Developer Console
**Repository:** local `Dead-Cells-Developer-Console` (no remote configured)
**Milestone:** DC-M1 — standalone extraction, modularization, public-release foundation
**Version:** 0.1.0-alpha
**Status:** engineering-complete to the offline boundary; native runtime matrix
pending an authorized disposable session.

---

## 1. What was delivered

A standalone DCCM mod that provides a developer console without any expansion
dependency. The console extends the native console and adds:

- `search` / `item info` / `item list` / `item categories`
- `spawn` with level, quality, Roman numerals, and legendary support
- `player` inspection and run-state testing utilities
- `debug` read-only diagnostics
- `help`, `suggest`, `config`, `version`, `uimode`, `consize`, `clear`
- bounded history, typo suggestions, and a custom output panel
- a safe configuration file and a disposable staging helper

The private source tree was not modified. The new project was created in a
separate directory with a fresh Git history.

## 2. Independence

- `DeveloperConsole.csproj` references only `DeadCellsCoreModding.MDK` 1.0.1.
- `modinfo.json` declares no dependencies.
- `DeveloperConsole.deps.json` contains only the project and the MDK package.
- The compiled assembly's `AssemblyRef` table contains only `System.*`,
  `GameProxy`, `ModCore`, `ModCore.Common`, `HaxeProxy`, `HashlinkSharp`, and
  `Serilog`. No private or expansion assembly is referenced.
- The equipment catalog is built at runtime from the live `item` sheet.
- No private assembly, namespace, generated catalog, name database, save,
  fixture, or content package is present.
- No code path reaches into the private research repository.

`docs/EXTRACTION_MAP.md` records the reusable, adapted, replaced, and excluded
components.

## 3. Architecture

- **Pure layer:** tokenizer/registry, catalog model and ranking, forge parsing,
  layout math, history, configuration. No game dependencies.
- **Seam:** `IGameContext` and `IHeroFacade`; commands are testable with mocks.
- **Live adapter:** `LiveGameContext` and `DeveloperConsoleMod`; native hooks,
  catalog enumeration, factory/drop, forge, diagnostics, UI sync.
- **UI:** `DevPanel` + `UiLayout`; native input field transplanted into the
  custom panel by default, with a cohesive mode available.

The native input lock is preserved by keeping the native background alive as a
thin strip. Unknown commands fall through to the native dispatcher, preserving
built-in commands.

## 4. Verification

| Gate | Result |
|---|---|
| Release build | 0 warnings, 0 errors |
| Debug build | 0 warnings, 0 errors |
| Offline acceptance suite | 111 checks, 0 failures |
| Package references | MDK 1.0.1 only |
| Private term/path audit | PASS (31 tracked text files) |
| Local Git checkpoint | clean root commit + follow-up commits |
| Public push | not performed |

The offline suite covers tokenization, registration, aliases, required
arguments, error paths, help, suggestions, configuration parsing/clamping,
catalog ranking and ambiguity, search filters, item inspection, spawning,
level/quality parsing, player commands, debug commands, history, layout, and
unavailable-game-context behavior.

## 5. Fixture

`fixture/Stage-Fixture.ps1` stages the Release build into a target disposable
game tree, verifies the file set, and writes a SHA-256 manifest. It never
launches the game and never touches saves.

A local Git-ignored staged copy exists at
`fixture/staged/DeveloperConsole/` with `DeveloperConsole.dll`,
`DeveloperConsole.pdb`, `modinfo.json`, and `staged-manifest.json`.

Full game-tree staging and the 20-point live matrix remain pending because a
disposable game copy and explicit live-session authorization are required.
`docs/RUNTIME_VERIFICATION.md` records the matrix and the honest status.

## 6. Privacy and publication

- `docs/PRIVACY_AUDIT.md` records the audit scope, method, and pass result.
- `tools/Privacy-Audit.ps1` reproduces the scan.
- No private content, game asset, raw database, save, credential, or
  machine-specific path is tracked.
- The repository has no remote and nothing was pushed.
- Publication still requires explicit user authorization and a fresh audit of
  the exact commit.

## 7. Safety posture

- The console is passive while closed.
- Commands are gated to active gameplay.
- Run-state commands are marked in the documentation and return a warning.
- No permanent-unlock, progression, or Cloud-write command exists in v0.1.0.
- No save writes are performed by the mod.
- Native calls are guarded; failures become command errors or `unavailable`
  values.
- Hook installation is one-time and preserves original behavior.
- History and search output are bounded.

## 8. Known limitations

- Native runtime verification is not complete; see the matrix.
- No live Tab completion (native input owns keystrokes); `suggest` and typo
  hints are the supported completion path.
- Enemy spawning and mutation equip/remove are out of scope for v0.1.0.
- Legendary support depends on the item row's legendary form.
- Display names depend on the game's native localization helper; fallback
  names may be localized row names or ids.
- The console targets Dead Cells v35 / DCCM 35.13.8. Other versions are not
  claimed.

## 9. Next gate

Run the 20-point matrix in a disposable fixture, record the DCCM log and
results, then decide whether any native boundary needs adjustment before
publication. Do not push before that pass and explicit authorization.

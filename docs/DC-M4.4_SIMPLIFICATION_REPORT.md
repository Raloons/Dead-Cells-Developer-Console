# DC-M4.4 â€” Workbench removal & simple console restoration

**Milestone:** DC-M4.4, following DC-M4.3 (`1ccb8ea`).
**Version:** 0.1.0-alpha; assembly/mod remains 0.1.0.
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Status:** offline-complete; native verification pending. No game launched for M4.4.

## 1. Directive

Per director decision, the desktop-style Workbench is removed entirely. The
console returns to a traditional overlay: dark background, `Console` title,
scrollable output, native input row, history, lightweight completion,
`consize` sizing. No redesign of the replacement; no new typography research.

## 2. Removed UI components

| Deleted file | What it was |
|---|---|
| `WorkbenchPresentation.cs` | App-shell geometry, 8 sections, hit regions |
| `WorkbenchState.cs` | Section/search/selection/pins/draft state |
| `WorkbenchUiCommands.cs` | `workbench`, `browse`, `pin`, `unpin` |
| `UiText.cs` / `GdiFontAtlas.cs` / `UiFonts.cs` | GDI atlas + font-face cache |
| `UiLayout.cs` | Unused panel geometry helper |
| `tests/.../WorkbenchTests.cs` | Workbench contracts |
| `tests/.../TypographyTests.cs` | Atlas/identity contracts |
| `tests/.../SamplerHarness.cs` | Sampling harness |
| `tests/.../PreviewWriter.cs` | SVG/PNG preview generators |

`WorkbenchCommands.cs` (essential shortcuts, despite its name) was renamed to
`ShortcutCommands.cs` with no behavior change.

Retired from `DeveloperConsoleMod`: section/mouse/Alt+1â€¦8 navigation
(`TickWorkbench`, `RunWorkbenchAction`, cursor/client `user32` imports),
Inspector refresh, Problems scroll branches, display-chain diagnostic.
`ConsoleMode` is transplant/cohesive placement only; `Presentation` is one
classic plan; `DevPanel` uses pooled `HtmlText` with the native `font12`
bitmap font; `ConsoleConfig` drops `UiMode`/`Accent`/`Density`;
`CompletionEngine` drops mode/section contexts.

## 3. Retired commands and config

- `workbench` (`desk`), `browse`, `pin`, `unpin`: unregistered. Unknown-name
  invocations now return "unknown command â€¦ use help to list commands" (or a
  typo hint) instead of falling through silently.
- `uimode` (`ui`, `theme`): `[transplant|cohesive]` only; other arguments
  return usage. Bare `uimode` still reports placement.
- Retired config keys in old `console-config.json` files are ignored by the
  JSON reader (verified by test); numbers still clamp as before.

## 4. Preserved functionality

Every gameplay system is untouched: tokenizer/registry, catalog model +
natural-name/English/alias resolution, search/item/spawn + forge options,
`ss`/`as`/`rs` inventory-backed scrolls + `player scroll/stat` semantics,
player/debug command groups, shortcuts (`heal`, `hp`, `gold`, `pos`, `stats`,
`equipped`, `mods`, `itemid`, `catalog`, `build`, `history`, `errors`,
`recent`, `aliases`, `repeat`), help/suggest/complete, config/consize/clear/
version, bounded journals, hooks, transplant/cohesive input, panel lifecycle,
atomic config writes, save-safety boundaries.

## 5. Validation

- Suite: **291 checks, 0 failures** (command/catalog/scroll/history/
  layout/config/completion contracts; UI-only tests removed, none added for
  deleted features).
- Release + Debug builds, zero warnings/errors.
- Privacy audit, deterministic packaging (rebuilt identically, checksums
  verified), R13 fixture admission VerifyOnly, auth-gate/installer/outcome
  negatives: pass (see `dist/DC-M4.4/` logs).
- R13 DLL `E7B1DD8777D2A585D245815DD4D706247EE8321535CB18C760BB98B823881765`
  (214016 bytes) identical across build/fixture/package; ZIP sealed via the
  adjacent `.sha256` sidecar (see `dist/DC-M4.4/seal.log` for the sealed hash).
- Candidate: `dist/DC-M4.4/DeveloperConsole-0.1.0-alpha.zip` (+`.sha256`).
  Prior fixtures, evidence and ZIPs preserved untouched.
- Public source export local-only; no remote/push/publication.

## 6. Package size comparison

| Artifact | DC-M4.3 (R12) | DC-M4.4 (R13) |
|---|---|---|
| `DeveloperConsole.dll` | 300032 bytes | 214016 bytes |
| Release ZIP | 2569607 bytes | 147048 bytes |
| Packaged files (SHA256SUMS) | 202 | 26 |

Previews and archived workbench reports are excluded from the package;
historical reports/galleries remain in git as archives.

## 7. Remaining native verification

Full R01â€“R20/H01 and M4-01â€¦M4-06 plus new M4.4-01â€¦M4.4-04 (open/close,
output/input, lifecycle, inherited behavior) per
[RUNTIME_VERIFICATION](RUNTIME_VERIFICATION.md). M4.1-01â€¦M4.1-08 retired.
No M4.4 launch or receipt exists; prior authorizations do not carry forward.
No release authorized.

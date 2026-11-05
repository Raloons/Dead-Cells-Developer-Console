# Troubleshooting

## The console does not open

- The console opens only during **active gameplay**, not on the title screen,
  menus, loading screens, or the training-room lobby before a hero exists.
- The game window must be focused. The toggle is ignored while another window
  is foreground.
- Check that `DeveloperConsole.dll` and `modinfo.json` are directly inside
  `<Dead Cells>/coremod/mods/DeveloperConsole/`.
- Launch with `DeadCellsModding.exe`, not the vanilla executable.
- Open the DCCM log and look for:

  ```text
  DeveloperConsole initialized
  DeveloperConsole console captured (starts hidden)
  ```

- If the toggle key was changed in `console-config.json`, use that key. Valid
  values are `=`, `` ` ``, `insert`, and `F1`–`F12`.

## DCCM is not loading the mod

- Confirm the DCCM version is 35.13.8 and the game is the v35 line.
- Confirm the mod folder name and `modinfo.json` are correct.
- Check the DCCM log for a load or dependency error.
- The mod has no dependencies; a missing-dependency error points at another
  installed mod or a corrupted `modinfo.json`.

## `search` returns no results

- The catalog is built after the CDB loads. If the console was opened before a
  run existed, start a run and reopen it.
- `debug catalog` should show a non-zero entry count and a source such as
  `live CDB item.all[]`.
- Try the actual internal id: `search StartSword`. `RustySword` is not the
  native starting-sword ID; normalized English input can still match Rusty Sword.
- Use `catalog refresh` after late item registration. `catalog stats` reports
  English alias coverage. See [equipment names](EQUIPMENT_NAMES.md).
- Word order does not matter for token matches: `search sword rusty` also
  finds `Rusty Sword`.
- If a result is empty, read the `did you mean` line; it is a suggestion only
  and never auto-selects an item.
- If another mod's item is expected, confirm that mod is loaded and its rows
  are registered in the live CDB. `debug catalog` counts them, but the console
  cannot invent a row that the game did not load.

## An item cannot be spawned

- `item info <id>` reports the reason. Common cases:
  - the row is internal/admin;
  - the row is a mutation, talisman, boss, or other non-equipment group;
  - the item id is not in the live catalog;
  - the native factory rejected the row.
- Only equipment groups 0–6 are spawnable in v0.1.0.
- Legendary quality is refused when the row has no legendary form.
- A level or quality read-back mismatch aborts the drop on purpose; the error
  states what was requested and what the game returned.

## Scroll changes revert after continuing a run

- `player stat` is a **temporary session override**. It is not saved and is
  expected to disappear after returning to the menu and continuing.
- `player scroll` allocates native `BrutalityUp`/`TacticUp`/`SurvivalUp`
  inventory items, the same data the game serializes in `inventory.items`.
  Use it for a scroll grant intended to persist with the run.
- Check `player status` or `player stats` for the native scroll counts before
  and after the operation. The effective tier may also include equipped
  `ColorlessUp`/stat affixes or a temporary `player stat` override.
- If `player scroll` reports a failure, no native scroll count should change;
  an over-removal is refused before mutation.
- Live save/resume confirmation is still pending; see
  `docs/SCROLL_PERSISTENCE_INVESTIGATION.md`.

## A command is rejected for length, quotes, or too many arguments

- Commands are capped at 512 characters and 64 arguments.
- Quotes group spaces only when they begin a token. Close both quote marks;
  escaped quotes are not supported.
- These limits reject the line before dispatch and do not execute anything.

## Configuration changes do not stick

- The file is `console-config.json` in the mod folder; use `config path`.
- Invalid JSON or out-of-range values fall back to defaults and are reported
  in the DCCM log.
- The console writes a sibling `.tmp` file and replaces the canonical file
  atomically, so a failed write should leave the previous config intact. If
  the mod folder is read-only, the defaults are used for the session and the
  failure is logged.

## A command only works in gameplay

This is intentional. The console is a gameplay testing tool. The active
hero/level gate prevents commands from running on menus and prevents native
console state from leaking across transitions.

## The console opened but typing moves the player

That should not happen in the shipped layout: the native input lock is kept
alive by leaving the native background visible as a thin strip. If it does,
remove the mod and report the log lines around
`DeveloperConsole transplant visuals`.

## The panel is the wrong size

There is one console overlay. `consize [10–80]` adjusts its height; the
`WidthPercent`, `Opacity` and `TextScale` config values adjust width, background
and scale (reload afterwards with `config reload`). Every new layout still needs
native testing.

```text
consize 40
```

or edit `panelPercent` in `console-config.json` (10–80) and run
`config reload`.

## `config reload` does not change the toggle key

The file must be `console-config.json` in the mod folder. Invalid keys fall
back to `=` and are reported in the DCCM log. Use `config path` to confirm the
location.

## Version incompatibility

The console targets Dead Cells v35 and DCCM 35.13.8. A different game or DCCM
version may load but can fail at any native boundary. Use the versions listed
in the README, or rebuild against the matching MDK reference assemblies.

## Conflicts with other mods

- The console reads the live equipment sheet, so items registered by other
  mods are discoverable.
- Two mods hooking the same native function can conflict. If commands stop
  dispatching after installing another mod, check the DCCM log for hook
  failures and test with only this mod installed.
- The console does not patch binaries or write shared files.

## Debugging checklist

```text
version
debug catalog
debug snapshot
debug mods
config path
```

Include those outputs and the relevant DCCM log lines when reporting an issue.

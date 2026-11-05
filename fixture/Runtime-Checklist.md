# Authorized runtime checklist

DC-M4 remains launch-prohibited. Use this only after separate explicit
authorization for the exact new fixture/hash. R6 and its receipt are not a
substitute. Testing is manual: do not send automated gameplay input.

Use this with `fixture/Invoke-RuntimeSession.ps1` after a real
`dc-m2-authorization/1` receipt has been issued. The launcher starts exactly
one bounded session; run as many of these checks as the window allows.

## Before launch

- [ ] Fixture admission passes in `VerifyOnly` mode.
- [ ] A real receipt matches the fixture root and current `DeveloperConsole.dll`.
- [ ] No Steam or game process is running.
- [ ] Production saves are backed up.
- [ ] The session window is 30–300 seconds.

## Boot and console

- [ ] Game reaches a playable state.
- [ ] DCCM log contains `DeveloperConsole initialized`.
- [ ] DCCM log contains `console captured (starts hidden)`.
- [ ] DCCM log contains `live item catalog linked` after CDB load.

## Opening, closing, and input

- [ ] Press `=` in a run: the panel opens.
- [ ] Type a command and press Enter: output appears.
- [ ] Press `=` on an empty line: the panel closes.
- [ ] Reopen, press `Escape`: the panel closes.
- [ ] Move, jump, attack, and open the pause menu after closing.
- [ ] Repeat open/close ten times: no duplicate panel or residual input.
- [ ] `PageUp`/`PageDown` scroll output while open.

## Discovery and catalog

```text
version
help
help spawn
help player
suggest spa
suggest rust
debug catalog
debug mods
search rusty
search sword --type weapon
search --type shield --limit 5
item info StartSword
item info assault shield
id rusty sword
catalog stats
catalog refresh
item list --type weapon
item categories
```

- [ ] Output is readable and bounded.
- [ ] `debug catalog` shows a live CDB source and a non-zero entry count.
- [ ] Known stock items resolve; unknown items fail clearly.
- [ ] Non-equipment rows are not offered as ordinary drops.

## Spawning

```text
spawn StartSword
spawn assault shield
spawnitem assault shield level=10 quality=L
spawn "Quick Bow"
spawn Cudgel
spawn FireBomb
spawn StartSword level=4
spawn StartSword level=IV quality=S
spawn StartSword quality=L
spawn Nope
spawn StartSword level=0
spawn StartSword quality=X
spawn
```

- [ ] Representative melee, ranged, shield, and skill drops appear.
- [ ] Each drop can be picked up and behaves as a native item.
- [ ] Level and quality match the request; mismatches fail.
- [ ] A row without a legendary form refuses `quality=L`.
- [ ] Invalid arguments fail without a crash or unrelated state change.

## Player and debug

Also test `ss tactics <current+2>`, `as brutality 2`, `rs brutality 2`,
insufficient removal, modifier-floor refusal and temporary-override refusal.
Clear an override with `player stat clear brutality` before setstat reconciliation.
Do not infer save/resume success from a visible grant.

```text
player stats
player position
player status
player mutations
player effects
player heal 25
player health 50
player health 500
player gold 50
player stat brutality 10
player scroll brutality 2
player scroll brutality -2
player scroll brutality -100
debug snapshot
debug player
debug level
debug equipment
debug status
```

- [ ] Values match the visible/native state.
- [ ] Unavailable fields report `unavailable`, not invented values.
- [ ] Run-state changes are visible and bounded.
- [ ] Below-floor or invalid values fail.

## Errors and repetition

```text
nope
spwan
search
search --type potato
item info
debug nope
save
```

- [ ] Unknown commands produce clear errors or fall through to native.
- [ ] `save` still works through the native dispatcher (if authorized).
- [ ] Repeated `debug snapshot` submissions remain stable.

## Lifecycle

Switch through `ui classic`, `ui fancy`, `ui compact`, `ui inspector`,
`uimode cohesive`. Check Tab/Shift+Tab, Enter, Up/Down, Page Up/Down,
input lock/restoration, output fit, config reload and resolution changes.
Use the full M4-01…M4-06 matrix in `docs/RUNTIME_VERIFICATION.md`.

- [ ] Transition to another level with the console closed.
- [ ] Reopen and run `player status` and `debug catalog`.
- [ ] No stale hero, duplicate UI, or exception appears.
- [ ] If separately authorized: save, return to title, resume, reopen the console.

## Uninstall

- [ ] Close the game normally.
- [ ] Remove `<fixture root>/dccm/mods/DeveloperConsole`.
- [ ] Clear fixture config/logs if testing a clean baseline.
- [ ] Relaunch; verify the console is absent and no errors appear.
- [ ] Verify production saves and installation are unchanged.

## Record

For each check, record the status category, command, observation, log
excerpt, screenshot path, and any uncertainty in
`docs/RUNTIME_VERIFICATION.md` and the session `evidence.json`.

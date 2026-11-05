# GitHub release draft

**Title:** Dead Cells Developer Console v0.1.0 Alpha

**Status:** Draft only. Not published. Do not create a remote or upload assets
until the director authorizes publication for this exact destination and these
exact artifacts.

---

## Developer Console v0.1.0 Alpha

A standalone developer console for Dead Cells Core Modding (DCCM). It extends
the game's native console with equipment search, item inspection, native
equipment spawning, player inspection, mutation management, and read-only
developer diagnostics. It does not require any expansion mod.

### Highlights

- `search` with id/name/category filters, order-independent token matching,
  and bounded typo hints.
- `item info`, `item list`, `item categories`, and concise green `itemid`
  over the live runtime catalog.
- `spawn` with level, Roman numerals, quality, and legendary support where the
  item row supports it; drops land at the hero's feet.
- `player` stats, health, heal, gold, temporary overrides, and inventory-backed
  scroll allocation through `ss`/`as`/`rs`.
- `mutation list/info/equipped/equip/remove` with native verification and
  rollback (effect activation pending live proof).
- English/current-language natural names, multiword spawns, contextual
  completion, scrollable output, paged help, command history, read-only repeat.
- `debug` read-only snapshots for player, level, equipment, catalog, and
  loaded mods.
- No private expansion dependency; the catalog is built from the live game
  item sheet.

### Requirements

- Dead Cells v35.
- DCCM 35.13.8 with `DeadCellsModding.exe`.
- No other mod is required. Third-party item rows may appear in the live
  catalog if they are registered by another loaded mod.

### Installation

1. Download `DeveloperConsole-0.1.0-alpha.zip`.
2. Copy its `DeveloperConsole` folder into `<Dead Cells>/coremod/mods/`.
3. Launch through `DeadCellsModding.exe`.
4. Enter a run and press `=`.

Full instructions: `docs/INSTALL.md`.

### Quick start

```text
help
search rusty
item info StartSword
spawn assault shield level=4 quality=S
player status
mutation list
```

### Verify the download

Use `dist/DC-M5/DeveloperConsole-0.1.0-alpha.zip.sha256` for the exact candidate.

The archive contains the `DeveloperConsole` mod folder and public documentation.
It does not contain game binaries, game databases, saves, or DCCM distributions.

### Verification status

- Source builds: Release and Debug, 0 warnings/errors.
- Offline suite: 326 checks pass, plus 2 installed-template contracts.
- Privacy audit and clean standalone assembly references pass.
- R16 disposable fixture admitted; boot/init/open-close observed live.
- **Command effects, mutation activation, level transitions, and full
  save/resume remain pending.** Scroll set/add persistence and equipment
  spawning are director-observed; the matrix records exact scope. This alpha
  does not claim checks beyond its evidence.

### Safety

This is a testing tool. Back up saves. State-changing commands affect the
current run. `player scroll` uses native inventory scroll allocation; `player
stat` is a temporary session override and is not saved. The mod does not write
save files, modify permanent unlocks, or access Steam Cloud directly.

### Known limitations

- Native runtime verification is not complete (see above).
- Tab completion is implemented but native input interaction remains unverified;
  `suggest` provides a text-only fallback.
- Quest/key/currency rows stay refused; there is no generic pickup spawner.
- Legendary support depends on the item row.
- Display names use current-language/installed-English/raw-ID fallbacks; English
  availability depends on the installed template. No database is bundled.
- Third-party item spawnability depends on native factory support.

### Documentation

- Installation: `docs/INSTALL.md`
- Commands: `docs/COMMANDS.md`
- Troubleshooting: `docs/TROUBLESHOOTING.md`
- Runtime matrix: `docs/RUNTIME_VERIFICATION.md`
- Privacy: `docs/PRIVACY_AUDIT.md`

### Disclaimer

Unofficial fan-created tool. Not affiliated with or endorsed by Motion Twin,
Evil Empire, or the DCCM developers. You must own a legitimate copy of Dead
Cells. No proprietary game assets or raw game databases are distributed.

# Dead Cells Developer Console

A standalone, MIT-licensed developer console for **Dead Cells v35 / DCCM
35.13.8**. Version **0.1.0-alpha**.

**Release candidate prepared; public release not yet authorized.** The console
is offline-complete and has been exercised in authorized disposable sessions;
remaining native checks are listed in [runtime verification](docs/RUNTIME_VERIFICATION.md).

## Features

- Spawn equipment by recognizable name: `spawn assault shield`.
- Canonical ID, English-alias and current-language lookup with ambiguity
  handling; fuzzy suggestions never execute.
- Native inventory-backed scroll commands (`ss`, `as`, `rs`).
- Mutation inspection and native equip/remove (`mutation equip vengeance`).
- Player utilities (health, heal, gold, position, stats), debug diagnostics,
  catalog search and inspection.
- Simple dark overlay titled **Console**: scrollable output, native input row,
  command history, lightweight Tab completion, concise paged help.

## Requirements

- Dead Cells v35 (Steam).
- DCCM framework 35.13.8 with a working `DeadCellsModding.exe` entry point.
- .NET SDK 10.0 only for building from source.

## Installation

1. Download `DeveloperConsole-0.1.0-alpha.zip` from the release and check its
   SHA-256 against the published `.sha256` file.
2. Close the game.
3. Copy the `DeveloperConsole` folder from the ZIP into
   `<Dead Cells>/coremod/mods/` so that `modinfo.json` sits directly inside
   `DeveloperConsole`.
4. Start the game with `DeadCellsModding.exe` (not the vanilla executable).

Removal: delete `<Dead Cells>/coremod/mods/DeveloperConsole/`. See
[installation](docs/INSTALL.md).

## How to open the console

Enter an active run and press `=` (configurable). The console is intentionally
unavailable on menus. Page Up/Page Down (or the mouse wheel over the input
row) scrolls output; Up/Down recalls command history; `consize` adjusts
height; `clear` clears output.

## Quick start

```text
help
search assault shield
spawn assault shield
itemid rusty sword
ss tactics 20
as brutality 5
mutation equip vengeance
```

## Commands

Full reference: [COMMANDS.md](docs/COMMANDS.md). Highlights:

```text
spawn assault shield
spawnitem symmetrical lance level=10 quality=L
spawn cursed sword legendary
id rusty sword
search sword --type melee
ss tactics 20
as brutality 5
rs survival 3
heal
gold 10000
build summary
```

English names come from the installed game's optional `lang/main-en.pot`, read
locally. No extracted game database is bundled.

`ss` sets an effective tier using the difference from the current tier;
`as` adds native scroll allocations; `rs` removes them.

## Compatibility

Verified targets: Dead Cells v35, DCCM 35.13.8. Other game or framework
versions are not claimed to work. The console is standalone: it needs no other
mod and bundles no game content.

## Safety

Development commands modify the active run and the game may save that state.
**Back up your saves before experimenting.** `player stat` is a temporary
session override. Scroll set/add have been observed to survive menu/continue;
removal, repeats and edge cases are still pending verification. Mutation
effect activation is pending live verification. The console never touches
Steam Cloud, permanent unlocks, or save files directly.

## Building from source

```text
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false
dotnet run --project tests/DeveloperConsole.Tests -c Release
```

Deployable output:
`src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole/`.
Builds never install automatically. The mod creates `console-config.json` in
its own folder; edit it and run `config reload`.

```json
{
  "ToggleKey": "=",
  "PanelPercent": 40,
  "WidthPercent": 100,
  "Opacity": 0.88,
  "TextScale": 1,
  "HistoryLimit": 200,
  "SearchLimit": 15,
  "SuggestionLimit": 5,
  "Suggestions": true,
  "TabCompletion": true,
  "VerboseLog": false
}
```

## Documentation

- [Command reference](docs/COMMANDS.md) and [command changelog](docs/COMMAND_CHANGELOG_DC-M4.md)
- [Name resolution and English aliases](docs/EQUIPMENT_NAMES.md)
- [Autocomplete](docs/AUTOCOMPLETE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)
- [Developer architecture](docs/DEVELOPER.md) and [native API audit](docs/NATIVE_API_AUDIT.md)
- [Release notes](docs/RELEASE_NOTES_v0.1.0-alpha.md) and [runtime matrix](docs/RUNTIME_VERIFICATION.md)
- [Steam launch & Workshop compatibility](docs/STEAM_LAUNCH_COMPATIBILITY.md) — Workshop publication held pending compatibility + authorization
- [DC-M5 public release report](docs/DC-M5_PUBLIC_RELEASE_REPORT.md)

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md). Do not upload private save files or
sensitive logs with bug reports.

## License and disclaimer

License: [MIT](LICENSE). See [third-party notices](THIRD_PARTY_NOTICES.md).

Unofficial fan-created tool; not affiliated with Motion Twin, Evil Empire, or
the DCCM developers. No game binaries, localization databases, private
catalogs or copied game artwork are distributed.

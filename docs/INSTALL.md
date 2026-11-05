# Installation and removal

## Requirements

- Dead Cells v35 (Steam).
- Dead Cells Core Modding (DCCM) 35.13.8 installed with
  `DeadCellsModding.exe`.
- .NET SDK 10.0 for building from source.

The mod targets the v35 API used by the installed DCCM reference assemblies.
Other game versions may work, but are not claimed for v0.1.0-alpha.

## Build

From the repository root:

```text
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false
```

The build deliberately does not install itself. The deployable folder is:

```text
src/DeveloperConsole/bin/Release/net10.0/output/DeveloperConsole/
```

It contains `DeveloperConsole.dll`, `DeveloperConsole.pdb`, and
`modinfo.json`.

## Manual installation

1. Back up your saves. The safest approach is to copy your entire Dead Cells
   save folder somewhere outside the game directory.
2. Close the game and `DeadCellsModding.exe`.
3. Copy the deployable folder to:

   ```text
   <Dead Cells>/coremod/mods/DeveloperConsole/
   ```

   The final path must contain `modinfo.json` directly inside `DeveloperConsole`.
   The release ZIP already has this layout: put its `DeveloperConsole` folder
   into `coremod/mods/`.
4. Start the game with `DeadCellsModding.exe` (not the vanilla executable).
5. Enter a run and press `=`.

On first launch the mod writes `console-config.json` into its own mod folder.
It does not write anywhere else.

Optional settings include `WidthPercent`, `Opacity`, `TextScale`,
`SuggestionLimit` and `TabCompletion`. Existing files retain defaults for
missing keys; retired Workbench keys are ignored. Edit the file and use
`config reload`.

English aliases are read from the installed game's optional `lang/main-en.pot`.
Do not copy localization files into the mod folder. IDs/current names remain
available if the template is absent; see [equipment names](EQUIPMENT_NAMES.md).

If the game does not load, remove the `DeveloperConsole` folder first and check
the DCCM log. Do not leave a partially copied mod folder in place. See
`docs/TROUBLESHOOTING.md`.

## Scripted staging into a disposable tree

In the source repository, `fixture/Stage-Fixture.ps1` copies the Release build
into a target game tree and verifies the copied files. It is developer tooling
and is not included in the release zip. It never launches the game and does
not touch saves:

```text
powershell -ExecutionPolicy Bypass -File fixture/Stage-Fixture.ps1 -DeadCellsRoot "<path to disposable game tree>"
```

Use a disposable copy of the game tree for testing. Do not point it at your
normal installation unless you intend to install the mod there.

## Verifying the installation

- The DCCM log should contain `DeveloperConsole initialized`.
- After the CDB loads, it should contain `DeveloperConsole: live item catalog linked`.
- In a run, `=` opens the overlay.
- `version` prints the console version and target.
- `debug catalog` shows the live provider and entry count.
- `search rusty` returns stock equipment.

## Uninstallation

1. Close the game.
2. Delete `<Dead Cells>/coremod/mods/DeveloperConsole/`.
3. Optionally delete the `console-config.json` file that was created there.
4. Launch through `DeadCellsModding.exe` and verify the console no longer opens.

No save files are modified by installing or removing the mod.

## Backup checklist

Before testing a new build:

- Back up the save folder.
- Note the DCCM version.
- Keep a copy of the last known-good mod folder.
- Prefer a disposable save slot for destructive testing.
- Do not test against a save whose progression you cannot restore.

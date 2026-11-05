# Developer guide

## DC-M4.4 simple console architecture

The Workbench was removed in DC-M4.4; the console is a single traditional
overlay (`Presentation` classic plan + pooled `DevPanel` rectangles and
game-font labels + transplanted native input). The command implementations,
name-resolution index and native scroll service are preserved; only UI-only
commands (`workbench`, `browse`, `pin`, `unpin`) were retired.

- `ShortcutCommands`: thin routes to existing handlers for quick controls and
  build/diagnostic views. Alias registration rejects internal collisions too.
- `Presentation`: pure rectangle/caption plan consumed by native h2d pools.
  `DevPanel` owns/detaches native presentation nodes; console replacement
  resets ownership.
- `ConsoleMode`: transplant/cohesive native input placement only.
- Config reload validates new settings, preserves history on capacity changes,
  resizes and reapplies native input placement. The old transplant/cohesive
  modes remain compatible. Native UI lifecycle/focus and save/resume still
  require manual verification.

Offline contracts cover command parsing, catalog behavior, scrolls, history,
layout, configuration and failure paths. Historical notes below retain their
original milestone context.

## DC-M4 architecture update

The original three-layer seam remains. The overhaul adds:

- `EnglishNames`: bounded reader for an optional installed game POT template;
  no extracted names are stored in the source or package.
- `CatalogItem.EnglishName`/`Aliases`, structured `ItemResolution`, and cached
  normalized name/token entries. One snapshot index is shared by the live adapter,
  search, spawn, inspection, ID lookup and completion.
- `ScrollPlan`/`StatNames`: pure effective-tier reconciliation and shortcut
  validation. All scroll mutations still use `TryAdjustNativeScrolls` below.
- `ShortcutCommands`: thin routes to existing handlers for quick controls and
  build/diagnostic views. Alias registration rejects internal collisions too.
- `SessionJournal`: bounded process-local command/error/spawn strings and a
  strictly read-only repeat target. It stores no native objects or save data.
- `CompletionEngine`/`CompletionSelection`: pure context discovery and text
  replacement selection; no execution callback.
- `Presentation`: pure rectangle/caption plan consumed by native h2d pools.
  `DevPanel` owns/detaches native presentation nodes; console replacement
  resets ownership.

The current-language GetText candidate is deferred until active gameplay; CDB
boot builds English/raw-name metadata without native localization. Catalog
enrichment occurs on first open and explicit refresh, not on every frame.
Input suggestions sample every six ticks and refresh immediately on Tab edges.

Mode commands are session-only. Config reload validates new settings, preserves
history on capacity changes, resizes and reapplies native input placement. The
old transplant/cohesive modes remain compatible. Native UI lifecycle/focus and
save/resume still require manual verification.

See [name resolution](EQUIPMENT_NAMES.md), [UI modes](UI_MODES.md) and the
[DC-M4 report](DC-M4_CONSOLE_OVERHAUL_REPORT.md) for current evidence. The
following foundation notes remain useful but do not supersede those statuses.

## Design

The console is a code-only DCCM mod. It extends the game's native console
instead of replacing its input, focus, history recall, or error display.

The three layers are deliberately separate:

1. **Pure command layer** — tokenizer, registry, catalog model, forge parsing,
   layout math, history, and configuration. No game dependencies; covered by
   the offline test harness.
2. **Game seam** — `IGameContext` / `IHeroFacade`. Commands talk only to this
   interface, so tests can run without Haxe.
3. **Live adapter** — `LiveGameContext` and `DeveloperConsoleMod`. These use
   DCCM/Haxe hooks and native game objects. Every native read/write is guarded;
   failures surface as command errors or `unavailable` values.

## Project layout

```text
src/DeveloperConsole/
  DeveloperConsoleMod.cs   bootstrap, hooks, toggle, panel sync, dispatch
  LiveGameContext.cs       live Haxe adapter, catalog provider, native spawn
  DevPanel.cs              custom h2d output panel
  GameContext.cs           seam interfaces and command result types
  CommandRegistry.cs       tokenizer, registration, execution, suggestions
  Catalog.cs               pure catalog model, ranking, resolution
  CoreCommands.cs          help/config/version/consize/clear
  CatalogCommands.cs       search/item/spawn
  PlayerCommands.cs        player group
  MutationCommands.cs      mutation list/info/equipped/equip/remove
  DebugCommands.cs         read-only diagnostics
  EquipmentForge.cs        level/quality parsing and read-back checks
  ConsoleConfig.cs         config parsing, clamping, key binding
  HistoryBuffer.cs         bounded output history
  ConsoleHistory.cs        native history bridge
  ConsoleMode.cs           transplant/cohesive placement state
  ConsoleSizing.cs         panel sizing math
  StatBonus.cs             stat re-apply after native tier recalculation
  assets/devconsole.txt    non-empty PackAssets marker (DCCM pak target)

tests/DeveloperConsole.Tests/
  Program.cs               offline acceptance suite with a mock game context

docs/                      installation, commands, developer, troubleshooting
fixture/                   disposable staging helper and test matrix
```

## Native integration

The bootstrap installs these hooks once:

| Hook | Purpose |
|---|---|
| `ui.$Console.__constructor__` | capture the native console and start hidden |
| `dc.h2d.Hook_Console.handleCommand` | try the C# registry, else call the original |
| `dc.h2d.Hook_Console.log` | mirror native output into the custom history |
| `en.Hero.computeTiers` | re-apply testing stat bonuses after recalculation |
| `Boot.update` | per-frame key edges, panel sync, heartbeat |

The native console's `bg.visible` flag drives the engine's input lock, so the
transplant layout deliberately keeps the native background alive as a thin
strip while moving the native input field into the custom panel. Hiding the
whole native object would unlock gameplay input and let typing move the
player.

Unknown commands call the original `handleCommand`, which preserves native
commands. Typo suggestions for this console's commands are swallowed only when
a suggestion exists.

## Catalog provider

`LiveGameContext.NoteData` is called from `IOnAfterLoadingCDB`. It enumerates
the live `item` sheet through `item.all[]`, with `byId.keys()` as a fallback,
and builds an immutable `CatalogSnapshot`.

Each entry records:

- internal id
- display name (native item-name helper, then row name, then id)
- native row name
- group
- category derived from the group
- spawn eligibility and internal-row policy
- legendary-form availability when readable
- provenance (`item.all[]` or `byId.keys`)

This is why the console has no generated content database and no dependency on
any expansion mod. Rows added by other loaded mods are part of the live sheet
and appear automatically. Non-equipment rows remain lookup-only.

Equipment groups 0–6 are treated as ordinary equipment. Group 12 is mutation,
group 7 is talisman, and other groups are lookup-only. A small generic
internal-id set excludes engine bookkeeping rows from spawning.

## Scroll persistence

Stats are derived from the inventory, not from the tier fields:

- `Hero.computeTiers` calls `inventory.countBrutalityUp(null)` (and the
  tactics/survival equivalents).
- Those counters call `inventory.countItemKind("BrutalityUp")` and also count
  matching affixes on equipped items.
- Native scroll acquisition creates `InventItem.fromItem("BrutalityUp", null)`
  (a `BagItem`) and adds it with `inventory.add`.
- `Inventory.serialize` writes `inventory.items`, and `unserialize` restores
  them: the native persistence basis. Actual save/resume remains unverified.

`player scroll` uses `IGameContext.TryAdjustNativeScrolls`, which adds or
removes those native items and calls `Hero.onEquipedItemsChange` with HUD
refresh. `onEquipedItemsChange` runs `computeTiers`, so the tier is recomputed
from the authoritative inventory.

`player stat` is deliberately different: it is a temporary `StatBonus`
override for the current session. It is not serialized by design. The command
text and command reference both say so.

The full investigation and decompiled evidence are in
`docs/SCROLL_PERSISTENCE_INVESTIGATION.md`.

## Parser and result rules

- Command names and aliases are case-insensitive.
- Commands are capped at 512 characters and 64 arguments.
- Single or double quotes group spaces when they begin a token; escaped quotes
  are not a supported syntax. An unclosed quote is a clear syntax error.
- A leading `/` is ignored, matching the native console's habit.
- The console never interprets input as a shell, path, or script command.
- Handler exceptions become `CommandResult.InternalError`; the raw exception
  is attached to the result for the mod layer to log, while the player sees a
  concise message.
- `CommandResult` distinguishes info, success, warning, error, and internal
  error. `ConsoleCommand.Safety` and `Subcommand.Safety` drive the help tags.

Configuration writes are atomic (sibling `.tmp` then replace), so a failed
write cannot leave a half-written `console-config.json`.

## Adding a command

Register a `ConsoleCommand` in the appropriate static command file:

```csharp
registry.Register(new ConsoleCommand
{
    Name = "example",
    Aliases = ["ex"],
    Description = "One-line description for help.",
    Usage = "<arg>",
    Category = "Debug",
    Safety = CommandSafety.ReadOnly,
    Examples = ["example value"],
    Args = [new ArgSpec("arg", true, "what the argument means")],
    Handler = ctx => CommandResult.Info("result"),
}, out var error);
```

Handlers receive `CommandContext` with the game seam and parsed arguments.
Return `CommandResult.Info/Success/Warning/Failure`. Use `Warning` for a
successful command that changes run state or could not read optional metadata.

For a grouped command, add a `Subcommand[]` table and dispatch on the first
argument. `player` and `debug` are examples.

## Tests

The offline suite is a plain console program with a mock game context:

```text
dotnet run --project tests/DeveloperConsole.Tests -c Release
```

It covers tokenization, registration, aliases, argument validation, result
formatting, catalog ranking/ambiguity, search filters, item inspection,
spawning, level/quality parsing, player commands, debug commands, history,
layout, configuration, and failure paths. Exit code equals the failure count.

No Haxe runtime is required. Native behavior (hooks, CDB enumeration, native
factory/drop, localization helper, forge finalization) is verified separately
against a disposable game fixture; see `docs/RUNTIME_VERIFICATION.md`.

## Build

```text
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Release -p:AutoInstallMod=false
dotnet build src/DeveloperConsole/DeveloperConsole.csproj -c Debug  -p:AutoInstallMod=false
```

The deployable folder is `bin/<Configuration>/net10.0/output/DeveloperConsole/`.
`AutoInstallMod` stays false so a build can never deploy itself accidentally.

## Extension policy

The internal command architecture is stable enough to add commands, but v0.1.0
does not promise a public third-party command API. A future version may expose
registration, custom help categories, completion providers, and structured
output after those seams have been validated in a live game.

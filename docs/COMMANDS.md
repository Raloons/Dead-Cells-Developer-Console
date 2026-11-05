# Command reference

Commands are gated to active gameplay. Open with `=`; submit with Enter.
Multiword names can be unquoted. Quotes group a complete argument; escaped
quotes are not supported. Limits: 512 characters, 64 arguments.

## Behavior labels

- **Read-only:** no gameplay mutation.
- **Run-state:** changes the active run; the game may save that state.
- **Temporary:** session-only stat override, not a native scroll allocation.
- **UI/config:** presentation or configuration; no gameplay mutation.
- **Mixed:** consult the individual subcommand.

Inventory-backed scroll commands are intended to survive native serialization,
but **save/resume is still unverified**. They are classified run-state, not
verified persistent. No command directly edits saves or permanent unlocks.

## Equipment

| Command | Aliases | Behavior |
|---|---|---|
| `search <words> [--type T] [--limit N]` | `find`, `finditem`, `items` | Read-only |
| `item <name>` / `item info <name>` | — | Read-only |
| `item list [words] [--type T] [--limit N]` | — | Read-only |
| `item categories` | — | Read-only |
| `itemid <name>` | `id` | Read-only single-line `Display (ID)`, green on success |
| `spawn <name> [level=N] [quality=Q]` | `spawnitem` | Run-state, drops at your feet |
| `spawn last` | — | Explicitly respawn the last successful canonical request |
| `spawn recent` / `recent` | — | Read-only session spawn history |
| `catalog [stats\|refresh]` | — | Read-only gameplay; refresh rebuilds lookup caches |

```text
spawn assault shield
spawnitem assault shield
spawn "Assault Shield"
spawn assault shield level=10 quality=L
spawn assault shield --level X --quality legendary
spawn assault shield legendary
spawn StartSword
item info assault shield
item rusty sword
id multiple-nocks bow
search shield --type equipment --limit 8
item list --type mutation
```

Level: decimal 1–99 or Roman I–XXX. Quality: `base`/`0`, `+`/`1`, `++`/`2`,
`S`/`3`, `L`/`legendary`. Named options use `key=value` or `--key value`.
Trailing standalone `legendary` is reserved as a quality modifier; use the
canonical ID for an item whose full name ends with that word. Duplicate and
unknown options fail. Bare trailing numbers/quality letters remain part of
the item name; `spawn name 10 L` is not level shorthand.

Forge requests read back native level/quality before dropping. Basic spawns
report available native level/quality or explicitly say read-back unavailable.
No automatic retry occurs after a native failure. `spawn last` stores canonical
IDs and options only after a reported success, and explicitly changes run state.
The unmodified factory's legendary flag is not inferred from incomplete metadata.

Search types: `weapon`, `melee`, `ranged`, `shield`, `skill`, `mutation`,
`equipment`, `all`, or an exact category. `category:<name>` is also accepted.
Limits are 1–100. Search is broader than execution: fuzzy/substring discovery
does not authorize spawning. Mutations, talismans, internal and unsupported
records remain lookup-only. Equipment eligibility is a group/ID policy, not
proof that another mod's factory supports the row.

See [name resolution](EQUIPMENT_NAMES.md) for precedence and language fallbacks.

## Scroll controls

| Command | Aliases | Meaning |
|---|---|---|
| `ss <stat> <target>` | `setstat`, `setscroll` | Set effective tier to 1–999, at most 100 allocation changes |
| `as <stat> <amount>` | `addstat`, `addscroll` | Add 1–100 native allocations |
| `rs <stat> <amount>` | `removestat`, `removescroll` | Remove 1–100 native allocations, only if available |
| `player scroll <stat> <delta>` | — | Compatibility interface, signed delta −100…100 excluding zero |

Shortcut stat aliases: `brutality`/`brut`/`b`/`red`,
`tactics`/`tactic`/`t`/`purple`, `survival`/`surv`/`s`/`green`.
The older `player scroll` interface retains its full stat names.

```text
ss tactics 20
as brutality 5
rs survival 3
player scroll tactics -2
```

Setstat computes `delta = target − current effective tier`. The modifier floor
is `effective − native allocation count`; requests below it are refused.
Unavailable/inconsistent counts or an active temporary override also refuse
reconciliation. A read-back mismatch reports that allocations may have changed;
it never retries. Removal checks availability before mutating inventory.

Native scroll creation/removal, ownership, tier/health recomputation and HUD
refresh retain the DC-M2.1 service. Mutation scaling and save/resume are native
acceptance requirements, not conclusions from mocks.

## Player and build inspection

| Command | Shortcut / alias | Behavior |
|---|---|---|
| `player stats` | `stats` | Read-only health, tiers, native counts, position |
| `player position` | `pos`, `whereami` | Read-only coordinates and biome |
| `player status` | — | Read-only combined snapshot |
| `player mutations` | `build mutations` | Read-only IDs/names |
| `player effects` | — | Read-only affects/timers |
| `player heal [amount]` | `heal`, `fullheal` | Run-state, default full, clamped to missing HP |
| `player health <value>` | `hp` | Run-state, clamped to 1…maximum HP |
| `player gold <amount>` | `gold` | Run-state, **add** 1…1,000,000 gold |
| `player stat <stat> <value>` | — | Temporary tier override, 1…999 |
| `player stat clear <stat>` | — | Clear only that temporary override |
| `debug equipment` | `equipped`, `build gear` | Read-only inventory equipment IDs |
| `build [summary\|stats\|gear\|mutations]` | — | Read-only views through existing handlers |

## Mutations

| Command | Aliases | Behavior |
|---|---|---|
| `mutation list` | `mut list` | Read-only known mutations |
| `mutation info <name>` | — | Read-only metadata and equipped state |
| `mutation equipped` | — | Read-only active mutations |
| `mutation equip <name>` | — | Run-state native equip with verification |
| `mutation remove <name>` | — | Run-state native removal with verification |

Mutation names resolve like equipment names (IDs, display names, English
aliases, multiword, ambiguity handling). Equip verifies native equipped state
afterwards and rolls back on mismatch; a replacement of another mutation is
reported, never silent. Effect activation and exact slot-capacity semantics
are native behaviors pending live verification — see the runtime matrix.
`player mutations` still lists active IDs.

`gold` now intercepts that native command name with the documented additive,
bounded semantics above. No native balance-setting behavior is promised.
`equipped` reports carried/equipped equipment by inventory group; it does not
claim every returned row occupies an active equipment slot.

## Diagnostics

`debug snapshot`, `debug player`, `debug level`, `debug equipment`,
`debug status`, `debug catalog`, `debug mods`, `debug last` are read-only.
`mods` is the shortcut for `debug mods`. Unavailable metadata says so.
`debug last` returns the last successful diagnostic, not a fresh snapshot.
Catalog counts are multi-line and include English-name coverage.

## Discovery and session tools

| Command | Aliases | Meaning |
|---|---|---|
| `help [command\|category\|page]` | `h`, `?`, `commands` | Overview, group, detail, or 12-per-page listing |
| `suggest <prefix or command context>` | `complete` | Text-only command/item/stat suggestions |
| `aliases` | — | Canonical names and registered aliases |
| `history` | — | Last 20 submitted registered command lines |
| `errors` | — | Last 20 recorded registered-handler failures |
| `recent` | — | Last 20 successful spawns |
| `repeat` | — | Repeat last successful read-only command |
| `clear` | `cls` | Clear output; leaves game state and journal intact |
| `version` | — | Console/game/framework target versions |

`help stats` describes the stats shortcut. An exact command/alias takes precedence
over a category of the same name. Journals are process-local and bounded;
unknown native commands and parse failures are not journaled as handler results.
`repeat` never records a spawn, scroll change, health/gold adjustment or UI
command as its target. It has no mutation-confirmation bypass.

## UI and configuration

The console is a single overlay; there is no mode switching. `consize [10–80]`
adjusts panel height. `config [show|path|reload]`
inspects or reloads `console-config.json` in the mod directory. The retired
`uimode`/`ui`/`theme` names now return the ordinary unknown-command response.

Settings: `ToggleKey` (`=`, backtick, insert, F1–F12), `PanelPercent`
(10–80), `WidthPercent` (45–100), `Opacity` (.35–1), `TextScale` (.75–1.5),
`HistoryLimit` (50–1000), `SearchLimit` (5–100), `SuggestionLimit` (1–8),
`Suggestions`, `TabCompletion`, `VerboseLog`. Keys are case-insensitive.
Retired Workbench keys (`UiMode`, `Accent`, `Density`) in old config files are
ignored; nothing crashes. Invalid JSON uses defaults; invalid numeric settings
are constrained and logged. Missing new fields use defaults. History survives
reload/capacity changes.

### Retired UI commands

`workbench` (`desk`), `browse`, `pin` and `unpin` were removed with the
Workbench in DC-M4.4. `uimode` (`ui`, `theme`) was removed in DC-M4.6; the
transplant input layout stays on with no user-facing switch. `search` covers
text equipment search; `history`, `errors` and `recent` cover the session
journal as plain text.

Output scrolls with the mouse wheel (best effort over the input row) and
Page Up/Page Down (one viewport); new commands return to the newest output.
Up/Down remain command history; `clear` resets output and scroll.

See [autocomplete](AUTOCOMPLETE.md). All native presentation and input changes
in this milestone still require manual testing.

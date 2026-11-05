# DC-M4 command changelog

Existing command groups are retained. No registered command was removed or
deprecated. Unknown commands still fall through to the native dispatcher.

## Improved existing interfaces

| Interface | Change | Classification |
|---|---|---|
| `spawn`, `spawnitem` | Shared English/current/raw/ID resolver; unquoted names; trailing `legendary`; native summary; canonical session history | Run-state |
| `search`, `find` | Cached keys/tokens, English aliases, accent folding, same catalog | Read-only |
| `item info` | Shared resolver, English name and ready-to-copy canonical spawn command | Read-only |
| `item <name>` | Direct inspection shorthand; existing info/list/categories preserved | Read-only |
| `item list` | Uses indexed search including English aliases | Read-only |
| `help`, `h`, `?` | Category discovery, new metadata and aliases | Read-only |
| `suggest`, `complete` | Contextual command/subcommand/item/stat completion | Read-only |
| `uimode` | Four presentations plus old placement modes | UI/config |
| `config` | Validated new UI/completion settings; history-preserving reload | UI/config |
| `debug catalog` | Multi-line categories and English-name coverage | Read-only |
| `player stat` | Adds `player stat clear <stat>`; temporary meaning retained | Temporary |
| `player scroll` | Implementation retained; help corrects unverified persistence classification | Run-state; native inventory-backed |
| `clear`, `cls` | Preserved; correctly classified as UI/config | UI/config |

Level 1–99, Roman I–XXX, named level/quality arguments, quality aliases,
duplicate-option rejection, and native read-back mismatch rejection remain.

## New commands and aliases

| Canonical command | Aliases | Purpose | Classification |
|---|---|---|---|
| `ss` | `setstat`, `setscroll` | Effective-tier target via reconciled native allocation delta | Run-state |
| `as` | `addstat`, `addscroll` | Native scroll additions | Run-state |
| `rs` | `removestat`, `removescroll` | Native scroll removals | Run-state |
| `heal` | `fullheal` | Existing safe heal handler | Run-state |
| `hp` | — | Existing health handler | Run-state |
| `gold` | — | Existing bounded additive gold handler | Run-state |
| `pos` | `whereami` | Coordinates/biome | Read-only |
| `stats` | — | Existing stat/count view | Read-only |
| `equipped` | — | Inventory equipment ID view | Read-only |
| `mods` | — | Existing mod-loader diagnostic | Read-only |
| `itemid` | `id` | Canonical identity lookup | Read-only |
| `catalog` | — | Stats or explicit lookup-index refresh | Read-only gameplay |
| `build` | — | Summary/stats/gear/mutations views | Read-only |
| `history` | — | Session command journal | Read-only |
| `errors` | — | Session handler failures | Read-only |
| `recent` | — | Successful spawn history | Read-only |
| `aliases` | — | Registered alias inventory | Read-only |
| `repeat` | — | Last successful eligible read-only command | Read-only |

Additional aliases on existing commands: `commands` → `help`;
`finditem`/`items` → `search`; `ui`/`theme` → `uimode`.
New subcommands: `spawn last`, `spawn recent`, `debug last`.

## Compatibility notes

- `gold` previously fell through to the native console. It is now intercepted
  and **adds**, never sets, the requested bounded amount. Update scripts that
  relied on a different native meaning.
- `item` with an unrecognized first word now treats the complete argument list
  as an item name rather than reporting an unknown item subcommand.
- A trailing standalone `legendary` is reserved; named options remain the
  unambiguous way to specify level/quality.
- `ss/as/rs` replace temporary historical semantics with native inventory-backed
  operations. `player stat` remains the explicit temporary interface.
- Setstat refuses an active temporary override, modifier-floor violations,
  inconsistent counts and changes larger than 100 allocations in one call.
- `repeat` never replays a mutating command, even if one was entered most recently.
- No command is described as native persistence-verified. Save/resume and
  mutation/health scaling remain on the manual matrix.

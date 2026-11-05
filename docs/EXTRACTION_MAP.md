# Extraction map

This map records what was reused, adapted, replaced, or excluded when the
private development console was turned into this standalone project. The
private source tree was read-only during extraction; this repository is a new,
independent implementation.

## A — Reusable without meaningful change

| Component | Outcome |
|---|---|
| Command tokenizer / registry patterns | Reimplemented in `CommandRegistry.cs`; same quoting, slash-prefix, alias, and required-argument behavior. |
| Bounded output history | `HistoryBuffer.cs`; capacity, scroll window, HTML escaping, and colors. |
| Native command-history bridge | `ConsoleHistory.cs`; pushes handled lines into the native `logs` list so Up/Down recall works. |
| Console geometry math | `UiLayout.cs`, `ConsoleSizing.cs`; pure layout and percentage sizing. |
| Panel construction pattern | `DevPanel.cs`; h2d bitmaps/text objects, guarded native calls, static prompt plus native input field. |
| Stat bonus model | `StatBonus.cs`; re-apply after native tier recalculation, reset on hero change. |
| Configuration principles | Safe defaults, bounded values, invalid values never crash. Reimplemented in `ConsoleConfig.cs`. |
| Native history / log mirroring | Kept the proven hook pattern and bounded recording. |

## B — Reusable after adaptation

| Component | Adaptation |
|---|---|
| Live hero facade | Kept the verified native reads/writes; added exact health setting and skill/mutation inventory classification. |
| Equipment forge | Kept native `fromItem` + `LootGen.finalize*` ordering and read-back checks; replaced item-policy lookups with the live catalog. |
| Spawn/drop pipeline | Kept `fromItem` + `ItemDrop` + `init` + `onDropAsLoot`; item validation now comes from the runtime catalog. |
| Hero diagnostics | Kept guarded reads; expanded into player/debug subcommands. |
| Console toggle/keyboard handling | Kept edge detection, focus gate, and Win32 fallback; toggle key is now configurable from a safe key set. |
| Panel/input transplant | Kept the native-input-lock-preserving layout; exposed as `uimode transplant|cohesive`. |
| Level/biome reads | Kept best-effort guarded reads; normalized into `LevelDiagnostics`. |

## C — Replaced

| Private implementation | Replacement |
|---|---|
| Generated static content catalog | Runtime catalog built from the live `item` sheet at `IOnAfterLoadingCDB`, with `item.all[]` and `byId.keys()` fallbacks. No generated catalog is shipped. |
| Embedded name CSV | Live localized-name helper (`ItemTools.getBlueprintLocalizedName`) with row-name and id fallbacks. No name database is shipped. |
| Project-specific `btd` command family | Generic `search`, `item`, and `spawn` commands with category filters, exact-before-partial resolution, and ambiguity errors. |
| Project-specific mutation equip/remove/debug | Generic read-only `player mutations` and debug diagnostics. Equip/remove is intentionally out of scope for v0.1.0. |
| Project-specific developer grant | Removed. No permanent unlock or progression command exists. |
| Project-specific content policy | Generic group policy: groups 0–6 equipment, 7 talisman, 12 mutation, others lookup-only, plus a small internal-id denylist. |

## D — Excluded

- All private expansion content, item implementations, skills, mutations,
  affixes, and legendary effects.
- Private biome, route, progression, and boss systems.
- Private generated catalogs, name databases, content snapshots, and audit
  data.
- Private save files, Steam Cloud data, fixtures, deployment manifests, and
  machine-specific paths.
- Private milestone reports and research output.
- Private debugging commands, environment-gated grants, and test-only
  progression paths.
- Credentials, authentication information, and private repository history.

## Boundary result

The standalone project has:

- no private assembly reference;
- no private namespace reference;
- no private content catalog;
- no private data file;
- no dependency on the private mod being installed.

The private console remains untouched in its original repository. The two
implementations are maintained separately; no cross-repository coupling was
introduced.

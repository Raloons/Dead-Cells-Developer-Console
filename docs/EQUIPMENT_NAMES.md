# Equipment names and localization — DC-M4

## Identity is separate from presentation

The live CastleDB `item` sheet supplies canonical IDs, raw row names, groups
and legendary-form metadata. The native factory always receives the canonical
ID. A display name or alias never replaces it. Examples: `StartSword` is Rusty
Sword; `DashShield` is Assault Shield.

The adapter enumerates `item.all[]`, falling back to `item.byId.keys()`.
Only scalar records enter the index; no row, hero or level object is retained
by lookup/completion. All sheets are game-owned runtime data, including rows
registered by other mods before refresh. No historical private catalog is used.

## English names without a bundled database

The installed v35 tree includes an optional gettext template:
`<game>/lang/main-en.pot`. Item entries identify their native IDs in `#.` name
comments and their `data.cdb:item/#…name` reference. The reader associates only
those **name** entries with their `msgid`; it does not use descriptions,
mutation text or guessed identifier splitting as translations.

- The local installed-file check found **598 usable item-name identities**
  (one additional identity has conflicting names and is excluded).
- It confirmed `StartSword → Rusty Sword` and `DashShield → Assault Shield`.
- This is offline file evidence, not native spawning/localization evidence.
- The reader supports continued strings and multiple identity comments, excludes
  deprecated/obsolete or conflicting entries, and bounds file/line sizes.
- If unavailable or malformed, current row/localized names and IDs still work.
- English names are joined **only to rows present in the live catalog**. A
  template entry alone never makes an item visible or spawnable.
- No extracted template, translation list or generated game catalog is packaged.

The root comes from the framework's `DEAD_CELLS_GAME_PATH`, or bounded ancestor
discovery from the host directory containing `res.pak`. No broad disk search
occurs. DLC names work when the installed template and runtime rows contain
them; coverage is not promised for every DLC/mod/version.

## Current-language names

The preserved DC-M3A candidate uses `ModCore.Modules.GetText.Instance.GetString`
for the raw CDB name. DC-M4 delays this call until active gameplay/explicit
refresh, rather than invoking it during CDB boot. Successful translations become
display names; otherwise the English template, raw name and ID are fallbacks.
The raw row name remains searchable regardless of display fallback.

The unsafe `ItemTools.getBlueprintLocalizedName` call that caused the R4 access
violation is absent. The GetText pathway remains **native-runtime unverified**;
a managed `catch` cannot protect against every native interop failure.

## Resolution precedence

1. Exact canonical ID (case-insensitive).
2. Exact displayed/current-language name.
3. Exact English name, when available.
4. Exact registered alias in the catalog model.
5. Unique normalized ID/name/alias.
6. Unique prefix or complete-token match within one recognized name.

Normalization removes punctuation/spacing, folds case and Unicode accents,
and preserves Unicode letters/digits. Original text is retained for display.
Repeated whitespace, apostrophe styles and hyphens do not require special input.
Matching does not combine unrelated aliases into a fabricated phrase.

Collisions within a priority tier return structured ambiguity with numbered
choices (first eight plus remaining count). No arbitrary item is selected.
Substring and edit-distance results are discovery hints, never fuzzy spawns.
`spawn sword` therefore lists matching choices rather than choosing a weapon.

## Shared implementation

`CatalogIndex.Resolve` returns `Exact`, `Normalized`, `Partial`, `Ambiguous`,
`Missing`, `Unavailable` or `NotSpawnable`, with the selected item/candidates.
Search, item inspection, ID lookup, spawn and completion share the snapshot and
cached normalized keys/tokens. Search has a broader ranked discovery policy.

Indexes are built once per `CatalogSnapshot`. A CDB event invalidates the
snapshot; the first active console open enriches localization; `catalog refresh`
explicitly rereads the runtime sheet and optional English template. It does not
reload mods or rerun their registration. Late mod registration requires refresh.

Group 0–6 equipment is eligible for the supported factory except known internal
IDs. Mutations, talismans and other groups are lookup-only. A compatible group
is **not proof** that a custom factory path exists. Unknown mod equipment fails
conservatively if the native factory rejects it; no alternate factory is guessed.

## Troubleshooting

```text
catalog stats
catalog refresh
search assault shield
item info DashShield
id rusty sword
```

Use full names or canonical IDs to resolve ambiguity. If English coverage is
zero, check the installed template and root configuration. Never copy an entire
game localization database into the mod or public source to repair a lookup.

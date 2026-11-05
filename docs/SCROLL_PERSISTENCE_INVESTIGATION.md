# Scroll persistence investigation

**Status: root cause identified; native scroll allocation implemented and
offline-tested. Live save/resume verification remains blocked by authorization.**

## Observed behavior

The legacy console's stat/scroll commands changed the current session but the
change reverted after returning to the main menu and continuing the run.

## Root cause

The native stat authority is the inventory, not the tier field or a console
side table.

Decompiled evidence from the installed v35 `hlboot.dat`:

- `en.Hero.computeTiers` (`f@1959`) computes:

  ```text
  setTacticTier(1 + inventory.countTacticUp(null));
  setSurvivalTier(1 + inventory.countSurvivalUp(null));
  setBrutalityTier(1 + inventory.countBrutalityUp(null));
  updateMaxLife();
  ```

- `tool.Inventory.countBrutalityUp` (`f@454`) starts with
  `countItemKind("BrutalityUp")` and then counts matching affixes on equipped
  items, actives, and the talisman. `countTacticUp` and `countSurvivalUp` do
  the same for their stat.
- `tool.Inventory.countItemKind` (`f@448`) iterates `this.items` and counts
  entries whose kind string equals the requested id.
- `tool.Inventory.add` (`f@422`) resolves `_itemData` for a new item, pushes it
  into `this.items`, and calls `equip`.
- `tool.Inventory.removeOne` (`f@434`) removes the first item whose kind string
  matches, and `removeAll` (`f@428`) removes every match.
- `tool.Inventory.serialize` (`f@468`) serializes `this.items`;
  `unserialize` (`f@471`) restores `this.items` from the save.
- The native scroll pickup path in `en.Hero.applyItemPickEffect` (`f@2011`)
  creates scroll items with `InventItem.fromItem("BrutalityUp"/"TacticUp"/
  "SurvivalUp", null)` and adds them through `inventory.add` (for example the
  `AllUp` and Boss Rush upgrade paths).

The old standalone console's `player scroll` command used `StatBonus` and the
tier setter. `StatBonus` is a runtime C# side table re-applied by a
`computeTiers` hook. It is not an inventory item and is not serialized. The
tier field itself is derived state. Consequently the console-granted change
disappeared when the run was reconstructed from the saved inventory.

## Native fix

`IGameContext` now exposes:

```text
TryAdjustNativeScrolls(stat, delta, out nativeCount, out effectiveTier, out error)
```

The live adapter:

1. Maps `brutality`/`tactics`/`survival` to `BrutalityUp`/`TacticUp`/
   `SurvivalUp`.
2. For grants, creates the native scroll item with
   `InventItem.fromItem(id, null)`, adds it with `inventory.add`, and calls
   `hudOnItemPick` best-effort.
3. For removals, pre-checks `countItemKind` and removes exactly that many with
   `inventory.removeOne`.
4. Calls `hero.onEquipedItemsChange(updateHUD=true, ...)`, which runs
   `computeTiers` and refreshes the HUD, so the effective tier is recomputed
   from the inventory.
5. Rolls back only the items added by the current call if a grant fails.
6. Caps a single command at 100 scrolls and refuses a removal larger than the
   native count.

The native scroll items are ordinary `BagItem` entries in `inventory.items`,
so the existing `Inventory.serialize`/`unserialize` path persists them.

## Command semantics

- `player scroll <stat> <delta>` now uses native scroll allocation.
  - Positive grants and negative removals are persistent with the run.
  - The command reports the native count before/after and the effective tier.
  - A negative delta larger than the available native scrolls fails without
    changing anything.
- `player stat <stat> <value>` remains a **temporary session override**.
  - It uses `StatBonus` and is intentionally not persisted.
  - Its message now says so explicitly and points to `player scroll`.
  - A temporary override can coexist with native scrolls; the effective tier
    includes the override, but the native count remains authoritative and
    survives resume.
- `player stats`, `player status`, and `debug player` report native scroll
  counts.

## Regression coverage

The offline suite now covers:

- native grant increments and reports the new count;
- native removal decrements and reports the new count;
- an over-removal fails without changing the count;
- a simulated resume reconstructs the tier from the native count
  (`1 + count`);
- a second resume does not re-add or duplicate scrolls;
- `player stat` changes the effective tier but does not add native scrolls;
- a simulated resume drops the temporary `player stat` override and restores
  the native tier;
- no duplicate health/tier bonus after resume.

Result: **118 checks, 0 failures**.

## Verification status

- **Offline/decompiled:** root cause and native path are established from the
  installed v35 bytecode and the local acceptance suite.
- **Native live:** scroll save/resume is **Blocked by authorization**. No game
  process was launched. The release claim remains pending until an authorized
  session confirms:
  1. `player scroll brutality 2` increments the native count and tier;
  2. save â†’ main menu â†’ continue restores the same count/tier;
  3. no duplicate scrolls or health bonus appears after repeated resumes;
  4. `player stat` remains temporary and is clearly labeled.

No save-file editing, persistence patch, or speculative reapplication logic was
added.

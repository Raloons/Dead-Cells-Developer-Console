# DC-M2.1 - Scroll persistence, native player state & save/resume correctness

**Status:** offline-complete; native save/resume verification remains
**Blocked by authorization**.

**Repository:** Dead-Cells-Developer-Console
**Product:** DeveloperConsole v0.1.0-alpha
**Target:** Dead Cells v35 / DCCM 35.13.8

---

## 1. Project-state reconciliation

At the start of DC-M2.1 the repository was clean at the then-current
checkpoint. The scroll-persistence fix had already been implemented after the
previously reported checkpoint `09c22ab` in commit `8cee500`, with follow-up
documentation in `472cc95`. DC-M2.1 therefore **reviewed and completed the
existing correction** rather than replacing it with a second design.

The standalone console did inherit the defect in a different form: its legacy
`player scroll` command used the same `StatBonus`/derived-tier approach. The
old command was transient; the current command is native.

The sealed DC-M2 fixture `<fixture root>/DC-M2-R1` had already been re-staged
with the scroll-fixed binary during that earlier pass, before this milestone's
"preserve R1" instruction. DC-M2.1 did not modify R1 further. A fresh fixture,
`<fixture root>/DC-M2-R2`, was generated from the approved script with the
current binary.

## 2. Original bug

Historical report:

1. Start or continue a run.
2. Use the console to grant scrolls or modify scroll-derived stats.
3. The stat increase is visible during the session.
4. Return to the main menu.
5. Continue the same run.
6. The console-granted change is gone.

## 3. Native state model

Confirmed from the installed v35 `hlboot.dat`:

| Question | Confirmed answer | Evidence |
|---|---|---|
| Where is the visible stat? | `Hero.brutalityTier` / `tacticTier` / `survivalTier` | `Hero.computeTiers` |
| What is authoritative? | `Inventory.items` entries of kind `BrutalityUp` / `TacticUp` / `SurvivalUp`, plus matching affixes | `countItemKind`, `count*Up` |
| Are the tier fields primary? | No; they are derived by `computeTiers` | `f@1959` |
| How does a normal scroll allocate? | `InventItem.fromItem("...Up", null)` â†’ `inventory.add` â†’ `onEquipedItemsChange` | `applyItemPickEffect`, `Inventory.add` |
| What updates max life? | `computeTiers` â†’ `updateMaxLife`; it preserves life/rally/bonus ratios and calls `Const.scaleHeroLifeToTier` | `f@1956`, `f@5968` |
| What updates the HUD? | `onEquipedItemsChange(updateHUD=true)` calls `hudSet*`, `hudInitItems`, `updateLifeBar` | `f@1998` |
| What is serialized? | `Hero.serialize` adds `inventory`; `Inventory.serialize` writes `items` | `f@2177`, `f@468` |
| What is restored? | `Inventory.unserialize` reconstructs `items`; `Hero` restores inventory | `f@471`, `f@2180` |
| Is a physical pickup required? | No; the native allocation path is item creation + inventory add. A pickup is presentation/interaction, not the authority. | `f@2011`, `f@552` |

### Key decompiled evidence

`Hero.computeTiers` (`f@1959`):

```text
setTacticTier(1 + inventory.countTacticUp(null));
setSurvivalTier(1 + inventory.countSurvivalUp(null));
setBrutalityTier(1 + inventory.countBrutalityUp(null));
updateMaxLife();
```

`Inventory.countBrutalityUp` (`f@454`) begins with
`countItemKind("BrutalityUp")`, then adds matching affixes from equipped
weapons, actives, and the talisman. `countItemKind` (`f@448`) iterates
`this.items`. `Inventory.add` (`f@422`) sets `_itemData`, pushes the item into
`items`, and calls `equip`. `removeOne` (`f@434`) removes the first matching
kind.

`InventItem.fromItem` (`f@552`) maps group 8 to `BagItem`, which is the native
scroll item kind. Native scroll acquisition in `Hero.applyItemPickEffect`
(`f@2011`) creates `BrutalityUp`/`TacticUp`/`SurvivalUp` items and adds them
through `inventory.add`.

`Hero.onEquipedItemsChange` (`f@1998`) calls `computeTiers`; with
`updateHUD=true` it also refreshes HUD tiers, items, and the life bar.

`Hero.updateMaxLife` (`f@1956`) recomputes `maxLife` through
`Const.scaleHeroLifeToTier` and preserves the current life, rally, and bonus
ratios. This is important: a native scroll grant must go through
`computeTiers`, not just write the tier field.

`Hero.serialize` (`f@2177`) includes `this.inventory` as a known reference.
`Inventory.serialize` (`f@468`) writes `this.items`; `Inventory.unserialize`
(`f@471`) restores them. This is the persistence chain.

## 4. Root cause

The old command modified a transient `StatBonus` table and the derived tier
fields. Those values are not the inventory items serialized by
`Inventory.serialize`. A save/resume reconstructs the hero tier from
`inventory.count*Up`, so the console delta disappeared. Writing the tier field
alone is also insufficient because `computeTiers` overwrites derived fields.

## 5. Player command persistence audit

| Command | Native operation | Persistence semantics |
|---|---|---|
| `player scroll` | `InventItem.fromItem` + `inventory.add/removeOne`; `onEquipedItemsChange` | Native inventory; intended to survive save/resume (live pending) |
| `player stat` | `StatBonus` + `setTier` + `hudSet*` | Temporary session override; intentionally not saved |
| `player heal` | `Hero.setLifeAndRally` | Current run health; no permanent progression |
| `player health` | `Hero.setLifeAndRally` | Current run health; no permanent progression |
| `player gold` | `Hero.addMoney` | Current run gold; no permanent progression |
| `player stats/position/status/mutations/effects` | read-only native reads | No mutation |
| `spawn` | native factory + `ItemDrop` | Run-state item drop; native pickup/lifecycle |
| `debug *` | read-only native reads/reflection | No mutation |

The scroll fix does not silently change the temporary `player stat` contract.

## 6. Persistent vs temporary semantics

- `player scroll <stat> <delta>` - **native, persistent design**. Adds/removes
  native scroll inventory items and refreshes through `onEquipedItemsChange`.
  The inventory is the same data that save/resume serializes.
- `player stat <stat> <value>` - **temporary session override**. Uses
  `StatBonus`; intentionally not saved. The command output and command
  reference both say so.
- `player health`, `player heal`, `player gold` - run-state operations, not
  permanent progression. They are not part of the scroll authority.
- No command modifies permanent unlocks or writes save files directly.

## 7. Implementation

- `IGameContext.TryAdjustNativeScrolls(stat, delta, out nativeCount,
  out effectiveTier, out error)`.
- `LiveGameContext`:
  - maps the stat to the native scroll id;
  - validates delta (non-zero, ±100 cap);
  - pre-checks removal count;
  - grants by `InventItem.fromItem` + `inventory.add`;
  - removes by `inventory.removeOne`;
  - calls `Hero.onEquipedItemsChange` with HUD refresh;
  - rolls back only items added by the failing call;
  - reports native count and effective tier.
- `player scroll` uses this method.
- `player stat` retains the temporary override and now states that it is not
  saved.
- `player stats`, `player status`, and `debug player` show native scroll
  counts.

No save-file editing, no persistence patch, no resume-time reapplication, and
no independent scroll ledger were added.

## 8. Health and scaling

Because `onEquipedItemsChange` calls `computeTiers`, the native consequences
follow:

- `updateMaxLife` recalculates max health from the new tiers and preserves the
  current life ratio.
- HUD tier labels and the life bar refresh.
- Equipment/mutation scaling reads the recalculated tiers.
- `updateLifeBar`, item HUD, and manager callbacks run through the native
  method.

Offline tests model the count/tier relationship and resume reconstruction. They
do not prove the real native health ratio, HUD, or manager callbacks; those are
explicitly part of the remaining live check.

## 9. Idempotency and duplication safety

- Each accepted command grants/removes exactly one delta.
- No resume hook re-applies grants.
- Native scroll items are counted from the inventory; a restored run starts
  from the saved count and does not add anything.
- The command pre-checks removals and rejects over-removal without mutation.
- A failed grant rolls back only the items added by that call.
- Hook installation remains one-time in the mod bootstrap; this milestone adds
  no new hooks.
- Repeated simulated resume tests confirm counts/tiers do not duplicate.

## 10. Numeric boundaries

- Zero and non-numeric deltas are rejected.
- ±100 scrolls per command is the implemented cap.
- `int.MinValue` is rejected without `Math.Abs` overflow.
- Negative removal beyond the native count is refused before mutation.
- Invalid stat names fail closed.
- Unavailable hero/gameplay context fails with a clear error.

## 11. Regression coverage

The offline suite is now **118 checks, 0 failures**. New coverage:

- native scroll grant changes the count and tier;
- native removal changes them back;
- over-removal fails without changing the count;
- extreme negative delta fails safely;
- +101 delta fails at the cap;
- no-hero scroll command fails;
- simulated resume reconstructs the tier from the native count;
- repeated resume does not duplicate scrolls;
- `player stat` changes the effective tier but not the native count;
- a simulated resume drops the temporary `player stat` override and restores
  the native tier;
- native scroll counts appear in `player stats`/`status` and debug output.

Mocks and simulations cannot prove real-game persistence.

## 12. Build and privacy evidence

- Release build: 0 warnings, 0 errors.
- Debug build: 0 warnings, 0 errors.
- Offline suite: 118 checks, 0 failures.
- Assembly references: System.*, GameProxy, ModCore, ModCore.Common,
  HaxeProxy, HashlinkSharp, Serilog only.
- Privacy audit: PASS over 42 tracked text files.
- Release package rebuilt; see section 13.
- Fixture R2 admission: 0 failures.

## 13. Fixture

- **New sealed fixture:** `<fixture root>/DC-M2-R2`.
  - Game v35, DCCM 35.13.8.
  - Only `DeveloperConsole` under `dccm/mods`.
  - Stock loader; empty fixture save roots.
  - No private-content names.
  - Manifest and SHA-256 package hashes match the current build.
- **Prior fixture:** `<fixture root>/DC-M2-R1` is retained. It had already been
  re-staged during the earlier scroll-fix pass; DC-M2.1 did not touch it
  further.

## 14. Release artifact

The release package is rebuilt after the documentation/fixture updates. The
current artifact is:

- `dist/DeveloperConsole-0.1.0-alpha.zip`
- ZIP SHA-256: `450F01001F96FC0E53AC30C7FFD02442BF5F94F7E63984E931A0241E9E0354D7`
- Mod DLL SHA-256: `F5EDE6C2321AA6CDACDEBED993C7F67260899927C17339472DD7FDC18FC746C7`

(The package is local and ignored by Git; the source tree rebuilds it with
`tools/Build-Release.ps1`.)

## 15. Remaining native verification

Cannot be claimed without an authorized disposable session. Exact tests:

1. **Control A - ordinary scroll:** collect a normal scroll, save, continue,
   verify preservation.
2. **Control B - console scroll:** `player scroll brutality 2`, save, continue,
   verify native count and tier.
3. **Control C - temporary stat:** `player stat brutality 10`, save, continue,
   verify the override is gone and the native count remains.
4. **Control D - multiple colors:** grant multiple supported scrolls, save,
   continue, compare exact counts/tiers.
5. **Control E - repeated resume:** continue several times; verify no
   duplication or extra health.
6. **Control F - invariants:** verify max/current health, HUD, damage/mutation
   scaling, and item/mutation state remain coherent.

These are integrated as R13, R18, and release-hardening check H01 in
`docs/RUNTIME_VERIFICATION.md`.

## 16. Known limitations

- Live save/resume is pending authorization.
- `BagItem` equip/serialization behavior is inferred from decompiled native
  code, not observed in a live save.
- The command presentation does not reproduce the full native pickup
  log/audio sequence; it does use the native item allocation and HUD refresh.
- `player stat` remains temporary by design.

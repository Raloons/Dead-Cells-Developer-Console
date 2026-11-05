# DC-M4.5 â€” Classic console usability & native item spawning repair

**Milestone:** DC-M4.5, following DC-M4.4 (`4996e60`).
**Version:** 0.1.0-alpha; assembly/mod remains 0.1.0.
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Status:** offline-complete; native verification pending. No game launched for M4.5.

## 1. Director feedback and findings

- **Font too large:** fixed by compacting the classic plan (below).
- **Assault Shield spawn: WORKS.** The director confirmed `spawn assault shield`
  produces the item. The earlier "success but not appearing" report was a
  visibility/location confusion, not a dispatch bug. The audited pipeline
  (Section 3) can only report success after the native drop completes.
- **`spawn walljumpkey` / `spawn biggem`:** correctly refused â€” non-equipment
  catalog rows (keys/gems/quest groups) are not ordinary equipment. Refusal
  stands; no bypass added.
- **Scroll persistence observed:** setting and adding scrolls survived
  menu/continue in the director's run. Recorded in H01 with exact scope
  (set+add observed once; removal/multi-color/repeats/invariants pending).
  Implementation untouched.

## 2. Native session finding: integer-field assignment bug (fixed)

The R14 session log showed repeated `input-row restore failed: Cannot
implicitly convert type 'double' to 'int'` warnings. Root cause, verified
against installed-proxy metadata (`dc.ui.Console.height` is `Int32`): the
cohesive restore assigned a `double` (`nativeHeight`, and the `2.0` literal in
transplant, silently swallowed) to the integer native `height` field, aborting
the whole restore — cohesive input placement never completed. The transplant
`maxWidth` path had the same latent shape. Fix: narrow to `int` before every
dynamic write (`(int)nativeHeight`, `2`, `(int)Math.Round(...)` for widths).
Locked by the `native-height-int-contract` offline test, which asserts the
Int32 property type against a local game proxy when present. This was a
pre-existing defect, not a DC-M4.5 regression.

## 3. Compact typography

Classic plan: title 16â†’13, body 16â†’13, line 24â†’21, header 38â†’32, completion
labels 14â†’13, completion header 11â†’10. Native input scale factor simplified
from `InputScaleÃ—4/3` to `InputScale` so input matches output size. Game-font
renderer unchanged; no new font infrastructure. Help default listing is now
one `name â€” description` line per command plus the hint line (Tags legend and
group headers removed); `help <command>` detail, safety labels and category
views unchanged.

## 4. Spawn pipeline audit (release-blocking investigation)

Traced `spawn assault shield` end to end against installed-proxy metadata:

1. `EquipmentForge.TryParse` splits forge options from the multiword query.
2. `CatalogIndex.TryResolve` matches "assault shield" â†’ `DashShield`
   (English alias), with ambiguity/typo handling; fuzzy matches never execute.
3. `IsEquipment` (groups 0â€“6) and `Spawnable` gates refuse keys, gems,
   mutations, talismans, internal rows with specific reasons.
4. `CreateItem` â†’ `tool.$InventItem.fromItem(id)` factory; non-equipment and
   unspawnable rejected before any native call.
5. `TryForgeEquipment` applies affixes/finalize via the active `lootGen`,
   then validates level/quality read-back (`ValidateReadback`) â€” mismatch fails.
6. `DropConverted` constructs `dc.en.inter.ItemDrop(level, cx, cy, item)`
   (verified ctor signature), `init()`, `setPosCase`, `onDropAsLoot()`,
   `dx = hero.dx` â€” the game's own loot-drop entity at the hero position.
7. Success is recorded (`RecordSpawn`) and reported only after all of the
   above complete. Any factory/forge/drop exception yields failure or
   internal-error â€” never a false success (covered by `spawn-factory-throw`
   and new `spawn-drop-throw` mock tests).

Conclusion: no dispatch bug found; user confirmation + entity-path evidence
agree the drop is real. Success messages now say "at your feet" to set the
visibility expectation.

## 5. Non-equipment entries and `spawnpickup`

Proxy survey: `PileOfGold(Level,x,y)` and `GoldNugget(Level,x,y,amount)` are
currency-only, not ID-driven; keys participate in door/quest logic
(`LockedDoorButton.hasKey`, `Gardener.dropKey`). There is **no verified
generic pickup-spawn path**, so no `spawnpickup` command was added â€” spawning
quest/progression objects through guessed native calls is explicitly out of
scope. Non-equipment `spawn` attempts keep their specific refusal messages.

## 6. Validation

- Suite: **295 checks, 0 failures** (294 inherited + native integer-field
  contract).
- Release + Debug builds, zero warnings/errors.
- Privacy audit, deterministic packaging (rebuilt identically, checksums
  verified), R15 fixture admission VerifyOnly, auth-gate/installer/outcome
  negatives: pass (see `dist/DC-M4.5/` logs).
- R15 DLL `2F7717CB91BD3929E0A35330FDF9275EF1787FBE8AFB28CB8B8EDCCEE83DF473` (212992 bytes) identical across
  build/fixture/package; ZIP sealed via the adjacent `.sha256` sidecar
  (see `dist/DC-M4.5/seal.log`).
- Candidate: `dist/DC-M4.5/DeveloperConsole-0.1.0-alpha.zip` (+`.sha256`).
  Prior fixtures, evidence and ZIPs preserved untouched.
- Public source export local-only; no remote/push/publication.

## 7. Remaining native verification

Full R01â€“R20/H01 (H01 partially director-observed, remainder pending) and
M4-01â€¦M4-06 plus M4.4-01â€¦M4.4-04. In particular: visible pickup confirmation
across equipment types/levels/qualities, forged-stat read-back on live rows,
`uimode` placement switch, compact layout at 720p. No M4.5 launch or receipt
exists; prior authorizations do not carry forward. No release authorized.

# DC-M4.6 â€” Console usability, mutations & command cleanup

**Milestone:** DC-M4.6, following DC-M4.5 (`10d3414`).
**Version:** 0.1.0-alpha; assembly/mod remains 0.1.0.
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Status:** offline-complete; native verification pending. No game launched for M4.6.

## 1. Separator tofu (fixed)

Director screenshots showed `â–¡` between command names and descriptions. Root
causes, both in format strings (never in game/item data):
- Real U+2014 em dashes (`CoreCommands`, `CatalogCommands`).
- Mojibake `Ã¢â‚¬"` triples (mis-decoded em dashes in `CompletionEngine` Ã—4,
  `SessionJournal` Ã—1) and a mojibake ellipsis in `Presentation.Fit`.
All replaced with `-` / `...`. The full source tree is now pure ASCII outside
game-resolved content; equipment/localized names are untouched. Locked by
`help-ascii` / `completion-ascii` tests.

## 2. Scrollable output

`HistoryBuffer` keeps a bounded scroll offset instead of resetting on every
append: reading older output is never yanked by incoming lines. New submitted
commands and their results call `ScrollToBottom()`; `clear` resets; eviction
clamps the offset. Page Up/Down scroll a full viewport (`DevPanel.VisibleLines`
from the live plan, minimum 1); Up/Down stay on command history and Left/Right
/Home/End stay on caret editing â€” no editing shortcut was rebound. A
best-effort wheel path attaches `Interactive.onWheel` on the transplanted
input (`HlAction<dc.hxd.Event>`, statically rooted, only when unset, fully
guarded); positive `wheelDelta` moves toward older output. If the proxy
rejects it, keyboard scrolling remains. `help`/`help N`/`help <category>` give
paged access regardless: overview (categories), 12-per-page listing with
`No help page N (M pages)` bounds errors, unchanged detail views.

## 3. `uimode` removed

The command, its `ui`/`theme` aliases, completion context, `ConsoleMode`
parsing and the now-dead `applyTransplant`/`applyCohesive` plumbing are gone.
`ConsoleMode.Transplant` stays on internally (cohesive remains a code
fallback). Typing `uimode` returns the ordinary unknown-command response.

## 4. `itemid` cleanup

`itemid rusty sword` now returns exactly `Rusty Sword (StartSword)` â€” one
line, no generated spawn suggestion â€” classified **Success** (green).
Unknown/ambiguous stay failures with existing colors. Locked by `id-natural`.

## 5. Input baseline alignment

The `>` prompt rendered at 13/12Ã— the input's scale with a shared top edge,
so its baseline sat lower than the input text. Both now use font12 at the
same scale and top (`>` caption size 13â†’12, field at plan `InputY`), which
aligns baselines by construction at every console size. No magic offsets.

## 6. Mutations: investigation

Proxy survey (read-only, no execution): no `Mutation*` types or Hero mutation
members exist. Mutations are group-12 inventory items (confirmed by the
existing diagnostics scan). Usable verified verbs: `Inventory.add`,
`Inventory.equip(InventItem)â†’bool`, `isEquiped`, `removeOne(id)â†’bool`,
`countItemGroup`, `Hero.onEquipedItemsChange`, `hudOnItemPick` â€” the same
verbs as the director-verified scroll path. `PileOfGold`/`GoldNugget` are
currency-only; perk members belong to niche systems. Slot-capacity constants
and add-vs-equip internals are not statically derivable, so the design
adapts: add â†’ verify â†’ explicit `equip()` only when not already active â†’
re-verify â†’ roll back on any mismatch; replacements are reported, never
silent. No `spawnpickup`, no guessed quest/key calls, no unlock edits.

## 7. Mutation commands

`mutation` (`mut`): `list`, `info <name>`, `equipped`, `equip <name>`,
`remove <name>`. Group-12 resolution (ID, display, English, alias,
multiword, ambiguity, suggestions) reuses shared catalog infrastructure.
Equip refuses duplicates/unsupported/no-hero/factory/slot-full; removal
refuses inactive entries; both verify post-state. Safety: read-only vs
run-state split. Completion covers `mut equip/info/remove` names. Effect
activation and exact slot semantics: native-pending, stated in help-adjacent
docs and the runtime matrix (new 10-item checklist).

## 8. Validation

- Suite: **326 checks, 0 failures** (322 inherited + separator,
  scroll-model, help pages, uimode removal, itemid, alignment, 25 mutation
  contracts).
- Release + Debug builds, zero warnings/errors.
- Privacy audit, deterministic packaging (rebuilt identically, checksums
  verified), R16 fixture admission VerifyOnly, auth-gate/installer/outcome
  negatives: pass (see `dist/DC-M4.6/` logs).
- R16 DLL `6C4193887F089AF9CDD1F7BCAA301266C2B7C0A6F8ABC467B04BB6628E7C7FBB` (222720 bytes) identical across
  build/fixture/package; ZIP sealed via the adjacent `.sha256` sidecar
  (see `dist/DC-M4.6/seal.log`).
- Candidate: `dist/DC-M4.6/DeveloperConsole-0.1.0-alpha.zip` (+`.sha256`).
  Prior fixtures, evidence and ZIPs preserved untouched.
- Public source export local-only; no remote/push/publication.

## 9. Remaining native verification

R01â€“R20/H01, M4-01â€¦M4-06, M4.4-01â€¦M4.4-04, plus the new 10-item mutation
checklist (equip flips equipped state, effects activate, slots/duplicates/
removal/transitions/resume/invalid-ops). No M4.6 launch or receipt exists;
prior authorizations do not carry forward. No release authorized.

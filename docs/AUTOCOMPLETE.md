# Contextual completion

The console includes a pure suggestion engine and a guarded native-input adapter.
A small completion list appears above the input row; Tab cycles forward and
Shift+Tab cycles backward. The engine is offline-tested; native key/focus
interactions remain pending.

| Input | Example completion |
|---|---|
| `pla` | `player ` |
| `debug ca` | `debug catalog ` |
| `spawn ass` | Assault Shield / `DashShield` / shield |
| `item info assault` | `item info DashShield ` |
| `ss ta` | `ss tactics ` |
| `mut equip veng` | `mut equip TestVengeance ` |

Suggestions show recognizable names, canonical IDs, category and English alias
when different. Tab cycles forward; Shift+Tab cycles backward. Completion inserts
canonical text and **never executes it**. Enter explicitly submits the input.
Up/Down remain native command-history controls. Output uses Page Up/Down.

Suggestions are capped by `SuggestionLimit` (1–8) and the available panel height.
They recompute only on changed input, sampled
every six game ticks; a fresh Tab edge also samples immediately. No CDB enumeration
or localization-file parsing happens per keystroke. Index scanning uses cached
strings, not native rows. Empty/bounded catalogs are handled.

Completion does not rewrite forge options (`=`/`--`); finish those manually.
It inserts a canonical ID for names with spaces so the result is deterministic.
Native Tab handling can vary by host; disable the adapter with
`"TabCompletion": false` if native editing conflicts. `Suggestions` controls
the popup. `suggest spawn ass`, `suggest ss ta`, and `complete debug ca` provide
text-only discovery independent of Tab.

No clipboard access, command submission, one-key spawning or mutation callback
is part of the completion-selection class. Tests cover that contract; they do
not prove the game delivers or suppresses keys correctly.

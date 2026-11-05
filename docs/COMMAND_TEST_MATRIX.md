# Command test coverage matrix

DC-M4 adds natural English/current-language/alias resolution, deterministic
multiword spawn grammar, scroll shortcuts and reconciliation, read-only repeat,
bounded journals and contextual completion. The suite retains the foundation
checks. DC-M4.4 retires the Workbench UI commands below; the suite now covers
the single-overlay console. Installed-template checks
are two additional local file contracts. See `OverhaulTests.cs`.
All new native interactions remain pending. The old starting-sword fixture
and examples now use the correct `StartSword` identity too.

**Offline coverage** means the command has parser/validation/handler/error
tests in the local suite. **Native status** is independent: no command is
claimed as live-verified until the authorized runtime matrix is executed.

| Command / subcommand | Parser | Validation | Handler | Error | Gameplay context | Docs | Native status |
|---|---|---|---|---|---|---|---|
| `help` | Yes | n/a | Yes | Yes | n/a | Yes | Pending |
| `suggest` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `config` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `uimode` / `ui` / `theme` | Retired (DC-M4.6) | — | — | — | — | Retired note | n/a |
| `version` | Yes | n/a | Yes | n/a | n/a | Yes | Pending |
| `consize` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `clear` | Yes | n/a | Yes | n/a | n/a | Yes | Pending |
| `workbench` / `desk` | Retired (DC-M4.4) | — | — | — | — | Retired note | n/a |
| `browse` | Retired (DC-M4.4) | — | — | — | — | Retired note | n/a |
| `pin` / `unpin` | Retired (DC-M4.4) | — | — | — | — | Retired note | n/a |
| `search` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `item info` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `item list` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `item categories` | Yes | Yes | Yes | Yes | n/a | Yes | Pending |
| `spawn` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player stats` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player heal` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player health` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player position` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player status` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player mutations` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player effects` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player gold` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player stat` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `player scroll` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `debug snapshot` | Yes | Yes | Yes | Yes | Hero/level dependent | Yes | Pending |
| `debug player` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `debug level` | Yes | Yes | Yes | Yes | Hero/level required | Yes | Pending |
| `debug equipment` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `debug status` | Yes | Yes | Yes | Yes | Hero required | Yes | Pending |
| `debug catalog` | Yes | Yes | Yes | Yes | Catalog loaded | Yes | Pending |
| `debug mods` | Yes | Yes | Yes | Yes | Host reflection | Yes | Pending |

## Covered invariants

- tokenizer: whitespace, quotes, slash prefix, Unicode, unclosed quotes,
  length/argument ceilings;
- registry: duplicate names/aliases, aliases, unknown-command fall-through,
  suggestions, handler exceptions;
- results: info/success/warning/error/internal-error, no raw exception text;
- catalog: exact/prefix/token/substring ranking, ambiguity, typo hints,
  category filters, bounds;
- spawning: item resolution, category policy, level/quality/legendary parsing,
  read-back failures, factory failures;
- player: read-only/run-state/temporary/persistent classification, native scroll
  counts, resume/no-duplicate model, heal/gold bounds;
- config: defaults, invalid JSON, clamping, key binding, atomic write,
  blocked destination;
- UI/history/layout: bounds, scroll, HTML escaping, geometry invariants;
- fixture/release tooling: package hash, admission, synthetic authorization
  rejection, installer tests.

## Not covered by offline tests

- Native hook dispatch and lifetime in the real game.
- Real CDB timing and localization.
- Real factory/forge/drop behavior.
- Real health/mutation/equipment scaling after scroll allocation.
- Real save/resume and level transitions.
- Real uninstall from a game installation.

Those are in `docs/RUNTIME_VERIFICATION.md` and remain blocked by authorization.

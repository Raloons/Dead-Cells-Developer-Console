# DC-M2.2 - Reliability, developer experience & mod compatibility report

**Status:** offline-complete; native runtime verification remains
**Blocked by authorization**.

**Starting HEAD:** `e86a351`
**Target:** Dead Cells v35 / DCCM 35.13.8
**Product:** DeveloperConsole v0.1.0-alpha

---

## 1. Review methodology

I audited the current standalone implementation subsystem by subsystem:

- command registry and tokenizer;
- command metadata, help, suggestions, and result handling;
- runtime catalog construction, ranking, and suggestions;
- item inspection and spawning;
- player commands and native state writes;
- native hook bootstrap and console/panel lifecycle;
- configuration read/write;
- logging and diagnostics;
- fixture and release tooling;
- offline tests and documentation.

The pre-existing suite was **118 checks**. The DC-M2.1 native scroll fix was
preserved. The new suite is **139 checks, 0 failures**.

No game process was launched. No production save, Steam Cloud data, or private
project was accessed or modified.

## 2. Findings and fixes

### High - heal integer overflow

`LiveHero.Heal` computed `life + amount` as a 32-bit integer. A command such
as `player heal 2147483647` could overflow to a negative value and pass it to
`setLifeAndRally`.

**Fix:** compute the missing-health room first, clamp the applied amount, then
add. The offline mock uses the same invariant. Added a regression test.

### High - unbounded gold grant

`player gold` accepted any positive 32-bit value. The native `addMoney` path
is an integer addition and could overflow near the upper bound.

**Fix:** the command now accepts 1-1,000,000 per invocation and rejects larger
values before touching native state. Added a regression test.

### Medium - parser had no length/argument bounds or quote error

A very long line or an unclosed quote could be processed as a command.

**Fix:** commands are capped at 512 characters and 64 arguments; an unclosed
quote is a clear syntax error. The parser still supports spaces, tabs, leading
`/`, and single/double quotes. Added tests.

### Medium - raw handler exception text reached the console

`CommandRegistry` returned `command failed: <exception message>`.

**Fix:** handlers now return `CommandStatus.InternalError`. The exception is
carried on the result, logged by the mod layer, and the player sees a concise
"failed internally; see the DCCM log" message. Added a regression test that
checks the raw exception text is not exposed.

### Medium - configuration write was not atomic

`console-config.json` was written directly on first creation.

**Fix:** the console now writes a sibling `.tmp` and replaces the canonical
file. Failed writes fall back to defaults and delete the temp file. Added
tests for creation, invalid JSON fallback, and a blocked destination.

### Medium - search did not handle word order or item typos

Search ranked exact/prefix/substring only. `search sword rusty` missed
`Rusty Sword`, and a zero-result typo gave no hint.

**Fix:**
- added token matching, so word order does not matter;
- added a bounded `did you mean` suggestion for zero-result searches and
  unknown `item info` queries;
- retained exact-before-partial resolution and never auto-selected a result.

Added tests for token order, typo suggestion, and suggestion use in `item info`.

### Medium - duplicate `--limit`

`search --limit 1 --limit 2` silently used the last value.

**Fix:** duplicate `--limit` is rejected. Added a test.

### Low - help lacked state-change classification

Help listed commands without saying whether they are read-only or change
state.

**Fix:** `ConsoleCommand.Safety` and `Subcommand.Safety` now tag every command
as `read-only`, `temporary`, `run-state`, `persistent run-state`, `ui/config`,
or `mixed`. General help and per-command help show the tag. Added tests.

### Low - no-argument player/debug commands ignored extra text

For example, `player stats extra` still returned stats.

**Fix:** read-only no-argument player and debug subcommands now reject extra
arguments with a usage message. Added tests.

## 3. UX improvements

- Help is grouped and includes safety tags.
- Search is order-independent for multi-word queries.
- Zero-result searches and unknown item queries show bounded suggestions.
- Duplicate options and syntax errors are explicit.
- Command errors expose a concise message; detail goes to the log.
- Config writes are atomic.
- Gold/heal bounds are documented and tested.
- `player scroll` and `player stat` remain clearly distinguished.

## 4. Compatibility findings

- The runtime catalog is built from the live item sheet at
  `IOnAfterLoadingCDB`; rows registered by other loaded mods are part of that
  sheet and can be searched.
- Spawnability is still governed by item group and the native factory. A row
  that is visible but not safely constructible returns a clear failure.
- No third-party mod is required or hardcoded, and no private expansion content
  or dependency is present.
- The console targets Dead Cells v35 and DCCM 35.13.8. No universal
  game-version or arbitrary-mod compatibility is claimed.
- No shell, network, save-file, or arbitrary path APIs are used.

## 5. Commands affected

- `help` - safety tags.
- `search` - token matching, typo hints, duplicate option rejection.
- `item info` - typo hints.
- `player heal` - overflow-safe native heal.
- `player gold` - 1-1,000,000 bound.
- `player stats`, `player position`, `player status`, `player mutations`,
  `player effects` - reject extra arguments.
- `debug snapshot`, `player`, `level`, `equipment`, `status`, `catalog`,
  `mods` - reject extra arguments.
- `config` / config file creation - atomic write.
- All commands - safety metadata, internal-error isolation.

## 6. Test additions

The suite grew from 118 to **139 checks**:

- unclosed quotes, Unicode tokens, long commands, too many arguments;
- internal-error status and non-exposure of the raw exception;
- token-order search, typo suggestions, duplicate `--limit`;
- item-info suggestions;
- help safety tags;
- no-argument extra-text rejection;
- heal overflow and negative heal;
- gold cap;
- config creation, invalid-JSON fallback, blocked write;
- all prior parser, registry, catalog, spawn, player, debug, history, layout,
  and scroll-persistence tests.

Mocks and simulations are explicitly not native persistence evidence.

## 7. Build and privacy evidence

- Release build: 0 warnings, 0 errors.
- Debug build: 0 warnings, 0 errors.
- Offline suite: 139 checks, 0 failures.
- Assembly references: only `System.*`, `GameProxy`, `ModCore`,
  `ModCore.Common`, `HaxeProxy`, `HashlinkSharp`, `Serilog`.
- Privacy audit: PASS over 43 tracked text files.
- New fixture `DC-M2-R4`: admission 0 failures; no private-content names.
- Package and fixture DLL hashes match the build.

## 8. Fixture and release artifact

- New sealed fixture: `<fixture root>/DC-M2-R4`.
- Prior fixtures R1-R3 are retained; R4 is the final DC-M2.2 binary.
- Release package: `dist/DeveloperConsole-0.1.0-alpha.zip`.
- Package SHA-256: `170784FE6AAB2DC37511779D074332F70C35A816211A5E27356B7452C52CECF0`
- Mod DLL SHA-256: `4733254175C0E49786F88ED1D4F496F7E68937826EA48750BD6B72BB7FF21877`

The package is local and ignored by Git; `tools/Build-Release.ps1` rebuilds it.

## 9. Performance and resource notes

- Search/suggestion work is bounded by the catalog size and result limits and
  runs only on command execution, not per frame.
- Token matching adds a small per-item token set; the catalog is cached and
  not rebuilt per keystroke.
- History and result lists remain bounded.
- No duplicate panel construction was introduced.
- Config writes are atomic and occur only on explicit load/reload/create paths.

## 10. Remaining native verification

The following remain **Blocked by authorization** and are not claimed:

- Baseline boot and console initialization.
- Open/close, focus, input restoration, repeated toggle.
- Live catalog timing/localization and third-party mod rows.
- Spawn/forge/drop and legendary behavior.
- Player health/heal/gold/stat/scroll native effects.
- Native scroll save/resume (R13, R18, H01).
- Level transitions, save/resume, UI cleanup, uninstall.
- No native claim is made from mocks or static analysis.

## 11. Known limitations

- No live runtime evidence for this pass.
- Search suggestions are hints, not fuzzy resolution.
- Escaped quotes are not supported; an unclosed quote is an error.
- `player stat` remains temporary by design.
- Third-party item spawnability depends on native factory support and cannot be
  guaranteed for arbitrary mods.
- The console does not manage enemy spawning, permanent unlocks, memory edits,
  scripts, or network control.

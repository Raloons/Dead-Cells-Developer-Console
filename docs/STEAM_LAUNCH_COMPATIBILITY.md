# Steam launch & Workshop compatibility

**Status:** investigation only. No game launched, no upload executed, no
production file modified for this note.

## 1. R9 preflight failure anatomy (historical)

The first authorized R9 session failed before gameplay with exit code 1:

```text
Unable to initialize the Steam API: Cannot create IPC pipe to Steam client
process. Steam is probably not running.
Steam init failed!
```

Cause, established at the time: the staged fixture's `dccm/config/modcore.json`
carried `EnableGoldberg=false`, so the game attempted a **real** Steam API
initialization while the Steam client was closed (our sessions run Steam-closed
by policy to protect the production installation). With no client IPC pipe,
`steam_api` init failed and the process exited before gameplay. No production
delta occurred. Restoring fixture-local `EnableGoldberg=true` and retrying the
identical DLL reached gameplay (R9 retry, 106s, clean exit).

## 2. The three launch paths (official DCCM docs)

Per the official DCCM documentation site (`dead-cells-core-modding.github.io`):

1. **Manual install:** DCCM core files under `<game>/coremod/`, launch via
   `coremod/core/host/startup/DeadCellsModding.exe`. All disposable-fixture
   sessions use this path.
2. **Workshop install:** replace the game's `deadcells.exe` with
   **SteamStartShell** (obtained from GitHub), then launch **via Steam**.
   SteamStartShell downloads the DCCM core from the Workshop and writes the
   `DCCM_EXTRA_MODS_PATHS` environment variable pointing at subscribed
   Workshop mods. Mod load order: local `coremod/mods/` first, Workshop
   paths second; a local mod wins a name conflict (matched on
   `modinfo.json` name).
3. **DCCMTool:** `DCCMTool steam upload -i <mod dir>` publishes a mod to the
   Workshop; `DCCMTool steam mount -n <name>` subscribes/mounts Workshop mods
   locally. Upload inherently runs inside a logged-in Steam client session.

The bundled `ModTools/ModsDoc.pdf` covers only asset tooling (CDB/TMX/atlas/
PAK); it documents no launch or Workshop workflow.

## 3. Goldberg emulator role

The DCCM tree ships both shims side by side:

- `coremod/core/native/win-x64/steam_api64.dll` — real Steam client shim.
- `coremod/core/native/win-x64/goldberg/steam_api64.dll` — Goldberg open-source
  Steam emulator (LGPL 3.0, credited in the DCCM repository).
- `coremod/core/host/startup/steam_appid.txt` — `588650` (Dead Cells AppID).

`EnableGoldberg=true` (our staged fixture value) routes Steam API calls to
the emulator so the game boots with the Steam client closed. `false` requires
a running, logged-in Steam client. This is a DCCM-supported configuration,
not a bypass: no authentication is spoofed, no credential is used, and no
Workshop/online feature is exercised in our sessions.

## 4. Our session safety policy (not a technical limitation)

`fixture/Invoke-RuntimeSession.ps1` **refuses** to start when any
`steam`/`steamwebhelper`/`GameOverlayUI`/game process runs, and sets
`DCCM_EXTRA_MODS_PATHS=';'` (local mods only). This is production
protection — it must NOT be read as "the game cannot run while Steam is
open." The normal player path (Steam open, real API init, Workshop mods via
SteamStartShell) is simply outside our safety bounds and therefore
**untested by us**. A Steam-open launch has never been attempted in this
project.

## 5. Can this mod load via the Workshop workflow?

- **Package shape:** yes, compatible. `DeveloperConsole/` holds `modinfo.json`
  at its root, folder name matches `name`, single DLL, `dependencies: []` —
  exactly the documented valid-mod layout for both manual and Workshop
  distribution.
- **End-to-end** (subscribe → SteamStartShell mount → load in a Steam-client
  session): **PENDING / not verifiable in-bounds.** It requires a running
  Steam client and either production-install modification or a new fixture
  shape outside the admitted manual-install layout. No claim is made.
- **Uploading this mod** (`DCCMTool steam upload`): interface documented,
  never executed here. It would publish publicly under the operator's Steam
  identity.

## 6. Standing holds

- **Workshop publication is held** until the compatibility question is
  resolved AND explicit publication authorization names the destination and
  artifacts. GitHub release preparation may proceed independently.
- No fixture, script, or config change in this note weakens any launch gate:
  explicit receipts, bounded windows, production snapshots, and the
  Steam-process refusal all remain in force.

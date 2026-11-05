# DC-M4.1 R10 — opaque Workbench correction

## Native finding

The authorized R9 retry reached gameplay. The user's screenshot showed
Workbench Home with game scenery visible through the workspace/header and
backing surfaces displaced from their intended layout. This is a native UI
failure despite the offline layout checks passing.

The first R9 attempt exited before gameplay because Steam initialization failed
(exit 1). After preserving that attempt, the fixture-only startup setting
`EnableGoldberg=true` was restored and the same DLL retried. The retry ran for
106,238 ms and exited with code 0, without timeout or production snapshot delta.
Its runner result is `EXITED_CLEANLY_UNVERIFIED`: reaching Home and exiting cleanly
does not establish the remaining manual acceptance matrix.

Both R9 attempt directories and the original R9 DLL/manifest are preserved.
Raw logs remain outside public artifacts because they contain private local
inventory information.

## Correction

- Workbench's main application backdrop now has alpha 1, independently of the
  legacy opacity preference. The small-viewport Classic projection is also opaque
  while serving as Workbench's fallback.
- `SceneTransform.Apply` explicitly sets `posChanged=true` after writing native
  position and scale. Pooled rectangles, captions, the transplanted input and
  cohesive input restoration all use the helper.
- This matters because installed GameProxy properties use field accessors
  (`SetValueFieldById`), bypassing Haxe inline setters that invalidate cached
  transforms. Updating coordinates alone can leave pooled nodes drawing with a
  previous layout's matrix.

Installed proxy metadata confirms the transform fields and boolean `posChanged`;
upstream Heaps source explains the matrix invalidation contract. REA's full
managed inventory exceeded its representation budget, so a local read-only
`System.Reflection.Metadata` probe inspected the exact proxy type instead.
That probe did not load or execute the game. Its output is retained locally in
`dist/DC-M4.1-R10/proxy-transform-metadata.log`.

## Offline validation

- **792 checks, zero failures**: the inherited 780 checks plus cached-transform
  transition/restoration and opaque-backing contracts for all eight sections and
  the small-viewport fallback.
- Release and Debug builds pass with zero warnings/errors.
- Fifteen regenerated SVG/PNG comparisons are in the
  [current gallery](previews/DC-M4.1-R10/index.html). They remain synthetic layout
  evidence; they cannot prove native rendering.
- R10 disposable fixture admission passes in VerifyOnly mode with the exact
  Release DLL. Its startup configuration explicitly retains the successful
  fixture-local setting from R9.

## Candidate identity

The assembly/mod version remains 0.1.0; release label remains 0.1.0-alpha.

| File | SHA-256 |
|---|---|
| R10 DLL | `05191DAAAE0A98ABA1FC314F1276BC79D982F48AA530DAF3D921BAE5FD21860A` |
| R10 PDB (local fixture only) | `4963543BD2B94EC5E5788D0A37C7F3179F80E74FBCC8D0499E0CE41F3EDB452E` |
| modinfo.json | `3A84598DF4A7AD39F4D3786422A3F767298562DDB109A95B64E012196CADFEBF` |

The current package is `dist/DC-M4.1-R10/DeveloperConsole-0.1.0-alpha.zip`;
its adjacent `.sha256` file seals the archive. The original DC-M4.1 ZIP is
preserved. The fresh public-source export is local to `dist/DC-M4.1-R10/public-source`.

## Native retest still required

R10 has not been launched. R9 authorization does not authorize the changed DLL.
A fresh bounded manual session must confirm a solid workspace/header after
Classic→Workbench, section switches, close/reopen, resize and mode cycling;
verify captions and native input stay aligned and hit regions match visible
controls. Continue the full [runtime matrix](RUNTIME_VERIFICATION.md), including
input/focus, preferences and lifecycle checks. Native confirmation and UI
acceptance remain pending. No public release has been authorized.

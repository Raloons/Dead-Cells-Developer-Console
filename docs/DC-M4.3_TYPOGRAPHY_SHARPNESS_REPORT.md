# DC-M4.3 — Native-resolution typography & rendering precision

**Milestone:** DC-M4.3, following DC-M4.2 (`f8581e2`).
**Version:** 0.1.0-alpha; assembly/mod remains 0.1.0.
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Status:** offline-complete; native visual acceptance pending. No game launched for M4.3.

## 1. R11 problem

The director found R11 (M4.2) legible — severe pixelation and missing strokes
resolved — but still visibly soft versus a native desktop application,
especially the title, sidebar, small metadata, output and status text.
Screenshots were supplied as conversation attachments (primary evidence).

## 2. Pipeline map: the string `Console`

| # | Stage | What happens | Resampling? |
|---|---|---|---|
| 1 | Caption plan | `WorkbenchPresentation` emits `Caption("Console", 13·s, sans, strong)`; `UiText.Fit` measures with GDI Segoe UI metrics | No |
| 2 | Rasterization | GDI `GetGlyphOutlineW(GGO_GRAY8_BITMAP)`: hinted grayscale coverage 0–64 per glyph at quantized pixel size | Source AA |
| 3 | Atlas pack | Coverage → alpha (`cov·255/64`), RGB white; 512-wide shelf, 2px gutters; ascent/lineHeight from `TEXTMETRIC` | No |
| 4 | Upload | `Bytes.alloc` → `setInt32` → `Pixels(RGBA)` → `Texture.fromPixels` (no `MipMapped` flag → `mipMap=None`) | No |
| 5 | Font bind | `FontChar(Tile at glyph rect, advance)`, `dx=origin.x`, `dy=ascent−origin.y`; `Text.font`, `textColor`, raw `set_text` | No |
| 6 | Layout | Heaps `Text.initGlyphs`: pen advances by integer `advance`; quads at integer `(pen+dx, lineTop+dy)`; node at rounded `(x, y)`, scale 1 | No |
| 7 | Batch | Glyphs batch through `TileGroup.drawWith(ctx, Text)` using the **Text's** parameters | No |
| 8 | Sampling | Sampler from `Text.smooth` → was forced **bilinear** (R11); now **nearest**. UVs are exact texel boundaries (`x/texW`) | **Yes — fixed** |
| 9 | Present | Scene → backbuffer → display. Scene units matched a 2560×1440 display in R11 logs; any residual scale is engine-side | Unknown — instrumented |

## 3. Confirmed contributors (with evidence)

### 3.1 Forced bilinear sampling — the main softener

Upstream Heaps: `Texture.filter` defaults to `Linear`, and R11 additionally set
every label `smooth = true` (docs: smoothing makes scaling "less crisp").
`Text.draw` batches glyphs with the Text's own parameters, so that flag
governed glyph sampling. Bilinear re-blurs already-antialiased coverage on
every edge whenever the sample phase isn't texel-perfect — and phase is never
guaranteed (subpixel bearings, present-scale, DPI virtualization).

Controlled harness (`tests/.../SamplerHarness.cs`, real 14px atlas asset,
Heaps-identical layout math, both samplers, 1.0×/1.25×/1.5×):
`docs/previews/DC-M4.3/type-sampling/`, `metrics.json`:

| Variant @1.0× | Edge ramp | Fringe lum |
|---|---|---|
| A — R11 baseline (dark gutters, bilinear) | 8.07 px | 118.6 |
| B — white gutters, bilinear | 8.07 px | 133.7 |
| **C — white gutters, nearest (selected)** | **1.96 px** | **150.6** |
| D — semibold + nearest | 1.40 px | 150.3 |
| E — threshold extreme + nearest | 1.00 px | — |

Nearest holds its advantage at 1.25× (2.45 vs 10.08) and 1.5× (2.94 vs 12.10):
under any upscale it stays tight where bilinear smears (compare
`variant-a@1.50x.png` vs `variant-c@1.50x.png`: "Explorer", lowercase rows).
At perfect phase bilinear converges to nearest; real pipelines don't hold
perfect phase, and nearest is phase-invariant. Fix: `smooth = false` on labels
**and** their `TileGroup`s (belt and braces; `drawWith` uses the Text), plus
`texture.filter = Nearest` on owned atlas textures only. Nothing global changed.

### 3.2 Transparent-black gutters — dark fringe

The atlas buffer was zero-initialized: non-glyph texels were `(0,0,0,0)`
while glyph texels were `(255,255,255,α)`. Any linear edge sample blending a
neighbor gutter darkened the fringe (fringe luminance 118.6 vs 133.7 with
white gutters, same coverage). Fix: clear the atlas to `(255,255,255,0)`.
Zero runtime cost; strictly more correct under either filter.

### 3.3 Scene vs display scale — instrumented, not assumed

R11 transplant logs (`(36,1415)` Classic full-width input; `(300,1283)`
Workbench input) solve to a **2560×1440 scene**, i.e. scene units = display
pixels on the director's machine — no virtual downscale in that session. But
`dc.Main.pixelScale`, `Scene.zoom`, OS DPI virtualization, or windowed-mode
client mapping could still interpose a scale. Read-only proxy survey confirms
all four checkpoints are reachable: `Scene.width/height/zoom`,
`Main.pixelScale`, `Window.windowWidth/Height`, `Engine.width/height`.
A VerboseLog-gated one-line diagnostic (`LogDisplayChain`: scene, zoom,
pixelScale, window + Win32 client rect) now records the exact chain on the
next authorized session. No behavior changes when logging is off.

## 4. Rasterization analysis

- GDI gray8 is hinted grayscale — appropriate for transparent atlases.
  ClearType (subpixel RGB) was rejected: composited/scaled subpixel coverage
  produces color fringes; grayscale is the correct source.
- Semibold (D) measures marginally tighter (1.40 vs 1.96) — headings were
  already strong/600 and stay so; body stays regular (no bold-everything).
- Threshold (E) is the sharpness ceiling and the jaggedness warning: kept as
  reference only; AA coverage is preserved.
- GDI+ / DirectWrite / FreeType / Skia evaluated: no evidence of meaningful
  gain over hinted GDI gray8 for this pipeline; all add packaging/dependency
  cost. Rejected.
- Font sizes unchanged: the complaint is blur, not size; layout stability
  preserved (all 849 layout checks still pass unmodified).

## 5. Changes implemented

1. `GdiFontAtlas`: white-transparent gutter clear.
2. `UiFonts.CreateFace`: atlas texture `filter = Nearest`.
3. `DevPanel`: label pool + hi-fi path `smooth = false` (Text and `glyphs`
   TileGroup); fallback path untouched.
4. `DeveloperConsoleMod`: transplanted + cohesive input `smooth = false`.
5. `SyncPanel`: `LogDisplayChain` verbose diagnostic (scene/zoom/pixelScale/
   window/client).
6. `SamplerHarness` + 6 ordering/determinism checks; 18 sampler PNGs +
   `metrics.json` under `docs/previews/DC-M4.3/type-sampling/`.
7. `TypographyTests`: white-gutter atlas contract.
8. Docs/NATIVE_API_AUDIT: sampler plumbing + Engine/Scene/Window survey.

Preserved: opaque backdrop, `posChanged` invalidation, `Console` identity,
concise copy, layout/spacing, all commands and safety behavior, CJK/game-font
fallback, bounded 32-face cache, upload-once policy.

## 6. Rejected alternatives

- Global filter change (only owned font textures touched).
- External overlay / injected present hook (fragile, out of safety bounds).
- Render-target control (no engine API; documented via survey).
- SDF fonts (no proxy/engine path).
- Size inflation or artificial contrast (masks, doesn't fix).
- Rewriting `setInt32` packing order (visually correct in screenshots; not
  provable offline — left untouched, flagged for native review).

## 7. Performance / resources

Atlas bytes identical in size (+one fill loop at build, one-time). Nearest is
cheaper than bilinear. No new per-frame work; cache still ≤32 faces, shared
across labels + input. `LogDisplayChain` allocates nothing unless VerboseLog.

## 8. Validation

- Suite: **850 checks, 0 failures** (849 inherited + white-gutter contract);
  sampling run: **856 checks, 0 failures** (+6 sampler order/determinism).
- Release + Debug builds, zero warnings/errors.
- Privacy audit, deterministic packaging (rebuilt identically, checksums
  verified), R12 fixture admission VerifyOnly, auth-gate/installer/outcome
  negatives: pass (see `dist/DC-M4.3/` logs).
- R12 DLL `6D27BA815FD50244D3C4C97DA3A50149EE0ED614A4130D62D2C05EE89E1AC193`
  (300032 bytes) identical across build/fixture/package; ZIP sealed via the
  adjacent `.sha256` sidecar (see `dist/DC-M4.3/seal.log` for the sealed hash).
- Candidate: `dist/DC-M4.3/DeveloperConsole-0.1.0-alpha.zip` (+`.sha256`);
  R11 evidence and all prior ZIPs preserved untouched.
- Public source export local-only; no remote/push/publication.

## 9. Known limits & remaining native verification

- Sampler PNGs simulate the sampling stage with the real asset; they are not
  engine captures — labeled as such.
- Native phase/present-scale still unmeasured: enable `VerboseLog`, run the
  R12 fixture under fresh authorization, collect the `display chain` line.
- Verify: title/sidebar/metadata/output/input sharpness vs R11 screenshots;
  descenders/brackets/accents; all four modes; 720p→1440p + DPI steps;
  open/close/resize/mode-cycle stability; full R01–R20/H01 + M4.1-01…08.
  R11 authorization does not cover the changed DLL. No release authorized.

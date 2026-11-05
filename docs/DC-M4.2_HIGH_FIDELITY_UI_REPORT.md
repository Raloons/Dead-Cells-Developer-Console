# DC-M4.2 — High-fidelity UI rendering, typography repair & minimalist refinement

**Milestone:** DC-M4.2, following DC-M4.1 R10 (`8dabd5e`).
**Version:** 0.1.0-alpha; assembly/mod remains 0.1.0.
**Target:** Dead Cells v35 / DCCM 35.13.8 / .NET 10.
**Status:** offline-complete; native visual acceptance pending. No game launched for M4.2.

## 1. Observed problems

The director inspected R10 Workbench inside Dead Cells (screenshots supplied as
conversation attachments, not workspace files; taken as primary evidence per the
brief):

- UI text visibly pixelated; large headings rough, small labels hard to read.
- Damaged glyphs: missing/cut-off strokes in Classic help and Workbench labels.
- Cramped controls (catalog rows, spawn builder, Preferences) and large empty
  Home workspace with tiny cards at the top.
- Verbose copy throughout: "Your development workspace", "Recognizable names.
  Canonical identities. Deliberate spawning.", per-section "Click an action…",
  "Command complete. See Output for the result.", subtitles under cards.
- Verbose header: "Developer Console | help · search · spawn" and
  "DEVELOPER / WORKBENCH".

Synthetic SVG previews looked cleaner than gameplay, indicating offline previews
did not model the native font pipeline. This milestone addresses the pipeline
first, then the copy/layout.

## 2. Root cause

Native text path (verified in source + installed `GameProxy` metadata +
upstream Heaps source):

- Single game font: `dc.Assets.font12` → `h2d.Font`, `FontType.BitmapFont`,
  12 px bitmap glyph atlas (`tile`, `glyphs`, `FontChar(t, width)`).
- Every Workbench/Classic/Compact/Inspector label reused that atlas through
  pooled `h2d.HtmlText` nodes with fractional scaling:
  `scale = displaySize / 12` (e.g. 25 px heading ≈ 2.08× upscale, 10 px
  metadata ≈ 0.83× downscale). `TextInput` (transplanted command field) was
  scaled by `InputScale * 4/3`.
- Bitmap glyphs scaled this way blur (upscale) or drop strokes (downscale);
  fractional scales plus subpixel `x/y` round bearings (`dx/dy`) and advances
  inconsistently, clipping ascenders/descenders at small sizes.
- No SDF/TTF API is exposed through the proxy (`Font.type` is BitmapFont;
  `Text` auto-smooth applies only to SDF). There is no supported API to change
  the engine backbuffer or present-scale; virtual scene units are mapped to the
  Win32 client rect for hit-testing (`sceneWidth / bounds.Right`), so some
  final present scaling is outside mod control. Documented as the residual
  limitation: exact-size raster + linear filtering + integer snapping is the
  highest viable correction within the current overlay.

Proxy inspection was read-only (`System.Reflection.Metadata`, reflection-only
assembly load). REA full managed inventory exceeded its representation budget;
the targeted probe did not load or execute the game.

## 3. Correction implemented

### 3.1 Exact-size GDI glyph atlases (no shipped fonts)

- `GdiFontAtlas.Build(size, mono, strong)`: Windows GDI
  `GetGlyphOutlineW(GGO_GRAY8_BITMAP)` rasters **Segoe UI** (sans) /
  **Consolas** (mono) — system faces, never distributed — at the exact
  quantized display size (10–40 px set). Coverage (0–64) maps to alpha;
  RGB is white so `textColor` tints correctly.
- 512 px-wide shelf packing, 2 px gutters, ≤2048 px height; per-glyph
  `dx = origin.x`, `dy = ascent − origin.y`, `advance = cellIncX`
  (verified against 'A' baseline, 'g/p/y/j' descenders, '|[]{}()' brackets,
  'É/é' accents). Transparent texel backs spaces; '?' is `defaultChar`.
  Charset covers ASCII, Latin-1, Latin Extended-A, punctuation/arrows,
  Greek and Cyrillic (~760 glyphs at 14 px).
- `UiFonts.TryGet`: one cached `h2d.Font` per (size, family, weight) (≤32
  entries; typical session uses ~6–8, ≈360 KB each). Atlas texture uses
  `Filter.Linear`. No per-frame rasterization; no logs dependency (test host
  lacks the game Serilog assembly by design).
- `DevPanel`: pooled `h2d.Text` (not `HtmlText`), `font = face.Font`,
  `textColor` from caption hex, raw `set_text` (no HTML escaping artifacts),
  `scale = 1`, positions rounded to integers. Unsupported scripts
  (e.g. CJK) fail closed to the game bitmap font at the legacy scale.
- Command input: transplanted `TextInput.font` set to the mono face at
  `Quantize(14 × InputScale)` (Workbench) / `Quantize(14 × TextScale)`
  (cohesive Classic), `scale = 1`, integer placement, `maxWidth` in
  unscaled units. Cursor/selection tiles are font-relative and follow.
- Small-viewport Classic fallback and R10 opaque backdrop / `posChanged`
  invalidation are preserved.

### 3.2 Measured typography system

- `UiText`: GDI-measured widths/metrics (Segoe UI/Consolas) with
  deterministic estimator fallback off-Windows. `Fit`/`WrapText` measure
  instead of `size × 0.58` estimates; long command/ID tokens hard-wrap by
  measured characters.
- Roles: app identity 13 strong; heading 24 strong; card value 22 strong;
  body 12–14; buttons 12; metadata 11; dock markers 10 mono; input/mono 12.
  Strong (600 wt) only for headings/values/active nav — no thin-on-black
  body text. Contrast raised for secondary text (`#A8A9BC`/`#7E8092`).
- `Presentation.Svg` now emits baseline (`y + ascent`), real families and
  weights; `Render-UiPreviews.ps1` rasterizes with ClearType + bold for
  strong. Offline previews now share measurement with native; rasterization
  still differs (GDI ClearType vs engine atlas) and is labeled as such.

### 3.3 Concise interface (Requirement B/C)

- Visible identity is exactly **Console** in Classic/Compact/Inspector and
  the Workbench top bar (version stays a small `0.1.0` tag).
- Section headings are single words: Home, Commands, Catalog, Player, Debug,
  Journal, Preferences, Help. All subtitles/descriptive paragraphs removed.
- Home: three value cards (no detail prose) + workflow buttons + Recent list
  with Prepare (fills the former void instead of tiny cards on black).
- Catalog: search + All/Melee/Ranged/Shield/Skill, roomy two-line rows
  (name + `id / category`), `N / shown` pager, Details pane (name, id,
  `category / eligible|lookup`, level, quality, Prepare spawn; aliases only
  when present and space allows).
- Commands: signature + `description [safety]`, Prepare/Help,
  `N commands / page P`.
- Journal: single-line entries + Prepare; `Page N`; `Prepare last` on Recent.
- Player/Debug/Help: label + value/safety tag rows (`[run-state]`,
  `[read-only]`), no instructional paragraphs.
- Dock keeps Output/Problems + `Page Up / Down`; completion keeps
  `Complete`; input keeps `Command >`; footer keeps run status +
  `Alt+1…8 / Enter`. Notices: success/ready silenced (`""`), stage/spawn →
  `Prepared.`, failure → `Failed. See Problems.`, pin → `Pinned.`/`Unpinned.`
- Classic `help` content unchanged (full docs + safety stay); its rendering
  uses the repaired pipeline with measured wrapping.

### 3.4 Spacing/density

Row/entry/catalog steps increased (40/64/58 comfortable; 34/58/52 dense),
buttons 29–34 px tall, input 42 px, dock lines 22 px, Explorer 208 px.
Controls verified non-overlapping and in-bounds at 1280×720, 1600×900,
1920×1080, 2560×1080 × 0.75/1/1.5 × comfortable/dense.

## 4. Alternatives considered

- **Reconfigure native SDF text:** no SDF font or shader API reachable via
  proxy; rejected.
- **Ship a TTF:** unnecessary — GDI rasterizes installed system faces at
  runtime; nothing is copied or distributed (license-safe).
- **Match display resolution 1:1:** no engine API for backbuffer/scale;
  integer-snapped exact-size glyphs + linear filtering is the viable max.
- **External overlay / injection for text:** rejected as unstable and out of
  scope for a cosmetic gain.

## 5. Evidence

- 15-view SVG+PNG gallery: `docs/previews/DC-M4.2/` (`index.html`,
  `workbench-home/catalog/commands/player/debug/journal/settings/help…`,
  720p catalog). Before → after: `DC-M4.1*` → `DC-M4.2` same view names.
- Classic title/help, Home, Catalog, Player, Journal, Preferences, Help,
  input and footer are all covered by gallery views + dock/completion states.
- Pixel crops were not fabricated; native before/after photography awaits an
  authorized session.

## 6. Validation

- **849 offline checks, 0 failures** (792 inherited + 57 new: quantization,
  measurement, fit/wrap, atlas bounds/packing/advances/baseline/ink,
  fail-closed fonts, `Console` titles, heading presence, filler ban list,
  type ranges, card/footer/notice contracts).
- Release + Debug builds, zero warnings/errors.
- Privacy audit + public-source hygiene + deterministic packaging + fixture
  admission (R11 VerifyOnly) + authorization-gate negatives: pass (see
  `dist/DC-M4.2/` logs).
- Candidate: `dist/DC-M4.2/DeveloperConsole-0.1.0-alpha.zip` (+ `.sha256`);
  DLL identity recorded in the sealed fixture manifest. Original DC-M4.1 ZIP
  and all R9/R10 evidence preserved untouched.

## 7. Performance / resources

Atlas ≈ 512×176×4 ≈ 360 KB per face; ≤32 cached, typical ≤8 (≈3 MB),
uploaded once and shared by all labels + input. No per-frame raster; text
rebuilds only on content/font change as before. No render-target,
batching or redraw-frequency changes.

## 8. Remaining native verification (pending, needs fresh authorization)

Solid workspace/header after Classic→Workbench, section switches,
close/reopen, resize, mode cycling; caption/input alignment vs hit regions;
clip checks (capitals, descenders g/j/p/q/y, brackets, accents, digits,
mono); 720p/900p/1080p/1440p + scales; preferences lifecycle; full
R01–R20/H01 + M4.1-01…08 matrix. R9/R10 authorizations do not cover the
changed M4.2 DLL. No release authorized.

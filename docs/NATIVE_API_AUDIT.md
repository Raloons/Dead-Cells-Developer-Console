# Native API audit

This audit records the compile-time and installed-proxy evidence gathered
before live verification. It is **static evidence**, not a substitute for the
runtime matrix.

## Installed framework

| Item | Value |
|---|---|
| DCCM | 35.13.8 (`coremod/ModCoreVersion.txt`) |
| MDK package | `DeadCellsCoreModding.MDK` 1.0.1 |
| Game | Dead Cells v35 |

The installed `GameProxy` cache and the MDK reference proxy have different
file sizes; the reference proxy is a reduced compile-time surface. The members
used by the console were checked in both where applicable.

## Verified signatures

### Console hooks and UI

`dc.h2d.Hook_Console` exposes:

```text
orig_handleCommand(Console self, String command)
orig_log(Console self, String text, int? color)
public static event hook_handleCommand handleCommand
public static event hook_log log
```

Both the reference and installed-cache proxies show the same delegate shapes.
The console attaches to both events and preserves the original delegate.

`dc.h2d.Console` exposes:

```text
int width; int height; Bitmap bg; TextInput tf; HtmlText logTxt;
ArrayObj logs; int logIndex;
void show(); void hide(); void handleCommand(String command);
void log(String text, int? color); bool isActive();
```

The overlay uses these members exactly as compiled. `bg.visible` drives the
engine input lock; the transplant layout keeps it alive.

### Hero and stat methods

`dc.en.Hero` exposes:

```text
void computeTiers()
void setLifeAndRally(int life, int rally)
void setBrutalityTier(int v)
void setTacticTier(int v)
void setSurvivalTier(int v)
void hudSetBrutality(int value, Ref<bool> animated)
void hudSetTactic(int value, Ref<bool> animated)
void hudSetSurvival(int value, Ref<bool> animated)
void addMoney(int v, Ref<bool> noStats)
void kill()
double invisibilityTimer; int curseCounter; Inventory inventory;
```

The console binds to these members through the installed proxy. `life`,
`maxLife`, `cx`, `cy`, `_level`, and `destroyed` are inherited entity members
used by the guarded diagnostics.

### Inventory and scroll authority

The installed proxy exposes:

```text
ArrayObj items;
InventItem add(InventItem i);
void remove(InventItem i);
bool removeOne(String k);
void removeAll(String k);
int countItemKind(String ik);
int countBrutalityUp(Ref<bool> withAffix);
int countTacticUp(Ref<bool> withAffix);
int countSurvivalUp(Ref<bool> withAffix);
```

Decompiled v35 bytecode confirms:

- `Hero.computeTiers` uses `inventory.count*Up(null)`.
- `count*Up` starts with `countItemKind("BrutalityUp"/"TacticUp"/
  "SurvivalUp")` and adds matching affixes from equipped items.
- `countItemKind` iterates `inventory.items` and matches the item kind string.
- `Inventory.add` pushes the item into `items` and calls `equip`.
- `Inventory.serialize` writes `items`; `unserialize` restores `items`.
- `Hero.serialize` adds `this.inventory` as a serialized known reference.
- `InventItem.fromItem("BrutalityUp"/"TacticUp"/"SurvivalUp", null)` produces
  a `BagItem`, which is the native scroll item kind.
- Native scroll pickup uses `fromItem` + `inventory.add` and then
  `onEquipedItemsChange`.
- `Hero.updateMaxLife` recomputes `maxLife` through
  `Const.scaleHeroLifeToTier` and preserves the current life, rally, and
  bonus ratios.

The console's `TryAdjustNativeScrolls` uses that path. `onEquipedItemsChange`
has both `Ref<bool>` and `ref bool` overloads; the console uses the `ref bool`
form with `updateHUD=true` so `computeTiers` and the HUD refresh run.

### Items and forge

`dc.tool.InventItem` exposes:

```text
InventItem addAffix(String k, Ref<bool> ignoreChecks)
InventItem addAffix(String k, ref bool ignoreChecks)
int getRawItemLevel()
int getUpgradeLevel()
int setItemLevel(int v)
```

`dc.level.LootGen` exposes:

```text
InventItem finalizeItem(InventItem i, int? iLevelBonus, Ref<bool> overrideBaseLevel, Room r, ArrayObj tags)
InventItem finalizeItem(InventItem i, int? iLevelBonus, ref bool overrideBaseLevel, Room r, ArrayObj tags)
InventItem finalizeLegendaryItem(InventItem i, int? iLevelBonus, Ref<bool> overrideBaseLevel, Room r, ArrayObj tags)
InventItem finalizeLegendaryItem(InventItem i, int? iLevelBonus, ref bool overrideBaseLevel, Room r, ArrayObj tags)
```

The console uses the `ref bool` overloads and reads back level and quality
before dropping the item.

`dc.en.inter.ItemDrop` exposes the constructor:

```text
ItemDrop(Level lvl, int x, int y, InventItem i, bool isNew, Ref<bool> inArmory)
```

The verified drop path is constructor → `init()` → `setPosCase` → `onDropAsLoot()`.

The static loot generators are present:

```text
dc.en.inter._RandomWeaponSpawner.lootGen : LootGen
dc.en.inter._TrainingWeaponSpawner.lootGen : LootGen
```

### Boot, assets, constants, and item names

```text
dc.Boot.update(double dt)
dc._Assets.font12 : Font
dc._Const.ROOT_DP_DEBUG : int
dc._Main.ME : Main
dc._ItemTools.getBlueprintLocalizedName :
    HlFunc<String, virtual_ambiantDesc_..._tier1_tier2_>
```

The item-name helper is a function field on the class object. **It is no longer
called:** R4 startup aborted with an access violation during that lookup.
The preserved DC-M3A GetText candidate uses
`ModCore.Modules.GetText.Instance.GetString(source)` instead; DC-M4 delays it
until active gameplay. It remains runtime-unverified. English aliases are read
locally from the installed `lang/main-en.pot` with a pure bounded reader.
Fallbacks are English name, raw row name, then ID; no translation is fabricated.

### DC-M4 renderer additions (compile-time verified only)

The presentation uses `Tile.fromColor`, `Bitmap`, `HtmlText` and root
`addChildAt` as before. Bitmap tint is a `dc.h3d.Vector`, not an integer;
its constructor takes `Ref<double>` RGBA components. Labels and native input
use position/scale/maxWidth fields. Pooled nodes detach with `remove()` on
reset or initialization failure. Actual layering, font metrics, focus and
native cleanup behavior remain manual gates. Original geometry is used as
artwork; there is no texture/shader asset dependency.

### DC-M4.4 simplification (offline/compile-time only)

The Workbench, custom font atlases and pointer navigation were removed; the
sections below are historical. The single overlay reuses the same h2d
primitives, the native `font12` font and the Boot.update hook.
`PresentationPlan` supplies the input row geometry; native field
coordinates/scale/maxWidth follow that plan. Pool growth reorders existing
labels and the native field above new surfaces. `SceneTransform` still
invalidates cached matrices after pooled updates.

Read-only `user32` key/focus APIs (`GetAsyncKeyState`, `GetForegroundWindow`)
supply toggle/completion/scroll edges while open. No key injection, process
launching, file browsing, shell or networking service is exposed. DPI
virtualization, keyboard modifiers, layering, text metrics, field focus and
lifecycle remain native acceptance gates.

### Retired UI experiments (M4.2–M4.3, historical)

M4.2 built GDI-rasterized system-font atlases and M4.3 switched them to
nearest sampling with white gutters, supported by a sampler harness. Both were
removed with the Workbench in M4.4; the console uses the native `font12`
bitmap font. Details remain in the archived
[DC-M4.2](DC-M4.2_HIGH_FIDELITY_UI_REPORT.md) and
[DC-M4.3](DC-M4.3_TYPOGRAPHY_SHARPNESS_REPORT.md) reports, excluded from the
public package.

### R10 scene-transform correction

The R9 screenshot exposed backing surfaces remaining at old positions after a
Classic→Workbench switch. Installed GameProxy metadata shows `x`, `y`, `scaleX`,
`scaleY` and boolean `posChanged` properties. Their generated CLR accessors use
field access (`SetValueFieldById`/`GetValueFieldById`); calling a C# property
accessor is not equivalent to running a Haxe inline transform setter.
Upstream Heaps `h2d.Object` documents and implements cached matrices controlled by
`posChanged`. `SceneTransform.Apply` now writes position/scale and explicitly sets
that flag for rectangles, captions and native input, including cohesive restore.
This is an implementation correction supported by proxy metadata and upstream
source; actual fixed rendering still requires an R10 native retest.

### Mod loader reflection

`ModCore.ModLoader` is internal and declares:

```text
public readonly Dictionary<string, ModInfo> modInfos
```

`ModCore.Module<TModule>.Instance` is a public static property. `debug mods`
uses reflection over `Module<ModLoader>.Instance.modInfos` and reads public
`Name`/`Version` properties. It is read-only and reports `unavailable` when
the host does not expose the shape.

## Residual risks

These are the areas that only a live session can settle:

1. GetText timing/translation behavior and installed English-template discovery;
   the crashing ItemTools helper is not used.
2. Dynamic `Data.item.all[]` / `byId.keys()` enumeration timing and shape.
3. `LootGen` availability at the moment of a spawn command.
4. `ItemDrop` ownership/lifecycle after the drop.
5. Hook dispatch and tray lifetime across level transitions.
6. Reflection into the internal mod loader.
7. Native scroll item serialization/restoration through an actual save/resume
   cycle, including the absence of duplicate scrolls or health bonuses.

The runtime matrix in `docs/RUNTIME_VERIFICATION.md` is designed to address
each of these with an observed result.

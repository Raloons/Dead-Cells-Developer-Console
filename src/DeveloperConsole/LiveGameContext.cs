// Live Haxe-backed context. Managed failures are guarded; native interop still
// requires disposable runtime verification (managed catch cannot contain AVs).
using System.Collections;
using System.Reflection;
using dc.en;
using dc.tool;
using Hashlink.Marshaling;
using HaxeProxy.Runtime;
using ModCore.Mods;
using ModCore.Utilities;
using Serilog;

namespace DeveloperConsole;

// Wraps the live hero object behind IHeroFacade.
public sealed class LiveHero(dc.en.Hero hero) : IHeroFacade
{
    private readonly dc.en.Hero h = hero;
    internal object Raw => h;
    internal nint Key => ((HaxeProxyBase)(object)h).HashlinkPointer;

    public int Life => h.life;
    public int MaxLife => h.maxLife;
    public double Cx => h.cx;
    public double Cy => h.cy;

    public int GetTier(string stat) => stat switch
    {
        "brutality" => h.brutalityTier,
        "tactics" => h.tacticTier,
        "survival" => h.survivalTier,
        _ => throw new ArgumentException($"unknown stat '{stat}'"),
    };

    public void SetTier(string stat, int value)
    {
        switch (stat)
        {
            case "brutality": h.setBrutalityTier(value); break;
            case "tactics": h.setTacticTier(value); break;
            case "survival": h.setSurvivalTier(value); break;
            default: throw new ArgumentException($"unknown stat '{stat}'");
        }
    }

    public void HudSet(string stat, int value, bool animated)
    {
        bool a = animated;
        var r = new Ref<bool>(ref a);
        switch (stat)
        {
            case "brutality": h.hudSetBrutality(value, r); break;
            case "tactics": h.hudSetTactic(value, r); break;
            case "survival": h.hudSetSurvival(value, r); break;
            default: throw new ArgumentException($"unknown stat '{stat}'");
        }
    }

    public void Heal(int amount)
    {
        // Never compute life + amount directly: a huge command argument would
        // overflow int and could pass a negative value to setLifeAndRally.
        int room = Math.Max(0, h.maxLife - h.life);
        int applied = Math.Clamp(amount, 0, room);
        h.setLifeAndRally(h.life + applied, 5);
    }

    public void SetLife(int value)
    {
        var target = Math.Clamp(value, 0, h.maxLife);
        h.setLifeAndRally(target, 5);
    }

    public void GiveGold(int amount)
    {
        bool noStats = false;
        h.addMoney(amount, new Ref<bool>(ref noStats));
    }

    public void Kill() => h.kill();
}

// Live game context. The Data object is stashed from IOnAfterLoadingCDB, then
// the equipment catalog is rebuilt from the live item sheet. This is what
// makes the standalone console independent of any generated content list.
public sealed class LiveGameContext : IGameContext
{
    public static LiveGameContext Instance { get; } = new();
    private dynamic? data;
    private CatalogSnapshot snapshot = CatalogSnapshot.Empty;
    private CatalogIndex index = CatalogIndex.Empty;
    private int refreshCount;
    private EnglishNames englishNames = new();
    private bool languageReady;

    private LiveGameContext() { }

    public void NoteData(dynamic value)
    {
        data = value;
        languageReady = false;
        englishNames = EnglishNames.Load(EnglishNames.FindGameRoot());
        RebuildCatalog();
        try
        {
            Serilog.Log.Information("DeveloperConsole: live catalog linked ({N} item rows via {S})",
                snapshot.Items.Count, snapshot.Source);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("DeveloperConsole: catalog link log failed: {E}", ex.Message);
        }
    }

    public CatalogSnapshot Catalog => snapshot;
    public IReadOnlyList<CatalogItem> Items => snapshot.Items;

    public bool RefreshCatalog(out string message)
    {
        if (data == null) { message = "CDB is not loaded yet."; return false; }
        languageReady = InGameplay;
        englishNames = EnglishNames.Load(EnglishNames.FindGameRoot());
        RebuildCatalog();
        message = $"Catalog refreshed: {snapshot.Items.Count} entries. {englishNames.Status}";
        return snapshot.Items.Count > 0;
    }

    public IHeroFacade? Hero
    {
        get
        {
            try
            {
                var h = ModCore.Modules.Game.Instance.HeroInstance;
                if (h == null) return null;
                var wrap = new LiveHero(h);
                StatBonus.ResetFor(wrap.Key);
                return wrap;
            }
            catch { return null; }
        }
    }

    public bool InGameplay
    {
        get
        {
            try
            {
                var h = ModCore.Modules.Game.Instance.HeroInstance;
                if (h == null) return false;
                dynamic hero = h;
                if (hero._level == null) return false;
                try { if ((bool)hero.destroyed) return false; } catch { }
                return true;
            }
            catch { return false; }
        }
    }

    // ---- catalog ---------------------------------------------------------

    private void RebuildCatalog()
    {
        var items = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        string source = "not loaded";
        if (data != null)
        {
            if (!TryAll(items, out var allSource))
                Serilog.Log.Information("DeveloperConsole catalog: {W}", allSource);
            if (items.Count == 0 && !TryKeys(items, out var keySource))
                Serilog.Log.Information("DeveloperConsole catalog: {W}", keySource);
            source = items.Count > 0
                ? (TryAllSource ?? "live CDB item rows")
                : "live CDB unavailable";
        }
        var ordered = items.Values.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
        refreshCount++;
        snapshot = new CatalogSnapshot(ordered, source, DateTime.UtcNow, refreshCount);
        index = snapshot.Index;
    }

    private string? TryAllSource;

    private bool TryAll(Dictionary<string, CatalogItem> items, out string why)
    {
        why = "";
        try
        {
            var arr = (dc.hl.types.ArrayDyn)(object)((dynamic)data!).item.all;
            int added = 0;
            foreach (dynamic row in arr)
            {
                try
                {
                    string id = row.id?.ToString() ?? "";
                    if (string.IsNullOrEmpty(id)) continue;
                    var item = FromRow(row, id, "item.all[]");
                    if (items.TryAdd(item.Id, item)) added++;
                }
                catch { }
            }
            why = $"item.all[] added={added}";
            if (added > 0)
            {
                TryAllSource = "live CDB item.all[]";
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            why = "item.all[]: " + ex.Message;
            return false;
        }
    }

    private bool TryKeys(Dictionary<string, CatalogItem> items, out string why)
    {
        why = "";
        try
        {
            dynamic sheet = ((dynamic)data!).item;
            var keys = (Hashlink.Virtuals.virtual_hasNext_next_<HlFunc<dc.String>>)
                (object)sheet.byId.keys();
            int added = 0;
            foreach (var k in keys)
            {
                try
                {
                    string id = k.ToString();
                    dynamic row = sheet.byId.get(k);
                    var item = FromRow(row, id, "item.byId.keys");
                    if (items.TryAdd(item.Id, item)) added++;
                }
                catch { }
            }
            why = $"item.byId.keys added={added}";
            if (added > 0)
            {
                TryAllSource = "live CDB item.byId.keys";
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            why = "item.byId.keys: " + ex.Message;
            return false;
        }
    }

    private CatalogItem FromRow(dynamic row, string id, string source)
    {
        string? nativeName = null;
        try { nativeName = row.name?.ToString(); } catch { }
        int group = -1;
        try { group = (int)row.group; } catch { }
        bool legendaryKnown = false, hasLegendary = false;
        try
        {
            dynamic affixes = row.legendAffixes;
            int n = (int)affixes.length;
            hasLegendary = n > 0;
            legendaryKnown = true;
        }
        catch { }
        // Do NOT invoke the native ItemTools localization helper here. Its
        // live invocation aborted startup with an access violation in the
        // standalone fixture. DCCM's GetText wrapper is a managed, bounded
        // lookup over the game's current language table; if it is unavailable
        // or returns the source string, the row name and id remain fallbacks.
        return CatalogItem.FromRow(id, nativeName, (languageReady ? Localize(nativeName) : null) ?? englishNames.Get(id), group,
            hasLegendary, legendaryKnown, source) with { EnglishName = englishNames.Get(id) };
    }

    private static string? Localize(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        try
        {
            string? translated = ModCore.Modules.GetText.Instance.GetString(source!);
            if (!string.IsNullOrWhiteSpace(translated) &&
                !string.Equals(translated, source, StringComparison.Ordinal))
                return translated;
        }
        catch { }
        return null;
    }

    public bool TryResolveItem(string query, out CatalogItem item, out string? error)
    {
        if (index.Count == 0)
        {
            item = null!;
            error = "item database not loaded yet; start a run and reopen the console";
            return false;
        }
        return index.TryResolve(query, out item, out error);
    }

    public object? GetItemRow(string id)
    {
        try
        {
            if (data == null) return null;
            dynamic row = ((dynamic)data).item.byId.get(id.AsHaxeString());
            return (object)row;
        }
        catch { return null; }
    }

    public object CreateItem(string id)
    {
        var item = snapshot.Items.FirstOrDefault(i =>
            i.Id.Equals(id, StringComparison.Ordinal));
        if (item == null)
            throw new InvalidOperationException($"item is not in the live catalog: '{id}'");
        if (!item.IsEquipment)
            throw new InvalidOperationException($"unsupported item type: '{id}' group {item.Group}");
        if (!item.Spawnable)
            throw new InvalidOperationException($"'{id}' is not spawnable: {CatalogPolicy.SpawnReason(item)}");
        return CreateFactoryItem(id);
    }

    public string EquipmentSummary(object item)
    {
        try
        {
            var native = ConvertToItem(item);
            if (native == null) return "level/quality read-back unavailable";
            int level = native.getRawItemLevel(), quality = native.getUpgradeLevel();
            return $"level {level}, quality {EquipmentForge.TierName(quality, false)}";
        }
        catch { return "level/quality read-back unavailable"; }
    }

    // Raw factory access for native items that are not ordinary equipment
    // drops (currently the three scroll items). Callers must validate policy.
    private static object CreateFactoryItem(string id)
    {
        var fn = HashlinkMarshal.FindFunction("tool.$InventItem", "fromItem");
        return fn.DynamicInvoke(id.AsHaxeString(), null)
            ?? throw new InvalidOperationException($"factory returned no item for '{id}'");
    }

    public bool TryAdjustNativeScrolls(string stat, int delta, out int nativeCount,
        out int effectiveTier, out string? error)
    {
        nativeCount = 0;
        effectiveTier = -1;
        error = null;

        string scrollId;
        string key;
        switch ((stat ?? "").ToLowerInvariant())
        {
            case "brutality": scrollId = "BrutalityUp"; key = "brutality"; break;
            case "tactics": scrollId = "TacticUp"; key = "tactics"; break;
            case "survival": scrollId = "SurvivalUp"; key = "survival"; break;
            default:
                error = $"unknown stat '{stat}': want brutality, tactics, or survival";
                return false;
        }
        if (delta == 0)
        {
            error = "scroll delta must not be zero";
            return false;
        }
        if (delta > 100 || delta < -100)
        {
            error = "scroll delta is limited to 100 per command";
            return false;
        }

        dc.en.Hero? hero;
        try { hero = ModCore.Modules.Game.Instance.HeroInstance; }
        catch { hero = null; }
        if (hero == null || hero._level == null || hero.destroyed)
        {
            error = "no live character: start a run first";
            return false;
        }

        try
        {
            int before = hero.inventory.countItemKind(scrollId.AsHaxeString());
            if (delta < 0)
            {
                int remove = -delta;
                if (before < remove)
                {
                    error = $"only {before} {key} scroll(s) available; no changes made";
                    return false;
                }
                for (int i = 0; i < remove; i++)
                {
                    if (!hero.inventory.removeOne(scrollId.AsHaxeString()))
                    {
                        error = $"native scroll removal failed after {i} removal(s)";
                        break;
                    }
                }
                if (error != null) return false;
            }
            else
            {
                int added = 0;
                try
                {
                    for (int i = 0; i < delta; i++)
                    {
                        var raw = CreateFactoryItem(scrollId);
                        var item = ConvertToItem(raw)
                            ?? throw new InvalidOperationException("factory returned no scroll item");
                        hero.inventory.add(item);
                        added++;
                        try { hero.hudOnItemPick(item, hero.cx, hero.cy); } catch { }
                    }
                }
                catch (Exception ex)
                {
                    // Roll back only the items this call added. Scroll items are
                    // fungible, so removing the same number restores the count.
                    for (int i = 0; i < added; i++)
                    {
                        try { hero.inventory.removeOne(scrollId.AsHaxeString()); } catch { }
                    }
                    error = $"native scroll allocation failed: {ex.Message}";
                    return false;
                }
            }

            bool updateHUD = true, duringHeroInit = false, duringItemTransform = false;
            try
            {
                hero.onEquipedItemsChange(ref updateHUD, ref duringHeroInit, ref duringItemTransform);
            }
            catch
            {
                // The inventory is authoritative; recompute the tiers even if
                // the HUD callback is unavailable.
                try { hero.computeTiers(); } catch { }
            }

            nativeCount = hero.inventory.countItemKind(scrollId.AsHaxeString());
            effectiveTier = key switch
            {
                "brutality" => hero.brutalityTier,
                "tactics" => hero.tacticTier,
                _ => hero.survivalTier,
            };
            return true;
        }
        catch (Exception ex)
        {
            error = $"native scroll adjustment failed: {ex.Message}";
            return false;
        }
    }

    public void DropAtHeroFeet(object item)
    {
        var hItem = ConvertToItem(item);
        if (hItem == null) throw new InvalidOperationException("factory returned no item");
        DropConverted(hItem);
    }

    private HashSet<string> EquippedMutationIds(dc.en.Hero hero)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            dynamic items = hero.inventory.items;
            int n = 0;
            try { n = items.length; } catch { n = 0; }
            for (int i = 0; i < n; i++)
            {
                try
                {
                    dynamic? entry = items.getDyn(i);
                    string? id = entry?._itemData?.id?.ToString();
                    int group = entry?._itemData != null ? (int)entry._itemData.group : -1;
                    if (!string.IsNullOrEmpty(id) && group == 12) ids.Add(id);
                }
                catch { }
            }
        }
        catch { }
        return ids;
    }

    private string MutationLabel(string id)
    {
        try
        {
            return snapshot.Items.FirstOrDefault(i => i.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.Label ?? id;
        }
        catch { return id; }
    }

    // Mutation equip/remove through native inventory verbs. Every step is
    // verified (duplicate check, factory, inventory add, equip acceptance,
    // post-state rescan); any mismatch rolls back and reports failure rather
    // than claiming success. Effect activation and exact slot-capacity rules
    // are native behaviors pending live verification.
    public bool TryEquipMutation(string id, out string report, out string? replaced, out string? error)
    {
        report = ""; replaced = null; error = null;
        dc.en.Hero? hero;
        try { hero = ModCore.Modules.Game.Instance.HeroInstance; }
        catch { hero = null; }
        if (hero == null || hero._level == null || hero.destroyed)
        {
            error = "no live character: start a run first";
            return false;
        }
        try
        {
            var before = EquippedMutationIds(hero);
            if (before.Contains(id))
            {
                error = $"Mutation already equipped: {MutationLabel(id)}.";
                return false;
            }
            dc.tool.InventItem item;
            try { item = ConvertToItem(CreateFactoryItem(id))
                ?? throw new InvalidOperationException("factory returned no item"); }
            catch (Exception ex)
            {
                error = $"native factory rejected '{id}': {ex.Message}";
                return false;
            }
            hero.inventory.add(item);
            bool updateHUD = true, duringHeroInit = false, duringItemTransform = false;
            try { hero.onEquipedItemsChange(ref updateHUD, ref duringHeroInit, ref duringItemTransform); }
            catch { try { hero.computeTiers(); } catch { } }
            try { hero.hudOnItemPick(item, hero.cx, hero.cy); } catch { }
            if (!IsMutationActive(hero, id, item))
            {
                // Not auto-active: try the explicit equip verb once, then
                // re-verify. A refusal keeps native slot judgment authoritative.
                try
                {
                    if (hero.inventory.equip(item))
                    {
                        try { hero.onEquipedItemsChange(ref updateHUD, ref duringHeroInit, ref duringItemTransform); }
                        catch { }
                    }
                }
                catch { }
            }
            var after = EquippedMutationIds(hero);
            if (!after.Contains(id) || !IsMutationActive(hero, id, item))
            {
                try { hero.inventory.removeOne(id.AsHaxeString()); } catch { }
                error = $"native equip not confirmed for '{id}'; rolled back";
                return false;
            }
            var lost = before.Where(previous => !after.Contains(previous)).ToArray();
            if (lost.Length > 0) replaced = string.Join(", ", lost);
            report = $"Equipped {MutationLabel(id)}." +
                (replaced == null ? "" : $" Replaced: {replaced}.");
            return true;
        }
        catch (Exception ex)
        {
            error = $"native mutation equip failed: {ex.Message}";
            return false;
        }
    }

    public bool TryRemoveMutation(string id, out string report, out string? error)
    {
        report = ""; error = null;
        dc.en.Hero? hero;
        try { hero = ModCore.Modules.Game.Instance.HeroInstance; }
        catch { hero = null; }
        if (hero == null || hero._level == null || hero.destroyed)
        {
            error = "no live character: start a run first";
            return false;
        }
        try
        {
            if (!EquippedMutationIds(hero).Contains(id))
            {
                error = $"Mutation is not equipped: {MutationLabel(id)}.";
                return false;
            }
            bool removed = false;
            try { removed = hero.inventory.removeOne(id.AsHaxeString()); } catch { }
            if (!removed)
            {
                error = $"native removal failed for '{id}'";
                return false;
            }
            bool updateHUD = true, duringHeroInit = false, duringItemTransform = false;
            try { hero.onEquipedItemsChange(ref updateHUD, ref duringHeroInit, ref duringItemTransform); }
            catch { try { hero.computeTiers(); } catch { } }
            if (EquippedMutationIds(hero).Contains(id))
            {
                error = $"removal not confirmed for '{id}'";
                return false;
            }
            report = $"Removed {MutationLabel(id)}.";
            return true;
        }
        catch (Exception ex)
        {
            error = $"native mutation removal failed: {ex.Message}";
            return false;
        }
    }

    private static bool IsMutationActive(dc.en.Hero hero, string id, dc.tool.InventItem item)
    {
        try { if (hero.inventory.isEquiped(item)) return true; }
        catch { }
        try { return hero.inventory.countItemKind(id.AsHaxeString()) > 0; }
        catch { return false; }
    }

    internal static dc.tool.InventItem? ConvertToItem(object item)
    {
        try
        {
            return ((Hashlink.Proxy.HashlinkObj)item).AsHaxe<dc.tool.InventItem>();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "factory product is not an item (" + ex.GetType().Name + ")", ex);
        }
    }

    internal void DropConverted(dc.tool.InventItem item)
    {
        var hero = ModCore.Modules.Game.Instance.HeroInstance
            ?? throw new InvalidOperationException("no live character");
        bool noStats = false;
        var drop = new dc.en.inter.ItemDrop(
            hero._level, hero.cx, hero.cy, item, true,
            new Ref<bool>(ref noStats));
        drop.init();
        try { drop.setPosCase(drop.cx, drop.cy, hero.xr, hero.yr); } catch { }
        drop.onDropAsLoot();
        drop.dx = hero.dx;
    }

    public bool TryForgeEquipment(CatalogItem item, int? level, int? stars, bool legendary,
        out string report, out string? error)
    {
        report = "";
        error = null;
        if (!item.SupportsForge)
        {
            error = $"'{item.Id}' (group {item.Group}) does not take level/quality";
            return false;
        }
        dc.tool.InventItem? hItem;
        try { hItem = ConvertToItem(CreateItem(item.Id)); }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "DeveloperConsole factory rejected {Item}", item.Id);
            error = $"Native factory rejected '{item.Id}'; see the DCCM log.";
            return false;
        }
        if (hItem == null)
        {
            error = $"factory returned no item for '{item.Id}'";
            return false;
        }
        try
        {
            bool noChecks = false;
            int wantLevel = level ?? -1;

            dc.tool.InventItem FinalizeNative(dc.tool.InventItem current)
            {
                if (wantLevel <= 0 && !legendary) return current;
                var lootGen = ResolveLootGen()
                    ?? throw new InvalidOperationException("no active loot generator");
                bool overwrite = true;
                return (legendary
                    ? lootGen.finalizeLegendaryItem(current, wantLevel > 0 ? wantLevel - 1 : null,
                        ref overwrite, null, null)
                    : lootGen.finalizeItem(current, wantLevel - 1, ref overwrite, null, null))
                    ?? throw new InvalidOperationException("native finalize returned no item");
            }

            if (legendary)
            {
                var forms = ReadLegendAffixes(item.Id);
                if (forms == null)
                {
                    error = $"'{item.Id}' has no legendary form: quality L is not available for it";
                    return false;
                }
                hItem.addAffix("Legendary".AsHaxeString(), ref noChecks);
                foreach (var affix in forms)
                {
                    try { hItem.addAffix(affix.AsHaxeString(), ref noChecks); }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "DeveloperConsole legendary affix failed for {Item}", item.Id);
                        error = $"Legendary affix application failed for '{item.Id}'; no successful spawn recorded.";
                        return false;
                    }
                }
                hItem.addAffix("Colorless".AsHaxeString(), ref noChecks);
                hItem = FinalizeNative(hItem);
            }
            else
            {
                hItem = EquipmentForge.FinalizeThenQuality(hItem, FinalizeNative, stars ?? 0,
                    current => current.addAffix("QualityUp".AsHaxeString(), ref noChecks));
            }

            int actualLevel = -1, actualStars = -1;
            try { actualLevel = hItem.getRawItemLevel(); } catch { }
            try { actualStars = hItem.getUpgradeLevel(); } catch { }
            if (!EquipmentForge.ValidateReadback(level, stars, legendary,
                    actualLevel, actualStars, out error)) return false;

            DropConverted(hItem);
            string levelText = actualLevel >= 0 ? $"level {actualLevel}" : "level (native default)";
            string qualityText = legendary ? "quality L"
                : actualStars >= 0 ? $"quality {EquipmentForge.TierName(actualStars, false)}"
                : "quality (native default)";
            report = $"{item.Label} - {levelText}, {qualityText}";
            return true;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "DeveloperConsole forge failed for {Item}", item.Id);
            error = "Native forge/drop failed. No automatic retry; inspect the run and DCCM log.";
            return false;
        }
    }

    private List<string>? ReadLegendAffixes(string id)
    {
        try
        {
            dynamic? row = GetItemRow(id);
            if (row == null) return null;
            dynamic affixes = row.legendAffixes;
            int n = 0;
            try { n = (int)affixes.length; } catch { return null; }
            if (n == 0) return null;
            var result = new List<string>();
            for (int i = 0; i < n; i++)
            {
                try
                {
                    dynamic entry = affixes.getDyn(i);
                    string? affix = entry.affix?.ToString();
                    if (!string.IsNullOrEmpty(affix)) result.Add(affix!);
                }
                catch { return null; }
            }
            return result;
        }
        catch { return null; }
    }

    private static dc.level.LootGen? ResolveLootGen()
    {
        try
        {
            var g = dc.en.inter.RandomWeaponSpawner.Class.lootGen;
            if (g != null) return g;
        }
        catch { }
        try
        {
            var g = dc.en.inter.TrainingWeaponSpawner.Class.lootGen;
            if (g != null) return g;
        }
        catch { }
        return null;
    }

    // ---- diagnostics -----------------------------------------------------

    public HeroDiagnostics? GetHeroDiagnostics()
    {
        try
        {
            var hero = ModCore.Modules.Game.Instance.HeroInstance;
            if (hero == null || hero._level == null || hero.destroyed) return null;
            int life = -1, maxLife = -1;
            double cx = double.NaN, cy = double.NaN;
            int brut = -1, tact = -1, surv = -1;
            try { life = hero.life; } catch { }
            try { maxLife = hero.maxLife; } catch { }
            try { cx = hero.cx; cy = hero.cy; } catch { }
            try { brut = hero.brutalityTier; } catch { }
            try { tact = hero.tacticTier; } catch { }
            try { surv = hero.survivalTier; } catch { }

            var mutations = new List<string>();
            var weapons = new List<string>();
            var skills = new List<string>();
            try
            {
                dynamic items = hero.inventory.items;
                int n = 0;
                try { n = items.length; } catch { n = 0; }
                for (int i = 0; i < n; i++)
                {
                    dynamic? entry = null;
                    try { entry = items.getDyn(i); } catch { continue; }
                    if (entry == null) continue;
                    string? id = null;
                    int group = -1;
                    try { id = entry._itemData?.id?.ToString(); } catch { }
                    try { group = entry._itemData != null ? (int)entry._itemData.group : -1; }
                    catch { group = -1; }
                    if (string.IsNullOrEmpty(id)) continue;
                    if (group == 12) mutations.Add(id);
                    else if (group is 4 or 5 or 6) weapons.Add(id);
                    else if (group is >= 0 and <= 3) skills.Add(id);
                }
            }
            catch { }

            int curse = 0;
            bool curseKnown = false;
            try { curse = hero.curseCounter; curseKnown = true; } catch { }
            var affects = new List<string>();
            try
            {
                dynamic all = hero.getAllAffects();
                int n = 0;
                try { n = all.length; } catch { n = 0; }
                for (int i = 0; i < n; i++)
                {
                    try
                    {
                        dynamic e = all.getDyn(i);
                        affects.Add($"{e.a}:{((double)e.t / 60.0):0.0}s");
                    }
                    catch { }
                }
            }
            catch { }
            double invis = double.NaN;
            bool invisKnown = false;
            try { invis = hero.invisibilityTimer; invisKnown = true; } catch { }

            int bScroll = -1, tScroll = -1, sScroll = -1;
            try { bScroll = hero.inventory.countItemKind("BrutalityUp".AsHaxeString()); } catch { }
            try { tScroll = hero.inventory.countItemKind("TacticUp".AsHaxeString()); } catch { }
            try { sScroll = hero.inventory.countItemKind("SurvivalUp".AsHaxeString()); } catch { }

            return new HeroDiagnostics(life, maxLife, cx, cy, brut, tact, surv,
                bScroll, tScroll, sScroll,
                mutations, weapons, skills, curse, curseKnown, affects, invis, invisKnown);
        }
        catch { return null; }
    }

    public LevelDiagnostics? GetLevelDiagnostics()
    {
        try
        {
            var hero = ModCore.Modules.Game.Instance.HeroInstance;
            if (hero == null || hero._level == null) return null;
            dynamic level = hero._level;
            string? id = null, name = null, biome = null, room = null;
            try { id = level.id?.ToString(); } catch { }
            try { name = level.name?.ToString(); } catch { }
            try { biome = level.biome?.ToString(); } catch { }
            try { room = level.rType?.ToString(); } catch { }
            return new LevelDiagnostics(id, name, biome, hero.cx, hero.cy, room);
        }
        catch { return null; }
    }

    public string GetBiome()
    {
        try
        {
            var level = GetLevelDiagnostics();
            if (level == null) return "unavailable";
            foreach (var candidate in new[] { level.Biome, level.LevelName, level.LevelId })
                if (!string.IsNullOrWhiteSpace(candidate)) return candidate!;
        }
        catch { }
        return "unavailable";
    }

    // ---- loaded mods (reflection, read-only) ----------------------------

    public IReadOnlyList<string> GetLoadedMods(out string? error)
    {
        error = null;
        try
        {
            var assembly = typeof(ModBase).Assembly;
            var loaderType = assembly.GetType("ModCore.Modules.ModLoader");
            if (loaderType == null)
            {
                error = "mod loader type is not available in this host";
                return [];
            }
            var openModule = assembly.GetType("ModCore.Module`1");
            if (openModule == null)
            {
                error = "module base type is not available in this host";
                return [];
            }
            var closed = openModule.MakeGenericType(loaderType);
            var instance = closed.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (instance == null)
            {
                error = "mod loader instance is not available";
                return [];
            }
            var field = loaderType.GetField("modInfos",
                BindingFlags.Public | BindingFlags.Instance);
            if (field?.GetValue(instance) is not IDictionary dict)
            {
                error = "mod loader does not expose modInfos";
                return [];
            }
            var result = new List<string>();
            foreach (DictionaryEntry entry in dict)
            {
                var info = entry.Value;
                if (info == null) continue;
                var t = info.GetType();
                var name = t.GetProperty("Name")?.GetValue(info)?.ToString() ?? entry.Key?.ToString() ?? "?";
                var version = t.GetProperty("Version")?.GetValue(info)?.ToString();
                result.Add(string.IsNullOrWhiteSpace(version) ? name : $"{name} {version}");
            }
            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }
        catch (Exception ex)
        {
            error = "loaded-mod enumeration failed: " + ex.Message;
            return [];
        }
    }

    public void Log(string message) => DeveloperConsoleMod.Print(message);
}

// Generic, game-independent equipment catalog model. The live provider fills
// this from the game's runtime CastleDB rows; tests fill it synthetically.
// No generated private content is embedded in this assembly.
namespace DeveloperConsole;

public sealed record CatalogItem(
    string Id,
    string DisplayName,
    string? NativeName,
    int Group,
    string Category,
    bool Spawnable,
    bool Internal,
    bool HasLegendaryForm,
    bool LegendaryKnown,
    string Source)
{
    public string? EnglishName { get; init; }
    public IReadOnlyList<string> Aliases { get; init; } = [];
    public bool IsEquipment => Group is >= 0 and <= 6;
    public bool SupportsForge => IsEquipment;

    // Pure mapping; the adapter supplies optional game-owned translations.
    public static CatalogItem FromRow(string id, string? nativeName, string? localizedName,
        int group, bool hasLegendaryForm, bool legendaryKnown, string source)
    {
        string display = !string.IsNullOrWhiteSpace(localizedName) ? localizedName!
            : !string.IsNullOrWhiteSpace(nativeName) ? nativeName!
            : id;
        bool internalRow = CatalogPolicy.IsInternal(id);
        bool spawnable = CatalogPolicy.Spawnable(group, id) && !internalRow;
        return new CatalogItem(
            id, display, nativeName, group, CatalogPolicy.CategoryForGroup(group),
            spawnable, internalRow, hasLegendaryForm, legendaryKnown, source);
    }

    public string Label =>
        string.IsNullOrWhiteSpace(DisplayName) ||
        DisplayName.Equals(Id, StringComparison.OrdinalIgnoreCase)
            ? Id
            : $"{DisplayName} ({Id})";
}

public sealed record CatalogSnapshot(
    IReadOnlyList<CatalogItem> Items,
    string Source,
    DateTime CapturedUtc,
    int RefreshCount)
{
    private CatalogIndex? index;
    public CatalogIndex Index => index ??= new CatalogIndex(Items);

    public IReadOnlyList<CatalogItem> Search(string query, int limit = 15,
        Func<CatalogItem, bool>? filter = null) => Index.Search(query, limit, filter);

    public bool TryResolve(string query, out CatalogItem item, out string? error,
        Func<CatalogItem, bool>? filter = null) =>
        Index.TryResolve(query, out item, out error, filter);

    public static CatalogSnapshot Empty { get; } =
        new([], "not loaded", DateTime.MinValue, 0);
}

public static class CatalogText
{
    // Accent-insensitive Unicode comparison; keep original strings for display.
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var sb = new System.Text.StringBuilder(s.Length);
        Span<char> chars = stackalloc char[2];
        foreach (var rune in s.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant().EnumerateRunes())
            if (System.Text.Rune.IsLetterOrDigit(rune)) sb.Append(chars[..rune.EncodeToUtf16(chars)]);
        return sb.ToString().Normalize(System.Text.NormalizationForm.FormC);
    }

    // Token view used for order-insensitive matching. "blood-sword" and
    // "sword blood" both yield the same two tokens.
    public static string[] Tokens(string? s)
    {
        if (string.IsNullOrEmpty(s)) return [];
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        foreach (char c in s.Normalize(System.Text.NormalizationForm.FormD).ToLowerInvariant())
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) ==
                System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }
        if (current.Length > 0) tokens.Add(current.ToString());
        return tokens.ToArray();
    }
}

public static class CatalogPolicy
{
    public const int MinGroup = 0;
    public const int MaxGroup = 6;

    // Item rows that exist for engine bookkeeping but must never be offered
    // as ordinary equipment drops. This list is generic game knowledge, not
    // content from any expansion.
    private static readonly HashSet<string> InternalIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "AdminWeapon", "GemPack", "Cell", "KingsHandBoss", "DookuScarf",
        "PermanentPoisonRemedy", "P_DmgDotsAround", "P_CooldownGrenade", "P_Cooldown",
        "LilStaphyEvoDummy", "BossRushStatue", "BossRushUp",
        "NoMob", "GenericEliteInfos",
    };

    public static bool IsInternal(string id) => InternalIds.Contains(id);

    public static string CategoryForGroup(int group) => group switch
    {
        >= 0 and <= 3 => "skill",
        4 => "weapon-melee",
        5 => "weapon-ranged",
        6 => "shield",
        12 => "mutation",
        7 => "talisman",
        _ => $"item-group-{group}",
    };

    public static bool Spawnable(int group, string id) =>
        group is >= MinGroup and <= MaxGroup && !IsInternal(id);

    public static string SpawnReason(CatalogItem item) =>
        item.Internal ? "internal/admin row; not a drop"
        : item.Group is >= MinGroup and <= MaxGroup ? "equipment; live registration required"
        : item.Group == 12 ? "mutation; not an equipment drop"
        : item.Group == 7 ? "talisman; console drop/pickup semantics not certified"
        : $"item-group-{item.Group}; not ordinary equipment";

    public static bool MatchesType(CatalogItem item, string type) => type.ToLowerInvariant() switch
    {
        "all" or "any" => true,
        "weapon" or "weapons" => item.Category is "weapon-melee" or "weapon-ranged",
        "melee" => item.Category == "weapon-melee",
        "ranged" => item.Category == "weapon-ranged",
        "shield" or "shields" => item.Category == "shield",
        "skill" or "skills" => item.Category == "skill",
        "mutation" or "mutations" => item.Category == "mutation",
        "item" or "equipment" => item.IsEquipment,
        _ => item.Category.Equals(type, StringComparison.OrdinalIgnoreCase),
    };
}

public sealed class CatalogIndex
{
    private readonly CatalogItem[] records;
    private readonly Dictionary<string, CatalogItem> byExactId;
    private readonly Entry[] entries;
    private sealed record Entry(CatalogItem Item, string[] Keys, string[][] Tokens);

    public CatalogIndex(IEnumerable<CatalogItem> items)
    {
        records = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
        byExactId = new Dictionary<string, CatalogItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in records) byExactId.TryAdd(item.Id, item);
        entries = records.Select(i =>
        {
            var names = Names(i).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
            return new Entry(i, names.Select(CatalogText.Normalize).Distinct().ToArray(),
                names.Select(CatalogText.Tokens).ToArray());
        }).ToArray();
    }

    public static CatalogIndex Empty { get; } = new([]);
    public int Count => records.Length;
    public IReadOnlyList<CatalogItem> Items => records;

    public IReadOnlyList<string> Categories() =>
        records.Select(r => r.Category).Distinct().OrderBy(c => c).ToArray();

    public IReadOnlyList<CatalogItem> ByCategory(string category) =>
        records.Where(r => r.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
               .ToArray();

    // Ranked search: exact id (0) > exact display/native (1) > prefix (2) >
    // substring (3). Deterministic order by id within a rank.
    public IReadOnlyList<CatalogItem> Search(string query, int limit = 15,
        Func<CatalogItem, bool>? filter = null)
    {
        var q = CatalogText.Normalize(query);
        if (q.Length == 0 || limit <= 0) return [];
        var tokens = CatalogText.Tokens(query);
        var scored = new List<(int Rank, CatalogItem Item)>();
        foreach (var e in entries)
        {
            var r = e.Item;
            if (filter != null && !filter(r)) continue;
            int rank = MatchRank(e, q, tokens);
            if (rank >= 0) scored.Add((rank, r));
        }
        return scored.OrderBy(t => t.Rank)
                     .ThenBy(t => t.Item.Id, StringComparer.Ordinal)
                     .Take(limit).Select(t => t.Item).ToArray();
    }

    // Small, deterministic typo suggestion. This is only used to improve an
    // empty-result message; resolution still uses the strict TryResolve path.
    public IReadOnlyList<CatalogItem> Suggest(string query, int limit = 5)
    {
        var q = CatalogText.Normalize(query);
        if (q.Length < 2 || limit <= 0) return [];
        var scored = new List<(int Distance, CatalogItem Item)>();
        foreach (var e in entries)
        {
            int distance = e.Keys.Where(k => Math.Abs(k.Length - q.Length) <= 2)
                .Select(k => CommandRegistry.Levenshtein(q, k)).DefaultIfEmpty(3).Min();
            if (distance <= 2) scored.Add((distance, e.Item));
        }
        return scored.OrderBy(t => t.Distance)
                     .ThenBy(t => t.Item.Id, StringComparer.Ordinal)
                     .Take(limit).Select(t => t.Item).ToArray();
    }

    // Exact matches win; multiple exact name matches are ambiguous. Partial
    // matches are accepted only when exactly one item matches. Never guesses.
    public bool TryResolve(string query, out CatalogItem item, out string? error,
        Func<CatalogItem, bool>? filter = null)
    {
        var result = Resolve(query, filter);
        item = result.Item!;
        error = result.Message;
        return result.Item != null;
    }

    public ItemResolution Resolve(string? query, Func<CatalogItem, bool>? filter = null,
        bool requireSpawnable = false)
    {
        if (Count == 0) return new(ResolutionKind.Unavailable, null, [], "Catalog unavailable. Enter a run or use catalog refresh.");
        var raw = query?.Trim() ?? "";
        var q = CatalogText.Normalize(raw);
        if (q.Length == 0) return new(ResolutionKind.Missing, null, [], "empty item name");
        var pool = entries.Where(e => filter == null || filter(e.Item)).ToArray();
        ItemResolution Finish(CatalogItem[] hits, ResolutionKind kind)
        {
            if (hits.Length > 1)
                return new(ResolutionKind.Ambiguous, null, hits,
                    $"Multiple items match '{raw}' (ambiguous). Use a full name or native ID:\n" +
                    string.Join("\n", hits.Take(8).Select((i, n) => $"  {n + 1}. {i.Label} [{i.Category}]")) +
                    (hits.Length > 8 ? $"\n  +{hits.Length - 8} more" : ""));
            var item = hits[0];
            if (requireSpawnable && !item.Spawnable)
                return new(ResolutionKind.NotSpawnable, null, hits, $"{item.Label}: {CatalogPolicy.SpawnReason(item)}");
            return new(kind, item, hits, null);
        }
        // Separate priority tiers; collisions within a tier are never guessed.
        var tiers = new Func<CatalogItem, bool>[]
        {
            i => i.Id.Equals(raw, StringComparison.OrdinalIgnoreCase),
            i => i.DisplayName.Equals(raw, StringComparison.OrdinalIgnoreCase),
            i => string.Equals(i.EnglishName, raw, StringComparison.OrdinalIgnoreCase),
            i => i.Aliases.Any(a => a.Equals(raw, StringComparison.OrdinalIgnoreCase)),
        };
        foreach (var tier in tiers)
        {
            var hits = pool.Where(e => tier(e.Item)).Select(e => e.Item).ToArray();
            if (hits.Length > 0) return Finish(hits, ResolutionKind.Exact);
        }
        var normalized = pool.Where(e => e.Keys.Contains(q)).Select(e => e.Item).ToArray();
        if (normalized.Length > 0) return Finish(normalized, ResolutionKind.Normalized);
        var tokens = CatalogText.Tokens(raw);
        var partial = pool.Where(e => e.Keys.Any(k => k.StartsWith(q, StringComparison.Ordinal)) ||
            e.Tokens.Any(ts => tokens.All(t => ts.Contains(t))))
            .Select(e => e.Item).ToArray();
        if (partial.Length > 0) return Finish(partial, ResolutionKind.Partial);
        var suggestions = Suggest(raw, 3);
        return new(ResolutionKind.Missing, null, suggestions,
            $"unknown item '{raw}'. No exact or unambiguous name match." +
            (suggestions.Count == 0 ? "\nTry search --type equipment, or the canonical ID."
                : "\nDid you mean: " + string.Join(", ", suggestions.Select(i => i.Label)) + "?"));
    }

    private static IEnumerable<string?> Names(CatalogItem i) =>
        new[] { i.Id, i.DisplayName, i.EnglishName, i.NativeName }.Concat(i.Aliases);

    private static int MatchRank(Entry e, string q, string[] queryTokens)
    {
        if (e.Keys.Length > 0 && e.Keys[0] == q) return 0;
        int rank = -1;
        foreach (var key in e.Keys)
        {
            if (key == q) return 1;
            if (key.StartsWith(q, StringComparison.Ordinal)) rank = 2;
            else if (rank < 0 && key.Contains(q, StringComparison.Ordinal)) rank = 4;
        }
        if (rank == 2) return rank;
        foreach (var tokens in e.Tokens)
        {
            bool all = true;
            foreach (var token in queryTokens)
                if (Array.IndexOf(tokens, token) < 0) { all = false; break; }
            if (all) return 3;
        }
        return rank;
    }
}

public enum ResolutionKind { Exact, Normalized, Partial, Ambiguous, Missing, Unavailable, NotSpawnable }
public sealed record ItemResolution(ResolutionKind Kind, CatalogItem? Item,
    IReadOnlyList<CatalogItem> Candidates, string? Message);

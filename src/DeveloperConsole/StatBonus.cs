// Authoritative-write stat bonuses. computeTiers() rebuilds tiers as
// 1 + inventory *Up counts on every scroll/equip event, so writing the tier
// field alone never survives recalculation. We store a per-stat bonus and
// re-apply it AFTER the original computeTiers runs. Bonuses reset when the
// underlying hero object changes, so nothing leaks across runs or saves.
namespace DeveloperConsole;

public static class StatBonus
{
    private static readonly Dictionary<string, int> bonus = new()
    {
        ["brutality"] = 0, ["tactics"] = 0, ["survival"] = 0,
    };
    private static object? heroKey;

    // Keyed by the underlying Haxe object identity (native pointer), never
    // by a C# wrapper reference: proxies may be re-created per access.
    public static void ResetFor(object? key)
    {
        if (Equals(key, heroKey)) return;
        heroKey = key;
        foreach (var k in bonus.Keys.ToList()) bonus[k] = 0;
    }

    public static int For(string stat) => bonus.TryGetValue(stat, out var v) ? v : 0;

    public static void SetTarget(string stat, int target, int computedNow) =>
        bonus[stat] = target - computedNow;

    public static void Add(string stat, int delta) =>
        bonus[stat] = For(stat) + delta;

    public static bool Any() => bonus.Values.Any(v => v != 0);

    public static int Apply(int computed, string stat) =>
        Math.Max(1, computed + For(stat));
}

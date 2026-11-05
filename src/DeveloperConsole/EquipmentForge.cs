// Equipment level/quality parsing and orchestration. The native forge is
// applied by the live context; this file is pure and unit-testable.
//
// Native ordering (verified by controlled native testing, retained here as
// implementation guidance): non-legendary finalization runs first and
// clears pre-existing quality affixes, so QualityUp stars are applied AFTER
// finalizeItem. Legendary uses the separate finalizeLegendaryItem pathway
// and requires a row with a legendary form. The actual level and quality are
// always read back and reported; a mismatch fails the spawn.
namespace DeveloperConsole;

public sealed record ForgeOptions(int? Level, int? QualityStars, bool Legendary);

public static class EquipmentForge
{
    public const int MinLevel = 1;
    public const int MaxLevel = 99;
    public const int MaxStars = 3;

    public static T FinalizeThenQuality<T>(T item, Func<T, T> finalize,
        int stars, Action<T> addQuality)
    {
        if (stars < 0 || stars > MaxStars) throw new ArgumentOutOfRangeException(nameof(stars));
        var finalized = finalize(item);
        for (int i = 0; i < stars; i++) addQuality(finalized);
        return finalized;
    }

    public static bool ValidateReadback(int? level, int? stars, bool legendary,
        int actualLevel, int actualStars, out string? error)
    {
        error = null;
        if (level.HasValue && actualLevel != level.Value)
            error = $"level mismatch: requested {level}, read back {(actualLevel < 0 ? "unavailable" : actualLevel.ToString())}; item not spawned";
        else if (!legendary && stars.HasValue && actualStars != stars.Value)
            error = $"quality mismatch: requested {TierName(stars.Value, false)}, read back {(actualStars < 0 ? "unavailable" : TierName(actualStars, false))}; item not spawned";
        return error == null;
    }

    // Supports both `level=4 quality=S` and `--level 4 --quality S`.
    public static bool TryParse(string[] args, out string[] nameParts,
        out ForgeOptions options, out string? error)
    {
        nameParts = [];
        options = new ForgeOptions(null, null, false);
        error = null;
        int? level = null;
        int? stars = null;
        bool legendary = false;
        bool seenLevel = false, seenQuality = false;
        var names = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var token = args[i] ?? "";
            string? key = null;
            string? value = null;

            // Trailing standalone modifier is reserved. Numeric words remain
            // part of the item name; levels always use named options.
            if (i == args.Length - 1 && token.Equals("legendary", StringComparison.OrdinalIgnoreCase) && names.Count > 0)
            { key = "quality"; value = "L"; }

            else if (token.StartsWith("--", StringComparison.Ordinal))
            {
                var body = token[2..];
                int eq = body.IndexOf('=');
                if (eq >= 0)
                {
                    key = body[..eq];
                    value = body[(eq + 1)..];
                }
                else
                {
                    key = body;
                    if (i + 1 >= args.Length)
                    {
                        error = $"missing value: --{key} <value>";
                        return false;
                    }
                    value = args[++i];
                }
            }
            else
            {
                int eq = token.IndexOf('=');
                if (eq > 0 && token[..eq].All(char.IsLetter))
                {
                    key = token[..eq];
                    value = token[(eq + 1)..];
                }
            }

            if (key == null)
            {
                names.Add(token);
                continue;
            }

            switch (key.ToLowerInvariant())
            {
                case "level":
                    if (seenLevel)
                    {
                        error = "duplicate level option";
                        return false;
                    }
                    seenLevel = true;
                    if (!TryParseLevel(value ?? "", out var lv, out var le))
                    {
                        error = le;
                        return false;
                    }
                    level = lv;
                    break;
                case "quality":
                    if (seenQuality)
                    {
                        error = "duplicate quality option";
                        return false;
                    }
                    seenQuality = true;
                    if (!TryParseQuality(value ?? "", out var st, out var leg, out var qe))
                    {
                        error = qe;
                        return false;
                    }
                    stars = st;
                    legendary = leg;
                    break;
                default:
                    error = $"unknown option '{key}': options are level and quality";
                    return false;
            }
        }

        if (names.Count == 0)
        {
            error = "missing item name or id";
            return false;
        }
        nameParts = names.ToArray();
        options = new ForgeOptions(level, stars, legendary);
        return true;
    }

    public static bool TryParseLevel(string s, out int level, out string? error)
    {
        level = 0;
        error = null;
        if (int.TryParse(s, out var n) && n >= MinLevel && n <= MaxLevel)
        {
            level = n;
            return true;
        }
        if (TryParseRoman(s, out var roman) && roman >= MinLevel && roman <= 30)
        {
            level = roman;
            return true;
        }
        error = $"bad level '{s}': want {MinLevel}-{MaxLevel} (roman I-XXX accepted)";
        return false;
    }

    // Strict Roman numerals I..XXX (1..30).
    public static bool TryParseRoman(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrEmpty(s)) return false;
        var t = s.ToUpperInvariant();
        var map = new Dictionary<char, int> { ['I'] = 1, ['V'] = 5, ['X'] = 10, ['L'] = 50, ['C'] = 100 };
        int total = 0, prev = 0, repeats = 0;
        for (int i = t.Length - 1; i >= 0; i--)
        {
            if (!map.TryGetValue(t[i], out var v)) return false;
            if (v < prev)
            {
                if (v * 10 < prev) return false;
                if (repeats > 0) return false;
                total -= v;
                repeats = 0;
            }
            else
            {
                if (v == prev)
                {
                    if (++repeats > 2) return false;
                }
                else repeats = 0;
                total += v;
            }
            prev = v;
        }
        if (total < 1 || total > 30) return false;
        value = total;
        return true;
    }

    public static bool TryParseQuality(string s, out int stars, out bool legendary,
        out string? error)
    {
        stars = 0;
        legendary = false;
        error = null;
        switch ((s ?? "").Trim().ToUpperInvariant())
        {
            case "BASE": case "0": case "NONE": return true;
            case "+": case "1": stars = 1; return true;
            case "++": case "2": stars = 2; return true;
            case "S": case "3": stars = 3; return true;
            case "L": case "LEGENDARY": legendary = true; return true;
        }
        error = $"bad quality '{s}': want base|+|++|S|L";
        return false;
    }

    public static string TierName(int stars, bool legendary) =>
        legendary ? "L" : stars switch { 0 => "base", 1 => "+", 2 => "++", _ => "S" };

    public static bool GroupSupportsForge(int group) => group is >= 0 and <= 6;

    public const string UsageExamples =
        "examples:\n" +
        "  spawn assault shield\n" +
        "  spawn StartSword level=4 quality=S\n" +
        "  spawn KingsSpear --level 8 --quality ++\n" +
        "  spawn \"Quick Bow\" quality=L";
}

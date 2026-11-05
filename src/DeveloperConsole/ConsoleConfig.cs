// Minimal, fail-safe configuration. A malformed file never crashes the mod:
// invalid values fall back to safe defaults and are reported in the log.
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeveloperConsole;

public sealed record ConsoleConfig
{
    public string ToggleKey { get; init; } = "=";
    public int HistoryLimit { get; init; } = 200;
    public int SearchLimit { get; init; } = 15;
    public double PanelPercent { get; init; } = 40.0;
    public bool VerboseLog { get; init; } = false;
    public bool Suggestions { get; init; } = true;
    public double WidthPercent { get; init; } = 100;
    public double Opacity { get; init; } = 0.88;
    public double TextScale { get; init; } = 1;
    public int SuggestionLimit { get; init; } = 5;
    public bool TabCompletion { get; init; } = true;

    public static ConsoleConfig Defaults => new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static bool TryParse(string json, out ConsoleConfig config, out string? error)
    {
        config = Defaults;
        error = null;
        try
        {
            var parsed = JsonSerializer.Deserialize<ConsoleConfig>(json, JsonOptions);
            if (parsed == null)
            {
                error = "configuration file is empty";
                return false;
            }
            config = parsed;
            return true;
        }
        catch (Exception ex)
        {
            error = "configuration parse failed: " + ex.Message;
            return false;
        }
    }

    public static ConsoleConfig Normalize(ConsoleConfig raw, out string? warning)
    {
        var warnings = new List<string>();
        var key = raw.ToggleKey;
        if (!KeyBinding.TryParse(key, out _, out var canonical))
        {
            warnings.Add($"unknown toggle key '{key}', using '='");
            canonical = "=";
        }

        int history = Math.Clamp(raw.HistoryLimit, 50, 1000);
        if (history != raw.HistoryLimit) warnings.Add($"historyLimit clamped to {history}");
        int search = Math.Clamp(raw.SearchLimit, 5, 100);
        if (search != raw.SearchLimit) warnings.Add($"searchLimit clamped to {search}");
        double pct = double.IsFinite(raw.PanelPercent)
            ? Math.Clamp(raw.PanelPercent, ConsoleSizing.MinPct, ConsoleSizing.MaxPct)
            : ConsoleSizing.DefaultPct;
        if (Math.Abs(pct - raw.PanelPercent) > 0.001) warnings.Add($"panelPercent clamped to {pct:F0}");

        // Unknown keys from retired Workbench preferences (uiMode, accent,
        // density) are ignored by the JSON reader; nothing crashes.
        if (!double.IsFinite(raw.WidthPercent) || raw.WidthPercent < 45 || raw.WidthPercent > 100) warnings.Add("widthPercent constrained to 45-100");
        if (!double.IsFinite(raw.Opacity) || raw.Opacity < .35 || raw.Opacity > 1) warnings.Add("opacity constrained to 0.35-1");
        if (!double.IsFinite(raw.TextScale) || raw.TextScale < .75 || raw.TextScale > 1.5) warnings.Add("textScale constrained to 0.75-1.5");
        if (raw.SuggestionLimit < 1 || raw.SuggestionLimit > 8) warnings.Add("suggestionLimit constrained to 1-8");
        warning = warnings.Count == 0 ? null : string.Join("; ", warnings);
        return raw with
        {
            ToggleKey = canonical,
            HistoryLimit = history,
            SearchLimit = search,
            PanelPercent = pct,
            WidthPercent = double.IsFinite(raw.WidthPercent) ? Math.Clamp(raw.WidthPercent, 45, 100) : 100,
            Opacity = double.IsFinite(raw.Opacity) ? Math.Clamp(raw.Opacity, 0.35, 1) : 0.88,
            TextScale = double.IsFinite(raw.TextScale) ? Math.Clamp(raw.TextScale, 0.75, 1.5) : 1,
            SuggestionLimit = Math.Clamp(raw.SuggestionLimit, 1, 8),
        };
    }

    // Reads <modDir>/console-config.json. Creates a documented default file
    // when absent. Returns defaults (with a warning) on any failure.
    public static ConsoleConfig LoadOrCreate(string? modDir, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(modDir))
        {
            warning = "mod directory unavailable; using defaults";
            return Defaults;
        }
        var path = Path.Combine(modDir, "console-config.json");
        try
        {
            if (!File.Exists(path))
            {
                var json = JsonSerializer.Serialize(Defaults, JsonOptions);
                WriteAtomic(path, json + Environment.NewLine);
                return Defaults;
            }
            var text = File.ReadAllText(path);
            if (!TryParse(text, out var parsed, out var parseError))
            {
                warning = parseError;
                return Defaults;
            }
            return Normalize(parsed, out warning);
        }
        catch (Exception ex)
        {
            warning = "configuration load failed: " + ex.Message;
            return Defaults;
        }
    }

    // Write a complete sibling temp file first, then replace the target.
    // A failed write cannot leave a half-written canonical config.
    private static void WriteAtomic(string path, string contents)
    {
        var tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, contents);
            File.Move(tmp, path, true);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    public static bool TrySave(string? modDir, ConsoleConfig config, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(modDir)) { error = "Mod configuration directory unavailable."; return false; }
        try { WriteAtomic(ConfigPath(modDir), JsonSerializer.Serialize(Normalize(config, out _), JsonOptions) + Environment.NewLine); return true; }
        catch { error = "Preferences could not be saved; active settings are unchanged. Check mod-folder permissions."; return false; }
    }

    public static string ConfigPath(string? modDir) =>
        string.IsNullOrWhiteSpace(modDir) ? "console-config.json" : Path.Combine(modDir, "console-config.json");

    public string Describe() =>
        $"uiMode=classic (single overlay) widthPercent={WidthPercent:0} opacity={Opacity:0.00} textScale={TextScale:0.00} suggestionLimit={SuggestionLimit}\n" +
        $"toggleKey={ToggleKey}  historyLimit={HistoryLimit}  searchLimit={SearchLimit}  " +
        $"panelPercent={PanelPercent:F0}  suggestions={(Suggestions ? "on" : "off")}  " +
        $"tabCompletion={(TabCompletion ? "on" : "off")}  verboseLog={(VerboseLog ? "on" : "off")}";
}

// A deliberately small, safe set of toggle keys. Letters are not accepted:
// a closed console must never intercept ordinary gameplay typing.
public static class KeyBinding
{
    public const int DefaultVk = 187; // '=' (VK_OEM_PLUS)

    private static readonly Dictionary<string, int> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["="] = 187, ["equal"] = 187, ["equals"] = 187,
        ["`"] = 192, ["backtick"] = 192, ["grave"] = 192,
        ["insert"] = 45,
        ["F1"] = 112, ["F2"] = 113, ["F3"] = 114, ["F4"] = 115,
        ["F5"] = 116, ["F6"] = 117, ["F7"] = 118, ["F8"] = 119,
        ["F9"] = 120, ["F10"] = 121, ["F11"] = 122, ["F12"] = 123,
    };

    public static bool TryParse(string? name, out int vk, out string canonical)
    {
        vk = DefaultVk;
        canonical = "=";
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (!Keys.TryGetValue(name.Trim(), out vk)) return false;
        canonical = NameFor(vk);
        return true;
    }

    public static string NameFor(int vk) => vk switch
    {
        187 => "=",
        192 => "`",
        45 => "insert",
        >= 112 and <= 123 => "F" + (vk - 111),
        _ => "=",
    };

    public static int ToVk(string? name) =>
        TryParse(name, out var vk, out _) ? vk : DefaultVk;
}

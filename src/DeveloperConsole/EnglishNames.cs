// Reads game-owned English template names locally. Nothing extracted is shipped.
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeveloperConsole;

public sealed class EnglishNames
{
    private readonly Dictionary<string, HashSet<string>> names = new(StringComparer.OrdinalIgnoreCase);
    public int Count => names.Count(p => p.Value.Count == 1);
    public string Status { get; private set; } = "English template unavailable; current names and IDs remain supported";
    public string? Get(string id) => names.TryGetValue(id, out var values) && values.Count == 1 ? values.First() : null;

    public static EnglishNames Parse(TextReader reader)
    {
        var result = new EnglishNames();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var value = new StringBuilder();
        bool reading = false, itemName = false, obsolete = false;
        void Flush()
        {
            if (itemName && !obsolete && value.Length > 0)
                foreach (var id in ids)
                {
                    if (!result.names.TryGetValue(id, out var set)) result.names[id] = set = new(StringComparer.Ordinal);
                    set.Add(value.ToString());
                }
            ids.Clear(); value.Clear(); reading = itemName = obsolete = false;
        }
        string Decode(string text)
        {
            try { return JsonSerializer.Deserialize<string>(text) ?? ""; }
            catch { obsolete = true; return ""; }
        }
        string? line;
        int lines = 0;
        while ((line = reader.ReadLine()) != null)
        {
            if (++lines > 150000 || line.Length > 32768) throw new InvalidDataException("English template limit exceeded");
            if (string.IsNullOrWhiteSpace(line)) { Flush(); continue; }
            if (line.StartsWith("#~") || line.Contains("Deprecated", StringComparison.OrdinalIgnoreCase)) obsolete = true;
            var match = Regex.Match(line, @"^#\. ([A-Za-z0-9_]+) \(name\)$");
            if (match.Success) ids.Add(match.Groups[1].Value);
            if (line.StartsWith("#: ") && Regex.IsMatch(line, @"data\.cdb:item/#\d+\.name\b")) itemName = true;
            if (line.StartsWith("msgid ")) { reading = true; value.Append(Decode(line[6..].Trim())); }
            else if (reading && line.StartsWith('"')) value.Append(Decode(line));
            else if (line.StartsWith("msgstr")) reading = false;
        }
        Flush();
        result.Status = $"English names: {result.Count} unique item identities from installed template (conflicts excluded)";
        return result;
    }

    public static EnglishNames Load(string? gameRoot)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(gameRoot)) return new();
            var file = Path.Combine(gameRoot, "lang", "main-en.pot");
            if (!File.Exists(file) || new FileInfo(file).Length > 8 * 1024 * 1024) return new();
            using var reader = new StreamReader(file, Encoding.UTF8, true);
            return Parse(reader);
        }
        catch { return new(); }
    }

    public static string? FindGameRoot()
    {
        var configured = Environment.GetEnvironmentVariable("DEAD_CELLS_GAME_PATH");
        if (!string.IsNullOrWhiteSpace(configured)) return configured;
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 7 && dir != null; i++, dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "res.pak"))) return dir.FullName;
        return null;
    }
}

namespace DeveloperConsole;

public sealed record Completion(string Text, string Label);

public static class CompletionEngine
{
    public static IReadOnlyList<Completion> Complete(string line, CommandRegistry registry, CatalogSnapshot catalog, int limit = 5)
    {
        if (line.Length > CommandRegistry.MaxCommandLength) return [];
        limit = Math.Clamp(limit, 1, 8);
        var raw = line.TrimStart('/').TrimStart();
        int space = raw.IndexOf(' ');
        if (space < 0)
            return registry.All.Where(c => c.Name.StartsWith(raw, StringComparison.OrdinalIgnoreCase))
                .Take(limit).Select(c => new Completion(c.Name + " ", $"{c.Name} - {c.Description}")).ToArray();
        var command = raw[..space];
        var tail = raw[(space + 1)..].TrimStart();
        if (!registry.TryGet(command, out var cmd) || cmd == null) return [];
        if (cmd.Name is "ss" or "as" or "rs" || cmd.Name == "player" && (tail.StartsWith("scroll ") || tail.StartsWith("stat ")))
        {
            var prefix = cmd.Name == "player" ? tail[..(tail.IndexOf(' ') + 1)] : "";
            var part = tail[prefix.Length..];
            if (part.Contains(' ')) return [];
            return new[] { "brutality", "tactics", "survival" }.Where(s => s.StartsWith(part, StringComparison.OrdinalIgnoreCase))
                .Select(s => new Completion(command + " " + prefix + s + " ", s + " - native scroll stat")).Take(limit).ToArray();
        }
        string itemPrefix = "";
        bool itemContext = cmd.Name is "spawn" or "search" or "itemid";
        if (cmd.Name == "item")
        {
            if (tail.StartsWith("info ", StringComparison.OrdinalIgnoreCase)) { itemPrefix = "info "; tail = tail[5..]; itemContext = true; }
            else itemContext = tail.Length > 0 && !cmd.Subcommands.Any(s => s.Name.StartsWith(tail, StringComparison.OrdinalIgnoreCase));
        }
        if (itemContext)
        {
            // Completion never strips or rewrites forge options.
            if (tail.Contains('=') || tail.Contains("--")) return [];
            return catalog.Search(tail.Trim('"', '\''), limit, i => i.IsEquipment && !i.Internal)
                .Select(i => new Completion(command + " " + itemPrefix + i.Id + " ",
                    $"{i.DisplayName} - {i.Id} - {i.Category}" +
                    (i.EnglishName != null && i.EnglishName != i.DisplayName ? " / " + i.EnglishName : ""))).ToArray();
        }
        if (cmd.Name is "mutation" or "mut")
        {
            var verb = tail.Split(' ', 2);
            if (verb.Length == 2 && verb[0] is "info" or "equip" or "remove" && !verb[1].Contains('='))
                return catalog.Search(verb[1].Trim('"', '\''), limit, i => i.Group == 12 && !i.Internal)
                    .Select(i => new Completion(command + " " + verb[0] + " " + i.Id + " ",
                        $"{i.DisplayName} ({i.Id})")).ToArray();
            return [];
        }
        var subs = cmd.Subcommands.Select(s => s.Name);
        return subs.Where(s => s.StartsWith(tail, StringComparison.OrdinalIgnoreCase)).Take(limit)
            .Select(s => new Completion(command + " " + s + " ", command + " " + s)).ToArray();
    }
}

// No execution method: selection can only produce replacement text.
public sealed class CompletionSelection
{
    public int Selected { get; private set; } = -1;
    public void Reset() => Selected = -1;
    public Completion? Move(IReadOnlyList<Completion> choices, int delta)
    {
        if (choices.Count == 0) { Reset(); return null; }
        Selected = Selected < 0 ? (delta < 0 ? choices.Count - 1 : 0)
            : (Selected + delta + choices.Count) % choices.Count;
        return choices[Selected];
    }
}

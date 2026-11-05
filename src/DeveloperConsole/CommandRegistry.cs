// Command registry, tokenizer, suggestions, and argument validation.
// No game dependencies: fully unit-testable. Mirrors the native console's
// token rules (space/tab, single/double quotes, leading slash).
namespace DeveloperConsole;

public sealed class CommandRegistry
{
    public SessionJournal Journal { get; } = new();
    private readonly Dictionary<string, ConsoleCommand> byName =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<ConsoleCommand> All => byName.Values
        .Distinct().OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyList<string> Categories() => All
        .Select(c => c.Category).Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public bool Register(ConsoleCommand cmd, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(cmd.Name))
        {
            error = "command name must not be empty";
            return false;
        }
        var keys = new[] { cmd.Name }.Concat(cmd.Aliases ?? []).ToArray();
        if (keys.Any(string.IsNullOrWhiteSpace) || keys.Distinct(StringComparer.OrdinalIgnoreCase).Count() != keys.Length)
        { error = "duplicate or empty command alias"; return false; }
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            if (byName.ContainsKey(key))
            {
                error = $"duplicate command name or alias: '{key}'";
                return false;
            }
        }
        byName[cmd.Name] = cmd;
        foreach (var alias in cmd.Aliases ?? [])
            if (!string.IsNullOrWhiteSpace(alias)) byName[alias] = cmd;
        return true;
    }

    public bool TryGet(string name, out ConsoleCommand? cmd) =>
        byName.TryGetValue(name ?? "", out cmd);

    public const int MaxCommandLength = 512;
    public const int MaxArguments = 64;

    // Unknown commands return false so the caller can fall through to the
    // native console (save/load/goto/gold/... keep working). A gate runs
    // first: when denied, the command is refused without executing.
    public bool TryExecute(IGameContext game, string line, out CommandResult result,
        Func<IGameContext, bool>? gate = null)
    {
        if (line != null && line.Length > MaxCommandLength)
        {
            result = CommandResult.Failure(
                $"command too long (max {MaxCommandLength} characters)");
            return true;
        }
        if (!TryTokenize(line, out var name, out var args, out var tokenError))
        {
            result = CommandResult.Failure(tokenError ?? "invalid command syntax");
            return true;
        }
        if (name == null)
        {
            result = CommandResult.Failure("empty command");
            return false;
        }
        if (args.Length > MaxArguments)
        {
            result = CommandResult.Failure(
                $"too many arguments (max {MaxArguments})");
            return true;
        }
        if (gate != null && !gate(game))
        {
            result = CommandResult.Failure("available during active gameplay only");
            return true;
        }
        if (!TryGet(name, out var cmd) || cmd == null)
        {
            var hint = SuggestCommand(name, out var canonical);
            result = hint == null
                ? CommandResult.Failure($"unknown command '{name}'\n\nuse help to list commands")
                : CommandResult.Failure($"unknown command '{name}'\n\ndid you mean '{canonical}'?");
            return false;
        }
        var missing = cmd.Args.Where(a => a.Required).Skip(args.Length).ToArray();
        if (missing.Length > 0)
        {
            result = CommandResult.Failure(
                $"missing argument <{missing[0].Name}>. Usage: {cmd.Name} {cmd.Usage}".Trim());
            return true;
        }
        try
        {
            result = cmd.Handler(new CommandContext { Game = game, Args = args });
        }
        catch (Exception ex)
        {
            // Handler details travel with the result for the mod layer to log;
            // ordinary console output must not expose a raw exception or stack.
            result = CommandResult.InternalError(
                $"command '{cmd.Name}' failed internally; see the DCCM log", ex);
        }
        Journal.Record(line ?? "", cmd, args, result);
        return true;
    }

    // Closest command name: exact-alias (mapped to canonical), prefix, then
    // Levenshtein distance <= 2. The native input field owns keystrokes, so
    // this is the supported suggestion mechanism (also used by `suggest`).
    public string? SuggestCommand(string query, out string? canonical)
    {
        canonical = null;
        var raw = (query ?? "").Trim();
        if (raw.Length == 0) return null;
        if (TryGet(raw, out var exact) && exact != null)
        {
            canonical = exact.Name;
            return exact.Name;
        }
        var candidates = All.Select(c => c.Name).ToArray();
        var prefix = candidates.FirstOrDefault(c => c.StartsWith(raw, StringComparison.OrdinalIgnoreCase));
        if (prefix != null) { canonical = prefix; return prefix; }
        string? best = null;
        int bestDistance = 3;
        foreach (var c in candidates)
        {
            int d = Levenshtein(raw.ToLowerInvariant(), c.ToLowerInvariant());
            if (d < bestDistance) { bestDistance = d; best = c; }
        }
        canonical = best;
        return best;
    }

    public static IReadOnlyList<string> SuggestPrefix(string prefix, IEnumerable<string> candidates,
        int limit = 10)
    {
        var p = CatalogText.Normalize(prefix);
        if (p.Length == 0) return [];
        return candidates
            .Where(c => CatalogText.Normalize(c).StartsWith(p, StringComparison.Ordinal))
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Take(limit).ToArray();
    }

    public static int Levenshtein(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    // Tokenizer: strip leading '/', split on space/tab, honor quotes.
    // A quote opens only at the start of a token; escaped quotes are not a
    // documented syntax. An unclosed quote is a clear syntax error rather
    // than a silently truncated argument.
    public static bool TryTokenize(string? line, out string? name, out string[] args,
        out string? error)
    {
        name = null;
        args = [];
        error = null;
        if (string.IsNullOrWhiteSpace(line)) return true;

        var s = line.Trim();
        if (s.StartsWith('/')) s = s[1..].TrimStart();
        var parsed = new List<string>();
        var cur = new System.Text.StringBuilder();
        char? quote = null;
        bool any = false;
        foreach (char ch in s)
        {
            if (quote != null)
            {
                if (ch == quote) quote = null;
                else cur.Append(ch);
            }
            else if ((ch == '"' || ch == '\'') && cur.Length == 0)
            {
                quote = ch;
            }
            else if (ch == ' ' || ch == '\t')
            {
                if (cur.Length > 0 || any)
                {
                    parsed.Add(cur.ToString());
                    cur.Clear();
                    any = false;
                }
            }
            else
            {
                cur.Append(ch);
                any = true;
            }
        }
        if (quote != null)
        {
            error = "unclosed quote in command";
            return false;
        }
        if (cur.Length > 0 || any) parsed.Add(cur.ToString());
        if (parsed.Count == 0) return true;
        name = parsed[0];
        args = parsed.Skip(1).ToArray();
        return true;
    }

    public static (string? Name, string[] Args) Tokenize(string? line)
    {
        return TryTokenize(line, out var name, out var args, out _)
            ? (name, args)
            : (null, []);
    }
}

namespace DeveloperConsole;

// Process-local, bounded strings only: no native objects, paths or save data.
public sealed class SessionJournal
{
    private readonly Queue<string> commands = new();
    private readonly Queue<string> errors = new();
    private readonly Queue<string> spawns = new();
    public IReadOnlyList<string> Commands => commands.ToArray();
    public IReadOnlyList<string> Errors => errors.ToArray();
    public string? Repeatable { get; private set; }
    public string? LastDiagnostic { get; private set; }
    private string[]? lastSpawn;
    public string[]? LastSpawn => lastSpawn?.ToArray();

    public void Record(string line, ConsoleCommand cmd, string[] args, CommandResult result)
    {
        Add(commands, line, 100);
        if (!result.Ok) Add(errors, result.Message, 20);
        var safety = cmd.Safety;
        if (cmd.Subcommands.Length > 0 && args.Length > 0)
            safety = cmd.Subcommands.FirstOrDefault(s => s.Name.Equals(args[0], StringComparison.OrdinalIgnoreCase))?.Safety ?? safety;
        if (result.Ok && safety == CommandSafety.ReadOnly &&
            cmd.Name is not ("repeat" or "history" or "errors" or "clear" or "recent")) Repeatable = line;
        if (result.Ok && cmd.Name == "debug" && (args.Length == 0 || args[0] != "last")) LastDiagnostic = result.Message;
    }

    public void RecordSpawn(CatalogItem item, string[] args)
    {
        if (!EquipmentForge.TryParse(args, out _, out var options, out _)) return;
        var canonical = new List<string> { item.Id };
        if (options.Level.HasValue) canonical.Add("level=" + options.Level);
        if (options.Legendary) canonical.Add("quality=L");
        else if (options.QualityStars.HasValue) canonical.Add("quality=" + EquipmentForge.TierName(options.QualityStars.Value, false));
        lastSpawn = canonical.ToArray();
        Add(spawns, item.Label + " - " + string.Join(" ", canonical.Skip(1)), 20);
    }

    public string RecentSpawns() => spawns.Count == 0 ? "No successful spawns in this session." : string.Join("\n", spawns.Reverse());
    private static void Add(Queue<string> queue, string value, int cap)
    {
        queue.Enqueue(value.Length > 4096 ? value[..4096] : value);
        while (queue.Count > cap) queue.Dequeue();
    }
}

namespace DeveloperConsole;

// Thin routes reuse existing handlers; no second health, spawn or scroll engine.
public static class ShortcutCommands
{
    public static CommandResult Forward(CommandRegistry registry, CommandContext ctx, string command, params string[] prefix)
    {
        if (!registry.TryGet(command, out var target) || target == null) return CommandResult.Failure("Command unavailable.");
        return target.Handler(new CommandContext { Game = ctx.Game, Args = prefix.Concat(ctx.Args).ToArray() });
    }

    public static void RegisterAll(CommandRegistry r)
    {
        void Add(ConsoleCommand c) { if (!r.Register(c, out var err)) throw new InvalidOperationException(err); }
        void Route(string name, string[] aliases, string command, string[] prefix, string usage, string description, CommandSafety safety)
            => Add(new ConsoleCommand { Name = name, Aliases = aliases, Usage = usage, Description = description,
                Category = safety == CommandSafety.ReadOnly ? "Inspect" : "Player", Safety = safety,
                Handler = ctx => Forward(r, ctx, command, prefix) });
        Route("heal", ["fullheal"], "player", ["heal"], "[amount]", "Restore health (default: full).", CommandSafety.RunState);
        Route("hp", [], "player", ["health"], "<value>", "Set current health, capped to maximum.", CommandSafety.RunState);
        Route("gold", [], "player", ["gold"], "<amount>", "Add 1-1,000,000 gold; never sets a balance.", CommandSafety.RunState);
        Route("pos", ["whereami"], "player", ["position"], "", "Current coordinates and biome.", CommandSafety.ReadOnly);
        Route("stats", [], "player", ["stats"], "", "Effective tiers and native scroll counts.", CommandSafety.ReadOnly);
        Route("equipped", [], "debug", ["equipment"], "", "Carried/equipped weapon, skill and mutation IDs.", CommandSafety.ReadOnly);
        Route("mods", [], "debug", ["mods"], "", "Loaded mod identities exposed by DCCM.", CommandSafety.ReadOnly);
        Add(new ConsoleCommand { Name = "itemid", Aliases = ["id"], Category = "Equipment", Safety = CommandSafety.ReadOnly,
            Usage = "<name>", Description = "Resolve a recognizable name to a canonical ID.", Handler = ctx =>
            {
                var result = ctx.Game.Catalog.Index.Resolve(string.Join(" ", ctx.Args));
                return result.Item == null ? CommandResult.Failure(result.Message!) : CommandResult.Success(result.Item.Label);
            } });
        Add(new ConsoleCommand { Name = "catalog", Category = "Equipment", Safety = CommandSafety.ReadOnly,
            Usage = "[stats|refresh]", Description = "Catalog health or explicit index refresh.", Handler = ctx =>
            {
                if (ctx.Args.Length == 0 || ctx.Args.SequenceEqual(new[] { "stats" }, StringComparer.OrdinalIgnoreCase))
                    return Forward(r, new CommandContext { Game = ctx.Game, Args = [] }, "debug", "catalog");
                if (ctx.Args.SequenceEqual(new[] { "refresh" }, StringComparer.OrdinalIgnoreCase))
                    return ctx.Game.RefreshCatalog(out var message) ? CommandResult.Info(message) : CommandResult.Failure(message);
                return CommandResult.Failure("Usage: catalog [stats|refresh]");
            } });
        Add(new ConsoleCommand { Name = "build", Category = "Inspect", Safety = CommandSafety.ReadOnly,
            Usage = "[stats|gear|mutations|summary]", Description = "Inspect the current test build without changing it.", Handler = ctx =>
            {
                if (ctx.Args.Length > 1) return CommandResult.Failure("Usage: build [stats|gear|mutations|summary]");
                var arg = ctx.Args.FirstOrDefault()?.ToLowerInvariant() ?? "summary";
                var clean = new CommandContext { Game = ctx.Game, Args = [] };
                return arg switch { "stats" => Forward(r, clean, "player", "stats"),
                    "gear" => Forward(r, clean, "debug", "equipment"), "mutations" => Forward(r, clean, "player", "mutations"),
                    "summary" => Forward(r, clean, "debug", "snapshot"), _ => CommandResult.Failure("Usage: build [stats|gear|mutations|summary]") };
            } });
        Add(new ConsoleCommand { Name = "history", Category = "Core", Safety = CommandSafety.ReadOnly,
            Usage = "", Description = "Last 20 submitted registered commands (session only).",
            Handler = ctx => NoArgs(ctx, () => CommandResult.Info(string.Join("\n", r.Journal.Commands.TakeLast(20)))) });
        Add(new ConsoleCommand { Name = "errors", Category = "Core", Safety = CommandSafety.ReadOnly,
            Description = "Last 20 console failures (session only).", Handler = ctx => NoArgs(ctx,
                () => CommandResult.Info(r.Journal.Errors.Count == 0 ? "No recorded console errors." : string.Join("\n", r.Journal.Errors))) });
        Add(new ConsoleCommand { Name = "recent", Category = "Equipment", Safety = CommandSafety.ReadOnly,
            Description = "Last 20 successful spawns (session only).", Handler = ctx => NoArgs(ctx, () => CommandResult.Info(r.Journal.RecentSpawns())) });
        Add(new ConsoleCommand { Name = "aliases", Category = "Core", Safety = CommandSafety.ReadOnly,
            Description = "List canonical command names and their aliases.", Handler = ctx => NoArgs(ctx,
                () => CommandResult.Info(string.Join("\n", r.All.Where(c => c.Aliases.Length > 0).Select(c => c.Name + ": " + string.Join(", ", c.Aliases))))) });
        Add(new ConsoleCommand { Name = "repeat", Category = "Core", Safety = CommandSafety.ReadOnly,
            Description = "Repeat the last successful read-only command; never repeats a spawn or scroll change.", Handler = ctx => NoArgs(ctx, () =>
            {
                var line = r.Journal.Repeatable;
                if (line == null) return CommandResult.Failure("No eligible read-only command in this session.");
                return r.TryExecute(ctx.Game, line, out var result) ? result : CommandResult.Failure("Previous command is unavailable.");
            }) });
    }

    private static CommandResult NoArgs(CommandContext ctx, Func<CommandResult> action) =>
        ctx.Args.Length == 0 ? action() : CommandResult.Failure("This command takes no arguments.");
}

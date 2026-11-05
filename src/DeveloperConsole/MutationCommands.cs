// Mutation management over the live catalog (group 12) and the hero
// inventory. Name resolution reuses the shared catalog index; equip/remove go
// through native inventory verbs with post-state verification and rollback.
// Mutation-effect activation and exact slot-capacity semantics are native
// behaviors pending live verification; nothing here guesses at them.
namespace DeveloperConsole;

public static class MutationCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        if (!registry.Register(new ConsoleCommand
        {
            Name = "mutation",
            Aliases = ["mut"],
            Description = "List, inspect, equip or remove mutations.",
            Usage = "<list|info|equipped|equip|remove> [name]",
            Category = "Player",
            Safety = CommandSafety.Mixed,
            Examples = ["mutation list", "mutation equip vengeance", "mutation remove vengeance", "mutation equipped"],
            Subcommands =
            [
                new Subcommand("list", "mutation list", "List known mutations.", List,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("info", "mutation info <name>", "Show mutation metadata.", Info,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("equipped", "mutation equipped", "List active mutations.", Equipped,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("equip", "mutation equip <name>", "Equip a mutation (run state).", Equip,
                    Safety: CommandSafety.RunState),
                new Subcommand("remove", "mutation remove <name>", "Remove a mutation (run state).", Remove,
                    Safety: CommandSafety.RunState),
            ],
            Handler = ctx => Dispatch(ctx, registry)
        }, out var err))
            throw new InvalidOperationException($"mutation command registration failed: {err}");
    }

    public static IReadOnlyList<CatalogItem> MutationRows(CatalogSnapshot catalog) =>
        catalog.Items.Where(i => i.Group == 12 && !i.Internal)
            .OrderBy(i => i.DisplayName, StringComparer.OrdinalIgnoreCase).ToArray();

    public static bool TryResolve(CatalogSnapshot catalog, string query,
        out CatalogItem item, out string? error)
    {
        item = null!;
        var rows = catalog.Items.Where(i => i.Group == 12 && !i.Internal).ToArray();
        if (rows.Length == 0) { error = "no mutations in the live catalog"; return false; }
        if (string.IsNullOrWhiteSpace(query)) { error = "missing mutation name"; return false; }
        var exact = rows.FirstOrDefault(i => i.Id.Equals(query, StringComparison.OrdinalIgnoreCase));
        if (exact != null) { item = exact; error = null; return true; }
        string norm = CatalogText.Normalize(query);
        var named = rows.Where(i => CatalogText.Normalize(i.DisplayName) == norm
            || (i.EnglishName != null && CatalogText.Normalize(i.EnglishName) == norm)
            || i.Aliases.Any(a => CatalogText.Normalize(a) == norm)).ToArray();
        if (named.Length == 1) { item = named[0]; error = null; return true; }
        if (named.Length > 1)
        {
            error = $"ambiguous mutation '{query}': " +
                string.Join(", ", named.Select(i => i.Label));
            return false;
        }
        var hits = catalog.Search(query, 10, i => i.Group == 12 && !i.Internal).ToArray();
        if (hits.Length == 1) { item = hits[0]; error = null; return true; }
        if (hits.Length > 1)
        {
            error = $"ambiguous mutation '{query}': " +
                string.Join(", ", hits.Select(i => i.Label));
            return false;
        }
        var suggest = catalog.Index.Suggest(query, 3)
            .Where(i => i.Group == 12 && !i.Internal).Take(3).ToArray();
        error = $"unknown mutation '{query}'" +
            (suggest.Length == 0 ? "" : "\ndid you mean: " + string.Join(", ", suggest.Select(i => i.Label)));
        return false;
    }

    private static CommandResult Dispatch(CommandContext ctx, CommandRegistry registry)
    {
        if (!registry.TryGet("mutation", out var cmd) || cmd == null)
            return CommandResult.Failure("mutation command is not registered");
        if (ctx.Args.Length == 0)
            return CommandResult.Info(GroupHelp(cmd));
        var sub = cmd.Subcommands.FirstOrDefault(s =>
            s.Name.Equals(ctx.Args[0], StringComparison.OrdinalIgnoreCase));
        if (sub == null)
        {
            var suggestion = CommandRegistry.SuggestPrefix(ctx.Args[0],
                cmd.Subcommands.Select(s => s.Name), 1).FirstOrDefault();
            return CommandResult.Failure(
                $"unknown mutation subcommand '{ctx.Args[0]}'" +
                (suggestion != null ? $"\n\ndid you mean '{suggestion}'?" : "") +
                $"\n\n{GroupHelp(cmd)}");
        }
        return sub.Run(ctx, ctx.Args.Skip(1).ToArray());
    }

    private static string GroupHelp(ConsoleCommand cmd) =>
        $"usage: {cmd.Usage}\n" +
        string.Join("\n", cmd.Subcommands.Select(s => $"  {s.Usage,-38} {s.Description} [{CommandSafetyText.Label(s.Safety)}]"));

    private static CommandResult List(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: mutation list");
        var rows = MutationRows(ctx.Game.Catalog);
        if (rows.Count == 0) return CommandResult.Info("No mutations in the live catalog.");
        return CommandResult.Info(
            $"Mutations ({rows.Count}):\n" +
            string.Join("\n", rows.Select(i => $"{i.DisplayName} ({i.Id})")));
    }

    private static CommandResult Info(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
            return CommandResult.Failure("Usage: mutation info <name> - e.g. mutation info vengeance");
        if (!TryResolve(ctx.Game.Catalog, string.Join(" ", args), out var item, out var error))
            return CommandResult.Failure(error!);
        var equipped = ctx.Game.GetHeroDiagnostics()?.MutationIds
            .Contains(item.Id, StringComparer.OrdinalIgnoreCase) == true;
        var lines = new List<string> { $"id: {item.Id}", $"display: {item.DisplayName}" };
        if (!string.IsNullOrWhiteSpace(item.EnglishName)) lines.Add($"English name: {item.EnglishName}");
        lines.Add($"category: {item.Category}");
        lines.Add($"equipped: {(equipped ? "yes" : "no")}");
        lines.Add($"source: {item.Source}");
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Equipped(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: mutation equipped");
        if (ctx.Game.Hero == null)
            return CommandResult.Failure("Player not available. Enter an active run before using this command.");
        var diag = ctx.Game.GetHeroDiagnostics();
        if (diag == null)
            return CommandResult.Failure("Player not available. Enter an active run before using this command.");
        if (diag.MutationIds.Count == 0) return CommandResult.Info("No mutations equipped.");
        var map = ctx.Game.Catalog.Items
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);
        return CommandResult.Info(
            $"Equipped mutations ({diag.MutationIds.Count}):\n" + string.Join("\n",
                diag.MutationIds.Select(id => map.TryGetValue(id, out var name) &&
                    !string.Equals(name, id, StringComparison.Ordinal) ? $"{name} ({id})" : id)));
    }

    private static CommandResult Equip(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
            return CommandResult.Failure("Usage: mutation equip <name> - e.g. mutation equip vengeance");
        if (ctx.Game.Hero == null)
            return CommandResult.Failure("Player not available. Enter an active run before using this command.");
        if (!TryResolve(ctx.Game.Catalog, string.Join(" ", args), out var item, out var error))
            return CommandResult.Failure(error!);
        if (!ctx.Game.TryEquipMutation(item.Id, out var report, out var replaced, out var err))
            return CommandResult.Failure(err ?? "mutation equip failed");
        return replaced == null ? CommandResult.Success(report) : CommandResult.Warning(report);
    }

    private static CommandResult Remove(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
            return CommandResult.Failure("Usage: mutation remove <name> - e.g. mutation remove vengeance");
        if (ctx.Game.Hero == null)
            return CommandResult.Failure("Player not available. Enter an active run before using this command.");
        if (!TryResolve(ctx.Game.Catalog, string.Join(" ", args), out var item, out var error))
            return CommandResult.Failure(error!);
        if (!ctx.Game.TryRemoveMutation(item.Id, out var report, out var err))
            return CommandResult.Failure(err ?? "mutation remove failed");
        return CommandResult.Success(report);
    }
}

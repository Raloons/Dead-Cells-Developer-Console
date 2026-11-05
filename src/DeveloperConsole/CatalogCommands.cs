// Equipment search, inspection, listing, and spawning.
namespace DeveloperConsole;

public static class CatalogCommands
{
    private sealed record Query(string Text, string? Type, int Limit);

    public static void RegisterAll(CommandRegistry registry, Func<ConsoleConfig> configProvider)
    {
        void Add(ConsoleCommand cmd)
        {
            if (!registry.Register(cmd, out var err))
                throw new InvalidOperationException($"catalog command registration failed: {err}");
        }

        Add(new ConsoleCommand
        {
            Name = "search",
            Aliases = ["find", "finditem", "items"],
            Description = "Search equipment by internal id, display name, or native name.",
            Usage = "<query> [--type weapon|melee|ranged|shield|skill|mutation|equipment] [--limit N]",
            Category = "Equipment",
            Safety = CommandSafety.ReadOnly,
            Examples = ["search rusty", "search sword --type weapon", "search --type shield --limit 5"],
            Args = [new ArgSpec("query", false, "words to match; omit with --type to browse")],
            Handler = ctx =>
            {
                if (!TryParseQuery(ctx.Args, configProvider().SearchLimit, out var q, out var error))
                    return CommandResult.Failure(error!);
                Func<CatalogItem, bool>? filter = BuildFilter(ctx.Game, q.Type, out var typeError);
                if (typeError != null) return CommandResult.Failure(typeError);
                if (q.Text.Length == 0 && filter == null)
                    return CommandResult.Failure(
                        "Usage: search <query> [--type T] - e.g. search rusty, or search --type shield");
                var all = q.Text.Length == 0 && filter != null
                    ? ctx.Game.Catalog.Items.Where(filter)
                        .OrderBy(i => i.Id, StringComparer.Ordinal).ToArray()
                    : ctx.Game.Catalog.Search(q.Text, int.MaxValue, filter);
                var hits = all.Take(q.Limit).ToArray();
                if (hits.Length == 0)
                {
                    var suggestions = ctx.Game.Catalog.Index.Suggest(q.Text, 3);
                    var hint = suggestions.Count == 0
                        ? "check spelling, or try fewer words - e.g. search rusty"
                        : "did you mean: " + string.Join(", ", suggestions.Select(i => i.Label));
                    return CommandResult.Failure(
                        $"no equipment matches '{q.Text}'\n\n{hint}");
                }
                var lines = new List<string>
                {
                    $"{all.Count} match(es), showing {hits.Length}:"
                };
                for (int i = 0; i < hits.Length; i++)
                    lines.Add($"{i + 1}. {Describe(hits[i])}");
                if (all.Count > hits.Length)
                    lines.Add($"(+{all.Count - hits.Length} more; use --limit to show more)");
                return CommandResult.Info(string.Join("\n", lines));
            }
        });

        Add(new ConsoleCommand
        {
            Name = "item",
            Description = "Inspect or browse the live equipment catalog.",
            Usage = "<info|list|categories> [args]",
            Category = "Equipment",
            Safety = CommandSafety.Mixed,
            Examples = ["item assault shield", "item info StartSword", "item list --type weapon", "item categories"],
            Subcommands =
            [
                new Subcommand("info", "item info <query>",
                    "Show metadata for one item.", ItemInfo,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("list", "item list [--type T] [--limit N]",
                    "Browse catalog entries.", ItemList,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("categories", "item categories",
                    "Show category counts.", ItemCategories,
                    Safety: CommandSafety.ReadOnly),
            ],
            Handler = ctx => ctx.Args.Length > 0 && !new[] { "info", "list", "categories" }.Contains(ctx.Args[0].ToLowerInvariant())
                ? ItemInfo(ctx, ctx.Args) : Dispatch(ctx, "item", registry)
        });

        Add(new ConsoleCommand
        {
            Name = "spawn",
            Aliases = ["spawnitem"],
            Description = "Drop equipment at your feet, optionally forged to a level/quality.",
            Usage = "<query> [level=1-99] [quality=base|+|++|S|L]",
            Category = "Equipment",
            Safety = CommandSafety.RunState,
            Examples = ["spawn assault shield", "spawn StartSword level=4 quality=S", "spawn multiple-nocks bow legendary"],
            Args = [new ArgSpec("query", true, "native ID or multiword name; named options are reserved")],
            Handler = ctx =>
            {
                var hero = ctx.Game.Hero;
                if (hero == null)
                    return CommandResult.Failure("no live character: start a run first");
                var spawnArgs = ctx.Args;
                if (spawnArgs.Length == 1 && spawnArgs[0].Equals("recent", StringComparison.OrdinalIgnoreCase))
                    return CommandResult.Info(registry.Journal.RecentSpawns());
                if (spawnArgs.Length == 1 && spawnArgs[0].Equals("last", StringComparison.OrdinalIgnoreCase))
                {
                    var previous = registry.Journal.LastSpawn;
                    if (previous == null) return CommandResult.Failure("No successful spawn in this session.");
                    spawnArgs = previous;
                }
                if (!EquipmentForge.TryParse(spawnArgs, out var nameParts, out var forge, out var perr))
                    return CommandResult.Failure((perr ?? "bad options") + "\n\n" + EquipmentForge.UsageExamples);
                var query = string.Join(" ", nameParts);
                if (!ctx.Game.TryResolveItem(query, out var info, out var rerr))
                    return CommandResult.Failure(rerr ?? "cannot resolve item");
                if (!info.IsEquipment)
                    return CommandResult.Failure(
                        $"'{info.Id}' is {info.Category}, not equipment: {CatalogPolicy.SpawnReason(info)}");
                if (info.Internal || !info.Spawnable)
                    return CommandResult.Failure($"'{info.Id}' is not spawnable: {CatalogPolicy.SpawnReason(info)}");

                bool wantsForge = forge.Level.HasValue || forge.QualityStars.HasValue || forge.Legendary;
                if (!wantsForge)
                {
                    object? item;
                    try { item = ctx.Game.CreateItem(info.Id); }
                    catch (Exception)
                    {
                        return CommandResult.Failure($"Native factory rejected {info.Label}; no successful spawn recorded.");
                    }
                    if (item == null)
                        return CommandResult.Failure($"factory returned no item for '{info.Id}'");
                    string nativeSummary = ctx.Game.EquipmentSummary(item);
                    ctx.Game.DropAtHeroFeet(item);
                    registry.Journal.RecordSpawn(info, spawnArgs);
                    return CommandResult.Success($"Spawned {info.Label} at your feet - {nativeSummary}.");
                }

                if (!ctx.Game.TryForgeEquipment(info, forge.Level, forge.QualityStars,
                        forge.Legendary, out var report, out var ferr))
                    return CommandResult.Failure((ferr ?? "forge failed") + "\n\n" + EquipmentForge.UsageExamples);
                registry.Journal.RecordSpawn(info, spawnArgs);
                return CommandResult.Success("Spawned " + report + " at your feet.");
            }
        });
    }

    private static CommandResult Dispatch(CommandContext ctx, string group,
        CommandRegistry registry)
    {
        if (!registry.TryGet(group, out var cmd) || cmd == null)
            return CommandResult.Failure($"unknown command group '{group}'");
        if (ctx.Args.Length == 0)
            return CommandResult.Info(GroupHelp(cmd));
        var sub = cmd.Subcommands.FirstOrDefault(s =>
            s.Name.Equals(ctx.Args[0], StringComparison.OrdinalIgnoreCase));
        if (sub == null)
        {
            var names = cmd.Subcommands.Select(s => s.Name).ToArray();
            var suggestion = CommandRegistry.SuggestPrefix(ctx.Args[0], names, 1).FirstOrDefault();
            return CommandResult.Failure(
                $"unknown {group} subcommand '{ctx.Args[0]}'" +
                (suggestion != null ? $"\n\ndid you mean '{suggestion}'?" : "") +
                $"\n\n{GroupHelp(cmd)}");
        }
        return sub.Run(ctx, ctx.Args.Skip(1).ToArray());
    }

    private static string GroupHelp(ConsoleCommand cmd) =>
        $"usage: {cmd.Usage}\n" +
        string.Join("\n", cmd.Subcommands.Select(s => $"  {s.Usage,-36} {s.Description} [{CommandSafetyText.Label(s.Safety)}]"));

    private static CommandResult ItemInfo(CommandContext ctx, string[] args)
    {
        if (args.Length == 0)
            return CommandResult.Failure("Usage: item info <query> - e.g. item info assault shield");
        var query = string.Join(" ", args);
        if (!ctx.Game.TryResolveItem(query, out var item, out var error))
        {
            var suggestions = ctx.Game.Catalog.Index.Suggest(query, 3);
            var hint = suggestions.Count == 0
                ? ""
                : "\n\ndid you mean: " + string.Join(", ", suggestions.Select(i => i.Label));
            return CommandResult.Failure((error ?? "cannot resolve item") + hint);
        }
        var lines = new List<string>
        {
            $"id: {item.Id}",
            $"display: {item.DisplayName}",
        };
        if (!string.IsNullOrWhiteSpace(item.NativeName) &&
            !string.Equals(item.NativeName, item.DisplayName, StringComparison.Ordinal))
            lines.Add($"native name: {item.NativeName}");
        if (!string.IsNullOrWhiteSpace(item.EnglishName)) lines.Add($"English name: {item.EnglishName}");
        lines.Add($"category: {item.Category}");
        lines.Add($"group: {item.Group}");
        lines.Add($"spawn: {(item.Spawnable ? "eligible" : "no")}" +
            (item.Spawnable ? "" : $" ({CatalogPolicy.SpawnReason(item)})"));
        lines.Add($"legendary form: {(item.LegendaryKnown ? (item.HasLegendaryForm ? "yes" : "no") : "unavailable")}");
        lines.Add($"quality: base|+|++|S" +
            (item.HasLegendaryForm ? "|L" : " (no legendary form)"));
        lines.Add($"source: {item.Source}");
        if (item.Spawnable) lines.Add($"spawn command: spawn {item.Id}");
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult ItemList(CommandContext ctx, string[] args)
    {
        if (!TryParseQuery(args, 20, out var q, out var error))
            return CommandResult.Failure(error!);
        Func<CatalogItem, bool>? filter = BuildFilter(ctx.Game, q.Type, out var typeError);
        if (typeError != null) return CommandResult.Failure(typeError);
        filter ??= i => i.IsEquipment;
        var text = CatalogText.Normalize(q.Text);
        var all = text.Length > 0 ? ctx.Game.Catalog.Search(q.Text, int.MaxValue, filter).ToArray()
            : ctx.Game.Catalog.Items.Where(filter).OrderBy(i => i.Id, StringComparer.Ordinal).ToArray();
        var hits = all.Take(q.Limit).ToArray();
        if (hits.Length == 0)
            return CommandResult.Info("no catalog entries match");
        var lines = new List<string> { $"{all.Length} item(s), showing {hits.Length}:" };
        foreach (var i in hits)
            lines.Add($"{i.Id}  ({i.DisplayName}) [group {i.Group}, {i.Category}]");
        if (all.Length > hits.Length)
            lines.Add($"(+{all.Length - hits.Length} more; use --limit to show more)");
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult ItemCategories(CommandContext ctx, string[] args)
    {
        if (args.Length > 0)
            return CommandResult.Failure("Usage: item categories");
        var groups = ctx.Game.Catalog.Items
            .GroupBy(i => i.Category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}: {g.Count()}").ToArray();
        if (groups.Length == 0)
            return CommandResult.Failure("catalog is not loaded yet; start a run and open the console again");
        return CommandResult.Info("categories:\n  " + string.Join("\n  ", groups));
    }

    private static string Describe(CatalogItem item)
    {
        var spawn = item.Spawnable ? "spawn=eligible" : $"spawn=no ({CatalogPolicy.SpawnReason(item)})";
        return $"{item.Label} [{item.Category}] {spawn}";
    }

    private static bool TryParseQuery(string[] args, int defaultLimit,
        out Query query, out string? error)
    {
        error = null;
        var words = new List<string>();
        string? type = null;
        int limit = defaultLimit;
        bool seenLimit = false;
        for (int i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (a.Equals("--type", StringComparison.OrdinalIgnoreCase))
            {
                if (++i >= args.Length)
                {
                    query = new Query("", null, limit);
                    error = "missing value: --type <weapon|melee|ranged|shield|skill|mutation|equipment|category>";
                    return false;
                }
                if (type != null)
                {
                    query = new Query("", null, limit);
                    error = "use one --type filter";
                    return false;
                }
                type = args[i];
            }
            else if (a.StartsWith("category:", StringComparison.OrdinalIgnoreCase))
            {
                if (type != null)
                {
                    query = new Query("", null, limit);
                    error = "use one --type or category: filter";
                    return false;
                }
                type = a["category:".Length..];
            }
            else if (a.Equals("--limit", StringComparison.OrdinalIgnoreCase))
            {
                if (seenLimit)
                {
                    query = new Query("", null, limit);
                    error = "duplicate --limit option";
                    return false;
                }
                seenLimit = true;
                if (++i >= args.Length || !int.TryParse(args[i], out var n) || n < 1 || n > 100)
                {
                    query = new Query("", null, limit);
                    error = "bad --limit: want 1-100";
                    return false;
                }
                limit = n;
            }
            else if (a.StartsWith("--", StringComparison.Ordinal))
            {
                query = new Query("", null, limit);
                error = $"unknown option '{a}': options are --type and --limit";
                return false;
            }
            else
            {
                words.Add(a);
            }
        }
        query = new Query(string.Join(" ", words).Trim(), type, Math.Clamp(limit, 1, 100));
        return true;
    }

    private static Func<CatalogItem, bool>? BuildFilter(IGameContext game, string? type,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(type)) return null;
        var cats = game.Catalog.Items.Select(i => i.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        bool known = type.ToLowerInvariant() is
            "all" or "any" or "weapon" or "weapons" or "melee" or "ranged" or
            "shield" or "shields" or "skill" or "skills" or "item" or "equipment" or
            "mutation" or "mutations" ||
            cats.Contains(type, StringComparer.OrdinalIgnoreCase);
        if (!known)
        {
            error = $"unknown type '{type}' - try weapon, melee, ranged, shield, skill, mutation, equipment, or item categories";
            return null;
        }
        return item => CatalogPolicy.MatchesType(item, type);
    }
}

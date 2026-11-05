// Core commands: help, suggestions, configuration, sizing, clearing.
namespace DeveloperConsole;

public static class CoreCommands
{
    public static void RegisterAll(CommandRegistry registry, Func<ConsoleConfig> configProvider,
        Func<double, string>? resize, Action? clear, Func<string>? reloadConfig,
        Func<string?> configPathProvider)
    {
        void Add(ConsoleCommand cmd)
        {
            if (!registry.Register(cmd, out var err))
                throw new InvalidOperationException($"core command registration failed: {err}");
        }

        Add(new ConsoleCommand
        {
            Name = "help",
            Aliases = ["h", "?", "commands"],
            Description = "List commands, or explain one command.",
            Usage = "[command|category|page]",
            Category = "Core",
            Safety = CommandSafety.ReadOnly,
            Examples = ["help", "help 2", "help spawn", "help player"],
            Handler = ctx =>
            {
                if (ctx.Args.Length > 1) return CommandResult.Failure("Usage: help [command|category|page]");
                if (ctx.Args.Length > 0)
                {
                    if (int.TryParse(ctx.Args[0], out int page)) return HelpPage(registry, page);
                    var topic = ctx.Args[0].Equals("stats", StringComparison.OrdinalIgnoreCase) ? "Player" : ctx.Args[0];
                    var category = registry.All.Where(c => c.Category.Equals(topic, StringComparison.OrdinalIgnoreCase)).ToArray();
                    if (category.Length > 0 && !registry.TryGet(topic, out _))
                        return CommandResult.Info(string.Join("\n", category.Select(c => $"{c.Name} {c.Usage} - {c.Description} [{CommandSafetyText.Label(c.Safety)}]")));
                    if (registry.TryGet(ctx.Args[0], out var one) && one != null)
                        return CommandResult.Info(Describe(one, ctx.Args[0]));
                    var hint = registry.SuggestCommand(ctx.Args[0], out var canonical);
                    return CommandResult.Failure(
                        $"unknown command '{ctx.Args[0]}'" +
                        (hint != null ? $"\n\ndid you mean '{canonical}'?" : "") +
                        "\n\nuse help to list commands");
                }
                var lines = new List<string> { $"Console commands ({registry.All.Count}). Categories:" };
                foreach (var group in registry.All.GroupBy(c => c.Category)
                             .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
                    lines.Add($"  {group.Key} - " + string.Join(", ",
                        group.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).Select(c => c.Name)));
                lines.Add("Type help <command> for detail, help <category> for a group, help 2 for the next page.");
                return CommandResult.Info(string.Join("\n", lines));
            }
        });

        Add(new ConsoleCommand
        {
            Name = "suggest",
            Aliases = ["complete"],
            Description = "Show command and equipment completions for a prefix.",
            Usage = "<prefix>",
            Category = "Core",
            Safety = CommandSafety.ReadOnly,
            Examples = ["suggest spa", "suggest rust"],
            Args = [new ArgSpec("prefix", true, "text to complete")],
            Handler = ctx =>
            {
                if (!configProvider().Suggestions)
                    return CommandResult.Failure("suggestions are disabled in configuration");
                var prefix = string.Join(" ", ctx.Args);
                if (string.IsNullOrWhiteSpace(prefix))
                    return CommandResult.Failure("Usage: suggest <prefix> - e.g. suggest spa");
                var contextual = CompletionEngine.Complete(prefix, registry, ctx.Game.Catalog, configProvider().SuggestionLimit);
                var commands = CommandRegistry.SuggestPrefix(prefix,
                    registry.All.Select(c => c.Name), configProvider().SearchLimit);
                var items = ctx.Game.Catalog.Search(prefix, configProvider().SearchLimit)
                    .Select(i => $"{i.Label} [{i.Category}]").ToArray();
                var lines = new List<string>();
                if (prefix.Contains(' ')) return CommandResult.Info(string.Join("\n", contextual.Select(c => c.Label)));
                if (commands.Count > 0)
                {
                    lines.Add("commands: " + string.Join(", ", commands));
                }
                if (items.Length > 0)
                {
                    lines.Add("equipment:");
                    lines.AddRange(items.Select(i => "  " + i));
                }
                if (lines.Count == 0)
                    return CommandResult.Info($"no commands or equipment start with '{prefix}'");
                return CommandResult.Info(string.Join("\n", lines));
            }
        });

        Add(new ConsoleCommand
        {
            Name = "config",
            Description = "Show configuration values and the config file path.",
            Usage = "[show|path|reload]",
            Category = "Core",
            Safety = CommandSafety.UiOrConfig,
            Examples = ["config", "config path", "config reload"],
            Handler = ctx =>
            {
                var sub = ctx.Args.Length == 0 ? "show" : ctx.Args[0].ToLowerInvariant();
                switch (sub)
                {
                    case "show":
                        return CommandResult.Info(configProvider().Describe() +
                            $"\nfile: {configPathProvider() ?? "(mod directory unavailable)"}");
                    case "path":
                        return CommandResult.Info(configPathProvider() ?? "mod directory unavailable");
                    case "reload":
                        if (reloadConfig == null)
                            return CommandResult.Failure("configuration reload is unavailable in this build");
                        return CommandResult.Info(reloadConfig());
                    default:
                        return CommandResult.Failure("Usage: config [show|path|reload]");
                }
            }
        });

        Add(new ConsoleCommand
        {
            Name = "version",
            Description = "Show the console and target versions.",
            Usage = "",
            Category = "Core",
            Safety = CommandSafety.ReadOnly,
            Handler = _ =>
            {
                var v = typeof(CoreCommands).Assembly.GetName().Version;
                return CommandResult.Info(
                    $"Developer Console {v?.ToString(3) ?? "0.1.0"} - target Dead Cells v35 / DCCM 35.13.8");
            }
        });

        if (resize != null)
        {
            Add(new ConsoleCommand
            {
                Name = "consize",
                Description = "Set the console panel height as a percentage of the screen.",
                Usage = "[percent 10-80]",
                Category = "Core",
                Safety = CommandSafety.UiOrConfig,
                Examples = ["consize", "consize 55"],
                Args = [new ArgSpec("percent", false, "10-80, default from config")],
                Handler = ctx =>
                {
                    double pct = configProvider().PanelPercent;
                    if (ctx.Args.Length > 0)
                    {
                        if (!double.TryParse(ctx.Args[0],
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out pct))
                            return CommandResult.Failure($"bad percent '{ctx.Args[0]}': want 10-80");
                    }
                    try
                    {
                        return CommandResult.Success(resize(pct));
                    }
                    catch (Exception ex)
                    {
                        return CommandResult.Failure("resize failed: " + ex.Message);
                    }
                }
            });
        }

        if (clear != null)
        {
            Add(new ConsoleCommand
            {
                Name = "clear",
                Aliases = ["cls"],
                Description = "Clear the console output.",
                Usage = "",
                Category = "Core",
                Safety = CommandSafety.UiOrConfig,
                Handler = _ =>
                {
                    clear();
                    return CommandResult.Success("console cleared");
                }
            });
        }
    }

    private static CommandResult HelpPage(CommandRegistry registry, int page)
    {
        const int perPage = 12;
        var all = registry.All.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        int pages = Math.Max(1, (all.Length + perPage - 1) / perPage);
        if (page < 1 || page > pages)
            return CommandResult.Failure($"No help page {page} ({pages} page(s)).");
        var lines = new List<string> { $"Commands page {page}/{pages}:" };
        lines.AddRange(all.Skip((page - 1) * perPage).Take(perPage)
            .Select(c => $"{c.Name} - {c.Description}"));
        if (page < pages) lines.Add($"Type help {page + 1} for the next page.");
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static string Describe(ConsoleCommand c, string requested)
    {
        var lines = new List<string>
        {
            $"{c.Name} {c.Usage}".Trim(),
            "  " + c.Description,
            "  safety: " + CommandSafetyText.Label(c.Safety),
        };
        if (c.Aliases.Length > 0)
            lines.Add("  aliases: " + string.Join(", ", c.Aliases));
        if (!c.Name.Equals(requested, StringComparison.OrdinalIgnoreCase))
            lines.Add($"  ('{requested}' is an alias of '{c.Name}')");
        if (c.Examples.Length > 0)
        {
            lines.Add("  examples:");
            lines.AddRange(c.Examples.Select(e => "    " + e));
        }
        if (c.Subcommands.Length > 0)
        {
            lines.Add("  subcommands:");
            foreach (var s in c.Subcommands)
                lines.Add($"    {s.Usage,-34} {s.Description} [{CommandSafetyText.Label(s.Safety)}]");
        }
        return string.Join("\n", lines);
    }
}

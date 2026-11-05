// Player inspection and testing utilities. Commands that change run state are
// marked in the command reference; nothing here touches permanent unlocks.
namespace DeveloperConsole;

public static class PlayerCommands
{
    public const int MaxGoldGrant = 1_000_000;

    public static void RegisterAll(CommandRegistry registry)
    {
        if (!registry.Register(new ConsoleCommand
        {
            Name = "player",
            Description = "Inspect or adjust the current character.",
            Usage = "<stats|heal|health|position|status|mutations|effects|gold|stat|scroll> [args]",
            Category = "Player",
            Safety = CommandSafety.Mixed,
            Examples = ["player stats", "player heal 25", "player health 50", "player position", "player status"],
            Subcommands =
            [
                new Subcommand("stats", "player stats", "Show health, stat tiers, and position.", Stats,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("heal", "player heal [amount]", "Restore hit points (default: full).", Heal,
                    Safety: CommandSafety.RunState),
                new Subcommand("health", "player health <value>", "Set current hit points (run state).", Health,
                    Safety: CommandSafety.RunState),
                new Subcommand("position", "player position", "Show coordinates and biome.", Position,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("status", "player status", "Show a compact read-only snapshot.", Status,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("mutations", "player mutations", "List equipped mutation IDs.", Mutations,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("effects", "player effects", "List active effects and durations.", Effects,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("gold", "player gold <amount>", "Add gold, 1-1,000,000 (run state).", Gold,
                    Safety: CommandSafety.RunState),
                new Subcommand("stat", "player stat <stat> <value>", "Set a temporary stat override (not saved).", StatSet,
                    Safety: CommandSafety.Temporary),
                new Subcommand("scroll", "player scroll <stat> <delta>", "Inventory-backed scrolls; save/resume verification pending.", Scroll,
                    Safety: CommandSafety.RunState),
            ],
            Handler = ctx => Dispatch(ctx, registry)
        }, out var err))
            throw new InvalidOperationException($"player command registration failed: {err}");

        foreach (var (name, aliases, operation) in new[]
        {
            ("ss", new[] { "setstat", "setscroll" }, "set"),
            ("as", new[] { "addstat", "addscroll" }, "add"),
            ("rs", new[] { "removestat", "removescroll" }, "remove"),
        })
        {
            if (!registry.Register(new ConsoleCommand
            {
                Name = name, Aliases = aliases, Category = "Player", Safety = CommandSafety.RunState,
                Description = $"{operation} native scroll allocations (save/resume verification pending).",
                Usage = "<brutality|tactics|survival> <amount>",
                Examples = [name + " tactics 5"],
                Handler = ctx => ScrollShortcut(ctx, operation),
            }, out err)) throw new InvalidOperationException(err);
        }
    }

    private static CommandResult Dispatch(CommandContext ctx, CommandRegistry registry)
    {
        if (!registry.TryGet("player", out var cmd) || cmd == null)
            return CommandResult.Failure("player command is not registered");
        if (ctx.Args.Length == 0)
            return CommandResult.Info(GroupHelp(cmd));
        var sub = cmd.Subcommands.FirstOrDefault(s =>
            s.Name.Equals(ctx.Args[0], StringComparison.OrdinalIgnoreCase));
        if (sub == null)
        {
            var suggestion = CommandRegistry.SuggestPrefix(ctx.Args[0],
                cmd.Subcommands.Select(s => s.Name), 1).FirstOrDefault();
            return CommandResult.Failure(
                $"unknown player subcommand '{ctx.Args[0]}'" +
                (suggestion != null ? $"\n\ndid you mean '{suggestion}'?" : "") +
                $"\n\n{GroupHelp(cmd)}");
        }
        return sub.Run(ctx, ctx.Args.Skip(1).ToArray());
    }

    private static string GroupHelp(ConsoleCommand cmd) =>
        $"usage: {cmd.Usage}\n" +
        string.Join("\n", cmd.Subcommands.Select(s => $"  {s.Usage,-38} {s.Description} [{CommandSafetyText.Label(s.Safety)}]"));

    private static CommandResult Stats(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: player stats");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        var diag = ctx.Game.GetHeroDiagnostics();
        var scrolls = diag != null && diag.BrutalityScrolls >= 0
            ? $"  native scrolls {diag.BrutalityScrolls}/{diag.TacticScrolls}/{diag.SurvivalScrolls}"
            : "";
        return CommandResult.Info(
            $"brutality {hero.GetTier("brutality")}  tactics {hero.GetTier("tactics")}  " +
            $"survival {hero.GetTier("survival")}  hit points {hero.Life}/{hero.MaxLife}  " +
            $"position ({hero.Cx:F1}, {hero.Cy:F1}){scrolls}");
    }

    private static CommandResult Heal(CommandContext ctx, string[] args)
    {
        if (args.Length > 1) return CommandResult.Failure("Usage: heal [amount]");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        int amount = hero.MaxLife - hero.Life;
        if (args.Length > 0)
        {
            if (!int.TryParse(args[0], out amount) || amount <= 0)
                return CommandResult.Failure($"bad amount '{args[0]}': want a positive whole number");
        }
        amount = Math.Clamp(amount, 0, Math.Max(0, hero.MaxLife - hero.Life));
        hero.Heal(amount);
        return CommandResult.Success($"Restored {amount} hit points ({hero.Life}/{hero.MaxLife}).");
    }

    private static CommandResult Health(CommandContext ctx, string[] args)
    {
        if (args.Length != 1)
            return CommandResult.Failure("Usage: player health <value> - e.g. player health 50");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        if (!int.TryParse(args[0], out var value) || value <= 0)
            return CommandResult.Failure($"bad value '{args[0]}': want a positive whole number");
        int applied = Math.Min(value, hero.MaxLife);
        hero.SetLife(applied);
        var note = applied != value ? $" (clamped from {value})" : "";
        return CommandResult.Warning($"Health set to {applied}/{hero.MaxLife}{note}; run state changed.");
    }

    private static CommandResult Position(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: player position");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        return CommandResult.Info($"You are at ({hero.Cx:F1}, {hero.Cy:F1}) in {ctx.Game.GetBiome()}.");
    }

    private static CommandResult Status(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: player status");
        var diag = ctx.Game.GetHeroDiagnostics();
        if (diag == null) return NoHero();
        var lines = new List<string>
        {
            $"HP {diag.Life}/{diag.MaxLife}  brutality {diag.Brutality}  tactics {diag.Tactics}  survival {diag.Survival}",
            $"position ({diag.Cx:F1}, {diag.Cy:F1}) in {ctx.Game.GetBiome()}",
        };
        if (diag.BrutalityScrolls >= 0)
            lines.Add($"native scrolls: brutality {diag.BrutalityScrolls}, tactics {diag.TacticScrolls}, survival {diag.SurvivalScrolls}");
        lines.Add(diag.WeaponIds.Count == 0
            ? "weapons: none"
            : "weapons: " + string.Join(", ", LabelIds(ctx, diag.WeaponIds)));
        lines.Add(diag.SkillIds.Count == 0
            ? "skills: none"
            : "skills: " + string.Join(", ", LabelIds(ctx, diag.SkillIds)));
        lines.Add(diag.MutationIds.Count == 0
            ? "mutations: none"
            : "mutations: " + string.Join(", ", LabelIds(ctx, diag.MutationIds)));
        if (diag.CurseKnown)
            lines.Add($"curse stacks: {diag.CurseStacks}");
        lines.Add(diag.Affects.Count == 0
            ? "effects: none"
            : "effects: " + string.Join(", ", diag.Affects));
        if (diag.InvisKnown && diag.InvisibilityTimer > 0)
            lines.Add($"invisibility: {diag.InvisibilityTimer / 60.0:F1}s");
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Mutations(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: player mutations");
        var diag = ctx.Game.GetHeroDiagnostics();
        if (diag == null) return NoHero();
        if (diag.MutationIds.Count == 0) return CommandResult.Info("No mutations equipped.");
        return CommandResult.Info(
            $"Equipped mutations ({diag.MutationIds.Count}):\n" +
            string.Join("\n", LabelIds(ctx, diag.MutationIds)));
    }

    private static CommandResult Effects(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: player effects");
        var diag = ctx.Game.GetHeroDiagnostics();
        if (diag == null) return NoHero();
        if (diag.Affects.Count == 0) return CommandResult.Info("No active effects.");
        return CommandResult.Info("Active effects (id: seconds left):\n" +
            string.Join("\n", diag.Affects.Select(a => "  " + a)));
    }

    private static CommandResult Gold(CommandContext ctx, string[] args)
    {
        if (args.Length != 1)
            return CommandResult.Failure("Usage: player gold <amount> - e.g. player gold 500");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        if (!int.TryParse(args[0], out var amount) || amount <= 0 || amount > MaxGoldGrant)
            return CommandResult.Failure(
                $"bad amount '{args[0]}': want a whole number from 1 to {MaxGoldGrant:N0}");
        hero.GiveGold(amount);
        return CommandResult.Warning($"Added {amount:N0} gold; run state changed.");
    }

    private static CommandResult StatSet(CommandContext ctx, string[] args)
    {
        if (args.Length == 2 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase) && StatNames.TryGet(args[1], out var clearStat))
        {
            var current = ctx.Game.Hero;
            if (current == null) return NoHero();
            int computed = Math.Max(1, current.GetTier(clearStat) - StatBonus.For(clearStat));
            StatBonus.SetTarget(clearStat, computed, computed);
            current.SetTier(clearStat, computed); current.HudSet(clearStat, computed, true);
            return CommandResult.Warning($"Cleared temporary {clearStat} override; native scroll inventory unchanged.");
        }
        if (args.Length != 2)
            return CommandResult.Failure("Usage: player stat <brutality|tactics|survival> <value>");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        var stat = args[0].ToLowerInvariant();
        if (stat is not ("brutality" or "tactics" or "survival"))
            return CommandResult.Failure($"unknown stat '{args[0]}': want brutality, tactics, or survival");
        if (!int.TryParse(args[1], out var value) || value < 1 || value > 999)
            return CommandResult.Failure($"bad value '{args[1]}': want a whole number from 1 to 999");
        StatBonus.SetTarget(stat, value, hero.GetTier(stat) - StatBonus.For(stat));
        hero.SetTier(stat, value);
        hero.HudSet(stat, value, true);
        return CommandResult.Warning(
            $"Set {stat} to {value} (temporary session override; not saved). " +
            "Use player scroll for persistent native scrolls.");
    }

    private static CommandResult Scroll(CommandContext ctx, string[] args)
    {
        if (args.Length != 2)
            return CommandResult.Failure("Usage: player scroll <brutality|tactics|survival> <delta>");
        if (ctx.Game.Hero == null) return NoHero();
        var stat = args[0].ToLowerInvariant();
        if (stat is not ("brutality" or "tactics" or "survival"))
            return CommandResult.Failure($"unknown stat '{args[0]}': want brutality, tactics, or survival");
        if (!int.TryParse(args[1], out var delta) || delta == 0)
            return CommandResult.Failure($"bad delta '{args[1]}': want a nonzero whole number");
        if (!ctx.Game.TryAdjustNativeScrolls(stat, delta, out var nativeCount,
                out var effectiveTier, out var error))
            return CommandResult.Failure(error ?? "native scroll adjustment failed");
        var verb = delta > 0 ? "Added" : "Removed";
        int before = nativeCount - delta;
        var note = "Native scroll allocation; inventory-backed (save/resume verification pending).";
        if (StatBonus.For(stat) != 0)
            note += $" Effective tier {effectiveTier} still includes the temporary {StatBonus.For(stat):+0;-0} override from player stat.";
        return CommandResult.Warning(
            $"{verb} {Math.Abs(delta)} {stat} scroll(s): native {before} -> {nativeCount}, tier {effectiveTier}. {note}");
    }

    private static CommandResult ScrollShortcut(CommandContext ctx, string operation)
    {
        if (ctx.Args.Length != 2 || !StatNames.TryGet(ctx.Args[0], out var stat))
            return CommandResult.Failure("Usage: ss|as|rs <brutality|tactics|survival> <amount>");
        var hero = ctx.Game.Hero;
        if (hero == null) return NoHero();
        if (!int.TryParse(ctx.Args[1], out int amount) || amount < 1 || amount > (operation == "set" ? 999 : 100))
            return CommandResult.Failure(operation == "set" ? "Target tier must be 1-999." : "Amount must be 1-100 per command.");
        int delta = operation == "remove" ? -amount : amount;
        if (operation == "set")
        {
            var d = ctx.Game.GetHeroDiagnostics();
            int native = d == null ? -1 : stat switch
            { "brutality" => d.BrutalityScrolls, "tactics" => d.TacticScrolls, _ => d.SurvivalScrolls };
            if (!ScrollPlan.TrySet(hero.GetTier(stat), native, StatBonus.For(stat), amount, out delta, out var error))
                return CommandResult.Failure(error!);
            if (delta == 0) return CommandResult.Info($"{stat} is already tier {amount}; no allocations changed.");
        }
        var result = Scroll(ctx, [stat, delta.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
        if (result.Ok && operation == "set" && hero.GetTier(stat) != amount)
            return CommandResult.Failure($"Native allocations changed, but effective tier is {hero.GetTier(stat)}, not {amount}. No automatic retry. Inspect player status.");
        return result;
    }

    private static IReadOnlyList<string> LabelIds(CommandContext ctx, IReadOnlyList<string> ids)
    {
        var map = ctx.Game.Catalog.Items
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().DisplayName, StringComparer.OrdinalIgnoreCase);
        return ids.Select(id =>
            map.TryGetValue(id, out var name) && !string.Equals(name, id, StringComparison.Ordinal)
                ? $"{name} ({id})" : id).ToArray();
    }

    private static CommandResult NoHero() =>
        CommandResult.Failure("Player not available. Enter an active run before using this command.");
}

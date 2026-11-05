// Read-only diagnostics for mod developers. These commands never modify state.
namespace DeveloperConsole;

public static class DebugCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        if (!registry.Register(new ConsoleCommand
        {
            Name = "debug",
            Description = "Read-only diagnostics for gameplay, equipment, and the catalog.",
            Usage = "<snapshot|player|level|equipment|status|catalog|mods> [args]",
            Category = "Debug",
            Safety = CommandSafety.ReadOnly,
            Examples = ["debug snapshot", "debug player", "debug level", "debug catalog"],
            Subcommands =
            [
                new Subcommand("last", "debug last", "Last successful diagnostic in this session.",
                    (ctx, args) => args.Length == 0 ? CommandResult.Info(registry.Journal.LastDiagnostic ?? "No diagnostic recorded.")
                        : CommandResult.Failure("Usage: debug last"), Safety: CommandSafety.ReadOnly),
                new Subcommand("snapshot", "debug snapshot", "Combined read-only snapshot.", Snapshot,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("player", "debug player", "Detailed player state.", Player,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("level", "debug level", "Current level and biome identity.", Level,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("equipment", "debug equipment", "Equipped and carried item IDs.", Equipment,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("status", "debug status", "Active effects and timers.", Status,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("catalog", "debug catalog", "Catalog provider and counts.", Catalog,
                    Safety: CommandSafety.ReadOnly),
                new Subcommand("mods", "debug mods", "Loaded DCCM mods, when the host exposes them.", Mods,
                    Safety: CommandSafety.ReadOnly),
            ],
            Handler = ctx => Dispatch(ctx, registry)
        }, out var err))
            throw new InvalidOperationException($"debug command registration failed: {err}");
    }

    private static CommandResult Dispatch(CommandContext ctx, CommandRegistry registry)
    {
        if (!registry.TryGet("debug", out var cmd) || cmd == null)
            return CommandResult.Failure("debug command is not registered");
        if (ctx.Args.Length == 0)
            return CommandResult.Info(GroupHelp(cmd));
        var sub = cmd.Subcommands.FirstOrDefault(s =>
            s.Name.Equals(ctx.Args[0], StringComparison.OrdinalIgnoreCase));
        if (sub == null)
        {
            var suggestion = CommandRegistry.SuggestPrefix(ctx.Args[0],
                cmd.Subcommands.Select(s => s.Name), 1).FirstOrDefault();
            return CommandResult.Failure(
                $"unknown debug subcommand '{ctx.Args[0]}'" +
                (suggestion != null ? $"\n\ndid you mean '{suggestion}'?" : "") +
                $"\n\n{GroupHelp(cmd)}");
        }
        return sub.Run(ctx, ctx.Args.Skip(1).ToArray());
    }

    private static string GroupHelp(ConsoleCommand cmd) =>
        $"usage: {cmd.Usage}\n" +
        string.Join("\n", cmd.Subcommands.Select(s => $"  {s.Usage,-40} {s.Description} [{CommandSafetyText.Label(s.Safety)}]"));

    private static CommandResult Snapshot(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug snapshot");
        var lines = new List<string>();
        lines.Add($"catalog: {ctx.Game.Catalog.Items.Count} entries from {ctx.Game.Catalog.Source}" +
            $" (refresh {ctx.Game.Catalog.RefreshCount})");
        var hero = ctx.Game.GetHeroDiagnostics();
        if (hero == null) lines.Add("player: unavailable (no live character)");
        else
        {
            lines.Add($"player: HP {hero.Life}/{hero.MaxLife}  tiers {hero.Brutality}/{hero.Tactics}/{hero.Survival}");
            if (hero.BrutalityScrolls >= 0)
                lines.Add($"native scrolls: {hero.BrutalityScrolls}/{hero.TacticScrolls}/{hero.SurvivalScrolls}");
            lines.Add($"position: ({hero.Cx:F1}, {hero.Cy:F1})");
        }
        var level = ctx.Game.GetLevelDiagnostics();
        lines.Add(level == null
            ? "level: unavailable"
            : $"level: {level.LevelName ?? level.LevelId ?? "unavailable"}  biome: {level.Biome ?? "unavailable"}");
        if (hero != null)
        {
            lines.Add($"weapons: {(hero.WeaponIds.Count == 0 ? "none" : string.Join(", ", hero.WeaponIds))}");
            lines.Add($"skills: {(hero.SkillIds.Count == 0 ? "none" : string.Join(", ", hero.SkillIds))}");
            lines.Add($"mutations: {(hero.MutationIds.Count == 0 ? "none" : string.Join(", ", hero.MutationIds))}");
            lines.Add($"effects: {(hero.Affects.Count == 0 ? "none" : string.Join(", ", hero.Affects))}");
        }
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Player(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug player");
        var d = ctx.Game.GetHeroDiagnostics();
        if (d == null) return CommandResult.Failure("player diagnostics unavailable: start a run first");
        var lines = new List<string>
        {
            $"life: {d.Life}/{d.MaxLife}",
            $"position: ({d.Cx:F1}, {d.Cy:F1})",
            $"tiers: brutality={d.Brutality} tactics={d.Tactics} survival={d.Survival}",
            $"native scrolls: brutality={d.BrutalityScrolls} tactics={d.TacticScrolls} survival={d.SurvivalScrolls}",
            $"weapons: {(d.WeaponIds.Count == 0 ? "none" : string.Join(", ", d.WeaponIds))}",
            $"skills: {(d.SkillIds.Count == 0 ? "none" : string.Join(", ", d.SkillIds))}",
            $"mutations: {(d.MutationIds.Count == 0 ? "none" : string.Join(", ", d.MutationIds))}",
            $"curse: {(d.CurseKnown ? d.CurseStacks.ToString() : "unavailable")}",
            $"invisibility: {(d.InvisKnown ? (d.InvisibilityTimer / 60.0).ToString("F1") + "s" : "unavailable")}",
        };
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Level(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug level");
        var l = ctx.Game.GetLevelDiagnostics();
        if (l == null) return CommandResult.Failure("level diagnostics unavailable: start a run first");
        var lines = new List<string>
        {
            $"id: {l.LevelId ?? "unavailable"}",
            $"name: {l.LevelName ?? "unavailable"}",
            $"biome: {l.Biome ?? "unavailable"}",
            $"player position: ({l.Cx:F1}, {l.Cy:F1})",
        };
        if (!string.IsNullOrWhiteSpace(l.RoomType)) lines.Add($"room: {l.RoomType}");
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Equipment(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug equipment");
        var d = ctx.Game.GetHeroDiagnostics();
        if (d == null) return CommandResult.Failure("equipment diagnostics unavailable: start a run first");
        var lines = new List<string>
        {
            "weapons: " + (d.WeaponIds.Count == 0 ? "none" : string.Join(", ", d.WeaponIds)),
            "skills: " + (d.SkillIds.Count == 0 ? "none" : string.Join(", ", d.SkillIds)),
            "mutations: " + (d.MutationIds.Count == 0 ? "none" : string.Join(", ", d.MutationIds)),
        };
        var map = ctx.Game.Catalog.Items
            .GroupBy(i => i.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var ids = d.WeaponIds.Concat(d.SkillIds).Concat(d.MutationIds).ToArray();
        foreach (var id in ids)
        {
            if (map.TryGetValue(id, out var item))
                lines.Add($"  {id}: {item.DisplayName} [group {item.Group}, {item.Category}]");
            else
                lines.Add($"  {id}: not present in the live catalog");
        }
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Status(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug status");
        var d = ctx.Game.GetHeroDiagnostics();
        if (d == null) return CommandResult.Failure("status diagnostics unavailable: start a run first");
        var lines = new List<string>
        {
            $"curse: {(d.CurseKnown ? d.CurseStacks.ToString() : "unavailable")}",
            $"invisibility: {(d.InvisKnown ? (d.InvisibilityTimer / 60.0).ToString("F1") + "s" : "unavailable")}",
        };
        if (d.Affects.Count == 0) lines.Add("effects: none");
        else
        {
            lines.Add("effects:");
            lines.AddRange(d.Affects.Select(a => "  " + a));
        }
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Catalog(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug catalog");
        var snapshot = ctx.Game.Catalog;
        var lines = new List<string>
        {
            $"source: {snapshot.Source}",
            $"entries: {snapshot.Items.Count}",
            $"refreshes: {snapshot.RefreshCount}",
            $"captured: {(snapshot.CapturedUtc == DateTime.MinValue ? "not loaded" : snapshot.CapturedUtc.ToString("u"))}",
            $"spawnable: {snapshot.Items.Count(i => i.Spawnable)}",
            $"internal: {snapshot.Items.Count(i => i.Internal)}",
        };
        var cats = snapshot.Items.GroupBy(i => i.Category)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => $"{g.Key}={g.Count()}");
        lines.Add($"English names: {snapshot.Items.Count(i => !string.IsNullOrWhiteSpace(i.EnglishName))}");
        lines.Add("categories:");
        lines.AddRange(cats.Select(c => "  " + c));
        return CommandResult.Info(string.Join("\n", lines));
    }

    private static CommandResult Mods(CommandContext ctx, string[] args)
    {
        if (args.Length > 0) return CommandResult.Failure("Usage: debug mods");
        var mods = ctx.Game.GetLoadedMods(out var error);
        if (mods.Count == 0)
            return CommandResult.Info(error ?? "no loaded mods reported by the host");
        return CommandResult.Info($"loaded mods ({mods.Count}):\n  " + string.Join("\n  ", mods));
    }
}

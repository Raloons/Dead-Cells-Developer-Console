using DeveloperConsole;

internal static class OverhaulTests
{
    internal static CatalogSnapshot Sample => new(new[]
    {
        CatalogItem.FromRow("DashShield", "Bouclier d'assaut", "Bouclier d'assaut", 6, true, true, "synthetic") with { EnglishName = "Assault Shield", Aliases = ["rush guard"] },
        CatalogItem.FromRow("StartSword", "Épée rouillée", "Épée rouillée", 4, true, true, "synthetic") with { EnglishName = "Rusty Sword" },
        CatalogItem.FromRow("KingsSpear", "Lance symétrique", null, 4, true, true, "synthetic") with { EnglishName = "Symmetrical Lance" },
        CatalogItem.FromRow("CursedSword", "Épée maudite", null, 4, true, true, "synthetic") with { EnglishName = "Cursed Sword" },
        CatalogItem.FromRow("MultiBow", "Arc à encoches multiples", null, 5, true, true, "synthetic") with { EnglishName = "Multiple-nocks Bow" },
        CatalogItem.FromRow("ExampleA", "Twin", null, 4, false, true, "synthetic mod"),
        CatalogItem.FromRow("ExampleB", "Twin", null, 4, false, true, "synthetic mod"),
        CatalogItem.FromRow("TestVengeance", "Vengeance", null, 12, true, true, "synthetic") with { EnglishName = "Vengeance" },
        CatalogItem.FromRow("TestGastronomy", "Gastronomy", null, 12, true, true, "synthetic") with { EnglishName = "Gastronomy" },
        CatalogItem.FromRow("TestAdrenaline", "Adrenaline", null, 12, true, true, "synthetic") with { EnglishName = "Adrenaline" },
        CatalogItem.FromRow("TestTwinMutA", "Twin Mutation", null, 12, false, true, "synthetic mod"),
        CatalogItem.FromRow("TestTwinMutB", "Twin Mutation", null, 12, false, true, "synthetic mod"),
        CatalogItem.FromRow("AdminWeapon", "Admin", null, 4, false, true, "synthetic"),
        CatalogItem.FromRow("ExampleLookup", "Lookup only", null, 16, false, false, "synthetic mod"),
    }, "synthetic contract rows", DateTime.UnixEpoch, 1);

    internal static CommandRegistry Registry()
    {
        var r = new CommandRegistry();
        CoreCommands.RegisterAll(r, () => ConsoleConfig.Defaults, _ => "resized", () => { }, () => "reloaded", () => "config");
        CatalogCommands.RegisterAll(r, () => ConsoleConfig.Defaults);
        PlayerCommands.RegisterAll(r); DebugCommands.RegisterAll(r); ShortcutCommands.RegisterAll(r);
        MutationCommands.RegisterAll(r);
        return r;
    }

    internal static void Run(Action<string, bool> check)
    {
        var catalog = Sample; var idx = catalog.Index; var r = Registry();
        var game = new MockGame(new MockHero(), catalog);
        CommandResult Run(string line) { r.TryExecute(game, line, out var result); return result; }
        foreach (string query in new[] { "assault shield", "DashShield", "ASSAULT SHIELD", " assault-shield ", "assault   shield", "rush guard", "Bouclier d’assaut", "bouclier dassaut" })
            check("names-" + query, idx.Resolve(query).Item?.Id == "DashShield");
        foreach (string query in new[] { "StartSword", "Rusty Sword", "Épée rouillée", "epee rouillee", "e\u0301pe\u0301e rouille\u0301e", "RustySword" })
            check("names-sword-" + query, idx.Resolve(query).Item?.Id == "StartSword");
        check("names-accent-cjk", CatalogText.Normalize("雷の剣") == "雷の剣");
        check("names-exact-priority", new CatalogIndex(new[] { Sample.Items[0], Sample.Items[1] with { DisplayName = "DashShield" } }).Resolve("DashShield").Item?.Id == "DashShield");
        check("names-ambiguous", idx.Resolve("sword").Kind == ResolutionKind.Ambiguous);
        check("names-duplicate-mod", idx.Resolve("Twin").Candidates.Count == 2 && idx.Resolve("Twin").Item == null);
        check("names-fuzzy-never-executes", idx.Resolve("assault shild").Item == null && idx.Suggest("assault shild").Any(i => i.Id == "DashShield"));
        check("names-internal-policy", idx.Resolve("AdminWeapon", requireSpawnable: true).Kind == ResolutionKind.NotSpawnable);
        check("names-lookup-policy", idx.Resolve("ExampleLookup", requireSpawnable: true).Kind == ResolutionKind.NotSpawnable);
        check("names-empty-catalog", CatalogIndex.Empty.Resolve("sword").Kind == ResolutionKind.Unavailable);
        check("names-empty-input", idx.Resolve("  ").Kind == ResolutionKind.Missing);
        check("names-refresh-snapshot", new CatalogSnapshot([Sample.Items[0] with { Aliases = ["new guard"] }], "refresh", DateTime.UtcNow, 2).Index.Resolve("new guard").Item?.Id == "DashShield" && idx.Resolve("new guard").Item == null);
        foreach (string line in new[] { "spawn assault shield", "spawnitem assault shield", "spawn DashShield", "spawn StartSword", "spawn Rusty Sword", "spawn \"Assault Shield\"", "spawn symmetrical lance", "spawn cursed sword", "spawn multiple-nocks bow", "spawn assault shield level=10", "spawn assault shield legendary", "spawn assault shield quality=L", "spawn assault shield --level X --quality legendary" })
            check("natural-" + line, Run(line).Ok);
        int drops = game.Dropped, forgeCalls = game.ForgeCalls;
        check("spawn-ambiguity-no-call", !Run("spawn sword").Ok && game.Dropped == drops && game.ForgeCalls == forgeCalls);
        check("spawn-options-not-guess-numeric", EquipmentForge.TryParse(["Example", "10", "L"], out var parts, out var opt, out _) && parts.Length == 3 && opt.Level == null);
        check("spawn-duplicate-shorthand", !Run("spawn assault shield quality=S legendary").Ok);
        check("spawn-failed-not-history", !Run("spawn no such item").Ok && r.Journal.LastSpawn?[0] == "DashShield");
        check("spawn-last-explicit", Run("spawn last").Ok && game.LastLevel == 10 && game.LastLegendary);
        check("spawn-recent", Run("spawn recent").Message.Contains("DashShield"));
        check("spawn-assault-shield", Run("spawn assault shield").Ok && game.Dropped >= 1);
        check("spawn-nonequipment-refused", !Run("spawn Lookup only").Ok);
        check("inspect-natural", Run("item info assault shield").Message.Contains("id: DashShield"));
        check("inspect-direct", Run("item assault shield").Ok);
        check("id-natural", Run("id assault shield") is { Ok: true, Status: CommandStatus.Success } idResult
            && idResult.Message.EndsWith("(DashShield)") && !idResult.Message.Contains("spawn "));
        check("search-english", Run("search assault shield").Message.Contains("DashShield"));
        check("list-english", Run("item list assault shield").Message.Contains("DashShield"));
        check("catalog-refresh-fallback", !Run("catalog refresh").Ok);
        check("catalog-multiline", Run("catalog stats").Message.Contains("categories:\n"));

        const string pot = "#. DashShield (name)\n#: res/data.cdb:item/#1.name\nmsgid \"Assault \"\n\"Shield\"\nmsgstr \"\"\n\n#. StartSword (name)\n#: res/data.cdb:item/#2.name\nmsgid \"Rusty Sword\"\nmsgstr \"\"\n";
        var english = EnglishNames.Parse(new StringReader(pot));
        check("pot-multiline", english.Get("DashShield") == "Assault Shield");
        check("pot-canonical", english.Get("StartSword") == "Rusty Sword");
        check("pot-conflict-excluded", EnglishNames.Parse(new StringReader(pot + "\n#. DashShield (name)\n#: res/data.cdb:item/#1.name\nmsgid \"Different Shield\"\nmsgstr \"\"\n")).Get("DashShield") == null);
        check("pot-non-item-ignored", EnglishNames.Parse(new StringReader(pot.Replace("data.cdb:item/", "data.cdb:mob/"))).Count == 0);
        check("pot-deprecated-ignored", EnglishNames.Parse(new StringReader("#. Deprecated\n" + pot)).Get("DashShield") == null);
        check("pot-invalid-string", EnglishNames.Parse(new StringReader(pot.Replace("\"Assault \"", "\"bad\\x\""))).Get("DashShield") == null);
        check("pot-unavailable", EnglishNames.Load(null).Count == 0);

        StatBonus.ResetFor(new object());
        foreach (var command in new[] { "as", "addstat", "addscroll" }) check("scroll-add-" + command, Run(command + " tactics 2").Ok);
        foreach (var command in new[] { "rs", "removestat", "removescroll" }) check("scroll-remove-" + command, Run(command + " tactics 1").Ok);
        foreach (var command in new[] { "ss", "setstat", "setscroll" }) check("scroll-set-" + command, Run(command + " tactics 10").Ok && game.HeroValue!.GetTier("tactics") == 10 && game.NativeScrolls["tactics"] == 9);
        foreach (var line in new[] { "as tactics -2", "rs tactics 0", "rs tactics 100", "as tactics 101", "as blue 1", "ss tactics 0", "ss tactics 999999999999", "as tactics 2147483647", "ss tactics 200", "ss tactics 2 extra" })
            check("scroll-invalid-" + line, !Run(line).Ok);
        check("scroll-color-alias", Run("as red 1").Ok);
        check("scroll-floor-modifier", !ScrollPlan.TrySet(8, 2, 0, 5, out _, out _));
        check("scroll-set-modifier", ScrollPlan.TrySet(8, 2, 0, 10, out var delta, out _) && delta == 2);
        check("scroll-unavailable-count", !ScrollPlan.TrySet(8, -1, 0, 10, out _, out _));
        check("scroll-reconcile-refuses", !ScrollPlan.TrySet(2, 9, 0, 10, out _, out _));
        Run("player stat tactics 20");
        check("scroll-temporary-conflict", !Run("ss tactics 15").Ok);
        check("scroll-clear-temporary", Run("player stat clear tactics").Ok && StatBonus.For("tactics") == 0);
        // ---- mutations ------------------------------------------------------
        foreach (string q in new[] { "TestVengeance", "vengeance", "Vengeance", "VENGEANCE" })
            check("mutation-resolve-" + q, MutationCommands.TryResolve(catalog, q, out var mr, out _) && mr.Id == "TestVengeance");
        check("mutation-ambiguous", !MutationCommands.TryResolve(catalog, "twin mutation", out _, out var mamb) && mamb!.Contains("ambiguous"));
        check("mutation-unknown", !MutationCommands.TryResolve(catalog, "xyzzy", out _, out var munk) && munk!.Contains("unknown mutation"));
        check("mutation-empty-catalog", !MutationCommands.TryResolve(CatalogSnapshot.Empty, "vengeance", out _, out _));
        check("mutation-list", Run("mutation list").Ok && Run("mutation list").Message.Contains("Vengeance (TestVengeance)"));
        check("mutation-info", Run("mutation info vengeance").Ok && Run("mutation info vengeance").Message.Contains("id: TestVengeance"));
        check("mutation-equipped-empty", Run("mutation equipped").Ok);
        check("mutation-equip", Run("mutation equip vengeance") is { Ok: true, Status: CommandStatus.Success } && game.Mutations.Contains("TestVengeance"));
        check("mutation-equip-duplicate", !Run("mutation equip vengeance").Ok);
        check("mutation-equip-unknown", !Run("mutation equip xyzzy").Ok);
        check("mutation-equip-noargs", !Run("mutation equip").Ok);
        check("mutation-equip-nohero", r.TryExecute(new MockGame(null, catalog), "mutation equip vengeance", out var mnohero) && !mnohero.Ok);
        check("mutation-equip-second", Run("mutation equip gastronomy").Ok && game.Mutations.Count == 2);
        check("mutation-slots-full", !Run("mutation equip adrenaline").Ok && game.Mutations.Count == 2);
        check("mutation-equipped", Run("mutation equipped").Message.Contains("TestVengeance"));
        check("mutation-remove", Run("mutation remove vengeance").Ok && !game.Mutations.Contains("TestVengeance"));
        check("mutation-remove-inactive", !Run("mutation remove vengeance").Ok);
        check("mutation-remove-noargs", !Run("mutation remove").Ok);
        game.MutateThrows = true;
        check("mutation-service-failure", !Run("mutation equip adrenaline").Ok);
        game.MutateThrows = false;
        check("mutation-alias", Run("mut list").Ok && Run("mut equipped").Ok);
        check("mutation-unknown-sub", !Run("mutation nope").Ok);
        check("mutation-help", Run("help mutation").Ok && Run("help mutation").Message.Contains("equip"));
        check("mutation-completion", CompletionEngine.Complete("mut equip veng", r, catalog).Any(c => c.Text == "mut equip TestVengeance "));
        var nullGame = new MockGame(null, catalog);
        foreach (string command in new[] { "ss tactics 2", "as tactics 2", "rs tactics 1", "heal", "hp 20", "gold 1" })
        { r.TryExecute(nullGame, command, out var result); check("shortcut-null-" + command, !result.Ok); }
        foreach (string command in new[] { "heal", "fullheal", "hp 50", "gold 10000", "pos", "whereami", "stats", "equipped", "build", "build stats", "build gear", "build mutations", "mods", "aliases", "errors", "history", "recent", "debug last", "help stats", "commands Equipment" })
            check("shortcut-" + command, Run(command).Ok);
        check("metadata-all-classified", r.All.All(c => c.Safety != CommandSafety.Unspecified));
        using (var file = File.OpenRead(typeof(CatalogItem).Assembly.Location))
        using (var pe = new System.Reflection.PortableExecutable.PEReader(file))
        {
            var paths = pe.ReadDebugDirectory().Where(d => d.Type == System.Reflection.PortableExecutable.DebugDirectoryEntryType.CodeView)
                .Select(d => pe.ReadCodeViewDebugDirectoryData(d).Path).ToArray();
            check("assembly-debug-path-generic", paths.Length > 0 && paths.All(p => p.StartsWith("/_/", StringComparison.Ordinal)));
        }
        check("alias-shared-handler", r.TryGet("ss", out var ss) && r.TryGet("setstat", out var setstat) && ReferenceEquals(ss, setstat));
        var fresh = Registry(); fresh.TryExecute(game, "spawn assault shield", out _);
        fresh.TryExecute(game, "repeat", out var repeatEmpty);
        check("repeat-never-mutates", !repeatEmpty.Ok);
        fresh.TryExecute(game, "stats", out _); fresh.TryExecute(game, "as tactics 1", out _);
        var nativeBefore = game.NativeScrolls["tactics"];
        fresh.TryExecute(game, "repeat", out var repeatStats);
        check("repeat-read-only", repeatStats.Ok && game.NativeScrolls["tactics"] == nativeBefore);
        for (int n = 0; n < 110; n++) fresh.TryExecute(game, "hp -1", out _);
        check("journal-bounded", fresh.Journal.Commands.Count == 100 && fresh.Journal.Errors.Count == 20);
        check("registration-internal-alias-collision", !r.Register(new ConsoleCommand { Name = "testduplicate", Aliases = ["same", "same"], Description = "test", Handler = _ => CommandResult.Info("") }, out _));

        foreach (var (line, expected) in new[] { ("spawn ass", "spawn DashShield "), ("spawnitem ass", "spawnitem DashShield "), ("debug ca", "debug catalog "), ("ss ta", "ss tactics "), ("pla", "player "), ("item info assault", "item info DashShield ") })
            check("completion-" + line, CompletionEngine.Complete(line, r, catalog).Any(c => c.Text == expected));
        check("completion-no-option-rewrite", CompletionEngine.Complete("spawn ass level=10", r, catalog).Count == 0);
        var selection = new CompletionSelection(); var options = CompletionEngine.Complete("spawn ass", r, catalog);
        check("completion-tab-select", selection.Move(options, 1)?.Text == "spawn DashShield ");
        check("completion-cycle", selection.Move(options, 1)?.Text == "spawn DashShield ");
        check("completion-empty", selection.Move([], 1) == null && selection.Selected == -1);
        var backSelection = new CompletionSelection();
        var multi = new[] { new Completion("a", "a"), new Completion("b", "b"), new Completion("c", "c") };
        check("completion-reverse-initial", backSelection.Move(multi, -1)?.Text == "c");
        var history = new HistoryBuffer(); history.Append(HistoryKind.Info, "history survives layout switch");
        check("history-resize-preserves", history.WithCapacity(50).Window(1)[0].Text == "history survives layout switch");
        var old = ConsoleConfig.Normalize(new(), out _);
        check("config-old-default", old.TabCompletion && old.TextScale == 1 && old.SuggestionLimit == 5);
        check("config-retired-keys-ignored", ConsoleConfig.TryParse(
            "{\"uiMode\":\"fancy\",\"accent\":\"blue\",\"density\":\"dense\",\"widthPercent\":80}",
            out var legacy, out _) && legacy.WidthPercent == 80);
        var invalid = ConsoleConfig.Normalize(new() { TextScale = double.NaN, Opacity = -1, WidthPercent = 2, SuggestionLimit = 200 }, out var warning);
        check("config-ui-fallback", warning != null && invalid.TextScale == 1 && invalid.WidthPercent == 45 && invalid.Opacity == .35 && invalid.SuggestionLimit == 8);
        var crowded = Presentation.Build(1280, 720, new() { PanelPercent = 10, TextScale = 1.5, SuggestionLimit = 8 }, history, "", Enumerable.Repeat(new Completion("id", "candidate"), 8).ToArray());
        check("layout-crowded-completion-fits", crowded.Captions.All(t => t.Y >= crowded.Surfaces[0].Y && t.Y + t.Size <= 720));
        check("uimode-removed", !Run("ui transplant").Ok && !Run("ui cohesive").Ok && !Run("uimode").Ok);
        check("mode-invalid", !Run("ui missing").Ok);
        check("mode-retired", !Run("ui fancy").Ok && !Run("theme").Ok);
        foreach (var res in new[] { (1280, 720), (1600, 900), (1920, 1080), (2560, 1080) })
        foreach (double scale in new[] { .75, 1, 1.5 })
        {
            var plan = Presentation.Build(res.Item1, res.Item2, new() { TextScale = scale }, history, "", options);
            check($"layout-classic-{res}-{scale}", plan.Surfaces.All(s => s.X >= 0 && s.Y >= 0 && s.X + s.Width <= res.Item1 + 1 && s.Y + s.Height <= res.Item2 + 1) &&
                plan.Captions.All(t => t.Y >= 0 && t.Y + t.Size <= res.Item2) && plan.InputX >= 0 && plan.InputY >= 0 && plan.InputWidth > 0);
        }
        check("title-console", Presentation.Build(1600, 900, new(), history, "").Captions.Any(t => t.Text == "Console"));
        ConsoleMode.ResetForTests();
    }
}

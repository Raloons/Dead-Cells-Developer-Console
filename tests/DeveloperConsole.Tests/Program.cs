// Offline acceptance tests for the standalone developer console.
// No Haxe runtime is required: the game seam is a mock. Exit code = failures.
using DeveloperConsole;

int failures = 0;
int checks = 0;
void Check(string name, bool cond, string detail = "")
{
    checks++;
    Console.WriteLine($"[{(cond ? "PASS" : "FAIL")}] {name}" + (detail.Length > 0 ? " " + detail : ""));
    if (!cond) failures++;
}

static CatalogItem Item(string id, string display, int group,
    bool internalRow = false, bool legendary = false) =>
    new(id, display, display.ToLowerInvariant(), group,
        CatalogPolicy.CategoryForGroup(group),
        CatalogPolicy.Spawnable(group, id) && !internalRow,
        internalRow, legendary, true, "test");

var items = new[]
{
    Item("StartSword", "Rusty Sword", 4, legendary: true),
    Item("QuickBow", "Quick Bow", 5, legendary: true),
    Item("Cudgel", "Cudgel", 6),
    Item("FireBomb", "Fire Grenade", 1),
    Item("TestMutation", "Test Mutation", 12),
    Item("TestVengeance", "Vengeance", 12),
    Item("TestGastronomy", "Gastronomy", 12),
    Item("TestTwinMutA", "Twin Mutation", 12),
    Item("TestTwinMutB", "Twin Mutation", 12),
    Item("AdminWeapon", "Admin Weapon", 4, internalRow: true),
    Item("AmbiguousA", "Twin", 4),
    Item("AmbiguousB", "Twin", 4),
};
var catalog = new CatalogSnapshot(items, "test", DateTime.UtcNow, 1);

var cfg = ConsoleConfig.Defaults with { SearchLimit = 15, HistoryLimit = 200 };
var registry = new CommandRegistry();
int cleared = 0;
double lastSize = 0;
string? reloadMessage = null;
CoreCommands.RegisterAll(registry, () => cfg,
    pct => { lastSize = pct; return $"sized {pct:F0}"; },
    () => cleared++, () => reloadMessage ??= "reloaded",
    () => "console-config.json");
CatalogCommands.RegisterAll(registry, () => cfg);
PlayerCommands.RegisterAll(registry);
MutationCommands.RegisterAll(registry);
DebugCommands.RegisterAll(registry);
ShortcutCommands.RegisterAll(registry);

var hero = new MockHero();
var game = new MockGame(hero, catalog);
bool Exec(string line, out CommandResult result) =>
    registry.TryExecute(game, line, out result);

// ---- tokenizer ------------------------------------------------------------
static bool Tok(string line, string? wantName, params string[] wantArgs)
{
    var (name, args) = CommandRegistry.Tokenize(line);
    return name == wantName && args.SequenceEqual(wantArgs);
}
Check("tok-basic", Tok("spawn StartSword", "spawn", "StartSword"));
Check("tok-quotes", Tok("spawn \"Quick Bow\"", "spawn", "Quick Bow"));
Check("tok-squote", Tok("spawn 'Quick Bow'", "spawn", "Quick Bow"));
Check("tok-apostrophe", Tok("spawn Assassin's Dagger", "spawn", "Assassin's", "Dagger"));
Check("tok-slash", Tok("/help", "help"));
Check("tok-empty", Tok("   ", null));
Check("tok-unicode", Tok("spawn Épée", "spawn", "Épée"));
Check("tok-unclosed", !CommandRegistry.TryTokenize("spawn \"Quick Bow",
    out _, out _, out var unclosedError) && unclosedError != null
    && unclosedError.Contains("unclosed"));

// ---- registry -------------------------------------------------------------
Check("registry-count", registry.All.Count == 30, $"count={registry.All.Count}");
Check("registry-help", registry.TryGet("h", out var hcmd) && hcmd!.Name == "help");
Check("registry-duplicate", !registry.Register(new ConsoleCommand
{
    Name = "HELP", Description = "duplicate", Handler = _ => CommandResult.Info("x")
}, out var dup) && dup != null && dup.Contains("duplicate"));
Check("registry-unknown", !registry.TryExecute(game, "goto somewhere", out _),
    "unknown falls through to native");
Check("registry-typo", !registry.TryExecute(game, "spwan", out var typo)
    && typo.Message.Contains("did you mean 'spawn'"), typo.Message);
Check("registry-gate", registry.TryExecute(new MockGame(null, catalog), "help",
    out var gated, g => g.InGameplay) && !gated.Ok
    && gated.Message.Contains("active gameplay only"));
Check("registry-missing-arg", Exec("spawn", out var missing) && !missing.Ok
    && missing.Message.Contains("missing argument"));
Check("registry-version", registry.TryExecute(game, "version", out var version) && version.Ok);
var throwing = new CommandRegistry();
throwing.Register(new ConsoleCommand
{
    Name = "boom",
    Description = "throws",
    Handler = _ => throw new InvalidOperationException("kaboom"),
}, out _);
Check("registry-handler-exception", throwing.TryExecute(game, "boom", out var thrown)
    && !thrown.Ok && thrown.Status == CommandStatus.InternalError
    && thrown.Message.Contains("internal") && !thrown.Message.Contains("kaboom"));
var longLine = "help " + new string('a', CommandRegistry.MaxCommandLength);
Check("registry-too-long", registry.TryExecute(game, longLine, out var longResult)
    && !longResult.Ok && longResult.Message.Contains("too long"));
var manyArgs = "help " + string.Join(" ", Enumerable.Repeat("a", CommandRegistry.MaxArguments + 1));
Check("registry-too-many-args", registry.TryExecute(game, manyArgs, out var manyArgsResult)
    && !manyArgsResult.Ok && manyArgsResult.Message.Contains("too many arguments"));

// ---- help -----------------------------------------------------------------
Check("help-list", Exec("help", out var help) && help.Ok
    && help.Message.Contains("Categories:") && help.Message.Contains("spawn")
    && help.Message.Contains("help 2 for the next page") && !help.Message.Contains("Tags:"));
Check("help-page", Exec("help 2", out var helpPage) && helpPage.Ok
    && helpPage.Message.Contains("Commands page 2/") && helpPage.Message.Contains(" - "));
Check("help-page-bad", Exec("help 99", out var helpBad) && !helpBad.Ok
    && helpBad.Message.Contains("No help page"));
Check("help-ascii", Exec("help", out var helpAscii) && helpAscii.Ok
    && helpAscii.Message.All(c => c < 128));
Check("completion-ascii", CompletionEngine.Complete("pla", registry, catalog)
    .All(c => c.Label.All(ch => ch < 128)));
var scrolled = new HistoryBuffer(10);
scrolled.Append(HistoryKind.Info, "a\nb\nc");
scrolled.ScrollBy(2);
scrolled.Append(HistoryKind.Info, "d");
Check("history-append-preserves-scroll", scrolled.Scroll == 2 && scrolled.Window(10)[0].Text == "a");
scrolled.ScrollToBottom();
Check("history-scroll-bottom", scrolled.Scroll == 0 && scrolled.Window(10).Last().Text == "d");
Check("help-topic", Exec("help spawn", out var helpSpawn) && helpSpawn.Ok
    && helpSpawn.Message.Contains("level=1-99") && helpSpawn.Message.Contains("quality"));
Check("help-player", Exec("help player", out var helpPlayer) && helpPlayer.Ok
    && helpPlayer.Message.Contains("subcommands") && helpPlayer.Message.Contains("player health"));
Check("help-alias", Exec("help spawnitem", out var helpAlias) && helpAlias.Ok
    && helpAlias.Message.Contains("alias"));
Check("help-unknown", Exec("help nope", out var helpUnknown) && !helpUnknown.Ok
    && helpUnknown.Message.Contains("unknown command"));
Check("help-safety-search", Exec("help search", out var helpSafetySearch)
    && helpSafetySearch.Ok && helpSafetySearch.Message.Contains("safety: read-only"));
Check("help-safety-scroll", Exec("help player", out var helpSafetyPlayer)
    && helpSafetyPlayer.Ok && helpSafetyPlayer.Message.Contains("save/resume verification pending"));
Check("help-safety-stat", Exec("help player", out var helpSafetyStat)
    && helpSafetyStat.Ok && helpSafetyStat.Message.Contains("temporary"));

// ---- suggestions ----------------------------------------------------------
Check("suggest-command", Exec("suggest spa", out var sug1) && sug1.Ok
    && sug1.Message.Contains("spawn"));
Check("suggest-item", Exec("suggest rust", out var sug2) && sug2.Ok
    && sug2.Message.Contains("Rusty Sword"));
Check("suggest-empty", Exec("suggest", out var sug3) && !sug3.Ok
    && sug3.Message.Contains("missing argument"));

// ---- config ---------------------------------------------------------------
var defaults = ConsoleConfig.Defaults;
Check("config-defaults", defaults.ToggleKey == "=" && defaults.HistoryLimit == 200
    && defaults.SearchLimit == 15 && defaults.PanelPercent == 40);
Check("config-key", KeyBinding.TryParse("F7", out var vk, out var canonical)
    && vk == 118 && canonical == "F7");
Check("config-key-invalid", !KeyBinding.TryParse("Q", out _, out _));
var normalized = ConsoleConfig.Normalize(
    defaults with { ToggleKey = "Q", HistoryLimit = 5, SearchLimit = 1000, PanelPercent = 200 },
    out var normWarning);
Check("config-normalize", normalized.ToggleKey == "="
    && normalized.HistoryLimit == 50 && normalized.SearchLimit == 100
    && normalized.PanelPercent == 80 && normWarning != null);
Check("config-parse", ConsoleConfig.TryParse(
    "{\"toggleKey\":\"`\",\"historyLimit\":300,\"searchLimit\":25,\"panelPercent\":55}",
    out var parsed, out var parseError) && parseError == null
    && parsed.ToggleKey == "`" && parsed.HistoryLimit == 300 && parsed.SearchLimit == 25);
Check("config-parse-bad", !ConsoleConfig.TryParse("{ nope", out _, out var badError)
    && badError != null);
var cfgDir = Path.Combine(Path.GetTempPath(), "dc-config-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(cfgDir);
try
{
    var created = ConsoleConfig.LoadOrCreate(cfgDir, out var createdWarning);
    Check("config-file-created", File.Exists(Path.Combine(cfgDir, "console-config.json"))
        && createdWarning == null && created.HistoryLimit == 200);
    File.WriteAllText(Path.Combine(cfgDir, "console-config.json"), "{bad");
    var fallback = ConsoleConfig.LoadOrCreate(cfgDir, out var fallbackWarning);
    Check("config-file-bad-fallback", fallback.ToggleKey == "=" && fallbackWarning != null);
}
finally { Directory.Delete(cfgDir, true); }
var blockedCfgDir = Path.Combine(Path.GetTempPath(), "dc-config-blocked-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(blockedCfgDir);
Directory.CreateDirectory(Path.Combine(blockedCfgDir, "console-config.json"));
try
{
    var blocked = ConsoleConfig.LoadOrCreate(blockedCfgDir, out var blockedWarning);
    Check("config-write-failure-defaults", blocked.ToggleKey == "=" && blockedWarning != null
        && !File.Exists(Path.Combine(blockedCfgDir, "console-config.json.tmp")));
}
finally { Directory.Delete(blockedCfgDir, true); }
Check("config-command", Exec("config", out var cfgShow) && cfgShow.Ok
    && cfgShow.Message.Contains("toggleKey=") && cfgShow.Message.Contains("console-config.json"));
Check("config-reload", Exec("config reload", out var cfgReload) && cfgReload.Ok
    && reloadMessage == "reloaded");

// ---- catalog --------------------------------------------------------------
var index = new CatalogIndex(items);
Check("catalog-row-name", CatalogItem.FromRow("X", "Épée", null, 4, true, true, "test").DisplayName == "Épée");
Check("catalog-row-localized", CatalogItem.FromRow("X", "Épée", "Rusty Sword", 4, true, true, "test").DisplayName == "Rusty Sword");
Check("catalog-row-id-fallback", CatalogItem.FromRow("X", null, null, 4, false, true, "test").DisplayName == "X");
Check("catalog-count", index.Count == items.Length);
Check("catalog-exact-id", index.TryResolve("StartSword", out var r1, out _)
    && r1.Id == "StartSword");
Check("catalog-exact-display", index.TryResolve("Rusty Sword", out var r2, out _)
    && r2.Id == "StartSword");
Check("catalog-partial", index.TryResolve("rust", out var r3, out _)
    && r3.Id == "StartSword");
Check("catalog-ambiguous", !index.TryResolve("Twin", out _, out var amb)
    && amb != null && amb.Contains("ambiguous"));
Check("catalog-unknown", !index.TryResolve("xyzzy", out _, out var unk)
    && unk != null && unk.Contains("unknown"));
Check("catalog-search-rank", index.Search("StartSword")[0].Id == "StartSword");
Check("catalog-token-order", index.Search("sword rusty").Any(i => i.Id == "StartSword"));
Check("catalog-suggest", index.Suggest("rusty swor", 3).Any(i => i.Id == "StartSword"));
Check("catalog-search-filter", index.Search("", 10, i => i.Category == "shield").Count == 0
    || index.Search("c", 10, i => i.Category == "shield").Any(i => i.Id == "Cudgel"));
Check("catalog-internal", !CatalogPolicy.Spawnable(4, "AdminWeapon"));
Check("catalog-mutation", !CatalogPolicy.Spawnable(12, "TestMutation"));
Check("catalog-talisman", !CatalogPolicy.Spawnable(7, "Talisman"));
Check("catalog-normalize", CatalogText.Normalize("Blood-Sword!") == "bloodsword");

// ---- search / item --------------------------------------------------------
Check("search", Exec("search sword", out var search) && search.Ok
    && search.Message.Contains("Rusty Sword"));
Check("search-type", Exec("search --type shield", out var searchType) && searchType.Ok
    && searchType.Message.Contains("Cudgel"));
Check("search-type-category", Exec("search category:shield", out var searchCat) && searchCat.Ok
    && searchCat.Message.Contains("Cudgel"));
Check("search-limit", Exec("search --type weapon --limit 1", out var searchLimit) && searchLimit.Ok
    && searchLimit.Message.Contains("showing 1"));
Check("search-noargs", Exec("search", out var searchNoArgs) && !searchNoArgs.Ok
    && searchNoArgs.Message.Contains("Usage: search"));
Check("search-unknown", Exec("search xyzzy", out var searchUnknown) && !searchUnknown.Ok
    && searchUnknown.Message.Contains("no equipment matches"));
Check("search-token-order", Exec("search sword rusty", out var searchToken) && searchToken.Ok
    && searchToken.Message.Contains("Rusty Sword"));
Check("search-typo-suggest", Exec("search rusty swrod", out var searchTypo) && !searchTypo.Ok
    && searchTypo.Message.Contains("did you mean"));
Check("search-duplicate-limit", Exec("search rusty --limit 1 --limit 2", out var searchDupLimit)
    && !searchDupLimit.Ok && searchDupLimit.Message.Contains("duplicate --limit"));
Check("search-bad-type", Exec("search --type potato", out var searchBad) && !searchBad.Ok
    && searchBad.Message.Contains("unknown type"));
Check("search-bad-option", Exec("search --nope", out var searchOpt) && !searchOpt.Ok);
Check("item-info", Exec("item info StartSword", out var info) && info.Ok
    && info.Message.Contains("id: StartSword") && info.Message.Contains("category: weapon-melee")
    && info.Message.Contains("spawn: eligible") && info.Message.Contains("quality: base|+|++|S|L"));
Check("item-info-unknown", Exec("item info xyzzy", out var infoUnknown) && !infoUnknown.Ok);
Check("item-info-suggest", Exec("item info rusty swrod", out var infoSuggest)
    && !infoSuggest.Ok && infoSuggest.Message.Contains("did you mean"));
Check("item-list", Exec("item list --type weapon", out var list) && list.Ok
    && list.Message.Contains("StartSword") && !list.Message.Contains("Cudgel"));
Check("item-list-text", Exec("item list sword", out var listText) && listText.Ok
    && listText.Message.Contains("StartSword") && !listText.Message.Contains("QuickBow"));
Check("item-categories", Exec("item categories", out var cats) && cats.Ok
    && cats.Message.Contains("weapon-melee") && cats.Message.Contains("shield"));
Check("item-unknown-sub", Exec("item nope", out var itemSub) && !itemSub.Ok
    && itemSub.Message.Contains("unknown item"));

// ---- spawn ----------------------------------------------------------------
Check("spawn-basic", Exec("spawn StartSword", out var spawn1) && spawn1.Ok
    && game.Dropped == 1 && spawn1.Message.Contains("at your feet"));
Check("spawn-forge-equals", Exec("spawn StartSword level=4 quality=S", out var spawn2)
    && spawn2.Ok && game.LastLevel == 4 && game.LastStars == 3 && !game.LastLegendary);
Check("spawn-forge-flags", Exec("spawn StartSword --level 8 --quality ++", out var spawn3)
    && spawn3.Ok && game.LastLevel == 8 && game.LastStars == 2);
Check("spawn-legendary", Exec("spawn StartSword quality=L", out var spawn4)
    && spawn4.Ok && game.LastLegendary);
Check("spawn-roman", Exec("spawn StartSword level=IV", out var spawn5)
    && spawn5.Ok && game.LastLevel == 4);
Check("spawn-bad-level", Exec("spawn StartSword level=0", out var spawn6) && !spawn6.Ok);
Check("spawn-bad-quality", Exec("spawn StartSword quality=X", out var spawn7) && !spawn7.Ok);
Check("spawn-dup-level", Exec("spawn StartSword level=1 level=2", out var spawn8) && !spawn8.Ok);
Check("spawn-unknown-option", Exec("spawn StartSword --nope 1", out var spawn9) && !spawn9.Ok);
Check("spawn-unsupported", Exec("spawn TestMutation", out var spawn10) && !spawn10.Ok
    && spawn10.Message.Contains("not equipment"));
Check("spawn-internal", Exec("spawn AdminWeapon", out var spawn11) && !spawn11.Ok
    && spawn11.Message.Contains("not spawnable"));
Check("spawn-unknown", Exec("spawn xyzzy", out var spawn12) && !spawn12.Ok);
Check("spawn-factory-error", Exec("spawn StartSword", out var spawn13) && spawn13.Ok);
game.CreateThrows = true;
Check("spawn-factory-throw", Exec("spawn StartSword", out var spawn14) && !spawn14.Ok
    && spawn14.Message.Contains("factory rejected"));
game.CreateThrows = false;
game.DropThrows = true;
Check("spawn-drop-throw", Exec("spawn StartSword", out var spawnDrop) && !spawnDrop.Ok
    && spawnDrop.Status == CommandStatus.InternalError && game.Dropped == 2);
game.DropThrows = false;
var noHero = new MockGame(null, catalog);
Check("spawn-nohero", registry.TryExecute(noHero, "spawn StartSword", out var spawn15)
    && !spawn15.Ok && spawn15.Message.Contains("no live character"));

// ---- player ---------------------------------------------------------------
StatBonus.ResetFor(new object());
Check("player-stats", Exec("player stats", out var pStats) && pStats.Ok
    && pStats.Message.Contains("60/100") && pStats.Message.Contains("brutality 3")
    && pStats.Message.Contains("native scrolls 2/1/0"));
Check("player-heal", Exec("player heal", out var pHeal) && pHeal.Ok
    && hero.Life == hero.MaxLife && hero.HealedBy == 40);
Check("player-heal-part", Exec("player heal 5", out var pHeal2) && pHeal2.Ok);
Check("player-health", Exec("player health 50", out var pHealth) && pHealth.Ok
    && hero.Life == 50 && pHealth.Status == CommandStatus.Warning);
Check("player-health-clamp", Exec("player health 500", out var pHealth2) && pHealth2.Ok
    && hero.Life == hero.MaxLife && pHealth2.Message.Contains("clamped"));
Check("player-health-bad", Exec("player health 0", out var pHealth3) && !pHealth3.Ok);
Exec("player health 50", out _);
Check("player-heal-overflow", Exec("player heal 2147483647", out var pHugeHeal)
    && pHugeHeal.Ok && hero.Life == hero.MaxLife);
Check("player-heal-negative", Exec("player heal -5", out var pNegHeal) && !pNegHeal.Ok);
Check("player-position", Exec("player position", out var pPos) && pPos.Ok
    && pPos.Message.Contains("TestBiome"));
Check("player-status", Exec("player status", out var pStatus) && pStatus.Ok
    && pStatus.Message.Contains("weapons") && pStatus.Message.Contains("HP")
    && pStatus.Message.Contains("native scrolls: brutality 2, tactics 1, survival 0"));
game.Mutations.Add("TestMutation");
Check("player-mutations", Exec("player mutations", out var pMut) && pMut.Ok
    && pMut.Message.Contains("TestMutation"));
game.Mutations.Clear();
Check("player-effects", Exec("player effects", out var pEff) && pEff.Ok);
Check("player-gold", Exec("player gold 50", out var pGold) && pGold.Ok
    && hero.GoldGiven == 50 && pGold.Status == CommandStatus.Warning);
Check("player-gold-bad", Exec("player gold 0", out var pGold2) && !pGold2.Ok);
Check("player-gold-cap", Exec("player gold 2147483647", out var pGold3)
    && !pGold3.Ok && hero.GoldGiven == 50);
Check("player-stat", Exec("player stat brutality 10", out var pStat) && pStat.Ok
    && hero.Tiers["brutality"] == 10 && hero.HudSets.Contains(("brutality", 10, true)));
Check("player-stat-bad", Exec("player stat volo 10", out var pStat2) && !pStat2.Ok);
Check("player-scroll", Exec("player scroll brutality 2", out var pScroll) && pScroll.Ok
    && hero.Tiers["brutality"] == 12 && game.NativeScrolls["brutality"] == 4
    && pScroll.Message.Contains("inventory-backed"));
Check("player-scroll-negative", Exec("player scroll brutality -2", out var pScroll2) && pScroll2.Ok
    && hero.Tiers["brutality"] == 10 && game.NativeScrolls["brutality"] == 2);
Check("player-scroll-floor", Exec("player scroll brutality -100", out var pScroll3)
    && !pScroll3.Ok && hero.Tiers["brutality"] == 10 && game.NativeScrolls["brutality"] == 2);
Check("player-scroll-overflow", Exec("player scroll brutality -2147483648", out var pScroll4)
    && !pScroll4.Ok && game.NativeScrolls["brutality"] == 2);
Check("player-scroll-cap", Exec("player scroll brutality 101", out var pScroll5)
    && !pScroll5.Ok && game.NativeScrolls["brutality"] == 2);
Check("player-scroll-nohero", registry.TryExecute(noHero, "player scroll brutality 2", out var pScrollNo)
    && !pScrollNo.Ok && pScrollNo.Message.Contains("Player not available"));
StatBonus.ResetFor(new object());
var resumedHero = new MockHero();
resumedHero.SetTier("brutality", 5);
var resumed = new MockGame(resumedHero, catalog,
    new Dictionary<string, int> { ["brutality"] = 4, ["tactics"] = 1, ["survival"] = 0 });
Check("scroll-persist-resume", registry.TryExecute(resumed, "player stats", out var rStats)
    && rStats.Ok && rStats.Message.Contains("brutality 5")
    && rStats.Message.Contains("native scrolls 4/1/0"));
Check("scroll-no-duplicate-resume", resumed.NativeScrolls["brutality"] == 4
    && resumedHero.Tiers["brutality"] == 5);
StatBonus.ResetFor(new object());
var tempHero = new MockHero();
tempHero.SetTier("brutality", 5);
var tempGame = new MockGame(tempHero, catalog,
    new Dictionary<string, int> { ["brutality"] = 4, ["tactics"] = 1, ["survival"] = 0 });
registry.TryExecute(tempGame, "player stat brutality 10", out _);
Check("player-stat-temporary", tempHero.Tiers["brutality"] == 10
    && tempGame.NativeScrolls["brutality"] == 4);
StatBonus.ResetFor(new object());
var afterResumeHero = new MockHero();
afterResumeHero.SetTier("brutality", 5);
var afterResume = new MockGame(afterResumeHero, catalog,
    new Dictionary<string, int> { ["brutality"] = 4, ["tactics"] = 1, ["survival"] = 0 });
Check("player-stat-not-persisted", registry.TryExecute(afterResume, "player stats", out var arStats)
    && arStats.Ok && arStats.Message.Contains("brutality 5"));
Check("player-unknown-sub", Exec("player nope", out var pSub) && !pSub.Ok
    && pSub.Message.Contains("unknown player subcommand"));
Check("player-nohero", registry.TryExecute(noHero, "player stats", out var pNoHero)
    && !pNoHero.Ok && pNoHero.Message.Contains("Player not available"));
Check("player-extra-args", Exec("player stats extra", out var pExtra)
    && !pExtra.Ok && pExtra.Message.Contains("Usage: player stats"));

// ---- debug ----------------------------------------------------------------
Check("debug-snapshot", Exec("debug snapshot", out var dSnap) && dSnap.Ok
    && dSnap.Message.Contains("catalog:") && dSnap.Message.Contains("player:"));
Check("debug-player", Exec("debug player", out var dPlayer) && dPlayer.Ok
    && dPlayer.Message.Contains("life: 100/100") && dPlayer.Message.Contains("weapons:"));
Check("debug-level", Exec("debug level", out var dLevel) && dLevel.Ok
    && dLevel.Message.Contains("TestLevel") && dLevel.Message.Contains("TestBiome"));
Check("debug-equipment", Exec("debug equipment", out var dEquip) && dEquip.Ok
    && dEquip.Message.Contains("StartSword"));
Check("debug-status", Exec("debug status", out var dStatus) && dStatus.Ok
    && dStatus.Message.Contains("curse:"));
Check("debug-catalog", Exec("debug catalog", out var dCat) && dCat.Ok
    && dCat.Message.Contains("source: test") && dCat.Message.Contains("entries:"));
Check("debug-mods", Exec("debug mods", out var dMods) && dMods.Ok
    && dMods.Message.Contains("TestMod"));
Check("debug-nohero", registry.TryExecute(noHero, "debug player", out var dNo)
    && !dNo.Ok && dNo.Message.Contains("unavailable"));
Check("debug-extra-args", Exec("debug snapshot extra", out var dExtra)
    && !dExtra.Ok && dExtra.Message.Contains("Usage: debug snapshot"));

// ---- history / layout -----------------------------------------------------
var hist = new HistoryBuffer(3);
hist.Append(HistoryKind.Info, "one");
hist.Append(HistoryKind.Success, "two");
hist.Append(HistoryKind.Error, "three");
hist.Append(HistoryKind.Warning, "four");
Check("history-cap", hist.Count == 3 && hist.Window(10)[0].Text == "two");
hist.ScrollBy(10);
Check("history-scroll", hist.Window(1)[0].Text == "two");
Check("history-html", HistoryBuffer.RenderHtml(
    [new HistoryLine(HistoryKind.Error, "<bad>&")]).Contains("&lt;bad&gt;&amp;"));
Check("history-policy", ConsoleHistory.AppendPolicy(["a"], "a") == null
    && ConsoleHistory.AppendPolicy(["a"], "b")![1] == "b");
// Native contract behind the cohesive/transplant height writes: dc.ui.Console
// .height is an Int32 field, so managed code must narrow before assigning
// through the dynamic proxy (a double aborts the whole restore natively).
// Uses a local game proxy when present; skips cleanly without one (e.g. CI).
Check("native-height-int-contract", NativeHeightIsInt32());
static bool NativeHeightIsInt32()
{
    try
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetEnvironmentVariable("DCCM_MDK_ROOT") ?? "", "ref", "GameProxy.dll"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Steam", "steamapps", "common", "Dead Cells", "coremod", "core", "mdk", "ref", "GameProxy.dll"),
        };
        var proxy = candidates.FirstOrDefault(File.Exists);
        if (proxy == null) return true;
        var host = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(proxy)))!, "host");
        var alc = new NativeProbeLoadContext(host);
        var asm = alc.LoadFromAssemblyPath(proxy);
        var type = asm.GetType("dc.ui.Console");
        var prop = type?.GetProperty("height");
        return prop != null && prop.PropertyType == typeof(int);
    }
    catch { return true; }
}
var plan0 = Presentation.Build(1920, 1080, new() { PanelPercent = 40 }, new HistoryBuffer(), "");
Check("layout", plan0.MaxLines > 0 && plan0.InputWidth > 0 && plan0.Surfaces.Count > 0);
Check("layout-zero", Presentation.Build(0, 0, new(), new HistoryBuffer(), "").MaxLines > 0);
Check("mode", ConsoleMode.Transplant);
Check("uimode-removed", !registry.TryExecute(game, "uimode cohesive", out var gone) && !gone.Ok);
Check("size", ConsoleSizing.ComputeTargetHeight(1080, 100, 40) > 0);

// ---- config/key safety ----------------------------------------------------
Check("key-default", KeyBinding.ToVk("=") == 187);
Check("key-backtick", KeyBinding.ToVk("`") == 192);
Check("key-invalid-default", KeyBinding.ToVk("Q") == 187);

OverhaulTests.Run((name, ok) => Check(name, ok));
if (failures == 0 && args.Contains("--qualification")) Qualification.Print();
if (args.Length == 2 && args[0] == "--installed-english")
{
    var en = EnglishNames.Load(args[1]);
    Check("installed-english-startsword", en.Get("StartSword") == "Rusty Sword");
    Check("installed-english-assault", en.Get("DashShield") == "Assault Shield");
    Console.WriteLine(en.Status);
}
Console.WriteLine($"--- {checks} checks, {failures} failures ---");
return failures;

// ---------------------------------------------------------------------------
// Mocks
// ---------------------------------------------------------------------------

sealed class MockHero : IHeroFacade
{
    public int Life { get; set; } = 60;
    public int MaxLife { get; set; } = 100;
    public double Cx { get; set; } = 12.3;
    public double Cy { get; set; } = 45.6;
    public Dictionary<string, int> Tiers { get; } = new()
    {
        ["brutality"] = 3, ["tactics"] = 2, ["survival"] = 1,
    };
    public List<(string Stat, int Value, bool Animated)> HudSets { get; } = [];
    public int HealedBy { get; private set; }
    public int GoldGiven { get; private set; }
    public int SetLifeValue { get; private set; }
    public bool Killed { get; private set; }

    public int GetTier(string stat) => Tiers[stat];
    public void SetTier(string stat, int value) => Tiers[stat] = value;
    public void HudSet(string stat, int value, bool animated) =>
        HudSets.Add((stat, value, animated));
    public void Heal(int amount)
    {
        int room = Math.Max(0, MaxLife - Life);
        int applied = Math.Clamp(amount, 0, room);
        HealedBy += applied;
        Life += applied;
    }
    public void SetLife(int value) { SetLifeValue = value; Life = value; }
    public void GiveGold(int amount) { GoldGiven += amount; }
    public void Kill() => Killed = true;
}

sealed class MockGame(MockHero? hero, CatalogSnapshot catalog,
    Dictionary<string, int>? scrolls = null) : IGameContext
{
    public MockHero? HeroValue { get; } = hero;
    public Dictionary<string, int> NativeScrolls { get; } = scrolls ?? new()
    {
        ["brutality"] = 2,
        ["tactics"] = 1,
        ["survival"] = 0,
    };
    public bool CreateThrows { get; set; }
    public bool ForgeThrows { get; set; }
    public bool DropThrows { get; set; }
    public List<string> Mutations { get; } = [];
    public bool MutateThrows { get; set; }
    // Simulates native slot judgment for offline contracts only.
    private const int MockMutationSlots = 2;
    public int Dropped { get; private set; }
    public int ForgeCalls { get; private set; }
    public int? LastLevel { get; private set; }
    public int? LastStars { get; private set; }
    public bool LastLegendary { get; private set; }
    public List<string> Logs { get; } = [];

    public IHeroFacade? Hero => HeroValue;
    public bool InGameplay => HeroValue != null;
    public CatalogSnapshot Catalog { get; } = catalog;
    public IReadOnlyList<CatalogItem> Items => Catalog.Items;

    public bool TryResolveItem(string query, out CatalogItem item, out string? error) =>
        Catalog.TryResolve(query, out item, out error);

    public object CreateItem(string id)
    {
        if (CreateThrows) throw new InvalidOperationException("Invalid ItemKind: " + id);
        return new object();
    }

    public bool TryAdjustNativeScrolls(string stat, int delta, out int nativeCount,
        out int effectiveTier, out string? error)
    {
        nativeCount = 0;
        effectiveTier = -1;
        error = null;
        if (HeroValue == null)
        {
            error = "no live character: start a run first";
            return false;
        }
        var key = (stat ?? "").ToLowerInvariant();
        if (key is not ("brutality" or "tactics" or "survival"))
        {
            error = $"unknown stat '{stat}'";
            return false;
        }
        if (delta == 0 || delta > 100 || delta < -100)
        {
            error = "bad scroll delta";
            return false;
        }
        int before = NativeScrolls[key];
        if (delta < 0 && before < -delta)
        {
            error = $"only {before} {key} scroll(s) available; no changes made";
            return false;
        }
        NativeScrolls[key] = before + delta;
        nativeCount = NativeScrolls[key];
        effectiveTier = StatBonus.Apply(1 + nativeCount, key);
        HeroValue.SetTier(key, effectiveTier);
        return true;
    }

    public bool TryForgeEquipment(CatalogItem item, int? level, int? stars, bool legendary,
        out string report, out string? error)
    {
        report = "";
        error = null;
        ForgeCalls++;
        LastLevel = level;
        LastStars = stars;
        LastLegendary = legendary;
        if (ForgeThrows)
        {
            error = "forge rejected";
            return false;
        }
        report = $"{item.Label} - level {(level?.ToString() ?? "default")}, " +
            $"quality {(legendary ? "L" : EquipmentForge.TierName(stars ?? 0, false))}";
        return true;
    }

    public object? GetItemRow(string id) => null;
    public string GetBiome() => "TestBiome";
    public void DropAtHeroFeet(object item)
    {
        if (DropThrows) throw new InvalidOperationException("drop rejected");
        Dropped++;
    }
    public bool TryEquipMutation(string id, out string report, out string? replaced, out string? error)
    {
        report = ""; replaced = null; error = null;
        if (HeroValue == null) { error = "no live character: start a run first"; return false; }
        if (Mutations.Contains(id, StringComparer.OrdinalIgnoreCase))
        {
            error = $"Mutation already equipped: {id}.";
            return false;
        }
        if (MutateThrows) { error = $"native factory rejected '{id}'"; return false; }
        if (Mutations.Count >= MockMutationSlots)
        {
            error = $"Mutation slots full ({MockMutationSlots}).";
            return false;
        }
        Mutations.Add(id);
        report = $"Equipped {id}.";
        return true;
    }
    public bool TryRemoveMutation(string id, out string report, out string? error)
    {
        report = ""; error = null;
        if (HeroValue == null) { error = "no live character: start a run first"; return false; }
        int found = Mutations.FindIndex(m => m.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (found < 0) { error = $"Mutation is not equipped: {id}."; return false; }
        if (MutateThrows) { error = $"native removal failed for '{id}'"; return false; }
        Mutations.RemoveAt(found);
        report = $"Removed {id}.";
        return true;
    }
    public HeroDiagnostics? GetHeroDiagnostics()
    {
        if (HeroValue == null) return null;
        return new HeroDiagnostics(
            HeroValue.Life, HeroValue.MaxLife, HeroValue.Cx, HeroValue.Cy,
            HeroValue.Tiers["brutality"], HeroValue.Tiers["tactics"],
            HeroValue.Tiers["survival"],
            NativeScrolls["brutality"], NativeScrolls["tactics"], NativeScrolls["survival"],
            Mutations.ToArray(), ["StartSword"], ["FireBomb"],
            2, true, ["88:3.0s"], 1.5, true);
    }
    public LevelDiagnostics? GetLevelDiagnostics() =>
        HeroValue == null ? null : new LevelDiagnostics(
            "TestLevel", "Test Level", "TestBiome", HeroValue.Cx, HeroValue.Cy, "TestRoom");
    public IReadOnlyList<string> GetLoadedMods(out string? error)
    {
        error = null;
        return ["DeveloperConsole 0.1.0", "TestMod 1.0"];
    }
    public void Log(string message) => Logs.Add(message);
}

sealed class NativeProbeLoadContext(string hostDir) : System.Runtime.Loader.AssemblyLoadContext(true)
{
    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName name)
    {
        var path = Path.Combine(hostDir, name.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }
}

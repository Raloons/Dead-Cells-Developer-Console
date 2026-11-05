// Core command-framework types. No game dependencies: fully unit-testable.
namespace DeveloperConsole;

public enum CommandStatus { Info, Success, Warning, Error, InternalError }

// A command outcome. Warnings are successful commands that changed run state
// or could not read optional metadata; failures are user/native errors;
// InternalError is a guarded handler crash whose details go to the log.
public sealed record CommandResult(bool Ok, string Message, CommandStatus Status = CommandStatus.Info, Exception? Error = null)
{
    public static CommandResult Success(string message) => new(true, message, CommandStatus.Success);
    public static CommandResult Info(string message) => new(true, message, CommandStatus.Info);
    public static CommandResult Warning(string message) => new(true, message, CommandStatus.Warning);
    public static CommandResult Failure(string message) => new(false, message, CommandStatus.Error);
    public static CommandResult InternalError(string message, Exception? error = null) =>
        new(false, message, CommandStatus.InternalError, error);
}

// User-facing classification shown by help. Unspecified is reserved for
// third-party/future commands that do not declare a policy.
public enum CommandSafety
{
    Unspecified,
    ReadOnly,
    Temporary,
    RunState,
    PersistentRunState,
    UiOrConfig,
    Mixed,
}

public static class CommandSafetyText
{
    public static string Label(CommandSafety safety) => safety switch
    {
        CommandSafety.ReadOnly => "read-only",
        CommandSafety.Temporary => "temporary",
        CommandSafety.RunState => "run-state",
        CommandSafety.PersistentRunState => "persistent run-state",
        CommandSafety.UiOrConfig => "ui/config",
        CommandSafety.Mixed => "mixed",
        _ => "unspecified",
    };
}

// Argument specification: parsed positionally after validation.
public sealed record ArgSpec(string Name, bool Required, string Help);

// A subcommand inside a command group (player, item, debug, config).
public sealed record Subcommand(
    string Name,
    string Usage,
    string Description,
    Func<CommandContext, string[], CommandResult> Run,
    string[]? Examples = null,
    CommandSafety Safety = CommandSafety.Unspecified);

// Execution context handed to every command.
public sealed class CommandContext
{
    public required IGameContext Game { get; init; }
    public required string[] Args { get; init; }
}

// A registered command: metadata + handler (game access only via context).
public sealed class ConsoleCommand
{
    public required string Name { get; init; }
    public string[] Aliases { get; init; } = [];
    public required string Description { get; init; }
    public string Usage { get; init; } = "";
    public string Category { get; init; } = "General";
    public CommandSafety Safety { get; init; } = CommandSafety.Unspecified;
    public string[] Examples { get; init; } = [];
    public ArgSpec[] Args { get; init; } = [];
    public Subcommand[] Subcommands { get; init; } = [];
    public required Func<CommandContext, CommandResult> Handler { get; init; }
}

// Minimal hero facade so commands are testable without a live Haxe object.
public interface IHeroFacade
{
    int Life { get; }
    int MaxLife { get; }
    double Cx { get; }
    double Cy { get; }
    int GetTier(string stat);
    void SetTier(string stat, int value);
    void HudSet(string stat, int value, bool animated);
    void Heal(int amount);
    void SetLife(int value);
    void GiveGold(int amount);
    void Kill();
}

// Read-only hero snapshot. Unreadable fields are surfaced by the producer as
// unavailable values, never invented.
public sealed record HeroDiagnostics(
    int Life, int MaxLife, double Cx, double Cy,
    int Brutality, int Tactics, int Survival,
    int BrutalityScrolls, int TacticScrolls, int SurvivalScrolls,
    IReadOnlyList<string> MutationIds,
    IReadOnlyList<string> WeaponIds,
    IReadOnlyList<string> SkillIds,
    int CurseStacks, bool CurseKnown,
    IReadOnlyList<string> Affects,
    double InvisibilityTimer, bool InvisKnown);

// Read-only level snapshot. Null string means the engine did not expose it.
public sealed record LevelDiagnostics(
    string? LevelId, string? LevelName, string? Biome,
    double Cx, double Cy, string? RoomType);

// Game-integration seam. The live implementation talks Haxe; tests use a mock.
public interface IGameContext
{
    IHeroFacade? Hero { get; }
    bool InGameplay { get; }
    CatalogSnapshot Catalog { get; }
    IReadOnlyList<CatalogItem> Items { get; }
    string EquipmentSummary(object item) => "level/quality read-back unavailable";
    bool RefreshCatalog(out string message) { message = "Catalog refresh unavailable in this host."; return false; }

    bool TryResolveItem(string query, out CatalogItem item, out string? error);
    object CreateItem(string id);
    // Native scroll allocation: adds/removes BrutalityUp/TacticUp/SurvivalUp
    // inventory items and refreshes tiers through onEquipedItemsChange.
    bool TryAdjustNativeScrolls(string stat, int delta, out int nativeCount,
        out int effectiveTier, out string? error);
    // Native mutation equip/remove through the hero inventory. Implementations
    // must verify post-state and roll back partial changes on mismatch.
    bool TryEquipMutation(string id, out string report, out string? replaced, out string? error);
    bool TryRemoveMutation(string id, out string report, out string? error);
    bool TryForgeEquipment(CatalogItem item, int? level, int? stars, bool legendary,
        out string report, out string? error);
    object? GetItemRow(string id);
    string GetBiome();
    void DropAtHeroFeet(object item);
    HeroDiagnostics? GetHeroDiagnostics();
    LevelDiagnostics? GetLevelDiagnostics();
    IReadOnlyList<string> GetLoadedMods(out string? error);
    void Log(string message);
}

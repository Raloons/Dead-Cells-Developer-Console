// Standalone developer console bootstrap. The native console owns text input,
// history navigation, focus, and error display. This mod unhides it, routes
// submitted commands through a C# registry, and renders a larger custom
// output panel. Unknown commands fall through to the native dispatcher, so
// built-in commands (save/load/goto/gold/cells/...) keep working.
using dc.h2d;
using dc.ui;
using Hashlink.Proxy.Clousre;
using Hashlink.Proxy.Objects;
using HaxeProxy.Runtime;
using ModCore.Events.Interfaces.Game;
using ModCore.Mods;
using ModCore.Modules;
using ModCore.Utilities;
using Serilog;

namespace DeveloperConsole;

public class DeveloperConsoleMod(ModInfo info) : ModBase(info), IOnBeforeGameInit,
    ModCore.Events.Interfaces.Game.IOnAfterLoadingCDB
{
    private static readonly CommandRegistry Registry = new();
    private static readonly DevPanel Panel = new();
    private static HistoryBuffer history = new(200);
    private static ConsoleConfig config = ConsoleConfig.Defaults;
    private static string? modDir;
    private static int toggleKeyCode = KeyBinding.DefaultVk;
    private static object? console;
    private static string echoLine = "";
    private static double panelPercent = ConsoleSizing.DefaultPct;
    private static bool tickInit;
    private static bool? useWin32Key;
    private static bool initialized;
    private static bool catalogPrepared;
    private static string lastInput = "";
    private static IReadOnlyList<Completion> choices = [];
    private static readonly CompletionSelection completionSelection = new();
    private static bool tabWasDown;
    private static double sceneWidth, sceneHeight;

    static DeveloperConsoleMod()
    {
        CoreCommands.RegisterAll(Registry, () => config,
            pct =>
            {
                panelPercent = ConsoleSizing.ClampPct(pct); var message = Panel.Resize(panelPercent);
                return message;
            },
            ClearHistory, ReloadConfig, () => ConsoleConfig.ConfigPath(modDir));
        CatalogCommands.RegisterAll(Registry, () => config);
        PlayerCommands.RegisterAll(Registry);
        MutationCommands.RegisterAll(Registry);
        DebugCommands.RegisterAll(registry: Registry);
        ShortcutCommands.RegisterAll(Registry);
        Panel.InputFront = () => { try { if (console != null) dc.Main.Class.ME.root.addChildAt(((dynamic)console).tf, dc.Const.Class.ROOT_DP_DEBUG); } catch { } };
        Panel.InputLayout = PlaceInput;
    }

    internal static void ClearHistory()
    {
        history.Clear();
        Panel.Render(history, echoLine);
    }

    public override void Initialize()
    {
        if (initialized) return;
        initialized = true;
        try
        {
            modDir = Info.ModRoot?.Info?.FullName;
        }
        catch { modDir = null; }

        config = ConsoleConfig.LoadOrCreate(modDir, out var configWarning);
        if (!string.IsNullOrWhiteSpace(configWarning))
            Logger.Warning("DeveloperConsole config: {W}", configWarning);
        history = new HistoryBuffer(config.HistoryLimit);
        panelPercent = config.PanelPercent;
        toggleKeyCode = KeyBinding.ToVk(config.ToggleKey);
        Panel.Configure(config);

        var hooks = HashlinkHooks.Instance;
        hooks.CreateHook("ui.$Console", "__constructor__", Hook_Console_ctor).Enable();
        dc.h2d.Hook_Console.handleCommand += Hook_Console_handleCommand;
        dc.h2d.Hook_Console.log += Hook_Console_log;
        hooks.CreateHook("en.Hero", "computeTiers", Hook_computeTiers).Enable();
        // IOnFrameUpdate delivery is unreliable in this host; drive the tick
        // from Boot.update, the same pump the game itself uses.
        hooks.CreateHook("Boot", "update", Hook_Boot_update).Enable();
        Logger.Information("DeveloperConsole initialized ({N} commands, toggle {K})",
            Registry.All.Count, config.ToggleKey);
    }

    private static string ReloadConfig()
    {
        var next = ConsoleConfig.LoadOrCreate(modDir, out var warning);
        config = next;
        toggleKeyCode = KeyBinding.ToVk(next.ToggleKey);
        if (history.Capacity != next.HistoryLimit)
            history = history.WithCapacity(next.HistoryLimit);
        panelPercent = next.PanelPercent;
        Panel.Configure(next);
        choices = []; completionSelection.Reset();
        Panel.SetContext(choices, -1);
        if (Panel.IsBuilt)
        {
            Panel.Resize(panelPercent);
            if (ConsoleMode.Transplant) ApplyTransplantVisuals(); else ApplyCohesiveVisuals();
        }
        var message = "configuration reloaded: " + next.Describe();
        if (!string.IsNullOrWhiteSpace(warning)) message += "\nwarning: " + warning;
        return message;
    }

    public delegate void orig_boot_update(object self, double dt);

    private static int tickCount;
    private static int lastHeartbeat;
    private static int bootErrLogged;

    private static void Hook_Boot_update(orig_boot_update orig, object self, double dt)
    {
        try { orig(self, dt); }
        catch (Exception ex)
        {
            if (bootErrLogged < 3)
            {
                bootErrLogged++;
                Log.Warning("DeveloperConsole boot-orig failed: {E}", ex.ToString());
            }
            return;
        }
        try
        {
            TickKeys();
            SyncPanel();
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole frame tick failed: {E}", ex.Message);
        }
        tickCount++;
        if (config.VerboseLog && tickCount - lastHeartbeat >= 600)
        {
            lastHeartbeat = tickCount;
            bool toggleKey = false;
            try { toggleKey = IsDown(toggleKeyCode); } catch { }
            Log.Information(
                "DeveloperConsole tick #{N} toggle={G} explicit={E} panel={P} focused={F}",
                tickCount, toggleKey, explicitOpen, Panel.IsVisible, GameWindowFocused());
        }
    }

    private static void Hook_Console_log(
        dc.h2d.Hook_Console.orig_log orig,
        dc.h2d.Console self, dc.String text, int? color)
    {
        try
        {
            var message = text?.ToString() ?? "";
            // Our own Print() calls already added the command result to the
            // history; mirroring them again here would duplicate every line.
            if (!suppressLogMirror && !string.IsNullOrWhiteSpace(message))
            {
                history.Append(HistoryKind.Info, message);
                if (Panel.IsBuilt) Panel.Render(history, echoLine);
            }
        }
        catch { }
        orig(self, text, color);
    }

    public delegate void orig_computeTiers(dc.en.Hero hero);

    private static void Hook_computeTiers(orig_computeTiers orig, dc.en.Hero hero)
    {
        orig(hero);
        try
        {
            StatBonus.ResetFor(((HaxeProxyBase)(object)hero).HashlinkPointer);
            if (!StatBonus.Any()) return;
            hero.setBrutalityTier(StatBonus.Apply(hero.brutalityTier, "brutality"));
            hero.setTacticTier(StatBonus.Apply(hero.tacticTier, "tactics"));
            hero.setSurvivalTier(StatBonus.Apply(hero.survivalTier, "survival"));
            bool quiet = false;
            var r = new Ref<bool>(ref quiet);
            hero.hudSetBrutality(hero.brutalityTier, r);
            hero.hudSetTactic(hero.tacticTier, r);
            hero.hudSetSurvival(hero.survivalTier, r);
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole stat re-apply failed: {E}", ex.Message);
        }
    }

    void IOnBeforeGameInit.OnBeforeGameInit()
    {
        Logger.Information("DeveloperConsole ready ({N} commands)", Registry.All.Count);
    }

    void ModCore.Events.Interfaces.Game.IOnAfterLoadingCDB.OnAfterLoadingCDB(dc._Data_ cdb)
    {
        try
        {
            LiveGameContext.Instance.NoteData(cdb);
            catalogPrepared = false;
            Logger.Information("DeveloperConsole: live item catalog linked");
            EnsurePanel();
        }
        catch (Exception ex)
        {
            Logger.Warning("DeveloperConsole data link failed: {E}", ex.Message);
        }
    }

    private static void Hook_Console_ctor(HashlinkClosure orig, HashlinkObject self)
    {
        orig.DynamicInvoke(self);
        try
        {
            Panel.Reset(); explicitOpen = false; nativeHeight = -1;
            console = self.AsHaxe<dc.ui.Console>();
            try { ((dynamic)console).hide(); } catch { }
            Log.Information("DeveloperConsole console captured (starts hidden)");
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole console capture failed: {E}", ex.Message);
        }
    }

    private static void Hook_Console_handleCommand(
        dc.h2d.Hook_Console.orig_handleCommand orig,
        dc.h2d.Console self, dc.String command)
    {
        var line = command?.ToString() ?? "";
        try
        {
            // Commands are available only during active gameplay. This also
            // prevents native console state from leaking across menu screens.
            if (!LiveGameContext.Instance.InGameplay) return;

            if (Registry.TryExecute(LiveGameContext.Instance, line, out var result,
                    g => g.InGameplay))
            {
                if (result.Status == CommandStatus.InternalError && result.Error != null)
                    Log.Error(result.Error, "DeveloperConsole command failed: {Line}", line);
                AppendLine(line);
                AppendResult(result);
                Print(result.Status is CommandStatus.Error or CommandStatus.InternalError
                    ? "Error: " + result.Message : result.Message);
                ConsoleHistory.Record(self, line);
                return;
            }

            // Unknown command. Show a typo suggestion for our own commands and
            // swallow it; otherwise let the native dispatcher handle it.
            if (config.Suggestions && result.Status == CommandStatus.Error &&
                result.Message.Contains("did you mean", StringComparison.Ordinal))
            {
                AppendLine(line);
                AppendResult(result);
                Print("Error: " + result.Message);
                ConsoleHistory.Record(self, line);
                return;
            }
        }
        catch (Exception ex)
        {
            history.Append(HistoryKind.Error, "console failure: " + ex.Message);
            Panel.Render(history, echoLine);
            Print("Error: console failure: " + ex.Message);
            return;
        }
        orig(self, command);
    }

    private static void AppendLine(string line)
    {
        echoLine = line;
        history.Append(HistoryKind.Info, "> " + line);
        history.ScrollToBottom();
    }

    private static void AppendResult(CommandResult result)
    {
        var kind = result.Status switch
        {
            CommandStatus.Success => HistoryKind.Success,
            CommandStatus.Warning => HistoryKind.Warning,
            CommandStatus.Error or CommandStatus.InternalError => HistoryKind.Error,
            _ => HistoryKind.Info,
        };
        var prefix = result.Status switch
        {
            CommandStatus.Warning => "Warning: ",
            CommandStatus.Error or CommandStatus.InternalError => "Error: ",
            _ => "",
        };
        history.Append(kind, prefix + result.Message);
        history.ScrollToBottom();
        Panel.Render(history, echoLine);
    }

    private const int EscapeKey = 27;
    private const int PageUpKey = 33;
    private const int PageDownKey = 34;
    private static bool toggleWasDown;
    private static bool escWasDown;
    private static bool pgUpWasDown;
    private static bool pgDnWasDown;
    private static object? keyIsDownFn;

    private static bool IsDown(int code)
    {
        if (useWin32Key == null || useWin32Key == false)
        {
            try
            {
                keyIsDownFn ??= Hashlink.Marshaling.HashlinkMarshal
                    .FindFunction("hxd.Key", "isDown");
                bool down = Convert.ToBoolean(((dynamic)keyIsDownFn).DynamicInvoke(code));
                useWin32Key = false;
                return down;
            }
            catch
            {
                useWin32Key = true;
                Log.Warning("DeveloperConsole key poll: hxd.Key unavailable, using Win32 fallback");
            }
        }
        return (GetAsyncKeyState(code) & 0x8000) != 0;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vkey);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();

    private static bool GameWindowFocused()
    {
        try
        {
            var main = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
            if (main == nint.Zero) return true;
            return GetForegroundWindow() == main;
        }
        catch { return true; }
    }

    private static void ResetKeyEdges()
    {
        toggleWasDown = false;
        escWasDown = false;
        pgUpWasDown = false;
        pgDnWasDown = false;
        tabWasDown = false;
    }

    private static bool NativeInputEmpty()
    {
        try
        {
            if (console == null) return true;
            dynamic tf = ((dynamic)console).tf;
            try
            {
                string? s = tf.get_text()?.ToString();
                if (s != null) return s.Length == 0;
            }
            catch { }
            try
            {
                string? s = tf.text?.ToString();
                if (s != null) return s.Length == 0;
            }
            catch { }
            return false;
        }
        catch { return false; }
    }

    private static void TickKeys()
    {
        bool inGame;
        try { inGame = LiveGameContext.Instance.InGameplay; }
        catch { inGame = false; }
        if (!inGame)
        {
            if (Panel.IsVisible)
            {
                try { if (console != null) ((dynamic)console).hide(); } catch { }
                Panel.SetVisible(false);
            }
            ResetKeyEdges();
            return;
        }
        if (!GameWindowFocused())
        {
            ResetKeyEdges();
            return;
        }
        bool toggle = IsDown(toggleKeyCode);
        bool esc = IsDown(EscapeKey);
        if (!tickInit)
        {
            tickInit = true;
            toggleWasDown = toggle;
            escWasDown = esc;
            pgUpWasDown = IsDown(PageUpKey);
            pgDnWasDown = IsDown(PageDownKey);
        }
        else
        {
            if (toggle && !toggleWasDown)
            {
                if (!Panel.IsVisible || NativeInputEmpty()) Toggle();
            }
            if (esc && !escWasDown && Panel.IsVisible) Toggle();
        }
        toggleWasDown = toggle;
        escWasDown = esc;

        bool pgUp = IsDown(PageUpKey);
        if (pgUp && !pgUpWasDown && Panel.IsVisible)
        {
            history.ScrollBy(Math.Max(1, Panel.VisibleLines));
            Panel.Render(history, echoLine);
        }
        pgUpWasDown = pgUp;
        bool pgDn = IsDown(PageDownKey);
        if (pgDn && !pgDnWasDown && Panel.IsVisible)
        {
            history.ScrollBy(-Math.Max(1, Panel.VisibleLines));
            Panel.Render(history, echoLine);
        }
        pgDnWasDown = pgDn;
        TickCompletion();
    }

    private static void TickCompletion()
    {
        if (!explicitOpen || !Panel.IsVisible || console == null) return;
        bool tab = IsDown(9);
        if (!config.Suggestions) { tabWasDown = tab; return; }
        if (tickCount % 6 == 0 || tab && !tabWasDown)
        {
            string input = "";
            try { input = ((dynamic)console).tf.get_text()?.ToString() ?? ""; } catch { }
            if (input != lastInput)
            {
                lastInput = input;
                completionSelection.Reset();
                choices = CompletionEngine.Complete(input, Registry, LiveGameContext.Instance.Catalog, config.SuggestionLimit);
                Panel.SetContext(choices, -1);
                Panel.Render(history, echoLine);
            }
        }
        if (config.TabCompletion && tab && !tabWasDown)
        {
            var selected = completionSelection.Move(choices, IsDown(16) ? -1 : 1);
            if (selected != null)
            {
                try
                {
                    ((dynamic)console).tf.set_text(selected.Text.AsHaxeString());
                    try { ((dynamic)console).tf.cursorIndex = selected.Text.Length; } catch { }
                    lastInput = selected.Text;
                    Panel.SetContext(choices, completionSelection.Selected);
                    Panel.Render(history, echoLine);
                }
                catch { }
            }
        }
        tabWasDown = tab;
    }

    private static bool explicitOpen;

    private static void Toggle()
    {
        try
        {
            if (console == null) return;
            if (explicitOpen)
            {
                explicitOpen = false;
                try { ((dynamic)console).hide(); } catch { }
                Panel.SetVisible(false);
                Log.Information("DeveloperConsole explicit close");
            }
            else
            {
                bool inGame = false;
                try { inGame = LiveGameContext.Instance.InGameplay; } catch { }
                if (!inGame)
                {
                    Log.Information("DeveloperConsole: available during active gameplay only");
                    return;
                }
                EnsurePanel();
                if (!catalogPrepared)
                {
                    LiveGameContext.Instance.RefreshCatalog(out _);
                    catalogPrepared = true;
                }
                choices = []; lastInput = ""; completionSelection.Reset();
                try
                {
                    dynamic c0 = (dynamic)console;
                    double w0 = sceneWidth;
                    double h0 = sceneHeight;
                    double y0 = 0;
                    if (w0 > 0 && y0 + h0 > 0)
                        Panel.ApplyLayout(w0, y0 + h0, panelPercent);
                }
                catch { }
                ((dynamic)console).show();
                if (ConsoleMode.Transplant) ApplyTransplantVisuals();
                else ApplyCohesiveVisuals();
                explicitOpen = true;
                Panel.SetVisible(true);
                Panel.Render(history, echoLine);
                ClearNativeInput();
                Log.Information("DeveloperConsole explicit open");
            }
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole toggle failed: {E}", ex.Message);
        }
    }

    private static void ClearNativeInput()
    {
        try
        {
            if (console == null) return;
            ((dynamic)console).tf.set_text("".AsHaxeString());
        }
        catch { }
    }

    private static void SyncPanel()
    {
        if (!Panel.IsBuilt || console == null) return;
        try
        {
            dynamic c = (dynamic)console;
            if (explicitOpen && Panel.IsVisible)
            {
                if (tickCount % 120 == 0)
                {
                    try
                    {
                        dynamic scene = dc.Main.Class.ME.root.getScene();
                        double w = Convert.ToDouble(scene.width), h = Convert.ToDouble(scene.height);
                        if (w > 0 && h > 0 && (w != sceneWidth || h != sceneHeight))
                        {
                            sceneWidth = w; sceneHeight = h;
                            Panel.ApplyLayout(w, h, panelPercent);
                            if (ConsoleMode.Transplant) ApplyTransplantVisuals();
                        }
                    }
                    catch { }
                }
                try { c.bg.set_visible(true); } catch { }
                if (ConsoleMode.Transplant)
                {
                    try { c.logTxt.set_visible(false); } catch { }
                }
                return;
            }
            bool was = Panel.IsVisible;
            try { c.bg.set_visible(false); } catch { }
            try { c.logTxt.set_visible(false); } catch { }
            Panel.SetVisible(false);
            explicitOpen = false;
            if (was) Log.Information("DeveloperConsole auto-show suppressed (explicit=false)");
        }
        catch { }
    }

    private static void EnsurePanel()
    {
        if (Panel.IsBuilt || console == null) return;
        try
        {
            dynamic c = (dynamic)console;
            double w = Convert.ToDouble(c.width);
            double h = Convert.ToDouble(c.height);
            double y = Convert.ToDouble(c.y);
            double sceneH = y + h;
            if (sceneH <= 0 || w <= 0) return;
            sceneWidth = w; sceneHeight = sceneH;
            dynamic main = dc.Main.Class.ME;
            dynamic root = main.root;
            dynamic font = dc.Assets.Class.font12;
            int layer = dc.Const.Class.ROOT_DP_DEBUG;
            Log.Information("DeveloperConsole panel: {R}",
                Panel.Build(root, font, layer, w, sceneH, panelPercent));
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole panel build failed: {E}", ex.Message);
        }
    }

    private static double nativeHeight = -1;

    private static void ApplyTransplantVisuals()
    {
        try
        {
            if (console == null) return;
            if (!Panel.IsBuilt) { ApplyCohesiveVisuals(); return; }
            Panel.Configure(config);
            if (!Panel.IsBuilt) { ApplyCohesiveVisuals(); return; }
            dynamic c = (dynamic)console;
            try
            {
                if (nativeHeight < 0) nativeHeight = Convert.ToDouble(c.height);
                c.height = 2;
            }
            catch { }
            dynamic tf = c.tf;
            dynamic main = dc.Main.Class.ME;
            dynamic root = main.root;
            int layer = dc.Const.Class.ROOT_DP_DEBUG;
            root.addChildAt(tf, layer);
            PlaceInput();
            AttachWheel();
            double rx = -1, ry = -1;
            try { rx = Convert.ToDouble(tf.x); ry = Convert.ToDouble(tf.y); } catch { }
            Log.Information("DeveloperConsole transplant field at ({X:F0},{Y:F0})", rx, ry);
            Log.Information("DeveloperConsole {P}", Panel.PlacePrompt(rx, ry));
            // The native console object stays visible: bg.visible feeds
            // isActive(), which drives the engine's input lock.
            Log.Information("DeveloperConsole transplant visuals applied");
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole transplant visuals failed: {E}", ex.Message);
        }
    }

    // Best-effort mouse-wheel scrolling via the input field's own Interactive.
    // The delegate is rooted in a static field; Haxe holds no managed root.
    // If the proxy rejects the assignment, keyboard scrolling remains.
    private static object? wheelHandler;
    private static void AttachWheel()
    {
        try
        {
            if (console == null) return;
            dynamic iact = ((dynamic)console).tf.interactive;
            if (iact == null) return;
            try { if (iact.onWheel != null) return; } catch { }
            wheelHandler = new HlAction<dc.hxd.Event>(OnConsoleWheel);
            iact.onWheel = wheelHandler;
        }
        catch { }
    }

    private static void OnConsoleWheel(dc.hxd.Event e)
    {
        try
        {
            if (!explicitOpen || !Panel.IsVisible) return;
            double delta = 0;
            try { delta = e.wheelDelta; } catch { return; }
            if (delta == 0) return;
            // Positive wheelDelta moves toward older output.
            int page = Math.Max(1, Panel.VisibleLines);
            history.ScrollBy(delta > 0 ? page : -page);
            Panel.Render(history, echoLine);
        }
        catch { }
    }

    private static void PlaceInput()
    {
        if (console == null || !ConsoleMode.Transplant) return;
        try
        {
            dynamic tf = ((dynamic)console).tf;
            SceneTransform.Apply(tf, Panel.InputX, Panel.InputY, Panel.InputScale);
            // Native integer fields reject double assignments at runtime;
            // always narrow before writing through the dynamic proxy.
            tf.maxWidth = Math.Max(1, (int)Math.Round(Panel.InputWidth / Panel.InputScale));
        }
        catch { }
    }

    private static void ApplyCohesiveVisuals()
    {
        try
        {
            if (console == null) return;
            dynamic c = (dynamic)console;
            try
            {
                // Console.height is an integer native field; a double
                // assignment fails at runtime (observed natively as
                // "input-row restore failed") and aborts the whole restore.
                if (nativeHeight > 0) c.height = (int)nativeHeight;
                dynamic tf = c.tf;
                dynamic bg = c.bg;
                bg.addChild(tf);
                SceneTransform.Apply(tf, 2.0, 1.0);
                tf.maxWidth = Math.Max(1, (int)Math.Round(sceneWidth - 4));
            }
            catch (Exception ex)
            {
                Log.Warning("DeveloperConsole input-row restore failed: {E}", ex.Message);
            }
            Log.Information("DeveloperConsole cohesive visuals applied");
        }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole cohesive visuals failed: {E}", ex.Message);
        }
    }

    private static bool suppressLogMirror;

    internal static void Print(string message)
    {
        try
        {
            if (console != null)
            {
                suppressLogMirror = true;
                try { ((dynamic)console).log(message.AsHaxeString(), null); }
                finally { suppressLogMirror = false; }
                return;
            }
        }
        catch { suppressLogMirror = false; }
        Log.Information("DeveloperConsole: {M}", message);
    }
}

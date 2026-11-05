// Shared command history with the native console input.
//
// The native handleCommand pushes submitted lines into console.logs and
// handleKey Up/Down walks that same list. Commands handled by this mod are
// swallowed before native dispatch, so we mirror the recording: push the
// line, reset logIndex, and let the native key handler do recall/editing.
// No key interception and no parallel history store.
using ModCore.Utilities;
using Serilog;

namespace DeveloperConsole;

public static class ConsoleHistory
{
    public const int KeepNewest = 100;
    public const int CapTotal = 200;
    private const int ReadCap = 210;

    public static void Record(object console, string line)
    {
        try
        {
            var text = line?.Trim() ?? "";
            if (text.Length == 0) return;
            dynamic c = console;
            dynamic logs;
            try { logs = c.logs; }
            catch { return; }
            if (logs == null) return;
            try
            {
                int n = (int)logs.length;
                if (n > 0)
                {
                    dynamic last = logs.getDyn(n - 1);
                    string? s = last?.ToString();
                    if (s != null && string.Equals(s, text, StringComparison.Ordinal)) return;
                }
            }
            catch { }
            try
            {
                int n = (int)logs.length;
                if (n >= CapTotal) logs.splice(0, n - KeepNewest);
            }
            catch (Exception ex)
            {
                Log.Warning("DeveloperConsole history trim failed: {E}", ex.Message);
            }
            try { logs.push(text.AsHaxeString()); }
            catch (Exception ex)
            {
                Log.Warning("DeveloperConsole history push failed: {E}", ex.Message);
                return;
            }
            try { c.logIndex = -1; }
            catch { }
        }
        catch { }
    }

    public static List<string>? AppendPolicy(IReadOnlyList<string> existing, string line)
    {
        var text = line?.Trim() ?? "";
        if (text.Length == 0) return null;
        if (existing.Count > 0 && string.Equals(existing[^1].Trim(), text, StringComparison.Ordinal)) return null;
        var next = new List<string>(existing) { text };
        if (next.Count > CapTotal) next.RemoveRange(0, next.Count - KeepNewest);
        return next;
    }
}

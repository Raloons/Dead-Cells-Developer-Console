// Console sizing: enlarge the native console instead of rebuilding UI.
// h2d.Console.sync maintains y = scene.height - height and scales bg to the
// full width, so setting height to a percentage of the scene turns the
// one-line bar into a readable panel. Pure math is unit-tested; the field
// write is guarded by the caller.
namespace DeveloperConsole;

public static class ConsoleSizing
{
    public const double DefaultPct = 40.0;
    public const double MinPct = 10.0;
    public const double MaxPct = 80.0;
    public const double MinPx = 120.0;

    public static double ClampPct(double pct) =>
        Math.Clamp(double.IsFinite(pct) ? pct : DefaultPct, MinPct, MaxPct);

    public static double ComputeTargetHeight(double y, double height, double pct)
    {
        var sceneH = y + height;
        if (sceneH <= 0 || height <= 0) return -1;
        pct = ClampPct(pct);
        return Math.Max(MinPx, Math.Min(sceneH * 0.8, sceneH * pct / 100.0));
    }

    public static string Apply(dynamic console, double pct)
    {
        double y, h;
        try
        {
            y = Convert.ToDouble(console.y);
            h = Convert.ToDouble(console.height);
        }
        catch (Exception ex)
        {
            return "cannot read console layout: " + ex.Message;
        }
        var target = ComputeTargetHeight(y, h, pct);
        if (target < 0) return "layout not ready yet; run any command again";
        try
        {
            console.height = target;
        }
        catch (Exception ex)
        {
            return "cannot write console height: " + ex.Message;
        }
        return $"console height -> {target:F0}px ({ClampPct(pct):F0}% of scene)";
    }
}

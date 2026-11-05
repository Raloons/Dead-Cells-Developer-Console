using ModCore.Utilities;
using HaxeProxy.Runtime;
using Serilog;

namespace DeveloperConsole;

// Fixed pools of native primitives: solid rectangles plus game-font labels.
// Failure tears down partial resources and reports the panel unavailable.
public sealed class DevPanel
{
    private readonly List<dynamic> rectangles = [];
    private readonly List<dynamic> labels = [];
    private readonly Dictionary<string, dc.h3d.Vector> palette = [];
    private dynamic? root;
    private dynamic? font;
    private int layer;
    private double width, height, pct;
    private ConsoleConfig config = ConsoleConfig.Defaults;
    private PresentationPlan? plan;
    private HistoryBuffer lastHistory = new();
    private string lastEcho = "";
    private IReadOnlyList<Completion> completions = [];
    private int selection = -1;
    public Action? InputFront { get; set; }
    public Action? InputLayout { get; set; }
    public bool IsBuilt { get; private set; }
    public bool IsVisible { get; private set; }
    public double InputX => plan?.InputX ?? -1;
    public double InputY => plan?.InputY ?? -1;
    public double InputWidth => plan?.InputWidth ?? 0;
    public double InputScale => plan?.InputScale ?? 1;
    public int VisibleLines => plan?.MaxLines ?? 10;

    public void Configure(ConsoleConfig value) { config = value; if (IsBuilt) Render(lastHistory, lastEcho); }
    public string Build(dynamic sceneRoot, dynamic sceneFont, int debugLayer, double sceneW, double sceneH, double percent)
    {
        if (IsBuilt) return "panel already built";
        root = sceneRoot; font = sceneFont; layer = debugLayer;
        width = sceneW; height = sceneH; pct = percent;
        IsBuilt = true;
        Render(lastHistory, lastEcho);
        return IsBuilt ? "presentation pool built" : "panel unavailable; native bar retained";
    }
    public void SetVisible(bool value)
    {
        if (IsVisible == value) return;
        IsVisible = value;
        foreach (dynamic node in rectangles.Concat(labels))
            try { node.set_visible(value); } catch { }
        // Render hides unused pooled nodes again.
        if (value && IsBuilt) Render(lastHistory, lastEcho);
    }
    public void SetContext(IReadOnlyList<Completion> choices, int selected)
    { completions = choices; selection = selected; }

    public void Render(HistoryBuffer history, string echoLine)
    {
        if (!IsBuilt) return;
        lastHistory = history; lastEcho = echoLine;
        try { Draw(); }
        catch (Exception ex)
        {
            Log.Warning("DeveloperConsole presentation failed: {E}", ex.Message);
            DisposeNodes(); IsBuilt = false;
        }
    }
    private void Draw()
    {
        plan = Presentation.Build(width, height, config with { PanelPercent = pct }, lastHistory,
            lastEcho, completions, selection);
        bool grew = rectangles.Count < plan.Surfaces.Count;
        while (rectangles.Count < Math.Max(24, plan.Surfaces.Count))
        {
            dynamic tile = dc.h2d.Tile.Class.fromColor(0xFFFFFF, 1, 1, 1f, null);
            dynamic bitmap = new dc.h2d.Bitmap(tile, null);
            rectangles.Add(bitmap); root!.addChildAt(bitmap, layer);
        }
        while (labels.Count < plan.Captions.Count)
        {
            dynamic text = new dc.h2d.HtmlText(font, null);
            labels.Add(text); root!.addChildAt(text, layer);
        }
        // Appending background nodes must never cover an older text pool.
        if (grew) foreach (dynamic label in labels) root!.addChildAt(label, layer);
        for (int i = 0; i < rectangles.Count; i++)
        {
            dynamic node = rectangles[i]; node.set_visible(IsVisible && i < plan.Surfaces.Count);
            if (i >= plan.Surfaces.Count) continue;
            var r = plan.Surfaces[i]; SceneTransform.Apply(node, r.X, r.Y);
            node.tile.scaleToSize((int)Math.Ceiling(r.Width), (int)Math.Ceiling(r.Height));
            if (!palette.TryGetValue(r.Color, out var color))
            {
                int rgb = Convert.ToInt32(r.Color[1..], 16);
                double red = ((rgb >> 16) & 255) / 255.0, green = ((rgb >> 8) & 255) / 255.0,
                    blue = (rgb & 255) / 255.0, alpha = 1;
                palette[r.Color] = color = new dc.h3d.Vector(new Ref<double>(ref red),
                    new Ref<double>(ref green), new Ref<double>(ref blue), new Ref<double>(ref alpha));
            }
            ((dc.h2d.Bitmap)node).color = color; node.alpha = r.Alpha;
        }
        for (int i = 0; i < labels.Count; i++)
        {
            dynamic node = labels[i]; node.set_visible(IsVisible && i < plan.Captions.Count);
            if (i >= plan.Captions.Count) continue;
            var t = plan.Captions[i]; SceneTransform.Apply(node, t.X, t.Y, t.Size / 12.0);
            node.set_text(($"<font color=\"{t.Color}\">{HistoryBuffer.Escape(t.Text)}</font>").AsHaxeString());
        }
        if (grew) InputFront?.Invoke();
        InputLayout?.Invoke();
    }
    private void DisposeNodes()
    {
        foreach (dynamic node in labels.Concat(rectangles)) try { node.remove(); } catch { }
        labels.Clear(); rectangles.Clear();
    }
    public void Reset()
    { DisposeNodes(); palette.Clear(); IsBuilt = IsVisible = false; plan = null; root = font = null; }
    public string Resize(double percent) { pct = ConsoleSizing.ClampPct(percent); Render(lastHistory, lastEcho); return $"panel height: {pct:0}%"; }
    public string ApplyLayout(double sceneW, double sceneH, double percent)
    { width = sceneW; height = sceneH; return Resize(percent); }
    public string PlacePrompt(double fieldX, double fieldY) => "prompt owned by presentation plan";
}

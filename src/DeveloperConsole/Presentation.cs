namespace DeveloperConsole;

public sealed record Surface(double X, double Y, double Width, double Height, string Color, double Alpha = 1);
public sealed record Caption(double X, double Y, double Width, double Size, string Text, string Color, bool Mono = true);
public sealed record PresentationPlan(double InputX, double InputY, double InputWidth,
    int MaxLines, IReadOnlyList<Surface> Surfaces, IReadOnlyList<Caption> Captions)
{
    public double InputScale { get; init; } = 1;
}

// One scene description for the native h2d renderer: a traditional dark
// developer-console overlay. All ornament is original geometry; no game
// texture or localization is shipped.
public static class Presentation
{
    public static PresentationPlan Build(double width, double height, ConsoleConfig raw,
        HistoryBuffer history, string echo, IReadOnlyList<Completion>? completions = null,
        int selection = -1)
    {
        var config = ConsoleConfig.Normalize(raw, out _);
        width = Math.Max(320, double.IsFinite(width) ? width : 1280);
        height = Math.Max(240, double.IsFinite(height) ? height : 720);
        double scale = config.TextScale, pad = 12 * scale, line = 21 * scale;
        double w = Math.Max(300, width * config.WidthPercent / 100);
        double x = (width - w) / 2;
        double h = height * config.PanelPercent / 100;
        h = Math.Clamp(h, Math.Min(150 * scale, height * .8), height * .85);
        double y = height - h, header = 32 * scale;
        var surfaces = new List<Surface>(); var captions = new List<Caption>();
        const string accent = "#ABBBD0";
        void Rect(double px, double py, double rw, double rh, string color, double alpha = 1)
            => surfaces.Add(new(px, py, Math.Max(0, rw), Math.Max(0, rh), color, alpha));
        void Text(double px, double py, double tw, string text, string color, double size = 13, bool mono = true)
            => captions.Add(new(px, py, tw, size * scale, Fit(text, tw, size * scale), color, mono));
        Rect(x, y, w, h, "#0A0E14", config.Opacity);
        Text(x + pad, y + 6, w - pad * 2, "Console", accent, 13);
        double inputH = 32 * scale, inputY = height - inputH + 5 * scale;
        Rect(x, height - inputH, w, inputH, "#1A2631", .96);
        Rect(x, height - inputH, w, 1, accent, .65);
        // Prompt and input share font, scale and top edge, so their baselines
        // match by construction across console sizes.
        Text(x + pad, inputY, 22 * scale, ">", accent, 12);
        double contentW = w - pad * 2;
        int availableSuggestions = Math.Max(0, (int)((h - header - inputH - pad * 2 - line) / line) - 1);
        int count = Math.Min(availableSuggestions, Math.Min(config.SuggestionLimit, completions?.Count ?? 0));
        double suggestionH = count == 0 ? 0 : (count + 1) * line;
        double outputTop = y + header, outputBottom = height - inputH - pad - suggestionH;
        int maxLines = Math.Max(1, (int)((outputBottom - outputTop) / line));
        var wrapped = Wrap(history.Window(Math.Min(100, maxLines * 3)), contentW, 13 * scale).TakeLast(maxLines).ToArray();
        for (int i = 0; i < wrapped.Length; i++)
            Text(x + pad, outputTop + i * line, contentW, wrapped[i].Text, HistoryBuffer.ColorOf(wrapped[i].Kind));
        if (count > 0)
        {
            double sy = height - inputH - suggestionH;
            Rect(x + pad - 4, sy, contentW + 8, suggestionH, "#172333", .98);
            Text(x + pad, sy + 2, contentW, "Complete", accent, 10);
            for (int i = 0; i < count; i++)
            {
                if (i == selection) Rect(x + pad - 2, sy + line * (i + 1), contentW, line, accent, .15);
                Text(x + pad + 4, sy + line * (i + 1), contentW - 8, completions![i].Label, i == selection ? accent : "#DFE8E5", 13);
            }
        }
        return new(x + pad + 24 * scale, inputY, w - pad * 2 - 24 * scale, maxLines, surfaces, captions) { InputScale = scale };
    }

    internal static string Fit(string text, double width, double size)
    {
        int max = Math.Max(1, (int)(width / (size * .58)));
        return text.Length <= max ? text : text[..Math.Max(0, max - 1)] + "...";
    }

    internal static IEnumerable<HistoryLine> Wrap(IReadOnlyList<HistoryLine> lines, double width, double size)
    {
        int max = Math.Max(12, (int)(width / (size * .58)));
        foreach (var line in lines)
        {
            var text = line.Text;
            if (text.Length == 0) { yield return line; continue; }
            while (text.Length > max)
            {
                int split = text.LastIndexOf(' ', max - 1, max);
                if (split < max / 2) split = max;
                yield return line with { Text = text[..split] };
                text = text[split..].TrimStart();
            }
            if (text.Length > 0) yield return line with { Text = text };
        }
    }
}

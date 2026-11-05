// Bounded output history. Pure logic, unit-tested. Rendering (HTML wrapping,
// colors) is a thin projection over Window().
namespace DeveloperConsole;

public enum HistoryKind { Info, Success, Warning, Error }

public sealed record HistoryLine(HistoryKind Kind, string Text);

public sealed class HistoryBuffer
{
    private readonly List<HistoryLine> lines = [];
    private int scroll;
    public int Capacity { get; }
    public int Count => lines.Count;
    public int Scroll => scroll;

    public HistoryBuffer(int capacity = 200) => Capacity = Math.Clamp(capacity, 1, 2000);

    public void Clear()
    {
        lines.Clear();
        scroll = 0;
    }

    public HistoryBuffer WithCapacity(int capacity)
    {
        var next = new HistoryBuffer(capacity);
        foreach (var line in lines.TakeLast(next.Capacity)) next.Append(line.Kind, line.Text);
        return next;
    }

    public void Append(HistoryKind kind, string text)
    {
        foreach (var part in (text ?? "").Replace("\r", "").Split('\n'))
            lines.Add(new HistoryLine(kind, part));
        while (lines.Count > Capacity) lines.RemoveAt(0);
        // Preserve the reader's position: an offset from the end stays valid
        // when new lines arrive, so reading older output is never yanked to
        // the bottom. Callers showing newest output call ScrollToBottom().
        scroll = Math.Clamp(scroll, 0, Math.Max(0, lines.Count - 1));
    }

    public void ScrollToBottom() => scroll = 0;

    public void ScrollBy(int delta)
    {
        scroll = Math.Clamp(scroll + delta, 0, Math.Max(0, lines.Count - 1));
    }

    public IReadOnlyList<HistoryLine> Window(int maxLines)
    {
        if (lines.Count == 0 || maxLines <= 0) return [];
        int end = lines.Count - scroll;
        int start = Math.Max(0, end - maxLines);
        return lines.GetRange(start, Math.Max(0, end - start));
    }

    public static string ColorOf(HistoryKind kind) => kind switch
    {
        HistoryKind.Success => "#7dd87d",
        HistoryKind.Warning => "#e0c060",
        HistoryKind.Error => "#ff7070",
        _ => "#e8e8e8",
    };

    public static string Escape(string s) =>
        (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

    public static string RenderHtml(IReadOnlyList<HistoryLine> window) =>
        string.Join("<br/>", window.Select(l =>
            $"<font color=\"{ColorOf(l.Kind)}\">{Escape(l.Text)}</font>"));
}

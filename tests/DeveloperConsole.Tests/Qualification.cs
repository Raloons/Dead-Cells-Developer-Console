using DeveloperConsole;
using System.Diagnostics;

internal static class Qualification
{
    internal static void Print()
    {
        Console.WriteLine("ASSEMBLY REFERENCES");
        foreach (var name in typeof(CatalogIndex).Assembly.GetReferencedAssemblies().OrderBy(n => n.Name))
            Console.WriteLine("  " + name.Name + " " + name.Version);
        var records = Enumerable.Range(0, 1000).Select(n => CatalogItem.FromRow("ExampleItem" + n,
            "Synthetic Equipment " + n, null, 4, false, true, "synthetic performance sample")).ToArray();
        var timer = Stopwatch.StartNew();
        var snapshot = new CatalogSnapshot(records, "synthetic", DateTime.UnixEpoch, 1);
        _ = snapshot.Index;
        timer.Stop();
        Console.WriteLine($"PERFORMANCE synthetic 1000-row index: {timer.Elapsed.TotalMilliseconds:0.00} ms");
        var registry = OverhaulTests.Registry();
        for (int n = 0; n < 100; n++) CompletionEngine.Complete("spawn synthetic", registry, snapshot);
        long before = GC.GetAllocatedBytesForCurrentThread(); timer.Restart();
        for (int n = 0; n < 1000; n++) CompletionEngine.Complete("spawn synthetic", registry, snapshot);
        timer.Stop();
        Console.WriteLine($"PERFORMANCE 1000 completions: {timer.Elapsed.TotalMilliseconds:0.00} ms; {(GC.GetAllocatedBytesForCurrentThread() - before) / 1000} bytes/request");
        Console.WriteLine("These are offline .NET timings, not native rendering or game-frame measurements.");
    }
}

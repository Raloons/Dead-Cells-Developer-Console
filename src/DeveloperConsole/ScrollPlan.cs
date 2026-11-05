namespace DeveloperConsole;

public static class StatNames
{
    public static bool TryGet(string raw, out string stat)
    {
        stat = raw.ToLowerInvariant() switch
        {
            "brutality" or "brut" or "b" or "red" => "brutality",
            "tactics" or "tactic" or "t" or "purple" => "tactics",
            "survival" or "surv" or "s" or "green" => "survival",
            _ => "",
        };
        return stat.Length > 0;
    }
}

public static class ScrollPlan
{
    public static bool TrySet(int effective, int native, int temporary, int target,
        out int delta, out string? error)
    {
        delta = 0; error = null;
        if (target < 1 || target > 999) error = "Target tier must be 1-999.";
        else if (native < 0 || effective < 1 || native > effective - 1)
            error = "Cannot reconcile effective tier with native allocations; inspect player status.";
        else if (temporary != 0)
            error = "Temporary player stat override is active. Clear that override before reconciling native scrolls.";
        else if (target < effective - native)
            error = $"Target is below the base/equipment modifier floor ({effective - native}); no allocations changed.";
        else if (Math.Abs((long)target - effective) > 100)
            error = "Target requires more than 100 allocations in one command; use smaller steps.";
        else delta = target - effective;
        return error == null;
    }
}

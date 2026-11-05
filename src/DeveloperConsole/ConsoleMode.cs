namespace DeveloperConsole;

// Native input placement only. The overlay is a single traditional console.
// The user-facing mode command was removed in DC-M4.6; transplant stays on.
public static class ConsoleMode
{
    public static bool Transplant { get; private set; } = true;
    public static void SetTransplant(bool value) => Transplant = value;
    public static void ResetForTests() => Transplant = true;
}

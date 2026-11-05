namespace DeveloperConsole;

// Proxy properties write Haxe fields directly, bypassing h2d's inline transform
// setters. Explicitly invalidate the cached scene matrix after pooled updates.
public static class SceneTransform
{
    public static void Apply(dynamic node, double x, double y, double scale = 1)
    {
        node.x = x;
        node.y = y;
        node.scaleX = node.scaleY = scale;
        node.posChanged = true;
    }
}

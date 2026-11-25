namespace soft.square.net.Module.DynamicFields.Dialogs;

/// <summary>
/// Node ids for content nodes. Every node an editor of this module returns has one, so translation and design overlays
/// (which key on it) always have something to hold on to; the server rejects a tree that uses one twice.
/// </summary>
public static class NodeIds
{
    /// <summary>A new id: "n" and 12 hex digits of a random GUID (unique within any post; 128 characters at most).</summary>
    public static string New() => "n" + Guid.NewGuid().ToString("n")[..12];

    /// <summary>The id given, trimmed; a new one when there is none.</summary>
    public static string OrNew(string? id) => string.IsNullOrWhiteSpace(id) ? New() : id.Trim();
}

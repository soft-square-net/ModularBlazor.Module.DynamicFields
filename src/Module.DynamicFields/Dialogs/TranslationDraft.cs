using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Client;

namespace soft.square.net.Module.DynamicFields.Dialogs;

/// <summary>
/// What the translate dialog edits: one text per translatable value (lists: one per source item), next to its source,
/// filled from the server's work list and the existing overlay, and turned back into an overlay on save. A value keeps
/// the hash of the source it was translated from, so an untouched stale one stays stale until it is edited or marked
/// as still right.
/// </summary>
public sealed class TranslationDraft
{
    private readonly ContentTranslation? _existing;

    public TranslationDraft(string language, IReadOnlyList<TranslationUnit> units, ContentTranslation? existing, string? onlyNode)
    {
        Language = language;
        _existing = existing;
        Units = units.Where(u => onlyNode is null || u.NodeId == onlyNode).Select(u => new UnitState(u, OldHash(u))).ToList();
    }

    public string Language { get; }
    public IReadOnlyList<UnitState> Units { get; }

    public int Count(TranslationState state) => Units.Count(u => u.State == state);

    /// <summary>
    /// The overlay to keep: the existing one with every unit shown here replaced (or removed when emptied). Values of
    /// nodes the dialog did not show stay as they were; the server prunes what the tree no longer has.
    /// </summary>
    public ContentTranslation ToTranslation()
    {
        var nodes = new Dictionary<string, IDictionary<string, TranslatedValue>>(StringComparer.Ordinal);
        foreach (var (id, values) in _existing?.Nodes ?? new Dictionary<string, IDictionary<string, TranslatedValue>>())
            nodes[id] = new Dictionary<string, TranslatedValue>(values, StringComparer.Ordinal);

        foreach (var unit in Units)
        {
            if (!nodes.TryGetValue(unit.Unit.NodeId, out var values)) nodes[unit.Unit.NodeId] = values = new Dictionary<string, TranslatedValue>(StringComparer.Ordinal);
            if (unit.ToJson() is { } value) values[unit.Unit.Key] = new TranslatedValue { Value = value, SourceHash = unit.Hash };
            else values.Remove(unit.Unit.Key);
        }
        foreach (var empty in nodes.Where(n => n.Value.Count == 0).Select(n => n.Key).ToList()) nodes.Remove(empty);
        return new ContentTranslation { Language = Language, Nodes = nodes };
    }

    /// <summary>The unit an issue's path belongs to (/nodes/img1/caption/value/0 → that unit), or null.</summary>
    public UnitState? Owner(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(p => p.Replace("~1", "/").Replace("~0", "~")).ToArray();
        return parts.Length >= 3 && parts[0] == "nodes" ? Units.FirstOrDefault(u => u.Unit.NodeId == parts[1] && u.Unit.Key == parts[2]) : null;
    }

    private string? OldHash(TranslationUnit u) =>
        _existing?.Nodes is { } nodes && nodes.TryGetValue(u.NodeId, out var values) && values.TryGetValue(u.Key, out var value) ? value.SourceHash : null;

    /// <summary>Languages written right to left (by their primary subtag).</summary>
    public static bool IsRightToLeft(string language) =>
        language.Split('-')[0].ToLowerInvariant() is "ar" or "he" or "fa" or "ur" or "ps" or "sd" or "ug" or "yi" or "dv" or "ckb";

    /// <summary>One translatable value as the inputs hold it.</summary>
    public sealed class UnitState
    {
        private readonly List<string> _original;
        private readonly string? _oldHash;

        public UnitState(TranslationUnit unit, string? oldHash)
        {
            Unit = unit;
            _oldHash = oldHash;
            Sources = unit.Source.ValueKind == JsonValueKind.Array
                ? unit.Source.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.String).Select(i => i.GetString() ?? "").ToList()
                : [unit.Source.ValueKind == JsonValueKind.String ? unit.Source.GetString() ?? "" : ""];
            var translated = unit.Translation is { ValueKind: JsonValueKind.Array } list
                ? list.EnumerateArray().Select(i => i.ValueKind == JsonValueKind.String ? i.GetString() ?? "" : "").ToList()
                : unit.Translation is { ValueKind: JsonValueKind.String } text ? [text.GetString() ?? ""] : [];
            Items = Sources.Select((_, i) => i < translated.Count ? translated[i] : "").ToList();
            _original = [.. Items];
        }

        public TranslationUnit Unit { get; }
        public IReadOnlyList<string> Sources { get; }
        public List<string> Items { get; }
        public bool IsRich => Unit.Kind == PropertyKind.RichText;

        /// <summary>The translator says a stale translation is still right for the changed source.</summary>
        public bool MarkedCurrent { get; set; }

        public bool Changed => !Items.SequenceEqual(_original);
        public bool HasText => Items.Any(i => i.Trim().Length > 0);

        public TranslationState State =>
            !HasText ? TranslationState.Missing
            : Changed || MarkedCurrent || Unit.State == TranslationState.Current ? TranslationState.Current
            : TranslationState.Stale;

        /// <summary>The source hash to store: the current source's once edited or confirmed, else the one it had.</summary>
        public string Hash => State == TranslationState.Current || _oldHash is null ? Unit.SourceHash : _oldHash;

        public JsonElement? ToJson()
        {
            if (!HasText) return null;
            string Clean(string s) => IsRich ? s : s.Trim();
            return Unit.Multiple
                ? JsonSerializer.SerializeToElement(Items.Where(i => i.Trim().Length > 0).Select(Clean).ToArray())
                : JsonSerializer.SerializeToElement(Clean(Items[0]));
        }

        /// <summary>Starts from the source text (to translate in place), for the items still empty.</summary>
        public void CopySource()
        {
            for (var i = 0; i < Items.Count; i++)
                if (Items[i].Trim().Length == 0) Items[i] = Sources[i];
        }
    }
}

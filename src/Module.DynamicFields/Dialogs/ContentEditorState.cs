using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using soft.square.net.ModularBlazor.Abstractions.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Client;

namespace soft.square.net.Module.DynamicFields.Dialogs;

/// <summary>
/// What the edit dialog edits: one text per value (lists: one per item) and one token per style, filled from a node's
/// content and style JSON and turned back into JSON on save. Values travel as typed JSON where they parse; one that does
/// not (a number with letters) is sent as typed, so the server's check reports it at its path. Lookup values are items
/// ({ key, text }, a recursive one with its path) and travel as objects.
/// </summary>
public sealed class ContentEditorState
{
    public ContentEditorState(FieldSnapshot field, JsonElement? content, JsonElement? style)
    {
        Field = field;
        Values = field.Properties.Select(p => new ValueState(p, Read(content, p.Key))).ToList();
        Styles = field.Style.Select(s => new StyleState(s, Read(style, s.Key))).ToList();
    }

    public FieldSnapshot Field { get; }
    public IReadOnlyList<ValueState> Values { get; }
    public IReadOnlyList<StyleState> Styles { get; }

    /// <summary>The content object: every value that has something in it, in the field's order.</summary>
    public JsonObject Content()
    {
        var content = new JsonObject();
        foreach (var value in Values)
            if (value.ToJson() is { } json) content[value.Property.Key] = json;
        return content;
    }

    /// <summary>The style object: every style that is set (an unset one takes the field's default).</summary>
    public JsonObject Style()
    {
        var style = new JsonObject();
        foreach (var s in Styles)
            if (s.ToJson() is { } json) style[s.Property.Key] = json;
        return style;
    }

    /// <summary>
    /// The required values left empty, as (owner path, message): checked before asking the server. With a group, only
    /// that group's values.
    /// </summary>
    public IEnumerable<(string Path, string Message)> MissingRequired(string? group = null) =>
        Values.Where(v => v.Required && !v.HasValue && InGroup(v.Property.Key, group))
            .Select(v => ($"/content/{v.Property.Key}", $"{v.Property.Label} is required."));

    /// <summary>Whether a value is in a root field's value group (no group: every value is).</summary>
    public bool InGroup(string key, string? group)
    {
        if (group is null || Field.Groups is not { Count: > 0 } groups) return true;
        var property = Field.Properties.FirstOrDefault(p => p.Key == key);
        return property is not null && (property.Group ?? groups[0].Key) == group;
    }

    /// <summary>The value or style an issue's path belongs to (/content/topics/1 → /content/topics), or null.</summary>
    public string? OwnerPath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] is not ("content" or "style")) return null;
        var key = parts[1].Replace("~1", "/").Replace("~0", "~");
        var known = parts[0] == "content" ? Values.Any(v => v.Property.Key == key) : Styles.Any(s => s.Property.Key == key);
        return known ? $"/{parts[0]}/{key}" : null;
    }

    /// <summary>
    /// After a lookup value changes: empties the values that depend on it (their parent property), all the way down,
    /// since a city of the old country is not a city of the new one.
    /// </summary>
    public void ClearDependents(ValueState changed)
    {
        foreach (var dependent in Values.Where(v => v.Lookup?.ParentProperty == changed.Property.Key && v != changed))
        {
            if (!dependent.HasValue) continue;
            dependent.Picked = null;
            dependent.Picks = [];
            ClearDependents(dependent);
        }
    }

    /// <summary>The key a dependent lookup is filtered by: its parent property's value, or null while that is empty.</summary>
    public string? ParentKey(ValueState value) =>
        value.Lookup?.ParentProperty is { } parent ? Values.FirstOrDefault(v => v.Property.Key == parent)?.Picked?.Key : null;

    private static JsonElement? Read(JsonElement? obj, string key) =>
        obj is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    /// <summary>One content value as the controls hold it.</summary>
    public sealed class ValueState
    {
        public ValueState(FieldProperty property, JsonElement? value)
        {
            Property = property;
            if (IsLookup)
            {
                if (Multiple)
                {
                    if (value is { ValueKind: JsonValueKind.Array } list) Picks = [.. list.EnumerateArray().Select(ToItem).OfType<LookupItem>()];
                }
                else if (value is { } one && ToItem(one) is { } item)
                {
                    if (Lookup?.Recursive == true)
                        Path = [.. (one.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.Array
                            ? path.EnumerateArray().Select(ToItem).OfType<LookupItem>() : []), item];
                    else Picked = item;
                }
            }
            else if (Multiple)
            {
                if (value is { ValueKind: JsonValueKind.Array } array) Items.AddRange(array.EnumerateArray().Select(ToText));
                if (!IsChoice && Items.Count == 0) Items.Add("");
            }
            else if (property.Kind == PropertyKind.Boolean) Flag = value?.ValueKind == JsonValueKind.True;
            else Text = value is { } v ? ToText(v) : "";
        }

        public FieldProperty Property { get; }
        public bool Multiple => Property.Multiple == true;
        public bool Required => Property.Required == true;
        public bool IsChoice => Property.Kind == PropertyKind.Choice;
        public bool IsLookup => Property.Kind == PropertyKind.Lookup && Property.Lookup is not null;
        public LookupBinding? Lookup => Property.Lookup;

        /// <summary>A single lookup value.</summary>
        public LookupItem? Picked { get; set; }

        /// <summary>A multiple lookup's values.</summary>
        public IReadOnlyList<LookupItem> Picks { get; set; } = [];

        /// <summary>A recursive lookup's value: the path from the root, its last item being the value.</summary>
        public IReadOnlyList<LookupItem> Path { get; set; } = [];

        /// <summary>What a picker shows as selected.</summary>
        public IReadOnlyList<LookupItem> Selection => Multiple ? Picks : Picked is { } one ? [one] : [];

        public string Text { get; set; } = "";
        public bool Flag { get; set; }
        public List<string> Items { get; } = [];

        public bool HasValue => IsLookup
            ? Multiple ? Picks.Count > 0 : Lookup!.Recursive == true ? Path.Count > 0 : Picked is not null
            : Multiple ? Items.Any(i => i.Trim().Length > 0) : Property.Kind == PropertyKind.Boolean || Text.Trim().Length > 0;

        public JsonNode? ToJson()
        {
            if (IsLookup)
            {
                if (Multiple) return Picks.Count == 0 ? null : new JsonArray([.. Picks.Select(Node)]);
                if (Lookup!.Recursive == true)
                {
                    if (Path.Count == 0) return null;
                    var leaf = Node(Path[^1]);
                    if (Path.Count > 1) leaf["path"] = new JsonArray([.. Path.Take(Path.Count - 1).Select(Node)]);
                    return leaf;
                }
                return Picked is null ? null : Node(Picked);
            }
            if (Property.Kind == PropertyKind.Boolean && !Multiple) return Flag;
            if (!Multiple) return Text.Trim().Length == 0 ? null : Item(Text);
            var items = Items.Where(i => i.Trim().Length > 0).Select(Item).ToArray();
            return items.Length == 0 ? null : new JsonArray(items);
        }

        private JsonNode? Item(string text)
        {
            var t = Property.Kind is PropertyKind.RichText ? text : text.Trim();
            return Property.Kind switch
            {
                PropertyKind.Integer when long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
                PropertyKind.Decimal when decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) => d,
                PropertyKind.Boolean when bool.TryParse(t, out var b) => b,
                // datetime-local has no offset: the browser's own applies.
                PropertyKind.DateTime when DateTime.TryParse(t, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var local) =>
                    new DateTimeOffset(local).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture),
                _ => t
            };
        }

        private static JsonObject Node(LookupItem item) => new() { ["key"] = item.Key, ["text"] = item.Text };

        private static LookupItem? ToItem(JsonElement v) =>
            v.ValueKind == JsonValueKind.Object && v.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
                ? new LookupItem(key.GetString()!, v.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String ? text.GetString()! : key.GetString()!)
                : null;

        private string ToText(JsonElement v) => v.ValueKind switch
        {
            JsonValueKind.String when Property.Kind == PropertyKind.DateTime
                && DateTimeOffset.TryParse(v.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) =>
                at.ToLocalTime().ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture),
            JsonValueKind.String => v.GetString() ?? "",
            JsonValueKind.Number => v.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => v.GetRawText()
        };
    }

    /// <summary>One style as the selects hold it: a token for every width, or one per breakpoint.</summary>
    public sealed class StyleState
    {
        public static IReadOnlyList<string> Breakpoints { get; } = ["sm", "md", "lg", "xl"];

        public StyleState(StyleProperty property, JsonElement? value)
        {
            Property = property;
            if (value is { ValueKind: JsonValueKind.String } token) Base = token.GetString() ?? "";
            else if (value is { ValueKind: JsonValueKind.Object } points)
            {
                foreach (var point in points.EnumerateObject())
                {
                    var text = point.Value.ValueKind == JsonValueKind.String ? point.Value.GetString() ?? "" : "";
                    if (point.Name == "base") Base = text;
                    else PerBreakpoint[point.Name] = text;
                }
                ByBreakpoint = PerBreakpoint.Count > 0;
            }
        }

        public StyleProperty Property { get; }
        public bool Responsive => Property.Responsive == true;
        public string Base { get; set; } = "";
        public bool ByBreakpoint { get; set; }
        public Dictionary<string, string> PerBreakpoint { get; } = [];

        public JsonNode? ToJson()
        {
            var points = Responsive && ByBreakpoint
                ? PerBreakpoint.Where(p => p.Value.Length > 0).ToDictionary(p => p.Key, p => p.Value)
                : [];
            if (points.Count == 0) return Base.Length == 0 ? null : Base;

            var json = new JsonObject();
            if (Base.Length > 0) json["base"] = Base;
            foreach (var point in Breakpoints)
                if (points.TryGetValue(point, out var token)) json[point] = token;
            return json;
        }
    }
}

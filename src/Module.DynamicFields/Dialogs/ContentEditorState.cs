using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using soft.square.net.WebApi.Modules.DynamicFields.Client;

namespace soft.square.net.Module.DynamicFields.Dialogs;

/// <summary>
/// What the edit dialog edits: one text per value (lists: one per item) and one token per style, filled from a node's
/// content and style JSON and turned back into JSON on save. Values travel as typed JSON where they parse; one that does
/// not (a number with letters) is sent as typed, so the server's check reports it at its path.
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

    /// <summary>The required values left empty, as (owner path, message): checked before asking the server.</summary>
    public IEnumerable<(string Path, string Message)> MissingRequired() =>
        Values.Where(v => v.Required && !v.HasValue).Select(v => ($"/content/{v.Property.Key}", $"{v.Property.Label} is required."));

    /// <summary>The value or style an issue's path belongs to (/content/topics/1 → /content/topics), or null.</summary>
    public string? OwnerPath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] is not ("content" or "style")) return null;
        var key = parts[1].Replace("~1", "/").Replace("~0", "~");
        var known = parts[0] == "content" ? Values.Any(v => v.Property.Key == key) : Styles.Any(s => s.Property.Key == key);
        return known ? $"/{parts[0]}/{key}" : null;
    }

    private static JsonElement? Read(JsonElement? obj, string key) =>
        obj is { ValueKind: JsonValueKind.Object } o && o.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null ? v : null;

    /// <summary>One content value as the controls hold it.</summary>
    public sealed class ValueState
    {
        public ValueState(FieldProperty property, JsonElement? value)
        {
            Property = property;
            if (Multiple)
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

        public string Text { get; set; } = "";
        public bool Flag { get; set; }
        public List<string> Items { get; } = [];

        public bool HasValue => Multiple ? Items.Any(i => i.Trim().Length > 0) : Property.Kind == PropertyKind.Boolean || Text.Trim().Length > 0;

        public JsonNode? ToJson()
        {
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

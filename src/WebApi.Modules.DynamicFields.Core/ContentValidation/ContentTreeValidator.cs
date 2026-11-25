using System.Globalization;
using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;

/// <summary>
/// One node of a post's content tree, as the consumer (Contents) stores it: the field version it is made with, its
/// values, its style and its child nodes. The root is the post itself and has no parent.
/// </summary>
public sealed record ContentNode(
    string Field,
    int FieldVersion,
    JsonElement Content,
    JsonElement Style,
    IReadOnlyList<ContentNode>? Children = null,
    string? NodeId = null);

/// <summary>One problem, at a JSON Pointer into the submitted tree (<c>/children/1/content/caption</c>).</summary>
public sealed record ContentIssue(string Path, string Message);

/// <summary>
/// Checks a content tree against published field versions: the root can be a post, each child is allowed by its
/// parent (pinned version, counts), each node's content has exactly the field's values with the right kinds and limits,
/// and its style uses only the field's tokens. Reports every problem it finds, not just the first.
/// </summary>
public static class ContentTreeValidator
{
    public const int MaxDepth = 12;
    public const int MaxNodes = 500;
    public const int MaxIdLength = 128;

    /// <summary>The breakpoints a responsive style value may name; "base" applies to every width.</summary>
    public static IReadOnlyList<string> Breakpoints { get; } = ["base", "sm", "md", "lg", "xl"];

    public static IReadOnlyList<ContentIssue> Validate(ContentNode root, Func<string, int, FieldSnapshot?> resolve)
    {
        ArgumentNullException.ThrowIfNull(root);
        var issues = new List<ContentIssue>();
        var nodes = 0;
        var ids = new Dictionary<string, string>(StringComparer.Ordinal);
        Node(root, parent: null, path: "", depth: 1, resolve, issues, ids, ref nodes);
        return issues;
    }

    /// <summary>
    /// Checks one node's values and style against its field, without its place in a tree (what an editor dialog checks
    /// before it closes). Paths start at <c>/content</c> and <c>/style</c>.
    /// </summary>
    public static IReadOnlyList<ContentIssue> ValidateValues(FieldSnapshot field, JsonElement content, JsonElement style)
    {
        ArgumentNullException.ThrowIfNull(field);
        var issues = new List<ContentIssue>();
        Content(field, content, "/content", issues);
        Style(field, style, "/style", issues);
        return issues;
    }

    private static void Node(ContentNode node, FieldSnapshot? parent, string path, int depth,
        Func<string, int, FieldSnapshot?> resolve, List<ContentIssue> issues, Dictionary<string, string> ids, ref int nodes)
    {
        if (++nodes > MaxNodes)
        {
            if (nodes == MaxNodes + 1) issues.Add(new(path, $"A post has at most {MaxNodes} nodes."));
            return;
        }
        if (depth > MaxDepth)
        {
            issues.Add(new(path, $"Content nests at most {MaxDepth} levels."));
            return;
        }

        NodeIdOf(node, path, ids, issues);

        var field = resolve(node.Field ?? "", node.FieldVersion);
        if (field is null)
        {
            issues.Add(new($"{path}/field", $"Field '{node.Field}' version {node.FieldVersion} is not published."));
            return;
        }

        if (parent is null && !field.CanBeRoot)
            issues.Add(new($"{path}/field", $"Field '{field.Key}' cannot be a post's root."));

        if (parent is not null)
        {
            var rule = parent.Children.FirstOrDefault(c => c.FieldKey == field.Key);
            if (rule is null)
                issues.Add(new($"{path}/field", $"'{parent.Key}' cannot contain '{field.Key}'."));
            else if (rule.Version != field.Version)
                issues.Add(new($"{path}/fieldVersion", $"'{parent.Key}' version {parent.Version} contains '{field.Key}' version {rule.Version}, not {field.Version}."));
        }

        Content(field, node.Content, $"{path}/content", issues);
        Style(field, node.Style, $"{path}/style", issues);

        var children = node.Children ?? [];
        foreach (var rule in field.Children)
        {
            var count = children.Count(c => c?.Field == rule.FieldKey);
            if (count < rule.Min) issues.Add(new($"{path}/children", $"'{field.Key}' needs at least {rule.Min} '{rule.FieldKey}' (has {count})."));
            if (count > rule.Max) issues.Add(new($"{path}/children", $"'{field.Key}' holds at most {rule.Max} '{rule.FieldKey}' (has {count})."));
        }
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i] is null) { issues.Add(new($"{path}/children/{i}", "A child node is missing.")); continue; }
            Node(children[i], field, $"{path}/children/{i}", depth + 1, resolve, issues, ids, ref nodes);
        }
    }

    /// <summary>
    /// A node id, when there is one: 1 to <see cref="MaxIdLength"/> characters, not the overlay key of the root, and used
    /// once in the tree. Translation and design overlays key on it, so two nodes with one id would share them.
    /// </summary>
    private static void NodeIdOf(ContentNode node, string path, Dictionary<string, string> ids, List<ContentIssue> issues)
    {
        if (node.NodeId is null) return;
        var at = $"{path}/nodeId";
        var id = node.NodeId;
        if (id.Trim().Length == 0 || id.Length > MaxIdLength || id != id.Trim())
            issues.Add(new(at, $"A node id is 1 to {MaxIdLength} characters, without spaces around it."));
        else if (id == Translation.ContentTranslator.RootId)
            issues.Add(new(at, $"'{id}' is the root's key in overlays, not a node id."));
        else if (!ids.TryAdd(id, path))
            issues.Add(new(at, $"Node id '{id}' is used twice (also at {(ids[id].Length == 0 ? "the root" : $"'{ids[id]}'")})."));
    }

    private static void Content(FieldSnapshot field, JsonElement content, string path, List<ContentIssue> issues)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new(path, "Content is a JSON object."));
            return;
        }
        var known = field.Properties.ToDictionary(p => p.Key, StringComparer.Ordinal);
        foreach (var value in content.EnumerateObject())
            if (!known.ContainsKey(value.Name))
                issues.Add(new($"{path}/{Escape(value.Name)}", $"'{field.Key}' has no value '{value.Name}'."));

        foreach (var property in field.Properties)
        {
            var at = $"{path}/{Escape(property.Key)}";
            if (!content.TryGetProperty(property.Key, out var value) || value.ValueKind == JsonValueKind.Null)
            {
                if (property.Required) issues.Add(new(at, $"{property.Label} is required."));
                continue;
            }
            if (property.Multiple)
            {
                if (value.ValueKind != JsonValueKind.Array) { issues.Add(new(at, $"{property.Label} is a list.")); continue; }
                if (property.Required && value.GetArrayLength() == 0) issues.Add(new(at, $"{property.Label} needs at least one value."));
                if (property.Lookup?.MaxSelections is { } most && value.GetArrayLength() > most)
                    issues.Add(new(at, $"{property.Label} has at most {most} values."));
                var i = 0;
                foreach (var item in value.EnumerateArray()) Value(property, item, $"{at}/{i++}", issues);
            }
            else Value(property, value, at, issues);
        }
    }

    private static void Value(FieldProperty p, JsonElement value, string at, List<ContentIssue> issues)
    {
        switch (p.Kind)
        {
            case PropertyKind.Text or PropertyKind.RichText:
                if (!String(value, out var text)) { issues.Add(new(at, $"{p.Label} is text.")); return; }
                if (p.Required && text.Trim().Length == 0) issues.Add(new(at, $"{p.Label} is required."));
                if (text.Length < p.MinLength) issues.Add(new(at, $"{p.Label} has at least {p.MinLength} characters."));
                if (text.Length > p.MaxLength) issues.Add(new(at, $"{p.Label} has at most {p.MaxLength} characters."));
                return;
            case PropertyKind.Integer or PropertyKind.Decimal:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)
                    || (p.Kind == PropertyKind.Integer && number != decimal.Truncate(number)))
                {
                    issues.Add(new(at, p.Kind == PropertyKind.Integer ? $"{p.Label} is a whole number." : $"{p.Label} is a number."));
                    return;
                }
                if (number < p.Minimum) issues.Add(new(at, $"{p.Label} is at least {p.Minimum}."));
                if (number > p.Maximum) issues.Add(new(at, $"{p.Label} is at most {p.Maximum}."));
                return;
            case PropertyKind.Boolean:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) issues.Add(new(at, $"{p.Label} is true or false."));
                return;
            case PropertyKind.Date:
                if (!String(value, out var date) || !DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    issues.Add(new(at, $"{p.Label} is a date, yyyy-MM-dd."));
                return;
            case PropertyKind.DateTime:
                if (!String(value, out var moment) || !HasOffset(moment)
                    || !DateTimeOffset.TryParse(moment, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    issues.Add(new(at, $"{p.Label} is a date and time with its offset (ISO 8601, e.g. 2026-11-21T09:30:00+02:00)."));
                return;
            case PropertyKind.Url:
                if (!String(value, out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
                    issues.Add(new(at, $"{p.Label} is an http or https address."));
                return;
            case PropertyKind.Choice:
                if (!String(value, out var choice) || p.Options?.Contains(choice) != true)
                    issues.Add(new(at, $"{p.Label} is one of: {string.Join(", ", p.Options ?? [])}."));
                return;
            case PropertyKind.Asset or PropertyKind.Keyword or PropertyKind.Term:
                if (!String(value, out var id) || id.Trim().Length is 0 or > MaxIdLength)
                    issues.Add(new(at, $"{p.Label} is an id of 1 to {MaxIdLength} characters."));
                return;
            case PropertyKind.Lookup:
                LookupValue(p, value, at, issues);
                return;
        }
    }

    public const int MaxLookupTextLength = 500;

    /// <summary>
    /// The shape of a lookup value, <c>{ key, text, path? }</c>. Whether the key exists in the lookup, and its parent and
    /// path, is checked against the owner (ValidateContent's second step), not here.
    /// </summary>
    private static void LookupValue(FieldProperty p, JsonElement value, string at, List<ContentIssue> issues)
    {
        var recursive = p.Lookup?.Recursive == true;
        if (value.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new(at, $"{p.Label} is an item of a list: {{ \"key\": …, \"text\": … }}."));
            return;
        }
        Item(value, at, p.Label, issues);
        foreach (var member in value.EnumerateObject())
        {
            if (member.Name is "key" or "text" || (member.Name == "path" && recursive)) continue;
            issues.Add(new($"{at}/{Escape(member.Name)}", $"{p.Label} has no '{member.Name}'."));
        }
        if (!recursive || !value.TryGetProperty("path", out var path) || path.ValueKind == JsonValueKind.Null) return;

        var maxDepth = p.Lookup!.MaxDepth;
        if (path.ValueKind != JsonValueKind.Array || path.GetArrayLength() >= maxDepth)
        {
            issues.Add(new($"{at}/path", $"{p.Label}'s path is a list of at most {maxDepth - 1} items above it."));
            return;
        }
        var i = 0;
        foreach (var step in path.EnumerateArray())
        {
            var stepAt = $"{at}/path/{i++}";
            if (step.ValueKind != JsonValueKind.Object) { issues.Add(new(stepAt, $"{p.Label}'s path holds items {{ key, text }}.")); continue; }
            Item(step, stepAt, p.Label, issues);
        }
    }

    private static void Item(JsonElement item, string at, string label, List<ContentIssue> issues)
    {
        if (!item.TryGetProperty("key", out var key) || !String(key, out var k) || k.Trim().Length is 0 or > MaxIdLength)
            issues.Add(new($"{at}/key", $"{label}: the key is text of 1 to {MaxIdLength} characters."));
        if (item.TryGetProperty("text", out var text) && text.ValueKind != JsonValueKind.Null
            && (!String(text, out var t) || t.Length > MaxLookupTextLength))
            issues.Add(new($"{at}/text", $"{label}: the text is at most {MaxLookupTextLength} characters."));
    }

    private static void Style(FieldSnapshot field, JsonElement style, string path, List<ContentIssue> issues)
    {
        if (style.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return; // no style: every default applies
        if (style.ValueKind != JsonValueKind.Object)
        {
            issues.Add(new(path, "Style is a JSON object."));
            return;
        }
        var known = field.Style.ToDictionary(s => s.Key, StringComparer.Ordinal);
        foreach (var entry in style.EnumerateObject())
        {
            var at = $"{path}/{Escape(entry.Name)}";
            if (!known.TryGetValue(entry.Name, out var s)) { issues.Add(new(at, $"'{field.Key}' has no style '{entry.Name}'.")); continue; }

            if (entry.Value.ValueKind == JsonValueKind.Object && s.Responsive)
            {
                foreach (var point in entry.Value.EnumerateObject())
                {
                    if (!Breakpoints.Contains(point.Name)) issues.Add(new($"{at}/{Escape(point.Name)}", $"Breakpoints are {string.Join(", ", Breakpoints)}."));
                    else Token(s, point.Value, $"{at}/{point.Name}", issues);
                }
            }
            else Token(s, entry.Value, at, issues);
        }
    }

    private static void Token(StyleProperty s, JsonElement value, string at, List<ContentIssue> issues)
    {
        if (!String(value, out var token) || !s.Values.Contains(token))
            issues.Add(new(at, $"{s.Label} is one of: {string.Join(", ", s.Values)}{(s.Responsive ? ", or one per breakpoint" : "")}."));
    }

    private static bool String(JsonElement value, out string text)
    {
        text = value.ValueKind == JsonValueKind.String ? value.GetString()! : "";
        return value.ValueKind == JsonValueKind.String;
    }

    /// <summary>A time zone offset or Z after the time, so "when" means one instant everywhere.</summary>
    private static bool HasOffset(string value) =>
        value.Length > 10 && (value.EndsWith('Z') || value.EndsWith('z') || value.LastIndexOfAny(['+', '-']) > 10);

    /// <summary>A key as a JSON Pointer token (RFC 6901).</summary>
    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
}

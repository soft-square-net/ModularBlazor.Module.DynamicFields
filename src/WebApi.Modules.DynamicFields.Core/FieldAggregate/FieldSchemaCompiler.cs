using System.Text.Json.Nodes;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>How a compiled node schema describes the node's children.</summary>
public enum ChildSchemas
{
    /// <summary>Each child is checked against its pinned version through <c>$ref</c>: one schema checks the whole tree.</summary>
    Pinned,

    /// <summary>
    /// Only each child's field and version are checked (allowed, pinned), not its own values: for a validator that walks
    /// the tree and checks every node against its own version (DynamicFields' schema engine).
    /// </summary>
    Shallow
}

/// <summary>
/// Compiles a published field version to one schema in the DynamicFields dialect (<see cref="FieldDialect"/>): the
/// schema of a content node made with it, <c>{ nodeId, field, fieldVersion, content, style, children }</c> as Content
/// stores it. Standard keywords do as much of the checking as they can: kinds and limits, the field's own style tokens,
/// which children are allowed (a <c>$ref</c> to each pinned version, <see cref="FieldDialect.VersionId"/>) and how many
/// (<c>contains</c> with <c>minContains</c> / <c>maxContains</c>). The dialect's keywords add what they cannot: what a
/// value refers to, the files it takes, and on a root field the tree's limits; and the schema.org, style, editor and
/// translation annotations.
/// <para>
/// Every compiled schema names its <c>$schema</c>, children's included: a document reached through <c>$ref</c> does not
/// inherit it. No keyword of the dialect applies subschemas or changes a standard keyword. The shapes are chosen so a
/// validator reports one error where the problem is: a value that may be left out also takes null through its
/// <c>type</c> (not an <c>anyOf</c>), and a responsive style is a token or, <c>if</c> an object, one per breakpoint.
/// </para>
/// </summary>
public static class FieldSchemaCompiler
{
    public static JsonObject Compile(FieldSnapshot field, ChildSchemas children = ChildSchemas.Pinned)
    {
        ArgumentNullException.ThrowIfNull(field);
        var schema = new JsonObject
        {
            ["$schema"] = FieldDialect.Uri,
            ["$id"] = FieldDialect.VersionId(field.Key, field.Version),
            ["title"] = field.Title
        };
        if (field.Description is not null) schema["description"] = field.Description;
        schema["schemaOrgType"] = field.SchemaType;
        if (field.CanBeRoot)
        {
            schema["canBeRoot"] = true;
            if (field.UrlPrefix is not null) schema["urlPrefix"] = field.UrlPrefix;
            schema["uniqueNodeIds"] = true;
            schema["maxDepth"] = ContentTreeValidator.MaxDepth;
            schema["maxNodes"] = ContentTreeValidator.MaxNodes;
        }
        if (field.EditorDialog is not null) schema["editorDialog"] = field.EditorDialog;
        if (field.Groups is { Count: > 0 } groups)
            schema["groups"] = new JsonArray([.. groups.Select(g =>
            {
                var group = new JsonObject { ["key"] = g.Key, ["title"] = g.Label };
                if (g.Description is not null) group["description"] = g.Description;
                return (JsonNode)group;
            })]);

        schema["type"] = "object";
        schema["properties"] = new JsonObject
        {
            ["nodeId"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = ContentTreeValidator.MaxIdLength, ["pattern"] = @"^\S(.*\S)?$" },
            ["field"] = new JsonObject { ["const"] = field.Key },
            ["fieldVersion"] = new JsonObject { ["const"] = field.Version },
            ["content"] = Content(field),
            ["style"] = Style(field),
            ["children"] = children == ChildSchemas.Shallow ? ShallowChildren(field) : Children(field)
        };
        schema["required"] = new JsonArray("field", "fieldVersion", "content");
        schema["additionalProperties"] = false;
        return schema;
    }

    /// <summary>The child rules that limit a count, in the order of the children schema's <c>allOf</c>.</summary>
    public static IReadOnlyList<PinnedChild> CountedChildren(FieldSnapshot field) =>
        [.. field.Children.Where(c => c.Min > 0 || c.Max is not null)];

    private static JsonObject Content(FieldSnapshot field)
    {
        var properties = new JsonObject();
        foreach (var p in field.Properties)
        {
            var value = Value(p);
            var schema = p.Multiple ? new JsonObject { ["type"] = "array", ["items"] = value } : value;
            if (p.Multiple && p.Required) schema["minItems"] = 1;
            if (p.Multiple && p.Lookup?.MaxSelections is { } most) schema["maxItems"] = most;
            // A value that is not required may also be sent as null (the same as leaving it out).
            if (!p.Required) OrNull(schema);
            schema["title"] = p.Label;
            if (p.HelpText is not null) schema["description"] = p.HelpText;
            if (p.SchemaProperty is not null) schema["schemaOrgProperty"] = p.SchemaProperty;
            if (p.Group is not null) schema["group"] = p.Group;
            if (p.Kind is PropertyKind.Text or PropertyKind.RichText) schema["translatable"] = p.Translatable ?? true;
            properties[p.Key] = schema;
        }

        var content = new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
        var required = field.Properties.Where(p => p.Required).Select(p => (JsonNode)p.Key).ToArray();
        if (required.Length > 0) content["required"] = new JsonArray(required);
        return content;
    }

    /// <summary>Also takes null: through the schema's <c>type</c>, or its <c>enum</c> when it has no type (a choice).</summary>
    private static void OrNull(JsonObject schema)
    {
        if (schema["type"] is JsonValue type) schema["type"] = new JsonArray(type.GetValue<string>(), "null");
        else if (schema["enum"] is JsonArray values) values.Add(null);
    }

    private static JsonObject Value(FieldProperty p)
    {
        var schema = p.Kind switch
        {
            PropertyKind.Integer => new JsonObject { ["type"] = "integer" },
            PropertyKind.Decimal => new JsonObject { ["type"] = "number" },
            PropertyKind.Boolean => new JsonObject { ["type"] = "boolean" },
            PropertyKind.Date => new JsonObject { ["type"] = "string", ["format"] = "date" },
            PropertyKind.DateTime => new JsonObject { ["type"] = "string", ["format"] = "date-time" },
            PropertyKind.Url => new JsonObject { ["type"] = "string", ["format"] = "uri", ["pattern"] = "^https?://" },
            PropertyKind.Choice => new JsonObject { ["enum"] = new JsonArray([.. (p.Options ?? []).Select(o => (JsonNode)o)]) },
            PropertyKind.Asset or PropertyKind.Keyword or PropertyKind.Term => new JsonObject
            {
                ["type"] = "string", ["minLength"] = 1, ["maxLength"] = ContentTreeValidator.MaxIdLength, ["pattern"] = @"\S",
                ["refKind"] = p.Kind.ToString().ToLowerInvariant()
            },
            PropertyKind.RichText => new JsonObject { ["type"] = "string", ["contentMediaType"] = "text/html" },
            PropertyKind.Lookup => Lookup(p.Lookup!),
            _ => new JsonObject { ["type"] = "string" }
        };
        // Required text has something besides spaces, as the editor and Content require.
        if (p.Required && p.Kind is PropertyKind.Text or PropertyKind.RichText) schema["pattern"] = @"\S";
        if (p.MinLength is { } min) schema["minLength"] = min;
        if (p.MaxLength is { } max) schema["maxLength"] = max;
        if (p.Minimum is { } low) schema["minimum"] = low;
        if (p.Maximum is { } high) schema["maximum"] = high;
        if (p.Accept is { Count: > 0 } accept) schema["accept"] = new JsonArray([.. accept.Select(a => (JsonNode)a)]);
        return schema;
    }

    /// <summary>
    /// <c>{ key, text?, path? }</c>; <c>lookup</c> names the lookup, <c>refKind</c> says its owner checks the key. The
    /// items of a path above the value name their key and text; anything else on them is ignored.
    /// </summary>
    private static JsonObject Lookup(LookupBinding b)
    {
        JsonObject Item() => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["key"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = ContentTreeValidator.MaxIdLength, ["pattern"] = @"\S" },
                ["text"] = new JsonObject { ["type"] = new JsonArray("string", "null"), ["maxLength"] = ContentTreeValidator.MaxLookupTextLength }
            },
            ["required"] = new JsonArray("key")
        };
        var schema = Item();
        if (b.Recursive)
            ((JsonObject)schema["properties"]!)["path"] = new JsonObject { ["type"] = new JsonArray("array", "null"), ["items"] = Item(), ["maxItems"] = b.MaxDepth - 1 };
        schema["additionalProperties"] = false;
        schema["refKind"] = "lookup";
        schema["lookup"] = b.Key;
        return schema;
    }

    private static JsonObject Style(FieldSnapshot field)
    {
        var properties = new JsonObject();
        foreach (var s in field.Style)
        {
            JsonObject Token() => new() { ["enum"] = new JsonArray([.. s.Values.Select(v => (JsonNode)v)]) };
            var schema = s.Responsive
                ? new JsonObject
                {
                    ["if"] = new JsonObject { ["type"] = "object" },
                    ["then"] = new JsonObject
                    {
                        ["properties"] = new JsonObject(ContentTreeValidator.Breakpoints.Select(b => KeyValuePair.Create(b, (JsonNode?)Token()))),
                        ["additionalProperties"] = false
                    },
                    ["else"] = Token(),
                    ["responsive"] = true,
                    ["breakpoints"] = new JsonArray([.. ContentTreeValidator.Breakpoints.Select(b => (JsonNode)b)])
                }
                : Token();
            schema["title"] = s.Label;
            schema["styleToken"] = s.Key;
            if (s.Default is not null) schema["default"] = s.Default;
            properties[s.Key] = schema;
        }
        // No style (null): every default applies.
        return new JsonObject { ["type"] = new JsonArray("object", "null"), ["properties"] = properties, ["additionalProperties"] = false };
    }

    /// <summary>
    /// The child nodes: each one a pinned version of an allowed field (<c>items</c>: <c>anyOf</c> of <c>$ref</c>s), and
    /// per field how many (<see cref="Counts"/>). None allowed: none.
    /// </summary>
    private static JsonObject Children(FieldSnapshot field)
    {
        if (field.Children.Count == 0) return new JsonObject { ["type"] = "array", ["maxItems"] = 0 };
        var children = new JsonObject
        {
            ["type"] = "array",
            ["items"] = new JsonObject
            {
                ["anyOf"] = new JsonArray([.. field.Children.Select(c => (JsonNode)new JsonObject { ["$ref"] = FieldDialect.VersionId(c.FieldKey, c.Version) })])
            }
        };
        Counts(field, children);
        return children;
    }

    /// <summary>
    /// The child nodes, by field and version only: a node of an allowed field (<c>field</c>'s <c>enum</c>; none allowed:
    /// <c>false</c>), at the pinned version (<c>if</c> that field, <c>then</c> that <c>fieldVersion</c>); and the counts.
    /// </summary>
    private static JsonObject ShallowChildren(FieldSnapshot field)
    {
        var item = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["field"] = field.Children.Count == 0 ? JsonValue.Create(false) : new JsonObject { ["enum"] = new JsonArray([.. field.Children.Select(c => (JsonNode)c.FieldKey)]) }
            }
        };
        if (field.Children.Count > 0)
            item["allOf"] = new JsonArray([.. field.Children.Select(c => (JsonNode)new JsonObject
            {
                ["if"] = IsField(c.FieldKey),
                ["then"] = new JsonObject { ["properties"] = new JsonObject { ["fieldVersion"] = new JsonObject { ["const"] = c.Version } } }
            })]);
        var children = new JsonObject { ["type"] = "array", ["items"] = item };
        Counts(field, children);
        return children;
    }

    /// <summary>Per counted field, how many: <c>contains</c> nodes of it, with <c>minContains</c> / <c>maxContains</c>.</summary>
    private static void Counts(FieldSnapshot field, JsonObject children)
    {
        var counts = CountedChildren(field).Select(c =>
        {
            var rule = new JsonObject { ["contains"] = IsField(c.FieldKey), ["minContains"] = c.Min };
            if (c.Max is { } max) rule["maxContains"] = max;
            return (JsonNode)rule;
        }).ToArray();
        if (counts.Length > 0) children["allOf"] = new JsonArray(counts);
    }

    /// <summary>A node made with that field (an object: a missing child is no node of any field).</summary>
    private static JsonObject IsField(string key) => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject { ["field"] = new JsonObject { ["const"] = key } },
        ["required"] = new JsonArray("field")
    };
}

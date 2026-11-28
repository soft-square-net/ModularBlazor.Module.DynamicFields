using System.Text.Json.Nodes;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// Compiles a published field version to one schema in the DynamicFields dialect (<see cref="FieldDialect"/>): the
/// schema of a content node made with it, <c>{ nodeId, field, fieldVersion, content, style, children }</c> as Content
/// stores it. Standard keywords do as much of the checking as they can: kinds and limits, the field's own style tokens,
/// which children are allowed (a <c>$ref</c> to each pinned version, <see cref="FieldDialect.VersionId"/>) and how many
/// (<c>contains</c> with <c>minContains</c> / <c>maxContains</c>). The dialect's keywords add what they cannot: what a
/// value refers to, the files it takes, and on a root field the tree's limits; and the schema.org, style, editor and
/// translation annotations.
/// <para>
/// It describes content; it does not validate it yet (that stays <see cref="ContentTreeValidator"/>). Every compiled
/// schema names its <c>$schema</c>, children's included: a document reached through <c>$ref</c> does not inherit it.
/// No keyword of the dialect applies subschemas or changes a standard keyword.
/// </para>
/// </summary>
public static class FieldSchemaCompiler
{
    public static JsonObject Compile(FieldSnapshot field)
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
            ["children"] = Children(field)
        };
        schema["required"] = new JsonArray("field", "fieldVersion", "content");
        schema["additionalProperties"] = false;
        return schema;
    }

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
            if (!p.Required) schema = new JsonObject { ["anyOf"] = new JsonArray(new JsonObject { ["type"] = "null" }, schema) };
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
                ["type"] = "string", ["minLength"] = 1, ["maxLength"] = ContentTreeValidator.MaxIdLength,
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

    /// <summary><c>{ key, text, path? }</c>; <c>lookup</c> names the lookup, <c>refKind</c> says its owner checks the key.</summary>
    private static JsonObject Lookup(LookupBinding b)
    {
        JsonObject Item() => new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["key"] = new JsonObject { ["type"] = "string", ["minLength"] = 1, ["maxLength"] = ContentTreeValidator.MaxIdLength },
                ["text"] = new JsonObject { ["type"] = "string", ["maxLength"] = ContentTreeValidator.MaxLookupTextLength }
            },
            ["required"] = new JsonArray("key"),
            ["additionalProperties"] = false
        };
        var schema = Item();
        if (b.Recursive)
            ((JsonObject)schema["properties"]!)["path"] = new JsonObject { ["type"] = "array", ["items"] = Item(), ["maxItems"] = b.MaxDepth - 1 };
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
                    ["oneOf"] = new JsonArray(
                        Token(),
                        new JsonObject
                        {
                            ["type"] = "object",
                            ["properties"] = new JsonObject(ContentTreeValidator.Breakpoints.Select(b => KeyValuePair.Create(b, (JsonNode?)Token()))),
                            ["additionalProperties"] = false
                        }),
                    ["responsive"] = true,
                    ["breakpoints"] = new JsonArray([.. ContentTreeValidator.Breakpoints.Select(b => (JsonNode)b)])
                }
                : Token();
            schema["title"] = s.Label;
            schema["styleToken"] = s.Key;
            if (s.Default is not null) schema["default"] = s.Default;
            properties[s.Key] = schema;
        }
        return new JsonObject { ["type"] = "object", ["properties"] = properties, ["additionalProperties"] = false };
    }

    /// <summary>
    /// The child nodes: each one a pinned version of an allowed field (<c>items</c>: <c>anyOf</c> of <c>$ref</c>s), and
    /// per field how many (<c>contains</c> on its key, with <c>minContains</c> / <c>maxContains</c>). None allowed: none.
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
        var counts = field.Children.Where(c => c.Min > 0 || c.Max is not null).Select(c =>
        {
            var rule = new JsonObject
            {
                ["contains"] = new JsonObject
                {
                    ["properties"] = new JsonObject { ["field"] = new JsonObject { ["const"] = c.FieldKey } },
                    ["required"] = new JsonArray("field")
                },
                ["minContains"] = c.Min
            };
            if (c.Max is { } max) rule["maxContains"] = max;
            return (JsonNode)rule;
        }).ToArray();
        if (counts.Length > 0) children["allOf"] = new JsonArray(counts);
        return children;
    }
}

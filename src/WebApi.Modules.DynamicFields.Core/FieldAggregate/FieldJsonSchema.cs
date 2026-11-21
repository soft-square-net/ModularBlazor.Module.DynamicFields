using System.Text.Json.Nodes;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// A published field as standard JSON Schema (draft 2020-12), for tools and other platforms. The module validates with
/// <see cref="ContentValidation.ContentTreeValidator"/>, which applies the same rules plus the parent/child ones. The
/// schema.org mapping travels as <c>x-schemaOrg</c> annotations, which JSON Schema validators ignore.
/// </summary>
public static class FieldJsonSchema
{
    public const string Draft = "https://json-schema.org/draft/2020-12/schema";

    public static JsonObject Content(FieldSnapshot field)
    {
        var properties = new JsonObject();
        foreach (var p in field.Properties)
        {
            var value = Value(p);
            var schema = p.Multiple ? new JsonObject { ["type"] = "array", ["items"] = value } : value;
            if (p.Multiple && p.Required) schema["minItems"] = 1;
            schema["title"] = p.Label;
            if (p.HelpText is not null) schema["description"] = p.HelpText;
            if (p.SchemaProperty is not null) schema["x-schemaOrg"] = p.SchemaProperty;
            properties[p.Key] = schema;
        }

        var root = new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = $"urn:dynamicfields:{field.Key}:{field.Version}:content",
            ["title"] = field.Title,
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false,
            ["x-schemaOrg"] = field.SchemaType
        };
        if (field.Description is not null) root["description"] = field.Description;
        var required = field.Properties.Where(p => p.Required).Select(p => (JsonNode)p.Key).ToArray();
        if (required.Length > 0) root["required"] = new JsonArray(required);
        return root;
    }

    public static JsonObject Style(FieldSnapshot field)
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
                            ["properties"] = new JsonObject(ContentValidation.ContentTreeValidator.Breakpoints
                                .Select(b => KeyValuePair.Create(b, (JsonNode?)Token()))),
                            ["additionalProperties"] = false
                        })
                }
                : Token();
            schema["title"] = s.Label;
            if (s.Default is not null) schema["default"] = s.Default;
            properties[s.Key] = schema;
        }

        return new JsonObject
        {
            ["$schema"] = Draft,
            ["$id"] = $"urn:dynamicfields:{field.Key}:{field.Version}:style",
            ["title"] = $"{field.Title} style",
            ["type"] = "object",
            ["properties"] = properties,
            ["additionalProperties"] = false
        };
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
                ["type"] = "string", ["minLength"] = 1, ["maxLength"] = ContentValidation.ContentTreeValidator.MaxIdLength,
                ["x-reference"] = p.Kind.ToString().ToLowerInvariant()
            },
            PropertyKind.RichText => new JsonObject { ["type"] = "string", ["contentMediaType"] = "text/html" },
            _ => new JsonObject { ["type"] = "string" }
        };
        if (p.MinLength is { } min) schema["minLength"] = min;
        if (p.MaxLength is { } max) schema["maxLength"] = max;
        if (p.Minimum is { } low) schema["minimum"] = low;
        if (p.Maximum is { } high) schema["maximum"] = high;
        return schema;
    }
}

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// The schema.org types a field may produce: the types of <see cref="SchemaOrgCatalog"/>, CreativeWork and the subtypes
/// an editorial site uses. A short, reviewed list rather than any string, so every published post maps to types search
/// engines and AI crawlers understand. Add a type to the catalog when a real field needs it.
/// </summary>
public static class SchemaOrg
{
    public static IReadOnlySet<string> CreativeWorkTypes { get; } =
        new HashSet<string>(SchemaOrgCatalog.Types.Select(t => t.Name), StringComparer.Ordinal);
}

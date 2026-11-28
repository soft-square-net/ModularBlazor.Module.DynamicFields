namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// The DynamicFields dialect of JSON Schema 2020-12 (<see cref="Uri"/>): the standard vocabularies plus five of its own.
/// <c>reference</c> asserts (what a value refers to, the files it takes, a tree's limits); <c>schema-org</c>,
/// <c>style</c>, <c>ui</c> and <c>i18n</c> only annotate, so a plain 2020-12 validator still checks everything the
/// standard keywords say. The meta-schemas ship inside the module (<c>Schemas/2026-12</c>) and are served at
/// <c>GET /api/dynamicfields/schemas/2026-12/{name}</c>. A new edition gets a new folder and URI; this one never changes.
/// </summary>
public static class FieldDialect
{
    public const string Edition = "2026-12";
    public const string Base = "https://schemas.soft-square.net/dynamicfields/" + Edition;
    public const string Uri = Base + "/dialect";

    /// <summary>The file names served: the dialect and its vocabularies' meta-schemas.</summary>
    public static IReadOnlyList<string> Names { get; } = ["dialect", "reference", "schema-org", "style", "ui", "i18n"];

    /// <summary>The vocabularies of the dialect and whether each asserts (true) or only annotates (false).</summary>
    public static IReadOnlyDictionary<string, bool> Vocabularies { get; } = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["reference"] = true,
        ["schema-org"] = false,
        ["style"] = false,
        ["ui"] = false,
        ["i18n"] = false
    };

    /// <summary>The id of a vocabulary's meta-schema (<c>{base}/meta/{name}</c>), or the dialect's own.</summary>
    public static string IdOf(string name) => name == "dialect" ? Uri : $"{Base}/meta/{name}";

    /// <summary>A pinned field version's schema id, the target of a parent's <c>$ref</c>.</summary>
    public static string VersionId(string fieldKey, int version) => $"urn:dynamicfields:{fieldKey}:{version}";

    /// <summary>The text of one of <see cref="Names"/>; null for any other name.</summary>
    public static string? Read(string name)
    {
        if (!Names.Contains(name)) return null;
        var suffix = name == "dialect" ? ".dialect.json" : $".meta.{name}.json";
        var assembly = typeof(FieldDialect).Assembly;
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(suffix, StringComparison.Ordinal));
        if (resource is null) return null;
        using var stream = assembly.GetManifestResourceStream(resource)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

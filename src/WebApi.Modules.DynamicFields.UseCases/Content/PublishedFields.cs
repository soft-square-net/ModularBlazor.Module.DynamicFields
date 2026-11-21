using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content;

/// <summary>Loads the fields a content tree names, in one query, and resolves (key, version) to a published snapshot.</summary>
internal static class PublishedFields
{
    public static async Task<Func<string, int, FieldSnapshot?>> ForTreeAsync(IReadRepository<FieldDefinition> fields, ContentNode root, CancellationToken ct)
    {
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Collect(root, keys, depth: 1);
        var found = keys.Count == 0 ? [] : await fields.ListAsync(new FieldsByKeysSpec(keys), ct);
        var byKey = found.ToDictionary(f => f.Key, StringComparer.Ordinal);
        return (key, number) => byKey.TryGetValue(key, out var field) ? field.GetVersion(number)?.Snapshot : null;
    }

    /// <summary>The field keys the tree names, within the validator's limits (it reports anything beyond them).</summary>
    private static void Collect(ContentNode node, HashSet<string> keys, int depth)
    {
        if (depth > ContentTreeValidator.MaxDepth || keys.Count >= ContentTreeValidator.MaxNodes) return;
        if (!string.IsNullOrEmpty(node.Field)) keys.Add(node.Field);
        foreach (var child in node.Children ?? [])
            if (child is not null) Collect(child, keys, depth + 1);
    }
}

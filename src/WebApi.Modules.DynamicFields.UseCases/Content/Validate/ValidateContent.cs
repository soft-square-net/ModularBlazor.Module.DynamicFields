using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;

/// <summary>The outcome of checking a content tree: valid, or every problem with its JSON Pointer.</summary>
public sealed record ContentReport(bool Valid, IReadOnlyList<ContentIssue> Issues);

/// <summary>
/// Check a post's content tree (the root and every nested content) against the published field versions it names.
/// Nothing is stored: the consumer (Contents) keeps the content.
/// </summary>
public sealed record ValidateContentQuery(ContentNode Root) : IQuery<Result<ContentReport>>;

public sealed class ValidateContentHandler(IReadRepository<FieldDefinition> fields) : IQueryHandler<ValidateContentQuery, Result<ContentReport>>
{
    public async ValueTask<Result<ContentReport>> Handle(ValidateContentQuery query, CancellationToken ct)
    {
        if (query.Root is null) return Result<ContentReport>.Invalid(new ValidationError("root", "Send the content tree's root node."));

        var keys = new HashSet<string>(StringComparer.Ordinal);
        Collect(query.Root, keys, depth: 1);
        var found = keys.Count == 0 ? [] : await fields.ListAsync(new FieldsByKeysSpec(keys), ct);
        var byKey = found.ToDictionary(f => f.Key, StringComparer.Ordinal);

        var issues = ContentTreeValidator.Validate(query.Root,
            (key, number) => byKey.TryGetValue(key, out var field) ? field.GetVersion(number)?.Snapshot : null);
        return new ContentReport(issues.Count == 0, issues);
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

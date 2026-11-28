using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;

/// <summary>The outcome of checking a content tree: valid, or every problem with its JSON Pointer.</summary>
public sealed record ContentReport(bool Valid, IReadOnlyList<ContentIssue> Issues, IReadOnlyList<ContentIssue> Warnings)
{
    /// <summary>
    /// <see cref="Issues"/> are the errors: any one makes the content invalid, and consumers refuse on them.
    /// <see cref="Warnings"/> are advice that never does, so they travel apart from the issues.
    /// </summary>
    public static ContentReport Of(IReadOnlyList<ContentIssue> issues, IReadOnlyList<ContentIssue>? warnings = null) =>
        new(issues.Count == 0, issues, warnings ?? []);
}

/// <summary>
/// Check a post's content tree (the root and every nested content) against the published field versions it names.
/// Lookup values are then checked with the modules that own their lookups. Nothing is stored: the consumer (Contents)
/// keeps the content.
/// </summary>
public sealed record ValidateContentQuery(ContentNode Root) : IQuery<Result<ContentReport>>;

public sealed class ValidateContentHandler(IReadRepository<FieldDefinition> fields, ILookupClient lookups) : IQueryHandler<ValidateContentQuery, Result<ContentReport>>
{
    public async ValueTask<Result<ContentReport>> Handle(ValidateContentQuery query, CancellationToken ct)
    {
        if (query.Root is null) return Result<ContentReport>.Invalid(new ValidationError("root", "Send the content tree's root node."));

        var resolve = await PublishedFields.ForTreeAsync(fields, query.Root, ct);
        var issues = ContentTreeValidator.Validate(query.Root, resolve).ToList();
        await LookupReferences.CheckAsync(LookupReferences.Nodes(query.Root, resolve), lookups, issues, ct);
        return ContentReport.Of(issues);
    }
}

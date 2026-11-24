using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;

/// <summary>The outcome of checking a content tree: valid, or every problem with its JSON Pointer.</summary>
public sealed record ContentReport(bool Valid, IReadOnlyList<ContentIssue> Issues);

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
        return new ContentReport(issues.Count == 0, issues);
    }
}

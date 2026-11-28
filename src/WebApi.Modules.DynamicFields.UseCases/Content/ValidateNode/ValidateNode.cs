using System.Text.Json;
using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.ValidateNode;

/// <summary>
/// Check one node's values and style against a published field version, without its parent or children: what the
/// edit dialog asks before it closes. 404 when the version is not published.
/// </summary>
public sealed record ValidateNodeQuery(string Field, int FieldVersion, JsonElement Content, JsonElement Style)
    : IQuery<Result<ContentReport>>;

public sealed class ValidateNodeHandler(IReadRepository<FieldDefinition> fields, ILookupClient lookups) : IQueryHandler<ValidateNodeQuery, Result<ContentReport>>
{
    public async ValueTask<Result<ContentReport>> Handle(ValidateNodeQuery query, CancellationToken ct)
    {
        var field = await fields.FirstOrDefaultAsync(new FieldDetailSpec(query.Field ?? ""), ct);
        if (field?.GetVersion(query.FieldVersion) is not { } version)
            return Result<ContentReport>.NotFound($"Field '{query.Field}' version {query.FieldVersion} is not published.");

        var issues = ContentTreeValidator.ValidateValues(version.Snapshot, query.Content, query.Style).ToList();
        var node = new ContentNode(version.Snapshot.Key, version.Snapshot.Version, query.Content, query.Style);
        await LookupReferences.CheckAsync([(node, version.Snapshot, "")], lookups, issues, ct);
        return ContentReport.Of(issues);
    }
}

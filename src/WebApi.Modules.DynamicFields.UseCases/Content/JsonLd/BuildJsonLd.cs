using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.JsonLd;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.JsonLd;

/// <summary>A post's schema.org JSON-LD, and the nodes left out because their field version is not published.</summary>
public sealed record JsonLdReport(JsonElement JsonLd, IReadOnlyList<ContentIssue> Skipped);

/// <summary>
/// A post as schema.org JSON-LD (<see cref="JsonLdBuilder"/>): the root's values by their schema.org property, then its
/// contents in reading order. <see cref="Draft"/>: build the root with this draft instead of a published version (the
/// field builder's preview); the root's <c>field</c> is then only its key. Nothing is stored. 404 when the root's field
/// version is not published (and no draft is given).
/// </summary>
public sealed record BuildJsonLdQuery(
    ContentNode Root,
    IReadOnlyDictionary<string, JsonLdAsset>? Assets = null,
    IReadOnlyDictionary<string, string>? Names = null,
    string? Url = null,
    string? Language = null,
    FieldDraft? Draft = null,
    bool Placeholders = false) : IQuery<Result<JsonLdReport>>;

public sealed class BuildJsonLdHandler(IReadRepository<FieldDefinition> fields) : IQueryHandler<BuildJsonLdQuery, Result<JsonLdReport>>
{
    /// <summary>At most this many files and names per call: more than a post's nodes can name.</summary>
    public const int MaxReferences = 2000;

    public async ValueTask<Result<JsonLdReport>> Handle(BuildJsonLdQuery query, CancellationToken ct)
    {
        if (query.Root is null) return Result<JsonLdReport>.Invalid(new ValidationError("root", "Send the post's root node."));
        if (query.Assets?.Count > MaxReferences || query.Names?.Count > MaxReferences)
            return Result<JsonLdReport>.Invalid(new ValidationError("assets", $"Send at most {MaxReferences} files and {MaxReferences} names."));
        if (query.Assets?.Values.Any(a => a is null || string.IsNullOrWhiteSpace(a.Url)) == true)
            return Result<JsonLdReport>.Invalid(new ValidationError("assets", "Every file needs its url."));
        if (query.Url is { } url && !(Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"))
            return Result<JsonLdReport>.Invalid(new ValidationError("url", "The post's url is an http or https address."));

        var resolve = await PublishedFields.ForTreeAsync(fields, query.Root, ct);
        var options = new JsonLdOptions(query.Assets, query.Names, query.Url, query.Language, query.Placeholders);

        JsonLdDocument document;
        if (query.Draft is { } draft)
        {
            var snapshot = Preview(query.Root.Field, draft);
            document = JsonLdBuilder.Build(snapshot, query.Root, resolve, options);
        }
        else
        {
            if (resolve(query.Root.Field ?? "", query.Root.FieldVersion) is not { } root)
                return Result<JsonLdReport>.NotFound($"Field '{query.Root.Field}' version {query.Root.FieldVersion} is not published.");
            document = JsonLdBuilder.Build(root, query.Root, resolve, options);
        }
        return new JsonLdReport(JsonSerializer.SerializeToElement(document.JsonLd), document.Skipped);
    }

    /// <summary>
    /// A draft as an unpublished snapshot (version 0), for the preview. Not checked like a save (an author is still typing
    /// it): values without a key are left out, and an unknown type reads as CreativeWork.
    /// </summary>
    private static FieldSnapshot Preview(string? key, FieldDraft draft)
    {
        var type = SchemaOrg.CreativeWorkTypes.Contains(draft.SchemaType ?? "") ? draft.SchemaType! : "CreativeWork";
        var properties = (draft.Properties ?? []).Where(p => p is not null && !string.IsNullOrWhiteSpace(p.Key))
            .Take(FieldRules.MaxProperties)
            .Select(p => p with
            {
                Label = string.IsNullOrWhiteSpace(p.Label) ? p.Key : p.Label,
                SchemaProperty = string.IsNullOrWhiteSpace(p.SchemaProperty) ? null : p.SchemaProperty.Trim()
            })
            .ToList();
        return new FieldSnapshot(key ?? "", 0, draft.Title ?? "", draft.Description, type, draft.CanBeRoot, properties, [], [],
            null, draft.Groups);
    }
}

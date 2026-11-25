using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.JsonLd;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.JsonLd;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Content;

/// <summary>
/// POST /content/jsonld: a post as schema.org JSON-LD, from its kind and each content's schema.org mapping. 200 with
/// <c>{ jsonLd, skipped }</c>; 404 when the root's field version is not published. Nothing is stored.
/// </summary>
public sealed class BuildJsonLd : IEndpoint
{
    /// <summary>
    /// The request body. <c>root</c>: the post (its kind, values and contents as children, in reading order).
    /// <c>assets</c>: the files Asset values name, by id: <c>{ url, width?, height?, encodingFormat? }</c>. <c>names</c>:
    /// the names of the keywords and terms, by id. <c>url</c>, <c>language</c>: the post's canonical address and language.
    /// <c>draft</c>: build the root with this draft instead (a preview). <c>placeholders</c>: show empty values as ‹label›.
    /// </summary>
    public sealed record JsonLdRequest(
        ContentNode Root,
        IReadOnlyDictionary<string, JsonLdAsset>? Assets,
        IReadOnlyDictionary<string, string>? Names,
        string? Url,
        string? Language,
        FieldDraft? Draft,
        bool? Placeholders);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder content) =>
        content.MapPost("/jsonld", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_BuildJsonLd")
            .WithSummary("Build a post's schema.org JSON-LD from its kind and its contents, in reading order.");

    private static async Task<Results<Ok<JsonLdReport>, ProblemHttpResult>> HandleAsync(JsonLdRequest body, ISender sender, CancellationToken ct) =>
        (await sender.Send(new BuildJsonLdQuery(body.Root, body.Assets, body.Names, body.Url, body.Language, body.Draft,
            body.Placeholders ?? false), ct)).ToOkResult();
}

using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.Translation;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Translate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Content;

/// <summary>
/// POST /content/translations/check: a tree's translatable values (node id, key, source text and its hash) and, with
/// an overlay, its problems and each value's state (current, stale, missing). 200 either way; nothing is stored.
/// </summary>
public sealed class CheckTranslation : IEndpoint
{
    /// <summary>The request body: the source tree and, optionally, its overlay in one language.</summary>
    public sealed record TranslationCheck(ContentNode Root, ContentTranslation? Translation);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder content) =>
        content.MapPost("/translations/check", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_CheckTranslation")
            .WithSummary("List a content tree's translatable values and check a translation overlay against it.");

    private static async Task<Results<Ok<TranslationReport>, ProblemHttpResult>> HandleAsync(TranslationCheck body, ISender sender, CancellationToken ct) =>
        (await sender.Send(new CheckTranslationQuery(body.Root, body.Translation), ct)).ToOkResult();
}

/// <summary>
/// POST /content/translations/apply: the tree with its textual values in the reader's culture, from the overlays sent
/// (the culture's, then its parents'), the source filling in the rest. Shape, style and other values stay the source's.
/// </summary>
public sealed class ApplyTranslations : IEndpoint
{
    /// <summary>The request body. UseStale (default true): serve a translation whose source has changed since.</summary>
    public sealed record TranslationMerge(ContentNode Root, IReadOnlyList<ContentTranslation> Translations, string? Culture, bool? UseStale);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder content) =>
        content.MapPost("/translations/apply", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_ApplyTranslations")
            .WithSummary("Merge translation overlays onto a content tree for a reader's culture, with fallback to the source.");

    private static async Task<Results<Ok<LocalizedContent>, ProblemHttpResult>> HandleAsync(TranslationMerge body, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ApplyTranslationsQuery(body.Root, body.Translations ?? [], body.Culture, body.UseStale ?? true), ct)).ToOkResult();
}

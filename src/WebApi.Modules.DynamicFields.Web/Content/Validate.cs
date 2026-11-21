using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Content;

/// <summary>
/// POST /content/validate: check a post's content tree against the published fields it names. 200 with
/// <c>{ valid, issues }</c> either way; nothing is stored.
/// </summary>
public sealed class Validate : IEndpoint
{
    /// <summary>The request body: the tree's root node (the post).</summary>
    public sealed record ContentTree(ContentNode Root);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder content) =>
        content.MapPost("/validate", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_ValidateContent")
            .WithSummary("Check a content tree against its published fields; every problem comes back with a JSON Pointer.");

    private static async Task<Results<Ok<ContentReport>, ProblemHttpResult>> HandleAsync(ContentTree tree, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ValidateContentQuery(tree.Root), ct)).ToOkResult();
}

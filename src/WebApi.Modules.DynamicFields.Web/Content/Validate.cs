using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.ValidateNode;

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

/// <summary>
/// POST /content/validate-node: check one node's content and style against its field version, without its place in a
/// tree. 200 with <c>{ valid, issues }</c>; 404 when the version is not published.
/// </summary>
public sealed class ValidateNode : IEndpoint
{
    /// <summary>The request body.</summary>
    public sealed record NodeValues(string Field, int FieldVersion, JsonElement Content, JsonElement Style);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder content) =>
        content.MapPost("/validate-node", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_ValidateNode")
            .WithSummary("Check one node's content and style against its field version (what the edit dialog checks).");

    private static async Task<Results<Ok<ContentReport>, ProblemHttpResult>> HandleAsync(NodeValues node, ISender sender, CancellationToken ct) =>
        (await sender.Send(new ValidateNodeQuery(node.Field, node.FieldVersion, node.Content, node.Style), ct)).ToOkResult();
}

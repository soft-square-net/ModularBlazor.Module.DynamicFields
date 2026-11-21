using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Catalog;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Catalog;

/// <summary>GET /catalog/schema-org: the schema.org types and properties the field builder offers, and the style presets.</summary>
public sealed class SchemaOrg : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder catalog) =>
        catalog.MapGet("/schema-org", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_GetSchemaOrgCatalog")
            .WithSummary("The curated schema.org types (with their properties and suggestions) and the style presets.");

    private static async Task<Results<Ok<SchemaOrgCatalogDto>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetSchemaOrgCatalogQuery(), ct)).ToOkResult();
}

/// <summary>GET /catalog/starter-fields: the starter library, marked with the fields the tenant has already.</summary>
public sealed class StarterFields : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder catalog) =>
        catalog.MapGet("/starter-fields", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_ListStarterFields")
            .WithSummary("The starter library: ready-made blocks and posts; installed = the tenant has a field with that key.");

    private static async Task<Results<Ok<IReadOnlyList<StarterFieldDto>>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct) =>
        (await sender.Send(new ListStarterFieldsQuery(), ct)).ToOkResult();
}

/// <summary>POST /catalog/starter-fields/install: create and publish starter fields and the fields they contain.</summary>
public sealed class InstallStarterFields : IEndpoint
{
    /// <summary>The request body; no keys: the whole library.</summary>
    public sealed record StarterInstall(IReadOnlyList<string>? Keys);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder catalog) =>
        catalog.MapPost("/starter-fields/install", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Manage)
            .WithName("DynamicFields_InstallStarterFields")
            .WithSummary("Install starter fields (none given: all) with their children, published as version 1; existing keys are skipped.");

    private static async Task<Results<Ok<StarterInstallReport>, ProblemHttpResult>> HandleAsync(StarterInstall request, ISender sender, CancellationToken ct) =>
        (await sender.Send(new InstallStarterFieldsCommand(request.Keys), ct)).ToOkResult();
}

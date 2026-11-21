using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Create;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Delete;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.EditDraft;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Get;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.GetVersion;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.List;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Publish;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Fields;

/// <summary>GET /fields?page=&amp;pageSize=&amp;search=&amp;canBeRoot=: one page of fields by key.</summary>
public sealed class List : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapGet("/", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_ListFields")
            .WithSummary("One page of fields by key; canBeRoot=true lists the fields a post can be made of.");

    private static async Task<Results<Ok<FieldPage>, ProblemHttpResult>> HandleAsync(ISender sender, CancellationToken ct,
        int page = 1, int pageSize = 20, string? search = null, bool? canBeRoot = null) =>
        (await sender.Send(new ListFieldsQuery(page, pageSize, search, canBeRoot), ct)).ToOkResult();
}

/// <summary>GET /fields/{key}: a field with its draft and its versions.</summary>
public sealed class Get : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapGet("/{key}", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_GetField");

    private static async Task<Results<Ok<FieldDetailDto>, ProblemHttpResult>> HandleAsync(string key, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetFieldQuery(key), ct)).ToOkResult();
}

/// <summary>GET /fields/{key}/versions/{number}: a published version (0: the newest), with its JSON Schemas.</summary>
public sealed class GetVersion : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapGet("/{key}/versions/{number:int}", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Read)
            .WithName("DynamicFields_GetFieldVersion")
            .WithSummary("A published field version; number 0 is the newest.");

    private static async Task<Results<Ok<FieldVersionDto>, ProblemHttpResult>> HandleAsync(string key, int number, ISender sender, CancellationToken ct) =>
        (await sender.Send(new GetFieldVersionQuery(key, number), ct)).ToOkResult();
}

/// <summary>POST /fields: define a new field as a draft; 201 with the field.</summary>
public sealed class Create : IEndpoint
{
    /// <summary>The request body.</summary>
    public sealed record NewField(string Key, FieldDraft Draft);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapPost("/", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Manage)
            .WithName("DynamicFields_CreateField");

    private static async Task<Results<Created<FieldDetailDto>, ProblemHttpResult>> HandleAsync(NewField request, ISender sender, CancellationToken ct) =>
        (await sender.Send(new CreateFieldCommand(request.Key, request.Draft), ct)).ToCreatedResult(field => $"fields/{field.Key}");
}

/// <summary>PUT /fields/{key}/draft: replace the draft; the body carries the version the caller read.</summary>
public sealed class EditDraft : IEndpoint
{
    /// <summary>The request body.</summary>
    public sealed record DraftEdit(int Version, FieldDraft Draft);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapPut("/{key}/draft", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Manage)
            .WithName("DynamicFields_EditFieldDraft");

    private static async Task<Results<Ok<FieldDetailDto>, ProblemHttpResult>> HandleAsync(string key, DraftEdit edit, ISender sender, CancellationToken ct) =>
        (await sender.Send(new EditFieldDraftCommand(key, edit.Version, edit.Draft), ct)).ToOkResult();
}

/// <summary>POST /fields/{key}/publish: publish the draft as the next version, pinning its children.</summary>
public sealed class Publish : IEndpoint
{
    /// <summary>The request body.</summary>
    public sealed record PublishRequest(int Version);

    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapPost("/{key}/publish", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Manage)
            .WithName("DynamicFields_PublishField")
            .WithSummary("Publish the draft as the next version; each child is pinned to its newest published version.");

    private static async Task<Results<Ok<FieldVersionDto>, ProblemHttpResult>> HandleAsync(string key, PublishRequest request, ISender sender, CancellationToken ct) =>
        (await sender.Send(new PublishFieldCommand(key, request.Version), ct)).ToOkResult();
}

/// <summary>DELETE /fields/{key}?version=: delete a field that was never published.</summary>
public sealed class Delete : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder fields) =>
        fields.MapDelete("/{key}", HandleAsync)
            .RequirePermission(DynamicFieldsPermissions.Manage)
            .WithName("DynamicFields_DeleteField")
            .WithSummary("Delete a field that was never published.");

    private static async Task<Results<NoContent, ProblemHttpResult>> HandleAsync(string key, int version, ISender sender, CancellationToken ct) =>
        (await sender.Send(new DeleteFieldCommand(key, version), ct)).ToNoContentResult();
}

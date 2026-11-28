using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Schemas;

/// <summary>
/// GET /schemas/2026-12/{name}: the dialect (<c>dialect</c>) or one of its vocabularies' meta-schemas, as
/// <c>application/schema+json</c>. Public, like any meta-schema: a validator anywhere must be able to read the
/// <c>$schema</c> a published version names. The files never change within an edition, so they are cached.
/// </summary>
public sealed class GetDialectSchema : IEndpoint
{
    public static RouteHandlerBuilder Map(IEndpointRouteBuilder schemas) =>
        schemas.MapGet("/" + FieldDialect.Edition + "/{name}", Handle)
            .AllowAnonymous()
            .WithName("DynamicFields_GetDialectSchema")
            .WithSummary("The DynamicFields JSON Schema dialect (name: dialect) or a vocabulary's meta-schema (reference, schema-org, style, ui, i18n).")
            // A file to the generated client (FileResponse): the text as served, for any JSON Schema library to read.
            .Produces<Stream>(StatusCodes.Status200OK, "application/schema+json");

    private static Results<ContentHttpResult, NotFound> Handle(string name, HttpContext http)
    {
        var text = FieldDialect.Read(name);
        if (text is null) return TypedResults.NotFound();
        http.Response.Headers.CacheControl = "public, max-age=86400";
        return TypedResults.Text(text, "application/schema+json");
    }
}

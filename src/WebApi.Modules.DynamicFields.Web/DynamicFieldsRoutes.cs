using Carter;
using Catalog = soft.square.net.WebApi.Modules.DynamicFields.Web.Catalog;
using Content = soft.square.net.WebApi.Modules.DynamicFields.Web.Content;
using Fields = soft.square.net.WebApi.Modules.DynamicFields.Web.Fields;
using Schemas = soft.square.net.WebApi.Modules.DynamicFields.Web.Schemas;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web;

/// <summary>
/// The module's route table, and its only Carter module (the runtime maps the Carter modules of the entry assembly,
/// under <c>/api/dynamicfields</c>). Every endpoint is listed here once, in the order of the OpenAPI document. A new
/// endpoint: add its class under the resource folder, then one line here.
/// </summary>
public sealed class DynamicFieldsRoutes : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Problem details for domain rules and lost races, on the module's own groups (module code, unloads with it).
        var fields = app.MapGroup("/fields").WithTags("Fields").AddEndpointFilter<DynamicFieldsProblemsFilter>();

        Fields.List.Map(fields);
        Fields.Get.Map(fields);
        Fields.GetVersion.Map(fields);
        Fields.Create.Map(fields);
        Fields.EditDraft.Map(fields);
        Fields.Publish.Map(fields);
        Fields.Delete.Map(fields);

        var content = app.MapGroup("/content").WithTags("Content").AddEndpointFilter<DynamicFieldsProblemsFilter>();

        Content.Validate.Map(content);
        Content.ValidateNode.Map(content);
        Content.CheckTranslation.Map(content);
        Content.ApplyTranslations.Map(content);
        Content.BuildJsonLd.Map(content);

        var catalog = app.MapGroup("/catalog").WithTags("Catalog").AddEndpointFilter<DynamicFieldsProblemsFilter>();

        Catalog.SchemaOrg.Map(catalog);
        Catalog.RootPostFields.Map(catalog);
        Catalog.StarterFields.Map(catalog);
        Catalog.InstallStarterFields.Map(catalog);

        // The dialect and its meta-schemas: public files, no problems filter (nothing to translate).
        var schemas = app.MapGroup("/schemas").WithTags("Schemas");

        Schemas.GetDialectSchema.Map(schemas);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Contracts;
using soft.square.net.WebApi.Modules.DynamicFields.Web.Configurations;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web;

/// <summary>
/// The module: its manifest (id, name, version, route prefix, default settings, permissions) and its services.
/// This is the composition root (the Ardalis template's Program.cs): the runtime creates it, reads the manifest and
/// calls <see cref="ConfigureServices"/> on the module's own container; the endpoints are in
/// <see cref="DynamicFieldsRoutes"/>, mapped under <c>/api/dynamicfields</c>. Tenant data, schema "dynamicfields".
/// </summary>
public sealed class DynamicFieldsModule : IRuntimeModule
{
    public ModuleManifest Manifest { get; } = new("dynamicfields", "DynamicFields", "0.1.0", "dynamicfields")
    {
        Description = "DynamicFields: notes per tenant (the template's example; replace with the module's own features).",
        DefaultSettings = new Dictionary<string, string?> { ["DynamicFields:MaxPageSize"] = "100" },
        Permissions = DynamicFieldsPermissions.All
    };

    public void ConfigureServices(IServiceCollection services, IConfiguration settings) => services.AddServiceConfigs(settings);
}

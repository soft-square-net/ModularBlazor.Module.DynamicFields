using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using soft.square.net.WebApi.Modules.DynamicFields.Infrastructure;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Engine;

namespace soft.square.net.WebApi.Modules.DynamicFields.Web.Configurations;

public static class ServiceConfigs
{
    /// <summary>
    /// Everything the module registers, in the module's own container (never the host's): settings, data access, any
    /// services the use cases share, and the mediator with every handler.
    /// </summary>
    public static IServiceCollection AddServiceConfigs(this IServiceCollection services, IConfiguration settings)
    {
        services.Configure<DynamicFieldsOptions>(settings.GetSection("DynamicFields"));
        services.AddInfrastructureServices();

        // Services shared by several use cases go here (AddScoped<...>()); handlers themselves need no registration.
        // The content check: one per module load (it holds the schema engine's dialect).
        services.AddSingleton<IContentValidator, ContentValidator>();

        services.AddMediatorSourceGen();
        return services;
    }
}

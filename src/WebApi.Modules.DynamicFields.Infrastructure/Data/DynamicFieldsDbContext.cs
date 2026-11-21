using System.Reflection;
using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Contracts.Tenancy;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.DynamicFields.Core.NoteAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Infrastructure.Data;

/// <summary>
/// The module's tenant data: schema "dynamicfields" in each tenant's database (table prefix on engines without schemas).
/// The platform's <see cref="TenantDbContext"/> adds the tenant filter on every <c>ITenantOwned</c> entity, stamps the
/// tenant on save and dispatches domain events. The entity mappings are the <c>IEntityTypeConfiguration</c> classes in
/// <c>Data/Config</c>; the migrations, one set per engine, are in <c>Data/Migrations/&lt;Provider&gt;</c>.
/// </summary>
public sealed class DynamicFieldsDbContext(DbContextOptions<DynamicFieldsDbContext> options, ITenantContext tenant) : TenantDbContext(options, tenant)
{
    protected override string Schema => "dynamicfields";

    public DbSet<Note> Notes => Set<Note>();

    protected override void ConfigureModel(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
}

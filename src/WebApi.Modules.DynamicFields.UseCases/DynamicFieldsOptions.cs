namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases;

/// <summary>Module settings (section <c>DynamicFields</c>; defaults in the manifest, changed on the Host Modules page).</summary>
public sealed class DynamicFieldsOptions
{
    /// <summary>The largest page a list or search returns.</summary>
    public int MaxPageSize { get; set; } = 100;
}

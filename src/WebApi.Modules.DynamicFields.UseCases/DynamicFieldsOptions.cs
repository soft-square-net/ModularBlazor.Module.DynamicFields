namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases;

/// <summary>Module settings (section <c>DynamicFields</c>; defaults in the manifest, changed on the Host Modules page).</summary>
public sealed class DynamicFieldsOptions
{
    /// <summary>The largest page a list or search returns.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>
    /// What checks content: the legacy rules, the schema engine (the fields' compiled JSON Schemas), or both, answering
    /// with the legacy rules and logging where they disagree (the default until the schema engine takes over).
    /// </summary>
    public ValidationEngine ValidationEngine { get; set; } = ValidationEngine.Compare;
}

/// <summary>The engines that check content (<see cref="DynamicFieldsOptions.ValidationEngine"/>).</summary>
public enum ValidationEngine
{
    Legacy,
    Schema,
    Compare
}

using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Engine;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests;

/// <summary>
/// Both engines at once: the schema engine's answer, after asserting it is the legacy rules' answer (the same issues,
/// in any order). Every test that checks content through it is a parity test.
/// </summary>
public sealed class ParityChecked : IContentValidator
{
    private static readonly LegacyContentValidator Legacy = new();
    private static readonly SchemaContentValidator Schema = new();

    public IReadOnlyList<ContentIssue> Validate(ContentNode root, Func<string, int, FieldSnapshot?> resolve) =>
        Same(Legacy.Validate(root, resolve), Schema.Validate(root, resolve));

    public IReadOnlyList<ContentIssue> ValidateValues(FieldSnapshot field, JsonElement content, JsonElement style) =>
        Same(Legacy.ValidateValues(field, content, style), Schema.ValidateValues(field, content, style));

    private static IReadOnlyList<ContentIssue> Same(IReadOnlyList<ContentIssue> legacy, IReadOnlyList<ContentIssue> schema)
    {
        var (onlyLegacy, onlySchema) = ContentValidator.Differences(legacy, schema);
        (onlyLegacy.Count + onlySchema.Count).ShouldBe(0,
            $"only legacy: [{string.Join("; ", onlyLegacy)}]; only schema: [{string.Join("; ", onlySchema)}]");
        return schema;
    }
}

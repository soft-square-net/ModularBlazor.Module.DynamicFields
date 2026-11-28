using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Engine;

/// <summary>Checks content against published field versions (the rules of <see cref="ContentTreeValidator"/>).</summary>
public interface IContentValidator
{
    /// <summary>A post's whole tree; the issues at JSON Pointers into it.</summary>
    IReadOnlyList<ContentIssue> Validate(ContentNode root, Func<string, int, FieldSnapshot?> resolve);

    /// <summary>One node's values and style, without its place in a tree; paths start at /content and /style.</summary>
    IReadOnlyList<ContentIssue> ValidateValues(FieldSnapshot field, JsonElement content, JsonElement style);
}

/// <summary>The legacy rules: the hand-written checks of <see cref="ContentTreeValidator"/>.</summary>
public sealed class LegacyContentValidator : IContentValidator
{
    public IReadOnlyList<ContentIssue> Validate(ContentNode root, Func<string, int, FieldSnapshot?> resolve) =>
        ContentTreeValidator.Validate(root, resolve);

    public IReadOnlyList<ContentIssue> ValidateValues(FieldSnapshot field, JsonElement content, JsonElement style) =>
        ContentTreeValidator.ValidateValues(field, content, style);
}

/// <summary>
/// The module's content check, by the engine its settings name (<see cref="DynamicFieldsOptions.ValidationEngine"/>).
/// <see cref="ValidationEngine.Compare"/> answers with the legacy rules and also runs the schema engine, logging a
/// warning for every check where the two disagree (or an error if the schema engine fails), so the switch to the
/// schema engine can be made once the logs are quiet. The logs name the field and the differing issues (paths and
/// messages, which hold the field's keys and labels, never the content's values).
/// </summary>
public sealed class ContentValidator(IOptions<DynamicFieldsOptions> options, ILoggerFactory loggers) : IContentValidator
{
    private readonly LegacyContentValidator _legacy = new();
    private readonly SchemaContentValidator _schema = new();
    private readonly ILogger _log = loggers.CreateLogger("soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content");

    public IReadOnlyList<ContentIssue> Validate(ContentNode root, Func<string, int, FieldSnapshot?> resolve) =>
        Run(() => _legacy.Validate(root, resolve), () => _schema.Validate(root, resolve), $"'{root?.Field}' version {root?.FieldVersion}");

    public IReadOnlyList<ContentIssue> ValidateValues(FieldSnapshot field, JsonElement content, JsonElement style) =>
        Run(() => _legacy.ValidateValues(field, content, style), () => _schema.ValidateValues(field, content, style),
            $"the values of '{field?.Key}' version {field?.Version}");

    private IReadOnlyList<ContentIssue> Run(Func<IReadOnlyList<ContentIssue>> legacy, Func<IReadOnlyList<ContentIssue>> schema, string what)
    {
        switch (options.Value.ValidationEngine)
        {
            case ValidationEngine.Legacy: return legacy();
            case ValidationEngine.Schema: return schema();
        }

        var answer = legacy();
        try
        {
            var (onlyLegacy, onlySchema) = Differences(answer, schema());
            if (onlyLegacy.Count > 0 || onlySchema.Count > 0)
                _log.LogWarning("The schema engine disagrees with the legacy rules on {What}: only legacy {Legacy}; only schema {Schema}",
                    what, Describe(onlyLegacy), Describe(onlySchema));
        }
        catch (Exception e)
        {
            _log.LogError(e, "The schema engine failed on {What}; the legacy rules answered", what);
        }
        return answer;
    }

    /// <summary>The issues only one side reports, each as often as it reports it beyond the other (order does not matter).</summary>
    public static (IReadOnlyList<ContentIssue> OnlyFirst, IReadOnlyList<ContentIssue> OnlySecond) Differences(
        IReadOnlyList<ContentIssue> first, IReadOnlyList<ContentIssue> second)
    {
        var left = first.ToList();
        var onlySecond = new List<ContentIssue>();
        foreach (var issue in second)
            if (!left.Remove(issue)) onlySecond.Add(issue);
        return (left, onlySecond);
    }

    private static string Describe(IReadOnlyList<ContentIssue> issues) =>
        issues.Count == 0 ? "none" : string.Join("; ", issues.Select(i => $"{(i.Path.Length == 0 ? "/" : i.Path)}: {i.Message}"));
}

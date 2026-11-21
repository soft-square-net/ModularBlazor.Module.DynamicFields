using System.Text.RegularExpressions;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>The rules a field definition keeps; a broken one is a <see cref="DomainException"/>.</summary>
public static partial class FieldRules
{
    public const int KeyMaxLength = 64;
    public const int TitleMaxLength = 120;
    public const int DescriptionMaxLength = 1000;
    public const int LabelMaxLength = 120;
    public const int HelpTextMaxLength = 500;
    public const int DialogIdMaxLength = 128;
    public const int MaxProperties = 100;
    public const int MaxStyleProperties = 50;
    public const int MaxChildRules = 50;
    public const int MaxOptions = 200;
    public const int OptionMaxLength = 100;

    /// <summary>Lowercase, dot-separated: <c>post.news</c>, <c>block.image</c>, <c>block.pull-quote</c>.</summary>
    [GeneratedRegex("^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$")]
    private static partial Regex FieldKey();

    /// <summary>camelCase, as JSON property names and schema.org properties are: <c>headline</c>, <c>contentUrl</c>.</summary>
    [GeneratedRegex("^[a-z][A-Za-z0-9]*$")]
    private static partial Regex CamelCase();

    /// <summary>A style token: <c>wide</c>, <c>md</c>, <c>50%</c>, <c>accent-2</c>.</summary>
    [GeneratedRegex("^[A-Za-z0-9%][A-Za-z0-9%._-]*$")]
    private static partial Regex Token();

    /// <summary>A client module dialog id, <c>"{module id}/{name}"</c>.</summary>
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9._-]*/[A-Za-z0-9._-]+$")]
    private static partial Regex DialogId();

    public static string CheckKey(string? key)
    {
        var trimmed = key?.Trim() ?? "";
        if (trimmed.Length is 0 or > KeyMaxLength || !FieldKey().IsMatch(trimmed))
            throw new DomainException($"A field key is lowercase words joined by '.' or '-' (e.g. block.image), at most {KeyMaxLength} characters: '{key}'.");
        return trimmed;
    }

    /// <summary>Checks a draft and returns it trimmed. <paramref name="ownKey"/> is the field's own key (no self-children).</summary>
    public static FieldDraft Check(string ownKey, FieldDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var title = Text(draft.Title, "A field needs a title", TitleMaxLength, required: true)!;
        var description = Text(draft.Description, "A field's description", DescriptionMaxLength, required: false);

        if (!SchemaOrg.CreativeWorkTypes.Contains(draft.SchemaType ?? ""))
            throw new DomainException($"'{draft.SchemaType}' is not a supported schema.org type; use one of: {string.Join(", ", SchemaOrg.CreativeWorkTypes.Order())}.");

        var properties = (draft.Properties ?? []).Select(CheckProperty).ToList();
        if (properties.Count > MaxProperties) throw new DomainException($"A field has at most {MaxProperties} properties.");
        Unique(properties.Select(p => p.Key), "property");

        var style = (draft.Style ?? []).Select(CheckStyle).ToList();
        if (style.Count > MaxStyleProperties) throw new DomainException($"A field has at most {MaxStyleProperties} style properties.");
        Unique(style.Select(s => s.Key), "style property");

        var children = (draft.Children ?? []).Select(c => CheckChild(ownKey, c)).ToList();
        if (children.Count > MaxChildRules) throw new DomainException($"A field allows at most {MaxChildRules} kinds of children.");
        Unique(children.Select(c => c.FieldKey), "child field");

        var dialog = string.IsNullOrWhiteSpace(draft.EditorDialog) ? null : draft.EditorDialog.Trim();
        if (dialog is not null && (dialog.Length > DialogIdMaxLength || !DialogId().IsMatch(dialog)))
            throw new DomainException($"The editor dialog is a dialog id, \"{{module id}}/{{name}}\" (e.g. Module.Media/edit-image): '{dialog}'.");

        return new FieldDraft(title, draft.SchemaType!, draft.CanBeRoot, properties, style, children, description, dialog);
    }

    private static FieldProperty CheckProperty(FieldProperty p)
    {
        if (p is null) throw new DomainException("A property is missing.");
        var key = p.Key?.Trim() ?? "";
        if (key.Length is 0 or > KeyMaxLength || !CamelCase().IsMatch(key))
            throw new DomainException($"A property key is camelCase (e.g. headline), at most {KeyMaxLength} characters: '{p.Key}'.");
        var label = Text(p.Label, $"Property '{key}' needs a label", LabelMaxLength, required: true)!;
        var help = Text(p.HelpText, $"The help text of '{key}'", HelpTextMaxLength, required: false);
        if (!Enum.IsDefined(p.Kind)) throw new DomainException($"Property '{key}' has an unknown kind.");

        var schemaProperty = string.IsNullOrWhiteSpace(p.SchemaProperty) ? null : p.SchemaProperty.Trim();
        if (schemaProperty is not null && (schemaProperty.Length > KeyMaxLength || !CamelCase().IsMatch(schemaProperty)))
            throw new DomainException($"Property '{key}': a schema.org property is camelCase (e.g. headline): '{schemaProperty}'.");

        var textual = p.Kind is PropertyKind.Text or PropertyKind.RichText;
        if (!textual && (p.MinLength is not null || p.MaxLength is not null))
            throw new DomainException($"Property '{key}': only text has a minimum or maximum length.");
        if (p.MinLength is < 0 || p.MaxLength is < 1 || p.MinLength > p.MaxLength)
            throw new DomainException($"Property '{key}': the length limits are 0 ≤ minimum ≤ maximum, and the maximum at least 1.");

        var numeric = p.Kind is PropertyKind.Integer or PropertyKind.Decimal;
        if (!numeric && (p.Minimum is not null || p.Maximum is not null))
            throw new DomainException($"Property '{key}': only numbers have a minimum or maximum.");
        if (p.Minimum > p.Maximum) throw new DomainException($"Property '{key}': the minimum is above the maximum.");

        IReadOnlyList<string>? options = null;
        if (p.Kind == PropertyKind.Choice)
        {
            options = (p.Options ?? []).Select(o => o?.Trim() ?? "").ToList();
            if (options.Count is 0 or > MaxOptions || options.Any(o => o.Length is 0 or > OptionMaxLength))
                throw new DomainException($"Choice '{key}' needs 1 to {MaxOptions} options of 1 to {OptionMaxLength} characters.");
            Unique(options, $"option of '{key}'");
        }
        else if (p.Options is { Count: > 0 })
            throw new DomainException($"Property '{key}': only a choice has options.");

        if (p.Translatable == true && !textual)
            throw new DomainException($"Property '{key}': only text (Text, RichText) has translations.");

        return p with { Key = key, Label = label, HelpText = help, SchemaProperty = schemaProperty, Options = options,
            Translatable = textual ? p.Translatable : null };
    }

    private static StyleProperty CheckStyle(StyleProperty s)
    {
        if (s is null) throw new DomainException("A style property is missing.");
        var key = s.Key?.Trim() ?? "";
        if (key.Length is 0 or > KeyMaxLength || !CamelCase().IsMatch(key))
            throw new DomainException($"A style key is camelCase (e.g. width), at most {KeyMaxLength} characters: '{s.Key}'.");
        var label = Text(s.Label, $"Style '{key}' needs a label", LabelMaxLength, required: true)!;
        var values = (s.Values ?? []).Select(v => v?.Trim() ?? "").ToList();
        if (values.Count is 0 or > MaxOptions || values.Any(v => v.Length is 0 or > OptionMaxLength || !Token().IsMatch(v)))
            throw new DomainException($"Style '{key}' needs 1 to {MaxOptions} values, each a token (letters, digits, '%', '.', '_', '-').");
        Unique(values, $"value of style '{key}'");
        var fallback = string.IsNullOrWhiteSpace(s.Default) ? null : s.Default.Trim();
        if (fallback is not null && !values.Contains(fallback))
            throw new DomainException($"Style '{key}': the default '{fallback}' is not one of its values.");
        return s with { Key = key, Label = label, Values = values, Default = fallback };
    }

    private static ChildRule CheckChild(string ownKey, ChildRule c)
    {
        if (c is null) throw new DomainException("A child rule is missing.");
        var key = CheckKey(c.FieldKey);
        if (key == ownKey) throw new DomainException($"Field '{ownKey}' cannot contain itself.");
        if (c.Min < 0 || c.Max is < 1 || c.Min > c.Max)
            throw new DomainException($"Child '{key}': the counts are 0 ≤ minimum ≤ maximum, and the maximum at least 1.");
        return c with { FieldKey = key };
    }

    private static string? Text(string? value, string what, int max, bool required)
    {
        var trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (required && trimmed is null) throw new DomainException($"{what}.");
        if (trimmed is { Length: var length } && length > max) throw new DomainException($"{what}: at most {max} characters.");
        return trimmed;
    }

    private static void Unique(IEnumerable<string> keys, string what)
    {
        var duplicate = keys.GroupBy(k => k, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null) throw new DomainException($"The {what} '{duplicate.Key}' appears twice.");
    }
}

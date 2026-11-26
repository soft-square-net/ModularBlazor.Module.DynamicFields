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
    public const int LookupKeyMaxLength = 128;
    public const int LookupMaxDepth = 10;
    public const int LookupMaxPresetFilters = 10;
    public const int LookupMaxFilterValues = 100;
    public const int LookupFilterValueMaxLength = 200;
    public const int LookupMaxSearchFields = 20;
    public const int LookupMaxSelections = 500;
    public const int MaxAccept = 20;
    public const int AcceptMaxLength = 100;
    public const int MaxGroups = 20;
    public const int UrlPrefixMaxLength = 200;

    /// <summary>The filter operators a lookup understands.</summary>
    public static IReadOnlyList<string> LookupOps { get; } =
        ["eq", "ne", "contains", "startsWith", "gt", "gte", "lt", "lte", "between", "in", "isEmpty"];

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

    /// <summary>
    /// An entry of an Asset property's accept list, lowercase: a content type <c>image/png</c>, a family <c>image/*</c>
    /// or an extension <c>.pdf</c>.
    /// </summary>
    [GeneratedRegex(@"^(?:[a-z0-9][a-z0-9!#$&^_.+-]*/(?:\*|[a-z0-9][a-z0-9!#$&^_.+-]*)|\.[a-z0-9][a-z0-9_-]*)$")]
    private static partial Regex AcceptEntry();

    /// <summary>A lookup key, <c>"{prefix}.{name}"</c>: <c>inventory.stock-items</c>, <c>geo.cities</c>.</summary>
    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]*(?:\.[A-Za-z0-9][A-Za-z0-9_-]*)+$")]
    private static partial Regex LookupKey();

    /// <summary>A property name of a lookup's entity (a search or filter field): <c>Name</c>, <c>Category.Name</c>.</summary>
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*$")]
    private static partial Regex MemberName();

    /// <summary>
    /// A root field's URL prefix: lowercase slug segments (letters, digits, single hyphens between them) separated by
    /// <c>/</c>, with no leading or trailing slash: <c>news</c>, <c>ar/news</c>, <c>opinion-pieces</c>.
    /// </summary>
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*)*$")]
    private static partial Regex UrlPrefixPattern();

    /// <summary>Whether a text is a valid URL prefix as it stands (no trimming).</summary>
    public static bool IsUrlPrefix(string? prefix) =>
        prefix is { Length: > 0 and <= UrlPrefixMaxLength } && UrlPrefixPattern().IsMatch(prefix);

    /// <summary>
    /// Checks a draft's URL prefix and returns it trimmed (null when blank). Only a root field has one; publishing a
    /// root field requires it (<see cref="FieldDefinition.Publish"/>).
    /// </summary>
    public static string? CheckUrlPrefix(string? prefix, bool canBeRoot)
    {
        var trimmed = string.IsNullOrWhiteSpace(prefix) ? null : prefix.Trim();
        if (trimmed is null) return null;
        if (!canBeRoot) throw new DomainException($"Only a field a post can be made of (a root) has a URL prefix: '{trimmed}'.");
        if (!IsUrlPrefix(trimmed))
            throw new DomainException($"A URL prefix is lowercase words (a-z, 0-9, single '-') separated by '/', with no slash at either end (e.g. news, ar/news), at most {UrlPrefixMaxLength} characters: '{trimmed}'.");
        return trimmed;
    }

    /// <summary>
    /// The URL prefix a root field's key suggests: <c>post.article</c> → <c>articles</c>, <c>post.news</c> → <c>news</c>,
    /// <c>post.review</c> → <c>reviews</c>, <c>post.blog</c> → <c>blog</c>, <c>post.video</c> → <c>videos</c>,
    /// <c>post.reportage</c> → <c>reportage</c>, <c>post.photo-story</c> → <c>photo-stories</c>, <c>post.other</c> →
    /// <c>posts</c>; any other key without its <c>post.</c> start and with '.' as '-' (<c>post.photo.story</c> →
    /// <c>photo-story</c>). The data migration <c>AddUrlPrefix</c> filled existing fields the same way, but knew only the
    /// first four named keys.
    /// </summary>
    public static string SuggestUrlPrefix(string key)
    {
        var known = key switch
        {
            "post.article" => "articles",
            "post.news" => "news",
            "post.review" => "reviews",
            "post.blog" => "blog",
            "post.video" => "videos",
            "post.photo-story" => "photo-stories",
            "post.other" => "posts",
            _ => null
        };
        if (known is not null) return known;
        var rest = key.StartsWith("post.", StringComparison.Ordinal) && key.Length > 5 ? key[5..] : key;
        return rest.Replace('.', '-');
    }

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

        var groups = CheckGroups(draft.Groups, draft.CanBeRoot);

        var properties = (draft.Properties ?? []).Select(CheckProperty).ToList();
        if (properties.Count > MaxProperties) throw new DomainException($"A field has at most {MaxProperties} properties.");
        Unique(properties.Select(p => p.Key), "property");
        CheckLookupParents(properties);
        properties = InGroups(properties, groups);

        var style = (draft.Style ?? []).Select(CheckStyle).ToList();
        if (style.Count > MaxStyleProperties) throw new DomainException($"A field has at most {MaxStyleProperties} style properties.");
        Unique(style.Select(s => s.Key), "style property");

        var children = (draft.Children ?? []).Select(c => CheckChild(ownKey, c)).ToList();
        if (children.Count > MaxChildRules) throw new DomainException($"A field allows at most {MaxChildRules} kinds of children.");
        Unique(children.Select(c => c.FieldKey), "child field");

        var dialog = string.IsNullOrWhiteSpace(draft.EditorDialog) ? null : draft.EditorDialog.Trim();
        if (dialog is not null && (dialog.Length > DialogIdMaxLength || !DialogId().IsMatch(dialog)))
            throw new DomainException($"The editor dialog is a dialog id, \"{{module id}}/{{name}}\" (e.g. Module.Media/edit-image): '{dialog}'.");

        var urlPrefix = CheckUrlPrefix(draft.UrlPrefix, draft.CanBeRoot);

        return new FieldDraft(title, draft.SchemaType!, draft.CanBeRoot, properties, style, children, description, dialog, groups, urlPrefix);
    }

    /// <summary>A root field's value groups, trimmed; null when there are none.</summary>
    private static IReadOnlyList<PropertyGroup>? CheckGroups(IReadOnlyList<PropertyGroup>? groups, bool canBeRoot)
    {
        if (groups is null or { Count: 0 }) return null;
        if (!canBeRoot) throw new DomainException("Only a field a post can be made of (a root) groups its values.");
        if (groups.Count > MaxGroups) throw new DomainException($"A field has at most {MaxGroups} value groups.");
        var checkedGroups = groups.Select(g =>
        {
            if (g is null) throw new DomainException("A value group is missing.");
            var key = g.Key?.Trim() ?? "";
            if (key.Length is 0 or > KeyMaxLength || !CamelCase().IsMatch(key))
                throw new DomainException($"A value group's key is camelCase (e.g. seo), at most {KeyMaxLength} characters: '{g.Key}'.");
            var label = Text(g.Label, $"Value group '{key}' needs a label", LabelMaxLength, required: true)!;
            var description = Text(g.Description, $"The description of value group '{key}'", HelpTextMaxLength, required: false);
            return new PropertyGroup(key, label, description);
        }).ToList();
        Unique(checkedGroups.Select(g => g.Key), "value group");
        return checkedGroups;
    }

    /// <summary>Each value in a declared group (none given: the first); no group at all when the field has none.</summary>
    private static List<FieldProperty> InGroups(List<FieldProperty> properties, IReadOnlyList<PropertyGroup>? groups)
    {
        if (groups is null)
        {
            var grouped = properties.FirstOrDefault(p => p.Group is not null);
            if (grouped is not null) throw new DomainException($"Property '{grouped.Key}' is in group '{grouped.Group}', but the field has no value groups.");
            return properties;
        }
        var keys = groups.Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        return [.. properties.Select(p =>
        {
            if (p.Group is null) return p with { Group = groups[0].Key };
            if (!keys.Contains(p.Group)) throw new DomainException($"Property '{p.Key}': the field has no value group '{p.Group}'.");
            return p;
        })];
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

        LookupBinding? lookup = null;
        if (p.Kind == PropertyKind.Lookup) lookup = CheckLookup(key, p.Multiple, p.Lookup);
        else if (p.Lookup is not null)
            throw new DomainException($"Property '{key}': only a Lookup property has a lookup.");

        IReadOnlyList<string>? accept = null;
        if (p.Kind == PropertyKind.Asset) accept = CheckAccept(key, p.Accept);
        else if (p.Accept is { Count: > 0 })
            throw new DomainException($"Property '{key}': only an Asset property has accepted file types.");

        var group = string.IsNullOrWhiteSpace(p.Group) ? null : p.Group.Trim();

        return p with { Key = key, Label = label, HelpText = help, SchemaProperty = schemaProperty, Options = options,
            Translatable = textual ? p.Translatable : null, Lookup = lookup, Accept = accept, Group = group };
    }

    /// <summary>Trimmed, lowercase and without repeats; null when empty (any file).</summary>
    private static IReadOnlyList<string>? CheckAccept(string key, IReadOnlyList<string>? accept)
    {
        if (accept is null or { Count: 0 }) return null;
        var entries = accept.Select(a => a?.Trim().ToLowerInvariant() ?? "").Distinct().ToList();
        if (entries.Count > MaxAccept)
            throw new DomainException($"Asset '{key}' accepts at most {MaxAccept} file types.");
        foreach (var entry in entries)
            if (entry.Length is 0 or > AcceptMaxLength || !AcceptEntry().IsMatch(entry))
                throw new DomainException($"Asset '{key}': a file type is a content type (image/png), a family (image/*) or an extension (.pdf): '{entry}'.");
        return entries;
    }

    private static LookupBinding CheckLookup(string key, bool multiple, LookupBinding? b)
    {
        if (b is null) throw new DomainException($"Lookup '{key}' needs the lookup it picks from.");
        var lookupKey = b.Key?.Trim() ?? "";
        if (lookupKey.Length is 0 or > LookupKeyMaxLength || !LookupKey().IsMatch(lookupKey))
            throw new DomainException($"Lookup '{key}': a lookup key is \"{{prefix}}.{{name}}\" (e.g. inventory.stock-items): '{b.Key}'.");
        if (!Enum.IsDefined(b.Mode)) throw new DomainException($"Lookup '{key}' has an unknown mode.");
        if (b.MinChars is < 0 or > 10) throw new DomainException($"Lookup '{key}': characters before searching are 0 to 10.");

        IReadOnlyList<string>? searchFields = null;
        if (b.SearchFields is { Count: > 0 })
        {
            searchFields = b.SearchFields.Select(f => f?.Trim() ?? "").ToList();
            if (searchFields.Count > LookupMaxSearchFields || searchFields.Any(f => f.Length is 0 or > KeyMaxLength || !MemberName().IsMatch(f)))
                throw new DomainException($"Lookup '{key}': at most {LookupMaxSearchFields} search fields, each a property name (e.g. Name).");
            Unique(searchFields, $"search field of '{key}'");
        }

        var parent = string.IsNullOrWhiteSpace(b.ParentProperty) ? null : b.ParentProperty.Trim();
        if (parent == key) throw new DomainException($"Lookup '{key}' cannot be its own parent.");
        if (parent is not null && b.Mode == LookupMode.Picker)
            throw new DomainException($"Lookup '{key}': a lookup that depends on another property is an autocomplete, not a picker.");

        var rootKey = string.IsNullOrWhiteSpace(b.RootKey) ? null : b.RootKey.Trim();
        if (b.Recursive)
        {
            if (multiple) throw new DomainException($"Lookup '{key}': a recursive lookup picks one value (a path), not a list.");
            if (b.Mode == LookupMode.Picker) throw new DomainException($"Lookup '{key}': a recursive lookup is a cascade of autocompletes, not a picker.");
            if (parent is not null) throw new DomainException($"Lookup '{key}': a recursive lookup starts at its root key, not at another property.");
            if (b.MaxDepth is < 1 or > LookupMaxDepth) throw new DomainException($"Lookup '{key}': the depth is 1 to {LookupMaxDepth} levels.");
            if (rootKey is { Length: > LookupFilterValueMaxLength })
                throw new DomainException($"Lookup '{key}': the root key has at most {LookupFilterValueMaxLength} characters.");
        }
        else if (rootKey is not null || b.LeafOnly)
            throw new DomainException($"Lookup '{key}': a root key and leaf-only belong to a recursive lookup.");

        if (b.MaxSelections is not null && (!multiple || b.MaxSelections is < 1 or > LookupMaxSelections))
            throw new DomainException($"Lookup '{key}': a maximum of selections is for a multiple lookup, 1 to {LookupMaxSelections}.");

        var filters = (b.PresetFilters ?? []).Select(f => CheckPresetFilter(key, f)).ToList();
        if (filters.Count > LookupMaxPresetFilters) throw new DomainException($"Lookup '{key}' has at most {LookupMaxPresetFilters} preset filters.");

        return b with
        {
            Key = lookupKey, SearchFields = searchFields, ParentProperty = parent, RootKey = rootKey,
            MaxDepth = b.Recursive ? b.MaxDepth : 5, PresetFilters = filters.Count == 0 ? null : filters,
            AllowUserFilters = b.Mode == LookupMode.Picker && b.AllowUserFilters
        };
    }

    private static LookupPresetFilter CheckPresetFilter(string key, LookupPresetFilter f)
    {
        if (f is null) throw new DomainException($"Lookup '{key}': a preset filter is missing.");
        var field = f.Field?.Trim() ?? "";
        if (field.Length is 0 or > KeyMaxLength || !MemberName().IsMatch(field))
            throw new DomainException($"Lookup '{key}': a filter's field is a property name (e.g. Kind): '{f.Field}'.");
        var op = f.Op ?? "";
        if (!LookupOps.Contains(op))
            throw new DomainException($"Lookup '{key}': '{f.Op}' is not a filter operator; use one of: {string.Join(", ", LookupOps)}.");

        var value = f.Value?.Trim();
        var values = f.Values?.Select(v => v?.Trim() ?? "").ToList();
        if (value is { Length: > LookupFilterValueMaxLength } || values is { Count: > LookupMaxFilterValues }
            || values?.Any(v => v.Length > LookupFilterValueMaxLength) == true)
            throw new DomainException($"Lookup '{key}': a filter has at most {LookupMaxFilterValues} values of {LookupFilterValueMaxLength} characters.");

        switch (op)
        {
            case "isEmpty":
                return new(field, op);
            case "in":
                if (values is not { Count: > 0 }) throw new DomainException($"Lookup '{key}': filter '{field} in' needs values.");
                return new(field, op, Values: values);
            case "between":
                if (values is not { Count: 2 }) throw new DomainException($"Lookup '{key}': filter '{field} between' needs two values.");
                return new(field, op, Values: values);
            default:
                if (string.IsNullOrEmpty(value)) throw new DomainException($"Lookup '{key}': filter '{field} {op}' needs a value.");
                return new(field, op, value);
        }
    }

    /// <summary>A dependent lookup's parent is another single Lookup property of the field, with no loop.</summary>
    private static void CheckLookupParents(IReadOnlyList<FieldProperty> properties)
    {
        var byKey = properties.ToDictionary(p => p.Key, StringComparer.Ordinal);
        foreach (var p in properties)
        {
            if (p.Lookup?.ParentProperty is not { } parentKey) continue;
            if (!byKey.TryGetValue(parentKey, out var parent) || parent.Kind != PropertyKind.Lookup)
                throw new DomainException($"Lookup '{p.Key}': its parent '{parentKey}' is not a Lookup property of this field.");
            if (parent.Multiple) throw new DomainException($"Lookup '{p.Key}': its parent '{parentKey}' holds one value, not a list.");

            var seen = new HashSet<string>(StringComparer.Ordinal) { p.Key };
            for (var at = parent; ; )
            {
                if (!seen.Add(at.Key)) throw new DomainException($"Lookup '{p.Key}': its parents go round in a loop.");
                if (at.Lookup?.ParentProperty is not { } next || !byKey.TryGetValue(next, out var up)) break;
                at = up;
            }
        }
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

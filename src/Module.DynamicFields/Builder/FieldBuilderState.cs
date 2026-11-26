using System.Text.RegularExpressions;
using soft.square.net.WebApi.Modules.DynamicFields.Client;

namespace soft.square.net.Module.DynamicFields.Builder;

/// <summary>
/// What the field builder edits: a field's draft as rows the form binds to (lists of options and tokens as text, one
/// row per value, style and child), turned back into the API's <see cref="FieldDraft"/> to save, and into a
/// <see cref="FieldSnapshot"/> for the live preview. The server checks the draft; the builder only shapes it.
/// </summary>
public sealed partial class FieldBuilderState
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string SchemaType { get; set; } = "CreativeWork";
    public bool CanBeRoot { get; set; }
    public string? EditorDialog { get; set; }

    /// <summary>A root field's static URL, the public route prefix of its posts (<c>news</c>, <c>ar/news</c>).</summary>
    public string? UrlPrefix { get; set; }

    public List<PropertyRow> Properties { get; } = [];
    public List<StyleRow> Style { get; } = [];
    public List<ChildRow> Children { get; } = [];

    /// <summary>A root field's value groups (main, seo), in order; the first holds the values with no group.</summary>
    public List<GroupRow> Groups { get; } = [];

    /// <summary>Whether the values are grouped: a root field with at least one group.</summary>
    public bool Grouped => CanBeRoot && Groups.Count > 0;

    public static FieldBuilderState From(string key, FieldDraft draft)
    {
        var state = new FieldBuilderState
        {
            Key = key,
            Title = draft.Title,
            Description = draft.Description,
            SchemaType = draft.SchemaType,
            CanBeRoot = draft.CanBeRoot,
            EditorDialog = draft.EditorDialog,
            UrlPrefix = draft.UrlPrefix
        };
        state.Properties.AddRange(draft.Properties.Select(PropertyRow.From));
        state.Style.AddRange(draft.Style.Select(StyleRow.From));
        state.Children.AddRange(draft.Children.Select(c => new ChildRow { FieldKey = c.FieldKey, Min = c.Min ?? 0, Max = c.Max }));
        state.Groups.AddRange((draft.Groups ?? []).Select(g => new GroupRow { Key = g.Key, Label = g.Label, Description = g.Description }));
        return state;
    }

    public FieldDraft ToDraft() => new()
    {
        Groups = Grouped ? [.. Groups.Select(g => g.ToModel())] : null,
        Title = Title.Trim(),
        SchemaType = SchemaType,
        CanBeRoot = CanBeRoot,
        Description = Blank(Description),
        EditorDialog = Blank(EditorDialog),
        UrlPrefix = CanBeRoot ? Blank(UrlPrefix) : null,
        Properties = [.. Properties.Select(p => p.ToModel(GroupOf(p)))],
        Style = [.. Style.Select(s => s.ToModel())],
        Children = [.. Children.Select(c => new ChildRule { FieldKey = c.FieldKey, Min = c.Min, Max = c.Max })]
    };

    /// <summary>
    /// The group a value is saved in: its own while the field has it, else the first; none when the values are not
    /// grouped.
    /// </summary>
    public string? GroupOf(PropertyRow row)
    {
        if (!Grouped) return null;
        var keys = Groups.Select(g => g.Key.Trim()).ToList();
        return row.Group is { } own && keys.Contains(own) ? own : keys[0];
    }

    /// <summary>The draft as an unpublished version (0), for the preview.</summary>
    public FieldSnapshot ToSnapshot()
    {
        var draft = ToDraft();
        return new FieldSnapshot
        {
            Key = Key.Trim().Length > 0 ? Key.Trim() : "new-field",
            Version = 0,
            Title = draft.Title.Length > 0 ? draft.Title : "Untitled field",
            Description = draft.Description,
            SchemaType = draft.SchemaType,
            CanBeRoot = draft.CanBeRoot,
            Properties = draft.Properties,
            Style = [.. draft.Style.Where(s => s.Values.Count > 0)],
            Children = [.. draft.Children.Select(c => new PinnedChild { FieldKey = c.FieldKey, Version = 0, Min = c.Min ?? 0, Max = c.Max })],
            EditorDialog = draft.EditorDialog,
            Groups = draft.Groups,
            UrlPrefix = draft.UrlPrefix
        };
    }

    /// <summary>The server's URL prefix rule (FieldRules.IsUrlPrefix): lowercase slug segments joined by '/'.</summary>
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*(?:/[a-z0-9]+(?:-[a-z0-9]+)*)*$")]
    private static partial Regex UrlPrefixPattern();

    /// <summary>Whether the typed URL prefix is blank (none yet) or keeps the rule.</summary>
    public bool UrlPrefixIsValid =>
        string.IsNullOrWhiteSpace(UrlPrefix) || (UrlPrefix.Trim().Length <= 200 && UrlPrefixPattern().IsMatch(UrlPrefix.Trim()));

    /// <summary>
    /// The URL prefix a key suggests, as the server's FieldRules.SuggestUrlPrefix: post.article → articles, post.news →
    /// news, post.review → reviews, post.blog → blog; else the key without "post." and with '.' as '-'.
    /// </summary>
    public static string SuggestUrlPrefix(string key)
    {
        key = key.Trim();
        return key switch
        {
            "post.article" => "articles",
            "post.news" => "news",
            "post.review" => "reviews",
            "post.blog" => "blog",
            "" => "news",
            _ => (key.StartsWith("post.", StringComparison.Ordinal) && key.Length > 5 ? key[5..] : key).Replace('.', '-')
        };
    }

    /// <summary>Moves a row up (-1) or down (+1).</summary>
    public static void Move<T>(List<T> rows, T row, int by)
    {
        var from = rows.IndexOf(row);
        var to = from + by;
        if (from < 0 || to < 0 || to >= rows.Count) return;
        rows.RemoveAt(from);
        rows.Insert(to, row);
    }

    /// <summary>A key not used yet among <paramref name="taken"/>: name, name2, name3, ...</summary>
    public static string FreeKey(string name, IEnumerable<string> taken)
    {
        var used = taken.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(name)) return name;
        for (var i = 2; ; i++)
            if (!used.Contains($"{name}{i}")) return $"{name}{i}";
    }

    /// <summary>Splits a list typed as text: one item per line or comma, trimmed, without blanks or repeats.</summary>
    public static List<string> SplitList(string? text) =>
        [.. (text ?? "").Split([',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal)];

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    /// <summary>One value of the field.</summary>
    public sealed class PropertyRow
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public PropertyKind Kind { get; set; } = PropertyKind.Text;
        public bool Required { get; set; }
        public bool Multiple { get; set; }
        public bool Translatable { get; set; } = true;
        public string? SchemaProperty { get; set; }
        public string? HelpText { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }
        public string OptionsText { get; set; } = "";
        public LookupRow Lookup { get; set; } = new();

        /// <summary>
        /// The files an Asset value takes, as typed: content types, families or extensions separated by commas
        /// (<c>image/*</c>, <c>.pdf</c>); empty: any file.
        /// </summary>
        public string AcceptText { get; set; } = "";

        /// <summary>The value group's key (a root field with groups); null: the first group.</summary>
        public string? Group { get; set; }

        /// <summary>Whether the row's details are shown in the builder.</summary>
        public bool Open { get; set; }

        public bool IsText => Kind is PropertyKind.Text or PropertyKind.RichText;
        public bool IsNumber => Kind is PropertyKind.Integer or PropertyKind.Decimal;
        public bool IsLookup => Kind == PropertyKind.Lookup;

        public static PropertyRow From(FieldProperty p) => new()
        {
            Key = p.Key,
            Label = p.Label,
            Kind = p.Kind,
            Required = p.Required == true,
            Multiple = p.Multiple == true,
            SchemaProperty = p.SchemaProperty,
            HelpText = p.HelpText,
            Translatable = p.Translatable != false,
            MinLength = p.MinLength,
            MaxLength = p.MaxLength,
            Minimum = p.Minimum,
            Maximum = p.Maximum,
            OptionsText = string.Join("\n", p.Options ?? []),
            Lookup = LookupRow.From(p.Lookup),
            AcceptText = string.Join(", ", p.Accept ?? []),
            Group = p.Group
        };

        /// <summary>
        /// A value made from a schema.org property, with the kind and limits the catalog suggests.
        /// <paramref name="contentAccept"/>: the files the field's type takes in contentUrl (a VideoObject's are videos).
        /// </summary>
        public static PropertyRow From(SchemaOrgProperty p, string key, IReadOnlyList<string>? contentAccept = null) => new()
        {
            Key = key,
            Label = p.Label,
            Kind = p.Kind,
            Multiple = p.Multiple == true,
            SchemaProperty = p.Name,
            HelpText = p.Description,
            MaxLength = p.MaxLength,
            Minimum = p.Minimum,
            Maximum = p.Maximum,
            OptionsText = string.Join("\n", p.Options ?? []),
            AcceptText = string.Join(", ", (p.Name == "contentUrl" ? contentAccept : null) ?? p.Accept ?? [])
        };

        /// <summary>The API's property; limits that do not fit the kind are dropped, not sent.</summary>
        public FieldProperty ToModel(string? group = null) => new()
        {
            Group = group,
            Key = Key.Trim(),
            Label = Label.Trim(),
            Kind = Kind,
            Required = Required,
            Multiple = Multiple,
            SchemaProperty = Blank(SchemaProperty),
            Translatable = IsText && !Translatable ? false : null,
            HelpText = Blank(HelpText),
            MinLength = IsText ? MinLength : null,
            MaxLength = IsText ? MaxLength : null,
            Minimum = IsNumber ? Minimum : null,
            Maximum = IsNumber ? Maximum : null,
            Options = Kind == PropertyKind.Choice ? SplitList(OptionsText) : null,
            Lookup = IsLookup ? Lookup.ToModel(Multiple) : null,
            Accept = Kind == PropertyKind.Asset && AcceptList is { Count: > 0 } accept ? accept : null
        };

        /// <summary>The accept list as typed, lowercase, without blanks or repeats.</summary>
        public List<string> AcceptList => [.. SplitList(AcceptText.ToLowerInvariant())];
    }

    /// <summary>
    /// What a Lookup value picks from and how: the lookup (from the catalog), the control, and its options. Options that
    /// do not fit the control are dropped, not sent (the server would refuse them).
    /// </summary>
    public sealed class LookupRow
    {
        public string Key { get; set; } = "";
        public LookupMode Mode { get; set; } = LookupMode.Autocomplete;
        public string SearchFieldsText { get; set; } = "";
        public int MinChars { get; set; }
        public string? ParentProperty { get; set; }
        public bool Recursive { get; set; }
        public string? RootKey { get; set; }
        public int MaxDepth { get; set; } = 5;
        public bool LeafOnly { get; set; }
        public bool AllowUserFilters { get; set; } = true;
        public int? MaxSelections { get; set; }
        public List<PresetFilterRow> Filters { get; } = [];

        public static LookupRow From(LookupBinding? b)
        {
            if (b is null) return new();
            var row = new LookupRow
            {
                Key = b.Key,
                Mode = b.Mode ?? LookupMode.Autocomplete,
                SearchFieldsText = string.Join(", ", b.SearchFields ?? []),
                MinChars = b.MinChars ?? 0,
                ParentProperty = b.ParentProperty,
                Recursive = b.Recursive == true,
                RootKey = b.RootKey,
                MaxDepth = b.MaxDepth ?? 5,
                LeafOnly = b.LeafOnly == true,
                AllowUserFilters = b.AllowUserFilters != false,
                MaxSelections = b.MaxSelections
            };
            row.Filters.AddRange((b.PresetFilters ?? []).Select(f => new PresetFilterRow
            {
                Field = f.Field, Op = f.Op, Value = f.Value ?? "", ValuesText = string.Join(", ", f.Values ?? [])
            }));
            return row;
        }

        public LookupBinding ToModel(bool multiple)
        {
            var recursive = Recursive && !multiple;
            var mode = recursive ? LookupMode.Autocomplete : Mode;
            var searchFields = SplitList(SearchFieldsText);
            var filters = Filters.Where(f => f.Field.Trim().Length > 0).Select(f => f.ToModel()).ToList();
            return new LookupBinding
            {
                Key = Key.Trim(),
                Mode = mode,
                SearchFields = searchFields.Count > 0 ? searchFields : null,
                MinChars = MinChars,
                ParentProperty = recursive || mode == LookupMode.Picker ? null : Blank(ParentProperty),
                Recursive = recursive,
                RootKey = recursive ? Blank(RootKey) : null,
                MaxDepth = recursive ? MaxDepth : 5,
                LeafOnly = recursive && LeafOnly,
                PresetFilters = filters.Count > 0 ? filters : null,
                AllowUserFilters = mode == LookupMode.Picker && AllowUserFilters,
                MaxSelections = multiple ? MaxSelections : null
            };
        }
    }

    /// <summary>A filter the author fixes: field, operator, and one value or a list (in, between).</summary>
    public sealed class PresetFilterRow
    {
        public string Field { get; set; } = "";
        public string Op { get; set; } = "eq";
        public string Value { get; set; } = "";
        public string ValuesText { get; set; } = "";

        public bool TakesValues => Op is "in" or "between";

        public LookupPresetFilter ToModel() => new()
        {
            Field = Field.Trim(),
            Op = Op,
            Value = Op is "isEmpty" || TakesValues ? null : Value.Trim(),
            Values = TakesValues ? SplitList(ValuesText) : null
        };
    }

    /// <summary>One style choice of the field: theme tokens, typed one per line or comma.</summary>
    public sealed class StyleRow
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string ValuesText { get; set; } = "";
        public bool Responsive { get; set; }
        public string? Default { get; set; }
        public bool Open { get; set; }

        public List<string> Values => SplitList(ValuesText);

        public static StyleRow From(StyleProperty s) => new()
        {
            Key = s.Key,
            Label = s.Label,
            ValuesText = string.Join(", ", s.Values),
            Responsive = s.Responsive == true,
            Default = s.Default
        };

        public StyleProperty ToModel()
        {
            var values = Values;
            return new StyleProperty
            {
                Key = Key.Trim(),
                Label = Label.Trim(),
                Values = values,
                Responsive = Responsive,
                Default = Default is { Length: > 0 } d && values.Contains(d) ? d : null
            };
        }
    }

    /// <summary>A group of a root field's values: main, seo, ...</summary>
    public sealed class GroupRow
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string? Description { get; set; }

        public PropertyGroup ToModel() => new() { Key = Key.Trim(), Label = Label.Trim(), Description = Blank(Description) };
    }

    /// <summary>A field this one may contain, and how many of it (no maximum: any number).</summary>
    public sealed class ChildRow
    {
        public string FieldKey { get; set; } = "";
        public int Min { get; set; }
        public int? Max { get; set; }
    }
}

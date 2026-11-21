using soft.square.net.WebApi.Modules.DynamicFields.Client;

namespace soft.square.net.Module.DynamicFields.Builder;

/// <summary>
/// What the field builder edits: a field's draft as rows the form binds to (lists of options and tokens as text, one
/// row per value, style and child), turned back into the API's <see cref="FieldDraft"/> to save, and into a
/// <see cref="FieldSnapshot"/> for the live preview. The server checks the draft; the builder only shapes it.
/// </summary>
public sealed class FieldBuilderState
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string SchemaType { get; set; } = "CreativeWork";
    public bool CanBeRoot { get; set; }
    public string? EditorDialog { get; set; }
    public List<PropertyRow> Properties { get; } = [];
    public List<StyleRow> Style { get; } = [];
    public List<ChildRow> Children { get; } = [];

    public static FieldBuilderState From(string key, FieldDraft draft)
    {
        var state = new FieldBuilderState
        {
            Key = key,
            Title = draft.Title,
            Description = draft.Description,
            SchemaType = draft.SchemaType,
            CanBeRoot = draft.CanBeRoot,
            EditorDialog = draft.EditorDialog
        };
        state.Properties.AddRange(draft.Properties.Select(PropertyRow.From));
        state.Style.AddRange(draft.Style.Select(StyleRow.From));
        state.Children.AddRange(draft.Children.Select(c => new ChildRow { FieldKey = c.FieldKey, Min = c.Min ?? 0, Max = c.Max }));
        return state;
    }

    public FieldDraft ToDraft() => new()
    {
        Title = Title.Trim(),
        SchemaType = SchemaType,
        CanBeRoot = CanBeRoot,
        Description = Blank(Description),
        EditorDialog = Blank(EditorDialog),
        Properties = [.. Properties.Select(p => p.ToModel())],
        Style = [.. Style.Select(s => s.ToModel())],
        Children = [.. Children.Select(c => new ChildRule { FieldKey = c.FieldKey, Min = c.Min, Max = c.Max })]
    };

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
            EditorDialog = draft.EditorDialog
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

        /// <summary>Whether the row's details are shown in the builder.</summary>
        public bool Open { get; set; }

        public bool IsText => Kind is PropertyKind.Text or PropertyKind.RichText;
        public bool IsNumber => Kind is PropertyKind.Integer or PropertyKind.Decimal;

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
            OptionsText = string.Join("\n", p.Options ?? [])
        };

        /// <summary>A value made from a schema.org property, with the kind and limits the catalog suggests.</summary>
        public static PropertyRow From(SchemaOrgProperty p, string key) => new()
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
            OptionsText = string.Join("\n", p.Options ?? [])
        };

        /// <summary>The API's property; limits that do not fit the kind are dropped, not sent.</summary>
        public FieldProperty ToModel() => new()
        {
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
            Options = Kind == PropertyKind.Choice ? SplitList(OptionsText) : null
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

    /// <summary>A field this one may contain, and how many of it (no maximum: any number).</summary>
    public sealed class ChildRow
    {
        public string FieldKey { get; set; } = "";
        public int Min { get; set; }
        public int? Max { get; set; }
    }
}

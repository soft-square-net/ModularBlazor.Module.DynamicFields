using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// The kind of one content value: its JSON shape, its checks and the control an editor shows for it. Travels and is
/// stored by name, so a new kind never changes the meaning of an old one.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<PropertyKind>))]
public enum PropertyKind
{
    /// <summary>A line of plain text (JSON string).</summary>
    Text,

    /// <summary>Formatted text (JSON string holding the editor's markup; the consumer sanitizes it).</summary>
    RichText,

    /// <summary>A whole number (JSON integer).</summary>
    Integer,

    /// <summary>A number with decimals (JSON number).</summary>
    Decimal,

    /// <summary>Yes or no (JSON boolean).</summary>
    Boolean,

    /// <summary>A calendar date, <c>yyyy-MM-dd</c> (JSON string).</summary>
    Date,

    /// <summary>A date and time with its offset, ISO 8601 (JSON string).</summary>
    DateTime,

    /// <summary>An absolute http or https address (JSON string).</summary>
    Url,

    /// <summary>One of the property's <see cref="FieldProperty.Options"/> (JSON string).</summary>
    Choice,

    /// <summary>
    /// The id of a file (JSON string): a Storage file, or a media asset when a media module is installed; that module owns
    /// it. <see cref="FieldProperty.Accept"/> limits which files can be chosen (images, video, audio, documents).
    /// </summary>
    Asset,

    /// <summary>The id of a Keywords concept (JSON string); maps to schema.org about, mentions or keywords.</summary>
    Keyword,

    /// <summary>The id of a Taxonomies term (JSON string).</summary>
    Term,

    /// <summary>
    /// An item of another module's lookup, <see cref="FieldProperty.Lookup"/> (JSON object: <c>{ key, text, path? }</c>).
    /// The key is the value; the text is kept so the content shows without asking the owner.
    /// </summary>
    Lookup
}

/// <summary>
/// One value of a field's content. <see cref="Key"/> is the JSON property name and never changes once published (a
/// label can). <see cref="SchemaProperty"/> is the schema.org property the value is published as (headline, caption,
/// contentUrl, ...), which is what makes the content readable by search engines and AI. <see cref="Translatable"/>:
/// whether the value has a translation per language (Text and RichText only; null means yes for them). Say false for
/// text that is not language, such as a code or a slug. <see cref="Lookup"/>: what a Lookup property picks from (that
/// kind only). <see cref="Accept"/>: the files an Asset property takes (that kind only; null: any file), each a content
/// type (<c>image/png</c>), a family (<c>image/*</c>) or an extension (<c>.pdf</c>), as the file picker's accept list.
/// <see cref="Group"/>: the key of the <see cref="PropertyGroup"/> the value belongs to (a root field's only; null when the
/// field has no groups).
/// </summary>
public sealed record FieldProperty(
    string Key,
    string Label,
    PropertyKind Kind,
    bool Required = false,
    bool Multiple = false,
    string? SchemaProperty = null,
    string? HelpText = null,
    int? MinLength = null,
    int? MaxLength = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    IReadOnlyList<string>? Options = null,
    bool? Translatable = null,
    LookupBinding? Lookup = null,
    IReadOnlyList<string>? Accept = null,
    string? Group = null);

/// <summary>
/// A group of a root field's values, such as <c>main</c> (headline, body, images) and <c>seo</c> (summary, keywords), so
/// the values of one group can be edited, reviewed and routed apart: the SEO section gets the <c>seo</c> group. The first
/// group is the default: a value with no group belongs to it. Groups only sort values; content stores every value flat,
/// the same as without groups.
/// </summary>
public sealed record PropertyGroup(string Key, string Label, string? Description = null);

/// <summary>
/// One style value of a field: a choice from <see cref="Values"/> (theme tokens such as "wide", "md", "start"), never
/// free CSS. A <see cref="Responsive"/> one may also be given per breakpoint:
/// <c>{ "base": "full", "md": "half" }</c>.
/// </summary>
public sealed record StyleProperty(
    string Key,
    string Label,
    IReadOnlyList<string> Values,
    bool Responsive = false,
    string? Default = null);

/// <summary>A field the parent may contain as a child node, and how many of it (<see cref="Max"/> null: no limit).</summary>
public sealed record ChildRule(string FieldKey, int Min = 0, int? Max = null);

/// <summary>
/// What an author edits: everything about a field except its key. Publishing freezes it as a numbered
/// <see cref="FieldSnapshot"/>.
/// </summary>
/// <param name="SchemaType">The schema.org type of content made with this field (NewsArticle, ImageObject, ...).</param>
/// <param name="CanBeRoot">Whether a post can be made of this field (a root node, with no parent).</param>
/// <param name="EditorDialog">
/// Another module's dialog id that edits this field's content instead of the generic editor, e.g.
/// <c>Module.Media/edit-image</c>. It gets and returns <c>{ content, style }</c>.
/// </param>
/// <param name="Groups">A root field's value groups (<c>main</c>, <c>seo</c>), in order; null: none.</param>
/// <param name="UrlPrefix">
/// A root field's static URL: the public route prefix of posts of this kind, a lowercase slug path such as <c>news</c>
/// or <c>ar/news</c> (see <see cref="FieldRules.CheckUrlPrefix"/>). Required to publish a root field, unique among the
/// tenant's published root fields; not allowed on other fields.
/// </param>
public sealed record FieldDraft(
    string Title,
    string SchemaType,
    bool CanBeRoot,
    IReadOnlyList<FieldProperty> Properties,
    IReadOnlyList<StyleProperty> Style,
    IReadOnlyList<ChildRule> Children,
    string? Description = null,
    string? EditorDialog = null,
    IReadOnlyList<PropertyGroup>? Groups = null,
    string? UrlPrefix = null);

/// <summary>A child rule as published: the child field pinned to the version that was current then.</summary>
public sealed record PinnedChild(string FieldKey, int Version, int Min, int? Max);

/// <summary>
/// A published field version: immutable, so content made with it always validates the same way. Children are pinned
/// to exact versions; a newer child version reaches a parent only when the parent is published again.
/// <see cref="UrlPrefix"/> is a root field's static URL as published (null: not a root, or published before URL
/// prefixes existed; <see cref="FieldDefinition.PublishedUrlPrefix"/> holds the current one either way).
/// </summary>
public sealed record FieldSnapshot(
    string Key,
    int Version,
    string Title,
    string? Description,
    string SchemaType,
    bool CanBeRoot,
    IReadOnlyList<FieldProperty> Properties,
    IReadOnlyList<StyleProperty> Style,
    IReadOnlyList<PinnedChild> Children,
    string? EditorDialog,
    IReadOnlyList<PropertyGroup>? Groups = null,
    string? UrlPrefix = null)
{
    /// <summary>The values of one group, in the field's order (the first group also has the values with no group).</summary>
    public IReadOnlyList<FieldProperty> PropertiesIn(string group) =>
        Groups is not { Count: > 0 } groups ? []
        : [.. Properties.Where(p => (p.Group ?? groups[0].Key) == group)];
}

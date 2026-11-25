using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.JsonLd;

/// <summary>
/// What the caller knows about a file an Asset value names (this module only has its id): its public address and, for
/// an image or a video, its size in pixels. Storage gives them (the file's public URL, width and height).
/// </summary>
public sealed record JsonLdAsset(string Url, int? Width = null, int? Height = null, string? EncodingFormat = null);

/// <summary>
/// What the builder needs besides the content. <paramref name="Assets"/>: the files Asset values name, by id (a value
/// whose file is not given is left out). <paramref name="Names"/>: the names of the Keyword concepts and Taxonomy terms
/// the values name, by id (left out when not given). <paramref name="Url"/>: the post's canonical address
/// (<c>url</c> and <c>mainEntityOfPage</c>). <paramref name="Language"/>: its BCP 47 language (<c>inLanguage</c>, unless
/// a value gives it). <paramref name="Placeholders"/>: an empty or unknown value shows as <c>‹label›</c> instead of being
/// left out (the field builder's preview).
/// </summary>
public sealed record JsonLdOptions(
    IReadOnlyDictionary<string, JsonLdAsset>? Assets = null,
    IReadOnlyDictionary<string, string>? Names = null,
    string? Url = null,
    string? Language = null,
    bool Placeholders = false);

/// <summary>The JSON-LD of a post, and the nodes the builder could not read (their field version is not published).</summary>
public sealed record JsonLdDocument(JsonObject JsonLd, IReadOnlyList<ContentIssue> Skipped);

/// <summary>
/// A post as schema.org JSON-LD, from its kind (the root field) and each content's schema.org mapping. Pure: it reads
/// published field versions and what <see cref="JsonLdOptions"/> gives, and stores nothing.
/// <list type="bullet">
///   <item>The root's values go under their <see cref="FieldProperty.SchemaProperty"/>, converted by kind: an image file
///   is an ImageObject, a person a Person, rich text plain text, a keyword or term its name, a lookup item its text.</item>
///   <item>The contents, in reading order (the tree's order, depth first): an image (ImageObject) is added to
///   <c>image</c>, a video to <c>video</c>, an audio to <c>audio</c> (each only when its file is given); a gallery's
///   media to <c>associatedMedia</c>; a
///   quotation to <c>citation</c>; any other content's text (its <c>text</c> and <c>headline</c> values) to
///   <c>articleBody</c> (<c>text</c> when the kind is not an Article), paragraphs separated by a blank line.</item>
/// </list>
/// </summary>
public static partial class JsonLdBuilder
{
    public const string Context = "https://schema.org";

    /// <summary>schema.org properties whose file is an ImageObject (with its size), not a bare address.</summary>
    private static readonly HashSet<string> ImageProperties = new(StringComparer.Ordinal) { "image", "logo", "photo", "primaryImageOfPage" };

    private static readonly HashSet<string> PersonProperties = new(StringComparer.Ordinal)
        { "author", "creator", "contributor", "editor", "spokenByCharacter", "actor", "director", "illustrator", "translator" };

    private static readonly HashSet<string> OrganizationProperties = new(StringComparer.Ordinal) { "publisher", "copyrightHolder", "sourceOrganization" };

    private static readonly HashSet<string> ThingProperties = new(StringComparer.Ordinal) { "about", "mentions" };

    /// <summary>The values of a content that are its reading text.</summary>
    private static readonly HashSet<string> TextProperties = new(StringComparer.Ordinal) { "text", "headline", "articleBody" };

    /// <summary>The JSON-LD of a post whose root is made with a published field version (resolved like the children).</summary>
    public static JsonLdDocument Build(ContentNode root, Func<string, int, FieldSnapshot?> resolve, JsonLdOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var field = resolve(root.Field ?? "", root.FieldVersion)
            ?? throw new DomainException($"Field '{root.Field}' version {root.FieldVersion} is not published.");
        return Build(field, root, resolve, options);
    }

    /// <summary>
    /// The JSON-LD of a post with its root field given (a draft, for the builder's preview); children are resolved by
    /// <paramref name="resolve"/>.
    /// </summary>
    public static JsonLdDocument Build(FieldSnapshot rootField, ContentNode root, Func<string, int, FieldSnapshot?> resolve, JsonLdOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(rootField);
        ArgumentNullException.ThrowIfNull(root);
        options ??= new JsonLdOptions();
        var walk = new Walk(resolve, options);

        var ld = new JsonObject { ["@context"] = Context, ["@type"] = rootField.SchemaType };
        if (options.Url is { Length: > 0 } url)
        {
            ld["url"] = url;
            ld["mainEntityOfPage"] = new JsonObject { ["@type"] = "WebPage", ["@id"] = url };
        }
        walk.Values(ld, rootField, root.Content);
        if (options.Language is { Length: > 0 } language && ld["inLanguage"] is null) ld["inLanguage"] = language;

        var body = new List<string>();
        walk.Contents(ld, root.Children ?? [], "", depth: 2, body);
        if (body.Count > 0)
        {
            var name = IsArticle(rootField.SchemaType) ? "articleBody" : "text";
            if (ld[name] is JsonValue own && own.TryGetValue<string>(out var lead) && lead.Length > 0) body.Insert(0, lead);
            ld[name] = string.Join("\n\n", body);
        }
        return new JsonLdDocument(ld, walk.Skipped);
    }

    /// <summary>Whether a type is an Article (NewsArticle, BlogPosting, ...), which has <c>articleBody</c>.</summary>
    public static bool IsArticle(string schemaType) => Is(schemaType, "Article");

    private static bool Is(string schemaType, string ancestor) =>
        schemaType == ancestor || SchemaOrgCatalog.PathOf(schemaType).Contains(ancestor);

    private enum ContentRole { Image, Video, Audio, Gallery, Quote, Text }

    private static ContentRole RoleOf(string schemaType) =>
        Is(schemaType, "ImageObject") ? ContentRole.Image
        : Is(schemaType, "VideoObject") ? ContentRole.Video
        : Is(schemaType, "AudioObject") ? ContentRole.Audio
        : Is(schemaType, "MediaGallery") ? ContentRole.Gallery
        : Is(schemaType, "Quotation") ? ContentRole.Quote
        : ContentRole.Text;

    /// <summary>One build: the resolver, the options and the nodes skipped so far.</summary>
    private sealed class Walk(Func<string, int, FieldSnapshot?> resolve, JsonLdOptions options)
    {
        private int _nodes = 1;

        public List<ContentIssue> Skipped { get; } = [];

        public void Contents(JsonObject ld, IReadOnlyList<ContentNode> children, string path, int depth, List<string> body)
        {
            if (depth > ContentTreeValidator.MaxDepth) return;
            for (var i = 0; i < children.Count; i++)
            {
                if (children[i] is not { } child || ++_nodes > ContentTreeValidator.MaxNodes) continue;
                var at = $"{path}/children/{i}";
                if (resolve(child.Field ?? "", child.FieldVersion) is not { } field)
                {
                    Skipped.Add(new($"{at}/field", $"Field '{child.Field}' version {child.FieldVersion} is not published."));
                    continue;
                }
                switch (RoleOf(field.SchemaType))
                {
                    case ContentRole.Image: AddMedia(ld, "image", field, child); break;
                    case ContentRole.Video: AddMedia(ld, "video", field, child); break;
                    case ContentRole.Audio: AddMedia(ld, "audio", field, child); break;
                    case ContentRole.Gallery:
                        GalleryMedia(ld, child, at, depth + 1);
                        break;
                    case ContentRole.Quote:
                        var quote = new JsonObject { ["@type"] = field.SchemaType };
                        Values(quote, field, child.Content);
                        Add(ld, "citation", quote);
                        break;
                    default:
                        foreach (var p in field.Properties.Where(p => p.SchemaProperty is { } sp && TextProperties.Contains(sp)))
                            foreach (var text in Texts(child.Content, p))
                                body.Add(text);
                        Contents(ld, child.Children ?? [], at, depth + 1, body);
                        break;
                }
            }
        }

        /// <summary>A gallery's images and videos, each to <c>associatedMedia</c>.</summary>
        private void GalleryMedia(JsonObject ld, ContentNode gallery, string path, int depth)
        {
            if (depth > ContentTreeValidator.MaxDepth) return;
            var items = gallery.Children ?? [];
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is not { } item || ++_nodes > ContentTreeValidator.MaxNodes) continue;
                if (resolve(item.Field ?? "", item.FieldVersion) is not { } field)
                {
                    Skipped.Add(new($"{path}/children/{i}/field", $"Field '{item.Field}' version {item.FieldVersion} is not published."));
                    continue;
                }
                if (RoleOf(field.SchemaType) is ContentRole.Image or ContentRole.Video or ContentRole.Audio)
                    AddMedia(ld, "associatedMedia", field, item);
            }
        }

        /// <summary>
        /// A media content as its own object (its values, and its file's address and size) added under
        /// <paramref name="name"/>; left out when its file is not known, since a media object without its file says
        /// nothing a reader can use.
        /// </summary>
        private void AddMedia(JsonObject ld, string name, FieldSnapshot field, ContentNode node)
        {
            var media = new JsonObject { ["@type"] = field.SchemaType };
            Values(media, field, node.Content, contentFile: true);
            if (media["contentUrl"] is null && !options.Placeholders) return;
            Add(ld, name, media);
        }

        /// <summary>Every mapped value of <paramref name="field"/> in <paramref name="content"/>, under its schema.org name.</summary>
        public void Values(JsonObject target, FieldSnapshot field, JsonElement content, bool contentFile = false)
        {
            foreach (var p in field.Properties)
            {
                if (p.SchemaProperty is not { } name) continue;
                var value = content.ValueKind == JsonValueKind.Object && content.TryGetProperty(p.Key, out var v) ? v : default;

                if (contentFile && name == "contentUrl" && p.Kind == PropertyKind.Asset)
                {
                    ContentFile(target, p, value);
                    continue;
                }

                List<JsonNode> items = value.ValueKind switch
                {
                    JsonValueKind.Array => [.. value.EnumerateArray().Select(i => Convert(p, name, i)).OfType<JsonNode>()],
                    JsonValueKind.Undefined or JsonValueKind.Null => [],
                    _ => Convert(p, name, value) is { } one ? [one] : []
                };
                if (items.Count == 0)
                {
                    if (options.Placeholders) Add(target, name, Placeholder(p));
                    continue;
                }
                if (target[name] is not null) foreach (var item in items) Add(target, name, item);
                else target[name] = p.Multiple ? new JsonArray([.. items]) : items[0];
            }
        }

        /// <summary>A media content's own file: contentUrl, and for an image or a video its url, width and height.</summary>
        private void ContentFile(JsonObject media, FieldProperty p, JsonElement value)
        {
            var id = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
            if (id is null || options.Assets?.GetValueOrDefault(id) is not { } asset)
            {
                if (options.Placeholders) media["contentUrl"] = Placeholder(p, id);
                return;
            }
            media["contentUrl"] = asset.Url;
            if (media["@type"]?.GetValue<string>() is { } type && Is(type, "ImageObject")) media["url"] = asset.Url;
            if (asset.Width is { } width) media["width"] = width;
            if (asset.Height is { } height) media["height"] = height;
            if (asset.EncodingFormat is { Length: > 0 } format) media["encodingFormat"] = format;
        }

        /// <summary>One value as schema.org expects it under <paramref name="name"/>; null when there is nothing to say.</summary>
        private JsonNode? Convert(FieldProperty p, string name, JsonElement value)
        {
            switch (p.Kind)
            {
                case PropertyKind.Asset:
                    var id = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    if (id is null || options.Assets?.GetValueOrDefault(id) is not { } asset)
                        return options.Placeholders ? Placeholder(p, id) : null;
                    if (!ImageProperties.Contains(name)) return asset.Url;
                    var image = new JsonObject { ["@type"] = "ImageObject", ["url"] = asset.Url };
                    if (asset.Width is { } width) image["width"] = width;
                    if (asset.Height is { } height) image["height"] = height;
                    return image;

                case PropertyKind.Keyword or PropertyKind.Term:
                    var key = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                    if (key is null || options.Names?.GetValueOrDefault(key) is not { Length: > 0 } termName)
                        return options.Placeholders ? Placeholder(p, key) : null;
                    return ThingProperties.Contains(name) ? new JsonObject { ["@type"] = "Thing", ["name"] = termName } : termName;

                case PropertyKind.Lookup:
                    if (value.ValueKind != JsonValueKind.Object) return null;
                    var text = value.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString()
                        : value.TryGetProperty("key", out var k) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
                    return Named(name, text);

                case PropertyKind.RichText:
                    return value.ValueKind == JsonValueKind.String && PlainText(value.GetString()!) is { Length: > 0 } plain ? plain : null;

                case PropertyKind.Text:
                    return value.ValueKind == JsonValueKind.String ? Named(name, value.GetString()!.Trim()) : null;

                default:
                    return JsonNode.Parse(value.GetRawText());
            }
        }

        private static JsonNode? Named(string name, string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (PersonProperties.Contains(name)) return new JsonObject { ["@type"] = "Person", ["name"] = text };
            if (OrganizationProperties.Contains(name)) return new JsonObject { ["@type"] = "Organization", ["name"] = text };
            return text;
        }

        private IEnumerable<string> Texts(JsonElement content, FieldProperty p)
        {
            if (content.ValueKind != JsonValueKind.Object || !content.TryGetProperty(p.Key, out var value)) yield break;
            List<JsonElement> values = value.ValueKind == JsonValueKind.Array ? [.. value.EnumerateArray()] : [value];
            foreach (var v in values)
                if (v.ValueKind == JsonValueKind.String)
                {
                    var text = p.Kind == PropertyKind.RichText ? PlainText(v.GetString()!) : v.GetString()!.Trim();
                    if (text.Length > 0) yield return text;
                }
        }

        private static JsonNode Placeholder(FieldProperty p, string? id = null) =>
            JsonValue.Create(id is { Length: > 0 } ? $"‹{p.Label}: {id}›" : $"‹{p.Label}›");
    }

    /// <summary>Adds a value under a name: the first as it is, the next ones turning it into a list.</summary>
    private static void Add(JsonObject target, string name, JsonNode value)
    {
        switch (target[name])
        {
            case null: target[name] = value; break;
            case JsonArray list: list.Add(value); break;
            case { } single:
                target.Remove(name);
                target[name] = new JsonArray(single, value);
                break;
        }
    }

    /// <summary>
    /// Rich text as plain text: line breaks for paragraphs and breaks, tags removed, entities decoded, spaces collapsed.
    /// The consumer sanitizes the markup; this only reads it.
    /// </summary>
    public static string PlainText(string html)
    {
        var text = BlockEnd().Replace(html, "\n");
        text = Tag().Replace(text, "");
        text = WebUtility.HtmlDecode(text);
        var lines = text.Split('\n').Select(l => Spaces().Replace(l, " ").Trim()).Where(l => l.Length > 0);
        return string.Join("\n", lines);
    }

    [GeneratedRegex(@"<br\s*/?>|</(?:p|div|li|h[1-6]|blockquote|tr)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockEnd();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

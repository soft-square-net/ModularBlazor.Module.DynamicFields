using static soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.SchemaOrgCatalog;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>A field of the starter library: its key and the draft it starts with.</summary>
public sealed record StarterField(string Key, FieldDraft Draft);

/// <summary>
/// The starter library: ready-made blocks (heading, text, paragraph, quote, image, video, audio, gallery) and posts
/// (article, news, review, blog post, video, reportage, photo story, other) built on the schema.org catalog, with the
/// values search engines ask for marked required. Each post has its static URL (<see cref="FieldDraft.UrlPrefix"/>):
/// <c>articles</c>, <c>news</c>, <c>reviews</c>, <c>blog</c>, <c>videos</c>, <c>reportage</c>, <c>photo-stories</c>,
/// <c>posts</c>. A tenant installs them on demand; they then are ordinary fields it can change and publish
/// again. Listed children first, so installing in this order can publish each field right away.
/// </summary>
public static class StarterFields
{
    private static FieldProperty P(string schemaProperty, bool required = false) => ToFieldProperty(schemaProperty, required);

    /// <summary>A value keyed differently from its schema.org property (an image block's "image" is its contentUrl).</summary>
    private static FieldProperty P(string key, string schemaProperty, string label, bool required = false, string? help = null, int? maxLength = null) =>
        ToFieldProperty(schemaProperty, required) with
        {
            Key = key,
            Label = label,
            HelpText = help ?? Properties[schemaProperty].Description,
            MaxLength = maxLength ?? Properties[schemaProperty].MaxLength
        };

    private static readonly string[] Blocks =
        ["block.heading", "block.text", "block.paragraph", "block.quote", "block.image", "block.video", "block.audio", "block.gallery"];

    private static IReadOnlyList<ChildRule> AnyBlock => [.. Blocks.Select(b => new ChildRule(b))];

    /// <summary>
    /// A post's value groups: the story itself, and what the SEO section looks after (routed to it apart). Values without
    /// a group are in the first, <c>main</c>.
    /// </summary>
    private static IReadOnlyList<PropertyGroup> PostGroups { get; } =
    [
        new("main", "Story", "The headline, the main image, who wrote it and when."),
        new("seo", "Search and sharing", "The summary and keywords search engines and link previews show.")
    ];

    private static IReadOnlyList<FieldProperty> PostValues(params FieldProperty[] extra) =>
    [
        P("headline", required: true),
        P("description", "description", "Summary", required: true,
          help: "One or two sentences; search results and link previews show it.", maxLength: 300) with { Group = "seo" },
        P("image", "image", "Main image", required: true, help: "Shown in previews and search results; at least 1200 pixels wide."),
        P("author", required: true),
        P("datePublished", required: true),
        P("dateModified"),
        P("section", "articleSection", "Section"),
        P("keywords") with { Group = "seo" },
        P("about"),
        .. extra
    ];

    /// <summary>
    /// A video post's values. They keep the keys every post has, published as what a VideoObject calls them: the headline
    /// is its name, the main image its thumbnail, the publishing date its upload date (Google requires all three) and the
    /// section its genre. The video is a file or a player address; neither is required, as authors have one or the other.
    /// </summary>
    private static IReadOnlyList<FieldProperty> VideoValues() =>
    [
        .. PostValues().Select(p => p.Key switch
        {
            "headline" => p with { SchemaProperty = "name" },
            "image" => p with { SchemaProperty = "thumbnailUrl", Label = "Thumbnail",
                HelpText = "Shown in previews and search results; Google requires it for videos." },
            "datePublished" => p with { SchemaProperty = "uploadDate",
                HelpText = "When the video was first published; Google requires it." },
            "section" => p with { SchemaProperty = "genre" },
            _ => p
        }),
        P("video", "contentUrl", "Video", help: "The video file.") with { Accept = AcceptFor("VideoObject", "contentUrl") },
        P("embedUrl"),
        P("duration"),
        P("transcript")
    ];

    public static IReadOnlyList<StarterField> All { get; } =
    [
        new("block.heading", new FieldDraft("Heading", "WebPageElement", false,
            [P("text", "headline", "Heading", required: true, help: "A short title for the part that follows.", maxLength: 200)],
            [StylePresets.Level, StylePresets.Align], [],
            "A heading that starts a part of a post.")),

        new("block.text", new FieldDraft("Text", "WebPageElement", false,
            [P("text", "text", "Text", required: true, help: "One line of plain text.", maxLength: 500) with { Kind = PropertyKind.Text }],
            [StylePresets.Align, StylePresets.Size, StylePresets.Tone], [],
            "A line of plain text: a kicker, a note, a call to action.")),

        new("block.paragraph", new FieldDraft("Paragraph", "WebPageElement", false,
            [P("text", "text", "Paragraph", required: true, help: "Formatted text.")],
            [StylePresets.Align, StylePresets.Size, StylePresets.Tone, StylePresets.Spacing], [],
            "Formatted text: the body of a post, a paragraph at a time.")),

        new("block.quote", new FieldDraft("Quote", "Quotation", false,
            [P("text", "text", "Quotation", required: true, help: "The words, without quotation marks.", maxLength: 1000) with { Kind = PropertyKind.Text },
             P("saidBy", "spokenByCharacter", "Said by"),
             P("source", "citation", "Source", help: "Where it was said or written.") with { Multiple = false }],
            [StylePresets.Align, StylePresets.Tone], [],
            "A quotation and who said it.")),

        new("block.image", new FieldDraft("Image", "ImageObject", false,
            [P("image", "contentUrl", "Image", required: true, help: "The image file.") with { Accept = AcceptFor("ImageObject", "contentUrl") },
             P("alt", "description", "Alternative text", required: true,
               help: "What the image shows, for people who cannot see it and for search engines.", maxLength: 250),
             P("caption", "caption", "Caption", maxLength: 300),
             P("credit", "creditText", "Credit")],
            [StylePresets.Width, StylePresets.Aspect, StylePresets.Align], [],
            "An image with its alternative text, caption and credit.")),

        new("block.video", new FieldDraft("Video", "VideoObject", false,
            [P("video", "contentUrl", "Video", required: true, help: "The video file.") with { Accept = AcceptFor("VideoObject", "contentUrl") },
             P("title", "name", "Title", required: true, help: "The video's title.", maxLength: 110),
             P("description", "description", "Description", required: true, maxLength: 300),
             P("thumbnail", "thumbnailUrl", "Thumbnail", required: true, help: "Search engines show it; Google requires it."),
             P("uploadDate", required: true),
             P("duration"),
             P("transcript")],
            [StylePresets.Width, StylePresets.Aspect], [],
            "A video with what search engines need to show it.")),

        new("block.audio", new FieldDraft("Audio", "AudioObject", false,
            [P("audio", "contentUrl", "Audio", required: true, help: "The audio file.") with { Accept = AcceptFor("AudioObject", "contentUrl") },
             P("title", "name", "Title", required: true, help: "The recording's title.", maxLength: 110),
             P("description", "description", "Description", maxLength: 300),
             P("duration"),
             P("transcript")],
            [StylePresets.Width], [],
            "An audio recording with its transcript.")),

        new("block.gallery", new FieldDraft("Gallery", "ImageGallery", false,
            [P("title", "name", "Title", help: "Shown above the gallery."),
             P("description", "description", "Description", maxLength: 300)],
            [StylePresets.Columns, StylePresets.Spacing],
            [new ChildRule("block.image", 1, 100)],
            "A gallery of images.")),

        new("post.article", new FieldDraft("Article", "Article", true,
            PostValues(), [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A general article, made of blocks.", Groups: PostGroups, UrlPrefix: "articles")),

        new("post.news", new FieldDraft("News", "NewsArticle", true,
            PostValues(P("dateline"), P("isAccessibleForFree", "isAccessibleForFree", "Free to read")),
            [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A news story, with its dateline.", Groups: PostGroups, UrlPrefix: "news")),

        new("post.review", new FieldDraft("Review", "Review", true,
            PostValues(P("itemReviewed", required: true), P("reviewRating")),
            [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A review of a film, a book, a product or a place, with its rating.", Groups: PostGroups, UrlPrefix: "reviews")),

        new("post.blog", new FieldDraft("Blog post", "BlogPosting", true,
            PostValues(), [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A blog post, made of blocks.", Groups: PostGroups, UrlPrefix: "blog")),

        new("post.video", new FieldDraft("Video", "VideoObject", true,
            VideoValues(), [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A video as a post of its own, with what search engines need to show it.", Groups: PostGroups,
            UrlPrefix: "videos")),

        new("post.reportage", new FieldDraft("Reportage", "ReportageNewsArticle", true,
            PostValues(P("dateline"), P("isAccessibleForFree", "isAccessibleForFree", "Free to read")),
            [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "Factual reporting, from the scene or from sources.", Groups: PostGroups, UrlPrefix: "reportage")),

        new("post.photo-story", new FieldDraft("Photo story", "Article", true,
            PostValues(), [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A story told mainly in pictures, with image galleries among its blocks.", Groups: PostGroups,
            UrlPrefix: "photo-stories")),

        new("post.other", new FieldDraft("Other", "Article", true,
            PostValues(), [StylePresets.Layout, StylePresets.Tone], AnyBlock,
            "A post of no other kind, made of blocks.", Groups: PostGroups, UrlPrefix: "posts")),
    ];

    /// <summary>A starter field by key, or null.</summary>
    public static StarterField? Find(string key) => All.FirstOrDefault(f => f.Key == key);

    /// <summary>The keys with every child they need, in install order (children first).</summary>
    public static IReadOnlyList<string> WithChildren(IEnumerable<string> keys)
    {
        var wanted = new HashSet<string>(StringComparer.Ordinal);
        var queue = new Queue<string>(keys);
        while (queue.TryDequeue(out var key))
        {
            if (!wanted.Add(key) || Find(key) is not { } field) continue;
            foreach (var child in field.Draft.Children) queue.Enqueue(child.FieldKey);
        }
        return [.. All.Select(f => f.Key).Where(wanted.Contains)];
    }
}

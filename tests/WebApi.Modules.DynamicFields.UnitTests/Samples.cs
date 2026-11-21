using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests;

/// <summary>A news post made of paragraphs and images: the fields, and content made with them.</summary>
public static class Samples
{
    public static readonly DateTimeOffset Now = TestClock.Start;

    public static FieldDraft Paragraph() => new(
        "Paragraph", "WebPageElement", CanBeRoot: false,
        [new FieldProperty("text", "Text", PropertyKind.RichText, Required: true, SchemaProperty: "text", MaxLength: 5000)],
        [new StyleProperty("align", "Alignment", ["start", "center", "end"], Default: "start")],
        []);

    public static FieldDraft Image() => new(
        "Image", "ImageObject", CanBeRoot: false,
        [
            new FieldProperty("asset", "Image", PropertyKind.Asset, Required: true, SchemaProperty: "contentUrl"),
            new FieldProperty("caption", "Caption", PropertyKind.Text, SchemaProperty: "caption", MaxLength: 300),
            new FieldProperty("credit", "Credit", PropertyKind.Text, SchemaProperty: "creditText", MaxLength: 120)
        ],
        [new StyleProperty("width", "Width", ["full", "half", "third"], Responsive: true, Default: "full")],
        []);

    public static FieldDraft NewsPost() => new(
        "News post", "NewsArticle", CanBeRoot: true,
        [
            new FieldProperty("headline", "Headline", PropertyKind.Text, Required: true, SchemaProperty: "headline", MaxLength: 110),
            new FieldProperty("summary", "Summary", PropertyKind.Text, SchemaProperty: "description", MaxLength: 300),
            new FieldProperty("topics", "Topics", PropertyKind.Keyword, Multiple: true, SchemaProperty: "about"),
            new FieldProperty("section", "Section", PropertyKind.Choice, Options: ["news", "sports", "culture"])
        ],
        [new StyleProperty("layout", "Layout", ["standard", "wide"], Default: "standard")],
        [new ChildRule("block.paragraph", Min: 1), new ChildRule("block.image", Max: 10)]);

    /// <summary>The three fields published at version 1, as the validator resolves them.</summary>
    public static Func<string, int, FieldSnapshot?> Published()
    {
        var paragraph = Publish("block.paragraph", Paragraph(), new Dictionary<string, int>());
        var image = Publish("block.image", Image(), new Dictionary<string, int>());
        var post = Publish("post.news", NewsPost(), new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 });
        var all = new[] { paragraph, image, post }.ToDictionary(s => (s.Key, s.Version));
        return (key, version) => all.GetValueOrDefault((key, version));
    }

    public static FieldSnapshot Publish(string key, FieldDraft draft, IReadOnlyDictionary<string, int> children) =>
        FieldDefinition.Create(key, draft, "author-1", Now).Publish(children, "author-1", Now).Snapshot;

    public static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    public static ContentNode Node(string field, string content, string style = "{}", params ContentNode[] children) =>
        new(field, 1, Json(content), Json(style), children);

    /// <summary>A valid post: a headline, one paragraph and one image at half width from md up.</summary>
    public static ContentNode Post(params ContentNode[] children) =>
        Node("post.news", """{ "headline": "Final day at the venue", "topics": ["kw_17"], "section": "sports" }""", """{ "layout": "wide" }""",
            children.Length > 0
                ? children
                : [
                    Node("block.paragraph", """{ "text": "<p>Opening text</p>" }"""),
                    Node("block.image", """{ "asset": "asset_21", "caption": "The venue" }""", """{ "width": { "base": "full", "md": "half" } }""")
                ]);
}

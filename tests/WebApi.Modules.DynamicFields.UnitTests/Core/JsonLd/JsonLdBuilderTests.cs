using System.Text.Json.Nodes;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.JsonLd;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.JsonLd;

public class JsonLdBuilderTests
{
    /// <summary>The starter library, every field published at version 1.</summary>
    private static readonly Func<string, int, FieldSnapshot?> Starter = PublishStarter();

    private static Func<string, int, FieldSnapshot?> PublishStarter()
    {
        var all = StarterFields.All
            .Select(f => Publish(f.Key, f.Draft, f.Draft.Children.ToDictionary(c => c.FieldKey, _ => 1)))
            .ToDictionary(s => (s.Key, s.Version));
        return (key, version) => all.GetValueOrDefault((key, version));
    }

    private static readonly JsonLdOptions Known = new(
        Assets: new Dictionary<string, JsonLdAsset>
        {
            ["main"] = new("https://cdn.example/main.jpg", 1600, 900),
            ["pic"] = new("https://cdn.example/pic.webp", 800, 600, "image/webp"),
            ["g1"] = new("https://cdn.example/g1.jpg", 640, 480),
            ["g2"] = new("https://cdn.example/g2.jpg"),
            ["clip"] = new("https://cdn.example/clip.mp4", 1920, 1080, "video/mp4"),
            ["thumb"] = new("https://cdn.example/clip.jpg", 1280, 720)
        },
        Names: new Dictionary<string, string> { ["kw1"] = "Elections", ["kw2"] = "Cairo", ["sec"] = "Politics" },
        Url: "https://news.example/2026/11/final-day",
        Language: "en");

    private static ContentNode Story() =>
        Node("post.news",
            """
            { "headline": "Final day at the venue", "description": "Voting closes tonight.", "image": "main",
              "author": ["Sam Writer"], "datePublished": "2026-11-25T09:00:00+02:00", "section": "sec",
              "keywords": ["kw1", "kw2"], "about": ["kw2"], "dateline": "CAIRO" }
            """, "{}",
            Node("block.heading", """{ "text": "Queues since dawn" }"""),
            Node("block.paragraph", """{ "text": "<p>Polls opened at <b>8</b>.</p><p>Turnout &amp; queues grew.</p>" }"""),
            Node("block.image", """{ "image": "pic", "alt": "The queue", "caption": "Voters wait", "credit": "Photo: Ana" }"""),
            Node("block.quote", """{ "text": "We waited three hours.", "saidBy": "A voter" }"""),
            Node("block.gallery", """{ "title": "The day in pictures" }""", "{}",
                Node("block.image", """{ "image": "g1", "alt": "One" }"""),
                Node("block.image", """{ "image": "g2", "alt": "Two" }""")),
            Node("block.video", """{ "video": "clip", "title": "Closing", "description": "The doors close.", "thumbnail": "thumb", "uploadDate": "2026-11-25T20:00:00+02:00" }"""),
            Node("block.text", """{ "text": "Results tomorrow." }"""));

    private static JsonObject Build(ContentNode root, JsonLdOptions? options = null) => JsonLdBuilder.Build(root, Starter, options ?? Known).JsonLd;

    [Fact]
    public void TheRootValuesGoUnderTheirSchemaOrgProperties()
    {
        var ld = Build(Story());

        ld["@context"]!.GetValue<string>().ShouldBe("https://schema.org");
        ld["@type"]!.GetValue<string>().ShouldBe("NewsArticle");
        ld["headline"]!.GetValue<string>().ShouldBe("Final day at the venue");
        ld["description"]!.GetValue<string>().ShouldBe("Voting closes tonight.");
        ld["datePublished"]!.GetValue<string>().ShouldBe("2026-11-25T09:00:00+02:00");
        ld["dateline"]!.GetValue<string>().ShouldBe("CAIRO");
        ld["url"]!.GetValue<string>().ShouldBe("https://news.example/2026/11/final-day");
        ld["mainEntityOfPage"]!["@id"]!.GetValue<string>().ShouldBe("https://news.example/2026/11/final-day");
        ld["inLanguage"]!.GetValue<string>().ShouldBe("en");
    }

    [Fact]
    public void PeopleKeywordsAndTermsAreNamed()
    {
        var ld = Build(Story());

        ld["author"]![0]!["@type"]!.GetValue<string>().ShouldBe("Person");
        ld["author"]![0]!["name"]!.GetValue<string>().ShouldBe("Sam Writer");
        ld["keywords"]!.AsArray().Select(k => k!.GetValue<string>()).ShouldBe(["Elections", "Cairo"]);
        ld["about"]![0]!["@type"]!.GetValue<string>().ShouldBe("Thing");
        ld["about"]![0]!["name"]!.GetValue<string>().ShouldBe("Cairo");
        ld["articleSection"]!.GetValue<string>().ShouldBe("Politics");
    }

    [Fact]
    public void ImagesAreImageObjectsWithTheirSize()
    {
        var images = Build(Story())["image"]!.AsArray();

        images.Count.ShouldBe(2);
        images[0]!["@type"]!.GetValue<string>().ShouldBe("ImageObject");
        images[0]!["url"]!.GetValue<string>().ShouldBe("https://cdn.example/main.jpg");
        images[0]!["width"]!.GetValue<int>().ShouldBe(1600);
        images[0]!["height"]!.GetValue<int>().ShouldBe(900);

        var pic = images[1]!;
        pic["@type"]!.GetValue<string>().ShouldBe("ImageObject");
        pic["contentUrl"]!.GetValue<string>().ShouldBe("https://cdn.example/pic.webp");
        pic["url"]!.GetValue<string>().ShouldBe("https://cdn.example/pic.webp");
        pic["width"]!.GetValue<int>().ShouldBe(800);
        pic["height"]!.GetValue<int>().ShouldBe(600);
        pic["encodingFormat"]!.GetValue<string>().ShouldBe("image/webp");
        pic["caption"]!.GetValue<string>().ShouldBe("Voters wait");
        pic["description"]!.GetValue<string>().ShouldBe("The queue");
        pic["creditText"]!.GetValue<string>().ShouldBe("Photo: Ana");
    }

    [Fact]
    public void AVideoGoesToVideo()
    {
        var video = Build(Story())["video"]!;

        video["@type"]!.GetValue<string>().ShouldBe("VideoObject");
        video["contentUrl"]!.GetValue<string>().ShouldBe("https://cdn.example/clip.mp4");
        video["url"].ShouldBeNull();
        video["name"]!.GetValue<string>().ShouldBe("Closing");
        video["thumbnailUrl"]!.GetValue<string>().ShouldBe("https://cdn.example/clip.jpg");
        video["width"]!.GetValue<int>().ShouldBe(1920);
    }

    [Fact]
    public void AGallerysMediaGoToAssociatedMedia()
    {
        var media = Build(Story())["associatedMedia"]!.AsArray();

        media.Select(m => m!["contentUrl"]!.GetValue<string>()).ShouldBe(["https://cdn.example/g1.jpg", "https://cdn.example/g2.jpg"]);
        media[0]!["width"]!.GetValue<int>().ShouldBe(640);
        media[1]!["width"].ShouldBeNull();
    }

    [Fact]
    public void AQuoteIsACitation()
    {
        var quote = Build(Story())["citation"]!;

        quote["@type"]!.GetValue<string>().ShouldBe("Quotation");
        quote["text"]!.GetValue<string>().ShouldBe("We waited three hours.");
        quote["spokenByCharacter"]!["@type"]!.GetValue<string>().ShouldBe("Person");
        quote["spokenByCharacter"]!["name"]!.GetValue<string>().ShouldBe("A voter");
    }

    [Fact]
    public void TheTextIsTheArticleBodyInReadingOrder() =>
        Build(Story())["articleBody"]!.GetValue<string>()
            .ShouldBe("Queues since dawn\n\nPolls opened at 8.\nTurnout & queues grew.\n\nResults tomorrow.");

    [Fact]
    public void AFileOrNameNotGivenIsLeftOut()
    {
        var ld = Build(Story(), new JsonLdOptions());

        ld["image"].ShouldBeNull(); // neither the main image nor the block's file are known
        ld["video"].ShouldBeNull();
        ld["associatedMedia"].ShouldBeNull();
        ld["keywords"].ShouldBeNull();
        ld["url"].ShouldBeNull();
        ld["citation"].ShouldNotBeNull(); // no file needed
    }

    [Fact]
    public void PlaceholdersMarkWhatIsEmpty()
    {
        var ld = Build(Node("post.news", """{ "headline": "Only a headline" }""", "{}", Node("block.paragraph", """{ "text": "x" }""")),
            new JsonLdOptions(Placeholders: true));

        ld["headline"]!.GetValue<string>().ShouldBe("Only a headline");
        ld["description"]!.GetValue<string>().ShouldBe("‹Summary›");
        ld["image"]!.GetValue<string>().ShouldBe("‹Main image›");
    }

    [Fact]
    public void ANodeWhoseFieldIsNotPublishedIsSkippedAndReported()
    {
        var root = Node("post.news", """{ "headline": "H" }""", "{}",
            Node("block.paragraph", """{ "text": "kept" }"""),
            new ContentNode("block.paragraph", 9, Json("""{ "text": "gone" }"""), Json("{}")));

        var document = JsonLdBuilder.Build(root, Starter);

        document.JsonLd["articleBody"]!.GetValue<string>().ShouldBe("kept");
        document.Skipped.ShouldHaveSingleItem().Path.ShouldBe("/children/1/field");
    }

    [Fact]
    public void AnUnpublishedRootIsRefused() =>
        Should.Throw<DomainException>(() => JsonLdBuilder.Build(Story() with { FieldVersion = 5 }, Starter));

    [Fact]
    public void AKindThatIsNotAnArticleGetsText()
    {
        var page = Publish("page.about", new FieldDraft("About", "WebPage", true,
            [new FieldProperty("name", "Name", PropertyKind.Text, SchemaProperty: "name")], [], [new ChildRule("block.paragraph")]),
            new Dictionary<string, int> { ["block.paragraph"] = 1 });
        FieldSnapshot? Resolve(string key, int version) => key == "page.about" ? page : Starter(key, version);

        var ld = JsonLdBuilder.Build(Node("page.about", """{ "name": "Us" }""", "{}", Node("block.paragraph", """{ "text": "<p>Hello</p>" }""")),
            Resolve).JsonLd;

        ld["articleBody"].ShouldBeNull();
        ld["text"]!.GetValue<string>().ShouldBe("Hello");
    }

    [Theory]
    [InlineData("<p>One</p><p>Two</p>", "One\nTwo")]
    [InlineData("Line<br/>break", "Line\nbreak")]
    [InlineData("  a   &lt;b&gt;  ", "a <b>")]
    public void RichTextReadsAsPlainText(string html, string text) => JsonLdBuilder.PlainText(html).ShouldBe(text);
}

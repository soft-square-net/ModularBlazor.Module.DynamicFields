using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.Core.JsonLd;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.JsonLd;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.UseCases.Content;

public class JsonLdHandlers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IReadRepository<FieldDefinition> _read = Substitute.For<IReadRepository<FieldDefinition>>();

    public JsonLdHandlers()
    {
        var paragraph = FieldDefinition.Create("block.paragraph", Paragraph(), "author-1", Now);
        paragraph.Publish(new Dictionary<string, int>(), "author-1", Now);
        var image = FieldDefinition.Create("block.image", Image(), "author-1", Now);
        image.Publish(new Dictionary<string, int>(), "author-1", Now);
        var post = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        post.Publish(new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 }, "author-1", Now);
        _read.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([paragraph, image, post]);
    }

    [Fact]
    public async Task BuildsThePostsJsonLd()
    {
        var assets = new Dictionary<string, JsonLdAsset> { ["asset_21"] = new("https://cdn.example/venue.jpg", 1200, 800) };

        var result = await new BuildJsonLdHandler(_read).Handle(new BuildJsonLdQuery(Post(), assets), Ct);

        result.IsSuccess.ShouldBeTrue();
        var ld = result.Value.JsonLd;
        ld.GetProperty("@type").GetString().ShouldBe("NewsArticle");
        ld.GetProperty("headline").GetString().ShouldBe("Final day at the venue");
        ld.GetProperty("articleBody").GetString().ShouldBe("Opening text");
        ld.GetProperty("image").GetProperty("contentUrl").GetString().ShouldBe("https://cdn.example/venue.jpg");
        ld.GetProperty("image").GetProperty("width").GetInt32().ShouldBe(1200);
        result.Value.Skipped.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnUnpublishedRootIsNotFound() =>
        (await new BuildJsonLdHandler(_read).Handle(new BuildJsonLdQuery(Post() with { FieldVersion = 3 }), Ct))
            .Status.ShouldBe(ResultStatus.NotFound);

    [Fact]
    public async Task ADraftPreviewsAnUnpublishedRoot()
    {
        var draft = NewsPost() with { SchemaType = "BlogPosting" };
        var root = Node("post.draft", """{ "headline": "Draft" }""") with { FieldVersion = 0 };

        var result = await new BuildJsonLdHandler(_read).Handle(new BuildJsonLdQuery(root, Draft: draft, Placeholders: true), Ct);

        var ld = result.Value.JsonLd;
        ld.GetProperty("@type").GetString().ShouldBe("BlogPosting");
        ld.GetProperty("headline").GetString().ShouldBe("Draft");
        ld.GetProperty("description").GetString().ShouldBe("‹Summary›");
    }

    [Fact]
    public async Task ThePostsUrlIsAnHttpAddress() =>
        (await new BuildJsonLdHandler(_read).Handle(new BuildJsonLdQuery(Post(), Url: "javascript:alert(1)"), Ct))
            .Status.ShouldBe(ResultStatus.Invalid);
}

using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

public class UrlPrefixTests
{
    private static readonly Dictionary<string, int> Children = new() { ["block.paragraph"] = 1, ["block.image"] = 1 };

    [Theory]
    [InlineData("news")]
    [InlineData("ar/news")]
    [InlineData("opinion-pieces")]
    [InlineData("2026/world-cup")]
    public void ASlugPathIsAPrefix(string prefix) =>
        FieldRules.Check("post.news", NewsPost() with { UrlPrefix = prefix }).UrlPrefix.ShouldBe(prefix);

    [Theory]
    [InlineData("News")]
    [InlineData("/news")]
    [InlineData("news/")]
    [InlineData("ar//news")]
    [InlineData("news-")]
    [InlineData("-news")]
    [InlineData("a--b")]
    [InlineData("news_items")]
    [InlineData("أخبار")]
    [InlineData("news?x=1")]
    public void AnythingElseIsNot(string prefix) =>
        Should.Throw<DomainException>(() => FieldRules.Check("post.news", NewsPost() with { UrlPrefix = prefix }))
            .Message.ShouldContain("URL prefix");

    [Fact]
    public void APrefixIsAtMost200Characters()
    {
        var segment = new string('a', 99);
        FieldRules.IsUrlPrefix($"{segment}/{segment}").ShouldBeTrue(); // 199
        FieldRules.IsUrlPrefix($"{segment}/{segment}aa").ShouldBeFalse(); // 201
    }

    [Fact]
    public void ThePrefixIsTrimmedAndABlankOneIsNone()
    {
        FieldRules.Check("post.news", NewsPost() with { UrlPrefix = "  news " }).UrlPrefix.ShouldBe("news");
        FieldRules.Check("post.news", NewsPost() with { UrlPrefix = "  " }).UrlPrefix.ShouldBeNull();
    }

    [Fact]
    public void OnlyARootHasAPrefix() =>
        Should.Throw<DomainException>(() => FieldRules.Check("block.image", Image() with { UrlPrefix = "images" }))
            .Message.ShouldContain("Only a field a post can be made of");

    [Theory]
    [InlineData("post.article", "articles")]
    [InlineData("post.news", "news")]
    [InlineData("post.review", "reviews")]
    [InlineData("post.blog", "blog")]
    [InlineData("post.video", "videos")]
    [InlineData("post.reportage", "reportage")]
    [InlineData("post.photo-story", "photo-stories")]
    [InlineData("post.other", "posts")]
    [InlineData("post.photo.story", "photo-story")]
    [InlineData("page.about", "page-about")]
    [InlineData("post", "post")]
    public void TheKeySuggestsAPrefix(string key, string prefix)
    {
        FieldRules.SuggestUrlPrefix(key).ShouldBe(prefix);
        FieldRules.IsUrlPrefix(prefix).ShouldBeTrue();
    }

    [Fact]
    public void ARootNeedsItsPrefixToBePublished()
    {
        var field = FieldDefinition.Create("post.news", NewsPost() with { UrlPrefix = null }, "author-1", Now);

        Should.Throw<DomainException>(() => field.Publish(Children, "author-1", Now)).Message.ShouldContain("needs its URL prefix (e.g. news)");
        field.LatestVersion.ShouldBe(0);
    }

    [Fact]
    public void PublishingFreezesThePrefix()
    {
        var field = FieldDefinition.Create("post.news", NewsPost() with { UrlPrefix = "ar/news" }, "author-1", Now);

        var version = field.Publish(Children, "author-1", Now, ["articles"]);

        version.Snapshot.UrlPrefix.ShouldBe("ar/news");
        field.PublishedUrlPrefix.ShouldBe("ar/news");
        JsonSerializer.Deserialize<FieldSnapshot>(version.SnapshotJson, FieldJson.Options)!.UrlPrefix.ShouldBe("ar/news", "it survives storage");
    }

    [Fact]
    public void APrefixAnotherPublishedRootHasIsRefused()
    {
        var field = FieldDefinition.Create("post.story", NewsPost(), "author-1", Now);

        Should.Throw<DomainException>(() => field.Publish(Children, "author-1", Now, ["articles", "news"]))
            .Message.ShouldContain("'news' belongs to another published post field");
    }

    [Fact]
    public void AFieldThatStopsBeingARootLosesItsPrefix()
    {
        var field = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        field.Publish(Children, "author-1", Now);

        field.EditDraft(NewsPost() with { CanBeRoot = false, UrlPrefix = null }, Now);
        var version = field.Publish(Children, "author-1", Now);

        version.Snapshot.UrlPrefix.ShouldBeNull();
        field.PublishedUrlPrefix.ShouldBeNull();
    }

    [Fact]
    public void ADraftWithoutAPrefixKeepsThePublishedOne()
    {
        var field = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        field.Publish(Children, "author-1", Now);

        field.EditDraft(NewsPost() with { Title = "Story", UrlPrefix = null }, Now); // an older client sends none
        field.Draft.UrlPrefix.ShouldBe("news");
        field.Publish(Children, "author-1", Now).Snapshot.UrlPrefix.ShouldBe("news");
    }

    [Fact]
    public void ADraftStoredBeforePrefixesReadsWithThePublishedOne()
    {
        // A field the data migration filled: its column has the prefix, its stored draft JSON has none.
        var field = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        field.Publish(Children, "author-1", Now);
        var legacy = JsonSerializer.Serialize(NewsPost() with { UrlPrefix = null }, FieldJson.Options);
        typeof(FieldDefinition).GetProperty(nameof(FieldDefinition.DraftJson))!.SetValue(field, legacy);
        typeof(FieldDefinition).GetField("_draft", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(field, null);

        legacy.ShouldNotContain("urlPrefix");
        field.Draft.UrlPrefix.ShouldBe("news");
    }
}

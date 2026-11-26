using System.Text.RegularExpressions;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

public class SchemaOrgCatalogTests
{
    [Fact]
    public void EveryTypeHasAKnownParentAndKnownProperties()
    {
        foreach (var type in SchemaOrgCatalog.Types)
        {
            if (type.Parent is { } parent) SchemaOrgCatalog.Find(parent).ShouldNotBeNull($"{type.Name}'s parent {parent}");
            else type.Name.ShouldBe("CreativeWork");
            foreach (var property in type.Properties)
                SchemaOrgCatalog.Properties.ShouldContainKey(property, $"{type.Name}.{property}");
            SchemaOrgCatalog.PathOf(type.Name)[0].ShouldBe("CreativeWork");
        }
    }

    [Fact]
    public void SuggestionsAreAmongTheTypesProperties()
    {
        foreach (var type in SchemaOrgCatalog.Types)
        {
            var has = SchemaOrgCatalog.PropertiesOf(type.Name).Select(p => p.Name).ToHashSet();
            foreach (var suggested in SchemaOrgCatalog.SuggestedFor(type.Name))
                has.ShouldContain(suggested, $"{type.Name} suggests {suggested}");
        }
    }

    [Fact]
    public void NoTypeInheritsAPropertyTwice()
    {
        foreach (var type in SchemaOrgCatalog.Types)
        {
            var names = SchemaOrgCatalog.PropertiesOf(type.Name).Select(p => p.Name).ToList();
            names.Distinct().Count().ShouldBe(names.Count, type.Name);
        }
    }

    [Fact]
    public void EveryPropertyMakesAValidFieldProperty()
    {
        var all = SchemaOrgCatalog.Properties.Keys.Select(k => SchemaOrgCatalog.ToFieldProperty(k)).ToList();
        foreach (var chunk in all.Chunk(FieldRules.MaxProperties))
            Should.NotThrow(() => FieldRules.Check("block.everything", new FieldDraft("Everything", "CreativeWork", false, chunk, [], [])));
        all.ShouldAllBe(p => Regex.IsMatch(p.Key, "^[a-z][A-Za-z0-9]*$"));
    }

    [Fact]
    public void PropertiesOfANewsArticleRunFromItsOwnToCreativeWorks()
    {
        var names = SchemaOrgCatalog.PropertiesOf("NewsArticle").Select(p => p.Name).ToList();

        names[0].ShouldBe("dateline");
        names.IndexOf("articleBody").ShouldBeLessThan(names.IndexOf("headline"));
        SchemaOrgCatalog.PathOf("NewsArticle").ShouldBe(["CreativeWork", "Article", "NewsArticle"]);
    }

    [Fact]
    public void ATypeWithoutItsOwnSuggestionTakesItsParents()
    {
        SchemaOrgCatalog.SuggestedFor("OpinionNewsArticle").ShouldBe(SchemaOrgCatalog.SuggestedFor("NewsArticle"));
    }

    [Fact]
    public void TheSupportedTypesAreTheCatalogs()
    {
        SchemaOrg.CreativeWorkTypes.Count.ShouldBe(SchemaOrgCatalog.Types.Count);
        SchemaOrg.CreativeWorkTypes.ShouldContain("ImageGallery");
    }

    [Fact]
    public void StylePresetsAreValidStyle()
    {
        var draft = new FieldDraft("Styled", "CreativeWork", false, [], StylePresets.All, []);
        Should.NotThrow(() => FieldRules.Check("block.styled", draft));
    }
}

public class StarterFieldsTests
{
    [Fact]
    public void EveryStarterDraftKeepsTheRules()
    {
        foreach (var starter in StarterFields.All)
            Should.NotThrow(() => FieldRules.Check(starter.Key, starter.Draft), starter.Key);
    }

    [Fact]
    public void ChildrenComeBeforeTheirParents()
    {
        var keys = StarterFields.All.Select(f => f.Key).ToList();
        foreach (var starter in StarterFields.All)
            foreach (var child in starter.Draft.Children)
                keys.IndexOf(child.FieldKey).ShouldBeInRange(0, keys.IndexOf(starter.Key) - 1, $"{starter.Key} → {child.FieldKey}");
    }

    [Fact]
    public void WithChildrenAddsWhatAFieldContains()
    {
        StarterFields.WithChildren(["block.gallery"]).ShouldBe(["block.image", "block.gallery"]);
        StarterFields.WithChildren(["post.news"]).Count.ShouldBe(9); // eight blocks and the post
    }

    [Fact]
    public void PostsAskForWhatSearchEnginesNeed()
    {
        var news = StarterFields.Find("post.news")!.Draft;

        news.CanBeRoot.ShouldBeTrue();
        news.SchemaType.ShouldBe("NewsArticle");
        news.Properties.Where(p => p.Required).Select(p => p.SchemaProperty)
            .ShouldBe(["headline", "description", "image", "author", "datePublished"]);
    }

    [Fact]
    public void EveryStarterPostSuggestsItsOwnUniquePrefix()
    {
        var posts = StarterFields.All.Where(f => f.Draft.CanBeRoot).ToList();

        posts.Select(f => f.Draft.UrlPrefix).ShouldBeUnique();
        foreach (var post in posts) FieldRules.SuggestUrlPrefix(post.Key).ShouldBe(post.Draft.UrlPrefix, post.Key);
    }

    [Theory]
    [InlineData("post.video", "VideoObject")]
    [InlineData("post.reportage", "ReportageNewsArticle")]
    [InlineData("post.photo-story", "Article")]
    [InlineData("post.other", "Article")]
    public void TheLaterPostKindsFollowTheArticlesRules(string key, string schemaType)
    {
        var article = StarterFields.Find("post.article")!.Draft;
        var post = StarterFields.Find(key)!.Draft;

        post.CanBeRoot.ShouldBeTrue();
        post.SchemaType.ShouldBe(schemaType);
        post.Children.ShouldBe(article.Children);
        post.Style.ShouldBe(article.Style);
        post.Groups.ShouldBe(article.Groups);
        post.Properties.Where(p => p.Required).Select(p => p.Key)
            .ShouldBe(["headline", "description", "image", "author", "datePublished"]);
        var has = SchemaOrgCatalog.PropertiesOf(schemaType).Select(p => p.Name).ToHashSet();
        foreach (var value in post.Properties) has.ShouldContain(value.SchemaProperty!, $"{key}.{value.Key}");
    }

    [Fact]
    public void AVideoPostAsksForWhatVideoSearchNeeds()
    {
        var video = StarterFields.Find("post.video")!.Draft;

        video.Properties.Where(p => p.Required).Select(p => p.SchemaProperty)
            .ShouldBe(["name", "description", "thumbnailUrl", "author", "uploadDate"]);
        video.Properties.Single(p => p.Key == "video").Accept.ShouldBe(["video/*"]);
    }

    [Fact]
    public void AnImageNeedsItsAlternativeText()
    {
        var alt = StarterFields.Find("block.image")!.Draft.Properties.Single(p => p.Key == "alt");

        alt.Required.ShouldBeTrue();
        alt.SchemaProperty.ShouldBe("description");
        alt.MaxLength.ShouldBe(250);
    }
}

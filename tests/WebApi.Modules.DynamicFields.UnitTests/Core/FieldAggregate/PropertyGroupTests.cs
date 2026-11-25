using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

public class PropertyGroupTests
{
    private static readonly PropertyGroup[] MainAndSeo = [new("main", "Story"), new(" seo ", " Search and sharing ", "What the SEO section looks after.")];

    private static FieldDraft Grouped() => NewsPost() with
    {
        Groups = MainAndSeo,
        Properties = [.. NewsPost().Properties.Select(p => p.Key == "summary" ? p with { Group = "seo" } : p)]
    };

    [Fact]
    public void ARootFieldGroupsItsValuesAndUngroupedOnesAreInTheFirst()
    {
        var draft = FieldDefinition.Create("post.news", Grouped(), "author-1", Now).Draft;

        draft.Groups!.Select(g => g.Key).ShouldBe(["main", "seo"]);
        draft.Groups![1].Label.ShouldBe("Search and sharing");
        draft.Properties.Single(p => p.Key == "summary").Group.ShouldBe("seo");
        draft.Properties.Where(p => p.Key != "summary").ShouldAllBe(p => p.Group == "main");
    }

    [Fact]
    public void TheSnapshotKeepsTheGroups()
    {
        var snapshot = Publish("post.news", Grouped(), new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 });

        snapshot.Groups!.Select(g => g.Key).ShouldBe(["main", "seo"]);
        snapshot.PropertiesIn("seo").Select(p => p.Key).ShouldBe(["summary"]);
        snapshot.PropertiesIn("main").Select(p => p.Key).ShouldBe(["headline", "topics", "section"]);
    }

    [Fact]
    public void AFieldWithoutGroupsIsUnchanged()
    {
        var draft = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now).Draft;

        draft.Groups.ShouldBeNull();
        draft.Properties.ShouldAllBe(p => p.Group == null);
    }

    [Fact]
    public void OnlyARootFieldHasGroups() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.paragraph", Paragraph() with { Groups = MainAndSeo }, "author-1", Now))
            .Message.ShouldContain("root");

    [Fact]
    public void AValueNamesADeclaredGroup() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.news", Grouped() with
        {
            Properties = [.. NewsPost().Properties.Select(p => p.Key == "summary" ? p with { Group = "social" } : p)]
        }, "author-1", Now)).Message.ShouldContain("social");

    [Fact]
    public void AValueHasNoGroupWhenTheFieldHasNone() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.news", Grouped() with { Groups = null }, "author-1", Now))
            .Message.ShouldContain("no value groups");

    [Theory]
    [InlineData("SEO")]
    [InlineData("search engine")]
    [InlineData("")]
    public void AGroupKeyIsCamelCase(string key) =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.news", NewsPost() with { Groups = [new(key, "Group")] }, "author-1", Now));

    [Fact]
    public void GroupKeysAreUnique() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.news", NewsPost() with { Groups = [new("seo", "A"), new("seo", "B")] }, "author-1", Now))
            .Message.ShouldContain("appears twice");

    [Fact]
    public void AGroupNeedsALabel() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.news", NewsPost() with { Groups = [new("seo", " ")] }, "author-1", Now));

    [Fact]
    public void TheJsonSchemaAnnotatesTheGroups()
    {
        var snapshot = Publish("post.news", Grouped(), new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 });
        var schema = FieldJsonSchema.Content(snapshot);

        schema["x-groups"]!.AsArray().Select(g => g!["key"]!.GetValue<string>()).ShouldBe(["main", "seo"]);
        schema["x-groups"]![1]!["description"]!.GetValue<string>().ShouldBe("What the SEO section looks after.");
        schema["properties"]!["summary"]!["x-group"]!.GetValue<string>().ShouldBe("seo");
        schema["properties"]!["headline"]!["x-group"]!.GetValue<string>().ShouldBe("main");
    }

    [Fact]
    public void TheStarterPostsPutTheSummaryAndKeywordsInTheSeoGroup()
    {
        foreach (var key in new[] { "post.article", "post.news", "post.blog" })
        {
            var draft = FieldRules.Check(key, StarterFields.Find(key)!.Draft);
            draft.Groups!.Select(g => g.Key).ShouldBe(["main", "seo"]);
            draft.Properties.Where(p => p.Group == "seo").Select(p => p.Key).ShouldBe(["description", "keywords"]);
            draft.Properties.Single(p => p.Key == "headline").Group.ShouldBe("main");
        }
    }

    [Fact]
    public void AlignmentAndTonePresetsAreResponsive()
    {
        StylePresets.Align.Responsive.ShouldBeTrue();
        StylePresets.Tone.Responsive.ShouldBeTrue();
    }
}

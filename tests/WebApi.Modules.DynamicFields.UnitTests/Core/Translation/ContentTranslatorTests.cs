using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.Translation;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.Translation;

public class ContentTranslatorTests
{
    private static readonly Func<string, int, FieldSnapshot?> Fields = Published();

    /// <summary>The sample post with node ids: a paragraph p1 and an image img1.</summary>
    private static ContentNode Tree() => Post(
        Node("block.paragraph", """{ "text": "<p>Opening text</p>" }""") with { NodeId = "p1" },
        Node("block.image", """{ "asset": "asset_21", "caption": "The venue" }""") with { NodeId = "img1" });

    private static string Hash(string text) => ContentTranslator.SourceHash(JsonSerializer.SerializeToElement(text));

    private static ContentTranslation Arabic(params (string Node, string Key, string Text, string SourceText)[] values) => new(
        "ar",
        values.GroupBy(v => v.Node).ToDictionary(
            g => g.Key,
            g => (IReadOnlyDictionary<string, TranslatedValue>)g.ToDictionary(
                v => v.Key, v => new TranslatedValue(JsonSerializer.SerializeToElement(v.Text), Hash(v.SourceText)))));

    [Fact]
    public void OnlyTextualValuesAreUnits()
    {
        var units = ContentTranslator.Units(Tree(), Fields);

        // headline (summary is empty), the paragraph's text, the image's caption; not topics, section or asset.
        units.Select(u => (u.NodeId, u.Key)).ShouldBe([("$root", "headline"), ("p1", "text"), ("img1", "caption")]);
        units.ShouldAllBe(u => u.State == TranslationState.Missing);
        units[2].Path.ShouldBe("/children/1/content/caption");
    }

    [Fact]
    public void APropertyCanSayItIsNotLanguage()
    {
        var draft = NewsPost() with
        {
            Properties = [.. NewsPost().Properties.Select(p => p.Key == "headline" ? p with { Translatable = false } : p)]
        };
        var post = Publish("post.news", draft, new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 });
        var fields = (Func<string, int, FieldSnapshot?>)((key, version) => key == "post.news" ? post : Fields(key, version));

        ContentTranslator.Units(Tree(), fields).ShouldNotContain(u => u.Key == "headline");
    }

    [Fact]
    public void OnlyTextCanBeTranslatable() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x", Image() with
        {
            Properties = [new FieldProperty("asset", "Image", PropertyKind.Asset, Translatable: true)]
        }, "author-1", Now)).Message.ShouldContain("only text");

    [Fact]
    public void ACurrentAndAStaleTranslation()
    {
        var translation = Arabic(("$root", "headline", "اليوم الأخير في الملعب", "Final day at the venue"),
            ("img1", "caption", "الملعب", "The old venue"));

        var units = ContentTranslator.Units(Tree(), Fields, translation);

        units.Single(u => u.Key == "headline").State.ShouldBe(TranslationState.Current);
        units.Single(u => u.Key == "caption").State.ShouldBe(TranslationState.Stale);
        ContentTranslator.Summarize(units).ShouldBe(new TranslationSummary(3, 1, 1, 1));
    }

    [Fact]
    public void AValidOverlayHasNoIssues() =>
        ContentTranslator.Validate(Tree(), Arabic(("p1", "text", "<p>نص</p>", "<p>Opening text</p>")), Fields).ShouldBeEmpty();

    [Fact]
    public void AnOverlayIsCheckedAgainstTheTree()
    {
        var translation = new ContentTranslation("not a tag", new Dictionary<string, IReadOnlyDictionary<string, TranslatedValue>>
        {
            ["gone"] = new Dictionary<string, TranslatedValue> { ["text"] = new(Json("\"x\""), "h") },
            ["$root"] = new Dictionary<string, TranslatedValue>
            {
                ["section"] = new(Json("\"رياضة\""), "h"),
                ["byline"] = new(Json("\"x\""), "h"),
                ["headline"] = new(Json("42"), ""),
                ["summary"] = new(Json($"\"{new string('x', 301)}\""), "h")
            },
            ["img1"] = new Dictionary<string, TranslatedValue> { ["caption"] = new(Json("\"  \""), "h") }
        });

        var issues = ContentTranslator.Validate(Tree(), translation, Fields);

        issues.ShouldContain(i => i.Path == "/language");
        issues.ShouldContain(i => i.Path == "/nodes/gone" && i.Message.Contains("no node"));
        issues.ShouldContain(i => i.Path == "/nodes/$root/section" && i.Message.Contains("not translated"));
        issues.ShouldContain(i => i.Path == "/nodes/$root/byline" && i.Message.Contains("no value"));
        issues.ShouldContain(i => i.Path == "/nodes/$root/headline/value" && i.Message.Contains("is text"));
        issues.ShouldContain(i => i.Path == "/nodes/$root/headline/sourceHash");
        issues.ShouldContain(i => i.Path == "/nodes/$root/summary/value" && i.Message.Contains("at most 300"));
        issues.ShouldContain(i => i.Path == "/nodes/img1/caption/value" && i.Message.Contains("empty"));
    }

    [Fact]
    public void ApplyMergesTheChainOverTheSource()
    {
        var arabic = Arabic(("$root", "headline", "اليوم الأخير", "Final day at the venue"));
        var egyptian = new ContentTranslation("ar-EG", Arabic(("img1", "caption", "الاستاد", "The venue")).Nodes);

        var result = ContentTranslator.Apply(Tree(), [egyptian, arabic], Fields);

        result.Root.Content.GetProperty("headline").GetString().ShouldBe("اليوم الأخير");
        result.Root.Content.GetProperty("section").GetString().ShouldBe("sports"); // not textual: the source's
        result.Root.Style.GetProperty("layout").GetString().ShouldBe("wide");
        result.Root.Children![1].Content.GetProperty("caption").GetString().ShouldBe("الاستاد");
        result.Root.Children![1].Content.GetProperty("asset").GetString().ShouldBe("asset_21");
        result.Root.Children![0].Content.GetProperty("text").GetString().ShouldBe("<p>Opening text</p>");
        result.Languages.OrderBy(l => l.Key).ShouldBe([new("ar", 1), new("ar-EG", 1)]);
        result.Untranslated.ShouldBe(1);
        new ParityChecked().Validate(result.Root, Fields).ShouldBeEmpty();
    }

    [Fact]
    public void AStaleTranslationIsUsedUnlessToldNotTo()
    {
        var arabic = Arabic(("$root", "headline", "قديم", "An older headline"));

        ContentTranslator.Apply(Tree(), [arabic], Fields).Stale.ShouldBe(1);
        ContentTranslator.Apply(Tree(), [arabic], Fields, useStale: false)
            .Root.Content.GetProperty("headline").GetString().ShouldBe("Final day at the venue");
    }

    [Fact]
    public void TheChainFollowsTheCultureToItsParents()
    {
        ContentTranslator.Chain("ar-EG", ["en", "AR", "ar-eg"]).ShouldBe(["ar-eg", "AR"]);
        ContentTranslator.Chain("fr", ["en", "ar"]).ShouldBeEmpty();
    }

    [Fact]
    public void PruneDropsWhatTheTreeNoLongerHas()
    {
        var translation = Arabic(("$root", "headline", "عنوان", "old"), ("$root", "summary", "ملخص", "x"), ("removed", "text", "نص", "x"));

        var pruned = ContentTranslator.Prune(Tree(), translation, Fields);

        pruned.Nodes.Keys.ShouldBe(["$root"]);
        pruned.Nodes["$root"].Keys.ShouldBe(["headline"]); // stale, but kept; summary has no source text
    }

    [Fact]
    public void EqualValuesHaveEqualHashes()
    {
        Hash("The venue").ShouldBe(ContentTranslator.SourceHash(Json("\"The venue\"")));
        Hash("The venue").ShouldNotBe(Hash("The venue "));
        Hash("The venue").Length.ShouldBe(16);
    }
}

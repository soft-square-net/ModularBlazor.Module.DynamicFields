using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Engine;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.ContentValidation;

/// <summary>The legacy rules.</summary>
public class ContentTreeValidatorTests : ContentRulesTests
{
    protected override IContentValidator Rules { get; } = new LegacyContentValidator();
}

/// <summary>The schema engine, giving the legacy rules' answers (<see cref="ParityChecked"/>).</summary>
public class SchemaEngineTests : ContentRulesTests
{
    protected override IContentValidator Rules { get; } = new ParityChecked();
}

/// <summary>What content checks report, whichever engine checks it.</summary>
public abstract class ContentRulesTests
{
    private static readonly Func<string, int, FieldSnapshot?> Fields = Published();

    protected abstract IContentValidator Rules { get; }

    private IReadOnlyList<ContentIssue> Check(ContentNode root) => Rules.Validate(root, Fields);

    [Fact]
    public void AValidPostHasNoIssues() => Check(Post()).ShouldBeEmpty();

    [Fact]
    public void TheRootMustBeAPostField() =>
        Check(Node("block.paragraph", """{ "text": "x" }""")).ShouldContain(i => i.Path == "/field" && i.Message.Contains("root"));

    [Fact]
    public void AnUnpublishedVersionIsReported() =>
        Check(Post() with { FieldVersion = 2 }).ShouldHaveSingleItem().Path.ShouldBe("/field");

    [Fact]
    public void RequiredValuesUnknownValuesAndLimits()
    {
        var issues = Check(Post() with
        {
            Content = Json("""{ "headline": "", "byline": "Sam", "summary": 42, "section": "weather" }""")
        });

        issues.ShouldContain(i => i.Path == "/content/headline" && i.Message.Contains("required"));
        issues.ShouldContain(i => i.Path == "/content/byline" && i.Message.Contains("no value"));
        issues.ShouldContain(i => i.Path == "/content/summary" && i.Message.Contains("text"));
        issues.ShouldContain(i => i.Path == "/content/section" && i.Message.Contains("news, sports, culture"));
    }

    [Fact]
    public void AListValueIsCheckedItemByItem() =>
        Check(Post() with { Content = Json("""{ "headline": "H", "topics": ["kw_1", ""] }""") })
            .ShouldHaveSingleItem().Path.ShouldBe("/content/topics/1");

    [Fact]
    public void ChildrenMustBeAllowedAndPinned()
    {
        var issues = Check(Post(
            Node("block.paragraph", """{ "text": "x" }"""),
            Node("post.news", """{ "headline": "Nested" }""", "{}", Node("block.paragraph", """{ "text": "x" }""")),
            new ContentNode("block.image", 2, Json("""{ "asset": "a" }"""), Json("{}"))));

        issues.ShouldContain(i => i.Path == "/children/1/field" && i.Message.Contains("cannot contain 'post.news'"));
        issues.ShouldContain(i => i.Path == "/children/2/field" && i.Message.Contains("not published"));
    }

    [Fact]
    public void ChildCountsFollowTheParent()
    {
        var images = Enumerable.Range(0, 11).Select(_ => Node("block.image", """{ "asset": "a" }""")).ToArray();

        var issues = Check(Post(images));

        issues.ShouldContain(i => i.Path == "/children" && i.Message.Contains("at least 1 'block.paragraph'"));
        issues.ShouldContain(i => i.Path == "/children" && i.Message.Contains("at most 10 'block.image'"));
    }

    [Fact]
    public void StyleUsesOnlyTheFieldsTokens()
    {
        var issues = Check(Post(
            Node("block.paragraph", """{ "text": "x" }""", """{ "align": "justify", "color": "red" }"""),
            Node("block.image", """{ "asset": "a" }""", """{ "width": { "base": "full", "xxl": "half", "md": "huge" } }""")));

        issues.Select(i => i.Path).ShouldBe(
            ["/children/0/style/align", "/children/0/style/color", "/children/1/style/width/xxl", "/children/1/style/width/md"],
            ignoreOrder: true);
    }

    [Fact]
    public void ANonResponsiveStyleTakesNoBreakpoints() =>
        Check(Post(Node("block.paragraph", """{ "text": "x" }""", """{ "align": { "base": "start" } }""")))
            .ShouldHaveSingleItem().Path.ShouldBe("/children/0/style/align");

    [Fact]
    public void UniqueNodeIdsAreFine() =>
        Check(Post(Node("block.paragraph", """{ "text": "x" }""") with { NodeId = "p1" },
            Node("block.image", """{ "asset": "a" }""") with { NodeId = "img1" }) with { NodeId = "post-7" }).ShouldBeEmpty();

    [Fact]
    public void ANodeIdIsUsedOnce()
    {
        var issues = Check(Post(
            Node("block.paragraph", """{ "text": "x" }""") with { NodeId = "p1" },
            Node("block.paragraph", """{ "text": "y" }""") with { NodeId = "p1" }));

        var issue = issues.ShouldHaveSingleItem();
        issue.Path.ShouldBe("/children/1/nodeId");
        issue.Message.ShouldContain("'/children/0'");
    }

    [Fact]
    public void AChildCannotReuseTheRootsNodeId() =>
        Check(Post(Node("block.paragraph", """{ "text": "x" }""") with { NodeId = "post-7" }) with { NodeId = "post-7" })
            .ShouldHaveSingleItem().Message.ShouldContain("the root");

    [Theory]
    [InlineData("$root")]
    [InlineData(" ")]
    [InlineData(" p1")]
    public void ANodeIdIsNotBlankNorTheRootsOverlayKey(string id) =>
        Check(Post(Node("block.paragraph", """{ "text": "x" }""") with { NodeId = id }))
            .ShouldHaveSingleItem().Path.ShouldBe("/children/0/nodeId");

    [Fact]
    public void ContentAndStyleAreObjects()
    {
        var issues = Check(Post() with { Content = Json("[]"), Style = Json("\"wide\"") });

        issues.ShouldContain(i => i.Path == "/content");
        issues.ShouldContain(i => i.Path == "/style");
    }

    [Fact]
    public void KindsAreChecked()
    {
        var draft = Paragraph() with
        {
            Properties =
            [
                new("count", "Count", PropertyKind.Integer, Minimum: 1),
                new("ratio", "Ratio", PropertyKind.Decimal),
                new("live", "Live", PropertyKind.Boolean),
                new("day", "Day", PropertyKind.Date),
                new("at", "At", PropertyKind.DateTime),
                new("link", "Link", PropertyKind.Url)
            ]
        };
        var root = Publish("block.mixed", draft with { CanBeRoot = true }, new Dictionary<string, int>());
        FieldSnapshot? Resolve(string key, int version) => key == root.Key && version == 1 ? root : null;

        Rules.Validate(Node("block.mixed",
                """{ "count": 2, "ratio": 0.5, "live": true, "day": "2026-11-21", "at": "2026-11-21T09:30:00+02:00", "link": "https://example.com/a" }"""),
            Resolve).ShouldBeEmpty();

        Rules.Validate(Node("block.mixed",
                """{ "count": 1.5, "ratio": "x", "live": "yes", "day": "21/11/2026", "at": "2026-11-21T09:30:00", "link": "ftp://example.com" }"""),
            Resolve).Select(i => i.Path).ShouldBe(
                ["/content/count", "/content/ratio", "/content/live", "/content/day", "/content/at", "/content/link"], ignoreOrder: true);
    }

    [Fact]
    public void DeepTreesStopAtTheLimit()
    {
        // A chain deeper than the limit: every level is reported once the limit is passed, and no further.
        var draft = Paragraph() with { CanBeRoot = true, Children = [new ChildRule("block.leaf")] };
        var leaf = Publish("block.leaf", Paragraph() with { Children = [] }, new Dictionary<string, int>());
        var root = Publish("block.root", draft, new Dictionary<string, int> { ["block.leaf"] = 1 });
        FieldSnapshot? Resolve(string key, int _) => key == "block.root" ? root : key == "block.leaf" ? leaf : null;

        var node = Node("block.leaf", """{ "text": "x" }""");
        for (var i = 0; i < ContentTreeValidator.MaxDepth + 2; i++) node = Node("block.leaf", """{ "text": "x" }""", "{}", node);

        Rules.Validate(Node("block.root", """{ "text": "x" }""", "{}", node), Resolve)
            .ShouldContain(i => i.Message.Contains($"at most {ContentTreeValidator.MaxDepth} levels"));
    }
}

public class NodeValuesTests : NodeValuesRules
{
    protected override IContentValidator Rules { get; } = new LegacyContentValidator();
}

public class SchemaNodeValuesTests : NodeValuesRules
{
    protected override IContentValidator Rules { get; } = new ParityChecked();
}

public abstract class NodeValuesRules
{
    private static readonly FieldSnapshot Image = Publish("block.image", Image(), new Dictionary<string, int>());

    protected abstract IContentValidator Rules { get; }

    [Fact]
    public void ChecksOneNodeWithoutItsPlaceInATree()
    {
        Rules.ValidateValues(Image, Json("""{ "asset": "a1", "caption": "The venue" }"""), Json("""{ "width": "half" }"""))
            .ShouldBeEmpty();

        Rules.ValidateValues(Image, Json("""{ "caption": "x" }"""), Json("""{ "width": "huge" }"""))
            .Select(i => i.Path).ShouldBe(["/content/asset", "/style/width"]);
    }
}

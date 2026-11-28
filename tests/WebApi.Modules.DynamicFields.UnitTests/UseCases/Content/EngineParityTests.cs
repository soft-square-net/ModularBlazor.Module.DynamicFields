using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Engine;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.UseCases.Content;

/// <summary>
/// Content the rule tests do not send, checked by both engines (<see cref="ParityChecked"/> fails on any difference):
/// missing and null parts, children at other versions, every kind's limits, lookups, and the tree limits.
/// </summary>
public class EngineParityTests
{
    private static readonly ParityChecked Both = new();

    private static readonly FieldSnapshot Mixed = Publish("block.mixed", Paragraph() with
    {
        CanBeRoot = true,
        Properties =
        [
            new("title", "Title", PropertyKind.Text, Required: true, MinLength: 3, MaxLength: 10),
            new("count", "Count", PropertyKind.Integer, Minimum: 1, Maximum: 5),
            new("ratio", "Ratio", PropertyKind.Decimal, Minimum: 0.5m),
            new("live", "Live", PropertyKind.Boolean),
            new("day", "Day", PropertyKind.Date),
            new("at", "At", PropertyKind.DateTime),
            new("link", "Link", PropertyKind.Url),
            new("kind", "Kind", PropertyKind.Choice, Required: true, Options: ["a", "b"]),
            new("photo", "Photo", PropertyKind.Asset),
            new("tags", "Tags", PropertyKind.Term, Required: true, Multiple: true),
            new("items", "Items", PropertyKind.Lookup, Multiple: true, Lookup: new LookupBinding("inventory.items", MaxSelections: 2)),
            new("place", "Place", PropertyKind.Lookup, Lookup: new LookupBinding("geo.places", Recursive: true, MaxDepth: 3))
        ],
        Style = [new StyleProperty("width", "Width", ["full", "half"], Responsive: true)]
    }, new Dictionary<string, int>());

    private const string GoodMixed = """{ "title": "Fine", "kind": "a", "tags": ["t1"] }""";

    private static IReadOnlyList<ContentIssue> Values(string content, string style = "{}") =>
        Both.ValidateValues(Mixed, Json(content), Json(style));

    [Theory]
    [InlineData(GoodMixed)]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "count": null, "ratio": null, "place": null, "items": null }""")]
    [InlineData("""{ "title": "x", "kind": "a", "tags": ["t1"] }""")]                                    // too short
    [InlineData("""{ "title": "   ", "kind": "a", "tags": ["t1"] }""")]                                  // blank and too short
    [InlineData("""{ "title": "far too long a title", "kind": "a", "tags": ["t1"] }""")]
    [InlineData("""{ "title": 7, "kind": null, "tags": [] }""")]                                         // not text, required choice null, empty list
    [InlineData("""{ "title": null, "kind": "c", "tags": "t1" }""")]                                     // required null, not an option, not a list
    [InlineData("""{ "kind": "a" }""")]                                                                  // missing required values
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1", " ", 3, ""], "count": 0, "ratio": 0.1 }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "count": 6, "live": 1, "day": "2026-13-45", "link": "mailto:x@y.z" }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "count": "3", "photo": "", "link": "https://example.com/a?b=c" }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "items": [{ "key": "1" }, { "key": "2" }, { "key": "3" }] }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "items": [{ "text": "no key" }, "x", { "key": "1", "extra": true }] }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "place": { "key": "9", "text": null, "path": null } }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "place": { "key": "9", "path": [{ "key": "1" }, { "key": "2" }, { "key": "3" }] } }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "place": { "key": " ", "path": [7, { "text": "no key" }, { "key": "2", "other": 1 }] } }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "place": { "key": "9", "path": "up" } }""")]
    [InlineData("""{ "title": "Fine", "kind": "a", "tags": ["t1"], "place": { "key": "9", "text": 5 } }""")]
    [InlineData("""[]""")]
    [InlineData("""null""")]
    public void ValuesAgree(string content) => Values(content);

    [Theory]
    [InlineData("""null""")]
    [InlineData("""{ "width": "half" }""")]
    [InlineData("""{ "width": { "base": "full", "lg": "half" } }""")]
    [InlineData("""{ "width": { "base": "wide", "xxl": "half" }, "color": "red" }""")]
    [InlineData("""{ "width": 3 }""")]
    [InlineData("""{ "width": null }""")]
    [InlineData("""[]""")]
    public void StyleAgrees(string style) => Values(GoodMixed, style);

    [Fact]
    public void MissingContentAndStyleAgree() =>
        Both.ValidateValues(Mixed, default, default).ShouldHaveSingleItem().Path.ShouldBe("/content");

    private static readonly Func<string, int, FieldSnapshot?> Fields = Published();

    [Fact]
    public void TreesAgree()
    {
        Both.Validate(Post() with { Content = default }, Fields);
        Both.Validate(Post() with { Style = Json("null") }, Fields).ShouldBeEmpty();
        Both.Validate(Post(Node("block.paragraph", """{ "text": "x" }"""), null!), Fields);
        Both.Validate(Post(new ContentNode(null!, 1, Json("{}"), Json("{}"))), Fields);
        Both.Validate(Post(Node("block.paragraph", """{ "text": "x" }""", "{}", Node("block.image", """{ "asset": "a" }"""))), Fields);
        Both.Validate(Node("block.paragraph", """{ "text": "" }""", "{}", Node("post.news", """{ "headline": "x" }""")), Fields);
        Both.Validate(Post(Node("block.paragraph", """{ "text": "x" }""")) with { Children = null }, Fields)
            .ShouldContain(i => i.Message.Contains("at least 1 'block.paragraph'"));
    }

    [Fact]
    public void AChildAtAnotherVersionIsCheckedAgainstItsOwn()
    {
        // block.image version 2 is published too (without a caption); the post pins version 1.
        var image2 = Publish("block.image", Image() with { Properties = [new("asset", "Image", PropertyKind.Asset, Required: true)] },
            new Dictionary<string, int>()) with { Version = 2 };
        FieldSnapshot? Resolve(string key, int version) => key == "block.image" && version == 2 ? image2 : Fields(key, version);

        var issues = Both.Validate(Post(
            Node("block.paragraph", """{ "text": "x" }"""),
            new ContentNode("block.image", 2, Json("""{ "asset": "a", "caption": "only in version 1" }"""), Json("{}"))), Resolve);

        issues.ShouldContain(i => i.Path == "/children/1/fieldVersion");
        issues.ShouldContain(i => i.Path == "/children/1/content/caption" && i.Message.Contains("no value"));
    }

    [Fact]
    public void TheNodeLimitAgrees()
    {
        var paragraphs = Enumerable.Range(0, ContentTreeValidator.MaxNodes + 5)
            .Select(i => Node("block.paragraph", i % 7 == 0 ? """{ "text": 1 }""" : """{ "text": "x" }""")).ToArray();

        Both.Validate(Post(paragraphs), Fields).ShouldContain(i => i.Message.Contains($"at most {ContentTreeValidator.MaxNodes} nodes"));
    }

    [Fact]
    public void TheStarterLibraryAgrees()
    {
        // Every starter field, as a node with no values: each reports its required values the same way.
        foreach (var starter in StarterFields.All)
        {
            var snapshot = Publish(starter.Key, starter.Draft, starter.Draft.Children.ToDictionary(c => c.FieldKey, _ => 1));
            Both.ValidateValues(snapshot, Json("{}"), Json("{}"));
        }
    }

    // ---- the setting -------------------------------------------------------------------------------------------------

    private sealed class TestLog : ILogger, ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
        public ILogger CreateLogger(string categoryName) => this;
        public void Dispose() { }
    }

    private static (ContentValidator Validator, TestLog Log) Engine(ValidationEngine engine)
    {
        var log = new TestLog();
        var loggers = LoggerFactory.Create(b => b.AddProvider(log));
        return (new ContentValidator(Options.Create(new DynamicFieldsOptions { ValidationEngine = engine }), loggers), log);
    }

    [Theory]
    [InlineData(ValidationEngine.Legacy)]
    [InlineData(ValidationEngine.Schema)]
    [InlineData(ValidationEngine.Compare)]
    public void EveryEngineGivesTheSameAnswer(ValidationEngine engine)
    {
        var (validator, log) = Engine(engine);

        validator.Validate(Post() with { Content = Json("""{ "headline": "" }""") }, Fields)
            .ShouldHaveSingleItem().Path.ShouldBe("/content/headline");
        log.Entries.ShouldNotContain(e => e.Level >= LogLevel.Warning);
    }

    [Fact]
    public void CompareAnswersWithTheLegacyRulesAndLogsADifference()
    {
        var (validator, log) = Engine(ValidationEngine.Compare);
        // A date and time without seconds: the legacy rules take it, JSON Schema's date-time (RFC 3339) does not.
        var at = Publish("block.at", Paragraph() with { CanBeRoot = true, Properties = [new("at", "At", PropertyKind.DateTime)] },
            new Dictionary<string, int>());

        var issues = validator.Validate(Node("block.at", """{ "at": "2026-11-21T09:30+02:00" }"""), (k, v) => k == at.Key ? at : null);

        issues.ShouldBeEmpty();
        log.Entries.ShouldContain(e => e.Level == LogLevel.Warning && e.Message.Contains("/content/at"));
    }
}

extern alias JsonSchemaNet;

using System.Text.Json;
using System.Text.Json.Nodes;
using JsonSchemaNet::Json.Schema;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

/// <summary>
/// The dialect and the compiled node schemas, checked with a standard JSON Schema 2020-12 validator (JsonSchema.Net):
/// the meta-schemas are valid schemas, every compiled schema is valid in the dialect, and a plain 2020-12 validator
/// accepts the content the module accepts and refuses what the standard keywords can tell is wrong.
/// </summary>
public class FieldSchemaCompilerTests
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";

    private static readonly FieldSnapshot Paragraph = Publish("block.paragraph", Samples.Paragraph(), new Dictionary<string, int>());
    private static readonly FieldSnapshot ImageBlock = Publish("block.image", Image(), new Dictionary<string, int>());
    private static readonly FieldSnapshot Post = Publish("post.news", NewsPost(),
        new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 });

    private static readonly EvaluationOptions Hierarchical = new() { OutputFormat = OutputFormat.Hierarchical };

    /// <summary>A registry with the dialect and its meta-schemas, built from the module's own files.</summary>
    private static BuildOptions WithDialect()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        // The vocabularies first: the dialect refers to them.
        foreach (var name in FieldDialect.Names.Reverse())
            JsonSchema.FromText(FieldDialect.Read(name)!, options, new Uri(FieldDialect.IdOf(name)));
        return options;
    }

    private static JsonSchema DialectMetaSchema(BuildOptions options) =>
        (JsonSchema)options.SchemaRegistry.Get(new Uri(FieldDialect.Uri))!;

    private static JsonElement Element(JsonNode node) => JsonSerializer.SerializeToElement(node);

    private static string Errors(EvaluationResults results) =>
        string.Join("; ", Flatten(results).Where(r => r.Errors is { Count: > 0 })
            .SelectMany(r => r.Errors!.Select(e => $"{r.InstanceLocation} {e.Key}: {e.Value}")));

    private static IEnumerable<EvaluationResults> Flatten(EvaluationResults r) =>
        new[] { r }.Concat((r.Details ?? []).SelectMany(Flatten));

    [Fact]
    public void TheFilesAreTheDialectAndItsVocabularies()
    {
        foreach (var name in FieldDialect.Names)
        {
            var doc = JsonNode.Parse(FieldDialect.Read(name)!)!;
            doc["$schema"]!.GetValue<string>().ShouldBe(Draft);
            doc["$id"]!.GetValue<string>().ShouldBe(FieldDialect.IdOf(name));
        }

        var vocabulary = JsonNode.Parse(FieldDialect.Read("dialect")!)!["$vocabulary"]!.AsObject();
        foreach (var (name, asserts) in FieldDialect.Vocabularies)
            vocabulary[$"{FieldDialect.Base}/vocab/{name}"]!.GetValue<bool>().ShouldBe(asserts);
        vocabulary.Count(v => v.Key.StartsWith("https://json-schema.org/", StringComparison.Ordinal)).ShouldBe(7);
        FieldDialect.Read("nope").ShouldBeNull();
    }

    [Fact]
    public void TheMetaSchemasAreValidSchemas()
    {
        foreach (var name in FieldDialect.Names)
        {
            var results = MetaSchemas.Draft202012.Evaluate(JsonDocument.Parse(FieldDialect.Read(name)!).RootElement, Hierarchical);
            results.IsValid.ShouldBeTrue($"{name}: {Errors(results)}");
        }
    }

    [Fact]
    public void EveryCompiledSchemaIsValidInTheDialect()
    {
        var meta = DialectMetaSchema(WithDialect());

        foreach (var snapshot in new[] { Paragraph, ImageBlock, Post }.Concat(Starters()))
        {
            var schema = FieldSchemaCompiler.Compile(snapshot);
            schema["$schema"]!.GetValue<string>().ShouldBe(FieldDialect.Uri);
            var results = meta.Evaluate(Element(schema), Hierarchical);
            results.IsValid.ShouldBeTrue($"{snapshot.Key}: {Errors(results)}");
        }
    }

    [Fact]
    public void TheDialectChecksItsOwnKeywords()
    {
        var meta = DialectMetaSchema(WithDialect());
        var schema = FieldSchemaCompiler.Compile(Post);
        schema["schemaOrgType"] = "not a type";
        schema["maxDepth"] = 0;

        var results = meta.Evaluate(Element(schema), Hierarchical);

        results.IsValid.ShouldBeFalse();
        var errors = Errors(results);
        errors.ShouldContain("/schemaOrgType");
        errors.ShouldContain("/maxDepth");
    }

    [Fact]
    public void ANodeSchemaUsesTheDialectsKeywords()
    {
        var schema = FieldSchemaCompiler.Compile(Post);

        schema["$id"]!.GetValue<string>().ShouldBe("urn:dynamicfields:post.news:1");
        schema["schemaOrgType"]!.GetValue<string>().ShouldBe("NewsArticle");
        schema["canBeRoot"]!.GetValue<bool>().ShouldBeTrue();
        schema["urlPrefix"]!.GetValue<string>().ShouldBe("news");
        schema["uniqueNodeIds"]!.GetValue<bool>().ShouldBeTrue();
        schema["maxDepth"]!.GetValue<int>().ShouldBe(12);
        schema["maxNodes"]!.GetValue<int>().ShouldBe(500);
        schema.ToJsonString().ShouldNotContain("\"x-");

        var content = schema["properties"]!["content"]!;
        content["properties"]!["headline"]!["schemaOrgProperty"]!.GetValue<string>().ShouldBe("headline");
        content["properties"]!["headline"]!["translatable"]!.GetValue<bool>().ShouldBeTrue();
        content["properties"]!["topics"]!["items"]!["refKind"]!.GetValue<string>().ShouldBe("keyword");

        var children = schema["properties"]!["children"]!;
        children["items"]!["anyOf"]!.AsArray().Select(r => r!["$ref"]!.GetValue<string>())
            .ShouldBe(["urn:dynamicfields:block.paragraph:1", "urn:dynamicfields:block.image:1"]);
        var counts = children["allOf"]!.AsArray();
        counts[0]!["contains"]!["properties"]!["field"]!["const"]!.GetValue<string>().ShouldBe("block.paragraph");
        counts[0]!["minContains"]!.GetValue<int>().ShouldBe(1);
        counts[1]!["minContains"]!.GetValue<int>().ShouldBe(0);
        counts[1]!["maxContains"]!.GetValue<int>().ShouldBe(10);

        var leaf = FieldSchemaCompiler.Compile(Paragraph);
        leaf.ContainsKey("canBeRoot").ShouldBeFalse();
        leaf.ContainsKey("maxNodes").ShouldBeFalse();
        leaf["properties"]!["children"]!["maxItems"]!.GetValue<int>().ShouldBe(0);
    }

    /// <summary>The post's schema with its children's, each read as plain 2020-12 (the dialect's keywords then only annotate).</summary>
    private static JsonSchema PlainPostSchema()
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        JsonSchema Build(FieldSnapshot s)
        {
            var schema = FieldSchemaCompiler.Compile(s);
            schema["$schema"] = Draft;
            return JsonSchema.Build(Element(schema), options, new Uri(FieldDialect.VersionId(s.Key, s.Version)));
        }
        Build(Paragraph);
        Build(ImageBlock);
        return Build(Post);
    }

    private const string GoodPost = """
        {
          "nodeId": "post-1", "field": "post.news", "fieldVersion": 1,
          "content": { "headline": "Rain in Cairo", "summary": null, "topics": ["kw-1"], "section": "news" },
          "style": { "layout": "wide" },
          "children": [
            { "nodeId": "p1", "field": "block.paragraph", "fieldVersion": 1, "content": { "text": "<p>Hello</p>" }, "style": { "align": "center" } },
            { "nodeId": "i1", "field": "block.image", "fieldVersion": 1, "content": { "asset": "file-1" }, "style": { "width": { "base": "full", "md": "half" } } }
          ]
        }
        """;

    [Fact]
    public void APlainValidatorAcceptsContentTheModuleAccepts()
    {
        var results = PlainPostSchema().Evaluate(Json(GoodPost), Hierarchical);

        results.IsValid.ShouldBeTrue(Errors(results));
    }

    [Theory]
    [InlineData("/children/0/style/align", "\"left\"", "/children/0/style/align")]                  // not one of the field's tokens
    [InlineData("/content/headline", "\"  \"", "/content/headline")]                                // required text, blank
    [InlineData("/content/extra", "1", "/content")]                                                  // not a value of the field
    [InlineData("/children/1/style/width", "{ \"xxl\": \"full\" }", "/children/1/style/width")]     // not a breakpoint
    [InlineData("/fieldVersion", "2", "/fieldVersion")]                                              // another version
    public void APlainValidatorRefusesWhatTheStandardKeywordsCanTell(string pointer, string value, string at)
    {
        var node = JsonNode.Parse(GoodPost)!;
        Set(node, pointer, JsonNode.Parse(value));

        var results = PlainPostSchema().Evaluate(Element(node), Hierarchical);

        results.IsValid.ShouldBeFalse();
        Errors(results).ShouldContain(at);
    }

    [Fact]
    public void APlainValidatorCountsTheChildren()
    {
        var node = JsonNode.Parse(GoodPost)!;
        node["children"]!.AsArray().RemoveAt(0); // the paragraph the post requires

        PlainPostSchema().Evaluate(Element(node), Hierarchical).IsValid.ShouldBeFalse();
    }

    private static void Set(JsonNode root, string pointer, JsonNode? value)
    {
        var parts = pointer.Trim('/').Split('/');
        var at = root;
        foreach (var part in parts[..^1]) at = int.TryParse(part, out var i) ? at[i]! : at[part]!;
        at[parts[^1]] = value;
    }

    /// <summary>The starter library, each field published after the fields it contains.</summary>
    private static IEnumerable<FieldSnapshot> Starters()
    {
        var published = new Dictionary<string, FieldSnapshot>();
        var pending = StarterFields.All.ToList();
        while (pending.Count > 0)
        {
            var ready = pending.First(f => f.Draft.Children.All(c => published.ContainsKey(c.FieldKey)));
            published[ready.Key] = Publish(ready.Key, ready.Draft, ready.Draft.Children.ToDictionary(c => c.FieldKey, _ => 1));
            pending.Remove(ready);
        }
        return published.Values;
    }
}

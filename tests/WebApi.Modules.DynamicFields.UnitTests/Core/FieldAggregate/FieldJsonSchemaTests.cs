using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

public class FieldJsonSchemaTests
{
    private static readonly FieldSnapshot Post = Publish("post.news", NewsPost(),
        new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 });

    [Fact]
    public void TheContentSchemaIsDraft2020WithTheSchemaOrgMapping()
    {
        var schema = FieldJsonSchema.Content(Post);

        schema["$schema"]!.GetValue<string>().ShouldBe(FieldJsonSchema.Draft);
        schema["$id"]!.GetValue<string>().ShouldBe("urn:dynamicfields:post.news:1:content");
        schema["x-schemaOrg"]!.GetValue<string>().ShouldBe("NewsArticle");
        schema["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
        schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["headline"]);

        var headline = schema["properties"]!["headline"]!;
        headline["type"]!.GetValue<string>().ShouldBe("string");
        headline["maxLength"]!.GetValue<int>().ShouldBe(110);
        headline["x-schemaOrg"]!.GetValue<string>().ShouldBe("headline");

        var topics = schema["properties"]!["topics"]!;
        topics["type"]!.GetValue<string>().ShouldBe("array");
        topics["items"]!["x-reference"]!.GetValue<string>().ShouldBe("keyword");

        schema["properties"]!["section"]!["enum"]!.AsArray().Count.ShouldBe(3);
    }

    [Fact]
    public void AResponsiveStyleTakesATokenOrOnePerBreakpoint()
    {
        var image = Publish("block.image", Image(), new Dictionary<string, int>());

        var width = FieldJsonSchema.Style(image)["properties"]!["width"]!;

        width["default"]!.GetValue<string>().ShouldBe("full");
        var choices = width["oneOf"]!.AsArray();
        choices[0]!["enum"]!.AsArray().Count.ShouldBe(3);
        choices[1]!["properties"]!.AsObject().Select(p => p.Key).ShouldBe(["base", "sm", "md", "lg", "xl"]);
    }
}

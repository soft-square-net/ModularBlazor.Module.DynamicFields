using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.Core.Translation;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Translate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.UseCases.Content;

public class TranslationHandlers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IReadRepository<FieldDefinition> _read = Substitute.For<IReadRepository<FieldDefinition>>();

    public TranslationHandlers()
    {
        var paragraph = FieldDefinition.Create("block.paragraph", Paragraph(), "author-1", Now);
        paragraph.Publish(new Dictionary<string, int>(), "author-1", Now);
        var image = FieldDefinition.Create("block.image", Image(), "author-1", Now);
        image.Publish(new Dictionary<string, int>(), "author-1", Now);
        var post = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        post.Publish(new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 }, "author-1", Now);
        _read.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([paragraph, image, post]);
    }

    private static ContentTranslation Headline(string language, string text) => new(language,
        new Dictionary<string, IReadOnlyDictionary<string, TranslatedValue>>
        {
            [ContentTranslator.RootId] = new Dictionary<string, TranslatedValue>
            {
                ["headline"] = new(JsonSerializer.SerializeToElement(text),
                    ContentTranslator.SourceHash(JsonSerializer.SerializeToElement("Final day at the venue")))
            }
        });

    [Fact]
    public async Task CheckListsTheUnitsAndTheirState()
    {
        var result = await new CheckTranslationHandler(_read).Handle(new CheckTranslationQuery(Post(), Headline("ar", "اليوم الأخير")), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Valid.ShouldBeTrue();
        result.Value.Units.Single(u => u.Key == "headline").State.ShouldBe(TranslationState.Current);
        // The sample's children have no node ids: only the root's values can be translated.
        result.Value.Summary.ShouldBe(new TranslationSummary(1, 1, 0, 0));
    }

    [Fact]
    public async Task ApplyTakesTheCultureThenItsParent()
    {
        var query = new ApplyTranslationsQuery(Post(), [Headline("ar", "عربي"), Headline("en-GB", "British"), Headline("fr", "Français")], "ar-EG");

        var result = await new ApplyTranslationsHandler(_read).Handle(query, Ct);

        result.Value.Chain.ShouldBe(["ar"]);
        result.Value.Root.Content.GetProperty("headline").GetString().ShouldBe("عربي");
    }

    [Fact]
    public async Task ApplyWithAnUnknownCultureServesTheSource()
    {
        var result = await new ApplyTranslationsHandler(_read).Handle(new ApplyTranslationsQuery(Post(), [Headline("ar", "عربي")], "de"), Ct);

        result.Value.Chain.ShouldBeEmpty();
        result.Value.Root.Content.GetProperty("headline").GetString().ShouldBe("Final day at the venue");
        result.Value.Untranslated.ShouldBe(1);
    }

    [Fact]
    public async Task ApplyRefusesABrokenCulture() =>
        (await new ApplyTranslationsHandler(_read).Handle(new ApplyTranslationsQuery(Post(), [], "<script>"), Ct))
            .Status.ShouldBe(ResultStatus.Invalid);
}

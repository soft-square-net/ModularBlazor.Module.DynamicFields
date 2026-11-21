using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

public class FieldRulesTests
{
    [Fact]
    public void CreatesADraftThatIsNotPublished()
    {
        var field = FieldDefinition.Create(" post.news ", NewsPost(), "author-1", Now);

        field.Key.ShouldBe("post.news");
        field.Title.ShouldBe("News post");
        field.SchemaType.ShouldBe("NewsArticle");
        field.CanBeRoot.ShouldBeTrue();
        field.LatestVersion.ShouldBe(0);
        field.HasDraftChanges.ShouldBeTrue();
        field.Draft.Children.Select(c => c.FieldKey).ShouldBe(["block.paragraph", "block.image"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Post.News")]
    [InlineData("post..news")]
    [InlineData("post news")]
    [InlineData("1post")]
    public void RejectsABadKey(string key) =>
        Should.Throw<DomainException>(() => FieldDefinition.Create(key, Paragraph(), "author-1", Now));

    [Fact]
    public void AcceptsDashesInAKey() =>
        FieldDefinition.Create("block.pull-quote", Paragraph(), "author-1", Now).Key.ShouldBe("block.pull-quote");

    [Fact]
    public void NeedsASupportedSchemaOrgType() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x", Paragraph() with { SchemaType = "Thing" }, "author-1", Now))
            .Message.ShouldContain("schema.org");

    [Fact]
    public void RejectsDuplicatePropertyKeys()
    {
        var draft = Paragraph() with
        {
            Properties = [new("text", "Text", PropertyKind.Text), new("text", "Again", PropertyKind.Text)]
        };
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x", draft, "author-1", Now)).Message.ShouldContain("twice");
    }

    [Theory]
    [InlineData("Text")]
    [InlineData("my-text")]
    [InlineData("my text")]
    public void PropertyKeysAreCamelCase(string key) =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Properties = [new(key, "Label", PropertyKind.Text)] }, "author-1", Now));

    [Fact]
    public void AChoiceNeedsOptionsAndOthersHaveNone()
    {
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Properties = [new("kind", "Kind", PropertyKind.Choice)] }, "author-1", Now));
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Properties = [new("kind", "Kind", PropertyKind.Text, Options: ["a"])] }, "author-1", Now));
    }

    [Fact]
    public void LimitsFitTheKind()
    {
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Properties = [new("count", "Count", PropertyKind.Integer, MaxLength: 3)] }, "author-1", Now));
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Properties = [new("name", "Name", PropertyKind.Text, Maximum: 3)] }, "author-1", Now));
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Properties = [new("name", "Name", PropertyKind.Text, MinLength: 5, MaxLength: 2)] }, "author-1", Now));
    }

    [Fact]
    public void StyleValuesAreTokensAndTheDefaultIsOneOfThem()
    {
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Style = [new("width", "Width", ["width: 100px"])] }, "author-1", Now));
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Style = [new("width", "Width", ["full", "half"], Default: "third")] }, "author-1", Now));
    }

    [Fact]
    public void AFieldCannotContainItself() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("block.x",
            Paragraph() with { Children = [new ChildRule("block.x")] }, "author-1", Now)).Message.ShouldContain("itself");

    [Fact]
    public void ChildCountsAreOrdered() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.x",
            NewsPost() with { Children = [new ChildRule("block.image", Min: 3, Max: 2)] }, "author-1", Now));

    [Theory]
    [InlineData("Module.Media/edit-image", true)]
    [InlineData("edit-image", false)]
    [InlineData("Module.Media/", false)]
    public void TheEditorDialogIsADialogId(string dialog, bool valid)
    {
        var create = () => FieldDefinition.Create("block.image", Image() with { EditorDialog = dialog }, "author-1", Now);
        if (valid) create().Draft.EditorDialog.ShouldBe(dialog);
        else Should.Throw<DomainException>(create);
    }

    [Fact]
    public void AnEditBumpsTheVersionAndAnInvalidOneChangesNothing()
    {
        var field = FieldDefinition.Create("block.paragraph", Paragraph(), "author-1", Now);

        field.EditDraft(Paragraph() with { Title = "Body text" }, Now.AddMinutes(5));
        field.Title.ShouldBe("Body text");
        field.Version.ShouldBe(1);

        Should.Throw<DomainException>(() => field.EditDraft(Paragraph() with { Title = " " }, Now));
        field.Title.ShouldBe("Body text");
        field.Version.ShouldBe(1);
    }
}

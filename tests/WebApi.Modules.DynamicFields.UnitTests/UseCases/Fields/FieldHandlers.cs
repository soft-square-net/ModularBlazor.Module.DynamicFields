using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Create;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Delete;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Publish;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.UseCases.Fields;

public class FieldHandlers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IRepository<FieldDefinition> _fields = Substitute.For<IRepository<FieldDefinition>>();
    private readonly IReadRepository<FieldDefinition> _read = Substitute.For<IReadRepository<FieldDefinition>>();
    private readonly IAccessScopes _access = Substitute.For<IAccessScopes>();
    private readonly TestClock _clock = new();

    public FieldHandlers() => _access.UserId.Returns("author-1");

    private static FieldDefinition Published(string key, FieldDraft draft, Dictionary<string, int>? children = null)
    {
        var field = FieldDefinition.Create(key, draft, "author-1", Now);
        field.Publish(children ?? new Dictionary<string, int>(), "author-1", Now);
        return field;
    }

    [Fact]
    public async Task CreateRefusesAKeyThatExists()
    {
        _fields.AnyAsync(Arg.Any<FieldByKeySpec>(), Ct).Returns(true);

        var result = await new CreateFieldHandler(_fields, _access, _clock).Handle(new CreateFieldCommand("block.image", Image()), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _fields.DidNotReceive().AddAsync(Arg.Any<FieldDefinition>(), Ct);
    }

    [Fact]
    public async Task CreateNeedsASignedInUser()
    {
        _access.UserId.Returns((string?)null);

        var result = await new CreateFieldHandler(_fields, _access, _clock).Handle(new CreateFieldCommand("block.image", Image()), Ct);

        result.Status.ShouldBe(ResultStatus.Unauthorized);
    }

    [Fact]
    public async Task PublishPinsEachChildToItsNewestVersion()
    {
        var post = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        var paragraph = Published("block.paragraph", Paragraph());
        paragraph.EditDraft(Paragraph() with { Title = "Body text" }, Now);
        paragraph.Publish(new Dictionary<string, int>(), "author-1", Now); // version 2
        _fields.FirstOrDefaultAsync(Arg.Any<FieldByKeySpec>(), Ct).Returns(post);
        _fields.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([paragraph, Published("block.image", Image())]);

        var result = await new PublishFieldHandler(_fields, _access, _clock).Handle(new PublishFieldCommand("post.news", 0), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Field.Children.Select(c => (c.FieldKey, c.Version)).ShouldBe([("block.paragraph", 2), ("block.image", 1)]);
        await _fields.Received(1).UpdateAsync(post, Ct);
    }

    [Fact]
    public async Task PublishWithAStaleVersionIsAConflict()
    {
        _fields.FirstOrDefaultAsync(Arg.Any<FieldByKeySpec>(), Ct).Returns(FieldDefinition.Create("block.image", Image(), "author-1", Now));

        var result = await new PublishFieldHandler(_fields, _access, _clock).Handle(new PublishFieldCommand("block.image", 7), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
    }

    [Fact]
    public async Task APublishedFieldCannotBeDeleted()
    {
        _fields.FirstOrDefaultAsync(Arg.Any<FieldByKeySpec>(), Ct).Returns(Published("block.image", Image()));

        var result = await new DeleteFieldHandler(_fields).Handle(new DeleteFieldCommand("block.image", 1), Ct);

        result.Status.ShouldBe(ResultStatus.Conflict);
        await _fields.DidNotReceive().DeleteAsync(Arg.Any<FieldDefinition>(), Ct);
    }

    [Fact]
    public async Task ValidateResolvesTheFieldsTheTreeNames()
    {
        _read.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([
            Published("block.paragraph", Paragraph()),
            Published("block.image", Image()),
            Published("post.news", NewsPost(), new() { ["block.paragraph"] = 1, ["block.image"] = 1 })
        ]);
        var handler = new ValidateContentHandler(_read);

        (await handler.Handle(new ValidateContentQuery(Post()), Ct)).Value.Valid.ShouldBeTrue();

        var report = (await handler.Handle(new ValidateContentQuery(Post() with { Content = Json("{}") }), Ct)).Value;
        report.Valid.ShouldBeFalse();
        report.Issues.ShouldHaveSingleItem().Path.ShouldBe("/content/headline");
    }
}

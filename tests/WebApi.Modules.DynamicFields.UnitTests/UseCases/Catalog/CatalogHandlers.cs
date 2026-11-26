using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Catalog;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.UseCases.Catalog;

public class CatalogHandlers
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly IRepository<FieldDefinition> _fields = Substitute.For<IRepository<FieldDefinition>>();
    private readonly IReadRepository<FieldDefinition> _read = Substitute.For<IReadRepository<FieldDefinition>>();
    private readonly IAccessScopes _access = Substitute.For<IAccessScopes>();
    private readonly TestClock _clock = new();
    private readonly List<FieldDefinition> _added = [];

    public CatalogHandlers()
    {
        _access.UserId.Returns("author-1");
        _fields.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns(new List<FieldDefinition>());
        _fields.ListAsync(Arg.Any<TakenUrlPrefixesSpec>(), Ct).Returns(new List<string>());
        _fields.AddAsync(Arg.Do<FieldDefinition>(_added.Add), Ct).Returns(c => c.Arg<FieldDefinition>());
    }

    private InstallStarterFieldsHandler Install => new(_fields, _access, _clock);

    [Fact]
    public async Task InstallingEverythingPublishesEveryStarterField()
    {
        var result = await Install.Handle(new InstallStarterFieldsCommand(null), Ct);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Installed.ShouldBe(StarterFields.All.Select(f => f.Key));
        result.Value.Skipped.ShouldBeEmpty();
        _added.ShouldAllBe(f => f.LatestVersion == 1);
        _added.Single(f => f.Key == "post.news").GetVersion(1)!.Snapshot.Children.ShouldAllBe(c => c.Version == 1);
    }

    [Fact]
    public async Task InstallingAFieldBringsItsChildren()
    {
        var result = await Install.Handle(new InstallStarterFieldsCommand(["block.gallery"]), Ct);

        result.Value.Installed.ShouldBe(["block.image", "block.gallery"]);
    }

    [Fact]
    public async Task AFieldTheTenantHasIsSkippedAndItsVersionPinned()
    {
        var image = FieldDefinition.Create("block.image", Image(), "author-1", Now);
        image.Publish(new Dictionary<string, int>(), "author-1", Now);
        image.EditDraft(Image() with { Title = "Photo" }, Now);
        image.Publish(new Dictionary<string, int>(), "author-1", Now); // version 2
        _fields.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([image]);

        var result = await Install.Handle(new InstallStarterFieldsCommand(["block.gallery"]), Ct);

        result.Value.Skipped.ShouldBe(["block.image"]);
        result.Value.Installed.ShouldBe(["block.gallery"]);
        _added.Single().GetVersion(1)!.Snapshot.Children.Single().Version.ShouldBe(2);
    }

    [Fact]
    public async Task AParentOfAnUnpublishedChildStaysADraft()
    {
        _fields.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([FieldDefinition.Create("block.image", Image(), "author-1", Now)]);

        var result = await Install.Handle(new InstallStarterFieldsCommand(["block.gallery"]), Ct);

        result.Value.Drafts.ShouldBe(["block.gallery"]);
        _added.Single().LatestVersion.ShouldBe(0);
    }

    [Fact]
    public async Task InstalledPostsHaveTheirStaticUrls()
    {
        await Install.Handle(new InstallStarterFieldsCommand(null), Ct);

        _added.Where(f => f.CanBeRoot).ToDictionary(f => f.Key, f => f.PublishedUrlPrefix).ShouldBe(new Dictionary<string, string?>
        {
            ["post.article"] = "articles", ["post.news"] = "news", ["post.review"] = "reviews", ["post.blog"] = "blog",
            ["post.video"] = "videos", ["post.reportage"] = "reportage", ["post.photo-story"] = "photo-stories",
            ["post.other"] = "posts"
        }, ignoreOrder: true);
        _added.Where(f => !f.CanBeRoot).ShouldAllBe(f => f.PublishedUrlPrefix == null);
    }

    [Fact]
    public async Task APostWhosePrefixIsTakenStaysADraft()
    {
        _fields.ListAsync(Arg.Any<TakenUrlPrefixesSpec>(), Ct).Returns(["news"]); // a post field of the tenant's own

        var result = await Install.Handle(new InstallStarterFieldsCommand(["post.news", "post.review"]), Ct);

        result.Value.Drafts.ShouldBe(["post.news"]);
        result.Value.Installed.ShouldContain("post.review");
        _added.Single(f => f.Key == "post.news").Draft.UrlPrefix.ShouldBe("news", "the author picks another before publishing");
    }

    [Fact]
    public async Task RootPostFieldsListThePublishedKindsWithTheirPrefixes()
    {
        var news = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);
        news.Publish(new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 }, "author-1", Now);
        news.EditDraft(NewsPost() with { Title = "Unpublished title" }, Now);
        var demoted = FieldDefinition.Create("post.old", NewsPost() with { UrlPrefix = "old" }, "author-1", Now);
        demoted.Publish(new Dictionary<string, int> { ["block.paragraph"] = 1, ["block.image"] = 1 }, "author-1", Now);
        _read.ListAsync(Arg.Any<PublishedRootFieldsSpec>(), Ct).Returns([news]);

        var result = await new ListRootPostFieldsHandler(_read).Handle(new ListRootPostFieldsQuery(), Ct);

        var kind = result.Value.ShouldHaveSingleItem();
        kind.ShouldBe(new soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.RootPostFieldDto("post.news", "News post", null, "NewsArticle", "news", 1));
    }

    [Fact]
    public async Task AnUnknownKeyIsNotFound()
    {
        var result = await Install.Handle(new InstallStarterFieldsCommand(["block.nope"]), Ct);

        result.Status.ShouldBe(ResultStatus.NotFound);
        _added.ShouldBeEmpty();
    }

    [Fact]
    public async Task TheListMarksWhatIsInstalled()
    {
        _read.ListAsync(Arg.Any<FieldsByKeysSpec>(), Ct).Returns([FieldDefinition.Create("block.image", Image(), "author-1", Now)]);

        var result = await new ListStarterFieldsHandler(_read).Handle(new ListStarterFieldsQuery(), Ct);

        result.Value.Single(f => f.Key == "block.image").Installed.ShouldBeTrue();
        result.Value.Single(f => f.Key == "post.news").Children.ShouldContain("block.gallery");
        result.Value.Count(f => f.Installed).ShouldBe(1);
    }

    [Fact]
    public async Task TheCatalogFlattensEachType()
    {
        var result = await new GetSchemaOrgCatalogHandler().Handle(new GetSchemaOrgCatalogQuery(), Ct);

        var image = result.Value.Types.Single(t => t.Name == "ImageObject");
        image.Path.ShouldBe(["CreativeWork", "MediaObject", "ImageObject"]);
        image.Properties.ShouldContain("caption");
        image.Properties.ShouldContain("contentUrl");
        image.Properties.ShouldContain("headline");
        image.Suggested.ShouldContain("caption");
        result.Value.StylePresets.ShouldContain(s => s.Key == "width");
    }
}

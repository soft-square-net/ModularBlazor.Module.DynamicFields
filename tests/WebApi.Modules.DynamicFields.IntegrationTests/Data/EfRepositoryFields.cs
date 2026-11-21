using Microsoft.EntityFrameworkCore;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

namespace soft.square.net.WebApi.Modules.DynamicFields.IntegrationTests.Data;

/// <summary>The module's mapping and specifications through the platform's repository, on a real database.</summary>
public sealed class EfRepositoryFields : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);
    private readonly DynamicFieldsDatabase _database = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private static FieldDraft Image(string title = "Image") => new(
        title, "ImageObject", CanBeRoot: false,
        [new FieldProperty("asset", "Image", PropertyKind.Asset, Required: true, SchemaProperty: "contentUrl")],
        [new StyleProperty("width", "Width", ["full", "half"], Responsive: true)],
        []);

    private static FieldDraft Post() => new(
        "News post", "NewsArticle", CanBeRoot: true,
        [new FieldProperty("headline", "Headline", PropertyKind.Text, Required: true, SchemaProperty: "headline")],
        [],
        [new ChildRule("block.image", Max: 10)]);

    private async Task<FieldDefinition> AddAsync(string key, FieldDraft draft, string tenant = "acme")
    {
        await using var context = _database.For(tenant);
        return await new EfRepository<FieldDefinition>(context).AddAsync(FieldDefinition.Create(key, draft, "author-1", Now), Ct);
    }

    private async Task PublishAsync(string key, IReadOnlyDictionary<string, int> children, string tenant = "acme")
    {
        await using var context = _database.For(tenant);
        var fields = new EfRepository<FieldDefinition>(context);
        var field = (await fields.FirstOrDefaultAsync(new FieldByKeySpec(key), Ct))!;
        field.Publish(children, "editor-1", Now);
        await fields.UpdateAsync(field, Ct);
    }

    [Fact]
    public async Task StoresTheDraftAndVersionsAndReadsThemBack()
    {
        await AddAsync("block.image", Image());
        await PublishAsync("block.image", new Dictionary<string, int>());
        await AddAsync("post.news", Post());
        await PublishAsync("post.news", new Dictionary<string, int> { ["block.image"] = 1 });

        await using var context = _database.For("acme");
        var post = (await new EfRepository<FieldDefinition>(context).FirstOrDefaultAsync(new FieldDetailSpec("post.news"), Ct))!;

        post.TenantId.ShouldBe("acme");
        post.Draft.Children.ShouldHaveSingleItem().FieldKey.ShouldBe("block.image");
        post.LatestVersion.ShouldBe(1);
        post.HasDraftChanges.ShouldBeFalse();
        var snapshot = post.GetVersion(1)!.Snapshot;
        snapshot.SchemaType.ShouldBe("NewsArticle");
        snapshot.Children.ShouldBe([new PinnedChild("block.image", 1, 0, 10)]);
    }

    [Fact]
    public async Task AKeyIsUniquePerTenant()
    {
        await AddAsync("block.image", Image());
        await AddAsync("block.image", Image(), tenant: "globex"); // another tenant: fine

        await Should.ThrowAsync<DbUpdateException>(() => AddAsync("block.image", Image("Again")));
    }

    [Fact]
    public async Task EachTenantSeesOnlyItsFields()
    {
        await AddAsync("block.image", Image(), tenant: "globex");

        await using var context = _database.For("acme");
        (await new EfRepository<FieldDefinition>(context).FirstOrDefaultAsync(new FieldDetailSpec("block.image"), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task ListsByKeyFilteredAndCountsAll()
    {
        await AddAsync("post.news", Post());
        await AddAsync("block.image", Image());
        await AddAsync("block.gallery-image", Image("Gallery image"));

        await using var context = _database.For("acme");
        var fields = new EfRepository<FieldDefinition>(context);

        (await fields.ListAsync(new FieldsPageSpec(1, 10), Ct)).Select(f => f.Key).ShouldBe(["block.gallery-image", "block.image", "post.news"]);
        (await fields.ListAsync(new FieldsPageSpec(1, 10, canBeRoot: true), Ct)).Select(f => f.Key).ShouldBe(["post.news"]);
        var search = new FieldsPageSpec(1, 1, search: "IMAGE");
        (await fields.ListAsync(search, Ct)).Select(f => f.Key).ShouldBe(["block.gallery-image"]);
        (await fields.CountAsync(search, Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task TheVersionIsAConcurrencyToken()
    {
        await AddAsync("block.image", Image());

        await using var first = _database.For("acme");
        await using var second = _database.For("acme");
        var mine = await new EfRepository<FieldDefinition>(first).FirstOrDefaultAsync(new FieldByKeySpec("block.image"), Ct);
        var theirs = await new EfRepository<FieldDefinition>(second).FirstOrDefaultAsync(new FieldByKeySpec("block.image"), Ct);

        mine!.EditDraft(Image("Mine"), Now);
        await new EfRepository<FieldDefinition>(first).UpdateAsync(mine, Ct);

        theirs!.EditDraft(Image("Theirs"), Now);
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => new EfRepository<FieldDefinition>(second).UpdateAsync(theirs, Ct));
    }
}

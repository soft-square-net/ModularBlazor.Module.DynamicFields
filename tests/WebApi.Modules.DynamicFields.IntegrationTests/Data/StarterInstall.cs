using soft.square.net.WebApi.Contracts.Security;
using soft.square.net.WebApi.Data;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Catalog;

namespace soft.square.net.WebApi.Modules.DynamicFields.IntegrationTests.Data;

/// <summary>The starter library installed through the real repository: every field and version stored, and again safely.</summary>
public sealed class StarterInstall : IDisposable
{
    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 21, 9, 0, 0, TimeSpan.Zero);
    }

    private readonly DynamicFieldsDatabase _database = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => _database.Dispose();

    private async Task<StarterInstallReport> InstallAsync(IReadOnlyList<string>? keys = null)
    {
        await using var context = _database.For("acme");
        var access = Substitute.For<IAccessScopes>();
        access.UserId.Returns("admin-1");
        var clock = new FixedClock();
        var result = await new InstallStarterFieldsHandler(new EfRepository<FieldDefinition>(context), access, clock)
            .Handle(new InstallStarterFieldsCommand(keys), Ct);
        result.IsSuccess.ShouldBeTrue(string.Join("; ", result.Errors));
        return result.Value;
    }

    [Fact]
    public async Task InstallsTheWholeLibraryPublishedAndThenSkipsIt()
    {
        var first = await InstallAsync();
        var again = await InstallAsync();

        first.Installed.Count.ShouldBe(StarterFields.All.Count);
        again.Installed.ShouldBeEmpty();
        again.Skipped.Count.ShouldBe(StarterFields.All.Count);

        await using var context = _database.For("acme");
        var news = (await new EfRepository<FieldDefinition>(context).FirstOrDefaultAsync(new FieldDetailSpec("post.news"), Ct))!;
        news.LatestVersion.ShouldBe(1);
        news.GetVersion(1)!.Snapshot.Children.Count.ShouldBe(8);
        news.GetVersion(1)!.Snapshot.Properties.ShouldContain(p => p.SchemaProperty == "dateline");

        var roots = await new EfRepository<FieldDefinition>(context).ListAsync(new PublishedRootFieldsSpec(), Ct);
        roots.Select(f => f.Key).ShouldBe(StarterFields.All.Where(f => f.Draft.CanBeRoot).Select(f => f.Key), ignoreOrder: true);
        roots.ShouldContain(f => f.Key == "post.photo-story" && f.PublishedUrlPrefix == "photo-stories");
        roots.Select(f => f.PublishedUrlPrefix).ShouldBeUnique();
    }
}

using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

public class FieldPublishingTests
{
    private static readonly Dictionary<string, int> Children = new() { ["block.paragraph"] = 3, ["block.image"] = 1 };

    [Fact]
    public void PublishingFreezesTheDraftAndPinsTheChildren()
    {
        var field = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now);

        var version = field.Publish(Children, "editor-1", Now.AddHours(1));

        version.Number.ShouldBe(1);
        version.PublishedBy.ShouldBe("editor-1");
        version.Snapshot.Children.ShouldBe([new PinnedChild("block.paragraph", 3, 1, null), new PinnedChild("block.image", 1, 0, 10)]);
        field.LatestVersion.ShouldBe(1);
        field.HasDraftChanges.ShouldBeFalse();
        field.Version.ShouldBe(1);
    }

    [Fact]
    public void AChildMustBePublishedFirst() =>
        Should.Throw<DomainException>(() => FieldDefinition.Create("post.news", NewsPost(), "author-1", Now)
                .Publish(new Dictionary<string, int> { ["block.paragraph"] = 1 }, "editor-1", Now))
            .Message.ShouldContain("block.image");

    [Fact]
    public void NothingToPublishWithoutChanges()
    {
        var field = FieldDefinition.Create("block.paragraph", Paragraph(), "author-1", Now);
        field.Publish(new Dictionary<string, int>(), "editor-1", Now);

        Should.Throw<DomainException>(() => field.Publish(new Dictionary<string, int>(), "editor-1", Now));
    }

    [Fact]
    public void AnEditAfterPublishingMakesTheNextVersionAndLeavesTheOldOne()
    {
        var field = FieldDefinition.Create("block.paragraph", Paragraph(), "author-1", Now);
        field.Publish(new Dictionary<string, int>(), "editor-1", Now);

        field.EditDraft(Paragraph() with { Title = "Body text" }, Now.AddDays(1));
        field.HasDraftChanges.ShouldBeTrue();
        var second = field.Publish(new Dictionary<string, int>(), "editor-1", Now.AddDays(1));

        second.Number.ShouldBe(2);
        field.GetVersion(1)!.Snapshot.Title.ShouldBe("Paragraph");
        field.GetVersion(2)!.Snapshot.Title.ShouldBe("Body text");
    }

    [Fact]
    public void TheSnapshotSurvivesStorage()
    {
        var version = FieldDefinition.Create("post.news", NewsPost(), "author-1", Now).Publish(Children, "editor-1", Now);

        var stored = System.Text.Json.JsonSerializer.Deserialize<FieldSnapshot>(version.SnapshotJson, FieldJson.Options)!;

        stored.Key.ShouldBe("post.news");
        stored.Properties.Single(p => p.Key == "topics").Kind.ShouldBe(PropertyKind.Keyword);
        stored.Properties.Single(p => p.Key == "section").Options.ShouldBe(["news", "sports", "culture"]);
        version.SnapshotJson.ShouldContain("\"kind\":\"Keyword\"");
    }
}

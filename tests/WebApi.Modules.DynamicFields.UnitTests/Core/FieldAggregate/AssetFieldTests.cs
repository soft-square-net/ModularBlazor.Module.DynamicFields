using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

/// <summary>The Asset kind's accept list: its rules, the catalog's and starter library's defaults, the JSON Schema.</summary>
public class AssetFieldTests
{
    private static FieldDraft Draft(params FieldProperty[] properties) =>
        new("Media block", "MediaObject", false, properties, [], []);

    private static FieldProperty Checked(FieldProperty p) => FieldRules.Check("block.media", Draft(p)).Properties.Single();

    private static FieldProperty Asset(params string[] accept) =>
        new("file", "File", PropertyKind.Asset, Accept: accept);

    [Fact]
    public void AnAcceptListIsTrimmedLowercasedAndWithoutRepeats() =>
        Checked(Asset(" Image/* ", "image/*", ".PDF", "video/mp4")).Accept.ShouldBe(["image/*", ".pdf", "video/mp4"]);

    [Fact]
    public void AnEmptyAcceptListMeansAnyFile() =>
        Checked(Asset()).Accept.ShouldBeNull();

    [Theory]
    [InlineData("image")]
    [InlineData("*/*x")]
    [InlineData("pdf")]
    [InlineData(".")]
    [InlineData("image/png; q=1")]
    [InlineData("")]
    public void AnEntryIsAContentTypeAFamilyOrAnExtension(string entry) =>
        Should.Throw<DomainException>(() => Checked(Asset(entry)));

    [Fact]
    public void AnAcceptListHasAtMostTwentyEntries() =>
        Should.Throw<DomainException>(() => Checked(Asset([.. Enumerable.Range(0, FieldRules.MaxAccept + 1).Select(i => $".x{i}")])));

    [Fact]
    public void OnlyAnAssetHasAnAcceptList() =>
        Should.Throw<DomainException>(() => Checked(new FieldProperty("title", "Title", PropertyKind.Text, Accept: ["image/*"])));

    [Fact]
    public void TheCatalogSuggestsImagesForImagesAndTheMediaTypeForContentUrl()
    {
        SchemaOrgCatalog.ToFieldProperty("image").Accept.ShouldBe(["image/*"]);
        SchemaOrgCatalog.AcceptFor("ImageObject", "contentUrl").ShouldBe(["image/*"]);
        SchemaOrgCatalog.AcceptFor("VideoObject", "contentUrl").ShouldBe(["video/*"]);
        SchemaOrgCatalog.AcceptFor("AudioObject", "contentUrl").ShouldBe(["audio/*"]);
        SchemaOrgCatalog.AcceptFor("MediaObject", "contentUrl").ShouldBeNull(); // any file
        SchemaOrgCatalog.ToFieldProperty("contentUrl", type: "VideoObject").Accept.ShouldBe(["video/*"]);
    }

    [Theory]
    [InlineData("block.image", "image", "image/*")]
    [InlineData("block.video", "video", "video/*")]
    [InlineData("block.audio", "audio", "audio/*")]
    public void StarterMediaBlocksAcceptTheirMedia(string field, string key, string accept)
    {
        var draft = StarterFields.All.Single(f => f.Key == field).Draft;
        FieldRules.Check(field, draft).Properties.Single(p => p.Key == key).Accept.ShouldBe([accept]);
    }

    [Fact]
    public void TheJsonSchemaCarriesTheAcceptList()
    {
        var field = new FieldSnapshot("block.media", 1, "Media block", null, "MediaObject", false, [Checked(Asset("image/*"))], [], [], null);
        var schema = FieldJsonSchema.Content(field)["properties"]!["file"]!;
        schema["x-accept"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["image/*"]);
        schema["x-reference"]!.GetValue<string>().ShouldBe("asset");
    }
}

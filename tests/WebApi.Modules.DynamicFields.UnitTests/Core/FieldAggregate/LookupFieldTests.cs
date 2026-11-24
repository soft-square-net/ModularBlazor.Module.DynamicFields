using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Publish;
using static soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Samples;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

/// <summary>The Lookup kind: the binding's rules, the value's shape, the check against the owner, the catalog check.</summary>
public class LookupFieldTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static FieldProperty Lookup(string key, LookupBinding binding, bool multiple = false, bool required = false) =>
        new(key, key, PropertyKind.Lookup, required, multiple, Lookup: binding);

    private static FieldDraft Draft(params FieldProperty[] properties) =>
        new("Stock block", "CreativeWork", false, properties, [], []);

    private static FieldDraft Checked(params FieldProperty[] properties) => FieldRules.Check("block.stock", Draft(properties));

    // ---- rules --------------------------------------------------------------------------------------------------

    [Fact]
    public void ABindingIsTrimmedAndKept()
    {
        var p = Checked(Lookup("item", new LookupBinding(" inventory.stock-items ", SearchFields: ["Name", "Sku"],
            PresetFilters: [new("Kind", "eq", " 25 ")]))).Properties.Single();

        p.Lookup!.Key.ShouldBe("inventory.stock-items");
        p.Lookup.PresetFilters!.Single().Value.ShouldBe("25");
        p.Lookup.AllowUserFilters.ShouldBeFalse(); // only a picker lets the editor add filters
    }

    [Theory]
    [InlineData("stock-items")]
    [InlineData("inventory.")]
    [InlineData("inventory stock")]
    public void ALookupKeyIsPrefixDotName(string key) =>
        Should.Throw<DomainException>(() => Checked(Lookup("item", new LookupBinding(key))));

    [Fact]
    public void ALookupPropertyNeedsABindingAndOnlyItHasOne()
    {
        Should.Throw<DomainException>(() => Checked(new FieldProperty("item", "Item", PropertyKind.Lookup)));
        Should.Throw<DomainException>(() => Checked(new FieldProperty("name", "Name", PropertyKind.Text, Lookup: new LookupBinding("a.b"))));
    }

    [Fact]
    public void ARecursiveLookupPicksOnePathWithACascade()
    {
        Should.Throw<DomainException>(() => Checked(Lookup("c", new LookupBinding("content.categories", Recursive: true), multiple: true)))
            .Message.ShouldContain("not a list");
        Should.Throw<DomainException>(() => Checked(Lookup("c", new LookupBinding("content.categories", Mode: LookupMode.Picker, Recursive: true))));
        Should.Throw<DomainException>(() => Checked(Lookup("c", new LookupBinding("content.categories", Recursive: true, MaxDepth: 11))));
        Should.Throw<DomainException>(() => Checked(Lookup("c", new LookupBinding("content.categories", RootKey: "3"))))
            .Message.ShouldContain("recursive");
    }

    [Fact]
    public void ADependentLookupsParentIsASingleLookupOfTheField()
    {
        var country = Lookup("country", new LookupBinding("geo.countries"));
        var city = Lookup("city", new LookupBinding("geo.cities", ParentProperty: "country"));

        Checked(country, city).Properties.Count.ShouldBe(2);
        Should.Throw<DomainException>(() => Checked(city)).Message.ShouldContain("not a Lookup property");
        Should.Throw<DomainException>(() => Checked(country with { Multiple = true }, city)).Message.ShouldContain("one value");
        Should.Throw<DomainException>(() => Checked(
            Lookup("a", new LookupBinding("x.a", ParentProperty: "b")),
            Lookup("b", new LookupBinding("x.b", ParentProperty: "a")))).Message.ShouldContain("loop");
    }

    [Theory]
    [InlineData("like", "x", null)]
    [InlineData("in", null, null)]
    [InlineData("between", null, "1")]
    [InlineData("eq", null, null)]
    public void PresetFiltersAreChecked(string op, string? value, string? oneValue) =>
        Should.Throw<DomainException>(() => Checked(Lookup("item", new LookupBinding("inventory.items",
            PresetFilters: [new("Kind", op, value, oneValue is null ? null : [oneValue])]))));

    [Fact]
    public void MaxSelectionsIsForAMultipleLookup() =>
        Should.Throw<DomainException>(() => Checked(Lookup("item", new LookupBinding("inventory.items", MaxSelections: 3))));

    // ---- shape --------------------------------------------------------------------------------------------------

    private static FieldSnapshot Snapshot(params FieldProperty[] properties) =>
        Publish("block.stock", Draft(properties), new Dictionary<string, int>());

    private static IReadOnlyList<ContentIssue> Shape(FieldSnapshot field, string content) =>
        ContentTreeValidator.ValidateValues(field, Json(content), Json("{}"));

    [Fact]
    public void ALookupValueIsKeyAndText()
    {
        var field = Snapshot(Lookup("item", new LookupBinding("inventory.items")));

        Shape(field, """{ "item": { "key": "42", "text": "Gear" } }""").ShouldBeEmpty();
        Shape(field, """{ "item": "42" }""").ShouldHaveSingleItem().Path.ShouldBe("/content/item");
        Shape(field, """{ "item": { "text": "Gear" } }""").ShouldHaveSingleItem().Path.ShouldBe("/content/item/key");
        Shape(field, """{ "item": { "key": "42", "path": [] } }""").ShouldHaveSingleItem().Path.ShouldBe("/content/item/path");
    }

    [Fact]
    public void ARecursiveValueHasAPathWithinTheDepth()
    {
        var field = Snapshot(Lookup("place", new LookupBinding("geo.places", Recursive: true, MaxDepth: 2)));

        Shape(field, """{ "place": { "key": "7", "text": "Cairo", "path": [{ "key": "1", "text": "Egypt" }] } }""").ShouldBeEmpty();
        Shape(field, """{ "place": { "key": "9", "path": [{ "key": "1" }, { "key": "7" }] } }""")
            .ShouldHaveSingleItem().Path.ShouldBe("/content/place/path");
    }

    [Fact]
    public void AMultipleLookupHasAtMostMaxSelections()
    {
        var field = Snapshot(Lookup("items", new LookupBinding("inventory.items", MaxSelections: 1), multiple: true));

        Shape(field, """{ "items": [{ "key": "1" }, { "key": "2" }] }""").ShouldHaveSingleItem().Message.ShouldContain("at most 1");
    }

    [Fact]
    public void TheJsonSchemaNamesTheLookup()
    {
        var schema = FieldJsonSchema.Content(Snapshot(Lookup("place", new LookupBinding("geo.places", Recursive: true))));
        var place = schema["properties"]!["place"]!;

        place["x-lookup"]!.GetValue<string>().ShouldBe("geo.places");
        place["properties"]!["path"]!["maxItems"]!.GetValue<int>().ShouldBe(4);
    }

    // ---- against the owner --------------------------------------------------------------------------------------

    /// <summary>A small owner: geo.places is a tree (Egypt › Cairo › Maadi), geo.cities depends on geo.countries.</summary>
    private sealed class Owner : ILookupClient
    {
        public List<(string Key, IReadOnlyList<LookupFilter>? Filters)> Calls { get; } = [];
        public bool Down { get; init; }

        private static readonly Dictionary<string, LookupItem[]> Items = new()
        {
            ["geo.places"] = [
                new("1", "Egypt") { HasChildren = true },
                new("7", "Cairo") { ParentKey = "1", HasChildren = true },
                new("9", "Maadi") { ParentKey = "7", HasChildren = false },
                new("2", "Jordan") { HasChildren = false }],
            ["geo.countries"] = [new("eg", "Egypt"), new("jo", "Jordan")],
            ["geo.cities"] = [new("cai", "Cairo") { ParentKey = "eg" }, new("amm", "Amman") { ParentKey = "jo" }],
            ["inventory.items"] = [new("1", "Gear"), new("2", "Widget")]
        };

        public Task<IReadOnlyList<LookupItem>> ResolveAsync(string key, IReadOnlyCollection<string> keys, IReadOnlyList<LookupFilter>? filters = null, CancellationToken ct = default)
        {
            if (Down) throw new LookupUnavailableException(key, "no pool serves it");
            Calls.Add((key, filters));
            // The preset filter Kind = gear keeps item 1 only.
            var items = Items[key].Where(i => keys.Contains(i.Key) && (filters is null || i.Key == "1"));
            return Task.FromResult<IReadOnlyList<LookupItem>>(items.ToList());
        }

        public Task<IReadOnlyList<LookupDescriptor>> CatalogAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<LookupDescriptor>>([
                new("geo.places", "Module.Geo", "places", "Places", null, null, ["Name"], [], [], "ParentId", "tree"),
                new("geo.cities", "Module.Geo", "cities", "Cities", null, null, ["Name"], [], [], "CountryId", "dependent"),
                new("inventory.items", "Module.Inventory", "items", "Items", null, null, ["Name", "Sku"],
                    [new LookupFilterField("Kind", "Kind", "text", ["eq", "in"])], [], null, null)
            ]);

        public Task<LookupPage> SearchAsync(string key, LookupQuery query, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private static async Task<List<ContentIssue>> Owned(FieldSnapshot field, string content, ILookupClient owner)
    {
        var issues = new List<ContentIssue>();
        await LookupReferences.CheckAsync([(new ContentNode(field.Key, field.Version, Json(content), Json("{}")), field, "")], owner, issues, Ct);
        return issues;
    }

    [Fact]
    public async Task AKeyMustExistAndPassThePresetFilters()
    {
        var owner = new Owner();
        var field = Snapshot(Lookup("items", new LookupBinding("inventory.items", PresetFilters: [new("Kind", "eq", "gear")]), multiple: true));

        var issues = await Owned(field, """{ "items": [{ "key": "1" }, { "key": "2" }, { "key": "404" }] }""", owner);

        issues.Select(i => i.Path).ShouldBe(["/content/items/1/key", "/content/items/2/key"]);
        owner.Calls.ShouldHaveSingleItem().Filters!.Single().Field.ShouldBe("Kind"); // one request for all the values
    }

    [Fact]
    public async Task ADependentValueBelongsToItsParentsValue()
    {
        var field = Snapshot(
            Lookup("country", new LookupBinding("geo.countries")),
            Lookup("city", new LookupBinding("geo.cities", ParentProperty: "country")));

        (await Owned(field, """{ "country": { "key": "eg" }, "city": { "key": "cai" } }""", new Owner())).ShouldBeEmpty();
        (await Owned(field, """{ "country": { "key": "eg" }, "city": { "key": "amm" } }""", new Owner()))
            .ShouldHaveSingleItem().Path.ShouldBe("/content/city/key");
        (await Owned(field, """{ "city": { "key": "cai" } }""", new Owner()))
            .ShouldHaveSingleItem().Message.ShouldContain("first");
    }

    [Fact]
    public async Task ARecursivePathIsAChainFromTheRoot()
    {
        var field = Snapshot(Lookup("place", new LookupBinding("geo.places", Recursive: true, LeafOnly: true)));

        (await Owned(field, """{ "place": { "key": "9", "path": [{ "key": "1" }, { "key": "7" }] } }""", new Owner())).ShouldBeEmpty();
        (await Owned(field, """{ "place": { "key": "9", "path": [{ "key": "2" }, { "key": "7" }] } }""", new Owner()))
            .ShouldHaveSingleItem().Path.ShouldBe("/content/place/path/1"); // Cairo is not under Jordan
        (await Owned(field, """{ "place": { "key": "7", "path": [{ "key": "1" }] } }""", new Owner()))
            .ShouldHaveSingleItem().Message.ShouldContain("more specific");
    }

    [Fact]
    public async Task ARecursivePathStartsUnderTheRootKey()
    {
        var field = Snapshot(Lookup("place", new LookupBinding("geo.places", Recursive: true, RootKey: "1")));

        (await Owned(field, """{ "place": { "key": "9", "path": [{ "key": "7" }] } }""", new Owner())).ShouldBeEmpty();
        (await Owned(field, """{ "place": { "key": "2" } }""", new Owner())).ShouldHaveSingleItem().Message.ShouldContain("starting item");
    }

    [Fact]
    public async Task AnUnreachableLookupIsAnIssueNotASilentPass()
    {
        var field = Snapshot(Lookup("item", new LookupBinding("inventory.items")));

        (await Owned(field, """{ "item": { "key": "1" } }""", new Owner { Down = true }))
            .ShouldHaveSingleItem().Message.ShouldContain("could not be checked");
    }

    // ---- publishing ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task PublishingChecksTheBindingAgainstTheCatalog()
    {
        var ok = Checked(
            Lookup("place", new LookupBinding("geo.places", Recursive: true)),
            Lookup("items", new LookupBinding("inventory.items", SearchFields: ["Sku"], PresetFilters: [new("Kind", "in", Values: ["a"])]), multiple: true));
        (await LookupBindings.CheckAsync(ok, new Owner(), Ct)).ShouldBeEmpty();

        var bad = Checked(
            Lookup("missing", new LookupBinding("shop.orders")),
            Lookup("items", new LookupBinding("inventory.items", SearchFields: ["Price"], PresetFilters: [new("Kind", "gt", "3")], Recursive: false)),
            Lookup("city", new LookupBinding("inventory.items", Recursive: true)));
        var errors = await LookupBindings.CheckAsync(bad, new Owner(), Ct);

        errors.Select(e => e.Identifier).ShouldBe([
            "properties.missing.lookup.key",
            "properties.items.lookup.searchFields",
            "properties.items.lookup.presetFilters",
            "properties.city.lookup.recursive"]);
    }
}

using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Catalog;

/// <summary>
/// A schema.org type for the builder. <see cref="Path"/> runs from CreativeWork to the type; <see cref="Properties"/>
/// are every property it has (its own first, then inherited); <see cref="Suggested"/> are the ones to tick when picked;
/// <see cref="ContentAccept"/> are the files its contentUrl takes (null: any file).
/// </summary>
public sealed record SchemaTypeDto(
    string Name,
    string? Parent,
    string Description,
    IReadOnlyList<string> Path,
    IReadOnlyList<string> Properties,
    IReadOnlyList<string> Suggested,
    IReadOnlyList<string>? ContentAccept);

/// <summary>What the field builder works from: the schema.org types and properties, and the common style choices.</summary>
public sealed record SchemaOrgCatalogDto(
    IReadOnlyList<SchemaTypeDto> Types,
    IReadOnlyList<SchemaOrgProperty> Properties,
    IReadOnlyList<StyleProperty> StylePresets);

/// <summary>The curated schema.org catalog and the style presets the builder offers.</summary>
public sealed record GetSchemaOrgCatalogQuery : IQuery<Result<SchemaOrgCatalogDto>>;

public sealed class GetSchemaOrgCatalogHandler : IQueryHandler<GetSchemaOrgCatalogQuery, Result<SchemaOrgCatalogDto>>
{
    private static readonly SchemaOrgCatalogDto Catalog = new(
        [.. SchemaOrgCatalog.Types.Select(t => new SchemaTypeDto(t.Name, t.Parent, t.Description, SchemaOrgCatalog.PathOf(t.Name),
            [.. SchemaOrgCatalog.PropertiesOf(t.Name).Select(p => p.Name)], SchemaOrgCatalog.SuggestedFor(t.Name),
            SchemaOrgCatalog.ContentAcceptOf(t.Name)))],
        [.. SchemaOrgCatalog.Properties.Values.OrderBy(p => p.Name, StringComparer.Ordinal)],
        StylePresets.All);

    public ValueTask<Result<SchemaOrgCatalogDto>> Handle(GetSchemaOrgCatalogQuery query, CancellationToken ct) =>
        ValueTask.FromResult(Result<SchemaOrgCatalogDto>.Success(Catalog));
}

/// <summary>
/// A field of the starter library: what it is, the fields it may contain (installed with it), and whether the tenant
/// already has a field with its key (installing skips it).
/// </summary>
public sealed record StarterFieldDto(
    string Key,
    string Title,
    string? Description,
    string SchemaType,
    bool CanBeRoot,
    IReadOnlyList<string> Children,
    bool Installed,
    FieldDraft Draft);

/// <summary>The starter library, marked with what the tenant has already.</summary>
public sealed record ListStarterFieldsQuery : IQuery<Result<IReadOnlyList<StarterFieldDto>>>;

public sealed class ListStarterFieldsHandler(IReadRepository<FieldDefinition> fields)
    : IQueryHandler<ListStarterFieldsQuery, Result<IReadOnlyList<StarterFieldDto>>>
{
    public async ValueTask<Result<IReadOnlyList<StarterFieldDto>>> Handle(ListStarterFieldsQuery query, CancellationToken ct)
    {
        var keys = StarterFields.All.Select(f => f.Key).ToList();
        var existing = (await fields.ListAsync(new FieldsByKeysSpec(keys), ct)).Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        return Result<IReadOnlyList<StarterFieldDto>>.Success(
        [
            .. StarterFields.All.Select(f => new StarterFieldDto(f.Key, f.Draft.Title, f.Draft.Description, f.Draft.SchemaType,
                f.Draft.CanBeRoot, [.. f.Draft.Children.Select(c => c.FieldKey)], existing.Contains(f.Key), f.Draft))
        ]);
    }
}

/// <summary>
/// The tenant's published root post fields by key: each post kind with its static URL, so a posts module can resolve
/// a kind key (<c>post.news</c>) to its URL prefix (<c>news</c>) and build public URLs.
/// </summary>
public sealed record ListRootPostFieldsQuery : IQuery<Result<IReadOnlyList<RootPostFieldDto>>>;

public sealed class ListRootPostFieldsHandler(IReadRepository<FieldDefinition> fields)
    : IQueryHandler<ListRootPostFieldsQuery, Result<IReadOnlyList<RootPostFieldDto>>>
{
    public async ValueTask<Result<IReadOnlyList<RootPostFieldDto>>> Handle(ListRootPostFieldsQuery query, CancellationToken ct)
    {
        var roots = await fields.ListAsync(new PublishedRootFieldsSpec(), ct);
        return Result<IReadOnlyList<RootPostFieldDto>>.Success([.. roots.Select(RootPostFieldDto.From).OfType<RootPostFieldDto>()]);
    }
}

/// <summary>
/// What installing did: <see cref="Installed"/> were created and published; <see cref="Skipped"/> existed already (left
/// as they are); <see cref="Drafts"/> were created but not published, because a child field they need exists unpublished,
/// or a post's URL prefix (<c>news</c>, ...) belongs to another published post field of the tenant.
/// </summary>
public sealed record StarterInstallReport(IReadOnlyList<string> Installed, IReadOnlyList<string> Skipped, IReadOnlyList<string> Drafts);

/// <summary>
/// Install starter fields (none given: all of them) with the fields they contain, each created and published as
/// version 1. A key the tenant has already is skipped, never overwritten, so installing again is safe.
/// </summary>
public sealed record InstallStarterFieldsCommand(IReadOnlyList<string>? Keys) : ICommand<Result<StarterInstallReport>>;

public sealed class InstallStarterFieldsHandler(IRepository<FieldDefinition> fields, IAccessScopes access, TimeProvider clock)
    : ICommandHandler<InstallStarterFieldsCommand, Result<StarterInstallReport>>
{
    public async ValueTask<Result<StarterInstallReport>> Handle(InstallStarterFieldsCommand command, CancellationToken ct)
    {
        if (access.UserId is not { Length: > 0 } me) return Result<StarterInstallReport>.Unauthorized("Sign in first.");

        var asked = command.Keys is { Count: > 0 } keys ? keys.Select(k => k.Trim()).ToList() : [.. StarterFields.All.Select(f => f.Key)];
        var unknown = asked.Where(k => StarterFields.Find(k) is null).ToList();
        if (unknown.Count > 0) return Result<StarterInstallReport>.NotFound($"Not in the starter library: {string.Join(", ", unknown)}.");

        var order = StarterFields.WithChildren(asked);
        var existing = await fields.ListAsync(new FieldsByKeysSpec([.. order]), ct);
        var published = existing.Where(f => f.LatestVersion > 0).ToDictionary(f => f.Key, f => f.LatestVersion, StringComparer.Ordinal);
        var have = existing.Select(f => f.Key).ToHashSet(StringComparer.Ordinal);
        var taken = (await fields.ListAsync(new TakenUrlPrefixesSpec(), ct)).ToHashSet(StringComparer.Ordinal);

        List<string> installed = [], skipped = [], drafts = [];
        var now = clock.GetUtcNow();
        foreach (var key in order)
        {
            if (have.Contains(key))
            {
                skipped.Add(key);
                continue;
            }

            var starter = StarterFields.Find(key)!;
            var field = FieldDefinition.Create(key, starter.Draft, me, now);
            if (starter.Draft.Children.All(c => published.ContainsKey(c.FieldKey))
                && (starter.Draft.UrlPrefix is not { } prefix || !taken.Contains(prefix)))
            {
                field.Publish(published, me, now, taken);
                published[key] = field.LatestVersion;
                if (field.PublishedUrlPrefix is { } own) taken.Add(own);
                installed.Add(key);
            }
            else drafts.Add(key);

            await fields.AddAsync(field, ct);
        }

        return new StarterInstallReport(installed, skipped, drafts);
    }
}

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate.Specifications;

/// <summary>One field by its key, with its versions, tracked (for changing it).</summary>
public sealed class FieldByKeySpec : SingleResultSpecification<FieldDefinition>
{
    public FieldByKeySpec(string key) => Query.Where(f => f.Key == key).Include(f => f.Versions);
}

/// <summary>One field by its key, with its versions, untracked (for reading it).</summary>
public sealed class FieldDetailSpec : SingleResultSpecification<FieldDefinition>
{
    public FieldDetailSpec(string key) => Query.Where(f => f.Key == key).Include(f => f.Versions).AsNoTracking();
}

/// <summary>The fields with these keys, with their versions, untracked (to pin children and to validate content).</summary>
public sealed class FieldsByKeysSpec : Specification<FieldDefinition>
{
    public FieldsByKeysSpec(IReadOnlyCollection<string> keys) =>
        Query.Where(f => keys.Contains(f.Key)).Include(f => f.Versions).AsNoTracking();
}

/// <summary>
/// The tenant's published root fields (those with a <see cref="FieldDefinition.PublishedUrlPrefix"/>) by key, with
/// their versions, untracked: the post kinds and their static URLs.
/// </summary>
public sealed class PublishedRootFieldsSpec : Specification<FieldDefinition>
{
    public PublishedRootFieldsSpec() =>
        Query.Where(f => f.PublishedUrlPrefix != null).OrderBy(f => f.Key).Include(f => f.Versions).AsNoTracking();
}

/// <summary>The URL prefixes of the tenant's published root fields other than <paramref name="exceptKey"/>.</summary>
public sealed class TakenUrlPrefixesSpec : Specification<FieldDefinition, string>
{
    public TakenUrlPrefixesSpec(string? exceptKey = null)
    {
        Query.Where(f => f.PublishedUrlPrefix != null && f.Key != exceptKey).AsNoTracking();
        Query.Select(f => f.PublishedUrlPrefix!);
    }
}

/// <summary>
/// One page of fields by key, untracked, optionally only those whose title or key contains a text, or only those that
/// can be a post's root. <c>CountAsync</c> with the same specification ignores the paging, so it gives the total.
/// </summary>
public sealed class FieldsPageSpec : Specification<FieldDefinition>
{
    public FieldsPageSpec(int page, int pageSize, string? search = null, bool? canBeRoot = null)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            Query.Where(f => f.Key.Contains(term) || f.Title.ToLower().Contains(term));
        }
        if (canBeRoot is { } root) Query.Where(f => f.CanBeRoot == root);
        Query.OrderBy(f => f.Key).Skip((page - 1) * pageSize).Take(pageSize).AsNoTracking();
    }
}

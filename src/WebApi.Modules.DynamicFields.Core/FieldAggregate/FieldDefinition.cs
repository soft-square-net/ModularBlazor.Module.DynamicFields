using System.Text.Json;
using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// A field: one kind of content (a news post, a paragraph, an image, an album), as one schema.org type with its
/// values, its style choices and the fields it may contain. Authors change the <see cref="Draft"/>; publishing freezes
/// it as the next <see cref="FieldVersion"/>, which content then names. Fields are reusable: an image field can be a
/// child of a news post, an album and a reportage, each pinning the image version current when it was published.
/// </summary>
public sealed class FieldDefinition : AggregateRoot<int>, ITenantOwned
{
    private readonly List<FieldVersion> _versions = [];
    private FieldDraft? _draft;

    private FieldDefinition() { } // EF Core

    /// <summary>Set by the platform's TenantDbContext when the field is saved.</summary>
    public string TenantId { get; set; } = "";

    /// <summary>The stable key content refers to (<c>post.news</c>, <c>block.image</c>); never changes.</summary>
    public string Key { get; private set; } = "";

    /// <summary>The draft as stored (JSON); read it through <see cref="Draft"/>.</summary>
    public string DraftJson { get; private set; } = "{}";

    /// <summary>The draft's title, kept in a column for lists and search.</summary>
    public string Title { get; private set; } = "";

    /// <summary>The draft's schema.org type, kept in a column for lists.</summary>
    public string SchemaType { get; private set; } = "";

    /// <summary>Whether the draft can be a post's root, kept in a column for lists.</summary>
    public bool CanBeRoot { get; private set; }

    /// <summary>The newest published version; 0 when the field has never been published.</summary>
    public int LatestVersion { get; private set; }

    /// <summary>Whether the draft has changed since the newest version was published (true before the first).</summary>
    public bool HasDraftChanges { get; private set; }

    public string CreatedBy { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Bumped by every change; a concurrency token, so a stale edit fails instead of overwriting.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<FieldVersion> Versions => _versions;

    public FieldDraft Draft => _draft ??= JsonSerializer.Deserialize<FieldDraft>(DraftJson, FieldJson.Options)!;

    public static FieldDefinition Create(string key, FieldDraft draft, string createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(createdBy)) throw new DomainException("A field needs its author.");
        var field = new FieldDefinition { Key = FieldRules.CheckKey(key), CreatedBy = createdBy, CreatedAt = now };
        field.SetDraft(draft, now);
        return field;
    }

    public void EditDraft(FieldDraft draft, DateTimeOffset now)
    {
        SetDraft(draft, now);
        Version++;
    }

    /// <summary>
    /// Publishes the draft as the next version. <paramref name="publishedChildren"/> gives the newest published version of
    /// each child field (by key); every child the draft allows must have one, and is pinned to it.
    /// </summary>
    public FieldVersion Publish(IReadOnlyDictionary<string, int> publishedChildren, string publishedBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(publishedBy)) throw new DomainException("Publishing needs its author.");
        if (!HasDraftChanges) throw new DomainException($"Field '{Key}' has no changes since version {LatestVersion}.");

        var draft = Draft;
        var missing = draft.Children.Where(c => !publishedChildren.ContainsKey(c.FieldKey)).Select(c => c.FieldKey).ToList();
        if (missing.Count > 0)
            throw new DomainException($"Publish these child fields first: {string.Join(", ", missing)}.");

        var snapshot = new FieldSnapshot(Key, LatestVersion + 1, draft.Title, draft.Description, draft.SchemaType, draft.CanBeRoot,
            draft.Properties, draft.Style,
            [.. draft.Children.Select(c => new PinnedChild(c.FieldKey, publishedChildren[c.FieldKey], c.Min, c.Max))],
            draft.EditorDialog);

        var version = new FieldVersion(snapshot, publishedBy, now);
        _versions.Add(version);
        LatestVersion = snapshot.Version;
        HasDraftChanges = false;
        UpdatedAt = now;
        Version++;
        return version;
    }

    /// <summary>A published version, or null.</summary>
    public FieldVersion? GetVersion(int number) => _versions.FirstOrDefault(v => v.Number == number);

    private void SetDraft(FieldDraft draft, DateTimeOffset now)
    {
        var checkedDraft = FieldRules.Check(Key, draft);
        DraftJson = JsonSerializer.Serialize(checkedDraft, FieldJson.Options);
        _draft = checkedDraft;
        Title = checkedDraft.Title;
        SchemaType = checkedDraft.SchemaType;
        CanBeRoot = checkedDraft.CanBeRoot;
        HasDraftChanges = true;
        UpdatedAt = now;
    }
}

/// <summary>One published, immutable version of a field.</summary>
public sealed class FieldVersion : EntityBase<int>
{
    private FieldSnapshot? _snapshot;

    private FieldVersion() { } // EF Core

    public FieldVersion(FieldSnapshot snapshot, string publishedBy, DateTimeOffset publishedAt)
    {
        Number = snapshot.Version;
        SnapshotJson = JsonSerializer.Serialize(snapshot, FieldJson.Options);
        _snapshot = snapshot;
        PublishedBy = publishedBy;
        PublishedAt = publishedAt;
    }

    public int Number { get; private set; }

    /// <summary>The snapshot as stored (JSON); read it through <see cref="Snapshot"/>.</summary>
    public string SnapshotJson { get; private set; } = "{}";

    public string PublishedBy { get; private set; } = "";
    public DateTimeOffset PublishedAt { get; private set; }

    public FieldSnapshot Snapshot => _snapshot ??= JsonSerializer.Deserialize<FieldSnapshot>(SnapshotJson, FieldJson.Options)!;
}

/// <summary>How drafts and snapshots are stored: web JSON (camelCase), enums by name.</summary>
public static class FieldJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

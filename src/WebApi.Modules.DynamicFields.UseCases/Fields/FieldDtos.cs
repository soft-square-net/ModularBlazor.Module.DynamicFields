using System.Text.Json;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields;

/// <summary>
/// A field in a list. <see cref="Version"/> goes back with an edit, a publish or a delete. <see cref="UrlPrefix"/> is the
/// published URL prefix of a root field (null: not a published root).
/// </summary>
public sealed record FieldSummaryDto(
    string Key, string Title, string SchemaType, bool CanBeRoot, int LatestVersion, bool HasDraftChanges,
    DateTimeOffset UpdatedAt, int Version, string? UrlPrefix = null)
{
    public static FieldSummaryDto From(FieldDefinition f) =>
        new(f.Key, f.Title, f.SchemaType, f.CanBeRoot, f.LatestVersion, f.HasDraftChanges, f.UpdatedAt, f.Version, f.PublishedUrlPrefix);
}

/// <summary>
/// A published root post field: a post kind (<see cref="Key"/>, e.g. <c>post.news</c>) and its static URL
/// (<see cref="UrlPrefix"/>, e.g. <c>news</c>: the public route prefix of posts of this kind). Title, description and
/// schema type are the newest published version's (<see cref="Version"/>).
/// </summary>
public sealed record RootPostFieldDto(string Key, string Title, string? Description, string SchemaType, string UrlPrefix, int Version)
{
    /// <summary>The field as a post kind, or null when its newest version is not a published root.</summary>
    public static RootPostFieldDto? From(FieldDefinition f) =>
        f.PublishedUrlPrefix is { } prefix && f.GetVersion(f.LatestVersion)?.Snapshot is { CanBeRoot: true } s
            ? new(f.Key, s.Title, s.Description, s.SchemaType, prefix, s.Version)
            : null;
}

/// <summary>One published version in a field's history.</summary>
public sealed record FieldVersionInfo(int Number, DateTimeOffset PublishedAt, string PublishedBy);

/// <summary>A field with its draft and the list of its published versions.</summary>
public sealed record FieldDetailDto(
    string Key, FieldDraft Draft, int LatestVersion, bool HasDraftChanges, IReadOnlyList<FieldVersionInfo> Versions,
    string CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version)
{
    public static FieldDetailDto From(FieldDefinition f) =>
        new(f.Key, f.Draft, f.LatestVersion, f.HasDraftChanges,
            [.. f.Versions.OrderBy(v => v.Number).Select(v => new FieldVersionInfo(v.Number, v.PublishedAt, v.PublishedBy))],
            f.CreatedBy, f.CreatedAt, f.UpdatedAt, f.Version);
}

/// <summary>
/// A published version: what content made with it must match (<see cref="Field"/>), and the same as standard JSON
/// Schema for the content and the style.
/// </summary>
public sealed record FieldVersionDto(
    FieldSnapshot Field, DateTimeOffset PublishedAt, string PublishedBy, JsonElement ContentSchema, JsonElement StyleSchema)
{
    public static FieldVersionDto From(FieldVersion v) =>
        new(v.Snapshot, v.PublishedAt, v.PublishedBy,
            JsonSerializer.SerializeToElement(FieldJsonSchema.Content(v.Snapshot)),
            JsonSerializer.SerializeToElement(FieldJsonSchema.Style(v.Snapshot)));
}

/// <summary>One page of fields and the total across all pages.</summary>
public sealed record FieldPage(IReadOnlyList<FieldSummaryDto> Items, int Page, int PageSize, int Total);

/// <summary>Small helpers the field use cases share.</summary>
public static class FieldResults
{
    /// <summary>Checks a requested page against the module's settings.</summary>
    public static Result CheckPage(int page, int pageSize, int maxPageSize) =>
        page < 1 ? Result.Invalid(new ValidationError(nameof(page), "The page starts at 1."))
        : pageSize < 1 || pageSize > maxPageSize ? Result.Invalid(new ValidationError(nameof(pageSize), $"The page size is 1 to {maxPageSize}."))
        : Result.Success();

    public static string NotFoundMessage(string key) => $"Field '{key}' was not found.";

    public static string StaleMessage(string key, int current, int read) =>
        $"Field '{key}' is at version {current}, not {read}; reload it and try again.";
}

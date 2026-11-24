using System.Text.Json.Serialization;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>How an editor picks a lookup value.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LookupMode>))]
public enum LookupMode
{
    /// <summary>Type to search, pick from the list (chips when the property is multiple).</summary>
    Autocomplete,

    /// <summary>A panel with filter chips, a paged result list and checkboxes or radio buttons.</summary>
    Picker
}

/// <summary>
/// A filter the author fixes on a lookup property: every search applies it, and the server checks saved values against
/// it. Op: eq, ne, contains, startsWith, gt, gte, lt, lte, between, in, isEmpty (the lookup's filter fields decide which).
/// </summary>
public sealed record LookupPresetFilter(string Field, string Op, string? Value = null, IReadOnlyList<string>? Values = null);

/// <summary>
/// What a <see cref="PropertyKind.Lookup"/> property picks from: a lookup another module publishes ("{prefix}.{name}",
/// e.g. <c>inventory.stock-items</c>). The value is <c>{ "key": "42", "text": "Gear" }</c>; a recursive one also has the
/// path above it, root first: <c>{ "key": "7", "text": "Cairo", "path": [{ "key": "1", "text": "Egypt" }] }</c>.
/// </summary>
/// <param name="Key">The lookup's key in the catalog (GET /api/lookups).</param>
/// <param name="SearchFields">Narrows the fields a search looks in (a subset of the lookup's own); null: all of them.</param>
/// <param name="Mode">The editor's control.</param>
/// <param name="MinChars">Characters typed before the first search (0 lists at once).</param>
/// <param name="ParentProperty">
/// Another Lookup property of the same field whose key filters this one (a dependent lookup: city after country). The
/// lookup must have a parent field; the server checks the saved value's parent.
/// </param>
/// <param name="Recursive">
/// A cascade over a tree lookup: one control per level, each filtered by the item chosen above it. Not with
/// <see cref="FieldProperty.Multiple"/>, the Picker mode or <paramref name="ParentProperty"/>.
/// </param>
/// <param name="RootKey">Recursive: the first level lists this item's children; null lists the roots.</param>
/// <param name="MaxDepth">Recursive: how many levels at most.</param>
/// <param name="LeafOnly">Recursive: the chosen item must have no children.</param>
/// <param name="PresetFilters">Locked filters (chips the editor cannot remove).</param>
/// <param name="AllowUserFilters">Picker: whether the editor may add filter chips of their own.</param>
/// <param name="MaxSelections">Multiple: how many values at most.</param>
public sealed record LookupBinding(
    string Key,
    IReadOnlyList<string>? SearchFields = null,
    LookupMode Mode = LookupMode.Autocomplete,
    int MinChars = 0,
    string? ParentProperty = null,
    bool Recursive = false,
    string? RootKey = null,
    int MaxDepth = 5,
    bool LeafOnly = false,
    IReadOnlyList<LookupPresetFilter>? PresetFilters = null,
    bool AllowUserFilters = true,
    int? MaxSelections = null);

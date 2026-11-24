using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Fields.Publish;

/// <summary>
/// Before a field is published, its Lookup properties are checked against the catalog of lookups (what the installed
/// modules declare): the lookup exists and the publisher may use it, the search fields and preset filters are the
/// lookup's own, a recursive property needs a tree lookup and a dependent one a lookup with a parent. A draft may name
/// a lookup that is not installed yet; publishing may not.
/// </summary>
public static class LookupBindings
{
    public static async Task<List<ValidationError>> CheckAsync(FieldDraft draft, ILookupClient lookups, CancellationToken ct)
    {
        var errors = new List<ValidationError>();
        var bound = draft.Properties.Where(p => p.Kind == PropertyKind.Lookup && p.Lookup is not null).ToList();
        if (bound.Count == 0) return errors;

        IReadOnlyList<LookupDescriptor> catalog;
        try { catalog = await lookups.CatalogAsync(ct); }
        catch (Exception e) when (e is HttpRequestException or LookupUnavailableException)
        {
            errors.Add(new ValidationError("properties", $"The catalog of lookups could not be read: {e.Message}"));
            return errors;
        }

        foreach (var p in bound)
        {
            var b = p.Lookup!;
            var at = $"properties.{p.Key}.lookup";
            var d = catalog.FirstOrDefault(l => l.Key == b.Key);
            if (d is null)
            {
                errors.Add(new ValidationError($"{at}.key", $"'{p.Label}': the lookup '{b.Key}' is not installed, or you may not use it."));
                continue;
            }
            foreach (var f in b.SearchFields ?? [])
                if (!d.SearchFields.Contains(f))
                    errors.Add(new ValidationError($"{at}.searchFields", $"'{p.Label}': '{f}' is not a search field of '{b.Key}' ({string.Join(", ", d.SearchFields)})."));
            foreach (var f in b.PresetFilters ?? [])
            {
                var field = d.FilterFields.FirstOrDefault(x => x.Name == f.Field);
                if (field is null)
                    errors.Add(new ValidationError($"{at}.presetFilters", $"'{p.Label}': '{f.Field}' is not a filter of '{b.Key}'."));
                else if (!field.Ops.Contains(f.Op))
                    errors.Add(new ValidationError($"{at}.presetFilters", $"'{p.Label}': '{f.Field}' takes {string.Join(", ", field.Ops)}, not '{f.Op}'."));
            }
            if (b.Mode == LookupMode.Picker && b.AllowUserFilters && d.FilterFields.Count == 0)
                errors.Add(new ValidationError($"{at}.allowUserFilters", $"'{p.Label}': '{b.Key}' has no filters an editor could add."));
            if (b.Recursive && d.ParentKind != "tree")
                errors.Add(new ValidationError($"{at}.recursive", $"'{p.Label}': '{b.Key}' is not a tree, so it cannot be recursive."));
            if (b.ParentProperty is not null && d.ParentKind is null)
                errors.Add(new ValidationError($"{at}.parentProperty", $"'{p.Label}': '{b.Key}' has no parent to filter by."));
        }
        return errors;
    }
}

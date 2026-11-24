using System.Text.Json;
using soft.square.net.WebApi.Contracts.Lookups;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content;

/// <summary>
/// The second step of checking content: every Lookup value is asked of the module that owns the lookup
/// (<see cref="ILookupClient"/>, in-process or through the gateway), one request per lookup and preset filters.
/// - the key exists and passes the lookup's fixed filter and the property's preset filters;
/// - a dependent value's parent is the key of its parent property;
/// - a recursive value's path is a chain from the root key down to it, within the depth, ending at a leaf when asked.
/// A lookup that cannot be reached is a problem at each value that uses it: content is never accepted unchecked.
/// </summary>
public static class LookupReferences
{
    private const int KeysPerRequest = 200;

    private sealed record Reference(string At, FieldProperty Property, string Key, string? ParentKey, IReadOnlyList<string> Path);

    public static async Task CheckAsync(IEnumerable<(ContentNode Node, FieldSnapshot Field, string At)> nodes, ILookupClient lookups,
        List<ContentIssue> issues, CancellationToken ct)
    {
        var references = new List<Reference>();
        foreach (var (node, field, at) in nodes) Collect(node.Content, field, $"{at}/content", references);
        if (references.Count == 0) return;

        // One request per (lookup, preset filters) for the values; the ancestors of recursive values without the filters.
        var requests = new Dictionary<(string Lookup, string Filters), (IReadOnlyList<LookupFilter>? Filters, HashSet<string> Keys)>();
        void Want(string lookup, IReadOnlyList<LookupPresetFilter>? preset, string key)
        {
            var filters = preset?.Select(f => new LookupFilter(f.Field, f.Op, f.Value, f.Values)).ToList();
            var id = (lookup, filters is null ? "" : JsonSerializer.Serialize(filters));
            if (!requests.TryGetValue(id, out var request)) requests[id] = request = (filters, new HashSet<string>(StringComparer.Ordinal));
            request.Keys.Add(key);
        }
        foreach (var r in references)
        {
            Want(r.Property.Lookup!.Key, r.Property.Lookup.PresetFilters, r.Key);
            foreach (var ancestor in r.Path) Want(r.Property.Lookup.Key, null, ancestor);
        }

        var found = new Dictionary<(string Lookup, string Filters), Dictionary<string, LookupItem>>();
        var failed = new Dictionary<string, string>(StringComparer.Ordinal); // lookup → why
        foreach (var ((lookup, filtersId), (filters, keys)) in requests)
        {
            if (failed.ContainsKey(lookup)) continue;
            var items = new Dictionary<string, LookupItem>(StringComparer.Ordinal);
            try
            {
                foreach (var chunk in keys.Chunk(KeysPerRequest))
                    foreach (var item in await lookups.ResolveAsync(lookup, chunk, filters, ct))
                        items[item.Key] = item;
                found[(lookup, filtersId)] = items;
            }
            catch (Exception e) when (e is LookupUnavailableException or UnauthorizedAccessException or ArgumentException or HttpRequestException)
            {
                failed[lookup] = e.Message;
            }
        }

        foreach (var r in references)
        {
            var b = r.Property.Lookup!;
            var label = r.Property.Label;
            if (failed.TryGetValue(b.Key, out var why)) { issues.Add(new(r.At, $"{label}: the list could not be checked. {why}")); continue; }

            var filtersId = b.PresetFilters is null ? "" : JsonSerializer.Serialize(b.PresetFilters.Select(f => new LookupFilter(f.Field, f.Op, f.Value, f.Values)).ToList());
            if (!found[(b.Key, filtersId)].TryGetValue(r.Key, out var item))
            {
                issues.Add(new($"{r.At}/key", $"{label}: '{r.Key}' is not in the list{(b.PresetFilters is null ? "" : " for this field")}."));
                continue;
            }

            if (b.ParentProperty is not null)
            {
                if (r.ParentKey is null) issues.Add(new(r.At, $"{label}: choose the value of '{b.ParentProperty}' first."));
                else if (item.ParentKey != r.ParentKey) issues.Add(new($"{r.At}/key", $"{label}: '{item.Text}' does not belong to the value chosen above it."));
            }

            if (b.Recursive)
            {
                var all = found[(b.Key, "")];
                var chain = new List<LookupItem>();
                var missing = false;
                for (var i = 0; i < r.Path.Count; i++)
                {
                    if (all.TryGetValue(r.Path[i], out var step)) chain.Add(step);
                    else { issues.Add(new($"{r.At}/path/{i}/key", $"{label}: '{r.Path[i]}' is not in the list.")); missing = true; }
                }
                if (missing) continue;
                chain.Add(item);

                if (chain.Count > b.MaxDepth) issues.Add(new($"{r.At}/path", $"{label} goes at most {b.MaxDepth} levels deep."));
                var expected = b.RootKey;
                for (var i = 0; i < chain.Count; i++)
                {
                    if (!SameParent(chain[i].ParentKey, expected))
                    {
                        issues.Add(new(i == chain.Count - 1 ? $"{r.At}/key" : $"{r.At}/path/{i}",
                            i == 0 && b.RootKey is not null
                                ? $"{label}: '{chain[i].Text}' is not under the field's starting item."
                                : $"{label}: '{chain[i].Text}' is not under the item above it."));
                        break;
                    }
                    expected = chain[i].Key;
                }
                if (b.LeafOnly && item.HasChildren == true)
                    issues.Add(new($"{r.At}/key", $"{label}: choose a more specific item than '{item.Text}'."));
            }
        }
    }

    /// <summary>Every node of a tree whose field version is published, with its JSON Pointer (the root is "").</summary>
    public static IEnumerable<(ContentNode Node, FieldSnapshot Field, string At)> Nodes(ContentNode root, Func<string, int, FieldSnapshot?> resolve)
    {
        var stack = new Stack<(ContentNode Node, string At, int Depth)>();
        stack.Push((root, "", 1));
        var count = 0;
        while (stack.Count > 0 && ++count <= ContentTreeValidator.MaxNodes)
        {
            var (node, at, depth) = stack.Pop();
            if (resolve(node.Field ?? "", node.FieldVersion) is { } field) yield return (node, field, at);
            if (depth >= ContentTreeValidator.MaxDepth) continue;
            var children = node.Children ?? [];
            for (var i = children.Count - 1; i >= 0; i--)
                if (children[i] is { } child) stack.Push((child, $"{at}/children/{i}", depth + 1));
        }
    }

    private static bool SameParent(string? actual, string? expected) =>
        string.IsNullOrEmpty(expected) ? string.IsNullOrEmpty(actual) : string.Equals(actual, expected, StringComparison.Ordinal);

    /// <summary>The lookup values of one node's content; malformed ones are left to the shape check.</summary>
    private static void Collect(JsonElement content, FieldSnapshot field, string at, List<Reference> references)
    {
        if (content.ValueKind != JsonValueKind.Object) return;
        foreach (var p in field.Properties)
        {
            if (p.Kind != PropertyKind.Lookup || p.Lookup is null) continue;
            if (!content.TryGetProperty(p.Key, out var value) || value.ValueKind == JsonValueKind.Null) continue;

            string? parentKey = null;
            if (p.Lookup.ParentProperty is { } parent && content.TryGetProperty(parent, out var parentValue))
                parentKey = KeyOf(parentValue);

            if (p.Multiple)
            {
                if (value.ValueKind != JsonValueKind.Array) continue;
                var i = 0;
                foreach (var element in value.EnumerateArray()) Add(element, $"{at}/{Escape(p.Key)}/{i++}");
            }
            else Add(value, $"{at}/{Escape(p.Key)}");

            void Add(JsonElement element, string where)
            {
                if (KeyOf(element) is not { } key) return;
                var path = new List<string>();
                if (p.Lookup.Recursive && element.TryGetProperty("path", out var steps) && steps.ValueKind == JsonValueKind.Array)
                {
                    foreach (var step in steps.EnumerateArray())
                    {
                        if (KeyOf(step) is not { } stepKey) return;
                        path.Add(stepKey);
                    }
                }
                references.Add(new(where, p, key, parentKey, path));
            }
        }
    }

    private static string? KeyOf(JsonElement value) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.String
        && key.GetString() is { Length: > 0 and <= ContentTreeValidator.MaxIdLength } text && text.Trim().Length > 0 ? text : null;

    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.Translation;

/// <summary>
/// One translated value: the text in the overlay's language (a string, or a list of strings for a list value) and the
/// <see cref="ContentTranslator.SourceHash"/> of the source value it was translated from. When the source changes, the
/// hashes differ and the translation is stale: still shown, flagged for the translator.
/// </summary>
public sealed record TranslatedValue(JsonElement Value, string SourceHash);

/// <summary>
/// The translation of one content tree into one language, kept apart from the source tree as an overlay: per node id
/// (<see cref="ContentTranslator.RootId"/> for the root), per property key, the translated value. Only textual values
/// (Text, RichText) are in it; everything else (ids, numbers, dates, style, the tree's shape) always comes from the
/// source. The consumer (Contents) stores one per language next to the source; this module owns its rules.
/// </summary>
public sealed record ContentTranslation(
    string Language,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, TranslatedValue>> Nodes);

/// <summary>Where a value's translation stands in one language.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TranslationState>))]
public enum TranslationState
{
    /// <summary>No translation yet: readers see the next language of their fallback chain, in the end the source.</summary>
    Missing,

    /// <summary>Translated from an older source value: shown, but the source has changed since.</summary>
    Stale,

    /// <summary>Translated from the current source value.</summary>
    Current
}

/// <summary>
/// One textual value of a tree that can be translated: where it is, what it is, its source text and hash, and (for a
/// given translation) its state and translated value. The work list of a translator, and what the translate dialog shows.
/// </summary>
public sealed record TranslationUnit(
    string NodeId,
    string Path,
    string Field,
    string Key,
    string Label,
    PropertyKind Kind,
    bool Multiple,
    int? MaxLength,
    JsonElement Source,
    string SourceHash,
    TranslationState State,
    JsonElement? Translation);

/// <summary>How many units are current, stale or missing in one language.</summary>
public sealed record TranslationSummary(int Total, int Current, int Stale, int Missing);

/// <summary>A tree with its values in the reader's language, and which languages served them.</summary>
/// <param name="Languages">Values served per language of the chain (the source's values are not counted).</param>
/// <param name="Stale">Values served from a stale translation.</param>
/// <param name="Untranslated">Translatable values no language of the chain has: they show the source.</param>
public sealed record TranslatedContent(ContentNode Root, IReadOnlyDictionary<string, int> Languages, int Stale, int Untranslated);

/// <summary>
/// The rules of content translation: which values are translatable, the overlay's checks, staleness, the fallback
/// chain and the merge of overlays onto a source tree. Pure functions over published field versions, like
/// <see cref="ContentTreeValidator"/>; nothing here stores anything.
/// </summary>
public static partial class ContentTranslator
{
    /// <summary>The node id the root (the post) has in an overlay: it has no node id of its own.</summary>
    public const string RootId = "$root";

    public const int MaxLanguageLength = 35;
    public const int MaxHashLength = 64;

    /// <summary>Text and RichText values, unless the field says the property is not language (Translatable false).</summary>
    public static bool IsTranslatable(FieldProperty property) =>
        property.Kind is PropertyKind.Text or PropertyKind.RichText && property.Translatable != false;

    /// <summary>A short hash of a source value (16 hex digits of SHA-256): equal values, equal hashes.</summary>
    public static string SourceHash(JsonElement value)
    {
        var canonical = value.ValueKind == JsonValueKind.String ? "s:" + value.GetString() : "j:" + JsonSerializer.Serialize(value);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))[..16];
    }

    /// <summary>A BCP 47 language tag such as ar, en, en-GB or zh-Hant-TW.</summary>
    public static bool IsLanguage(string? tag) => tag is { Length: > 0 and <= MaxLanguageLength } && LanguageTag().IsMatch(tag);

    /// <summary>
    /// The languages to read a tree in, best first: the reader's culture, then its parents (ar-EG, then ar), each only
    /// when <paramref name="available"/> has it (ignoring case). The source language needs no entry: it is what is left.
    /// </summary>
    public static IReadOnlyList<string> Chain(string culture, IEnumerable<string> available)
    {
        var have = available.ToList();
        var chain = new List<string>();
        for (var tag = culture?.Trim() ?? ""; tag.Length > 0; tag = tag.LastIndexOf('-') is var dash and > 0 ? tag[..dash] : "")
            if (have.FirstOrDefault(a => string.Equals(a, tag, StringComparison.OrdinalIgnoreCase)) is { } match && !chain.Contains(match))
                chain.Add(match);
        return chain;
    }

    /// <summary>Every translatable value of the tree, in tree order, with its state in <paramref name="translation"/>.</summary>
    public static IReadOnlyList<TranslationUnit> Units(ContentNode root, Func<string, int, FieldSnapshot?> resolve, ContentTranslation? translation = null)
    {
        ArgumentNullException.ThrowIfNull(root);
        var units = new List<TranslationUnit>();
        foreach (var (node, id, path, field) in Walk(root, resolve))
        {
            if (id is null) continue;
            var values = translation?.Nodes.GetValueOrDefault(id);
            foreach (var p in field.Properties.Where(IsTranslatable))
            {
                if (!Source(node, p, out var source)) continue;
                var hash = SourceHash(source);
                var translated = values?.GetValueOrDefault(p.Key);
                var state = translated is null ? TranslationState.Missing
                    : translated.SourceHash == hash ? TranslationState.Current : TranslationState.Stale;
                units.Add(new TranslationUnit(id, $"{path}/content/{Escape(p.Key)}", field.Key, p.Key, p.Label, p.Kind,
                    p.Multiple, p.MaxLength, source, hash, state, translated?.Value));
            }
        }
        return units;
    }

    public static TranslationSummary Summarize(IReadOnlyCollection<TranslationUnit> units) => new(
        units.Count,
        units.Count(u => u.State == TranslationState.Current),
        units.Count(u => u.State == TranslationState.Stale),
        units.Count(u => u.State == TranslationState.Missing));

    /// <summary>
    /// Checks an overlay against the tree it translates: a language tag, nodes the tree has (by node id), translatable
    /// values the node's field has, the value's shape and the property's length limits. Paths are JSON Pointers into the
    /// overlay (<c>/nodes/img-1/caption/value</c>). Values of a node whose field is not published are not checked (the
    /// tree's own validation reports that).
    /// </summary>
    public static IReadOnlyList<ContentIssue> Validate(ContentNode root, ContentTranslation translation, Func<string, int, FieldSnapshot?> resolve)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(translation);
        var issues = new List<ContentIssue>();
        if (!IsLanguage(translation.Language))
            issues.Add(new("/language", "The language is a BCP 47 tag such as ar, en or en-GB."));

        var fields = new Dictionary<string, FieldSnapshot?>(StringComparer.Ordinal);
        foreach (var (_, id, _, field) in Walk(root, resolve, includeUnresolved: true))
            if (id is not null) fields.TryAdd(id, field);

        foreach (var (id, values) in translation.Nodes ?? new Dictionary<string, IReadOnlyDictionary<string, TranslatedValue>>())
        {
            var at = $"/nodes/{Escape(id)}";
            if (!fields.TryGetValue(id, out var field)) { issues.Add(new(at, $"The content has no node '{id}'.")); continue; }
            if (field is null || values is null) continue;
            var properties = field.Properties.ToDictionary(p => p.Key, StringComparer.Ordinal);
            foreach (var (key, translated) in values)
            {
                var keyAt = $"{at}/{Escape(key)}";
                if (!properties.TryGetValue(key, out var p)) { issues.Add(new(keyAt, $"'{field.Key}' has no value '{key}'.")); continue; }
                if (!IsTranslatable(p)) { issues.Add(new(keyAt, $"{p.Label} is not translated.")); continue; }
                if (translated is null) { issues.Add(new(keyAt, $"The translation of {p.Label} is missing: leave the key out instead.")); continue; }
                if (string.IsNullOrWhiteSpace(translated.SourceHash) || translated.SourceHash.Length > MaxHashLength)
                    issues.Add(new($"{keyAt}/sourceHash", $"The translation of {p.Label} needs the hash of its source value."));
                Value(p, translated.Value, $"{keyAt}/value", issues);
            }
        }
        return issues;
    }

    /// <summary>
    /// The tree in the reader's languages: each translatable value from the first overlay of <paramref name="chain"/>
    /// that has it (a stale one too, unless <paramref name="useStale"/> is false), else the source value. The tree's
    /// shape, style and every other value are the source's.
    /// </summary>
    public static TranslatedContent Apply(ContentNode root, IReadOnlyList<ContentTranslation> chain, Func<string, int, FieldSnapshot?> resolve, bool useStale = true)
    {
        ArgumentNullException.ThrowIfNull(root);
        var languages = new Dictionary<string, int>(StringComparer.Ordinal);
        var stale = 0;
        var untranslated = 0;

        ContentNode Merge(ContentNode node, string? id, int depth)
        {
            var field = depth <= ContentTreeValidator.MaxDepth ? resolve(node.Field ?? "", node.FieldVersion) : null;
            var content = node.Content;
            if (field is not null && id is not null && node.Content.ValueKind == JsonValueKind.Object)
            {
                JsonObject? merged = null;
                foreach (var p in field.Properties.Where(IsTranslatable))
                {
                    if (!Source(node, p, out var source)) continue;
                    var hash = SourceHash(source);
                    var hit = chain
                        .Select(t => (t.Language, Value: t.Nodes.GetValueOrDefault(id)?.GetValueOrDefault(p.Key)))
                        .FirstOrDefault(t => t.Value is not null && (useStale || t.Value.SourceHash == hash));
                    if (hit.Value is null) { untranslated++; continue; }
                    merged ??= JsonNode.Parse(node.Content.GetRawText())!.AsObject();
                    merged[p.Key] = JsonNode.Parse(hit.Value.Value.GetRawText());
                    languages[hit.Language] = languages.GetValueOrDefault(hit.Language) + 1;
                    if (hit.Value.SourceHash != hash) stale++;
                }
                if (merged is not null) content = JsonSerializer.SerializeToElement(merged);
            }
            var children = node.Children?.Select(c => c is null ? c! : Merge(c, c.NodeId, depth + 1)).ToList();
            return node with { Content = content, Children = children };
        }

        return new TranslatedContent(Merge(root, RootId, 1), languages, stale, untranslated);
    }

    /// <summary>
    /// The overlay without what the tree no longer has: nodes that were removed, values that were removed or emptied,
    /// properties that stopped being translatable. What the consumer stores after the source is edited. Stale values are
    /// kept (they still help readers and translators).
    /// </summary>
    public static ContentTranslation Prune(ContentNode root, ContentTranslation translation, Func<string, int, FieldSnapshot?> resolve)
    {
        var live = Units(root, resolve).Select(u => (u.NodeId, u.Key)).ToHashSet();
        var nodes = new Dictionary<string, IReadOnlyDictionary<string, TranslatedValue>>(StringComparer.Ordinal);
        foreach (var (id, values) in translation.Nodes)
        {
            var kept = values.Where(v => v.Value is not null && live.Contains((id, v.Key))).ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal);
            if (kept.Count > 0) nodes[id] = kept;
        }
        return translation with { Nodes = nodes };
    }

    /// <summary>The nodes of the tree with their overlay id and resolved field, within the validator's limits.</summary>
    private static IEnumerable<(ContentNode Node, string? Id, string Path, FieldSnapshot Field)> Walk(
        ContentNode root, Func<string, int, FieldSnapshot?> resolve, bool includeUnresolved = false)
    {
        var stack = new Stack<(ContentNode Node, string? Id, string Path, int Depth)>();
        stack.Push((root, RootId, "", 1));
        var count = 0;
        while (stack.Count > 0 && ++count <= ContentTreeValidator.MaxNodes)
        {
            var (node, id, path, depth) = stack.Pop();
            var field = resolve(node.Field ?? "", node.FieldVersion);
            if (field is not null || includeUnresolved) yield return (node, id, path, field!);
            if (depth >= ContentTreeValidator.MaxDepth) continue;
            var children = node.Children ?? [];
            for (var i = children.Count - 1; i >= 0; i--)
                if (children[i] is { } child)
                    stack.Push((child, string.IsNullOrWhiteSpace(child.NodeId) ? null : child.NodeId, $"{path}/children/{i}", depth + 1));
        }
    }

    /// <summary>The source value of a translatable property, when it has text to translate.</summary>
    private static bool Source(ContentNode node, FieldProperty p, out JsonElement value)
    {
        value = default;
        if (node.Content.ValueKind != JsonValueKind.Object || !node.Content.TryGetProperty(p.Key, out value)) return false;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Trim().Length > 0,
            JsonValueKind.Array => value.EnumerateArray().Any(i => i.ValueKind == JsonValueKind.String && i.GetString()!.Trim().Length > 0),
            _ => false
        };
    }

    private static void Value(FieldProperty p, JsonElement value, string at, List<ContentIssue> issues)
    {
        if (p.Multiple)
        {
            if (value.ValueKind != JsonValueKind.Array) { issues.Add(new(at, $"{p.Label} is a list of texts.")); return; }
            var i = 0;
            foreach (var item in value.EnumerateArray()) Text(p, item, $"{at}/{i++}", issues);
            return;
        }
        Text(p, value, at, issues);
    }

    private static void Text(FieldProperty p, JsonElement value, string at, List<ContentIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.String) { issues.Add(new(at, $"{p.Label} is text.")); return; }
        var text = value.GetString()!;
        if (text.Trim().Length == 0) issues.Add(new(at, $"The translation of {p.Label} is empty: leave it out instead."));
        else if (text.Length < p.MinLength) issues.Add(new(at, $"{p.Label} has at least {p.MinLength} characters."));
        if (text.Length > p.MaxLength) issues.Add(new(at, $"{p.Label} has at most {p.MaxLength} characters."));
    }

    private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");

    [GeneratedRegex("^[A-Za-z]{2,8}(-[A-Za-z0-9]{1,8})*$")]
    private static partial Regex LanguageTag();
}

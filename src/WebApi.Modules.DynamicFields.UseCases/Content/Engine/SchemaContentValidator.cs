using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.Translation;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Engine;

/// <summary>
/// The schema engine: checks content with the field versions' compiled schemas (<see cref="FieldSchemaCompiler"/>), in
/// the DynamicFields dialect, with JsonSchema.Net. It walks the tree and checks every node against its own version's
/// schema (<see cref="ChildSchemas.Shallow"/>: the parent's schema checks only which children it allows, at which
/// version and how many), so a node at another version than its parent pins is still checked against the version it
/// names, as Content stores it. The tree keywords of the dialect's <c>reference</c> vocabulary (<c>maxDepth</c>,
/// <c>maxNodes</c>, <c>uniqueNodeIds</c>, read from the root's schema) are applied by the walk; reference existence stays
/// with the owners (LookupReferences), after either engine.
/// <para>
/// The library's errors are mapped back to the issues the legacy rules (<see cref="ContentTreeValidator"/>) report: the
/// same paths and the same messages, written from the field (its labels, options, limits), so consumers see no change.
/// Instance registries only (a dialect registry per engine, a schema registry per check), never the library's global
/// ones: compiled versions of one tenant never meet another's, and nothing outlives the module.
/// </para>
/// </summary>
public sealed class SchemaContentValidator : IContentValidator
{
    private static readonly EvaluationOptions Evaluation = new() { OutputFormat = OutputFormat.List, RequireFormatValidation = true };

    /// <summary>The keywords that only apply subschemas: their own errors only summarise their subschemas' errors.</summary>
    private static readonly HashSet<string> Applicators = new(StringComparer.Ordinal)
    {
        "properties", "additionalProperties", "patternProperties", "propertyNames", "items", "prefixItems", "allOf", "anyOf",
        "oneOf", "not", "if", "then", "else", "dependentSchemas", "unevaluatedItems", "unevaluatedProperties", "$ref", "$dynamicRef"
    };

    private readonly DialectRegistry _dialects = new();

    public SchemaContentValidator()
    {
        Vocabulary[] standard =
        [
            Vocabulary.Draft202012_Core, Vocabulary.Draft202012_Applicator, Vocabulary.Draft202012_Unevaluated,
            Vocabulary.Draft202012_Validation, Vocabulary.Draft202012_MetaData, Vocabulary.Draft202012_FormatAnnotation,
            Vocabulary.Draft202012_Content
        ];
        // The dialect's own keywords, as their vocabularies' meta-schemas list them. In this engine they all annotate:
        // what the reference keywords assert is checked by the walk (tree limits) and by the owners (references).
        var own = FieldDialect.Vocabularies.Keys.SelectMany(v =>
            JsonNode.Parse(FieldDialect.Read(v)!)!["properties"]!.AsObject().Select(k => (IKeywordHandler)new DialectKeyword(k.Key)));
        var handlers = standard.SelectMany(v => v.Keywords)
            .Append(Json.Schema.Keywords.ThenKeyword.Instance) // JsonSchema.Net 9.4's applicator vocabulary leaves out "then".
            .Concat(own)
            .DistinctBy(h => h.Name, StringComparer.Ordinal);
        _dialects.Register(new Dialect(handlers) { Id = new Uri(FieldDialect.Uri) });
    }

    public IReadOnlyList<ContentIssue> Validate(ContentNode root, Func<string, int, FieldSnapshot?> resolve)
    {
        ArgumentNullException.ThrowIfNull(root);
        var check = new Check(this, resolve);
        check.Tree(root);
        return check.Issues;
    }

    public IReadOnlyList<ContentIssue> ValidateValues(FieldSnapshot field, JsonElement content, JsonElement style)
    {
        ArgumentNullException.ThrowIfNull(field);
        var check = new Check(this, (_, _) => null);
        check.Values(field, content, style);
        return check.Issues;
    }

    /// <summary>A dialect keyword: valid wherever its meta-schema allows it, kept as an annotation.</summary>
    private sealed class DialectKeyword(string name) : IKeywordHandler
    {
        public string Name => name;
        public object ValidateKeywordValue(JsonElement value) => value.Clone();
        public void BuildSubschemas(KeywordData data, BuildContext context) { }
        public KeywordEvaluation Evaluate(KeywordData data, EvaluationContext context) =>
            new() { Keyword = name, IsValid = true, Annotation = data.RawValue };
    }

    private sealed record Compiled(FieldSnapshot Field, JsonObject Json, JsonSchema Schema);

    /// <summary>One check: its schema registry, the versions compiled for it, and what it found.</summary>
    private sealed class Check(SchemaContentValidator engine, Func<string, int, FieldSnapshot?> resolve)
    {
        private readonly BuildOptions _build = new() { SchemaRegistry = new SchemaRegistry(), DialectRegistry = engine._dialects };
        private readonly Dictionary<(string, int), Compiled> _compiled = [];
        private readonly HashSet<ContentIssue> _seen = [];
        private readonly Dictionary<string, string> _ids = new(StringComparer.Ordinal);
        private int _nodes, _maxDepth = ContentTreeValidator.MaxDepth, _maxNodes = ContentTreeValidator.MaxNodes;
        private bool _uniqueIds = true;

        public List<ContentIssue> Issues { get; } = [];

        private void Add(string path, string message)
        {
            var issue = new ContentIssue(path, message);
            if (_seen.Add(issue)) Issues.Add(issue);
        }

        private Compiled Compile(FieldSnapshot field)
        {
            if (_compiled.TryGetValue((field.Key, field.Version), out var compiled)) return compiled;
            var json = FieldSchemaCompiler.Compile(field, ChildSchemas.Shallow);
            var schema = JsonSchema.Build(JsonSerializer.SerializeToElement(json), _build, new Uri(FieldDialect.VersionId(field.Key, field.Version)));
            return _compiled[(field.Key, field.Version)] = new Compiled(field, json, schema);
        }

        public void Tree(ContentNode root)
        {
            // The tree's limits are the root schema's reference keywords; a root that is no post field has none, and the
            // dialect's defaults apply.
            if (resolve(root.Field ?? "", root.FieldVersion) is { } field)
            {
                var json = Compile(field).Json;
                _maxDepth = json["maxDepth"]?.GetValue<int>() ?? _maxDepth;
                _maxNodes = json["maxNodes"]?.GetValue<int>() ?? _maxNodes;
                _uniqueIds = json["uniqueNodeIds"]?.GetValue<bool>() ?? _uniqueIds;
            }
            Node(root, "", depth: 1, isRoot: true, fromParent: null);
        }

        public void Values(FieldSnapshot field, JsonElement content, JsonElement style)
        {
            var node = new ContentNode(field.Key, field.Version, content, style);
            var compiled = Compile(field);
            Map(compiled, node, children: null, "", compiled.Schema.Evaluate(Instance(node, children: null), Evaluation), []);
        }

        private void Node(ContentNode node, string path, int depth, bool isRoot, List<ContentIssue>? fromParent)
        {
            if (++_nodes > _maxNodes)
            {
                if (_nodes == _maxNodes + 1) Add(path, $"A post has at most {_maxNodes} nodes.");
                return;
            }
            if (depth > _maxDepth)
            {
                Add(path, $"Content nests at most {_maxDepth} levels.");
                return;
            }

            NodeId(node, path);

            var field = resolve(node.Field ?? "", node.FieldVersion);
            if (field is null)
            {
                Add($"{path}/field", $"Field '{node.Field}' version {node.FieldVersion} is not published.");
                return;
            }
            var compiled = Compile(field);
            if (isRoot && compiled.Json["canBeRoot"]?.GetValue<bool>() != true)
                Add($"{path}/field", $"Field '{field.Key}' cannot be a post's root.");
            foreach (var issue in fromParent ?? []) Add(issue.Path, issue.Message);

            var children = node.Children ?? [];
            var forChildren = new Dictionary<int, List<ContentIssue>>();
            Map(compiled, node, children, path, compiled.Schema.Evaluate(Instance(node, children), Evaluation), forChildren);

            for (var i = 0; i < children.Count; i++)
                if (children[i] is { } child)
                    Node(child, $"{path}/children/{i}", depth + 1, isRoot: false, forChildren.GetValueOrDefault(i));
        }

        /// <summary>The reference vocabulary's node id rules: 1 to 128 characters, not the root's overlay key, used once.</summary>
        private void NodeId(ContentNode node, string path)
        {
            if (node.NodeId is not { } id) return;
            var at = $"{path}/nodeId";
            if (id.Trim().Length == 0 || id.Length > ContentTreeValidator.MaxIdLength || id != id.Trim())
                Add(at, $"A node id is 1 to {ContentTreeValidator.MaxIdLength} characters, without spaces around it.");
            else if (id == ContentTranslator.RootId)
                Add(at, $"'{id}' is the root's key in overlays, not a node id.");
            else if (_uniqueIds && !_ids.TryAdd(id, path))
                Add(at, $"Node id '{id}' is used twice (also at {(_ids[id].Length == 0 ? "the root" : $"'{_ids[id]}'")}).");
        }

        /// <summary>
        /// The node as its schema reads it: field, version, content and style as sent, and each child by its field and
        /// version only (its own values are checked when the walk reaches it). The node id is the walk's.
        /// </summary>
        private static JsonElement Instance(ContentNode node, IReadOnlyList<ContentNode?>? children)
        {
            var instance = new JsonObject { ["field"] = node.Field, ["fieldVersion"] = node.FieldVersion };
            if (node.Content.ValueKind != JsonValueKind.Undefined) instance["content"] = JsonNode.Parse(node.Content.GetRawText());
            if (node.Style.ValueKind != JsonValueKind.Undefined) instance["style"] = JsonNode.Parse(node.Style.GetRawText());
            if (children is not null)
                instance["children"] = new JsonArray([.. children.Select(c => c is null ? null : (JsonNode)new JsonObject
                {
                    ["field"] = c.Field,
                    ["fieldVersion"] = c.FieldVersion
                })]);
            return JsonSerializer.SerializeToElement(instance);
        }

        // ---- the library's errors, as the legacy issues ---------------------------------------------------------------

        private void Map(Compiled compiled, ContentNode node, IReadOnlyList<ContentNode?>? children, string path,
            EvaluationResults results, Dictionary<int, List<ContentIssue>> forChildren)
        {
            if (results.IsValid) return;
            foreach (var unit in Units(results))
            {
                if (unit.Errors is not { Count: > 0 } errors) continue;
                var evaluation = Tokens(unit.EvaluationPath.ToString());
                // A failed "if" only picks the branch, and an item that does not match "contains" is only not counted.
                if (Keywords(evaluation).Any(k => k is "if" or "contains")) continue;
                var at = Tokens(unit.InstanceLocation.ToString());
                foreach (var keyword in errors.Keys)
                    if (!Applicators.Contains(keyword))
                        Issue(compiled.Field, node, children ?? [], path, at, keyword, evaluation, forChildren);
            }
        }

        private static IEnumerable<EvaluationResults> Units(EvaluationResults results)
        {
            yield return results;
            foreach (var detail in results.Details ?? [])
                foreach (var unit in Units(detail))
                    yield return unit;
        }

        /// <summary>The keywords along an evaluation path (property names and array indexes skipped).</summary>
        private static IEnumerable<string> Keywords(string[] evaluation)
        {
            for (var i = 0; i < evaluation.Length; i++)
            {
                yield return evaluation[i];
                if (evaluation[i] is "properties" or "patternProperties" or "dependentSchemas" or "$defs"
                    or "allOf" or "anyOf" or "oneOf" or "prefixItems") i++;
            }
        }

        private void Issue(FieldSnapshot field, ContentNode node, IReadOnlyList<ContentNode?> children, string path,
            string[] at, string keyword, string[] evaluation, Dictionary<int, List<ContentIssue>> forChildren)
        {
            var here = path + Pointer(at);
            switch (at)
            {
                case [] when keyword == "required":
                case ["content"] when keyword == "type":
                    Add($"{path}/content", "Content is a JSON object.");
                    return;
                case ["content"] when keyword == "required":
                    var content = node.Content;
                    foreach (var p in field.Properties.Where(p => p.Required && !content.TryGetProperty(p.Key, out _)))
                        Add($"{path}/content/{Escape(p.Key)}", $"{p.Label} is required.");
                    return;
                case ["content", var key, .. var rest]:
                    if (field.Properties.FirstOrDefault(p => p.Key == key) is not { } property)
                        Add(here, $"'{field.Key}' has no value '{key}'.");
                    else ContentValue(property, rest, keyword, here, At(node.Content, at[1..]),
                        At(node.Content, at[1..Math.Min(at.Length, property.Multiple ? 3 : 2)]));
                    return;
                case ["style"]:
                    Add(here, "Style is a JSON object.");
                    return;
                case ["style", var name, .. var below]:
                    if (field.Style.FirstOrDefault(s => s.Key == name) is not { } style)
                        Add(here, $"'{field.Key}' has no style '{name}'.");
                    else if (below.Length == 1 && keyword == "")
                        Add(here, $"Breakpoints are {string.Join(", ", ContentTreeValidator.Breakpoints)}.");
                    else
                        Add(here, $"{style.Label} is one of: {string.Join(", ", style.Values)}{(style.Responsive ? ", or one per breakpoint" : "")}.");
                    return;
                case ["children"] when keyword is "contains" or "minContains" or "maxContains":
                    var index = Array.IndexOf(evaluation, "allOf");
                    if (index < 0 || index + 1 >= evaluation.Length || !int.TryParse(evaluation[index + 1], out var n)) return;
                    var rules = FieldSchemaCompiler.CountedChildren(field);
                    if (n >= rules.Count) return;
                    var rule = rules[n];
                    var count = children.Count(c => c?.Field == rule.FieldKey);
                    if (count < rule.Min) Add($"{path}/children", $"'{field.Key}' needs at least {rule.Min} '{rule.FieldKey}' (has {count}).");
                    if (count > rule.Max) Add($"{path}/children", $"'{field.Key}' holds at most {rule.Max} '{rule.FieldKey}' (has {count}).");
                    return;
                case ["children", _] when keyword == "type":
                    Add(here, "A child node is missing.");
                    return;
                case ["children", var i, var member] when int.TryParse(i, out var c) && c < children.Count && children[c] is { } child:
                    // Reported when the walk reaches the child, after its own limits, and only if its version is published.
                    if (resolve(child.Field ?? "", child.FieldVersion) is not { } version) return;
                    var issues = forChildren.TryGetValue(c, out var list) ? list : forChildren[c] = [];
                    if (member == "field")
                        issues.Add(new(here, $"'{field.Key}' cannot contain '{version.Key}'."));
                    else if (member == "fieldVersion" && field.Children.FirstOrDefault(r => r.FieldKey == version.Key) is { } pin)
                        issues.Add(new(here, $"'{field.Key}' version {field.Version} contains '{version.Key}' version {pin.Version}, not {version.Version}."));
                    return;
            }
        }

        private void ContentValue(FieldProperty p, string[] rest, string keyword, string here, JsonElement? value, JsonElement? item)
        {
            var label = p.Label;
            if (rest.Length == 0 && value is { ValueKind: JsonValueKind.Null })
            {
                if (p.Required) Add(here, $"{label} is required.");
                return;
            }
            if (p.Multiple)
            {
                if (rest.Length == 0)
                {
                    Add(here, keyword switch
                    {
                        "minItems" => $"{label} needs at least one value.",
                        "maxItems" => $"{label} has at most {p.Lookup?.MaxSelections} values.",
                        _ => $"{label} is a list."
                    });
                    return;
                }
                rest = rest[1..];
            }
            if (p.Kind == PropertyKind.Lookup)
            {
                LookupValue(p, rest, keyword, here, item);
                return;
            }
            Add(here, p.Kind switch
            {
                PropertyKind.Text or PropertyKind.RichText => keyword switch
                {
                    "pattern" => $"{label} is required.",
                    "minLength" => $"{label} has at least {p.MinLength} characters.",
                    "maxLength" => $"{label} has at most {p.MaxLength} characters.",
                    _ => $"{label} is text."
                },
                PropertyKind.Integer or PropertyKind.Decimal => keyword switch
                {
                    "minimum" => $"{label} is at least {p.Minimum}.",
                    "maximum" => $"{label} is at most {p.Maximum}.",
                    _ => p.Kind == PropertyKind.Integer ? $"{label} is a whole number." : $"{label} is a number."
                },
                PropertyKind.Boolean => $"{label} is true or false.",
                PropertyKind.Date => $"{label} is a date, yyyy-MM-dd.",
                PropertyKind.DateTime => $"{label} is a date and time with its offset (ISO 8601, e.g. 2026-11-21T09:30:00+02:00).",
                PropertyKind.Url => $"{label} is an http or https address.",
                PropertyKind.Choice => $"{label} is one of: {string.Join(", ", p.Options ?? [])}.",
                _ => $"{label} is an id of 1 to {ContentTreeValidator.MaxIdLength} characters."
            });
        }

        /// <summary>
        /// A lookup value <c>{ key, text, path? }</c> (<paramref name="item"/>), at <paramref name="here"/> or below it
        /// (<paramref name="rest"/>). A path that is too long is one issue: its items are not checked.
        /// </summary>
        private void LookupValue(FieldProperty p, string[] rest, string keyword, string here, JsonElement? item)
        {
            var label = p.Label;
            if (rest is ["path", _, ..] && item is { ValueKind: JsonValueKind.Object } value
                && value.TryGetProperty("path", out var path) && path.ValueKind == JsonValueKind.Array
                && path.GetArrayLength() >= p.Lookup!.MaxDepth)
                return;
            var key = $"{label}: the key is text of 1 to {ContentTreeValidator.MaxIdLength} characters.";
            var text = $"{label}: the text is at most {ContentTreeValidator.MaxLookupTextLength} characters.";
            switch (rest)
            {
                case [var member] when keyword == "": // the false schema of additionalProperties: no such member
                    Add(here, $"{label} has no '{member}'.");
                    return;
                case [] or ["path", _] when keyword == "required":
                    Add($"{here}/key", key);
                    return;
                case []:
                    Add(here, $"{label} is an item of a list: {{ \"key\": …, \"text\": … }}.");
                    return;
                case ["key"] or ["path", _, "key"]:
                    Add(here, key);
                    return;
                case ["text"] or ["path", _, "text"]:
                    Add(here, text);
                    return;
                case ["path"]:
                    Add(here, $"{label}'s path is a list of at most {p.Lookup!.MaxDepth - 1} items above it.");
                    return;
                case ["path", _]:
                    Add(here, $"{label}'s path holds items {{ key, text }}.");
                    return;
            }
        }

        /// <summary>The value at a pointer below <paramref name="root"/>; null when there is none.</summary>
        private static JsonElement? At(JsonElement root, string[] tokens)
        {
            var at = root;
            foreach (var token in tokens)
            {
                if (at.ValueKind == JsonValueKind.Object && at.TryGetProperty(token, out var member)) at = member;
                else if (at.ValueKind == JsonValueKind.Array && int.TryParse(token, out var i) && i < at.GetArrayLength()) at = at[i];
                else return null;
            }
            return at;
        }

        /// <summary>A JSON Pointer's tokens, unescaped (RFC 6901).</summary>
        private static string[] Tokens(string pointer) =>
            pointer.Length == 0 ? [] : [.. pointer.TrimStart('#').Split('/').Skip(1).Select(t => t.Replace("~1", "/").Replace("~0", "~"))];

        private static string Pointer(string[] tokens) => string.Concat(tokens.Select(t => "/" + Escape(t)));

        private static string Escape(string key) => key.Replace("~", "~0").Replace("/", "~1");
    }
}

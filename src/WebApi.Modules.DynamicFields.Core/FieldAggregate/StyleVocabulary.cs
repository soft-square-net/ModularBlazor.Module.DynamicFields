namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// Every style token and value the platform's stylesheet styles (MudTail's <c>_style-tokens.scss</c>, written as
/// <c>data-mb-{token}</c> attributes). It is the same table as the Content Designer's <c>StyleTokens</c>: change all
/// three together, or a value a field offers is not styled, or the designer refuses it. A preset
/// (<see cref="StylePresets"/>) may offer only some of a token's values.
/// </summary>
public static class StyleVocabulary
{
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Values { get; } = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal)
    {
        ["width"] = ["content", "wide", "full", "half", "third"],
        ["align"] = ["start", "center", "end"],
        ["spacing"] = ["none", "sm", "md", "lg", "xl"],
        ["tone"] = ["default", "muted", "accent", "inverse"],
        ["size"] = ["sm", "md", "lg", "xl"],
        ["level"] = ["h1", "h2", "h3", "h4", "h5", "h6"],
        ["aspect"] = ["auto", "1-1", "4-3", "3-2", "16-9", "21-9", "3-4", "9-16"],
        ["columns"] = ["1", "2", "3", "4", "5", "6", "12"],
        ["layout"] = ["standard", "wide", "full"]
    };
}

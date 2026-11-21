using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.Core.Translation;

namespace soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Translate;

/// <summary>A tree's translatable values with their state in one language, and the overlay's problems (when one was sent).</summary>
public sealed record TranslationReport(bool Valid, IReadOnlyList<ContentIssue> Issues, IReadOnlyList<TranslationUnit> Units, TranslationSummary Summary);

/// <summary>
/// List the translatable values of a content tree (the translator's work list) and, given an overlay, check it and say
/// which values are current, stale or missing. Nothing is stored: the consumer (Contents) keeps the overlays.
/// </summary>
public sealed record CheckTranslationQuery(ContentNode Root, ContentTranslation? Translation) : IQuery<Result<TranslationReport>>;

public sealed class CheckTranslationHandler(IReadRepository<FieldDefinition> fields) : IQueryHandler<CheckTranslationQuery, Result<TranslationReport>>
{
    public async ValueTask<Result<TranslationReport>> Handle(CheckTranslationQuery query, CancellationToken ct)
    {
        if (query.Root is null) return Result<TranslationReport>.Invalid(new ValidationError("root", "Send the content tree's root node."));

        var resolve = await PublishedFields.ForTreeAsync(fields, query.Root, ct);
        var issues = query.Translation is null ? [] : ContentTranslator.Validate(query.Root, query.Translation, resolve);
        var units = ContentTranslator.Units(query.Root, resolve, query.Translation);
        return new TranslationReport(issues.Count == 0, issues, units, ContentTranslator.Summarize(units));
    }
}

/// <summary>A tree in the reader's language: the languages tried (best first) and how its values were served.</summary>
public sealed record LocalizedContent(
    ContentNode Root,
    IReadOnlyList<string> Chain,
    IReadOnlyDictionary<string, int> Languages,
    int Stale,
    int Untranslated);

/// <summary>
/// Merge a tree's overlays for a reader: with a culture, the overlays of that culture and its parents (ar-EG, then ar)
/// in that order; without one, every overlay in the order sent. The source fills in what none has.
/// </summary>
public sealed record ApplyTranslationsQuery(ContentNode Root, IReadOnlyList<ContentTranslation> Translations, string? Culture, bool UseStale = true)
    : IQuery<Result<LocalizedContent>>;

public sealed class ApplyTranslationsHandler(IReadRepository<FieldDefinition> fields) : IQueryHandler<ApplyTranslationsQuery, Result<LocalizedContent>>
{
    public async ValueTask<Result<LocalizedContent>> Handle(ApplyTranslationsQuery query, CancellationToken ct)
    {
        if (query.Root is null) return Result<LocalizedContent>.Invalid(new ValidationError("root", "Send the content tree's root node."));
        if (query.Culture is { Length: > 0 } culture && !ContentTranslator.IsLanguage(culture))
            return Result<LocalizedContent>.Invalid(new ValidationError("culture", "The culture is a BCP 47 tag such as ar, en or en-GB."));

        var translations = (query.Translations ?? []).Where(t => t is not null && ContentTranslator.IsLanguage(t.Language)).ToList();
        var chain = query.Culture is { Length: > 0 }
            ? ContentTranslator.Chain(query.Culture, translations.Select(t => t.Language))
            : translations.Select(t => t.Language).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var ordered = chain
            .Select(language => translations.First(t => string.Equals(t.Language, language, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var resolve = await PublishedFields.ForTreeAsync(fields, query.Root, ct);
        var result = ContentTranslator.Apply(query.Root, ordered, resolve, query.UseStale);
        return new LocalizedContent(result.Root, chain, result.Languages, result.Stale, result.Untranslated);
    }
}

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// The schema.org types a field may produce: CreativeWork and the subtypes an editorial site uses. A short, reviewed
/// list rather than any string, so every published post maps to types search engines and AI crawlers understand.
/// Add a type here when a real field needs it (https://schema.org/CreativeWork lists them all).
/// </summary>
public static class SchemaOrg
{
    public static IReadOnlySet<string> CreativeWorkTypes { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "CreativeWork",
        // Articles and posts
        "Article", "NewsArticle", "AnalysisNewsArticle", "BackgroundNewsArticle", "OpinionNewsArticle",
        "ReportageNewsArticle", "ReviewNewsArticle", "BlogPosting", "LiveBlogPosting", "Report", "TechArticle",
        "ScholarlyArticle", "SocialMediaPosting",
        // Media
        "MediaObject", "ImageObject", "VideoObject", "AudioObject", "Photograph", "Clip", "DigitalDocument",
        "ImageGallery", "VideoGallery", "MediaGallery",
        // Parts of a story
        "WebPageElement", "Quotation", "Table", "Comment", "Question", "Answer", "Claim", "ClaimReview", "Review",
        "HowTo", "HowToStep", "FAQPage", "Recipe", "Map", "Dataset",
        // Collections
        "Collection", "CreativeWorkSeries", "Book", "Chapter", "PodcastEpisode"
    };
}

namespace soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;

/// <summary>
/// A schema.org property as the field builder offers it: the value kind it suggests (an image is an
/// <see cref="PropertyKind.Asset"/>, keywords are <see cref="PropertyKind.Keyword"/> concepts, a section is a
/// <see cref="PropertyKind.Term"/>), a label and a short explanation. The author can still change the kind.
/// <see cref="Accept"/>: the files an Asset property takes by default (an image is <c>image/*</c>).
/// </summary>
public sealed record SchemaOrgProperty(
    string Name,
    string Label,
    PropertyKind Kind,
    string Description,
    bool Multiple = false,
    int? MaxLength = null,
    decimal? Minimum = null,
    decimal? Maximum = null,
    IReadOnlyList<string>? Options = null,
    IReadOnlyList<string>? Accept = null);

/// <summary>
/// A schema.org type the builder offers. <see cref="Properties"/> are its own (the parent's come through
/// <see cref="SchemaOrgCatalog.PropertiesOf"/>); <see cref="Suggested"/> are ticked when an author picks the type
/// (null: the parent's). <see cref="ContentAccept"/>: the files its <c>contentUrl</c> takes (an ImageObject's are images;
/// null: the parent's, else any file).
/// </summary>
public sealed record SchemaOrgType(
    string Name,
    string? Parent,
    string Description,
    IReadOnlyList<string> Properties,
    IReadOnlyList<string>? Suggested = null,
    IReadOnlyList<string>? ContentAccept = null);

/// <summary>
/// The curated part of schema.org the builder works with: CreativeWork and the subtypes an editorial site publishes,
/// each with the properties search engines and AI crawlers read, as listed on https://schema.org (Thing's properties
/// are folded into CreativeWork). A short, reviewed list rather than the whole vocabulary, so authors see what
/// matters; add a type or property here when a real field needs it.
/// </summary>
public static class SchemaOrgCatalog
{
    private const string Duration = "An ISO 8601 duration, e.g. PT1M30S (1 minute 30 seconds).";
    private const string People = "Names of people or organizations.";
    private static readonly string[] Images = ["image/*"];

    private static readonly SchemaOrgProperty[] AllProperties =
    [
        // Thing and CreativeWork
        new("name", "Name", PropertyKind.Text, "The name or title of the item.", MaxLength: 200),
        new("alternateName", "Alternative name", PropertyKind.Text, "Other names it is known by.", Multiple: true),
        new("description", "Description", PropertyKind.Text, "A short summary; search results often show it.", MaxLength: 500),
        new("url", "URL", PropertyKind.Url, "The item's own address."),
        new("sameAs", "Same as", PropertyKind.Url, "Pages about the same thing elsewhere (Wikipedia, Wikidata, an official site).", Multiple: true),
        new("identifier", "Identifier", PropertyKind.Text, "An id from another system (ISBN, DOI, product code)."),
        new("image", "Image", PropertyKind.Asset, "An image of the item.", Accept: Images),
        new("thumbnailUrl", "Thumbnail", PropertyKind.Asset, "A small image shown in lists and previews.", Accept: Images),
        new("headline", "Headline", PropertyKind.Text, "The title as shown to readers; Google shows up to 110 characters.", MaxLength: 110),
        new("alternativeHeadline", "Alternative headline", PropertyKind.Text, "A secondary title or subtitle.", MaxLength: 200),
        new("abstract", "Abstract", PropertyKind.Text, "A summary of the work.", MaxLength: 2000),
        new("text", "Text", PropertyKind.RichText, "The textual content."),
        new("author", "Author", PropertyKind.Text, People, Multiple: true),
        new("creator", "Creator", PropertyKind.Text, People, Multiple: true),
        new("contributor", "Contributor", PropertyKind.Text, People, Multiple: true),
        new("editor", "Editor", PropertyKind.Text, People, Multiple: true),
        new("publisher", "Publisher", PropertyKind.Text, "The person or organization that publishes it."),
        new("dateCreated", "Date created", PropertyKind.DateTime, "When the work was created."),
        new("datePublished", "Date published", PropertyKind.DateTime, "When it was first published."),
        new("dateModified", "Date modified", PropertyKind.DateTime, "When it last changed in a way readers notice."),
        new("inLanguage", "Language", PropertyKind.Text, "A BCP 47 language code, e.g. en or ar-EG.", MaxLength: 35),
        new("keywords", "Keywords", PropertyKind.Keyword, "Concepts it is tagged with.", Multiple: true),
        new("about", "About", PropertyKind.Keyword, "The main subjects of the work.", Multiple: true),
        new("mentions", "Mentions", PropertyKind.Keyword, "Subjects the work mentions without being about them.", Multiple: true),
        new("genre", "Genre", PropertyKind.Term, "The genre, from a taxonomy."),
        new("isAccessibleForFree", "Free to access", PropertyKind.Boolean, "Whether it can be read without paying (paywalls)."),
        new("license", "License", PropertyKind.Url, "The address of the license it is published under."),
        new("copyrightHolder", "Copyright holder", PropertyKind.Text, "Who holds the copyright."),
        new("copyrightYear", "Copyright year", PropertyKind.Integer, "The year the copyright was first asserted.", Minimum: 1000, Maximum: 9999),
        new("creditText", "Credit", PropertyKind.Text, "How to credit the work, e.g. \"Photo: Jane Doe / Agency\".", MaxLength: 200),
        new("citation", "Citations", PropertyKind.Url, "Works it cites or refers to.", Multiple: true),
        new("isBasedOn", "Based on", PropertyKind.Url, "Works it is derived from.", Multiple: true),
        new("contentRating", "Content rating", PropertyKind.Text, "An official rating, e.g. PG-13."),
        new("audience", "Audience", PropertyKind.Text, "Who it is written for."),
        new("timeRequired", "Time required", PropertyKind.Text, Duration),
        new("position", "Position", PropertyKind.Integer, "Its position in a list or series.", Minimum: 0),
        new("version", "Version", PropertyKind.Text, "The version of the work."),
        new("encodingFormat", "Format", PropertyKind.Text, "A MIME type, e.g. image/webp or application/pdf."),
        // Article and its subtypes
        new("articleBody", "Article body", PropertyKind.RichText, "The full text of the article."),
        new("articleSection", "Section", PropertyKind.Term, "The section it belongs to (Sports, Business), from a taxonomy."),
        new("wordCount", "Word count", PropertyKind.Integer, "The number of words in the body.", Minimum: 0),
        new("backstory", "Backstory", PropertyKind.RichText, "How and why the article was written."),
        new("dateline", "Dateline", PropertyKind.Text, "Where (and when) the story was reported, e.g. \"CAIRO\".", MaxLength: 100),
        new("printEdition", "Print edition", PropertyKind.Text, "The print edition it appeared in."),
        new("printSection", "Print section", PropertyKind.Text, "The print section it appeared in."),
        new("printPage", "Print page", PropertyKind.Text, "The print page it appeared on."),
        new("sharedContent", "Shared content", PropertyKind.Url, "What the post shares.", Multiple: true),
        new("coverageStartTime", "Coverage starts", PropertyKind.DateTime, "When the live coverage starts."),
        new("coverageEndTime", "Coverage ends", PropertyKind.DateTime, "When the live coverage ends."),
        new("reportNumber", "Report number", PropertyKind.Text, "The number the publisher gave the report."),
        new("proficiencyLevel", "Proficiency level", PropertyKind.Choice, "The reader's expected level.", Options: ["Beginner", "Expert"]),
        new("dependencies", "Prerequisites", PropertyKind.Text, "What the reader needs first.", Multiple: true),
        // Media
        new("contentUrl", "Media file", PropertyKind.Asset, "The file itself."),
        new("embedUrl", "Embed URL", PropertyKind.Url, "A player address to embed it from."),
        new("duration", "Duration", PropertyKind.Text, Duration),
        new("width", "Width", PropertyKind.Integer, "In pixels.", Minimum: 1),
        new("height", "Height", PropertyKind.Integer, "In pixels.", Minimum: 1),
        new("uploadDate", "Upload date", PropertyKind.DateTime, "When it was uploaded; Google requires it for videos."),
        new("contentSize", "File size", PropertyKind.Text, "e.g. 1.2 MB."),
        new("caption", "Caption", PropertyKind.Text, "The caption shown with it.", MaxLength: 500),
        new("representativeOfPage", "Main image of the page", PropertyKind.Boolean, "Whether it represents the whole page."),
        new("transcript", "Transcript", PropertyKind.RichText, "What is said in it, as text."),
        new("videoQuality", "Video quality", PropertyKind.Choice, "The quality of the video.", Options: ["SD", "HD", "4K"]),
        new("clipNumber", "Clip number", PropertyKind.Integer, "Its position among the clips.", Minimum: 0),
        new("startOffset", "Starts at", PropertyKind.Decimal, "Seconds from the start of the media.", Minimum: 0),
        new("endOffset", "Ends at", PropertyKind.Decimal, "Seconds from the start of the media.", Minimum: 0),
        new("episodeNumber", "Episode number", PropertyKind.Integer, "Its number in the season or series.", Minimum: 0),
        // Pages
        new("lastReviewed", "Last reviewed", PropertyKind.Date, "When the page's content was last checked."),
        new("reviewedBy", "Reviewed by", PropertyKind.Text, People, Multiple: true),
        // Parts of a story
        new("spokenByCharacter", "Said by", PropertyKind.Text, "The person who said the quotation."),
        new("upvoteCount", "Upvotes", PropertyKind.Integer, "How many readers approved it.", Minimum: 0),
        new("downvoteCount", "Downvotes", PropertyKind.Integer, "How many readers disapproved of it.", Minimum: 0),
        new("answerCount", "Answer count", PropertyKind.Integer, "How many answers the question has.", Minimum: 0),
        new("firstAppearance", "First appearance", PropertyKind.Url, "Where the claim first appeared."),
        new("appearance", "Appearances", PropertyKind.Url, "Where else the claim appeared.", Multiple: true),
        new("claimInterpreter", "Interpreted by", PropertyKind.Text, "Who interpreted the claim."),
        new("claimReviewed", "Claim reviewed", PropertyKind.Text, "The claim being checked, in short.", MaxLength: 500),
        new("itemReviewed", "Item reviewed", PropertyKind.Text, "What the review is about."),
        new("reviewRating", "Rating", PropertyKind.Decimal, "The rating given, from 1 (worst) to 5 (best).", Minimum: 1, Maximum: 5),
        new("reviewBody", "Review", PropertyKind.RichText, "The text of the review."),
        // How-tos and recipes
        new("totalTime", "Total time", PropertyKind.Text, Duration),
        new("prepTime", "Preparation time", PropertyKind.Text, Duration),
        new("performTime", "Time to do it", PropertyKind.Text, Duration),
        new("cookTime", "Cooking time", PropertyKind.Text, Duration),
        new("estimatedCost", "Estimated cost", PropertyKind.Text, "e.g. 20 USD."),
        new("supply", "Supplies", PropertyKind.Text, "What is used up.", Multiple: true),
        new("tool", "Tools", PropertyKind.Text, "What is needed but not used up.", Multiple: true),
        new("yield", "Yield", PropertyKind.Text, "What it makes, e.g. 1 chair."),
        new("recipeIngredient", "Ingredients", PropertyKind.Text, "One ingredient per item, with its amount.", Multiple: true),
        new("recipeInstructions", "Instructions", PropertyKind.RichText, "The steps."),
        new("recipeYield", "Servings", PropertyKind.Text, "e.g. 4 servings."),
        new("recipeCategory", "Category", PropertyKind.Term, "Dinner, dessert, ..., from a taxonomy."),
        new("recipeCuisine", "Cuisine", PropertyKind.Text, "e.g. Egyptian."),
        // Others
        new("mapType", "Map type", PropertyKind.Choice, "What the map shows.", Options: ["VenueMap", "ParkingMap", "SeatingMap", "TransitMap"]),
        new("variableMeasured", "Variables measured", PropertyKind.Text, "What the data measures.", Multiple: true),
        new("measurementTechnique", "Measurement technique", PropertyKind.Text, "How it was measured.", Multiple: true),
        new("temporalCoverage", "Time covered", PropertyKind.Text, "An ISO 8601 interval, e.g. 2020/2024."),
        new("spatialCoverage", "Place covered", PropertyKind.Text, "The place the data is about."),
        new("collectionSize", "Collection size", PropertyKind.Integer, "How many items the collection has.", Minimum: 0),
        new("startDate", "Start date", PropertyKind.Date, "When it starts."),
        new("endDate", "End date", PropertyKind.Date, "When it ends."),
        new("issn", "ISSN", PropertyKind.Text, "The series' ISSN."),
        new("isbn", "ISBN", PropertyKind.Text, "The book's ISBN."),
        new("numberOfPages", "Pages", PropertyKind.Integer, "How many pages it has.", Minimum: 1),
        new("bookEdition", "Edition", PropertyKind.Text, "e.g. 2nd edition."),
        new("bookFormat", "Book format", PropertyKind.Choice, "The format of the book.",
            Options: ["Hardcover", "Paperback", "EBook", "AudiobookFormat", "GraphicNovel"]),
        new("pageStart", "First page", PropertyKind.Text, "The page it starts on."),
        new("pageEnd", "Last page", PropertyKind.Text, "The page it ends on."),
    ];

    private static readonly string[] ArticleSuggested =
        ["headline", "description", "image", "author", "datePublished", "dateModified", "articleBody", "keywords"];

    private static readonly SchemaOrgType[] AllTypes =
    [
        new("CreativeWork", null, "Any creative work: the base of every type here.",
            ["name", "alternateName", "description", "url", "sameAs", "identifier", "image", "thumbnailUrl", "headline",
             "alternativeHeadline", "abstract", "text", "author", "creator", "contributor", "editor", "publisher",
             "dateCreated", "datePublished", "dateModified", "inLanguage", "keywords", "about", "mentions", "genre",
             "isAccessibleForFree", "license", "copyrightHolder", "copyrightYear", "creditText", "citation", "isBasedOn",
             "contentRating", "audience", "timeRequired", "position", "version", "encodingFormat"],
            ["name", "description"]),

        // Articles and posts
        new("Article", "CreativeWork", "An article: news, investigation, blog post, ...",
            ["articleBody", "articleSection", "wordCount", "backstory"], ArticleSuggested),
        new("NewsArticle", "Article", "A news story, reporting on current events.",
            ["dateline", "printEdition", "printSection", "printPage"],
            ["headline", "description", "image", "author", "datePublished", "dateModified", "dateline", "articleSection", "articleBody", "keywords"]),
        new("AnalysisNewsArticle", "NewsArticle", "News that interprets events, with the author's expertise.", []),
        new("BackgroundNewsArticle", "NewsArticle", "Context for a news story: explainers, timelines, profiles.", []),
        new("OpinionNewsArticle", "NewsArticle", "An opinion piece: editorials, columns, letters.", []),
        new("ReportageNewsArticle", "NewsArticle", "Factual reporting, from the scene or from sources.", []),
        new("ReviewNewsArticle", "NewsArticle", "A review published as news (a film or a product).", []),
        new("SocialMediaPosting", "Article", "A post on a social network or forum.", ["sharedContent"],
            ["headline", "description", "image", "author", "datePublished", "sharedContent"]),
        new("BlogPosting", "SocialMediaPosting", "A blog post.", [], ArticleSuggested),
        new("LiveBlogPosting", "BlogPosting", "Live coverage of an event, updated as it happens.",
            ["coverageStartTime", "coverageEndTime"],
            ["headline", "description", "image", "coverageStartTime", "coverageEndTime", "author", "datePublished"]),
        new("Report", "Article", "A report from an organization or government.", ["reportNumber"],
            ["headline", "description", "reportNumber", "author", "datePublished", "articleBody"]),
        new("TechArticle", "Article", "A technical article: how-to guides, documentation.", ["proficiencyLevel", "dependencies"],
            ["headline", "description", "proficiencyLevel", "dependencies", "author", "datePublished", "articleBody"]),
        new("ScholarlyArticle", "Article", "A scholarly article, for academic readers.", [],
            ["headline", "abstract", "author", "datePublished", "citation", "articleBody"]),

        // Media
        new("MediaObject", "CreativeWork", "A media file: image, video, audio or document.",
            ["contentUrl", "embedUrl", "duration", "width", "height", "uploadDate", "contentSize"],
            ["contentUrl", "name", "description", "encodingFormat"]),
        new("ImageObject", "MediaObject", "An image.", ["caption", "representativeOfPage"],
            ["contentUrl", "caption", "creditText", "copyrightHolder", "license"], ContentAccept: Images),
        new("VideoObject", "MediaObject", "A video.", ["caption", "transcript", "videoQuality"],
            ["contentUrl", "name", "description", "thumbnailUrl", "uploadDate", "duration", "transcript"], ContentAccept: ["video/*"]),
        new("AudioObject", "MediaObject", "An audio file: a recording, a podcast.", ["caption", "transcript"],
            ["contentUrl", "name", "description", "duration", "transcript"], ContentAccept: ["audio/*"]),
        new("Photograph", "CreativeWork", "A photograph as a work (its file is an ImageObject).", [],
            ["image", "name", "creator", "dateCreated", "creditText"]),
        new("Clip", "CreativeWork", "A short part of a video or audio.", ["clipNumber", "startOffset", "endOffset"],
            ["name", "startOffset", "endOffset"]),
        new("DigitalDocument", "CreativeWork", "A document file: PDF, spreadsheet, ...", [],
            ["name", "description", "url", "encodingFormat"]),

        // Pages and galleries
        new("WebPage", "CreativeWork", "A web page.", ["lastReviewed", "reviewedBy"], ["name", "description", "lastReviewed"]),
        new("CollectionPage", "WebPage", "A page that lists other works.", []),
        new("MediaGallery", "CollectionPage", "A page of media.", []),
        new("ImageGallery", "MediaGallery", "A gallery of images.", []),
        new("VideoGallery", "MediaGallery", "A gallery of videos.", []),
        new("FAQPage", "WebPage", "A page of frequently asked questions and their answers.", []),
        new("WebPageElement", "CreativeWork", "A part of a page: a heading, a paragraph, a sidebar.", [], ["text"]),
        new("Table", "WebPageElement", "A table of data on a page.", [], ["name", "text"]),

        // Parts of a story
        new("Quotation", "CreativeWork", "A quotation: what someone said or wrote.", ["spokenByCharacter"],
            ["text", "spokenByCharacter", "citation"]),
        new("Comment", "CreativeWork", "A comment on a work.", ["upvoteCount", "downvoteCount"], ["text", "author", "dateCreated"]),
        new("Question", "Comment", "A question, as in an FAQ or a Q&A.", ["answerCount"],
            ["name", "text", "author", "answerCount", "dateCreated"]),
        new("Answer", "Comment", "An answer to a question.", [], ["text", "author", "dateCreated", "upvoteCount"]),
        new("Claim", "CreativeWork", "A claim someone made, as checked by fact-checkers.",
            ["firstAppearance", "appearance", "claimInterpreter"], ["text", "author", "firstAppearance", "datePublished"]),
        new("Review", "CreativeWork", "A review of something.", ["itemReviewed", "reviewRating", "reviewBody"],
            ["itemReviewed", "reviewRating", "reviewBody", "author", "datePublished"]),
        new("ClaimReview", "Review", "A fact check of a claim.", ["claimReviewed"],
            ["claimReviewed", "reviewRating", "reviewBody", "author", "datePublished", "url"]),
        new("HowTo", "CreativeWork", "Instructions to do something.",
            ["totalTime", "prepTime", "performTime", "estimatedCost", "supply", "tool", "yield"],
            ["name", "description", "image", "totalTime", "supply", "tool"]),
        new("HowToStep", "CreativeWork", "One step of a how-to.", [], ["name", "text", "image", "position"]),
        new("Recipe", "HowTo", "A recipe.",
            ["cookTime", "recipeIngredient", "recipeInstructions", "recipeYield", "recipeCategory", "recipeCuisine"],
            ["name", "description", "image", "recipeIngredient", "recipeInstructions", "prepTime", "cookTime", "recipeYield",
             "recipeCategory", "recipeCuisine", "author", "keywords"]),
        new("Map", "CreativeWork", "A map.", ["mapType"], ["name", "mapType", "url"]),
        new("Dataset", "CreativeWork", "A data set.", ["variableMeasured", "measurementTechnique", "temporalCoverage", "spatialCoverage"],
            ["name", "description", "license", "creator", "variableMeasured", "temporalCoverage", "spatialCoverage", "keywords"]),

        // Collections
        new("Collection", "CreativeWork", "A collection of works.", ["collectionSize"], ["name", "description", "collectionSize"]),
        new("CreativeWorkSeries", "CreativeWork", "A series of works: a column, a book series.", ["startDate", "endDate", "issn"],
            ["name", "description", "startDate", "endDate"]),
        new("Book", "CreativeWork", "A book.", ["isbn", "numberOfPages", "bookEdition", "bookFormat"],
            ["name", "author", "isbn", "bookFormat", "numberOfPages", "datePublished", "publisher", "inLanguage"]),
        new("Chapter", "CreativeWork", "A chapter of a book.", ["pageStart", "pageEnd"], ["name", "pageStart", "pageEnd", "author"]),
        new("Episode", "CreativeWork", "An episode of a series.", ["episodeNumber", "duration"],
            ["name", "episodeNumber", "datePublished", "description", "duration"]),
        new("PodcastEpisode", "Episode", "An episode of a podcast.", []),
    ];

    /// <summary>Every property, by name.</summary>
    public static IReadOnlyDictionary<string, SchemaOrgProperty> Properties { get; } =
        AllProperties.ToDictionary(p => p.Name, StringComparer.Ordinal);

    /// <summary>Every type, in the catalog's order (general before specific within a family).</summary>
    public static IReadOnlyList<SchemaOrgType> Types { get; } = AllTypes;

    private static readonly Dictionary<string, SchemaOrgType> ByName = AllTypes.ToDictionary(t => t.Name, StringComparer.Ordinal);

    /// <summary>A type by name, or null.</summary>
    public static SchemaOrgType? Find(string? name) => name is null ? null : ByName.GetValueOrDefault(name);

    /// <summary>The type's ancestry, from CreativeWork down to the type itself.</summary>
    public static IReadOnlyList<string> PathOf(string name)
    {
        var path = new List<string>();
        for (var type = Find(name); type is not null; type = Find(type.Parent)) path.Insert(0, type.Name);
        return path;
    }

    /// <summary>The properties a type has: its own first, then its parent's, up to CreativeWork's.</summary>
    public static IReadOnlyList<SchemaOrgProperty> PropertiesOf(string name)
    {
        var path = PathOf(name);
        return [.. path.AsEnumerable().Reverse().SelectMany(t => ByName[t].Properties).Select(p => Properties[p])];
    }

    /// <summary>The properties to tick when an author picks the type: its own suggestion, else its nearest parent's.</summary>
    public static IReadOnlyList<string> SuggestedFor(string name)
    {
        for (var type = Find(name); type is not null; type = Find(type.Parent))
            if (type.Suggested is { } suggested) return suggested;
        return [];
    }

    /// <summary>A field property made from a schema.org property, keyed by its name.</summary>
    public static FieldProperty ToFieldProperty(string name, bool required = false, string? type = null)
    {
        var p = Properties[name];
        return new FieldProperty(p.Name, p.Label, p.Kind, required, p.Multiple, p.Name, p.Description,
            MaxLength: p.MaxLength, Minimum: p.Minimum, Maximum: p.Maximum, Options: p.Options,
            Accept: type is null ? p.Accept : AcceptFor(type, name));
    }

    /// <summary>The files the type's <c>contentUrl</c> takes: its own, else its nearest parent's; null: any file.</summary>
    public static IReadOnlyList<string>? ContentAcceptOf(string type)
    {
        for (var t = Find(type); t is not null; t = Find(t.Parent))
            if (t.ContentAccept is { } accept) return accept;
        return null;
    }

    /// <summary>The files a property of the type takes by default: contentUrl per type (a VideoObject's are videos).</summary>
    public static IReadOnlyList<string>? AcceptFor(string type, string property) =>
        property == "contentUrl" ? ContentAcceptOf(type) : Properties.GetValueOrDefault(property)?.Accept;
}

/// <summary>
/// Style choices fields commonly need, as theme tokens the site's theme maps to CSS. The builder offers them as
/// one-click additions; an author can still define any other.
/// </summary>
public static class StylePresets
{
    public static StyleProperty Width { get; } = new("width", "Width", ["content", "wide", "full", "half", "third"], Responsive: true, Default: "content");
    public static StyleProperty Align { get; } = new("align", "Alignment", ["start", "center", "end"], Responsive: true, Default: "start");
    public static StyleProperty Spacing { get; } = new("spacing", "Spacing", ["none", "sm", "md", "lg", "xl"], Responsive: true, Default: "md");
    public static StyleProperty Tone { get; } = new("tone", "Tone", ["default", "muted", "accent", "inverse"], Responsive: true, Default: "default");
    public static StyleProperty Size { get; } = new("size", "Text size", ["sm", "md", "lg", "xl"], Responsive: true, Default: "md");
    /// <summary>Without h1: a page has one, its headline, so a heading block starts at h2.</summary>
    public static StyleProperty Level { get; } = new("level", "Heading level", ["h2", "h3", "h4", "h5", "h6"], Default: "h2");
    public static StyleProperty Aspect { get; } = new("aspect", "Aspect ratio", ["auto", "1-1", "4-3", "3-2", "16-9", "21-9", "3-4", "9-16"], Responsive: true, Default: "auto");
    public static StyleProperty Columns { get; } = new("columns", "Columns", ["1", "2", "3", "4", "5", "6", "12"], Responsive: true, Default: "3");
    public static StyleProperty Layout { get; } = new("layout", "Layout", ["standard", "wide", "full"], Default: "standard");

    public static IReadOnlyList<StyleProperty> All { get; } = [Width, Align, Spacing, Tone, Size, Level, Aspect, Columns, Layout];
}

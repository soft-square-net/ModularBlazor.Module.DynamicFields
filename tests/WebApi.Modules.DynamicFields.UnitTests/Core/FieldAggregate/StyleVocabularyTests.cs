using soft.square.net.WebApi.Modules.DynamicFields.Core.ContentValidation;
using soft.square.net.WebApi.Modules.DynamicFields.Core.FieldAggregate;
using soft.square.net.WebApi.Modules.DynamicFields.UseCases.Content.Validate;

namespace soft.square.net.WebApi.Modules.DynamicFields.UnitTests.Core.FieldAggregate;

/// <summary>The style vocabulary shared with MudTail and the Content Designer, and the report's two lists.</summary>
public class StyleVocabularyTests
{
    /// <summary>
    /// The table, written out. The Content Designer's tests hold the same lines; a change here needs one there and in
    /// MudTail's _style-tokens.scss.
    /// </summary>
    [Fact]
    public void TheVocabularyIsTheSharedTable() =>
        string.Join("; ", StyleVocabulary.Values.OrderBy(v => v.Key, StringComparer.Ordinal).Select(v => $"{v.Key}={string.Join(",", v.Value)}"))
            .ShouldBe("align=start,center,end; aspect=auto,1-1,4-3,3-2,16-9,21-9,3-4,9-16; columns=1,2,3,4,5,6,12; "
                + "layout=standard,wide,full; level=h1,h2,h3,h4,h5,h6; size=sm,md,lg,xl; spacing=none,sm,md,lg,xl; "
                + "tone=default,muted,accent,inverse; width=content,wide,full,half,third");

    [Fact]
    public void EveryPresetOffersOnlyVocabularyValues()
    {
        foreach (var preset in StylePresets.All)
            preset.Values.ShouldBeSubsetOf(StyleVocabulary.Values[preset.Key], preset.Key);
    }

    [Fact]
    public void OnlyIssuesMakeContentInvalid()
    {
        ContentReport.Of([], [new ContentIssue("/content/x", "advice")]).Valid.ShouldBeTrue();
        ContentReport.Of([new ContentIssue("/content/x", "wrong")]).Valid.ShouldBeFalse();
        ContentReport.Of([]).Warnings.ShouldBeEmpty();
    }
}

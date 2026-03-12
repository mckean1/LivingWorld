using LivingWorld.Presentation;
using Xunit;

namespace LivingWorld.Tests;

public sealed class ChronicleColorWriterTests
{
    private static readonly ChronicleColorContext Context = new(
        polityNames: ["River Clan", "Deepfield Tribe"],
        placeNames: ["Amber Reach", "Green Barrow"],
        knowledgeNames: ["Fire", "River Elk Edible"]);

    [Fact]
    public void RegressionNarrative_DoesNotColorDescriptiveProse_AsPositive()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            "Year 121 - Ashfang Wolf in Amber Reach grew tougher under generations of hunting pressure.",
            Context);

        Assert.Contains(segments, segment => segment.Text == "Year 121" && segment.Semantic == ChronicleSemantic.YearHeader);
        Assert.Contains(segments, segment => segment.Text == "Amber Reach" && segment.Semantic == ChronicleSemantic.PlaceName);
        Assert.DoesNotContain(segments, segment => segment.Semantic == ChronicleSemantic.Positive);
        Assert.DoesNotContain(segments, segment => segment.Text.Contains("grew tougher", StringComparison.OrdinalIgnoreCase) && segment.Semantic != ChronicleSemantic.Text);
        Assert.DoesNotContain(segments, segment => segment.Text.Contains("hunting pressure", StringComparison.OrdinalIgnoreCase) && segment.Semantic != ChronicleSemantic.Text);
    }

    [Fact]
    public void PhraseMatching_DoesNotTriggerInsideLargerWords()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            "Year 44 - Deepfield Tribe discussed the shortagestorm without panic.",
            Context);

        Assert.DoesNotContain(segments, segment => segment.Semantic == ChronicleSemantic.Warning && segment.Text.Contains("shortages", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ChronicleNarrative_StillHighlightsPolityAndPlaceNames()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            "Year 72 - River Clan migrated to Amber Reach.",
            Context);

        Assert.Contains(segments, segment => segment.Text == "River Clan" && segment.Semantic == ChronicleSemantic.PolityName);
        Assert.Contains(segments, segment => segment.Text == "Amber Reach" && segment.Semantic == ChronicleSemantic.PlaceName);
    }

    [Fact]
    public void ChronicleNarrative_StillHighlightsKnowledgeNames()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            "Year 73 - River Clan learned Fire after years of hardship.",
            Context);

        Assert.Contains(segments, segment => segment.Text == "Fire" && segment.Semantic == ChronicleSemantic.KnowledgeName);
    }

    [Fact]
    public void FoodStoresStatusLine_ColorsOnlyStateSegment()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            " Food Stores: 63 (Hunger)",
            Context);

        Assert.Contains(segments, segment => segment.Text == "Hunger" && segment.Semantic == ChronicleSemantic.Warning);
        Assert.DoesNotContain(segments, segment => segment.Text.Contains("Food Stores", StringComparison.Ordinal) && segment.Semantic != ChronicleSemantic.Text);
    }

    [Fact]
    public void CrisisNarrative_StillHighlightsCatastropheWords()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            "Year 88 - Famine struck River Clan in Amber Reach.",
            Context);

        Assert.Contains(segments, segment => segment.Text.Contains("Famine", StringComparison.Ordinal) && segment.Semantic == ChronicleSemantic.Crisis);
        Assert.Contains(segments, segment => segment.Text == "River Clan" && segment.Semantic == ChronicleSemantic.PolityName);
        Assert.Contains(segments, segment => segment.Text == "Amber Reach" && segment.Semantic == ChronicleSemantic.PlaceName);
    }

    [Fact]
    public void DiscoveryStatusLine_UsesKnowledgeColor_ForDiscoveryValues()
    {
        ChronicleLineColorizer colorizer = new();

        IReadOnlyList<ChronicleStyledSegment> segments = colorizer.Colorize(
            " Discoveries: River Elk Edible",
            Context);

        Assert.Contains(segments, segment => segment.Text == "River Elk Edible" && segment.Semantic == ChronicleSemantic.KnowledgeName);
    }
}

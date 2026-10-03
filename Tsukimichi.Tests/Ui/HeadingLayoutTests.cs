using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

/// <summary>
/// The one heading line every Moon Road pane draws (<see cref="HeadingLayout"/>): shared metrics, and what gives way
/// first when the room runs out (the caption, then the rule, then the title is cut). And its case policy
/// (<see cref="HeadingCase"/>).
/// </summary>
public class HeadingLayoutTests
{
    // An Eyebrow line of 24 px and a caption of 15 px at scale 1.
    private const float Line = 24f;
    private const float CaptionLine = 15f;

    [Fact]
    public void Metrics_are_shared_top_pad_min_height_sigil_gaps()
    {
        var g = HeadingLayout.Compute(100f, 400f, 1f, Line, 80f, 40f, CaptionLine, sigil: true, CaptionOverflow.Tooltip);

        Assert.Equal(4f, g.Top);
        Assert.Equal(24f, g.Height);
        Assert.Equal(28f, g.TotalHeight);
        Assert.Equal(16f, g.MidY);
        Assert.Equal(10f, g.SigilSize);
        Assert.Equal(105f, g.SigilCenterX);
        Assert.Equal(117f, g.TitleX);                 // sigil 10 + gap 7
        Assert.Equal(117f + 80f + 8f, g.RuleStart);   // 8 after the title
        Assert.True(g.CaptionShown);
        Assert.Equal(460f, g.CaptionX);               // right-aligned
        Assert.Equal(452f, g.RuleEnd);                // 8 before the caption
        Assert.True(g.HasRule);
    }

    [Fact]
    public void A_short_line_keeps_the_minimum_height_and_scales()
    {
        var g = HeadingLayout.Compute(0f, 300f, 2f, 13f, 50f, 0f, 0f, sigil: false, CaptionOverflow.Tooltip);

        Assert.Equal(8f, g.Top);
        Assert.Equal(40f, g.Height);                  // 20 logical at 2x
        Assert.Equal(0f, g.SigilSize);
        Assert.Equal(0f, g.TitleX);
        Assert.Equal(300f, g.RuleEnd);                // no caption: the rule runs to the edge
        Assert.False(g.CaptionShown);
        Assert.False(g.CaptionBelow);
    }

    [Fact]
    public void The_sigil_never_outgrows_a_small_line()
    {
        var g = HeadingLayout.Compute(0f, 300f, 1f, 10f, 50f, 0f, 0f, sigil: true, CaptionOverflow.Tooltip);
        Assert.Equal(8f, g.SigilSize);                // 0.8 of a 10 px line
    }

    [Fact]
    public void The_caption_gives_way_first_then_the_rule_then_the_title_is_cut()
    {
        // Title ends at 17 + 80 = 97; the caption (40) needs 8 + 16 + 8 more: 169 of room.
        var fits = HeadingLayout.Compute(0f, 169f, 1f, Line, 80f, 40f, CaptionLine, sigil: true, CaptionOverflow.Tooltip);
        Assert.True(fits.CaptionShown);
        Assert.Equal(16f, fits.RuleEnd - fits.RuleStart);

        var crowded = HeadingLayout.Compute(0f, 168f, 1f, Line, 80f, 40f, CaptionLine, sigil: true, CaptionOverflow.Tooltip);
        Assert.False(crowded.CaptionShown);
        Assert.False(crowded.CaptionBelow);           // Tooltip: named on hover
        Assert.True(crowded.HasRule);                 // the rule now runs to the edge
        Assert.Equal(168f, crowded.RuleEnd);

        var below = HeadingLayout.Compute(0f, 168f, 1f, Line, 80f, 40f, CaptionLine, sigil: true, CaptionOverflow.Below);
        Assert.True(below.CaptionBelow);

        // Less than 16 px of rule is left out rather than drawn as a stub.
        var noRule = HeadingLayout.Compute(0f, 120f, 1f, Line, 80f, 0f, 0f, sigil: true, CaptionOverflow.Tooltip);
        Assert.False(noRule.HasRule);
        Assert.Equal(103f, noRule.TitleRoom);

        // Then the title is cut to the room left after the sigil.
        var cut = HeadingLayout.Compute(0f, 60f, 1f, Line, 80f, 0f, 0f, sigil: true, CaptionOverflow.Tooltip);
        Assert.Equal(43f, cut.TitleRoom);
        Assert.False(cut.HasRule);
    }

    [Fact]
    public void Nonsense_input_lays_out_nothing_wider_than_the_room()
    {
        var g = HeadingLayout.Compute(10f, float.NaN, float.NaN, float.NaN, float.PositiveInfinity, -5f, float.NaN, sigil: true, CaptionOverflow.Below);
        Assert.Equal(0f, g.TitleRoom);
        Assert.False(g.HasRule);
        Assert.False(g.CaptionShown);
        Assert.False(g.CaptionBelow);
        Assert.Equal(24f, g.TotalHeight);
    }

    [Fact]
    public void English_headings_are_upper_cased_invariantly_and_other_languages_keep_their_case()
    {
        var headings = new HeadingCase();
        Assert.Equal("REQUIREMENTS", headings.For("Requirements", capitals: true));
        Assert.Equal("Voraussetzungen", headings.For("Voraussetzungen", capitals: false));

        // The invariant culture: "i" stays "I" whatever the system culture (Turkish would give "İ").
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("GIVER", headings.For("Giver", capitals: true));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Path_band_labels_follow_the_heading_case_rule()
    {
        // The Path chart's expansion bands go through the heading case (they once upper-cased with the system culture,
        // so a Turkish system printed "SHADOWBRİNGERS"); other languages keep the sheet's own case.
        var headings = new HeadingCase();
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("tr-TR");
            Assert.Equal("SHADOWBRINGERS", headings.For("Shadowbringers", capitals: true));
            Assert.Equal("Shadowbringers", headings.For("Shadowbringers", capitals: false));
            Assert.Equal("漆黒のヴィランズ", headings.For("漆黒のヴィランズ", capitals: false));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Upper_cased_headings_are_cached_and_the_cache_is_bounded()
    {
        var headings = new HeadingCase();
        var first = headings.For("Path", capitals: true);
        Assert.Same(first, headings.For("Path", capitals: true));
        Assert.Equal(1, headings.Count);

        for (var i = 0; i < HeadingCase.MaxCached + 10; i++)
        {
            headings.For("h" + i, capitals: true);
        }

        Assert.InRange(headings.Count, 1, HeadingCase.MaxCached);
        Assert.Equal(0, new HeadingCase().Count);
    }

    [Theory]
    [InlineData(Flair.Full, 30f, 10f)]
    [InlineData(Flair.Quiet, 26f, 8f)]
    [InlineData(Flair.Plain, 22f, 2f)]
    [InlineData((Flair)99, 30f, 10f)]
    public void Section_rows_follow_the_level(Flair flair, float row, float gap)
    {
        Assert.Equal((row, gap), HeadingLayout.SectionRow(flair));
    }

    [Fact]
    public void A_section_row_grows_for_a_taller_title_and_scales()
    {
        Assert.Equal(30f, HeadingLayout.SectionRowHeight(Flair.Full, 1f, 24f));
        Assert.Equal(39f, HeadingLayout.SectionRowHeight(Flair.Full, 1f, 38.6f));
        Assert.Equal(35f, HeadingLayout.SectionRowHeight(Flair.Quiet, 1.35f, 20f));
        Assert.Equal(22f, HeadingLayout.SectionRowHeight(Flair.Plain, float.NaN, float.NaN));
    }
}

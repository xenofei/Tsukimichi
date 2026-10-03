using Tsukimichi.Core.Ui;

namespace Tsukimichi.Tests.Ui;

public class SettingsSearchTests
{
    [Fact]
    public void Sections_are_listed_in_the_planned_order()
    {
        SettingsSection[] expected =
        [
            SettingsSection.General,
            SettingsSection.Journal,
            SettingsSection.TodoOverlay,
            SettingsSection.Alerts,
            SettingsSection.Spoilers,
            SettingsSection.InGame,
            SettingsSection.Automation,
            SettingsSection.Characters,
            SettingsSection.Advanced,
        ];

        Assert.Equal(expected, SettingsSections.Order);
        Assert.Equal(expected.Length, SettingsSections.Count);
    }

    [Fact]
    public void Every_section_is_listed_exactly_once()
    {
        var all = Enum.GetValues<SettingsSection>();
        Assert.Equal(all.Length, SettingsSections.Order.Count);
        Assert.Equal(all.OrderBy(static s => s), SettingsSections.Order.OrderBy(static s => s));
        for (var i = 0; i < SettingsSections.Order.Count; i++)
        {
            Assert.Equal(i, SettingsSections.IndexOf(SettingsSections.Order[i]));
        }

        Assert.Equal(-1, SettingsSections.IndexOf((SettingsSection)99));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t \n")]
    public void Blank_text_matches_everything(string? text)
    {
        var query = SettingsQuery.Parse(text);
        Assert.True(query.IsEmpty);
        Assert.Same(SettingsQuery.Empty, query);
        Assert.True(query.Matches("Window scale"));
        Assert.True(query.Matches(null));
    }

    [Fact]
    public void Text_is_split_into_words()
    {
        var query = SettingsQuery.Parse("  overlay   lock ");
        Assert.False(query.IsEmpty);
        Assert.Equal(["overlay", "lock"], query.Terms);
        Assert.Equal("  overlay   lock ", query.Text);
    }

    [Theory]
    [InlineData("scale", true)]
    [InlineData("SCALE", true)]
    [InlineData("window sca", true)]
    [InlineData("scale window", true)]
    [InlineData("zoom", true)]
    [InlineData("pointer", true)]
    [InlineData("scale opacity", false)]
    [InlineData("opacity", false)]
    public void Every_word_must_be_found_in_the_label_hint_or_keywords(string text, bool expected)
    {
        var query = SettingsQuery.Parse(text);
        Assert.Equal(expected, query.Matches("Window scale", "Text and spacing under the pointer.", "ui size zoom"));
    }

    [Fact]
    public void Matching_ignores_accents()
    {
        Assert.True(SettingsQuery.Parse("pokemon").Matches("Pokémon"));
        Assert.True(SettingsQuery.Parse("Pokémon").Matches("POKEMON"));
    }

    [Fact]
    public void Without_a_query_every_row_shows_and_headings_draw_at_once()
    {
        var filter = new SettingsFilter();
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.General, "Display");
        filter.BeginBlock();
        Assert.False(filter.Active);
        Assert.True(filter.Heading("Look"), "a heading draws at once on a section's page");
        Assert.Equal("Look", filter.TakeBlockHeading());
        Assert.Null(filter.TakeBlockHeading());
        Assert.False(filter.TakeSectionHeading(), "the page title is drawn by the page, not by the filter");
        Assert.True(filter.Row("Flair"));
        Assert.True(filter.Row("Reduce motion"));
        Assert.Equal(2, filter.RowsInBlock);
        Assert.Equal(2, filter.VisibleInBlock);
    }

    [Fact]
    public void A_heading_equal_to_the_section_title_is_not_repeated()
    {
        var filter = new SettingsFilter();
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.General, "Display");
        filter.BeginBlock();
        Assert.True(filter.Heading("Display"));
        Assert.Null(filter.TakeBlockHeading());
    }

    [Fact]
    public void A_query_hides_rows_that_do_not_match_and_headings_wait_for_the_first_match()
    {
        var filter = new SettingsFilter();
        Assert.True(filter.SetText("lock"));
        Assert.False(filter.SetText("lock"), "the same words are no change");
        filter.BeginFrame();

        filter.BeginSection(SettingsSection.General, "Display");
        filter.BeginBlock();
        Assert.False(filter.Heading("Look"), "no match yet: the heading waits");
        Assert.False(filter.Row("Flair", "Full, Quiet or Plain"));
        Assert.Equal(1, filter.RowsInBlock);
        Assert.Equal(0, filter.VisibleInBlock);

        filter.BeginSection(SettingsSection.TodoOverlay, "Todo overlay");
        filter.BeginBlock();
        Assert.False(filter.Heading("Todo overlay"));
        Assert.False(filter.Row("Show the Todo overlay"));
        Assert.True(filter.Row("Locked (click-through)", "Clicks go to the game."));
        Assert.True(filter.TakeSectionHeading());
        Assert.False(filter.TakeSectionHeading(), "the section title is handed out once");
        Assert.Null(filter.TakeBlockHeading());
        Assert.False(filter.Row("Compact"));

        filter.BeginBlock();
        Assert.False(filter.Heading("Sections"));
        Assert.True(filter.Row("Unlock", keywords: "padlock"));
        Assert.False(filter.TakeSectionHeading(), "the section's title was already drawn");
        Assert.Equal("Sections", filter.TakeBlockHeading());

        Assert.Equal(2, filter.VisibleThisFrame);
        filter.EndFrame();
        Assert.Equal(0, filter.Shown(SettingsSection.General));
        Assert.Equal(2, filter.Shown(SettingsSection.TodoOverlay));
        Assert.Equal(2, filter.ShownTotal);
    }

    [Fact]
    public void A_query_matching_the_section_title_shows_the_whole_section()
    {
        var filter = new SettingsFilter();
        filter.SetText("spoil");
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.Spoilers, "Spoilers");
        filter.BeginBlock();
        Assert.True(filter.BlockWhole);
        Assert.True(filter.Heading("Spoilers"));
        Assert.True(filter.TakeSectionHeading());
        Assert.True(filter.Row("Hide main scenario names ahead"));
        Assert.True(filter.Row("Hide artwork"));

        filter.BeginSection(SettingsSection.Advanced, "Keyboard");
        filter.BeginBlock();
        Assert.False(filter.BlockWhole);
        Assert.False(filter.Row("Ctrl+1 to 5 switch tabs"));
    }

    [Fact]
    public void A_query_matching_a_block_heading_or_its_keywords_shows_the_whole_block()
    {
        var filter = new SettingsFilter();
        filter.SetText("questionable");
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.InGame, "Integrations");

        filter.BeginBlock();
        Assert.True(filter.Heading("Questionable"), "the heading matched: the block shows whole, heading first");
        Assert.True(filter.BlockWhole);
        Assert.True(filter.Row("Allow Tsukimichi to start it"));

        filter.BeginBlock("questionable autoduty artisan");
        Assert.True(filter.BlockWhole, "the entry's keywords matched");
        Assert.True(filter.Row("Copy repo URL"));

        filter.BeginBlock();
        Assert.False(filter.BlockWhole, "each block starts afresh");
        Assert.False(filter.Heading("Travel"));
        Assert.False(filter.Row("Show Walk to giver"));
    }

    [Fact]
    public void A_blocks_first_appearance_while_a_query_is_active_is_searched()
    {
        // Regression: a block was skipped while searching until its page had been drawn once, so the search missed
        // every section not opened this session.
        var filter = new SettingsFilter();
        filter.SetText("click-through");
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.TodoOverlay, "Todo overlay");
        filter.BeginBlock();
        bool? rowAware = null;
        Assert.True(filter.DrawsBlock(rowAware), "a block not drawn yet draws, so its settings are searched");
        Assert.False(filter.Heading("Todo overlay"));
        Assert.False(filter.Row("Show the Todo overlay"));
        Assert.True(filter.Row("Locked (click-through)", "Clicks go to the game."));
        rowAware = filter.LearnRowAware(rowAware);
        Assert.True(rowAware);
        filter.EndFrame();
        Assert.Equal(1, filter.Shown(SettingsSection.TodoOverlay));
    }

    [Fact]
    public void A_block_that_registers_no_setting_shows_while_searching_only_when_whole()
    {
        var filter = new SettingsFilter();
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.Advanced, "About");
        filter.BeginBlock();
        Assert.True(filter.DrawsBlock(false), "without a query every block draws");

        filter.SetText("catalog");
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.Advanced, "About");
        filter.BeginBlock();
        bool? rowAware = null;
        Assert.True(filter.DrawsBlock(rowAware));
        rowAware = filter.LearnRowAware(rowAware);
        Assert.False(rowAware, "it drew without registering a setting");

        filter.BeginBlock();
        Assert.False(filter.DrawsBlock(rowAware), "its contents would show unfiltered");
        filter.BeginBlock("catalog stamp");
        Assert.True(filter.DrawsBlock(rowAware), "the entry's keywords matched: it shows whole");
    }

    [Fact]
    public void A_block_known_to_register_settings_stays_known()
    {
        var filter = new SettingsFilter();
        filter.SetText("nothing here");
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.InGame, "Integrations");
        filter.BeginBlock();
        Assert.True(filter.DrawsBlock(true));
        Assert.True(filter.LearnRowAware(true), "a draw that registered nothing (a plugin went away) does not forget");
        Assert.False(filter.LearnRowAware(null));
    }

    [Fact]
    public void Counts_start_again_each_frame()
    {
        var filter = new SettingsFilter();
        filter.SetText("a");
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.Characters, "Data");
        filter.BeginBlock();
        filter.Row("a");
        filter.Row("a");
        filter.EndFrame();
        Assert.Equal(2, filter.Shown(SettingsSection.Characters));

        filter.BeginFrame();
        Assert.Equal(0, filter.VisibleThisFrame);
        Assert.Equal(2, filter.Shown(SettingsSection.Characters));
        filter.EndFrame();
        Assert.Equal(0, filter.Shown(SettingsSection.Characters));
        Assert.Equal(0, filter.ShownTotal);
    }

    [Fact]
    public void Clearing_the_query_shows_everything_again()
    {
        var filter = new SettingsFilter();
        filter.SetText("nothing matches this");
        Assert.True(filter.Active);
        Assert.True(filter.SetText(string.Empty));
        Assert.False(filter.Active);
        filter.BeginFrame();
        filter.BeginSection(SettingsSection.Advanced, "About");
        filter.BeginBlock();
        Assert.True(filter.Row("Plugin version"));
    }

    [Theory]
    [InlineData("General", SettingsSection.General)]
    [InlineData("Journal", SettingsSection.Journal)]
    [InlineData("Overlay", SettingsSection.TodoOverlay)]
    [InlineData("Alerts", SettingsSection.Alerts)]
    [InlineData("Spoilers", SettingsSection.Spoilers)]
    [InlineData("InGame", SettingsSection.InGame)]
    [InlineData("Automation", SettingsSection.Automation)]
    [InlineData("Characters", SettingsSection.Characters)]
    [InlineData("Advanced", SettingsSection.Advanced)]
    [InlineData("Display", SettingsSection.General)]
    [InlineData("About", SettingsSection.General)]
    [InlineData("TodoOverlay", SettingsSection.TodoOverlay)]
    [InlineData("Routes", SettingsSection.TodoOverlay)]
    [InlineData("Notices", SettingsSection.Alerts)]
    [InlineData("Integrations", SettingsSection.InGame)]
    [InlineData("Data", SettingsSection.Characters)]
    [InlineData("Keyboard", SettingsSection.Advanced)]
    [InlineData(" journal ", SettingsSection.Journal)]
    [InlineData("", SettingsSection.General)]
    [InlineData(null, SettingsSection.General)]
    [InlineData("Nonsense", SettingsSection.General)]
    public void A_remembered_page_name_opens_its_page_old_names_included(string? name, SettingsSection expected)
    {
        Assert.Equal(expected, SettingsSections.Parse(name));
    }

    [Fact]
    public void Every_page_round_trips_through_its_name()
    {
        foreach (var section in SettingsSections.Order)
        {
            Assert.Equal(section, SettingsSections.Parse(SettingsSections.Name(section)));
        }
    }

    [Theory]
    [InlineData("Window scale", "Spacing and text in every window.", true)]
    [InlineData("", null, false)]
    [InlineData("1234567890123456789012345678901234567890", null, true)]
    [InlineData("12345678901234567890123456789012345678901", null, false)]
    public void The_copy_rules_cap_labels_at_40_and_hints_at_110(string label, string? hint, bool fits)
    {
        Assert.Equal(fits, SettingsCopy.Fits(label, hint));
        Assert.True(SettingsCopy.Fits("Label", new string('a', SettingsCopy.MaxHint)));
        Assert.False(SettingsCopy.Fits("Label", new string('a', SettingsCopy.MaxHint + 1)));
    }
}

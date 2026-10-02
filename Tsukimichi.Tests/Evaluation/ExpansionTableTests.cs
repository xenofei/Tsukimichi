using Lumina.Excel.Sheets;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ui;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// 8.0 readiness (R10 proposal 6): every expansion the game data has is known to the hand-written tables (a short code,
/// main scenario art, a sidequest region family, a ring icon the Journal tree draws round) and every journal section
/// has its banner. A new expansion or section fails here rather than reading "expansion 6" with the generic art.
/// The fixture checks run everywhere; the sheet checks run with the game data.
/// </summary>
public sealed class ExpansionTableTests(FixtureCatalog fixture, GameDataFixture live) : IClassFixture<FixtureCatalog>, IClassFixture<GameDataFixture>
{
    [Fact]
    public void The_table_answers_for_its_ids_and_degrades_honestly_beyond()
    {
        Assert.Equal(6, Expansions.Count);
        Assert.Equal("ShB", Expansions.ShortCode(3));
        Assert.Equal("Shadowbringers", Expansions.Name(3));
        Assert.Equal((byte)3, Expansions.FromEnglishName(" Shadowbringers "));
        Assert.True(Expansions.IsShortCode("DT"));
        Assert.False(Expansions.IsShortCode("dt"));

        Assert.Null(Expansions.ShortCode(6));
        Assert.Equal("expansion 6", Expansions.Name(6));
        Assert.Null(Expansions.FromEnglishName("Not an expansion"));
    }

    [Fact]
    public void Every_expansion_in_the_catalog_fixture_is_in_the_tables()
    {
        var names = fixture.Bundle.Names.Expansions;
        Assert.NotEmpty(names);
        foreach (var (id, name) in names)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                AssertKnown(id, name);
            }
        }

        foreach (var quest in fixture.Bundle.Catalog.All)
        {
            Assert.True(quest.Expansion < Expansions.Count, $"quest {quest.RowId} is in expansion {quest.Expansion}, which the table lacks");
        }
    }

    [Fact]
    public void Every_journal_section_in_the_catalog_fixture_has_its_banner()
    {
        foreach (var quest in fixture.Bundle.Catalog.All)
        {
            if (quest.Journal.GenreId != 0)
            {
                Assert.True(BannerArts.KnownSections.Contains(quest.Journal.SectionId), $"quest {quest.RowId}: journal section {quest.Journal.SectionId} has no banner rule");
            }
        }
    }

    [GameDataFact]
    public void Every_expansion_in_the_game_data_is_in_the_tables()
    {
        var rows = 0;
        foreach (var row in live.Game.Excel.GetSheet<ExVersion>())
        {
            var name = row.Name.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            rows++;
            AssertKnown(row.RowId, name);
            Assert.True(row.RowId <= byte.MaxValue);
            Assert.Equal(NodeIcons.FirstExpansionRingIcon + row.RowId, row.Icon);
        }

        Assert.Equal(Expansions.Count, rows);
    }

    [GameDataFact]
    public void Every_journal_section_the_game_files_a_quest_under_has_its_banner()
    {
        var sections = live.Game.Excel.GetSheet<JournalSection>();
        foreach (var quest in live.Bundle.Catalog.All)
        {
            if (quest.Journal.GenreId != 0 && !BannerArts.KnownSections.Contains(quest.Journal.SectionId))
            {
                var name = sections.TryGetRow(quest.Journal.SectionId, out var row) ? row.Name.ExtractText() : "?";
                Assert.Fail($"quest {quest.RowId} is filed under journal section {quest.Journal.SectionId} ({name}), which has no banner rule");
            }
        }
    }

    private static void AssertKnown(uint id, string sheetName)
    {
        Assert.True(id < Expansions.Count, $"ExVersion {id} ({sheetName}) has no row in Core.Evaluation.Expansions: add its short code");
        var expansion = (byte)id;
        Assert.False(string.IsNullOrEmpty(Expansions.ShortCode(expansion)));
        // The English fallback is the sheet's English name, so the journal names it parses match the game's.
        Assert.Equal(sheetName, Expansions.Name(expansion));
        Assert.NotEqual(BannerArt.Other, BannerArts.Msq(expansion));
        if (expansion > 0)
        {
            // A Realm Reborn's regions are all mapped by category; every later expansion needs a family for new ones.
            Assert.NotEqual(BannerArt.Other, BannerArts.RegionOf(expansion));
        }

        Assert.True(NodeIcons.IsExpansionRing(NodeIcons.FirstExpansionRingIcon + id));
    }
}

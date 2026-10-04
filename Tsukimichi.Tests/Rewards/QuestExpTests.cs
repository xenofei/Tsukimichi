using Tsukimichi.Core.Model;
using Tsukimichi.Core.Rewards;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;

namespace Tsukimichi.Tests.Rewards;

/// <summary>
/// EXP per quest (1.9.0, R6 G): <c>floor(ExpFactor × ParamGrow[L].QuestExpModifier × ParamGrow[L].ScaledQuestXP / 100)</c>
/// at the level the journal prints, checked against the reward windows the community wiki
/// (ffxiv.consolegameswiki.com) records. The quests' own rows come from the frozen catalog; the ParamGrow rows from
/// the 2026.09.15 game data (<see cref="Sheet"/>), and the live test reads them from the game.
/// </summary>
public sealed class QuestExpTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    /// <summary>The ParamGrow rows the examples need: (level, QuestExpModifier, ScaledQuestXP), as the 2026.09.15 sheet has them.</summary>
    private static readonly (int Level, uint Modifier, uint Scale)[] Sheet =
    [
        (1, 2, 50), (2, 3, 55), (3, 4, 60), (7, 7, 80), (10, 9, 95), (20, 12, 145), (25, 12, 170),
        (50, 39, 300), (58, 39, 340), (59, 39, 345), (60, 50, 500), (70, 72, 750), (73, 72, 765), (79, 72, 795),
        (80, 110, 1020), (89, 110, 1155), (90, 168, 1480), (97, 168, 1620), (99, 168, 1660),
    ];

    private static readonly QuestExpTable Table = QuestExpTable.From(Sheet);

    /// <summary>Quests whose reward window shows one amount: (row id, name, the wiki's EXP).</summary>
    public static TheoryData<uint, string, ulong> Fixed => new()
    {
        { 65621u, "Close to Home", 400UL },
        { 65561u, "Quarrels with Squirrels", 240UL }, // level 1 + offset 2: the level 3 amount
        { 65574u, "Parsemontrenomics", 82UL }, // 82.5 rounds down
        { 65929u, "Vox Populi", 90UL }, // 90.75 rounds down
        { 65631u, "Hematophagic Harassment", 560UL }, // level 4 + offset 3
        { 65541u, "Weapons of a Feather", 5_899UL }, // 5,899.5 rounds down
        { 66049u, "Sylph-management", 6_960UL },
        { 65587u, "Skin in the Game", 40_800UL },
        { 69409u, "Rock the Castrum", 2_340UL },
        { 67541u, "Spinning the Truth", 1_113_840UL },
        { 67204u, "Fetters of Lament", 53_820UL },
        { 68851u, "Spore Sweeper", 220_320UL },
        { 69190u, "Shadowbringers", 22_440UL },
        { 70272u, "Going Haam", 49_728UL },
        { 69649u, "The Gift of Mercy", 994_560UL },
        { 70468u, "Embracing Oblivion", 1_088_640UL },
    };

    /// <summary>Quest Sync quests: the wiki prints the range from the quest's level to its cap.</summary>
    public static TheoryData<uint, string, ulong, ulong> Synced => new()
    {
        { 68879u, "A Cry for Help", 54_000UL, 57_240UL },
        { 69716u, "A Waiting Love", 168_300UL, 190_575UL },
        { 70602u, "Leap into the Unknown", 372_960UL, 418_320UL },
    };

    private QuestRecord Quest(uint rowId, string name)
    {
        var quest = fixture.Bundle.Catalog.GetByRowId(rowId);
        Assert.NotNull(quest);
        Assert.EndsWith(name, quest.Name, StringComparison.Ordinal);
        return quest;
    }

    [Theory]
    [MemberData(nameof(Fixed))]
    public void The_formula_matches_the_reward_window(uint rowId, string name, ulong exp)
    {
        var reward = QuestExp.For(Quest(rowId, name), Table);
        Assert.Equal(ExpKind.Fixed, reward.Kind);
        Assert.Equal(exp, reward.Min);
        Assert.Equal(exp, reward.Max);
    }

    [Theory]
    [MemberData(nameof(Synced))]
    public void A_quest_sync_quest_reads_as_its_range(uint rowId, string name, ulong min, ulong max)
    {
        var reward = QuestExp.For(Quest(rowId, name), Table);
        Assert.Equal(ExpKind.Range, reward.Kind);
        Assert.Equal(min, reward.Min);
        Assert.Equal(max, reward.Max);
    }

    [Theory]
    [InlineData(68515u, "Best Served Foul")] // an allied society daily: the wiki shows 62,500–80,387, not the formula's
    [InlineData(67860u, "The Tools Make the Moogle")] // an allied society story quest: 1,832,220, not the formula's
    [InlineData(66754u, "Brotherhood of Ash")] // 5,720, a tenth of the formula's
    [InlineData(71003u, "We Scare Bears")] // a seasonal event quest: the wiki shows none
    public void Quests_the_formula_does_not_cover_show_no_number(uint rowId, string name)
    {
        var quest = Quest(rowId, name);
        Assert.NotEqual(0u, quest.ExpFactor);
        Assert.Equal(ExpKind.Unknown, QuestExp.For(quest, Table).Kind);
        Assert.False(QuestExp.For(quest, Table).HasAmount);
    }

    [Fact]
    public void No_exp_factor_is_no_exp_and_no_table_is_unknown()
    {
        var dawntrail = Quest(70495u, "Dawntrail");
        Assert.Equal(0u, dawntrail.ExpFactor);
        Assert.Equal(ExpKind.None, QuestExp.For(dawntrail, Table).Kind);

        // The frozen catalog carries no ParamGrow rows: every EXP is unknown rather than guessed.
        Assert.True(fixture.Bundle.ExpTable.IsEmpty);
        Assert.Equal(ExpKind.Unknown, QuestExp.For(Quest(65621u, "Close to Home"), fixture.Bundle.ExpTable).Kind);
    }

    [Fact]
    public void The_table_reads_nothing_past_its_rows_and_never_overflows()
    {
        Assert.Null(Table.At(0, 100));
        Assert.Null(Table.At(100, 100));
        Assert.Null(Table.At(-1, 100));
        Assert.Equal(99, Table.MaxLevel);

        // The largest factor in the sheets (8,400) at level 100's row stays well inside 64 bits.
        var big = QuestExpTable.From([(100, 168u, 1680u)]);
        Assert.Equal(23_708_160UL, big.At(100, 8_400));
        Assert.Equal(4_294_967_295UL * 168 * 1680 / 100, big.At(100, uint.MaxValue));
    }

    [Fact]
    public void An_unknown_level_reads_as_unknown_not_zero()
    {
        var quest = new QuestRecord { RowId = 70000u, Level = 42, ExpFactor = 100 };
        Assert.Equal(ExpKind.Unknown, QuestExp.For(quest, Table).Kind);
    }

    [Fact]
    public void The_share_of_a_level_rounds_to_a_whole_percent_and_says_under_one()
    {
        // ParamGrow's ExpToNext: Lv 56 needs 927,000 (spec-1.19 C8); Lv 100 is the cap, with no next level (0).
        var table = QuestExpTable.From([(56, 39, 330, 927_000), (99, 168, 1660, 4_000_000), (100, 168, 1680, 0)]);
        Assert.Equal(927_000, table.ExpToNext(56));

        // Into the Aery: 50,700 is 5.47%, so 5%.
        Assert.Equal(new LevelShare(5, false), QuestExp.ShareOfLevel(50_700, 56, table));

        // Half a percent rounds away from zero; under 1% says so rather than "0%"; exactly 1% is 1%.
        Assert.Equal(new LevelShare(2, false), QuestExp.ShareOfLevel(13_905, 56, table));
        Assert.Equal(new LevelShare(0, true), QuestExp.ShareOfLevel(9_269, 56, table));
        Assert.Equal(new LevelShare(1, false), QuestExp.ShareOfLevel(9_270, 56, table));

        // More than a level is more than 100%.
        Assert.Equal(new LevelShare(200, false), QuestExp.ShareOfLevel(1_854_000, 56, table));
    }

    [Fact]
    public void No_share_at_the_level_cap_an_unknown_level_or_no_exp()
    {
        var table = QuestExpTable.From([(56, 39, 330, 927_000), (100, 168, 1680, 0)]);

        // The cap has no next level; a level outside the table, level 0 and no EXP say nothing.
        Assert.Null(table.ExpToNext(100));
        Assert.Null(QuestExp.ShareOfLevel(50_700, 100, table));
        Assert.Null(QuestExp.ShareOfLevel(50_700, 0, table));
        Assert.Null(QuestExp.ShareOfLevel(50_700, 57, table));
        Assert.Null(QuestExp.ShareOfLevel(50_700, 120, table));
        Assert.Null(QuestExp.ShareOfLevel(0, 56, table));

        // A table built without the column (the 1.9 shape) and the empty table know no share.
        Assert.Null(Table.ExpToNext(58));
        Assert.Null(QuestExp.ShareOfLevel(100, 58, Table));
        Assert.Null(QuestExp.ShareOfLevel(100, 56, QuestExpTable.Empty));
    }

    [GameDataFact]
    public void The_live_param_grow_has_the_exp_each_level_needs_and_none_at_the_cap()
    {
        using var game = new GameDataFixture();
        var table = game.Bundle.ExpTable;

        // Every level from 1 to 99 needs EXP to the next one; the spec's example: Lv 56 needs 927,000.
        for (var level = 1; level < 100; level++)
        {
            Assert.True(table.ExpToNext(level) > 0, $"Lv {level}");
        }

        Assert.Equal(927_000, table.ExpToNext(56));
        Assert.Equal(new LevelShare(5, false), QuestExp.ShareOfLevel(50_700, 56, table));

        // Nothing past the last row the game fills.
        Assert.Null(table.ExpToNext(table.MaxLevel + 1));
    }

    [GameDataFact]
    public void The_live_param_grow_rows_give_the_reward_windows()
    {
        using var game = new GameDataFixture();
        var table = game.Bundle.ExpTable;
        Assert.False(table.IsEmpty);
        Assert.True(table.MaxLevel >= 100);
        foreach (var (level, modifier, scale) in Sheet)
        {
            Assert.Equal((ulong)modifier * scale, table.At(level, 100));
        }

        foreach (var row in Fixed)
        {
            var quest = game.Bundle.Catalog.GetByRowId((uint)row[0])!;
            Assert.Equal((ulong)row[2], QuestExp.For(quest, table).Min);
        }

        foreach (var row in Synced)
        {
            var reward = QuestExp.For(game.Bundle.Catalog.GetByRowId((uint)row[0])!, table);
            Assert.Equal(((ulong)row[2], (ulong)row[3]), (reward.Min, reward.Max));
        }
    }
}

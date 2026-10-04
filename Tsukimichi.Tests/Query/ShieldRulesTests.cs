using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Export;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Unique;
using Tsukimichi.Core.Unlocks;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// How the panes apply the wider spoiler shield (plan v7, 1.20.0 N6) where one rule takes several calls
/// (<see cref="ShieldRules"/>), from the 1.20.0 UI review: the Journal table's title and Copy table as TSV name a hidden
/// area by its placeholder, travel hides when either character's story has not reached the place, an AutoDuty duty row
/// hides a duty the wider shield hides, and "Reveal names in this quest" covers the quest's duties.
/// </summary>
public class ShieldRulesTests
{
    private const string Nbsp = " ";

    // The story: 1 → 2 (A Realm Reborn) → 3 (Stormblood; opens Kugane) → 4 (opens the Sirensong Sea) → 5.
    private const uint Start = 1;
    private const uint Path = 2;
    private const uint Opener = 3;
    private const uint Dungeon = 4;
    private const uint Far = 5;

    // Side quests: given in Kugane, filed under "Kugane Sidequests"; one in Ul'dah that needs the Sirensong Sea.
    private const uint KuganeSide = 10;
    private const uint DutySide = 11;

    private const uint Kugane = 628;
    private const uint Uldah = 130;
    private const uint Sirensong = 238;
    private const uint SirensongInstance = 62;

    private const uint StormbloodCategory = 70;
    private const uint KuganeGenre = 700;
    private const uint KuganeCategory = 71;
    private const uint KuganeHousingGenre = 710;

    private const uint Hancock = 1009;

    private static QuestRecord Msq(uint rowId, string name, byte level, byte expansion, uint previous, string giver) =>
        Quest(rowId, name, section: 0, category: 1, genre: 1, sortKey: (int)rowId, level: level, expansion: expansion) with
        {
            PreviousQuests = previous == 0 ? Prereq.None : new Prereq([previous], JoinKind.All),
            Issuer = new Issuer(rowId, giver, Uldah, 0, 0f, 0f, 0f),
        };

    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Msq(Start, "Coming to Ul'dah", 1, 0, 0, "Momodi"),
        Msq(Path, "The Path", 50, 0, Start, "Alphinaud"),
        Msq(Opener, "Not without Incident", 61, 2, Path, "Alphinaud"),
        Msq(Dungeon, "Once More to the Ruby Sea", 62, 2, Opener, "Hancock"),
        Msq(Far, "The Far Edge", 70, 2, Dungeon, "Alphinaud"),
        Quest(KuganeSide, "Leves of the East", section: 2, level: 61, expansion: 2) with
        {
            Journal = new JournalRef(2, "Sidequests", StormbloodCategory, "Stormblood Sidequests", KuganeGenre, "Kugane Sidequests", 10),
            Issuer = new Issuer(Hancock, "Hancock", Kugane, 0, 0f, 0f, 0f),
        },
        Quest(DutySide, "A Thirst for the Sea", section: 2, level: 10) with
        {
            Journal = new JournalRef(2, "Sidequests", KuganeCategory, "Kugane Housing", KuganeHousingGenre, "Housing", 11),
            PreviousQuests = new Prereq([Start], JoinKind.All),
            Issuer = new Issuer(2001, "Momodi", Uldah, 0, 0f, 0f, 0f),
            InstanceContentRequired = [SirensongInstance],
        },
    ]);

    private static UnlockLinks Links() => new()
    {
        Zones =
        [
            new UnlockZone(Kugane, "Kugane", "Hingashi", 371, 2, 111, 628),
            new UnlockZone(Uldah, "Ul'dah - Steps of Nald", "Thanalan", 1, 0, 9, 130),
        ],
        Warps = [new UnlockWarp(Opener, Kugane)],
        Duties = [new UnlockDuty(Sirensong, 61801, 61, 2)],
        AreaIcon = 7,
    };

    private static readonly QuestUnlocks Index = QuestUnlocks.Build(
        Catalog,
        UniqueRewardCatalog.Build(
            new UniqueRewardsData("test", default, [new UniqueRewardEntry(Dungeon, RewardKind.DutyUnlock, Sirensong, 0, "the Sirensong Sea", Confidence.Curated, "curated/duty_unlocks.json")]),
            new Dictionary<uint, UniqueOverride>(),
            CuratedData.Empty),
        PlanDuties.From([new PlanDuty(Sirensong, SirensongInstance, UnlockKind.Dungeon, "the Sirensong Sea")]),
        Links());

    private static readonly DutyRunInfo SirensongDuty = new(Sirensong, SirensongInstance, 626, DutyRunInfo.Dungeons, "The Sirensong Sea", OffersDutySupport: true, OffersTrust: false, Icon: 61801) { LevelRequired = 61 };

    private static readonly DutyRunIndex Duties = DutyRunIndex.From([SirensongDuty]);

    private static Dictionary<uint, QuestState> StatesAt(uint next)
    {
        var states = new Dictionary<uint, QuestState>();
        foreach (var quest in Catalog.All)
        {
            states[quest.RowId] = quest.RowId < next || (quest.RowId == DutySide && next > Start) ? QuestState.Completed
                : quest.RowId == next ? QuestState.Ready
                : QuestState.Blocked;
        }

        return states;
    }

    /// <summary>The character whose next main scenario quest is <paramref name="next"/>, with nothing revealed ahead.</summary>
    private static SpoilerMask At(uint next) =>
        SpoilerMask.Build(Catalog, StatesAt(next), SpoilerOptions.Default with { Ahead = 0 }, names: Index.Names);

    private static QuestRecord Q(uint rowId) => Catalog.GetByRowId(rowId)!;

    // ------------------------------------------------------------------ the Journal table's title (review #1)

    [Fact]
    public void The_table_title_names_a_hidden_genre_by_its_placeholder()
    {
        var parts = ShieldRules.JournalScope(QuestScope.Genre(KuganeGenre), Catalog, At(Path));

        Assert.Equal(("Stormblood Sidequests", "Stormblood area" + Nbsp + "1 Sidequests"), parts);
        // Once the story reaches Kugane the genre keeps its own name.
        Assert.Equal(("Stormblood Sidequests", "Kugane Sidequests"), ShieldRules.JournalScope(QuestScope.Genre(KuganeGenre), Catalog, At(Far)));
    }

    [Fact]
    public void The_table_title_names_a_hidden_category_and_a_genre_under_it_by_their_placeholders()
    {
        var mask = At(Path);

        Assert.Equal(("Sidequests", "Stormblood area" + Nbsp + "1 Housing"), ShieldRules.JournalScope(QuestScope.Category(KuganeCategory), Catalog, mask));
        Assert.Equal(("Stormblood area" + Nbsp + "1 Housing", "Housing"), ShieldRules.JournalScope(QuestScope.Genre(KuganeHousingGenre), Catalog, mask));
        Assert.Null(ShieldRules.JournalScope(QuestScope.Genre(9999), Catalog, mask));
        Assert.Null(ShieldRules.JournalScope(QuestScope.Issuer(Hancock), Catalog, mask));
    }

    [Fact]
    public void Quests_from_a_hidden_person_name_them_by_their_placeholder()
    {
        var hidden = ShieldRules.IssuerName(Catalog, Hancock, At(Path));

        Assert.NotNull(hidden);
        Assert.NotEqual("Hancock", hidden);
        Assert.True(SpoilerMask.IsPlaceholder(hidden));
        Assert.Equal("Hancock", ShieldRules.IssuerName(Catalog, Hancock, At(Far)));
        Assert.Null(ShieldRules.IssuerName(Catalog, 424242, At(Path)));
    }

    // ------------------------------------------------------------------ genre names elsewhere (review #2)

    [Fact]
    public void Genre_and_category_print_through_the_shield()
    {
        var mask = At(Path);

        Assert.Equal("Stormblood area" + Nbsp + "1 Sidequests", ShieldRules.Genre(Q(KuganeSide), mask));
        Assert.Equal("Stormblood area" + Nbsp + "1 Housing", ShieldRules.Category(Q(DutySide), mask));
        Assert.Equal("Kugane Sidequests", ShieldRules.Genre(Q(KuganeSide), At(Far)));
    }

    [Fact]
    public void Copy_table_as_TSV_names_a_hidden_genre_by_its_placeholder()
    {
        var mask = At(Path);
        var quest = Q(KuganeSide);

        var row = ExportWriter.Row(quest, null, mask.DisplayName(quest), null, null, null, mask);
        var tsv = TableTsv.Quests([row], static _ => null, static _ => false);

        Assert.Equal("Stormblood area" + Nbsp + "1 Sidequests", row.Genre);
        Assert.DoesNotContain("Kugane", tsv, StringComparison.Ordinal);
        // The export, which writes the character's own data, keeps the journal's names.
        Assert.Equal("Kugane Sidequests", ExportWriter.Row(quest, null, quest.Name, null, null, null).Genre);
    }

    [Fact]
    public void A_journal_node_names_the_hidden_area_its_placeholder_stands_for()
    {
        var mask = At(Path);

        Assert.Equal("Kugane", ShieldRules.NodeArea(mask, "Kugane Sidequests"));
        Assert.Equal("Kugane", ShieldRules.NodeArea(mask, "Kugane"));
        Assert.Null(ShieldRules.NodeArea(mask, "Ul'dah Sidequests"));
        Assert.Null(ShieldRules.NodeArea(At(Far), "Kugane Sidequests"));
    }

    // ------------------------------------------------------------------ travel under two masks (review #3)

    [Fact]
    public void Travel_to_a_place_hides_when_either_the_viewed_or_the_logged_in_story_has_not_reached_it()
    {
        // A main past Kugane views an alt before it: the pane names Kugane by its placeholder, so no travel leads there.
        var main = At(Far);
        var alt = At(Path);

        Assert.True(ShieldRules.PlaceHidden(alt, main, "Kugane"));
        Assert.True(ShieldRules.PlaceHidden(main, alt, "Kugane"));
        Assert.False(ShieldRules.PlaceHidden(main, main, "Kugane"));
        Assert.True(ShieldRules.PlaceHidden(alt, null, "Kugane"));
        Assert.False(ShieldRules.PlaceHidden(null, null, "Kugane"));
        Assert.False(ShieldRules.PlaceHidden(alt, alt, "Ul'dah - Steps of Nald"));
        Assert.False(ShieldRules.PlaceHidden(alt, alt, null));
    }

    // ------------------------------------------------------------------ a duty row's mask (review #4)

    [Fact]
    public void A_duty_row_is_masked_when_the_wider_shield_hides_the_duty_even_through_a_shown_quest()
    {
        var mask = At(Path);
        QuestRecord[] shownThrough = [Q(DutySide)];

        Assert.False(mask.HidesDuty(shownThrough));
        Assert.True(mask.IsNameMasked(SpoilerKind.Duty, SirensongDuty.Name));
        Assert.True(ShieldRules.HidesDuty(mask, shownThrough, SirensongDuty.Name));
        Assert.False(ShieldRules.HidesDuty(At(Far), shownThrough, SirensongDuty.Name));
        // Shown only through masked quests: the board's stand-in, as before.
        Assert.True(ShieldRules.HidesDuty(mask, [Q(Far)], "Some Other Duty"));
    }

    // ------------------------------------------------------------------ Reveal names in this quest (review #5)

    [Fact]
    public void Reveal_names_in_this_quest_covers_the_duties_it_needs()
    {
        var mask = At(Path);
        var quest = Q(DutySide);
        var duties = ShieldRules.DutyNames(quest, Duties, CuratedData.Empty, null);

        Assert.Equal(["The Sirensong Sea"], duties);
        var names = ShieldRules.QuestNames(quest, Index, "Ul'dah - Steps of Nald", "Thanalan", duties);
        Assert.Contains((SpoilerKind.Duty, "The Sirensong Sea"), names);
        // Its giver, place and rewards are all known: only the duty is hidden, and that is enough to offer the reveal.
        Assert.True(ShieldRules.HidesAny(mask, quest, names));
        Assert.Empty(ShieldRules.DutyNames(quest, null, null, null));
    }
}

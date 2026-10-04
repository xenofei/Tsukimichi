using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Data;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>The spoiler shield (T19) over the frozen catalog: a mid-Endwalker character, completed and accepted quests, the shield off, no data.</summary>
public class SpoilerMaskFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private const byte Endwalker = 4;
    private const byte Dawntrail = 5;

    private QuestCatalog Catalog => fixture.Bundle.Catalog;

    /// <summary>Every live main scenario quest in story order: section 0 then section 1, journal order within each.</summary>
    private List<QuestRecord> StoryOrder() =>
        MsqProgress.MainScenarioSections
            .SelectMany(section => Catalog.BySection.GetValueOrDefault(section) ?? [])
            .Where(q => !q.IsRemoved)
            .ToList();

    /// <summary>
    /// A character halfway through Endwalker: every main scenario quest before the middle Endwalker one completed,
    /// the rest blocked (the middle one Ready). Grand Company branches are left completed, so none is foreclosed and
    /// the ordinal walk counts every quest.
    /// </summary>
    private (Dictionary<uint, QuestState> States, List<QuestRecord> Story, int Position) MidEndwalker()
    {
        var story = StoryOrder();
        var endwalker = story.Select((q, i) => (q, i)).Where(p => p.q.Expansion == Endwalker).ToList();
        Assert.True(endwalker.Count > 20, "the fixture holds the Endwalker main scenario");
        var position = endwalker[endwalker.Count / 2].i;
        var states = new Dictionary<uint, QuestState>();
        for (var i = 0; i < story.Count; i++)
        {
            states[story[i].RowId] = i < position ? QuestState.Completed : i == position ? QuestState.Ready : QuestState.Blocked;
        }

        return (states, story, position);
    }

    [Fact]
    public void Mid_endwalker_masks_dawntrail_names_but_not_the_next_three()
    {
        var (states, story, position) = MidEndwalker();

        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);

        Assert.Equal(story[position].RowId, MsqProgress.Compute(Catalog, states)?.Next?.RowId);
        for (var i = 0; i <= position + SpoilerOptions.DefaultAhead; i++)
        {
            Assert.False(mask.IsMasked(story[i]), $"{story[i].RowId} at {i - position} past the position keeps its name");
            Assert.Equal(story[i].Name, mask.DisplayName(story[i]));
        }

        for (var i = position + SpoilerOptions.DefaultAhead + 1; i < story.Count; i++)
        {
            Assert.True(mask.IsMasked(story[i]), $"{story[i].RowId} at {i - position} past the position is masked");
        }

        var dawntrail = story.Where(q => q.Expansion == Dawntrail).ToList();
        Assert.NotEmpty(dawntrail);
        Assert.All(dawntrail, q =>
        {
            Assert.True(mask.IsMasked(q));
            Assert.Equal($"Main scenario quest (Lv {q.DisplayLevel})", mask.DisplayName(q));
            Assert.DoesNotContain(q.Name, mask.DisplayName(q), StringComparison.Ordinal);
        });

        // Side quests are never masked, whatever their expansion.
        Assert.All(Catalog.All.Where(q => !FeaturePresets.IsMainScenario(q)), q => Assert.False(mask.IsMasked(q)));
        Assert.Equal(Endwalker, mask.ReachExpansion);
    }

    [Fact]
    public void Completed_and_accepted_quests_are_never_masked()
    {
        var (states, story, position) = MidEndwalker();
        var accepted = story[^1];
        var completedAhead = story[position + 20];
        states[accepted.RowId] = QuestState.Accepted;
        states[completedAhead.RowId] = QuestState.Completed;

        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);

        Assert.False(mask.IsMasked(accepted));
        Assert.False(mask.IsMasked(completedAhead));
        Assert.All(story.Where(q => states[q.RowId] == QuestState.Completed), q => Assert.False(mask.IsMasked(q)));
        Assert.True(mask.IsMasked(story[^2]));
    }

    [Fact]
    public void An_abandoned_quest_far_past_the_position_keeps_its_name()
    {
        var (states, story, position) = MidEndwalker();
        var abandonedQuest = story.Last(q => q.Expansion == Dawntrail);
        var other = story[^2] == abandonedQuest ? story[^3] : story[^2];
        var ledger = new Dictionary<ushort, AbandonedEntry>
        {
            [abandonedQuest.QuestId] = new AbandonedEntry(abandonedQuest.QuestId, DateTime.UtcNow, 3, 5),
        };

        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default, abandoned: ledger);
        var evaluated = SpoilerMask.Build(Catalog, states.ToDictionary(p => p.Key, p => new QuestEvaluation(p.Value, [], null, null, null)), SpoilerOptions.Default, abandoned: ledger);

        Assert.True(story.IndexOf(abandonedQuest) - position > SpoilerOptions.DefaultAhead);
        Assert.False(mask.IsMasked(abandonedQuest));
        Assert.Equal(abandonedQuest.Name, mask.DisplayName(abandonedQuest));
        Assert.False(evaluated.IsMasked(abandonedQuest));
        Assert.True(mask.IsMasked(other));
        Assert.True(SpoilerMask.Build(Catalog, states, SpoilerOptions.Default).IsMasked(abandonedQuest));
    }

    [Fact]
    public void Shield_off_masks_nothing_and_shows_every_banner()
    {
        var (states, story, _) = MidEndwalker();

        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Off);

        Assert.Equal(0, mask.MaskedCount);
        Assert.All(story, q => Assert.Equal(q.Name, mask.DisplayName(q)));
        Assert.True(mask.ShowArtwork(story[^1], QuestState.Blocked));
    }

    [Fact]
    public void A_character_without_data_masks_every_main_scenario_quest_after_the_first()
    {
        var story = StoryOrder();

        var mask = SpoilerMask.Build(Catalog, new Dictionary<uint, QuestEvaluation>(), SpoilerOptions.Default);

        Assert.False(mask.IsMasked(story[0]));
        Assert.All(story.Skip(1), q => Assert.True(mask.IsMasked(q)));
        Assert.Equal(story.Count - 1, mask.MaskedCount);
        Assert.Equal(0, mask.ReachExpansion);
    }

    [Fact]
    public void Revealed_quests_keep_their_names_and_change_the_fingerprint()
    {
        var (states, story, position) = MidEndwalker();
        var target = story[position + 10];

        var masked = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);
        var revealed = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default, new HashSet<uint> { target.RowId });

        Assert.True(masked.IsMasked(target));
        Assert.False(revealed.IsMasked(target));
        Assert.Equal(masked.MaskedCount - 1, revealed.MaskedCount);
        Assert.NotEqual(masked.Fingerprint, revealed.Fingerprint);
        Assert.Equal(masked.Fingerprint, SpoilerMask.Build(Catalog, states, SpoilerOptions.Default).Fingerprint);
    }

    [Fact]
    public void Quests_ahead_setting_moves_the_boundary()
    {
        var (states, story, position) = MidEndwalker();

        var none = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        var ten = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 99 });

        Assert.False(none.IsMasked(story[position]));
        Assert.True(none.IsMasked(story[position + 1]));
        Assert.False(ten.IsMasked(story[position + SpoilerOptions.MaxAhead]));
        Assert.True(ten.IsMasked(story[position + SpoilerOptions.MaxAhead + 1]));
    }

    [Fact]
    public void Search_finds_a_masked_quest_by_its_placeholder_and_never_by_its_name()
    {
        var (states, story, position) = MidEndwalker();
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);
        var hidden = story.Last(q => q.Expansion == Dawntrail);
        var index = SearchIndex.For(Catalog);
        var byName = SearchIndex.Normalize(hidden.Name);

        Assert.True(index.Matches(hidden.RowId, byName));
        Assert.False(index.Matches(hidden.RowId, byName, mask));
        Assert.True(index.Matches(hidden.RowId, SearchIndex.Normalize("main scenario"), mask));
        Assert.True(index.Matches(story[position].RowId, SearchIndex.Normalize(story[position].Name), mask));

        var result = QuestQuery.Apply(Catalog, states, new FilterSet(), QuestScope.None, SortSpec.Default, hidden.Name, QueryContext.Empty with { Spoilers = mask });
        Assert.DoesNotContain(result.Rows, row => row.Quest.RowId == hidden.RowId);
    }

    [Fact]
    public void Sprout_mode_lists_quests_at_or_below_the_main_scenario_expansion()
    {
        var (states, _, _) = MidEndwalker();
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);
        var filters = new FilterSet { Preset = Preset.Sprout, IncludeUnlisted = false };

        var result = QuestQuery.Apply(Catalog, states, filters, QuestScope.None, SortSpec.Default, null, QueryContext.Empty with { Spoilers = mask });

        Assert.NotEmpty(result.Rows);
        Assert.All(result.Rows, row => Assert.True(row.Quest.Expansion <= Endwalker));
        Assert.Contains(result.Rows, row => row.Quest.Expansion == Endwalker);
        Assert.Equal(Catalog.All.Count(q => !q.IsRemoved && q.Expansion <= Endwalker), result.Rows.Length);
    }

    [Fact]
    public void Blocker_and_todo_text_print_the_placeholder_for_a_masked_quest()
    {
        var (states, story, position) = MidEndwalker();
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default);
        var hidden = story[position + 6];
        var previous = hidden.PreviousQuests.QuestIds.Select(Catalog.GetByRowId).OfType<QuestRecord>().FirstOrDefault(mask.IsMasked);
        Assert.NotNull(previous);
        var names = fixture.Bundle.BlockerNames() with { QuestName = mask.DisplayName };
        var evaluations = states.ToDictionary(p => p.Key, p => new QuestEvaluation(p.Value, [], null, null, null));
        var evaluation = new QuestEvaluation(
            QuestState.Blocked,
            [new RequirementResult(new PreviousQuestsRequirement(hidden.PreviousQuests.QuestIds, hidden.PreviousQuests.Join, 0, []), false, "needs " + previous.Name)],
            null,
            null,
            null);
        evaluation = evaluation with { NextStep = evaluation.Requirements[0] };

        var status = BlockerText.StatusText(evaluation, hidden, names, evaluations);

        Assert.DoesNotContain(previous.Name, status, StringComparison.Ordinal);
        Assert.Contains(SpoilerMask.Placeholder(previous), status, StringComparison.Ordinal);
        Assert.Equal("needs " + SpoilerMask.Placeholder(previous), mask.MaskNamesIn("needs " + previous.Name, Catalog, hidden.PreviousQuests.QuestIds));
    }

    [Fact]
    public void Artwork_shows_only_for_accepted_or_completed_quests_while_the_shield_is_on()
    {
        var quest = Catalog.All.First(q => q.Icon != 0);
        var mask = SpoilerMask.Build(Catalog, new Dictionary<uint, QuestState>(), SpoilerOptions.Default);

        Assert.True(mask.ShowArtwork(quest, QuestState.Accepted));
        Assert.True(mask.ShowArtwork(quest, QuestState.Completed));
        Assert.True(mask.ShowArtwork(quest, QuestState.DoneThisCycle));
        foreach (var state in new[] { QuestState.Ready, QuestState.ReadyOnOtherJob, QuestState.Blocked, QuestState.Foreclosed, QuestState.Unknown })
        {
            Assert.False(mask.ShowArtwork(quest, state));
        }

        var namesOnly = SpoilerMask.Build(Catalog, new Dictionary<uint, QuestState>(), SpoilerOptions.Default with { HideArtwork = false });
        Assert.True(namesOnly.ShowArtwork(quest, QuestState.Blocked));
    }
}

/// <summary>The spoiler shield over a small hand-built catalog.</summary>
public class SpoilerMaskTests
{
    // Section 0 then section 1, journal order; 20 is a side quest.
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Coming to Gridania", section: 0, category: 1, genre: 1, sortKey: 10, level: 1),
        Quest(2, "Close to Home", section: 0, category: 1, genre: 1, sortKey: 20, level: 1),
        Quest(3, "The Company You Keep (Maelstrom)", section: 0, category: 1, genre: 1, sortKey: 30, level: 20),
        Quest(4, "The Company You Keep (Twin Adder)", section: 0, category: 1, genre: 1, sortKey: 31, level: 20),
        Quest(5, "Coming to Ishgard", section: 0, category: 2, genre: 2, sortKey: 40, level: 50, expansion: 1),
        Quest(6, "A Vow of Virtue", section: 1, category: 3, genre: 3, sortKey: 50, level: 90, expansion: 5),
        Quest(7, "Secret Finale", section: 1, category: 3, genre: 3, sortKey: 60, level: 100, expansion: 5),
        Quest(20, "Side quest", section: 2, category: 10, genre: 100, sortKey: 5, expansion: 5),
    ]);

    [Fact]
    public void Foreclosed_branches_hold_no_place_in_the_count()
    {
        var states = States((1, QuestState.Completed), (2, QuestState.Ready), (3, QuestState.Blocked), (4, QuestState.Foreclosed), (5, QuestState.Blocked), (6, QuestState.Blocked), (7, QuestState.Blocked));

        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 2 });

        // Position 2; 3 and 5 are the two ahead (4 is skipped); 6 and 7 are masked.
        Assert.False(mask.IsMasked(3));
        Assert.False(mask.IsMasked(5));
        Assert.True(mask.IsMasked(6));
        Assert.True(mask.IsMasked(7));
        Assert.False(mask.IsMasked(20));
        Assert.Equal(0, mask.ReachExpansion);
    }

    [Fact]
    public void A_completed_story_masks_nothing_and_reaches_every_expansion()
    {
        var mask = SpoilerMask.Build(Catalog, States(Catalog, QuestState.Completed), SpoilerOptions.Default);

        Assert.Equal(0, mask.MaskedCount);
        Assert.Equal(byte.MaxValue, mask.ReachExpansion);
    }

    [Fact]
    public void Name_sort_orders_masked_quests_by_their_placeholder()
    {
        var states = States((1, QuestState.Ready), (2, QuestState.Blocked), (3, QuestState.Blocked), (4, QuestState.Blocked), (5, QuestState.Blocked), (6, QuestState.Blocked), (7, QuestState.Blocked));
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        var ctx = QueryContext.Empty with { Spoilers = mask };

        var result = Run(Catalog, states, scope: QuestScope.Section(1), sort: SortSpec.Default with { Column = SortColumn.Name }, ctx: ctx);

        // Masked, both print as placeholders and order by their level as a number: "(Lv 90)" before "(Lv 100)",
        // not as text, where '1' < '9' would put Lv 100 first.
        Assert.Equal([6u, 7u], RowIds(result));
        Assert.Equal([7u, 6u], RowIds(Run(Catalog, states, scope: QuestScope.Section(1), sort: SortSpec.Default with { Column = SortColumn.Name, Descending = true }, ctx: ctx)));

        // Unmasked, by name: "A Vow of Virtue" (6) before "Secret Finale" (7).
        Assert.Equal([6u, 7u], RowIds(Run(Catalog, states, scope: QuestScope.Section(1), sort: SortSpec.Default with { Column = SortColumn.Name })));

        // Placeholders sort among real names as text: "Coming to Gridania" (1, shown) before "Main scenario quest (…)".
        var all = RowIds(Run(Catalog, states, scope: QuestScope.Section(0), sort: SortSpec.Default with { Column = SortColumn.Name }, ctx: ctx));
        Assert.Equal(1u, all[0]);
    }

    [Fact]
    public void Masked_names_compare_by_level_and_among_real_names_as_text()
    {
        var states = States((1, QuestState.Ready), (2, QuestState.Blocked), (3, QuestState.Blocked), (4, QuestState.Blocked), (5, QuestState.Blocked), (6, QuestState.Blocked), (7, QuestState.Blocked));
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        var q = (uint id) => Catalog.GetByRowId(id)!;
        Assert.True(mask.IsMasked(6) && mask.IsMasked(7) && !mask.IsMasked(1));

        Assert.True(mask.CompareDisplayNames(q(6), q(7)) < 0);
        Assert.True(mask.CompareDisplayNames(q(7), q(6)) > 0);
        Assert.Equal(0, mask.CompareDisplayNames(q(6), q(6)));
        Assert.True(mask.CompareDisplayNames(q(1), q(7)) < 0);
        Assert.True(mask.CompareDisplayNames(q(20), q(7)) > 0);
    }

    [Fact]
    public void Masking_names_in_text_replaces_longest_names_first_on_word_boundaries()
    {
        var catalog = QuestCatalog.Build(
        [
            Quest(1, "Start", section: 0, category: 1, genre: 1, sortKey: 10, level: 1),
            Quest(2, "Endwalker Prologue", section: 0, category: 1, genre: 1, sortKey: 20, level: 80),
            Quest(3, "Endwalker", section: 0, category: 1, genre: 1, sortKey: 30, level: 90),
            Quest(4, "Endwalker Finale", section: 0, category: 1, genre: 1, sortKey: 40, level: 100),
            Quest(5, "Home", section: 0, category: 1, genre: 1, sortKey: 50, level: 95),
        ]);
        var states = States((1, QuestState.Ready), (2, QuestState.Blocked), (3, QuestState.Blocked), (4, QuestState.Blocked), (5, QuestState.Blocked));
        var mask = SpoilerMask.Build(catalog, states, SpoilerOptions.Default with { Ahead = 1 });
        Assert.False(mask.IsMasked(2));
        Assert.True(mask.IsMasked(3));
        Assert.True(mask.IsMasked(4));
        Assert.True(mask.IsMasked(5));

        // Masked "Endwalker" inside masked "Endwalker Finale": the longer name wins whatever the list order.
        Assert.Equal(
            "needs Main scenario quest (Lv 100) and Main scenario quest (Lv 90)",
            mask.MaskNamesIn("needs Endwalker Finale and Endwalker", catalog, [3u, 4u]));

        // Masked "Endwalker" inside a listed name that is not masked stays as it is.
        Assert.Equal(
            "needs Endwalker Prologue and Main scenario quest (Lv 90)",
            mask.MaskNamesIn("needs Endwalker Prologue and Endwalker", catalog, [3u, 2u]));

        // Only whole words: "Home" inside "Homestead" is not a quest name.
        Assert.Equal(
            "Homestead, then Main scenario quest (Lv 95).",
            mask.MaskNamesIn("Homestead, then Home.", catalog, [5u]));
    }

    [Fact]
    public void A_duty_is_hidden_only_when_every_quest_it_is_shown_through_is_masked()
    {
        // 1.19.0 review: "How you'll clear it" printed a masked quest's duties by name; the detail pane asks for the
        // quest on view, the Duties board for every quest that unlocks the duty.
        var states = States((1, QuestState.Ready), (2, QuestState.Blocked), (3, QuestState.Blocked), (4, QuestState.Blocked), (5, QuestState.Blocked), (6, QuestState.Blocked), (7, QuestState.Blocked));
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        var q = (uint id) => Catalog.GetByRowId(id)!;
        Assert.True(mask.IsMasked(7) && mask.IsMasked(6) && !mask.IsMasked(1));

        Assert.True(mask.HidesDuty([q(7)]));
        Assert.True(mask.HidesDuty([q(6), q(7)]));
        Assert.False(mask.HidesDuty([q(1)]));
        Assert.False(mask.HidesDuty([q(7), q(20)]), "one quest the player may see names the duty");
        Assert.False(mask.HidesDuty([]), "a duty no quest leads to keeps its name");
        Assert.False(SpoilerMask.Build(Catalog, states, SpoilerOptions.Off).HidesDuty([q(7)]));
    }

    [Fact]
    public void Blocker_names_route_quest_names_through_the_mask()
    {
        var states = States((1, QuestState.Ready));
        var mask = SpoilerMask.Build(Catalog, states, SpoilerOptions.Default with { Ahead = 0 });
        var names = BlockerNames.Default with { Catalog = Catalog, QuestName = mask.DisplayName };

        Assert.Equal("Main scenario quest (Lv 100)", names.QuestName(Catalog.GetByRowId(7)!));
        Assert.Equal("Coming to Gridania", names.QuestName(Catalog.GetByRowId(1)!));
        Assert.Equal("Side quest", names.QuestName(Catalog.GetByRowId(20)!));
        Assert.Equal("Close to Home", BlockerNames.Default.QuestName(Catalog.GetByRowId(2)!));
    }
}

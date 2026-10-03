using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using static Tsukimichi.Tests.Query.QueryTestData;

namespace Tsukimichi.Tests.Query;

/// <summary>
/// The Journal badge (plan v7, spec Revision 3 R3.2): quests newly ready since the player last looked, the seen set it
/// keeps per character, the badge's count per mode and its one fixed text, and the Newly ready list it opens.
/// </summary>
public class NewlyReadyTests
{
    private static readonly QuestCatalog Catalog = QuestCatalog.Build(
    [
        Quest(1, "Main scenario", section: 0, category: 10, genre: 100),
        Quest(2, "Unlock", section: 2, category: 12, genre: 103),
        Quest(3, "Side", section: 2, category: 12, genre: 103),
        Quest(4, "Other job", section: 2, category: 12, genre: 103),
        Quest(5, "Blocked", section: 2, category: 12, genre: 103),
        Quest(6, "Done", section: 2, category: 12, genre: 103),
        Quest(7, "In journal", section: 2, category: 12, genre: 103),
        Quest(8, "Removed", section: 2, category: 12, genre: 0),
        Quest(9, "Weekly", section: 2, category: 12, genre: 103, repeatable: true),
    ]);

    private static readonly HashSet<uint> Features = [2];

    private static Dictionary<uint, QuestEvaluation> Evaluate(params (uint RowId, QuestState State)[] entries) =>
        Evaluations(States(entries));

    private static readonly Dictionary<uint, QuestEvaluation> Today = Evaluate(
        (1, QuestState.Ready),
        (2, QuestState.Ready),
        (3, QuestState.Ready),
        (4, QuestState.ReadyOnOtherJob),
        (5, QuestState.Blocked),
        (6, QuestState.Completed),
        (7, QuestState.Accepted),
        (8, QuestState.Ready),
        (9, QuestState.Ready));

    [Fact]
    public void Available_is_ready_or_ready_on_another_job_counted_and_not_done()
    {
        var tally = NewlyReady.Tally(Catalog, Today, Features);

        // Removed quests and repeatables outside the counts are never available; Blocked, done and accepted are not.
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, tally.Available);
        Assert.Equal(3, tally.Ready);
        Assert.Equal(2, tally.StoryReady);
    }

    [Fact]
    public void Ready_agrees_with_the_trees_ready_count()
    {
        Assert.Equal(TreeCounts.Compute(Catalog, Today, includeUnlisted: false).OverallReady, NewlyReady.Tally(Catalog, Today, Features).Ready);
    }

    [Fact]
    public void The_first_look_seeds_the_seen_set_so_a_returning_player_starts_at_zero()
    {
        var state = NewlyReady.Reconcile([3, 1, 2, 4], seen: null);

        Assert.Empty(state.New);
        Assert.Equal(new uint[] { 1, 2, 3, 4 }, state.Seen);
        Assert.True(state.SeenChanged);
    }

    [Fact]
    public void Nothing_available_seeds_nothing_so_the_next_evaluation_is_not_a_flood()
    {
        // A capture committed before the game sent the quest data lists nothing: no set is seeded or saved.
        var empty = NewlyReady.Reconcile([], seen: null);
        Assert.Empty(empty.New);
        Assert.Null(empty.Seen);
        Assert.False(empty.SeenChanged);

        // So the first real evaluation seeds, rather than reporting all 300 quests as new.
        var real = NewlyReady.Reconcile([.. Enumerable.Range(1, 300).Select(static i => (uint)i)], empty.Seen);
        Assert.Empty(real.New);
        Assert.Equal(300, real.Seen!.Length);
        Assert.True(real.SeenChanged);
    }

    [Fact]
    public void A_seen_set_kept_under_other_rules_is_seeded_again()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tsukimichi-seen-rules-{Guid.NewGuid():N}.json");
        try
        {
            var book = new CharacterSettingsBook(path);
            book.Edit(CharacterSettingChange.Seen(7, [5, 6]));
            Assert.Equal(NewlyReady.RulesVersion, book.Get(7)!.SeenReadyRules);

            // A set written under another version of the rules reads as none, so the badge seeds it again silently.
            var text = File.ReadAllText(path);
            Assert.Matches("\"seenReadyRules\"\\s*:\\s*" + NewlyReady.RulesVersion, text);
            File.WriteAllText(path, System.Text.RegularExpressions.Regex.Replace(text, "(\"seenReadyRules\"\\s*:\\s*)\\d+", "${1}" + (NewlyReady.RulesVersion + 1)));
            var newer = new CharacterSettingsBook(path);
            newer.Load();
            Assert.Null(newer.SeenReady(7));

            // One written before the version was stored reads as the first rules.
            File.WriteAllText(path, System.Text.RegularExpressions.Regex.Replace(text, ",?\\s*\"seenReadyRules\"\\s*:\\s*\\d+", string.Empty));
            var older = new CharacterSettingsBook(path);
            older.Load();
            Assert.Null(older.Get(7)!.SeenReadyRules);
            var rules = NewlyReady.RulesVersion;
            if (rules == 1)
            {
                Assert.Equal(new uint[] { 5, 6 }, older.SeenReady(7));
            }
            else
            {
                Assert.Null(older.SeenReady(7));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void What_became_available_since_is_new()
    {
        var state = NewlyReady.Reconcile([1, 2, 3, 4, 5], seen: [1, 2, 3, 4]);

        Assert.Equal(new uint[] { 5 }, state.New);
        Assert.False(state.SeenChanged);
    }

    [Fact]
    public void Switching_jobs_makes_nothing_new()
    {
        // Ready and Ready on another job are both available: a job change only moves quests between the two.
        var before = NewlyReady.Tally(Catalog, Today, Features);
        var afterSwitch = Evaluate((1, QuestState.ReadyOnOtherJob), (2, QuestState.ReadyOnOtherJob), (3, QuestState.ReadyOnOtherJob), (4, QuestState.Ready));
        var after = NewlyReady.Tally(Catalog, afterSwitch, Features);

        var seeded = NewlyReady.Reconcile(before.Available, null);
        Assert.Empty(NewlyReady.Reconcile(after.Available, seeded.Seen).New);
        Assert.Equal(before.Available, after.Available);
    }

    [Fact]
    public void A_quest_accepted_or_done_leaves_the_seen_set_but_one_merely_blocked_stays()
    {
        // 2 was accepted, 3 completed, 4 went Blocked (a capture read before the quest data, a weapon swapped).
        var later = Evaluate((1, QuestState.Ready), (2, QuestState.Accepted), (3, QuestState.Completed), (4, QuestState.Blocked));
        var tally = NewlyReady.Tally(Catalog, later, Features);
        var state = NewlyReady.Reconcile(tally.Available, [1, 2, 3, 4], Gone(later));

        Assert.Equal(new uint[] { 1, 4 }, state.Seen);
        Assert.True(state.SeenChanged);
        Assert.Empty(state.New);

        // So 4 is not new when it reads Ready again, but 2, abandoned and Ready again, is.
        var again = NewlyReady.Reconcile([1, 2, 4], state.Seen);
        Assert.Equal(new uint[] { 2 }, again.New);
    }

    [Fact]
    public void Without_a_gone_rule_everything_unavailable_is_pruned()
    {
        var state = NewlyReady.Reconcile([1], [1, 2, 3]);
        Assert.Equal(new uint[] { 1 }, state.Seen);
        Assert.True(state.SeenChanged);
    }

    [Fact]
    public void Gone_is_accepted_done_locked_out_or_removed()
    {
        var quest = Catalog.GetByRowId(3)!;
        Assert.True(NewlyReady.IsGone(null, null));
        Assert.True(NewlyReady.IsGone(Catalog.GetByRowId(8), null));
        Assert.True(NewlyReady.IsGone(quest, Evaluate((3, QuestState.Accepted))[3]));
        Assert.True(NewlyReady.IsGone(quest, Evaluate((3, QuestState.Completed))[3]));
        Assert.True(NewlyReady.IsGone(quest, Evaluate((3, QuestState.Foreclosed))[3]));
        Assert.True(NewlyReady.IsGone(quest, Evaluate((3, QuestState.DoneThisCycle))[3]));
        Assert.False(NewlyReady.IsGone(quest, Evaluate((3, QuestState.Blocked))[3]));
        Assert.False(NewlyReady.IsGone(quest, Evaluate((3, QuestState.Unknown))[3]));
        Assert.False(NewlyReady.IsGone(quest, Evaluate((3, QuestState.Ready))[3]));
    }

    [Fact]
    public void Marking_seen_adds_only_available_quests_and_reports_no_change_as_null()
    {
        var available = new HashSet<uint> { 1, 2, 3 };
        Assert.Equal(new uint[] { 1, 2, 3 }, NewlyReady.MarkSeen([1], [3, 2, 9], available));
        Assert.Null(NewlyReady.MarkSeen([1, 2], [2, 9], available));
    }

    [Fact]
    public void A_selected_quest_stops_being_new()
    {
        var state = NewlyReady.Reconcile([1, 2, 3], [1]);
        Assert.Equal(new uint[] { 2, 3 }, state.New);

        var seen = NewlyReady.MarkSeen(state.Seen!, [3], new HashSet<uint> { 1, 2, 3 })!;
        Assert.Equal(new uint[] { 2 }, NewlyReady.Reconcile([1, 2, 3], seen).New);
    }

    [Theory]
    [InlineData(JournalBadgeMode.NewlyReady, 3)]
    [InlineData(JournalBadgeMode.StoryAndUnlock, 12)]
    [InlineData(JournalBadgeMode.EveryReady, 214)]
    [InlineData(JournalBadgeMode.Nothing, 0)]
    public void The_badge_counts_what_the_setting_says(JournalBadgeMode mode, int expected)
    {
        Assert.Equal(expected, NewlyReady.BadgeCount(mode, newlyReady: 3, storyReady: 12, everyReady: 214));
    }

    [Theory]
    [InlineData(0, "")]
    [InlineData(-4, "")]
    [InlineData(3, "3")]
    [InlineData(99, "99")]
    [InlineData(100, "99+")]
    [InlineData(1234, "99+")]
    public void The_badge_spells_up_to_99_then_99_plus(int count, string text)
    {
        Assert.Equal(text, NewlyReady.BadgeText(count));
    }

    [Fact]
    public void The_newly_ready_scope_lists_the_badges_quests_in_journal_order()
    {
        var ctx = QueryContext.Empty with { NewlyReady = new HashSet<uint> { 4, 1, 3 } };
        var result = Run(Catalog, Today, scope: QuestScope.NewlyReady(2), ctx: ctx);

        Assert.Equal(new uint[] { 1, 3, 4 }, RowIds(result));
        Assert.Equal(ScopeKind.VirtualNewlyReady, QuestScope.NewlyReady(2).Kind);
        Assert.Empty(Run(Catalog, Today, scope: QuestScope.NewlyReady(3)).Rows);
    }

    [Fact]
    public void The_seen_set_is_stored_per_character_and_survives_a_round_trip()
    {
        var settings = new Dictionary<ulong, CharacterSettings>();
        CharacterSettingsFile.Apply(settings, [CharacterSettingChange.Seen(42, [1, 2, 3])]);
        Assert.Equal(new uint[] { 1, 2, 3 }, settings[42].SeenReady);
        Assert.False(settings[42].IsEmpty);

        var copy = CharacterSettingsFile.Copy(settings);
        copy[42].SeenReady!.Add(9);
        Assert.Equal(3, settings[42].SeenReady!.Count);

        // Forget drops it with the rest.
        CharacterSettingsFile.Apply(settings, [CharacterSettingChange.Forget(42)]);
        Assert.False(settings.ContainsKey(42));
    }

    [Fact]
    public void The_book_answers_the_seen_set_and_saves_a_change()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tsukimichi-seen-{Guid.NewGuid():N}.json");
        try
        {
            var book = new CharacterSettingsBook(path);
            Assert.Null(book.SeenReady(7));
            book.Edit(CharacterSettingChange.Seen(7, [5, 6]));
            Assert.Equal(new uint[] { 5, 6 }, book.SeenReady(7));

            var version = book.Version;
            book.Edit(CharacterSettingChange.Seen(7, [5, 6, 8]));
            Assert.True(book.Version > version);

            var reloaded = new CharacterSettingsBook(path);
            reloaded.Load();
            Assert.Equal(new uint[] { 5, 6, 8 }, reloaded.SeenReady(7));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static Func<uint, bool> Gone(IReadOnlyDictionary<uint, QuestEvaluation> states) =>
        id => NewlyReady.IsGone(Catalog.GetByRowId(id), states.GetValueOrDefault(id));
}

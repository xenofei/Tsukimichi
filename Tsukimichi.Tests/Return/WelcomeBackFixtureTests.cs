using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Return;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.GameData;
using Tsukimichi.Tests.Data;
using Tsukimichi.Tests.Storage;

namespace Tsukimichi.Tests.Return;

/// <summary>
/// "Since you were away" (P7) over the frozen catalog, dated with the shipped <c>quest_patches.json</c> (P8), and the
/// real schema-v1 character in <c>Fixtures/snapshot-v1.json</c>: a return after 200 days with a stored capture, a
/// fresh install that answered "7.2", and a login with nothing new.
/// </summary>
public sealed class WelcomeBackFixtureTests(FixtureCatalog fixture) : IClassFixture<FixtureCatalog>
{
    private static readonly Lazy<CatalogBundle> DatedBundle = new(() =>
    {
        var curated = CuratedData.Load(FixtureCatalog.CuratedDir());
        var patches = QuestPatches.Load(Path.Combine(FixtureCatalog.ShippedDataDir(), QuestPatches.FileName));
        return CatalogFixtureFile.Read(FixtureCatalog.CatalogPath(), JournalFiling.Refiled, curated, patches).Bundle;
    });

    private static readonly Lazy<CharacterSnapshot> Stored = new(LoadFixtureSnapshot);

    private static QuestCatalog Catalog => DatedBundle.Value.Catalog;

    private static CharacterSnapshot LoadFixtureSnapshot()
    {
        using var tmp = new TempDir();
        Directory.CreateDirectory(tmp.File("characters"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "snapshot-v1.json"), Path.Combine(tmp.File("characters"), "1.json"));
        return new JsonSnapshotStore(tmp.Path).Load(1) ?? throw new InvalidOperationException("fixture snapshot did not load");
    }

    private static Dictionary<uint, QuestEvaluation> Resolve(CharacterSnapshot snapshot) =>
        StateResolver.ResolveAll(Catalog, snapshot, EvalContext.Default);

    private WelcomeBackInput Input(CharacterSnapshot current, DateTime nowUtc) =>
        new(Catalog, current, Resolve(current), nowUtc)
        {
            FeatureQuestIds = fixture.Curated.FeatureQuests,
            Festivals = ServerFestivals.Of(current),
            CuratedFestivals = fixture.Curated.Festivals,
        };

    /// <summary>The quests the new-quest count must hold: live, not seasonal, added after <paramref name="since"/> under the given reading.</summary>
    private static List<QuestRecord> ExpectedNew(string since, bool series) =>
        Catalog.All.Where(q => !q.IsRemoved && q.Festival == 0 && PatchVersion.IsPatch(q.AddedIn)
            && (series
                ? PatchVersion.Compare(PatchVersion.Series(q.AddedIn), since) > 0
                : PatchVersion.Compare(q.AddedIn, since) > 0))
            .ToList();

    private static List<AcceptedQuest> Midway(CharacterSnapshot snapshot) =>
        snapshot.Accepted.Where(a => Catalog.TryGetByQuestId(a.QuestId, out var q) && !q.IsRepeatable).ToList();

    private static byte[] WithBit(byte[] bits, ushort questId)
    {
        var copy = bits.ToArray();
        copy[questId >> 3] |= (byte)(1 << (questId & 7));
        return copy;
    }

    [Fact]
    public void Stored_snapshot_200_days_old_says_what_moved_and_what_is_new()
    {
        var then = Stored.Value;
        var now = then.TakenUtc.AddDays(200).AddHours(3);
        var thenStates = Resolve(then);
        var msqThen = MsqGraph.For(Catalog).Position(thenStates);
        Assert.NotNull(msqThen);
        var msqNext = Assert.IsType<QuestRecord>(msqThen.Next);

        // Three journal quests the player was mid-way through: one completed since, one moved on a step, one dropped.
        var midway = Midway(then).Where(a => a.QuestId != msqNext.QuestId).ToList();
        Assert.True(midway.Count >= 3, $"the fixture character has {midway.Count} non-repeatable journal quests besides the MSQ");
        var (completed, moved, dropped) = (midway[0], midway[1], midway[2]);
        var movedTo = moved.Sequence == BlockerText.FinalSequence ? (byte)1 : (byte)(moved.Sequence + 1);

        var bits = WithBit(WithBit(then.CompletedBits, completed.QuestId), msqNext.QuestId);
        var accepted = then.Accepted
            .Where(a => a.QuestId != completed.QuestId && a.QuestId != dropped.QuestId && a.QuestId != msqNext.QuestId)
            .Select(a => a.QuestId == moved.QuestId ? a with { Sequence = movedTo } : a)
            .ToList();
        var job = then.JobLevels.Keys.Min();
        var levels = then.JobLevels.ToDictionary(kv => kv.Key, kv => kv.Value);
        levels[job] = (short)(levels[job] + 2);
        var current = then with { TakenUtc = now, CompletedBits = bits, Accepted = accepted, JobLevels = levels };

        var summary = WelcomeBack.Compute(Input(current, now) with { Previous = then, PreviousStates = thenStates, RecordedPatch = "7.2" });

        Assert.True(summary.HasPrevious);
        Assert.Equal(then.TakenUtc, summary.PreviousTakenUtc);
        Assert.Equal(200, summary.DaysAway);

        // Mid-way then, and where each stands now.
        Assert.Equal(Midway(then).Count, summary.Midway.Count);
        Assert.Equal(MidwayOutcome.Completed, Assert.Single(summary.Midway, m => m.Quest.QuestId == completed.QuestId).Outcome);
        var movedRow = Assert.Single(summary.Midway, m => m.Quest.QuestId == moved.QuestId);
        Assert.Equal(MidwayOutcome.Moved, movedRow.Outcome);
        Assert.Equal(moved.Sequence, movedRow.ThenSequence);
        Assert.Equal(movedTo, movedRow.NowSequence);
        Assert.NotEqual(movedRow.ThenStep, movedRow.NowStep);
        var droppedRow = Assert.Single(summary.Midway, m => m.Quest.QuestId == dropped.QuestId);
        Assert.Equal(MidwayOutcome.Dropped, droppedRow.Outcome);
        Assert.Equal(string.Empty, droppedRow.NowStep);
        Assert.All(summary.Midway.Where(m => m.Quest.QuestId != msqNext.QuestId && m.Quest.QuestId != completed.QuestId && m.Quest.QuestId != moved.QuestId && m.Quest.QuestId != dropped.QuestId),
            m => Assert.Equal(MidwayOutcome.SameStep, m.Outcome));
        Assert.True(summary.Midway.Select(m => m.Quest.Journal.SortKey).SequenceEqual(summary.Midway.Select(m => m.Quest.Journal.SortKey).Order()), "journal order");

        // The main scenario moved by the one quest completed since, route-aware position both times.
        Assert.Same(msqNext, summary.MsqThen!.Next);
        Assert.NotNull(summary.MsqNow);
        Assert.NotSame(msqNext, summary.MsqNow.Next);
        // The next quest, plus the completed journal quest when that one is main scenario too (a side route's).
        var completedQuest = Catalog.GetByQuestId(completed.QuestId)!;
        Assert.Equal(1 + (FeaturePresets.IsMainScenario(completedQuest) ? 1 : 0), summary.MsqDoneSince);
        Assert.True(summary.MsqMoved);

        // New since the recorded patch: exact, so 7.21 and 7.25 count and 7.2 itself does not.
        Assert.Equal(SincePatchSource.Recorded, summary.SinceSource);
        Assert.Equal("7.2", summary.SincePatch);
        var expected = ExpectedNew("7.2", series: false);
        Assert.NotEmpty(expected);
        Assert.Equal(expected.Count, summary.NewQuestTotal);
        Assert.Equal(expected.Count(FeaturePresets.IsMainScenario), summary.NewMainScenario);
        Assert.Equal(expected.Count(q => !FeaturePresets.IsMainScenario(q) && fixture.Curated.FeatureQuests.Contains(q.RowId)), summary.NewUnlocks);
        Assert.Equal(summary.NewQuestTotal, summary.NewMainScenario + summary.NewUnlocks + summary.NewSide);
        Assert.True(summary.NewMainScenario > 0 && summary.NewUnlocks > 0 && summary.NewSide > 0, "every kind is represented after 7.2");
        Assert.Equal(PatchIndex.For(Catalog).NewestSeries, summary.NewQuests[0].Series);
        Assert.True(summary.NewQuests.Select(s => s.Series).SequenceEqual(summary.NewQuests.Select(s => s.Series).OrderBy(s => s, PatchVersion.NewestFirst)), "newest series first");
        Assert.Contains(summary.NewQuests, s => s.Series == "7.2"); // 7.21 and 7.25 are after 7.2
        Assert.All(summary.NewQuests, s => Assert.True(s.Total > 0));

        // Events running now come from the current capture's festivals; the level that changed is listed.
        Assert.All(summary.Events, e => Assert.Contains(e.FestivalId, current.ActiveFestivals));
        var change = Assert.Single(summary.JobChanges);
        Assert.Equal(new JobLevelChange(job, then.JobLevels[job], (short)(then.JobLevels[job] + 2)), change);

        Assert.True(summary.HasChanges);
        Assert.False(summary.IsEmpty);
    }

    [Fact]
    public void Fresh_install_with_an_answer_of_7_2_counts_what_came_after_7_2x()
    {
        var current = Stored.Value;
        var now = current.TakenUtc.AddMinutes(5);

        var summary = WelcomeBack.Compute(Input(current, now) with { LastPlayedPatch = "7.2" });

        Assert.False(summary.HasPrevious);
        Assert.Null(summary.DaysAway);
        Assert.Null(summary.MsqThen);
        Assert.NotNull(summary.MsqNow);
        Assert.Empty(summary.JobChanges);

        // The answer is a series: 7.2x was seen, so the first new series is 7.3.
        Assert.Equal(SincePatchSource.Answer, summary.SinceSource);
        Assert.Equal("7.2", summary.SincePatch);
        var expected = ExpectedNew("7.2", series: true);
        Assert.Equal(expected.Count, summary.NewQuestTotal);
        Assert.DoesNotContain(summary.NewQuests, s => PatchVersion.Compare(s.Series, "7.2") <= 0);
        Assert.Contains(summary.NewQuests, s => s.Series == "7.3");
        Assert.True(summary.NewMainScenario > 0, "7.3 and later added main scenario quests");
        Assert.Equal(expected.Count(FeaturePresets.IsMainScenario), summary.NewMainScenario);

        // No "then": the journal now is what the player was doing.
        Assert.Equal(Midway(current).Count, summary.Midway.Count);
        Assert.All(summary.Midway, m => Assert.Equal(MidwayOutcome.InJournal, m.Outcome));
        Assert.All(summary.Midway, m => Assert.Equal(string.Empty, m.ThenStep));
        Assert.True(summary.HasChanges);

        // "I'm new" measures from nothing.
        var newcomer = WelcomeBack.Compute(Input(current, now) with { LastPlayedPatch = WelcomeBackState.NewPlayer });
        Assert.Equal(SincePatchSource.None, newcomer.SinceSource);
        Assert.Empty(newcomer.NewQuests);
    }

    [Fact]
    public void Nothing_new_when_the_capture_is_from_today_on_the_newest_patch()
    {
        var snapshot = Stored.Value;
        var now = snapshot.TakenUtc.AddHours(1);
        var newest = PatchIndex.For(Catalog).Newest;
        Assert.NotEmpty(newest);

        var summary = WelcomeBack.Compute(Input(snapshot, now) with { Previous = snapshot, RecordedPatch = newest });

        Assert.Equal(0, summary.DaysAway);
        Assert.Empty(summary.NewQuests);
        Assert.Equal(0, summary.NewQuestTotal);
        Assert.Empty(summary.JobChanges);
        Assert.False(summary.MsqMoved);
        Assert.Equal(0, summary.MsqDoneSince);
        Assert.All(summary.Midway, m => Assert.Equal(MidwayOutcome.SameStep, m.Outcome));
        Assert.False(summary.HasChanges);

        // With an empty journal and no event running there is nothing at all to say.
        var idle = snapshot with { Accepted = [], ActiveFestivals = [], ActiveFestivalPhases = [] };
        var quiet = WelcomeBack.Compute(Input(idle, now) with { Previous = idle, RecordedPatch = newest });
        Assert.Empty(quiet.Midway);
        Assert.Empty(quiet.Events);
        Assert.True(quiet.IsEmpty);
    }

    [Fact]
    public void A_capture_without_a_recorded_patch_is_dated_by_what_it_had_done()
    {
        var snapshot = Stored.Value;
        var inferred = WelcomeBack.InferPatch(Catalog, snapshot);

        Assert.True(PatchVersion.IsPatch(inferred), $"inferred \"{inferred}\"");
        Assert.True(PatchVersion.Compare(inferred, PatchIndex.For(Catalog).Newest) <= 0);
        Assert.Contains(Catalog.All, q => q.AddedIn == inferred && !q.IsRemoved && (snapshot.IsCompleted(q.QuestId) || snapshot.Accepted.Any(a => a.QuestId == q.QuestId)));
        Assert.DoesNotContain(Catalog.All, q => !q.IsRemoved && PatchVersion.Compare(q.AddedIn, inferred) > 0 && snapshot.IsCompleted(q.QuestId));

        var summary = WelcomeBack.Compute(Input(snapshot, snapshot.TakenUtc.AddDays(30)) with { Previous = snapshot, LastPlayedPatch = "3.0" });
        Assert.Equal(SincePatchSource.Inferred, summary.SinceSource);
        Assert.Equal(inferred, summary.SincePatch);
        Assert.Equal(ExpectedNew(inferred, series: false).Count, summary.NewQuestTotal);
    }

    [Fact]
    public void Opened_by_hand_after_the_first_evaluation_it_counts_from_the_patch_recorded_before_this_login()
    {
        // At login the sidecar says the old capture was taken on 7.2; the first live evaluation then saves this
        // session's newest patch over it (and the player may since have answered or muted the card).
        var then = Stored.Value;
        var now = then.TakenUtc.AddDays(60);
        var newest = PatchIndex.For(Catalog).Newest;
        var atLogin = new WelcomeBackState { SeenPatch = "7.2", LastPlayedPatch = "7.0" };
        var saved = atLogin with { SeenPatch = newest, ShownForUtc = then.TakenUtc, Quiet = true };

        var state = saved.MeasuredFrom(atLogin);
        Assert.Equal("7.2", state.SeenPatch);
        Assert.True(state.Quiet);
        Assert.Equal(then.TakenUtc, state.ShownForUtc);
        Assert.Same(saved, saved.MeasuredFrom(null));

        var input = Input(then, now) with { Previous = then, LastPlayedPatch = state.LastPlayedPatch };
        var reopened = WelcomeBack.Compute(input with { RecordedPatch = state.SeenPatch });
        Assert.Equal(SincePatchSource.Recorded, reopened.SinceSource);
        Assert.Equal(ExpectedNew("7.2", series: false).Count, reopened.NewQuestTotal);
        Assert.True(reopened.NewQuestTotal > 0);

        // What the card said before the fix: measured from the patch saved at this login, nothing is new.
        Assert.Equal(0, WelcomeBack.Compute(input with { RecordedPatch = saved.SeenPatch }).NewQuestTotal);
    }

    [Fact]
    public void Newness_compares_patches_or_series()
    {
        Assert.True(WelcomeBack.IsNewSince("7.21", "7.2", sinceIsSeries: false));
        Assert.False(WelcomeBack.IsNewSince("7.21", "7.2", sinceIsSeries: true));
        Assert.True(WelcomeBack.IsNewSince("7.3", "7.25", sinceIsSeries: true));
        Assert.False(WelcomeBack.IsNewSince("7.2", "7.2", sinceIsSeries: false));
        Assert.False(WelcomeBack.IsNewSince(string.Empty, "7.2", sinceIsSeries: false));
        Assert.False(WelcomeBack.IsNewSince("7.3", string.Empty, sinceIsSeries: true));
    }
}

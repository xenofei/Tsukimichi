using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

public sealed class PollerMemoryTests
{
    private static readonly DateTime T0 = new(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc);
    private static readonly IReadOnlyDictionary<uint, QuestEvaluation> NoStates = new Dictionary<uint, QuestEvaluation>();

    private static PollerMemory Committed(object catalog, ulong contentId = 1)
    {
        var memory = new PollerMemory(TimeSpan.FromSeconds(10));
        memory.Commit(Fixture.Snapshot(Fixture.A) with { ContentId = contentId }, NoStates, catalog);
        memory.SetAcceptedSince(new Dictionary<ushort, DateTime> { [QuestRecord.ToQuestId(Fixture.B)] = T0 }, dirty: false);
        memory.Saves.MarkSaved(T0);
        return memory;
    }

    [Fact]
    public void Commit_marks_the_snapshot_dirty_and_remembers_the_catalog()
    {
        var catalog = new object();
        var memory = new PollerMemory(TimeSpan.FromSeconds(10));
        Assert.True(memory.IsEmpty);

        memory.Commit(Fixture.Snapshot(Fixture.A), NoStates, catalog);

        Assert.False(memory.IsEmpty);
        Assert.True(memory.Saves.Pending);
        Assert.Same(catalog, memory.Catalog);
        Assert.False(memory.IsStaleFor(catalog));
    }

    [Fact]
    public void Catalog_swap_makes_the_next_poll_a_first_pass()
    {
        // A catalog retry hands the poller a new bundle instance; the committed evaluations belong to the old one, so
        // diffing the next capture against them would be an empty no-op that never re-resolves anything.
        var memory = Committed(new object());
        var retried = new object();

        Assert.True(memory.IsStaleFor(retried));

        memory.Reset();

        Assert.True(memory.IsEmpty);
        Assert.Null(memory.States);
        Assert.Null(memory.Catalog);
        Assert.False(memory.IsStaleFor(retried));
    }

    [Fact]
    public void Delete_all_data_forgets_the_character()
    {
        var memory = Committed(new object());
        memory.AcceptedSinceDirty = true;

        memory.OnDataDeleted();

        Assert.True(memory.IsEmpty);
        Assert.Null(memory.States);
        Assert.Empty(memory.AcceptedSince);
        Assert.False(memory.AcceptedSinceDirty);
    }

    [Fact]
    public void Forgetting_the_live_character_marks_both_files_dirty()
    {
        var memory = Committed(new object(), contentId: 42);
        Assert.False(memory.Saves.Pending);
        Assert.False(memory.AcceptedSinceDirty);

        memory.OnCharacterForgotten(42);

        // Both, never only the sidecar: the next flush writes the snapshot and the sidecar together.
        Assert.True(memory.Saves.Pending);
        Assert.True(memory.AcceptedSinceDirty);
    }

    [Fact]
    public void Forgetting_another_character_changes_nothing()
    {
        var memory = Committed(new object(), contentId: 42);

        memory.OnCharacterForgotten(7);

        Assert.False(memory.Saves.Pending);
        Assert.False(memory.AcceptedSinceDirty);

        var empty = new PollerMemory(TimeSpan.FromSeconds(10));
        empty.OnCharacterForgotten(42);
        Assert.False(empty.Saves.Pending);
        Assert.False(empty.AcceptedSinceDirty);
    }
}

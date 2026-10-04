using Tsukimichi.Core.Companions;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Companions;

/// <summary>
/// The per-job item levels the reader keeps between captures (1.19.0 review): a skipped read never hands one
/// character's gearsets to another, and an empty table reads as not judged.
/// </summary>
public class JobItemLevelMemoryTests
{
    private const ulong Main = 1001;
    private const ulong Alt = 1002;
    private const byte Dragoon = 22;
    private const byte Warrior = 21;

    [Fact]
    public void A_skipped_read_keeps_the_table_only_for_the_character_it_was_read_for()
    {
        var memory = new JobItemLevelMemory();
        var main = memory.Read(Main, new Dictionary<byte, ushort> { [Warrior] = 692, [Dragoon] = 677 });

        Assert.Same(main, memory.Skipped(Main));

        // The alt logs in while the hooks are paused: nothing of the main's gearsets comes with it.
        var alt = memory.Skipped(Alt);
        Assert.Empty(alt);
        Assert.Same(JobItemLevelMemory.Unknown, alt);

        // Nor once the alt's own read lands and the main comes back before its next one.
        memory.Read(Alt, new Dictionary<byte, ushort> { [Dragoon] = 600 });
        Assert.Empty(memory.Skipped(Main));
        Assert.Empty(new JobItemLevelMemory().Skipped(0));
    }

    [Fact]
    public void An_unchanged_read_keeps_its_instance_and_a_changed_one_replaces_it()
    {
        var memory = new JobItemLevelMemory();
        var first = memory.Read(Main, new Dictionary<byte, ushort> { [Warrior] = 692 });

        Assert.Same(first, memory.Read(Main, new Dictionary<byte, ushort> { [Warrior] = 692 }));
        var raised = memory.Read(Main, new Dictionary<byte, ushort> { [Warrior] = 700 });
        Assert.NotSame(first, raised);
        Assert.Equal(700, raised[Warrior]);

        // The same levels read for another character are that character's, not a carry-over.
        Assert.Same(raised, memory.Skipped(Main));
        memory.Read(Alt, new Dictionary<byte, ushort> { [Warrior] = 700 });
        Assert.Empty(memory.Skipped(Main));
    }

    [Fact]
    public void The_wall_reads_an_unknown_table_as_not_judged_never_as_zero()
    {
        var alt = Fixture.Snapshot() with { CurrentJob = Dragoon, ItemLevel = 0, JobItemLevels = new JobItemLevelMemory().Skipped(Alt) };

        Assert.Null(ItemLevelWall.For(690, alt, ItemLevelRule.PerJob));
        Assert.Null(ItemLevelWall.For(690, alt, ItemLevelRule.Shared));
    }
}

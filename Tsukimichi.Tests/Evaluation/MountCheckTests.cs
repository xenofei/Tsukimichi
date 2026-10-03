using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Unique;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The mounts a quest needs owned (1.11.0, C2): the sheet's mount and the mount-collection gates, judged from the owned
/// mounts the capture saved. Before, both read a silent Ready: the sheet's mount counted as met when nobody said, and
/// the collections were never seen, so the Firebird quest read Ready (and Moonlit's Get now) from level 1.
/// </summary>
public sealed class MountCheckTests
{
    private const uint Firebird = 67086;
    private static readonly uint[] Lanners = [75, 76, 77, 78, 90, 98, 104];

    private static readonly EvalContext Names = new()
    {
        MountName = id => id switch { 1 => "company chocobo", 76 => "rose lanner", 90 => "dark lanner", _ => string.Empty },
    };

    private static CharacterSnapshot Owning(uint[] owned, uint[] missing) =>
        Snapshot() with { Collectibles = new Dictionary<string, CollectibleSet> { ["Mount"] = new() { Owned = owned, Missing = missing } } };

    private static QuestCatalog Catalog(QuestRecord quest) =>
        QuestCatalog.Build([quest], null, new Dictionary<uint, QuestGate> { [quest.RowId] = new("all seven Heavensward Lanner mounts", [], null, Lanners) });

    [Fact]
    public void A_collection_gate_is_not_checked_until_the_capture_read_every_mount()
    {
        var quest = Quest(Firebird);
        var catalog = Catalog(quest);

        // A capture from before 1.11.0: no mount read. Not checked, never Ready.
        var unread = StateResolver.Resolve(quest, Snapshot(), catalog, Names);
        Assert.Equal(QuestState.Unknown, unread.State);
        var requirement = Assert.IsType<MountRequirement>(unread.NextStep!.Req);
        Assert.Null(requirement.HasMount);
        Assert.Equal(Lanners, requirement.Mounts);
        Assert.Equal("needs all seven Heavensward Lanner mounts, not checked", unread.NextStep.Detail);

        // Six read, one not: still not checked.
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, Owning(Lanners[..6], []), catalog, Names).State);
    }

    [Fact]
    public void A_collection_gate_blocks_until_every_mount_is_owned()
    {
        var quest = Quest(Firebird);
        var catalog = Catalog(quest);

        var short2 = StateResolver.Resolve(quest, Owning([75, 77, 78, 98, 104], [76, 90]), catalog, Names);
        Assert.Equal(QuestState.Blocked, short2.State);
        Assert.Equal("needs all seven Heavensward Lanner mounts, you have 5 of 7 (missing: rose lanner and dark lanner)", short2.NextStep!.Detail);
        Assert.Equal([76u, 90u], ((MountRequirement)short2.NextStep.Req).Missing);

        var short1 = RequirementEvaluator.Evaluate(quest, Owning([.. Lanners.Where(id => id != 104)], [104]), catalog, Names);
        Assert.Equal("needs all seven Heavensward Lanner mounts, you have 6 of 7 (missing: mount 104)", Only(short1, RequirementKind.Mount).Detail);

        var all = StateResolver.Resolve(quest, Owning(Lanners, []), catalog, Names);
        Assert.Equal(QuestState.Ready, all.State);
        Assert.Equal("has all seven Heavensward Lanner mounts", Only(all.Requirements, RequirementKind.Mount).Detail);
    }

    [Fact]
    public void The_sheet_mount_is_judged_from_the_capture()
    {
        var quest = Quest(66698) with { MountRequired = 1 };
        var catalog = QuestCatalog.Build([quest]);

        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, Snapshot(), catalog, Names).State);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, Owning([], [1]), catalog, Names).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Owning([1], []), catalog, Names).State);
        Assert.Equal([1u], catalog.MountWatch);
    }

    [Fact]
    public void A_house_nobody_read_is_not_checked_rather_than_Ready()
    {
        var quest = Quest(67096) with { HouseRequired = true };
        var evaluation = StateResolver.Resolve(quest, Snapshot(), QuestCatalog.Build([quest]), EvalContext.Default);

        Assert.Equal(QuestState.Unknown, evaluation.State);
        Assert.IsType<HouseRequirement>(evaluation.NextStep!.Req);
    }

    [Fact]
    public void An_unread_mount_does_not_hide_a_real_block()
    {
        // Level too low and the mount unread: Blocked by the level, the mount listed as not checked.
        var quest = Quest(66698) with { MountRequired = 1, Level = 60 };
        var evaluation = StateResolver.Resolve(quest, Snapshot(), QuestCatalog.Build([quest]), Names);

        Assert.Equal(QuestState.Blocked, evaluation.State);
        Assert.Equal(RequirementKind.Level, evaluation.NextStep!.Req.Kind);
    }

    [Fact]
    public void The_catalog_watches_the_sheet_mounts_and_the_gate_mounts()
    {
        var chocobo = Quest(66698) with { MountRequired = 1 };
        var magitek = Quest(65700) with { MountRequired = 6 };
        var catalog = QuestCatalog.Build(
            [chocobo, magitek, Quest(Firebird)],
            null,
            new Dictionary<uint, QuestGate> { [Firebird] = new("all seven Heavensward Lanner mounts", [], null, Lanners) });

        Assert.Equal([1u, 6u, .. Lanners], catalog.MountWatch);
    }

    [Fact]
    public void The_capture_targets_gain_the_watched_mounts_once()
    {
        IReadOnlyList<CollectibleTarget> targets =
        [
            new(RewardKind.Emote, 3, 0),
            new(RewardKind.Mount, 105, 17008),
            new(RewardKind.Minion, 9, 0),
        ];

        var merged = Collectibles.WithMounts(targets, [105, 75, 1]);
        Assert.Equal(5, merged.Count);
        Assert.Contains(new CollectibleTarget(RewardKind.Mount, 75, 0), merged);
        Assert.Contains(new CollectibleTarget(RewardKind.Mount, 1, 0), merged);
        Assert.Single(merged, t => t.Kind == RewardKind.Mount && t.RewardId == 105);
        Assert.Equal(merged.OrderBy(t => t.Kind).ThenBy(t => t.RewardId), merged);

        // Nothing to add: the same list, so the capture's reuse check keeps holding.
        Assert.Same(targets, Collectibles.WithMounts(targets, [105]));
        Assert.Same(targets, Collectibles.WithMounts(targets, []));
    }

    [Fact]
    public void The_capture_answer_reads_owned_missing_or_unread()
    {
        var s = Owning([75], [76]);
        Assert.True(MountCheck.Owns(s, 75));
        Assert.False(MountCheck.Owns(s, 76));
        Assert.Null(MountCheck.Owns(s, 77));
        Assert.Null(MountCheck.Owns(Snapshot(), 75));
    }
}

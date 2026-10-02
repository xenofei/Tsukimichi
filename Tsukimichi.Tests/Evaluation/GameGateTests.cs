using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// A game gate (<c>curated/game_gates.json</c>, <see cref="QuestCatalog.GameGateOf"/>): something the game checks that
/// Tsukimichi cannot read. It is never judged, so it never blocks; it keeps the quest from reading Ready; and its
/// "after" quests are prerequisites like any other.
/// </summary>
public class GameGateTests
{
    private const string Nexus = "a relic weapon nexus equipped";

    private static readonly BlockerNames Names = new()
    {
        JobAbbreviation = id => id switch { Gladiator => "GLA", Conjurer => "CNJ", _ => string.Empty },
    };

    /// <summary>A side quest (journal section 3): the shared factory files quests under the main scenario.</summary>
    private static QuestRecord Side(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Sidequests", 1, "Category", 1, "Genre", (int)rowId) };

    /// <summary>A side quest gated by <see cref="Nexus"/>, which cannot be passed before <see cref="A"/>.</summary>
    private static (QuestRecord Quest, QuestCatalog Catalog) Gated(Func<QuestRecord, QuestRecord>? shape = null)
    {
        var quest = Side(Target, "His Dark Materia");
        quest = shape?.Invoke(quest) ?? quest;
        var catalog = QuestCatalog.Build([quest, Side(A, "Mmmmmm, Soulglazed Relics")], null, new Dictionary<uint, QuestGate> { [Target] = new(Nexus, [A]) });
        return (quest, catalog);
    }

    [Fact]
    public void The_after_quests_are_prerequisites_and_block_until_done()
    {
        var (quest, catalog) = Gated();

        Assert.Equal([A], catalog.PrerequisitesOf(quest).QuestIds);
        Assert.Equal([A], catalog.ExtraPrerequisitesOf(Target));
        Assert.Equal(Nexus, catalog.GameGateOf(Target)!.Gate);
        Assert.Null(catalog.GameGateOf(A));

        var blocked = StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, blocked.State);
        Assert.Equal(RequirementKind.PreviousQuests, blocked.NextStep!.Req.Kind);
        Assert.Equal("Blocked · after: Mmmmmm, Soulglazed Relics", BlockerText.StatusText(blocked, quest, Names with { Catalog = catalog }));
    }

    [Fact]
    public void Once_nothing_else_is_missing_the_quest_is_Not_checked_rather_than_Ready()
    {
        var (quest, catalog) = Gated();

        var result = StateResolver.Resolve(quest, Snapshot(A), catalog, EvalContext.Default);

        Assert.Equal(QuestState.Unknown, result.State);
        var gate = Assert.IsType<GameGateRequirement>(result.NextStep!.Req);
        Assert.Equal(Nexus, gate.Gate);
        Assert.False(result.NextStep.Met);
        Assert.Equal("needs a relic weapon nexus equipped, not checked", result.NextStep.Detail);
        Assert.Equal(StateNames.Name(QuestState.Unknown, quest) + " · " + Nexus, BlockerText.StatusText(result, quest, Names with { Catalog = catalog }));
        Assert.Equal("Not checked: " + Nexus, BlockerText.For(result, quest, Names with { Catalog = catalog }));
        Assert.Equal("needs a relic weapon nexus equipped, not checked", RequirementDetail.Text(result.NextStep, Names with { Catalog = catalog }));
        Assert.Equal(new IpcBlocker(IpcBlockerKinds.Unchecked, 0, 0, 0), IpcBlocker.Of(result, catalog, null));
        Assert.Equal("not checked", QuestDiagnostic.Verdict(result.NextStep));
    }

    [Fact]
    public void A_quest_ready_on_another_job_is_Not_checked_too()
    {
        var (quest, catalog) = Gated(q => q with { ClassJobRequired = Conjurer, Level = 10 });
        var snapshot = Snapshot(A) with { JobLevels = Levels((Gladiator, 50), (Conjurer, 20)) };

        var result = StateResolver.Resolve(quest, snapshot, catalog, EvalContext.Default);

        Assert.Equal(QuestState.Unknown, result.State);
        Assert.Null(result.ReadyOnJob);
        Assert.Equal(RequirementKind.GameGate, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Another_unmet_gate_still_reads_Blocked_by_that_gate()
    {
        var (quest, catalog) = Gated(q => q with { Level = 90 });

        var result = StateResolver.Resolve(quest, Snapshot(A), catalog, EvalContext.Default);

        Assert.Equal(QuestState.Blocked, result.State);
        Assert.Equal(RequirementKind.Level, result.NextStep!.Req.Kind);
    }

    [Fact]
    public void Completed_and_accepted_quests_ignore_the_gate()
    {
        var (quest, catalog) = Gated();

        Assert.Equal(QuestState.Completed, StateResolver.Resolve(quest, Snapshot(Target), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Accepted, StateResolver.Resolve(quest, Snapshot(A) with { Accepted = [Accepted(Target)] }, catalog, EvalContext.Default).State);
    }

    private const uint CurtanaNexus = 8649;
    private const uint HolyShieldNexus = 8658;
    private const uint ThyrusNexus = 8654;
    private const uint ThyrusNovus = 7868;

    private static readonly EvalContext Named = EvalContext.Default with
    {
        ItemName = id => id switch { CurtanaNexus => "Curtana Nexus", HolyShieldNexus => "Holy Shield Nexus", ThyrusNexus => "Thyrus Nexus", ThyrusNovus => "Thyrus Novus", _ => string.Empty },
    };

    /// <summary>The nexus gate with its weapons (a paladin's pair, a conjurer's staff) and the novus listed elsewhere, so it is watched.</summary>
    private static (QuestRecord Quest, QuestCatalog Catalog) Gear(GateHold hold = GateHold.Equipped)
    {
        var quest = Side(Target, "His Dark Materia");
        var other = Side(B, "Mmmmmm, Soulglazed Relics");
        var catalog = QuestCatalog.Build(
            [quest, other],
            null,
            new Dictionary<uint, QuestGate>
            {
                [Target] = new(Nexus, [], new GateItems(hold, [[CurtanaNexus, HolyShieldNexus], [ThyrusNexus]])),
                [B] = new("a relic weapon novus equipped", [], new GateItems(GateHold.Equipped, [[ThyrusNovus]])),
            });
        return (quest, catalog);
    }

    private static CharacterSnapshot Wearing(QuestCatalog catalog, uint[] equipped, params uint[] held) =>
        Snapshot() with { GateItems = new GateItemCapture(catalog.GateItemFingerprint, equipped, [.. equipped.Concat(held).Order()]) };

    [Fact]
    public void A_gear_gate_with_the_weapon_equipped_is_met_and_the_quest_Ready()
    {
        var (quest, catalog) = Gear();
        Assert.Equal([ThyrusNovus, CurtanaNexus, ThyrusNexus, HolyShieldNexus], catalog.GateItemWatch.Order());

        var result = StateResolver.Resolve(quest, Wearing(catalog, [ThyrusNexus]), catalog, Named);

        Assert.Equal(QuestState.Ready, result.State);
        var gate = result.Requirements.Single(r => r.Req.Kind == RequirementKind.GameGate);
        Assert.True(gate.Met);
        Assert.Equal("Thyrus Nexus equipped", gate.Detail);
        Assert.Equal("met", QuestDiagnostic.Verdict(gate));
        Assert.Equal(GateHold.Equipped, ((GameGateRequirement)gate.Req).Checked);
    }

    [Fact]
    public void A_gear_gate_without_the_weapon_blocks_and_says_what_is_equipped()
    {
        var (quest, catalog) = Gear();

        var novus = StateResolver.Resolve(quest, Wearing(catalog, [ThyrusNovus]), catalog, Named);
        Assert.Equal(QuestState.Blocked, novus.State);
        Assert.Equal("needs a relic weapon nexus equipped, you have Thyrus Novus equipped", novus.NextStep!.Detail);
        Assert.Equal("Blocked · needs a relic weapon nexus equipped, you have Thyrus Novus equipped", BlockerText.StatusText(novus, quest, Names with { Catalog = catalog }));
        Assert.Equal("needs a relic weapon nexus equipped, you have Thyrus Novus equipped", RequirementDetail.Text(novus.NextStep, Names with { Catalog = catalog }));
        Assert.Equal("unmet", QuestDiagnostic.Verdict(novus.NextStep));
        Assert.Equal(IpcBlockerKinds.Other, IpcBlocker.Of(novus, catalog, null).Kind);

        var none = StateResolver.Resolve(quest, Wearing(catalog, []), catalog, Named);
        Assert.Equal(QuestState.Blocked, none.State);
        Assert.Equal("needs a relic weapon nexus equipped, none equipped", none.NextStep!.Detail);

        // Names the plugin did not supply print as item ids.
        var unnamed = StateResolver.Resolve(quest, Wearing(catalog, [ThyrusNovus]), catalog, EvalContext.Default);
        Assert.Equal("needs a relic weapon nexus equipped, you have item 7868 equipped", unnamed.NextStep!.Detail);
    }

    [Fact]
    public void A_carried_weapon_is_named_to_equip_and_a_paladin_needs_both_pieces()
    {
        var (quest, catalog) = Gear();

        var carried = StateResolver.Resolve(quest, Wearing(catalog, [ThyrusNovus], ThyrusNexus), catalog, Named);
        Assert.Equal(QuestState.Blocked, carried.State);
        Assert.Equal("needs a relic weapon nexus equipped, equip Thyrus Nexus", carried.NextStep!.Detail);
        Assert.Equal([ThyrusNexus], ((GameGateRequirement)carried.NextStep.Req).Matching);

        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, Wearing(catalog, [CurtanaNexus]), catalog, Named).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Wearing(catalog, [CurtanaNexus, HolyShieldNexus]), catalog, Named).State);
    }

    [Fact]
    public void A_held_gate_counts_the_Armoury_Chest_and_the_bags()
    {
        var (quest, catalog) = Gear(GateHold.Held);

        var carried = StateResolver.Resolve(quest, Wearing(catalog, [ThyrusNovus], ThyrusNexus), catalog, Named);
        Assert.Equal(QuestState.Ready, carried.State);
        Assert.Equal("Thyrus Nexus in your possession", carried.Requirements.Single(r => r.Req.Kind == RequirementKind.GameGate).Detail);

        var none = StateResolver.Resolve(quest, Wearing(catalog, [ThyrusNovus]), catalog, Named);
        Assert.Equal(QuestState.Blocked, none.State);
        Assert.Equal("needs a relic weapon nexus equipped, you have none", none.NextStep!.Detail);
    }

    [Fact]
    public void A_gear_gate_stays_Not_checked_without_a_capture_or_with_one_against_another_list()
    {
        var (quest, catalog) = Gear();

        // No capture: a stored character from before the gear was read, or the hooks paused.
        var stored = StateResolver.Resolve(quest, Snapshot(), catalog, Named);
        Assert.Equal(QuestState.Unknown, stored.State);
        Assert.Equal("needs a relic weapon nexus equipped, not checked", stored.NextStep!.Detail);
        Assert.Equal(StateNames.Name(QuestState.Unknown, quest) + " · " + Nexus, BlockerText.StatusText(stored, quest, Names with { Catalog = catalog }));
        Assert.Equal(new IpcBlocker(IpcBlockerKinds.Unchecked, 0, 0, 0), IpcBlocker.Of(stored, catalog, null));

        // A capture made against an older weapon list never looked for this gate's weapons: not judged.
        var older = Snapshot() with { GateItems = new GateItemCapture(catalog.GateItemFingerprint + 1, [], []) };
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, older, catalog, Named).State);

        // A gate without weapons is never judged, whatever the capture.
        var (plain, plainCatalog) = Gated();
        var captured = Snapshot(A) with { GateItems = new GateItemCapture(plainCatalog.GateItemFingerprint, [ThyrusNexus], [ThyrusNexus]) };
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(plain, captured, plainCatalog, Named).State);
        Assert.Empty(plainCatalog.GateItemWatch);
        Assert.Equal(0u, plainCatalog.GateItemFingerprint);
    }

    [Fact]
    public void The_fingerprint_follows_the_list()
    {
        Assert.Equal(0u, GateItemCapture.Fingerprint([]));
        Assert.Equal(GateItemCapture.Fingerprint([1, 2]), GateItemCapture.Fingerprint([1, 2]));
        Assert.NotEqual(GateItemCapture.Fingerprint([1, 2]), GateItemCapture.Fingerprint([1, 3]));
        Assert.NotEqual(GateItemCapture.Fingerprint([1, 2]), GateItemCapture.Fingerprint([1, 2, 3]));
    }

    [Fact]
    public void A_gate_on_a_row_the_catalog_lacks_or_with_unknown_after_ids_adds_nothing_unknown()
    {
        var quest = Quest(Target);
        var catalog = QuestCatalog.Build([quest], null, new Dictionary<uint, QuestGate> { [Target] = new(Nexus, [A]), [B] = new(Nexus, []) });

        Assert.Empty(catalog.PrerequisitesOf(quest).QuestIds);
        Assert.NotNull(catalog.GameGateOf(Target));
        Assert.Null(catalog.GameGateOf(B));
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default).State);
    }
}

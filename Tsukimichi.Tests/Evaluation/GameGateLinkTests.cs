using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Ipc;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using static Tsukimichi.Tests.Evaluation.Fixture;

namespace Tsukimichi.Tests.Evaluation;

/// <summary>
/// The 1.19 game gates (feature plan v7 C3): an unlock-link gate judged from the links a capture read, a gate met by a
/// quest the game gives only once it is passed (<see cref="QuestGate.MetBy"/>), the sheet's accept conditions a gate
/// stands for, and "I've done this" for a gate Tsukimichi cannot check.
/// </summary>
public class GameGateLinkTests
{
    private const string Occult = "the Occult Record entries unlocked";
    private const string Floor = "floor 50 of the Palace of the Dead cleared";

    private static QuestRecord Side(uint rowId, string name) =>
        Quest(rowId, name) with { Journal = new JournalRef(3, "Sidequests", 1, "Category", 1, "Genre", (int)rowId) };

    private static (QuestRecord Quest, QuestCatalog Catalog) Gated(QuestGate gate, Func<QuestRecord, QuestRecord>? shape = null)
    {
        var quest = Side(Target, "A Common Thread");
        quest = shape?.Invoke(quest) ?? quest;
        var catalog = QuestCatalog.Build([quest, Side(A, "What Lies Beneath"), Side(B, "The House That Death Built")], null, new Dictionary<uint, QuestGate> { [Target] = gate });
        return (quest, catalog);
    }

    private static QuestGate Links(params uint[] links) => new(Occult, []) { UnlockLinks = links };

    private static CharacterSnapshot Read(CharacterSnapshot s, uint[] owned, uint[] missing) =>
        s with { GateUnlockLinks = new CollectibleSet { Owned = owned, Missing = missing } };

    [Fact]
    public void An_unlock_link_gate_is_judged_from_the_capture()
    {
        var (quest, catalog) = Gated(Links(510, 511, 512));
        Assert.Equal([510u, 511u, 512u], catalog.GateUnlockLinkWatch);

        // No read (a file from before 1.19): not checked, so Not checked rather than Ready.
        var unread = StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Unknown, unread.State);
        Assert.True(Assert.IsType<GameGateRequirement>(unread.NextStep!.Req).IsNotChecked);

        // Every link set: met, Ready.
        var ready = StateResolver.Resolve(quest, Read(Snapshot(), [510, 511, 512], []), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Ready, ready.State);
        var met = Assert.Single(ready.Requirements, r => r.Req.Kind == RequirementKind.GameGate);
        Assert.True(met.Met);
        Assert.Equal(Occult, met.Detail);

        // Two read as not set: Blocked, saying what is left (never a tally).
        var blocked = StateResolver.Resolve(quest, Read(Snapshot(), [510], [511, 512]), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Blocked, blocked.State);
        var gate = Assert.IsType<GameGateRequirement>(blocked.NextStep!.Req);
        Assert.True(gate.Judged);
        Assert.False(gate.IsNotChecked);
        Assert.Equal([511u, 512u], gate.MissingLinks);
        Assert.Equal("needs " + Occult + ", 2 left", blocked.NextStep.Detail);
        Assert.Equal(new IpcBlocker(IpcBlockerKinds.GameGate, 511, 1, 0), IpcBlocker.Of(blocked, catalog, null));
        Assert.Equal("unmet", QuestDiagnostic.Verdict(blocked.NextStep));

        // A link the capture did not read (the watch list gained it): not checked, even with the others set.
        var partial = StateResolver.Resolve(quest, Read(Snapshot(), [510, 511], []), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Unknown, partial.State);
    }

    [Fact]
    public void One_link_alone_says_what_it_needs_without_a_count()
    {
        var (quest, catalog) = Gated(Links(114));
        var result = RequirementEvaluator.Evaluate(quest, Read(Snapshot(), [], [114]), catalog, EvalContext.Default).Single(r => r.Req.Kind == RequirementKind.GameGate);
        Assert.False(result.Met);
        Assert.Equal("needs " + Occult, result.Detail);
    }

    [Fact]
    public void A_gate_is_met_by_a_quest_the_game_gives_only_once_it_is_passed()
    {
        var (quest, catalog) = Gated(new QuestGate(Floor, [B]) { MetBy = [A] });

        // Before the dungeon's own quest: Blocked by it.
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default).State);

        // The dungeon open, the floor not shown cleared: not checked.
        var open = StateResolver.Resolve(quest, Snapshot(B), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Unknown, open.State);

        // The quest the game gives after the floor is done: met, Ready.
        var cleared = StateResolver.Resolve(quest, Snapshot(B, A), catalog, EvalContext.Default);
        Assert.Equal(QuestState.Ready, cleared.State);
        var gate = cleared.Requirements.Single(r => r.Req.Kind == RequirementKind.GameGate);
        Assert.True(gate.Met);
        Assert.Equal(Floor + " (What Lies Beneath done)", gate.Detail);
    }

    [Fact]
    public void A_met_by_quest_wins_over_a_link_read_as_missing_and_a_read_link_over_a_missing_proof()
    {
        var gate = new QuestGate("My Little Chocobo done", []) { UnlockLinks = [17], MetBy = [A] };
        var (quest, catalog) = Gated(gate);

        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Read(Snapshot(A), [], [17]), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Ready, StateResolver.Resolve(quest, Read(Snapshot(), [17], []), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Blocked, StateResolver.Resolve(quest, Read(Snapshot(), [], [17]), catalog, EvalContext.Default).State);
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, Snapshot(), catalog, EvalContext.Default).State);
    }

    [Fact]
    public void The_accept_conditions_a_gate_stands_for_are_not_listed_twice()
    {
        var plain = Side(Target, "On the Shoulders of Giants") with { AcceptConditions = [226, 999] };
        var catalog = QuestCatalog.Build([plain], null, new Dictionary<uint, QuestGate> { [Target] = new("floor 30 of Heaven-on-High cleared", []) { AcceptConditions = [226] } });
        Assert.Equal([999u], catalog.UncheckedAcceptConditions(plain));

        var linked = QuestCatalog.Build([plain], null, new Dictionary<uint, QuestGate> { [Target] = Links(226, 999) });
        Assert.Empty(linked.UncheckedAcceptConditions(plain));

        var ungated = QuestCatalog.Build([plain]);
        Assert.Equal([226u, 999u], ungated.UncheckedAcceptConditions(plain));
    }

    [Fact]
    public void Ive_done_this_meets_a_gate_that_cannot_be_checked_and_nothing_else()
    {
        var (quest, catalog) = Gated(new QuestGate(Floor, []) { Sources = [QuestGate.GameTextSource, QuestGate.WikiSource] });
        var marked = EvalContext.Default with { GateMarkedDone = (contentId, rowId) => contentId == 1 && rowId == Target };

        var result = StateResolver.Resolve(quest, Snapshot() with { ContentId = 1 }, catalog, marked);
        Assert.Equal(QuestState.Ready, result.State);
        var gate = result.Requirements.Single(r => r.Req.Kind == RequirementKind.GameGate);
        Assert.True(gate.Met);
        Assert.Equal("you said so", gate.Detail);
        var req = Assert.IsType<GameGateRequirement>(gate.Req);
        Assert.True(req.MarkedByYou);
        Assert.Equal([QuestGate.GameTextSource, QuestGate.WikiSource], req.Sources);

        // Another character is not marked.
        Assert.Equal(QuestState.Unknown, StateResolver.Resolve(quest, Snapshot() with { ContentId = 2 }, catalog, marked).State);

        // A gate the capture judged keeps its own answer: a link read as missing stays unmet.
        var (linkedQuest, linkedCatalog) = Gated(Links(509));
        var linked = StateResolver.Resolve(linkedQuest, Read(Snapshot() with { ContentId = 1 }, [], [509]), linkedCatalog, marked);
        Assert.Equal(QuestState.Blocked, linked.State);
    }

    [Fact]
    public void A_changed_link_read_is_a_change_the_poller_resolves_everything_for()
    {
        var before = Read(Snapshot(), [], [509]);
        var after = Read(Snapshot(), [509], []);
        Assert.False(SnapshotDiff.Compute(before, after).IsEmpty);
        Assert.True(SnapshotDiff.Compute(before, after).OtherChanged);
        Assert.True(SnapshotDiff.Compute(before, Read(Snapshot(), [], [509])).IsEmpty);
    }
}

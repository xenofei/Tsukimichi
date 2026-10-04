using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Tests.Evaluation;

namespace Tsukimichi.Tests.Runtime;

/// <summary>
/// The New Game+ session (1.19.0, C4): recognised from the game's HUD or, with the HUD unreadable, from the quests the
/// plausibility guard restores; the replayed quests kept apart (re-completed ones included) until it ends; the
/// player's End session; and the notices a replay holds back.
/// </summary>
public sealed class NewGamePlusSessionTests
{
    private const uint Story1 = 66000;
    private const uint Story2 = 66001;
    private const uint Side = 66500;
    private const ulong Michiru = 1;

    private static readonly QuestCatalog Catalog = Fixture.Catalog(
        Fixture.Quest(Story1), Fixture.Quest(Story2), Fixture.Quest(Side),
        Fixture.Quest(66600) with { IsRepeatable = true });

    private static ushort Id(uint rowId) => QuestRecord.ToQuestId(rowId);

    [Fact]
    public void No_evidence_no_session()
    {
        var session = new NewGamePlusSession();
        session.ObserveHud(NewGamePlusHud.Inactive);
        session.ObserveCapture(Michiru, []);

        Assert.False(session.Active);
        Assert.Equal(NewGamePlusSource.None, session.Source);
        Assert.Empty(session.Replaying);
        Assert.Equal(0, session.QuestId);
    }

    [Fact]
    public void The_HUD_starts_a_session_on_its_quest_and_the_session_keeps_every_quest_it_replays()
    {
        var session = new NewGamePlusSession();
        session.ObserveCapture(Michiru, []);
        session.ObserveHud(new NewGamePlusHud(true, Id(Story1), 15));

        Assert.True(session.Active);
        Assert.Equal(NewGamePlusSource.Game, session.Source);
        Assert.Equal((Id(Story1), (ushort)15), (session.QuestId, session.Chapter));
        Assert.True(session.IsReplaying(Id(Story1)));

        // The game re-completes Story1 (no longer restored) and moves on: both stay "Replaying" while the session runs.
        session.ObserveCapture(Michiru, [Id(Story2)]);
        session.ObserveHud(new NewGamePlusHud(true, Id(Story2), 15));
        session.ObserveCapture(Michiru, []);
        Assert.True(session.IsReplaying(Id(Story1)));
        Assert.True(session.IsReplaying(Id(Story2)));
        Assert.Equal(Michiru, session.ContentId);

        // The HUD closes and the capture shows nothing replayed: the session ends and forgets.
        session.ObserveHud(NewGamePlusHud.Inactive);
        Assert.False(session.Active);
        Assert.Empty(session.Replaying);
    }

    [Fact]
    public void The_session_is_for_the_character_it_was_captured_from_only()
    {
        const ulong Alt = 2;
        var session = new NewGamePlusSession();

        // The HUD runs before any capture names the character: the line waits for one.
        session.ObserveHud(new NewGamePlusHud(true, Id(Story1), 15));
        Assert.True(session.Active);
        Assert.False(session.IsFor(Michiru));
        Assert.False(session.IsFor(null));

        session.ObserveCapture(Michiru, []);
        Assert.True(session.IsFor(Michiru));

        // Another (stored) character on view, or none: not theirs.
        Assert.False(session.IsFor(Alt));
        Assert.False(session.IsFor(null));

        // No session, no line, even for its character.
        session.ObserveHud(NewGamePlusHud.Inactive);
        Assert.False(session.IsFor(Michiru));
    }

    [Fact]
    public void With_the_HUD_unreadable_the_restored_quests_are_the_evidence()
    {
        var session = new NewGamePlusSession();
        session.ObserveHud(null);
        var before = session.Version;
        session.ObserveCapture(Michiru, [Id(Story1), Id(Story2)]);

        Assert.True(session.Active);
        Assert.Equal(NewGamePlusSource.Replay, session.Source);
        Assert.Equal(0, session.QuestId);
        Assert.False(session.HudReadable);
        Assert.True(session.Version > before);

        session.ObserveCapture(Michiru, []);
        Assert.False(session.Active);
    }

    [Fact]
    public void End_session_stops_the_mode_until_the_evidence_has_gone_once()
    {
        var session = new NewGamePlusSession();
        session.ObserveCapture(Michiru, [Id(Story1)]);
        session.End();

        Assert.False(session.Active);
        Assert.Empty(session.Replaying);
        Assert.False(session.IsReplaying(Id(Story1)));

        // Still the same stuck evidence: stays ended.
        session.ObserveCapture(Michiru, [Id(Story1)]);
        Assert.False(session.Active);

        // The evidence goes, then a new replay starts: the mode is back.
        session.ObserveCapture(Michiru, []);
        session.ObserveCapture(Michiru, [Id(Story2)]);
        Assert.True(session.Active);
        Assert.True(session.IsReplaying(Id(Story2)));
        Assert.False(session.IsReplaying(Id(Story1)));
    }

    [Fact]
    public void Another_character_or_a_logout_starts_over()
    {
        var session = new NewGamePlusSession();
        session.ObserveCapture(Michiru, [Id(Story1)]);
        session.ObserveCapture(2, []);
        Assert.False(session.Active);
        Assert.Empty(session.Replaying);

        session.ObserveCapture(Michiru, [Id(Story1)]);
        session.Reset();
        Assert.False(session.Active);
        Assert.Equal(0ul, session.ContentId);
    }

    [Fact]
    public void Restored_lists_the_bits_the_guard_put_back()
    {
        var raw = Fixture.Snapshot(Side);
        var kept = Fixture.Snapshot(Side, Story1, Story2);

        Assert.Equal([Id(Story1), Id(Story2)], NewGamePlusSession.Restored(raw, kept).Order());
        Assert.Empty(NewGamePlusSession.Restored(kept, kept));
        Assert.Empty(NewGamePlusSession.Restored(kept, raw));
    }

    [Fact]
    public void A_replay_announces_nothing_and_other_quests_announce_as_usual()
    {
        var now = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);
        var record = Fixture.Snapshot(Story1, Story2, 66600);
        QuestEvent[] events =
        [
            new(QuestEventKind.Accepted, Story2, now),        // a completed story quest taken again: only a replay does that
            new(QuestEventKind.NewlyAvailable, Story1, now),  // a replayed quest
            new(QuestEventKind.Accepted, Side, now),          // a side quest taken meanwhile
            new(QuestEventKind.Accepted, 66600, now),         // a repeatable taken again on its schedule
        ];

        var session = new NewGamePlusSession();
        Assert.Same(events, session.Filter(events, Catalog, record));

        session.ObserveCapture(Michiru, [Id(Story1)]);
        var shown = session.Filter(events, Catalog, record);
        Assert.Equal([Side, 66600u], shown.Select(e => e.RowId));
    }
}

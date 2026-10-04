using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>What the game's New Game+ HUD says (<c>AgentId.QuestRedoHud</c>), read on the framework thread.</summary>
/// <param name="Active">A chapter is being replayed: the agent is active and its HUD reads playing, between steps or open.</param>
/// <param name="QuestId">The runtime quest id the chapter is on (0 when not said).</param>
/// <param name="Chapter">The chapter (or part) id the HUD names (0 when not said), a hint for <see cref="Query.NewGamePlusChapters.Position"/>.</param>
public readonly record struct NewGamePlusHud(bool Active, ushort QuestId, ushort Chapter)
{
    /// <summary>No chapter running.</summary>
    public static readonly NewGamePlusHud Inactive = new(false, 0, 0);
}

/// <summary>How the session was recognised.</summary>
public enum NewGamePlusSource : byte
{
    /// <summary>No session.</summary>
    None,

    /// <summary>The game's New Game+ HUD says a chapter is running.</summary>
    Game,

    /// <summary>
    /// The HUD could not be read (the game hooks are paused, or a newer game moved it) and the capture shows a replay:
    /// completion bits cleared, every one of them on a quest some New Game+ chapter lists (1.11.0, C4a).
    /// </summary>
    Replay,
}

/// <summary>
/// The New Game+ session (feature plan v7, 1.19.0, C4). Replaying a chapter makes the game read the chapter's quests as
/// not completed while it lasts. The capture already keeps their real completion (<see cref="CapturePlausibility.Judge"/>,
/// so the saved progress, Moonlit, My blues, Compare and export never lose them); this class is the replay's own,
/// separate record: whether a session runs, where it is (the HUD's quest and chapter), and which quests it is
/// replaying, for the status bar's New Game+ line, the "Replaying" chip and the notice filter. Nothing here is saved.
/// <para>
/// It is recognised from the game's HUD when the hook gate lets the plugin read it (<see cref="ObserveHud"/>), and
/// otherwise from the capture (<see cref="ObserveCapture"/>: replayed quests restored by the plausibility guard). It
/// ends when both say nothing runs. <see cref="End"/> is the player's recovery when detection is stuck: it stops this
/// mode (never the game's) until the evidence has gone once. Framework thread only.
/// </para>
/// </summary>
public sealed class NewGamePlusSession
{
    private readonly HashSet<ushort> replaying = [];
    private NewGamePlusHud hud = NewGamePlusHud.Inactive;
    private bool hudRead;
    private bool restoredNow;
    private bool ended;
    private ulong contentId;

    /// <summary>Moves whenever <see cref="Active"/>, the HUD's quest or <see cref="Replaying"/> changes.</summary>
    public int Version { get; private set; }

    /// <summary>A session runs and the player has not ended it.</summary>
    public bool Active => !ended && Evidence;

    /// <summary>How the running session was recognised; <see cref="NewGamePlusSource.None"/> when none runs.</summary>
    public NewGamePlusSource Source => !Active ? NewGamePlusSource.None : hud.Active ? NewGamePlusSource.Game : NewGamePlusSource.Replay;

    /// <summary>The runtime quest id the HUD says the chapter is on; 0 when unknown or no session runs.</summary>
    public ushort QuestId => Active && hud.Active ? hud.QuestId : (ushort)0;

    /// <summary>The HUD's chapter hint; 0 when unknown or no session runs.</summary>
    public ushort Chapter => Active && hud.Active ? hud.Chapter : (ushort)0;

    /// <summary>The character the session belongs to; 0 when none runs.</summary>
    public ulong ContentId => Active ? contentId : 0;

    /// <summary>
    /// The quests (runtime ids) this session replays: every one whose completion the replay cleared since it began,
    /// re-completed ones included, and the HUD's current quest. Empty when no session runs or the player ended it.
    /// </summary>
    public IReadOnlyCollection<ushort> Replaying => Active ? replaying : [];

    /// <summary>Whether <paramref name="questId"/> is in <see cref="Replaying"/>.</summary>
    public bool IsReplaying(ushort questId) => Active && replaying.Contains(questId);

    private bool Evidence => hud.Active || restoredNow;

    /// <summary>
    /// The HUD as read this frame, or null when it cannot be read (the hook gate is closed, or no character is
    /// logged in): the session then rests on the capture alone.
    /// </summary>
    public void ObserveHud(NewGamePlusHud? read)
    {
        var next = read ?? NewGamePlusHud.Inactive;
        hudRead = read is not null;
        if (next == hud)
        {
            return;
        }

        var wasActive = Active;
        hud = next;
        if (hud.Active && hud.QuestId != 0 && !ended)
        {
            replaying.Add(hud.QuestId);
        }

        Settle(wasActive);
        Version++;
    }

    /// <summary>
    /// One poll's capture of <paramref name="character"/>: <paramref name="restored"/> are the quests whose completion
    /// the plausibility guard kept although the game read them as not completed (<see cref="Restored"/>); empty when the
    /// capture read every completed quest as completed.
    /// </summary>
    public void ObserveCapture(ulong character, IReadOnlyCollection<ushort> restored)
    {
        ArgumentNullException.ThrowIfNull(restored);
        var changed = false;
        var wasActive = Active;
        if (character != contentId)
        {
            changed = contentId != 0 || replaying.Count > 0 || ended;
            contentId = character;
            replaying.Clear();
            ended = false;
        }

        var now = restored.Count > 0;
        changed |= now != restoredNow;
        restoredNow = now;
        if (!ended)
        {
            foreach (var quest in restored)
            {
                changed |= replaying.Add(quest);
            }
        }

        changed |= Settle(wasActive);
        if (changed)
        {
            Version++;
        }
    }

    /// <summary>
    /// The player's End session: stops this mode (the status bar line, the chips, the notice filter) until the HUD and
    /// the capture have both shown no replay once. The game's own New Game+ is untouched.
    /// </summary>
    public void End()
    {
        if (ended || !Evidence)
        {
            return;
        }

        ended = true;
        replaying.Clear();
        Version++;
    }

    /// <summary>Logged out or another character: forgets everything.</summary>
    public void Reset()
    {
        if (contentId == 0 && replaying.Count == 0 && !ended && !Evidence)
        {
            return;
        }

        contentId = 0;
        replaying.Clear();
        hud = NewGamePlusHud.Inactive;
        restoredNow = false;
        ended = false;
        Version++;
    }

    /// <summary>Whether the HUD was readable at its last observation (the gate open and a character logged in).</summary>
    public bool HudReadable => hudRead;

    /// <summary>
    /// The events to announce from <paramref name="events"/> while a session runs: a quest in <see cref="Replaying"/>
    /// neither enters the journal, leaves it, completes nor opens as far as the notices and "Opened" lines go, and nor
    /// does a quest the character's real record (<paramref name="snapshot"/>, the capture the guard kept) has completed
    /// entering or leaving the journal, which only a replay does. Other quests played meanwhile announce as usual.
    /// Returns the list itself when nothing is held back, or no session runs.
    /// </summary>
    public IReadOnlyList<QuestEvent> Filter(IReadOnlyList<QuestEvent> events, QuestCatalog catalog, CharacterSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!Active || events.Count == 0)
        {
            return events;
        }

        List<QuestEvent>? kept = null;
        for (var i = 0; i < events.Count; i++)
        {
            var e = events[i];
            var quest = catalog.GetByRowId(e.RowId);
            var replay = quest is not null
                && (replaying.Contains(quest.QuestId)
                    || (!CapturePlausibility.MayClear(quest.QuestId, catalog) && e.Kind is QuestEventKind.Accepted or QuestEventKind.Abandoned && snapshot.IsCompleted(quest.QuestId)));
            if (replay)
            {
                if (kept is null)
                {
                    kept = new List<QuestEvent>(events.Count);
                    for (var j = 0; j < i; j++)
                    {
                        kept.Add(events[j]);
                    }
                }

                continue;
            }

            kept?.Add(e);
        }

        return kept ?? events;
    }

    /// <summary>
    /// The quests <paramref name="kept"/> reads as completed and <paramref name="raw"/> (the same capture before the
    /// guard) does not: the ones a New Game+ replay cleared and <see cref="CapturePlausibility.KeepReplayed"/> put back.
    /// Empty when the guard changed nothing.
    /// </summary>
    public static IReadOnlyList<ushort> Restored(CharacterSnapshot raw, CharacterSnapshot kept)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(kept);
        if (ReferenceEquals(raw.CompletedBits, kept.CompletedBits))
        {
            return [];
        }

        List<ushort>? restored = null;
        var bits = kept.CompletedBits;
        for (var i = 0; i < bits.Length; i++)
        {
            var added = bits[i] & ~(i < raw.CompletedBits.Length ? raw.CompletedBits[i] : 0);
            for (var bit = 0; added != 0 && bit < 8; bit++)
            {
                if ((added & (1 << bit)) != 0)
                {
                    (restored ??= []).Add((ushort)((i << 3) | bit));
                }
            }
        }

        return restored ?? (IReadOnlyList<ushort>)[];
    }

    /// <summary>Clears the end latch once the evidence has gone, and the replayed set when the session ends.</summary>
    private bool Settle(bool wasActive)
    {
        var changed = false;
        if (!Evidence)
        {
            changed = ended || replaying.Count > 0;
            ended = false;
            replaying.Clear();
        }

        return changed || wasActive != Active;
    }
}

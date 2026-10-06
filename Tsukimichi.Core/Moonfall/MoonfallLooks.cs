namespace Tsukimichi.Core.Moonfall;

/// <summary>How a companion's card shows (characters.md, "The spoiler shield").</summary>
public enum MoonfallCardFace : byte
{
    /// <summary>The Triple Triad card back: the story has not introduced them.</summary>
    Back,

    /// <summary>The card dimmed: met in the story, not yet reached in Moonfall.</summary>
    Dimmed,

    /// <summary>Face up: met and reached.</summary>
    Up,
}

/// <summary>
/// A companion as the characters grid, Quick Play's picker and the duel's opponents show them.
/// </summary>
/// <param name="Companion">Who.</param>
/// <param name="Face">Their card's face.</param>
/// <param name="Named">Whether their name, art and role may show (never for <see cref="MoonfallCardFace.Back"/>: "Not yet met").</param>
/// <param name="Stage">The stage that opens them (shown on a dimmed card: "stage N").</param>
/// <param name="FarShore">Whether that stage is The Far Shore's (the moogle: "The Far Shore" rather than a number).</param>
public readonly record struct MoonfallCompanionLook(MoonfallCompanion Companion, MoonfallCardFace Face, bool Named, int Stage, bool FarShore)
{
    /// <summary>The power is always named, even face down (the spoiler shield hides only the person).</summary>
    public bool PowerNamed => true;

    /// <summary>Whether the card carries its stage ("Lunar Burst · stage 4"): only while met and not yet reached.</summary>
    public bool ShowsStage => Face == MoonfallCardFace.Dimmed;

    /// <summary>Whether they can be played with (Quick Play, "Play with …"): face up only.</summary>
    public bool Playable => Face == MoonfallCardFace.Up;
}

/// <summary>What a map stop shows in its ring.</summary>
public enum MoonfallStopFace : byte
{
    /// <summary>The companion's face (the card's face crop).</summary>
    Portrait,

    /// <summary>The card back: the companion is not yet met in the story.</summary>
    CardBack,

    /// <summary>The free-choice stage's own sign: a gilt four-point star.</summary>
    PickStar,
}

/// <summary>
/// A stop on Adventure's map (spec-rich2.md §4, "Adventure map"): its face, and its four states' signs. Won: a lit
/// orange moon (the pip). Here: a glow in the carrier's colour. Not reached: drained, the ring dimmed, a padlock (the
/// free-choice star has no padlock; it stays dim). Not met: the card back, whatever else holds.
/// </summary>
/// <param name="Face">The ring's face.</param>
/// <param name="Drained">The face drained and darkened (not reached).</param>
/// <param name="Padlock">A padlock at the ring's foot (not reached).</param>
/// <param name="Pip">The lit orange moon (every level won).</param>
/// <param name="Glow">The carrier's glow (it holds the next level).</param>
/// <param name="Dim">The ring dimmed (not reached, or past the player's story).</param>
/// <param name="Veiled">
/// The spoiler shield's mark at the ring's foot in place of the padlock: the stage is set past the player's story
/// (<see cref="MoonfallShield"/>), which is not Moonfall's progress. The companion's face follows its own gating.
/// </param>
public readonly record struct MoonfallStopLook(MoonfallStopFace Face, bool Drained, bool Padlock, bool Pip, bool Glow, bool Dim, bool Veiled = false);

/// <summary>A companion's words for the detail panel (characters.md; r2cast.CAST): English data, as their names are.</summary>
/// <param name="Role">Who they are, as the card would say it.</param>
/// <param name="Line">A line in their voice.</param>
/// <param name="Does">What the power does, in one sentence.</param>
/// <param name="Lasts">How long it lasts ("3 shots", "this shot").</param>
public sealed record MoonfallCompanionLore(string Role, string Line, string Does, string Lasts);

/// <summary>The menus' looks (pure): how each companion and each map stop shows for the progress and the story.</summary>
public static class MoonfallLooks
{
    private static readonly MoonfallCompanionLore[] Lore =
    [
        new("Leader of the Scions of the Seventh Dawn", "Hears the Mother Crystal's voice, and knows where the road goes before you take it.",
            "The aim guide runs on past the first bounce, to the peg after.", "3 shots"),
        new("Twins of Sharlayan, the Archon's grandchildren", "Never agree on a path, so they take both.",
            "A twin ball springs from the green peg and flies the mirror of the first.", "this shot"),
        new("Engineer of the Garlond Ironworks", "Can fix anything with a spanner, and fly most of it.",
            "Airship wings bolt onto the bucket: its mouth doubles in width.", "5 turns"),
        new("General of Ul'dah's Immortal Flames", "Speaks softly; the floor shakes when he does not.",
            "One great sweep at the green peg lights every peg within its reach.", "this shot"),
        new("Admiral of Limsa Lominsa", "Keeps a fleet afloat on wit and powder.",
            "Two oars at the foot's corners bat the ball back up when you click.", "3 turns"),
        new("Scholar of the Scions, reader of the stars", "Answers every question with a better one.",
            "A ball that falls out of the board drops back in from the sky above.", "this shot"),
        new("Elder Seedseer of Gridania", "Listens to the Twelveswood, and it listens back.",
            "Moonflowers open from the green peg and light the nearest fifth of the oranges.", "this shot"),
        new("Receptionist of the Scions, keeper of the purse", "Counts every gil twice, and finds three more.",
            "A draw turns once: a free ball, a triple score, or another friend's power.", "the draw"),
        new("Scholar and mage of the Scions", "Patient with the world, impatient with fools.",
            "The ball meets no peg: each one it touches burns away.", "next shot"),
        new("Archon of Sharlayan, founder of the Circle of Knowing", "Old enough to be patient, wise enough to be quick.",
            "Seventeen angles round your aim are weighed, and the ball is nudged onto the best.", "next shot"),
        new("Moogle post, every inn in Eorzea", "Delivers the bolt, kupo, and signs for nothing.",
            "The first peg lit sends a bolt straight to the bucket, lighting every peg along it.", "this shot"),
    ];

    /// <summary>The companion's words; throws for <see cref="MoonfallCompanion.None"/>.</summary>
    public static MoonfallCompanionLore LoreOf(MoonfallCompanion companion)
    {
        var index = (int)companion - 1;
        return (uint)index < (uint)Lore.Length ? Lore[index] : throw new ArgumentOutOfRangeException(nameof(companion), companion, "not a companion");
    }

    /// <summary>How <paramref name="info"/>'s card shows in <paramref name="state"/>.</summary>
    public static MoonfallCompanionLook Companion(MoonfallCompanionInfo info, MoonfallCompanionState state)
    {
        ArgumentNullException.ThrowIfNull(info);
        var face = state switch
        {
            MoonfallCompanionState.NotMet => MoonfallCardFace.Back,
            MoonfallCompanionState.MetNotReached => MoonfallCardFace.Dimmed,
            _ => MoonfallCardFace.Up,
        };
        return new MoonfallCompanionLook(info.Companion, face, face != MoonfallCardFace.Back, info.Stage, info.Campaign == MoonfallCampaignKind.Expansion);
    }

    /// <summary>How the stop for <paramref name="view"/> shows on the map.</summary>
    public static MoonfallStopLook Stop(MoonfallStageView view) => Stop(view, coming: false);

    /// <summary>
    /// How a map stop shows; a stop whose levels are <paramref name="coming"/> (reached, but not built yet: see
    /// <see cref="Coming"/>) is not sealed, so it shows no padlock and is not drained or dimmed.
    /// </summary>
    public static MoonfallStopLook Stop(MoonfallStageView view, bool coming)
    {
        ArgumentNullException.ThrowIfNull(view);
        var face = view.Stage.PlayerPicks ? MoonfallStopFace.PickStar
            : view.Companion == MoonfallCompanionState.NotMet ? MoonfallStopFace.CardBack
            : MoonfallStopFace.Portrait;
        var sealedStop = view.State == MoonfallStageState.Sealed && !coming;
        var veiled = view.State == MoonfallStageState.Veiled;
        return new MoonfallStopLook(
            face,
            Drained: sealedStop && face == MoonfallStopFace.Portrait,
            Padlock: sealedStop && face != MoonfallStopFace.PickStar,
            Pip: view.State == MoonfallStageState.Done,
            Glow: view.Here && !veiled,
            Dim: sealedStop || veiled,
            Veiled: veiled);
    }

    /// <summary>
    /// Whether a stage is reached but its levels are not built yet (an open Far Shore before its levels ship): its first
    /// level is missing and the campaign's progress (<paramref name="cleared"/> levels won) has come to it. The map says
    /// "Levels on their way" there, not "Not reached".
    /// </summary>
    public static bool Coming(MoonfallStageView view, int cleared)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.State == MoonfallStageState.Sealed && view.Levels.Count > 0 && view.Levels[0].State == MoonfallLevelState.Missing
            && cleared >= view.Stage.FirstLevelIndex;
    }
}

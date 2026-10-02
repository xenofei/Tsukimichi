using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Runtime;

/// <summary>Which mark a quest giver's nameplate carries (1.7.0, R8 D); a larger value wins when one NPC has several.</summary>
public enum NamePlateMarkKind : byte
{
    /// <summary>"☾ Ready on WHM": a pinned or Moonlit-reward quest the character can take on another job.</summary>
    OtherJob = 1,

    /// <summary>"☾ Moonlit reward": a quest with a Moonlit reward the character can take now.</summary>
    MoonlitReward = 2,

    /// <summary>"☾ Pinned": a pinned quest the character can take now.</summary>
    Pinned = 3,
}

/// <summary>One giver's mark: the kind, the quest behind it and, for <see cref="NamePlateMarkKind.OtherJob"/>, the job (ClassJob row id, 0 when unknown).</summary>
public readonly record struct NamePlateMark(NamePlateMarkKind Kind, uint QuestRowId, byte Job);

/// <summary>
/// The optional nameplate marks on quest givers (feature plan v5, 1.7.0; R8 D): which event NPCs get a short title line,
/// worked out once per change of the logged-in character's states, pins or Moonlit catalog, so the nameplate hook's
/// per-frame cost is one dictionary lookup per event NPC plate. Only quests the character can take count (Ready, or
/// Ready on another job); a spare alternative of an open choice and a removed quest never do. Pure.
/// </summary>
public static class NamePlateMarks
{
    /// <summary>
    /// The mark one quest earns, or null: a pinned quest Ready now is <see cref="NamePlateMarkKind.Pinned"/>, a Moonlit
    /// reward quest Ready now is <see cref="NamePlateMarkKind.MoonlitReward"/>, and either of them Ready only on another
    /// job is <see cref="NamePlateMarkKind.OtherJob"/>. Any other quest, or state, earns none.
    /// </summary>
    public static NamePlateMarkKind? Select(QuestState state, bool pinned, bool moonlit) => state switch
    {
        QuestState.Ready when pinned => NamePlateMarkKind.Pinned,
        QuestState.Ready when moonlit => NamePlateMarkKind.MoonlitReward,
        QuestState.ReadyOnOtherJob when pinned || moonlit => NamePlateMarkKind.OtherJob,
        _ => null,
    };

    /// <summary>
    /// ENpcResident row id (the giver's <see cref="Issuer.NpcId"/>, which is an event NPC's base id) to its mark. Where
    /// one NPC hands out several marked quests the strongest kind wins, and among equals the first in journal order.
    /// </summary>
    public static Dictionary<uint, NamePlateMark> Build(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlySet<uint> pinned,
        IReadOnlySet<uint> moonlitQuests)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(pinned);
        ArgumentNullException.ThrowIfNull(moonlitQuests);

        var marks = new Dictionary<uint, NamePlateMark>();
        if (states.Count == 0 || (pinned.Count == 0 && moonlitQuests.Count == 0))
        {
            return marks;
        }

        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved
                || quest.Issuer is not { NpcId: > 0 } issuer
                || !states.TryGetValue(quest.RowId, out var evaluation)
                || evaluation.IsSpareAlternative
                || Select(evaluation.State, pinned.Contains(quest.RowId), moonlitQuests.Contains(quest.RowId)) is not { } kind)
            {
                continue;
            }

            if (marks.TryGetValue(issuer.NpcId, out var existing) && existing.Kind >= kind)
            {
                continue;
            }

            var job = kind == NamePlateMarkKind.OtherJob ? evaluation.ReadyOnJob ?? 0 : (byte)0;
            marks[issuer.NpcId] = new NamePlateMark(kind, quest.RowId, job);
        }

        return marks;
    }

    /// <summary>Whether two mark tables give every NPC the same mark (the hook redraws the plates only when they differ).</summary>
    public static bool Same(IReadOnlyDictionary<uint, NamePlateMark> a, IReadOnlyDictionary<uint, NamePlateMark> b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var (npcId, mark) in a)
        {
            if (!b.TryGetValue(npcId, out var other) || other.Kind != mark.Kind || other.Job != mark.Job)
            {
                return false;
            }
        }

        return true;
    }
}

using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// Questionable's answer about one quest, as its IPC gives it (feature plan v3, V2-17). <see cref="Reason"/> is the
/// text of <c>Questionable.IsQuestLockedReason</c> (its reasons joined by ','), or null when the loaded Questionable
/// only offers <c>Questionable.IsQuestLocked</c>, which answers the flag alone.
/// <para>
/// Questionable answers "locked" for a quest it has no path for, and then gives no reason: <c>IsQuestLockedReason</c>
/// returns <c>(true, "")</c> for such a quest, while a quest it knows and finds locked always carries at least one
/// reason. So a locked answer with no reason says nothing about the quest (<see cref="IsIndeterminate"/>).
/// </para>
/// </summary>
/// <param name="Locked">Questionable's flag: true when it would not start the quest now.</param>
/// <param name="Reason">Its reasons joined by ',' ("Prev quest (2),Aetheryte locked: Ul'dah"); empty when it gave none; null when the gate has no reason at all.</param>
public sealed record QuestionableAnswer(bool Locked, string? Reason)
{
    /// <summary>
    /// Locked, with no reason: Questionable has no path for the quest (<c>IsQuestLockedReason</c>'s <c>(true, "")</c>),
    /// or the reason-less gate said locked, which can mean the same. Either way it is not an opinion on the quest.
    /// </summary>
    public bool IsIndeterminate => Locked && string.IsNullOrWhiteSpace(Reason);

    /// <summary>The reasons one by one, trimmed; empty when there are none.</summary>
    public IReadOnlyList<string> Reasons =>
        string.IsNullOrWhiteSpace(Reason)
            ? []
            : Reason.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>The reasons for display, joined by ", " ("Prev quest (2), Aetheryte locked: Ul'dah").</summary>
    public string ReasonText => string.Join(", ", Reasons);
}

/// <summary>How Tsukimichi's state for a quest compares with Questionable's answer.</summary>
public enum CrossCheckOutcome
{
    /// <summary>Questionable is loaded but did not answer (its gate is missing, not ready, or threw).</summary>
    Unavailable,

    /// <summary>The viewed character is a stored one; Questionable answers for the character logged in.</summary>
    OtherCharacter,

    /// <summary>Tsukimichi's state is not one Questionable's lock speaks to (in the journal, done, locked out, out of season, not checked).</summary>
    NotCompared,

    /// <summary>Questionable said locked with no reason: it has no path for the quest, or its gate gives no reasons.</summary>
    NoAnswer,

    /// <summary>Both say the quest can be picked up, or both say it cannot.</summary>
    Agrees,

    /// <summary>Tsukimichi says the quest can be picked up; Questionable says it is locked.</summary>
    QuestionableLocked,

    /// <summary>Tsukimichi says the quest is blocked by something Questionable also checks; Questionable says it is not locked.</summary>
    QuestionableOpen,
}

/// <summary>One cross-check: the outcome, Questionable's answer (null when it gave none) and Tsukimichi's state.</summary>
/// <param name="LevelAside">
/// Tsukimichi has the quest Blocked only by gates Questionable's lock does not check (level, job, the account caps), so
/// it was compared as open; the pane says the agreement leaves those aside.
/// </param>
public sealed record CrossCheckResult(CrossCheckOutcome Outcome, QuestionableAnswer? Answer, QuestState? State, bool LevelAside = false)
{
    /// <summary>The two answers differ.</summary>
    public bool Disagrees => Outcome is CrossCheckOutcome.QuestionableLocked or CrossCheckOutcome.QuestionableOpen;
}

/// <summary>
/// Compares Tsukimichi's evaluation of a quest with Questionable's lock answer (feature plan v3 §3 V2-17), for the
/// detail pane's "Questionable agrees" line and the diagnostic block's "questionable:" line. Pure; the plugin's
/// <c>Game.QuestionableIpc</c> fetches the answer.
/// <para>
/// What Questionable's <c>QuestFunctions.IsQuestLocked</c> checks (github.com/PunishXIV/Questionable, commit
/// 0bd61efe8a6806a7a8010741c0c46b7dea153709): the Grand Company and its rank, the level of a class or job quest,
/// allied society standing and today's dailies, the carrier level of a delivery moogle quest, retainers for three
/// quests, the previous quests and instances, an aetheryte its path needs, a few special prerequisites (EX mounts,
/// the chocobo race rank, two achievements) and whether a collaboration event runs. It does not check the level of an
/// ordinary quest, the job, the account caps, or whether the quest is already done or in the journal. So only
/// Tsukimichi's Ready, Available on another job and Blocked states are compared, and a quest Blocked only by gates
/// outside that list is compared as open.
/// </para>
/// </summary>
public static class QuestionableCrossCheck
{
    /// <summary>The first Quest sheet row id; Questionable names a quest by the row id's low 16 bits.</summary>
    public const uint FirstRowId = 0x10000;

    /// <summary>One past the last Quest sheet row id.</summary>
    public const uint EndRowId = 0x20000;

    /// <summary>
    /// Quests whose lock check makes Questionable open the game's Achievements window (it requests the achievement
    /// list when it is not loaded): Gold Saucer 4081 and Palace of the Dead 2387, as row ids. Tsukimichi never asks
    /// about these, so a glance at the detail pane opens nothing.
    /// </summary>
    public static readonly IReadOnlySet<uint> SideEffectRowIds = new HashSet<uint> { FirstRowId + 4081, FirstRowId + 2387 };

    /// <summary>The requirement kinds Questionable's lock also checks; a quest Blocked by none of these is compared as open.</summary>
    public static readonly IReadOnlySet<RequirementKind> JudgedKinds = new HashSet<RequirementKind>
    {
        RequirementKind.PreviousQuests,
        RequirementKind.DutyCompletion,
        RequirementKind.GrandCompany,
        RequirementKind.GrandCompanyRank,
        RequirementKind.TribeRank,
        RequirementKind.TribeReputation,
        RequirementKind.TribeAllowance,
        RequirementKind.TribeDailyOffer,
        RequirementKind.CarrierLevel,
    };

    /// <summary>The prefix of Questionable's (English) level reason, "Low level (GLA)"; it checks level for class and job quests only.</summary>
    public const string LowLevelReason = "Low level";

    /// <summary>
    /// The id Questionable's gates take for a Quest sheet row: the row id's low 16 bits in invariant digits
    /// (<c>ElementId.FromString</c> parses a bare number as a <c>QuestId</c>). Null for a row id outside the Quest sheet
    /// and for <see cref="SideEffectRowIds"/>.
    /// </summary>
    public static string? QuestionableId(uint rowId)
    {
        if (rowId < FirstRowId || rowId >= EndRowId || SideEffectRowIds.Contains(rowId))
        {
            return null;
        }

        return (rowId & 0xFFFF).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Compares the two answers. <paramref name="theirs"/> is null when Questionable did not answer (or was not asked);
    /// <paramref name="live"/> is false when the evaluation is a stored character's, which Questionable cannot speak
    /// for, so the caller need not ask it.
    /// </summary>
    public static CrossCheckResult Compare(QuestEvaluation? ours, QuestionableAnswer? theirs, bool live = true)
    {
        var state = ours?.State;
        if (!live)
        {
            return new CrossCheckResult(CrossCheckOutcome.OtherCharacter, theirs, state);
        }

        if (theirs is null)
        {
            return new CrossCheckResult(CrossCheckOutcome.Unavailable, null, state);
        }

        if (ours is null
            || ours.State is not (QuestState.Ready or QuestState.ReadyOnOtherJob or QuestState.Blocked)
            || ours.IsOutOfSeason
            || HasUnmet(ours, RequirementKind.Retired))
        {
            return new CrossCheckResult(CrossCheckOutcome.NotCompared, theirs, state);
        }

        if (theirs.IsIndeterminate)
        {
            return new CrossCheckResult(CrossCheckOutcome.NoAnswer, theirs, state);
        }

        var blocked = ours.State == QuestState.Blocked;
        var oursOpen = !blocked || !HasUnmetJudged(ours);
        var levelAside = blocked && oursOpen;
        if (oursOpen != theirs.Locked)
        {
            return new CrossCheckResult(CrossCheckOutcome.Agrees, theirs, state, levelAside);
        }

        // Questionable checks the level of class and job quests; Tsukimichi's level or job gate says the same thing.
        if (oursOpen && OnlyLowLevel(theirs) && (HasUnmet(ours, RequirementKind.Level) || HasUnmet(ours, RequirementKind.ClassJob) || ours.State == QuestState.ReadyOnOtherJob))
        {
            return new CrossCheckResult(CrossCheckOutcome.Agrees, theirs, state, levelAside);
        }

        return new CrossCheckResult(oursOpen ? CrossCheckOutcome.QuestionableLocked : CrossCheckOutcome.QuestionableOpen, theirs, state, levelAside);
    }

    /// <summary>
    /// The diagnostic block's line after "questionable: ": the outcome, then Questionable's answer.
    /// <code>
    /// agrees; not locked
    /// disagrees; locked: Prev quest (2), Aetheryte locked: Ul'dah; tsukimichi Ready
    /// no answer; locked, no reason (no path for this quest)
    /// </code>
    /// </summary>
    public static string DiagnosticText(CrossCheckResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var outcome = result.Outcome switch
        {
            CrossCheckOutcome.Unavailable => "loaded, no answer from its IPC",
            CrossCheckOutcome.OtherCharacter => "not compared (viewing a stored character)",
            CrossCheckOutcome.NotCompared => "not compared (state " + (result.State?.ToString() ?? "none") + ")",
            CrossCheckOutcome.NoAnswer => "no answer",
            CrossCheckOutcome.Agrees => result.LevelAside ? "agrees (level and job aside)" : "agrees",
            _ => "disagrees",
        };

        if (result.Answer is not { } answer)
        {
            return outcome;
        }

        var text = outcome + "; " + AnswerText(answer);
        return result.Disagrees ? text + "; tsukimichi " + result.State : text;
    }

    /// <summary>"not locked", "locked: Prev quest (2)", "locked, no reason (no path for this quest)", "locked (reason gate absent)".</summary>
    public static string AnswerText(QuestionableAnswer answer)
    {
        ArgumentNullException.ThrowIfNull(answer);
        if (!answer.Locked)
        {
            return "not locked";
        }

        if (answer.Reason is null)
        {
            return "locked (reason gate absent)";
        }

        return answer.IsIndeterminate ? "locked, no reason (no path for this quest)" : "locked: " + answer.ReasonText;
    }

    private static bool HasUnmet(QuestEvaluation evaluation, RequirementKind kind)
    {
        foreach (var result in evaluation.Requirements)
        {
            if (!result.Met && result.Req.Kind == kind)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnmetJudged(QuestEvaluation evaluation)
    {
        foreach (var result in evaluation.Requirements)
        {
            if (!result.Met && JudgedKinds.Contains(result.Req.Kind))
            {
                return true;
            }
        }

        return false;
    }

    private static bool OnlyLowLevel(QuestionableAnswer answer)
    {
        var reasons = answer.Reasons;
        if (reasons.Count == 0)
        {
            return false;
        }

        foreach (var reason in reasons)
        {
            if (!reason.StartsWith(LowLevelReason, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

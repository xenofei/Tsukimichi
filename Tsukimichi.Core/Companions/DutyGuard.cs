namespace Tsukimichi.Core.Companions;

/// <summary>
/// What Tsukimichi does when a Questionable run reaches a duty with no Duty Support or Trust (plan v7, 1.18.0, A3):
/// the setting Settings › Automation › Questionable › "Before a duty with other players".
/// </summary>
public enum DutyGuardMode
{
    /// <summary>Stop Questionable and say why (the default): the community's line is solo and NPC content only.</summary>
    Stop,

    /// <summary>Leave Questionable running and say what is coming, with the Needs-you sound and toast.</summary>
    Warn,

    /// <summary>Say nothing.</summary>
    Nothing,
}

/// <summary>What one look at Questionable's step asks Tsukimichi to do.</summary>
public enum DutyGuardAction
{
    None,

    /// <summary>Stop Questionable: a duty the step may be has no Duty Support or Trust.</summary>
    Stop,

    /// <summary>Say that a duty the step may be has no Duty Support or Trust; Questionable keeps running.</summary>
    Warn,

    /// <summary>
    /// The step is a duty but the quest's script names none Tsukimichi knows: say so and leave Questionable running,
    /// whatever the mode, since there is no duty to name.
    /// </summary>
    Unsure,
}

/// <summary>The action, and the duty it is about.</summary>
/// <param name="Action">What to do.</param>
/// <param name="Duty">The first duty with other players the step may be; null for none and for <see cref="DutyGuardAction.Unsure"/>.</param>
/// <param name="Certain">
/// Every duty the step may be needs other players. False when the quest also names one with Duty Support or Trust the
/// character has not cleared yet (the expansion finales name a dungeon and a trial): the step may be either, so the
/// line says "may be".
/// </param>
public readonly record struct DutyGuardVerdict(DutyGuardAction Action, DutyRunInfo? Duty, bool Certain)
{
    public static readonly DutyGuardVerdict None = new(DutyGuardAction.None, null, false);
}

/// <summary>
/// The rules of A3, pure so they are tested without the game. Questionable's step data names the step's kind
/// (<c>StepData.InteractionType</c>, its <c>EInteractionType</c> as text) but not its duty, so the duty comes from the
/// quest's own script: the <c>INSTANCEDUNGEON</c> and <c>CONTENT_START</c> constants Tsukimichi.GameData's
/// <c>QuestScriptDuties.NamedRuns</c> reads, mapped to the Duty Finder entries of <see cref="DutyRunIndex"/>. A quest
/// naming several (Endwalker's finale names the Dead Ends and the Final Day) is narrowed to those the character has
/// not cleared, which on a first run is the one still ahead.
/// <para>
/// Only <see cref="DutyInteraction"/> counts: Questionable's <c>Duty</c> step queues a Duty Finder entry (through
/// AutoDuty, or by opening the Duty Finder on it; <c>Questionable/Controller/Steps/Interactions/Duty.cs</c> at
/// PunishXIV/Questionable 0bd61efe). Its <c>SinglePlayerDuty</c> step is a solo quest battle and is never guarded.
/// </para>
/// </summary>
public static class DutyGuard
{
    /// <summary>Questionable's <c>EInteractionType.Duty</c>, as <c>GetCurrentStepData</c> spells it.</summary>
    public const string DutyInteraction = "Duty";

    /// <summary>The duty can be run with an NPC party: Duty Support or Trust lists it.</summary>
    public static bool NpcRunnable(DutyRunInfo duty)
    {
        ArgumentNullException.ThrowIfNull(duty);
        return duty.OffersDutySupport || duty.OffersTrust;
    }

    /// <summary>
    /// The verdict for one step: <paramref name="interactionType"/> is Questionable's step kind and
    /// <paramref name="questDuties"/> the duties the quest's script names; <paramref name="cleared"/> says whether the
    /// character has cleared one (null when unknown: then none counts as cleared). None unless the step is a duty, the
    /// mode acts, and a duty the step may be has no NPC party; then Stop or Warn per <paramref name="mode"/>. A duty step
    /// of a quest naming no known duty is <see cref="DutyGuardAction.Unsure"/>.
    /// </summary>
    public static DutyGuardVerdict Decide(DutyGuardMode mode, string? interactionType, IReadOnlyList<DutyRunInfo> questDuties, Func<DutyRunInfo, bool>? cleared = null)
    {
        ArgumentNullException.ThrowIfNull(questDuties);
        if (mode == DutyGuardMode.Nothing || !string.Equals(interactionType, DutyInteraction, StringComparison.Ordinal))
        {
            return DutyGuardVerdict.None;
        }

        if (questDuties.Count == 0)
        {
            return new DutyGuardVerdict(DutyGuardAction.Unsure, null, false);
        }

        // The duties the step may be: those not cleared yet, or all of them when every one is (a repeat, New Game+).
        Func<DutyRunInfo, bool>? skip = null;
        if (cleared is not null)
        {
            foreach (var duty in questDuties)
            {
                if (!cleared(duty))
                {
                    skip = cleared;
                    break;
                }
            }
        }

        DutyRunInfo? firstWithPlayers = null;
        var anyNpc = false;
        foreach (var duty in questDuties)
        {
            if (skip is not null && skip(duty))
            {
                continue;
            }

            if (NpcRunnable(duty))
            {
                anyNpc = true;
            }
            else
            {
                firstWithPlayers ??= duty;
            }
        }

        if (firstWithPlayers is null)
        {
            return DutyGuardVerdict.None;
        }

        return new DutyGuardVerdict(mode == DutyGuardMode.Stop ? DutyGuardAction.Stop : DutyGuardAction.Warn, firstWithPlayers, !anyNpc);
    }
}

/// <summary>
/// Acts once per Questionable step. A step acted on is not acted on again while Questionable stays on it, so a Warn
/// is said once, and after a Stop the player can start Questionable again on the same step to let it go ahead (the chat
/// line says so). Questionable reaching another step forgets it. Pure; fed Questionable's status while it runs.
/// </summary>
public sealed class DutyGuardWatch
{
    private (uint RowId, byte Sequence, int Step)? actedOn;

    /// <summary>
    /// One reading of Questionable's step. Returns the verdict to act on now, or <see cref="DutyGuardVerdict.None"/>.
    /// <paramref name="dutiesFor"/> and <paramref name="cleared"/> are asked only for a duty step not acted on yet.
    /// </summary>
    public DutyGuardVerdict Observe(DutyGuardMode mode, uint? rowId, byte? sequence, int? step, string? interactionType, Func<uint, IReadOnlyList<DutyRunInfo>> dutiesFor, Func<DutyRunInfo, bool>? cleared = null)
    {
        ArgumentNullException.ThrowIfNull(dutiesFor);
        if (rowId is not { } row || sequence is not { } seq || step is not { } index)
        {
            return DutyGuardVerdict.None;
        }

        var key = (row, seq, index);
        if (actedOn is { } acted)
        {
            if (acted == key)
            {
                return DutyGuardVerdict.None;
            }

            actedOn = null;
        }

        if (mode == DutyGuardMode.Nothing || !string.Equals(interactionType, DutyGuard.DutyInteraction, StringComparison.Ordinal))
        {
            return DutyGuardVerdict.None;
        }

        var verdict = DutyGuard.Decide(mode, interactionType, dutiesFor(row), cleared);
        if (verdict.Action != DutyGuardAction.None)
        {
            actedOn = key;
        }

        return verdict;
    }

    /// <summary>Forgets the step acted on (another character logged in).</summary>
    public void Reset() => actedOn = null;
}

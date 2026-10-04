using System.Globalization;
using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.Core.Ui;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>What the game's own offers say about Tsukimichi's state for one quest (feature plan v7, C1).</summary>
public enum GameOfferVerdict : byte
{
    /// <summary>Nothing to compare: no sighting, or the quest is in the journal, done, or a stale sighting of a state that moved since.</summary>
    None,

    /// <summary>The game showed it and Tsukimichi reads it Ready (on this job or another).</summary>
    Agrees,

    /// <summary>The game showed it while Tsukimichi reads it Not checked: the gate Tsukimichi cannot read is met.</summary>
    Confirms,

    /// <summary>The game showed it recently while Tsukimichi reads it Blocked or Locked out: one of the two is wrong.</summary>
    Disagrees,

    /// <summary>
    /// Tsukimichi reads it Ready and the character stood in the giver's zone for several polls while the game listed
    /// other quests there but not this one. Suspicion only: the game hides some markers (low-level quests among
    /// them), so this never changes a state and only reaches the owner's report.
    /// </summary>
    Unseen,
}

/// <summary>One quest's comparison: the verdict, the sighting it rests on, and the polls without a marker.</summary>
public readonly record struct GameOfferCheck(GameOfferVerdict Verdict, OfferSighting? Sighting, int UnseenPolls)
{
    public static readonly GameOfferCheck Nothing = new(GameOfferVerdict.None, null, 0);
}

/// <summary>A quest where the game and Tsukimichi do not say the same thing, for the owner's report.</summary>
public sealed record GameDisagreement(QuestRecord Quest, QuestEvaluation Evaluation, GameOfferCheck Check);

/// <summary>
/// Reconciles the game's own offers (<see cref="OfferSightings"/>) with Tsukimichi's computed state (feature plan v7,
/// C1). The game showing a quest is evidence it can be taken: a quest Tsukimichi reads Not checked is confirmed, one it
/// reads Blocked is a disagreement for a bug report. Nothing here changes a state. Pure.
/// </summary>
public static class GameOfferChecks
{
    /// <summary>Polls in the giver's zone without its marker before a Ready quest counts as unseen.</summary>
    public const int UnseenPolls = 3;

    /// <summary>
    /// How recent a sighting must be to contradict a Blocked state. An older one may predate a change that blocks the
    /// quest for real (another Grand Company, another path chosen), so it is only printed, not called a disagreement.
    /// </summary>
    public static readonly TimeSpan Fresh = TimeSpan.FromHours(1);

    /// <summary>The comparison for one quest; <see cref="GameOfferCheck.Nothing"/> without an evaluation.</summary>
    public static GameOfferCheck Judge(QuestRecord quest, QuestEvaluation? evaluation, OfferSighting? sighting, int unseenPolls, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(quest);
        if (evaluation is null)
        {
            return GameOfferCheck.Nothing;
        }

        if (sighting is null)
        {
            return evaluation.State == QuestState.Ready && unseenPolls >= UnseenPolls
                ? new GameOfferCheck(GameOfferVerdict.Unseen, null, unseenPolls)
                : GameOfferCheck.Nothing;
        }

        var verdict = evaluation.State switch
        {
            QuestState.Ready or QuestState.ReadyOnOtherJob => GameOfferVerdict.Agrees,
            QuestState.Unknown => GameOfferVerdict.Confirms,
            QuestState.Blocked or QuestState.Foreclosed when CanContradict(quest, evaluation) && nowUtc - sighting.LastSeenUtc <= Fresh => GameOfferVerdict.Disagrees,
            _ => GameOfferVerdict.None,
        };
        return new GameOfferCheck(verdict, sighting, 0);
    }

    /// <summary>
    /// Every quest the game and Tsukimichi disagree on (<see cref="GameOfferVerdict.Disagrees"/>), the ones the game
    /// confirmed (<see cref="GameOfferVerdict.Confirms"/>) and the unseen Ready ones (<see cref="GameOfferVerdict.Unseen"/>),
    /// in that order, then by row id.
    /// </summary>
    public static List<GameDisagreement> Disagreements(
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        IReadOnlyDictionary<ushort, OfferSighting> sightings,
        IReadOnlyDictionary<ushort, int> unseen,
        DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(states);
        ArgumentNullException.ThrowIfNull(sightings);
        ArgumentNullException.ThrowIfNull(unseen);

        var result = new List<GameDisagreement>();
        foreach (var sighting in sightings.Values)
        {
            Add(result, catalog, states, sighting.RowId, sighting, 0, nowUtc);
        }

        foreach (var (questId, polls) in unseen)
        {
            if (!sightings.ContainsKey(questId))
            {
                Add(result, catalog, states, 0x10000u | questId, null, polls, nowUtc);
            }
        }

        result.Sort(static (a, b) =>
        {
            var byVerdict = Rank(a.Check.Verdict).CompareTo(Rank(b.Check.Verdict));
            return byVerdict != 0 ? byVerdict : a.Quest.RowId.CompareTo(b.Quest.RowId);
        });
        return result;
    }

    /// <summary>
    /// The Report block's "game:" value (always English): "offered on the map 2026-10-03T08:00:00Z, last
    /// 2026-10-03T09:10:00Z; agrees", "…; disagrees: tsukimichi Blocked", "…; confirms the gate tsukimichi could not
    /// check", "no marker in the giver's zone over 4 polls; tsukimichi Ready". Null when there is nothing to say.
    /// </summary>
    public static string? DiagnosticText(GameOfferCheck check, QuestEvaluation? evaluation)
    {
        if (check.Sighting is not { } sighting)
        {
            return check.Verdict == GameOfferVerdict.Unseen
                ? string.Create(CultureInfo.InvariantCulture, $"no marker in the giver's zone over {check.UnseenPolls} polls; tsukimichi {StateWord(evaluation)}")
                : null;
        }

        var sb = new StringBuilder(96);
        sb.Append("offered ").Append(SourceWords(sighting.Sources)).Append(' ').Append(Stamp(sighting.FirstSeenUtc));
        if (sighting.LastSeenUtc > sighting.FirstSeenUtc)
        {
            sb.Append(", last ").Append(Stamp(sighting.LastSeenUtc));
        }

        sb.Append("; ").Append(check.Verdict switch
        {
            GameOfferVerdict.Agrees => "agrees",
            GameOfferVerdict.Confirms => "confirms the gate tsukimichi could not check",
            GameOfferVerdict.Disagrees => "disagrees: tsukimichi " + StateWord(evaluation),
            _ => "earlier; tsukimichi " + StateWord(evaluation),
        });
        return sb.ToString();
    }

    /// <summary>
    /// The detail pane's line, for the two verdicts a player should see: "The game offers this quest (seen 2 days
    /// ago), so the gate Tsukimichi cannot read is met." and "The game offered this quest 5 min ago, but Tsukimichi
    /// reads it Blocked. Report this quest so it can be fixed." Null for every other verdict: an agreement is no news,
    /// and an unseen marker is only a suspicion.
    /// </summary>
    public static string? DetailLine(GameOfferCheck check, DateTime nowUtc)
    {
        if (check.Sighting is not { } sighting)
        {
            return null;
        }

        var age = AbandonedLedger.AgeText(nowUtc - sighting.LastSeenUtc);
        return check.Verdict switch
        {
            GameOfferVerdict.Confirms => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.GameOffer.Confirms", "The game offers this quest (seen {0}), so the gate Tsukimichi cannot read is met."), age),
            GameOfferVerdict.Disagrees => string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.GameOffer.Disagrees", "The game offered this quest {0}, but Tsukimichi reads it as not available. Report this quest so it can be fixed."), age),
            _ => null,
        };
    }

    /// <summary>The <c>/tsuki why</c> line: "Game: offered on the map 2 days ago." and the verdict's clause; null when nothing was seen.</summary>
    public static string? WhyLine(GameOfferCheck check, DateTime nowUtc)
    {
        if (check.Sighting is not { } sighting)
        {
            return null;
        }

        var where = sighting.Sources.HasFlag(OfferSource.Offer)
            ? CoreText.T("Core.GameOffer.WhyOffer", "Game: offered to you {0}.")
            : CoreText.T("Core.GameOffer.WhyMarker", "Game: shown on the map {0}.");
        var line = string.Format(CultureInfo.CurrentCulture, where, AbandonedLedger.AgeText(nowUtc - sighting.LastSeenUtc));
        return check.Verdict == GameOfferVerdict.Disagrees
            ? line + " " + CoreText.T("Core.GameOffer.WhyDisagrees", "Tsukimichi disagrees: please report this quest.")
            : line;
    }

    /// <summary>
    /// The plain-text report Settings › Advanced › Diagnostics copies (always English): one line per quest, the
    /// disagreements first. Empty when there is none.
    /// </summary>
    public static string Report(IReadOnlyList<GameDisagreement> rows, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation>? states = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(names);
        using var english = CoreText.English();
        var sb = new StringBuilder(rows.Count * 96);
        foreach (var row in rows)
        {
            sb.Append(row.Quest.RowId.ToString(CultureInfo.InvariantCulture)).Append(" \"").Append(names.QuestName(row.Quest)).Append("\": ")
                .Append(BlockerText.StatusText(row.Evaluation, row.Quest, names, states)).Append(" | game ")
                .Append(DiagnosticText(row.Check, row.Evaluation) ?? "nothing").Append('\n');
        }

        return sb.ToString();
    }

    /// <summary>
    /// Whether the game's offer can contradict a Blocked or Locked out state: never for a seasonal quest (an event that
    /// ended blocks it for real) or a quest out of season.
    /// </summary>
    private static bool CanContradict(QuestRecord quest, QuestEvaluation evaluation) =>
        quest.Festival == 0 && !quest.IsRepeatable && !evaluation.IsOutOfSeason;

    private static void Add(
        List<GameDisagreement> into,
        QuestCatalog catalog,
        IReadOnlyDictionary<uint, QuestEvaluation> states,
        uint rowId,
        OfferSighting? sighting,
        int polls,
        DateTime nowUtc)
    {
        if (catalog.GetByRowId(rowId) is not { } quest || !states.TryGetValue(rowId, out var evaluation))
        {
            return;
        }

        var check = Judge(quest, evaluation, sighting, polls, nowUtc);
        if (check.Verdict is GameOfferVerdict.Disagrees or GameOfferVerdict.Confirms or GameOfferVerdict.Unseen)
        {
            into.Add(new GameDisagreement(quest, evaluation, check));
        }
    }

    private static int Rank(GameOfferVerdict verdict) => verdict switch
    {
        GameOfferVerdict.Disagrees => 0,
        GameOfferVerdict.Confirms => 1,
        _ => 2,
    };

    private static string SourceWords(OfferSource sources) => sources switch
    {
        OfferSource.Marker | OfferSource.Offer => "on the map and in the offer window",
        OfferSource.Offer => "in the offer window",
        _ => "on the map",
    };

    private static string StateWord(QuestEvaluation? evaluation) => evaluation is null ? "no evaluation" : StateNames.Name(evaluation.State);

    private static string Stamp(DateTime utc) => utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}

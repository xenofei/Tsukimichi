using System.Globalization;
using System.Text;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>
/// The "Report this quest" block (feature plan v3 T18): one fenced text block a player pastes into a GitHub issue,
/// composed on click only. It carries the plugin, client and data versions, the quest, its display state with the
/// blocker line, one line per requirement in the evaluator's order with the verdict and the values compared, the
/// curated quirk note when the quest has one, the character inputs the evaluation used, and when they were
/// captured. It is always English, whatever the UI language (<see cref="CoreText.English"/>). It never carries the content id, the character's name, the world or the account: the fields the
/// composer reads from the snapshot are gameplay ones.
/// <code>
/// ```tsukimichi-diagnostic
/// plugin: 0.6.0.0
/// game: 2026.09.15.0000.0000 (client) / 2026.09.15.0000.0000 (data)
/// data: unique_quests generated 2026-09-28T22:39:14Z, curated 573d225, schema 1
/// quest: 66754 "Brotherhood of Ash" (genre 89, lvl 24 / display 24, filing n/a)
/// state: Blocked · after: Peace for Thanalan
/// requirements:
///   - Level: met (24 ≤ 31)
///   - PreviousQuests: unmet (66753 Peace for Thanalan: not done)
///   - TribeRank: met (Amalj'aa Recognized ≥ Recognized)
/// quirk: Up in Arms is optional once the Zenith is in hand …   (only when curated/quirks.json names the quest)
/// questionable: agrees; not locked   (only when Questionable is loaded)
/// questionable more: path yes; list #3; unobtainable no, agrees   (what Questionable's other gates answered)
/// in game: offered on the map 2026-10-03T08:00:00Z; agrees   (when the game showed the quest, C1)
/// inputs: job WHM 31, msq 66043, tribe 1 rank 2 rep 0
/// captured: 2026-09-28T21:14:02Z live
/// ```
/// </code>
/// </summary>
public static class QuestDiagnostic
{
    /// <summary>Opens the block; the language tag lets a triager find every pasted block with one search.</summary>
    public const string FenceOpen = "```tsukimichi-diagnostic";

    public const string FenceClose = "```";

    /// <summary>Verdict of a requirement the plugin cannot judge (accept conditions, an unknown mount or house, achievements not loaded).</summary>
    public const string NotChecked = "not checked";

    public const string Met = "met";
    public const string Unmet = "unmet";

    /// <summary>Daily allied society allowances a character has after the reset; printed as the denominator of "dailies n/12".</summary>
    public const int MaxDailyAllowance = 12;

    private const string Unknown = "unknown";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const string Indent = "  - ";

    /// <summary>The block, lines joined by '\n' and wrapped in the fence.</summary>
    public static string Compose(DiagnosticInputs inputs)
    {
        // For bug reports and tools: English whatever the UI language (docs/localization.md).
        using var english = CoreText.English();
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(inputs.Quest);
        ArgumentNullException.ThrowIfNull(inputs.Names);
        ArgumentNullException.ThrowIfNull(inputs.Context);

        var quest = inputs.Quest;
        var names = inputs.Names;
        var sb = new StringBuilder(768);

        sb.Append(FenceOpen).Append('\n');
        sb.Append("plugin: ").Append(Or(inputs.PluginVersion)).Append('\n');
        sb.Append("game: ").Append(Or(inputs.ClientGameVersion)).Append(" (client) / ").Append(Or(inputs.DataGameVersion)).Append(" (data)\n");
        sb.Append("data: unique_quests generated ").Append(inputs.DataGeneratedUtc is { } generated ? Timestamp(generated) : Unknown)
            .Append(", curated ").Append(Or(inputs.CuratedRevision))
            .Append(", schema ").Append(inputs.SnapshotSchema.ToString(CultureInfo.InvariantCulture)).Append('\n');

        sb.Append("quest: ").Append(quest.RowId.ToString(CultureInfo.InvariantCulture)).Append(" \"").Append(names.QuestName(quest)).Append("\" (genre ")
            .Append(quest.Journal.GenreId.ToString(CultureInfo.InvariantCulture))
            .Append(", lvl ").Append(quest.Level.ToString(CultureInfo.InvariantCulture))
            .Append(" / display ").Append(quest.DisplayLevel.ToString(CultureInfo.InvariantCulture))
            .Append(", filing ").Append(Or(inputs.FilingRule)).Append(")\n");

        AppendState(sb, inputs);
        AppendRequirements(sb, inputs);
        AppendQuirk(sb, inputs);
        AppendQuestionable(sb, inputs);
        AppendGameOffer(sb, inputs);
        AppendInputs(sb, inputs);

        sb.Append("captured: ");
        if (inputs.Snapshot is { } snapshot)
        {
            sb.Append(Timestamp(snapshot.TakenUtc)).Append(inputs.IsLive ? " live" : " stored");
        }
        else
        {
            sb.Append("no character");
        }

        sb.Append('\n').Append(FenceClose);
        return sb.ToString();
    }

    private static void AppendState(StringBuilder sb, DiagnosticInputs inputs)
    {
        sb.Append("state: ");
        if (inputs.Evaluation is not { } evaluation)
        {
            sb.Append(StateNames.Name(QuestState.Unknown, inputs.Quest)).Append(BlockerText.Separator).Append("no evaluation\n");
            return;
        }

        sb.Append(BlockerText.StatusText(evaluation, inputs.Quest, inputs.Names, inputs.States));
        if (evaluation.ReadyOnJob is { } job)
        {
            sb.Append(" (").Append(JobName(inputs.Names, job)).Append(')');
        }

        sb.Append('\n');
    }

    private static void AppendRequirements(StringBuilder sb, DiagnosticInputs inputs)
    {
        var requirements = inputs.Evaluation?.Requirements;
        if (requirements is null || requirements.Count == 0)
        {
            sb.Append("requirements: none\n");
            return;
        }

        sb.Append("requirements:\n");
        foreach (var result in requirements)
        {
            sb.Append(Indent);
            AppendRequirement(sb, result, inputs.Names);
            sb.Append('\n');
        }
    }

    /// <summary>The curated quirk note, when the quest has one: what the game does that its data does not say.</summary>
    private static void AppendQuirk(StringBuilder sb, DiagnosticInputs inputs)
    {
        if (inputs.QuirkNote is { Length: > 0 } quirk)
        {
            sb.Append("quirk: ").Append(quirk).Append('\n');
        }
    }

    /// <summary>
    /// Questionable's answer beside Tsukimichi's, when Questionable is loaded (V2-17): "questionable: agrees; not
    /// locked", "questionable: disagrees; locked: Prev quest (1); tsukimichi Ready". A disagreement is the line a
    /// triager looks for.
    /// </summary>
    private static void AppendQuestionable(StringBuilder sb, DiagnosticInputs inputs)
    {
        if (inputs.Questionable is { } check)
        {
            sb.Append("questionable: ").Append(Ipc.QuestionableCrossCheck.DiagnosticText(check)).Append('\n');
        }

        if (inputs.QuestionableMore is { } more && Ipc.QuestionableWiderCheck.DiagnosticText(more, inputs.Evaluation?.State) is { Length: > 0 } text)
        {
            sb.Append("questionable more: ").Append(text).Append('\n');
        }
    }

    /// <summary>
    /// What the game's own offers say (feature plan v7, C1): "in game: offered on the map 2026-10-03T08:00:00Z; agrees",
    /// "…; disagrees: tsukimichi Blocked". A disagreement is the line a triager looks for.
    /// </summary>
    private static void AppendGameOffer(StringBuilder sb, DiagnosticInputs inputs)
    {
        if (inputs.GameOffer is { } check && GameOfferChecks.DiagnosticText(check, inputs.Evaluation) is { Length: > 0 } text)
        {
            sb.Append("in game: ").Append(text).Append('\n');
        }
    }

    /// <summary>
    /// "Level: met (24 ≤ 31)", "PreviousQuests: unmet (66753 Peace for Thanalan: not done)": one requirement as the
    /// block prints it, without the indent. <c>/tsuki why</c> prints the same lines, so a chat answer and a pasted
    /// block read alike.
    /// </summary>
    public static string RequirementLine(RequirementResult result, BlockerNames names)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(names);
        var sb = new StringBuilder(96);
        AppendRequirement(sb, result, names);
        return sb.ToString();
    }

    private static void AppendRequirement(StringBuilder sb, RequirementResult result, BlockerNames names)
    {
        sb.Append(result.Req.Kind.ToString()).Append(": ").Append(Verdict(result)).Append(" (");
        AppendValues(sb, result, names);
        sb.Append(')');
    }

    /// <summary>"met", "unmet", or "not checked" for a gate the evaluator listed without judging it.</summary>
    public static string Verdict(RequirementResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Req switch
        {
            AcceptConditionRequirement => NotChecked,
            MountRequirement { HasMount: null } => NotChecked,
            HouseRequirement { HasHouse: null } => NotChecked,
            AchievementRequirement { Loaded: false } => NotChecked,
            GameGateRequirement { IsNotChecked: true } => NotChecked,
            CustomDeliveryRankRequirement { ActualRank: null } => NotChecked,
            CarrierLevelRequirement { ActualLevel: null } => NotChecked,
            TribeReputationRequirement { NotChecked: true } => NotChecked,
            _ => result.Met ? Met : Unmet,
        };
    }

    private static void AppendValues(StringBuilder sb, RequirementResult result, BlockerNames names)
    {
        switch (result.Req)
        {
            case OtherPathRequirement o:
                // "StartCity Ul'dah, chosen Gridania; StartClass Gladiator, chosen Lancer; by 65575 Coming to Gridania"
                for (var i = 0; i < o.Facets.Count; i++)
                {
                    var facet = o.Facets[i];
                    if (i > 0)
                    {
                        sb.Append("; ");
                    }

                    sb.Append(facet.Kind.ToString()).Append(' ').Append(PathText.Options(facet, names.GrandCompany));
                    sb.Append(", chosen ").Append(facet.Chosen is { } chosen ? (chosen.GrandCompany != 0 ? names.GrandCompany(chosen.GrandCompany) : chosen.Text) : "none");
                }

                if (o.Evidence.Length > 0)
                {
                    sb.Append("; by ");
                    AppendQuestList(sb, o.Evidence, names);
                }

                break;

            case ForeclosureRequirement f:
                sb.Append("locks ");
                AppendQuestList(sb, f.LockIds, names);
                if (f.CompletedLockIds.Length == 0)
                {
                    sb.Append("; none completed");
                }
                else
                {
                    sb.Append("; completed ");
                    AppendQuestList(sb, f.CompletedLockIds, names);
                }

                break;

            case ExpansionCapRequirement e:
                sb.Append(names.Expansion(e.Expansion)).Append(" (").Append(e.Expansion.ToString(CultureInfo.InvariantCulture)).Append(") > cap ")
                    .Append(e.MaxExpansion.ToString(CultureInfo.InvariantCulture));
                break;

            case LevelCapRequirement l:
                sb.Append(l.Level.ToString(CultureInfo.InvariantCulture)).Append(" > cap ").Append(l.LevelCap.ToString(CultureInfo.InvariantCulture));
                break;

            case ClassJobRequirement c:
                if (c.RequiredJob != 0)
                {
                    sb.Append("job ").Append(JobName(names, c.RequiredJob));
                }
                else
                {
                    sb.Append("category ").Append(c.CategoryId.ToString(CultureInfo.InvariantCulture));
                    var category = names.ClassJobCategory(c.CategoryId);
                    if (category.Length > 0)
                    {
                        sb.Append(' ').Append(category);
                    }
                }

                sb.Append(", you are ").Append(JobName(names, c.Job));
                break;

            case LevelRequirement l:
                sb.Append(l.Level.ToString(CultureInfo.InvariantCulture)).Append(Compare(result.Met)).Append(l.ActualLevel.ToString(CultureInfo.InvariantCulture));
                break;

            case PreviousQuestsRequirement p:
                if (p.Join == JoinKind.Any)
                {
                    sb.Append("any of ");
                }

                for (var i = 0; i < p.QuestIds.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    var id = p.QuestIds[i];
                    AppendQuest(sb, id, names);
                    sb.Append(p.DoneIds is not null && Array.IndexOf(p.DoneIds, id) >= 0 ? ": done" : ": not done");
                }

                break;

            case GrandCompanyRequirement g:
                sb.Append(GrandCompanyName(names, g.GrandCompany)).Append(" required, you are ").Append(GrandCompanyName(names, g.ActualGrandCompany));
                break;

            case GrandCompanyRankRequirement g:
                sb.Append(GrandCompanyName(names, g.GrandCompany)).Append(" rank ").Append(g.ActualRank.ToString(CultureInfo.InvariantCulture))
                    .Append(CompareActualFirst(result.Met)).Append(g.RequiredRank.ToString(CultureInfo.InvariantCulture));
                break;

            case TribeRankRequirement t:
                sb.Append(TribeName(names, t.Tribe)).Append(' ').Append(names.TribeRank(t.ActualRank)).Append(CompareActualFirst(result.Met)).Append(names.TribeRank(t.RequiredRank));
                break;

            case TribeReputationRequirement { NotChecked: true } t:
                sb.Append(TribeName(names, t.Tribe)).Append(' ').Append(names.TribeRank(t.MaxedRank)).Append(" maxed, not checked");
                break;

            case TribeReputationRequirement { MaxedRank: not 0 } t:
                sb.Append(TribeName(names, t.Tribe)).Append(' ').Append(names.TribeRank(t.MaxedRank)).Append(' ').Append(t.ActualValue.ToString(CultureInfo.InvariantCulture))
                    .Append(CompareActualFirst(result.Met)).Append(t.RequiredValue.ToString(CultureInfo.InvariantCulture)).Append(" (maxed)");
                break;

            case TribeReputationRequirement t:
                sb.Append(TribeName(names, t.Tribe)).Append(' ').Append(t.ActualValue.ToString(CultureInfo.InvariantCulture))
                    .Append(CompareActualFirst(result.Met)).Append(t.RequiredValue.ToString(CultureInfo.InvariantCulture));
                break;

            case TribeAllowanceRequirement a:
                sb.Append(a.Allowance.ToString(CultureInfo.InvariantCulture)).Append(" left today");
                break;

            case TribeDailyOfferRequirement d:
                sb.Append("quest ").Append(d.QuestId.ToString(CultureInfo.InvariantCulture)).Append(d.Offered ? " offered today" : " not offered today");
                break;

            case DutyCompletionRequirement d:
                sb.Append(d.DoneCount.ToString(CultureInfo.InvariantCulture)).Append(" of ").Append(d.InstanceIds.Length.ToString(CultureInfo.InvariantCulture)).Append(" cleared");
                if (d.Join == JoinKind.Any)
                {
                    sb.Append(", one needed");
                }

                sb.Append(": ");
                for (var i = 0; i < d.InstanceIds.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    var id = d.InstanceIds[i];
                    sb.Append(id.ToString(CultureInfo.InvariantCulture));
                    var duty = names.Duty(id);
                    if (duty.Length > 0)
                    {
                        sb.Append(' ').Append(duty);
                    }
                }

                break;

            case SeasonalRequirement s:
                sb.Append("festival ").Append(s.FestivalId.ToString(CultureInfo.InvariantCulture)).Append(s.Active ? " active" : " not active");
                if (s.HasWindow)
                {
                    sb.Append(", window ").Append(s.Begin.ToString(CultureInfo.InvariantCulture)).Append('-').Append(s.End.ToString(CultureInfo.InvariantCulture));
                    if (s.Active)
                    {
                        sb.Append(", phase ").Append(s.Phase is { } phase ? phase.ToString(CultureInfo.InvariantCulture) : Unknown);
                    }
                }

                break;

            case CustomDeliveryRankRequirement c:
                sb.Append(SatisfactionNpcName(names, c.Npc)).Append(" rank ");
                if (c.ActualRank is { } rank)
                {
                    sb.Append(rank.ToString(CultureInfo.InvariantCulture)).Append(CompareActualFirst(result.Met)).Append(c.RequiredRank.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(Unknown).Append(", needs ").Append(c.RequiredRank.ToString(CultureInfo.InvariantCulture));
                }

                break;

            case CarrierLevelRequirement c:
                sb.Append("carrier level ");
                if (c.ActualLevel is { } level)
                {
                    sb.Append(level.ToString(CultureInfo.InvariantCulture)).Append(CompareActualFirst(result.Met)).Append(c.RequiredLevel.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    sb.Append(Unknown).Append(", needs ").Append(c.RequiredLevel.ToString(CultureInfo.InvariantCulture));
                }

                break;

            case AcceptConditionRequirement a:
                sb.Append("conditions ");
                for (var i = 0; i < a.ConditionIds.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(a.ConditionIds[i].ToString(CultureInfo.InvariantCulture));
                }

                sb.Append("; listed, not judged");
                break;

            case MountRequirement m:
                // Mount ids only: the diagnostic block stays language-neutral.
                sb.Append("has mount ").Append(YesNo(m.HasMount)).Append(", mounts [").Append(string.Join(", ", m.Mounts)).Append(']');
                if (m.Missing.Length > 0)
                {
                    sb.Append(", missing [").Append(string.Join(", ", m.Missing)).Append(']');
                }
                break;

            case HouseRequirement h:
                sb.Append("has house ").Append(YesNo(h.HasHouse));
                break;

            case AchievementRequirement a:
                sb.Append("achievements ").Append(a.Loaded ? "loaded" : "not loaded").Append(", quest ").Append(a.RowId.ToString(CultureInfo.InvariantCulture));
                break;

            case GameGateRequirement { IsNotChecked: true } g:
                sb.Append("game gate \"").Append(g.Gate).Append("\"; listed, not judged");
                break;

            case GameGateRequirement { Judged: true } g:
                // Unlock link ids only, as for the weapons below.
                sb.Append("game gate \"").Append(g.Gate).Append("\"; judged, ").Append(result.Met ? "met" : "unmet")
                    .Append(", links not set [").Append(string.Join(", ", g.MissingLinks)).Append(']');
                break;

            case GameGateRequirement g:
                // Item ids only: the diagnostic block stays language-neutral.
                sb.Append("game gate \"").Append(g.Gate).Append("\"; ").Append(g.Checked == GateHold.Held ? "held" : "equipped")
                    .Append(" check, equipped [").Append(string.Join(", ", g.Equipped)).Append("], matching [")
                    .Append(string.Join(", ", g.Matching)).Append(']');
                break;

            default:
                sb.Append(result.Detail);
                break;
        }
    }

    /// <summary>
    /// "inputs: job WHM 31, …": the job and level always, then only what the listed requirements read: the account
    /// caps, the main scenario position (for a prerequisite gate, when every state is at hand), the Grand Company
    /// and its rank, the allied society standing, the daily allowances and today's offer, the active festivals, the
    /// mount, the house and whether achievements are loaded.
    /// </summary>
    private static void AppendInputs(StringBuilder sb, DiagnosticInputs inputs)
    {
        sb.Append("inputs: ");
        if (inputs.Snapshot is not { } s)
        {
            sb.Append("no character\n");
            return;
        }

        var names = inputs.Names;
        var quest = inputs.Quest;
        sb.Append("job ").Append(JobName(names, s.CurrentJob)).Append(' ').Append(RequirementEvaluator.LevelOf(s, s.CurrentJob).ToString(CultureInfo.InvariantCulture));

        var kinds = Kinds(inputs.Evaluation);
        if (Has(kinds, RequirementKind.ExpansionCap) || Has(kinds, RequirementKind.LevelCap))
        {
            sb.Append(", cap expansion ").Append(s.MaxExpansion.ToString(CultureInfo.InvariantCulture)).Append(" lv ").Append(s.LevelCap.ToString(CultureInfo.InvariantCulture));
        }

        if (Has(kinds, RequirementKind.PreviousQuests) && inputs.States is { } states && names.Catalog.Count > 0
            && MsqProgress.Compute(names.Catalog, states) is { } msq)
        {
            sb.Append(", msq ").Append(msq.Next is { } next ? next.RowId.ToString(CultureInfo.InvariantCulture) : "complete");
            foreach (var route in msq.Routes)
            {
                // Inside a branch region: "route 70011 2/3" per route, by its first quest's row id.
                sb.Append(" route ").Append(route.Route.First.RowId.ToString(CultureInfo.InvariantCulture))
                    .Append(' ').Append(route.Done.ToString(CultureInfo.InvariantCulture))
                    .Append('/').Append(route.Total.ToString(CultureInfo.InvariantCulture));
            }
        }

        if (Has(kinds, RequirementKind.GrandCompany) || Has(kinds, RequirementKind.GrandCompanyRank))
        {
            var rank = s.GrandCompany < s.GcRanks.Length ? s.GcRanks[s.GrandCompany] : (byte)0;
            sb.Append(", gc ").Append(s.GrandCompany.ToString(CultureInfo.InvariantCulture)).Append(" rank ").Append(rank.ToString(CultureInfo.InvariantCulture));
        }

        if (quest.BeastTribe != 0 && (Has(kinds, RequirementKind.TribeRank) || Has(kinds, RequirementKind.TribeReputation)
            || Has(kinds, RequirementKind.TribeAllowance) || Has(kinds, RequirementKind.TribeDailyOffer)))
        {
            var standing = s.Tribes.GetValueOrDefault(quest.BeastTribe);
            sb.Append(", tribe ").Append(quest.BeastTribe.ToString(CultureInfo.InvariantCulture))
                .Append(" rank ").Append(standing.Rank.ToString(CultureInfo.InvariantCulture))
                .Append(" rep ").Append(standing.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (Has(kinds, RequirementKind.TribeAllowance) || Has(kinds, RequirementKind.TribeDailyOffer))
        {
            sb.Append(", dailies ").Append(s.TribeAllowance.ToString(CultureInfo.InvariantCulture)).Append('/').Append(MaxDailyAllowance.ToString(CultureInfo.InvariantCulture));
        }

        if (Has(kinds, RequirementKind.TribeDailyOffer))
        {
            sb.Append(", offer ");
            if (inputs.Context.TodaysDailyOffer is { } offer
                && (inputs.Context.DailyOfferTribes is not { } known || known.Contains(quest.BeastTribe)))
            {
                AppendIds(sb, offer);
            }
            else
            {
                sb.Append(Unknown);
            }
        }

        if (Has(kinds, RequirementKind.Seasonal))
        {
            sb.Append(", festivals ");
            AppendFestivals(sb, s);
        }

        if (Has(kinds, RequirementKind.CustomDeliveryRank))
        {
            var rank = s.SatisfactionRank(quest.SatisfactionNpc);
            sb.Append(", delivery client ").Append(quest.SatisfactionNpc.ToString(CultureInfo.InvariantCulture))
                .Append(" rank ").Append(rank is { } r ? r.ToString(CultureInfo.InvariantCulture) : Unknown);
        }

        if (Has(kinds, RequirementKind.CarrierLevel))
        {
            sb.Append(", carrier level ").Append(s.CarrierLevel is { } level ? level.ToString(CultureInfo.InvariantCulture) : Unknown);
        }

        if (Has(kinds, RequirementKind.Mount))
        {
            var mount = inputs.Evaluation?.Requirements.Select(r => r.Req).OfType<MountRequirement>().FirstOrDefault();
            sb.Append(", mount ").Append(YesNo(mount?.HasMount));
        }

        if (Has(kinds, RequirementKind.House))
        {
            sb.Append(", house ").Append(YesNo(inputs.Context.HasHouse));
        }

        if (Has(kinds, RequirementKind.Achievement))
        {
            sb.Append(", achievements ").Append(s.AchievementsLoaded ? "loaded" : "not loaded");
        }

        sb.Append('\n');
    }

    /// <summary>Bit set of the requirement kinds an evaluation listed.</summary>
    private static int Kinds(QuestEvaluation? evaluation)
    {
        var kinds = 0;
        if (evaluation is null)
        {
            return kinds;
        }

        foreach (var result in evaluation.Requirements)
        {
            kinds |= 1 << (int)result.Req.Kind;
        }

        return kinds;
    }

    private static bool Has(int kinds, RequirementKind kind) => (kinds & (1 << (int)kind)) != 0;

    /// <summary>" ≤ " when met, " > " when not: the required value first, then the character's.</summary>
    private static string Compare(bool met) => met ? " ≤ " : " > ";

    /// <summary>" ≥ " when met, " &lt; " when not: the character's value first, then the required one.</summary>
    private static string CompareActualFirst(bool met) => met ? " ≥ " : " < ";

    private static string YesNo(bool? value) => value switch
    {
        true => "yes",
        false => "no",
        null => Unknown,
    };

    private static string Or(string? value) => string.IsNullOrWhiteSpace(value) ? Unknown : value;

    private static string Timestamp(DateTime utc) =>
        (utc.Kind == DateTimeKind.Local ? utc.ToUniversalTime() : utc).ToString(TimestampFormat, CultureInfo.InvariantCulture);

    private static string JobName(BlockerNames names, uint job)
    {
        var abbreviation = names.JobAbbreviation(job);
        return abbreviation.Length > 0 ? abbreviation : "job " + job.ToString(CultureInfo.InvariantCulture);
    }

    private static string GrandCompanyName(BlockerNames names, byte grandCompany) =>
        grandCompany == 0 ? "none" : names.GrandCompany(grandCompany);

    private static string TribeName(BlockerNames names, byte tribe)
    {
        var name = names.Tribe(tribe);
        return name.Length > 0 ? name : "tribe " + tribe.ToString(CultureInfo.InvariantCulture);
    }

    private static string SatisfactionNpcName(BlockerNames names, byte npc)
    {
        var name = names.SatisfactionNpc(npc);
        return name.Length > 0 ? name : "client " + npc.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>"[39/1, 256/0]" as id/phase when the capture holds phases, "[39, 256]" without them; "[-]" when none run.</summary>
    private static void AppendFestivals(StringBuilder sb, CharacterSnapshot s)
    {
        if (s.ActiveFestivalPhases.Count == 0)
        {
            AppendIds(sb, s.ActiveFestivals);
            return;
        }

        sb.Append('[');
        for (var i = 0; i < s.ActiveFestivals.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            sb.Append(s.ActiveFestivals[i].ToString(CultureInfo.InvariantCulture));
            if (i < s.ActiveFestivalPhases.Count)
            {
                sb.Append('/').Append(s.ActiveFestivalPhases[i].ToString(CultureInfo.InvariantCulture));
            }
        }

        if (s.ActiveFestivals.Count == 0)
        {
            sb.Append('-');
        }

        sb.Append(']');
    }

    private static void AppendQuest(StringBuilder sb, uint rowId, BlockerNames names)
    {
        sb.Append(rowId.ToString(CultureInfo.InvariantCulture));
        if (names.Catalog.GetByRowId(rowId) is { Name.Length: > 0 } quest)
        {
            sb.Append(' ').Append(names.QuestName(quest));
        }
    }

    private static void AppendQuestList(StringBuilder sb, uint[] rowIds, BlockerNames names)
    {
        for (var i = 0; i < rowIds.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(", ");
            }

            AppendQuest(sb, rowIds[i], names);
        }
    }

    /// <summary>"[1, 2]" for ids in the order given; "[-]" when there are none.</summary>
    private static void AppendIds(StringBuilder sb, IEnumerable<ushort> ids)
    {
        sb.Append('[');
        var any = false;
        foreach (var id in ids)
        {
            if (any)
            {
                sb.Append(", ");
            }

            sb.Append(id.ToString(CultureInfo.InvariantCulture));
            any = true;
        }

        if (!any)
        {
            sb.Append('-');
        }

        sb.Append(']');
    }
}

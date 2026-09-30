using System.Globalization;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Localization;

namespace Tsukimichi.Core.Diagnostics;

/// <summary>
/// The chat lines <c>/tsuki why &lt;quest&gt;</c> prints (feature plan v3 P2): why an NPC is not offering a quest,
/// one line per requirement, in the words the Status column and the diagnostic block already use.
/// <code>
/// Blocked · after: Peace for Thanalan
///   - Level: met (24 ≤ 31)
///   - PreviousQuests: unmet (66753 Peace for Thanalan: not done)
/// Note: …                                            (curated/quirks.json, when the quest has one)
/// </code>
/// A Ready quest instead says where to go: "Ready — talk to Gerolt in Northern Thanalan (23.1, 14.2)"; the plugin
/// prints that line with a map link. Pure text: the caller supplies the place name and coordinates it reads from
/// the map sheet.
/// </summary>
public static class WhyText
{
    /// <summary>Before each requirement line; the same indent the diagnostic block uses.</summary>
    public const string Indent = "  - ";

    /// <summary>The headline when the character has no evaluation yet (logged out, catalog not built).</summary>
    public static string NoEvaluation => CoreText.T("Core.Why.NoEvaluation", "Not checked · no character evaluated yet");

    /// <summary>
    /// The first line. Ready or Ready on another job: the state name, then where to go ("Ready — talk to Gerolt in
    /// Northern Thanalan (23.1, 14.2)"; the place and coordinates are left out when the caller has none, the giver when
    /// the quest has none). Every other state: the Status text ("Blocked · after: Peace for Thanalan", "Locked out ·
    /// closed by: …", "In journal · step 3 of 7", "Completed"). <see cref="NoEvaluation"/> without an evaluation.
    /// </summary>
    /// <param name="place">The giver's zone from the map sheet, or null.</param>
    /// <param name="coordinates">The giver's map coordinates, or null.</param>
    public static string Headline(
        QuestEvaluation? evaluation,
        QuestRecord quest,
        BlockerNames names,
        IReadOnlyDictionary<uint, QuestEvaluation>? states = null,
        string? place = null,
        (float X, float Y)? coordinates = null)
    {
        ArgumentNullException.ThrowIfNull(quest);
        ArgumentNullException.ThrowIfNull(names);

        if (evaluation is null)
        {
            return NoEvaluation;
        }

        if (evaluation.State is not (QuestState.Ready or QuestState.ReadyOnOtherJob))
        {
            return BlockerText.StatusText(evaluation, quest, names, states);
        }

        var line = StateNames.Name(evaluation.State, quest);
        if (evaluation.ReadyOnJob is { } job && names.JobAbbreviation(job) is { Length: > 0 } abbreviation)
        {
            line += " (" + abbreviation + ")";
        }

        if (quest.Issuer is not { Name.Length: > 0 } giver)
        {
            return line;
        }

        var where = place is { Length: > 0 } ? place : string.Empty;
        if (coordinates is { } c)
        {
            var at = string.Format(CultureInfo.InvariantCulture, "({0:0.0}, {1:0.0})", c.X, c.Y);
            where = where.Length > 0 ? where + " " + at : at;
        }

        return where.Length > 0
            ? string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Why.TalkToIn", "{0} — talk to {1} in {2}"), line, giver.Name, where)
            : string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Why.TalkTo", "{0} — talk to {1}"), line, giver.Name);
    }

    /// <summary>One indented line per requirement, in the evaluator's order: "  - Level: met (24 ≤ 31)".</summary>
    public static List<string> RequirementLines(QuestEvaluation evaluation, BlockerNames names)
    {
        ArgumentNullException.ThrowIfNull(evaluation);
        ArgumentNullException.ThrowIfNull(names);

        var lines = new List<string>(evaluation.Requirements.Count);
        foreach (var result in evaluation.Requirements)
        {
            lines.Add(Indent + QuestDiagnostic.RequirementLine(result, names));
        }

        return lines;
    }

    /// <summary>"Note: …" for a curated quirk note.</summary>
    public static string NoteLine(string note)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(note);
        return string.Format(CultureInfo.CurrentCulture, CoreText.T("Core.Why.Note", "Note: {0}"), note.Trim());
    }

    /// <summary>
    /// Every line in print order: the headline, the requirement lines (none without an evaluation), then the note
    /// when the quest has one.
    /// </summary>
    public static List<string> Lines(
        QuestEvaluation? evaluation,
        QuestRecord quest,
        BlockerNames names,
        IReadOnlyDictionary<uint, QuestEvaluation>? states = null,
        string? place = null,
        (float X, float Y)? coordinates = null,
        string? quirkNote = null)
    {
        var lines = new List<string> { Headline(evaluation, quest, names, states, place, coordinates) };
        if (evaluation is not null)
        {
            lines.AddRange(RequirementLines(evaluation, names));
        }

        if (!string.IsNullOrWhiteSpace(quirkNote))
        {
            lines.Add(NoteLine(quirkNote));
        }

        return lines;
    }
}

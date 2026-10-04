using System.Globalization;
using Tsukimichi.Core.Query;

namespace Tsukimichi.Core.Ipc;

/// <summary>
/// The summary's lines (plan v8 M1 and M2) that name a place: the server info bar's tooltip and the summary IPC gates
/// speak for the logged-in character, so a place goes through that character's shield, whichever character a pane shows.
/// </summary>
public static class SummaryText
{
    /// <summary>
    /// Up next's step line, "Step 3: Speak with Erenville. · Shaaloani": the step, its objective (or
    /// <paramref name="noObjective"/>) and its zone named through <paramref name="shield"/>, the logged-in character's.
    /// </summary>
    /// <param name="step">The step's number.</param>
    /// <param name="objective">The game's objective text for it; empty for none.</param>
    /// <param name="zone">The zone's own name, not yet shielded; null or empty leaves the place out.</param>
    /// <param name="shield">The logged-in character's spoiler shield.</param>
    /// <param name="stepFormat">"Step {0}: {1}".</param>
    /// <param name="noObjective">The words for a step without objective text.</param>
    /// <param name="separator">" · ".</param>
    public static string StepLine(int step, string objective, string? zone, SpoilerMask shield, string stepFormat, string noObjective, string separator)
    {
        ArgumentNullException.ThrowIfNull(shield);
        var line = string.Format(CultureInfo.CurrentCulture, stepFormat, step, string.IsNullOrEmpty(objective) ? noObjective : objective);
        var place = string.IsNullOrEmpty(zone) ? string.Empty : shield.Name(SpoilerKind.Area, zone);
        return place.Length > 0 ? line + separator + place : line;
    }
}

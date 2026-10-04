using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;

namespace Tsukimichi.Ui;

/// <summary>
/// The Loose ends card on the Characters dashboard, after Side stories (feature plan v7 N8; spec-1.21 N8): the
/// storylines the viewed character started and never finished (<see cref="LooseEndsSource"/>), caption "5 started, not
/// finished". Two-line rows (<see cref="DrawStoryRows"/>): line 1 the line's icon and name; line 2 a neutral Finale chip
/// when the next quest ends the line, then one order for every row, "1 left · Lv 80 · ◑ A Harmony from the Heavens"
/// (the approval record's note). A next quest past the story point names neither itself nor its zone: "1 left · ○ Job
/// quest ahead (Lv 80)", with the moon disc and no actions.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>Rows the card shows before "N more ›".</summary>
    private const int LooseRowsShown = 5;

    private (IReadOnlyList<LooseEnd>? Ends, int Language, int Shield) looseKey = (null, -1, 0);
    private StoryRowView[] looseRows = [];
    private string looseCaption = string.Empty;
    private bool looseAll;

    /// <summary>The storylines started and never finished; set by the plugin. Null hides the card.</summary>
    public LooseEndsSource? LooseEnds { get; set; }

    private void DrawLooseEnds(UiState ui)
    {
        if (LooseEnds is not { } source || session.Bundle is null || session.States.Count == 0)
        {
            return;
        }

        RefreshLooseEnds(source);
        using var id = ImRaii.PushId("looseEnds");
        Gap();
        SectionHeading.Draw(Strings.LooseEndsSection, looseCaption.Length > 0 ? looseCaption : null);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.LooseEndsTooltip);
        }

        if (looseRows.Length == 0)
        {
            ImGui.TextDisabled(Strings.LooseEndsNone);
            return;
        }

        DrawStoryRows(ui, "##ends", looseRows, looseAll ? looseRows.Length : Math.Min(LooseRowsShown, looseRows.Length));
        DrawMoreToggle(looseRows.Length - LooseRowsShown, ref looseAll);
    }

    private void RefreshLooseEnds(LooseEndsSource source)
    {
        var ends = source.Viewed;
        var spoilers = session.Spoilers;
        var key = (ends, Localization.Loc.Version, spoilers.Fingerprint);
        if (key == looseKey)
        {
            return;
        }

        looseKey = key;
        var rows = new StoryRowView[ends.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            var end = ends[i];
            var name = LooseEndsSource.NextName(end, spoilers, out var ahead);
            var lead = ahead
                ? string.Format(CultureInfo.CurrentCulture, Strings.LooseEndsLeftFormat, end.Left)
                : string.Format(CultureInfo.CurrentCulture, Strings.LooseEndsLeftLevelFormat, end.Left, end.Next.DisplayLevel);
            rows[i] = new StoryRowView(
                source.IconOf(end.Line),
                source.NameOf(end.Line, spoilers),
                end.IsFinale ? Strings.LooseEndsFinale : null,
                false,
                lead,
                end.Next,
                name,
                true,
                end.NextState,
                ahead,
                string.Empty,
                end.Line.Chain.RowIds,
                end.Line.Kind is StoryLineKind.Chain or StoryLineKind.Story ? RecapQuestOf(session.Chains, end.Line.Chain, session.States) : 0);
        }

        looseRows = rows;
        looseCaption = rows.Length == 0 ? string.Empty : string.Format(CultureInfo.CurrentCulture, Strings.LooseEndsCaptionFormat, rows.Length);
    }
}

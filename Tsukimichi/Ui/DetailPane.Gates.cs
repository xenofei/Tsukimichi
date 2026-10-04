using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Gates Tsukimichi can't check (feature plan v7 C3, spec-1.19 C3): on the Requirements card a game gate it cannot read
/// gets the third verdict, a hollow Dusk ring, its plain-words label, "can't check", the line that says where the gate
/// was confirmed, and two quiet buttons: <b>I've done this</b> (marks the gate passed for the character on view, with
/// the floating Undo) and <b>Where to start</b> (selects the quest that opens what the gate needs). A gate the player
/// marked reads as a check, "you said so".
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string MarkDoneIcon = FontAwesomeIcon.Check.ToIconString();
    private static readonly string StartIcon = FontAwesomeIcon.ArrowRight.ToIconString();

    /// <summary>The character on view when the model was built (the one "I've done this" marks); null for none.</summary>
    private ulong? lastViewed;

    /// <summary>The per-character settings the "I've done this" marks are kept in; null leaves the button out.</summary>
    public CharacterSettingsBook? Characters { get; set; }

    /// <summary>The Requirements line of a game gate Tsukimichi can't check, or one the player marked passed.</summary>
    private static RequirementLine GateLine(SessionState session, CatalogBundle bundle, QuestRecord quest, GameGateRequirement gate, RequirementLine line)
    {
        var label = gate.Gate.Length == 0 ? line.Label : char.ToUpper(gate.Gate[0], CultureInfo.CurrentCulture) + gate.Gate[1..];
        if (gate.MarkedByYou)
        {
            return line with { Label = label, Detail = Strings.GateYouSaidSo };
        }

        var start = StartOf(session, bundle.Catalog.GameGateOf(quest.RowId));
        return line with
        {
            Label = label,
            Detail = Strings.GateCantCheck,
            CantCheck = true,
            Source = SourceLine(gate),
            MarkRowId = session.ViewedContentId is not null ? quest.RowId : 0,
            StartRowId = start,
            StartTooltip = start == 0
                ? null
                : string.Format(CultureInfo.CurrentCulture, Strings.GateWhereToStartTooltipFormat, session.Spoilers.DisplayName(bundle.Catalog, start, start.ToString(CultureInfo.InvariantCulture))),
            GapText = null,
            JumpRowId = 0,
        };
    }

    /// <summary>"The game doesn't show plugins this. From the wiki, confirmed by 2 sources."; null without sources.</summary>
    private static string? SourceLine(GameGateRequirement gate)
    {
        if (gate.Sources.Count == 0)
        {
            return null;
        }

        var from = gate.Sources.Contains(QuestGate.WikiSource) ? Strings.GateSourceWiki
            : gate.Sources.Contains(QuestGate.GameTextSource) ? Strings.GateSourceGameText
            : Strings.GateSourceSheets;
        return string.Format(CultureInfo.CurrentCulture, Strings.GateSourceFormat, from, gate.Sources.Count);
    }

    /// <summary>The first quest the gate cannot be passed before (<see cref="QuestGate.After"/>) not done yet; 0 for none.</summary>
    private static uint StartOf(SessionState session, QuestGate? gate)
    {
        if (gate is null)
        {
            return 0;
        }

        foreach (var rowId in gate.After)
        {
            if (!(session.States.TryGetValue(rowId, out var evaluation) && evaluation.State == QuestState.Completed))
            {
                return rowId;
            }
        }

        return 0;
    }

    /// <summary>
    /// Under a can't-check line: the source line (caption size, Dusk) and the quiet buttons. Returns the new bottom.
    /// </summary>
    private float DrawGateExtras(RequirementLine line, float left, float top, float room)
    {
        var bottom = top;
        if (line.Source is { } source)
        {
            ImGui.SetCursorScreenPos(new Vector2(left, top + UiMetrics.Px(2f)));
            using (Typography.Caption())
            {
                TextFlow.Wrapped(source, room, Theme.U32(Theme.Surface.TextTertiary));
            }

            bottom = ImGui.GetItemRectMax().Y;
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.GateCantCheckTooltip);
            }
        }

        var canMark = line.MarkRowId != 0 && Characters is not null && lastViewed is not null;
        if (!canMark && line.StartRowId == 0)
        {
            return bottom;
        }

        ImGui.SetCursorScreenPos(new Vector2(left, bottom + UiMetrics.Px(4f)));
        var any = false;
        if (canMark && Chrome.ActionPill("##gateDone", MarkDoneIcon, Strings.GateMarkDone, PillTone.Quiet, enabled: true, Strings.GateMarkDoneTooltip, PillLayout.Row))
        {
            MarkGateDone(line);
        }

        any |= canMark;
        if (line.StartRowId != 0)
        {
            if (any)
            {
                ImGui.SameLine();
            }

            if (Chrome.ActionPill("##gateStart", StartIcon, Strings.GateWhereToStart, PillTone.Quiet, enabled: true, line.StartTooltip, PillLayout.Row))
            {
                RevealRow(line.StartRowId);
            }
        }

        return MathF.Max(bottom, ImGui.GetItemRectMax().Y);
    }

    /// <summary>"I've done this": marks the gate for the character on view, with the floating Undo (8 s).</summary>
    private void MarkGateDone(RequirementLine line)
    {
        if (Characters is not { } book || lastViewed is not { } contentId)
        {
            return;
        }

        var rowId = line.MarkRowId;
        book.Edit(CharacterSettingChange.GateDone(contentId, rowId, true));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, Strings.GateMarkedToastFormat, line.Label),
            () => book.Edit(CharacterSettingChange.GateDone(contentId, rowId, false)));
    }
}

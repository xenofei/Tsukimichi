using System;
using System.Collections.Generic;
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
/// marked reads as a check, "you said so", with a quiet <b>Take back</b> under it (the "…" menu has it too, as it has
/// "Use Tsukimichi's answer"), which reads the gate as can't check again, with its own Undo. The marks are per quest:
/// the curated gates are keyed by the quest they stand before and carry no id of their own.
/// </summary>
public sealed partial class DetailPane
{
    private static readonly string MarkDoneIcon = FontAwesomeIcon.Check.ToIconString();
    private static readonly string StartIcon = FontAwesomeIcon.ArrowRight.ToIconString();
    private static readonly string TakeBackIcon = FontAwesomeIcon.Undo.ToIconString();

    /// <summary>The character on view when the model was built (the one "I've done this" marks); null for none.</summary>
    private ulong? lastViewed;

    /// <summary>The per-character settings the "I've done this" marks are kept in; null leaves the button out.</summary>
    public CharacterSettingsBook? Characters { get; set; }

    /// <summary>The Requirements line of a game gate Tsukimichi can't check, or one the player marked passed.</summary>
    private static RequirementLine GateLine(SessionState session, CatalogBundle bundle, QuestRecord quest, GameGateRequirement gate, RequirementLine line)
    {
        var label = GateLabel(gate.Gate, line.Label);
        if (gate.MarkedByYou)
        {
            return line with { Label = label, Detail = Strings.GateYouSaidSo, MarkedByYou = true, MarkRowId = session.ViewedContentId is not null ? quest.RowId : 0 };
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

    /// <summary>The gate's phrase with its first letter raised ("Floor 50 of the Palace of the Dead cleared"); <paramref name="fallback"/> when it has none.</summary>
    private static string GateLabel(string gate, string fallback) =>
        gate.Length == 0 ? fallback : char.ToUpper(gate[0], CultureInfo.CurrentCulture) + gate[1..];

    /// <summary>"The game doesn't show plugins this. From the wiki, confirmed by 2 sources."; null with fewer than two sources besides the player.</summary>
    private static string? SourceLine(GameGateRequirement gate)
    {
        // The player's own confirmation (a gate the wiki alone states, 1.22.0) is no source the gate was confirmed by, so
        // such a gate shows no source line rather than "confirmed by 1 sources".
        var confirmedBy = gate.Sources.Count(s => s != QuestGate.PlayerSource);
        if (confirmedBy < 2)
        {
            return null;
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.GateSourceFormat, StatedIn(gate.Sources), confirmedBy);
    }

    /// <summary>
    /// Where a gate is stated, for "From {0}": the wiki, else the game's quest text, the sheets, the Lodestone,
    /// Questionable, in that order of the sources it has; a gate the Lodestone and Questionable alone confirm is labelled
    /// by them, never as "the game's data".
    /// </summary>
    internal static string StatedIn(IReadOnlyList<string> sources) => SourceLabelKey(sources) switch
    {
        QuestGate.WikiSource => Strings.GateSourceWiki,
        QuestGate.GameTextSource => Strings.GateSourceGameText,
        QuestGate.LodestoneSource => Strings.GateSourceLodestone,
        QuestGate.QuestionableSource => Strings.GateSourceQuestionable,
        _ => Strings.GateSourceSheets,
    };

    /// <summary>The source a gate's "From …" names (<see cref="StatedIn"/>): the first of wiki, game text, sheet, Lodestone, Questionable it has.</summary>
    internal static string SourceLabelKey(IReadOnlyList<string> sources)
        => new[] { QuestGate.WikiSource, QuestGate.GameTextSource, QuestGate.SheetSource, QuestGate.LodestoneSource, QuestGate.QuestionableSource }
            .FirstOrDefault(sources.Contains) ?? QuestGate.SheetSource;

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
    /// Under a can't-check line: the source line (caption size, Dusk) and the quiet buttons; under a "you said so" line,
    /// Take back. Returns the new bottom.
    /// </summary>
    private float DrawGateExtras(RequirementLine line, float left, float top, float room)
    {
        var bottom = top;
        if (line.MarkedByYou)
        {
            if (line.MarkRowId == 0 || Characters is null || lastViewed is null)
            {
                return bottom;
            }

            ImGui.SetCursorScreenPos(new Vector2(left, bottom + UiMetrics.Px(4f)));
            if (Chrome.ActionPill("##gateTakeBack", TakeBackIcon, Strings.GateTakeBack, PillTone.Quiet, enabled: true, Strings.GateTakeBackTooltip, PillLayout.Row))
            {
                SetGateDone(line.MarkRowId, line.Label, done: false);
            }

            return MathF.Max(bottom, ImGui.GetItemRectMax().Y);
        }

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
            SetGateDone(line.MarkRowId, line.Label, done: true);
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

    /// <summary>
    /// "I've done this" (<paramref name="done"/>) or Take back: marks or unmarks the gate of <paramref name="rowId"/> for
    /// the character on view, with the floating Undo (8 s) that puts it back.
    /// </summary>
    private void SetGateDone(uint rowId, string label, bool done)
    {
        if (Characters is not { } book || lastViewed is not { } contentId || rowId == 0)
        {
            return;
        }

        book.Edit(CharacterSettingChange.GateDone(contentId, rowId, done));
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, done ? Strings.GateMarkedToastFormat : Strings.GateTakenBackToastFormat, label),
            () => book.Edit(CharacterSettingChange.GateDone(contentId, rowId, !done)));
    }

    /// <summary>
    /// Take back "I've done this" in the quest's "…" menu, while the player marked its gate passed for the character on
    /// view (as C1's "Use Tsukimichi's answer" sits there).
    /// </summary>
    private void DrawGateTakeBackMenuItem(QuestRecord quest)
    {
        if (Characters is not { } book || lastViewed is not { } contentId || !book.IsGateDone(contentId, quest.RowId))
        {
            return;
        }

        if (ImGui.MenuItem(Strings.GateTakeBackMenu))
        {
            var gate = model.Bundle?.Catalog.GameGateOf(quest.RowId)?.Gate ?? string.Empty;
            SetGateDone(quest.RowId, GateLabel(gate, Strings.GateTakeBackMenu), done: false);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.GateTakeBackTooltip);
        }
    }
}

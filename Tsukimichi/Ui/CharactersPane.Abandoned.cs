using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Runtime;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Abandoned (N)" section of the Characters dashboard (P10): the quests the viewed character dropped from the
/// journal and has not taken up again, newest first, from <see cref="Game.SessionState.Abandoned"/>. Each row: the
/// quest's state moon, its name (click reveals it in the Journal; the tooltip names the giver), "step 3 of 5 · 2 days
/// ago", and Flag, Teleport (with Lifestream) and Reveal. "Show in Journal" opens the Journal under the Abandoned filter.
/// Rows are rebuilt once per session version and once a minute (the ages tick).
/// </summary>
public sealed partial class CharactersPane
{
    private AbandonedRow[] abandonedRows = [];
    private string abandonedHeader = string.Format(CultureInfo.CurrentCulture, Strings.AbandonedHeaderFormat, 0);
    private int abandonedVersion = -1;
    private long abandonedMinute = -1;

    /// <summary>Map flags and Lifestream teleports for the Abandoned rows; set by the plugin. Null hides Flag and Teleport.</summary>
    public GameLinks? Links { get; set; }

    private void DrawAbandoned(UiState ui)
    {
        RefreshAbandoned();
        using var id = ImRaii.PushId("abandoned");
        var open = ImGui.CollapsingHeader(abandonedHeader + "###abandonedHeader", ImGuiTreeNodeFlags.DefaultOpen);
        if (!open)
        {
            return;
        }

        if (abandonedRows.Length == 0)
        {
            ImGui.TextDisabled(Strings.AbandonedNone);
            return;
        }

        if (ImGui.SmallButton(Strings.AbandonedShowInJournal))
        {
            ui.ShowAbandoned();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AbandonedShowInJournalTooltip);
        }

        using var table = ImRaii.Table("##abandoned", 4, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        ImGui.TableSetupColumn("##state", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
        ImGui.TableSetupColumn(Strings.CharactersColumnQuest, ImGuiTableColumnFlags.WidthFixed, UiMetrics.Px(260f));
        ImGui.TableSetupColumn(Strings.CharactersColumnStatus, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("##actions", ImGuiTableColumnFlags.WidthFixed);

        for (var i = 0; i < abandonedRows.Length; i++)
        {
            var row = abandonedRows[i];
            using var rowId = ImRaii.PushId(i);
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            MoonGlyph.DrawInline(row.State, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest));
            }

            ImGui.TableNextColumn();
            if (row.Quest is { } quest)
            {
                if (ImGui.Selectable(row.Name))
                {
                    Reveal(ui, quest);
                }

                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(row.Tooltip);
                }
            }
            else
            {
                ImGui.TextDisabled(row.Name);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(row.Detail);

            ImGui.TableNextColumn();
            if (row.Quest is { } target)
            {
                DrawAbandonedActions(ui, target);
            }
        }
    }

    /// <summary>Flag (when the giver has a map spot), Teleport (only with Lifestream) and Reveal, like the Nearby window's row.</summary>
    private void DrawAbandonedActions(UiState ui, QuestRecord quest)
    {
        if (Links is { } links)
        {
            using (ImRaii.Disabled(!links.CanFlagMap(quest)))
            {
                if (ImGui.SmallButton(Strings.AbandonedFlag))
                {
                    links.FlagMap(quest);
                }
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.AbandonedFlagTooltip);
            }

            if (links.TeleportAvailable)
            {
                ImGui.SameLine();
                var aetheryte = links.NearestAetheryte(quest);
                var busy = links.TeleportBusy;
                using (ImRaii.Disabled(aetheryte is null || busy))
                {
                    if (ImGui.SmallButton(Strings.AbandonedTeleport))
                    {
                        links.TeleportToGiver(quest);
                    }
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    UiMetrics.Tooltip(aetheryte is not { } target ? Strings.TeleportNoAetheryte
                        : busy ? Strings.TeleportBusy
                        : string.Format(CultureInfo.CurrentCulture, Strings.TeleportTooltipFormat, target.Name));
                }
            }

            ImGui.SameLine();
        }

        if (ImGui.SmallButton(Strings.AbandonedReveal))
        {
            Reveal(ui, quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.AbandonedRevealTooltip);
        }
    }

    /// <summary>Rebuilds the rows from the viewed character's ledger when the session changed or a minute passed.</summary>
    private void RefreshAbandoned()
    {
        var now = DateTime.UtcNow;
        var minute = now.Ticks / TimeSpan.TicksPerMinute;
        if (abandonedVersion == session.Version && abandonedMinute == minute)
        {
            return;
        }

        abandonedVersion = session.Version;
        abandonedMinute = minute;
        var ledger = session.Abandoned;
        var catalog = session.Bundle?.Catalog;
        var rows = new List<AbandonedRow>(ledger.Count);
        foreach (var entry in AbandonedLedger.Newest(ledger))
        {
            var quest = catalog?.GetByRowId(entry.RowId);
            var state = session.States.TryGetValue(entry.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            // Through the spoiler shield (T19): an abandoned main scenario quest far ahead reads as its placeholder.
            var name = quest is null ? Strings.MoonlitQuestPrefix + entry.RowId.ToString(CultureInfo.InvariantCulture) : session.Spoilers.DisplayName(quest);
            var when = Strings.AbandonedAtPrefix + entry.AbandonedUtc.ToLocalTime().ToString(Strings.DateTimeFormat, CultureInfo.CurrentCulture);
            var tooltip = quest?.Issuer is { } issuer && issuer.Name.Length > 0
                ? Strings.AbandonedGiverPrefix + issuer.Name + "\n" + when
                : when;
            rows.Add(new AbandonedRow(quest, name, state, AbandonedLedger.Describe(entry, now), tooltip));
        }

        abandonedRows = rows.ToArray();
        abandonedHeader = string.Format(CultureInfo.CurrentCulture, Strings.AbandonedHeaderFormat, abandonedRows.Length);
    }

    private sealed record AbandonedRow(QuestRecord? Quest, string Name, QuestState State, string Detail, string Tooltip);
}

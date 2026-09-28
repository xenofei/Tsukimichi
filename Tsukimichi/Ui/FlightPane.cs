using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Flight (feature plan V2-10): which aether current quests stand between the character and flying in a zone.
/// <see cref="DrawLeft"/> lists every flying zone under its expansion with a filling moon of attuned currents and the
/// quest currents done; <see cref="DrawMain"/> shows the selected zone's quest currents with their attunement, quest
/// state, next step and Flag / Teleport buttons, then one line pointing field currents at the Aether Compass. Field
/// currents are counted, never located.
/// <para>
/// The <see cref="FlightIndex"/> is built on the first draw through the factory the plugin hands in (a handful of
/// small sheets). Zone and row models are built once per catalog; attunement and quest counts, and every count string,
/// refresh only when <see cref="SessionState.Version"/> changes; the header strings when the version or the selected
/// zone changes. Nothing allocates per frame except tooltips on hover.
/// </para>
/// </summary>
public sealed class FlightPane
{
    private readonly SessionState session;
    private readonly RewardUnlockReader unlocks;
    private readonly GameLinks links;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly Func<uint> currentTerritory;
    private readonly Func<FlightIndex> buildIndex;

    private FlightIndex? index;
    private ZoneItem[] zones = [];
    private ExpansionGroup[] groups = [];
    private CatalogBundle? zonesBundle;
    private int countsVersion = -1;

    // Selected zone and the strings the centre column shows for it.
    private ZoneItem? selected;
    private int headerVersion = -1;
    private string header = string.Empty;
    private string fieldLine = string.Empty;

    public FlightPane(
        SessionState session,
        RewardUnlockReader unlocks,
        GameLinks links,
        ITextureProvider textures,
        IPluginLog log,
        Func<uint> currentTerritory,
        Func<FlightIndex> buildIndex)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.currentTerritory = currentTerritory ?? throw new ArgumentNullException(nameof(currentTerritory));
        this.buildIndex = buildIndex ?? throw new ArgumentNullException(nameof(buildIndex));
    }

    /// <summary>The zone index, built on first use; <see cref="FlightIndex.Empty"/> when the sheets could not be read.</summary>
    public FlightIndex Index
    {
        get
        {
            if (index is null)
            {
                try
                {
                    index = buildIndex();
                }
                catch (Exception ex)
                {
                    log.Warning(ex, "Flight index could not be built; the Flight view is empty");
                    index = FlightIndex.Empty;
                }
            }

            return index;
        }
    }

    /// <summary>Left column: flying zones under expansion headers, the current zone marked and selected on the first draw.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("flightLeft");
        Refresh(ui);

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (zones.Length == 0)
        {
            ImGui.TextWrapped(Strings.FlightNoData);
            ui.RecordSpan(UiRects.FlightZones, start, width);
            return;
        }

        var here = currentTerritory();
        var line = ImGui.GetTextLineHeight();
        var countWidth = ImGui.CalcTextSize("99/99").X;
        using (var table = ImRaii.Table("##flightZones", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            if (!table)
            {
                return;
            }

            ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, line * 1.4f);
            ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##count", ImGuiTableColumnFlags.WidthFixed, countWidth);

            foreach (var group in groups)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TableNextColumn();
                using (Theme.PushText(Theme.Dusk))
                {
                    ImGui.TextUnformatted(group.Header);
                }

                foreach (var zone in group.Zones)
                {
                    DrawZoneRow(ui, zone, zone.TerritoryId == here, line);
                }
            }
        }

        ui.RecordSpan(UiRects.FlightZones, start, width);
    }

    /// <summary>Centre column: the selected zone's header moon, its quest currents and the field-current line.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("flightMain");
        Refresh(ui);

        if (zones.Length == 0)
        {
            ImGui.TextWrapped(Strings.FlightNoData);
            return;
        }

        if (selected is not { } zone)
        {
            ImGui.TextDisabled(Strings.FlightNoZone);
            return;
        }

        DrawHeader(zone);
        if (!session.IsLive)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextWrapped(Strings.FlightOfflineHint);
        }

        ImGui.Spacing();
        DrawQuestTable(ui, zone);
        ImGui.Spacing();
        DrawFieldLine(zone);
    }

    private void DrawHeader(ZoneItem zone)
    {
        var radius = UiMetrics.HeaderMoonRadius;
        var box = radius * 2.4f;
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        var center = pos + new Vector2(box * 0.5f, box * 0.5f);
        if (zone.AllUnknown)
        {
            MoonGlyph.Draw(ImGui.GetWindowDrawList(), center, radius, QuestState.Unknown);
        }
        else
        {
            MoonGlyph.DrawFilling(ImGui.GetWindowDrawList(), center, radius, zone.Fraction);
        }

        ImGui.SameLine();
        var textHeight = ImGui.GetTextLineHeight();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (box - textHeight) * 0.5f));
        ImGui.TextUnformatted(header);
        if (zone.Complete)
        {
            ImGui.SameLine();
            using var moon = Theme.PushText(Theme.Moon);
            ImGui.TextUnformatted(Strings.FlightHeaderComplete);
        }
    }

    private void DrawQuestTable(UiState ui, ZoneItem zone)
    {
        ImGui.TextDisabled(Strings.FlightQuestCurrents);
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

        const ImGuiTableFlags Flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;
        var line = ImGui.GetTextLineHeight();
        var glyphColumn = MathF.Max(UiMetrics.InlineGlyphSize(line) * 2f, UiMetrics.Px(44f));
        var teleport = links.TeleportAvailable;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var padding = ImGui.GetStyle().FramePadding.X * 2f;
        var actionsWidth = ImGui.CalcTextSize(Strings.FlightFlag).X + padding;
        if (teleport)
        {
            actionsWidth += spacing + ImGui.CalcTextSize(Strings.FlightTeleport).X + padding;
        }

        using (var table = ImRaii.Table("##flightQuests", 5, Flags))
        {
            if (!table)
            {
                return;
            }

            ImGui.TableSetupColumn(Strings.FlightColumnAttuned, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
            ImGui.TableSetupColumn(Strings.FlightColumnQuest, ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn(Strings.FlightColumnState, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphColumn);
            ImGui.TableSetupColumn(Strings.FlightColumnNextStep, ImGuiTableColumnFlags.WidthStretch, 3f);
            ImGui.TableSetupColumn(Strings.FlightColumnActions, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, actionsWidth + spacing);
            ImGui.TableHeadersRow();

            var rows = zone.Rows;
            for (var i = 0; i < rows.Length; i++)
            {
                DrawQuestRow(ui, rows[i], i, line, teleport);
            }
        }

        ui.RecordSpan(UiRects.FlightTable, start, width);
    }

    private void DrawQuestRow(UiState ui, QuestRow row, int index, float line, bool teleport)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow();

        // Attuned.
        ImGui.TableNextColumn();
        MoonGlyph.DrawInline(row.AttunedGlyph, UiMetrics.InlineGlyphSize(line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(row.AttunedText);
        }

        // Quest: click shows it in the detail pane without leaving the tab.
        ImGui.TableNextColumn();
        if (row.Quest is { } quest)
        {
            if (ImGui.Selectable(row.QuestName, ui.SelectedRowId == quest.RowId))
            {
                ui.SelectedRowId = quest.RowId;
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.FlightQuestClickHint);
            }
        }
        else
        {
            ImGui.TextDisabled(row.QuestName);
        }

        // Quest state for the viewed character, and its first unmet requirement.
        ImGui.TableNextColumn();
        var state = QuestState.Unknown;
        string nextStep = string.Empty;
        if (session.States.TryGetValue(row.Current.QuestRowId, out var evaluation))
        {
            state = evaluation.State;
            nextStep = evaluation.NextStep?.Detail ?? string.Empty;
        }

        MoonGlyph.DrawInline(state, UiMetrics.InlineGlyphSize(line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.StateName(state));
        }

        ImGui.TableNextColumn();
        if (nextStep.Length > 0)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextUnformatted(nextStep);
        }

        // Flag the giver; teleport through Lifestream when it is loaded.
        ImGui.TableNextColumn();
        if (row.Quest is { } target)
        {
            using (ImRaii.Disabled(!links.CanFlagMap(target)))
            {
                if (ImGui.SmallButton(Strings.FlightFlag))
                {
                    links.FlagMap(target);
                }
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(Strings.FlightFlagTooltip);
            }

            if (teleport)
            {
                ImGui.SameLine();
                using (ImRaii.Disabled(!links.CanTeleport(target)))
                {
                    if (ImGui.SmallButton(Strings.FlightTeleport))
                    {
                        links.TeleportToGiver(target);
                    }
                }

                if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                {
                    var tip = links.TeleportBusy ? Strings.TeleportBusy
                        : links.NearestAetheryte(target) is { } aetheryte ? aetheryte.Name
                        : Strings.TeleportNoAetheryte;
                    UiMetrics.Tooltip(tip);
                }
            }
        }
    }

    private void DrawFieldLine(ZoneItem zone)
    {
        var icon = Index.AetherCompassIcon;
        if (icon != 0 && zone.Zone.FieldCurrentCount > 0)
        {
            var size = UiMetrics.DetailIconSize;
            var wrap = textures.GetFromGameIcon(new GameIconLookup(icon)).GetWrapOrEmpty();
            ImGui.Image(wrap.Handle, new Vector2(size, size));
            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (size - ImGui.GetTextLineHeight()) * 0.5f));
        }

        ImGui.TextWrapped(fieldLine);
        if (zone.Zone.FieldCurrentCount > 0 && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FlightFieldTooltip);
        }
    }

    private void DrawZoneRow(UiState ui, ZoneItem zone, bool here, float line)
    {
        using var id = ImRaii.PushId((int)zone.TerritoryId);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        var glyph = UiMetrics.InlineGlyphSize(line);
        if (zone.AllUnknown)
        {
            MoonGlyph.DrawInline(QuestState.Unknown, glyph);
        }
        else
        {
            MoonGlyph.DrawFillingInline(zone.Fraction, glyph);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(zone.TooltipText);
        }

        ImGui.TableNextColumn();
        var isSelected = ReferenceEquals(selected, zone);
        if (ImGui.Selectable(here ? zone.HereLabel : zone.Label, isSelected, ImGuiSelectableFlags.SpanAllColumns))
        {
            Select(ui, zone);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(here ? Strings.FlightCurrentZoneTooltip : zone.TooltipText);
        }

        ImGui.TableNextColumn();
        ImGui.TextDisabled(zone.CountText);
    }

    private void Select(UiState ui, ZoneItem zone)
    {
        ui.FlightTerritoryId = zone.TerritoryId;
        selected = zone;
        headerVersion = -1;
    }

    /// <summary>Index, zone models, counts, selection and header strings, each only when its inputs changed.</summary>
    private void Refresh(UiState ui)
    {
        var bundle = session.Bundle;
        if (!ReferenceEquals(zonesBundle, bundle) || zones.Length != Index.Zones.Count)
        {
            BuildZones(bundle);
        }

        if (countsVersion != session.Version)
        {
            RefreshCounts();
        }

        SyncSelection(ui);
        if (selected is { } zone && headerVersion != session.Version)
        {
            headerVersion = session.Version;
            header = zone.AllUnknown
                ? string.Format(CultureInfo.CurrentCulture, Strings.FlightHeaderUnknownFormat, zone.Name, zone.Zone.TotalCurrents)
                : string.Format(CultureInfo.CurrentCulture, Strings.FlightHeaderFormat, zone.Name, zone.Attuned, zone.Zone.TotalCurrents);
            var field = zone.Zone.FieldCurrentCount;
            fieldLine = field == 0 ? Strings.FlightFieldNone
                : zone.AllUnknown ? string.Format(CultureInfo.CurrentCulture, Strings.FlightFieldUnknownFormat, field)
                : zone.FieldAttuned == field ? string.Format(CultureInfo.CurrentCulture, Strings.FlightFieldAllFormat, field)
                : string.Format(CultureInfo.CurrentCulture, Strings.FlightFieldFormat, zone.FieldAttuned, field);
        }
    }

    /// <summary>
    /// Keeps <see cref="selected"/> in step with <see cref="UiState.FlightTerritoryId"/>: an outside change (a command,
    /// the tutorial) is honoured, and a null selection picks the zone the character stands in, else the first zone.
    /// </summary>
    private void SyncSelection(UiState ui)
    {
        if (zones.Length == 0)
        {
            selected = null;
            return;
        }

        if (ui.FlightTerritoryId is { } wanted)
        {
            if (selected?.TerritoryId == wanted)
            {
                return;
            }

            foreach (var zone in zones)
            {
                if (zone.TerritoryId == wanted)
                {
                    Select(ui, zone);
                    return;
                }
            }
        }

        var here = currentTerritory();
        foreach (var zone in zones)
        {
            if (zone.TerritoryId == here)
            {
                Select(ui, zone);
                return;
            }
        }

        Select(ui, zones[0]);
    }

    private void BuildZones(CatalogBundle? bundle)
    {
        var source = Index.Zones;
        var built = new ZoneItem[source.Count];
        var groupList = new List<ExpansionGroup>();
        var groupZones = new List<ZoneItem>();
        var groupExpansion = -1;
        for (var i = 0; i < built.Length; i++)
        {
            var zone = source[i];
            var rows = new QuestRow[zone.QuestCurrents.Count];
            for (var r = 0; r < rows.Length; r++)
            {
                var current = zone.QuestCurrents[r];
                rows[r] = new QuestRow(current, bundle?.Catalog.GetByRowId(current.QuestRowId));
            }

            built[i] = new ZoneItem(zone, rows);
            if (zone.Expansion != groupExpansion)
            {
                if (groupZones.Count > 0)
                {
                    groupList.Add(new ExpansionGroup(ExpansionName(bundle, (byte)groupExpansion), groupZones.ToArray()));
                    groupZones.Clear();
                }

                groupExpansion = zone.Expansion;
            }

            groupZones.Add(built[i]);
        }

        if (groupZones.Count > 0)
        {
            groupList.Add(new ExpansionGroup(ExpansionName(bundle, (byte)groupExpansion), groupZones.ToArray()));
        }

        zones = built;
        groups = groupList.ToArray();
        zonesBundle = bundle;
        selected = null;
        countsVersion = -1;
        headerVersion = -1;
    }

    private static string ExpansionName(CatalogBundle? bundle, byte expansion) =>
        bundle?.Names.Expansion(expansion) is { Length: > 0 } named ? named : Expansions.Name(expansion);

    /// <summary>Attunement per current and quest completion per row, once per session version; count strings follow.</summary>
    private void RefreshCounts()
    {
        var states = session.States;
        foreach (var zone in zones)
        {
            var attuned = 0;
            var unknown = 0;
            var questsDone = 0;
            foreach (var row in zone.Rows)
            {
                var flag = unlocks.IsAetherCurrentUnlocked(row.Current.AetherCurrentId);
                row.SetAttuned(flag);
                switch (flag)
                {
                    case true:
                        attuned++;
                        break;
                    case null:
                        unknown++;
                        break;
                }

                if (states.TryGetValue(row.Current.QuestRowId, out var evaluation) && evaluation.State == QuestState.Completed)
                {
                    questsDone++;
                }
            }

            var fieldAttuned = 0;
            foreach (var fieldId in zone.Zone.FieldCurrentIds)
            {
                switch (unlocks.IsAetherCurrentUnlocked(fieldId))
                {
                    case true:
                        attuned++;
                        fieldAttuned++;
                        break;
                    case null:
                        unknown++;
                        break;
                }
            }

            zone.SetCounts(attuned, fieldAttuned, unknown, questsDone);
        }

        countsVersion = session.Version;
        headerVersion = -1;
    }

    /// <summary>Zones of one expansion under a header, in index order.</summary>
    private sealed class ExpansionGroup(string header, ZoneItem[] zones)
    {
        public string Header { get; } = header;
        public ZoneItem[] Zones { get; } = zones;
    }

    /// <summary>One flying zone with its quest rows; counts and their strings change once per session version.</summary>
    private sealed class ZoneItem
    {
        public ZoneItem(FlightZone zone, QuestRow[] rows)
        {
            Zone = zone;
            Rows = rows;
            Label = zone.Name;
            HereLabel = Strings.FlightCurrentZoneMarker + zone.Name;
            CountText = string.Format(CultureInfo.InvariantCulture, Strings.FlightZoneCountFormat, 0, rows.Length);
            TooltipText = string.Empty;
        }

        public FlightZone Zone { get; }
        public QuestRow[] Rows { get; }
        public uint TerritoryId => Zone.TerritoryId;
        public string Name => Zone.Name;
        public string Label { get; }
        public string HereLabel { get; }
        public string CountText { get; private set; }
        public string TooltipText { get; private set; }

        /// <summary>Attuned currents (quest and field) over every current in the zone; unknown counts as not attuned.</summary>
        public int Attuned { get; private set; }

        public int FieldAttuned { get; private set; }
        public float Fraction { get; private set; }
        public bool Complete { get; private set; }

        /// <summary>True when no current is readable (a stored snapshot, logged out), so the moon is veiled instead of new.</summary>
        public bool AllUnknown { get; private set; } = true;

        public void SetCounts(int attuned, int fieldAttuned, int unknown, int questsDone)
        {
            var total = Zone.TotalCurrents;
            Attuned = attuned;
            FieldAttuned = fieldAttuned;
            Fraction = total > 0 ? (float)attuned / total : 0f;
            AllUnknown = total > 0 && unknown == total;
            Complete = total > 0 && attuned == total;
            CountText = string.Format(CultureInfo.InvariantCulture, Strings.FlightZoneCountFormat, questsDone, Rows.Length);
            TooltipText = AllUnknown
                ? string.Format(CultureInfo.CurrentCulture, Strings.FlightZoneTooltipUnknownFormat, total, questsDone, Rows.Length)
                : string.Format(CultureInfo.CurrentCulture, Strings.FlightZoneTooltipFormat, attuned, total, questsDone, Rows.Length);
        }
    }

    /// <summary>One quest current with its quest record; only the attunement changes after construction.</summary>
    private sealed class QuestRow(FlightCurrent current, QuestRecord? quest)
    {
        public FlightCurrent Current { get; } = current;
        public QuestRecord? Quest { get; } = quest;
        public string QuestName { get; } = quest?.Name ?? Strings.FlightQuestPrefix + current.QuestRowId.ToString(CultureInfo.InvariantCulture);
        public QuestState AttunedGlyph { get; private set; } = QuestState.Unknown;
        public string AttunedText { get; private set; } = Strings.FlightAttunedUnknown;

        public void SetAttuned(bool? attuned)
        {
            (AttunedGlyph, AttunedText) = attuned switch
            {
                true => (QuestState.Completed, Strings.FlightAttunedYes),
                false => (QuestState.Blocked, Strings.FlightAttunedNo),
                null => (QuestState.Unknown, Strings.FlightAttunedUnknown),
            };
        }
    }
}

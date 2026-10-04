using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
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
/// The Moon Road look (feature plan v4 V5, proposal §7.7): at Flair Full the selected zone's loading-screen art is a
/// banner over the centre column (only that one texture is asked for, through the shared cache, with a drawn night sky
/// while it loads), the zone name on it in the Title role over a scrim (a solid band under high contrast) and the counts
/// in the Numeral role. At Full and Quiet each zone in the list carries a bead ring, one segment per quest current, lit
/// when done, whose new segments light one after another at Full (never under Reduce motion, and only as progress of
/// the same character: <see cref="BeadRingMath.ShouldSequence"/>); the expansion headers and the currents heading are open
/// sections. Plain keeps the look before 1.4.
/// </para>
/// <para>
/// The <see cref="FlightIndex"/> (a handful of small sheets) is built on a worker when the plugin loads
/// (<see cref="Game.IndexWarmer"/>): the pane reads it through the function the plugin hands in, null while it builds,
/// and says it is reading the zones meanwhile, so its first open never builds it. Zone and row models are built once per catalog; attunement and quest counts, and every count string,
/// refresh when <see cref="SessionState.Version"/> changes and, because attuning a current changes nothing in the
/// snapshot, also when the tab becomes visible, when the territory changes and every
/// <see cref="LiveAttunementRefreshMs"/> while a live character is viewed (the memoized attunements are dropped through
/// <see cref="RewardUnlockReader.InvalidateAetherCurrents"/> first); the header strings when the counts or the selected
/// zone change. Nothing allocates per frame except tooltips on hover.
/// </para>
/// </summary>
public sealed class FlightPane
{
    /// <summary>How often the live character's attunements are re-read while the tab is visible.</summary>
    public const int LiveAttunementRefreshMs = 5000;

    /// <summary>Motion tag of the zones' bead rings ("BEAD"); the territory id is the low half.</summary>
    private const uint BeadTag = 0x4245_4144;

    /// <summary>The widest count the zone list reserves room for.</summary>
    private const string CountSample = "99 left";

    private readonly SessionState session;
    private readonly RewardUnlockReader unlocks;
    private readonly GameLinks links;
    private readonly ITextureProvider textures;
    private readonly IPluginLog log;
    private readonly Func<uint> currentTerritory;
    private readonly Func<FlightIndex?> readIndex;

    private FlightIndex? index;
    private ZoneItem[] zones = [];
    private ExpansionGroup[] groups = [];
    private CatalogBundle? zonesBundle;
    private int countsVersion = -1;

    // Attunement re-reads: the frame the tab was last drawn in (a gap means it just became visible), the territory the
    // counts were read in and when they were last re-read for a live character.
    private int lastDrawFrame = -2;
    private uint countsTerritory;
    private long lastLiveRefreshTick;

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
        Func<FlightIndex?> readIndex)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.unlocks = unlocks ?? throw new ArgumentNullException(nameof(unlocks));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.textures = textures ?? throw new ArgumentNullException(nameof(textures));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.currentTerritory = currentTerritory ?? throw new ArgumentNullException(nameof(currentTerritory));
        this.readIndex = readIndex ?? throw new ArgumentNullException(nameof(readIndex));
    }

    /// <summary>
    /// The zone index once it has landed (it is warmed at load, feature plan v6 A11); <see cref="FlightIndex.Empty"/>
    /// when the sheets could not be read. Read only after <see cref="IndexReady"/>.
    /// </summary>
    public FlightIndex Index => index ?? FlightIndex.Empty;

    /// <summary>Whether the zone index has landed; the pane says it is reading the zones until then.</summary>
    private bool IndexReady()
    {
        if (index is not null)
        {
            return true;
        }

        try
        {
            index = readIndex();
        }
        catch (Exception ex)
        {
            log.Warning(ex, "Flight index could not be built; the Flight view is empty");
            index = FlightIndex.Empty;
        }

        return index is not null;
    }

    /// <summary>Left column: flying zones under expansion headers, the current zone marked and selected on the first draw.</summary>
    public void DrawLeft(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("flightLeft");
        if (!IndexReady())
        {
            ImGui.TextDisabled(Strings.FlightLoading);
            return;
        }

        Refresh(ui);

        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (zones.Length == 0)
        {
            ImGui.TextWrapped(Strings.FlightNoData);
            ui.RecordSpan(UiRects.FlightZones, start, width);
            return;
        }

        // Every A Realm Reborn field zone answers the one A Realm Reborn entry.
        var here = Index.ZoneFor(currentTerritory());
        var line = ImGui.GetTextLineHeight();
        var art = Theme.Sectioned;
        var countWidth = CountWidth(art);

        // The ring column holds the zone rows' bead rings and, at Full and Quiet, the expansion marks (U6: 32 px, hi-res,
        // centred on their heading); both centre in it.
        var glyph = UiMetrics.HaloBoxSize(line);
        var mark = UiMetrics.ExpansionMarkSize;
        var column = art ? FlightGeometry.ColumnWidth(mark, glyph) : glyph;
        using (var table = ImRaii.Table("##flightZones", 3, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX))
        {
            if (!table)
            {
                return;
            }

            ImGui.TableSetupColumn("##moon", ImGuiTableColumnFlags.WidthFixed, column);
            ImGui.TableSetupColumn("##name", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("##count", ImGuiTableColumnFlags.WidthFixed, countWidth);

            var first = true;
            foreach (var group in groups)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                if (art)
                {
                    // The expansion's mark beside an open-section heading (proposal §7.7), its centre on the title's.
                    var gap = first ? 0f : UiMetrics.Px(FlightGeometry.GroupGapLogical);
                    var (headingHeight, headingMid) = SectionHeading.Measure(group.Header);
                    var row = FlightGeometry.HeadingRow(group.Icon != 0 ? mark : 0f, headingHeight, headingMid, gap);
                    var cellY = ImGui.GetCursorPosY();
                    if (group.Icon != 0)
                    {
                        ImGui.SetCursorPos(new Vector2(ImGui.GetCursorPosX() + FlightGeometry.Centred(column, mark), cellY + row.MarkTop));
                        var min = ImGui.GetCursorScreenPos();
                        ImGui.Dummy(new Vector2(mark, mark));
                        Orbit.DrawIcon(ImGui.GetWindowDrawList(), textures, NodeIcon.Game(group.Icon), min, min + new Vector2(mark, mark));
                    }

                    ImGui.TableNextColumn();
                    ImGui.SetCursorPosY(ImGui.GetCursorPosY() + row.HeadingTop);
                    SectionHeading.Draw(group.Header, sigil: group.Icon == 0);
                }
                else
                {
                    ImGui.TableNextColumn();
                    using (Theme.PushText(Theme.Surface.TextTertiary))
                    {
                        ImGui.TextUnformatted(group.Header);
                    }
                }

                foreach (var zone in group.Zones)
                {
                    DrawZoneRow(ui, zone, ReferenceEquals(zone.Zone, here), glyph, column, art);
                }

                first = false;
            }
        }

        ui.RecordSpan(UiRects.FlightZones, start, width);
    }

    /// <summary>Centre column: the selected zone's header moon, its quest currents and the field-current line.</summary>
    public void DrawMain(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        using var id = ImRaii.PushId("flightMain");
        if (!IndexReady())
        {
            ImGui.TextDisabled(Strings.FlightLoading);
            return;
        }

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

        if (Theme.FlairSetting == Flair.Full && zone.Zone.LoadingImagePath is not null)
        {
            DrawBanner(zone);
        }
        else
        {
            DrawHeader(zone);
        }

        if (!session.IsLive)
        {
            using var dusk = Theme.PushText(Theme.Surface.TextTertiary);
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
            Marks.Draw(ImGui.GetWindowDrawList(), center, box, Mark.Unknown);
        }
        else
        {
            MoonGlyph.DrawHalo(ImGui.GetWindowDrawList(), center, radius, zone.Fraction);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(zone.AllUnknown ? Strings.StateTooltip(QuestState.Unknown) : zone.TooltipText);
        }

        ImGui.SameLine();
        var line = ImGui.GetTextLineHeight();
        var spacing = ImGui.GetStyle().ItemSpacing;

        // The header is a band of fixed height (feature plan v6, U4): the moon's box, or two lines of text, whichever
        // is taller, so "Complete" arriving during play, or a count growing a digit, never moves the table below. The
        // text is centred in the band on the lines it needs now.
        var band = MathF.Max(box, (2f * line) + spacing.Y);
        var room = Chrome.RoomX();
        var oneLine = ImGui.CalcTextSize(header).X + (zone.Complete ? spacing.X + ImGui.CalcTextSize(Strings.FlightHeaderComplete).X : 0f) <= room;
        var textBlock = oneLine ? line : (2f * line) + spacing.Y;
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, pos.Y + MathF.Max(0f, (band - textBlock) * 0.5f)));

        // The zone and its counts wrap between words beside the moon; "Complete" follows on the line when it fits and
        // starts its own otherwise (UI audit §3).
        TextFlow.Wrapped(header, room);
        if (zone.Complete)
        {
            Chrome.SameLineOrWrap(ImGui.CalcTextSize(Strings.FlightHeaderComplete).X);
            using var moon = Theme.PushText(Theme.AccentDim);
            ImGui.TextUnformatted(Strings.FlightHeaderComplete);
        }

        // Only a pane too narrow for two lines grows the band.
        var below = MathF.Max(pos.Y + band, ImGui.GetItemRectMax().Y);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, below));
        ImGui.Dummy(Vector2.Zero);
    }

    // The quest table's columns, in display order (feature plan v4 L6): the state moon hides first, the actions and
    // the status never; the status keeps its state word.
    private const int ColumnAttuned = 0;
    private const int ColumnQuest = 1;
    private const int ColumnState = 2;
    private const int ColumnStatus = 3;
    private const int ColumnActions = 4;
    private const string RowMenuId = "##flightRowMenu";
    private readonly ColumnFit columns = new(5);

    private void DrawQuestTable(UiState ui, ZoneItem zone)
    {
        SectionHeading.Draw(Strings.FlightQuestCurrents);
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;

        const ImGuiTableFlags Flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingFixedFit;
        var line = ImGui.GetTextLineHeight();
        var glyphColumn = MathF.Max(UiMetrics.InlineGlyphSize(line) * 2f, UiMetrics.Px(44f));
        var style = ImGui.GetStyle();
        var padding = style.FramePadding.X * 2f;

        // Flag, Teleport and Walk fold into one "…" under the fold width.
        var fold = PaneFit.FoldActions(width / UiMetrics.Scale);
        var actionsWidth = fold ? MoreSize(line) : TravelControls.FlagWidth(Strings.FlightFlag) + TravelControls.ButtonsWidth(links, Strings.FlightTeleport);

        // A fixed column is at least as wide as its header, which ImGui would widen it to anyway.
        Span<ColumnSpec> specs = stackalloc ColumnSpec[5];
        PaneFit.FlightColumns(glyphColumn, UiMetrics.Px(LayoutBudgets.RowNameMinLogical), StatusMin(zone.Rows), actionsWidth, specs);
        ColumnFit.FitHeader(specs, ColumnAttuned, Strings.FlightColumnAttuned);
        ColumnFit.FitHeader(specs, ColumnState, Strings.FlightColumnState);
        ColumnFit.FitHeader(specs, ColumnActions, Strings.FlightColumnActions);
        columns.Plan(width, specs);
        using (var table = columns.Begin("##flightQuests", Flags))
        {
            if (!table.Success)
            {
                return;
            }

            columns.Setup(ColumnAttuned, Strings.FlightColumnAttuned);
            columns.Setup(ColumnQuest, Strings.FlightColumnQuest);
            columns.Setup(ColumnState, Strings.FlightColumnState);
            columns.Setup(ColumnStatus, Strings.FlightColumnStatus);
            columns.Setup(ColumnActions, Strings.FlightColumnActions);
            ImGui.TableHeadersRow();

            var rows = zone.Rows;
            for (var i = 0; i < rows.Length; i++)
            {
                DrawQuestRow(ui, rows[i], i, line, fold);
            }
        }

        ui.RecordSpan(UiRects.FlightTable, start, width);
    }

    /// <summary>The status column's least width: the rows' widest state word and a little reason, so no state word is cut.</summary>
    private static float StatusMin(QuestRow[] rows)
    {
        var stateWord = 0f;
        foreach (var row in rows)
        {
            stateWord = MathF.Max(stateWord, ImGui.CalcTextSize(row.StatusText.AsSpan(0, TableGeometry.StateWordLength(row.StatusText))).X);
        }

        return stateWord + UiMetrics.Px(24f);
    }

    /// <summary>Moves to the column's cell when the column shows; false when it is hidden.</summary>
    private bool NextColumn(int column) => columns.Next(column);

    /// <summary>The "…" button's side in a table row.</summary>
    private static float MoreSize(float line) => MathF.Min(UiMetrics.MinTarget, MathF.Max(line, UiMetrics.RowIconSize));

    private void DrawQuestRow(UiState ui, QuestRow row, int index, float line, bool fold)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow();

        // Attuned.
        if (NextColumn(ColumnAttuned))
        {
            Marks.DrawInline(row.AttunedGlyph, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.AttunedText);
            }
        }

        // Quest: click shows it in the detail pane without leaving the tab. A long name ends in an ellipsis.
        if (NextColumn(ColumnQuest))
        {
            if (row.Quest is { } quest)
            {
                if (Chrome.EllipsisSelectable(row.QuestName, ui.SelectedRowId == quest.RowId, 0f, out var cut))
                {
                    ui.SelectedRowId = quest.RowId;
                }

                if (ImGui.IsItemHovered())
                {
                    if (cut)
                    {
                        UiMetrics.Tooltip(row.QuestName, Strings.FlightQuestClickHint);
                    }
                    else
                    {
                        UiMetrics.Tooltip(Strings.FlightQuestClickHint);
                    }
                }
            }
            else
            {
                Chrome.FitText(row.QuestName, ImGui.GetColorU32(ImGuiCol.TextDisabled));
            }
        }

        // Quest state for the viewed character, and its status line (state word first, then the decisive blocker),
        // both stored on the row by RefreshCounts once per session version.
        var state = row.State;
        if (NextColumn(ColumnState))
        {
            MoonGlyph.DrawInline(state, UiMetrics.InlineGlyphSize(line));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.StateTooltip(state, row.Evaluation, row.Quest, session.Names, session.States);
            }
        }

        // The state word is never cut; the reason ends in an ellipsis with the whole line on hover.
        if (NextColumn(ColumnStatus) && row.StatusText.Length > 0)
        {
            var s = Theme.Surface;
            Chrome.StatusText(row.StatusText, ImGui.GetContentRegionAvail().X, s.Text, s.TextSecondary);
        }

        // Flag the giver, teleport through Lifestream (greyed, naming it, without), walk with vnavmesh. Folded into "…"
        // in a narrow pane.
        if (!NextColumn(ColumnActions) || row.Quest is not { } target)
        {
            return;
        }

        if (fold)
        {
            DrawRowMenu(target, line);
            return;
        }

        if (TravelControls.FlagButton(Strings.FlightFlag, links.CanFlagMap(target)))
        {
            links.FlagMap(target);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.FlightFlagTooltip);
        }

        TravelControls.Buttons(links, target, Strings.FlightTeleport);
    }

    /// <summary>Flag, Teleport, Walk and Go to giver folded into one "…" button and its menu (a narrow pane).</summary>
    private void DrawRowMenu(QuestRecord target, float line)
    {
        Keyboard.MoreButton("##more", RowMenuId, ImGui.GetCursorScreenPos(), MoreSize(line));
        using var popup = ImRaii.Popup(RowMenuId);
        if (!popup)
        {
            return;
        }

        // Opened from the centre column (own font scale 1), so the menu scales itself.
        UiMetrics.ApplyFontScale();
        if (ImGui.MenuItem(Strings.FlightFlag, enabled: links.CanFlagMap(target)))
        {
            links.FlagMap(target);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.FlightFlagTooltip);
        }

        TravelControls.MenuItems(links, target, Strings.FlightTeleport);
    }

    private void DrawFieldLine(ZoneItem zone)
    {
        var icon = Index.AetherCompassIcon;
        if (icon != 0 && zone.Zone.FieldCurrentCount > 0)
        {
            var size = UiMetrics.DetailIconSize;
            GameIcon.Draw(textures, icon, size);
            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, (size - ImGui.GetTextLineHeight()) * 0.5f));
        }

        ImGui.TextWrapped(fieldLine);
        if (zone.Zone.FieldCurrentCount > 0 && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.FlightFieldTooltip);
        }
    }

    /// <summary>
    /// One zone: its bead ring (<paramref name="glyph"/> square, centred in the <paramref name="column"/>-wide ring
    /// column), then the name and the count, the row as tall as the ring with both centred on it, so the selection
    /// wash and the gold edge cover the whole row.
    /// </summary>
    private void DrawZoneRow(UiState ui, ZoneItem zone, bool here, float glyph, float column, bool art)
    {
        using var id = ImRaii.PushId((int)zone.TerritoryId);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + FlightGeometry.Centred(column, glyph));
        if (zone.AllUnknown)
        {
            Marks.DrawInline(Mark.Unknown, glyph);
        }
        else if (art)
        {
            DrawBeads(zone, glyph);
        }
        else
        {
            MoonGlyph.DrawHaloInline(zone.Fraction, glyph);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(zone.TooltipText);
        }

        ImGui.TableNextColumn();
        var isSelected = ReferenceEquals(selected, zone);
        bool clicked;
        using (ImRaii.PushStyle(ImGuiStyleVar.SelectableTextAlign, new Vector2(0f, 0.5f)))
        {
            clicked = ImGui.Selectable(here ? zone.HereLabel : zone.Label, isSelected, ImGuiSelectableFlags.SpanAllColumns, new Vector2(0f, glyph));
        }

        if (clicked)
        {
            Select(ui, zone);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(here ? Strings.FlightCurrentZoneTooltip : zone.TooltipText);
        }

        if (art && isSelected)
        {
            // The selected zone's gold edge (the mockup's inset rule), at the row's left. The span-all-columns item
            // starts in the ring column, outside this column's clip, so the edge goes on the table's background
            // channel (over the selection fill, under every column's content).
            var min = ImGui.GetItemRectMin();
            var max = ImGui.GetItemRectMax();
            ImGuiP.TablePushBackgroundChannel();
            ImGui.GetWindowDrawList().AddRectFilled(min, new Vector2(min.X + MathF.Max(2f, UiMetrics.Px(2f)), max.Y), Theme.GoldU32);
            ImGuiP.TablePopBackgroundChannel();
        }

        // The count, centred on the row in the face it draws in.
        ImGui.TableNextColumn();
        using var role = art ? Typography.Numeral(zone.CountText) : default;
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + MathF.Max(0f, MathF.Round((glyph - ImGui.GetTextLineHeight()) * 0.5f)));
        ImGui.TextDisabled(zone.CountText);
    }

    /// <summary>The count column's width: the widest count, in the Numeral role at Full and Quiet.</summary>
    private static float CountWidth(bool art)
    {
        if (!art)
        {
            return ImGui.CalcTextSize(CountSample).X;
        }

        using var numeral = Typography.Numeral(CountSample);
        return ImGui.CalcTextSize(CountSample).X;
    }

    /// <summary>
    /// The zone's bead ring in a <paramref name="box"/> square item: one segment per quest current, the done ones lit;
    /// segments that just became done light one after another at Full flair (<see cref="BeadRingMath.Shown"/>).
    /// </summary>
    private static void DrawBeads(ZoneItem zone, float box)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(box, box));
        var progress = Theme.FlairMotion ? Motion.Pulse(zone.BeadKey, BeadRingMath.SequenceSeconds(zone.LitFrom, zone.QuestsDone)) : -1f;
        var (lit, head) = BeadRingMath.Shown(zone.LitFrom, zone.QuestsDone, progress);
        var center = min + new Vector2(box * 0.5f);
        BeadRing.Draw(ImGui.GetWindowDrawList(), center, box * 0.40f, zone.Rows.Length, lit, MathF.Max(1.5f, box * 0.09f), head);
    }

    /// <summary>
    /// The zone banner (Flair Full): the zone's loading-screen art cover-cropped to min(160, 0.35 × width), a scrim, the
    /// expansion's ring, the zone's name in the Title role and the counts in the Numeral role. Only the selected zone's
    /// texture is asked for; while it loads (or if it cannot) a drawn night sky stands in. One item, hovered for the
    /// zone's counts. A pane too narrow for a banner that holds its text (padding, the title line and the counts line)
    /// gets the header the other flair levels draw. Under the high-contrast palette (whose flair is capped at Quiet, so
    /// the banner follows the Flair setting) a solid band behind the text replaces the scrims, as on the detail hero.
    /// </summary>
    private void DrawBanner(ZoneItem zone)
    {
        var width = ImGui.GetContentRegionAvail().X;
        var height = MathF.Round(UiMetrics.Px(PaneGrid.BannerHeight(width / UiMetrics.Scale)));
        var pad = UiMetrics.Px(12f);
        var body = ImGui.GetTextLineHeight();
        float titleLine;
        using (Typography.Title(zone.Name))
        {
            titleLine = ImGui.GetTextLineHeight();
        }

        if (height < (2f * pad) + titleLine + body)
        {
            DrawHeader(zone);
            return;
        }

        var min = ImGui.GetCursorScreenPos();
        var max = min + new Vector2(width, height);
        ImGui.Dummy(new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        if (!ImGui.IsItemVisible())
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var highContrast = Theme.Glyphs.HighContrast;
        if (textures.GetFromGame(zone.Zone.LoadingImagePath!).TryGetWrap(out var wrap, out _))
        {
            Chrome.ImageCoverAt(dl, wrap.Handle, min, max, wrap.Size);
        }
        else
        {
            // Loading, or no such file: the drawn night sky (NightTop over TideDeep).
            dl.AddRectFilledMultiColor(min, max, Theme.TopU32, Theme.TopU32, Theme.U32(Theme.Surface.CoolDeep), Theme.U32(Theme.Surface.CoolDeep));
        }

        var s = Theme.Surface;
        var countsY = max.Y - pad - body;
        var titleY = countsY - titleLine;
        if (highContrast)
        {
            // A solid band behind the text (proposal §10.2).
            dl.AddRectFilled(new Vector2(min.X, MathF.Max(min.Y, titleY - (pad * 0.5f))), max, Theme.WithAlpha(s.Window, 0.97f));
        }
        else
        {
            // Scrims: the bottom one under the text, the side one keeps the title legible over bright art (proposal §3,
            // §10.1).
            Chrome.Scrim(dl, new Vector2(min.X, min.Y + (height * 0.35f)), max, 0f, 0.94f);
            var side = Theme.WithAlpha(s.Deep, 0.55f);
            var clear = Theme.WithAlpha(s.Deep, 0f);
            dl.AddRectFilledMultiColor(min, new Vector2(min.X + (width * 0.6f), max.Y), side, clear, clear, side);
        }

        dl.AddRect(min, max, highContrast ? Theme.U32(Theme.Surface.StrongLine) : Theme.U32(s.Line), 0f, ImDrawFlags.None, UiMetrics.Hairline);
        var x = min.X + pad;
        var icon = zone.Zone.ExpansionIcon;
        if (icon != 0)
        {
            // The expansion's mark at 44 px (U6), centred on the title and counts lines and kept inside the banner's
            // padding, on a soft Deep disc that keeps it legible over bright art (the high-contrast band does that there).
            var size = MathF.Min(UiMetrics.BannerExpansionMarkSize, height - (2f * pad));
            var centerY = titleY + ((titleLine + body) * 0.5f);
            var iconMin = new Vector2(x, MathF.Round(Math.Clamp(centerY - (size * 0.5f), min.Y + pad, max.Y - pad - size)));
            if (!highContrast)
            {
                dl.AddCircleFilled(iconMin + new Vector2(size * 0.5f), size * 0.56f, Theme.WithAlpha(s.Deep, 0.55f));
            }

            Orbit.DrawIcon(dl, textures, NodeIcon.Game(icon), iconMin, iconMin + new Vector2(size, size));
            x += size + UiMetrics.Px(12f);
        }

        var room = MathF.Max(0f, max.X - pad - x);
        bool nameCut;
        using (Typography.Title(zone.Name))
        {
            nameCut = Chrome.EllipsisTextAt(dl, new Vector2(x, titleY), room, zone.Name, Theme.U32(s.Text));
        }

        // "Quest currents 2 left · Field currents all done": the labels in the body font, the counts in the Numeral role.
        var at = new Vector2(x, countsY);
        var right = max.X - pad;
        var labelInk = Theme.U32(s.TextSecondary);
        var numberInk = Theme.U32(s.Text);
        at.X = CountPart(dl, at, right, Strings.FlightQuestCurrents, zone.BannerCountText, labelInk, numberInk);
        if (zone.FieldCountText.Length > 0 && at.X < right)
        {
            var dot = Strings.StateReasonSeparator;
            var dotWidth = ImGui.CalcTextSize(dot).X;
            if (at.X + dotWidth < right)
            {
                dl.AddText(at, labelInk, dot);
                at.X += dotWidth;
                CountPart(dl, at, right, Strings.FlightFieldCurrents, zone.FieldCountText, labelInk, numberInk);
            }
        }

        if (hovered)
        {
            var counts = zone.AllUnknown ? Strings.StateTooltip(QuestState.Unknown) : zone.TooltipText;
            if (nameCut)
            {
                UiMetrics.Tooltip(zone.Name, counts);
            }
            else
            {
                UiMetrics.Tooltip(counts);
            }
        }
    }

    /// <summary>
    /// One "label count" pair of the banner's counts line at <paramref name="at"/>, stopping at <paramref name="right"/>
    /// (a label with no room is ellipsised and its count dropped). Returns where the next part starts.
    /// </summary>
    private static float CountPart(ImDrawListPtr dl, Vector2 at, float right, string label, string count, uint labelInk, uint numberInk)
    {
        var body = ImGui.GetTextLineHeight();
        var gap = ImGui.CalcTextSize(" ").X;
        var labelWidth = ImGui.CalcTextSize(label).X;
        float countWidth;
        float countLine;
        using (Typography.Numeral(count))
        {
            countWidth = ImGui.CalcTextSize(count).X;
            countLine = ImGui.GetTextLineHeight();
        }

        if (at.X + labelWidth + gap + countWidth > right)
        {
            Chrome.EllipsisTextAt(dl, at, MathF.Max(0f, right - at.X), label, labelInk, labelWidth);
            return right;
        }

        dl.AddText(at, labelInk, label);

        // The numeral centred on the body line (its face may be shorter or taller).
        var countAt = new Vector2(at.X + labelWidth + gap, at.Y + MathF.Round((body - countLine) * 0.5f));
        using (Typography.Numeral(count))
        {
            dl.AddText(countAt, numberInk, count);
        }

        return countAt.X + countWidth;
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

        if (AttunementsMayHaveChanged())
        {
            unlocks.InvalidateAetherCurrents();
            countsVersion = -1;
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
                : zone.Attuned >= zone.Zone.TotalCurrents ? string.Format(CultureInfo.CurrentCulture, Strings.FlightHeaderAllFormat, zone.Name, zone.Zone.TotalCurrents)
                : zone.Zone.TotalCurrents - zone.Attuned == 1 ? string.Format(CultureInfo.CurrentCulture, Strings.FlightHeaderOneFormat, zone.Name)
                : string.Format(CultureInfo.CurrentCulture, Strings.FlightHeaderFormat, zone.Name, zone.Zone.TotalCurrents - zone.Attuned);
            var field = zone.Zone.FieldCurrentCount;
            fieldLine = field == 0 ? Strings.FlightFieldNone
                : zone.AllUnknown ? string.Format(CultureInfo.CurrentCulture, Strings.FlightFieldUnknownFormat, field)
                : zone.FieldAttuned == field ? string.Format(CultureInfo.CurrentCulture, Strings.FlightFieldAllFormat, field)
                : string.Format(CultureInfo.CurrentCulture, Strings.FlightFieldFormat, field - zone.FieldAttuned);
        }
    }

    /// <summary>
    /// Once per frame, for a live character: true when the tab was not drawn last frame (it just became visible), when
    /// the territory differs from the one the counts were read in, or when <see cref="LiveAttunementRefreshMs"/> have
    /// passed since the last re-read. Both columns call <see cref="Refresh"/>, so the frame check keeps it to one answer.
    /// </summary>
    private bool AttunementsMayHaveChanged()
    {
        var frame = ImGui.GetFrameCount();
        if (frame == lastDrawFrame)
        {
            return false;
        }

        var becameVisible = frame != lastDrawFrame + 1;
        lastDrawFrame = frame;
        if (!session.IsLive)
        {
            return false;
        }

        var territory = currentTerritory();
        var now = Environment.TickCount64;
        if (!becameVisible && territory == countsTerritory && now - lastLiveRefreshTick < LiveAttunementRefreshMs)
        {
            return false;
        }

        countsTerritory = territory;
        lastLiveRefreshTick = now;
        return true;
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

            // Any A Realm Reborn field zone asks for the one A Realm Reborn entry.
            var wantedZone = Index.ZoneFor(wanted);
            foreach (var zone in zones)
            {
                if (ReferenceEquals(zone.Zone, wantedZone))
                {
                    Select(ui, zone);
                    return;
                }
            }
        }

        var here = Index.ZoneFor(currentTerritory());
        foreach (var zone in zones)
        {
            if (ReferenceEquals(zone.Zone, here))
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
                    groupList.Add(new ExpansionGroup(ExpansionName(bundle, (byte)groupExpansion), groupZones.ToArray(), groupZones[0].Zone.ExpansionIcon));
                    groupZones.Clear();
                }

                groupExpansion = zone.Expansion;
            }

            groupZones.Add(built[i]);
        }

        if (groupZones.Count > 0)
        {
            groupList.Add(new ExpansionGroup(ExpansionName(bundle, (byte)groupExpansion), groupZones.ToArray(), groupZones[0].Zone.ExpansionIcon));
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

    /// <summary>
    /// Attunement per current, quest state and status line per row, once per session version; count strings follow. A
    /// quest current counts as done by its attunement flag, or by its awarding quest's completion only when the flag
    /// cannot be read (<see cref="FlightProgress.QuestCurrentDone"/>).
    /// </summary>
    private void RefreshCounts()
    {
        var states = session.States;
        var viewed = session.ViewedContentId;
        var live = session.IsLive;
        var hasStates = states.Count > 0;
        foreach (var zone in zones)
        {
            zone.SetName(session.Spoilers);
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

                states.TryGetValue(row.Current.QuestRowId, out var evaluation);
                row.SetState(evaluation, session.Names, states);
                if (FlightProgress.QuestCurrentDone(flag, evaluation?.State))
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

            var total = zone.Zone.TotalCurrents;
            var reading = new BeadReading(viewed, live, total > 0 && unknown == total, hasStates);
            zone.SetCounts(attuned, fieldAttuned, unknown, questsDone, reading);
        }

        countsVersion = session.Version;
        headerVersion = -1;
    }

    /// <summary>Zones of one expansion under a header, in index order, with the expansion's ring icon (0 when unknown).</summary>
    private sealed class ExpansionGroup(string header, ZoneItem[] zones, uint icon)
    {
        public string Header { get; } = header;
        public ZoneItem[] Zones { get; } = zones;
        public uint Icon { get; } = icon;
    }

    /// <summary>One flying zone with its quest rows; counts and their strings change once per session version.</summary>
    private sealed class ZoneItem
    {
        public ZoneItem(FlightZone zone, QuestRow[] rows)
        {
            Zone = zone;
            Rows = rows;
            Name = zone.Name;
            Label = zone.Name;
            HereLabel = Strings.FlightCurrentZoneMarker + zone.Name;
            CountText = LeftText.Left(0, rows.Length);
            BannerCountText = LeftText.LeftOrDone(0, rows.Length);
            TooltipText = string.Empty;
            BeadKey = Motion.Key(BeadTag, zone.TerritoryId);
        }

        /// <summary>The bead ring's motion key: its lighting sequence plays under it.</summary>
        public ulong BeadKey { get; }

        /// <summary>Quest currents done (attuned, or their quest complete where attunement cannot be read): the lit beads.</summary>
        public int QuestsDone { get; private set; }

        /// <summary>The lit beads before the last rise, where the lighting sequence starts.</summary>
        public int LitFrom { get; private set; }

        /// <summary>The field currents left, for the banner ("2 left", "all done"); empty when the zone has none or none is readable.</summary>
        public string FieldCountText { get; private set; } = string.Empty;

        /// <summary>Whose counts these are and how they were read; the default before the first read.</summary>
        private BeadReading reading;

        public FlightZone Zone { get; }
        public QuestRow[] Rows { get; }
        public uint TerritoryId => Zone.TerritoryId;

        /// <summary>The zone's name as the spoiler shield prints it: its placeholder before the story reaches it (1.20.0 N6).</summary>
        public string Name { get; private set; }

        public string Label { get; private set; }
        public string HereLabel { get; private set; }

        /// <summary>Names the zone through <paramref name="spoilers"/>; composes only when the printed name changes.</summary>
        public void SetName(Core.Query.SpoilerMask spoilers)
        {
            var name = spoilers.Name(Core.Query.SpoilerKind.Area, Zone.Name);
            if (string.Equals(name, Name, StringComparison.Ordinal))
            {
                return;
            }

            Name = name;
            Label = name;
            HereLabel = Strings.FlightCurrentZoneMarker + name;
        }

        /// <summary>The zone list's count: quest currents left ("2 left"), empty once none is (the moon says so).</summary>
        public string CountText { get; private set; }

        /// <summary>The banner's count: quest currents left ("2 left"), or "all done".</summary>
        public string BannerCountText { get; private set; }

        public string TooltipText { get; private set; }

        /// <summary>Attuned currents (quest and field) over every current in the zone; unknown counts as not attuned.</summary>
        public int Attuned { get; private set; }

        public int FieldAttuned { get; private set; }
        public float Fraction { get; private set; }
        public bool Complete { get; private set; }

        /// <summary>True when no current is readable (a stored snapshot, logged out), so the moon is veiled instead of new.</summary>
        public bool AllUnknown { get; private set; } = true;

        public void SetCounts(int attuned, int fieldAttuned, int unknown, int questsDone, BeadReading read)
        {
            // A rise between two real reads of the same character lights the new beads one after another (Full flair,
            // motion on); the first count, a character switch, attunement becoming readable, a fall, or a rise with the
            // motion off shows at once.
            if (BeadRingMath.ShouldSequence(reading, read, QuestsDone, questsDone) && Theme.FlairMotion)
            {
                LitFrom = QuestsDone;
                Motion.Trigger(BeadKey);
            }
            else
            {
                LitFrom = questsDone;
            }

            reading = read;
            QuestsDone = questsDone;
            var total = Zone.TotalCurrents;
            Attuned = attuned;
            FieldAttuned = fieldAttuned;
            Fraction = total > 0 ? (float)attuned / total : 0f;
            AllUnknown = total > 0 && unknown == total;
            Complete = total > 0 && attuned == total;
            CountText = LeftText.Left(questsDone, Rows.Length);
            BannerCountText = LeftText.LeftOrDone(questsDone, Rows.Length);
            var field = Zone.FieldCurrentCount;
            FieldCountText = field == 0 || AllUnknown ? string.Empty : LeftText.LeftOrDone(fieldAttuned, field);
            TooltipText = AllUnknown
                ? string.Format(CultureInfo.CurrentCulture, Strings.FlightZoneTooltipUnknownFormat, total, questsDone, Rows.Length)
                : string.Format(CultureInfo.CurrentCulture, Strings.FlightZoneTooltipFormat, attuned, total, questsDone, Rows.Length);
        }
    }

    /// <summary>
    /// One quest current with its quest record; the attunement and the quest state with its status line change once
    /// per session version (<see cref="RefreshCounts"/>), so the row draw reads stored strings.
    /// </summary>
    private sealed class QuestRow(FlightCurrent current, QuestRecord? quest)
    {
        public FlightCurrent Current { get; } = current;
        public QuestRecord? Quest { get; } = quest;
        /// <summary>The quest's name as the spoiler shield prints it; refreshed with the state each session version.</summary>
        public string QuestName { get; private set; } = quest?.Name ?? string.Format(CultureInfo.InvariantCulture, Strings.FlightQuestFormat, current.QuestRowId);
        public Mark AttunedGlyph { get; private set; } = Mark.Unknown;
        public string AttunedText { get; private set; } = Strings.FlightAttunedUnknown;

        /// <summary>The viewed character's evaluation of the quest; null when it has none (no snapshot, unknown quest).</summary>
        public QuestEvaluation? Evaluation { get; private set; }

        public QuestState State { get; private set; } = QuestState.Unknown;

        /// <summary>The Status cell: state word then the decisive blocker; empty without an evaluation.</summary>
        public string StatusText { get; private set; } = string.Empty;

        public void SetAttuned(bool? attuned)
        {
            (AttunedGlyph, AttunedText) = attuned switch
            {
                true => (Mark.Check, Strings.FlightAttunedYes),
                false => (Mark.Cross, Strings.FlightAttunedNo),
                null => (Mark.Unknown, Strings.FlightAttunedUnknown),
            };
        }

        public void SetState(QuestEvaluation? evaluation, BlockerNames names, IReadOnlyDictionary<uint, QuestEvaluation> states)
        {
            Evaluation = evaluation;
            if (Quest is { } named)
            {
                // Many aether currents come from main scenario quests; a masked one reads as its placeholder (T19).
                QuestName = names.QuestName(named);
            }
            else
            {
                // No quest record: the "Quest #id" fallback, composed again so it follows a language switch.
                QuestName = string.Format(CultureInfo.InvariantCulture, Strings.FlightQuestFormat, Current.QuestRowId);
            }

            if (evaluation is null)
            {
                State = QuestState.Unknown;
                StatusText = string.Empty;
                return;
            }

            State = evaluation.State;
            StatusText = Quest is { } quest ? BlockerText.StatusText(evaluation, quest, names, states) : Strings.StateName(State);
        }
    }
}

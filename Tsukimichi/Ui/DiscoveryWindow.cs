using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using TerritoryType = Lumina.Excel.Sheets.TerritoryType;
using Tsukimichi.Core.Discovery;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// Nearby quests (F-76 companion): the quests the viewed character can start in the current zone, plus the accepted
/// quests whose giver stands here. It is read while playing, so it sits on the Chrome like the Todo overlay (T13):
/// Night chrome, names outlined in the primary tone and level / job in the secondary, moons never under 14 px, and a
/// "…" button per row with the same menu as a right-click (Show in the Journal, Flag, Teleport through Lifestream,
/// Link in chat). A click shows the quest in the Journal; the map is flagged on a double-click or from the menu, never
/// on a single click (accessibility A6/A8). The row model is rebuilt only on
/// <see cref="SessionState.Changed"/> and <see cref="IClientState.TerritoryChanged"/> (and when a setting flips), and
/// a notification that changed neither the session version, the catalog nor the territory is skipped outright;
/// nothing is looked up per frame beyond the visible rows' teleport gating. <see cref="Changed"/> fires after a
/// rebuild that altered the lists, which is what the server info bar entry listens to. Opened with
/// <c>/tsuki nearby</c> or a click on that entry. The cog at the top right opens Settings › Integrations, where its
/// settings live since 1.7.0; they persist through <see cref="DiscoverySettings"/>.
/// </summary>
public sealed class DiscoveryWindow : Window, IDisposable
{
    private readonly record struct Row(QuestRecord Quest, QuestState State, string Level, string Job, string StateText);

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;

    /// <summary>The row menu's popup id, shared by the right-click and the "…" button (same id scope, the row's).</summary>
    private const string RowMenuId = "##nearbyRowMenu";

    private static readonly string CogGlyph = Chrome.Icon(FontAwesomeIcon.Cog);
    private static readonly string MoreGlyph = Chrome.Icon(FontAwesomeIcon.EllipsisH);
    private static readonly string FoldedGlyph = Chrome.Icon(FontAwesomeIcon.CaretRight);
    private static readonly string OpenGlyph = Chrome.Icon(FontAwesomeIcon.CaretDown);

    /// <summary>The widest level label ("Lv 100"), which sizes the Level column.</summary>
    private static readonly Localization.LocText LevelColumnSample = new(static () => string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryLevelFormat, 100));

    private Theme.StyleScope nightChrome;
    private bool acceptedOpen;

    // A clicked row whose reveal waits out the double-click window (ImGui time of the click); see DrawRow.
    private QuestRecord? pendingReveal;
    private double pendingRevealTime;

    /// <summary>Logical minimum size of the window, scaled by the UI scale each frame.</summary>
    private const float MinWidthLogical = 320f;
    private const float MinHeightLogical = 180f;

    private readonly SessionState session;
    private readonly IClientState clientState;
    private readonly GameLinks links;
    private readonly QueryRunner runner;
    private readonly IDataManager data;
    private readonly DiscoverySettings settings;
    private readonly string settingsPath;
    private readonly IPluginLog log;
    private readonly Action<QuestRecord> reveal;
    private readonly Dictionary<uint, string> zoneNames = [];

    private Row[] startable = [];
    private Row[] accepted = [];
    private string[] startableNames = [];
    private uint territoryId;
    private int rowsVersion = -1;
    private int rowsLanguage = -1;
    private GameData.CatalogBundle? rowsBundle;
    private string zoneName = string.Empty;
    private string zoneLabel = Strings.DiscoveryUnknownZone;
    private string header = string.Empty;
    private string emptyText = string.Empty;
    private string acceptedHeader = string.Empty;

    private ImGuiListClipperPtr clipper;
    private bool clipperCreated;

    /// <param name="session">Catalog, viewed character and its evaluations.</param>
    /// <param name="clientState">Current territory and its change event.</param>
    /// <param name="links">Map flags and Lifestream teleports.</param>
    /// <param name="runner">Job labels (its per-category cache).</param>
    /// <param name="data">TerritoryType sheet for the zone name.</param>
    /// <param name="settings">The discovery settings; edited in place by the cog popup and saved to <paramref name="settingsPath"/>.</param>
    /// <param name="settingsPath">Where <paramref name="settings"/> is written.</param>
    /// <param name="log">Warnings for sheet and save failures.</param>
    /// <param name="reveal">Shows a quest in the main window (open, bring to front, select).</param>
    public DiscoveryWindow(
        SessionState session,
        IClientState clientState,
        GameLinks links,
        QueryRunner runner,
        IDataManager data,
        DiscoverySettings settings,
        string settingsPath,
        IPluginLog log,
        Action<QuestRecord> reveal)
        : base(Strings.DiscoveryWindowTitle)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.runner = runner ?? throw new ArgumentNullException(nameof(runner));
        this.data = data ?? throw new ArgumentNullException(nameof(data));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.settingsPath = settingsPath ?? throw new ArgumentNullException(nameof(settingsPath));
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));

        Size = new Vector2(420f, 360f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        session.Changed += OnSessionChanged;
        clientState.TerritoryChanged += OnTerritoryChanged;
        Rebuild(force: true);
    }

    /// <summary>Raised after a rebuild changed the startable list, the accepted list or the zone, and when a setting flips.</summary>
    public event Action? Changed;

    /// <summary>The settings Settings › Integrations › Nearby and server info bar edits.</summary>
    public DiscoverySettings Settings => settings;

    /// <summary>Opens Settings on the Nearby block; set by the plugin. Null hides the cog.</summary>
    public Action? OpenSettings { get; set; }

    /// <summary>
    /// After <see cref="Settings"/> was edited: saves them, then rebuilds the rows when <paramref name="rowsChanged"/>
    /// (which tells the server info bar entry when the lists changed) or tells the entry at once.
    /// </summary>
    public void SettingsChanged(bool rowsChanged)
    {
        SaveSettings();
        if (rowsChanged)
        {
            Rebuild(force: true);
        }
        else
        {
            Changed?.Invoke();
        }
    }

    /// <summary>Quests the viewed character can start in the current zone, after the other-job setting.</summary>
    public int StartableCount => startable.Length;

    /// <summary>Names of <see cref="StartableCount"/> quests, level then name order.</summary>
    public IReadOnlyList<string> StartableNames => startableNames;

    /// <summary>Place name of the current territory; empty when the sheet has none.</summary>
    public string ZoneName => zoneName;

    /// <summary><see cref="ZoneName"/>, or "this zone" when it is empty, for sentences.</summary>
    public string ZoneLabel => zoneLabel;

    public void Dispose()
    {
        session.Changed -= OnSessionChanged;
        clientState.TerritoryChanged -= OnTerritoryChanged;
        if (clipperCreated)
        {
            clipper.Destroy();
            clipperCreated = false;
        }
    }

    public override void PreDraw()
    {
        // The window is its own top level, so its minimum follows the UI scale like the main window's.
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.FontScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void Draw()
    {
        // The window scales itself so the rows, the tooltips and the moons (already at the icon scale) agree; the
        // scale is reset before Begin lays the title bar out again.
        UiMetrics.ApplyFontScale();
        try
        {
            DrawContent();
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawContent()
    {
        FireDueReveal();
        DrawHeader();
        Chrome.Hairline();

        if (session.Bundle is null)
        {
            Chrome.OutlinedText(session.CatalogLoading ? Strings.CatalogNotReady : Strings.CatalogUnavailable, Theme.Surface.TextSecondary);
            return;
        }

        if (session.ViewedSnapshot is null)
        {
            Chrome.OutlinedText(Strings.ZoneNoCharacter, Theme.Surface.TextSecondary);
            return;
        }

        if (startable.Length == 0)
        {
            DrawEmpty();
        }
        else
        {
            DrawTable("##nearbyStartable", startable);
        }

        if (accepted.Length == 0)
        {
            return;
        }

        ImGui.Spacing();
        DrawAcceptedHeader();
        if (acceptedOpen)
        {
            DrawTable("##nearbyAccepted", accepted);
        }
    }

    private void DrawHeader()
    {
        var line = ImGui.GetTextLineHeight();
        var button = UiMetrics.MinTarget;
        var start = ImGui.GetCursorScreenPos();
        var glyph = UiMetrics.PlayingGlyphSize(line);
        var rowHeight = MathF.Max(glyph, button);
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + (rowHeight - glyph) * 0.5f));
        MoonGlyph.DrawInline(QuestState.Ready, glyph);
        ImGui.SameLine();
        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetCursorScreenPos().X, start.Y + (rowHeight - line) * 0.5f));
        Chrome.OutlinedText(header, Theme.Surface.Text);

        // Cog at the right edge, a round button never under the minimum target: it opens Settings on this window's block.
        if (OpenSettings is { } openSettings)
        {
            ImGui.SetCursorScreenPos(new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - button, start.Y + (rowHeight - button) * 0.5f));
            if (Chrome.IconButtonRound("##nearbyCog", CogGlyph, Strings.DiscoverySettingsTooltip))
            {
                openSettings();
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + rowHeight));
        ImGui.Dummy(Vector2.Zero);
    }

    private void DrawEmpty()
    {
        var glyph = UiMetrics.PlayingGlyphSize(ImGui.GetTextLineHeight());
        MoonGlyph.DrawInline(QuestState.Blocked, glyph);
        ImGui.SameLine();
        Chrome.OutlinedText(emptyText, Theme.Surface.TextSecondary);
    }

    /// <summary>"Also in your journal here (N)" as an outlined caption with a caret over a hairline; a click folds it.</summary>
    private void DrawAcceptedHeader()
    {
        var start = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        if (ImGui.InvisibleButton("##acceptedToggle", new Vector2(width, ImGui.GetTextLineHeight())))
        {
            acceptedOpen = !acceptedOpen;
        }

        var hovered = ImGui.IsItemHovered();
        Chrome.FocusRing();
        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary);
        ImGui.PushFont(UiBuilder.IconFont);
        Chrome.OutlinedTextAt(dl, start, acceptedOpen ? OpenGlyph : FoldedGlyph, ink);
        ImGui.PopFont();
        Chrome.OutlinedTextAt(dl, new Vector2(start.X + ImGui.GetFontSize(), start.Y), acceptedHeader, ink);
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.DiscoveryAcceptedToggleTooltip);
        }

        Chrome.Hairline();
    }

    private void DrawTable(string id, Row[] rows)
    {
        using var table = ImRaii.Table(id, 5, TableFlags);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var glyphSize = UiMetrics.PlayingGlyphSize(line);
        var button = UiMetrics.MinTarget;
        ImGui.TableSetupColumn(Strings.DiscoveryColumnState, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphSize * 1.2f);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnQuest, ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnLevel, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize(LevelColumnSample.Value).X);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnJob, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("WWWW").X);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnActions, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, button);
        ImGui.TableHeadersRow();

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        var rowHeight = MathF.Max(glyphSize, button);
        clipper.Begin(rows.Length, rowHeight + ImGui.GetStyle().CellPadding.Y * 2f);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                DrawRow(rows[i], i, glyphSize, rowHeight);
            }
        }

        clipper.End();
    }

    private void DrawRow(Row row, int index, float glyphSize, float rowHeight)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow(ImGuiTableRowFlags.None, rowHeight);
        var line = ImGui.GetTextLineHeight();

        ImGui.TableNextColumn();
        var cell = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(cell.X, cell.Y + (rowHeight - glyphSize) * 0.5f));
        MoonGlyph.DrawInline(row.State, glyphSize);
        if (ImGui.IsItemHovered())
        {
            // The job line only adds something for ReadyOnOtherJob ("Ready on WHM"); the name is in the first line otherwise.
            UiMetrics.Tooltip(Strings.StateTooltip(row.State, row.Quest), row.State == QuestState.ReadyOnOtherJob ? row.StateText : null);
        }

        // Name: the selectable spans the cell with the name painted over it, outlined. A click shows it in the
        // Journal after the double-click window passes (FireDueReveal); a double-click flags the giver instead.
        ImGui.TableNextColumn();
        var nameMin = ImGui.GetCursorScreenPos();
        var nameWidth = ImGui.GetContentRegionAvail().X;
        if (ImGui.Selectable("##row", false, ImGuiSelectableFlags.AllowDoubleClick, new Vector2(0f, rowHeight)))
        {
            if (ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
            {
                pendingReveal = null;
                FlagOrReveal(row.Quest);
            }
            else
            {
                pendingReveal = row.Quest;
                pendingRevealTime = ImGui.GetTime();
            }
        }

        var hovered = ImGui.IsItemHovered();
        var openMenu = hovered && ImGui.IsMouseReleased(ImGuiMouseButton.Right);
        var dl = ImGui.GetWindowDrawList();
        var textY = nameMin.Y + (rowHeight - line) * 0.5f;
        dl.PushClipRect(nameMin, new Vector2(nameMin.X + nameWidth, nameMin.Y + rowHeight), true);
        Chrome.OutlinedTextAt(dl, new Vector2(nameMin.X, textY), session.Spoilers.DisplayName(row.Quest), Theme.U32(Theme.Surface.Text));
        dl.PopClipRect();
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.DiscoveryRowTooltip);
        }

        ImGui.TableNextColumn();
        Chrome.OutlinedTextAt(dl, new Vector2(ImGui.GetCursorScreenPos().X, textY), row.Level, Theme.U32(Theme.Surface.TextSecondary));

        ImGui.TableNextColumn();
        Chrome.OutlinedTextAt(dl, new Vector2(ImGui.GetCursorScreenPos().X, textY), row.Job, Theme.U32(Theme.Surface.TextSecondary));

        // The "…" opens the same menu as the right-click, for keyboard, controller and one-handed players (A6).
        ImGui.TableNextColumn();
        openMenu |= Chrome.IconButtonRound("##more", MoreGlyph, Strings.DiscoveryRowMoreTooltip);
        if (openMenu)
        {
            ImGui.OpenPopup(RowMenuId);
        }

        DrawRowMenu(row.Quest, row.State);
    }

    /// <summary>The single-click reveal, once a double-click window has passed since the click without a second press.</summary>
    private void FireDueReveal()
    {
        if (pendingReveal is not { } quest || ImGui.GetTime() - pendingRevealTime <= ImGui.GetIO().MouseDoubleClickTime)
        {
            return;
        }

        pendingReveal = null;
        reveal(quest);
    }

    /// <summary>Double-click flags the giver; a quest without a mappable giver is shown in the Journal instead.</summary>
    private void FlagOrReveal(QuestRecord quest)
    {
        if (links.CanFlagMap(quest))
        {
            links.FlagMap(quest);
        }
        else
        {
            reveal(quest);
        }
    }

    /// <summary>Show in the Journal, Flag, Teleport, Walk and Go to giver (disabled with the reason when they cannot, naming the plugin they need) and Link in chat.</summary>
    private void DrawRowMenu(QuestRecord quest, QuestState state)
    {
        if (!ImGui.IsPopupOpen(RowMenuId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(RowMenuId);
        if (!popup)
        {
            return;
        }

        if (ImGui.MenuItem(Strings.DiscoveryRevealInJournal))
        {
            reveal(quest);
        }

        if (ImGui.MenuItem(Strings.FlagOnMap, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        TravelControls.MenuItems(links, quest, Strings.TeleportToGiver);

        if (ImGui.MenuItem(Strings.LinkInChat))
        {
            links.PrintQuestLink(quest, Strings.StateName(state, quest));
        }
    }

    private void OnSessionChanged() => Rebuild(force: false);

    private void OnTerritoryChanged(uint territory) => Rebuild(force: false);

    /// <summary>
    /// Recomputes the lists from the session and the current territory. A call that finds the session version, the
    /// catalog and the territory unchanged since the last build returns at once. Otherwise row labels are rebuilt, and
    /// <see cref="Changed"/> raised, only when the zone or the (quest, state) sequences differ from the last build or
    /// the catalog was replaced (rows hold its <see cref="QuestRecord"/>s), so a poll that changed nothing nearby
    /// costs two catalog scans and no allocation.
    /// </summary>
    private void Rebuild(bool force)
    {
        var territory = clientState.TerritoryType;
        var version = session.Version;
        var sessionBundle = session.Bundle;
        // A language switch rebuilds every label (and the server bar entry, through Changed) even when no quest moved.
        var unchanged = territory == territoryId && ReferenceEquals(sessionBundle, rowsBundle) && rowsLanguage == Localization.Loc.Version;
        if (!force && unchanged && version == rowsVersion)
        {
            return;
        }

        List<QuestRecord> startNow;
        List<QuestRecord> acceptedHere;
        var states = session.States;
        if (sessionBundle is { } bundle && session.ViewedSnapshot is not null)
        {
            startNow = QuestDiscovery.StartableInZone(bundle.Catalog, states, territory, settings.NearbyIncludeOtherJob);
            acceptedHere = QuestDiscovery.AcceptedInZone(bundle.Catalog, states, territory);
        }
        else
        {
            bundle = null;
            startNow = [];
            acceptedHere = [];
        }

        rowsVersion = version;
        if (!force && unchanged && Same(startable, startNow, states) && Same(accepted, acceptedHere, states))
        {
            return;
        }

        territoryId = territory;
        rowsBundle = sessionBundle;
        rowsLanguage = Localization.Loc.Version;
        zoneName = ZoneNameFor(territory);
        zoneLabel = zoneName.Length > 0 ? zoneName : Strings.DiscoveryUnknownZone;
        startable = BuildRows(startNow, states, bundle);
        accepted = BuildRows(acceptedHere, states, bundle);
        startableNames = new string[startable.Length];
        for (var i = 0; i < startable.Length; i++)
        {
            startableNames[i] = session.Spoilers.DisplayName(startable[i].Quest);
        }

        header = startable.Length == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryHeaderOneFormat, zoneLabel)
            : string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryHeaderFormat, zoneLabel, startable.Length);
        emptyText = string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryEmptyFormat, zoneLabel);
        acceptedHeader = string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryAcceptedHeaderFormat, accepted.Length);
        Changed?.Invoke();
    }

    private static bool Same(Row[] rows, List<QuestRecord> quests, IReadOnlyDictionary<uint, Core.Evaluation.QuestEvaluation> states)
    {
        if (rows.Length != quests.Count)
        {
            return false;
        }

        for (var i = 0; i < rows.Length; i++)
        {
            var quest = quests[i];
            var state = states.TryGetValue(quest.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            if (rows[i].Quest.RowId != quest.RowId || rows[i].State != state)
            {
                return false;
            }
        }

        return true;
    }

    private Row[] BuildRows(List<QuestRecord> quests, IReadOnlyDictionary<uint, Core.Evaluation.QuestEvaluation> states, GameData.CatalogBundle? bundle)
    {
        if (quests.Count == 0)
        {
            return [];
        }

        var rows = new Row[quests.Count];
        for (var i = 0; i < rows.Length; i++)
        {
            var quest = quests[i];
            var evaluation = states.TryGetValue(quest.RowId, out var found) ? found : null;
            var state = evaluation?.State ?? QuestState.Unknown;
            var job = runner.JobShort(quest);
            var stateText = BlockerText.StatusText(evaluation, quest, session.Names, states);
            if (state == QuestState.ReadyOnOtherJob && evaluation?.ReadyOnJob is { } readyOn && bundle is not null)
            {
                var abbreviation = bundle.Names.ClassJobAbbreviation(readyOn);
                if (abbreviation.Length > 0)
                {
                    job = abbreviation;
                    stateText = string.Format(CultureInfo.CurrentCulture, Strings.ReadyOnJobFormat, abbreviation);
                }
            }

            rows[i] = new Row(quest, state, string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryLevelFormat, quest.DisplayLevel), job, stateText);
        }

        return rows;
    }

    /// <summary>TerritoryType place name, read once per territory; empty for 0 or when the sheet has none.</summary>
    private string ZoneNameFor(uint territory)
    {
        if (territory == 0)
        {
            return string.Empty;
        }

        if (zoneNames.TryGetValue(territory, out var cached))
        {
            return cached;
        }

        var name = string.Empty;
        try
        {
            name = UiFormat.CleanSheetText(data.GetExcelSheet<TerritoryType>()?.GetRowOrDefault(territory)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty);
        }
        catch (Exception ex)
        {
            log.Warning(ex, "TerritoryType lookup for {TerritoryId} failed", territory);
        }

        zoneNames[territory] = name;
        return name;
    }

    private void SaveSettings()
    {
        try
        {
            // Framework thread: a few milliseconds at most; the next change saves again.
            settings.Save(settingsPath, Core.Storage.AtomicFile.QuickAttempts);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warning(ex, "Discovery settings could not be saved to {Path}", settingsPath);
        }
    }
}

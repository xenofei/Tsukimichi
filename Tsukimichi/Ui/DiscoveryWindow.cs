using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
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
/// Nearby quests (F-76 companion): the quests the viewed character can start in the current zone, with a Flag and a
/// Teleport button per row, plus the accepted quests whose giver stands here. The row model is rebuilt only on
/// <see cref="SessionState.Changed"/> and <see cref="IClientState.TerritoryChanged"/> (and when a setting flips), and
/// a notification that changed neither the session version, the catalog nor the territory is skipped outright;
/// nothing is looked up per frame beyond the visible rows' teleport gating. <see cref="Changed"/> fires after a
/// rebuild that altered the lists, which is what the server info bar entry listens to. Opened with
/// <c>/tsuki nearby</c> or a click on that entry. Settings live behind the cog at the top right and persist through
/// <see cref="DiscoverySettings"/>.
/// </summary>
public sealed class DiscoveryWindow : Window, IDisposable
{
    private readonly record struct Row(QuestRecord Quest, QuestState State, string Level, string Job, string StateText);

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.SizingStretchProp;

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
            MinimumSize = new Vector2(320f, 180f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        session.Changed += OnSessionChanged;
        clientState.TerritoryChanged += OnTerritoryChanged;
        Rebuild(force: true);
    }

    /// <summary>Raised after a rebuild changed the startable list, the accepted list or the zone, and when a setting flips.</summary>
    public event Action? Changed;

    /// <summary>The settings the cog popup edits.</summary>
    public DiscoverySettings Settings => settings;

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

    public override void Draw()
    {
        DrawHeader();
        ImGui.Separator();

        if (session.Bundle is null)
        {
            ImGui.TextDisabled(session.CatalogLoading ? Strings.CatalogNotReady : Strings.CatalogUnavailable);
            return;
        }

        if (session.ViewedSnapshot is null)
        {
            ImGui.TextDisabled(Strings.ZoneNoCharacter);
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
        if (ImGui.CollapsingHeader(acceptedHeader))
        {
            DrawTable("##nearbyAccepted", accepted);
        }
    }

    private void DrawHeader()
    {
        var line = ImGui.GetTextLineHeight();
        MoonGlyph.DrawInline(QuestState.Ready, UiMetrics.InlineGlyphSize(line));
        ImGui.SameLine();
        ImGui.TextUnformatted(header);

        // Cog at the right edge; the popup below hangs off it.
        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        ImGui.SameLine(ImGui.GetWindowContentRegionMax().X - buttonSize.X);
        if (ImGuiComponents.IconButton("##nearbyCog", FontAwesomeIcon.Cog, buttonSize))
        {
            ImGui.OpenPopup(Strings.DiscoverySettingsPopup);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.DiscoverySettingsTooltip);
        }

        using var popup = ImRaii.Popup(Strings.DiscoverySettingsPopup);
        if (!popup)
        {
            return;
        }

        var show = settings.ShowDtrEntry;
        if (ImGui.Checkbox(Strings.DiscoveryShowDtrLabel, ref show))
        {
            settings.ShowDtrEntry = show;
            SaveSettings();
            Changed?.Invoke();
        }

        using (ImRaii.Disabled(!settings.ShowDtrEntry))
        {
            var whenEmpty = settings.DtrShowWhenEmpty;
            if (ImGui.Checkbox(Strings.DiscoveryDtrShowWhenEmptyLabel, ref whenEmpty))
            {
                settings.DtrShowWhenEmpty = whenEmpty;
                SaveSettings();
                Changed?.Invoke();
            }
        }

        var otherJob = settings.NearbyIncludeOtherJob;
        if (ImGui.Checkbox(Strings.DiscoveryIncludeOtherJobLabel, ref otherJob))
        {
            settings.NearbyIncludeOtherJob = otherJob;
            SaveSettings();
            Rebuild(force: true);
        }
    }

    private void DrawEmpty()
    {
        MoonGlyph.DrawInline(QuestState.Blocked, UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight()));
        ImGui.SameLine();
        ImGui.TextDisabled(emptyText);
    }

    private void DrawTable(string id, Row[] rows)
    {
        using var table = ImRaii.Table(id, 5, TableFlags);
        if (!table)
        {
            return;
        }

        var line = ImGui.GetTextLineHeight();
        var glyphSize = UiMetrics.InlineGlyphSize(line);
        var padding = ImGui.GetStyle().FramePadding.X * 2f;
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var actionsWidth = ImGui.CalcTextSize(Strings.DiscoveryFlag).X + padding;
        if (links.TeleportAvailable)
        {
            actionsWidth += spacing + ImGui.CalcTextSize(Strings.DiscoveryTeleport).X + padding;
        }

        ImGui.TableSetupColumn(Strings.DiscoveryColumnState, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, glyphSize * 1.4f);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnQuest, ImGuiTableColumnFlags.WidthStretch, 1f);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnLevel, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Lv 100").X);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnJob, ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("WWWW").X);
        ImGui.TableSetupColumn(Strings.DiscoveryColumnActions, ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoResize, actionsWidth);
        ImGui.TableHeadersRow();

        if (!clipperCreated)
        {
            clipper = ImGui.ImGuiListClipper();
            clipperCreated = true;
        }

        clipper.Begin(rows.Length);
        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                DrawRow(rows[i], i, glyphSize);
            }
        }

        clipper.End();
    }

    private void DrawRow(Row row, int index, float glyphSize)
    {
        using var id = ImRaii.PushId(index);
        ImGui.TableNextRow();

        ImGui.TableNextColumn();
        MoonGlyph.DrawInline(row.State, glyphSize);
        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(row.StateText);
        }

        ImGui.TableNextColumn();
        if (ImGui.Selectable(row.Quest.Name))
        {
            reveal(row.Quest);
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.DiscoveryRevealTooltip);
        }

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(row.Level);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(row.Job);

        ImGui.TableNextColumn();
        DrawActions(row.Quest);
    }

    private void DrawActions(QuestRecord quest)
    {
        using (ImRaii.Disabled(!links.CanFlagMap(quest)))
        {
            if (ImGui.SmallButton(Strings.DiscoveryFlag))
            {
                links.FlagMap(quest);
            }
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(Strings.DiscoveryFlagTooltip);
        }

        // Hidden without Lifestream; disabled, with the reason on hover, while it is busy or the giver's zone has no aetheryte.
        if (!links.TeleportAvailable)
        {
            return;
        }

        ImGui.SameLine();
        var aetheryte = links.NearestAetheryte(quest);
        var busy = links.TeleportBusy;
        using (ImRaii.Disabled(aetheryte is null || busy))
        {
            if (ImGui.SmallButton(Strings.DiscoveryTeleport))
            {
                links.TeleportToGiver(quest);
            }
        }

        if (!ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            return;
        }

        if (aetheryte is not { } target)
        {
            ImGui.SetTooltip(Strings.TeleportNoAetheryte);
        }
        else if (busy)
        {
            ImGui.SetTooltip(Strings.TeleportBusy);
        }
        else
        {
            ImGui.SetTooltip(string.Format(CultureInfo.CurrentCulture, Strings.TeleportTooltipFormat, target.Name));
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
        var unchanged = territory == territoryId && ReferenceEquals(sessionBundle, rowsBundle);
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
        zoneName = ZoneNameFor(territory);
        zoneLabel = zoneName.Length > 0 ? zoneName : Strings.DiscoveryUnknownZone;
        startable = BuildRows(startNow, states, bundle);
        accepted = BuildRows(acceptedHere, states, bundle);
        startableNames = new string[startable.Length];
        for (var i = 0; i < startable.Length; i++)
        {
            startableNames[i] = startable[i].Quest.Name;
        }

        header = startable.Length == 1
            ? string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryHeaderOneFormat, zoneLabel)
            : string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryHeaderFormat, zoneLabel, startable.Length);
        emptyText = string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryEmptyFormat, zoneLabel);
        acceptedHeader = string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryAcceptedHeaderFormat, accepted.Length) + "###nearbyAccepted";
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
            settings.Save(settingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log.Warning(ex, "Discovery settings could not be saved to {Path}", settingsPath);
        }
    }
}

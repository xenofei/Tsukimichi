using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Tsukimichi.Config;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Storage;
using Tsukimichi.Core.Todo;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Todo overlay (feature plan V2-13): a small always-visible panel, DailyDuty style, with one collapsible section per
/// enabled part of <see cref="TodoList"/> (pins, feature quests startable here, the next main scenario quest, the
/// current job's next job and role quest). Each row is a state moon, the quest name (click flags the giver on the
/// map; right-click offers Reveal in Tsukimichi, Flag, Teleport through Lifestream and a chat link) and a Dusk hint;
/// hovering shows the state and the next step. <see cref="Window.IsOpen"/> follows
/// <see cref="Configuration.TodoOverlayEnabled"/>; the window is not drawn while logged out, in a duty or in a
/// cutscene. The rows are rebuilt on <see cref="SessionState.Changed"/>, <see cref="IClientState.TerritoryChanged"/>,
/// when a section toggle flips and when <c>user/pins.json</c> changes (its write time is checked every
/// <see cref="PinsCheckInterval"/>); drawing allocates nothing. Locked (<see cref="Configuration.TodoOverlayLocked"/>)
/// only takes NoMove and NoResize so rows stay clickable; the background alpha is
/// <see cref="Configuration.TodoOverlayOpacity"/>. <see cref="ResetPosition"/> moves the panel back to the top left
/// on the next frame.
/// </summary>
public sealed class TodoOverlay : Window, IDisposable
{
    public const float MinOpacity = 0.2f;
    public const float MaxOpacity = 1f;

    /// <summary>How often the pins file's write time is checked while the overlay is drawn.</summary>
    public static readonly TimeSpan PinsCheckInterval = TimeSpan.FromSeconds(2);

    private const ImGuiWindowFlags BaseFlags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.AlwaysAutoResize
                                               | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoDocking;

    private const ImGuiWindowFlags LockedFlags = BaseFlags | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize;

    /// <summary>Offset from the main viewport's work area the panel returns to on <see cref="ResetPosition"/>, in logical pixels.</summary>
    private static readonly Vector2 DefaultOffset = new(24f, 96f);
    private static readonly string LockGlyph = FontAwesomeIcon.Lock.ToIconString();

    private readonly record struct Row(QuestRecord Quest, QuestState State, string Label, string Hint, string Tooltip);

    private sealed record SectionView(TodoSection Section, string Header, Row[] Rows);

    private readonly Configuration settings;
    private readonly SessionState session;
    private readonly GameLinks links;
    private readonly Action<QuestRecord> reveal;
    private readonly IClientState clientState;
    private readonly ICondition condition;
    private readonly PluginPaths paths;
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IPluginLog log;

    private SectionView[] sections = [];
    private int enabledSections;
    private bool catalogReady;
    private bool dirty = true;
    private int builtVersion = -1;
    private uint builtTerritory;
    private int builtSettings = -1;

    // Pins: the file's write time (checked on a timer) and the set loaded for the viewed character.
    private readonly HashSet<uint> pinned = [];
    private DateTime pinsStamp;
    private DateTime pinsLoadedStamp = DateTime.MinValue;
    private ulong? pinsContentId;
    private DateTime nextPinsCheckUtc;
    private bool pinsWarned;

    private JobLadder ladder = JobLadder.Empty;
    private CatalogBundle? ladderBundle;
    private bool resetPosition;
    private bool disposed;

    /// <param name="settings">Overlay settings; read every frame so the config window's changes show at once.</param>
    /// <param name="session">Catalog, viewed character and its evaluations.</param>
    /// <param name="links">Map flags, Lifestream teleports and chat links.</param>
    /// <param name="reveal">Shows a quest in the main window (open, bring to front, select).</param>
    /// <param name="clientState">Login state and the current territory.</param>
    /// <param name="condition">Duty and cutscene flags that hide the panel.</param>
    /// <param name="paths">Where <c>user/pins.json</c> lives.</param>
    /// <param name="pluginInterface">Saves the settings the panel's own menu changes.</param>
    /// <param name="log">Warnings for pins that could not be read.</param>
    public TodoOverlay(
        Configuration settings,
        SessionState session,
        GameLinks links,
        Action<QuestRecord> reveal,
        IClientState clientState,
        ICondition condition,
        PluginPaths paths,
        IDalamudPluginInterface pluginInterface,
        IPluginLog log)
        : base(Strings.TodoWindowTitle, BaseFlags)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
        this.reveal = reveal ?? throw new ArgumentNullException(nameof(reveal));
        this.clientState = clientState ?? throw new ArgumentNullException(nameof(clientState));
        this.condition = condition ?? throw new ArgumentNullException(nameof(condition));
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.pluginInterface = pluginInterface ?? throw new ArgumentNullException(nameof(pluginInterface));
        this.log = log ?? throw new ArgumentNullException(nameof(log));

        RespectCloseHotkey = false;
        DisableWindowSounds = true;
        ShowCloseButton = false;
        AllowPinning = false;
        AllowClickthrough = false;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(180f, 0f),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };

        session.Changed += OnSessionChanged;
        session.DataDeleted += MarkDirty;
        clientState.TerritoryChanged += OnTerritoryChanged;
        IsOpen = settings.TodoOverlayEnabled;
    }

    /// <summary>Flips <see cref="Configuration.TodoOverlayEnabled"/> and saves; the window follows on the next frame (<c>/tsuki todo</c>).</summary>
    public void ToggleEnabled()
    {
        settings.TodoOverlayEnabled = !settings.TodoOverlayEnabled;
        Save();
    }

    /// <summary>Moves the panel back to its default place on the next frame (the settings window's "Reset position").</summary>
    public void ResetPosition() => resetPosition = true;

    /// <summary>Rebuilds the rows on the next frame, e.g. after a setting outside the per-frame signature changed.</summary>
    public void MarkDirty() => dirty = true;

    /// <summary><see cref="Configuration.TodoOverlayOpacity"/> within the allowed bounds; a corrupt value reads as the default.</summary>
    public static float ClampOpacity(float opacity) => float.IsFinite(opacity) ? Math.Clamp(opacity, MinOpacity, MaxOpacity) : 0.85f;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        session.Changed -= OnSessionChanged;
        session.DataDeleted -= MarkDirty;
        clientState.TerritoryChanged -= OnTerritoryChanged;
    }

    public override void PreOpenCheck()
    {
        IsOpen = settings.TodoOverlayEnabled;
    }

    /// <summary>Not drawn while logged out, bound by a duty or watching a cutscene.</summary>
    public override bool DrawConditions() =>
        clientState.IsLoggedIn
        && !condition[ConditionFlag.BoundByDuty]
        && !condition[ConditionFlag.WatchingCutscene]
        && !condition[ConditionFlag.OccupiedInCutSceneEvent];

    public override void PreDraw()
    {
        Flags = settings.TodoOverlayLocked ? LockedFlags : BaseFlags;
        BgAlpha = ClampOpacity(settings.TodoOverlayOpacity);
        if (resetPosition)
        {
            resetPosition = false;
            ImGui.SetNextWindowPos(ImGuiHelpers.MainViewport.WorkPos + DefaultOffset * ImGuiHelpers.GlobalScale, ImGuiCond.Always);
        }

        Refresh();
    }

    public override void Draw()
    {
        DrawHeader();
        if (!catalogReady)
        {
            ImGui.TextDisabled(session.CatalogLoading ? Strings.CatalogNotReady : Strings.CatalogUnavailable);
            return;
        }

        if (sections.Length == 0)
        {
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextUnformatted(enabledSections > 0 ? Strings.TodoEmpty : Strings.TodoNoSections);
            return;
        }

        var glyphSize = UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight());
        foreach (var section in sections)
        {
            using var id = ImRaii.PushId((int)section.Section);
            if (!ImGui.CollapsingHeader(section.Header, ImGuiTreeNodeFlags.DefaultOpen))
            {
                continue;
            }

            for (var i = 0; i < section.Rows.Length; i++)
            {
                DrawRow(section.Rows[i], i, glyphSize);
            }
        }
    }

    /// <summary>"☾ Tsukimichi" in Moon, a small lock when locked; right-click for lock, reset position and hide.</summary>
    private void DrawHeader()
    {
        using (Theme.PushText(Theme.Moon))
        {
            ImGui.TextUnformatted(Strings.TodoHeader);
        }

        DrawHeaderMenu();

        if (!settings.TodoOverlayLocked)
        {
            return;
        }

        ImGui.SameLine();
        ImGui.SetWindowFontScale(0.75f);
        using (ImRaii.PushFont(UiBuilder.IconFont))
        using (Theme.PushText(Theme.Dusk))
        {
            ImGui.TextUnformatted(LockGlyph);
        }

        ImGui.SetWindowFontScale(1f);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.TodoLockedTooltip);
        }
    }

    private void DrawHeaderMenu()
    {
        using var popup = ImRaii.ContextPopupItem("##todoHeaderMenu");
        if (!popup)
        {
            return;
        }

        if (ImGui.MenuItem(settings.TodoOverlayLocked ? Strings.TodoMenuUnlock : Strings.TodoMenuLock))
        {
            settings.TodoOverlayLocked = !settings.TodoOverlayLocked;
            Save();
        }

        if (ImGui.MenuItem(Strings.TodoMenuResetPosition))
        {
            ResetPosition();
        }

        if (ImGui.MenuItem(Strings.TodoMenuHide))
        {
            settings.TodoOverlayEnabled = false;
            Save();
        }
    }

    private void DrawRow(Row row, int index, float glyphSize)
    {
        using var id = ImRaii.PushId(index);
        MoonGlyph.DrawInline(row.State, glyphSize);
        ImGui.SameLine();

        // The selectable is sized to the name so the hint can follow on the same line.
        var nameWidth = ImGui.CalcTextSize(row.Quest.Name).X;
        if (ImGui.Selectable(row.Label, false, ImGuiSelectableFlags.None, new Vector2(nameWidth, 0f)))
        {
            OnRowClick(row.Quest);
        }

        var hovered = ImGui.IsItemHovered();
        DrawRowMenu(row);

        if (row.Hint.Length > 0)
        {
            ImGui.SameLine();
            using var dusk = Theme.PushText(Theme.Dusk);
            ImGui.TextUnformatted(row.Hint);
        }

        if (hovered)
        {
            UiMetrics.Tooltip(row.Tooltip);
        }
    }

    /// <summary>Click flags the giver; a quest without a mappable giver is shown in the main window instead.</summary>
    private void OnRowClick(QuestRecord quest)
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

    private void DrawRowMenu(Row row)
    {
        using var popup = ImRaii.ContextPopupItem("##todoRowMenu");
        if (!popup)
        {
            return;
        }

        var quest = row.Quest;
        if (ImGui.MenuItem(Strings.TodoRevealInTsukimichi))
        {
            reveal(quest);
        }

        if (ImGui.MenuItem(Strings.FlagOnMap, enabled: links.CanFlagMap(quest)))
        {
            links.FlagMap(quest);
        }

        // Hidden without Lifestream; disabled, with the reason on hover, while it is busy or the giver's zone has no aetheryte.
        if (links.TeleportAvailable)
        {
            var aetheryte = links.NearestAetheryte(quest);
            var busy = links.TeleportBusy;
            if (ImGui.MenuItem(Strings.TeleportToGiver, enabled: aetheryte is not null && !busy))
            {
                links.TeleportToGiver(quest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                if (aetheryte is not { } target)
                {
                    UiMetrics.Tooltip(Strings.TeleportNoAetheryte);
                }
                else if (busy)
                {
                    UiMetrics.Tooltip(Strings.TeleportBusy);
                }
                else
                {
                    UiMetrics.Tooltip(string.Format(CultureInfo.CurrentCulture, Strings.TeleportTooltipFormat, target.Name));
                }
            }
        }

        if (ImGui.MenuItem(Strings.LinkInChat))
        {
            links.PrintQuestLink(quest, Strings.StateName(row.State, quest));
        }
    }

    private void OnSessionChanged() => dirty = true;

    private void OnTerritoryChanged(uint territory) => dirty = true;

    /// <summary>Section toggles as one integer, compared per frame so a change in the settings window rebuilds at once.</summary>
    private int SettingsSignature() =>
        (settings.TodoShowPins ? 1 : 0) | (settings.TodoShowNearbyFeature ? 2 : 0) | (settings.TodoShowMsq ? 4 : 0) | (settings.TodoShowJobQuests ? 8 : 0);

    /// <summary>Once per frame: notices a changed pins file on a timer, then rebuilds when any input moved.</summary>
    private void Refresh()
    {
        var now = DateTime.UtcNow;
        if (now >= nextPinsCheckUtc)
        {
            nextPinsCheckUtc = now + PinsCheckInterval;
            var stamp = PinsWriteTime();
            if (stamp != pinsStamp)
            {
                pinsStamp = stamp;
                dirty = true;
            }
        }

        var version = session.Version;
        var territory = clientState.TerritoryType;
        var signature = SettingsSignature();
        if (!dirty && version == builtVersion && territory == builtTerritory && signature == builtSettings)
        {
            return;
        }

        dirty = false;
        builtVersion = version;
        builtTerritory = territory;
        builtSettings = signature;
        Rebuild(territory);
    }

    private void Rebuild(uint territory)
    {
        var bundle = session.Bundle;
        catalogReady = bundle is not null;
        enabledSections = (settings.TodoShowPins ? 1 : 0) + (settings.TodoShowNearbyFeature ? 1 : 0) + (settings.TodoShowMsq ? 1 : 0) + (settings.TodoShowJobQuests ? 1 : 0);

        if (bundle is null || session.ViewedSnapshot is not { } snapshot)
        {
            sections = [];
            return;
        }

        LoadPins(session.ViewedContentId);
        if (!ReferenceEquals(ladderBundle, bundle))
        {
            ladderBundle = bundle;
            ladder = bundle.BuildJobLadder();
        }

        var model = TodoList.Build(new TodoInputs(
            bundle.Catalog,
            session.States,
            pinned,
            session.FeatureQuestIds,
            territory,
            snapshot.CurrentJob,
            snapshot.JobLevels,
            ladder,
            bundle.Names.ClassJobAbbreviations,
            settings.TodoShowPins,
            settings.TodoShowNearbyFeature,
            settings.TodoShowMsq,
            settings.TodoShowJobQuests));

        enabledSections = model.EnabledSections;
        if (model.Sections.Count == 0)
        {
            sections = [];
            return;
        }

        var views = new SectionView[model.Sections.Count];
        for (var i = 0; i < views.Length; i++)
        {
            var section = model.Sections[i];
            var rows = new List<Row>(section.Rows.Count);
            foreach (var row in section.Rows)
            {
                if (bundle.Catalog.GetByRowId(row.RowId) is not { } quest)
                {
                    continue;
                }

                var label = row.Name + "##" + row.RowId.ToString(CultureInfo.InvariantCulture);
                var tooltip = row.Hint.Length > 0
                    ? Strings.StateName(row.State, quest) + Strings.StateReasonSeparator + row.Hint + "\n" + Strings.TodoRowClickHint
                    : Strings.StateName(row.State, quest) + "\n" + Strings.TodoRowClickHint;
                rows.Add(new Row(quest, row.State, label, row.Hint, tooltip));
            }

            var header = string.Format(CultureInfo.CurrentCulture, Strings.TodoSectionFormat, Strings.TodoSectionName(section.Section), rows.Count) + "###todo" + section.Section;
            views[i] = new SectionView(section.Section, header, rows.ToArray());
        }

        sections = views;
    }

    /// <summary>The viewed character's pins, re-read only when the file or the character changed.</summary>
    private void LoadPins(ulong? contentId)
    {
        if (pinsLoadedStamp == pinsStamp && pinsContentId == contentId)
        {
            return;
        }

        pinsLoadedStamp = pinsStamp;
        pinsContentId = contentId;
        pinned.Clear();
        if (contentId is not { } id)
        {
            return;
        }

        try
        {
            var warnings = new List<string>();
            var pins = PinsFile.Load(paths.PinsFile, warnings);
            foreach (var warning in warnings)
            {
                log.Warning("Pins: {Warning}", warning);
            }

            if (pins.TryGetValue(id, out var list))
            {
                pinned.UnionWith(list);
            }
        }
        catch (Exception ex)
        {
            if (!pinsWarned)
            {
                pinsWarned = true;
                log.Warning(ex, "Pins could not be read for the todo overlay; further failures are logged at debug level");
            }
            else
            {
                log.Debug(ex, "Pins could not be read for the todo overlay");
            }
        }
    }

    /// <summary>Write time of <c>user/pins.json</c>; a missing file reads as a fixed old time, so deleting it counts as a change too.</summary>
    private DateTime PinsWriteTime()
    {
        try
        {
            return File.GetLastWriteTimeUtc(paths.PinsFile);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return DateTime.MinValue;
        }
    }

    private void Save() => settings.Save(pluginInterface);
}

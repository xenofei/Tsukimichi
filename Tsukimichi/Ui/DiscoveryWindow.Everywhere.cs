using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unlocks;

namespace Tsukimichi.Ui;

/// <summary>
/// Nearby's Here · Everywhere (feature plan v7, 1.21.0 P7; spec-1.21 P7). A switch in the header, and one fixed row
/// under it in both views (so switching never moves the list): the kind chips Ready, Blues, Side stories and Rewards
/// (1.14 chips; off is dashed) and Sort (Level fit, Ready first, Story order). Here keeps its table, filtered by the
/// chips and ordered by the sort. Everywhere is the zones board (<see cref="ZoneBoard"/>): expansion groups under an
/// Eyebrow heading ("ENDWALKER · Lv 80–90 · fits SGE 90"), the group that fits the job's level open and first, the
/// others one line ("Shadowbringers · 12 Ready across 6 zones ›"); zone rows two-line (the zone, its level span, "new
/// since 7.4"; then what is left in words) with Teleport and "…" (Go to the nearest Ready giver, Show in Journal, and
/// at Full hand-offs Send zone to Questionable) in a reserved slot; zones with nothing left fold per expansion. An
/// expansion past the story point is one line ("Dawntrail · 6 zones past Kiri's story · names hidden"), a zone past it
/// "A zone ahead" with its level span and the shield's hover and right-click. Quests set aside (P4) count nowhere and
/// "…" never goes to, shows or hands them off; travel and hand-offs are offered only for the character logged in here
/// (spec-1.21 decision 5). The view, the chips and the sort are saved with Nearby's settings.
/// </summary>
public sealed partial class DiscoveryWindow
{
    private const uint ZoneHoverTag = 0x5A4F_4E45; // "ZONE"

    /// <summary>Masked zones of a revealed expansion shown as rows before "N more zones ahead".</summary>
    private const int MaskedZoneRows = 2;

    private const string NearbyQuestionableHost = "nearby";

    private static readonly ZoneKinds[] KindOrder = [ZoneKinds.Ready, ZoneKinds.Blues, ZoneKinds.SideStories, ZoneKinds.Rewards];

    // The chips' ids, one per kind, so drawing them allocates nothing.
    private static readonly string[] KindIds = ["##kind0", "##kind1", "##kind2", "##kind3"];

    private readonly HashSet<byte> toggledGroups = [];
    private readonly HashSet<byte> openEmpty = [];
    private (int Version, GameData.CatalogBundle? Bundle, int Language, ZoneKinds Kinds, ZoneSort Sort, bool OtherJob, object? Zones, int Spoilers, ulong? Viewed) boardKey;
    private IReadOnlyList<ZoneExpansion> board = [];
    private readonly Dictionary<uint, List<QuestRecord>> zoneQuests = [];
    private string fitJob = string.Empty;
    private string firstName = string.Empty;
    private string newSince = string.Empty;
    private (Row[] Source, ZoneKinds Kinds, ZoneSort Sort, int Version) hereKey;
    private Row[] hereRows = [];

    /// <summary>The town and field zones (<see cref="QuestUnlocks.Zones"/>); null hides Everywhere's rows. Set by the plugin.</summary>
    public Func<IReadOnlyList<UnlockZone>?>? Zones { get; set; }

    /// <summary>What a quest counts for besides Ready: blue, side story, Moonlit reward left. Set by the plugin.</summary>
    public Func<QuestRecord, ZoneQuestKinds>? QuestKinds { get; set; }

    /// <summary>The shared Questionable hand-offs, for Send zone to Questionable at Full hand-offs; null hides it. Set by the plugin.</summary>
    public QuestionableActions? Questionable { get; set; }

    private bool Everywhere => settings.NearbyEverywhere;

    /// <summary>The Here · Everywhere switch, right-aligned before the cog; returns its left edge.</summary>
    private float DrawViewSwitch(float y, float rowHeight, float right)
    {
        ReadOnlySpan<string> labels = [Strings.NearbyHere, Strings.NearbyEverywhere];
        ReadOnlySpan<string> tips = [Strings.NearbyHereTip, Strings.NearbyEverywhereTip];
        var width = Chrome.SegmentedWidth(labels);
        var x = right - width;
        ImGui.SetCursorScreenPos(new Vector2(x, y + ((rowHeight - UiMetrics.MinTarget) * 0.5f)));
        var selected = Everywhere ? 1 : 0;
        if (Chrome.Segmented("##nearbyView", ref selected, labels, width, tips))
        {
            settings.NearbyEverywhere = selected == 1;
            SaveSettings();
        }

        return x;
    }

    /// <summary>The fixed row under the header: the kind chips and Sort, the same in both views.</summary>
    private void DrawKindRow()
    {
        var gap = UiMetrics.Px(6f);
        ReadOnlySpan<string> names = [Strings.NearbyChipReady, Strings.NearbyChipBlues, Strings.NearbyChipSideStories, Strings.NearbyChipRewards];
        ReadOnlySpan<string> tips = [Strings.NearbyChipReadyTip, Strings.NearbyChipBluesTip, Strings.NearbyChipSideStoriesTip, Strings.NearbyChipRewardsTip];
        var kinds = settings.NearbyKinds;
        for (var i = 0; i < KindOrder.Length; i++)
        {
            if (i > 0)
            {
                Chrome.SameLineOrWrap(BoardChips.Width(names[i]) + gap);
            }

            var kind = KindOrder[i];
            if (BoardChips.Chip(KindIds[i], names[i], (kinds & kind) != 0, tips[i]))
            {
                settings.NearbyKinds = kinds ^ kind;
                SaveSettings();
            }
        }

        // Sort, a small dropdown at the row's end.
        var label = string.Format(CultureInfo.CurrentCulture, Strings.NearbySortFormat, SortName(settings.NearbySort));
        var width = ImGui.CalcTextSize(label).X + ImGui.GetFrameHeight() + (ImGui.GetStyle().FramePadding.X * 2f);
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X;
        Chrome.SameLineOrWrap(width + gap);
        var cursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(MathF.Max(cursor.X, right - width), cursor.Y));
        ImGui.SetNextItemWidth(width);
        using (var combo = ImRaii.Combo("##nearbySort", label, ImGuiComboFlags.NoArrowButton | ImGuiComboFlags.HeightSmall))
        {
            if (combo)
            {
                // The list is its own popup window: it scales itself.
                UiMetrics.ApplyFontScale();
                foreach (var sort in (ReadOnlySpan<ZoneSort>)[ZoneSort.LevelFit, ZoneSort.ReadyFirst, ZoneSort.StoryOrder])
                {
                    if (ImGui.Selectable(SortName(sort), settings.NearbySort == sort))
                    {
                        settings.NearbySort = sort;
                        SaveSettings();
                    }
                }
            }
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.NearbySortTip);
        }

        ImGui.Spacing();
    }

    private static string SortName(ZoneSort sort) => sort switch
    {
        ZoneSort.ReadyFirst => Strings.NearbySortReadyFirst,
        ZoneSort.StoryOrder => Strings.NearbySortStoryOrder,
        _ => Strings.NearbySortLevelFit,
    };

    /// <summary>The current job's level and abbreviation; (0, "") without a character.</summary>
    private (int Level, string Job) JobOf(GameData.CatalogBundle? bundle)
    {
        if (session.ViewedSnapshot is not { } snapshot)
        {
            return (0, string.Empty);
        }

        var level = snapshot.JobLevels.TryGetValue(snapshot.CurrentJob, out var l) ? l : (short)0;
        return (level, bundle?.Names.ClassJobAbbreviation(snapshot.CurrentJob) ?? string.Empty);
    }

    // ---------------------------------------------------------------- Here

    /// <summary>Here's rows after the chips (a row stays when any kind it has is on) and in the chosen order.</summary>
    private Row[] HereRows()
    {
        var key = (startable, settings.NearbyKinds, settings.NearbySort, session.Version);
        if (key == hereKey)
        {
            return hereRows;
        }

        hereKey = key;
        var kinds = settings.NearbyKinds;
        var kindsOf = QuestKinds;
        var level = JobOf(session.Bundle).Level;
        var kept = new List<Row>(startable.Length);
        foreach (var row in startable)
        {
            var of = kindsOf?.Invoke(row.Quest) ?? default;
            if (((kinds & ZoneKinds.Ready) != 0)
                || ((kinds & ZoneKinds.Blues) != 0 && of.Blue)
                || ((kinds & ZoneKinds.SideStories) != 0 && of.SideStory)
                || ((kinds & ZoneKinds.Rewards) != 0 && of.Reward))
            {
                kept.Add(row);
            }
        }

        var sort = settings.NearbySort;
        kept.Sort((a, b) =>
        {
            var by = sort switch
            {
                ZoneSort.LevelFit => Math.Abs(a.Quest.DisplayLevel - level).CompareTo(Math.Abs(b.Quest.DisplayLevel - level)),
                ZoneSort.ReadyFirst => (a.State != QuestState.Ready).CompareTo(b.State != QuestState.Ready),
                _ => a.Quest.Journal.SortKey.CompareTo(b.Quest.Journal.SortKey),
            };
            return by != 0 ? by : a.Quest.DisplayLevel != b.Quest.DisplayLevel ? a.Quest.DisplayLevel.CompareTo(b.Quest.DisplayLevel) : a.Quest.RowId.CompareTo(b.Quest.RowId);
        });
        hereRows = [.. kept];
        return hereRows;
    }

    // ---------------------------------------------------------------- Everywhere

    private void RefreshBoard()
    {
        var bundle = session.Bundle;
        var zones = Zones?.Invoke();
        var spoilers = session.Spoilers;
        var key = (session.Version, bundle, Localization.Loc.Version, settings.NearbyKinds, settings.NearbySort, settings.NearbyIncludeOtherJob, (object?)zones, spoilers.Fingerprint, session.ViewedContentId);
        if (key == boardKey)
        {
            return;
        }

        boardKey = key;
        board = [];
        zoneQuests.Clear();
        if (bundle is null || zones is not { Count: > 0 } || session.ViewedSnapshot is not { } snapshot)
        {
            return;
        }

        var catalog = bundle.Catalog;
        var states = session.States;
        var (level, job) = JobOf(bundle);
        fitJob = level > 0 && job.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.ZoneFitsFormat, job, level) : string.Empty;
        var name = snapshot.Name?.Trim() ?? string.Empty;
        firstName = name.IndexOf(' ', StringComparison.Ordinal) is > 0 and var space ? name[..space] : name;
        var patches = PatchIndex.For(catalog);
        newSince = patches.Series.Count > 1 ? string.Format(CultureInfo.CurrentCulture, Strings.ZoneNewSinceFormat, patches.Series[1].Series) : string.Empty;

        // Quests set aside in My blues (P4) leave Nearby: Everywhere's counts and its "…" as Here's list (the session's
        // version moves with the list, so the key above catches a change).
        var setAside = session.ViewedSetAside;
        board = ZoneBoard.Build(
            zones,
            catalog,
            states,
            QuestKinds,
            settings.NearbyKinds,
            settings.NearbySort,
            level,
            settings.NearbyIncludeOtherJob,
            spoilers.ReachExpansion,
            zone => spoilers.IsNameMasked(SpoilerKind.Area, zone),
            newSince.Length > 0 ? patches.IsNew : null,
            setAside);

        // Each zone's quests left, Ready first then lowest level: what "…" goes to, shows and hands off.
        foreach (var quest in catalog.All)
        {
            if (quest.IsRemoved || quest.IsRepeatable || quest.Issuer is not { } issuer || !states.TryGetValue(quest.RowId, out var evaluation)
                || evaluation.State is QuestState.Completed or QuestState.DoneThisCycle || evaluation.LeavesTotals || setAside.Contains(quest.RowId))
            {
                continue;
            }

            if (!zoneQuests.TryGetValue(issuer.TerritoryId, out var list))
            {
                zoneQuests[issuer.TerritoryId] = list = [];
            }

            list.Add(quest);
        }

        foreach (var list in zoneQuests.Values)
        {
            list.Sort((a, b) =>
            {
                var ra = IsReady(a) ? 0 : 1;
                var rb = IsReady(b) ? 0 : 1;
                return ra != rb ? ra.CompareTo(rb) : a.DisplayLevel != b.DisplayLevel ? a.DisplayLevel.CompareTo(b.DisplayLevel) : a.RowId.CompareTo(b.RowId);
            });
        }
    }

    /// <summary>The character on view is the one logged in on this client: the only one travel and hand-offs act for (spec-1.21 decision 5).</summary>
    private bool ViewedLiveHere => session.ViewedContentId is { } viewed && viewed == session.LiveContentId;

    private bool IsReady(QuestRecord quest) =>
        session.States.TryGetValue(quest.RowId, out var evaluation)
        && (evaluation.State == QuestState.Ready || (settings.NearbyIncludeOtherJob && evaluation.State == QuestState.ReadyOnOtherJob));

    private void DrawEverywhere()
    {
        RefreshBoard();
        if (board.Count == 0)
        {
            Chrome.OutlinedText(Strings.ZoneEmpty, Theme.Surface.TextSecondary);
            return;
        }

        var anyLeft = false;
        for (var g = 0; g < board.Count; g++)
        {
            var group = board[g];
            using var id = ImRaii.PushId(group.Expansion);
            if (g > 0)
            {
                ImGui.Spacing();
            }

            anyLeft |= group.Zones.Count > 0;
            DrawGroup(group);
        }

        if (!anyLeft)
        {
            Chrome.OutlinedText(Strings.ZoneEmpty, Theme.Surface.TextSecondary);
        }
    }

    private string ExpansionName(byte expansion) =>
        session.Bundle?.Names.Expansion(expansion) is { Length: > 0 } named ? named : Expansions.Name(expansion);

    private void DrawGroup(ZoneExpansion group)
    {
        var s = Theme.Surface;
        var name = ExpansionName(group.Expansion);
        if (group.Masked)
        {
            // Past the story point: one line, no zone named.
            var line = name + Strings.ZoneSeparator + string.Format(CultureInfo.CurrentCulture, Strings.ZoneMaskedExpansionFormat, group.ZoneCount, firstName);
            Chrome.OutlinedText(line, s.TextSecondary);
            return;
        }

        var open = group.Fits != toggledGroups.Contains(group.Expansion);
        if (!open)
        {
            var left = group.Zones.Count;
            var tail = (settings.NearbyKinds & ZoneKinds.Ready) != 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.ZoneFoldedReadyFormat, group.Ready, left)
                : string.Format(CultureInfo.CurrentCulture, Strings.ZoneFoldedLeftFormat, left);
            if (Chrome.EllipsisSelectable(name + Strings.ZoneSeparator + tail, false, ImGui.GetContentRegionAvail().X, out _))
            {
                Toggle(group.Expansion);
            }

            return;
        }

        // The Eyebrow heading: gold at Full, Text semibold at Quiet and Plain; a click folds the group.
        var heading = string.Format(CultureInfo.CurrentCulture, Strings.ZoneEyebrowFormat, name, group.MinLevel, group.MaxLevel) + (group.Fits ? fitJob : string.Empty);
        var start = ImGui.GetCursorScreenPos();
        if (Theme.MoonRoadArt)
        {
            var label = SectionHeading.Label(heading);
            using (Typography.Eyebrow(label))
            using (Theme.PushText(Theme.StateText(QuestState.Ready)))
            {
                ImGui.TextUnformatted(label);
            }
        }
        else
        {
            Chrome.SemiboldText(heading, s.Text);
        }

        var headingMax = ImGui.GetItemRectMax();
        ImGui.SetCursorScreenPos(start);
        if (ImGui.InvisibleButton("##groupFold", new Vector2(MathF.Max(1f, headingMax.X - start.X), MathF.Max(1f, headingMax.Y - start.Y))))
        {
            Toggle(group.Expansion);
        }

        Chrome.FocusRing();
        var width = MathF.Max(1f, ImGui.GetContentRegionAvail().X - UiMetrics.Px(4f));
        var masked = 0;
        for (var i = 0; i < group.Zones.Count; i++)
        {
            var zone = group.Zones[i];
            if (zone.Masked && masked++ >= MaskedZoneRows)
            {
                continue;
            }

            using var rowId = ImRaii.PushId((int)zone.Zone.TerritoryId);
            DrawZoneRow(zone, width, empty: false);
        }

        if (masked > MaskedZoneRows)
        {
            Chrome.OutlinedText(string.Format(CultureInfo.CurrentCulture, Strings.ZoneMoreAheadFormat, masked - MaskedZoneRows), s.TextSecondary);
        }

        if (group.Empty.Count == 0)
        {
            return;
        }

        // Zones with nothing left: one line, a click lists them (map clearers check them).
        var emptyOpen = openEmpty.Contains(group.Expansion);
        var more = group.Empty.Count == 1 ? Strings.ZoneNothingLeftOne : string.Format(CultureInfo.CurrentCulture, Strings.ZoneNothingLeftFormat, group.Empty.Count);
        if (Chrome.EllipsisSelectable(more, emptyOpen, ImGui.GetContentRegionAvail().X, out _) && !openEmpty.Remove(group.Expansion))
        {
            openEmpty.Add(group.Expansion);
        }

        if (!openEmpty.Contains(group.Expansion))
        {
            return;
        }

        foreach (var zone in group.Empty)
        {
            using var rowId = ImRaii.PushId((int)zone.Zone.TerritoryId);
            DrawZoneRow(zone, width, empty: true);
        }
    }

    private void Toggle(byte expansion)
    {
        if (!toggledGroups.Remove(expansion))
        {
            toggledGroups.Add(expansion);
        }
    }

    private void DrawZoneRow(ZoneLine zone, float width, bool empty)
    {
        var span = zone.MinLevel == zone.MaxLevel
            ? string.Format(CultureInfo.CurrentCulture, Strings.ZoneLevelOneFormat, zone.MinLevel)
            : string.Format(CultureInfo.CurrentCulture, Strings.ZoneLevelSpanFormat, zone.MinLevel, zone.MaxLevel);
        if (zone.Masked)
        {
            // "A zone ahead": the shield's hover and right-click ("Reveal this name" reveals the zone for the session).
            BoardRow.Draw("##zone", Motion.Key(ZoneHoverTag, zone.Zone.TerritoryId), width, 0, null, Strings.ZoneAhead, span, factAfterName: true, chip: null,
                [new BoardRun(Strings.ZoneWaitsForStory)], null, Strings.TeleportToGiver, string.Empty, slot: false,
                shield: new BoardShield(session, SpoilerKind.Area, zone.Zone.Name, Links: links));
            return;
        }

        var name = zone.Zone.Name;
        var line2 = empty ? Strings.ZoneNothingLeftLine : LeftLine(zone);
        var aetheryte = zone.Zone.AetheryteId;
        var aetheryteName = aetheryte != 0 && links.Aetherytes.Find(aetheryte) is { } info ? info.Name : name;
        // Travel only for the character logged in here (spec-1.21 decision 5).
        bool? teleport = links.TeleportShown && ViewedLiveHere ? aetheryte != 0 && links.CanTeleportTo(aetheryte) : null;
        var tip = aetheryte == 0 ? Strings.ZoneNoAetheryte : links.TeleportToBlocked(aetheryte) ?? string.Format(CultureInfo.CurrentCulture, Strings.ZoneTeleportFormat, aetheryteName);
        var result = BoardRow.Draw("##zone", Motion.Key(ZoneHoverTag, zone.Zone.TerritoryId), width, 0, null, name, span, factAfterName: true,
            zone.IsNew && newSince.Length > 0 ? newSince : null, [new BoardRun(line2)], teleport, Strings.TeleportToGiver, tip, slot: true);
        if (result.Teleport)
        {
            links.TeleportTo(aetheryte, aetheryteName);
        }

        if (result.MenuRequested)
        {
            ImGui.OpenPopup(BoardRow.MenuId);
        }

        DrawZoneMenu(zone);
    }

    /// <summary>"4 Ready · 9 blues · 3 rewards": the kinds that are on, with something left.</summary>
    private string LeftLine(ZoneLine zone)
    {
        var kinds = settings.NearbyKinds;
        var text = new StringBuilder();
        void Add(string part)
        {
            if (text.Length > 0)
            {
                text.Append(Strings.ZoneSeparator);
            }

            text.Append(part);
        }

        if ((kinds & ZoneKinds.Ready) != 0 && zone.Ready > 0)
        {
            Add(string.Format(CultureInfo.CurrentCulture, Strings.ZoneReadyFormat, zone.Ready));
        }

        if ((kinds & ZoneKinds.Blues) != 0 && zone.Blues > 0)
        {
            Add(zone.Blues == 1 ? Strings.ZoneBluesOne : string.Format(CultureInfo.CurrentCulture, Strings.ZoneBluesFormat, zone.Blues));
        }

        if ((kinds & ZoneKinds.SideStories) != 0 && zone.SideStories > 0)
        {
            Add(zone.SideStories == 1 ? Strings.ZoneSideStoriesOne : string.Format(CultureInfo.CurrentCulture, Strings.ZoneSideStoriesFormat, zone.SideStories));
        }

        if ((kinds & ZoneKinds.Rewards) != 0 && zone.Rewards > 0)
        {
            Add(zone.Rewards == 1 ? Strings.ZoneRewardsOne : string.Format(CultureInfo.CurrentCulture, Strings.ZoneRewardsFormat, zone.Rewards));
        }

        return text.Length > 0 ? text.ToString() : Strings.ZoneNothingLeftLine;
    }

    /// <summary>"…": Go to the nearest Ready giver, Show in Journal, and at Full hand-offs Send zone to Questionable.</summary>
    private void DrawZoneMenu(ZoneLine zone)
    {
        if (!ImGui.IsPopupOpen(BoardRow.MenuId))
        {
            return;
        }

        using var style = Theme.PushPopup();
        using var popup = ImRaii.Popup(BoardRow.MenuId);
        if (!popup)
        {
            return;
        }

        var quests = zoneQuests.GetValueOrDefault(zone.Zone.TerritoryId) ?? [];
        var live = ViewedLiveHere;
        var nearest = NearestReady(zone, quests);
        if (links.GoToShown && live)
        {
            var go = nearest is null ? default : links.CheckGoTo(nearest);
            if (ImGui.MenuItem(Strings.ZoneMenuNearestReady, string.Empty, false, nearest is not null && go.Ready) && nearest is not null)
            {
                links.GoToGiver(nearest);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                UiMetrics.Tooltip(nearest is null ? Strings.ZoneMenuNearestReadyTip : links.GoToTooltip(nearest, go));
            }
        }

        if (ImGui.MenuItem(Strings.ZoneMenuShowInJournal, string.Empty, false, quests.Count > 0) && quests.Count > 0)
        {
            reveal(quests[0]);
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.ZoneMenuShowInJournalTip);
        }

        if (quests.Count > 0 && live)
        {
            var rowIds = new uint[quests.Count];
            for (var i = 0; i < rowIds.Length; i++)
            {
                rowIds[i] = quests[i].RowId;
            }

            AutomationGate.Questionable(Questionable)?.DrawSubmenu(NearbyQuestionableHost, Strings.ZoneMenuSendQuestionable, rowIds, static rows => rows);
        }
    }

    /// <summary>The zone's Ready quest whose giver stands nearest the zone's aetheryte (where a teleport lands); the first Ready one without it.</summary>
    private QuestRecord? NearestReady(ZoneLine zone, List<QuestRecord> quests)
    {
        var aetheryte = zone.Zone.AetheryteId != 0 ? links.Aetherytes.Find(zone.Zone.AetheryteId) : null;
        QuestRecord? best = null;
        var bestDistance = float.MaxValue;
        foreach (var quest in quests)
        {
            if (!IsReady(quest) || quest.Issuer is not { } issuer)
            {
                continue;
            }

            var distance = aetheryte is null ? 0f : Vector2.DistanceSquared(new Vector2(issuer.X, issuer.Z), new Vector2(aetheryte.X, aetheryte.Z));
            if (best is null || distance < bestDistance)
            {
                best = quest;
                bestDistance = distance;
            }
        }

        return best;
    }

    /// <summary>The Questionable confirmations of Nearby's menus; called once a frame at the window's root.</summary>
    private void DrawNearbyModals() => Questionable?.DrawModals(NearbyQuestionableHost);
}

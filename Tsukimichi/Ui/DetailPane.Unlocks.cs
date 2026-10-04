using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Localization;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Unlocks;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's Unlocks section (feature plan v6 K2), between Rewards and Hand in: what the quest opens, as icon
/// rows grouped like the wiki's "Unlocks" column (Areas · Aetherytes · Duties · Features · Actions), from
/// <see cref="QueryRunner.Unlocks"/>.
/// <list type="bullet">
/// <item>Each row: the game icon on a sunken well the size of a Rewards tile (the veiled moon while none is known), the
/// name over the kind and one fact ("Dungeon · Lv 61"), and a check in a reserved slot only when the game confirms the
/// character has it (a duty unlocked, an aetheryte attuned, a job). Rows keep the Rewards tiles' gap, so Rewards and
/// Unlocks read as a pair. There is no "not yet" mark.</item>
/// <item>One split with Rewards (<see cref="RewardSplit"/>): the quest's own duty, job, action, flying and feature
/// rewards are rows here and never Rewards tiles; what the character keeps (an emote, a mount, a title) is a tile and
/// never a row. Next quests are left to the Path card, which lists the same quests with their moons. A group left
/// empty is not drawn, and a quest left with nothing draws no section at all.</item>
/// <item>At most six rows a group, then "+N more" with the rest in a popup.</item>
/// <item>Clicks: an aetheryte teleports through Lifestream when the game confirms it is attuned, else it is flagged on
/// the map; an area opens the map. A duty's right-click menu opens the Duty Finder on it (a read-only UI call that never
/// queues). Every row has a right-click menu and is a focusable item.</item>
/// <item>The tooltip says how sure a row is: "Likely: you first reach it here" for the first-visit rule's rows, the
/// curated note for curated ones.</item>
/// <item>Spoiler shield: a masked quest's section is one line and nothing else; Sprout mode leaves out rows past the character's reach (<see cref="UnlockView"/>).</item>
/// </list>
/// Rows are rebuilt only when the quest, the session, the index, attunement, the reach or the language changes; their
/// height never changes on hover or as icons load, so nothing moves under the player.
/// </summary>
public sealed partial class DetailPane
{
    private const int UnlocksPerGroup = 6;
    private const string UnlockMenuId = "##unlockMenu";
    private const string UnlockMoreId = "##unlockMore";

    private static readonly string UnlocksIcon = FontAwesomeIcon.Key.ToIconString();

    private readonly List<UnlockGroupView> unlockGroups = [];
    private uint unlocksRowId = uint.MaxValue;
    private int unlocksVersion = -1;
    private int unlocksRevision = -1;
    private int unlocksAttunement = -1;
    private int unlocksText = -1;
    private byte unlocksReach;
    private bool unlocksMasked;
    private bool unlocksAny;
    private string unlocksCaption = string.Empty;

    /// <summary>
    /// Whether the viewed character has a reward-backed unlock (a duty, an emote, a mount): the game's answer, or null
    /// when it cannot tell; null until the plugin attaches it, which leaves those rows without a check.
    /// </summary>
    public Func<UniqueRewardEntry, bool?>? UnlockObtained { get; set; }

    /// <summary>The icon of a reward-backed row the index has none for (the Moonlit resolver); null leaves the stand-in.</summary>
    public Func<QuestRecord?, UniqueRewardEntry, uint>? UnlockIcon { get; set; }

    private sealed class UnlockRowView(UnlockEntry entry, string name, string caption, uint icon, bool confirmed, string? confirmedText)
    {
        public UnlockEntry Entry { get; } = entry;

        public string Name { get; } = name;

        public string Caption { get; } = caption;

        public uint Icon { get; } = icon;

        /// <summary>The game confirms the character has it: the row wears a check.</summary>
        public bool Confirmed { get; } = confirmed;

        /// <summary>What the check means ("Attuned", "Unlocked", "You have it"); null without a check.</summary>
        public string? ConfirmedText { get; } = confirmedText;
    }

    private sealed class UnlockGroupView(UnlockGroup group, string caption)
    {
        public UnlockGroup Group { get; } = group;

        public string Caption { get; } = caption;

        public List<UnlockRowView> Rows { get; } = [];

        /// <summary>The rows past the cap, listed in the "+N more" popup.</summary>
        public List<UnlockRowView> More { get; } = [];

        public string MoreLabel { get; set; } = string.Empty;
    }

    /// <summary>The section; nothing at all for a quest that opens nothing (or nothing within Sprout mode's reach).</summary>
    private void DrawUnlocksSection(SessionState session, QuestRecord quest)
    {
        if (runner.Unlocks is not { } source)
        {
            return;
        }

        RefreshUnlocks(session, quest, source);
        if (!unlocksAny)
        {
            return;
        }

        Gap();
        BeginSection("##unlocks", Strings.UnlocksSection, UnlocksIcon, unlocksMasked ? string.Empty : unlocksCaption, Theme.Surface.TextSecondary);
        if (unlocksMasked)
        {
            TextFlow.Wrapped(Strings.UnlocksMasked, RoomTo(cardRight), Theme.U32(Theme.Surface.TextDisabled));
        }
        else
        {
            DrawUnlockGroups(quest);
        }

        EndSection();
    }

    /// <summary>Rebuilds the rows when an input moved; otherwise a few comparisons.</summary>
    private void RefreshUnlocks(SessionState session, QuestRecord quest, QuestUnlocksSource source)
    {
        var index = source.Current;
        var reach = ui.Filters.Preset == Preset.Sprout ? session.Spoilers.ReachExpansion : byte.MaxValue;
        if (unlocksRowId == quest.RowId && unlocksVersion == session.Version && unlocksRevision == source.Revision
            && unlocksAttunement == links.AttunementRevision && unlocksReach == reach && unlocksText == CoreText.Version)
        {
            return;
        }

        unlocksRowId = quest.RowId;
        unlocksVersion = session.Version;
        unlocksRevision = source.Revision;
        unlocksAttunement = links.AttunementRevision;
        unlocksReach = reach;
        unlocksText = CoreText.Version;
        unlockGroups.Clear();
        unlocksCaption = string.Empty;

        // The index already leaves out what belongs to the Rewards tiles (RewardSplit); next quests are the Path card's.
        var entries = index.For(quest.RowId);
        unlocksMasked = session.Spoilers.IsMasked(quest);
        if (unlocksMasked)
        {
            unlocksAny = false;
            foreach (var entry in entries)
            {
                unlocksAny |= entry.Target != UnlockTarget.NextQuest;
            }

            return;
        }

        var visible = UnlockView.Visible(entries, masked: false, reach);
        var catalog = index.Catalog ?? session.Bundle?.Catalog;
        UnlockGroupView? group = null;
        foreach (var entry in visible)
        {
            if (entry.Target == UnlockTarget.NextQuest)
            {
                continue;
            }

            if (group is null || group.Group != entry.Group)
            {
                group = new UnlockGroupView(entry.Group, UnlockTargets.GroupName(entry.Group));
                unlockGroups.Add(group);
            }

            if (group.Rows.Count >= UnlocksPerGroup)
            {
                group.More.Add(UnlockRow(session, quest, entry, catalog));
                continue;
            }

            group.Rows.Add(UnlockRow(session, quest, entry, catalog));
        }

        foreach (var view in unlockGroups)
        {
            if (view.More.Count > 0)
            {
                view.MoreLabel = string.Format(CultureInfo.CurrentCulture, Strings.UnlocksMoreFormat, view.More.Count);
            }
        }

        unlocksAny = unlockGroups.Count > 0;
        unlocksCaption = UnlockText.Places(visible, 2);
    }

    private UnlockRowView UnlockRow(SessionState session, QuestRecord quest, UnlockEntry entry, QuestCatalog? catalog)
    {
        var name = catalog is null ? entry.Name : UnlockView.NameOf(entry, catalog, session.Spoilers);
        var icon = entry.Icon;
        UniqueRewardEntry? asReward = entry.Reward is { } k && entry.TargetId != 0
            ? new UniqueRewardEntry(quest.RowId, k, entry.TargetId, entry.ItemId, entry.Name, Confidence.Static, string.Empty)
            : null;
        if (icon == 0 && asReward is not null && UnlockIcon is { } resolve)
        {
            icon = resolve(quest, asReward);
        }

        var confirmed = false;
        string? confirmedText = null;
        if (entry.Target == UnlockTarget.Aetheryte)
        {
            confirmed = session.IsLive && links.IsAttunedConfirmed(entry.TargetId);
            confirmedText = confirmed ? Strings.UnlocksAttuned : null;
        }
        else if (asReward is not null && UnlockObtained?.Invoke(asReward) == true)
        {
            confirmed = true;
            confirmedText = entry.Group == UnlockGroup.Duty ? Strings.UnlocksDutyUnlocked : Strings.UnlocksOwned;
        }

        return new UnlockRowView(entry, name, entry.Caption, icon, confirmed, confirmedText);
    }

    private void DrawUnlockGroups(QuestRecord quest)
    {
        for (var g = 0; g < unlockGroups.Count; g++)
        {
            var group = unlockGroups[g];
            using var id = ImRaii.PushId(g);
            using (Typography.Caption())
            {
                TextFlow.Wrapped(group.Caption, RoomTo(cardRight), Theme.U32(Theme.Surface.TextSecondary));
            }

            // The Rewards tiles' gap between rows, so the two sections keep one rhythm.
            using (ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(ImGui.GetStyle().ItemSpacing.X, PairGap)))
            {
                for (var i = 0; i < group.Rows.Count; i++)
                {
                    using var rowId = ImRaii.PushId(i);
                    DrawUnlockRow(quest, group.Rows[i]);
                }
            }

            if (group.MoreLabel.Length > 0)
            {
                DrawUnlockMore(group);
            }
        }
    }

    /// <summary>
    /// One row: the icon on a well the size of a Rewards tile, the name over its caption, and the check slot; an item
    /// the full row wide and one tile high, with the Rewards tiles' gap below it, so the two sections read as a pair
    /// and the layout never moves on hover.
    /// </summary>
    private void DrawUnlockRow(QuestRecord quest, UnlockRowView row)
    {
        var dl = ImGui.GetWindowDrawList();
        var tile = PairTile;
        var iconSize = tile - UiMetrics.Px(8f);
        var lineHeight = ImGui.GetTextLineHeight();
        var height = MathF.Max(tile, 2f * lineHeight);
        var min = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, cardRight - min.X - UiMetrics.Px(4f));
        ImGui.InvisibleButton("##unlock", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left) || (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter, false));
        Keyboard.OpenMenuOnKey(UnlockMenuId);
        var rounding = UiMetrics.Px(6f);
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(width, height), Theme.U32(Theme.Surface.Hover), rounding);
        }

        Chrome.FocusRing(rounding);

        // The icon well, as a Rewards tile draws it: the game icon, or the veiled moon while none is known.
        var tileMin = min + new Vector2(0f, (height - tile) * 0.5f);
        var tileMax = tileMin + new Vector2(tile);
        dl.AddRectFilled(tileMin, tileMax, Theme.U32(Theme.Surface.Sunken), rounding);
        dl.AddRect(tileMin, tileMax, Theme.U32(Theme.Surface.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        var center = (tileMin + tileMax) * 0.5f;
        var iconMin = center - new Vector2(iconSize * 0.5f);
        if (row.Icon == 0 || !GameIcon.DrawAt(dl, textures, row.Icon, iconMin, iconMin + new Vector2(iconSize), UiMetrics.Px(4f)))
        {
            MoonGlyph.DrawVeiled(dl, center, iconSize * 0.32f, 0.6f);
        }

        // The check slot is reserved on every row, drawn only when the game confirms.
        var checkSize = lineHeight;
        var checkMin = new Vector2(min.X + width - checkSize, min.Y + ((height - checkSize) * 0.5f));
        if (row.Confirmed)
        {
            Marks.Draw(dl, checkMin + new Vector2(checkSize * 0.5f), checkSize, Mark.Check);
        }

        // The name over its caption ("Dungeon · Lv 61"), the pair centred on the well.
        var gap = UiMetrics.Px(10f);
        var nameX = tileMax.X + gap;
        var room = MathF.Max(1f, checkMin.X - UiMetrics.Px(4f) - nameX);
        var nameY = min.Y + ((height - (2f * lineHeight)) * 0.5f);
        Chrome.EllipsisTextAt(dl, new Vector2(nameX, nameY), room, row.Name, Theme.U32(Theme.Surface.Text));
        Chrome.EllipsisTextAt(dl, new Vector2(nameX, nameY + lineHeight), room, row.Caption, Theme.U32(Theme.Surface.TextTertiary));

        if (hovered || (ImGui.GetIO().NavVisible && ImGui.IsItemFocused()))
        {
            UnlockTooltip(row);
        }

        if (clicked)
        {
            UnlockClick(row);
        }

        DrawUnlockMenu(row);
    }

    /// <summary>The row's tooltip: the name, the caption, how sure, the check and the click.</summary>
    private void UnlockTooltip(UnlockRowView row)
    {
        using var tooltip = Theme.Tooltip();
        UiMetrics.ApplyFontScale();
        using (UiMetrics.TooltipWrap())
        {
            ImGui.TextUnformatted(row.Name);
            ImGui.TextDisabled(row.Caption);
            switch (row.Entry.Source)
            {
                case UnlockSource.Derived:
                    ImGui.TextDisabled(Strings.UnlocksLikely);
                    break;
                case UnlockSource.Curated when row.Entry.Note is { Length: > 0 } note:
                    ImGui.TextDisabled(note);
                    break;
            }

            if (row.ConfirmedText is { } confirmed)
            {
                using (Theme.PushText(Theme.Accent))
                {
                    ImGui.TextUnformatted(confirmed);
                }
            }

            if (ClickHint(row) is { } hint)
            {
                ImGui.TextDisabled(hint);
            }
        }
    }

    /// <summary>What a left click does on the row, or null when it does nothing.</summary>
    private string? ClickHint(UnlockRowView row) => row.Entry.Target switch
    {
        UnlockTarget.Aetheryte => links.CanTeleportTo(row.Entry.TargetId) ? Strings.UnlocksClickTeleport : Strings.UnlocksClickFlag,
        UnlockTarget.Zone when links.CanOpenMap(row.Entry.PlaceId) => Strings.UnlocksClickMap,
        _ when row.Entry.Group == UnlockGroup.Duty && links.CanOpenDutyFinder(row.Entry.TargetId) => Strings.UnlocksRightClickDuty,
        _ => null,
    };

    private void UnlockClick(UnlockRowView row)
    {
        var entry = row.Entry;
        switch (entry.Target)
        {
            case UnlockTarget.Aetheryte:
                if (!links.TeleportTo(entry.TargetId, entry.Name))
                {
                    links.FlagAetheryte(entry.TargetId);
                }

                break;
            case UnlockTarget.Zone:
                links.OpenMap(entry.PlaceId);
                break;
        }
    }

    /// <summary>The row's right-click menu: what the click does, and the rest (the Duty Finder, the map, coordinates).</summary>
    private void DrawUnlockMenu(UnlockRowView row)
    {
        using var menu = ImRaii.ContextPopupItem(UnlockMenuId);
        if (!menu)
        {
            return;
        }

        var entry = row.Entry;
        switch (entry.Target)
        {
            case UnlockTarget.Aetheryte:
            case UnlockTarget.AethernetShard:
                if (entry.Target == UnlockTarget.Aetheryte && links.TeleportShown)
                {
                    var teleport = string.Format(CultureInfo.CurrentCulture, Strings.UnlocksMenuTeleportFormat, entry.Name);
                    if (ImGui.MenuItem(teleport, string.Empty, false, links.CanTeleportTo(entry.TargetId)))
                    {
                        links.TeleportTo(entry.TargetId, entry.Name);
                    }

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && links.TeleportToBlocked(entry.TargetId) is { } why)
                    {
                        UiMetrics.Tooltip(why);
                    }
                }

                if (ImGui.MenuItem(Strings.FlagOnMap, string.Empty, false, links.Aetherytes.Find(entry.TargetId) is not null))
                {
                    links.FlagAetheryte(entry.TargetId);
                }

                if (ImGui.MenuItem(Strings.UnlocksMenuCopyCoordinates, string.Empty, false, links.Aetherytes.Find(entry.TargetId) is not null)
                    && links.AetheryteCoordinateText(entry.TargetId) is { } coordinates)
                {
                    ImGui.SetClipboardText(coordinates);
                }

                break;
            case UnlockTarget.Zone:
                if (ImGui.MenuItem(Strings.UnlocksMenuOpenMap, string.Empty, false, links.CanOpenMap(entry.PlaceId)))
                {
                    links.OpenMap(entry.PlaceId);
                }

                if (links.TeleportShown && links.ZoneAetheryte(entry.PlaceId) is { } home)
                {
                    var teleport = string.Format(CultureInfo.CurrentCulture, Strings.UnlocksMenuTeleportFormat, home.Name);
                    if (ImGui.MenuItem(teleport, string.Empty, false, links.CanTeleportTo(home.RowId)))
                    {
                        links.TeleportTo(home.RowId, home.Name);
                    }

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled) && links.TeleportToBlocked(home.RowId) is { } why)
                    {
                        UiMetrics.Tooltip(why);
                    }
                }

                break;
            default:
                if (entry.Group == UnlockGroup.Duty)
                {
                    if (ImGui.MenuItem(Strings.UnlocksMenuDutyFinder, string.Empty, false, links.CanOpenDutyFinder(entry.TargetId)))
                    {
                        links.OpenDutyFinder(entry.TargetId);
                    }

                    if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                    {
                        UiMetrics.Tooltip(Strings.UnlocksMenuDutyFinderHint);
                    }
                }

                if (ImGui.MenuItem(Strings.UnlocksMenuCopyName))
                {
                    ImGui.SetClipboardText(row.Name);
                }

                break;
        }

        // Route to unlock (K3): the quests to this thing for the viewed character, whichever quest opens it.
        if (runner.Unlocks?.Current.FindFor(entry) is { } find)
        {
            ImGui.Separator();
            if (ImGui.MenuItem(Strings.RouteToUnlockAction))
            {
                ui.OpenRoute(Core.Route.RouteTarget.ForUnlock(find));
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.RouteToUnlockTooltip);
            }
        }
    }

    /// <summary>"+N more": the rest in a popup.</summary>
    private void DrawUnlockMore(UnlockGroupView group)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, Theme.Surface.TextSecondary);
        if (ImGui.SmallButton(group.MoreLabel))
        {
            ImGui.OpenPopup(UnlockMoreId);
        }

        using var popup = ImRaii.Popup(UnlockMoreId);
        if (!popup)
        {
            return;
        }

        // Each name after the icon its row would wear, a line high, so the list reads like the rows above it.
        var side = ImGui.GetTextLineHeight();
        foreach (var row in group.More)
        {
            var min = ImGui.GetCursorScreenPos();
            ImGui.Dummy(new Vector2(side));
            // An icon the game lacks or is still loading gets GameIcon's own stand-in (with its veiled moon when
            // missing); only a row with no icon at all takes the moon here, so the moon is never drawn twice.
            if (row.Icon == 0)
            {
                MoonGlyph.DrawVeiled(ImGui.GetWindowDrawList(), min + new Vector2(side * 0.5f), side * 0.32f, 0.6f);
            }
            else
            {
                GameIcon.DrawAt(ImGui.GetWindowDrawList(), textures, row.Icon, min, min + new Vector2(side), UiMetrics.Px(2f));
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(row.Name);
            ImGui.SameLine();
            ImGui.TextDisabled(row.Caption);
        }
    }
}

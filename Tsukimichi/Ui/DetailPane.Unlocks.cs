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
/// rows grouped like the wiki's "Unlocks" column (Areas · Aetherytes · Duties · Features · Actions &amp; emotes · Items ·
/// Next quests), from <see cref="QueryRunner.Unlocks"/>.
/// <list type="bullet">
/// <item>Each row: the game icon on a sunken tile (the veiled moon while none is known; a next quest wears its state
/// moon), the name, the kind and one fact on the right ("Dungeon · Lv 61"), and a check in a reserved slot only when
/// the game confirms the character has it (a duty unlocked, an aetheryte attuned, an emote or mount owned). There is no
/// "not yet" mark.</item>
/// <item>At most six rows a group, then "+N more" with the rest in a popup; next quests at most three, then "+N in
/// Path", which scrolls to the Path card. Actions, emotes and items the Rewards tiles already show are left out, and a
/// group left empty is not drawn.</item>
/// <item>Clicks: a next quest is selected; an aetheryte teleports through Lifestream when the game confirms it is
/// attuned, else it is flagged on the map; an area opens the map. A duty's right-click menu opens the Duty Finder on it
/// (a read-only UI call that never queues). Every row has a right-click menu and is a focusable item.</item>
/// <item>The tooltip says how sure a row is: "Likely: you first reach it here" for the first-visit rule's rows, the
/// curated note for curated ones.</item>
/// <item>Spoiler shield: a masked quest's section is one line and nothing else; next quests print through the shield;
/// Sprout mode leaves out rows past the character's reach (<see cref="UnlockView"/>).</item>
/// </list>
/// Rows are rebuilt only when the quest, the session, the index, attunement, the reach or the language changes; their
/// height never changes on hover or as icons load, so nothing moves under the player.
/// </summary>
public sealed partial class DetailPane
{
    private const int UnlocksPerGroup = 6;
    private const int UnlockNextQuestsShown = 3;
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

    private sealed class UnlockRowView(UnlockEntry entry, string name, string caption, uint icon, bool confirmed, string? confirmedText, RewardRef? reward, QuestState state)
    {
        public UnlockEntry Entry { get; } = entry;

        public string Name { get; } = name;

        public string Caption { get; } = caption;

        public uint Icon { get; } = icon;

        /// <summary>The game confirms the character has it: the row wears a check.</summary>
        public bool Confirmed { get; } = confirmed;

        /// <summary>What the check means ("Attuned", "Unlocked", "You have it"); null without a check.</summary>
        public string? ConfirmedText { get; } = confirmedText;

        /// <summary>The quest's own reward for the row, whose tooltip it shows; null for none.</summary>
        public RewardRef? Reward { get; } = reward;

        /// <summary>A next quest's state, for its moon.</summary>
        public QuestState State { get; } = state;
    }

    private sealed class UnlockGroupView(UnlockGroup group, string caption)
    {
        public UnlockGroup Group { get; } = group;

        public string Caption { get; } = caption;

        public List<UnlockRowView> Rows { get; } = [];

        /// <summary>The rows past the cap, listed in the "+N more" popup (next quests: in Path instead).</summary>
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
        BeginSection("##unlocks", Strings.UnlocksSection, UnlocksIcon, unlocksMasked ? string.Empty : unlocksCaption, Theme.Surface.TextTertiary);
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

        var entries = index.For(quest.RowId);
        unlocksMasked = session.Spoilers.IsMasked(quest);
        if (unlocksMasked)
        {
            unlocksAny = entries.Count > 0;
            return;
        }

        var visible = UnlockView.Visible(entries, masked: false, reach);
        var catalog = index.Catalog ?? session.Bundle?.Catalog;
        UnlockGroupView? group = null;
        var nextMore = 0;
        foreach (var entry in visible)
        {
            // Actions, emotes and items the Rewards tiles already show are not repeated.
            if (entry.InRewards && entry.Group is UnlockGroup.ActionEmote or UnlockGroup.Collectable)
            {
                continue;
            }

            if (group is null || group.Group != entry.Group)
            {
                group = new UnlockGroupView(entry.Group, UnlockTargets.GroupName(entry.Group));
                unlockGroups.Add(group);
            }

            var cap = entry.Group == UnlockGroup.NextQuest ? UnlockNextQuestsShown : UnlocksPerGroup;
            if (group.Rows.Count >= cap)
            {
                if (entry.Group == UnlockGroup.NextQuest)
                {
                    nextMore++;
                }
                else
                {
                    group.More.Add(UnlockRow(session, quest, entry, catalog));
                }

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
            else if (view.Group == UnlockGroup.NextQuest && nextMore > 0)
            {
                view.MoreLabel = string.Format(CultureInfo.CurrentCulture, Strings.UnlocksMoreInPathFormat, nextMore);
            }
        }

        unlocksAny = unlockGroups.Count > 0;
        unlocksCaption = UnlockText.Places(visible, 2);
    }

    private UnlockRowView UnlockRow(SessionState session, QuestRecord quest, UnlockEntry entry, QuestCatalog? catalog)
    {
        var name = catalog is null ? entry.Name : UnlockView.NameOf(entry, catalog, session.Spoilers);
        var state = QuestState.Unknown;
        if (entry.Target == UnlockTarget.NextQuest && session.States.TryGetValue(entry.TargetId, out var evaluation))
        {
            state = evaluation.State;
        }

        RewardRef? reward = null;
        if (entry.Reward is { } kind)
        {
            foreach (var r in quest.Rewards)
            {
                if ((entry.ItemId != 0 && r.ItemId == entry.ItemId) || (r.Kind == kind && r.Id == entry.TargetId && entry.TargetId != 0))
                {
                    reward = r;
                    break;
                }
            }
        }

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

        return new UnlockRowView(entry, name, entry.Caption, icon, confirmed, confirmedText, reward, state);
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

            for (var i = 0; i < group.Rows.Count; i++)
            {
                using var rowId = ImRaii.PushId(i);
                DrawUnlockRow(quest, group.Rows[i]);
            }

            if (group.MoreLabel.Length > 0)
            {
                DrawUnlockMore(group);
            }
        }
    }

    /// <summary>One row: an item the full row wide and one icon high, so the layout never moves on hover.</summary>
    private void DrawUnlockRow(QuestRecord quest, UnlockRowView row)
    {
        var dl = ImGui.GetWindowDrawList();
        var iconSize = UiMetrics.Icon(24f);
        var lineHeight = ImGui.GetTextLineHeight();
        var height = MathF.Max(iconSize + UiMetrics.Px(4f), lineHeight);
        var min = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, cardRight - min.X - UiMetrics.Px(4f));
        ImGui.InvisibleButton("##unlock", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked(ImGuiMouseButton.Left) || (ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter, false));
        Keyboard.OpenMenuOnKey(UnlockMenuId);
        var rounding = UiMetrics.Px(4f);
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(width, height), Theme.U32(Theme.Surface.Hover), rounding);
        }

        Chrome.FocusRing(rounding);

        // The icon tile: the game icon, a next quest's moon, or the veiled moon while none is known.
        var tileMin = min + new Vector2(0f, (height - iconSize) * 0.5f);
        var tileMax = tileMin + new Vector2(iconSize);
        dl.AddRectFilled(tileMin, tileMax, Theme.U32(Theme.Surface.Sunken), rounding);
        var center = (tileMin + tileMax) * 0.5f;
        if (row.Entry.Target == UnlockTarget.NextQuest)
        {
            MoonGlyph.Draw(dl, center, iconSize * 0.36f, row.State);
        }
        else if (row.Icon == 0 || !GameIcon.DrawAt(dl, textures, row.Icon, tileMin + new Vector2(UiMetrics.Px(1f)), tileMax - new Vector2(UiMetrics.Px(1f)), rounding))
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

        // The caption on the right, before the check slot, while the name keeps room; else it lives in the tooltip.
        var gap = UiMetrics.Px(8f);
        var textY = min.Y + ((height - lineHeight) * 0.5f);
        var nameX = tileMax.X + gap;
        var right = checkMin.X - UiMetrics.Px(4f);
        var captionWidth = ImGui.CalcTextSize(row.Caption).X;
        var nameRoom = right - nameX;
        if (captionWidth > 0f && nameRoom - captionWidth - gap >= UiMetrics.Px(80f))
        {
            dl.AddText(new Vector2(right - captionWidth, textY), Theme.U32(Theme.Surface.TextTertiary), row.Caption);
            nameRoom -= captionWidth + gap;
        }

        Chrome.EllipsisTextAt(dl, new Vector2(nameX, textY), MathF.Max(1f, nameRoom), row.Name, Theme.U32(Theme.Surface.Text));

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

    /// <summary>The row's tooltip: the reward's own card for a reward, else the name, the caption, how sure, the check and the click.</summary>
    private void UnlockTooltip(UnlockRowView row)
    {
        if (row.Reward is { } reward && row.Entry.Group is UnlockGroup.ActionEmote or UnlockGroup.Collectable)
        {
            RewardTooltip.Draw(reward, links, textures);
            return;
        }

        using var style = Theme.PushTooltip();
        using var tooltip = ImRaii.Tooltip();
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
                using (Theme.PushText(Theme.Moon))
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
        UnlockTarget.NextQuest => Strings.UnlocksClickShowQuest,
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
            case UnlockTarget.NextQuest:
                RevealRow(entry.TargetId);
                break;
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
            case UnlockTarget.NextQuest:
                if (ImGui.MenuItem(Strings.UnlocksMenuShowQuest))
                {
                    RevealRow(entry.TargetId);
                }

                break;
            case UnlockTarget.Aetheryte:
            case UnlockTarget.AethernetShard:
                if (entry.Target == UnlockTarget.Aetheryte)
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

                if (links.ZoneAetheryte(entry.PlaceId) is { } home)
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
    }

    /// <summary>"+N more" (the rest in a popup) or, for next quests, "+N in Path" (scrolls to the Path card).</summary>
    private void DrawUnlockMore(UnlockGroupView group)
    {
        using var color = ImRaii.PushColor(ImGuiCol.Text, Theme.Surface.TextSecondary);
        if (ImGui.SmallButton(group.MoreLabel))
        {
            if (group.Group == UnlockGroup.NextQuest)
            {
                ui.ScrollToPath = true;
            }
            else
            {
                ImGui.OpenPopup(UnlockMoreId);
            }
        }

        if (group.More.Count == 0)
        {
            return;
        }

        using var popup = ImRaii.Popup(UnlockMoreId);
        if (!popup)
        {
            return;
        }

        foreach (var row in group.More)
        {
            ImGui.TextUnformatted(row.Name);
            ImGui.SameLine();
            ImGui.TextDisabled(row.Caption);
        }
    }
}

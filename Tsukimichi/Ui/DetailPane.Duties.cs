using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// "How you'll clear it" (feature plan v7 C7; spec-1.19 C7): a section after Requirements and before Rewards, only for
/// a quest that involves a duty (<see cref="QuestDuties"/>: one it asks to have cleared or one it unlocks). The caption
/// is "1 duty" or "N duties". Each duty is a line, its name in Text and its badges (<see cref="DutyBadgeRules"/>: Solo
/// with NPCs, Group of N, High-end, Story-required or Optional), then the item-level wall when it bites, in the unmet
/// tone: "i110 needed · you're i108 (DRG) · WAR gearset i112" before Patch 8.0, "i110 needed · you're i108" from 8.0
/// (<see cref="ItemLevelRule"/> reads which from the game data), with "Your SGE (i705) also qualifies. Duty Support
/// checks item level too." under it in Tertiary. No Switch gearset button for the wall (spec decision 6). The lines
/// are built when the selection, the session version, the duty index, the duty source or the language changes. For a
/// quest the spoiler shield masks, each duty is named "A duty further along the story" (as the Duties board names it,
/// <see cref="DutyBoardSource.ShownDutyName"/>); its badges and wall stay.
/// </summary>
public sealed partial class DetailPane
{
    /// <summary>One duty of the section as of the last refresh.</summary>
    /// <param name="Hidden">The duty's own name while the spoiler shield hides it (1.20.0 N6: <see cref="Name"/> is its placeholder); null when shown.</param>
    private sealed record ClearRow(string Name, DutyBadges.Look[] Badges, string Wall, string WallNote, string? Hidden = null);

    private readonly List<ClearRow> clearRows = [];
    private string clearCaption = string.Empty;
    private (uint Row, int Version, DutyRunIndex? Index, CatchUpDutySource? Source, int Language) clearKey = (uint.MaxValue, -1, null, null, -1);

    /// <summary>The planning lines, for the catch-up's duty source (which duties the story needs); null leaves the story badge out.</summary>
    public PlanningSource? Planning { get; set; }

    private void RefreshClear(SessionState session, QuestRecord quest)
    {
        var index = DutyRuns?.Invoke();
        var source = Planning?.DutySourceNow;
        var key = (quest.RowId, session.Version, index, source, Localization.Loc.Version);
        if (key == clearKey)
        {
            return;
        }

        clearKey = key;
        clearRows.Clear();
        model.DutyNames.Clear();
        clearCaption = string.Empty;
        if (index is null || session.Bundle is not { } bundle)
        {
            return;
        }

        var duties = QuestDuties.For(quest, index, session.Curated, RewardEntries?.Invoke(quest.RowId));
        if (duties.Count == 0)
        {
            return;
        }

        var story = source is null ? null : StoryRequirements.For(bundle.Catalog, source);
        var rule = ItemLevelRule.For(bundle.Catalog);
        var queues = bundle.DutyJobs();
        QuestRecord[] shownThrough = [quest];
        foreach (var duty in duties)
        {
            var info = duty.Duty;
            bool? storyRequired = story?.IsStoryDuty(info.ContentFinderConditionId, info.InstanceContentId);
            var badges = DutyBadgeRules.For(info, storyRequired).Select(b => DutyBadges.Describe(b, info, index.Roulettes)).ToArray();
            var (wall, note) = WallLines(ItemLevelWall.For(info, session.ViewedSnapshot, rule, queues), info, bundle.Names);
            // A duty the story has not introduced reads as its placeholder (1.20.0 N6), and one shown only through
            // masked quests as the board's stand-in; the badges stay, being generic.
            var hidden = session.Spoilers.IsNameMasked(SpoilerKind.Duty, info.Name);
            clearRows.Add(new ClearRow(DutyBoardSource.ShownDutyName(info, shownThrough, session.Spoilers), badges, wall, note, hidden ? info.Name : null));
            model.DutyNames.Add(info.Name);
        }

        clearCaption = duties.Count == 1
            ? Strings.DutyClearCaptionOne
            : string.Format(CultureInfo.CurrentCulture, Strings.DutyClearCaptionFormat, duties.Count);
    }

    /// <summary>
    /// Whether the wider shield hides a duty the quest involves (as of the last <see cref="RefreshClear"/>): the note
    /// under the hero then offers "Reveal names in this quest", which reveals it with the rest (1.20.0 N6).
    /// </summary>
    private bool ClearDutyHidden()
    {
        foreach (var row in clearRows)
        {
            if (row.Hidden is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The wall's two lines; both empty unless the wall bites. Before 8.0 the first names the current job and the best
    /// gearset that qualifies, the second the next ones that do; from 8.0 neither names a job. The second line adds
    /// "Duty Support checks item level too." for a duty NPCs can clear.
    /// </summary>
    private static (string Wall, string Note) WallLines(ItemLevelWall? wall, DutyRunInfo duty, GameData.GameNames names)
    {
        if (wall is not { Met: false })
        {
            return (string.Empty, string.Empty);
        }

        string line;
        var note = new List<string>(2);
        if (wall.Basis == ItemLevelBasis.CurrentJob)
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.DutyWallJobFormat, wall.Required, wall.Have, names.ClassJobAbbreviation(wall.CurrentJob));
            if (wall.Qualifying.Count > 0)
            {
                var (job, level) = wall.Qualifying[0];
                line += MsqText.Separator + string.Format(CultureInfo.CurrentCulture, Strings.DutyWallGearsetFormat, names.ClassJobAbbreviation(job), level);
            }

            var also = wall.Qualifying.Skip(1).Take(2)
                .Select(q => string.Format(CultureInfo.CurrentCulture, Strings.DutyWallJobLevelFormat, names.ClassJobAbbreviation(q.Job), q.ItemLevel))
                .ToArray();
            if (also.Length > 0)
            {
                note.Add(string.Format(CultureInfo.CurrentCulture, also.Length == 1 ? Strings.DutyWallAlsoOneFormat : Strings.DutyWallAlsoManyFormat, string.Join(Strings.DutyWallAnd, also)));
            }
        }
        else
        {
            line = string.Format(CultureInfo.CurrentCulture, Strings.DutyWallSharedFormat, wall.Required, wall.Have);
        }

        if (duty.OffersDutySupport || duty.OffersTrust)
        {
            note.Add(Strings.DutyWallNpcNote);
        }

        return (line, string.Join(' ', note));
    }

    /// <summary>The section; nothing for a quest that involves no duty the index knows.</summary>
    private void DrawClearSection(SessionState session, QuestRecord quest)
    {
        RefreshClear(session, quest);
        if (clearRows.Count == 0)
        {
            return;
        }

        Gap();
        BeginSection("##howClear", Strings.DutyClearHeading, DutiesIcon, clearCaption, Theme.Surface.TextSecondary);
        foreach (var row in clearRows)
        {
            var cut = Chrome.EllipsisText(row.Name, RoomTo(cardRight), ShieldText.U32(row.Name, Theme.Surface.Text));
            if (row.Hidden is { } hidden)
            {
                ShieldItem(SpoilerKind.Duty, hidden, row.Name, cut ? row.Name : null);
            }
            else if (cut && Dalamud.Bindings.ImGui.ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(row.Name);
            }

            foreach (var badge in row.Badges)
            {
                Chrome.SameLineOrWrap(DutyBadges.Width(badge), cardRight);
                DutyBadges.Draw(badge, textures);
            }

            if (row.Wall.Length > 0)
            {
                TextFlow.Wrapped(row.Wall, RoomTo(cardRight), Theme.U32(Theme.DangerText));
            }

            if (row.WallNote.Length > 0)
            {
                TextFlow.Wrapped(row.WallNote, RoomTo(cardRight), Theme.U32(Theme.Surface.TextTertiary));
            }
        }

        EndSection();
    }
}

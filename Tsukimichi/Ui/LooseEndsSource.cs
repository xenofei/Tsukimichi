using System;
using System.Collections.Generic;
using System.Globalization;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// Loose ends for the panes (feature plan v7 N8; spec-1.21 N8; <see cref="LooseEnds"/>): the storylines over the
/// session's catalog and chains, each line's icon (the job's for a job line, the role's for a role line, the side-quest
/// icon otherwise), and the loose ends of the viewed and of the logged-in character, rebuilt when the session version
/// moves. A line whose next quest is set aside ("Not for me" on the card, P4) is left out. The Characters dashboard's
/// card, the Tonight line, the overlay section and the finale chat line all read it, so they agree. Framework thread only.
/// </summary>
public sealed class LooseEndsSource
{
    private static readonly IReadOnlyList<LooseEnd> None = [];

    private readonly SessionState session;

    private (CatalogBundle? Bundle, ChainCatalog? Chains) linesKey;
    private IReadOnlyList<StoryLine> lines = [];
    private Dictionary<StoryLine, uint> icons = [];

    private (int Version, IReadOnlyList<StoryLine>? Lines, ulong? Viewed) viewedKey = (-1, null, null);
    private IReadOnlyList<LooseEnd> viewed = None;

    private (int Version, IReadOnlyList<StoryLine>? Lines, ulong? Live) liveKey = (-1, null, null);
    private IReadOnlyList<LooseEnd> live = None;

    public LooseEndsSource(SessionState session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>The viewed character's loose ends, in display order (finales to take now first).</summary>
    public IReadOnlyList<LooseEnd> Viewed
    {
        get
        {
            var all = Lines();
            var key = (session.Version, all, session.ViewedContentId);
            if (key != viewedKey)
            {
                viewedKey = key;
                viewed = Find(all, session.States, session.ViewedSetAside);
            }

            return viewed;
        }
    }

    /// <summary>The logged-in character's loose ends: what the finale chat line speaks of whichever character the window shows.</summary>
    public IReadOnlyList<LooseEnd> Live
    {
        get
        {
            var all = Lines();
            var key = (session.Version, all, session.LiveContentId);
            if (key != liveKey)
            {
                liveKey = key;
                live = session.LiveContentId is null ? None : Find(all, session.LiveStates, session.SetAsideOf(session.LiveContentId));
            }

            return live;
        }
    }

    /// <summary>The row's icon: the job's or role's for a job or role line, the side-quest icon otherwise.</summary>
    public uint IconOf(StoryLine line) => icons.TryGetValue(line, out var icon) ? icon : CharactersPane.SideQuestIcon;

    /// <summary>A line's name: a side story's through the shield ("Story: &lt;first quest&gt;"), any other line's own.</summary>
    public string NameOf(StoryLine line, SpoilerMask spoilers)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(spoilers);
        return session.Bundle is { } bundle
            ? ChainCatalog.DisplayName(line.Chain, id => spoilers.DisplayName(bundle.Catalog, id, id.ToString(CultureInfo.InvariantCulture)))
            : line.Chain.Name;
    }

    /// <summary>
    /// The next quest's name on a row: the shield's placeholder for a masked main scenario quest, "Job quest ahead (Lv 80)"
    /// or "Sidequest (Lv 90)" for another quest past the story point, the name otherwise.
    /// </summary>
    public static string NextName(LooseEnd end, SpoilerMask spoilers, out bool ahead)
    {
        ArgumentNullException.ThrowIfNull(end);
        ArgumentNullException.ThrowIfNull(spoilers);
        ahead = spoilers.IsAhead(end.Next.RowId);
        if (!ahead || spoilers.IsMasked(end.Next))
        {
            return spoilers.DisplayName(end.Next);
        }

        return string.Format(
            CultureInfo.CurrentCulture,
            end.Line.Kind is StoryLineKind.Job or StoryLineKind.Role ? Strings.LooseEndsJobAheadFormat : Strings.StoriesSideAheadFormat,
            end.Next.DisplayLevel);
    }

    /// <summary>The loose ends over <paramref name="states"/>, leaving out lines whose next quest is set aside ("Not for me", P4).</summary>
    private IReadOnlyList<LooseEnd> Find(IReadOnlyList<StoryLine> all, IReadOnlyDictionary<uint, QuestEvaluation> states, IReadOnlySet<uint> setAside)
    {
        if (session.Bundle is not { } bundle || states.Count == 0 || all.Count == 0)
        {
            return None;
        }

        return LooseEnds.Find(all, states, bundle.Catalog, setAside);
    }

    private IReadOnlyList<StoryLine> Lines()
    {
        var bundle = session.Bundle;
        var chains = session.Chains;
        if (ReferenceEquals(linesKey.Bundle, bundle) && ReferenceEquals(linesKey.Chains, chains))
        {
            return lines;
        }

        linesKey = (bundle, chains);
        lines = [];
        icons = [];
        if (bundle is null)
        {
            return lines;
        }

        lines = LooseEnds.Lines(bundle.Catalog, chains);
        var ladder = bundle.BuildJobLadder();
        foreach (var line in lines)
        {
            if (line.Kind is StoryLineKind.Job or StoryLineKind.Role && line.Chain.RowIds.Count > 0
                && bundle.Catalog.GetByRowId(line.Chain.RowIds[^1]) is { } quest && LineIcon(quest, line.Kind, bundle, ladder) is var icon and not 0)
            {
                icons[line] = icon;
            }
        }

        return lines;
    }

    /// <summary>
    /// A job line's job icon (the one job its quests are for), a role line's role icon (the one role its quests admit);
    /// 0 when the quests name no single job or role.
    /// </summary>
    private static uint LineIcon(QuestRecord quest, StoryLineKind kind, CatalogBundle bundle, JobLadder ladder)
    {
        if (kind == StoryLineKind.Job)
        {
            if (quest.ClassJobRequired is > 0 and <= byte.MaxValue)
            {
                return MoonlitIconResolver.ClassJobIconBase + quest.ClassJobRequired;
            }

            byte single = 0;
            var count = 0;
            foreach (var job in bundle.Jobs.JobsIn(quest.ClassJobCategory))
            {
                single = job;
                count++;
            }

            return count == 1 ? MoonlitIconResolver.ClassJobIconBase + single : 0;
        }

        JobRole? role = null;
        foreach (var job in bundle.Jobs.JobsIn(quest.ClassJobCategory))
        {
            if (ladder.RoleOf(job) is not { } of)
            {
                continue;
            }

            if (role is { } seen && seen != of)
            {
                return 0;
            }

            role = of;
        }

        return role is { } only ? PaneIcons.Role(only) : 0;
    }
}

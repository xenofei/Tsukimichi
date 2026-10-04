using System;
using System.Collections.Generic;
using System.Globalization;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Side stories card on the Characters dashboard (feature plan v7 P5; spec-1.21 P5), where the story chains table
/// was: every curated line (<c>chains.json</c>) named as players name it, in two-line rows (<see cref="DrawStoryRows"/>):
/// "3 left · next: Forever in Our Hearts · Ready", an ongoing series with every released quest done "Caught up ·
/// continues in a later patch" in silver, and the finished lines folded into "7 lines done ›". The caption counts what is
/// left ("3 lines to go"). A line that opens past the story point reads "A side story ahead · opens in a later
/// expansion · name hidden"; a next quest past it reads "Sidequest (Lv 90)". The order and statuses are
/// <see cref="SideStories"/>'; rows are built when the session, the shield or the language change.
/// </summary>
public sealed partial class CharactersPane
{
    /// <summary>Rows the card shows before "N more ›".</summary>
    private const int SideRowsShown = 6;

    private (int Version, CatalogBundle? Bundle, int Language, ulong? Viewed, int Shield, ChainCatalog? Chains) sideKey = (-1, null, -1, null, 0, null);
    private StoryRowView[] sideRows = [];
    private StoryRowView[] sideDone = [];
    private string sideCaption = string.Empty;
    private string sideDoneLabel = string.Empty;
    private bool sideAll;
    private bool sideDoneOpen;

    private void DrawSideStories(UiState ui)
    {
        RefreshSideStories();
        using var id = ImRaii.PushId("sideStories");
        SectionHeading.Draw(Strings.StoriesSection, sideCaption.Length > 0 ? sideCaption : null);
        if (sideRows.Length == 0 && sideDone.Length == 0)
        {
            ImGui.TextDisabled(Strings.StoriesNone);
            return;
        }

        DrawStoryRows(ui, "##lines", sideRows, sideAll ? sideRows.Length : Math.Min(SideRowsShown, sideRows.Length));
        DrawMoreToggle(sideRows.Length - SideRowsShown, ref sideAll);
        if (sideDone.Length == 0)
        {
            return;
        }

        if (Chrome.EllipsisSelectable(sideDoneOpen ? Strings.StoriesDoneHide : sideDoneLabel, false, 0f, out _))
        {
            sideDoneOpen = !sideDoneOpen;
        }

        if (sideDoneOpen)
        {
            DrawStoryRows(ui, "##done", sideDone, sideDone.Length);
        }
    }

    private void RefreshSideStories()
    {
        var bundle = session.Bundle;
        var spoilers = session.Spoilers;
        var key = (session.Version, bundle, Localization.Loc.Version, session.ViewedContentId, spoilers.Fingerprint, session.Chains);
        if (key == sideKey)
        {
            return;
        }

        sideKey = key;
        sideRows = [];
        sideDone = [];
        sideCaption = string.Empty;
        var states = session.States;
        if (bundle is null || states.Count == 0)
        {
            return;
        }

        var rows = new List<StoryRowView>();
        var done = new List<StoryRowView>();
        var toGo = 0;
        foreach (var line in SideStories.Build(session.Chains, states, spoilers.IsAhead))
        {
            var chain = line.Chain;
            switch (line.Status)
            {
                case SideStoryStatus.Finished:
                    done.Add(new StoryRowView(SideQuestIcon, chain.Name, null, false, Strings.StoriesFinished, null, string.Empty, false, QuestState.Completed, false, string.Empty, chain.RowIds, RecapQuestOf(session.Chains, chain, states)));
                    continue;
                case SideStoryStatus.CaughtUp:
                    rows.Add(new StoryRowView(SideQuestIcon, chain.Name, null, true, Strings.StoriesContinues, null, string.Empty, false, QuestState.Completed, false, string.Empty, chain.RowIds, RecapQuestOf(session.Chains, chain, states)));
                    continue;
            }

            toGo++;
            if (line.Ahead)
            {
                var first = bundle.Catalog.GetByRowId(chain.RowIds[0]);
                var later = first is not null && first.Expansion > spoilers.ReachExpansion && spoilers.ReachExpansion != byte.MaxValue;
                rows.Add(new StoryRowView(SideQuestIcon, Strings.StoriesAhead, null, false, later ? Strings.StoriesAheadExpansion : Strings.StoriesAheadStory, null, string.Empty, false, QuestState.Unknown, true, string.Empty, [])
                {
                    // The placeholder's reveal is the line's first quest's: once revealed it is no longer ahead.
                    Shield = first is null ? null : new StoryShield(chain.Name, first, OnName: true),
                });
                continue;
            }

            var next = line.Progress.NextRowId is { } nextRowId ? bundle.Catalog.GetByRowId(nextRowId) : null;
            var lead = string.Format(CultureInfo.CurrentCulture, Strings.StoriesLeftNextFormat, line.Left);
            if (next is null)
            {
                rows.Add(new StoryRowView(SideQuestIcon, chain.Name, null, false, lead, null, string.Empty, false, QuestState.Unknown, false, string.Empty, chain.RowIds));
                continue;
            }

            var state = states.TryGetValue(next.RowId, out var evaluation) ? evaluation.State : QuestState.Unknown;
            var veiled = spoilers.IsAhead(next.RowId);
            var name = veiled ? AheadName(next, StoryLineKind.Chain) : spoilers.DisplayName(next);
            var tail = veiled ? string.Empty : Strings.StateReasonSeparator + Strings.StateName(state, next);
            rows.Add(new StoryRowView(SideQuestIcon, chain.Name, null, false, lead, next, name, false, state, veiled, tail, chain.RowIds, RecapQuestOf(session.Chains, chain, states))
            {
                Shield = veiled ? new StoryShield(next.Name, next, OnName: false) : null,
            });
        }

        sideRows = rows.ToArray();
        sideDone = done.ToArray();
        sideCaption = toGo == 0 ? string.Empty : toGo == 1 ? Strings.StoriesToGoOne : string.Format(CultureInfo.CurrentCulture, Strings.StoriesToGoFormat, toGo);
        sideDoneLabel = sideDone.Length == 1 ? Strings.StoriesDoneOne : string.Format(CultureInfo.CurrentCulture, Strings.StoriesDoneFormat, sideDone.Length);
    }

    /// <summary>
    /// What a quest past the story point is called on a storyline row: a masked main scenario quest by the shield's own
    /// placeholder, a job or role quest "Job quest ahead (Lv 80)", any other "Sidequest (Lv 90)".
    /// </summary>
    private string AheadName(QuestRecord quest, StoryLineKind kind)
    {
        if (session.Spoilers.IsMasked(quest))
        {
            return session.Spoilers.DisplayName(quest);
        }

        return string.Format(CultureInfo.CurrentCulture, kind is StoryLineKind.Job or StoryLineKind.Role ? Strings.LooseEndsJobAheadFormat : Strings.StoriesSideAheadFormat, quest.DisplayLevel);
    }
}

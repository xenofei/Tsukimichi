using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Plan;
using Tsukimichi.Core.Query;
using StoryLine = Tsukimichi.Core.Plan.StoryLine;

namespace Tsukimichi.Ui;

/// <summary>
/// Your story on one page (feature plan v7 N9, spec-1.21): My blues' second view. The left column holds the title, the
/// pace line (always an estimate, and a plain count below <see cref="StoryPace.MinDated"/> dated story quests) and
/// Copy as checklist; the centre the patch bands (<see cref="StoryPage"/>), each with what is left and its optional
/// lines placed under the main scenario quest that opens them. Finished bands fold to one line; bands past the story
/// point show counts only, and every name goes through the spoiler shield. Built on the session's version.
/// </summary>
public sealed partial class PlanPane
{
    private (int Version, ChainCatalog? Chains, IReadOnlySet<uint>? Story, int Language) storyKey = (-1, null, null, -1);
    private StoryPage? storyPage;
    private StoryPaceEstimate storyPace = new(0, 0, 0);
    private string paceText = string.Empty;

    // Finished bands the player opened (by index), and the "Copied" note.
    private readonly HashSet<int> openBands = [];
    private double storyCopiedAt = double.NegativeInfinity;

    /// <summary>The page for the viewed character, rebuilt when the session, the chains or the story's side quests move.</summary>
    private StoryPage? RefreshStory()
    {
        if (session.Bundle is not { } bundle)
        {
            storyPage = null;
            return null;
        }

        var story = source.StoryRequired;
        var key = (session.Version, (ChainCatalog?)session.Chains, (IReadOnlySet<uint>?)story, Localization.Loc.Version);
        if (storyPage is not null && key == storyKey)
        {
            return storyPage;
        }

        storyKey = key;
        var spoilers = session.Spoilers;
        var names = session.Names;
        storyPage = StoryPage.Build(new StoryPageInputs(bundle.Catalog, session.Chains, session.States, spoilers.IsMasked, names.QuestName, names.Expansion, story));
        var dated = StoryPace.DatedStoryQuests(MsqGraph.For(bundle.Catalog).Story, session.ViewedSnapshot);
        storyPace = StoryPace.Compute(dated, TimeZoneInfo.Local);
        paceText = PaceText(storyPage, storyPace, session.ViewedSnapshot is not null);
        return storyPage;
    }

    /// <summary>The pace line (spec-1.21 N9 item 2).</summary>
    private static string PaceText(StoryPage page, StoryPaceEstimate pace, bool character)
    {
        if (page.StoryLeft == 0)
        {
            return Strings.StoryCaughtUp;
        }

        var culture = CultureInfo.CurrentCulture;
        if (!character)
        {
            return string.Format(culture, Strings.StoryPaceNoCharacterFormat, page.StoryLeft, page.MinLevelLeft, page.MaxLevelLeft);
        }

        if (pace.EveningsFor(page.StoryLeft) is not { } evenings)
        {
            return string.Format(culture, Strings.StoryPaceThinFormat, page.StoryLeft, page.MinLevelLeft, page.MaxLevelLeft, StoryPace.MinDated, pace.Dated);
        }

        return evenings <= 1
            ? string.Format(culture, Strings.StoryPaceOneEveningFormat, page.StoryLeft, page.MinLevelLeft, page.MaxLevelLeft)
            : string.Format(culture, Strings.StoryPaceFormat, page.StoryLeft, page.MinLevelLeft, page.MaxLevelLeft, evenings);
    }

    /// <summary>Left column of Your story: the title, the pace line and Copy as checklist.</summary>
    private void DrawStoryLeft(UiState ui)
    {
        _ = ui;
        using (Typography.Display())
        {
            ImGui.TextUnformatted(Strings.StoryTitle);
        }

        if (RefreshStory() is not { } page)
        {
            ImGui.TextDisabled(Strings.StoryLoading);
            return;
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(paceText);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.StoryPaceTooltip);
        }

        ImGui.Spacing();
        if (ImGui.Button(Strings.PlanCopy))
        {
            ImGui.SetClipboardText(StoryChecklist(page));
            storyCopiedAt = ImGui.GetTime();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.StoryCopyTooltip);
        }

        // The note's slot is always there, so the line never appears under the pointer.
        ImGui.SameLine();
        var alpha = copiedFade.Alpha(ImGui.GetTime() - storyCopiedAt < CopiedSeconds ? Strings.StoryCopied : null);
        var ink = Theme.Surface.TextSecondary;
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.WithAlpha(ink, ink.W * alpha)))
        {
            ImGui.TextUnformatted(Strings.StoryCopied);
        }
    }

    /// <summary>Centre column of Your story: the bands.</summary>
    private void DrawStoryMain(UiState ui)
    {
        if (RefreshStory() is not { } page)
        {
            ImGui.TextDisabled(Strings.StoryLoading);
            return;
        }

        using var list = ImRaii.Child("##storyBands", Vector2.Zero);
        if (!list)
        {
            return;
        }

        for (var b = 0; b < page.Bands.Count; b++)
        {
            DrawBand(ui, page.Bands[b], b);
            ImGui.Spacing();
        }
    }

    /// <summary>"A Realm Reborn · 2.0", "2.1 – 2.5 · Seventh Astral Era"; past the story point the patch title goes.</summary>
    private static string BandTitle(StoryBand band)
    {
        var culture = CultureInfo.CurrentCulture;
        if (band.FirstOfExpansion)
        {
            return band.Patches.Length > 0 ? string.Format(culture, Strings.StoryBandTitleFormat, band.ExpansionName, band.Patches) : band.ExpansionName;
        }

        if (band.Past)
        {
            return band.Patches.Length > 0 ? band.Patches : band.ExpansionName;
        }

        return band.Patches.Length > 0 ? string.Format(culture, Strings.StoryBandTitleFormat, band.Patches, band.Title) : band.Title;
    }

    /// <summary>What a band's header says on the right: what is left, done, or (past the story point) counts.</summary>
    private static string BandCaption(StoryBand band)
    {
        var culture = CultureInfo.CurrentCulture;
        if (band.Past)
        {
            return string.Format(culture, Strings.StoryBandPastFormat, band.StoryTotal, band.LineCount);
        }

        if (band.IsDone)
        {
            return band.LinesLeft > 0 ? string.Format(culture, Strings.StoryBandDoneLinesFormat, band.LinesLeft) : Strings.StoryBandDone;
        }

        return string.Format(culture, Strings.StoryBandLeftFormat, band.StoryLeft, band.NextName);
    }

    private void DrawBand(UiState ui, StoryBand band, int index)
    {
        Chrome.BeginCard(3000 + index);
        var folded = band.IsDone && !band.Past && !openBands.Contains(index);
        var title = BandTitle(band);
        var caption = BandCaption(band);
        var start = ImGui.GetCursorScreenPos();
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f);
        var width = MathF.Max(1f, right - start.X);
        var line = ImGui.GetTextLineHeight();
        var dl = ImGui.GetWindowDrawList();

        // The header: a finished band's whole line opens or folds it.
        var hovered = false;
        if (band.IsDone && !band.Past)
        {
            if (ImGui.InvisibleButton("##band", new Vector2(width, line)))
            {
                if (!openBands.Remove(index))
                {
                    openBands.Add(index);
                }
            }

            hovered = ImGui.IsItemHovered();
            Chrome.FocusRing();
            ImGui.SetCursorScreenPos(start);
        }

        var captionWidth = ImGui.CalcTextSize(caption).X;
        var captionRoom = MathF.Min(captionWidth, width * 0.55f);
        var titleRoom = MathF.Max(1f, width - captionRoom - UiMetrics.Px(12f));
        var cut = Chrome.EllipsisTextAt(dl, start, titleRoom, title, Theme.U32(hovered ? Theme.Surface.Text : band.IsDone ? Theme.Surface.TextSecondary : Theme.Surface.Text));
        var captionCut = Chrome.EllipsisTextAt(dl, new Vector2(right - captionRoom, start.Y), captionRoom, caption, Theme.U32(Theme.Surface.TextSecondary), captionWidth);
        if (hovered || ((cut || captionCut) && ImGui.IsMouseHoveringRect(start, start + new Vector2(width, line))))
        {
            UiMetrics.Tooltip(title, band.IsDone && !band.Past ? caption + "\n" + Strings.StoryBandDoneTooltip : caption);
        }

        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + line));
        ImGui.Dummy(new Vector2(width, UiMetrics.Px(2f)));
        if (band.Past)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(Strings.StoryNamesHidden);
            }
        }
        else if (!folded)
        {
            foreach (var group in band.Groups)
            {
                DrawStoryGroup(ui, group);
            }
        }

        Chrome.EndCard();
    }

    private void DrawStoryGroup(UiState ui, StoryGroup group)
    {
        ImGui.Spacing();
        var opens = group.OpensAfter is null ? Strings.StoryOpenFromStart : string.Format(CultureInfo.CurrentCulture, Strings.StoryOpensAfterFormat, group.OpensAfterName);
        using (Typography.Caption())
        {
            Chrome.FitText(opens, Theme.U32(Theme.Surface.TextSecondary));
        }

        if (group.Hidden)
        {
            using (Theme.PushText(Theme.Surface.TextSecondary))
            {
                ImGui.TextUnformatted(HiddenLines(group.Lines.Count));
            }

            return;
        }

        foreach (var storyLine in group.Lines)
        {
            DrawStoryLine(ui, storyLine);
        }
    }

    private static string HiddenLines(int count) =>
        count == 1 ? Strings.StoryHiddenLineOne : string.Format(CultureInfo.CurrentCulture, Strings.StoryHiddenLinesFormat, count);

    private static string LineState(StoryLine storyLine, string nextName) =>
        storyLine.IsDone ? Strings.StoryLineDone
        : storyLine.InJournal ? string.Format(CultureInfo.CurrentCulture, Strings.StoryLineInJournalFormat, storyLine.Left)
        : string.Format(CultureInfo.CurrentCulture, Strings.StoryLineLeftFormat, storyLine.Left, nextName);

    /// <summary>One optional line: its moon, its name (a click shows its next quest), "Story needs it", and what is left.</summary>
    private void DrawStoryLine(UiState ui, StoryLine storyLine)
    {
        var line = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(line);
        var height = MathF.Max(ImGui.GetFrameHeight(), glyph) + UiMetrics.Px(2f);
        var start = ImGui.GetCursorScreenPos();
        var right = ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - UiMetrics.Px(10f);
        var width = MathF.Max(1f, right - start.X);
        var size = new Vector2(width, height);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        var target = storyLine.Next ?? (session.Bundle?.Catalog.GetByRowId(storyLine.Chain.RowIds[0]));
        using var id = ImRaii.PushId((int)(target?.RowId ?? 0));
        var gap = UiMetrics.Px(8f);
        var textY = start.Y + ((height - line) * 0.5f);
        var dl = ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + ((height - glyph) * 0.5f)));
        MoonGlyph.DrawInline(storyLine.IsDone ? QuestState.Completed : storyLine.NextState, glyph);

        var nextName = storyLine.Next is { } next ? session.Names.QuestName(next) : string.Empty;
        var state = LineState(storyLine, nextName);
        var stateWidth = ImGui.CalcTextSize(state).X;
        var stateRoom = MathF.Min(stateWidth, width * 0.5f);
        var nameX = start.X + glyph + gap;
        var chipLabel = UnlockTiers.Name(UnlockTier.StoryNeedsIt);
        float chipWidth;
        using (Typography.Caption())
        {
            chipWidth = storyLine.StoryNeedsIt ? TierChipSize(chipLabel).X + gap : 0f;
        }

        var nameWidth = ImGui.CalcTextSize(storyLine.Name).X;
        var nameRoom = MathF.Max(1f, MathF.Min(nameWidth, right - stateRoom - gap - chipWidth - nameX));
        ImGui.SetCursorScreenPos(new Vector2(nameX, textY));
        if (Chrome.EllipsisSelectable(storyLine.Name, target is not null && ui.SelectedRowId == target.RowId, nameRoom, out _) && target is not null)
        {
            ui.SelectedRowId = target.RowId;
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(storyLine.Name, state);
        }

        if (storyLine.StoryNeedsIt)
        {
            DrawTierChip(dl, chipLabel, nameX + nameRoom + chipWidth, start.Y, height);
        }

        Chrome.EllipsisTextAt(dl, new Vector2(right - stateRoom, textY), stateRoom, state, Theme.U32(Theme.Surface.TextSecondary), stateWidth);
        ImGui.SetCursorScreenPos(start);
        ImGui.Dummy(size);
    }

    /// <summary>The page as plain text (Copy as checklist), names hidden the same way.</summary>
    private string StoryChecklist(StoryPage page)
    {
        var text = new StringBuilder();
        text.AppendLine(Strings.StoryTitle);
        text.AppendLine(paceText);
        foreach (var band in page.Bands)
        {
            text.AppendLine();
            text.Append(BandTitle(band)).Append(Core.Evaluation.BlockerText.Separator).AppendLine(BandCaption(band));
            if (band.Past)
            {
                text.Append("  ").AppendLine(Strings.StoryNamesHidden);
                continue;
            }

            foreach (var group in band.Groups)
            {
                var opens = group.OpensAfter is null ? Strings.StoryOpenFromStart : string.Format(CultureInfo.CurrentCulture, Strings.StoryOpensAfterFormat, group.OpensAfterName);
                text.Append("  ").AppendLine(opens);
                if (group.Hidden)
                {
                    text.Append("    ").AppendLine(HiddenLines(group.Lines.Count));
                    continue;
                }

                foreach (var storyLine in group.Lines)
                {
                    var nextName = storyLine.Next is { } next ? session.Names.QuestName(next) : string.Empty;
                    text.Append("    ").Append(storyLine.IsDone ? "- [x] " : "- [ ] ").Append(storyLine.Name);
                    if (storyLine.StoryNeedsIt)
                    {
                        text.Append(Core.Evaluation.BlockerText.Separator).Append(UnlockTiers.Name(UnlockTier.StoryNeedsIt));
                    }

                    text.Append(Core.Evaluation.BlockerText.Separator).AppendLine(LineState(storyLine, nextName));
                }
            }
        }

        return text.ToString();
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Jobs;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Prep;
using Tsukimichi.Core.Seasonal;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// "Before Evercold" (feature plan v7, 1.20.0, N7) for the viewed character, live or a stored alt read from its
/// snapshot: what to finish before Patch 8.0 (<see cref="BeforeEvercold"/>), drawn as a card in Tonight and as a
/// section of the Characters dashboard. Each line is checked off live (done lines keep their place, with the done
/// check, so nothing moves), its reason and source are its tooltip, and a click acts: it selects the quest, or from
/// Tonight opens the Characters tab, where Make room is. Quest names go through the spoiler shield. Hide takes the card
/// off Tonight for that character, with Undo; the dashboard keeps the list and can put it back. The card retires by
/// itself on Evercold's data or date. Rebuilt when the session, the hidden cards, the language or the minute change;
/// drawing allocates nothing. Framework thread only.
/// </summary>
public sealed class BeforeEvercoldCard
{
    private readonly SessionState session;
    private readonly CharacterSettingsBook characters;

    private (int Version, int Book, long Minute, int Language) key = (-1, -1, -1, -1);
    private CatalogBundle? ladderBundle;
    private JobLadder ladder = JobLadder.Empty;

    private LineView[] lines = [];
    private bool active;
    private bool dismissed;
    private string expected = string.Empty;
    private string header = string.Empty;

    /// <param name="Title">The line's words ("Main scenario through Patch 7.56").</param>
    /// <param name="Hint">What is left, under it ("12 quests left · next: …"), or how it stands once done.</param>
    /// <param name="Tooltip">The reason and its source.</param>
    /// <param name="Target">The quest a click selects; null when there is none.</param>
    private sealed record LineView(PrepKind Kind, bool Done, string Title, string Hint, string Tooltip, QuestRecord? Target);

    public BeforeEvercoldCard(SessionState session, CharacterSettingsBook characters)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.characters = characters ?? throw new ArgumentNullException(nameof(characters));
    }

    /// <summary>Whether Tonight shows the card for the viewed character: not retired, not hidden, a character in view.</summary>
    public bool ShowsOnTonight
    {
        get
        {
            Refresh();
            return active && !dismissed;
        }
    }

    /// <summary>Whether the dashboard shows the section: not retired and a character in view (hidden or not).</summary>
    public bool Active
    {
        get
        {
            Refresh();
            return active;
        }
    }

    /// <summary>The Tonight card: the title, the expected date, the lines, and Hide.</summary>
    public void DrawCard(UiState ui, float wrap)
    {
        ArgumentNullException.ThrowIfNull(ui);
        Refresh();
        using var id = ImRaii.PushId("beforeEvercoldCard");
        var s = Theme.Surface;

        // Inside the card every part starts at the card's inner left, not the window's.
        var left = ImGui.GetCursorScreenPos().X;
        Chrome.SemiboldTextWrapped(Strings.PrepTitle, s.Text, wrap);
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
        DrawExpected(wrap);
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(4f)));
        DrawLines(ui, onDashboard: false, wrap);
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(4f)));
        if (ImGui.SmallButton(Strings.PrepHide))
        {
            SetDismissed(true);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PrepHideTooltip);
        }
    }

    /// <summary>
    /// The dashboard's section: a header saying what is left ("Before Evercold · 2 to do"), open by itself while
    /// something is left and the card is not hidden; inside, the expected date, the lines and Hide from Tonight or
    /// Show on Tonight.
    /// </summary>
    public void DrawSection(UiState ui)
    {
        ArgumentNullException.ThrowIfNull(ui);
        Refresh();
        if (!active)
        {
            return;
        }

        using var id = ImRaii.PushId("beforeEvercoldSection");
        var flags = !dismissed && Array.Exists(lines, static l => !l.Done) ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None;
        var open = ImGui.CollapsingHeader(header, flags);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PrepSectionTooltip);
        }

        if (!open)
        {
            return;
        }

        var wrap = MathF.Max(1f, ImGui.GetContentRegionAvail().X);
        DrawExpected(wrap);
        DrawLines(ui, onDashboard: true, wrap);
        if (ImGui.SmallButton(dismissed ? Strings.PrepShowOnTonight : Strings.PrepHideFromTonight))
        {
            SetDismissed(!dismissed);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(dismissed ? Strings.PrepShowOnTonightTooltip : Strings.PrepHideTooltip);
        }
    }

    private void DrawExpected(float wrap)
    {
        using (Typography.Caption())
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrap);
            ImGui.TextWrapped(expected);
            ImGui.PopTextWrapPos();
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.PrepExpectedTooltip);
        }
    }

    /// <summary>
    /// One row per line: the done check in a fixed box (empty while left, so the words never shift), the title as a
    /// selectable that acts, and the hint under it.
    /// </summary>
    private void DrawLines(UiState ui, bool onDashboard, float wrap)
    {
        var s = Theme.Surface;
        var lineHeight = ImGui.GetTextLineHeight();
        var glyph = UiMetrics.InlineGlyphSize(lineHeight);
        var gap = ImGui.GetStyle().ItemSpacing.X;
        var left = ImGui.GetCursorScreenPos().X;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            using var lineId = ImRaii.PushId(i);
            var at = ImGui.GetCursorScreenPos();
            if (line.Done)
            {
                Marks.Draw(ImGui.GetWindowDrawList(), new Vector2(at.X + (glyph * 0.5f), at.Y + (glyph * 0.5f)), glyph, Mark.Check);
            }

            ImGui.Dummy(new Vector2(glyph, glyph));
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(line.Done ? Strings.PrepDoneTooltip : Strings.PrepLeftTooltip);
            }

            ImGui.SameLine();
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + ((glyph - lineHeight) * 0.5f));
            var room = MathF.Max(1f, left + wrap - ImGui.GetCursorScreenPos().X);
            bool clicked;
            bool cut;
            using (Theme.PushText(line.Done ? s.TextSecondary : s.Text))
            {
                clicked = Chrome.EllipsisSelectable(line.Title, false, room, out cut);
            }

            if (ImGui.IsItemHovered())
            {
                var action = Action(line, onDashboard);
                var detail = action.Length == 0 ? line.Tooltip : line.Tooltip + "\n\n" + action;
                UiMetrics.Tooltip(cut ? line.Title : detail, cut ? detail : null);
            }

            if (clicked)
            {
                Act(ui, line, onDashboard);
            }

            if (line.Hint.Length > 0)
            {
                ImGui.SetCursorScreenPos(new Vector2(left + glyph + gap, ImGui.GetCursorScreenPos().Y));
                TextFlow.Wrapped(line.Hint, MathF.Max(1f, wrap - glyph - gap), Theme.U32(s.TextSecondary));
            }

            ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
        }
    }

    /// <summary>What a click does, for the tooltip; empty when it does nothing.</summary>
    private static string Action(LineView line, bool onDashboard) =>
        line.Kind == PrepKind.JournalRoom && !line.Done && !onDashboard ? Strings.PrepActionMakeRoom
        : line.Target is not null ? Strings.PrepActionSelect
        : string.Empty;

    private static void Act(UiState ui, LineView line, bool onDashboard)
    {
        if (line.Kind == PrepKind.JournalRoom && !line.Done && !onDashboard)
        {
            // Make room lives on the Characters dashboard, and opens by itself while the journal is nearly full.
            ui.Tab = NavTab.Characters;
            return;
        }

        if (line.Target is { } quest)
        {
            ui.Reveal(quest);
        }
    }

    private void SetDismissed(bool hide)
    {
        if (session.ViewedContentId is not { } contentId)
        {
            return;
        }

        characters.Edit(CharacterSettingChange.Dismiss(contentId, BeforeEvercold.CardId, hide));
        var name = session.ViewedSnapshot?.Name ?? string.Empty;
        UndoToast.Show(
            string.Format(CultureInfo.CurrentCulture, hide ? Strings.PrepHiddenToastFormat : Strings.PrepShownToastFormat, name),
            () => characters.Edit(CharacterSettingChange.Dismiss(contentId, BeforeEvercold.CardId, !hide)));
    }

    private void Refresh()
    {
        var now = DateTime.UtcNow;
        var next = (session.Version, characters.Version, now.Ticks / TimeSpan.TicksPerMinute, Localization.Loc.Version);
        if (next == key)
        {
            return;
        }

        key = next;
        active = false;
        lines = [];
        if (session.Bundle is not { } bundle || session.ViewedSnapshot is not { } snapshot || session.States.Count == 0
            || BeforeEvercold.IsRetired(bundle.Catalog, now))
        {
            return;
        }

        if (!ReferenceEquals(ladderBundle, bundle))
        {
            ladderBundle = bundle;
            ladder = bundle.BuildJobLadder();
        }

        active = true;
        dismissed = session.ViewedContentId is { } contentId && characters.IsCardDismissed(contentId, BeforeEvercold.CardId);
        expected = string.Format(CultureInfo.CurrentCulture, Strings.PrepExpectedFormat, SeasonalNow.DateText(BeforeEvercold.ExpectedUtc, now));
        var running = SeasonalNow.Running(bundle.Catalog, session.ServerFestivals, session.States, session.Curated.Festivals, now, session.EnteredFestivalEnds);
        var prep = BeforeEvercold.Lines(new PrepInputs(bundle.Catalog, snapshot, session.States, ladder, running));
        var views = new LineView[prep.Count];
        var left = 0;
        for (var i = 0; i < prep.Count; i++)
        {
            views[i] = View(prep[i], bundle, now);
            left += prep[i].Done ? 0 : 1;
        }

        lines = views;
        header = (left == 0 ? Strings.PrepHeaderDone : string.Format(CultureInfo.CurrentCulture, Strings.PrepHeaderLeftFormat, left)) + "###beforeEvercoldHeader";
    }

    private LineView View(PrepLine line, CatalogBundle bundle, DateTime now)
    {
        switch (line.Kind)
        {
            case PrepKind.MainScenario:
                {
                    var patch = line.StoryEnd?.AddedIn ?? string.Empty;
                    var title = patch.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.PrepMsqTitleFormat, patch) : Strings.PrepMsqTitle;
                    var hint = line.Done ? Strings.PrepMsqDone
                        : line.Target is not { } next ? string.Empty
                        : line.Left == 1 ? string.Format(CultureInfo.CurrentCulture, Strings.PrepMsqLeftOneFormat, session.Spoilers.DisplayName(next))
                        : string.Format(CultureInfo.CurrentCulture, Strings.PrepMsqLeftFormat, line.Left, session.Spoilers.DisplayName(next));
                    return new LineView(line.Kind, line.Done, title, hint, Strings.PrepMsqTooltip, line.Target);
                }

            case PrepKind.JobQuests:
                {
                    var title = string.Format(CultureInfo.CurrentCulture, Strings.PrepJobsTitleFormat, line.LevelCap);
                    string hint;
                    if (line.Done)
                    {
                        hint = string.Format(CultureInfo.CurrentCulture, Strings.PrepJobsDoneFormat, line.LevelCap);
                    }
                    else
                    {
                        var parts = new List<string>(line.Jobs.Count);
                        foreach (var job in line.Jobs)
                        {
                            var name = job.Role is { } role
                                ? string.Format(CultureInfo.CurrentCulture, Strings.JobsRoleRowFormat, Strings.JobsRoleName(role))
                                : bundle.Names.ClassJobInfo(job.JobId) is { Name.Length: > 0 } info ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(info.Name) : string.Empty;
                            if (name.Length > 0)
                            {
                                parts.Add(string.Format(CultureInfo.CurrentCulture, Strings.PrepJobLeftFormat, name, job.Left));
                            }
                        }

                        hint = string.Join(Strings.StateReasonSeparator, parts);
                    }

                    return new LineView(line.Kind, line.Done, title, hint, Strings.PrepJobsTooltip, line.Target);
                }

            case PrepKind.JournalRoom:
                {
                    var hint = line.Done ? line.Slots.Text : string.Format(CultureInfo.CurrentCulture, Strings.PrepJournalLeftFormat, line.Slots.Text);
                    return new LineView(line.Kind, line.Done, Strings.PrepJournalTitle, hint, Strings.PrepJournalTooltip, line.Target);
                }

            default:
                {
                    if (line.Festival is not { } festival)
                    {
                        return new LineView(line.Kind, line.Done, string.Empty, string.Empty, string.Empty, null);
                    }

                    var end = festival.AnnouncedEndUtc ?? BeforeEvercold.ExpectedUtc;
                    var title = string.Format(CultureInfo.CurrentCulture, Strings.PrepEventTitleFormat, festival.Name, SeasonalNow.DateText(end, now));
                    var hint = line.Done ? Strings.PrepEventDone
                        : line.Left == 1 ? Strings.PrepEventLeftOne
                        : string.Format(CultureInfo.CurrentCulture, Strings.PrepEventLeftFormat, line.Left);
                    var tooltip = string.Format(CultureInfo.CurrentCulture, Strings.PrepEventTooltipFormat, festival.Name, SeasonalNow.Status(festival, now));
                    return new LineView(line.Kind, line.Done, title, hint, tooltip, line.Target);
                }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Storage;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The Tonight card's "New chapters" line (feature plan v7 P5; spec-1.21 P5; in Tonight's lines since the What's new
/// card left in 1.22, spec-1.22 W4): the named side stories the viewed character has started that gained quests in the
/// newest patch series ("New chapters · Inconceivably Further Hildibrand Adventures · 2 quests · Show ›"), once per
/// patch and per character (recorded with the character's noticed ids, so every client agrees), and no new notice. It
/// stays for the session until × closes it. A line past the story point reads "A side story ahead · 2 quests · name
/// hidden". Show selects the first new quest. Framework thread only.
/// </summary>
public sealed class NewChaptersSource
{
    private readonly SessionState session;
    private readonly UiState ui;

    // Characters this load showed the line to (it stays for the session), the lines composed for each, and the
    // characters whose line × closed.
    private readonly Dictionary<ulong, Line[]> shown = [];
    private readonly HashSet<ulong> closed = [];

    // The answer for the view key, kept whatever it is (none included), so a character with no new chapters costs
    // nothing per frame.
    private (int Version, int Shield, int Language, ulong Character) builtKey;
    private object? builtBundle;
    private object? builtBook;
    private Line[]? built;
    private bool hasBuilt;

    public NewChaptersSource(SessionState session, UiState ui)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <summary>The character's noticed id for a patch series' new chapters ("chapters:7.5").</summary>
    public static string NoticeId(string series) => "chapters:" + series;

    /// <summary>The settings book the once-per-patch record lives in; set by the plugin. Null shows nothing.</summary>
    public CharacterSettingsBook? CharacterSettings { get; set; }

    /// <param name="Ahead">The line lies past the story point: <see cref="Name"/> is the shield's words for it.</param>
    private sealed record Line(string Name, string Count, QuestRecord First, bool Ahead = false);

    /// <summary>Whether the line has something to say for the viewed character.</summary>
    public bool Visible => Lines() is { Length: > 0 };

    /// <summary>Draws the line inside the Tonight card, when there is something to say for the viewed character.</summary>
    public void Draw()
    {
        if (Lines() is not { Length: > 0 } lines)
        {
            return;
        }

        using var id = ImRaii.PushId("newChapters");
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.NewChaptersLabel);
        }

        // × closes the line for this character; it was recorded as seen when it first showed, so it stays closed.
        Chrome.SameLineRightOrWrap(UiMetrics.MinTarget);
        if (Chrome.IconButtonRound("##close", Chrome.Icon(Dalamud.Interface.FontAwesomeIcon.Times), Strings.NewChaptersCloseTooltip) && session.ViewedContentId is { } viewed)
        {
            closed.Add(viewed);
            hasBuilt = false;
            return;
        }

        for (var i = 0; i < lines.Length; i++)
        {
            using var row = ImRaii.PushId(i);
            var line = lines[i];
            var cut = Chrome.FitText(line.Name, line.Ahead ? Theme.Surface.TextSecondary : ShieldText.Tone(line.Name, Theme.Surface.Text), tooltip: !line.Ahead);
            if (line.Ahead)
            {
                // "A side story ahead": the placeholder's hover and right-click (spec-1.20 N6), which reveal the quest.
                ShieldText.InteractQuestItem(session, line.First, line.Name, lead: cut ? line.Name : null);
            }

            ImGui.SameLine();
            ImGui.TextColored(Theme.Surface.TextSecondary, line.Count);
            Chrome.SameLineRightOrWrap(ImGui.CalcTextSize(Strings.NewChaptersShow).X + (ImGui.GetStyle().FramePadding.X * 2f));
            if (ImGui.SmallButton(Strings.NewChaptersShow))
            {
                ui.Reveal(line.First);
            }

            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.NewChaptersShowTooltip);
            }
        }

        ImGui.Spacing();
    }

    /// <summary>The lines for the viewed character, worked out once per view key (an empty answer too).</summary>
    private Line[]? Lines()
    {
        if (session.ViewedContentId is not { } character || session.Bundle is not { } bundle || session.States.Count == 0 || CharacterSettings is not { } book || closed.Contains(character))
        {
            return null;
        }

        var key = (session.Version, session.Spoilers.Fingerprint, Localization.Loc.Version, character);
        if (hasBuilt && key == builtKey && ReferenceEquals(bundle, builtBundle) && ReferenceEquals(book, builtBook))
        {
            return built;
        }

        builtKey = key;
        builtBundle = bundle;
        builtBook = book;
        hasBuilt = true;
        built = Build(character, bundle, book);
        return built;
    }

    private Line[]? Build(ulong character, CatalogBundle bundle, CharacterSettingsBook book)
    {
        var series = PatchIndex.For(bundle.Catalog).NewestSeries;
        if (series.Length == 0)
        {
            return null;
        }

        var noticeId = NoticeId(series);
        var first = !shown.ContainsKey(character);
        if (first && book.Noticed(character).Contains(noticeId))
        {
            return null;
        }

        var chapters = SideStories.NewChapters(session.Chains, bundle.Catalog, session.States);
        var lines = new List<Line>(chapters.Count);
        var spoilers = session.Spoilers;
        foreach (var chapter in chapters)
        {
            if (bundle.Catalog.GetByRowId(chapter.NewQuests[0]) is not { } quest)
            {
                continue;
            }

            var count = chapter.NewQuests.Count == 1 ? Strings.NewChaptersQuestOne : string.Format(CultureInfo.CurrentCulture, Strings.NewChaptersQuestsFormat, chapter.NewQuests.Count);
            if (spoilers.IsAhead(quest.RowId))
            {
                lines.Add(new Line(Strings.StoriesAhead, count + Strings.StateReasonSeparator + Strings.NewChaptersNameHidden, quest, Ahead: true));
                continue;
            }

            // A line made of several journal genres (Hildibrand) is named by the chapter's genre, as the journal files it.
            var several = chapter.Chain.RowIds.Count > 0 && bundle.Catalog.GetByRowId(chapter.Chain.RowIds[0]) is { } head && head.Journal.GenreId != quest.Journal.GenreId;
            lines.Add(new Line(several && quest.Journal.GenreName.Length > 0 ? ShieldRules.Genre(quest, spoilers) : chapter.Chain.Name, count, quest));
        }

        if (lines.Count == 0)
        {
            return null;
        }

        if (first)
        {
            book.Edit(CharacterSettingChange.Noticed(character, noticeId));
        }

        var result = lines.ToArray();
        shown[character] = result;
        return result;
    }
}

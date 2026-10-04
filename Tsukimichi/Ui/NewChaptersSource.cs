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

namespace Tsukimichi.Ui;

/// <summary>
/// The What's new card's "New chapters" line (feature plan v7 P5; spec-1.21 P5): the named side stories the viewed
/// character has started that gained quests in the newest patch series ("New chapters · Inconceivably Further Hildibrand
/// Adventures · 2 quests · Show ›"), once per patch and per character (recorded with the character's noticed ids, so
/// every client agrees), and no new notice. A line past the story point reads "A side story ahead · 2 quests · name
/// hidden". Show selects the first new quest. Framework thread only.
/// </summary>
public sealed class NewChaptersSource
{
    private readonly SessionState session;
    private readonly UiState ui;

    // Characters this load showed the line to (it stays while the card does), and the lines composed for each.
    private readonly Dictionary<ulong, Line[]> shown = [];
    private (int Version, int Shield, int Language, ulong Character) builtKey = (-1, 0, -1, 0);

    public NewChaptersSource(SessionState session, UiState ui)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    /// <summary>The character's noticed id for a patch series' new chapters ("chapters:7.5").</summary>
    public static string NoticeId(string series) => "chapters:" + series;

    /// <summary>The settings book the once-per-patch record lives in; set by the plugin. Null shows nothing.</summary>
    public CharacterSettingsBook? CharacterSettings { get; set; }

    private sealed record Line(string Name, string Count, QuestRecord First);

    /// <summary>Draws the line inside the What's new card, when there is something to say for the viewed character.</summary>
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

        for (var i = 0; i < lines.Length; i++)
        {
            using var row = ImRaii.PushId(i);
            var line = lines[i];
            Chrome.FitText(line.Name, Theme.Surface.Text);
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

    private Line[]? Lines()
    {
        if (session.ViewedContentId is not { } character || session.Bundle is not { } bundle || session.States.Count == 0 || CharacterSettings is not { } book)
        {
            return null;
        }

        var key = (session.Version, session.Spoilers.Fingerprint, Localization.Loc.Version, character);
        if (shown.TryGetValue(character, out var known) && key == builtKey)
        {
            return known;
        }

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

        builtKey = key;
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
                lines.Add(new Line(Strings.StoriesAhead, count + Strings.StateReasonSeparator + Strings.NewChaptersNameHidden, quest));
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

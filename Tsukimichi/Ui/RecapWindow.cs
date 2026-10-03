using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Tsukimichi.Config;
using Tsukimichi.Core.Chains;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The story recap, "Previously…" (feature plan v5, collector extras; R9 F5): the journal text of the last few main
/// scenario quests the viewed character completed (Settings › Display › Free trial and story recap sets how many), or
/// of every completed quest of one chain, as one page to read in story order, with Copy all. A request may name its
/// character (<see cref="RecapRequest.ContentId"/>: the Since you were away card's, which can be the logged-in one
/// while a stored character is viewed); the page then reads that character's capture and shield. Spoiler-safe by
/// construction: only completed quests are picked (<see cref="StoryRecap"/>), from the completion bits, so a stored
/// character's recap works too. The text comes from the journal text reader (<see cref="QuestTextService.ReadCompleted"/>),
/// a few quests per frame so a long chain never stalls a frame; the logged-in character's text is evaluated by the
/// game (its name, its gender), a stored one's neutrally. Opened by <see cref="UiState.OpenRecap"/> from the Since you
/// were away card, a chain quest's detail pane, the Characters tab's story chain rows (their right-click menu; an
/// achievement's quests are no one story, so its rows offer none) and <c>/tsuki recap</c>.
/// </summary>
public sealed class RecapWindow : Window
{
    /// <summary>Quests whose text is read per frame while the page fills.</summary>
    private const int ReadsPerFrame = 2;

    private const float MinWidthLogical = 360f;
    private const float MinHeightLogical = 260f;
    private const double CopiedSeconds = 2.0;

    private readonly SessionState session;
    private readonly Configuration settings;
    private readonly Func<QuestTextService?> questText;
    private Theme.StyleScope nightChrome;

    private RecapRequest? request;

    // What the page was built for; a new request, another character, a new catalog, another language or the
    // character turning live or stored starts it again.
    private RecapRequest? builtRequest;
    private ulong? builtFor;
    private CatalogBundle? builtBundle;
    private int builtLength;
    private int builtLanguage = -1;
    private bool builtLive;

    private string heading = string.Empty;
    private string intro = string.Empty;
    private readonly List<QuestRecord> pending = [];
    private int nextToRead;
    private readonly List<RecapChapter> chapters = [];
    private bool live;
    private double copiedUntil;

    /// <param name="questText">The journal text reader; null (not created yet) shows why the page is empty.</param>
    public RecapWindow(SessionState session, Configuration settings, Func<QuestTextService?> questText)
        : base(Strings.RecapWindowTitle)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        this.questText = questText ?? throw new ArgumentNullException(nameof(questText));
        Size = new Vector2(560f, 620f);
        SizeCondition = ImGuiCond.FirstUseEver;
        SizeConstraints = new WindowSizeConstraints { MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) };
    }

    /// <summary>Opens the window on <paramref name="recap"/> and brings it to the front.</summary>
    public void Show(RecapRequest recap)
    {
        request = recap ?? throw new ArgumentNullException(nameof(recap));
        builtRequest = null;
        IsOpen = true;
        BringToFront();
    }

    public override void PreDraw()
    {
        // Follows a language switch; the part after "###" keeps the window's place.
        WindowName = Strings.RecapWindowTitle;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(MinWidthLogical, MinHeightLogical) * UiMetrics.UiScale,
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        nightChrome = Theme.PushNightWindow();
    }

    public override void PostDraw()
    {
        nightChrome.Dispose();
        nightChrome = default;
    }

    public override void OnClose()
    {
        // The text is the character's journal: nothing is kept once the window closes.
        pending.Clear();
        chapters.Clear();
        builtRequest = null;
    }

    public override void Draw()
    {
        UiMetrics.ApplyFontScale();
        try
        {
            DrawContent();
        }
        finally
        {
            ImGui.SetWindowFontScale(1f);
        }
    }

    private void DrawContent()
    {
        if (request is null)
        {
            return;
        }

        if (session.Bundle is not { } bundle)
        {
            ImGui.TextDisabled(Strings.RouteLoading);
            return;
        }

        if (Subject(request) is not { } subject)
        {
            EmptyState.Draw(Strings.RecapNoCharacter);
            return;
        }

        if (questText() is not { } reader)
        {
            EmptyState.Draw(Strings.RecapNoReader);
            return;
        }

        Build(bundle, subject, request);
        ReadSome(reader, subject);

        using (Typography.Title(heading))
        {
            ImGui.TextUnformatted(heading);
        }

        if (pending.Count == 0)
        {
            // No quest to read: the empty state says why, with no "the last 0 quests" line above it.
            ImGui.Spacing();
            EmptyState.DrawWithAction(Strings.RecapEmptyHeading, request.IsMainScenario ? Strings.RecapEmptyMsq : Strings.RecapEmptyChain, null);
            return;
        }

        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            ImGui.TextWrapped(intro);
        }

        DrawToolbar();
        ImGui.Separator();
        using var page = ImRaii.Child("##recapPage", Vector2.Zero);
        if (!page)
        {
            return;
        }

        DrawChapters();
    }

    private void DrawToolbar()
    {
        var loading = nextToRead < pending.Count;
        using (ImRaii.Disabled(loading))
        {
            if (ImGui.Button(Strings.RecapCopy))
            {
                ImGui.SetClipboardText(StoryRecap.Compose(heading, chapters));
                copiedUntil = ImGui.GetTime() + CopiedSeconds;
            }
        }

        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(Strings.RecapCopyTooltip);
        }

        ImGui.SameLine();
        using var mist = Theme.PushText(Theme.Surface.TextSecondary);
        if (loading)
        {
            ImGui.TextUnformatted(string.Format(CultureInfo.CurrentCulture, Strings.RecapReadingFormat, nextToRead, pending.Count));
        }
        else if (ImGui.GetTime() < copiedUntil)
        {
            ImGui.TextUnformatted(Strings.JournalTextCopied);
        }
        else if (!live)
        {
            ImGui.TextUnformatted(Strings.RecapNeutral);
            if (ImGui.IsItemHovered())
            {
                UiMetrics.Tooltip(Strings.JournalTextNeutral);
            }
        }
    }

    private void DrawChapters()
    {
        for (var i = 0; i < chapters.Count; i++)
        {
            var chapter = chapters[i];
            using var id = ImRaii.PushId(i);
            if (i > 0)
            {
                ImGui.Spacing();
                Chrome.Hairline();
            }

            using (Typography.Title(chapter.Title))
            using (ImRaii.PushColor(ImGuiCol.Text, Theme.Moon))
            {
                ImGui.TextWrapped(chapter.Title);
            }

            if (chapter.Entries.Count == 0)
            {
                ImGui.TextDisabled(Strings.JournalTextNoText);
                continue;
            }

            foreach (var entry in chapter.Entries)
            {
                ImGui.Spacing();
                ImGui.TextWrapped(entry);
            }
        }
    }

    /// <summary>
    /// The character the request reads (<see cref="RecapRequest.ContentId"/>, else the viewed one), whether it is the
    /// logged-in one, and its spoiler shield; null when that character is neither viewed nor logged in any more.
    /// </summary>
    private (CharacterSnapshot Snapshot, bool Live, SpoilerMask Shield)? Subject(RecapRequest recap)
    {
        if (recap.ContentId is not { } id || id == session.ViewedContentId)
        {
            return session.ViewedSnapshot is { } viewed ? (viewed, session.IsLive, session.Spoilers) : null;
        }

        return id == session.LiveContentId && session.LiveSnapshot is { } live ? (live, true, session.LiveSpoilers) : null;
    }

    /// <summary>
    /// Picks the quests when the request, the character, the catalog, the length, the language or whether the
    /// character is the logged-in one changed; the last two re-read the chapters (their names and the journal text
    /// follow the language, and the text is evaluated by the game only for the logged-in character).
    /// </summary>
    private void Build(CatalogBundle bundle, (CharacterSnapshot Snapshot, bool Live, SpoilerMask Shield) subject, RecapRequest recap)
    {
        var (snapshot, isLive, shield) = subject;
        var length = settings.RecapLengthClamped;
        var language = Localization.Loc.Version;
        if (ReferenceEquals(builtRequest, recap) && builtFor == snapshot.ContentId && ReferenceEquals(builtBundle, bundle)
            && (!recap.IsMainScenario || builtLength == length) && builtLanguage == language && builtLive == isLive)
        {
            return;
        }

        builtRequest = recap;
        builtFor = snapshot.ContentId;
        builtBundle = bundle;
        builtLength = length;
        builtLanguage = language;
        builtLive = isLive;
        pending.Clear();
        chapters.Clear();
        nextToRead = 0;

        // The completion bits, not the resolved states: a stored character's recap needs nothing else.
        bool Done(uint rowId) => snapshot.IsCompleted(QuestRecord.ToQuestId(rowId));
        if (recap.IsMainScenario)
        {
            pending.AddRange(StoryRecap.MainScenario(bundle.Catalog, Done, length));
            heading = Strings.RecapMsqHeading;
            intro = string.Format(CultureInfo.CurrentCulture, pending.Count == 1 ? Strings.RecapMsqIntroOne : Strings.RecapMsqIntroFormat, pending.Count);
            return;
        }

        if (session.Chains.ForQuest(recap.ChainQuestRowId) is not { } chain)
        {
            heading = Strings.RecapMsqHeading;
            intro = string.Empty;
            return;
        }

        pending.AddRange(StoryRecap.Chain(chain, bundle.Catalog, Done));
        var name = ChainCatalog.DisplayName(chain, id => shield.DisplayName(bundle.Catalog, id, id.ToString(CultureInfo.InvariantCulture)));
        heading = string.Format(CultureInfo.CurrentCulture, Strings.RecapChainHeadingFormat, name);
        intro = string.Format(CultureInfo.CurrentCulture, Strings.RecapChainIntroFormat, pending.Count);
    }

    /// <summary>Reads the next few quests' journals into chapters.</summary>
    private void ReadSome(QuestTextService reader, (CharacterSnapshot Snapshot, bool Live, SpoilerMask Shield) subject)
    {
        live = subject.Live;
        for (var n = 0; n < ReadsPerFrame && nextToRead < pending.Count; n++, nextToRead++)
        {
            var quest = pending[nextToRead];
            var view = reader.ReadCompleted(quest, live, live ? null : subject.Snapshot.Name);
            chapters.Add(new RecapChapter(quest.RowId, subject.Shield.DisplayName(quest), view.Entries));
        }
    }
}

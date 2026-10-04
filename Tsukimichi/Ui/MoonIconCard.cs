using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Tsukimichi.Commands;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Todo;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The moon icon's quick card (feature plan v8 H1; spec-1.22 H1 "The quick card", moon-icon-1.22.png): a tooltip
/// 300 px wide that never takes focus or the pointer, beside the icon on the side with room and never over it. It speaks
/// for the character on screen (the logged-in one; the viewed one while nobody is):
/// <list type="bullet">
/// <item><b>Tonight</b>, with the character and job on the right;</item>
/// <item><b>Up next</b>: Up next's own pick (<see cref="GuidanceCommand.UpNext"/>, the 1.21 P1 rules), its moon and
/// name, then the step and the place on their own lines;</item>
/// <item>"12 quests are Ready on WHM", "Journal 27/30 · 3 slots left" and the events ending soon
/// (<see cref="EventWarningSource"/>);</item>
/// <item>when present, the Needs you line (it wins, as on the dot) and "Tsukimichi 1.23.0 is ready · Update", each
/// with its dot, so no colour carries meaning alone;</item>
/// <item>the hint: "Click to open · right-click for options", or "Locked in place · right-click to unlock".</item>
/// </list>
/// At Full it is the kit's card, at Quiet a tonal card, at Plain a flat sheet with a line. Its words are built when it
/// opens and again at most twice a second while it shows, never per frame; it settles its height unseen on its first
/// frame so it never jumps, and takes a later change of height in the same frame, staying visible.
/// </summary>
internal sealed class MoonIconCard
{
    private const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoInputs | ImGuiWindowFlags.NoBackground
        | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

    private const float PadLogical = 14f;
    private const float GapLogical = 6f;
    private const double RefreshSeconds = 0.5;

    private static readonly string BookIcon = Chrome.Icon(FontAwesomeIcon.Book);
    private static readonly string ClockIcon = Chrome.Icon(FontAwesomeIcon.Clock);

    private readonly SessionState session;
    private readonly GameLinks links;
    private readonly List<string> endings = [];

    private double builtAt = double.NegativeInfinity;
    private int builtVersion = -1;
    private int builtLanguage = -1;
    private float height;

    // What the card says, built by Refresh.
    private string eyebrowUpper = string.Empty;
    private string who = string.Empty;
    private bool hasUpNext;
    private QuestState upNextState;
    private string upNextName = string.Empty;
    private string upNextStep = string.Empty;
    private string upNextPlace = string.Empty;
    private string upNextNothing = string.Empty;
    private string ready = string.Empty;
    private string readyJob = string.Empty;
    private string journal = string.Empty;
    private string? needsYou;
    private string? update;

    public MoonIconCard(SessionState session, GameLinks links)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.links = links ?? throw new ArgumentNullException(nameof(links));
    }

    /// <summary>Up next's pick for the character on screen; null leaves the Up next block saying nothing is picked.</summary>
    public GuidanceCommand? Guidance { get; set; }

    /// <summary>The events ending soon; null leaves them out.</summary>
    public EventWarningSource? Events { get; set; }

    /// <summary>The Needs you alerts (1.18 A5); null leaves the line out.</summary>
    public RunStops? Stops { get; set; }

    /// <summary>Whether Dalamud has a newer Tsukimichi (U1); null leaves the line out.</summary>
    public IUpdateState? Updates { get; set; }

    /// <summary>The card closed: the next time it opens it builds its words afresh and settles its height again.</summary>
    public void Close()
    {
        builtAt = double.NegativeInfinity;
        height = 0f;
    }

    /// <summary>
    /// Draws the card beside <paramref name="icon"/> (the icon's square on screen). On open it settles its height unseen
    /// for one frame; a height that changes while it is open (a line arriving or leaving) is taken in the same frame, the
    /// card moved to its place for that height, and it stays visible (<see cref="MoonIconRules.Settle"/>).
    /// </summary>
    public void Draw(in ScreenRect icon, bool locked, double now)
    {
        Refresh(now);
        var viewport = ImGuiHelpers.MainViewport;
        var screen = new ScreenRect(viewport.Pos, viewport.Pos + viewport.Size);
        var width = MathF.Min(UiMetrics.Px(MoonIconRules.CardWidthLogical), MathF.Max(1f, viewport.Size.X - UiMetrics.Px(16f)));
        var shown = MathF.Max(1f, height > 0f ? height : UiMetrics.Px(160f));
        var gap = UiMetrics.Px(MoonIconRules.CardGapLogical);
        var pos = Rounded(MoonIconRules.CardPlace(icon, new Vector2(width, shown), screen, gap));
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, shown), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        using var body = Typography.Body();
        var visible = ImGui.Begin("##tsukimichiMoonCard", Flags);
        try
        {
            if (visible)
            {
                ImGuiP.BringWindowToDisplayFront(ImGuiP.GetCurrentWindow());
                UiMetrics.ApplyFontScale();
                var dl = ImGui.GetWindowDrawList();
                var start = dl.VtxBuffer.Size;
                var settle = Content(dl, icon, screen, width, gap, locked);
                if (settle == CardSettle.Measure)
                {
                    Chrome.FadeVertices(dl, start, 0f);
                }
            }
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }

    private static Vector2 Rounded(Vector2 at) => new(MathF.Round(at.X), MathF.Round(at.Y));

    // The draw-list channels: the card's frame at the back, the Up next lift over it, the lines on top.
    private const int FrameChannel = 0;
    private const int LiftChannel = 1;
    private const int LinesChannel = 2;

    /// <summary>
    /// The card's lines first, measured; when their height changed while open, they move to the card's place for the new
    /// height; then its frame at that place and height, beneath them. Returns what the height did.
    /// </summary>
    private CardSettle Content(ImDrawListPtr dl, in ScreenRect icon, in ScreenRect screen, float width, float gap, bool locked)
    {
        var start = dl.VtxBuffer.Size;
        var min = ImGui.GetWindowPos();
        dl.ChannelsSplit(3);
        try
        {
            // Against the screen's clip, not the window's, so lines past the height the window was given still draw.
            dl.ChannelsSetCurrent(LinesChannel);
            ImGui.PushClipRect(screen.Min, screen.Max, false);
            float measured;
            try
            {
                measured = Lines(dl, min, width, locked);
            }
            finally
            {
                ImGui.PopClipRect();
            }

            var settle = MoonIconRules.Settle(height, measured);
            height = measured;
            if (settle == CardSettle.Resize)
            {
                var place = Rounded(MoonIconRules.CardPlace(icon, new Vector2(width, measured), screen, gap));
                Chrome.ShiftVertices(dl, start, place - min);
                min = place;
            }

            // The frame is clipped to the card, as the window clipped it before.
            var max = min + new Vector2(width, MathF.Max(1f, measured));
            dl.ChannelsSetCurrent(FrameChannel);
            dl.PushClipRect(min, max, false);
            Frame(dl, min, max);
            dl.PopClipRect();
            return settle;
        }
        finally
        {
            dl.ChannelsMerge();
        }
    }

    /// <summary>The palette's own sheet, opaque (the card sits over the game, light palettes included), in the level's frame.</summary>
    private static void Frame(ImDrawListPtr dl, Vector2 min, Vector2 max)
    {
        var flair = Theme.Flair;
        var s = Theme.Surface;
        var rounding = flair == Flair.Plain ? 0f : UiMetrics.Px(Theme.Spacing.CardRounding);
        dl.AddRectFilled(min, max, Theme.U32(s.Window with { W = 1f }), rounding);
        switch (FlairRules.Card(flair))
        {
            case CardFrame.BrassCorners:
                Chrome.CardSurface(dl, min, max);
                break;
            case CardFrame.Tonal:
                dl.AddRectFilled(min, max, Theme.U32(Theme.Tones.Card with { W = 1f }), rounding);
                Chrome.CardSurface(dl, min, max);
                break;
            default:
                dl.AddRect(min, max, Theme.U32(s.StrongLine), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                break;
        }
    }

    /// <summary>The card's lines from <paramref name="min"/>; returns the card's height.</summary>
    private float Lines(ImDrawListPtr dl, Vector2 min, float width, bool locked)
    {
        var flair = Theme.Flair;
        var s = Theme.Surface;
        var max = new Vector2(min.X + width, min.Y);
        using var text = Theme.PushText(s.Text);
        var pad = UiMetrics.Px(PadLogical);
        var gap = UiMetrics.Px(GapLogical);
        var left = min.X + pad;
        var right = max.X - pad;
        var y = min.Y + pad;

        // Tonight, and who it is about.
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        if (flair == Flair.Full)
        {
            using var role = Typography.Eyebrow(eyebrowUpper);
            using (Theme.PushText(Theme.OrnamentLight))
            {
                ImGui.TextUnformatted(role.GameFace ? eyebrowUpper : Strings.TonightTitle);
            }
        }
        else
        {
            Chrome.SemiboldText(Strings.TonightTitle, s.TextSecondary);
        }

        var headerTop = ImGui.GetItemRectMin().Y;
        var headerBottom = ImGui.GetItemRectMax().Y;
        if (who.Length > 0)
        {
            using (Typography.Caption())
            {
                var whoWidth = ImGui.CalcTextSize(who).X;
                var whoY = MathF.Round(headerTop + MathF.Max(0f, (headerBottom - headerTop - ImGui.GetTextLineHeight()) * 0.5f));
                dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(MathF.Max(left, right - whoWidth), whoY), Theme.U32(s.TextSecondary), who);
            }
        }

        y = headerBottom + gap;
        y = UpNext(dl, left, right, y, flair) + gap;

        // Ready, the journal and the events, each behind its own small mark.
        var mark = MathF.Round(UiMetrics.Px(16f));
        var textX = left + mark + UiMetrics.Px(6f);
        ImGui.SetCursorScreenPos(new Vector2(textX, y));
        ImGui.TextUnformatted(ready);
        var lineMin = ImGui.GetItemRectMin();
        var lineMax = ImGui.GetItemRectMax();
        MoonGlyph.Draw(dl, new Vector2(left + (mark * 0.5f), (lineMin.Y + lineMax.Y) * 0.5f), mark * 0.42f, QuestState.Ready);
        if (readyJob.Length > 0)
        {
            ImGui.SameLine(0f, UiMetrics.Px(4f));
            using (Theme.PushText(s.TextSecondary))
            {
                ImGui.TextUnformatted(readyJob);
            }
        }

        y = lineMax.Y + UiMetrics.Px(3f);
        if (journal.Length > 0)
        {
            y = MarkedLine(left, textX, y, mark, BookIcon, journal, s.Text);
        }

        foreach (var ending in endings)
        {
            y = MarkedLine(left, textX, y, mark, ClockIcon, ending, s.Text);
        }

        // Needs you wins, as on the dot; then the update. Each has its dot and its words.
        if (needsYou is not null || update is not null)
        {
            y += gap * 0.5f;
            dl.AddLine(new Vector2(left, MathF.Round(y)), new Vector2(right, MathF.Round(y)), Theme.U32(s.Line), UiMetrics.Hairline);
            y += gap;
            if (needsYou is not null)
            {
                y = DotLine(dl, left, right, textX, y, Theme.Copper, needsYou, Strings.MoonIconCardShow);
            }

            if (update is not null)
            {
                y = DotLine(dl, left, right, textX, y, Theme.Surface.Cool, update, Strings.MoonIconCardUpdate);
            }
        }

        // The hint.
        y += gap * 0.5f;
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        using (Typography.Caption())
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextUnformatted(locked ? Strings.MoonIconLockedHint : Strings.MoonIconCardHint);
        }

        return MathF.Ceiling(ImGui.GetItemRectMax().Y + pad - min.Y);
    }

    /// <summary>The Up next block on its tonal lift (a line under it at Plain): the moon, then the name, step and place.</summary>
    private float UpNext(ImDrawListPtr dl, float left, float right, float top, Flair flair)
    {
        var s = Theme.Surface;
        var inset = flair == Flair.Plain ? 0f : UiMetrics.Px(10f);
        var medal = MathF.Round(UiMetrics.Px(flair == Flair.Plain ? 16f : 22f));
        var textX = left + inset + medal + UiMetrics.Px(8f);
        var wrap = MathF.Max(textX + 1f, right - inset);
        dl.ChannelsSetCurrent(LinesChannel);
        ImGui.SetCursorScreenPos(new Vector2(textX, top + inset));
        ImGui.PushTextWrapPos(wrap - ImGui.GetWindowPos().X);
        ImGui.BeginGroup();
        using (Typography.Caption())
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextUnformatted(Strings.UpNextEyebrow);
        }

        if (hasUpNext)
        {
            Chrome.SemiboldTextWrapped(upNextName, s.Text);
            using (Typography.Caption())
            using (Theme.PushText(s.TextSecondary))
            {
                if (upNextStep.Length > 0)
                {
                    ImGui.TextWrapped(upNextStep);
                }

                if (upNextPlace.Length > 0)
                {
                    ImGui.TextWrapped(upNextPlace);
                }
            }
        }
        else
        {
            using (Theme.PushText(s.TextSecondary))
            {
                ImGui.TextWrapped(upNextNothing);
            }
        }

        ImGui.EndGroup();
        ImGui.PopTextWrapPos();
        var bottom = ImGui.GetItemRectMax().Y + inset;
        var first = ImGui.GetItemRectMin().Y;
        if (hasUpNext)
        {
            MoonGlyph.Draw(dl, new Vector2(left + inset + (medal * 0.5f), first + (medal * 0.5f)), medal * 0.5f, upNextState);
        }

        dl.ChannelsSetCurrent(LiftChannel);
        var min = new Vector2(left, top);
        var max = new Vector2(right, bottom);
        if (flair == Flair.Plain)
        {
            dl.AddLine(new Vector2(min.X, max.Y - 0.5f), new Vector2(max.X, max.Y - 0.5f), Theme.U32(s.Line), UiMetrics.Hairline);
        }
        else
        {
            var lift = Theme.IsLight ? s.Raised : Theme.WithAlphaVector(s.Text, flair == Flair.Full ? 0.07f : 0.05f);
            dl.AddRectFilled(min, max, Theme.U32(lift), UiMetrics.Px(6f));
            dl.AddRect(min, max, Theme.U32(Theme.IsLight ? s.Line : Theme.WithAlphaVector(s.Text, 0.10f)), UiMetrics.Px(6f), ImDrawFlags.None, UiMetrics.Hairline);
        }

        dl.ChannelsSetCurrent(LinesChannel);
        return bottom;
    }

    /// <summary>One line of text behind an icon-font mark; returns the next line's top.</summary>
    private static float MarkedLine(float left, float textX, float y, float mark, string icon, string text, Vector4 ink)
    {
        ImGui.SetCursorScreenPos(new Vector2(left, y));
        using (Typography.Caption())
        using (Theme.PushText(Theme.Surface.TextSecondary))
        {
            Typography.Icon(icon);
        }

        ImGui.SetCursorScreenPos(new Vector2(textX, y));
        ImGui.PushTextWrapPos(ImGui.GetWindowSize().X - UiMetrics.Px(PadLogical));
        using (Theme.PushText(ink))
        {
            ImGui.TextWrapped(text);
        }

        ImGui.PopTextWrapPos();
        return ImGui.GetItemRectMax().Y + UiMetrics.Px(3f);
    }

    /// <summary>A coloured dot, the words, and the action word on the right in the cool accent; returns the next line's top.</summary>
    private static float DotLine(ImDrawListPtr dl, float left, float right, float textX, float y, Vector4 dot, string text, string action)
    {
        float actionWidth;
        using (Typography.Caption())
        {
            actionWidth = ImGui.CalcTextSize(action).X;
        }

        ImGui.SetCursorScreenPos(new Vector2(textX, y));
        ImGui.PushTextWrapPos(right - actionWidth - UiMetrics.Px(8f) - ImGui.GetWindowPos().X);
        ImGui.TextWrapped(text);
        ImGui.PopTextWrapPos();
        var lineMin = ImGui.GetItemRectMin();
        var firstLine = ImGui.GetTextLineHeight();
        dl.AddCircleFilled(new Vector2(left + UiMetrics.Px(8f), lineMin.Y + (firstLine * 0.5f)), UiMetrics.Px(3.5f), Theme.U32(dot), 12);
        using (Typography.Caption())
        {
            var actionY = lineMin.Y + MathF.Max(0f, (firstLine - ImGui.GetTextLineHeight()) * 0.5f);
            dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(right - actionWidth, actionY), Theme.CoolU32, action);
        }

        return ImGui.GetItemRectMax().Y + UiMetrics.Px(3f);
    }

    /// <summary>Builds the card's words when it opens, then at most twice a second, or at once after the session or language changed.</summary>
    private void Refresh(double now)
    {
        if (builtVersion == session.Version && builtLanguage == Localization.Loc.Version && now - builtAt < RefreshSeconds && now >= builtAt)
        {
            return;
        }

        builtAt = now;
        builtVersion = session.Version;
        builtLanguage = Localization.Loc.Version;
        eyebrowUpper = SectionHeading.Label(Strings.TonightTitle);
        who = string.Empty;
        hasUpNext = false;
        upNextName = upNextStep = upNextPlace = string.Empty;
        upNextNothing = Strings.CatalogNotReady;
        ready = Strings.MoonIconCardReadyNone;
        readyJob = string.Empty;
        journal = string.Empty;
        endings.Clear();
        needsYou = Stops?.NeedsYou.Current?.Title;
        update = Updates?.ReadyVersion is { Length: > 0 } version
            ? string.Format(CultureInfo.CurrentCulture, Strings.MoonIconCardUpdateFormat, version)
            : null;

        var live = session.LiveContentId is not null;
        var snapshot = live ? session.LiveSnapshot : session.ViewedSnapshot;
        if (session.Bundle is not { } bundle || snapshot is null)
        {
            return;
        }

        var states = live ? session.LiveStates : session.States;
        var spoilers = live ? session.LiveSpoilers : session.Spoilers;
        var job = bundle.Names.ClassJobAbbreviation(snapshot.CurrentJob);
        var first = snapshot.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries) is [var name, ..] ? name : snapshot.Name;
        who = snapshot.JobLevels.TryGetValue(snapshot.CurrentJob, out var level) && level > 0 && job.Length > 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.MoonIconCardWhoFormat, first, job, level)
            : first;

        var count = 0;
        foreach (var quest in bundle.Catalog.All)
        {
            if (!quest.IsRemoved && states.TryGetValue(quest.RowId, out var evaluation) && evaluation.State == QuestState.Ready)
            {
                count++;
            }
        }

        ready = count switch
        {
            0 => Strings.MoonIconCardReadyNone,
            1 => Strings.MoonIconCardReadyOne,
            _ => string.Format(CultureInfo.CurrentCulture, Strings.MoonIconCardReadyFormat, count),
        };
        readyJob = job.Length > 0 ? string.Format(CultureInfo.CurrentCulture, Strings.MoonIconCardOnJobFormat, job) : string.Empty;

        var slots = Core.Journal.JournalSlots.Of(snapshot, bundle.Catalog);
        if (slots.Cap > 0)
        {
            journal = slots.Left == 0
                ? string.Format(CultureInfo.CurrentCulture, Strings.JournalBarFullFormat, slots.Used, slots.Cap)
                : string.Format(CultureInfo.CurrentCulture, Strings.JournalBarFormat, slots.Used, slots.Cap) + Strings.UpNextSeparator
                    + (slots.Left == 1 ? Strings.JournalBarLeftOne : string.Format(CultureInfo.CurrentCulture, Strings.JournalBarLeftFormat, slots.Left));
        }

        if (Events is { } events)
        {
            foreach (var warning in events.Current)
            {
                endings.Add(EventWarningSource.Title(warning));
                if (endings.Count == 2)
                {
                    break;
                }
            }
        }

        upNextNothing = string.Format(CultureInfo.CurrentCulture, Strings.UpNextNothingFormat, job);
        if (Guidance?.UpNext() is not { } pick)
        {
            return;
        }

        var picked = pick.Quest;
        states.TryGetValue(picked.RowId, out var picks);
        hasUpNext = true;
        upNextState = picks?.State ?? QuestState.Unknown;
        upNextName = spoilers.DisplayName(picked);
        var contentId = session.LiveContentId ?? session.ViewedContentId ?? 0;
        if (links.CurrentStep(picked, picks, live, null, contentId) is { } step)
        {
            upNextStep = string.Format(CultureInfo.CurrentCulture, Strings.UpNextStepFormat, step.Step.Step, step.Objective.Length > 0 ? step.Objective : Strings.StepNoObjective);
            upNextPlace = step.Zone;
        }
        else if (pick.Rule == UpNextRule.LevelGate)
        {
            upNextStep = Strings.UpNextGateHint;
        }
        else
        {
            upNextStep = string.Format(CultureInfo.CurrentCulture, Strings.UpNextTalkFormat, GiverPortraits.Name(picked, spoilers));
            upNextPlace = GiverPortraits.Place(picked, spoilers) ?? string.Empty;
        }
    }
}

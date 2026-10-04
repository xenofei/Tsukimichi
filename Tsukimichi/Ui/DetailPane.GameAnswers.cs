using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Tsukimichi.Core.Diagnostics;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Journal;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The game's answers and the full journal in the detail pane (feature plan v7, 1.19.0; spec-1.19 C1 "In the hero" and
/// "When the game disagrees", C9 "In rows and the hero").
/// <list type="bullet">
/// <item>After the state in the hero: the quest-marker icon (071201, 12 px) and "Offered by the game · 3 Oct" in
/// Secondary, when a Not checked quest turned Ready because the game offered it, or an offer agrees with Ready.</item>
/// <item>A Ready quest the full journal keeps out reads "Ready · journal full", then a copper dot and "Make room to
/// accept it", a link that opens Make room under it (<see cref="MakeRoomView"/>).</item>
/// <item>Above Requirements, while the game offered a quest Tsukimichi reads Blocked: the 1.18 attention card in the
/// plain keyline (nothing is urgent, so no copper), "The game and Tsukimichi disagree", both sides, Go with the game
/// (the primary pill: per character, with the 8 s Undo) and Copy report, and the hint that the "…" takes it back.</item>
/// </list>
/// The lines are rebuilt when the quest, the session, the game's offers, the language or the minute move, so drawing
/// allocates nothing.
/// </summary>
public sealed partial class DetailPane
{
    /// <summary>The game's quest-marker icon: the "!" of a quest not taken yet.</summary>
    private const uint QuestMarkerIcon = 71201;

    private static readonly string GameCopyIcon = FontAwesomeIcon.Copy.ToIconString();

    /// <summary>The Make room popover the hero's link opens; null leaves the link out.</summary>
    public MakeRoomView? MakeRoom { get; set; }

    /// <summary>Go with the game and Use Tsukimichi's answer for the character on view; null leaves both out.</summary>
    public GameAnswerActions? GameAnswers { get; set; }

    private (uint Row, int Version, int Offers, int Language, long Minute) gameAnswerKey = (uint.MaxValue, -1, -1, -1, -1);
    private string? heroOffered;
    private string heroOfferedTooltip = string.Empty;
    private bool heroJournalFull;
    private string? gameCardWhy;
    private float gameCardHeight;
    private double gameCardCopiedUntil;

    // The session the model was last built from (the plugin keeps one), for the hero's item after the state.
    private SessionState? answersSession;

    /// <summary>
    /// Under the model's status: "journal full" after a Ready quest the full journal keeps out (C9). Called where the
    /// model is built.
    /// </summary>
    private void ApplyJournalFull(SessionState session, CatalogBundle bundle, QuestEvaluation? evaluation)
    {
        answersSession = session;
        heroJournalFull = evaluation is not null && session.ViewedSnapshot is { } snapshot
            && JournalSlots.Of(snapshot, bundle.Catalog).KeepsOut(evaluation.State);
        if (!heroJournalFull)
        {
            return;
        }

        model.StatusReason = model.StatusReason.Length == 0 ? Strings.JournalFullWords : model.StatusReason + BlockerText.Separator + Strings.JournalFullWords;
        model.StatusTail = BlockerText.Separator + model.StatusReason;
    }

    /// <summary>Rebuilds the hero's offer line and the disagreement card when their inputs moved.</summary>
    private void RefreshGameAnswers(SessionState session, QuestRecord quest)
    {
        // The minute too: a disagreement lapses an hour after the game last showed the quest (GameOfferChecks.Fresh).
        var key = (quest.RowId, session.Version, session.GameOffersRevision, Localization.Loc.Version, DateTime.UtcNow.Ticks / TimeSpan.TicksPerMinute);
        if (key == gameAnswerKey)
        {
            return;
        }

        gameAnswerKey = key;
        heroOffered = null;
        gameCardWhy = null;
        if (session.ViewedContentId is not { } contentId
            || session.GameOffer(contentId, quest.RowId) is not { } sighting
            || !session.States.TryGetValue(quest.RowId, out var evaluation))
        {
            return;
        }

        var culture = CultureInfo.CurrentCulture;
        var date = UiFormat.ShortDate(sighting.FirstSeenUtc);
        if (evaluation.State is QuestState.Ready or QuestState.ReadyOnOtherJob)
        {
            heroOffered = string.Format(culture, Strings.GameOfferedFormat, date);
            heroOfferedTooltip = evaluation.ByGame == GameAnswer.Override ? Strings.GameOfferedOverrideTooltip : Strings.GameOfferedTooltip;
            return;
        }

        if (GameOfferChecks.Judge(quest, evaluation, sighting, 0, DateTime.UtcNow).Verdict != GameOfferVerdict.Disagrees)
        {
            return;
        }

        var spoilers = session.Spoilers;
        var name = spoilers.DisplayName(quest);
        var state = Strings.StateName(evaluation.State, quest);
        var (first, reason) = GameOfferChecks.Expectation(evaluation, quest, session.Names, session.States);
        gameCardWhy = first is { } expected
            ? string.Format(culture, Strings.GameCardWhyFormat, name, date, state, spoilers.DisplayName(session.Names.Catalog, expected, expected.ToString(CultureInfo.InvariantCulture)))
            : string.Format(culture, Strings.GameCardWhyReasonFormat, name, date, state, reason);
    }

    /// <summary>The full journal's link wins the slot after the state: it is what the player can act on.</summary>
    private bool ShowsMakeRoomLink => heroJournalFull && MakeRoom is not null;

    /// <summary>Whether the hero has an item after its state: Make room to accept it, or the game's offer.</summary>
    private bool HasStateExtra(QuestRecord quest)
    {
        if (answersSession is not { } session)
        {
            return false;
        }

        RefreshGameAnswers(session, quest);
        return ShowsMakeRoomLink || heroOffered is not null;
    }

    /// <summary>
    /// The item after the hero's state: on the same line while it fits before <paramref name="right"/> and
    /// <paramref name="inline"/> allows, else on the line under it.
    /// </summary>
    private void DrawStateExtra(QuestRecord quest, float right, bool inline)
    {
        if (!HasStateExtra(quest))
        {
            return;
        }

        var gap = UiMetrics.Px(8f);
        var lead = UiMetrics.Px(12f) + UiMetrics.Px(5f);
        float textWidth;
        using (Typography.Caption())
        {
            textWidth = ImGui.CalcTextSize(ShowsMakeRoomLink ? Strings.MakeRoomToAccept : heroOffered ?? string.Empty).X;
        }

        var width = lead + textWidth;
        var end = ImGui.GetItemRectMax().X;
        if (inline && !TextFlow.LastItemWrapped() && end + gap + width <= right)
        {
            ImGui.SameLine(0f, gap);
        }

        var line = ImGui.GetTextLineHeight();
        var min = ImGui.GetCursorScreenPos();
        using (Typography.Caption())
        {
            if (!ShowsMakeRoomLink && heroOffered is { } offered)
            {
                DrawOffered(offered, min, line, lead, width);
                return;
            }

            DrawMakeRoomLink(min, line, lead, width, textWidth);
        }

        MakeRoom?.DrawPopover(new Vector2(min.X, min.Y + line + UiMetrics.Px(4f)), above: false, ImGui.GetMainViewport().Size.Y);
    }

    /// <summary>"◉ Offered by the game · 3 Oct": the quest-marker icon at 12 px and the words in Secondary, at caption size.</summary>
    private void DrawOffered(string offered, Vector2 min, float line, float lead, float width)
    {
        var dl = ImGui.GetWindowDrawList();
        var textY = MathF.Round(min.Y + ((line - ImGui.GetTextLineHeight()) * 0.5f));
        var icon = MathF.Round(UiMetrics.Px(12f));
        var iconMin = new Vector2(min.X, MathF.Round(min.Y + ((line - icon) * 0.5f)));
        Orbit.DrawIcon(dl, textures, NodeIcon.Game(QuestMarkerIcon), iconMin, iconMin + new Vector2(icon), 0.85f);
        dl.AddText(new Vector2(min.X + lead, textY), Theme.U32(Theme.Surface.TextSecondary), offered);
        ImGui.Dummy(new Vector2(width, line));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(offered, heroOfferedTooltip);
        }
    }

    /// <summary>"● Make room to accept it": the copper dot beside words in Text, a link that opens Make room (caption size).</summary>
    private void DrawMakeRoomLink(Vector2 min, float line, float lead, float width, float textWidth)
    {
        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        var captionLine = ImGui.GetTextLineHeight();
        var textY = MathF.Round(min.Y + ((line - captionLine) * 0.5f));
        var radius = UiMetrics.Px(3f);
        dl.AddCircleFilled(new Vector2(min.X + radius, min.Y + (line * 0.5f)), radius, Theme.U32(Theme.Copper), 12);
        var clicked = ImGui.InvisibleButton("##makeRoomLink", new Vector2(width, line));
        var hovered = ImGui.IsItemHovered();
        var textMin = new Vector2(min.X + lead, textY);
        dl.AddText(textMin, Theme.U32(s.Text), Strings.MakeRoomToAccept);
        if (hovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
            var underline = MathF.Round(textY + captionLine);
            dl.AddLine(new Vector2(textMin.X, underline), new Vector2(textMin.X + textWidth, underline), Theme.U32(s.Text), UiMetrics.Hairline);
            UiMetrics.Tooltip(Strings.JournalFullRowTooltip);
        }

        Chrome.FocusRing(UiMetrics.Px(4f));
        if (clicked)
        {
            MakeRoom?.Open();
        }
    }

    /// <summary>
    /// The disagreement card above Requirements (spec-1.19 "When the game disagrees"): drawn when the game offered the
    /// quest lately and Tsukimichi reads it Blocked or Locked out. The keyline goes under the words at the height they took
    /// last frame, so the words never move. Returns whether it drew.
    /// </summary>
    private bool DrawGameDisagreement(SessionState session, QuestRecord quest)
    {
        RefreshGameAnswers(session, quest);
        if (gameCardWhy is not { } why)
        {
            gameCardHeight = 0f;
            return false;
        }

        Gap();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, bodyRight - min.X);
        var pad = UiMetrics.Px(Theme.Flair == Flair.Plain ? 8f : 11f);
        var padX = UiMetrics.Px(Theme.Flair == Flair.Plain ? 10f : 14f);
        var rounding = Theme.Flair == Flair.Plain ? 0f : UiMetrics.Px(Theme.Spacing.CardRounding);
        if (gameCardHeight > 0f)
        {
            var max = new Vector2(min.X + width, min.Y + gameCardHeight);
            dl.AddRectFilled(min, max, Theme.U32(s.Raised), rounding);
            dl.AddRect(min, max, Theme.U32(Theme.Glyphs.HighContrast ? s.StrongLine : s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }

        var left = min.X + padX;
        var room = MathF.Max(1f, min.X + width - padX - left);
        var line = ImGui.GetTextLineHeight();

        // The title: the quest-marker icon, then the words semibold in Text.
        var icon = MathF.Round(UiMetrics.Px(Theme.Flair == Flair.Plain ? 14f : 18f));
        var iconMin = new Vector2(left, MathF.Round(min.Y + pad + ((line - icon) * 0.5f)));
        Orbit.DrawIcon(dl, textures, NodeIcon.Game(QuestMarkerIcon), iconMin, iconMin + new Vector2(icon));
        ImGui.SetCursorScreenPos(new Vector2(left + icon + UiMetrics.Px(6f), min.Y + pad));
        Chrome.SemiboldTextWrapped(Strings.GameCardTitle, s.Text, MathF.Max(1f, room - icon - UiMetrics.Px(6f)));

        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(2f)));
        TextFlow.Wrapped(why, room, Theme.U32(s.Text));
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y));
        using (Typography.Caption())
        {
            TextFlow.Wrapped(Strings.GameCardContext, room, Theme.U32(s.TextSecondary));
        }

        // Go with the game (the primary pill) and Copy report, right-aligned.
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetCursorScreenPos().Y + UiMetrics.Px(6f)));
        var actionsTop = ImGui.GetCursorScreenPos().Y;
        var canChoose = GameAnswers is not null && session.ViewedContentId is not null;
        if (canChoose && Chrome.ActionPill("##goWithGame", ActionGlyphs.Reveal, Strings.GoWithGame, PillTone.Primary, true, Strings.GoWithGameTooltip, PillLayout.Row))
        {
            GameAnswers!.GoWithGame(quest);
        }

        var pillBottom = canChoose ? ImGui.GetItemRectMax().Y : actionsTop;
        var pillHeight = canChoose ? ImGui.GetItemRectSize().Y : line;
        if (Diagnostics is { } diagnostics)
        {
            var copied = ImGui.GetTime() < gameCardCopiedUntil;
            var label = copied ? Strings.GameCardCopied : Strings.GameCardCopyReport;
            if (CopyLink("##gameCopy", label, new Vector2(left + room, actionsTop), pillHeight))
            {
                gameCardCopiedUntil = DiagnosticBuilder.TryCopy(diagnostics.Compose(quest), log ?? Plugin.Log) ? ImGui.GetTime() + ReportNoteSeconds : 0.0;
            }

            pillBottom = MathF.Max(pillBottom, ImGui.GetItemRectMax().Y);
        }

        ImGui.SetCursorScreenPos(new Vector2(left, pillBottom + UiMetrics.Px(6f)));
        if (canChoose)
        {
            using (Typography.Caption())
            {
                TextFlow.Wrapped(Strings.GameCardHint, room, Theme.U32(s.TextTertiary));
            }
        }

        var bottom = ImGui.GetCursorScreenPos().Y - ImGui.GetStyle().ItemSpacing.Y + pad;
        var height = MathF.Ceiling(MathF.Max((2f * pad) + line, bottom - min.Y));
        if (gameCardHeight <= 0f)
        {
            // The first frame: the words are drawn and the frame is not; its keyline now, over the empty ground only.
            dl.AddRect(min, new Vector2(min.X + width, min.Y + height), Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }

        gameCardHeight = height;
        ImGui.SetCursorScreenPos(new Vector2(min.X, min.Y + height));
        ImGui.Dummy(new Vector2(width, 0f));
        return true;
    }

    /// <summary>The 1.18 copy link: the copy icon and the label in Secondary, Text on hover, right-aligned at <paramref name="rightTop"/>.</summary>
    private static bool CopyLink(string id, string label, Vector2 rightTop, float height)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        var iconSize = ImGui.CalcTextSize(GameCopyIcon);
        ImGui.PopFont();
        var textSize = ImGui.CalcTextSize(label);
        var width = iconSize.X + UiMetrics.Px(6f) + textSize.X + UiMetrics.Px(8f);
        var min = new Vector2(rightTop.X - width, rightTop.Y);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var dl = ImGui.GetWindowDrawList();
        var ink = Theme.U32(hovered ? Theme.Surface.Text : Theme.Surface.TextSecondary);
        ImGui.PushFont(UiBuilder.IconFont);
        dl.AddText(min + new Vector2(UiMetrics.Px(4f), (height - iconSize.Y) * 0.5f), ink, GameCopyIcon);
        ImGui.PopFont();
        dl.AddText(min + new Vector2(UiMetrics.Px(4f) + iconSize.X + UiMetrics.Px(6f), (height - textSize.Y) * 0.5f), ink, label);
        Chrome.FocusRing(UiMetrics.Px(4f));
        if (hovered)
        {
            UiMetrics.Tooltip(Strings.GameCardCopyReportTooltip);
        }

        return clicked;
    }

    /// <summary>"Use Tsukimichi's answer" in the quest's "…" menu, while the player went with the game on it (C1).</summary>
    private void DrawGameAnswerMenuItem(QuestRecord quest)
    {
        if (GameAnswers is not { } answers || !answers.IsChosen(quest))
        {
            return;
        }

        if (ImGui.MenuItem(Strings.UseOwnAnswer))
        {
            answers.UseOwnAnswer(quest);
        }

        if (ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(Strings.UseOwnAnswerTooltip);
        }
    }
}

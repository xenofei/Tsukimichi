using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Tsukimichi.Core.Companions;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The "Needs you" panel over the game (feature plan v7, 1.18.0, A5; spec-1.18 and needs-you.png): one calm panel at the
/// top centre of the game's viewport, 18 % down (clear of the game's own top-centre banners), 400 px wide, one alert at a
/// time with "+1 more" for the rest. Its look follows the Decoration level and palette as the Todo overlay's does: the
/// kit's card at Full, a tonal panel at Quiet, a flat sheet with a 1 px strong line at Plain, always opaque. A copper bar
/// on its left edge (3 px) is the one colour cue, never alone: the "NEEDS YOU" eyebrow (TrumpGothic at Full; semibold
/// "Needs you" in copper at Quiet and Plain) and the title say it in words. Under them one line of what Tsukimichi did,
/// then Stop all (every running hand-off, as <c>/tsuki stop</c>), the one safe fix when there is one, and Dismiss.
/// <para>
/// It rises 4 px and fades in over <see cref="MotionTokens.Rise"/>, once; no pulse, no blink, no shake. It stays until
/// dismissed or its cause clears, then fades over <see cref="StopDock.LeaveSeconds"/>; under Reduce motion it appears and
/// goes at once. It never takes keyboard focus from the game (no focus on appearing, no nav). Drawn from
/// <c>UiBuilder.Draw</c>.
/// </para>
/// </summary>
public sealed class NeedsYouOverlay
{
    private const float WidthLogical = 400f;
    private const float DownShare = 0.18f;
    private const float PadXLogical = 16f;
    private const float PadYLogical = 10f;
    private const float BarLogical = 3f;
    private const float GapLogical = 8f;

    private const ImGuiWindowFlags Flags =
        ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking
        | ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoScrollWithMouse
        | ImGuiWindowFlags.NoBackground;

    private static readonly string StopIcon = FontAwesomeIcon.Stop.ToIconString();

    private readonly RunStops stops;
    private float height;
    private (int Version, int More, int Language) moreKey = (-1, -1, -1);
    private string moreText = string.Empty;
    private (int Version, int Language) eyebrowKey = (-1, -1);
    private string eyebrowUpper = string.Empty;

    public NeedsYouOverlay(RunStops stops)
    {
        this.stops = stops ?? throw new ArgumentNullException(nameof(stops));
    }

    /// <summary>Draws the panel when an alert waits; nothing otherwise. Never throws into the draw loop.</summary>
    public void Draw()
    {
        if (stops.NeedsYou.Current is not { } alert)
        {
            height = 0f;
            return;
        }

        var now = stops.Now;
        var reduce = UiMetrics.ReduceMotion;
        var alpha = stops.NeedsYou.Alpha(now, reduce);
        var viewport = ImGuiHelpers.MainViewport;
        var width = MathF.Min(UiMetrics.Px(WidthLogical), viewport.Size.X - UiMetrics.Px(32f));
        var measuring = height <= 0f;
        var pos = new Vector2(
            MathF.Round(viewport.Pos.X + ((viewport.Size.X - width) * 0.5f)),
            MathF.Round(viewport.Pos.Y + (viewport.Size.Y * DownShare) + UiMetrics.Px(stops.NeedsYou.Rise(now, reduce))));
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(width, MathF.Max(1f, height)), ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        using var body = Typography.Body();
        var visible = ImGui.Begin("##tsukimichiNeedsYou", Flags);
        try
        {
            if (visible)
            {
                UiMetrics.ApplyFontScale();
                var dl = ImGui.GetWindowDrawList();
                var start = dl.VtxBuffer.Size;
                height = Content(alert, width, now, reduce);
                var fade = measuring ? 0f : alpha;
                if (fade < 1f)
                {
                    Chrome.FadeVertices(dl, start, fade);
                }
            }
        }
        finally
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
        }
    }

    /// <summary>The panel's frame, bar, words and buttons; returns its height.</summary>
    private float Content(NeedsYouAlert alert, float width, double now, bool reduce)
    {
        var flair = Theme.Flair;
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetWindowPos();
        var max = min + new Vector2(width, MathF.Max(1f, height));
        var rounding = UiMetrics.Px(Theme.Spacing.CardRounding);

        // The palette's own sheet, opaque: the panel sits over the game, light palettes included.
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

        var left = min.X + UiMetrics.Px(PadXLogical);
        var right = max.X - UiMetrics.Px(PadXLogical);
        var y = min.Y + UiMetrics.Px(PadYLogical);
        ImGui.SetCursorScreenPos(new Vector2(left, y));

        // The eyebrow and the title on one line.
        if (flair == Flair.Full)
        {
            var upper = EyebrowUpper();
            using (var role = Typography.Eyebrow(upper))
            {
                var text = role.GameFace ? upper : Strings.NeedsYouEyebrow;
                using (Theme.PushText(Theme.OrnamentLight))
                {
                    ImGui.TextUnformatted(text);
                }
            }

            ImGui.SameLine(0f, UiMetrics.Px(GapLogical));
            using (Typography.Title(alert.Title))
            using (Theme.PushText(s.Text))
            {
                ImGui.TextUnformatted(alert.Title);
            }
        }
        else
        {
            Chrome.SemiboldText(Strings.NeedsYouEyebrow, Theme.Copper);
            ImGui.SameLine(0f, UiMetrics.Px(GapLogical));
            Chrome.SemiboldText(alert.Title, s.Text);
        }

        // What Tsukimichi did.
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetItemRectMax().Y + UiMetrics.Px(4f)));
        ImGui.PushTextWrapPos(right - min.X);
        ImGui.BeginGroup();
        using (Typography.Caption())
        using (Theme.PushText(s.TextSecondary))
        {
            ImGui.TextWrapped(alert.Line);
        }

        ImGui.EndGroup();
        ImGui.PopTextWrapPos();

        // Stop all, the fix, Dismiss, and "+1 more" at the right.
        var interactive = stops.NeedsYou.Interactive(now, reduce);
        ImGui.SetCursorScreenPos(new Vector2(left, ImGui.GetItemRectMax().Y + UiMetrics.Px(GapLogical)));
        if (stops.StopAll is { } stopAll
            && Chrome.ActionPill("##needsYouStopAll", StopIcon, Strings.NeedsYouStopAll, PillTone.Normal, true, Strings.NeedsYouStopAllTooltip, PillLayout.Panel)
            && interactive)
        {
            stopAll();
        }

        if (stops.PanelShows(alert))
        {
            ImGui.SameLine(0f, UiMetrics.Px(GapLogical));
            if (Chrome.ActionPill("##needsYouFix", ActionIcons.WalkIcon, stops.Label(alert.Fix), PillTone.Normal, !stops.Retrying, Strings.StopFixReloadRetryTooltip, PillLayout.Panel) && interactive)
            {
                stops.PanelFix(alert);
            }
        }

        ImGui.SameLine(0f, UiMetrics.Px(GapLogical));
        if (DismissButton(interactive))
        {
            stops.NeedsYou.Dismiss(now);
        }

        var rowBottom = ImGui.GetItemRectMax().Y;
        if (stops.NeedsYou.More > 0)
        {
            var more = MoreText();
            using (Typography.Caption())
            {
                var size = ImGui.CalcTextSize(more);
                dl.AddText(ImGui.GetFont(), ImGui.GetFontSize(), new Vector2(right - size.X, MathF.Round(rowBottom - ((Chrome.PillHeight(PillLayout.Panel) + size.Y) * 0.5f))), Theme.U32(GamePanelShell.QuietTone), more);
            }
        }

        var bottom = rowBottom + UiMetrics.Px(PadYLogical);

        // The copper bar, beside the words that say the same.
        dl.AddRectFilled(min, new Vector2(min.X + UiMetrics.Px(BarLogical), MathF.Max(min.Y + 1f, MathF.Min(max.Y, bottom))), Theme.U32(Theme.Copper), rounding, ImDrawFlags.RoundCornersLeft);
        return MathF.Ceiling(bottom - min.Y);
    }

    /// <summary>Dismiss as a quiet text button; true on click while the panel acts.</summary>
    private static bool DismissButton(bool interactive)
    {
        var label = Strings.NeedsYouDismiss;
        var height = Chrome.PillHeight(PillLayout.Panel);
        var width = ImGui.CalcTextSize(label).X + UiMetrics.Px(12f);
        var clicked = ImGui.InvisibleButton("##needsYouDismiss", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var min = ImGui.GetItemRectMin();
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        if (hovered)
        {
            dl.AddRectFilled(min, min + new Vector2(width, height), Theme.U32(s.Hover), height * 0.5f);
        }

        dl.AddText(min + new Vector2(UiMetrics.Px(6f), (height - ImGui.GetTextLineHeight()) * 0.5f), Theme.U32(hovered ? s.Text : s.TextSecondary), label);
        Chrome.FocusRing(height * 0.5f);
        return clicked && interactive;
    }

    /// <summary>"+1 more", composed again only when the count or the language changes.</summary>
    private string MoreText()
    {
        var key = (stops.NeedsYou.Version, stops.NeedsYou.More, Localization.Loc.Version);
        if (key != moreKey)
        {
            moreKey = key;
            moreText = string.Format(CultureInfo.CurrentCulture, Strings.NeedsYouMoreFormat, stops.NeedsYou.More);
        }

        return moreText;
    }

    /// <summary>"NEEDS YOU" for the game face, cased for the language once per language.</summary>
    private string EyebrowUpper()
    {
        var key = (0, Localization.Loc.Version);
        if (key != eyebrowKey)
        {
            eyebrowKey = key;
            eyebrowUpper = SectionHeading.Label(Strings.NeedsYouEyebrow);
        }

        return eyebrowUpper;
    }
}

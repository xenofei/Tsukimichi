using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Game;

namespace Tsukimichi.Ui;

/// <summary>
/// The frame every panel beside a game window (1.7.0) draws in, as the Duty Finder unlock hint draws its own: a small
/// borderless window placed by <see cref="BesidePlacement"/> (right of the game window, else left, below or above,
/// and not at all when no side has room, so it never covers it), drawn transparent and not clickable for
/// <see cref="SettleFrames"/> frames whenever its content changes shape while ImGui settles its auto-resized size.
/// <para>
/// Its colours follow <see cref="Theme.Surface"/>: the window, text and lines of the palette in use (Night, the
/// Dalamud-mapped one, or the high-contrast one, which also draws the panel opaque with a strong border), and a
/// brass rule under the caption only while the Flair setting draws rules (<see cref="Theme.ShowRules"/>).
/// Allocation-free per frame: the content callback is a cached delegate and every string comes built.
/// </para>
/// </summary>
public sealed class GamePanelShell
{
    /// <summary>Frames a reshaped panel is drawn transparent while ImGui measures it.</summary>
    public const int SettleFrames = 2;

    private const float GapPx = 6f;
    private const float RoundingPx = 4f;

    private const ImGuiWindowFlags PanelFlags =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.AlwaysAutoResize |
        ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoDocking |
        ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoNav |
        ImGuiWindowFlags.NoFocusOnAppearing;

    private readonly string windowId;

    private uint drawnKey;
    private int drawnCount = -1;
    private Vector2 size;
    private int settled;

    /// <param name="windowId">The ImGui id of the panel's window ("##…", never shown).</param>
    public GamePanelShell(string windowId)
    {
        this.windowId = windowId ?? throw new ArgumentNullException(nameof(windowId));
    }

    /// <summary>Whether the panel has settled this frame: buttons act only then.</summary>
    public bool Interactive => settled >= SettleFrames;

    /// <summary>Forgets the measured shape: the next draw settles again.</summary>
    public void Reset()
    {
        drawnKey = 0;
        drawnCount = -1;
    }

    /// <summary>
    /// Draws the panel beside <paramref name="target"/>. <paramref name="key"/> (the quest's row id) and
    /// <paramref name="count"/> (how many lines it holds) decide when the size must be measured again; a session
    /// change that keeps both redraws in place, visible and clickable.
    /// </summary>
    public void Draw(in ScreenRect target, uint key, int count, Action content)
    {
        if (key != drawnKey || count != drawnCount)
        {
            drawnKey = key;
            drawnCount = count;
            settled = 0;
        }

        var viewport = ImGuiHelpers.MainViewport;
        var bounds = new ScreenRect(viewport.Pos, viewport.Pos + viewport.Size);
        var gap = GapPx * UiMetrics.Scale;
        Vector2 pos;
        if (settled < SettleFrames)
        {
            // Measuring: drawn transparent where it will most likely go.
            pos = new Vector2(target.Max.X + gap, target.Min.Y);
        }
        else if (!BesidePlacement.TryPlace(in target, size, in bounds, gap, out pos, out _))
        {
            return;
        }

        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        using var style = PushPanelStyle(measuring: settled < SettleFrames);

        // Drawn from a raw UiBuilder.Draw handler, so nothing rebalances a Begin left open: End runs whatever Begin
        // returned and whatever the content throws, and the style scope pops after it.
        var visible = ImGui.Begin(windowId, PanelFlags);
        try
        {
            if (visible)
            {
                size = ImGui.GetWindowSize();
                if (settled < SettleFrames)
                {
                    settled++;
                }

                UiMetrics.ApplyFontScale();
                content();
            }
        }
        finally
        {
            ImGui.End();
        }
    }

    /// <summary>
    /// The colours and frame of every panel drawn beside or over a game window (the 1.7 panels, the Duty Finder hint,
    /// the item hover hint; R3 #11), pushed before its <c>Begin</c>: the palette in use (<see cref="Theme.Surface"/>:
    /// Night, the Dalamud-mapped one under "Follow Dalamud colours", or the high-contrast one, which draws the panel
    /// opaque with a strong border), rounding 4, a 1 px border and padding 8 × 6, and, while <paramref name="measuring"/>,
    /// alpha 0 so a panel settling its size is not seen. Dispose after <c>End</c>.
    /// </summary>
    public static Theme.StyleScope PushPanelStyle(bool measuring)
    {
        var s = Theme.Surface;
        var highContrast = Theme.Glyphs.HighContrast;
        ImGui.PushStyleColor(ImGuiCol.WindowBg, s.Window with { W = highContrast ? 1f : 0.96f });
        ImGui.PushStyleColor(ImGuiCol.Border, highContrast ? s.StrongLine : s.Line);
        ImGui.PushStyleColor(ImGuiCol.Text, s.Text);
        ImGui.PushStyleColor(ImGuiCol.TextDisabled, QuietTone);
        ImGui.PushStyleColor(ImGuiCol.Button, s.Raised with { W = highContrast ? 1f : 0.85f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, s.Hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, s.StrongLine);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, RoundingPx * UiMetrics.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 6f) * UiMetrics.Scale);
        ImGui.PushStyleVar(ImGuiStyleVar.Alpha, measuring ? 0f : 1f);
        return new Theme.StyleScope(7, 4);
    }

    /// <summary>The panels' quieter text: the tertiary tone, the secondary under high contrast (where the tertiary would fall under 4.5 : 1).</summary>
    public static Vector4 QuietTone => Theme.Glyphs.HighContrast ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary;

    /// <summary>
    /// The caption line ("Worth it?", "Locked duty") in the quieter tone. While the Flair setting draws rules (Full and
    /// Quiet) it is a Moon Road heading in small: the sigil before it and a brass rule under it, fading out to the right
    /// (solid under high contrast; R3 #11). Under Plain the caption alone.
    /// </summary>
    public static void Caption(string text)
    {
        if (!Theme.ShowRules)
        {
            ImGui.TextDisabled(text);
            return;
        }

        var start = ImGui.GetCursorScreenPos();
        var line = ImGui.GetTextLineHeight();
        var sigil = MathF.Round(MathF.Min(UiMetrics.Px(HeadingLayout.SigilLogical), line * HeadingLayout.SigilLineShare));
        ImGui.Dummy(new Vector2(sigil, line));
        var dl = ImGui.GetWindowDrawList();
        Ornament.Sigil(dl, new Vector2(start.X + (sigil * 0.5f), MathF.Round(start.Y + (line * 0.5f))), sigil);
        ImGui.SameLine(0f, UiMetrics.Px(HeadingLayout.SigilGapLogical));
        ImGui.TextDisabled(text);

        var pos = ImGui.GetCursorScreenPos();
        var width = MathF.Max(ImGui.GetItemRectMax().X - start.X, UiMetrics.Px(120f));
        var thickness = UiMetrics.Hairline;
        ImGui.Dummy(new Vector2(width, thickness));
        Ornament.Rule(dl, new Vector2(start.X, pos.Y), width, Ornament.RuleAlpha, thickness);
    }

    /// <summary>A quest line: its state moon and its (spoiler-shielded) name.</summary>
    public static void QuestLine(QuestState state, string name)
    {
        var glyph = UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight());
        MoonGlyph.DrawInline(state, glyph);
        ImGui.SameLine();
        ImGui.TextUnformatted(name);
    }

    /// <summary>A secondary line in the quieter text colour.</summary>
    public static void Quiet(string text)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Glyphs.HighContrast ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary))
        {
            ImGui.TextUnformatted(text);
        }
    }

    /// <summary>The verdict line in the accent colour (Moon), wrapped at <paramref name="wrapPx"/>.</summary>
    public static void Verdict(string text, float wrapPx)
    {
        using (ImRaii.PushColor(ImGuiCol.Text, Theme.Accent))
        {
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + wrapPx);
            ImGui.TextUnformatted(text);
            ImGui.PopTextWrapPos();
        }
    }

    /// <summary>A small button that acts only once the panel settled; its tooltip on hover (disabled buttons too).</summary>
    public bool Button(string label, string tooltip, bool enabled = true)
    {
        var clicked = false;
        using (ImRaii.Disabled(!enabled))
        {
            clicked = ImGui.SmallButton(label) && Interactive && enabled;
        }

        if (tooltip.Length > 0 && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
        {
            UiMetrics.Tooltip(tooltip);
        }

        return clicked;
    }

    /// <summary>The drawn moon size for the current line height, for indents that line text up after a quest moon.</summary>
    public static float GlyphIndent() => UiMetrics.InlineGlyphSize(ImGui.GetTextLineHeight()) + ImGui.GetStyle().ItemSpacing.X;

    /// <summary>The panel's wrap width: wide enough for a verdict, never wider than a third of the screen.</summary>
    public static float WrapWidth() => MathF.Min(UiMetrics.Px(300f), ImGuiHelpers.MainViewport.Size.X / 3f);

    /// <summary>
    /// The body every quest panel shares: the quest line, the verdict, the status, the Moonlit rewards with owned or
    /// not, what it unlocks, the chain line and the facts line. A quest the spoiler shield masks shows its placeholder
    /// and the shield's note only.
    /// </summary>
    public static void BriefBody(QuestBrief brief, bool verdict = true)
    {
        var wrap = WrapWidth();
        QuestLine(brief.State, brief.Name);
        if (brief.Masked)
        {
            Quiet(Strings.GamePanelMasked);
            return;
        }

        if (verdict && brief.Verdict.Length > 0)
        {
            Verdict(brief.Verdict, wrap);
        }

        Quiet(brief.StatusText);
        if (brief.Moonlit.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(Strings.GamePanelMoonlitHeading);
            foreach (var reward in brief.Moonlit)
            {
                using (ImRaii.PushIndent(UiMetrics.Px(8f), scaled: false))
                {
                    ImGui.TextUnformatted(reward.Name);
                    ImGui.SameLine();
                    using (ImRaii.PushColor(ImGuiCol.Text, reward.Owned == false ? Theme.Accent : Theme.Glyphs.HighContrast ? Theme.Surface.TextSecondary : Theme.Surface.TextTertiary))
                    {
                        ImGui.TextUnformatted(reward.StatusWord);
                    }
                }
            }
        }

        if (brief.Unlocks.Count > 0)
        {
            ImGui.Spacing();
            ImGui.TextDisabled(Strings.GamePanelUnlocksHeading);
            foreach (var unlock in brief.Unlocks)
            {
                using (ImRaii.PushIndent(UiMetrics.Px(8f), scaled: false))
                {
                    ImGui.TextUnformatted(unlock);
                }
            }
        }

        if (brief.ChainLine.Length > 0 || brief.FactsLine.Length > 0)
        {
            ImGui.Spacing();
        }

        if (brief.ChainLine.Length > 0)
        {
            Quiet(brief.ChainLine);
        }

        if (brief.FactsLine.Length > 0)
        {
            Quiet(brief.FactsLine);
        }
    }

    /// <summary>How many lines <see cref="BriefBody"/> draws for a brief: part of the shape the panel measures.</summary>
    public static int BriefLines(QuestBrief brief) =>
        brief.Masked ? 2 : 3 + brief.Moonlit.Count + brief.Unlocks.Count + (brief.ChainLine.Length > 0 ? 1 : 0) + (brief.FactsLine.Length > 0 ? 1 : 0);

    /// <summary>Whether a brief's quest can be pinned from an in-world panel (pins belong to the character on view).</summary>
    public static bool CanPinLive(SessionState session, QueryRunner runner) => session.IsLive && runner.CanPin;
}

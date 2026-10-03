using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Tsukimichi.Config;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Query;
using Tsukimichi.Core.Ui;
using Tsukimichi.GameData;

namespace Tsukimichi.Ui;

/// <summary>
/// The filter drawer's sheet body (plan v7 UI-2, docs/design/v7/ui/spec.md §2.3), drawn inside the drawer's scrolling
/// body child: <b>Show</b> (Hide completed and Available now, each with its per-category link, and Pinned first),
/// <b>Quick views</b> (the Stalled after stepper) and <b>Advanced</b>. Closed, Advanced is seven summary lines that say
/// what each group holds (a dot marks one off its default) and open it at that group on a click; open, it is the groups
/// themselves: state chips with their medals, expansion and job segments, the Added in combo, a level range over a Dusk
/// track, a Hide · Show · Only segment per reward kind and the More toggles. Every control keeps its binding to
/// <see cref="UiState.Filters"/>. Each Decoration level has its own look (§2.2): moon toggles at Full with the only gold
/// in the body, silver toggles at Quiet, checkboxes and bands at Plain. Rows are laid out on a cursor of their own
/// (<see cref="sheetY"/>), so the sheet's height is known exactly and nothing moves when a value changes.
/// </summary>
public sealed partial class FilterPanel
{
    // Motion key tags ("FCH", "FRV") with a small id in the low half.
    private const uint ChevronTag = 0x0046_4348;
    private const uint RevealTag = 0x0046_5256;

    private const string HideCompletedPopup = "##hideCompletedOverrides";
    private const string AvailablePopup = "##availableOverrides";

    /// <summary>The reward kinds in the Rewards group's order: the common six, then the rest as the game lists them.</summary>
    private static readonly RewardKind[] RewardOrder = BuildRewardOrder();

    /// <summary>The Hide · Show · Only labels, in <see cref="RewardOptionOrder"/>.</summary>
    private static readonly Localization.LocArray RewardOptionLabels = new(static () =>
        [Strings.RewardOptionName(TriState.Hidden), Strings.RewardOptionName(TriState.Show), Strings.RewardOptionName(TriState.Only)]);

    // Whether Advanced is open, and the group a summary line asked to be scrolled to (for a few frames, while the sheet grows).
    private bool advancedOpen;
    private FilterGroup? scrollTo;
    private int scrollFrames;

    // Whether the Rewards group lists every kind, not only the common six.
    private bool allRewardKinds;

    // This frame's sheet: the content's left and right edges, the next row's top (screen px), the level's look.
    private float sheetLeft;
    private float sheetRight;
    private float sheetY;
    private Flair flair;
    private DrawerMetrics metrics;
    private DrawerTones tones;

    private readonly CountText setPill = new(static () => Strings.FilterDrawerSetFormat);
    private readonly CountText hideChangedCaption = new(static () => Strings.FilterDrawerPerCategoryChangedFormat);
    private readonly CountText availableChangedCaption = new(static () => Strings.FilterDrawerPerCategoryChangedFormat);
    private readonly CountText stalledDaysText = new(static () => Strings.StalledDaysFormat);
    private readonly CountText moreKindsText = new(static () => Strings.FilterDrawerMoreKindsFormat);

    // The summary lines' values (FilterGroup order), rebuilt when the filters or the language change.
    private readonly string[] summaryValues = new string[FilterSummary.Groups.Length];
    private readonly bool[] summarySet = new bool[FilterSummary.Groups.Length];
    private FilterSet? summaryFor;
    private int summaryLanguage = -1;
    private string summaryJob = string.Empty;

    // The expansion segments' short names ("ARR", "HW"), built with the expansion list.
    private string[] expansionLabels = [];

    // The More group's toggles, in drawing order (their own lambdas: a static field of the other part may not be set yet).
    private static readonly (Func<string> Label, Func<string> Tooltip, Func<FilterSet, bool> Get, Action<FilterSet, bool> Set, bool NeedsSnapshot)[] MoreToggles =
    [
        (static () => Strings.RepeatableOnly, static () => Strings.RepeatableOnlyTooltip, static f => f.RepeatableOnly, static (f, v) => f.RepeatableOnly = v, false),
        (static () => Strings.SeasonalActiveOnly, static () => Strings.SeasonalActiveOnlyTooltip, static f => f.SeasonalActiveOnly, static (f, v) => f.SeasonalActiveOnly = v, true),
        (static () => Strings.IncludeUnlisted, static () => Strings.IncludeUnlistedTooltip, static f => f.IncludeUnlisted, static (f, v) => f.IncludeUnlisted = v, false),
        (static () => Strings.IncludeOtherPaths, static () => Strings.IncludeOtherPathsTooltip, static f => f.IncludeOtherPaths, static (f, v) => f.IncludeOtherPaths = v, true),
        (static () => Strings.PinnedOnly, static () => Strings.PinnedOnlyTooltip, static f => f.PinnedOnly, static (f, v) => f.PinnedOnly = v, false),
        (static () => Strings.AbandonedOnly, static () => Strings.AbandonedOnlyTooltip, static f => f.AbandonedOnly, static (f, v) => f.AbandonedOnly = v, true),
        (static () => Strings.OnceOnlyStoryFilter, static () => Strings.OnceOnlyStoryFilterTooltip, static f => f.OnceOnlyStory, static (f, v) => f.OnceOnlyStory = v, true),
    ];

    /// <summary>
    /// The sheet body, in the drawer's body child (its side padding pushed by the caller). <paramref name="snapshot"/>
    /// null means browse mode: runtime-only filters are disabled. <paramref name="settings"/> holds the Stalled
    /// threshold. Returns the content's height in pixels, for the drawer to size itself to on the next frame.
    /// </summary>
    public float DrawSheet(CatalogBundle current, CharacterSnapshot? snapshot, Configuration settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        EnsureLists(current);
        var f = ui.Filters;
        var hasSnapshot = snapshot is not null;
        flair = Theme.Flair;
        metrics = DrawerLayout.MetricsFor(flair);
        tones = DrawerTones.For(flair, Theme.Surface);

        var windowPos = ImGui.GetWindowPos();
        sheetLeft = windowPos.X + ImGui.GetWindowContentRegionMin().X;
        sheetRight = MathF.Max(sheetLeft + 1f, windowPos.X + ImGui.GetWindowContentRegionMax().X);
        var top = ImGui.GetCursorScreenPos().Y;
        sheetY = top + UiMetrics.Px(2f);
        UpdateSummary(f, snapshot);

        // Show
        SectionHead(Strings.FilterDrawerShow, null, first: true);
        var hide = f.HideCompleted;
        if (ToggleRow("##hideCompleted", Strings.HideCompleted, PerCategoryCaption(f.PerCategoryHideCompleted, hideChangedCaption), HideCompletedPopup, ref hide, hasSnapshot, Strings.HideCompletedTooltip, metrics.ToggleRow, f.PerCategoryHideCompleted))
        {
            SetHideCompleted(f, hide);
            changed();
        }

        var available = f.AvailableOnly;
        if (ToggleRow("##availableOnly", Strings.AvailableOnly, PerCategoryCaption(f.PerCategoryAvailableOnly, availableChangedCaption), AvailablePopup, ref available, hasSnapshot, Strings.AvailableOnlyTooltip, metrics.ToggleRow, f.PerCategoryAvailableOnly))
        {
            SetAvailableOnly(f, available);
            changed();
        }

        var pinnedFirst = ui.Sort.PinnedFirst;
        if (ToggleRow("##pinnedFirst", Strings.PinnedFirst, Strings.FilterDrawerPinnedFirstCaption, null, ref pinnedFirst, true, Strings.PinnedFirstTooltip, metrics.ToggleRow, null))
        {
            // The sort's pinned-first flag lives beside the filters; MainWindow persists it with the sort.
            ui.Sort = ui.Sort with { PinnedFirst = pinnedFirst };
            ui.MarkQueryDirty();
        }

        // Quick views: the views sit on the toolbar; the sheet keeps the Stalled threshold they read.
        SectionHead(Strings.Presets, null);
        StalledRow(settings);

        // Advanced
        var setCount = FilterSummary.SetCount(f);
        if (SectionHead(Strings.Advanced, setCount > 0 ? setPill.For(setCount) : null, chevron: true, open: advancedOpen))
        {
            advancedOpen = !advancedOpen;
            scrollTo = null;
        }

        var reveal = Motion.Lerp(Motion.Key(RevealTag, 1u), advancedOpen ? 1f : 0f, MotionTokens.RateFor(MotionTokens.Reveal));
        using (ImRaii.PushStyle(ImGuiStyleVar.Alpha, ImGui.GetStyle().Alpha * (advancedOpen ? reveal : 1f - reveal)))
        {
            if (advancedOpen)
            {
                DrawAdvanced(f, snapshot, hasSnapshot);
            }
            else
            {
                DrawSummaryLines();
            }
        }

        if (scrollFrames > 0 && --scrollFrames == 0)
        {
            scrollTo = null;
        }

        // The sheet is one item as tall as its rows, plus the foot padding the body ends on.
        var foot = UiMetrics.Px(flair == Flair.Plain ? 4f : 10f);
        ImGui.SetCursorScreenPos(new Vector2(sheetLeft, sheetY));
        ImGui.Dummy(new Vector2(1f, foot));
        return (sheetY - top) + foot;
    }

    // ------------------------------------------------------------------ section heads

    /// <summary>
    /// A section head (Show · Quick views · Advanced) on its own row, in the Section heading role as the detail pane's
    /// cards draw it (plan v7 UI-1): at Full the game face tracked +0.08 em in capitals, in OrnamentLight with a 1 px
    /// Abyss shadow (falling back to the Lead size); at Quiet the Lead size in the text tone; at Plain the body on the
    /// ledger's band with its line. Then a rule fading to the right (OrnamentLight at Full, the hairline at Quiet) and a
    /// neutral <paramref name="pill"/> at the end. With <paramref name="chevron"/> the whole row is a button with a
    /// chevron that turns as it opens; returns true on its click.
    /// </summary>
    private bool SectionHead(string text, string? pill, bool first = false, bool chevron = false, bool open = false)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        sheetY += first ? 0f : UiMetrics.Px(metrics.SectionGap);
        var upper = SectionHeading.Label(text);
        using var role = SectionRole(upper);
        var label = role.GameFace ? upper : text;
        var tracking = Typography.SectionTracking(in role);
        var line = ImGui.GetTextLineHeight();
        var labelWidth = Chrome.TrackedTextWidth(label, tracking);
        var height = HeadingLayout.SectionRowHeight(flair, UiMetrics.Scale, line);
        var min = new Vector2(sheetLeft, sheetY);
        var max = new Vector2(sheetRight, sheetY + height);
        var mid = MathF.Round(sheetY + (height * 0.5f));

        var clicked = false;
        var hovered = false;
        if (chevron)
        {
            ImGui.SetCursorScreenPos(min);
            clicked = ImGui.InvisibleButton("##advancedHead", max - min);
            hovered = ImGui.IsItemHovered();
            Chrome.FocusRing(UiMetrics.Px(metrics.Rounding));
        }

        if (flair == Flair.Plain)
        {
            // The ledger's band, full bleed across the sheet, with its line under it.
            var windowPos = ImGui.GetWindowPos();
            var left = windowPos.X;
            var right = windowPos.X + ImGui.GetWindowWidth();
            dl.AddRectFilled(new Vector2(left, min.Y), new Vector2(right, max.Y), Ink(hovered ? Vector4.Lerp(tones.HeaderBand, s.Text, 0.04f) : tones.HeaderBand));
            dl.AddRectFilled(new Vector2(left, max.Y), new Vector2(right, max.Y + 1f), Ink(Theme.Tones.Rule));
        }
        else if (hovered)
        {
            dl.AddRectFilled(min - new Vector2(UiMetrics.Px(6f), 0f), max + new Vector2(UiMetrics.Px(6f), 0f), Ink(tones.Hover, 0.5f), UiMetrics.Px(metrics.Rounding));
        }

        var x = sheetLeft;
        if (chevron)
        {
            var turn = Motion.Lerp(Motion.Key(ChevronTag, 1u), open ? 1f : 0f, MotionTokens.RateFor(MotionTokens.Chevron));
            var size = MathF.Max(6f, UiMetrics.Px(7f));
            var ink = flair == Flair.Full ? tones.Heading : s.TextTertiary;
            Chevron(dl, new Vector2(x + (size * 0.5f), mid), size, turn * MathF.PI * 0.5f, Ink(ink));
            x += size + UiMetrics.Px(7f);
        }

        var pillWidth = pill is null ? 0f : PillWidth(pill);
        var ruleEnd = sheetRight - (pill is null ? 0f : pillWidth + UiMetrics.Px(8f));
        var shadow = flair == Flair.Full ? Ink(Theme.Abyss, 0.55f) : 0u;
        Chrome.TrackedTextAt(dl, new Vector2(x, MathF.Round(mid - (line * 0.5f))), MathF.Max(1f, ruleEnd - x), label, Ink(tones.Heading), tracking, shadow);

        var ruleStart = x + labelWidth + UiMetrics.Px(10f);
        if (flair != Flair.Plain && ruleEnd - ruleStart > UiMetrics.Px(8f))
        {
            FadeRule(dl, new Vector2(ruleStart, mid), ruleEnd - ruleStart, flair == Flair.Full ? tones.Heading : Theme.RuleColor, flair == Flair.Full ? 0.55f : 1f, fade: flair == Flair.Full);
        }

        if (pill is not null)
        {
            DrawPill(dl, new Vector2(sheetRight - pillWidth, mid), pill);
        }

        sheetY += height + UiMetrics.Px(HeadingLayout.SectionRow(flair).Gap) + (flair == Flair.Plain ? 1f : 0f);
        return clicked;
    }

    /// <summary>
    /// The Section heading role at this level (plan v7 UI-1): the game face at Full (<see cref="Typography.Section"/>,
    /// falling back to the Lead size), the Lead size at Quiet, the body at Plain.
    /// </summary>
    private Typography.Scope SectionRole(string upper) => flair switch
    {
        Flair.Full => Typography.Section(upper),
        Flair.Quiet => Typography.Lead(),
        _ => default,
    };

    /// <summary>A rule from <paramref name="start"/>: fading to nothing to the right, or flat; it follows the drawer's fade.</summary>
    private static void FadeRule(ImDrawListPtr dl, Vector2 start, float width, Vector4 color, float alpha, bool fade)
    {
        var top = MathF.Floor(start.Y);
        var min = new Vector2(start.X, top);
        var max = new Vector2(start.X + width, top + UiMetrics.Hairline);
        if (!fade || Theme.Glyphs.HighContrast)
        {
            dl.AddRectFilled(min, max, Ink(color, fade ? 1f : alpha));
            return;
        }

        var from = Ink(color, alpha);
        var to = Ink(color, 0f);
        dl.AddRectFilledMultiColor(min, max, from, to, to, from);
    }

    /// <summary>A small filled chevron centred on <paramref name="center"/>, pointing right and turned by <paramref name="angle"/>.</summary>
    private static void Chevron(ImDrawListPtr dl, Vector2 center, float size, float angle, uint color)
    {
        var (sin, cos) = MathF.SinCos(angle);
        var r = size * 0.5f;
        Vector2 Turn(float x, float y) => center + new Vector2((x * cos) - (y * sin), (x * sin) + (y * cos));
        dl.AddTriangleFilled(Turn(-r * 0.55f, -r), Turn(r * 0.75f, 0f), Turn(-r * 0.55f, r), color);
    }

    /// <summary>The width of a neutral count pill for <paramref name="text"/>, in the caption role (Plain: the text alone).</summary>
    private float PillWidth(string text)
    {
        using var caption = Typography.Caption();
        return ImGui.CalcTextSize(text).X + (flair == Flair.Plain ? 0f : 2f * UiMetrics.Px(8f));
    }

    /// <summary>
    /// A neutral count pill ("3 on", "2 set") whose left edge is <paramref name="at"/>.X, centred on
    /// <paramref name="at"/>.Y: never gold, a count asks for nothing. Plain draws the text alone in the secondary tone.
    /// </summary>
    private void DrawPill(ImDrawListPtr dl, Vector2 at, string text)
    {
        using var caption = Typography.Caption();
        var size = ImGui.CalcTextSize(text);
        if (flair == Flair.Plain)
        {
            dl.AddText(new Vector2(at.X, MathF.Round(at.Y - (size.Y * 0.5f))), Ink(Theme.Surface.TextSecondary), text);
            return;
        }

        var height = MathF.Max(UiMetrics.Px(19f), size.Y + UiMetrics.Px(4f));
        var min = new Vector2(at.X, MathF.Round(at.Y - (height * 0.5f)));
        var max = min + new Vector2(size.X + (2f * UiMetrics.Px(8f)), height);
        dl.AddRectFilled(min, max, Ink(tones.Pill), height * 0.5f);
        dl.AddText(new Vector2(min.X + UiMetrics.Px(8f), MathF.Round(at.Y - (size.Y * 0.5f))), Ink(Theme.Surface.Text), text);
    }

    // ------------------------------------------------------------------ toggle rows

    /// <summary>"Per category ›", or "Per category · 2 changed ›" while some categories are overridden.</summary>
    private static string PerCategoryCaption(Dictionary<uint, bool> overrides, CountText changedCaption) =>
        overrides.Count > 0 ? changedCaption.For(overrides.Count) : Strings.FilterDrawerPerCategory;

    /// <summary>
    /// One switch row: the label (wrapping onto a second line when it must), an optional <paramref name="caption"/>
    /// under it, and the toggle at the row's end; at Plain a 14 px checkbox first, then the label and the caption on one
    /// line. A click on the label flips it too. With <paramref name="link"/> the caption is a link that opens that
    /// popup: the per-category <paramref name="overrides"/>, unchanged since 1.0. Returns true on the frame the value
    /// was flipped (<paramref name="value"/> already holds the new value).
    /// </summary>
    private bool ToggleRow(string id, string label, string? caption, string? link, ref bool value, bool enabled, string tooltip, float rowLogical, Dictionary<uint, bool>? overrides)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var flipped = false;
        using var scope = ImRaii.PushId(id);
        using var disabled = ImRaii.Disabled(!enabled);
        var hint = enabled ? tooltip : Strings.NeedsSnapshot;
        var line = ImGui.GetTextLineHeight();
        var width = sheetRight - sheetLeft;

        float captionLine = 0f;
        float captionWidth = 0f;
        if (caption is not null)
        {
            using var role = Typography.Caption();
            captionLine = ImGui.GetTextLineHeight();
            captionWidth = ImGui.CalcTextSize(caption).X;
        }

        var linkHovered = false;
        var linkClicked = false;
        if (flair == Flair.Plain)
        {
            var box = MathF.Max(12f, UiMetrics.Px(14f));
            var gap = UiMetrics.Px(6f);
            var labelX = sheetLeft + box + gap;
            var labelWidth = MathF.Min(ImGui.CalcTextSize(label).X, MathF.Max(1f, sheetRight - labelX));
            var inline = caption is not null && labelX + labelWidth + gap + captionWidth <= sheetRight;
            var lineBox = MathF.Max(UiMetrics.Px(rowLogical), line + UiMetrics.Px(4f));
            var height = lineBox + (caption is not null && !inline ? captionLine + UiMetrics.Px(2f) : 0f);
            var lineTop = sheetY + ((lineBox - line) * 0.5f);

            var boxMin = new Vector2(sheetLeft, MathF.Round(sheetY + ((lineBox - box) * 0.5f)));
            ImGui.SetCursorScreenPos(boxMin);
            if (ImGui.InvisibleButton("##toggle", new Vector2(box)))
            {
                value = !value;
                flipped = true;
            }

            var boxHovered = ImGui.IsItemHovered();
            Tip(hint);
            Chrome.FocusRing(UiMetrics.Px(2f));
            ImGui.SetCursorScreenPos(new Vector2(labelX, lineTop));
            if (ImGui.InvisibleButton("##label", new Vector2(MathF.Max(1f, labelWidth), line)))
            {
                value = !value;
                flipped = true;
            }

            Tip(hint);
            DrawCheckbox(dl, boxMin, box, value, boxHovered || ImGui.IsItemHovered());
            Chrome.EllipsisTextAt(dl, new Vector2(labelX, lineTop), MathF.Max(1f, sheetRight - labelX), label, Ink(s.Text));
            if (caption is not null)
            {
                var captionAt = inline
                    ? new Vector2(labelX + labelWidth + gap, MathF.Round(lineTop + ((line - captionLine) * 0.5f)))
                    : new Vector2(labelX, sheetY + lineBox);
                DrawCaption(dl, captionAt, caption, captionWidth, sheetRight - captionAt.X, link, out linkHovered, out linkClicked);
            }

            sheetY += height;
        }
        else
        {
            var toggle = Chrome.ToggleSize;
            var room = MathF.Max(1f, width - toggle.X - UiMetrics.Px(10f));
            var labelHeight = TextFlow.Height(label, room);
            var block = labelHeight + (caption is not null ? UiMetrics.Px(1f) + captionLine : 0f);
            var height = MathF.Max(UiMetrics.Px(rowLogical), block + UiMetrics.Px(6f));
            var blockTop = MathF.Round(sheetY + ((height - block) * 0.5f));
            var labelWidth = MathF.Min(ImGui.CalcTextSize(label).X, room);

            ImGui.SetCursorScreenPos(new Vector2(sheetLeft, blockTop));
            if (ImGui.InvisibleButton("##label", new Vector2(MathF.Max(1f, labelWidth), labelHeight)))
            {
                value = !value;
                flipped = true;
            }

            Tip(hint);
            TextFlow.DrawClamped(dl, new Vector2(sheetLeft, blockTop), label, room, 3, Ink(s.Text));
            if (caption is not null)
            {
                DrawCaption(dl, new Vector2(sheetLeft, blockTop + labelHeight + UiMetrics.Px(1f)), caption, captionWidth, room, link, out linkHovered, out linkClicked);
            }

            ImGui.SetCursorScreenPos(new Vector2(sheetRight - toggle.X, MathF.Round(sheetY + ((height - toggle.Y) * 0.5f))));
            if (Chrome.MoonToggle("##toggle", ref value, silver: flair == Flair.Quiet))
            {
                flipped = true;
            }

            Tip(hint);
            sheetY += height;
        }

        if (link is not null)
        {
            if (linkHovered)
            {
                UiMetrics.Tooltip(enabled ? Strings.OverridesTooltip : Strings.NeedsSnapshot);
            }

            if (linkClicked)
            {
                ImGui.OpenPopup(link);
            }

            if (overrides is not null)
            {
                OverridesPopup(link, overrides);
            }
        }

        return flipped;
    }

    /// <summary>
    /// A row's caption in the caption role at <paramref name="at"/>, ending in an ellipsis in <paramref name="room"/>:
    /// the tertiary tone, or with <paramref name="link"/> a link in the secondary tone that brightens and underlines on
    /// hover.
    /// </summary>
    private static void DrawCaption(ImDrawListPtr dl, Vector2 at, string caption, float captionWidth, float room, string? link, out bool hovered, out bool clicked)
    {
        var s = Theme.Surface;
        hovered = false;
        clicked = false;
        using var role = Typography.Caption();
        var line = ImGui.GetTextLineHeight();
        var shown = MathF.Max(1f, MathF.Min(captionWidth, room));
        if (link is not null)
        {
            ImGui.SetCursorScreenPos(at);
            clicked = ImGui.InvisibleButton("##link", new Vector2(shown, line));
            hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            Chrome.FocusRing(UiMetrics.Px(2f));
        }

        var ink = link is null ? s.TextTertiary : hovered ? s.Text : s.TextSecondary;
        Chrome.EllipsisTextAt(dl, at, MathF.Max(1f, room), caption, Ink(ink), captionWidth);
        if (hovered)
        {
            var y = MathF.Round(at.Y + line - UiMetrics.Px(1f));
            dl.AddLine(new Vector2(at.X, y), new Vector2(at.X + shown, y), Ink(ink, 0.7f), UiMetrics.Hairline);
        }
    }

    /// <summary>Plain's 14 px checkbox: a sunken square with a line border and a check in the text tone.</summary>
    private static void DrawCheckbox(ImDrawListPtr dl, Vector2 min, float side, bool on, bool hovered)
    {
        var s = Theme.Surface;
        var max = min + new Vector2(side);
        dl.AddRectFilled(min, max, Ink(hovered ? s.Hover : Vector4.Lerp(s.Window, s.Text, 0.07f)), UiMetrics.Px(2f));
        dl.AddRect(min, max, Ink(s.StrongLine with { W = 1f }), UiMetrics.Px(2f), ImDrawFlags.None, UiMetrics.Hairline);
        if (!on)
        {
            return;
        }

        var ink = Ink(s.Text);
        var thickness = MathF.Max(1.5f, UiMetrics.Px(1.6f));
        var a = min + new Vector2(side * 0.22f, side * 0.52f);
        var b = min + new Vector2(side * 0.42f, side * 0.72f);
        var c = min + new Vector2(side * 0.80f, side * 0.28f);
        dl.AddLine(a, b, ink, thickness);
        dl.AddLine(b, c, ink, thickness);
    }

    /// <summary>The per-category overrides behind a "Per category" link: a Default / On / Off choice per journal category.</summary>
    private void OverridesPopup(string popupId, Dictionary<uint, bool> overrides)
    {
        using var popup = ImRaii.Popup(popupId);
        if (!popup)
        {
            return;
        }

        // Opened from the left column (own font scale 1), so the popup scales itself.
        UiMetrics.ApplyFontScale();
        var width = UiMetrics.Px(90f);
        foreach (var (categoryId, name) in categories)
        {
            using var row = ImRaii.PushId((int)categoryId);
            var current = overrides.TryGetValue(categoryId, out var v) ? (v ? 1 : 2) : 0;
            ImGui.SetNextItemWidth(width);
            if (ImGui.Combo("##override", ref current, Strings.OverrideOptions))
            {
                if (current == 0)
                {
                    overrides.Remove(categoryId);
                }
                else
                {
                    overrides[categoryId] = current == 1;
                }

                changed();
            }

            ImGui.SameLine();
            ImGui.TextUnformatted(name);
        }
    }

    // ------------------------------------------------------------------ Quick views

    /// <summary>
    /// "Stalled after" with its caption, and a stepper at the row's end: a pill on the sunken tone with round − and +
    /// buttons (hold one to repeat), 1 to 90 days. On a narrow column the stepper goes under the label.
    /// </summary>
    private void StalledRow(Configuration settings)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var days = settings.StalledDaysClamped;
        var value = stalledDaysText.For(days);
        var line = ImGui.GetTextLineHeight();
        var height = MathF.Max(UiMetrics.Px(metrics.Field), line + UiMetrics.Px(6f));
        var button = MathF.Max(14f, height - UiMetrics.Px(6f));
        var valueWidth = MathF.Max(UiMetrics.Px(54f), ImGui.CalcTextSize(value).X + UiMetrics.Px(10f));
        var stepper = (2f * button) + valueWidth + UiMetrics.Px(6f);
        var width = sheetRight - sheetLeft;
        var stacked = width - stepper - UiMetrics.Px(10f) < UiMetrics.Px(96f);
        var room = stacked ? width : width - stepper - UiMetrics.Px(10f);

        float captionHeight;
        using (Typography.Caption())
        {
            captionHeight = TextFlow.Height(Strings.FilterDrawerStalledCaption, room);
        }

        var block = line + UiMetrics.Px(1f) + captionHeight;
        var rowHeight = stacked ? block : MathF.Max(MathF.Max(UiMetrics.Px(metrics.ToggleRow), height), block + UiMetrics.Px(6f));
        var blockTop = MathF.Round(sheetY + ((rowHeight - block) * 0.5f));
        Chrome.EllipsisTextAt(dl, new Vector2(sheetLeft, blockTop), room, Strings.StalledDaysLabel, Ink(s.Text));
        using (Typography.Caption())
        {
            TextFlow.DrawClamped(dl, new Vector2(sheetLeft, blockTop + line + UiMetrics.Px(1f)), Strings.FilterDrawerStalledCaption, room, 3, Ink(s.TextTertiary));
        }

        var stepperTop = stacked ? blockTop + block + UiMetrics.Px(6f) : MathF.Round(sheetY + ((rowHeight - height) * 0.5f));
        var min = new Vector2(stacked ? sheetLeft : sheetRight - stepper, stepperTop);
        var max = min + new Vector2(stepper, height);
        var rounding = flair == Flair.Plain ? UiMetrics.Px(2f) : height * 0.5f;
        dl.AddRectFilled(min, max, Ink(s.Sunken), rounding);
        dl.AddRect(min, max, Ink(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);

        var inset = (height - button) * 0.5f;
        var next = days;
        if (StepButton("##stalledLess", new Vector2(min.X + inset, min.Y + inset), button, plus: false, Strings.FilterDrawerStalledFewer))
        {
            next--;
        }

        if (StepButton("##stalledMore", new Vector2(max.X - inset - button, min.Y + inset), button, plus: true, Strings.FilterDrawerStalledMore))
        {
            next++;
        }

        var textSize = ImGui.CalcTextSize(value);
        dl.AddText(new Vector2(MathF.Round(min.X + ((stepper - textSize.X) * 0.5f)), MathF.Round(min.Y + ((height - textSize.Y) * 0.5f))), Ink(s.Text), value);
        ImGui.SetCursorScreenPos(new Vector2(min.X + inset + button, min.Y));
        ImGui.Dummy(new Vector2(valueWidth, height));
        Tip(Strings.StalledDaysTooltip);

        next = Math.Clamp(next, Configuration.MinStalledDays, Configuration.MaxStalledDays);
        if (next != days)
        {
            // A change re-runs the query and is saved with the settings.
            settings.StalledDays = next;
            changed();
        }

        sheetY += stacked ? (stepperTop + height) - sheetY : rowHeight;
    }

    /// <summary>
    /// A round − or + button of <paramref name="side"/> px at <paramref name="min"/>: true on the press and then on
    /// every key-repeat tick while held, and on a keyboard activation.
    /// </summary>
    private bool StepButton(string id, Vector2 min, float side, bool plus, string tooltip)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton(id, new Vector2(side));
        var hovered = ImGui.IsItemHovered();
        var step = (ImGui.IsItemActive() && ImGui.IsMouseClicked(ImGuiMouseButton.Left, true)) || (clicked && !ImGui.IsMouseReleased(ImGuiMouseButton.Left));
        Tip(tooltip);
        var center = min + new Vector2(side * 0.5f);
        var rounding = flair == Flair.Plain ? UiMetrics.Px(2f) : side * 0.5f;
        dl.AddRectFilled(min, min + new Vector2(side), Ink(hovered ? s.Hover : s.Raised), rounding);
        Chrome.FocusRing(rounding);

        var ink = Ink(hovered ? s.Text : s.TextSecondary);
        var arm = MathF.Round(side * 0.22f);
        var thickness = MathF.Max(1f, UiMetrics.Px(1.4f));
        dl.AddLine(center - new Vector2(arm, 0f), center + new Vector2(arm, 0f), ink, thickness);
        if (plus)
        {
            dl.AddLine(center - new Vector2(0f, arm), center + new Vector2(0f, arm), ink, thickness);
        }

        return step;
    }

    // ------------------------------------------------------------------ Advanced, closed: the summary lines

    /// <summary>Advanced's seven summary lines: what each group holds, a dot on one off its default; a click opens it there.</summary>
    private void DrawSummaryLines()
    {
        for (var i = 0; i < FilterSummary.Groups.Length; i++)
        {
            var group = FilterSummary.Groups[i];
            if (SummaryLine(i, GroupName(group), summaryValues[i], summarySet[i], last: i == FilterSummary.Groups.Length - 1))
            {
                advancedOpen = true;
                scrollTo = group;
                scrollFrames = 3;
            }
        }
    }

    /// <summary>One summary line: the group's name, its value (with a dot when set; Plain: a gold "*"), and "›".</summary>
    private bool SummaryLine(int index, string name, string value, bool set, bool last)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var line = ImGui.GetTextLineHeight();
        var height = MathF.Max(UiMetrics.Px(metrics.SummaryLine), line + UiMetrics.Px(6f));
        var bleed = UiMetrics.Px(flair == Flair.Plain ? 4f : 8f);
        var min = new Vector2(sheetLeft - bleed, sheetY);
        var max = new Vector2(sheetRight + bleed, sheetY + height);
        using var id = ImRaii.PushId(index);
        ImGui.SetCursorScreenPos(min);
        var clicked = ImGui.InvisibleButton("##summary", max - min);
        var hovered = ImGui.IsItemHovered();
        Tip(Strings.FilterDrawerSummaryTooltip);
        var hover = Motion.Hover(ImGuiP.GetItemID(), hovered);
        if (hover > 0f)
        {
            dl.AddRectFilled(min, max, Ink(tones.Hover, (flair == Flair.Full ? 0.65f : DrawerTones.HoverAlpha) * hover), UiMetrics.Px(metrics.Rounding));
        }

        Chrome.FocusRing(UiMetrics.Px(metrics.Rounding));
        var textY = MathF.Round(sheetY + ((height - line) * 0.5f));
        var chevronWidth = ImGui.CalcTextSize(Chevrons).X;
        dl.AddText(new Vector2(sheetRight - chevronWidth, textY), Ink(hovered ? s.TextSecondary : s.TextDisabled), Chevrons);

        var nameWidth = ImGui.CalcTextSize(name).X;
        var valueRight = sheetRight - chevronWidth - UiMetrics.Px(6f);
        var star = set && flair == Flair.Plain ? ImGui.CalcTextSize(PlainSetMark).X + UiMetrics.Px(2f) : 0f;
        var dot = set && flair != Flair.Plain ? UiMetrics.Px(11f) : 0f;
        var valueRoom = MathF.Max(0f, valueRight - star - dot - (sheetLeft + MathF.Min(nameWidth, UiMetrics.Px(70f)) + UiMetrics.Px(12f)));
        var valueWidth = MathF.Min(ImGui.CalcTextSize(value).X, valueRoom);
        var valueX = valueRight - star - valueWidth;
        Chrome.EllipsisTextAt(dl, new Vector2(sheetLeft, textY), MathF.Max(1f, valueX - dot - UiMetrics.Px(10f) - sheetLeft), name, Ink(s.TextSecondary), nameWidth);
        if (valueWidth > 0f)
        {
            Chrome.EllipsisTextAt(dl, new Vector2(valueX, textY), valueWidth, value, Ink(s.Text));
        }

        if (star > 0f)
        {
            dl.AddText(new Vector2(valueRight - star + UiMetrics.Px(2f), textY), Ink(Theme.Moon), PlainSetMark);
        }
        else if (dot > 0f)
        {
            // A MoonHigh dot at Full, a silver one at Quiet: the value stays in the text tone.
            var center = new Vector2(valueX - UiMetrics.Px(6f), MathF.Round(sheetY + (height * 0.5f)));
            dl.AddCircleFilled(center, MathF.Max(2f, UiMetrics.Px(2.5f)), Ink(flair == Flair.Full ? Theme.MoonHigh : s.Text), 12);
        }

        if (!last && flair != Flair.Plain)
        {
            var y = sheetY + height - UiMetrics.Hairline;
            dl.AddRectFilled(new Vector2(sheetLeft, y), new Vector2(sheetRight, y + UiMetrics.Hairline), Ink(s.Line, 0.45f));
        }

        sheetY += height;
        return clicked;
    }

    private const string Chevrons = "›";
    private const string PlainSetMark = "*";

    /// <summary>A group's name on its summary line and over it when open.</summary>
    private static string GroupName(FilterGroup group) => group switch
    {
        FilterGroup.States => Strings.States,
        FilterGroup.Expansions => Strings.Expansions,
        FilterGroup.AddedIn => Strings.AddedIn,
        FilterGroup.Level => Strings.FilterDrawerLevel,
        FilterGroup.Job => Strings.FilterDrawerJob,
        FilterGroup.Rewards => Strings.FilterDrawerRewards,
        _ => Strings.FilterDrawerMore,
    };

    /// <summary>Rebuilds the summary values when the filters, the current job or the language changed since the last frame.</summary>
    private void UpdateSummary(FilterSet f, CharacterSnapshot? snapshot)
    {
        CurrentJobCategory(snapshot);
        var job = JobPreview(f);
        if (summaryFor is not null && summaryLanguage == Localization.Loc.Version && ReferenceEquals(job, summaryJob) && f.Equals(summaryFor))
        {
            return;
        }

        summaryFor = f.Clone();
        summaryLanguage = Localization.Loc.Version;
        summaryJob = job;
        for (var i = 0; i < FilterSummary.Groups.Length; i++)
        {
            var group = FilterSummary.Groups[i];
            summarySet[i] = FilterSummary.IsSet(f, group);
            summaryValues[i] = SummaryValue(f, group, job);
        }
    }

    /// <summary>A group's value as its summary line says it.</summary>
    private string SummaryValue(FilterSet f, FilterGroup group, string job)
    {
        var culture = CultureInfo.CurrentCulture;
        switch (group)
        {
            case FilterGroup.States:
                var on = FilterSummary.StatesOn(f);
                return on == FilterSummary.StateCount
                    ? string.Format(culture, Strings.FilterDrawerAllStatesFormat, on)
                    : string.Format(culture, Strings.FilterDrawerSomeStatesFormat, on, FilterSummary.StateCount);

            case FilterGroup.Expansions:
                if (f.Expansions.Count == 0)
                {
                    return Strings.FilterDrawerAny;
                }

                var names = new List<string>(f.Expansions.Count);
                foreach (var (id, _) in expansions)
                {
                    if (f.Expansions.Contains(id))
                    {
                        names.Add(Strings.ExpansionShort(id));
                    }
                }

                return string.Join(Strings.ChipStateSeparator, names);

            case FilterGroup.AddedIn:
                return !f.AddedInEngaged() ? Strings.AddedInAny
                    : f.AddedInNewSinceData() ? Strings.AddedInNewSinceData
                    : string.Format(culture, Strings.FilterDrawerAddedInFormat, f.AddedIn);

            case FilterGroup.Level:
                var max = f.LevelMax == FilterSet.NoLevelMax ? LevelCap : f.LevelMax;
                return string.Format(culture, Strings.FilterDrawerLevelFormat, Math.Max(1, (int)f.LevelMin), max);

            case FilterGroup.Job:
                return job;

            case FilterGroup.Rewards:
                var kinds = 0;
                foreach (var value in f.RewardKinds.Values)
                {
                    kinds += value != TriState.Show ? 1 : 0;
                }

                return kinds == 0 ? Strings.FilterDrawerAny : setPill.For(kinds);

            default:
                if (FilterSummary.MoreOn(f) == 0)
                {
                    return Strings.FilterDrawerNoneOn;
                }

                var more = new List<string>(FilterSummary.MoreCount);
                foreach (var toggle in MoreToggles)
                {
                    if (toggle.Get(f))
                    {
                        more.Add(toggle.Label());
                    }
                }

                return string.Join(Strings.ChipStateSeparator, more);
        }
    }

    // ------------------------------------------------------------------ Advanced, open: the groups

    /// <summary>The Advanced groups, each under a small head with its value on the right.</summary>
    private void DrawAdvanced(FilterSet f, CharacterSnapshot? snapshot, bool hasSnapshot)
    {
        GroupHead(FilterGroup.States, first: true);
        StateChips(f);

        GroupHead(FilterGroup.Expansions);
        ExpansionSegments(f);

        GroupHead(FilterGroup.AddedIn, showValue: false);
        using (FieldColors())
        using (FieldShape())
        {
            ImGui.SetCursorScreenPos(new Vector2(sheetLeft, sheetY));
            DrawAddedIn(f, sheetRight - sheetLeft);
        }

        sheetY += FieldHeight();

        GroupHead(FilterGroup.Level);
        LevelRange(f);

        GroupHead(FilterGroup.Job, showValue: false);
        JobRow(f, snapshot);

        GroupHead(FilterGroup.Rewards);
        RewardRows(f);

        GroupHead(FilterGroup.More);
        for (var i = 0; i < MoreToggles.Length; i++)
        {
            var (label, tooltip, get, set, needsSnapshot) = MoreToggles[i];
            var value = get(f);
            var enabled = !needsSnapshot || hasSnapshot;
            if (ToggleRow(MoreIds[i], label(), null, null, ref value, enabled, tooltip(), metrics.SmallRow, null))
            {
                set(f, value);
                changed();
            }
        }
    }

    private static readonly string[] MoreIds = ["##repeatable", "##seasonal", "##unlisted", "##otherPaths", "##pinnedOnly", "##abandoned", "##onceOnly"];

    /// <summary>
    /// A group's head: its name in the text tone and its value on the right in the caption role (the tertiary tone, the
    /// text tone when set). A summary line's pending scroll lands here.
    /// </summary>
    private void GroupHead(FilterGroup group, bool first = false, bool showValue = true)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        sheetY += UiMetrics.Px(first ? 4f : flair == Flair.Plain ? 6f : 12f);
        if (scrollTo == group && scrollFrames > 0)
        {
            ImGui.SetScrollY(MathF.Max(0f, ImGui.GetScrollY() + (sheetY - ImGui.GetWindowPos().Y) - UiMetrics.Px(4f)));
        }

        var line = ImGui.GetTextLineHeight();
        var index = (int)group;
        var name = GroupName(group);
        var valueWidth = 0f;
        if (showValue)
        {
            using var role = Typography.Caption();
            var value = summaryValues[index];
            valueWidth = MathF.Min(ImGui.CalcTextSize(value).X, (sheetRight - sheetLeft) * 0.55f);
            var captionLine = ImGui.GetTextLineHeight();
            Chrome.EllipsisTextAt(dl, new Vector2(sheetRight - valueWidth, MathF.Round(sheetY + ((line - captionLine) * 0.5f))), valueWidth, value, Ink(summarySet[index] ? s.Text : s.TextTertiary));
        }

        Chrome.EllipsisTextAt(dl, new Vector2(sheetLeft, sheetY), MathF.Max(1f, sheetRight - sheetLeft - valueWidth - UiMetrics.Px(8f)), name, Ink(s.Text));
        sheetY += line + UiMetrics.Px(6f);
    }

    /// <summary>
    /// The states as flowing chips, each with its medal: on, a raised fill with a Dusk border; off, dashed with the
    /// medal faded and greyed. A click keeps or drops the state.
    /// </summary>
    private void StateChips(FilterSet f)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        using var role = Typography.Caption();
        var height = MathF.Max(UiMetrics.Px(metrics.Chip), ImGui.GetTextLineHeight() + UiMetrics.Px(6f));
        var medal = MathF.Min(UiMetrics.Px(16f), height - UiMetrics.Px(6f));
        var gap = UiMetrics.Px(5f);
        var x = sheetLeft;
        for (var i = 0; i < StateOrder.Length; i++)
        {
            var state = StateOrder[i];
            var label = Strings.StateName(state);
            var textWidth = ImGui.CalcTextSize(label).X;
            var width = MathF.Min(UiMetrics.Px(5f) + medal + UiMetrics.Px(6f) + textWidth + UiMetrics.Px(10f), sheetRight - sheetLeft);
            if (x > sheetLeft && x + width > sheetRight)
            {
                x = sheetLeft;
                sheetY += height + gap;
            }

            var min = new Vector2(x, sheetY);
            var max = min + new Vector2(width, height);
            var on = f.StateMask.Contains(state);
            using (ImRaii.PushId(i))
            {
                ImGui.SetCursorScreenPos(min);
                if (ImGui.InvisibleButton("##state", max - min))
                {
                    var mask = f.StateMask;
                    f.StateMask = on ? mask & ~state.ToMask() : mask | state.ToMask();
                    on = !on;
                    changed();
                }

                var hovered = ImGui.IsItemHovered();
                Tip(Strings.StatesTooltip);
                var rounding = flair == Flair.Plain ? UiMetrics.Px(3f) : height * 0.5f;
                if (on)
                {
                    var top = Vector4.Lerp(s.Raised, s.Text, hovered ? 0.09f : 0.05f);
                    dl.AddRectFilled(min, max, Ink(top), rounding);
                    dl.AddRect(min, max, Ink(s.TextTertiary, 0.55f), rounding, ImDrawFlags.None, UiMetrics.Hairline);
                }
                else
                {
                    dl.AddRectFilled(min, max, Ink(hovered ? s.Hover : s.Sunken), rounding);
                    DashedOutline(dl, min, max, rounding, Ink(s.StrongLine with { W = 1f }));
                }

                Chrome.FocusRing(rounding);
                var center = new Vector2(min.X + UiMetrics.Px(5f) + (medal * 0.5f), min.Y + (height * 0.5f));
                var from = dl.VtxBuffer.Size;
                MoonGlyph.Draw(dl, center, medal * 0.46f, state);
                DimVertices(dl, from, (on ? 1f : 0.38f) * ImGui.GetStyle().Alpha, on ? 0f : 0.6f);

                var textX = center.X + (medal * 0.5f) + UiMetrics.Px(6f);
                var textY = MathF.Round(min.Y + ((height - ImGui.GetTextLineHeight()) * 0.5f));
                Chrome.EllipsisTextAt(dl, new Vector2(textX, textY), MathF.Max(1f, max.X - UiMetrics.Px(8f) - textX), label, Ink(on ? s.Text : s.TextTertiary), textWidth);
            }

            x += width + gap;
        }

        sheetY += height;
    }

    /// <summary>The expansions as cells that each toggle on their own; none on keeps every expansion.</summary>
    private void ExpansionSegments(FilterSet f)
    {
        var count = Math.Min(expansions.Count, expansionLabels.Length);
        if (count == 0)
        {
            return;
        }

        Span<bool> on = stackalloc bool[count];
        for (var i = 0; i < count; i++)
        {
            on[i] = f.Expansions.Contains(expansions[i].Id);
        }

        var clicked = Segments("##expansions", expansionLabels.AsSpan(0, count), on, sheetLeft, sheetRight - sheetLeft, UiMetrics.Px(metrics.Segment), Strings.ExpansionsTooltip);
        if (clicked >= 0)
        {
            var id = expansions[clicked].Id;
            if (!f.Expansions.Remove(id))
            {
                f.Expansions.Add(id);
            }

            changed();
        }
    }

    /// <summary>
    /// The level range as two field pills ("Lv 1" to "100") and, but at Plain, a 4 px track under them that shows the
    /// range in Dusk. Level 1 at the bottom and 100 at the top read as unbounded.
    /// </summary>
    private void LevelRange(FilterSet f)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var height = FieldHeight();
        var to = Strings.FilterDrawerLevelTo;
        var toWidth = ImGui.CalcTextSize(to).X;
        var gap = UiMetrics.Px(8f);
        var field = MathF.Max(1f, (sheetRight - sheetLeft - toWidth - (2f * gap)) * 0.5f);
        var min = Math.Max(1, (int)f.LevelMin);
        var max = f.LevelMax == FilterSet.NoLevelMax ? LevelCap : (int)f.LevelMax;
        var edited = false;
        using (FieldColors())
        using (FieldShape())
        {
            ImGui.SetCursorScreenPos(new Vector2(sheetLeft, sheetY));
            ImGui.SetNextItemWidth(field);
            edited |= ImGui.DragInt("##levelMin", ref min, 0.25f, 1, max, Strings.LevelFormat, ImGuiSliderFlags.AlwaysClamp);
            Tip(Strings.LevelRangeTooltip);
            ImGui.SetCursorScreenPos(new Vector2(sheetRight - field, sheetY));
            ImGui.SetNextItemWidth(field);
            edited |= ImGui.DragInt("##levelMax", ref max, 0.25f, min, LevelCap, "%d", ImGuiSliderFlags.AlwaysClamp);
            Tip(Strings.LevelRangeTooltip);
        }

        var line = ImGui.GetTextLineHeight();
        dl.AddText(new Vector2(MathF.Round(sheetLeft + field + gap), MathF.Round(sheetY + ((height - line) * 0.5f))), Ink(s.TextTertiary), to);
        if (edited)
        {
            f.LevelMin = min <= 1 ? FilterSet.NoLevelMin : (byte)Math.Clamp(min, 0, LevelCap);
            f.LevelMax = max >= LevelCap ? FilterSet.NoLevelMax : (byte)Math.Clamp(max, 0, LevelCap);
            changed();
        }

        sheetY += height;
        if (flair == Flair.Plain)
        {
            return;
        }

        // The track: the whole range faint, the chosen part in Dusk at every level (gold is the toggles' alone).
        sheetY += UiMetrics.Px(9f);
        var inset = UiMetrics.Px(6f);
        var trackMin = new Vector2(sheetLeft + inset, sheetY);
        var trackMax = new Vector2(sheetRight - inset, sheetY + UiMetrics.Px(4f));
        var radius = (trackMax.Y - trackMin.Y) * 0.5f;
        dl.AddRectFilled(trackMin, trackMax, Ink(s.TextDisabled, 0.45f), radius);
        var span = trackMax.X - trackMin.X;
        var from = trackMin.X + (span * ((min - 1) / (float)(LevelCap - 1)));
        var until = trackMin.X + (span * ((max - 1) / (float)(LevelCap - 1)));
        dl.AddRectFilled(new Vector2(from, trackMin.Y), new Vector2(MathF.Max(from + UiMetrics.Px(4f), until), trackMax.Y), Ink(s.TextTertiary), radius);
        sheetY = trackMax.Y + UiMetrics.Px(2f);
    }

    /// <summary>The job choices as one segment row (All · DoW/DoM · DoH · DoL), then the Current job only toggle.</summary>
    private void JobRow(FilterSet f, CharacterSnapshot? snapshot)
    {
        var labels = JobChoiceLabels.Value;
        Span<bool> on = stackalloc bool[JobChoiceIds.Length];
        for (var i = 0; i < JobChoiceIds.Length; i++)
        {
            on[i] = f.ClassJobCategoryId == JobChoiceIds[i];
        }

        var clicked = Segments("##job", labels, on, sheetLeft, sheetRight - sheetLeft, UiMetrics.Px(metrics.Segment), Strings.JobCategoryTooltip);
        if (clicked >= 0 && !on[clicked])
        {
            f.ClassJobCategoryId = JobChoiceIds[clicked];
            changed();
        }

        sheetY += UiMetrics.Px(4f);
        var currentOnly = CurrentJobCategory(snapshot);
        var current = currentOnly is not null && f.ClassJobCategoryId == currentOnly;
        if (ToggleRow("##currentJob", Strings.JobCurrentOnly, null, null, ref current, currentOnly is not null, Strings.JobCurrentOnlyTooltip, metrics.SmallRow, null))
        {
            f.ClassJobCategoryId = current ? currentOnly : null;
            changed();
        }
    }

    /// <summary>
    /// A Hide · Show · Only segment per reward kind: the common six, any other kind that is set, and the rest behind
    /// "17 more kinds ›". On a narrow column the segment goes under its kind's name.
    /// </summary>
    private void RewardRows(FilterSet f)
    {
        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var labels = RewardOptionLabels.Value;
        var line = ImGui.GetTextLineHeight();
        var segmentHeight = MathF.Max(UiMetrics.Px(metrics.KindSegment), line + UiMetrics.Px(2f));
        var segmentWidth = MathF.Min(UiMetrics.Px(flair == Flair.Plain ? 114f : 126f), sheetRight - sheetLeft);
        var labelRoom = sheetRight - sheetLeft - segmentWidth - UiMetrics.Px(10f);
        var stacked = labelRoom < UiMetrics.Px(64f);
        var hidden = 0;
        Span<bool> on = stackalloc bool[RewardOptionOrder.Length];
        foreach (var kind in RewardOrder)
        {
            if (!FilterSummary.ShowsRewardKind(f, kind, allRewardKinds))
            {
                hidden++;
                continue;
            }

            var current = f.RewardKinds.TryGetValue(kind, out var v) ? v : TriState.Show;
            for (var i = 0; i < RewardOptionOrder.Length; i++)
            {
                on[i] = RewardOptionOrder[i] == current;
            }

            var name = Strings.RewardKindName(kind);
            int clicked;
            using (ImRaii.PushId((int)kind))
            {
                if (stacked)
                {
                    Chrome.EllipsisTextAt(dl, new Vector2(sheetLeft, sheetY), sheetRight - sheetLeft, name, Ink(s.Text));
                    sheetY += line + UiMetrics.Px(3f);
                    clicked = Segments("##kind", labels, on, sheetLeft, sheetRight - sheetLeft, segmentHeight, Strings.RewardKindsTooltip, advance: false);
                    sheetY += segmentHeight + UiMetrics.Px(6f);
                }
                else
                {
                    var rowHeight = MathF.Max(UiMetrics.Px(metrics.SmallRow), segmentHeight + UiMetrics.Px(4f));
                    Chrome.EllipsisTextAt(dl, new Vector2(sheetLeft, MathF.Round(sheetY + ((rowHeight - line) * 0.5f))), MathF.Max(1f, labelRoom), name, Ink(s.Text));
                    var rowTop = sheetY;
                    sheetY = MathF.Round(rowTop + ((rowHeight - segmentHeight) * 0.5f));
                    clicked = Segments("##kind", labels, on, sheetRight - segmentWidth, segmentWidth, segmentHeight, Strings.RewardKindsTooltip, advance: false);
                    sheetY = rowTop + rowHeight;
                }
            }

            if (clicked < 0)
            {
                continue;
            }

            var option = RewardOptionOrder[clicked];
            if (option == TriState.Show)
            {
                f.RewardKinds.Remove(kind);
            }
            else
            {
                f.RewardKinds[kind] = option;
            }

            changed();
        }

        if (hidden > 0 || allRewardKinds)
        {
            sheetY += UiMetrics.Px(4f);
            if (Link("##moreKinds", hidden > 0 ? moreKindsText.For(hidden) : Strings.FilterDrawerFewerKinds))
            {
                allRewardKinds = hidden > 0;
            }
        }
    }

    /// <summary>A text link on its own line in the caption role: the secondary tone, brightening and underlined on hover.</summary>
    private bool Link(string id, string text)
    {
        var dl = ImGui.GetWindowDrawList();
        float width;
        float line;
        using (Typography.Caption())
        {
            width = ImGui.CalcTextSize(text).X;
            line = ImGui.GetTextLineHeight();
        }

        DrawCaption(dl, new Vector2(sheetLeft, sheetY), text, width, sheetRight - sheetLeft, id, out _, out var clicked);
        sheetY += line + UiMetrics.Px(2f);
        return clicked;
    }

    // ------------------------------------------------------------------ shared pieces

    /// <summary>
    /// A row of equal cells at (<paramref name="x"/>, the sheet's cursor), <paramref name="width"/> wide, on a sunken
    /// track: a cell that is <paramref name="on"/> carries a neutral wash and the text tone (never gold). Returns the
    /// index of the cell clicked this frame, or -1. With <paramref name="advance"/> the sheet's cursor moves past it.
    /// </summary>
    private int Segments(string id, ReadOnlySpan<string> labels, ReadOnlySpan<bool> on, float x, float width, float height, string tooltip, bool advance = true)
    {
        var count = labels.Length;
        if (count == 0)
        {
            return -1;
        }

        var s = Theme.Surface;
        var dl = ImGui.GetWindowDrawList();
        var origin = new Vector2(x, sheetY);
        var cell = MathF.Max(1f, width / count);
        var max = origin + new Vector2(width, height);
        var rounding = flair == Flair.Plain ? UiMetrics.Px(2f) : height * 0.5f;
        var clicked = -1;
        dl.AddRectFilled(origin, max, Ink(s.Sunken), rounding);
        using (Typography.Caption())
        using (ImRaii.PushId(id))
        {
            var line = ImGui.GetTextLineHeight();
            var pad = UiMetrics.Px(4f);
            for (var i = 0; i < count; i++)
            {
                var cellMin = new Vector2(origin.X + (i * cell), origin.Y);
                var cellMax = new Vector2(i == count - 1 ? max.X : cellMin.X + cell, max.Y);
                using var cellId = ImRaii.PushId(i);
                ImGui.SetCursorScreenPos(cellMin);
                if (ImGui.InvisibleButton("##cell", cellMax - cellMin))
                {
                    clicked = i;
                }

                var hovered = ImGui.IsItemHovered();
                Tip(tooltip);
                var corners = count == 1 ? ImDrawFlags.RoundCornersAll
                    : i == 0 ? ImDrawFlags.RoundCornersLeft
                    : i == count - 1 ? ImDrawFlags.RoundCornersRight
                    : ImDrawFlags.RoundCornersNone;
                if (on[i])
                {
                    dl.AddRectFilled(cellMin, cellMax, Ink(s.Text, 0.10f), rounding, corners);
                }
                else if (hovered)
                {
                    dl.AddRectFilled(cellMin, cellMax, Ink(s.Hover), rounding, corners);
                }

                if (i > 0)
                {
                    dl.AddLine(new Vector2(cellMin.X, origin.Y), new Vector2(cellMin.X, max.Y), Ink(s.Line), UiMetrics.Hairline);
                }

                Chrome.FocusRing(rounding);
                var label = labels[i];
                var textWidth = ImGui.CalcTextSize(label).X;
                var room = MathF.Max(1f, cellMax.X - cellMin.X - (2f * pad));
                var textX = cellMin.X + pad + MathF.Max(0f, (room - textWidth) * 0.5f);
                Chrome.EllipsisTextAt(dl, new Vector2(MathF.Round(textX), MathF.Round(origin.Y + ((height - line) * 0.5f))), room, label, Ink(on[i] ? s.Text : s.TextSecondary), textWidth);
            }
        }

        dl.AddRect(origin, max, Ink(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        if (advance)
        {
            sheetY += height;
        }

        return clicked;
    }

    /// <summary>A field pill's height at this level: 28 px (Plain 20), never under the text.</summary>
    private float FieldHeight() => MathF.Max(UiMetrics.Px(metrics.Field), ImGui.GetTextLineHeight() + UiMetrics.Px(4f));

    /// <summary>The field pill's colours for the combo and the level fields: sunken, a line border.</summary>
    private static IDisposable FieldColors()
    {
        var s = Theme.Surface;
        return ImRaii.PushColor(ImGuiCol.FrameBg, s.Sunken)
            .Push(ImGuiCol.FrameBgHovered, s.Hover)
            .Push(ImGuiCol.FrameBgActive, s.Hover)
            .Push(ImGuiCol.Border, s.Line);
    }

    /// <summary>The field pill's shape: rounded to a pill (Plain: 2 px), as tall as <see cref="FieldHeight"/>, with a 1 px border.</summary>
    private IDisposable FieldShape()
    {
        var height = FieldHeight();
        var padY = MathF.Max(0f, (height - ImGui.GetTextLineHeight()) * 0.5f);
        return ImRaii.PushStyle(ImGuiStyleVar.FrameRounding, flair == Flair.Plain ? UiMetrics.Px(2f) : height * 0.5f)
            .Push(ImGuiStyleVar.FramePadding, new Vector2(UiMetrics.Px(12f), padY))
            .Push(ImGuiStyleVar.FrameBorderSize, UiMetrics.Hairline);
    }

    /// <summary>
    /// A dashed outline round a pill (or a rounded box) from <paramref name="min"/> to <paramref name="max"/>: short
    /// dashes laid along its perimeter, the arcs included, so an "off" chip reads as a place a state could be.
    /// </summary>
    private static void DashedOutline(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, uint color)
    {
        var r = MathF.Min(rounding, MathF.Min(max.X - min.X, max.Y - min.Y) * 0.5f);
        var straightX = MathF.Max(0f, max.X - min.X - (2f * r));
        var straightY = MathF.Max(0f, max.Y - min.Y - (2f * r));
        var arc = MathF.PI * 0.5f * r;
        var perimeter = (2f * straightX) + (2f * straightY) + (4f * arc);
        var dash = MathF.Max(2f, UiMetrics.Px(3f));
        var gap = MathF.Max(2f, UiMetrics.Px(2.5f));
        var thickness = UiMetrics.Hairline;
        for (var at = 0f; at < perimeter; at += dash + gap)
        {
            var end = MathF.Min(perimeter, at + dash);
            var a = Perimeter(at);
            var mid = Perimeter((at + end) * 0.5f);
            var b = Perimeter(end);
            dl.AddLine(a, mid, color, thickness);
            dl.AddLine(mid, b, color, thickness);
        }

        // The point at distance d along the outline, clockwise from the top edge's left end.
        Vector2 Perimeter(float d)
        {
            var o = new Vector2(0.5f);
            if (d < straightX)
            {
                return new Vector2(min.X + r + d, min.Y) + o;
            }

            d -= straightX;
            if (d < arc)
            {
                return Corner(new Vector2(max.X - r, min.Y + r), -MathF.PI * 0.5f, d) - new Vector2(0.5f, -0.5f);
            }

            d -= arc;
            if (d < straightY)
            {
                return new Vector2(max.X, min.Y + r + d) - new Vector2(0.5f, -0.5f);
            }

            d -= straightY;
            if (d < arc)
            {
                return Corner(new Vector2(max.X - r, max.Y - r), 0f, d) - new Vector2(0.5f);
            }

            d -= arc;
            if (d < straightX)
            {
                return new Vector2(max.X - r - d, max.Y) - new Vector2(-0.5f, 0.5f);
            }

            d -= straightX;
            if (d < arc)
            {
                return Corner(new Vector2(min.X + r, max.Y - r), MathF.PI * 0.5f, d) + new Vector2(0.5f, -0.5f);
            }

            d -= arc;
            if (d < straightY)
            {
                return new Vector2(min.X, max.Y - r - d) + new Vector2(0.5f, -0.5f);
            }

            d -= straightY;
            return Corner(new Vector2(min.X + r, min.Y + r), MathF.PI, MathF.Min(d, arc)) + o;
        }

        Vector2 Corner(Vector2 center, float start, float along)
        {
            var angle = start + (r > 0f ? along / r : 0f);
            var (sin, cos) = MathF.SinCos(angle);
            return center + new Vector2(cos * r, sin * r);
        }
    }

    /// <summary>
    /// Fades the vertices drawn since <paramref name="from"/> to <paramref name="alpha"/> and greys them toward their own
    /// lightness by <paramref name="desaturate"/> (0 keeps the colour): an "off" chip's medal at 0.38, half grey.
    /// </summary>
    private static void DimVertices(ImDrawListPtr dl, int from, float alpha, float desaturate)
    {
        var vertices = dl.VtxBuffer;
        var keep = Math.Clamp(alpha, 0f, 1f);
        var grey = Math.Clamp(desaturate, 0f, 1f);
        if (keep >= 1f && grey <= 0f)
        {
            return;
        }

        for (var i = Math.Max(0, from); i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var col = vertex.Col;
            float r = col & 0xFF;
            float g = (col >> 8) & 0xFF;
            float b = (col >> 16) & 0xFF;
            var a = (col >> 24) * keep;
            var luma = (0.299f * r) + (0.587f * g) + (0.114f * b);
            r += (luma - r) * grey;
            g += (luma - g) * grey;
            b += (luma - b) * grey;
            vertex.Col = (uint)MathF.Round(r) | ((uint)MathF.Round(g) << 8) | ((uint)MathF.Round(b) << 16) | ((uint)MathF.Round(a) << 24);
            vertices[i] = vertex;
        }
    }

    /// <summary><paramref name="color"/> at its own alpha times <paramref name="alpha"/>, times the style's alpha (the drawer's fade, a disabled row).</summary>
    private static uint Ink(Vector4 color, float alpha = 1f) => ImGui.GetColorU32(color with { W = color.W * alpha });

    private static RewardKind[] BuildRewardOrder()
    {
        var order = new List<RewardKind>(FilterSummary.CommonRewardKinds);
        foreach (var kind in Enum.GetValues<RewardKind>())
        {
            if (!FilterSummary.IsCommonRewardKind(kind))
            {
                order.Add(kind);
            }
        }

        return [.. order];
    }
}

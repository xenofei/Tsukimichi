using System;
using System.Globalization;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Evaluation;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Ui;

/// <summary>
/// The detail pane's hero at each Decoration level (docs/design/flair-v13/spec.md §1, "Banners") and its sections.
/// <para>
/// <b>Full, the banner.</b> Every quest has one, through the fallback chain resolved once per catalog
/// (<see cref="BannerIndexSource{TKey}"/>): its own journal banner, a related quest's, the duty it unlocks, the zone
/// where it starts, then the bundled category art. Game art is read from the player's install at runtime; when the
/// spoiler shield withholds it the category art stands in, which spoils nothing. It is 156 px tall at most, with a 1 px
/// brass keyline and a soft shadow, and night-graded as it is drawn (<see cref="BannerGrading"/>: a multiply toward
/// indigo, a desaturation, a scrim to Night, a moonlight wash and a faint moon road), so a daylight zone sits in the
/// moonlit window. A drawn night sky holds its place while a texture loads. The banner's tooltip names its source.
/// The hero medal (80 px) rises over its bottom-left edge, fading in over 1.1 s on a new selection (never under Reduce
/// motion), with a gold halo only for Ready and Ready on another job; the title is on the art in the Title role.
/// </para>
/// <para>
/// <b>Quiet, the plate.</b> The name and its path over a hairline, then a 52 px light-rim medal beside the state in its
/// ink and the reason. <b>Plain, the ledger.</b> The name and its path, then State, Level and Giver as a key-value list
/// with a 12 px flat glyph; no banner and no medal plate.
/// </para>
/// <para>
/// <b>Sections</b> are cards (<see cref="Chrome.BeginCard(string, string?, string?, CardKind, Vector4?, bool)"/>): gilt
/// brass at Full, tonal planes at Quiet, heading rows with a line under them at Plain.
/// </para>
/// Nothing here allocates per frame: strings are composed when the selection changes, headings cased once per string
/// (<see cref="SectionHeading.Label"/>).
/// </summary>
public sealed partial class DetailPane
{
    private const float HeroBannerMaxLogical = 156f;
    private const float WideAspect = 156f / 380f;
    private const float NarrowAspect = 1f / 2.6f;
    private const double MoonriseSeconds = 1.1;
    private const double TitleDelaySeconds = 0.08;
    private const float MoonriseLogical = 8f;

    /// <summary>The hero medal's keyline radius at Full (an 80 px medal), and the plate's at Quiet (52 px), logical px.</summary>
    private const float HeroMedalLogical = 39.5f;

    private const float PlateMedalLogical = 25.7f;

    /// <summary>The ledger's label column and its 12 px glyph's radius at Plain, logical px.</summary>
    private const float LedgerLabelLogical = 78f;

    private const float LedgerGlyphLogical = 5.9f;

    /// <summary>The title drawn on the art: a warm near-white over the night grade (the palette's scene; navy ink on a light one).</summary>
    private static Vector4 TitleOnArt => Theme.Scene.BannerTitle;

    // The moonrise: the row it last started for and when.
    private uint riseRowId = uint.MaxValue;
    private double riseStart;

    // The hero's location line's ladder ("Main Scenario › Dawntrail · Lv 100", "Dawntrail · Lv 100", "Lv 100"), built
    // when the selection or its journal segments change.
    private string[]? heroPathFrom;
    private uint heroPathRow = uint.MaxValue;
    private string[] heroPath = [];

    /// <summary>Every quest's hero banner; null until the plugin attaches it, when a quest shows its own banner or its category art.</summary>
    public BannerIndexSource<DutyUnlockIndex>? Banners { get; set; }

    // ------------------------------------------------------------------ banner choice

    /// <summary>
    /// The banner to draw for <paramref name="quest"/>: the chain's choice, or its category art when the spoiler shield
    /// withholds it (<see cref="BannerShield"/>: a borrowed banner by its donor's own state, a duty's once the quest is
    /// completed). Looked up per frame (frozen dictionary reads), so an index that finishes building while the quest is
    /// shown takes over at once.
    /// </summary>
    private BannerChoice CurrentBanner(QuestRecord quest)
    {
        var choice = ChainBanner(quest);
        return BannerShield.Apply(in choice, quest, model.State, lastSpoilers ?? Core.Query.SpoilerMask.None, lastCatalog, lastStates);
    }

    /// <summary>The chain's choice before the shield.</summary>
    private BannerChoice ChainBanner(QuestRecord quest) => Banners?.For(quest) ?? BannerIndex.Resolve(quest, null, null);

    /// <summary>Whether the spoiler shield withholds game art this quest would otherwise show.</summary>
    private bool ArtworkWithheld(QuestRecord quest)
    {
        var choice = ChainBanner(quest);
        return !BannerShield.Allows(in choice, quest, model.State, lastSpoilers ?? Core.Query.SpoilerMask.None, lastCatalog, lastStates);
    }

    /// <summary>The banner tooltip's caption for the source drawn.</summary>
    private static string BannerCaption(BannerSource shown) => shown switch
    {
        BannerSource.Own => Strings.DetailBannerOwn,
        BannerSource.Sibling => Strings.DetailBannerSibling,
        BannerSource.Duty => Strings.DetailBannerDuty,
        BannerSource.Zone => Strings.DetailBannerZone,
        _ => Strings.DetailBannerCategory,
    };

    /// <summary>
    /// The banner's source on hover, over the banner where no item (the moon, the badge, the state pill) is hovered;
    /// with the shield withholding the game's art, why the category art shows. Nothing while it is still loading.
    /// </summary>
    private void BannerTooltip(Vector2 min, Vector2 max, BannerSource shown)
    {
        if (shown == BannerSource.None || ImGui.IsAnyItemHovered() || !ImGui.IsWindowHovered() || !ImGui.IsMouseHoveringRect(min, max))
        {
            return;
        }

        if (model.Quest is { } quest && ArtworkWithheld(quest))
        {
            UiMetrics.Tooltip(BannerCaption(shown), Strings.ArtworkHidden);
            return;
        }

        UiMetrics.Tooltip(BannerCaption(shown));
    }

    // ------------------------------------------------------------------ hero

    /// <summary>
    /// The location line's ladder (<see cref="LocationLine.Rungs"/>): the journal path and the level, without the path's
    /// prefix, then the level alone; built once per selection.
    /// </summary>
    private string[] HeroLocation(QuestRecord quest)
    {
        if (!ReferenceEquals(heroPathFrom, model.JournalSegments) || heroPathRow != quest.RowId)
        {
            heroPathFrom = model.JournalSegments;
            heroPathRow = quest.RowId;
            var level = string.Format(CultureInfo.CurrentCulture, Strings.DiscoveryLevelFormat, quest.DisplayLevel);
            heroPath = LocationLine.Rungs(model.JournalSegments, JournalSeparator, level);
        }

        return heroPath;
    }

    /// <summary>
    /// The location line on the art (spec-1.16 §A5): the longest rung of <paramref name="rungs"/> that fits
    /// <paramref name="room"/> on one line, never an ellipsis and never a wrap; the shortest is clipped to the banner if
    /// even it does not fit. On a light palette it takes the title's white shadow and bloom (<see cref="Chrome.ArtTextAt"/>).
    /// </summary>
    private static void LocationOnArt(ImDrawListPtr dl, Vector2 pos, float room, string[] rungs, Vector2 clipMin, Vector2 clipMax, float alpha)
    {
        if (rungs.Length == 0)
        {
            return;
        }

        Span<float> widths = stackalloc float[Math.Min(rungs.Length, 4)];
        for (var i = 0; i < widths.Length; i++)
        {
            widths[i] = ImGui.CalcTextSize(rungs[i]).X;
        }

        var pick = LocationLine.Fit(widths, room);
        dl.PushClipRect(clipMin, clipMax, true);
        Chrome.ArtTextAt(dl, pos, MathF.Max(room, widths[pick]), rungs[pick], Theme.WithAlphaVector(Theme.Surface.TextSecondary, alpha), widths[pick], alpha);
        dl.PopClipRect();
    }

    /// <summary>Full's hero: the night-graded banner, the medal rising over its edge, the title on the art (see the type's summary).</summary>
    private void DrawMoonRoadHero(QuestRecord quest)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, bodyRight - min.X);
        var stacked = DetailTiers.Stacks(tier);
        var height = MathF.Max(UiMetrics.Px(40f), MathF.Min(UiMetrics.Px(HeroBannerMaxLogical), width * (stacked ? NarrowAspect : WideAspect)));
        var max = min + new Vector2(width, height);
        var rounding = UiMetrics.Px(5f);
        var highContrast = Theme.Glyphs.HighContrast;

        // The banner on its shadow (0 6 18), graded at draw time, or the drawn sky while it loads, on the Abyss letterbox.
        if (!highContrast)
        {
            Ornament.DropShadow(dl, min, max, rounding, UiMetrics.Px(6f), UiMetrics.Px(18f), 0.45f);
        }

        dl.AddRectFilled(min, max, Theme.DeepU32, rounding);
        var choice = CurrentBanner(quest);
        var shown = BannerSource.None;
        if (BannerArtwork.TryGetWrap(textures, in choice, out var wrap, out var drawn) && wrap.Width > 0 && wrap.Height > 0)
        {
            if (FlairRules.BannerGrade(Theme.Flair) && !highContrast)
            {
                BannerGrading.DrawImage(dl, wrap, BannerGrading.KeyFor(in choice, drawn), min, max, rounding);
            }
            else
            {
                Chrome.ImageCoverAt(dl, wrap.Handle, min, max, new Vector2(wrap.Width, wrap.Height), rounding);
            }

            shown = drawn;
        }
        else
        {
            NightSky(dl, min, max, rounding, quest.RowId);
        }

        HeroScrims(dl, min, max, rounding);
        dl.AddRect(min, max, Theme.WithAlpha(Theme.Surface.OrnamentHigh, Theme.OrnamentAlpha(0.42f)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        ImGui.Dummy(new Vector2(width, height));

        // The special badge, top right on the banner.
        if (quest.IconSpecial != 0)
        {
            var pad = UiMetrics.Px(8f);
            var badgeSize = MathF.Min(UiMetrics.BannerBadgeSize, UiMetrics.Px(22f));
            var badgeMin = new Vector2(max.X - pad - badgeSize, min.Y + pad);
            if (DrawSpecialBadge(dl, quest, badgeMin, badgeSize))
            {
                ImGui.SetCursorScreenPos(badgeMin);
                ImGui.InvisibleButton("##heroBadge", new Vector2(badgeSize, badgeSize));
                if (ImGui.IsItemHovered())
                {
                    UiMetrics.Tooltip(BadgeTooltip(quest));
                }
            }
        }

        // The hero medal: over the banner's bottom-left edge (three quarters of it on the art), or centred when narrow.
        var radius = MathF.Max(UiMetrics.Px(14f), MathF.Min(UiMetrics.Px(stacked ? 26f : HeroMedalLogical), MathF.Min(width * 0.11f, height * 0.36f)));
        var (moonIn, titleIn) = Moonrise(quest.RowId);
        var centerX = stacked ? min.X + (width * 0.5f) : min.X + UiMetrics.Px(12f) + radius;
        var center = new Vector2(centerX, max.Y - (radius * 0.25f) + ((1f - moonIn) * UiMetrics.Px(MoonriseLogical)));
        var firstVertex = dl.VtxBuffer.Size;
        if (FlairRules.HeroHalo(Theme.Flair, model.State))
        {
            // The halo is light: only on what the player can take now, gold, soft (glow is never on the blocked or done).
            HeroHalo(dl, center, radius);
        }

        if (!highContrast)
        {
            dl.AddCircleFilled(center + new Vector2(0f, UiMetrics.Px(4f)), radius + UiMetrics.Px(1f), Theme.DropShadow(0.45f), 40);
        }

        MoonWax.Draw(dl, center, radius, model.State, quest.RowId, model.ReadyOnJob);
        if (moonIn < 1f)
        {
            FadeVertices(dl, firstVertex, moonIn);
        }

        var box = radius;
        ImGui.SetCursorScreenPos(new Vector2(centerX - box, center.Y - box));
        ImGui.InvisibleButton("##heroMoon", new Vector2(box * 2f, box * 2f));
        Chrome.FocusRing(box);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        BannerTooltip(min, max, shown);

        // The title (Title role) and the path on the art, beside the medal, while they fit there; else under the banner.
        var pad2 = UiMetrics.Px(12f);
        var artLeft = stacked ? min.X + pad2 : centerX + radius + pad2;
        var artRoom = max.X - pad2 - artLeft;
        var location = HeroLocation(quest);
        float titleLine;
        float titleWidth;
        using (Typography.HeroTitle(model.DisplayName))
        {
            titleLine = ImGui.GetTextLineHeight();
            titleWidth = ImGui.CalcTextSize(model.DisplayName).X;
        }

        float pathLine;
        using (Typography.Caption())
        {
            pathLine = ImGui.GetTextLineHeight();
        }

        var onArt = !stacked && artRoom > UiMetrics.Px(80f) && titleLine + pathLine + UiMetrics.Px(16f) <= height;
        var bottom = center.Y + radius;
        if (onArt)
        {
            var pathY = MathF.Round(max.Y - pad2 - pathLine);
            var titleY = MathF.Round(pathY - UiMetrics.Px(2f) - titleLine);
            bool cut;
            using (Typography.HeroTitle(model.DisplayName))
            {
                cut = Chrome.ArtTextAt(dl, new Vector2(artLeft, titleY), artRoom, model.DisplayName, Theme.WithAlphaVector(TitleOnArt, titleIn), titleWidth, titleIn);
            }

            using (Typography.Caption())
            {
                LocationOnArt(dl, new Vector2(artLeft, pathY), artRoom, location, min, max, titleIn);
            }

            if (cut && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(new Vector2(artLeft, titleY), new Vector2(artLeft + artRoom, titleY + titleLine)))
            {
                UiMetrics.Tooltip(model.DisplayName);
            }

            // The chips beside the medal, under the banner.
            ImGui.SetCursorScreenPos(new Vector2(artLeft, max.Y + UiMetrics.Px(6f)));
            ImGui.BeginGroup();
            DrawChips(model.HeaderSegments, MathF.Max(1f, bodyRight - artLeft));
            ImGui.EndGroup();
            bottom = MathF.Max(bottom, ImGui.GetItemRectMax().Y);
        }
        else
        {
            var textLeft = stacked ? min.X : centerX + radius + UiMetrics.Px(8f);
            var textTop = stacked ? center.Y + radius + UiMetrics.Px(4f) : max.Y + UiMetrics.Px(6f);
            var room = MathF.Max(1f, bodyRight - textLeft);
            ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
            ImGui.BeginGroup();
            using (Typography.HeroTitle(model.DisplayName))
            {
                TextFlow.Wrapped(model.DisplayName, room, Theme.WithAlpha(Theme.Surface.Text, titleIn));
            }

            SegmentFlow(model.JournalSegments, JournalSeparator, room, Theme.Surface.TextDisabled);
            ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
            DrawChips(model.HeaderSegments, room);
            ImGui.EndGroup();
            bottom = MathF.Max(bottom, ImGui.GetItemRectMax().Y);
        }

        // The hero as one block: at least down to the medal's foot.
        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, bottom - min.Y + UiMetrics.Px(4f)));
    }

    /// <summary>The hero medal's halo (Ready and Ready on another job only): Moon at 0.30 at the medal's edge, fading out 14 px beyond.</summary>
    private static void HeroHalo(ImDrawListPtr dl, Vector2 center, float radius)
    {
        const int Rings = 7;
        var reach = UiMetrics.Px(14f);
        var washes = Theme.Washes;
        var wash = Theme.Scene.Washes.HeroHalo;
        for (var i = Rings; i >= 1; i--)
        {
            var t = i / (float)Rings;

            // On a light palette a warm wash (#E9C46A .30 at the edge → 0; spec-1.16 §A4), never light.
            var ring = washes ? Theme.WithAlpha(wash, wash.W * (1f - t) * 0.55f) : Theme.Glow(0.30f * (1f - t) * 0.55f + 0.02f);
            dl.AddCircleFilled(center, radius + (reach * t), ring, 48);
        }
    }

    /// <summary>
    /// Quiet's hero, the plate: the name (Quiet's title face, 1.40×) and its path in the tertiary tone, a hairline, then the 52 px
    /// medal beside the state in its ink, the reason and the job note.
    /// </summary>
    private void DrawPlateHero(QuestRecord quest)
    {
        var dl = ImGui.GetWindowDrawList();
        var room = RoomTo(bodyRight);

        // At 1.40× the body (plan v7 §1), so the name stays above the Lead-face section headings under it.
        using (Typography.QuietTitle())
        {
            TextFlow.Wrapped(model.DisplayName, room, Theme.U32(Theme.Surface.Text));
        }

        SegmentFlow(model.JournalSegments, JournalSeparator, room, Theme.Surface.TextTertiary);
        SegmentFlow(model.HeaderSegments, BlockerText.Separator, room, Theme.Surface.TextTertiary);
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
        Chrome.Rule(room);
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));

        var radius = UiMetrics.Px(PlateMedalLogical);
        var side = MathF.Round(radius * 2.05f);
        var pos = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(side, side));
        MoonWax.Draw(dl, pos + new Vector2(side * 0.5f), radius, model.State, quest.RowId, model.ReadyOnJob);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        ImGui.SameLine(0f, UiMetrics.Px(12f));
        var textLeft = ImGui.GetCursorScreenPos().X;
        var textRoom = MathF.Max(1f, bodyRight - textLeft);
        ImGui.BeginGroup();
        TextFlow.Wrapped(model.StateName, textRoom, Theme.U32(StateTextColor(model.State)));
        if (model.Callout is null && model.StatusReason.Length > 0)
        {
            TextFlow.Wrapped(model.StatusReason, textRoom, Theme.U32(Theme.Surface.TextSecondary));
        }

        if (model.Callout is null && model.StateNote is { } note)
        {
            TextFlow.Wrapped(note, textRoom, Theme.U32(Theme.Surface.TextDisabled));
        }

        ImGui.EndGroup();

        // The plate is as tall as the medal at least, so the line under it never rides up.
        var bottom = MathF.Max(ImGui.GetItemRectMax().Y, pos.Y + side);
        ImGui.SetCursorScreenPos(new Vector2(pos.X, bottom));
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
    }

    /// <summary>
    /// Plain's hero, the ledger: the name and its path, then a key-value list, State (a 12 px flat glyph, the state in
    /// its ink and the reason, with the job or other-path note under it), Level (the header line) and Giver (the name and the place), each value ending in an
    /// ellipsis that names it whole on hover.
    /// </summary>
    private void DrawLedgerHero(QuestRecord quest)
    {
        var dl = ImGui.GetWindowDrawList();
        var room = RoomTo(bodyRight);

        // The name in the Lead face (1.15×, plan v7 §1), a step over the body-size section bands under it.
        using (Typography.Lead())
        {
            TextFlow.Wrapped(model.DisplayName, room, Theme.U32(Theme.Surface.Text));
        }

        using (Typography.Caption())
        {
            SegmentFlow(model.JournalSegments, JournalSeparator, room, Theme.Surface.TextTertiary);
        }

        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
        var line = ImGui.GetTextLineHeight();
        var rowHeight = MathF.Max(line, UiMetrics.Px(20f));
        var left = ImGui.GetCursorScreenPos().X;
        var valueX = left + UiMetrics.Px(LedgerLabelLogical);
        var valueRoom = MathF.Max(1f, bodyRight - valueX);
        var gap = UiMetrics.Px(6f);
        var label = Theme.U32(Theme.Surface.TextTertiary);
        var secondary = Theme.U32(Theme.Surface.TextSecondary);

        // State: the glyph, the state in its ink, then the reason.
        var y = ImGui.GetCursorScreenPos().Y;
        var textY = y + ((rowHeight - line) * 0.5f);
        dl.AddText(new Vector2(left, textY), label, Strings.LedgerState);
        var glyphRadius = UiMetrics.Px(LedgerGlyphLogical);
        var glyphCenter = new Vector2(valueX + glyphRadius, y + (rowHeight * 0.5f));
        MoonWax.Draw(dl, glyphCenter, glyphRadius, model.State, quest.RowId, model.ReadyOnJob);
        var x = glyphCenter.X + glyphRadius + gap;
        var stateWidth = ImGui.CalcTextSize(model.StateName).X;
        var cut = Chrome.EllipsisTextAt(dl, new Vector2(x, textY), MathF.Max(1f, bodyRight - x), model.StateName, Theme.U32(StateTextColor(model.State)), stateWidth);
        x += stateWidth;
        if (model.Callout is null && model.StatusTail.Length > 0 && x < bodyRight)
        {
            cut |= Chrome.EllipsisTextAt(dl, new Vector2(x, textY), MathF.Max(1f, bodyRight - x), model.StatusTail, secondary, ImGui.CalcTextSize(model.StatusTail).X);
        }

        ImGui.Dummy(new Vector2(room, rowHeight));
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        // The job and other-path note ("Ready on WHM", the other company's path) under the state, in the value column,
        // as the plate shows it; the "Not yet" callout carries it instead when there is one.
        if (model.Callout is null && model.StateNote is { } note)
        {
            ImGui.SetCursorScreenPos(new Vector2(valueX, ImGui.GetCursorScreenPos().Y));
            TextFlow.Wrapped(note, valueRoom, Theme.U32(Theme.Surface.TextDisabled));
        }

        // Level: the header line (expansion · level · job).
        y = ImGui.GetCursorScreenPos().Y;
        textY = y + ((rowHeight - line) * 0.5f);
        dl.AddText(new Vector2(left, textY), label, Strings.LedgerLevel);
        cut = Chrome.EllipsisTextAt(dl, new Vector2(valueX, textY), valueRoom, model.HeaderLine, Theme.U32(Theme.Surface.Text), ImGui.CalcTextSize(model.HeaderLine).X);
        ImGui.Dummy(new Vector2(room, rowHeight));
        if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(model.HeaderLine);
        }

        // Giver: the name, then the place in the secondary tone.
        y = ImGui.GetCursorScreenPos().Y;
        textY = y + ((rowHeight - line) * 0.5f);
        dl.AddText(new Vector2(left, textY), label, Strings.Giver);
        var giver = model.GiverName ?? Strings.NoGiver;
        var giverWidth = ImGui.CalcTextSize(giver).X;

        // The giver's 18 px portrait plate inline before the name (1.15, spec A5), when there is a giver.
        var nameX = valueX;
        var plateMin = Vector2.Zero;
        var plate = 0f;
        var portrait = PortraitRequest.None;
        if (model.GiverName is not null && GiverPortraits.Enabled)
        {
            plate = MathF.Round(UiMetrics.Px(PortraitPlate.PlainSize));
            plateMin = new Vector2(valueX, y + MathF.Round((rowHeight - plate) * 0.5f));
            portrait = GiverPortraits.For(quest, lastSpoilers ?? Core.Query.SpoilerMask.None);
            Chrome.Portrait(dl, plateMin, plate, portrait);
            nameX += plate + UiMetrics.Px(PortraitPlate.PlainGap);
        }

        var nameRoom = MathF.Max(1f, bodyRight - nameX);
        cut = Chrome.EllipsisTextAt(dl, new Vector2(nameX, textY), nameRoom, giver, Theme.U32(model.GiverName is null ? Theme.Surface.TextDisabled : Theme.Surface.Text), giverWidth);
        if (model.PlaceLine is { } place && nameX + giverWidth + gap < bodyRight)
        {
            var placeX = nameX + giverWidth + gap;
            cut |= Chrome.EllipsisTextAt(dl, new Vector2(placeX, textY), MathF.Max(1f, bodyRight - placeX), place, secondary, ImGui.CalcTextSize(place).X);
        }

        ImGui.Dummy(new Vector2(room, rowHeight));
        if (plate > 0f && ImGui.IsItemHovered() && ImGui.IsMouseHoveringRect(plateMin, plateMin + new Vector2(plate)))
        {
            Chrome.PortraitTooltip(portrait, giver, model.PlaceLine);
        }
        else if (cut && ImGui.IsItemHovered())
        {
            UiMetrics.Tooltip(giver, model.PlaceLine);
        }

        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
    }

    /// <summary>
    /// How far the moonrise is for <paramref name="rowId"/>, eased: the moon's and the title's progress, 0 to 1. A new
    /// row starts it; it plays at Flair Full only and never under Reduce motion (<see cref="Theme.FlairMotion"/>).
    /// </summary>
    private (float Moon, float Title) Moonrise(uint rowId)
    {
        var now = ImGui.GetTime();
        if (riseRowId != rowId)
        {
            riseRowId = rowId;
            riseStart = now;
        }

        if (!Theme.FlairMotion)
        {
            return (1f, 1f);
        }

        var elapsed = now - riseStart;
        return (EaseOut(elapsed / MoonriseSeconds), EaseOut((elapsed - TitleDelaySeconds) / MoonriseSeconds));
    }

    /// <summary>Cubic ease-out of <paramref name="t"/> clamped to 0..1.</summary>
    private static float EaseOut(double t)
    {
        var x = (float)Math.Clamp(t, 0.0, 1.0);
        var inverse = 1f - x;
        return 1f - (inverse * inverse * inverse);
    }

    /// <summary>Scales the alpha of every vertex drawn since <paramref name="from"/> by <paramref name="alpha"/> (the moonrise fade).</summary>
    private static void FadeVertices(ImDrawListPtr dl, int from, float alpha)
    {
        var vertices = dl.VtxBuffer;
        var scale = Math.Clamp(alpha, 0f, 1f);
        for (var i = Math.Max(0, from); i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var a = (uint)MathF.Round((vertex.Col >> 24) * scale);
            vertex.Col = (vertex.Col & 0x00FFFFFFu) | (a << 24);
            vertices[i] = vertex;
        }
    }

    /// <summary>
    /// A rounded rectangle filled with a two-stop gradient, top to bottom or left to right: drawn in one colour, then
    /// each vertex recoloured by its position. The anti-aliased fringe keeps its own fade. Allocation-free.
    /// </summary>
    private static void GradientRect(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, ImDrawFlags corners, Vector4 from, Vector4 to, bool vertical)
    {
        if (!(max.X > min.X) || !(max.Y > min.Y))
        {
            return;
        }

        var first = dl.VtxBuffer.Size;
        dl.AddRectFilled(min, max, 0xFFFFFFFFu, rounding, corners);
        var vertices = dl.VtxBuffer;
        var span = vertical ? max.Y - min.Y : max.X - min.X;
        for (var i = first; i < vertices.Size; i++)
        {
            var vertex = vertices[i];
            var t = Math.Clamp(((vertical ? vertex.Pos.Y - min.Y : vertex.Pos.X - min.X)) / span, 0f, 1f);
            var c = Vector4.Lerp(from, to, t);
            c.W *= (vertex.Col >> 24) / 255f;
            vertex.Col = Theme.U32(c);
            vertices[i] = vertex;
        }
    }

    /// <summary>
    /// The passes over the banner: the night grade's scrim to Night, its moonlight wash and its moon road
    /// (<see cref="BannerGrading.DrawOver"/>), and a soft side scrim (Abyss 0.4 → 0 over the left half) so the title on
    /// the art reads. Under the high-contrast palette a solid band (0.97) under the title replaces them (proposal §10.2).
    /// </summary>
    private static void HeroScrims(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding)
    {
        var s = Theme.Surface;
        var height = max.Y - min.Y;
        if (Theme.Glyphs.HighContrast)
        {
            dl.AddRectFilled(new Vector2(min.X, max.Y - (height * 0.45f)), max, Theme.WithAlpha(s.Window, 0.97f), rounding, ImDrawFlags.RoundCornersBottom);
            return;
        }

        BannerGrading.DrawOver(dl, min, max, rounding);
        GradientRect(dl, min, new Vector2(min.X + ((max.X - min.X) * 0.5f), max.Y), rounding, ImDrawFlags.RoundCornersLeft, s.Deep with { W = 0.4f }, s.Deep with { W = 0f }, vertical: false);
    }

    /// <summary>
    /// The placeholder while a banner loads: a night sky (NightTop → TideDeep), a faint brass horizon, and at Flair Full a
    /// few stars placed by the quest's row id (the same sky for the same quest), on a palette with a star field.
    /// </summary>
    private static void NightSky(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding, uint seed)
    {
        var s = Theme.Surface;
        GradientRect(dl, min, max, rounding, ImDrawFlags.None, s.Top, s.CoolDeep, vertical: true);
        var width = max.X - min.X;
        var height = max.Y - min.Y;
        var horizon = MathF.Floor(min.Y + (height * 0.72f));
        var line = Theme.WithAlpha(s.Ornament, Theme.OrnamentAlpha(0.45f));
        dl.AddRectFilled(new Vector2(min.X + UiMetrics.Px(6f), horizon), new Vector2(max.X - UiMetrics.Px(6f), horizon + 1f), line);
        if (!Theme.ShowGlow || !Theme.Scene.StarField)
        {
            return;
        }

        var hash = (seed * 2654435761u) | 1u;
        for (var i = 0; i < 14; i++)
        {
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            var x = min.X + (width * ((hash & 0xFFFF) / 65535f));
            var y = min.Y + (height * 0.66f * (((hash >> 16) & 0xFFFF) / 65535f));
            var bright = (hash & 0x7) == 0;
            dl.AddCircleFilled(new Vector2(x, y), bright ? UiMetrics.Px(1.2f) : UiMetrics.Px(0.7f), Theme.WithAlpha(Theme.Surface.Text, bright ? 0.7f : 0.4f), 6);
        }
    }

    /// <summary>
    /// The meta chips (expansion, level, jobs, patch) as a flow in <paramref name="room"/>: pills on the raised surface,
    /// captions in the secondary tone, wrapping to the next line as whole chips. A chip wider than a line ends in an
    /// ellipsis and names itself on hover. One item spanning the flow; nothing allocated.
    /// </summary>
    private static void DrawChips(string[] chips, float room)
    {
        if (chips.Length == 0)
        {
            return;
        }

        var dl = ImGui.GetWindowDrawList();
        var s = Theme.Surface;
        using var caption = Typography.Caption();
        var fontSize = ImGui.GetFontSize();
        var height = MathF.Max(UiMetrics.Px(20f), fontSize + UiMetrics.Px(8f));
        var padX = UiMetrics.Px(8f);
        var gap = UiMetrics.Px(5f);
        var origin = ImGui.GetCursorScreenPos();
        var x = 0f;
        var y = 0f;
        var hoverable = ImGui.IsWindowHovered();
        foreach (var chip in chips)
        {
            var textWidth = ImGui.CalcTextSize(chip).X;
            var chipWidth = MathF.Min(textWidth + (padX * 2f), room);
            if (x > 0f && x + chipWidth > room)
            {
                x = 0f;
                y += height + gap;
            }

            var min = origin + new Vector2(x, y);
            var max = min + new Vector2(chipWidth, height);
            var rounding = height * 0.5f;
            dl.AddRectFilled(min, max, Theme.U32(s.Raised), rounding);
            dl.AddRect(min, max, Theme.U32(s.Line), rounding, ImDrawFlags.None, UiMetrics.Hairline);
            var cut = Chrome.EllipsisTextAt(dl, new Vector2(min.X + padX, min.Y + ((height - fontSize) * 0.5f)), MathF.Max(1f, chipWidth - (padX * 2f)), chip, Theme.U32(s.TextSecondary), textWidth);
            if (cut && hoverable && ImGui.IsMouseHoveringRect(min, max))
            {
                UiMetrics.Tooltip(chip);
            }

            x += chipWidth + gap;
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(room, y + height));
    }

    /// <summary>
    /// The state line under the Moon Road hero: the state in its tone, then " · the reason" in the secondary tone on the
    /// same line while it fits, else the reason on the lines under it, wrapped between words; the job note follows.
    /// </summary>
    private void DrawStateLine()
    {
        var room = RoomTo(bodyRight);
        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
        if (Theme.MoonRoadArt)
        {
            // Full: the state in the Title role (Jupiter) in its ink, the reason on the line under it.
            using (Typography.Title(model.StateName))
            {
                TextFlow.Wrapped(model.StateName, room, Theme.U32(StateTextColor(model.State)));
            }

            if (model.StatusReason.Length > 0)
            {
                TextFlow.Wrapped(model.StatusReason, RoomTo(bodyRight), Theme.U32(Theme.Surface.Text));
            }

            if (model.StateNote is { } stateNote)
            {
                TextFlow.Wrapped(stateNote, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextDisabled));
            }

            return;
        }

        TextFlow.Wrapped(model.StateName, room, Theme.U32(StateTextColor(model.State)));
        if (model.StatusTail.Length > 0)
        {
            var end = ImGui.GetItemRectMax().X;
            if (!TextFlow.LastItemWrapped() && end + ImGui.CalcTextSize(model.StatusTail).X <= bodyRight)
            {
                ImGui.SameLine(0f, 0f);
                TextFlow.Wrapped(model.StatusTail, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextSecondary));
            }
            else
            {
                TextFlow.Wrapped(model.StatusReason, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextSecondary));
            }
        }

        if (model.StateNote is { } note)
        {
            TextFlow.Wrapped(note, RoomTo(bodyRight), Theme.U32(Theme.Surface.TextDisabled));
        }
    }

    /// <summary>The moon-road divider between the hero and the sections: the full width, 12 px tall, with room above and below.</summary>
    private static void MoonRoadDivider()
    {
        var pos = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var height = UiMetrics.Px(12f);
        var margin = UiMetrics.Px(6f);
        ImGui.Dummy(new Vector2(MathF.Max(1f, width), height + (margin * 2f)));
        Ornament.Divider(ImGui.GetWindowDrawList(), new Vector2(pos.X + (width * 0.5f), pos.Y + margin + (height * 0.5f)), width, height);
    }

    // ------------------------------------------------------------------ sections

    /// <summary>
    /// Opens one of the pane's sections as a card at the Decoration level (docs/design/flair-v13 §1, "Card frame"): gilt
    /// brass with corner marks at Full, a tonal plane at Quiet, none at Plain, each under a heading in the Section role
    /// (docs/design/v7/ui/spec.md §1: tracked gilt capitals at Full, the Lead face at Quiet, a band at Plain); the
    /// caption, in the secondary tone, sits on the heading's right. Close it with <see cref="EndSection"/>. A <paramref name="captionTooltip"/> shows while the heading line is
    /// hovered (the totals behind a caption that says what is left, feature plan v6 U5). The icon is left out: the cards
    /// name themselves.
    /// </summary>
    private void BeginSection(string id, string title, string icon, string caption = "", Vector4 captionColor = default, string captionTooltip = "")
    {
        _ = icon;
        var top = ImGui.GetCursorScreenPos();
        var right = top.X + ImGui.GetContentRegionAvail().X;
        Chrome.BeginCard(id, title, null, eyebrow: true);
        CardCaption(caption, right, captionColor);
        if (captionTooltip.Length > 0 && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(top, new Vector2(right, ImGui.GetCursorScreenPos().Y)))
        {
            UiMetrics.Tooltip(captionTooltip);
        }
    }

    /// <summary>Closes the section <see cref="BeginSection"/> opened.</summary>
    private static void EndSection() => Chrome.EndCard();
}

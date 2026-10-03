using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Tsukimichi.Core.Model;
using Tsukimichi.Core.Ui;
using Tsukimichi.Core.Unique;

namespace Tsukimichi.Ui;

/// <summary>
/// The Moon Road hero and open sections (feature plan V4, proposal §7.4.1 and §7.4.2), drawn at Flair Full and Quiet.
/// <para>
/// <b>Banner.</b> Every quest has one, through the fallback chain resolved once per catalog
/// (<see cref="BannerIndexSource{TKey}"/>): its own journal banner, a related quest's, the duty it unlocks, the zone
/// where it starts, then the bundled category art. Game art is read from the player's install at runtime; when the
/// spoiler shield withholds it the category art stands in, which spoils nothing. At full width the banner keeps the
/// journal banner's shape (376 × 120, at most 128 px tall); below 320 px it is 2.6 : 1. A drawn night sky holds its
/// place while a texture loads. The frame is four corner marks and an Abyss keyline at Full, a brass hairline at Quiet.
/// A bottom scrim (Night 0 → 0.94 from 35 % down) and a side scrim (Abyss 0.55 → 0 over the left 60 %) keep anything
/// over it legible; under the high-contrast palette both give way to a solid band. The banner's tooltip names its
/// source.
/// </para>
/// <para>
/// <b>Moon and title.</b> The state moon sits on the banner's bottom edge in an Abyss cut-out, at the left (r 22, 16 at
/// D2) or centred (r 14 below 320 px); on a new selection it rises 6 px while fading in (0.35 s, the title 80 ms later),
/// at Flair Full only and never under Reduce motion. The title (Title role) and the meta chips follow beside it, or
/// under it when narrow; the state line under the hero spells the state out.
/// </para>
/// Nothing here allocates per frame: strings are composed when the selection changes, headings cased once per string
/// (<see cref="SectionHeading.Label"/>).
/// </summary>
public sealed partial class DetailPane
{
    private const float HeroBannerMaxLogical = 128f;
    private const float WideAspect = 120f / 376f;
    private const float NarrowAspect = 1f / 2.6f;
    private const double MoonriseSeconds = 0.35;
    private const double TitleDelaySeconds = 0.08;
    private const float MoonriseLogical = 6f;

    // The moonrise: the row it last started for and when.
    private uint riseRowId = uint.MaxValue;
    private double riseStart;

    // Whether the section being drawn is an open section (else a card).
    private bool sectionOpen;

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

    /// <summary>The Moon Road hero: framed banner, rising moon, title and chips (see the type's summary).</summary>
    private void DrawMoonRoadHero(QuestRecord quest)
    {
        var dl = ImGui.GetWindowDrawList();
        var min = ImGui.GetCursorScreenPos();
        var width = MathF.Max(1f, bodyRight - min.X);
        var stacked = DetailTiers.Stacks(tier);
        var height = MathF.Max(UiMetrics.Px(40f), MathF.Min(UiMetrics.Px(HeroBannerMaxLogical), width * (stacked ? NarrowAspect : WideAspect)));
        var max = min + new Vector2(width, height);
        var rounding = UiMetrics.Px(6f);

        // The banner, or the drawn sky while it loads, on the Abyss letterbox.
        dl.AddRectFilled(min, max, Theme.DeepU32, rounding);
        var choice = CurrentBanner(quest);
        var shown = BannerSource.None;
        if (BannerArtwork.TryGetWrap(textures, in choice, out var wrap, out var drawn) && wrap.Width > 0 && wrap.Height > 0)
        {
            Chrome.ImageCoverAt(dl, wrap.Handle, min, max, new Vector2(wrap.Width, wrap.Height), rounding);
            shown = drawn;
        }
        else
        {
            NightSky(dl, min, max, rounding, quest.RowId);
        }

        HeroScrims(dl, min, max, rounding);
        if (Theme.ShowCornerMarks)
        {
            dl.AddRect(min, max, Theme.DeepU32, rounding, ImDrawFlags.None, UiMetrics.Hairline);
            OrnamentAtlas.Corners(dl, min, max, UiMetrics.Px(12f), UiMetrics.Px(4f));
        }
        else
        {
            dl.AddRect(min, max, Theme.WithAlpha(Theme.Surface.Ornament, Theme.OrnamentAlpha(0.6f)), rounding, ImDrawFlags.None, UiMetrics.Hairline);
        }

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

        // The rising moon: on the bottom edge in an Abyss cut-out, left of the title or centred above it.
        var radius = MathF.Min(stacked ? UiMetrics.Icon(14f) : tier == DetailTier.Medium ? UiMetrics.Icon(16f) : UiMetrics.Icon(22f), width * 0.12f);
        var ring = UiMetrics.Px(4f);
        var (moonIn, titleIn) = Moonrise(quest.RowId);
        var centerX = stacked ? min.X + (width * 0.5f) : min.X + UiMetrics.Px(16f) + ring + radius;
        var center = new Vector2(centerX, max.Y + ((1f - moonIn) * UiMetrics.Px(MoonriseLogical)));
        var firstVertex = dl.VtxBuffer.Size;
        dl.AddCircleFilled(center, radius + ring, Theme.DeepU32);
        MoonWax.Draw(dl, center, radius, model.State, quest.RowId);
        if (moonIn < 1f)
        {
            FadeVertices(dl, firstVertex, moonIn);
        }

        var box = radius + ring;
        ImGui.SetCursorScreenPos(new Vector2(centerX - box, max.Y - box));
        ImGui.InvisibleButton("##heroMoon", new Vector2(box * 2f, box * 2f));
        Chrome.FocusRing(box);
        if (ImGui.IsItemHovered())
        {
            UiMetrics.StateTooltip(model.State, model.Evaluation, quest, BlockerNamesOf(), lastStates);
        }

        BannerTooltip(min, max, shown);

        // The title (Title role) and the chips: beside the moon, or under it at full width when narrow.
        var textLeft = stacked ? min.X : centerX + box + UiMetrics.Px(8f);
        var textTop = stacked ? max.Y + box + UiMetrics.Px(4f) : max.Y + UiMetrics.Px(6f);
        var room = MathF.Max(1f, bodyRight - textLeft);
        ImGui.SetCursorScreenPos(new Vector2(textLeft, textTop));
        ImGui.BeginGroup();
        using (Typography.Title(model.DisplayName))
        {
            TextFlow.Wrapped(model.DisplayName, room, Theme.WithAlpha(Theme.Surface.Text, titleIn));
        }

        ImGui.Dummy(new Vector2(1f, UiMetrics.Px(2f)));
        DrawChips(model.HeaderSegments, room);
        ImGui.EndGroup();

        // The hero as one block: at least down to the moon's foot.
        var bottom = MathF.Max(ImGui.GetItemRectMax().Y, max.Y + box);
        ImGui.SetCursorScreenPos(min);
        ImGui.Dummy(new Vector2(width, bottom - min.Y + UiMetrics.Px(2f)));
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
    /// The hero's scrims: the bottom one (Night 0 → 0.94 from 35 % of the height) and the side one (Abyss 0.55 → 0 over the
    /// left 60 %). Under the high-contrast palette a solid band (0.97) behind the moon replaces both (proposal §10.2).
    /// </summary>
    private static void HeroScrims(ImDrawListPtr dl, Vector2 min, Vector2 max, float rounding)
    {
        var s = Theme.Surface;
        var height = max.Y - min.Y;
        if (Theme.Glyphs.HighContrast)
        {
            dl.AddRectFilled(new Vector2(min.X, max.Y - (height * 0.3f)), max, Theme.WithAlpha(s.Window, 0.97f), rounding, ImDrawFlags.RoundCornersBottom);
            return;
        }

        GradientRect(dl, new Vector2(min.X, min.Y + (height * 0.35f)), max, rounding, ImDrawFlags.RoundCornersBottom, s.Window with { W = 0f }, s.Window with { W = 0.94f }, vertical: true);
        GradientRect(dl, min, new Vector2(min.X + ((max.X - min.X) * 0.6f), max.Y), rounding, ImDrawFlags.RoundCornersLeft, s.Deep with { W = 0.55f }, s.Deep with { W = 0f }, vertical: false);
    }

    /// <summary>
    /// The placeholder while a banner loads: a night sky (NightTop → TideDeep), a faint brass horizon, and at Flair Full a
    /// few stars placed by the quest's row id (the same sky for the same quest).
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
        if (!Theme.ShowGlow)
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
            dl.AddCircleFilled(new Vector2(x, y), bright ? UiMetrics.Px(1.2f) : UiMetrics.Px(0.7f), Theme.WithAlpha(Theme.Silver, bright ? 0.7f : 0.4f), 6);
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
    /// Opens one of the pane's sections: an open section (<see cref="OpenSection"/>: the shared heading line with its
    /// sigil, <paramref name="title"/> cased for the language, the Gilt rule and the caption) at Flair Full and Quiet,
    /// the 1.3 card with its title and caption at Plain. Close it with <see cref="EndSection"/>. A
    /// <paramref name="captionTooltip"/> shows while the heading line is hovered (the totals behind a caption that says
    /// what is left, feature plan v6 U5).
    /// </summary>
    private void BeginSection(string id, string title, string icon, string caption = "", Vector4 captionColor = default, string captionTooltip = "")
    {
        var top = ImGui.GetCursorScreenPos();
        var right = top.X + ImGui.GetContentRegionAvail().X;
        sectionOpen = Theme.ShowRules;
        if (sectionOpen)
        {
            OpenSection.Begin(id, title, caption, Theme.U32(captionColor));
        }
        else
        {
            Chrome.BeginCard(id, title, icon, eyebrow: true);
            CardCaption(caption, right, captionColor);
        }

        if (captionTooltip.Length > 0 && ImGui.IsWindowHovered() && ImGui.IsMouseHoveringRect(top, new Vector2(right, ImGui.GetCursorScreenPos().Y)))
        {
            UiMetrics.Tooltip(captionTooltip);
        }
    }

    /// <summary>Closes the section <see cref="BeginSection"/> opened.</summary>
    private void EndSection()
    {
        if (sectionOpen)
        {
            OpenSection.End();
            return;
        }

        Chrome.EndCard();
    }
}

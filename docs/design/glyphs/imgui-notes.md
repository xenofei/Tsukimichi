# Implementing glyphs v2 with ImDrawList

Notes for `Tsukimichi/Ui/MoonGlyph.cs`, `Tsukimichi.Core/Ui/MoonGeometry.cs`,
`Tsukimichi/Ui/UiMetrics.cs` and `Tsukimichi/Ui/TreePane.cs`. Geometry and colours are the
ones in `proposal.md` §3; the sheet `glyphs-v2.svg` is the reference rendering.

## 1. Shared rules

```csharp
// Rim stroke for a state moon of radius r (px).
static float Rim(float r) => Math.Clamp(0.10f * r, 1.25f, 3f);

// Visual fraction with a floor at both ends. eps is per glyph (see below).
static float Floored(float f, float eps) =>
    f <= 0f ? 0f : f >= 1f ? 1f : eps + (1f - 2f * eps) * f;
```

Pixel snapping: ImGui anti-aliases every edge, so a rim whose centre line falls between
pixels is two half-alpha pixels. Snap the glyph centre before drawing:
`c = MathF.Round(c * 2f) / 2f`, and for an even-diameter glyph prefer integer centres,
for odd-diameter half-integer ones (`center = MathF.Floor(center) + (MathF.Round(2r) % 2 == 1 ? 0.5f : 0f)`).
This alone visibly sharpens the 1.25 px rims at 16–24 px.

Segments: keep `MoonGeometry.SegmentsFor(radius)`; for an arc of sweep θ use
`Math.Max(6, (int)MathF.Ceiling(segments * θ / (2π)))` in `PathArcTo`.

Decoration gates (radius r of the disc, or R of the halo box):

| Decoration | Appears from |
|---|---|
| rim stroke | always (min 1.25 px) |
| Ready glow | r ≥ 6 |
| radial shading of the lit part (3 inset polygons) | r ≥ 9 |
| highlight arc (Completed only) | r ≥ 16 |
| 12-dash Unknown ring (else 8 dashes of 22°) | r ≥ 10 |
| Foreclosed notch | always, radius max(0.30 r, 2 px) |
| halo core moon (else flat gold / Umbra) | core radius ≥ 6 |
| halo complete glow | R ≥ 8 |
| halo core at all (else track + arc only) | R ≥ 7 |

## 2. State glyphs (`MoonGlyph.Draw`)

Order per state, all with `segments = SegmentsFor(r)`, `o = Rim(r)`, `rr = r − o/2`:

```
Completed        FillDisc(Moon shaded)                     ; r≥16: HighlightArc
Accepted         FillDisc(Veil) ; FillPhase(WaxingGibbous, Moon shaded) ; AddCircle(rr, Moon, o)
Ready            Glow ; FillDisc(Veil) ; AddCircle(rr, Dusk, o) ; FillPhase(FirstQuarter, Moon shaded)
ReadyOnOtherJob  FillDisc(Veil) ; FillPhase(FirstQuarter, Silver shaded) ; AddCircle(rr, Moon, o)
DoneThisCycle    FillDisc(Veil) ; AddCircle(rr, Dusk, o) ; FillPhase(WaningGibbous, Silver shaded)
Blocked          FillDisc(Veil) ; AddCircle(rr, Silver, o)
Foreclosed       FillDisc(Bruise) ; AddCircle(r − 0.6·o, Eclipse, 1.2·o) ; AddCircleFilled((0.72,−0.72)·r, max(0.30r, 2), Night)
Unknown          FillDisc(Veil @45%) ; DashedRing(rr, Dusk, o, r ≥ 10 ? (12, 16°) : (8, 22°))
```

Only three things change against the current code:

1. **Unlit tone** is Veil (`Theme.VeilU32`) instead of `UnlitDiscU32`; Foreclosed uses the
   new Bruise token.
2. **Rim on the plain moons**: Ready and DoneThisCycle gain a Dusk rim drawn *before* the lit
   part so it only shows around the dark side. `RingFraction` 0.07 becomes `Rim(r)`
   (0.10 r clamped to 1.25–3 px). Rings are drawn with `AddCircle(center, r − o/2, col, segments, o)`
   exactly as `Ring()` does now.
3. **Shading gate** moves from `ShadingMinRadius = 9` for everything to 9 for the inset
   polygons and 16 for the arc; below 9 the fill is flat Moon/Silver. The three inset layers
   stay (0.78 / 0.55 / 0.32 scale about the centroid) but shift their centroid by
   (−0.32 r, −0.34 r)·0.5 toward the upper left to mimic the SVG gradient's highlight.

Glow for Ready (the SVG's blurred disc, σ = 0.28 r, 55 %): three `AddCircleFilled` discs,
largest first, `(1.70r, 0.05)`, `(1.42r, 0.09)`, `(1.20r, 0.15)`. At r < 9 use
`(1.6r, 0.06)`, `(1.25r, 0.14)`. These replace the current `Glow` table.

## 3. Halo gauge (`MoonGlyph.DrawHalo`) replaces `DrawFilling` for progress

```csharp
public static void DrawHalo(ImDrawListPtr dl, Vector2 center, float R, float fraction)
{
    if (!(R > 3.5f)) return;
    var rt   = 0.80f * R;
    var wt   = MathF.Max(1.5f, 0.16f * R);
    var gap  = MathF.Max(1f, 0.10f * R);
    var rc   = rt - wt * 0.5f - gap;
    var eps  = MathF.Max(0.06f, (wt + 1.5f) / (2f * MathF.PI * rt));
    var f    = float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);
    var v    = Floored(f, eps);
    var segs = MoonGeometry.SegmentsFor(rt);

    if (v >= 1f)
    {
        if (R >= 8f)
        {
            dl.AddCircle(center, rt, Theme.WithAlpha(Theme.Moon, 0.10f), segs, wt * 2.2f);
            dl.AddCircle(center, rt, Theme.WithAlpha(Theme.Moon, 0.16f), segs, wt * 1.5f);
        }
        dl.AddCircle(center, rt, Theme.MoonU32, segs, wt);
        if (R >= 7f) FillDiscShaded(dl, center, rc, Theme.MoonU32, GoldTone);   // full core
        return;
    }

    dl.AddCircle(center, rt, Theme.VeilU32, segs, wt);                            // track
    if (v > 0f)
    {
        const float Top = -MathF.PI / 2f;
        var sweep = 2f * MathF.PI * v;
        dl.PathArcTo(center, rt, Top, Top + sweep, Math.Max(6, (int)MathF.Ceiling(segs * v)));
        dl.PathStroke(Theme.MoonU32, ImDrawFlags.None, wt);
        // Round caps: PathStroke has none.
        dl.AddCircleFilled(center + new Vector2(0f, -rt), wt * 0.5f, Theme.MoonU32);
        var (s, c) = MathF.SinCos(Top + sweep);
        dl.AddCircleFilled(center + new Vector2(c, s) * rt, wt * 0.5f, Theme.MoonU32);
    }

    if (R < 7f) return;                                                            // no room for a core
    if (f <= 0f) { dl.AddCircleFilled(center, rc, Theme.UmbraU32, segs); return; }

    // Core moon: the existing two-layer geometry, fed the floored WIDTH fraction.
    var epsCore = MathF.Max(0.10f, 1.5f / (2f * rc));
    var w = Floored(f, epsCore);
    var baseLit = MoonGeometry.FillingLayers(center, rc, w, segs, Overlay);
    FillDisc(dl, center, rc, segs, baseLit ? Theme.MoonU32 : Theme.UmbraU32);
    if (baseLit) { if (rc >= 6f) ShadeLit(dl, center, rc, Scratch, Overlay, GoldTone); FillPolygon(dl, Overlay, Theme.UmbraU32); }
    else         { FillPolygon(dl, Overlay, Theme.MoonU32); if (rc >= 6f && Overlay.Count >= 3) ShadeLit(dl, center, rc, Overlay, null, GoldTone); }
}
```

`FillingLayers` already interprets its argument as the lit **width** fraction at the equator
(`x = (1 − 2·fraction)·r`), which is exactly the floored `w`, so `MoonGeometry` needs no
change for the core. Its `Epsilon = 0.005` early-outs stay correct because `w` is never within
0.005 of 0 or 1 unless `f` is exactly 0 or 1.

`DrawFillingInline` becomes `DrawHaloInline(fraction, size)` reserving `size × size` with
`R = size * 0.5f` (the halo has no overshoot except the complete glow, which may spill).

Why the arc, not a lens, for the ring: `PathArcTo` + `PathStroke` gives an anti-aliased
stroke whose length is exactly proportional to `v` at any radius; ImGui's `AddCircle`
thickness handling is the same code path, so the track and the arc coincide to the pixel.

Cost: worst case (partial, shaded core) is 1 `AddCircle` + 1 path stroke + 2 cap discs +
1 disc + 1 convex poly + 3 shading polys ≈ 9 draw calls in one draw list, no texture switches.
The tree draws ≤ ~60 nodes: negligible.

## 4. Sizes: `UiMetrics` and `TreePane`

Let `L = ImGui.GetTextLineHeight()` (≈ 18.4 px at Dalamud 16 px font × UiScale 1.15) and
`k = ScaleMetrics.ClampIconScale(settings.IconScale)` (1.25 default).

```csharp
// Halo half-size for a tree row: half the line, scaled by the icon factor, kept sane.
public static float TreeGlyphRadius(float lineHeight) =>
    Math.Clamp(0.5f * lineHeight * FontIconRatio, Icon(6.5f), Icon(14f));   // FontIconRatio = IconScale / Scale = user icon scale
public static float TreeRowHeight(float lineHeight) =>
    MathF.Max(lineHeight, 2f * TreeGlyphRadius(lineHeight) + Px(6f));
```

| UiScale | IconScale | L (px) | glyph R | halo box | row height |
|---|---|---|---|---|---|
| 1.00 | 1.00 | 16.0 | 8.0 | 16 | 22 |
| 1.15 (default) | 1.00 | 18.4 | 9.2 | 18 | 24 |
| 1.15 (default) | 1.25 (default) | 18.4 | 11.5 | 23 | 30 |
| 1.15 | 1.60 | 18.4 | 14.7 | 29 | 36 |
| 1.40 | 1.25 | 22.4 | 14.0 | 28 | 36 |

Today's tree moon is `Icon(5f)` = 7.2 px radius (14.4 px) in a 22 px row. The default
above gives a 23 px glyph in a 30 px row: every node ≥ 18 px as requested, and the row pitch
grows 35 %, which for a 40-row tree is 320 px more scroll, acceptable for a navigation pane.
If that feels tall, the sheet's row 4 (20 px glyph, 26 px row) is the floor.

To grow the row: add `ImGuiTreeNodeFlags.FramePadding` and push
`ImGuiStyleVar.FramePadding = (style.FramePadding.X, (rowHeight − L) / 2)` around
`TreeNodeEx`; `DrawNodeOverlay` already centres on `GetItemRectMin/Max`, so the glyph and
text land in the middle without further change. `labelX` stays
`min.X + fontSize + FramePadding.X·2`; place the halo at `labelX + R` and the name at
`labelX + 2R + pad`.

Other `DrawFilling` callers and the size they should pass to `DrawHalo`:

| Caller | Today | Proposed |
|---|---|---|
| `TreePane` rows | r = Icon(5) | R = `TreeGlyphRadius(L)` |
| `CharactersPane`, `MoonlitPane` inline rows | box = `InlineGlyphSize(L)` | same box, R = box/2 (≥ 9 px) |
| `FlightPane` zone rows | radius | R = same value + 2 px |
| `DetailPane` chain moon | size | R = size/2, min 16 px box |
| `MainWindow` status bar | r = Icon(4.5) = 6.5 px | R = 6.5: track + arc only (core gated at R ≥ 7) |
| `HelpWindow`, `GlyphDebugWindow` | any | add halo rows at 16 / 20 / 24 / 32 / 64 px next to the existing moons |

State glyph sizes: `RowGlyphRadius` `Icon(6f)` → `Icon(7.5f)` (10.8 px at defaults);
`InlineRadiusFraction` 0.42 → 0.44; `RowContentHeight` keeps `RowGlyphRadius * 2.4f`.

## 5. Order of work

1. `Theme.cs`: Veil as the unlit tone, add Umbra, Bruise, MoonHigh/Deep, SilverHigh/Deep.
2. `MoonGlyph.Draw`: rim rule, Dusk rims on Ready/DoneThisCycle, new glow table, shading gates.
3. `MoonGlyph.DrawHalo` / `DrawHaloInline`; keep `DrawFilling` for one release behind the
   glyph debug window so both can be compared in game, then delete it.
4. `UiMetrics.TreeGlyphRadius/TreeRowHeight`; `TreePane` frame padding + halo.
5. Swap the remaining `DrawFilling` callers per the table.
6. Journal upgrades from `proposal.md` §4 (section rule, badges, mini bar, hover/selected).
7. Regenerate `assets/icons/moon-phases-preview.png` from the new rules so the README sheet
   matches the plugin (the render script's `glyph()` needs the same three changes as §2).

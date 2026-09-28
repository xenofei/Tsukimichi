# Implementing glyphs v2.1 with ImDrawList

Notes for `Tsukimichi/Ui/MoonGlyph.cs`, `Tsukimichi.Core/Ui/MoonGeometry.cs`,
`Tsukimichi/Ui/UiMetrics.cs` and `Tsukimichi/Ui/TreePane.cs`. Geometry and colours are the
ones in `proposal.md` §3; the sheet `glyphs-v2.svg` is the reference rendering.

## 1. Shared rules

```csharp
// Rim stroke for a state moon of radius r (px). ui-revamp §2.7 rule.
static float Rim(float r) => Math.Clamp(0.12f * r, 1.5f, 3f);

// Visual fraction with a floor at both ends. eps is per glyph (see below).
static float Floored(float f, float eps) =>
    f <= 0f ? 0f : f >= 1f ? 1f : eps + (1f - 2f * eps) * f;
```

Pixel snapping: ImGui anti-aliases every edge, so a rim whose centre line falls between
pixels is two half-alpha pixels. Snap the glyph centre before drawing:
`c = MathF.Round(c * 2f) / 2f`, and for an even-diameter glyph prefer integer centres,
for odd-diameter half-integer ones (`center = MathF.Floor(center) + (MathF.Round(2r) % 2 == 1 ? 0.5f : 0f)`).
This alone visibly sharpens the 1.5 px rims at 16–24 px.

Segments: keep `MoonGeometry.SegmentsFor(radius)`; for an arc of sweep θ use
`Math.Max(6, (int)MathF.Ceiling(segments * θ / (2π)))` in `PathArcTo`.

Decoration gates (radius r of the disc, rc of the halo core, R of the halo box):

| Decoration | Appears from |
|---|---|
| rim stroke | always (min 1.5 px) |
| Ready: 1 px Moon ring at 1.25 r @ 35 % | r < 9 |
| Ready: 3-disc glow | r ≥ 9 |
| radial shading of the lit part (3 inset polygons) | r ≥ 9 |
| interior detail (§2b) | r ≥ 12 |
| highlight arc (Completed only) | r ≥ 16 |
| 12-dash Unknown ring (else 8 dashes of 22°) | r ≥ 10 |
| Accepted seal | always, radius max(0.16 r, 1.5 px) |
| Foreclosed bar | always, width max(2 px, 0.22 r) |
| Foreclosed notch | r ≥ 12, radius 0.30 r |
| halo core (Shadow disc + Dusk rim, moon inside) | R ≥ 12 (24 px box); below: track + arc + number |
| halo core shading / detail | rc ≥ 9 / rc ≥ 12 |
| halo complete glow | R ≥ 8 |

## 2. State glyphs (`MoonGlyph.Draw`)

Order per state, all with `segments = SegmentsFor(r)`, `o = Rim(r)`, `rr = r − o/2`.
`Shadow` is the unlit tone (`Theme.ShadowU32`, `#3A4363`); `Lit(...)` is "fill + shade + detail"
from §2b.

```
Completed        Lit(Disc, Moon)                                                  ; r≥16: HighlightArc
Accepted         FillDisc(Shadow) ; Lit(Gibbous60, Moon) ; Seal ; AddCircle(rr, Silver, o)
Ready            r<9 ? AddCircle(1.25r, Moon@0.35, 1px) : Glow ; FillDisc(Shadow) ; AddCircle(rr, Dusk, o) ; Lit(RightHalf, Moon)
ReadyOnOtherJob  FillDisc(Shadow) ; Lit(RightHalf, Silver) ; AddCircle(rr, Moon, o)
DoneThisCycle    FillDisc(Shadow) ; AddCircle(rr, Dusk, o) ; Lit(WaningGibbous75, Silver)
Blocked          FillDisc(Shadow) ; AddCircle(rr, Silver, o)
Foreclosed       FillDisc(Shadow) ; AddCircle(rr, Eclipse, o) ; AddLine((−.636,−.636)·r, (.636,.636)·r, Eclipse, max(2, .22r)) ; r≥12: AddCircleFilled((0.72,−0.72)·r, 0.30r, Night)
Unknown          FillDisc(Shadow @60%) ; DashedRing(rr, Dusk, o, r ≥ 10 ? (12, 16°) : (8, 22°))
```

Lit shapes, in `MoonGeometry` terms (`FillingLayers` takes the lit **width** fraction at
the equator, `x = (1 − 2w)·r`, terminator through the poles):

- `Gibbous60` = `FillingLayers(w = 0.60)` — equator crossing at x = −0.20 r, terminator circle
  centre (2.4 r, 0), radius 2.6 r. This is the new Accepted (spec §2.1: waxing gibbous, early).
- `RightHalf` = `FillingLayers(w = 0.50)`.
- `WaningGibbous75` = the existing offset-disc lens (disc ∩ disc offset −0.5 r); unchanged.
- `Seal` = `AddCircleFilled(center + (0.40 r, 0), MathF.Max(0.16f * r, 1.5f), Theme.NightU32)`,
  drawn after the lit part and before the rim.

Only these things change against the current code:

1. **Unlit tone** is Shadow (`Theme.ShadowU32`) instead of `UnlitDiscU32`; Bruise is not added.
2. **Rim on the plain moons**: Ready and DoneThisCycle gain a Dusk rim drawn *before* the lit
   part so it only shows around the dark side. `RingFraction` 0.07 becomes `Rim(r)`
   (0.12 r clamped to 1.5–3 px). Rings are drawn with `AddCircle(center, r − o/2, col, segments, o)`
   exactly as `Ring()` does now.
3. **Accepted**: `WaxingGibbous` (offset lens) → `FillingLayers(0.60)`, ring Moon → Silver, plus the seal.
4. **Foreclosed**: the ring is `o` (not 1.2 o), the bar is one `AddLine`, the notch is gated at r ≥ 12.
5. **Ready below r = 9**: one `AddCircle(center, 1.25f * r, Theme.WithAlpha(Theme.Moon, 0.35f), segs, 1f)`
   replaces the glow discs.
6. **Shading gate** moves from `ShadingMinRadius = 9` for everything to 9 for the inset
   polygons, 12 for the detail and 16 for the arc; below 9 the fill is flat Moon/Silver. The
   three inset layers stay (0.78 / 0.55 / 0.32 scale about the centroid) but shift their
   centroid by (−0.32 r, −0.34 r)·0.5 toward the upper left to mimic the SVG gradient's highlight.

Glow for Ready at r ≥ 9 (the SVG's blurred disc, σ = 0.28 r, 55 %): three `AddCircleFilled`
discs, largest first, `(1.70r, 0.05)`, `(1.42r, 0.09)`, `(1.20r, 0.15)`. These replace the
current `Glow` table.

### 2b. Interior detail (`MoonGlyph.Detail`, r ≥ 12 only, ≤ 12 primitives)

ImGui has no clipping to an arbitrary lens, so the detail is drawn as small filled shapes
that are **tested against the lit region before being emitted**: a mare or crater whose
centre is on the dark side of the terminator is skipped, one that straddles it is drawn at
half alpha (the lit-side gradient hides the seam at these alphas). The terminator test is
the same equation as `FillingLayers`: for lit width w the point (x, y) (fractions of r) is
lit when it lies on the lit side of the terminator circle through the poles:
`lit = (x − h)² + y² ≤ Rt²` with `h = (xe² − 1)/(2 xe)`, `Rt = √(h² + 1)`, `xe = 1 − 2w`
(for xe < 0 the lit side is *inside* that circle, for xe > 0 *outside*; xe = 0 is `x ≥ 0`).
For DoneThisCycle mirror x first.

Budget per glyph, all in one draw list, no texture switches (Umbra = `#2C334A`):

| # | Primitive | Geometry (unit disc, y down) | Colour / alpha (gold ; silver) |
|---|---|---|---|
| 1 | `AddEllipseFilled` M1 | centre (−0.30, −0.24), radii 0.32 × 0.24, rot −25° | Umbra 0.20 ; 0.14 |
| 2 | `AddEllipseFilled` M2 | (0.30, 0.06), 0.24 × 0.20, rot −15° | Umbra 0.18 ; 0.13 |
| 3 | `AddEllipseFilled` M3 | (−0.10, 0.40), 0.30 × 0.14, rot +10° | Umbra 0.18 ; 0.13 |
| 4 | `AddCircleFilled` C1 floor | (0.34, −0.46), r 0.120 | Umbra 0.14 |
| 5 | `PathArcTo` + `PathStroke` C1 shadow | radius 0.84·0.120, 170°→290° (cw from 3 o'clock), 1 px | Umbra 0.30 |
| 6 | `PathArcTo` + `PathStroke` C1 highlight | radius 0.96·0.120, −10°→110°, 1 px | white 0.38 ; 0.30 |
| 7 | `AddCircleFilled` C2 floor | (−0.50, 0.30), r 0.095 | Umbra 0.14 |
| 8 | C2 highlight arc | radius 0.96·0.095, −10°→110°, 1 px | white 0.38 ; 0.30 |
| 9 | `AddCircleFilled` C3 floor | (0.10, 0.60), r 0.075 | Umbra 0.14 |
| 10 | C3 highlight arc | radius 0.96·0.075, −10°→110°, 1 px | white 0.38 ; 0.30 |
| 11 | terminator glow: `AddConvexPolyFilled` | the lit polygon minus the lit polygon for `w − 0.06` (a band 0.12 r wide on the lit side; for the offset lens use the lens with its offset disc at 0.88 r); build it as the strip between the two terminator arcs from pole to pole | MoonHigh 0.30 ; white 0.25 |
| 12 | rim vignette: `AddCircle` | radius 0.93 r, thickness 0.14 r, only the segments on the lit side (`PathArcTo` over the lit rim from pole to pole) | Umbra 0.14 ; 0.10 |

Notes:
- `ImDrawList.AddEllipseFilled(center, radius(Vector2), col, rot, segments)` exists from
  ImGui 1.90 (Dalamud API 10+). If the bound ImGui lacks it, emit the ellipse as a 24-point
  `AddConvexPolyFilled`.
- Soft edges: the sheet blurs the maria and the band. In ImGui draw the mare twice instead —
  the full ellipse at 0.6× alpha and a 0.75-scale copy at 0.4× alpha — only if the budget
  allows (that makes 15 primitives); the single hard-edged ellipse at these alphas already
  reads as a sea at 32–64 px, so the default is the 12-primitive table.
- The vignette is one stroked arc on the lit side; the SVG's quadratic falloff is
  approximated by its 0.14 r width blending under anti-aliasing. If it looks like a band at
  64 px, split it into two arcs (0.90 r @ 0.08 width 0.12 r, 0.97 r @ 0.10 width 0.06 r) —
  the 13th primitive is acceptable only at r ≥ 24.
- Draw order: lit fill (+ inset shading) → 1–3 → 4–10 → 11 → 12 → seal / arc / rim.
- Nothing in this table is drawn on Shadow, on Foreclosed, on Unknown or on Blocked; the
  state channels (seal, bar, notch, dashes, half-line) are always on top.
- Cost at the default tree size (24 px halo, rc = 7.3) is zero: the core is below 12 px.
  Only the 64 px help-window glyphs and the 32 px tooltip halo pay the 12 primitives.

## 3. Halo gauge (`MoonGlyph.DrawHalo`) replaces `DrawFilling` for progress

```csharp
public static void DrawHalo(ImDrawListPtr dl, Vector2 center, float R, float fraction)
{
    if (!(R >= 8f)) return;                       // below 16 px: caller draws the percentage only
    var rt   = 0.80f * R;
    var wt   = MathF.Max(2f, 0.18f * R);
    var gap  = MathF.Max(1f, 0.10f * R);
    var rc   = rt - wt * 0.5f - gap;              // 0.61 R from R = 12 up
    var eps  = MathF.Max(0.06f, (wt + 1.5f) / (2f * MathF.PI * rt));
    var f    = float.IsNaN(fraction) ? 0f : Math.Clamp(fraction, 0f, 1f);
    var v    = Floored(f, eps);
    var segs = MoonGeometry.SegmentsFor(rt);
    var hasCore = R >= 12f;

    if (v >= 1f)
    {
        dl.AddCircle(center, rt, Theme.WithAlpha(Theme.Moon, 0.10f), segs, wt * 2.2f);
        dl.AddCircle(center, rt, Theme.WithAlpha(Theme.Moon, 0.16f), segs, wt * 1.5f);
        dl.AddCircle(center, rt, Theme.MoonU32, segs, wt);
        if (hasCore) Lit(dl, center, rc, Disc, GoldTone);          // full core, shaded ≥ 9, detailed ≥ 12
        return;
    }

    dl.AddCircle(center, rt, Theme.VeilLineU32, segs, wt);                        // track, 3.19 : 1 on Night
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

    if (!hasCore) return;                                                          // 16-18 px: track + arc, number beside it
    var oc = Rim(rc);
    dl.AddCircleFilled(center, rc, Theme.ShadowU32, segs);                         // core disc
    dl.AddCircle(center, rc - oc * 0.5f, Theme.DuskU32, segs, oc);                 // core rim
    if (f <= 0f) return;

    // Core moon: the existing two-layer geometry, fed the floored WIDTH fraction.
    var epsCore = MathF.Max(0.10f, 1.5f / (2f * rc));
    var w = Floored(f, epsCore);
    var baseLit = MoonGeometry.FillingLayers(center, rc, w, segs, Overlay);
    if (baseLit) { FillDisc(dl, center, rc, segs, Theme.MoonU32); if (rc >= 9f) ShadeLit(...); FillPolygon(dl, Overlay, Theme.ShadowU32); dl.AddCircle(center, rc - oc * 0.5f, Theme.DuskU32, segs, oc); }
    else         { FillPolygon(dl, Overlay, Theme.MoonU32); if (rc >= 9f && Overlay.Count >= 3) ShadeLit(dl, center, rc, Overlay, null, GoldTone); }
    if (rc >= 12f) Detail(dl, center, rc, w, GoldTone);
}
```

`FillingLayers` already interprets its argument as the lit **width** fraction at the equator
(`x = (1 − 2·fraction)·r`), which is exactly the floored `w`, so `MoonGeometry` needs no
change for the core. Its `Epsilon = 0.005` early-outs stay correct because `w` is never within
0.005 of 0 or 1 unless `f` is exactly 0 or 1. When the base disc is lit (w > 0.5) the Dusk
rim is redrawn after the dark overlay so it shows around the dark side only, as on the state moons.

`DrawFillingInline` becomes `DrawHaloInline(fraction, size)` reserving `size × size` with
`R = size * 0.5f` (the halo has no overshoot except the complete glow, which may spill). When
`R < 12` the caller also writes `$"{fraction:P0}"` in Dusk after the glyph; when `R < 8` it
writes only the text.

Why the arc, not a lens, for the ring: `PathArcTo` + `PathStroke` gives an anti-aliased
stroke whose length is exactly proportional to `v` at any radius; ImGui's `AddCircle`
thickness handling is the same code path, so the track and the arc coincide to the pixel.

Cost: worst case (partial, shaded core) is 1 `AddCircle` + 1 path stroke + 2 cap discs +
1 disc + 1 rim + 1 convex poly + 3 shading polys ≈ 10 draw calls in one draw list, no texture
switches; +12 for the detail at rc ≥ 12 (64 px only). The tree draws ≤ ~60 nodes: negligible.

## 4. Sizes: `UiMetrics` and `TreePane`

Let `L = ImGui.GetTextLineHeight()` (≈ 18.4 px at Dalamud 16 px font × UiScale 1.15) and
`k = ScaleMetrics.ClampIconScale(settings.IconScale)` (1.25 default).

```csharp
// Halo half-size for a tree row: half the line, scaled by the icon factor, floored at 24 px boxes.
public static float TreeGlyphRadius(float lineHeight) =>
    Math.Clamp(0.5f * lineHeight * FontIconRatio, 12f, Icon(14f));   // 12 px absolute floor (panel A4)
public static float TreeRowHeight(float lineHeight) =>
    MathF.Max(lineHeight, 2f * TreeGlyphRadius(lineHeight) + Px(6f));
```

| UiScale | IconScale | L (px) | glyph R | halo box | row height |
|---|---|---|---|---|---|
| 1.00 | 0.80 | 16.0 | 12.0 (floor) | 24 | 30 |
| 1.00 | 1.00 | 16.0 | 12.0 (floor) | 24 | 30 |
| 1.15 (default) | 1.00 | 18.4 | 12.0 (floor) | 24 | 30 |
| 1.15 (default) | 1.25 (default) | 18.4 | 12.0 | 24 | 30 |
| 1.15 | 1.60 | 18.4 | 14.7 | 29 | 36 |
| 1.40 | 1.25 | 22.4 | 14.0 | 28 | 36 |

Today's tree moon is `Icon(5f)` = 7.2 px radius (14.4 px) in a 22 px row. The default
above gives a 24 px glyph in a 30 px row: every node ≥ 24 px as the panel asks, and the row
pitch grows 35 %, which for a 40-row tree is 320 px more scroll, acceptable for a navigation
pane. The sheet's row 4 shows exactly this.

To grow the row: add `ImGuiTreeNodeFlags.FramePadding` and push
`ImGuiStyleVar.FramePadding = (style.FramePadding.X, (rowHeight − L) / 2)` around
`TreeNodeEx`; `DrawNodeOverlay` already centres on `GetItemRectMin/Max`, so the glyph and
text land in the middle without further change. `labelX` stays
`min.X + fontSize + FramePadding.X·2`; place the halo at `labelX + R` and the name at
`labelX + 2R + pad`.

Other `DrawFilling` callers and the size they should pass to `DrawHalo`:

| Caller | Today | Proposed |
|---|---|---|
| `TreePane` rows | r = Icon(5) | R = `TreeGlyphRadius(L)` (≥ 12) |
| `CharactersPane`, `MoonlitPane` inline rows | box = `InlineGlyphSize(L)` | same box, R = box/2; if R < 12 write the percentage after it |
| `FlightPane` zone rows | radius | R = same value + 2 px, min 12 |
| `DetailPane` chain moon | size | R = size/2, min 24 px box |
| `MainWindow` status bar | r = Icon(4.5) = 6.5 px | R = 8: track + arc only + "62 %" text; below 8 text only |
| `HelpWindow`, `GlyphDebugWindow` | any | add halo rows at 16 / 24 / 32 / 64 px next to the existing moons, plus a greyscale toggle (feColorMatrix-style luminance on the draw-list colours) |

State glyph sizes: `RowGlyphRadius` `Icon(6f)` → `Icon(7.5f)` (10.8 px at defaults);
`InlineRadiusFraction` 0.42 → 0.44; `RowContentHeight` keeps `RowGlyphRadius * 2.4f`.

## 5. Order of work

1. `Theme.cs`: Shadow as the unlit tone, add VeilLine, MoonHigh/Deep, SilverHigh/Deep; keep
   Umbra for the detail tint; do not add Bruise.
2. `MoonGlyph.Draw`: rim rule, Dusk rims on Ready/DoneThisCycle, Accepted = `FillingLayers(0.60)`
   + Silver rim + seal, Foreclosed bar, Ready small ring, shading gates.
3. `MoonGlyph.Detail` (§2b) behind the r ≥ 12 gate; verify in `GlyphDebugWindow` at 32 and 64 px.
4. `MoonGlyph.DrawHalo` / `DrawHaloInline`; keep `DrawFilling` for one release behind the
   glyph debug window so both can be compared in game, then delete it.
5. `UiMetrics.TreeGlyphRadius/TreeRowHeight` (24 px floor); `TreePane` frame padding + halo.
6. Swap the remaining `DrawFilling` callers per the table; add the percentage text where R < 12.
7. Journal upgrades from `proposal.md` §4 (section rule, badges, mini bar, hover/selected).
8. Regenerate `assets/icons/moon-phases-preview.png` from the new rules so the README sheet
   matches the plugin (the render script's `glyph()` needs the same changes as §2).

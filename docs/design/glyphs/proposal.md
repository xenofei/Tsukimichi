# Tsukimichi glyphs v2.1 — status moons and the Journal progress glyph

Proposal only. Companion files: `glyphs-v2.svg` (design sheet), `glyphs-v2.png` and
`glyphs-v2-zoom.png` (rasterized by `render_preview.py`), `imgui-notes.md` (how to build it
with ImDrawList). Nothing under `assets/` or in plugin code is touched.

v2.1 folds in two owner notes ("Accepted looks like a full moon", "the moons need interior
detail") and the accessibility panel's findings A1, A2, A4 and B1
(`docs/review/panel/accessibility.md`). What changed against v2.0 is listed in §3.0.

## 1. Why the current glyphs fail between 12 and 20 px

Numbers below come from the live code: `UiMetrics.TreeMoonRadius = Icon(5f)`, so at Dalamud
global scale 1, UiScale 1.15 and IconScale 1.25 the tree moon has radius 7.2 px, i.e. a
**14.4 px** disc. Its Veil ring is `max(1, 0.07·r)` = 1 px. The unlit disc is `UnlitDisc`
= Night→Veil at 50 % = `#2C334A`.

### 1.1 The unlit disc is the background

Relative-luminance contrast (WCAG formula) of the tokens against the two backgrounds the
tree actually sits on:

| Colour | vs Night `#0F1424` | vs Dalamud default window bg (~`#141414`) |
|---|---|---|
| UnlitDisc `#2C334A` (current) | **1.47 : 1** | 1.53 : 1 |
| Shadow `#3A4363` (v2.1 unlit disc) | 1.89 : 1 | 1.97 : 1 |
| Veil `#4A5270` | 2.38 : 1 | 2.47 : 1 |
| VeilLine `#5C6584` | 3.19 : 1 | 3.3 : 1 |
| Dusk `#7C86A8` | 5.1 : 1 | 5.3 : 1 |
| Moon `#F2D27A` | 12.5 : 1 | 13 : 1 |
| Silver `#DDE3F0` | 14.2 : 1 | 14.8 : 1 |

Anything under ~2 : 1 is invisible as a *shape*; 1.47 : 1 is a colour you can only see when
it is large and edged. So a 14 px moon has, in practice, only two visible states: "some gold"
and "no gold". A node at 129/195 (66 %) is a gold disc with a dark bite whose tone equals the
background; the bite is 4.9 px wide at the equator and tapers to 0 at the poles, so the eye
reads a slightly dented full moon. That is the complaint exactly. The cure is not a lighter
disc alone (even Veil stays under 3 : 1) but a **rim that always closes the silhouette**
(§3.1) — with that rim in place the disc tone can stay dark, which is why v2.1 settles on
Shadow rather than Veil (§3.4).

### 1.2 The terminator has no floor

`MoonGeometry.FillingLayers` already maps the fraction linearly to the lit **width** at the
equator (`x = (1 − 2f)·r`), which is the right idea, but there is no minimum:

| fraction | lit width at r = 7.2 px | what ImGui paints |
|---|---|---|
| 17/612 = 0.028 | 0.40 px | one anti-aliased column at ~35 % alpha: nothing |
| 0.10 | 1.4 px | a hairline that tapers to nothing at the poles |
| 0.90 | dark sliver 1.4 px | same, in a tone you cannot see anyway |
| 129/195 = 0.66 | dark 4.9 px | reads as a full moon with a soft edge |

Lit **area** is worse than width: a crescent of equator width δ has area ≈ (4/3)·δ·r, i.e.
2.4 % of the disc at f = 0.03, and it is all spread along a taper. Near 1 the same happens to
the dark side. The perceptual curve is flat for the first ~8 % and the last ~8 % of every node.

### 1.3 No closed silhouette

The 1 px Veil ring at 2.4 : 1 is anti-aliased into ~50 % alpha on a curve that never sits on
pixel centres, so effectively 1.6 : 1. Without an edge, the lit lens and the dark disc are two
separately anti-aliased convex polygons that meet at the rim (`AddConvexPolyFilled` twice
on the same chord), which shows as a faint seam and a soft, undersized moon.

### 1.4 Shading noise

From 9 px radius the lit region gets three inset polygons and a highlight arc. Between 9 and
12 px those are 1–2 px features: the arc becomes one stray bright pixel and the insets smear
the terminator. Decoration must be gated on size more aggressively than fill.

### 1.5 It is simply too small

14 px is Dalamud's text x-height. Nothing that must encode a continuous quantity should be
drawn at 14 px in a 20 px row; the row has room for 24 px.

## 2. Candidates

All three keep the eight **state** moons as moons (they encode a category, and moons are the
brand). They differ in the **progress** glyph used by the Journal tree, the Characters and
Moonlit panes, the Flight pane, the detail-pane chain moon and the status bar.

### A. Refined phase moon (evolution of today)

- Disc radius r; unlit tone Shadow `#3A4363` behind a rim that always closes the silhouette.
- Crisp rim: `o = clamp(0.12·r, 1.5 px, 3 px)` drawn *inside* the rim (circle at `r − o/2`),
  Dusk for the plain moons, state colour where the spec already asks for a ring.
- Terminator maps fraction to visual width with a floor:
  `w = ε + (1 − 2ε)·f`, `ε = max(0.10, 1.5 px / (2r))`, so 17/612 is a 1.5 px crescent at any
  size and 97 % keeps a 1.5 px dark sliver. 0 and 1 stay exact.
- Radial shading on the lit part from r ≥ 9 px, interior detail from r ≥ 12 (§3.6),
  highlight arc from r ≥ 16 px.
- Optional 8 px numeric badge ("3 %") at the lower right for r ≥ 12.

Pros: smallest change; the tree keeps literal moons; `FillingLayers` needs a two-line
change. Cons: even floored, a crescent is a *taper*, so 3 % and 10 % look alike until ~20 px;
the plain moon is the same silhouette as the Ready / Accepted state glyphs, so a 50 % node and
a Ready quest are told apart only by the ring colour; the badge fights the count text that
is already on the row.

### B. Halo gauge (ring arc around a moon core) — **recommended**

A **track ring** in VeilLine, a **gold arc** growing clockwise from 12 o'clock over it, and
(from a 24 px box) a **core disc** behind a gap that is itself a small filling moon. Named
after the lunar halo (月暈): the ring reads as the moon's corona.

- Box S, R = S/2. Track radius `0.80 R`, stroke `max(2 px, 0.18 R)`. Gap `max(1 px, 0.10 R)`.
  Core radius `rc = 0.80R − stroke/2 − gap` (= 0.61 R from 24 px up).
- Visual fraction `v = ε + (1 − 2ε)·f` for 0 < f < 1, `ε = max(0.06, (stroke + 1.5 px) / circumference)`;
  v = 0 and v = 1 exactly at the ends. Round caps (two filled circles of radius stroke/2).
  The floor guarantees a visible pip beyond the cap for 1/612 and a visible dark gap at 611/612.
- Core (R ≥ 12 only): Shadow disc with a Dusk rim, like every state moon; 0 < f < 1 → filling
  moon in the core with its own floor `ε_c = max(0.10, 1.5 px / (2 rc))` (flat gold below
  rc = 9 px, shaded above, detailed from 12); f = 1 → full gold core + full gold ring + two soft
  gold glow rings. Below R = 12 (tab strip, status bar) the halo is **track + arc only** and
  the number is written beside it.
- Arc length is linear in f at every size: at 24 px the circumference is 60 px, so one quest
  of 195 is 0.3 px of arc plus the 2 px cap, 3 % is a 4 px pip, 25 % a quarter turn.

Pros: linear and legible from 16 px up (the sheet shows 0 / .03 / .10 / .25 / .50 / .75 /
.90 / .97 / 1 all distinct at 16 px as track + arc); a completely different silhouette from
every state glyph (hollow ring + small core vs solid disc), so nothing is confused with Ready;
still a moon inside, and at 24 px+ the core phase doubles the arc, which is the "beautiful"
part; complete nodes get a real reward look (gold ring + core + glow). Cons: three primitives
instead of two per glyph (trivial for ImDrawList; the tree has < 60 rows); a new drawing
helper alongside `DrawFilling`; slightly wider than tall in feel because of the cap at 12
o'clock (mitigated by starting the arc at exactly −90°).

### C. Segmented lunar-calendar pips

Eight or twelve small dots on a ring, filling like a lunar calendar. At 64 px it is
charming; at 16 px a 12-pip ring means 1.3 px pips with 2 px gaps, below the AA floor, and
a quantised readout hides the difference between 129/195 and 150/195. Rejected for the tree;
could be reused later for chains with ≤ 8 steps in the detail pane.

## 3. Recommendation

**B for progress, A's refinements for the eight state glyphs.** The two systems differ in
silhouette (solid disc vs ring-with-core), so a user never reads a Ready quest as a 50 % node
or vice versa; they share tokens, shading, glow layers and the floor formula, so they look
like one family.

### 3.0 What changed from v2.0 to v2.1

| Item | v2.0 | v2.1 | Why |
|---|---|---|---|
| Accepted | 75 % offset lens, Moon ring | 60 % gibbous, Silver rim, Night seal dot | owner: read as a full moon at 16–24 px; panel A2: must not be the mirror of DoneThisCycle |
| Unlit disc | Veil `#4A5270` | Shadow `#3A4363` | one answer with ui-revamp §2.7; Veil is also the disabled-text colour (B1) |
| Rim | `clamp(0.10 r, 1.25, 3)` | `clamp(0.12 r, 1.5, 3)` | ui-revamp rule; 1.5 px rims survive anti-aliasing (§1.3) |
| Foreclosed | Bruise disc, 1.2 o Eclipse ring, notch | Shadow disc, Eclipse rim, Eclipse diagonal bar, notch from r ≥ 12 | panel A1: colour-only under red-green CVD; Bruise dropped |
| Ready below r = 9 | 3-disc glow | one 1 px Moon ring at 1.25 r @ 35 % | panel A2: the glow vanishes at 11 px |
| Unknown disc | Veil @ 45 % | Shadow @ 60 % | follows the disc token |
| Halo track / stroke | Veil, `max(1.5, 0.16 R)` | VeilLine `#5C6584`, `max(2, 0.18 R)` | panel A4: 1.54 : 1 → 3.19 : 1; low-vision acuity |
| Halo core | Umbra, from R ≥ 7 | Shadow + Dusk rim, from R ≥ 12; below: track + arc + number | panel A4 |
| Tree glyph | 20 px floor | 24 px floor, 30 px rows | panel A4 |
| Lit part | gradient | gradient + interior detail from r ≥ 12 | owner note 2, §3.6 |

### 3.1 State glyphs, exact geometry (r = disc radius)

Common: rim stroke `o = clamp(0.12·r, 1.5, 3)` px, ring circle at `r − o/2`; unlit tone
Shadow `#3A4363`; lit tones shaded radially (highlight centre at (−0.32 r, −0.34 r), radius
1.25 r; gold stops `#FFF0BE` → `#F2D27A` at 42 % → `#D6B25A`; silver `#FFFFFF` → `#DDE3F0` →
`#B9C2D8`) from r ≥ 9 px, flat below; interior detail (§3.6) from r ≥ 12 px. Draw order is
always disc → rim → lit part → detail → marks, except the ring states, where the ring goes last.

| State | Disc | Lit part | Rim / ring | Extra (non-colour channel) |
|---|---|---|---|---|
| Completed | gold gradient, full | — | none | highlight arc at 0.76 r, 200°–252°, white 32 %, width 0.06 r, r ≥ 16. Only rimless disc. |
| Accepted | Shadow | early waxing gibbous, lit **width 60 %**: terminator through the poles with its equator at x = −0.20 r (circle centre (2.4 r, 0), radius 2.6 r), gold | Silver, o | **seal**: Night disc at (0.40 r, 0), radius max(0.16 r, 1.5 px). Only glyph with a dark mark on its lit side. |
| Ready | Shadow | right half, gold | Dusk, o (under the lit half) | r ≥ 9: glow (blurred gold disc 1.12 r, σ = 0.28 r, 55 %; ImGui: 3 discs). r < 9: one 1 px Moon ring at 1.25 r @ 35 %. Only glyph with anything outside its rim. |
| ReadyOnOtherJob | Shadow | right half, silver | Moon, o | half + bright rim, no outer ring |
| DoneThisCycle | Shadow | lens: disc ∩ disc offset −0.5 r (75 %, cusps at x = −0.25 r, y = ±0.968 r), silver | Dusk, o | lit on the **left**, grey rim, no mark |
| Blocked | Shadow | — | Silver, o | the only plain empty ring |
| Foreclosed | Shadow | — | Eclipse, o | **bar**: Eclipse line from (−0.636 r, −0.636 r) to (0.636 r, 0.636 r), width max(2 px, 0.22 r), butt caps; notch: Night disc at (0.72 r, −0.72 r), radius 0.30 r, r ≥ 12 only |
| Unknown | Shadow at 60 % | — | Dusk, o, dashed: 12 dashes of 16° from 12 o'clock for r ≥ 10, 8 dashes of 22° below | the only dashed rim |

Minimum sizes: the glyph is legible from r = 6 px (12 px box); the sheet shows r = 6, 7, 10.5,
16 and 28. Row glyphs should use r = 7.5 logical → 10.8 px at default scales, and the
inline box fraction can rise from 0.42 to 0.44 since the glow is allowed to overflow.

### 3.1a Accepted: the candidates (sheet row 0)

The owner's note: at 16–24 px the v2.0 Accepted (75 % offset lens + gold ring) reads as a full
moon. Cause: the gold ring closes the silhouette into a gold circle, and the dark side is
0.5 r − o wide at the equator, 2.3 px at r = 7, which anti-aliasing blends away. Row 0 of the
sheet renders every option at 24 / 16 / 12 px beside Completed, Ready and DoneThisCycle, and
repeats the 16 px line in greyscale (the achromatopsia / monochrome-stream test from panel
A2). Judged on the zoom sheet:

| Candidate | 16 px vs Completed | 12 px | greyscale vs DoneThisCycle | verdict |
|---|---|---|---|---|
| v2.0: 75 % lens, gold ring | fails: gold disc with a sliver | fails | mirror + ring tone only | rejected |
| (a) 60 % gibbous, gold ring | dark side 4.2 px wide, but the gold ring still closes a gold circle | marginal | mirror + ring tone only | rejected |
| (b) 66 % gibbous, gold rim over the lit side only | ok; the open dark side reads as a crescent-shaped bite | ok | bracket shape is unique | rejected: the open rim breaks the "every state moon is a closed disc" rule that separates states from the halo core, and at 12 px the arc ends look like the Foreclosed notch |
| (c) 75 % + journal badge at lower right | badge visible | badge is a 3 px dot on a 12 px moon | badge mirrors the Foreclosed notch position | rejected: a second "bite" vocabulary next to Foreclosed's notch, and the tick is unreadable below 32 px |
| (d) 75 % lens, Silver ring | dark side still a sliver | fails | mirror + ring tone only | rejected |
| (e) 60 % gibbous, Silver ring (a + d) | clear: gold "more than half" moon in a bright rim, Completed stays a rimless disc | clear | differs from DoneThisCycle by mirror, 15 % of width and rim luminance only | rejected on the panel's rule 2 (§3.7) |
| **(f) = (e) + Night seal dot** at (0.40 r, 0), radius max(0.16 r, 1.5 px) | clear | clear (3 px dot) | unique: the only gibbous with a dark in-disc mark | **chosen** |

The seal is the "written in the journal" mark: a dark stamp on the lit side. It keeps the
phase language intact (Accepted is still a waxing gibbous, between Ready = first quarter and
Completed = full; DoneThisCycle stays a waning gibbous), and it is the same primitive
(`AddCircleFilled`) as the Foreclosed notch, in a different place (inside, on the lit side) so
the two are never confused. The lit width moves from 75 % to 60 % so the dark side stays
≥ 3.5 px wide at 12 px; the mapping table in spec §2.1 should read "Accepted — waxing
gibbous (early, ~60 % lit), silver rim, seal".

### 3.2 Progress glyph (halo gauge), exact geometry (R = box/2)

| Element | Value |
|---|---|
| Track | circle r = 0.80 R, stroke `max(2, 0.18 R)`, VeilLine `#5C6584` (3.19 : 1 on Night; Dusk @ 0.8 on cards) |
| Arc | same circle, Moon, from −90° clockwise through `360°·v`, round caps |
| ε | `max(0.06, (stroke + 1.5) / (2π·0.80 R))` → 0.060 at 64 px, 0.061 at 24 px, 0.087 at 16 px |
| Gap | `max(1, 0.10 R)` |
| Core | R ≥ 12 only. `rc = 0.80R − stroke/2 − gap` = 0.61 R; Shadow disc + Dusk rim `clamp(0.12 rc, 1.5, 3)` at every fraction < 1; filling moon (floor ε_c = max(0.10, 1.5/(2 rc)), gold lit side, flat below rc = 9, shaded from 9, detailed from 12) when 0 < f < 1; gold gradient (+ detail from rc ≥ 12) when f = 1 |
| Complete | ring Moon full + glow rings (stroke 1.5× at 16 %, 2.2× at 10 %) at R ≥ 8 + gold core at R ≥ 12 |
| Minimum | R = 8 (16 px): track + arc only, stroke 2 px, the percentage written beside it. Below 16 px draw the percentage only |

### 3.3 Never confuse a state with a progress value

- State glyph = **solid disc filling its box**; progress = **ring with a hollow gap and a small core**.
- State glyph rims are Dusk/Moon/Silver/Eclipse **inside** the rim; the halo track is VeilLine and the arc is Moon **on** the ring, never inside a disc.
- Ready is the only state with anything outside its rim (glow, or the 1 px ring below r = 9); the halo glows only at exactly 100 %, where its shape (ring + core) is still not a solid disc.
- The halo arc always starts at 12 o'clock; state lit parts always sit on the right (waxing) or left (waning), never at the top.
- Dark marks: Accepted's seal is inside on the lit side; Foreclosed's notch bites the rim at the upper right and its bar crosses the disc. Nothing else carries a dark mark.

### 3.4 Token changes proposed for `Theme.cs`

| Token | Value | Use |
|---|---|---|
| `UnlitDisc` | → Shadow `#3A4363` | dark side of every state moon and of the halo core; always behind a rim |
| `VeilLine` (new, from ui-revamp) | `#5C6584` | halo track |
| `Umbra` (the old UnlitDisc) | `#2C334A` | maria / crater tint in the interior detail only |
| `MoonHigh` / `MoonDeep` | `#FFF0BE` / `#D6B25A` | shading stops |
| `SilverHigh` / `SilverDeep` | `#FFFFFF` / `#B9C2D8` | shading stops |
| `Bruise` | — | dropped (ΔE 2–4 vs Veil under CVD, 1.13 : 1 for normal vision) |

Why Shadow and not Veil for the unlit disc (the two documents disagreed): with the rim
mandatory on every state moon, the disc no longer has to carry the silhouette, so its own
contrast against Night (Shadow 1.89 : 1, Veil 2.38 : 1, both under 3 : 1) is not what decides
legibility; the rim does (Dusk 5.1 : 1 on Night, 4.3 : 1 on raised cards). What the disc
tone does decide is the lens contrast, and Moon on Shadow is 6.6 : 1 against 5.2 : 1 on Veil.
Veil is also the disabled-text colour, which would make a dark side read as "disabled".
The row 2 sheet on the `#141414` Dalamud background confirms Shadow + 1.5 px rim holds.

### 3.5 Sizes for the Journal tree

Line height L at default scales ≈ 18.4 px (Dalamud 16 px font × UiScale 1.15). Proposed
`treeGlyphRadius = clamp(0.5 · L · IconScale, 12, 18)` = 12 px at defaults → a 24 px halo
(the floor, regardless of IconScale 0.8), and row height `max(L, 2r + 6·Scale)` ≈ 30 px via
`ImGuiTreeNodeFlags.FramePadding`. Tables and the UiScale/IconScale matrix are in
`imgui-notes.md`. The sheet's row 4 shows exactly this floor: 24 px glyphs in 30 px rows.

### 3.6 Interior detail (lit parts only, r ≥ 12 px)

A gold disc with a radial gradient reads as a coin; the owner asked for a moon. The detail is
one fixed "map" in unit disc coordinates (x right, y down, fractions of r), drawn once and
clipped to whatever the lit region is, so Completed shows all of it, Ready half of it,
Accepted 60 % of it, and DoneThisCycle the left part, mirrored by nothing (the map is not
mirrored; the moon simply shows its left side). All tints are palette tokens at low alpha.

**Maria** (dark seas): three soft Umbra `#2C334A` ellipses. Edge softness: alpha ramps from
full at 0.70 of the ellipse radius to 0 at the edge (SVG: Gaussian blur σ = 0.05 r).

| Mare | centre | rx × ry | rotation | α on gold | α on silver |
|---|---|---|---|---|---|
| M1 | (−0.30, −0.24) | 0.32 × 0.24 | −25° | 0.20 | 0.14 |
| M2 | (0.30, 0.06) | 0.24 × 0.20 | −15° | 0.18 | 0.13 |
| M3 | (−0.10, 0.40) | 0.30 × 0.14 | +10° | 0.18 | 0.13 |

**Craters**: three rings. Floor = Umbra disc at α 0.14; shadow arc (C1 only) = Umbra at
α 0.30 on the upper-left inner wall (radius 0.84 rc, 170°→290°, angles clockwise from
3 o'clock); highlight edge = white at α 0.38 (0.30 on silver) on the lower-right inner wall
(radius 0.96 rc, −10°→110°). Line width max(1 px, 0.035 r). Light comes from the upper left,
consistent with the gradient's highlight.

| Crater | centre | rc |
|---|---|---|
| C1 | (0.34, −0.46) | 0.120 |
| C2 | (−0.50, 0.30) | 0.095 |
| C3 | (0.10, 0.60) | 0.075 |

**Terminator glow**: a band 0.12 r wide on the lit side of the terminator, MoonHigh at α 0.30
(white at 0.25 on silver), blurred σ = 0.03 r, clipped to the lit region. Geometrically it is
the lit region minus the lit region with the terminator moved 0.12 r into the light; the SVG
draws it as a 0.24 r stroke on the terminator circle clipped to the lit shape. Full moons have
no terminator and no band.

**Rim vignette**: Umbra whose alpha rises quadratically from 0 at 0.72 r to 0.16 at the rim
(0.12 on silver), clipped to the lit region. This is what turns the coin into a sphere; the
old highlight arc on Completed stays on top of it.

**Size thresholds** (per glyph radius r; the halo core uses its own rc):

| r | lit part |
|---|---|
| < 9 px | flat Moon / Silver |
| 9 – 12 px | radial gradient only (a clean 24 px moon) |
| ≥ 12 px | gradient + maria + craters + terminator glow + vignette |
| ≥ 16 px | + highlight arc (Completed) |

At 24 px (r = 10.5) nothing of the detail is drawn, so row glyphs stay a clean gradient; at
32 px (r = 16, sheet row 2 left column) the maria are 2–5 px blobs and the craters 2–4 px
rings — visible but quiet; at 64 px it reads as a moon. Nothing in the detail touches the
state channels: the seal, the bar, the notch and the dashes are drawn after it, and no detail
is drawn on Shadow.

### 3.7 Accessibility: what each state relies on when colour is removed

Rules adopted from the panel (accessibility.md §2.3): every state is unique at r = 6 in
greyscale; no pair differs *only* by mirror symmetry or *only* by ring colour; every gauge
under 16 px is drawn as text. The greyscale line in sheet row 0 is the test.

| State | Colour channel | Non-colour channel that survives greyscale at r = 6 |
|---|---|---|
| Completed | gold | the only rimless solid disc |
| Accepted | gold + silver rim | gibbous lit right + **dark seal dot** inside the lit side |
| Ready | gold + Dusk rim + glow | exact half + **1 px outer ring** at 1.25 r (r < 9) / glow (r ≥ 9); the only glyph with anything outside its rim |
| ReadyOnOtherJob | silver + gold rim | exact half + bright rim, nothing outside |
| DoneThisCycle | silver + Dusk rim | lit **left**, 75 %, grey rim, no mark |
| Blocked | Shadow + silver rim | the only plain empty ring |
| Foreclosed | Eclipse | **diagonal bar** through the disc (+ notch from r ≥ 12) |
| Unknown | Dusk | the only dashed rim |

Pairs the panel flagged, after v2.1: Accepted vs DoneThisCycle now differ by the seal (shape),
not by mirror alone. Ready vs ReadyOnOtherJob differ by outer ring vs bright rim (shape).
Foreclosed vs Ready / DoneThisCycle / Unknown rims (all grey under deutan/protan) differ by the
bar. Bruise is gone, so nothing depends on Bruise vs Shadow. Remaining colour-only pair:
Completed's gold vs a silver full disc — there is no silver full disc in the vocabulary, so
it does not arise.

Halo gauge: track 3.19 : 1 on Night, 3.3 : 1 on the Dalamud default background; arc Moon vs
track 5.5 : 1; stroke ≥ 2 px at every size; the core is a rimmed disc at f = 0 so an empty
node has a visible outline; below 24 px the number is always beside the ring, below 16 px
only the number is drawn. The mini bar (44 × 3) stays in every density as the most
acuity-tolerant encoding.

## 4. Journal tab visual upgrades beyond the glyphs

Shown in row 4 of the sheet.

1. **Section header rows**: 600-weight (second text pass, as today) plus a 1 px Moon rule at
   28 % alpha under the row, indented past the arrow, so sections read as chapters.
2. **Expansion badges**: a 14 px pill after the name (Veil 1 px stroke, Dusk 9 px caps:
   ARR, HW, SB, ShB, EW, DT) for nodes that map to one expansion; hidden when the pane is
   narrower than 200 px. (Panel B1: the caption colour should become Mist on tinted rows.)
3. **Count as "done / total" with a mini bar**: the count stays Dusk right-aligned; a
   44 × 3 px bar (Veil track, Moon fill, min 2 px when > 0) sits 12 px before it. Counts of
   completed nodes tint Moon like their names.
4. **Hover row**: Silver 5 % wash across the full row width (the tree node already spans the
   width; push `HeaderHovered`).
5. **Selected row**: Veil 22 % wash plus a 2 px Moon rule on the left edge; the count text
   switches to Silver so the selected row is the only one with two bright texts.
6. **Complete nodes**: name and count in Moon, glyph glows; at section level the gold rule
   under the header brightens to 45 %.
7. **Folded-path tooltip** keeps the current behaviour, but shows the halo at 32 px with the
   exact fraction to two decimals as the tooltip's header.

## 5. References

Styles studied for the interior detail, by eye only; no image was traced or copied and every
coordinate above is original:

- **Classic astronomical engravings** (17th–19th c. lunar maps in the manner of Hevelius and
  Cassini, and the Victorian "Moon in phases" plates): a few large low-contrast seas, craters
  drawn as rings with one lit and one shadowed wall, and a darkened limb. This is the source
  of the three-maria / three-crater economy and of the vignette.
- **Tarot "The Moon" cards** (Rider–Waite lineage): a face-less disc with a soft glow at the
  terminator and a heavier rim; the terminator glow band comes from here.
- **Game iconography** (Celeste's crescent sigils, Hollow Knight's pale ore and seal
  medallions, Ori's spirit-light orbs): flat fills with one or two shapes of interior
  detail that vanish at small sizes, and a single specular arc. This set the "detail only
  from r ≥ 12, gradient below" rule and the highlight arc.
- **FFXIV's own moon renders and the Dalamud plugin-icon style**: warm gold with a slightly
  desaturated deep tone at the edge rather than a darker hue, which is why the vignette is
  Umbra (blue-grey) over gold and not a darker gold.
- **Luxury watch moon-phase dials** (the classic gold moon on a lacquered blue disc): the
  two-tone gold-lit / blue-dark relationship, the small "seal" sized like a dial's applied
  index, and the rule that the moon is a finished object, so its marks are few and placed.
- **Vector moon-logo sets**: the reminder that below ~24 px a moon is its silhouette; hence
  the 12 px zoom test in row 0.

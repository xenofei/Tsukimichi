# Tsukimichi glyphs v2 — status moons and the Journal progress glyph

Proposal only. Companion files: `glyphs-v2.svg` (design sheet), `glyphs-v2.png` and
`glyphs-v2-zoom.png` (rasterized by `render_preview.py`), `imgui-notes.md` (how to build it
with ImDrawList). Nothing under `assets/` or in plugin code is touched.

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
| Veil `#4A5270` | 2.38 : 1 | 2.47 : 1 |
| Dusk `#7C86A8` | 5.1 : 1 | 5.3 : 1 |
| Moon `#F2D27A` | 12.5 : 1 | 13 : 1 |
| Silver `#DDE3F0` | 14.2 : 1 | 14.8 : 1 |

Anything under ~2 : 1 is invisible as a *shape*; 1.47 : 1 is a colour you can only see when
it is large and edged. So a 14 px moon has, in practice, only two visible states: "some gold"
and "no gold". A node at 129/195 (66 %) is a gold disc with a dark bite whose tone equals the
background; the bite is 4.9 px wide at the equator and tapers to 0 at the poles, so the eye
reads a slightly dented full moon. That is the complaint exactly.

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
drawn at 14 px in a 20 px row; the row has room for 20–24 px.

## 2. Candidates

All three keep the eight **state** moons as moons (they encode a category, and moons are the
brand). They differ in the **progress** glyph used by the Journal tree, the Characters and
Moonlit panes, the Flight pane, the detail-pane chain moon and the status bar.

### A. Refined phase moon (evolution of today)

- Disc radius r; unlit tone **Veil** `#4A5270` (2.4 : 1) instead of UnlitDisc.
- Crisp rim: `o = clamp(0.10·r, 1.25 px, 3 px)` drawn *inside* the rim (circle at `r − o/2`),
  Dusk for the plain moons, state colour where the spec already asks for a ring.
- Terminator maps fraction to visual width with a floor:
  `w = ε + (1 − 2ε)·f`, `ε = max(0.10, 1.5 px / (2r))`, so 17/612 is a 1.5 px crescent at any
  size and 97 % keeps a 1.5 px dark sliver. 0 and 1 stay exact.
- Radial shading on the lit part from r ≥ 9 px, highlight arc from r ≥ 16 px.
- Optional 8 px numeric badge ("3 %") at the lower right for r ≥ 12.

Pros: smallest change; the tree keeps literal moons; `FillingLayers` needs a two-line
change. Cons: even floored, a crescent is a *taper*, so 3 % and 10 % look alike until ~20 px;
the plain moon is the same silhouette as the Ready / Accepted state glyphs, so a 50 % node and
a Ready quest are told apart only by the ring colour; the badge fights the count text that
is already on the row.

### B. Halo gauge (ring arc around a moon core) — **recommended**

A thin **track ring** in Veil, a **gold arc** growing clockwise from 12 o'clock over it, and
a **core disc** behind a 1 px gap that is itself a small filling moon. Named after the
lunar halo (月暈): the ring reads as the moon's corona.

- Box S, R = S/2. Track radius `0.80 R`, stroke `max(1.5 px, 0.16 R)`. Gap `max(1 px, 0.10 R)`.
  Core radius `rc = 0.80R − stroke/2 − gap` (= 0.62 R from 24 px up, 0.58 R at 16 px).
- Visual fraction `v = ε + (1 − 2ε)·f` for 0 < f < 1, `ε = max(0.06, (stroke + 1.5 px) / circumference)`;
  v = 0 and v = 1 exactly at the ends. Round caps (two filled circles of radius stroke/2).
  The floor guarantees a visible pip beyond the cap for 1/612 and a visible dark gap at 611/612.
- Core: f = 0 → Umbra `#2C334A` (hollow look, framed by the track); 0 < f < 1 → filling moon
  in the core with its own floor `ε_c = max(0.10, 1.5 px / (2 rc))` (flat gold below rc = 6 px,
  shaded above); f = 1 → full gold core + full gold ring + two soft gold glow rings.
- Arc length is linear in f at every size: at 16 px the circumference is 40 px, so one quest
  of 195 is 0.2 px of arc plus the 1.5 px cap, 3 % is a 3.6 px pip, 25 % a quarter turn.

Pros: linear and legible from 14 px up (the sheet shows 0 / .03 / .10 / .25 / .50 / .75 /
.90 / .97 / 1 all distinct at 16 px); a completely different silhouette from every state
glyph (hollow ring + small core vs solid disc), so nothing is confused with Ready; still a
moon inside, and at 20 px+ the core phase doubles the arc, which is the "beautiful" part;
complete nodes get a real reward look (gold ring + core + glow). Cons: three primitives
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

### 3.1 State glyphs, exact geometry (r = disc radius)

Common: rim stroke `o = clamp(0.10·r, 1.25, 3)` px, ring circle at `r − o/2`; unlit tone
Veil; lit tones shaded radially (highlight centre at (−0.32 r, −0.34 r), radius 1.25 r; gold
stops `#FFF0BE` → `#F2D27A` at 42 % → `#D6B25A`; silver `#FFFFFF` → `#DDE3F0` → `#B9C2D8`)
from r ≥ 9 px, flat below. Draw order is always disc → rim → lit part, except the ring
states, where the ring goes last.

| State | Disc | Lit part | Rim / ring | Extra |
|---|---|---|---|---|
| Completed | gold gradient, full | — | none | highlight arc at 0.76 r, 200°–252°, white 32 %, width 0.06 r, r ≥ 16 |
| Accepted | Veil | lens: disc ∩ disc offset +0.5 r (cusps at x = 0.25 r, y = ±0.968 r), gold | Moon, o | |
| Ready | Veil | right half, gold | Dusk, o (under the lit half) | glow: blurred gold disc 1.12 r, σ = 0.28 r, 55 % (ImGui: 3 discs, see notes) |
| ReadyOnOtherJob | Veil | right half, silver | Moon, o | |
| DoneThisCycle | Veil | lens offset −0.5 r, silver | Dusk, o | |
| Blocked | Veil | — | Silver, o | |
| Foreclosed | Bruise `#645574` (Veil→Eclipse 25 %) | — | Eclipse, 1.2 o | notch: Night disc at (0.72 r, −0.72 r), radius max(0.30 r, 2 px) |
| Unknown | Veil at 45 % | — | Dusk, o, dashed: 12 dashes of 16° from 12 o'clock for r ≥ 10, 8 dashes of 22° below | |

Minimum sizes: the glyph is legible from r = 6 px (12 px box); the sheet shows r = 7 and
r = 10.5. Row glyphs should use r = 7.5 logical → 10.8 px at default scales, and the
inline box fraction can rise from 0.42 to 0.44 since the glow is allowed to overflow.

### 3.2 Progress glyph (halo gauge), exact geometry (R = box/2)

| Element | Value |
|---|---|
| Track | circle r = 0.80 R, stroke `max(1.5, 0.16 R)`, Veil |
| Arc | same circle, Moon, from −90° clockwise through `360°·v`, round caps |
| ε | `max(0.06, (stroke + 1.5) / (2π·0.80 R))` → 0.060 at 64 px, 0.062 at 20 px, 0.075 at 16 px |
| Gap | `max(1, 0.10 R)` |
| Core | `rc = 0.80R − stroke/2 − gap`; Umbra when f = 0; filling moon (floor ε_c = max(0.10, 1.5/(2 rc)), Umbra dark side, gold lit side, shaded when rc ≥ 6 px) when 0 < f < 1; gold gradient when f = 1 |
| Complete | ring Moon full + glow rings (stroke 1.5× at 16 %, 2.2× at 10 %) + gold core, at R ≥ 8; below R = 8 no glow |
| Minimum | R = 7 (14 px). Below that (status bar today: 4.5 logical) draw track + arc only, no core |

### 3.3 Never confuse a state with a progress value

- State glyph = **solid disc filling its box**; progress = **ring with a hollow gap and a small core**.
- State glyph rims are Dusk/Moon/Silver/Eclipse **inside** the rim; the halo track is Veil and the arc is Moon **on** the ring, never inside a disc.
- Ready is the only state with a glow; the halo glows only at exactly 100 %, where its shape (ring + core) is still not a solid disc.
- The halo arc always starts at 12 o'clock; state lit parts always sit on the right (waxing) or left (waning), never at the top.

### 3.4 Token changes proposed for `Theme.cs`

| Token | Value | Use |
|---|---|---|
| `UnlitDisc` | → Veil `#4A5270` | dark side of every state moon |
| `Umbra` (new, the old UnlitDisc) | `#2C334A` | hollow halo core, dark side of the core moon |
| `Bruise` (new) | `#645574` | Foreclosed disc |
| `MoonHigh` / `MoonDeep` | `#FFF0BE` / `#D6B25A` | shading stops |
| `SilverHigh` / `SilverDeep` | `#FFFFFF` / `#B9C2D8` | shading stops |

### 3.5 Sizes for the Journal tree

Line height L at default scales ≈ 18.4 px (Dalamud 16 px font × UiScale 1.15). Proposed
`treeGlyphRadius = clamp(0.5 · L · IconScale, 8, 18)` = 11.5 px → a 23 px halo, and row
height `max(L, 2r + 6·Scale)` ≈ 30 px via `ImGuiTreeNodeFlags.FramePadding`. Tables and the
UiScale/IconScale matrix are in `imgui-notes.md`. The sheet's row 4 is the conservative
variant: 20 px glyphs in 26 px rows, which is already the size where 17/612 reads at a glance.

## 4. Journal tab visual upgrades beyond the glyphs

Shown in row 4 of the sheet.

1. **Section header rows**: 600-weight (second text pass, as today) plus a 1 px Moon rule at
   28 % alpha under the row, indented past the arrow, so sections read as chapters.
2. **Expansion badges**: a 14 px pill after the name (Veil 1 px stroke, Dusk 9 px caps:
   ARR, HW, SB, ShB, EW, DT) for nodes that map to one expansion; hidden when the pane is
   narrower than 200 px.
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

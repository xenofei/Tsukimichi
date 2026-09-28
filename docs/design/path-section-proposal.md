# Tsukimichi — Path section as a star chart

Date: 2026-09-28
Status: proposal for owner approval (no code touched)
Scope: the **Path** card of the detail pane (§2.5 item 6 of `ui-revamp-proposal.md`), its folded completed runs, branch (Any-join) alternatives and the **Unlocks next** tail. Everything else in the approved mockup is unchanged.
Mockup: `docs/design/mockups/main-window.html` — the Path card inside the window, plus a standalone 360 × 420 card under the window that shows every element at once.
Today: `Tsukimichi/Ui/DetailPane.cs` `DrawPath` / `DrawUnlocks` — a list of `Selectable`s with a 6 px moon each, joined by a 1.5 px Moon line where the previous step is done and a 1 px Dusk hairline where it is not, expansion names as `TextDisabled` headers, completed runs folded into "▸ N completed steps", the target as a selected row; Unlocks next as a second plain list.

---

## 0. Brief

The owner's verdict on the approved mockup: *"The proposed quest path section needs a graphical update — it looks too plain. Make it more artistically beautiful. Use inspiration from other games/applications."*

What "plain" is, concretely: the path is a text list with a gutter line. It has no depth (nothing behind it), no rhythm (every row is the same 22 px), no destination (the target is a tinted row, indistinguishable from a selected row anywhere else), and no sense of *sky* even though every node is a moon. The fix is not more colour — gold stays the only warm hue (P2) — it is composition: a background that reads as night sky, a thread that reads as a constellation figure, and one node that reads as *where you are*.

Constraints carried over: ImDrawList only, allocation-free per frame, all sizes × `UiMetrics.Scale` and moons × `IconScale`, shape before colour (P3), no height animation (P5), the existing eight state glyphs unchanged.

---

## 1. Inspirations (researched, none copied)

| Source | What it does well | What we take | What we leave |
|---|---|---|---|
| **Final Fantasy X — Sphere Grid** | Nodes on a track; activated nodes are lit, the party member's current node has a bright pulsing marker; the unactivated web is dim but complete. | Lit-vs-unlit *track* (not just lit nodes) and a pulsing halo for "you are here". | The free-roaming 2-D web; our path is linear. |
| **Final Fantasy XII — License Board** | The board is partitioned into regions with distinct tints; unowned licences are faded silhouettes. | Region tints → expansion **sky bands**; faded silhouettes → the hollow ghost node for an untaken branch. | Grid layout. |
| **Path of Exile — passive tree** | An enormous web on a faint, non-competing background painting; the allocated path is one bright continuous line through a dim web. | The **faint backdrop** rule (never above ~0.3 alpha, never behind a label at full alpha) and the continuous bright thread. | Density; cluster art. |
| **Genshin Impact — Constellations / Honkai: Star Rail — Traces** | Unlocks drawn as a constellation: star nodes joined by thin lines, locked ones dim outlines, unlocked ones glow; a subtle star field behind. | Quests as stars-on-a-line; locked = outline, done = glow; three star magnitudes in the field. | Character art behind the chart. |
| **Hades — Mirror of Night** | A vertical list where each row has a primary and an alternate (flip) option, drawn side by side. | The **"or via …" alternate** placed beside the main line rather than as a second list. | Toggle semantics. |
| **Ori and the Will of the Wisps — Spirit Trees / ability web** | Soft radial glow on the active node, thin hairline links, very dark ground. | Multi-layer soft glow (three alpha discs) rather than a hard ring for the target. | Radial layout. |
| **Slay the Spire — map** | A vertical ascending route with dotted lines, forks that merge, and a clear "current floor" marker. | Ascending vertical route, **dashed segments for the not-yet-walked**, forks drawn as curves that merge into a node. | Hand-drawn look; random topology. |
| **Inscryption — map** | Dotted ink path with sparse ornaments. | Restraint: one ornament per section, not per row. | Paper texture. |
| **Duolingo — path** | One winding line; completed units are gold, the current node has a pulsing ring, completed sections collapse into a compact summary. | Gold for walked nodes, pulsing ring on the current node, **completed runs collapse into one bead**. | Cartoon scale; the wind. |
| **The Witcher 3 — quest journal** | Quests grouped under chapter headers, one tracked quest highlighted across the whole list. | Chapter (expansion) headers as small caption rows with a rule, one highlighted row. | Two-pane layout. |
| **Zelda: Tears of the Kingdom — Dragon's Tears** | Memories drawn as nodes on a constellation-like figure that light up as found, on a dark blue field. | "Lit as found" nodes with an outer ring, dark-blue field. | Geoglyph map. |
| **Timeline UIs (Ghost of Tsushima *Tales*, Assassin's Creed memory timeline, Destiny 2 triumph paths)** | A vertical spine with milestone nodes, the current milestone larger, the past compacted. | Larger current node; compacted past (the bead); the spine passing *through* section markers. | Horizontal scroll. |
| **FFXIV — Journal and Main Scenario Guide** | Gold hairline rules, small four-point star ornaments, a parchment-and-gold vocabulary; the MSQ guide shows one "next" quest above a muted list. | The **four-point star** as the section ornament and the constellation junction; hairline rule fading to the right; one "next" emphasised. | Parchment; Square's fonts. |
| **FFXIV — Sightseeing Log** | Entries are dim until discovered, then gold with a sparkle; a soft vignette behind. | Sparkle = the large stars' twinkle; discovered = gold moon. | Weather / time icons. |
| **Star maps / planispheres** | Constellation lines are hairlines; stars have magnitudes (dot sizes); the Milky Way is a faint band. | Hairline thread, **three magnitudes**, faint **band** fills. | Coordinate grids. |
| **Orreries** | Concentric brass rings around a gold sun. | Concentric rings around the target (halo + one thin ring). | Mechanical detail. |
| **Lunar phase calendars** | A row of moons whose phases progress; the reader sees time as phase. | Directly: every node is a moon in its state phase (the plugin's existing language), so the chart reads as a lunar calendar of the chain. | — |

Design rule extracted from all of them: *one bright continuous line, dim complete context, one glowing position, ornaments only at section boundaries.*

---

## 2. Concepts

All three keep the card chrome from the approved mockup (rounding 6, NightRaised, 1 px NightLine, header "Path · N steps · M done").

### Concept A — Star Chart (recommended)

```
 +- (route icon) Path ----------------------- 12 steps . 8 done -+
 | .  * A REALM REBORN ---------------------------.------        |  band header: 4-point star ON the thread, name, fading rule
 | .  [:] 5 moons walked >               .                       |  bead (capsule on the thread), Dusk label
 | .   O= Sylph-management        .                              |  gold thread, completed moon, Mist name
 |    * HEAVENSWARD .....(band tinted, faint stars)......        |
 |    [:] 3 moons walked >                    .                  |
 |     O= A Fortuitous Encounter                                 |
 | .  * STORMBLOOD ---------------------------------------       |
 |     D= The Rising Tide                 .                      |  Ready: half moon + small halo; still gold thread in (prev done)
 |     o: Under the Moonlight                                    |  future: dashed silver thread
 |     : ,-o or via A Distant Shore . 3 steps                    |  Any-join alternative: ghost node, curve merges into the target
 |   (( @ ))  Brotherhood of Ash            <- spotlight row     |  target: larger moon, 3-layer halo, breathing ring, gradient row
 |     :                                                         |
 |    *: UNLOCKS NEXT . 3                                        |  hollow star junction, dashed spine continues
 |     +-o Sylphic Sympathy                                      |  comb: quarter-arc branch into each unlock
 |     +-o Moonlit Vows                                          |
 |     +-o Whispers in the Dark                                  |
 |     :  and 2 more                                             |
 +---------------------------------------------------------------+
```

A vertical constellation strip: one thread down a 36 px gutter, moons on the thread, expansion **sky bands** behind the rows (alternating faint tint, pre-seeded star field), gold solid thread where walked, dashed silver where not, the target as the one large glowing moon, completed runs as a capsule **bead** on the thread, alternatives as a ghost node whose curve merges into the join, and Unlocks next as a comb hanging off the continuing spine. Names stay left-aligned text at the same x as today, so scanning is unchanged.

Verdict: the richest reading of "night sky" with zero change to what the rows *mean*; every element maps to a small number of ImDrawList primitives; long chains still fold.

### Concept B — Sphere Path (zigzag nodes)

```
 |    O            5 moons walked                    |
 |      \                                            |
 |        O   Sylph-management                       |
 |      /                                            |
 |    D   The Rising Tide                            |
 |      \                                            |
 |        o   Under the Moonlight                    |
 |      /                                            |
 |  (( @ ))   Brotherhood of Ash                     |
```

Nodes alternate between two columns (x = 18 and x = 40) like the Slay the Spire / Duolingo winding path, joined by diagonal segments; the sky bands and star field as in A.

Verdict: the most "game map" of the three, but the zigzag costs 22 px of label width on every other row, makes the label x jitter (harder to scan a 20-row chain), and with folding most chains are 4–8 rows, too few for the wind to read as a wind. Rejected; the diagonal-merge curve survives in A's branch drawing.

### Concept C — Illuminated Journal (ledger rows)

```
 | * A Realm Reborn --------------------------------- *  |
 |   O  Sylph-management .......................... done |
 | * Stormblood ------------------------------------- *  |
 |   D  The Rising Tide ........................... ready |
 |   o  Under the Moonlight ..................... blocked |
 | | @  Brotherhood of Ash ....................... here | |
```

FFXIV-journal vocabulary: gold hairline rules with four-point stars at both ends of each expansion header, dotted leaders to a right-aligned state word, the target row framed by two gold bars. No thread, no sky.

Verdict: elegant and cheapest, but it is a *book*, not a *sky*; it drops the one thing the current design gets right (the thread showing where the gold stops), and it duplicates the state word the moon already gives. Rejected; its band-header ornament (star + fading rule) is adopted in A.

---

## 3. Recommendation

**Concept A, Star Chart**, with C's band header. It is the only one where the metaphor (a constellation of moons across sky bands), the data (a linear chain with one target, folded past, optional forks, a fan of unlocks) and the medium (circles, lines, a few beziers, points) line up without compromise.

---

## 4. Specification

All sizes are logical px at `UiMetrics.Scale = 1.0`; moons and node radii additionally scale by `IconScale`. Colours are the tokens in `ui-revamp-proposal.md` §4.3.

### 4.1 Card and columns

| Item | Value |
|---|---|
| Card | as approved: rounding 6, fill NightRaised, 1 px NightLine border, padding 10 × 8, header line 0.85× Mist with the `Route` icon, right caption "N steps · M done" in Dusk |
| Chart area | full card inner width (324 at the 360 px column); drawn inside a clipped child (§4.12) |
| Gutter | 36 px; **thread x = 18** (gutter centre) |
| Label x | 36 (same as today's label position, so nothing moves for the eye) |
| Row heights | band header 20 · step 24 · bead 24 · alternative 22 · target 32 · unlocks header 20 · unlock 22 · "and N more" 18 |
| Node radius | step **r 7** (14 px); target **r 9**; folded-run inner moons r 6; ghost (alternative) r 5; unlock r 6; band sigil r 4 |

### 4.2 Sky bands (one per expansion)

- Each expansion's rows are one band spanning the card's inner width, from the top of its header row to the bottom of its last row.
- Band fill: bands alternate **transparent** and **Silver @ 0.03**, rounding 4, inset 2 px from the card edges. (On NightRaised this is a one-step tint, enough to separate three bands without reading as a table.)
- Band top: a 1 px line Silver @ 0.05 (a "horizon"); nothing at the bottom.
- **Band header row** (20 px): a **four-point star sigil** centred on the thread (outer r 4, inner r 1.6, Dusk; the thread passes through it — the expansion boundary is a waypoint star); the expansion name at label x in **Mist 0.85×**, upper-case; a **fading rule** from name end + 8 px to the right edge: 1 px, VeilLine @ 0.6 → @ 0 (`AddRectFilledMultiColor`).
- A single-expansion path still gets its one header (it is the band's caption).

### 4.3 Star field

Purpose: depth without noise. Rules:

- Density **1 star per 1,400 px²** of band area (a 324 × 92 band ≈ 21 stars), cap 48 per band, minimum 6.
- Three magnitudes: 70 % **r 0.6 @ 0.14** (drawn as a 1 × 1 `AddRectFilled`), 25 % **r 1.0 @ 0.22** (`AddCircleFilled`, 6 segments), 5 % **r 1.5 @ 0.32** plus a cross of two 1 px lines 5 px long at Silver @ 0.12 (the FFXIV sparkle).
- Colour Silver; never gold (gold means "look here").
- Exclusion zones: within 12 px of any node centre, within 6 px of the thread, and the label text box (x 36 → name width, ± 6 px vertically). Stars behind *caption* text (Dusk / Mist 0.85×) are allowed — at ≤ 0.32 alpha they do not measurably change contrast.
- **Pre-seeded**, deterministic: the seed is `expansionId * 7919 + rowCount`, so the sky is identical across frames and re-selections of quests in the same expansion, and the field is rebuilt only when `PathRows` is rebuilt.
- Twinkle (only the 5 % large stars): alpha ± 0.08 on a sine with period 3–5 s and a per-star phase; off under `ReduceMotion`.

### 4.4 Thread

| Segment | Style |
|---|---|
| **Walked** (the step above is Completed) | 2 px **Moon @ 0.9** over a 5 px **Moon @ 0.10** glow line (two `AddLine`s); round caps come free from the node discs |
| **Not walked** (the step above is not Completed) | **dashed Silver @ 0.45**, 1 px, dash 3 / gap 3, phase starts at the upper node's rim so the first dash touches the moon |
| Through a bead | the thread passes *behind* the bead; the bead sits on top |
| Through a band header | continuous; the sigil sits on top |
| Below the target (the Unlocks spine) | dashed Silver @ 0.35, 1 px (dimmer: it is the future's future) |

The rule "lit iff the previous step is done" is today's rule; it means the gold thread arrives *at* the target's doorstep when everything before it is done, and stops one node short when it is not — the eye reads exactly where the gold ends.

### 4.5 Nodes

- Every node is the existing state glyph (`MoonGlyph.Draw`) at its row's radius; nothing about the eight glyphs changes.
- Name colour by state: Completed → **Mist** (done, de-emphasised); Ready / Accepted / Blocked / ReadyOnOtherJob / DoneThisCycle → **Silver**; Foreclosed → **EclipseText**; Unknown → **VeilText**. Level is *not* shown (the tooltip has it).
- Node centres are pixel-snapped (`docs/design/glyphs/imgui-notes.md` §1) so the 1.25 px rims stay crisp.

### 4.6 The target

The single point of emphasis; four layers, all cheap:

1. **Spotlight row**: a horizontal gradient fill across the row, **Moon @ 0.14 at the left edge → Moon @ 0.02 at the right**, rounding 4, inset 2 px (one `AddRectFilledMultiColor`). Replaces the flat `SelectionFill` so it does not look like a selected list row.
2. **Halo**: three discs, **Moon @ 0.04 / 0.07 / 0.10 at 2.2 r / 1.7 r / 1.35 r** (the Ready halo is 1.6 / 1.35 / 1.15 — the target's is deliberately wider so a Ready target still reads as *target*).
3. **Orrery ring**: 1 px **Moon @ 0.45** circle at **1.5 r**, alpha breathing 0.35 ↔ 0.55 with a 2.4 s sine (off under `ReduceMotion`; static 0.45).
4. The moon itself at **r 9** (vs 7), name in **Silver, two-pass bold** (as section names in the tree).

"Show path" reveal: keeps the approved 1.5 s pulse, applied to the ring (radius 1.5 r → 2.4 r while alpha 0.7 → 0, twice) instead of the card border.

### 4.7 Hover and click

| Element | Hover | Click | Tooltip |
|---|---|---|---|
| Step / unlock row | row fill **NightHover** rounding 4 from x 34 to the right edge (not over the gutter, so the thread stays clean); name → Silver (Completed names lift from Mist); the moon gets the hover halo at 0.4× (approved rule); cursor `Hand` | select that quest (as now) | 14 px state moon + state name + "Lv N · Expansion" |
| Target row | no hover fill (it is already lit); cursor `Arrow` | nothing | — |
| Bead | border Moon @ 0.6 → Moon; label Dusk → Mist; cursor `Hand` | expand / collapse (§4.8) | "Show the completed steps" / "Fold the completed steps away" (existing strings) |
| Alternative (ghost) | ghost ring Dusk → Silver; label Dusk → Mist | select that branch's first quest (the chart re-roots on it, which *is* how you inspect the other branch) | "Alternative prerequisite · N steps remaining" |
| Band header | none | none | none (it is a caption) |
| "and N more" | Dusk → Mist | none (until a "show all" is wanted) | lists the next 8 names |

Keyboard: every clickable row keeps a real item (`Selectable` with transparent header colours, or `InvisibleButton`), so focus / arrow navigation and the focus ring (§5.3 of the main proposal) work unchanged.

### 4.8 Folded runs (the bead)

**Collapsed** — a **capsule 14 × 22, rounding 7**, centred on the thread: fill **NightSunken**, 1 px border **Moon @ 0.6**, inside three **2 px Moon dots** at −5 / 0 / +5 px (a vertical ellipsis that reads as "moons, stacked"). Label at label x in **Dusk 0.85×**: "**N moons walked**" followed by a 6 px chevron `›` in Dusk. The gold thread runs in at the top and out at the bottom (both segments are walked by definition).

**Expanded** — the bead becomes an **open ring** (14 px, 1 px Moon ring, no fill, chevron `⌄`) on its own 24 px row with the label "N moons walked" in Mist, and the run's steps follow as **compact rows (22 px, r 6, names Mist)** on the gold thread. A **1 px Moon @ 0.25 bracket** at x = 4 spans from the ring row to the last expanded step so the group reads as one unit. Click the ring row to fold again. No height animation (P5); the rows just appear.

Rule change (small, in `BuildPathRows`): **the completed step immediately before a non-completed step never folds** (it is the moon the gold thread visibly arrives from). The fold threshold stays `MinFoldedRun = 2`, so "1 moon walked" never occurs.

Label wording is a string change (`FoldedRunCollapsedFormat` "▸ {0} completed steps" → "{0} moons walked"); the current strings work as-is if the owner prefers them.

### 4.9 Branches (Any-joins)

`PathFinder` walks only the cheapest branch through an Any join, so the model today has no fork data. Proposed (Core, small): after building the path, for each step whose quest has `PreviousQuests.Join == Any` and ≥ 2 catalogued prerequisites, list the prerequisites **not on the path** as alternatives with `RemainingCount` each (`PathFinder.Alternatives(rowId, …)` or computed in `DetailPane.BuildPathRows`). Cap 3 per join, then "and N more".

Drawing — the subway merge:

- Alternative rows are inserted **immediately above the join node**, after the chosen branch's last step; the main thread continues straight through them at x 18.
- Each alternative is a **ghost node**: r 5, no fill, **1 px Dusk ring dashed 8 × 22°** (the Unknown dash pattern — it is "a road not taken", visually a cousin of "unknown"), at **x 40**; label at x 52 in **Dusk 0.85×**: "or via *Quest Name* · N steps".
- A **1 px Dusk @ 0.6 cubic** from the ghost's bottom (40, y + 5) to the join node's rim: `AddBezierCubic(P0 = ghost, P1 = ghost + (0, 12), P2 = join − (0, 12), P3 = join − (0, r))`, 12 segments. Two alternatives stack (22 px apart), both curving into the same node.
- If the chosen branch is fully walked and an alternative is not, nothing changes — the gold is the walked line, the ghost is grey; if the *target itself* is the join, the curves merge into the halo, which is the mockup's case.

### 4.10 Unlocks next (the tail)

- After the target row, the spine continues **dashed Silver @ 0.35** down the gutter.
- **Junction row** (20 px): a **hollow four-point star** (outer r 4, 1 px Dusk stroke, no fill) on the spine, then "**UNLOCKS NEXT · N**" in Mist 0.85× with the same fading rule as a band header. Hollow vs filled tells the two kinds of star apart without colour.
- Each unlock (22 px): the spine passes at x 18; a **quarter-arc branch** (`PathLineTo` from (18, y − 11) → `PathBezierQuadraticCurveTo`(18, y) → (30, y), then a line to the node rim at x 34; 1 px Silver @ 0.35) into a node at **x 40, r 6**; name at x 52, colour by state.
- "and N more" (18 px): Dusk 0.85× at x 52; the spine ends **2 px below the last branch** with three fading 1 px dots (Silver @ 0.3 / 0.2 / 0.1) — the constellation trails off.
- No unlocks: the junction row reads "UNLOCKS NEXT · none" in Dusk and the spine ends right under the target with the three fading dots (the existing `UnlocksNone` sentence moves to the tooltip).

### 4.11 Empty and single-quest states

- **Path of one** (`Path.Count <= 1`): no band header; a single target row (spotlight, halo, r 9) with a Dusk 0.85× caption under the name: "Starts its own path — no previous quests." (today's `PathSingle` string), then the Unlocks tail as normal. The star field still draws behind (one band, ≥ 6 stars), so even a lone quest gets a sky.
- **Path of one, no unlocks**: the same row, caption "Stands alone — no previous quests, unlocks nothing."; the spine ends with the fading dots. Card height ≈ 64.
- **No snapshot / all states Unknown**: identical geometry with Unknown glyphs and a VeilText caption "States unknown — take a snapshot to light the path." Threads all dashed; the target still gets the halo (position is known even when state is not).

### 4.12 Scaling and very long chains

- Everything × `Scale`; radii × `IconScale`; row heights `max(lineHeight + 6, 2.4 r)` so a large `IconScale` never overlaps moons. Star density is per *logical* area, so a 1.6× window has the same number of stars, larger.
- **Height**: the chart lives in a clipped child of height `min(contentHeight, max(160, 0.4 × detailHeight))`. With folding, Hildibrand's 57-step line at a mid-chain target is typically 3 bands, 3 beads, ~6 loose steps and the tail ≈ 400 px logical → it scrolls at the default 700 px window and fits at ≥ 900.
- **Auto-scroll**: on selection, scroll so the target sits at 60 % of the child height (`SetScrollHereY(0.6f)`, existing `PathScrollFrames`).
- **Jump to target**: while the target is outside the visible range, a pill (h 18, fill Moon @ 0.16, text Moon 0.85× "▴ target" or "▾ target") floats at the child's top or bottom inside edge; click scrolls it back. Drawn on the child's draw list after the rows so it overlays.
- **Thread minimap** (only when the child scrolls): a **4 px column** in the card's right padding, full child height: Veil @ 0.35 track; a Moon segment for the walked part of the chain (proportional to *step index*, not pixels, so beads do not distort it); a 2 px Moon dot for the target; a Silver @ 0.12 window for the visible range. Three rects and a dot; click on the track scrolls proportionally. The tree pane has no minimap; the path is the only list that folds, so this is the only place it earns its cost.
- **Density modes**: Dense → step 20, node r 6, target r 8, no star cross, no breathing; Cards → step 28, r 8 / 10.

---

## 5. ImGui implementation note

Draw order inside `ImRaii.Child("##path", size)` (scrollbar kept, styled 6 px, grab Veil @ 0.5):

1. **Layout pass (no drawing)**: walk `PathRows` once computing each row's y from the cumulative heights (row heights are a function of `Kind`, so y is accumulated in a local). Record band extents (first / last row y per expansion), node centres for visible rows, the target y. Everything is a value on the stack.
2. **Bands + stars**: per band, `AddRectFilled` (tint) + `AddRectFilled` 1 px (horizon); stars from the pre-seeded `float[]` on the model (x, y, magnitude, phase; ≤ 48 × 4 floats per band), skipping any star whose y is outside the clip rect (`ImGui.IsRectVisible`).
3. **Threads**: rows are visited in order and each row draws the segment from the *previous node* (today's `Chain` struct) — no look-ahead needed because a segment's style depends only on the upper node. Walked: two `AddLine`. Dashed: a `for` over 6 px periods with `AddLine` per dash (a 24 px row = 4 dashes). Bezier merges: `AddBezierCubic(…, thickness 1, segments 12)`. Unlocks comb: `PathLineTo` + `PathBezierQuadraticCurveTo` + `PathStroke`.
4. **Nodes**: `MoonGlyph.Draw` as today. Target halo = 3 × `AddCircleFilled` + 1 × `AddCircle` (ring). Bead = `AddRectFilled` (rounded) + `AddRect` + 3 × `AddCircleFilled`. Filled sigil star = two `AddTriangleFilled` (a four-point star is not convex, so not `PathFillConvex`); hollow sigil = `PathLineTo` × 8 + `PathStroke(closed)`.
5. **Items + text**: `Selectable` (header colours pushed transparent, hover fill drawn by us from x 34) or `InvisibleButton` for the bead / ghost / pill; names via the item's own text; captions via `AddText(font, size × 0.85, …)` (the approved Typography fallback).
6. **Overlays**: jump-to-target pill and minimap on the same draw list after the rows.

Allocation: the model already materialises `PathRows`; add `StarField` arrays and `Alternative` rows at build time. The per-frame path touches no heap: no strings are formatted (all labels are pre-formatted in the model), no lists are created, hover state is an `int` (hovered row index) and expansion state is the existing `HashSet<int>`.

Primitive budget (worst realistic visible frame: 3 bands, 1 bead, 12 steps, 2 ghosts, target, 8 unlocks): bands 6 + stars ≤ 144 + threads ≈ 60 (dashes dominate) + nodes ≈ 12 × 5 + target 6 + ghosts 4 + comb 16 + captions 6 ≈ **~300 primitives, ≈ 2.5 k vertices**, one draw list, one clip rect. Comparable to 40 gauge rows in the tree; no measurable frame cost. Arc / circle segments follow `MoonGeometry.SegmentsFor(r)`; the halo discs use 24 segments at r ≤ 20.

Motion: the target ring and the large-star twinkle read `ImGui.GetTime()` directly (no `Motion` keys), so there is nothing to prune; both gated by `Configuration.ReduceMotion`.

Model / Core changes summarised: `PathFinder` exposes Any-join alternatives (or `DetailPane` derives them from `Prereq.Join`); `BuildPathRows` keeps the last completed step before a non-completed one unfolded; `PathRow` gains an `Alternative` kind and star-field data; `UiMetrics` gains `PathNodeRadius (7)`, `PathTargetRadius (9)`, `PathGutter (36)` and row heights per density. `Strings`: optional "N moons walked", "or via {0} · {1} steps", the two single-quest captions.

---

## 6. Open questions for the owner

1. Alternatives (Any-joins): draw them by default (as mocked) or behind a "show alternatives" toggle in the card header? Default-on adds up to three grey rows near a join.
2. Bead label wording: "5 moons walked" (mocked) or keep "▸ 5 completed steps"?
3. Thread minimap on scrolling paths: worth its 4 px column, or is "jump to target" enough?
4. Star field density: 1 / 1,400 px² is deliberately sparse (~20 per band). The mockup shows it; say if the sky should be busier.

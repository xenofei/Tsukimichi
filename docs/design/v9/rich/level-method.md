# Moonfall: the level-scene method

Status: draft for the owner's review, 5 October 2026, with six pilot levels in `levels/` (see the end). It answers the owner's brief: "For all the levels, there needs to be a design that builds around it or emphasizes it (gives it meaning)."

Every Moonfall level is **an illustrated scene, and its pegs and bricks draw that scene's subject**. The player should be able to say what a board is before the first shot: "the airship road", "the moogle", "the ferry in the stars". When the pegs clear, the painting is still there, so a cleared board looks finished rather than empty.

Nothing here comes from Peggle. The subjects, the paintings and the layouts are FFXIV's world (official art, used as the owner allows) or our own.

## 1. Choosing a level's subject

A subject must pass four tests:

1. **It belongs to the campaign's road.**
   - The Moon Road (base, 55 levels) is an overland journey across Eorzea by night: cities, ridges, forests, roads and the creatures met on the way.
   - The Far Shore (expansion, 60 levels) is the sea voyage and what lies past it: harbours, islands, the sky over open water, and at its end the moon itself.
   - Each stage of five levels follows one stretch of the road, and its character's medallion hangs over it on the Adventure map.
2. **It has one strong shape.** The subject must read at 650 × 553 units (the board's opening) under 60 to 110 pegs. Good shapes are a silhouette (a creature, a tower), a line (a ridge, a coast, a route), a ring (a dome, a window, an orbit) or a set of points (a constellation, cities on a map). A busy panorama with no single shape fails this test.
3. **The shape leaves room to play.**
   - It must avoid the launcher's swing: no piece within 85 units plus its radius of the pivot at (400, 87).
   - It must stay clear of the bucket: nothing lower than y 560.
   - The bright part of the art (a moon, a lit planet, a lamp) must sit where pegs need not go.
4. **It has a source that holds up at 2×.** In order of preference:
   - **(a) An official painting from the player's own install.** The 37 regional loading-screen paintings (`ui/loadingimage/-nowloading_base*.tex`, 1920 × 1080) and the world maps (`ui/map/world/*`). They are rich, they are FFXIV's own, and they cost the plugin nothing to ship, because it reads them from the game as the Flight pane already does.
   - **(b) Our own painting, made in code** (`src/brush.py` and the scene scripts). Use this when no official painting has the subject, for example a creature or a constellation.
   - **(c) A Fan Kit wallpaper.** Only when it carries no logo or text.

The six pilots cover both kinds of source. Four start from official paintings: the world map, Ishgard, Old Sharlayan and Mare Lamentorum. Two are our own: the moogle and the constellation.

## 2. Composing the scene so the layout draws it

The rule: **the layout sits on the subject's edges, never on its face.** Pegs follow a silhouette from 14 to 20 units outside it, or ride a line, or ring a shape. The subject's interior stays mostly open, so the art shows through and the ball has room.

There are six techniques, one per pilot, and they combine:

| Technique | What the pieces do | Pilot |
|---|---|---|
| **A trail on a map** | Dotted pegs (slightly smaller, r 9) follow a route engraved on the map. Each stop is a ring of five moons, and the rings hold the oranges, so clearing the board means visiting every stop. | base-p1 The Airship Road |
| **Terrain emphasis** | A continuous run of bricks lies on a ridge's crest, with a moon standing over each summit. Gentle humps of brick ride the cloud tops, and arches sit in a bridge's arches. | base-p2 The Holy See |
| **A creature in outline** | An even ring of moons runs 18 units outside the creature's silhouette, computed from the painting's own masks. The parts a child would draw first carry the oranges: the pom-pom, the ears, the wing tips, the eye. | base-p3 The Moonlit Post |
| **A landmark partly outlined** | Curved bricks lie on the crowns of the domes, with a moon on each finial. The falling water is a column of pegs, and the columns are short vertical runs. Only part of each shape is drawn, and the painting finishes it. | exp-p1 The Domes of Sharlayan |
| **A constellation** | A moon sits on every star of the figure, and smaller moons are dotted along the atlas's lines. A loose field of sky stars surrounds it, thicker along the Milky Way. | exp-p2 The Ferry in the Stars |
| **Orbit** (our own) | The painting implies motion, so the layout moves. A ring of moons orbits the world (an engine `orbit` mover: 26 pegs, 26 s a turn, 42 px/s), and the drifting rocks are loose rows along the belt. | exp-p3 The Sea of Sorrows |

Further techniques for the other 109 levels:
- **Silhouette band:** a skyline traced along its roofline.
- **Reflection:** a gate and its mirror image across a waterline, the reflection drawn with slow `slide` movers like ripples.
- **Light and shadow:** pegs along a lantern's beam.
- **Procession:** a lantern parade dotted along a street.
- **Lattice:** a rose window or a trellis as rings and spokes of brick.

### Line weights

- **Continuous lines are bricks** (12 thick, 24 to 28 long, 2 to 3 apart): crests, decks, dome crowns. A ball rolls along them, so a sloped crest becomes a slide.
- **Dotted lines are pegs at least 34 apart centre to centre** (14 between them): outlines, routes, star lines. A ball passes through a dotted line instead of resting on it.
- **Smaller pegs (r 7 to 9) are secondary lines**, such as the trail between cities or the lines between stars. They read as a lighter stroke under the main stars.
- **Never a cup.** A concave-up arc of brick is a bowl that traps the ball, so outlines that hang (a hull, a bowl) are always dotted. Concave-down arcs, such as domes, arches and cloud tops, are bricks.

## 3. Placing oranges and greens

**Meaning.** The engine picks the 25 oranges at random from the pieces marked `canBeOrange` (MoonfallGame.PickColours). So the designer marks **only the pieces that carry the subject**, about 25 to 34 of them:
- the cities on a route;
- the summits;
- a creature's features;
- a constellation's stars;
- dome crowns;
- half of an orbit.

Wherever the 25 land, they light the meaning. The other pieces are `canBeOrange: false`.

**Play.** The pool must be spread over the whole board, upper and lower, left and right, so no single region holds most of the oranges. The checks:
- No 200 × 200 square holds more than 10 candidates.
- The pool reaches below y 400 on both halves of the board.
- Between 25 and 35 candidates, so a level is never solved by memory alone yet always shows its subject.

Where the subject sits in one region (the moogle in the middle, the ferry in the sky), add a few candidates elsewhere that still mean something: the brightest field stars, the creature's feet.

**Greens (two, from level 3).** The engine takes them from the remaining blue pegs at random. Greens should be reachable by a direct shot, but today the format cannot say which pegs may be green. Proposal: format v2 adds `canBeGreen` (default true), so a designer can keep greens off a figure's eye or off a constellation star. This is open question 3 in `spec-rich.md`. The pilots work without it.

**Purple.** One per turn, from the blue pegs at random. No designer control is needed.

## 4. The hard rules (checked by the loader and by `tools/mfcheck`)

| Rule | Value | Checked by |
|---|---|---|
| Oranges | 25 per level, from at least 25 `canBeOrange` pieces (the pilots use 25 to 34) | `MoonfallLevelLoader` |
| Pieces | 28 to 400. Pilots use 62 to 100; target 80 to 120 for a full board | loader, mfcheck |
| Launcher zone | no piece within 85 units plus its radius of (400, 87), including along a mover's path | loader |
| Bucket zone | nothing lower than y 560 (the bucket's rim at 573 less a ball and a pixel), so a ball always passes over the bucket | loader |
| Walls | every piece inside x 75.5 to 724.5. Keep 12.5 or more clear of a wall, or touch it: a gap narrower than a ball wedges it (mfcheck "wall pinches") | loader, mfcheck |
| Overlaps | no round peg overlaps another piece. Movers never pass through a still piece, and pass at least a ball's width from one | loader, `layout.py` |
| Mover speed | 420 px/s at most | loader |
| No saddles | two pegs closer than a ball (gap under 12) must stand steeper than the angle a resting ball needs, `asin((r + gap/2)/(r + 6))` (46° for a gap of 3); otherwise they are 14 or more apart | `layout.py` check |
| No unreachable pockets | every piece is reached by some first shot from a fresh board (mfcheck `sweep`: "pieces no first shot reaches"), or becomes reachable once the pieces shielding it clear. No piece sits inside a closed ring with no gap of a ball's width | mfcheck sweep, critic |
| Stuck balls | the stuck rule (1.5 s, or 3 s without sinking) should fire on fewer than 5% of first shots (mfcheck sweep: "stuck rule fired") | mfcheck sweep |
| In reach | every orange candidate is touched by some first free flight from the launcher (no candidate at the launcher's height or tucked in a top corner). A candidate no direct flight can touch was the commonest reason a level was never won (critic, round 1) | `layout.py` check |
| Spread | at most 10 candidates in any 200 × 200 square (scanned in 5 px steps); candidates below y 400 on both halves | `layout.py` check |
| No notches | two bricks either touch or leave a ball's width and more between them (not 3.5–12.5 px); a peg stands 13 px or more from any brick (no cradle at a brick's end) | `layout.py` check |
| Winnable | a level is won by mfcheck's greedy player (48 games at level 5) at a rate no worse than the shipped levels' lowest (5 of 48 at the time of writing). It shows that no orange is impossible, not that the level is easy | mfcheck play |

**The pre-flight blocks the export.** Every level script raises when `check()` reports a problem, so a level that breaks a rule is never written (critic, round 2: a level file and its script had drifted apart).

## 5. Grading the scene so pegs stay readable

A peg's lit face is about L 0.6 to 0.7, and its hue carries its kind. A scene behind it must never compete. Four steps make sure of that.

1. **The Medallion night grade, version 2** (`rich_lib.night_lab`, for official paintings; rebuilt after realism rounds 1 and 2).
   - It works in OKLab. Lightness is lowered on a curve (`L^1.25` to `L^1.8`), so haze falls back and the planes facing the light stay brightest.
   - A wide base layer (48 units, wider than the largest form) is compressed under a soft knee that keeps a slope of 0.35, and the detail layer is added back. A cloud or a dome keeps its modelling instead of flattening to a slab.
   - A soft roll-off over the last 0.06 holds the ceiling.
   - A **form light** then gives the painting's pale shapes relief. A painted dome is often a flat white shape; its silhouette, blurred, rounds into a height, and the one light from the upper left shades it, darkening the faces turned away more than it lightens the others.
   - Chroma is kept (mid-tones scaled more than highlights). Hue is pulled toward lapis in the shadows and moonstone in the highlights, so lit cloud reads cool silver, never khaki.
   - Yellow and green (OKLab hue 45–195°) are cut to 0.3 chroma, so foliage goes blue-grey, as under moonlight. Warm hues are drained harder than cool.
   - The sky's own colour is halved.
   - The Far Shore uses the same grade, pulled toward violet-moonstone.
   - **Our own paintings** are painted directly in the same values, and a painted moon is emissive (an even face, no terminator), because it is the light, not a lit object.
2. **The value ceiling.** The 99th-percentile luma of a scene behind the board is about 0.42 or less (0.41–0.43 on the pilots), with nothing above about 0.47. The one exception is a painted moon or our own overlay line, which the layout leaves clear.
3. **The light.** Every scene's natural light comes from the upper left, the side the pegs and the frame are lit from.
   - An official painting lit from the right is mirrored. The pilots mirror Ishgard and Mare Lamentorum.
   - A painting with lettering, such as the map, is never mirrored. The world map has no single sun, so it doesn't need to be.
4. **The veil.** Where the layout is, the scene recedes. It dims by 20 to 36% within about 18 units of every piece (36% over bright cloud and pale domes), feathered over 6 units, and is untouched elsewhere. The veil has no shape: it is not a shadow (there are no cast shadows on the board), and the painting simply quietens where the play is.

Readability is checked at 640 × 480, where a peg is 16 px, on the composite renders. Every peg kind must separate from the scene behind it. Purple, the darkest kind, is the case to watch.

## 6. Asset sizes and the scene recipe

- **Scene:** 800 × 600 at 1× and 1600 × 1200 at 2×. Only the board's opening, x 75 to 725 and y 41 to 594 (650 × 553), is ever seen. Anything outside it lies under the frame.
- **From an official painting:**
  - The plugin stores only a **recipe** in the level file: the texture path, the mirror flag, the crop rectangle in source pixels, any edge padding, and the grade name.
  - At load it reads the texture from the game (as the Flight pane does), crops it, grades it on the CPU once (about 2 megapixels at 2×, roughly 20 to 40 ms) and caches the result.
  - Nothing of Square Enix's ships with Tsukimichi.
  - Overlays that are ours, such as the engraved route on the map, ship as a small transparent PNG.
  - The source is 1920 × 1080, so a 4:3 crop is at most 1440 × 1080. The 2× tier upscales 1.1 to 1.9 times, depending on the crop. Crops tighter than 1100 px wide look soft at 2×; the airship road's 840 px crop is the softest of the pilots and is noted as such.
- **From our own painting:** a JPEG at quality 88, 4:4:4. About 120 to 220 KB at 1× and 350 to 600 KB at 2×.
- **The level file:**
  - It keeps format version 1. The loader ignores unknown properties, so the pilots carry a `scene` object (the recipe, or the asset's file name) and a `design` note today without breaking anything.
  - A format v2 would make `scene` official and add `canBeGreen`.

## 7. The workflow (one level)

1. Choose the subject (section 1). Pull its source with `tools/texdump`, or paint it (`src/scene_*.py`).
2. Grade it and save `scenes/<id>.png` and `@2x` (`src/scene_official.py` or the painting script).
3. Read its coordinates on a 50-unit grid, then write the layout as a script (`src/level_*.py` with `src/layout.py`):
   - lines become bricks or dotted pegs;
   - silhouettes become outline rings;
   - the meaning becomes `canBeOrange`.
4. Check it: `layout.check()` before export; `mfcheck validate` (the shipped loader); `mfcheck sweep` (reach, stuck balls, bucket catches); `mfcheck play` (the greedy and random players).
5. Composite it (`src/composite.py`). The colours are the engine's own pick for a seed.
6. Supervision:
   - the realism supervisor reviews the scene and the composite;
   - the level critic checks that it reads as its subject, plays well and stays readable.
   - Fix and repeat until both approve.

## The six pilots

| Id | Name | Campaign | Technique | Source |
|---|---|---|---|---|
| base-p1 | The Airship Road | The Moon Road | a trail on a map | official world map painting "The Three Great Continents" (`-nowloading_base05`) |
| base-p2 | The Holy See | The Moon Road | terrain emphasis | official Coerthas painting (`-nowloading_base03`), mirrored |
| base-p3 | The Moonlit Post | The Moon Road | a creature in outline | our own painting (`scene_moogle.py`) |
| exp-p1 | The Domes of Sharlayan | The Far Shore | a landmark partly outlined | official Old Sharlayan painting (`-nowloading_base21`) |
| exp-p2 | The Ferry in the Stars | The Far Shore | a constellation | our own painting (`scene_constellation.py`) |
| exp-p3 | The Sea of Sorrows | The Far Shore | orbit (our own technique) | official Mare Lamentorum painting (`-nowloading_base25`), mirrored |

Each pilot's note on its subject and technique is in the docstring of its `src/level_*.py` and in `spec-rich.md`.

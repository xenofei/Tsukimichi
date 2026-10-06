# Moonfall level pipeline

One command turns a level's **layout source** and its **scene recipe** into a checked level file (format v2), a graded
and dressed scene, composites at 1x and 2x, and a report. It refuses any level that fails a check. The rules it
enforces are the approved method: `docs/design/v9/rich/level-method.md` (sections 1-7) and
`docs/design/v9/rich2/level-method.md` (section 8, the fuller-board F-rules), plus decision 21 (the greedy player
wins at least 5 of 48 seeded games) and the round-1 supervision decisions (below).

It renders with the approved design kit (`docs/design/v9/src`, `rich/src`, `rich2/src`), imported rather than copied,
so a new board looks exactly like the approved pilots. The two .NET helpers live here: `dotnet/mfcheck` (the shipped
loader and engine) and `dotnet/texdump` (reads textures from the game install).

## Commands

Run from the repo root (`py -3`; needs numpy and Pillow, .NET 10, and the game installed for `fetch`).

| Command | What it does |
|---|---|
| `py -3 tools/moonfall-levels/mfl.py fetch` | Dumps every game texture the recipes name, plus the chrome's UI art, the cards and the fonts, into `docs/design/v9/rich/.cache` (gitignored) |
| `py -3 tools/moonfall-levels/mfl.py selftest` | Runs every checker against its known-bad and known-good cases |
| `py -3 tools/moonfall-levels/mfl.py trace <scene or level-id>` | The tracing sheet (`docs/design/v9/levels/build/trace/`): the graded scene at 2x, a 50-unit grid, the opening, the launcher's swing, the bucket's lane, the recipe's features, and for a level its pieces (oranges ringed orange, never-green pegs barred, skipped points as red crosses) plus the pre-flight |
| `py -3 tools/moonfall-levels/mfl.py build <id>... \| --all [--keep2x id,...]` | The full pipeline (below); exit 1 if any level is refused |
| `py -3 tools/moonfall-levels/mfl.py stage <n>` | The stage's table from the reports: pieces, the rule's 48 games, the 864-game ramp, jewels; faults (`mflkit/stagecheck.py`) neighbours whose jewels are within 30 degrees in both hues, a level not at least 0.5 per 48 harder than the one before, a finale that is not the stage's hardest by 2.5 below its 4th level, and band ends more than 1.6 (two standard errors) harder than the stage's band |
| `py -3 tools/moonfall-levels/mfl.py stuck <id>` | Where balls come to rest on the first shots the stuck rule fires on |
| `py -3 tools/moonfall-levels/mfl.py dead <id>` | Where the first shots that touch nothing fly, so a piece can go in their lane |
| `py -3 tools/moonfall-levels/mfl.py ease <id> [tags] [--pick N] [--skip tags]` | A scratch copy with every (tagged) peg a candidate, 864 games: each place ranked by how often it is the orange left behind; `--pick` proposes the N most readily cleared places that keep the spread rule and stay above y 430 |
| `py -3 tools/moonfall-levels/mfl.py sources` | Writes `docs/design/v9/levels/sources.json`: every game file a scene reads, and which level uses it |

## What `build` does, in order

0. **Self-tests** (once per run; nothing builds if one fails). Each checker must fault its known-bad cases and pass its
   known-good ones. Pre-flight: overlapping pegs, a saddle (touching pegs too, at their real radii), a cup arc, a cup by
   geometry (two touching bricks sloping into one joint, a three-brick V, the feet of two crowns), a level deck, a peg
   in the wedge band over a brick, bricks in the bucket's lane, a mover passing a brick or another drift's mover, an
   orbit over a still peg's place, an orange mover out of reach, no green-able pegs, a row in the lane, a wall pinch,
   candidates crowded into one half. Framing: a peg under framing, a slab across the middle, a 60-unit rim run, a lamp
   beside a peg, lamps in a row, the approved posts/discs/holes. Readability: a bright ground and a dark one, one hue,
   two jewels 32 degrees apart (one jewel), two jewels far apart, orange on orange and on lapis (protan), a ghost disc
   and a clean board, a hue disc printed round each peg on a flat sky (isolated pegs, and a cluster with none
   isolated), an even dress, a region's edge through the pegs. Pre-flight, round 2: slides of different periods that
   collide in real time, co-moving slides overlapping or in a saddle, a deck at 6.5 degrees, one level brick of 28,
   long bricks across the lane. Stage: a good stage, neighbours with one palette, a level easier than the one before,
   a weak finale, band ends. Engine: the loader on a pilot and on a peg in the launcher's swing; play on a pilot and on
   oranges sealed in brick rings; the stuck gate on a pilot and on a board of flat decks; dead first shots on a pilot
   and on a chute; reach on a pilot and on a ceiling peg no ball can touch; cheap difficulty on a pilot and on low
   oranges that decide losses (one, and in share); the colours gate on a green dealt where it may not go.
1. **Pre-flight** (`mflkit/author.py`): bounds, launcher, bucket, overlaps, saddles (still pegs, and the movers of one
   drift group among themselves), cradles, notches, wall pinches, the wedge band (a peg 13-16 above a brick), level
   decks (line bricks under 10 degrees: chained over 30 units, or one alone of 20 or more), cups by geometry, movers'
   clearance along their paths (against movers of another period over real time, the periods' common cycle),
   every orange candidate (movers' paths and bricks too) in a direct flight's reach, the spread rule (10 per
   200 x 200), 28-35 candidates (the deal's 25 and 3-7 more, so the deal varies), 60-160 pieces, format v2's greens (8
   or more sure greens; every greenable peg, brick and mover in reach), the bucket's lane (pegs and bricks: at most 5,
   covering at most 120 units of its width).
2. **The shipped loader** (`mfcheck validate`, `MoonfallLevelLoader`).
3. **The sweep**: one first shot at every aim a quarter of a degree apart (681 aims); refuses if the stuck rule fires
   on 5% or more, or if any aim touches nothing (a dead first shot).
4. **Reach**: drops every piece the first sweep touches and sweeps again, a few rounds; refuses a piece no ball ever
   reaches.
5. **Play**: mfcheck's greedy player at the level's own number over 864 seeded games; the first 48 are the rule's
   (refuses below 5 wins) and all 864 give the ramp (per 48, about +-0.8). Holdouts are counted by piece (a mover by
   its home). Refuses cheap difficulty (`engine.cheap`): an orange at home at y 430 or lower left in 25% or more of
   lost games, or low oranges more than 1.5 times their share of the candidates among the oranges left.
6. **The scene**: graded from its recipe (cached in `build/scenes`); refuses a 99th-percentile luma over 0.465 (a
   painted moon may be exempted with `ceilingExempt`).
7. **The dress** (`mflkit/dress.py`) and the **framing rules** F2, F3a (and its pixel backstop), F3b, F3c, F3d, F5.
8. **Colours**: the engine's own deal at seed 1 (the engine reads `canBeGreen`); a green dealt to a never-green piece
   is refused as an engine fault.
9. **Composites** at 2x and 1x, and **readability**: F6 for every kind at its worst placement, the faces measured at
   each scale (1x and 0.8x), against the same board undressed (margin 0.20, drop at most 0.02); F7 two jewels by
   mean-hue distance (60 degrees or more, the second 15% or more of the coloured pixels); F9 protan orange separation
   over every candidate, movers at four moments of their cycle and orange-able bricks along their length (p10 0.12, or
   0.101, the lowest approved pilot); the ghost check (on the cleared board with its baked veil, the disc round an
   isolated peg: median distance 0.066 or less, the pilots' highest); the print check (the dress's own change,
   dressed minus undressed, with no pieces and no veil: 5-12 units outside every still peg against 20-36 out, the
   median over eight sectors round the peg, so an edge crossing it does not count; the board's median at most 0.010
   and its 90th percentile at most 0.026; never vacuous: with fewer than three pegs measured, the board as a whole).
10. If everything passed: `docs/design/v9/levels/json/<id>.json`, `composites/<id>.png` (and `@2x` if asked), our
    paintings as JPEG in `scenes/assets/`. The report, pass or fail, goes to `report/<id>.json`, with the ramp.

## Authoring a level

1. **Pick the subject and source** (method section 1). For a game painting, find it with texdump (`loading` lists
   every zone's loading image; the `_hr1` versions are 3840 x 2160, so crops of 1100 px and up stay sharp at 2x;
   `maps` lists the area maps). Add it to a recipe and run `fetch`.
2. **Write the scene recipe** `docs/design/v9/levels/scenes/<scene>.json` and run `trace <scene>`. Adjust the crop until
   the subject sits in the opening, clear of the launcher's swing and the bucket's lane, with its upper features below
   the curve no direct flight rises above (a ball launched sideways drops about 1.6e-3 x dx squared below the pivot).
3. **Draw the features** over the sheet, in board units, into the recipe's `features` (or, for our own painting, in the
   painter's `FEATURES`, which the recipe merges, so the painting and the layout read one set of coordinates).
4. **Write the layout** `docs/design/v9/levels/layouts/<id>.py`: a `LEVEL` dict (id, name, stage, number, scene,
   subject, technique) and `build(b)`. The helper (`mflkit/author.py`, `Board`):
   - `b.trace(feature, spacing, r, orange=..., green=..., offset=..., start=..., end_trim=...)`: dotted pegs along a
     polyline at legal spacing (default 2r + 14), nudged or skipped where a point would break a rule;
   - `b.outline(feature, offset=18)`: an even ring just outside a closed silhouette;
   - `b.stop(x, y, R, n, oranges=...)`: a ring of moons (a stop on a route, a lantern's head);
   - `b.bricks_along(feature)`, `b.arc_bricks(cx, cy, R, start, sweep, n)`: brick runs and crowns (concave down only);
   - `b.slide(x, y, dx, dy, period)`: a slide mover checked along its whole path (movers drifting together are
     checked against each other as still pegs); orbit movers via `b.peg(..., move=...)`;
   - `b.key(x, y, orange=True, green=False)`: mark the subject's features; `b.greens_in_reach()`: pegs no direct flight
     touches may never be green.
5. **Dress it** (the recipe's `dress`: palette, jewel bands and regions, shafts, glows, framing, silhouette, lights;
   see the docstring of `mflkit/dress.py`), `build`, read the report and the composite, adjust, repeat.
6. **Check the stage** with `stage <n>`.

## Decisions (round-1 supervision, the coordinator's calls)

- **The ramp**, measured over 864 games at each level's own number (432 at least): stage 1 runs from about 30 down to 21, stage 2 from
  about 27 down to 20, and each finale is its stage's hardest, at least 2.5 per 48 below its 4th level. Difficulty
  comes from the subject's places, never from low oranges in the bucket's approach (the cheap-difficulty gate).
- **Paintings**: never the same painting twice in a row, and a painting that belongs to a later stage's home stays
  there (stage 4, Ul'dah, keeps the Thanalan painting beyond 1-4's crop; stage 5 keeps the Merlthor chart and the east
  of the La Noscea painting). Our own paintings are fine; keep the campaign's share of game paintings near two thirds.
  Stages 1-2 hold 4 game paintings of 10, so stages 3-11 need about 73% (33 of 45) to reach two thirds over the
  campaign (game designer round 2, G4); stage 4 (Ul'dah) needs its own sources, since 1-4's crop takes the middle of
  the only Thanalan painting.
- **Bricks can be green**: the subject's crowns and key features are marked never green, and bricks and movers count
  in the greens' reach.

## Lessons

- A long, shallow deck of brick holds a resting ball (draw level decks dotted), and so does the flat apex of a
  two-brick crown (open it at the keystone, 16 or more between the bricks, or stand a moon over it). A peg 13-16 units
  above a sloped brick wedges balls.
- Oranges low on the board (below about y 440) or behind a moving ring are the ones the greedy player leaves.
- **Blue pegs in the open sky make a board harder**, often by 0.5-1 per 48 each: removing nine sky stars eased 2-2 by
  5.7, and the open-sea pegs and rhumb lines on 2-3 cost 3.6 and 7.5. Pieces that belong to a structure (a ring, a row,
  a run) cost little, and low arcs of brick can ease a board. So buy fullness with structures, not scatter, and tune
  the ramp last by moving one candidate or one blue peg and re-measuring: one peg can move it by 2-4.
- The 864-game ramp keeps +-0.8 of noise, and nearby layouts differ by more: compare variants side by side.
- Candidates set difficulty more than anything else: oranges at the board's edges and corners, and low ones, are the
  ones left. `ease --pick` proposes a starting set from the places a player clears most readily; mix it with the
  subject's own places, then tune the ramp with one blue peg (placed last, so the deal of the other candidates stays).
- Two jewels within about 60 degrees of hue count as one; neighbours need 30 degrees in at least one jewel.
- The jewel's quiet once took the region's colour out round each peg, and over a flat field (our own paintings above
  all) it printed a coin round every peg that stayed when the peg cleared, and a ghost check on isolated pegs missed
  it. The quiet is now spread low-frequency (`dress.QUIET_SIGMA`, 40 units or the recipe's outer reach): a cluster
  quietens its area as one calm band and a lone peg leaves only a faint wide wash. The print check measures what
  stays. Where a sky must hold one colour, paint it into our painting and keep the jewel band the same hue there.
- A sky darkened in the grade (`tone`) shrinks the veil's step.

## The scene recipe

```json
{
 "name": "uldah-gilded-dome",
 "source": {"kind": "game", "texture": "ui/loadingimage/-nowloading_base02_hr1.tex", "mirror": true,
            "crop": [800, 0, 2080, 1560], "pad": [0, 0], "erase": [[x, y, w, h]],
            "grade": {"gamma": 1.7, "exposure": 0.70, "ceiling": 0.36}, "skyDrop": 0.3, "glow": [-50, -60]},
 "masks": {"land": {"kind": "rim-fill", "threshold": 0.75, "grow": 5}},
 "tone": [{"mask": [["not-mask", "land", 12]], "mul": 0.80}],
 "overlays": [{"kind": "route", "points": [[x, y]], "color": "#D9BE82", "width": 2.0, "alpha": 0.55}],
 "veil": 0.44,
 "ceilingExempt": [[x, y, r]],
 "features": {"great dome": {"circle": [447, 330, 150], "from": 210, "sweep": 126}, "...": [[x, y]]},
 "dress": {"palette": {}, "jewel": {}, "shafts": [], "glows": [], "extras": [], "framing": [], "silhouette": {},
           "lights": []}
}
```

`source` is what the plugin runs at load (texture, mirror, crop in source px, erase boxes, the Medallion night grade's
parameters); for our own paintings it is `{"kind": "painting", "painter": "<name>"}` and the plugin ships the JPEG in
`scenes/assets/`. `masks` and `tone` are ours, applied after the grade (a map's unwalked desert receding). `features`
are authoring data, not needed at runtime. The level file names the scene by `scene` (format v2: a name, not a path).

The runtime's own scene-recipe format (`moonfall-scene` version 1: `docs/design/v9/scene-recipe.md` on main, files in
`Tsukimichi.Core/Moonfall/Levels/scenes/<name>.json`) has since landed on main. It covers the same ground under other
names (`palette` for `jewel`, `light` for shafts and glows, `"picture"` sources as PNG in
`Tsukimichi/assets/moonfall/scenes/`) and draws the veil per piece at play time, so the baked veil the ghost check
guards against never reaches the game. These recipes need a converter to that format before the levels ship: the
jewel's `regions` map onto `palette.regions` and its `quiet` onto a `near` mask term, but our `masks` (rim-fill),
`tone`, and the `poly`, `not-poly` and feature-`near` mask terms have no counterpart there yet.

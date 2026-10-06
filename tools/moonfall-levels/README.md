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
| `py -3 tools/moonfall-levels/mfl.py stage <n>` | The stage's table from the reports: pieces, the rule's 48 games, the held-out ramp, jewels; faults (`mflkit/stagecheck.py`) neighbours (the previous stage's last level too) whose jewel pairs are within 30 degrees in both hues either way round, a level not at least 0.5 per 48 harder than the one before, a finale that is not the stage's hardest by 2.5 below its 4th level, and band ends more than 1.2 (two standard errors) harder than the stage's band, or far easier |
| `py -3 tools/moonfall-levels/mfl.py stuck <id>` | Where balls come to rest on the first shots the stuck rule fires on |
| `py -3 tools/moonfall-levels/mfl.py dead <id>` | Where the first shots that touch nothing fly, so a piece can go in their lane |
| `py -3 tools/moonfall-levels/mfl.py ease <id> [tags] [--pick N] [--skip tags]` | A scratch copy with every (tagged) peg a candidate, 864 games: each place ranked by how often it is the orange left behind; `--pick` proposes the N most readily cleared places that keep the spread rule and stay above y 430 |
| `py -3 tools/moonfall-levels/mfl.py sources` | Writes `docs/design/v9/levels/sources.json`: every game file a scene reads, and which level uses it |
| `py -3 tools/moonfall-levels/mfl.py convert <id>... \| --all` | The converter (`mflkit/convert.py`): each level's runtime recipe (`Tsukimichi.Core/Moonfall/Levels/scenes/<scene>.json`), our painting undressed at 2x and the dress's plates (`Tsukimichi/assets/moonfall/scenes/`) |
| `py -3 tools/moonfall-levels/mfl.py convert <id>... \| --all --check` | The converter's gate: the game's bare scene (MoonfallRender `--scene-only`) against this pipeline's dressed scene; diffs in `build/convert/` |

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
   oranges that decide losses (one, and in share); the colours gate on a green dealt where it may not go. Round 3:
   a 12.8-unit slot between bricks, a chain of short level bricks, movers whose common cycle outlasts a game; the
   print check's ring and wide-disc shapes and the six approved pilots as known-good; a stage much easier than its
   band, one palette swapped round, the previous stage's last level. Round 4: a 15-unit slot; crossing slides of
   periods 10 and 10.04 (whose common cycle is 2510 s, not 10); round 2's per-peg coin put back on the real 2-2, 2-1
   and 1-4 (known-bad for the print check; 2-1 and 1-4 run where their game textures are fetched); a recipe asking
   for less quiet blur than 40 (refused); a second jewel only along the walls (F7). Round 5: UX's syn05 hue coin on the
   real 2-2 and 1-4 (known-bad for the print check's hue clause); the dress's structural guard (`dress.lint`) on
   real recipes: a NaN quietBlur, a `dist` term in a region and in a keep mask, regionQuiet 1.5, 2-3 with round 5's
   disc on its lead bird, a keep disc and a tone disc on a peg (all refused), and the ten as shipped (all pass); a
   16-unit slot between bricks of odd and even sample counts; a step of 0.8 on one block and of 0.6 over 6912 games
   (not resolved) against one of 0.95 (resolved). Round 6: the guard's bypasses of a centre rule, all on the real 2-3
   at its lead bird (round 5's disc nudged 20 units, a not-poly octagon, a keep box, a one-point `near` keep, a small
   glow, a tone box, a lens of four large discs: all refused); a second jewel only at the walls in one window (F7); a
   held-out step of 1.2 that pools to 0.7, and a finale gap of 2.7 over 6912 games (not resolved).
1. **Pre-flight** (`mflkit/author.py`): bounds, launcher, bucket, overlaps, saddles (still pegs, and the movers of one
   drift group among themselves), cradles, notches (3.5-17 between bricks), wall pinches, the wedge band (a peg 13-16 above a brick), level
   decks (line bricks under 10 degrees: chained over 30 units, or one alone of 20 or more), cups by geometry, movers'
   clearance along their paths (against movers of another period over real time, the common cycle of their exact
   periods, or every pair of phases when that cycle is longer than 240 s),
   every orange candidate (movers' paths and bricks too) in a direct flight's reach, the spread rule (10 per
   200 x 200), 28-35 candidates (the deal's 25 and 3-7 more, so the deal varies), 60-160 pieces, format v2's greens (8
   or more sure greens; every greenable peg, brick and mover in reach), the bucket's lane (pegs and bricks: at most 5,
   covering at most 120 units of its width).
2. **The shipped loader** (`mfcheck validate`, `MoonfallLevelLoader`).
3. **The sweep**: one first shot at every aim a quarter of a degree apart (681 aims); refuses if the stuck rule fires
   on 5% or more, or if any aim touches nothing (a dead first shot).
4. **Reach**: drops every piece the first sweep touches and sweeps again, a few rounds; refuses a piece no ball ever
   reaches.
5. **Play**: mfcheck's greedy player at the level's own number: seeds 1-48 are the rule's games (refuses below 5
   wins); the ramp is measured on 1728 held-out games, seeds 865-2592 (per 48, about +-0.55). Tune on seeds 1-864
   (`ease`, scratch variants) and never on the held-out block: a layout kept because it hit its target on fixed seeds
   carries their luck (about 0.8 per 48 easier on fresh seeds: game designer round 3, G10). Accept or reject a
   variant on the tuning seeds too: the held-out figure reports, it does not decide (critic round 4, N12; round 4's
   2-3 and 2-5 calls were made on it; the critic's third block of fresh seeds showed no bias from that, +0.09). The
   bands and steps apply to the held-out figure only: a level's tuning figure can read 1-2 per 48 easier or harder
   than its held-out one (1-1 at round 5: 31.9 against 30.7), so do not hold a tuning figure to a band's edge
   (critic round 5, N19). For a final pick between close variants, the 864 tuning games are too few (+-0.8): play a
   second tuning block of 3456 games, seeds 20001-23456 (`engine.play(path, number, 3456, first=20000)`), which no
   check and no reviewer block uses (round 6: 2-3 read 25.7 on seeds 1-864, 26.0 on that block and 27.6 held out). Holdouts are counted by piece (a mover by
   its home). Refuses cheap difficulty (`engine.cheap`): an orange at home at y 430 or lower left in 25% or more of
   lost games, or low oranges more than 1.5 times their share of the candidates among the oranges left. (A cap on the
   low share of the deal itself was tried and dropped: the approved pilot base-p2 has 35% of its candidates that low.)
6. **The scene**: graded from its recipe (cached in `build/scenes`); refuses a 99th-percentile luma over 0.465 (a
   painted moon may be exempted with `ceilingExempt`).
7. **The dress** (`mflkit/dress.py`), refused first if its structural guard (`dress.lint`) faults: the quiet's blur
   below 40 or not a number, `regionQuiet` outside 0-1, a `dist` term in a jewel region or keep mask; by coverage, not
   centre, a jewel region, keep mask or `tone` whose positional terms (`disc`, `x`, `y`, `poly`, `not-poly`, `near` and
   any product of them, drawn at 1x; `luma` and painting-derived masks are left out, since they follow the painting)
   single out a shape under 100 units across that covers a piece (the shape is the drawn mask over 0.5, or under 0.5
   for an inverted term, whichever is smaller); and a glow of radius under 100 that reaches a piece. A shape at a
   peg's scale over a peg is a coin, whatever it is for (rounds 5 and 6: 2-3's disc on its lead bird, and its
   bypasses). Then the **framing rules** F2, F3a (and its pixel backstop), F3b, F3c, F3d, F5.
8. **Colours**: the engine's own deal at seed 1 (the engine reads `canBeGreen`); a green dealt to a never-green piece
   is refused as an engine fault.
9. **Composites** at 2x and 1x, and **readability**: F6 for every kind at its worst placement, the faces measured at
   each scale (1x and 0.8x), against the same board undressed (margin 0.20, drop at most 0.02); F7 two jewels by
   mean-hue distance (60 degrees or more, the second 15% or more of the coloured pixels, and at most 1.5 times as
   much of it in the 50-unit strips along the walls as their share of the area: game designer round 4, G18; and at
   most 5 times in any 100-unit tall window, `second_at_walls_worst_window` [figure, the window's centre y]: round 4's
   wall strips read 5.7-6.5 there, and 1-2's unfaded green water 6.4 until round 6, UX m9); F9 protan orange separation
   over every candidate, movers at four moments of their cycle and orange-able bricks along their length (p10 0.12, or
   0.101, the lowest approved pilot); the ghost check (on the cleared board with its baked veil, the disc round an
   isolated peg: median distance 0.066 or less, the pilots' highest; this is the board in play); the print check
   (what stays when the pieces clear: the dress's own change, dressed minus undressed, with no pieces and no veil,
   round every peg (a mover at its home), 5-12 units out against 20-36 and 5-20 against 45-70, the median over eight
   sectors so an edge crossing a peg does not count, less 1.2 times the painting's own fine grain round the peg
   (a robust spread, so stars do not inflate it); the board's median at most 0.006 and its 90th percentile at most
   0.040; and the hue part alone (the a/b change less 1.2 times the painting's a/b grain: a lightness texture does not
   hide a shift of hue) at most 0.022 in median; never vacuous). Set between known cases, all in the self-test: the six
   approved pilots pass (median at most 0.003, p90 at most 0.032, hue median at most 0.0074) and the ten levels as
   built (p90 at most 0.034, hue median at most 0.0143, 1-1's broad quiet); round 2's per-peg quiet put back on the
   real 2-2, 2-1 and 1-4 fails (2-2 on its median, 0.0105; the textured 2-1 and 1-4 on p90, 0.045 and 0.074; 2-1 is
   the narrowest margin), and so does UX's syn05 hue coin (a/b +0.035 each round every peg) on all ten, by its hue
   median (0.029-0.046; 1-4 is the narrowest, 0.0295). Round 4 found its first form (an all-or-nothing grain rule on
   the plain spread) zeroed most pegs and passed that coin on 2-2 (critic M1, UX m5).
   **What the print check cannot see**, so the structural guard and the eye must: one peg singled out (a board's
   median and p90 do not move for it, and the approved pilots' own worst single pegs reach 0.086 and 0.19, so no
   worst-peg limit can be set: round 5's disc on 2-3 measured 0.088); a coin round a minority of the pegs (the rule is
   a median: a syn05-strength coin round only the 28 candidates passes on 1-1, 2-2 and 2-3, and round the top 45% of
   pegs on 1-1, 1-4 and 2-3; a coin that marks the candidates is the worst kind, since it tells the player where the
   oranges may fall: critic N23, UX m7); `tone`, which is graded into the scene the dress is compared with; hue coins
   on 1-4 up to ΔE 0.05 in any direction, and weaker ones elsewhere (syn03, a/b +0.021 each, passes on 1-1, 1-4 and
   2-2); and lightness coins on textured game paintings (L -0.03 passes on 1-4 and 2-3, up to 0.05 on 1-4). The guard
   refuses the routes the dress has to such a print by their shape (the quiet's blur, `dist`, and any positional mask,
   tone or glow at a peg's scale over a piece); a print through a term the guard does not know (a new primitive, a
   painting-derived mask shaped like a coin) is caught by neither, so a new dress primitive needs a guard rule and a
   bypass self-test before it ships.
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

- **The ramp**, measured on held-out games at each level's own number (432 at least): stage 1 runs from about 30 down to 21, stage 2 from
  about 27 down to 20, and each finale is its stage's hardest, at least 2.5 per 48 below its 4th level. Difficulty
  comes from the subject's places, never from low oranges in the bucket's approach (the cheap-difficulty gate). A
  step of 0.5 cannot be resolved on one held-out block, so `stage` plays every level of the stage on 5184 more fresh
  seeds (the shipped files in `json/`) and judges every step and the finale gap on the pooled 6912 games by one
  standard error: the step less it at least 0.5, the gap less it at least 2.5 (game designer rounds 4-6, G19, G20 and
  G22; critic N22). It prints the pooled figures. Open steps by design rather than by re-measuring.
- **A finale takes its difficulty from the whole board, not its bottom** (game designer G11; the owner's answer,
  6 October 2026: recommendation taken). 2-5's reflected heads are candidates at their two upper moons only and the
  reflected arch at the U's second pair (y 486); six sky stars carry the other oranges. Lost games leaving an orange
  at y 430 or lower fell from about 80% (round 5) to about 55%. The corner stars are a fair pair since round 6 (the
  right one no longer sits behind a blue star on the launcher's line: critic N21, game designer G24); what carries the
  gap is recorded in `layouts/base-10.py`.
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
  two-brick crown (open it at the keystone, 17 or more between the bricks, or stand a moon over it; a wider slot lets
  more balls through and eases the board: 2-1's from 16.7 to about 19 eased it by 1.2 per 48). A peg 13-16 units
  above a sloped brick wedges balls.
- Oranges low on the board (below about y 440) or behind a moving ring are the ones the greedy player leaves.
- **Blue pegs in the open sky make a board harder**, often by 0.5-1 per 48 each: removing nine sky stars eased 2-2 by
  5.7, and the open-sea pegs and rhumb lines on 2-3 cost 3.6 and 7.5. Pieces that belong to a structure (a ring, a row,
  a run) cost little, and low arcs of brick can ease a board. So buy fullness with structures, not scatter, and tune
  the ramp last by moving one candidate or one blue peg and re-measuring: one peg can move it by 2-4.
- The held-out ramp keeps +-0.55 of noise, and nearby layouts differ by more: compare variants side by side on the
  tuning seeds, and expect the held-out figure to differ by up to 1 per 48 either way.
- Candidates set difficulty more than anything else: oranges at the board's edges and corners, and low ones, are the
  ones left. `ease --pick` proposes a starting set from the places a player clears most readily; mix it with the
  subject's own places, then tune the ramp with one blue peg (placed last, so the deal of the other candidates stays).
- Two jewels within about 60 degrees of hue count as one; neighbours need 30 degrees in at least one jewel.
- The jewel's quiet once took the region's colour out round each peg, and over a flat field (our own paintings above
  all) it printed a coin round every peg that stayed when the peg cleared. It is now drawn exactly as the runtime's
  `near` mask term draws it: the clearance blurred by `quietBlur` (40 units, the format's most, and pinned there:
  `dress.py` refuses less, since with less blur the quiet is a per-peg term again) before the smoothstep
  `quiet: [a, b]`, so a lone peg gets no coin and a cluster quietens as one band. With the blur first, a narrow
  `[22, 12]` barely reaches a lone peg, so F9 may need a wider `[40, 18]` to `[60, 25]`; widen it until F9 holds,
  then lift the second jewel's chroma or area until F7's share does. Where a sky must hold one colour, paint it into
  our painting and keep the jewel band the same hue there. Never settle F9 with a mask centred on a peg (round 5:
  2-3's disc round its lead bird printed a lilac coin there; `dress.lint` now refuses it): move the candidate, or
  shape a region by the painting at a scale of 100 units or more.
- With that quiet a region keeps its full colour wherever the layout is not, so a region running to the walls shows
  as coloured light leaking in at the sides and the cleared board keeps the layout's envelope (game designer round 4,
  G18). Fade every region within 60 units of the walls (`["x", 75, 135], ["x", 725, 665]` in its mask); F7 checks it.
  Where a region still shows the layout's outline (2-4's sea), let it take less of the quiet (`regionQuiet` 0.3
  rather than 0.6) at a lower chroma, and check F9.
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

**The converter** (`mfl.py convert`, levels runtime round). The runtime format grew this pipeline's parts (erase boxes, a
squeezed crop, `poly`, `line` and `land` mask terms, the palette's `spare`, `tone`, plates, `beamsOnly` shafts; see
`scene-recipe.md`, "From the level pipeline"). A game painting ships nothing of Square Enix's: the runtime cuts, erases and
grades it, and our dress over it ships as plates; our own painting ships undressed (no grain) as a 2x picture. The
palette, glows, the moving beams (the shafts' 15%), the small lights (lanterns flicker) and the recipe's `runtime` block
(`motion`, `fireflies`, `flicker`, `feverMoon`) stay runtime parts, so every level keeps ambient motion. The selftest holds
that the dress rebuilt from the plates equals the dress (exact) and catches the plates with the cover left out.

**The jewel's gamut** (found by the converter's gate; the owner's answer, runtime round 1). `dress2.jewel` clips each sRGB
channel of a colour pushed out of gamut, which moves its lightness; the runtime keeps the lightness and gives up chroma
(`MoonfallColor.ToSrgbKeepingLightness`, F1). The game's form is the pipeline's default (`dress.RUNTIME_GAMUT`), so the
reports, composites and stage checks describe what ships; `dress.clipped_gamut()` draws the round-6 approved form. The
gamut moved three second jewels most, and their recipes bring the approved look back within it: 1-2's water (hue 150,
not the teal 200 the kept gamut gave #30B888: `#6AAC5D` at 0.252 and a lightness tone of 1.2 on the water, since the
clip had raised its lightness), 2-1's sea mist (`#56AE6C`, tone 1.16: hue 154 against the approved 156, so F7 holds
against 1-5's 202; critic M1) and 2-3's base jewel (chroma 1.5, floor 0.05, region `#4FAF71`: mean chroma 0.066 and 77
degrees apart, UX m1). Round 2's per-peg coin on 2-1 fails the print check again in this form (p90 0.050).

The runtime's own scene-recipe format (`moonfall-scene` version 1: `docs/design/v9/scene-recipe.md` on main, files in
`Tsukimichi.Core/Moonfall/Levels/scenes/<name>.json`) has since landed on main. It covers the same ground under other
names (`palette` for `jewel`, `light` for shafts and glows, `"picture"` sources as PNG in
`Tsukimichi/assets/moonfall/scenes/`) and draws the veil per piece at play time, so the baked veil the ghost check
guards against never reaches the game. The converter (above) writes these recipes in that format: the
jewel's `regions` map onto `palette.regions`, and its quiet maps one to one onto a `near` mask term `[a, b]` with
`blur` = `quietBlur` (each region's `where` gets it with `invert` and `scale` = `regionQuiet`, 0.6 by default; the palette's `where` with `invert`
and `scale 0.5`): `dress.py` draws the quiet in exactly that form, so F9 and the print measured here are what the game
draws (UX round 3, G3; UX round 4 re-derived it from main's C# and matched the pipeline exactly at 1x and to
OKLab 0.014 at 2x, the residue being how the clearance is resampled near the foot). Our `masks` (rim-fill), `tone`,
the `poly`, `not-poly` and feature-`near` mask terms, and a `keepMask` made of several ramps inverted as a whole (1-5's
dome house) map onto the runtime's `land`, `tone`, `poly` (`not-poly` inverted), `line` and the palette's `spare` (a
union, inverted as a whole). The runtime refuses a negative
feather, so a `disc` with one (2-2's region, `["disc", 292, 128, 60, -20]`) converts to the same disc with a positive
feather and `invert`: 1 - Smooth(80, 40, d) = Smooth(40, 80, d), exact (UX round 6, n14). The converter's gate is a direct
comparison, not the print check: render each converted level with `tools/Tsukimichi.MoonfallRender` and compare it
with this pipeline's dressed scene pixel for pixel (OKLab distance, the maximum, or at least the 99.9th percentile, at
most about 0.02: critic rounds 4 and 5, M1 and N17; a 99th percentile would pass a local term mis-scaled at a peg's
scale, since dropping round 5's disc on 2-3 moved it by only 0.018, and the formula's own residue is 0.014 at most). The two formulas are meant to be identical, so any difference is the converter's fault, and the print check
and F9 measured here then stand for the game (once they are measured in the game's gamut: see "The jewel's gamut").
`mfl.py convert --all --check` runs it at 2x (max 0.04 and 99.9th percentile 0.015, both) and at 1x (99th percentile
0.035), and fails a level whose build dropped a moon, a mist band, a part or more than half its stars.

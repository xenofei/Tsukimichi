# Moonfall level pipeline

One command turns a level's **layout source** and its **scene recipe** into a checked level file (format v2), a graded
and dressed scene, composites at 1x and 2x, and a report. It refuses any level that fails a check. The rules it
enforces are the approved method: `docs/design/v9/rich/level-method.md` (sections 1-7) and
`docs/design/v9/rich2/level-method.md` (section 8, the fuller-board F-rules), plus decision 21 (the greedy player
wins at least 5 of 48 seeded games).

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
| `py -3 tools/moonfall-levels/mfl.py stage <n> [--ramp 144]` | The stage's table: pieces, greedy wins, the ramp over more games, jewels (neighbours must differ: F7), game paintings against ours |
| `py -3 tools/moonfall-levels/mfl.py stuck <id>` | Where balls come to rest on the first shots the stuck rule fires on |
| `py -3 tools/moonfall-levels/mfl.py sources` | Writes `docs/design/v9/levels/sources.json`: every game file a scene reads, and which level uses it |

## What `build` does, in order

0. **Self-tests** (once per run; nothing builds if one fails). Each checker must fault its known-bad cases and pass its
   known-good ones: overlapping pegs, a saddle, a cup arc, a cup in a brick run, no green-able pegs, a row in the
   bucket's lane, a wall pinch, candidates crowded into one half; framing over a peg, a slab across the middle, a
   60-unit rim run, a lamp beside a peg, lamps in a row, plus the approved posts/discs/holes cases; a bright ground and
   a dark one, one hue and two jewels, orange on orange and on lapis (protan); the loader on a pilot and on a peg in
   the launcher's swing; play on a pilot and on oranges sealed in brick rings.
1. **Pre-flight** (`mflkit/author.py`): bounds, launcher, bucket, overlaps, saddles, cradles, notches, wall pinches,
   every orange candidate in a direct flight's reach, the spread rule, 25-35 candidates, 60-160 pieces, no cups,
   format v2's greens (8 or more pegs that may be green and are never orange, every one in reach), the bucket's lane.
2. **The shipped loader** (`mfcheck validate`, `MoonfallLevelLoader`).
3. **The sweep**: one first shot at every aim; refuses if the stuck rule fires on 5% or more.
4. **Play**: mfcheck's greedy player over 48 seeded games at the level's own number; refuses below 5 wins.
5. **The scene**: graded from its recipe (cached in `build/scenes`); refuses a 99th-percentile luma over 0.465 (a
   painted moon may be exempted with `ceilingExempt`).
6. **The dress** (`mflkit/dress.py`) and the **framing rules** F2, F3a (and its pixel backstop), F3b, F3c, F3d, F5.
7. **Colours**: the engine's own deal; from level 3, the first seed whose greens land on pegs the file allows to be
   green (the shipped engine does not read `canBeGreen` yet).
8. **Composites** at 2x and 1x, and **readability**: F6 for every kind at its worst placement at 1x and 0.8x against
   the same board undressed (margin 0.20, drop at most 0.02), F7 two jewels, F9 protan orange separation.
9. If everything passed: `docs/design/v9/levels/json/<id>.json`, `composites/<id>.png` (and `@2x` if asked), our
   paintings as JPEG in `scenes/assets/`. The report, pass or fail, goes to `report/<id>.json`.

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
6. **Check the stage** with `stage <n> --ramp 144`.

Lessons from the first ten levels: a long, shallow deck of brick holds a resting ball (draw level decks dotted); a peg
13-16 units above a sloped brick wedges balls; oranges low on the board (below about y 460) or behind a moving ring are
the ones the greedy player leaves; adding blue pegs usually makes a board easier, not harder; two jewels within about
60 degrees of hue count as one.

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
are authoring data, not needed at runtime. The level file names the scene by `scene` (format v2: a name, not a path);
when the runtime scene-recipe format (`Tsukimichi.Core/Moonfall/Levels/scenes/<name>.json`) lands, these fields map
across one to one.

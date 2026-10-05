# Moonfall scene recipes (format `moonfall-scene`, version 1)

A scene recipe dresses a level's board: which painting it starts from, how that painting is night graded and coloured,
the light added to it, the silhouettes that frame the opening, the small lights, the veil, and what moves. The plugin
builds it at load, off the framework thread, from the player's own install (nothing of Square Enix's ships with the
plugin), and keeps the result in memory for the session. This page is for whoever writes levels: everything a recipe can
say, the ranges the loader accepts, and the rules the build enforces.

- Files: `Tsukimichi.Core/Moonfall/Levels/scenes/<name>.json`, embedded in Core. The file's name is the recipe's `name`.
- Reader: `Tsukimichi.Core/Moonfall/Art/MoonfallSceneRecipeLoader.cs` (`Parse`, `LoadBuiltIn`, `Pick`, `CheckSet`).
- Builder: `Tsukimichi.Core/Moonfall/Art/MoonfallSceneBuilder.cs`. Rules: `MoonfallFramingCheck.cs`, `MoonfallMotion.cs`.
- Design sources it ports: `rich/src/rich_lib.py` (night grade), `rich2/src/dress2.py` (jewel palette, light, framing,
  veil), `rich2/src/framecheck.py` (the fuller-board rules), `rich2/level-method.md` (F1 to F6).

## Which scene a level gets

`MoonfallSceneRecipeLoader.Pick`:

1. The level file names a `scene` and a recipe has that name: that recipe.
2. The level file names a `scene` with no recipe of that name: no recipe; the shipped picture of that name is drawn as
   before (the interim art).
3. The level file names no `scene`: the recipe whose `levels` lists the level's id, else the recipe marked
   `"default": true`, else none (the night sky).

So a new level either names its recipe in its own file (`"scene": "holy-see"`, level format version 2) or is listed by a
recipe. `CheckSet` (a test runs it) refuses two default recipes and a level listed by two recipes.

## The pipeline

The build runs in this order. Each step reads the result of the one before.

1. **Cut**: the painting is padded (`pad`, `padMode`), mirrored if asked, cropped (`crop`, 4:3) and resampled (Lanczos)
   to the board at the tier's scale: 1 px a unit (800 × 600) or 2 px a unit at the 2× tier.
2. **Grade** (`grade`): day for night in OKLab. Skipped for a picture already painted in the night's values, and for a
   fallback picture.
3. **Paint** (`paint`): light layers painted *under* the palette, so they take its colour (a moon, a route, a glow).
4. **Vignette** and **grain**.
5. **Palette** (`palette`): the jewel grade. It changes OKLab hue and chroma only; lightness is kept exactly (F1). A colour
   pushed outside sRGB gives up chroma, never lightness.
6. **Light** (`light`): layers added *over* the palette (shafts, glows, aurora, the compass rose). A shaft with
   `"moving": true` is baked at 85% and its other 15% becomes the two drifting beam layers.
7. **Framing** (`framing`): the silhouettes. Each element that would come within 6.5 units of a piece is dropped whole,
   and a hard clamp behind that keeps every framing pixel 6 units clear.
8. **Small lights** (`lights`), **fireflies**, **stars**: each placed 8 units clear of every piece or dropped.
9. **Check** (Debug builds and the tests): the fuller-board rules below, measured on the result.
10. **Veil** (`veil`): the scene recedes behind and round the layout, so the pieces read.
11. **Layers**: the scene over the opening, the margins' blurred backdrop (with the rails' enamel), the beams, the open
    sky for Fever, the framing in front of the moon, the mist tiles.

## Top level

| Field | Type | Default | Notes |
|---|---|---|---|
| `format` | string | required | `"moonfall-scene"` |
| `version` | int | required | 1. A newer version is refused (the level keeps its picture or the night sky) |
| `name` | scene name | required | Lower-case letters, digits and hyphens; must equal the file's name |
| `levels` | list of level ids | `[]` | Levels with no `scene` of their own that take this recipe (at most 64) |
| `default` | bool | `false` | The scene a level with no scene and no listing takes; one recipe at most |
| `source` | object | required | The painting (below) |
| `fallback` | scene name | none | A shipped picture drawn whole, ungraded, when the game texture is missing or changed |
| `grade` | object | none | The night grade (below); none for a picture painted at night |
| `paint` | list (≤ 16) | `[]` | Light layers under the palette |
| `vignette` | 0–0.6 | 0 | |
| `grain` | 0–0.05 | 0 | |
| `palette` | object | none | The jewel grade (below) |
| `light` | list (≤ 16) | `[]` | Light layers over the palette |
| `framing` | list (≤ 8 groups) | `[]` | Silhouettes (below) |
| `lights` | list (≤ 48) | `[]` | Small lights: lamps, windows, lanterns |
| `fireflies` | object | none | |
| `veil` | 0–0.6 | 0.30 | 0.20 on our own dark paintings, 0.40–0.44 on the official ones |
| `motion` | object | none | Dust, stars, mist |
| `chrome` | object | the Medallion's | The rails' enamel and the margins: `sky`, `deep`, `jewel1`, `jewel2` (`#RRGGBB`) |
| `feverMoon` | `[x, y, r]` | the `moon` layer | The moon Fever swells, when the painting has its own |

Every number is range-checked and every colour is `#RRGGBB`; a bad value refuses the whole recipe with a reason (the
level then keeps its picture or the night sky). Comments (`//`) and trailing commas are allowed; unknown fields are
ignored. A recipe is at most 64 KB.

## `source`

Exactly one of:

- `"game": "ui/loadingimage/<file>.tex"` or `"ui/map/<path>.tex"`: a texture in the player's install, read through
  Dalamud. Only loading-screen paintings and maps; lower-case, no parent steps (`..`), no empty segments.
- `"picture": "<scene name>"`: one of Moonfall's own paintings in `Tsukimichi/assets/moonfall/scenes/` (`<name>.png` and
  `<name>@2x.png`).

| Field | Type | Default | Notes |
|---|---|---|---|
| `mirror` | bool | false | Mirrored left to right (so the light comes from the upper left) |
| `pad` | `[top, left]` | `[0, 0]` | Pixels added before the crop (0–1024) |
| `padMode` | `"edge"` or `"reflect"` | `"edge"` | |
| `crop` | `[x, y, w, h]` | the whole | In the padded painting's pixels; at least 16 × 12 and 4:3 within 0.02 |

## `grade` (the night grade)

`kind`: `"night"` (the Medallion's, default), `"violet"` (The Far Shore's later hour) or `"none"`. Any of these override
the kind's defaults: `exposure` 0.1–2, `gamma` 0.5–3, `ceiling` 0.2–0.6, `knee` 0.05–0.5, `detail` 0–3, `chromaMid`
0–2, `chromaHigh` 0–2, `tint` colour, `tintK` 0–1, `baseHue` colour, `skyDrop` 0–0.9 (how far the sky sinks), `skyTop`
and `skyBottom` 0–1, `warmKeep` 0–1 (how much warm light survives), `form` 0–0.5 and `formRadius` 2–80 (the planes facing
the light), `bandK` 0–2 (the restored mid band).

## `palette` (the jewel grade, F1)

| Field | Range | Default | Notes |
|---|---|---|---|
| `bands` | `[[y, colour], …]` (≤ 8, y −200–800) | required | The hue down the board, interpolated by y |
| `valueHues` | `[[L, colour], …]` (L 0–1) | none | A hue by lightness, mixed with the bands by `mix` |
| `mix` | 0–1 | 0.5 | |
| `chroma` | 0–2 | 1 | Raises chroma: a floor plus 1.4 × the source's, times this |
| `floor` | 0–0.1 | 0.024 | |
| `keep` | 0–1 | 0.30 | How much of the source's own colour stays |
| `keepHigh` | 0–1 | 0.75 | Highlights stay cool moonstone |
| `where` | mask | everywhere | Where the palette applies |
| `regions` | list (≤ 6) | `[]` | A second jewel: `{ "hue", "chroma" 0–0.2, "weight" 0–1, "where": mask }` |

### Masks (`where`)

A list of at most 6 terms, multiplied together. Each term names one of:

| Term | Arguments | 1 where |
|---|---|---|
| `lum` | `[a, b]` | The graded scene's OKLab lightness runs from a to b (smoothstep; either order) |
| `y` | `[a, b]` | Board y runs from a to b |
| `x` | `[a, b]` | Board x runs from a to b |
| `disc` | `[x, y, r, feather]` | Inside the disc |
| `near` | `[a, b]` | Near the pieces: 1 within b units of a piece's edge, 0 beyond a |

Options on a term: `blur` (0–40 units, before the smoothstep, for `lum` and `near`), `invert` (bool), `scale` (0–1).

## `paint` and `light` layers

Each is an object with a `kind`:

| Kind | Fields (defaults) |
|---|---|
| `shafts` | `origin` [x, y] (−140, −220), `angles` (1–8, degrees), `widths` (one per angle, 1–200), `k` (0.07, at most **0.08**: F4), `colour` (#BFD2FF), `seed`, `reach` (900), `near` (150), `moving` (false). A moving shaft belongs in `light` only |
| `glow` | `x`, `y`, `r`, `colour` (required), `k` (0.05, ≤ 0.5) |
| `moonGlow` | `x` (−50), `y` (−60), `rCore` (330), `rWide` (900), `kCore` (0.12), `kWide` (0.05), `colour` (#B9C8F0) |
| `moon` | `x` (150), `y` (100), `r` (28, 4–120), `seed`. One moon at most across paint and light; Fever swells it |
| `aurora` | `y` (300), `k` (0.16, ≤ 0.4) |
| `nebula` | `k` (0.42, ≤ 0.8), `seed` |
| `compassRose` | `x` (138), `y` (112), `r` (58, 10–160) |
| `neatline` | `inset` (2, 0.5–12) |
| `route` | `points` (2–256 [x, y]), `smooth` (8), `colour` (#D9BE82), `width` (2.2), `dash` (5), `gap` (4.5), `alpha` (0.6) |

## `framing` (silhouettes)

A list of at most 8 groups. A group draws its shapes as one silhouette: `body` (#05060E), `inner` (#0C1230) and
`innerK` (0.5) for the faint inner light, `rim` (#9EB4FF), `rimK` (0.5) and `rimWidth` (1.6) for the moonlit edge,
`snow` (a colour, optional), `seed`, and `shapes` (at most 48):

| Kind | Fields (defaults) |
|---|---|
| `frond` | `style` (laurel, oak, fir, fern, willow), `x`, `y`, `length` (120), `angle` (degrees), `droop` (0.4), `leaf` (16), `leaves` (12, ≤ 240), `seed`, `width` (1.8), `twigs` (0) |
| `trunk` | `x`, `y0`, `y1`, `w0` (12), `w1` (18), `lean`, `seed` |
| `pines` | `trees`: up to 16 `[x, baseY, height, width]`, `seed` |
| `outcrop` | `ridge`: 2–32 [x, y], `seed`, `rough` (10) |
| `rock` | `x`, `y`, `r` (15), `seed` |
| `crystal` | `x`, `y`, `h` (40), `w` (6), `tilt` (radians, ±1.5), `body`, `lit` |
| `rope` | `from` [x, y], `to` [x, y], `sag` (20), `width` (1.6, at most 2: a rope is exempt from F3c only while thin) |

Keep framing to the corners and the walls: the open middle must stay open (F2).

## `lights`, `fireflies`

- `lights`: `{ "x", "y", "size" (1), "colour" (#FFC86E), "core" (1.4), "k" (0.8), "halo" (5), "haloK" (0.25),
  "flicker" (false) }`. A light that would come within 8 units of a piece is dropped. `flicker` makes it a lantern
  (±10%, steady under Reduce motion).
- `fireflies`: `{ "count" (≤ 40), "seed", "region": [x0, y0, x1, y1], "colour" }`. Each rests where its whole wander
  (9 × 4 units, a 6 s loop), core and halo, keeps 8 units clear of every piece and 110 units from the launcher's pivot;
  those that cannot are not placed.

## `motion`

| Field | Range | Notes |
|---|---|---|
| `dust` | 0–120 | Motes drifting in the moving beams; needs a moving shaft. Drawn only 8 units clear of every piece |
| `stars` | 0–120 | Twinkles on the scene's own bright points in `starRegion` (default 75, 41, 725, 330) |
| `mist` | ≤ 3 layers | `{ "y": [y0, y1], "speed" (4, 1–10 units/s), "alpha" (0.07, ≤ 0.2), "cell" (120), "seed", "colour" }`. A periodic tile that wraps at its width; a band that crosses a piece within 2.5 units is not made |

**The motion budget:** dust, stars and fireflies together at most **120** a board.

**What moves, and where:** nothing moves within 2.5 units of a piece (the beams' moving share is masked off every piece
and every mover's whole path), particles keep 8. Under Reduce motion, or Plain, nothing moves at all; Simple (the Quiet
flair) keeps the beams and the halos and stills the rest.

## The fuller-board rules (enforced at build)

Measured on every build in Debug builds and by the tests for every shipped recipe at both tiers
(`MoonfallFramingCheck.Check`). A Debug build that breaks one logs it and asserts.

| Rule | Limit |
|---|---|
| F1 | The palette keeps OKLab L per pixel (by construction) |
| F2 | Framing covers at most 12% of the opening, under 0.2% of its open middle, and nothing within 100 units of the launcher's pivot |
| F3a | Framing (coverage over 0.5) keeps 6 units from every piece's edge, movers along their whole path; and no pixel within 6 units of a piece is darkened 0.06 or more by the dress |
| F3b | The rim light's longest connected run is at most 30 units |
| F3c | No straight outline run (posts, slabs: 36 units within 0.75); thin ropes are exempt |
| F3d | No peg-sized disc or hole (10–26 units across) in the framing |
| F4 | A shaft adds at most 0.08 |
| F5 | Small lights keep 8 units from every piece |

## Fallbacks

- The game texture is missing or changed: the `fallback` picture is drawn whole and ungraded, with the recipe's dress;
  no fallback: the night sky. Logged once.
- The recipe is refused: the level keeps its picture (or the night sky); the reason is logged at load.
- Nothing here can crash the plugin: every read, grade and build runs off the framework thread and fails to a fallback.

## Checking a recipe

```
dotnet test Tsukimichi.sln -c Release --filter "FullyQualifiedName~MoonfallScene"      # loader, the rules at 2x
dotnet test Tsukimichi.sln -c Release --filter "FullyQualifiedName~MoonfallRuntimeArt"  # 1x, motion, budget
```

Set `TSUKIMICHI_GAME_PATH` to the install's `sqpack` folder so a `game` source is built from the real painting (without
it the tests build from the `fallback`). To look at the board, the offline renderer draws it with the plugin's own code:

```
dotnet build tools/Tsukimichi.MoonfallRender -c Release
dotnet tools/Tsukimichi.MoonfallRender/bin/Release/net10.0-windows/Tsukimichi.MoonfallRender.dll out.png --level base-02 --size 1280x800 --moment hud
```

(`--moment hud|power|fever|tally`, `--marks`, `--reduce-motion`, `--no-game-art`.)

## Example

```json
{
  "format": "moonfall-scene",
  "version": 1,
  "name": "holy-see",
  "levels": ["base-02"],
  "source": { "game": "ui/loadingimage/-nowloading_base03.tex", "mirror": true, "crop": [430, 60, 1293, 970] },
  "grade": { "kind": "night", "skyDrop": 0.3 },
  "palette": { "bands": [[0, "#2B4FB0"], [600, "#1D3A8A"]], "chroma": 0.9, "floor": 0.03 },
  "light": [
    { "kind": "shafts", "origin": [-60, -160], "angles": [50, 58, 67, 75], "widths": [30, 22, 34, 20], "k": 0.06, "moving": true }
  ],
  "framing": [
    { "body": "#05060E", "rim": "#9EB4FF", "snow": "#DDE6FF",
      "shapes": [ { "kind": "frond", "style": "fir", "x": 70, "y": 40, "length": 160, "angle": 25, "leaves": 40 } ] }
  ],
  "veil": 0.42,
  "motion": { "dust": 30 }
}
```

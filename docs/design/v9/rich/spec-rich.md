# Moonfall, the rich pass: identity, screens, level scenes

Status: design for the owner's review, 5 October 2026. It answers the owner's brief of that day: "It needs more detail and passion in it, as it looks somewhat plain. I want this to have beautiful designs in every part of the main interface, and also the game art (levels). For all the levels, there needs to be a design that builds around it or emphasizes it (gives it meaning)."

It builds on the approved v9 art (`../spec-moonfall.md`) and keeps everything the owner liked: the moon pegs, the eleven characters, the lantern boat, the Medallion identity and the night palette. Style frame A's crescent-cradle bucket, which the owner voted down, is replaced (see "Buckets").

Two independent supervisors checked every image and every level until both approved. Their rounds are recorded verbatim in `supervisor/` (see "Supervision record").

## What is here

| Path | What it is |
|---|---|
| `screens/<screen>-1280.png`, `-640.png` | The eight screens at 1280 × 800 and at the 640 × 480 minimum: `title`, `map`, `characters`, `levels`, `hud`, `fever`, `tally`, `pause` |
| `screens/buckets.png` | The two campaigns' buckets: the new lantern cart and the approved lantern boat |
| `scenes/<id>-<name>.png`, `@2x` | The six pilot scenes, 800 × 600 and 1600 × 1200 |
| `composites/<id>.png`, `@2x` | Each pilot as the player sees it: scene, pegs, frame and HUD |
| `levels/<id>.json` | The six pilot levels, in the engine's format (version 1) |
| `level-method.md` | How every level becomes an illustrated scene whose layout draws it |
| `src/` | Everything that makes the images and the levels; `py -3 <script>.py` from `src/` |
| `tools/texdump` | Reads official textures from the player's own install into `.cache/` (gitignored) |
| `tools/mfcheck` | Validates level files with the shipped loader and plays them with the shipped engine |
| `supervisor/` | Every supervision round, verbatim, with the designer's responses |

Rebuild everything: `dotnet build tools/texdump -c Release` and run it (see `src/rich_lib.py`). Then build `tools/mfcheck`, and from `src/` run, in order:
1. the scene scripts: `scene_*.py`;
2. the level scripts: `level_*.py`;
3. `composite.py <id> <scene> <cart|boat> <seed>`;
4. `screens.py`;
5. `buckets.py`.

## The identity, enriched

The Medallion stays: the lapis night, brass and gilt, moonstone, cream type, and one light from the upper left, `L = (-0.424, -0.424, 0.80)`, on every part. What changes is the depth and the craft of each surface (`src/frame_rich.py`, `src/ui_kit.py`).

- **Guilloche enamel.** The rails and every panel are translucent lapis enamel fired over an engraved ground of fine waved lines, as on a watch dial or a presentation medallion. Each panel shows the engraving through the glaze, catches a soft sheen toward the light, and has slight mottling, so the frame has a surface at every size instead of flat paint. The multiplier dial's face is a sunburst guilloche, and locked plates are a rose-engine ring.
- **Pearl beading.** The board's walls are a row of small brass beads on a brass fillet. The ball turns at them, so they are structural, not decoration.
- **Rosettes and moonstones.** Brass rosettes with moonstone cabochons mark the board's corners and the window's. The moonstones are lit cabochons with their blue sheen toward the light.
- **Fluted pilasters.** The side rails' lower halves are fluted brass pilasters with capitals and bases.
- **Type.** MOONFALL is set in display capitals in gilt, with a dark lower lip, over a gilt rule with lozenge ends. Titles are a serif, and labels and numbers a humanist sans.
  - The mocks use Perpetua Titling, Georgia and Segoe UI as stand-ins.
  - Whether to bundle a display face is open question 7.
- **No noise glyphs.** Every ornament is either structure (a bead is a wall, a bezel holds glass, a pilaster holds the rail) or a sign with meaning (a lit orange moon means "won", the moonstone pips count turns).

## The screens

All screens share the one light, the enamel, the brass, the type, and these components:
- **the panel:** enamel over guilloche, with a brass rim, an engraved inner rule and a soft shadow below-right;
- **the button:** an enamel plate with a brass rim. A focused button glows with moonlight; a locked one is dimmed brass;
- **the tab;**
- **the gilt rule;**
- **the cameo badge:** a moonstone cameo in an oval brass bezel with a bead ring.

At 640 × 480 every screen keeps its structure, with fewer words. No label is smaller than 9 px, and no number is smaller than 10 px.

| Screen | Layout (1280 × 800) | At 640 × 480 | Assets |
|---|---|---|---|
| **Title** | Sohm Al under the moon behind everything; a darkened left column holds the logotype, its line ("a peg game under Menphina's moon"), the four modes (each with a status line), then Characters and Options; a Continue card at the lower right shows the next level's scene, its best score and its companion's cameo | The logotype centred, four modes stacked, Characters and Options; no Continue card | The official Dravania painting (`-nowloading_base07`), night graded at runtime; the moon painted where its light comes from |
| **Adventure map** | The world map as a moonlit chart under the header (Back, the two campaign tabs, progress). The Moon Road's eleven stages are cameo stops on it: done (with a lit orange moon), here (a moonlight ring), open, and locked (dimmed). A dashed gilt road joins them, solid where walked. A stage panel at the upper right: the companion's cameo, the stage's name, its five levels with best scores, and Play | The same chart and stops, smaller; the stage panel shrinks to the cameo, the name and Play | The official world map texture (`ui/map/world/01/world01_m.tex`), graded as a chart; The Far Shore's tab carries on past the map's edge (Sharlayan, the open sea, the moon) |
| **Characters** | Eleven cameo cards in rows of four, four and three. Locked companions are dimmed with "meet at stage N". A detail panel on the right: a large cameo, name, race and role, the personality line in italic, the power with its duration, what it does, and "Play with <name>" | The same grid smaller; a narrow detail panel with name, power and description | 11 cameos (see "The characters as cameos") |
| **Level select** | The stage's cameo and name over the chart; five level tiles, each the level's own board (scene and pegs), its name, best score, a lit moon and "aced" when aced, the ace score when open, and an engraved sealed plate when not reached; a selection panel below with the board, the level's one-line idea, its companion and Play | Tiles in rows of three and two; Play at the lower right | Each level's thumbnail is its composite, generated at runtime |
| **In game (HUD)** | The 800 × 600 board scaled to fit and centred; the margins are rail enamel with a rosette each. The board's top rail holds the level cartouche (stage roundel and name), the launcher's escutcheon and the recessed score window. The left rail holds the glass ball tube (caged, graduated, with acorn finials) and the count plate. The right rail holds the multiplier dial (sunburst face, gilt arc in a groove, knurled bezel), the oranges-left count and the power medallion (the carrier's cameo under glass, the power's name on a plate, moonstone pips for turns left) | The board fills the window exactly | `frame_rich.py`; shown aiming with Super Guide |
| **Fever** | Every peg left lit; FULL MOON in the display capitals over a gilt rule on a soft dark band behind the pegs; the line "Every moon left is worth more. Pick your cup."; the five brass cups along the foot | Same | The approved v9 cups; the banner type |
| **Tally** | The board dimmed; a panel with the level's name and campaign line, the score rows (shots, the Full Moon cup, balls left, style shots), the total in gilt, the ace plate (a lit orange moon, "Aced", the ace score, "new best"), the companion's cameo, and Replay, Map and "Next: 7-5" | Same rows, smaller | |
| **Pause** | The board dimmed; a panel: PAUSED, the level's state line, Resume (focused), Restart level ("hold to restart: the level's score is lost", the owner's safety rule for destructive clicks), Options, and Leave to the map; quick settings (Reduce motion, Decoration, Sound); the note that Moonfall pauses itself in combat, duties and cutscenes | Same, without the note | |

### The characters as cameos

The owner asked for the eleven characters as finished portraits, not silhouettes. Each is a **moonstone cameo** (`src/cameo.py`, `src/cameo_cast.py`): a portrait carved in relief, standing proud of a lapis ground, in a brass bezel. A cameo is the Medallion identity itself (a medallion with a face on it), and it reads at every size the game needs, from the 220-unit detail panel down to the HUD's 50-unit medallion.

Each one is built as a height field: bust, head in profile, hair carved in strands, eye socket and lid, ear, nose wing, lips' line, jaw, and each one's own props:
- Pipiru's hood and star globe;
- Kaede's horn, scales and chakram;
- Marcia's third eye, rivets and spanner;
- Haldbrand's beard, fur collar and gunblade;
- Gajavati's trunk, fanned ear and festival fan;
- Ysolde's long ear and moonlight ring;
- Ottilie's veil, moonflower and crook;
- Gyobo's whiskers and drum;
- Aldous's hat and staff;
- Ione's spectacles and nouliths;
- Kupsa's pom-pom, wing and strap.

Each is lit by the one light: moonstone's body colour, a cool subsurface in the shadows, a blue sheen toward the light, a polished highlight, and a cast shadow on the ground toward the lower right. The cast is unchanged; whether the cameo style is right is open question 4.

## Buckets

The owner voted down style frame A's crescent cradle. **Proposal: the lantern cart for The Moon Road and the lantern boat for The Far Shore** (`screens/buckets.png`). They are one craft and one light:
- **The cart:** a deep open box of dark planks with brass corner straps, two spoked wheels on the pale moon road, and the same paper lantern on a curved rear post. The lantern warms the post, the near strap and the plank's end, and lays a pool on the road.
- **The boat:** the approved bucket B, unchanged.

**The lantern among the pegs.** The lantern rises to y 529, above the bucket's rim, and sweeps the whole width, so in play it passes among the lowest pegs. These rules come from realism round 3:
- The lantern draws in front of the pegs.
- Any peg within about 30 units of it takes a faint warm light on the side that faces it, falling off with distance.
- The stills and composites show each bucket at a moment in its sweep where the lantern is clear of the pegs.
- Layouts keep the lowest 40 units sparse (base-p3 dropped a row for this).

The engine sizes are the same for both: 131 across the rails, a 104 mouth, the rim at y 573. Only the drawing changes (the road strip y 586–594 replaces the water strip). If the owner prefers one bucket for both, the boat works on every board. On a board with no water in its scene (base campaign), the boat sits on its own strip of water; exp-p3's scene now carries a still pool so its water belongs.

## The level scenes

The method is in `level-method.md`.

### Every level is a scene

Every level is a scene whose peg and brick layout draws its subject. There are two kinds of scene:
- **Official paintings from the player's install.** These are the regional loading-screen paintings and the world map, night graded by `rich_lib.night_lab`. Nothing of Square Enix's ships: the level file carries a recipe (texture path, mirror, crop, padding, grade), and the plugin reads and grades the texture at load, as the Flight pane already reads loading images.
- **Our own paintings,** made in code. These are for subjects no official painting has, such as a creature or a constellation.

### The Medallion night grade, version 2

The grade (`rich_lib.night_lab`), after both realism rounds, works like this:
1. It works in OKLab. Lightness is lowered on a curve.
2. A wide base layer (48 units) is compressed under a sloped soft knee, and the detail is added back.
3. A soft roll-off holds the ceiling. The 2–12 px band it would flatten is then restored at full strength (realism round 3), so cloud seas and planets keep their modelling.
4. A form light gives pale painted shapes, such as domes, relief from their silhouettes, lit from the upper left. It acts only on shapes under about 40 units, never on a large pale field.
5. Chroma is kept, with mid-tones scaled more than highlights. Yellow and green (hue 45–195°) are cut to 0.3. Hue is pulled toward lapis in the shadows and moonstone in the highlights; The Far Shore uses violet-moonstone.
6. The result: the 99th-percentile luma is 0.45–0.46 on every painting. With the veil (0.40–0.44 on the official paintings), a peg's lit face (0.6–0.7) stands above the scene behind it by at least 0.20, measured round every peg on every composite.

### The six pilots

| Id | Name | Technique | Subject and how the layout draws it | Pieces, candidates |
|---|---|---|---|---|
| base-p1 | The Airship Road | a trail on a map | The official map of Aldenard, with the airship's route engraved in gilt. Small moons dot the route; each city (Limsa Lominsa, Ul'dah, Mor Dhona, Ishgard, Gridania, Ala Mhigo) is a ring of four that the route runs through, and the rings carry the oranges, so clearing the level is visiting every stop. The compass rose, the ships' lights and the land fill the rest | 84, 26 |
| base-p2 | The Holy See | terrain emphasis | Ishgard above the sea of clouds (mirrored so its light comes from the upper left). A run of bricks rides the mountain crest, with a moon over each summit; brick humps ride the cloud tops; arches sit in the bridge's arches; the cathedral is drawn by its spire tips, its flanks and its rose window | 89, 26 |
| base-p3 | The Moonlit Post | a creature in outline | Our painting of a moogle courier flying over the Black Shroud by night, backlit by the moon. An even ring of moons follows its whole silhouette 18 units out (the ring is computed from the painting's own masks); the pom-pom, ears, wing tips, letter and feet carry the oranges; the forest's crowns are a wavy treeline with cottage lamps | 85, 26 |
| exp-p1 | The Domes of Sharlayan | a landmark partly outlined | Old Sharlayan's harbour. Curved bricks lie on the domes' crowns, in two courses on the great dome, with drums dotted down and a moon on each finial; the statue is an even dotted outline from hood to train; the water from her urn is a column of pegs; the ships are a row along their hulls. The painting finishes each shape | 67, 29 |
| exp-p2 | The Ferry in the Stars | a constellation | Our painting of the Lantern Ferry, the constellation the Far Shore's sailors steer by, over the open sea, with the Milky Way. A moon sits on every star of the figure (the lantern's star largest) and smaller moons dot the atlas lines between them; the field stars thicken along the Milky Way, and glints on the sea ride the swell | 102, 35 |
| exp-p3 | The Sea of Sorrows | orbit (our own) | Mare Lamentorum, with the world hanging in the black (mirrored). A ring of 26 moons orbits the world, slowly (an engine `orbit` mover, 26 s a turn, 42 px/s); the drifting rocks are loose rows; the tower's rib and sphere are traced; the world's storms sit on its night side, clear of the lit face | 72 (26 movers), 28 |

All six:
- pass the shipped loader;
- pass the strict pre-flight: every candidate is in a first flight's reach, the oranges are spread, and there are no notches, cradles, saddles or wall pinches;
- play within the shipped levels' range by mfcheck's greedy player (48 games each; the shipped levels win 5–13).

### The level-file format (proposal for version 2)

Version 1 ignores unknown properties, so the pilots carry two extra objects without breaking anything:
- `scene`: the recipe, or the file of our own painting, and the veil strength;
- `design`: notes.

Version 2 would:
- make `scene` official;
- add `canBeGreen` (open question 3).

## Runtime assets and sizes

| Asset | Count | Size and format | Notes |
|---|---|---|---|
| Scene from an official painting | per level | nothing shipped; the recipe is about 120 bytes in the level file | Graded once on load (about 2 megapixels at 2×, roughly 20–40 ms on the CPU) and cached for the session |
| Scene of our own | per level | JPEG q88 4:4:4: about 70 KB at 1×, 260–320 KB at 2× | Ship the 2× and reduce for 1× and 0.8×, so one file per level |
| Overlays on official scenes (an engraved route) | per level that has one | PNG with alpha, 10–40 KB | |
| The veil | per level | none | Computed from the layout at load |
| Level thumbnails | per level | none | Rendered from the scene and the layout at load, 220 × 187 |
| Cameos | 11 | PNG with alpha, 400 × 480, about 60–90 KB each | Baked from `src/cameo_cast.py`; drawn at every size from 44 to 240 units |
| Frame and HUD | 1 set | 9-slice PNGs (rails, beads, rosettes, pilasters, cartouche, escutcheon, score window, ball tube, dial, medallion), about 0.6 MB at 2× | Or drawn procedurally: every recipe is analytic |
| Panels, buttons, tabs | 1 set | 9-slice PNGs, about 0.3 MB | |
| Buckets | 2 | cart and boat sprites; the lantern light and reflection are drawn each frame | |
| Title and map backgrounds | 2 | nothing shipped (official textures, graded at load) | |
| Fonts | 1 display face, if the owner agrees | about 60 KB | Open question 7 |

### The texture budget per level (proposal)

- **On disk:**
  - At most 350 KB per level for a level of our own (its 2× JPEG and any overlay).
  - About 0.1 KB for an official-painting level (the recipe).
  - The proposed mix: about 40 of the 115 levels are our own paintings (creatures, constellations, interiors no loading screen shows) and 75 are official paintings. That ships about **12 MB** of scenes.
  - At q82 (to be checked by the realism supervisor) the 2× is about 200 KB, for about 8 MB.
- **In memory:**
  - One scene texture at a time: 1600 × 1200 RGBA, 7.3 MiB at 2×, 1.8 MiB at 1×.
  - A second during the cross-fade between levels.
  - Thumbnails, 11 cameos and the frame: under 6 MiB together.
  - The peak is about **21 MiB** at 2×.
- **Load time:** under 60 ms per level on the CPU (decode or grade, plus the veil).

## Sources

- **Official FINAL FANTASY XIV paintings (© SQUARE ENIX),** read from the player's own game install (patch 2026.09.15) by `tools/texdump`. Nothing is redistributed: the renders here are derived and graded for review only, and the plugin would read the textures at runtime.
  - `ui/loadingimage/-nowloading_base03.tex`: Coerthas and Ishgard above the sea of clouds (base-p2).
  - `ui/loadingimage/-nowloading_base05.tex`: the world map "The Three Great Continents" (base-p1).
  - `ui/loadingimage/-nowloading_base07.tex`: Dravania, Sohm Al (the title screen).
  - `ui/loadingimage/-nowloading_base21.tex`: Old Sharlayan (exp-p1).
  - `ui/loadingimage/-nowloading_base25.tex`: Mare Lamentorum (exp-p3).
  - `ui/map/world/01/world01_m.tex`: the world map (the Adventure map and the level select).
  - Reviewed but not used: the battle-talk portraits (`ui/icon/073000`), which carry their banner frame, and the adventurer-plate backgrounds (`ui/icon/190000`).
- **The official FINAL FANTASY XIV Fan Kit,** reviewed for references: https://na.finalfantasyxiv.com/lodestone/special/fankit/desktop_wallpaper/2_0/ through `/7_0/` (97 wallpapers). None was used: nearly all carry the logo or text, and the install's loading paintings cover the same places without them.
- **Our own work:** the moogle and constellation scenes, the cameos, the frame and the UI kit are painted in code. Nothing is traced; the map's road is drawn over the official map.
- **Nothing from Peggle:** no layouts, art, characters or boards. The techniques (outline, trail, terrain, constellation) are the general ones the owner named.

## Supervision record

Two independent agents judged everything.

- **The realism supervisor** checked light, shadow, reflection, colour and materials, from the rendered PNGs only. Its rounds are `supervisor/realism-round-N.md`. In summary:
  - Round 1: no asset approved. The main finding was a night grade that flattened highlights into khaki slabs.
  - Round 2: six of twelve approved. The Sharlayan domes were still flat; there were yellow-green casts; the Mare Lamentorum pool and sphere needed fixing.
  - Round 3 (the 21 assets: 6 scenes, 6 composites, the 8 screens and the bucket sheet): 13 of 21 approved. Problems: the form light flattened the planet and the cloud sea; a grey title moon; cameos that read as embossed silhouettes; an impossible Super Guide path; a lantern touching pegs. (This round's report reached the designer as the coordinator's relay.)
  - Round 4: 18 of 21 approved. Problems: the planet's lit half out of focus; flat cameo necks and busts; a glow under Pipiru's chin; the HUD's lantern touching a peg.
  - Round 5: 20 of 21 approved. Pipiru's hood folds read as scratches.
  - Round 6: the hood re-carved in broad folds. The final verdict is below.
- **The level-design critic** checked readability as the subject, play (with the shipped engine) and readability over the scene. Its rounds are `supervisor/level-critic-round-N.md`. In summary:
  - Round 1: two of six approved. Unreachable top-row oranges, a ridge cup and a dome notch.
  - Round 2: five of six approved. base-p1's file and its script disagreed, and the pre-flight did not block an export.
  - Round 3: **all six approved**.
  - Round 4: all six approved after base-p3 dropped a row for the lantern's lane; one Minor (base-p3's last-orange treetop), taken.
  - Round 5: all six approved again on the final regraded composites (readability re-measured).

### Final verdicts

- **Level-design critic:** OVERALL APPROVE, all six pilot levels (rounds 3, 4 and 5).
- **Realism supervisor:** 20 of 21 approved in round 5 (all scenes, all composites, and the title, map, levels, hud, fever, tally, pause and buckets screens). The characters screen awaits its round-6 verdict on Pipiru's hood; see `supervisor/realism-round-6.md` once it is recorded.

## Open questions for the owner

1. **Buckets.** The lantern cart for The Moon Road and the lantern boat for The Far Shore, or the boat for both?
2. **Official paintings as level scenes.** Should about two thirds of the levels be built on the game's own loading-screen paintings, read and night graded from the player's install at load, with nothing shipped? The alternative is our own paintings for every level (about 30 MB more, and much more painting).
3. **Format v2.** Add `canBeGreen`, so a designer can keep greens off a figure's eye or a constellation's star? And make `scene` an official part of the level file?
4. **The cameos.** Are moonstone cameos (carved portraits) the right finished look for the eleven characters? The alternative is painted portraits in the night palette.
5. **The engine (from the level critic): being fixed separately.** At the bucket's right extreme its outer rim reaches past the right wall (401.5 + 260 + 65.5 = 727, past the wall at 724.5). A ball falling there chatters between the wall and the rim, up to 64 bounces before it is lost, on the shipped base-04 too. The coordinator has the engine fix in hand; no engine code was changed here. This is for your information, not a decision.
6. **Names.** Stage 4 is "The Shroud by Night", and its five levels are the three base pilots plus "Bentbranch at Dusk" and "The Twelveswood". These are placeholders for the mocks; they need your approval or new names.
7. **A display face.** Bundle one open-licence display serif for MOONFALL and the banners (about 60 KB), or keep Dalamud's fonts with the logotype as a baked image?
8. **The Far Shore's end.** It closes on the moon itself (Mare Lamentorum). Is that the right end?
9. **The acceptance rule for levels.** Should mfcheck's greedy player at or above the shipped levels' lowest win rate (5 of 48) become a rule for every level, alongside the pre-flight?

## Not verified

- How any of this looks in the game, at the plugin's real window scales and in Dalamud's renderer.
- The runtime grade's speed in C#.
- The JPEG recipe of our own scenes as shipped.
- Purples and greens dealt by seeds other than those used for the composites.

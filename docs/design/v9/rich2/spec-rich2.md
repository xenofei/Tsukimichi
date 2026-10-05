# Moonfall, rich pass 2: FFXIV's own art, real characters, motion and colour

Status: design for the owner's review, 5 October 2026. It answers the owner's verdict on the approved rich pass: "Everything looks okay so far. I think the pictures for the characters look ugly, and it still looks a bit plain overall."

The owner then chose two things:
- **The characters:** the eleven power carriers become real FFXIV characters, shown with the game's own portrait art from the player's install.
- **Richness:** all four levers:
  - FFXIV's own UI art;
  - ambient motion;
  - fuller boards;
  - bolder colour.

This pass raises the approved baseline (`../rich/`) and keeps what passed:
- the official paintings, graded at load;
- the six pilot layouts, peg for peg;
- the lantern cart and boat;
- the emissive moon;
- the launcher;
- the readability rule.

The supervisors are a game designer, a game UX/UI specialist and the level-design critic. On 5 October 2026 the owner replaced the realism supervisor with the first two for Moonfall. Every round is in `supervisor/` (see "Supervision record").

## What is here

| Path | What it is |
|---|---|
| `screens/<screen>-1280.png`, `-640.png` | The nine screens: `title`, `map`, `characters`, `levels`, `hud`, `power` (a power firing), `fever`, `tally`, `pause` |
| `screens/pegmarks.png` | The colour-blind assist: peg marks on, and the same board as a deuteranope sees it |
| `characters/lineup.png` | The eleven companions face up, with their powers |
| `characters.md` | The character mapping: power, character, card, reason, spoiler note, alternates |
| `scenes/<id>-<name>.png`, `@2x` | The six pilot scenes, re-dressed (800 × 600 and 1600 × 1200) |
| `composites/<id>.png`, `@2x` | Each pilot as the player sees it |
| `motion/title.mp4`, `play.png`, `fever.png` | Animated previews: true-colour APNG, 6 s seamless loops at 8 fps, with three still frames each |
| `level-method.md` | The level-scene method, with the fuller-board rules (section 8) |
| `sources.json` | Every game file the renders read, with what it is for (written by the scripts) |
| `src/` | Everything that makes the images; run `py -3 <script>.py` from `src/` |
| `supervisor/` | Every round, verbatim; the designer's responses; the measurements (`framecheck.json`, `readcheck.json`) |

### Rebuild

1. **The tools.** Run `dotnet build ../rich/tools/texdump -c Release`, and build `../rich/tools/mfcheck` the same way.
2. **The cache.** Fill `../rich/.cache` (gitignored) with texdump:
   - `dump` the loading images and the world map;
   - `icon .cache/portraits <ids>` for the cards;
   - `dumplist .cache/uld <list>` for the UI textures (the list is the `ui/uld` keys of `sources.json`);
   - `raw .cache/font <fdt>...` and `dump .cache/font common/font/font1.tex` to `font7.tex` for the fonts.
3. **The renders.** From `src/`, run in order:
   1. `dress2.py` (scenes, refused if a framing rule fails);
   2. `composite2.py`;
   3. `screens2.py` (every screen, the line-up and the peg marks; refused if any text at 640 is below the floor);
   4. `motion2.py`;
   5. `readcheck.py` (readability and colour, refused if a board fails).

texdump has four commands added in this pass:
- `cards` (the Triple Triad cards);
- `trust` (the Trust members);
- `uldscan` (the textures a UI layout names);
- `raw` (a file's bytes).

## 1. FFXIV's own UI art

Every frame, panel, button, ring and ornament is a texture from the game's own UI. They are read from the install at runtime, in their `_hr1` versions, drawn at half size as the game draws them. Nothing is redrawn; the grade only recolours.

| Moonfall part | Game texture (`ui/uld/…_hr1.tex`) | Where the game uses it |
|---|---|---|
| Every window's gilt frame: the triple-rule band (one seamless image, mitred), the vine corners above (their cut stems faded), the banner-and-reed corners below; the crest over title rules; the shelf rule across each side rail. On the board the band lies wholly outside the walls, with a dark 4-unit fillet between the wall and the gilt and a 2.5-unit dark reveal inside, so a peg at a wall sits on dark | `Journal_Frame` | The quest journal |
| Window ground (the enamel) | `Journal_Detail` (its grain, high-passed and mirror-tiled, coloured as enamel) | The quest journal |
| Buttons and plates (the gilt pill, assembled whole so there are no seams), switches | `LovmPalette` | Lord of Verminion (Gold Saucer) |
| Portrait rings; the multiplier dial (the lattice ring with crest points). The textured backing plates are cut away | `LovmPalette` (two rings) | Lord of Verminion |
| Tabs | `TabButtonA` | Every window's tabs |
| Card and open-tile selection glow, tinted in the companion's colour | `TripleTriadCardSelect` | Triple Triad |
| Card back (a companion not yet met); sealed level tiles (darkened) | `TripleTriadBattle` | Triple Triad |
| Laurel and ribbon: FULL MOON, LEVEL CLEAR and the power ribbon, split to frame the words, joined by one ribbon; the same ribbon, small and without laurel, for style shots | `TripleTriadResultCrown` | Triple Triad results |
| Launcher crest (gilt wings with turquoise inlay); Brass Wings on the cart; the ball tube's finials | `PVPRankEmblem3` | The PvP rank emblem |
| Turns left: one gem per turn the power lasts, lit in the carrier's colour (the scholar gauge's own setting for three; a slim gilt bar for five) | `JobHudSCH0` | The scholar's job gauge |

**How they were found.** texdump `uldscan` read every UI layout the game names: 1,529 textures. A gold-ornament ranking and contact sheets picked these. The atlas rectangles are `ATLAS` in `src/r2lib.py`.

**The grade, "gild".** It keeps the game's lightness exactly and pulls the hue toward the Medallion's warm gilt (OKLab hue about 78°). The journal's pale brass and the Gold Saucer's yellow gold become one gilt. The game's grey grounds become enamel through a ramp in the screen's or the level's palette.

**Type.** The type is the game's own faces, read from `common/font` for the mocks. At runtime Dalamud provides them as game fonts (`GameFontFamily`), so **nothing is bundled**. This answers the rich pass's open question 7 (bundle a display serif?).

| Face | Used for |
|---|---|
| Jupiter | Titles, MOONFALL, names, banners, primary buttons |
| AXIS | The game's window sans: labels, body text, secondary buttons, as FFXIV sets them |
| TrumpGothic | Score, counts, stage numbers, ACED |

- The "×" and "·" are always set in AXIS; TrumpGothic drew them wrongly in round 1.
- A glyph a face lacks stops the build. It is never a silent fallback.

**Text floors (checked).** At the 640 × 480 minimum:
- a label's cap height is at least 7 px, about a 9.7 px font;
- a number's cap height is at least 8 px.

`screens2.py` measures every string on every 640 screen, including the board chrome at 0.8 ×, and refuses to finish if one falls short. Labels that cannot meet the floor at 640 are left out there, not shrunk: SCORE, BALLS, ORANGES and the power's name on the rail. Secondary text with meaning keeps at least 4.5:1 contrast on its ground.

## 2. The colour system

The Medallion stays: the lapis night, gilt, moonstone, cream type, and the one light from the upper left. Rich pass 2 adds jewel tones and warm gold, by these rules.

1. **Lightness is never changed by colour.** Every jewel grade works in OKLab, keeps L exactly, and changes only hue and chroma. The values the level critic measured stay as they were.
2. **Two jewels per board, checked (F7, exclusive hue windows since round 3).**
   - Each scene has a base jewel down the board, plus a second jewel pushed into its large non-peg regions (sky, sea, cloud sea, far ground) at chroma 0.06–0.10.
   - `readcheck.py` bins the coloured pixels by hue in 30° bins. The first jewel is the window of three bins round the fullest bin. The second is the fullest window at least 60° away, counting only bins outside the first window. It must hold at least 15% of the coloured pixels, and the mean chroma must be at least 0.06.
   - Orange pegs keep their colour separation for a protanope: the 10th percentile, over the orange pegs, of the a/b distance from peg core to the ground round it is at least 0.12, or no worse than the approved board's less 0.02.
3. **Warm gold belongs to the chrome and to lights:** frames, rings, type, lanterns, lamps and fireflies. On the board, warm hues stay small, so an orange peg is always the most saturated warm thing near it.
4. **Each companion has an accent colour.**
   - It appears in their portrait's glow, their power's name, the turns-left gems, the selection glow, the power's effect and the Super Guide line.
   - The eleven accents are at least 25° apart in OKLab hue and kept off the chrome's gilt (60–100°), so each power has a colour you learn.
5. **Purple is watched.** Every kind's worst placement is measured (section 5).

| Palette | Base and jewels (measured second jewel, share) | Warm |
|---|---|---|
| Title and characters | Amethyst sky, sapphire shadows, silver snow | Gilt chrome, the moon's halo |
| Chart (map, level select) | Sapphire lands, teal seas | The gilt road |
| base-p1 The Airship Road | Sapphire lands, teal seas (165°, 25%) | The desk lamp's pool and beams |
| base-p2 The Holy See | Glacier blue, a rose cloud sea (315°, 25%) | Pale moonbeams |
| base-p3 The Moonlit Post | Amethyst sky, a teal-emerald Twelveswood, quietened round the pegs (195°, 17%) | Fireflies, the moogle's red pom-pom |
| exp-p1 The Domes of Sharlayan | Sapphire sky, turquoise harbour (165°, 23%), gilt on the domes' crowns | Quay lamps, the balustrade's lantern |
| exp-p2 The Ferry in the Stars | Violet night, an aquamarine aurora and sea (195°, 24%) | The ferry's stern lantern |
| exp-p3 The Sea of Sorrows | Amethyst space, a magenta and teal nebula; the world's own ocean blue, a little richer (225°, 20%) | Violet crystal |

**Companion accents** (OKLab hue):

| Companion | Colour | Hue |
|---|---|---|
| Tataru | rose | 355° |
| Raubahn | flame | 27° |
| Cid | copper | 52° |
| Kan-E-Senna | leaf | 118° |
| Merlwyb | sea-green | 148° |
| Louisoix | jade | 176° |
| Minfilia | aquamarine | 200° |
| the moogle | sky | 228° |
| the twins | sapphire | 258° |
| Urianger | star-violet | 286° |
| Y'shtola | amethyst | 316° |

## 3. The characters

The full mapping, the reasons, the spoiler notes and the alternates are in `characters.md`. In short:

| Stage | Power | Character |
|---|---|---|
| 1 | Super Guide | Minfilia |
| 2 | Multiball | Alphinaud & Alisaie |
| 3 | Brass Wings | Cid Garlond |
| 4 | Lunar Burst | Raubahn |
| 5 | Flippers | Merlwyb |
| 6 | Moon Gate | Urianger |
| 7 | Moonbloom | Kan-E-Senna |
| 8 | Moon-Viewing Draw | Tataru |
| 9 | Fireball | Y'shtola |
| 10 | Sage's Path | Louisoix |
| The Far Shore | Storm Post | Moogle courier |

All eleven are A Realm Reborn characters, shown with their **Triple Triad card art** (`ui/icon/087000/0870NN_hr1.tex`), which is their A Realm Reborn look.

**Each stage is themed to its companion's home** (round 2):

| Stage | Name |
|---|---|
| 1 | The Waking Sands |
| 2 | Vesper Bay |
| 3 | The Night Skyway |
| 4 | The Sunlit Steps (Ul'dah) |
| 5 | Harbour Lights (Limsa Lominsa) |
| 6 | The Silent Stars |
| 7 | The Shroud by Night |
| 8 | The Market Lanterns |
| 9 | Mor Dhona's Glass |
| 10 | Silvertear by Night |
| 11 | Your Pick |

The mocks' pilots belong to stage 3: Cid's airship carries the road over Eorzea by night, so its levels are seen from the air.

**Framing uses the plugin's own crop rules** (`Tsukimichi.Core/Portraits`):
- **Round portraits** (the HUD, map stops, the power row) use the card family's face box `[28, 23, 135]` (`giver_portraits.json`, `crops.TripleTriadCard`). The moogle uses its own box, `[14, 14, 180]`, which keeps its pom-pom and wings in the ring so it never reads as a cat.
- **The hero image** is the art inside the card's border (`ArtBounds`), in the journal's gilt frame.
- **The grid** shows the cards themselves.

**The grade is light.** Shadows lean a little toward the night's blue; highlights and skin are kept.

**The spoiler shield.**
- A companion the player's story has not introduced is the card back, captioned "Not yet met", with the power still named. The rule is the shield's own NPC rule, so it follows the player's progress and setting.
- A companion met in the story but not reached in Moonfall is the card dimmed, with "stage N".
- The mocks show one state throughout (`src/r2state.py`). The player is on The Moon Road, stage 3, with Cid. Stages 1 and 2 are won. The twins are face down, because Alisaie is not yet met.

**The power moment** (`screens/power-*.png`), when a green peg is hit:
- **At 1280 and wider:** the carrier's card slides in beside the board, in the window's margin and the journal's frame, for 1.2 s. Their power is named in their colour.
- **At 640, where there is no margin:** the moment stays in the chrome. Nothing covers the opening while the ball is live:
  - the power's name rides a laurelled ribbon along the top rail;
  - the rail portrait and its gems pulse in the carrier's colour.
- **The effect is light.** A white-gold flash and ring at the green, only its outer edge in the carrier's colour, never a filled disc of a peg's hue. For Brass Wings, the PvP emblem's gilt wings spread the cart.
- **The turns-left gems** show the power's length: five for Brass Wings, three for Super Guide and Flippers, one for the rest.

Under Reduce motion the card fades in and out without sliding. A style shot is the FULL MOON ribbon at a small size, without laurel, in open sky, with its bonus: LONG SHOT, +25,000.

## 4. The screens and their motion

Every screen is built from the parts above, from the one progress state. Motion is ambient: nothing in the layout moves.

| Screen | What it shows | Ambient motion | Under Reduce motion |
|---|---|---|---|
| **Title** | Sohm Al jewel-graded. MOONFALL in gilt Jupiter over the journal's crest rule. The modes as gilt pills. The **Continue card is the default focus**: the next board, Cid's portrait and power, and "Continue 3-3" | Moondust drifts up and right (5–9 px/s). Stars twinkle (±35%, 2 and 3 s). Two mist layers cross the ground (4 and 9 px/s: parallax). The moon's halo breathes (±6%, 6 s). One glint crosses the logotype (0.8 s). All are masked out of the logotype, the subtitle, the buttons and the card. `motion/title.mp4` shows the whole title | Still; no glint |
| **Adventure map** | The world map in sapphire and teal. Stops are companion portraits in lattice rings:<br>• won: a lit orange moon;<br>• here: a glow in the carrier's colour;<br>• not reached: drained, the ring dimmed, a padlock (at both sizes);<br>• not met: the card back;<br>• stage 11, your pick: its own sign, a gilt four-point star.<br>Each stage number sits on its own plate. A legend. A focus tooltip explains the face-down stop | The "here" glow breathes (±15%, 3 s). The road's dashes drift forward along the unwalked part (6 px/s) | Still |
| **Characters** | Eleven Triple Triad cards (face up, dimmed, or face down). The selection glow is in the companion's colour. The detail window holds:<br>• the hero art, name and role;<br>• a line in their voice;<br>• their stage and its home;<br>• the power with its portrait and duration;<br>• the power at work, cut from a real board;<br>• levels won together;<br>• Play with | The selection glow pulses (±15%, 3 s). Moondust as on the title. The hero glow breathes (±10%, 6 s) | Still |
| **Level select** | Board thumbnails in gilt frames. The open tile has the selection glow in the carrier's colour. Sealed tiles are the card back, darkened and drained, with a padlock. A selection window holds Play. At 640: one row of five tiles and a selection strip | The open tile's glow breathes (±15%, 3 s) | Still |
| **In game (HUD)** | The chrome from game art. The margins carry the level's scene, blurred. Each rail is divided into its instruments by the journal's rule. The dial has a true ×. The power's name sits on a dark plate in the carrier's colour | The level's motion (section 5): the beams' break-up drifting (light through moving leaves, a 5-unit circle every 6 s) and breathing (±15%), dust in the beams, the level's lights (fireflies, lamps, glints, aurora), the lantern flicker on the cart's lantern (±10%). The active carrier's portrait glows (±10%, 3 s). One glint along the top rail per 6 s. `motion/play.mp4` | Still; the lantern steady |
| **A power firing** | See section 3 | The card slides in (0.25 s) and out (0.25 s). The accent ring at the green expands once | The card fades; no slide |
| **Fever** | A lighting change:<br>• the board's moon swells and brightens behind the leaves;<br>• the sky lifts toward the carrier's colour;<br>• moondust bursts once from the last orange.<br>FULL MOON on one ribbon, framed by Triple Triad's laurel. Once Fever has landed, the ribbon's plate fades to 35% so the last ball stays in view; the lettering stays. The cups lit from within, the centre brightest, with their values at the floor | The arrival plays once over 1.5 s. Then the cups' light breathes (±15%, 3 s), and one glint crosses the laurel. `motion/fever.mp4` (it loops for review) | The lit state at once; no burst, no glint |
| **Tally** | LEVEL CLEAR in gilt on the ribbon, as large as the level's name. Rows in Jupiter, numbers in TrumpGothic, the total in gilt. An ACED callout: a lit moon, ACED and NEW BEST in gilt. Cid's portrait. The buttons | The total counts up (gameplay). One glint across the ribbon | The count shown at once |
| **Pause** | A taller journal window, below the top rail at 640. Resume is focused. Restart and Leave are garnet "danger" pills, each **held to confirm**; the mock shows Restart part-way through its hold, a lighter fill sweeping the pill. Options. Quick settings: Reduce motion and Peg marks as switches (On/Off, the track filled when on; both Off in the mocks' state), Decoration and Sound between gilt chevron buttons (20 × 20 at 1280, 16 × 16 at 640). The note about auto-pause | None: the game behind is paused, and so is its ambience | (the same) |

### The motion rules (every screen)

1. **What may move:** light (beams, halos, glints, flicker), particles (dust, fireflies, stars) and atmosphere (mist, aurora). Never a peg, a brick, a button, a label, a frame or a panel.
2. **Speeds:**
   - particles move at 4–10 px/s at 1×;
   - halos and beams breathe ±6–15% over 3–6 s;
   - twinkles are ±35% over 2–3 s;
   - a glint takes 0.8 s, at most one per 6 s per screen;
   - the lantern flickers ±10%.
3. **Clearance:**
   - Moving layers are masked out of all type and UI.
   - Particles, with their whole paths and halos, keep 8 units from every piece's edge (F5).
   - No ambient layer lifts a pixel within 8 units of a peg by more than 0.03 luma. `motion2.py` measures this on the play preview: 0.017 at most.
4. **Decoration levels** (Tsukimichi's three):
   - **Full:** everything above.
   - **Simple:** halos, beams and the lantern only.
   - **Off:** still.
5. **Reduce motion** always means still. It overrides Decoration. The power card fades instead of sliding.
6. **Budget:**
   - At most 120 particles, drawn as ImGui circles.
   - The beams and mist are each one textured quad, with their alpha changed per frame.
   - Target: under 0.3 ms a frame.

### The previews (`motion/`)

The previews are rendered from the stills by `src/motion2.py` in true colour, so the peg hues are kept exactly. Each is a 6 s loop at 8 fps.

| Preview | Size | File size |
|---|---|---|
| `title.png` | the whole title at 600 × 375 | 16.6 MB |
| `play.png` | base-p3 at 640 × 480 | 15.2 MB |
| `fever.png` | 640 × 480, with the build-up (the light first, then the banner) | 6.8 MB |

The ambient motion is subtle by design (the owner's taste, and F4 caps the beams at 0.08). In the play preview the beams' drifting gaps change a pixel by about 0.012 (95th percentile over the beam area). The fireflies and the dust are what the eye catches first.

**The colour-blind assist ("Peg marks").** The kinds differ by hue alone, so Options has an assist that engraves a mark on each kind's face:
- a crescent on orange;
- a leaf, pointed at both ends with its midrib, on green;
- a four-point star, 55% of the peg with a light rim, on purple;
- blue stays plain.

The marks are signs with meaning, not noise. See `screens/pegmarks.png`, which shows the same board as a deuteranope sees it.

## 5. The six pilots, re-dressed

Pegs, bricks and layouts are exactly the approved files (`../rich/levels`, unchanged). The composites use the engine's own colours (seed 1, level 5).

| Id | Palette | Depth: framing and foreground | Light event |
|---|---|---|---|
| base-p1 The Airship Road | Sapphire lands, teal seas (the cooler chart) | The chart's own compass rose, engraved in gilt light in the open top-left corner (it replaced round 2's quill, which read as a fishbone). The chart's neat-line on the frame's own edge, kept out of the launcher's span | The lamp's warm pool and three broad beams from the upper left |
| base-p2 The Holy See | Glacier blue, a rose cloud sea | Snow-laden fir boughs from both top corners (we stand among Coerthas's firs); firs in the lower corners, shortened until they clear every peg | Four moonbeams from the upper left |
| base-p3 The Moonlit Post | Amethyst sky, a teal-emerald wood, its colour quietened round the pegs | Two oaks at the walls, mostly beyond them; oak leaves hanging from both top corners, in front of the moon; ferns in the lower corners | Five beams through the canopy; fireflies spread over the wood (at most three in any 40-unit band of height, 30 or more apart, none in the bucket's lane) |
| exp-p1 The Domes of Sharlayan | Sapphire sky, turquoise harbour, gilt crowns | Laurel boughs from both top corners; the scholars' balustrade on a curved terrace in the lower right, beyond the pegs, its lamp on the end newel by the wall (round 2's straight lamp column is gone) | Three shafts across the domes; the quay's lamps; the newel's lantern |
| exp-p2 The Ferry in the Stars | Violet night, an aquamarine sea | Aboard the ferry: the furled sail's festoons along the yard above the board; one line of rigging through empty sky; the stern lantern on its post in the lower left | An aurora of aquamarine, green and violet over the sea |
| exp-p3 The Sea of Sorrows | Amethyst space; the world in its own colours, its ocean a little richer | Lunar outcrops in both lower corners, settled until they clear every peg; irregular drifting rocks in the top corners; faceted crystal spires where they clear | A magenta and teal nebula in the empty black; earthlight on the world's limb |

**The framing checks** (`src/framecheck.py`, run by `dress2.py` on every board, which refuses to write a board that fails; the checker runs its own self-test of synthetic shapes first: straight posts 4–60 units wide, a slab at 45°, a peg-sized disc and hole must fail, a wavy stem and an ellipse must pass). Results are in `supervisor/framecheck.json`.

| Rule | Requirement | The six pilots |
|---|---|---|
| F2 | Coverage at most 12% of the opening and under 0.2% of the open middle; none in the launcher's swing | 0–5.3%; under 0.01%; none |
| F3a | No framing within 6 units of any piece's edge, including every mover's whole path; and, as a backstop whatever drew it, no pixel 0.06 or more darker than the graded scene within 6 units of a piece | Smallest clearance 6.5–34.5 units. The foliage drops any leaf or needle that would come closer |
| F3b | No rim-lit run longer than 30 units | Longest 0–24.3 units |
| F3c | No straight outline run of 36 units or more, further than 15 units inside the opening (fitted per edge side; strokes 4 units wide or narrower are exempt) | None |
| F3d | No peg-sized disc, or hole | None |
| F5 | Small lights keep 8 units, plus their halo, from every piece | All |

**Readability (F6), colour (F7) and protan separation**, from `src/readcheck.py`; the report is `supervisor/readcheck.json`.

F6 sets each kind's face against every place it can be dealt, on the piece-free composite (scene, veil, chrome, bucket and spill), so the frame counts:
- orange at its candidates;
- blue, green and purple at every peg;
- movers along their whole path;
- bricks against the band round them.

The test runs at 1× and 0.8×. The rule is a margin of at least 0.20, and no kind more than 0.02 worse than on the approved board (measured the same way, with the approved chrome).

| Id | Worst margin, any kind, any placement, either scale (approved) | Largest drop | Second jewel (hue, share) | Protan orange separation (approved) |
|---|---|---|---|---|
| base-p1 | 0.228 (0.238) | 0.010 | 165°, 25% | 0.121 (0.116) |
| base-p2 | 0.240 (0.235) | none | 315°, 25% | 0.119 (0.110) |
| base-p3 | 0.289 (0.296) | 0.010 | 195°, 17% | 0.141 (0.144) |
| exp-p1 | 0.227 (0.230) | 0.003 | 165°, 23% | 0.101 (0.068) |
| exp-p2 | 0.302 (0.310) | 0.009 | 195°, 24% | 0.141 (0.010) |
| exp-p3 | 0.303 (0.298) | none | 225°, 20% | 0.135 (0.122) |

## 6. Runtime assets and the texture budget

**Read from the player's install at runtime (nothing shipped):**

| What | Files | Size in memory (RGBA) | When |
|---|---|---|---|
| UI art | `ui/uld/` `Journal_Frame`, `Journal_Detail`, `LovmPalette`, `TabButtonA`, `TripleTriadCardSelect`, `TripleTriadBattle`, `TripleTriadResultCrown`, `PVPRankEmblem3`, `JobHudSCH0` (`_hr1`) | About 8.6 MiB as read. The graded copies are about 4 MiB: only the used rectangles are graded and kept | Graded once when Moonfall opens, then cached |
| Companion cards | `ui/icon/087000/0870NN_hr1.tex` × 11 (208 × 256) | 2.2 MiB, graded | When Moonfall opens |
| Title backdrop | `ui/loadingimage/-nowloading_base07.tex` | 7.9 MiB to grade, then 3.9 MiB at 1280 × 800 | The title; released on leaving it |
| Map and level select | `ui/map/world/01/world01_m.tex` | 16 MiB to grade (2048²), then 3.9 MiB at window size | Released on leaving the map |
| Level scenes | The approved recipes (`../rich/spec-rich.md`) | As in the rich pass: 7.3 MiB per scene at 2× | Per level |
| Type | Jupiter, AXIS and TrumpGothic, through Dalamud's game fonts | Dalamud's font atlas | Always |

The plugin already uses this pattern: `BannerGrading.cs` and `PortraitGrading.cs` read a game texture, grade it on the CPU and upload it with `ITextureProvider.CreateFromRawAsync`.

**Shipped with Moonfall:**

| What | Size | Why |
|---|---|---|
| The re-dress recipe per level: palette and regions, beams, framing shapes with their parameters, small lights | About 1–2 KB of JSON in each level's `scene.dress` | The framing is analytic (fronds, trunks, firs, sailcloth, rigging, outcrops, crystals, rocks), drawn at load like the veil, and it checks its own clearance. Nothing is painted, and nothing of Square Enix's ships |
| Our own paintings (the moogle, the constellation and their like) | As in the rich pass: about 200–350 KB per level | They are ours |

**The budget:**
- **On disk:** about 1–2 KB extra per level, under 0.25 MB for 115 levels.
- **Load:** under 120 ms per level on the CPU.
- **In memory at peak:** about 21 MiB at 2×, the same as the rich pass.

## 7. Sources

**Official FINAL FANTASY XIV art (© SQUARE ENIX)** is read from the player's own install (patch 2026.09.15) by `../rich/tools/texdump`.
- Every file, and what it is for, is in `sources.json`, written by the scripts as they read.
- Nothing is redistributed. The renders are derived and graded for review only, and the plugin reads the files at runtime.

The files:
- **UI art:** `ui/uld/` `Journal_Frame_hr1`, `Journal_Detail_hr1`, `LovmPalette_hr1`, `TabButtonA_hr1`, `TripleTriadCardSelect_hr1`, `TripleTriadBattle_hr1`, `TripleTriadResultCrown_hr1`, `PVPRankEmblem3_hr1`, `JobHudSCH0_hr1` (`.tex`).
- **Cards:** `ui/icon/087000/` (`_hr1.tex`):

  | Card | Character |
  |---|---|
  | `087019` | Tataru |
  | `087020` | Moogle |
  | `087049` | Y'shtola |
  | `087050` | Urianger |
  | `087056` | Minfilia |
  | `087058` | Cid Garlond |
  | `087059` | Alphinaud & Alisaie |
  | `087060` | Louisoix |
  | `087065` | Merlwyb |
  | `087066` | Kan-E-Senna |
  | `087067` | Raubahn |

- **Paintings:**
  - `ui/loadingimage/-nowloading_base03`, `05`, `07`, `21` and `25` (`.tex`);
  - `ui/map/world/01/world01_m.tex`.
- **Fonts:**
  - glyph tables: `common/font/Jupiter_46.fdt`, `Jupiter_23.fdt`, `TrumpGothic_184.fdt`, `TrumpGothic_68.fdt`, `AXIS_36.fdt`, `MiedingerMid_36.fdt`;
  - atlases: `common/font/font1`, `font2`, `font4` and `font7` (`.tex`).

Reviewed and not used:
- the Trust busts (`072621`–`072664`), which show later-expansion looks;
- the battle-talk faces (`073xxx`);
- the other 1,500 UI textures the scan listed.

Our own work:
- the layouts, the dress shapes and the motion;
- the grades;
- the lantern cart and boat;
- the launcher and the pegs;
- the peg marks.

**Nothing from Peggle.** No layouts, art, characters or boards. Peggle's Masters were checked against the cast; the two nearest cases are noted in `characters.md`.

## 8. Supervision record

The rounds are in `supervisor/`:
- the game designer: `game-designer-round-N.md`;
- the UX/UI specialist: `ux-round-N.md`;
- the level-design critic: `level-critic-round-N.md`;
- the designer's answers to each round: `response-round-N.md`.

**Round 1** (commit 1ef1ab72): all three said REVISE.
- **Game designer:** "still plain" was not solved. Single-hue boards, framing that read as walls, a plain Fever and tally, and no power moment.
- **UX/UI specialist:** the level-select hierarchy, unclear map states, 640 text below the floor, the Fever laurel over the title, the tally headline, pause spacing, and the fireflies near pegs.
- **Level-design critic:** solid-looking framing on base-p3, exp-p1 and exp-p3, and rules that could not be enforced.

Every Major and Minor was addressed in round 2 (`supervisor/response-round-1.md`).

**Round 2** (commit e9691733):
- **Game designer: APPROVE**, with Minors: the fireflies in a row, the beams not visibly moving, the power card at 640, the five-turn gauge, the F7 loophole, the quill, the card back's double meaning, and accent effects near peg hues.
- **UX/UI specialist: REVISE** on one Major: the power card covered the board at 640. Its Minors were the gauge, the peg-marks state, the Fever plate, protan separation on base-p3, map signs, the 640 padlocks, a caption, the corner cut, the steppers and the fireflies.
- **Level-design critic:** all six boards APPROVE. The rules were REVISE: F3c could not fail and F7 double-counted. Its Minors were F3d holes, F6 blind to the chrome (a peg by the right wall on base-p2), F3a relying on drawers registering, and the fireflies in a row.

All of these were addressed in round 3 (`supervisor/response-round-2.md`).

**Round 3** (commit 5130f3df): **all three APPROVE.**
- **Game designer: APPROVE.** Every round-2 Minor is resolved.
- **UX/UI specialist: APPROVE.** Every round-2 finding is resolved.
- **Level-design critic: APPROVE.** All six boards and the F-rules. The checks fail on the critic's own synthetic shapes, and its measurements agree with `readcheck.json` and `framecheck.json`.

### Final verdicts

All three supervisors approve the assets at commit 5130f3df. The renders in this folder are those assets.

Minors and Nits left open, none blocking. They are recorded for the build, not fixed after approval, so the approved renders stay exactly as reviewed:
- **Power moment:**
  - the LONG SHOT callout covers one peg (designer N9, UX m1). Place callouts by the clearance field, the same 6-unit rule as the framing, and keep them small;
  - at 640 the BRASS WINGS ribbon's tails run over the level name plate.
- **Tally 640:** "Cid · Brass Wings ×1" touches the right frame.
- **Title motion:** the mist layers pop at the loop seam. At runtime each layer's UV scroll must wrap at its tile width.
- **The checks:**
  - drawn-light overlays (the compass rose, the neat-line) are outside F3 (critic R6);
  - clean arcs are not tested by F3c (critic R7). Until a constant-curvature test exists, large clean arcs are the critic's call.
- **Nits:**
  - base-p3's second jewel sits near the 15% floor;
  - exp-p3's space shares purple's hue (purple separates by value; keep the space no nearer 310° and no brighter);
  - one exp-p3 rock still reads round;
  - "FS" on the characters 640 grid;
  - the hold fill mocked at 1280 only;
  - the title backdrop is still mostly one hue.
- **The motion figure:** 0.017 is the largest lift within 8 units of a peg on the 0.8× preview, counting both brightening and dimming over the loop. The critic and the UX specialist measure up to 0.034 at single dust-mote pixels; their 99.9th percentiles are 0.019.

## 9. Open questions for the owner

1. **The cast.** Approve the eleven (`characters.md`, `characters/lineup.png`), or swap any for its alternate. Two cases are borderline:
   - Y'shtola is a Miqo'te, with feline ears;
   - Kan-E-Senna carries the flower power and wears a flower.
2. **Alisaie and the shield.** Until the shield clears Alisaie, should the twins show face down (as mocked), or as Alphinaud alone? Alphinaud alone would need a later-looking portrait, which spoils.
3. **The game's fonts.** Use Jupiter, AXIS and TrumpGothic through Dalamud, with nothing bundled?
4. **Stage names and homes.** Approve the eleven stage names (section 3), each themed to its companion's home, or rename any.
5. **Motion defaults.** Decoration "Full" by default (Reduce motion still everything off), or "Simple" (beams and halos only)?
6. **Peg marks.** Ship the colour-blind assist off by default, with a first-run hint in Options (as mocked)? Or on?
7. **Preview sizes.** The three previews total about 38 MB in true colour. Keep them in the repo, or put them on the plan site only?

The rich pass's question 4 (warm parchment or the cooler chart for base-p1) is settled for the cooler chart. The UX/UI specialist measured that the parchment weakened the oranges for colour-blind players.

## Not verified

- How any of this looks in the game, in Dalamud's renderer, at the plugin's window scales.
- Dalamud's game-font metrics against the mocks' glyph rendering.
- The cost of the motion in ImGui, and of the dress shapes in C#.
- Frame pacing in the game: the previews are rendered from stills.

# Moonfall levels: a source for every "needs a source" level

Status: research result, 7 October 2026, for the charter's 55 *needs* levels (`charter.md`, section 2). Nothing here
changes a level, a recipe or code. Each stage's author still fixes the real crop on the trace sheet.

**How the paths were found.** Every texture path below was read from the install, not guessed:
- `texdump loading` lists every territory's loading image. The `LoadingImage` sheet holds exactly 37 paintings,
  `-nowloading_base` and `-nowloading_base01` to `_base36`, each with a 3840 × 2160 `_hr1`.
- `texdump maps` lists every area and region map.
- `texdump dumplist` dumped each candidate into `docs/design/v9/rich/.cache` (gitignored) and confirmed it exists
  (`OK <path> <w>x<h>`).

Nothing from the game is committed.

**Crops.** Crops are `[x, y, w, h]` in the texture's own pixels: 3840 × 2160 for `_hr1` paintings and 2048 × 2048 for
maps. They are 4:3 and unmirrored unless marked. A crop on a 1x painting (`base03`, `base10`, `base21`, `base25`) is
given in 1x pixels, as the charter writes it.

**Checks.** All the crops were checked against each other and against the shipped crops (1-1, 1-4, 2-1, 2-3):
- no two neighbouring levels use the same texture;
- no texture is used more than twice in a stage;
- every pair of crops on one texture overlaps by at most 25% of either crop's area.

That check found one conflict, between two crops the charter had already verified. 5-1 `[1800, 60, 1440, 1080]` and 5-4
`[2300, 640, 1440, 1080]` on `base01` overlap by 30%. Moving 5-4 to `[2400, 700, 1440, 1080]` brings the overlap down
to 24%.

**What the game has.** Only one painting covers each region, and several regions are thin:
- **Thanalan:** `base02` is the only painting, and 1-4, 4-2 and 4-5 already use it. FS 2 has almost nothing left.
- **Mor Dhona:** `base04` is the only painting. It shows Silvertear Lake with Dalamud's wreck, not the Crystal Tower.
  `l1f1/02` is an interior map and of no use.
- **Kugane:** `base10` is the only painting, and lantern-night's crop takes 75% of its width.

There are also some sources the charter did not know about:
- the Gold Saucer's two illustrated maps (`w1eb/00`, `w1ed/00`), as rich as any painting;
- the Labyrinthos map's great ring (`k5f1/00`);
- the Carteneau Flats map (`l1p3/00`).

Plate backgrounds (`BannerBg`, 512 × 840) are too small to use.

**Legend.**
- **game:** a game texture. The plugin reads it at runtime and never ships it.
- **Fan Kit:** an official Fan Kit wallpaper, shipped as a picture only after the owner checks it (see below).
- **ours (re-subject):** moved to our own painting under the charter's rule 1.3 or the owner's answer 6.
- **weak:** only a pale or empty source exists.
- *Place (era)* is the scene's spoiler tag.
- *[assumed]* marks a shield area name the charter has not confirmed.

## The Moon Road (22 levels)

| Code | Source | Crop | Place (era) | Why it fits |
|---|---|---|---|---|
| 3-4 | **ours (re-subject):** `cloudlane` (new). DR: cloud banks sliding past under the moon, with the lane's beacon lamps and no hull | — | nowhere | No airship art at a usable size anywhere. The chart's airships on `base05` are about 100 px, and stage 3 already uses `base05` twice. `base02`'s red craft is a blurred brushstroke, and stage 4 reserves that painting. No Fan Kit wallpaper shows an airship |
| 4-1 | game `ui/map/w1t2/01/w1t201_m.tex` | [560, 620, 960, 720] | Ul'dah - Steps of Thal (ARR) | The Steps of Thal plan: the Gold Court's ring, the streets and the gates, ready for a walk with ring stops |
| 4-4 | game `ui/map/w1t2/02/w1t202_m.tex` | [560, 640, 960, 720] | Ul'dah - Steps of Thal (ARR). The Coliseum is on the Thal map, not Nald | The lower floor's ring and wings, over the faint city ring and spokes: rings and spokes of brick |
| 5-5 | game `ui/map/s1f4/00/s1f400_m.tex` | [560, 360, 1360, 1020] | Western La Noscea (ARR) | The Swiftperch and Aleport coast and its headlands, where the lighthouse stops go. Pale map style |
| 7-1 | game `ui/map/f1t1/00/f1t100_m.tex` | [640, 820, 1040, 780] | New Gridania (ARR) | The city's plan with its canal arms and bridges |
| 7-2 | game `ui/loadingimage/-nowloading_base_hr1.tex` (the Black Shroud painting) | [0, 240, 1440, 1080] | Central Shroud (ARR) | The great trunks and canopy at the painting's west, a treeline band in late light |
| 7-4 | game `ui/map/f1f2/00/f1f200_m.tex` | [360, 440, 1400, 1050] | East Shroud (ARR) | The East Shroud's paths through the Sylphlands, along which the lanterns wind |
| 7-5 | game `-nowloading_base_hr1.tex` | [2440, 240, 1400, 1050] | North Shroud (ARR) | The east trees: roots, boughs and a lit lamp path. Overlap with 11-3 is 18% |
| 8-1 | game `ui/map/w1eb/00/w1eb00_m.tex` | [0, 256, 2048, 1536] | The Gold Saucer (ARR) | The illustrated Saucer map: its great ring, the balloons, the cactuar and the chocobo. Very rich |
| 8-3 | game `ui/map/s1f3/01/s1f301_m.tex` | [1024, 1040, 1024, 768] | Eastern La Noscea (ARR) | The east lobe and its bay, with Costa del Sol's beach. A map is weak for a reflection level. No Costa del Sol painting exists (the Fan Kit's 2.1 coast picture is Mist) |
| 8-4 | game `ui/map/w1t1/01/w1t101_m.tex` | [380, 560, 960, 720] | Ul'dah - Steps of Nald (ARR). The Sapphire Avenue Exchange is on the Nald map (charter: Thal) | The market arc's aisles and stalls |
| 8-5 | game `ui/map/w1ed/00/w1ed00_m.tex` | [448, 512, 1152, 864] | The Gold Saucer (ARR) | The second Saucer map's ring of circles and its Chocobo Square, with room for an orbiting drum |
| 9-1 | game `ui/map/l1f1/01/l1f101_m.tex` | [160, 300, 1100, 825] | Mor Dhona (ARR) | Revenant's Toll's cluster and the paths round the lake's north shore |
| 9-2 | game `ui/loadingimage/-nowloading_base04_hr1.tex` | [0, 400, 1320, 990] | Mor Dhona (ARR) | **Subject adjusted:** the wreck's spined hull rising over the lake makes the skyline band. The painting has no glass spires |
| 9-4 | game `-nowloading_base04_hr1.tex` | [1100, 300, 1800, 1350] | Mor Dhona (ARR) | **Subject adjusted:** "the shell of Dalamud" round its glowing heart, partly outlined. No Crystal Tower art exists in the game's UI art |
| 9-5 | game `ui/map/l1f1/01/l1f101_m.tex` | [1048, 120, 1000, 750] | Mor Dhona (ARR) | The east ridges on the way to the tower. Overlap with 9-1 is 16%, and 9-2 to 9-4 sit between them |
| 10-1 | **ours (re-subject, owner's answer 6):** `silvertear` (new). LM: the falls as columns over the cliffs | — | Mor Dhona (ARR) | Mor Dhona is thin: one painting and one usable map. No falls appear in either |
| 10-3 | game `ui/map/l1p3/00/l1p300_m.tex` (Carteneau Flats: Borderland Ruins) | [512, 640, 1024, 768] | Mor Dhona (ARR) | The flats' three-lobed impact ground, whose ridges carry the bricks |
| 10-5 | game `-nowloading_base04_hr1.tex` | [2560, 1000, 1280, 960] | Mor Dhona (ARR) | The lake's mist under the peaks and the dome wreck, for the mirror. Overlap with 9-4 is 18% |
| 11-1 | game `ui/map/w1f4/01/w1f401_m.tex` | [480, 1320, 960, 720] | Southern Thanalan (ARR) | The Sagolii's open dunes. **Weak:** the map is pale and almost empty, so the crests will be our dress |
| 11-3 | game `-nowloading_base_hr1.tex` | [1300, 300, 1400, 1050] | New Gridania (ARR) | Gridania's lit hall and roofline between the trunks. The painting has no lake, so the band is the roofline |
| 11-4 | game `ui/map/r1f1/00/r1f100_m.tex` | [640, 400, 1200, 900] | Coerthas Central Highlands (ARR) | Camp Dragonhead's walls and towers in the middle of the highlands |

## The Far Shore (33 levels)

| Code | Source | Crop | Place (era) | Why it fits |
|---|---|---|---|---|
| FS 1-1 | game `ui/map/s1t2/01/s1t201_m.tex` | [128, 760, 1200, 900] | Limsa Lominsa Lower Decks (ARR) | The Lower Decks' quays and the Bulwark's walk |
| FS 1-2 | **Fan Kit** 2.0 #51 "A Realm Awoken (2.1)", Mist | [0, 150, 880, 660] of 1920 × 1080 | Mist (ARR) [assumed]. Nearest shield area: Lower La Noscea | Two sailing boats at anchor by the pier, the lighthouse islet, the cliffs and the white town. Clear of the logo, which starts at x 665, y 860. The boats are small (about 75 px), and an 880 px crop is soft at 2x |
| FS 1-4 | game `ui/map/s1f2/00/s1f200_m.tex` | [1024, 384, 1024, 768] | Lower La Noscea (ARR) | Moraby Bay's ports and quays |
| FS 1-5 | **Fan Kit** 2.0 #45 "Through the Maelstrom (2.2)" | [0, 40, 840, 630] of 1920 × 1080 | Limsa Lominsa Lower Decks (ARR); a 2.2 primal | **Re-subject (CR):** "Leviathan", the serpent in an even ring of moons. No galleon at a usable size exists in game art or the Fan Kit. Clear of the logo and of every adventurer figure |
| FS 2-1 | **weak**, game `ui/map/region/02/region02_m.tex` | [64, 320, 1024, 768] | Western Thanalan (ARR) | The region map's west coast, where Vesper Bay lies. The parchment is nearly blank. **Treat as missing:** see the owner's questions |
| FS 2-3 | **ours (re-subject):** `coastcliffs` (new). TE: the coast's cliffs by night | — | Western Thanalan (ARR) | `base02` is used up (1-4, 4-2, 4-5). 1-1 takes `w1f1/00`'s coast. `world/00` is the same chart as `base05` |
| FS 2-4 | **weak**, game `region/02` | [64, 1088, 1024, 768] | Western Thanalan (ARR) | The south-west coast of the Silver Bazaar. Blank parchment. **Treat as missing** |
| FS 2-5 | **ours (re-subject):** `twinlights` (new). RF: two beams and their mirror halves | — | Western Thanalan (ARR) | As FS 2-3 |
| FS 3-1 | game `ui/loadingimage/-nowloading_base06_hr1.tex` | [200, 300, 2000, 1500] | The Sea of Clouds (HW) | The floating isles over the cloud sea: cloud tops as humps, moons on the isles |
| FS 3-2 | game `ui/map/r2f1/00/r2f100_m.tex` | [1100, 1300, 900, 675] | Coerthas Western Highlands (HW) | Falcon's Nest's walls and tower on the highlands' south tip |
| FS 3-4 | game `ui/map/a2f1/00/a2f100_m.tex` | [384, 448, 1280, 960] | The Sea of Clouds (HW) | The isles as islands on the map: a route with ring stops |
| FS 3-5 | game `ui/loadingimage/-nowloading_base03_hr1.tex` | [2700, 700, 1140, 855] | Coerthas Central Highlands (ARR) | **Subject adjusted:** "the Coerthas peaks receding", a band of snow peaks over the cloud sea. Ishgard itself lies inside 3-3's crop. Overlap with 3-3 is exactly 25% |
| FS 4-1 | game `ui/map/e3f1/00/e3f100_m.tex` | [256, 384, 1536, 1152] | The Ruby Sea (SB) | The isles, Onokoro among them, as ring stops on a sea route |
| FS 4-2 | game `ui/loadingimage/-nowloading_base09_hr1.tex` | [1900, 700, 1500, 1125] | The Ruby Sea (SB) | The vermilion shrine hall and its gate over the falls, partly outlined. Confirm on the trace sheet that this is Onokoro's |
| FS 4-4 | game `-nowloading_base09_hr1.tex` | [0, 300, 1500, 1125] | The Ruby Sea (SB) | The karst ridges and mist: bricks on the crests, a moon on each head |
| FS 5-1 | game `ui/map/s1f4/00/s1f400_m.tex` | [40, 1100, 960, 720] | Western La Noscea (ARR) | The Isles of Umbra's coasts and ruin. Overlap with 5-5 is 18% |
| FS 5-4 | game `ui/map/s1f2/00/s1f200_m.tex` | [920, 1260, 860, 645] | Lower La Noscea (ARR) | **Re-subject (LM):** "the Moraby Drydocks", where the Admiral's fleet is built, partly outlined. No wreck art is usable: the only wreck, in the 2.2 key art, lies under its title |
| FS 5-5 | **ours (re-subject):** `swell` (new). DR: the storm swell's crests carried by slide movers, lamp flicker on the spray | — | Western La Noscea (ARR) | No flagship picture in game art or the Fan Kit |
| FS 7-1 | game `ui/loadingimage/-nowloading_base15_hr1.tex` | [2560, 560, 1280, 960] | Il Mheg (ShB) | The faerie tower's ruined gate and stair among the trees |
| FS 7-3 | game `-nowloading_base15_hr1.tex` | [300, 900, 1500, 1125] | Il Mheg (ShB) | **Subject adjusted:** the falls and crystal spires mirrored in the misty lake. The painting has no lake castle (the map has one, but 7-4 uses the map) |
| FS 7-4 | game `ui/map/n4f4/00/n4f400_m.tex` | [256, 256, 1536, 1152] | Il Mheg (ShB) | The flower paths round the lake and Lyhe Ghiah's isle |
| FS 7-5 | **ours (re-subject):** `floatgrove` (new). TE: floating isles' rims, drifting | — | Il Mheg (ShB) | A third `base15` crop would break "at most twice in a stage". An alternative is `base15` [800, 100, 1400, 1050], the floating cliff and vine pillar, as a recorded exception |
| FS 8-2 | game `ui/map/e3t1/00/e3t100_m.tex` | [512, 400, 1100, 825] | Kugane (SB) | The harbour streets and piers |
| FS 8-4 | **ours (re-subject):** `rooftops` (new). SB: lantern-lit roof silhouettes | — | Kugane (SB) | No Kugane painting other than `base10`, and lantern-night takes 75% of it |
| FS 8-5 | game `e3t1/00` | [560, 1040, 1000, 750] | Kugane (SB) | The southern piers and open water for the orbiting drum. Overlap with 8-2 is 25%. `e3ff/00` (Kugane Ohashi) is a tiny arena map and of no use |
| FS 9-3 | game `ui/map/k5t1/00/k5t100_m.tex` | [200, 260, 1440, 1080] | Old Sharlayan (EW) | The city plan, from the Studium to the harbour walk |
| FS 9-4 | game `ui/loadingimage/-nowloading_base21_hr1.tex` | [2560, 300, 1280, 960] | Old Sharlayan (EW) | The east colonnades, domes and terraces beyond exp-p1's crop (exp-p1 ends at x 2550 in hr1). Overlap is 4% |
| FS 9-5 | game `k5t1/00` | [480, 1180, 1000, 750] | Old Sharlayan (EW) | The harbour's curved breakwaters: runs of brick. Overlap with 9-3 is 21% |
| FS 10-1 | game `ui/loadingimage/-nowloading_base24_hr1.tex` | [900, 200, 2000, 1500] | Labyrinthos (EW) | The false sky's lattice dome and the round plaza below |
| FS 10-3 | game `ui/map/k5f1/00/k5f100_m.tex` | [440, 100, 1280, 960] | Labyrinthos (EW) | The gardens' groves and paths north of the ring |
| FS 10-4 | game `-nowloading_base24_hr1.tex` | [2640, 520, 1200, 900] | The Aitiascope [assumed] / Labyrinthos (EW) | The great brass pylon, partly outlined. Overlap with 10-1 is 22% |
| FS 10-5 | game `k5f1/00` | [80, 820, 1360, 1020] | Labyrinthos (EW) | The great ring and its four crossings with pylons: spans as bricks, moons on the pylons |
| FS 12-3 | game `ui/map/u5f1/00/u5f100_m.tex` | [384, 448, 1280, 960] | Mare Lamentorum (EW) | The crater basins drawn on the map: brick crowns on the rims |

## Counts

| Stage | Game | Fan Kit | Ours (re-subject) | Missing (weak fallback) |
|---|---|---|---|---|
| MR 3 | 0 | 0 | 1 (3-4) | 0 |
| MR 4 | 2 | 0 | 0 | 0 |
| MR 5 | 1 | 0 | 0 | 0 |
| MR 7 | 4 | 0 | 0 | 0 |
| MR 8 | 4 | 0 | 0 | 0 |
| MR 9 | 4 (2 with adjusted subjects) | 0 | 0 | 0 |
| MR 10 | 2 | 0 | 1 (10-1) | 0 |
| MR 11 | 3 | 0 | 0 | 0 |
| FS 1 | 2 | 2 (1-2; 1-5 re-subjected) | 0 | 0 |
| FS 2 | 0 | 0 | 2 | 2 (2-1, 2-4) |
| FS 3 | 4 (3-5 adjusted) | 0 | 0 | 0 |
| FS 4 | 3 | 0 | 0 | 0 |
| FS 5 | 2 (5-4 re-subjected) | 0 | 1 | 0 |
| FS 7 | 3 (7-3 adjusted) | 0 | 1 | 0 |
| FS 8 | 2 | 0 | 1 | 0 |
| FS 9 | 3 | 0 | 0 | 0 |
| FS 10 | 4 | 0 | 0 | 0 |
| FS 12 | 1 | 0 | 0 | 0 |
| **All 55** | **44** | **2** | **7** | **2** |

**Game shares.**
- **The Moon Road:** stage 3 drops to 3 of 5 and stage 10 to 2 of 5. The second is the owner's answer 6.
- **The Far Shore:** stage 2 drops to 0 of 5 (2 of 5 with the weak region map), and stages 5, 7 and 8 drop to 3 of 5.
  Fan Kit pictures are counted as neither game nor ours.

## The five figures and the Fan Kit

| Figure (level) | Result |
|---|---|
| Airship (3-4) | **None found.** No Fan Kit desktop wallpaper (2.0 to 7.0) or ARR/HW smartphone wallpaper shows one. Re-subjected to ours |
| Ships at anchor (FS 1-2) | **Fan Kit** 2.0 #51 (Mist, patch 2.1): `https://lds-img.finalfantasyxiv.com/h/E/-qRcuAOPrQ_dQEg05rnr-BLyok.jpg` (1920 × 1080). Crop [0, 150, 880, 660] |
| Galleon (FS 1-5) | **None found.** Proposed Fan Kit re-subject: 2.0 #45 (Through the Maelstrom, 2.2), `https://lds-img.finalfantasyxiv.com/h/8/Xo_h0JONVhFH_C_f1U4408ezXI.jpg`. Crop [0, 40, 840, 630], Leviathan only |
| Wreck (FS 5-4) | **None usable.** The 2.2 key art's ship lies under the title and logo (x ≥ 660, y ≥ 860). Re-subjected to the Moraby Drydocks map |
| Flagship (FS 5-5) | **None found.** Re-subjected to ours |

Source page: `https://na.finalfantasyxiv.com/lodestone/special/fankit/desktop_wallpaper/2_0/`. The same wallpaper with a
different title is #57 (Mist) and #41 (Maelstrom).

**What the pages say (read 7 October 2026).**
- **The Fan Kit page** says: "Please read and agree to the license agreement before downloading the FFXIV fan kit." Its
  link (`support.na.square-enix.com/rule.php?id=5382&la=1&tag=software_en`) opens the FINAL FANTASY XIV Software
  License Agreement. That agreement grants a "limited, personal, nonexclusive, non-transferable, revocable license … for
  your own personal use … not for any commercial purpose".
- **The FFXIV Materials Usage Policy** (`support.na.square-enix.com/rule.php?id=5382&tag=authc`, shown as effective 16
  September 2026) covers images published on the official website:
  - non-commercial use only;
  - "You may not make excessive edits or alterations to the Materials";
  - include the notice "© SQUARE ENIX";
  - comply at once with any request to remove material, and the policy is revocable at any time;
  - "Creating items using AI-generated derivative works is prohibited".
  - It names websites, blogs and social media. It says nothing about shipping images inside software.

**For the owner to check before anything ships:**
- does bundling a cropped, night-graded wallpaper in a plugin fit "no excessive edits" and the policy's scope at all;
- where the "© SQUARE ENIX" notice goes.

Candidates were downloaded only to the session scratchpad.

## Questions for the owner

1. **FS 2 (Western Thanalan) has no rich source left.** Options:
   - (a) accept two weak region-map crops and three of ours;
   - (b) allow the Fan Kit for places, not only figures;
   - (c) move FS 2's place or theme.
2. **Fan Kit beyond figures.** FS 1-5 becomes Leviathan, a figure from the Fan Kit. Accept it, or keep "Setting Sail" as
   one of ours, which would make it a constellation and change its technique?
3. **Re-subjects to ours:** 3-4, 10-1, FS 2-3, FS 2-5, FS 5-5, FS 7-5 and FS 8-4. Two of them could stay game instead:
   - FS 7-5 as a third `base15` crop, as a recorded exception;
   - FS 8-4 on `base09`, at the cost of a Ruby Sea scene inside the Kugane stage.
4. **Subjects that change with the art:**
   - 9-2: the wreck's spined hull, not glass spires;
   - 9-4: Dalamud's shell, not the Crystal Tower;
   - FS 3-5: the Coerthas peaks, not Ishgard;
   - FS 7-3: falls in the lake, not the lake castle;
   - FS 5-4: the Moraby Drydocks, not the wreck.
5. **Two of the charter's verified crops:**
   - 4-5's west strip `[0, 240, 800, 600]` unmirrored is a blurred smudge;
   - 5-1 and 5-4 overlap by 30% (fix above).

**Contact sheets** (scratchpad, not the repo: they contain game art): one per stage in the session scratchpad's
`mf/sheets/`, `<stage>.png` plus `full_<stage>.png` with each crop boxed on its whole texture.

## Owner's answers (7 October 2026)

- **Fan Kit art.** Ship the two cropped wallpapers with a "© SQUARE ENIX" notice, and remove them if Square Enix asks. They are ships at anchor for FS 1-2 and Leviathan for FS 1-5. They are credited in `Tsukimichi/THIRD-PARTY-NOTICES.txt` and the README's credits. The licence, as read on 6 October 2026, covers personal, non-commercial use and doesn't mention software; the owner accepts that.
- **Far Shore stage 2:** two crops of the faint Western Thanalan region map, richly dressed, plus three of our own sky, water or light paintings.
- **Subjects and moves accepted:**
  - 9-2 shows Dalamud's spined hull and 9-4 Dalamud's shell;
  - FS 3-5 shows the Coerthas peaks and FS 7-3 the falls in the lake;
  - the seven moves to our paintings stand;
  - FS 7-5 keeps game art with a third crop of the Il Mheg painting, recorded as an exception to the twice-per-stage rule.
- **Crop fixes:** 5-4 moves to [2400, 700, 1440, 1080]. 4-5 switches from the blurred west strip to an Ul'dah city map.

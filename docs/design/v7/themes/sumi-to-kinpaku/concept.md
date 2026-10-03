# Sumi to Kinpaku (v7 theme)

Round 1's chosen direction ("ink and gold leaf"), revived as a full theme for the v7 picker (plan v7 T15, `docs/research/plan-v7/theme-system.md` §4.4). Every quest state is a small **tsukimi crest**: a lacquered plaque made the way a maki-e craftsman makes a suzuri-bako lid. The meanings, the silhouette (r 63.2), the well (centre 64, 64, r 52.4) and the badge slot are Menphina's Medallion round 5's, so a player can take the whole theme or mix single states with any other set.

## Pitch

Black lacquer, shell-white and cut gold leaf.

- **The ground is ro-iro lacquer (sumi).** It is glossy, so it carries one soft reflection of the key light and nothing else.
- **The moons are gofun** (ground shell-white) built up a hair in relief (moriage): matte, one flat tone, a lit lip where an edge faces the key light, a short shadow down and to the right. No gradient ever touches a lit moon, and nothing is ever cut into one: no cheese at any size.
- **The gold is real leaf.** It is cut into strips (kirikane) for the moon road and the frame's crest line, and laid flat for the check and the "comes back" arc. Flat leaf reflects one even tone, so it never takes a ball gradient.
- **Every other material is a craft one:** Blocked's cloud is silver leaf tarnished to pewter, Dalamud is vermilion lacquer (shu-urushi) cracked to the black ground, and the ribbon is indigo-dyed silk (ai-zome).
- **FFXIV:** Kugane Castle's tenshu stands on Ready's far shore, Dalamud is the cracked red moon, the theme's paired palette is Kugane Lacquer, and Completed is the moon-viewing moon of Hingashi (the hanafuda August card: the full moon over a hill of susuki grass).

Medallion is a struck gilt medal with lapis enamel and a bright day scene. Sumi is a dark crest plaque: in a column it reads as black, ivory and gold, and only Ready opens a lit sky. It is the quiet theme, the natural partner for Decoration Plain and the Kugane Lacquer palette.

**Generator:** `_src/gen.py` writes every SVG here (faces, the Kirikane kit, composites, the row badges). `_src/mix.py` renders `_mix.png`. The sheet is `moon-v6/round5/render_sheet.py`.
- Run `python _src/gen.py`, then `python _src/mix.py`, then `python docs/design/moon-v6/round5/render_sheet.py docs/design/v7/themes/sumi-to-kinpaku`.
- `gen.py` reads the three job PNGs from `moon-v6/round5/medallion-r5/_src/jobs/`. `mix.py` imports `gen5.py` (read-only) for Medallion's faces and its Brass kit.
- The atlas manifest is `tools/themes/sets/sumi-to-kinpaku.json` (with `faces` and `plain`), the kit manifest `tools/themes/kits/kirikane.json` (with `ornaments`; layout and job-seat layering copied from Ishgard Glass's, which has the same tier split). They lived in `_src/` until the set shipped in 1.17 (T15).

## Files

| File | What it is |
|---|---|
| `faces/<state>-under.svg` | The **unframed face**: the well and its picture, clipped to the shared well (centre 64, 64, r 52.4). No pixel falls outside it (rule R1). Ready on another job's face has an empty seat. |
| `faces/<state>-over.svg` | The **overhangs**, drawn above the frame: In journal's silk ribbon and Completed's gold check. Empty for the other six. |
| `_mid/…` | The mid tier (48 and 64 px): relief and its one-pixel cast shadow, without the hero's hairline craft. Same file layout. |
| `_row/…` | The row tier (under 32 px): flat shapes only, strokes drawn bold, no badge. Same layout, plus `_row/badge-{open,closed,journal}.svg`, the badge glyphs alone at text height. |
| `kit/frame-<tier>-{full,quiet}.svg` | **The Kirikane kit**: `act-now`, `resting`, `finished`, `ghost`, each at Full and Quiet. `kit/_row/` holds the row-tier frames. |
| `kit/badge-ring-{act-now,resting}.svg` | The badge keyline, shadow and ring: gilt on Ready, lacquer with a kirikane line elsewhere. |
| `kit/badge-seat-{open,closed,journal,tank,healer,dps}.svg` | The lacquer seats. Role seats keep the fixed role colours (the Hand seat is the tank seat recoloured by the manifest). |
| `kit/glyph-{open,closed,journal}.svg` | The badge glyphs at the badge position in the 128 box. |
| `ready.svg` … `not-checked.svg`, `ready-on-another-job-{paladin,bard,white-mage}.svg` | **Composites**: face under, kit frame for the state's tier, kit badge, face over. Same names as `medallion-r5`. `ready-on-another-job.svg` carries the Paladin icon. |
| `_sheet.png` | The standard sheet (`render_sheet.py`). It shows the full (96 px and up) composites at every size, as for the other sets; `_mid/` and `_row/` are what the atlas actually draws below 96 px. |
| `_plain/<state>.svg` | **Decoration Plain** (theme-system §3.5): the row geometry in flat ink and gold, no kit frame, and Medallion's Plain rim: each state's own ink at .6 (Ready and In journal Moon `#F2D27A`, Completed `#D6B25A`, Ready on another job and Done `#DDE3F0`, Blocked `#7C86A8`, Locked out `#B25C7F`, Not checked `#8A93B0`), r 54–60, about one device pixel at 16–20 px, so the two Plain finishes line up in a mix. One master per state for every size, no badge. Built into `row.png` as the row strip's `plain` finish (12–31 px); from 32 px Decoration Plain shows Medallion's Plain ladder, since a hero `plain.*` atlas would break the per-set texture budget. |
| `kit/ornaments/sigil.svg`, `sigil-small.svg`, `lozenge.svg`, `corner.svg` | **The Kirikane ornaments** for Decoration, in flat gold leaf, 32-unit box. **Size rule for the sigil** (the crescent-in-circle crest, maru ni tsuki): `sigil.svg` from 13 px up (with the leaf's lit edge); `sigil-small.svg` at 10–12 px only (a ring just over 1 px wide at 10 px, a thick crescent, a full pixel of pane between them, no hairlines); **below 10 px, never the crest:** draw `lozenge.svg`, a single 2 px gold-leaf lozenge, as Medallion's divider does (2 px times the UI scale). At 8 px the crest turns to a ring with a blob in it. `corner.svg` is the square kamon corner mark (an L of kirikane with a cut square at the corner; mirror it for the other corners). |
| `_mix.png` | The swap test at 96, 48 and 20 px: Sumi faces in the Kirikane kit, Sumi faces in the Brass kit, Medallion's faces in the Kirikane kit, Medallion as shipped, the kit alone with its ornaments, and the Plain finish. |

## Round 2 (realism supervisor and design critic)

This section supersedes anything below it that conflicts.

| # | Ruling | Done |
|---|---|---|
| Must 1 | **Locked out read as a basketball** (both reviewers; the most ball-like of the five sets) | **Redrawn as broken lacquer, not a seamed ball.**<ul><li>**Impact** at (52, 50), up and left, off both axes.</li><li>**Through cracks** (four at mid and hero, the three widest at row): each is a zigzag of two or three straight runs, every run at least 25° off the vertical and the horizontal, the longest 33 units (44% of the diameter). The bends alternate, so no crack curves like a seam.</li><li>**Two cracks die inside their plates** (mid and hero): tapered dark slits, 15 and 10 units long.</li><li>**The cracks are gaps, not drawn lines.** Each plate is pushed out along its bisector (row 6.0 units, about 1 device px at 16 px; mid 3.0; hero 2.2) and turned up to 3.6° alternately, so the black socket (`#080406`) shows between plates. There is no seam stroke.</li><li>**One small plate has fallen out:** a chip at the rim where the lower-left crack runs out (18° of rim, 11 units deep). The socket shows there; its lower-right wall carries a faint lip.</li><li>**Each plate sits at a slightly different tilt**, so the plates differ by one small value step (`#D0524A` / `#C94842` / `#C4433F` / `#BB3D3A`). No ball takes uniform colour with seams.</li><li>**True shu-urushi, leaning crimson:** body `#C4433F`, lit `#DE6A50`, shade `#7E2418`, crack lip `#F4A892`. The body is flat; light comes only as one soft gloss on the upper left, lit lips on edges facing the key light, and shaded lips on the others. There is no ball gradient.</li></ul>**Check:** Done vs Locked out under protanopia, worst ground, rose from 11.9 to 12.4 (96–128 tier); 13.2 at mid. |
| Must 2 | **The ground wasn't ro-iro lacquer** (it was Medallion's flat navy-black) | **Warm black lacquer** `#0E0C10` → `#060508` on every face except Ready's and In journal's painted dusk (Ready on another job included). **One gloss band:** an upper-left diagonal sheen, `#FFFFFF` at .07, blurred (σ 5), 34 units wide (about a third of the well), at every tier including row, where it is broad tone, not texture. The kit's resting ring is the same black (`#3A363E` / `#141216` / `#060508`), so the kirikane line lies on lacquer. Earthlit discs, the closed-lock seat, the keyline (`#050406`) and the susuki silhouettes moved to the same warm black family. |
| Should 3 | Not checked looked like a help button | The ghost frame's gofun edge went from .2 to .32. The faint moon behind the "?" is lifted (`#35333E`) and carries its own gofun hairline at every tier. **The "?" has a real brush profile at mid and hero:** a thin, sharp entry, full width a third of the way round, thinning steadily into the stem, and lifting off dry at the foot (kasure: two slits part the last 5–7.5 units into three tines). At row size it keeps the solid stroke and round tome. |
| Should 4 | Done read as a ◐ contrast toggle | The earthlit half is a step lighter (`#3A3843`, against a well of `#0E0C10`). |
| Should 5 | Ready: lift toward a 1.25× lead in a Medallion mix | **The lower bokashi is brighter** (horizon `#B2C4EC`, middle `#4660A2`), and the **row road has more coverage** (strips about 15% longer and taller). **The light-palette lightness floor stopped it short:** 0.71 at 20 px (bar 0.70). A brighter horizon (`#BCCDEF` with a lifted sea) put the row Ready at 78 but the floor at 0.69, a fail. Ready's own lead rose to 1.65–1.70, but in the tool's neutral Brass mix Sumi's Ready over Medallion's other states reaches only 1.18–1.20 (it was 1.16–1.19), so the mix still warns. The **crescent's relief shadow** is now 1.18 units (0.7, 0.95), soft (σ 0.5), at .35, and masked off the moon's own disc. |
| Should 6 | Completed's susuki read as cracks across the moon at 96 px | The blades and ears now stay below the moon's equator (y 60): they rise from the hill only into the lower-left quarter of the disc. |
| Should 7 | Ornaments and Plain | Both delivered as SVG masters: `kit/ornaments/` and `_plain/` (see Files). Measured as a row strip, Plain passes the same gates: weakest pair 16.2 (16 px grey), worst colour-vision pair 15.6, Ready lead 1.61, Completed/Ready 0.62. The build tool does not write `plain.*` yet. |

### Round 2 tidy-ups (after approval)

| # | Note | Done |
|---|---|---|
| 1 | Locked out at 20–28 px: the gap junction read close to a letter "T" | At the row and mid tiers only, the upper arm (t2) swings about 15° toward the vertical. Its runs are now −64°, −44° and −64°, each still at least 25° off both axes. It no longer lines up with the left arm across the impact, so the three arms meet as an unequal "Y" (about 97°, 109° and 148° apart, where the "T" had 90°, 103° and 167°). The hero tier is unchanged. |
| 2 | Stale lines below the Round 2 table | The kit table, the Completed row, and the Round 1 → v7 table now give the files' values: warm lacquer, the ghost edge at .32, susuki below the equator. The frame-edge contrast figures are re-measured for the warm lacquer. |
| 3 | The small sigil turned to mush at 8 px | `sigil-small.svg` is redrawn for 10–12 px and floored at 10 px. Below 10 px it gives way to `lozenge.svg`, a single 2 px gold-leaf lozenge, as Medallion's divider does (see Files for the size rule). |
| 4 | The Plain finish's resting rims were uniform grey | Plain now wears Medallion's Plain rim: each state's own ink at .6, so the two Plain finishes line up in a mix. Ready's flat sky became its pale twilight (`#7D94CD`), because with the brighter rims Ready's lead had dropped to 1.28. Plain re-measured: see Should 7 above. |

## Round 1 → v7: what was kept, what was fixed

| Round 1 | v7 |
|---|---|
| **Kept:** the only set every round 1 juror chose; flat light shapes on a dark seat; gold only as cut leaf; calm, no cheese | All kept. Every lit moon is one flat tone; gold appears only as leaf (road, crest line, arc, check, open lock). |
| Read as a stock status set (radio button, no-entry ring and bar, check_circle, dashed spinner) | The full §2 grammar: the moon road, the ribbon over the rim, the clouded new moon, the "comes back" arc, the check across a full moon, the cracked Dalamud, the "?" over a faint moon. No ring plus slash, no dashed ring, no check in an empty circle. |
| The sumi well was invisible (1.05 : 1 on Night) | The medal's edge is now the frame's job: a gofun hairline at the lacquer's outer edge over a dark keyline. Worst case against each dark window (Night, Dawn, Kugane Lacquer): resting 2.4 : 1, finished 1.75 : 1, ghost 2.2 : 1 (bar 1.3); on Ishgard Snow the keyline (`#050406`) gives 18.1 : 1 (bar 3). The well itself is warm ro-iro lacquer (`#0E0C10` → `#060508`) with one broad gloss band. |
| Flat, with no material at large sizes | Craft by layering, not texture: gofun relief and cut-paper planes with a one-pixel cast shadow at mid tier; kirikane hairlines, Kugane Castle, susuki grass, the cloud's silver contour and the lacquer's crazing at hero. No noise, no speckle. |
| No FFXIV identity | Kugane Castle on Ready's shore, Dalamud as cracked shu lacquer, the hanafuda moon-viewing image on Completed, paired with Kugane Lacquer. |
| Gold ring as the "go" signal | Kept as the frame: Ready alone wears the shared medal gilt. The resting frames are lacquer with a fine gold crest line: the crest's *kage* (shadow) form, with Ready its *hinata* (sunlit) form. |

## Light and material

**Two lights, never confused** (the convention Ishgard Glass's transmitted/front split established):
- **The picture is lit by its own moon.** Ready's road falls directly under the crescent's lit centroid (x 61.8); the horizon is brightest under it; the far shore is a silhouette against the twilight; the castle's dark mirror lies directly below it on the sea.
- **The plaque is lit by the UI key light** (upper left, azimuth 135°). Everything physical obeys it: gofun and leaf have a lit upper-left lip and a shaded lower-right lip and cast a short shadow down-right (1.1, 1.5 at hero; 1.6, 2.1 at mid, about one device pixel); the lacquer frame's convex band is lit on its upper-left slope and carries a soft specular there; the badge drops Medallion's shadow (1.4, 1.9).
- **The unlit disc follows the sky.** In Ready's dusk the moon's dark limb is the sky's own value (the air in front of it is lit), marked only by the crest's gofun hairline; at night (Ready on another job, In journal, Done) it is an earthlit sumi plane a step above the well.

**Materials:**

| Material | Behaviour |
|---|---|
| Ro-iro lacquer (well, frame) | Glossy warm black. One broad diagonal gloss band toward the light, never a texture. The frame is a convex band (lit slope / shaded slope). |
| Gofun (moons, "?", book pages) | Matte, one flat tone, raised a hair. |
| Kinpaku, kirikane (road, crest line, arc, check, open lock) | Flat metal leaf: one even tone, a bright cut edge where it faces the light. The road brightens toward the viewer by **coverage** (longer, thicker strips), never by gradient. |
| Silver leaf tarnished to pewter (Blocked's cloud, closed lock) | Flat mid grey, held under the limb's value so the limb stays the only light. |
| Shu-urushi (Dalamud) | Vermilion lacquer, one soft gloss highlight; broken plates show the black ground in the sockets; crack faces toward the light catch a pale lip. |
| Ai-zome silk (ribbon) | Lit edge on the left, a gofun selvedge stitch at hero, a soft shadow down-right. |

**Tiers** (theme-system §2 tier contract):

| Tier | Size | What draws |
|---|---|---|
| Row | under 32 px | Flat shapes only, at most three masses and three tones per face. Bold strokes (frame crest line 2.6 units, gofun edge 1.8, Dalamud's cracks 3.6, "?" 15). No relief, no shadow, no badge. |
| Mid | 48, 64 px | Adds the relief lips and the one-pixel kirie shadow, the lacquer sheen and the horizon hairline. |
| Hero | 96, 128 px | Adds the kirikane hairlines (leaf edges, castle eaves), Kugane Castle and its mirror, the susuki hill, the cloud's silver contour, the ribbon's stitch and Dalamud's crazing (hibi). |

## States

| State | Face | Badge | Frame tier |
|---|---|---|---|
| **Ready** | **Dusk on the Kugane shore.** The sky is a Hiroshige *ai bokashi*: a band of deep indigo at the top opening quickly into pale twilight at the horizon. Medallion's crescent ((49, 42), r 29, k −0.18, limb 28° below level) in flat gofun, its dark limb drawn only as the crest's hairline. The sea is a second cut-paper plane; **the road is kirikane**: strips of gold leaf cut on a slant, two or three per row, under the lit centroid, longer and thicker toward the viewer (three rows at row size). At hero, Kugane Castle's tenshu stands on the far shore at the left (x 14–33, clear of the moon's horn and the badge), its eaves picked out in hairline leaf, its dark mirror below it. | Open padlock in gold leaf on an indigo lacquer seat; gilt ring | **Act now** (shared gilt) |
| **Ready on another job** | The same crescent at the same place and tilt, resting on a sumi-ai night with no sea and no road (R2). The earthlit disc is a lifted sumi plane edged with a gofun hairline. | The game's job icon on its role seat | Resting |
| **In journal** | Ready's shore at night: a slow bokashi, the same crescent, the sea, and the road laid in dull silver leaf (mid and hero only; at row size the night sea is bare, so it never reads as bars). An indigo silk ribbon wraps behind the medal's top left (arc r 66), lies over the frame and drops into the well over the moon's lower horn, ending in a swallowtail. | Open book: gofun pages, sumi script, an indigo cloth board and ribbon | Resting |
| **Blocked** | A new moon (72, 44, r 25): an earthlit sumi disc with a thin sunlit gofun limb (k −0.78, 11% lit), behind **a bank of cloud cut from tarnished silver leaf**: a scalloped crown of seven billows rising toward the moon, covering its lower quarter and **96% of the well's lower half** (R4 floor 30%). The crown is lit along its upper left and shadows the moon; at hero a silver kirikane contour runs just inside the edge, as a maki-e cloud is outlined. | Closed padlock in pewter on a sumi seat | Resting |
| **Done this cycle** | A waning half moon (60, 63, r 23), lit on the left, on its earthlit disc (a step lighter than the other states', so it never reads as a ◐ toggle). Along its dark edge a **strip of cut gold leaf** curls clockwise from the top (−112°) to the bottom (82°), widening from r 30 to 35.5, its tail cut to a point and its head a cut arrowhead pointing on round: the moon comes back. Short of a full turn and off-centre, so it is never an orbit ring. | – | Resting |
| **Completed** | **The moon-viewing moon.** A full moon (60, 60, r 31) in gofun gone cool and quiet (`#A4AABA`), two steps under Ready's moon. At hero, the hanafuda August image in sumi silhouette: a low hill at the lower left, **clear of the moon so it never reads as a phase**, and susuki blades and two ears standing from it into the moon's lower-left quarter, never above its equator (in front of it, never marks on it). The check is Medallion's mark and width in flat gold leaf, keylined, struck across the lower right and out past the rim. | – | Finished (dimmer) |
| **Locked out** | **Dalamud in shu lacquer**, broken to the black: a disc (r 38) of flat `#C4433F` parted into plates by zigzag cracks from an impact up and left (52, 50), every run at least 25° off each axis. The plates are pushed apart, so each crack is a gap over the black socket, never a drawn seam; two more cracks die inside their plates; one small plate at the lower-left rim has fallen out. Each plate sits at a slightly different tilt (one value step apart), with lit and shaded lips and one soft gloss. Row size shows the three widest cracks; hero adds hairline crazing. | – | Resting |
| **Not checked** | A faint moon (a lifted lacquer disc, r 27, `#35333E`, with a gofun hairline) under a **"?" brushed in gofun**: a thin, sharp entry at the moon's left horn, full width a third of the way round (the bowl is the moon's limb), thinning into the stem, and lifting off dry at the foot (three tines) at mid and hero; a solid stroke with a round tome at row size. The dot is a tiny full moon. Width is drawn, never textured. | – | Ghost (lowest; gofun edge .32) |

## The Kirikane kit

| Part | Design |
|---|---|
| **Act now** | The shared medal gilt (`#E6CF98` / `#9A7E4A` / `#7C6236` / `#5C4724`, specular `#FFF4D6`) in the kit's shape: a convex band from r 50.9 to 61.6 over the keyline to r 63.2, its crown line (r 56.8) a fine dark groove. |
| **Resting** | Warm ro-iro lacquer in the same band (lit slope `#3A363E`, body `#141216`, shaded slope `#060508`), the key light's soft reflection on the upper-left crown, a **crest line of kirikane** (gold leaf `#DEB862`, 1.1 units at hero, 2.6 at row) at r 56.8, and a gofun hairline (`#F2ECDD` at .34) at the outer edge. Hero: the leaf's cut edge catches the light on its upper-left run. |
| **Finished** | Resting, a step quieter: the crest line in tarnished leaf (`#A98843` at .8), the gofun edge at .26, a weaker sheen. |
| **Ghost** | Lacquer with no gold at all: the gofun edge at .32 and a faint sheen. |
| **Quiet** | The keyline (r 50.9–55.6) and one hairline (r 53.4–54.8): gilt, kirikane, tarnished kirikane, or gofun at .35. |
| **Badge ring** | Medallion's slot (centre 95, 95; keyline 24; ring 19.9–23.1; seat 19.9; shadow 1.4, 1.9). Gilt on act now; otherwise lacquer with a kirikane line at r 21.5 and a gofun edge. |
| **Seats** | Lacquer discs lit toward the upper left and shaded by the ring: open `#33508F` → `#121C3C`, closed `#2B3144` → `#0E1119`, journal `#3E5A9A` → `#1A2A57`; role seats are the fixed role colours. |
| **Glyphs** | Medallion's padlock and book outlines (so a mix reads one lock and one book), in this kit's materials: the open lock in gold leaf, the closed lock in pewter, the book in gofun on indigo. |

The frame's inner edge (r 50.9) covers the well (r 52.4) by 1.5 units, so any set's face sits in it without a gap (see `_mix.png`: Medallion's faces in this kit read as one medal with a lacquer rim).

## Mixing rules (critic R1–R5)

| Rule | Result |
|---|---|
| R1 one frame layer | Faces carry no frame or badge; every `under` layer is clipped to r 52.4. Overhangs (ribbon, check) are in `over`. |
| R2 Ready and RoJ move together | Both use Medallion's crescent exactly, as Ishgard Glass and Aether Crystal do, so a Ready from any set and a RoJ from this one show the same moon. |
| R3 gilt budget | Saturated gold only on Ready's road and frame, Done's arc and the check (each under 7% of the face). The resting frames' crest line is a hairline of leaf, the same in every state of a tier. Blocked's cloud is pewter, the ribbon indigo. |
| R4 Blocked shows its obscurer | 96% of the well's lower half (floor 30%); the cloud covers 23% of the moon's disc; 82% of the thin limb stays visible above it. |
| R5 row texture ceiling | The busiest row face is Locked out, with five cracks. Row faces have no hairlines, contours, grass, crazing or road on In journal. |

## Palette

| Group | Tokens |
|---|---|
| Keyline, sockets | `#07090F` |
| Ro-iro well | `#0E0C10` → `#060508` (every face but Ready's and In journal's dusk); gloss `#FFFFFF` at .07 |
| Ro-iro lacquer (frame) | `#3A363E` / `#141216` / `#060508`; sheen `#FFFFFF`; keyline `#050406` |
| Gofun | `#FFFBF1` / `#F2ECDD` / `#D6CFBE` / `#A29C8D`; lit moon (torinoko) `#F3EAD3`, its shaded lip `#B7AE97`; earthlit disc `#2E2C36` (Done's `#3A3843`, Not checked's faint moon `#35333E`) |
| Completed's moon | `#A4AABA`, lip `#CDD2DE` |
| Gold leaf | `#F4DA92` / `#DEB862` / `#A98843` / `#6E5426`; act now: the shared gilt ramp |
| Pewter (tarnished silver leaf) | cloud `#5C637A`, lit edge and contour `#A3AABC`, shade `#232736`; closed lock `#9AA1B4` |
| Shu lacquer (Dalamud) | lit `#DE6A50`, body `#C4433F`, shade `#7E2418`, lip `#F4A892`; plate tilts `#D0524A` / `#C94842` / `#BB3D3A`; socket `#080406` |
| Ai-zome silk | `#9DB5E8` / `#5C7DC6` / `#2E447F` |
| Ready's dusk (ai bokashi) | sky `#1B2652` → `#3F5797` (38%) → `#A3B8E4`; sea `#4660A6` → `#1B2852` |
| In journal's night | sky `#1D2848` → `#4A5F98`; sea `#2A3A6C` → `#111938` |
| Silhouettes (castle, susuki) | `#0B0F1F`, `#0A0D15` |

## Metrics (the build's own gates)

Measured by the current `tools/themes/build_themes.py` (1.17: faces, kits, the dark-palette gates), its own `build()` called with `_src/manifest.json` and `_src/kit-manifest.json` added to the shipped sets and kits, into a temporary root (`Tsukimichi/assets/` untouched). Units are round 5's (summed blurred luminance difference in a 40 px cell). **Every gate passes**, for the set and for the Kirikane kit.

| Gate (bar) | hero 48–64 (`_mid/`) | hero 96–128 | row (`_row/`) |
|---|---|---|---|
| G1 weakest pair, 16 px grey (≥ 12) | 14.5 Done-Lock | 13.7 Done-Lock | 14.3 Blk-Lock |
| G1 weakest pair, 16 px Vienot deut (≥ 12) | 15.4 | 14.5 | 14.8 |
| G1 weakest pair, 20 px grey / deut (≥ 16) | 25.6 / 27.2 | 24.8 / 26.4 | 23.8 / 25.2 |
| G1c weakest pair, 16 px, worst ground: Machado prot / deut / trit (≥ 11) | 13.1 / 15.5 / 14.5 | 12.4 / 14.7 / 13.7 | 15.5 / 14.8 / 14.3 |
| G2 Ready lead (≥ 1.3) | 1.70 (Rdy 72 / Jrn 43) | 1.65 (Rdy 70 / Jrn 43) | 1.67 (Rdy 76 / Jrn 45) |
| G2 Completed / Ready (≤ 0.8) | 0.57 | 0.58 | 0.56 |
| G2 every state but Not checked (≥ 15) | 28.3 (Done) | 28.4 (Done) | 33.2 (RoJ) |
| G2L Ready lead on Ishgard Snow, chroma (≥ 1.3) | – | – | 1.96 at 16 px, 1.65 at 20 px |
| G2L lightness floor (≥ 0.70) | – | – | 0.74 at 16 px, **0.71** at 20 px (next: RoJ) |
| G2L Not checked under Ready, chroma (< 1) | – | – | 0.07, 0.08 |
| G2D Dawn and Kugane Lacquer, 16 and 20 px: Ready lead (≥ 1.3) / Completed ÷ Ready (≤ 0.8) | ≥ 2.58 / ≤ 0.39 | ≥ 2.55 / ≤ 0.39 | ≥ 2.58 / ≤ 0.38 |
| G2D mixes with Sumi's Ready, dark palettes (≥ 1.25) | – | – | ≥ 1.77 |
| Fit (medals, row, faces, faces-row; no bleed, edge alpha ≤ 254) | ok | ok | ok |

The weakest pair is Done this cycle against Locked out at the hero tiers and Blocked against Locked out at row size. The thinnest colour-vision reading is protanopia at the 96–128 tier, 12.4.

Salience at 16 px grey on Night (row): Rdy 76, RoJ 33, Jrn 45, Blk 36, Done 34, Comp 43, Lock 35, NotC 25.

**The Kirikane kit** passes with its own set. Other sets' faces in it are a frames choice and only warn, as the tool intends. Aether, Orrery and Medallion miss the light-palette lightness floor in it (0.66–0.69), because black lacquer rings make their dark states heavier on Snow. Medallion's Blocked vs Locked out falls to 11.75 (protanopia 10.88).

**Cross-set table** (in the neutral Brass kit; warnings, never gates):
- Two pairs are "close": Sumi's In journal beside Ishgard's or Medallion's Ready on another job, 11.7 and 11.75 at the hero tier.
- Ready from another set leads Sumi's other states by 1.35–1.64.
- Ready from Sumi leads Ishgard's by 1.26–1.28, but only 1.17–1.24 against Aether, Orrery and Medallion, which warns.

**Package** (as built): medals, row and faces PNGs 1.98 MB on disk (budget 2.5 MB).

## Iterations

1. **First build.** Ready's salience was only 1.0–1.09× Completed (the dark sky gave it nothing) and RoJ-Jrn measured 8.4 (In journal was a crescent on a dark sea plus a ribbon). Blocked's mist was a stack of grey slabs; the full moon was beige (round 1's "cracker" read); the susuki plumes read as eyebrows.
2. **Salience.** Ready's sky became a lit dusk bokashi and Completed went cool and smaller (r 31). In journal lost its silver strips at row size (two parallel strips would be the banned hamburger) and its night was lifted a step. RoJ-Jrn rose from 8.4 to 15.2 (16 px grey, hero).
3. **Blocked.** Egasumi bands (stepped slabs, then lobed bands) read as terraces and as parallel bars; replaced by one scalloped kirie cloud with a silver contour at hero.
4. **Locked out.** The first cracks bent gently and read as a ball's seams at 28–48 px. They now bend sharply, run at least 25° off every axis, and the plates part to show the socket. Shu moved a step toward orange; with the wider sockets, Done-Lock under protanopia at the 96–128 tier rose from 10.5 to 11.9.
5. **Completed.** The plumes were cut; the hanafuda hill was lowered so it never touches the moon (it had read as a phase shadow).
6. **Ready.** The dark-disc earthshine was removed at dusk (a dark disc on a lit sky read as a hole); the sky became Hiroshige's two-stage bokashi (deep band, quick opening), which gives Ready its own look apart from Medallion's lapis day.

## Doubts

- **Ready in a mix.** Sumi's Ready (70–76) is still quieter than Medallion's (82–85). Brightening it costs the light-palette lightness floor one for one, and the floor is now 0.71 at 20 px (bar 0.70), a thin margin a Chrome update could tip. In the neutral Brass kit, Sumi's Ready over Aether's, Orrery's or Medallion's other states warns (1.17–1.24).
- **Shu against Medallion's crimson.** The brief's body `#C4433F` sits only ΔE 0.03 (OKLab) from Medallion's `#C24A58`; the supervisor's `#C4432F` is 0.05 and the critic's `#C9454A` 0.02, so no colour in that range is "clearly apart" by hue alone. The two Dalamuds are apart by hue at the ends (lit hue 34° vs 16°) and by material: flat plates parted over a black socket, against Medallion's shaded sphere of shards. Against the plum Locked-out word on Snow (`#9B2C6E`) it is ΔE 0.13.
- **Locked out at 16 px** reads as a red disc parted by a dark zigzag "Y", with the chipped notch at the left. No ball seams remain, but at one pixel per gap it is a broken plate only to a close look; the plate tilts and the chip carry it at 20–28 px.
- **The kasure tines** on the "?" are crisp slits, not a bristle texture. If the owner sees noise in them, they come out with one constant (the `slits` list in `question_mark()`).
- **The moon's relief shadow** is now short and masked off its own disc. A reviewer may still want the scene's moon unshadowed, as Medallion's is (cost: none to the gates).
- **Plain at hero tiers** has no badge (the row badge glyphs serve beside a row). A `plain.*` hero atlas would need flat badges; the build tool writes neither yet.
- **Integration (done in 1.17 T15).** The manifests are `tools/themes/sets/sumi-to-kinpaku.json` and `tools/themes/kits/kirikane.json`; the set builds into `Tsukimichi/assets/ui/themes/sumi-to-kinpaku/` (its row strip carries the `plain` finish) and the kit into `assets/ui/kits/kirikane/` (with `ornaments.png`), and `ThemeAtlasTests` lists them.

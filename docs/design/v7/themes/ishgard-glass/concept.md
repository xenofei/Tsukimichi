# Ishgard Glass (v7 theme)

Round 2's concept C, revived as a full theme for the v7 theme picker. Each quest state is a small leaded-glass roundel from the Holy See. Jewel glass glows from behind, the lead came holds it, and a cusped ring of Ishgard stone tracery frames it. The meanings, the silhouette (r 63.2) and the badge slot are Menphina's Medallion round 5's, so a player can use this whole theme or mix single states from both.

**Generator:** `_src/gen.py` writes the faces, the Came kit and the composites. `_src/mix.py` renders `_mix.png`.
- Run `python _src/gen.py`, then `python _src/mix.py`.
- Both read Medallion's sources read-only:
  - `gen.py` reads the three job PNGs from `moon-v6/round5/medallion-r5/_src/jobs/`;
  - `mix.py` imports `gen5.py` to get Medallion's faces and its Brass kit.

## Files

| File | What it is |
|---|---|
| `faces/<state>-under.svg` | The **unframed face**: the well and its glass, clipped to the shared well (centre 64, 64, r 52.4). Ready on another job's face has an empty seat. |
| `faces/<state>-over.svg` | The face's **overhangs**, drawn above the frame: In journal's silk ribbon and Completed's gilt check. The other six are empty. |
| `_row/faces/…` | The same two layers for the row tier (under 32 px) |
| `kit/frame-<tier>-full.svg`, `kit/frame-<tier>-quiet.svg` | **The Came kit**, at four urgency tiers: `act-now` (the shared medal gilt), `resting`, `finished` and `ghost` (Ishgard stone, each a step quieter). `kit/_row/` holds the row-tier versions. |
| `kit/badge-ring-act-now.svg`, `kit/badge-ring-resting.svg` | The badge keyline, shadow and ring. Gilt on Ready, stone elsewhere. |
| `kit/badge-seat-<open, closed, journal, tank, healer, dps>.svg` | The glass seats |
| `kit/glyph-open.svg`, `glyph-closed.svg`, `glyph-journal.svg` | The leaded-glass badge glyphs, at the badge position in the 128 box |
| `ready.svg` … `not-checked.svg`, `ready-on-another-job-<job>.svg` | **Composites**, built as face under, then kit frame for the state's tier, then kit badge, then face over. They use the same names as `medallion-r5`. |
| `_row/<state>.svg` | Row-tier composites, with no badge |
| `_row/badge-open.svg`, `badge-closed.svg`, `badge-journal.svg` | The badge glyphs at text height, for drawing after a row-tier medal |
| `_sheet.png` | The standard sheet of the composites (`moon-v6/round5/render_sheet.py`) |
| `_mix.png` | The swap test, at 96, 48 and 20 px, in four rows:<ol><li>Glass faces in the Came kit</li><li>Glass faces in Medallion's Brass kit</li><li>Medallion's faces in the Came kit</li><li>Medallion as shipped</li></ol> |

## Review fixes (round 2)

The supervisor's second ruling had one required item (Locked out's hole) and one optional item (Completed's leads). This section supersedes the Locked out and Completed rows of the round-1 table below.

| # | Fix | Done |
|---|---|---|
| Required | **Locked out: a hole, not a bird or a hand** | **The hole.** The void is now one compact kite: the inner parts of two adjacent lights (between the cracks at −14°, 31° and 84°) have fallen out.<ul><li>**Sides:** three straight radial cracks from the impact point (59, 57). The jogs that stair-stepped them are gone.</li><li>**Outer edge:** two gently curved cross breaks at 0.70 R from the moon's centre, each bowed 2.2 units toward the impact point.</li><li>**Shape:** no stair steps; nothing narrower than 4 units; the gap is far over 12 units.</li><li>**The outer strips** of both lights still hang in their lead, so the void never reaches the rim.</li></ul>**Remove the fingers.** The shards beside the hole barely move (1.3 units), so no dark arm opens beside it.<br>**The falling shard** is now a broken-off splinter: five irregular sides, nowhere under 4 units across. It tumbles below the hole and just past Dalamud's rim (centre (72, 103), turned −20°), entirely inside the window. It is the ruby a step lighter, its broken edges lit where they face the key light. The earlier versions were a hinged lower-left shard, which left a dark arm, and a neat triangle, which read as an arrow.<br>**Colour.** Ruby moved one small step lighter to hold row-tier Blocked vs Locked out at 12 or more: `#E46E7E` / `#CA4C60` / `#7E2236`. |
| Required (guard) | **Blocked vs Locked out ≥ 12 at row size** | The tighter hole first dropped it to 10.3. Four changes brought it to 12.1:<ul><li>the hole at 0.70 R;</li><li>Blocked's lighter cloud piece widened to r 40 about the moon;</li><li>the slightly lighter ruby;</li><li>the falling splinter placed where Blocked's window is dark.</li></ul> |
| Optional (taken) | **Completed: leads along the maria** | The rim-to-rim arcs are gone. The mare glass is one piece with a single lead along its outline: a smooth closed curve through 16 hand-placed points:<ul><li>Oceanus Procellarum, running off the west limb;</li><li>the round top of Imbrium;</li><li>Serenitatis;</li><li>Tranquillitatis' eastern shore;</li><li>Fecunditatis;</li><li>the bays of Nectaris and Nubium along its southern edge.</li></ul>**Ties.** Two short ties to the north and south limb split the highland glass, as a glazier must hold a lobed piece. That makes four lights:<ul><li>the maria in `#9AA6C6`;</li><li>three highland lights in `#C8D2E8`.</li></ul>**Detail.** Both tones are flat. Hero adds hatching at 0.15. The row tier has no lead round the maria (rule R5).<br>**Rejected.** Two other mare builds read as a cartoon cloud (a union of circles) and as cheese spots (separate ellipses). The single outline reads as the moon's face with no ball, pie or phase. |

**Metrics, round 2** (`moon-v6/round5/metrics.py`, composites)

```
hero (with badges)
glass 16px grey:      weakest RoJ-Jrn 12.6, Blk-Lock 13.7, RoJ-Done 13.7 | salience Rdy=77 RoJ=46 Jrn=55 Blk=44 Done=47 Comp=56 Lock=41 NotC=35
glass 16px deut+grey: weakest RoJ-Jrn 12.5, RoJ-Done 13.8, Blk-Done 13.9
glass 20px grey:      weakest RoJ-Jrn 20.3, Blk-Done 23.6, Blk-Lock 23.6 | salience Rdy=120 ... Comp=86
with Bard as RoJ: 12.9;  with White Mage: 12.5
row tier (no badges)
row 16px grey:        weakest Blk-Lock 12.1, RoJ-Done 12.2, Blk-Done 12.5 | salience Rdy=87 RoJ=43 Jrn=55 Blk=44 Done=46 Comp=57 Lock=40 NotC=35
row 16px deut+grey:   weakest RoJ-Done 12.4, Blk-Lock 12.6, Blk-Done 12.6
row 20px grey:        weakest Blk-Lock 20.3, RoJ-Done 21.3, Blk-Done 22.5 | salience Rdy=135 ... Comp=88
```

| Target (16 px grey) | Hero | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 12.6 | 12.1 |
| Ready ≥ 1.3× the next state | 77/56 = 1.38 | 87/57 = 1.53 |
| Completed ≤ 0.8× Ready | 0.73 | 0.66 |

**Round-2 doubts**
- **Blocked vs Locked out at row size is 12.1, just over the guard.** Any further darkening of either face needs re-measuring.
- **The falling splinter is a separate piece in front of the window and casts no shadow,** per the glass rule. It reads as a loose shard rather than one of the hole's two lights.

## Review fixes (supervisor S1–S2, G1–G2; critic changes 3–6)

This section supersedes anything below it that conflicts. It responds to `../supervisor-review.md` and `../critic.md`.

| # | Fix | Done |
|---|---|---|
| S1 | **Split faces from frames** | Every face now sits on Medallion's well: centre (64, 64), r 52.4, with everything under it clipped to that circle (R1). Faces are emitted in two layers, `under` and `over`.<br>**The Came kit** is emitted separately:<ul><li>four urgency tiers, each at Full and Quiet, in hero and row versions;</li><li>the badge ring;</li><li>six seats;</li><li>three glyphs.</li></ul>**Overlap:** the kit frame's inner edge sits at r 50.9, so it covers the well's edge by 1.5 units.<br>**Rebuilt as composites:** the state SVGs and `_sheet.png`.<br>**`_mix.png`** shows the swap both ways, with no gaps and one rim per row. |
| S2 | **Act now is gilt** | Ready's frame is the Came kit's turned ring and twelve cusps in the shared medal gilt, with a gilt inner came:<ul><li>ramp `#E6CF98` / `#9A7E4A` / `#7C6236` / `#5C4724`;</li><li>specular `#FFF4D6`.</li></ul>Ready's badge ring is gilt too. This restores the warmth the dropped afterglow took away.<br>The resting tier is the Ishgard stone. Finished and ghost are each a step darker, and ghost has no catch-light. |
| Rule | **Glass takes no cast shadows and no front-lit bevels** | Kept throughout:<ul><li>Only lead, stone, gilt, the badges, the silk ribbon and the gilt check are front-lit.</li><li>The ribbon's and the check's shadows are masked so they fall on the frame only, never on glass.</li></ul> |
| G1 | **Blocked's clouds are glass** | No gradient, no grisaille, no shadow. Each cloud is two flat glass tones:<ul><li>body `#5A6690`;</li><li>a lighter `#8A96BE` piece on the edge nearest the moon (the backlight through thinner cloud).</li></ul>**The dividing came:**<ul><li>Front bank: an arc concentric with the moon (r 37 about its centre).</li><li>Back bank: an arc about the limb (r 21 about (97, 38)).</li></ul>Each cloud keeps its lead outline.<br>**R4:** the banks cover 48% of the well's lower half (the floor is 30%). |
| G2 | **Completed is four panes** | Three curved came (one from rim to rim, two branching to the rim) cut the moon (r 32) into four lights:<ul><li>`#9AA6C6` mare, on the north-west and south-east;</li><li>`#C8D2E8` highland, on the north-east and south-west.</li></ul>The tones are flat with no blur. The dark lights sit on a diagonal, so no phase or terminator can be read into them.<br>Hero adds grisaille hatching on the mare lights at 0.15, and halation is removed.<br>The check is now Medallion's **applied gilt check**: keylined and front-lit, struck out past the lower-right rim, in the `over` layer, as the grammar asks. |
| C3 | **Done: no clock** | The spokes behind Done are gone, and the field is one quiet light. The half moon and the amber arrow carry the glass. The arrow's radii are now 31–38.5, so its head (17 wide) clears the cusps. |
| C4 | **Locked out: a hole, not a slice** | The void is now the **inner parts of two lights** that fell out. Their outer strips still hang in the lead at the rim, so the night shows through *inside* the ring of shards and never reaches the rim.<br>The break between strip and hole is an angular zigzag, like glass, never a smooth profile. Each void gap is 12 units or more. The falling shard is unchanged. |
| C5 | **The Ishgard skyline** | It sits on Ready's far shore at the hero tier, left of the road and clear of the badge (x 15–48, at most 9.8 units tall):<ul><li>Saint Reymanaud's spire between two west towers, the Vault's lower roofs and a gate tower;</li><li>one piece of dark slate glass, leaded on its outline;</li><li>a bead of lit amber glass for its small rose window.</li></ul>**Reflections:**<ul><li>a grisaille dark mirror of the same height, softened 0.5;</li><li>the window's warm reflection directly below it, at mirror distance.</li></ul>**To pay for it,** the halo rose is cut from nine spokes to seven. |
| C6 | **The skyline at night** | In journal shows the same skyline at low contrast (`#24336A` on the night sky, its window at 0.7). It is hero only. |
| Grammar | **The ribbon hangs over the rim** | In journal's bookmark is now a **silk ribbon** in the `over` layer:<ul><li>its top wraps behind the medal (an arc at r 66);</li><li>it lies over the frame and drops into the well over the moon's lower horn;</li><li>lit edge on the left, a stitch line at hero;</li><li>it shades the frame where it enters the well.</li></ul>It replaces the leaded banderole, which could not overhang. |
| Mixing | **Same moon as Medallion** | Ready's, Ready on another job's and In journal's crescent is now Medallion's exactly: (49, 42), r 29, k −0.18, limb 28° below level. Its upper-left limb runs under the frame, as Medallion's does under its bezel. So a Ready from one set and a Ready on another job from the other show the same moon (critic pairing 5). |

**Mixing rules R1–R5**
- **R1:** no face pixel lies outside r 52.4 except the declared overhangs.
- **R2:** Ready and Ready on another job share the identical crescent.
- **R3:** saturated gold appears only on Ready's frame, Done's arc and the check.
- **R4:** Blocked's obscurer covers 48% of the well's lower half.
- **R5:** row faces have at most 4 internal came inside any moon.

**Metrics after the fixes** (`moon-v6/round5/metrics.py`, composites)

```
hero (with badges)
glass 16px grey:      weakest RoJ-Jrn 12.6, RoJ-Done 13.7, Blk-Lock 13.8 | salience Rdy=77 RoJ=46 Jrn=55 Blk=43 Done=47 Comp=57 Lock=39 NotC=35
glass 16px deut+grey: weakest RoJ-Jrn 12.5, RoJ-Done 13.8, Blk-Done 14.1
glass 20px grey:      weakest RoJ-Jrn 20.3, Blk-Done 23.8, RoJ-Done 24.0 | salience Rdy=120 ... Comp=88
with Bard as RoJ: 12.9;  with White Mage: 12.5
row tier (no badges)
row 16px grey:        weakest RoJ-Done 12.2, Blk-Lock 12.4, Blk-Done 12.5 | salience Rdy=87 RoJ=43 Jrn=55 Blk=44 Done=46 Comp=57 Lock=39 NotC=35
row 16px deut+grey:   weakest RoJ-Done 12.4, Blk-Done 12.6, Blk-Lock 12.7
row 20px grey:        weakest RoJ-Done 21.3, Blk-Lock 21.8, Blk-Done 22.2 | salience Rdy=135 ... Comp=89
```

| Target (16 px grey) | Hero | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 12.6 | 12.2 |
| Ready ≥ 1.3× the next state | 77/57 = 1.35 | 87/57 = 1.53 |
| Completed ≤ 0.8× Ready | 0.74 | 0.66 |

**Tuning needed along the way**
- **Flat, darker clouds dropped Blk-Lock to 8.5** at the row tier. Two changes brought it back:
  - the light cloud piece became the arc concentric with the moon, so the bank's upper billows are the lighter glass;
  - Locked out's void became two inner lights.
- **The brighter highland glass pushed Completed to 0.83× Ready.** Three changes brought it back: the moon shrank from 35 to 32, its halation was removed, and the mare lights got the larger share.
- **Ready's lead fell to 1.305× at the hero tier.** The finished tier (Completed's frame) moved one step darker (`#9EA4B4` → `#1E2232`), as the grammar's "finished (dimmer)" asks, which restored it to 1.35×.
- **Ready's glass was lifted one step** (sky `#5687DA` → `#97BBF3`, halo 0.15) to hold its 1.3× lead.

**Review-fix doubts**
- **Ready's hero lead (1.35×) rests partly on Completed's dimmer finished frame.** In a kit whose finished tier is as bright as its resting one, it would drop to about 1.3×.
- **Completed can still suggest a ball** at 48–96 px: four lights, diagonal tones and curved seams. The cames are now gentle bows, and the check covers a seam, but a juror may still say "tennis ball".
- **Locked out's hole at 16–20 px** is a dark angular shape inside a red ring. It reads as a hole, but its outline has an arrow-like point toward the upper right.
- **Supervisor optional 1 is not done:** rose spokes only from 64 px. The set has two tiers (hero from 32 px and row under 32 px), so 48 px still shows the seven spokes. That would need a third, mid tier.
- **The kit frame's inner edge (r 50.9) is wider than Medallion's well edge.** Medallion's faces in the Came kit lose about 1.5 units of well at the rim, and the cusps sit over their moons' edges. In `_mix.png` this reads as tracery in front of the scene, which is acceptable.

## The material, and its two lights

The supervisor's rule is one key light. Glass needs a second source, so the two are kept strictly apart:

- **Transmitted light (from behind)** sets every glass value. Pale glass glows and deep glass is dark. The key light never shades a pane.
  - Tone inside a pane comes only from **grisaille**, the glazier's brown-black vitreous paint, always applied soft:
    - the moon's maria;
    - the matting from terminator to limb;
    - the clouds' undersides;
    - the shading inside the question mark;
    - the keyhole and the book's lines of script.
- **The UI key light (from the front, upper left)** lights only what is opaque:
  - **Came:** each came is a raised H-section, light on its upper-left flank and dark on its lower-right flank.
  - **Stone frame:** convex, with a light outer slope at the upper left and the inner slope reversed. It is matte, so its catch-light is broad and faint.
  - **Badge:** the same frame, casting the standard down-right shadow.
  - **Badge content:** the glass glyphs carry a soft under-glow.
- **Halation** (hero only): bright glass seen against dark lead spreads over the lead's edges, so the came over the moon look slightly eaten by light. It is a soft copy of the brightest glass over its own came, at 0.14–0.28.
- **No textures.** There are no seeds, bubbles or spots, so the glass never reads as a crater. Pane-to-pane variation is a 4–16% tint per light.

## Tiers: how the glass survives at row size

Round 2's came were 1.1–1.3 units (0.15 px at 16 px) and disappeared. Here every came is planned per tier:

| Tier | Primary came (subject outlines, horizon, frame seat) | Secondary came (cuts inside a subject, rose spokes, waves) | Hero-only extras |
|---|---|---|---|
| Hero (32 px and up) | 2.5 units, with lit and shaded flanks | 1.45 units | cusps, grisaille, halation, hairline cracks, fold band |
| Row (`_row/`, under 32 px) | 3.4 units (0.43 px at 16, 0.53 at 20, 0.74 at 28), dull lead only | none | none |

**What the row tier keeps.** These are the leaded outlines of the moon's whole disc, the crescent, the horizon, one wave, the clouds, the arrow, the check, the question mark and its bullseye dot, and the stone ring with its dark seat line. The outlines of bright subjects are thinned to 0.6× (2.0 units) so they frame the light instead of eating it.

**How it reads at 16–28 px** (the zoom renders are in the scratchpad):
- Each state reads as dark-leaded jewel glass in a pale ring.
- The moon's disc is leaded whole, so the crescent always reads as a moon and never as a sail.
- The road reads as a broken bright column.

## States

| State | Window | Badge |
|---|---|---|
| **Ready** | **The moon-road window by day.** Deep azure glass brightens toward the horizon. A rose of paler halo glass surrounds the moon, cut by nine spokes into lights that alternate in tint, and every other spoke runs on to the tracery. The crescent (r 27.5, k −0.2, its limb facing 28° below level) is cut into three lights and matted from terminator to limb, with soft near-limb maria. The unlit disc is one piece of the halo's glass, the same value as the sky, as a real dark limb is. The sea is cut by three wave came. **The road is abraded flashed glass:** the sea is blue flash on pale glass, and the glints are ground through it, as medieval glaziers did. So the road needs no lead and cannot become a tree or paving stones. There are 12 needle glints (aspect 4.6 or more) under the lit centroid x 62.4. They foreshorten toward the horizon, brighten toward the viewer (the mirror point is at the bottom edge), and peak at 0.88× the moon. | Open padlock in silver-stain amber, on an azure glass seat |
| **Ready on another job** | **The same moon at rest:** night glass with no sea and no road. The disc is one piece of earthlit ash glass, and the halo rose sits at low contrast. | The game's job glyph, optically centred (Medallion's method), on a role-colour glass seat |
| **In journal** | **Ready's window at night:** a lighter night sky and sea, with the road at 0.78× in moonstone. A banderole of tide glass hangs from the tracery over the moon's lower horn, with a swallowtail and a darker fold band near the top. Because it is leaded into the window, it stays inside the silhouette. | Open book: two pages of pale amber glass, a spine came, grisaille script and a small tide-glass ribbon, on a tide-glass seat |
| **Blocked** | **A new moon behind cloud.** An ashen disc with only a thin sunlit limb of moon glass (k −0.62). Two clouds of smoky streaky glass sit over it, each leaded on its outline with one interior came, and grisaille matting runs along their flat undersides. The moon is leaded before the clouds are set over it, so no came crosses a cloud. The back bank hides the disc's middle. The front bank sits low and wide, filling the lower window where no other state has light. The cloud tops are dimmer than the limb, so the limb stays the only light. | Closed padlock in cool moonstone glass, on a night glass seat |
| **Done this cycle** | **A waning half moon,** lit on the left, with a straight came as its terminator and soft western maria. A repeat arrow of silver-stain amber spirals clockwise round it, from radius 32 to 39.5, open at the bottom so it never closes into an orbit ring. The arrow is cut into three lengths of glass, and its stain is denser along its inner edge. | None |
| **Completed** | **The full moon:** one roundel of moonstone glass (a glazier would cut a disc this size in one piece) with soft grisaille maria in their real layout. Its value is held under Ready's. The background spokes radiate from the moon like a glory. A gold check of silver-stain amber, leaded, crosses the lower right and is cut once at its elbow. | None |
| **Locked out** | **Dalamud's red glass, shattered:** the impact point is up and left of centre, and seven shards are split along cracks that bend twice. One light has fallen out, and the night behind the window shows through as near-black. One shard is falling: 7 units out and turned 11°. The shards keep their lead only on the limb. Crack faces turned toward the key light carry a pale refracting lip, and faces turned away are dark. Hero adds hairline cracks that did not run through, a streaky denser band in the ruby, and a slightly different light per shard as each tilts out of plane. | None |
| **Not checked** | **A veiled roundel of smoky glass,** whose lit limb is the question mark. The bowl is a crescent-shaped strip of moon glass, cut once where it turns into the stem. The dot is a bullseye of crown glass, thicker at its centre, a tiny full moon. | None |

## Badge slot (shared with Medallion r5)

The geometry is identical, so badges line up when states from the two themes are mixed:
- centre (95, 95);
- keyline r 24;
- two-slope ring r 19.9–23.1;
- seat r 19.9;
- drop shadow (1.4, 1.9, sd 1.1, .65).

Only the material changes:
- **Ring:** the frame's stone instead of gilt.
- **Seat:** a disc of glass, brightest toward its centre where the light comes straight through, with the ring's front-light shade on its upper-left edge and a lead seat line.
- **Job glyphs:** unchanged; the runtime spec in `medallion-r5/concept.md` applies.
- **Row tier:** draws no badge. The glyph is drawn at text height after the medal, as in Medallion.

## Round 2's failures, and what fixes each

| Round 2 failure | Fix |
|---|---|
| The came vanish below 48 px, leaving plain discs | A planned row tier: primary came at 3.4 units. Every subject is leaded whole (disc, crescent, clouds, arrow, check, "?"), and the stone ring and its seat line frame every state. At 16–20 px the windows read as leaded glass, not discs. |
| Locked out read as a no-entry sign (red disc, white band) | No band. The ruby moon is shattered, with one light gone to black and one shard falling. |
| Ready read as a lollipop (a disc above a column of shards) | The moon sits in a full sky-and-sea window with a horizon. The road is abraded glints inside the sea, not a stack of shards under a disc, and the crescent's whole disc is leaded so it reads as a moon. |
| Not checked was nearly invisible on daylight | Every state now has the dark keyline and stone ring, and Not checked has a bright moon-glass "?". It holds on the daylight swatch (see the sheet). |
| Ready on another job read as an AI sparkle (a crystal) | The crystal is gone. The game's own job glyph sits in the shared badge. |

## Palette

| Group | Tokens |
|---|---|
| Lead came | `#2A2E3B`, lit flank `#A2A9BC`, shaded flank `#0C0E15` |
| Ishgard stone | `#C3C9D6` / `#8C93A8` / `#626A84` / `#3E4560` / `#232839` |
| Grisaille | `#2A2430` |
| Moon glass | `#F7F4EA` / `#E4E9F4` / `#C6D0E6` / `#98A8CB` / `#62729B` |
| Day azure (Ready) | sky `#4C7FD6` → `#93B8F2`, sea `#5E8DD4` → `#3459A3` |
| Night (In journal) | sky `#22346E` → `#3E5A9E`, sea `#33509A` → `#1B2B66` |
| Resting night | `#18244E` → `#121B40` (Ready on another job); field `#1C2A5A` → `#121B40` |
| Earthlit ash | `#46528A` → `#252E55` |
| Silver-stain amber | `#FBE3A0` / `#E9BE68` / `#C4913F` / `#86591E` |
| Dalamud ruby | `#DE6274` / `#C6465B` / `#7E2236`, lip `#F7B6BE`, void `#06070C` |
| Smoky cloud glass | `#BEC5DD` / `#9199BC` / `#646D93` |
| Tide glass | `#B4C5EE` / `#7E9BDC` / `#45609E` |
| Badge seats | open `#6592E0` → `#22407E`, closed `#33406E` → `#121A36`, journal `#6A86C8` → `#26407C`; role seats as Medallion |

## Metrics

From `moon-v6/round5/metrics.py`:

```
as shipped (hero, with badges)
glass 16px grey:      weakest RoJ-Jrn 12.4, Lock-NotC 12.6, Blk-Done 13.6 | salience Rdy=75 RoJ=41 Jrn=50 Blk=42 Done=43 Comp=56 Lock=35 NotC=37
glass 16px deut+grey: weakest RoJ-Jrn 12.4, Lock-NotC 12.9, Blk-Done 13.6
glass 20px grey:      weakest RoJ-Jrn 20.5, Lock-NotC 22.1, RoJ-Done 23.5 | salience Rdy=116 ... Comp=87
with Bard as RoJ:       weakest 12.6;  with White Mage: 12.3
row tier (_row, no badges)
row 16px grey:        weakest Lock-NotC 12.6, RoJ-Done 12.9, Blk-Lock 13.1 | salience Rdy=79 RoJ=37 Jrn=50 Blk=43 Done=43 Comp=54 Lock=35 NotC=36
row 16px deut+grey:   weakest Blk-Lock 12.9, Lock-NotC 13.0, RoJ-Done 13.1
row 20px grey:        weakest RoJ-Done 22.3, Blk-Lock 22.5, Lock-NotC 22.7 | salience Rdy=125 ... Comp=85
```

| Target (16 px grey) | Hero | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 12.4 | 12.6 |
| Ready ≥ 1.3× the next state | 75/56 = 1.34 | 79/54 = 1.46 |
| Completed ≤ 0.8× Ready | 0.75 | 0.68 |

## Iterations

1. **First build** (the rose halo, the cusped frame, glass glyphs). Problems:
   - The background spokes were drawn over the subjects.
   - The road was one tapering leaded piece and read as a fir tree.
   - RoJ-Jrn measured 5.4.
2. **Layering and road.**
   - Each field's came are now drawn before its subject.
   - The road became abraded flashed glass.
   - Completed lost its four-way cuts, which had read as a tennis ball.
   - Ready on another job is darker and In journal lighter.
   - Row came went from 4.2 to 3.4, with the outlines of bright subjects at 0.6×.
3. **Distinctness at row size.**
   - The row-tier crescent lost its cut, which had read as a sail, and gained its leaded disc.
   - Blocked's front bank moved low and wide, into the lower window that no other state lights. Blk-RoJ went from 10.2 to 13 or more.
   - The arrow and the "?" are bolder in the row tier.
   - Ruby was tuned between crimson and salmon.
4. **Realism pass.**
   - The moon is leaded before the clouds, so no came crosses a cloud.
   - The earthlit discs are opaque, single pieces.
   - The road is capped at 0.88× the moon.
   - Ready's afterglow pane was removed: as a leaded piece it read as a sand dune, and silver stain on blue glass would turn green.
   - The cloud grisaille is limited to the undersides.
   - The ruby gained hairline cracks and a streaky band.

## Doubts

- **No afterglow.** Ready loses Medallion's warm horizon note. The open lock's amber is now its only warm mark.
- **The stone ring is cooler and greyer than Medallion's gilt.** In a mixed set, a glass state beside a medal state shows two frame materials. The silhouette and the badge slot match, but the rims do not.
- **Ready's halo rose is busy at 48 px:** nine spokes plus the halo ring and the moon's three lights. It is calmer at 96 and up. Two fewer spokes would quiet it at a small cost to the "rose window" read.
- **Completed's moon is a single uncut roundel.** It is correct for the meaning, but it is the one state whose glass-ness rests on the frame, the leaded outline, the check and the spokes rather than on cuts in the subject.
- **Margins are thin:** In journal vs Ready on another job is 12.3 with White Mage, and Lock-NotC is 12.6.
- **The two light sources (transmitted versus front) are a stated convention.** A supervisor may want each glyph checked against it.
- **No plugin icon was made.** The brief asked only for state glyphs. Round 2's Ishgard skyline remains available for one.

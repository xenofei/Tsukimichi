# Astrologian's Orrery (v7 theme)

Round 2's concept B, revived as a full theme beside Menphina's Medallion. The state meanings and the badge system are unchanged, so a user can take the whole theme or mix single states into another one.

## Pitch

Every state is a moon seen through a small Sharlayan astrolabe, in brass and starlight on Prussian blue: the palette of an antique celestial globe or star atlas.

- **The rim is the instrument's limb.** It has a bright rounded lip, a darker flat face of old brass engraved with a double rule and four cardinal notches (the moon's four quarters), and a step down into the plate.
- **The plate is night lacquer.** Engraved constellations sit in its empty sky.
- **Starlight is the light.** Stars are round points with a soft glow, never four-point sparkles.

The Medallion is champagne gilt on lapis enamel, a struck medal. The Orrery is two-tone brass on Prussian blue, an instrument. The two should be told apart at 28 px and up.

**Generator:** `_src/gen.py` writes every SVG here (faces, the kit, composites and the mix check). `_src/sheets.py` renders `_mix.png` and `_kit.png`. The generator reuses the Medallion r5 builders where the physics was already approved: moon phase, crescent shading, road rows, shard lighting and job-icon centring. It reads the game job icons from `moon-v6/round5/medallion-r5/_src/jobs/` without copying them, and imports `gen5.py` read-only for the Brass kit and the Medallion faces in the mix check.

## Files

| File | What it is |
|---|---|
| `hero/<state>.svg`, `hero/<state>.over.svg` | **Unframed faces** (theme-system §3.2). The under layer is the well and emblem, clipped to the shared well (centre 64, 64, r 52.4), so no pixel falls outside it (rule R1). The over layer holds the overhangs drawn above the frame: In journal's ribbon and Completed's check. It is empty for the other six states. |
| `mid/<state>.svg`, `mid/<state>.over.svg` | The 48 and 64 px tiers: hero craft, but with the row tier's smooth tone on every lit moon and no hatching (round 2). `hero/` is then the 96 and 128 tiers (and 2×). |
| `row/<state>.svg`, `row/<state>.over.svg` | The same faces at row tier, with no hairline craft. |
| `kit/frame-<tier>-full.svg`, `kit/frame-<tier>-quiet.svg`, `kit/row/frame-<tier>-full.svg` | **The Astrolabe kit** (§3.3), in four tiers: `act-now`, `resting`, `finished` and `ghost`. Full has the hero engraving; the row frame is clean; Quiet is a hairline ring. These files belong in `v7/themes/kits/astrolabe/`. I kept them here because this task may not write outside this folder. |
| `kit/badge-ring.svg`, `badge-ring-act-now.svg`, `badge-seat-{open,closed,journal}.svg`, `badge-seat-role-mask.svg`, `badge-{open,closed,journal}.svg` | The kit's badge parts, at the shared slot (95, 95). The role-seat mask is white, for tinting at runtime. |
| `ready.svg` … `not-checked.svg`, `ready-on-another-job-*.svg` | **Composites:** face, Astrolabe frame for the state's tier, badge, then the over layer. They show the theme as designed. |
| `_row/<state>.svg` | Row composites with no badge. These are what the metrics measure. |
| `_row/badge-{open,closed,journal}.svg` | The badge glyphs alone, at text height (34-unit viewBox). |
| `_mix/orrery-in-brass/`, `_mix/medallion-in-astrolabe/` (each with `_row/`) | **The swap, both ways:** Orrery faces in the Brass kit (Medallion's bezel and badges), and Medallion's faces in the Astrolabe kit. |
| `_sheet.png`, `_mix.png`, `_kit.png`, `_48x6.png` | The composite sheet (`render_sheet.py`), the mix check, the kit sheet, and the six moon states at true 48 px enlarged 6×. |

## Review fixes (round 1)

These respond to `../supervisor-review.md` (S1, S2 and O1) and `../critic.md` (changes 12–14 and rules R1–R5). The sections from "Pitch" down are the pre-review design. Where they disagree with this section, this section wins.

### Required

| # | Fix | Done |
|---|---|---|
| S1 | Split faces from frames | The face builders return `(defs, under, over)`, and the under layer is clipped to r 52.4. The frame (`frame()`), badge ring, seats and glyphs are emitted as the kit. Every state SVG and `_sheet.png` are now composites: face, kit frame by tier, kit badge, then the over layer. `_mix.png` shows the swap both ways and needs no retouching in either direction. |
| S2 | Act now is gilt | Ready's frame tier is the shared medal gilt (lip `#E6CF98` / `#9A7E4A` / `#7C6236` / `#5C4724`, specular `#FFF4D6`) in the astrolabe shape: lip, groove, two-tone flat face, step and hero engraving. Ready's badge ring takes the same gilt. Resting uses the kit's old brass. Finished (Completed) is a duller brass, and Ghost (Not checked) a dark bronze with a weak specular. |
| O1 | Blocked: the disc | The earthshine disc is now `#22305A` → `#1A2448`, a step off the Prussian plate (`#15355A` → `#08162C`), so the new moon reads as dark. The engraved constellation is hero-only, with hairlines at 0.5. The thin limb (`#E3E8F3`) is the only bright thing. |
| O1 | Blocked: the cloud | The kumo is now **patinated brass**. The body runs `#7A6A45` → `#4E4330`, with a `#2A2418` shaded bevel. The `#B8A578` highlight is masked to the upper-left bevel of the scroll crests only and fades toward the lower right. The scroll spirals are engraved in the patina. Blocked's salience is **0.41× Ready** at the row tier and 0.44× as shipped (target ≤ 0.7). |

### Critic change 13: an identity of its own

The test is that a stranger can point to the Orrery row at 28 px without its label. The idea every state shares: **the moon as an astronomer reads it on an instrument.**

| Element | Where | Tier | Notes |
|---|---|---|---|
| **The engraved silver moon** | Every lit moon (Ready, RoJ, In journal, Done, Completed) | Hero | The maria are cut as fine parallel hatching (2-unit pitch, one direction for the whole set, at 0.38–0.42), faded in through the blurred mare shapes. This is the look of a 17th-century engraved selenography. The tone underneath is lightened, so the hatching carries the maria. Row faces keep plain smooth tone (rule R5). It is never craters, and it never touches the terminator. |
| **Star charts on the dark side** | RoJ, In journal and Done (masked to the unlit part of the disc); Blocked (the whole disc) | Hero | The chart is engraved (incised hairlines with brass-inlaid pits) and does not glow, because it is not light. |
| **Ready's own signature: the dawn sighting** | Ready | All tiers, and hero for the scale | The scene is the hour before sunrise (the Orrery's paired **Dawn** palette): a periwinkle zenith (`#4A5BA8`) falls to a rose-gold horizon (`#EDB79E`). The glow is brightest under the lit limb's direction, where the sun is, and is mirrored directly below on a violet sea (`#6E68A6` → `#2C3170`). The sea stays darker than the sky at the horizon. The road stays silver, the moon's colour. At hero, a **hairline limb scale** at 0.3 is engraved along the sky's edge only (198°–342°): divisions every 6°, longer every 30°. A single small brass index sits at the moon's azimuth: the instrument has the moon in its sight. The warm scene reads apart from Medallion's lapis Ready from 16 px. |
| **"Comes back" as a graduated scale arc** | Done | Row: the band; hero: the divisions | Plan §4.3 asks for a short scale arc. This one is a flat brass band of constant width with a square-cut tail, spiralling out (r 33.5 → 39.5, so never concentric with the rim) to a star-pointer head. At hero, divisions every 15° cross its inner half, with a full-width one every 45°. It reads as a piece of an astrolabe limb, not a refresh icon. **There is no bead on it**, which avoids the collision §4.3 warns of. |
| **The constellation "?"** | Not checked | Every tier | Unchanged: the critic's and the supervisor's best Not checked. It now sits in the ghost frame. |
| **Rose Dalamud** | Locked out | Every tier | Plan §4.3's rose (`#DE8C86` / `#C25C60` / `#8E2A2E`, lit edge `#F6C0B6`), the round-2 companion moon's colour. It parts from Medallion's crimson at a glance. |
| **The Astrolabe kit** | Every frame | Hero | A two-tone limb: a bright lip around a darker flat face. The face carries a double rule, the four cardinal notches (the quarters) and eight hairline marks between them (the twelve moons of the year), all inside the band. Row frames are clean, and nothing radiates outside the rim. |

### Critic changes 12 and 14, and the mixing rules

| Item | Result |
|---|---|
| 12 (Blocked's cloud off gilt) | Patinated brass, as in O1. |
| 14 (ribbon a step down) | The ember silk is now `#C98A52`, highlight `#E6B582`, deep `#86501F`. |
| R1 (one frame layer) | Faces carry no frame and no badge, and the under layer is clipped to r 52.4. |
| R2 (Ready and RoJ move together) | The two faces share the moon's geometry, tilt and place. Only Ready's sky is dawn. |
| R3 (gilt budget) | Gold appears only on Completed's check (the shared gilt) and Done's arc (resting brass, about 1/8 of the face). The kumo is patina and the ribbon is ember, not gold. |
| R4 (Blocked shows its obscurer) | The cloud covers **33%** of the well's lower half (measured by `coverage_lower_half()`; the base was widened to reach it). |
| R5 (row texture ceiling) | Row faces drop the hatching, the charts, the scale, the graduations and the plate seams. The busiest row face is Locked out, with 5 cracks. |

### Metrics after the fixes

```
composites (Astrolabe kit by tier, badges in)
orr 16px grey:      weakest RoJ-Jrn 14.9, Done-Lock 15.0, RoJ-Done 16.5 | salience Rdy=77 RoJ=40 Jrn=51 Blk=34 Done=39 Comp=57 Lock=42 NotC=30
orr 16px deut+grey: weakest RoJ-Jrn 15.0, Done-Lock 15.9, RoJ-Done 16.7
orr 20px grey:      weakest Done-Lock 25.9, RoJ-Jrn 26.4, Blk-Done 26.6 | salience Rdy=118 ... Comp=89
row tier (no badges)
row 16px grey:      weakest RoJ-Done 14.0, Done-Lock 15.0, Blk-Done 15.3 | salience Rdy=80 RoJ=36 Jrn=51 Blk=33 Done=40 Comp=58 Lock=43 NotC=30
row 16px deut+grey: weakest RoJ-Done 14.2, Blk-Done 15.3, RoJ-Blk 15.6
row 20px grey:      weakest Blk-Done 23.7, RoJ-Done 23.7, RoJ-Blk 25.5 | salience Rdy=123 ... Comp=89
mix: Orrery faces in the Brass kit (row)
16px grey:          weakest Lock-NotC 14.0, RoJ-Done 14.0, RoJ-NotC 14.4 | salience Rdy=81 ... Blk=35 Comp=62
mix: Medallion faces in the Astrolabe kit (row)
16px grey:          weakest Blk-Lock 12.0, RoJ-Blk 12.7, Blk-Done 13.2 | salience Rdy=84 ... Blk=37 Comp=57
```

| Target (16 px grey) | Composites | Row tier | Orrery in Brass | Medallion in Astrolabe |
|---|---|---|---|---|
| Weakest pair ≥ 12 | 14.9 | 14.0 | 14.0 | 12.0 (Medallion's own floor) |
| Ready ≥ 1.3× the next state | 77/57 = 1.35 | 80/58 = 1.38 | 81/62 = 1.31 | 84/57 = 1.47 |
| Completed ≤ 0.8× Ready | 0.74 | 0.73 | 0.77 | 0.68 |
| Blocked ≤ 0.7× Ready (O1) | 0.44 | 0.41 | 0.43 | – |

### Doubts after the fixes

- **The hatching** (resolved in round 2): it is now limited to 96 px and up, and 48 and 64 px use the smooth tone.
- **Dawn Ready lowers Ready's lead a little** (1.35 against 1.40 before), because a rose horizon is darker than a bright lapis sky in greyscale. It is still above the bar.
- **A mixed Orrery Ready keeps its dawn sky** beside night faces from other sets. That is intended: Ready is the one scene that changes.
- **Ghost and finished frames** are this kit's proposal. The plan names the tiers but not their metals.
- **The kit lives in this folder,** not in `kits/astrolabe/`.

## Review fixes (round 2)

These respond to the supervisor's second review: CHANGES REQUIRED, with one must-fix and one optional item. Both are done. This section supersedes round 1 where they differ.

| # | Fix | Done |
|---|---|---|
| Must | Hatching at 48 px became dark specks on lit crescents | The hatching now exists **only in the 96 and 128 atlas tiers and their 2×**: `hero/` faces, `HATCH = "on"`. A new **`mid/`** face set for 48 and 64 px (`HATCH = "off"`) draws every lit moon with the row tier's smooth tone at full strength. It keeps every other hero detail: the dark-side star charts, the dawn sky with its limb scale and index, Done's scale-arc graduations, the Blocked chart and the gap stars. Row faces are unchanged. |
| Must | The one-file composites | `ready.svg` and the other state composites carry both renderings. A media query inside the SVG (`@media (min-width:80px)`) shows the hatching only when the file is drawn at 80 px or more, and the smooth tone below that. A test SVG confirmed that Chrome switches correctly at 48 and 96 px. So `_sheet.png`, `_mix.png` and `_48x6.png` show each size as the atlas will. The atlas build should still use `hero/` and `mid/` explicitly, not rely on the media query. |
| Optional | Blocked's lit edge | k −0.62 (was −0.55), the same as Medallion's Blocked, so the two match in a mix. |
| Proof | `_48x6.png` | The six moon states (Ready, RoJ, In journal, Blocked, Done, Completed), as composites at true 48 px (device scale 1), enlarged 6× with nearest-neighbour. No hatching reaches them. What remains on the crescents is the blurred smooth maria tone, at the same values as Medallion's shipped crescents. |

Regenerated: every face (`hero/`, `mid/`, `row/`), the composites, `_row/`, `_mix/`, `_sheet.png`, `_mix.png`, `_kit.png` and `_48x6.png`.

**Metrics after round 2**

```
composites:  16px grey weakest RoJ-Jrn 14.9, Done-Lock 15.0, RoJ-Done 16.5 | Rdy=77 RoJ=40 Jrn=51 Blk=33 Done=39 Comp=57 Lock=42 NotC=30
             16px deut+grey weakest RoJ-Jrn 15.0 | 20px grey weakest Done-Lock 25.9
row tier:    16px grey weakest RoJ-Done 14.0, Blk-Done 14.9, Done-Lock 15.0 | Rdy=80 RoJ=36 Jrn=51 Blk=32 Done=40 Comp=58 Lock=43 NotC=30
             16px deut+grey weakest RoJ-Done 14.2 | 20px grey weakest Blk-Done 23.1
Orrery faces in Brass (row):       16px grey weakest Lock-NotC 14.0 | Rdy=81 Blk=34 Comp=62
Medallion faces in Astrolabe (row): 16px grey weakest Blk-Lock 12.0 | Rdy=84 Blk=37 Comp=57
```

| Target (16 px grey) | Composites | Row tier | Orrery in Brass | Medallion in Astrolabe |
|---|---|---|---|---|
| Weakest pair ≥ 12 | 14.9 | 14.0 | 14.0 | 12.0 (Medallion's own floor) |
| Ready ≥ 1.3× the next state | 1.35 | 1.38 | 1.31 | 1.47 |
| Completed ≤ 0.8× Ready | 0.74 | 0.73 | 0.77 | 0.68 |
| Blocked ≤ 0.7× Ready | 0.43 | 0.40 | 0.42 | – |

## Round 2's failures, and what fixes each

| Round 2 failure | Fix |
|---|---|
| 24 rim ticks read as sun rays (noise) | Nothing stands outside the rim. There are four cardinal notches, engraved *into* the brass face between two incised rules: small wedges pointing in, each with a lit wall. They show from 48 px as an instrument scale and are gone at row size. |
| Ready as a lightbulb or user icon (a gibbous on two pills) | Ready uses the family's moon road. A thick crescent sits high and left, and a horizon spans the whole plate. A broken road of thin glints (aspect 4.5 or more) runs under the lit centroid on a twilight sea. No head-and-shoulders shape is left. |
| Not checked as Saturn (mist bands past the disc) | A "?" drawn as an Astrologian constellation: seven stars joined by straight lines of light, with the dot as the brightest star. There are no bands and nothing crosses the rim. |
| Completed as a gold coin | The moon is silver, not gold: a full moon with soft maria, darkened a step (owner note 5), and a slight cool glow on the lacquer. The brass check is struck through the rim. Gold appears only on the check and the rim. |
| Locked out as a stray slash (a strip pasted on a half disc) | Dalamud broken apart. Five red plates split along cracks that each bend twice. Each plate is pushed out from an off-centre point of impact, and one falls away. The night plate and three stars show through the gaps: the moon is gone. No line dominates. |
| The road like bricks | The road keeps the Medallion's approved physics: needle streaks that foreshorten toward the horizon, a soft centred core and no capsule edge. |

**Kept from round 2, as the panel liked:**
- Blocked's engraved constellation;
- the round silhouette;
- the ember journal ribbon.

**Dropped:** Dalamud as a small rose companion moon. It belonged to the round-2 plugin icon, and themes don't change the icon. It would be the first thing to add if the Orrery ever gets one.

## States

| State | Build | Says |
|---|---|---|
| **Ready** | The plate at twilight: a teal-blue sky brightening to the horizon over a darker sea. A thick waxing crescent (r 29, lit limb 28° below level), its bloom, a fine moonlit horizon, and the broken road under the lit centroid (x 61.8). A faint engraved asterism sits in the sky (faint, because the sky is bright). Open brass padlock badge. | Go now. |
| **Ready on another job** | Ready's crescent at the same place and tilt, resting on the night plate with earthshine and an engraved asterism. No sea and no road. The game's job icon sits on a role-colour seat. | Ready, on that job. |
| **In journal** | Ready's scene at night, so the asterism is now clear. The ember-silk bookmark lies over the lip and face and drops down the step into the plate, with its top wrapping behind the instrument. Star-atlas badge. | You started this one. |
| **Blocked** | A new moon (an ashen, earthlit disc with a thin sunlit limb facing up and right) with a constellation **engraved** on its dark face: incised lines and brass-inlaid pits, no glow, because it is engraving and not light. A raised brass scroll-cloud (the Hingashi *kumo*, an instrument ornament) hides its lower half and casts a soft shadow onto it. Closed steel padlock badge. | It's charted, but you can't reach it yet. |
| **Done this cycle** | A waning half moon, lit on the left. Round it runs a brass repeat arrow that spirals out (r 33 → 39.5) from a fine tail to a head shaped as an astrolabe star pointer (a flame with concave flanks). An engraved centre line runs along it, with a pointer star set where the flame begins. An opening of about 88° at the bottom keeps it from becoming an orbit ring. | Done; it comes round again. |
| **Completed** | Menphina's full moon in silver, with eight maria (a step darker than the Medallion's), a soft key-lit limb and a slight cool moon glow. A brass check with an engraved rule down its long arm is struck out through the rim. | Done for good. |
| **Locked out** | Dalamud broken into five plates with lit and shaded fracture faces and faint plate seams. One falls away, turned 10°. The stars behind show through the gaps. | Gone, and not coming back. |
| **Not checked** | An uncharted asterism shaped like a "?", on open sky. Its lines and stars are light: a soft glow and **no drop shadow**, as the Astrologian's constellations are drawn in the game. No moon sits behind it, because stars can't shine through a moon. | Unknown yet. |

## Badges

The frame uses the shared slot geometry, unchanged:
- centre (95, 95);
- keyline r 24;
- ring r 19.9–23.1;
- seat r 19.9;
- drop shadow (1.4, 1.9);
- optical centring for job icons.

It is rendered as a small astrolabe: a brass lip (r 22.2–23.1), a flat face (20.6–22.0) and a step into a lacquer seat. Glyphs follow the job-glyph idiom: raised metal, no ink keyline, a soft under-glow, and a 60% envelope. Both locks share one body position, and the open shackle clears the body by 4.2 units.

| Badge | Seat | Glyph |
|---|---|---|
| Ready | Twilight plate `#4384C4` → `#163F72` | **Open padlock** in warm brass, with an engraved border on the body and a recessed keyhole with a lit lower-right wall. |
| Blocked | Night plate `#22415F` → `#0A1A30` | **Closed padlock** in cool blued steel, the same build. |
| In journal | Indigo plate `#2E4F7E` → `#0F2142` | **Star atlas**: an open brass book whose pages carry engraved asterisms instead of text lines, with an ember ribbon at the spine. |
| Ready on another job | Role colour (unchanged) | The game's job icon. |

Below 32 px the medal is drawn from `_row/` with no badge, and the glyph is drawn at text height beside it (`_row/badge-*.svg`, or the job icon), as in the Medallion.

## Light and material

**One key light from the upper left.**
- The brass lip is bright upper-left and dark lower-right.
- The step into the plate, the badge seat and every engraving are recesses: dark on the upper-left wall, lit on the lower-right.
- The kumo, the arrow, the check, the shards and the badge are raised, and cast a short shadow down and to the right.
- All blurs are sRGB.

**Inside the plate:**
- Phases are lit by their sun.
- In the scenes the moon is the light, and the road falls directly below it.

**Light versus engraving:**
- Glowing stars appear only where stars can be seen: open sky, the gaps in Dalamud, and Not checked.
- On the moon's face the chart is engraved and does not glow.

## Palette

| Group | Tokens |
|---|---|
| Keyline, shadow | `#070A15` |
| Brass (lip, badge ring, check, arrow, kumo) | `#FFF0C6` / `#EDCB82` / `#D6A957` / `#B5863A` / `#8F6526` / `#644515` / `#2C1C07`. Face: `#B98636` → `#5A3B12`. Engraving: `#3A2709`. |
| Plate (Prussian-blue night lacquer) | `#15355A` → `#08162C` |
| Twilight (Ready) | sky `#4282C2` → `#90C5E8`, sea `#4380BA` → `#1F4E84` |
| Night scene (In journal) | sky `#10294A` → `#2E5884`, sea `#244A72` → `#0C1F3C` |
| Moon (silver) | `#F5F6FA` / `#E3E8F3` / `#C4CCE0` / `#94A0C2` / `#5C6890` |
| Starlight | `#F1F4FF`, glow `#B9CAFF`, chart lines `#CDB57C` |
| Ember silk | `#F6CF98` / `#E3A060` / `#9E5C28` |
| Dalamud | `#E26C68` / `#C9505A` / `#7A283E`, lit edge `#F6B2AA` |
| Blued steel (closed lock) | `#C2CBE0` / `#8C99BA` / `#4E5A80` |

## Metrics (`moon-v6/round5/metrics.py`)

```
as shipped (badges in the SVG)
orr 16px grey:      weakest Lock-NotC 13.6, Done-Lock 15.5, RoJ-Done 16.3 | salience Rdy=81 RoJ=40 Jrn=53 Blk=45 Done=39 Comp=58 Lock=40 NotC=35
orr 16px deut+grey: weakest Lock-NotC 13.9, Done-Lock 16.4, RoJ-Done 16.5
orr 20px grey:      weakest Lock-NotC 23.7, Done-Lock 26.2, RoJ-Done 27.7 | salience Rdy=126 ... Comp=90
row tier (_row, no badges)
row 16px grey:      weakest Lock-NotC 13.6, RoJ-Done 13.8, RoJ-NotC 14.3 | salience Rdy=84 RoJ=35 Jrn=52 Blk=42 Done=39 Comp=58 Lock=40 NotC=35
row 16px deut+grey: weakest Lock-NotC 13.9, RoJ-Done 14.1, RoJ-NotC 14.3
row 20px grey:      weakest RoJ-Done 23.4, Lock-NotC 23.7, RoJ-NotC 25.2 | salience Rdy=130 ... Comp=90
```

| Target (16 px grey) | Shipped | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 13.6 | 13.6 |
| Ready ≥ 1.3× the next state | 81/58 = 1.40 | 84/58 = 1.45 |
| Completed ≤ 0.8× Ready | 0.72 | 0.69 |

**Mix-and-match check.** I swapped each Orrery state, one at a time, into the Medallion r5 row tier. The weakest pair never fell below the Medallion's own floor of 12.0, and Ready's lead held at 1.38× or more.

## Iterations

1. **First pass:** the Medallion's builders in a brass limb on night lacquer. Blocked was a bare new moon, and Not checked was a "?" constellation over a veiled moon. Lock-NotC was 10.6 and Blk-NotC 8.9 at row size. The stars of the "?" lay in front of a moon disc, and Blocked's stars glowed on the moon's face.
2. **Physics:** Not checked's moon disc was removed and the "?" made bolder. Blocked's chart became engraving with no glow. Blk-NotC rose to 10.7.
3. **Blocked's limb turned down** (a "boat" new moon). This was worse: RoJ-Blk 9.9. I reverted it.
4. **The brass kumo across the new moon.** This answers the brief's "clouds or a new moon" with both, and takes the owner's own round-4 cloud idea. Every pair reached 12.2 or more. Its curls were then redrawn as true scroll spirals with a trailing tail, because the first version read as a weather-app cloud. Stars were added in Dalamud's gaps.
5. **Identity:** at sheet scale the set still read as the Medallion recoloured. I moved to the Prussian-blue and brass palette and a two-tone limb. The plate's higher green luma cost Lock-NotC (10.8), so the plate was darkened, Dalamud lifted a step and the "?" made bolder: 12.4.
6. **Light logic:** the "?" lines and stars lost their drop shadows (light casts none) and gained a soft glow. Ready's twilight stars were dimmed. Weakest pair: 13.6.

## Doubts

- **Mixed sets mix rims.** Each state carries its theme's rim, so a mixed list shows champagne and brass side by side. The metrics hold, but the owner may prefer mixing to swap only the emblem and keep one rim per list. That is a runtime decision (rim and emblem as separate atlas layers).
- **The kumo is ornament, not weather.** It is raised brass relief, like the round-4 clouds' medal convention, but in metal. A supervisor may ask whether metal in front of the moon is plausible. My answer is that it is an appliqué on the instrument plate.
- **Not checked is the quietest state (35),** and at 16 px it is a small "?". The family rim is what keeps it from reading as a help button.
- **Ready's road and composition are the Medallion's.** That is on purpose (one Ready across themes), but Ready is the state where the two themes differ least: palette and rim only.
- **The cardinal notches** are visible from 48 px only. At 96 px the set could carry more of the instrument, but I held it to four, on the owner's "no noise glyphs".

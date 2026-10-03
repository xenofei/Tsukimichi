# Aether Crystal (v7 theme)

This revives round 2's concept A as a full theme: the moon is cut moonstone. It keeps medallion-r5's state meanings, file names and badge slot, so a user can take any state from either theme.

**Generator:** `_src/gen_ac.py` writes every SVG here (`python _src/gen_ac.py`). `_src/mix.py` renders `_mix.png`; it imports medallion-r5's `gen5.py` read-only and patches it in memory. The job icons are read from `docs/design/moon-v6/round5/medallion-r5/_src/jobs/` without changes. There is no plugin icon; the theme covers state glyphs only.

## Files

| File | What it is |
|---|---|
| `faces/<state>-under.svg`, `faces/<state>-over.svg` | **Unframed faces** (S1). `under` is the well content on the common circular well (centre (64, 64), r 52.4). `over` holds the overhangs drawn above the frame: In journal's ribbon and Completed's check, empty otherwise. |
| `faces/row/…` | The row-tier faces (under 32 px), in the same two layers |
| `kit/frame-<tier>-<full\|quiet>.svg` | **The Silver kit:** the 16-gon frame at four tiers (act-now, resting, finished, ghost), each at Full and Quiet. Each frame carries its own cast shadow on the well. |
| `kit/badge-seat-<open\|closed\|journal\|tank\|healer\|dps>.svg` | The badge keyline, the cut silver ring and each seat (the same slot as medallion-r5) |
| `kit/badge-open.svg`, `badge-closed.svg`, `badge-journal.svg` | The badge glyphs in the kit's metal (34-unit viewBox). The same files are copied to `_row/` for drawing at text height. |
| `ready.svg` … `not-checked.svg`, `ready-on-another-job[-job].svg` | **Composites:** face under, then the kit frame for the state's tier, then face over, then the badge |
| `_row/<state>.svg` | Row composites: the row face and the frame, with no badge |
| `_sheet.png` | The standard sheet of the composites |
| `_mix.png` | The mix proof: Aether faces in the Silver kit, Aether faces in the Brass kit, Medallion faces in the Silver kit, and the kit itself |

The critic suggests the kit live at `v7/themes/kits/silver/`. It is kept inside this folder for now, as the brief limited my edits to this folder; moving it is a copy.

## Review fixes (supervisor-review.md and critic.md)

| # | Fix | Done |
|---|---|---|
| S1 | Split faces from frames | Faces sit on one circular well, r 52.4, in `under` and `over` layers, with no frame and no badge.<br>The kit is its own set of files.<br>The 16-gon's inner apothem is **50.8**, so it covers the r 52.4 well by **1.6** at the flats. The vertices sit at r 51.8, still inside the well edge. The Quiet band's inner apothem is also 50.8, and its outer apothem is 53.6.<br>The frame's cast shadow onto the well moved from the face into the kit, because it is the frame's shadow.<br>Composites and the sheet are rebuilt from face plus kit, and `_mix.png` proves the swap both ways. |
| S2 | Act-now is the shared gilt | Ready's frame uses the medal gilt ramp `#E6CF98` / `#9A7E4A` / `#7C6236` / `#5C4724`, with the `#FFF4D6` specular, in the 16-gon crown-and-pavilion cut.<br>Resting is moonstone silver. Finished is that silver a step dimmer (value .08 + .8v). Ghost is the dimmest and flattest (.14 + .55v). |
| A1 | Blocked: frost bank and a thinner limb | A low-poly frost bank of **7 flat facets**: three billows of two facets each (a broad upper face toward the light and a lower-right face away from it), plus a dark underside with a scalloped lower edge.<br>Its top edge is three chamfered domes. Facets run from `#B8C4DE` (facing the light) to `#4E5C82` (facing away), and the bank casts the 1.1/1.6 shadow.<br>It covers **37% of the disc** and 39% of the well's lower half (R4 asks at least 30%). It hides the lower limb.<br>The disc moved to (62, 49), r 30. The limb is **k −0.82** (about 9% lit), on the right and a little up, so it is no longer a mirror of Ready on another job. The transmitted rim is kept. |
| Critic 7 | Frost bank in the set's material, between well and limb in value, wider than the disc | As A1. The bank spans x 16–97 against the disc's 32–92. |
| Critic 8 | Done's arrow reads as a refresh button | The outer slope is now moonstone (`#56618A` → `#CED6E8`), and only the inner slope keeps aether. Its value is about 15% lower (ambient .36 → .27, diffuse .58 → .56).<br>The arrowhead spans **13** (was 18), and the tail tapers to a fine point (4% width, as Medallion's does). |
| Critic 9 | The star motif on the dark side | Earthshine facet contrast is halved (value range .48–.78, was .25–1.0), only girdle facets take the key-light gloss, and dark-side edge hairlines drop from .10 to .06. The 8-point table pattern no longer shows below 64 px. |
| Critic 10 and R5 | Completed speckles at row size | Every row-tier moon, including Dalamud, uses a 7-facet cut: a hexagonal table and 6 sectors, so **12 internal edges**. Row faces have no facet hairlines and no glint. The ridges are coarser at row size: the "?" has 15 points, the arrow 13. |
| Critic 11 | The bezel looked periwinkle and plastic | The Silver kit ramp is now low-chroma moonstone silver: `#12172A` / `#353D56` / `#646F90` / `#AEB7CC` / `#E2E8F4` / `#F8FAFD`. |
| Optional 1 | The check as a two-facet ridge | Each arm is two flat faces split on its centreline: the face toward the light `#F0DDA8`, the other `#7C6236`. The caps are faceted, and a crisp `#FFF6DC` crest line runs along it. The check is in the `over` layer. |
| Kit metals | Badge contents in the kit's metal | The open lock stays gilt (the act-now signal), the closed lock is silver, and the book is now silver with the Tide ribbon (it was gold), which also meets R3's gilt budget. |

**Mixing rules R1–R5, as they apply here:**
- **R1:** the faces carry no frame and no badge ring. Only the `over` layer reaches outside r 52.4.
- **R2:** Ready and Ready on another job share one moon (same geometry, same cut).
- **R3:** on resting faces, gold appears only as Completed's check.
- **R4:** Blocked's bank covers 39% of the well's lower half.
- **R5:** row moons have 12 internal edges.

**Metrics after the fixes** (`round5/metrics.py`, composites, so the frames take part):

```
as shipped (face + Silver kit + badge)
ac 16px grey:      weakest RoJ-Done 14.4, Jrn-Lock 15.3, Blk-Lock 15.8 | salience Rdy=74 RoJ=34 Jrn=47 Blk=36 Done=34 Comp=55 Lock=41 NotC=26
ac 16px deut+grey: weakest RoJ-Done 14.4, Jrn-Lock 15.5, RoJ-Jrn 15.5
ac 20px grey:      weakest RoJ-Done 25.6, RoJ-Jrn 26.1, Done-Lock 27.1 | salience Rdy=116 ... Comp=88
row tier (row face + frame, no badge)
row 16px grey:      weakest RoJ-Done 12.8, Blk-Done 13.9, Jrn-Lock 15.0 | salience Rdy=79 RoJ=33 Jrn=46 Blk=37 Done=36 Comp=57 Lock=44 NotC=27
row 16px deut+grey: weakest RoJ-Done 12.7, Blk-Done 13.9, Jrn-Lock 15.5
row 20px grey:      weakest RoJ-Done 21.9, Blk-Done 24.3, Blk-Lock 27.2
```

| Target (16 px grey) | Shipped | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 14.4 | 12.8 |
| Ready ≥ 1.3× the next state (Completed) | 74/55 = 1.35 | 79/57 = 1.39 |
| Completed ≤ 0.8× Ready | 0.74 | 0.72 |
| RoJ-Blk ≥ 12 at the row tier | 17.5 (was 12.1) | |
| Blocked ≤ 0.8× Ready | 0.49 | 0.47 |

**Doubts after the fixes:**
- **Ready's lead fell** from 1.42 to 1.35. The gilt frame adds to Ready, but the finished frame and the brighter Completed moon came with it. The lead is still above 1.3.
- **The weakest pair is now RoJ-Done** (12.8 on the row tier), because the quieter arrow cost Done some contrast. Done's moon grew from r 24 to 25 to hold it.
- **The frost bank's billows touch the badge's keyline** at the lower right. The state-carrying mass is the left two-thirds of the bank, so it stays readable with the badge on.
- **The Brass kit in `_mix.png`** is Medallion's shipped bezel with its well shadow and badges. Aether's cool faces in it read as "one medal with a different enamel", as the supervisor predicted.
- **Medallion's own faces must also drop their baked well shadow** for S1. `mix.py` does this in memory only, and does not change `gen5.py`.

## Review fixes (round 2: supervisor spot check)

**The fix:** Blocked's frost-bank tops outshone the moon. The bank's ramp is now capped at `#9AA6C8`, about 80% of the limb's lightness: `FROST` = `#2A3352` / `#4E5C82` / `#7C89B0` / `#9AA6C8`.
- The per-facet key lighting is unchanged. The upper-left faces land at the cap, and the lower-right faces and the underside step down toward `#4E5C82` and below.
- The crest seams are now `#9AA6C8` at .25 (they were `#E2E8F4` at .18).
- The thin limb (k −0.82, up to `#F7F8FB`) is now the brightest thing on the face, so Blocked reads as a dark new moon behind cloud.

Files regenerated: `faces/blocked-under.svg`, `faces/row/blocked-under.svg`, `blocked.svg`, `_row/blocked.svg`, `_sheet.png` and `_mix.png`. Nothing else changed.

**Metrics** (16 px grey):

| | Weakest pair | Blk-Done | Blk-Lock | RoJ-Blk | Salience |
|---|---|---|---|---|---|
| Shipped | RoJ-Done 14.4 | 14.9 | 14.8 | 16.2 | Rdy 74, Blk 32, Comp 55 |
| Row tier | RoJ-Done 12.8 | 13.0 | 15.1 | 15.2 | Rdy 79, Blk 33, Comp 57 |

- Ready leads by 1.35× (shipped) and 1.39× (row). Completed is 0.74 and 0.72 of Ready. Blocked is 0.43 and 0.42 of Ready.
- In the deuteranopia simulation, the weakest pair is 12.7 at the row tier.

The sections below describe the design before the review. Where they disagree with this section, this section wins.

## The idea

Every cut surface is drawn as flat facets, and each facet takes **one flat value from its own normal**. That one rule makes the whole set read as crystal, and it keeps the lighting honest. This covers the moons, the bezel, the badge ring, the check, the arrow, the question mark and the locks.

- **Moons** are a brilliant cut seen from above: a flat octagonal table, 8 star facets, 8 kites and 16 girdle facets (33 in all). There are three clear zones, so a moon reads as a gem, not a geodesic ball.
  - **Lit side.** The phase's own sun lights it, Lambert per facet. The sun direction comes from the phase: the terminator's k is the sun's z, so a crescent is lit from behind. The facets brighten toward the lit limb, so the cut itself carries the shading. There are no bands and no second terminator.
  - **Dark side.** Earthshine, brightest on facets that face the viewer. A faint key-light gloss on the facets nearest the half-vector shows that the dark side is polished stone, not a hole.
- **The frame** is a 16-sided cut crystal bezel in smoky blue moon-quartz. It has one crown facet sloping outward and one pavilion facet sloping inward per side, each lit by the key light:
  - the crown is bright at the upper left and the inner slope bright at the lower right, as a convex cut rim should be;
  - a white specular flash sits on the upper-left crown edge.
  - Its silhouette is a 16-gon, not a circle, so the set doesn't read as coins or radio buttons. From 48 px it reads as a cut gem setting.
- **Crystal shards:**
  - Locked out's Dalamud splits along straight cleavage planes, because a crystal cleaves flat and doesn't craze.
  - Ready's road is slivers of light: straight-edged, with long fine tips, in one flat tone.

## Light

- **One UI key light:** from the upper left (azimuth 135°, elevation 45°), vector (−0.42, −0.57, 0.71). It lights:
  - the bezel and the badge ring;
  - the locks and the book;
  - the check, the arrow and the question mark;
  - Locked out's red crystal.

  It uses ambient, Lambert and a Blinn specular. Raised things cast a soft shadow down and to the right (1.1, 1.6). The well and the badge seat are recesses with an inner shadow on their upper-left edge.
- **Each moon's phase lighting is its sun,** per facet, as in medallion-r5.
- **Inside Ready and In journal:**
  - the sky brightens toward the horizon;
  - the sea is darker than the sky at the horizon (a Fresnel band of sky colour under it);
  - the road hangs directly under the lit centroid (x 61.8), in the moon's colour, below the horizon only;
  - the rows foreshorten toward the horizon and brighten toward the mirror point at the bottom of the well.
- **Aether blue** is used in three places only, each with a physical cause. It is never a halo.
  1. **Adularescence:** moonstone's soft blue sheen under the lit surface, a blurred `#C2DAFB` billow at .26–.45.
  2. **Transmitted rim:** a crescent is lit from behind, so sunlight scatters through the crystal's thin edge. The dark limb carries a 1.5-unit `#7FAEEA` rim, strongest beside the horns and fading opposite the sun.
  3. **Done's arrow:** a band of cut aether crystal, read as an aether current that brings the moon round again.

## Palette

| Group | Tokens |
|---|---|
| Keyline and shadow | `#070A15` |
| Well (night crystal) | `#1C2856` → `#0E1533` |
| Moon-quartz bezel (value ramp) | `#10162F` / `#2A3670` / `#5868A8` / `#A2B2DC` / `#D5DEF4` / `#F6F8FE` |
| Moonstone (lit moon) | `#4A587F` / `#7E8DB3` / `#BAC6E0` / `#E2E8F4` / `#F7F8FB` |
| Earthshine crystal | `#18214A` / `#253264` / `#34437A` (set per state) |
| Aether (the one new hue, OKLCH C ≈ .10) | `#7FAEEA`, highlight `#C2DAFB`, deep `#3D64A8` |
| Gold (check, open lock, book) | `#4A3818` / `#7C6236` / `#B0904F` / `#D9BE82` / `#F0DDA8` / `#FFF6DC` |
| Silver (closed lock) | `#232B48` / `#4E5C82` / `#8292B8` / `#B8C4DE` / `#E6ECF8` |
| Dalamud red crystal | `#2A0C14` / `#6E2232` / `#AE3A48` / `#D65A62` / `#EC8C88` / `#F8CCC6`, socket `#0B0408` |
| Ready sky and sea | sky `#5A86D8` → `#A2C4F6`, sea `#6E98DC` → `#2C4C92` |
| In journal night | sky `#1A2654` → `#3A508A`, sea `#2A3F7E` → `#141F48` |
| Ribbon (Moon Road Tide) | `#6F8FD0`, `#A9BEEA`, `#3F5A98` |
| Badge seats and role colours | Unchanged from medallion-r5 |

Gold appears only on the check, the open lock and the book. The palette is otherwise cool, so the open lock is the warm, inviting mark the brief asks for.

## Badge system (same slot as medallion-r5)

- **Geometry:** centre (95, 95); keyline r 24; ring r 19.9–23.1 with the crest at 21.5; seat r 19.9; icon slot 35.5; drop shadow (1.4, 1.9).
- **What differs here:** the keyline, ring and seat are 12-sided and cut in moon-quartz rather than gilt.
- **Seat colours and runtime rules:** the seats, the job-icon optical centring (`job_optical_offset`, the same method as medallion-r5) and the under-32 px row fallback are unchanged.
- **Glyphs:** cut metal in the job-glyph idiom: a soft under-glow and no ink keyline.
  - **Locks:** a slab body with four flat bevels and a recessed keyhole with a lit lower-right wall. The shackle is a ridge, two flat slopes per segment, so its bend reads as cut facets.
    - Open: gold, with the shackle lifted 5. Its free leg clears the body by 4.2.
    - Closed: silver.
    - Both share one body position.
  - **Book:** each page is four strips that turn from the light toward the spine, with incised text lines and the Tide silk ribbon.

## States

| State | Build |
|---|---|
| Ready | The scene: a faceted moonstone crescent (r 29, k −0.18, limb 28° below level) with adularescence and an aether rim, over a bright twilight sea with its sliver road. Open-lock badge. |
| Ready on another job | The same crescent at full brightness on night crystal, with no sea. Job badge. |
| In journal | Ready's scene at night, with the moon and road dimmed, the Tide ribbon wrapped over the bezel and covering the lower horn, and the book badge. |
| Blocked | A new moon: a dark crystal disc (r 31) turned from its sun. Its facets show only earthshine and gloss, with one thin sunlit limb low on the left (k −0.66) and a faint transmitted rim. Closed-lock badge. |
| Done this cycle | A waning half moon, lit on the left (r 24), with soft moonstone maria, inside a cut aether-crystal repeat arrow. The arrow spirals outward from r 33 to 39, and its head is a two-facet crystal point. It is open at the bottom. |
| Completed | A full moonstone (r 35), lit from the front and upper left, with its cloudy inclusions in the maria's real layout. The gold check is cut as a ridge, struck across the lower right and out past the bezel. |
| Locked out | Dalamud as a red crystal sphere. It is split along seven straight cleavage lines that each kink once, from an off-centre impact. Each shard is pushed out and turned, and one falls away past the silhouette. Lit cleavage faces get a pale edge and shaded faces a dark one. The socket is a black recess with a lit lower-right lip. |
| Not checked | A "?" whose bowl is a veiled moon's lit limb, cut as a moonstone ridge, with a tiny cut full moon for its dot. The earthshine moon behind is blurred and faint. |

## What changed from round 2, failure by failure

| Round-2 failure | Fix |
|---|---|
| Locked out read as a no-entry sign | The ofuda band is gone. The red moon is shattered along kinked cleavage lines, with no diagonal bar, and one shard breaks the silhouette. |
| Completed read as a coin | It has a brilliant cut with visible facets, moonstone maria and a gold check that crosses the rim. The frame is a 16-gon, not a ring. |
| Done read as a check-circle | There is no check now. The waning half moon sits in a repeat arrow, and the arrow spirals instead of closing a ring. |
| Not checked read as a hamburger | The mist bars are gone. It is a single "?" built from a moon's limb. |
| Ready read as a lollipop | The road is inside the frame, on a sea. Nothing hangs below the disc. |
| The teal glow looked like a mobile game | There is no glow anywhere. Aether is a blue (not teal) used three ways, each physically motivated and inside the stone. |
| The road looked like stepping stones | Slivers in one flat tone, with long tips, aspect 4.5 or more and heights cut 22%. Glints on water carry no facets of their own. |
| The aetheryte crystal (owner veto) | None. Crystal is only the material of the moon, the bezel and the shards. |
| Mixed rims (gilt and pewter) | One moon-quartz bezel on all eight, as the owner asked in round 4. Ready leads through its scene. |
| The facet mesh read as a geodesic ball (first draft this round) | Replaced the 48-triangle rose cut with a 33-facet brilliant. |

## Metrics (`round5/metrics.py`)

```
as shipped (badges in the SVG)
ac 16px grey:      weakest Lock-NotC 13.2, Blk-Done 14.4, RoJ-Blk 14.5 | salience Rdy=74 RoJ=32 Jrn=45 Blk=22 Done=33 Comp=52 Lock=38 NotC=28
ac 16px deut+grey: weakest Lock-NotC 13.9, Blk-Done 14.2, RoJ-Blk 14.4
ac 20px grey:      weakest Lock-NotC 23.1, Blk-Done 23.7, RoJ-Blk 24.1 | salience Rdy=120 ... Comp=84
row tier (_row, no badges)
row 16px grey:      weakest RoJ-Blk 12.1, RoJ-NotC 12.4, Blk-Done 12.9 | salience Rdy=79 RoJ=28 Jrn=43 Blk=21 Done=33 Comp=52 Lock=38 NotC=28
row 16px deut+grey: weakest RoJ-Blk 12.1, RoJ-NotC 12.4, Blk-Done 12.7
row 20px grey:      weakest RoJ-Blk 20.1, Blk-Done 20.9, Blk-NotC 21.6 | salience Rdy=128 ... Comp=84
```

| Target (16 px grey) | Shipped | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 13.2 | 12.1 |
| Ready ≥ 1.3× the next state (Completed) | 74/52 = 1.42 | 79/52 = 1.52 |
| Completed ≤ 0.8× Ready | 0.70 | 0.66 |

## Iterations

1. **First render.**
   - The bezel keyline was a filled disc, which blacked out every interior.
   - Once fixed, the moons read as golf balls (48-triangle mesh), Ready's dark disc was lighter than the sky, and the ribbon gradient's id clashed with a filter's.
   - The weakest pair was 7.6, and 5.8 on the row tier.
2. **Brilliant-cut mesh, gloss on the dark side, kinked cleavage lines, a rebuilt arrowhead, a brighter "?".** The weakest pair was 8.2.
3. **Maria for the full moons; Blocked's limb moved to the lower left,** away from the "?" bowl and the crescent. Blocked then collided with Done (9.4).
4. **A full pairwise matrix guided the rest.**
   - Blocked became the dark state: a near-well-value disc with a thin limb.
   - The lit floor rose to .42, so the crescents read bright and the facets only modulate them.
   - Done, Not checked and Locked out were brightened.
   - Result: 12.1 shipped and 12.0 row tier.
5. **The 520 px look.**
   - Hexagonal road "plates" became tapered slivers.
   - Blocked's limb was thinned toward a true new moon.
   - Dalamud was pulled back from pink.
   - A split-facet crown was tried and reverted, because at 96 px it beaded like a rope (noise). The calm 16-sided cut stays.

## Doubts

- **Blocked's limb is k −0.66** (about 17% lit). That is a young crescent with earthshine, not a strict new moon. Thinner drops the row tier's RoJ-Blk under 12. At row size the meaning rests on it being the one dark glyph; the closed lock carries it from 32 px.
- **Not checked at 16–28 px** is still "a ? in a frame", the same doubt as medallion-r5. The moon reading shows from about 48 px.
- **Done's arrow** is aether blue, the most saturated non-alarm mark. At 16 px it can still read as a refresh icon.
- **Mixing light conventions.** Locked out's red crystal is lit by the key light, as relief, while every other moon is lit by its phase. That is deliberate: a shattered moon has no phase. A supervisor may want it stated.
- **The bezel is cool and mid-value.** The set is calmer than the gilt medallion, and Ready's lead comes entirely from its bright sky. In daylight, Ready is the lowest-contrast state, as in medallion-r5.
- **Mixed with medallion states,** the 16-gon frame and the round gilt frame will sit side by side in one list. Whether that is acceptable is the owner's call.

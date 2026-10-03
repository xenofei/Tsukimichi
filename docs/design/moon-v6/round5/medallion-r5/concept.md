# Menphina's Medallion, round 5

Round 5 is a polish pass on round 4, which the owner said "looks great". It follows `../brief.md`. Everything not listed here is unchanged from round 4, which is kept below as history.

**Generator:** `_src/gen5.py` writes every SVG in this folder, the badge-less row tier in `_row/`, and the badge glyphs. `_src/variants_sheet.py` renders `_variants.png`. `gen4.py` and `gen3.py` are kept for reference; don't run them.

**Files**

| File | What it is |
|---|---|
| `ready.svg`, `in-journal.svg`, `blocked.svg`, `done-this-cycle.svg`, `completed.svg`, `locked-out.svg`, `not-checked.svg` | The standard states, now with badges on Ready, In journal and Blocked |
| `ready-on-another-job.svg` (Paladin), `-paladin.svg`, `-bard.svg`, `-white-mage.svg` | The job badges, now optically centred |
| `blocked.svg` | Blocked: variant b, the new moon behind cloud, as chosen by the supervisor and the critic. a and c are retired. |
| `plugin-icon.svg` | The recomposed icon |
| `_row/*.svg` | The row tier (under 32 px): the same medals with no badge, plus `badge-open.svg`, `badge-closed.svg` and `badge-journal.svg`, the badge glyphs alone for text height |
| `_sheet.png`, `_variants.png` | Renders. `_variants.png` shows every badge medal, all job variants and Blocked candidates at 16–128 px, the row mock and the icon at 32, 64, 128 and 256 px. |

`completed-green.svg` is gone (owner note 7: gold only).

## Supervisor round 1 fixes (round 5)

These respond to `../supervisor/art-review-1.md` (CHANGES REQUIRED, eight items) and `../supervisor/critic.md`.

**Blocked decision:** b ships as `blocked.svg`. `blocked-a.svg`, `blocked-b.svg` and `blocked-c.svg` are removed, and `blocked()` now builds b only. The numbers in "Round 5: what changed" below are from before these fixes; this section supersedes them.

| # | Fix | Done |
|---|---|---|
| 1 | The lining lit a cloud over the unlit disc | Both lining clips are now the sunlit limb itself (the `phase()` crescent, k −0.62, `rotate(-12 72 44)`), so a lining appears only where a cloud edge crosses the lit sliver. `cloud()` takes the clip as markup. |
| 2 | Cloud tops too bright for a new moon | The billow highlights and lit rims are scaled to 75% (`CLOUD_LIT`). The cloud tops' 99th-percentile L drops from .766 to .721, about 6%. The asked 12% (lit 0.5, L .671) dropped the row-tier Blk-Lock to 11.8. Following the guard, I backed off to the strongest dimming that holds 12.0. |
| 3 | One thin wisp across the limb | A tapered translucent stratus wisp, 2.4 thick, runs from the upper bank across the limb about a third of the way down (y 33–38, ending at x 98.5 on the limb's outer edge). It is `#46507A` at .55, with no key-lit top and no shadow. A `#DCE2EE` .25 lining sits on its upper edge, clipped to the lit limb. |
| 4 | Badge glyphs as metal, in the job-glyph idiom | There is no ink keyline. Every glyph sits on a soft under-glow (`#080B16` .5, offset .6/.9, blur .8, sRGB).<br>**Locks:** the shackle is shaded as a cylinder (mid stroke 2.6, a light .8 stripe offset to the upper left, a dark 1.1 band to the lower right, all masked to the stroke, which follows the bend), and its round cap gives the free leg a lit end. The body is a slab: face gradient upper-left → lower-right, a 1-unit lit bevel on the upper and left edges, a shaded bevel on the lower and right. The keyhole is a recess: dark fill with a 0.4 lit hairline on its lower-right inner wall.<br>**Book:** the board is darker metal, not an outline. The pages carry lit and shaded bevels. The text lines and spine are incised (a 0.5 dark groove with a 0.3 light hairline on its lower-right side), and the slot lenses are gone.<br>**Size:** the closed lock is 22.9 tall (−11.9 to 11) and the book 24, about 60% of the seat. The `_row/badge-*.svg` copies are regenerated from the same builders, now with a 34-unit viewBox. |
| 5 | One body position; a visible open gap | Both locks put the body at the same place (y 0–11, offset by one shared `LOCK_OY`). The open shackle is lifted 5. Its free leg ends at −5.5 with a 1.3 cap, so it clears the body by **4.2 units** (1.05 px at the 32 px medal). |
| 6 | Pale seam under the far shore | Both. The shore is built on a waterline at H + 1.5, its dark mirror is reflected about that waterline, and every lantern-line reflection is measured from it. The horizon hairline `r5i-hz` is clipped to the open gap (x 206–306). At 3× there is no light line under either shore. |
| 7 | Moon size; horn tangent; road at 32 px | `ICON_H` is 245. The moon is **r 63, k −0.22**, with the limb facing 30° below level (as in round 4), and the lit centroid is at (256.0, 182.0). The lower horn is at y 220, **25 above the horizon**; the earthshine disc's bottom is 16 above. The mirror row (2H − 182) is 308, and the measured brightest row is **y 303**, above the Installed corner (312). The first rows now start at .60 opacity (was .42) and reach .82 at the mirror point, so the top rows reach 0.95× the peak and the road reads as a bright line at 32 px. The shore, lights and reflections move with the horizon. |
| 8 | Symmetric glitter path | Chips sit on both sides (alternating, both sides every third row), and their offsets are staggered ±8 row to row, so no column forms. The main streaks' extents are symmetric. The whole road is then re-centred so its light-weighted centre is on x 256. The measured axis is **x 254.6** (target ±1.5). The Installed corner is kept quiet by the vertical fade only: 0.92× the peak from y 330, 0.88× from 360, 0.82× from 400, 0.72× from 440. |
| 9 | The wisp read as a slash | It is blurred 0.5 (sRGB) and drawn under the small bank, so it emerges from the bank's lower right. It fades to 0 within 3 units outside the lit limb, using a mask from the limb's outer edge at the wisp's height to 3 units beyond it. Thickness, position and lining are unchanged. Blk-Lock holds at 13.0 shipped and 12.0 at row size. |
| Opt. | Chōchin lanterns | Four of the 13 waterfront lights are Kugane red-orange `#E27A4C`, and each reflection directly below it takes the same hue at the usual .5 / .42 / .30 steps. |

Not adopted:
- the critic's optional neutral seat for state badges (Ready's lead would have dropped);
- the closed-tome variant;
- the warm cast on the icon's lower limb (the moon is now 25 clear of the horizon).

**Icon after the fixes** (OKLab L at 512):
- moon max .963;
- road peak .836 = 0.87× the moon, at row y 303;
- axis 254.6;
- horizon sky .422 against sea .402, measured off the road at x 208–222 and 292–304;
- file size 86 KB.

**Metrics after the fixes**

```
as shipped (badges in the SVG)
r5 16px grey:      weakest Lock-NotC 12.3, Blk-Lock 13.0, RoJ-Jrn 14.3 | salience Rdy=82 RoJ=42 Jrn=55 Blk=40 Done=43 Comp=61 Lock=40 NotC=40
r5 16px deut+grey: weakest Lock-NotC 12.4, Blk-Lock 13.7, RoJ-Jrn 14.1
r5 20px grey:      weakest Blk-Lock 22.2, Lock-NotC 22.4, RoJ-Jrn 24.3 | salience Rdy=128 RoJ=63 Jrn=84 Blk=63 Done=68 Comp=96 Lock=63 NotC=62
row tier (_row, no badges)
row 16px grey:      weakest Blk-Lock 12.0, Lock-NotC 12.3, RoJ-Blk 12.8 | salience Rdy=85 RoJ=38 Jrn=52 Blk=39 Done=43 Comp=61 Lock=40 NotC=40
row 16px deut+grey: weakest Lock-NotC 12.4, Blk-Lock 12.8, RoJ-Blk 12.8
row 20px grey:      weakest Blk-Lock 19.7, Blk-Done 22.1, RoJ-Blk 22.2 | salience Rdy=135 RoJ=58 Jrn=82 Blk=62 Done=68 Comp=96 Lock=63 NotC=62
```

| Target (16 px grey) | Shipped | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 12.3 | 12.0 (Blk-Lock, at the guard) |
| Ready ≥ 1.3× the next state | 82/61 = 1.34 | 85/61 = 1.39 |
| Completed ≤ 0.8× Ready | 0.74 | 0.72 |

**Open doubts:**
- **The cloud dimming stops at 6%, not 12%,** because the row tier's Blk-Lock sits right at 12.0.
- **The road crosses the Installed corner at 0.97× its peak where it enters (y 312).** The fade only reaches 0.72× by y 440.

## Round 5: what changed

### 1. Job icons centred optically

The badge now measures each icon from its visible pixels, not its canvas.
- Pixels with alpha above 0.5 count; this drops the soft glow.
- The optical centre is the mean of the alpha bounding-box centre and the alpha-weighted centroid.
- The image is then shifted so that point lands on the seat's centre.

The game's glyphs all sit high in their canvases:

| Job | Shift in the 128 viewBox (x, y) |
|---|---|
| Paladin | (0, +2.8) |
| Bard | (−0.7, +2.6) |
| White Mage | (−0.4, +3.7) |

Round 4 used a flat +1.25 for every job. The runtime does the same measurement once per job icon (`job_optical_offset()` in `gen5.py` is the reference).

### 2. The plugin icon: centred, with Kugane

- **The moon and road are on the centre axis.**
  - The crescent's lit centroid is at x 256.0. The moon is r 56, k −0.26, and its lit limb faces 18° below level.
  - The road is centred under it. The measured axis is x 250.8: the extra chips sit left of the axis to keep the Installed corner quieter.
- **The horizon moved to y 255 (the middle),** and the moon sits low over it, 8 units clear. That is what puts the mirror point, and so the road's brightest row, at **y 305–310, above Dalamud's Installed check (y 312 and below)**.
  - Inside the corner the road fades: its brightest pixel is 0.94× the peak from y 330, 0.90× from y 360, 0.82× from y 400 and 0.74× from y 440.
  - A road centred on x 256 always crosses into the bottom-right quadrant's left edge. What we kept out of the corner is its peak.
- **FFXIV detail: Kugane's far shore,** in silhouette after the official Stormblood art of Kugane and Kugane Castle (references below).
  - **Left:** machiya rooftops along the quay and a five-storey pagoda with a spire.
  - **Right:** Kugane Castle (a battered stone base, three curved roofs, a crowning gable with shachihoko finials) and rooftops.
  - **Behind:** paler far hills, for atmospheric perspective.
  - **The gap:** a gap in the middle leaves open horizon where the road meets it.
  - **The lantern line:** 13 warm points on the waterfront and one castle window, each with a short broken warm column **directly below it**, its mirror image as far below the horizon as the light is above it.
  - **Moonlit edges:** the shore's moon-facing edges get a faint moonlit edge. The left shore is lit on its right edges and the right shore on its left, because the moon is between them.
  - **Reflection:** the whole shore has a dark mirror at .30, broken by the ripple texture.
  - **Not added:** no aetheryte, no airship (a 5 px smudge at 64), and no torii (it would have sat in the Installed corner).
- **Unchanged:** the stone tōrō, its warm column and pool, the frame, the filigree, the Fresnel band, the bloom and its mirror, the soft crest cores and the wind-ripple texture.
- **The road** is generated: 20 rows and 45 streaks, foreshortened toward the horizon, with every streak at aspect 4.6–21.9. About 40% of the rows break into two streaks, and the chips are thin dashes.
- **The file** is 83 KB. The shore is defined once and reused, and the streaks use 20 samples.

| Measure (OKLab L, 512 render) | Round 4 | Round 5 |
|---|---|---|
| Moon max | .961 | .963 |
| Road peak | .853 = 0.89× | .826 = 0.86× |
| Road axis vs lit centroid | 171.1 vs 172.0 | 250.8 vs 256.0 (chips biased left, on purpose) |
| Brightest row vs rule (2H − lit y) | 436 vs 431 | 305 vs 310 |
| Horizon sky / sea at the road | .385 / .364 | .488 / .483 (the sea is still darker) |
| Installed corner L std | .062 | .123 (the road's right half now passes through it, fading) |

At 64 px the moon, the road, the lit lantern window and the two skyline bumps (pagoda spike and castle) all read. At 32 px the moon and road read, and the shore is a dark band.

**References** (studied for silhouette and proportion; nothing traced):
- Kugane art, Final Fantasy XIV: Stormblood art gallery: https://www.creativeuncut.com/gallery-31/ff14sb-kugane.html
- Kugane Castle: https://ffxiv.consolegameswiki.com/wiki/Kugane_Castle and https://ffxiv.gamerescape.com/wiki/Kugane_Castle
- Kugane: https://finalfantasy.fandom.com/wiki/Kugane

### 3. Blocked: more obscurity, a darker moon, three candidates

All three move the moon up and right, to (72, 44) r 25, out of the badge's way. Each has more of the moon hidden than round 4, and each carries the closed-lock badge.

| Variant | Build | 16 px weakest pair (shipped / row tier) |
|---|---|---|
| **a**, heavy bank | A full moon a step darker (`#BAC4DA` → `#64729A`), about two-thirds hidden by a back bank across its top and a front bank below | 12.3 / **11.8** (RoJ-Blk) |
| **b**, new moon behind cloud (**pick**) | An ashen, earthlit disc (`#4A5682` → `#252E52`) with only a thin sunlit limb (k −0.62), behind the same two banks. This combines the owner's clouds with the tester's new moon. | 12.3 / 12.3 |
| **c**, veiled | The darker full moon seen through a translucent stratus sheet. The sheet is three soft lobes, never a band, so no Saturn ring can read. Its aureole lights the veil around the moon, with a cumulus in front below. | 12.3 / **11.2** (RoJ-Blk) |

- **Why b.** It is the most obscure: almost no lit moon, only a sliver. It is also the only one that passes the row tier.
- **a or c.** Either would need its row tier retuned if the supervisor or the critic prefers it.
- **Shared with round 4:** the silver linings, the key-lit relief clouds and the cast shadows (sRGB) are unchanged.

### 4–6. Badges on Ready, In journal and Blocked

One badge system serves four states. `badge()` in `gen5.py` uses round 4's job-badge frame unchanged:
- centre (95, 95);
- keyline r 24, which breaks the medal's silhouette by 4.6;
- the gilt two-slope ring at r 19.9–23.1;
- a seat at r 19.9 with the ring's inner shadow;
- a drop shadow (1.4, 1.9).

Only the seat colour and the content change:

| Badge | Seat | Content |
|---|---|---|
| Ready on another job | Role colour (unchanged) | The game's job icon, optically centred |
| **Ready** | Deep lapis `#4A72C4` → `#1E3470` | An **open padlock** in warm gilt (`#F4DFA6` → `#D9BE82` → `#9A7A40`): the shackle is lifted and swung free, there is a keyhole recess, and the bevel is lit from the upper left. Warm and inviting, and the only warm mark on the cool medal besides the afterglow. |
| **Blocked** | Night enamel `#2E3A66` → `#111832` | A **closed padlock** in cool, muted moonstone pewter (`#B4C0DA` → `#8292B8` → `#4E5C82`), with the shackle seated. |
| **In journal** | Tide silk `#5674B8` → `#243C78` (the ribbon's blue) | An **open book** in gilt: two curved pages, a darker board behind, the right page turned slightly from the light, faint page lines at hero size, a spine line, and a small silk ribbon at the spine. |

- **Centring.** Each lock is centred on its whole bounding box (shackle and body); the book is lowered 1.6 for its ribbon tail. Both locks share one body, so they read as a pair.
- **The book is original.** The official Journal main-command icon (`000005`, https://v2.xivapi.com/api/asset?path=ui/icon/000000/000005_hr1.tex&format=png) was checked: it is a gold "!" on red, the quest marker, not a book. It would read as an alert and clash with the lapis, so I didn't use it. It remains an option for the owner.
- **Below 32 px** (`_row/`): the medal is drawn without its badge, and the badge content is drawn at text height after it. For jobs that is the game's job icon; otherwise it is `_row/badge-open.svg`, `badge-closed.svg` or `badge-journal.svg`. The rows in `_variants.png` show this at 16 and 20 px.

### 7. Gold check only

`completed.svg` keeps the gilt check. Its moon is a hair darker (`#D8DFED` → `#7B8AAF`), which keeps Ready 1.3× ahead now that Ready's badge covers some of its bright lapis.

### Other changes

- **Ready on another job's crescent** now uses Ready's full brightness. The quiet version had dropped RoJ-Blk to 10.7 at the row tier, and "ready, on that job" is still Ready.
- **In journal's night scene** is a little lighter: sky horizon `#3C528A`, sea `#2A4080` → `#18244E`, road at full 1.0× of its night value, Fresnel band .30.
  - This separates In journal from Ready on another job, which share a moon.
  - The sea stays darker than the sky at the horizon (L .421 against .434).

## Round 5 metrics

```
as shipped (badges in the SVG)
r5 16px grey:      weakest Lock-NotC 12.3, Blk-Lock 13.4, RoJ-Jrn 13.8 | salience Rdy=81 RoJ=42 Jrn=53 Blk=41 Done=43 Comp=61 Lock=40 NotC=40
r5 16px deut+grey: weakest Lock-NotC 12.4, RoJ-Jrn 13.6, Blk-Lock 13.9
r5 20px grey:      weakest Lock-NotC 22.4, Blk-Lock 22.5, RoJ-Jrn 23.5 | salience Rdy=127 ... Comp=96
row tier (_row, no badges: what 16-20 px rows actually draw)
row 16px grey:      weakest Lock-NotC 12.3, Blk-Lock 12.5, RoJ-Blk 13.0 | salience Rdy=85 RoJ=38 Jrn=52 Blk=40 Done=43 Comp=61 Lock=40 NotC=40
row 16px deut+grey: weakest Lock-NotC 12.4, RoJ-Blk 12.9, Blk-Lock 13.0
row 20px grey:      weakest Blk-Lock 20.3, Lock-NotC 22.4, Blk-Done 22.4 | salience Rdy=135 ... Comp=96
```

| Target (16 px grey) | Shipped | Row tier |
|---|---|---|
| Weakest pair ≥ 12 | 12.3 | 12.3 |
| Ready ≥ 1.3× the next state | 81/61 = 1.33 | 85/61 = 1.39 |
| Completed ≤ 0.8× Ready | 0.75 | 0.72 |

## Round 5 doubts

- **Ready's lead is thin when the badge is shown:** 1.33 at 16 px and 1.32 at 20 px. The badge covers part of the bright lapis. In the plugin, rows under 32 px draw no badge (1.39), so the thin case only occurs if a badged SVG is rendered small.
- **The icon's road crosses the Installed corner.** Centring it makes that unavoidable. Its peak row sits just above the corner (y 305 against the corner's 312), but the next rows inside are still 0.94× the peak. Check the overlay in the real installer.
- **Blocked a and c fail the row tier** (11.8 and 11.2). Only b passes. If the critic prefers a or c, its clouds need retuning.
- **Badges cover part of Ready's and In journal's road and Ready's afterglow.** The badge is raised and casts its shadow, so the overlap is physically fine, but less of the moon road shows at hero size.
- **The Kugane shore is original silhouette work after reference art,** not traced. At 32 px it is only a dark band.

---

# Round 4 (history)

Round 3's approved medals, reworked from the owner's notes 0–7 and the player tester's ideas in `../brief.md`. Concept D stays: every quest state is a minted FFXIV job-icon-style medal (a moon emblem on lapis enamel inside one shared champagne-gilt two-slope rim), and the icon is the moon road on calm water past a stone lantern.

**Generator:** `_src/gen4.py` writes every SVG in this folder. `_src/gen3.py` is round 3's generator, kept for reference; don't run it, because it writes round 3's files. `_src/variants_sheet.py` renders `_variants.png`.

**Files**

| File | What it is |
|---|---|
| `ready.svg`, `in-journal.svg`, `blocked.svg`, `done-this-cycle.svg`, `completed.svg`, `locked-out.svg`, `not-checked.svg` | The standard states, 128 viewBox, transparent |
| `ready-on-another-job.svg` | The default Ready on another job (the Paladin example) |
| `ready-on-another-job-paladin.svg`, `-bard.svg`, `-white-mage.svg` | One job from each role, which shows the badge works for any job |
| `completed-green.svg` | Completed with the tester's green check, for the owner to compare with the gilt check |
| `plugin-icon.svg` | The 512 icon |
| `_sheet.png` | The standard sheet from `../render_sheet.py` |
| `_variants.png` | The job variants and both checks at 16, 20, 32, 64 and 128 px, plus a 16 and 20 px quest-list mock with the row fallback |
| `_src/jobs/*.png` | The three game job icons that are embedded in the badge |

## Supervisor round 1 fixes

These respond to `../supervisor/art-review-1.md` (CHANGES REQUIRED, three items), its recommendation on doubt 3, and two optional polish items.

| # | Fix | Done |
|---|---|---|
| 1 | Teal fringe on blurred dark shadows | `blur_filter()` and the inline `ib`, `fs` and `mbl` filters now set `color-interpolation-filters="sRGB"`, so every blur in every file runs in sRGB. Sampled under Blocked's clouds at 512 px, the shadow is now (37–38, 45–46, 78): blue, with no pixel where green leads red and matches blue. |
| 2 | Crest glints read as glossy pills | The crisp upper lens is replaced by a soft centred core with no edge of its own. **Icon:** `streak(x0 + L·.18, x1 − L·.18, y, h·.45)` at `o·.35`, in one group blurred with stdDeviation 2 (sRGB). **Ready and In journal:** the same core at `o·.3·k`, blurred 0.5 (sRGB). The `skew` is 0.5 (centred), because `streak()` cannot take 0. At 1024 px the cores are a soft brightening inside each ripple, with no edge. Road peak: icon .853 = 0.89× the moon, Ready 0.90×, In journal 0.72×. |
| 3 | In journal's horn ends on the ribbon's edge | The whole ribbon group (path, stitches, crest and inner-slope bands, shadow) moved **+5 in x** (x 24.5–42), so the silk covers the horn by about 7.7 units. The −6 option was rendered too: it pushed the ribbon onto the rim for most of its length, so it no longer dropped into the well. +5 reads better. RoJ-Jrn is 12.8 at 16 px (was 12.9) and 21.8 at 20 px. |
| Doubt 3 | Done's arrow echoes the rim | The arrow now spirals outward from tail to head: radius 33 at the tail (132°) to 39 at the head (25°). The band is 8 wide (was 7), and the head is 17.5 wide so it stays 5.5 units off the rim. Its bevel highlights ride the same spiral. On the left, the gap to the rim grows from about 4 to 16 units, which shows as visible enamel from 20 px. The opening at the bottom is about 87°. |
| Polish 2 | Silver lining on Blocked's clouds | A 0.6-unit ring just outside each cloud's outline (`feMorphology` dilate minus the cloud), `#DCE2EE` at .25, clipped to the moon's disc. It shows only where a cloud's edge crosses the moon. |
| Polish 3 | Locked out's socket lip | The socket's lit lower-right lip is now 1.8 wide at .42, from 15° to 140°, so it shows where the falling shard has exposed it. |

The green-check variant is kept for the owner.

## The owner's notes

### 0. "The moon needs more details. The water reflections need some more detail."

All detail is smooth tone. There are no craters, no rings and no hard-edged spots.

**Moon**
- **Crescents** (Ready, In journal, Ready on another job, the icon) share one treatment.
  - The lit body brightens across its width, from Moonstone mid at the terminator to Moonstone high at the limb.
  - There is one soft terminator band and one soft limb band, both blurred inside the lit clip.
  - The lit body carries the maria a young waxing crescent really shows near its limb: Crisium, Fecunditatis, Nectaris and the edge of Tranquillitatis. They are blurred ellipses at 10–32% opacity (blur 0.06 r on the glyphs, stdDeviation 5 on the icon), so they read as value shifts, not spots.
- **Full moons** (Completed and Blocked) have a radial body lit from the upper left and eight maria in their real layout: Imbrium, Serenitatis, Tranquillitatis, Crisium, Fecunditatis, Nectaris, Nubium and Procellarum. They are blurred at 0.07 r, and their key-lit limb band is now soft-edged.
- **Done's half moon** carries the western maria on its lit half, blurred at 1.8.
- **Not checked's veiled moon** has three faint dark maria on its earthshine body.

**Water** (the icon, Ready, and In journal at lower strength)

| Detail | Icon | Glyph scene |
|---|---|---|
| Varied ripple lengths | Rows 307, 323 and 368 are now a long streak beside short chips; row counts run 1,2,1,2,2,2,3,1,2,1,2,1 | 8 rows; short chips beside long streaks |
| Crest glints | Each long streak (h ≥ 7, length ≥ 30) gets a soft centred hot core at 0.35× the streak's opacity, blurred 2 (supervisor fix 2) | Same idea on every dash with h ≥ 2.5 and length ≥ 12: 0.3×, blurred 0.5 |
| Faint sky reflection | A 26-unit Fresnel band under the horizon in the sky's horizon colour (`#3A4C7A` at .32 → 0), and the moon's bloom mirrored as a broad glow centred on the road (6%) | A 12-unit band under the horizon in the sky's horizon colour (.55 Ready, .45 In journal) |
| Soft moon column | A lens-shaped radial glow under the road, 15% → 5% → 0, scaled 70 × 150 about the mirror point | A radial column, scaled 22 × 40, about the mirror point at the bottom edge |
| Water texture | Wind ripples across the whole sea: a pale crest (`#5A6C9C`) over a dark trough, foreshortened toward the horizon, .16 and .22 left of x 240 and .07 and .10 to the right, so the Installed corner stays quiet | none (too fine at 128) |

Every glyph dash keeps an aspect of 4.6–8.0, so none of them reads as a pebble or a leaf. Every icon streak keeps 3.9–15.2, the same as round 3's minimum.

### 1. "The Ready border doesn't have a full complete trim like the others."

- Ready now uses the same complete `bezel()` as the other seven. The gap and its end faces are gone, and the road stays inside the well.
- Ready stays the loudest state through what it shows, not through a broken rim:
  - **Brighter lit lapis.** The sky runs `#6090DE` → `#8AB2F3` toward the horizon, and the sea runs `#5480C8` → `#30529A`. The sea stays darker than the sky at the horizon (L .676 against .766).
  - **A brighter crescent.** It is r 29 (was 27.5) and thicker (k −0.18, was −0.24), with a 26% cool halo.
  - **The moon road inside the medal,** with its crest glints and column.
  - **A warm inner glow.** This is a twilight afterglow on the horizon, placed where the physics puts it. The crescent's lit limb points 28° below the horizontal, toward the set sun, so the glow is centred on the horizon at x 120.5, under that direction (`#EDCB94`, .55 → 0). Its reflection lies directly below it on the sea at .30. It is the only warm note in the cool scene, so it gives Ready the warm/cool balance the other states get from the gilt alone.
- Ready is 1.37× the next state at 16 and 20 px (see the metrics).

### 2. "The crystal in 'Ready, other job' seems weird. Give it a more appropriate icon to reference a job."

- The soul crystal is gone. Ready on another job is now Ready's crescent, at Ready's place and tilt, resting at night, with a **job badge**: a raised mini-medal in the lower right that breaks the medal's silhouette. It holds the game's own job icon on a role-coloured enamel seat.
- The three examples are Paladin (tank blue), Bard (DPS red) and White Mage (healer green).
- In the plugin, the badge shows the real icon of the job the quest is ready on, read from the game at runtime. See "Job-badge frame spec" below.
- **16 px fallback.** Under 32 px the job glyph is mush, so the badge is drawn as a role-colour pip in its gilt ring, and the row draws the game's job icon at text height right after the medal. FFXIV players read these at party-list size. The 16 and 20 px rows in `_variants.png` show this.

### 3. "The Blocked icon doesn't make sense. Maybe reference clouds blocking the moon?"

- Blocked is now a full moon at rest value (`#DCE2EE` → `#8A97BA`, r 26, up and right at (76, 47)) with two cumulus banks drifting across it:
  - a small one over its upper-left limb;
  - a large one hiding its lower-left half and spilling over the enamel.
- The moon is there, but you can't reach it yet.
- The clouds are raised relief, lit by the key light:
  - a body that darkens toward a flat underside (`#6F7BA2` → `#46507A` → `#262E4E`);
  - each billow rounded by its own upper-left highlight;
  - a soft lit rim on the upper-left edges and a shaded rim on the lower-right edges;
  - a soft shadow cast down and to the right (dx 1.7, dy 2.3, blur 1.3), which lands on the moon and the enamel.

### 4. "Done this cycle doesn't visually make sense. If it involves repeatables, then something that represents repeat."

- Done this cycle is now a waning half moon (this cycle's light is spent) inside a gilt **repeat arrow**.
- The arrow sweeps clockwise from the lower left (132°), over the top, to the lower right, and its head points on round the circle.
- The tail tapers in from a fine point, like a moon's path rather than a UI stroke.
- An 86° opening at the bottom keeps it from closing into an orbit ring.
- The moon fills the middle, so the mark reads as "a moon that comes round again", not as a bare refresh button.
- The arrow is raised gilt:
  - its outer edge catches the light where it faces the upper left;
  - its inner edge is shaded there and lit on the right, where it faces the light;
  - it casts the standard emblem shadow.

### 5. "Repeated should be a full, bright moon with nice details." (read as Completed)

- Completed is a full, bright, detailed moon:
  - eight soft maria;
  - a body lit from the upper left (`#DCE2EF` → `#AEBAD4` → `#808FB4`);
  - a soft key-lit limb band.
- It keeps its check, struck across the moon's lower-right edge and out through the medal's outline.
- The moon is held a step under Ready in value and is r 35, so Completed recedes in a list without losing detail: 0.73× Ready at 16 and 20 px.
- Two checks are offered:
  - **gilt** (`completed.svg`);
  - **green** (`completed-green.svg`): jade `#A9DCA2` → `#6FAE79` → `#3F7A50`, with a dark green inner keyline and the same bevel. It is held under OKLCH C 0.10, so it sits with the champagne and lapis rather than as a UI-success green.
- Both measure within one point of each other.

### 6. "I don't like the large broken band; it's too thick." (Locked out)

- The thick black crack is gone. Dalamud is now **shattered**:
  - the impact point is up and left of centre (59, 57);
  - seven shards are split along fine cracks that each bend twice;
  - each shard is pushed 2–2.8 units out along its own bisector and turned 1–2°;
  - one shard at the lower left is falling away: 6 units out, turned 9°, breaking the disc's silhouette.
- **Gone for good.** The empty socket behind is near-black, and its lower-right wall is faintly lit like the inside of a recess.
- **Light on the shards.**
  - Every crack face whose outward normal turns toward the key light gets a fine pale edge (`#F4AAB2`, width and opacity scaled by how squarely it faces the light).
  - Faces turned away get a dark edge.
  - The limb keeps a key-lit band on each shard.
  - The shards cast the emblem shadow into the socket.
- **Colour.** It is a truer Dalamud red than round 3's milky rose: `#DA6470` / `#C24A58` / `#7A2838`. The supervisor's polish note asked for more body.

### 7. "'Not checked' doesn't make sense. Obscurity, but not like Blocked. Or question marks?"

- Not checked is a **veiled moon whose lit sliver is the question mark**:
  - **The moon.** A dim earthshine body (`#4A5B90` → `#2A3866`, soft edge, three faint maria) sits in the upper well.
  - **The bowl.** It is that moon's lit limb: a crescent in moon-silver that starts at a fine horn on the left, swells over the top, and turns down into the stem.
  - **The dot.** A tiny full moon.
- **Lighting.** The mark is lit like a crescent, brightest along its outer limb with a soft terminator on its inner edge, and the key light falls across it from the upper left.
- **Against Blocked.** There are no clouds and no mist, so nothing covers the moon. You just can't make it out yet.
- **Against a help button.** The question mark is built from the moon. It is not a glyph pasted on a coin.

## The tester's ideas

| Idea | Decision |
|---|---|
| In journal = Ready plus the bookmark | **Adopted.** In journal is Ready's own scene at night (`#1B2856` → `#2A3D72` sky, `#22346A` → `#111A3C` sea), with the same crescent dimmed (Moonstone low → mid → base) and the road at 0.75× in Moonstone mid, plus the Tide-silk ribbon (now 17.5 wide at x 24.5–42, to y 76, covering the crescent's lower horn). It makes the family coherent: Ready, In journal and Ready on another job share one moon. |
| Ready with a class symbol, current job bright, others dimmed | **Partly adopted** through note 2. Ready on another job is Ready's moon dimmed, with no road (the road opens when you're on that job), plus the job badge. |
| Green check on Completed | **Made** as `completed-green.svg`, beside the gilt one. The owner decides. Our lean is gilt: it keeps one metal across the set and recedes better in a long list. Green reads faster as "done" for players who don't know the set. |
| Locked out: completely shattered, gone | **Adopted.** It matches note 6: the shards have broken apart and one is falling away. |
| Blocked: new moon (black) | **Not adopted.** The owner chose clouds, and a black disc gives the clouds no bright silhouette to cross at 16 px. The moon behind the clouds is held at rest value instead of full brightness, which keeps a little of "not lit for you". |
| Done: eclipsed moon with a flare | **Not adopted.** The owner chose a repeat symbol. |
| Not checked: blank, no moon | **Partly adopted.** The medal shows no lit moon, only the earthshine body that the question mark's sliver belongs to. |
| Ready as a moon lander or rocket | **Not adopted.** It breaks the calm moon family. Recorded for the owner. |

## States

| State | Metaphor and build |
|---|---|
| **Ready** | The icon in miniature, now with a full rim: a bright lapis twilight over a calm sea, a thick crescent with soft maria, its road of crest-lit ripples, and a warm afterglow where the sun has set. "Go now." |
| **Ready on another job** | Ready's crescent at rest, with no road, and a gilt job badge in the lower right holding the game's own job icon on a role-colour seat. "Ready, on that job." |
| **In journal** | Ready's scene at night, quieter, with the Tide-silk bookmark wrapped over the rim. "You've started this one." |
| **Blocked** | A full moon with two clouds drifting across it, the clouds lit from the upper left and casting soft shadows. "It's there, but you can't reach it yet." |
| **Done this cycle** | A waning half moon inside a gilt repeat arrow, open at the bottom. "Done; it comes round again." |
| **Completed** | Menphina's full moon, bright and detailed, with the gilt (or green) check struck through the rim. "Done for good." |
| **Locked out** | Dalamud shattered: thin cracks, the shards pushed apart, one falling away from an empty socket. "Gone, and not coming back." |
| **Not checked** | A veiled moon whose lit sliver is a question mark, dotted with a tiny full moon. "Unknown yet." |

## Job-badge frame spec (for implementation)

The badge is a reusable frame: everything except the icon is fixed, and the icon drops in. All numbers are in the medal's 128-unit space; at draw size `s` px, multiply by `s / 128`.

| Part | Spec |
|---|---|
| Position | Centre (95, 95), lower right. Its keyline reaches r 67.8 from the medal centre, which breaks the medal's silhouette by 4.6 units. That is a clear overlap, not a tangent. |
| Drop shadow | The whole badge casts dx 1.4, dy 1.9, stdDeviation 1.1, Abyss at .65 onto the rim and enamel. |
| Keyline | A disc of r 24, Abyss `#080B16`. |
| Gilt ring | The same ramp as the medal bezel. Outer slope r 21.5–23.1 (light upper-left → dark lower-right); inner slope r 19.9–21.5 (reversed); crest seam at r 21.5; specular arc on the outer slope from −170° to −100°. |
| Enamel seat | r 19.9, a radial gradient centred at (90, 89), r 29.9, from the role's light stop to its dark stop. The ring casts a soft shadow onto the seat's upper-left inner edge (offset 1.4, 1.9, blur 0.8, .5). |
| Role colours | Tank `#5878C2` → `#2C417E`; healer `#5C9A68` → `#2B5A38`; DPS (melee, ranged and caster) `#B25A64` → `#5E2632`. All are under OKLCH C 0.13. |
| Icon slot | A 35.5-unit square centred at (95, 96.25). The game's job glyphs sit about 2/56 above the centre of their own frame, so the slot is lowered 1.25 to centre the glyph optically. The icon is drawn unframed with its own alpha. Its glyph stays inside the seat, so no circular clip is needed at runtime (the SVG clips anyway). |
| Runtime source | `ITextureProvider.GetFromGameIcon(new GameIconLookup(62000 + classJobId))`, the unframed gold job glyph (Paladin 62019, Bard 62023, White Mage 62024). Use the `_hr1` 56 px texture when the slot is 28 px or larger (128 px medal: slot 35.5 px, 1.6× shrink). Otherwise use the 28 px base texture, so the shrink stays at 2× or less (the atlas rule). The role comes from `ClassJob.Role` (1 tank, 4 healer, otherwise DPS). |
| Draw order | Medal (atlas), then the badge shadow, keyline, ring and seat (one atlas sprite, with the seat as a white mask tinted by the role colour), then `AddImage(jobIcon, slotRect)` with no tint. |
| Row fallback (medal under 32 px) | Skip the icon. Draw the ring and the role-colour seat only (a pip), then draw the job icon itself at text height (16–20 px, from the 28 px base texture) immediately after the medal, before the quest name. |

## Icon (512 master)

The composition, frame, lantern, headland and road geometry are round 3's, which the supervisor approved.

**Moon**
- The lit body brightens from a soft terminator to the limb (Moonstone mid → base → high across the lit width).
- The blurred terminator and limb bands are kept.
- Four soft near-limb maria are added (stdDeviation 5). Round 3's blur filters were sized for the 128 glyphs; the icon's are now sized for 512, so this detail actually renders.

**Water**
- Crest glints on the long streaks.
- Varied lengths in rows 307, 323 and 368.
- The Fresnel sky band and the mirrored bloom.
- The wind-ripple texture across the sea.
- Row 368's streak is longer (−26 → 24) and row 492 is a single 70-unit streak again, so no streak is shorter than aspect 3.9.

| Measure (`OKLab L`, rendered at 512) | Round 3 | Round 4 |
|---|---|---|
| Moon max | .951 | .961 |
| Road peak | .816 = 0.86× moon | .853 = 0.89× moon (soft crest cores) |
| Road axis vs lit centroid x 172.0 | 170.2 | 171.1 |
| Brightest row (rule y 431) | 436 | 436 |
| Horizon sky / sea (x 250–300) | .385 / .345 | .385 / .364 (the sea is still darker: Fresnel band) |
| Installed corner (x 248–488, y 312–488) L std | .061 | .062 |
| Glints | 17 in 12 rows, aspect 3.9–15 | 20 in 12 rows, aspect 3.9–15.2 |
| Road x span | 117–231 (Installed check zone from 248) | 117–233 |
| File size | 26 KB | 63 KB (the ripple texture is 4 path elements) |

The lantern, its four warm needles, the pool in front of the base, the islet reflection, the headland mirror and the earthshine disc (1.12:1) are unchanged.

**Ready's scene** (128 glyph, rendered at 512)
- The moon max L is .961 and the road peak is .868, which is 0.90× the moon.
- The road axis is x 63.1 against a lit centroid of 61.8.
- The mirror point falls at the bottom edge, so the rows brighten toward the viewer (.46 → .74).
- In journal's road peaks at 0.72× its moon.

## Light, palette and sources

**Light.** One UI key light from the upper left (azimuth 135°, about 45° elevation) on every rim, the badge, the arrow, the check, the clouds, the shards and the frame.
- Raised things are bright on their upper-left edges and dark on their lower-right ones, and they cast a soft shadow down and to the right: emblems at 1.1/1.6, the clouds at 1.7/2.3, the badge at 1.4/1.9, the ribbon at 1.2/1.0.
- Recesses are reversed: the enamel well, the badge seat, and Dalamud's empty socket.
- Moon phases carry meaning, so each moon's phase lighting is its sun, as in round 3.
- Inside the scenes, the moon and the lantern are the lights, and every reflection falls directly below its source, below the horizon only.

**Palette**

| Group | Tokens |
|---|---|
| Keyline, shadow | `#080B16` (Abyss) |
| Gilt (rims, badge ring, check, arrow, frame) | `#FFF4D6` / `#E6CF98` / `#D9BE82` / `#9A7E4A` / `#7C6236` / `#5C4724` / `#33260F`, unchanged |
| Lit lapis (Ready) | sky `#6090DE` → `#8AB2F3`, sea `#5480C8` → `#30529A` |
| Afterglow (Ready) | `#EDCB94` (OKLCH C .081) |
| Night scene (In journal) | sky `#1B2856` → `#2A3D72`, sea `#22346A` → `#111A3C` |
| Resting enamel | `#1D2B5A` → `#131C40` |
| Moonstone | `#F4F2EA` / `#E2E8F4` / `#C3CEE4` / `#95A5C8` / `#5E6E97` |
| Cloud (Mist family) | `#BAC3DB` / `#6F7BA2` / `#46507A` / `#262E4E` |
| Ribbon | Tide `#6F8FD0`, with `#A9BEEA` / `#3F5A98` |
| Dalamud | `#DA6470` / `#C24A58` / `#7A2838`, crack edge `#F4AAB2`, socket `#0B0408` |
| Jade (green check) | `#E4F6DC` / `#A9DCA2` / `#6FAE79` / `#3F7A50` |
| Role seats | tank `#5878C2` / `#2C417E`, healer `#5C9A68` / `#2B5A38`, DPS `#B25A64` / `#5E2632` |
| Lantern | `#E0B860` |

**Web sources.** The job icons are © Square Enix and are embedded as PNG data URIs, with the owner's permission, for the concept only. The shipped plugin reads them from the game, so it redistributes nothing.
- Paladin job icon 062019: https://v2.xivapi.com/api/asset?path=ui/icon/062000/062019_hr1.tex&format=png
- Bard job icon 062023: https://v2.xivapi.com/api/asset?path=ui/icon/062000/062023_hr1.tex&format=png
- White Mage job icon 062024: https://v2.xivapi.com/api/asset?path=ui/icon/062000/062024_hr1.tex&format=png
- Framed role-colour versions 062119 / 062123 / 062124, studied for the role colours and the gilt frame: https://v2.xivapi.com/api/asset?path=ui/icon/062000/062119_hr1.tex&format=png (and `062123`, `062124`)
- Round 3's references (the framed job icon, quest markers, Menphina, the tōrō) still apply.
- The lunar maria layout is general lunar geography; nothing is traced.

## Metrics

```
(after supervisor round 1)
r4 16px grey:      weakest Lock-NotC 12.3, RoJ-Jrn 12.8, Blk-NotC 14.0 | salience Rdy=85 RoJ=40 Jrn=48 Blk=47 Done=43 Comp=62 Lock=40 NotC=40
r4 16px deut+grey: weakest Lock-NotC 12.4, RoJ-Jrn 12.7, Blk-NotC 13.9 | salience Rdy=85 RoJ=40 Jrn=48 Blk=47 Done=44 Comp=62 Lock=42 NotC=40
r4 20px grey:      weakest RoJ-Jrn 21.8, Lock-NotC 22.4, Blk-NotC 25.3 | salience Rdy=135 RoJ=60 Jrn=75 Blk=75 Done=68 Comp=98 Lock=63 NotC=62
r4 20px deut+grey: weakest RoJ-Jrn 21.6, Lock-NotC 23.2, Blk-NotC 25.2 | salience Rdy=134 RoJ=61 Jrn=75 Blk=75 Done=68 Comp=98 Lock=67 NotC=62
with Bard as RoJ (16 grey):        weakest Lock-NotC 12.3, RoJ-Jrn 12.6
with White Mage as RoJ (16 grey):  weakest Lock-NotC 12.3, RoJ-Jrn 13.7
with the green check (16 grey):    weakest Lock-NotC 12.3, RoJ-Jrn 12.8; Comp=61 (0.72x); 20px Comp=96 (0.71x)
daylight 16px: salience Rdy=73 RoJ=121 Jrn=112 Blk=111 Done=115 Comp=97 Lock=118 NotC=118; weakest Lock-NotC 12.3
```

| Target | Result |
|---|---|
| Weakest pair at 16 px grey ≥ 12 | 12.3 (12.4 with deuteranopia); 21.6 or more at 20 px |
| Ready ≥ 1.3× the next state | 85/62 = 1.37 at 16 px, 135/98 = 1.38 at 20 px |
| Completed ≤ 0.8× Ready | 0.73 at 16 and 20 px; the green check is 0.72 |
| Reads as its meaning | Clouds read as cumulus from 28 px, the arrow as repeat from 28 px, and the question mark from 16 px (see the self-check) |

## Iterations

1. **First pass.** Ready 1.21× Completed and Completed 0.83×; RoJ-Jrn 11.2; the Locked-out core ring read as a crosshair; Done's arrowhead was too small.
2. **Fixes.** Brighter lapis and a toned, smaller Completed moon; a bigger badge; cracks without a core ring; a bigger arrowhead; a stronger veiled moon. Lock-NotC was 11.7.
3. **Margins.** Blocked's moon moved up and right; Dalamud enlarged to r 37.5; the ribbon widened; In journal's road brightened. The weakest pair reached 12.9.
4. **Realism pass.**
   - Every chip at aspect 4.5 or more.
   - Done's and the moons' maria blurred further.
   - Softer cloud rims.
   - Not checked's moon became an earthshine body, and its question mark got bolder.
   - Crest glints toned down so the road stays at 0.88–0.90× the moon.
   - The Fresnel band toned down so the sea stays darker than the sky.
   - Blur filters fixed for the 512 icon.
   - The icon file cut from 352 KB to 63 KB.
   - Truer Dalamud red.

## Honest self-check

- **Not checked at 16–28 px** still reads mainly as "a question mark in a ring". The moon integration (the crescent bowl, the earthshine body, the moon dot) shows from about 48 px. At row size the gilt rim and family context are what keep it from being a generic help button.
- **The badge is unreadable below 32 px.** At 16–20 px it is only a role-colour pip, so the row fallback (the game's job icon at text height beside the medal) is part of the design, not optional. The badge's role colour also brings a small saturated accent into an otherwise calm medal.
- **Lock-NotC is the thinnest pair at 12.3** (the target is 12). The truer red cost about 0.3; the rose red measured 12.6.
- **Daylight.** Ready has the lowest luminance contrast on a bright ground (73 against 97–121), as in round 3, and a little more now that its lapis is brighter. Its lead holds on the Night ground only. It still differs by hue and by being the only scene with a horizon.
- **Done's arrow** now spirals out from radius 33 to 39, so it shows enamel between itself and the rim from 20 px. At 16 px the arrow and the rim still sit about one pixel apart.
- **Saturation.** Dalamud is now at OKLCH C .15–.16, above round 3's .13, on purpose: it is the one alarm state. Everything else stays under C .13 (the gilt under .09).
- **Light conventions.** The clouds and shards are relief lit by the key light, while the moons behind them are phase-lit. This is the medal convention round 3 established, but a supervisor may want it stated per glyph.
- **Ready's road** peaks at 0.90× its moon, at the top of the brief's "about 90%".
- **The icon file** grew from 26 KB to 63 KB because of the ripple texture. That is still small for a 512 master.

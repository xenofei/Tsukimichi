# Ishgard Glass

## Pitch

The moon is a stained-glass roundel from the Holy See's cathedrals. Lit glass is moonstone ivory and dark glass is lapis. Lead came divides the panes, and a two-tone bevelled metal came frames the roundel. The phase is simply which panes are lit. Detail comes from **material and structure**, never from surface texture: the came, a single diagonal sheen across the glass, and a slight tint change between alternate lights. That lets the moon carry FFXIV craft without becoming a textured yellow disc. Gilt marks act-now states (Ready only); every other state sits in pewter. The plugin icon is the Ready glyph scaled up: the same tilted glass moon over a calm sea, with its moon road falling beneath the lit glass and an original Ishgardian skyline on the far shore.

## FFXIV idioms borrowed (all original geometry)

- **Ishgard rose-window glass:** ivory and lapis panes, lead came, and voussoir-like lights around the limb, like the outer ring of a rose window.
- **Gilt bevel:** a light–dark–light bevel with one specular hairline, as on job icons and the top plugin icons. It is used on the Ready came and the icon frame.
- **Moon lore:** Dalamud's red for Locked out; Menphina's cool, ice-toned moonstone for the lit glass; the moon road (月の道) itself.
- **Aether crystal:** a faceted job crystal set into the dark glass for Ready on another job.
- **Coerthas and Ishgard setting in the icon:** a snow ridge and a gothic spired skyline with its own small rose window, plus gothic spandrel tracery in the frame's upper corners.
- **Quest-marker grammar:** a dark keyline under every glyph so it holds on bright daylight scenes.

## Tier plan (one SVG serves every size)

| Tier | Flair | What shows |
|---|---|---|
| Row (≤20 px) | Plain | Silhouette, two glass tones, the came rim, and the state mark (road, crystal, ribbon, check, band, mist). Came hairlines are 1.1–1.3 units (≈0.15 px) and vanish. |
| Mid (24–40 px) | Quiet | One straight came (2.6 units) splits the lit glass parallel to the terminator. A gilt terminator line appears. The bevel reads as metal. |
| Hero (≥48 px) | Full | Three voussoir came across the limb pane, a tinted alternate light, faint gilt came mirrored on the dark glass, the diagonal glass sheen, a specular hairline on the bevel, a gilt inlay on Completed's bezel, and a seal edge on the band. All hero lines are at 0.3–0.55 opacity. |

## States, one line each

- **Ready:** the gilt-framed glass moon (gibbous, lit limb 55° below horizontal) with its moon road of three rows of ivory glass shards beneath, centred under the lit centroid. It is the icon in miniature.
- **Ready on another job:** the same moon in pewter with no road, and an aether job crystal set into the dark glass.
- **In journal:** a first-quarter half moon with a gilt journal ribbon hanging from above the came over the dark half, swallow-tailed.
- **Blocked:** a new moon. Ashen earthshine glass with a thin moonstone limb: not lit yet.
- **Done this cycle:** a waning half (lit left) with a gilt check set in the waned half. Waning says "comes back".
- **Completed:** the quiet full moon in dim moonstone glass with no halo. Hero size adds faint came and a gilt inlay in the bezel.
- **Locked out:** Dalamud. Red glass (copper-dark and red panes, same tilt as Ready) sealed by a paper ofuda band running lower-left to upper-right, with swallow-tailed ends just past the came.
- **Not checked:** oborozuki. An unframed, misted disc with a faint lit area and two tapered mist wisps running past its edge. No came, because the glass has not been set yet.

## Icon composition

**Choice.** I tried the rose window over water against composition 3 (journal with a moon clasp, ribbon falling into water) at 32, 64 and 128 px, with the Installed and Disabled overlays. The journal read as a hand mirror or magnifying glass at 64 px, so I kept the rose window.

**Layout (512 master, horizon H = 300)**

- **Moon.** A glass roundel centred at (180, 148), glass r 90 plus an 8-unit gilt came. It is a gibbous with terminator k = 0.25, and the lit limb faces 55° below horizontal (G7e). The lapis dark glass shows the full disc as earthshine.
- **Lit centroid** (180, 148) + 0.75·(4r/3π) along 55° → **x 196.4**, y 171.5.
- **Road axis x 188.2.** That is 8.2 from the disc centre and 8.2 from the lit centroid, so it passes both G7(c) readings. The measured luminance-weighted glade axis is x 186.7.
- **Brightest point.** Designed y = H + (H − 148) = **452**. The brightest glade pixel measured is at y 463.
- **Mirrored moon.** A flattened mirror of the lit glass sits just under the horizon: flipped, y scaled 0.3, lit side on the right, cut into **3 rippled slices** at y 310–339.
- **Glints: 14**, in 6 rows from y 344 to 482.
  - Heights run 14.3 → 20.4 (all ≥ 12), and row spacing widens toward the viewer.
  - The envelope is a lens: about 84 wide at the top row, widest (about 165) at y 408–462, then narrower in front. Edges are ragged.
  - Row-mean lengths are 36 / 48 / 58 / 40 / 58 / 43 and row-mean gaps 11 / 20 / 10 / 14 / 22 / 15. Consecutive rows differ by at least 15%, and no row mirrors another (checked by script).
  - Glints are opaque ivory dimmed in linear light (same hue), not ivory at partial opacity over blue water.
- A 14% ivory glow column sits under the road.
- **Other elements.**
  - A distant snow ridge.
  - The Ishgard skyline on the right horizon: moonlit rim on its left edges, a tiny gilt rose window, and a faint mirror.
  - Gothic spandrel came in the two upper corners at 28% opacity.
  - A three-band gilt frame (#5A4320 10 / gradient 8 / #F6E6B4 45% 2).

## Palette tokens

| Token | Value | Use |
|---|---|---|
| sumi | #0B0F1C | keyline |
| ivory / ivory-cool | #F3F0E6 / #E2E7F1 | Ready lit glass and icon moon; alternate light |
| moonstone / -alt | #C3CEE4 / #B3C0DA | lit glass for all other states |
| moon-dim / -alt | #7E8EB2 / #7686AB | Completed glass |
| lapis / lapis-2 | #1B2A57 / #22346C | dark glass |
| ash | #2B3658 | Blocked earthshine glass |
| gilt ramp | #FFF3D1 #E9D49C #C9A766 #A88B52 #6B5124 | Ready came, ribbon, check, terminator |
| lead | #8A6E3A | came on pale glass |
| pewter ramp | #8C95B0 #69728F #474F6C #323950 | came for non-act states |
| aether | #9BE6FF #4FB3EA #1F5FA8 | job crystal |
| dalamud red | #A23A38 / #8F3133 / #6A2024 | Locked out glass |
| paper | #EDE3C8 | ofuda band |
| mist | #A9B6D2 | Not checked |

## Self-assessment against the gates

**Metrics (`metrics.py`, final)**

```
glass 16px grey: weakest pairs Blk-NotC 12.9, RoJ-Lock 15.5, RoJ-Comp 15.7 | salience Rdy=59 RoJ=45 Jrn=45 Blk=25 Done=42 Comp=44 Lock=38 NotC=15
glass 16px deut+grey: weakest pairs Blk-NotC 13.0, RoJ-Lock 14.9, RoJ-Comp 15.8 | salience Rdy=60 RoJ=45 Jrn=45 Blk=25 Done=42 Comp=44 Lock=39 NotC=15
glass 20px grey: weakest pairs Blk-NotC 22.1, RoJ-Lock 25.9, RoJ-Comp 26.7 | salience Rdy=97 RoJ=71 Jrn=72 Blk=41 Done=68 Comp=70 Lock=59 NotC=24
glass 20px deut+grey: weakest pairs Blk-NotC 22.2, RoJ-Lock 25.1, RoJ-Comp 26.8 | salience Rdy=98 RoJ=71 Jrn=72 Blk=41 Done=68 Comp=70 Lock=61 NotC=24
```

My own extra pass, same renders:
- Protanopia + grey: 13.0 at 16 px and 22.3 at 20 px.
- Tritanopia + grey: 12.9 at 16 px and 22.3 at 20 px.

**G1 Distinctness: pass.**
- The weakest pair is 12.9 at 16 px (bar 10) and 22.1 at 20 px (bar 16) in every mode.
- No pair differs only by mirror image. In journal and Done are mirrored halves, but they carry different marks (ribbon vs check), on different sides.

**G2 Salience: pass.**
- Ready is 1.31× the next state at 16 px and 1.35× at 20 px.
- Completed is 0.75× / 0.72× Ready.
- Every state except Not checked is at least 25.

**G3 Feature floor: mostly passes.**
- Passing at 16 px:
  - Crystal: 2.4 × 3.75 px.
  - Ribbon: 2.25 px wide.
  - Blocked limb: 1.7 px.
  - Check: 1.5 px gilt (2.1 px with keyline).
  - Band: 2.6 px.
  - Mist wisps: about 1.7 px.
- **Caveat:** Ready's road is 4.25 px tall as a block, but each shard row is only 0.9–1.4 px. At 16 px it reads as one soft "reflection" mass under the moon, not as separate glints.

**G4 Moon identity: needs jurors.** My own risks, highest first:
1. **Locked out** is a red disc with a diagonal light band, so a juror may say "prohibited sign". It has no ring and no empty interior, and the band is paper with swallowtails, but the risk is real.
2. **Done** is a check in a circle. The half-lit moon is the dominant read at 28 px and above, but at 16 px it is close to check_circle.
3. **Completed** at 16 px is a plain filled dot.
4. **Ready** at 16 px may read as a lollipop or a moon on a stand.

Blocked, RoJ, In journal and Not checked read as moons at every size.

**G5 No cheese: pass.**
- No craters, holes or radial ball gradients.
- The lit glass is pale and cool, and gold appears only as metal or signal.
- Ready's gilt came is the largest gold area, an estimated 20–25% of the glyph, much of it dark gilt below C 0.09.

**G6 No Moon Road collision: two flags for the jurors.**
1. **RoJ** sets a crystal inside the dark glass of a gibbous, and that dark area is crescent-shaped. It is not a crystal *over* a crescent, but it is adjacent to the "Moonlit" ornament read.
2. **Ready's gilt came** is flush to the glass, with no gap, so it is a frame, not an orbit ring. Even so, the brief lists "gold ring around a core" as a collision.

No dashed circles, no diagonals inside rings, no centre stars.

**G7 Icon physics: pass on every measured item.**
- **(a)** The glade's hue is 91.5° against the moon's 93.0° (Δ1.5°). Its chroma is 0.011 against 0.014.
- **(b)** The glade peaks at Y 0.731 against the moon's 0.871, which is 84%.
- **(c)** The axis is 8.2 from both the disc centre and the lit centroid. Earthshine is 1.54:1.
- **(d)** 14 glints, all ≥ 12 tall, with the script-verified row variation, thicker and sparser toward the viewer, ragged and lens-shaped.
- **(e)** The lit limb faces 55° below horizontal. Only the true-mirror slices echo the moon's shape.
- **(f)** At the horizon, sky Y is 0.0386 and sea Y is 0.0418.
- **(g)**
  - The rim is 7.0:1 against #101010.
  - The icon survives at 32 px.
  - The Installed corner is quiet (mean Y 0.023), but three glint tips reach x 248–271 inside its top-left edge, where the check covers them.

**Other honest notes**
- **T3 coherence.** This adds more than 2 new tokens: the pewter ramp, paper, mist, ash and moon-dim.
- **Tracery in the icon.** At 256–512 the tilted gibbous with its straight came can read as a glass "egg" rather than a moon to some eyes.
- **Road texture.** The shard road is deliberately glassy (tesserae). It is water-correct in placement and value, but a juror may still call it "paving stones".

`_gen.py` in this folder regenerates every SVG.

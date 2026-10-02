# Menphina's Medallion, round 3

Round 2's concept D, enhanced. Each quest state is a minted medal in the FFXIV job-icon style: a moon emblem on enamel, set in a bevelled gilt rim. The icon is the moon road on calm water, leading past a stone lantern.

Generator: `_src/gen3.py`, which writes all nine SVGs. The other files in `_src/` are round-2 copies kept for reference. Do not run `_src/gen.py`: it writes into `round2/`.

## Supervisor round 1 fixes

These respond to `../supervisor/art-review-1.md` (CHANGES REQUIRED, 14 required fixes) and to the mock reviewer's four glyph notes.

### Required fixes

| # | Fix | Done |
|---|---|---|
| 1 | Squashed crescent mirror at the horizon | Deleted from the icon (`r3i-sl`) and from Ready (`r3r-sl`). The road rows carry the reflection alone. |
| 2 | Road above the horizon, no foreshortening | 12 rows at y 302.5 / 307 / 314 / 323 / 335 / 350 / 368 / 389 / 412 / 437 / 464 / 492, with heights 2.5 → 18. Row 1's top edge is at y 301.1, below the horizon. Gaps grow monotonically from 4.5 to 28. Opacity runs .40 → .80 at row 437, then .70 and .55. The brightest measured row is y 433 (rule 431). Lens half-widths are unchanged. |
| 3 | Secondary glints read as leaves | Every secondary is a thin dash: height 3–5.5 (≤ 8 and ≤ half its row), aspect 5.1–5.5, opacity 0.75× its row's main streak. Row counts run 1,1,1,1,2,2,2,1,2,1,2,1, which breaks the ladder. All glint aspects are now 3.9–15 (median 5.5). |
| 4 | Lantern reflection too dim and fading | Replaced with four needle streaks centred on the window's x (72): y 446 / 457 / 469 / 482, widths 12 / 16 / 22 / 28, heights 2.5 / 3 / 4 / 5, opacity .40 / .52 / .62 / .70, in `#E0B860`. The column brightens and lengthens toward its mirror point at the bottom edge. |
| 5 | Tangents at the lantern | The lantern group and its glow moved +10 to x 72. The islet now starts at local x −70 (under the frame) and ends at local x 40 (global 106.4). Its moonlit rim stroke ends at local 38. The nearest glint is at x 117, so 10.6 units of open water separate them. The eave tip is at global x 32.4, 14 units clear of the frame's inner edge. |
| 6 | The flame lights nothing | A warm pool sits on the islet top: an ellipse at local (0, 436), rx 30, ry 4, `#E0B860` at .25, clipped to the islet. The window jambs and sill are traced by a 1-unit `#E0B860` line at .5. |
| 7 | Lemon-slice bands | The icon's rx 41.8 hairline is deleted. The icon's limb and terminator bands are blurred (stdDeviation 3) inside the lit clip. The same softening (0.8) applies to Ready's and Ready on another job's bands. |
| 8 | In journal's phantom terminator | Only the front (left) half of the terminator ellipse is stroked: `M70 34A13.44 32 0 0 0 70 98`. The body is graded from `#C3CEE4` at the terminator to `#E2E8F4` at the right limb. A soft limb band is added. |
| 9 | Ready's sky upside down | The userSpace gradient runs from y 12 to the horizon (y 80): `#4F78C8` → `#6A95E0`. The sea starts at `#4067AF` at the horizon, so it stays darker than the sky. |
| 10a | Completed's limb ring ignores the key light | It is stroked with a gradient along (0.2, 0.15) → (0.8, 0.9): `#E2E8F4` at .95, `#C3CEE4` at .5 at the midpoint, 0 at the lower right. |
| 10b | Check tip tangent | The long arm now ends at (117.5, 40), and the highlight tip is at (115.1, 38.3). The cap reaches r 66.1, which clears the r 63.2 silhouette by 2.9, so the circle really is broken. |
| 11 | Done's inlay hangs off the moon | `inlay_band()`: the outer edge is flush on the limb (r 34) and the inner edge sits at r 34 − w(θ). w eases from 0.6 at −50° up to 6 at 15° and back to 0.6 at 80°. That is the review's 5 widened inward to 6, as it allows, so the mark survives at 16 px. The keyline and gradient are kept. |
| 12a | Hazy moon lit like a bubble | The ring is deleted. The r 31 disc now carries a radial gradient: `#C3CEE4` at .55 at the centre, `#95A5C8` at .35 at r 28, 0 at r 40. It is blurred 1.6. |
| 12b | Mist brightest away from its light | The wisp gradient now peaks at x 64, over the moon (offset 0.58 on the new 3 → 108 span). The end stops are .35 and .30. The haze under it is a radial gradient centred over the moon. |
| 12c | Wisp clipped at x 0 | The wisp now tapers to a point at x 3, and its haze starts at x 6. |
| 13 | Ribbon physics | The top edge is an arc concentric with the medal at r 66, as a fold wrapping behind it. The strip outside r 63.2 is shaded `#3F5A98`. The horizontal band is replaced by two rim-following bands: a crest highlight at r 58.5–60.5 (`#A9BEEA`, .5) and an inner-slope shade at r 53–56 (`#080B16`, .3). |
| 14a | Filigree echoes the moon | The inner arcs are deleted. Each outer arc is now `M30 110C30 64 64 30 110 30`, with crescent finials at (30, 116) and (116, 30). |
| 14b | Horizon line crosses land | The hairline is now drawn before the headland paths, so the land hides it. |

### Mock reviewer notes

- **Locked out pulls the eye at 16 px.** Dalamud is less saturated now. Ready is 1.97× Locked out in grey and 1.85× with deuteranopia, well over the 1.3× asked.
- **Locked out should sit with Eclipse.** Dalamud is now a rose-biased crimson, `#DA808A` / `#BA5462` / `#742C3C`, between Dalamud red and Eclipse `#B25C7F`, so it sits with the plugin's Eclipse text `#D68AA8`. The lit fracture-wall hairline follows it (`#E8A0AA`). The value is held up on purpose: the first darker rose dropped Blocked vs Locked out to 9.3 in greyscale.
- **Completed recedes, check stays gilt.** The check is still gilt (`#E6CF98` → `#D9BE82` → `#A88B52`). Completed sits at 0.71× Ready at 16 and 20 px.
- **The ribbon's shadow.** The ribbon casts its own soft shadow down and to the right onto the rim and enamel: dx 1.2, dy 1.0, blur 1.1. This is tighter than the emblem shadow because silk lies flat (this also covers polish 9).

### Optional polish

| Item | Status |
|---|---|
| 1. Rim value | Done. The gilt base and mid stops are about 8% darker: `#A88B52` → `#9A7E4A` and `#8A6F3E` → `#7C6236`, on all eight and the icon frame. It is still one colour, and the emblems lead. |
| 2. Ready's end faces | Done. The right arm's end face gets a `#D9BE82` hairline and the left arm's a dark one. The soft shadow onto the sea is not added. |
| 4. Hatching | Done. It is uniform at .15, with no mask that strengthens it toward the lit side. |
| 5. Completed's check | Done, with a darker lower stop, `#A88B52`. The top stop stays bright, because a fully darkened check dropped Completed vs Not checked to 12.3. |
| 7. Islet reflection | Done. It is now as tall as the islet (23 local). |
| 3. Incised rosettes | Not done. |
| 6. Locked-out socket hairline | Not done. |
| 8. Blocked's thinner limb | Tried and reverted: k −0.8 dropped Blocked vs Locked out to 9.3–11.7 at 16 px. Blocked keeps k −0.62. |

## What changed and why (round 3 vs round 2)

### Owner feedback (highest priority)

**1. "The aetheryte crystal doesn't make sense."**
- **Icon.** The crystal, its glow and its water glint are gone. The far shore is now a quiet two-layer headland, with the farther layer paler for atmospheric perspective. Its dark mirror lies directly below it. Nothing on the island emits light, so the only lights in the scene are the moon and the lantern. The moon road now leads past the lantern toward an unlit far shore: the next destination.
- **Ready on another job.** The blue diamond spindle was aetheryte-shaped, so it is replaced by an original soul crystal: an irregular cut shard in a thin gilt setting, amethyst with three facets. This follows how FFXIV draws job stones: Soul of the Monk and Soul of the Bard are flat cut shards, and Soul of the Sage is a shard in a metal setting. A gem in a setting reads as a job stone you equip, not a floating travel crystal. It is violet, not aether blue, and a tiny engraved crescent on its table appears at hero size only.

**2. One border colour for all the icons.**
- All eight glyphs call the same `bezel()` with the same metal, widths, gradients and specular.
- The icon frame uses the same gilt ramp and the same two-slope build.
- There are no pewter, oxidised or iron rims.
- States differ only by:
  - the emblem;
  - the silhouette: Ready's open bezel, the ribbon, the check and the mist all break the circle;
  - value.
- Ready is the loudest because of its lit lapis sky, its road, and the opening in its bezel.

**3. "Enhance it."**

Every glyph:
- **Two-slope turned rim.** The outer slope is bright at the upper left and the inner slope is bright at the lower right. A fine dark crest seam runs between them.
- **Specular streaks.** A strong one sits on the crest at the upper left and a weak one on the inner slope at the lower right.
- **Enamel well.** The raised rim casts a soft shadow onto it at the upper left, and a faint vitreous sheen faces the light.
- **Emblem shadows.** Each emblem casts a 1.1 / 1.6-unit soft shadow down and to the right.

Hero tier: four tiny six-petal moon-daisy rosettes on the rim diagonals (Menphina's flower) and one engraved hairline. The round-2 tick ring is gone, because ticks read as sun rays (B's lesson).

Icon:
- The frame casts a soft shadow onto the scene along its upper and left inner edges.
- The lantern is a proper tōrō: hōju finial, kasa roof with upturned corners, hibukuro firebox with a two-pane lit window, chūdai platform, sao post and kiso base. The flame lights its jambs and the islet.

### Shared fixes from FINDINGS

| Finding | Round 3 |
|---|---|
| Move the check to Completed | Completed is a full moon with a gilt check. The check has a dark keyline, a bevel highlight on its upper-left edges, and a drop shadow. |
| Done gets a "comes back" mark | A waning half moon (lit left). A gilt inlay sits flush inside the dark limb from −50° to 80°, where the light will return. It is widest at 15° and tapers at both ends, with no arrowhead, so it does not read as refresh. |
| Completed must not be a coin or radio button | The moon sits off-centre (up-left), with a key-lit limb band that fades toward the lower right. The check crosses the moon's lower-right edge and its tip breaks the medal's silhouette at the upper right. |
| Not checked: one tapered wisp, no ring, readable on daylight | There is no ghost ring: the medal has the same bezel as the rest. A core-bright hazy moon (oborozuki) with a fading corona sits behind one tapered mist wisp. The wisp is brightest over the moon, which lights it. It crosses the upper face, off the moon's equator, which avoids a Saturn ring. It spills over the left rim and sits on dark enamel, so it survives the daylight swatch. |
| Locked out: thicker, darker crack | The fracture runs top to bottom: a 14-unit cut, with each half pushed 3 units outward, so the gap is about 20 units (2.5 px at 16 px). It is filled with near-black #0B0408. Its walls are lit like real walls: the right half's wall faces the light (light hairline) and the left half's wall faces away (dark hairline). It is vertical and zig-zag, so it is never a 45° no-entry slash. |
| Ready: bezel opens at the bottom, glints cross it, no glow, no stalk | The bezel is cut from 58° to 122°. Its end faces are keylined and lit per the key light. Sea fills the opening, and the two lowest road rows cross it. There is no glow anywhere and the outline stays round, so there is no stalk. |
| Icon: glints at most about 85% of the moon | Peak road L is 0.816 against moon max 0.949, which is 0.86. |
| Icon: glints as horizontal ripple streaks, not leaves | There are 17 needle streaks, flat through the middle and drawn to fine points, with a flatter lower edge. They foreshorten toward the horizon, and the secondaries are thin dashes. Aspect is 3.9–15. |
| Icon: moon and road shifted left, clear of the Installed check | The moon centre moved from (172, 158) to (132, 146). The road spans x 117–231, and the check zone starts at x 248. |
| Icon: one quiet detail on the crescent | Earthshine: the dark disc is held at 1.12:1, under the 1.15 gate, so the road correctly stays on the lit centroid. The terminator and limb bands are soft, with one terminator only. |
| Fold colours into fewer tokens | Iron, oxidised pewter, pewter and aether are all removed. The ribbon reuses Moon Road's Tide and the mist reuses Mist. |

## States

| State | Metaphor and build |
|---|---|
| **Ready** | The icon in miniature. A lit lapis sky that brightens toward the horizon, over a moonlit sea. A thick crescent's lit limb faces 28° below horizontal, with soft bands. Seven rows of road streaks fall under the lit centroid (x 63.8), in the moon's colour and below its brightness, and the last two cross the opened bezel. The scene is flush painted enamel, so the moon casts no shadow on its sky. |
| **Ready on another job** | Ready's crescent at the same tilt, on resting enamel, with a soul crystal set in the dark upper-left part of the disc: "this moon could rise on another job". |
| **In journal** | A graded gibbous moon with a single terminator. A Tide-silk bookmark ribbon wraps behind the medal's top, lies over the rim (lit on the crest, shaded on the inner slope) and drops into the well. It has a swallowtail end and a soft shadow. Stitching appears at hero size only. |
| **Blocked** | A new moon: an ashen earthlit disc and a thin sunlit limb ("not lit yet"). |
| **Done this cycle** | A waning half moon, plus the gilt "comes back" inlay set inside the dark limb. |
| **Completed** | Menphina's full moon with a key-lit limb, and the gilt check struck across its edge and out through the medal's outline. The engraved moon-daisy appears at hero size only. |
| **Locked out** | Dalamud, a rose-crimson moon split by a wide black fracture. |
| **Not checked** | Oborozuki: a core-bright hazy moon behind one drifting wisp lit by it. |

## Icon composition (512 master)

**Moon**
- A crescent at (132, 146), r 80, terminator k −0.36, rotated 30°. The lit limb faces 30° below horizontal.
- The lit centroid is (172.0, 169.1), computed from the same geometry.
- Moonstone #E2E8F4, with a #C3CEE4 terminator band and a #F4F2EA limb band, both blurred 3 so brightness falls off smoothly. There is a 13% sky bloom and no craters.

**Horizon**
- It sits at y 300. A hairline, drawn under the headland, brightens to 62% where the road meets it.
- Sky and sea lightness at x 250–300 are 0.385 and 0.350. The sea is slightly darker, as water reflectance under 1 should be.

**Road**

| Measure | Value |
|---|---|
| Axis | x 169.8, against the lit centroid's 172.0 (Δ 2.2/512) |
| Hue and chroma | Hue 268.6° against the moon's 264.5° (Δ 4.1°). Chroma 0.024 against 0.018 (Δ +0.007). |
| Peak | 86% of the moon's lightness |
| Brightest row | y 433. The rule (H + (H − lit y)) gives 431. |
| Glints | 17 in 12 rows |
| Glint height | 2.5 → 18 toward the viewer |
| Row gaps | 4.5 → 28 |
| Lens half-width | about 19 at the horizon, about 58 at the mirror point, about 35 in front, ragged |

There is also a faint under-glow at 12%.

**Lantern**
- A Hingashi-style stone tōrō at x 72, scaled 0.86 about the horizon. The moon is to its upper right, so its moonlit edges are on the right.
- The window is #E0B860 at L 0.80, which is below the moon. Its jambs, its sill and a pool on the islet top are lit by the flame.
- Four warm needle streaks sit directly below the window, brightening toward the viewer. The islet's dark reflection, as tall as the islet, lies directly below it.
- The islet runs under the frame on the left, with 10.6 units of water between it and the nearest glint.

**Far headland**
- Two silhouette layers on the right horizon, with the farther one paler. Their dark mirror lies directly below at 35%.

**Frame**
- Keyline, then the outer slope (#E6CF98 → #9A7E4A → #7C6236 → #5C4724, light at the upper left), then the inner slope (reversed), a crest seam and an inner keyline.
- A specular gradient sits on the upper-left edge.
- The frame casts a soft inner shadow on the upper and left edges.
- One filigree hairline in each top corner ends in crescents at 28%, at least 27 units clear of the moon.

**Installed-check corner (x 248–488, y 312–488)**
- It holds only the sea gradient and the faint headland mirror: L standard deviation 0.061.

## Light direction

- **Key light.** One UI key light comes from the upper left (azimuth 135°, about 45° elevation), for every glyph and for the icon frame.
- **Raised metal** (rims, the end faces of Ready's opening, check, crystal setting, gilt inlay) is bright on its upper-left faces and dark on its lower-right ones.
- **Recesses** (the enamel well, the fracture) are the reverse: shadow at the upper left inside the edge, light on the lower-right wall.
- **Shadows.** Raised emblems cast a soft shadow down and to the right. The ribbon lies flatter, so its shadow is tighter.
- **Moon phases.** Phase lighting on each moon is the sun's direction relative to that moon. It carries meaning, the same way FFXIV's light-on-dark job emblems carry their own shading. The emblems are flat inlays, not spheres lit by the key light.
- **Completed and Locked out.** Their full discs carry a gentle upper-left to lower-right value gradient that matches the key light. Completed's limb ring follows it too.
- **Inside the icon's scene,** the moon and the lantern flame are the lights:
  - moonlit edges face the moon;
  - the flame lights the jambs and the islet top;
  - each reflection falls directly below its source, below the horizon only.
- **Not checked.** The mist is lit only by the moon behind it, so it is brightest where it crosses the moon.

## Palette tokens

| Group | Tokens |
|---|---|
| Keyline, groove, shadow | #080B16 (Moon Road Abyss) |
| **Gilt (all rims, icon frame, check, inlay, setting)** | spec #FFF4D6, high #E6CF98, light #D9BE82 (Moon Road GiltHigh), base #9A7E4A, mid #7C6236 (Moon Road Gilt #A88B52 less about 8%; Gilt itself is the check's low stop), deep #5C4724, dark #33260F. Desaturated champagne gold: 0.0% of any glyph exceeds OKLCH C 0.09. |
| Lit lapis (Ready only) | sky #4F78C8 (top) → #6A95E0 (horizon), sea #4067AF → #2A4888 |
| Resting enamel | #1D2B5A → #131C40; unlit disc #17224A |
| Moonstone | #F4F2EA / #E2E8F4 / #C3CEE4 / #95A5C8 / #5E6E97 |
| Ash (Blocked) | #424D70 → #2B3352 |
| Soul crystal | #E3D6FF / #A88CEB / #7A5CC8 / #43307E |
| Ribbon | Moon Road Tide #6F8FD0, with #A9BEEA / #3F5A98 |
| Dalamud (rose-biased toward Eclipse #B25C7F) | #DA808A / #BA5462 / #742C3C, wall #E8A0AA, fracture #0B0408 |
| Mist | Moon Road Mist #A9B2CC, with #D3D9E8 |
| Lantern | #E0B860 |

## Web references

All of these were studied for build and proportion. Nothing is traced, and every shape is drawn from original geometry.

**FFXIV icon art (via XIVAPI)**
- Framed job icon 062119 (Paladin): the champagne gilt frame, lapis enamel and top-left light: https://v2.xivapi.com/api/asset?path=ui/icon/062000/062119_hr1.tex&format=png
- Quest markers 071201 and 071221: gold bevel grammar, with highlights on the upper-left inner edges: https://v2.xivapi.com/api/asset?path=ui/icon/071000/071221_hr1.tex&format=png
- Soul of the Monk, Bard and Sage (026004, 026007, 026064): cut-shard job stones and the metal setting: https://v2.xivapi.com/api/asset?path=ui/icon/026000/026064_hr1.tex&format=png

**Lore and furnishings**
- Soul crystals as cut shards: https://ffxiv.consolegameswiki.com/wiki/Soul_Crystals
- Menphina's symbol is the full moon: https://ffxiv.gamerescape.com/wiki/Menphina
- Hingashi tōrō: https://ffxiv.consolegameswiki.com/wiki/Stone_Toro_Lantern and https://ffxiv.consolegameswiki.com/wiki/Toro_Lantern
- Role-colour conventions: https://ffxiv.gamerescape.com/wiki/Dictionary_of_Icons

No raster is embedded. Every file is pure SVG, 7–26 KB.

## Metrics (after supervisor round 1)

```
r3 16px grey:      weakest Blk-Lock 12.8, Lock-NotC 14.3, Comp-NotC 14.5 | salience Rdy=75 RoJ=48 Jrn=53 Blk=37 Done=45 Comp=53 Lock=38 NotC=43
r3 16px deut+grey: weakest Blk-Lock 13.0, Lock-NotC 14.5, Comp-NotC 14.6 | salience Rdy=74 RoJ=48 Jrn=54 Blk=37 Done=45 Comp=53 Lock=40 NotC=43
r3 20px grey:      weakest Blk-Lock 21.8, Lock-NotC 23.6, RoJ-Blk 24.0  | salience Rdy=118 RoJ=72 Jrn=82 Blk=59 Done=70 Comp=83 Lock=59 NotC=66
r3 20px deut+grey: weakest Blk-Lock 22.0, Lock-NotC 24.1, RoJ-Blk 24.2  | salience Rdy=118 RoJ=73 Jrn=82 Blk=59 Done=70 Comp=84 Lock=62 NotC=66
16px prot+grey: Blk-Lock 12.6 | 16px trit+grey: Blk-Lock 12.8 | 20px prot+grey 21.6 | 20px trit+grey 21.8
daylight 16px salience: Rdy=85 RoJ=110 Jrn=106 Blk=121 Done=113 Comp=106 Lock=121 NotC=115
icon: moon L .920 C .0175 h 264.5 | road L .725 C .0244 h 268.6, peak .816 = 0.86 x moon max .949
      road axis 169.8 vs lit centroid 172.0 | brightest row y 433 (rule 431) | earthshine 1.12:1
      horizon sky/sea .385/.350 | rim 7.22:1 vs #101010 | road x 117-231 (check zone from 248)
      17 glints in 12 rows, heights 2.5-18, aspect 3.9-15 (median 5.5) | islet-to-glint water 10.6
saturated gold (C > .09): 0.0 % on every glyph
```

### Against the targets

| Target | Result |
|---|---|
| Weakest pair at 16 px ≥ 12 and better than round 2's 12.4 | 12.8 in grey; 12.6–13.0 across the four vision modes |
| Ready salience ≥ 1.3× the next state | 75/53 = 1.42 in grey, 74/54 = 1.37 with deuteranopia, 118/84 = 1.40 at 20 px |
| Ready ≥ 1.3× Locked out (mock note) | 1.97 in grey, 1.85 with deuteranopia |
| Completed ≤ 0.8× Ready | 0.71–0.72 |
| Every state's salience ≥ 15 | The minimum is 37 |

## Honest self-check

- **Margin on the weakest pair.** Round 3's first pass had 13.1. Blocked vs Locked out is now 12.6–13.0, a thinner margin over 12.4. The rose Dalamud (mock note) costs about 0.3. Every darker or more saturated rose I tried, and the supervisor's thinner Blocked limb, pushed the pair under 12.4.
- **G7d.** The supervisor's foreshortened horizon rows (2.5–9 tall) break the brief's 12/512 minimum glint height by design. At 64 px they merge into the horizon highlight. The review accepts this, but it changes the brief's rule.
- **G6: a gilt ring on every medal.** Owner feedback 2 puts a gold ring on all eight, and a gold ring around a core is exactly the Moon Road orbit-reference ornament.
  - What separates them: a 10-unit two-slope bevel rather than a hairline, a filled enamel well, and broken outlines on Ready, In journal, Completed and Not checked.
  - The darker rim stops help.
  - Still needed: an in-context check next to the ornaments.
- **Coin reads.** At 16 px each medal is "a gold ring with something inside". Every interior differs in silhouette, but "gold coin" is still a possible juror read for Blocked and Done.
- **Daylight.** On a bright ground, Ready has the lowest luminance contrast (85 against 106–121) because its sky is light. It still stands out by being the only saturated blue and by its opened outline, but the 1.3× lead holds on Night only.
- **Done's "comes back" mark.** It is untested on people. At 16 px it is a half moon with a thin gold edge on the dark side.
- **Token budget.** It is still over the 2-new-token allowance: lit lapis, the resting enamel, the moonstone ramp, the soul crystal and Dalamud. The rim, ribbon, mist and keyline reuse Moon Road tokens, though, and Dalamud now leans on Eclipse.
- **Gold on gold.** Completed's check crosses the gilt rim, and only its dark keyline separates the two.

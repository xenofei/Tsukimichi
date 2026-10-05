# Moonfall rich pass 2: game designer supervision, round 1

Date: 5 October 2026
Reviewed: commit 1ef1ab72, `docs/design/v9/rich2/`. I read `spec-rich2.md`, `characters.md` and section 8 of `level-method.md`. I looked at the 8 screens at 1280 and 640, `characters/lineup.png`, the 6 composites at 1× and 2×, and both APNG previews: all 60 frames extracted, with max-minus-min motion maps and frame differences. I compared everything against the approved `../rich/`. I also measured OKLab chroma and hue spread on the map, the title and the six boards, rich against rich2 (script in my scratch folder, not in the repo).

Verdict rule: an asset gets REVISE when it carries a Major or Blocker finding. Minor findings and Nits are listed for fixing, but on their own they do not block an asset.

## Summary

This pass fixes the owner's first complaint. The real FFXIV cast on Triple Triad cards is a large improvement. The character-to-power mapping is strong: an FFXIV player would recognise every pairing, and the twins on Multiball and Tataru on the Draw are excellent. The gilt chrome and the game's own fonts lift every window.

It does not yet fix "it still looks a bit plain". The measurements show where:
- **Colour.** On the boards, "bolder colour" mostly turned up the chroma of the same blue-violet. The second jewel tone that the palette table promises is absent on four of six boards, and the map is unchanged.
- **Fuller boards.** Four of six boards gained 0–3.7% framing, so they are not fuller.
- **Framing that looks solid.** Several of the new framing shapes draw clean edges that a player will take for walls or ramps.
- **Reward beats.** Fever and the tally are still the plainest moments in the game, and no asset shows a power firing.

## Verdicts

| Asset | Verdict |
|---|---|
| title (1280, 640) | APPROVE |
| map (1280, 640) | REVISE |
| characters (1280, 640) | APPROVE |
| levels (1280, 640) | REVISE |
| hud (1280, 640) | APPROVE |
| fever (1280, 640) | REVISE |
| tally (1280, 640) | REVISE |
| pause (1280, 640) | APPROVE |
| characters/lineup.png | APPROVE |
| composites/base-p1 | REVISE |
| composites/base-p2 | REVISE |
| composites/base-p3 | REVISE |
| composites/exp-p1 | REVISE |
| composites/exp-p2 | REVISE |
| composites/exp-p3 | REVISE |
| motion/title.png | APPROVE |
| motion/play.png | REVISE |

## Findings

### Major

**M1. Bolder colour is not delivered: the boards and the map stay single-hue (map, all six composites).**
- **What I measured.** I measured OKLab chroma and the hue spread over the board openings and the map, counting only pixels with chroma above 0.03, in 30° hue bins.
  - **Map:** mean chroma went from 0.039 to 0.041, which is effectively unchanged. The spec says "sapphire and teal", and the map looks the same grey-blue as the approved one.
  - **base-p2:** 84% of the coloured pixels fall in the 270–300° band. The promised "rose-mauve cloud sea" (330–360°) measures 0.00.
  - **exp-p1:** 83% fall in 240–270°. The "turquoise harbour" (180–210°) is 2%, and the "gilt on the domes' crowns" is not visible.
  - **exp-p2:** 93% is blue-violet. The "aquamarine aurora" is a thin teal band at the horizon (y≈420–460).
  - **exp-p3:** 92% falls in 270–300°. The planet, which is the level's subject, lost its blue, white and green and became a lavender monochrome with less contrast than the approved board.
  - **The two that work:** only base-p3 (emerald under amethyst) and base-p1 (parchment over sapphire) really have two jewel tones.
- **Why it matters.** Rule F7 says each level has two jewels and a warm light, so a stage reads as a journey. In practice, consecutive boards read as the same violet night, which is the "plain" the owner named. Mean chroma of 0.05–0.09 is still muted, not jewel-toned.
- **Fix.** Keep L locked, as F1 requires. In each board's large non-peg regions (sky, sea, cloud, far ground), push a second hue at least 60° away from the base and raise chroma to roughly 0.10–0.14 there:
  - base-p2: a truly rose cloud sea;
  - exp-p1: a real turquoise for the water;
  - exp-p2: a wider aquamarine-to-green aurora band;
  - exp-p3: keep the planet's natural blue, white and green, and put the nebula in magenta and teal;
  - the map: teal seas against the sapphire land.

  Then add the hue-bin measurement above to `readcheck.py`, so that "two jewels" is checked rather than asserted.

**M2. The framing draws clean edges that read as walls, ramps or posts inside the opening (base-p2, base-p3, exp-p1, exp-p2; Minor on base-p1).** Each case, in 1× composite coordinates:
- **base-p2: the "tracery".** It is a solid dark mask with a clean circular arc, running from about (80,155) to (210,45) in the top left and from (590,45) to (720,195) in the top right. The right arc has a bright rim. There is also a straight-edged notch along the top (x 75–210, y 42–60) and a vertical rim line at x≈590, y 42–67. It reads as rounded top corners on the playfield, not as a cathedral window. The trefoils at about (115,88) and (685,90) read as playing-card clubs (♣), or as three dark pegs: a noise glyph by the owner's rule.
- **base-p3: the right trunk.** It is a near-black vertical band with a straight bright rim at x≈695, running the full height of the board. It reads as an inner wall about 25–30 units inside the real one. The left trunk (x≈80–100) does the same, and its branch stub at about (100–130, 205–235) reaches toward a peg.
- **exp-p1: the lamp column and balustrade.** The column at x≈597–612, y≈470–585 is a clean rectangular post with a rim, and an orange peg at about (610,525) overlaps it. The balustrade's top rail at y≈528 (x 620–720) reads as a shelf. The laurel bough draws clean arcs from (720,42) to (600,130).
- **exp-p2: the furled-sail boom.** It is a straight diagonal bar with a lit top edge, running from about (75,135) to (170,42). It looks like a slanted brick in exactly the corner where a high left shot travels.
- **Why it matters.** Bank shots are a core skill, and the tally rewards them ("Off the Wall"). A visible boundary that does not match the physics teaches wrong angles. A ball that appears to pass through a "post" or "ramp" also reads as a bug. This is rule F3 ("never draws a clean line or arc… a player could take for a wall or a brick"), and the coverage numbers in F2 do not catch it.
- **Fix.**
  - Replace geometric silhouettes with broken, organic edges, and drop the rim light wherever an edge runs roughly parallel to a wall within 60 units.
  - base-p2: either real pierced tracery, meaning a lattice with the sky showing through and no solid arc, or no tracery at all, keeping the firs. Remove the trefoils.
  - base-p3: break the trunk edges with bark texture, ivy and offsets, and remove the vertical rim.
  - exp-p1: move the column outside the bucket's lane or lower it below y 540, and make the balustrade rail irregular or partly hidden by foliage.
  - exp-p2: replace the boom bar with sailcloth folds that have soft edges and no lit straight edge.
  - Add a check to `dress2.py` that flags any rim-lit edge segment longer than about 40 units that runs within 20° of a wall or the horizontal.

**M3. The boards are not fuller where it counts (base-p1, exp-p1, exp-p2, exp-p3).**
- **What I see.** By the project's own measurements, framing covers 0%, 1.7%, 0.8% and 3.7% of the opening on these four boards. The light shafts promised in section 5 of the spec cannot be seen in the stills. These are base-p1's "lamp's warm pool and three broad beams", exp-p1's three beams and base-p3's five beams, and the motion map shows only faint streaks for them. Next to the approved boards, base-p1, exp-p1 and exp-p3 read as the same picture with a tint.
- **Why it matters.** Busier, fuller boards was one of the four levers the owner chose, and the board is what the player looks at for 95% of the time.
- **Fix.** Give each board one clearly visible depth layer and one visible light event while keeping F2 and F6:
  - base-p1: the lamp pool, about +0.06 in the lower-left quadrant, plus a compass rose and a ship silhouette in the margins;
  - exp-p1: real laurel masses in the top corners and visible shafts across the domes;
  - exp-p2: the ferry's prow and its lantern in a lower corner;
  - exp-p3: larger lunar outcrops and a visible earthlight rim on the planet.

  Raise shaft strength toward the F4 cap of 0.08 where the veil allows. At present they are below perception.

**M4. Fever ("FULL MOON") is not a spectacle (fever, 1280 and 640).**
- **What I see.**
  - Next to the approved pass, only the banner changed. The board keeps the same calm violet night, with no change in light, colour or sky.
  - There is no moon in a beat called FULL MOON.
  - The cups are not lit.
  - The laurel stems cross the letterforms "LL" and "OO" (≈470–560 and 740–800, y 250–330).
  - The ribbon has a gap where its centre should be, so the type floats between two tails.
  - The subtitle at y≈385 runs over live pegs at (560,400) and (690,400).
- **Why it matters.** This is the game's peak reward, the moment Peggle builds everything toward. A still that looks like ordinary play plus a ribbon does not read as a payoff, and it is the clearest place where "plain" survives. The spec defers rainbow and fireworks to G8, but this pass is the art pass, and the visual payoff belongs here.
- **Fix.**
  - Design the Fever hit as a lighting change within the motion rules: a full moon rises or swells in the board's sky, the sky lifts toward the companion's accent colour (the veil keeps pegs readable), and moondust bursts once from the last orange.
  - Light the centre cup and give the other cups gilt rims.
  - Rebuild the banner as one continuous ribbon with the laurel framing it, not crossing it.
  - Move the subtitle onto the ribbon or below the peg rows.
  - Add a `motion/fever.png` preview. Reduce motion can keep a static lit state.

**M5. The level-clear headline is the smallest type on the tally and disappears at 640 (tally).**
- **What I see.**
  - At 1280, "LEVEL CLEAR" is about 10 px type across the bottom of an empty grey plaque at (610–675, 85–145). The level name below it is about four times larger.
  - At 640 the plaque is empty: "LEVEL CLEAR" is gone, and the crown overlaps the HUD title strip.
  - At 640 the ace line reads "ACED tt new best", so a glyph is missing for "·".
  - "new best", a real reward, is small grey body text at both sizes.
- **Why it matters.** The tally is the only place the game says "you won". The hierarchy is upside down, and at the minimum window the win message is missing.
- **Fix.**
  - Put "LEVEL CLEAR" on the ribbon in gilt Jupiter, at least the size of the level name, and fill the plaque with something meaningful: the lit ace moon, or the companion's medallion.
  - Make "ACED" with "new best" a gilt callout with a glint.
  - Fix the TrumpGothic fallback for "·", or set that line in AXIS.

**M6. Sealed level tiles are the loudest things on the level select (levels, 1280 and 640).**
- **What I see.** The two card-back tiles at (765–990, 180–420) and (1007–1235, 180–420) are the brightest, most saturated areas on the screen. At 640 they fill the lower half. The playable tile, 4-3, does not stand out in the still, and its glow is too faint to see.
- **Why it matters.** The screen exists to get the player into the next playable level. Here the eye goes first to what it cannot play.
- **Fix.**
  - Dim the sealed tiles: multiply them toward lapis to about 40–50% value and lower their chroma, so the card back still reads as "sealed" but recedes.
  - Give the open tile a visible accent-coloured glow at rest, not only while breathing.
  - At 640, put the Play button under the selected tile or in a panel. It currently overlaps the 4-5 sealed tile's plate, which suggests that Play refers to the locked level.

**M7. The play preview misrepresents peg colours (motion/play.png).**
- **What I see.** The shared palette with no dither drops the peg hues. The green peg at about (308,183) becomes grey-teal and the purple at (498,500) becomes grey-blue. Oranges desaturate toward tan (compare `play-frame0.png` with APNG frame 0, RMSE 0.030). Palette shimmer also shows in the forest band: frame-to-frame differences of up to 20 levels, in blocks.
- **Why it matters.** The owner judges motion from this file, and in it the pegs look washed out, with green indistinguishable from blue. That undersells the boards and hides the very readability the pass promises.
- **Fix.** Render the APNG in true colour (24-bit or per-frame palettes), or reserve palette entries for the five peg colours and their lit and unlit ramps. If size forces a palette, render an MP4 or WebM for review.

**M8. No asset shows a power firing or a style shot.**
- **What I see.** Powers and style shots are listed in the brief as reward beats. The real characters appear in play only as a 40 px medallion. Nothing shows what happens when a green peg is hit: no cameo, no callout, no accent burst. Style shots ("Long Shot", "Off the Wall") have no callout design in the new chrome either.
- **Why it matters.** The cast is this pass's biggest win, and the power moment is where a companion earns their place. Without it, the characters are decoration on menus. Peggle's Masters are memorable because they appear when their power fires.
- **Fix.** Add a "power" screen or composite:
  - On the green hit, the companion's card art slides in at the rail edge for about 1.2 s inside the journal frame, glowing in their accent colour.
  - The power name is set in their accent and Jupiter.
  - The effect is drawn in that accent: Super Guide's line, Lunar Burst's ring, the Moonbloom petals.
  - Under Reduce motion this becomes a fade.

  Add one style-shot callout (gilt TrumpGothic on a small ribbon, away from pegs).

### Minor

**m1. Accent colours cluster, and some vanish against their ground (lineup, characters, HUD, fever).**
- **Hue clusters.** In OKLab hue:
  - Louisoix (#FFE08A) and the moogle (#FFD24A) are both at 90°, and Cid (#FFB45E) is at 68°. All three sit on the chrome's own gilt hue (about 78°), so their power names read as ordinary gilt labels.
  - Urianger (287°) and Y'shtola (300°) are 13° apart.
  - Merlwyb (182°) and Minfilia (200°) are 18° apart.
  - Tataru, Alisaie and Raubahn sit at 1°, 13° and 33°.
- **Contrast.** "Multiball" and "Moon Gate" on the lineup's violet ground, and "FIREBALL" on exp-p2's violet rail (fever-1280 ≈ (1095–1155, 352–368)), read as grey.
- **Fireball in violet.** A fire power shown in amethyst also works against the player's intuition.
- **Why it matters.** The spec promises "each power has a colour you learn", and a third of the cast cannot deliver that.
- **Fix.**
  - Spread the accents to at least 25° apart and keep them off 60–100°: for example Louisoix pale silver-blue or ivory with a gilt edge, the moogle pom-pom red-pink, Cid copper.
  - Put power names on a small dark plate, or add a light outline, wherever the rail enamel shares their hue.
  - Consider giving Fireball a flame-coloured effect while Y'shtola's own accent stays amethyst.

**m2. Stage and character place do not match (map, levels).** Stage 4 is "The Shroud by Night", with Moonlit Post, Bentbranch and Twelveswood levels, and its companion is Raubahn, Ul'dah's Flame General. Kan-E-Senna, the Shroud's own seedseer, is stage 7, and she carries the tally on a Sharlayan board. FFXIV players will notice. Fix: name and theme each stage after its companion's home (Raubahn's stage in Thanalan, Kan-E-Senna's in the Shroud), or reorder the stage themes. The power order can stay.

**m3. Progress state is inconsistent across the mocks (characters, map, HUD).** The title and map say stage 4, yet Merlwyb (stage 5) is shown met and reached on the characters grid (50–160, 345–535), and map stop 5 at (447,470) is not dimmed. The HUD for 4-3 shows Minfilia and Super Guide, while the title, levels and map all say 4-3 is "with Raubahn, Lunar Burst". The title also offers "Duel against Louisoix", who is "meet at stage 10". Fix: render all mocks from one progress state. If the HUD shows Super Guide on purpose, label it as Quick Play.

**m4. The characters grid's selection glow is pink, not the companion's accent (characters, (40–170, 112–275)).** It also has a hard white rim, so it reads as a highlighter box. Fix: tint TripleTriadCardSelect with the selected companion's accent, which reinforces the colour the player is meant to learn, and soften the inner rim.

**m5. The characters detail does not show the power working (characters).** The power is described in text only. Fix: a small still or looping thumbnail of the effect on a mini board in the companion's accent. Learning eleven powers from text alone is hard.

**m6. The moogle's medallion reads as a white cat (exp-p3 rail ≈ (740–790, 210–260); map stop 11).** The face box crops off the pom-pom and the wings, which is unfortunate given the Peggle cat check. Fix: a per-character `box` for card 87020 (r2cast already supports one) that keeps the pom-pom in the ring.

**m7. The HUD's outer margins are flat, with floating ornaments (hud, base composites).** The spec says the margins carry the level's scene, blurred, but x<108 and x>1172 are flat navy. The lone gilt scrolls at (35–70, 320–475) and (1210–1245, 320–475) attach to nothing: ornament without structure, against the owner's rule. Fix: draw the blurred, darkened scene in the margins as specified, and either attach the scrolls to the frame as brackets or remove them.

**m8. base-p1's neat-line reads as an inner wall (base-p1, HUD).** The ruled line with its degree-scale bars sits about 6–13 units inside the opening along the top (y≈46–50) and the left (x≈80–90), so the visible boundary is not the physical one. It is the same issue as M2, but milder. Fix: put the degree scale on the frame's inner bevel, outside the opening, or let it fade out within 30 units of the corners and drop the inner second rule.

**m9. exp-p2's rigging runs through pegs and competes with the constellation (exp-p2).** The dotted ropes pass through pegs (for example (162,129)) and cross the constellation lines that are the level's whole idea. Fix: route the ropes only through empty sky, or make them clearly rope (thicker, soft, darker) so they cannot be read as constellation links.

**m10. base-p3's willow fronds read as palm fronds (base-p3, top corners).** Twelveswood is oak and elm, and the stiff paired leaflets read as tropical. Fix: longer, drooping, irregular strands, or oak leaf masses.

**m11. The title preview covers only part of the title, and dust crosses the logotype (motion/title.png).**
- The 640 × 400 preview is the top-left crop: the logo and two buttons, with Challenges cut off. The two mist layers and their parallax, which run across the lower ground, are never in frame.
- Moondust tracks cross the logotype's letter area, for example inside the first "O" at about (140,197) in preview coordinates, and pass just above the subtitle at about (260,240). Section 4 rule 3 of the spec says moving layers are masked out of all type and UI.

Fix: preview the whole title at 640 × 400, and widen the type mask to the logotype's and subtitle's bounding boxes plus a few pixels.

**m12. The play preview's beams cannot be seen (motion/play.png).** The motion map shows fireflies clearly, but the beams' breathing and the dust inside them are near the noise floor. In practice the board's only visible life is about 12 two-pixel fireflies, which will vanish at the 640 window. Fix: let the beams breathe at the top of their allowed range (±15%) at a visible base strength, and make the fireflies 3 px with a soft halo. This stays within the motion spec.

**m13. The pause screen's footnote collides with the frame's lower corner ornaments (pause, y≈667; corners (440–500, 610–685) and (765–845, 610–685)).** The quick settings are cramped against the frame, but the owner wants spacious settings. Fix: drop the footnote into Options, or make the window about 30 px taller.

**m14. The fever banner's ribbon is broken (fever).** This is covered in M4. It is listed here so it is not lost if the banner is kept: the two ribbon tails with no centre read as two pasted pieces.

### Nit

**n1. Visible seams in the chrome (hud and all play screens).** The PvP crest halves meet the central jewel with hard vertical cuts (≈ (585–700, 10–45)). The journal corner pieces step against the rail bars in the top corners (≈ (110–200, 10–60)). This is the UX/UI specialist's call. I note it because it reads as a pasted-together crest at the launcher, the focal point of play.

**n2. The level code "12-5" (exp-p3) clashes with "11 stages" and "stage 11 (The Far Shore)" for Storm Post in `characters.md`.**

**n3. The title's four mode pills have equal weight, and the Continue card is the real primary action in the corner, with a 40 px medallion.** Consider giving the Continue card the companion's accent glow and a little more scale, so the new cast also shows on the first screen.

## Unverified

- **The twins' card art.** I could not confirm from the install that card 087059 shows the twins in their A Realm Reborn outfits, not later ones; the spoiler claim depends on it. Please verify against the card's patch of origin.
- **Feel in motion.** In-game pacing, the Fever slow-motion and the tally count-up are not shown in any asset. My notes on them are from stills only.

OVERALL: REVISE

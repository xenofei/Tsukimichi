# Round 2 findings: moon glyphs and plugin icon

This summarises what the panel found. It is not a sign-off; you decide.

## Headline

Menphina's Medallion (D) led on both glyphs and icon, and all six jurors picked its icon. Nothing reached the 8.0 ship bar, so nothing is ready to ship yet: D is the base for a short round 3 that fixes a known list of row-size problems.

## Scores

Weighted means out of 10. The bar is 8.0 for glyphs and icon, 8+ from the owner proxy, and no criterion under 6.

| Concept | Glyphs | Icon | Owner proxy (glyphs / icon) | Favourite glyphs | Favourite icon |
|---|---|---|---|---|---|
| A Aether Crystal | 6.70 | 6.88 | 6.4 / 6.25 | 2 (veteran raider, colour-blind) | 0 |
| B Astrologian Orrery | 5.76 | 6.12 | 5.4 / 5.75 | 0 | 0 |
| C Ishgard Glass | 5.87 | 6.29 | 5.75 / 6.25 | 0 | 0 |
| D Menphina's Medallion | **6.91** | **7.54** | **6.9 / 7.75** | **4** | **6** |

- **How far D is from the bar:** about 0.5 short on the icon and 1.1 short on the glyphs. Its distinctness (5.8) is under the floor.
- **The stock-icon gate:** at most one glyph per set may read as a stock UI icon. Every set fails it at 16 px.
- **At 48 px:** D is the only set most jurors read as eight moons.

## The four concepts

### A. Aether Crystal

Moonstone moons in a job-icon rim; only Ready glows.

**Liked**
- The best row legibility and distinctness.
- Ready's road hangs below the disc, so you can find it by shape alone, even colour-blind.
- Calm and cool, with no cheese.

**Failed**
- Locked out reads as a no-entry sign (all six jurors).
- Completed reads as a coin and Done as a check-circle.
- Not checked reads as a hamburger menu, and Ready at 16 px as a lollipop or magnifier.
- The teal glow looks like a mobile-game "claim" button.
- In the icon, the road looks like stepping stones and the aetheryte is a speck.

### B. Astrologian Orrery

Each moon sits in an astrolabe with 24 rim ticks, and the icon is round.

**Liked**
- Blocked's engraved constellation.
- Dalamud as a small rose moon in the icon.
- The round icon matching Ready.

**Failed**
- The ticks read as sun rays, which is decoration with no meaning.
- Ready is a lightbulb or user icon at 16 px.
- Not checked is Saturn, Completed a gold coin and Locked out a stray slash.
- In the icon, the road is a brick stack as bright as the moon, and the ring looks like a clock.

### C. Ishgard Glass

A cathedral stained-glass roundel.

**Liked**
- The most specific FFXIV idea.
- Its Ishgard skyline is the best FFXIV detail in the round.

**Failed**
- The lead lines vanish below 48 px, so the in-game glyphs are plain discs.
- Locked out reads as a no-entry sign and Ready as a lollipop.
- Not checked almost disappears on daylight, and Ready on another job reads as an AI sparkle.
- The icon moon is a gilt-framed plate (lemon slice, clock) whose reflection can't match it, which is your round-1 complaint again.

### D. Menphina's Medallion

Each state is a minted job-icon medal, and the metal tone says how urgent it is.

**Liked**
- The most FFXIV-feeling family, with the finest detail at large sizes.
- Its cracked red Dalamud is the only Locked out that isn't a no-entry sign.
- Ready, a moon over the sea in a gilt medal, is the panel's best Ready.
- The icon's reflection sits directly under the lit crescent, in the same colour, dimmer and broken up, which answers your round-1 note.
- The lantern and aetheryte island give it a sense of place.

**Failed**
- The same rim on all eight makes the row look like radio buttons at 16 px. Blocked and Completed read as a hollow and a filled radio button.
- Ready on another job reads as a contrast toggle, Done as a check-circle, and Not checked as a hamburger in a ring.
- In greyscale, Ready's only cue is its gold rim.
- In the icon, the glints are nearly as bright as the moon and look like leaves.
- In the icon, the road touches the Installed checkmark, and the moon is the plainest element.
- It uses more new colours than the brief allows.

## Shared problems and fixes

| Problem | Fix |
|---|---|
| Done and Completed read backwards: the new player swapped them in every set. A check means "finished"; a blank full moon means nothing. | Put the check on Completed. Give Done this cycle a "comes back" mark instead, such as a short gilt arc along the waning half's dark edge. Test that it doesn't read as a refresh icon. |
| Completed reads as a coin or radio button. | A brighter face with a pale edge band, plus a gilt check that crosses the lower-right edge (so it isn't a check-circle). No ring at row size. |
| Not checked reads as a hamburger menu (two parallel bars). | One curved, tapered mist wisp at a slight angle, with no ring. Make it bright enough to survive daylight scenes. |
| Locked out reads as a no-entry sign (A, C; B's slash). | Use D's cracked Dalamud. Make the crack thicker and darker so it works without colour. |
| A half moon in a ring reads as a contrast toggle. | Make Ready on another job a crescent (as in A) with a larger crystal. |

## Recommendation

**Icon: take D forward.** Round 3 changes:
1. Lower the brightest glints to about 85% of the crescent's brightness.
2. Flatten and vary the glints into horizontal ripple streaks, so they read as light on water, not leaves.
3. Shift the moon and road slightly left, clear of the Installed checkmark.
4. Give the crescent one quiet detail (a faint earthshine disc or one facet line), so the moon is as crafted as the frame and lantern.

**Glyphs: D's medal material with A's row-size legibility.** Round 3 changes:
1. **Rims:** a thin, darker pewter hairline on the seven resting states, so the column reads as moons, not buttons. Only Ready keeps the full gilt bezel.
2. **Ready:** open the bezel at the bottom and let two road glints cross it. The circle breaks without relying on colour, which is A's strength, but without the lollipop stalk. No glow.
3. **Done and Completed:** the swap and Completed fixes above. Drop Completed's moon-daisy if it costs clarity.
4. **Ready on another job:** a crescent with a larger crystal. This also separates it from Blocked, D's weakest pair (12.4).
5. **Not checked:** a single wisp with no ghost ring.
6. **Locked out:** keep the crack and thicken it.
7. **Colours:** fold the iron and oxidised-pewter colours into pewter to stay within the colour budget.

Then run the same blind test and metrics again. The icon fixes are small and should close its 0.5 gap. The glyphs need about +1.1, so they are the real risk.

## What we need from you

Nothing is final. Look at `_pack/hero_compare.png`, `_pack/icons_compare.png` and `_pack/rows_mock.png`, then tell us one of these:
- **Go:** take D's icon and the D-plus-A glyphs into round 3 as above.
- **Redirect:** a different concept, or parts from others, such as C's Ishgard skyline or B's small Dalamud moon in the icon.
- **Veto:** anything you dislike that the panel missed.

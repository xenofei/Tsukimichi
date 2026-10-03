# Round 5 design critique: Menphina's Medallion

**Reviewer:** design critic (meaning, consistency and taste). Light and physics belong to the realism supervisor.
**Looked at:** `medallion-r5/_sheet.png`, `_variants.png`, round 4's `_sheet.png`, and my own renders:
- Blocked r4, a, b and c, plus Not checked, Locked out and the row tier, at 16, 20, 32, 64 and 128 px on night and daylight grounds;
- every badge at 10× with a crosshair on the seat centre;
- the icon at 512, 128, 64 and 32, next to round 4, with a mock Installed check.

I also confirmed from the game data that the Journal main command (`MainCommand` row 4) uses icon `000005`, the gold "!" on red.

**Verdict:** approve after changes 1–4. Round 5 does what the owner asked. The two things most likely to get a "no" are the plugin icon getting smaller and murkier at small sizes, and the new lock and book glyphs being drawn in a different idiom from the game's job glyphs.

---

## Blocked: ranking and recommendation

**Ranking: b > a > c. Ship b (`blocked.svg` as it stands), with the tweak below.**

| | 16–20 px | 32 px | 64–128 px | Meaning |
|---|---|---|---|---|
| **b**, new moon behind cloud | A dark cloudy blob with one bright notch at the upper right. It is the only one that keeps a point of light at 16. | A sliver peeks out past the clouds: obviously "a moon, held back". | The ashen disc is barely there, the sliver is thin, and the clouds own the medal. It is the most obscure and the darkest of the three, which is exactly the owner's "more obscurity, moon a bit darker". | **Best.** A new moon is literally "not yet": the moon that hasn't come. Inside this family, where the crescent means Ready, it says "your moon is coming, but it's covered for now". That is Blocked. |
| **a**, heavy bank | A grey blob, very close to round 4's. | A dimmer full disc behind the clouds. | It reads as round 4 one step darker. The owner may not see that anything changed. | Fine, but it fails the brief's intent through timidity, and the row tier is 11.8. |
| **c**, veiled | Grey mush; the veil fills the medal with mid-tone. | A soft pale disc and a smudge. | The moodiest at 128, but the veil lobes read as a dirty smear or fog on the enamel rather than as cloud, and the disc shows *more* area than in b. | It reads "hazy", not "locked", and it is the weakest at 16 px (11.2). |

**The tweak for b** (optional, keep the 16 px weakest pair at 12 or more):
- **Dim the clouds' lit tops by about 10%.** The cloud highlights are now the brightest large mass on the medal, so at 64–128 the medal reads as "cloudy" more than "dark". With a new moon there is little moonlight to light them, so the whole medal should feel hushed.
- **Let one thin wisp of the upper bank cross the sliver about a third of the way down.** This breaks the sliver into two pieces, so it stops reading as a clean Ready-style crescent and starts reading as "glimpsed through cloud".

Measure after each step. If Blk-Lock (12.5 at the row tier) drops under 12, keep the wisp and drop the dimming.

---

## Recommended changes

1. **Plugin icon: restore the moon's size and punch at 64 and 32 px.** This is the highest owner risk.
   - **The problem.** Side by side with round 4, which the owner said "looks great", round 5's moon is visibly smaller. At 32 px the road reads as a faint grey column, where round 4's was a bright line.
   - **The tangent.** At 64 px the crescent's lower horn touches the skyline, so the moon looks perched on the shore. Clear that tangent.
   - **Lift the moon without moving the road peak.** Raise the horizon about 10 units and the moon about 20. The mirror row (2H − moon y) stays at y 305–310, so the corner rule still holds, and the gap between the horn and the horizon grows from 8 units to about 18.
   - **Make the moon bigger.** Grow it from r 56 to about r 62–64, and thicken the crescent a touch (k −0.26 toward −0.22), so it is a clear 4–5 px shape at 32 px, as in round 4.
   - **Keep the road contrast.** At 32 px the road must still read as a bright line, not a grey haze. The top three or four rows can carry the brightness, because they sit above the corner.

2. **Redraw the lock and book glyphs in the job-glyph idiom.**
   - **The mismatch.** At 10× the game's job glyphs are embossed gilt with a soft bevel and no keyline. The locks and the book are flat fills inside a heavy near-black outline, which is a sticker or emoji look, and they look like a second icon set dropped into the frame.
   - **The fix.**
     - Drop the black keyline.
     - Model the shapes as raised metal: a lit upper-left bevel, a shaded lower-right, and a soft dark under-glow, like the job glyphs.
     - Scale each glyph to the job-glyph envelope, about 60% of the seat's height (the locks are about 75% now).
   - **The bonus.** This also answers the brief's "neither may become the loudest thing on its medal". The outlined gold open lock is currently the second-loudest mark on Ready.

3. **Make the two locks one pair, with one body position.**
   - **The problem.** Each lock is centred on its own bounding box, so the open lock's body sits about 2 units higher than the closed lock's. Ready and Blocked rows then show the padlock body jumping.
   - **The fix.** Centre the closed lock, give the open lock the same body position, and let the raised shackle use the headroom. This is the Material and SF Symbols convention.
   - **Widen the lift.** Raise the open shackle's lift until the gap between the free leg and the body is at least 1 px at the 32 px medal size. At 32 px the open/closed difference currently rests on colour alone (gold against pewter).

4. **Keep the book; do not switch to the game's "!" journal icon.**
   - **Why not the "!".**
     - In FFXIV a gold "!" means "a quest is available here: go pick it up". That is Ready's meaning, not In journal's (already accepted).
     - It sits on a red tile, and red is reserved for Locked out, the one alarm state.
     - In any UI, a "!" reads as a warning.
   - **Why the book.** It is unambiguous, and its little lavender ribbon ties it to the medal's Tide-silk bookmark.
   - **Optional FFXIV flavour.** A closed, leather-bound tome with gilt corner fittings would feel more like an adventurer's journal than an open paperback. Apply change 2's metal treatment either way.

5. **Optional: a seat colour for the state badges that doesn't borrow a role colour.**
   - **The overlap.** The Ready seat (`#4A72C4`), In journal's Tide seat (`#5674B8`) and the tank seat (`#5878C2`) are effectively the same blue. On job badges, the seat colour means role. On state badges it currently means nothing.
   - **Why it's minor.** The medals themselves differ, so this is system hygiene, not a misread.
   - **The cleanest rule.** State badges share one neutral night-enamel seat, and the content's metal carries the meaning: warm gilt for open and journal, cool pewter for closed. Adopt it only if Ready stays at 1.3× or more with a dark seat on its lapis. Otherwise leave it.

6. **Icon: warm a few of the lantern points toward Kugane red-orange.**
   - **The gap.** The skyline silhouettes are well studied, but at 64 px they are two dark spikes that could be any Far Eastern harbour, or ships.
   - **The fix.** Tint three or four of the 13 waterfront lights toward chōchin red-orange (keep them under the brief's chroma ceiling). Kugane's night identity is its red-lantern glow, so this is the cheapest FFXIV-specific cue that survives at 64 px.
   - **Leave out:** the airship, the torii and the aetheryte, for the reasons the designer gives.

7. **Job icons: no action needed.**
   - Measured at 10×, Paladin and Bard sit within about 0.1–0.8 units of the seat centre.
   - White Mage keeps its stem on the vertical axis, so its head bulges about 1.5 units left. For a staff that is the right optical choice: centring the mass would push the stem off-axis and look wrong.
   - If anything, nudge White Mage +0.3 x. This is fixed.

---

## What would make the owner say "no"

- **The icon's moon shrinking** at the sizes he actually sees in the installer (change 1).
- **The badges looking clip-art next to the game's own glyphs** (change 2). The owner prefers tasteful work and dislikes noise glyphs, and heavy-outlined icons are the likeliest thing to trip that.
- **Blocked a** would read as "you didn't change it". **Blocked c** would read as "smudged".
- **Watch item: In journal is getting busy.** It now has a ribbon breaking the silhouette at the top left, a badge breaking it at the bottom right, and two "journal" signs (the bookmark and the book). This is acceptable because the owner asked for the book. If he calls it busy, shorten the ribbon's tail rather than removing either sign.

## What works

- **The badge frame is one system:** one position, one gilt two-slope ring and one drop shadow on four states. The seats sit dead centre (verified with a fitted circle at 10×).
- **The open and closed locks read instantly at 48 px and up.** Warm gold against cool pewter is exactly the right temperature contrast for "go" against "not yet".
- **Ready keeps its lead.** The badge costs it a little (1.33×), but the row tier, where Ready is actually seen in a list, draws no badge and stays at 1.39×.
- **The row tier** (badge content drawn at text height beside the medal) is a clean, FFXIV-native solution. It is how the party list shows jobs.
- **Job centring is fixed:** measured from visible pixels, not the canvas, which is the right method and matches what the runtime will do.
- **Blocked b's semantics:** the new-moon sliver is the most meaningful "not yet" in the set.
- **The icon's Kugane shore** is well researched and restrained, and at 128 and above it is a lovely, specific place. The lit castle window, the lantern line with true reflections and the open gap in the horizon behind the road are all good choices.
- **Done this cycle and the game.** FFXIV's own repeatable-quest marker (`071222`) is two circling arrows in gilt, which independently validates the repeat arrow.
- **The gold-only check** is the right call: one metal across the set.

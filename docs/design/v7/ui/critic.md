# Plan v7 UI: design critic review

Reviewer: the design critic (meaning, beauty, owner taste, FFXIV feel, usability). Light and physics belong to the realism supervisor.

Inputs:
- `docs/research/plan-v7/owner-points.md` (points 1, 2, 3, 5, 6 and 8) and the two owner screenshots;
- `spec.md`;
- `before-after.png`, `filter-drawer.png`, `rail-states.png` and `stars.png`;
- `completed-moon/compare.png`;
- `mock.html` at `#full` and `#quiet`, rendered in headless Chrome.

## Verdict

Every item answers the owner's actual complaint, and the drawer is the best piece of work in the set. Two things go too far:
- **The selected rail station carries six indicators.**
- **The Completed moon gained three craters,** which the moon rules ban outright.

Two smaller things to trim:
- **The sky gains a Milky Way band** that will look like a smudge once it is clipped into fragments.
- **The drawer spends gilt,** our "act now" colour, on settings chrome.

## Item by item

### 1. Section headings (point 1): fixed

- The diagnosis is right: TrumpGothic at 1.45x is condensed and thin, so it reads smaller than its pixel size.
- 1.80x with +0.08 em tracking and a lighter gilt makes REQUIREMENTS, REWARDS and the rest read at a glance in `before-after.png` and in the Full mock. The card hierarchy is now clear.
- Quiet at 1.15x and Plain moving off `TextDisabled` both fix real legibility bugs.
- **Not too much at 100 %.** At Text size 150 % the role reaches 43 px, which crowds a narrow detail pane (the owner's pane is about 450 px wide).

### 2. Filter drawer (point 2): fixed, with one excess

Each complaint is answered directly:
- **Empty grey:** the sheet is sized to its content, and the collapsed Advanced section becomes seven summary lines that say something useful.
- **Unreadable title:** there is a real header, and the tree is no longer drawn underneath it.
- **Text bleeding at the sides:** the drawer is exactly the tree column, with no 300 px minimum.
- **Aesthetic love:** the moon toggles, the stepper, the state chips with their medals, and Reset with Undo.

The position, the fade, pinning and Esc are all kept, as the owner asked.

**Too much: gilt.** The drawer has a gold count pill, a gold "2 set" pill, a gilt ADVANCED heading, gold toggle crescents, gold dots on the summary lines, a gold Level track and a brass edge. Gold means "act now" in this plugin, and a filter sheet asks the player to do nothing. Quiet already shows the restrained version (neutral pills, silver toggles), and it looks more expensive for it.

### 3. Stars (point 3): better, and the right kind of better

- Three depths, four temperatures and a slow 7–13 s breathing twinkle give real depth without parallax or busyness.
- The rule "never under text, 4 px off every item" keeps them clear of the UI.

What is too much:
- **The Milky Way band.** It is a mottled texture, and it is clipped to disjoint sky rectangles (the tree's empty sky, then the rail's gap). In-game it will show up as two or three unrelated grey smudges, not one band. That is noise, and the owner's love of the stars doesn't extend to haze.
- **The near-star 7 px cross.** It is a four-point spike. 8 % of stars carrying it is fine, but it must stay faint, because it is the "sparkle" look the moon rules ban.
- **The Tower constellation (Shadowbringers)** is a plus sign or a Christian cross at 70–90 px, which is a stock read.

### 4. Completed moon (point 5): half right

- **The maria are exactly what the owner asked for.** He calls the dark seas "craters". The basalt colour, ×1.6 opacity and darker hearts are a clear but gentle step. The cool glow inside the well is lovely and correctly argued: it is cool, it stays inside the rim, and Ready keeps the only warm halo.
- **The three new rim-lit craters are a mistake.**
  - They are "craters or holes in a lit area", which the moon rules ban (theme-system §2), because that is the path to cheese.
  - At 64 px in `compare.png` they read as specks of dirt.
  - The owner asked for darker, not more.
- The bright Tycho with no rays is a borderline case. It reads as a fleck at 32–64 px.

### 5. Column headers (point 6): fixed, with one hierarchy slip

- 1.62x in TextSecondary reads well, and the sorted column keeps its gilt.
- **Hierarchy slip:** the column headers (1.62x, +0.12 em) are now larger and wider than the table's group headers (MAIN SCENARIO and the rest, still Eyebrow 1.45x). In the Full mock, NAME / LV / JOB / STATUS now outweigh the groups they sit above.
  - Column labels are metadata, and group headers are structure.
  - Either take the group headers to 1.62x as well, or hold the column headers at 1.55x with +0.08 em.

### 6. The rail (point 8): fixed, but the selected state is overdressed

All four asks are met:
- the thread and the lit bar are gone;
- the icons go from 22 to 30 px;
- the stations share the rail's height, so the bar fills;
- hover lifts the icon 2 px and fades in a plate, and the bead travels between stations.

The labels never move, which matches the owner's static-layout rule.

**Too much:** a selected station has six marks at once:
- a plate;
- a 1 px OrnamentHigh border;
- a MoonHigh top hairline;
- a warm radial glow at r 24;
- gold icon ink;
- the bead.

The warm glow is also the third warm halo in the window, after Ready and the primary pill. Three marks (plate, gold ink and the travelling bead) say "you are here" calmly. The rest is noise.

## Changes

1. **`mock-src/v7.css` and spec §2.2 Full, the drawer's pills:** make the count pill ("3 on") and the "2 set" pill neutral, as in Quiet (`#262D42`). Keep the toggles' gold crescent as the only gold in the body.
2. **Spec §2.2 Full, the Advanced section head and the summary-line dots:** draw the heading in the Section role's ink, but put the rule in OrnamentLight, not brass. Draw set values in Text with a MoonHigh dot. Make the Level range track Dusk, not gold.
3. **Spec §2.2 Full header, the 28 px icon disc:** cut the disc and its brass hairline, and keep a bare MoonHigh glyph as at Quiet. It repeats the Filters button and adds a circle for no reason.
4. **Spec §3.4, the Milky Way band (`OrnamentAtlas.MilkyWay`, `StarField.Band`):** cut it from v7. If the owner wants it later, draw it only inside the single largest sky rect and only when that rect is at least 200 px tall, so it never shows as fragments.
5. **Spec §3.1, the near-star cross:** go from 7 px to 5 px, and from .6 to .4 of the star's alpha. Keep the cross length fixed during the twinkle, so the spike never pulses.
6. **Spec §3.5, The Tower:** redraw it as a tall, narrow spire (two parallel stems converging to a crown star), not a plus sign. The Crystal Tower is a spire.
7. **`completed-moon/make_completed.py`, the craters:** remove the three rim-lit craters, and remove Tycho or halve its halo. Keep the maria colour, the ×1.6 opacity, the hearts and the cool well glow.
8. **Spec §5, `TypeScale.HeaderFactor`:** change it to 1.55x with +0.08 em, or raise the table group headers to 1.62x to match. The column headers must never be larger than the group headers.
9. **Spec §6 Full, the selected plate:** cut the radial glow (`GlowRadiusLogical`), the top hairline and the OrnamentHigh border. Keep the plate at Moon .07, the gold icon ink and the bead.
10. **Spec §1 Typography, the Section role:** above Text size 110 %, taper the factor linearly to 1.60x at 150 %, so the headings never take over a narrow pane.

## The owner's three questions

1. **Heading size: keep 1.80x at the default Text size,** tapering per change 10.
   - The owner asked for bigger, and TrumpGothic's condensed caps read about a third smaller than their pixel size.
   - 1.65x would be a step the owner might not notice.
2. **Constellations: keep our own, drawn in the game's idiom.**
   - The game has no per-expansion constellations, so real ones would lose the meaning (this is that expansion's sky).
   - The Astrologian's arcana skies (Balance, Arrow, Spear, Bole, Ewer, Spire) would map onto expansions arbitrarily.
   - Do borrow the Astrologian card style: round stars at the joints, hairline links, and a brighter lead star. The spec already mostly does this.
   - Fix The Tower (change 6).
3. **Meteor: on by default at Full.**
   - It is rare (once per completion, at most once in 30 s), short (0.7 s) and soft (peak .6).
   - It turns the owner's favourite feature into a reward, and it is off under Reduce motion, Quiet and Plain.
   - Give it its own toggle under Motion, "Shooting star on completion", so it can be turned off without losing the twinkle.

## What I would cut

- the Milky Way band (change 4);
- the Completed moon's craters (change 7);
- the drawer's header icon disc (change 3);
- the selected station's glow, top hairline and border (change 9).

Everything else should ship.

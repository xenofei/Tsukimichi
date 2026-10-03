# Supervisor review: plan v7 UI (owner points 1, 2, 3, 5, 6 and 8)

**Reviewer:** art supervisor (realism, light and material consistency with the approved Medallion and Decoration v13, colour theory and value hierarchy)

**Read:**
- `docs/research/plan-v7/owner-points.md` and the owner's two screenshots;
- `spec.md`;
- `before-after.png`, `filter-drawer.png`, `rail-states.png` and `stars.png`;
- `completed-moon/compare.png`, with `completed-v7.svg` rendered at 800 px (`scratchpad/sup-v7/cm800.png`).

## Verdict: CHANGES REQUIRED (three items)

The design answers each owner point squarely:
- **The drawer:** fits the tree column exactly, sizes to its content, and the tree is hidden beneath it.
- **The rail:** stations fill the height, with no thread.
- **Headings:** a real Section role.
- **Column headers:** bigger.
- **The sky:** richer.

Its light model is the v13 one. The blocking items are:
- a crater detail that breaks the project's own cheese ban at mid sizes;
- one shadow that departs from v13's light direction;
- a type-scale change that inverts the detail pane's hierarchy.

## Required fixes

### 1. Completed moon: the craters read as holes at 48–64 px

**Element:** `completed-moon/completed-v7.svg`: the three rim-lit craters (r 1.4–2.4).

**At 800 px they are right:**
- each floor is in shadow on its upper-left inner wall and lit on its lower-right inner rim, which is correct for a depression under the upper-left key light;
- the young bright crater has no rays.

**What is wrong:**
- In `compare.png` at 32–64 px, each crater is two or three pixels of dark floor with no legible rim. They read as dark specks: holes in a lit disc.
- That is exactly the failure the grammar bans in every set (theme-system §2: "craters or holes in a lit area… cheese"), and the owner's standing rule ("moons never cheese").
- The owner's note asks for *darker* detail ("craters just a bit more darkening"). The darker maria and mare hearts deliver that on their own.

**Change:**
- Draw the three craters only in the **96 and 128 atlas tiers** (and their 2x). At 96 px their rims span 2 px or more, so they read as depressions.
- Omit them in the 48 and 64 tiers.
- The young bright crater may stay at every tier, because a bright spot cannot read as a hole.
- `gen_atlas.py` needs a per-tier source for Completed: `completed-v7.svg` for 96 and 128, and a crater-less variant for 48 and 64. Keep the face otherwise identical.
- Leave Completed's 0.72× Ready salience unchanged; the crater-less variant should still measure 0.72.

### 2. The drawer's sideways shadow brings a second light direction

**Element:** spec §2.2, Full sheet: "Shadow 0 14 30 at .55, and **8 0 18 at .30** toward the list".

**What is wrong:**
- In v13 every raised surface at Full casts its shadow straight down (cards 0 3 10, tooltips 0 10 26): the moon is far off and high.
- A horizontal 8 px offset makes the drawer the one object in the window lit from the left.
- Next to the cards in the detail pane, the shadows disagree.

**Change:**
- Keep the drop shadow 0 14 30 at .55.
- Replace the sideways shadow with **contact shading without offset**: 0 0 14 at .30, clipped to the list side.
- That gives the separation from the list as ambient occlusion along the edge, not as a second light.

### 3. Section headings now out-rank the quest title

**Element:** spec §1, Full: "Section… 1.80x body", against the detail pane's title (the v13 hero title, Jupiter 20–21 px).

**What is wrong:**
- At 1.80× (28.8 px, caps of about 15.5 px, in GiltLight `#E6CF98` with a shadow), the Requirements, Rewards and Unlocks headings are as tall as, or taller than, the quest name's caps. They are also the brightest, warmest text in the pane.
- Each is now as loud as the title of the thing it describes. The value hierarchy should run title > state > section > body.
- The owner asked for headings that read, not headings that lead.

**Change (either option):**
- **(a)** Take the spec's own gentler option, **1.65×**, for the Section role (caps about 14 px). Keep the GiltLight ink and the shadow, which carry the legibility gain.
- **(b)** Keep 1.80× and raise the hero title so its cap height stays at least 1.2× the Section caps: Jupiter 23 pt drawn at 1.0, about 25 px at a 16 px body.
- In either case, the quest title stays the largest text in the pane.

## Checks that pass

### Headings and column headers
- The 1 px Abyss shadow under the gilt heading falls straight down, as v13's shadows do.
- `TrackedTextAt` is the honest way to get tracking in ImGui.
- Quiet grows by size, never by fake bold, and Plain becomes a band.
- **Column headers:** 1.62× in TextSecondary, with the sorted column in gilt. A sound step: still below the scope title and group headers in value.

### The filter drawer
- **Value and material:**
  - The raised gradient runs lighter at the top and darker at the foot.
  - The inner top highlight is on the top edge only.
  - The brass is only on the right and bottom (the two sides that meet content), with the darker corner mark at the bottom right, because those are the brass's shaded sides under the upper-left light.
  - The summary lines replace the empty grey.
- **The opaque sheet.** Hiding the tree under it removes the bleed the owner disliked.
- **Gold on states only.** The Quiet toggles turning silver keeps gold for states. Good colour discipline.

### The stars
- **Depth and colour:**
  - The three depths separate by size, alpha and halo, with no parallax.
  - The colour temperatures are believable stellar colours (blue-white, white, gold, ember; no green), and the warm ones are rare and mid or near only.
  - The Milky Way at .06, mottled and clipped to empty sky, is restrained.
- **Twinkle:** the 7–13 s breath is a stylisation, since real scintillation is fast, but it is the right one for a UI that must stay calm and below photosensitivity concerns. Accepted.
- **The meteor:**
  - It enters from the upper left, travels down-right at the 28° tilt, and its head leads with the tail trailing behind and fading.
  - It is one at a time, under a second, and never under Reduce motion.
  - Believable.
- **The constellation** is hairlines at .09 with small stars: an engraving-level touch, not a noise glyph.

### The Completed moon (apart from fix 1)
- The darker basalt maria and the mare hearts sit in the real maria layout.
- They are soft and irregular, and blue-grey rather than yellow. **Not cheese.**
- **The cool glow:**
  - It is symmetric around the disc, cool, inside the well and under the rim's inner shadow.
  - Physically it is the scattered halo around a bright moon, and Ready's scene already carries the same bloom.
  - It does not break v13's "nothing completed glows" rule, which is about the *warm* act-now halo.
  - **Accepted.**
- **The ladder holds:** Completed is still 0.72× Ready, and it is in no weak pair.

### The rail
- **Plates:** flat fills (an honest ImGui limit), with the selected plate's top hairline in MoonHigh: the lit edge on top.
- **The bead** travels the left edge, and the selected station's warm glow is on a lit thing, per v13.
- **Motion:** the 2 px icon lift with the label fixed is tasteful, and the static layout keeps the owner's taste rule.

## Optional polish

1. **The meteor's brightness.** At peak .60 it is only as bright as the brightest near stars (.52–.60). A meteor that is no brighter than the stars around it reads as a moving star. Peak the head at .80, with the tail starting at .45.
2. **Constellation naming.** "The Crystal" (A Realm Reborn) is a diamond of stars. Given the owner's vetoes on aetheryte crystals, the critic may prefer a non-crystal ARR motif, such as the Bow of the Archer or a Twelve sigil. This is not a realism issue.
3. **The hover lift.** At Full, the plate could gain a 1 px darker bottom edge while hovered, so the 2 px rise has a cause. Optional; the flat UI reads fine without it.

Once fixes 1–3 are in, a spot check of the 48 and 64 px Completed tiers, the drawer's shadow and one detail-pane screenshot is enough.

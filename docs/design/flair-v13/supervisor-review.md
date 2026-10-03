# Supervisor review: Decoration v13 (Full, Quiet, Plain)

**Reviewer:** art supervisor (realism, colour theory, material consistency)

**Evidence:**
- `full.png`, `quiet.png`, `plain.png` and `side-by-side.png`.
- `mock.html` rendered at 2× (`scratchpad/sup-flair/*@2x.png`), with crops of:
  - Full's hero, cards, rows and tooltip, tree, status bar and star field;
  - Quiet's detail pane and rows;
  - Plain's rows and detail pane.
- Pixel samples of the brass frames and the banner.
- For Plain's flat medals: the shipped `MedalTokens.HighContrastDark` meshes, exported at 12 and 16 px with the round-5 exporter (`scratchpad/sup-g1/hc_flat.png`). The spec builds Plain on that flat path.

## Verdict: CHANGES REQUIRED (four items)

The three looks are now clearly distinct.
- **In the thumbnail test:**
  - Full reads as the gold-framed night scene with a banner.
  - Quiet reads as an even slate with soft blocks.
  - Plain reads as a spreadsheet.
- **Full's brass is lit correctly:**
  - top and left edges are light (sampled `#C5A96F`); bottom and right are shaded (`#9C8049`);
  - the top corner marks are bright (`#F0D9A0`); the bottom ones are darker brass (`#B79755`);
  - the inner highlight is on the top edge only, and shadows fall straight down.
- **Quiet** is calm and beautiful in its own right.
- **Plain** is a clean ledger.

What blocks approval:
- one light contradiction in Full;
- one value-hierarchy failure in Plain;
- two Plain glyphs that leave the approved medal language;
- one glow rule that the spec states two ways.

## Required fixes

### 1. Full: the quest banner is daylight inside a moonlit window

**Elements:** the hero banner in `DetailPane.Hero.cs` (spec §1, "Banners"), mocked in `mock.html` `#full` as the Dawntrail banner.

**What is wrong:**
- The art is a sunlit midday scene: saturated blue sky, white cumulus, warm sunlit stone. It sits in a pane whose whole premise is moonlight from the upper left.
- It is also the brightest large area in the window: mean L 94 over its upper half, peaking at 202. It outshines the moon medals and every Ready signal.
- The spec's own rule ("the sky is darker than the art... the zone image melts into the pane") is not met. The scrim only touches the bottom edge.
- The medal over its edge shows a night moon in front of a noon sky. That is two suns in one picture.

**Change (a night grade on every banner, at Full only):**
- Apply three passes, in this order:
  1. Multiply by `#2A3768` at 0.55. This cools and darkens toward the Moon Road's indigo.
  2. Desaturate by 35%.
  3. Run a vertical scrim from `Night` at 0 at the top, to 0.25 at 45%, to 0.85 at the bottom edge.
- Add a faint moonlight wash from the upper left: a radial gradient of `MoonHigh` at 0.06, centred at the banner's top-left.
- **Target:** the banner's mean L is at most 55, and its brightest pixel is under the hero medal's moon (L ≈ 230 → at most 170).
- Apply the grade at draw time (as a tint on `AddImage` plus scrim quads), so every zone banner in the fallback chain gets it. Do not ship pre-graded art.

### 2. Plain: Completed is the loudest glyph in the table

**Elements:** the Plain row medal (`MedalFinish.Flat`, which the spec builds as "`MedalTokens.Flat`, as high contrast already draws").

**What is wrong:**
- At 12 px, Completed is a solid gold disc. In the mock it is the brightest and most saturated mark on the page.
- The shipped high-contrast flat meshes do the same: Completed is a pale-gold full disc, while Ready is a thin dim crescent (exported in `hc_flat.png`).
- Completed is also the most common state, so a typical Plain table is a column of gold coins with the actionable Ready rows quieter than the finished ones.
- That inverts the value hierarchy every round has held: Ready reads first, Completed recedes.
- The high-contrast ladder exists for low-vision contrast. It is not a hierarchy, and it must not become Plain's.

**Change:**
- Add a dedicated `MedalTokens.Plain`: flat like high contrast, but on the standard palette and with its own ladder. Do not reuse the high-contrast tokens.
- Assign the flat inks:

| State | Plain flat medal | Brightness rank |
|---|---|---|
| Ready | Lapis disc `#5480C8` with a `MoonstoneSpecular` crescent | Brightest |
| Ready on another job | Night disc with a `MoonstoneHigh` crescent | High |
| In journal | Night disc with a `Moonstone` crescent and a Tide ribbon notch | Middle |
| Completed | `MoonstoneMid` disc (`#95A5C8`) with a 1.5 px gilt check | One step below In journal |
| Done this cycle | `MoonstoneMid` half disc with a gilt arc | Low |
| Blocked | `MoonstoneDeep` disc with a grey cloud band | Low |
| Locked out | Dalamud `#C24A58` disc (see fix 3) | Hue, not brightness |
| Not checked | `Mist` "?" (see fix 3) | Lowest |

- Rims: 1 px in the state ink at 0.6.
- **Acceptance:** in a 12 px greyscale render, Ready ≥ 1.3× the next state's salience, and Completed ≤ 0.8× Ready (the round-5 metrics, at 12 px).

### 3. Plain: two glyphs leave the approved medal language

**Elements:** the Plain glyphs for Locked out and Not checked (mock `#plain`).

**What is wrong:**
- **Locked out** is a circle with a diagonal slash: a no-entry sign. The round-2 panel rejected exactly this read, and round 4 replaced it with the shattered red moon.
- **Not checked** is a dotted empty ring. The approved emblem is a question mark built from the moon. A dotted ring reads as "loading" or "optional".
- Both break the "same medal, flatter finish" contract that Quiet keeps.

**Change:**
- **Locked out:** a flat Dalamud disc (`#C24A58`) with two thin dark fractures (`#0B0408`, 1 px at 12 px) meeting off-centre at the upper left. This is the shipped `locked-out` crack layout, simplified to its two longest cracks.
- **Not checked:** the flat "?" from the high-contrast mesh (`MedalArt` `Unknown`) in `Mist`, with its moon-dot.
- With Moon style = Classic, Plain keeps the 1.11 glyphs as specified; this fix applies to the Medallion style only.

### 4. Full: the hero halo must follow the "glow is light" rule

**Elements:** the hero medal's gold halo (spec §1, "Banners": "the hero medal rises... with a gold halo"; "Glows" list; `DetailPane.Hero.cs`).

**What is wrong:**
- The spec says, correctly, "Nothing blocked or completed glows". But the Banners row gives the hero medal a gold halo for whatever quest is selected.
- In the mock the selected quest is In journal, and it glows.
- A warm halo behind a Locked out or Completed medal would be light where the physics has none, and it would make every hero look Ready.

**Change:**
- Draw the halo only for `Ready` and `ReadyOnOtherJob`. No other state gets one.
- Update the spec's Banners and Glows rows to say so.

## Checks that pass

### Full
- **Brass frames:** sampled light top and left, shaded bottom and right, with bright top corner marks and darker bottom ones.
  - Four-stop brass reads as a metal edge, not a stroke.
  - The gilt matches the medals' champagne ramp, so cards and medals are one metal.
- **Star field:**
  - It stays in empty sky, at believable magnitudes (mostly 1 px at 0.16).
  - The rare cross-flare stars are few, and none sits under text.
  - Seeded, with no shimmer.
- **Glows:**
  - Every glow is one warm hue at low strength, on lit things: the selected station, Ready rows, the divider lozenge and the primary pill. Once fix 4 is in, the set is consistent.
  - The pill bloom sits beneath its own top highlight, which is correct for a lit gold surface.
- **Medals, cards and type:**
  - The medals are shipped as approved: gilt rim, row tier 18 px with a straight-down shadow.
  - The display type is in gilt, and the eyebrows are tracked: a strong, FFXIV-native hierarchy.

### Quiet
- **Tonal panes:**
  - The panes, cards and selection are flat, with no light, so they read as deliberate.
  - The cards lift by tone alone (`#1D2438` on `#161C2E`). That is subtle but legible at 1:1.
  - The high-contrast VeilLine border covers the low-vision case.
- **Silver-rim medals:**
  - The medal face is kept, so the moon, enamel and badges stay the approved art.
  - The rim becomes a 1 px silver hairline. In a level with no lit metal anywhere, a flat hairline is the consistent choice.
  - It is still one rim colour across all glyphs, which keeps the owner's cohesion rule within the level.
- **Gold budget:** only states, the primary action and the unique rewards carry gold. A clean value and temperature hierarchy.

### Plain
- **Layout:**
  - The zebra rows, the raised header band with column dividers, the key-value detail and the text-only status bar all read as a ledger.
  - The colour budget is disciplined.
  - The state stripe keeps its dash and dot pattern at 2 px.
- **State colour in text:**
  - Red "after…" reasons, gold "unique" and Silver names stay legible on both zebra tones.
  - Only the glyph hierarchy (fixes 2 and 3) is wrong.

### Distinctness
- Each level is beautiful on its own terms, and the three now differ in pane tone, art, card material, density and medal finish, all visible at 32%.

## Optional polish

1. **Full corner marks.** They sit 3–4 px outside the card corner with a visible gap, so they read as floating brackets rather than fittings. Overlap the frame by 1 px so each mark wraps the corner.
2. **Full star in the column header.** One cross-flare star sits on the column-header rule between STATUS and EXP. Keep the star field's skip rule off the header band's rule line too.
3. **Quiet tone steps.** The window, tree and detail tones (`#131929`, `#11172A`, `#161C2E`) differ by only about 1–2% L. On some monitors the panes will merge. If the owner wants the panes to separate without lines, widen to about 3% L per step.
4. **Full banner scrim.** Once fix 1 is in, let the scrim's bottom 12 px carry a faint moon-road reflection (`MoonHigh` at 0.05) under the hero medal. That ties the banner into the water theme.

## Re-review after the fixes: CHANGES REQUIRED (one small item)

I re-rendered `mock.html` at 2× (`#full`, `#plain` and `#glyphs`; crops in `scratchpad/sup-flair/v2_*.png`).

### Verified

| # | Fix | Result |
|---|---|---|
| 1 | Night-graded banner | The Dawntrail art now reads as a moonlit scene: a cool, desaturated sky with a dark scrim at the bottom, and it sits under the hero medal's moon. Mean L 52.5 and peak 102 are inside the targets. Choosing the multiply strength per banner from its mean L (.55–.75) is the right generalisation. Accepted. |
| 2 | Plain's value hierarchy | Ready (a lapis disc with a bright crescent) is clearly the first read. Completed (a small MoonstoneMid moon with a gilt check) recedes. 1.70× and 0.52× pass. |
| 3 | Plain's Locked out and Not checked | A red disc with two fine dark cracks meeting at the upper left, and a Mist "?" with its dot. Both are back in the approved medal language. |
| 4 | Hero halo | Ready and Ready on another job only. |
| Polish | | The corner marks wrap the frame, the stars stay in the title band, Quiet's tones are about 3% apart, and the faint moon road runs along the banner's bottom edge. All correct. |

### Required fix

**5. Plain flat medals: the crescents and the half moon are mirrored relative to the approved emblems.**

**Elements:** the `MedalTokens.Plain` flat glyphs for Ready, Ready on another job, In journal and Done this cycle (mock `#glyphs`, and the Plain rows).

**What is wrong:**
- At 12 px, the three crescents are lit on the **left**: a "C" opening to the right.
- The approved Ready, Ready on another job and In journal crescents are lit on the **right**, with the limb facing about 28–30° below horizontal toward the lower right. The moon road falls under that lit limb.
- Done's half moon is lit on the **right**. The approved Done is a waning half lit on the **left**: this cycle's light is spent, and the gilt "comes back" mark sits on the dark side.
- The phase direction carries meaning in this set (waxing means ready, waning means done). Mirroring it turns Done's waning moon into a waxing one, and every Plain moon contradicts the same state's medal in Full and Quiet.

**Change:**
- Draw the flat crescents with the shipped geometry: `MedalArt.PhaseOutline(SceneMoon, SceneMoonRadius, SceneMoonTerminator, SceneMoonTilt)`, the same call the high-contrast flat medal already uses. That makes them lit on the right, with the limb toward the lower right.
- Draw Done's half moon with `litLeft: true`. Its gilt arc then belongs on the **right** (dark) side.
- Re-check the 12 px metrics afterwards. They should not move, because the area and value are unchanged.

Everything else is approved. After fix 5, a spot check of the `#glyphs` strip is enough.

## Final verdict, after the spot check of fix 5: APPROVED

I checked the `#glyphs` strip, rendered at 2× (`scratchpad/sup-flair/v3_glyph_strip.png`), and the Plain rows in `plain.png` (`v3_plain_col.png`).

- **Crescents:** Ready, Ready on another job and In journal are now lit on the right, with the limb toward the lower right. They match the approved medals' crescent, and the moon road falls under the lit side.
- **Done:** a waning half lit on the left, with its gilt arc on the dark right side. That matches the approved emblem and its "comes back" meaning.
- **Rows:** in the Plain table the moons read the same way as in Full and Quiet.
- **Hierarchy:** Ready is 1.78× the next state and Completed is 0.52× Ready, so it holds.

All three Decoration levels pass for light, shadow, colour theory and material consistency with the approved Menphina's Medallion medals.

**APPROVED.**

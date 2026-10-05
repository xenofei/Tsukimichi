# Moonfall runtime art: supervision, round 2

All three supervisors re-reviewed after `response-round-1.md` (commit 59e00985) and **approved every asset**. Their
reports are summarised here with each finding; what was done after round 2 follows.

## Game designer: APPROVE (all 14 assets)
Both round-1 Majors resolved: no peg-shaped marks remain where pegs cleared (every hud and fever render, base-01's
smooth sky included); base-04 is now its own place (Kugane by night, teal canal, lantern cords, moonlit haze) and every
kind reads at 1280 and 640. Round-1 Minors and Nits fixed as claimed; no regressions in the chrome or the moments.
- [Minor] A cleared brick still leaves dimming (the bricks' veil was baked; about 13% residual on base-04's arcs).
- [Minor] base-04's lanterns and warmth are thin: glow dots without bodies, cord ends in mid-air, little warm light in
  the city.
- [Minor, follow-up accepted] base-01's forest silhouette layer (level-authoring pass).
- [Nit] hud-640 with the hint; [Nit] the tally's TOTAL counts up from the HUD score; [Nit] the panel's corner scrolls
  touch "Play again"; [Nit, carried] base-03 shares level 13's chart.

## UX/UI specialist: APPROVE (all 11 assets)
M1 fixed (no dark discs at 1280 or 640 on any board; base-04's bands gone). M2 fixed: the 640 tally is about 451 × 324 px
at 0.8 px a unit, fits its rows, the ACED plate inset, Cid's caption back. Text floors on the 640 tally all pass (row
labels 9–10 px caps, numbers 11, total 19, level name 16, subline 7–8, ACED 11, NEW BEST 8, ace score 8, caption 7–9,
buttons 9); secondary text contrast 6.0–12.6:1. Margins fixed (mean 0.18–0.19); the hint rendered.
- [Minor] n1: the bricks' veil is baked, so a cleared brick should leave its halo.
- [Minor] n2: hud-640 with the one-time hint: the bar grows to about 140 px until answered.
- [Minor] n3: power-640's "+25,000" has 7 px caps, under the 8 px number floor (the ribbon's value used the label floor).
- [Minor] n4: power-640's ribbon tail runs about 15 px into the name plate.
- [Nit] tally-640's LEVEL CLEAR tail over the board's name plate; fever-640's subline over a lit orange; the caption's
  "×1" shown only when used.

## Level-design critic: APPROVE (all 10 assets)
All three round-1 Majors resolved: the F6 test measures the right quantity and catches what it should (with the veil
removed, base-04 drops to 0.179 and base-02 to 0.129, failing); the loader guards hold and are tested; the authors' doc
states F6, the ceiling, "Fit the layout", the 640 step, the dropped count and the keep-outs; base-03's route is gone and
routes keep off pieces. base-04 reads well at both sizes. The per-peg veil is no weaker than the approved baked veil
(worst with neighbours cleared: base-02 0.258 against 0.286 at the deal, both above 0.20).
- [Minor] A cleared brick still leaves a dark ghost (base-04's arcs merge into a ring; a band on base-02; rectangles on
  base-03).
- [Minor] The F6 test measures only at the deal (overlapping sprites multiply); the lowest margin is each peg with only
  its own veil.
- [Minor] The test's backdrop leaves out the lantern spill, the chrome and the moving beam share that readcheck counts.
- [Minor, carried] base-02's arc over the city (level pass). [Nit] base-04's fine linework and lavender ground; base-03's
  place names beside the aim line; the ceiling test's moon exclusion is generous; a moon centred off the board is never
  dropped; the ceilings sit at 0.457–0.459 against 0.46; level-method §5's 16 px.

## Done after round 2
- **Bricks' veil (all three):** no longer baked. Each brick carries the same veil as a row of the peg veil's sprites
  along its middle line, every 10 units, each at `MoonfallVeil.BrickAlpha(k)` = 1 − (1 − k)^(1/5.7) (5.7 sprites
  overlap a point on the line), fading with the brick as it clears (`RichVeil`). The F6 test applies the same sprites.
  Re-rendered: base-02's cleared y = 330 bricks leave no band.
- **n3:** a ribbon's value line uses the number floor (`Banner(…, subIsNumber: true)`).
- **n4:** the laurel is fitted per side (each half within half the room) and left out when even a small one does not fit.
- **Tally buttons** inset from the panel's corner scrolls.
- **base-04:** the lantern cords are tied to the walls and the lanterns sit on them, a little larger.
- F6 after the change: worst margins base-01 0.429, base-02 0.286, base-03 0.318, base-04 0.289; ceilings 0.216,
  0.459, 0.457, 0.415.

## Left for later (not blocking)
The F6 test at the deal only and without the lantern spill/beams (headroom 0.08 or more on every board); the hint's
extra row at 640 (one-time; the bar is out of scope); base-01's forest layer, base-02's crop and base-03's shared chart
(level-authoring pass); the tally's count-up starting from the HUD score (pre-existing); level-method §5's peg size.

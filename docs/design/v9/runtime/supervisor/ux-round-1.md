# Moonfall runtime art: UX/UI supervision, round 1

**Reviewed:** `spec-rich2.md` (sections 1, 4 and 6) and my `ux-round-3.md`; all 21 runtime JPEGs against the nine design PNGs. Cap heights measured with row profiles on 640 crops (HUD, power, fever, tally); a Machado deutan simulation on the marks render. Re-rendered: base-p1 hud 640 `--marks`, base-p1 power 1024×768, base-02 tally 1280, base-01 hud 640 and tally 1280 `--no-game-art`. Read `MoonfallDress.Veil`, the render harness and `DrawPegMarksHint`.

## Summary
The build is faithful to the approved screens. Every text measured at 640 meets the floor (level name caps 8 px, stage 8, score 10, balls 9, oranges 9, dial "10" about 9, cup values 9, callout 7, tally labels 7–8, tally numbers 9). Two problems need a fix: the readability veil is baked into the scene, so every cleared peg leaves a dark disc; and the tally at 640 is drawn at the board's reduced scale, about 25% smaller than approved, with a third of its height empty and the ACED plate's text on its frame.

## Verdicts
| Asset | Verdict |
|---|---|
| hud-1280 | **REVISE** (M1) |
| hud-640 | **REVISE** (M1; Minor m1) |
| power-1280 | APPROVE |
| power-640 | APPROVE (Minor m3) |
| fever-1280 | APPROVE (Nit) |
| fever-640 | APPROVE |
| tally-1280 | APPROVE (Minor m4) |
| tally-640 | **REVISE** (M2) |
| peg marks | APPROVE (unverified hint, m6) |
| fallback (no game art) | APPROVE (Minor m5) |
| scenes base-02/03/04 | APPROVE (Nit) |

## Findings
- **[Major] M1. Every play-state screen: cleared pegs leave dark discs** (base-01-hud-1280 at about (712,412), (780,412), (680,488), (740,488); 25–35% darker). These are the peg-sized holes F3d forbids. Fix: take the per-peg veil out of the baked base; draw it as a soft dark sprite under each live peg that fades with the peg, or bake only the movers' paths and the bricks.
- **[Major] M2. tally-640: the tally scales with the board, not the window** (about 355×350 px against the approved 430×440; row labels 7–8 px against 10; about 100 px empty; "ace score 100,000" on the plate's rule; Cid's caption dropped). Fix: size the tally from the area below the bar at the approved type sizes; inset the ACED plate; restore the caption.
- **[Minor] m1. hud-640: the bar takes about 90 px of 480**, a third row with the one-time hint. Fold the bar to one row at 640.
- **[Minor] m2. All 1280 HUDs: the margins are near-black.** Raise the margin blur's brightness to the design's level.
- **[Minor] m3. power-640 (and 1024×768): the ribbon's tail runs over the level name.** End the tails at the name plate's edge.
- **[Minor] m4. tally-1280:** "11 BALLS LEFT × 10,000" in Jupiter's old-style figures ("II"), full capitals among mixed case; about 150 px empty. Use "Balls left 11 × 10,000" with lining figures and fit the window to its rows. Nit: hide "Super Guide ×0".
- **[Minor] m5. Fallback at 640:** the bar's first row clips "Quick Play"; the fallback tally drops ACED and NEW BEST.
- **[Minor] m6. Peg marks: the one-time hint is unverified** (the harness marks it seen). The marks themselves are right at 1280 and 640, and under deutan the star and the crescent stay unmistakable.
- **[Nit] fever:** a lighter teal pill behind the words FULL MOON; the 640 subline runs over a lit orange peg.
- **[Nit] power-640 callout:** 6 units is 3–4 px at 0.58 px a unit.
- **[Nit] base-04:** the veil along the ring's path draws concentric dark bands.
- **[Nit] hud-640 dial:** "×10" crowds the ring.

Not verified: Dalamud's font atlas and GPU differences, the motion, the one-time hint, widths between 1024 and 1280.

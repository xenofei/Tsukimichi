# Moonfall rich pass 2: UX/UI supervision, round 2

Date: 5 October 2026
Reviewer: game UX/UI specialist supervisor (independent)
Reviewed: `docs/design/v9/rich2/` at commit e9691733. I read `supervisor/response-round-1.md`, the updated `spec-rich2.md` (sections 1–5), the 18 screens (9 screens × 2 sizes, `power` included), `screens/pegmarks.png`, `characters/lineup.png`, the 6 composites, and all 48 frames of each of `motion/title.png`, `motion/play.png` and `motion/fever.png`, decoded with Pillow. I re-measured independently with numpy:
- WCAG contrast;
- per-glyph ink heights;
- per-pixel motion range against each peg's distance in units, using the approved level files scaled to the preview;
- Machado-2009 protan and deutan simulation, then OKLab a/b distance from each orange peg's core to a 12–20 px ring round it.

## Verdicts

| Asset | Verdict |
|---|---|
| Title (1280, 640) | **APPROVE** |
| Adventure map (1280, 640) | **APPROVE** (Minors) |
| Characters (1280, 640) | **APPROVE** (Nits) |
| Level select (1280, 640) | **APPROVE** (Minor) |
| In game HUD (1280, 640) | **APPROVE** (Minor) |
| A power firing (1280, 640) | **REVISE** (Major M1 at 640) |
| Fever (1280, 640) | **APPROVE** (Minor) |
| Tally (1280, 640) | **APPROVE** (Nit) |
| Pause (1280, 640) | **APPROVE** (Minors) |
| Character line-up | **APPROVE** (Nit) |
| HUD chrome on the 6 composites | **APPROVE** (Minors). Every board still reads; the dial and rail are fixed. |
| Motion: title | **APPROVE** |
| Motion: play | **APPROVE** (Minor) |
| Motion: fever | **APPROVE** |
| Peg marks | **APPROVE** (Minor) |

## Round-1 findings: status

| # | Status | Evidence |
|---|---|---|
| M1 Continue title clipped | **Resolved** | "3-3 The Airship Road" sits inside the card column. The card is the default focus and has a "Continue 3-3" pill. |
| M2 map stop states | **Resolved at 1280** | There are four distinct states: drained portrait with padlock, moon pip, glow in the carrier's colour, and card back. There is also a legend and a tooltip. Stage 5 is locked everywhere. The 640 size drops the padlocks (new m6). |
| M3 level-select hierarchy | **Resolved** | Sealed tiles are dark and drained, with a padlock and "opens after 3-3" (6.6:1). The open tile has the selection glow. |
| M4 640 level-select clipping | **Resolved** | One row of five tiles; Play is in a strip. One title still runs into its frame (new m7). |
| M5 text floors | **Resolved** | 640 dial: "×1" is clear, with a real AXIS ×. The 640 cup values are about 9 px cap on plates (8.3:1). "hold to restart" and "hold to leave" are 7–9 px (5.5:1). Labels that do not fit are dropped, not shrunk. AXIS secondary text at 640 measures at the 7 px cap floor. |
| M6 laurel over the title | **Resolved** | The laurel frames FULL MOON and never crosses it. |
| M7 LEVEL CLEAR plaque | **Resolved** | LEVEL CLEAR is on the ribbon at title size, at both sizes. |
| M8 "ACED tt new best" | **Resolved** | 640 now reads "ACED", "NEW BEST" (4.6:1) and "ace score 240,000". The tally adds up: 71,880 + 100,000 + 30,000 + 25,000 + 25,000 = 251,880, the same as the HUD score. |
| M9 pause collisions | **Resolved** | The window is taller. The note is 6.99:1 and clear of the corners. |
| M10 fireflies onto pegs | **Resolved** | See the motion measurements below. |
| m1 frame seams | **Resolved** | Thumbnail, tile and header joins are now clean. A different corner cut remains in the HUD (new m8). |
| m2 pill seams | **Resolved** | |
| m3 margins and floating ornaments | **Resolved** | The margins show the blurred scene; the floating scrolls are gone. |
| m4 power-name fitting | **Resolved** | Each name sits on a dark plate in two lines. |
| m5 dial artifact | **Resolved** | |
| m6 switch state | **Resolved** | On/Off text, the track filled when on, ‹ › steppers. The steppers are too small (new m9). |
| m7 Leave without safety | **Resolved** | Garnet pill, hold to confirm. |
| m8 no default focus | **Resolved** | |
| m9 broken AXIS spacing | **Resolved** | |
| m10 secondary contrast | **Resolved** | Every caption I measured is 5.0–8.4:1. |
| m11 face-down power, Back position | **Resolved** | The face-down card shows its power; Back is top-left everywhere. At 640 the dimmed cards drop their power (n2). |
| m12 640 medallion overlap | **Resolved** | |
| m13 colour-blind distinction | **Resolved for base-p1** | Peg marks are added. base-p1 orange separation, 10th percentile, is now 0.173 normal and 0.137 protan (approved pass: 0.154 and 0.129). base-p3 has a new regression (new m4). |
| m14 lantern and medallion in the preview | **Resolved** | The flicker sits on the cart's lantern (the frame corners now move by ≤ 0.004). The medallion glow ring is rendered. |
| m15 map crowding | **Resolved** | |
| m16 Jupiter on small buttons | **Resolved** | |
| n1 moogle name | **Partly resolved** | The 640 grid still says "THE MOOGLE" (n2). |
| n2–n4 | **Resolved** | |

### Motion measurements (independent)

**`motion/play.png`** (640 × 480, board at 0.8×, base-p3). Luma range over all 48 frames, by distance from the nearest peg edge:

| Band (units) | Max range |
|---|---|
| Inside pegs | 0.003 |
| 0–2.5 | 0.008 |
| 2.5–5 | 0.020 |
| 5–8 | 0.032 (one pixel, at about (290, 340); 99.9th percentile 0.019) |

- Range counts the beams dimming as well as brightening, so the positive lift is at most about half of it. The rule (0.03) holds.
- **Nit:** the spec's "0.008 within 8 units" matches only the 0–2.5 band in my measurement. Report the figure per band.
- The loop is seamless.

**`motion/title.png`:**
- Buttons: zero change.
- Card: 99th percentile 0.008.
- Logotype: only the 0.8 s glint.
- Seamless.

**`motion/fever.png`:**
- The arrival takes 1.5 s, then holds. The cups breathe and one glint crosses the laurel.
- The jump at the wrap is expected, because the arrival is one-shot and the preview loops.
- Pegs change during the arrival. This is Fever's gameplay lighting, not ambient motion, so it is acceptable.

## New findings

### Major

**M1. Power firing at 640: Cid's card covers live pegs, and can cover the ball.**
- **What I see:** at 640 there is no margin, so the card and its caption sit inside the board at px (62–145, 92–245), which is units (78–181, 115–306) at 0.8×. Comparing `power-640.png` with `hud-640.png`, it hides the pegs at about px (143, 175) and (104, 207). It stays for 1.2 s, while the ball is live.
- **Concrete case:** the card always appears top-left. exp-p3's green peg sits at about (150, 220) units, inside that box. Hitting that green at 640 puts the card on top of the ball at the instant the power fires.
- **Why it matters:** the owner's rule is that pegs stay readable. Losing sight of the ball at the most exciting moment is the worst place to break that rule. At 1280 the card is in the outer margin and is fine.
- **Fix (any one):**
  - At 640, keep the moment in the chrome. Pulse the rail portrait and gems in the carrier's colour, and slide a name ribbon along the top rail ("BRASS WINGS · Cid Garlond").
  - Or place the card on the wall side away from the ball's x, never within 40 units of the ball or the green, and draw it at 60% opacity over pegs.
- Mock both sizes from a case with the green near the top-left.

### Minor

**m1. Power firing, both sizes: the turns-left gauge cannot show the power's length.**
- **What I see:** "the bucket doubles for 5 turns" (1280 margin, and `r2cast` `lasts="5 turns"`), yet the scholar gauge has three gems and shows 3 lit (power-1280 x ≈ 1100–1160, y ≈ 393–410). Flippers is 3 turns and Super Guide is 3 shots.
- **Fix:** either gems = the power's count (a row of 5 for Brass Wings, which fits the 1280 rail), or keep 3 gems and print the number on them ("5"). The gauge and the text must agree.

**m2. Pause, both sizes: Peg marks shows On, but the board behind it has no marks.**
- **What I see:** for example the orange peg at 1280 (905, 268) is plain. The HUD, power, Fever and tally screens are also unmarked.
- **Why it matters:** this breaks the one-progress-state rule, and the owner will judge the setting from these mocks.
- **Fix:** set Peg marks Off in the shared state (`r2state`), or render marks on every in-game screen.

**m3. Fever, both sizes: the FULL MOON plate is now solid and covers pegs for the whole of Fever.**
- **What I see:** the plate spans 1280 x 410–870, y 248–375 (640 x 180–460, y 145–225). It half-hides the green peg at 1280 (517, 240) and passes over others. The round-1 banner was open lettering; this one is an opaque plank.
- **Why it matters:** the last ball plays out in slow motion behind it.
- **Fix:** after the 1.5 s arrival, fade the plate to about 35% or lift it to the top rail. Or drop the plate and keep the ribbon and lettering only.

**m4. base-p3 composite, protan and deutan: orange separation is half that of the approved board.**

| Vision | Approved pass | Rich pass 2 |
|---|---|---|
| Protan (10th percentile) | 0.156 | 0.078 |
| Deutan (10th percentile) | 0.170 | 0.100 |

- **Where:** the orange pegs in the emerald band, y ≈ 440–456 units (for example (135, 447), (211, 442), (590, 456)). Under protan, emerald and orange collapse to the same olive.
- Luma margins still pass, and Peg marks mitigate this. But the assist is opt-in.
- **Fix:** push the wood's jewel toward teal (about 185–195°) or halve its chroma within 12 units of pegs. Add the protan check to `readcheck.py` (10th percentile of at least 0.12).

**m5. Map 1280: the legend gives the card back two meanings.**
- **What I see:** "not yet met · 11: your pick" (x ≈ 1040–1205, y ≈ 526). Stop 11's card back means a free choice; stop 2's means not met.
- **Fix:** give stop 11 its own sign, such as the crest without the card-back tooling or a "?" plate, with its own legend line.

**m6. Map 640: no padlocks and no legend, so "not reached" is shown only by dimming.**
- **Where:** stops 4–10, px (130–480, 220–410).
- **Fix:** keep the padlock (it is a 7–8 px glyph and fits on the number plate), or put "locked" in the stop's focus tooltip.

**m7. Level select 640: "THE IRONWORKS DOCK" runs into the tile's right frame.**
- **What I see:** the K is cut at x ≈ 611, y ≈ 183–191.
- **Fix:** apply the fit-to-column rule to tile captions, with an 8-unit inset, or allow two lines.

**m8. HUD, all composites and both sizes: the top-right corner ornament ends in a hard square cut.**
- **What I see:** the cut is offset from the rail band. 1280: x ≈ 1150–1172, y ≈ 78–86. 640: x ≈ 625–638, y ≈ 40–50. On composites it is visible at the top of the right rail.
- **Fix:** cut the corner piece by its alpha (connected component), as the journal corners are, or let the rail band overlap it by 1 px.

**m9. Pause, both sizes: the ‹ › steppers are about 5 px glyphs set apart from their value.**
- **What I see:** for example 640 (402, 368) and (446, 368).
- **Why it matters:** they barely read as controls, and the hit targets are tiny.
- **Fix:** draw them as small gilt chevron buttons, at least 14 × 14 at 640 and 20 × 20 at 1280, hugging the value. Or make the whole row a stepper with ←/→ on focus.

**m10. Motion, play: the fireflies sit in one row, in pairs, in the bucket's lane.**
- **What I see:** all 14 fireflies are at about y 556 units, under the last peg row (preview px y ≈ 445). They read as pairs of eyes in the dark, and they move where the player watches the ball fall into the cart.
- **Fix:** let the clearance rule pick from several clear pockets (between the upper rows and beside the trunks), vary y by at least 20 units, and keep at least 18 units between fireflies. Keep them out of the bucket's sweep (y > 530) or dim them there.

**m11. Peg marks: the purple star is the weakest mark, and it is the case that needs it most.**
- **What I see:** under deutan, purple becomes pale blue, and the star is about 6 px at 1× (about 5 px at 640). It is close in size to the blue peg's own crater texture.
- **Fix:** enlarge the star to about 55% of the peg's diameter, give it a light rim, and drop the blue peg's crater texture when marks are on.

### Nit
- **n1.** The companion accents are hard to tell apart in pairs. On the line-up text: Minfilia and moogle are ΔE_ok 0.037 apart, Louisoix and Merlwyb 0.039, Urianger and Y'shtola 0.045, Cid and Raubahn 0.047. These sit at or below the level where a colour can be named, so "a colour you learn" holds only with the name beside it. Separate them in lightness or chroma as well as hue.
- **n2.** Characters 640: the grid says "THE MOOGLE" (elsewhere "Moogle courier"), and the dimmed cards drop their power (1280 shows "Lunar Burst · stage 4"). Shorten to "Lunar Burst · 4" instead.
- **n3.** Tally 640: Cid's portrait has no name or power line. Add "Cid · Brass Wings ×1" at the floor.
- **n4.** The hold-to-confirm progress (a fill sweeping the pill while held) is not mocked. Show one frame of it.
- **n5.** The LONG SHOT callout is a brown plaque, not the "small gilt ribbon" the spec describes. Match one to the other.
- **n6.** Pause 640: the window's top overlaps the HUD name plate ("THE AIRSHIP ROAD" is clipped at y ≈ 10–22). Start the window below the top rail, or dim the HUD.

OVERALL: REVISE

# Moonfall rich pass 2: UX/UI supervision, round 3

Date: 5 October 2026
Reviewer: game UX/UI specialist supervisor (independent)
Reviewed: `docs/design/v9/rich2/` at commit 5130f3df, against `supervisor/response-round-2.md`.

I looked at:
- the 18 screens, `screens/pegmarks.png`, `characters/lineup.png` and the 6 composites;
- all 48 frames of each of `motion/title.png`, `motion/play.png` and `motion/fever.png`, decoded with Pillow.

I re-measured with numpy:
- motion range by distance from each peg, using the approved level files scaled to the preview;
- the loop seams;
- orange-peg separation (OKLab a/b distance from peg core to a 12–20 px ring) under Machado-2009 protan and deutan, on all six boards.

## Verdicts

| Asset | Verdict |
|---|---|
| Title (1280, 640) | **APPROVE** |
| Adventure map (1280, 640) | **APPROVE** |
| Characters (1280, 640) | **APPROVE** (Nit) |
| Level select (1280, 640) | **APPROVE** |
| In game HUD (1280, 640) | **APPROVE** |
| A power firing (1280, 640) | **APPROVE** (Minor, Nit) |
| Fever (1280, 640) | **APPROVE** |
| Tally (1280, 640) | **APPROVE** (Minor) |
| Pause (1280, 640) | **APPROVE** (Nit) |
| Character line-up | **APPROVE** |
| HUD chrome on the 6 composites | **APPROVE** |
| Motion: title | **APPROVE** (Minor) |
| Motion: play | **APPROVE** |
| Motion: fever | **APPROVE** |
| Peg marks | **APPROVE** |

## Round-2 findings: status

| # | Status | Evidence |
|---|---|---|
| M1 power card covers the board at 640 | **Resolved** | At 640 nothing of the power moment is drawn inside the opening. A laurelled BRASS WINGS ribbon sits on the top rail. The rail portrait glows in Cid's colour over five lit gems. At 1280 the card stays in the outer margin. |
| m1 gauge against the text | **Resolved** | A five-gem gilt bar for Brass Wings at both sizes ("for 5 turns"). Unlit, it reads as an empty slot on the composites. |
| m2 Peg marks On, no marks | **Resolved** | The pause screen shows Off at both sizes, so the state is consistent. |
| m3 FULL MOON plate | **Resolved** | Once landed the plate is translucent; pegs and the green read through it (fever-1280, around (517, 240)). The lettering and sub-line stay crisp. |
| m4 base-p3 protan and deutan separation | **Resolved** | Orange 10th percentile: 0.152 protan and 0.172 deutan (round 2: 0.078 and 0.100; approved: 0.156 and 0.170). All six boards are now 0.11–0.15 under protan, at or above the approved figures. |
| m5 card back's double meaning | **Resolved** | Stop 11 has a gilt four-point star with its own legend line, "your pick". |
| m6 no padlocks at 640 | **Resolved** | Padlocks show on stops 4–10 at 640. |
| m7 IRONWORKS DOCK clipped | **Resolved** | It wraps to "THE / IRONWORKS DOCK" inside the inset. |
| m8 square corner cut | **Resolved** | The vine stem fades out. A faint step remains at 1280 (1160, 83), visible only at 4× zoom. |
| m9 steppers too small | **Resolved** | Gilt chevron buttons hug Full and 70% at both sizes. |
| m10 fireflies in a row | **Resolved** | Nine fireflies spread across y ≈ 400–515 units, in pockets, and out of the bucket's lane. |
| m11 purple star | **Resolved** | The star is about 55% of the peg with a light rim. Under deutan it is unmistakable against plain blue. The leaf is pointed at both ends with a midrib. |
| n1 accent pairs | **Accepted as is** | The name is always beside the colour. |
| n2 characters 640 | **Resolved** | "Moogle"; the dimmed cards show power and stage ("Burst · 4"). See new n2. |
| n3 tally 640 Cid line | **Resolved** | See new m2, a clipping. |
| n4 hold progress | **Resolved at 1280** | A lighter fill sweeps the Restart pill. See new n3. |
| n5 style-shot callout | **Resolved** | It is now the small ribbon style. See new m1. |
| n6 pause 640 over the name plate | **Resolved** | The window starts below the top rail. |

### Motion (independent)

**`motion/play.png`.** Luma range over all 48 frames, by distance from the nearest peg edge:

| Band (units) | Max range |
|---|---|
| Inside pegs | 0.003 |
| 0–2.5 | 0.008 |
| 2.5–5 | 0.020 |
| 5–8 | 0.034 (one pixel; 99.9th percentile 0.019) |

- Range includes the beams dimming, so the positive lift is about half of it. That is consistent with the spec's 0.017, and within the 0.03 rule.
- The loop is seamless.

**`motion/fever.png`:** the arrival plays, then holds. The cups breathe and one glint crosses the laurel.

**`motion/title.png`:** buttons and card are static. The seam is covered in new m3.

## New findings

### Major
None.

### Minor

**m1. Power firing, both sizes: the LONG SHOT ribbon covers a live peg.**
- **Where:** 1280: the ribbon spans x 795–1040, y 145–215 and hides the blue peg at about (1012, 212); compare with `hud-1280`. 640: the ribbon spans x 412–560, y 85–130 and covers the peg at about (544, 127).
- **Why it matters:** the spec says "a small gilt ribbon in open sky". A peg hidden while the ball is in play is the same concern as round-2 M1, smaller in scale.
- **Fix:** place the callout by rule, at least 6 units from every live peg (search candidate spots near the shot's apex). Or draw it at about 60% over pegs, as the Fever plate now is.

**m2. Tally 640: "Cid · Brass Wings ×1" runs into the window's right frame.**
- **What I see:** the "1" sits on the gilt band at x ≈ 532–536, y ≈ 340–346.
- **Fix:** fit the line to the column with an 8-unit inset (it can centre under the portrait and shift left), or drop "×1" at 640.

**m3. Motion, title: the mist layers jump at the loop seam.**
- **Measured:** the frame 47 → 0 difference in the lower third (y 240–375) is a mean of 4.4e-3 with a 99th percentile of 0.031, against 0.2e-3 and 0.004 between normal frames. That is about 20× normal: a soft shimmer pop every 6 s across the mist.
- This was also in the round-2 render, and I missed it then.
- **Why it matters:** the spec promises seamless loops. In game the mist should scroll continuously, so check the runtime design as well: the quad's UV scroll must be modulo its tile width.
- **Fix:** make each mist layer's 6 s travel (24 and 54 px) a whole multiple of its tile period, or cross-fade the last 0.5 s into the first.

### Nit
- **n1.** Power firing at 640: the ribbon's tails and laurel are drawn over the level name plate, so "THE AIRSHIP ROAD" is half covered at x ≈ 140–200, y ≈ 10–22. That is acceptable for 1.2 s, but shorten the tails so they end at the plates' edges.
- **n2.** Characters 640: "Storm Post · FS" uses an abbreviation no other screen teaches. Use "Storm Post · 11" or "Far Shore".
- **n3.** The hold fill on Restart is mocked only at 1280. The 640 pill shows no progress. Mock it at 640 too, or note that it is the same at both sizes.

OVERALL: APPROVE

# Moonfall runtime art: game designer supervision, round 1

Reviewed: the spec (sections 1, 3–5), characters.md, my round-3 verdict, all nine design screens, and all 21 runtime JPEGs. I cropped the rails, ribbons, tally, peg marks and scene detail at 2–4× and took a luma profile across a cleared-peg spot. I re-rendered base-p1 fever with `--seconds 2.5` (it lands in the tally mid-count) and base-02 hud at 640. I also read `MoonfallWindow.Moments.cs`, the four scene recipes and the harness's `Stage()`.

## Summary
The chrome and the reward moments are faithful to the approved screens, and at 1280 they are often richer:
- **Power card:** larger, with the power named in the carrier's colour and a plain-language hint.
- **Rail HUD:** balls, oranges, multiplier, score, power name and gems all read at a glance at 1280.
- **Fever:** reads as an event, with the laurel ribbon, the plate fade, lit cups and the ×10 dial.
- **Tally:** carries LEVEL CLEAR, the gilt total and the ACED / NEW BEST callout.
- **Peg marks and the no-art fallback:** both are good.

**Main problem: the scenes, not the HUD.**
- The baked readability veil leaves a dark peg-shaped hole wherever a peg has been cleared. On smooth skies the board turns pocked as play goes on.
- base-04 is the plainest board and is nearly a copy of base-01.

**Other issues:** a few polish bugs in the tally, one round-3 Nit still open, and the 640 toolbar costs real board size.

## Verdicts
| Asset | Verdict |
|---|---|
| hud-1280 | APPROVE |
| hud-640 | APPROVE |
| power-1280 | APPROVE |
| power-640 | APPROVE |
| fever-1280 | APPROVE |
| fever-640 | APPROVE |
| tally-1280 | APPROVE |
| tally-640 | APPROVE |
| peg marks | APPROVE |
| fallback (no game art) | APPROVE |
| scene base-01 (moon-road-night; shown via hud/fever) | REVISE |
| scene base-02 (holy-see) | APPROVE |
| scene base-03 (airship-road) | APPROVE |
| scene base-04 (lantern-night) | REVISE |

## Findings
- **[Major] scenes, base-01 and base-04 (shared code, all boards): cleared pegs leave dark holes.** `MoonfallDress.Veil` is baked once at load around the initial layout; when a peg clears, its dimmed disc stays (base-01-hud-1280 about (545,350): luma 0x1F against 0x2F, about 35% local dimming). By Fever most of the board is pocked. Fix: draw the veil per live peg as a soft sprite that fades with the peg, or bake only a low-frequency veil.
- **[Major] base-04: plain, and nearly a duplicate of base-01.** Same painting, same teal band and hills, boughs in the same corners; no focal light; the "lanterns" are two 6 px dots. Fix: real lanterns (bodies, cords, warm pools), a distinct second jewel and foreground; keep F2/F3/F6 green.
- **[Minor] base-04: the mover veil draws a bullseye** round about (640,490). Use a softer falloff for mover paths.
- **[Minor] base-01: a step down from its pilot** (no forest silhouette layer from base-p3).
- **[Minor] hud-640 / toolbar:** the wrapped toolbar takes about 95 of 480 px; the Brass Wings turns-left bar is about 22×3 px. Collapse the toolbar at narrow widths; give the gems a size floor.
- **[Minor] tally-640: the ACED plate's text overflows** its lower gilt rule.
- **[Minor] tally: NEW BEST shows before it is earned** (lit while the total still counts at 5,600 against a best of 60,000). Reveal it as the count passes the old best.
- **[Minor] tally-1280/640: dead space** between the callout and the buttons.
- **[Minor] fever: a hard pill behind FULL MOON** (the accent glow is a hard-edged rounded rectangle).
- **[Nit] power-640:** the BRASS WINGS ribbon's tail cuts the level name.
- **[Nit] LEVEL CLEAR and FULL MOON plates:** the drop shadow ends in a hard band.
- **[Nit] tally-640:** the companion's name and power are omitted.
- **[Nit] tally:** the Fever cups' value plates peek below the panel.
- **[Nit] tally:** "11 BALLS LEFT" in Jupiter's old-style figures reads as "II".
- **[Nit] fever:** the ribbon tails stay opaque over pegs; fade them with the plate.
- **[Nit] hud-1280:** the Brass Wings gems light pale white, not Cid's copper.
- **[Nit] hud-1280:** the margins are much darker than the design's blurred scene.
- **[Nit] base-03:** it shares the identical chart with level 13.
- **Resolved:** round-3 N9 (the LONG SHOT ribbon covers no peg at 1280 or 640).

Unverified: the flash and ring at the green (0.3 s), Fever's moondust burst, the rest of the motion, real frame pacing and GPU output.

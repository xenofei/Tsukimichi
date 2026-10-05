# Moonfall runtime art: level-design critic supervision, round 1

**Reviewed:** level-method.md (F1–F6), my rich-pass round-3 verdict, scene-recipe.md, the four recipes, the level files (base-01..04 and the pilot base-p1), `MoonfallFramingCheck.cs`, `MoonfallMotion.cs`, the builder's dress, veil, stars and mist code, and the test sources. Ran the three scene suites with `TSUKIMICHI_GAME_PATH` set: 66 of 66 pass (base-01: cover 5.46%, clearance 9.3, 6 dropped; base-02: cover 7.33%, middle 0.08%, clearance 9.6, 1 dropped; base-03: no framing; base-04: cover 1.74%, clearance 12.2). Renders: all five boards at 1280 and 640, base-02 `--marks`, base-04 `--reduce-motion`. My own F6-style measure on those renders (view fitted from peg centroids: 1.2135 px a unit at 1280, 0.631 at 640): face 80th-percentile luma in 0.6 r; backdrop 90th percentile of the ring r+2..r+9 at every peg position; 99th percentile of the scene with no pieces. base-02 compared side by side with the approved base-p2 composite.

## Summary
The scenes are ported faithfully (base-02 matches the approved Holy See composite almost exactly) and every board stays readable: the worst F6-style margin is 0.283 (purple on base-p1 at 640), 0.285 on a shipped board (base-02 at 640); the ceiling is 0.29–0.45; motion stays off the pieces. Two things block: the authoring path has no readability guard, and base-03 carries the pilot's engraved route over Tidewater's unrelated layout, through its pieces.

## Verdicts
| Asset | Verdict |
|---|---|
| scene base-01 moon-road-night | APPROVE |
| scene base-02 holy-see | APPROVE |
| scene base-03 airship-road | REVISE |
| scene base-04 lantern-night | APPROVE |
| pilot base-p1 | APPROVE |
| motion keep-outs | APPROVE |
| peg readability at 640 | APPROVE |
| scene-recipe.md for authors | REVISE |
| runtime F-rule enforcement | REVISE |

## Findings
- **[Major] runtime F-rule enforcement: F6 is not checked anywhere, and the ranges allow unreadable scenes.** The loader accepts `grade: none` on a game painting, `ceiling` up to 0.6, `glow` k up to 0.5, `veil` 0, and a `moon` with no keep-out from the pieces. Fix: a test over the shipped scenes at 1× and 0.8× on the veiled base (ring p90 ≤ face − 0.20 at every piece position; p99 ≤ 0.46 piece-free) and a moon keep-out (no piece within r + 18) at build.
- **[Major] scene-recipe.md: the doc never states F6 or the value ceiling, and gives no layout-fit rule.** Fix: an F6 row and the ceiling; a "fit the layout" paragraph; a "render at 640 and look" step; say how to see dropped elements.
- **[Major] base-03 airship-road: the pilot's route crosses Tidewater's pieces and means nothing there** (through the orange near (134,440), along the orange brick about 3 px clear). Fix: drop `route` from the shipped chart recipe; give Route the compass rose's 6-unit keep.
- **[Minor] base-02 holy-see: Crescent's right arc lies across the city's face.** Re-crop so the city sits between the two arcs.
- **[Minor] base-01: the cleared-peg halos form ringed "sockets"** in the teal band (the `near` region term). Blur the near term or lower its scale.
- **[Minor] the F5 backstop is weaker than the rule** (light centre against 8 units, halo ignored). Subtract the halo reach.
- **[Nit] motion tests looser than the rules** (stars at 2.5 + 1.8 r, not the builder's 10; lamps by centre only).
- **[Nit] scene-recipe.md:** lights drop at 8 plus the halo, not "within 8 units"; "either order" is stated only for `lum`.
- **[Nit] base-04:** the veil draws the mover paths as concentric rings.
- **[Nit] level-method §5:** at 640 × 480 a peg is about 12.6 px across, not 16.

Not verified: in-game rendering; deals other than seed 1; the fallback picture under another recipe's palette; F7 and F9; motion frames beyond the code and tests.

OVERALL: REVISE (base-03; F6 at runtime and in the authors' doc)

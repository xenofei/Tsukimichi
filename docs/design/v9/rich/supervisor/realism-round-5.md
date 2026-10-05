# Realism supervisor, round 5 (verbatim)

The same independent agent judged the rendered files only. Its report follows unchanged; the designer's response is at the end.

Round 5 realism supervisor report: 20 of 21 assets approved; only the characters screen needs changes, for one Minor (Pipiru's hood).

```
ROUND 5 — REALISM SUPERVISOR

Method: rendered files only, measured with numpy. Earlier renders for comparison come from git.

Board-wide checks, all of which held:
- **Peg readability:** the smallest peg margin is +0.24 (exp-p2, unchanged), and +0.35 or more elsewhere.
- **Shadows and corners:** still no drop shadows and no corner marks.
- **1x and 2x match:** they differ by a mean of 1.9–3.2 levels.
- **Yellow-green cut:** 0 off-hue lit pixels on base-p2, exp-p1 and exp-p3.

What the scenes measure now:
- **Planet, detail finer than 3 px at 2x:** lit half 0.023, centre 0.026, dark half 0.022. Round 2 was 0.019, 0.020 and 0.018, so the focus is now even across the disc.
- **Planet, large-scale std:** 0.089 and 0.082.
- **base-p2 cloud boxes:** std 0.081 and 0.104.
- **Ceiling:** the 99th-percentile luma is 0.465–0.50.
  - A few small spots go above 0.55: the cathedral crown and bridge on base-p2 (about 1,100 px), small cloud specks on the planet, and map highlights on base-p1.
  - Every peg's margin over those spots is still at least +0.35, so readability holds. See the Nit on assets 2 and 8.

ASSET 1: scene base-p1-airship-road — APPROVED
1. [Nit] Carried over: the edge marks and smear behind the rails, and the gilt route drawn flat over the banners.

ASSET 2: scene base-p2-holy-see — APPROVED
The spires are crisp again: fine-detail energy on the cathedral is 0.055, the round-2 level. The clouds have their modelling.
1. [Nit] The crown's highlights now reach luma 0.74 at 1x ≈ (540–600, 200–260). Pegs on it still read (margin ≥0.35), but it is the brightest thing on the board apart from the pegs. Optional: soften the brightest spots to about 0.6.
2. [Nit] Carried over: the arch streaks.

ASSET 3: scene base-p3-moogle — APPROVED

ASSET 4: scene exp-p1-sharlayan — APPROVED

ASSET 5: scene exp-p2-lantern-ferry — APPROVED

ASSET 6: scene exp-p3-mare-lamentorum — APPROVED
The planet reads as cloud over ocean, in focus across the whole disc and lit from the upper left. The sphere is a single opaque limb. The pool is a true mirror.

ASSET 7: composite base-p1 — APPROVED

ASSET 8: composite base-p2 — APPROVED
1. [Nit] The bright crown behind the purple peg at 1x (623,133): see asset 2, Nit 1.

ASSET 9: composite base-p3 — APPROVED

ASSET 10: composite exp-p1 — APPROVED

ASSET 11: composite exp-p2 — APPROVED

ASSET 12: composite exp-p3 — APPROVED

ASSET 13: title — APPROVED
The moon is still emissive (0.89–0.92 luma) and matches at both sizes.
1. [Nit] Carried over: the moon's hard edge against the cloud.

ASSET 14: map — APPROVED

ASSET 15: characters — CHANGES
These are fixed:
- The human cameos' necks are now round cylinders, with the jaw's shadow, and the busts curve into the shoulders. Kaede, Marcia, Haldbrand, Aldous and Ione all read as carved.
- Pipiru's chin glow is gone: the ground under the chin at 1280 ≈ (1110,330) is lapis #121E43.

1. [Minor] Pipiru's new hood "folds" read as scratches, not cloth.
   - Where: the hero cameo at 1280 ≈ x 985–1075, y 185–320, and the same cameo on the HUD medallion, the map's stop 1 and the levels header.
   - What is drawn: dozens of thin raised hairlines, 1–2 px wide and 10–40 px long, at random angles, crossing one another and running across the hood's edge.
   - Why it fails: real folds in a soft hood are a few broad, rounded ridges and troughs. They hang along gravity from the crown and gather toward the nape and the shoulder, they do not cross, and each one turns from lit to shaded under the upper-left light.
   - At thumbnail size the strokes read as scratches or noise, which the owner's no-noise rule forbids.
   Fix: replace the strokes with 4–6 broad folds about 6–10 px wide at 1280. Run them from the crown down and back, each modelled as a rounded height ridge, with no crossings and no strokes over the silhouette edge.

ASSET 16: levels — APPROVED
1. [Nit] The header cameo shows the hood issue from asset 15.

ASSET 17: hud — APPROVED
The lantern now clears every peg at both sizes: at 1280 it sits at about (865,720), and the nearest peg at (895,700) is about 15 px away. The aim guide is still correct.
1. [Nit] The medallion shows the hood issue from asset 15.

ASSET 18: fever — APPROVED

ASSET 19: tally — APPROVED

ASSET 20: pause — APPROVED

ASSET 21: buckets.png — APPROVED

OVERALL: 20 of 21 assets are approved: all 6 scenes, all 6 composites, and the title, map, levels, hud, fever, tally, pause and buckets screens.

One is not: the characters screen, for a single Minor. Pipiru's hood strokes read as crossing scratches rather than soft folds. The same cameo appears as Nits on levels and hud.

No Majors remain.

Not verified: how the art looks in the game, motion, and the JPEG q88 4:4:4 export.
```

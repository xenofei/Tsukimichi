# Realism supervisor, round 4 (verbatim)

The same independent agent as rounds 1–3 judged the rendered files only. Its report follows unchanged; the designer's response is at the end.

Round 4 realism supervisor report: 18 of 21 assets approved; exp-p3 (scene and composite), characters and hud need changes, with Minor findings only.

```
ROUND 4 — REALISM SUPERVISOR

Method: rendered files only, measured with numpy. For before/after comparisons I read earlier renders from git (round 2 is commit cef7b166, round 3 is 83c4de95) into my scratch folder; I opened no source.

What held on every board:
- **Peg readability:** the smallest margin between a peg's lit face and the 90th-percentile luma of the ring around it is +0.24 (exp-p2), and +0.36 or more on every other board.
- **Shadows and corners:** still no drop shadows and no corner marks.
- **1x and 2x match:** they differ by a mean of 1.9–3.2 levels.
- **Yellow-green cut:** OKLab finds 0 off-hue lit pixels on base-p2, exp-p1 and exp-p3.
- **Contrast restored:** I measured base-p2's clouds at std 0.075/0.093 and exp-p3's planet at 0.072/0.060, the same as your numbers.

Remaining in the grade: detail finer than 2 px is still about 25–50% below round 2:
- planet: 0.026 → 0.016;
- base-p2 cloud sea: 0.028 → 0.020;
- Sharlayan roofs: 0.037 → 0.027.

It is visible only on the planet.

ASSET 1: scene base-p1-airship-road — APPROVED
1. [Nit] Carried over: the edge marks and smear behind the rails, and the gilt route drawn flat over the banners.

ASSET 2: scene base-p2-holy-see — APPROVED
1. [Nit] The cathedral's crown is slightly softer and more mottled than round 3: mean gradient 0.0297 → 0.0267 (2x x 980–1340, y 100–600). Restoring the band finer than 2 px would sharpen the spires.
2. [Nit] Carried over: the arch streaks.
The clouds have their modelling back.

ASSET 3: scene base-p3-moogle — APPROVED

ASSET 4: scene exp-p1-sharlayan — APPROVED
The dome falls off from 0.469 to 0.409 at 1x y 290, and the domes read as rounded.

ASSET 5: scene exp-p2-lantern-ferry — APPROVED

ASSET 6: scene exp-p3-mare-lamentorum — CHANGES
1. [Minor] The planet's lit half is out of focus while its shadowed half is crisp. This is one object at one distance, so the focus difference reads as a smeared watercolour.
   - Detail finer than 3 px at 2x: upper-left lit half (820–1040, 460–700) is 0.010, against 0.019 in round 2.
   - Centre (920–1100, 620–800): 0.010, against 0.020.
   - Lower-right half: 0.014.
   - It is visible in the composite at 1x (390–520, 230–350).
   Fix: put the band finer than 2 px back at full strength on large pale forms too, so the lit half's fine detail is at least 0.017, matching round 2 and the dark half.
2. [Nit] The sphere is fixed: one opaque disc, lit from the upper left, with the bracket in front. It now peaks at luma about 0.52 (2x (143–166, 611–633)), brighter than the planet's clouds. Optional: cap it near 0.45 so it does not compete with the planet.
The pool and its reflection pass.

ASSET 7: composite base-p1 — APPROVED

ASSET 8: composite base-p2 — APPROVED

ASSET 9: composite base-p3 — APPROVED
The lowest forest row is cut, and the lantern has a clear lane.

ASSET 10: composite exp-p1 — APPROVED

ASSET 11: composite exp-p2 — APPROVED

ASSET 12: composite exp-p3 — CHANGES
1. [Minor] The scene's issue 1: the soft-focus lit half of the planet behind the ring of pegs.

ASSET 13: title — APPROVED
The moon is emissive at both sizes: face 0.88–0.93 luma, cool white #E5E9F2, flat with soft maria, matching at 1280 and 640.
1. [Nit] The disc's edge is a hard cut over the lit cloud. Optional: add a 2–4 px bloom into the cloud at the edge, or let a thin cloud edge cross the lower limb, so the moon sits behind the clouds rather than pasted on top.

ASSET 14: map — APPROVED
The cameo stops now read as portraits at 60–80 px: Haldbrand's beard and Kaede's feather are legible.

ASSET 15: characters (cameos) — CHANGES
This is a large improvement. The faces now have brow, socket, nose, both lips and cheek volume; the hair has locks; Haldbrand's beard is carved; Kupsa is one mass.
1. [Minor] On the human cameos the neck and bust are still flat slabs with a uniform bevel, the emboss look the faces no longer have.
   - On the large Pipiru cameo (1280 ≈ x 1000–1070, y 300–370), the neck is a straight-sided rectangular post. A pass across the cheek and side of the head at y 290 stays at exactly RGB 175,187,217 over x 1026–1046.
   - The other humans have the same pillar neck: Kaede, Marcia, Aldous, Ione.
   Fix: round the neck as a cylinder turning away from the light, with the jaw shadow falling on it and a hint of the neck muscle. Make the bust's top surface curve into the shoulders, and run a gentle gradient across the plain side of the head.
2. [Minor] There is a soft bright blob in the dark ground under Pipiru's chin (1280 ≈ (1080–1095, 300–312)). It sits in the shadowed lower-right, glows onto the background and reads as a smudge. A carved stone cannot emit; light passing through it should only lighten the thin edges inside the silhouette. Fix: keep the translucency within the carving, and keep the under-chin edge, which faces away from the light, at shadow value.
3. [Nit] Pipiru's hero cameo still has a smooth helmet of hair with only two grooves, unlike the carved locks on the other cameos.

ASSET 16: levels — APPROVED
The 640 Play button is fixed.

ASSET 17: hud — CHANGES
The aim guide passes: it leaves the muzzle along the barrel's axis, the dots fall slightly under gravity, and the bounce is a glancing hit on the lower-left of the peg at 1280 ≈ (850,250) that turns the path downward. The new medallion is fine.
1. [Minor] The cart's lantern overlaps a blue peg at both sizes (1280 ≈ (575–600, 705–730); 640 ≈ (287–300, 425–440)). The lantern sits in front of the peg's left limb, so the two solids interpenetrate. That limb is also cool white (#C6CDEB), with no warm light from the lantern touching it. This is the round-3 bucket finding, now on the HUD.
   Fix: pick a position in the sweep where the lantern clears the pegs, as you did for the composites. If the cart has to sit there, give the peg its warm spill on the side facing the lantern.

ASSET 18: fever — APPROVED
The rim highlights on the cups are present.

ASSET 19: tally — APPROVED

ASSET 20: pause — APPROVED

ASSET 21: buckets.png — APPROVED
Each bucket is on an empty board. The strap on the lantern's side is warmer than the far one (0.743 vs 0.717). The boat's reflection is broken by ripples and sits directly under the lantern.

OVERALL: 18 of 21 assets are approved:
- the scenes and composites for base-p1, base-p2, base-p3, exp-p1 and exp-p2;
- title, map, levels, fever, tally, pause and buckets.

Three are not: exp-p3 (scene and composite), characters and hud. No Majors remain; there are four Minors:
- the planet's lit half is out of focus;
- the cameos' necks and busts are flat slabs;
- Pipiru's chin glow;
- the HUD lantern overlaps a peg.

Not verified: how the art looks in the game, motion, and the JPEG export.
```

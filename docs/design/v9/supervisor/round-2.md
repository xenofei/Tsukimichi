# Moonfall art, realism supervisor: round 2 (verbatim)

Supervisor: the same independent agent (a980d3496ee3ce660). It judged rendered files only. Before this round the coordinator ruled on the round-1 disagreements: no drop shadows on pegs or bricks (the board is open air); the What's new JPEG stays at quality 88, 4:4:4 (the owner's shipping recipe); the Medallion corner marks stay removed from the playfield. The supervisor was asked to accept each ruling or say why it could not.

```
ROUND 2 — REALISM SUPERVISOR. 7 of 8 approved; the base campaign tile still needs one change (the lantern bracket).

RULINGS
(1) No drop shadows on pegs or bricks: ACCEPTED. At 10x (style-frame-a@2x, peg at 2x (198,298)) the dark lower-right band sits inside one circular limb, about 390 px wide by 390 px tall at 10x. It is the unlit sliver in sky tone, not a cast shadow. With no surface to receive a shadow, round-1 items A1-3 (shadow part), A3-4 and A5-2 are withdrawn.
(2) What's new JPEG at quality 88, 4:4:4: ACCEPTED. It measures PSNR 43.6 dB against the PNG master. At 1x I see no blocking or banding. Mild ringing around the lantern halo shows only at 5x.
(3) Medallion corner marks removed from the playfield: ACCEPTED. This was my round-1 Nit.

ASSET 1: style-frame-a — APPROVED
1. [Nit] The "100" popup at 1x (457,313) now touches the lower limb of the unlit orange peg above it at (457,300). Fix: move it 3–4 px down, or set it beside the struck peg.
2. [Nit] I cannot see the launcher gauge glint at 1x or in a 3x crop of the 2x file (2x around (760,150)). Fix: make it a little larger or brighter on the arc's upper left.
3. [Nit] The Bucket A cradle (230–360, 568–588) is still lit symmetrically. Fix: a brighter left horn and a darker right horn.
4. [Nit] The top facets of the descending right-hand bricks (450–540, 420–460) are still almost as bright as those of the rising left-hand bricks.
Verified fixed: lobed, varied seas; soft terminator; no halo on unlit pegs; the ball is satin and clearly brightest (luma about 198 against about 156 for a blue peg); channel balls are seamless with contact shading; right-rail shading is gone.

ASSET 2: style-frame-b — APPROVED
1. [Nit] The hull reflection bands (2x around 900–1160, 1170–1190) are full-length rectangles. Fix: taper them toward the bow and stern to follow the hull shape.
Verified fixed: dark hull above the waterline; contact line; banded mirror reflection; broken warm column under the lantern; lantern warmth limited to the post, rail and stern.

ASSET 3: peg-states — APPROVED
1. [Nit] All 16 large reference pegs show the same sea layout at the same rotation. The board varies, so the sheet should too. Fix: show the four layouts across the rows.
2. [Nit] The purple Clearing-1 bloom (1x about (435,470)) still crosses the panel border at y≈525–560.
Verified fixed: the ball reference is satin with a soft horizon; "+1" is clear of the rail; the approach panel has no warm pool.

ASSET 4: fever — APPROVED
1. [Nit] The cup interiors are flat near-black. Fix: under upper-left light, the far inner wall on the right should catch a faint value.
Verified fixed: the band sits behind the pegs, so all remaining pegs read lit; the cups are real bowls with inset plates; lit pegs keep their seas; the ball reads clearly.

ASSET 5: readability — APPROVED
1. [Nit] The same tight "100" placement as Asset 1 item 1 (0.8x board at about (385,312)).
Verified fixed: the ball is now the brightest small thing at 0.8x; pegs read round; the sky wisps are gone.

ASSET 6: characters — APPROVED
1. [Nit] Super Guide's globe (2x about (300,530)): the equator band can read as a ball seam at medallion size. Fix: thinner, fainter bands.
2. [Nit] Sister Ottilie's petal cluster on the robe (2x about (680,1530)) reads as a paw print. Fix: scatter it, or make it smaller.
Verified fixed: the rim is on upper-left-facing edges only (checked Zen Ball at 2.5x); emissive props light their nearest surfaces, with faint ground pools; Ottilie has a hooded head and a crook, with no crescent emblems; the ring is open with sky inside; accent colours are near silhouette value.

ASSET 7: campaign-base + campaign-expansion — CHANGES
1. [Minor] Base: the bracket under the lantern (1x about (698,330); 2x crop at (1330,610)) is still a saturated, evenly lit gold crescent, outer underside included. It reads as a glowing crescent-moon or smile glyph under the lantern, a second moon motif in a painting allowed one moon. Fix: dark brass in shadow with warmth only on its upper inner lip facing the lantern, or a non-crescent bracket shape.
2. [Nit] Expansion: the traveller (1x about (578,395)) carries a faint warm-olive rim on the left and back edges, away from the lantern. Fix: a cool rim there, warm only on the right side facing the lantern.
3. [Nit] Expansion: there is a small light spike on the top right of the traveller's head (2x about (1172,748)).
Verified fixed: the moon has one circular limb, an internal soft terminator, six lobed maria and a neutral tone, and is no longer coin- or cheese-like. The road is darker far and lit near, with the lantern pool crossing it. The figure has a shadow. The moon glints are cooler. The treeline is varied. Shared identity holds.

ASSET 8: art/moonfall-b-medallion.png + whatsnew-1.23.0.jpg — APPROVED
1. [Nit] The moon glints (x≈205–265, y 260–405) are still a little more yellow than the moon's cream.
2. [Nit] The traveller (1x about (735,305)) shows a faint warm rim on the side away from the lantern. It is barely visible at 1x.
3. [Nit] The gold pole still has no reflection beside the lantern column.
Verified fixed: the new moon follows all the moon rules; the hull is darker; lantern warmth falls off fast; the stray streak is gone. The JPEG is acceptable per ruling (2).

OVERALL: 7 of 8 APPROVED. Asset 7 is blocked by one Minor (the base tile's crescent bracket).
Most important fixes:
1. Base campaign tile: make the lantern bracket dark brass lit only on its inner lip, or change its shape, so it stops reading as a second crescent moon.
2. Expansion and What's new: move the traveller's rim to the correct sides (cool upper left, warm only facing the lantern).
3. Playfield: give the "100" popup a few pixels of clearance from the peg above; make the gauge glint visible at 1x.
```

## Designer's response to round 2

- **The base tile's bracket (the Minor):** the crescent is gone. The lantern now hangs from the beam on its cord over a small square tray of dark brass, which is warm only on the top lip facing the lantern.
- **The nits** are all fixed:
  - the popup clearance;
  - a larger gauge glint;
  - the cradle lit unevenly, so its left horn is brighter;
  - the bricks' top bevels;
  - the boat's reflection tapered to the hull;
  - the peg sheet showing all four sea layouts;
  - the purple bloom clipped to its panel;
  - the cups' far inner walls;
  - finer globe bands;
  - the petals scattered;
  - the travellers' rims (cool on the upper left, warm only facing the lantern);
  - the head spike;
  - cooler moon glints;
  - the pole's reflection.

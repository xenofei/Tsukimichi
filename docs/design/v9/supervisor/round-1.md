# Moonfall art, realism supervisor: round 1 (verbatim)

Supervisor: an independent agent (a980d3496ee3ce660) that judged the rendered PNG and JPG files only, never the source.

```
ROUND 1 — REALISM SUPERVISOR

ASSET 1: style-frame-a (.png / @2x) — CHANGES
1. [Major] Peg seas. Every peg on the board has the same soft, blurred "M"-shaped smudge in the same place and orientation (blue peg at 1x (100,148), orange at (148,190), all others the same). It reads as a stamped thumbprint or dent, not broken lobed maria, which breaks the owner's moon rule. Fix: 3–5 crisp-edged, irregular, lobed maria per peg, with at least 4 variants and a random rotation for each peg.
2. [Minor] Unlit sliver too thin. On unlit pegs it is a 1–2 px dark rim on the lower right (about 0.1 r at 2x), with a hard edge and no soft terminator, so at 1x the pegs read as flat discs. Fix: widen the shadowed lower-right zone to about 0.2 r and blend it with a soft gradient terminator.
3. [Minor] Peg silhouette. The dark offset drop shadow is the same value as the peg's dark rim and sits almost touching it, so each peg reads as an egg tilted toward the lower right (visible at 1x everywhere, for example (125,315) and (200,367)). Fix: make the shadow lower-contrast and softer, offset it a little further, and separate it from the rim with a thin lighter board gap.
4. [Minor] Unlit pegs carry a pale cool glow halo (for example (100,180) and (700,182)). A non-emissive object cannot glow, and the halo weakens the lit/unlit contrast. Fix: no halo on unlit pegs; keep glow for lit pegs only.
5. [Minor] Brick arc (260–540, 410–460). The bevel shading rotates with each brick: the top facets of the descending right-hand bricks (450–540) are as bright as those of the rising left-hand bricks, although they face away from an upper-left light. The bricks also cast no drop shadow while the pegs do. Fix: shade the facets by their world-space normal against the one light, and give the bricks the same drop shadow as the pegs.
6. [Minor] The ball at (487,352) is the same mean luminance as an unlit blue peg (about 147 vs 151 luma at 1x), and it is grey with no glow, so it is not the most readable small thing on the board. Fix: raise the ball's lit-side value and specular well above the pegs, keep the lower half dark, and add a thin dark contact edge.
7. [Minor] Channel balls (37, 188–318): each ball shows a hard horizontal seam at its equator, and stacked balls get no contact darkening where they touch. Fix: a soft horizon gradient (satin), plus a small occlusion shadow on top of each lower ball.
8. [Nit] The playfield darkens along the inside of the right rail (about x 700–725) as well as the top and left. A right-hand rail cannot cast a shadow onto the board under upper-left light. Fix: keep the inner shadow on the top and left edges only.
9. [Nit] Bucket A cradle (230–360, 568–588) is lit symmetrically with its highlight at the bottom centre. Fix: brighten the left horn and inner left face, and darken the right horn.
10. [Nit] The launcher's crescent gauge glass is a flat fill with no specular. Fix: one small soft upper-left glint along the arc. The L-shaped corner marks at (80,48) and (718,585) carry no meaning (noise glyphs).

ASSET 2: style-frame-b (.png / @2x) — CHANGES
1. [Major] The water strip (y 585–600) reflects nothing: no mirrored hull and no lantern reflection. Fix: mirror the hull about the waterline, darker and broken by ripples, and add a vertical warm glitter column directly under the lantern (x≈570), broken into horizontal dashes.
2. [Major] The lantern (570,540) lights nothing near it: the pole, the gunwale beside it and the stern are not warmed, and its halo is tiny. Fix: warm light falling off within about 1–2 lantern heights on the pole, the gunwale top near x 540–585 and the stern; nothing further away.
3. [Minor] The gunwale is one uniform bright gold stripe along the whole boat (455–585), including the far left, which the lantern cannot reach. It reads as brass trim rather than wood. Fix: a cool moonlit top edge, brightest on the upper-left, plus warm light only near the lantern.
4. [Minor] The hull sits on top of the water line with no contact (no waterline darkening, no ripple ring). Fix: sink the hull slightly, with a darker contact band and a few ripple lines.
5. [Nit] The hull is flat brown with a visible vertical band at mid-length (2x crop x≈340–360). Fix: smooth the plank gradient.
6. All peg, brick, ball and channel findings from Asset 1 (items 1–7) apply here too.

ASSET 3: peg-states (.png / @2x) — CHANGES
1. [Major] Seas. All 16 large reference pegs (column x≈213 to 435 at 1x, rows y 190/283/375/467) share one identical soft cloud-blob sea. It is a blurred single mass, not broken lobed maria, and it is the same on every colour and state. Fix as in Asset 1, item 1. This sheet is the reference, so it must show the variants.
2. [Major] The ball reference (90,1035) contradicts its own caption, "satin silver; the lower half reflects the dark board". It has a crisp horizontal step at the equator (two glued hemispheres) and a small pure-white disc highlight, which is chrome, not satin. Fix: a soft blurred horizon between the upper (sky) and lower (board) reflections, and a broad soft highlight on the upper left.
3. [Minor] On the unlit pegs the unlit sliver is about 0.12–0.15 r with a fairly hard edge. Fix: about 0.2 r with a soft terminator.
4. [Minor] Bricks (right panel) cast no drop shadow while the pegs do. Fix: add the same soft lower-right shadow.
5. [Minor] Free-ball crop: the "+1" at about (1085,828) is cut by the channel's brass rail. Fix: move it clear of the rail.
6. [Nit] The purple Clearing-1 bloom (about (430,470)) spills across the panel border at y≈525–560. Fix: clip it, or reduce its radius.
7. [Nit] Fever-1 panel: the last orange throws a warm pool on the board, but the blue pegs nearest it (about (230,830) and (358,830)) pick up no warmth. Fix: a faint warm tint on their facing sides, or drop the board pool.

ASSET 4: fever (.png / @2x) — CHANGES
1. [Minor] The "FULL MOON" scrim band (y≈205–265) darkens the pegs under it, at (100,245), (198,218), (283,207) and (541,217). They now read as unlit pegs, contradicting "all remaining pegs lit". Fix: draw the lit pegs above the scrim, or keep the scrim off the pegs.
2. [Minor] Lit pegs lose their gibbous terminator and seas and read as glowing pearls or snowballs. Fix: keep a faint lower-right sliver and the maria visible under the bloom.
3. [Minor] The five Fever cups (y≈570–585) are thin crescent blades with no bowl depth, inner wall or rim, so they read as smile marks rather than cups. Fix: show the far rim and a shadowed interior, with the highlight on the near rim's upper-left.
4. [Minor] The ball (451,469) is the dimmest object on a field of glowing pegs. Fix: see Asset 1, item 6.
5. [Nit] The value plaques float unattached above the cups. Fix: seat them on the cup rims or give them a contact shadow.

ASSET 5: readability (640x480 plus zooms) — CHANGES
1. [Minor] The caption's claim that the ball "stays the brightest small thing on the board" is not supported. At 0.8x the ball (408,340) is a grey dot no brighter than the unlit blue pegs and much dimmer than the lit pegs beside it. Fix as in Asset 1, item 6, then re-render.
2. [Minor] At 0.8x the drop shadow merges into the peg, so every peg reads as an egg tilted toward the lower right (whole board). Fix as in Asset 1, item 3.
3. [Minor] The "100" score pop at about (385,341) overlaps the edge of the lit blue peg. Fix: offset it clear of the struck peg.
4. [Nit] Faint whitish diagonal wisps in the sky (about (200–330, 230–330) in the 0.8x board) do not appear in style frame A. They are noise. Fix: remove them, or match frame A.

ASSET 6: characters (.png / @2x) — CHANGES
1. [Major] The rim light is an even outline stroke around the whole silhouette, on the right and lower edges as well as the upper left (Space Blast's right arm and right leg, Spooky Ball's right side of the cape, Zen Ball's right leg, Gajavati's right side). A moon at the upper left cannot rim the lower-right edges. Fix: rim only on the edges whose normals face the upper left, fading to nothing on the right and lower edges.
2. [Major] Flower Power: the lantern (2x about (960,1520)) has a halo, but the staff under it, Ottilie's right shoulder and hand, and the ground below get no warm light. Fix: warm falloff on the staff, the right edge of the robe and a small ground pool within about one figure-width.
3. [Minor] The other emissive props light nothing near them. Super Guide's globe does not light Pipiru's raised hand or head. Fireball's ember does not light the staff top or hat brim. Electrobolt's pom-pom does not light the moogle's head. Spooky Ball's ring does not light Ysolde's raised hand. Fix: a small local tint on the nearest surfaces only.
4. [Minor] Front-facing local colours read as front-lit on backlit figures: Pyramid's brass vanes (tan), Gyobo's drum and patch (mid brown), Electrobolt's pouch (brown disc), Gajavati's fans (purple). Fix: drop them to near silhouette value, with colour only in a thin upper-left rim.
5. [Minor] Sister Ottilie reads as headless (flat robe top), with a floating "C" crescent above it, and the crescent finial on her staff is a second "C". Both break the moon-as-glyph rule. Fix: give her a hooded head silhouette, and make the emblems small, solid, shaded crescents or drop them.
6. [Minor] Spooky Ball: the ring of moonlight has a filled, lighter inner disc, so it reads as a second full moon or an "O" glyph in the sky. Fix: an open ring with the sky visible through it, as a thin luminous band.
7. [Nit] Super Guide's globe orbit rings read as an atom symbol, which is a noise glyph. Fix: drop the rings, or render them as faint ellipses in perspective.
Cast-shadow direction (toward the viewer and to the right) is consistent: no finding.

ASSET 7: campaign-base + campaign-expansion (.png / @2x) — CHANGES
1. [Major] The moon (both tiles, 1x about (232,108)) is a flat cream disc laid over an offset blue disc. The "sliver" is a hard-edged crescent outside the lit disc, so the outer limb is not one circle (the union of two offset circles), and there is no soft terminator. Its single sea is a smooth horizontal lozenge across the middle. This is the coin/cheese read the owner forbids. Fix: one circular limb; inside it a soft elliptical terminator with the dark sliver on the side away from the sun; 4–6 broken lobed maria of varied value; slightly cooler, less yellow lit tone.
2. [Minor] Base: the "Moon Road" (665–835, 395–535) is one uniform pale value along its whole length, does not fade with distance, and is not warmed at its top end under the lantern (the warm pool at about (690,395) stops at the road edge). Fix: moonlit value that falls off with distance, and a warm pool at the post base that continues across the road.
3. [Minor] Base: the glowing gold crescent under the lantern (698,330) reads as a glowing "U" or smile glyph and a second moon motif. Fix: a dark brass bracket lit only on its top face by the lantern.
4. [Minor] Expansion: the traveller and the gunwale (500–670, 375–432) carry a uniform light outline on all sides, including the left/back side away from the lantern. Fix: a warm rim only on the faces turned to the lantern (right) and a faint cool rim at the upper left.
5. [Minor] Expansion: the moon glitter column (x≈230, y 325–540) is warm yellow-gold, close to the lantern's reflection hue, so the cool/warm temperature split is lost. Fix: cream-silver glints, desaturated and cooler than the lantern column at x≈680.
6. [Nit] Base: the figure (768,440) casts no shadow. Fix: a short soft shadow toward the viewer and to the right.
7. [Nit] Expansion: the treeline is a repeated sawtooth (600–950, 285–300) that reads as a pattern. Fix: vary the crown heights and spacing.
8. [Nit] Both skies carry fine horizontal streaks around the moon that read as scanlines at 1x. Fix: soften them or vary their angle.
Water reflections in the expansion (boat, figure, torii, lantern column) are physically placed: no finding. Shared identity (moon position, falling star, lockup) holds.

ASSET 8: art/moonfall-b-medallion.png + whatsnew-1.23.0.jpg — CHANGES
1. [Major] The moon (1x about (242,104)) has the same construction fault as Asset 7, item 1: a hard offset blue crescent outside a flat cream disc, a non-circular outer limb, no soft terminator, and one smooth lozenge sea. Fix as in Asset 7, item 1.
2. [Minor] The traveller and boat (655–825, 285–340) have a uniform warm outline on every edge, including the traveller's back and the left end of the hull far from the lantern (828,278). Fix: warm light only on the surfaces facing the lantern within about one boat-height; elsewhere a cool upper-left rim.
3. [Minor] The hull is a mid warm brown along its whole length. Its side faces the viewer with the moon behind and to the left, so it should be in shade; the left end cannot be lit by the lantern. Fix: darken the hull toward silhouette value, with warmth only at the bow under the lantern.
4. [Minor] The moon glints (x≈205–265, y 260–405) are more saturated yellow than the cream moon. Fix: match the moon's hue, slightly desaturated, and keep them cooler than the lantern glints at x≈830.
5. [Nit] The gold pole has no reflection beside the lantern column. Fix: a short wavy gold line under the bow. There is also a faint pale horizontal streak on the water right of the boat (about 830–900, y≈345) that no source explains. Fix: remove it.
6. [Nit] The JPEG shows mild blocking in the lantern halo and the dark water. Fix: export at slightly higher quality.
Placement (one moon high left, the boat right of centre, the falling star right that lights nothing) and the lantern reflection column are correct: no finding.

OVERALL: CHANGES on all 8 assets. No asset approved this round.

Top three fixes:
1. Rebuild the moon used in all three paintings (campaign-base, campaign-expansion, What's new): one circular limb, a soft internal terminator with the unlit sliver, broken lobed maria, a less yellow tone. The current offset-disc and lozenge-sea build is a coin/cheese read.
2. Replace the single stamped smudge sea on every peg with varied, crisp, lobed maria per peg, and widen the unlit sliver to about 0.2 r with a soft terminator. Separate the drop shadow so the pegs stop reading as tilted eggs.
3. Make the one warm light behave physically. The style-frame-B lantern needs water reflections (hull mirror plus glitter column) and must warm its pole, gunwale and stern. Across the character cards and the boat paintings, replace the uniform outline strokes with upper-left-only rims plus local lantern/emissive light on the nearest surfaces.
```

## Designer's response to round 1

Every finding is acted on, except where noted. Disagreements are recorded for the owner.

- **Pegs' "drop shadow" (A1.3, A5.2) and bricks' shadow (A1.5, A3.4).** No drop shadow is drawn. The dark lower-right band is the unlit sliver, painted in the sky's tone, and it made the disc's outline read as an egg. The fix is the supervisor's intent: a wider, softer terminator and a lifted earthshine, so the whole disc stays round. On shadows the designer disagrees: the board is open night air in front of a far sky, so no surface exists to take a cast shadow. The pegs and bricks are kept shadowless and consistent with each other.
- **Brick facets (A1.5).** The flat faces are lit by their world-space normal already, and a flat face turned to the viewer takes the same light anywhere on the arc. The bevels now shade more strongly by their own normals, so the side away from the light reads.
- **The JPEG's quality (A8.6).** Quality 88 at 4:4:4 is the owner's shipping rule (spec-1.22 W2). It is kept, and the blocking is reduced at the source by smoothing the dark water and the halo. The owner decides.
- **The Medallion corner marks (A1.10).** They are part of the Medallion frame kit in the plugin (the tooltips' corner marks), not decoration invented here. They are removed from the playfield anyway, to keep it calm, and the owner can restore them.

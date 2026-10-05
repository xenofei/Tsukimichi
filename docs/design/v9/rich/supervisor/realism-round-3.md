# Realism supervisor, round 3

The supervisor's round-3 report went to the coordinator, not to the designer. What follows is the coordinator's relay of it, with every finding and measurement as relayed. If the supervisor's own text is recovered, it replaces this relay. The designer's response is at the end.

## Verdict

13 of 21 assets are approved:
- the scenes base-p1, base-p3, exp-p1 and exp-p2, with their composites;
- the screens map, levels, fever, tally and pause.

8 assets need CHANGES: base-p2 and exp-p3 (scene and composite), title, characters, hud and buckets.png.

## Findings

1. **(Major) Grade regression.** The form light gives large pale areas a uniform lift and flattens them.
   - exp-p3's planet is now a flat lavender haze. The std in box (420,240)–(520,320) went from 0.075 to 0.030, and in box (450,330)–(560,420) from 0.065 to 0.033.
   - base-p2's sea of clouds is flattened: box (20,280)–(200,330) went from 0.064 to 0.051, and box (250,300)–(400,330) from 0.085 to 0.050.
   - Fix: limit the form light to shapes smaller than the 48-unit blur, or restore the mid-frequency band (σ about 2–12 px) at round-2 strength after the form light.
   - Target: the planet boxes at std 0.06 or more, and the cloud tops rolling again.
2. **(Minor) The exp-p3 sphere repaint.**
   - The disc is drawn over the cradle's front bracket, which ghosts through it at 2x ≈ (150–210, 670–700).
   - A second, lighter limb shows on the right at 2x ≈ x 240–255, y 600–720.
   - Fix: mask the repaint so the cradle stays in front, and make the disc opaque with a single limb.
3. **(Major) The title screen's moon** is a grey matte ball lit from outside.
   - At 1280 it is #505050, luma 0.32, dimmer than the clouds it lights. At 640 it is cream.
   - Fix: reuse the approved emissive moon from base-p3. It must be the brightest thing in the sky (luma 0.8 or more), nearly uniform with about 10% symmetric limb darkening, with soft irregular maria, a cool tint toward #C3CEE4 and an even halo. It must be identical at both sizes and antialiased.
4. **(Major) The cameos read as embossed silhouettes.**
   - Across Pipiru's face at y 290 the luma is a flat plateau of 0.771–0.776, and the features are minimal.
   - At 640 and on the 60–80 px stops they collapse to silhouettes.
   - Fix: shade a real height field with rounded forms: forehead, brow, eye socket and lid, cheekbone, nostril, lips, chin, ear, and the hair in locks. Light it from the upper left with soft shading. Keep the bevel only on the outer cut edge. They must read as finished carved portraits at every size where they appear.
5. **(Minor) The moonstone material** reads as matte porcelain or plaster.
   - Fix: add slight translucency at thin edges and a soft milky blue adularescent sheen across the high points, drifting toward the upper left. Lift the relief's cast shadow with cool scattered light.
6. **(Minor) Kupsa's head floats off its body** (1280 ≈ (595–705, 590–700)), and the wings come off the head.
   - Fix: join the head and body through a continuous neck or chest mass.
7. **(Major) The HUD's Super Guide line is physically impossible.** The barrel points down-left, but the line rises up and to the right with no bounce.
   - Fix: start at the muzzle along the barrel's axis and draw the real parabola, using the engine's guide maths if possible. If a curve to the right is wanted, rotate the barrel to that aim.
8. **(Minor) buckets.png:** in the cart panel, a blue peg touches the lit lantern at 1x ≈ (488–545, 88–150) but is darkest on that side. A second peg at ≈ (600–660, 68–125) has the same problem.
   - Fix: move both pegs clear of the lantern. Or add a faint warm spill on the side facing the lantern for pegs within about 20 px, and never let a peg overlap the lantern.

**Nits:**
- exp-p1: a little more left-to-right dome falloff (0.045 → about 0.06).
- The Fever cups: an upper-left rim highlight.
- levels-640: the "Play 4-3" button overlaps the 4-5 card.
- base-p1: the edge Nits carried over from earlier rounds.

# Moonfall runtime art: response to round 1

What changed for each finding. Screens re-rendered in `../screens/` (the plugin's own drawing code, headless).

## Majors
- **Cleared pegs leave dark holes (GD, UX M1; LD sockets and the base-04 rings).** The veil round a round peg is no
  longer baked into the scene. `MoonfallVeil.Sprite` (Core) is the same shape (1 − k · blur₆(smooth(18, 0, edge))) as
  a sprite; `RichVeil` (`Tsukimichi/Ui/MoonfallWindow.Scene.cs`) draws it under each live round peg at its place that
  frame (movers too), fading with the peg over its clear. Only the bricks' veil is baked. The `near` palette term on
  moon-road-night (teal rings round each peg) is gone. So nothing peg-shaped stays where a peg was, and the mover path
  no longer draws a bullseye.
- **base-04 plain and a copy of base-01 (GD).** lantern-night is now Kugane by night: the official painting
  (`-nowloading_base10`) night graded to a plum sky with a teal canal, two strings of paper lanterns on cords across the
  top (flickering), a moonlit haze from the upper right, stars over the roofs and mist on the water. Its own place, its
  own second jewel; F2–F6 green.
- **tally-640 at the board's scale (UX M2) and dead space (GD, UX m4).** The tally window now fits its rows (no empty
  band), is centred on the board, and in a small window is set at the approved 0.8 px a unit when the window has the
  room, not at the board's scale. The ACED plate is taller with an inset; the companion's caption is back at 640.
- **F6 not checked; ranges allow unreadable scenes (LD).** New test
  `MoonfallRuntimeArtTests.Every_peg_reads_against_its_scene_and_the_scene_keeps_under_its_ceiling`: each kind's face
  (80th-percentile luma of its unlit sprite) against the 90th percentile of the veiled scene 2–9 units round every place
  it can be dealt to (movers at 48 places, with their own veil), at 1× and 0.8×, at least 0.20; the veiled scene's p99
  at most 0.46; and no piece within a moon's radius + 18. Worst margins now: base-01 0.429, base-02 0.286, base-03
  0.309, base-04 0.289 (purple, the darkest face). Ceilings 0.216 / 0.458 / 0.457 / 0.410 (base-02 and base-03 had
  0.47–0.49 and now grade with `ceiling` 0.37 and 0.35). The build leaves out a moon any piece comes within r + 18 of.
  The loader now refuses an ungraded game painting, `ceiling` over 0.46, a `glow` over 0.2 and a `veil` under 0.15.
- **scene-recipe.md (LD).** F6 and the ceiling in the rules table, a "Fit the layout" section, the 640 render step,
  where the dropped count shows, lights' halo rule, "either order" for y and x, the moon keep-out, the route keep-out,
  the veil as sprites, the tighter ranges.
- **base-03's route (LD).** Dropped from the shipped recipe (it traced base-p1's own trail); `MoonfallDress.Route` now
  keeps 6 units off every piece (feathered to 9), as the compass rose does.

## Minors and Nits
- NEW BEST (and ACED) are revealed as the counting total passes the old best (the ace); at once under Reduce motion.
- "Balls left" in Jupiter with "11 × 10,000" beside it in AXIS (lining figures); "Super Guide ×0" now shows the power
  alone.
- The hard pill behind FULL MOON is gone; the plates' shadow is feathered at both ends; the ribbon's tails fade with
  Fever's plate.
- power-640: the ribbon is fitted between the name plate and the score plate, its tails shrinking, never crossing the
  level's name.
- Lit gems are a pale tint of the companion's colour at the top, not white.
- Margins: the backdrop now averages the rails as the eye does (lapis enamel and the gilt), so the margins read warm
  grey-blue: mean 0.20–0.28 across the 1280 margin against the design's 0.21–0.32 (was 0.04–0.17).
- The F5 backstop subtracts 0.6 of the light's halo (as the build places it); the motion tests hold stars at 10 units
  and lamps at 8 + 0.6 × halo.
- The moon's Fever glow stays inside the framing laid back over it (a seam showed at the front layer's edge).
- The one-time Peg marks hint wraps under the bar in a narrow window; rendered (`base-p1-hud-640-hint.jpg`).

## Not changed, and why
- **The toolbar at 640 (two rows, about 90 px):** the window's ImGui bar is outside this pass (the brief keeps the level
  picker and the bar as they are); the hint row shows only until it is answered.
- **Gem size floor at 640:** the rail is 42 units (about 26 px at 640); five gems at a legible size do not fit it.
- **base-02's arcs over the city (LD minor):** the crop is the approved base-p2 one; re-cropping for Crescent's layout is
  a level-authoring change for the next level pass (the doc's "Fit the layout" now says so).
- **base-01's missing forest layer (GD minor), base-03 sharing the chart with level 13 (GD nit):** level-authoring
  follow-ups for the agent that writes the levels.
- **Fallback tally without ACED/NEW BEST, "Quick Play" clipped in the fallback bar at 640 (UX m5):** the interim end
  screen and bar are the pre-existing ones; unchanged.
- **The flash at the green, Fever's dust burst, other motion:** not captured in stills; in-game check.

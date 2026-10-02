# Round 2 juror: colour-blind (deuteranope; also checks protanopia and tritanopia)

I judged only from rendered PNGs, never the SVG source. My simulations live in the session scratchpad (`r2-cvd/sim.py`):

- Deuteranopia and protanopia: Viénot 1999, in LMS.
- Tritanopia: the single-plane Viénot-style matrix barely moved the image, so I also checked against Machado 2009 (severity 1). The tritan notes below use the Machado renders.
- Greyscale: Rec. 709 luminance.

I ran these on `blind_16px`, `blind_48px`, `rows_mock`, `hero_compare` and `icons_compare`.

## Blind associations (deuteranopia view, written before opening the key)

| Concept | # | 16 px first read | 48 px first read | Key state |
|---|---|---|---|---|
| A | 1 | grey coin | faceted grey gem or coin | Completed |
| A | 2 | hamburger in a circle | moon behind fog | Not checked |
| A | 3 | no-entry sign (olive, slashed) | disc with a strip of tape | Locked out |
| A | 4 | check-circle | half moon with a check | Done this cycle |
| A | 5 | magnifier or lamp on a stand | gold-ringed moon above stepping stones | Ready |
| A | 6 | half disc with a bookmark | half moon with a bookmark ribbon | In journal |
| A | 7 | dim moon, thin rim | dark new moon with a thin crescent | Blocked |
| A | 8 | crescent moon | crescent with a crystal shard | Ready on another job |
| B | 1 | check-circle | clock dial with a check | Done this cycle |
| B | 2 | contrast toggle | half moon with a crystal | Ready on another job |
| B | 3 | Saturn or hamburger | planet with cloud streaks | Not checked |
| B | 4 | dark disc, thin rim | dark moon with a constellation | Blocked |
| B | 5 | no-entry slash | crescent with tape across it | Locked out |
| B | 6 | lightbulb | glowing moon above two pills | Ready |
| B | 7 | half disc with a flag | half moon with a bookmark | In journal |
| B | 8 | grey coin | gold-rimmed coin | Completed |
| C | 1 | no-entry sign | olive disc with tape | Locked out |
| C | 2 | lightbulb with bubbles | gold-ringed disc above cobbles | Ready |
| C | 3 | crescent moon | crescent moon | Blocked |
| C | 4 | half disc with a flag | half moon with a bookmark | In journal |
| C | 5 | grey coin | grey glass disc or coin | Completed |
| C | 6 | check-circle or pie | half moon with a check | Done this cycle |
| C | 7 | faint hamburger | dark disc with cloud streaks | Not checked |
| C | 8 | disc with a bright diagonal slash | slashed crescent with a crystal (comet) | Ready on another job |
| D | 1 | grey coin or button | coin with a faint sun or flower | Completed |
| D | 2 | radio button with a check | ringed pie with a check | Done this cycle |
| D | 3 | contrast toggle in a ring | ringed half moon with a crystal | Ready on another job |
| D | 4 | radio button | ringed crescent | Blocked |
| D | 5 | Saturn | ringed planet with streaks | Not checked |
| D | 6 | medal | gold medal: crescent over the sea | Ready |
| D | 7 | ringed half with a flag | ringed half moon with a bookmark | In journal |
| D | 8 | refresh or recycle spinner | olive ring around a cracked disc | Locked out |

## Confusable pairs per concept and CVD type

The measured weakest pairs come from each `concept.md`. My visual checks are from the simulated rows mock at 100% and 150% and from the blind sheets.

| Concept | Deuteranopia / protanopia (same result) | Tritanopia (Machado) | Greyscale |
|---|---|---|---|
| A | Ready on another job vs Blocked are both crescents on a dark disc and differ mainly in value (17.6 measured; fine in rows). Completed vs Blocked separate by value only. | Same pairs. Gold turns rose and olive turns red; nothing new merges. | Same. Done vs Completed is 20.2, and the check carries it. |
| B | **Ready on another job vs In journal**: both half discs lit on the right. They differ only by a 1–2 px crystal against a ribbon tab. They looked identical in the 100% rows ("A Hint of Things to Come" vs "A Bedtime Tale"). Measured 14.0. In journal vs Blocked is 16.4. | Same pairs, 13.9. | Same pairs, 13.9. |
| C | **Ready on another job vs Locked out**: both discs are cut by a bright diagonal band. At 16 px in greyscale the olive goes and the two read the same. Measured 14.9. Blocked vs Not checked are both dim and sink together on the daylight overlay (13.0). | Locked out turns red, which separates it from Ready on another job. Blocked vs Not checked stays the same. | Locked out vs Ready on another job is worst here, because the diagonal motif is shared. |
| D | **Ready on another job vs Blocked and vs In journal**: every glyph is the same ringed coin, so these differ only inside a ~10 px core. Measured 12.4 and 13.0. Locked out vs Not checked is 13.9. | Ready's gold bezel turns rose and still separates by value. Locked out turns red. | **Ready loses its gold bezel** and becomes one grey-ringed medallion among eight; only its interior separates it. Locked out loses its olive and becomes a grey ring with a crack, close to Blocked and Not checked. |

All four pass G1 numerically. Ranked by real-world risk for a colour-blind player: A (no pair needs hue) < B (one same-orientation pair) < C (shared diagonal) < D (one shared silhouette; Ready and Locked out lean on hue and saturation).

Every concept has two tritan side effects:
- Ready's gold reads rose-pink.
- Locked out's olive becomes a saturated red, which arguably helps.

Neither creates a merge. Separately, the installed-overlay green check in `icons_compare` becomes olive-yellow under deuteranopia. It then sits on top of the gold frame and is weak on B and C. That is a shared overlay problem, not a concept problem.

## Stock-icon failures (my blind reads, 16 px and 48 px)

A glyph counts as a stock-icon failure when I named a stock icon at either size.

| Concept | Stock reads | Moon phenomena at 48 px |
|---|---|---|
| A | check-circle (Done), no-entry (Locked out, 16 px only; tape at 48), hamburger (Not checked, 16 px only), coin (Completed) | 6 of 8 (Ready, Ready on another job, In journal, Blocked, Done, Not checked) |
| B | check-circle (Done), contrast toggle (Ready on another job), hamburger/Saturn (Not checked), coin (Completed), no-entry (Locked out, 16 px), lightbulb (Ready, 16 px) | 6 of 8 |
| C | no-entry (Locked out), check-circle/pie (Done), coin (Completed), hamburger (Not checked), lightbulb (Ready, 16 px) | 5 of 8 (Ready on another job read as a comet or slash first) |
| D | coin/button (Completed), radio + check (Done), contrast toggle (Ready on another job), radio button (Blocked), Saturn (Not checked), refresh spinner (Locked out) | 6 of 8 (Completed and Locked out are not moons) |

D has the most 16 px stock reads (6 of 8), because the ring around every glyph turns the set into a radio-button family. A has the fewest that survive to 48 px. Done reads as a check-circle in every concept; that is structural to "half moon + check".

## Scores (glyph, 1–10)

| | Identity | FFXIV | Craft | Legibility | Distinct | Meaning | Coherence | Taste |
|---|---|---|---|---|---|---|---|---|
| A aether-crystal | 7 | 7 | 7 | 8 | 8 | 7 | 8 | 6 |
| B astrologian-orrery | 6 | 7 | 6 | 6 | 6 | 6 | 6 | 5 |
| C ishgard-glass | 6 | 8 | 7 | 6 | 6 | 6 | 7 | 6 |
| D menphina-medallion | 6 | 6 | 7 | 6 | 5 | 6 | 7 | 7 |

**A, aether-crystal**
- **Identity 7:** Crescents, the gibbous and the half all read as moons; only Completed reads as a coin.
- **FFXIV 7:** The faceted crystal moons and crystal shard read as aether.
- **Craft 7:** The facets add detail at 48 px and drop out cleanly at 16 px.
- **Legibility 8:** In every simulation Ready is the only glyph whose silhouette breaks below the circle (the stones), so it pops without hue.
- **Distinct 8:** Its weakest pair is the best of the four (17.6), and none of its pairs depends on hue.
- **Meaning 7:** Check, ribbon, tape and fog all read without a legend.
- **Coherence 8:** Ready's crystal stepping stones are the icon's road in miniature.
- **Taste 6:** The Locked out tape reads as a bandage or caution tape. It is the only noisy, non-lunar mark.

**B, astrologian-orrery**
- **Identity 6:** Not checked reads as Saturn, and Completed reads as a gold-rimmed coin.
- **FFXIV 7:** The dial ticks and constellations feel like the Astrologian job.
- **Craft 6:** The tick ring becomes fuzzy noise at 16 px.
- **Legibility 6:** Ready on another job and In journal merged in the 100% rows.
- **Distinct 6:** 14.0 is above the gate, but the merge comes from orientation plus a tiny mark, which is the worst kind for me.
- **Meaning 6:** Without the dial, the crystal on the half moon means nothing to me.
- **Coherence 6:** The two pills under Ready echo the round-1 pill road.
- **Taste 5:** The ticks and pills are noise glyphs.

**C, ishgard-glass**
- **Identity 6:** The diagonal terminator reads as a slash before it reads as a phase.
- **FFXIV 8:** Stained-glass leading and the Ishgard link are the most "FFXIV place" of the four.
- **Craft 7:** The leading lines are a real material.
- **Legibility 6:** Blocked has salience 25, and it and Not checked vanish together on the daylight overlay.
- **Distinct 6:** Ready on another job, Locked out and Ready all share a diagonal; in greyscale the first two merge at 16 px.
- **Meaning 6:** Ready on another job reads as a comet.
- **Coherence 7:** Ready's pavers match the icon road.
- **Taste 6:** Static and tidy, but there are three slashes in one set.

**D, menphina-medallion**
- **Identity 6:** All eight are moons inside a ring, but at 16 px the ring makes them radio buttons.
- **FFXIV 6:** The medallion bevel reads as a generic medal more than Eorzea.
- **Craft 7:** The bevelled rims are the most finished metalwork.
- **Legibility 6:** In greyscale, Ready's identity is mostly its gold bezel, and that disappears.
- **Distinct 5:** It has the weakest pair (12.4) and one shared silhouette for all eight states.
- **Meaning 6:** The crack for Locked out is good; Completed's coin is not.
- **Coherence 7:** Ready is a miniature of the icon.
- **Taste 7:** The calmest, most static set.

## Scores (icon, 1–10)

| | Physics | Readability | FFXIV | Unity |
|---|---|---|---|---|
| A | 8 | 7 | 6 | 8 |
| B | 4 | 6 | 7 | 7 |
| C | 5 | 6 | 8 | 8 |
| D | 7 | 8 | 7 | 7 |

**A**
- **Physics 8:** The road starts as a mirrored crescent right under the lit limb, breaks into dimmer, greyer lozenges and is ragged rather than a pyramid.
- **Readability 7:** The crescent reads at 32 px. At 16 px the road becomes a fuzzy column.
- **FFXIV 6:** A crystal on an island and a gold frame. That is pleasant, but not specific.
- **Unity 8:** Same crystal moon and stones as the Ready glyph.

**B**
- **Physics 4:** The road is a stack of white brick pills as bright as the moon. That is a ziggurat or signal bars again, the round-1 failure.
- **Readability 6:** The round gold dial reads as a clock or compass at 32 px. The pink second moon becomes an olive dot under deuteranopia.
- **FFXIV 7:** The Astrologian dial and a red moon (Dalamud).
- **Unity 7:** The dial matches the glyph ticks.

**C**
- **Physics 5:** The moon has a gold rim, as though seen through a porthole. The hex-paver road is a widening triangle as bright as the moon.
- **Readability 6:** A disc plus a skyline. The skyline is mush at 32 px.
- **FFXIV 8:** The Ishgard silhouette is instantly FFXIV.
- **Unity 8:** It is the stained-glass Ready glyph, scaled up.

**D**
- **Physics 7:** The road sits under the lit crescent and is broken and dimmer. It tapers slightly toward the viewer. The lantern's warm reflection is tiny but present.
- **Readability 8:** The crescent is the strongest silhouette at 32 and 16 px, and the lantern is a value accent, not a hue cue.
- **FFXIV 7:** Kugane-style lantern plus an aether-crystal isle.
- **Unity 7:** The square icon does not match the medallion glyphs, but the Ready glyph contains the icon.

## Best glyph set: A (aether-crystal)

It is the only set where I never needed hue.
- Ready's stepping stones break the circle silhouette.
- Locked out's tape is a shape.
- Not checked is fog, Done is a check and In journal is a ribbon.
- The two crescents differ by value and by a crystal.

Its weakest pair (17.6 at 16 px) is far above the others in every CVD mode, and in the 100% rows I could sort every state by shape alone.

## Best icon: D (menphina-medallion), narrowly over A

For a colour-blind player the icon has to work by silhouette at 32 px under an overlay. D's big crescent does that best, and its road physics are nearly as good as A's. A has better unity with the winning glyphs. If A is picked for the glyphs, take A's icon but give its moon D's larger, cleaner crescent silhouette.

## What must change

**A**
- Replace the Locked out tape with a lunar lock that keeps a non-hue shape: an eclipse-dark disc with a bar notch, for example. It must still be distinct from Blocked's dark disc by shape, not by olive.
- Give Completed one distinguishing mark, such as a rim glint or a full-moon halo, so it stops reading as a coin.
- Make the icon road at 16 px fewer and larger lozenges.

**B**
- Mirror Ready on another job or In journal so the two are not both lit on the right.
- Grow the crystal to at least 2 px.
- Drop the tick ring below 24 px.
- Replace the brick-pill road in the icon and the two pills under Ready with broken, dimmer glints.
- Remove the gold rim on Completed.

**C**
- Break the diagonal motif. Give Ready on another job a curved terminator instead of a straight band, so it cannot match Locked out's diagonal tape.
- Raise Blocked's value; salience 25 is too dim on the daylight overlay.
- In the icon, remove the gold rim around the moon and make the paver road ragged, not a triangle.

**D**
- Drop or thin the outer ring below 24 px. It turns the whole set into radio buttons, and only the core carries state.
- Give Ready a silhouette cue that survives greyscale, such as the sea line or a glint breaking the ring.
- Make Locked out's crack thicker, in a darker value than the other rims, so it does not rely on olive.
- Give Completed a non-coin mark.

**All concepts**
- The installed-overlay check needs a dark outline. Under deuteranopia it turns olive and sits on the gold frame.

```json
{"juror":"colour-blind","glyph":{"A":{"identity":7,"ffxiv":7,"craft":7,"legibility":8,"distinct":8,"meaning":7,"coherence":8,"taste":6},"B":{"identity":6,"ffxiv":7,"craft":6,"legibility":6,"distinct":6,"meaning":6,"coherence":6,"taste":5},"C":{"identity":6,"ffxiv":8,"craft":7,"legibility":6,"distinct":6,"meaning":6,"coherence":7,"taste":6},"D":{"identity":6,"ffxiv":6,"craft":7,"legibility":6,"distinct":5,"meaning":6,"coherence":7,"taste":7}},"icon":{"A":{"physics":8,"readability":7,"ffxiv":6,"unity":8},"B":{"physics":4,"readability":6,"ffxiv":7,"unity":7},"C":{"physics":5,"readability":6,"ffxiv":8,"unity":8},"D":{"physics":7,"readability":8,"ffxiv":7,"unity":7}},"favourite_glyphs":"A","favourite_icon":"D"}
```

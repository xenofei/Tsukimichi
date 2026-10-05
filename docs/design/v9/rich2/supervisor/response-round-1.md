# Designer's response to round 1 (rich pass 2)

5 October 2026. Answers every Major and Minor in `game-designer-round-1.md` (GD), `ux-round-1.md` (UX) and `level-critic-round-1.md` (LC), and the cheap Nits. Round 2 is reviewed at the commit that adds this file.

## Shared changes

- **One progress state for every mock** (`src/r2state.py`; GD m3, UX M2):
  - The player is on The Moon Road, stage 3, with Cid (Brass Wings). Stages 1 and 2 are won.
  - The twins are face down, because Alisaie has not been met in the player's story.
  - Stages 4 to 11 are dimmed.
  - The base pilots are stage 3's levels.
- **Stages themed to their companion's home** (GD m2): Raubahn's stage is "The Sunlit Steps" (Ul'dah), and Kan-E-Senna's is "The Shroud by Night". The table is in `spec-rich2.md`, section 3.
- **Accents spread** (GD m1): the eleven accents are at least 25° apart in OKLab hue, none in the gilt band (60–100°).
  - Louisoix is now jade and the moogle sky blue.
  - Fireball's colour is amethyst. It is Y'shtola's colour, and the effect draws in it.
  - Power names on the rail sit on a dark plate.
- **The moogle crop** (GD m6): a per-card box `[14, 14, 180]` keeps the pom-pom and wings in the ring. The name is now "Moogle courier" everywhere (UX n1).
- **The twins' card era** (GD's unverified note): card 087059 is card No. 59. The plugin's own `CardEra` rule (`GiverPortraitSources.CardEraStarts`: No. 68 starts Heavensward) dates it to A Realm Reborn.

## Game designer

| # | Answer |
|---|---|
| M1 (colour) | Each board now pushes a second jewel into its large non-peg regions, with L kept:<br>• base-p1: teal seas;<br>• base-p2: a rose cloud sea;<br>• base-p3: emerald wood under an amethyst sky;<br>• exp-p1: a turquoise harbour, with gilt on the crowns;<br>• exp-p2: an aquamarine sea and aurora;<br>• exp-p3: the world keeps its own colours, with a magenta and teal nebula;<br>• the map: teal seas against sapphire land.<br>`readcheck.py` now measures F7 in 30° hue bins. The second jewel holds 17–43% of the coloured pixels on every board (the approved boards: 1–48%), and the mean chroma is 0.062–0.088 (approved: 0.046–0.068) |
| M2 (framing reads as walls) | Every flagged shape was replaced:<br>• the tracery and trefoils are gone (base-p2 has fir boughs now);<br>• base-p3's trunks sit mostly beyond the walls, with bark edges;<br>• exp-p1's column is at x 702, beyond every peg, and the balustrade is right of x 652;<br>• exp-p2's boom is now sailcloth festoons above the board;<br>• exp-p3's crystals are shorter, faceted, and placed clear;<br>• base-p1's neat-line is on the frame's own edge.<br>`framecheck.py` now refuses a board with:<br>• framing within 6 units of a piece (F3a);<br>• a rim-lit run over 30 units (F3b);<br>• a straight run of 36 units (F3c);<br>• a peg-sized disc (F3d). |
| M3 (not fuller) | Each board has a depth layer and a light event (`spec-rich2.md`, section 5):<br>• base-p1: the quill and inkwell, and the lamp's pool;<br>• exp-p1: laurel in both corners, the balustrade and its lantern;<br>• exp-p2: festoons, rigging and the stern lantern, under a brighter aurora;<br>• exp-p3: outcrops, rocks and crystals, with a nebula and earthlight.<br>Shafts are at F4's cap of 0.08 |
| M4 (Fever) | Fever is now a lighting change:<br>• the board's moon swells and brightens behind the leaves;<br>• the sky lifts toward the carrier's colour;<br>• moondust bursts once from the last orange;<br>• the cups light from within.<br>The banner is one ribbon, with the laurel framing the words, and the subtitle sits on the ribbon. There is a new `motion/fever.png` |
| M5 (tally) | LEVEL CLEAR is on the ribbon, as large as the level's name. ACED and NEW BEST form a gilt callout with a lit moon. The "·" glyph is fixed: "·" and "×" are always set in AXIS, and a missing glyph now stops the build |
| M6 (sealed tiles) | Sealed tiles are darkened and drained, with a padlock. The open tile has the selection glow in Cid's colour. At 640: one row of five, and Play in a selection strip |
| M7 (preview colours) | The previews are true colour; the peg hues are exact |
| M8 (no power moment) | New `screens/power-*.png`:<br>• Cid's card slides in beside the board, in the journal's frame;<br>• Brass Wings is named in his copper;<br>• the PvP emblem's gilt wings spread the cart;<br>• an accent ring marks the green;<br>• a LONG SHOT +25,000 style-shot ribbon sits in open sky. |
| m1 | See "Accents spread" above |
| m2 | See "Stages themed" above |
| m3 | See "One progress state" above |
| m4 | The selection glow is tinted in the companion's colour, with a softer rim |
| m5 | The characters detail shows the power at work: Super Guide's line running past the bounce on a real board, from the engine's flight |
| m6 | See "The moogle crop" above |
| m7 | The margins carry the level's scene, blurred. The floating scrolls are gone, and the rail is divided by the journal's rule as a shelf |
| m8 | See M2 (base-p1's neat-line) |
| m9 | The rigging is continuous, unlit rope through empty sky, at least 6 units from every piece |
| m10 | base-p3's willow fronds are now oak leaves |
| m11 | The preview shows the whole title. The type mask covers the logotype, the subtitle, the buttons and the Continue card |
| m12 | The beams are at the F4 cap and breathe ±15%. There are 14 fireflies with soft halos |
| m13 | The pause window is taller, and the note sits clear of the corners |
| m14 | See M4 (one continuous ribbon) |
| n1 | The crest's wings tuck under a larger centre ring |
| n2 | Storm Post reads "The Far Shore" everywhere. The exp-p3 HUD still says 12-5 (The Far Shore has twelve stages) |
| n3 | The Continue card is the default focus: larger, glowing in Cid's colour, with Cid's portrait and power and a "Continue 3-3" button |

## UX/UI specialist

| # | Answer |
|---|---|
| M1 | The Continue card's title is fitted to its column. The card is larger |
| M2 | The four stop states are now distinct:<br>• locked: the portrait drained to about 40%, the ring dimmed, a padlock;<br>• won: the moon pip;<br>• here: the glow in the carrier's colour;<br>• not met: the card back.<br>There is a legend and a focus tooltip. Stage 5 is locked everywhere |
| M3 | See GD M6 |
| M4 | The 640 level select is reflowed: one row, with Play in a strip |
| M5 | Text floors are enforced by the build (labels at least 7 px cap, numbers at least 8 px). Where a label cannot meet the floor at 640 it is left out: SCORE, BALLS, ORANGES and the rail's power name. The dial shows a real AXIS "×" with TrumpGothic digits. The cup values are now 18-unit TrumpGothic on larger plates |
| M6 | The laurel frames the words and never crosses them |
| M7 | See GD M5 |
| M8 | See GD M5 |
| M9 | The pause window is 690 tall, and the note sits 46 units above the bottom |
| M10 | Fireflies are placed so that their whole wander and halo keep 8 units from pieces. The preview measures a maximum lift of 0.008 within 8 units of any peg (rule: 0.03) |
| m1 | The plain gilt band is now one seamless image with mitred corners |
| m2 | The pill is assembled whole before resampling |
| m3 | See GD m7 |
| m4 | One fitting rule for the power name |
| m5 | The rings' backing plates are cut away by radius |
| m6 | Switches fill with the accent and show On/Off. Decoration and Sound have ‹ › steppers |
| m7 | "Leave to the map" is a garnet danger pill, held to confirm |
| m8 | The Continue card is the default focus |
| m9 | Text is composed at the atlas's resolution with exact advances, and resampled once |
| m10 | Secondary text is lifted to INK2 (#C3CBEA, at least 4.5:1 on every panel ground) |
| m11 | The face-down card shows its power. Back is top-left everywhere |
| m12 | The portrait is inset |
| m13 | The colour-blind assist is "Peg marks": a crescent, a leaf or a star on each kind (`screens/pegmarks.png`, with a deuteranope simulation). base-p1 uses the cooler chart (owner question 4 is answered) |
| m14 | The lantern flicker is anchored on the cart's lantern. The medallion glow is rendered |
| m15 | The stops are respaced. Numbers sit on their own plates. The tooltip covers the face-down stop |
| m16 | Secondary buttons use AXIS; Jupiter is kept for primary buttons |
| n1 | See "The moogle crop" above |
| n2 | Panel titles are fitted to their column |
| n3 | The accents are lighter (see "Accents spread") |
| n4 | The Fever line sits on the ribbon |

## Level-design critic

| # | Answer |
|---|---|
| base-p1, purple's worst drop | The lamp glow is lowered to 0.05. F6 is now measured at every kind's worst placement: the largest drop on base-p1 is 0.016 (rule 0.02) |
| base-p1, neat-line | It is on the frame's edge, with an inset of 2 units, and out of x 315–485 |
| base-p2, arch and trefoils | Removed |
| base-p3, Major (trunks) | The trunks sit mostly beyond the walls, with a smallest clearance of 9.1 units. The branch is gone |
| base-p3, pom-pom fronds | The right-hand foliage now keeps clear: each leaf is checked, and none comes within 6 units |
| base-p3, fireflies in motion | Whole path and halo at least 8 units from pieces. The measured lift is 0.008 |
| exp-p1, Major (column and rail) | The column is at x 702 and the balustrade right of x 652. Both clear every piece by 9.0 units |
| exp-p1, laurel tip | Every leaf is checked for clearance |
| exp-p2, rigging and boom | The rigging is continuous, unlit rope at least 6 units clear. The boom is replaced by sailcloth festoons |
| exp-p3, Major (crystals) | The crystals are shorter and faceted, placed only where they clear by at least 6 units. The outcrops settle until they clear. The smallest clearance is 8.1 units |
| F3a, F3b, F3c, F3d (rules not enforceable) | Added to `src/framecheck.py` exactly as proposed. `dress2.py` refuses any board that fails |
| F6 (checker gaps) | `readcheck.py` now checks:<br>• movers along their whole path;<br>• bricks;<br>• each kind at its worst eligible place;<br>• both scales;<br>• the drop against the approved board.<br>It refuses any failure |
| F5 and F8 (stills only) | F5 now covers motion paths, and F8 is a measured backstop |
| F1 (not checkable on output) | F1 now applies to the jewel step alone, and light counts against F6 |
| Nit (base-p2 purple brick) | `readcheck` now checks bricks |

# Moonfall screens: response to round 3

**Round 3 verdicts:**
- Level critic: APPROVE, with Minors.
- Game designer: REVISE (M1, M2).
- UX/UI: REVISE (M4, the same issue as the game designer's M1).

The owner ruled on M2: **step over it.** All findings are answered below. The renders in `../renders/` are re-made from this state.

**New renders:**
- `title-road-waits-*`: the Far Shore walked at Shadowbringers; the road waits at stage 9.
- `far-stepover-*`: walked at A Realm Reborn, with stage 3 selected.
- `title-arr-*`: the title for a story still in A Realm Reborn.
- `scene-veiled-*` and `scene-shown-*` at both sizes, now rendered at the same moment.

**New renderer flags** let the reviewers stage these states:
- `--far-built`: the Far Shore's 60 levels stand in.
- `--far-walk`: walks the road as Continue leads.
- `--far-won N`
- `--story N`: also meets everyone the shield places in A Realm Reborn.

## M2: one veiled stage walled off every stage after it (owner's ruling: step over it)
`MoonfallModes.Frontier` is the first level that is neither won nor on a veiled stage. A level is Open at or before the frontier.
- A veiled stage waits, unwon. Its stop has the shield's mark. While the road has not reached it, it also has the padlock (`MoonfallStageView.Reached`).
- "All won" and the Far Shore's count still need it: `ExpansionCleared` stays a run of consecutive wins.
- The tally's Next steps over it too (`NextLevel`).

**Test:** `Adventure_steps_over_a_veiled_stage_so_every_stage_the_story_allows_is_reached_at_each_era` walks the road at every era with nothing revealed. It asserts that:
- every stage not set past the story is won, including the moogle's Storm Post from Heavensward through Shadowbringers;
- every veiled stage is unwon;
- the Far Shore is complete only when nothing is veiled;
- the road waits at the first veiled stage.

## M1 / M4: Continue at a veiled frontier
`MoonfallModes.Next()` reports where Adventure goes: a level to play (stepping over veils), or, when nothing else is left, the first veiled stage it stepped over (`MoonfallNext.Veiled`).

**Title**
- **1280:**
  - The Continue card shows CONTINUE, the shield's mark and the stage's placeholder in Axis, so its digits read as figures.
  - The placeholder has the shield's hover and its "Reveal this name".
  - Below it is a line: "The road waits at a stage set past your story. It opens when your story gets there, or reveal its place on the map for this session."
  - The button, "Adventure map", opens the Far Shore on that stage.
- **640:** "The road waits past your story: Endwalker area 1", with the placeholder's hover and reveal.

**Adventure sub-line.** It follows the real place, a veiled one included ("The Far Shore · stage 9 of 12"). Once The Moon Road is won it stays on the Far Shore.

**Map.** "Here" marks the veiled stop the road waits at, unlit, and the map opens on it.

**Tally**
- After a win with no Next because of the veil, it says "Stage 12 is set past your story: Map opens on it.", and Map selects that stage.
- When the next stage is veiled but a later one is open, Next steps over it.

**Tests**
- `Continue_points_at_the_next_playable_stage_past_a_veiled_one_and_the_tally_steps_over_it`: at Stormblood, Continue goes from stage 6 to stage 8, the veiled stage 7 shows no padlock, and stage 9 (veiled and not yet reached) shows both marks.
- `A_veiled_stage_at_the_end_of_the_road_is_what_continue_reports_not_road_goes_on`.

## Game designer, round 3
| Finding | Fix |
|---|---|
| m1, veiled stop looked more open | `StoryVeil` puts a cool slate veil (slate, then indigo wash) over a veiled ring's face on the map and in the panel's face. A veiled face has no glow. |
| m2, copy | Accurate copy. Reached: "Set past your story. Right-click its name, or press its button, to reveal the place for this session." Not reached: "Set past your story, and not reached yet: win the stages before it too. Right-click its name to reveal the place for this session." When both hold, the padlock shows beside the mark. |
| m3, reason only on hover | At 1280 the panel prints the line and shows the five codes on one row, with no repeated "Past your story" rows. The 640 line reads "Right-click its name to reveal it", and the Play pill reveals on a press. |
| m4, challenges | A challenge has its own line: "Runs through stage N, set past your story. Press Play to open it on the map, where its place can be revealed." The same line is the pill's tooltip, and pressing the pill opens the map on that stage. |
| m5, scenes | On an open stage, a level whose scene's own place is hidden draws the recipe over its declared story-safe fallback picture (`MoonfallSceneHide.Fallback`; `scene-veiled-*` shows base-04's lanterns over the Moon Road sky). A veiled stage keeps the bare sky. A test holds that every veilable recipe declares a placeless fallback. |
| m5, title backdrop | Sohm Al is tagged "The Dravanian Forelands" (Heavensward), and the game-data test checks the tag. While it is hidden, the title is drawn over the chart (`title-arr-*`). The backdrop policy is written into `moonfall-modes.md`. |
| m6, plain bar | Whole parts in priority order until one does not fit. In a duel the order is turn, Balls, Oranges, multiplier, code, level name; in a level, code and name lead. Nothing is cut mid-word. |
| m7, placeholders outside `ShieldPlaceholder` | Every placeholder that is drawn goes through `ShieldPlaceholder` (the stage panels, the title). The others drop the name: a veiled stop's hover reads "Stage 9 · past your story · Fireball"; the companions' line reads "Joins on The Far Shore, stage 11 (past your story)". |

**Nits**
- **n1:** placeholders are set in Axis.
- **n2:** at 640 the shield's mark is at least 7 units: an almond and a slash at 1.5 px, with no pupil or dark cut.
- **n3:** `FitLine` never ends a cut on an article or small linking word ("Above…"). The font has no "…", so "..." stays.
- **n4:** `--story` meets everyone.
- **n5:** the legend's shield row shows only when a stop is veiled.
- **n6:** frontier tests are added.

## UX/UI, round 3
| Finding | Fix |
|---|---|
| m18, reveal is mouse-only | Added `ShieldText.RequestMenu(kind, name, shown)` (additive). Pressing the Veiled Play pill with mouse, keyboard or gamepad opens the shield's own "Reveal this name · this session" menu. The pill's tooltip reads "Set past your story. Press to reveal its place for this session." |
| m19, merged tooltip at 640 | The 640 head's fill and "See the stage's five levels" tooltip show only for Open or Done stages. |
| m20, 1280 stop tooltip and characters | The 1280 stop tooltip leaves the name out and is drawn in `Ink2`. The characters line drops the name. |
| m21, challenge line and pill tooltip | See the game designer's m4. |
| m22, 640 shield mark | See the game designer's n2. |
| m23, plain bar | See the game designer's m6. |

**Nits**
- The repeated "Past your story" rows are gone.
- The cut never ends on "the".
- Duel's second locked reason is a sentence: "Meet a companion in Adventure first: a duel is played against one."

## Level critic, round 3
| Finding | Fix |
|---|---|
| Plain bar at 640, opponent's turn | "Balls N" is second, right after the turn. |
| Thumbnail race | `Thumbs` records the recipe each build was made with. A finished build whose recipe differs from today's pick is never landed and is built again. `VeilChanged` drops landed thumbnails the same way. |
| Plain bar text-check | Its parts and scores go through the text-check sink. |
| Reproducible staged renders | `Settle` holds the board's clock (`HoldBoardForRender`). `scene-veiled` and `scene-shown` are now the same moment (5,760 and 9 balls). |

## Gates
All four pass at the commit that carries this file; see the hand-back.

# Moonfall screens: game designer supervision, round 1

Reviewed: spec-rich2 §1–4, characters.md, moonfall-modes.md, the owner's answers (feature-plan-v9), the nine mock pairs, and all 26 render JPEGs. I compared each render with its mock side by side and cropped at 2–10× (crops are in my scratchpad `c/`). I built the renderer and re-rendered three screens: the title with `--hint` at both sizes, the options with `--hint`, and the duel HUD with `--reduce-motion`. I read MoonfallScreens.cs, the picker, challenge and duel-plate code in MoonfallWindow.Modes.cs, PowerAtWork, PegMarksHint, the Far Shore sealing in Map.cs, and the renderer's staging (`StageDuel`, the tally's Ace).

## Summary
The mocked screens are faithful at both sizes. The title, map, level select, pause and tally match their mocks almost element for element. The round-1 runtime findings are closed:
- the 640 toolbar is gone;
- the companion is back on the 640 tally;
- the Brass Wings gems are lit in copper.

The spoiler states read correctly everywhere:
- the twins are the card back with "Not yet met" and Multiball named;
- met-but-not-reached companions are drained, with "stage N" and a padlock;
- the moogle stays face up.

Hold-to-confirm and the mode-aware Leave labels are in place.

There is one Major: the duel HUD does not make whose turn it is clear, and it shows no "thinking" signal. The rest are fidelity and polish Minors.

## Verdicts
| Screen (1280 / 640) | Verdict |
|---|---|
| title | APPROVE |
| map | APPROVE |
| far | APPROVE |
| levels | APPROVE |
| characters | APPROVE |
| quickplay | APPROVE |
| challenges | APPROVE |
| duel (setup) | APPROVE |
| options | APPROVE |
| hud | APPROVE |
| pause | APPROVE |
| tally | APPROVE |
| duelhud | **REVISE** |
| **Overall** | **REVISE** (one Major) |

## Findings
- **[Major] duelhud, both sizes, top rail: the turn and the opponent's thinking are not readable.**
  - The opponent's turn shows only a faint jade halo round its plate and a jade name.
  - The three thinking dots (`DuelPlates`, at y 38.5 under the score plate) are absent from the render. They are also absent from my `--reduce-motion` re-render, where their alpha is fixed at 0.85 (crops `duel-dots2.png`, `duel-rm-top.png`).
  - At 640 both plates lose their names, so only the halo separates the two sides.
  - Fix:
    - draw the dots above the frame band, or inside the plate beside the score;
    - add a turn caption in Jupiter under the active plate ("Louisoix is aiming…" / "Your shot");
    - dim the idle plate to about 60%.
- **[Minor] duelhud, left rail:** the tube shows 4 balls (the player's) while the right rail shows Louisoix and Sage's Path. Fix: show each side's balls left on its own plate, or switch both rails together.
- **[Minor] characters-1280, detail inset (about 1000,520):**
  - The "power at work" is the whole board with a ring on a green peg. It does not show the Super Guide line, and every companion would get the same picture.
  - At 640 the inset is missing, which leaves about 90 px empty above Play.
  - Fix: draw the power's own effect (guide line, wings, second ball…) on a crop round the green, at both sizes.
- **[Minor] characters-640, row 3 captions:** "Sage's Path · 10" and "Storm Post · FS" run together into one line (`char640-row3.png`). Fix: shorten them or clamp each to its column.
- **[Minor] characters, levels, both sizes: the selection glow is a flat, hard-edged slab** (teal behind Minfilia, orange under 3-3; `char-sel-cmp.png`). The spec asks for the soft `TripleTriadCardSelect` glow. Fix: use the texture or a feathered falloff.
- **[Minor] levels, both sizes:**
  - The "3-1" codes and the "ACED" tag touch the label plate's frame (`levels-tiles.png`).
  - At 640 the two-line names crowd the Best line.
  - Fix: add 6–8 px of inner padding, and shrink names to fit or use one line at 640.
- **[Minor] levels-1280, selection window:** the level's description is replaced by the power text ("He bolts airship wings…"), so "He" has no antecedent, and the stage blurb is dropped. Fix: restore the level's line and keep the power text with Cid.
- **[Minor] map, stage panel:** an aced level (3-2) looks the same as a won one (3-1); level select and Quick Play say ACED (`map-rows.png`). Fix: show the ACED tag or a brighter moon.
- **[Minor] challenges, both sizes:**
  - The last row ("The Archon's Path") sits under the lower-left corner ornament (`chal-bottom.png`).
  - The list cannot scroll, but the title mock plans 40 challenges.
  - Fix: inset the list and add paging as in Quick Play.
- **[Minor] challenges: the default "none" pick shows no selection and no companion line.** The 1.45r selection ring is not visible on any picker (`sel-compare.png`). Fix: show "No companion: no power" as Quick Play does, and draw the ring after the medallion.
- **[Minor] duel setup:**
  - Your companion defaults to none against a powered opponent. Fix: preselect the current Adventure companion.
  - The "1-1" code is set in Jupiter old-style figures and reads "I-I" (`duel-level.png`). Fix: use TrumpGothic, per the spec.
- **[Minor] quickplay/characters copy:** "any companion you have met" is wrong, because met-but-not-reached companions are locked. Fix: say "reached".
- **[Minor] title-640:** the sealed Challenges pill drops its reason. Fix: add a tooltip or a short caption.
- **[Minor] title-640, first-run hint (`--hint`):** "…colour-blind play." runs past the plate's gilt edge and up to the Companions button (`hint640.png`). Fix: widen the plate or wrap the text.
- **[Minor] options, Peg marks samples:** with marks off, the four pegs show no marks (`opt-swatch.png`), so the preview does not show what the switch does. Fix: always draw the marks in the sample.
- **[Minor] far:** in an open Far Shore whose levels are not shipped yet, the panel's button says "Not reached" and stop 1 is padlocked. Fix: say "Levels on their way" and drop the padlock.
- **[Minor] hud:** the Ace target is not visible during play. Fix: add a small Ace tick or label on the score plate.
- **[Nit] hud:** the margins are still a dark gradient, not the blurred scene (carried over from round 1).
- **[Nit] tally:** the HUD score (136,600) differs from TOTAL (161,600) by the Ace bonus.
- **[Nit] title-1280:** the hint plate is a plain dark bar; use the journal frame.
- **[Nit] duel setup:** the right half beside the opponent's name is empty.
- **[Nit] challenges:** "Try again" on a won challenge; "Play again" fits better.

## Flow
The flow is sound and consistent:
- **Title:** Continue goes straight to play.
- **Adventure:** goes to the map. From there, either Play 3-3 or the level select leads to play.
- **Pause:** opens from the crest or Esc. Restart and Leave are held. Options returns to the paused board.
- **Tally:** offers Replay, Map, or Next.
- **Other modes:** Quick Play, Challenges and Duel each return to their own setup screen.
- **Companions:** "Play with X" opens Quick Play with X picked.
- **Sealing:** Challenges are sealed both on the title and in the flow (`CanOpen`). The Far Shore tab is sealed with a tooltip.

Leave from an Adventure level goes to the map, not to the level select. That is acceptable, because the map panel lists the stage's levels. The crest is the only on-screen pause control; give it a hover glow and tooltip.

## Far Shore names
The set works well: each name mirrors its companion's base stage and the expansion's "sea and sky" road. The three approved pilot names fit naturally.

Strongest names:
- The Twin Lights
- The Skyward Deck
- The Admiral's Sea
- The Archon's Crossing
- The Courier's Wake

Suggested changes:
- "The Night Market Boats" is clunky; "The Floating Market" is better.
- "Lantern" repeats (Lantern Quay, Market Lanterns, Seven Lanterns); "The Harbour Quay" or "The Waking Tide" would avoid it.

Spoiler question for the owner: "The Domes of Sharlayan" and "The Sea of Sorrows" name Endwalker places. Moonfall progress does not follow the story, so a player still in A Realm Reborn could see them once The Moon Road is won. Either accept this (decision 20 already sends the expansion to the moon), or mask these two names behind the shield's place rule.

## Unverified
- The hold fill sweep (no hold is staged).
- The map's focus tooltip on the face-down stop (no focus is staged).
- The real Ace numbers on the tally (the renderer stages the Ace at 100,000).
- Whether the thinking dots are hidden or not drawn at all.
- All motion, in-game font metrics, Dalamud rendering and other window scales.

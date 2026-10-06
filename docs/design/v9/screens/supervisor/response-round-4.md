# Moonfall screens: response to round 4

**Round 4 verdicts**
- Game designer: REVISE (M3, M4).
- UX/UI: APPROVE, with m24–m26.
- Level critic: APPROVE, with one Minor (the tally's veil note was never drawn).

Each finding is answered below. Two of the game designer's Minors are the owner's to decide and are left as they are. The renders in `../renders/` are made again from this state.

**New renders**
- `tally-road-waits-*` and `tally-road-waits-plain-*`: the tally's note after winning the last level before the road waits.
- `far-veiled-not-reached-*`: a veiled stage the road has not reached. It shows the padlock and the "Not reached" pill.

**New renderer flag**
- `--leave-one`: used with `--far-walk`. It leaves the walk's last level unwon, so `--screen tally` wins it and the tally reaches the point where the road waits.

## Game designer, round 4

**[Major] M3. The companion follows its stage.**
- `MoonfallModes.CompanionState` now asks `CompanionReached`. A companion is reached when:
  - its stage is open by the road's own rule (the frontier is at or past it, or a level of it is won or opened);
  - and its stage is not itself veiled.
- `QuickPlayCompanions` goes through the same rule, so the Companions screen, Quick Play, duels and challenge picks all agree.
- The walk-the-road theory now asserts, from Heavensward to Shadowbringers:
  - `CompanionState(Moogle) == Available`;
  - the moogle is in `QuickPlayCompanions()`;
  - `QuickPlay("expansion-51", Moogle)` is not null.
- At every era it also asserts that every Far Shore companion is reached exactly when its stage is won.

**[Major] M4. Unbuilt is never "past your story".**
- `Next()` reports a waiting stage only when that stage is veiled, lies before the frontier, and has levels built (`StageBuilt`). Otherwise, when the frontier's level is Missing, it reports nothing ("The road goes on").
- `NextLevel` names a stepped-over stage only if it is built. When the walk stops on a Missing level it names none.
- The map's `Coming` is measured from `Frontier`.
- The three tests asked for:
  - `Nothing_built_yet_is_never_called_past_the_story`: the Moon Road won, nothing built, A Realm Reborn → `Next()` is null.
  - `A_partly_built_far_shore_waits_on_its_unbuilt_stages_as_coming_not_veiled`: Heavensward with stages 1–3 built → `Next()` is null, stage 5 is Coming, and the tally names no veil.
  - `A_built_veiled_stage_before_the_frontier_is_where_the_road_waits`: Stormblood with stages 1–8 built → `Next()` is Veiled at stage 7.

**Minors**

| Finding | Fix |
|---|---|
| m8, re-closing | Winning a level opens the next, for good. A level whose predecessor in road order is won stays Open, whatever the frontier says now. Test: `Advancing_the_story_or_revealing_never_closes_a_level_already_opened` walks Heavensward to 11-3, then switches to Stormblood. 11-3 and 4-1 are both Open, and the moogle stays Available. |
| m9, title and tally disagreed | The tally takes its waiting stage from `modes.Next()`, the same as the title and the map's "here" (`tally-road-waits-*` names stage 9, as `title-road-waits-*` does). |
| m10, reveal offered where it opens nothing | A veiled stage the road has not reached shows the padlock's "Not reached" pill with its tooltip. The reveal stays on the name's right-click, and the panel says a reveal won't open it yet. |
| m11, duel copy | "Meet a companion in your story first: a duel is played against one." |
| m12, retag stage 11 | **Owner's call, pending.** Not changed: stage 11 stays The Churning Mists (Heavensward). |
| m13, title backdrop at A Realm Reborn | **Owner's call, pending.** Not changed: the chart stands in. |

**Nits**
- **n7:** the card's caption reads "CONTINUE · STAGE 9", and its line reads "It opens when your story gets there. To play it now, reveal its place on the map (for this session)."
- **n8:** a veiled "here" gets a faint slate ring.
- **n9:** the reached veiled pill reads "Reveal its place".
- **n10:** `moonfall-modes.md` §2 "Unlocks" is rewritten around the frontier, step-over, open-for-good, companion reach and the built-aware Next.

## UX/UI, round 4

| Finding | Fix |
|---|---|
| m24, Esc or B also went back | `HandleKeys` returns before Back and Start when a popup was open at the end of the last frame (`popupWasOpen`, set after `ShieldText.DrawMenu`) or is open now. Lint: `The_key_that_closes_a_popup_is_not_also_back`. |
| m25, Back over the tally ignored the waiting stage | The waiting stage is selected in `EndBoard`, so the tally's Map and Esc/B both open the map on it. |
| m26, the stop tooltip overflowed | The stop tooltip has short reasons of its own: "Past your story: select it to reveal its place" and "Past your story, and not reached yet". |

**Nits**
- The remedy copy now names the button ("press Reveal its place"; the challenges' "Press Past your story").
- A Veiled pill gets the hover glow.
- The 640 head reveals on a reached veiled stage, and is inert otherwise.
- The road-waits card wraps wider, so there is no orphaned word.
- `challengeVeil` falls back to the plain "Runs past your story."
- The `FitLine` doc comment is fixed.
- New lints:
  - a Veiled press reaches `RequestMenu`;
  - the head's `headOpens` gate;
  - the plain bar's whole parts;
  - m24's guard.
- **Not changed:**
  - the 640 road-waits mark (optional);
  - the Axis placeholder size (accepted);
  - the flat selected-row band (carried over).

## Level critic, round 4

| Finding | Fix |
|---|---|
| Minor: tally veil note never drawn | On a win whose note says why there is no Next (the road waits past the story, or this was the last level built), the rich tally draws the note in place of the sub-line. The plain tally adds a line for it and grows to fit. Staged by `--far-built --far-walk --leave-one --story 3 --screen tally` (`tally-road-waits-*`, `tally-road-waits-plain-*`). |
| Nit: thumbnail race untested | `A_thumbnail_the_veil_overtakes_mid_build_is_never_landed_and_is_rebuilt_story_safe`. The Kugane read is held on a gate while the veil falls. One thumbnail lands, and it is built from the fallback picture. |
| Nit: per-frame re-pick on the failure path | A finished build is checked against the veil once (`thumbChecked`), and again only after `VeilChanged`. It is never re-picked each frame. |
| Nit: `SafeRecipe` keeps the dress | `scene-recipe.md` § Fallbacks now says that a hidden scene keeps its whole dress (paint, framing, lights, palette regions, motion), so a hideable recipe's dress must name no place. |
| Nit: plain solo bar | In a level, a name that doesn't fit gives way, and the game state after it still shows. |
| Nit: text sink | The solo score and the Pause label now go through the sink as well. |

## Gates
All four pass: build (`-warnaserror`), LoadCheck, tests (8863 passed with `Category!=Perf`), and the themes check.

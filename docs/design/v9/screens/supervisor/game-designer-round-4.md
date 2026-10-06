# Moonfall screens: game designer supervision, round 4

Before reviewing I read four things: my round-3 review, the implementer's round-3 response, the level-critic and UX round-3 reviews, and the Far Shore section of `moonfall-modes.md` (l.55–85). I looked at the committed renders at e0db5d19, and at the new ones closely: `title-road-waits-*`, `far-stepover-*`, `title-arr-*`, `scene-veiled-*` and `scene-shown-*`, at both sizes.

In code I read:
- `MoonfallModes.cs`: `Frontier`, `StageReached`, `NextLevel`, `Slot`, `Stages`, `Continue`, `Next`, `SceneHide`, `FinishLevel`, the challenge state, and the duels;
- `MoonfallCompanions.Reached` and `State`; `MoonfallLooks.Stop` and `Coming`; `MoonfallPlaces.OfBackdrop`; `MoonfallShield`;
- `MoonfallGameArt.cs`: `PickScene`, `SafeRecipe`, `VeilChanged`, the thumbnail landing;
- `MoonfallWindow.Title.cs`: the veiled Continue card, `Continue`, `DuelLockedWhy`;
- `MoonfallWindow.Map.cs`: `StoryVeil`, the panels, `PlayOrReveal`, `PanelPlayTip`, the map words;
- `MoonfallWindow.Flow.cs` and `MoonfallWindow.Tally.cs`: `AdventureNext`, `HasNext`, `Next`, `LeaveBoard`, the tally's veil line;
- `MoonfallWindow.Modes.cs` (veiled challenges), `MoonfallWindow.Characters.cs` (the joins line), `MoonfallWindow.cs` (`FollowShield`);
- `ShieldText.RequestMenu`, the new strings, and `MoonfallShieldTests.cs`.

I re-rendered into my scratchpad `…/scratchpad/gd4/`, never into the repo:
- `title`, `characters`, `quickplay`, `duel` and `far --stage 11` at `--story 1 --far-built --far-walk` (1280);
- `far` at `--story 0 --far-built --far-walk` (1280; 640 with `--stage 7`), and `title` at 640;
- `far --stage 9` at `--story 2 --far-built --far-won 42`;
- `title` at `--story 0` and `--story 5` with `--far-won 0` (the Moon Road won, no Far Shore level built);
- `far --stage 5` at `--story 1 --far-won 0`;
- `levels` at 640.

Every run with `--text-check` reported **0 leaks**, with up to 8 stages veiled.

Crops are in the same folder:
- `hw-chars-moogle`, `hw-quick-companions`, `hw-far-st11-panel`
- `unbuilt-cards`, `arr-far-1280-stops`, `dhp640-bar`, `lv640-ellipsis`
- `scenes-1280`, `scenes-640`, `arr640-pair`

Finding numbers continue from round 3 (M3–M4, m8–m13, n7–n10), so they do not collide with the round-3 numbers in the status table.

## Summary
The owner's "step over it" is implemented cleanly for **stages**. Walking the road at each era wins every stage the story allows, and the veiled ones wait unwon, marked, with their own line. The round-3 walls are gone (`arr-far-1280-stops.png`, `hw-far-1280.png`). The title and the tally now tell the truth at a veiled frontier when the Far Shore is built (`title-road-waits-*`, `hw-title-1280.png`). Every round-3 Minor and Nit is resolved.

Two Majors remain. Both come from the same root: parts of the game still measure progress with the old consecutive-wins count, or ignore whether a stage is built.

- **M3:** step-over opens Storm Post's *stage* but not the *moogle*. The companion's reach still reads the consecutive count, so after the road is walked at Heavensward the moogle is dimmed and padlocked on the Companions screen and in Quick Play. Its stage, meanwhile, shows won. This is the second half of round-3 M2, unresolved.
- **M4:** with the Moon Road won and the Far Shore not yet built, every pre-Dawntrail player is told "The road waits at a stage set past your story… reveal its place". A reveal opens nothing ("On its way"). This is round-3 M1's false message inverted, and it is the very next state the game will ship in.

Six Minors and four Nits follow.

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title (and `title-hint`) | REVISE (M4; n7) | REVISE (M4) |
| title-road-waits (new) | APPROVE as drawn (n7) | APPROVE |
| title-arr (new) | APPROVE (m13, owner's call) | APPROVE (m13) |
| map (Moon Road) | APPROVE | APPROVE |
| far (shown) | APPROVE | APPROVE |
| far-veiled / far-stepover | APPROVE as drawn (m10; n8, n9); REVISE on M4's map case (code-read) | same |
| levels | APPROVE | APPROVE |
| characters | REVISE (M3, rendered) | REVISE (M3, same code) |
| quickplay | REVISE (M3, rendered) | REVISE (M3, same code) |
| challenges | APPROVE | APPROVE |
| duel (setup) | APPROVE (m11) | APPROVE (m11) |
| options (and reduce-motion) | APPROVE | APPROVE |
| hud | APPROVE (round-2 n9 still deferred to the board pass) | APPROVE |
| pause | APPROVE | APPROVE |
| tally | REVISE (M4, m9; code-read) | REVISE (M4, m9; code-read) |
| duelhud (and reduce-motion) | APPROVE | APPROVE |
| duelhud-plain | APPROVE (m6 fixed) | APPROVE (m6 fixed) |
| scene-veiled / scene-shown | APPROVE | APPROVE |
| **Spoiler shield (design)** | **REVISE** (M3, M4) | |
| **Overall** | **REVISE** (two Majors) | |

## Round-3 findings: status
| Round-3 finding | Status | Evidence |
|---|---|---|
| [Major] M1: title says the road is finished at a veiled frontier; tally silent | **Resolved for a built Far Shore**; inverted when unbuilt (M4) | `Next()` reports `Veiled` (`MoonfallModes.cs:304–336`). The card shows CONTINUE, the mark, the placeholder with its hover and reveal, the line, and "Adventure map" opening the stage (`Title.cs:136–148`, `:411–430`, `:447–451`). The 640 line is the placeholder with its reveal (`Title.cs:323–328`). The sub-line stays on the Far Shore. The tally says "Stage N is set past your story: Map opens on it." and Map selects it (`Tally.cs:128–132`, `Flow.cs:358–366`). Seen in `hw-title-1280.png` and `title-road-waits-640.jpg`. |
| [Major] M2: one veiled stage walls every later stage; the moogle closed until Endwalker | **Stages: resolved. The moogle: not resolved (M3).** | `Frontier` skips won and veiled levels (`MoonfallModes.cs:198–217`). `hw-far-1280.png`: stage 11 won at Heavensward. But `hw-chars-moogle.png` and `hw-quick-companions.png` show the moogle padlocked. |
| [Minor] m1: veiled stop looked more open | **Resolved** | A slate veil over the face, and no glow (`arr-far-1280-stops.png`, `sb-far9-1280.png`, the panel face). |
| [Minor] m2: the line over-promised and hid "this session" | **Resolved** | Separate Reached and not-reached lines, both saying "for this session". The padlock shows beside the mark when both apply (`MoonfallLooks.cs:141`). |
| [Minor] m3: the reason was hover-only at 1280; 640 dropped the reveal | **Resolved** | The 1280 panel prints the line, with the codes on one row (`far-stepover-1280.jpg`). 640 reads "Right-click its name to reveal it" (`arr-far7-640.png`). |
| [Minor] m4: challenge pointed at a name not on screen | **Resolved (code-read; latent)** | Its own line. The pill's tooltip and its press open the map on the stage (`Modes.cs`, `challengeVeil`). |
| [Minor] m5: scene veil and title backdrop policy | **Resolved** | `SceneHide.Fallback` draws the recipe over its declared safe picture (`scenes-1280.png`, `scenes-640.png`: the lantern strings over the Moon Road sky). A veiled stage keeps the bare sky. A test holds that every veilable recipe has a placeless fallback. The title is tagged Heavensward and falls back to the chart (`title-arr-*`). The policy is in `moonfall-modes.md` l.70. See m13 for the taste question. |
| [Minor] m6: plain bar cut "Ora" | **Resolved** | `dhp640-bar.png`: "LOUISOIX IS THINKING · Balls 5 · Oranges 19 · ×1 · 3-3", whole parts only. |
| [Minor] m7: placeholders without the hover | **Resolved** | Placeholders are drawn only through `ShieldPlaceholder`. The stop tip and the joins line leave the name out ("Joins on The Far Shore, stage 11 (past your story)"). |
| [Nit] n1: Jupiter's "1" | **Resolved** | Placeholders are set in Axis ("Heavensward area 1"). |
| [Nit] n2: the 640 mark was a blob | **Resolved** | It reads as an eye-slash at 640 (`arr-far7-640.png`). |
| [Nit] n3: cut on an article; ellipsis | **Resolved, with an accepted residue** | "Above…", not "Above the…". The dots still read as two at 640 (`lv640-ellipsis.png`). The font has no "…", as the response says. |
| [Nit] n4: twins hidden at every `--story` | **Resolved** | `--story` meets everyone. |
| [Nit] n5: legend row with nothing veiled | **Resolved** | `anyVeiled` gates it (`Map.cs:308`, `:724`). |
| [Nit] n6: tests staged only "everything reached" | **Partly resolved** | The walk-the-road theory and two frontier tests are added. None checks the moogle *companion* (M3) or an unbuilt Far Shore (M4). |

## Step-over, as a game designer

### Does it play well?
Yes, for the road itself:
- **At A Realm Reborn** (`arr-far-1280-stops.png`), the player plays 1, 2, 5 and 6. The map shows 3 and 4 slate-veiled between won stops, with no padlock, which is exactly "it waits".
- **At Heavensward** (`hw-far-1280.png`), Storm Post's stage is won and the road waits at stage 4.
- **The tally** steps over a veiled stage to the next playable level (`NextLevel`, `MoonfallModes.cs:230–246`).
- **Quick Play** lists the stepped-past levels (`hw-quick-1280.png`: FS 11-1…11-5).

### The round-3 M2 table, re-checked
| Story | Story-safe but walled in round 3 | Now |
|---|---|---|
| A Realm Reborn | 5, 6 | Won (`arr-far-1280-stops.png`) |
| Heavensward | 5, 6, 11 | Stages won (`hw-far-1280.png`); **the moogle companion is still locked** (M3) |
| Stormblood | 8, 11 | Stages won (test `Continue_points…`, walk theory); moogle locked (M3, same code) |
| Shadowbringers | 11 | Stage won; moogle locked (M3, same code) |

At A Realm Reborn, stage 11 is itself veiled (The Churning Mists), so Storm Post stays closed there by design. See m12.

### The title and the tally at a veiled frontier
- **Built Far Shore:** both are right. The title names the *first* stage stepped over, which is where the "here" sits.
- **Built Far Shore, gap:** the tally names the *next* veiled stage after the level just won, which at the road's end can be a different stage from the title's (m9).
- **Unbuilt Far Shore:** both say the wrong thing (M4).

### Copy, veil and reveal path
- The two veiled lines are accurate. The press-to-reveal on the pill closes the keyboard and gamepad gap.
- One hole: a veiled stage that is *not reached* still offers the reveal on its pill, though revealing will not open it (m10).
- The title's line mixes a statement and an order (n7).
- The duel's new locked reason sends the player to Adventure to meet companions, but meeting is the FFXIV story (m11).

### Scenes and backdrops
- **Scene policy:** right. A veiled stage gets the bare sky. An open stage whose scene's place is hidden gets its safe fallback, which keeps base-04's lantern identity (`scenes-1280.png`).
- **Backdrop policy:** consistent and now written down. My only concern is taste: every new player's first screen is the chart, the same image as the Far Shore map (m13).

## Findings

### [Major] M3. Step-over opens Storm Post's stage but not the moogle: companion reach still reads the consecutive count
- **Where:** `MoonfallCompanions.Reached` is `progress.Cleared(info.Campaign) >= info.FirstLevelIndex` (`MoonfallCompanions.cs:181–185`). `Cleared(Expansion)` is the run of consecutive wins (`MoonfallProgress.cs:82`), and it stops at the first veiled stage. At Heavensward it stops at 15 (stage 4), well short of the moogle's 50.
  - `State` feeds the Companions screen, `QuickPlayCompanions`, `StartChallenge`'s pick, `StartDuel` (through `QuickPlay`) and each stop's companion state.
  - Adventure is unaffected, because `AdventureCompanion` gives the moogle on stage 11 regardless (`MoonfallStages.cs:152`).
- **Seen** at `--story 1 --far-built --far-walk`:
  - `hw-far-st11-panel.png`: stage 11 "The Courier's Wake", five levels won, "Moogle · Storm Post".
  - `hw-chars-moogle.png`: the Moogle courier dimmed, with a padlock ("Dimmed: met, not yet reached in Moonfall").
  - `hw-quick-companions.png`: FS 11-5 selected, and the moogle's medallion locked.
- **Why it matters:**
  - Round-3 M2 named the moogle as the worst case: a whole companion and power closed until after Endwalker. The owner's ruling was meant to fix that.
  - The player now plays all five of Storm Post's levels *with* the moogle, then is told they have not reached it.
  - It contradicts the doc's own rule, "a companion is reached once its stage's first level is open" (`moonfall-modes.md` l.82).
  - The walk-the-road test claims "the moogle's Storm Post … is reached", but it only checks the stage. It walks with Minfilia and never asks `CompanionState(Moogle)`.
- **Fix:**
  - Make companion reach follow the frontier. Move the reach check into `MoonfallModes` (`CompanionState` → story first, then `StageReached(stage)` for the companion's stage), and route `QuickPlayCompanions`, the characters words and `Stages()` through it.
  - Or give `MoonfallCompanions.State` a reach predicate that the modes supply.
  - Extend `Adventure_steps_over…` so that from Heavensward to Shadowbringers it asserts:
    - `CompanionState(Moogle) == Available`;
    - `QuickPlayCompanions()` contains the moogle;
    - `QuickPlay("expansion-51", Moogle)` is not null.

### [Major] M4. With the Far Shore open but not (fully) built, Continue says the road waits past the story and asks for a reveal that opens nothing
- **Where:** `Next()` falls back to scanning *every* level from the consecutive count to the campaign's end for an unwon veiled stage (`MoonfallModes.cs:321–327`). It does not ask whether that stage lies before the frontier, or whether any of its levels ship.
  - `Slot` ranks Veiled above Missing (`:259–260`), so an unbuilt veiled stage looks like a waiting one.
- **Seen:** `unbuilt-cards.png`, the Moon Road won and no Far Shore level built.
  - At A Realm Reborn the card says CONTINUE, "Heavensward area 1", "The road waits at a stage set past your story… reveal its place on the map".
  - At Dawntrail it correctly says "The road goes on".
  - After a reveal, the A Realm Reborn player finds "On its way" ×5. They have spent a spoiler for nothing, which is the opposite of the shield's promise.
- **The same root, in two more places:**
  - **The tally:** `NextLevel` sets `steppedOver` and then returns null on a Missing slot (`:236–242`). The tally then says "Stage N is set past your story" (`Tally.cs:128–132`) when the real reason is unbuilt content, and stage N may be unbuilt too.
  - **The map:** `MoonfallLooks.Coming` uses `progress.Cleared` (`Looks.cs:153–158`, called from `Map.cs:776–780`). A not-yet-built stage beyond a stepped-over one therefore shows "Not reached · Win the stages before it to reach it", with a padlock, instead of "Levels on their way". That is round-3 M2's contradiction again, in the partly built case (code-read; for example, at Heavensward with stages 1–3 built, stage 5 says "Not reached").
- **Why Major:**
  - Only 7 of the Moon Road's 55 levels ship today, so the Far Shore will open before its 60 levels are written. That is the next state players will meet.
  - It is the main entry point, it is false, and it pushes a reveal.
- **Fix:**
  - In `Next()`, report `Veiled` only for a veiled stage *before the frontier* (`i < index`) that has at least one shipped level. Otherwise, when the frontier slot is Missing, return null ("The road goes on").
  - In `NextLevel`, set `steppedOver` only for a built stage. When the walk stops on a Missing slot, report no veil.
  - Pass `modes.Frontier(campaign)` to `Coming` instead of `progress.Cleared`.
  - Tests:
    - the Moon Road won, nothing built, A Realm Reborn → `Next()` is null;
    - Heavensward with stages 1–3 built → `Next()` is null, and stage 5 is Coming;
    - Stormblood with stages 1–8 built → `Next()` is `Veiled` at 7.

### [Minor] m8. A reveal, or the story reaching a stepped-over stage, pulls the frontier back and re-closes levels that were open
- **Where:** `Frontier` starts at the consecutive count (`MoonfallModes.cs:201`). Once a stepped-over stage stops being veiled (a session reveal, or the story moving on), the frontier drops back to its first level. Every unwon level after it becomes Sealed again.
- **Example:** a Heavensward player has won stage 11's first two levels, with 11-3 next. They reach Stormblood in FFXIV. Stage 4 unveils, the frontier returns to 4-1, and 11-3 shows a padlock, drops out of Quick Play and is lost as Continue.
  - The same happens for one session after a reveal, and undoes itself on reload.
- **Why it matters:** progress should never go backwards because the player advanced their story or chose to reveal.
- **Fix:** keep "winning a level opens the next" true across veils. A slot is Open if it is at or before the frontier, *or* the level before it in road order is won. Alternatively, persist a reached high-water mark. Add a test: walk at Heavensward partway into stage 11, switch the story to Stormblood, and assert that 11-3 is still Open and 4-1 is Open.
- **Status:** code-read. The renderer cannot stage a reveal or a mid-stage walk.

### [Minor] m9. At the road's end the tally and the title name different stages
- **Example:** at Shadowbringers after 11-5:
  - the tally names stage 12 and Map opens on 12 (`Tally.cs:128–132`, test `A_veiled_stage_at_the_end…`);
  - the title's Continue and the map's "here" point at stage 9.
- **Fix:** when Adventure has no Next, take the tally's stage from `modes.Next()` (where the road waits), so the tally, the title and the map agree. The step-over inside `NextLevel` stays as is.

### [Minor] m10. A veiled stage the road has not reached still offers the reveal on its pill, though a reveal will not open it
- **Where:**
  - `PanelPlayStyle` and `PanelPlayTip` choose the Veiled pill whenever the stage is veiled (`Map.cs:140`, `:581`).
  - `PlayOrReveal` opens "Reveal this name" (`:587–594`).
  - After the reveal, the stage is Sealed ("Not reached").
  - The panel line warns about this, but the pill's tip ("Press to reveal its place for this session") does not.
- **Fix:** when the stage is veiled and not reached, make the pill the Locked "Not reached" pill with the padlock, and leave the reveal on the name's right-click. Or keep it, and make the tip say "Revealing its place will not open it until the stages before it are won."

### [Minor] m11. Duel's new locked reason sends the player to Adventure to meet a companion
- **Where:** `MoonfallDuelNoOpponentTooltip` reads "Meet a companion in Adventure first: a duel is played against one." (`Strings.resx:17258`, used at `Title.cs:372`). Opponents are companions not `NotMet`, and meeting is the FFXIV story ("Face down: someone your story has not introduced yet"). Adventure cannot make that happen.
- **Fix:** "Meet a companion in your story first: a duel is played against one."

### [Minor] m12. Owner's call: the moogle stays closed to every A Realm Reborn player
- **Where:** stage 11 is tagged The Churning Mists (Heavensward). The walk test exempts A Realm Reborn on purpose (`if (reach is >= Heavensward …)`).
- **Why it matters:** the moogle "has no story names (met in the first hours of any start)" (`moonfall-modes.md` l.100). The veil on its stage is the only thing keeping Storm Post from A Realm Reborn players.
- **Fix:** retag stage 11 to nowhere (our own painting, as stage 6 is). This was round-3 M2 option (a), applied to the one stage where it matters. Not blocking.

### [Minor] m13. Owner's call: the chart as every new player's title
- **Where:** `title-arr-*`. The policy is correct and consistent. But the first screen a fresh character sees is the chart, the same image as the Far Shore map, and the painted title is kept for Heavensward players.
- **Fix:** choose a story-safe painting (an A Realm Reborn loading screen) as the title for everyone, or as the fallback in place of the chart, so the first impression is a painting.

### [Nit] n7. The title's veiled card: no stage number, and a mixed sentence
- **Where:** `MoonfallTitleVeiledLine`: "It opens when your story gets there, or reveal its place on the map for this session." It joins a statement to an order. The card also never says *which* stage on the map.
- **Fix:**
  - Eyebrow: "CONTINUE · STAGE 4".
  - Line: "It opens when your story gets there. To play it now, reveal its place on the map (for this session)."

### [Nit] n8. A veiled "here" has no "here" cue
- **Where:** `Glow: view.Here && !veiled` (`MoonfallLooks.cs:143`). On `far-stepover-1280.jpg` no stop wears the legend's "you are here" glow. The map opening on the stage carries it.
- **Fix:** draw a faint slate ring on a veiled "here", or hide the legend's "you are here" row when no stop shows it.

### [Nit] n9. The veiled pill's label names a state, not what pressing it does
- **Where:** "Past your story" opens the reveal menu.
- **Fix:** keep the mark and the menu as the confirmation, but label it as an action ("Reveal…"), or "Past your story · reveal".

### [Nit] n10. `moonfall-modes.md` §2 "Unlocks" still describes the old rule
- **Where:** l.75–85 still says "Open if … i <= the count", and l.82 says the companion is reached when its first level is open (true in the doc, false in the code: M3).
- **Fix:** rewrite the block around `Frontier` and step-over.

## Regressions
- Outside the shield, nothing regressed that I could see. The Moon Road map, level select, Quick Play's layout, challenges, the duel setup, options, the HUD, pause, the tally's layout and both duel HUDs match round 3 or improve on it.
- The plain bar is fixed.
- New problems: M3 (companion reach missed by step-over), M4 (unbuilt Far Shore), and m8 (reveal re-closing levels). All are in logic introduced or exposed by the step-over.

## Overall verdict
**REVISE.** Two Majors remain:
- **M3:** the moogle companion is still locked after its stage is won.
- **M4:** the title, the tally and the map make false "past your story" claims while the Far Shore is unbuilt.

Each is a small change on top of the work done: route companion reach through the frontier, and make `Next`, `NextLevel` and `Coming` respect the frontier and built levels. Each needs a test. The step-over itself, the veil's look, the copy, the reveal path and the scene and backdrop policy are right. Once M3 and M4 are fixed (m8–m11 with them, if possible), I expect to approve.

## Unverified
- **Tests:** I did not run the test suite or the gates, to keep the worktree untouched. The response says all four pass.
- **Code-read only:**
  - the tally at a veiled frontier, and m9, because the renderer cannot stage a Far Shore tally;
  - m8, because no flag stages a reveal or a mid-stage walk;
  - M4's map case (`Coming` with a partly built Far Shore), because `--far-built` builds all 60 or none.
  - M4's title case **is** rendered (`unbuilt-cards.png`).
- **640 for M3:** I rendered characters and Quick Play only at 1280. The cause is the same code at both sizes.
- **The real shield:** the area-by-area masking for a fresh character, for example whether Limsa Lominsa Lower Decks or Western Thanalan is still masked for a character from another starting city when the Far Shore opens. Also whether the title's tag should be The Dravanian Forelands or The Churning Mists for Sohm Al. The era is Heavensward either way.
- **The reveal in game:** `ShieldText.RequestMenu` from the pill by keyboard and gamepad, the menu's placement, the in-place lift through `FollowShield`, the title backdrop swapping live on a reveal, and the veil returning after a plugin reload.
- **Hover, focus and motion:** every tooltip and focus path, Dalamud font metrics, other window scales, and in-game input.


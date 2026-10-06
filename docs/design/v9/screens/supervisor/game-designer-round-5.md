# Moonfall screens: game designer supervision, round 5

Before reviewing I read my round-4 review, the round-4 response, the UX and level-critic round-4 reviews, and `moonfall-modes.md` (the Far Shore section l.55–71 and the rewritten §2 Unlocks, l.73–97). I read the diff from e0db5d19 to 8c51d4d8 in:
- `MoonfallModes.cs`: `Frontier`, `Opened`, `StageBuilt`, `NextLevel`, `Slot`, `Stages`, `Next`, `CompanionState`, `CompanionReached`, `QuickPlayCompanions`;
- `MoonfallWindow.Map.cs`: `panelRevealable`, `PanelPlayStyle`, `PanelPlayTip`, `PlayOrReveal`, the head, the veiled "here" ring, the stop lines, and `Coming` measured from `Frontier`;
- `MoonfallWindow.Tally.cs` and `MoonfallWindow.Board.cs`: the note from `modes.Next()`, and `tallyNoteOnWin` in the rich and plain tallies;
- `MoonfallWindow.Flow.cs` (`EndBoard`, `popupWasOpen` in `HandleKeys`), `MoonfallWindow.Title.cs` (the caption and line), and `MoonfallWindow.cs`;
- `MoonfallShieldTests.cs`: the extended walk theory and the four new tests;
- `Strings.resx`: the changed strings.

**Renders.** I re-rendered 28 screens into my scratchpad `…/scratchpad/gd5/`, never into the repo. Every run used `--text-check` and reported **0 leaks** (up to 8 stages veiled). The states were:
- `characters`, `quickplay` and `title` at `--story 1 --far-built --far-walk` (1280 and 640);
- `far` at the same state, plus `--stage 4` (1280 and 640) and `--stage 11`;
- `title` at `--story 0 --far-won 0` (1280 and 640) and `--story 5 --far-won 0`;
- `far` at `--story 0 --far-won 0`, and at `--story 1 --far-won 0 --stage 1`;
- `far --story 0 --far-built --far-won 5 --stage 7` (1280 and 640);
- `tally` at `--story 3 --far-built --far-walk --leave-one`, rich and `--decoration off`, at both sizes; also at `--story 1`;
- `title`, `far` and `characters` at `--story 0 --far-built --far-walk`;
- `title` and `far` at `--story 3 --far-built --far-walk`;
- `duel` at `--story 1 --far-built --far-walk`.

**Harness.** The renderer cannot stage a partly built Far Shore, a reveal, a change of story, or the Moon Road's last win. For those I wrote a scratch console harness (`…/scratchpad/gd5/harness/Program.cs`, outside the repo). It references the renderer's built `Tsukimichi.Core.dll`, calls `MoonfallModes` directly and reproduces each state.

**Crops** are in the same folder:
- `c-cards.png` (four Continue cards), `c-title640-pair.png`, `c-hw640-pair.png`;
- `c-hw-here-ring.png`, `c-panels.png`, `c-far640.png`, `c-nr640-committed.png`;
- `c-tally640.png`, `c-tallyplain1280.png`, `c-montage.png`, `c-committed.png`.

Finding numbers continue from round 4: m14–m17, n11–n12.

## Summary
**Both Majors are resolved.**
- **M3:** the moogle now follows its stage. At Heavensward after the road is walked, it is face up on the Companions screen and selectable in Quick Play at both sizes (`hw-chars-*`, `hw-quick-*`). The walk theory now asserts it.
- **M4:** an unbuilt Far Shore is no longer called "past your story" on the title or the tally. With the Moon Road won and nothing built, A Realm Reborn and Dawntrail both read "The road goes on" (`c-cards.png`, `c-title640-pair.png`). A sweep over every era with 0–12 stages built found no veiled Continue on an unbuilt stage.
- **m9:** the tally, the title and the map now name the same waiting stage: stage 9 at Shadowbringers, stage 4 at Heavensward.
- **Also resolved:** m10, m11, n7, n8, n9 and n10.

**Four new Minors**, none blocking:
- **m14:** the map still offers "Reveal its place" on a reached veiled stage with no levels built. This is the last surface of M4's root.
- **m15:** "open for good" covers only a level whose predecessor is won. A stage the road stepped to but the player has not yet played re-closes after a reveal or a story advance, and the moogle can lock again.
- **m16:** the tally's "last level for now" note is now drawn on every win without a Next. It is false at the Moon Road's end once the Far Shore has levels.
- **m17:** at 640, a not-reached veiled stage's line says "Press Reveal" above a "Not reached" pill.

**Two Nits** follow. m12 and m13 are pending with the owner and are not counted.

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title (and `title-hint`) | APPROVE (M4, n7 fixed) | APPROVE (M4 fixed) |
| title-road-waits | APPROVE | APPROVE |
| title-arr | APPROVE (m13 pending, owner's call) | APPROVE (m13 pending) |
| map (Moon Road) | APPROVE | APPROVE |
| far (shown) | APPROVE | APPROVE |
| far-veiled / far-stepover | APPROVE (n8, n9 fixed); m14 (harness) | APPROVE; m14 |
| far-veiled-not-reached (new) | APPROVE (m10 fixed) | APPROVE with m17 |
| levels | APPROVE | APPROVE |
| characters | APPROVE (M3 fixed, rendered) | APPROVE (M3 fixed, rendered) |
| quickplay | APPROVE (M3 fixed, rendered) | APPROVE (M3 fixed, rendered) |
| challenges | APPROVE | APPROVE |
| duel (setup) | APPROVE (m11 fixed) | APPROVE (m11 fixed) |
| options (and reduce-motion) | APPROVE | APPROVE |
| hud | APPROVE (round-2 n9 still deferred to the board pass) | APPROVE |
| pause | APPROVE | APPROVE |
| tally | APPROVE with m16 (harness) | APPROVE with m16 |
| tally-road-waits (new) | APPROVE | APPROVE |
| tally-road-waits-plain (new) | APPROVE | APPROVE |
| duelhud (and reduce-motion) | APPROVE | APPROVE |
| duelhud-plain | APPROVE | APPROVE |
| scene-veiled / scene-shown | APPROVE | APPROVE |
| **Spoiler shield (design)** | **APPROVE** (m14, m15 Minor) | |
| **Overall** | **APPROVE** (no Major; m12 and m13 pending with the owner) | |

## Round-4 findings: status
| Round-4 finding | Status | Evidence |
|---|---|---|
| [Major] M3: the moogle stays locked after its stage opens | **Resolved** | `CompanionState` now asks `CompanionReached` (`MoonfallModes.cs:409–457`). A companion is reached when its stage is not veiled and the frontier is at or past it, a level of it is won, or it is `Opened`. `QuickPlayCompanions` goes through the same rule (`:481`). `hw-chars-1280.png`/`-640`: the Moogle courier is face up. `hw-quick-1280.png`/`-640`: the moogle's medallion is selected, with "Play FS 11-5". The walk theory asserts Available, the Quick Play list and `QuickPlay("expansion-51", Moogle)` (`MoonfallShieldTests.cs:315–318`). |
| [Major] M4: false "past your story" claims while unbuilt | **Resolved on the title and tally**; one map residue (m14) | `Next()` scans only up to the frontier and requires `StageBuilt` (`:357`). `NextLevel` clears the stepped-over stage on a Missing slot (`:270–275`). `Coming` is measured from `Frontier` (`Map.cs:793`). `c-cards.png`: A Realm Reborn unbuilt reads "The road goes on". `dt-title-1280.png` reads the same. `hw-unbuilt-far-1280.png`: stage 1 shows "Levels on their way". Harness sweep (6 eras × 0–12 stages built): no veiled Next on an unbuilt stage. The three requested tests exist (`:220`, `:229`, `:243`). |
| [Minor] m8: a reveal or story advance re-closes levels | **Partly resolved** (m15) | `Opened` keeps a level open when its predecessor is won (`:224`, `:297`). The test covers a walk into 11-3. Not covered: a stage stepped to but not yet played (harness, m15). |
| [Minor] m9: title and tally named different stages | **Resolved** | The tally takes its stage from `modes.Next()` (`Tally.cs:132`). `sb-tally-1280.png` ("Stage 9 …") matches `shb-title-1280.png` ("CONTINUE · STAGE 9"). At Heavensward, `hw-tally-1280.png` says stage 4 and the title says stage 4. |
| [Minor] m10: reveal offered where it opens nothing | **Resolved for not reached**; m17 at 640; m14 for unbuilt | `panelRevealable` requires Reached (`Map.cs:854`). `c-panels.png` (left): stage 7 shows the padlock's "Not reached" pill and its line "Revealing its place will not open it until then." |
| [Minor] m11: duel copy | **Resolved** | "Meet a companion in your story first: a duel is played against one." (`Strings.resx:17259`) |
| [Minor] m12: retag stage 11 | **Pending, owner's call** | Unchanged. In `arr-walk-far-1280.png`, stage 11 shows both the mark and the padlock at A Realm Reborn. |
| [Minor] m13: chart as the A Realm Reborn title | **Pending, owner's call** | Unchanged (`c-title640-pair.png`, right). |
| [Nit] n7: card caption and line | **Resolved** | "CONTINUE · STAGE 4/3/9" and the two-sentence line (`c-cards.png`, `Title.cs:146`). |
| [Nit] n8: veiled "here" had no cue | **Resolved** | A slate ring around stop 4 (`c-hw-here-ring.png`, `Map.cs:444`). It is faint but legible at 1280. |
| [Nit] n9: pill named a state | **Resolved** | "Reveal its place" (`hw-far-1280.png`). |
| [Nit] n10: §2 Unlocks described the old rule | **Resolved**, with one over-claim (m15) and one stale line (n11) | `moonfall-modes.md` l.73–97. |

## Step-over, end to end
| Surface | Built Far Shore | Partly built or unbuilt Far Shore |
|---|---|---|
| Stages (frontier, Open, Sealed) | Right at every era (walk theory, `arr-walk-far`, `hw-far`, `shb-far`) | Right. `Coming` is from the frontier, and stage 5 is "Levels on their way" in the M4 test state. |
| Companions | Right (M3 fixed). A side effect: the frontier also steps over *unbuilt* veiled stages, so at Heavensward with stages 1–6 built the moogle becomes Available while stage 11 has no levels (harness, Case 5). That is consistent with the doc's rule and generous rather than wrong, so it is not a finding. | Same |
| Title | Right: the first built veiled stage before the frontier, with its stage number | Right: "The road goes on" |
| Tally | Right, and in agreement with the title (m9 fixed). Rich and plain both draw the note. | Right when nothing veiled waits. The fallback note is wrong when Next() is playable elsewhere (m16). |
| Map | Right: the ring, the reveal pill on reached stages, and "Not reached" on unreached ones | **m14:** a reached veiled stage that is unbuilt still offers the reveal |
| Built-aware rules | Hold in `Next`, `NextLevel` and `Coming` | Do not reach `panelRevealable` or the stop tooltip (m14) |

## Findings

### [Minor] m14. The map still offers "Reveal its place" on a reached veiled stage whose levels are not built
- **Where:** `panelRevealable = sel.State == Veiled && sel.Reached` (`Map.cs:854`). There is no `StageBuilt` check. The stop's tooltip line "Past your story: select it to reveal its place" (`Map.cs:816`) and the panel line "Set past your story. … press Reveal its place …" behave the same way.
- **Why it happens:** `Frontier` steps over veiled stages whether built or not (`MoonfallModes.cs:198–217`), so the unbuilt veiled stages behind the frontier count as Reached (`:323`).
- **Seen in the harness, Case 6** (the stage numbers are those the map would offer to reveal):

| Era | Stages built | Next() | Reveal offered on |
|---|---|---|---|
| A Realm Reborn | 1–2 | null | 3 (unbuilt), 4 (unbuilt) |
| Heavensward | 1–3, the new M4 test's own state | null | 4 (unbuilt) |
| Heavensward | 1–6 | Veiled at 4 | 4, 7, 8, 9, 10 (7–10 unbuilt) |
| Shadowbringers | 1–8 | null | 9 (unbuilt), 10 (unbuilt) |

- **Why it matters:**
  - The title now correctly says "The road goes on", and its button opens the map. On that map, a veiled stop's primary action is a reveal.
  - Pressing it spends a later expansion's area name (The Ruby Sea, Il Mheg) and shows "Levels on their way". That is M4's "spoiler spent for nothing", on a secondary screen.
  - It breaks the doc's own rule: "a stage whose levels are not built is never 'past your story'" (`moonfall-modes.md` l.95–97).
  - It is the same pattern as round-4 m10, which was a Minor.
- **Fix:**
  - `panelRevealable = Veiled && Reached && modes.StageBuilt(sel.Stage)`.
  - Otherwise, keep the veil (mark and placeholder), but give the panel the Waiting pill "Levels on their way" and a line such as "Set past your story; its levels are on their way." Leave the reveal on the name's right-click.
  - Apply the same check to `stopTipLines` and the 640 line.
  - Extend `A_partly_built_far_shore…` so that stage 4 is not offered a reveal. For example, expose `Built` on `MoonfallStageView`, or assert `StageBuilt` against the Reached flag.

### [Minor] m15. "Open for good" holds only after a win: a stage stepped to but not yet played re-closes, and the moogle can lock again
- **Where:** `Opened` is "the predecessor is won" (`MoonfallModes.cs:224–225`). A stage's first level that was opened by step-over has a veiled, unwon predecessor. When the veil lifts, `Frontier` drops back (`:198`) and that level becomes Sealed again. `CompanionReached` (`:424–457`) follows it.
- **Seen in the harness, Case 1:** walk at Heavensward until Continue is 11-1, without playing it. The moogle is Available.
  - Switch the story to Stormblood: 11-1 is **Sealed**, stage 11 is Sealed, and the moogle is **MetNotReached**. It stays locked until stages 4 and 8 are won (10 levels).
  - Reveal stage 4 (The Ruby Sea) at Heavensward instead: the same happens for the session.
- **Seen in the harness, Case 1b:** at Heavensward after 3-5, Continue is 5-1. Reveal stage 4 from its own "Reveal its place" pill, and 5-1 and stage 5 become Sealed. The stage the player was just told to play gets a padlock.
- **Why it matters:**
  - Progress goes backwards because the player advanced their story or used the reveal the map offers.
  - The doc claims the opposite: "a reveal … never closes a level the player had come to" (l.91–93). The response says the same.
  - The new test passes only because it walks into 11-3 (`MoonfallShieldTests.cs:253`).
  - The moogle relocking in Quick Play and duels is a narrower echo of M3.
- **Fix:**
  - Persist a per-campaign high-water mark of the frontier, recorded on every `FinishLevel` as `max(high, Frontier)`. Then make a slot Open when `index <= max(Frontier, high)` or `Opened`, and give `CompanionReached` the same rule.
  - This stays spoiler-safe: Veiled still ranks first in `Slot`, so an alt with less story sees nothing new.
  - Add tests: Heavensward walked to frontier 11-1 with it unwon, then Stormblood → 11-1 Open and the moogle Available. Heavensward after 3-5 with stage 4 revealed → 5-1 Open.
  - If the owner prefers the strict road, narrow the doc's claim instead. Either way the doc and code must agree.

### [Minor] m16. The tally's "That was the last level for now: more are on the way" is drawn whenever Next() is not veiled, including when there is a level to play
- **Where:** `Tally.cs:141–145`. The fallback branch runs for any Adventure win with no `AdventureNext()` whose `modes.Next()` is not `Veiled`, and that includes a playable `Next()`. Before this round the note was never drawn on a win. `tallyNoteOnWin` (`:219`, `Board.cs`) now draws it in place of the sub-line, so the false case is new and visible.
- **Seen in the harness:**
  - **Case 2:** win the Moon Road's 55th level with Far Shore levels built. `NextLevel(Base, 54)` is null and `Next()` is FS 1-1, playable. The tally says "the last level for now" at the moment the Far Shore opens, the game's biggest milestone, and offers no Next.
  - **Case 3:** in m15's state, win 11-5 from the map. `Next()` is 4-1, playable, and the tally says the same.
- **Why Minor:** the copy is false, but it pushes no reveal, and it needs content that does not ship yet (all 55 Moon Road levels plus Far Shore levels).
- **Fix:**
  - Draw `MoonfallLastLevel` only when `modes.Next()` is null.
  - When `modes.Next()` is a playable place outside `NextLevel`'s reach, offer Next on it ("Next · FS 1-1"), or add a note such as "The Far Shore opens" and select its stage for Map, as `tallyVeil` does.
  - Test: win base-55 with the Far Shore built → the tally offers FS 1-1.

### [Minor] m17. At 640, a not-reached veiled stage says "Press Reveal or right-click its name" over a "Not reached" pill
- **Where:** `panelState = … m.Small ? Strings.MoonfallStageVeiledShort : veiledLine` (`Map.cs:860`). The short line ignores `Reached`, while the pill follows it (`:854–855`).
- **Seen:** `arr-far7-nr-640.png` and the committed `far-veiled-not-reached-640.jpg` (`c-nr640-committed.png`): "Press Reveal or right-click its name" above "🔒 Not reached".
- **Why it matters:** it names a button that is not on screen, for a stage a reveal will not open. This is m10's case, left open at 640.
- **Fix:** use a not-reached short line when `!sel.Reached`. The existing `MoonfallStopVeiledSealedLine` ("Past your story, and not reached yet") fits.

### [Nit] n11. `moonfall-modes.md` l.69 still says pressing a veiled stage's Play reveals it
- **Fix:** add "when the road has come to it (and its levels are built, after m14); otherwise the pill is the padlock's Not reached". Also update l.91–93 per m15.

### [Nit] n12. The walk theory's companion loop checks "reached iff Done", which is not the rule
- **Where:** `MoonfallShieldTests.cs`, the loop after the moogle block. Only the moogle is a Far Shore companion. "Reached iff the stage is Done" holds only at the end of a walk, because the rule is "reached once opened".
- **Fix:** assert `CompanionReached(who) == (view.State != Veiled && view.Levels[0].Reached)`, which states the real rule and would also catch m15's case.

## Regressions
- **Copy:** m16 is the only new false copy. It comes from drawing the note on wins, which is new this round.
- **Screens:** none. I compared these against round 4 and they are unchanged or improved:
  - re-rendered: Moon Road map, levels, challenges, title, pause, duelhud-plain (`c-committed.png`), Quick Play, Companions and the duel;
  - from code: `popupWasOpen` (Esc closing the reveal menu no longer also goes back) and `EndBoard` (Esc over the tally opens the waiting stage, gated on `tallyVeil`, which is set only on a won Adventure tally).
- **Hover:** the Veiled pill's hover glow (`Menu.cs:452`) applies only to the reachable reveal pill, so the inert pills stay unlit.

## Overall verdict
**APPROVE.** M3 and M4 are resolved. Step-over is now right end to end, at both sizes, for:
- the stages;
- the companions;
- the title and the tally, which agree with each other and with the map's "here";
- the built-aware Continue.

I would like m14 (one condition on `panelRevealable`, which finishes M4's rule on the map) and m17 (a one-line string choice) folded in before release. m15 and m16 are worth fixing with their tests, but they need a reveal, a story change or future content to appear. m12 and m13 remain the owner's calls.

## Unverified
- **Tests and gates:** I did not run the suite or the gates, to keep the worktree untouched. The response reports all four green (8863 tests).
- **The harness's DLL:** the harness used the renderer's `Tsukimichi.Core.dll`, built at 04:47. HEAD was committed at 04:59. I assumed no Core change landed in between. The harness's results match the code I read and the new tests.
- **Code and harness only, not rendered:** m14, m15 and m16. The renderer cannot build part of the Far Shore, stage a reveal or a story change, or win base-55. m16's on-screen text is inferred from `Tally.cs`.
- **The real shield:** area masking for real characters. My harness and the renderer use an era stand-in.
- **In game:** the reveal menu by keyboard and gamepad, the `popupWasOpen` timing, `FollowShield`'s live lift, hover and focus paths, Dalamud font metrics, other window scales, and the veil returning after a reload.


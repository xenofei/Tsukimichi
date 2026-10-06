# Moonfall screens: game designer supervision, round 6

Before reviewing I read three things: my round-5 review, `response-round-5.md` (with the owner's two answers of 6 October 2026), and `moonfall-modes.md` (the Far Shore list and the step-over bullet at l.68–69, the backdrop policy at l.70, and §2 Unlocks at l.73–100). I then read `git diff 8c51d4d8 8addf12c` in these files:
- `MoonfallPlaces.cs`: stage 11 at l.72, and `OfBackdrop(TitleEarly)` at l.105;
- `MoonfallBackdrops.cs`, and `MoonfallGameArt.BackdropFailed`;
- `MoonfallProgress.cs`: `BaseReach`, `ExpansionReach`, `RecordReach`, `Absorb` and `Clean`;
- `MoonfallModes.cs`: `Slot` at l.297, `CompanionReached` at l.445, `NoteReach` at l.466, and `FinishLevel` at l.545;
- `MoonfallWindow.cs`: `FollowShield` at l.161;
- `MoonfallWindow.Title.cs`: `DrawTitle` at l.195–215;
- `MoonfallWindow.Map.cs`: l.616 and l.836–883;
- `MoonfallWindow.Flow.cs`: `AdventureNext` at l.450;
- `MoonfallWindow.Tally.cs`: l.126–146;
- the shield tests, the game-data test, the scene-start tests and the screens lint.

**Renders.** I re-rendered 16 screens into my scratchpad `…/scratchpad/gd6/`, never into the repo, with `TSUKIMICHI_GAME_PATH` set. Every run with `--text-check` reported **0 leaks**, with 7 stages veiled. The screens were:
- `title --story 0` and `title --story 1`;
- `far --story 0 --far-built --far-walk`, with `--stage 11` (1280 and 640) and `--stage 3`;
- `far --story 0 --far-built --far-won 5 --stage 7` (1280 and 640);
- `characters` and `quickplay` at `--story 0 --far-built --far-walk` (1280 and 640);
- `tally --story 0 --far-built --far-walk --leave-one` (1280 and 640);
- `play --decoration off` (1280 and 640).

My `title-arr-*` and `hud-plain-*` match the committed JPEGs to within JPEG noise (RMSE 0.6–1.2 %).

**The painting.** I dumped `ui/loadingimage/-nowloading_base02.tex` (and base01, base03 and base07 for comparison) straight from the install with Lumina, before any grading, using a scratch tool in `gd6/texdump/`.

**Harness.** I wrote a new console harness, `gd6/harness/Program.cs`, outside the repo. It references the renderer's built `Tsukimichi.Core.dll`. A probe confirmed that the DLL is round 6's: `BaseReach` exists, `TitleEarly` exists, stage 11 is Nowhere, and the backdrop path is `-nowloading_base02.tex`. Every win in the harness calls `NoteReach()`, as `FinishLevel` does.

**Crops** are in `gd6/`:
- `c-uldah-raw-vs-title.png` and `c-uldah-vs-sohmal-raw.png`;
- `c-far640-pair.png`, `c-arr-chars-quick-640.png` and `c-tally-hud-640.png`;
- `c-committed.png` and `c-committed2.png`.

Finding numbers continue from round 5: m18, n13–n16.

## Summary
**Both owner answers are implemented correctly, and every round-5 Minor and Nit is resolved.**
- **m12:** stage 11 (the Courier's Wake) is set nowhere.
  - After a full walk, at every era from A Realm Reborn to Dawntrail: stage 11 is Done, the moogle is Available, the moogle is in Quick Play, and `QuickPlay(FS 11-1, Moogle)` is not null (harness A).
  - At A Realm Reborn the moogle becomes Available exactly when the road reaches 11-1, and not before (harness B).
  - Stage 11's lore ("Moogle post, every inn in Eorzea"), its stop position, its name and its card name no area.
- **m13:** the A Realm Reborn title is the game's own Ul'dah painting. I confirmed this from the raw texture: the great dome, the minaret spires, the white gate and the airship. It is tagged A Realm Reborn, and the chart is used only when the chosen painting fails or both paintings are hidden.
- **m14–m17, n11, n12:** resolved in the code, the tests, the lint and the docs. I confirmed m15 and m16 in the harness, and m17 in a render.

**One new Minor:**
- **m18:** `CompanionReached` now goes through `Slot(...).Reached`. This silently changed the rule for a companion whose stage's first level is not built but is at the frontier: such a companion was Available and is now MetNotReached. The doc still states the old rule.

**Four Nits** (n13–n16) cover:
- the title's fallback order;
- how Ul'dah is framed at 640;
- `Next()` not knowing the reach mark;
- the fresh-character case m13 is meant for, which no test checks.

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title-arr (Ul'dah) | APPROVE (m13 resolved; n14 framing) | APPROVE (n14: the dome sits behind the logo) |
| title, title-hint, title-road-waits (re-encoded) | APPROVE | APPROVE |
| far-stepover (A Realm Reborn, stage 3 waiting, stage 11 won) | APPROVE (m12 resolved) | APPROVE |
| far-stepover, stage 11 selected (my render) | APPROVE: "The Courier's Wake · Moogle · Storm Post", Choose a level | APPROVE |
| far-veiled-not-reached | APPROVE | APPROVE (m17 resolved: "Past your story, and not reached yet" over "Not reached") |
| far / far-veiled / map / levels (re-encoded) | APPROVE | APPROVE |
| characters at A Realm Reborn after the walk | APPROVE (the moogle is face up, "Storm Post") | APPROVE |
| quickplay at A Realm Reborn after the walk | APPROVE (the moogle is selectable, "Play FS 11-5") | APPROVE |
| tally at A Realm Reborn, road waits | APPROVE ("Stage 3 is set past your story: Map opens on it."; Map has focus) | APPROVE |
| hud-plain (new) | APPROVE (3-3 · Balls · Oranges · ×1 · name) | APPROVE |
| challenges / duel / options / tally-road-waits(-plain) (re-encoded) | APPROVE | APPROVE |
| **Spoiler shield (design)** | **APPROVE** (m18 Minor; n15 and n16 Nits) | |
| **Overall** | **APPROVE** (no Major) | |

## Round-5 findings: status
| Round-5 finding | Status | Evidence |
|---|---|---|
| [Minor] m12: retag stage 11 | **Resolved** (owner's answer taken) | `MoonfallPlaces.cs:72` is `MoonfallPlace.Nowhere`. The era test allows exactly stages 6 and 11 to name no place (`MoonfallShieldTests.cs:81`). The walk theory has no exemption by era (l.400–405). Harness A covers all six eras. `arr-walk-far-1280.png` shows stage 11 Done with no padlock and no eye-slash. `c-arr-chars-quick-640.png`: the moogle is face up and selectable. The lore is `MoonfallLooks.cs:98`. The doc's Far Shore list (l.68) and §3 (moogle line) agree. |
| [Minor] m13: chart as the A Realm Reborn title | **Resolved** (owner's answer taken); n13, n14, n16 | `MoonfallBackdrops.PathOf` returns base02. `OfBackdrop(TitleEarly)` is `(A Realm Reborn, "Ul'dah - Steps of Nald")` (`MoonfallPlaces.cs:105`). `FollowShield` chooses Title, then TitleEarly, then Chart (`MoonfallWindow.cs:161`). `DrawTitle` drops to the chart only on `BackdropFailed` (`Title.cs:195–197`). It stops counting as pending once failed (l.215), so a missing painting cannot hold the art-pending counter forever. The shield test asserts that A Realm Reborn hides Sohm Al but not Ul'dah (l.508–510). The game-data test adds the tag. The doc (l.70) agrees, apart from n13. |
| [Minor] m14: reveal on an unbuilt reached veiled stage | **Resolved** | `veiledComing` and `panelRevealable` (`Map.cs:875–877`): the pill is "Levels on their way" and the line is `MoonfallStageVeiledComingLine`. The stop tooltip has three reasons (l.836) and the 640 line follows them (l.883). The head is NoNav when it can't act (l.616). The shield test asserts that stage 4 is Veiled, Reached and not built (l.246–251), and the lint holds the exact expressions. Not rendered: the renderer cannot build part of the Far Shore. |
| [Minor] m15: a stepped-to level re-closes | **Resolved** | `Slot` adds `index == Progress.Reach(campaign)` (`MoonfallModes.cs:297`). `NoteReach` runs after every `FinishLevel` (l.545). `Absorb` takes the max, and `Clean` clamps to the level counts. Harness C: Heavensward walked to 11-1, then Stormblood gives frontier 4-1, 11-1 **Open**, 11-2 Sealed and the moogle **Available**. Heavensward after 3-5 with stage 4 revealed gives 5-1 **Open** and stage 5 Open. Both requested tests exist (l.255 and l.287). |
| [Minor] m16: "last level for now" on a playable Next | **Resolved** | The note is drawn only when `modes.Next() is null` (`Tally.cs:141`). `AdventureNext` falls back to a playable `Next()` (`Flow.cs:450`), so base-55 offers "Next · FS 1-1" (harness G: `NextLevel(Base,54)` is null and `Next` is FS 1-1). With the Far Shore unbuilt, `Next` is null and the note shows, which is correct. Test at l.307, and a lint. |
| [Minor] m17: the 640 not-reached line | **Resolved** | `Map.cs:883`. Seen in `arr-nr7-640.png` and the committed `far-veiled-not-reached-640.jpg` (`c-committed.png`, right): "Past your story, and not reached yet" over "🔒 Not reached". |
| [Nit] n11: the doc's veiled Play | **Resolved** | `moonfall-modes.md` l.69 describes the reveal only where it opens something. l.92–97 describe the reach mark. |
| [Nit] n12: the walk theory's companion rule | **Resolved** | `MoonfallShieldTests.cs:414` asserts the rule itself at every era. The walk builds every level, so it never exercises an unbuilt stage (see m18). |

## Step-over and reach, end to end
| Surface | All built | Partly built |
|---|---|---|
| Stages | Right at every era; stage 11 is open everywhere (harness A) | Right; m14 holds the map |
| Reach mark | Story advance and reveal keep the level the road came to (harness C). An alt with less story raises the shared mark: an A Realm Reborn alt walked to 11-1 leaves the main character at Dawntrail with 3-1, 7-1 and 11-1 open (harness D). That is generous, consistent with the doc's "stays open, for good" and spoiler-safe (Veiled ranks first), so it is not a finding. | Same |
| Companions | Right (harness A and B) | **m18**: a companion whose first level is unbuilt and at the frontier is now MetNotReached |
| Title / tally | Right; they agree with the map's "here" (`c-tally-hud-640.png`) | **n15**: `Next()` ignores the reach mark (out-of-order content only) |

## Findings

### [Minor] m18. `CompanionReached` changed its rule for a stage whose first level is unbuilt at the frontier, and the doc still states the old rule
- **Where:** `MoonfallModes.cs:445`.
  - Before: `stage.FirstLevelIndex <= Frontier(...)`, then won, then `Opened`.
  - Now: `Slot(info.Campaign, stage.FirstLevelIndex).Reached`. `Reached` is Open or Cleared (`:61`), so a **Missing** first level is never reached.
  - The frontier stops on a Missing level, so "the road has come to a stage that ships no levels" was reached before and is not now.
- **Seen in harness F:**
  - Moon Road with only stage 1 built (base-01 to base-05), all won: the frontier is 5 (2-1, Missing). The twins are **MetNotReached**. The old rule, and the pure `MoonfallCompanions.Reached`, give Available.
  - Far Shore with stages 1–10 built, walked at Dawntrail: the frontier is 11-1 (Missing). The moogle is **MetNotReached** (old rule: Available).
- **Why it matters:**
  - This is the shipping pattern from the next content drop on: a stage ships whole and the next one does not. Finishing a stage then no longer unlocks the next companion for duels or Quick Play until that companion's levels ship.
  - The response does not mention this change, and no test covers it. The walk theory always builds every level.
  - `moonfall-modes.md` l.85 still says "the frontier at or past it".
  - No save regresses today: only base-01 to base-04 ship, so the frontier cannot reach 2-1.
- **My view on the design:** the new behaviour reads better. The map already says "Levels on their way" for that stage, so a dimmed card with "stage 2" agrees with it. Under the old rule the duel opened before the stage did.
- **Fix:** choose one rule, and make the doc and a test match it.
  - **To keep the new rule:** change l.85–86 to "once its stage's first level is open (built and reached, by that rule), or a level of it is won". Add a test: stage 1 built and won with stage 2 unbuilt gives the twins MetNotReached; shipping 2-1 makes them Available.
  - **To restore the old rule:** use `if (StageReached(stage) || Slot(...).Reached)`.

### [Nit] n13. When Sohm Al cannot be read, the title drops to the chart, not to Ul'dah
- **Where:** `Title.cs:195–197`. A failed `Title` goes straight to `Chart`.
- **The doc (l.70) says:** "The chart … is only the last fallback, when neither painting can be shown or read." From Heavensward on, Ul'dah can be shown, so the doc and the code disagree.
- **Impact:** low. Both paintings are in the same `ui/loadingimage` folder and would usually fail together.
- **Fix:** try `TitleEarly` when `Title` fails and the shield shows Ul'dah, or narrow the doc to "when the chosen painting cannot be read".

### [Nit] n14. Ul'dah is framed with Sohm Al's crop: at 640 its dome sits behind the logo
- **Where:** `MoonfallBackdrops.Title`, `x0 = 960 − 0.30·cw` (`:76`). That focus puts Sohm Al's peak right of the modes. Ul'dah's dome is at the painting's centre, so it lands about 30 % across the frame.
- **Seen:** `c-uldah-raw-vs-title.png`.
  - At 1280 the dome shows between the logo and the right-hand tower, and the city reads as Ul'dah to anyone who knows it.
  - At 640 (`title-arr-640.png`), the logo and the Continue button cover the dome and the white gate, so it reads as a generic moonlit city of spires.
  - The jewel grade also removes Ul'dah's signature gold. That is consistent with the title's look, and I am not asking to change it.
- **Fix (optional, for the owner's eye):** give `TitleEarly` its own focus, around 0.45–0.5, so that the dome sits right of the modes as Sohm Al's peak does.

### [Nit] n15. `Next()` does not know the reach mark
- **Where:** `MoonfallModes.Next()` (around `:337`) looks only at the frontier's slot, then at veiled built stages.
- **Seen in harness E:** stages 1, 2, 5, 6 and 11 built; walked at A Realm Reborn to 11-1, which is unplayed and is the reach mark. The story then reaches Heavensward, and stage 3 (unbuilt) pulls the frontier back onto a Missing 3-1.
  - `Next()` is null, while 11-1 is **Open** by the mark.
  - The title offers no Continue, and a win would draw "the last level for now".
- **Why only a Nit:** it needs content shipped out of order. With stages shipped in order, the frontier never lands on an unbuilt stage before the mark.
- **Fix:** in `Next()`, when the frontier's slot is not Open, return the reach slot if it is Open and unwon, before the veiled scan. Add a one-line test.

### [Nit] n16. m13 is meant for a fresh A Realm Reborn character, and no test checks one
- **Where:** `MoonfallShieldGameDataTests.Each_place_is_hidden_before_its_era_and_shown_after_it`.
  - For era 0, the "hidden before" check is skipped.
  - "Shown after" is checked at Heavensward's first quest.
- **Why it matters:** the real `SpoilerMask` also hides an area whose anchors are all still ahead (`SpoilerMask.cs:428`). If "Ul'dah - Steps of Nald" is anchored to quests ahead of a new Limsa or Gridania starter, that player gets the chart until the main scenario takes them to Ul'dah.
  - This is story-safe either way, and the code handles it: `FollowShield` falls to Chart.
  - It may not be what the owner pictured.
- **Fix:** assert `At(0).IsNameMasked(Area, "Ul'dah - Steps of Nald")` with the default options, for a fresh character. If it is masked, tell the owner that early starters from the other cities see the chart, or accept that.

## Regressions
- **Behaviour:** m18 is the only behaviour change outside the announced scope.
- **Screens:** none. Against round 5:
  - the far map, Companions, Quick Play, the tally, the plain HUD and the 640 not-reached panel are unchanged or improved, by re-render;
  - the rest are byte-level re-encodes, checked against the committed JPEGs.
- **Copy:**
  - Far Shore tally Next now always carries the campaign ("Next · FS 3-2", `PlaceCode`). This is consistent with Quick Play's list, and I do not count it as a finding.
  - The new strings name no place.

## Overall verdict
**APPROVE.** No Major. The owner's two answers are implemented as decided:
- stage 11 and the moogle open at every era once the road comes to them;
- the A Realm Reborn title is Ul'dah's own loading screen, story-safe at A Realm Reborn and correctly tagged, with the chart as the true last fallback.

Every round-5 Minor and Nit is resolved, with tests and lint. I would like m18 settled before release, by choosing a rule and aligning the doc and a test, because the next content drop will hit it. n13–n16 are optional.

## Unverified
- **Tests and gates:** I did not run the suite or the gates, to keep the worktree untouched. The response reports them green (8868 tests). The game-data test over the real shield is unverified by me, and so is the fresh-character case (n16).
- **The harness's DLL:** `Tsukimichi.Core.dll` was built at 05:22 and HEAD was committed at 06:03. A reflection probe confirmed the round-6 features (reach, TitleEarly, stage 11 Nowhere, the base02 path), and the harness results match the code I read.
- **Not rendered (code and harness only):** m14's "Levels on their way" panel, m15, m16's "Next · FS 1-1", m18 and n15. The renderer cannot build part of the Far Shore, stage a story change or a reveal, or win base-55.
- **The chart fallback path:** a painting that fails to read was not exercised.
- **In game:** the real shield's masking of Ul'dah for real characters, `FollowShield`'s live switch from Ul'dah to Sohm Al when the story reaches Heavensward, the Esc latch, focus and nav on the NoNav head, Dalamud font metrics, other window scales, and the reach mark across a save and reload.


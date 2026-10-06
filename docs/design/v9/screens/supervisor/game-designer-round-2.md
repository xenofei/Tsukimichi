# Moonfall screens: game designer supervision, round 2

I read round 1, the implementer's response, and the UX and level-critic round-1 reviews. I looked at all 32 renders from b47c7834 and compared the levels screen with its mock (`rich2/screens/levels-1280.png`). I read:
- `DuelPlates`, `TurnGlow`, `IdleDim`, `TurnCaption` and `BallsChip` (`MoonfallWindow.Modes.cs:834–990`);
- `CompanionPicker` (`Modes.cs:100–152`) and the challenge and Quick Play paging (`Modes.cs:196–465`);
- `PowerAtWork` and `PowerGlyph` (`Characters.cs:235–378`);
- the level-select words and `FitLine` (`Map.cs:711–796`);
- `AceChip` (`Rich.cs:571`), the menu-button tooltip path (`Menu.cs:478–486`), the renderer's `StageDuel` and the relevant strings.

I re-rendered the duel HUD into my scratchpad to see the states the committed renders do not show:
- `--seconds 1.5` and `--seconds 4`: "Louisoix's shot", the ball in flight;
- `--seconds 8`: "Your shot", at 1280 and 640.

The crops are in my scratchpad `c/`. I wrote nothing into the repo.

## Summary
The Major is resolved. The duel's top rail now says whose turn it is in three ways:
- the shooting side's plate is lit in its companion's colour (jade for Louisoix, copper for Cid) with a rim;
- the waiting plate is dimmed;
- a caption sits under the lit plate.

The caption cycles through "Louisoix is thinking ···", "Louisoix's shot" and "Your shot" as play goes on (my renders `dh-states.png`, `dh-8.png`). The thinking dots are visible, and they are fixed under Reduce motion (`dhrm1280-top.png`). Both names are back at 640. The rails now switch together: the tube and the right rail show the shooter, and a "BALLS n" chip under the waiting plate shows the other side's balls.

Every round-1 Minor is resolved or acceptably answered. Four new or newly found Minors remain:
- a duplicated sentence in the level strip of a face-down or no-pick stage;
- 640 tile names cut mid-word;
- "full last page" paging that repeats most of the previous page;
- the 640 companion captions now reading as codes.

None blocks approval.

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title (and `title-hint`) | APPROVE | APPROVE |
| map | APPROVE | APPROVE |
| far | APPROVE | APPROVE |
| levels | APPROVE (m1, n6) | APPROVE (m1, m2) |
| characters | APPROVE (n3, n4) | APPROVE (m4) |
| quickplay | APPROVE | APPROVE (m3) |
| challenges | APPROVE (m3, n5) | APPROVE (m3, n5) |
| duel (setup) | APPROVE | APPROVE |
| options (and reduce-motion) | APPROVE (n7) | APPROVE (n7) |
| hud | APPROVE (n9) | APPROVE (n9) |
| pause | APPROVE | APPROVE |
| tally | APPROVE | APPROVE |
| duelhud (and reduce-motion) | APPROVE (n1) | APPROVE (n1, n2) |
| **Overall** | **APPROVE** (no Major; four Minors listed) | |

## Round-1 findings: status
| Round-1 finding | Status | Evidence |
|---|---|---|
| [Major] duelhud: turn and thinking not readable | **Resolved** | Glow and rim in the companion's accent, idle plate dimmed to about 60%, caption under the lit plate in three states, dots fixed under Reduce motion, names fitted at 640 (`Modes.cs:879–937`). Crops: `dh1280-top.png`, `dh640-top.png`, `dhrm1280-top.png`, `dh-states.png`, `dh640-8top.png`. |
| [Minor] duelhud: tube and rail show different sides | **Resolved** | The tube and the right rail both show the shooter (Louisoix/Sage's Path with 5 balls; Cid/Brass Wings with 4). The waiting side's balls are on a chip (`Modes.cs:926–937`). See n1 for a residue. |
| [Minor] characters: power-at-work inset generic, missing at 640 | **Resolved** | A real level cut round its green, with each power's own glyph (`Characters.cs:236–378`). Super Guide shows the white run and the jade run on past the bounce. It is present at both sizes. |
| [Minor] characters-640: row-3 captions ran together | **Resolved** | "Fireball · 9 / Path · 10 / Storm · FS" now sit apart (`char640-row3.png`). The copy cost is in m4. |
| [Minor] selection glow is a hard slab | **Resolved** | It is feathered and lighter on Minfilia and on 3-3 at both sizes (`char1280-row1.png` against the old render). |
| [Minor] levels: captions touch the frame; 640 names crowd Best | **Resolved** | 6-unit inset. Names are one line, and Best clears the rule. Residues are in m2 and n6. |
| [Minor] levels-1280: power text replaces the level line, "He" has no antecedent | **Resolved, with a note** | The strip opens "Level 3-3 of The Night Skyway." and the pronoun is gone. The mock's level flavour line ("A trail on a map…") cannot be restored, because levels carry no description field. That is a content gap, not a screen bug. |
| [Minor] map: aced looks like won | **Resolved** | ACED on 3-2 in the 1280 stage panel. |
| [Minor] challenges: last row under the ornament; no scrolling | **Resolved** | Rows are inset and paged ("1 of 2"). See m3. |
| [Minor] challenges: "none" shows no selection or line | **Resolved** | A 1.6 r ring on a dark under-stroke, and "No companion: no power" under the picker. |
| [Minor] duel setup: companion defaults to none; "1-1" reads "I-I" | **Resolved** | Cid is preselected, and the code is in TrumpGothic. |
| [Minor] copy: "met" should be "reached" | **Resolved** | "…with any companion you have reached, or none." |
| [Minor] title-640: sealed Challenges has no reason | **Resolved** | Padlock, plus the reason on hover and on keyboard focus (`Menu.cs:483`, `Title.cs:298`). |
| [Minor] title-640: hint overflows | **Resolved** | One row under Companions and Options; the text and pills fit. |
| [Minor] options: Peg marks samples blank when off | **Resolved** | Marks show with the switch off at both sizes. |
| [Minor] far: "Not reached" and padlock before levels ship | **Resolved** | "Levels on their way" in slate, no padlock on stop 1, rows "On its way". |
| [Minor] hud: no Ace target | **Resolved** | "ACE 240,000" chip under the score. It turns gilt once reached ("ACE 100,000" on the tally). |
| [Nit] hud margins are a gradient | Open (deferred to the board art pass) | n9 |
| [Nit] tally HUD score differs from TOTAL | **Resolved** | Both read 161,600. |
| [Nit] title-1280 hint is a plain bar | **Resolved** | It is now a journal panel. |
| [Nit] duel setup right half empty | **Resolved** | The opponent's role and line fill it. |
| [Nit] "Try again" on a won challenge | **Resolved** | It says "Play again". |
| Flow note: crest needs a hover glow and tooltip | Resolved in code (not rendered) | See Unverified. |

## Findings

### [Minor] m1. Level strip repeats "Clear the oranges to win the level." on a face-down stage and on a stage with no pick
- Where: `Map.cs:757–762`.
- When the carrier is not met, or the stage is a player-pick with no pick, `does` falls back to `Strings.MoonfallClearTheOranges`.
- Line 762 then appends `MoonfallClearTheOranges` again.
- The strip for stage 2 (twins face down) would read: "Level 2-1 of …. Clear the oranges to win the level. Clear the oranges to win the level."
- The bug predates round 2 (it is the same at 7e34fab6), but it lands on the spoiler shield's own state.
- Found from the code; the renderer cannot stage another stage's level select.
- Fix: make the fallback `does` empty and build `place + (does.Length > 0 ? " " + does : "") + " " + ClearTheOranges`.

### [Minor] m2. levels-640: locked tile names are cut mid-word and appear nowhere else
- Evidence: "Above the C..." and "Ironworks.." (`lv640-tiles.png`).
- `FitLine` cuts at any character (`Map.cs:779–796`).
- The comment at `Map.cs:737–738` says "the strip names the selected level in full". But `EnsureLevelsSelection` (`Map.cs:693–709`) never selects a level that has not been reached, and the 640 map panel lists no levels. So at 640 a locked level's name is never seen whole.
- Locked tiles have a free second caption line, because "opens after 3-3" is drawn on the thumbnail.
- Fix:
  - when `tileLines[i]` is drawn on the thumbnail (the locked and coming cases), wrap the name onto two lines;
  - otherwise cut at a word boundary ("Above the…" rather than "Above the C...").

### [Minor] m3. The "full last page" repeats most of the page before it
- `first = Math.Max(0, Math.Min(page * rows, count - rows))` (`Modes.cs:234`, `Modes.cs:414`).
- Challenges at 1280 have 11 rows (`Modes.cs:522`) for 12 challenges. Page 2 therefore shows rows 2–12: ten rows already seen and one new one.
- Quick Play at 640 (11 rows) shows 9 of the 11 page-1 rows again on "2 of 2".
- Turning a page that moves the list by one row reads as a glitch. With 40 challenges planned, every final page will overlap the one before it.
- Fix (either):
  - label the stepper as a range ("1–11 of 12", then "2–12 of 12") so the overlap reads as scrolling;
  - go back to true pages and let the last page be short. The UX nit was about opening on a 2-row stub, which "open on the page holding the selection" already avoids.

### [Minor] m4. characters-640: the shortened captions read as codes
- Evidence: "Burst · 4", "Draw · 8", "Path · 10", "Storm · FS" (`char640-caps.png`, `char640-row3.png`).
- A bare number beside a power name reads like a shot count; the detail panel uses "3 shots".
- "FS" is an internal abbreviation.
- "Path" and "Storm" lose the power's identity: Sage's Path and Storm Post are what the player learns.
- Fix:
  - at 640, caption drained cards with the power name only, shrunk to the card pitch ("Lunar Burst", "Sage's Path", "Storm Post");
  - let the drained art and the detail panel ("Joins at stage 4 · …") carry the stage;
  - if a stage marker must stay, use "st. 4" and "Far Shore", not bare figures and "FS".

### [Nit] n1. duelhud: the big tube switches owner silently
- On the opponent's turn the left tube and "5 BALLS" are Louisoix's, and the player's balls are on the small chip under YOU.
- A player glancing at the tube can read 5 as their own.
- Fix: tint the tube's BALLS label in the shooter's accent, or put the shooter's short name above it, as the right rail does with the face.

### [Nit] n2. duelhud-640: the opponent's name fits only while the score is short
- With a 4-digit score it is already "LOUI..." (`dh640-8top.png`), and with five or six digits `DuelPlates` drops it (`Modes.cs:870–876`). The face and the caption still carry the identity, so this is acceptable.
- Fix: draw nothing rather than a three- or four-letter stub when fewer than about six letters fit.

### [Nit] n3. characters-1280: the power text runs into the inset
- The text wraps at 300 units from x0+40 (`Characters.cs:165`, `:443`), and the inset starts at x1−36−190 (`:437`). That leaves a gap of about 2 units, and "to" touches the gilt (`char-inset.png`).
- Fix: wrap at about 280.

### [Nit] n4. characters: the Super Guide's first guide dot sits on the inset's gilt top edge
- The glyph's clip is the whole thumbnail, frame included (`Characters.cs:259`).
- Fix: clip to the opening inside the frame.

### [Nit] n5. challenges: "its levels are on their way" is the only row subtitle in lower case
- Where: `Strings.resx:17051`.
- Fix: "Its levels are on their way."

### [Nit] n6. levels-1280: the ACED tag sits about 2 px above the caption plate's bottom rule
- Best on the same plate has more room (`lv1280-caps.png`).
- Fix: raise it 2 units, or centre it on the Best line.

### [Nit] n7. options: the held-Decoration note contradicts Reduce motion's own line
- "everything holds still, whatever this says" against "nothing moves but the game itself".
- Fix: "Reduce motion is on, so the scenery holds still whatever this is set to."

### [Nit] n8. companions grid against the pickers: met-but-not-reached is marked differently
- The pickers add a padlock to a drained medallion (`Modes.cs:126–129`); the companions grid shows only the drained card and "stage N".
- Fix: add the same small padlock to drained cards, for one locked language across the menus.

### [Nit] n9. hud: the margins are still the dark gradient (carried; deferred to the board art pass)

## Screens against the game-design bar
- **State and flow:** clear at both sizes.
  - The title seals Challenges with a padlock and a reason.
  - The map gives ACED/won/next at 1280, and a state line plus a Levels pill at 640.
  - An open-but-empty Far Shore says "Levels on their way".
  - The duel rail always says whose shot it is.
- **Spoiler shield:**
  - face down: the card back, "Not yet met", the power named; unpickable in every picker; never a duel opponent (10 offered);
  - drained: padlocked in the pickers, "stage N" on the grid;
  - the moogle stays face up.

  The only shield-state defect is m1's copy.
- **Companion identity:** strong.
  - The turn glow and caption use the companion's accent.
  - The power-at-work inset is now per power.
  - The duel setup gives the opponent's role and line.
  - The rails show the shooter's face and power.

  m4 is the one place identity thins.
- **Reward moments:** the tally's ACED callout with NEW BEST, the Ace bonus line, and the "Brass Wings ×1" credit. The HUD's Ace chip goes gilt once reached, and the HUD and TOTAL agree.
- **Copy:** reads naturally, apart from m1, m4, n5 and n7.

## Unverified
- **Hover and focus:** tooltips on keyboard focus (the 640 Challenges reason, the Far Shore tab, the picker reasons), and the crest's hover lift, ring, hand cursor and tooltip. These are code-read only.
- **The Ace chip's quiet-to-gilt change at the moment the score crosses the target:** I saw only the before (HUD) and after (tally) states.
- **Code-only checks:** challenges page 2 and Quick Play 640 page 1 (the renderer cannot turn pages); m1's strip on a face-down or no-pick stage.
- **Duel states not covered:** the end of a duel (outcome screen, reward moment); the case where one side runs out and the other "shoots on"; a 6-digit opponent score at 640 (n2 is inferred from `Modes.cs:870–876`).
- **Pause under Reduce motion:** whether the pause's quick-settings Decoration value is also dimmed.
- **Motion and platform:** all motion (glow breathing, the dots' cycle, ambient scenes); the hold fill sweep; Dalamud's font metrics, other window scales, and in-game input.

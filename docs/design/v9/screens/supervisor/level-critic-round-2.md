# Moonfall screens: level-design critic supervision, round 2

**Reviewed:** my round-1 review; the implementer's response; spec-rich2 §1, 2, 4 and 5 and the level method; the committed renders from HEAD b47c7834. In code I read `DuelPlates`, `TurnGlow`, `IdleDim`, `TurnCaption`, `BallsChip` (Modes.cs), `FitLine` (Map.cs), `RichHud`, `LevelPlates`, `AceChip`, `RichCrest` (Rich.cs), `PauseCrest` and `DrawPauseMenu` (Pause.cs), the plain tally (Board.cs), `DrawPlainBar` and `RefreshBarText` (MoonfallWindow.cs), the draw order in Art.cs, and the loader's `InBounds`.

I rendered with the built Release renderer into my scratchpad (`…/scratchpad/lc2/`), never the repo:
- play, pause, tally and duelhud at 1280 and 640;
- duelhud with `--reduce-motion` at both sizes;
- `--decoration off` for play and duelhud at both sizes, and for pause and tally at 640.

Crops are `lc2/c-*.png`. The view mapping came from the renders, and the right wall checks it:
- 1280: x = 132.8 + 1.268·u, y = 31.2 + 1.268·u;
- 640: x = 26.0 + 0.735·u, y = 31.0 + 0.735·u. The wall at u 724.5 maps to px 558.5, as measured.

## Summary
Every round-1 finding is resolved.
- The opponent's name can no longer touch its score.
- Both sides are labelled at 640.
- The turn glow now lifts about 0.20 luma.
- The tally's two scores agree.
- The dots have moved, and the plain-bar and Plain nits are fixed.

The board is unchanged. My play-640 render is pixel-identical to round 1 inside the opening below y 80. At 1280 the opening differs only at the new Ace chip, the moving bucket and a few dust pixels. So the round-1 readability margins stand.

One Major remains, and I missed it in round 1. **On the plain board (Decoration Off), a duel shows only the player's score.** There is no opponent score, no turn cue and no thinking cue, and the ball count is the two sides' sum.

The rich duel HUD has four Minors:
- the opponent's name is cut to 1–2 letters at 640 once the score has five digits;
- the 640 thinking caption runs past the right wall;
- "the twins is thinking";
- the name cache can keep the previous opponent's name.

## Verdicts
| Render | Verdict |
|---|---|
| hud-1280 | APPROVE |
| hud-640 | APPROVE |
| pause-1280 | APPROVE |
| pause-640 | APPROVE |
| tally-1280 | APPROVE |
| tally-640 | APPROVE |
| duelhud-1280 | APPROVE (Minors listed) |
| duelhud-640 | APPROVE (Minors listed) |
| duelhud-reduce-motion-1280 | APPROVE |
| duelhud-reduce-motion-640 | APPROVE |
| plain fallback: play, pause, tally (`--decoration off`) | APPROVE |
| plain fallback: duel (`--screen duelhud --decoration off`, 1280 and 640) | REVISE |

**OVERALL: REVISE** (one Major: the plain board's duel)

## Round-1 findings
| Round-1 finding | Status | Evidence |
|---|---|---|
| [Major] The opponent's name runs into its score | **Resolved** | See below. |
| [Minor] 640 side labels | **Resolved** | YOU is set by `NamePx(13, Jupiter)` (Modes.cs:898) and the opponent by `NamePx(11.5, Axis)` (Modes.cs:871, 917). At 640 both have 7 px caps, measured. The dimmed YOU is about 4.9:1 on its plate (core #898579 on #0F1739). |
| [Minor] The glow is weak and too fast | **Resolved** | Lift just outside the opponent's plate, against the same spot in play with no glow: about 0.19–0.21 at 1280 and 0.20–0.24 at 640 (round 1: 0.07). It is 6 units wide plus an accent rim (`Tint(accent, 0.2)` at 0.95), and breathes `Breath(clock, 3 s, ±15%)` (Modes.cs:881, 964–965). Under Reduce motion it holds: lift 0.21 at breath 1. The waiting plate is dimmed by 40% (Modes.cs:972). |
| [Minor] The crest has no visible cue | **Resolved (code only)** | On hover the moonstone gets a soft lift (alpha 0.25), a white disc of 0.10 over the stone (0.18 while pressed) and a ring tinted toward light (Rich.cs:408–420). It shows the hand cursor and a tooltip (Pause.cs:49–54), and `crestHovered` is reset each frame (Board.cs:160). |
| [Minor] The tally shows two scores | **Resolved** | The plate equals TOTAL: 151,600 at 1280 and 161,600 at 640 (`RefreshBarText`, MoonfallWindow.cs:454). |
| [Nit] The dots' place and pace | **Resolved** | The dots are now inside the caption chip, beside "is thinking", 15 units under the plate and at least 99 units from the launcher's swing. Their period is 2.4 s (Modes.cs:1000), and they hold at 0.85 under Reduce motion, as rendered. |
| [Nit] The plain bar | **Resolved** | It reads "Oranges 19" and "Resume" while paused, and has no button over the tally. The plain tally reads Replay, Map, Next (Board.cs:628–657), as rendered. |
| [Nit] Plain asks for a picture | **Resolved** | `picture = plain ? null : …` (Art.cs:145). My Decoration Off runs logged no missing-picture warning. |
| [Nit] The 640 pause line | **Resolved** | "9 balls · 20 oranges" is AXIS 12 under the title (Pause.cs:100), with 7 px digit and ascender height, measured (`c-pause640-line.png`). |

**The Major in detail.** `DuelPlates` fits the name into `Size(701−618−6) − scoreWidth`, so the name ends at least 6 units before the score's left edge (Modes.cs:870–876). A cut that still does not fit is dropped. LOUISOIX now spans px 917–971 at 1280 against a "0" at 1015–1021 (`c-duel1280-foe.png`). At 640 it spans 481–525 against a score at 537 (`c-duel640-foe.png`). The name and the score can no longer overlap. How short the name gets is a separate Minor below.

## Findings
- **[Major] Plain board (Decoration Off, or while the chrome loads): a duel has no opponent score and no turn.**
  - Both sizes render the same bar: "3-3 The Airship Road · Balls 9 · Oranges 19 · ×1 … 0 · Pause" (`lc2/duel-off-1280.png`, `duel-off-640.png`).
  - `DrawPlainBar` (MoonfallWindow.cs:466–511) shows `scoreText`, which in a duel is the player's score only (line 454).
  - Nothing shows the opponent's score, whose shot it is, or that the opponent is thinking. A click during the opponent's thought does nothing, with no explanation.
  - "Balls 9" is `g.BallsLeft`, the two sides' sum (5 + 4 here). The rich tube shows the shooter's 5. The 640 pause line has the same count.
  - Decoration Off is a user setting, so this path is reachable on purpose, not only during loading.
  - I missed this in round 1; it was there then too.
  - Fix:
    - In a duel, make the bar's parts: `YOU {you}` · `{NAME} {foe}` · the turn caption text ("Your shot" / "Louisoix is thinking…" / "Louisoix's shot") · `Balls {TubeBalls(g)}`. The rest of the bar stays as it is.
    - Use `TubeBalls` for the duel's pause line too.
    - Clip from the left as the bar does now, so the scores and the turn survive at 640.
- **[Minor] duelhud: the fitted name is cut to a stub at ordinary duel scores, and the shrink step the response describes is not in the code.**
  - `px` is fixed at `NamePx(v, 11.5f, Axis)` (Modes.cs:871). `FitLine` (Map.cs:779–796) only cuts, down to one letter. So the name never shrinks before it is cut.
  - Projected from glyph widths, calibrated on measured widths:
    - LOUISOIX: 54 px at 1280 and 44 at 640;
    - SCORE: 37 px at 1280, against 38 projected;
    - "151,600": 60 px at 1280; "161,600": 35.5 px at 640;
    - the room is 97.6 px at 1280 and 56.6 at 640, less the score.

    | Score | 1280 | 640 |
    |---|---|---|
    | 1 digit | all whole | KAN-E-S…; the rest whole |
    | 4 digits | LOUISOIX whole; URIANG…, THE TW…, KAN-E-… | LOUI…, URIA…, THE…, KAN-… |
    | 5 digits | LOUIS…, URIAN…, THE T…, KAN-E… | LO…, URI…, TH…, KA… |
    | 6 digits | LOUI…, URIA…, THE…, KAN… | about U…, L…, T…, K… |
  - Duel sides are mostly at five digits from mid-duel on, so at 640 the plate carries a two-letter stub most of the time.
  - Fix:
    - (1) At 1280, shrink the name toward the AXIS label floor (12.0 px, which is −18%) before cutting. LOUISOIX then fits whole at five digits.
    - (2) Never show fewer than 4 letters. Drop the name instead; the face ring and the caption carry it.
    - (3) Better at 640: put the opponent's name in the caption row on the player's turn as well. The chip under the opponent's plate would read "LOUISOIX · BALLS 4", where there is room.
- **[Minor] duelhud-640: the thinking caption runs past the right wall.**
  - `TurnCaption` centres its chip on the plate's middle, u 652 (Modes.cs:986–987). At 640 the Jupiter caption is held at its floor, 13.7 px, which is 1.56× its design size in units.
  - "Louisoix is thinking" already spans px 450.6–560.5, or u 578–727.2. That is 2.7 units past the wall at 724.5, on the dark reveal, 1.5 px short of the gilt at px 562 (row profile at y 68; `c-duel640-foe.png`).
  - Projected:
    - "Kan-E-Senna is thinking": the chip's right edge is about u 734, roughly 5 units over the gilt band;
    - "the twins is thinking": it touches the gilt;
    - at 1280 every name stays inside: Louisoix's chip ends at u 705, Kan-E-Senna's at about 709.
  - Fix: right-align the caption and the balls chip to the plate's right end (x1 = 716) instead of centring them. They then grow left, toward open sky; the launcher's swing ends at u 479. Or clamp x1 ≤ 716 and shift left.
- **[Minor] duelhud-640: the caption's small caps are under the label floor.** Jupiter sets lower case as small caps. In "Louisoix is thinking" at 640, the L is 7 px but every other letter is 6 px (rows 66–71, `ink.py` on px 455–545 × 60–76). The caption is the only turn cue at 640, so 6 px letters miss the floor's intent, though the rule measures caps. Fix: set the caption in capitals (`ToUpper(culture)`, as the name plate does), or raise its floor so the small caps reach 7 px. Either one makes the wall finding above more urgent.
- **[Minor] The twins' captions are ungrammatical.**
  - `ShortName(Twins)` is "the twins" (Title.cs:158). With `MoonfallDuelThinkingFormat` "{0} is thinking" and `MoonfallDuelTheirShotFormat` "{0}'s shot", the HUD shows "the twins is thinking" and "the twins's shot", starting in lower case. Every other name starts with a capital.
  - The twins are met at stage 2, so they are an early opponent.
  - Fix: add strings for the twins ("The twins are thinking", "The twins' shot"), or keep a capitalised, plural-aware form with the companion.
- **[Minor] The name-fit cache key leaves out the opponent (and the fonts).**
  - `duelHudFor` is (you, foe, language, scale) and `duelBallsFor` is (youBalls, foeBalls) (Modes.cs:828–829, 852).
  - Suppose a player leaves a duel before the first shot resolves (0–0, full tubes) and starts one against another opponent. The key is unchanged, so the plate and captions keep the old name ("LOUISOIX", "Louisoix is thinking") until a score changes.
  - The fit is also not redone when the game fonts land, unlike the medallion's (`powerNameFonts`, Rich.cs:745–751). This is unlikely, because the setup screen builds the fonts first.
  - Fix: add the duel's opponent (or `ReferenceEquals` on the duel) and a fonts-ready flag to the key.
- **[Nit] The top-band chips hide the ball.**
  - Two pieces sit inside the opening under the rail's foot (y 41):
    - the Ace chip (u 655–712 × 39–54 at 1280; 636–712 at 640);
    - the duel's caption and balls chip (y 43–58).
  - `RichHud` draws after `ArtBall` (Art.cs:224–227). A ball bounced up there passes under a 15-unit chip for a moment.
  - No piece comes near: the highest is base-p3's peg at (571.5, 82.5) r10, at least 14.5 units below the widest projected caption. The launcher's swing is at least 99 units away.
  - Fix: draw the ball after the HUD's chips, or give the chips 0.6 alpha while the ball is above y 66.
- **[Nit] The loader lets a peg hide under the top rail and the chips.** `InBounds` only requires `y − r ≥ Ceiling (0)` (MoonfallLevelLoader.cs:425–430). No shipped level or pilot comes within 40 units, but a future level could put a peg under the rail or a chip. Fix: a top limit of y − r ≥ 66 (the chips' foot at 58 plus the 8-unit clearance).

## What passed, with measurements
- **Readability margins:** unchanged from round 1 (1280: purple 0.425, orange 0.355, blue 0.415; 640: purple 0.426, orange 0.353). The opening's pixels are identical to my round-1 renders at 640 below y 80. At 1280 they differ only at the Ace chip, the moving bucket and dust (`lc2/diff1280.png`). The clearance-field change since round 1 is a pure speed-up: `ArcBound` is a true lower bound, so no distance changes.
- **Floors at 640, measured:**
  - Ace chip: ACE caps 7 px, digits 8 px;
  - balls chip: BALLS 7 px, count 7–8 px;
  - YOU: 7 px;
  - the opponent's name: 7 px;
  - the caption's capital: 7 px (its small caps 6 px, see above);
  - duel scores: about 11–12 px;
  - the pause line: 7 px.
- **The glow:** lift 0.19–0.24, against 0.07 in round 1. It breathes ±15% over 3 s and holds under Reduce motion.
- **The dots:** 2.4 s; 0.40–0.95 alpha in turn; 0.85 and still under Reduce motion (`c-duelrm1280-foe.png`).
- **The name against the score:** at least 6 units apart by construction. As rendered: LOUISOIX ends at px 971 and the score starts at 1015 (1280); 525 against 537 (640).
- **The Ace chip:** under the score plate, inside u 712, quiet ("ACE 240,000"). On the tally it reads gilt at 151,600 ≥ 100,000. It is hidden in duels and challenges (Rich.cs:573).
- **Clearance:**
  - the chips and caption are at least 14.5 units from the highest piece in any level or pilot, and at least 99 units from the launcher's swing (pivot (400, 87), barrel 73, ±85°);
  - the 1280 caption stays inside the opening for every name;
  - no brick or orbit reaches above y 130.
- **Pause:** the 640 panel spans y 70–467, below the top rail. "Restart level" and "Leave to the map" are garnet, with their hold notes.
- **Tally:** the plate and TOTAL agree at both sizes; the rich and plain buttons share one order.

## Unverified
- The player's-turn state ("Your shot", and the balls chip under the opponent's plate) and "Louisoix's shot": the renderer stages only the thinking state, so I judged these from the code.
- The name-fit table: projected from measured widths (LOUISOIX, SCORE, "151,600", "161,600") and calibrated letter widths. I did not render any other opponent or a duel with 4–6-digit scores.
- The caption's overrun for Kan-E-Senna and the twins: projected from the Louisoix chip at 640.
- The crest's hover and press, and the hand cursor: from code only, since the renderer has no mouse.
- The breath rate: from the code; I measured single frames, not a sequence.
- The stale-name path: from code; not reproduced.
- The duel tally, the plain duel tally, other levels' scenes (only base-01 was rendered), the cold-start swap from the 1x to the 2x tier, and in-game rendering through Dalamud's fonts.

*Process note: one mistaken renderer call (`--help` as the output path) wrote a stray PNG named `--help` in the worktree root. I deleted it at once, and I left nothing else in the worktree. The untracked `docs/design/v9/screens/supervisor/game-designer-round-2.md` there is another reviewer's file, not mine.*

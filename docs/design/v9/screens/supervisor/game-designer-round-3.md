# Moonfall screens: game designer supervision, round 3

Before reviewing I read four things: my round-2 review, the implementer's round-2 response (which includes the shield design and the era table), the level-critic and UX round-2 reviews, and the Far Shore section of `moonfall-modes.md` (l.55–68). I looked at all 38 renders at c9547fd3, and at the new ones closely: `far-veiled-*`, `duelhud-plain-*`, `scene-veiled-1280` and `scene-shown-1280`.

In code I read:
- `MoonfallPlaces.cs`, `MoonfallShield.cs`;
- `MoonfallModes.cs`: `Slot`, `Stages`, `Continue`, `Adventure`, `QuickPlay`, `ChallengeState`, `StartDuel`;
- `MoonfallLooks.Stop`, `MoonfallProgress.RecordLevel` and `MoveCounts`;
- `MoonfallWindow.Map.cs`: `StageNameShown`, `LevelNameShown`, `PlayLevelName`, `ShieldPlaceholder`, `ShieldMark`, both stage panels, `Legend`, the map words, the level-select words, `FitLine`;
- the challenge rows and words in `MoonfallWindow.Modes.cs`;
- the characters words in `MoonfallWindow.Characters.cs`;
- `MakeTitleWords` and `Continue` in `MoonfallWindow.Title.cs`;
- `HasNext` and `Next` in `MoonfallWindow.Flow.cs`;
- `FollowShield` and `DrawPlainBar` in `MoonfallWindow.cs`;
- `ShieldText.cs`, the shield wiring in `Plugin.Moonfall.cs`, `MoonfallGameArt.PickScene` and `VeilChanged`, and the scene recipes;
- `MoonfallShieldTests.cs` and `MoonfallShieldGameDataTests.cs`.

I re-rendered into my scratchpad `…/scratchpad/gd3/`, never into the repo:
- the far map at `--story 0` (stage 3 at 1280, stage 5 at 640);
- `--story 3` (stage 10 at 1280, stage 12 at 640);
- `--story 5` (stage 9);
- `levels --stage 9 --story 3` at both sizes;
- `title --story 0`.

Every run used `--text-check`, and every one reported 0 leaks. Crops are in the same folder: `fv1280-stops`, `fv640-stops`, `fv1280-legend`, `fv1280-panel`, `fv640-panel`, `far-s0-stops`, `far-s0-st5-640`, `dhp640-bar`, `dh1280-tubelabel`, `lv640-tiles`, `lv1280-tiles`.

## Summary
Every round-2 Minor and Nit is resolved; n9 stays deferred to the board pass, as agreed. "The Floating Market" is in place in the data, the doc and the tests.

**What works in the shield:**
- Each FFXIV area is tagged with the right expansion.
- No mode can start a veiled level (`Slot` puts Veiled first, and every start goes through `Reached`).
- The placeholder answers with the shield's own hover and "Reveal this name".
- The shield's mark (slate) is clearly a different sign from the progress padlock (gold).
- No veiled name or area is drawn anywhere I could render.

**Where it does not hold together:** the shield meets Moonfall's linear Adventure badly, and that gives two Majors.
- **M1:** when the player's next level sits on a veiled stage, the title's Continue card says "Every level built so far is won. More of the road is on its way." That is false, and it hides the way to reveal the stage. The tally also goes quiet at that moment.
- **M2:** stages that are safe for the player's story but sit after a veiled one are walled off as well. The worst case is the moogle, the one companion with no story gating: Storm Post can only be reached after Endwalker, or after two or three reveals.

Both are latent until the Far Shore's levels ship, but they are design flaws in the shield as built, so I cannot approve it as it stands. Seven Minors and six Nits follow.

## Verdicts
| Screen | 1280 | 640 |
|---|---|---|
| title (and `title-hint`) | REVISE (M1: the Continue card at a veiled frontier, code-read) | REVISE (M1) |
| map (Moon Road) | APPROVE | APPROVE |
| far (shown: `far-*`) | APPROVE | APPROVE |
| far (veiled: `far-veiled-*`) | REVISE (M2; m1, m2, m3; n1) | REVISE (M2; m1, m2, m3; n1, n2) |
| levels | APPROVE | APPROVE (n3) |
| characters | APPROVE (m7) | APPROVE |
| quickplay | APPROVE | APPROVE |
| challenges | APPROVE (m4, latent) | APPROVE (m4, latent) |
| duel (setup) | APPROVE | APPROVE |
| options (and reduce-motion) | APPROVE | APPROVE |
| hud | APPROVE (n9 deferred) | APPROVE (n9 deferred) |
| pause | APPROVE | APPROVE |
| tally | APPROVE (M1: silent at the veil, code-read) | APPROVE (M1) |
| duelhud (and reduce-motion) | APPROVE | APPROVE |
| duelhud-plain | APPROVE | APPROVE (m6) |
| scene-veiled / scene-shown | APPROVE as drawn (m5 on the policy) | not rendered |
| **Spoiler shield (design)** | **REVISE** (M1, M2) | |
| **Overall** | **REVISE** (two Majors) | |

## Round-2 findings: status
| Round-2 finding | Status | Evidence |
|---|---|---|
| [Minor] m1: the strip said "Clear the oranges" twice | **Resolved** | The power text is empty when there is none, and the sentence is joined once (`Map.cs:842–847`). |
| [Minor] m2: 640 locked tile names cut mid-word | **Resolved (the implementer's alternative)** | Cut at a word's end ("Above the…", "Ironworks…"), with the full name in a tooltip on hover or focus (`Map.cs:817–824`, `FitLine` `Map.cs:864–894`). A small residue is in n3 (`lv640-tiles.png`). |
| [Minor] m3: the "full last page" overlap | **Resolved** | True pages. `quickplay-640` "2 of 2" shows the two remaining rows, with the selection on them. |
| [Minor] m4: 640 companion captions read as codes | **Resolved** | "Super Guide", "Multiball", "Brass Wings", "Lunar Burst", "Flippers", "Moon Gate", "Moonbloom", "Draw", "Fireball", "Sage's Path", "Storm Post". No figures, no "FS" (`characters-640.jpg`, `Characters.cs` 640 branch). |
| [Nit] n1: the tube switched owner silently | **Resolved** | On Louisoix's turn the tube's BALLS label is jade (`dh1280-tubelabel.png`). |
| [Nit] n2: name stubs at 640 | **Resolved (code-read)** | The name shrinks to the floor, is cut at a word's end, and is left out below four letters (`Modes.cs:915`). A 6-digit score was not rendered. |
| [Nit] n3: the power text ran into the inset | **Resolved** | The text wraps at 280 (`Characters.cs`). |
| [Nit] n4: a glyph sat on the inset's gilt | **Resolved** | The glyph is clipped to the opening, inset 4% (`Characters.cs`). |
| [Nit] n5: "its levels are on their way" in lower case | **Resolved** | "Its levels are on their way." (`challenges-1280.jpg`). |
| [Nit] n6: ACED against the rule at 1280 | **Resolved** | The plate is 60 units tall. ACED on 3-2 clears the rule (`lv1280-tiles.png`). |
| [Nit] n7: the held-Decoration note contradicted Reduce motion | **Resolved** | It now reads "Reduce motion is on: nothing moves at any setting; Off still draws the plain board." |
| [Nit] n8: no padlock on drained companion cards | **Resolved** | Dimmed cards carry the pickers' padlock (`characters-640.jpg`). |
| [Nit] n9: the HUD margins | Deferred to the board pass, as agreed | |

## The shield, as a game designer

### Era tagging (the response's table)
Every area is tagged with the right expansion: Limsa Lominsa Lower Decks, Western Thanalan, Western La Noscea and Coerthas Central Highlands are A Realm Reborn; The Sea of Clouds and The Churning Mists are Heavensward; The Ruby Sea and Kugane are Stormblood; Il Mheg is Shadowbringers; Old Sharlayan, Labyrinthos and Mare Lamentorum are Endwalker. The game-data test checks each area's expansion against the shield's own placement (`MoonfallShieldGameDataTests.cs`).

The real shield reveals an area only once the main scenario reaches the quest that introduces it (`SpoilerMask.PlacedMasked`). So the moon (Mare Lamentorum) stays hidden until mid-Endwalker, and Il Mheg until mid-Shadowbringers. That granularity is right for FFXIV.

The problem is the *choice* of area for some stages, read in road order. The eras by stage are A Realm Reborn, A Realm Reborn, Heavensward, Stormblood, A Realm Reborn, nowhere, Shadowbringers, Stormblood, Endwalker, Endwalker, Heavensward, Endwalker. Because the road's eras go up and down, the first veiled stage arrives early (see M2). Thematically, three of the tags are loose:
- Kan-E-Senna, Gridania's Elder Seedseer, is placed in Il Mheg (Shadowbringers);
- Raubahn, of Ul'dah, is placed on the Ruby Sea (Stormblood);
- the moogle, who per `moonfall-modes.md` l.98 "has no story names", is placed in Heavensward's Churning Mists.

The scenes are tagged correctly, but see m5 for what the veil costs the Moon Road.

### Veiled against not reached
The two read as different states: a slate eye-slash against a gold padlock, a legend row, "Past your story" against "Not reached", and a slate button with the mark. But a veiled stop shows its companion's face at full colour, while a stop that is only progress-sealed is drained. So the closed-by-story stops look *more* open than the merely sealed ones (m1).

### Gating
Every entry point is closed: Adventure, Quick Play, duels and challenges (`MoonfallModes.cs:170–180`, `:342–346`, `StartDuel` going through `QuickPlay`), and the tests cover each one at every era boundary. It is sound against starting a veiled level. It is not sound against *collateral* walls (M2), and the moment the wall is hit is not communicated (M1).

### The reveal path
The placeholder's own hover says "Right-click to reveal it for this session." But:
- at 1280 the panel shows the reason and the reveal only on hover or focus of the Play button or the stop;
- the 640 line drops the reveal;
- the copy never says the reveal lasts only for this session;
- the challenges screen points the player at a name it does not show (m2–m4).

### Copy and the mark
- "Past your story", the legend's "past your story" and "Runs past your story." read well, and stand well beside "Not reached".
- The long line is clear but promises too much (m2).
- The mark is distinct at 1280. At 640 it reads as a hatched blob, and its slate colour carries it (n2).

## Findings

### [Major] M1. At a veiled frontier the title says the road is finished, and the tally says nothing
- **Where:** `MoonfallModes.Continue` (`MoonfallModes.cs:208–220`) returns null when the next Adventure level's slot is not Open. A veiled slot is not Open, so `MakeTitleWords` falls through to "The road goes on" / "Every level built so far is won. More of the road is on its way." (`Title.cs:144–151`, strings `MoonfallRoadGoesOn` and `MoonfallRoadGoesOnLine`). Continue then just opens the map (`Title.cs:378–390`).
- **Also:** the Adventure pill's subtitle falls back to "The Moon Road · stage 11 of 11" while the player is in the Far Shore (`Title.cs:98`).
- **On the tally:** after the player wins the last level before a veiled stage, `HasNext` is false (`Flow.cs:430`), so the tally offers Replay and Map with no word on why there is no Next.
- **Why it matters:** this is exactly where the shield meets progress, and the main entry point tells the player they are done and should wait for an update. A player has no reason to look for a right-click reveal on the map.
- **Latent:** it shows only once Far Shore levels ship. No test stages a frontier on a veiled stage: `MoonfallShieldTests.Everything` marks everything reached.
- **Fix:**
  - Have `Continue()` (or a sibling) report "next level is veiled", with the stage.
  - On the title card, show the stage's placeholder name with its hover and right-click, the line "Past your story", and Continue opening the map with that stage selected.
  - On the tally, when Next is absent because of the veil, show one line ("Stage 3 is set past your story") and make Map select that stage.
  - Keep the subtitle on the Far Shore.
  - Add a test: a frontier at a veiled stage gives the veiled Continue state, not "road goes on".

### [Major] M2. One veiled stage walls every stage after it, including ones the story allows, and the moogle is closed until after Endwalker
- **Where:** progress is a frontier (`MoonfallProgress.MoveCounts`, `:296–320`). A level is Open only at or below the count of consecutive wins (`MoonfallModes.cs:176–178`). A veiled stage stops the frontier, so every stage after it is Sealed whatever its own era.
- **What each story level gets** (by era alone; within an expansion the real shield's area-by-area reveal moves this a little):

| Story reached | First veiled stage | Playable | Story-safe but walled |
|---|---|---|---|
| A Realm Reborn | 3 | stages 1–2 (10 levels) | 5, 6 |
| Heavensward | 4 | stages 1–3 | 5, 6, 11 |
| Stormblood | 7 | stages 1–6 | 8, 11 |
| Shadowbringers | 9 | stages 1–8 | 11 |

- **Seen:** `far-s0-st5-640.png`: at A Realm Reborn, stage 5 "The Admiral's Sea" says "Win the stages before it to reach it" with a padlock, but stages 3 and 4 before it are veiled and cannot be won. `far-s0-stops.png` shows the alternating pattern: shield, shield, padlock, padlock, then shields.
- **The moogle:** it is reached at Far Shore stage 11 (`moonfall-modes.md` l.98), which sits after three Endwalker-tagged stages. So Storm Post, a whole companion and power, is closed to every pre-Endwalker player unless they reveal Old Sharlayan and Labyrinthos (and The Churning Mists before Heavensward). That contradicts the moogle's own design: "met in the first hours of any start".
- **Why it matters:** the owner's rule closes the stage set past the story, not the story-safe stages behind it. Over-gating costs whole companions, and it makes the only way forward a series of spoiler reveals.
- **Fix** (owner's call; either works, and (b) is robust to future content):
  - **(a) Retag so the road's eras never go down.** For example:
    - stage 3, the Skyward Deck: nowhere (an airship deck over open sky, our own painting, as stage 6 is);
    - stage 4, the Sunlit Isles: Eastern La Noscea's Costa del Sol (A Realm Reborn);
    - stage 7, the Floating Grove: the Central Shroud or Gridania (A Realm Reborn, Kan-E-Senna's own wood);
    - stage 11, the Courier's Wake: nowhere (the moogle has no story names).

    The walls then fall at stage 8 for A Realm Reborn and Heavensward players and at stage 9 for Stormblood and Shadowbringers players, which is the voyage's last leg. The moogle is still behind stages 9–10, so this alone does not fix Storm Post.
  - **(b) Let Adventure step over a veiled stage.** When the frontier reaches a veiled stage, the first level of the next stage that is not veiled opens. The veiled stage waits, and its levels stay unwon until the story or a reveal opens it. "All won" and the Far Shore's completion still count it. Show the skipped stage on the map with the shield's mark and its own "Past your story" line.

  Whichever you choose, add a test that walks the road at each era with nothing revealed and asserts that every stage not set past the story is reachable.

### [Minor] m1. A veiled stop looks more open than a sealed one
- **Where:** `MoonfallLooks.Stop` drains only progress-sealed portraits (`MoonfallLooks.cs:139`). A veiled one gets `Dim` (the ring and the number) but keeps a full-colour face, both on the map and in the panel's `StageFace`.
- **Evidence:**
  - `fv1280-stops.png`: Y'shtola (9) and Louisoix (10) are bright beside drained Tataru (8) and the moogle (11);
  - `far-s0-1280.png`: eight bright veiled faces against drained 5 and 6;
  - `fv640-stops.png`.

  Brightness is the strongest open/closed cue on this map, and here it points the wrong way.
- **Fix:** drain veiled portraits too, or give them their own cool slate veil (desaturate plus an indigo wash) that is distinct from the progress drain. Keep the mark. Apply it in the panel's `StageFace` as well.

### [Minor] m2. The veiled line promises an opening the reveal may not give, and hides that the reveal lasts one session
- **Where:** `MoonfallStageVeiledLine` (`Strings.resx:17190`): "…it opens when your story gets there, or when you reveal the place (right-click its name)."
- **Problem 1:** `Stages` makes Veiled override Sealed (`MoonfallModes.cs:195`). A stage that is both past the story and not reached (stage 9 in `far-veiled-1280`, where the Far Shore stands at 0 of 12) does not open on a reveal: it becomes "Not reached".
- **Problem 2:** the reveal is for the session (`SpoilerRevealThisSession`; the hover says "for this session"). The stage closes again after a reload, which is surprising after the player has played in it.
- **Fix:** split the facts.
  - Veiled: "Set past your story. Right-click its name to reveal the place for this session."
  - When it is also not reached, add "Win the stages before it to reach it."
  - Optionally show the padlock beside the mark when both apply.
  - The response's "That reveal opens the stage" should say "lifts the veil".

### [Minor] m3. At 1280 the reason and the reveal are hover-only, and at 640 the visible line drops the reveal
- **At 1280:** the panel draws no state line (`StagePanel`, `Map.cs:461–518`). The only words are five identical "Past your story" rows and a "Past your story" button. The reason and the reveal live in the Play button's tooltip (`Map.cs:515`) and the stop's tooltip.
- **At 640:** the panel shows "Opens when your story gets there" (`MoonfallStageVeiledShort`), with no reveal and no tooltip on the stops at that size (`fv640-panel.png`).
- **Fix:**
  - At 1280, replace the five repeated rows with the codes alone (or a single row) and print m2's line in the panel.
  - At 640, use a short line that keeps the way in, such as "Past your story · right-click the name to reveal".

### [Minor] m4. Challenges: the veiled line asks the player to right-click a name that is not on the screen (latent)
- **Where:** a veiled challenge's detail uses `MoonfallStageVeiledLine` (`Modes.cs:580`). The challenges screen shows only level codes ("Levels: 9-1"), so there is nothing to right-click.
- **Latent:** no shipped challenge runs through the Far Shore yet.
- **Fix:** give it its own line: "Runs through stage 9, set past your story. Reveal its place on the map (right-click the stage's name) to play it." Optionally add a "Map" pill that opens the map on that stage.

### [Minor] m5. The scene veil leaves the Moon Road's first stage without paintings, and the title backdrop is outside the rule
- **Where:** `PickScene` returns null for a veiled recipe (`MoonfallGameArt.cs:600`), so the bare night sky stands in (`scene-veiled-1280.jpg`). But every veilable recipe already declares a story-safe fallback (`"fallback": "moon-road-night"` in `lantern-night.json`, `holy-see.json` and `airship-road.json`).
- **The cost:**
  - base-04 (Kugane, Stormblood) is plain for every pre-Stormblood player.
  - Under the real shield, Coerthas Central Highlands stays hidden until the A Realm Reborn main scenario reaches it, so base-02 (holy-see) is probably plain for new characters too (inferred, not verified).
  - That makes up to two of the first four levels a new player meets plain. The runtime art round already rejected base-04 being a near copy of base-01 (`lantern-night.json` comment); a blank sky is a step further back.
- **The title:** the title backdrop is Sohm Al (`MoonfallBackdrops.cs:8`, `-nowloading_base07`), a Heavensward place shown to everyone, untagged. The policy is inconsistent: a Kugane painting is veiled, a Sohm Al painting is not.
- **Fix:**
  - Keep the bare sky for a veiled *stage* (the owner's "no scene art").
  - For a level whose stage is open but whose scene's place is hidden, draw the recipe's declared fallback.
  - Decide whether menu backdrops follow the same rule, and write that decision into `moonfall-modes.md`. If they do, choose a title painting every story shows, or tag it.

### [Minor] m6. duelhud-plain-640: "Oranges" is cut mid-word to "Ora", and the multiplier is lost
- **Where:** the left run is clipped at the right group's edge (`MoonfallWindow.cs:569–587`). At 640 a duel's bar reads "LOUISOIX IS THINKING 3-3 The Airship Road Balls 5 Ora | LOUISOIX 0 YOU 0" (`dhp640-bar.png`).
- **Why it matters:** the oranges left are game state the player needs, and the level name is not.
- **Fix:**
  - Lay out whole parts in priority order: the turn, the balls, the oranges, the multiplier, then the level code, then the level name.
  - Drop the lowest-priority whole part when space runs out; never clip mid-word.

### [Minor] m7. Placeholders drawn without the shield's hover and reveal
- **Where:** only the stage panel's name uses `ShieldPlaceholder` (`Map.cs:470`, `:555`). Other strings that hold the placeholder draw it as plain text:
  - the characters detail's "Joins at stage 9 … Endwalker area 1" (`Characters.cs:176–179`, drawn at `:442`);
  - the stop tooltip (`Map.cs:699`);
  - the level-select header and strip (`Map.cs:846`, `:920`, `:931`), which are reachable only after a reveal or on a level-select path I could not stage.
- **Why it matters:** `ShieldText`'s contract is that every placeholder answers to hover and right-click.
- **Fix:** route these through `ShieldPlaceholder`, or leave the stage name out while the stage is veiled ("Joins at stage 9 of The Far Shore").

### [Nit] n1. "ENDWALKER AREA I": Jupiter's 1 reads as a letter
- **Where:** `fv640-panel.png` and `fv1280-panel.png`. This is the same problem round 1 fixed for level codes.
- **Fix:** set the placeholder's digits in TrumpGothic, or draw the placeholder in Axis.

### [Nit] n2. At 640 the shield's mark is a hatched blob
- **Evidence:** `fv640-stops.png`. Its slate colour still tells it from the gold padlock.
- **Fix:** raise its minimum size at 640 (s ≥ 6), or simplify the glyph to a closed eye.

### [Nit] n3. A 640 tile name can be cut on an article ("Above the…"), and the ellipsis shows as two dots
- **Evidence:** `lv640-tiles.png`.
- **Fix:** drop a trailing article or preposition before the ellipsis, and use the font's "…".

### [Nit] n4. The renders stage the twins as not met at Shadowbringers
- **Where:** the renderer's NPC stand-in always hides Alisaie (`Program.cs:516`), whatever `--story` says. So `far-veiled-*` shows a card back on stage 2 at Shadowbringers.
- **Fix:** derive the NPC stand-in from `--story`.

### [Nit] n5. The legend's "past your story" row shows when nothing is veiled
- **Evidence:** `far-s5-1280.png`, at Dawntrail.
- **Fix:** draw the row only when a stop is veiled.

### [Nit] n6. The shield tests only stage "everything reached"
- **Where:** `MoonfallShieldTests.Everything`.
- **Fix:** add the frontier cases from M1 and M2.

## Regressions
- Outside the shield, nothing regressed that I could see across the 32 re-made renders.
- The new plain duel bar has m6 at 640.
- The Moon Road map, the title, level select, companions, Quick Play, challenges, the duel setup, options, the HUD, pause, the tally and the rich duel HUD all match round 2 or improve on it.

## Overall verdict
**REVISE.** Two Majors remain, both in how the shield meets Adventure's linear progress:
- **M1:** the false "road is finished" message at a veiled frontier.
- **M2:** collateral walls that also close the moogle until after Endwalker.

The shield's mechanics, the era tags and the leak-proofing are sound. M1 is a few lines plus a test. M2 needs the owner to choose between retagging places and letting Adventure step over a veiled stage. Once both are addressed, and m1–m3 with them, I expect to approve.

## Unverified
- **Code-read only:** M1 and M2, because the renderer cannot stage shipped Far Shore levels or a frontier on a veiled stage. Likewise the tally at a veiled frontier, and the m4 challenge line, since no shipped challenge is veiled.
- **The real shield:** the area-by-area masking for a fresh A Realm Reborn character (that Coerthas Central Highlands hides base-02's scene, m5) is inferred from `SpoilerMask.PlacedMasked`. I did not run the game-data tests, to keep the worktree untouched.
- **The reveal in game:** "Reveal this name" from the panel, the hover's three lines, the remaking of the menus' words through `FollowShield`, the lifting of the veil in place, and the closing again after a plugin reload.
- **Whether FFXIV's own loading screens already show the Kugane and Sohm Al paintings to early players.** That would change how much the scene veil and the backdrop policy matter (m5).
- **Hover, focus and motion:** every tooltip and focus path, the glow breathing, Dalamud font metrics, other window scales, and in-game input.
- **Duel states not rendered:** a 6-digit opponent score at 640 (n2 of round 2) and the end of a duel.


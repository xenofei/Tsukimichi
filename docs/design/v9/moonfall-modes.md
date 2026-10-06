# Moonfall: modes and progression (plan v9 G7)

Status: built in Core on 5 October 2026, with tests; no screens yet. This document is for whoever builds the menus. It lists the state machines, the data formats and the Core APIs each screen calls. Everything here is pure Core code in `Tsukimichi.Core/Moonfall/`: no ImGui, no clock, no file access except where noted.

Sources: `docs/feature-plan-v9.md` (G6, G7, decisions 7 to 28), `docs/design/v9/rich2/characters.md` (the cast and the spoiler rules), `docs/design/v9/rich2/spec-rich2.md` (stage names, screens) and `docs/research/plan-v9/peg-mechanics.md` (§6, the original's modes). `[R §n l.N]` cites a research line, and `[J]` marks a judgement call.

## 1. The one entry point: `MoonfallModes`

The menus read everything through one object:

```csharp
var modes = new MoonfallModes(
    MoonfallCampaigns.LoadBuiltIn(),          // the shipped levels, by id
    progress,                                  // MoonfallProgress.Load(path); MoonfallModes updates it, so save it after
    MoonfallStory.FromShield(spoilerMask),     // the viewed character's spoiler shield
    MoonfallChallenges.LoadBuiltIn().Challenges);
```

| Screen | Calls | Returns |
|---|---|---|
| Title: Continue card | `Continue()` | the next Adventure level (`MoonfallLevelPlace`), or null when every shipped level is won or the next one isn't authored yet |
| Title: "Duel against …" | `TitleOpponent()` | Louisoix once met, else the first companion met |
| Adventure map | `Stages(campaign)`, `CampaignOpen(campaign)` | each stage: `Done` / `Open` / `Sealed`, `Here`, its companion's state, and its five `MoonfallLevelSlot`s |
| Level select | `Slot(campaign, index)` | `Missing` / `Sealed` / `Open` / `Cleared`, best score, `Aced`, the Ace score |
| Play an Adventure level | `Adventure(campaign, index, picked)` then `start.Create(seed)` | a `MoonfallStart` (level, number, companion, balls, oranges), or null when sealed or missing. On the "Your Pick" stage, pass an `Available` companion |
| Characters grid | `CompanionState(c)`, `MoonfallCompanions.All` | `NotMet` (card back), `MetNotReached` (dimmed, "stage N"), `Available` |
| Quick Play | `QuickPlayLevels()`, `QuickPlayCompanions()`, `QuickPlay(levelId, companion)` | levels reached; only `Available` companions; null for anything else |
| Tally | `FinishLevel(start, game)` once the game is `Won` or `Lost` | `MoonfallLevelResult`: score (with the Ace bonus), `AceBonus`, `Aced` (first time), `NewBest`, `Unlocked` |
| Challenges | `ChallengesOpen`, `ChallengeList()`, `StartChallenge(id, companion, seed)`, `FinishChallenge(run)` | see §5 |
| Duel | `DuelOpponents()`, `StartDuel(levelId, companion, opponent, difficulty, seed)`, `FinishDuel(duel)` | see §6 |

After `FinishLevel`, `FinishChallenge` or `FinishDuel`, save the progress the same way the window saves it now: `MoonfallProgress.Record(path, progress.Copy())` on a background thread, then `progress.Absorb(merged)` on the draw thread.

## 2. Adventure (`MoonfallStages`)

**Structure** (`MoonfallStages.Of(campaign)`):

| Campaign | Stages | Levels | Ids |
|---|---|---|---|
| The Moon Road (base) | 10 companion stages, then "Your Pick" | 55 [R §6 l.126] | `base-01` … `base-55` |
| The Far Shore (expansion) | the same 10, then the moogle courier (Storm Post), then a last pick stage | 60 [R §6 l.130] | `expansion-01` … `expansion-60` |

- **The Moon Road's stage names** come from `spec-rich2.md` §3 (decision 25). The parenthesised homes ("(Ul'dah)", "(Limsa Lominsa)") are notes on the theme, not part of the names.
  1. The Waking Sands
  2. Vesper Bay
  3. The Night Skyway
  4. The Sunlit Steps
  5. Harbour Lights
  6. The Silent Stars
  7. The Shroud by Night
  8. The Market Lanterns
  9. Mor Dhona's Glass
  10. Silvertear by Night
  11. Your Pick
- **The Far Shore's stage names** were proposed here, because the spec names only the base stages, and the owner approved them (stage 8 as "The Floating Market"). They follow the level method's road for the expansion ("harbours, islands, the sky over open water, and at its end the moon itself"), and they reuse its three approved pilots:
  1. The Lantern Quay
  2. The Twin Lights
  3. The Skyward Deck
  4. The Sunlit Isles
  5. The Admiral's Sea
  6. The Ferry in the Stars
  7. The Floating Grove
  8. The Floating Market
  9. The Domes of Sharlayan
  10. The Archon's Crossing
  11. The Courier's Wake
  12. The Sea of Sorrows (the moon, decision 20)
- **The Far Shore follows the spoiler shield** (owner's decision). Each stage, and each shipped scene, is set in an area on the shield's era scale (`MoonfallPlaces`): the Lantern Quay in Limsa Lominsa Lower Decks, the Twin Lights in Western Thanalan, the Skyward Deck over the Sea of Clouds, the Sunlit Isles on the Ruby Sea, the Admiral's Sea in Western La Noscea, the Ferry in the Stars nowhere (our own painting), the Floating Grove in Il Mheg, the Floating Market in Kugane, the Domes of Sharlayan in Old Sharlayan, the Archon's Crossing in Labyrinthos, the Courier's Wake nowhere (our own painting, the owner's answer of 6 October 2026: the moogle, met in the first hours of any start, and its Storm Post open at every era), the Sea of Sorrows in Mare Lamentorum. A stage whose area the shield hides is `Veiled`: its name prints as the shield's placeholder (with its hover and "Reveal this name"), its levels have no names and no scene, and Adventure, Quick Play, duels and challenges cannot play them, until the story reaches the area or the player reveals it. The map marks such a stop with the shield's eye-slash, not the padlock. The companions keep their own gating.
- **Adventure steps over a veiled stage** (owner's ruling). The road's frontier skips a stage set past the story: the next stage the story allows opens, so no story-safe stage (the moogle's Storm Post included) waits behind a veiled one. The veiled stage waits on the map with the shield's mark (and the padlock too while the road has not come to it), its levels unwon; "all won" and The Far Shore's completion still need it. When nothing else is left to play, the title's Continue says the road waits past the story and opens the map on that stage; the tally says so when it has no Next because of it. When the road has come to the stage and its levels are built, pressing its "Reveal its place" pill opens the shield's "Reveal this name" (for this session), as a right-click on its name does. Otherwise the pill is the padlock's "Not reached" (the road has not come to it) or "Levels on their way" (none of its levels is built yet), and a reveal is left to the name's right-click: a reveal is offered only where it opens something.
- **Scenes and backdrops follow the same rule.** A level on a veiled stage has no scene art (the night sky). A level whose stage is open but whose scene's own place is past the story is drawn over the recipe's declared story-safe fallback picture (every veilable recipe declares one; a test holds it). The menus' backdrops are tagged too: the title's painting is Sohm Al (Heavensward). While the shield hides it, the title is Ul'dah's loading-screen painting (A Realm Reborn, tagged "Ul'dah - Steps of Nald", so the shield test confirms its era; the owner's answer of 6 October 2026). The chart (Eorzea's world map, no story place) is only the last fallback, when neither painting can be shown or read.
- **Levels are referenced by id.** Content is authored separately. `MoonfallCampaigns.Find(id)` returns the shipped level, or null when no level of that id is shipped yet, in which case the slot is `Missing` and Adventure stops before it.

**Unlocks** [R §6 l.126]:

```
level i of a campaign:  Veiled   if its stage is set past the player's story (the spoiler shield; MoonfallPlaces)
                        Missing  else if no level of its id ships
                        Cleared  else if won
                        Open     else if the campaign is open and (i <= the frontier, or level i - 1 is won, or i is the
                                 campaign's reach: the furthest the frontier has ever stood)
                        Sealed   otherwise
the frontier = the first level neither won nor on a veiled stage (MoonfallModes.Frontier): the road steps over a veiled stage
The Moon Road is always open; The Far Shore opens when all 55 of The Moon Road are won.
A stage is Veiled when set past the story, Done when all five are cleared, Open when its first level is reached, Sealed otherwise.
A companion is reached once its stage is open by that rule (the frontier at or past it, or a level of it won or opened) and the
stage is not itself veiled (MoonfallModes.CompanionReached).
```

- **The frontier** steps over veiled stages, so a stage the story allows is never stuck behind one set past it. The count
  (`BaseCleared`, `ExpansionCleared`) is still the run of consecutive wins, so a veiled stage's unwon levels keep "all won"
  and the Far Shore's completion waiting.
- **A level the road came to stays open, for good.** A level whose predecessor is won stays open whatever the frontier says
  now. So does the level the frontier has reached furthest (the campaign's reach, `MoonfallProgress.BaseReach` and
  `ExpansionReach`, a high-water mark noted after every level's end and saved and merged like the counts), even when the
  road stepped to it over a veiled stage and it is not yet played. So a reveal, or the story reaching a stepped-over stage
  (which pulls the frontier back to it), never closes a level the player had come to, and never locks the moogle again.
  Veiled still ranks first, so an alt with less story sees nothing new.
- **Where Adventure goes next** (`MoonfallModes.Next`, the title's Continue): the frontier's level when it can be played;
  otherwise the first veiled stage before the frontier that has levels built (the road waits there, the map opens on it); a
  stage whose levels are not built is never "past your story", it is "levels on their way" (`MoonfallLooks.Coming`, measured
  from the frontier).

## 3. Companions and the spoiler shield (`MoonfallCompanions`, `MoonfallStory`)

`MoonfallCompanion` ids equal their power's ids (`Minfilia = 1 = SuperGuide` … `Moogle = 11 = Bolt`). `MoonfallCompanions.All` gives each companion's key, name, power, Triple Triad card icon, campaign and stage, and **story names**: the English `ENpcResident` names that must all be met.

```
State(companion) =
  NotMet          if the story has not introduced every one of its story names   -> the card back, "Not yet met", power named
  MetNotReached   else if Moonfall has not reached its stage                      -> the card dimmed, "stage N"
  Available       otherwise                                                       -> face up; offered in Quick Play
```

- **The story** is `MoonfallStory.FromShield(SpoilerMask)`. A name is met once `SpoilerMask.IsNameMasked(SpoilerKind.Npc, name)` is false. That is the shield's own NPC rule with the player's own setting: with the shield off, everyone is met, and a name the data doesn't place counts as met, because the shield never guesses.
- **The twins** need both "Alphinaud" and "Alisaie", so their card stays face down until Alisaie is met (decision 23).
- **The moogle** has no story names (met in the first hours of any start), and it is reached at The Far Shore's stage 11, which is set nowhere, so it is reached at every era once the road comes to stage 11.
- **Quick Play** offers only `Available` companions. The research gives Quick Play no unlocks of its own ("Quick Play replays unlocked levels" [R §6 l.126]).

## 4. Quick Play and Ace scores

- **Quick Play** plays any level reached (`Open` or `Cleared`) with any available companion, or none.
- **Ace scores** (`MoonfallAces`) [R §3 l.93]:
  - Each level has a fixed Ace score, in `Moonfall/Modes/aces.json` (see §8).
  - A won level that reaches it earns `MoonfallRules.AceBonus` (25,000 `[J]`), and the level is marked aced; it stays aced.
  - `FinishLevel` records `max(best, score + bonus)`.
  - A level with no entry has no Ace.
  - `[J]` The numbers come from the greedy player: the 75th percentile of its won games' scores, rounded up to 10,000 (`MoonfallAces.Suggest`).
  - The playability test prints the suggestion for every shipped level, so whoever authors a level can copy it in.

| Level | Ace |
|---|---|
| base-01 | 340,000 |
| base-02 | 330,000 |
| base-03 | 320,000 |
| base-04 | 350,000 |

## 5. Challenges (`MoonfallChallenges`, `MoonfallChallengeRun`)

**Kinds** [R §6 l.129–130]:

| Kind | Met when |
|---|---|
| `score` | the run's total reaches `target` when a level ends |
| `win` | every level is won |
| `clearAll` | every level ends in a perfect clear: the last orange hit with every other peg lit or gone ("In the Clear") |
| `duel` | every duel is won; a draw is not a win |

A lost level ends any run. A challenge may set `balls` (1 to 20), `oranges` (25 to 45; the engine's meter stays at ×1 until 15 are left, so it is "frozen until 25 remain"), and `companion` (fixed; when absent, the player picks an available one). A duel challenge adds `opponent` and `difficulty`. A run of 2 to 6 levels is the "multilevel run".

**States** for the menu (`MoonfallChallenges.State`):
- `Sealed`: challenges open after The Moon Road is won: "Challenge mode unlocks after Adventure" [R §6 l.126].
- `Unavailable`: a level the challenge names isn't shipped, or has too few pegs that may be orange.
- `Open`.
- `Done`.

**A run:**

```
run = modes.StartChallenge(id, companion, seed)        // null unless Open or Done
loop:
  game = run.StartLevel()       (or duel = run.StartDuel())
  ... play it to Won/Lost ...
  status = run.Finish(game)     (or run.Finish(duel))   // Playing -> next level; Met; Failed
modes.FinishChallenge(run)                              // records done and best total
```

Each level is played at its Adventure number, and at least 3, so greens and the companion's power are on the board. It is dealt from `run.SeedOf(index)`.

**The starter twelve** are in `Moonfall/Modes/challenges.json`. Five of them run on the four shipped levels; the rest wait for their levels:

| Id | Name | Kind | Levels | Rules |
|---|---|---|---|---|
| ch-01 | Seven Lanterns | score 150,000 | base-03 | 7 balls |
| ch-02 | Half the Light | win | base-02 | 5 balls |
| ch-03 | Thirty-Five Moons | win | base-04 | 35 oranges |
| ch-04 | Forty-Five Moons | win | base-03 | 45 oranges |
| ch-05 | Clear Skies | clearAll | base-01 | |
| ch-06 | Not a Star Left | clearAll | base-13 | |
| ch-07 | The Long Road | score 600,000 | base-06, 07, 08 | |
| ch-08 | Moonrise Run | win | base-10, 15, 20, 25 | |
| ch-09 | Burning Bright | score 250,000 | base-43 | 7 balls, Y'shtola |
| ch-10 | A Duel at the Waking Sands | duel | base-01, 02, 03 | Minfilia, novice |
| ch-11 | The Admiral's Wager | duel | base-21, 22, 23 | Merlwyb, adept |
| ch-12 | The Archon's Path | duel | base-46, 48, 50 | Louisoix, master |

## 6. The duel (`MoonfallDuel`, `MoonfallAi`)

**Rules** [R §6 l.127–128, §2 l.47, §3 l.74–92]. The engine plays them under `MoonfallRuleSet.Duel`:
- **Balls and turns.** Two sides, 5 balls each (`[J]`, from "5 or 6"). The player shoots first, and the sides alternate. A side out of balls passes, and the other shoots on.
- **Powers.** Each side's greens trigger its own companion's power, and each side's powers (shots left, the drum's triple) are kept apart between turns (`MoonfallGame.HandOver`).
- **Greens.** One green at a time; the next appears the turn after the standing one is gone (2 in all).
- **No orange.** A turn that lights no orange keeps 75% of its own score. `[J]`: it is the shot's score, not the running total.
- **Free balls** belong to the shooter.
- **The end.** The last orange brings the Full Moon on the duel's smaller buckets (`[J]` 2,500 / 12,500 / 25,000 / 12,500 / 2,500; 25,000 on a perfect clear), paid to the side that hit it. The duel also ends when both sides are out of balls.
- **Values.** The duel's style values are the research's numbers in parentheses. `[J]` No bonus is paid for balls left.
- **The result.** The higher score wins; equal scores are a draw.

**The turn machine**, driven by `duel.Advance(dt)` (or `Tick()`) and `duel.Shoot(angle)`. Never drive `duel.Game` directly: draw it and read its events as usual.

```
PlayerAiming --Shoot--> Flying -> Clearing --turn scored--> close turn: kept score, free balls, then
   HandOver(other side if it has balls, else the same side)
OpponentAiming: each tick MoonfallAi.Step weighs a few angles; on its LaunchTick it shoots
Won/Lost (board) --> Outcome = Won / Lost / Drawn (player's view)
```

`duel.PlayersTurn`, `Turn`, `Score(side)`, `ShownScore(side)` (counts up), `BallsLeft(side)`, `Companion(side)`, `LastTurn` and `Outcome` are what the HUD needs. `Game.Side` says whose powers the HUD shows.

**The opponent** (`MoonfallAi`):
- **How it chooses.** It flies probe balls with the engine's own physics (`MoonfallGame.WeighShot`, the same flight Sage's Path uses) at every *step* degrees from −84° to 84°. It weighs each probe as a shot scores, plus its own weights for oranges and a catch, then adds its aim error.
- **When it shoots.** It decides on its first thinking tick which tick it will shoot on, so every probe sees the bucket where it will be.
- **Determinism.** It draws from `MoonfallRandom(seed)`, so the same seed and the same game give the same shot.

| Difficulty | Step | Aim error | Budget per angle | Angles per tick | Choice | Ticks at the launcher |
|---|---|---|---|---|---|---|
| Novice | 6° (29 angles) | ±5° | 2,000 sub-steps | 6 | one of its best 3 | ≥ 80 |
| Adept | 3° (57) | ±2° | 3,000 | 4 | the best | ≥ 70 |
| Master | 1.5° (113) | ±0.75° | 4,000 | 3 | the best | ≥ 60 |

**Its cost.** A tick never costs more than 12,000 physics sub-steps, about a millisecond on the largest shipped level (Sage's Path's whole 24,000-step search measures about 2 ms there). A Master's whole turn is at most 452,000 sub-steps, spread over its 60 ticks. Nothing is allocated per tick; a test checks a duel at 0 bytes. The opponent never works the flippers `[J]`.

**Opponents offered** `[J]`: every companion the story has introduced, whether Moonfall has reached them or not, because the title mock offers "Duel against Louisoix" while the player is at stage 3. A companion not yet met is never offered. The player's own duel companion follows Quick Play's rule (`Available`, or none), and so do the levels (reached).

## 7. Progress, version 2 (`MoonfallProgress`)

`<config>/user/moonfall.json`:

```json
{
  "version": 2,
  "baseCleared": 7,
  "expansionCleared": 0,
  "levels":     { "base-01": { "cleared": true, "best": 312340, "aced": false } },
  "challenges": { "ch-01": { "done": true, "best": 168000 } },
  "duels":      { "louisoix/adept": { "wins": 3, "losses": 1, "draws": 0 } }
}
```

- **Version 1** (counts only) reads as it is. Its counts still say which levels are won (`IsCleared`), and nothing is made up for the scores it never held. The next save writes version 2. Empty lists are left out of the file.
- **Merge** (`Record`, `Absorb`), across clients:
  - counts and best scores take the higher;
  - cleared, aced and done hold if either says so;
  - each duel count takes the higher. Two clients that both finish a duel at the same moment count it once, so the record never goes back but may undercount by one.
- **Damaged entries** read as nothing: null, an empty key, or a negative score or count. A corrupt file is quarantined and this client's progress is written in its place, as before.
- **An older build caution.** An older build saving over a version 2 file keeps the counts but drops the lists. Only a downgrade does this.
- **Write calls**, all on the draw thread, followed by a save:
  - `RecordLevel(id, won, score, ace)`
  - `RecordChallenge(id, done, score)`
  - `RecordDuel(opponent, difficulty, outcome)`

## 8. Data formats

**Level format, version 2** (decision 16; `MoonfallLevelLoader`):
- **`canBeGreen`** (default true) on any peg or brick. Off keeps a power's peg off a figure's eye or a constellation's star.
- **The deal.** Greens come from the blue pegs left after the oranges, in file order, filtered by the flag. A level whose pieces all may be green deals exactly as before, seed for seed. The purple keeps its rule (any blue still standing), so it never lands on a green. `[J]` The flag is about greens only.
- **Validation.** The loader refuses a level whose oranges could take every green: `MoonfallLevel.GreenCandidatesAtWorst` must be at least 2.
- **`scene`** is official in version 2 (a picture's name, never a path).
- **Version 1 files** still read, and both properties are ignored there like any unknown property.

**Challenges** (`Moonfall/Modes/challenges.json`, `MoonfallChallengeLoader`):

```json
{ "format": "moonfall-challenges", "version": 1, "challenges": [
  { "id": "ch-01", "name": "Seven Lanterns", "text": "Score 150,000 with 7 balls.", "kind": "score",
    "levels": ["base-03"], "target": 150000, "balls": 7, "oranges": 25,
    "companion": "yshtola", "opponent": "louisoix", "difficulty": "adept" } ] }
```

- **`kind`** is `score`, `win`, `clearAll` or `duel`.
- **Required:** `id`, `name`, `kind`, `levels` (1 to 6 ids), plus `target` for a score challenge and `opponent` for a duel.
- **Companion keys:** `minfilia`, `twins`, `cid`, `raubahn`, `merlwyb`, `urianger`, `kan-e-senna`, `tataru`, `yshtola`, `louisoix`, `moogle`.
- **Any error** loads no challenge.

**Aces** (`Moonfall/Modes/aces.json`, `MoonfallAces`): `{ "format": "moonfall-aces", "version": 1, "aces": { "base-01": 340000 } }`.

## 9. The playability gate (decision 21)

- **The port.** `MoonfallPlayability` is the designer's greedy player (`docs/design/v9/rich/tools/mfcheck`, `play`), ported move for move:
  - **Choosing.** It tries every 2° from −84° to 84° on a copy of the game and scores each by pegs + 6 × oranges + 5 for a catch − 0.5 per stuck fire. It misses by up to ±1.5° (the tool's `Random(k × 31 + 7)`).
  - **Seeds.** Game *k* is dealt with seed `(k + 1) × 7919`, at level number 5, and gives up after 41 shots.
- **Cost.** The tool replays the whole game for each trial. The port copies it instead (`MoonfallGame.CopyFrom`, bit for bit, with no allocation), which gives the same games far faster.
- **Checked against the tool.** It reproduces the tool's verdicts exactly on all four shipped levels: 31, 23, 40 and 23 wins of 48.
- **The rule.** A level passes with at least 5 wins of 48.
- **The test.** `MoonfallPlayabilityTests.Every_shipped_level_passes_the_greedy_player` runs once per shipped level, under `Category=Playability`. That category runs in the gates, which skip only `Perf`.

**Measured** (this machine, 48 games in parallel per level):
- About 0.4 s per level once warm, and 2.2 s for the first.
- The four shipped levels take 3.7 s.
- At 115 levels that is about 50 s, over the 20 s budget.

**Proposed split** (not done):
- Keep the per-level theory, and add a `Playability` filter to the gates' test command for levels changed since `main`: a small script lists `Moonfall/Levels/*.json` from `git diff --name-only main`.
- Or run the full set only in a second gates step, `--filter Category=Playability`, which can run in parallel with the rest.
- The theory already makes each level its own test case, so either split is a filter change.

## 10. For the UI

- **Engine additions** (all additive):
  - `MoonfallGame(…, ruleSet, oranges)`
  - `HandOver`
  - `WeighShot`
  - `CopyFrom`
  - `AddTime` / `TickDue` / `TakeTick`
  - `FeverBucketValue`
  - `DuelStyleShotBonus`
  - `Duel`, `RuleSet`, `Side`
  - `MoonfallPeg.CanBeGreen`
- **Unchanged.** The window's current calls (`Playable`, `ExpansionOpen`, `BaseCleared`) still work. `MoonfallModes` is the replacement to move to.
- **Names.** The companions' names and the stage names are English data in Core. Show a companion's name, art and role only in the `MetNotReached` and `Available` states.

# Unlisted quest report

Generated 2026-09-28 from game version `2026.09.15.0000.0000` (the same sqpack `docs/data/catalog-stats.md` was built from) with a throw-away console probe in the session scratchpad that calls `CatalogMapper.Map` and reads the raw `Quest` sheet through Lumina 7.7.1 / Lumina.Excel 7.5.0. No repo code was changed. Counts below are the probe's actual output, not estimates.

- Quest sheet: 5533 rows, 5373 named, 160 skipped (empty name).
- Named quests with `JournalGenre == 0` (Unlisted): **180**.
- The owner's screenshot shows 179 because `TreeCounts` drops Foreclosed quests from `Total`; `Seeing the Cieldalaes` (70180) is quest-locked by `Seeking Sanctuary` (70179), so it reads as foreclosed for anyone who owns an island.
- `JournalGenre` row 0 has an empty name and `JournalCategory` 0; category 0 is the placeholder `Sephiroth Missions` under section 255. The mapper's `JournalRef` for unlisted quests therefore carries section 255 / category 0, which `TreeCounts` and the UI already ignore.

## 1. What the sheet says about the 180

Four sheet-level signals separate the bucket cleanly. Each was checked against the full 5373-quest population, not only the 180.

| Signal | Unlisted | Listed | Meaning |
|---|---:|---:|---|
| `IssuerStart == 1034221` (an `ENpcResident` row whose name is empty) | 99 | 5 | Placeholder NPC the game moves a quest to when it retires it. All 5 listed hits are quests the wiki marks as removed (see section 5). |
| Lumina `Unknown12` (unnamed in EXDSchema; the bool between `HideOfferIcon` and `HideInScenarioGuide`) | 98 | 2 | Set on exactly the retired rows from the 5.3 ARR pruning, 5.5 SMN rework and 6.3 Crystal Tower removal. The 98 are a strict subset of the 99 issuer-1034221 rows (`The Favors of the House`, removed in 5.2, has the issuer but not the flag). The 2 listed hits are `But I Hardly Noah` / `The Gift of the Archmagus` (removed 6.3). |
| `EventIconType == 10` | 59 | 2 | Same map icons as type 8 (71140/71150, the blue `+`). Every one the wiki documents is a **quasi-quest**: talking to the NPC accepts and completes it in one dialogue. The 2 listed ones are `Earning Your Wings` (68543) and `The Crystal (Line's) Call` (70121), both filed under La Noscean Sidequests. |
| `InternalId` prefix `Xxa`/`Xxb`/`Xxc` on a *listed* quest with the same name | 44 have one | – | The replacement rows the game added when it rewrote a quest (`Xxa` = 5.3 ARR pruning, `Xxb` = 5.5 SMN, `Xxc` = 6.x `Ultimate Weapon` / `Steps of Faith` / `Operation Archon`). The old row keeps its script id (`GaiUsa301_00733`) and completion flag but loses its genre. |

Other columns requested in the brief, across all 180: `Festival` = 0, `BellStart`/`BellEnd` = 0, `IsHouseRequired` = false, `BeastTribe` = 0, `Type` = 0 (the sheet's `Type` byte is 0 for every unlisted row and non-zero only on 1151 listed rows), `IconSpecial` = 0, `Icon` = 0 for 155 of them. `GrandCompany` is non-zero only on the three `Squadron and Commander` rows (1/2/3). `IsRepeatable` only on `Recondition the Anima` and `Forged Anew`. `Introduction` is true on all 180 but also on 4102 listed quests, so it carries no information; `HideOfferIcon` is false everywhere. None of the seasonal, housing, beast-tribe, PvP, treasure-map or deep-dungeon hypotheses from the brief produced a hit: no unlisted row has a festival, a house requirement or a tribe.

Per-expansion split of the 180: ARR 127, HW 14, SB 7, ShB 17, EW 5, DT 10.

## 2. Groups

Assignment order matters: a row is tested for A first, then B1, then everything else.

### A. Retired rows (99): `IssuerStart == 1034221 || Unknown12`

- **A1 – replaced by a same-name listed row (44).** The old pre-5.3 MSQ chain (`GaiUsa`/`GaiUsb`/`GaiUsc`/`GaiUse`, `ManFst`), plus `Can't Do It without U` (old `GaiUsc704`, replaced by `GaiUsx704` in 4.0), `Legacy of Allag` (old `GaiUsd201`, replaced by `GaiUsx201` in 4.4) and `To Be Second Best` (old SMN 80 quest `LucKbc008`, replaced by `XxbKbc008` in 5.5). Their replacements are all listed under the expected genre (G1/G2 MSQ, G114, G18, G184).
- **A2 – removed with no replacement (55).** Pruned ARR MSQ and sidequests (`Doman Connection`, `Ruffled Feathers`, `Feeding Time`, `Courier for a Day`, the 2.x `Hest`/`Morbol Country` chain, ...), `I Believe I Can Fly` (HW flying unlock, removed 5.3) and `The Favors of the House` (removed 5.2).
- Evidence: old rows still point at their old prerequisites (`Chasing Ivy` 65615 -> `Tendrils of Intrigue` 65614, G2), and chains of them link only to each other; walking back through unlisted prerequisites, the nearest listed prerequisite is an MSQ quest (G1/G2) for 95 of the 99. `EventIconType` is 3 (plain sidequest icon) on 94 of them even for MSQ rows, i.e. the icon was reset when the row was retired.
- Wiki cross-check (section 3): every A row checked is either marked *removed in patch 5.3/5.2/6.1* or has a page that describes the *replacement* row (the wiki has one page per name).
- The owner's 118/179 completion is consistent with a character that played ARR before 5.3: the game keeps the completion bit on the old row id, so all old MSQ rows they cleared read as Completed while a post-5.3 character can never complete them.

### B1. Class/job intro quasi-quests (23): `InternalId` matches `^(Cls\w{3}(001|999)|Job\w{3}299)_`

`So You Want to Be a Gladiator` ... `Fisher` (20 ARR classes), `So You Want to Be a Machinist`, `A Dark Spectacle` (DRK), `What's Your Sign` (AST). `EventIconType` 10, `Header` 6, reward `ClassJob:<class>` for the DoW/DoM ones. No prerequisites; each one is the sole prerequisite of the class's first listed quest (`Way of the Gladiator` 65821, G156, ...). The wiki calls them "Feature quasi-quest" with "There are no journal entries for this quest."

### B2. Other quasi-quests (35): `EventIconType == 10`, not A/B1

Expansion hub unlocks that the journal never shows: `Leves of <city>` (5), `Sights of <region>` sightseeing-log unlocks (6), wandering-minstrel cutscene/EX unlocks (`Songs in the Key of Kugane`, `Minstrel from Another Mother`, `I Wandered Sharlayan as a Minstrel`, `How the West Was Sung`), collectables/scrip vendors (`Reach Long and Prosper`, `The Boutique Always Wins`, `Expanding House of Splendors`, `Dawn of a New Deal`), Gold Saucer (`Triple Triad Trial`, `Scratch It Rich`, `Hitting the Cactpot`), `Squadron and Commander` x3, Eureka `And We Shall Call It Pagos/Pyros/Hydatos`, `What Lies Beneath` (PotD 51+), `Plucking the Heartstrings` (performance), `Weapon of Choice` (Werlyt EX), `Memories Rekindled` (New Game+), `An Odd Job` (Variant dungeons), `The Aspiring Skywatcher`, `The New Frontier` (Diadem).

### C. Hidden steps with a normal icon (23): everything else

- **C1 `EventIconType` 8 (16):** YoRHa 5.5 Komra chain `A Message from Konogg` -> ... -> `All's Well That Ends with Ale` (6, between `Komra Wasn't Built in a Day` 69579 and `The Merchant of Komra` 69587, all G27), Resistance Weapons steps `Memoirs from the Front` / `A Seaside Story` / `An Honor to Serve` (G91 on both sides), Beastmaster `Free for All` / `Mastery Rematch` / `Blazing Trails` (G198, unlock Master's Board instances 56004/56005), `Seeing the Cieldalaes` (island visitation, quest-locked by G107 `Seeking Sanctuary`), `Recall of Duty` (Duty Recorder), `Open and Inviting` (Triple Triad open tournament), `Abridged Too Far` (7.45, unlocks instance 38001 `The Merchant's Tale (Advanced)`).
- **C2 `EventIconType` 1 (4):** Firmament phase-view quests `The Mendicant's Court`, `The New Nest`, `Featherfall`, `The Risensong Quarter` (`LucKha002/011/021/031`, all prerequisite `Towards the Firmament` 69208, G100). Their wiki pages are landmark pages, not quest pages, so they were only cross-checked on Garland (genre 0, patch 5.1, Francel / Augebert).
- **C2 `EventIconType` 4 (3):** `Recondition the Anima` (repeatable, G89 prerequisite), `Forged Anew` (repeatable, G91 prerequisite), `The Pilgrim's Answer` (7.35, G106 prerequisite; wiki: "hidden quasi-quest that does not show up on the minimap"). Type 4 has no map icon at all in `EventIconType`.

Hypotheses from the brief that turned out empty: no Ishgardian Restoration *sidequest* rows, no Bozja/Eureka *step* rows beyond the three Eureka entry quasi-quests, no Hall of the Novice, no custom-delivery unlocks, no squadron missions, no PvP, no treasure map, no legacy 1.0 rows (the `Legacy Quests` category C131 / genres 344-348 exists in the sheet but holds 0 quests), no `(Hidden)` names.

## 3. External cross-check (27 quests)

Sources: consolegameswiki quest pages, Garland Tools `db/doc/quest/en/2/<rowid>.json`, xivapi v2 `sheet/Quest/<rowid>`. Garland and xivapi both report `genre`/`JournalGenre` = 0 for every row queried; neither carries a removed/obsolete marker, so they only confirm the sheet. The wiki is the only source that names a tab or says "removed". Garland and xivapi were reachable; the wiki returned 404 for six titles (`Dwarves of a Beard`, `A Message from Konogg`, `The New Nest`, `Featherfall`, `Memoirs from the Front`, `Recondition the Anima`, `Open and Inviting`), which were then checked on Garland instead.

| Row | Quest | Group | Wiki says | Garland / xivapi |
|---:|---|---|---|---|
| 66269 | Never Forget | A1 | Main Scenario Quest (page describes replacement 69396) | genre 0, issuer Teteroon, patch 2.0; xivapi JournalGenre 0, EventIconType 3, IssuerStart 1034221 |
| 65615 | Chasing Ivy | A1 | Main Scenario Quest, Seventh Astral Era | genre 0, patch 2.4 |
| 65616 | Doman Connection | A2 | "retired or removed ... It was removed in patch 5.3" | genre 0 |
| 65661 | A Soldier's Breakfast | A1 | Main Scenario Quest (page describes replacement 69391) | – |
| 66211 | It's Probably Pirates | A1 | Main Scenario Quest (page is the Limsa row 65781; the Gridania-start copy 66211 was pruned) | – |
| 65955 | The Steps of Faith | A1 | "Patch 6.2 removed The Steps of Faith and converted the encounter ... into a solo instanced duty" (replacement 70127 `XxcUse607`) | – |
| 66735 | Legacy of Allag | A1 | Feature Quest (page describes 67245, patch 4.4) | garland 66735: genre 0, issuer Rammbroes, patch 2.1; garland 67245: genre 18, issuer Outlandish Man, patch 4.4 |
| 66552 | Can't Do It without U | A1 | Ul'dahn Sidequests; "NPCs associated with this quest were changed" in 4.0 (replacement 67089) | – |
| 68753 | To Be Second Best | A1 | Summoner job quest lvl 80; "adjusted in Patch 5.5. If you accepted the quest prior to this, you will have to abandon and re-start it" (replacement 69569 `XxbKbc008`) | – |
| 67653 | I Believe I Can Fly | A2 | Feature Quest lvl 52; "It was removed in patch 5.3" | – |
| 67635 | The Favors of the House | A2 | Feature quasi-quest lvl 60; "It was removed in patch 5.2" | – |
| 65713 | So You Want to Be a Gladiator | B1 | "Feature quasi-quest ... There are no journal entries for this quest" | xivapi JournalGenre 0, EventIconType 10, IssuerStart Lulutsu |
| 68457 | Leves of Kugane | B2 | Feature quasi-quest lvl 60, Keltraeng | – |
| 67643 | Sights of the North | B2 | Feature quasi-quest lvl 52, unlocks Sightseeing Log (HW) | – |
| 68477 | Reach Long and Prosper | B2 | Feature quasi-quest lvl 60; "This is not a proper quest, even though it acts like one" | – |
| 67926 | Squadron and Commander (Maelstrom) | B2 | Feature quasi-quest lvl 47, unlocks Adventurer Squadrons | – |
| 68478 | And We Shall Call It Pagos | B2 | quasi-quest lvl 70, Rodney, unlocks Eureka Pagos, patch 4.36 | – |
| 67923 | What Lies Beneath | B2 | Feature quasi-quest lvl 17, unlocks PotD floors 51-100, patch 3.45 | – |
| 65973 | Triple Triad Trial | B2 | Feature quasi-quest lvl 15, unlocks Triple Triad | – |
| 68555 | Plucking the Heartstrings | B2 | Feature quasi-quest lvl 30, unlocks Performance | – |
| 68479 | Songs in the Key of Kugane | B2 | Feature quasi-quest lvl 70, unlocks SB EX/Ultimate | – |
| 69378 | Weapon of Choice | B2 | Feature quasi-quest lvl 80, unlocks Werlyt EX trials, patch 5.2 | – |
| 69295 | Memories Rekindled | B2 | Feature Quest lvl 1, unlocks New Game+, patch 5.1 | – |
| 70187 | An Odd Job | B2 | Feature quasi-quest lvl 90, unlocks Variant Dungeons, patch 6.25 | – |
| 69141 | The Aspiring Skywatcher | B2 | Feature quasi-quest lvl 80 | – |
| 68624 | Recall of Duty | C1 | Feature quasi-quest lvl 70, unlocks Duty Recorder, patch 4.2 | – |
| 71036 | Free for All | C1 | Feature quasi-quest lvl 50 (Beastmaster), unlocks First Master's Board, patch 7.56 | – |
| 70180 | Seeing the Cieldalaes | C1 | Feature Quest lvl 1, Baldin, unlocks Island Sanctuary visitation | – |
| 70995 | Abridged Too Far | C1 | Feature Quest lvl 100, Memolivia, unlocks The Merchant's Tale (Advanced), patch 7.45 | – |
| 69563 | A Seaside Story | C1 | Feature quasi-quest, Resistance Weapons questline, unlocks Delubrum Reginae (Savage), patch 5.45 | – |
| 69478 | Memoirs from the Front | C1 | (404) | garland: genre 0, Resistance Historian, Bozjan Southern Front, patch 5.35 |
| 69580/69581 | A Message from Konogg / Dwarves of a Beard | C1 | (404) | garland: genre 0, Dig Site Chief, Kholusia, patch 5.5, prev `Komra Wasn't Built in a Day`, next `Dwarves of a Beard` |
| 69296 | The Mendicant's Court | C2 | landmark page only | garland: genre 0, Francel, The Firmament, patch 5.1 |
| 69566 | Open and Inviting | C1 | (404) | garland: genre 0, Open Tournament Official, Gold Saucer, patch 5.4 |
| 67870 | Recondition the Anima | C2 | (404) | garland: genre 0, Ulan, Idyllshire, patch 3.3, repeatable |
| 70944 | The Pilgrim's Answer | C2 | "hidden quasi-quest that does not show up on the minimap", lvl 91, patch 7.35 | – |

The wiki files quasi-quests under the *feature quest* type and, where it names a tab, under the regional sidequest category of the NPC's city (e.g. `Squadron and Commander` under Grand Company, `Triple Triad Trial` under Gold Saucer / Ul'dahn Sidequests). That matches what the game does for the two listed quasi-quests (`Earning Your Wings`, `The Crystal (Line's) Call` -> La Noscean Sidequests).

## 4. Recommendations

Counts are from the rule simulation in the probe (`rules.psv`), applied in the order listed; every one of the 180 rows lands in exactly one bucket.

| # | Rule (in order) | Rows | Recommendation |
|---:|---|---:|---|
| 1 | `IssuerStart == 1034221 \|\| Unknown12` -> **Retired** | 99 | (c) keep in Unlisted, hidden by default. In the evaluator treat as `Foreclosed` unless the completion flag is set (a new `RequirementKind.Retired`, reason "removed from the game"), so `TreeCounts` never counts them in a total. Optionally show them under the sheet's own empty `Legacy Quests` category (C131, genres 344 MSQ / 345 sidequests / 347 job) using the nearest listed prerequisite's section to pick the genre; but with Foreclosed handling that is cosmetic. Also apply the same retired handling to the 5 *listed* rows in section 5. |
| 2 | `InternalId` matches `^(Cls\w{3}(001\|999)\|Job\w{3}299)_` -> genre of the first listed successor | 23 | (a) map into the class/job genre (G156-G175, G186-G188). Also mark as feature quest. The 20 class intros (`Cls…001/999`) are listed there but kept out of the genre's counts (`QuestRecord.CountsInTotals` false); the 3 job intros (`Job…299`, nobody starts as a job) count as usual. Each ARR class genre holds a starter's opening (`ClsLnc000` Way of the Lancer, no prerequisite) and a switcher's opening (the intro, then `ClsLnc998` Way of the Lancer and My First Spear), and a character walks exactly one; the real snapshot in `Tsukimichi.Tests/Fixtures/snapshot-v1.json` (started as Lancer) has every other levelled class's intro flagged and the Lancer intro unflagged with the starter's Way of the Lancer done (`ClassIntroSnapshotTests`). |
| 3 | `GrandCompany != 0` -> genre `233 + GrandCompany` (G234 Maelstrom / G235 Twin Adder / G236 Immortal Flames) | 3 | (a) `Squadron and Commander` x3 under Grand Company Quests (C96). |
| 4 | First listed **prerequisite** (walking through unlisted prerequisites in slot order) whose section is not 0/1 and whose `Expansion` equals the quest's -> its genre | 24 | (a) Anima Weapons G89 (1), Resistance Weapons G91 (4), Ishgardian Restoration Main G100 (4), Pilgrim's Traverse G106 (1), YoRHa G27 (6), Beastmaster G198 (3), Garlemald's Machinations G28 (1), Bard Quests G180 (1), Ul'dahn Sidequests G114 (3 Gold Saucer). |
| 5 | Else first listed **successor or QuestLock** (same section/expansion constraint) -> its genre | 3 | (a) `An Odd Job` -> Variant Dungeons G108, `Reach Long and Prosper` -> Rhalgr's Reach Sidequests G124, `The Boutique Always Wins` -> Eulmore Sidequests G133. |
| 6 | Else the dominant genre among listed quests in journal categories 59-85 (regional sidequests) whose `IssuerLocation` territory equals this quest's -> that genre | 27 | (a) Ishgardian G117 (1), Dravanian Forelands G119 (1), Gridanian G113 (1), Ul'dahn G114 (2), Mor Dhonan G116 (1), La Noscean G112 (1), Kugane G128 (5), Rhalgr's Reach G124 (1), Crystarium G132 (4), Old Sharlayan G140 (4), Radz-at-Han G141 (1), Tuliyollal G148 (3), Solution Nine G154 (2). The table in appendix B shows the vote is unanimous for every territory involved. |
| 7 | Else keep Unlisted | 1 | `The New Frontier` (67752, Diadem unlock; issuer Aurvael has no `IssuerLocation`). Curated override candidate -> G117 Ishgardian Sidequests. |

Totals: (a) map into an existing node **80** (rules 2-6), (c) keep Unlisted **100** (99 retired + 1 no-signal). No new virtual node is needed; the existing `Feature Unlocks` node covers the quasi-quests once `FeaturePresets` treats `EventIconType == 10` like 8 (adds 58 unlisted + the 2 listed EIT-10 quests to that node).

Curated overrides worth adding on top (3 rows): `And We Shall Call It Pagos/Pyros/Hydatos` (68478/68148/68149) belong with `And We Shall Call It Eureka` (68614) under G90 `The Forbidden Land, Eureka` (C56), not Kugane Sidequests; rule 6 cannot see that because Rodney issues no listed quest. `Seeing the Cieldalaes` (70180) lands in La Noscean Sidequests by rule 6 because its only forward link (`Seeking Sanctuary`, G107) is a QuestLock in the *ARR* expansion slot mismatch (70180 has Expansion 0, 70179 Expansion 4); dropping the expansion constraint for QuestLocks would put it in G107.

Implementation notes for `CatalogMapper`:

- Rules 1-3 and 7 need only the row. Rule 1 needs `quest.IssuerStart.RowId == 1034221 || quest.Unknown12`; a `QuestRecord.IsRetired` bool is the natural carrier. The `Xx*` replacement lookup (same `Name`, `InternalId` starting `Xx`) is not needed for the rule, only for a tooltip ("replaced by 69396").
- Rules 4-6 need the whole sheet (successor index and a territory -> genre vote), so they belong in a second pass after all records exist, before `QuestCatalog.Build`. The successor index is already implied by `PreviousQuests`; the territory vote is one `GroupBy` over listed section-3 quests with `Journal.CategoryId in 59..85`.
- `JournalIndex.Resolve` must then be handed the *assigned* genre so `SortKey` ranks the quest inside its new genre; keep `IsUnlisted` true (or add `IsAssigned`) so the detail pane can still say the journal does not list it.
- `FeaturePresets.FeatureEventIconType` becomes a set `{8, 10}`.

## 5. Listed quests that look misfiled

- **Retired but still listed (5):** `But I Hardly Noah` 66033 and `The Gift of the Archmagus` 66034 (G18 Crystal Tower Quests, wiki: removed 6.3, `Unknown12` true), `A Seat at the Feast` 67819 (G116 Mor Dhonan Sidequests, wiki: removed 6.1 with The Feast), `Makin' Bacon (Bread)` 68629 and `Wok on By` 68727 (G24 Return to Ivalice, wiki: removed 6.2). All five carry `IssuerStart == 1034221`. They inflate those genres' totals for any character that did not finish them in time; rule 1 above should apply to them too.
- **Genre without section/category:** none. Every listed genre resolves to a category and section.
- **Seasonal Events (C97, 294 quests):** all 294 have `Festival != 0` and no `Festival != 0` quest lives outside C97 except the 16 below. Nothing misfiled; the 10/294 is real (seasonal quests are one-shots per year).
- **Special Quests (C98, 20 quests):** G251 `Special Quests` holds only `The Ties That Bind` 67114 (Eternal Bond ceremony; wiki confirms quest line "Special Quests"). G252 `Collaboration Quests` holds 19 crossover quests, 16 of which have `Festival != 0` (festival ids 3, 6, 7, 30, 39, 84, 147, 148, 257; e.g. the Lightning Returns quests 66689-66692 carry 3 and `Keyward Bound` 71002 carries 257) and 3 have `Festival == 0` (`The New King on the Block` / `The Newer King on the Block` 67090/67091 = Yo-kai Watch 4.x, `The White Wanderer` 70910). So 16 of the 20 "Special Quests" are event-gated exactly like Seasonal Events; the game files them here, so this is not a mapper bug, but the `Seasonal active only` filter and the seasonal badge already apply to them via `Festival`. If the tree should reflect availability rather than the game's tab, a `Festival != 0` rule would move those 16 into a virtual "Collaboration events" leaf under Seasonal Events.
- `IconSpecial` is set on all 294 seasonal quests and on 19 of the 20 special quests (not on `The Ties That Bind`), so `IconSpecial != 0` is equivalent to "C97 or G252" and cannot flag anything unlisted (all 180 have 0).

## 6. Full enumeration

Columns: Exp = `Expansion` row id, Lvl = `ClassJobLevel[0]`, EIT = `EventIconType`, Rep = `IsRepeatable`/`RepeatIntervalType`, GC = `GrandCompany`, U12 = Lumina `Unknown12`, Prev = `PreviousQuest` row ids (with the genre the sheet gives them; `unlisted` = genre 0), Succ = quests listing this row as a prerequisite, Lock = `QuestLock`, Issuer = `IssuerStart` NPC and `IssuerLocation` territory place name. `Festival`, `BellStart`, `BellEnd`, `IsHouseRequired`, `BeastTribe`, `Type`, `IconSpecial` are omitted because they are 0/false on all 180 rows (see section 1). The sheet gives every unlisted row section 255 / category 0 through `JournalGenre` row 0.

| Row | QuestId | Internal id | Name | Exp | Lvl | EIT | Rep | GC | U12 | Prev | Succ | Lock | Issuer | Group | Proposed target |
|---:|---:|---|---|---:|---:|---:|---|---:|---|---|---|---|---|---|---|
| 65615 | 79 | GaiUse411_00079 | Chasing Ivy | 0 | 50 | 3 | - | 0 | y | 65614 (G2) | 65616 (unlisted) | - | (blank NPC 1034221), Gridania (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 65616 | 80 | GaiUse412_00080 | Doman Connection | 0 | 50 | 3 | - | 0 | y | 65615 (unlisted) | 65617 (unlisted) | - | (blank NPC 1034221), Gridania (quest PlaceName) | A2 | UNLISTED (was near -) |
| 65617 | 81 | GaiUse413_00081 | In Flagrante Delicto | 0 | 50 | 3 | - | 0 | y | 65616 (unlisted) | - | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 65637 | 101 | ClsRog001_00101 | So You Want to Be a Rogue | 0 | 1 | 10 | - | 0 | - | - | 65638 (G161) | - | Lonwoerd, Limsa Lominsa Lower Decks | B1 | 999 / Job...299 -> genre of first listed successor G161 |
| 65661 | 125 | SubFst031_00125 | A Soldier's Breakfast | 0 | 4 | 3 | - | 0 | y | 65734 (unlisted) | - | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 65664 | 128 | SubFst034_00128 | Eggs over Queasy | 0 | 4 | 3 | - | 0 | y | 65732 (unlisted) | - | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 65692 | 156 | SubFst050_00156 | Ruffled Feathers | 0 | 10 | 3 | - | 0 | y | 65916 (G1) | - | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65695 | 159 | SubFst053_00159 | Lights Out | 0 | 10 | 3 | - | 0 | y | 65920 (G1) | - | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65713 | 177 | ClsGla001_00177 | So You Want to Be a Gladiator | 0 | 1 | 10 | - | 0 | - | - | 65821 (G156) | - | Lulutsu, Ul'dah - Steps of Thal | B1 | 999 / Job...299 -> genre of first listed successor G156 |
| 65714 | 178 | ClsPgl001_00178 | So You Want to Be a Pugilist | 0 | 1 | 10 | - | 0 | - | - | 66068 (G157) | - | Gagaruna, Ul'dah - Steps of Nald | B1 | 999 / Job...299 -> genre of first listed successor G157 |
| 65715 | 179 | ClsExc001_00179 | So You Want to Be a Marauder | 0 | 1 | 10 | - | 0 | - | - | 65846 (G158) | - | Blauthota, Limsa Lominsa Upper Decks | B1 | 999 / Job...299 -> genre of first listed successor G158 |
| 65716 | 180 | ClsLnc999_00180 | So You Want to Be a Lancer | 0 | 1 | 10 | - | 0 | - | - | 65668 (G159) | - | Jillian, Old Gridania | B1 | 999 / Job...299 -> genre of first listed successor G159 |
| 65717 | 181 | ClsArc999_00181 | So You Want to Be an Archer | 0 | 1 | 10 | - | 0 | - | - | 65667 (G160) | - | Athelyna, New Gridania | B1 | 999 / Job...299 -> genre of first listed successor G160 |
| 65718 | 182 | ClsCnj999_00182 | So You Want to Be a Conjurer | 0 | 1 | 10 | - | 0 | - | - | 65669 (G162) | - | Madelle, Old Gridania | B1 | 999 / Job...299 -> genre of first listed successor G162 |
| 65719 | 183 | ClsThm001_00183 | So You Want to Be a Thaumaturge | 0 | 1 | 10 | - | 0 | - | - | 65880 (G163) | - | Yayake, Ul'dah - Steps of Nald | B1 | 999 / Job...299 -> genre of first listed successor G163 |
| 65720 | 184 | ClsWdk999_00184 | So You Want to Be a Carpenter | 0 | 1 | 10 | - | 0 | - | - | 65674 (G165) | - | Corgg, New Gridania | B1 | 999 / Job...299 -> genre of first listed successor G165 |
| 65721 | 185 | ClsBsm001_00185 | So You Want to Be a Blacksmith | 0 | 1 | 10 | - | 0 | - | - | 65827 (G166) | - | Randwulf, Limsa Lominsa Upper Decks | B1 | 999 / Job...299 -> genre of first listed successor G166 |
| 65722 | 186 | ClsArm001_00186 | So You Want to Be an Armorer | 0 | 1 | 10 | - | 0 | - | - | 65809 (G167) | - | G'wahnako, Limsa Lominsa Upper Decks | B1 | 999 / Job...299 -> genre of first listed successor G167 |
| 65723 | 187 | ClsGld001_00187 | So You Want to Be a Goldsmith | 0 | 1 | 10 | - | 0 | - | - | 66144 (G168) | - | Jemime, Ul'dah - Steps of Thal | B1 | 999 / Job...299 -> genre of first listed successor G168 |
| 65724 | 188 | ClsTan999_00188 | So You Want to Be a Leatherworker | 0 | 1 | 10 | - | 0 | - | - | 65641 (G169) | - | Randall, Old Gridania | B1 | 999 / Job...299 -> genre of first listed successor G169 |
| 65725 | 189 | ClsWvr001_00189 | So You Want to Be a Weaver | 0 | 1 | 10 | - | 0 | - | - | 66070 (G170) | - | Maronne, Ul'dah - Steps of Thal | B1 | 999 / Job...299 -> genre of first listed successor G170 |
| 65726 | 190 | ClsAlc001_00190 | So You Want to Be an Alchemist | 0 | 1 | 10 | - | 0 | - | - | 66111 (G171) | - | Deitrich, Ul'dah - Steps of Thal | B1 | 999 / Job...299 -> genre of first listed successor G171 |
| 65727 | 191 | ClsCul001_00191 | So You Want to Be a Culinarian | 0 | 1 | 10 | - | 0 | - | - | 65807 (G172) | - | Charlys, Limsa Lominsa Upper Decks | B1 | 999 / Job...299 -> genre of first listed successor G172 |
| 65728 | 192 | ClsMin001_00192 | So You Want to Be a Miner | 0 | 1 | 10 | - | 0 | - | - | 66133 (G173) | - | Linette, Ul'dah - Steps of Thal | B1 | 999 / Job...299 -> genre of first listed successor G173 |
| 65729 | 193 | ClsHrv999_00193 | So You Want to Be a Botanist | 0 | 1 | 10 | - | 0 | - | - | 65539 (G174) | - | Leonceault, Old Gridania | B1 | 999 / Job...299 -> genre of first listed successor G174 |
| 65732 | 196 | SubFst039_00196 | An Eft for Effort | 0 | 4 | 3 | - | 0 | y | 65981 (G1) | 65664 (unlisted) | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65734 | 198 | SubFst042_00198 | Butcher of Greentear | 0 | 4 | 3 | - | 0 | y | 65711 (G1) | 65661 (unlisted) | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65841 | 305 | SubWil062_00305 | Until a Quieter Time | 0 | 4 | 3 | - | 0 | y | 65860 (unlisted) | 65842 (unlisted) | - | (blank NPC 1034221), Central Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 65842 | 306 | SubWil063_00306 | Prudence at This Junction | 0 | 4 | 3 | - | 0 | y | 65841 (unlisted) | - | - | (blank NPC 1034221), Central Thanalan (quest PlaceName) | A1 | UNLISTED (was near -) |
| 65860 | 324 | SubWil070_00324 | Disorderly Conduct | 0 | 4 | 3 | - | 0 | y | 65839 (G1) | 65841 (unlisted) | - | (blank NPC 1034221), Central Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65863 | 327 | SubWil073_00327 | Spriggan Cleaning | 0 | 4 | 3 | - | 0 | y | 65843 (G1) | - | - | (blank NPC 1034221), Central Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65867 | 331 | SubWil083_00331 | Heir Today, Gone Tomorrow | 0 | 10 | 3 | - | 0 | y | 65866 (G1) | - | - | (blank NPC 1034221), Central Thanalan (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65871 | 335 | SubWil087_00335 | Compulsory Catering | 0 | 10 | 3 | - | 0 | y | 65870 (G1) | - | - | (blank NPC 1034221), Western Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65903 | 367 | GaiUse505_00367 | Aether on Demand | 0 | 50 | 3 | - | 0 | y | 65902 (G2) | - | - | (blank NPC 1034221), The Rising Stones (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 65910 | 374 | SubFst047_00374 | Feeding Time | 0 | 10 | 3 | - | 0 | y | 65712 (G1) | - | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65918 | 382 | SubFst066_00382 | Skeletons in My Deepcroft | 0 | 10 | 3 | - | 0 | y | 65917 (G1) | - | - | (blank NPC 1034221), Central Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65934 | 398 | SubSea101_00398 | Courier for a Day | 0 | 10 | 3 | - | 0 | y | 65933 (G1) | - | - | (blank NPC 1034221), Middle La Noscea (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65940 | 404 | SubSea107_00404 | Farmer of Fortune | 0 | 10 | 3 | - | 0 | y | 65938 (G1) | 65941 (G112) | - | (blank NPC 1034221), Western La Noscea (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 65955 | 419 | GaiUse607_00419 | The Steps of Faith | 0 | 50 | 3 | - | 0 | y | 65954 (G2) | - | - | (blank NPC 1034221), Coerthas Central Highlands (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 65973 | 437 | SubGsc102_00437 | Triple Triad Trial | 0 | 15 | 10 | - | 0 | - | 65970 (G114) | - | - | Triple Triad master, The Gold Saucer | B2 | G114 Ul'dahn Sidequests |
| 65987 | 451 | ClsAcn001_00451 | So You Want to Be an Arcanist | 0 | 1 | 10 | - | 0 | - | - | 65988 (G164) | - | Murie, Limsa Lominsa Lower Decks | B1 | 999 / Job...299 -> genre of first listed successor G164 |
| 66000 | 464 | SubSea052_00464 | Further Afield | 0 | 5 | 3 | - | 0 | y | 66079 (G1) | 66006 (G112) | - | (blank NPC 1034221), Middle La Noscea (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66024 | 488 | SubGsc103_00488 | Scratch It Rich | 0 | 15 | 10 | - | 0 | - | 65970 (G114) | - | - | Mini Cactpot broker, The Gold Saucer | B2 | G114 Ul'dahn Sidequests |
| 66025 | 489 | SubGsc104_00489 | Hitting the Cactpot | 0 | 15 | 10 | - | 0 | - | 65970 (G114) | - | - | Jumbo Cactpot broker, The Gold Saucer | B2 | G114 Ul'dahn Sidequests |
| 66060 | 524 | ManFst503_00524 | The Ultimate Weapon | 0 | 50 | 3 | - | 0 | y | 69409 (G1) | 70315 (G93) | - | (blank NPC 1034221), Northern Thanalan (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66211 | 675 | ManFst203_00675 | It's Probably Pirates | 0 | 15 | 3 | - | 0 | y | 66209 (G1), 66210 (G1) | 68618 (G240), 68741 (G240), 69288 (G240), 70190 (G240), 70861 (G240), 67084 (G248), 68547 (G248), 68705 (G248), 69204 (G248), 69531 (G248) | 65781 (G1) | (blank NPC 1034221), Limsa Lominsa (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66244 | 708 | GaiUsa002_00708 | We Come in Peace | 0 | 20 | 3 | - | 0 | y | 66049 (G1) | - | - | (blank NPC 1034221), Gridania (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66253 | 717 | GaiUsa103_00717 | Dance Dance Diplomacy | 0 | 21 | 3 | - | 0 | y | 66251 (G1) | 66254 (unlisted) | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66254 | 718 | GaiUsa104_00718 | Forest Friend | 0 | 21 | 3 | - | 0 | y | 66253 (unlisted) | - | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66262 | 726 | GaiUsa203_00726 | Druthers House Rules | 0 | 22 | 3 | - | 0 | y | 66261 (G1) | 66269 (unlisted) | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66269 | 733 | GaiUsa301_00733 | Never Forget | 0 | 23 | 3 | - | 0 | y | 66262 (unlisted) | 66270 (unlisted) | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66270 | 734 | GaiUsa302_00734 | Microbrewing | 0 | 23 | 3 | - | 0 | y | 66269 (unlisted) | - | - | (blank NPC 1034221), Upper La Noscea (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66276 | 740 | GaiUsa308_00740 | Nouveau Riche | 0 | 23 | 3 | - | 0 | y | 66274 (G1) | - | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66288 | 752 | GaiUsa410_00752 | Ratting It Out | 0 | 24 | 3 | - | 0 | y | 66284 (G1) | - | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66320 | 784 | GaiUsa711_00784 | Terror at Fallgourd | 0 | 27 | 3 | - | 0 | y | 66319 (G1) | 66321 (unlisted) | - | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66321 | 785 | GaiUsa801_00785 | Ziz Is So Ridiculous | 0 | 28 | 3 | - | 0 | y | 66320 (unlisted) | - | - | (blank NPC 1034221), North Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66351 | 815 | GaiUsb008_00815 | The Penitent Man | 0 | 30 | 3 | - | 0 | y | 66350 (G1) | 66352 (unlisted) | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66352 | 816 | GaiUsb009_00816 | Changing of the Guard | 0 | 30 | 3 | - | 0 | y | 66351 (unlisted) | 66355 (unlisted) | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66355 | 819 | GaiUsb012_00819 | Trial by Turtle | 0 | 30 | 3 | - | 0 | y | 66352 (unlisted) | 66356 (unlisted) | - | (blank NPC 1034221), South Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66356 | 820 | GaiUsb101_00820 | The Drake Exception | 0 | 31 | 3 | - | 0 | y | 66355 (unlisted) | - | - | (blank NPC 1034221), Southern Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66375 | 839 | GaiUsb208_00839 | What Do You Mean You Forgot the Wine | 0 | 32 | 3 | - | 0 | y | 66368 (G1) | - | - | (blank NPC 1034221), Eastern La Noscea (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66383 | 847 | GaiUsb306_00847 | Not My War | 0 | 33 | 3 | - | 0 | y | 66382 (G1) | - | - | (blank NPC 1034221), Eastern La Noscea (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66390 | 854 | GaiUsb313_00854 | A Final Ignominy | 0 | 33 | 3 | - | 0 | y | 66386 (G1) | - | - | (blank NPC 1034221), Eastern La Noscea (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66407 | 871 | GaiUsb502_00871 | With a Little Elbow Grease | 0 | 35 | 3 | - | 0 | y | 66053 (G1) | 66417 (unlisted) | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66408 | 872 | GaiUsb503_00872 | You Can't Take It with You | 0 | 35 | 3 | - | 0 | y | 66053 (G1) | 66417 (unlisted) | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66413 | 877 | GaiUsb508_00877 | The Warden Works in Mysterious Ways | 0 | 35 | 3 | - | 0 | y | 66412 (G1) | - | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66417 | 881 | GaiUsb512_00881 | A Tall Drink of Aqua del Sol | 0 | 35 | 3 | - | 0 | y | 66407 (unlisted), 66408 (unlisted) | - | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66432 | 896 | GaiUsb701_00896 | Feats of Strength | 0 | 36 | 3 | - | 0 | y | 66426 (G1) | - | - | (blank NPC 1034221), Coerthas Central Highlands (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66453 | 917 | GaiUsb808_00917 | The Final Flight of the Enterprise | 0 | 38 | 3 | - | 0 | y | 66448 (G1) | - | - | (blank NPC 1034221), Coerthas Central Highlands (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66461 | 925 | GaiUsb902_00925 | Opportunity Knocks | 0 | 39 | 3 | - | 0 | y | 66460 (G1) | 66473 (unlisted) | - | (blank NPC 1034221), Coerthas Central Highlands (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66462 | 926 | GaiUsb903_00926 | All by Ourselves | 0 | 39 | 3 | - | 0 | y | 66460 (G1) | 66473 (unlisted) | - | (blank NPC 1034221), Coerthas Central Highlands (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66473 | 937 | GaiUsb914_00937 | The Best Inventions | 0 | 39 | 3 | - | 0 | y | 66461 (unlisted), 66462 (unlisted), 66463 (G1) | - | - | (blank NPC 1034221), Coerthas Central Highlands (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66490 | 954 | GaiUsc103_00954 | All Due Precautions | 0 | 41 | 3 | - | 0 | y | 66489 (G1) | - | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66504 | 968 | GaiUsc301_00968 | The Curious Case of Giggity | 0 | 43 | 3 | - | 0 | y | 66503 (G1) | 66507 (unlisted) | - | (blank NPC 1034221), Western La Noscea (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66507 | 971 | GaiUsc304_00971 | Of Sylphs and Spriggans | 0 | 43 | 3 | - | 0 | y | 66504 (unlisted) | 66510 (unlisted) | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66510 | 974 | GaiUsc307_00974 | Crazy Enough to Work | 0 | 43 | 3 | - | 0 | y | 66507 (unlisted) | - | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66539 | 1003 | GaiUsc603_01003 | Dressed for Conquest | 0 | 45 | 3 | - | 0 | y | 66538 (G1) | - | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66552 | 1016 | GaiUsc704_01016 | Can't Do It without U | 0 | 47 | 1 | - | 0 | y | 66058 (G1) | 66557 (G114), 66558 (G114) | 67089 (G114) | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66572 | 1036 | GaiUsc901_01036 | Operation Archon | 0 | 49 | 3 | - | 0 | y | 66058 (G1) | - | - | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66575 | 1039 | GaiUsc904_01039 | The Ladle in the Darkness | 0 | 49 | 3 | - | 0 | y | 66573 (G1) | 66576 (G114), 66578 (unlisted) | - | (blank NPC 1034221), Northern Thanalan (quest PlaceName) | A2 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66578 | 1042 | GaiUsc907_01042 | All upon the Watchtowers | 0 | 49 | 3 | - | 0 | y | 66575 (unlisted) | 66579 (unlisted) | - | (blank NPC 1034221), Northern Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66579 | 1043 | GaiUsc908_01043 | Hearts on Fire | 0 | 49 | 3 | - | 0 | y | 66578 (unlisted) | 66582 (unlisted) | - | (blank NPC 1034221), Northern Thanalan (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66582 | 1046 | GaiUsc911_01046 | Setting the Stage | 0 | 49 | 3 | - | 0 | y | 66579 (unlisted) | 66672 (unlisted) | - | (blank NPC 1034221), Northern Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66670 | 1134 | ClsFsh001_01134 | So You Want to Be a Fisher | 0 | 1 | 10 | - | 0 | - | - | 66643 (G175) | - | N'nmulika, Limsa Lominsa Lower Decks | B1 | 999 / Job...299 -> genre of first listed successor G175 |
| 66672 | 1136 | ManFst502_01136 | Rock the Castrum | 0 | 50 | 3 | - | 0 | y | 66582 (unlisted) | - | - | (blank NPC 1034221), Northern Thanalan (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66712 | 1176 | GaiUse102_01176 | Moving On | 0 | 50 | 3 | - | 0 | y | 66711 (G2) | 66713 (unlisted) | - | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 66713 | 1177 | GaiUse103_01177 | Flowers for One | 0 | 50 | 3 | - | 0 | y | 66712 (unlisted) | 66714 (unlisted) | - | (blank NPC 1034221), Eastern Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66714 | 1178 | GaiUse104_01178 | All Things in Time | 0 | 50 | 3 | - | 0 | y | 66713 (unlisted) | 66715 (unlisted) | - | (blank NPC 1034221), Eastern La Noscea (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66715 | 1179 | GaiUse105_01179 | The Resolute | 0 | 50 | 3 | - | 0 | y | 66714 (unlisted) | 66716 (unlisted) | - | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66716 | 1180 | GaiUse106_01180 | Laying the Foundation | 0 | 50 | 3 | - | 0 | y | 66715 (unlisted) | 66718 (unlisted), 66719 (unlisted), 66717 (unlisted) | - | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66717 | 1181 | GaiUse107_01181 | Better Late than Sever | 0 | 50 | 3 | - | 0 | y | 66716 (unlisted) | 66720 (unlisted), 67097 (unlisted) | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66718 | 1182 | GaiUse108_01182 | Rock-solid Protection | 0 | 50 | 3 | - | 0 | y | 66716 (unlisted) | 66720 (unlisted), 67097 (unlisted) | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66719 | 1183 | GaiUse109_01183 | Crate Go Kaboom | 0 | 50 | 3 | - | 0 | y | 66716 (unlisted) | 66720 (unlisted), 67097 (unlisted) | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66720 | 1184 | GaiUse110_01184 | Hest of the Best | 0 | 50 | 3 | - | 0 | y | 66717 (unlisted), 66718 (unlisted), 66719 (unlisted) | 66721 (unlisted), 67098 (unlisted) | 67097 (unlisted) | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66721 | 1185 | GaiUse111_01185 | Pass the Smell Hest | 0 | 50 | 3 | - | 0 | y | 66720 (unlisted) | 66722 (unlisted) | 67098 (unlisted) | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66722 | 1186 | GaiUse112_01186 | You're Gonna Carry That | 0 | 50 | 3 | - | 0 | y | 66721 (unlisted), 67098 (unlisted) | 66723 (unlisted) | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66723 | 1187 | GaiUse113_01187 | The Things We Do for Tea | 0 | 50 | 3 | - | 0 | y | 66722 (unlisted) | 66724 (unlisted) | - | (blank NPC 1034221), Western Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66724 | 1188 | GaiUse114_01188 | It's Possibly a Primal | 0 | 50 | 3 | - | 0 | y | 66723 (unlisted) | - | - | (blank NPC 1034221), Western Thanalan (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66729 | 1193 | GaiUse119_01193 | Build on the Stone | 0 | 50 | 3 | - | 0 | y | 66728 (G2) | 66881 (unlisted) | - | (blank NPC 1034221), The Waking Sands (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 66735 | 1199 | GaiUsd201_01199 | Legacy of Allag | 0 | 50 | 8 | - | 0 | y | 70058 (G1) | 66736 (G18) | 67245 (G18) | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A1 | UNLISTED (was near G1 Seventh Umbral Era) |
| 66881 | 1345 | GaiUse201_01345 | Still Waters | 0 | 50 | 3 | - | 0 | y | 66729 (unlisted) | - | - | (blank NPC 1034221), The Rising Stones (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66884 | 1348 | GaiUse204_01348 | Promises to Keep | 0 | 50 | 3 | - | 0 | y | 66883 (G2) | 66885 (unlisted) | - | (blank NPC 1034221), Ul'dah (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 66885 | 1349 | GaiUse205_01349 | A Small-scale Operation | 0 | 50 | 3 | - | 0 | y | 66884 (unlisted) | 66886 (unlisted) | - | (blank NPC 1034221), Ul'dah (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66886 | 1350 | GaiUse206_01350 | Yugiri's Game | 0 | 50 | 3 | - | 0 | y | 66885 (unlisted) | 66887 (unlisted) | - | (blank NPC 1034221), Western Thanalan (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66887 | 1351 | GaiUse207_01351 | If Wishes Were Horsebirds | 0 | 50 | 3 | - | 0 | y | 66886 (unlisted) | - | - | (blank NPC 1034221), Western Thanalan (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66889 | 1353 | GaiUse209_01353 | All Due Respect | 0 | 50 | 3 | - | 0 | y | 66888 (G2) | 66890 (unlisted) | - | (blank NPC 1034221), Ul'dah (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 66890 | 1354 | GaiUse210_01354 | Full Belly, Happy Heart | 0 | 50 | 3 | - | 0 | y | 66889 (unlisted) | 66891 (unlisted) | - | (blank NPC 1034221), The Rising Stones (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66891 | 1355 | GaiUse211_01355 | Writhing in the Dark | 0 | 50 | 3 | - | 0 | y | 66890 (unlisted) | - | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66893 | 1357 | GaiUse213_01357 | Fireworks and Fish Don't Mix | 0 | 50 | 3 | - | 0 | y | 66892 (G2) | - | - | (blank NPC 1034221), Western La Noscea (quest PlaceName) | A2 | UNLISTED (was near G2 Seventh Astral Era) |
| 66980 | 1444 | GaiUse303_01444 | Shock and Awe | 0 | 50 | 3 | - | 0 | y | 66979 (G2) | 66981 (unlisted) | - | (blank NPC 1034221), Central Thanalan (quest PlaceName) | A1 | UNLISTED (was near G2 Seventh Astral Era) |
| 66981 | 1445 | GaiUse304_01445 | Reap the Whirlwind | 0 | 50 | 3 | - | 0 | y | 66980 (unlisted) | - | - | (blank NPC 1034221), Ul'dah (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66985 | 1449 | GaiUse308_01449 | A Sylphlands Sting | 0 | 50 | 3 | - | 0 | y | 66984 (G2) | 66986 (unlisted) | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A2 | UNLISTED (was near G2 Seventh Astral Era) |
| 66986 | 1450 | GaiUse309_01450 | Scattered Scions | 0 | 50 | 3 | - | 0 | y | 66985 (unlisted) | 66987 (unlisted) | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66987 | 1451 | GaiUse310_01451 | True to Form | 0 | 50 | 3 | - | 0 | y | 66986 (unlisted) | 66988 (unlisted) | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A2 | UNLISTED (was near -) |
| 66988 | 1452 | GaiUse311_01452 | Levin an Impression | 0 | 50 | 3 | - | 0 | y | 66987 (unlisted) | - | - | (blank NPC 1034221), East Shroud (quest PlaceName) | A1 | UNLISTED (was near -) |
| 66990 | 1454 | GaiUse313_01454 | A Hard Hapalit to Break | 0 | 50 | 3 | - | 0 | y | 66989 (G2) | 66991 (unlisted) | - | (blank NPC 1034221), The Rising Stones (quest PlaceName) | A2 | UNLISTED (was near G2 Seventh Astral Era) |
| 66991 | 1455 | GaiUse314_01455 | Picking Up the Sledge | 0 | 50 | 3 | - | 0 | y | 66990 (unlisted) | - | - | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 67097 | 1561 | GaiUsx110_01561 | Welcome to Morbol Country | 0 | 50 | 3 | - | 0 | y | 66717 (unlisted), 66718 (unlisted), 66719 (unlisted) | 67098 (unlisted) | 66720 (unlisted) | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 67098 | 1562 | GaiUsx111_01562 | Answering the Call | 0 | 50 | 3 | - | 0 | y | 66720 (unlisted), 67097 (unlisted) | 66722 (unlisted) | 66721 (unlisted) | (blank NPC 1034221), Mor Dhona (quest PlaceName) | A2 | UNLISTED (was near -) |
| 67635 | 2099 | HeaVny805_02099 | The Favors of the House | 1 | 60 | 10 | - | 0 | - | 67634 (G116) | - | - | (blank NPC 1034221) | A2 | UNLISTED (was near G116 Mor Dhonan Sidequests) |
| 67642 | 2106 | HeaVny806_02106 | Leves of Ishgard | 1 | 50 | 10 | - | 0 | - | 67118 (G3) | - | - | Eloin, Foundation | B2 | G117 Ishgardian Sidequests |
| 67643 | 2107 | HeaVny807_02107 | Sights of the North | 1 | 52 | 10 | - | 0 | - | 67145 (G3), 65698 (G113) | - | - | Kester Ironheart, The Dravanian Forelands | B2 | G119 Dravanian Forelands Sidequests |
| 67645 | 2109 | JobMch299_02109 | So You Want to Be a Machinist | 1 | 50 | 10 | - | 0 | - | 65964 (G2) | 67232 (G187) | - | Stephanivien, Foundation | B1 | 999 / Job...299 -> genre of first listed successor G187 |
| 67646 | 2110 | JobDrk299_02110 | A Dark Spectacle | 1 | 50 | 10 | - | 0 | - | 65964 (G2) | 67589 (G186) | - | Ishgardian citizen, The Pillars | B1 | 999 / Job...299 -> genre of first listed successor G186 |
| 67653 | 2117 | HeaVny808_02117 | I Believe I Can Fly | 1 | 52 | 8 | - | 0 | y | 66698 (G113), 67145 (G3) | - | - | (blank NPC 1034221) | A2 | UNLISTED (was near G113 Gridanian Sidequests) |
| 67659 | 2123 | JobAst299_02123 | What's Your Sign | 1 | 50 | 10 | - | 0 | - | 65964 (G2) | 67548 (G188) | - | Jannequinard, The Pillars | B1 | 999 / Job...299 -> genre of first listed successor G188 |
| 67752 | 2216 | HeaVny814_02216 | The New Frontier | 1 | 60 | 10 | - | 0 | - | 67205 (G3) | - | - | Aurvael | B2 | UNLISTED |
| 67870 | 2334 | JobRel399_02334 | Recondition the Anima | 1 | 60 | 4 | yes/0 | 0 | - | 67864 (G89) | - | - | Ulan, Idyllshire | C2 | G89 Anima Weapons |
| 67923 | 2387 | SubCts711_02387 | What Lies Beneath | 1 | 17 | 10 | - | 0 | - | 67092 (G103) | - | - | Wood Wailer expeditionary captain, South Shroud | B2 | G113 Gridanian Sidequests |
| 67925 | 2389 | ComGrd101_02389 | Squadron and Commander (Twin Adder) | 0 | 47 | 10 | - | 2 | - | 66967 (G112) | - | 67926 (unlisted), 67927 (unlisted) | serpent personnel officer, New Gridania | B2 | G235 Order of the Twin Adder Quests |
| 67926 | 2390 | ComLim101_02390 | Squadron and Commander (Maelstrom) | 0 | 47 | 10 | - | 1 | - | 66967 (G112) | - | 67925 (unlisted), 67927 (unlisted) | storm personnel officer, Limsa Lominsa Upper Decks | B2 | G234 Maelstrom Quests |
| 67927 | 2391 | ComUld101_02391 | Squadron and Commander (Immortal Flames) | 0 | 47 | 10 | - | 3 | - | 66967 (G112) | - | 67926 (unlisted), 67925 (unlisted) | flame personnel officer, Ul'dah - Steps of Nald | B2 | G236 Immortal Flames Quests |
| 68148 | 2612 | StmBdy403_02612 | And We Shall Call It Pyros | 2 | 70 | 10 | - | 0 | - | - | - | - | Rodney, Kugane | B2 | G128 Kugane Sidequests |
| 68149 | 2613 | StmBdy404_02613 | And We Shall Call It Hydatos | 2 | 70 | 10 | - | 0 | - | - | - | - | Rodney, Kugane | B2 | G128 Kugane Sidequests |
| 68456 | 2920 | StmBdy102_02920 | Sights of Crimson and Dawn | 2 | 60 | 10 | - | 0 | - | 65698 (G113), 67985 (G6) | - | - | Ulger Ironheart, Rhalgr's Reach | B2 | G124 Rhalgr's Reach Sidequests |
| 68457 | 2921 | StmBdy103_02921 | Leves of Kugane | 2 | 60 | 10 | - | 0 | - | 67895 (G5) | - | - | Keltraeng, Kugane | B2 | G128 Kugane Sidequests |
| 68477 | 2941 | StmBdy110_02941 | Reach Long and Prosper | 2 | 60 | 10 | - | 0 | - | 67631 (G117) | 68541 (G124) | - | Galiena, Rhalgr's Reach | B2 | G124 Rhalgr's Reach Sidequests |
| 68478 | 2942 | StmBdy402_02942 | And We Shall Call It Pagos | 2 | 70 | 10 | - | 0 | - | - | - | - | Rodney, Kugane | B2 | G128 Kugane Sidequests |
| 68479 | 2943 | StmBdy201_02943 | Songs in the Key of Kugane | 2 | 1 | 10 | - | 0 | - | 68089 (G6) | - | - | wandering minstrel, Kugane | B2 | G128 Kugane Sidequests |
| 68555 | 3019 | SubCts051_03019 | Plucking the Heartstrings | 0 | 30 | 10 | - | 0 | - | 66621 (G180) | - | - | Simpkin, Old Gridania | B2 | G180 Bard Quests |
| 68624 | 3088 | SubCts501_03088 | Recall of Duty | 0 | 70 | 8 | - | 0 | - | 70058 (G1) | - | - | wandering minstrel, Mor Dhona | C1 | G116 Mor Dhonan Sidequests |
| 68753 | 3217 | LucKbc008_03217 | To Be Second Best | 3 | 80 | 1 | - | 0 | y | 68165 (G184), 69164 (G220), 69190 (G8) | - | - | (blank NPC 1034221), Gridania (quest PlaceName) | A1 | UNLISTED (was near G184 Summoner Quests) |
| 69138 | 3602 | LucKyr001_03602 | Leves of the Crystarium | 3 | 70 | 10 | - | 0 | - | 68817 (G8) | - | - | Eirikur, The Crystarium | B2 | G132 Crystarium Sidequests |
| 69139 | 3603 | LucKyr002_03603 | The Boutique Always Wins | 3 | 70 | 10 | - | 0 | - | 68822 (G8), 67631 (G117) | 70266 (G102), 69265 (G133) | - | Mowen, Eulmore | B2 | G133 Eulmore Sidequests |
| 69140 | 3604 | LucKyr003_03604 | Sights of the First | 3 | 70 | 10 | - | 0 | - | 65698 (G113), 68817 (G8) | - | - | Eirlalth, The Crystarium | B2 | G132 Crystarium Sidequests |
| 69141 | 3605 | LucKyr004_03605 | The Aspiring Skywatcher | 3 | 80 | 10 | - | 0 | - | 69190 (G8) | - | - | Lor Feo, The Crystarium | B2 | G132 Crystarium Sidequests |
| 69198 | 3662 | LucKyb006_03662 | Minstrel from Another Mother | 3 | 1 | 10 | - | 0 | - | 69190 (G8) | - | - | minstreling wanderer, The Crystarium | B2 | G132 Crystarium Sidequests |
| 69295 | 3759 | SubCts502_03759 | Memories Rekindled | 0 | 1 | 10 | - | 0 | - | 70058 (G1) | - | - | wistful whitebeard, Western Thanalan | B2 | G114 Ul'dahn Sidequests |
| 69296 | 3760 | LucKha002_03760 | The Mendicant's Court | 1 | 1 | 1 | - | 0 | - | 69208 (G100) | - | - | Francel, The Firmament | C2 | G100 Ishgardian Restoration Main Quests |
| 69377 | 3841 | LucKha011_03841 | The New Nest | 1 | 1 | 1 | - | 0 | - | 69208 (G100) | - | - | Augebert, The Firmament | C2 | G100 Ishgardian Restoration Main Quests |
| 69378 | 3842 | LucKyb007_03842 | Weapon of Choice | 3 | 80 | 10 | - | 0 | - | 69319 (G28) | - | - | warmachina fanatic, The Lochs | B2 | G28 Garlemald's Machinations |
| 69478 | 3942 | LucKsa102_03942 | Memoirs from the Front | 3 | 1 | 8 | - | 0 | - | 69477 (G91) | 69479 (G91) | - | Resistance historian, Bozjan Southern Front | C1 | G91 Resistance Weapons |
| 69508 | 3972 | LucKha021_03972 | Featherfall | 1 | 1 | 1 | - | 0 | - | 69208 (G100) | - | - | Augebert, The Firmament | C2 | G100 Ishgardian Restoration Main Quests |
| 69563 | 4027 | LucKsa211_04027 | A Seaside Story | 3 | 1 | 8 | - | 0 | - | 69562 (G91) | - | - | troubled gentleman, Gangos | C1 | G91 Resistance Weapons |
| 69566 | 4030 | SubGsc108_04030 | Open and Inviting | 0 | 1 | 8 | - | 0 | - | - | - | - | Open tournament official, The Gold Saucer | C1 | G114 Ul'dahn Sidequests |
| 69577 | 4041 | LucKsa299_04041 | Forged Anew | 3 | 80 | 4 | yes/0 | 0 | - | 69576 (G91) | - | - | Allagan node, Gangos | C2 | G91 Resistance Weapons |
| 69578 | 4042 | LucKha031_04042 | The Risensong Quarter | 1 | 1 | 1 | - | 0 | - | 69208 (G100) | - | - | Augebert, The Firmament | C2 | G100 Ishgardian Restoration Main Quests |
| 69580 | 4044 | LucKta511_04044 | A Message from Konogg | 3 | 1 | 8 | - | 0 | - | 69579 (G27) | 69581 (unlisted) | - | dig site chief, Kholusia | C1 | G27 YoRHa: Dark Apocalypse |
| 69581 | 4045 | LucKta512_04045 | Dwarves of a Beard | 3 | 1 | 8 | - | 0 | - | 69580 (unlisted) | 69587 (G27), 69582 (unlisted) | - | dig site chief, Kholusia | C1 | G27 YoRHa: Dark Apocalypse |
| 69582 | 4046 | LucKta513_04046 | Strange Glagg | 3 | 1 | 8 | - | 0 | - | 69581 (unlisted) | 69583 (unlisted) | - | dig site chief, Kholusia | C1 | G27 YoRHa: Dark Apocalypse |
| 69583 | 4047 | LucKta514_04047 | Dwarves of a Beard Freshened | 3 | 1 | 8 | - | 0 | - | 69582 (unlisted) | 69588 (G27), 69584 (unlisted) | - | dig site chief, Kholusia | C1 | G27 YoRHa: Dark Apocalypse |
| 69584 | 4048 | LucKta515_04048 | Stranger Glagg | 3 | 1 | 8 | - | 0 | - | 69583 (unlisted) | 69585 (unlisted) | - | dig site chief, Kholusia | C1 | G27 YoRHa: Dark Apocalypse |
| 69585 | 4049 | LucKta516_04049 | All's Well That Ends with Ale | 3 | 1 | 8 | - | 0 | - | 69584 (unlisted) | 69589 (G27) | - | dig site chief, Kholusia | C1 | G27 YoRHa: Dark Apocalypse |
| 69630 | 4094 | LucKsa321_04094 | An Honor to Serve | 3 | 1 | 8 | - | 0 | - | 69624 (G91) | - | - | Resistance councilor, Zadnor | C1 | G91 Resistance Weapons |
| 69706 | 4170 | AktKyr101_04170 | Leves of Old Sharlayan | 4 | 80 | 10 | - | 0 | - | 69894 (G11) | - | - | Grigge, Old Sharlayan | B2 | G140 Old Sharlayan Sidequests |
| 69707 | 4171 | AktKyr102_04171 | I Wandered Sharlayan as a Minstrel | 4 | 1 | 10 | - | 0 | - | 70000 (G11) | - | - | wandering minstrel, Old Sharlayan | B2 | G140 Old Sharlayan Sidequests |
| 69710 | 4174 | AktKyr105_04174 | Sights of the End | 4 | 80 | 10 | - | 0 | - | 65698 (G113), 69894 (G11) | - | - | Babeth Ironheart, Old Sharlayan | B2 | G140 Old Sharlayan Sidequests |
| 69711 | 4175 | AktKyr106_04175 | Expanding House of Splendors | 4 | 80 | 10 | - | 0 | - | 67631 (G117) | - | - | Ofpilona, Radz-at-Han | B2 | G141 Radz-at-Han Sidequests |
| 70180 | 4644 | AktKua102_04644 | Seeing the Cieldalaes | 0 | 1 | 8 | - | 0 | - | 70058 (G1) | - | 70179 (G107) | Baldin, Lower La Noscea | C1 | G112 La Noscean Sidequests |
| 70187 | 4651 | AktKva001_04651 | An Odd Job | 4 | 90 | 10 | - | 0 | - | 70000 (G11) | 70182 (G108), 70269 (G108), 70325 (G108), 70977 (G108) | - | Osmon, Old Sharlayan | B2 | G108 Variant Dungeons |
| 70539 | 5003 | KinGyr101_05003 | Leves of Tuliyollal | 5 | 90 | 10 | - | 0 | - | 70400 (G13) | - | - | Malihali, Tuliyollal | B2 | G148 Tuliyollal Sidequests |
| 70540 | 5004 | KinGyr102_05004 | How the West Was Sung | 5 | 1 | 10 | - | 0 | - | 70495 (G13) | - | - | wandering minstrel, Tuliyollal | B2 | G148 Tuliyollal Sidequests |
| 70542 | 5006 | KinGyr104_05006 | Sights of the West | 5 | 90 | 10 | - | 0 | - | 65698 (G113), 70400 (G13) | 70543 (unlisted) | - | Elsebee Ironheart, Tuliyollal | B2 | G148 Tuliyollal Sidequests |
| 70543 | 5007 | KinGyr105_05007 | Sights of the West and Beyond | 5 | 90 | 10 | - | 0 | - | 70542 (unlisted) | - | - | Emeline Ironheart, Solution Nine | B2 | G154 Solution Nine Sidequests |
| 70544 | 5008 | KinGyr106_05008 | Dawn of a New Deal | 5 | 90 | 10 | - | 0 | - | 67631 (G117) | - | - | Rhodina, Solution Nine | B2 | G154 Solution Nine Sidequests |
| 70944 | 5408 | KinGdd104_05408 | The Pilgrim's Answer | 5 | 91 | 4 | - | 0 | - | 70943 (G106), 70136 (G12), 69521 (G221) | - | - | Cyella, The Crystarium | C2 | G106 Pilgrim's Traverse |
| 70995 | 5459 | KinGva199_05459 | Abridged Too Far | 5 | 100 | 8 | - | 0 | - | - | - | - | Memolivia, Old Sharlayan | C1 | G140 Old Sharlayan Sidequests |
| 71036 | 5500 | JobXbm191_05500 | Free for All | 5 | 50 | 8 | - | 0 | - | 71035 (G198) | 71037 (unlisted) | - | Sylmond, Central Shroud | C1 | G198 Beastmaster Quests |
| 71037 | 5501 | JobXbm192_05501 | Mastery Rematch | 5 | 50 | 8 | - | 0 | - | 71036 (unlisted) | 71045 (unlisted) | - | Sylmond, Central Shroud | C1 | G198 Beastmaster Quests |
| 71045 | 5509 | JobXbm193_05509 | Blazing Trails | 5 | 50 | 8 | - | 0 | - | 71037 (unlisted) | - | - | Sylmond, Central Shroud | C1 | G198 Beastmaster Quests |

## Appendix A. Rule outcome counts (probe output)

| Group | Rows |
|---|---:|
| A1 retired, replaced by same-name row | 44 |
| A2 retired, no replacement | 55 |
| B1 class/job intro quasi-quest | 23 |
| B2 quasi-quest (EventIconType 10) | 35 |
| C1 hidden feature step (EventIconType 8) | 16 |
| C2 hidden step (EventIconType 1) | 4 |
| C2 hidden step (EventIconType 4) | 3 |

| Group -> target | Rows |
|---|---:|
| A1 retired, replaced by same-name row -> UNLISTED (was near -) | 18 |
| A1 retired, replaced by same-name row -> UNLISTED (was near G1 Seventh Umbral Era) | 17 |
| A1 retired, replaced by same-name row -> UNLISTED (was near G184 Summoner Quests) | 1 |
| A1 retired, replaced by same-name row -> UNLISTED (was near G2 Seventh Astral Era) | 8 |
| A2 retired, no replacement -> UNLISTED (was near -) | 27 |
| A2 retired, no replacement -> UNLISTED (was near G1 Seventh Umbral Era) | 23 |
| A2 retired, no replacement -> UNLISTED (was near G113 Gridanian Sidequests) | 1 |
| A2 retired, no replacement -> UNLISTED (was near G116 Mor Dhonan Sidequests) | 1 |
| A2 retired, no replacement -> UNLISTED (was near G2 Seventh Astral Era) | 3 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G156 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G157 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G158 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G159 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G160 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G161 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G162 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G163 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G164 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G165 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G166 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G167 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G168 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G169 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G170 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G171 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G172 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G173 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G174 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G175 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G186 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G187 | 1 |
| B1 class/job intro quasi-quest -> 999 / Job...299 -> genre of first listed successor G188 | 1 |
| B2 quasi-quest (EventIconType 10) -> G108 Variant Dungeons | 1 |
| B2 quasi-quest (EventIconType 10) -> G113 Gridanian Sidequests | 1 |
| B2 quasi-quest (EventIconType 10) -> G114 Ul'dahn Sidequests | 4 |
| B2 quasi-quest (EventIconType 10) -> G117 Ishgardian Sidequests | 1 |
| B2 quasi-quest (EventIconType 10) -> G119 Dravanian Forelands Sidequests | 1 |
| B2 quasi-quest (EventIconType 10) -> G124 Rhalgr's Reach Sidequests | 2 |
| B2 quasi-quest (EventIconType 10) -> G128 Kugane Sidequests | 5 |
| B2 quasi-quest (EventIconType 10) -> G132 Crystarium Sidequests | 4 |
| B2 quasi-quest (EventIconType 10) -> G133 Eulmore Sidequests | 1 |
| B2 quasi-quest (EventIconType 10) -> G140 Old Sharlayan Sidequests | 3 |
| B2 quasi-quest (EventIconType 10) -> G141 Radz-at-Han Sidequests | 1 |
| B2 quasi-quest (EventIconType 10) -> G148 Tuliyollal Sidequests | 3 |
| B2 quasi-quest (EventIconType 10) -> G154 Solution Nine Sidequests | 2 |
| B2 quasi-quest (EventIconType 10) -> G180 Bard Quests | 1 |
| B2 quasi-quest (EventIconType 10) -> G234 Maelstrom Quests | 1 |
| B2 quasi-quest (EventIconType 10) -> G235 Order of the Twin Adder Quests | 1 |
| B2 quasi-quest (EventIconType 10) -> G236 Immortal Flames Quests | 1 |
| B2 quasi-quest (EventIconType 10) -> G28 Garlemald's Machinations | 1 |
| B2 quasi-quest (EventIconType 10) -> UNLISTED  | 1 |
| C1 hidden feature step (EventIconType 8) -> G112 La Noscean Sidequests | 1 |
| C1 hidden feature step (EventIconType 8) -> G114 Ul'dahn Sidequests | 1 |
| C1 hidden feature step (EventIconType 8) -> G116 Mor Dhonan Sidequests | 1 |
| C1 hidden feature step (EventIconType 8) -> G140 Old Sharlayan Sidequests | 1 |
| C1 hidden feature step (EventIconType 8) -> G198 Beastmaster Quests | 3 |
| C1 hidden feature step (EventIconType 8) -> G27 YoRHa: Dark Apocalypse | 6 |
| C1 hidden feature step (EventIconType 8) -> G91 Resistance Weapons | 3 |
| C2 hidden step (EventIconType 1) -> G100 Ishgardian Restoration Main Quests | 4 |
| C2 hidden step (EventIconType 4) -> G106 Pilgrim's Traverse | 1 |
| C2 hidden step (EventIconType 4) -> G89 Anima Weapons | 1 |
| C2 hidden step (EventIconType 4) -> G91 Resistance Weapons | 1 |

## Appendix B. Territory -> regional sidequest genre vote (rule 6)

Computed over listed quests in categories 59-85 with a non-zero `IssuerLocation` territory; only territories that an unlisted quest is issued from are shown. `n/total` is the winning genre's share.

| Territory | Place | Winning genre | Share |
|---:|---|---|---:|
| 128 | Limsa Lominsa Upper Decks | G112 La Noscean Sidequests | 15/15 |
| 129 | Limsa Lominsa Lower Decks | G112 La Noscean Sidequests | 14/14 |
| 130 | Ul'dah - Steps of Nald | G114 Ul'dahn Sidequests | 15/15 |
| 131 | Ul'dah - Steps of Thal | G114 Ul'dahn Sidequests | 13/13 |
| 132 | New Gridania | G113 Gridanian Sidequests | 15/15 |
| 133 | Old Gridania | G113 Gridanian Sidequests | 12/12 |
| 135 | Lower La Noscea | G112 La Noscean Sidequests | 21/21 |
| 140 | Western Thanalan | G114 Ul'dahn Sidequests | 30/30 |
| 144 | The Gold Saucer | G114 Ul'dahn Sidequests | 4/4 |
| 148 | Central Shroud | G113 Gridanian Sidequests | 21/21 |
| 153 | South Shroud | G113 Gridanian Sidequests | 24/24 |
| 156 | Mor Dhona | G116 Mor Dhonan Sidequests | 43/43 |
| 398 | The Dravanian Forelands | G119 Dravanian Forelands Sidequests | 58/58 |
| 418 | Foundation | G117 Ishgardian Sidequests | 12/12 |
| 419 | The Pillars | G117 Ishgardian Sidequests | 11/11 |
| 478 | Idyllshire | G122 Idyllshire Sidequests | 33/33 |
| 621 | The Lochs | G127 Lochs Sidequests | 10/10 |
| 628 | Kugane | G128 Kugane Sidequests | 20/20 |
| 635 | Rhalgr's Reach | G124 Rhalgr's Reach Sidequests | 13/13 |
| 814 | Kholusia | G135 Kholusia Sidequests | 43/43 |
| 819 | The Crystarium | G132 Crystarium Sidequests | 20/20 |
| 820 | Eulmore | G133 Eulmore Sidequests | 14/14 |
| 886 | The Firmament | (no listed section-3 quests) |  |
| 915 | Gangos | (no listed section-3 quests) |  |
| 920 | Bozjan Southern Front | (no listed section-3 quests) |  |
| 962 | Old Sharlayan | G140 Old Sharlayan Sidequests | 25/25 |
| 963 | Radz-at-Han | G141 Radz-at-Han Sidequests | 7/8 |
| 975 | Zadnor | (no listed section-3 quests) |  |
| 1185 | Tuliyollal | G148 Tuliyollal Sidequests | 23/23 |
| 1186 | Solution Nine | G154 Solution Nine Sidequests | 12/12 |

## Appendix C. Method

- Probe: a net10.0 console project in the session scratchpad referencing `Tsukimichi.Core` and `Tsukimichi.GameData`, opening the sqpack with `Lumina.GameData` exactly as `Tsukimichi.Tests/Data/CatalogLoaderTests.cs` does, then joining `CatalogMapper.Map` output with the raw `Quest`, `JournalGenre`, `TerritoryType`, `ENpcResident`, `EventIconType` and `Festival` sheets. Total runtime about 6 s per run. The project stays in the scratchpad (`UnlistedProbe60059/`) and nothing was added to the repo besides this file.
- The scratchpad `Probe/` folder is shared with other sessions and was overwritten mid-run by another agent; the outputs used here were copied out before that happened and regenerated from the isolated folder.
- `Festival` sheet rows carry no name strings in this Lumina version, so festival ids are reported raw.

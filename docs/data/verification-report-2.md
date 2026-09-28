# Quest and reward database verification, pass 2

Hand-written report of the 2026-09-28 second verification pass. Game data `2026.09.15.0000.0000` (xivapi version
`541c0c12e07da325`, Lodestone Eorzea Database "Patch 7.56"). Read-only: nothing under `Tsukimichi*` was changed.
Scripts and raw fetches live in the session scratchpad (`quests.json` dump of all 5,533 Quest rows via Lumina 7.7.1 /
Lumina.Excel 7.5.0, `cmp.py`, `collect.py`, `lode.py`, `lode_totals.py`, `lode_diff.py`, `a1.py`, `a2.py`).

Sources used and reachability:

| Source | Status | Used for |
|---|---|---|
| Lodestone Eorzea Database (official) | reachable (curl, UA required) | displayed level, class/job, "Quest/Duty" prerequisites, rewards, per-section/category totals (`?category2=<JournalSection>&category3=<JournalCategory>`) |
| xivapi v2 | reachable | sheet cross-reads (JournalGenre 0, ContentFinderCondition 808/865/15/16, Quest search) |
| Garland Tools (`db/doc/quest/en/2/<row>.json`, `db/doc/browse/en/2/quest.json`) | reachable | prerequisites/joins, job level, instance partials, quest index (5,373 entries, identical to the sheet) |
| FFXIV consolegameswiki (`?action=raw`, `api.php categoryinfo`) | reachable | levels, requirements text, acquisition sections, category page counts |
| FFXIV Collect API | reachable | sources of every collectible-kind entry (249 distinct rewards) |
| Lalachievements | reachable (HTTP 200) | probe only |
| FFXIV Online Store (store.finalfantasyxiv.com) | **unreachable** (search 404, product pages return "technical error") | wanted as the official second source for Mog Station availability; the wiki `{{onlinestore}}` template was used instead |

## 1. `--verify` run

`dotnet run --project Tsukimichi.DataGen -- --verify --game <sqpack> --data Tsukimichi/Data/unique_quests.json --report <scratchpad>/verify-run.md --sample 64 --seed 20260928`

```
data:    Tsukimichi/Data/unique_quests.json (3464 entries, game 2026.09.15.0000.0000)
verify passed (2 observations)     done in 6.3 s   exit=0
Hard checks: all passed.
Sample: 64 entries (seed 20260928), 194 checks, 0 failed. xivapi version 541c0c12e07da325, 21 requests, 0 request errors.
Observations: DutyUnlock entries never get an icon (kind/id mismatch, known); 18 Item entries are unlock items kept despite a consumable-like category.
```

Nothing new from the generator's own checks; every finding below comes from the cross-checks.

## 2. Confirmed inaccuracies

"Confirmed" = two independent sources agree, or the game sheet itself proves it.

| # | What the catalog / data says | What the sources say | Evidence | Proposed fix (where) |
|---|---|---|---|---|
| 1 | **Quest level shown is `ClassJobLevel[0]` alone; `QuestLevelOffset` is stored but never added.** 215 named quests (123 Sidequests, 40 ARR MSQ, 35 allied society, 17 unlisted) display a lower level than the game. E.g. Quarrels with Squirrels shows 1, Surveying the Damage 4, Passing Muster 4, Step Nine 4, Out of House and Home 4, Reach for the Starboard 1. | Lodestone lists them as Lv. **3, 8, 5, 6, 9, 10** = `ClassJobLevel[0] + QuestLevelOffset` (1+2, 4+4, 4+1, 4+2, 4+5, 1+9); the wiki agrees on all 65 sampled quests. Lodestone's requirement line still reads "Any Class or Job Lv. 1" for Quarrels with Squirrels, so the raw value is the acceptance requirement and the sum is the journal level. | https://na.finalfantasyxiv.com/lodestone/playguide/db/quest/d1fe3b80980/ ; search results for the other five; wiki `level =` fields | Keep `QuestRecord.Level` for `RequirementEvaluator`; add a display level (`Level + LevelOffset`) and use it in `Tsukimichi/Ui/TablePane.cs` (lines 295, 364), `DetailPane.cs` (872), `DiscoveryWindow.cs` (468) and the level-range filter in `Tsukimichi.Core/Query/QuestQuery.cs` (441), and for sorting by level. |
| 2 | **Custom delivery satisfaction rank is not a requirement.** `Quest.SatisfactionNpc` / `SatisfactionLevel` are not mapped; 16 quests that need satisfaction rank 4 or 5 evaluate as Ready once the previous quest is done: 68542, 68676, 68714, 69266, 69267, 69426, 69427, 69616, 70060, 70061, 70252, 70253, 70352, 70776, 70777, 70997. | Sheet: `SatisfactionNpc=2, SatisfactionLevel=4` on 68542; wiki: "[[Custom Delivery]] Level 4 with [[M'naago]]" (68542), "level 4 with [[Kurenai]]" (68676). | https://ffxiv.consolegameswiki.com/wiki/Not_While_Their_Names_Are_Still_Spoken ; Quest sheet columns `SatisfactionNpc`, `SatisfactionLevel` | `CatalogMapper.MapQuest`: map both fields; new `RequirementKind.CustomDeliveryRank` in `RequirementEvaluator`; snapshot needs the per-NPC satisfaction rank (`SatisfactionSupplyManager` in ClientStructs) or, failing that, a "not checked" result like Mount/House. |
| 3 | **Delivery Moogle carrier level is not a requirement.** `Quest.DeliveryQuest` is not mapped; all 17 postmoogle quests (65569, 65572, 65776-65780, 65898, 66032, 67106-67113) have **no PreviousQuest at all**, so every level-50 character sees them Ready. | Sheet `DeliveryQuest=7` on 65569 vs wiki "Carrier level 7"; `=12` on 67106 vs wiki "Carrier level 12". Lodestone lists no quest prerequisite either (it does not model carrier level). | https://ffxiv.consolegameswiki.com/wiki/Sweet_Words,_Shadowy_Dealings ; https://ffxiv.consolegameswiki.com/wiki/Death_of_a_Mailman | Map `DeliveryQuest` (its row id equals the carrier level in every row); new requirement "carrier level N" (read from `PlayerState` if available, else "not checked"). |
| 4 | **Seasonal quests are gated on festival id only; the festival phase is ignored.** `Quest.FestivalBegin`/`FestivalEnd` are not mapped (all 294 seasonal rows and 16 collaboration rows carry `FestivalEnd`; 37 rows have `FestivalBegin > 0`), and `GameStateReader.ReadActiveFestivals` keeps only `ActiveFestivals[i].Id`, dropping `.Phase`. A phased event (chapters unlock on later days) shows its later quests as Ready from day one. | Sheet: Hatching-tide 2014 rows 66950-66953 begin 1, 66954 begin 2, 66955 begin 3, 66956 begin 4 (end 5); wiki: "Chapter 1 begins on April 9 ... Chapter 2 April 11, Chapter 3 April 13, Chapter 4 April 15". ClientStructs `GameMain.Festival { ushort Id; ushort Phase }`. Also current-era rows: 69292-69294 (phases 3-5), 69383 (2), 67670 (5-6). | https://ffxiv.consolegameswiki.com/wiki/Hatching-tide_(2014) ; FFXIVClientStructs `GameMain+Festival` | Map `FestivalBegin`/`FestivalEnd`; store `(Id, Phase)` in `CharacterSnapshot.ActiveFestivals`; `RequirementEvaluator` seasonal check = id active **and** `Begin <= Phase <= End`. |
| 5 | **Curated duty unlock 70011 "The Crystal from Beyond" -> Asphodelos: The First Circle (CFC 808) is wrong.** | The raid is unlocked by 70012 "Where Familiars Dare": wiki (`req-quest = Where Familiars Dare` on the duty page; `unlocks = ra Asphodelos: The First Circle` on the quest page; `unlocks =` empty on The Crystal from Beyond) and Garland (70012 `reward.instance 30111`, instance partial "Asphodelos: The First Circle"; 70011 has no instance). CFC 808 has no `UnlockCriteria`, so the sheet is silent. | https://ffxiv.consolegameswiki.com/wiki/Where_Familiars_Dare ; https://www.garlandtools.org/db/doc/quest/en/2/70012.json | `Tsukimichi/Data/curated/duty_unlocks.json`: move the entry from key `70011` to `70012` (70011 stays the chain's first quest), regenerate `feature_quests.json` and `unique_quests.json`. |
| 6 | **Three retired sidequests are listed as live quests** (journal genre set, issuer NPC set, no distinguishing flag): 66023 "Meet, Greet, and Deceit" (Lominsan), 66964 "He's Got a Ticket to Ride" (Gridanian), 66965 "Lend Me Your Ears Already" (Ul'dahn). They evaluate to Blocked/Ready like any level-9 sidequest. | Wiki: "Removed due to inconsistencies in the Mac and Korean version of the game" (patch 3.05) on all three; the Lodestone database omits all three from its category lists (Lominsan 126 vs catalog 129, Gridanian 117 vs 118, Ul'dahn 137 vs 138) while listing every other row of those categories. | https://ffxiv.consolegameswiki.com/wiki/Meet,_Greet,_and_Deceit ; Lodestone `?category2=3&category3=59/60/61` | No sheet field marks them (`IssuerStart`, `PlaceName`, `JournalGenre` all normal; only `CanCancel=false` differs, which MSQ rows share). Add a curated `retired_quests.json` (row ids + note) consumed by `QuestCatalog`/`QuestQuery` to hide or mark them. |
| 7 | **Unlisted quests carry the journal category name "Sephiroth Missions".** `JournalGenre` row 0 (the genre of the 180 unlisted rows) points at `JournalCategory` row 0, whose `Name` is "Sephiroth Missions" and whose `JournalSection` is 255. `CatalogMapper.JournalIndex` keeps row 0's category and section for the unlisted template, so every unlisted quest has `Journal.CategoryName == "Sephiroth Missions"` and `SectionId == 255`; `DetailPane.cs:865` renders `"{Genre} > {Category}"` = " > Sephiroth Missions" for all of them. | Local sheet (dump: all 180 unlisted rows have categoryName "Sephiroth Missions", section 255) and xivapi `JournalGenre/0` -> `JournalCategory 0 {Name: "Sephiroth Missions", JournalSection: 255}`. | https://v2.xivapi.com/api/sheet/JournalGenre/0?fields=Name,JournalCategory.Name,JournalCategory.JournalSection.Name | `CatalogMapper.JournalIndex.Build`: use `JournalRef.None` (empty names, section/category 0) for genre 0 instead of `Template(row 0)`; and/or guard `IsUnlisted` in `DetailPane` before formatting the journal path. |
| 8 | **68 unique-reward entries are seasonal-event collectibles that are also sold on the FFXIV Online Store (Mog Station)**, with no `otherSource` marker (only 6 of them carry one, for tradability): 25 Minion, 21 Emote, 11 Mount, 5 Barding, 4 Orchestrion, 2 Ornament. Examples: Starlight Bear (68546), Witch's Broom (67687), Wind-up Iceheart/Yugiri (67670), Pumpkin Butler (67686), Eggshilaration System (67959), Red Moon Parasol (69627), Postmoogle Barding (70002), Rising Phoenix (70302), Clowning Around hairstyle (70056). Full list in scratchpad `premium_list.json`. | FFXIV Collect lists `Premium: Online Store` for all 68; the wiki item pages carry `{{onlinestore|...}}` under Acquisition for the three checked (Starlight Bear Horn, Witch's Broom, Wind-up Iceheart). The sheets cannot know the store, so the exclusivity rule cannot catch these. | https://ffxivcollect.com/api/mounts?name_en_cont=Starlight%20Bear ; https://ffxiv.consolegameswiki.com/wiki/Starlight_Bear_Horn | Add a curated `online_store.json` (item ids, or "every past-year festival collectible") that `CuratedOverlay` turns into `otherSource=OnlineStore` on the entry `source`, so the Moonlit pane can say "quest reward, also on the Online Store". 19 further event rewards are event-only per FFXIV Collect (FFXVI/DQX collab items and the 2025-26 rewards not yet re-sold). |

Related but already-marked: TripleTriadCard 444 "King Elmer III" (70858) is also 48,000 MGP at the Gold Saucer; the entry already carries `otherSource=SpecialShop`.

## 3. Quest sheet fields the mapper/evaluator do not consume

Lumina `Quest` row (API 15, Lumina.Excel 7.5.0) versus `CatalogMapper.MapQuest`. Counts are named quests (5,373) with a non-zero value.

| Field | Non-zero rows | What it is | Consumed? | Assessment |
|---|---:|---|---|---|
| `QuestLevelOffset` | 215 | added to `ClassJobLevel[0]` for the journal level | mapped to `LevelOffset`, never used | **inaccuracy #1** |
| `SatisfactionNpc`, `SatisfactionLevel` | 28 / 16 | custom delivery NPC and required satisfaction rank | no | **inaccuracy #2** |
| `DeliveryQuest` | 17 | delivery moogle carrier level | no | **inaccuracy #3** |
| `FestivalBegin`, `FestivalEnd` | 37 / 310 | festival phase window | no (and Phase not read from the client) | **inaccuracy #4** |
| `QuestLockJoin` | 5,373 (always 2) | how `QuestLock` slots combine | no | harmless: the value is 2 (any) on every row, which is what `StateResolver` rule 2 implements; 64 rows have locks |
| `ClassJobCategory1`, `ClassJobLevel[1]` | 50 / 9 | second class/job category and level | `ClassJobCategory1` mapped, never evaluated; level[1] not mapped | 41 rows have category 1 "All Classes" level 0 (no-op); 9 Ixal daily rows have BTN/MIN/FSH level 1 - see suspected list |
| `BellStart`, `BellEnd` | 12 | Eorzean time window (18:00-06:00) | no | only the 2013/2014 All Saints' Wake rows 66699-66710 (retired festivals) |
| `LevelMax` | 867 | upper level for XP scaling (e.g. Vanu Vanu quests 50/59) | mapped, unused | not a requirement; fine |
| `SystemReward[0]` | 48 | Trait row id (48/48 match the `Trait.Quest` reverse links already shipped) | no | redundant with `Trait` entries |
| `SystemReward[1]` | 49 | system-unlock id (21 = "Way of the ..." class intro quests, 6 = level-10 class quests, 7 = Simply the Hest, 93 = An Ill-conceived Venture, 97 = A Sight to Behold, 17 = My Little Chocobo, 26 = Beauty Is Only Scalp Deep, 92 = Rising to the Challenge, 99 = Let the Hunt Begin, 521-537 = Occult Crescent, 632 = Wisdom's End ...) | no | a sheet-provided signal for the hand-maintained `system_unlocks.json`: only 11 of the 49 rows are in the curated file; the id -> feature meaning is undocumented, so listed as suspected, not as an inaccuracy |
| `GCTypeReward` | 3 | 65665, 65856, 66005 (level-20 Grand Company quests) | no | unknown meaning |
| `ReputationReward` | 566 | tribal reputation points | no | display only |
| `TomestoneReward`, `TomestoneCountReward`, `Tomestone` | 306 / 306 / 28 | tomestone rewards | no (currency comes through `CurrencyReward`) | display only |
| `QuestRewardOtherDisplay` | 79 | display variant of `OtherReward` (e.g. 66591 Paladin's Pledge: 10 vs 0; 67090: 0 vs 9) | no | display only |
| `RewardStain`, `OptionalItemStainReward`, `OptionalItemIsHQReward`, `ItemCatalyst` | - | reward dyes / HQ flags / catalysts | no | display only |
| `QuestRepeatFlag` | 12 | repeat-flag slot for a few repeatables | no | `DailyDone` is read from the client instead; fine |
| `Header` | 57 | 6 on the 20 "So You Want to Be a ..." rows, otherwise scattered | no | unknown |
| `Type` | 1,151 | 8 on 488 sidequests, 1/3 on tribal rows, 10 on 151 sidequests | no | unknown |
| `Introduction`, `CanCancel`, `HideOfferIcon`, `HideInScenarioGuide` | 4,282 / 4,181 / 0 / 242 | client flags | no | `HideInScenarioGuide` marks 242 rows (HW 3.x "Vna" MSQ etc.); none identifies retired quests |
| `Unknown0..12`, `Unknown_70` | few | - | no | unknown |
| `TargetEnd`, `PlaceName`, `QuestClassJobSupply`, `ClientBehavior` | - | hand-in NPC, zone, supply subrows | no | `PlaceName` would give the Lodestone "Area" column for free |
| `ExpansionUnlock`, `ReleaseLevel` | - | not columns of this sheet (they were guesses in the task) | - | - |

Verified as correctly consumed: `PreviousQuestJoin` (1 = all, 2 = any; 102 rows use 2, e.g. 65564 To the Bannock over the three Close to Home variants - Garland and Lodestone agree), `InstanceContentJoin` (1 = all on 38 rows; 65899 Good Intentions needs all three hard primals - Lodestone "All of the above quests complete"; 2 = any on the rest, none of which has more than one instance), `QuestLock` (64 rows), Grand Company / rank, `BeastTribe` / rank / value, `MountRequired` (2 rows), `IsHouseRequired` (1 row), `Festival` id, `ClassJobRequired` (631 rows), `ClassJobCategory0`. `UnlockedInstances` is filled from `UIState.IsInstanceContentCompleted`, i.e. completion, which is what the `InstanceContent` gate means.

## 4. Suspected but not confirmed

- **Ixal daily quests and `ClassJobCategory1`.** 67034, 67035, 67045-67047, 67050, 67056-67058 have `ClassJobCategory0 = Disciple of the Hand L1` and `ClassJobCategory1 = BTN/MIN/FSH L1`. Garland lists both as job requirements; Lodestone lists only "Disciple of the Hand Lv. 1"; the wiki says "To complete this quest, you will need to change your class to botanist". Most likely a completion hint, so ignoring it is harmless, but if the client treats it as an alternative acceptance category the evaluator would wrongly report "not available on the current job" for a BTN/MIN/FSH.
- **Cape Westwind (CFC 865) as a `DutyUnlock` of Operation Archon (66572, 70057).** CFC 865 is a "Quest Battles" solo instance (6.1), not a Duty Finder duty; whether it belongs in the duty-unlock list is a definition question. The Praetorium/Castrum links are correct (Garland, Lodestone).
- **Lodestone omits, wiki keeps:** 66033 But I Hardly Noah, 66034 The Gift of the Archmagus (Crystal Tower; wiki says the Archmagus quest is "somewhat redundant" since 5.3 but exists), 68629 Makin' Bacon (Bread) and 68727 Wok on By (level-1 Return to Ivalice rows), 68543 Earning Your Wings, 70121 The Crystal (Line's) Call, 67819 A Seat at the Feast (promotional "featurequasi" quests). One source only; not listed as retired.
- **`SystemReward[1]` semantics** (see table above): 38 quests with a system-unlock id are absent from `system_unlocks.json` (the six level-10 class quests with id 6, the "Way of the ..." rows with 21, 65700 Magiteknical Difficulties 94, 66967 Rising to the Challenge 92, 70845-70853 Occult Crescent 521-537, 71054 Wisdom's End 632 ...). Worth decoding before trusting; if id 6/21 are the Hall of the Novice / class-quest markers they are not features.
- **Seasonal past-year rows.** 294 seasonal rows span 111 festival ids; only the highest id per genre can ever be active again (All Saints' Wake 176, Starlight 258, Hatching-tide 172, Heavensturn 157, Little Ladies' 169, Moonfire 174, Rising 175, Valentione's 163, Gold Saucer 173). The other ~273 rows (plus 8 exact-duplicate 2013 city variants: Not-so-evil Dead, Show Me the Candy, I Burn for You, Monstrous Mummery x3) are shown as Blocked "seasonal event not active" forever unless the character completed one (then Foreclosed). Not wrong, but noisy; consider hiding rows whose festival id is below the genre maximum, or a curated retired-festival list. `Festival.Name` is empty on all 264 rows, so the sheet cannot name events; `curated/festivals.json` is still empty.
- **Wiki page collision:** "Pitch Perfect" wiki page says level 52; the only sheet row 70230 is a level-80 Loporrit daily. Wiki-side, ignore.

## 5. Totals

### Lodestone Eorzea Database (official) versus the catalog, per journal section

`category2` = JournalSection id, `category3` = JournalCategory id (the Lodestone uses the sheet ids). The Lodestone counts quest rows, not names (215 Seventh Umbral Era entries including the eight "Close to Home").

| Section | Lodestone | Catalog rows | Diff | Explanation (name-level diff, `lode_diff.py`) |
|---|---:|---:|---:|---|
| 0 Main Scenario (ARR-EW) | 907 | 907 | 0 | |
| 1 Main Scenario (Dawntrail) | 140 | 143 | +3 | 71012 A Rush of Cold Wind, 71013 The World Locked in Ice, 71014 Windborne (newest "Winter's Prelude" rows; Lodestone search finds 71013 but its category list lags) |
| 2 Chronicles of a New Era | 192 | 196 | +4 | Crystal Tower 66033, 66034; Return to Ivalice 68629, 68727 (see suspected) |
| 3 Sidequests | 2021 | 2027 | +6 | 3 retired 3.05 sidequests (**inaccuracy #6**); 68543, 70121, 67819 promotional quests |
| 4 Allied Society (ARR-EW) | 612 | 612 | 0 | |
| 5 Allied Society (DT) | 104 | 104 | 0 | |
| 6 Class & Job | 866 | 875 | +9 | Beastmaster job quests 71027-71035 (Lodestone lists 71026 Strangers in the Wood only) |
| 7 Other | 307 | 329 | +22 | 21 seasonal rows >= 70765 (Heavensturn/Starlight/Rising/Moonfire/Valentione's/All Saints'/Hatching-tide/Make It Rain/Little Ladies' 2025-26) + 71002 Keyward Bound; the Lodestone drops the newest event rows |
| All listed | 5149 | 5193 | +44 | sum of the above |
| Unlisted (genre 0) | n/a | 180 | | not in the Lodestone DB by construction |

Per category, every one of the 98 categories matches except: 15 Post-Dawntrail II 15/18, 18 Crystal Tower 8/10, 23 Return to Ivalice 10/12, 59 Lominsan 126/129, 60 Gridanian 117/118, 61 Ul'dahn 137/138, 63 Mor Dhonan 43/44, 93 DoW Job Quests 199/208, 97 Seasonal Events 273/294, 98 Special Quests 19/20 - all explained above. Hildibrand 65/65, Seventh Umbral Era 215/215, every allied society and every Chronicles category other than 18/23 identical.

### Garland Tools

The Garland quest index has exactly the catalog's 5,373 named rows with the same JournalGenre on every row (5,373/5,373), and the same per-section totals. Garland is derived from the same sheets, so it confirms the mapper's reading but is not independent.

### consolegameswiki category pages (pages per category)

Seventh Umbral Era 211 (catalog 215 rows, 207 distinct names; the wiki keeps separate pages for some start-city variants), Seventh Astral Era 80/80, Heavensward 94/94, Stormblood 122/122, Shadowbringers 106/106, Endwalker 108/108, Dawntrail 100/100, Gridanian Sidequests 118/118 (the wiki keeps a page for the retired quest), Ul'dahn 138/138, Beastmaster 10 (catalog genre 198: 10 listed + 3 unlisted), Paladin Quests 19. Sidequests 1,836 pages + 37 subcategories - not comparable directly. Categories are not one-to-one with journal categories (no "Amalj'aa Quests", "Seasonal Events" pages).

### Screenshot totals versus `catalog-stats.md`

Screenshot: MSQ 905, Chronicles 195, Sidequests 2,024, Unlisted 179 (5,187 listed) versus the current 907, 196, 2,027, 180 (5,193). `TreeCounts` applies no filter, so the 7-row difference is the data set, not the UI: the screenshot predates the latest rows (e.g. 71012-71014 MSQ, 71015-71016 Echoes of Vana'diel, 71047-71054 Occult Crescent, 71045 unlisted). Dawntrail 143, Allied 612/104, Class & Job 875, Other 329 and Seasonal 294 are identical.

## 6. Seasonal representation (task 4)

- All 294 rows of category 97 have a non-zero `Festival`; 16 "Collaboration Quests" rows (category 98) also do. `StateResolver` rule 3 makes any quest with an inactive festival **Blocked** (Seasonal requirement, "seasonal event not active"), or **Foreclosed** when `FestivalIsPast` says the run already happened (curated end date - none, `festivals.json` is empty - or any quest of that festival completed). That is the intended behaviour and it holds: no seasonal row can be Ready without the client reporting its festival id. The `SeasonalActiveOnly` filter (`QuestQuery.cs:463`) also keys on the id.
- Gaps: the festival **phase** is ignored (inaccuracy #4); obsolete past-year rows are never hidden (suspected list); the sheet has no festival names.

## 7. Sample verification (tasks 1-2)

- **66 quests** across every section (4 random per section + 2 unlisted + 3 seasonal + 2 Dawntrail allied society + 27 chosen feature/reward/join/offset/satisfaction/delivery cases) against Garland and the wiki: name 66/66; prerequisite set 66/66 (including the `Any` joins 65564, 65781/66211, 69204, 69590); wiki level = `Level + LevelOffset` 65/65 (Pitch Perfect excluded as a wiki collision); rewards consistent (Her Last Vow: Wind-up Gentleman, Manderville Coatee/Bottoms, Most Gentlemanly, instance Battle in the Big Keep).
- **28 Lodestone quest pages** (24 parsed; seasonal pages use a different layout and 3 names returned no search hit - Triple Triad Trial, What Can Eye Do for You, Windborne): displayed level = `Level + LevelOffset` on 19/19; "Class/Job" text = `ClassJobCategory0` name on 19/19 (67034 "Disciple of the Hand Lv. 1" confirms the raw level is the gate); Grand Company on 66237 (Maelstrom); "Quest/Duty" only ever names duties (65899: the three hard primals, "All of the above"); rewards match the catalog (70058 Armor Identification Key = mount item 6008 + Fantasia, which the exclusivity rule correctly drops; 66592 Sheltron + Oath Mastery; 71026 Soul of the Beastmaster + Beast Herder's gear; 70909 The Promise of Tomorrow Orchestrion Roll; 70941 Luminous Oil).
- **249 distinct collectible rewards** (all Mount, Minion, Emote, Orchestrion, Barding, Ornament, Hairstyle, TripleTriadCard entries) on FFXIV Collect: 246 matched by name (3 needed a shorter query: "Endwalker - Footfalls", "Where Daemons Abide", "Clowning Around"); 160 are quest-only or tribal-quest; 68 also on the Online Store (inaccuracy #8); 19 event-only; 1 also Gold Saucer (already marked). Non-seasonal spot checks quest-only: Magitek Armor, Argos, Wind-up Gentleman, Endwalker - Footfalls, Where Daemons Abide.
- **Curated duty unlocks (39)** against Garland instance partials: 34 confirmed by name; 70011 wrong (inaccuracy #5); 66060, 66211, 66476, 66572/70057 have no Garland instance (legacy/variant rows or the Cape Westwind quest battle) but the Praetorium/Sastasha/Stone Vigil/Castrum links are corroborated by the Lodestone and the wiki.
- **Post-7.0 classification:** every Dawntrail-section row has `Expansion = 5`; expansion ranges by row id are consistent (5: 70353-71054). Newest rows (Occult Crescent 71047-71054, Phantom Weapons 71038-71042, Cosmic Exploration 71019-71025, Beastmaster 71026-71035, Winter's Prelude 71006-71014, Rising 2026 71046/71056) all carry the expected genre; 5 new rows are unlisted by the sheet itself (70944 The Pilgrim's Answer, 70995 Abridged Too Far, 71036/71037/71045 Beastmaster). No misclassification found.

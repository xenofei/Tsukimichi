# Quest patches (P8)

Written by `Tsukimichi.Verify patches`; do not edit by hand. The patch each quest was added in, as shipped in `Tsukimichi/Data/quest_patches.json`, seeded from Garland Tools' patch data (facts only: ids and patch numbers) and cross-checked against the game data. After a game patch, `tools/regen.ps1 -Patch <x.y>` stamps new quest ids offline; this report is only rewritten when the seed is re-run.

| Field | Value |
|---|---|
| Game version | `2026.09.15.0000.0000` |
| Run | 2026-09-30, UA `Tsukimichi.Verify/0.1.0 (+https://github.com/xenofei/Tsukimichi)`, 2.0 s per request, offline |
| Garland's current patch | 7.5 |
| Newest patch in the data | 7.56 |
| Coverage, every named quest | 5373 / 5373 (100.0%) |
| Coverage, quests still in the game | 5266 / 5266 (100.0%) |
| From Garland's patch documents | 5365 |
| From Garland's quest documents | 0 (37 quest documents were already cached, 0 fetched this run, 0 still to fetch) |
| Kept from the previous file | 0 |
| Hand corrections (`docs/data/quest-patch-corrections.json`) | 8 |
| Unknown | 0 |
| Cached quest documents that agree with the patch documents | 37 |

## Cross-checks against the game data

The Quest sheet carries no patch or version column (Lumina.Excel 7.5.0's schema has `PatchNumber` on Item only; `QuestRedoChapterUI` names MSQ parts, not patches), so nothing in the game data states a quest's patch. What it can refute:

- **olderThanExpansion**: a quest whose patch is older than its `Quest.Expansion` allows (an Endwalker quest cannot be from 5.x). The reverse is normal: event, Gold Saucer and feature quests added in later patches often keep `Expansion` 0.
- **nameDiffers**: Garland's name for the row id is not the catalog's, which would mean a reused or renamed row.
- **documentsDisagree**: Garland's per-quest document and its patch document give different patches for one quest.
- **listedTwice**: a quest under two patches in Garland's patch documents; the older is kept (first seen).
- **changedFromPrevious**: the committed file had another patch for the quest.
- **corrected**: a hand correction (`docs/data/quest-patch-corrections.json`, with its reason and evidence) replaced Garland's patch. **correctionNameDiffers**: the correction names another quest than the row id's.

| Check | Findings |
|---|---:|
| olderThanExpansion | 0 |
| nameDiffers | 0 |
| documentsDisagree | 0 |
| listedTwice | 0 |
| changedFromPrevious | 8 |
| corrected | 8 |
| correctionNameDiffers | 0 |

4966 quests carry a patch of their own expansion; 407 carry a later expansion's patch while filed under an earlier `Quest.Expansion` (expected for events and features).

## Quests per series

Row ids rise with the patches for most genres, so each series' id range is a rough check on its own; overlaps come from ids the game fills in later.

| Series | Quests | Patches | Lowest row id | Highest row id | Garland document |
|---|---:|---|---:|---:|---|
| 7.5x | 61 | 7.5 (15), 7.51 (9), 7.55 (20), 7.56 (17) | 70979 | 71056 | [7.5](https://www.garlandtools.org/db/#patch/7.5) |
| 7.4x | 35 | 7.4 (21), 7.41 (9), 7.45 (5) | 70765 | 70995 | [7.4](https://www.garlandtools.org/db/#patch/7.4) |
| 7.3x | 69 | 7.3 (19), 7.31 (9), 7.35 (41) | 70767 | 70950 | [7.3](https://www.garlandtools.org/db/#patch/7.3) |
| 7.2x | 69 | 7.2 (16), 7.21 (4), 7.25 (49) | 70789 | 70860 | [7.2](https://www.garlandtools.org/db/#patch/7.2) |
| 7.1x | 63 | 7.1 (53), 7.15 (6), 7.16 (4) | 70503 | 70788 | [7.1](https://www.garlandtools.org/db/#patch/7.1) |
| 7.0x | 369 | 7.0 (363), 7.01 (6) | 70353 | 70723 | [7.0](https://www.garlandtools.org/db/#patch/7.0) |
| 6.5x | 42 | 6.5 (20), 6.51 (7), 6.55 (12), 6.58 (3) | 70215 | 70352 | [6.5](https://www.garlandtools.org/db/#patch/6.5) |
| 6.4x | 43 | 6.4 (39), 6.48 (4) | 70269 | 70322 | [6.4](https://www.garlandtools.org/db/#patch/6.4) |
| 6.3x | 72 | 6.3 (28), 6.35 (44) | 70124 | 70268 | [6.3](https://www.garlandtools.org/db/#patch/6.3) |
| 6.2x | 69 | 6.2 (28), 6.25 (41) | 70080 | 70196 | [6.2](https://www.garlandtools.org/db/#patch/6.2) |
| 6.1x | 67 | 6.1 (25), 6.15 (42) | 70057 | 70126 | [6.1](https://www.garlandtools.org/db/#patch/6.1) |
| 6.0x | 433 | 6.0 (433) | 69531 | 70056 | [6.0](https://www.garlandtools.org/db/#patch/6.0) |
| 5.5x | 53 | 5.5 (32), 5.55 (21) | 69567 | 69637 | [5.5](https://www.garlandtools.org/db/#patch/5.5) |
| 5.4x | 61 | 5.4 (33), 5.41 (28) | 69274 | 69578 | [5.4](https://www.garlandtools.org/db/#patch/5.4) |
| 5.3x | 123 | 5.3 (97), 5.31 (9), 5.35 (17) | 69307 | 69510 | [5.3](https://www.garlandtools.org/db/#patch/5.3) |
| 5.2x | 82 | 5.2 (66), 5.21 (9), 5.25 (7) | 69261 | 69387 | [5.2](https://www.garlandtools.org/db/#patch/5.2) |
| 5.1x | 73 | 5.1 (67), 5.15 (6) | 68712 | 69296 | [5.1](https://www.garlandtools.org/db/#patch/5.1) |
| 5.0x | 460 | 5.0 (452), 5.01 (6), 5.05 (2) | 68746 | 69207 | [5.0](https://www.garlandtools.org/db/#patch/5.0) |
| 4.5x | 45 | 4.5 (33), 4.55 (1), 4.56 (11) | 68149 | 68745 | [4.5](https://www.garlandtools.org/db/#patch/4.5) |
| 4.4x | 26 | 4.4 (22), 4.45 (4) | 67245 | 68711 | [4.4](https://www.garlandtools.org/db/#patch/4.4) |
| 4.3x | 65 | 4.3 (57), 4.35 (5), 4.36 (3) | 67090 | 68677 | [4.3](https://www.garlandtools.org/db/#patch/4.3) |
| 4.2x | 62 | 4.2 (59), 4.25 (3) | 68551 | 68624 | [4.2](https://www.garlandtools.org/db/#patch/4.2) |
| 4.1x | 56 | 4.1 (53), 4.15 (3) | 67961 | 68555 | [4.1](https://www.garlandtools.org/db/#patch/4.1) |
| 4.0x | 542 | 4.0 (530), 4.01 (6), 4.05 (6) | 67089 | 68497 | [4.0](https://www.garlandtools.org/db/#patch/4.0) |
| 3.5x | 45 | 3.5 (36), 3.55 (5), 3.56 (4) | 67016 | 67960 | [3.5](https://www.garlandtools.org/db/#patch/3.5) |
| 3.4x | 41 | 3.4 (31), 3.45 (10) | 67084 | 67929 | [3.4](https://www.garlandtools.org/db/#patch/3.4) |
| 3.3x | 69 | 3.3 (58), 3.35 (11) | 67011 | 67870 | [3.3](https://www.garlandtools.org/db/#patch/3.3) |
| 3.2x | 64 | 3.2 (60), 3.25 (4) | 65781 | 67823 | [3.2](https://www.garlandtools.org/db/#patch/3.2) |
| 3.1x | 97 | 3.1 (86), 3.15 (11) | 65571 | 67761 | [3.1](https://www.garlandtools.org/db/#patch/3.1) |
| 3.0x | 557 | 3.0 (542), 3.01 (5), 3.05 (2), 3.07 (8) | 65581 | 67687 | [3.0](https://www.garlandtools.org/db/#patch/3.0) |
| 2.5x | 65 | 2.5 (28), 2.51 (15), 2.55 (22) | 65776 | 67111 | [2.5](https://www.garlandtools.org/db/#patch/2.5) |
| 2.4x | 78 | 2.4 (54), 2.45 (24) | 65579 | 67115 | [2.4](https://www.garlandtools.org/db/#patch/2.4) |
| 2.3x | 118 | 2.3 (58), 2.35 (52), 2.38 (8) | 65569 | 67109 | [2.3](https://www.garlandtools.org/db/#patch/2.3) |
| 2.2x | 128 | 2.2 (122), 2.25 (1), 2.28 (5) | 65698 | 67000 | [2.2](https://www.garlandtools.org/db/#patch/2.2) |
| 2.1x | 148 | 2.1 (147), 2.15 (1) | 66640 | 66847 | [2.1](https://www.garlandtools.org/db/#patch/2.1) |
| 2.0x | 923 | 2.0 (923) | 65537 | 66698 | [2.0](https://www.garlandtools.org/db/#patch/2.0) |

## changedFromPrevious (8)

| Row id | Quest | Detail |
|---:|---|---|
| 65557 | Way of the Archer | previous file 3.1, now 2.0 (hand correction) |
| 65558 | Way of the Conjurer | previous file 3.1, now 2.0 (hand correction) |
| 65559 | Way of the Lancer | previous file 3.1, now 2.0 (hand correction) |
| 65789 | Way of the Gladiator | previous file 3.1, now 2.0 (hand correction) |
| 65846 | Way of the Marauder | previous file 3.1, now 2.0 (hand correction) |
| 65880 | Way of the Thaumaturge | previous file 3.1, now 2.0 (hand correction) |
| 66068 | Way of the Pugilist | previous file 3.1, now 2.0 (hand correction) |
| 66091 | Burning Up the Quarter Malm | previous file 3.1, now 2.0 (hand correction) |

## corrected (8)

| Row id | Quest | Detail |
|---:|---|---|
| 65557 | Way of the Archer | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 65558 | Way of the Conjurer | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 65559 | Way of the Lancer | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 65789 | Way of the Gladiator | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 65846 | Way of the Marauder | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 65880 | Way of the Thaumaturge | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 66068 | Way of the Pugilist | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |
| 66091 | Burning Up the Quarter Malm | 3.1 (Garland patch document) corrected to 2.0: A 2.0 launch quest: its row id sits among the 2.0 rows and the wiki infobox says patch 2.0. Garland's patch documents list it under 3.1, so the "Added in" filter showed it under 3.1 and not under 2.0. |

## Unknown (0)

None.

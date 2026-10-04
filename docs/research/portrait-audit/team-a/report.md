# Portrait audit, Team A (game-data driven)

4 October 2026. Inputs: game 2026.09.15.0000.0000; `main` at 51fdccda (Release DataGen DLLs, no portrait code changed since); `giver_portraits.json` as committed; the 1.20 pack (1,213 images, 1,852 givers). Files here: `findings.json` (402 rows). Contact sheets `contact-01..12.png` and `suggested-crops.png` stay local (Square Enix art). Probe, raw data and scripts: the session scratchpad `portrait-audit/team-a/`.

## Outcome

- **166 characters audited:** 114 recurring characters (8 or more MSQ quests name them, the N10 rule) and 52 side-story characters (the Hildibrand cast, raid and alliance leads, relic and field-operation leads, allied-society story givers). That is **402 character × era rows**.
- 157 of the 166 have at least one problem row. All three owner complaints are real; each comes from code or data, and most fixes need no new art.

### Status counts

Each row is judged three ways: **game art** (the default install), **with pack** (the 1.20 pack installed) and **cast plate** (the "Who's in it" plate, with the pack). The second number counts only rows with at least one MSQ quest (269 rows).

| Status | Game art | With pack | Cast plate |
|---|---|---|---|
| OK | 136 / 97 | 34 / 22 | 29 / 17 |
| off-centre (incl. no face inside the crop) | 55 / 44 | 248 / 168 | 219 / 141 |
| wrong era | 36 / 27 | 44 / 30 | 80 / 63 |
| none | 175 / 101 | 76 / 49 | 74 / 48 |

85 characters have no game art in any era; 45 have none even with the pack (mostly allied-society leaders and 7.x characters: Garland has no 7.x photos).

- **Off-centre:** the eye midpoint is more than 10 % of the plate off the centre line, or outside a 40–48 % eye band (spec A2's 42–46 % plus ±2 % reading error), or there is no face inside the crop.
- **Wrong era:** the art's outfit is not one the character wears in that era, compared on the ENpcBase look fields (ModelHead, ModelBody, NpcEquip, ModelChara, HairStyle, Face): the main outfit of each era for game art, the photographed row for pack photos.

## Top 15 problems (MSQ impact × severity)

1. **Wuk Lamat DT (112 MSQ), all modes.** Trust bust 72661 uses the family box (6, 86, 140); her eyes land at 65 %, 4 %, at the rim. Fix: `"72661": { "eyes": [0.516, 0.191], "chin": 0.296 }` (box 29, 32, 136).
2. **Krile DT (105 MSQ).** The curated crop for 72659 is wrong: its eyes `[0.5, 0.564]` land on her chin; her real eyes are at 37 %, 22 % of the plate. Fix: `{ "eyes": [0.396, 0.495], "chin": 0.598 }` (box 7, 179, 134).
3. **Y'shtola ShB + EW (146 MSQ), with pack.** Pack image 1026571 shows only her staff. Her correct curated Trust bust 72626 loses because the pack ranks above every game family.
4. **Estinien EW / ShB / DT (83 MSQ).** Pack images 1036120 and 1029535 show his lance and a tip of hair; Trust bust 72644 puts the eyes at 45 %, 8 %; ShB game art falls back to his helmeted HW card 87088 (wrong era). See below.
5. **Lyse SB (112 MSQ), with pack.** Photo 1019468 puts the eyes at 70 % and the chin below the plate. Garland box 150, 104, 71, or the same-size box 139, 87, 95.
6. **Alisaie SB–DT (~330 MSQ).** Pack photos: eyes at 60–70 %. Trust bust 72639 (family box): eyes 69 %, 45 %; fix `{ "eyes": [0.546, 0.31], "chin": 0.427 }`. Bust 72622: eyes 55 %, 52 %. The cast plate shows her ARR photo in every era.
7. **Cast plates are wrong-era for every Scion (63 MSQ rows).** `CastText.PlateFor` takes the lowest giver row id (`NpcIds` is a SortedSet) and uses that row's photo for every era. The pack photo's era is stamped with the quest era, so the shield never hides it. Example: the ARR Alphinaud photo on his DT quests.
8. **Alphinaud ARR (53 MSQ), game art: none.** The only ARR art, twin card 87059, is blocked, and later faces are dropped. With the pack, HW–EW photos put his eyes at about 55–62 %.
9. **Varshahn EW (40 MSQ), game art.** Trust bust 72650 (family box): eyes 63 %, 8 %, cut by the rim. Fix: `{ "eyes": [0.501, 0.202], "chin": 0.296 }` (box 34, 44, 121).
10. **Zero EW (32 MSQ).** Pack 1042980 shows the scythe. Trust bust 72651: eyes 62 %, 13 %; fix `{ "eyes": [0.494, 0.217], "chin": 0.331 }`.
11. **Minfilia ShB (44 MSQ).** Pack 1029560: eyes at 90 %. Trust bust 72624: eyes 52 %, 60 %; fix `{ "eyes": [0.419, 0.354], "chin": 0.45 }`.
12. **Later looks in earlier eras (spoiler-grade), cast plates with pack.** Emet-Selch's ShB quests show his Elpis (EW) photo; Lahabrea's ARR quest shows the unmasked EW Keyward; Arenvald, Honoroit, Magnai and Fourchenault in ARR show later looks.
13. **Raubahn HW–EW (50 MSQ), wrong era in all modes.** He wears ARR card 87067 though SB faces exist: battle talk 73031, strip 72790, card 87210 "Raubahn & Pipin" (the index doesn't match this name). His SB Garland photos all failed the head crop.
14. **Yugiri ARR–EW (71 MSQ).** ARR pack photo 1007097 and battle talk 73167 show only her hood, which is her real masked ARR look (an owner call). SB–EW game art shows HW card 87084 while SB battle talk 73081 and strip 72783 exist (family-before-era pick).
15. **Recurring characters with no art anywhere:** Shale DT (25 MSQ), Dulia-Chai ShB (18), Livingway EW (18), Matsya EW (15), Nidhana EW (15), Count Edmont HW (13; card 87093 exists but is unused), Meteion EW (11; card 87337 exists but is unused). The two cards go unused because cast plates only read rows that give a quest.

Next after these: Sphene DT (31): curated 72664 is wrong, eyes at 57 %, 57 %; fix `{ "eyes": [0.566, 0.326], "chin": 0.419 }`. Koana DT (23): 72663, eyes 72 %, 33 %. Crystal Exarch ShB pack: eyes at 82 %. Urianger ShB/EW pack 1026570: face at 70 % across, pushed right by his globe.

## Varshahn and Estinien

**Varshahn EW** (40 MSQ; givers 1039600, 1039645, 1042064, 1043841, 1043858, 1044271, 1045662): game art as in problem 9. Pack images 1038053 and 1039663 put the eyes at 50 %, 49 % and 49 %, 51 % (+4 to +5 px low): mildly low, acceptable; optional same-size Garland box 136, 78, 101. Unnamed battle-talk faces 73078 (high confidence) and 73070 (medium) show him.

**Estinien:**

| Era | MSQ | Portrait now | Finding | Fix |
|---|---|---|---|---|
| HW | 37 | Card 87088 | Helmeted, eye slit at 48 %, 45 %. Accurate to the era. | None needed. His HW Garland photos all failed the crop (helmet and lance). |
| SB | 1 | HW card 87088 | Wrong era. | — |
| ShB | 7 | Game art: HW card. Pack: 1029535 (row 1036346). | Game art wrong era; pack shows a lance. | Candidate: unnamed battle talk 73035 (medium confidence). |
| EW | 70 | Game art: bust 72644 (family box). Pack: 1036120. | Bust eyes at 45 %, 8 %. Pack shows a lance and outranks the bust. | `"72644": { "eyes": [0.367, 0.202], "chin": 0.331 }` (box 0, 24, 166; face about 8 % right of centre, clamped at the left edge). Alternatives: battle talk 73007, strip 72704. |
| DT | 6 | 72644 | Same as EW. Row 1058863 has no Garland photo. | Same as EW. |
| ARR | 1 | None | No art available. | — |

## Root causes

**A. The Trust bust family box (6, 86, 140) only fits ShB busts.** Eyes under the default box: Wuk Lamat 72661 (65, 4), Varshahn 72650 (63, 8), Estinien 72644 (45, 8), Zero 72651 (62, 13), Venat 72647 (56, 21), Koana 72663 (72, 33), Alisaie 72639 (69, 45), Minfilia 72624 (52, 60), Alisaie 72622 (55, 52), Ryne 72627 (56, 52). Two curated busts are also mis-measured: Krile 72659 and Sphene 72664.

**B. The pack's head crop frames too low.** Over 167 pack images worn by audited characters: median eye line 62 % (IQR 57–70) against 43 %; median chin 88 % against 83 %. Only 9 of 156 measurable images have the eyes in band; the chin falls below the plate in 38; 11 contain no face; only 4 are more than 10 % off horizontally, so the fault is vertical. The weapon images fail because the crown rule ("first row as wide as half a head") accepts a staff, lance or scythe raised above the head, and `MinFilled` then passes. Rule-framed boxes are small (median side 71 px; 163 of 275 under `MinBox` 72), so a same-size box moved onto the eyes is also given, which avoids upscaling.

**C. The pack ranks above every game family**, replacing good curated game art even when its own image is broken (Y'shtola, Estinien EW).

**D. Cast plates have three faults.** They pick the lowest-id giver row for every era and use that row's pack photo, whose era is set to the quest's, so the shield never hides it. They `break` on the first known row, so a member falls back to initials even when another row has art. Cast members who never give a quest get no game art even when it exists: Count Edmont (card 87093), Meteion (87337), Hakuro (battle talk 73116), Gigi (card 87181), Coultenet (73171 / 87122), Wilred (73198), J'moldva (strip 72791), Frixio (card 87035).

**E. `Pick` ranks family before era.** An older card beats a right-era battle-talk face or strip: Raubahn HW–EW (ARR card while SB faces exist), Tataru EW/DT (SB card 87241 while EW battle talk 73072 exists), Yugiri SB–EW (HW card while SB faces exist), Cid HW–EW (ARR card 87058).

**F. Duty Support art takes the era of its duties, not its look.** Alisaie's bust 72631 and strip 72691 match her HW–ShB rows' outfit; if confirmed visually, re-dating them to HW gives her 15 HW MSQ quests a face. Alphinaud's 72630 and 72690 match his HW rows.

**G. 96 battle-talk faces have no name.** By eye: Varshahn 73078 (high), 73070 (medium); Estinien 73035 (medium); Hien 73032 (medium); Cirina 73023 (medium); Thancred 73013 (low); Venat 73001 (low).

**H. Faceless game art should be blocked**, as Anden, Hermes and Seiryu already are: Nero card 87047 (helmet), Erichthonios card 87335 (armoured form), Yugiri battle talk 73167 (hood; owner call). Two-person cards need a crop per giver: 87062 Hildibrand & Nashu (nothing usable for Nashu) and 87210 Raubahn & Pipin.

## Early-arc gaps (no game art; later eras have art)

| Character | Era | MSQ | Fix options |
|---|---|---|---|
| Alphinaud | ARR | 53 | Only the blocked card 87059; needs a per-giver crop. |
| Alisaie | HW | 15 | Re-date 72631 / 72691. |
| Krile | HW | 11 | Pack 1012813 / 1016760. |
| Aymeric | ARR | 7 | Pack only. |
| Emet-Selch | ShB | 5 | Cast-plate code fix. |
| Jullus | EW | 17 | His art is DT only. |
| Livingway | EW | 18 | Battle talk 73289 is DT. |
| Lyse, Pipin, Gosetsu, Lucia, Lahabrea, F'lhaminn | — | small | — |

## Further suggested `iconCrops`

| Icon | Character | Eyes now (x %, y %) | Suggested |
|---|---|---|---|
| 72627 | Ryne | 56, 52 | `{ "eyes": [0.449, 0.331], "chin": 0.447 }` |
| 72628 | Lyna | 41, 35 | `{ "eyes": [0.337, 0.281], "chin": 0.377 }` |
| 72647 | Venat | 56, 21 | `{ "eyes": [0.449, 0.24], "chin": 0.34 }` |
| 72663 | Koana | 72, 33 | `{ "eyes": [0.568, 0.275], "chin": 0.398 }` |
| 72622 | Alisaie | 55, 52 | `{ "eyes": [0.441, 0.331], "chin": 0.436 }` |
| 87166 | Y'shtola (HW card) | 60, 58 | `{ "eyes": [0.514, 0.433], "chin": 0.592 }` |

The uncurated battle-talk faces of Conrad, Wedge, Haurchefant, Ilberd, Hoary Boulder, Isse, Drillemont, Papashan, Gundobald, Erenville and Livingway are all off by +12 to +17 % across, or −10 to −22 % vertically. The full table (33 icons) and the 34 broken or low pack images with Garland boxes are in `findings.json` (`suggestedCrop` / `suggestedCropWithPack`).

## Method

- **Game data** through the repo's own code (Lumina): `GiverPortraitSources.Read` and `PortraitIndex.Build` with the curated overlay and `WithPack`; `QuestCastReader.Read` for the cast; ENpcResident and ENpcBase rows for each character's rows by era and the outfit match; `DawnQuestMember`, `QuestBattle`, `ContentDirectorBattleTalk` and `AkatsukiNote` for unnamed faces. MSQ means `JournalSection` 0 or 1.
- **Pack:** the manifest, the 128 px images, the Garland JSON and photo cache, and the pack report's "no face found" list. `PortraitHeadCrop` was ported to Python; its boxes match the pack (alpha difference 3–6/255).
- **Measuring:** all 264 distinct portraits rendered as drawn with a 5 % grid; eye midpoint and chin read by eye. Seven doubtful readings got a second, larger reading (Krile, Sphene, Momodi, Minfilia, Alphinaud 73034 and Lyse were corrected). Pack readings spot-checked with markers.
- **Contact sheets:** every character × era shows P (pack), G (game art) and C (cast plate) with coloured status rings, the 44 % eye line, centre ticks, a cyan dot at the measured eye midpoint, and a red X where there is no face.

## Confidence

- **High:** what the code picks, the systemic causes A–E, the broken pack images and the EW/DT bust offsets (errors of 20–40 %).
- **Medium:** each eye reading (±3 % of the plate; three-quarter views are harder); rows near the 40/48 % band edges; the outfit-based "wrong era" test (cutscene costumes can mislead it, and Raubahn's face is unchanged where his outfit flags); the F re-datings.
- **Low to medium:** naming unnamed battle-talk faces by eye.
- **Not verified:** in-game drawing; the cast-plate "game art only" mode; Garland coverage beyond the cached pack builds.

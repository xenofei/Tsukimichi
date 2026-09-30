# Tagging audit (condensed by the coordinator from the agent's report)

**Method:**
- A scratch program (scratchpad\tagging\src\Audit, with copies of Core and GameData at HEAD 880a468) built the real refiled catalog and resolved every quest against Michiru's snapshot.
- Compared against 6,239 cached wiki infoboxes (covering 5,289 of 5,373 quests) and raw sheets.
- Outputs in tagging\out\ (duty_diff.txt, finished_sections.txt, quests.json, treecounts.json, chains.json, dutyindex.json, scriptduties.json, sheet_*.jsonl).
- Start-city / class / GC alternatives and the aether-current mapping were excluded (covered elsewhere).

## Findings (most misleading first)

1. **Allied-society dailies read "Done this cycle" forever (39/39 wrong; a rule bug).**
   - StateResolver rule 5 treats the completion bit on RepeatInterval != 0 as "done this cycle"; "done today" lives only in DailyDone. The snapshot has dailyDone={} and tribeAllowance=12, yet 8 Vanu dailies show done.
   - Rows: Sylph 66797-9; Kobold 66865, 66866, 66868; Sahagin 66915, 66916, 66919, 66923, 66924; Vanu 67709-16; Vath 67797-801; Kojin 68515-19; Ananta 68579-85; Pixie 69225, 69226, 69231.
   - 566 society dailies sit in the Allied Society totals and can never count as done.
   - Fix: rule 5 uses DailyDone only. For done/total, the completion bit counts as "done once", or dailies leave the totals.
2. **Hidden progress-tracker rows (genre 0, level 1, unlisted by the Lodestone and the wiki) are refiled into real genres and counted (14).**

   | Group | Rows |
   |---|---|
   | YoRHa | 69580-69585 |
   | Resistance | 69478, 69630 |
   | Ishgardian Restoration | 69296, 69377, 69508, 69578 |
   | Likely also | 67870 Recondition the Anima, 69577 Forged Anew |

   - 8 of them are also tagged blue in My blues.
   - Fix: a refiler rule (genre 0, level 1, prerequisite at level 50 or higher → a tracker: CountsInTotals=false, no feature tag, not a chain step). Gate: no counted quest is notListed in verification-manifest.json.
3. **Repeatables count in genre totals and chain progress.** 92 non-daily repeatables are counted, so a finished chronicle can't reach 100 %:
   - Primal Focus 66845 (Primals 14/15);
   - UFO 67016 (Mhach 6/7);
   - the YoRHa weeklies 69586-8 and the relic repeatables.

   The chain "next" points at them. Fix: leave IsRepeatable out of TreeCounts, NodeCount and ChainCatalog.Progress, or count them done once the bit is set.
4. **Duty unlocks are largely missing.**
   - Of 308 wiki pairs, the index has 226 entries; 201 pairs are missing, and 177 duties have no unlock quest, so the P13 Duty Finder hint is silent. Missing:
     - about 70 MSQ duties;
     - about 70 Chronicles duties (Coil turns 2-4, Alexander, Omega, Eden, Pandæmonium, Arcadion, Myths, YoRHa, Ivalice, extra trials);
     - many sidequest duties;
     - the "A Relic Reborn" Chimera/Hydra quests.

     The list is in out\duty_diff.txt.
   - The script rule "primary INSTANCEDUNGEON and (UNLOCK_ADD_NEW_CONTENT_TO_CF or UNLOCK_IMAGE)" agrees with the wiki on 108 of 111 pairs and recovers 30. Its false positives are Castrum Meridianum 66672 (a known owner gate) and 66988 (retired).
   - Fix: derive from the rule, and seed the rest into curated/duty_unlocks.json from the wiki pairs, with evidence.
   - Also: 67205 "Heavensward" is credited with 7 Diadem entries rather than the Aetherochemical Research Facility / Singularity Reactor. 111 of the 226 entries point at non-Duty Finder content (deep-dungeon floors, ocean fishing, Eureka).
5. **Plan (My blues) unlock kinds are wrong (81 missing).** Tagged "Other" when they should be:
   - raids or trials (35 raids, 18 trials): Ivalice 68540/68628, Eden, Arcadion, Myths, Ruby/Emerald/Diamond;
   - dungeons (12): 65966, 65967, 68169, 68170, 68613, 68678, 69131, 69703, 69704, 70549, 70550, 68552.

   Also:
   - "A Relic Reborn" should be Job plus Trial.
   - 59 system/feature unlocks (Cactpot, housing, Challenge Log, transmutation, Fashion Report, Duty Recorder, hunts, sightseeing, scrip vendors, Cosmic Exploration, Variant, …) are tagged "Other" rather than "System". Option: EventIconType 10 defaults to System.
6. **Moonlit achievements are misattributed (475 of 884 misleading).**
   - 454 type-24 "obtain weapon X" achievements are attached to relic step quests (up to 21 each), and their obtained check reads the quest's completion bit.
   - 21 "complete all N" titles are credited to each quest.
   - 150 "Other: Aether Current" entries duplicate the AetherCurrent entries.
   - Fix: DataGen ScanReverseLinks skips type-24 and multi-quest type-6 achievements and de-duplicates the aether-current entries; Title/Achievement obtained reads achievement state.
7. **The Job column shows "Multi" where it should say "Any" (216 rows).** Category 130 admits 42 jobs, while ComputeJobShort requires ≥ JobColumns-1 = 45. Fix: compare against the named non-limited ClassJob rows. Open question: the Ixal dailies' ClassJobCategory1 (MIN/BTN/FSH) isn't read.
8. **Refiling:** besides the trackers, 67923 What Lies Beneath (Palace of the Dead 51-200) is filed under Gridanian Sidequests and should be genre 103. Fix: an override, or relax the expansion check for feature-genre prerequisites.
9. **Chains (12 of 139 flagged):**
   - 4 are out of order, because refiled rows keep the raw SortKey (Recondition the Anima, Forged Anew, A Message from Konogg, Weapon of Choice).
   - Foreclosed quests stay in chain totals (Tails, You Lose; the Valentione's/Little Ladies' chain).
   - Fix: order by the prerequisite graph; leave foreclosed and repeatable rows out of Progress.
10. **Patch of origin (low severity):** 5,074 exact, 137 in the same series. The 62 major-version differences are MSQ rows re-added in 5.3/6.1 (correct per row), plus about 16 small Garland disagreements.
11. **Retired / unobtainable:** 0 wrong. All 63 wiki {{Retired}} quests are retired, and there are no placeholder rows (160 unnamed rows are skipped).
12. **Blue icon tagging:** 4 disagree with the wiki (67114, 70944, 68741, 69288), plus the trackers. The code comment is wrong: EventIconType 3 is MSQ, and 1 is an ordinary sidequest.
13. **System unlocks:** 0 wrong of 65.
14. **Festival counting:** correct.

## Tests to add

1. Snapshot fixture with DailyDone empty: nothing reads "done this cycle"; every finished genre counts x/x (repeatables excluded).
2. Manifest gate: no counted, feature or chain quest is notListed in both the Lodestone and the wiki.
3. Duty coverage: every dungeon, trial or raid Duty Finder entry has an unlock quest (with an allowlist); script-rule pairs are a subset of the index.
4. Plan kind: a raid, trial or dungeon unlock is never only "Other".
5. Moonlit: no type-24 achievement entries and no all-of-N achievements per quest.
6. Job label: category 130 reads "Any".
7. Chain order follows prerequisites; Progress ignores repeatable and foreclosed rows.
8. Refile golden: 67923 is filed under genre 103.

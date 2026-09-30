# Alternative paths: findings (saved by the coordinator from the agent's report)

Reproduced exactly from Michiru's snapshot (characters\18014498557857397.json: Gridania start, Lancer start, Twin Adder, max expansion EW):
- All quests 1,672/4,944.
- MSQ ARR–EW 821/905.
- Seventh Umbral Era 160/213.

The 53 missing are all other-path quests. Scratch program: scratchpad\paths (Program.cs + Alt.cs, and outputs alt.txt, percity.txt, locks.txt, anyjoin.txt, msq.txt, msqopen.txt).

## Groups

1. **Start city.** There's no sheet column for it; it's encoded in the prerequisite graph.
   - Roots: 65575 Coming to Gridania, 65643 Coming to Limsa Lominsa, 66130 Coming to Ul'dah.
   - Rule that finds them: no PreviousQuest, and a successor includes a level-1 MSQ quest.
   - The lines rejoin via Any joins: 66210 Call of the Sea ← ANY(66043, 66082); 66209 ← 66064; 65781 It's Probably Pirates ← ANY(66209, 66210).
   - Quests only one city can take:

     | City | Seventh Umbral Era | Sidequests |
     |---|---|---|
     | Gridania | 24 | 11 |
     | Limsa Lominsa | 23 | 20 |
     | Ul'dah | 26 | 19 |

   - For a Gridania starter this adds +49 to Seventh Umbral Era and +39 to Sidequests.
   - 65643 and 66130 currently read Ready for her.
2. **Start class.** There is one "Close to Home" per ARR starting class:

   | City | Rows | Classes |
   |---|---|---|
   | Gridania | 65621 / 65659 / 65660 | LNC / ARC / CNJ |
   | Limsa Lominsa | 65644 / 65645 | MRD / ACN |
   | Ul'dah | 66104 / 66105 / 66106 | GLA / PGL / THM |

   - Only QuestParams differ: the guild actor equals the IssuerStart of that class's "Way of" quest.
   - Sibling sets are merged by Any joins: 65564 ← ANY(65621, 65659, 65660); 65998 ← ANY(65644, 65645); 66131 ← ANY(66104–66106).
   - Class & Job: each ARR class has a starter track (a "Way of X" with no prerequisite) and a switcher track (intro → "Way of X" → "My First X"). They merge by an Any join, e.g. 65792 ← ANY(65789, 65822). That adds +17 for her; 5 rows falsely read "Ready on another job".
3. **Grand Company.**
   - MSQ: 66216/7/8 The Company You Keep (QuestLock each other). Their follow-ups 66219/66220/66221 stay Blocked and count (+2).
   - GC-tagged trios: 66236–8, 66640–2, 67063–5, 67099–101, 67925–7. The 10 other-company versions read "Blocked · Grand Company". Cause: RequirementEvaluator.IsSwitchableGrandCompanyQuest skips the lock (this is a bug).
   - Untagged: 67001–3 Call of the Wild (GC 0, officer issuers). All read Ready.
4. **Others** (via QuestLock unless noted):
   - glamour routes 66957→66958 vs 68553→68554 (+1);
   - Zodiac 66097/67823 The Vital Title (+1);
   - Qitari stelae 69336/7, 69338/9, 69340/1 (+3);
   - retainers (already excluded), Island Sanctuary 70179/80 and YoRHa 69256/7 (both fine);
   - seasonal trios (+2 per trio while the event runs).
   - Not alternatives: Hildibrand, Anima/Resistance/Manderville, Gold Saucer.

## "Main scenario quest (Lv 1)"

- These are 66104–66106, the Ul'dah Close to Home rows, masked by SpoilerMask.
- MsqGraph.Position picks 65659 Close to Home (Archer, false Ready). So the status bar, dashboard, Tonight card, Todo overlay and IPC GetMsqPosition all say "Close to Home" (821/1,048), and 224 names are masked, including 30 real Newfound Adventure quests.
- Fixed, the position becomes 70134 Return from the Void (821/995), with 170 names masked.

## Today's model

- Foreclosed (Locked out) is set only for retired quests, a completed QuestLock (with the GC exemption), and past festivals. It doesn't propagate.
- Start city and start class aren't modelled anywhere.
- TreeCounts counts done = Completed; the total drops only LeavesTotals (Foreclosed / out of season).

## Corrected numbers for Michiru

| Node | Today | Corrected |
|---|---|---|
| All quests | 1,672 / 4,944 | 1,672 / 4,824 (4,818 if undecided groups count once) |
| MSQ ARR–EW | 821 / 905 | 821 / 852 |
| Seventh Umbral Era | 160 / 213 | **160 / 160** |
| Sidequests | 495 / 2,063 | 495 / 2,021 |
| Class & Job | 188 / 882 | 188 / 865 |
| Other | 23 / 37 | 23 / 29 |

Seventh Umbral Era after the GC choice is 160 (Gridania), 160 (Limsa) and 161 (Ul'dah); before it, 164/164/165.

## Recommended model

- "Other path" is a reason for Foreclosed, not a new state: RequirementKind.OtherPath + OtherPathRequirement(kind, group, option, chosen, evidence), and QuestEvaluation.IsOtherPath. LeavesTotals then fixes totals, MSQ position, spoiler mask, dashboard and Compare.
- **PathIndex** (per catalog, cached like MsqGraph):
  - choice groups: city roots (rule plus a 3-entry curated pin), sibling sets (same name + same prerequisites + one Any join), class tracks, QuestLock components, GC column plus the MSQ trio;
  - forward-propagate option sets in topological order: an All join intersects, an Any join unions.
  - Curated file path_choices.json holds: city labels, the 8-row class label map, and the Call of the Wild GC tags.
- **Chosen branch per snapshot:**
  - city: the completed or in-journal root;
  - class: the completed Close to Home sibling, else the starter "Way of", else JobLevels;
  - GC: the completed Company You Keep, else snapshot.GrandCompany;
  - lock groups: the completed member.
  - Never exclude a completed quest. If two options of a group are completed, treat that group as not exclusive.
  - Resolve fully when an anchor quest's completion changes. Retire IsSwitchableGrandCompanyQuest.
- **Undecided groups:** not Locked out. Count one option (the presumed one, else the first); mark the rest IsSpareAlternative (honoured by LeavesTotals); tag them "Choose one of 3".
- **UI:**
  - excluded from done/total, with an OtherPaths count shown in the tooltip;
  - a virtual "Other paths" tree node grouped by reason, with FilterSet.IncludeOtherPaths off by default;
  - rows read "Locked out · Another city's start (Ul'dah)" with the real name, never masked;
  - the detail line reads "Only for Ul'dah Gladiator starters · you started in Gridania as a Lancer";
  - PathFinder skips other-path branches.
- **IPC** is additive only: GetState returns Foreclosed, IsQuestAvailable(65659) returns false, GetMsqPosition returns 70134, GetBlockers gains an OtherPath line.

## Unverified in game

- Starting-class "Way of" quests for classes you didn't start as can't be taken.
- Call of the Wild is offered only by your own company's officer.
- My Little Chocobo (Maelstrom) stays locked after switching company.

# Flight counts: findings (saved by the coordinator from the agent's report)

## Root cause

- `Tsukimichi.GameData/FlightIndex.cs` `Build()` takes each aether current's quest from the `AetherCurrent.Quest` column.
- For 5 of 150 quest currents, that column names the wrong quest:
  - four Heavensward currents name the follow-up of the quest that awards them;
  - Thavnair names an unrelated quest in the same zone.
- The values have been the same in every datamining snapshot since 2017.
- `Tsukimichi/Ui/FlightPane.cs` `RefreshCounts` (L569-619, 668) counts done from snapshot quest completion, even for the live character. Only the moons, header and Attuned column use `PlayerState->IsAetherCurrentUnlocked`.

| Zone | Current | Counted (wrong) | Real awarding quest |
|---|---|---|---|
| The Churning Mists | 2818096 | 67365 The Unceasing Gardener | 67364 Hide Your Moogles |
| The Dravanian Forelands | 2818065 | 67328 Natural Repellent | 67326 Stolen Munitions |
| The Dravanian Forelands | 2818066 | 67334 Chocobo's Last Stand | 67333 The Hunter Becomes the Kweh |
| The Sea of Clouds | 2818110 | 67437 Search and Rescue | 67410 Honoring the Past |
| Thavnair | 2818328 | 70030 Curing What Ails | 69793 In Agama's Footsteps |

Evidence:
- `Quest.OtherReward` = 2 ("Aether Current") marks exactly 150 quests: the 145 correct listed quests plus the 5 real quests above.
- The Console Games Wiki Aether Currents page and the individual quest pages agree.
- Garland Tools agrees (quest docs 67364/67365/67410/67437/69793/70030).

## ARR

- Set 19 (quest 70058 The Ultimate Weapon) covers all 17 ARR field territories: 134, 135, 137-141, 145-148, 152-156, 180.
- FlightIndex keys only by the set's own territory, so the pane shows "Mor Dhona 1/1", and `ZoneFor` is null in the other 16 zones.
- An existing test wrongly asserts that 134 has no flying.

## Dawntrail

Complete: 6 zones, each with 5 quest currents and 10 field currents.

## Fix

1. `FlightIndex.Build`: resolve each current's quest in this order:
   - the listed quest, if it has OtherReward = 2;
   - else the single PreviousQuest entry that has it;
   - else the zone's single unclaimed flagged quest, or a curated override `{2818328: 69793}`.

   Keep the listed quest id for diagnostics. Map territories through `TerritoryType.AetherCurrentCompFlgSet`, so ARR resolves as one entry, "A Realm Reborn (all zones)".
2. `FlightPane.RefreshCounts`: done = the live flag when it can be read, else snapshot quest completion. Move this into a testable helper, and update the tooltip strings.
3. `Tsukimichi.DataGen/UniqueRewardGenerator.cs:184-195, 516-522` makes the same assumption. Use the shared resolver, then regenerate `unique_quests.json` and `docs/data/unique-report.md`. This affects `FeaturePresets.AetherCurrentQuests` / `UnlocksOnlyAetherCurrents` (Core/Query/FeaturePresets.cs:103-127, 154-182).

## Tests

- Every counted quest has OtherReward = 2, matched one to one (150), with the five ids asserted.
- Fix the `ZoneFor(134)` assertion.
- Unit-test the counting rule: flag set ⇒ done; no flag and quest done ⇒ done; flag unset and quest done ⇒ not done.

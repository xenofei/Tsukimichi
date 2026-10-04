## 0. Findings of the 2026-09-27 verification pass (hand-written)

This section is kept in `docs/data/verification-notes.md` and inserted by `Tsukimichi.DataGen --verify`; everything
below it is regenerated on every run. Update the notes when the generator rules change.

### Generator bugs fixed

| Bug | Effect on the shipped file | Fix |
|---|---|---|
| Orchestrion rolls read the roll id from `ItemAction.Data[0]`, which is always 0 for rolls; the Orchestrion row is linked from `Item.AdditionalData`. | All 62 Orchestrion entries shipped `rewardId 0` (runtime `IsOrchestrionRollUnlocked(0)` and a name that was the item name). Because every roll of a multi-roll quest collided on the same key, 17 further rolls were silently dropped. | `rewardId` = `Item.AdditionalData` (typed `Orchestrion`), name = `Orchestrion.Name`; 79 entries now. Verifier checks the link offline and against xivapi. |
| `ItemRewardType 7` was handled as a second QuestClassJobReward case; Lumina resolves `Reward[0]` for type 7 to `BeastRankBonus` (30 tribal quests). | Latent: the lookup found no QuestClassJobReward row, so no entry was produced (and none was wrong). | Type 7 now reads `BeastRankBonus.Item` and classifies it like any reward item. |
| A `ContentFinderCondition` without a name (row 121, reached from InstanceContent 40001 by the three "A Pup No Longer" quests) became a `DutyUnlock` entry. | 3 entries with an empty `rewardName`. | Unnamed conditions are skipped (forward and reverse links). |
| An unnamed Item row (33138) rewarded by three Bozja quests became an `Item` entry. | 3 entries with an empty `rewardName`. | Unnamed items are dropped and listed in `unique-report.md`. |

### Exclusivity rule (new, default on; `--keep-nonexclusive-items` restores the old behaviour)

A plain untradable, non-marketable reward item is shipped as `Item`/`OptionalItem` only when it is not in a
consumable-like `ItemUICategory` (Medicine, Meal, Ingredient, Reagent, Dye, Crystal, Catalyst, Currency, Other,
Miscellany, Seasonal Miscellany, Materia, Demimateria, Part, Lumber, Stone, Metal, Cloth, Leather, Bone, Gardening),
is not sold by a special shop, crafted by a recipe or gathered, and is not sold by a gil shop menu other than the
Calamity Salvager's quest-reward reacquisition menus ("Purchase Quest Rewards ...", "... Arms & Gear", "... Arms & Tools",
which only re-sell what the character already earned). Since 1.19.0 (C6) a gil shop row that wants the rewarding quest
done first counts the same way: the recompense officer's seasonal menus ("Purchase Heavensturn Items", "Purchase All
Saints' Wake Items" and the like) sell a festival's reward back only to those who finished it, so those 115 items ship
again and the "sold by a real vendor menu" count below lists them. Items whose ItemAction is an unlock (portrait
framer's kits 29459, Bozja field notes 19743) are kept whatever their category, as is facewear (37312). Collectible
kinds (Mount, Minion, Emote, Orchestrion, Triple Triad card, Ornament, Barding, Hairstyle) are untouched.

### Before / after (the 2026-09-27 regeneration that introduced the rule)

| | Shipped before (2026-09-27 20:07) | Fixed generator, legacy item rule | Fixed generator, strict rule (shipped now) |
|---|---:|---:|---:|
| Entries | 4160 | 4171 | 3464 |
| Item | 739 | 736 | 165 |
| OptionalItem | 189 | 189 | 53 |
| Orchestrion | 62 (all `rewardId 0`) | 79 | 79 |
| DutyUnlock | 177 (3 unnamed) | 174 | 174 |
| Entries with `;otherSource=` | 1114 (444 Item/OptionalItem) | 1114 (444) | 806 (136; every one of them a Calamity Salvager reacquisition menu) |
| Empty reward names | 6 | 0 | 0 |

Items refused by the strict rule: 713 (Miscellany 337, other gil shop menu 130, special shop 109, Seasonal Miscellany 58,
Other 37, Medicine 24, Currency 10, Reagent 3, unnamed 3, Catalyst 1, Stone 1). Examples that are gone: Fantasia
(70058), Hi-Cordial (19 quests), Aetheryte Tickets, MGP vouchers and cards, Faire Vouchers, Gold Saucer Tickets,
Rowena's and Jandelaine's Tokens, every gear and accessory coffer, chocobo feed, Bozjan coins and seasonal fireworks.
The 130 "other gil shop menu" refusals included seasonal-event gear that the recompense officer sells back only after
the festival quest; 1.19.0 (C6) restores those 115. The full list is in `unique-report.md`.

### Observations not fixed here (outside `Tsukimichi.DataGen` / `Tsukimichi/Data`)

- `DutyUnlock` entries never get a reward icon in the Moonlit pane: the catalog emits `RewardKind.Instance` keyed by
  InstanceContent id while the data file keys `DutyUnlock` by ContentFinderCondition id, so `MoonlitPane.FindIcon`
  never matches. Likewise ClassJob (icon 0 in the catalog), AetherCurrent, BlueMageSpell, Trait, Achievement, Title,
  SystemUnlock and reverse-linked Actions have no icon source in the catalog.
- Mount, Minion, Ornament and ClassJob names are the sheet's lowercase `Singular`/`Name` ("magitek armor",
  "wind-up gentleman", "paladin"); the UI shows them as-is.

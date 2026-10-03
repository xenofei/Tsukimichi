# Quest unlocks: feature spec

Status: **research, 2026-10-02.** Nothing is built and no product code was changed. Written for plan v6.

## The owner's request

> "I want deeper context when it comes to what the user unlocks with quests, and it displaying in the quest pane in the journal (and possibly other areas in the plugin). Such as territories you can travel to / map unlock. Dungeons you unlock. Aetheryte unlocks. And anything else."

The reference was the Console Games Wiki MSQ table and its "Unlocks" column (area, dungeon, aetheryte and next-quest icons).

## In short

- **Tsukimichi already knows about 60% of this.** It has duties (464 links over 318 quests), features and systems (133 curated), jobs, flying, actions, traits, emotes and collectables in `unique_quests.json` and the curated files. It also knows next quests (`ReversePrereqIndex`, already shown as "UNLOCKS NEXT" in the Path card).
  - The problem is how it is shown. The data sits in four places: Rewards tiles, Path, the AutoDuty "Duties" section, and the Clear my blues (plan) pills. Only feature quests get `UnlockTags`.
- **Areas, aetherytes and world-map regions are new.** The game has no "zone unlocked" field. Four signals together cover it well:
  1. **WarpCondition.** Ferries, gates and NPC passage gated by a quest: 45 quests, 102 warps. Example: Not without Incident → Kugane.
  2. **MapCondition.** World-map regions revealed by a quest: 14 quests, 37 maps and place names. Example: The Next Ship to Sail → The Source, The Northern Empty, Ilsabard.
  3. **Aetheryte.RequiredQuest.** 13 aethernet or invisible gate rows. Example: Once More, to the Ruby Sea → The Ruby Price.
  4. **A first-visit rule over the main scenario graph** (new; verified). A quest opens an area when its objectives reach that town or field zone and none of its main-scenario ancestors did. It opens an aetheryte when one of its objectives lies within 40 yalms of it. This covers **65 of 67** town and field zones and **87 of 107** aetherytes. It gives the wiki's own answers: Kugane, The Ruby Sea, Onokoro, Old Sharlayan, Tuliyollal, Mare Lamentorum.
- **The proposal:**
  - one `QuestUnlocks` index in Core, built once per catalog;
  - an "Unlocks" section in the detail pane, with icon rows grouped by kind;
  - the same data in search, filters, tooltips, game panels, the Todo overlay, the route window and My blues P4.

  Six plan items (K1–K6). The core is **M+M** (K1, K2), and everything together is about **L**.

---

## 1. What Tsukimichi already knows

| Where | What it holds | Shown today |
|---|---|---|
| `Tsukimichi/Data/unique_quests.json` (DataGen) | 2 629 entries over 1 301 quests. **Unlock kinds:** DutyUnlock 464 (318 quests), SystemUnlock 133, ClassJob 48, AetherCurrent 151, Action 250, Trait 54, Emote 56, GeneralAction 12, BlueMageSpell 16. **Collectables:** Mount 38, Minion 64, Orchestrion 79, TripleTriadCard 6, Hairstyle 1, Barding 6, Ornament 4, and more. **Sources:** `Quest.InstanceContentUnlock`, `ContentFinderCondition.UnlockCriteria`, `QuestScript` (UNLOCK_ADD_NEW_CONTENT_TO_CF), `ClassJob.UnlockQuest`, `Trait.Quest`, `Action.UnlockLink`, `AetherCurrent.Quest`, `Quest.OtherReward`, plus curated. | **Moonlit**, and **Rewards tiles** for the kinds `QuestRecord.Rewards` carries |
| `Tsukimichi/Data/curated/duty_unlocks.json` | 308 quest → CFC links, each with evidence (wiki and quest script) | Same as above, plus the Duty Finder hint (`DutyUnlockIndex`) |
| `Tsukimichi/Data/curated/system_unlocks.json` | 133 features: Retainers, Gold Saucer, Guildhests, Hunts per expansion and bill tier, Custom deliveries ×10, Materia ×3, Sightseeing Logs, Scrip exchanges, Relics, Island Sanctuary, Cosmic Exploration, Ocean fishing, Triple Triad, and more | Same as above |
| `QuestRecord.Rewards` (`GameData/CatalogMapper.MapRewards`) | Items, optional items, currency, emote, action, general action, instance (`InstanceContentUnlock`), class/job, `OtherReward` | **Rewards** tiles in `DetailPane.DrawRewards` |
| `Core/Plan/UnlockTags.cs`, `UnlockKind.cs`, `PlanDuties.cs` | Per **feature quest** only: kinds Dungeon, Trial, NormalRaid, AllianceRaid, FieldOperation, Job, Society, Flying, System and Other, with names and the genre-inheritance rules | Plan pills, Clear my blues checklist, `QuestVerdict.Unlocks` ("Unlocks Aglaia" in the game panels) |
| `Core/Unique/DutyUnlockIndex.cs` | CFC → quests (reverse) | Duty Finder side panel, "Route to unlock" |
| `ReversePrereqIndex` / `session.Index.Dependents` | Next quests | Path card: "UNLOCKS NEXT · N", at most 8 (`DetailPane.MaxUnlocks`), with "+N" (`PathChart.LoadUnlocks`) |
| `DetailPane.Companions.cs` Duties section | Duties a quest requires or unlocks ("Unlocked by this quest") | Only while AutoDuty is installed |
| `GameData/AetheryteIndex.cs`, `FlightIndex.cs` | Every aetheryte and shard with raw X/Z (from MapMarker), every flying zone | Travel and Flight; nothing about which quest opens them |
| `Game/RewardUnlockReader.cs`, `CollectibleReader.cs` | Owned or unlocked state for duties (`IsInstanceContentUnlocked`), emotes, mounts, minions, cards, currents and more. Aetheryte attunement through `UIState.IsAetheryteUnlocked` in TravelService. | Moonlit, Flight |
| `MoonlitIconResolver` (`Ui/MoonlitPane.cs:2481`) | Icons per reward kind: CFC → `ContentType.Icon`, job 62100+id, current 60033 | Moonlit |

**The gap, in one line:** there is no per-quest list of *everything* it opens. Areas, aetherytes and map regions are not known at all, and the detail pane spreads what is known over four sections.

## 2. What the game data provides

Everything was measured against the live sqpack (game 2026.09.15) with a throwaway Lumina 7.7.1 / Lumina.Excel 7.5.0 probe in the session scratchpad (`scratchpad/unlocks/`, modes `refs`, `probe`, `stats`, `areas2`, `sys`). A reflection pass found **75 sheet columns typed `RowRef<Quest>`**. The useful ones are below.

### 2.1 Unlock sources, ranked

| Group | Source (sheet.column) | Coverage | Reliability | Gaps |
|---|---|---|---|---|
| **Next quests** | `Quest.PreviousQuest` reversed | 3 290 of 5 373 named quests have one or more | Exact | "Any of" (PreviousQuestJoin) and locks are already handled by `PrerequisitesOf` |
| **Duties** | `Quest.InstanceContentUnlock` (48 quests), `ContentFinderCondition.UnlockCriteria/UnlockCriteria2` with UnlockType 1 (35 quests, 109 links), curated `duty_unlocks.json` (308) | 318 quests, 464 links, all already in `unique_quests.json` | High: sheet or script, with evidence | Guildhests open as a system, not per hest. Retired content (the Diadem, CFC 722) is dropped by `DutyUnlockDerivation`. |
| **Areas: travel** | `Warp.WarpCondition.RequiredQuest1..4` → `Warp.TerritoryType` | 45 quests, 102 warps: city ferries and airships, Kugane, Ishgard, the Gold Saucer, housing districts, the Firmament, Empyreum, Radz-at-Han | Exact, but noisy: return trips (Lavender Beds → Old Gridania) and airship legs back to known cities | Needs the ancestor filter (2.2) to drop "back to" destinations |
| **Areas: world map** | `Map.MapCondition` / `PlaceName.MapCondition` → `MapCondition.Quest` (+ QuestSequence) | 14 quests, 37 links. Examples: The Syrcus Trench → The First, Norvrandt; A Trip to the Moon → Mare Lamentorum, Sea of Stars; Hope Upon a Flower → Elpis; A New World to Explore → Yok Tural, Xak Tural; Unfamiliar Territory → South Horn; Where Eagles Nest → Bozjan Southern Front | Exact | Only regions the map hides; ARR, HW and SB regions are always visible |
| **Areas: first visit** | Derived: `Quest.IssuerLocation` + `TodoParams.ToDoLocation` → `Level.Territory`, `TerritoryIntendedUse` 0 (town) or 1 (field), ancestor rule over main-scenario `PreviousQuest` | **65 of 67** town and field zones that have an aetheryte; 68 quests; 6 zones with several openers (the three start cities, as intended). The two zones it misses, Mist and Wolves' Den Pier, come from warps or curated data. | Good for the main scenario, and it matches the wiki on every checked row | Heuristic: "first objective there", not a game flag. Feature quests give noise (1 314 "openers" when included), so the rule stays main-scenario only. |
| **Aetherytes** | `Aetheryte.RequiredQuest` | 13 rows, **all aethernet or invisible gates** (for example The Ruby Price, Ruby Sea access). **No real aetheryte is quest-gated in the sheet.** | Exact | Attunement is physical, so this alone is almost nothing |
| **Aetherytes: first visit** | Derived: an objective `Level` within 40 y of the aetheryte's MapMarker position (`AetheryteIndex` X/Z), plus the same ancestor rule | **87 of 107** aetherytes (28 at 15 y). Confederate Consternation → **Onokoro** (matches the wiki); The Whims of the Divine → Tamamizu; Come-Into-My-Castrum → Revenant's Toll | Medium: "you stand next to it during this quest" | **20 unreached:** Fallgourd Float, Wolves' Den Pier, Gold Saucer, Camp Cloudtop, Helix, Idyllshire, Zenith, Dawn Throne, Dhoro Iloh, Fanow, Macarenses Angle, Sinus Lacrimarum, Poieten Oikos, Reah Tahra, Abode of the Ea, Ok'hanu, Iq Br'aax, Sheshenewezi Springs, Electrope Strike, Dock Poga. These need a curated `aetheryte_unlocks.json` (20 rows). The city aetheryte can land one quest late: Kugane goes to "The Man from Ul'dah", where the wiki says Not without Incident. |
| **Features & systems** | curated `system_unlocks.json` (133); `Quest.GeneralActionReward` (12); `Quest.OtherReward` (`QuestRewardOther`: Aether Current ×150, Collectable/Specialist Action, Aether Compass, Wondrous Tails, Spearfishing, the Soul crystals) | 133 + 12 + 13 | High (curated with evidence) | **`Quest.SystemReward` (95 quests) is not read anywhere.** It is mixed: 48 values are Trait row ids (job quests, already covered by `Trait.Quest`). 49 values are about 20 opaque feature codes with no name in any sheet, for example 7 = Guildhests, 17 = Chocobo companion, 93 = Retainers, 97 = Sightseeing Log, 99 = Hunts, 21 = every "Way of the <class>", 6 = the level-15 class quests, and 521–537 = Occult Crescent. Use it as a **completeness audit**, not as names. |
| Features from sheets | `SatisfactionNpc.QuestRequired` (Custom deliveries, 12), `MobHuntOrderType.Quest` (hunt bills, 20), `NotebookDivision.QuestUnlock` (crafting log pages, 35 quests / 44), `GatheringSubCategory.Quest` (folklore, 24 / 164), `ContentsNote.ReqUnlock` (Challenge Log, 13), `MountSpeed.Quest` (mount speed per zone, 52 / 63), `ItemStainCondition` (dyes, 11 / 243), `Recipe.Quest` (5 / 72), `DpsChallengeOfficer` (Stone, Sky, Sea, 5), `BeastTribe.IntersocietalQuest` (4), `Buddy.QuestRequirement` (1), `EmjCostume` (8), `TripleTriad.PreviousQuest` (106 quests → card NPCs; plan P6), `Fate.RequiredQuest` (6 / 58 FATEs) | As listed | Exact | Names are often generic: a Recipe or ItemStainCondition would need grouping ("12 dyes") |
| **Jobs** | `Quest.ClassJobUnlock`, `ClassJob.UnlockQuest` / `RelicQuest` | 48 quests | Exact | — |
| **Flying** | `AetherCurrent.Quest`, `QuestRewardOther` 2 | 151 | Exact | Already in Flight; the unlock row reads "Flying in <zone> · 1 current" |
| **Actions & emotes** | `Quest.ActionReward`, `Action.UnlockLink`, `Trait.Quest`, `Quest.EmoteReward`, `AozActionTransient` | 163 + 52 + 51 + 16 quests | Exact | — |
| **Items & collectables** | `Quest.Reward` with ItemAction (mount, minion, roll, card, hairstyle, barding), Achievement → Title | Already in rewards | Exact | — |
| **Shops & vendors** | `GilShop.Quest` (44 quests), `SpecialShop.Quest` (79), `GilShopItem.QuestRequired` (286 quests, 1 177 items), `CollectablesShop`, `InclusionShop` | As listed | Exact | Shop names are generic ("Purchase Items", "Rain Exchange", "Totem Gear (Zoraal Ja)"). The NPC needs an `ENpcBase.ENpcData` scan. Expansion-finale quests open about 36 tomestone shops each, which is noisy. |

Not useful for unlocks:
- `CSBonusContentIdentifier` (162 quests): the challenge-bonus system;
- `QuestRewardOtherDisplay` 9 "???": the finale "new content" teaser on 59 quests;
- `Description`, `Opening`, `AkatsukiNote` and `QuestEventAreaEntranceInfo`.

`MapReplace` (26 quests) swaps an area's map art after the story, which is good to know, not an unlock.

### 2.2 The ancestor rule, precisely

1. For each quest, take its **touched zones**: the territories of `IssuerLocation` and of every `TodoParams[].ToDoLocation`, where `TerritoryType.TerritoryIntendedUse` is 0 (town) or 1 (field).
2. Take its **objective points**: the same `Level` rows with X/Z.
3. Take its **main-scenario ancestors**: the transitive closure of `PreviousQuest`, restricted to main-scenario quests (`EventIconType` 3). For example, The Lominsan Envoy reaches New Gridania.
4. A main-scenario quest **opens** zone T when T is among its touched zones and no ancestor touched T.
5. It **opens** aetheryte A when one of its objective points lies within 40 y of A (same territory, `AetheryteIndex` X/Z) and no ancestor's did.
6. **Warp destinations** use the same filter, applied to every quest. A warp whose destination an ancestor already opened is a way back, not an unlock.

Because the rule works per ancestor set, each start city and Grand Company gets its own correct opener. Six zones have several openers, and that is correct. The cost is cheap: about 1 050 main-scenario quests with memoized sets.

### 2.3 The wiki examples, verified

| Quest (row) | Wiki says | Data says |
|---|---|---|
| Not without Incident (68005) | Kugane (area), The Sirensong Sea (dungeon), Incidentally Speaking (quest) | **Kugane:** Warp 131249 gated by this quest, and also the first-visit rule. **The Sirensong Sea:** CFC 238 in curated `duty_unlocks.json` (script). **Next quest:** "The Man from Ul'dah" (68006). *No quest named "Incidentally Speaking" exists in the current data.* Also: the title "Survivor of the Song", and 10 Gyuki and Ruby Cotton gear items at two vendors (GilShopItem). The Kugane aetheryte lands on the next quest by the 40 y rule. |
| Once More, to the Ruby Sea (68012) | The Ruby Sea (area), Destination Unknown (quest) | **The Ruby Sea:** first-visit rule, and `Aetheryte.RequiredQuest` → the invisible aethernet gate "The Ruby Price" (Aetheryte 120). **Next quest:** "Open Water" (68013). The wiki's next quest is likely an older name or a different step. |
| Confederate Consternation (68016) | Eastern Bow (?), Onokoro (aetheryte) | **Eastern Bow is an emote** (`Quest.EmoteReward` 154). **Onokoro:** the 40 y rule. **Next quests:** The Last Voyage, The Solace of the Sea, The Arrows of Misfortune. |

### 2.4 Spot check: what the Unlocks section would show

| Quest | Exp | Areas | Aetherytes | Duties | Features & systems | Actions, emotes, collectables | Next |
|---|---|---|---|---|---|---|---|
| Close to Home (Gridania, 65621) | ARR | New Gridania, Old Gridania | New Gridania | — | Lancer (job) | — | 3 |
| Into a Copper Hell (66196) | ARR | — | — | Copperbell Mines | — | — | 2 |
| The Ultimate Weapon (70058) | ARR | — | — | The Praetorium, The Porta Decumana | Flying in Mor Dhona (1 current); mount speed rows | Magitek armor (mount) | 32 (cap 3 + "+29") |
| It Could Happen to You (65970) | ARR | The Gold Saucer (warp) | — | — | Gold Saucer | — | 9 |
| An Ill-conceived Venture (66968) | ARR | — | — | — | Retainers | — | — |
| Let the Hunt Begin (Maelstrom) (67100) | ARR | — | — | — | Hunts (ARR) | — | 1 |
| Coming to Ishgard (67116) | HW | Foundation, The Pillars (warp + first visit) | (Foundation lands on "Taking in the Sights") | — | Shop: Purchase Items (GilShop) | — | 2 |
| Towards the Firmament (69208) | HW | The Firmament (warp) | — | (the Diadem: retired, hidden) | — | Triple Triad card | 6 |
| Not without Incident (68005) | SB | Kugane | — | The Sirensong Sea | — | Title | 1 |
| Confederate Consternation (68016) | SB | — | Onokoro | — | — | Eastern Bow (emote) | 3 |
| The Syrcus Trench (68815) | ShB | The Crystarium; world map: The First, Norvrandt | — | — | — | Orchestrion roll | 1 |
| The Next Ship to Sail (69893) | EW | Old Sharlayan; world map: The Source, The Northern Empty, Ilsabard | (Old Sharlayan lands on the next quest) | — | — | — | 1 |
| A New World to Explore (70396) | DT | Tuliyollal; world map: Hydaelyn, Yok Tural, Xak Tural | — | — | — | — | 1 |
| Where the Heart Is (The Lavender Beds) (66748) | ARR | The Lavender Beds (the return to Old Gridania is dropped) | — | — | (Housing, if curated) | — | — |

### 2.5 Icons

| Kind | Icon | Source |
|---|---|---|
| Duty | `ContentType.Icon`: Dungeons 61801, Guildhests 61803, Trials 61804, Raids 61802, PvP 61806, Gold Saucer 61820, Deep Dungeons 61824, Eureka 61833, Ultimate 61832, Save the Queen 61838, V&C 61846, Chaotic 61850, Occult Crescent 61851 | Already resolved by `MoonlitIconResolver` |
| Aetheryte | **060453** (MapMarker DataType 3, ×109) | MapMarker sheet |
| Aethernet shard / gate | **060430** (MapMarker DataType 4, ×278) | MapMarker sheet |
| Area (zone) | MainCommand "Map" (Icon 7). **Check it in the glyph debug window**: `TerritoryType.PlaceNameIcon` (123xxx) and `PlaceNameRegionIcon` (122xxx) are wide name banners, not square icons, so don't use them. | MainCommand sheet |
| World-map region | Same as Area, with the caption "World map" | — |
| Job | 62100 + ClassJob id | Existing |
| Flying | 60033 (aether current) | Existing |
| Feature/system | A curated `icon` field (new, optional) in `system_unlocks.json`. Otherwise the ContentType icon when one fits (Gold Saucer 61820, Hunt 61819, Custom Deliveries 61827, Island Sanctuary 61847, Retainer Ventures 61818, Treasure Hunt 61808, Fishing 61756), else the veiled-moon stand-in | New curated field |
| Emote, action, general action, mount and so on | Their sheet icons (already on `RewardRef.Icon`) | Existing |
| Shop | GilShop.Icon / a coin icon (to verify) | — |
| Next quest | The state moon (Tsukimichi's own glyph), not a game icon | Existing |

---

## 3. Spec

### 3.1 Data model (Core, built once per catalog)

New folder `Tsukimichi.Core/Unlocks/`.

```csharp
/// The section a row is drawn under, in display order.
public enum UnlockGroup : byte { Area, Aetheryte, Duty, Feature, ActionEmote, Collectable, NextQuest }

/// What one row is; maps to a group and to the plan's coarse UnlockKind (P3/P4) via UnlockTargets.PlanKind().
public enum UnlockTarget : byte
{
    Zone, WorldMap, Aetheryte, AethernetShard,                       // Area / Aetheryte
    Dungeon, Trial, NormalRaid, AllianceRaid, FieldOperation, DeepDungeon, Pvp, GoldSaucerDuty, OtherDuty, // Duty
    Job, Society, Flying, System, MountSpeed, Shop, CraftingLog, Folklore, CustomDelivery, // Feature
    Action, Trait, Emote, GeneralAction, BlueMageSpell,               // ActionEmote
    Mount, Minion, Orchestrion, Card, Hairstyle, Barding, Ornament, Title, // Collectable
    NextQuest,
}

/// How sure the row is, shown only in its tooltip ("From the game's data" / "Curated: <evidence>" / "Likely: you first reach it here").
public enum UnlockSource : byte { Sheet, Curated, Derived }

/// One thing a quest opens. Immutable.
/// TargetId: the row id in the target's own sheet (TerritoryType, Map, Aetheryte, ContentFinderCondition, ClassJob, Emote, …; the quest row id for NextQuest).
/// PlaceId: territory of an aetheryte or zone (for Flag map / Teleport); 0 when none.
/// Expansion: ExVersion of the target (sprout mode hides rows past the character's reach).
public sealed record UnlockEntry(UnlockTarget Target, uint TargetId, string Name, uint Icon, UnlockSource Source, byte Expansion, uint PlaceId = 0, string? Note = null)
{
    public UnlockGroup Group => UnlockTargets.GroupOf(Target);
}

/// Per quest, every unlock in display order; reverse lookups; masks for filters; search text. Immutable, allocation-free reads.
public sealed class QuestUnlocks
{
    public static readonly QuestUnlocks Empty;
    public IReadOnlyList<UnlockEntry> For(uint questRowId);                  // grouped, sorted
    public IReadOnlyList<uint> UnlockedBy(UnlockTarget target, uint targetId); // "find the quest that unlocks X"
    public ushort GroupMask(uint questRowId);                               // filter bits
    public string SearchText(uint questRowId);                              // lowercased names, for SearchIndex
    public UnlockEntry? Headline(uint questRowId);                          // the one line a tooltip / overlay / verdict uses
    public static QuestUnlocks Build(QuestCatalog catalog, UniqueRewardCatalog rewards, PlanDuties duties,
                                     UnlockLinks links, CuratedData curated);
}
```

**`UnlockLinks`** (a Core record of plain arrays, so tests can build it by hand) holds what only the sheets know. It is filled in GameData by a new `Tsukimichi.GameData/UnlockLinkReader.cs`, reading through `ExcelModule` like `AetheryteIndex` and `FlightIndex`, at runtime from the client's own sheets, so a patch needs no DataGen run:
- `Warps`: (questRowId, territoryId, name, use);
- `MapRegions`: (questRowId, mapId or placeNameId, name, sequence);
- `GatedAethernet`: (questRowId, aetheryteId);
- `Touches`: per quest, (territoryId, x, z) objective points from `IssuerLocation` and `TodoParams.ToDoLocation`, town and field only;
- `SheetGates`: (questRowId, target, id, name) from SatisfactionNpc, MobHuntOrderType, NotebookDivision, GatheringSubCategory, MountSpeed, DpsChallengeOfficer, BeastTribe.IntersocietalQuest and Buddy. Shops are left for K6.

**The ancestor rule runs in Core** (`UnlockAreas.Derive(catalog, links.Touches, aetherytes)`) over `QuestCatalog.PrerequisitesOf`, so it is tested with a synthetic three-city graph and no game files.

**Build order and precedence:**
1. curated (duty_unlocks, system_unlocks, new `aetheryte_unlocks.json` and `area_unlocks.json` overrides, which can also *remove* a derived row with `"remove": true`);
2. sheet links;
3. reward data (`UniqueRewardCatalog` without the user's overrides, as `UnlockTags` does);
4. the quest's own `Rewards`;
5. derived areas and aetherytes;
6. next quests (true prerequisites only, as `PathChart.LoadUnlocks` already filters).

**Dedupe:**
- the same target and id appears once, and the most trusted source wins (Curated > Sheet > Derived);
- Instance and DutyUnlock of one CFC are one row;
- deep-dungeon floor sets fold into one row (reuse `UnlockTags.ContentName`);
- a Zone opened by a warp *and* by first visit is one Sheet row;
- WorldMap rows for a region whose zone row is present stay: they say different things.

**Order inside a group:** duty kind precedence (as `UnlockKind`), then expansion, then sheet sort key or name.

**Where it lives:**
- `SessionState.Unlocks`, built beside `FeatureQuestIds` / `Stories` on the worker that builds the catalog;
- rebuilt when the catalog, the curated data or the reward catalog instance changes (the `DutyUnlockIndexSource` pattern);
- the cost is O(quests + links), about 20k rows, measured at a few ms in the probe.

**Plan coupling:**
- `UnlockTags` stays the plan's model for 1.13–1.15;
- in K5 it can be rebuilt as a projection of `QuestUnlocks` (`UnlockTargets.PlanKind`), so P3 and P4 read one source;
- `QuestVerdict.Unlocks` reads `QuestUnlocks.Headline`/`For` right away, which adds areas such as "Unlocks Kugane" to the game panels.

### 3.2 The detail pane: an "Unlocks" section

**Placement:** Hero → Not yet → Requirements → Rewards → **Unlocks** → Hand-in → Moonlit → Path → Duties → Giver. It follows Rewards because "what you get" and "what opens" are read together, and the wiki puts them side by side.

**Section header:** "Unlocks" with a FontAwesome `Key` (or `DoorOpen`) section icon. The caption names the headline, for example "Kugane · The Sirensong Sea". It shows no count: plan v6's "show what is left, not tallies" rule.

**Rows:** each group is a small-caps caption line (Areas · Aetherytes · Duties · Features & systems · Actions & emotes · Items & collectables · Next quests) followed by rows:

```
[24px icon]  Kugane                                   Area · Hingashi
[24px icon]  The Sirensong Sea                        Dungeon · Lv 61
[24px icon]  Onokoro                         ✓        Aetheryte · The Ruby Sea
```

- **Layout:** the icon sits on a sunken well the size of a Rewards tile, with the tiles' hairline and 6 px gap between rows, so Rewards and Unlocks read as a pair. The name is in the body text, over a TextTertiary caption: the kind word plus one fact (region, level, territory, "aether current").
- **Owned check:** a ✓ in Moon colour only when the game *confirms* it: duty unlocked, aetheryte attuned, emote or mount owned, through `CollectibleReader` / `RewardUnlockReader`. Otherwise nothing is drawn. There is no "not unlocked" mark, so the pane stays calm.
- **Caps:** at most 6 rows per group, then a "+N more" text button that opens a popover with the rest. The section's height never changes on hover or as icons load; the stand-in tile holds the place.
- **Not repeated** (owner request after 1.12.1: "If the reward is already listed, then don't show the unlock"; see 3.2.1):
  - no group lists anything that belongs to the quest's Rewards (the split in 3.2.1), and an empty group is never drawn;
  - next quests are not drawn here: the Path card's "Unlocks next" comb lists the same quests with the same moons and the same click, so rows here were a full duplicate.
- **When it shows:** the section is left out when nothing is left to draw, so a quest whose only unlocks are its rewards or next quests has no Unlocks section (and an empty Unlocks cell in the table).

### 3.2.1 What belongs in Unlocks, and what stays in Rewards

**One split, nothing in both** (owner request after 1.12.2: "Those need to be explicitly in one section"; `Core/Unlocks/RewardSplit.cs`, tested over every `RewardKind` and `UnlockTarget`):

| Section | Holds | Kinds |
|---|---|---|
| **Rewards**: what you receive and keep | items, optional items, gear, gil and currencies; mounts, minions, emotes, hairstyles, orchestrion rolls, Triple Triad cards, bardings, fashion accessories, titles, achievements; the seven 2.x soul crystals (`QuestRewardOther` 10-16) | `Item`, `OptionalItem`, `ArtifactGear`, `Other` (a currency, a kept other reward), `Emote`, `Mount`, `Minion`, `Orchestrion`, `TripleTriadCard`, `Ornament`, `Barding`, `Hairstyle`, `Achievement`, `Title` |
| **Unlocks**: access and abilities you gain | areas, world-map regions, aetherytes; duties of every kind; flying; features and systems (a system unlock, a named `Quest.OtherReward` such as Wondrous Tails or Spearfishing); jobs and classes; actions, general actions, traits, blue magic | `Instance`, `DutyUnlock`, `AetherCurrent`, `SystemUnlock`, `ClassJob`, `Action`, `GeneralAction`, `Trait`, `BlueMageSpell`, a named `Other` (`QuestRewardOther` 2-8) |

Emotes stay in Rewards: the game and Moonlit count them as collectables. A new `QuestRewardOther` row fails `RewardUnlockSplitTests` until it is sorted.

**What the surfaces draw** (after the split):

| Surface | Draws | Source |
|---|---|---|
| Journal table, Rewards column | The first 4 reward-class rewards that have an icon | `QuestRecord.Rewards` |
| Detail pane, Rewards tiles | Every reward-class reward, then the reward-class rows no reward slot carries (`QuestUnlocks.ExtraRewards`: titles); a gold ring when the unique-reward data names it | `QuestRecord.Rewards`, the unlock index |
| Unlocks column and section, tooltips, Todo, panels, chat | `QuestUnlocks.For`: never a reward-class row, and always the quest's own unlock-class rewards | the unlock index |
| Moonlit | One row per unique reward, in its own categories (duty unlocks, aether currents, jobs and system unlocks among them; left as they are) | `UniqueRewardCatalog` |
| Game panels (offer/result) | A Moonlit line per unique reward | `UniqueRewardCatalog` |

`QuestRecord.Rewards` holds items and optional items (a mount's whistle, a minion, a roll, a card, a hairstyle, a barding, an emote's book), currency, the emote, the action, general actions, the instance (`InstanceContentUnlock`, now with its duty-kind icon), the class or job (now with its job icon) and the named `OtherReward` (Aether Current, Aether Compass, Wondrous Tails, Spearfishing, the soul crystals).

**The rule** (`Core/Unlocks/UnlockRewards.cs`, applied once per catalog in `QuestUnlocks.Build`): an unlock row is *the same thing* as a reward when they name the same item; or the same sheet row of the same kind (emote, action, general action, class/job); or the reward is the aether current (`QuestRewardOther` 2) and the row is flying a current opens; or, failing ids, they carry the same name (case, a leading "the" and a floor set aside) and the row is a duty, feature, job, action, emote or collectable. Areas, aetherytes and next quests never match. A row whose target is reward-class, or that one of the quest's *reward-class* rewards names, is marked `UnlockEntry.InRewards`; an unlock-class reward hides nothing and is drawn as its row (an instance as its duty with the duty-kind icon, the aether current as flying in the zone the reward data names, a named other reward as a feature with the reward's icon or as the action of its name, "Aether Compass"). `QuestUnlocks.For` and `UnlockView.Visible` leave it out, so every surface agrees: the Unlocks column, the detail pane, the row tooltip, the Todo hint, Path's station tooltips, Moonlit's "Also opens", the game panels, `/tsuki` search output and the "Unlocked:" chat line. `QuestUnlocks.IncludingRewards` keeps every row for the reverse lookup and the tests. Moonlit's "Also opens" and the panels' unlock lines also leave out what the quest's own Moonlit rows or lines name.

Next quests live in Path.

**Inside Unlocks**, two rows of one kind with one name are one row ("Collect" for two actions, a title listed twice), a zone and the world map of its name are the zone's row ("The Tempest", "Mare Lamentorum", "Solution Nine"), and a feature named like a duty, job or action of the quest is that duty, job or action ("Blue Mage", "Desynthesis", "Aether Compass"), keeping the feature's icon and note. Action rows wear the sheet's icon (`UnlockLinks.ActionIcons`).

**Visual pair** (detail pane): Rewards, then Unlocks, under the same section header. An Unlocks row's icon well is the size of a Rewards tile, rows keep the tiles' 6 px gap, and the name sits over its caption. Neither section draws an empty header: a quest with no EXP, gil or kept reward has no Rewards section.

**Measured** over the installed game. In 1.12.1, 600 quests drew at least one unlock that repeated a reward; 1.12.2 hid those rows. In 1.12.2, 398 quests drew an unlock-class entry among their Rewards (160 actions, 150 aether currents, 48 instances, 25 jobs, 12 general actions, 6 named features); 0 do after the split (`RewardUnlockSplitTests`). Every reward lands in exactly one section (25 currencies stay in Rewards; 3 solo-instance slots name nothing either could draw), no quest shows a thing in both, no shown unlock repeats a kept reward (206 quests would without the rule, `UnlockRewardDuplicateTests`), no quest shows one unlock twice, and all 308 action rows have an icon. The AutoDuty Duties section keeps its duty rows: it is the automation surface (a Run pill), only drawn with AutoDuty loaded.

**Clicks** (every row is a focusable item, accessibility A6):

| Row | Click | Context menu / tooltip |
|---|---|---|
| Next quest | Selects it (`RevealRow`) | Tooltip: state, level, expansion |
| Duty | Opens a tooltip card: level, item level, content type, roulettes, Duty Support/Trust (C7 badges when built) | "Open in Duty Finder" (decision 1), "Run with AutoDuty" when the Duties section offers it, "Open on the wiki" (ExternalLinks) |
| Aetheryte | If attuned and Lifestream is available: **Teleport** through `TravelService.Teleport(id, name)`, with the usual travel gates. Otherwise **Flag on map** at the aetheryte. | "Flag on map", "Copy coordinates". An aethernet shard uses `AethernetTeleport` when you are in that city. |
| Area / world map | **Open map** to the zone (`AgentMap` open on `TerritoryType.Map`; read-only UI) | "Teleport to <main aetheryte>" when attuned (`TerritoryType.Aetheryte`) |
| Feature & system | Tooltip with the curated note and evidence link | "Open on the wiki" |
| Action, emote, collectable | The existing `RewardTooltip` | As Rewards |

**Motion:** the section fades in with the U8 tokens, like the other sections. Nothing loops.

### 3.3 Spoiler shield

The pane follows the same rules as the rest of the plugin (`SpoilerMask`):

1. **Masked quest** (`IsMasked(rowId)`): the Unlocks section shows one line, "Hidden by the spoiler shield", and nothing else: no names, no icons, no group captions. The existing "Reveal this name" unmasks it with the quest.
2. **Next quests** always print `DisplayName`, so a masked next quest reads "Main scenario quest (Lv 83)".
3. **Reverse lookups** ("Unlocked by …" in tooltips, Route to unlock, search results) print quest names through `DisplayName`. Search **never** matches a masked quest by an unlock name: `SearchIndex` reads `QuestUnlocks.SearchText` only when the quest is not masked, the same path the name already takes.
4. **Sprout mode** (`ReachExpansion`): rows whose `Expansion` is past the character's reach are left out. This covers warps on non-MSQ quests that name a later expansion's city.
5. **Filter chips** count quests only: they never list the names of masked unlocks.
6. **Chat and IPC:** `QuestBrief.Unlocks` keeps its rule ("empty when masked"). The `/tsuki` text outputs (P8) follow the same rule.

### 3.4 Where else it appears

| Surface | Change | Item |
|---|---|---|
| **Game panels** (Worth it?, Journal companion) | `QuestVerdict` reads `QuestUnlocks`: "Unlocks Kugane", "Opens The Sirensong Sea", for every quest, not only feature quests | K4 |
| **Journal table and tree** | Hovering the Name cell for 0.5 s shows a tooltip: "Opens Kugane (area) · The Sirensong Sea (dungeon) · Eastern Bow (emote)", at most 4 and then "+N". No glyph is added to rows. There is an **Unlocks** column (on by default since 1.12.1) with up to 3 kind icons, like the Rewards column; it never repeats a Rewards icon (3.2.1). | K4 |
| **Todo overlay** | "Unlocks you can start here" rows add the headline after the name ("· Opens Retainers"). One line, no icons, quiet (M3). | K4 |
| **Path chart** | Next-step stations' tooltips add the step's headline unlock. The chain line can say "Next in chain opens Kugane". | K4 |
| **Route window** | "Route to unlock" is generalised from duties (`DutyFinderPanel`) to any target: right-click an unlock row, or use the search result "Route to the quest that unlocks Kugane". It is built on `QuestUnlocks.UnlockedBy` + the existing `UnlockRoute`. | K3 |
| **Search** | Unlock names join the search text: typing "kugane", "retainers", "onokoro" or "sirensong" finds the quest. A result row matched by an unlock shows "Opens Kugane" in place of the reward-name hint. | K3 |
| **Filters** | A tri-state **Unlocks** group in the filter drawer (U2): Area · Aetheryte · Dungeon · Trial · Raid · Feature & system · Job · Flying · Emote & action. The same `FilterSet` shape as `RewardKinds` (a dictionary of `TriState`), with a chip in the lane. | K3 |
| **My blues P4 "Do first" tiers** | The tiers read `UnlockTarget`. **Tier 1:** a main-scenario quest later requires the opened duty (`Quest.InstanceContent` of an MSQ quest ∈ this quest's duties: the Crystal Tower, the Hard primals). **Tier 2:** a Dungeon, Trial, NormalRaid, AllianceRaid, DeepDungeon or FieldOperation that is not `ContentFinderCondition.HighEndDuty`, or Flying, or Zone (the Firmament, housing districts). **Tier 3:** System, CustomDelivery, CraftingLog, MountSpeed or Shop. **Tier 4:** a duty with `HighEndDuty` set (read in `UnlockLinkReader`). **Tier 5:** Job, Society. | P4 (consumer) |
| **Moonlit** | No new category: areas and aetherytes are not collectible rewards. Moonlit's DutyUnlock and SystemUnlock entries reuse the Unlocks icons and captions so the two read alike, and their tooltips add "Also opens: …" from `QuestUnlocks`. | K4 |
| **Chat "Opened:" lines** | When a quest completes, an optional line names what opened: "Opened: Kugane · The Sirensong Sea". It follows the existing ChatNotifier.Opened setting. | K4 |
| **Characters › Welcome back** | "Since you were away" lists newly opened duties and areas on the alt's completed quests (optional). | later |

### 3.5 Tests

**Core** (no game files, in `Tsukimichi.Tests/Unlocks/`):
- `QuestUnlocksTests`:
  - grouping and order;
  - precedence (Curated over Sheet over Derived);
  - dedupe: Instance and DutyUnlock of one CFC; floor sets; a warp and a first visit;
  - curated `remove`;
  - next quests exclude lock-only dependents;
  - `UnlockedBy` round-trip;
  - `GroupMask`, `Headline`.
- `UnlockAreasTests`: a synthetic three-city MSQ graph, where each city's own opener opens its zone and The Lominsan Envoy does not reopen Limsa. Also the 40 y radius edge, a return warp dropped, and a cycle guard.
- `UnlockSpoilerTests`:
  - a masked quest returns no names;
  - a next-quest placeholder;
  - search does not match a masked quest by an unlock name;
  - sprout mode hides rows past reach.
- `FilterSet` tri-state Unlocks: equality, hash, clear (as `RewardKinds`).

**Data** (gated by `TSUKIMICHI_GAME_PATH`, `Tsukimichi.Tests/Data/UnlockLinkReaderTests.cs`):
- floors:
  - warp-gated quests ≥ 45;
  - map-condition quests ≥ 14 / links ≥ 37;
  - `Aetheryte.RequiredQuest` = 13 rows, none `IsAetheryte`;
- the **three wiki rows** pinned:
  - 68005 → Zone 628 Kugane + Duty CFC 238;
  - 68012 → Zone The Ruby Sea + gate Aetheryte 120;
  - 68016 → Aetheryte 106 Onokoro + Emote 154;
- the **spot-check table** in §2.4, as a golden file;
- first-visit coverage: 65 or more of 67 town and field zones, 87 or more of 107 aetherytes;
- **curated lint:** every aetheryte the rule cannot reach is in `aetheryte_unlocks.json`, so a new patch's aetheryte fails the test until it is curated;
- **SystemReward audit:** every non-trait `Quest.SystemReward` code on a live quest maps to a `system_unlocks.json` label, so new codes after a patch fail with their quest names.

**UI:**
- `StringsVocabularyTests` for the new strings;
- the detail-pane section stays inside ChromeBands' fixed-height rule (the body scrolls; no band changes);
- the icon ids (060453, 060430, the Map icon) are checked once in the G3 glyph debug window.

### 3.6 Effort and risks

| Piece | Effort |
|---|---|
| K1 Core index + GameData reader + tests | M |
| K2 Detail pane section, clicks, owned checks | M |
| K3 Search, filter, Route to unlock any target | M |
| K4 Tooltips, game panels, Todo overlay, Path, Moonlit icons, chat line | S–M |
| K5 Curated gaps + SystemReward audit + sheet gates (mount speed, crafting log, folklore and others) + UnlockTags as a projection | M |
| K6 Shops and vendors (ENpc scan, grouping) | L (optional) |

**Risks:**
1. **Heuristic areas and aetherytes can be one quest off.** Kugane's aetheryte lands on "The Man from Ul'dah", not the wiki's quest. *Mitigation:* the source is shown as "Likely: you first reach it here" in the tooltip, curated overrides exist, and the golden test pins the result.
2. **Branches** (start cities, Grand Companies, job quests) give one area several openers. That is correct per character, but the reverse lookup lists all of them. *Mitigation:* order the reverse lookup by "on this character's path" first (`MsqProgress`).
3. **Spoilers at the edge of the window.** A visible quest three steps ahead names its zone. *Mitigation:* the same rule as the name: if the quest is shown, its unlocks are shown. Sprout mode trims by expansion.
4. **Noise.** The Ultimate Weapon has 32 next quests, and expansion finales open about 36 shops. *Mitigation:* caps, "+N", and shops left out until K6, grouped.
5. **Patch drift.** New SystemReward codes and sheet column renames. *Mitigation:* audit tests. The reader is one file inside the P9 Lumina compatibility layer: it adds about 12 sheet types to the API 16 port.
6. **Doubling** with Rewards, Path and the AutoDuty Duties section. *Mitigation:* the dedupe rules in 3.2. Duties keeps its automation role, and Unlocks is the reading view.
7. **Opening the Duty Finder** is a game UI call that `DutyFinderHint` deliberately avoids today. → Decision 1.

---

## 4. Plan rows (v6 style)

K is for *key*: what a quest opens.

| Id | Item | Effort | Release |
|---|---|---|---|
| K1 | **What a quest opens, for every quest.** One index in Core of areas, world-map regions, aetherytes, duties, features, jobs, flying, actions and emotes, collectables and next quests, from the sheets (warps, map regions, aethernet gates), the shipped and curated data, and a first-visit rule over the main scenario. Tested against the wiki's own rows. | M | 1.13.0 |
| K2 | **An "Unlocks" section in the quest pane:** game-icon rows grouped by kind; a ✓ only when the game confirms it; a next quest selects it; an aetheryte teleports through Lifestream or flags the map; an area opens the map; spoiler-shielded like names | M | 1.13.0 |
| K3 | **Find what opens X:** search matches unlock names ("kugane", "retainers"); an Unlocks filter (Area, Aetheryte, Dungeon, Trial, Raid, Feature, Job, Flying); Route to unlock any area, aetheryte, duty or feature | M | 1.15.0 |
| K4 | **The same answer everywhere:** a Name-cell tooltip in the Journal, "Unlocks Kugane" in the game panels for every quest, the Todo overlay's unlock rows, Path station tooltips, Moonlit icons, an optional "Opened:" chat line | S | 1.15.0 |
| K5 | **Fill the gaps:** curate the 20 aetherytes and 2 zones the rule can't reach; a SystemReward audit so a patch's new feature can't slip by; mount speed, crafting-log pages, folklore, custom deliveries and hunt bills from the sheets; the plan's tags read from K1 | M | 1.15.0 |
| K6 | **Shops a quest opens**, grouped per vendor with the NPC's name (Totem Gear, tomestone exchanges, the Ishgard vendors) | L | 1.16.0, or "Not doing, for now" |

**Why this order:**
- K1 and K2 land in **1.13**, after U2 fixes the window frame in 1.12, so the new section is built once on the final layout and motion tokens.
- K3–K5 join **1.15 "Right answers"**, where they belong with C1 and C7: duty badges and the item-level wall sit naturally in the duty row's tooltip.
- **P4 (1.16) depends on K1:** its tiers read `UnlockTarget` and `HighEndDuty`.
- If you want it sooner, K1 and K2 can move to 1.12 instead, without touching U2.

## 5. Decisions for the owner

| # | Question | Recommendation |
|---|---|---|
| 1 | Clicking a duty: open the game's Duty Finder on it (`AgentContentsFinder.OpenRegularDuty`, a read-only UI call, never a queue)? Today the plugin deliberately avoids it. | **Yes, behind the duty's context menu only.** Queueing stays with AutoDuty or the player. |
| 2 | Show "likely" rows (first-visit areas and aetherytes) or only rows the game's data states? | **Show them, with "Likely: you first reach it here" in the tooltip.** Without them, aetherytes are almost empty (13 gates, 0 real aetherytes). |
| 3 | Next quests in the Unlocks section too, when the Path card already lists them? | **Yes, at most 3 with "+N in Path"**, so the section reads like the wiki's column. |
| 4 | Show shops (K6)? | **Later.** Names are generic and expansion finales open about 36 shops each. Revisit after 1.15. |
| 5 | An optional "Opens" column in the Journal table? | **Hidden by default**, like the other optional columns. |

## Appendix: how this was measured

- **Probe:** `scratchpad/unlocks/` (not committed). It is a Lumina 7.7.1 console app referencing `Tsukimichi.GameData` for `AetheryteIndex`.
- **Modes:**
  - `refs`: every `RowRef<Quest>` column (75);
  - `probe "<quest name>"`: every link for a quest;
  - `stats`: coverage counts, warps, map conditions, ContentType icons, QuestRewardOther;
  - `areas2 40`: the ancestor rule;
  - `sys`: what SystemReward holds (48 of 97 are Trait ids);
  - `icons`: TerritoryType and MapMarker icons.
- **Shipped data counts:** from `Tsukimichi/Data/unique_quests.json` (game 2026.09.15.0000.0000) and `curated/*.json`.

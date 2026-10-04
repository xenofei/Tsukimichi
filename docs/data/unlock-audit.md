# Unlock data audit (1.19.0, K5 and C3)

Status: **done, 2026-10-04**, against game `2026.09.15.0000.0000`. Feature plan v7 items K5 (unlock data completeness) and C3 (gates from the wiki's free text).

## In short

| What | Before | After |
|---|---:|---:|
| Teleportable aetherytes with an opening quest | 96 of 107 (87 by the first-visit rule, 9 curated) | **107 of 107** (87 by the rule, 20 curated) |
| Town and field zones with an opening quest | 65 of 67 names | 65 of 67 (Mist and Wolves' Den Pier, see Open items) |
| `Quest.SystemReward[1]` feature codes on live quests | 24 distinct codes on 49 quests; 11 shown as a feature, job or action row | 24 codes; **14 shown**, 10 explained as no feature of their own |
| `Quest.SystemReward[0]` trait ids | 48, all the quest's own Trait row | unchanged, now pinned by a test |
| Class unlock rows (`ClassJob`) | 48 (the second Way of the Thaumaturge had none) | **49** |
| Accept conditions that are no quest (unlock links) | 12 values on 10 quests, all "not checked" and none blocking | **11 of 12 judged or stated by a gate**; the 12th is on a removed row |
| Curated game gates | 42 | **90** (48 new) |
| Wiki gates compared (`gate-verification.csv`) | none | 78 rows: **61 match**, 17 single-source rows allowlisted with reasons |

The tests that keep this so: `UnlockLinkReaderTests` (every aetheryte placed), `FeatureCodeAuditTests` (every feature code mapped or explained), `GameGateLinksGameDataTests` (every accept condition that is no quest has its gate; unlock links derived from their sheet rows), `GameGatesDataTests` (two sources per gate), `VerificationAllowlistTests` (every wiki gate curated or excused).

## 1. Aetherytes

The first-visit rule places an aetheryte on the main scenario quest whose objectives first stand within 40 yalms of it. Eleven aetherytes had no such objective and showed on no quest. They are now in `curated/aetheryte_unlocks.json`, chosen this way: the quest the wiki's quest page lists the aetheryte under (its `unlocks` field, `aet …`) when it does; else the first quest in story order whose objectives stand nearest.

| Aetheryte | Quest | Why |
|---|---|---|
| Fallgourd Float | Terror at Fallgourd | First of the level 27–28 quests whose objectives stand 56–68 y away |
| Wolves' Den Pier | A Pup No Longer (each Grand Company) | The PvP introduction; objectives 44 y away; the wiki lists the pier |
| Camp Cloudtop | Onwards and Upwards | The wiki's quest; no objective within 90 y |
| Zenith | The Wyrm's Lair | Nearest main scenario objectives, 159 y |
| The Dawn Throne | The Labors of Magnai | First of three quests at 42–43 y |
| Dhoro Iloh | Something Fishy This Way Comes | The Namazu's first quest; no main scenario quest within 500 y |
| Fanow | Into the Wood | The wiki's quest |
| The Macarenses Angle | The End of a World | The wiki's quest; nothing within 150 y |
| Poieten Oikos | Travelers at the Crossroads | The wiki's quest |
| Electrope Strike | All Aboard | The wiki's quest (with the other Heritage Found aetherytes) |
| Dock Poga | A Tentative First Tour | The allied society quest that first goes there; no main scenario quest within 140 y |

**Compared with the wiki.** Of the 61 aetherytes the wiki's quest pages name, 23 agree with the index and 38 differ. Every difference is the same kind: the wiki lists an aetheryte on the quest that brings the character into the zone (often one quest earlier, sometimes a whole zone's aetherytes on one quest, as All Aboard does for Heritage Found), while the rule names the quest whose objectives stand at the aetheryte. The rule's rows are shown as "Likely: you first reach it here", so both readings are honest. Four curated entries from 1.13 (Reah Tahra, Abode of the Ea, Ok'hanu, Sheshenewezi Springs) also name the later quest; they are kept, because that quest's objectives stand within 41–52 y of the aetheryte while the wiki's quest's stand 133–807 y away.

## 2. Feature codes (`Quest.SystemReward`)

`SystemReward[1]` is an **unlock link**: the game's own flag for a thing a character has opened, the same value space as the accept conditions that are no quest and as `Action.UnlockLink` (`UIState.IsUnlockLinkUnlockedOrQuestCompleted` reads a value below 65536 as a link and anything above as a quest). It has no name in any sheet, so each code was identified from the quest's own text and the wiki.

| Code | Quests | What it is | Shown as |
|---:|---|---|---|
| 2 | Spirithold Broken, Way Down in the Hole, Just Deserts | Guildleves and inn rooms (the quests' own text says so) | **new:** feature "Guildleves and inn rooms" |
| 3 | The Scions of the Seventh Dawn | Unknown; the wiki lists Retainers, which code 93 opens | nothing (explained) |
| 6 | the eight level-10 class quests | The Armoury system (the text says so) | nothing: only the first one done opens it, so naming it on eight quests would mislead |
| 7 | Simply the Hest ×3 | Guildhests | feature (already) |
| 17 | My Little Chocobo ×3 | The chocobo companion | feature (already) |
| 19, 20 | Austerities of Earth, of Wind | Summon Titan, Summon Garuda | actions (already) |
| 21 | the nine Way of the <class> quests | The class | Job row; **fixed:** the second Way of the Thaumaturge row (for a character of another class) had none |
| 26, 92, 93, 97, 99, 255 | Aesthetician, Challenge Log, Retainers, Sightseeing Log, Hunts, Performance | as named | feature (already) |
| 94 | Magiteknical Difficulties | The magitek armor's cannons on the pet hotbar | nothing (no feature of its own) |
| 227 | Yes We Cant | A seasonal pet-hotbar action for one FATE | nothing |
| 247 | Hearts on Fire | Unknown | nothing (explained) |
| 521 | One Last Hurrah | The Occult Record (the text says so) | **new:** feature "Occult Record" |
| 537 | New Job, Old Tricks | The first three phantom jobs | **new:** feature "Phantom jobs" |
| 522, 523, 524, 536, 632 | Occult Crescent story quests | Occult Record lore entries | nothing (lore, not a feature) |

`SystemReward[0]` holds a Trait row id on 48 job quests, each that trait's own quest; the index already shows them as traits.

## 3. Accept conditions that are no quest

`QuestAcceptAdditionCondition` names quests (the catalog makes them prerequisites) and, on 10 quests, 12 values that are no quest. They were listed as "accept condition not checked" and never blocked anything, so those quests read Ready too early. They are unlock links, and each now has a game gate (C3):

| Quest | Value | Gate |
|---|---|---|
| The First Flight of the Excelsior, In the Name of the Light, Operation Archon | 17 | My Little Chocobo done (the link the three My Little Chocobo quests set); met by those quests, else judged from the link |
| A Ruined Land, A Common Thread, Past and Crescent | 509; 510–512; 513 | Occult Record entries, judged from the links |
| Wisdom's End | 639 | The Forked Tower: Magic cleared, judged from the link |
| On the Shoulders of Giants, Faerie Tale | 226, 566 | Heaven-on-High floor 30, Pilgrim's Traverse stone 30: flagged unlike an unlock link, so stated and not read |
| Operation Archon (removed row) | 17 | none: the game no longer lists the row |

## 4. Gates from the wiki's free text (C3)

`Tsukimichi.Verify gates` reads every cached quest page's `requirements` field and its System lines about the quest, sorts them into classes and compares them with the curated gates (`docs/data/gate-verification.csv`). The 48 new gates in `curated/game_gates.json`, each confirmed by two of the game's text, the sheets and the wiki:

| Class | Quests | How Tsukimichi judges it |
|---|---|---|
| Palace of the Dead floor 50 | Knocking on Heaven's Door, Delve into Myth, Pilgrimage of Light | Not checked; met once What Lies Beneath is done; Blocked before The House That Death Built |
| Heaven-on-High floor 30, Pilgrim's Traverse stone 30 | On the Shoulders of Giants, Faerie Tale | Not checked |
| Resistance rank, mettle | 9 Bozja quests | Not checked |
| Occult Record entries, the Forked Tower | 4 Occult Crescent quests | Judged from the unlock links the capture reads |
| Blue magic learned | 16 blue mage quests | Judged from the spell's unlock link |
| The chocobo companion | 3 quests | Met by My Little Chocobo, else judged from the link |
| Skysteel and Splendorous tools held | 10 relic tool quests | Judged from the tools the capture reads (the gear gate's "held") |
| The Dun Scaith raid unlocked | Unidentified Flying Object | Blocked before Where Shadows Reign, met once it is done (this settles its Questionable allowlist entry) |

Not taken, each a single source (allowlisted until 1.21.0 for the in-game check): the floor named on The Nightmare's End, What Lies Beneath, Dead but Not Gone, Rage Extinguished, Orthos Unveiled and A Branch and Their Sapling (most likely the quest's objective: its completion text opens the floors beyond, and no sheet or script states a gate); island sanctuary ranks (3 quests); variant dungeon notes (4); beasts tamed (2); the achievement for The Adventurer with All the Cards; My Little Chocobo for The Black Wolf's Ultimatum. The Pinnacle of Possibility's tool is needed to advance the quest, not to take it, so it has no gate.

## 5. Open items

- **Two zones** have no opening quest: Mist (its housing quest opens the district through no quest-gated warp) and Wolves' Den Pier (reached by the PvP introduction, which the first-visit rule, main scenario only, does not count). A curated `area_unlocks.json`, as the unlocks spec proposed, would place them; it is not built.
- **Sheet features not in the index:** mount speed (`MountSpeed.Quest`), crafting log pages (`NotebookDivision`), folklore (`GatheringSubCategory`), dyes and recipes. Hunts and custom deliveries are already features through `system_unlocks.json`. Left for a later release.
- **Unverified in game:** that the 226 and 566 accept conditions are the Heaven-on-High and Pilgrim's Traverse floors (only the wiki names them; Tsukimichi leaves them unread), that What Lies Beneath is given only after floor 50, and that the unlock links read on a live character match the game's own checks (the reads follow ClientStructs' `UIState.IsUnlockLinkUnlocked`).

# Curated seed data

Hand-maintained overlay for facts that are **not** in the static game sheets
(see `docs/feasibility-report.md` section 4). The DataGen tool merges these
files into `unique_quests.json` and marks touched entries with
`confidence: "curated"`. Runtime reads them read-only from the plugin
directory (design spec section 6).

This page is the per-file reference. The contributor workflow (which file
to change, the evidence rules, checking a change offline with
`tools/regen.ps1` and the curated tests, and reading an invariant failure) is
[CONTRIBUTING.md › Correcting curated data](../../../CONTRIBUTING.md#correcting-curated-data);
players without a pull request use the **Data correction** issue template.

## Files

| File | Shape | Purpose |
| --- | --- | --- |
| `system_unlocks.json` | `{ "<questRowId>": { "label", "kind": "system", "note" } }` | Quests that unlock a game system or feature (retainers, chocobo companion, Gold Saucer, glamour, custom deliveries, deep dungeons, ...). Nothing in the sheets ties these quests to the feature they gate. |
| `duty_unlocks.json` | `{ "<questRowId>": { "contentFinderConditionIds": [..], "note", "evidence" } }` | Quests that unlock a duty where the game data does not say so. DataGen already reads `Quest.InstanceContentUnlock`, `ContentFinderCondition.UnlockCriteria` and the quest-script rule (the script's first duty when it runs `UNLOCK_ADD_NEW_CONTENT_TO_CF` or shows an unlock image; `Tsukimichi.DataGen/DutyUnlockDerivation.cs`); this file holds the rest (Sastasha, Toto-Rak, the main scenario dungeons and trials, the Coil turns, the Wandering Minstrel's extreme trials, ...), each from the wiki's quest or duty page. Values are `ContentFinderCondition` row ids. `DutyUnlockCoverageTests` fails when a Duty Finder dungeon, trial or raid has no unlock quest and is not on its allowlist (the Savage raids opened by talking to an NPC after a quest). |
| `feature_quests.json` | `{ "questRowIds": [..], "note" }` | **Generated, never hand-edited.** DataGen writes it from the same rule the plugin applies at runtime (`FeaturePresets.Derive` over the refiled catalog: quests with the blue `EventIconType` 8 journal icon or the quasi-quest type 10 that shares it, quests in the two unlock files above, quests the unique-reward data credits with an unlock, and quests whose own rewards unlock something; never main scenario, repeatable or retired quests). Seeds the "Unlock quests" virtual category so it is right even when `unique_quests.json` fails to load. `CuratedInvariantsTests` fails when the file differs from the derived set. |
| `festivals.json` | `{ "$schema_note", "entries": { "<festivalId>": { name, start, end, evidence, note } } }` | Seasonal-event windows keyed by `Festival` id (the `Quest.Festival` column), seeded from `docs/data/festival-end-dates.json` (the full-catalog verification's Lodestone-sourced windows) after checking each id's quests against the catalog. `start`/`end` are UTC; the Lodestone gives dates only, so `end` is the close of that day. A past `end` turns the festival's undone quests Locked out while the game does not run it; the plugin shows "ends <date> (Lodestone)" only for an end still ahead with an https `evidence` URL, and "running now" otherwise. Collaboration events (A Nocturne for Heroes, Yo-kai Watch, Blunderville, …) rerun under the same id, so their entries carry `name` (with no year), `evidence` and `note` but **no dates**; such an entry is final: its quests never read Locked out, even for a character who did part of it. An edition whose window is not in `festival-end-dates.json` may carry only its `name` with the year ("The Rising (2024)"), `evidence` (the Lodestone page, or the wiki's event page for an edition not announced yet) and `note`, so the plugin reads the event's name instead of a journal genre that only files it ("Gold Saucer Festivities", "Collaboration Quests"); it is left to the completed-quest rule. The name's "(2014)" is the edition year the Characters dashboard groups seasonal history by. `CuratedInvariantsTests` checks ids, evidence, dates, that only collaborations are reruns, and that every uncurated Festival id's genre is an event name. |
| `chains.json` | `{ "chains": [ { "name", "genreIds": [..], "note" } ] }` | Named quest chains for the detail pane's chain progress widget. Values are `JournalGenre` row ids (not quest ids); a chain is the listed genres concatenated in order, each in journal order. Genres left out still form a chain on their own when their quests are a single previous-quest line. |
| `refile_overrides.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "genre", "note", "evidence" } } }` | Quests pinned to a `JournalGenre` row id **after** the seven refiling rules of `docs/data/unlisted-report.md` section 4 ran (`Tsukimichi.GameData/JournalRefiler.cs`): the sheet's signals point at the wrong genre (the three Eureka entry quasi-quests vote into Kugane Sidequests by their issuer's zone; they belong with And We Shall Call It Eureka) or at none (The New Frontier, whose issuer has no location). An override never moves a retired quest. The detail pane shows "Filed under … (curated override)"; `RefiledFrom` is 8. |
| `retired_quests.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "note", "evidence", "patch"? } } }` | Quests the game removed that the sheets do not mark (no placeholder issuer 1034221, no hidden flag: the three level-9 sidequests deleted in 3.05), which the refiler retires like rule 1 does (`IsRetired`, never counted, listed only under "Removed from the game"; they keep their genre; `RefiledFrom` is 8). Rows the sheets do mark (But I Hardly Noah, The Gift of the Archmagus, A Seat at the Feast, Makin' Bacon (Bread), Wok on By) are retired by rule 1 itself, listed or not (`RefiledFrom` 1); their entry here only supplies `patch`. `patch` (optional, "6.3") feeds the detail pane's "Removed from the game in patch 6.3"; without it the pane names the sheet signal ("Rule 1: placeholder issuer"). |
| `quirks.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "note", "evidence" } } }` | Known quirks: the game behaves differently from what its data says (Up in Arms is optional once the Zenith is in hand, so the next Zodiac Weapons step is offered while the plugin shows it Blocked), or a name changed and older guides mislead (the rank "Bloodsworn" is "Allied" since 7.0; patch 7.5 re-pointed eleven crafter and gatherer sidequests from "Go West, Craftsman" to "Inscrutable Tastes"). The detail pane shows the note under the requirements as "Note: …", `/tsuki why` prints it and the diagnostic block carries it. Notes are original wording (never a wiki sentence); `evidence` is the forum thread, Reddit thread or Lodestone patch note the quirk was reported in. |
| `online_store.json` | `{ "schema": 1, "note", "entries": { "<itemId>": { "name", "kind", "rewardId", "evidence", "note" } } }` | Quest rewards the FFXIV Online Store also sells (seasonal-event collectibles re-sold on the Mog Station), so they are not exclusive to the quest. Keys are the **store item's** `Item` row id; `kind` and `rewardId` name the collectible it unlocks (`Mount`, `Companion`, `Emote`, `Orchestrion`, `BuddyEquip` or `Ornament` row id, the same ids FFXIV Collect uses), so an emote a quest grants directly (no item on the entry) still matches. DataGen marks every `unique_quests.json` entry delivered as the item or granting the collectible with `otherSources: ["OnlineStore"]`; the plugin shows those rows as "Store only" and can hide them. `evidence` is the FFXIV Collect API URL that lists `Premium: Online Store`. |
| `other_sources.json` | `{ "schema": 1, "note", "entries": { "<itemId>": { "name", "source", "where", "evidence", "note" } } }` | Quest rewards that can also be had somewhere other than the quest and the Online Store. Keys are the reward **item's** `Item` row id; `name` is the item's name (DataGen skips an entry whose name differs from the sheet). `source` is an `OtherSource` name the file may use: today only `DungeonDrop` (the item drops in a duty); `where` names the duties as players read them ("Snowcloak, Sastasha (Hard) and The Sunken Temple of Qarn (Hard)"). DataGen marks every `unique_quests.json` entry delivered as the item with `otherSources: ["DungeonDrop"]` and copies `where` into `otherSourceNotes`; the plugin shows those rows as "Also drops" with "Also drops in …" on hover and can hide them with the store re-sells. `evidence` is the Lodestone item page listing the duties under Obtained From. Seeded from the full-catalog verification (`docs/data/verification-full.md`, Discrepancies to fix): the 44 Darklight and Hero's accessories. |
| `payoff_gates.json` | `{ "schema": 1, "review", "note", "entries": { "<gateId>": { "milestone", "before", "instruction", "why", "evidence": [..], "note" } } }` | "Before you continue" payoff gates (P5): optional content whose completion changes a scene in a later story. `milestone` is the `Quest` row id at which the gate speaks (normally a main scenario quest; the last one before the scene when the scene's quest is ambiguous, said in the `note`); `before` is an array of `Quest` row ids that must all be completed, or the name of a chain in `chains.json`. The plugin shows the gate only while the milestone is Ready or in the journal and the content is not done, on the Characters dashboard under the MSQ line, in the Tonight card and once in chat. `instruction` is shown as is and is **instruction only**: it may name the optional content ("Finish the Eden raid series first."), never a story beat, character or place the milestone reveals, and never a main scenario quest past the milestone (`PayoffGatesFixtureTests` lints it against the milestone's name, the main scenario genres after it and the capitalised words of its `why` other than the content's own quest, chain and genre names). `why` is the spoiler, shown only when the player opens "why? (spoiler)". `evidence` holds https URLs (the wiki quest page, the thread the pairing came from). `review` says whether two players who finished the content confirmed every pair; a pair where `before` is already a prerequisite of the milestone can never show and fails the tests. Keys are stable, spoiler-free ids: per-character notice and disclosure state is stored by them. |
| `path_choices.json` | `{ "schema": 1, "note", "cities": { "<questRowId>": { "label", "note" } }, "classes": { "<classJobId>": { "label", "closeToHome", "starter", "note" } }, "grandCompanies": { "<questRowId>": { "grandCompany", "note" } } }` | Labels and guards for the choice groups (`Tsukimichi.Core/Evaluation/PathIndex.cs`, feature plan v4 D1; investigation `docs/data/v4/other-paths.md`). The groups are found from the sheets: the start cities by rule (no previous quest, a level-1 main scenario quest after it), the "Close to Home" sets, the class tracks and the QuestLock sets. `cities` names the three roots and pins them: a root counts only when the rule finds it too, so a rule that drifts after a patch drops a city rather than inventing one. `classes` (keyed by `ClassJob` row id, the one section not keyed by a quest) maps each A Realm Reborn starting class to its "Close to Home" row (the rows differ only in the guild actor, which is the class's "Way of" issuer) and its starter "Way of" quest. `grandCompanies` gives the company of the quests the sheet leaves at 0: The Company You Keep and Call of the Wild. A quest on another city's, class's or company's path reads Locked out ("Another city's start (Ul'dah)") and leaves the counts. `CuratedInvariantsTests` checks that the pin equals the rule, the names and the sets. |
| `extra_prerequisites.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "requires": [..], "sources": [..], "gameTextKey"?, "evidence", "note" } } }` | Quests the game wants completed before it offers a quest that neither the sheet's `PreviousQuest` slots nor its accept conditions (`QuestAcceptAdditionCondition`) record: mostly a main scenario milestone the quest's own text asks for (the Shadowbringers role and crafter lines, the Heavensward and Stormblood job quests, Kojin, Ananta and Pixie quests), and the quests with no previous quest at all (The Hero's Journey, Shadow Walk with Me, Call of the Wild). `requires` are `Quest` row ids, always all needed; the catalog adds them to `QuestCatalog.PrerequisitesOf`, so the evaluator, the blocker text, the reverse index, the path, the unlock route and the path chart see them. **Two sources per entry**: `sources` names at least two of `gameText` (the quest text row in `gameTextKey`, such as `TEXT_LUCKBA131_03246_SYSTEM_100_001`, names every required quest; the text itself is never committed), `questionable` (Questionable adds the link; the ids are in `docs/data/questionable-prerequisites.json`) and `wiki` (the Console Games Wiki infobox names it, as `docs/data/quest-verification.csv` records), and each cited source must name **every** id in `requires`; when the sources disagree, leave the entry out and allowlist it instead. `evidence` is the wiki page when the wiki is cited, else the Questionable file at the pinned commit. `ExtraPrerequisitesDataTests` checks each cited source offline (the game text where the game is installed) and that no entry repeats a sheet prerequisite or closes a cycle; `QuestionableLinksTests` fails on a Questionable link nothing covers. |
| `VERSION.json` | `{ "$schema_note", "curatedRevision": "573d225" }` | **Written by `tools/regen.ps1`, never hand-edited.** The short git hash of the last commit touching a data file in this directory (this file and `README.md` excluded, so a wording change never looks like new data), `-dirty` appended when those files had uncommitted changes. The plugin shows it in Settings › About, the status bar tooltip and the "Report this quest" diagnostic block, so a bug report says which overlay it was judged with. Absent in a checkout that never ran the script; the plugin then shows "unknown". |

## Key convention

- Every key is a **`Quest` sheet row id** as a decimal string (`uint`, always
  `65536` or higher). Never use quest names or the string `Id`
  (`SubCts811_01432`) as keys; names are not unique (city and Grand Company
  variants share a name) and are localized.
- Where a quest has start-city or Grand Company variants, include **all**
  variants as separate entries.
- `contentFinderConditionIds` values are `ContentFinderCondition` row ids
  (`uint`), not `InstanceContent` or `TerritoryType` ids.
- Every entry carries a one-line `note` explaining why it is included and,
  where useful, which variant it is. Entries without a `note` are rejected in
  review.
- `chains.json` is the one file keyed by something else: its `genreIds` are
  `JournalGenre` row ids. Find them with the data-driven tests
  (`ChainCatalogDataTests`) or `Quest.JournalGenre` on xivapi.

## Verifying an id with xivapi v2

Search by name, then confirm the `row_id` and the string `Id` (its prefix
tells you the start city / expansion):

```
https://v2.xivapi.com/api/search?sheets=Quest&query=Name~"My Little Chocobo"&fields=Name,Id,Expansion.Name
https://v2.xivapi.com/api/search?sheets=ContentFinderCondition&query=Name~"Sastasha"&fields=Name,ContentType.Name
```

Direct row lookup: `https://v2.xivapi.com/api/sheet/Quest/66236?fields=Name,Id`.

xivapi v2 requires a `User-Agent` header (requests without one get 403).
The response carries a `version` hash; map it to a patch with
`https://v2.xivapi.com/api/version`. Record the patch in the commit message
when re-verifying after a game update. The initial seed was verified against
version `541c0c12e07da325` (patch 7.56x1).

## Editing rules

- Do not guess ids from memory; verify every id before writing it.
- Keep entries sorted by row id within each file.
- After any change here run `tools/regen.ps1`: it regenerates
  `unique_quests.json` and `feature_quests.json` (DataGen derives the latter;
  do not edit it by hand), verifies the output, refreshes the test fixture and
  records the data version in `docs/data/DATA-VERSION.md`.
- All files must parse as strict JSON (no comments, no trailing commas); both
  the plugin and DataGen reject a file that does not.
- `online_store.json`, `other_sources.json`, `refile_overrides.json`, `retired_quests.json`, `quirks.json`
  and `extra_prerequisites.json` entries need an `evidence` URL besides the `note`; the loader skips an
  entry without either (`other_sources.json` also needs `source` and `where`; `extra_prerequisites.json`
  needs `requires` and two `sources`, and `gameTextKey` with the `gameText` source).
- Evidence is an `https` URL: the Lodestone first (Eorzea Database page or
  official announcement; required for a dated `festivals.json` entry), the
  Console Games Wiki second, and the file-specific source where the table
  above names one (FFXIV Collect for store re-sells, the report thread or
  patch notes for a quirk). Notes are original wording, never wiki text.
- After changing `refile_overrides.json` or `retired_quests.json`, run the tests once
  with `TSUKIMICHI_REGEN_GOLDEN=1` to rewrite `docs/data/refile-expected.csv` (the
  refiling outcome per quest that `RefilingFixtureTests` diffs row by row), review the
  rows that moved, and commit the file with the change. Verify the target genre id with
  `JournalGenre` on xivapi (`sheet/JournalGenre/<id>?fields=Name,JournalCategory.Name`).

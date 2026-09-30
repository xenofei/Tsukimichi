# Curated seed data

Hand-maintained overlay for facts that are **not** in the static game sheets
(see `docs/feasibility-report.md` section 4). The DataGen tool merges these
files into `unique_quests.json` and marks touched entries with
`confidence: "curated"`. Runtime reads them read-only from the plugin
directory (design spec section 6).

## Files

| File | Shape | Purpose |
| --- | --- | --- |
| `system_unlocks.json` | `{ "<questRowId>": { "label", "kind": "system", "note" } }` | Quests that unlock a game system or feature (retainers, chocobo companion, Gold Saucer, glamour, custom deliveries, deep dungeons, ...). Nothing in the sheets ties these quests to the feature they gate. |
| `duty_unlocks.json` | `{ "<questRowId>": { "contentFinderConditionIds": [..], "note" } }` | Quests whose script unlocks a duty. Only ~35 quests link via `InstanceContentUnlock`; the rest (Sastasha, Toto-Rak, the Praetorium, ...) need this file. Values are `ContentFinderCondition` row ids. |
| `feature_quests.json` | `{ "questRowIds": [..], "note" }` | **Generated, never hand-edited.** DataGen writes it from the same rule the plugin applies at runtime (`FeaturePresets.Derive` over the refiled catalog: quests with the blue `EventIconType` 8 journal icon or the quasi-quest type 10 that shares it, quests in the two unlock files above, quests the unique-reward data credits with an unlock, and quests whose own rewards unlock something; never main scenario, repeatable or retired quests). Seeds the "Unlock quests" virtual category so it is right even when `unique_quests.json` fails to load. `CuratedInvariantsTests` fails when the file differs from the derived set. |
| `festivals.json` | `{ "$schema_note", "entries": { "<festivalId>": { name, start, end, mogStation } } }` | Seasonal-event windows. Empty in V1 (DRAFT-NEEDED B); when absent or empty the UI shows only active/inactive. |
| `chains.json` | `{ "chains": [ { "name", "genreIds": [..], "note" } ] }` | Named quest chains for the detail pane's chain progress widget. Values are `JournalGenre` row ids (not quest ids); a chain is the listed genres concatenated in order, each in journal order. Genres left out still form a chain on their own when their quests are a single previous-quest line. |
| `refile_overrides.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "genre", "note", "evidence" } } }` | Quests pinned to a `JournalGenre` row id **after** the seven refiling rules of `docs/data/unlisted-report.md` section 4 ran (`Tsukimichi.GameData/JournalRefiler.cs`): the sheet's signals point at the wrong genre (the three Eureka entry quasi-quests vote into Kugane Sidequests by their issuer's zone; they belong with And We Shall Call It Eureka) or at none (The New Frontier, whose issuer has no location). An override never moves a retired quest. The detail pane shows "Filed under … (curated override)"; `RefiledFrom` is 8. |
| `retired_quests.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "note", "evidence", "patch"? } } }` | Quests the game removed that the sheets do not mark (no placeholder issuer 1034221, no hidden flag: the three level-9 sidequests deleted in 3.05), which the refiler retires like rule 1 does (`IsRetired`, never counted, listed only under "Removed from the game"; they keep their genre; `RefiledFrom` is 8). Rows the sheets do mark (But I Hardly Noah, The Gift of the Archmagus, A Seat at the Feast, Makin' Bacon (Bread), Wok on By) are retired by rule 1 itself, listed or not (`RefiledFrom` 1); their entry here only supplies `patch`. `patch` (optional, "6.3") feeds the detail pane's "Removed from the game in patch 6.3"; without it the pane names the sheet signal ("Rule 1: placeholder issuer"). |
| `quirks.json` | `{ "schema": 1, "note", "entries": { "<questRowId>": { "note", "evidence" } } }` | Known quirks: the game behaves differently from what its data says (Up in Arms is optional once the Zenith is in hand, so the next Zodiac Weapons step is offered while the plugin shows it Blocked), or a name changed and older guides mislead (the rank "Bloodsworn" is "Allied" since 7.0; patch 7.5 re-pointed eleven crafter and gatherer sidequests from "Go West, Craftsman" to "Inscrutable Tastes"). The detail pane shows the note under the requirements as "Note: …", `/tsuki why` prints it and the diagnostic block carries it. Notes are original wording (never a wiki sentence); `evidence` is the forum thread, Reddit thread or Lodestone patch note the quirk was reported in. |
| `online_store.json` | `{ "schema": 1, "note", "entries": { "<itemId>": { "name", "kind", "rewardId", "evidence", "note" } } }` | Quest rewards the FFXIV Online Store also sells (seasonal-event collectibles re-sold on the Mog Station), so they are not exclusive to the quest. Keys are the **store item's** `Item` row id; `kind` and `rewardId` name the collectible it unlocks (`Mount`, `Companion`, `Emote`, `Orchestrion`, `BuddyEquip` or `Ornament` row id, the same ids FFXIV Collect uses), so an emote a quest grants directly (no item on the entry) still matches. DataGen marks every `unique_quests.json` entry delivered as the item or granting the collectible with `otherSources: ["OnlineStore"]`; the plugin shows those rows as "Store only" and can hide them. `evidence` is the FFXIV Collect API URL that lists `Premium: Online Store`. |
| `other_sources.json` | `{ "schema": 1, "note", "entries": { "<itemId>": { "name", "source", "where", "evidence", "note" } } }` | Quest rewards that can also be had somewhere other than the quest and the Online Store. Keys are the reward **item's** `Item` row id; `name` is the item's name (DataGen skips an entry whose name differs from the sheet). `source` is an `OtherSource` name the file may use: today only `DungeonDrop` (the item drops in a duty); `where` names the duties as players read them ("Snowcloak, Sastasha (Hard) and The Sunken Temple of Qarn (Hard)"). DataGen marks every `unique_quests.json` entry delivered as the item with `otherSources: ["DungeonDrop"]` and copies `where` into `otherSourceNotes`; the plugin shows those rows as "Also drops" with "Also drops in …" on hover and can hide them with the store re-sells. `evidence` is the Lodestone item page listing the duties under Obtained From. Seeded from the full-catalog verification (`docs/data/verification-full.md`, Discrepancies to fix): the 44 Darklight and Hero's accessories. |
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
- `online_store.json`, `other_sources.json`, `refile_overrides.json`, `retired_quests.json` and `quirks.json`
  entries need an `evidence` URL besides the `note`; the loader skips an entry without either
  (`other_sources.json` also needs `source` and `where`).
- After changing `refile_overrides.json` or `retired_quests.json`, run the tests once
  with `TSUKIMICHI_REGEN_GOLDEN=1` to rewrite `docs/data/refile-expected.csv` (the
  refiling outcome per quest that `RefilingFixtureTests` diffs row by row), review the
  rows that moved, and commit the file with the change. Verify the target genre id with
  `JournalGenre` on xivapi (`sheet/JournalGenre/<id>?fields=Name,JournalCategory.Name`).

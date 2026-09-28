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
| `feature_quests.json` | `{ "questRowIds": [..], "note" }` | Union of the quest ids in the two files above. Seeds the "Feature Unlocks" virtual category. Regenerate whenever the other two change. |
| `festivals.json` | `{ "$schema_note", "entries": { "<festivalId>": { name, start, end, mogStation } } }` | Seasonal-event windows. Empty in V1 (DRAFT-NEEDED B); when absent or empty the UI shows only active/inactive. |
| `chains.json` | `{ "chains": [ { "name", "genreIds": [..], "note" } ] }` | Named quest chains for the detail pane's chain progress widget. Values are `JournalGenre` row ids (not quest ids); a chain is the listed genres concatenated in order, each in journal order. Genres left out still form a chain on their own when their quests are a single previous-quest line. |
| `online_store.json` | `{ "schema": 1, "note", "entries": { "<itemId>": { "name", "kind", "rewardId", "evidence", "note" } } }` | Quest rewards the FFXIV Online Store also sells (seasonal-event collectibles re-sold on the Mog Station), so they are not exclusive to the quest. Keys are the **store item's** `Item` row id; `kind` and `rewardId` name the collectible it unlocks (`Mount`, `Companion`, `Emote`, `Orchestrion`, `BuddyEquip` or `Ornament` row id, the same ids FFXIV Collect uses), so an emote a quest grants directly (no item on the entry) still matches. DataGen marks every `unique_quests.json` entry delivered as the item or granting the collectible with `otherSources: ["OnlineStore"]`; the plugin shows those rows as "Store only" and can hide them. `evidence` is the FFXIV Collect API URL that lists `Premium: Online Store`. |

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
- Changing `system_unlocks.json` or `duty_unlocks.json` means regenerating
  `feature_quests.json` (union of both key sets, sorted, deduplicated).
- All files must parse as strict JSON (no comments, no trailing commas).

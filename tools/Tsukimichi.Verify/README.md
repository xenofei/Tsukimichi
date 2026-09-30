# Tsukimichi.Verify

A standalone console (feature plan v3, T2a) that checks every named quest in the catalog the plugin ships against
external references, and every `unique_quests.json` entry's "only a quest gives this" claim. It is not part of the
plugin and never runs in CI. It commits facts only (ids, levels, names, counts, URLs), never quest or description text.

Sources, in order of authority:

| Source | Used for |
|---|---|
| Lodestone Eorzea Database (official) | category listings (which quests exist per journal category), quest pages (name, level, class, Grand Company, Quest/Duty requirements, rewards), item pages ("Obtained From") to confirm a reward finding |
| consolegameswiki | quest infobox (name, level, journal, release patch, prerequisites, required duties, rewards, unlocks, retired marker), item Acquisition sections, seasonal event pages |
| FFXIV Collect | the full dumps of mounts, minions, emotes, orchestrion rolls, bardings, hairstyles, fashion accessories and cards |
| Garland Tools | only `reward.instance` for the quests in `curated/duty_unlocks.json` |

The catalog is mapped with the plugin's own `CatalogMapper` and the curated overlay (`Tsukimichi/Data/curated`), so the
tool verifies exactly what players see, refiling and retired quests included. The Lodestone and wiki journal
comparisons read the sheet's own filing (the refiler's genres are the plugin's, not the game's).

## Running

From the repository root:

```
dotnet build tools/Tsukimichi.Verify -c Release -warnaserror
dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll quests
dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll rewards
dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll summary
```

| Command | Does |
|---|---|
| `quests` | every named quest; writes `quest-verification.csv`, `quest-verification-summary.csv`, `festival-end-dates.json`, `verification-full.md`, `verification-manifest.json` |
| `rewards` | every `unique_quests.json` entry; writes `reward-verification.csv`, `verification-full.md`, `verification-manifest.json` |
| `summary` | reads the committed CSVs and the allowlist; prints totals and every row that fails the gate; exits 1 when a row is `unresolved` or `catalogWrong` outside the allowlist, else 0 |

Options (`quests` and `rewards`):

| Option | Meaning |
|---|---|
| `--game <sqpack>` | game sqpack directory (default: the Steam install) |
| `--cache <dir>` | fetch cache (default `%LOCALAPPDATA%\Tsukimichi.Verify\<gameVersion>`); refused inside the repository |
| `--offline` | never fetch; a cache miss becomes a status-0 response and the row says so |
| `--since <csv>` | after the run, print the rows whose verdict changed against a previous `quest-verification.csv` |
| `--rate <seconds>` | minimum seconds between requests to one host (default 2.0) |
| `--limit N` | smoke run over N quests or reward entries, spread over every journal section and special case |
| `--out <dir>` | output directory (default `docs/data`) |
| `--data <file>` | `unique_quests.json` (default `Tsukimichi/Data/unique_quests.json`) |
| `--curated <dir>` | curated directory (default `Tsukimichi/Data/curated`) |

A full online run from an empty cache takes about three hours (roughly 5,400 requests at one every two seconds). With
the cache warm, `quests --offline` takes about fifteen seconds and `rewards --offline` a few.

### Resuming

Every 2xx and 404 response is cached permanently, keyed by URL. Stopping a run (Ctrl+C, a crash, a host that keeps
failing) loses nothing: the next run reads what is cached and fetches only the rest. `quests` also writes partial CSVs
every 100 quests. Other statuses are not cached and are retried next run.

### Offline and after a patch

`--offline` re-derives every verdict from the cache, which is how rule changes are tried. The cache root is keyed by
game version, so a new patch starts a fresh cache; `--cache` points at an older one for an offline comparison, and
`--since` shows what moved.

## Network policy

- User-Agent `Tsukimichi.Verify/<version> (+https://github.com/xenofei/Tsukimichi)` on every request.
- `robots.txt` is fetched once per host and honoured; a disallowed URL is skipped and counted.
- Requests are serial, at most one every `--rate` seconds (2.0 by default) per host.
- 429, 5xx and transport errors back off exponentially; ten consecutive failures stop the run for that host
  (exit code 3) rather than keep trying.
- The wiki is read through the MediaWiki API in batches of 50 titles with `maxlag=5`.
- `verification-manifest.json` records every URL, status, fetch time and body hash, so a run can be audited without
  the bodies.

## Outputs (docs/data)

### quest-verification.csv (long form, one row per quest, fact and source)

| Column | Meaning |
|---|---|
| `rowId` | Quest sheet row id |
| `name` | quest name as the catalog has it |
| `fact` | `listed`, `name`, `displayLevel`, `rawLevel`, `classJob`, `startingClass`, `grandCompany`, `genre`, `section`, `expansion`, `prereqs`, `duties`, `rewards`, `dutyUnlock`, `retired` |
| `catalogValue` | the catalog's value (ids and names joined with `;`, `all:`/`any:` for joins, `accept:` for QuestAcceptAdditionCondition) |
| `source` | `lodestone`, `wiki`, `garland` |
| `sourceValue` | the source's value, same encoding |
| `sourceRef` | the page the value was read from |
| `verdict` | see below |
| `reason` | why, in one line |
| `fixedIn` | where a catalogWrong is corrected, when known |

### quest-verification-summary.csv (one row per quest)

`rowId, name, sourcesChecked, overallVerdict, worstFact, facts, gateFailures`: the worst verdict over the quest's
rows (catalogWrong, unresolved, ambiguous, sourceWrong, sourceLagging, notListed, notModeled, match in that order),
the fact that carries it, the number of rows and how many fail the gate.

### reward-verification.csv (one row per unique-reward entry and source)

`questRowId, questName, kind, rewardId, itemId, rewardName, catalogClaim, source, sourceValue, sourceRef, verdict,
reason, fixedIn`. `catalogClaim` is `quest only`, with `otherSource=` when the entry already names another source
(OnlineStore, SpecialShop, ...). `source` adds `collect` and `sheet` (structural links no external source models).

### Verdicts

| Verdict | Meaning | Fails the gate |
|---|---|---|
| `match` | the source agrees | no |
| `catalogWrong` | two independent sources agree against the catalog (quest facts), or the wiki and the Lodestone (items) or Collect and the wiki (collectibles) show a non-quest source for a reward the catalog calls quest-only | yes |
| `sourceWrong` | the source disagrees, and another source or the sheet itself backs the catalog | no |
| `sourceLagging` | the source has not caught up (a quest newer than the Lodestone's newest listed row in its category; a retired quest the Lodestone still lists) | no |
| `notModeled` | the source states a fact the catalog expresses differently or not at all (reason names how) | no |
| `notListed` | the source has no page or row for the quest or reward | no |
| `ambiguous` | the source cannot be tied to one row (a wiki page shared by same-name rows, a disambiguation page), or the value needs a human reading (an Exchange acquisition) | no |
| `unresolved` | one source disagrees and nothing else decides; needs a human | yes |

### verification-allowlist.json

`entries: [{ rowId, fact, source?, verdict, reason, evidence, fix?, until, rewardId? }]`. `rowId` is a quest row id or `*`;
`fact` is a quest fact or `reward:<Kind>` for reward rows, and `rewardId` narrows a reward entry to one reward. An entry excuses a gate-failing row until the plugin
version reaches `until`; after that `summary` fails again until the row is re-verified or the entry renewed. `fix`
names where a confirmed catalogWrong is corrected (a mapper rule, a curated file, or a data regeneration) and feeds
"Discrepancies to fix" in `verification-full.md`.

### Other files

- `verification-full.md`: run parameters, totals per source, fact and kind, the gate, "Discrepancies to fix", the
  unresolved and catalogWrong lists, the allowlist, the Lodestone lag list, duty-unlock candidates and the
  `SystemReward[1]` coverage list.
- `festival-end-dates.json`: seasonal-event windows with an official Lodestone URL only; `curated/festivals.json`
  consumes it later (P11).
- `verification-manifest.json`: every URL fetched or read from cache in the last run.

## Cache

`%LOCALAPPDATA%\Tsukimichi.Verify\<gameVersion>\<host>\<first 32 hex of SHA-256 of the URL>.body` plus `.meta.json` (URL, status, fetch time,
ETag, SHA-256, size). About 1 GB for a full run. It never lives in the repository; delete it to start over.

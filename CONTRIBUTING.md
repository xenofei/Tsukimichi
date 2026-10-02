# Contributing

Tsukimichi is a Dalamud plugin for FINAL FANTASY XIV; the [README](README.md) says what it does and how players install it. This page is for building it and correcting its data. Bug reports and data corrections go through the [issue templates](https://github.com/xenofei/Tsukimichi/issues/new/choose).

## Build

Requires the .NET 10 SDK and a Dalamud dev install (XIVLauncher puts it at `%AppData%\XIVLauncher\addon\Hooks\dev`; the SDK finds it there, or set `DALAMUD_HOME`).

```
dotnet build Tsukimichi.sln -c Release -warnaserror
dotnet test Tsukimichi.Tests -c Release --no-build
```

Warnings are errors, in CI and locally. The plugin lands in `Tsukimichi\bin\x64\Release\` when built through the solution; building `Tsukimichi/Tsukimichi.csproj` on its own additionally produces `Tsukimichi\bin\Release\Tsukimichi\latest.zip`, the manifest and `images\icon.png` through DalamudPackager. Tests marked `[GameDataFact]` run only when `TSUKIMICHI_GAME_PATH` points at the game's `sqpack` folder; everything else runs from committed fixtures.

To load a build in game: Dalamud Settings (`/xlsettings`) › Experimental › Dev Plugin Locations, add the full path to the build output; then `/xlplugins` › Dev Tools › enable Tsukimichi.

## Layout

| Project | Purpose |
|---|---|
| `Tsukimichi.Core` | Domain model, state evaluation, query, storage, unique-reward catalog. net10.0, no Dalamud reference. |
| `Tsukimichi.GameData` | Lumina mapping from game sheets to the Core model. Shared by the plugin and the tests. |
| `Tsukimichi` | The Dalamud plugin (`Dalamud.NET.Sdk/15.0.0`): runtime state reader, poller, snapshots, ImGui UI. |
| `Tsukimichi.DataGen` | Console tool that turns local game files into `Tsukimichi/Data/unique_quests.json` and verifies it, and stamps new quest ids in `Tsukimichi/Data/quest_patches.json`. |
| `Tsukimichi.Tests` | xunit tests for Core and GameData, with frozen fixtures under `Fixtures/`. |
| `assets/` | The icon (SVG source, renderer, PNG). |
| `docs/` | Feasibility report, plans, design spec, review panel, data reports. |
| `tools/` | `make_pluginmaster.py` (the repository index), `set-tested-version.ps1` (patch day) and curated-data helpers. |

## Correcting curated data

Facts the game sheets do not hold (duty and system unlock quests, story chains, seasonal windows, store re-sells, quirks) live in `Tsukimichi/Data/curated/`. A player who spots a wrong one can file the **Data correction** issue template; this section is for fixing it in a pull request. The per-file reference (every field, the key conventions, the xivapi recipe for checking an id) is [Tsukimichi/Data/curated/README.md](Tsukimichi/Data/curated/README.md).

### Which file holds what

| File | Keyed by | Holds | Change it when |
|---|---|---|---|
| `duty_unlocks.json` | Quest row id | the `ContentFinderCondition` ids the quest unlocks | a duty is unlocked by a different quest, or an unlock quest is missing |
| `system_unlocks.json` | Quest row id | the system or feature the quest unlocks (`label`) | a system unlock (retainers, Gold Saucer, desynthesis, …) points at the wrong quest |
| `chains.json` | `JournalGenre` row ids | named chains as genres in order | a chain is missing a genre or lists them out of order |
| `festivals.json` | `Festival` id | the event's name, Lodestone window and evidence | a seasonal event's dates or name are wrong |
| `refile_overrides.json` | Quest row id | the `JournalGenre` a quest is filed under after the refiling rules | an unlisted quest lands in the wrong journal genre |
| `retired_quests.json` | Quest row id | quests the game removed that the sheets do not mark, and the patch | a removed quest still counts, or the patch is wrong |
| `quirks.json` | Quest row id | a note where the game behaves differently from its data | the NPC offers a quest the plugin shows Blocked (or the reverse) and the data cannot say why |
| `online_store.json` | store `Item` row id | quest rewards the Online Store also sells | a Moonlit reward is sold on the Mog Station |
| `other_sources.json` | reward `Item` row id | quest rewards that also drop in duties | a Moonlit reward drops in a duty |
| `path_choices.json` | Quest row id (cities, companies), `ClassJob` row id (classes) | the start cities' names (a pin the rule that finds them must match), each starting class's "Close to Home" and starter "Way of", the Grand Company of quests whose sheet row leaves it 0 | another city's, class's or company's quests are counted or read Ready, or a label is wrong |
| `extra_prerequisites.json` | Quest row id | the quests the game wants done first that neither the sheet's previous quests nor its accept conditions record, each with two sources | a quest reads Ready while the game asks for another quest first (usually "you must first complete the main scenario quest …") |
| `feature_quests.json` | none | **generated**; never edit it | (regenerate instead) |
| `VERSION.json` | none | **written by `tools/regen.ps1`**; never edit it | (regenerate instead) |

A wrong quest *state* that no curated file explains is a bug in the evaluator, not a data correction: use the **Wrong quest state** template with the diagnostic block.

### The shape of each file

All files are strict JSON: no comments, no trailing commas, keys as decimal strings sorted ascending, and every entry with a `note` in your own words. The sketches below use `…` for text and omit repetition; they are not valid JSON as written.

```
duty_unlocks.json
{ "<questRowId>": { "contentFinderConditionIds": [4], "note": "…" } }

system_unlocks.json
{ "<questRowId>": { "label": "Gold Saucer", "kind": "system", "note": "…" } }

chains.json
{ "$schema_note": "…", "chains": [ { "name": "Hildibrand", "genreIds": [82, 83], "note": "…" } ] }

festivals.json: a dated edition, then an undated collaboration (no year in the name, no dates)
{ "$schema_note": "…", "entries": {
    "<festivalId>": { "name": "All Saints' Wake (2013)", "start": "2013-10-18T00:00:00Z", "end": "2013-11-01T23:59:59Z", "evidence": "https://…", "note": "…" },
    "<festivalId>": { "name": "Lightning Strikes", "evidence": "https://…", "note": "…" } } }

refile_overrides.json ("genre" is a JournalGenre row id)
{ "schema": 1, "note": "…", "entries": { "<questRowId>": { "genre": 117, "note": "…", "evidence": "https://…" } } }

retired_quests.json ("patch" is optional)
{ "schema": 1, "note": "…", "entries": { "<questRowId>": { "note": "…", "evidence": "https://…", "patch": "3.05" } } }

quirks.json
{ "schema": 1, "note": "…", "entries": { "<questRowId>": { "note": "…", "evidence": "https://…" } } }

online_store.json ("kind" is the FFXIV Collect collection: Mount, Minion, Emote, Orchestrion, Barding, Hairstyle, Ornament; "rewardId" its id there)
{ "schema": 1, "note": "…", "entries": { "<itemId>": { "name": "Tender lamb", "kind": "Minion", "rewardId": 46, "evidence": "https://ffxivcollect.com/api/minions/46", "note": "…" } } }

other_sources.json ("source" is DungeonDrop today)
{ "schema": 1, "note": "…", "entries": { "<itemId>": { "name": "…", "source": "DungeonDrop", "where": "Snowcloak and Sastasha (Hard)", "evidence": "https://…", "note": "…" } } }

path_choices.json (classes keyed by ClassJob row id; "grandCompany" is 1 Maelstrom, 2 Twin Adder, 3 Immortal Flames)
{ "schema": 1, "note": "…",
  "cities": { "<questRowId>": { "label": "Gridania", "note": "…" } },
  "classes": { "<classJobId>": { "label": "Lancer", "closeToHome": 65621, "starter": 65559, "note": "…" } },
  "grandCompanies": { "<questRowId>": { "grandCompany": 2, "note": "…" } } }

extra_prerequisites.json ("sources" names at least two of gameText, questionable, wiki; "gameTextKey" only with gameText)
{ "schema": 1, "note": "…", "entries": { "<questRowId>": { "requires": [68850], "sources": ["gameText", "questionable", "wiki"], "gameTextKey": "TEXT_…", "evidence": "https://…", "note": "…" } } }
```

**Two sources for every extra prerequisite.** Each source an entry cites must name every id it requires: the game's own quest text (the row key in `gameTextKey`; the text is never committed), Questionable's hand-added link (`docs/data/questionable-prerequisites.json`) or the wiki infobox (as `docs/data/quest-verification.csv` records it). The tests check all three. When two sources disagree (Questionable names one quest, the sheet's accept condition another), leave the entry out and allowlist the Questionable link with the reason instead.

Quest keys are Quest **row ids** (65536 and up), never the runtime quest id, a name or the script id (`SubCts811_01432`). Where a quest has start-city or Grand Company variants, list every variant. `contentFinderConditionIds` are `ContentFinderCondition` rows, not `InstanceContent` or `TerritoryType` rows. Check every id with xivapi before writing it (the curated README has the queries); never write one from memory.

### Evidence

- Every `evidence` is an **https** URL a maintainer can open. The tests reject anything else.
- **The Lodestone first**: the Eorzea Database page for a quest or item, or the official announcement for an event window or a patch change. A dated `festivals.json` entry must cite the Lodestone.
- **The wiki second** ([Console Games Wiki](https://ffxiv.consolegameswiki.com/)) when the Lodestone does not show the fact: an unlisted quest's filing, a removed quest, an event edition not announced yet.
- File-specific sources where the file calls for one: the FFXIV Collect API entry listing "Premium: Online Store" for `online_store.json`; the Lodestone item page's "Obtained From" for `other_sources.json`; the forum or Reddit thread the quirk was reported in, or the Lodestone patch notes, for `quirks.json`.
- Notes are your own words. Never paste wiki text (CC BY-NC-SA) or quest text into a note.

### Check the change offline

```
dotnet build Tsukimichi.sln -c Release -warnaserror
dotnet test Tsukimichi.Tests -c Release --no-build --filter "Category=Curated"
```

The curated tests run from committed fixtures (`Tsukimichi.Tests/Fixtures/catalog-*.json.gz` and the shipped `unique_quests.json`), so they need neither the game nor the network. With the game installed, regenerate everything the change touches and run the whole suite:

```
pwsh tools/regen.ps1 -NoXivApi
pwsh tools/regen.ps1 -NoXivApi -GamePath "D:\FFXIV\game\sqpack"
```

`regen.ps1` builds, regenerates `unique_quests.json` and `feature_quests.json`, runs DataGen's verification (`-NoXivApi` skips its xivapi spot checks, so it runs offline), refreshes the catalog fixture and the data-version stamps, runs the tests and ends on `git diff --stat`. Commit what it wrote together with your edit. After touching `refile_overrides.json` or `retired_quests.json`, also run the tests once with `TSUKIMICHI_REGEN_GOLDEN=1` and review the rows that moved in `docs/data/refile-expected.csv`.

The external verifier (`tools/Tsukimichi.Verify`, built with the solution) cross-checks the catalog against the Lodestone, the wiki, FFXIV Collect and Garland Tools. A full online run takes hours, so re-derive its verdicts from its cache instead:

```
dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll quests --offline
dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll rewards --offline
dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll summary
```

`--offline` never fetches: a URL missing from the cache (`%LOCALAPPDATA%\Tsukimichi.Verify\<gameVersion>`) becomes a status-0 row that says so. `summary` exits 1 when a row is `unresolved` or `catalogWrong` outside the allowlist. [tools/Tsukimichi.Verify/README.md](tools/Tsukimichi.Verify/README.md) explains the verdicts.

`Tsukimichi.Verify.dll questionable` checks every prerequisite link Questionable adds by hand (`docs/data/questionable-prerequisites.json`, ids only, pinned to a commit) against the catalog built from your game: a link the previous quests, the accept conditions and `extra_prerequisites.json` do not imply fails unless `docs/data/verification-allowlist.json` excuses it (`fact` `prereqs`, `source` `questionable`, `prereqId`). `QuestionableLinksTests` runs the same check in CI from the frozen catalog. To refresh the links, fetch Questionable's `Questionable/Data/QuestData.cs` yourself (for example with `gh api`) and pass it with `--extract <file> --commit <full hash>`.

Every allowlist entry carries an `until` release, and `VerificationAllowlistTests` fails once the csproj `<Version>` reaches it: resolve the row, or extend `until` with a reason.

### When the invariants fail

`CuratedInvariantsTests` and the loader tests beside it (`dotnet test … --filter "Category=Curated"`) fail with a message that names the file and the key, for example:

- `curated quest ids missing from the catalog: duty_unlocks.json:67999` or `duty_unlocks.json:700 is not a Quest sheet row id`: a wrong id, or a runtime quest id where the row id belongs; check it on xivapi.
- `quirks 67999 is not a row of the catalog fixture`, `refile_overrides 67752 names genre 999, which no listed quest holds`: a wrong id or genre.
- An `Assert.StartsWith() Failure` on an `evidence` value, or `festivals.json 2 evidence is not an https URL`: the evidence rule above.
- `festivals.json 2 runs backwards`, `festival 3 (…) is a collaboration event and must not carry an end date`: a festival window that cannot be right.
- `Every_curated_file_parses_as_strict_json_and_loads_without_warnings`: a comment, a trailing comma, or an entry the loader skipped because it lacks `note`, `evidence` or another required field; the warning in the failure names it.
- `feature_quests.json differs from the derived set … regenerate with tools/regen.ps1`: an unlock file changed and the derived list did not; run the script, do not edit the list.
- `Version_json_names_the_last_commit_that_changed_the_curated_data`: commit the data change, then run `tools/regen.ps1` (`-NoXivApi` is enough) so `VERSION.json` names that commit, and commit `VERSION.json` and `docs/data/DATA-VERSION.md`.
- An `Assert.Equal() Failure` on a key list: sort the entries by id.
- `extra_prerequisites.json:68782 cites wiki, but the wiki infobox does not name …` (or `… has no link …`, `… gameTextKey … does not name …`): a cited source does not back the id; fix the id or drop the source (two must remain).
- `Questionable links the catalog does not imply …`: Questionable added a link (after a refresh of its links); add it to `extra_prerequisites.json` with a second source, or allowlist it with the reason.
- `verification-allowlist.json … expired: until 1.5.0 <= plugin version 1.5.0`: the release that was to fix the row is here; resolve it, or extend `until` with a reason.

## Releases

A release is a tag `vX.Y.Z` whose version equals the csproj `<Version>`, whose csproj carries a non-empty `<TsukimichiTestedGameVersion>` no older than the `gameVersion` of `Tsukimichi/Data/unique_quests.json` (see Patch day below), and has a `## [X.Y.Z]` section in [CHANGELOG.md](CHANGELOG.md). GitHub Actions builds `latest.zip` from the tagged commit, attaches it to the GitHub Release and regenerates `pluginmaster.json` on `main` ([release workflow](.github/workflows/release.yml)). Nothing is built or uploaded from a developer machine.

## Patch day

The game hooks (the item tooltip panel, the item and NPC context-menu entries and the server info bar entry) sit beside game UI that a patch can move or change. Each build records the game version they were play-tested on, `<TsukimichiTestedGameVersion>` in `Tsukimichi/Tsukimichi.csproj` (the text of `ffxivgame.ver`, for example `2026.09.15.0000.0000`), and on a game with a newer patch date they pause themselves (a hotfix, which keeps the date and bumps the build numbers, does not pause them): one chat line at login and a notice in Settings › Integrations, while the quest journal keeps working. Players can tick "Enable game hooks on this untested version" to run them anyway; the setting stores the game version it was ticked on, covers that patch date's hotfixes and does not carry over to the next patch. Anything new that draws beside a game addon (the Duty Finder unlock hint in 0.9.0) takes the same `HookGate` (`Tsukimichi.Core/Runtime/HookGate.cs`).

When a patch lands:

1. Update the client and load a dev build with "Enable game hooks on this untested version" ticked (or with the version already bumped locally).
2. Play-test each hook on the new client: hover a quest-exclusive reward in the inventory (the panel sits beside the game tooltip, not over it); right-click it (the "Tsukimichi: quest reward" entry opens the quest); open the target bar menu on a quest-giving NPC ("Tsukimichi: quests here (N)"); check the "☾ N" server info bar entry and its tooltip and click.
3. Fix whatever broke, then run `pwsh tools/set-tested-version.ps1` (reads `ffxivgame.ver` beside the sqpack directory; `-GamePath` or `-Version` to override) and commit the csproj.
4. Add a changelog line such as "Game hooks tested on patch 7.x (game 2026.10.28)", bump `<Version>` and release as usual. The release workflow refuses a tag whose tested version is empty or older than the game version the shipped data was generated from.

A patch that also changes quest data needs `tools/regen.ps1` as well; the two are independent. When the patch added quests, pass the patch number: `pwsh tools/regen.ps1 -Patch 7.6` (see Patch of origin below; the script stops if new quests appear without it).

## Patch of origin

`Tsukimichi/Data/quest_patches.json` maps every Quest row id to the patch the quest was added in, as the game writes it (`"2.0"`, `"6.55"`, `"7.5"`; `""` when unknown). The plugin reads it at catalog build into `QuestRecord.AddedIn` for the "Added in" filter, the "New in 7.5x" group of the Unlocks quick view (its unlock quests from the newest patch series) and the detail pane's "Added in" line. Patch numbers compare as the game writes them (`PatchVersion`: 7.5 < 7.51 < 7.55 < 7.6).

- **Seed (once, networked).** `dotnet tools/Tsukimichi.Verify/bin/Release/net10.0/Tsukimichi.Verify.dll patches` reads Garland Tools' patch documents (about forty requests at one per two seconds, identifying User-Agent, cache under `%LOCALAPPDATA%\Tsukimichi.Verify`), fills any gap from Garland's per-quest documents (cached first, then fetched; resumable) and writes the file plus `docs/data/quest-patches-report.md` (coverage and the cross-checks against the game data). See [tools/Tsukimichi.Verify/README.md](tools/Tsukimichi.Verify/README.md). No wiki text is read.
- **Every later game patch (offline).** `tools/regen.ps1 -Patch <x.y>` runs `Tsukimichi.DataGen --patches Tsukimichi/Data/quest_patches.json --game <sqpack> --patch <x.y>`, which stamps every quest id the file does not list yet with that patch. The file lists every quest id the catalog held at its last write (unknown ones included), so "not listed" means "new since then". A hotfix that adds quests takes its own number (`-Patch 7.51`).
- **Corrections** go in `docs/data/quest-patch-corrections.json` (`{ "<questRowId>": { "name", "patch", "reason", "evidence" } }` under `entries`; `evidence` is an https page stating the patch, usually the wiki quest infobox) and the same line of `quest_patches.json`. The seed lays the corrections over Garland's values, so a rerun keeps them and reports each one as `corrected`; `QuestPatchesFixtureTests` fails when the shipped file disagrees with a correction. The first eight: the ARR class-starting quests Garland dates 3.1 are 2.0 launch quests.

## Translations

Tsukimichi's text is English first, with Japanese, German and French translations in `Tsukimichi/Localization/`. The three translations are **drafts**: machine-assisted and not yet read through by players of each language, so corrections are very welcome, from a single wrong word to a whole help topic. How the text is wired (keys, format strings, what stays English, the layout checks) is in [docs/localization.md](docs/localization.md).

**What to edit.** One XML file per language: `Strings.ja.resx`, `Strings.de.resx`, `Strings.fr.resx`. Each entry is

```xml
<data name="ConfigLanguage" xml:space="preserve">
  <value>Plugin-Sprache</value>
</data>
```

The `name` is the key (never change it); only the `<value>` is yours. `Strings.resx` holds the English source and a `<comment>` explaining what each placeholder is and where the text appears; the in-game Settings › Display › Plugin language switch shows your change after a reload of the plugin. A key missing from a translation reads in English, so a partial fix is fine.

**Rules the tests enforce** (`dotnet test`; they read the files, no game needed):

- Keep every placeholder exactly as the English has it: `{0}`, `{1:N0}`, `{0:0.0}`, `%d`, `%.2f`. You may reorder them (`{1} … {0}`) to suit the sentence.
- Keep anything from `##` or `###` to the end of a value unchanged (`Tsukimichi-Hilfe###TsukimichiHelp`): it is how the game's UI toolkit recognises the window.
- Do not add keys English does not have; do not translate `Core.Culture` (a culture name such as `de-DE`) or the date formats beyond their .NET pattern (`d. MMM`).
- Short labels have room limits: tab names, table column headers, quick views and state names are measured against the layout in `LayoutBudgetTests`; a label that would need the tab rail wider than 200 px, a column wider than 120 px or the quick views wider than the smallest window fails and needs a shorter word.

**Style.** Calm, precise, short, no exclamation marks; labels are nouns. Use the FFXIV client's own terms for your language (quest, main scenario, Grand Company, allied society, aether current, Duty Finder…). Use one name for each quest state everywhere: the table of names per language is in [docs/glossary.md](docs/glossary.md#translated-display-names), and a change there is a change to every surface, so change the key and the glossary together. Help texts quote UI labels ("Hide completed"); quote the label exactly as its own key translates it.

**Marking a translation reviewed.** When players of the language have read the whole file, set its `Meta.TranslationStatus` value from `draft` to `reviewed`: the draft notice in Settings goes away. Mention it in the pull request.

**Not in the files.** Quest, item, NPC, place, job and duty names come from the game in the client's language; curated notes (Known quirks, "Before you continue") and the diagnostic block stay English on purpose.

**A new language** needs a resource file `Strings.<code>.resx` with the two-letter code Dalamud uses (`it`, `es`, `ko`…), the code added to `Loc.Resolve` and `Loc.Shipped` (`Tsukimichi/Localization/Loc.cs`) and to `ResxFiles.Translations` (the tests), a plural rule in `Loc.IsOne`, and fonts that can draw it (Korean and Chinese need Dalamud's extra glyphs, which the plugin already merges).

## Ground rules

- No network code in the plugin, and no automation: it never moves the character, accepts quests or presses anything.
- Nothing that identifies a character (name, content id) goes into logs, diagnostics or issue text.
- Public copy writes the name as "Tsukimichi — FFXIV quest tracker (Dalamud)".
- Commits use conventional prefixes (`feat:`, `fix:`, `docs:`, `build:`, `ci:`); user-visible changes get a line under `## [Unreleased]` in the changelog, written for players.

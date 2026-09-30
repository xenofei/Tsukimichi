# Localization (V2-19)

Tsukimichi's own text follows Dalamud's UI language where a translation exists and reads in English otherwise. Quest, item, NPC, place, job and duty names are not Tsukimichi's text: the catalog is read from the game sheets in the client's language (`IDataManager.Language`), so a Japanese client sees Japanese quest names whatever language the plugin text is in.

## Languages

| Code | Language | File | Status |
|---|---|---|---|
| `en` | English | `Tsukimichi/Localization/Strings.resx` | source |
| `ja` | 日本語 | `Strings.ja.resx` | draft |
| `de` | Deutsch | `Strings.de.resx` | draft |
| `fr` | Français | `Strings.fr.resx` | draft |
| `qps` | Pseudo (layout check) | none: generated from English | test only |

The three translations are machine-assisted drafts, marked as such at the top of each file and by the `Meta.TranslationStatus` key (`draft`). While a file says `draft`, Settings › Display shows a line under the language choice with the translation's coverage and an invitation to correct it. A reviewed file changes the key to `reviewed` and the line goes away. How to correct one is in [CONTRIBUTING.md › Translations](../CONTRIBUTING.md#translations).

## Choosing the language

Settings › Display › Plugin language:

- **Follow Dalamud** (the default) reads `IDalamudPluginInterface.UiLanguage` and follows `LanguageChanged` live. A Dalamud language without a Tsukimichi file (Italian, Korean, Chinese…) reads in English.
- **English** keeps English whatever Dalamud is set to.
- **Pseudo (layout check)** shows while Shift is held when the settings window draws. It stretches every English string by 40 % and brackets it ("[Çháráçtérs ····]"), which shows where a long translation would be cut and which text bypasses the tables (it stays unbracketed).

A switch applies at once: the session bumps its version, so every label cached per session version is rebuilt, the query re-runs, the windows take their titles in the new language (the ImGui ids after `###` never change, so positions and sizes are kept), and the caches that outlive a session version (tab labels, column headers, help cards, the tour, the tree's virtual nodes, the Moonlit rows, the hold countdown) compare `Loc.Version`.

## How it is built

- **Resource files.** `Strings.resx` holds every key in English with a comment on its placeholders; each translation holds the keys it translates. The build turns the translations into satellite assemblies (`ja/Tsukimichi.resources.dll` and so on), and DalamudPackager puts them in `latest.zip` beside `Tsukimichi.dll`; Dalamud's plugin loader probes the culture folders beside the plugin. If a satellite does not load, the log says so and Settings shows "The resource file for this language did not load", and the text stays English. A key a translation lacks reads in English.
- **`Loc`** (`Tsukimichi/Localization/Loc.cs`) merges English and the current language into one dictionary per switch, so `Loc.Get(key)` is a single lookup on the draw path. `Loc.Array(key)` reads `key.0`, `key.1`, … (help cards). `Loc.Plural(n, one, other)` picks a plural form: English and German use the one form for 1, French for 0 and 1, Japanese never.
- **`Strings`** (`Tsukimichi/Ui/Strings*.cs`) keeps its API: `Strings.TabJournal` is a property reading `Loc.Get("TabJournal")`, nested classes key as `Help.StepOpenTitle`, `Tutorial.WelcomeBody`, switch tables as `RequirementName.Level`. Separators, markers and ImGui ids (`" · "`, `"● "`, `"##actions"`) stay constants.
- **Format strings, not concatenation.** Every former Prefix + value + Suffix is one format string with positional placeholders, so a language can move the value: `CharactersForgetQuestionFormat` is "Forget {0}? …". Chat lines put the quest link where the format's slot is (`Strings.SplitAtLink` fills the format with a private-use marker and splits there).
- **Core stays Dalamud-free.** Core's builders write their English at the call site, `CoreText.T("Core.State.Ready", "Ready")`, and the plugin installs `Loc.Provider` for any language but English. Without a provider (tests, DataGen) Core speaks English. The pairs are mirrored into `Strings.resx` under `Core.*` keys, and `CoreTextTests` fails when a Core phrase and its resx entry differ. Composed Core text (state tooltips, the stripe tooltips, the spoiler placeholder) is cached per language by `TextCache`.
- **Requirement clauses.** The evaluator writes `RequirementResult.Detail` in English at evaluation time. The detail pane renders the clause from the requirement record at display time (`RequirementDetail.Text`), so a language switch needs no re-evaluation. In English it is `Detail` itself, and `RequirementDetailTests` checks the rendering equals it for every requirement of a whole resolve.

## What stays English

- **The diagnostic block** (Report this quest, `/tsuki report`) is composed inside `CoreText.English()`: it goes into GitHub issues and is read by maintainers and tools, so its labels, state line and requirement verdicts are English whatever the UI language. Quest names in it are the client's.
- **`/tsuki why`'s requirement lines** use the diagnostic block's per-requirement format ("Level: met (24 ≤ 31)"), so they are English as well. Its headline (state, blocker, giver) follows the UI language.
- **Export files** (Settings › Data › Export, `/tsuki export`): the JSON property names, the CSV column headers and the reward kinds (`Item`, `Mount`) are a file format ([export-format.md](export-format.md)), not UI text, and stay English. The quest, section and reward names inside are the client's (Moonlit reward names fall back to the shipped English names where the sheets have none).
- **Curated data** (`Tsukimichi/Data/curated/*.json`): quirk notes, "Before you continue" instructions, festival names and system unlock labels are written in English by contributors and show in English.
- **Logs**: the Dalamud log is for the maintainer.

## Checks

- `ResxParityTests`: every translation's keys exist in English; every format string keeps English's placeholders (`{0}`, `{1:N0}`, `%d`, `###Id`), a hard failure; every composite format still formats; window ids are kept; the machine keys (`Core.Culture`, the date formats) are readable; the drafts are marked; every key the plugin reads exists in English. Keys a draft lacks are listed in the test output, not failed.
- `LayoutBudgetTests`: every language's tab labels, fixed column headers, quick views and state names against `LayoutBudgets` (Core) at UI scale 1, measured with the advance widths of Dalamud's UI font (`Tsukimichi.Tests/Fixtures/font-advances.json`, written by `tools/font-advances.py` from `NotoSansCJKjp-Medium.otf`). A label wider than its default room is reported and relies on the widening below; one beyond a cap fails.
- `PseudoTextTests`: the pseudo language keeps every placeholder and still formats.
- `StringsVocabularyTests` and `TutorialCopyTests` lint the English file (retired spellings, 35-word tour bodies).
- `CatalogLanguageTests` (with the game data): the catalog maps the same quests in Japanese, German and French, with names in the language and role quests found in each.

## Layout for long strings

Measured at UI scale 1 against the German draft: the tab rail's labels "Tagebuch" (beside the Ready badge) and "Meine Blauen" do not fit the 136 px rail, and the Level ("St."), Expansion ("Erw.") and Rewards ("Belohnungen") headers do not fit their columns. The fixes:

- The tab rail widens to the widest label (`LayoutBudgets.RailWidth`, at most 200 px); English keeps 136 px.
- Each fixed quest-table column is at least its header's width with padding and sort arrow (`LayoutBudgets.FixedColumnWidth`, at most 120 px), re-checked after a language switch.
- The status column's minimum grows to the widest state name ("Bereit (anderer Job)", "Diesen Zyklus erledigt"; `LayoutBudgets.StatusMin`, at most 260 px) before Rewards and Expansion are hidden to make room.
- The quick views already move to a row of their own when the toolbar is narrow; every language fits the smallest window's toolbar.

## Adding a string

1. Add the key to `Strings.resx` (a comment names each placeholder) and a property to the `Strings` partial file that owns the pane: `public static string MyLabel => Loc.Get("MyLabel");`.
2. Anything built from it and drawn every frame is cached with `LocText`/`LocArray`/`LocCache`, or its cache key includes `Loc.Version` (or the session version).
3. In Core, write `CoreText.T("Core.Area.Name", "English")` with a literal English and run the tests: `CoreTextTests` names the key to add to `Strings.resx`.
4. The translations need not follow at once: the key reads in English until a translator adds it.

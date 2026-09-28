# Glossary

One display name per concept. The plugin shows the **display name** on every surface; the **glyph subtitle** (the moon phase the state is drawn as) appears only under the name in Help › Moon phases and in the `/tsukimichi glyphs` window. Internal enum spellings (`QuestState.Foreclosed`, `Preset.LevelBand`) never reach the screen.

Owner of the names: `Tsukimichi.Core/Ui/StateNames.cs` (`StateNames.Name`, `StateNames.GlyphSubtitle`). The plugin's `Strings.StateName`, `Strings.StateName(state, quest)`, `Strings.StateWithReason` and `Strings.StateGlyphSubtitle` delegate to it. `Tsukimichi.Tests/Ui/StateNamesTests.cs` pins the table; `Tsukimichi.Tests/Ui/StringsVocabularyTests.cs` lints `Tsukimichi/Ui/Strings*.cs` for retired spellings.

## Quest states

| Concept (`QuestState`) | Display name | Where it appears | Glyph subtitle |
|---|---|---|---|
| `Ready` | Ready | Table Status, state chips, glyph tooltips, detail pane pill, Moonlit State column, Compare, Todo overlay, Nearby, chat links, Help legend, filter checkboxes | first quarter, glow |
| `ReadyOnOtherJob` | Ready on another job (Nearby and the detail pane name the job: "Ready on {JOB}") | same surfaces | first quarter, silver, gold ring |
| `Accepted` | In journal (detail pane: "In journal, step {n}") | same surfaces; the Recent activity event reads "Picked up" | waxing gibbous, gold ring |
| `Blocked` | Blocked · {blocker} (always paired with the decisive requirement where one exists) | same surfaces; the blocker is the existing requirement detail text | new moon, silver ring |
| `DoneThisCycle` | Done today (daily, `RepeatInterval` 1) / Done this week (weekly, `RepeatInterval` 2) / Done this cycle (any other reset, and surfaces with no quest at hand such as the filter checkboxes and the Help legend) | same surfaces | waning gibbous, silver |
| `Completed` | Completed | same surfaces; item hover hint says "done" | full moon |
| `Foreclosed` | Locked out · {reason} (the reason is the existing detail: "locked out by {quest}") | same surfaces; Help › Totals ("Locked out quests are left out of totals") | eclipsed |
| `Unknown` | Not checked · {reason} where the evaluation has one | same surfaces; the Browse-mode notice explains why states are not evaluated | veiled |

"Veiled" keeps one other meaning: a moon whose data cannot be read live (the toolbar sync moon on a stored snapshot, an item's obtained state for a stored character). It is never a quest-state label.

## Labels renamed in 0.6.0

| Concept | Display name (0.6.0) | Was | Where it appears |
|---|---|---|---|
| Virtual tree node of unlock quests | Unlock quests | Feature Unlocks | Journal tree, Include Unlisted tooltip, tutorial Journal tree step |
| Quick view of unlock quests | Unlocks | Feature quests / Features | Quick views chips, active-filter chip, empty-result guard |
| Quick view of quests near the current job's level | My level | Around my level / Level band | Quick views chips, active-filter chip, empty-result guard |
| Quick view of quests sitting in the journal | Stalled | Stalled | unchanged |
| Group label of the quick views | Quick views | Presets | Head of the filter panel |
| Table column after Job | Status | Next step | Journal table, Characters › Pinned, Characters › Account view, Flight table |
| Todo overlay section of unlock quests in the zone | Unlocks you can start here | Feature quests here | Todo overlay header, Settings › Todo overlay |
| Moonlit tab | Moonlit (pane subtitle: rewards only a quest gives) | Moonlit | Tab strip; subtitle at the top of the Moonlit pane |
| Compare "Why" column reason | Unlock quest | Feature quest | Characters › Compare with |
| Beast tribe requirements | Allied Society rank / Allied Society reputation | Allied society rank / reputation | Requirement names in the detail pane; "Tribal" never appears |
| Quests with no journal genre | Unlisted | Unlisted | unchanged until 0.6.1 |

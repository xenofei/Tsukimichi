# Glossary

One display name per concept. The plugin shows the **display name** on every surface; the **glyph subtitle** (the moon phase the state is drawn as) appears only under the name in Help › Moon phases and in the `/tsukimichi glyphs` window. Internal enum spellings (`QuestState.Foreclosed`, `Preset.LevelBand`) never reach the screen.

Owner of the names: `Tsukimichi.Core/Ui/StateNames.cs` (`StateNames.Name`, `StateNames.GlyphSubtitle`). The plugin's `Strings.StateName`, `Strings.StateName(state, quest)`, `Strings.StateWithReason` and `Strings.StateGlyphSubtitle` delegate to it. `Tsukimichi.Tests/Ui/StateNamesTests.cs` pins the table; `Tsukimichi.Tests/Ui/StringsVocabularyTests.cs` lints `Tsukimichi/Ui/Strings*.cs` for retired spellings.

## Quest states

| Concept (`QuestState`) | Display name | Where it appears | Glyph subtitle |
|---|---|---|---|
| `Ready` | Ready | Table Status, state chips, glyph tooltips, detail pane pill, Moonlit State column, Compare, Todo overlay, Nearby, chat links, Help legend, filter checkboxes | first quarter, glow |
| `ReadyOnOtherJob` | Ready on another job (Nearby and the detail pane name the job: "Ready on {JOB}") | same surfaces | first quarter, silver, gold ring |
| `Accepted` | In journal · step {n} of {m} (the journal step and the quest's step count; "step {n}" when the sheet lists no objectives) | same surfaces; the Recent activity event reads "Picked up" | waxing gibbous, gold ring |
| `Blocked` | Blocked · {blocker} (always paired with the decisive blocker where one exists; see the blocker phrases below) | same surfaces | new moon, silver ring |
| `DoneThisCycle` | Done today (daily, `RepeatInterval` 1) / Done this week (weekly, `RepeatInterval` 2) / Done this cycle (any other reset, and surfaces with no quest at hand such as the filter checkboxes and the Help legend) | same surfaces | waning gibbous, silver |
| `Completed` | Completed | same surfaces; item hover hint says "done" | full moon |
| `Foreclosed` | Locked out · {reason} ("closed by: {quest}" for a completed lock, "Seasonal: ended" for an event the character already saw) | same surfaces; Help › Totals ("Locked out quests are left out of totals") | eclipsed |
| `Unknown` | Not checked · {reason} where the evaluation has one ("Not checked · achievements") | same surfaces; the Browse-mode notice explains why states are not evaluated | veiled |

"Veiled" keeps one other meaning: a moon whose data cannot be read live (the toolbar sync moon on a stored snapshot, an item's obtained state for a stored character). It is never a quest-state label.

## Blocker phrases (0.6.0)

One phrase per quest that is not Ready, from `BlockerText` in Core: the single requirement the player must act on first. The order is play order, not the sheet's: locked out; expansion or level cap; prerequisites; job; level; Grand Company membership, then rank; society rank, reputation, allowances, today's offer; duties; mount, house; seasonal; the kinds the plugin cannot judge. The Status column, the ladders, the pinned and account rows, Compare, Nearby, the todo overlay hints, the item hover hint, the Flight table, the detail header, `/tsuki which`, `/tsuki zone` and the level-up notice all print it.

| Requirement | Phrase | Notes |
|---|---|---|
| Completed lock | closed by: {quest} | Locked out only |
| Expansion above the account's | Expansion: {expansion} | |
| Level above the account's cap | Lv {n}, above your cap | |
| Prerequisite quest | after: {quest} / after MSQ: {quest} | the nearest one still to do; "MSQ" when it is a main scenario quest; through an Any join, the branch with the fewest quests left |
| Job pinned to the quest | Lv {n} on {JOB} | reaching the level on that job is the whole gate |
| Job category | Job: any {category}, you are {JOB} | "any" is not repeated when the sheet's name already starts with it |
| Level on the current job | Lv {n} | the acceptance level, not the journal's displayed level |
| Grand Company membership / rank | Grand Company: {company} / Grand Company: {rank title} | rank titles without the Storm/Serpent/Flame prefix |
| Allied society rank / reputation | Rank: {rank} with the {society} / Reputation: {n} more with the {society} | |
| Allied society allowances / offer | Allowance: none left today / Not offered today | |
| Duty | Duty: {duty} / Duty: {n} to clear | the Duty Finder name of the first duty; the count when no name is known |
| Mount / house | Mount / House | |
| Seasonal event | Seasonal: not running / Seasonal: ended | "ended" with Locked out |
| Not judged by the plugin | Not checked: achievements / accept condition / mount / house | Help › Known quirks lists what is not judged yet; the Status line writes "Not checked · achievements" rather than repeating the words |

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
| Quests with no journal genre | Unlisted | Unlisted | retired in 0.6.1, see below |

## 0.6.1: journal refiling

| Concept | Display name (0.6.1) | Was | Where it appears |
|---|---|---|---|
| Virtual tree node of quests the game deleted (`QuestRecord.IsRemoved`: retired rows, plus any genre-0 row no rule could place) | Removed from the game | Unlisted | Journal tree (off by default), detail pane journal path of a genre-0 quest, tutorial Journal tree step, Help › Why my counts differ |
| Setting that shows the node | Show removed quests | Show Unlisted bucket | Settings › Journal |
| Filter that widens All quests and Unlock quests to the node's quests | Include removed | Include Unlisted | Filters › Advanced, active-filter chip, empty-result guard |
| Setting that picks the filing (`Configuration.JournalFiling`) | Journal filing: Refiled / Legacy | – | Settings › Display; Legacy is the in-field rollback to the 0.6.0 filing |
| Requirement of a retired quest (`RequirementKind.Retired`) | Removed (requirement name); "Locked out · removed from the game" (status) | – | Detail pane requirement list, Status column, blocker line |
| Provenance line of a refiled quest | Filed under {genre} (rule N: {reason}) / Filed under {genre} (curated override) | – | Detail pane, under the journal path |
| Provenance line of a retired quest | Removed from the game / Removed from the game in patch {patch} | – | Detail pane, under the journal path |

"Unlisted" no longer appears on any surface (`StringsVocabularyTests` lints it). The rule reasons are: class or job intro (2), Grand Company (3), nearest listed prerequisite (4), nearest listed successor (5), issuer's zone (6), no signal (7), hidden progress tracker, not counted (9).

## 1.2: what the counts count

Every done/total on screen (tree nodes, the Characters dashboard, the tab badges, chain progress) follows these rules. Owners: `QuestRecord.EntersCounts`, `QuestEvaluation.CountsAsDone` and `TreeCounts`; `ChainCatalog.Progress` for chains.

| Concept | Rule | Why |
|---|---|---|
| Done today / Done this week (`DoneThisCycle`) | Only from the client's cycle data (`CharacterSnapshot.DailyDone`, the allied society daily slots). A repeatable's completion bit never means "done today". | The game never clears a daily's completion bit, so reading it as "today" left 39 dailies reading done forever. Repeatables without cycle data (weeklies, seasonal gifts) read Ready or Blocked; nothing says whether they were done this week. |
| Allied society daily (`QuestRecord.IsAlliedSocietyDaily`: repeatable, offered by an allied society) | In the counts. Counts as done once the character has completed it at least once (`QuestEvaluation.RepeatableDoneBefore`), whatever it reads today; then it no longer adds to the Ready badge. | A "Daily Quests" genre fills as the dailies are tried once, and never empties again at the daily reset. |
| Every other repeatable (weeklies, relic and seasonal repeatables, Primal Focus, Unidentified Flying Object) | Listed under its genre, in no count, not a chain step's progress (`Chain.Uncounted`), never a chain's "next". | They never finish, so a finished chronicle stayed at 14/15 for good. |
| Hidden progress tracker (`QuestRecord.IsProgressTracker`, refiling rule 9) | Listed under the genre rule 4 finds, in no count, never an unlock quest, never a chain step. | Rows the game sets behind the scenes, which neither the Lodestone nor the wiki lists: the YoRHa, Resistance and Ishgardian Restoration markers, Recondition the Anima, Forged Anew. |
| Locked out, or out of season (`QuestEvaluation.LeavesTotals`) | Leaves the total, the chain's included. | As before, and now in chain progress too. |
| Class intro ("So You Want to Be a …") | Listed, in no count. | Unchanged since 0.6.1. |

## Translated display names

Since 1.1 (V2-19) the display names follow the plugin language (Settings › Display › Plugin language; [docs/localization.md](localization.md)). English is the source and owns the concepts above; the Japanese (ja), German (de) and French (fr) names below are the **draft** translations' choices, and players of each language are invited to correct them ([CONTRIBUTING.md › Translations](../CONTRIBUTING.md#translations)). One name per concept per language, as in English: change the resource key (named in the first column) and this table together. Core's names are keys `Core.*` in `Tsukimichi/Localization/Strings*.resx`; the English stays written at its call site in Core (`CoreText.T`), which the tests keep equal to `Strings.resx`.

### Quest states

| Concept (key) | English | 日本語 (ja) | Deutsch (de) | Français (fr) |
|---|---|---|---|---|
| `Ready` (`Core.State.Ready`) | Ready | 受注可能 | Bereit | Prête |
| `ReadyOnOtherJob` | Ready on another job | 他ジョブで受注可能 | Bereit (anderer Job) | Prête (autre job) |
| … naming the job (`ReadyOnJobFormat`) | Ready on {JOB} | {JOB}で受注可能 | Bereit als {JOB} | Prête sur {JOB} |
| `Accepted` | In journal | 受注中 | Im Tagebuch | En cours |
| … with the step (`Core.Blocker.StepOf`) | step {n} of {m} | {m}段階中{n}段階目 | Schritt {n} von {m} | étape {n} sur {m} |
| `Blocked` | Blocked | 条件未達 | Blockiert | Bloquée |
| `DoneThisCycle`, daily | Done today | 今日は完了 | Heute erledigt | Faite aujourd'hui |
| `DoneThisCycle`, weekly | Done this week | 今週は完了 | Diese Woche erledigt | Faite cette semaine |
| `DoneThisCycle`, other | Done this cycle | 今期は完了 | Diesen Zyklus erledigt | Faite ce cycle |
| `Completed` | Completed | 完了済み | Abgeschlossen | Terminée |
| `Foreclosed` | Locked out | 受注不可 | Ausgeschlossen | Exclue |
| `Unknown` | Not checked | 未確認 | Ungeprüft | Non vérifiée |

### Glyph subtitles (`Core.Glyph.*`)

| State | English | 日本語 | Deutsch | Français |
|---|---|---|---|---|
| Ready | first quarter, glow | 上弦の月、輝き | zunehmender Halbmond, leuchtend | premier quartier, lueur |
| Ready on another job | first quarter, silver, gold ring | 上弦の月、銀、金の輪 | zunehmender Halbmond, silbern, goldener Ring | premier quartier, argent, anneau d'or |
| In journal | waxing gibbous, sealed, silver ring | 満ちゆく月、封印、銀の輪 | zunehmender Dreiviertelmond, versiegelt, silberner Ring | gibbeuse croissante, scellée, anneau d'argent |
| Blocked | new moon, silver ring | 新月、銀の輪 | Neumond, silberner Ring | nouvelle lune, anneau d'argent |
| Done | waning gibbous, silver | 欠けゆく月、銀 | abnehmender Dreiviertelmond, silbern | gibbeuse décroissante, argent |
| Completed | full moon | 満月 | Vollmond | pleine lune |
| Locked out | eclipsed | 月食 | Finsternis | éclipsée |
| Not checked | veiled | 朧月 | verschleiert | voilée |

### Blocker phrases (`Core.Blocker.*`)

| Requirement | English | 日本語 | Deutsch | Français |
|---|---|---|---|---|
| Completed lock | closed by: {quest} | {quest}により受注不可 | ausgeschlossen durch: {quest} | fermée par : {quest} |
| Expansion | Expansion: {expansion} | 拡張パッケージ: {expansion} | Erweiterung: {expansion} | Extension : {expansion} |
| Level cap | Lv {n}, above your cap | Lv{n}（レベル上限超過） | St. {n}, über deiner Obergrenze | Niv. {n}, au-dessus de votre plafond |
| Prerequisite | after: {quest} / after MSQ: {quest} | 前提: {quest} / 前提（メイン）: {quest} | nach: {quest} / nach MSQ: {quest} | après : {quest} / après le MSQ : {quest} |
| Pinned job | Lv {n} on {JOB} | {JOB}でLv{n} | St. {n} als {JOB} | Niv. {n} ({JOB}) |
| Job category | Job: any {category}, you are {JOB} | ジョブ: {category}のいずれか（現在は{JOB}） | Job: jeder {category}, du bist {JOB} | Job : {category} au choix, vous êtes {JOB} |
| Level | Lv {n} | Lv{n} | St. {n} | Niv. {n} |
| Grand Company | Grand Company: {company or rank} | グランドカンパニー: {…} | Staatliche Gesellschaft: {…} | Grande Compagnie : {…} |
| Allied society rank | Rank: {rank} with the {society} | {society}とのランク: {rank} | Rang: {rank} bei {society} | Rang : {rank} auprès des {society} |
| Allied society reputation | Reputation: {n} more with the {society} | {society}との友好度: あと{n} | Ansehen: noch {n} bei {society} | Réputation : encore {n} auprès des {society} |
| Allowances / today's offer | Allowance: none left today / Not offered today | 受注権: 本日の残りなし / 本日は依頼なし | Kontingent: heute keins mehr / Heute nicht angeboten | Quota : aucun restant aujourd'hui / Pas proposée aujourd'hui |
| Custom delivery | Custom delivery: rank {n} with {client} | カスタムデリバリー: {client}の満足度ランク{n} | Sonderauftrag: Rang {n} bei {client} | Livraison spéciale : rang {n} avec {client} |
| Delivery Moogle | Delivery Moogle: carrier level {n} | モグレター配達: 配達士レベル{n} | Mogry-Post: Zustellerstufe {n} | Mog-poste : niveau de facteur {n} |
| Duty | Duty: {duty} / Duty: {n} to clear | コンテンツ: {duty} / コンテンツ: {n}をクリア | Inhalt: {duty} / Inhalt: {n} abschließen | Mission : {duty} / Mission : {n} à terminer |
| Mount / house | Mount / House | マウント / ハウス | Reittier / Haus | Monture / Maison |
| Seasonal | Seasonal: not running / Seasonal: ended | シーズナル: 開催期間外 / シーズナル: 終了 | Saisonal: läuft nicht / Saisonal: beendet | Événement : pas en cours / Événement : terminé |
| Not judged | Not checked: achievements / accept condition / mount / house | 未確認: アチーブメント / 受注条件 / マウント / ハウス | Ungeprüft: Errungenschaften / Annahmebedingung / Reittier / Haus | Non vérifiée : hauts faits / condition d'acceptation / monture / maison |
| Removed | removed from the game | ゲームから削除 | aus dem Spiel entfernt | retirée du jeu |

### Labels

| Concept (key) | English | 日本語 | Deutsch | Français |
|---|---|---|---|---|
| Tabs (`TabJournal`, `TabMoonlit`, `TabCharacters`, `TabFlight`, `PlanTab`) | Journal · Moonlit · Characters · Flight · My blues | ジャーナル · 月明かり · キャラクター · 飛行 · 青クエ | Tagebuch · Mondlicht · Charaktere · Fliegen · Meine Blauen | Journal · Moonlit · Personnages · Vol · Mes bleues |
| Moonlit pane subtitle (`MoonlitSubtitle`) | rewards only a quest gives | クエストでしか得られない報酬 | Belohnungen, die nur ein Auftrag gibt | récompenses que seule une quête donne |
| Group of the quick views (`Presets`) | Quick views | クイックビュー | Schnellansichten | Vues rapides |
| Quick views (`QuickViewAll`, `Core.Filter.*`) | All · Unlocks · My level · Stalled · Story sidequests · Sprout mode | すべて · 解放 · 適正レベル · 停滞中 · 物語サブクエ · 若葉モード | Alle · Freischaltung · Meine Stufe · Stockend · Geschichten · Neulingsmodus | Toutes · Déblocages · Mon niveau · En suspens · Récits annexes · Mode novice |
| Tree node of unlock quests (`FeatureUnlocks`) | Unlock quests | 解放クエスト | Freischaltaufträge | Quêtes de déblocage |
| Table column after Job (`ColumnStatus`) | Status | 状況 | Status | Statut |
| Todo overlay section (`TodoSectionNearby`) | Unlocks you can start here | ここで開始できる解放クエスト | Freischaltungen hier | Déblocages à commencer ici |
| Removed node, setting, filter (`RemovedFromGame`, `ConfigShowUnlisted`, `Core.Filter.IncludeUnlisted`) | Removed from the game · Show removed quests · Include removed | ゲームから削除 · 削除されたクエストを表示 · 削除済みを含める | Aus dem Spiel entfernt · Entfernte Aufträge zeigen · Entfernte einbeziehen | Retirées du jeu · Afficher les quêtes retirées · Inclure les retirées |
| Journal filing (`ConfigJournalFiling*`) | Journal filing: Refiled / Legacy | ジャーナル分類: 再分類 / 従来 | Tagebuch-Einordnung: Neu eingeordnet / Klassisch | Classement du journal : Reclassé / Ancien |
| Allied society requirements (`RequirementName.Tribe*`) | Allied Society rank / reputation | 友好部族のランク / 友好度 | Rang / Ansehen der Verbündeten Gesellschaft | Rang / Réputation de tribu alliée |
| Spoiler placeholder (`Core.Spoiler.Placeholder`) | Main scenario quest (Lv 83) | メインクエスト（Lv83） | Hauptszenario-Auftrag (St. 83) | Quête du scénario principal (niv. 83) |
| Filters (`Core.Filter.HideCompleted`, `Core.Filter.AvailableOnly`) | Hide completed · Available now | 完了済みを隠す · 今すぐ着手可能 | Abgeschlossene ausblenden · Jetzt verfügbar | Masquer les terminées · Disponibles maintenant |

Filter identities (`FilterNames.HideCompleted` and the rest) stay English in code: the empty-result guard names filters by them, and only `FilterNames.Display` translates what a chip prints.

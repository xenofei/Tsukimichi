# Panel review: game UX designer

Reviewer: outside principal UX designer (companion apps and in-game journals for large RPGs)
Date: 2026-09-28
Scope: docs/feature-plan-v3.md, docs/design/ui-revamp-proposal.md, docs/design/mockups/main-window.html (rendered with headless Chrome at 1x and 2x), docs/design/glyphs/proposal.md + glyphs-v2.png, spec §2, Tsukimichi/Ui/Strings*.cs, TutorialOverlay.cs, TodoOverlay.cs, MoonlitPane.cs, FilterPanel.cs.
Out of scope by instruction: the Accepted glyph, moon interior detail, the Path star chart (all in revision).

## 0. Verdict in three sentences

The proposal is a well-measured reskin of a catalog. It fixes the thing the owner could see (moons too small, no surface hierarchy) and it is honest about what ImDrawList can do. It does not fix the thing the player research says (docs/research/player-gripes-2026.md §1): the player opens this window to answer "what can I do right now, why can't I do that one, and is it worth it", and the design still lands them on 5,193 rows under "All quests" with the answer to the first question nowhere and the answer to the second cut off by an ellipsis in the mockup itself ("Ready · Vors…", "Rank Truste…", "Job any Disci…").

## 1. Top ten issues, ranked, with a fix each

### 1. There is no "what now" surface; the catalog is the landing page
The window opens on Journal › All quests, the tab badge says "62%", the status bar says "62%" again and the tree root says "62%" a third time. Sixty-two percent of 5,373 quests is not a goal any player holds, and nothing on screen says "you have 4 quests you can pick up, the next MSQ is X, an event is running". The Todo overlay has exactly that data (Pinned, Feature quests here, Main scenario, Job quests) but it is a satellite, not the front door. The tutorial's own Quick Start says "six steps from an empty window to a plan for the evening"; the plan for the evening should be the first thing drawn, not the sixth step.

Fix: make the Journal tab's empty selection (no quest selected) a "Tonight" card in the detail column instead of "Select a quest in the table…": Ready now (count, top 5 with zone), next MSQ with giver, events running now, pins, quests stalled. Every line is a click into the table. It costs one card and reuses TodoOverlay's row model. Replace the three "62%" with one gauge (status bar) and put the Ready count where the tab badge is. This is where P7 ("Since you were away") and P11 ("Seasonal now") land later without new chrome.

### 2. Gold carries everything, so the attention hierarchy is inverted
Principle P2 says "gold means look here". In the rendered mockup gold is: Completed moons, Ready moons, Accepted moons, the 100 % node, section names of complete nodes, the selection fill, the selection bar, the sort arrow, the active tab icon, the Ready badge pill, the MSQ chip, primary buttons, the focus ring, the halo arc, the chain bar, unique-reward rings. For a veteran (most of the research cohort) the tree is 80 % gold: the past glows as loudly as the present. Look at the tree crop: A Realm Reborn (done) is the brightest row; Shadowbringers (where the player actually is) is the dimmest.

Fix: split gold into two jobs. "Actionable now" (Ready, Accepted, next step, pins, MSQ chip) keeps Moon/MoonBright plus the halo. "Done" (Completed moons, complete nodes, 100 % gauges) drops to MoonDim `#B8933F` with no glow, and the complete-node name stays Silver. Chrome that is not a call to action (sort arrow, selection outline, section rules, active tab icon) moves to Silver/VeilLine. Rule to write into §1: at most one gold element per row unless the row is actionable.

### 3. The answer to gripe #1 is the column that gets truncated
"Hidden prerequisites with no explanation" is the top grievance (16 threads). The Next step column is the blocker line, and the mockup shows it truncated on 11 of 13 rows at the default 1100 px window with a 360 px detail pane and 240 px tree. The subject of the sentence is what is cut ("Ready · Vors…" loses the NPC; "Rank Truste…" loses what rank is needed). The chain "Next step" is also doing three jobs: blocker for Blocked, step for Accepted, date for Completed, veil reason for Unknown.

Fix: (a) rename the column "Status" and make it the widest, weight-stretch column; Name gets a fixed max and ellipsis, Status never truncates the qualifier before the noun (ellipsise "Rank: Sworn with the Sylphs" as "Rank: Sworn (Sylphs)…", never "Rank Truste…"). (b) Hide Exp and Rewards below 1000 px table width before touching Status. (c) Move "Done · 2024-…" out of the column: Completed rows show the date only on hover; the column shows "—". (d) Make the detail column collapsible (a 24 px rail with the moon and name) so the table can have the width when the player is scanning, not reading. (e) Pull P1 (blocker line) into 0.8.0 T15 since the column is being rebuilt anyway.

### 4. Three names for most states
The same state has different names in the state enum, the chips, the Next step column, the tutorial and the help: Ready / "Available now" (filter) / "Available only"; Unknown / "veiled" (help, hint, mockup "Veiled · achi…") / "Not checked"; Completed / "Done" (column, Items hint "done"); Foreclosed / "Locked by G…" (mockup) / "eclipsed" (help); Done this cycle / "Done today" (mockup) / "Done cycle" (chip). "Foreclosed" is a mortgage word; the mockup's own author avoided it in the column. Players learn one label per concept; the phase names ("waning gibbous, silver") belong in the legend, not the labels.

Fix: a one-page glossary in the design doc with exactly one user-facing label, one short label (chips), one glyph name (legend only) per state, and a lint test that every Strings constant naming a state uses the glossary word. Proposed set in §3.

### 5. The while-playing surface is the least designed
Principle P1 is "glanceable while playing", but the 1100 × 700 main window is never open while playing; the Todo overlay and Nearby window are. Today the overlay is a stock ImGui window with `CollapsingHeader`s, BgAlpha 0.85 by default (min 0.2), hints in Dusk (`#7C86A8`) and no text outline. Dusk at 5.1 : 1 on Night becomes 2–3 : 1 over a bright Thavnair beach through a translucent window. The proposal gives the overlay one clause in T17 ("Help/Todo/Discovery on Chrome") and no spec rows. The glyph proposal's own floor (14 px minimum for a moon) is not applied to the overlay's inline glyph.

Fix: a §2.9 for the overlay: Silver text with a 1 px Night outline (four-offset `AddText`, cheap), Mist minimum for hints, moons at the 14 px floor, section headers as Chrome hairlines not `CollapsingHeader`, a Compact mode (moon + name only, one line per row, fixed width), auto-hide in combat and cutscene (already there, keep). Move it to the front of 0.8.0 (see §5).

### 6. First run is a fifteen-step tour of the furniture
`CheckFirstRun` offers a 15-step tour whose steps are named after widgets (Search, Filters, Chips, Four tabs, Journal tree, Quest table…). Studios that measured this see 70–85 % skip rates on tours longer than four steps; what is remembered is the first task the product helped with. The welcome body ("Tsukimichi is the road you walk by moonlight: every quest is a step, and the moon fills as you complete it") is lovely and says nothing about what the plugin does for the player. Worse, the first thing a fresh install shows before login is a table of veiled moons with the notice "No character snapshot: states, next steps and availability are not evaluated."

Fix: three task-framed steps only ("What can you pick up now" → Ready rows; "Why not this one" → detail requirements; "What is worth doing" → Moonlit), each ending on a real row, and the rest of the tour behind "Show me everything" in Help. Welcome copy leads with value (see §3). Before the first snapshot, the landing card says "Log in and Tsukimichi reads your journal; nothing in the game changes" rather than a wall of veiled moons.

### 7. The presets control lies about its semantics and the scope is invisible
A segmented control means exactly one segment is always on. Here Features / Level band / Stalled are three optional, mutually exclusive filters (`f.Preset = active ? Preset.None : preset`), so the control has a hidden fourth state with no segment. Users will look for "All". They are also three different kinds of thing (content class, snapshot-relative level, state + age), and none is a "preset" in the sense any other app uses (a saved filter set). Meanwhile the tree scope, which narrows the table more than any filter, appears nowhere in the chip row, so "Nothing matches" can be caused by a scope the chips do not mention.

Fix: four segments with "All" first, or three toggle chips styled like the chip row (my preference: they then live in the chip row logic). Rename the group "Quick views". The tree scope becomes the first chip ("in Side Story Quests ×", click clears to All quests). Define what the Filters badge counts (filters only; not search, not the view). Real saved views are idea B4 below.

### 8. The detail pane spends its best pixels on decoration and shows the chain three times
Hero banner: 150 px of a 700 px window for art the in-game journal already shows, pushing the Requirements card (the reason the pane exists) to y ≈ 340. The chain is then shown as a bar ("Chain: Sylphs 3 of 7"), as a Path card header ("7 steps · 3 done") and as a requirement row ("Previous quest Sylph-management"). The next-step row carries four markers at once: a Ready state glyph (which means "Ready" everywhere else, so a Blocked quest's requirement shows a Ready moon), a ▸, gold text and a gold bar. The action bar is five unlabeled round icons; Pin, the action the Todo overlay and dashboard depend on, is not among them; "Show path" is gone; the primary action (Flag on map, or Teleport when Lifestream is present) has the same weight as Copy coordinates. Provenance reads "Blocked per client flags at 21:14", which is engineer's English.

Fix: hero ≤ 96 px (name strip over a cropped band), Requirements first, chain bar folded into the Path card header, next-step marker is one thing (the gold bar plus Moon text; no state glyph, no ▸), action bar = one labelled primary pill (Flag giver / Teleport) + Pin + overflow icons, provenance "Live · checked 21:14" / "From snapshot · 21:14".

### 9. The Classic toggle makes two products
"Classic layout restores the stock tab bar and Dalamud chrome" means two toolbars, two tab strips, two sets of tutorial rects, two smoke checklists, forever. No studio maintains a second layout for theme compatibility; they map tokens. The stated reason (users on light Dalamud themes) is real but is a colour problem, not a layout problem.

Fix: one layout. Add a "Follow Dalamud colours" setting (default off) that derives the surface tokens from the host style at push time (`Night ← ImGuiCol.WindowBg`, `NightRaised ← ChildBg/FrameBg`, `NightLine ← Border`, text tokens from `Text`/`TextDisabled`) and keeps Moon/Eclipse as the only fixed hues. Drop `ClassicLayout` from Configuration before it ships.

### 10. Moonlit's confidence badges and Compare's "unlock value" expose internals in the wrong colours
Confidence is rendered as the lowercase words "static / community / curated / yours" in Silver / Dusk / Moon / **Eclipse**. "static" means "from the game sheets" to nobody but the author; "community" in Dusk reads as disabled; the user's own verdict is in the Foreclosed-and-destructive colour. The confidence filter offers Static, Curated, Yours and Unknown obtained but not Community, so the column shows a value the filter cannot select. In Compare, "Value: 1 for any quest, +3 main scenario, +5 feature quest, +2 per unique reward" is a made-up score surfaced as a column with a tooltip apologising for it, and "{A} is 212 quests ahead of {B}" turns alts into a race.

Fix: labels "Game data" (no colour, the default, not even shown as a word: an empty cell), "Community" (Mist), "Verified" (Moon), "Yours" (Silver with a small pencil glyph). Never Eclipse for a non-destructive meaning. Add Community to the filter or drop the label. In Compare, replace the numeric value with the reason tag (MSQ / Unlock / Unique reward) and sort by tag, and reword the headline to "212 quests done on A that B has not".

## 2. Further findings (not in the top ten, still fix)

- **Dense mode breaks the glyph floor.** Dense = 24 px rows, r 6 (12 px) moons. The glyph proposal's own minimum is R = 7 (14 px) with track-and-arc only below that. Either Dense rows are 26 px with 14 px moons, or Dense drops the moon and keeps the 3 px stripe + a two-letter state code.
- **Cards mode: cut it, do not slip it.** Journal banners are shared across genres; a Cards view of Sidequests is hundreds of identical thumbnails at 64 × 32. It also drags a `Column.Banner`, texture loads on the draw thread and a third density into T15. Nobody browses 5,373 quests as cards. T12 already ships a "Cards 44 px" density value in 0.7.0 with nothing to render it; remove it there too.
- **Three interacting size controls** (UiScale × IconScale × Density) is a 3 × continuous × continuous QA matrix. Density should replace IconScale for row glyphs; IconScale stays for banners and reward icons only.
- **Idle motion.** A breathing live pip in the toolbar and the status bar, plus hover lerps on every row, is exactly what game UI teams strip out: peripheral motion in a window that sits next to the game view steals attention from the game. Keep motion event-driven (gauge fill on change, reveal pulse, chevron) and make the pip static. Hover in ImGui is instantaneous everywhere else in Dalamud; a 120 ms lerp reads as lag, not polish.
- **Keyboard shortcuts collide with the game.** Ctrl+1..4 and a bare F are hotbar keys in FFXIV's default bindings; the plugin only sees them when its window is focused, but the user does not always know which has focus and an accidental Ctrl+3 that changes a tab instead of casting is a support ticket. Keep Ctrl+F and Esc; drop the rest or make them opt-in.
- **Tab badges mean different things.** Journal "62%" (progress), Moonlit "41%" (progress), Characters "3" (a count of snapshots, not a to-do), Flight "2 zones" (a to-do). Rule: a badge is a count of things to do, or nothing. Journal → Ready count; Characters → none; Flight → zones with missing currents (keep).
- **The Moonlit tab icon is a crescent** in a column full of crescents that mean states. Use a different family (a star, a gem, the gift-box glyph the Rewards card already uses).
- **"No snapshot" empty state uses a 0 % gauge.** Zero percent is a measurement; no snapshot is an absence. Use the veiled moon there and keep the gauge for real zeros.
- **The Ready halo at 18 px** is three discs at 0.04 / 0.07 / 0.10 alpha; on the 2x render it is barely visible and on Night it vanishes at 1x. Ready vs Ready-on-other-job then rest on the ring alone. Raise to 0.08 / 0.14 / 0.20 at r ≤ 9 (the glyph sheet's 64 px version is fine).
- **Status bar redundancy.** "5,373 quests · showing 1,204 of 1,204" says the same number twice and the catalog total is trivia. "Showing 1,204" unfiltered, "Showing 212 of 1,204" filtered.
- **Localisation debt in the copy layer.** Twenty-odd `Prefix` / `Suffix` constants (`CharactersSnapshotPrefix`, `CharactersForgetQuestionPrefix` + name + `Suffix`, `CharactersAllowancesPrefix`…) are string concatenation; V2-19 will have to rewrite them all. Convert to `{0}` format strings now while the panes are being rebuilt.
- **Hold-Shift guard copy.** "Hold Shift and click" describes the gesture; "Hold Shift to confirm" says why.
- **Mark as unique is the only destructive-ish action without an undo toast.** The plan adds Restore in Settings › Data, which is three clicks away. A four-second "Marked unique · Undo" line in the pane is the standard.

## 3. Copy suggestions (current → proposed)

Voice rule stays as the spec says (calm, precise, short, nouns for labels, no exclamation marks). The changes below are about one-name-per-concept, plain words over borrowed ones, and value before metaphor.

| Where | Current | Proposed | Why |
|---|---|---|---|
| State | Ready | Ready | Keep; it is short and fits pills. Change the filters to match instead. |
| Filter | Available only / "Available now" (help, tour) | Ready now | One word for one state. |
| State | Ready on another job · chip "Other job" | Ready on {JOB} · chip "Ready · other job" | Names the job, as `ReadyOnJobFormat` already does. |
| State | Foreclosed · column "Locked by …" · help "eclipsed" | Locked out · column "Locked out by {quest}" | Plain English; "eclipsed" stays as the glyph's name in the legend only. |
| State | Done this cycle · chip "Done cycle" · column "Done today …" | Done today / Done this week (by reset) · chip "Done for now" | "Done cycle" is not English; the column already says today. |
| State | Unknown · "veiled" · "Veiled · achi…" | Not checked · column "Not checked: {reason}" | Honest about what happened; "veiled" stays in the legend. |
| State pill | Blocked | Blocked · {blocker} (always paired) | The state alone is the gripe. |
| Column | Next step | Status | It shows blockers, steps, dates and reasons; "next step" is only true for two states. |
| Toolbar | Presets | Quick views | "Preset" implies a saved filter set. |
| Segment | Features / Level band / Stalled | Unlocks / My level / Stalled | "Features" is what the game calls duties and systems? No: players say unlocks. "Level band" is a chart word. |
| Status bar | "5,373 quests · showing 1,204 of 1,204 · live" | "Showing 1,204 · live" / "Showing 212 of 1,204 · live" | Drop the repeated number. |
| Detail | "Completed per client flags at 21:14" | "Live · checked 21:14" | No "client flags". |
| Detail | "Evaluated from snapshot taken {0}" | "From snapshot · {0}" | Shorter. |
| Detail | "No snapshot; state unknown" | "Log in to check this quest" | Says what to do. |
| Detail | "Select a quest in the table to see its requirements, rewards and path." | (replaced by the Tonight card; if kept) "Pick a quest to see what blocks it and what it gives." | Value, not anatomy. |
| Detail | "Nothing gates this quest." | "No requirements." | "Gates" is jargon. |
| Detail | "No quest lists this one as a previous quest." | "Unlocks nothing further." | Reads forwards. |
| Moonlit | static / community / curated / yours | (blank) / Community / Verified / Yours | "static" means nothing to a player; never in Eclipse. |
| Moonlit | "Not unique (hide)" | "Not unique: hide from Moonlit" | No parenthetical verbs. |
| Moonlit | "Restore shipped verdict" | "Undo my change" | Users do not know what "shipped" means. |
| Moonlit | "Obtained states need the live character." | "Log in to see what you already own." | Plain. |
| Compare | "Unlock value: 1 for any quest, +3 main scenario, +5 feature quest, +2 per unique reward. Higher first." | "Sorted: main scenario, then unlocks, then unique rewards, then the rest." | Drop the invented score. |
| Compare | "{0} is {1} ahead of {2}" | "{1} done on {0}, not on {2}" | Alts are not a race. |
| Browse mode | "No character snapshot: states, next steps and availability are not evaluated." | "Log in and Tsukimichi reads your journal. Until then, moons are veiled." | Says what will happen, uses the brand word once. |
| Tour | "Tsukimichi is the road you walk by moonlight: every quest is a step, and the moon fills as you complete it. This tour points at each part of the window. Nothing in it changes your game." | "Tsukimichi shows what you can pick up now, why a quest is locked, and which rewards exist nowhere else. Three steps; nothing in it changes your game." | Value first; keep the moon line for the Finish card. |
| Tour | "Four tabs" | "Where things live" | Step titles as outcomes, not counts. |
| Tour | "That is the road" | "That is the road" | Keep; the metaphor earns its place at the end. |
| Todo | "Nothing to do here" | "Nothing to pick up here" | Matches the Nearby copy ("Nothing to start in {0}"). |
| Todo section | "Feature quests here" | "Unlocks you can start here" | "Feature quest" is catalog vocabulary. |
| Search | "Search quests, rewards or ids" | "Search quests, rewards, NPCs" (once B3 lands; ids still work, nobody needs to be told) | Ids are a power-user path. |
| Tree | Unlisted → "Retired and hidden" (planned) | "Retired and hidden" | Agree with the plan. |
| Guard | "Hold Shift and click" | "Hold Shift to confirm" | Says why. |
| Help lede | "Six steps from an empty window to a plan for the evening." | "From an empty window to a plan for the evening." | The count will drift. |
| Chip | "States: −Completed, −Foreclosed +1" | "Hiding: Completed, Locked out +1" | The minus sign is not a word. |

## 4. Five ideas the plan lacks

**B1. Spoiler shield.** The tree, the table, the Path card, "Unlocks next", the MSQ chip and the Todo overlay all print the names of quests the player has not reached. FFXIV quest titles are spoilers by design (the 5.0 and 6.0 finales, the Dawntrail Krile arc the plan itself references). A story-first player, the audience for P5 "Before you continue", will not open a window that tells them the name of the next MSQ. Add a default-on "Hide names of main scenario quests ahead of me" that renders them as "Main scenario · Lv 87 · 3 ahead" until hovered-and-held, with the same mask in chat output and the overlay. This is table stakes in every companion app for a story RPG and it is cheap: a predicate over `IsMsq && sequence > current`.

**B2. A "Tonight" landing card** (issue 1 in full): Ready now, next MSQ, events running now, pins, stalled, nearby. It is the home screen the four tabs lack, it reuses the Todo overlay's row model, and it gives P7 and P11 a place to land without new chrome.

**B3. Search by NPC and zone, with a typed scope.** Search matches names, reward names and ids. The second gripe class ("why does this NPC not offer me anything") is answered by typing the NPC's name. Index `Giver` and zone names; show a scope chip on the result ("giver: Vorsaile Heuloix"). This overlaps P2 and gets most of its value without the context-menu hook.

**B4. Saved views (real presets).** Let the player name the current scope + filters + sort ("ARR side quests, ready, by level", "Alt unlocks") and pick it from the Quick views control. The three fixed toggles become the shipped defaults of the same mechanism. Every serious tracker (Achievement trackers, TODO-style journals, Destiny/WoW companion apps) ends up here; building the fixed control first and the saved one later means rebuilding the control.

**B5. A validation loop before 0.8.0 ships.** The plan has bug-hunter passes and a smoke checklist; it has no player in the loop. Two things a studio would insist on: (a) a five-player click-through of the HTML mockup with three tasks ("find something you can pick up", "say why Brotherhood of Ash is blocked", "find a unique minion you do not own"), timed, before T14; (b) opt-in, local-only usage counters (tab opens, filter uses, overlay clicks, tour completion) shown in Settings › Data so the owner can see which of the five ways to narrow the table anyone uses. No network, no telemetry upload; a JSON file the player can read.

## 5. Increment order: what I would change in 0.8.0

Current: T13 Chrome → T14 toolbar + tab strip + chips → T15 table (+ Cards) → T16 detail + empty states → T17 motion + typography + secondary panes (overlay, Nearby, Help).

Proposed:

| # | Scope | Why here |
|---|---|---|
| 1 | **T13 Chrome helpers** (unchanged) plus the outlined-text helper for overlays | Everything else needs it. |
| 2 | **Todo overlay and Nearby window on Chrome** (pulled out of T17), with the §2.9 overlay spec from issue 5 | The only surfaces used while playing; smallest code; highest daily exposure; no layout risk. |
| 3 | **T16 detail card stack + empty states + Tonight card**, with **P1 blocker line** pulled from 0.9.0 | Answers gripes 1 and 2; self-contained pane; the Model is untouched. Ship it before the toolbar so the "why" is visible a release earlier. |
| 4 | **T15 table** without Cards mode, with the Status column rules from issue 3 | Depends on Chrome only; clipper work needs the most testing, so give it its own slot. |
| 5 | **T14 toolbar + tab strip + chips + Night chrome** with "Follow Dalamud colours" instead of Classic layout, Quick views instead of the segmented control, scope chip | Highest structural risk (tutorial rects, keyboard nav, two-layout trap); least player value per hour; last. |
| 6 | **T17 motion + typography**, motion event-driven only, no idle pip, no Ctrl+1..4 / F | Additive; can trail. |

Cross-release: cut the "Cards 44 px" density value from T12 (0.7.0) since nothing renders it; move P6 "Unlock route for alts" from 1.0.0 to 0.9.0 (alt unlock lists are gripe #4 with 12 threads and the data is present) and push P8 "Patch of origin" to 1.0.0 in its place (it needs a curated seed table and answers a weaker gripe). Consider B1 (spoiler shield) for 0.9.0 ahead of P4: it protects the audience P5 is for.

## 6. What a AAA companion-app team would do differently

- **Design from the three player jobs, not the data model.** The tabs mirror the code (catalog, reward index, snapshot store, aether currents). A companion team names the tabs after intents: Tonight, Journal, Rewards, Characters; Flight becomes a Quick view of Journal.
- **Ship the glanceable surface first and treat the big window as the reference.** Console-adjacent teams learned this from second-screen apps: the thing on screen during play is the product; the big window is where you go to understand something.
- **One layout, themed by tokens.** Never two chrome paths.
- **A copy deck with a glossary and a term policy** before the strings are typed, and a lint that fails when two labels name one state.
- **Spoiler safety by default** for any story game.
- **Badges mean "to do".** Progress lives in gauges, never in badges.
- **No idle motion next to a game view.**
- **Cut density to two.** Compact and Comfortable; Cards is a mobile idiom.
- **Playtest the mockup before coding the mockup.** Five players, three tasks, a stopwatch.
- **Localisation-ready strings from day one** (format strings, no prefix/suffix concatenation); it is cheaper than the retrofit V2-19 is going to be.

## 7. What is right and should not be touched

The measured contrast work, the shape-before-colour rule for the eight states, the dashed Foreclosed stripe, the halo gauge as a second encoding, the requirement rows with the gap spelled out ("Level 24 · you are 31", "Trusted, needs Sworn"), "Step 3 of 7" for Accepted, chips as the clear button, the offending-filter chips in the empty state, the Shift guard on Mark as unique, the auto-hide of the overlay in combat and cutscenes, and the decision not to build navigation the game is about to ship natively. Keep all of it.

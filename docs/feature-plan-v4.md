# Tsukimichi feature plan v4: honest counts, room to breathe, the Moon Road

Status: **approved by the owner on 2026-09-30** ("Mockup looks good … Execute when ready"). Decisions: the rail is option (a), 64 px with labels; the release order is as written; the Moon Road direction is approved.

Three additions came with the approval: D11 multibox, L8 requirement visuals, and the V4 banner fallback with original category art.

**Complete (2026-09-30): 1.2.0, 1.3.0 and 1.4.0 are released.**

**Release numbering as shipped (2026-09-30):**
- **1.2.0** shipped D1–D11: honest counts, complete tags and multibox, together with L1–L2 (the pane floors and text helpers).
- **1.3** is the layout release: L3–L8.
- **1.4** is the Moon Road look: V1–V6. The art foundations (category banners, ornament atlas, banner resolver, node icons, orbit) are already on main but not drawn yet.


Sources:
- the owner's feedback of 2026-09-30, with screenshots of the quest table, Journal tree, rail, narrow panes and Flight tab;
- five investigations (reports in `docs/data/v4/`, design in `docs/design/moon-road-proposal.md`);
- the published mockup: https://claude.ai/artifact/2DFuDpsyL3VtF761AmX6rN.

Standing rules:
- Only what makes the plugin better for the player counts; Dalamud's official-repository rules don't apply.
- Localization is frozen: new text is English only, and nothing is spent on translations.
- Every release follows the usual gates: build with warnings as errors, the tests, the load check, a review, then tag and release.

## What the investigations found

| Problem the owner saw | Cause | Where it's fixed |
|---|---|---|
| Seventh Umbral Era 160/213 although finished; five "Close to Home" rows; unnamed "Main scenario quest (Lv 1)" rows | Start city, starting class and Grand Company alternatives are not modelled. 53 other-path quests count, and 4 read Ready. The MSQ position lands on the Archer "Close to Home", so the status bar, dashboard, Tonight, Todo and the IPC report it, and the spoiler shield hides 224 names, 30 of them real Newfound Adventure quests. | D1 |
| Flight: Churning Mists 4/5, Dravanian Forelands 3/5, Sea of Clouds 4/5 | The game's `AetherCurrent.Quest` column names the wrong quest for 5 currents (since 2017), and the count uses quest completion, not the attunement flags. ARR is listed only as Mor Dhona. | D2 |
| (found by the database pass) Allied-society dailies read "Done this cycle" forever | Rule 5 reads the completion bit as "done today". 566 dailies can never count as done. | D3 |
| (found) Finished chronicles stuck below 100 % | 14 hidden tracker rows and 92 repeatables are counted | D4, D5 |
| (found) Duty Finder hint silent for most duties | 177 duties have no unlock quest recorded | D8 |
| (found) My blues calls raids, trials, dungeons and systems "Other" | follows from D8, plus EventIconType 10 | D9 |
| (found) Moonlit shows achievements obtained that aren't | 475 achievement entries attached to the wrong quests, and aether currents counted twice | D10 |
| (found) Job column says "Multi" on 216 MSQ rows | "Any" is detected by the sheet's column count | D6 |
| Tree overlaps when narrow; My blues cut to "Blocked ·"; detail pane wraps a letter per line | No pane has a floor after a splitter drag; fixed Name column; SameLine + TextWrapped; no ellipsis helpers | L1–L6 |
| Chain names too long | The game's names repeat the parent and section | L3 (`JournalNames.Short`: median node width 183 → 94 px) |
| Rail wastes space | 156 px wide, about 70-80 % empty | L7 |
| "Looks basic, no flair" | — | V1–V6 (the Moon Road design) |

## Release 1.2.0 · Honest counts

A finished section reads x / x, and nothing reads Ready or done when it isn't.

- **D1. Other paths.**
  - A per-catalog `PathIndex` finds choice groups from the game data:
    - the start-city roots;
    - "Close to Home" sibling sets;
    - the ARR class starter/switcher tracks;
    - QuestLock groups;
    - Grand Company (the GC column plus the MSQ Company You Keep trio).
  - It propagates options forward (All joins intersect, Any joins union). `path_choices.json` supplies city and class labels and Call of the Wild's GC.
  - The character's branch comes from completed quests, else the start city, JobLevels or GrandCompany. A completed quest is never excluded.
  - Other-path quests become **Locked out** with the reason "Another city's start (Ul'dah)", under their real name (never masked). They leave done/total and move to a virtual **Other paths** node, hidden by default.
  - Choices not yet made count once and are tagged "Choose one of 3".
  - `IsSwitchableGrandCompanyQuest` is retired.
  - The MSQ position and spoiler shield follow; for Michiru the position becomes Return from the Void.
  - IPC changes are additive only.
  - Tests: per-city Seventh Umbral Era 160 / 160 / 161 after the GC choice and 164 / 164 / 165 before; the three roots; the 3/2/3 Close to Home sets; the 8 class tracks.
- **D2. Flight.**
  - Resolve each current's quest from `Quest.OtherReward = Aether Current`, via the listed quest, then its prerequisite, then an override for Thavnair.
  - Count done from the live attunement flags, falling back to quest completion for stored characters.
  - Show ARR as one entry covering its 17 zones, so "you are here" works in all of them.
  - DataGen uses the same resolver; regenerate `unique_quests.json`.
  - Test: every counted quest carries the aether-current reward, one to one (150), with the five corrected ids asserted.
- **D3. Dailies.** "Done this cycle" comes only from DailyDone. In totals, a daily counts as done once it has been done at least once.
- **D4. Tracker rows.** A refile rule stops the 14 hidden progress rows from being counted, tagged blue or listed as chain steps. Gate: no counted quest is unlisted by both the Lodestone and the wiki.
- **D5. Repeatables.** Leave them out of section totals and chain progress, so chains no longer point at them as "next".
- **D6. Small tags.**
  - The Job column reads "Any" for all-job quests.
  - 67923 What Lies Beneath moves to Palace of the Dead.
  - Refiled chain steps are ordered by the prerequisite graph.
  - Locked-out quests leave chain progress.
- **D7. Snapshot consistency suite.** On the test fixture, with DailyDone empty: nothing reads "done this cycle", and every section the player has finished counts x / x.

**Owner checks in game**, to confirm three points inferred from the data:
- another class's starter "Way of" quest can't be taken;
- Call of the Wild is offered only by your own company's officer;
- the Maelstrom's My Little Chocobo stays locked after switching company.

## Release 1.3.0 · Complete tags

- **D8. Duty unlocks.**
  - Derive them from the quest-script rule, which agrees with the wiki on 108 of 111 pairs.
  - Seed the remaining wiki pairs into `duty_unlocks.json` with evidence.
  - Drop entries that aren't Duty Finder content.
  - Test: every dungeon, trial and raid entry has an unlock quest (with an allowlist).
- **D9. My blues kinds.** Raids, trials and dungeons come from D8. EventIconType 10 quasi-quests default to System. "A Relic Reborn" is Job plus Trial. Test: no raid, trial or dungeon unlock is only "Other".
- **D11. Multibox.**
  - Every client runs its own copy of the plugin, and all share one config folder. Each client writes its live character's snapshot plus a small heartbeat file with a timestamp and a process id. The others watch the folder and reload changed snapshots, so a character logged in on another client shows as **live in another client**, with its current data, in the character switcher, the dashboard, Compare and Account view.
  - Writes that two clients can race get made safe:
    - every file is written through a temp file and an atomic rename;
    - shared files such as pins, overrides and settings are re-read and merged before each save, or split per character;
    - a stale heartbeat, more than 30 s old, means that client is gone.
  - Nothing reads another process's memory, and nothing sends input to another client.
  - Tests: concurrent-save merge, and heartbeat staleness.
- **D10. Moonlit achievements.**
  - DataGen skips per-weapon achievements and all-of-N achievements, and de-duplicates aether currents.
  - Title and achievement "obtained" reads the achievement state.
  - Regenerate the data.

## Release 1.4.0 · Room to breathe (layout)

Every narrow-width complaint is fixed before any new art.

- **L1. PaneSplit.** The body table becomes a splitter with enforced floors: tree 180, centre 320, detail 260 (logical px).
  - Widths are stored in logical units, and double-clicking a handle resets it.
  - Below its floor the tree snaps to a 56 px strip of icons.
  - The window minimum drops from 1,076 to about 925 px.
- **L2. Text helpers.** `EllipsisText`, `StatusText`, `LabelValue`, `TextFlow`/`WordWrap` (breaks between words, never mid-word) and `SameLineOrWrap`. Breakpoints live in `LayoutBudgets`, with tests.
- **L3. Journal tree.**
  - `RowFit` tiers: at ≥ 300 the full row; at 240 the pill and bar go; at 200 the count becomes a percent; at 180 the ring carries progress.
  - A cut name always shows its full name and path on hover.
  - `JournalNames.Short`: "Seventh Umbral Era", "Eden", "Hildibrand", "Allied Societies · ARR–EW", "Class & Job"; the full name goes in the tooltip.
- **L4. Quest table.**
  - A column-priority model: Name stretches, Status never loses its state word.
  - Columns hide in the order Rewards → Exp → Job → Level.
  - Below 360 px, rows become two-line.
- **L5. Detail pane.** Below 320: label over value, the moon above the title, meta chips that wrap whole, captions on their own line, wrapped chain and journal path, and the primary action as an icon button below 260.
- **L6. Other panes:**
  - My blues: two-line rows from 560 px; Flag and Reveal fold into "…".
  - Flight and Abandoned: status never cut.
  - Moonlit: the toolbar goes to two rows; Confidence and Kind hide before State.
  - Characters: stats ahead of names.
  - Tonight, Since you were away, What's new, empty states, Settings and the filter panel: nothing overlaps or runs off an edge.
- **L7. Rail.** 64 px, with a label under each icon, a crest on top, and the overall gauge, Help and Settings at its foot. It becomes a 44 px icon rail on windows under 1,040 px or by setting. The freed width goes to the tree, whose default grows from 240 to 300.
- **L8. Requirements you don't meet are unmistakable.**
  - The detail pane opens with a "Not yet" callout that names the blocker in one line. For example: "Not yet · level 56 (you're 52) and 1 previous quest".
  - Each unmet requirement line gets:
    - the Locked out / Blocked colour and an eclipse ✕ mark;
    - a gap meter for levels and ranks, e.g. "52 → 56";
    - a jump button to the quest, class or unlock that clears it.
  - Met lines go quiet: a small check and dimmed text.
  - The quest table's status and the path chart use the same marks.
  - Ready never shows while a requirement is unmet. A new test asserts this for every state.

## Release 1.5.0 · The Moon Road (look and character)

Design proposal: `docs/design/moon-road-proposal.md`. Concept: the road of light the moon lays on night water. Gold means "walked" or "act now"; brass ornament is used sparingly; the game's own art and fonts give it character.

- **V1. Palette and Flair.** Six new tokens (Abyss, NightTop, Gilt, GiltHigh, Tide, TideDeep), with contrast checked. A **Flair** setting (Full / Quiet / Plain) controls how much ornament shows. The high-contrast palette keeps working.
- **V2. Journal icons.**
  - Official icons: the section markers, allied-society emblems, class/job/role, Grand Company and content tiles, and the expansion icons for MSQ and regional nodes.
  - Each sits inside an **orbit ring**: the gold arc is completion and a moon bead marks its tip; Ready is a gold dot.
  - 17 original glyphs fill the gaps (Chronicles, Hildibrand, festivals, Other, Removed and so on) from one ornament atlas.
  - Game icons are read at runtime, never shipped.
- **V3. Game fonts.** TrumpGothic for section headings and rail labels, Jupiter for titles, MiedingerMid for numbers; body text stays Dalamud's font. Switchable off.
- **V4. Detail hero and open sections.**
  - The quest's journal banner in a corner-marked frame, with the state moon rising on its edge; it falls back to the duty banner, then a drawn night sky.
  - **Banner fallback, so every quest has art.** In order:
    1. the quest's own journal banner;
    2. the banner of the nearest quest in the same chain or journal genre;
    3. the duty banner of the content it unlocks;
    4. the loading-screen art of the zone where it starts;
    5. **original category art** that ships with the plugin: illustrated night-sky banners in the Moon Road style, one per category (each expansion's main scenario, sidequest regions, allied societies, class and job, Grand Company, seasonal events, Chronicles, Hildibrand, relics, Other).

    Steps 1–4 are official art read at runtime from the player's own install. Art downloaded from the web is never shipped, because it belongs to Square Enix.
  - Meta chips.
  - Sections become open headings (star, small caps, fading rule) instead of boxed cards.
  - Moon-road dividers between tree blocks.
- **V5. Panes.**
  - Flight: the zone's loading art as a banner, and a bead ring you can count currents on.
  - Moonlit: a gallery of reward icons.
  - Characters: a framed header and a grid of section rings.
  - Settings: a "Look" group with previews.
- **V6. Motion** (Could): moonrise on selection, orbit fill, a glint on completion. All instant under Reduce motion.

## Decisions (settled 2026-09-30)

1. **The rail.** The designers and the UI audit disagree:
   - **(a) 64 px rail with labels under the icons**, the designers' choice and my recommendation. It frees 72 px, keeps names visible, and moves the overall gauge, Help and Settings to its foot. It shrinks to 44 px icons on small windows.
   - **(b) 44 px icon-only rail** with labels on hover, expandable to today's 136 px. It frees 105 px, but its expanded state brings the empty space back.
2. **Approved, in this order:** 1.2 data, then 1.3 tags, then 1.4 layout, then 1.5 look. The data and layout work touch different files, so they can be built side by side and released in that order.
3. **The Moon Road direction** is approved as shown in the mockup.

The detailed reports are in `docs/data/v4/`:
- `other-paths.md`
- `flight-currents.md`
- `tagging-audit.md`
- `ui-audit.md`
- `journal-short-names.tsv`
- `duty-unlock-diff.txt`

The design is in `docs/design/moon-road-proposal.md`, `docs/design/mockups/moon-road.html` and `docs/design/moon-road/ornaments/`.

## Follow-ups after 1.4

- **Icon lookups:** seven older `GetFromGameIcon(...).GetWrapOrEmpty()` call sites (DetailPane reward icons, FlightPane, MoonlitPane table, RewardTooltip, TablePane job and reward icons, CharactersPane:1118) would throw on a missing game icon. No icon is missing in the 2026.09.15 data. Move them to `TryGetFromGameIcon` with a drawn fallback.
- **Casing:** `PathChart.cs:201` upper-cases with `CurrentCulture` (pre-1.4). Route it through `HeadingCase`.
- **Owner checks in game:**
  - the Moon Road look at Flair Full, Quiet and Plain, and with the high-contrast palette;
  - multibox with two clients (docs/multibox.md);
  - achievement and title state after a character switch;
  - the three other-path inferences (starter "Way of", Call of the Wild, My Little Chocobo after switching company);
  - the 25 wiki-only prerequisites (allowlist until 1.5.0).

# UI smoke checklist (T4.2–T4.4)

> Historical: kept because older design and review documents link here. It is no longer maintained, and no new smoke-test pages are written.

Manual, in game, with the repo folder added as a dev plugin location. One line per feature: what to do, what to expect.
"Snapshot" below means a stored character file under `<config>/characters/`.

Round 3 additions (toolbar icon buttons, the Display sliders, quest banners, the engagement pass) are folded into the sections below; the "Display" subsection under Filter panel is new. Regions the interactive tutorial highlights are recorded every frame under these keys: toolbar, search, filtersButton, chips, character, sync, helpButton, settingsButton, tabs, tree, table, detail, detail.requirements, detail.path, detail.giver, statusBar, filterPanel (only while open), moonlit.kinds, moonlit.table, characters.list, characters.dashboard.

## Window shell
- `/tsukimichi` → the main window "Tsukimichi" toggles; default size 1100×700 (scaled), cannot shrink below 800×500.
- Dalamud plugin installer → "Open" on Tsukimichi → the main window opens (OpenMainUi hook).
- While the catalog builds → the window shows "Loading catalog" with animated dots, nothing else.
- Force a catalog failure (rename the game's `sqpack` is not practical; simulate by breaking `LuminaCatalogLoader` in a dev build) → "Catalog unavailable" in Eclipse, the exception text, a Retry button; Retry shows "Loading catalog" again and then the normal window.
- Toolbar left to right: search box with hint "Search quests, rewards or ids", an × clear button (disabled when empty), a Filters button (highlighted when the panel is open), the Character combo, the active-filter chip strip (empty when nothing is engaged), then a right-aligned block: the sync moon glyph and three square icon buttons — Help (question circle), Tutorial (graduation cap), Settings (cog).
- Hover each toolbar control (search box, ×, Filters, Character combo, sync glyph, the three icon buttons) → a one-line tooltip explains it ("Help", "Tour: …", "Settings").
- Help button → the help window toggles (same as `/tsukimichi help`); Settings button → the config window toggles (same as the plugin installer's cog); Tutorial button → the interactive tutorial starts over the main window. Before the plugin has wired them (never, in a normal load) the buttons are disabled with the tooltip "Not available yet".
- Status bar at the bottom reads `N,NNN quests · showing R of T · live · vX.Y.Z` while logged in; `snapshot HH:mm` when viewing a stored character; `no snapshot` before any capture. A tiny filling moon leads the line: its lit fraction is the overall completed/total of the catalog (full when everything is done, new before any snapshot).
- The window title bar keeps Dalamud's font size whatever the UI scale slider says; only the content scales.

## Toolbar
- Type in the search box → the table narrows about 150 ms after the last keystroke, not per keystroke; the status bar "showing" count follows.
- Click × (or the "Search" chip) → search clears, the table restores on the next frame with no 150 ms wait, the "Search" chip disappears; the config is not written for that (search is not persisted).
- Filters button → the filter panel appears above the tree in the left column; click again → hidden. The chips on the toolbar row stay visible either way.
- Toggle Hide completed / Available now on and off → chips appear and vanish inside the toolbar strip between the Character combo and the sync glyph; nothing below the toolbar moves. Too many chips are clipped at the strip's right edge, never wrapped.
- Character combo, logged in → the live character is listed with a leading ● and no age; stored characters show "Name@World · 3 h ago"; choosing one switches the whole window to that snapshot.
- Sync glyph → full moon (gold) while live and the poller is healthy, tooltip "Live"; veiled moon while viewing a snapshot, tooltip "Snapshot from HH:mm"; veiled with "Game reads paused…" while the poller backs off.
- Log out → banner "Snapshot: Name@World, HH:mm" appears in Dusk under the toolbar; the glyph goes veiled.
- Fresh install with no character → banner "No character snapshot: …", "Hide completed", "Available now" and "Seasonal active only" are disabled with tooltip "Needs a character snapshot", every glyph in the table is veiled, the Next step column is greyed.

## Filter panel and chips
- Hide completed → Completed and Foreclosed rows vanish; a "Hide completed" chip appears; clicking the chip clears it.
- Hide completed → Overrides → pick a category → set Off → that category's completed quests show again while others stay hidden; the chip remains.
- Available now → only Ready / Ready on another job / Accepted rows remain.
- Advanced → States: untick Completed → completed rows vanish; a "States: −Completed" chip appears; untick Foreclosed too → "States: −Completed, −Foreclosed"; untick five → three names then "+2"; chip click restores all eight.
- Hover every checkbox, combo, slider and the Overrides / Reset buttons in the filter panel → each has a short tooltip (disabled ones say "Needs a character snapshot").
- "Pinned first" (top of the panel, on by default) → pinned rows sit at the top of the table whatever the sort; untick → they sort in place; the choice survives a plugin reload.
- Advanced → Expansions: tick Heavensward → only HW rows; the chip reads "Expansion".
- Advanced → Level range: drag to 50–60 → rows outside vanish; the chip reads "Lv 50–60"; dragging the max to 100 means unbounded.
- Advanced → Job category: pick DoH → only crafter-restricted quests; "Current job only" is disabled without a snapshot and otherwise limits to quests restricted to exactly the current job.
- Advanced → Reward kinds: set Mount to Only → only mount-rewarding quests; set Item to Hidden → quests with item rewards vanish; chip reads "Reward kinds".
- Repeatable only / Seasonal active only / Include removed / Pinned only → each narrows (or widens, for Include removed) and adds its chip except Include removed.
- Reset (panel) or Reset filters (empty state) → every filter and the search clear at once.
- Filters survive a plugin reload (saved 1 s after the last change into `Settings.Filters`).
- Make the query empty (e.g. Pinned only with no pins) → the table area shows "Nothing matches", the offending filter names as bullets, and a "Reset filters" button; when only a combination is to blame the text says so.
- Select a genre with no quests under the current filters but quests in scope → "Nothing matches"; select a scope with zero quests → a large veiled moon centred in the table area with "This node has no quests." in Dusk beneath it.

### Display
- Bottom of the filter panel, under "Display": a "UI scale" slider (0.90×–1.60×, default 1.15×) and an "Icon scale" slider (0.80×–2.00×, default 1.25×), each with a tooltip, and a "Default sizes" button (disabled while both are at their defaults).
- Fresh config → text, moons, reward icons and column widths in the main window are visibly larger (about 15–25 %) than Dalamud's global scale alone; the rest of Dalamud and the other plugin windows are unchanged.
- Drag "UI scale" → text in the toolbar, tree, table, detail pane, tooltips, the table's context menu, the Overrides / job popups and the Forget-character modal all grow together on the next frame; table rows get taller with the text (no clipped rows, the list clipper still scrolls smoothly); the three columns widen; nothing overlaps.
- Drag "Icon scale" → state moons (table, tree, detail header, path chain, requirement marks, status bar), reward icons, job icons, the reward tooltip's big icon and the detail banner's moon grow while the text stays; rows grow only when an icon gets taller than the text and their text stays vertically centred.
- Extremes: 0.90× / 0.80× still readable; 1.60× / 2.00× still fits at the 800×500 minimum window with no overlapping toolbar controls (the chip strip shrinks first).
- Both values survive a plugin reload (saved 1 s after the last change into `Settings.UiScale` / `Settings.IconScale`); "Default sizes" restores 1.15× / 1.25× at once. The Config window (other agent) may expose the same two values; changing them there is reflected in the panel's sliders next frame.
- Hand-edit the config with `"UiScale": 99` or `NaN` → the window uses the clamped or default value, no error in `/xllog`.

## Journal tree
- Left column tabs Journal / Moonlit / Characters → Moonlit and Characters show "Coming in the next merge"; Journal shows the tree.
- Tree order: "All quests", then each Section → Category → Genre, then "Unlock quests", then "Removed from the game" only when Show removed quests is on in the config.
- Refiling (0.6.1): with Settings › Display › Journal filing on Refiled, Class & Job › Gladiator Quests opens with "So You Want to Be a Gladiator", Kugane Sidequests holds "Leves of Kugane", The Forbidden Land, Eureka holds the three "And We Shall Call It…" quests, and each of them shows a "Filed under … (rule N: …)" or "(curated override)" line under the journal path in the detail pane. Switching to Legacy rebuilds the catalog: those quests move back to "Removed from the game", the line disappears, and the tree totals return to the 0.6.0 numbers; switching back restores them.
- A category with a single genre shows as one leaf named after the category (no arrow); clicking it scopes the table to that genre and the leaf highlights; hovering shows "Section › Category › Genre". A section whose only category folded this way is itself one leaf. No node anywhere expands to a single child.
- Every node shows a small filling moon *before* its name (in the label slot, after the arrow) and `done/total` in Dusk at the right edge; the moon's lit fraction matches done/total; counts match the in-game journal's Completed tab for one category.
- Top-level nodes ("All quests", each Section, "Unlock quests", "Removed from the game") are drawn slightly bolder than their children; a top-level node whose done equals total (e.g. a finished section, or "All quests" on a completed character) has its name tinted Moon gold. Children never tint.
- Long node names are clipped before the count, never drawn over it; hovering, clicking and the selection highlight behave exactly as before (the label is painted over an unlabelled tree item).
- Clicking a node name selects it (highlighted) and scopes the table; clicking the arrow only expands; double-click expands too.
- "All quests" → the table shows the whole catalog (removed quests only when Include removed is on); "Unlock quests" → only derived feature quests, quasi-quests such as "Leves of Kugane" included, never a removed one.
- "Removed from the game" → exactly the retired rows (the old A Realm Reborn story, But I Hardly Noah, the three 3.05 sidequests, …); an undone one reads "Locked out · removed from the game", a completed one "Completed"; its detail pane says "Removed from the game in patch 6.3" where the curated note names the patch.
- Moonlit → "Show in Journal" on a quest while Hide completed / Available now / Pinned only / a State filter is on → the Journal opens on its genre with those filters cleared (their chips gone) and the row selected and visible. For a removed quest → Include removed turns on and the "Removed from the game" node appears selected even when Show removed quests is off in the config.

## Quest table
- Columns: state glyph, Name, Lv, Job, Next step, Exp, Rewards; headers can be reordered, hidden (right-click header) and resized; Name and Next step stretch; hovering a header shows a tooltip naming what the column holds.
- Sorting: on first open (fresh imgui.ini) the table is in journal order with no sort arrow on any header, the glyph column included. Click Name, Lv, Exp or the glyph header → rows re-sort; click again → descending; a third click returns to journal order.
- Sort survives a plugin reload (saved 1 s after the change into `Settings.SortColumn` / `SortDescending`, like the filters).
- Scrolling through 5,000+ rows stays smooth (list clipper); the frame counter in `/xlstats` shows no growth in allocations while idle over the table.
- Row click → the detail pane shows that quest; double-click an accepted or completed quest → the in-game journal opens on it; double-click any other quest → it is only selected (the game journal has no page for it).
- Select a quest from another pane (Path chain in the detail pane, Moonlit "Show in Journal") repeatedly → the table scrolls to the row every time, not every other time; clicking a row in the table never scrolls it.
- Pinned rows show a small gold dot at the left edge of the glyph column.
- Rows alternate between plain and a faint Veil tint (zebra); the tint follows the rows when sorting and scrolling.
- Ready rows carry a thin Moon-gold stripe on the row's left edge, Accepted rows a Silver one, every other state none; in browse mode (no snapshot) no row has a stripe.
- Next step column: the first word in Silver, the rest in Dusk ("Level 50, you are 43" → "Level" bright); greyed as a whole in browse mode.
- Hover a quest name (the Name cell only, not the reward icons) → a tooltip with the quest's journal banner about 240 px wide when the quest has one (Close to Home, The Ultimate Weapon do; most side quests do not, and then no image), the name, the genre, and "ARR · Lv 1"-style expansion and level. Hovering the reward icons still shows the reward tooltip, never both at once.
- Rewards column shows up to four icons (scaled by Icon scale); hovering an icon shows the blown-up tooltip: a large icon (56 px × Icon scale), the name in bold, the kind with "×N" for stacks, and for items "iLv N · Category" plus the item description; emotes and actions show their description. No boxed private-use glyphs or stray hyphens in the text.
- Right-click a row → menu: Pin/Unpin, Flag on map (disabled when the issuer has no map), Open journal (disabled unless accepted or completed, tooltip explains), Copy name, Copy coordinates (disabled without a map), Show path, Link in chat, and "Quest Map graph" only when the QuestMap plugin is installed and loaded.
- Copy coordinates → the clipboard holds "Place (x.x, y.y)", pasteable into chat.
- Pin → the dot appears immediately and `user/pins.json` gains the row id within about a second; Unpin removes it; pins are per character. Without a character (browse mode) the Pin item is disabled and nothing is written under content id 0.
- Config → Delete all data → `user/pins.json` and `user/overrides.json` are gone and stay gone: pinning afterwards writes only the new pin, and Moonlit quests marked "not unique" reappear at once. Characters → Forget → that character's pins are dropped from `user/pins.json` too.
- Copy name → the clipboard holds the quest name.
- Show path → the detail pane scrolls so the Path header sits at the top and the header plus its rule glow gold for about 1.5 s (visible even when the whole pane already fits without scrolling).
- Link in chat → one chat line: a clickable quest link followed by a clickable map link with place name and coordinates.

## Detail pane
- No selection → a large veiled moon centred in the pane with "Select a quest in the table to see its requirements, rewards and path." in Dusk beneath it; a selection that is not in the catalog shows the same moon with "Quest not in catalog".
- Header, quest with a journal banner (Close to Home, The Ultimate Weapon, most MSQ): the banner fills the pane's width keeping its aspect (no stretching, capped in height), a Night gradient strip covers its lower part with the quest name in Silver bottom-left (wrapping to a second line for long names) and the large state moon at the strip's right; resizing the right column rescales the banner. While the texture loads (first frame or two) the card below is shown instead, then the banner replaces it.
- Header, quest without a banner (icon 0, most side quests): a raised Night card with the large state moon beside the name.
- Under either header: "Genre › Category", then "Expansion · Lv N · Job category", then the state name in its token colour plus "Ready on XXX" or "Accepted, step N" when applicable, and "Pinned" in gold when pinned.
- Section headers (Requirements, Rewards, Moonlit, Path, Giver) each start with a small FontAwesome icon in Dusk (tasks, gift, moon, route, map marker), the title, and a Dusk rule; "Show path" turns the Path icon, title and rule gold for about 1.5 s.
- Requirements: one line per gate with a small full moon (met) or new moon (unmet) instead of ✓/✗ (hover: "Met" / "Not met"), the gate name, and the detail clause ("Trusted, needs Sworn"); the next step is marked ▶ and drawn in gold; without a snapshot the section explains that requirements need a snapshot.
- Rewards: icon, name (with ×N for stacks) and the kind in Dusk; hovering anywhere on a reward row shows the same blown-up tooltip as the table; no obtained badges are shown yet.
- Moonlit: a quest with shipped unique entries reads "Listed in Moonlit treasures."; one without reads "Not listed…" with "Mark as unique…" → a popup with a note field and "Mark as unique" adds it (Moonlit tab shows it under source "yours"); a quest marked not unique from the Moonlit tab reads "Marked not unique by you" with Restore, and Restore brings it back in Moonlit at once.
- Path: grouped under an expansion header each ("A Realm Reborn", "Heavensward", …); runs of two or more consecutive completed steps fold into "▸ N completed steps" (click to unfold, click again to fold; the choice holds until another quest is selected); incomplete, accepted, ready and the selected quest are always listed; every visible moon is joined by a line that is Moon gold (slightly thicker) below a completed step and thin Dusk below any other, so the walked part of the road is lit and the rest dark; each name is clickable and selects that quest; a quest with no prerequisites reads "This quest starts its own path."
- Unlocks next (under the chain): the quests that list the selected one as a previous quest, each with its moon, clickable; at most eight then "and N more"; none reads "No quest lists this one as a previous quest."
- Giver: NPC name, "Region › Place" and "(x.x, y.y)"; Flag map opens the map with a flag at the NPC (compare against the journal's own map); Open journal opens the in-game journal (disabled with a tooltip unless the quest is accepted or completed); Link in chat prints the link line; Copy coordinates puts "Place (x.x, y.y)" on the clipboard.
- Provenance line at the bottom: "Completed per client flags at HH:mm" for completed quests, "Evaluated from snapshot taken HH:mm" otherwise, "No snapshot; state unknown" without a character.
- Complete a quest in game → within 2 s the row's glyph flips to full, the detail pane's state and provenance update, the tree counts increment.

## Command
- `/tsukimichi` → toggles the main window; `/tsuki` does the same with every sub-command (`/tsuki config`, `/tsuki close to home`); `/xlhelp` lists `/tsukimichi` once and not the alias.
- `/tsukimichi help` → the help window (the main window until one exists).
- `/tsukimichi glyphs` → the glyph sheet window.
- `/tsukimichi close to home` → the window opens with "close to home" in the search box and chat shows up to five quest links (each with a map link when available); more than five adds "and N more"; no match prints "No quests match."
- `/tsukimichi search 65576` → same as above searching by id.
- `/tsukimichi <text>` before the catalog is ready → chat says "The catalog is still loading."

## Unload
- Unload the plugin with unsaved pins or filter changes → `user/pins.json` and the config are written on dispose; no errors in `/xllog`.

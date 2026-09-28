# UI smoke checklist (T4.2–T4.4)

Manual, in game, with the repo folder added as a dev plugin location. One line per feature: what to do, what to expect.
"Snapshot" below means a stored character file under `<config>/characters/`.

## Window shell
- `/tsukimichi` → the main window "Tsukimichi" toggles; default size 1100×700 (scaled), cannot shrink below 800×500.
- Dalamud plugin installer → "Open" on Tsukimichi → the main window opens (OpenMainUi hook).
- While the catalog builds → the window shows "Loading catalog" with animated dots, nothing else.
- Force a catalog failure (rename the game's `sqpack` is not practical; simulate by breaking `LuminaCatalogLoader` in a dev build) → "Catalog unavailable" in Eclipse, the exception text, a Retry button; Retry shows "Loading catalog" again and then the normal window.
- Toolbar left to right: search box with hint "Search quests, rewards or ids", an × clear button (disabled when empty), a Filters button (highlighted when the panel is open), the Character combo, the active-filter chip strip (empty when nothing is engaged), and a moon glyph at the far right.
- Hover each toolbar control (search box, ×, Filters, Character combo, sync glyph) → a one-line tooltip explains it.
- Status bar at the bottom reads `N,NNN quests · showing R of T · live · vX.Y.Z` while logged in; `snapshot HH:mm` when viewing a stored character; `no snapshot` before any capture.

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
- Repeatable only / Seasonal active only / Include Unlisted / Pinned only → each narrows (or widens, for Unlisted) and adds its chip except Include Unlisted.
- Reset (panel) or Reset filters (empty state) → every filter and the search clear at once.
- Filters survive a plugin reload (saved 1 s after the last change into `Settings.Filters`).
- Make the query empty (e.g. Pinned only with no pins) → the table area shows "Nothing matches", the offending filter names as bullets, and a "Reset filters" button; when only a combination is to blame the text says so.
- Select a genre with no quests under the current filters but quests in scope → "Nothing matches"; select a scope with zero quests → "This node has no quests."

## Journal tree
- Left column tabs Journal / Moonlit / Characters → Moonlit and Characters show "Coming in the next merge"; Journal shows the tree.
- Tree order: "All quests", then each Section → Category → Genre, then "Feature Unlocks", then "Unlisted" only when Show Unlisted is on in the config.
- A category with a single genre shows as one leaf named after the category (no arrow); clicking it scopes the table to that genre and the leaf highlights; hovering shows "Section › Category › Genre". A section whose only category folded this way is itself one leaf. No node anywhere expands to a single child.
- Every node shows a small filling moon and `done/total` in Dusk at the right edge; the moon's lit fraction matches done/total; counts match the in-game journal's Completed tab for one category.
- Clicking a node name selects it (highlighted) and scopes the table; clicking the arrow only expands; double-click expands too.
- "All quests" → the table shows the whole catalog (Unlisted only when Include Unlisted is on); "Feature Unlocks" → only curated feature quests.
- Moonlit → "Show in Journal" on a quest while Hide completed / Available now / Pinned only / a State filter is on → the Journal opens on its genre with those filters cleared (their chips gone) and the row selected and visible. For an unlisted quest → Include Unlisted turns on and the "Unlisted" node appears selected even when Show Unlisted is off in the config.

## Quest table
- Columns: state glyph, Name, Lv, Job, Next step, Exp, Rewards; headers can be reordered, hidden (right-click header) and resized; Name and Next step stretch; hovering a header shows a tooltip naming what the column holds.
- Sorting: on first open (fresh imgui.ini) the table is in journal order with no sort arrow on any header, the glyph column included. Click Name, Lv, Exp or the glyph header → rows re-sort; click again → descending; a third click returns to journal order.
- Sort survives a plugin reload (saved 1 s after the change into `Settings.SortColumn` / `SortDescending`, like the filters).
- Scrolling through 5,000+ rows stays smooth (list clipper); the frame counter in `/xlstats` shows no growth in allocations while idle over the table.
- Row click → the detail pane shows that quest; double-click an accepted or completed quest → the in-game journal opens on it; double-click any other quest → it is only selected (the game journal has no page for it).
- Select a quest from another pane (Path chain in the detail pane, Moonlit "Show in Journal") repeatedly → the table scrolls to the row every time, not every other time; clicking a row in the table never scrolls it.
- Pinned rows show a small gold dot at the left edge of the glyph column.
- Rewards column shows up to four icons; hovering an icon shows the blown-up tooltip: a 64 px icon, the name in bold, the kind with "×N" for stacks, and for items "iLv N · Category" plus the item description; emotes and actions show their description. No boxed private-use glyphs or stray hyphens in the text.
- Right-click a row → menu: Pin/Unpin, Flag on map (disabled when the issuer has no map), Open journal (disabled unless accepted or completed, tooltip explains), Copy name, Copy coordinates (disabled without a map), Show path, Link in chat, and "Quest Map graph" only when the QuestMap plugin is installed and loaded.
- Copy coordinates → the clipboard holds "Place (x.x, y.y)", pasteable into chat.
- Pin → the dot appears immediately and `user/pins.json` gains the row id within about a second; Unpin removes it; pins are per character. Without a character (browse mode) the Pin item is disabled and nothing is written under content id 0.
- Config → Delete all data → `user/pins.json` and `user/overrides.json` are gone and stay gone: pinning afterwards writes only the new pin, and Moonlit quests marked "not unique" reappear at once. Characters → Forget → that character's pins are dropped from `user/pins.json` too.
- Copy name → the clipboard holds the quest name.
- Show path → the detail pane scrolls so the Path header sits at the top and the header plus its rule glow gold for about 1.5 s (visible even when the whole pane already fits without scrolling).
- Link in chat → one chat line: a clickable quest link followed by a clickable map link with place name and coordinates.

## Detail pane
- Header: quest name, a large state moon, "Genre › Category", then "Expansion · Lv N · Job category", then the state name in its token colour plus "Ready on XXX" or "Accepted, step N" when applicable, and "Pinned" in gold when pinned.
- Requirements: one line per gate with ✓ (gold) or ✗ (Eclipse), the gate name, and the detail clause ("Trusted, needs Sworn"); the next step is marked ▶ and drawn in gold; without a snapshot the section explains that requirements need a snapshot.
- Rewards: icon, name (with ×N for stacks) and the kind in Dusk; hovering anywhere on a reward row shows the same blown-up tooltip as the table; no obtained badges are shown yet.
- Moonlit: a quest with shipped unique entries reads "Listed in Moonlit treasures."; one without reads "Not listed…" with "Mark as unique…" → a popup with a note field and "Mark as unique" adds it (Moonlit tab shows it under source "yours"); a quest marked not unique from the Moonlit tab reads "Marked not unique by you" with Restore, and Restore brings it back in Moonlit at once.
- Path: grouped under an expansion header each ("A Realm Reborn", "Heavensward", …); runs of two or more consecutive completed steps fold into "▸ N completed steps" (click to unfold, click again to fold; the choice holds until another quest is selected); incomplete, accepted, ready and the selected quest are always listed; every visible moon is joined by a thin Dusk line; each name is clickable and selects that quest; a quest with no prerequisites reads "This quest starts its own path."
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

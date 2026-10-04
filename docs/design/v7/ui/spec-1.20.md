# 1.20 "Before Evercold": the shield, the prep card and the portrait pack

This spec designs the UI for the 1.20.0 rows of `docs/feature-plan-v7.md`:

| Row | Surface |
|---|---|
| **N6** | A wider spoiler shield: places, duties, rewards and people past your story point, everywhere they print, and its setting |
| **N7** | The Before Evercold card: one per character, in Tonight and the Characters dashboard |
| **F4** | The opt-in portrait pack: the Settings row, the confirmation, progress, errors, Remove pack and the update offer |

The behaviour follows ideas 3 and 4 of `docs/research/plan-v7/feature-ideas.md` and tiers 2 and P4 of `docs/research/plan-v7/giver-portraits.md`, with decision 8 of the plan (an opt-in download from Tsukimichi's own GitHub release, with confirmation). It builds on the 1.15 portrait spec (`spec-1.15.md`: the plate, the framing rule, the fallbacks, the masked-quest rule) and the 1.18 and 1.19 colour language. Where this spec changes a research detail, the Decisions section says so.

**Revision 2** applies the realism supervisor's first review (CHANGES: six major findings, eleven minor):
1. The pack row has one fixed height in every state (F4).
2. A ticked prep line keeps its height: "you said so" takes the buttons' own slot (N7).
3. The prep checkbox gets a sunken fill and a Secondary outline, which passes 3:1 (N7).
4. Remove pack uses the shipped `Chrome.HoldButton` unchanged: the Moon arc, and the countdown under Reduce motion (F4).
5. Placeholders get a layout rule: non-breaking locators, single-line slots with an ellipsis, multi-line slots sized for the longer string, and composite strings in Secondary as a whole (N6).
6. A second looks board covers every new part on every palette (`looks-new-1.20.png`).

**Revision 3** applies the second review (CHANGES: one major finding). The confirmation no longer uses ImGui's modal dim, which covers the whole game screen, fades on its own clock and ignores the plugin's colours. Tsukimichi draws its own scrim over the Settings window only and blocks Settings' input itself (F4, "The confirmation"). Five minor findings and the nits are fixed in the same pass.

**The principles:**
- **Nothing new floats.** The shield changes words, not layout. The prep card is a card inside Tonight and the Characters dashboard. The pack is one Settings row, plus a confirmation window that opens only on a click.
- **Static layout.** A placeholder takes the place of the name it hides, at the same size. The prep card never reorders while you look at it. The pack row keeps its height through every state.
- **Plain words first.** Placeholders are words ("Dawntrail area 6", "Dungeon (Lv 97)"), never a glyph, a blur or a redaction bar. Errors say what happened, what was kept and what to do next.
- **Local-only stays true.** The pack is the one thing Tsukimichi ever fetches. It never happens unless the player clicks, it says so before it happens, and it never checks online by itself.
- **Safety.** Hiding the prep card has Undo. Removing the pack asks first, at the Hold tier.

**Files**

| File | What it is |
|---|---|
| `spec-1.20.md` | This spec |
| `1.20/mock-1.20.html` | The 1.20 boards: `#shield20`, `#prep20`, `#pack20`, `#looks20`, `#looks20b`. Built by `1.20/mock-src/build.py` from the shared `mock.html` plus `1.20/mock-src/v720.css` and `v720.js`. No shared `mock-src` file changes. |
| `1.20/shield-1.20.png` | N6: a masked quest in the detail pane, table rows before and after, Settings › Spoilers, the hover and right-click, Route, Find by unlock, and the placeholder vocabulary |
| `1.20/prep-card-1.20.png` | N7: the card in Tonight, a stored alt in the Characters dashboard, a tick before and after, all done, the Undo toast, the way back, the line hover and the five lines' rules |
| `1.20/portrait-pack-1.20.png` | F4: the confirmation over Settings › General › Look, downloading, checking, installed, the update offer, three errors and Cancelled, Remove pack (mid-hold, and the Reduce-motion countdown), two pack faces beside Alphinaud's game-art face at 72 px, the hover, the status-bar note and the Privacy & trust line |
| `1.20/looks-1.20.png` | Every look (1 of 2): the prep card, a masked giver and the pack row at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer |
| `1.20/looks-new-1.20.png` | Every look (2 of 2), on all four palettes: pack photos beside a game-art face at 72, 64, 24 and 18 px; the hidden-reward tile; a pack error with the hint dot; the Remove pack popover at Plain; the compact confirmation over its dim at Quiet |
| `1.20/mock-src/render.py` | Renders the five PNGs with headless Chrome at 1560 px wide (long edge ≤ 1568) |
| `1.20/art/Enpc_1000590.png`, `Enpc_1003929.png` | Two Garland Tools NPC photos (Buscarron, Isembard; photos by Celes), for the mock only, to show what pack faces look like on the 1.15 plate |

**The mock's data.** The Dawntrail zone names (Heritage Found, Living Memory, Kozama'uka, Shaaloani), duty names and levels (Vanguard 97, Origenics 99, Alexandria 100, The Interphos 100) are real. Which quest unlocks which, the area numbers and the character's counts are illustrative. The two pack photos are real Garland Tools renders, cropped by the 1.15 framing rule (Buscarron: box 150, 113, 82; Isembard: 154, 117, 76, measured on a 4× zoom; eye line at 43%, chin at 82–83%).

---

## Colour language

1.20 adds no colour meaning. It uses what 1.16 to 1.19 defined and gated, plus one darker value of an existing token on Ishgard Snow.

| Meaning | Token | Where in 1.20 |
|---|---|---|
| Hidden by the shield | Secondary (`--mist`), in place of Text | every placeholder |
| A hidden reward's icon | the 1.15 moon disc on a 22 px quiet tile (`--sunk`, `--line` outline) | Rewards, Moonlit rows, reward tooltips |
| Done (the game says so) | the check in the card's ink, the line in Secondary and Tertiary | the prep card's done lines and its fold line |
| Finished, all of it | gold (`--goldtx` on Snow) | "Ready for Evercold" |
| A Settings problem | the 1.17 Settings hint: a 6 px amber dot (`--hint`) beside words in Text | the pack's errors |
| Hold to confirm | the shipped `Chrome.HoldButton`: a Moon arc along the outline over a line-tone track | Remove pack |
| A checkbox | Sunken fill, a 1.5 px Secondary outline (6.4:1 on Night Hover, 8.4 on Snow), the check in Text | the prep card's lines |

**Tertiary is the gated value.** Night's Tertiary is the plugin's `#8B94B3` (4.5:1 at its worst case), which the 1.20 renders use. The shared mock's older `#7C86A8` (4.3:1) is not used for 11 px lines.

**No copper anywhere in 1.20.** Copper means "it needs you" during a hand-off (1.18) or a full journal (1.19). Nothing in Before Evercold is urgent, and a failed download waits calmly in Settings.

**`--hint` on Ishgard Snow is new: `#8E6A1E`.** The 1.17 amber `#C9A866` is 2.3:1 on Snow's white card, under the 3:1 a dot needs. `#8E6A1E` is 4.96:1 on `#FFFFFF` and 4.75:1 on `#F9FAFC`. It is close to Snow's gold text `#755308` (1.41:1), but the two never share an element type: one is a dot in Settings, the other is a word in the panes. On the dark palettes `--hint` stays `#C9A866` (6.9:1 on Night raised, 6.9 on Dawn, 7.6 on Kugane).

---

## N6. A wider spoiler shield (`shield-1.20.png`)

### What it hides

Today the shield hides main scenario quest names past your position (plus a few revealed ahead) and journal artwork. From 1.20 it also hides the names of **places, duties, rewards and people** that belong to the story past your position. The rule is one function, used by every surface:

| Kind | Hidden when | Placeholder |
|---|---|---|
| Main scenario quest | unchanged | Main scenario quest (Lv 97) |
| Area or city | its expansion is past the one your story has reached, or every main scenario quest that first takes you there is hidden | **Dawntrail area 6** |
| Aetheryte | its area is hidden | **Dawntrail aetheryte · area 6** |
| Duty | its unlock quest is hidden, or its expansion is past yours | **Dungeon (Lv 97)**, **Trial (Lv 99)**, **Alliance raid (Lv 100)** (the duty's own content type and level) |
| Reward (item, mount, minion, emote, orchestrion roll, title, hairstyle) | the quest that gives it is hidden | **An item**, **A mount**, **An orchestrion roll** … The icon becomes the moon-disc tile. |
| Person (an NPC's name) | every quest they give or appear in is hidden | **Dawntrail character** |

**The area number** is the area's place in the expansion's map order (the game's own `TerritoryType` order for that expansion). It is the same number on every surface, so you can still group and sort by area without learning its name. It is never a count of what is left.

**People you have met keep their names.** Alphinaud still reads Alphinaud in a hidden Dawntrail quest, because earlier quests named him. Faces still follow 1.15: a masked quest never shows a face, even a known one (A6.4); it shows the silhouette or the initials.

**The Ahead slider applies.** "Quests ahead to reveal" (0 to 10, default 3) also reveals the places, duties, rewards and people those quests introduce.

### How a placeholder looks

- **The same size, weight and slot as the name.** Only the colour changes: Secondary instead of Text. That includes masked main scenario titles, which move from Text to Secondary in the hero, the table and every list.
- **Locators never break:** "area 6", "Lv 97" and "Patch 7.5" keep a non-breaking space.
- **Nothing moves when a name is revealed or hidden.** Placeholders are often longer than the names they hide ("Dawntrail area 6" for "Shaaloani"), so:
  - single-line slots (table cells, search results, Route and Next stops, the Plain giver line) never wrap: they end in a real "…", and the hover shows the whole string. Where a slot holds a name and a place, the place is shortened first to its locator ("area 6", as Route does), then the name is truncated, so the locator is never the part cut off;
  - multi-line slots (the prep card's details, tooltips, the Unlocks section) are wrapped for the longer of the name and its placeholder, so a reveal never changes their line count.
- **A placeholder keeps the weight of the name it replaces** (medium in search results and titles, regular in lines).
- **A string that holds a placeholder is Secondary as a whole:** "Flying in Dawntrail area 6", not "Flying in" in Text and the rest in Secondary.
- **No glyph, no blur, no redaction bar, no italics** (the game fonts have none). The word is the signal.
- **Kind icons stay** where they are generic: the aetheryte symbol (060453), the Duty Finder tile (000046), the duty badges (C7). They say what kind of thing it is, not which.
- **Reward icons do not stay.** An item's own icon can spoil it, so a hidden reward shows the 1.15 moon disc (`1.15/silhouettes/moon-disc.svg`) on a 22 px tile in `--sunk` with a `--line` outline. On Ishgard Snow the disc is drawn in `#56607C`, the 1.16 fallback ink.

**The hover** (HoverHint) on any placeholder:
- "Hidden by the spoiler shield" (Text, semibold)
- "It's from the story past yours." (Secondary)
- "Right-click to reveal it for this session." (Tertiary)

**The right-click menu** on any placeholder:
- **Reveal this name** · "this session" (Tertiary, trailing)
- **Reveal names in this quest**, when the placeholder sits inside one quest's context
- **Open on Garland Tools…** and the other "Open on…" links ask first, as 1.8's spoiler question does for masked quests (`LinkConfirmWindow`), and name the thing by its placeholder.

**In the detail pane** of a masked quest, the existing note ("Names hidden: this quest is further ahead than you are.") keeps its place under the status line. Its button becomes **Reveal names in this quest**: the quest's name, its giver, place, duty, rewards and unlocks, for this session.

### Where it applies

| Surface | What follows the shield |
|---|---|
| Detail pane | the hero's place line; the Giver card (name, place; the plate per 1.15); Requirements; How you'll clear it (duty names, not the badges); Rewards (names and icons); Unlocks; Hand in |
| Journal table | Name (as today); the Zone, Giver and Unlocks columns |
| Journal tree | a node named after a hidden area takes that area's placeholder |
| Path | node labels and their hovers |
| Route window, Next stops, Tonight | stop names, places and the 24 px avatar (silhouette for a hidden person) |
| Characters dashboard | the Duties board (N4: duty names and their unlock quests), Seasonal and Moonlit rows |
| Moonlit pane and reward tooltips | reward names and icons |
| Toolbar search and Find by unlock (K3) | a hidden name matches only its placeholder, as masked quests do today. Typing a name from ahead finds nothing, and no line hints that something was hidden. |
| Copy for Discord, the table's Copy, chat lines | the placeholder |
| Wotsit | hidden names are not registered; they re-register when the mask's fingerprint changes, as quest names do today |
| IPC | unchanged: other plugins ask for data, not display text |

**Travel buttons to a hidden place are not shown.** The place is ahead of your story, so there is nothing to do there yet, and the game's map would name it. This follows 1.18's rule: hidden, never greyed.

### The setting (Settings › Spoilers)

One new switch sits under **Hide story names ahead**, indented as a sub-setting, like **Quests ahead to reveal**:

- **Also hide places, duties, rewards and people:** on by default.
- **Help:** "Areas, aetherytes, duties, quest rewards and characters from the story past yours read **Dawntrail area 6**, **Dungeon (Lv 97)**, **An orchestrion roll**, **Dawntrail character**. Unlocks, Path, Route, the Duties board, tooltips and search follow it."
- **Disabled** (with the existing reason line) while Hide story names ahead is off: the position it measures from is the same.
- **On upgrade** it takes the value of Hide story names ahead. A player who turned the shield off keeps everything shown.
- **The per-character override** (As above · Always shield · Show everything) covers both switches.
- **The count line** becomes "212 story names and 486 other names hidden for Michiru."

**Motion:** none. A reveal swaps the word on the next frame. A face that a reveal unmasks fades in over `ArtFade` (0.3 s), never under Reduce motion or at Plain (1.15 A7).

---

## N7. Before Evercold (`prep-card-1.20.png`)

### Where it lives

- **Tonight** (the detail column with no quest selected): a keyline card below the story line and the events line, above the pinned quests. Event cards ending soon stay above it, since they are dated and closer.
- **The Characters dashboard:** the same card for the viewed character, below the header. It works for any stored character, read from its last snapshot, with "As of the last login, 2 days ago" as its sub-line.

It shows from the day 1.20 installs until Evercold's launch day. The plugin knows that day from the 8.0 game data, or the early-access date shipped in curated data (22 January 2027, expected; it is rechecked after the Tokyo Fan Fest). On launch day the card goes away by itself, and N12's "New in 8.0" card takes its slot.

### Anatomy

| Part | Full | Quiet | Plain |
|---|---|---|---|
| Frame | the card (the kit's frame and corner marks), padding 11 × 14 | tonal card | a band header (22 px) over ledger lines |
| Title | "Before Evercold" in the Title face (16 px), × at the trailing end | semibold 13.5 px | 12.5 px in the band |
| Sub-line | "For Michiru · early access 22 Jan (expected)", 11.5 px Secondary | the same | the same |
| Line | a 14 px checkbox, an 18 px icon, the label (12.5 px Text, medium) over the detail (11.5 px Secondary), and quiet buttons | the same | the same |
| Fold | "Done: Job and role quests", 11 px Tertiary, one line under a hairline | the same | the same |

**The buttons sit at the trailing end in a card of 460 px or more,** in a fixed 150 px column, and under the line's words in a narrower card, in a fixed 24 px row (the looks matrices show the narrow form). The choice is made per card width, never per line, so lines never differ.

**The checkbox** is 14 px with a 3 px radius (2 px at Plain): a Sunken fill, a 1.5 px Secondary outline and the check in Text. The × has a 24 px target; its glyph stays 10 px.

### The five lines

| Line | Icon | Detail (example) | Buttons | Done when (the game says so) |
|---|---|---|---|---|
| **Finish the main story** | the MSQ marker (071201) | "14 quests left, through Patch 7.5" | Show next · Route | the last 7.x main scenario quest is complete |
| **Room in your journal** | the book glyph (1.15) | "27 of 30 slots used. Evercold brings new quests." | Make room (C9's popover) | 10 or more free slots (C9's count) |
| **Job and role quests** | the first job's icon (62100 + job) | "Your DRG, SGE and CUL have quests you can take now" | Show them (the Journal on those quests) | no job or role quest is Ready for this character |
| **Duties for the roulettes** | Duty Finder (000046) | "2 Dawntrail duties not unlocked yet" | Duties board (N4) | every 7.x dungeon, trial and raid is unlocked |
| **Flying in Dawntrail** | Fly (000122) | "2 areas left: Shaaloani, Dawntrail area 6" (N6 hides the second) | Route (K3's Route to unlock) | flying in every Dawntrail area |

- **Only the lines that apply.** A character still in Stormblood gets "The main story: You're in Stormblood: 588 quests to Evercold. No rush; the lines below matter first." and no Dawntrail lines. Lines are decided when the card is built, never while you look at it.
- **Names follow N6.** A hidden area in the flying line reads "Dawntrail area 6".
- **Wording that depends on Tokyo** (roulettes, Adventurer Activity) lives in the strings and is rechecked after 1 November. The duties line says "for the roulettes" until then.
- **Each line's hover** says why it is there and when it counts as done, then "Tick it yourself if …" in Tertiary.

### Check-offs

- **The checkbox ticks a line yourself.** "you said so" (the C3 wording, Tertiary 11 px) takes the buttons' own slot, the label turns Secondary, and the detail keeps its wrap, so the line keeps its height. Clicking again unticks it and brings the buttons back to the same slot. Ticks are per character, saved with the per-character settings.
- **A line the game says is done** gets the same check, without "you said so".
- **Nothing moves while you look.** A line you tick stays in its place, checked, until the card is next built (a new selection, a new session or a new snapshot). Then done lines fold into the one "Done: …" line, in the order of the table above.
- **Show what is left, not tallies.** The fold line names what is done; there is no "3 of 5".
- **All done:** the card keeps its title and sub-line, and its body is one line with the gold check: "Ready for Evercold. Nothing left on Michiru."

### Hide, with Undo

- **×** (in the title row, tooltip "Hide for this character") hides the card for that character, in Tonight and the dashboard, at once.
- **The Undo toast** (1.12's `UndoToast`, 8 s, the pointer pauses it): "Before Evercold hidden for Michiru · Undo". It floats above the status bar, so nothing else moves. The card's own slot closes on the click: that is the one reflow, and the player asked for it.
- **The way back:** the Characters dashboard keeps one quiet line in the card's place: "Before Evercold is hidden for Michiru." with **Show again**.
- **Safety tier:** a new `GuardedAction.HideEvercoldCard` at `SafetyTier.None` (one click, Undo follows), like Unpin.

**Motion:** the card leaves with `Leave` (0.12 s) and returns with `Rise` (0.16 s, 4 px). A tick's check fades in with `HoverIn`. Nothing slides. Under Reduce motion everything is instant.

---

## F4. The portrait pack (`portrait-pack-1.20.png`)

### What it is

About 2,300 head-cropped Garland Tools NPC photos (by Celes), as a single file on Tsukimichi's own GitHub release (`portraits-1`). With game art it raises giver coverage from about 18% to roughly 80–85% of quests. Pack photos go first in the 1.15 source order (A1), are graded with the colour-family matrix (A3), and sit on the same plate.

### Settings › General › Look

**Giver portraits** gains its third choice: **Off · Game art · Game art + pack**.
- Picking **Game art + pack** without the pack opens the confirmation (below). Cancelling leaves the choice where it was, so the choice is never a dead option.
- When the pack installs, the choice switches to **Game art + pack** by itself: the player asked for it.
- The help reads "The giver's face from the game's art and the pack, or a silhouette, emblem or initials. Read on this PC."

**Portrait pack** is a sub-row under it, indented, always present. Its three columns (label, status, a 210 px action column) and its height stay the same in every state:
- **One fixed height:** a status line, two Secondary lines (clamped; the hover shows more), and one reserved slot for the bar or a Tertiary line. That is 118 px at Full and Quiet and 108 at Plain, at 100%. In a card under 400 px the actions move under the words and the row is 141 px (130 at Plain).
- **At most two actions, side by side.** Nothing stacks.
- So a download that finishes while Settings is open changes words, never the row's height.

| State | Status (Text, medium) | Line (Secondary) | Actions |
|---|---|---|---|
| Not downloaded | Not downloaded | "About 2,300 more giver faces from Garland Tools' NPC photos. **14.8 MB** from Tsukimichi's GitHub release, only when you click." | **Download…** |
| Downloading | Downloading | "6.2 of 14.8 MB · about 20 s left", a 4 px bar (`--mist` on `--line`) in the slot, and in Tertiary "Keeps going if Settings closes." | Cancel (quiet) |
| Checking | Checking the file | "Comparing it with the fingerprint built into Tsukimichi 1.20.0", the bar full | none |
| Installed | Installed · pack 1 | "2,310 faces · 14.8 MB on this PC · downloaded 4 Dec" | Remove pack… (quiet) |
| Update offered | Pack 2 is ready to download | "140 more faces · 15.2 MB, replaces pack 1, which keeps working until then." and in Tertiary "Named by this version of Tsukimichi; it never checks online." | **Update…** · Remove pack… |
| Cancelled | Cancelled | "Nothing was saved." | Download… |

Sizes, counts and the release name come from a manifest compiled into the plugin. The mock's 14.8 MB and 2,310 are examples.

### The confirmation

A small window of its own, 470 px wide, centred on the Settings window and kept in front of it. **It does not use ImGui's modal popup:** `BeginPopupModal`'s dim covers the whole game screen, fades in on ImGui's own clock (about 0.17 s, ignoring Reduce motion), and is drawn after the plugin's style colours are popped, so it would take Dalamud's colour, not the palette's. Instead:
- **The scrim is Tsukimichi's own:** a filled rect over the Settings window's rect only (the dialog window's draw list, its clip rect pushed to the Settings rect), in `rgba(5,7,14,.55)` on the dark palettes and `rgba(26,33,54,.30)` on Ishgard Snow. The game and other windows stay as they are. It is the only dimming: Settings is not faded or desaturated as well.
- **Settings ignores input** while the dialog is open (it draws inside `BeginDisabled` without the dimmed alpha, so only the scrim dims it), and a click on Settings brings the dialog back to the front (`SetNextWindowFocus`).
- **It follows Settings:** its position is set from the Settings window on every frame, not only when it opens, so the two always share a viewport, even when Settings is dragged out of the game window.
- **Accepted:** because the scrim is drawn from the dialog's own draw list, another Tsukimichi window that overlaps the Settings rect, behind the dialog, is darkened where they overlap.
- **It goes when** the player cancels, downloads, closes Settings or the plugin unloads.
- `LinkConfirmWindow` stays as it is; this one blocks because it asks about the one network action.

1. **Title** in the Title face at Full (semibold at Quiet and Plain): "Download the portrait pack?"
2. **What:** "Adds about 2,300 giver faces, so roughly 4 in 5 quests show who gives them. The rest keep a silhouette, an emblem or initials."
3. **The facts**, as a two-column list (Tertiary keys, Text values with Secondary asides):
   - **Size:** 14.8 MB · saved once in Tsukimichi's folder on this PC
   - **From:** github.com/xenofei/Tsukimichi · release portraits-1
   - **Photos:** Garland Tools NPC photos by Celes · cropped to the head
   - **Checked:** against the fingerprint built into Tsukimichi 1.20.0
4. **The promise**, in a sunk inset with a `--line` outline: "**Only because you clicked.** This is the one time Tsukimichi goes online. It sends nothing about you or your characters: GitHub sees an ordinary file download from this PC. Tsukimichi never checks for updates by itself."
5. **Spoilers:** "The spoiler shield still hides faces from quests ahead of you."
6. **Actions:** "What Tsukimichi sends" (a link that opens `docs/privacy.md` in the browser), then **Download 14.8 MB** (the primary pill) and **Cancel**.

**Keyboard focus starts on Cancel** (`SetItemDefaultFocus`), and Enter is handled as Cancel explicitly, so Enter never downloads by accident. Esc cancels too. The focus ring is TextSecondary (6:1 or better on the dialog), not the Line tone. The update uses the same window with "Download pack 2?", "Adds 140 faces and replaces pack 1 when it's checked." and the new size.

**Motion:** the dialog uses `Rise` (0.16 s, 4 px) and `Leave` (0.12 s), and the scrim fades with it over the same times, through `PopupFade` and the plugin's own clock. Both are instant under Reduce motion.

### Progress and arrival

- The bar eases each update over 0.12 s. Under Reduce motion it steps. It has no shimmer and no pulse.
- The download runs off the draw thread and survives closing Settings. It stops with the plugin; a half-file is deleted on the next start.
- **Checking** compares the SHA-256 with the manifest. Only a match is unpacked, into `pluginConfigs\Tsukimichi\portraits\pack-1\`, and swapped in whole.
- **When it lands:** a status-bar note "**Portrait pack installed** · 2,310 faces", like "Report copied". Faces appear on the next draw of each plate and fade in over `ArtFade` (0.3 s) where a fallback stood. The plate never moves (1.15 A7).

### Errors

An error is the 1.17 Settings hint form: a 6 px `--hint` dot beside the status in **Text**, the reason in Secondary, and one way forward. Nothing is ever half-installed.

| Error | Status | Line | Actions |
|---|---|---|---|
| No connection, or GitHub unreachable | Download failed | "Couldn't reach GitHub. Nothing was saved." | Try again |
| The fingerprint doesn't match | Download failed | "The file didn't match this version's fingerprint, so it was deleted. Nothing was installed." | Try again · Copy report |
| Not enough space | Download failed | "Not enough space: the pack needs 15 MB in Dalamud's pluginConfigs folder." | Try again |
| Damaged on disk (found at start) | Pack damaged | "Some of the pack's files are missing. Faces use game art until you download it again." | Download again… |

Copy report puts the version, the expected and received hashes and the byte count on the clipboard. It includes no character data.

### Remove pack

**Remove pack…** opens the 1.18 confirm popover, anchored to the button:
- **Title:** "Remove the portrait pack?"
- **Body:** "Deletes 14.8 MB from this PC. Faces go back to game art, silhouettes, emblems and initials. Getting it back means downloading it again."
- **Actions:** **Remove pack**, the shipped `Chrome.HoldButton` unchanged (`SafetyTier.Hold`, a new `GuardedAction.RemovePortraitPack`, like Forget this character). Press and hold for the hold length set in Settings (0.6 s by default) while a Moon arc closes clockwise from the top centre along the outline over a line-tone track, or Ctrl/Shift-click. Under Reduce motion the label counts down instead ("Hold… (0.3 s)"); in two-click mode, two clicks. Then **Keep it** (quiet). Under them, in Tertiary: "Hold, or Ctrl-click".

There is no Undo: the files are gone, and getting them back is a download, so the Hold tier asks first instead. Afterwards Giver portraits goes back to **Game art** and the row reads Not downloaded.

### The update offer

The plugin never asks GitHub whether a newer pack exists. Each Tsukimichi release compiles in the name, size and hash of the pack it pairs with. When Dalamud updates Tsukimichi and the new manifest names a newer pack than the one installed, the row shows **Pack 2 is ready to download**. There is no popup and no chat line. The old pack keeps working until the player updates or removes it.

### Pack faces on the plate

- The pack's photos are full-body renders, cut to a square box per photo by the 1.15 framing rule (crown 8–12%, eye line 42–46%, chin 78–84%) at DataGen time, with the contact sheet's guide bands for a spot check.
- They are graded as a colour family (A3), so a pack face sits beside a Duty Support bust without looking like a different feature.
- **Pack crops sit high in the bands:** the eye line at 42–44% and the chin at 82–84%, so a pack head fills the plate as fully as a Duty Support bust beside it. The DataGen contact sheet checks this side by side with a game-art face.
- **A pack photo is never drawn above its own box** (its source pixels, 1.0×): the hover shows it at min(128, box), so a 92 px box shows at 92 px. Its source line is "Portrait: Garland Tools photo · credit Celes".
- **DataGen keeps only photos whose head box is at least 72 px,** so the 72 px plate never upscales at 100% UI scale; a smaller photo falls through to the next source (A1). Above 100%, a pack face may upscale by up to the scale factor, as game art does.
- **The crown band applies to the skull,** not to a hat or turban (Isembard's turban top sits above it).
- **Credit** also appears in Settings › Advanced › Privacy & trust and the README's credits.

### What the trust pages say

| Where | Today | From 1.20 |
|---|---|---|
| Privacy & trust › Sends | "Sends: nothing. A web page opens in your browser only when you click its link…" | "Sends: nothing, except the portrait pack when you click Download: one file from Tsukimichi's GitHub release." |
| Privacy & trust › summary | "It has no network code and sends nothing." | "Its only network code downloads the portrait pack, when you click Download. It sends nothing." |
| About automation › Local only | "Tsukimichi has no network code. Nothing you do here leaves your computer." | "Nothing you do here leaves your computer." (the network sentence moves to Privacy & trust) |
| Help › Privacy (lede) | "Tsukimichi reads your own characters, keeps its files on this PC and has no network code." | "… keeps its files on this PC, and goes online only to download the portrait pack when you ask." |
| `docs/privacy.md` | "If a portrait pack is ever offered…" | a "The portrait pack" section: what it fetches, from where, when, the hash check, where it is kept, how to remove it |

---

## Every look (`looks-1.20.png`, `looks-new-1.20.png`)

The first matrix shows the prep card (an open line and a ticked one, in the narrow form), a masked giver (the silhouette at 72, 64 and 18 px, the name and place as placeholders) and the pack row (Installed) at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer. Everything is built from tokens each level already defines:
- **Full:** the framed card, the Title face, gold section headers;
- **Quiet:** tonal cards and hairlines;
- **Plain:** ledger bands, the 18 px inline plate before the giver's name, square-cornered buttons.

The contrast pairs are the ones gated in `1.16/contrast.md`, `1.17/contrast17.md` and 1.18:
- Text, Secondary and Tertiary on the card (placeholders are Secondary: 7.35:1 on Night raised, 8.37 on Snow white);
- the silhouette ink on Snow, `#56607C` on the Snow well: 4.84:1 at the top, 4.06 at the bottom;
- the checkbox's Secondary outline: 6.4:1 on Night Hover, 8.4 on Snow;
- the one new value, `--hint` on Snow, above.

The second matrix shows every part new in 1.20 on all four palettes:
- pack photos beside Alphinaud's Duty Support portrait at 72 (Full), 64 (Quiet), 24 and 18 (Plain) px, graded alike (the night multiply skipped on Snow, per 1.15 A3);
- the hidden-reward tile (its disc in `#56607C` on Snow, 3.8:1);
- a pack error with the `--hint` dot;
- the Remove pack popover at Plain, with the Moon arc mid-hold;
- the compact confirmation at Quiet over its dim.

**High-contrast forms:** placeholders move to Text with a 1 px dotted underline (Secondary is too close to Text to matter there, and the underline keeps them distinct). The moon-disc tile gets a 1.5 px outline. The progress bar's track gets a 1 px outline.

**Text size and UI scale:** every size above is at 100%. The card's line height, the pack row's height and the dialog's width scale with them. The prep card's buttons drop under the words when the card is under 460 px wide at the current scale; the pack row's actions do so under 400 px.

---

## Code map

| Piece | Where |
|---|---|
| The name rule and placeholders | a new `Tsukimichi.Core/Query/SpoilerNames.cs` beside `SpoilerMask.cs`: `SpoilerNames.Name(kind, id)` for area, aetheryte, duty, reward and person, built once per session version with the mask and sharing its `Fingerprint`; placeholders cached per level and language as `SpoilerMask` does |
| A lint test | `Tsukimichi.Tests/Query/`: no UI file prints a place, duty, item or NPC name except through `SpoilerNames` (the research's suggestion) |
| The setting | `ConfigWindow.Spoilers.cs` (`DrawSpoilers`: the sub-switch, the count line), `Strings.Spoilers.cs`, `Configuration.cs` (a new `SpoilerHideOtherNames`, migrated from `SpoilerHideMsqNames`) |
| Hover and right-click | `HoverHint.cs` (the three-line hover), the row and pane context menus; `LinkConfirmWindow.cs` asks for hidden names too |
| Surfaces | `DetailPane.cs`, `DetailPane.Hero.cs`, `DetailPane.Duties.cs`, `DetailPane.Unlocks.cs`, `DetailPane.Collector.cs`, `DetailPane.HandIn.cs`, `RewardTooltip.cs`, `TablePane.cs` (Zone, Giver, Unlocks), `TreePane.cs`, `PathChart.cs`, `RouteWindow.cs`, `NextStopsSource.cs`, `TonightCard.Stops.cs`, `DutyBoardSource.cs`, `CharactersPane.Duties.cs`, `MoonlitPane.cs`, `MainWindow.FindUnlock.cs`, `TableCopy.cs`, `DiscordCopy.cs`, `Game/WotsitIpc.cs` |
| The hidden-reward tile | `Chrome.Portrait.cs`'s moon-disc sprite from `OrnamentAtlas`, at 16 px on a 22 px tile |
| Before Evercold lines | a new `Tsukimichi.Core/Plan/EvercoldPrep.cs`: pure, tested; builds the lines from a snapshot (C9's count, the job ladders, N4's duty board, K3's flying unlocks, the MSQ graph) |
| The card | a new `TonightCard.Evercold.cs` and `CharactersPane.Evercold.cs`, drawn from one `EvercoldCardModel` |
| Ticks and hidden state | `CharacterSettingsBook` (per character, in `user/characters.json`) |
| Hide with Undo | `SafetyRules.cs` (`GuardedAction.HideEvercoldCard`, `SafetyTier.None`), `UndoToast` |
| The launch date | curated data beside `festivals.json` (one date, rechecked after Tokyo) |
| Giver portraits choice | `ConfigWindow.General.cs` (`DrawLook`), `GiverPortraitMode.GameArtAndPack` in `Tsukimichi.Core/Ui/PortraitPlate.cs`, `Strings.Portraits.cs` |
| The pack row | a new `ConfigWindow.PortraitPack.cs` |
| The confirmation | a new `PortraitPackWindow.cs` (a `Window` kept in front of Settings), with its own scrim clipped to the Settings rect; `ConfigWindow` draws inside `BeginDisabled` while it is open; faded through `PopupFade` |
| Manifest, download, check, install, remove | a new `Tsukimichi.Core/Portraits/PortraitPackManifest.cs` (compiled-in name, size, SHA-256, URL) and `PortraitPackStore.cs` (verify, swap in, remove); the one download call in a new `Tsukimichi/Portraits/PortraitPackDownload.cs` |
| Pack faces | `PortraitSource.PackPhoto` first in `PortraitIndex`; `GiverPortraits.cs`, `PortraitGrading.cs` (colour family) |
| Remove at the Hold tier | `SafetyRules.cs` (`GuardedAction.RemovePortraitPack`, `SafetyTier.Hold`), `Chrome.HoldButton` in `Chrome.cs` (unchanged) |
| Trust pages | `ConfigWindow.Privacy.cs`, `Strings.Privacy.cs`, `AboutAutomationWindow.cs`, `HelpWindow.cs` (the Privacy topic), `docs/privacy.md` |
| The no-network guard | `Tsukimichi.Tests/Diagnostics/NoNetworkTests.cs`: see open question 1 |
| Building the pack | `Tsukimichi.DataGen --portraits` (giver-portraits P4): fetch at 1 request a second, crop by the framing rule, write the contact sheet; the release workflow attaches `portraits-N` and its hash |

---

## Decisions

1. **Placeholders are kind words with a safe locator,** not "An area of the next chapter" (the research's wording). The expansion's name, the content type and the level are public and help planning; the area number keeps grouping possible without the name.
2. **Secondary, not a glyph.** A placeholder is the hidden name's own slot in Secondary. No blur, no bar, no lock.
3. **People you have met keep their names;** faces still hide on masked quests (1.15 A6.4).
4. **One switch, under Hide story names ahead, on by default,** taking that switch's value on upgrade. No per-kind switches: the per-character override already covers "show everything".
5. **Reveal stays per session.** "Reveal names in this quest" in the detail pane, "Reveal this name" on right-click.
6. **Search never hints at hidden matches.** A "3 hidden matches" line would itself confirm a name from ahead.
7. **Travel buttons to hidden places are hidden,** not greyed (1.18's rule).
8. **Before Evercold is quest-side only:** five lines from data Tsukimichi already has. Tomestones and gear are out of scope.
9. **The journal line is done at 10 free slots,** and every line takes a tick from the player ("you said so").
10. **Hiding the card has Undo** (8 s toast); the dashboard keeps a "Show again" line. Ticks and hiding are per character.
11. **No copper in 1.20.** The prep card is never urgent; pack errors use the Settings hint.
12. **The pack is downloaded in the plugin,** per plan decision 8, behind a confirmation that names size, source, credit and the hash check, with focus on Cancel.
13. **Tsukimichi never checks for a newer pack online.** The update offer comes from the manifest in each plugin release.
14. **Remove pack is at the Hold tier with no Undo,** because getting it back takes a download. It uses the shipped Hold button unchanged; no destructive colour variant is added.
15. **Installing switches Giver portraits to Game art + pack;** picking that choice without the pack opens the confirmation.
16. **Every placeholder slot is either single-line with an ellipsis or sized for the longer string,** so a reveal never reflows anything.
17. **The confirmation blocks Settings, not the game.** Its scrim is Tsukimichi's own, over Settings only, in the palette's colour, and it follows Reduce motion. ImGui's modal dim is not used.

## Open questions

1. **The no-network guard.** An in-plugin download needs one exception in `NoNetworkTests`: one named file, one host (`github.com/xenofei/Tsukimichi/releases/download/`), one pinned URL per release. Do you accept that exception, as plan decision 8 implies? The alternative keeps the test as it is: Download… opens the release asset in your browser and the row offers **Import pack…** (a file picker) with the same hash check. It is one extra step for players. I recommend the in-plugin download.
2. **Side-quest names.** The shield still shows the names of side, feature and job quests from expansions you haven't reached (only their zones, givers and rewards are hidden). Should 1.20 also show those as "Side quest (Lv 93)"? It closes a leak, but a new player browsing All quests would see thousands of placeholders.
3. **The journal line's threshold:** 10 free slots (my proposal), or 5?

## Approval record (realism supervisor: APPROVED, round 3)

| Round | Commit | Verdict | What it found |
|---|---|---|---|
| 1 | (draft) | CHANGES | 6 major findings:<br>• the pack row's height changed with its state;<br>• a tick reflowed the prep card;<br>• the checkbox outline was under 3:1;<br>• the Hold button was not the shipped one;<br>• placeholders wrapped or changed a row's height on reveal;<br>• the new parts were rendered on Night only.<br>Also 11 minor findings and some nits. |
| 2 | `2faa372` | CHANGES | 1 major: ImGui's modal dim would cover the whole game screen, fade on its own clock and ignore the palette.<br>5 minor findings and some nits. |
| 3 | `22766f1` | **APPROVED** | No blocker or major finding. |

**Round 3's one minor finding is applied:** the dialog's position follows Settings every frame, and the overlap darkening is accepted (F4, "The confirmation").

**Its nits:**
- The double space in the Plain giver line is fixed.
- Whether a placeholder in search really keeps medium weight is left to an in-plugin check, since the lighter look may only be the Secondary colour.

**The supervisor asked to keep:**
- the fixed-height rows;
- the reserved button slot on a tick;
- the MoonToggle and the shipped Hold button;
- composite strings in Secondary;
- the Snow disc in `#56607C` (3.78:1) and the `--hint` dot;
- pack photos checked beside a game-art face;
- the TextSecondary focus ring.

**Unverified until it is built:** the scrim, input blocking and focus in Dalamud's ImGui (including multi-viewport); whether a placeholder in search keeps medium weight; the size of the real pack and how it frames once DataGen has built it.

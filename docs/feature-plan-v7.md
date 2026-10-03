# Tsukimichi feature plan v7: your look, your faces, your road

Status: **signed off on 2026-10-03 and executing.** 1.14.0 and 1.15.0 are released; 1.16.0 is in progress. Plan v6 shipped 1.11.0 to 1.13.0; its unbuilt releases (automation, right answers, what next, and the API 16 track) are carried into this plan and re-checked against Patch 8.0.

## Sources

- **Your ten points of 2026-10-03.** They are recorded with screenshots in `docs/research/plan-v7/owner-points.md`.
- **A UI audit of the 1.13 code.** It gives the root causes and file references for every point (`docs/research/plan-v7/ui-audit.md`).
- **Giver portraits research.** It covers every source, with coverage measured against the game data (`giver-portraits.md`).
- **A theme system design.** It covers themes, mix-and-match and palettes (`theme-system.md`).
- **Feature research.** It includes recent community evidence, UX patterns and what Patch 8.0 "Evercold" changes (`feature-ideas.md`).
- **The design team.** Three revived moon themes (Ishgard Glass, Aether Crystal and Astrologian's Orrery) and the UI designs, each reviewed by the realism supervisor and a design critic (`docs/design/v7/`).

## Standing rules

These carry over:
- player value first;
- English only;
- art from anywhere (official or fan-made, or drawn to match) when it fits;
- automation only behind explicit buttons, through companion plugins;
- teleport through Lifestream only;
- no contact with other plugin developers;
- every plan gets a website;
- a realism supervisor reviews all concept, UI and UX art;
- the window never moves under the player;
- show what is left, not tallies;
- no decorative glyphs that carry no meaning.

New in this plan:
- **Every theme speaks one visual language.** Each moon set uses the same mark for each state: the moon road on Ready, the padlock, book and job badges, the clouded new moon, the "comes back" arc, the gold check, the cracked Dalamud and the "?". So a mix of sets never gives one mark two meanings.
- **One frame per mix.** Moon faces and their frames are separate, so a mixed column always has one rim style, with Ready on gilt in every kit.

## Your points

| # | You said | Plan | Release |
|---|---|---|---|
| 1 | Quest-pane section names are too small (the game font for headings) | **UI-1.** The root cause: the game's condensed display font at 1.45× reads smaller than body text, and turning game fonts off makes it *smaller* (the fallback is a 0.85× caption). A new Section heading role is 1.80× at Full (tracked, lighter gilt), 1.15× at Quiet and a band at Plain. It never drops below body size, follows Text size (tapering to 1.60× at 150%), and the quest title grows to keep its lead. | 1.14.0 |
| 2 | The filter menu: empty grey space, an unreadable title, text visible at its sides, needs love | **UI-2.** The drawer becomes one opaque sheet the exact width of the tree column, as tall as its content. The tree isn't drawn underneath it, which fixes the bleed. It gets:<br>• a **Filters** header with an "N on" count;<br>• grouped switch rows;<br>• seven summary lines when Advanced is collapsed;<br>• a quiet Reset with Undo.<br>Each Decoration level has its own designed look. The supervisor and critic approved the design. | 1.14.0 |
| 3 | Make the stars better | **UI-6.** One star renderer everywhere, gated by Decoration. Three depths and four star colours, a slow twinkle (7–13 s), our own constellation for each expansion region in the Astrologian card style, and a single meteor when you finish a quest (Full; never under Reduce motion). | 1.14.0 |
| 4 | Face portraits of the giver | **F1–F5.** Game art in your install covers the givers of **69% of main-story quests** (every major Scion): the painted "battle talk" faces, Duty Support and Trust busts, Triple Triad card art and custom delivery portraits. A 72 px medallion portrait sits in the Giver card, 128 px on hover, and 24 px in Next stops and Route. Fallbacks are a society emblem, a race silhouette or initials. An optional portrait pack raises coverage to about 80–85% of all quests. The spoiler shield never shows a face it would hide. | 1.15.0 |
| 5 | Darken the Completed moon's craters; a slight glow; more ideas from the supervisor and critics | **V1.** Darker basalt seas at every size and a cool glow inside the well. The three rim-lit craters are drawn only at 96 px and up, where they read as craters; at small sizes they became specks. Completed still recedes (0.72× Ready). | 1.14.0 |
| 6 | Journal column headers a bit bigger | **UI-3.** 1.55× in the secondary text colour, with a minimum header height of 32/28/24 px per level. The sorted column keeps its gilt. | 1.14.0 |
| 7 | Revive the earlier moon concepts, beautifully, as themes; mix and match icons and colour palettes | **T1–T16.** A theme sets three things:<br>• the **moons**;<br>• a **colour palette**;<br>• a **frame** style.<br>You can switch any one of them, or pick each state's moon from a different set; the Themes page warns if two picks look too alike. Revived sets, all with the shared visual language:<br>• **Ishgard Glass** (stained glass);<br>• **Aether Crystal** (cut moonstone);<br>• **Astrologian's Orrery** (engraved instrument, remade with its own identity);<br>• later, **Sumi to Kinpaku**.<br>Palettes are Night, Ishgard Snow (the first light palette), Dawn, Kugane Lacquer and Follow Dalamud, each with a high-contrast form. Medallion and Classic stay. | 1.16.0 / 1.17.0 |
| 8 | Bigger rail icons that fill the bar, no coloured lines, better hover and selection motion | **UI-4.** 30 px icons on plates in stations that share the rail's full height. The brass thread and lit bar are gone. The selected tab keeps its plate, a gold icon and a travelling bead (0.22 s); hover lifts 2 px. Labels are never below 12 px. | 1.14.0 |
| 9 | Add missing icons | **UI-5.** The audit found 24 gaps. Among them:<br>• every curated feature unlock showed a placeholder moon;<br>• the "A Pup No Longer" instance was dropped for having no name;<br>• titles, blank reward slots, EXP and gil had no icons;<br>• roles, Grand Companies and tribes had no icons;<br>• travel buttons lacked icons.<br>Each now gets a game icon from the right sheet. | 1.14.0 / 1.15.0 |
| 10 | Other features for a better experience | **N1–N12,** from new community research. Highlights:<br>• Back and forward through quests;<br>• a wider spoiler shield before Evercold;<br>• a Before-Evercold prep card;<br>• a true story meter;<br>• a Duties board;<br>• Loose ends;<br>• your story on one page;<br>• Who's in it;<br>• where to get hand-in items;<br>• alt goals;<br>• verifiable builds;<br>• Evercold day. | 1.14.0 → API 16 |

## Designs, for your review

Everything is on the plan website (https://claude.ai/artifact/5vkMh4sLVygddQfD3RYETM), with renders and a live mock. In the mock you can switch the moon themes or mix them per state.

- **UI:** `docs/design/v7/ui/`. The spec, the mock (Full, Quiet, Plain, Filter drawer, Rail, Stars, Before/after), and the Completed moon.
  - The supervisor and critic approved it after two rounds.
  - Two calls are yours: whether the Milky Way is on by default (it is now off), and whether the Chocobo is the right constellation for A Realm Reborn.
- **Themes:** `docs/design/v7/themes/`. Three revived sets. Each is split into bare faces and a frame kit, so any moon fits any frame.

| Theme | Supervisor | Weakest pair, 16 px (row tier) | Ready lead | Completed vs Ready |
|---|---|---|---|---|
| Ishgard Glass | Approved after two rounds | 12.6 (12.1) | 1.38× | 0.73× |
| Aether Crystal | Approved after two rounds | 14.4 (12.8) | 1.35× | 0.74× |
| Astrologian's Orrery | Approved after two rounds; remade so it has its own identity | 14.9 (14.0) | 1.35× | 0.74× |

The targets are a weakest pair of at least 12, a Ready lead of at least 1.3×, and Completed at most 0.8× Ready. The critic ranked Ishgard Glass first ("the most FFXIV"), then Aether Crystal.

## Releases

Each release gets the usual gates, two reviews and fixers, the supervisor on any art, then the tag, plus an in-game check list in this document.

### 1.14.0 · The polish you asked for (released 2026-10-03)

Points 1, 2, 3, 5, 6, 8 and part of 9, plus two small features.

| Id | Item | Effort |
|---|---|---|
| UI-1 | Section heading role:<br>• Full 1.80× tracked, tapering to 1.60× at 150% Text size;<br>• Quiet 1.15×, Plain a band;<br>• never below body size;<br>• the quest title grows to keep its lead. | S–M |
| UI-2 | Filter drawer:<br>• one opaque sheet the width of the tree column, as tall as its content, with the tree hidden beneath it (fixes the bleed);<br>• a Filters header with a neutral count, and grouped rows;<br>• summary lines when Advanced is collapsed;<br>• Reset with Undo;<br>• one look per Decoration level. | M |
| UI-3 | Journal column headers at 1.55× in the secondary text colour, minimum height 32/28/24 | S |
| UI-4 | The rail:<br>• 30 px icons on plates, with stations filling the rail;<br>• no thread or lit bar;<br>• the selected tab has a plate, a gold icon and a travelling bead;<br>• hover lifts 2 px;<br>• labels at least 12 px. | S–M |
| UI-6 | Stars:<br>• one renderer, gated by Decoration;<br>• three depths and four colours;<br>• slow twinkle;<br>• our own constellations per region;<br>• the completion meteor (Full, own toggle);<br>• the Milky Way band as its own setting, off by default. | S–M |
| V1 | Completed moon: darker seas, cool glow in the well, craters only at 96 px and up | S |
| UI-5a | Icons for feature unlocks (MainCommand and ContentType), the "A Pup No Longer" PvP unlock, titles, blank reward slots, EXP and gil | S |
| UI-Q | Eleven quick wins from the audit (alignment, clipped text, spacing) | S |
| N1 | **Back and forward** through quests you've jumped between: buttons, mouse back and forward, and Alt+arrows | S |
| N2 | **Trust you can check:** build hashes on each release, and a plain statement of what the plugin reads and that it uses no network (or exactly what the portrait pack fetches, if you choose a download) | S |

### 1.15.0 · Faces and icons (released 2026-10-03)

Point 4 and the rest of point 9.

| Id | Item | Effort |
|---|---|---|
| F1 | Portrait index from game data: battle-talk faces (named via their FACE_GRAPHIC variables), Duty Support and Trust busts, Triple Triad cards and custom delivery. 118 givers, 69% of main-story quests. | M |
| F2 | Giver card portrait (72 px medallion, night-graded at Full, 128 px on hover with a source line) and fallbacks (society emblem, race silhouette, initials). Spoiler-safe. | M |
| F3 | Curation pass: name the unnamed faces, fix aliases, and block wrong matches (curated file plus regen) | S |
| F5 | Small avatars in Next stops, Route and Tonight; an optional journal column, off by default | S |
| UI-5b | Duty icon chain, and nameless instances named from their territory | M |
| UI-5d | Icons in Characters (roles, Grand Companies, tribes, collections), table job groups and Plan | M |
| UI-5e | Icon-and-label action buttons on every travel and route button; icons on the Route header, the in-game panels and requirements | M–L |

### 1.16.0 · Themes

Point 7, part 1: the system and the first two revived sets.

| Id | Item | Effort |
|---|---|---|
| T1 | The appearance model: moon set, palette and frame kit as one appearance, with presets and migration from Moon style and Moon colours | M |
| T2 | Palette plumbing: move about 230 fixed colours onto the palette, so a light palette works everywhere | L |
| T3 | The renderer seam: moon sets and the frame compositor behind the current moon drawing; Medallion and Classic ported as sets | M |
| T4 | The multi-theme build: generator manifests, renders at every tier, per-set gates, the cross-set similarity table and contact sheets | M |
| T5 | Atlas runtime: load only the sets in use, row strips per pixel size, 2x on demand, with tests | M |
| T6 | **Ishgard Glass**, with the lead-came frame kit, through designer, supervisor, critic and your check | L |
| T7 | **Aether Crystal**, with the silver frame kit | L |
| T8 | Palettes: Night (ported) and **Ishgard Snow**, the first light palette, each with a high-contrast form, plus contrast tests | M |
| T9 | Settings › Themes: a card grid with live previews, the palette picker, the frames choice, and Reset with safety | M |

### 1.17.0 · Mix and match

Point 7, part 2.

| Id | Item | Effort |
|---|---|---|
| T10 | The per-state mix table: pick each state's moon from any set, with warnings when two picks look too alike or Ready stops being the loudest, and a "Fix it" button | M |
| T11 | Frames as a choice: Brass, Silver or Lead came for medals, badges, gauges and the Decoration ornament | M |
| T12 | Share codes: copy and paste a look, preview it before applying, Undo | S |
| T13 | The glyph window's Themes tab: compare two looks, and see how alike every pair of moons is | S |
| T14 | **Astrologian's Orrery**, remade with its own instrument identity, with the astrolabe kit | L |
| T15 | **Sumi to Kinpaku** revived, with the gold-leaf kit (design round first) | L |
| T16 | Palettes **Dawn** and **Kugane Lacquer**, with high-contrast forms | M |

### 1.18.0 · Runs you can trust

Carried from plan v6 (was 1.14). Automation, built on what the community accepts: solo and NPC content only.

| Id | Item | Effort |
|---|---|---|
| A2 | A "Why it stopped" card with one-click fixes and Copy report | M |
| A3 | Keep automation out of other players' runs: Questionable stops (or warns, or does nothing, per your setting) before a duty with no Duty Support or Trust | M |
| A4 | Stop after N quests or at a set time, a run receipt, and stop-after-current for runs started some other way (Questionable has had its own "Stop after current quest" toggle since May 2026, and A6 covers single quests) | S |
| A5 | "Needs you" alerts while a hand-off runs: death, stuck, a duty pop, a tell | M |
| A6 | Questionable, **decided by the panel: build with changes.**<br>• **Start does one quest:** the Start pill calls `StartSingleQuest`: "Questionable does this quest, then stops." It falls back to the old path, with a tooltip saying so, only if the gate is missing.<br>• **"Start here and keep going"** (the old behaviour) moves to the "…" menu.<br>• **"Do this next"** (insert at #1) goes in the "…" menu under the hand-off setting. It is enabled only for quests Questionable has a path for, and the list is read back afterwards. It is disabled while Questionable runs if in-game testing reproduces Questionable#45.<br>• **When a run ends,** a chat line says what finished.<br>No split button and no new setting. | S |
| A7 | Craft with Artisan becomes Stop while running | S |
| A8 | Travel recovery: reload the navmesh and retry; walk to an aetheryte before a hop; land where you can talk | M |
| A9 | A travel preflight in Setup | S |
| A10 | An automation level switch with Full hand-offs as a first-class choice, and an "About automation" card | S |

### 1.19.0 · Right answers

Carried from plan v6 (was 1.15), adjusted for Evercold, plus new correctness features.

| Id | Item | Effort |
|---|---|---|
| C1 | The game confirms it: record the game's own available-quest markers and offers | M |
| C3 | Gates from the wiki's free text (deep-dungeon floors, relic tools, Bozja rank and others) | M |
| C4 | A full New Game+ session mode | M |
| C5 | Allied societies: carried-over dailies and stored alts' allowances | S |
| C6 | Quest rewards you can buy back | M |
| C7 | How you'll clear it: duty badges and the item-level wall. **From 8.0, the wall compares your best job's item level**, since Evercold shares it across jobs. | M |
| C8 | EXP to the right job; Switch gearset only for quests that need a specific job | M |
| C9 | Journal slots and Make room | S |
| C10 | Seasonal events: ending-soon warnings and dated reruns | S |
| K3 | Find by unlock, an Unlocks filter, and Route to unlock | M |
| K5 | Unlock data completeness: the 11 uncurated aetherytes and a feature-code audit | M |
| N3 | **A true story meter:** catch-up counts the side quests the story requires (Crystal Tower, Hard primals) | S |
| N4 | **Duties board:** why a roulette is locked, and duties unlocked but never cleared | M |
| N5 | **Where to get hand-in items** for quests that need them | S–M |

### 1.20.0 · Before Evercold

Aim: early December 2026, ahead of 8.0 early access (expected 22 January 2027).

| Id | Item | Effort |
|---|---|---|
| N6 | **A wider spoiler shield:** hide zone, duty, reward and NPC names past your story point, so 8.0 names can't leak through Unlocks, Path or tooltips | M |
| N7 | **Before Evercold prep card:** what each character should finish before 8.0 | S |
| F4 | The optional portrait pack (see decision 8) | M |

### 1.21.0 · What next, for every character

Carried from plan v6 (was 1.16), plus new features.

| Id | Item | Effort |
|---|---|---|
| P1 | "Up next" at the top of Tonight | M |
| P2 | Go to the current step of a quest already in your journal | M |
| P3 | A roster board of all characters, including every open client's characters read-only for multiboxers | M |
| P4 | My blues: "Do first" triage by tier | M |
| P5 | Named side-story questlines and "Caught up" | M |
| P6 | Triple Triad opponents behind quests | M |
| P7 | A zones board | M |
| P8 | `/tsuki msq` and `/tsuki next` for text-to-speech | S |
| N8 | **Loose ends:** storylines you started and never finished | M |
| N9 | **Your story on one page,** with your pace | M |
| N10 | **Who's in it:** which side quests feature story characters you've already met | M |
| N11 | **Alt goals:** "catch this alt up to…" across your alts | M |

### Parallel track · API 16 and Evercold day

| Id | Item | Effort |
|---|---|---|
| P9 | API 16 readiness: bundle Lumina.Excel and the compatibility layer; merge when Dalamud announces v16 | M |
| N12 | **Evercold day:**<br>• the level cap moves to 110;<br>• new jobs;<br>• a shared item level across jobs;<br>• Adventurer Activity replaces dailies and tomestones;<br>• the story splits into three areas you can play in any order;<br>• a "New in 8.0" card per character.<br>Recheck after the Tokyo Fan Fest (31 Oct to 1 Nov). | M |

## Not doing, for now

| Idea | Why not |
|---|---|
| A live 3D render of the giver | Possible through the game's character view, but fragile and crash-prone across patches |
| Screen-capture portraits | The game UI is baked into the frame, angles vary, and you only meet the NPC when you no longer need the picture. A "Take portrait" button may come later. |
| Console Games Wiki portraits | Their images forbid third-party use |
| A Milky Way band on by default | It is a separate setting, off by default (decision 12) |
| Go to anything (Ctrl+K), saved views, session recap, quest achievements within reach | Good but lower value; revisit after 1.21 |
| A native-style Todo overlay | Investigate on the API 16 track |
| The game's own Auto-Move for walks | Wait until 8.x adds it |
| Queue-safe quests (cutscene and voice markers) | Wait for 8.0 data |
| Lore codex, Try On for quest gear, levequest companion, personal notes | Low value or covered elsewhere |
| Everything plan v6 already ruled out | Unchanged (Lodestone fetches, non-Lifestream teleports, stream mode and the rest) |

## What the community said

- **Patch 8.0 "Evercold"** (early access expected 22 January 2027):
  - the level cap moves to 110;
  - new jobs;
  - your highest item level applies to every job;
  - a weekly Adventurer Activity replaces daily routines and tomestones;
  - the story splits into three areas you can play in any order;
  - the game's own Auto-Move arrives patch by patch.

  Tsukimichi's C7, catch-up and spoiler shield must adapt (N12, N6).
- **Spoilers from 8.0 names** are a top worry: the shield must cover zone, duty and NPC names, not just quest names (N6).
- **"Which side quests matter for the story?"** keeps coming back: Crystal Tower and the Hard primals are required, and players miss them (N3).
- **Roulettes locked for unknown reasons**, and duties unlocked but never cleared, are frequent questions (N4).
- **Players jump between quests** and lose their place; every browser-style tool has Back (N1).
- **Plugin privacy** stays a live topic in 2026; verifiable builds and a plain "what we read" statement are cheap trust (N2).
- **Multiboxers and alt players** want catch-up goals across characters (N11, P3).
- **Gaps in the research:** Bluesky refused searches, and the plugin subreddits have no archived posts since 2024, so evidence leans on r/ffxiv and the forums.

## Decisions for you

| # | Question | My recommendation |
|---|---|---|
| 1 | Release order: polish (1.14), faces (1.15), themes (1.16 to 1.17), then automation, right answers, Before Evercold and what next? | As written. Polish and faces are what you asked for first; Before Evercold must land by December. |
| 2 | Which revived themes ship first? | Ishgard Glass and Aether Crystal (the critic's top two) |
| 3 | Astrologian's Orrery | Keep it: remade with its own identity (supervisor-approved) rather than dropped; its constellation "?" is the best single glyph |
| 4 | Keep Classic (the 1.11 moons) as a theme? | Yes, as a whole theme only (it predates the shared visual language) |
| 5 | High contrast | One shared low-vision set, ignoring the mix, for safety |
| 6 | Does each theme get its own Plain (ledger) moons? | Medallion keeps its own; the revived themes share a flat Plain set at first |
| 7 | Palette names: Night, Ishgard Snow, Dawn, Kugane Lacquer, Follow Dalamud | As listed |
| 8 | The portrait pack (about 80–85% coverage, 10–20 MB): bundle it, offer an opt-in download, or skip it? | An opt-in download of a pack we host on Tsukimichi's own GitHub release, with confirmation. The plugin stays local-only unless you click. |
| 9 | Section heading size | 1.80× with a bigger quest title (the supervisor's preferred fix) |
| 10 | Constellations | Our own, in the Astrologian card style (the game has none per expansion) |
| 11 | The completion meteor | On by default at Full, with its own toggle under Motion |
| 12 | The Milky Way band | Its own setting, off by default |
| 13 | Revive Sumi to Kinpaku as a fourth theme in 1.17? | Yes, after its own design round |
| 14 | Before Evercold (1.20) by early December | Yes; recheck its contents after the Tokyo Fan Fest |

### Your answers (3 October 2026)

You agreed with all 14 recommendations and voted "build it" on every item except A6, which you asked to put to a panel. **The plan is signed off and executing from 1.14.0.**

Your notes, and what happens with each:

| Item | You said | What happens |
|---|---|---|
| UI-5a | "If an icon would give it more emphasis and it supports it, then add it in." | Icons are added wherever they give emphasis and fit, not only for the 24 audited gaps. |
| A6 | "Get with critics, power users, casual players, and others to determine if this is wanted or not." | **Decided: build with changes.** All six personas want it: a design critic, a power user, a casual story player, a returning player, an automation skeptic and an accessibility view. Evidence: r/ffxivdiscussion threads, Questionable#5, #45 and #118, AutoDuty#556. The main change is that Start does one quest by default (see A6 in 1.18), with no split button. A4 is narrowed to match. |
| Rail badge | People with 99+ quests see a number that never goes down. "Think of a better solution, and implement it." | A redesigned Journal badge (UI addendum, Revision 3), built in 1.14 with the rail. |
| UI-4 | The "Characters" label escapes the highlighted plate. | Labels always fit inside their plate (Revision 3), built in 1.14. |
| UI-6 | "Subtle star animations … or maybe even a slow moving night sky." | A very slow drift of the whole sky at Full, off under Reduce motion (Revision 3), built in 1.14. |
| V1 | The craters are part blurry and part crisp. "Needs to have consistency, and maybe a bit more realism." | The craters are redesigned for one consistent, realistic treatment, then reviewed by the supervisor, built in 1.14. |
| Ishgard Glass | Completed "looks just like a blob"; Locked out should be "similarly cracked like the others … slightly different", with no large hole | Glass round 3, reviewed by the supervisor before 1.16. |
| Aether Crystal, Orrery | "Looks great!" | No change. |

## In-game checks

### 1.14.0 (released 2026-10-03)

- **Headings:**
  - The quest title is now 2.25× body. The spec said 1.92×, but the game's real fonts needed more for the title to stay above the new section headings. If it's too big, it's one setting in code.
  - Do long titles still fit on the banner?
  - Is the letter spacing even, and does the sort arrow sit level with the larger column headers?
- **Filter drawer:**
  - At Full, Quiet and Plain, with Advanced open and closed: no bleed at the edges, and the shadow stays above the status bar.
  - Clicking a summary line opens that group.
  - Reset, then Undo, brings back your filters and your search.
  - With the tree dragged down to its icon strip, the drawer uses a 262 px floor.
- **Rail:**
  - Hover, select and bead motion.
  - "Characters" fits at 150% Text size.
  - The Journal badge starts at 0 and counts newly ready quests; clicking it shows them, and it clears after you close that list.
  - The same works on a character set to "Don't track".
- **Stars:**
  - The drift pauses when you click into the game, and stars fade at the edges.
  - The constellation needs a tall window.
  - A meteor crosses the sky on completion.
  - Milky Way faintness (off by default).
- **Back and forward:**
  - The ‹ › buttons, and mouse buttons 4 and 5.
  - Alt+←/→ against your camera keybinds; the keys can be turned off in Settings › Advanced › Keyboard.
  - Nothing happens while a menu is open.
- **Trust:** Settings › Advanced › Privacy & trust shows a hash. Compare it to the release's "Verify this build" table: Tsukimichi.dll is `04c14ca9…921778`.
- **Icons:**
  - The EXP and gil icons line up with their text.
  - A Pup quest shows one "PvP · Duty" row.
  - Judgement calls to look at: Aesthetician, the striking dummies, Doman Enclave, and Cosmic Exploration and Ishgardian Restoration.
- **Completed moon:** the craters read as hollows at 96 px and up, and smaller medals have the darker seas without craters.

### 1.15.0 (released 2026-10-03)

- **Giver portraits:**
  - The Giver card at Full, Quiet and Plain for Alphinaud (Trust bust), Y'shtola (battle face), Tataru (card) and Zhloe or M'naago (delivery). Check the crop, the brass keyline, the lip shadow, and that the fade plays only when the giver changes.
  - Delivery portraits never show the green emblem script.
  - Hover shows a larger portrait with its source line.
  - At Plain there is one plate, in the summary's Giver line.
- **Era and spoilers:**
  - Alphinaud, Y'shtola and Thancred show era-appropriate faces across expansions.
  - G'raha Tia's Crystal Tower quests show initials, not the Exarch.
  - A spoiler-masked quest shows no face and no society emblem.
  - The Journal's Giver column shows "Hidden giver" for masked rows.
- **Fallbacks:** silhouettes on generic givers, emblems on allied-society quests, initials (two letters on the card, one in small avatars), and the moon disc at 18 px.
- **Avatars:**
  - Next stops, the Route window (rows are a little taller) and the Todo overlay.
  - The Journal Giver column is off after the update; turn it on from the header menu.
  - Scrolling with it on stays smooth.
- **Buttons:**
  - Game icons on every travel and route button, Plain included.
  - Labels shorten, then become icon-only with a tooltip, never cut off.
  - The book icon on "Read the journal".
  - The Route window title icon survives a reload.
- **Icons:**
  - Requirement lines.
  - Characters: Grand Company insignia at 1.5×, societies, and collections.
  - Plan chips at narrow widths.
  - Duty emblems (the Great Hunt, the Windward Wilds) drawn whole.
- **Setting:** Settings › Look › Giver portraits Off brings back the text-only layout.

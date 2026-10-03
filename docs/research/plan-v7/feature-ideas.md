# Plan v7: new feature ideas (owner point 10)

Researched 2026-10-03, after 1.13.0. This is research only and nothing here is built.

The owner asked: "Consider other features that would be great to add into the plugin (to improve user experience)."

I checked every idea against what has shipped (README, CHANGELOG through 1.13.0) and what is planned (feature-plan-v6: 1.14 automation, 1.15 right answers, 1.16 what next, the API 16 track). An idea that overlaps a plan item is kept only when it is materially different, and the overlap is named.

## How this was researched, and its limits

- **Reddit** came through the Arctic Shift archive. Title searches worked for narrow date windows (r/ffxiv, August to October 2026), and whole threads were read by id. Wider searches and comment-body searches timed out ("Timeout. Maybe slow down a bit"). r/FFXIVPlugins and r/dalamud return no posts after 2024 in the archive.
- **GitHub** came through `gh`: goatcorp/suggestions, Questionable, Quest Map, Allagan Tools, Simple Tweaks, Teamcraft, and the READMEs of competing quest plugins (Time Memoria V3, QuestShare, QuestFlow, QuestTracker).
- **Square Enix forums and press coverage** of the three 2026 Fan Fests came through web search and fetches.
- **Japanese sources** were a Hatena blog and Yahoo! Chiebukuro results. Bluesky's public search API refused unauthenticated requests, so there is no Bluesky evidence.
- **Earlier evidence** from `docs/research/plan-v6/community.md` is reused where a v6 proposal was never adopted. In those cases the original link and date are given.

## Patch 8.0 "Evercold": what Tsukimichi should prepare for

| Fact | Source (date) | What it means for Tsukimichi |
|---|---|---|
| Release in January 2027; estimated early access Fri 22 Jan 2027 | [Eorzean Tavern](https://eorzeantavern.com/evercold/) (updated 2026-09-17); [PCGamesN](https://www.pcgamesn.com/final-fantasy-14/ff14-evercold-fanfest-berlin-keynote-summary) (2026-07-25) | Fixes the date for the API 16 track and for any "before Evercold" feature. |
| Last pre-launch keynote at Tokyo Fan Fest, 31 Oct to 1 Nov 2026 | [Fan Fest site](https://fanfest.finalfantasyxiv.com/); [TheGamer](https://www.thegamer.com/everything-announced-final-fantasy-14-berlin-fanfest/) (2026-07-25) | Recheck this list after 1 November. Quest-system details were promised for Tokyo. |
| **Your highest item level applies to all your jobs** (Armoury change) | [Eorzean Tavern](https://eorzeantavern.com/evercold/); [GamesRadar](https://www.gamesradar.com/games/final-fantasy/final-fantasy-14-evercold-promises-to-respect-your-limited-free-time-as-yoshi-p-teases-the-death-of-tomestones-and-daily-roulettes-alongside-shared-item-levels-across-every-job/) (2026-04); [ButWhyTho](https://butwhytho.net/2026/04/ffxiv-fan-fest-patch-8-0-evercold/) (2026-04) | C7's item-level wall ("needs i690, you have 672") must compare against the highest job's item level from 8.0, not the current job's. |
| **Adventurer Activity** replaces tomestones and daily routines with a weekly format | Same sources | Any copy that tells players to "run your daily roulette" goes stale. Weekly activity may become a new planning target. |
| **Story order is chosen by the player:** after a common intro, three areas (and their dungeons) in any order, which meet again later | [PCGamesN](https://www.pcgamesn.com/final-fantasy-14/ff14-evercold-fanfest-berlin-keynote-summary); [Elyxir](https://www.elyxir.gg/news/final-fantasy-xiv/ffxiv-eu-fan-fest-2026-recap-evercold-story-bastion-world-raids-and-the-biggest-80-changes) (2026-08-06) | The data model has been ready for branching since 1.0. The UI (Path, catch-up, "Up next", the spoiler shield) needs a three-route presentation. |
| **Auto Content Balancing:** some dungeons and field content scale to your level instead of syncing it | Same sources | "Ready" for some story duties may no longer depend on level the way it does today, so check how DutyRunSheets and the level gates read on 8.0 data. |
| **Level cap goes from 100 to 110**; new jobs Bastion (tank) and a physical ranged DPS revealed in Tokyo | [Eorzean Tavern](https://eorzeantavern.com/evercold/) | Job ladders, "Levelling opens", EXP tables and level filters must not assume 100 as the maximum. |
| **Auto-Move / Auto-Guide:** optional movement to the quest target along a blue line. It won't cover every quest at launch and will roll out patch by patch | [mein-MMO](https://mein-mmo.de/en/final-fantasy-xiv-gets-new-feature-that-gives-my-partner-no-excuse-to-skip-the-game,1579865/) (2026-07-27); [MMORPG.com live blog](https://www.mmorpg.com/event-blogs/follow-the-berlin-ffxiv-fan-festival-keynotes-biggest-announcements-live-blog-2000138577) (2026-07); hostile reaction on the [SE forum "Auto-pathing in quests"](https://forum.square-enix.com/ffxiv/threads/529900) (2026-07-26 to 09-30) | It overlaps Walk to giver. Prefer the game's own movement where it exists (it survives patch day when vnavmesh is down), with vnavmesh as the fallback. Not before 8.x. |
| Dalamud is adding a **Native Overlays** service, which draws game-style windows | [goatcorp/Dalamud#2874](https://github.com/goatcorp/Dalamud/pull/2874) (opened 2026-07-04, still open) | A possible API 16+ path for a Todo overlay that looks native and hides with the game's UI. |
| Japanese players' 8.0 to-do lists include "clear unneeded side quests from the journal" and "finish class quests" | [ファミ蔵通信トロフィー部, "FF14 8.0へ向けてやっておきたいリスト"](https://trozo.hateblo.jp/entry/FF14junbi8.0) (2026-04-08) | Supports a "Before Evercold" card (idea 4). |

No journal-window redesign has been announced. The character-focused menu redesign ([ButWhyTho](https://butwhytho.net/2026/04/ffxiv-fan-fest-patch-8-0-evercold/)) may move addons that Tsukimichi hooks: the Duty Finder hint, the Journal companion, and the "Worth it?" and "What this opened" panels. The existing patch-day hook pause already handles that safely.

## What the community is asking for now (new evidence, 2025–2026)

- **The quest spreadsheet is still missed.** "I really love using this one … it was discontinued after 6.3 … it gives me some kind of direction of what I can do in the game." Replies point to xivtodo.com and FFXIV Collect's achievement list as stand-ins. ([r/ffxiv 1vh6nzo](https://www.reddit.com/r/ffxiv/comments/1vh6nzo/), 2026-08-06)
- **Lore and context for new players.** A goatcorp suggestion asks for a "progressive lore companion": terms highlighted in dialogue, quest-aware, expanding as you progress, with a glossary of everything met so far ([goatcorp/suggestions#1071](https://github.com/goatcorp/suggestions/issues/1071), 2026-03-04). On the SE forum, players asked to unlock the Unending Codex "as early as possible" and to "hide in-game spoilers … based on MSQ progression" ([SE forum 526605](https://forum.square-enix.com/ffxiv/threads/526605), 2026-02-22).
- **Fear of missing story.** "I have FOMO sometimes thinking I'll miss some critical information"; "the ones that have pictures on the journal are usually interesting … it's a needle in a haystack" ([r/ffxiv 1vmj8qe](https://www.reddit.com/r/ffxiv/comments/1vmj8qe/), 2026-08-12).
- **Finding where to get quest items.** "The minimap only directs me to NPC to whom I need to turn in the items. … IN GAME, is there a way to find out where that area is?" The answer was the gathering log ([r/ffxiv 1vq686p](https://www.reddit.com/r/ffxiv/comments/1vq686p/), 2026-08-16). Allagan Tools users ask for item and quest/leve cross-links ([InventoryTools#347](https://github.com/Critical-Impact/InventoryTools/issues/347), 2026-01-02, answered by its new "Compendium").
- **"What do I do now?"** threads keep arriving: "Bored of MSQ need some side quest to do" ([1w5j7b3](https://www.reddit.com/r/ffxiv/comments/1w5j7b3/), 2026-09-02); "Specific Quest/Activities/Items to look out for as a Beginner?" (blue quests, the Conjurer-only unicorn quest) ([1vx5hbn](https://www.reddit.com/r/ffxiv/comments/1vx5hbn/), 2026-08-24).
- **MSQ percentage is wanted, and other tools get it wrong.** Simple Tweaks' scenario progress display has open bugs ([SimpleTweaks#1005](https://github.com/Caraxi/SimpleTweaksPlugin/issues/1005), 2026-03-09; [#912](https://github.com/Caraxi/SimpleTweaksPlugin/issues/912), 2025-01-12).
- **Duty tracking.** A fan-made mentor roulette tracker website replaced "a spreadsheet … too big and annoying to handle" ([r/ffxiv 1tcqo1z](https://www.reddit.com/r/ffxiv/comments/1tcqo1z/), 2026-05-14). The "why is Level Cap roulette still locked" confusion kept getting follow-ups through 2026-03-26 ([1o421cx](https://www.reddit.com/r/ffxiv/comments/1o421cx/)).
- **Item-level walls still stop players** ([1w1meyg](https://www.reddit.com/r/ffxiv/comments/1w1meyg/), 2026-08-29). This is planned as C7, but 8.0 changes the rule (see above).
- **Privacy dominates plugin talk.** "Stalking plugin Memoria has shut down" (1,710 points, [1ulptqq](https://www.reddit.com/r/ffxiv/comments/1ulptqq/), 2026-07-02); "my former stalker has found my new character" (1,886 points, [1v3up9u](https://www.reddit.com/r/ffxiv/comments/1v3up9u/), 2026-07-22); EchoVault "made a show of closing its website, only to just update" (588 points, [1v8o2m1](https://www.reddit.com/r/ffxiv/comments/1v8o2m1/), 2026-07-28). A request for a no-code character-sharing plugin was met with "You do not want what you are asking for" ([1uoszvd](https://www.reddit.com/r/ffxiv/comments/1uoszvd/), 2026-07-06).
- **Competing quest plugins (2025–26):**
  - [Time Memoria V3](https://github.com/LegendsOfTheGame/TimeMemoriaV3) (rebuilt 2026-08, updated 2026-09-27) has "Recommended" what-next, session and lifetime playtime pacing, a "What's New since baseline" list, and native game-style windows via KamiToolKit with a classic ImGui fallback.
  - [QuestShare](https://github.com/Era-FFXIV/QuestShare.Plugin) (updated 2026-04-30) shares your current quest step with your party through a server.
  - [QuestFlow](https://github.com/RoseOfficial/QuestFlow) (updated 2026-09-10) is a Questionable-style automator that switches jobs by itself.
  - Quest Map's open "notify about newly-unlocked quests" request ([QuestMap#2](https://github.com/GemPlugins/QuestMap/issues/2), 2025-05-15) is already covered by Tsukimichi's "Opened by that".
- **Already answered by Tsukimichi** (no new work needed):
  - "I'm missing an Aether Current quest" ([1vsaj8z](https://www.reddit.com/r/ffxiv/comments/1vsaj8z/), 2026-08-19) is answered by Flight and `/tsuki why`;
  - "trying to track down an emote" from a collab ([1vgtvv1](https://www.reddit.com/r/ffxiv/comments/1vgtvv1/), 2026-08-06) is answered by Moonlit, with C10's dated reruns planned.

## UX patterns worth borrowing

| Pattern | Where it is loved | Tsukimichi today | Gap |
|---|---|---|---|
| Back and forward through what you looked at | Wikis, Garland Tools' linked pages, Allagan Tools' item windows | Many jump links (requirement arrows, Path, Unlocks, chat [Open]) but no way back | **Yes:** idea 2 |
| User-saved lists and views | Teamcraft lists, Allagan Tools custom filters and lists | Built-in quick views and presets only | **Yes:** idea 14 |
| "Missing, filtered by source" | [FFXIV Collect](https://ffxivcollect.com/) | Moonlit already does this | No |
| "What's new since you last looked" | Time Memoria V3 What's New | "New in 7.5x" group; Since you were away | Partly: folded into idea 1 |
| Session and lifetime pacing | Time Memoria V3; the old spreadsheet's hours | Completion dates are stored but no pace is shown | **Yes:** in idea 8 |
| Native-looking in-game panels | Time Memoria V3 (KamiToolKit); Dalamud#2874 | ImGui panels styled as Moon Road | Investigate: idea 17 |
| Visible privacy and trust | Community reaction to the stalker plugins | No network code, but nothing lets a player verify it | **Yes:** idea 11 |
| Onboarding, search, keyboard, accessibility, multi-character, notifications, sharing | Simple Tweaks' searchable list; Wotsit | Tour, Set up your road, Settings search, Ctrl+F, Esc order, hold or two-click safety, Text size, High contrast, Reduce motion, multibox, Copy for Discord | Little left. The one gap is an "anything" jump box: idea 15 |

## The candidates, ranked

Value is player value from 1 to 5. Effort is S, M or L. "Taste" says how each idea fits the owner's taste: calm, a static layout, no noise glyphs.

| Rank | Idea | Value | Effort | Recommend |
|---|---|---|---|---|
| 1 | Evercold day: adapt to 8.0 | 5 | M | **Yes** |
| 2 | Back and forward | 4 | S | **Yes** |
| 3 | A wider spoiler shield, before Evercold | 4 | M | **Yes** |
| 4 | Before Evercold: a quest-side prep card | 4 | S | **Yes** |
| 5 | The story's hidden requirements, and a true story meter | 4 | S | **Yes** |
| 6 | Duties board: roulette gaps and never-cleared duties | 4 | M | **Yes** |
| 7 | Loose ends: storylines you started and never finished | 4 | M | **Yes** |
| 8 | Your story on one page, with your pace | 4 | M | **Yes** |
| 9 | Who's in it: story characters in side quests | 4 | M | **Yes** |
| 10 | Where to get hand-in items | 3.5 | S–M | **Yes** |
| 11 | Trust you can check | 3 | S | Optional (cheap) |
| 12 | Alt goals: "catch this alt up to…" | 4* | M | Optional (*high for multiboxers like the owner) |
| 13 | Session recap: "Tonight's road" | 3 | S | Later |
| 14 | Saved views | 3 | S | Later |
| 15 | Go to anything (Ctrl+K) | 3 | M | Later |
| 16 | Quest achievements within reach | 3 | M | Later |
| 17 | A native-style Todo overlay (Dalamud native overlays) | 3 | L | Investigate on the API 16 track |
| 18 | Use the game's own Auto-Move for Walk to giver | 3 | L | 8.x, once it exists |
| 19 | Lore codex: people and places you've met | 3 | L | After idea 9, if wanted |
| 20 | Queue-safe quests (cutscene and voice markers) | 3 | L | Only after 8.0 data, as v6 decided |
| 21 | Try On for quest gear | 2 | S | Later |
| 22 | Levequest companion | 2 | M–L | No for now |
| 23 | Personal notes on quests | 2 | S | No for now |

### 1. Evercold day: adapt to 8.0 (value 5, M)

**What it is:** on 8.0 launch day, Tsukimichi already speaks the new expansion's rules:
- the item-level wall uses the account's highest job item level;
- levels go up to 110 everywhere;
- the new jobs' ladders work;
- copy about daily roulettes and tomestones moves to Adventurer Activity;
- Evercold's three story routes show as three parallel strands in Path, catch-up and "Up next";
- a one-time **"New in 8.0" card per character** shows the new main scenario, new unlock quests, new job and role quests and new Moonlit rewards, with a quiet "new" tint on those rows until first viewed.

**Evidence:** Eorzean Tavern (2026-09-17), PCGamesN (2026-07-25), Elyxir (2026-08-06) and ButWhyTho (2026-04), all linked in the Evercold table. Time Memoria V3's "What's New since baseline" shows players value a patch-diff view.

**Risk:**
- The route structure and the Adventurer Activity data are unknown until the 8.0 client exists.
- This must ride the API 16 branch.
- Tokyo (31 Oct) may change details.

**Taste:** no new band; the card uses the existing no-selection slot. The "new" tint is colour only, with no glyph, and fades on view.

### 2. Back and forward (value 4, S)

**What it is:** a navigation history for the selected quest and tab.
- Mouse buttons 4 and 5, Alt+← and Alt+→ step through what you looked at.
- A small ← appears in the detail pane's title line only while there is somewhere to go back to.

**Evidence:** Tsukimichi now has a dozen jump paths: requirement arrows, Path "Next", Unlocks, Moonlit "Also opens", chat [Open], the Duty Finder hint's Reveal, Wotsit and Chat 2. None of them can be undone. This is a standard pattern in Garland Tools-style linked browsing and in wikis.

**Risk:**
- Low. Keep history per window, capped at about 50 entries.
- The game also sees mouse buttons 4 and 5 (often bound to camera or macros), so act on them only while Tsukimichi's window is hovered.

**Taste:** no layout change. The arrow carries meaning and is absent when unused.

### 3. A wider spoiler shield, before Evercold (value 4, M)

**What it is:** the shield today hides main-scenario quest names ahead of you and journal art. This idea extends it to everything else past your story point: zone and aetheryte names in Unlocks, duty names, Moonlit reward names and icons, NPC names, and the Unlocks column. Each hidden item reads "An area of the next chapter". The release lands before Evercold's datamining wave.

**Evidence:** "Hide in-game spoilers … based on MSQ progression" ([SE forum 526605](https://forum.square-enix.com/ffxiv/threads/526605), 2026-02-22); v6's Must/L "Widen the spoiler shield" (community.md, streamers section) was never adopted.

1.12 made Unlocks (with zone names like Kugane) on by default and in more places. K2 says it "follows the spoiler shield", but only for quest names. This is the materially different part.

**Risk:**
- The spoiler point has to be resolved for every name source, which is a lot of call sites.
- The fix is to centralise it through one `SpoilerGate.Name(kind, id)` and add a lint test.

**Taste:** placeholders are words, not glyphs. Nothing moves.

### 4. Before Evercold: a quest-side prep card (value 4, S)

**What it is:** from early December 2026 until launch, the Tonight card (and the roster, once P3 ships) shows one calm checklist per character of what the game's quests need before 8.0:
- the main story finished through 7.56;
- journal room (C9's count);
- job and role quests caught up to 100;
- the unlocks that 8.0 roulettes or Adventurer Activity will likely ask for;
- Dawntrail flying.

Each line has its usual Route and Flag. The card disappears on launch day.

**Evidence:** [trozo.hateblo.jp 8.0 prep list](https://trozo.hateblo.jp/entry/FF14junbi8.0) (2026-04-08: discard unneeded side quests, finish class quests, finish the 7.5x story); v6's "Ready for Evercold" and "Evercold prep" proposals were never adopted; catch-up threads in community.md.

**Risk:**
- Small, because almost every line reuses existing data (catch-up, ladders, C9, Flight).
- Wording about roulettes depends on Tokyo.

**Taste:** a card in the existing slot, dated, and gone by itself.

### 5. The story's hidden requirements, and a true story meter (value 4, S)

**What it is:**
- Catch-up and the status-bar MSQ pill count the side quests the story requires: the Crystal Tower series, and the Shadowbringers role quests the wiki treats as mandatory.
- The MSQ pill's tooltip gives an honest story percentage, plus "next milestone in N quests" (spoiler-safe).
- A heads-up appears before the main scenario goes red: "The story will need 12 Crystal Tower quests at Lv 50, 3 alliance raids."

**Evidence:**
- [SE forum 484482](https://forum.square-enix.com/ffxiv/threads/484482) (2023-06-23);
- the [Console Games Wiki MSQ count](https://ffxiv.consolegameswiki.com/wiki/Main_Scenario_Quests) ("92 quests if mandatory sidequests are included");
- Simple Tweaks' MSQ percentage bugs ([#1005](https://github.com/Caraxi/SimpleTweaksPlugin/issues/1005), 2026-03-09; [#912](https://github.com/Caraxi/SimpleTweaksPlugin/issues/912));
- v6's "Story-required side quests" (Should/S) and "Story meter", which the critic flagged as dropped without a reason.

**Risk:** small. MsqCatchUp already walks prerequisites; this only follows them outside journal sections 0 and 1.

**Taste:** a tooltip and one optional line, no new band.

### 6. Duties board: roulette gaps and never-cleared duties (value 4, M)

**What it is:** a card on the Characters dashboard and in My blues answering "why is my Level Cap, Trials or Alliance roulette locked?". It lists each locked roulette's missing duties, the quests that unlock them, and Route / Pin all.

A second list shows **unlocked but never cleared** duties, which mentors and completionists track by hand.

The Duty Finder hint also answers when a roulette row is selected; today it returns nothing for roulettes (DutyFinderHint.cs:287).

**Evidence:**
- [1o421cx](https://www.reddit.com/r/ffxiv/comments/1o421cx/) (2025-10-11, with follow-ups to 2026-03-26);
- the mentor tracker website ([1tcqo1z](https://www.reddit.com/r/ffxiv/comments/1tcqo1z/), 2026-05-14);
- v6's "Roulette unlock checker" and "Unlocked, never cleared", neither adopted.

**Risk:**
- 8.0's Adventurer Activity may change what roulettes are for, though roulette pools themselves aren't announced as removed. Recheck after Tokyo.
- Duty-clear flags come from the game's instance-content state, which works only for the logged-in character; alts read from their last snapshot.

**Taste:** lives inside existing cards. Lists show what's left, not tallies.

### 7. Loose ends: storylines you started and never finished (value 4, M)

**What it is:** one list of every line you started and haven't finished:
- job and role ladders;
- curated chains;
- Story sidequests;
- the Side Story Quests category.

Each row shows "N left", the next quest's state, Flag / Go to giver / Send to Questionable. **Finale quests the game marks only in yellow** (the Shadowbringers job epilogues, the Endwalker role finale, One Final Journey, the Void quests) rank first. An optional notice says when such a finale becomes Ready.

**Evidence:** [r/ffxiv 1ll418m](https://reddit.com/r/ffxiv/comments/1ll418m/) (2025-06-26: "I had to look up all of the past job NPC's last co-ordinates"); [1vf8ynp](https://reddit.com/r/ffxiv/comments/1vf8ynp/) (2026-08-04); [1uum2mn](https://reddit.com/r/ffxiv/comments/1uum2mn/) (2026-07-12).

**Overlap:** P5 adds named side-story questlines and "Caught up" for ongoing series. Loose ends is the cross-cutting "started, unfinished" view over all of them, plus finale detection. Build it with P5.

**Risk:** small, since the chains exist. "Started" needs a sane threshold so a single intro quest doesn't count.

**Taste:** a dashboard card and an optional overlay section (off by default).

### 8. Your story on one page, with your pace (value 4, M)

**What it is:** the beloved community spreadsheet, rebuilt from your own journal. It is one spoiler-safe scroll by expansion and patch: the main scenario, with each optional line (raids such as Coil, Hildibrand, allied societies, job and role quests, optional dungeons) placed at the story quest that opens it, all checked off live.

A pace line uses your stored completion dates ("about 9 evenings at your recent pace"). It never appears with fewer than about 15 dated quests. The page copies as a printable checklist.

**Evidence:**
- "Are there any current quest trackers for 7.5 Dawntrail in spreadsheet format?" ([1vh6nzo](https://www.reddit.com/r/ffxiv/comments/1vh6nzo/), 2026-08-06);
- the original 688-point tracker [10tdu0k](https://www.reddit.com/r/ffxiv/comments/10tdu0k/) and repeated re-upload requests (2024, in community.md);
- Time Memoria V3's session and lifetime pacing (2026).

**Risk:**
- Placing each line at its opening quest uses the unlock-route engine and is cheap.
- The pace estimate must say "estimate" and hide itself when data is thin.

**Taste:** a mode in My blues ("By story order") or a Plan view, not a new tab, using the moons and the existing Before you continue markers.

### 9. Who's in it: story characters in side quests (value 4, M)

**What it is:**
- From each quest's script actors, Tsukimichi knows which recurring story characters appear in it (Alisaie, Thancred, Wuk Lamat…).
- A tooltip line and a "With story characters" quick view answer "which side quests are MSQ-related?".
- The detail pane gets a short Cast line.
- It names only characters you have already met in the story ("a familiar face" otherwise).

**Evidence:** [1vf8ynp](https://reddit.com/r/ffxiv/comments/1vf8ynp/) (2026-08-04: picks sorted by "Scions or major character from the MSQ involved"); [1oiva8z](https://reddit.com/r/ffxiv/comments/1oiva8z/) (2025-10-28); FOMO about missing story ([1vmj8qe](https://www.reddit.com/r/ffxiv/comments/1vmj8qe/), 2026-08-12); the lore-companion request ([goatcorp#1071](https://github.com/goatcorp/suggestions/issues/1071), 2026-03-04).

**Risk:**
- Script actors can include non-speaking extras, so recurring characters are derived as those appearing in at least about 8 main scenario quests.
- The "met" rule prevents spoilers.

**Taste:** no glyph after names. The cast lives in the tooltip, the detail pane and a quick view.

### 10. Where to get hand-in items (value 3.5, S–M)

**What it is:** each item in the detail pane's Hand in section gets a "Where" line:
- gathered: zone, node level, and a Flag that marks the area;
- sold by: vendor, with Flag / Teleport;
- crafted: the recipe's job and level;
- or "market board only".

It also says which container the game counts (inventory only, not the armoury or retainers) when you hold the item elsewhere.

**Evidence:** [1vq686p](https://www.reddit.com/r/ffxiv/comments/1vq686p/) (2026-08-16); [InventoryTools#347](https://github.com/Critical-Impact/InventoryTools/issues/347) (2026-01-02); v6's Must/S "Say where hand-in items are and which container the game counts", never adopted.

**Risk:**
- Gathering-point positions come from game sheets; some are approximate.
- Keep it to facts from the data, not advice.

**Taste:** one muted line under each item, with the buttons in the existing row menu.

### 11. Trust you can check (value 3, S)

**What it is:**
- Each GitHub release gets a build attestation and SHA-256 hashes.
- The Settings footer reads "Built from v1.x @ abc123 · no network code", with how to verify it.
- A CI test fails the build if any `System.Net` / `HttpClient` type is referenced.

**Evidence:** the 2026 stalker-plugin wave (Memoria [1ulptqq](https://www.reddit.com/r/ffxiv/comments/1ulptqq/), 2026-07-02; [1v3up9u](https://www.reddit.com/r/ffxiv/comments/1v3up9u/); EchoVault [1v8o2m1](https://www.reddit.com/r/ffxiv/comments/1v8o2m1/), 2026-07-28); v6's critic: "dropped without comment".

**Risk:** none for players; this is release-workflow work only.

**Taste:** one footer line and no UI elsewhere.

### 12. Alt goals: "catch this alt up to…" (value 4 for multiboxers, M)

**What it is:**
- Pick a goal per alt: "Dawntrail flying", "all roulettes open", "match Michiru's unlocks", "story to 7.56".
- The roster (P3) shows what's left for each alt toward its goal.
- One click sends the alt's list to Questionable when that alt is logged in.

**Evidence:**
- v6's "Alt goal templates and 'Catch up to…' from Compare" and "Catch this alt up to my main", neither adopted;
- the owner multiboxes a bard with 8 alts (v6 decision 11/13);
- the r/ffxivdiscussion consensus that catching alts up is the main reason people use Questionable (community.md, 1wf3rnt, 2026-09-13).

**Risk:** goals must be read-only for alts that aren't logged in. Build it on P3.

**Taste:** a column on the roster, not a new window.

### 13. Session recap: "Tonight's road" (value 3, S)

**What it is:** on demand, or optionally at logout, one card shows what you did this session: quests done, what they unlocked, what is newly Ready, and new Moonlit rewards. It copies for Discord.

**Evidence:** Time Memoria V3's session pacing (2026); 1.14's run receipt (A4) does the same for automated runs only.

**Risk:** low.

**Taste:** a card in the no-selection slot, never a popup.

### 14. Saved views (value 3, S)

**What it is:** name and save the current scope, filters, sort and columns ("Emote hunt", "Alt catch-up"). They appear in the quick views control under the built-in ones.

**Evidence:** the custom lists in Teamcraft and Allagan Tools.

**Risk:** settings migration and the column-width interplay with U9.

**Taste:** lives inside the existing control. The fixed-height lane is unchanged.

### 15. Go to anything, Ctrl+K (value 3, M)

**What it is:** one floating box that jumps to any quest, zone, chain, NPC, Moonlit reward, setting or command, with keyboard selection.

**Evidence:** Wotsit's popularity (and its 26-match cap, which Tsukimichi already works around); Teamcraft's search-first home page.

**Risk:** overlaps Ctrl+F and Settings search. Make Ctrl+K the superset and keep the others.

**Taste:** a transient overlay with nothing permanent added.

### 16. Quest achievements within reach (value 3, M)

**What it is:** the quest-count achievements (regional sidequest sets, Tales of…, "complete N quests") with the quests still missing for each, as an extension of the Achievement ladders shipped in 1.9.

**Evidence:** players use FFXIV Collect's achievement list as a stand-in for a content checklist ([1vh6nzo comment p22ojz5](https://www.reddit.com/r/ffxiv/comments/1vh6nzo/), 2026-08-06).

**Risk:** the game loads achievement progress only after the Achievements window opens, so this needs the existing "open once" prompt.

**Taste:** fits inside Story chains on Characters.

### 17. A native-style Todo overlay (value 3, L)

**What it is:** on API 16+, an option to draw the Todo overlay as a native game window through Dalamud's native overlays. It would hide with the game's UI toggle and sit in the HUD layout like a game element. ImGui stays the default.

**Evidence:** [Dalamud#2874](https://github.com/goatcorp/Dalamud/pull/2874) (2026-07-04); Time Memoria V3 ships native windows with an ImGui fallback.

**Risk:**
- The PR isn't merged.
- KamiToolKit is a submodule dependency.
- Two render paths to maintain.

**Taste:** if it lands, it is the calmest possible overlay. Investigate only.

### 18. Use the game's own Auto-Move (value 3, L)

**What it is:** Walk to giver uses Evercold's Auto-Move / Auto-Guide where the game supports the quest, and falls back to vnavmesh.

**Evidence:** mein-MMO (2026-07-27) and the MMORPG.com live blog; v6's "Work alongside the game's own quest auto-path" (Could/L).

**Risk:** not at 8.0 launch; coverage grows patch by patch. Wait for the 8.x data.

**Taste:** invisible. It is the same button.

### 19. Lore codex: people and places you've met (value 3, L)

**What it is:** built on idea 9's cast data, a browsable "who's who" of recurring characters and places. Each entry lists the quests where you met them and links to the journal text Tsukimichi already shows, filtered to what you've done.

**Evidence:** [goatcorp#1071](https://github.com/goatcorp/suggestions/issues/1071) (2026-03-04); the SE forum asking for the Unending Codex early ([526605](https://forum.square-enix.com/ffxiv/threads/526605), 2026-02-22).

**Risk:** the game has no lore descriptions, so curated text would be a large and English-only effort. Keep it to "where you met them".

**Taste:** could be beautiful as a quiet archive page, but it is scope creep.

### 20. Queue-safe quests (value 3, L)

**What it is:** marks for quests with and without cutscenes, and voiced or not, with a "Safe while queued" quick view.

**Evidence:** r/ffxivdiscussion [1mgk73x](https://www.reddit.com/r/ffxivdiscussion/comments/1mgk73x/) (2025-08-04), "I don't even use plugins but if this could be done, it'd certainly entice me".

**Risk:** the data derivation is uncertain, and v6 already decided to "wait for the 8.0 data". It stays deferred.

### 21. Try On for quest gear (value 2, S)

**What it is:** "Try On" on any gear reward, using the game's own Try On window.

**Evidence:** v6 glamour research (community.md, glamour section).

**Risk:** low.

**Taste:** one menu entry.

### 22. Levequest companion (value 2, M–L)

**What it is:** allowances, done/undone leves and leve achievements.

**Evidence:** [InventoryTools#347](https://github.com/Critical-Impact/InventoryTools/issues/347); the 8.0 prep list's "keep 100 allowances".

**Risk:** a separate game system and scope creep. Not recommended now.

### 23. Personal notes on quests (value 2, S)

**What it is:** a free-text note on any quest or pin.

**Evidence:** weak; only the existing Moonlit verdict notes suggest a habit. Not recommended now.

## Considered and left out

- **Sharing quest progress with friends or a party** (QuestShare-style, or offline share codes). The v6 plan already rejected reading other people's data, and July 2026's privacy backlash makes it worse.
- **Giver face portraits.** This is already the owner's own point 4, so it is not proposed here.
- **Field aether current locations.** Flight deliberately points at the Aether Compass, and recent threads were about quest currents, which are covered.

## Recommendation for plan v7: the top 10, with suggested releases

| # | Idea | Suggested release | Why then |
|---|---|---|---|
| 2 | Back and forward | 1.14 (beside the owner's UI points) | Small, and helps every other jump |
| 11 | Trust you can check | 1.14 (release workflow) | Cheap; 2026 privacy climate |
| 5 | Story's hidden requirements and a true story meter | 1.15 (right answers) | A correctness gap in catch-up |
| 10 | Where to get hand-in items | 1.15 | Sits with C6/C8 data work |
| 6 | Duties board | 1.15 | Duty data already loaded; recheck roulettes after Tokyo |
| 7 | Loose ends | 1.16 (with P5) | Same chain data as P5 |
| 8 | Your story on one page, with your pace | 1.16 (with P1) | The "what next" release |
| 9 | Who's in it | 1.16 | Needs a DataGen pass, so do it once |
| 4 | Before Evercold prep card | 1.17, ship by early December 2026 | Must arrive before 22 Jan 2027 |
| 3 | A wider spoiler shield | 1.17, before Evercold | Before 8.0 names leak through Unlocks |
| 1 | Evercold day | API 16 / 1.18, January 2027 | On launch; recheck after Tokyo, 31 Oct to 1 Nov |

That is 11 items, because Trust you can check is too cheap to leave out. Alt goals (12) is the best next pick if the owner wants the roster (P3) to go further for their own eight alts.

# 1.19 "Right answers": the correctness surfaces

This spec designs the UI for the 1.19.0 rows of `docs/feature-plan-v7.md`:

| Row | Surface |
|---|---|
| **C1** | The game confirms it: "Offered by the game", "seen in game", and the disagreement card |
| **C3** | Gates Tsukimichi can't check |
| **C4** | New Game+ in the status bar and the Replaying chip |
| **C5** | Allied societies: a carried-over daily, rank-up hints, stored alts |
| **C6** | Rewards you can buy back |
| **C7** | How you'll clear it: duty badges and the item-level wall |
| **C8** | Where the EXP goes, and Switch gearset for job quests |
| **C9** | The journal count and Make room |
| **C10** | Events: ending soon, dated reruns, Set end date |
| **K3** | Find by unlock, the Unlocks filter, Route to unlock |
| **N3** | The story meter |
| **N4** | The Duties board |
| **N5** | Where to get hand-in items |

The behaviour follows the C1 to C10 and K3 entries of `docs/research/plan-v6/plan-synthesis.json` and items 5, 6 and 10 of `docs/research/plan-v7/feature-ideas.md`. Where this spec changes a synthesis detail, the Decisions section says so.

**The principles:**
- **Nothing new floats.** Every 1.19 surface is a line, chip or section inside a card, pane or bar that already exists. Only Make room is new, and it is a popover.
- **Static layout.** Lines are present or absent per quest; nothing appears or reflows while you look at it. Hover actions take a reserved slot.
- **Plain words first.** Every signal is a word. Colour backs it up. There are no warning glyphs.
- **Never a confident wrong answer.** What Tsukimichi can't know says so ("can't check"), and every claim names its source (the game, the wiki, your entry).
- **Tsukimichi never abandons a quest.**

**Files**

| File | What it is |
|---|---|
| `spec-1.19.md` | This spec |
| `mock.html` | `#detail19`, `#rows19`, `#makeroom`, `#boards19`, `#looks19`. Sources: `mock-src/v719.js` and `v719.css`. |
| `detail-1.19.png` | The detail pane, Full on Night: Into the Aery (C1, C7, C8), Knocking on Heaven's Door (C3), Looking for Some Hot Stuff (C9, C8, N5), Two Nations, One Seed (C6), Sellspade (N5), the disagreement card (C1) and N5's actions by automation level |
| `rows-1.19.png` | Table rows with the new chips, the status bar at 22, 28 and 30 slots (on four palettes) and in New Game+, and the story meter hover |
| `make-room.png` | Make room at Full Night, Quiet Ishgard Snow, Plain Kugane Lacquer and Quiet Dawn |
| `boards-1.19.png` | The Duties board, Allied societies, the event card and Seasonal events list, the Unlocks search group, the Unlocks filter and the Route window |
| `looks-1.19.png` | Requirements and How you'll clear it at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer |
| `1.15/icons/000023, 000084, 060412, 062115, 062122.png` | New game icons: Gathering Log, New Game+, shop, Culinarian, Dragoon |

**The mock's data is real.** Quest names, levels, previous quests, places, reward gil, item levels, hand-in items, recipes, shop prices, duty unlock quests and allied society ranks were read from the game's sheets with Lumina. The EXP figures use the formula in `Tsukimichi.Core/Rewards/QuestExp.cs`: Into the Aery is 400 × 39 × 325 / 100 = 50,700 EXP, 5% of the 927,000 that Lv 56 needs. For Grilled Sweetfish, the vendor (Kurogai) and the price (1,053 gil) come from the shop sheets. The vendor's zone is left out because that lookup didn't resolve. The dates, item levels of the player's gear, ranks and counts are an example character.

---

## Colour language

There are no new tokens. 1.19 uses only what 1.16 to 1.18 defined and gated.

| Meaning | Token | Where in 1.19 |
|---|---|---|
| Met | the Requirements check (Text) | Requirements rows |
| Unmet | `--etext` (Eclipse text: rose on dark palettes, plum on Snow) | the unmet detail, the item-level wall line, as the existing `.req .d.ec` |
| Can't check | `--dusk` (Tertiary) and a hollow 9 px ring, 1.5 px stroke | the gate row, its section caption, the row status |
| Needs you | `--attn` copper, **only as a 6 px dot** beside Text-coloured words | journal full (row, hero, status bar), a carried-over allied daily |
| Ending soon | the 1.18 attention card (`tone-attn`, a 3 px copper bar) | the event card, only with an event quest in the journal |
| Finished or act now | gold (`--goldtx` on Snow) | Ready, the primary pill |
| Done | silver | Completed in the hero |

**The copper rule stays as ruled in 1.18:** copper is never the only carrier. Every copper dot sits beside words that say the same thing. In 1.19 the words are in Text, not copper. That is a little stricter than 1.18's stop card.

**The synthesis asked for amber at 27 and red at 30 slots.** This spec uses Text at 27 to 29 and copper at 30. Amber is the Settings hint, and red/plum means Locked out and destructive. A full journal is neither: it needs you.

---

## C1. The game confirms it (`detail-1.19.png`, `rows-1.19.png`)

### In the hero

The status line under the title gains one item after the state:

`Ready  ◉ Offered by the game · 3 Oct`

- The quest-marker icon (071201) at 12 px, then "Offered by the game · <date>" in Secondary.
- The date is the first time the game offered it to this character (a map marker or a JournalAccept offer). The format is the short date of the client's culture, with no year unless it differs from this year.
- It shows only when a Not checked quest turned Ready because of an offer, or when an offer agrees with Ready. Missing markers never show anything.

### In the table

A Ready row the game offered gets "seen in game" at the trailing end of the row, in Tertiary, 11 px, with no chip. The hover says "The game offered this quest on 3 Oct."

### When the game disagrees

A Blocked quest the game offered gets "game disagrees" at the trailing end of the row: Secondary, 11 px, with a 1 px dotted underline in `--vline`. The hover says "The game offered it; Tsukimichi expects The Narwhal Beckons first."

In the detail pane, the 1.18 attention card sits above Requirements, in the `tone-you` keyline (the plain card: no copper, since nothing is urgent):
- **Title:** quest-marker icon, then "The game and Tsukimichi disagree".
- **Why:** "The game offered **Sleepless in Ishgard** on 3 Oct, but Tsukimichi has it as Blocked: it expects **The Narwhal Beckons** first."
- **Context:** "One of us is wrong. The game sees your character; Tsukimichi's data may be out of date."
- **Actions:** **Go with the game** (primary pill) and **Copy report** (the 1.18 copy link, with the same report block and the C1 lines added).
- **Hint:** "Shows it as Ready on this character until the data agrees. Undo any time from the quest's "…"."

Go with the game is a per-character override stored in the OfferObservations sidecar, never in the shared data. It is undone from the "…" menu ("Use Tsukimichi's answer"). The disagreement is always logged for `/tsuki why` and the report, whether or not the player picks.

---

## C3. Gates Tsukimichi can't check (`detail-1.19.png`, `looks-1.19.png`)

### The Requirements row

A third verdict joins check (met) and cross (unmet): a **hollow ring**, 9 px, 1.5 px stroke in `--dusk`, aligned with the other marks.
- **Label:** the gate in plain words, in Text: "Palace of the Dead, floors 41–50".
- **Detail (trailing):** "can't check" in `--dusk`.
- **Source line** (11 px, `--dusk`): "The game doesn't show plugins this. From the wiki, confirmed by 2 sources." Gates with fewer than two sources are not shipped (the synthesis rule).
- **Quiet actions** (the 1.18 quiet buttons, 24 px tall):
  - **I've done this** marks the gate met for this character. The row turns into a check with "you said so" as its detail, and Undo comes from the 8 s toast.
  - **Where to start** selects the gate's duty or unlock quest in the table, if Tsukimichi knows one.
- **Section caption:** "1 can't be checked" in `--dusk`. When a can't-check gate and an unmet one coexist, the caption says "1 unmet · 1 can't be checked".

### The quest's state

A quest whose only open gate can't be checked keeps the existing **Not checked** moon. Its row status reads "Can't check · <gate>", and its hero reads "Can't check · 1 gate" in `--dusk`. After **I've done this**, it reads Ready as usual.

Bozja Resistance Rank, Occult Record, relic tools (the "held" gate) and deep-dungeon floors all use this row.

---

## C4. New Game+ (`rows-1.19.png`)

### In the status bar

While a New Game+ session is active, the status bar's left side is replaced, without changing the bar's height:
- the New Game+ icon (000084) at 16 px;
- "New Game+ · Shadowbringers – Part 2 · quest 87 of 112", in Text, using the chapter names from `NewGamePlusQuests`;
- "Your saved progress is kept", in Secondary;
- **End session**, a quiet button.

End session says what it does in a 1.18 Confirm popover ("Leave New Game+ in the game first. This only stops Tsukimichi's replay mode."). It never touches the game: Tsukimichi follows the game's NG+ state, and the button exists only for the case where detection is stuck.

### In the table

A re-completed quest keeps Completed (silver, with its first date) and gains a **Replaying** chip at the trailing end: the 1.14 chip, Secondary text, a `--line` outline. Notices and "Opened" lines are suppressed for replays. Tonight and the Todo overlay show the same one line as the status bar.

---

## C5. Allied societies (`boards-1.19.png`)

These are lines in the Characters dashboard's Allied societies card. Each row keeps its society icon (22 px tile), "Name · Rank N", and one line under it.

| Case | Line | Style |
|---|---|---|
| A daily accepted before the reset | "0 allowances until you turn in **Moogle on the Wall** (accepted before the reset)" | a copper dot, Text; **Flag** and **Teleport** quiet buttons at the row's end (hidden at Tracker only, Flag kept) |
| Rank-up ready | "Rank-up ready: keep 3 allowances for the bonus dailies" | Secondary |
| A stored alt holding an old daily | "0 allowances today: holds a daily from before the reset (last login 2 days ago)" | Secondary; the alt's name after the rank in Tertiary |
| A stored alt, clear | "12 allowances today (projected from the last login, 2 days ago)" | Secondary |

The "Ranked up today" tooltip is fixed only after the in-game check that the synthesis asks for.

---

## C6. Rewards you can buy back (`detail-1.19.png`)

This is in the Rewards section of a completed quest. Each item row keeps its reward chip (uncommon items keep their keyline). A state line follows the chip on the same row, in Secondary:

| State | Line | Actions |
|---|---|---|
| Glamour you picked and no longer hold | "You picked this · not on you · buy it back from a **Calamity Salvager**" | Flag, Teleport (to the nearest Salvager) |
| A roll, minion, card or emote book | "Done, not learned · reclaim at the **Recompense Officer**, 100 gil" | Flag, Teleport |
| Held somewhere else | "In your armoury chest" (from Allagan Tools when it is installed) | none |
| No buy-back row in the data | "Not offered by the Salvager" | none |

- The section caption says "you picked 1 of 5" for optional rewards.
- A price appears only when the shop row has one. **Never claim a re-buy the data doesn't show.**
- The item hover hint and context menu gain one line: "Re-buyable · 100 gil".

---

## C7. How you'll clear it (`detail-1.19.png`, `looks-1.19.png`, `boards-1.19.png`)

### The section

"How you'll clear it" is a new detail section, placed after Requirements and before Rewards. It shows only when the quest involves a duty. The caption is "1 duty" or "N duties".

Each duty is a line: its name in Text, then badges.

### Badges

The badge is the 1.14 chip with a 14 px game icon, 20 px tall, Text label, `--line` outline. The first badge is the size, then the content's role, then Story-required or Optional.

| Badge | Icon | When |
|---|---|---|
| **Solo with NPCs** | Duty Support (000089) | the duty has Duty Support or Trust |
| **Group of 4 / 8 / 24** | Duty Finder (000046), or the alliance icon (000017) for 24 | otherwise; the roulette's name is in the badge hover |
| **High-end** | none | Savage, Extreme, Ultimate, Unreal and Chaotic |
| **Story-required** / **Optional** | none, Secondary text | from the main scenario chain |

### The item-level wall

A line under the badges, in `--etext`, appears only when the wall bites: "i110 needed · you're i108 (DRG) · WAR gearset i112". Under it, in Tertiary: "Your SGE (i705) also qualifies. Duty Support checks item level too."
- **Before 8.0:** the current job's equipped average (main hand counted twice without an off-hand) and the best gearset that qualifies.
- **From 8.0** (Evercold shares item level across jobs): the line becomes "i110 needed · you're i108" with no job names.

There is **no Switch gearset button for the wall.** Switch gearset is only for job-locked quests (C8).

The same badges appear wherever duties appear: My blues, routes, the Duty Finder hint, catch-up and the Duties board (N4). The catch-up and Tonight say "Ahead: 3 story duties need other players".

---

## C8. Where the EXP goes, and Switch gearset (`detail-1.19.png`)

### The EXP line

This is a line under the Rewards headline ("50,700 EXP · 5,000 gil"), with the current job's icon (14 px tile):

`Hand in on DRG Lv 56: 50,700 EXP (5% of a level) · your SGE is capped, 0`

- The numbers come from `QuestExp.For` at the job's level. A Quest Sync quest shows the clamped value for this job.
- The percentage is EXP ÷ `ParamGrow[level].ExpToNext`, rounded to a whole percent. Under 1% shows as "<1%".
- The capped clause appears only when the player has a capped job. It names the job they used most recently.
- Allied society and seasonal quests show no EXP line (`ExpKind.Unknown`).

**The Todo row:** when an In journal quest reaches its turn-in step on a capped job, a Todo row reads "Turn in on a job that isn't capped: Into the Aery". An optional chat line exists, off by default.

### Switch gearset

This appears only on quests that **require a specific class or job** (`ClassJobRequired`), and only when you are on another job. It sits as a small pill under the unmet Job requirement:

`✕ Job        Culinarian · you're on Dragoon`
`   [◈ Switch gearset: Culinarian]`

- The pill is the 1.18 small pill with the job icon, and it calls `RaptureGearsetModule` for the first gearset of that job.
- If the player has no gearset for the job, the pill is replaced by the words "No Culinarian gearset saved".
- Switch gearset is a game UI action, so it is allowed at every automation level.

---

## C9. The journal count and Make room (`rows-1.19.png`, `make-room.png`)

### In the status bar

The count reads the accepted quests, without the MSQ, as the game counts them.

| Count | Status bar | Hover |
|---|---|---|
| 0–24 | nothing | — |
| 25–26 | "Journal 25/30" in Secondary | "5 slots left" |
| 27–29 | "Journal 28/30" in Text | "2 slots left" |
| 30 | copper dot · "Journal full · 30/30" in Text · **Make room** (quiet button) | "Ready quests can't be accepted until you finish or drop one" |

The Todo overlay shows the same item from 25, if the player turns that on (off by default).

### In rows and the hero

- **Rows:** a Ready quest reads "Ready · ● journal full", with the copper dot before "journal full" and the words in Text.
- **Hero:** "Ready · journal full", then a copper dot and "Make room to accept it" as a link that opens Make room.

### Make room

Make room is a popover anchored to its trigger: the status-bar button or the hero link. It is 520 px wide and fits its content up to 70% of the window height, scrolling after that.

The anatomy:

1. **Title:** "Make room". At Full it uses the display font and gold (Trump Gothic, as section headers do). At Quiet and Plain it is Text, semibold.
2. **Subtitle:** "30 of 30 slots. Finish one, or drop one in the game's journal."
3. **Groups**, in this order, each with a header (semibold) and a Tertiary sub-label. Empty groups are hidden.

   | Group | Sub-label | Contents |
   |---|---|---|
   | Finish now | one talk or delivery left | In journal quests at their last step, when it is a talk or a delivery |
   | Needs a duty | solo or with NPCs | a solo duty, or Duty Support |
   | Needs an item | to hand in | items the player doesn't have yet |
   | Needs a group | other players | a duty with other players |
   | Safe to drop | still on step 1: nothing to redo if you take it again | see below |

4. **Each row** is 30 px: the In journal moon (16 px), the name, what finishing takes in Tertiary ("a talk · Old Sharlayan", "Jeuno: The First Walk · 24 players"), and a "safe to drop" chip where it applies. A **reserved fifth column** (112 px) holds **Open in journal** on the hovered or keyboard-focused row, so nothing shifts.
5. **Footer note:** "Tsukimichi never abandons a quest. **Open in journal** shows it in the game, where you drop it and the game asks first. Tsukimichi notes the step you had reached."

**What "safe to drop" means.** All of these must hold:
- the quest is on its first step, so the game's restart costs nothing;
- it holds no key item that dropping would take away;
- it is not a daily, an allied society quest or a timed event quest (dropping those can cost an allowance or the event);
- it is not the main scenario (the game doesn't allow that anyway).

**Safety.** No button in Tsukimichi abandons a quest. Open in journal opens the game's Journal at that quest (`AgentJournal`'s open-for-quest call). The player abandons with the game's own button and its own confirmation. That is the explicit, confirmed action, and it is the game's. The Abandoned ledger records the step that was reached.

**Motion.** The popover uses Rise (0.16 s, 4 px) to open and Leave (0.12 s) to close. A hovered row fades its background with HoverIn and HoverOut. Open in journal takes the reserved slot instantly, with no slide.

---

## C10. Events (`boards-1.19.png`, `rows-1.19.png`)

### The ending-soon card

This is the 1.18 attention card in Tonight and the notice dock. It shows **only when the end date is known** and the event ends within N days (Settings › Seasonal: "Warn before an event ends", default 3 days; notices themselves stay off until turned on).
- **Title:** the event icon, then "All Saints' Wake ends in 2 days". On the last day it says "ends today, 07:59".
- **Why:** "1 quest in your journal, 4 rewards you don't have."
- **Context:** "Ends 3 Nov, 07:59 local · runs every October."
- **Action:** **Show the event** (primary pill).
- **The copper bar** appears only when an event quest is in the journal. Otherwise the card is the plain keyline.

### In the table

An event quest in the journal gets the chip "Ends in 2 days" (the 1.14 chip) and sorts first in Todo and Tonight.

### The Seasonal events list

This is in the Characters dashboard. Each event gets one line:
- "running · end not announced" with **Set end date…** (a quiet button that opens a date field);
- "ends 20 Oct · you entered this" for a date the player set;
- "ended · usually August · last ran 2026", or the dated runs from `festivals.json` in the hover ("12–26 Aug 2025, 14–28 Aug 2024").

---

## K3. Find by unlock (`boards-1.19.png`)

### The toolbar search

The search gets an **Unlocks** group above Quests when the text matches an unlock name. Its header uses the same small caps as Quests. Each result row has:
- a kind chip (Flying, Area, Mount, Duty, Feature, Job, Emote, Orchestrion);
- the unlock name in Text;
- "via <quest>", or a summary ("10 aether currents, 4 from quests"), in Secondary;
- **Route to unlock**, a quiet button.

### The Unlocks filter

This is a group in the filter drawer's Advanced section, using the 1.14 filter chips: Mount, Flying, Duty, Feature, Job, Area, Emote and Orchestrion. With any chip on, the table keeps only quests that unlock one of those kinds. Below the chips: "Keeps quests that unlock one of these. Off: every quest." The group header counts what is on ("2 kinds").

### Route to unlock

This opens the existing Route window with the unlock as its header: the unlock's icon, "Route to Flying in Thavnair" and "10 stops". The stops are the remaining quests and aether currents in order. Field currents are listed as "Aether current · <nearest aetheryte>".

---

## N3. The story meter (`rows-1.19.png`)

This is the hover of the status bar's MSQ pill. The pill keeps its text ("MSQ · 742 of 1,038").
- **Title:** "Main scenario · 742 of 1,038 (71%)".
- "Counts the **side quests the story requires** too: the Crystal Tower series and the Hard primal trials."
- "Next milestone in 6 quests."
- In Text, semibold, only when the player skipped something: "Still to do from earlier: the Crystal Tower series, 3 alliance raids at Lv 50. The story needs it later."

**Spoiler-safe:** past the player's story point, the meter shows counts and milestones, never names (N6 widens this in 1.20).

---

## N4. The Duties board (`boards-1.19.png`)

This is a "Duties" card in the Characters dashboard, after Allied societies. The caption reads "1 roulette locked", or nothing when all roulettes are open.

**A roulette block:**
- **Header:** the roulette's name (Text, semibold) and its state in Secondary at the trailing end: "locked · needs a Lv 100 job · best is BLM 98", or "open · 1 raid not unlocked".
- **One row per duty in it that isn't unlocked:** the name, the C7 badge, and "not unlocked · with **Beyond the Mountains**" (the unlock quest, from the quest's `INSTANCEDUNGEON` link).
- **Actions:** **Route** (the Route window over those unlock quests) and **Pin both** / **Pin all**.

**"Unlocked, never cleared":** the count in the caption, then the first 2 duties with badges and "12 more ›", which opens the full list in the same card.

Lists show what is left. They never show a tally of what is done.

---

## N5. Where to get hand-in items (`detail-1.19.png`)

The Hand in section of a quest gets one line per item under the name and count. Each source is one line with its game icon at 14 px; at most two sources show, joined by "· or".

| Source | Line | Actions |
|---|---|---|
| Crafted | Crafting Log icon · "Crafted · Culinarian Lv 55" | **Craft with Artisan** (the 1.18 small pill) |
| Sold | shop icon (060412) · "Sold by **Kurogai** · 1,053 gil" | Flag, Teleport |
| Gathered | Gathering Log icon (000023) · "Mined · the Gathering Log shows the nodes" | **Open Gathering Log** |
| Held in the wrong place | (no icon) "The game counts your **inventory** only: take it out of the saddlebag." | none |

- The count reads "0 / 1 · 1 in your saddlebag" when the item is elsewhere. It uses the game's inventory for the count and Allagan Tools (when installed) for the rest.
- **Gathered items name no node.** Tsukimichi opens the game's own Gathering Log at the item rather than guess a node or a time window.

**Actions by automation level** (1.18 rule: hidden above the level, never greyed):

| Level | Shown |
|---|---|
| Tracker only | Flag, Open Gathering Log |
| Travel | + Teleport |
| Travel and walking | + Teleport |
| Full hand-offs | + Craft with Artisan |

---

## Every look (`looks-1.19.png`)

The matrix shows Requirements (met, unmet and can't check) and How you'll clear it (badges and the wall) at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer. Everything is built from tokens each level already defines:
- **Full:** the framed card, Trump Gothic headers, gold header ink;
- **Quiet:** tonal cards and hairlines;
- **Plain:** ledger bands.

Badges and quiet buttons take each palette's Line and Text. The hollow ring takes `--dusk`.

`make-room.png` covers the popover on four palettes, and `rows-1.19.png` shows the journal-full status bar on all four.

The contrast pairs used here were gated in `1.16/contrast.md`, `1.17/contrast17.md` and 1.18:
- Text, Secondary and Tertiary on the card;
- `--etext` on the card;
- copper as a dot (a non-text 3:1 part).

There are no new pairs.

**High-contrast forms:** the ring becomes 2 px, and badges get a 1.5 px outline.

---

## Code map

| Piece | Where |
|---|---|
| Offer line, disagreement card | `DetailPane.Hero.cs`; card via `StopCardView`'s attention card; override in `OfferObservations` |
| "seen in game", "game disagrees", "Ends in N days", "Replaying" | the trailing cell of the table row (`QueryRunner` row model), chips via `Chrome.Filters` chip |
| Can't-check row | `DetailPane.Unmet.cs`: a third `RequirementVerdict.CantCheck` |
| How you'll clear it | new `DetailPane.Duties.cs`; badges in `JobBadges.cs`'s neighbour `DutyBadges` |
| EXP line | `DetailPane.Exp.cs` |
| Switch gearset | `Chrome.ActionPill(…, PillLayout.Row)` under the Job requirement |
| Hand-in sources | `DetailPane.HandIn.cs` |
| Rewards states | `DetailPane.Collector.cs` / `RewardTooltip.cs` |
| Journal count, NG+ segment | `MainWindow.Frame.cs` (status bar) |
| Make room | new `MakeRoomPopover.cs`, opened with `PopupFade` |
| Allied lines, Duties card, Seasonal list | `CharactersPane.Seasonal.cs`, a new `CharactersPane.Duties.cs` |
| Unlocks search group, filter | `Strings.Toolbar` / `FilterPanel.Drawer.cs` |
| Route to unlock | `RouteWindow.cs` header |
| Story meter | the MSQ pill's `HoverHint` |

---

## Decisions

1. **Nothing new floats.** Every surface is a line or chip in an existing card, except Make room (a popover).
2. **Can't check is a third verdict:** a hollow ring in Tertiary, with its source and "I've done this". It is never shown as unmet, and never Ready by default.
3. **The game's word wins only when the player says so.** Go with the game is per character and undoable. The disagreement is always logged.
4. **Copper is a dot, with words in Text.** It is used for a full journal and a carried-over allied daily. The ending-soon card uses the 1.18 copper bar only with a quest in the journal. This replaces the synthesis's amber at 27 and red at 30.
5. **Tsukimichi never abandons.** Make room groups, marks "safe to drop" by strict rules, and opens the game's journal. The game's own Abandon button and its confirmation are the explicit action.
6. **Switch gearset only for job-locked quests.** The item-level wall names a gearset in words, without a button.
7. **N5 names a node only through the game's Gathering Log,** and never guesses a node, a timer or a vendor that the data doesn't show.
8. **Spoiler-safe story meter:** counts and milestones past the player's story point, never names.

## Open questions

1. **Go with the game:** per character (my proposal), or account-wide once any character sees the offer?
2. **"I've done this"** for a can't-check gate: per character (my proposal), or shared across a Content ID's alts? Deep-dungeon progress is per character in the game.
3. **Make room "safe to drop":** is step 1 strict enough, or should it also allow quests whose remaining steps are all talks? I left those out because the game restarts the quest.
4. **The ending-soon default:** 3 days (from the synthesis), or 2 with a last-day line?
5. **End session for New Game+:** keep it as a recovery button, or hide it unless detection looks stuck for 5 minutes?
6. **N5's Market Board source:** leave it out (my proposal, since prices need a network service), or add "On the Market Board" with no price?


## Approval record (realism supervisor: APPROVED)

Coordinator decisions:
- "Go with the game" and "I've done this" apply per character.
- "Safe to drop" means strictly step 1.
- Events warn 3 days before they end.
- New Game+'s End session button always shows.
- No Market Board source, because its prices need a network service.
- Picked-reward chips (C6) use a Silver border. The gold dot appears only on the buy-back line, so gold keeps meaning "act now".

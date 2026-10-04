# 1.21 "What next, for every character": the guidance surfaces

This spec designs the UI for the 1.21.0 rows of `docs/feature-plan-v7.md`:

| Row | Surface |
|---|---|
| **P1** | Up next at the top of Tonight, and the way back to Tonight |
| **P2** | Go to the current step of a quest in your journal, and `/tsuki go` |
| **P3** | All characters: the roster board, and "your other characters" under the detail hero |
| **P4** | My blues: Do first, tiers and Set aside |
| **P5** | Named side stories and Caught up |
| **P6** | Triple Triad opponents and the quests behind them |
| **P7** | The zones board: Nearby's Everywhere view |
| **P8** | `/tsuki msq`, `/tsuki next` and `/tsuki go` in chat, for text-to-speech |
| **N8** | Loose ends |
| **N9** | Your story on one page, with your pace |
| **N10** | Who's in it |
| **N11** | Alt goals |

The behaviour follows the P1 to P8 entries of `docs/research/plan-v6/plan-synthesis.json` and ideas 7, 8, 9 and 12 of `docs/research/plan-v7/feature-ideas.md`. Where this spec changes a synthesis detail, the Decisions section says so.

**The principles:**
- **One answer first.** Tonight leads with one recommendation and says why in words. Everything else stays where it was.
- **Nothing new floats, and nothing moves.** Every 1.21 surface is a block, card, column, line or view inside a window that already exists. Rows have fixed heights, hover actions take a reserved slot, and a set-aside row keeps its place until the list is rebuilt.
- **Show what is left.** Counts say "13 left", "91 to the latest story", "caught up". There are no done/total tallies and no percentages (the critic's check on P4 and P8).
- **Never a spoiler.** Every surface that names a quest, zone, duty, opponent, reward or character uses 1.20's shield (N6), in words, and never shows a face it would hide.
- **Other clients are read-only.** Tsukimichi in one client never writes another client's character or starts a hand-off for it.

**Files**

| File | What it is |
|---|---|
| `spec-1.21.md` | This spec |
| `1.21/mock-1.21.html` | The 1.21 mock: `#tonight21`, `#step21`, `#roster21`, `#blues21`, `#stories21`, `#boards21`, `#chat21`, `#looks21`. It is built from the v7 mock sources, read-only, plus the 1.21 layer, so it never touches `mock.html`. |
| `1.21/mock-src/` | `v721.js` and `v721.css` (the 1.21 layer), `build21.py` (`py -3 build21.py` builds the mock), `render21.py` (`py -3 render21.py` renders the PNGs with headless Chrome) |
| `1.21/upnext-1.21.png` | P1: Tonight with Up next in place, the way back, and Up next for six characters (route with a masked zone, goal, main scenario, pin, closest stop, level gate) with the reason hover |
| `1.21/current-step-1.21.png` | P2: the detail pane's Where to go for an In journal quest, the giver choice, a step with many places, a duty step, a step with no place, the right-click menu, and Quiet on Ishgard Snow |
| `1.21/roster-1.21.png` | P3 and N11: the roster at Full Night and Quiet Snow, your other characters with its hover, Set a goal, and the goal card read-only and live |
| `1.21/blues-1.21.png` | P4 and N9: the My blues left column, Do first with a set-aside row, the group confirm, the tier hover, Your story with masked patches, the switch, tier words in Nearby, thin pace data |
| `1.21/stories-1.21.png` | P5, N8 and N10: Side stories with Caught up, the new-chapter line, the chain line, Loose ends, the finale setting, the Cast line (met and masked) and the quick view |
| `1.21/boards-1.21.png` | P6 and P7: the Triple Triad card (with masked opponents), Nearby Everywhere (with a masked expansion), the Unlocks line, the Route header, and Quiet Snow |
| `1.21/chat-1.21.png` | P8: every chat line and the Say what's next setting |
| `1.21/looks-1.21.png` | Up next and Loose ends at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer |
| `1.21/icons/` | New game icons: Triple Triad card (060156), side quest (061411), and the job icons for WAR, BRD, WHM, SCH, DRK, AST, SGE and PCT (062121–062142) |

**The mock's data is real.** These were read from the 2026.09.15 client with Lumina (a throwaway extractor, not shipped):
- quest names, levels, givers and previous quests;
- the steps of The Long Road to Xak Tural, Gifts for the Outcasts, Into the Aery and Morbid Motivation, with each objective's own text (`TEXT_…_TODO_nn`) and its map coordinates from `TodoParams.ToDoLocation`. For example, step 3 of The Long Road to Xak Tural is "Speak with Erenville." at Shaaloani X 27.0, Y 34.8, and the giver is Erenville in Tuliyollal;
- the Triple Triad sheet: 142 opponent rows with an NPC, **106 of them gated by a quest** (`PreviousQuest`), with each opponent's place. For example, Elaisse in The Pillars plays after Caught in the Act, and Celia in Old Sharlayan after Endwalker;
- side-story genres (Tataru's Grand Endeavor is 7 quests in the game's own genre; Inconceivably Further Hildibrand Adventures is 9 so far) and job finales. A Harmony from the Heavens (BRD, Lv 80) and One Final Journey carry the plain yellow side-quest marker, which is the N8 evidence;
- casts, from each quest's script (`ACTOR…` and `LOC_ENPC_…` constants) and its listeners. Named characters in at least 8 main scenario quests give **114** recurring characters, and **147** side quests feature one of the top 30.

Main scenario counts come from the catalog fixture: Dawntrail 100, The New Dawn 25 and Winter's Prelude 18, so a character at Dawntrail's first quest has **143 to the latest story** (the synthesis's number). The characters, worlds, dates, Ready counts, allowances, Moonlit counts, zone counts and the pace are an example account: the owner's main and eight alts, as in plan v6's multibox notes.

---

## Colour language

There are no new tokens. 1.21 uses only what 1.16 to 1.19 defined and gated.

| Meaning | Token | Where in 1.21 |
|---|---|---|
| Act now | gold (`--moon`; `--goldtx` text on Snow) | the Up next primary pill, Ready in heroes, the selected roster row's 2 px edge, the moon toggles |
| Finished, or nothing left yet | silver (Secondary, semibold) | **Caught up**, **Goal reached** |
| Text, Secondary | `--silver`, `--mist` | everything else |
| Tertiary | `--dusk` | **only** off-state chips and a disabled segment, never body text on a Full card or a lifted block (supervisor round 1: the Full card's lightest fill takes Tertiary under 4.5:1, so 1.21's sub-labels, notes and set-aside rows use Secondary) |
| Masked by the spoiler shield | Secondary words, never the name | "a zone ahead", "Main scenario quest (Lv 90)", "An opponent ahead", "a familiar face" |
| Needs you | `--attn` copper | **not used.** Nothing in 1.21 needs the player urgently. |

**No colour carries meaning alone.** Finale, Live, Caught up and the tier words are words or chips in Text and Secondary. The Finale chip is the neutral 1.14 chip, not gold, because gold means "act now" and a finale is a fact about the quest.

---

## P1. Up next (`upnext-1.21.png`)

### Where

Up next is the first block inside the Tonight card (the detail column when nothing is selected). It sits above the card's existing lines (Ready count, main scenario, events, Pinned and Ready, Next stops), which keep their places.

### The pick

Up next shows one quest: the first rule below that has one for the viewed character.

| # | Rule | Reason line |
|---|---|---|
| 1 | The next stop of the route you follow | "Your route to Flying in Thavnair · 8 stops left" |
| 2 | This character's goal (N11) | "Your goal: match Michiru's unlocks · 13 left" |
| 3 | The next main scenario quest, when it is Ready or in your journal | "Next in the main scenario" (+ "· in your journal") |
| 4 | Your first pinned quest that is Ready | "Your first pinned quest that is Ready" |
| 5 | The closest Ready quest in Next stops | "The closest Ready quest, from Next stops" |
| 6 | The level that opens the next story quest | "The next story quest needs Lv 70 · you're DRK 69" |

If none applies (nothing Ready on this job), the block keeps its size and says "Nothing is Ready on DRK. 4 quests are Ready on another job ›".

The reason line's hover lists the six rules in order, in words.

### Anatomy

| Part | Full | Quiet | Plain |
|---|---|---|---|
| Block | 150 px tall, radius 8. Built from the card's own fill: moonlight `#F4F2EA` at .085→.05 over it, so it is always one step lighter than whatever card it sits in, with a 1 px keyline (the same moonlight at .10), a top-edge highlight (`#FFF0BE` .12) and a 0 2 5 shadow at .28, straight down. Snow: white→`#F4F6FA`, keyline `#C3CBDA`, navy 0 4 10 at .10. It is a tonal lift, not a framed card, so Tonight keeps one frame. | 146 px, moonlight .055 over the card, keyline .10 | 118 px, no fill, a Line under it |
| Eyebrow | "UP NEXT" in the Eyebrow role (Trump Gothic small caps, `OrnamentLight`) | "Up next", semibold 11.5, Secondary | the same |
| Moon | the 48 px hero medal of the quest's state (its badge seat included) | the Quiet hero medal, 44 px | the 20 px flat glyph |
| Name | Title face (Jupiter 16), one line, cut to the room with the whole name on hover; a level chip after it | semibold 13.5 | the same |
| Reason | 12 px Text, one line | | |
| Place | a 20 px giver plate at every level, Plain included (1.15: face, silhouette, emblem or initials; never the moon disc, which N10 keeps for "a familiar face"), then "Talk to Krile · Main Hall, Old Sharlayan" or, for an In journal quest, "Step 3: Speak with Erenville. · Shaaloani" (P2) | | 18 px plate |
| Actions | the primary travel pill (`TravelControls.Primary`, so it is exactly what the detail pane's primary would be at this automation level), then **Details** (a quiet button, right-aligned) that selects the quest. The level gate's pill is **Show the 3 quests** (the Journal, filtered to the Ready quests that close the gap, from C8's EXP). | | |

**Tonight's main scenario line** stays in its place. When Up next already shows the next main scenario quest, the line shows the catch-up instead of repeating the name: "Main scenario · 91 quests to the latest story, Lv 95–100".

### The way back to Tonight

- A **Tonight** button (the moon icon and the word) on the detail header, at the right end of the row that holds ‹ › (1.14's back and forward).
- **Esc**, when nothing else is open (after menus, popovers and the drawer, in the existing Esc order).
- **A second click on the selected row.**

The detail pane and Tonight cross-fade over `Select` (0.15 s) in and `Leave` (0.12 s) out. Back (‹) after Tonight returns to the last quest.

### Motion

When the pick changes (the quest is finished, a pin changes, the route moves on), the block's text, moon and plate cross-fade in place over `Select` (0.15 s). The block never changes size or position. At Plain and under Reduce motion the change is instant. The completion meteor (1.14) is untouched.

### Spoilers

Up next uses the shield like every other surface. It only ever picks a quest the character can act on now (Ready, In journal, or the level gate's current story quest), and such a quest is never past the story point, so its name and giver always show. The mask appears where Up next names a target further on: a route or goal whose target lies past the story point reads "Your route to Flying in **a zone ahead** · 31 stops left" (`upnext-1.21.png`, Kiri). If a future rule ever picked a masked quest, it would read as the shield's words with the moon-disc plate, never the giver's face.

---

## P2. Go to the current step (`current-step-1.21.png`)

### In the detail pane

For a quest **In journal**, the action area becomes a **Where to go** section (the Section heading of 1.14) above the travel pills:

1. **Aim at**: a two-cell segmented control, **Current step** · **Giver** (`Chrome.Segmented`, 26 px). Default: Current step.
2. **The step**: "Step 3 · **Speak with Erenville.**" Coordinate pairs and "· 2 more" never break inside; the lines wrap only at the "·" separators. The objective is the game's own text for the step you are on (`TEXT_<ID>_TODO_nn`, already read by `QuestTextReader`). **Later steps are never named**, and a step's text is never shown before you reach it.
3. **The place**: "Shaaloani · X 27.0, Y 34.8 · you're in Tuliyollal".
4. **The pills**: Flag, Teleport (Lifestream) and Walk (vnavmesh), by automation level (1.18: hidden above the level, never greyed).
5. **A note** in Tertiary: "Travel follows this step until it's done, then the next one. The giver, Erenville in Tuliyollal, is one click away."

Choosing **Giver** aims the same pills at the giver. The choice is kept for that quest until it leaves the journal.

### Cases

| Case | Example | What it shows |
|---|---|---|
| One place | The Long Road to Xak Tural, step 3 | the place and coordinates |
| Several places, or an area | Gifts for the Outcasts, step 2: "Step 2 · 2 more · Obtain hunks of nanka flesh from clearwater nankas." (the count comes before the objective, so a wrapped line never starts with a separator) | "Where the clearwater nankas are · The Dravanian Forelands · X 28.8, Y 22.3", the area the game gives; several separate places aim at the **nearest unfinished** one ("Nearest of 3 places") |
| A duty | Into the Aery, step 2: "Enter the Aery." | "The entrance · The Churning Mists · X 33.7, Y 15.5" (`EntranceIndex`), the 1.19 badges, **Walk to the entrance** and **Duty Finder** |
| No place in the data | Morbid Motivation, step 1 (a roulette) | "This step has no place in the game's data, so travel aims at the giver: Brangwine · Mor Dhona". **Current step** is disabled, and the reason stays visible in the section, never only in a tooltip. |

Quests that are not In journal keep today's giver travel and show no Aim at control.

### Elsewhere

- **The Todo overlay's right-click** on an In journal quest, and Nearby's "…" menu: **Flag the current step**, **Teleport near the current step**, **Walk to the current step**, a separator, then **Flag the giver**, Show in the Journal and Link in chat.
- **`/tsuki go [quest]`** travels to the current step of the named or selected In journal quest (else its giver), at the player's automation level, and prints one line (P8). It is macro-able, for players who need one-press travel (accessibility research). **`/tsuki stop`** stops it.

### Safety

Travel is the existing Lifestream and vnavmesh hand-off with its existing confirmations and Stop. Nothing new runs on its own.

---

## P3. All characters (`roster-1.21.png`)

### The view

Characters gets a third view in its switch: **Dashboard · Collection by character · All characters** (the 1.8 switch, now three cells). The caption on the right reads "9 characters · 2 live in other clients".

**The table** has a fixed header (the 1.14 column header style: Trump Gothic at Full, sorted column in gilt with ▾) and **two-line rows, 44 px**:

| Column | Line 1 | Line 2 (Secondary or Tertiary) |
|---|---|---|
| Character | name (Text); a starred character has a 10 px star in Text before it | world · role (the player's own word: main, healer, crafter) |
| Job | the job icon (20 px tile) and "BRD 100" | — |
| Story | expansion · "next: The Long Road to Xak Tural" | "91 to the latest", "caught up" or "Lv 70 needed" |
| Goal (N11) | the goal, or **Set a goal…** (a dotted-underline link in Secondary) | what is left ("13 left · 9 Ready"), or **Goal reached** (silver, semibold) with "Set a new goal" |
| Ready | the count of Ready quests | — |
| Today | "12 allowances" (1.19's projection for stored alts) | "6 deliveries left" |
| Moonlit | rewards left | — |
| Last seen | "now", "2 days ago", or **Live** (a 6 px Text dot and the word) | "this client", "other client", "other folder", or "saved before 7.5" when the snapshot predates the patch |

- **Right-aligned numbers** (Ready, Moonlit) keep a 22 px gutter from the left-aligned text after them.
- **Sorting** is by any header. The default is Story, with starred characters first and the player's custom order breaking ties.
- **Narrow windows:** below 1,000 px the Moonlit and Today columns hide first; the header's menu turns them back on.
- **Clicking a row** opens that character's dashboard. A hovered row takes the Hover tone; the selected row a gold 2 px left edge and a faint Moon wash (1.14's selection language).
- **Nicknames, roles, the star and the order** are set from the row's right-click and stored in the shared character settings file (where hidden and not-tracked live today).

### Other clients are read-only

Rows marked **Live** are characters logged in on another game client on this computer (multibox heartbeats, `LiveClients`). **Other folder** rows come from another XIVLauncher roaming folder (`FolderScan`), which closes the multibox gap community.md found. Both:
- update from that client's snapshot every few seconds (Live) or when its file changes;
- are never written by this client;
- offer no hand-off (Send to Questionable, travel): the goal card says so in words (below).

### Your other characters, under the detail hero

One fixed-height line (22 px) under the hero's status:

`Your other characters  ◑ Kiri  ◑ Sora  Done on 4  2 can't take it yet`

- 14 px state moons with first names for the characters that can act on it (Ready or In journal), then counts in words for the rest.
- The hover is the account table: every character's state for this quest, evaluated offline from its snapshot (the existing Account view's data).
- The line is present for every quest when the player has more than one character, so the hero never changes height between quests.

### Spoilers

Each row's names follow **that character's** shield setting (1.16 "For this character"). A row's next main scenario quest is that character's current one, so it is always named.

---

## N11. Alt goals (`roster-1.21.png`)

### Set a goal

**Set a goal…** in the Goal column opens a popover (`PopupFade`, Rise 0.16 s) titled "Goal for Kiri · Catch this character up to…". It has four radio rows, each with its picker:

| Goal | Picker | What is left |
|---|---|---|
| The story, up to a patch | a patch ("7.0 Dawntrail") | main scenario quests to that patch's last quest |
| Another character's unlocks | a character ("Michiru") | unlock quests that character has done and this one hasn't (the existing Compare diff) |
| Flying in an expansion | an expansion | zones left to fly (Flight) |
| Every duty roulette open | — | the duties left to unlock (1.19's Duties board) |

- **The preview line** counts what the goal adds before it is set: "13 unlock quests Michiru has done and Kiri hasn't: 9 can be done now, 4 wait for Kiri's story."
- **Set goal** (primary) and **Cancel**. An existing goal also shows **Clear goal**, which has the 8 s Undo toast.
- Goals live in the shared character settings file, so any client can set a goal for any character. Progress is read from that character's snapshot.

### The goal card

The goal appears on that character's dashboard as a card titled with the goal ("Goal: match Michiru's unlocks", caption "13 left"):
- the first rows, Ready first ("Maniac Manor · Ready · Mor Dhona"; the card names no duties, so a row always fits), then "9 more ›";
- rows past the character's story point are masked: "Sidequest (Lv 95) · a zone ahead · waits for Kiri's story".

**The actions** depend on where the character is logged in:

| Where | Actions |
|---|---|
| In this client, at Full hand-offs | **Send 9 to Questionable** (the Ready ones, A6's priority list), **Route**, **Clear goal** |
| In this client, below Full hand-offs | **Route**, **Clear goal** |
| Live in another client, or stored | the words "Kiri is logged in on another client: her list is read-only here. Send it to Questionable from that client." and **Route** (a plan to read, not a run) |

**Goal reached** reads in silver on the roster, with **Set a new goal**. Up next uses the goal as its rule 2 (P1).

---

## P4. My blues: Do first and Set aside (`blues-1.21.png`)

### Tiers

Every unlock quest gets one tier, derived from data the plan already has:

| Tier | Word | From |
|---|---|---|
| 1 | **Story needs it** | `story_required.json` and the main scenario's duty and previous-quest gates (the Crystal Tower, the hard primals before Good Intentions) |
| 2 | **Opens content** | unlocks a dungeon, trial, normal raid, area or flying |
| 3 | **Systems** | unlocks a game feature (`UnlockKind.System`: glamour, retainers, the Gold Saucer) |
| 4 | **High-end** | Extreme, Savage, Unreal, Ultimate, Chaotic |
| 5 | **Another job or society** | a job, class or allied society the character doesn't play now |

The tier word's hover explains all five.

### Do first

My blues' left column gains **Sort: Story order · Do first** (`Chrome.Segmented`), above the existing kind chips and filters. With Do first, the main column shows **one card per tier, in tier order**, each with:
- its Section heading and "N left";
- a one-line why ("The main scenario asks for these later. Do them first.");
- the first rows and "N more ›";
- tiers 4 and 5 fold to one line each ("High-end · 3 left") until opened.

**Rows become two-line, 44 px:**
- line 1: the moon, the name and the kind chips;
- line 2: "Ready · Mor Dhona · the Crystal Tower; Shadowbringers needs it";
- a reserved 104 px action slot: **Teleport** and "…" on hover or keyboard focus.

In Story order, the tier word is one more chip on line 1. **Nearby and the Todo overlay** add the tier word after the level, in Secondary ("Lv 50 · Story needs it").

### Set aside

- **One quest:** the row's "…" offers **Set aside for later** and **Not for me**. The quest leaves the counts, Nearby, the DTR entry, the overlay and notices, with the 8 s Undo toast. **Its row stays in place** as one quiet line, "Out of Sight, Out of Mine · Set aside for later · it leaves your counts", with **Undo** in its action slot, until the list is rebuilt (tab change, filter change). Nothing moves under the pointer.
- **A whole group or chain:** the group's "…" → **Set aside these 5…** opens the 1.18 Confirm popover: "Set aside 5 quests? The Mor Dhona Alexander and Minstrel unlocks leave your counts, Nearby, the overlay and notices. The Set aside filter brings them back." The buttons are **Set aside 5** (a plain pill, not gold) and **Cancel**.
- **The summary** reads "42 left · 18 Ready · 6 set aside ›". The link turns on the **Set aside** filter chip, which lists them with **Bring back** on each row.
- Set-aside lists are per character, merged across clients like pins (the synthesis).

---

## N9. Your story on one page (`blues-1.21.png`)

### Where

At the top of My blues, a switch like Characters': **Clear my blues · Your story**. It is a view, not a new tab.

### The page

1. **Title:** "Your story" (Title face).
2. **The pace line:** "To the latest story: **832 quests**, Lv 50–100. About 52 evenings at your recent pace (estimate)."
   - An **evening** is a local day (05:00 to 05:00) on which the character finished at least one main scenario quest.
   - The **recent pace** is the median number of main scenario quests per evening over the last 20 such evenings.
   - The words "(estimate)" are always there.
   - With **fewer than 15 dated story quests**, the line says so instead: "Your pace shows after 15 dated story quests. You have 9."
3. **Patch bands**, in story order. Each band is a tonal block with its header ("2.1 – 2.5 · Seventh Astral Era") and what is left ("77 left · next: Laying the Foundation").
   - Inside, each **optional line** sits under the quest that opens it ("Opens after **The Ultimate Weapon**"): the moon of its state, its name, "Story needs it" where it applies, and "9 left · next: Legacy of Allag", "in your journal", or "done".
   - Bands you finished fold to one line ("A Realm Reborn · 2.0 · done").
4. **Past the story point**, a band shows counts only: "Heavensward · 3.0 · 94 main scenario quests · 7 optional lines", then "3.1 – 3.5 · 25 main scenario quests · 6 optional lines", and "Names hidden until you get there." Expansion names are not spoilers; patch titles, quest names and line names past the point are hidden. Inside the current band, a line that opens after a main scenario quest the character hasn't reached reads "Opens after Main scenario quest (Lv 50)" and "1 optional line · name hidden".
5. **Copy as checklist** (the existing My blues button) copies the page as plain text, names hidden the same way.

Placement uses the unlock-route engine (`UnlockRoute`), so each line is placed at the main scenario quest that gates its first quest.

---

## P5. Named side stories and Caught up (`stories-1.21.png`)

### The card

The dashboard's story chains section becomes **Side stories**, with lines named as players name them:
- Tataru's Grand Endeavor, Tales from the Shadows, the Void quests, Chronicles of Light, Delivery Moogle, Scholasticate;
- Ishgardian Restoration, Cosmic Exploration and the Occult Crescent main lines;
- Hildibrand.

`chains.json` gains a `questIds` / `startQuest` form and an `ongoing` flag. Curated names override "Story: <first quest>".

**Rows are two-line, 44 px:**
- line 1: the side-quest icon (061411) and the line's name;
- line 2: "3 left · next: **Forever in Our Hearts** · Ready".

| State | Line 2 |
|---|---|
| Started or not | "N left · next: <quest> · <state>" |
| Ongoing, every released quest done | **Caught up** (silver, semibold) · "continues in a later patch" |
| Finished | folds into "7 lines done ›" |

The caption counts what is left: "3 lines to go".

### The chain line in the detail pane

It takes the curated name: "Tataru's Grand Endeavor · next after this: **Treasured Bonds**". Totals stay in its hover (plan v6's decision).

### The new-chapter line

On the first login of a patch, the **What's new** card (which already opens then) gains one line: "New chapters · **Inconceivably Further Hildibrand Adventures** · 2 quests · Show ›". It shows once per patch and per character, and there is no new notice (the critic's static-layout check).

The Hildibrand expansion note in `chains.json` is corrected to Dawntrail (data only).

### Spoilers

A side story that opens past the story point reads "A side story ahead · opens in a later expansion · name hidden". The new-chapter line and the chain line say "a side story ahead" and "Sidequest (Lv 90)" in the same case (`stories-1.21.png`, Aki).

---

## N8. Loose ends (`stories-1.21.png`)

A **Loose ends** card on the Characters dashboard, after Side stories. Its caption is "5 started, not finished".

**What counts:** job and role quest lines, curated chains, story side quests and the Side Story Quests genres that the character **started and never finished**. Started means at least two quests done, or one for lines of up to four quests, so a lone intro quest doesn't count.

**Rows are two-line, 44 px:**
- line 1: the line's icon (the job icon for job and role lines, the side-quest icon otherwise), its name, and "1 left" at the right;
- line 2: a **Finale** chip when the next quest ends the line, then "1 left", the level, then its moon and "**A Harmony from the Heavens**". The count and level come first so the reserved hover slot can only ever meet the end of the quest name, and line 1 holds only the line's name, so long names such as Physical Ranged DPS Role Quests (Endwalker) fit (supervisor round 2).

A row whose next quest lies past the story point names neither the quest nor its zone: "Dark Knight Quests · Finale · 1 left · Job quest ahead (Lv 80)" (Aki).

The reserved slot holds **Teleport** and "…" (Go to giver, Flag, Send to Questionable at Full hand-offs, **Not for me**). Not for me uses P4's set-aside list, with Undo.

**Order:**
1. Finales that are Ready, especially those the game marks only in yellow: the Shadowbringers job finales, the Endwalker role finale, One Final Journey, the Void quests. In the data, A Harmony from the Heavens and One Final Journey carry the plain side-quest marker.
2. Then the lines with the fewest left.

**Options**, both off by default:
- **Settings › Alerts › When a storyline's finale is Ready.** One chat line and one Tonight line, once per finale.
- A Loose ends section in the Todo overlay (Settings › Overlay).

---

## N10. Who's in it (`stories-1.21.png`)

### The cast

From each quest's script (the `ACTOR` and `LOC_ENPC_` constants) and its listeners, the data generator records the **recurring story characters** in it. A character is recurring when it is named in at least 8 main scenario quests (114 characters on 2026.09.15). Generic names ("serpent officer") are dropped. A curated list joins aliases (for example Gaius and Gaius van Baelsar) and blocks wrong matches.

### Where it shows

- **The Cast line** under the detail hero (22 px, present only for quests with a cast): 20 px plates (1.15: a face where the portrait index has one, otherwise initials), then "With **Tataru**".
- **The row hover** gains "With Thancred and Urianger".
- **The quick view With story characters** in the filter drawer's Quick views: "Side quests where someone from the story appears". 147 side quests qualify for the top 30 alone.

### Spoilers: only who you've met

A character is named only once the viewed character has finished a main scenario quest that features them. Everyone else is "a familiar face" ("and familiar faces" for more than one), with the moon disc and never their face.

Example: Aki, at the end of Stormblood, sees In the Middle of Nowhere "With **Thancred**, **Urianger** and a familiar face". Era names follow 1.15's rule: G'raha Tia is never shown as the Crystal Exarch before the story says so.

---

## P6. Triple Triad opponents (`boards-1.21.png`)

### The card

A **Triple Triad** card on the Characters dashboard, after Duties (1.19). Its caption counts what is left: "11 opponents to unlock". The 1.14 filter chips are one fixed row with counts: Plays you 2 · Locked 11 · Cards left 9.

**Groups:**

| Group | Sub-label | Row line 2 |
|---|---|---|
| Plays you | cards you don't have | "3 cards you don't have", or "beaten · 2 cards left" (`UIState.IsTripleTriadNpcBeaten`) |
| Locked behind a quest | the game never says which | "after ◑ **Caught in the Act** · Ready", or "after ◑ **Criminal Phrenology** · Blocked: All the Little Angels first" |

- **Rows are two-line, 44 px:** the card icon (060156), the opponent's name and place on line 1, and the state on line 2.
- **The reserved slot** holds Teleport and "…" (Flag, Show the quest, Send to Questionable at Full hand-offs).
- Opponents with every card owned and beaten leave the list. Their count is only in the caption's hover.

**The footer:**
- **Route to 3 opponents** opens the Route window ("Route to 3 Triple Triad opponents · 5 stops") over the unlock quests and the opponents, through a new `RouteTargetKind.TriadNpc`, which honours Any/All joins (Swift plays after any one of the three Grand Company quests).
- **Pin the quests.**

### Elsewhere

- **The detail pane's Unlocks section** gains a line for quests that open an opponent: "Triple Triad opponent · **Elaisse**, The Pillars", or "Triple Triad opponent · An opponent ahead, a zone ahead" when masked.
- **Moonlit** keeps opponents out of its unique-reward totals (the cards stay in Moonlit as today).

### Spoilers

An opponent past the story point is masked in name, place and quest: "An opponent ahead · a zone ahead · after Main scenario quest (Lv 90)". Beyond the first masked row, the rest fold to "6 more past Kiri's story · names hidden".

---

## P7. The zones board (`boards-1.21.png`)

Nearby (`DiscoveryWindow`) gets a switch in its header: **Here · Everywhere**.

### Everywhere

- **One fixed row** under the header: the kind chips Ready, Blues, Side stories and Rewards (1.14 chips; off is dashed), and **Sort** (Level fit, Ready first, Story order) as a small dropdown. The row is always present in both views, so switching never moves the list.
- **Expansion groups** use the Eyebrow role ("ENDWALKER · Lv 80–90 · fits SGE 90"). The group that fits the job's level opens first; the others fold to one line ("Shadowbringers · 12 Ready across 6 zones ›").
- **Zone rows are two-line, 44 px:**
  - line 1: the zone, its level span ("Lv 80–90", Tertiary), and "new since 7.4" (a chip) when the zone has quests added since the last patch;
  - line 2: what is left, in words: "4 Ready · 9 blues · 3 rewards". Kinds that are off are left out.
- **The reserved slot** holds **Teleport** and "…". The "…" menu has Go to the nearest Ready giver, Show in Journal (the Journal filtered to the zone) and, at Full hand-offs, Send zone to Questionable.
- **Zones with nothing left** fold to "2 zones with nothing left ›" per expansion. Map clearers can open that list to check.

### Spoilers

An expansion past the story point is one line: "Dawntrail · 6 zones past Kiri's story · names hidden". Zones in a revealed expansion that lie past the story point read "A zone ahead" with their level span and "waits for your story" (`boards-1.21.png`, Sora early in Endwalker).

---

## P8. `/tsuki msq`, `/tsuki next` and `/tsuki go` (`chat-1.21.png`)

### The lines

| Command | Line |
|---|---|
| `/tsuki msq` | "Main scenario: Dawntrail, at The Long Road to Xak Tural. 48 quests left in this expansion, and 91 to the latest story, levels 95 to 100." |
| `/tsuki msq`, caught up | "Main scenario: caught up. The story continues in a later patch." |
| `/tsuki next`, In journal | "Next: The Long Road to Xak Tural, step 3. Speak with Erenville. Shaaloani, X 27, Y 34.8. You are in Tuliyollal." |
| `/tsuki next`, Ready, same zone | "Next: Caught in the Act. Talk to Elaisse in The Pillars, X 7.8, Y 10.8, about 60 yalms south-west of you." |
| `/tsuki next`, nothing Ready | "Next: nothing is Ready on this job. 4 quests are Ready on another job." |
| `/tsuki next`, masked | "Next: a main scenario quest at level 83, in a zone ahead of your story." |
| `/tsuki go` | "Going to step 3 of The Long Road to Xak Tural: Erenville, Shaaloani, X 27, Y 34.8. Teleporting first." |
| Say what's next (opt-in) | "Step done. Next: step 4. Speak with Erenville again. X 26.7, Y 31, about 70 yalms north of you." |

`/tsuki next` uses Up next's pick (P1) and P2's current step, so the chat and Tonight always agree.

### The format, for text-to-speech

- **The prefix:** the plugin's existing gold chat prefix "[Tsukimichi]", then sentences with full stops.
- **No glyphs or symbols** in the text: no ☾, ·, ›, arrows or brackets. Lists use commas and "and".
- **Numbers** are digits. Coordinates read "X 27, Y 34.8": one decimal, with a trailing .0 dropped.
- **Distance** is given only in the same zone: rounded to tens of yalms, with one of eight compass words.
- **Objectives** are the game's own text. Givers are "Talk to <name> in <place>".
- **The quest name** is a chat link whose text is the plain name, so a reader speaks the name and a click selects the quest.
- **No tallies** and no percentages (the critic's check).
- **Lines go to the plugin's echo channel only**, never to party, say or Free Company.

### Say what's next in chat

Settings › In game › Chat › **Say what's next in chat**, off by default, with a sample line. After each step or quest is done, Tsukimichi prints the `/tsuki next` line, at most one line every 10 s.

---

## Every look (`looks-1.21.png`)

The matrix shows Tonight with Up next and the Loose ends card at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer. Everything is built from tokens each level already defines:
- **Full:** the framed card, Trump Gothic headings, gilt header ink; Up next is a tonal lift inside the card, with the hero medal;
- **Quiet:** tonal cards, hairlines, the Quiet hero medal;
- **Plain:** ledger bands, the flat glyph, no fill behind Up next.

On **Ishgard Snow**:
- Up next lifts to white with the `#C3CBDA` keyline and a navy 0 4 10 shadow at .10 (1.16's light shadows);
- initials on plates take Text ink (`#2A3350`), so they read on the light plate;
- the primary pill is Snow's gold pill.

**Rows and chips** take each palette's Line, Text, Secondary and Tertiary. The selected roster row uses the Moon wash and edge (Snow: `#A07B25` edge on `#F3EEDD`).

**The contrast pairs** used here were gated in `1.16/contrast.md`, `1.17/contrast17.md` and 1.18 and 1.19:
- Text, Secondary and Tertiary on the card and on Raised;
- the gold pill's ink;
- the Line keyline as a 3:1 part.

There are no new pairs.

**High contrast** caps at Quiet as always. Up next's keyline and the reserved-slot buttons get a 1.5 px outline, and the Live dot becomes the word alone.

**Text size and UI scale.**
- Up next's height scales with the body size: 150 px at 100 %, from the Title, body and pill heights. The name and reason stay one line each, with the full text on hover.
- Two-line rows scale their 44 px the same way.
- The roster's columns hide in the stated order rather than squeezing.
- Rail-style label fitting (R3.1) applies to the new segmented controls: a label never leaves its cell, and it shortens to its first word if needed ("Current", "Giver").

---

## Code map

| Piece | Where |
|---|---|
| Up next block, its pick, the main scenario line swap | `TonightCard.cs` and a new `TonightCard.UpNext.cs`; the pick as a pure `UpNextPicker` in `Tsukimichi.Core/Todo` beside `TodoList` (route from `ActiveRoute`, goals from N11, pins, `NextStopsSource`, the level gate from `PlanningSource`) |
| The Tonight button, Esc, second click | `DetailPane.Hero.cs` (beside ‹ ›, `MainWindow.History.cs`); `Keyboard.cs` for Esc; `TablePane.cs` for the second click |
| Where to go, Aim at, cases | a new `DetailPane.Step.cs`; step targets from `Quest.TodoParams[].ToDoLocation` (`UnlockLinkReader`, `QuestTextReader` objectives), duty entrances from `EntranceIndex`; travel through `TravelControls` / `GameLinks.Travel.cs` |
| Right-click items, `/tsuki go` | `TodoOverlay.cs`, `DiscoveryWindow.cs`; `Commands/TsukimichiCommand.cs` (a `go` verb) |
| All characters, Your other characters | a new `CharactersPane.Roster.cs` (view switch in `CharactersPane.Alts.cs`); the row model `RosterBoard` in `Tsukimichi.Core/Characters` with tests; live and folder rows from `Tsukimichi.Core/Multibox` (`LiveClients`, `FolderScan`) and `Game/MultiboxService.cs`; the hero line in `DetailPane.Hero.cs` |
| Goals, the popover, the goal card | `Tsukimichi.Core/Characters/AltGoal.cs` (pure, tested), stored with the shared character settings (`Game/CharacterRoster.cs`); a new `CharactersPane.Goal.cs`; Send to Questionable via `QuestionableActions` |
| Tiers, Do first, Set aside | `Tsukimichi.Core/Plan/UnlockPlan.cs` (+ a `UnlockTier` beside `UnlockKind.cs`); `PlanPane.cs`; set-aside list next to the pins store, merged across clients; Confirm via `Chrome.Safety.cs`, Undo via `UndoToast.cs` |
| Your story | a new `PlanPane.Story.cs`; placement from `Tsukimichi.Core/Route/UnlockRoute.cs`; pace from the stored completion dates, as a pure `StoryPace` with tests |
| Side stories, Caught up, new chapters | `Data/curated/chains.json` (`questIds`, `startQuest`, `ongoing`), `Tsukimichi.Core/Chains/ChainCatalog.cs`, `StorySidequests.cs`; the card in `CharactersPane.cs`; the line in `WhatsNewCard.cs` |
| Loose ends | `Tsukimichi.Core/Chains/LooseEnds.cs` (pure: the started rule, finale order); a new `CharactersPane.LooseEnds.cs`; the notice in `ChatNotifier.cs`, its setting in `ConfigWindow.Alerts.cs` |
| Cast | `Tsukimichi.DataGen` (script constants and listeners, curated `story_cast.json` aliases); `DetailPane.Hero.cs`; the quick view in `FilterPanel.Drawer.cs`; plates via `Chrome.Portrait.cs` |
| Triple Triad | `Tsukimichi.GameData` reader for `TripleTriad` (PreviousQuest, join, NPC, place) and `IsTripleTriadNpcBeaten`; a new `CharactersPane.Triad.cs`; `RouteTargetKind.TriadNpc` in `Tsukimichi.Core/Route/RouteTarget.cs`; the Unlocks line in `DetailPane.Unlocks.cs` |
| Zones board | `DiscoveryWindow.cs` (Here · Everywhere, the fixed chip row, sort); a pure `ZoneBoard` in `Tsukimichi.Core/Query` |
| Chat lines | `Commands/TsukimichiCommand.cs` (`msq`, `next`, `go`); text built by a pure `GuidanceText` in Core with tests for the no-glyph rule; Say what's next in `ChatNotifier.cs` and `ConfigWindow.InGame.cs` |
| Masks | 1.20's shield (N6) everywhere above; strings in `Strings.Spoilers.cs` |

---

## Decisions

1. **Up next's order** is route, goal, main scenario (Ready or In journal), first Ready pin, closest Next stop, then the level gate. The synthesis's order is kept, with N11's goal second. There is no "skip": the player steers by following a route, pinning, or setting a goal.
2. **Tonight keeps one frame.** Up next is a tonal lift, not a nested card. The main scenario line swaps to the catch-up instead of repeating Up next's quest.
3. **P2 aims at the current step by default** for every In journal quest, with the giver one click away, and the choice kept per quest. A step with no place says so and aims at the giver. Later steps are never named.
4. **`/tsuki go`** is part of P2 (the synthesis lists it). It travels at the player's automation level, never above it, and `/tsuki stop` stops it.
5. **Other clients' characters are read-only.** They get no writes and no hand-offs, and the goal card says where to act. Goals and nicknames live in Tsukimichi's shared settings file, so any client may set them.
6. **The star is Text-coloured**, not gold, because gold means act now. Live is a Text dot beside the word.
7. **The roster says what is left:** "91 to the latest", "212" Moonlit rewards left, "13 left". It never shows done/total.
8. **Set aside:** one quest has Undo and keeps its row in place as a quiet line until the list rebuilds; a group asks first. The lists are per character, merged across clients like pins.
9. **Your story is a view inside My blues**, not a new tab, as the research proposed. The pace line always says "estimate" and hides itself below 15 dated quests.
10. **Caught up is silver words with no new glyph**, so the shared visual language keeps its marks. The new-chapter line rides in the What's new card, with no new notice.
11. **Finale is a neutral chip.** The finale notice and the overlay section are off by default.
12. **N10 names only characters already met in the story.** Others are "a familiar face", with the moon disc, never their face.
13. **The Triple Triad card lives on the Characters dashboard after Duties**, out of Moonlit's totals. Routes honour Any/All joins.
14. **The zones board is Nearby's Everywhere view**, with one fixed chip-and-sort row. Send zone to Questionable sits in "…" at Full hand-offs only.
15. **Chat lines** are glyph-free sentences in the echo channel only. Say what's next is off by default and limited to one line every 10 s.
16. **No copper in 1.21**, because nothing here needs the player urgently. No new tokens.
17. **Every list row in 1.21 is two-line with a reserved action slot**: blues, side stories, Loose ends, Triple Triad and zones. A hover never covers text.

## Open questions

1. **Log in as this character** from the roster, through Lifestream's character switch, behind a hold confirm and disabled while a hand-off runs. Plan v6 deferred it. Should 1.21 add it, or leave the roster read-and-plan only (my proposal)?
2. **The pace line in Your story:** on by default (my proposal, always labelled an estimate), or opt-in for players who don't want a number of evenings?
3. **The roster's Moonlit column:** keep it (rewards left, as the synthesis asked) or drop it to keep the table calmer at its default width?
4. **Set aside reach:** per character (the synthesis, my proposal), or account-wide for blues that no character wants, such as housing?
5. **Up next for a character with a goal:** goal second (my proposal, after a route you chose today), or above the route?


## Approval record (realism supervisor)

- **Round 1: CHANGES.** The supervisor found 6 Major, 3 Minor and 3 Nits, and no Blockers:
  - the Up next lift read as a sunken well on the dark palettes;
  - a patch title and main scenario name leaked past the story point in Your story;
  - a goal card row was cut;
  - Tertiary text on Full cards was under 4.5:1;
  - the Plain plate fell back to the "familiar face" moon disc;
  - several surfaces had no masked render;
  - wrapping, the Goal reached weight, a near-collision in Loose ends, the roster gutters, the disabled segment's contrast, and a struck-through demo.

  All twelve were fixed in the renders and in this spec.
- **Round 2: CHANGES.** Eleven of the twelve fixes held. One new Major came from fix 9: the reserved hover slot clipped "1 left" at the end of a Loose ends row. Two Nits: a caption still said "Tertiary", and a wrapped line started with "·". The supervisor accepted, with no render, the decision not to show a masked Up next name, because Up next never picks a quest past the story point; P1 says what a masked pick would show. The supervisor said it would approve once the row was fixed, with a spot check of that one row.
- **After round 2:**
  - Loose ends line 2 now reads Finale · 1 left · level · quest name. The Loose ends column is wider, so the hovered row with the longest finale name (A Harmony from the Heavens) fits whole.
  - The masked row reads "Job quest ahead (Lv 80)".
  - Both Nits are fixed.
  - **Not re-reviewed:** the supervisor has not seen the re-rendered row, so the final spot check is still open.

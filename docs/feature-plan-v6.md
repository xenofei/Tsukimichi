# Tsukimichi feature plan v6: quiet, steady, beautiful, and right

Status: **proposed on 2026-10-02, waiting for the owner's approval.** Nothing here is built.

## Sources

- **The owner's nine points of 2026-10-02.** Each one is planned below.
- **20 developer reviews** of the code, under `docs/research/plan-v6/developers.md`.
- **30 community research reports** from Reddit (through the public Arctic Shift archive), the Square Enix forums, GitHub issues and discussions, Bluesky, Japanese blogs and Q&A sites, Console Games Wiki, and YouTube and blog guides. They are under `docs/research/plan-v6/community.md`, with links and dates.
- **A graphic design team** for the moons and the icon. It worked in five steps:
  1. three design researchers;
  2. a five-person focus group on the current art;
  3. three competing redesigns;
  4. the same focus group scoring them;
  5. a lead designer's pick.

  Everything is in `docs/design/moon-v6/`.
- **The synthesis and a completeness critic.** These are `docs/research/plan-v6/plan-synthesis.json` and `critic.md`. The critic's corrections are applied here.

## Standing rules

These carry over:
- player value first;
- localization frozen (English only);
- no Square Enix art downloaded from the web;
- automation only behind explicit buttons, through companion plugins;
- teleport through Lifestream only;
- no contact with other plugin developers.

New in this plan:
- **The window never moves under the player.** Any new band, chip row or overlay line must pass the fixed-height test (ChromeBands).
- **Show what is left, not tallies.** No "N of M done" captions unless a number is the point, and totals live in tooltips.
- **No decorative glyphs that carry no meaning.**

## Your nine points

| # | You said | Plan | Release |
|---|---|---|---|
| 1 | The "#" before Journal icons ruins the look | **U1.** The cause is confirmed by four developers: `TreePane.cs:401` passes `"##"` as a visible label to `TreeNodeEx`. It becomes an id-only label, with transparent text on every row, and a lint test stops it coming back. The same pass stops section names being drawn twice to fake bold, and drops the duplicate header count. | 1.11.0 |
| 2 | Tasteful animations that fit the theme | **U8 motion foundation.** Hover and selection settle, a fade on expand, content veils, and tooltip and popup fades, all from one token table (120–220 ms, opacity only for text you are reading).<br>**Moon moments (M1–M3).** The rail bead travels between tabs. On completion the moon waxes and a glint runs down the road. The Todo overlay calms down. The panels beside game windows rise in instead of blinking.<br>**A likely cause.** Tsukimichi follows Windows' "Show animations" setting, so if that is off you saw almost nothing move. A Motion line in Settings will say so, with a one-click override. | 1.12.0 / 1.13.0 |
| 3 | Filters push the main interface down | **U2–U4.** The chip row and all six banners leave the layout:<br>• the scope goes into the table title with an ×;<br>• filters sit in one fixed-height lane with a "+N" popover;<br>• notices float in a dock;<br>• the filter panel becomes a drawer.<br>**U3.** The selected row keeps its screen position when rows change.<br>**Everywhere else.** The same rule covers the detail pane's hand-off lines (they move to the status bar), Moonlit, the Todo overlay, My blues, Nearby, Characters and Flight.<br>**Floating layers.** One slot manager places the dock, the Undo toast and hints, and never covers the selected row or the status bar. | 1.12.0 |
| 4 | Flight expansion icons too small | **U6.** The icons go from about 16 px to 32 px (never under 28), using the game's sharper high-resolution art. They are centred on their heading, with a wider column and taller rows. The banner icon becomes 44 px. | 1.11.0 |
| 5 | Settings is cramped; words meld together | **U7, a full rebuild.** Every setting uses one row design:<br>• the label;<br>• a one-line plain-language hint underneath;<br>• the control in its own column;<br>• 10 px row padding, hairlines between rows, blocks as cards.<br>**9 balanced pages:** General, Journal, Overlay & routes, Alerts, Spoilers, In game, Automation, Characters & data, Advanced.<br>**Copy rules:** labels at most 40 characters, hints at most 110 characters and two lines, and a glossary (Flair becomes "Decoration", Glyph palette becomes "Moon colours"). A test enforces the limits.<br>**New controls:** moon toggles and segmented pickers replace checkboxes and radio sentences.<br>**Cuts:** the language picker is hidden while localization is frozen. Duplicate scale controls leave the filter panel. About folds into the footer.<br>**Migration:** saved settings and the last-open section carry over. | 1.13.0 |
| 6 | Much better plugin icon | **G4.** Round 1 was not approved: its gold "road" didn't sit under the moon or match its colour, and it lacked detail and FFXIV character. **Round 2 is done** (`docs/design/moon-v6/round2/FINDINGS.md`): all six panel jurors picked Menphina's Medallion's icon, whose reflection sits directly under the lit crescent in the moon's own colour, broken by ripples. It scored 7.5, so a short round 3 polish is proposed to clear the 8.0 bar. Exported crisp at 128 px plus a 512 master, checked against the installer's checkmark corner. You choose from the round-2 concepts. | 1.11.0 |
| 7 | Moons look like cheese | **G1, G2, G3, G5.** Round 1 ("Sumi to Kinpaku") was not approved: it read as generic UI icons. **Round 2 is done:** Menphina's Medallion led (6.9; job-icon medals, a cracked red Dalamud for Locked out), with Aether Crystal best at row size. Nothing reached the 8.0 bar yet; the proposal is D's material with A's row-size legibility, the Done/Completed meanings swapped, and a round 3 polish. It keeps the rules that matter: one distinct shape per state, readable at 16–20 px without colour, and the same shapes in High contrast. For more detail the moons can become pre-rendered textures instead of simple vector shapes. You compare old and new in game on an A/B sheet before anything merges. | 1.12.0 |
| 8 | "831 steps · 830 done" is weird | **U5.** The Path header becomes:<br>• nothing when the quest is the next one or already done;<br>• otherwise "3 quests before this one" and a **Next: <quest> · Ready** link.<br>The chain line shows the chain's name, a "Next in chain" link and a slim bar. Totals appear only on hover. The same audit covers the other "N of M done" strings (job ladders, Flight currents, payoff, recap, requirement detail, "Step X of Y"). | 1.12.0 |
| 9 | No Ctrl/Shift safety on "Mark as unique" | **S1.**<br>**The cause:** Enter in the note box already confirms, which bypassed the existing gate (`VerdictPrompt.cs:92/99`).<br>**The fix:** Mark as unique and Not unique only act while Ctrl or Shift is held. The cue shows on hover, not as a permanent glyph. An armed click saves at once and offers Undo with "Add note". Enter no longer confirms.<br>**S2, the same rules everywhere:**<br>• hold a key for single changes you can undo;<br>• press and hold for bulk or irreversible ones (Replace Questionable's list, Apply settings, Forget, Delete all);<br>• a floating Undo after every data change;<br>• adjustable hold length and a two-click alternative for hand strain. | 1.11.0 |

## Moon and icon designs, for your approval

The full process and every file are in `docs/design/moon-v6/`:
- `README.md`: the research, both focus groups, the scores and the production plan;
- the final eight state SVGs and `plugin-icon.svg`;
- the three competing concepts, under `concepts/`.

A preview page shows them at real sizes: https://claude.ai/artifact/DqR73vER1sspDDQZGmoxEV

**Round 1 was not approved** ("not a fan"; the icon's reflection didn't match the moon; needs more detail and FFXIV inspiration). Round 2 follows the same process: research, concepts rendered and inspected as real images, a focus group, then the lead designer's findings. Its results are in `docs/design/moon-v6/round2/` (briefs, four concepts, six panel verdicts, `FINDINGS.md`) and on the plan website's Moon & icon tab, where you can vote and comment: https://claude.ai/artifact/ARD5rLEo7d8QsZsfEG4WiL

## Releases

Each release gets the usual gates, two reviews, fixers, then the tag. Every release also gets an in-game check list in this document.

### 1.11.0 · Quick wins and safety

Points 1, 4, 6 and 9, plus correctness guards.

| Id | Item | Effort |
|---|---|---|
| U1 | Remove the "#" from Journal rows, with a lint guard. Also: no double-drawn bold, no duplicate header count. | S |
| U6 | Flight expansion icons at 32 px, centred; taller zone rows; 44 px banner icon | S |
| S1 | Mark as unique and Not unique need Ctrl or Shift held; Enter no longer confirms; Undo with "Add note" | S |
| S2 | One safety table across the UI and a floating Undo toast. Hold length is adjustable, with a two-click alternative. | M |
| A1 | `/tsuki stop`: one Stop for every hand-off, for macros and one-press use | S |
| G4 | The new plugin icon, after your approval, exported crisp at 128 and 512 | S |
| G2a | The new glyph colour tokens (Kinpaku gold #E0B860, Torinoko ivory, cool silver), so new 1.11 UI isn't built in the old gold | S |
| S3 | **Fix:** the AutoDuty duty index has been hidden since 1.6, because it was built without a language. Build it off the frame. | S |
| S4 | **Fix:** the Questionable fork renamed itself WigglyQuest and left a do-nothing "Questionable" stub at 99.0.0.0. Recognise both. | S |
| S5 | **Fix:** Moonlit "Copy view as TSV" crashes when the view is grouped | S |
| C4a | **New Game+ guard.** Never save a lost-progress reading made only of quests New Game+ replays, so a replay can't overwrite your saved progress. | S |
| C2 | **Silent-Ready holes:** five mount-collection quests (Firebird, Kamuy, Landerwaffe, apocryphal Bahamut, Wings of Legacy) and mount/house conditions | M |
| A11a | Remove the 5 s companion-setup stall and the per-frame travel recomputation before more motion is added | S |
| S6 | The verification allowlist expires at 1.11.0: settle what data allows and extend the rest to 1.15.0 | S |

### 1.12.0 · A steady window and new moons

Points 3, 7 and 8.

| Id | Item | Effort |
|---|---|---|
| U2 | **Fixed window frame:**<br>• the scope in the table title;<br>• a one-line chip lane with "+N";<br>• notices in a floating dock (one-time prompts close themselves after about 15 s, prompts that need action stay);<br>• the filter drawer;<br>• the Setup, What's new and Welcome back cards in the no-selection slot;<br>• a ChromeBands test that locks band heights. | L |
| U3 | The selected row keeps its screen position when rows change | S |
| U4 | **The same rule elsewhere:**<br>• hand-off status in the status bar;<br>• a one-line Moonlit toolbar;<br>• Questionable status in the Todo overlay header;<br>• My blues, Nearby, Characters and Flight audited. | M |
| U5 | Path and chain captions say what to do next, with tallies in tooltips; the string audit across the plugin | M |
| G1 | The moon renderer rewritten to Sumi to Kinpaku: still vector, drawn with ImGui primitives, about half the code | M |
| G2 | The rest of the glyph tokens; High contrast as a token swap; tests (no mark on the lit side, the ink gap, the contrast ladder) | S |
| G3 | An A/B contact sheet in the glyph debug window (all states at 5 to 32 px, greyscale and colour-blind toggles), plus an icon tab. **You check this in game before G1 merges.** | S |
| G5 | Rail and completion gauges restyled in the same material | S |
| U8a | Motion tokens and the Motion status line with an override. The first visible moment: the moon waxes when you complete a quest you can see. | S |

### 1.13.0 · Settings, motion and Moonlit art

Points 2 and 5.

| Id | Item | Effort |
|---|---|---|
| U7 | The Settings rebuild: row design, 9 pages, plain words, spacing, toggles and segmented pickers, migration, tour targets updated | L |
| U8 | Interaction motion everywhere: tree, table, rail, pills, tooltips, popups, Settings | M |
| M1 | Moon Road moments: station travel, a road glint on completion, a halo pulse when a quest becomes Ready. One-shot only, never looping. | M |
| M2 | Panels beside game windows rise in, never blink, stick to one side, and don't strobe while you arrow through lists | M |
| M3 | A calmer Todo overlay: optional hide in combat, while talking to NPCs or in group pose; opacity down to 0; a completion beat | M |
| G6 | Moonlit rewards each get their own game art, at a readable size, instead of rows of identical stand-ins | M |
| A11 | No first-open freezes: index warming and smarter companion refresh | M |

### 1.14.0 · Runs you can trust

Automation, built on what the community accepts: solo and NPC content only.

| Id | Item | Effort |
|---|---|---|
| A2 | A "Why it stopped" card with one-click fixes and Copy report (Questionable's top complaint is runs that stop without a reason) | M |
| A3 | Keep automation out of other players' runs: stop Questionable before a duty with no Duty Support or Trust (on by default) | M |
| A4 | Stop after this quest, after N quests, or at a set time; a run receipt | S |
| A5 | "Needs you" alerts while a hand-off runs: death, stuck, a duty pop, a tell | M |
| A6 | Questionable: "Just this quest" and "Do this next" | S |
| A7 | Craft with Artisan becomes Stop while running | S |
| A8 | Travel recovery: reload the navmesh and retry; walk to an aetheryte before a hop; land where you can talk | M |
| A9 | A travel preflight in Setup (movement mode, first-person camera, conflicts) | S |
| A10 | An automation level switch (Tracker only, Travel, Travel and walking, Full hand-offs) and an "About automation" card | S |

### 1.15.0 · Right answers

| Id | Item | Effort |
|---|---|---|
| C1 | **The game confirms it.** Record the game's own "available quest" markers and offers. A Not checked quest the game offered reads Ready, and a disagreement is flagged. This also settles the wiki-only allowlist on your own characters. | M |
| C3 | **Gates from the wiki's free text:** deep-dungeon floors, Skysteel and Splendorous tools, Bozja rank, Occult record, Eureka elemental level | M |
| C4 | **A full New Game+ session mode.** Replays never touch saved progress, and Tonight shows the chapter. | M |
| C5 | **Allied societies:** a carried-over daily that sets allowances to 0, stored alts' allowances, and the rank-up day's bonus dailies | S |
| C6 | **Quest rewards you can buy back** from the Recompense Officer and Calamity Salvager. This adds about 110 seasonal and collab glamour pieces and shows "Done, not learned". | M |
| C7 | **How you'll clear it:** duty badges (Solo with NPCs, Group of N, High-end) and the item-level wall ("needs i690, you have 672") | M |
| C8 | **EXP to the right job:** "Hand in on SGE Lv 84", with a warning on capped jobs and an explicit Switch gearset button (your call) | M |
| C9 | **Journal slots:** "Journal 27/30" in the status bar and a Make room helper | S |
| C10 | **Seasonal events:** ending-soon warnings and dated reruns | S |

### 1.16.0 · What next, for every character

| Id | Item | Effort |
|---|---|---|
| P1 | "Up next" at the top of Tonight: one recommendation with its reason and the travel button | M |
| P2 | Go to the current step of a quest already in your journal, not back to its giver | M |
| P3 | A roster board of all characters: MSQ position, allowances, Moonlit %, last seen | M |
| P4 | My blues: "Do first" triage, plus Not for me / Later | M |
| P5 | Named side-story questlines (Tataru's Grand Endeavor, Tales from the Shadows, Restoration and more), and "Caught up" for ongoing series | M |
| P6 | Triple Triad opponents and the quests that unlock them (106 of 134 NPCs are behind a quest the game never names) | M |
| P7 | A zones board: what's left, across every zone | M |
| P8 | `/tsuki msq` and `/tsuki next`: chat lines text-to-speech plugins can read | S |

### Parallel track · API 16 and Patch 8.0 "Evercold" (P9)

Dalamud's API 16 ships with Patch 8.0 in January 2027 and removes Lumina.Excel, which Tsukimichi uses in 28 files.
- Branch work starts now: bundle Lumina.Excel, and add a compatibility layer so the move is about five file edits.
- It merges when Dalamud announces v16, whatever release slot that falls in.
- Recheck after Fan Fest.

## Not doing, for now

| Idea | Why not |
|---|---|
| A full saved "Questing Run" session engine | 1.14 gives most of the value with far less risk |
| Our own quest graph | Quest Map does it, and "Open in Quest Map" stays |
| A self-test runner, flight recorder or UI gallery | Your no-smoke-test-pages rule; only the glyph contact sheet and motion debug switches stay |
| Friend share codes, compare with a friend's export | The first time Tsukimichi would read someone else's data |
| Stream mode | Deferred |
| Lodestone fetches | Local only is a trust feature: plugin privacy scandals dominate 2025–26 discussion |
| Non-Lifestream teleports | Your rule |
| Relic material lists, glamour dresser tidy-up, Treasure Trove | Other plugins already cover them |
| A discard-warning hook | Little gain for a new hook |
| Cutscene and voice flags | Wait for the 8.0 data |
| Expansion "skies" themes, a pocket mode | They compete with the calm direction |
| Broad presets beyond the automation level | Too much settings coupling |
| Growing Tsukimichi's own IPC | 25 gates, no known consumer, and outreach isn't allowed |
| A separate refactor release | Refactors happen inside the items that touch those files |
| An installer screenshot carousel | Waiting on your call about game icons appearing in screenshots |
| Map markers for priority givers | Waiting on your call |

## What the community said

These are the strongest signals, with sources in `community.md`.
- **Automation:** the accepted line is "with trusts, not real people". Bot complaints are almost all about the Duty Finder, and Questionable has been seen queueing into the regular Duty Finder even when Duty Support existed. This is the basis for A3.
- **Runs that stop without a reason** are Questionable's number-one complaint (a 71-comment discussion). This is A2.
- **The Questionable fork is now "WigglyQuest"**, and the old name is a stub. This is S4.
- **Wasted EXP on capped jobs** is the most repeated levelling mistake. Japanese players switch jobs before turn-in as a habit. This is C8.
- **The patch MSQ item-level wall** is the top surprise for returning players. This is C7.
- **Triple Triad NPCs** behind quests the game never names. This is P6.
- **Lost quest rewards:** players don't know whether they can buy them back. This is C6.
- **The 30-quest journal cap** makes players skip givers. This is C9.
- **New Game+** likely makes the safety guard accept a "loss" and overwrite saved progress. This is C4a now and C4 later.
- **Players want to travel to the current step** of a quest in their journal, not back to its giver. This is P2.
- **Accessibility:** one-press commands and chat lines text-to-speech plugins can read. This is A1 and P8.
- **Multiboxers using separate roaming folders** can't share Tsukimichi's view. This is a decision below.

## Decisions for you

| # | Question | My recommendation |
|---|---|---|
| 1 | **Choose the moon direction** from the round-2 concepts | Menphina's Medallion material with Aether Crystal's row-size legibility, after a round 3 polish; then an in-game A/B check before it merges |
| 2 | **Choose the icon** from the round-2 concepts | Menphina's Medallion (moon road to the lantern), after a small round 3 polish; exported at 128 px plus a 512 master |
| 3 | Animations follow Windows' "Show animations". Keep that, or animate regardless? | Keep it, with a Motion line in Settings and an override for Tsukimichi alone |
| 4 | Mark as unique with a key held: save at once with Undo and "Add note", or still open the note popup? | Save at once, with Undo and "Add note". Either Ctrl or Shift arms it. |
| 5 | Where do active filters live? | A fixed one-line lane, with the scope in the table title |
| 6 | One-time prompts: close by themselves? | Yes, after about 15 s (hover pauses it). Prompts that need action stay. |
| 7 | Settings: toggles instead of checkboxes, scale controls only in Settings, hide the language picker? | Yes to all |
| 8 | Questionable heading into a duty with no Duty Support or Trust: stop or warn? | Stop by default, with one line saying why |
| 9 | A "Switch gearset" button (the game's own gearset change, only on click): allowed? | Allowed, disabled in combat and duties |
| 10 | May Tsukimichi read Questionable's own chat lines to explain why a run stopped? It stays local, but it's a new kind of read. | Yes, and listed in the README |
| 11 | Log in an alt through Lifestream from the roster board? | Defer until after 1.16 |
| 12 | Todo overlay in combat and NPC talk | Hiding options exist, off by default |
| 13 | Read characters from other XIVLauncher roaming folders, for multiboxers using separate folders? | Yes, read-only, in 1.16 if you want it |
| 14 | Have you entered New Game+ since installing, and did progress drop? | If yes, C4 moves into 1.11 as an urgent fix |
| 15 | Release order as above: 1.11 quick wins, 1.12 steady window and moons, 1.13 Settings and motion, then automation, correctness and what-next | As written |

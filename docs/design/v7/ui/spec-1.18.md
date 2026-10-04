# 1.18 "Runs you can trust": the automation surfaces

This spec designs the UI for the 1.18.0 rows of `docs/feature-plan-v7.md`:

| Row | Surface |
|---|---|
| **A2** | Why it stopped |
| **A5** | Needs you |
| **A9** | Travel preflight |
| **A10** | Automation level and About automation |
| **A4** | The run receipt and stop conditions |
| **A6** | The "…" menu items |

It follows `docs/research/plan-v7/a6-panel.md` and the A2–A10 entries of `docs/research/plan-v6/plan-synthesis.json`.

**Three principles run through every section:**
- **Static layout.** Every surface has a fixed place, and nothing in the panes moves when a run starts or stops.
- **Tasteful motion.** Motion uses the existing tokens.
- **Plain words and one colour cue.** Words carry the meaning. Colour backs them up, and there are no warning glyphs.
- **Safe fixes.** Anything that changes a setting outside Tsukimichi has Undo or a confirmation.

**Files**

| File | What it is |
|---|---|
| `spec-1.18.md` | This spec |
| `mock.html` | `#stop`, `#stoplooks`, `#needs`, `#preflight`, `#auto`, `#run`. Sources: `mock-src/v718.js` and `v718.css`. |
| `stop-card.png` | Why it stopped: all eight reasons (Full, Night), and Copy report's text |
| `stop-card-looks.png` | The Stuck card at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer |
| `needs-you.png` | Needs you over the game, with the chat lines, in four looks |
| `preflight.png` | The travel preflight in Setup: with problems, all clear, three other looks, and the Undo toast |
| `automation-level.png` | The automation level switch (Full on Night, Quiet on Snow, Plain on Kugane) and About automation |
| `run-controls.png` | Stop conditions, the status bar, the receipt, and the A6 "…" menu (with the disabled case) |

Game icons are from `1.15/icons/`:

| Hand-off | Icon | Source |
|---|---|---|
| Questionable | the quest's marker | 071221 |
| AutoDuty | Duty Finder | 000046 |
| Travel | Sprint | 000104 |
| Artisan | Crafting Log | 000022 |
| Flag | map flag | 060561 |

---

## Colour language, shared by all four surfaces

| Cue | Night, Dawn, Kugane | Ishgard Snow | Meaning |
|---|---|---|---|
| **Gold** (Moon) | `#F2D27A` (Dawn `#F5C47C`, Kugane `#F0CC72`) | `#A07B25` bar, `#755308` text | Finished well; the one primary action |
| **Silver** | the palette's secondary text (`#A9B2CC`, `#C4B4C0`, `#C6B6A2`) | `#59627A` | You did this (you pressed Stop) |
| **Copper**, new | `#D08654` (5.3:1 on Night Raised, 5.4 on Dawn, 5.9 on Kugane) | `#A8582A` (4.5:1 on Snow) | It needs you |

**Why copper:** gold already means "act now on a quest". Red and plum mean Locked out and destructive. Amber `#C9A866` is the Settings-page hint colour (the 1.17 mix warnings). Copper is a new hue that is still in the brass family, so it fits the Moon Road without shouting.

The cue is always a **3 px bar** on the card's left edge (2 px at Plain), or the dot of a status line. It is never an icon or a glyph.

---

## A2. Why it stopped (`stop-card.png`, `stop-card-looks.png`)

### Where

A hand-off Tsukimichi started can stop: Questionable, AutoDuty, vnav travel or Artisan. When one does, a card appears in two places:
- the **notice dock**, the floating layer above the detail pane's action bar (1.12, `FloatingLayers`), so the panes never move;
- the **Todo overlay**, when it is open, as its top row.

**How long it stays:**
- It stays until the player dismisses it (×), or another hand-off starts.
- It shrinks to the status bar's activity note when another quest is selected.
- "You stopped it" and "Finished" fade after 30 s; the attention cards stay until dismissed.

**Width:** the detail column's content width, 360–420 px. **Motion:** it fades in over `Rise` (0.16 s, 4 px), and fades out over `Leave` 0.12 s.

### Anatomy

| Part | Full | Quiet | Plain |
|---|---|---|---|
| Frame | The card (the kit's frame and ornament), padding 11 × 18 | Tonal card | A band header (22 px) over the pane, no card |
| Colour cue | 3 px bar, inset 6 px, top and bottom 12 px | the same | 2 px bar, full height |
| Header | The hand-off's game icon at 18 px; the title in the Title face (Jupiter 16); "2 min ago" in Tertiary; × | The icon at 18 px; the title semibold 13.5 px | The icon at 14 px; the title 12.5 px in the band |
| Reason | One or two sentences, 12.5 px Text, in plain words | the same | 12 px |
| Context | Quest · step · zone (or what was running), 11.5 px Secondary | the same | the same |
| Actions | Up to two fix pills (the first is the primary when it is safe), then **Copy report** right-aligned as a quiet text action | the same, Quiet pills | Plain buttons |

### The eight reasons

| Reason | Cue | Title | Fixes | Notes |
|---|---|---|---|---|
| **Finished** | gold | Questionable finished | **Start the next one** (one quest, A6's default) | Carries the receipt line (A4) |
| **You stopped it** | silver | You stopped Questionable | **Start again** (resumes at the step) | Fades after 30 s |
| **Duty guard** (A3) | copper | Stopped before a duty with other players | **Show the duty** · **Keep going after it** (Questionable restarts when you clear the duty yourself) | Names the duty and your setting |
| **Knocked out** | copper | Stopped: you were knocked out | **Try again**, disabled until you are up, with the reason under it | Tsukimichi stopped its own travel |
| **Stuck** | copper | Travel got stuck | **Reload navmesh and retry** (A8) · **Flag the spot** | No progress for 20 s, with the place |
| **No path** | copper | No path to the giver | **Reload navmesh and retry** · **Teleport closer** (Lifestream only) | |
| **Missing plugin** | copper | AutoDuty isn't loaded | **Open Setup** | Says what is installed but off, or not installed |
| **Error** | copper | Questionable stopped with an error | **Flag the objective**; **Copy report** is the primary | Says whose data it is ("in Questionable's quest data, not your game") |

**What counts as safe:** every fix starts the same kind of hand-off again or opens a Tsukimichi view. None of them answers the game for the player, and none of them chains: a fix never adds a second run. **Try again** after a knock-out is enabled only once the player is alive and not in combat.

### Copy report

Copy report puts plain text on the clipboard, ready for a GitHub issue. It never includes the character's name, world, Free Company or chat.

```
Tsukimichi 1.18.0 · Dalamud API 15 · game 2026.09.15
Hand-off: Questionable 7.4.12 (started by Tsukimichi, "this quest only")
Stopped: error · missing sequence
Quest: Into the Aery (#67187) · step 3 of 6 · sequence 3
Job: PLD 57 · Zone: Coerthas Central Highlands (155) · at 18.2, 24.7
Travel: vnavmesh 0.4.3 · Lifestream 2.5.1 · movement Standard
Last Questionable lines:
  [QST v7.4.12] Step 3: no path data for sequence 3
Stops at this step on this computer: 2
```

The "[QST v…]" lines appear only if the player approved reading Questionable's chat lines (the A2 note in the synthesis). The per-step stop count is local, and also shows as "stopped here 2×" on Route and send lists.

A status-bar note follows: "Report copied".

### Code map

| Piece | Where |
|---|---|
| `RunStopClassifier` | Core, pure, tested: `StopReason { Finished, Player, DutyGuard, KnockedOut, Stuck, NoPath, MissingPlugin, Error }` |
| `RunWatcher` | A 1 Hz framework tick, not tied to window drawing |
| `StopCard` | `Ui`, drawn by the notice dock and the Todo overlay from one `StopCardModel` |
| `Chrome.ActionPill` | The fixes |
| `RunReport` | Builds the text |

---

## A5. Needs you (`needs-you.png`)

### When

Needs you is active **only while a hand-off Tsukimichi started is running.** It fires for four things:

| Alert | Trigger |
|---|---|
| **Knocked out** | The character is knocked out |
| **Stuck** | No step progress for 20 s, or travel reports Stuck |
| **Duty ready** | A duty pop |
| **A tell** | An incoming tell or party invite |

**Tsukimichi never answers tells and never commences duties.**

On a knock-out or stuck, Tsukimichi stops **its own** hand-offs (travel and AutoDuty) by default. Stopping Questionable too is an opt-in setting.

### The panel over the game

| Property | Value |
|---|---|
| Placement | Top centre of the game viewport, 18 % down, clear of the game's own top-centre banners |
| Size | 400 × 64–84 px |
| Queue | One at a time; later alerts wait behind "+1 more" |
| Look | Follows the Decoration level and palette, like the Todo overlay: Full uses the kit frame, Quiet a tonal panel, Plain a flat box with a 1 px strong line |
| Colour cue | A **copper bar** on the left, 3 px |
| Text | **"NEEDS YOU"** as the Section eyebrow (at Quiet and Plain, semibold "Needs you" in copper), then the title in the Title face ("You were knocked out"), then one line of what Tsukimichi did, 12 px Secondary |
| Buttons | **Stop all** (it stops every running hand-off; the `StopAll` order already exists), any one safe fix (Reload navmesh and retry for Stuck), and **Dismiss** |
| Motion | It rises 4 px and fades in over `Rise` 0.16 s, once. **No pulse, no blink, no shake.** It stays until dismissed, or until the cause clears (the character is revived, or the duty pop closes), then fades over `Leave`. Under Reduce motion it appears at once. |
| Other | It never takes keyboard focus away from the game |

**The taskbar** flashes once (FlashWindowEx, tray and taskbar) when the game isn't the foreground window.

### The chat line

Every alert prints one line:

```
[Tsukimichi] Needs you: you were knocked out during Into the Aery (step 3). Questionable is stopped.
[Tsukimichi] Needs you: your duty is ready: The Stone Vigil (Duty Support).
[Tsukimichi] Needs you: a tell arrived.
[Tsukimichi] Needs you: travel is stuck near Camp Dragonhead.
```

**Format:**
- the plugin's existing gold chat prefix;
- "Needs you:" in the chat channel's bold;
- the rest in the channel's normal colour.

**Rules:**
- The line goes to the plugin's echo channel, never to the party, say or free company channels.
- A tell's sender and text are never repeated.
- The run receipt (A4) uses the same prefix without "Needs you".

### Sound

- **Source:** one of the game's own chat sound effects, `<se.1>` to `<se.16>`, played through the game (`UIGlobals.PlayChatSoundEffect`), so it follows the system-sound volume and mute.
- **Behaviour:** played once per alert, never looped. Default **on, `<se.7>`**, the same sound for all four.
- **Settings:** each alert can pick its own sound or none, with a **Test** button.

### Settings › Alerts › While automation runs

| Setting | Default |
|---|---|
| Knocked out | on |
| Stuck | on |
| Duty ready | on |
| Tells and party invites | on |
| Sound | `<se.7>`, with Test |
| Flash the taskbar | on |
| Stop my hand-offs on knock-out or stuck | on |
| Also stop Questionable | off |

---

## A9. Travel preflight in Setup (`preflight.png`)

### Where

Three game checks join Settings › Companions › Setup, under the plugin rows (`CompanionSetupCatalog`, game entries):
- they are checked when Setup opens, and before a walk starts;
- the first time a check fails before a walk, the walk still starts, and the Setup row is linked from the status bar note ("Movement is Legacy · Fix in Setup").

### A row

| Column | Contents |
|---|---|
| **What** (110 px) | Movement, Camera, vnavmesh, Conflicts |
| **Status** | A 7 px dot, then the status in words, 13 px semibold: **Standard** / **Legacy**. Under it, one line explaining the consequence, 11.5 px Secondary. |
| **Fix** (280 px, reserved even when empty) | One pill and its safety line ("Undo for 8 s") |

**Dots:**

| Dot | Meaning |
|---|---|
| filled Secondary | fine |
| copper | needs a change |
| hollow Tertiary ring | for your information |

Rows are at least 58 px tall (40 at Plain). The fix column is reserved, so a row has the same width whether or not it has a fix.

### The checks

| Check | Reads | Problem status | Fix | Safety |
|---|---|---|---|---|
| **Movement** | `IGameConfig` UiControl MoveMode | Legacy: "With Legacy movement the character can run the wrong way while vnavmesh walks." | **Switch to Standard** | An **Undo** toast for 8 s ("Movement: Standard · Undo"). The row then offers **Restore Legacy** until the next restart. |
| **Camera** | first-person mode | First person: "Walking turns the camera as it goes. Tsukimichi reminds you when a walk starts." | none (information) | |
| **vnavmesh** | `vnavmesh.Path.GetMovementAllowed` | "Movement paused by another plugin": walks start, then stand still | **Allow movement** (`SetMovementAllowed(true)`) | Undo toast |
| **Conflicts** | loaded plugins against a curated list (WrongWarpFinder first) | "WrongWarpFinder is loaded: it can send Lifestream to the wrong aetheryte." | **Open the plugin installer**: Tsukimichi cannot turn other plugins off, and does not try | none needed |

**Every fix changes one setting and can be undone. Nothing runs on its own.** A game setting is only changed by the player's click.

---

## A10. Automation level and About automation (`automation-level.png`)

### The switch

Settings › Automation, first row: **Automation buttons**, with one line: "Which buttons Tsukimichi shows. Higher levels add buttons that hand the game to another plugin."

There are four **level cards** in a row (each 1/4 of the column, at least 150 px tall). Each card has:
- a radio, the level's name and one sentence;
- at its foot, the **pills it adds**, drawn with their real icons.

| Level | Adds |
|---|---|
| **Tracker only** | Flag, Map. Tsukimichi tracks; it never presses anything for you. |
| **Travel** | Teleport and the aethernet, through Lifestream |
| **Travel and walking** | Walk to giver, through vnavmesh |
| **Full hand-offs** | Start (this quest) with Questionable, Run with AutoDuty, Craft with Artisan |

**Full hand-offs is a first-class choice.** It is the same size, words and weight as the others. It has no warning icon, no "advanced" label and no extra confirmation to pick it. Its first-start confirmations, per plugin, are unchanged, and they link About automation.

**Behaviour:**

| Rule | What happens |
|---|---|
| Hidden, not greyed | Buttons **above** the chosen level are hidden. Disabled AutoDuty and Questionable buttons in sight read as a nudge (the synthesis's evidence). |
| One click | The choice applies at once, with Undo. |
| Fine-tuning | A "Fine-tune each button ›" row opens the existing per-button toggles as overrides. |
| Existing users | They keep their current buttons: the level is derived from their toggles, and "Custom" shows when the toggles don't match a level. |
| Onboarding | Offers the choice once. The default for new users is **Travel**. |

**Selected card:** 2 px gilt inside edge (Snow `#A07B25`; Plain a 1 px Text edge), and the radio fills Moon. The layout is static.

### About automation

A card with six short paragraphs, each under a semibold heading:

1. **What Tsukimichi does itself.** It reads your game to track quests. It never moves, fights or talks for you. Every button that does sends the job to another plugin you installed, and only when you press it.
2. **What the other plugins do.** Lifestream teleports, vnavmesh walks, Questionable plays quests, AutoDuty runs duties with Duty Support or Trust, Artisan crafts.
3. **What the rules say.** Third-party tools are against the FINAL FANTASY XIV User Agreement. Square Enix has said it acts on what affects other players, and it can act on any account. No setting here makes automation safe.
4. **Where players draw the line.** Most players accept automation in solo content and with NPCs (Duty Support, Trust). They object when it runs in content with other players, or unattended for hours. Tsukimichi stops before a duty with other players unless you change that.
5. **Good manners.** Stay at the keyboard. Answer tells yourself. Keep runs short: Start does one quest unless you choose otherwise.
6. **Local only.** Tsukimichi has no network code. Nothing you do here leaves your computer.

**Where the card appears:**
- it is reached from Settings › Automation, from the Help window, and from every hand-off's first-start confirmation;
- it is never shown unasked;
- "Read the User Agreement" opens the official page through the existing link confirmation.

**The README** and the pluginmaster description gain the line "Local only: no network code."

---

## A4. Stop conditions and the run receipt (`run-controls.png`)

### Stop conditions

- **No split button.** This follows the A6 panel and the owner's static layout. The running **Stop** pill keeps its place, and its **"…"** menu holds the options:

| Option | Detail |
|---|---|
| Stop now | |
| Stop after this quest | Names the quest. Works for runs started some other way too: Tsukimichi watches `GetCurrentQuestId` and its own completion capture. |
| Stop after… | A popover with a stepper, 1–50 quests |
| Stop at… | A popover with a time field, local time |
| Remind me after 2 hours | A reminder, not a stop: a Needs-you-style panel without the copper bar |

- **The chosen condition** shows with a gold dot in the menu, and in the status bar's activity slot: "Questionable: Close to Home · stops after 3 more". It can be changed or cleared any time.

### The receipt

When the run ends, the **Finished** card (A2) carries it:
- the reason line ("It stopped after 14 quests, as you asked.");
- the span ("1 h 12 m · from The Rising Chorus to Close to Home");
- three numbers on sunken tiles: **quests**, **time** and **why it stopped**;
- **Show the 14 quests**, which opens them as a list filter.

**The chat line:** "Questionable ran 1 h 12 m: 14 quests, then stopped after 14 as you asked." It never uses an anti-detection framing.

---

## A6. The "…" menu items (`run-controls.png`)

The A6 panel decided that the detail pane's **Start (this quest)** pill does one quest: "Questionable does this quest, then stops." Its "…" menu gains:

| Item | What it does |
|---|---|
| **Start here and keep going** | The old behaviour: Questionable continues after this quest |
| **Do this next** | "#1 on Questionable's list". It inserts at index 0, reads the list back, and chat confirms. Enabled only when the reason gate shows a path. |

**When Do this next is disabled:**
- **While Questionable runs,** but only if Questionable#45 reproduces in game testing. The reason stays visible in the item ("Not while Questionable runs"), never only in a tooltip.
- **When there is no path,** with the reason "Questionable has no path for this quest".

Both items keep the existing confirmations. There is no new setting.

---

## Every look

The `stop-card-looks.png` matrix (3 levels × 4 palettes) is the reference. Every surface here is built from tokens the levels and palettes already define:
- the card frame;
- the pills;
- Text, Secondary and Tertiary;
- Line;
- the kit's ornament.

Each palette adds only its copper (and Snow's gold-as-text).

On light palettes, panels over the game use the palette's own sheet: Snow's white with a lead edge and navy shadows, as `needs-you.png` shows over a daylight scene.

---

## Decisions

1. **One new cue colour, copper, means "needs you".** Gold stays "finished or act now", silver means "you did it", and amber stays the Settings hint. It is always a bar or a dot, never a glyph.
2. **Why it stopped lives in the notice dock and the Todo overlay,** never in the panes. Attention cards stay until dismissed, and the others fade after 30 s.
3. **Fixes are safe by construction.** A fix restarts the same kind of hand-off or opens a view. Nothing chains, nothing answers the game, and Try again waits until you're up.
4. **Needs you is one calm panel:** top centre, one at a time, no pulse. It comes with a chat line, one game sound (`<se.7>` by default) and a taskbar flash.
5. **Copy report** never includes the character's name, world or chat.
6. **Preflight fixes** change one game or plugin setting, with Undo. Tsukimichi never turns other plugins off.
7. **The automation level** has four equal cards. Full hand-offs is first-class, and buttons above the level are hidden. New users default to Travel.
8. **Stop conditions live in the Stop pill's "…" menu**, with no split button. The receipt rides on the Finished card and one chat line.

## Open questions

1. **The default sound:** `<se.7>`, or none until the player picks one? I lean towards on: the alert exists to reach a player who looked away.
2. **The default level for new users:** Travel (my proposal), or Tracker only?
3. **"Keep going after it"** on the duty-guard card restarts Questionable after you clear the duty yourself. Is that wanted, or should the card only show the duty?
4. **The reminder after 2 hours:** on by default, or opt-in?


## Supervisor ruling and coordinator decisions (recorded at approval)

**Rule: copper is never the only carrier.** Every copper bar or dot sits beside words that carry the same meaning:
- the Needs-you panel's "NEEDS YOU" eyebrow (semibold "Needs you" at Quiet and Plain);
- the stop card's title in words, plus its action;
- each preflight status in words, plus its fix button.

All-clear preflight rows use a silver filled dot, and info rows a hollow dot, so shape also separates them. A later change may not remove these words.

**Copper colour distances** (OKLab ΔE; normal / protanopia / deuteranopia / tritanopia):

| Pair | ΔE | Note |
|---|---|---|
| Night copper #D08654 vs gold #F2D27A | 0.20 / 0.21 / 0.19 / 0.19 | apart |
| Night copper vs locked stripe #B25C7F | 0.15 / 0.15 / 0.14 / 0.10 | apart |
| Night copper vs Eclipse text #D68AA8 | 0.11 / 0.12 / 0.10 / 0.04 | close under tritanopia; never the same element type (a bar or dot vs a status word) |
| Night copper vs amber hint #C9A866 | 0.08 / 0.09 / 0.06 / 0.09 | close; amber appears only in Settings hints, copper only in run alerts |
| Snow copper #A8582A vs Snow gold #755308 | 0.10 / 0.05 / 0.08 / 0.11 | close under protanopia; covered by the words rule |
| Snow copper vs Snow plum #962A6A | 0.16 / 0.17 / 0.14 / 0.08 | apart |

**Defaults (coordinator):**
- the alert sound is on by default, with a per-alert "none" option and a Test button visible in Settings › Alerts;
- new users start at the Travel automation level;
- the duty-guard card offers "Keep going after it";
- the 2-hour reminder is off by default.

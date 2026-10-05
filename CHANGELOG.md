# Changelog

All notable changes to Tsukimichi are recorded here. The format follows Keep a Changelog; versions follow the plugin's AssemblyVersion (major.minor.patch). The section for a tagged version is copied into the Dalamud manifest's changelog by `tools/make_pluginmaster.py`.

## [Unreleased]

### Added
- **Moonlit:** click a column header to sort by it; click again to reverse, and a third time to go back to the usual order. The table remembers your choice.
- **Moonfall:** a peg game inside Tsukimichi. Open it with the Moonfall button at the right of the status bar, or `/tsuki moonfall`. Aim with the mouse, click to shoot, and clear the 25 orange pegs with 10 balls; the last orange brings on the Full Moon. Four starter levels for now, with simple shapes until the artwork arrives. It pauses itself in combat, in duties, in cutscenes and when you click away, and your furthest level is saved.

### Changed
- **Portrait photos come with Tsukimichi:** quest-giver photos are now part of the plugin, re-cut so faces sit centred, and photos that showed a weapon, a helmet crest or a hat now show the face. There's nothing to download, and a copy you downloaded before is removed to free space. To turn the photos off, set Settings › General › Look › Giver portraits to Game art.

### Fixed
- **Quest-giver portraits:** faces sit in the middle of the plate, eyes on the eye line. Varshahn, Estinien, Wuk Lamat, Krile and about 260 other portraits were cropped again and checked by hand.
- **Earlier arcs:** major characters now have a portrait earlier in the story, such as Alphinaud in A Realm Reborn, Alisaie and Krile in Heavensward, and Jullus in Endwalker.
- **Looks match the arc:** a quest shows the character as they looked at that point in the story. Examples are Tataru and Zero in Endwalker and Dawntrail, Yugiri in Stormblood, the twins' Heavensward coats in 5.4, and Sphene as the veiled queen in 7.0. The faces in "With …" lines follow the same rule and never show a later look.
- **Quest-giver photos** no longer replace a good game portrait. A photo that showed a staff or a lance instead of a face (Y'shtola, Estinien) now gives way to the hand-checked portrait.
- **Who's in it:** hovering a character's small portrait now shows it larger, with their name, as the quest giver's portrait does.

## [1.22.0] - 2026-10-04

### Added
- **What's new, after an update:** once Dalamud installs a newer Tsukimichi, a popup shows what changed in a few plain points, with a painting for each release drawn in your theme. It shows once per update, never on a first install, and several releases arriving at once become pages. Past notes, back to 1.14.0, are in Settings › Advanced › What's new.
- **Update ready:** Tsukimichi asks Dalamud at login and every few hours whether a newer version is waiting (on by default). A quiet status-bar note and a dot on the moon icon say so, and **Update** opens Dalamud's installer. Tsukimichi still makes no network request of its own.
- **The moon icon:** a small moon on your screen that opens Tsukimichi.
  - Hover shows Up next, your Ready count, journal room and events ending soon. Right-click offers Lock, Hide, Tonight and Settings.
  - Drag it anywhere, lock it, or hide it from Settings or `/tsuki icon`. Its place is kept per screen size, and it can hide in cutscenes, Group Pose and duties.
  - Each theme gives it its own subtle particles and hover glow; none under Reduce motion, and plain at Plain.
- **Server info bar:** "12 Ready" with a moon in your theme's colour, in the game's server info bar and Umbra's. Hover for the quick card, click to open Tsukimichi, right-click for Tonight.
- **Umbra:** the moon icon, the Todo overlay and the Needs-you panel keep clear of Umbra's toolbar. An optional **Follow Umbra** palette matches Umbra's colours. Settings shows the Umbra status and how to add the Tsukimichi for Umbra add-on.
- **IPC:** read-only, versioned calls for other plugins (the summary, and "open Tsukimichi at…"), documented in `docs/ipc.md`. Anything that starts a run still needs your click in Tsukimichi.
- **Portrait pack offer:** after you install Tsukimichi (or on your first login after this update, if you've never been asked), it offers the portrait pack once, at a quiet moment after What's new. "Download portraits" is the highlighted choice, and once you've clicked the offer Enter accepts it. "Not now" declines, or Esc once you've clicked the offer, and it won't ask again. Keys meant for the game never answer it, and nothing goes online unless you choose Download. The pack stays in Settings › Look.

### Changed
- **The What's new card leaves the main window;** the popup and Settings replace it.
- **Quest gates:** twelve late-game quests that need a dungeon floor, a variant dungeon record, an Island Sanctuary rank or a Triple Triad achievement now ask you to confirm ("I've done this") instead of showing Ready too early. The detail pane names where such a gate is stated, the Lodestone or Questionable included.

## [1.21.0] - 2026-10-04

### Added
- **Up next:** Tonight opens with one quest and the reason it was picked, plus a travel button.
  - It picks, in order: your route, then your goal, then the story, your pins, the closest stop, or the level you need.
  - To get back to Tonight, use the Tonight button, press Esc, or click the selected row again.
- **Where to go:** for a quest in your journal, the detail pane shows the step you're on in the game's own words, with Flag, Teleport and Walk. The giver is one click away, and a step with no place on the map says so. Right-click a journal quest in the Todo overlay or Nearby for the same.
- **All characters:** a third view on the Characters tab, with one table of every character.
  - The columns are story, goal, Ready quests, allowances, Moonlit and last seen.
  - Characters on your other game clients, or in other launcher folders you add under Settings › Data, are read-only; Tsukimichi only reads those folders.
  - Under each quest's title, **Your other characters** shows who can take it.
- **Alt goals:** "catch this character up to…" a patch, another character's unlocks, flying in an expansion, or every duty roulette, with Undo.
  - A goal card lists what's left, Ready first.
  - It offers Send to Questionable when that character is logged in here.
- **My blues:**
  - A **Do first** sort, with one card per tier: what the story needs, content, systems, high-end, then other jobs and societies.
  - **Set aside** a quest for later, or as Not for me. It leaves your counts, Nearby, Next stops, Up next, the server bar, the overlay and notices. Undo restores it, and the Set aside filter brings quests back.
- **Your story on one page:** the main scenario by patch, with each optional line placed at the quest that opens it. A pace line estimates the evenings left to the latest story; it is labelled an estimate and waits for 15 dated story quests.
- **Side stories:** the dashboard names side stories as players do: Tataru's Grand Endeavor, the Void quests, Cornservant, Cosmic Exploration and more.
  - A series you're up to date on reads "Caught up · continues in a later patch".
  - The What's new card says when one you started got new chapters.
- **Loose ends:** storylines you started and never finished, with finales the game marks only in yellow listed first. You can mark a line Not for me. A chat and Tonight line when a finale is Ready is opt-in.
- **Who's in it:** quests show which story characters appear ("With Thancred and Urianger"), naming only the characters you've already met. A "With story characters" quick view finds side quests with someone from the story.
- **Triple Triad:** a dashboard card shows who still has cards for you and which opponents wait on a quest, with that quest's state and a route to them. A quest that opens an opponent says so in Unlocks.
- **Nearby › Everywhere:** every zone with something left, grouped by expansion and starting with the one that fits your job. It has kind chips and a sort.
- **Text-to-speech friendly commands:**
  - `/tsuki msq`, `/tsuki next` and `/tsuki go` print plain sentences, with coordinates as "X 27, Y 34.8" and a compass direction.
  - "Say what's next in chat" (off by default) speaks after each step or quest, at most once every 10 seconds.

### Changed
- Every new name these features show follows the spoiler shield. Hover a placeholder to see why it's hidden, and right-click to reveal it.
- `/tsuki go` never travels to or flags a place hidden by the shield, and chat lines never link a hidden quest.
- Send to Questionable and travel are offered only for the character logged in on this client.

### Fixed
- The Nightmare's End, What Lies Beneath and Dead but Not Gone no longer read Ready before you've cleared floor 50 (or floor 100) of the Palace of the Dead, and Dead but Not Gone now waits for What Lies Beneath.
- Free for All and Mastery Rematch now say they need 25 or 30 beasts befriended.

## [1.20.0] - 2026-10-04

### Added
- **A wider spoiler shield:** past your story point, the names of places, aetherytes, duties, rewards and people are hidden too, not only main scenario quests.
  - They read "Dawntrail area 6", "Dawntrail aetheryte · area 6", "Dungeon (Lv 97)", "A mount" or "Dawntrail character".
  - They're drawn in a quieter colour, in the same place and size as the real name.
  - Hidden rewards show a moon-disc tile instead of their icon.
  - People you've already met keep their names.
  - It covers Unlocks, Path, Route, the Journal table and tree, the Duties board, Moonlit, Flight, My blues, tooltips, search, find by unlock, Wotsit, chat lines and copied text.
  - Hover any placeholder to see why it's hidden. Right-click it for **Reveal this name**, or **Reveal names in this quest**, for this session.
  - No Teleport, Walk or Flag leads to a place you haven't reached yet. When you view another character, a place stays hidden if either character hasn't reached it.
  - Settings › Spoilers has a new switch, "Hide places, duties, rewards and people". It starts with the same value as Hide story names ahead. The count line reads "212 story names and 486 other names hidden for …".
  - It reads the game data, so Evercold's new names are covered as soon as the data updates.
- **Before Evercold:** a card in Tonight and on the Characters dashboard listing what each character should finish before Patch 8.0. It shows only the lines that apply:
  - finish the main story;
  - make room in your journal;
  - job and role quests;
  - Dawntrail duties for the roulettes;
  - flying in Dawntrail.
  - Each line has its buttons, and its hover says why it's there.
  - Tick a line yourself ("you said so") and untick it any time. Lines the game says are done fold into one "Done" line the next time the card is built, so nothing moves while you look.
  - × hides the card for that character, with Undo. The dashboard keeps a "Show again" line.
  - The card goes away by itself on your own early-access day.
- **An optional portrait pack:** about 1,850 more quest-giver faces, from Garland Tools' NPC photos (by Celes). 10 MB from Tsukimichi's own GitHub release.
  - It downloads only when you choose Download in Settings › Look and confirm. Giver portraits gains a "Game art + pack" choice.
  - The download is checked against a fingerprint built into Tsukimichi before anything is installed.
  - A damaged pack says so and offers Download again. A failed update or removal shows what went wrong, and your installed pack keeps working.
  - Remove pack switches Giver portraits back to Game art.
  - A newer pack is offered only by a Tsukimichi update; Tsukimichi never checks online by itself.
  - Privacy & trust says exactly what the one download fetches, from where, and when.

## [1.19.0] - 2026-10-04

### Added
- **The game confirms it:** when the game itself offers you a quest Tsukimichi couldn't check, the quest shows as Ready, "Offered by the game", with the date.
  - Rows the game has confirmed say "seen in game".
  - If the game offers a quest Tsukimichi reads as blocked, the row says "game disagrees" and a card explains why. **Go with the game** trusts the game for that character, with Undo; "Use Tsukimichi's answer" in the "…" menu takes it back.
- **Gates Tsukimichi can't check:**
  - More quests that the game holds back for things outside the quest log are now gated, so they no longer read Ready too early: Palace of the Dead floor 50, Heaven-on-High and Pilgrim's Traverse floor 30, Resistance rank and mettle, Occult Record entries, blue magic learned, Skysteel and Splendorous tools, and the chocobo companion.
  - A gate that can't be read says "can't check" and where it was confirmed. **I've done this** marks it passed for that character (Take back undoes it), and **Where to start** selects the quest that opens it.
- **New Game+ is recognised.** The status bar, Tonight and the Todo overlay show "New Game+ · Shadowbringers - Part 2 · quest 87 of 112".
  - Replayed quests keep their real completion and get a "Replaying" chip, and replays send no notices.
  - End session stops the mode if detection ever sticks.
- **How you'll clear it:** every quest with a duty says whether NPCs can clear it with you or you need a group of 4, 8 or 24, whether it's high-end, and whether the story needs it.
  - The same badges show in My blues, on route steps and beside the Duty Finder.
  - An item-level warning names a gearset that would pass. From Patch 8.0 it uses your best job automatically.
- **A Duties card** on the Characters dashboard shows:
  - why each Duty Roulette is locked;
  - which duties and unlock quests open it, with Route and Pin all;
  - the duties you've unlocked but never cleared.
  - Selecting a locked roulette in the Duty Finder shows the same.
- **A true story meter:** the main scenario hover and the catch-up now count the side quests the story needs (the Crystal Tower series, the Hard primals, a Shadowbringers role quest line), with your next milestone and anything you skipped.
- **Where the EXP goes:** the EXP line names the job to hand a quest in on and how much of a level it's worth ("Hand in on DRG Lv 56: 50,700 EXP (5% of a level)").
  - It warns when a capped job would get nothing.
  - When a quest in your journal is ready to hand in on a capped job, the Todo overlay says "Turn in on a job that isn't capped". A chat notice is in Settings › Alerts, off by default.
- **Switch gearset** sits under the Job requirement of class and job quests. It reads "No Culinarian gearset saved" when you have none.
- **Journal room:**
  - The status bar shows how full your journal is from 25 quests.
  - Ready quests say "journal full" when there's no room to accept them.
  - **Make room** lists what you can finish now and what's safe to drop, and opens the game's journal. Tsukimichi never abandons anything for you.
- **Rewards you can buy back:** a finished quest's rewards say whether you still have them and where to buy them back ("Not on you · buy it back from a Calamity salvager, 100 gil"), with Flag and Teleport.
  - Moonlit now lists about 115 seasonal, collaboration and job rewards that are sold back only after their own quest.
- **Where to get hand-in items:** each item says where it comes from (vendor and price, crafter and level, gathered or fished, quartermaster, exchange), with Flag, Teleport and Open Gathering Log.
  - Hand-in counts show what the game will take, with a note when the rest is in the saddlebag or armoury chest.
- **Find by unlock:** type "Kugane", "Sirensong" or "flying Thavnair" in the search. An Unlocks group under it offers **Route to unlock**.
  - Flying routes list every aether-current quest, then the field currents left, each named by its nearest aetheryte and with a Flag button.
  - A new Unlocks group in the filter drawer (Mount, Flying, Duty, Feature, Job, Area, Emote, Orchestrion) keeps only quests that unlock those kinds.
- **Allied societies:**
  - A daily left in your journal across the reset reads "0 allowances until you turn it in", with Flag and Teleport.
  - Stored alts holding one show a quiet note instead of 12 allowances.
  - A hint tells you to keep 3 allowances on rank-up day.
- **Events ending soon:** seasonal events warn 3 days before a known end (adjustable in Settings › Alerts).
  - A card shows in Tonight, quests in your journal get "Ends in 2 days", and those quests are listed first in the Todo overlay.
  - A chat line and window notice are available, off by default.
  - Collaboration reruns carry their dated runs. You can enter an end date yourself for an event with none announced, and the Seasonal events list shows when the other events usually come.
- **Unlock data:** every aetheryte now names the quest that opens it. Unlocks shows guildleves and inn rooms, the Occult Record and phantom jobs.

### Changed
- Copy report on a "Why it stopped" card gives the job, level and zone from the moment it stopped.
- Crafter, gatherer and Blue Mage gearsets no longer count toward duty item levels or roulette levels.
- Role quests no longer offer Switch gearset. It now shows only on quests that require one class or job.

### Fixed
- "How you'll clear it" and the AutoDuty Duties section no longer name the duties of a main scenario quest the spoiler shield hides.
- Item-level walls no longer use another character's gearsets after you switch characters.
- "I've done this" is kept for characters that already had other per-character settings.
- Allied society dailies are never marked "journal full"; they don't use a journal slot.
- A reward in a saddlebag you haven't opened this session no longer reads "Not on you".
- Event end dates print in your time zone, and an event still running past its announced end uses your own end date.
- Tonight's ending-soon cards no longer flicker once a minute; the Todo overlay re-sorts when an event starts ending soon.
- The second Way of the Thaumaturge shows the class it unlocks.
- With two game clients open, reading the other client's saved files no longer fails now and then while it is saving.

## [1.18.0] - 2026-10-03

### Added
- **Why it stopped:** when a run Tsukimichi started stops, a card says why in plain words.
  - It offers up to two safe fixes: Start again, Show the duty, Keep going after it, Reload navmesh and retry, Teleport closer, or Open Setup.
  - Copy report never includes your character's name, world or chat.
- **Needs you:** if you're knocked out, stuck, your duty pops or a tell arrives during a run, one calm panel appears at the top of the screen.
  - It comes with a chat line, a game sound (pick one per alert, or none, in Settings › Alerts) and a taskbar flash when the game isn't in front.
  - On a knock-out or a stall, Tsukimichi stops its own walks, AutoDuty runs and Artisan crafts; stopping Questionable too is your choice.
- **Duty guard:** Questionable no longer walks you into a party with other players.
  - Before a duty with no Duty Support or Trust, Tsukimichi stops it and says why.
  - Settings › Automation › Questionable can switch this to Warn or Do nothing.
- **Stop later:** stop Questionable after this quest, after a number of quests, or at a time, however it was started.
  - Find it in the Send menus, the "…" menu, or by right-clicking Stop.
  - The status line shows what's set.
- **Run receipts:** each run ends with a line saying how long it ran, the quests done and why it stopped. Settings › Automation › Questionable › Recent runs keeps the last ten.
- **"Do this next"** (with "Show Questionable hand-off" on) puts a quest first on Questionable's list and says in chat where it landed.
- **Automation buttons** (Settings › Automation): choose Tracker only, Travel, Travel and walking, or Full hand-offs.
  - Buttons above your level are hidden everywhere, and you can fine-tune each button.
  - New installs start at Travel; if you're updating, you keep every button you had.
- **About automation:** a card explaining what Tsukimichi does itself, what the User Agreement says, where players draw the line, and that Tsukimichi is local only.
- **"Before a walk" checks** (Settings › Automation):
  - Legacy movement, with one-click Switch to Standard and Restore Legacy;
  - first-person camera;
  - vnavmesh movement paused by another plugin (Allow movement, with Undo);
  - plugins known to break travel.

### Changed
- **Start Questionable does one quest:** Questionable does it, then stops, and chat says "Questionable finished <quest> and stopped." The old "keep going" start is "Start here and keep going" in the "…" menu.
- **Walks recover by themselves:**
  - A walk that gets stuck or finds no path has vnavmesh reload its map and tries once more.
  - Go to giver walks to the nearest aetheryte or shard before taking the aethernet.
  - Flights land on solid ground beside the quest giver and walk the last few yalms.
- **"Craft with Artisan" turns into Stop** while Artisan crafts the run you started, and the status bar names the item.
- **A new copper colour** means "needs you" on every palette, always beside words that say the same.
- **Inside a duty, a knock-out only alerts you.** It never stops AutoDuty or Questionable, because the NPC healers raise you.
- **A Stop for a run already under way stays visible** after you lower the automation level.

## [1.17.0] - 2026-10-03

### Added
- **Mix moons by state** (Settings › Themes):
  - Pick each state's moon from any theme.
  - A plain note says when two moons look alike in a list, or when Ready stops being the loudest, and each option warns before you pick it.
  - "Fix it" suggests one change that clears the warnings and never undoes the moon you just picked; Undo follows.
  - Reset mix is press-and-hold, with Undo.
- **Frames are a choice:** Brass, Silver, Lead came, Astrolabe or Kirikane rims and badges, for any theme or mix.
  - Gauges and card ornament take the same metal, and moons mixed in from another theme wear your column's frames.
  - The Frames setting says in plain words when a metal makes two moons hard to tell apart.
- **New themes:**
  - **Astrologian's Orrery:** moons seen through a Sharlayan astrolabe.
  - **Sumi to Kinpaku:** tsukimi crests in black lacquer, shell-white and cut gold leaf, with its own Kirikane frames, gold-leaf heading crests and card corners, and its own flat moons at Decoration Plain.
- **New palettes:** Dawn (plum night with a rose horizon) and Kugane Lacquer (black lacquer at dusk, with a hazier, warmer sky).
  - Each has a high-contrast form.
  - On both, Ready quests get a soft gold halo.
- **Share your look:**
  - Settings › Themes › Share shows your look as a short code (TM1-…) with a Copy button.
  - Paste a code to see everything it would change, with sample moons, before you choose Apply; Undo follows.
  - A code from a newer Tsukimichi still works: what this version doesn't have is named and left out.
  - A mistyped code changes nothing, and a shared look never changes your High contrast setting.
- **`/tsuki look <code>`** opens the Themes page with that code previewed.
- **Themes tab in the glyph window** (`/tsukimichi glyphs`): compare two looks side by side, see how alike every pair of moons is under each kind of colour vision, and check Ready's lead.

## [1.16.0] - 2026-10-03

### Added
- **Themes** (new Settings › Themes page). Pick from Menphina's Medallion, **Ishgard Glass** (stained glass in lead and stone), **Aether Crystal** (cut moonstone in a silver setting) or Classic.
  - Each card shows the theme's moons; hover a card to preview it, click to use it, and Undo follows.
  - Theme moon art loads only for the themes you use.
- **Ishgard Snow,** a new light palette:
  - navy ink, deep-gold "act now" words and lead frames;
  - a still dawn sky with no stars;
  - banners in a soft daylight grade, and portraits in their natural colour.
- **High contrast** has designed forms for both Night and Ishgard Snow, with every text colour at 7:1 or better.
- **Reset appearance** returns to Menphina's Medallion on Night in one click, with Undo.

### Changed
- **Moon style, Moon colours and Follow Dalamud colours** moved from General › Look to Settings › Themes; search still finds them. Your settings carry over unchanged, and older versions still read them.
- **Every colour in the windows now comes from the chosen palette.** "Follow Dalamud colours" applies to every text colour, the tutorial card included, so light Dalamud styles stay readable throughout.
- **Night's faint captions and divider lines** are a touch brighter, so they read on cards and on hover.
- **The banner's location line always fits on one line.** It drops the "Main Scenario (…) ›" prefix first, and never cuts words off.

### Fixed
- Locked out, Not checked and Blocked status words, and the catalog error message, are easier to read.
- With Follow Dalamud colours on a light Dalamud style, the danger button's text, the tree's progress ring and the state stripes are readable.

## [1.15.0] - 2026-10-03

### Added
- **Quest givers have faces.** The Giver card shows the giver's portrait from the game's own art: Duty Support busts, Triple Triad cards, painted dialogue portraits and custom delivery portraits.
  - Portraits are night-toned to match the moon look. Hover one for a larger view and where it comes from.
  - They cover about 70% of main scenario quests.
  - Each portrait matches the quest's era, so early quests never show a later outfit or a face you haven't met yet.
  - Givers without art get a race silhouette, their allied society's emblem or their initials on the same moon plate.
  - The spoiler shield never shows a face from story you haven't reached.
- **Small portraits of who to walk up to** in Next stops, the Route window and the Todo overlay. The Journal table has an optional Giver column (right-click the header to show it).
- **Giver portraits setting** (Settings › Look): Off or Game art.

### Changed
- **Every travel and route button wears the game's own icon:**
  - the aetheryte for Teleport, Sprint for Walk and the map flag;
  - the quest's own marker for Go to giver, and the aethernet shard for the hop.
  
  Plain's buttons get them too. When space runs short, a button shortens its label, then shows the icon alone with the full name in its tooltip; it is never cut off.
- **Requirement lines show the thing they name:** the job, the quest marker, the allied society, the Grand Company rank, the mount, the achievement or the duty.
- **The Route window's title shows what you're routing to.**
- **The panels beside the game's quest windows have icon buttons,** with icons on their reward and unlock lines.
- **Duties with their own emblem show it instead of the generic category tile:** the Great Hunt, the Windward Wilds, Blunderville and the seasonal event duties.
- **The Duties section (Run with AutoDuty) and the Duty Finder hint show each duty's icon.**
- **More game icons in Characters:**
  - role ladders and job groups;
  - your Grand Company rank and allied societies;
  - collection progress, and achievements that need several quests;
  - Collection by character shows each reward's art.
- **Journal table:** crafter, gatherer and mixed-job quests show a Disciples of the Hand or Land tile, or the Class & Job emblem.
- **Plan:** the kind chips show each kind's Duty Finder icon, and expansions show their ring.

### Fixed
- A duty the game gives no picture shows the Duty Finder icon instead of a blank moon. Unnamed instances take their area's name.
- A followed route keeps its header icon after a restart.

## [1.14.0] - 2026-10-03

### Added
- **Back and forward through quests.** Use the new ‹ › buttons at the left of the toolbar, mouse buttons 4 and 5 over the window, or Alt+← and Alt+→. Hover a button to see which quest it goes to. The keys can be turned off in Settings › Advanced › Keyboard.
- **A moving night sky** (Full):
  - stars at three depths in warm and cool colours, with a slow twinkle;
  - a very slow drift while the window has focus, with a rare faint shooting star;
  - a constellation for the selected quest's region, shown in empty sky;
  - a shooting star when you complete a quest.
  
  "Moving night sky" and "Shooting star on completion" are in Settings › Look. A "Milky Way in the sky" setting is also there, off by default.
- **Trust you can check.**
  - Each release lists the SHA-256 of `latest.zip` and of the `Tsukimichi.dll` inside it.
  - Settings › Advanced › Privacy & trust shows the hash of the plugin you're running, with a Copy button, so you can compare.
  - A plain statement of what Tsukimichi reads, keeps and sends is in Settings, Help and `docs/privacy.md`. It sends nothing; it has no network code.
- **Journal badge setting** (Settings › Main window): Newly ready, Ready story and unlock quests, Every Ready quest (the old count), or Nothing.

### Changed
- **Quest-pane section names** (Requirements, Rewards, Unlocks, Moonlit, Path and the rest) are much larger and easier to read, in a brighter gold with a little letter spacing. With "Game fonts for headings" off they no longer shrink below the body text. The quest title grows with them so it still leads. Quiet and Plain get larger headings too.
- **The Journal's column headers** are a size larger and brighter, on a slightly taller header row.
- **The filter drawer is redesigned.**
  - It covers exactly the Journal tree and is only as tall as its content, so there's no empty grey block and no tree text shows at its edges or over its title.
  - A readable **Filters** header shows how many filters are on.
  - The sections are Show, Quick views and Advanced, with moon toggles and a Stalled after stepper.
  - With Advanced closed, seven summary lines show what each group is set to; click one to jump to it.
  - Reset sits at the foot next to "Showing N of M", and is always followed by "Filters reset · Undo".
- **The rail.**
  - Tab icons are larger and fill the bar, and the coloured lines between them are gone.
  - Hovering lifts an icon onto a soft plate. The selected tab gets a plate, a gold icon and a moon bead that glides to it.
  - Labels always fit inside their highlight.
- **The Journal badge** counts quests that became ready since you last looked, at one steady size, and disappears when there's nothing new. Click it to see them.
- **The Completed moon** has darker basalt seas and a soft, cool moonlight glow inside its well. At 96 px and up it shows three rim-lit craters; smaller medals leave them out, so they never read as specks.
- **Game icons everywhere they help:**
  - feature unlocks (PvP, Retainers, Hunts, the Gold Saucer, Island Sanctuary, Eureka, Bozja and more) show their game icon instead of a placeholder moon, in the quest details, the Opens column and Moonlit;
  - title rewards show their achievement's icon;
  - EXP and gil show the game's own icons.

### Fixed
- "A Pup No Longer" lists the PvP it opens, with the PvP icon.
- Empty reward slots no longer appear.
- The Opens column counts every unlock the quest details list.
- The Moonlit card's text is easier to read.

## [1.13.0] - 2026-10-03

### Added
- **Text size** (Settings › General): makes Tsukimichi's text larger or smaller, from 80% to 150%, separately from the window scale. It applies live, and layouts are measured with the font you choose.
- **Resizable Journal columns.** Drag any column's edge to resize it. Widths are saved, and "Reset column widths" is in the header's right-click menu.
- **Three Decoration looks that are clearly different:**
  - **Full:** the moonlit Moon Road, with a night sky, gilt brass cards, a night-graded quest banner with a hero medal, glows and Moon Road moments.
  - **Quiet:** calm and flat, with tonal panes, hairlines and lighter medals.
  - **Plain:** a dense ledger, with zebra rows, flat glyphs, an icon-only rail and no motion.

  Settings shows all three side by side, so you can see the choice before you click.
- **Moon Road moments** (Full): the rail bead travels between tabs, a glint runs along the road when you finish a quest, and a soft halo marks a quest that becomes Ready. Each plays once and never under Reduce motion.
- **Todo overlay:**
  - optional hiding in combat, while talking to NPCs or in group pose (off by default);
  - opacity down to 0, and a nearly invisible overlay lets clicks through except over its rows;
  - a short completion beat when you finish a quest.
- **Moonlit rewards show their own game art** at a readable size: mounts, minions, emotes, cards, hairstyles, bardings, titles and more.

### Changed
- **Settings is rebuilt into nine roomy pages:** General, Journal, Overlay & routes, Alerts, Spoilers, In game, Automation, Characters & data and Advanced. Every setting has a plain one-line description, and toggles and segmented pickers replace checkboxes. Flair is now "Decoration", and the glyph palette is "Moon colours".
- **The window scale changes every Tsukimichi window live** as you drag it. Scale controls live only in Settings › General, and version info is at the foot of Settings.
- **Gentle motion:** lists, tabs, buttons, tooltips and menus now move gently. Hover glides, selections settle and menus fade in.
- **Panels beside game windows rise in** instead of blinking, keep to one side, and stay up while you arrow through lists.

### Fixed
- Journal columns fit their content at every UI and text size, so job labels, expansion pills and icons are no longer cut off.
- Opening Flight, Plan or Moonlit for the first time no longer freezes the game.
- `/tsuki stop` and the hand-off buttons read Questionable's settings fresh before deciding, so a just-enabled "command after stop" is respected.
- Settings changes made just before quitting are saved.

## [1.12.4] - 2026-10-03

### Fixed
- The plugin installer shows the new Menphina's Medallion icon. Dalamud had kept the old picture because its address never changed; the icon now has a new address. If you still see the old one, restart the game once.

## [1.12.3] - 2026-10-03

### Changed
- **Rewards and Unlocks split cleanly.** Rewards shows only what you receive and keep: items, gear, currencies, mounts, minions, emotes, hairstyles, orchestrion rolls, Triple Triad cards, titles and soul crystals. Unlocks shows the access and abilities you gain: areas, aetherytes, duties, flying (aether currents), features, jobs and actions. Nothing appears in both.
- **Rewards and Unlocks look like a pair in the quest pane:** the same tile size and spacing, Rewards first, and a section with nothing in it is hidden. Action rows show their own icons, and titles moved into Rewards.

## [1.12.2] - 2026-10-03

### Changed
- **Unlocks never repeats what the quest's Rewards already show.** If a job, duty, emote, mount, minion or aether current is in the Rewards, it isn't listed again in the Unlocks column, the quest pane's Unlocks section, tooltips, the Todo overlay, Moonlit, the game-window panels or chat.
- **Unlocks now holds what a quest opens access to:** areas, aetherytes, duties, features, jobs and flying. Actions, emotes and collectables appear only when they aren't rewards. Next quests stay in the Path card instead of being listed twice.
- **Job and duty rewards show their icons** in the Rewards column and tiles.

### Fixed
- Unlocks no longer lists the same thing twice: two "Collect" actions, a title listed twice, a zone next to its own world map, or the Blue Mage feature next to the Blue Mage job.
- Moonlit's "Also opens" and the game-window panels no longer repeat the quest's own Moonlit rewards.

## [1.12.1] - 2026-10-03

### Changed
- **The Journal's quest list shows an Unlocks column**, on by default. It shows up to three icons per quest for the areas, aetherytes, duties and features it unlocks, and hovering it names them.
  - The column was called "Opens" and was off by default in 1.12.0; it is now switched on for everyone.
  - It stays in view on narrower windows: Expansion, Rewards and EXP step aside first.
  - Turn it off in Settings › Display › Planning if you prefer.

## [1.12.0] - 2026-10-03

### Added
- **New moons: "Menphina's Medallion".** Every quest state is a gilt-rimmed medal that keeps its detail at every size:
  - a moon road for Ready;
  - clouds over a new moon for Blocked;
  - a repeat arrow for Done this cycle;
  - a full moon with a gold check for Completed;
  - a shattered red moon for Locked out;
  - a question mark for Not checked.
- **Badges on the medals.**
  - Ready shows an open padlock, Blocked a closed one, and In journal a journal.
  - Quests ready on another job show that job's own icon. In quest lists the badge sits beside the medal.
- **Settings › Look › Moon style:** switch between the new Medallion medals and the Classic 1.11 moons, to compare them in game.
- **What a quest unlocks.** A new Unlocks section in the quest pane lists, with game icons:
  - areas and aetherytes;
  - dungeons and trials;
  - features and emotes;
  - the next quests.

  Click an entry to teleport through Lifestream, open the map or jump to the next quest. Right-click a duty to open it in the Duty Finder (it never queues).
- **Unlocks in more places.** Quest tooltips, the game-window panels, the Todo overlay, Path, Moonlit and `/tsuki` search say what a quest opens. An "Unlocked:" chat line follows each turn-in, and there's a new optional "Opens" column in the Journal (Settings › Display › Planning).
- **A completed quest's moon waxes to full** once, never under Reduce motion. Settings › Look says when Windows' "Show animations" setting has turned motion off, with a one-click override.

### Changed
- **The window no longer moves under you.**
  - The scope sits in the list's title with an ×.
  - Filters sit in one fixed line with "+N", and open as a drawer you can pin.
  - Notices float in a corner, clear of the action bar.
  - One-time prompts close after about 15 seconds, hovering pauses them, and anything that needs you stays.
- **The selected quest stays where it is** on screen when you filter, search, re-sort or the list updates.
- **Status lives in the status bar.** Starting or stopping a hand-off, or copying, no longer pushes the detail pane down; status and Stop sit in the status bar.
- **Moonlit's** filters fit on one toolbar line. The **Todo overlay** shows Questionable's status in its title line and no longer jumps narrower.
- **The Path card says what to do next:** "3 quests before this one" with a Next link, or nothing when the quest is up next. Chains show what's next in them over a slim bar.
- **Counts say what's left** ("3 left", "1 prerequisite left"); full totals are on hover.
- **Progress gauges and the rail's Journal moon** are gilt in a lapis groove around a moonstone moon.

## [1.11.0] - 2026-10-02

### Added
- **`/tsuki stop`** stops everything Tsukimichi started: walks, flights, Go to giver, Lifestream hops, Questionable, and AutoDuty and Artisan runs. One chat line says what stopped. Put it in a macro for one-key use.
- **`/ts` and `/moon`** work like `/tsuki`, subcommands included. Add your own aliases in Settings › Keyboard; an alias already taken by the game or another plugin is skipped with a notice.
- **A floating Undo** follows verdicts, Restore, Pin all, unpin, Hide and Don't track.
- **Settings › Keyboard › Safety:** choose how long a press-and-hold takes, or click twice instead of holding.
- **A new plugin icon:** the moon road on calm water, a Kugane shore and a stone lantern, in a gilt frame.

### Changed
- **Mark as unique and Not unique act only while Ctrl or Shift is held.** They save at once, with Undo and Add note. Enter in the note box no longer confirms.
- **Forget, Delete all, Replace Questionable's list and Apply need a press and hold.**
- **Flight's expansion icons are twice as big and sharp**, and line up with their headings. The zone banner's icon is bigger too.
- **Questionable's fork WigglyQuest is recognised.** The leftover "Questionable" stub at 99.0.0.0 counts as not installed.
- **Quests that need mounts wait for them.** The Firebird, Kamuy, Landerwaffe, apocryphal Bahamut and Wings of Legacy quests wait for the mounts they need. A quest that needs a mount or a house no longer shows Ready before that is checked.

### Fixed
- The stray "#" before Journal icons is gone, and chapter names look crisp instead of smeared.
- The Duties section and "Run with AutoDuty" show again in game. They had been hidden since 1.6 because the duty list failed to build. The main-scenario catch-up counts duties again.
- Replaying a chapter in New Game+ can no longer overwrite your saved progress, and the rest of your progress keeps saving during the replay.
- Learning the last mount of a set updates the quest that needs it straight away.
- Moonlit "Copy view as TSV" no longer crashes when the view is grouped by expansion.
- Removed a small stall every 5 seconds while travel or hand-off buttons were on screen. The travel buttons also do less work each frame.

## [1.10.0] - 2026-10-02

### Added
- **Mounting and flying for Walk to giver and Go to giver.**
  - Longer walks mount up (Mount Roulette or a mount you choose).
  - Where you've unlocked flying, the trip flies and lands beside the giver, staying mounted.
  - In towns it can Sprint.
  - A walk that really gets stuck tries a new path once. A detour around a building or up a slope doesn't count as stuck.
  - Settings › Integrations › Travel: "Mount for walks longer than N yalms", the mount, "Fly where flying is unlocked" and "Sprint in towns".
  - The detail pane shows each step ("Mounting…", "Flying to Varshahn…", "Landing…").
- **Givers inside buildings and story areas.** Teleport and Go to giver now aim at the way in for givers in the Waking Sands, the Rising Stones, Fortemps Manor, Zero's Domain and other interiors:
  - the aetheryte nearest the door;
  - an aethernet hop where one helps;
  - a walk to the door, then "go in to find X".

  The tooltip says where the giver really is.
- **Companion plugin setup.**
  - Settings › Integrations says whether you're ready for full automation ("Ready for full automation" / "2 plugins need setup"), and so do Help and the setup card.
  - Each companion plugin has a Setup list of the settings that matter for Tsukimichi's hand-offs, with ✓ / ✕ / ? and what to change.
  - "Apply recommended settings" changes AutoDuty and vnavmesh settings through their own IPC, after showing exactly what will change, and reads each change back.
  - TextAdvance, Boss Mod and the rotation plugins are now listed as companions ("Needed by Questionable / AutoDuty").

### Changed
- **The detail pane's travel and automation actions are labelled buttons** with the same weight as Teleport: Go to giver, Teleport, Walk to giver, and, when they apply to the quest, Start Questionable and Run with AutoDuty.
  - The one you can use now is highlighted.
  - A running hand-off turns into a labelled Stop, with a status line under the buttons.
  - Buttons you can't use yet stay visible and labelled, and say why.
  - On a narrow pane the labels shorten, then become icons, then move into "…", without cutting text.
- When the nearest aetheryte isn't attuned, the Teleport tooltip says so and names the one it uses instead. A substitute in another region is refused, with the reason.
- **Kienkan, Stygian Insenescence Cells and a few other interiors** now teleport to the zone their door is actually in.
- **Buttons explain setup problems.**
  - Walk and Go to giver say when a vnavmesh or Lifestream setting blocks them.
  - "Start Questionable" says when a setting would stop the run, for example "Questionable needs TextAdvance's quest accept on".
- **Start Questionable and Run with AutoDuty wait while Go to giver is under way.** If Questionable or AutoDuty is started from its own window mid-trip, Go to giver steps aside without stopping it.
- If Questionable's "command after stop" setting is on, Tsukimichi's Stop button says that Questionable will run that command (for example /li auto), and asks before the first time.

### Fixed
- **His Dark Materia and the other relic weapon steps** (Zodiac, Anima, Resistance, Manderville and Phantom weapons) no longer read Ready right after the previous step. Tsukimichi now checks the weapon you have equipped or carry, as the game does:
  - With the right stage, the quest reads Ready.
  - With the right weapon in your bags or Armoury Chest, it reads Ready on the job that can wear it.
  - Otherwise it reads Blocked with what's missing ("needs a relic weapon nexus equipped, you have Curtana Novus equipped").
  - Characters not logged in since this update show these steps as Not checked until their next login.
- **The Eureka zone quests** (And We Shall Call It Pagos, Pyros and Hydatos) and Lighting the Way no longer read Ready. They depend on Eureka progress or Doman Enclave rebuilding, which Tsukimichi cannot read. Each now waits for the quest that opens it, then reads Not checked with the reason.
- **Return from the Void and other givers with no aetheryte of their own** no longer show a Teleport that implies the giver stands at the aetheryte.
- **Stop now stops a walk** even while vnavmesh is still working out the path; the character no longer starts moving afterwards.
- Settings › Integrations no longer marks the newest Allagan Tools (1.15.0.13) as outdated: its version is now compared the way Dalamud numbers it.

## [1.9.0] - 2026-10-02

### Added
- **EXP and gil per quest** in the detail pane ("12,345 EXP · 1,200 gil"), from the game's own reward formula.
  - Quest Sync quests show their range.
  - Allied society and seasonal event quests show gil only.
  - An optional EXP column in the Journal table (Settings › Display › Planning, off by default).
- **"Levelling opens"** on the Characters dashboard: what each job's next level opens ("52→56 opens 7 quests (2 unlock quests, MSQ)"), with the quests on hover. The Tonight card says when the next main scenario quest is waiting for a level.
- **Main scenario catch-up.** "To reach the latest story: 143 quests, Lv 90–100, 18 duties", with each expansion on hover. Shown in the Tonight card, on the Characters dashboard and in Since you were away.
- **Allied society board** on the Characters dashboard. For each unlocked society:
  - rank and reputation;
  - today's dailies and allowances left;
  - Teleport to its giver;
  - the time to the daily reset.

  It can be turned off in Settings › Display › Planning.
- **Achievements that need several quests.** Tales of War, Tales of Magic, Tales of the Hand, Tales of the Land and The War Still Wageth On show under Story chains on Characters, with "3 of 5", the next quest and whether you've earned it. Each of their quests' detail panes says how far the set is and what's left. Works for stored characters too.
- **Read the story so far ("Previously…").** One page with the journal of your last main scenario quests, or of every quest of a story chain you've done, in story order, with Copy all.
  - Settings › Display sets how many main scenario quests (10 by default).
  - Only quests you've completed are shown, so nothing is spoiled.
  - Open it from Since you were away, a chain quest's detail pane, a chain's right-click menu on Characters, or /tsuki recap [quest name].
- **New Game+ badge.** The detail pane says whether New Game+ can replay a quest or it's once only. A new filter lists the once-only story quests you haven't done.
- **Moonlit "Added in" filter:** rewards by the patch their quest came in.
- **Free-trial view.** Turn on Settings › Display › "I'm on the free trial". Quest counts, the Characters dashboard, My blues and the Journal table then put Endwalker, Dawntrail and anything above level 80 in a "Beyond your trial" group instead of showing it as Blocked. Off by default.

## [1.8.0] - 2026-10-02

### Added
- **Characters › Collection by character.** A "who has it" grid of every Moonlit collectible (or every unlock quest) against your characters, with kind, "missing on any" and name filters.
- **Hide or stop tracking a character.**
  - Hide removes it from the lists.
  - "Don't track this character" saves nothing of it while it is logged in, and stays on when you forget the character.
  - Right-click it in the Characters list, or use the boxes next to Forget.
- **Settings › Data › Characters:**
  - group the list by data center;
  - show hidden characters;
  - see the hidden or untracked ones, with Show and Track buttons;
  - "Forget characters not seen in N days".
- **A search box in the Characters list,** and data center groups when your characters are on more than one.
- **A "not updating" status** for a character another game client saved with a newer Tsukimichi, or in a file this client cannot read, with the reason in its tooltip. It clears once this client saves that character itself.
- **Open on….** Open any quest on the Lodestone, Garland Tools, the Console Games Wiki or Teamcraft from the detail pane's "…" menu, or by right-clicking in the Journal table and My blues.
  - Moonlit rewards open on FFXIV Collect and Garland Tools.
  - Pages open in your browser; Tsukimichi itself stays offline.
  - A quest the spoiler shield hides asks before opening.
- **Copy for Discord** on routes, My blues and Compare. Plain bullets that read well in a message, split into parts of up to 2,000 characters ("Copy part 2/3"). Right-click the button to add links to quest names.
- **Copy table as TSV.** Right-click the Journal table or a Moonlit row to copy what's listed straight into a spreadsheet, with a link column.
- **Richer exports.**
  - Quest exports include the state, level, patch, main scenario and repeatable flags, and the Lodestone id.
  - Moonlit exports add the item id and the FFXIV Collect id.
  - A JSON Schema is published in docs.
- **For plugin authors:**
  - new IPC queries: bulk states; quests by state, zone or item; structured blockers; routes; Moonlit status; duty unlock quests; pins; abandoned quests; the next job quest;
  - per-quest change and unloading messages;
  - a drop-in client file, and a /tsuki ipc window for testing.

### Changed
- **The quest list takes the Moon Road look:**
  - the chapter's name in the game's title font, with its count ("160 of 213" while filters are on);
  - a clean header in the game's heading font, with a brass rule under it;
  - at Flair Full, a faint road line under quests you can take now.
- Cards are framed in brass, and the first card in each window gets corner marks at Flair Full. High contrast keeps a solid line.
- **Moon Road headings in more places:** a small star and a fading brass rule on Route milestones, the Todo overlay's and Nearby's section captions, and My blues' filter headings. The Route title uses the game's title font and wraps.
- Long quest names in Route, Nearby and the Todo overlay end in "…", with the full name on hover, instead of being cut mid-letter. Route's buttons wrap on narrow windows.
- The toolbar and status bar are separated by brass rules.
- The Duty Finder hint and the item hover hint follow your palette, including "Follow Dalamud colours" and high contrast, with a brass caption rule.
- While the catalog loads, a moon cycles through its phases; it stays still with Reduce motion.
- **Smoother play:**
  - Changing gearsets, levelling up and clearing a duty no longer stutters; Tsukimichi re-checks your quests in the background.
  - Opening another character, and comparing two characters, no longer freezes the game for a moment.
  - The moment after Tsukimichi finishes loading, or after you change the journal filing, is smoother.
- With several game clients open, another client saving no longer makes this one recompute everything every few seconds.
- The Todo overlay shows a new pin at once.
- **Characters keep one order everywhere:** logged in here, then in another client, then by name and world. Another client's save no longer moves them around.
- Compare with remembers your choice for each character. Without one, it shows a fixed choice rather than the most recent save.
- **Per-character settings now live in a file every game client merges,** so two clients no longer undo each other's changes or repeat a notice. These are the spoiler override, "Before you continue" notices and "why?" lines. They move over by themselves on the first load.
- Viewing a character that another game client forgets returns to your logged-in character. Forget says so when the character has just logged in elsewhere.
- Ages read the same everywhere ("23 h ago", then "1 d ago").

## [1.7.0] - 2026-10-01

### Added
- **"Worth it?" beside quest offers.** When an NPC offers a quest, a small panel beside the offer window says what it's worth to you:
  - what it unlocks;
  - its Moonlit rewards, and whether you have them;
  - its step in a chain, and the patch it came in;
  - whether it's repeatable or seasonal.

  It follows your spoiler settings and never accepts or declines for you. Open in Tsukimichi and Pin are one click away.
- **"What this opened" beside quest completions.** When you turn in a quest, a panel beside the reward window lists the quests it makes ready, what it unlocks and the next quest of its chain, each with Flag giver and Pin.
- **Journal companion.** While the game's Journal is open, a panel beside it gives Tsukimichi's verdict on the selected quest (chain step, what it unlocks next, Moonlit rewards), with Open in Tsukimichi and Route to this.
- Each panel can be turned off under Settings › Integrations › Quest panels beside game windows. Like the other in-game hooks, they pause on an untested game patch.
- **Clickable chat actions.** Tsukimichi's chat lines (notices, /tsuki search, zone, which and why) end with [Open] [Pin] [Route]:
  - Open shows the quest in Tsukimichi.
  - Pin pins it for the character you're playing.
  - Route opens its unlock route.

  They work in the game's chat and in Chat 2.
- **"Opened by that."** After a turn-in, one chat line says what it made available, e.g. "Opened: 2 unlock quests, 8 side quests (3 with a story) · [Show]".
  - Show lists those quests in the Journal.
  - Turn-ins a few seconds apart share one line, and no quest names are printed.
- **Chat 2.** Right-click a quest link, or an item link that is a Moonlit reward or needed for an open quest, and Chat 2's Integrations menu offers "Open in Tsukimichi" with the quest's state.
- **Item links in the game's chat** now get the "Tsukimichi: quest reward" and "needed for" menu entries, like items in your inventory.
- **Nameplate marks** (optional, off by default). Quest givers can show "☾ Pinned", "☾ Moonlit reward" or "☾ Ready on WHM" under their name. The marks follow your pins as you change them.
- **Quest toasts** (optional, off by default). The game's quest banner for a Moonlit reward or an unlocked duty.
- **Set up your road.** On a fresh install, after the tour offer, a card lists what Tsukimichi can do while you play, each with one line on what it gives you:
  - the Todo overlay;
  - the chat notice for new quests, and the "Opened" line;
  - the Nearby count in the server info bar;
  - item hints and the Duty Finder hint.

  Recommended turns them all on except the overlay, which stays your choice. Help › Quick start opens the card again.
- **Overlay and Nearby buttons** at the foot of the tabs, beside Help and Settings. Your first pin while the overlay is off asks once whether to show it on screen.
- **Help: While you play.** One page for everything that works while you play, with buttons that turn the overlay on and open Nearby:
  - the Todo overlay, Nearby and the server info bar;
  - item hints, an NPC's "quests here" and the Duty Finder hint;
  - abandoned quests, routes, following a route and Go to giver;
  - the panels beside the game's windows, and the clickable chat lines.
- **Help: What Tsukimichi reads and keeps.**
  - Your own characters only, and which files it keeps on your PC.
  - No network code, and links open only when you click.
  - How to forget a character or delete everything.
- **New commands.** /tsuki tour, /tsuki journal, moonlit, characters, flight and blues open a tab, and /tsuki route [quest name] opens a quest's unlock route. A search that finds nothing suggests a command when the word was close: "Did you mean /tsuki nearby?"
- **A reminder of what was restored.** When Tsukimichi opens on a stored character or with two or more filters on, one line says so, with Follow me and Clear.

### Changed
- **Settings has a section index and a search box.** The index becomes tabs when the window is narrow. The search finds any setting by its name or description, across every section.
- **Settings sections are reordered:** Display, Todo overlay, Routes, Notices, Spoilers, Keyboard, Integrations, Data, Advanced, About. Polling, Journal filing and "Enable game hooks on this untested version" moved to Advanced.
- **The Nearby quests settings moved to Settings › Integrations.** These are the server info bar count, keeping it visible at zero, and other-job quests. The cog in Nearby opens them there.
- Settings uses the Night look and Moon Road headings, with a "Help & tour" button at the top.
- Settings and Help now follow Settings › Display › Window scale. The window-scale slider applies when you let go instead of resizing while you drag.
- **The tour shows a real quest.** Its Read chapter selects one, so the requirements, path and giver it points at are real. Your selection comes back afterwards.
- **What's new collects every version you skipped,** newest first, as short highlights with More for the full text.
- Moonlit and My blues show the same "nothing matches" panel as the Journal, with Reset filters.
- Without a character the banner now reads "Log in and Tsukimichi reads your journal…".
- Known quirks gained two entries: HaselTweaks revealing hidden duty names, and Simple Tweaks' different scenario percentage. The Duty Finder hint moved to While you play.
- /tsuki glyphs is no longer listed in help; the README lists /tsuki report and the new commands.
- On a short window the rail drops its progress gauge before the crest; the Journal tab's moon shows the same progress.

## [1.6.0] - 2026-10-01

### Added
- **Companion plugins.** Settings › Integrations lists the optional plugins Tsukimichi works with: Lifestream, vnavmesh, Questionable, AutoDuty, Artisan, GatherBuddy, Allagan Tools, Quest Map and Chat 2.
  - Each one shows whether it is loaded, turned off, outdated or missing, and what it unlocks, with a Copy repo URL button.
  - There are steps for adding a custom repository.
  - A button that needs a missing plugin stays visible, disabled, and names the plugin.
  - A Help topic and a tour step explain them.
- **Walk to giver** (vnavmesh). It walks your character to the quest giver in the zone you stand in.
  - It shows "Preparing path… N%" while vnavmesh maps the zone, turns into Stop while moving, and stops by itself when you leave the zone.
  - It waits while Questionable or AutoDuty is running, and never stops another plugin's walk.
- **Go to giver.** One click teleports, takes the aethernet and walks to the giver (Lifestream and vnavmesh), with Stop at every step and a chat line if a step fails. A Stop pressed during the teleport cast lets the cast land and does nothing after.
- **Aethernet to <shard>.** For a giver in a city, it hops to the attuned aethernet shard nearest them, including the Foundation's hop to the Firmament.
  - For Island Sanctuary and Occult Crescent givers, Teleport hands the trip to Lifestream's /li island and /li occult; Lifestream talks to the NPC for you, and the tooltip says so.
- **Follow a route.** "Follow this route" in the route window puts it in the Todo overlay ("Route: everything for Dragoon").
  - The section shows the next three steps, any level you still need, and Flag next stop. Steps drop off as you turn them in.
  - When the route is done, a chat line says so and the section goes away. Your pins are not touched.
- **Flag next stop** in the route window, the overlay's route section and My blues. While you follow a route, the map flag can move to the next stop each time you turn in a step (Settings › Routes, on by default; the map does not open).
- **Where each route step is.**
  - Each step names the aetheryte nearest its giver.
  - Each step has Flag, Teleport and Walk, and a right-click menu with Go to giver.
  - Steps in a row at one aetheryte read as one stop ("3 quests near Camp Dragonhead"), without changing the order.
- **Routes to several things at once:**
  - "Route: everything for <job>" (unlock, job quests and role quests up to your level cap) from the Characters job rows;
  - "Route through my pins" from the overlay's Pinned heading;
  - a Route button on each My blues expansion card.

  Each target is marked where the route reaches it. If what's left is locked out, the route says so instead of "complete".
- **Route to unlock** beside the Duty Finder, and **Route to this** on My blues rows.
- **Next stops.** Quests you can pick up now, grouped by the aetheryte nearest their giver.
  - Your zone comes first, then the stops with the most quests (unlocks count double).
  - Shown in the Tonight card with Teleport, and as an optional Todo overlay section (off by default).
- **Send to Questionable.** Hand a route, a story chain or job ladder (right-click it on Characters), a My blues expansion or your pins (Todo overlay menu) to Questionable's priority list, in order.
  - Quests your logged-in character has done, has in the journal or is locked out of are left out.
  - Chat says how many it took: "Questionable: sent 14 of 17 (3 have no Questionable path)."
  - It adds to the end of the list by default. "Replace Questionable's list…" asks first, reads the list, empties it, and puts it back if sending fails.
- **Add and start Questionable.** Sends the quests and starts Questionable on the first one it can do.
  - It asks before the first start and names any plugin Questionable needs that is missing (vnavmesh, TextAdvance, Lifestream).
  - A Stop button shows while it runs. Starting is off while you view a character other than the one you're logged in as, and you can turn it off in Settings › Integrations.
- **Questionable badges and live status.**
  - The detail pane shows "On Questionable's list (#3)" and whether Questionable has a path; route steps show "Q #3" or "Q no path".
  - While it runs, the status bar and the overlay show "Questionable: running · quest · step 3 of 7" with Stop, and its quest is highlighted in the Journal.
  - The detail pane and Report this quest also compare Questionable's "can no longer be done" answer with Locked out, and its running events with the game's.
- **Run with AutoDuty.** For a quest that needs or unlocks a duty, a new Duties section in the details says whether AutoDuty has a path.
  - The button hands AutoDuty one clear in Duty Support or Trust, with Stop while it runs.
  - AutoDuty's own run mode, duty mode and loop count always come back afterwards, also when it fails to start.
  - The regular Duty Finder is used only if you allow it in Settings › Integrations (off by default).
- **Open in Quest Map.** A button in the Path section shows the quest in Quest Map's requirement graph; /tsuki why points there too.
- **Hand in.** A new section in quest details lists the items a quest asks for, with the amount and quality when the game data says, and how many you hold.
  - Counts cover your bags, armoury and saddlebag, plus retainers with Allagan Tools.
  - A high-quality-only item counts only high-quality items; retainer counts are labelled "(NQ+HQ)".
- **Craft with Artisan** and **Gather with GatherBuddy** on each hand-in item.
  - Craft asks for the right number of crafts for recipes that make several at once, and is disabled when you already have enough.
  - Without the plugin, each button stays and names it.
- **Copy missing items:** a Teamcraft import link, or an Artisan/Teamcraft "3x Item" list, for the selected quest or all pinned quests. It only copies to the clipboard.
- Item tooltips and the item right-click menu say "Needed for: quest" for quests in your journal or ready to take.
- With Allagan Tools, Moonlit marks relic and special weapons you hold anywhere (bags, armoury, armoire, glamour dresser, retainers) as owned.
- My blues rows gain Teleport, Walk and Go to giver.

### Changed
- **Teleport now knows which aetherytes you have attuned.**
  - It goes to the nearest attuned aetheryte and shows its gil cost and favourite flag. With none in the zone, it says "No attuned aetheryte in <zone>" instead of doing nothing.
  - When you already stand closer to the giver than the aetheryte, it goes quiet and says so.
  - A teleport Lifestream refuses (combat, casting, a duty) says why in chat.
  - It is greyed during a cast or loading screen.
  - Without Lifestream, Teleport buttons stay visible but greyed, and say Lifestream is needed.
- Tsukimichi automates only when you press a button that hands the work to one of the companion plugins.
- My blues groups zones of similar level by region, so the list no longer jumps back and forth across the map.

## [1.5.0] - 2026-10-01

### Added
- Moonlit, the Characters tab and the export show what your other characters own (mounts, minions, emotes, orchestrion rolls, cards, bardings, hairstyles, duties and more), as of their last capture. This includes characters logged in on another game client. A newly learned mount, minion or other collectible is marked as owned right away.
- Quest completion dates. A completed quest shows "Done 12 Sep 2026" in the details, and the quest export includes `completedAt`. Recording starts with your first login on 1.5: quests completed earlier show "Done before …", and nothing is guessed. The dates live in their own file next to your save, so an older plugin version can't erase them.
- Every Moonlit reward has an availability label: Get now, Event running (with its end date), Upcoming event, Event not running, Collab — may return, Past event — on the Online Store, or Gone for good. The export has a matching `availability` field.
- Moonlit Expansion and State filters (Ready now, In journal, Blocked, Done) and a "Group by expansion" option.
- "Copy missing" in Moonlit copies the rewards you don't have as a Discord-ready list, spoiler-safe. Long lists copy in parts of up to 2,000 characters ("Copy part 1/3"), and the button keeps its place while the game updates.
- After a game update, the main window says "Game updated: N quests are newer than Tsukimichi's data; rewards and patch info for them may be missing until an update." Dismiss it once per game version, or press "Show them". Filters › Added in › "New since data" lists those quests.
- Tsukimichi no longer saves a reading in which the game suddenly reports far fewer completed quests or an emptied journal, at login or while you play. It keeps your saved progress, tries again, and says so once in chat. If the game keeps reporting the same thing for a few minutes, Tsukimichi accepts it and says so, after backing up the earlier save. Seasonal and repeatable quests resetting on their own schedule are not affected.
- Each character keeps two backups of its save (`<id>.prev.json` and `<id>.prev2.json`), refreshed at most once a day and never with a save that lost progress. docs/restore-backup.md explains how to restore one. Forgetting a character or deleting all data removes them.
- The Done today / Done this week tooltip says when the quest resets ("resets in 3 h").

### Changed
- Moonlit counts each reward once. A reward several quests give (Guildhests, Retainers, Hunts, Grand Company enrollment, class unlocks) is one row, and the other quests are listed under "Also from". Rewards whose quests are all on a path you didn't take (another city, starting class or Grand Company) are hidden.
- "Artifact gear" is now Relic & special weapons, and each relic, Manderville, Skysteel, Splendorous or Phantom quest counts once ("1 of 18", the items listed on hover), its repeatable "another job" quest included.
- Moonlit rewards you can no longer get (a past event, a removed quest) leave the totals unless you tick "Count rewards that are gone for good", and a line says how many time-limited rewards you missed. Only rewards known not to be yours count as missed.
- Hunt bill tiers, the later Dawntrail sightseeing entries, the Summoner and Scholar egi glamours, and variant dungeons and the Sil'dih survey record now count as separate system unlocks. Every soul crystal is listed under Items.
- In-game features (item tooltip panel, item and NPC menus, server info bar, Duty Finder hint) now pause only when a new game patch arrives, not on every hotfix. "Enable game hooks on this untested version" covers that patch's hotfixes too.
- Settings › About compares game versions properly and, on a newer game, says how many quests are newer than the data. Requirement details name expansions from the game's own data.

### Fixed
- About forty quests no longer show as Ready before the game will offer them. This covers job, role, crafter, gatherer and allied society quests that also need a main scenario quest (The Princess and Her Knight now waits for A Fitting Payment), and quests like The Hero's Journey and Shadow Walk with Me that showed Ready as soon as you reached their level. "What's blocking" and routes name the missing quest.
- Allied society rank-up quests (such as "I Heard You Like Tanks") no longer read Ready before your reputation for that rank is maxed. They show how far you are, e.g. "Trusted 300/720 reputation".
- Allied society dailies the quest givers aren't offering today read Blocked "Not offered today", and no longer count in Nearby, Ready lists or other plugins' availability checks. This applies to the societies whose quest givers Tsukimichi has seen that day; the others behave as before.
- Repeatables the game tracks with a repeat flag (the Gift of Joy dailies, One Man's Relic, Seeking Inspiration, A Ruined Opportunity, the Komra weeklies and others) read Done today / Done this week after you turn them in.
- Your other characters no longer show yesterday's dailies or last week's weeklies as done, or stale allowances, after the daily (15:00 UTC) or weekly (Tuesday 08:00 UTC) reset. This includes the character left on screen after you log out.
- Bardings and hairstyles in Moonlit no longer always read unknown, and Moonlit no longer says "Obtained states need the live character" for your alts.
- Moonlit totals can reach 100%: path alternatives, duplicate rows and missed time-limited rewards no longer count against you.
- The Pride of Labyrinthos no longer counts as unlocking Margrat's deliveries; A Request of One's Own does.

## [1.4.2] - 2026-10-01

### Fixed
- Quests that need another quest finished first no longer show as Ready too early. The game lists some of these extra requirements separately, and Tsukimichi now checks them. Examples are The Killing Art, which needs the main scenario quest "Over the Wall", and the final quests of the Studium, the Dawntrail allied societies and the role quests: 47 quests in all. They now read Blocked and name the quest you still need, and "Route to this" includes it.
- On the day you rank up with an allied society, Tsukimichi no longer treats you as maxed out with them. Your real rank is shown and higher-rank society quests no longer read Ready. Characters saved on a rank-up day are corrected when they're next loaded.
- Allied society crafter and gatherer quests (Namazu, Dwarves, Loporrits, Yok Huy) no longer list their crystals, Cordials and society currencies as "Artifact gear" in the reward tooltip and the reward filter. They show as ordinary item rewards.
- "Pin all" on a route keeps the steps in route order in the Todo overlay. The overlay lists your first 8 pinned quests, with a "+N more" line that opens all your pins in the main window.
- Wotsit finds the quest you're looking for. Wotsit shows only each plugin's first 26 matches, so early quests used to crowd out later ones and Moonlit rewards. Entries now match on their name only. Quests in your journal or ready to take come first, then Moonlit rewards, then the rest, with completed and locked-out quests last. The order follows your progress within about 10 seconds.
- If you run Questionable in Japanese or Chinese, quests blocked only by your level no longer show a false "Questionable says…" disagreement. Questionable's answers also refresh after it reloads its quest paths, so a quest checked while the paths were still downloading no longer reads as having no Questionable path.
- Game icons no longer break a window if a game patch removes one; a placeholder takes its place. Large icons, such as the one in the reward tooltip, are sharp instead of blurry.
- Long tooltips, such as the hints in Settings and the seasonal history note, wrap onto several lines instead of running off the screen. In an item's quest hint, a status too long to sit beside the quest name moves to its own line.
- Expansion names on the Path chart use the same capitals as other headings, so they no longer print with stray letters on some system languages, such as Turkish.
- An error in one part of Tsukimichi (chat notices, the Todo overlay, Nearby quests) no longer leaves the others out of date. Every part still updates, and the error is logged once instead of on every change.
- Settings no longer says "Catalog unavailable" after a catalog rebuild that worked. If a rebuild fails, for example after Retry or a Journal filing change, the previous catalog stays in use, and the main window and Settings › About say so with a Retry button.
- If Delete all data or Forget character can't fully clear something, Tsukimichi says so and points to /xllog instead of reporting plain success.
- Unloading or updating the plugin no longer leaves menu entries or game hooks behind when one part fails to shut down. The final save gets its full time before the game closes.
- The export guide no longer claims FFXIV Collect or XIV Shinies can import Tsukimichi's files. FFXIV Collect's import replaces whole lists, so importing a quest-rewards file there would erase your other entries.

## [1.4.1] - 2026-10-01

### Fixed
- Moonlit no longer lists crystals, Cordials and allied society currencies (Namazu Koban, Hammered Frogment, Loporrit Carat, Yok Huy Ward) as artifact gear: crafter and gatherer society quests pay them through the same per-job reward table the relic and tool lines use, and 568 such rows had slipped in. Only equipment is artifact gear now; the relic weapons and tools are unchanged.

## [1.4.0] - 2026-09-30

### Added
- Settings › Display › Look, the first step of the new Moon Road look. Flair sets how much decoration the windows show: Full (the default) adds a soft night-sky shading at the top of the panes, Quiet keeps only the brass lines and dividers, and Plain keeps the look you know. Game fonts for headings draws the details pane's section headings (Requirements, Rewards, Path and the rest) in the game's own title font; a heading the game font can't show, such as one in Japanese, uses the usual font, and turning it off or choosing Plain brings the usual font back everywhere. Reduce motion has moved into the same group. Headings are in capitals in English and keep their usual case in other languages. At large UI scales the game fonts are drawn from their next larger size rather than stretched, so they stay sharp. With the high-contrast glyph palette, Full looks like Quiet.
- Every quest now has a picture at the top of its details. A quest without journal art of its own borrows the art of an earlier quest in the same chain or journal section (a later one only when nothing earlier has art), then the Duty Finder banner of the duty it unlocks, then the loading screen of the zone where it starts; anything still without art gets an original illustration made for Tsukimichi for its category (each expansion's main scenario, the sidequest regions, allied societies, class and job, Grand Company, seasonal events, Chronicles, Hildibrand, relics and the rest). The game's own art is read from your game install as you play and never ships with the plugin. At Flair Full or Quiet, hover the picture to see where it came from ("Journal art", "From a related quest", "Duty art", "Zone art" or "Tsukimichi art"). With the spoiler shield hiding artwork, game art shows only once you've seen what it shows: a quest's own art and its zone's once you've picked the quest up, art borrowed from another quest once you've picked that quest up, and a duty's banner once you've finished the quest that unlocks it. Until then the category illustration stands in, which gives nothing away.

### Changed
- The details pane takes on the Moon Road look (Settings › Display › Look › Flair Full or Quiet). The picture sits in a brass-cornered frame (a plain brass line at Quiet), with the quest's moon rising on its bottom edge (keyboard focus rings it like any other control) and the quest name in the game's title font under it, beside small chips for the expansion, level, jobs and patch; the line under them spells the state out ("Blocked · after …"). Requirements, Rewards, Moonlit, Path, Giver and Journal are open sections, each a star, a heading and a fading brass line, instead of boxes, with a moon-phase divider under the header. Everything inside works as before: the "Not yet" line, the requirement marks, bars and arrows, and captions that move under their heading when the pane is narrow. A completed quest's rewards wear a brass frame, and on the Path chart the quests you've done read in soft gold under brass headings. Below 320 px the picture gets a little taller for its width and the moon centres above the name. At Flair Full the moon rises into place when you pick a quest; Reduce motion turns that off. With the high-contrast glyph palette the picture's shading becomes a solid band and the brass lines are solid. Plain keeps the look you know, with the new pictures.
- The Journal tree shows each section's own icon, the one the game uses where it has one (the main scenario and sidequest markers, the expansion rings, the allied societies' emblems, class, job and role icons, the Grand Company and content tiles) and a small drawn emblem where it doesn't (Chronicles, Hildibrand, Seasonal Events, Other Quests, Removed from the game…). Each icon sits in a ring whose gold arc is how much of it you've done, with a small moon at the arc's tip, and a gold star on the ring marks quests you can take now where the Ready count doesn't fit. A thin gold road under each row shows the same progress as a length you can compare down the list, a "Journal" heading with your overall count sits on top, and small moon dividers separate the story, the side quests and the plugin's own entries. On the rail, the tabs hang on a thin thread with the open one lit in gold, the crest is the new moon emblem, Moonlit, Flight and My blues have new icons, and the Journal tab and your overall completion at the bottom are rings around the moon. With Flair on Full the rings fill in the first time they appear (not again when you scroll back to them) and the lit tab glows in; Reduce motion turns that off. With the high-contrast glyph palette the rings and roads use solid lines and the icons get a light outline. Flair on Plain keeps the tree and rail you know.
- Flight, Moonlit and Characters take on the Moon Road look. Flight shows the selected zone's loading-screen art as a banner, with the zone's name and its quest and field current counts on it (Full only; a drawn night sky shows while the art loads, and with the high-contrast glyph palette the text sits on a solid band). Each zone in the list has a ring of beads, one per quest current, lit gold when it's done, so "4 of 5" is four gold beads; at Full, currents you finish while you watch light up one after another (switching characters or logging in just shows the count). Moonlit's reward kinds show the game's own menu icons (Mount Guide, Minion Guide, Emotes, Orchestrion List and so on) inside a ring of how many you have, and a new Gallery view (the button beside the title; it remembers your choice) shows every reward as its icon: yours have a gold frame and a checked full moon, the others the quest's moon, so a Ready treasure stands out; a dash marks a reward the game can't tell you about for that character. Right-click a tile, or use its "…" button, for the same menu as in the table, including Not unique and Undo. Characters opens with the character's name and current job in a frame, the overall completion as a ring, and a ring for each journal section above the completion table (two rows of them until you click Show all sections). Section headings in these tabs have a small star and a brass line instead of a separator line. Settings › Display › Look shows a small preview of each Flair level; click one to choose it. Plain keeps these tabs as they were, apart from the Gallery, which is there at every level. Under the high-contrast palette the lines are solid, and obtained and done are shown by shape and line weight as well as colour.

## [1.3.0] - 2026-09-30

### Added
- Requirements you don't meet now stand out. A quest you can't take yet opens its details with a "Not yet" line that names everything in the way at once ("Not yet · level 56 (you're 52) and 1 previous quest"); a quest you're locked out of says why ("Locked out · Another city's start (Ul'dah)", with who the quest is for under it), and one another job can take says "Not on this job · ready on PLD". Each requirement you don't meet gets a ✕ and its value in the Locked out colour; a level, rank, reputation or carrier level gets a small bar showing where you stand against what it asks ("52 → 56"); and an arrow button beside it goes to the quest that clears it: the next previous quest to do, the unlock quest of a job you haven't unlocked yet, or the quest that opens a required duty. Requirements you meet go quiet, with a small check. In the quest list, the reason after Blocked or Locked out is in the same colour, and the Path chart puts the same ✕ after a selected quest you can't take yet.

### Changed
- The Journal tree reads shorter names: "Eden" for Chronicles of a New Era - Eden, "Hildibrand" for Hildibrand Sidequests, "Seventh Umbral Era" without "Main Scenario Quests", "Main Scenario" with an ARR–EW tag, "Allied Societies · DT" and "Class & Job". Hover a row to see its full name and where it sits in the journal; a name too long for the tree ends in "…" and does the same. On a narrow tree, rows give up their extras in turn instead of drawing over each other: the expansion tag goes first (a section keeps it whole at the end of its name, "Main Sc… · DT", so the two Main Scenario rows never look alike), then the small progress bar, then the count becomes a percentage, and at the narrowest the moon alone shows progress, with a gold dot where quests are Ready. The shorter names are English only for now; other game languages keep the full names.
- The tabs on the left are a slimmer rail, 64 px instead of 136, with each tab's name under its icon, a moon crest on top (click it for All quests) and, at the bottom, your overall completion with Help and Settings. These moved there from the toolbar and the status bar rather than being shown twice; the tour is in Help. On a window narrower than about 1,040 px, or with Settings › Display › Compact rail, the rail shrinks to 44 px of icons with their names on hover. The width it frees goes to the Journal tree.
- A narrow quest list keeps each quest's name and status: the name column now takes the spare room and ends in "…" when it is cut (hover for the full name), and the status always shows its first word, such as Ready or Blocked. As the list gets narrower the least important columns step aside first: Rewards, then Expansion, then Job (its icon alone before it goes), then Level, and they come back when there is room again; while the list is sorted by a column that has stepped aside, hovering the Name header says so. When the list is too narrow for the status beside the name (under about 380 px in English), each quest takes two lines: its name and level on the first, its status on the second. The row's "…" button no longer covers the end of the name. Columns you hide from the header's right-click menu stay hidden; because the columns work differently now, columns hidden or reordered in earlier versions are shown again in their usual order.
- The details pane reads properly when it's narrow: nothing breaks in the middle of a word or runs off the edge any more. Below 320 px the moon sits above the quest name, and each requirement puts its name above its value. Card captions such as "All met" move under the card title instead of running into it, and the line under the quest name (expansion, level, jobs, patch) and the journal path wrap between their parts. A long quest name on a banner moves under the banner instead of climbing over it, and the state badge ends in "…" when it has to. The chain line, the Moonlit buttons, the quest giver's name and the Path chart's quest names stay inside the pane (a shortened name shows in full on hover), and at the very narrowest the Teleport or Flag button becomes a round icon button.
- My blues, Moonlit, Characters, Flight, the cards and Settings stay readable when narrow. In My blues each quest takes two lines when the list is narrower than 560 px, with its status under its name so "Blocked" or "Ready" is never cut off; only its main kind shows, and under 420 px Flag and Reveal fold into one "…" button. Flight and the Abandoned list keep their buttons and status words whole (their buttons fold into "…" when narrow) and shorten long quest names instead. Moonlit's toolbar moves onto two rows, and its table hides Confidence, then Kind, but never State. The Characters tables shorten names rather than lose their numbers and status. Tonight, Since you were away, What's new, the empty-list messages, Settings and the filter panel wrap whole words and move buttons to the next line instead of overlapping or running off the edge. Hover a shortened name to read it in full.

### Fixed
- The "Not yet" line no longer compares your current job's level with a quest your current job can't take. A Paladin quest you're ready for on Paladin now reads "Not on this job · ready on PLD" on Lancer, without Lancer's level; a Culinarian quest read on Dragoon says "level 70 (CUL is 35)"; and a quest none of your jobs can take names the class or job it needs and its level, without a comparison. The requirement's line and its bar measure the same job.
- The details of an option of a choice you haven't made yet say "Choose one of 2" again (The Vital Title, the stelae).
- The "Not yet" line names a fourth requirement instead of "and 1 more", and an allied society you haven't started reads "(not started)" instead of "(you're None)".
- "Ready on" names the job you'd play: one of your current job's line or role first, and Paladin rather than Gladiator when they share a level.
- The arrow buttons beside requirements no longer lead to quests you can never take: a quest you're locked out of has none, and a previous quest removed from the game or locked out is skipped for the next one you can still do.
- Following an arrow button, or any "Show in Journal", now always shows the quest in the list: a search, expansion, patch, level range, job, reward or Repeatable/Seasonal filter that would hide it is turned off; the ones it passes stay on.
- The Characters tables' number and state columns are as wide as their contents, so "3123/5402", "100%" and a state like "Completed" are no longer cut in the middle of a letter, and the Level and Reputation headers show in full.
- Moonlit's Kind and Confidence columns come back when the table is wide again. The table used to save them as hidden when the pane had been narrow; because of that, Moonlit's column widths and order are reset once.
- On the Path chart, a selected quest's name cut short by its ✕ now shows in full on hover, and a quest ready on another job wears the ✕ too, as the "Not yet" line does.
- A button after a line of text that wrapped onto two lines (Flight's "Complete", "Show in Journal" on Since you were away, Unpin in Settings, Moonlit's offline hint and Undo) goes under the text instead of beside its first line.

## [1.2.0] - 2026-09-30

### Added
- Several game clients at once: when you play two or more characters in separate game clients, each client's Tsukimichi now shows the others' characters as "live in another client" (a ◎ dot and badge) in the character switcher, on the Characters tab, in Compare with and in the account view, with their latest progress, updated within seconds each time the other client saves. It works through the shared config folder only, so both clients must use the same Dalamud config folder; it never reads another game process or sends it anything. Forget character is not offered for a character logged in on another client, and Delete all data leaves that character's files to its own client. docs/multibox.md explains how it works and what it can't do.

### Changed
- Panes keep a minimum width: however you drag the dividers or size the window, the Journal tree stays at least 180 px wide, the quest list 320 and the details 260, so nothing is squeezed to a sliver; when the window gets narrow the details give way first, then the tree. The widths you drag to follow Settings › Display › UI scale, and double-click a divider to reset that pane to its default width (the tree now starts at 300 px, a little wider than before). Drag the tree's divider far to the left and the tree folds into a narrow strip of moons, one per section (hover for the name and count, click to show that section); drag it back out or double-click the divider to open it again. The main window can now be a little narrower. Pane widths set in earlier versions are not carried over.
- Weekly repeatables (and the few other non-daily repeatables that come back on a schedule of their own) read Ready again straight after you turn them in, because the game gives the plugin no data about their weekly cycle. They are out of every count, so this never changes a number.

### Fixed
- Finished sections no longer count other starting cities', classes' and Grand Companies' quests: a character who has finished the Seventh Umbral Era now reads 160/160 instead of 160/213. A quest on a path you did not take (another city's start, another starting class, another Grand Company, or the other side of a choice only one of which can be done) reads "Locked out · Another city's start (Ul'dah)" under its own name, the details say who it is for ("Only for Ul'dah Gladiator starters · you started in Gridania as a Lancer"), and it moves to a new Other paths node in the Journal tree; Filters › Advanced › Include other paths lists them in their sections too, and each node's tooltip says how many it left out. A choice you have not made yet counts once, and its options say "Choose one of 3". The main scenario position no longer stops at another class's "Close to Home", so the status bar, the dashboard, Tonight, the Todo overlay and plugins asking over IPC name your real next quest, and the spoiler shield no longer hides real quest names because of it. Another company's version of a quest you already did for your company reads Locked out instead of Blocked.
- Other paths follow what your character has actually done: a character whose game data never marked its first city quest done, but who played that city's "Close to Home" or story, is now read as having started there, so its own city's quests count and read Ready instead of another city's. A new character with nothing in its journal is taken to have started as the class it is playing and in that class's city, and while you have not joined a Grand Company every company version of a quest assumes the same company. Call of the Wild for another company now reads Blocked · Grand Company instead of Ready, and is no longer announced or offered as available (whether only your own company's officer offers it is still to be confirmed in game).
- Options of a choice you have not made yet (another city's version of a seasonal quest, say) no longer count as Ready in a running event's quest count, are no longer announced as newly available, and no longer appear in Nearby quests or the Todo overlay.
- A search or a Journal node whose only matches are on other paths now says so and offers "Include other paths", where it used to show an empty list with no reason.
- Compare with: weeklies, class intros and hidden progress rows no longer show as done on one character only, and an allied society daily you have done before counts as done, as it does in the Journal's counts. A chain with nothing left to do for your character (every step Locked out or out of season) no longer reads complete: its line is left out of the Characters tab and the detail pane. No Journal node can read more done than its total any more.
- Moonlit: an achievement or title you earn while the Moonlit tab is open now shows as obtained without reopening the Achievements window.
- Pane minimum widths hold to the pixel at every UI scale (at 130 % the tree could end a pixel short). Wrapped text no longer breaks at a no-break space, never starts a line with the "?", "!", "%" or "»" that French puts after a space, and keeps accents and marks on the letter they belong to.
- Flight: The Churning Mists, The Dravanian Forelands and The Sea of Clouds counted the wrong quests, because the game's own data names the next quest instead of the one that gives the current (and an unrelated quest for one Thavnair current). The zone count now follows the currents you have attuned, and falls back to completed quests only for a character you are not logged in on. The Flag and Teleport buttons point at the right quests. A Realm Reborn shows as one entry for all its zones, marked "you are here" in any of them.
- Allied society dailies no longer read "Done today" for good once you have turned them in once: "Done today" now comes only from the dailies you turned in since the last reset. In the counts, a daily counts as done once you have done it at least once, so a Daily Quests section fills up as you try each one and stays filled.
- Finished Chronicles of a New Era series and relic lines now reach 100 %: repeatable quests (Primal Focus, Unidentified Flying Object, the YoRHa weeklies, the relic repeatables) stay listed but no longer count, and a chain's next quest is never one of them or a quest you are locked out of. Primals read 14/14 and The Shadow of Mhach 6/6 when you have finished them.
- Hidden progress rows the game keeps behind the scenes (six in YoRHa: Dark Apocalypse, two in the Resistance weapons, four in Ishgardian Restoration, Recondition the Anima and Forged Anew) no longer count towards their section, no longer show in My blues or as unlock quests, and are no longer chain steps.
- The Job column reads "Any" for quests every class and job can take, where 216 main scenario quests read "Multi".
- What Lies Beneath (Palace of the Dead, floors 51–200) is filed under Palace of the Dead, not Gridanian Sidequests.
- Chain steps now follow the order you play them in, where a few quests the game files without a place came before the quest that unlocks them.
- Two game clients saving at the same time can no longer leave a half-written file or lose each other's changes: every file is written whole and then swapped into place, and pins and Moonlit verdicts made in one client are merged with those made in the other instead of being saved over, down to a single pin (one client unpinning a quest while the other pins another on the same character keeps both). Saving no longer holds up the game: a character file that was read-only or held open by another program froze the game for about a second and a half every ten seconds; saves now run in the background, and one that cannot be written is tried again later. A character file written by a newer Tsukimichi in the other client, or one that cannot be read, is left where it is instead of being moved aside as damaged, and an unreadable pins or verdicts file no longer makes the other characters' pins or your other verdicts disappear. Delete all data now keeps the pins of a character logged in on another client, and a pin that client saves at the same moment no longer brings the deleted ones back. Two clients in different languages, or two game installs at different patch levels, no longer delete each other's journal word index, and one starting up no longer removes the index the other is building.
- Moonlit: relic weapon achievements are gone from the relic quests (one quest showed up to 21 of them, all obtained as soon as you finished it once), and titles that need several quests, such as Seeker of Bounty, are no longer listed under each of those quests as obtained after the first one. Each aether current is counted once, where it was counted twice before. Titles and achievements now follow the game's own record once it is loaded, which happens when you open the Achievements window in game; until then, and for characters you are not logged in on, they are worked out from the quests each one asks for.
- Duty Finder hint and unlock routes: they now know the quests that open most dungeons, trials and raids, where many said nothing before: the main scenario dungeons and trials of every expansion, the later turns of the Binding, Second and Final Coil, the Alexander, Omega, Eden, Pandæmonium and Arcadion raids, the Ivalice, YoRHa, Myths of the Realm and Vana'diel alliance raids, Ruby, Emerald and Diamond Weapon, the Four Lords, Hildibrand's trials, the hard and side-story dungeons, A Relic Reborn's Chimera and Hydra, and the extreme trials and ultimate raids the Wandering Minstrel opens. Heavensward now opens the Aetherochemical Research Facility and the Singularity Reactor, not the old Diadem that is no longer in the game. Savage raids you unlock by talking to someone after a quest still show no quest.
- My blues: raids, trials and dungeons are named as such instead of "Other"; A Relic Reborn reads Trial and Job; quests that switch on a system (the Cactpot, housing, the Challenge Log, materia transmutation, the Fashion Report, the Duty Recorder, hunt bills, sightseeing logs, scrip exchanges, Cosmic Exploration, variant dungeons and the like) read System with its name; role, Blue Mage and crafter and gatherer quests read Job; and a deep dungeon's floor sets are listed once.

## [1.1.0] - 2026-09-30

### Added
- Languages: Tsukimichi's own text now follows Dalamud's language, with draft Japanese, German and French translations (English elsewhere); Settings › Display › Plugin language switches between following Dalamud and English, and the change applies at once. The translations are drafts made with machine assistance, and Settings says so: corrections from players are very welcome (CONTRIBUTING.md › Translations). Quest, item, NPC and place names already came from the game in your client's language, and now the Moonlit reward names and the role quest ladders do too (Japanese, German and French clients had no role quests on the Characters tab before). Long words get room: the tab rail, the quest table's column headers and the Status column widen to fit them. Report this quest stays in English on purpose, and so do the export files' column names and Moonlit reward names; quest names in exports are your client's.
- Since you were away: after a long break, a card above the detail pane says what the character was doing then and where each of those quests stands now (moved on, completed, dropped), where the main scenario stood then and stands now (route by route, names through your spoiler shield), how many quests the game added since, by patch series and as main scenario, unlock and side quests, with "Show in Journal" setting the Added in filter to that series, the events running now and the levels that changed. It opens at a login only when every one of your characters was last captured at least 14 days ago, so an alt you simply did not play stays quiet, at most once per session and once per return; "Don't show again" keeps it closed for that character, and Settings › Notices sets the days (0 turns it off). A character Tsukimichi has never seen, as on a fresh install, is asked "When did you last play?" with a patch picker (or "I'm new"), and the answer is kept. "Since you were away…" on the Characters tab opens it at any time. Help › Characters explains it.
- High-contrast glyph palette: Settings › Display › Glyph palette › "High contrast" redraws every quest moon in flat colours on a brightness ladder, with rims at least 2 px thick and a mark inside each one: a bold bar for Ready, a hollow bar for Ready on another job, a large seal for In journal, a check for Done, a solid bright disc for Completed, a thick rim for Blocked, a thick diagonal bar for Locked out and dashes for Not checked. Moons of the same shape differ at least 3 : 1 in brightness, so every state reads in greyscale, on a greyscale stream and with red-green or blue-yellow colour blindness. The halo gauges get a thicker track and arc at 3 : 1 against the window and each other, and the table stripes and check marks follow the palette. It works with the Night palette and with "Follow Dalamud colours", switching to dark inks on a light Dalamud theme. Help › Moon phases shows the marks while it is on; Standard stays the default.
- Journal text: the detail pane has a Journal card for every quest you have completed, with its journal entries as the game wrote them and its objectives, read from the game's own files (nothing is downloaded, and no quest text ships with the plugin). It opens behind "Read the journal", and Copy entry copies one entry. For a quest in your journal it shows the entries up to the step you are on, never further. On the character you are logged in as the text reads as the game showed it to you; on a stored character, words that depend on who reads them show both versions ("he/she"). Settings › Journal text › "Search journal text of completed quests" (off by default) lets the search box also find quests by the words of their journal entries, only among the quests the character shown has completed, so a search never spoils a quest ahead of you. The first time, the plugin reads every quest's text in the background (under a second to a few seconds, with progress in Settings) and keeps a word index of about 1 MB, not the text, in its config folder; it is rebuilt after a game patch. Help › Reading a quest explains it.

### Fixed
- Journal text search now finds French words after an elision ("Ishgard" finds "d'Ishgard"), a single kanji inside a longer word ("竜" finds "竜騎士"), names written with ・ typed with or without it ("ヤシュトラ" finds "ヤ・シュトラ"), names without their apostrophe ("uldah" finds "Ul'dah"), and œ, æ and ß spelled out ("coeur", "strasse"); the word index is rebuilt once to pick this up. "Since you were away…" opened from the Characters tab counts the quests added since your last visit, where it said none when the card had not opened on its own at login. The Journal card no longer shows the previous character's name after you switch characters. Delete all data also removes the journal word index.
- Languages: after a language switch, the job filter, the Job and Expansion columns, the Moonlit kinds, Nearby quests and its server info bar entry, reward tooltips and a few Settings lines now follow the new language at once.

## [1.0.0] - 2026-09-30

### Added
- Questionable cross-check: when the Questionable plugin is loaded, the detail pane says under a quest's status whether Questionable's own lock check agrees with Tsukimichi ("Questionable agrees") or not ("Questionable says: Prev quest (1)"), for the character you are logged in on, and Report this quest adds a "questionable:" line with both answers, so a quest shown in the wrong state is easier to pin down. Questionable is asked when you select a quest, not every frame. Settings › Integrations › "Show Questionable hand-off" (off by default) adds a "…" button to the detail pane's action bar with "Add to Questionable priority" (only with a Questionable that can say which quests it has a path for), which only puts the quest on Questionable's priority list: nothing starts, and Tsukimichi never moves your character.
- Before you continue: when your story reaches a point where optional content changes a scene and you have not done it, a line under the MSQ line on the Characters tab and in the Tonight card says what to do first ("Before you continue: Finish the Eden raid series first."). It names only the optional content, never what it changes or a main scenario quest ahead of you, and shows only while that quest is ready or in your journal. "why? (spoiler)" under it gives the reason, only when clicked, and stays open for that character. Chat says it once per character (Settings › Notices, on by default). Settings › Spoilers › "Show 'Before you continue' notes" (on by default) hides the lines and the chat line. The first five: the Eden raids, the Shadowbringers role quests and their epilogue, the role quest epilogue before the Pilgrim's Traverse's last floor, the story of Eureka, and the Bozja and Zadnor story. Help › Spoilers explains it.

### Changed
- The main scenario line is ready for branching stories: when a main scenario splits into routes you can play in any order and meet again later, as Evercold's is announced to, the status bar shows each route and how far along it you are ("MSQ · route A 3/9 · route B —", the tooltip naming each route's next quest and where they meet), the Characters dashboard, the Tonight card and the Todo overlay list one next quest per route, the spoiler shield counts "quests ahead" along each route, and an unlock route finishes one route before starting the next. Nothing changes on today's main scenario. Plugin authors get a new IPC gate, `Tsukimichi.GetMsqPositions`, with every route's next quest (docs/ipc.md).

## [0.9.0] - 2026-09-30

### Added
- Works with other plugins: other Dalamud plugins can now ask Tsukimichi about your quests over IPC: whether a quest can be picked up now, its state, what blocks it (the same lines as `/tsuki why`), the next main scenario quest, and "open this quest in Tsukimichi", with a message when your quests change. Answers are about the character you are logged in on, and quest names follow your spoiler settings. Nothing changes for you unless another plugin uses it; plugin authors find the details in `docs/ipc.md`.
- Correcting curated data: CONTRIBUTING.md explains which data file holds what, the evidence each correction needs (the Lodestone first, the wiki second) and how to check a change without the game. The Data correction issue form now asks for the file, the quest or item id, the evidence link and the diagnostic block, and a new IPC request form is there for plugin authors.
- Duty Finder unlock hint: select a padlocked duty in the Duty Finder or Raid Finder and a small panel beside the window names the quest that unlocks it, with its moon and what it is waiting for, and buttons to reveal it in Tsukimichi or flag its giver on the map. Nothing shows for a duty you have unlocked or one no known quest unlocks; it follows the character you are logged in on and your spoiler shield, and never queues or opens a duty. The curated unlock data gains the Crystal Tower and Shadow of Mhach alliance raids, nine Heavensward optional dungeons, five A Realm Reborn hard dungeons and three extreme trials, and, each checked against the quest's own script and the wiki, Snowcloak, Bardam's Mettle, Shisui of the Violet Tides, the Alexander and Deltascape raids, the four Variant dungeons, five hard trials (the Akh Afah Amphitheatre, Thornmarch, the Whorleater, the Striking Tree, the Limitless Blue), Hildibrand's three trials and the Grand Company quests that also open Dzemael Darkhold and the Aurum Vale. Like the other game hooks it pauses on an untested game version. Settings › Integrations › "Duty Finder unlock hint" (on by default); Help › Known quirks explains it.
- Unlock route: pick a job, a duty, a system or a Moonlit reward and get every quest the character still needs for it, in order, each after the quests it needs and lower levels first where there is a choice, with level gates where you must level up first and the main scenario milestones on the way ("24 quests · Lv 50–60 · MSQ: Heavensward"). Where a quest takes either of two earlier quests, the shorter way is used and the other is shown with how many quests it needs. Open it from the signpost button under a quest (Route to this), Route to this reward in a Moonlit row's menu, or Route to unlock… beside Job quests on the Characters tab (a right-click on a class under Jobs offers its jobs), so a fresh alt can see exactly what stands between it and Blue Mage. Viewing a stored character gives that character's route. Copy route copies it as a Markdown list, names through the spoiler shield and without your character's name; Pin all (hold, or Shift and click) pins every step not pinned yet, in order, for the Todo overlay (its count is those steps), with an Undo that only ever unpins them from the character they were pinned for. Help › Characters explains it.
- Clear my blues, a new My blues tab: every blue unlock quest your character has left, by expansion and then zone in story order, each with its moon, what it unlocks (Dungeon, Trial, Raid, Alliance raid, Field operation, Job, Allied society, Flying, System; "leads to" for the steps of a raid series or a job line), its status and what blocks it, and Flag and Reveal buttons. Filter by kind, "Ready only" or Sprout mode (expansions your story has not reached are hidden; it turns on with the Journal's Sprout mode quick view). "Copy as checklist" copies the list as shown as Markdown for Discord or a document ("- [ ] Hallo Halatali (Lv 20, Western Thanalan) — unlocks: Dungeon: Halatali"), spoiler shield applied and nothing naming your character. "Pin to overlay" on an expansion adds a "Clear my blues" section with its Ready unlock quests to the Todo overlay; Settings › Todo overlay turns it off or unpins the expansion. Seasonal event quests are left out. With Settings › Keyboard's tab shortcuts on, Ctrl+5 opens it. Help › My blues explains it, and the tour and Help › Quick start now include it.
- Patch of origin: every quest now knows the patch it came with. The detail pane says so after the level ("Dawntrail · Lv 100 · Any · Added in 7.5"); a new "Added in" filter (filter panel › Advanced, chip "Added in 7.5x") keeps one patch series, 7.5x meaning 7.5, 7.51, 7.55 and the rest, newest first in the list; and the Unlocks quick view opens with a "New in 7.5x" group, the unlock quests of the newest patch series (7.5, 7.51, 7.55 and 7.56 today), above everything else. The patch numbers come from Garland Tools' patch data for all 5,373 quests, checked against the game's own expansion for each quest, and future patches are added from the game data alone.

### Fixed
- The Stone Vigil and Castrum Meridianum named the wrong unlock quest (Blood for Blood, two quests too early, and Operation Archon); the Duty Finder hint, the unlock route and the My blues tab now name In Pursuit of the Past and Rock the Castrum. The eight A Realm Reborn class-starting quests (Way of the Archer, Way of the Gladiator and the rest) now read "Added in 2.0" instead of 3.1.

## [0.8.0] - 2026-09-29

### Added
- Patch-day safety: the item tooltip panel, the item and NPC menu entries and the server info bar entry now pause themselves on a game version newer than the one this release was tested on, instead of misbehaving after a patch. A chat line at login and a notice in Settings › Integrations say when they are paused; the quest journal and everything else work as usual, and the next tested update turns them back on. Settings › Integrations › "Enable game hooks on this untested version" (off by default) runs them anyway, at once, on the game version you are on; the next patch pauses them again. Help › Known quirks explains it. This release is tested on game 2026.09.15.
- Story sidequests, a new quick view in the filter panel: the sidequests with journal artwork (the picture in the quest box that marks a quest as part of a small story), zone by zone, with each side story in the order you play it. Two lines that meet in a last quest read as one story, so a zone's tale reads top to bottom: in each Dawntrail field zone that is two lines of four and the quest that joins them. The blue aether current quests that open those lines, and the blue quests that carry them on, are part of the story; other blue quests (dungeon, system and job unlocks) are left out. A small book after the name marks these quests in any view; hover it for the story and how far in the quest sits ("Part of a side story: When the Bill Comes Due (3 of 9)"). Select one and the detail pane shows "Story: When the Bill Comes Due · 2 of 9 done · next: …". Help › Filters and chips › Quick views explains it.
- Settings › Display › "Follow Dalamud colours": draws Tsukimichi's windows in your Dalamud theme's colours instead of the Night palette. The layout is the same either way; the moons keep their colours, and gold text, hints, the status bar and the other labels are kept readable against your theme's background, light themes included.
- Todo overlay: Compact mode (the moon and the name only, one line per quest, at a fixed width), from the overlay title's right-click menu or Settings › Todo overlay.
- Todo overlay and Nearby quests: a "…" button at the end of every row opens the same menu as a right-click (show the quest, flag the giver, teleport with Lifestream, link in chat), so everything works with a keyboard, a controller or one hand. The overlay's title has one too.
- Seasonal events you can do right now: while an event runs, the Todo overlay has an "Event quests running now" section between your pins and the unlock quests, with each quest you can start or have in your journal, its moon and its giver ("Lv 30 · Mayaru Moyaru"). An end date appears under the heading ("Ends Aug 28 (Lodestone)") only when the Lodestone announced it; otherwise nothing is guessed. Events run for the whole server, so a character you view from its saved snapshot shows the events running now on the character you are logged in on, and while no one is logged in an event whose Lodestone end has passed no longer shows as running on a character saved during it (its quests read Locked out). Settings › Todo overlay › "Event quests running now" (on by default).
- A chat line at login when an event has quests ready, "Moonfire Faire is running: 2 quests ready (ends Aug 28)" with a link to the first one, once per event per login. Settings › Notices › "Chat line at login when a seasonal event has quests ready" (on by default).
- Characters dashboard: a "Seasonal events" section with the events running now and all their quests (moon, state, what blocks them, giver on hover; click one to see it in the Journal), and "Completed seasonal quests by year", your seasonal history for collectors. The year is the one the Lodestone gave the event ("Moonfire Faire (2014)"), or counted from the nearest announced edition of the same event; collaboration events, which come back again and again, are listed under "Year not known". Help › Characters explains it.
- Lodestone dates for 83 past seasonal events (All Saints' Wake 2013 to The Rising 2026), each with its announcement's link, in the curated data, and the event's own name for the editions the journal files under another heading, so the running-now lines and the login notice say "The Make It Rain Campaign", "The Rising", "Little Ladies' Day & Hatching-tide" or "Keybound Brawler" rather than "Gold Saucer Festivities", "Rising" or "Collaboration Quests".
- Quest table and Moonlit list: a "…" button at the end of a row (shown when you point at the row or reach it with the keyboard) opens the same menu as a right-click, so Pin, Copy name, Show path, Quest Map, "Not unique (hide)" and the rest no longer need the right mouse button. The Menu key or Shift+F10 opens that menu on the row that has keyboard focus.
- Settings › Keyboard: optional shortcuts, all off by default because the game sees the keys too (Ctrl+1 to Ctrl+4 are hotbar 2 in the default keybinds): Ctrl+1 to 4 switch tabs, F flags the selected quest's giver, Enter shows the selected quest in the Journal from the Moonlit, Characters and Flight tabs, and P pins or unpins it. Help › Commands lists every key.
- Help › The moon phases: a legend of the quest table's state stripes, each drawn as the table draws it with its state and pattern name.

### Changed
- The Night look for the whole main window, title bar included, and for Help, Nearby quests and the Todo overlay: dark surfaces, cards and pills, tooltips and menus to match. Your Dalamud window opacity still applies.
- Gold now means "you can act on this": quests you can accept, quests in progress, the next step, pins and the main scenario pill. Things that are only information (selections, badges, headings, confirmations) are silver, and things already done (met requirements, owned rewards, a finished chain or zone) are a quieter gold.
- Todo overlay: its text is outlined so it reads over snow, sand and sky even at low opacity, hints are brighter, moons are never smaller than 14 px, and section headings are a quiet caption over a thin line (click to fold).
- Todo overlay: Locked now means click-through. A locked overlay cannot be moved and lets every click through to the game; set it from the title's menu, and unlock it in Settings › Todo overlay. If yours was locked before this update, a chat line at login says so once, and Settings › Todo overlay reminds you until you unlock it.
- Nearby quests: a click shows the quest in the Journal and a double-click flags the giver on the map; Flag and Teleport moved from buttons on each row into the "…" and right-click menu, so a stray click never plants a flag or starts a teleport.
- Reduce motion also turns off hover fades. Nothing in Tsukimichi moves on its own; hover fades pause while you scroll.
- Quest table: every row has a thin stripe at its left edge whose shape names the state, so it reads even without colour: a full bar for Completed, two bars for In journal, a short centred bar for Ready, a small centred tick for Ready on another job, the lower half for Done today or this week, dashes for Locked out, dots for Not checked, and none for Blocked. Hover the stripe for its name ("Ready · short bar"). Rows lift gently under the mouse, the selected row has a quiet outline instead of a coloured fill, headers are calmer with the sorted column brighter, levels and expansions sit in small pills, and quests for one job show that job's icon (hover it for the job's name). The Status column always shows the whole state word; only the reason after it is shortened with "…", and hovering shows the full line. Comfortable rows use thin separators instead of alternating shading (Dense keeps the shading), the moons grow with the row, and rows are never shorter than 24 px.
- Quests of seasonal events that ended in past years now read Locked out instead of Blocked, even on a character who never took part: the event's Lodestone end date is known and the game gives each year's event new quests. Collaboration events (A Nocturne for Heroes, the Yo-kai Watch event, Blunderville and the like) come back from time to time, so their quests still read Blocked between runs, even after you did part of one (before, finishing The Man in Black turned the rest of A Nocturne for Heroes Locked out), and any event the game switches on again reads by what the game says, not by an old date.
- The detail pane is a stack of cards. The quest's picture is a short strip (at most 96 px) with the state as a pill, so the requirements come first; the one blocking you is marked by a gold bar. Rewards are tiles you hover for details, with a gold ring and crescent on rewards found nowhere else and "Store only" or "Also drops" under the ones sold or dropped elsewhere. The chain line appears once, at the top of the Path card, and the giver has a card of its own.
- The Path is drawn as a star chart: a moon per quest on a thread that is gold where you have walked and dashed ahead, one band of sky per expansion, finished stretches folded into a bead ("12 moons walked") you can open, and this quest as the large moon with a halo. Where a quest accepts either of two previous quests, the other way in shows as a hollow moon beside the thread with how many steps it needs; click it to see its own path. What the quest unlocks next branches off below. Long paths scroll inside the card with this quest placed in view; a "target" pill brings it back and a thin bar beside the chart shows where you are.
- A button bar along the bottom of the detail pane: Flag on map (Teleport when Lifestream is installed), Pin, Show path, Link in chat, Copy coordinates, Open journal and Report, all usable from the keyboard. Pin and Show path no longer need a right-click. The last line now reads plainly: "Checked just now · live", or "From Michiru's snapshot, 2 d ago".
- With no quest selected, the detail pane shows Tonight: how many quests are Ready for you now (Show them opens the Journal showing just those, with the search and other filters cleared), the next main scenario quest and what blocks it, the seasonal events running now, and up to three pinned quests that are ready.
- When the table comes up empty, the filters responsible are chips: click one to clear just that filter, or Reset filters for all.
- A new toolbar: a rounded search box (Ctrl+F puts the cursor in it), the quick views as one control with All first (Unlocks, My level, Stalled, Story sidequests, Sprout mode), a Filters button whose number counts the filters narrowing the table, your character as a chip with the job icon, world and a live or snapshot dot (click it to switch character), and round Help, Tour and Settings buttons. On a narrow window or at a large UI scale it takes two rows instead of hiding anything. The filter panel keeps the Stalled days setting.
- A row of chips under the toolbar, only while something narrows the table: the tree selection first ("Scope: Sidequests › Gridania"; click it to go back to All quests), then one chip per filter, each clearing its filter. The search and the quick view are no longer chips, since the search box and the Quick views control already show them.
- The tabs run down the left edge in a column of their own, each with an icon, and Journal shows the number of quests you can pick up now. The Journal tree keeps its full width.
- The main window opens at a size that fits your screen at every UI scale (up to 1.6), and its smallest size makes room for the tab column.
- The tour is shorter and comes in three chapters you can jump between: Find, Read and Beyond. Read shows all eight quest moons side by side with their names. Enter or the right arrow moves on, the left arrow goes back and Esc closes; the card follows your UI scale, and the tab and the filter panel go back to how you left them when the tour ends. The first-run offer now has "Later" (asked again next session, up to three times) and "Don't offer again".
- Help › Quick start follows the tour: find, narrow, read why, then Moonlit, Characters and Flight.
- A little motion, only when something changes: a tree chevron turns as its node opens or closes, the Journal row and tree node you jump to from another pane, the dashboard or chat pulse twice, and a halo's ring slides to its new value when a quest is completed or you switch character. Nothing moves on its own, and Reduce motion turns all of it off.
- Esc closes the open menu or the filter panel first, and the window only after that; the game does not see that Esc, so it does not also close a game window or clear your target. Only Ctrl+F and Esc are bound unless you turn on more in Settings › Keyboard.
- Captions and titles use the game's own font at sizes picked for your UI scale: table headers, level and expansion pills, the status bar, card titles and the provenance line are a little smaller (never under 12 px), and the quest's name at the top of the detail pane and the headings of empty views and the tour a little larger. Settings' tooltips now match the rest of the window and follow the UI scale.
- Players whose saved table layout still had the narrow Job column get it widened once, so "DoH/DoL" beside the job icon is no longer cut off.

## [0.7.0] - 2026-09-29

### Added
- Abandoned quests are no longer lost: the game keeps no list of what you dropped from the journal, so Tsukimichi now does. The Characters dashboard has an "Abandoned (N)" section with each quest's moon, name, the step it had reached and when ("step 3 of 5 · 2 days ago"), and Flag, Teleport (with Lifestream) and Reveal to go back for it; "Show in Journal" opens the Journal under a new Abandoned filter (also under Filters › Advanced › Abandoned only, with its chip). Taking the quest up again or completing it clears the row. The list is kept per character beside its snapshot (`characters\<id>.abandoned.json`) and is deleted with the character or by Delete all data. It starts with this version: quests abandoned before it are not known. A seasonal quest the game clears from the journal when its event ends is not counted as abandoned (no row, no chat line).
- A chat line the moment you abandon a quest, "Abandoned: [quest] (step 3 of 5)" with a quest link and the giver's map link, so a mis-click in the journal is noticed before the duty has to be run again. Once per quest per session; Settings › Notices › "Chat line when you abandon a quest" (on by default).
- Export for spreadsheets and collection trackers: Settings › Data › Export writes your completed quests (quest id, name, journal section, category and genre, expansion) or your Moonlit collection (each quest-exclusive reward with obtained yes, no or unknown) as JSON or CSV, into `exports` in the plugin's config folder or a folder you choose, then shows the path with an Open folder button. `/tsuki export [quests|moonlit] [json|csv]` does the same from chat. The files never carry your content id, account or world, and your character's name only if you tick "Include character name". Quest names are written in full: the spoiler shield does not apply to files. Nothing is uploaded anywhere. The format is described in docs/export-format.md.
- Spoiler shield, on by default: a main scenario quest more than three quests past where you are reads "Main scenario quest (Lv 83)" everywhere a quest name prints: the tree, the table and its tooltip, the detail pane (header, path, what it unlocks, the chain line, the requirements), the status bar, the Todo overlay, Characters (the main scenario line, pins, recent activity, the Abandoned rows, Compare, the job ladders and chains), Nearby, item hints, chat links and every `/tsuki` answer, chat notices (the "Abandoned: [quest]" line too), Wotsit and the Report this quest block (which keeps the quest id). Searching for a hidden name finds nothing; the placeholder is what search matches. Quests you have accepted or completed always show their names, and "Reveal this name" in the detail pane shows one quest's name until the plugin reloads. Chat, item hints, the item menu and Wotsit follow the shield of the character you are logged in as, even while the window shows another one. Sorted by name, hidden quests follow their level (Lv 90 before Lv 100).
- Journal artwork shows only once a quest is in your journal or done; until then the detail pane and the name tooltip show a card saying "Artwork appears once the quest is in your journal".
- Settings › Spoilers: "Hide main scenario names ahead of me", "Quests ahead to reveal" (0 to 10, default 3), "Hide journal artwork until a quest is in my journal", and a choice for the character shown (use the settings, always shield, or show everything), so a character who finished the story can see it all while an alt stays shielded.
- Sprout mode, a new quick view in the filter panel: the table keeps to the expansions your main scenario has reached, with a line saying how many quests are in your reach ("412 quests in your reach"), and the tree's later sections fold to their counts; a selection inside a folded section moves to it.
- Help › Spoilers explains the shield, the artwork card, Sprout mode and the settings; the tour's table step mentions the placeholder.
- Settings › Display › Table rows: Comfortable (the default, taller rows) or Dense. Only the quest table changes.
- Reduce motion now follows Windows' "Show animations" setting until you set it yourself in Settings. If you turned it on in an earlier version, it stays on.

### Changed
- New moons. Accepted is now an early gibbous (a little more than half lit) with a silver rim and a small dark seal on its lit side, so it no longer reads as a full moon at row size. Larger moons (the detail pane header, the Help legend, tooltips) show a lunar surface: soft seas, a few craters, a glow along the terminator and a darker limb; small moons stay clean. A moon now only ever means a quest state or a completion: met and unmet requirements are a check and a cross, rewards you own or not are a check and a cross (a dash when unreadable), and the live indicator is a small dot, filled when live and hollow on a snapshot.
- Progress is now a halo: a ring that fills clockwise from the top around a small moon that fills with it. Even 17 of 612 shows a gold pip and 611 of 612 a visible gap; the ring closes and glows only when everything is done. It replaces the filling moon in the Journal tree, the status bar, the Characters dashboard, Moonlit reward kinds and Flight zones; where the ring is small the number sits beside it.
- Larger Journal tree rows (at least 30 px) with a 24 px halo, "done / total" with a small bar before it (the percentage is in the tooltip), a thin rule under each section, an expansion tag on nodes that belong to one expansion, and a gold count of quests you can accept now on any node that has some. The Journal tab shows that count too. Finished chapters, their halo included, are drawn in a quieter gold without the glow; the selected row has a gold edge.
- Status bar: the overall halo with its percentage, the quest counts, a still dot with "live" or the snapshot time, the main scenario quest as a gold pill (click it to select the quest), and the version at the right.
- Every one of the 5,373 quests was checked against the Lodestone and the wiki, and every quest-only reward against FFXIV Collect, the wiki and the Lodestone; 45 discrepancies were found and are now marked in Moonlit (no quest fact was found wrong): the 44 Darklight and Hero's accessories from A Realm Reborn quests that also drop in dungeons carry a new "Also drops" mark whose tooltip names the dungeons ("Also drops in Snowcloak, Sastasha (Hard) and The Sunken Temple of Qarn (Hard)"), shown too in the reward tooltip and the item hover hint, and the Clowning Around face paint from A Feast to Remember joins the rewards marked "Store only". The full list is in docs/data/verification-full.md.
- Moonlit: the "Hide store re-sells" checkbox is now "Hide rewards found elsewhere" and hides the dungeon drops as well as the store re-sells, from the list and from the obtained/total counts; a setting saved by an older version carries over. Help › Moonlit explains the marks and the filter.

## [0.6.2] - 2026-09-29

### Added
- "Why isn't this NPC giving me the quest?": target a quest-giving NPC and open the target bar's menu, and a new entry, "Tsukimichi: quests here (N)", opens the Journal on that NPC's quests with the Status column saying what blocks each one ("Blocked · after: Peace for Thanalan", "Blocked · Lv 50", "Ready"). The view shows as a chip, "Quests from Gerolt", that clears back to the whole journal. It reads only the NPC's kind and id (never a player's) and stores nothing; Settings › Integrations › "NPC context menu" turns it off.
- `/tsuki why <quest name>` (or `/tsuki why` for the selected quest) answers in chat: the quest with its state and blocker, then one line per requirement with met or unmet and the values compared ("Level: met (24 ≤ 31)", "PreviousQuests: unmet (Peace for Thanalan: not done)"), the same lines Report this quest copies. A quest you can take says whom to talk to, with the giver's zone and coordinates as a map link. Help › Commands lists it.
- Curated notes for the quirks the game data cannot express, shown in the detail pane under the requirements as "Note: …", printed by `/tsuki why` and carried in the diagnostic block: Up in Arms is optional once the Zenith is in hand (Gerolt offers the next Zodiac Weapons step without it); the allied-society quests that raise your standing to Allied, the rank patch 7.0 renamed from Bloodsworn; and the eleven crafter and gatherer sidequests whose prerequisite patch 7.5 moved from Go West, Craftsman to Inscrutable Tastes. Each note cites the forum thread, Reddit thread or Lodestone patch note it comes from; Help › Known quirks points at them.

### Fixed
- Custom deliveries: the 16 quests that need a satisfaction rank with a delivery client (Not While Their Names Are Still Spoken needs rank 4 with M'naago, A Gift from House Leveilleur rank 5 with Ameliance, and so on) no longer show Ready the moment the previous quest is done. The plugin now reads your rank with each client and the row says "Blocked · Custom delivery: rank 4 with M'naago" until you reach it; the detail pane and the diagnostic block show the rank you hold against the one needed. A character file written by an older version carries no ranks, so its quests read as before until the next capture.
- Delivery Moogle: the 17 postmoogle quests (Sweet Words, Shadowy Dealings, Death of a Mailman, All in the Family, …) have no previous quest in the game data, so every level 50 character saw them all Ready. They now gate on your carrier level ("Blocked · Delivery Moogle: carrier level 7"), a character who has not unlocked the Delivery Moogle included (carrier level 0). A character file written by an older version carries no carrier level, so these quests read as before until the next capture.
- Seasonal event chapters: an event that opens its later chapters on later days (Hatching-tide, some Starlight and Moonfire runs) showed every chapter Ready from day one. A chapter now waits for the event's phase: "Blocked · Seasonal: chapter not open yet" until it opens, "Seasonal: chapter over" once its phase has passed, and Ready in between; a chapter's quests count toward totals only while its phase is running. A chapter quest already in your journal stays "In journal" and counted while the event runs, even after its chapter has passed. Only these phased events are gated (seven events, 39 quests, whose quests carry more than one phase window); every other seasonal quest is Ready whenever its event runs, as before. Where the game does not report a phase, the event's quests behave as before too.
- If a chapter gate looks wrong during an event, please send the line that starts with "[festival probe]" from the Dalamud log (`/xllog`, written once per character and again whenever an event starts, ends or changes phase) with the report: the game keeps the event phase in three places and that line records all three, so the right one can be picked for the next release.

## [0.6.1] - 2026-09-28

### Changed
- The Unlisted bucket is gone. Of the 180 quests the game's journal never lists, 81 now sit where they belong: the "So You Want to Be a…" class intros open their class's quest line (the A Realm Reborn class intros are listed there but never counted, since the class you started as never offers its intro and it would keep that class one short for good; the job intros, A Dark Spectacle and the like, count as usual), Leves of Kugane and Sights of the North join their zone's sidequests, Squadron and Commander joins each Grand Company's quests, the three "And We Shall Call It…" quests join The Forbidden Land, Eureka, Seeing the Cieldalaes joins the Island Sanctuary quests, and the hidden steps of YoRHa: Dark Apocalypse, the Resistance Weapons, the Anima Weapons, the Ishgardian Restoration, the Pilgrim's Traverse and the Beastmaster quests sit in their own chains. Each one's detail pane says which rule filed it ("Filed under Kugane Sidequests (rule 6: issuer's zone)"); the rules are docs/data/unlisted-report.md, section 4, and the outcome per quest is docs/data/refile-expected.csv.
- The other 99 are quests the game removed (the A Realm Reborn trim in 5.3, the Summoner rework in 5.5, the Crystal Tower rewrite in 6.3, The Steps of Faith, I Believe I Can Fly, …), joined by eight quests the journal still lists but the game no longer hands out (But I Hardly Noah and The Gift of the Archmagus, A Seat at the Feast, Makin' Bacon (Bread) and Wok on By, and the three level-9 sidequests deleted in 3.05). They live under a new tree node, "Removed from the game", off by default (Settings › Journal › Show removed quests, formerly "Show Unlisted bucket"; the filter is "Include removed"). They never count toward any total, never appear in search, Wotsit, the status bar, Nearby, the todo overlay, Compare, the job ladders, the chains or the main scenario line, and an undone one reads "Locked out · removed from the game". A character who cleared the old A Realm Reborn story before 5.3 still sees those quests Completed when the node is shown; the new versions of the same quests stay undone until played. The detail pane names the patch that removed a quest where that is known, and otherwise the sign the game data carries ("Rule 1: placeholder issuer"). Switching the journal filing while logged in shows the catalog as loading until the rebuild lands, and the quest states are re-read against the new catalog rather than shown from the old one.
- Unlock quests now include the 58 "quasi-quests" the game accepts and completes in one dialogue (class intros, Leves of…, Sights of…, the Gold Saucer openers, Eureka entry, Palace of the Dead floors 51+, New Game+, Variant dungeons, the Diadem), and no longer lists removed quests.
- Settings › Display › Journal filing: "Refiled" (the default, everything above) or "Legacy", which files the same 180 quests exactly as 0.6.0 did, in one bucket, in case a quest lands somewhere wrong. Switching rebuilds the catalog on the spot. "Report this quest" states the filing rule and mode.
- Help › "Why my counts differ from the journal" explains the new node and the filing; genre-0 quests no longer show a " › Sephiroth Missions" journal path.

## [0.6.0] - 2026-09-28

### Added
- Report this quest: a Report button at the end of the detail pane's action row (and `/tsuki report`, or `/tsuki report <quest name>`) copies a small text block to the clipboard, ready to paste into a GitHub issue: the plugin, game and data versions, the quest, its state with the blocker line, every requirement with met, unmet or not checked and the values compared, the inputs it was judged from (job and level, and only what the requirements read: Grand Company, society standing, allowances, festivals, mount, house, achievements) and when they were captured. Nothing that identifies your character is in it: no content id, no name, no world. Settings › About now shows one data stamp ("Data: game 2026.09.15 · unique rewards 3,464 (generated 2026-09-28) · curated 573d225"), the status bar shows the same stamp on hover, and About warns in one line when the reward data was generated for a different game version than the one running. The "Wrong quest state" issue template asks for the block.
- The blocker line: wherever a quest is not Ready, one short phrase says the single thing to do first, in the same words everywhere: "after MSQ: Shadowbringers", "after: Peace for Thanalan", "Lv 80 on DRG", "Rank: Recognized with the Amalj'aa", "Grand Company: Sergeant Third Class", "Duty: The Vault", "Seasonal: not running", "Job: any Disciple of the Hand, you are WHM", "Mount", "House", "Not checked: achievements". Prerequisites come before level, level before rank, and a seasonal event last, so a level 80 job quest gated by the main scenario says which main scenario quest, not "at Lv 80". The Journal's Status column shows the state word first, then the blocker ("Blocked · after MSQ: Shadowbringers"), or the step for a quest in the journal ("In journal · step 3 of 7"); it is now the column that grows with the window, and the Rewards and Expansion columns step aside before it can lose the state word (a cut line shows in full on hover). The same line appears on the Characters job and role ladders (instead of "at Lv N"), the pinned and account rows, Compare, the Nearby window, the todo overlay hints, the item hover hint, the Flight table, the detail pane header, `/tsuki which` and `/tsuki zone`, and in the level-up notice when the level was reached but something else still blocks the quest ("Level 80 Dragoon: Gone but Not Forgiven · after MSQ: Shadowbringers").
- "What's new" card: after an update, the first time the main window opens, a card above the detail pane lists what changed in this version, with Close and Help. It never shows on a fresh install, and nothing is fetched: the notes ship inside the plugin.
- Help topics "Why my counts differ from the journal" (unlisted and removed quests, seasonal quests out of season, foreclosed choices, the derived Feature Unlocks node, repeatables) and "Known quirks" (steps the game skips such as Up In Arms with the Zenith, "Bloodsworn" reading "Allied" since 7.0, conditions listed but not judged, Online Store re-sells in Moonlit), both searchable; the Commands topic gains `/tsuki todo`.
- MIT license, a README written for players and Discord moderators (install, features, every command, what the plugin hooks and never does, releases, credits), a CONTRIBUTING page and GitHub issue templates for bugs, wrong quest states (with a field for the diagnostic block) and data corrections.

### Changed
- One name per quest state, the same everywhere (table, chips, tooltips, detail pane, Moonlit, Compare, the todo overlay, Nearby, chat links, Help and the tour): Ready, Ready on another job, In journal (was Accepted), Blocked (now always followed by what blocks it), Done today or Done this week (was Done this cycle; picked by the quest's reset), Completed, Locked out (was Foreclosed; followed by the cause) and Not checked (was Unknown). The moon-phase names (first quarter, waxing gibbous, eclipsed, veiled, …) stay as the small subtitle under each name in Help › Moon phases and the glyphs window only. Recent activity says "Picked up" for a newly accepted quest.
- Renamed labels: the Feature Unlocks tree node is "Unlock quests"; the presets group is "Quick views" with chips "Unlocks" (was Feature quests), "My level" (was Around my level) and "Stalled"; the "Next step" column is "Status" in the Journal table, the Characters pins and account view and the Flight table; the todo overlay section is "Unlocks you can start here"; the Moonlit pane carries the subtitle "rewards only a quest gives"; Compare's reason reads "Unlock quest"; requirement names say "Allied Society". A glossary of every name lives in docs/glossary.md.
- Moonlit: 68 seasonal-event rewards that the FFXIV Online Store also sells (Starlight Bear, Witch's Broom, Pumpkin Butler, the Bomb Dance and Huzzah emotes, Postmoogle Barding, Red Moon Parasol, ...) are now marked "Store only" in the row, in the reward tooltip and in the item hover hint, with the note that they are not exclusive to the quest. A new toolbar checkbox, "Hide store re-sells", drops them from the list and from the obtained/total counts; the setting is remembered.
- Every moon now says what it is when you hover it: the state's name and the shape to look for ("Blocked · new moon, silver ring"), with the reason under it where there is one. That covers the path and unlock moons in the detail pane, the large header moon, Moonlit, Flight, the todo overlay, Nearby, the Characters dashboard and its Compare rows; filling moons in the tree and on the dashboard show done/total and the percent. Moonlit reward icons show the same large reward tooltip as the detail pane (with the verdict's source under it), a reward whose kind has no icon shows a faint veiled moon that says so, and the confidence badges explain what static, community, curated and yours mean. Job icons on the dashboard name the job and its level, the state chip lists every hidden state in full, and the server info bar entry says "Ready". Tooltips on the detail pane's special badge now stay hidden behind popups and other windows.

### Fixed
- A double-click on a Todo overlay row flags the giver without first opening the main window over the overlay (a single click still shows the quest in Tsukimichi, a beat after the click), and sorting the Journal by Expansion now survives a window too narrow to show the column: the column stays while it carries the sort, and the Rewards and Expansion columns no longer flicker in and out at some widths.
- Allied society story quests (Ranger Rescue, An Eye on the Inside, Brotherhood of Ash and the like) no longer show Blocked on "needs 65535 reputation": the game data marks them with that value to mean no reputation gate at all, and the plugin now reads it that way. Their rank gate still applies.
- Quest levels now match the game's journal and the Lodestone. 215 quests (Quarrels with Squirrels, Surveying the Damage, Reach for the Starboard and others, mostly early sidequests, ARR main scenario and allied society quests) showed a lower level than the game because the level the journal prints adds a per-quest offset the plugin was not applying. The Lv column, the detail header, the Nearby list, the Todo overlay hints, the job ladder "next" line, the level-range filter, the "Around my level" view and the level sort all use the journal level now. Whether you can take a quest is unchanged: the game still accepts Quarrels with Squirrels at level 1, so it still shows Ready at level 1.
- The plugin installer shows Tsukimichi's icon: the manifest inside the release package now carries the icon address, and the icon ships as `images/icon.png`.
- Scaling and layout: the main window's minimum size now grows with the UI scale, so the quest table no longer collapses to a sliver at 1.5× and above; the reward-kind dropdowns in Filters › Advanced, the Moonlit confidence dropdown and the Characters "Compare with" dropdown open their lists at the window's scale instead of Dalamud's; the Todo overlay and the Nearby window scale their text and tooltips to match their moons, so a row is one line tall again at high icon scales; the Todo overlay's background opacity floors at 0.6 and its text gains a thin dark shadow below 0.9 so it reads over snow, sand and sky; the icon buttons, the search clear button and the active-filter chips are never smaller than 24 px, even at UI scale 0.9; raising the icon scale now widens the moon and reward-icon columns instead of clipping them; the saved sort column is restored on launch even when ImGui had remembered another; the journal banner in the detail pane is cropped rather than squashed when it is too tall for the column; and the status bar clips with an ellipsis instead of running past the window edge. Navigation: showing a quest from Moonlit, Characters, Flight, the main scenario line or a chat link now opens the Journal tree to that quest's genre and scrolls to it; clicking a path step, an unlock or the chain's next quest in the detail pane does the same, so the table always shows the selected row; the Moonlit row highlight follows the selected quest wherever it was chosen and survives marking a verdict; the Characters dashboard's Pinned section updates as soon as you pin or unpin a quest instead of up to a minute later (and no longer re-reads the pins file while you look at it); a Todo overlay row now shows the quest in Tsukimichi on a click and flags the giver on the map on a double-click, so a slip of the mouse never plants a flag (the right-click menu is unchanged); a Help topic hidden by the search box is no longer left showing; and the special-quest badge tooltip no longer appears through a window lying over it.
- Pandaemonium: Asphodelos: The First Circle is now credited to the quest that actually unlocks it, "Where Familiars Dare", instead of the chain's first quest "The Crystal from Beyond" (which stays listed as an unlock quest by its journal icon).
- Login: the first capture after logging in is no longer trusted while the game has not yet delivered the character's quest data (an all-zero completion mask and an empty journal). Before, that capture could reset every "accepted since" time, and the next poll then announced every pinned quest as newly available. Loading the plugin while already logged in now waits for the character the same way instead of retrying with a growing back-off.
- The "What's new" card shows after updating from 0.5.1 or earlier: those builds recorded no last-seen version, so the update looked like a fresh install and the card was recorded as seen without showing. A configuration that already exists now counts as an update; a genuinely fresh install still records silently.
- The login wait never gives up on a character the plugin has already seen: when the game takes longer than ten seconds to deliver the quest data, the stored snapshot (completion history, accepted-since times) is kept and the plugin keeps waiting instead of overwriting it with an empty character and announcing every quest on the next update. Only a character with nothing stored is still committed as empty after the wait.
- `/tsukimichi` help (the plugin installer's command list and Help › Commands) now names the `search <text>`, `settings` and `todo` subcommands, which worked but were not listed.
- Settings that cannot be read at load are no longer silently replaced: the unreadable file is copied to `Tsukimichi.corrupt-<timestamp>.json` next to it before defaults are written, and the log says where.
- Wotsit: a registration call that fails part-way through the list is retried from the same entry on the next tick instead of leaving the remaining quests and rewards unregistered until the next patch; after five failures on one entry the rest is skipped with a warning in the log.
- Settings › Delete all data now also clears what the plugin remembers about the logged-in character, so its files come back together on the next poll instead of the snapshot reappearing first and the accepted-time file only after the next journal change; forgetting the logged-in character no longer leaves an `.accepted.json` next to no snapshot.
- The account's expansion and level cap are now read from the game, so a quest above what the account owns shows that as its blocker ("requires Dawntrail", "level 100 is above your cap of 90") instead of Ready. Snapshots written by earlier versions carry no cap and are evaluated as before.
- Class quests taken on the job: a class-pinned quest (a Lancer quest, say) that your journal shows accepted on the class's job (Dragoon) now reads as available on that job instead of "Ready on Lancer"; the plugin reads which job each journal quest was accepted on and keeps it in the snapshot.
- The first evaluation after login or plugin load no longer stalls a frame: reading the character still happens on the game's thread, but resolving every quest's state now runs on a worker and the result is shown when it lands (a moment later); every later update stays incremental as before.
- Counts and totals: a seasonal quest whose event is not running now leaves every done/total count the way a foreclosed quest does (tree nodes, tab badges, dashboard sections, the Feature Unlocks count, Compare's "neither done"), so "Seasonal Events" and the overall total can reach 100 % between events. Completed seasonal quests still count as done; a quest blocked for any other reason still counts.

## [0.5.1] - 2026-09-28

### Added
- Settings › Display: "Reduce motion". With it on, hold-to-confirm buttons count down in text ("Hold… (0.4 s)") instead of filling an arc.
- Settings › Data: "Your Moonlit verdicts (N)" lists every quest you marked unique or hid as not unique, with the note and the date, a Restore button per row and "Restore all" (hold to confirm, or Shift and click).
- Test fixtures: a real schema-v1 character snapshot (anonymised) that must round-trip through the store unchanged, and a gzipped dump of the mapped quest catalog (`Tsukimichi.DataGen --dump-catalog`) so the tree-total, feature-quest and chain tests run without the game files; one game-data test checks the dump against the live sheets and says when to regenerate it.

### Changed
- CI: every push and pull request builds the solution with warnings as errors and runs the tests. The release workflow refuses a tag whose version differs from the plugin's, or that has no changelog section; it publishes tags with a suffix (`-rc1`) as prereleases without touching the plugin repository index, retries the index push, and can be re-run for an existing tag.
- Partial moons in the tree are visible again: a section that is only a few quests along shows a thin gold crescent instead of a dark disc, and one that is nearly done keeps a visible dark sliver until the last quest. Every state moon now sits on a slightly lighter disc with a coloured rim, so it reads at row size on dark backgrounds; Ready keeps a thin gold ring around it at small sizes where the glow used to vanish; a foreclosed quest shows a diagonal bar through its moon, so the state no longer depends on telling red from grey.
- Marking a quest as unique, and hiding one as not unique from a Moonlit row's context menu, both ask first in the same small popup: type a note (it has the keyboard already; Enter confirms), then press and hold the confirm button until the gold arc around it closes, or hold Shift and click. Releasing early cancels; Escape cancels. "Not unique (hide)" no longer applies on a single click.
- After either verdict a "Marked unique · Undo" (or "Hidden as not unique · Undo") line shows for eight seconds where you made the change; Undo forgets the verdict again.
- Quests you hid as not unique are no longer unreachable: the Moonlit "Yours only" confidence filter lists them struck through in grey, and their context menu offers "Restore shipped verdict". Every other filter keeps them hidden.
- Verdicts now remember when they were given; verdicts stored by earlier versions load unchanged and show no date.
- Help (Moonlit › Overrides and Restore) and the tour's Moonlit step describe the hold-or-Shift confirm and the ways back.

### Fixed
- Allied society dailies: accepting one daily no longer turns every other tribe's dailies Blocked with "not offered today". The game never stores the day's offer (the array the plugin read holds the dailies you have already accepted), so that check is gone; a daily you have picked up now shows Accepted instead of Ready, and one you have turned in today still shows done this cycle.
- Seasonal quests: known event end dates now reach the evaluation, so a seasonal quest of an event whose end date is known and past shows Foreclosed instead of staying Blocked forever (no end dates ship yet; they arrive with the verified data); an event with no known end still shows Blocked with "seasonal event not active" until it runs. Counts and totals are unchanged in this release.
- Switching characters without logging out in between no longer carries the previous character's Recent activity over, and no longer re-announces that character's newly available quests in chat for the new one.

## [0.5.0] - 2026-09-28

### Added
- Item hover hint: while the game's tooltip is up for an item that is a quest-exclusive reward, a small Tsukimichi panel beside it lists each quest that hands the item out with its state moon and "Quest reward: name", then "done" in gold for a completed quest or the next step otherwise; for mounts, minions, orchestrion rolls, cards and ornaments a second line says owned, not owned or veiled (stored character). The panel takes no input and stays clear of the game tooltip and the screen edges. On by default; a Settings checkbox turns it off.
- Item context menu: right-clicking such an item in the inventory, armoury, saddlebag or a retainer adds "Tsukimichi: quest reward (quest)", which opens the main window on that quest; an item several quests give shows "quest rewards (N)" with one submenu line per quest. Chat item links carry no item in the menu and get no entry. On by default; a Settings checkbox turns it off.
- Compare with on the Characters dashboard (alt diff): pick another stored character (the most recently captured one at first) and see "Done on A, not on B" and the reverse, ranked by unlock value (1 for any quest, +3 main scenario, +5 feature quest, +2 per unique reward) with a value badge and the reason ("Feature quest · 2 unique rewards"), the lacking character's state moon, and a click that reveals the quest; each list shows 25 rows and "and N more". A lead line ("A is 12 quests ahead of B"), done-on-both and done-on-neither counts, and per-section counts sit above the lists. A quest foreclosed on the other character (a Grand Company choice not taken) is not counted as missing. Copy list puts the whole list on the clipboard as "name (value)" lines. The other character is evaluated offline from its snapshot once per capture; with a single stored character the section shows "Log in on another character to compare."
- Todo overlay (`/tsuki todo`, Settings › Todo overlay): a small always-visible "☾ Tsukimichi" panel with one collapsible section per enabled part: your pins that are still to do (Ready first), the feature quests you can start in the current zone (up to eight), the next main scenario quest with its blocker, and the current job's next job and role quest when they are open. Each row shows the state moon, the quest name and a hint (next step, "Ready on PLD", journal step, or level and giver); hovering shows the state and next step, a click flags the giver on the map, and a right-click offers Reveal in Tsukimichi, Flag on map, Teleport to giver (with Lifestream) and Link in chat. Right-clicking the title locks or unlocks the panel, resets its position or hides it. The panel is hidden while logged out, in a duty or in a cutscene, and is rebuilt only when the session, the zone, the pins file or a section toggle changes.
- Settings › Todo overlay: show the overlay, lock its position (rows stay clickable), background opacity, the four section toggles and a "Reset position" button. Settings › Item hints: the hover hint and the item context-menu entry (the hooks behind them ship separately).

## [0.4.0] - 2026-09-28

### Added
- Nearby quests window (`/tsuki nearby`): the quests you can start in the current zone (Ready, plus Ready on another job unless turned off), each with its state moon, level, job and Flag and Teleport buttons (Teleport hidden without Lifestream); clicking a name shows it in the Journal. "Also accepted here (N)" folds out the accepted quests whose giver stands in the zone. The list is rebuilt only when the session or the territory changes. A cog at the top right holds the window's settings, stored in `user/discovery.json`.
- Server info bar entry "☾ N" with the count of quests you can start here; the tooltip names up to five of them and a click opens Nearby quests. Hidden at zero unless "Keep the entry visible" is on, and off entirely with "Show a count in the server info bar" unticked.
- Flight tab: every flying zone under its expansion with a filling moon of attuned currents (veiled for stored characters) and the quest currents done, the zone you stand in marked ● and selected first. The selected zone lists its quest currents from the AetherCurrentCompFlgSet and AetherCurrent sheets with attunement, quest state, next step, Flag and (with Lifestream) Teleport buttons; clicking a quest shows its requirements and path in the detail pane. Field currents are counted and pointed at the Aether Compass, never located. Since patch 6.0 the game's own sets hold five quest and four field currents per zone from Heavensward to Endwalker, five and ten in Dawntrail, and Mor Dhona's single current (The Ultimate Weapon) for A Realm Reborn; the view follows the sheet.
- Job quests on the Characters dashboard: one row per leveled job (icon, level, filling moon over its quest ladder, done/total) with the next quest, "Lv N" in gold when it can be taken now or "at Lv N" when not; click reveals it in the Journal. A job's ladder is its base class's quests, its unlock quest (Dark Knight's "Our End" and the like) and then its own quests, from the Class & Job Quests section of the sheet; role quests get one row per role the character has a job in (tank, healer, melee, physical ranged, magical ranged), with the Shadowbringers physical DPS line on both the melee and the ranged row.
- Story chains on the Characters dashboard: every curated chain (Hildibrand, the relic lines, the raid stories and the rest of `curated/chains.json`) with a filling moon, "N of M" and a clickable next quest; chains with nothing done yet fold under "Not started (N)".
- Level-up nudge: when a job's level rises and the next quest of its ladder or its role's ladder is open, a chat line "Level N Job: [quest] is available" with the giver's map link, once per quest per login session. Settings › Notices: "Chat notice when a job or role quest becomes available after a level-up" (on by default).

### Changed
- Tutorial and help cover the Flight tab and the Nearby quests window: a fifteenth tour step after Characters, a "Flight and nearby" help topic, an "Unlock flying" quick-start step, and `/tsuki nearby` in the Commands topic.

### Fixed
- The level-up nudge no longer announces a job or role quest that is already in the journal, or one that was already available before the level-up; only a quest the new level itself unlocks is named.

## [0.3.0] - 2026-09-28

### Added
- Chain progress in the detail pane: quests in a named chain (Hildibrand, the relic lines, Crystal Tower, Omega, Eden, Pandæmonium, the Arcadion, Myths of the Realm and the other Chronicles stories from `curated/chains.json`, plus every journal genre whose quests form a single line) show "Chain: name · N of M done · next: quest" with a filling moon and a clickable next quest.
- Seasonal and special quests show the journal's special icon as a badge beside the state moon in the detail header.
- Teleport to the quest giver through Lifestream: a context-menu item on table rows (hidden when Lifestream is absent, disabled while it is busy or the giver's zone has no aetheryte), backed by an aetheryte index built from the Aetheryte, MapMarker and TerritoryType sheets.
- Wotsit search: every catalog quest ("Quest: name") and every Moonlit reward ("Reward: name (kind)") is registered with Wotsit when it is loaded, re-registered when it reloads or the catalog rebuilds, and picking one reveals the quest in the Journal. Registration is spread over frames within a 4 ms budget per tick.
- Settings › Integrations: "Register quests and rewards with Wotsit" (on by default); turning it off unregisters the entries at once.
- `/tsuki zone` prints chat links for quests you can start in the current zone (Ready or Ready on another job, by level, up to ten plus "and N more"); `/tsuki which` prints every quest the targeted NPC hands out with its state.
- Moonlit confidence filter next to Hide obtained: Any, Static only, Curated only, Yours only, or only rows whose obtained state cannot be read.
- Presets at the top of the filter panel: Feature quests (unlock quests, the ones you can pick up now first), Around my level (current job level ±5, unsynced) and Stalled (accepted quests untouched for a number of days, default 7, slider beside the chips); the active preset shows as a toolbar chip and the empty-result guard names it.
- Accepted-since sidecar (`characters/<ContentId>.accepted.json`) recording when each quest entered the journal, kept by the poller from each diff (a step change refreshes it) and removed with the character or all data.
- Main scenario position independent of the journal's hide state: " · MSQ: <quest>" in the status bar with a tooltip naming the expansion, progress, NPC and zone (click selects the quest), and an "MSQ: <expansion> · next: <quest> (<NPC>, <zone>)" line on the Characters dashboard.
- Chat notice with a quest link and the giver's map link when a pinned or feature quest becomes available (Settings › Notices; main scenario quests only when included), one line per quest per login session.

### Changed
- Mount, minion, fashion accessory and job names read "Magitek Armor" and "Paladin" instead of the sheet's lower case.
- Moonlit rows without a reward icon now show one by kind: duty unlocks and instances use the duty's content-type icon, jobs their job icon, aether currents the attunement crystal, traits, achievements and blue mage spells their sheet icon.
- The Feature Unlocks node and the Feature quests preset use a derived set (1,699 quests): every quest the game draws with the blue "+" journal icon (`Quest.EventIconType` 8, which covers job quests and the Chronicles raid stories), plus curated system and duty unlocks, the shipped unique-reward unlock entries, and quests rewarding a duty, class or job, action, general action, trait, aether current, blue magic spell or a named other reward; main scenario and repeatable quests are excluded.
- Revealing a quest from another pane (Moonlit, Wotsit, chain links) also clears the active preset, so the revealed row is never hidden by it.
- `/tsukimichi` help text names the `zone` and `which` subcommands.

### Fixed
- `/tsuki which` printed every quest a prolific NPC hands out; it now stops at ten links and adds "and N more", like `/tsuki zone`.
- Teleport to the giver could pick the wrong aetheryte for city aetherytes drawn on several maps: the marker came from whichever map page the sheet listed first, so the position was converted with another map's scale and offset. The aetheryte's own map page is used now.

## [0.2.0] - 2026-09-28

### Added
- Help, Tutorial and Settings buttons on the main window toolbar.
- Interactive tutorial: fourteen steps that dim the window and highlight each region, offered on first run and restartable from the toolbar, help window, settings or glyph window.
- Help window rebuilt from cards, phase rows, numbered quick-start steps with Try-it buttons, key caps and tips, with topic search.
- UI scale and icon scale sliders (defaults 1.15× and 1.25×) in the filter panel and settings; the window title bar keeps Dalamud's size.
- Quest journal artwork in the detail pane header and in a tooltip on quest names.
- Characters tab dashboard: completion by journal section, Moonlit summary, pinned quests, recent activity, job levels grouped by role with job icons and base classes hidden once the job is unlocked.
- Reward tooltips with a large icon, item level, category and description; Copy coordinates; `/tsuki` alias; `/tsukimichi help`.
- Pinned quests sort first (toggle in the filter panel and persisted).
- Path section grouped by expansion with completed runs folded, plus an Unlocks next list.
- Mark a quest unique or restore a "not unique" mark from the detail pane.
- Poll timing readout in Settings › About.
- Tooltips on every toolbar control, filter, and column header.
- DataGen `--verify` mode that checks the reward database structure, icon files, banner artwork and cross-checks entries against xivapi.

### Changed
- Filter chips moved onto the toolbar row so the layout below never shifts; the State chip names the excluded states.
- Categories with a single genre fold into one tree leaf.
- Open journal is available only for accepted or completed quests, since the game journal has no page for others.
- Moon glyphs gained shading and a highlight arc at 20 px and larger.
- Tree rows show the filling moon before the name; table rows alternate shading and carry gold or silver stripes for ready and accepted quests; requirement marks are small moons; empty states show a veiled moon with guidance.
- Unique reward data tightened: 713 items that are not quest-exclusive (Fantasia, cordials, tickets, coffers, vendor-resold items) removed; 3,464 entries remain across 1,173 quests.

### Fixed
- Show path now scrolls to and highlights the Path section.
- Moonlit kind moons drew a full moon regardless of progress.
- Every orchestrion roll carried reward id 0 and seventeen rolls were lost to key collisions.
- Placeholder ClassJob rows 44 and 45 appeared as "Job 44" and "Job 45" in the character page.
- The Moonlit State column could not be reordered.
- Forward tab switches (Journal to Moonlit or Characters) were dropped.
- The tutorial card could fall behind the main window; Esc is now scoped to the card.
- Display sliders in Settings desynced from the filter panel and let NaN through.
- Settings changed just before closing the window could go unsaved.

## [0.1.0] - 2026-09-27

### Added
- First release: quest catalog by journal type with completion counts, completed and available-now filters with a per-requirement breakdown, Moonlit unique-rewards tab with obtained state and confidence badges, per-character snapshots with an account-wide view, prerequisite path, pins, map flag, journal open and chat links, characters page, settings and help windows.

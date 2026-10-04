# 1.22 "Welcome home": what's new, updates, the moon icon and Umbra

This spec designs every visible row of `docs/feature-plan-v8.md`:

| Row | Surface |
|---|---|
| **W1** | The What's new popup, after an update has installed |
| **W2** | One illustration per release, restyled by each theme |
| **W3** | Settings › About › What's new: every release, newest first |
| **W4** | The What's new card leaves the main window |
| **U1** | "Tsukimichi 1.23.0 is ready": the status-bar note, the dot on the moon icon, Update |
| **H1** | The moon icon: rest, hover and quick card, menu, lock, hide, dots |
| **H2** | Theme particles and the hover |
| **M1** | "◐ 12 Ready" in the server info bar, in the game and in Umbra (the plan's "◑", mirrored to the moons' light) |
| **M3** | Keeping clear of Umbra's toolbar; the Umbra rows in Settings › About |
| **A1, A2, A3** | Tsukimichi for Umbra: the widget and its popup, the small widgets, the missing state |

M2 (the IPC) has no surface of its own; where a surface depends on it, this spec says so.

**The principles** carry over from plan v7 and add three for this release:
- **Nothing interrupts.** The popup waits for a quiet moment, the update note never pops up, and nothing prints in chat unless the player asks.
- **One painting per release.** Each theme restyles it; nobody keeps six paintings in step (decision 1).
- **Umbra looks like Umbra.** Inside Umbra, everything is drawn with Umbra's own controls and colour profile. Only the moon is Tsukimichi's.
- As before: static layout, show what is left, no glyph without a meaning, Reduce motion and all three Decoration levels everywhere, English only.

## Files

Everything is under `docs/design/v8/`.

| File | What it is |
|---|---|
| `spec-1.22.md` | This spec |
| `mock-1.22.html` | The 1.22 mock: `#wn22a`, `#wn22b`, `#wn22s`, `#art22`, `#optb22`, `#about22`, `#upd22`, `#icon22`, `#fx22`, `#dtr22`, `#umb22`, `#clear22`. Built from the v7 mock sources (read-only) plus the 1.22 layer. |
| `mock-src/` | `v722.js` and `v722.css` (the 1.22 layer), `build22.py` (`py -3 build22.py` builds the mock), `render22.py` (`py -3 render22.py` renders the PNGs with headless Chrome), and two debugging aids, `console22.py` and `peek22.py` |
| `art/src/` | `artlib.py` (a small numpy and Pillow painting kit), `paint_release.py` (paints the four release illustrations), `grade_release.py` (the per-theme recipe: grade and motif layer) |
| `art/welcome-base.png`, `art/whatnext-base.png`, `art/evercold-base.png`, `art/answers-base.png` | The four paintings of the first popup a player on 1.18.0 sees (1.22.0, 1.21.0, 1.20.0, 1.19.0), 1120 × 440 (the 2x tier of the 560 × 220 art band) |
| `art/themed/` | Each painting restyled for the six themes, plus Medallion's Quiet grade without the motif layer (`-quiet`), which the Quiet popup uses |
| `whatsnew-evercold-1.22.png` | W1/W2: the popup on page 3 of 4, 1.20.0 Before Evercold, in all six themes (a player updating from 1.18.0) |
| `whatsnew-answers-1.22.png` | W1/W2: the popup on page 4 of 4, 1.19.0 Right answers, in all six themes |
| `whatsnew-states-1.22.png` | W1: page 1 of 4 (1.22.0 Welcome home) at Full, Quiet, Plain, one release (no pager), Text size 150 % on a page that fits and on one that scrolls, and the size and timing table |
| `release-art-1.22.png` | W2: the four paintings with their six restyles each, and the grade and motif table |
| `release-art-option-b-1.22.png` | Decision 1: 1.20.0 in Option A and Option B side by side for all six themes, Option B's painting, and the cost table |
| `art/optionb/` | Option B: the painting (`evercold-b-base.png`), its region masks (`src-masks.npz`) and the six treatments (`evercold-b-<theme>.png`); made by `art/src/paint_option_b.py` and `option_b_themes.py` |
| `about-history-1.22.png` | W3, U1, M3: Settings › About with Updates, What's new (backfilled) and Umbra, and the popup opened from the list |
| `update-ready-1.22.png` | U1: the status-bar note at each level, its hover, the dot on the icon with the quick card, Dalamud's installer, and the flow |
| `moon-icon-1.22.png` | H1: rest, hover with the quick card, the right-click menu, locked, first run, hide with Undo, the dots, Needs you, the three levels, and the behaviour table |
| `icon-particles-1.22.png` | H2: four-frame strips per theme, the hover in four frames, Reduce motion and Quiet, every icon on a daylight sky, and the timings |
| `server-info-bar-1.22.png` | M1: the entry and its tooltip in the game's bar, the moon in each theme, and in Umbra's toolbar |
| `umbra-widgets-1.22.png` | A1/A2/A3: the widgets and popup on Umbra's default profile and on YoRHa Light, and the missing state |
| `umbra-clearance-1.22.png` | M3: the icon, the Todo overlay and Needs you under Umbra's top bar in 1.21, and clear of it in 1.22 |

**Sources.** The game scenes behind the UI are the official screenshots already in `docs/plan-site/mock/` (`scene-night.jpg`, `scene-day.jpg`; © Square Enix, as the plan's art rule allows). The paintings are original, painted in code by `paint_release.py`; nothing is traced. Umbra's look comes from its source at `una-xiv/umbra` (main, read 4 October 2026):
- `Umbra/src/Umbra.Colors.cs`: the default colours and the built-in profiles (Umbra, Metal, Clear Blue, YoRHa Dark, YoRHa Light), decoded from their stored data;
- `Umbra/udt/umbra/widgets/_popup.xml` and `_popup_menu.xml`: the popup (radius 7, a top-anchored popup has no top border and round bottom corners, a vertical gradient) and its groups and buttons (a 12 px muted header with a rule; buttons 4 × 8 padding, 13 px text, 11 px alt text, 22 px icon);
- `_standard.xml` and `toolbar.xml`: the 32 px toolbar and its decorated widgets (radius 5, a 1 px stroke).

---

## Colour language

There are **no new tokens**. 1.22 uses what 1.14 to 1.21 defined and gated.

| Meaning | Token | Where in 1.22 |
|---|---|---|
| Act now | gold (`--moon`) | nothing new. The popup's Close and the menus are neutral; the moon icon's rim is the kit's resting metal, never the act-now gilt. |
| News, not a call to act | Cool (Tide `#6F8FD0`) | the update note's dot and tint, and the update dot on the moon icon |
| Needs you | copper `--attn` (`#D08654`) | the needs-you dot on the moon icon (1.18 A5) |
| Moonlight | `#E2E8F4` | the moon icon's hover glow. It is the cool in-medal glow of the v7 Completed moon, so Ready and the primary pill keep the only warm halos. |
| Text, Secondary | `--silver`, `--mist` | everything else |
| The theme's moon | the theme's accent | the "◐" in the server info bar (nearest UIColor row) |

**No colour carries meaning alone.** Each dot also has its words: in the quick card, the status bar and the menu.

**Umbra surfaces take Umbra's colours,** from the player's Umbra colour profile. Tsukimichi's palette never paints inside Umbra.

---

## W1. The What's new popup (`whatsnew-evercold-1.22.png`, `whatsnew-answers-1.22.png`, `whatsnew-states-1.22.png`)

### When it shows

- **Once per update, after it has installed.** It never shows on a first install. The check is the existing `Configuration.LastSeenVersion` and `Core.Ui.WhatsNew.Decide`, now driving the popup instead of the card.
- **At the first quiet moment after login:** not in combat, a duty, a cutscene, Group Pose or a loading screen, and at least 10 s after the character is in the world.
- It is **not modal**: the game keeps its input. It is centred on the main window if that is open, otherwise on the screen.
- **Seen** is recorded when the popup closes, by Close, × or Esc. If the game closes first, it shows again next time.

### Several releases at once

If the player skipped releases, each release is a **page**, newest first. A player on 1.18.0 who updates to 1.22.0 gets four pages: 1.22.0, 1.21.0, 1.20.0 and 1.19.0. The footer reads **‹ 1 of 4 ›**. The header says **"You were on 1.18.0"** only in that case. A player on 1.21 gets one page and no pager.

**The popup keeps the height of its tallest page.** Page 3 (three points) leaves space under its notes, so the footer and ‹ › never move while you page. This is in both six-theme renders.

### Anatomy (logical px at UI scale 1.0; the mock draws at 0.8)

| Part | Full | Quiet | Plain |
|---|---|---|---|
| Window | 560 wide. The kit's frame (Brass, Silver, Lead came, Astrolabe or Kirikane) with its corner marks. Shadow 0 22 48 at .55, straight down. | A tonal card (`--raised`), 1 px Line, radius 8 | Flat `--night`, 1 px Line, no radius |
| Header, 48 | the theme's moon icon (28, no particles), **WHAT'S NEW** in the Eyebrow role (Trump Gothic, `OrnamentLight`), "You were on 1.18.0" in **Secondary** on the right (Tertiary is never text on a Full card: 1.21's rule), × (26 round) | the moon icon (Quiet face), "What's new" semibold, Secondary | a 26 px band (`#1A1F2C`), no icon |
| Art, 536 × 220 | the release painting, graded **with** the motif layer, inset 12, radius 4, the kit's keyline | graded, **without** the motif layer | **none**: Plain loads no art |
| Title | the release name in the Title face (Jupiter 23), then "Tsukimichi 1.20.0 · 4 October 2026" in Secondary | the same | semibold body × 1.15 |
| Notes | 3 to 5 points. Each is a semibold lead and one plain sentence, 13 px, with a 5 px dot in kit ink, centred on the first line at any Text size. The block is the tallest page's height plus 14 px of room. | the dot in Secondary | a 4 px square in Text |
| Footer, 56 | **All releases** (opens Settings › About › What's new), **‹ 1 of 4 ›** (24 px round, disabled at the ends), **Close** (a neutral pill, not gold) | the same, flat | a 30 px band, square buttons |

**Theme** sets the palette, the kit and the moon icon (Ishgard Glass is a light popup on Ishgard Snow, `whatsnew-*.png` top right). Classic uses the Brass kit, as theme-system §3.1 sets, so Classic and Medallion differ in the art's grade and the header moon.

### The words

The notes come from a new curated `Data/whats_new.json`, one entry per release: version, date, name, art file and moon place (W2), and 3 to 5 points. They are written for players:
- no class names, settings paths only when the player needs them, no "fixed" lists;
- a semibold lead of two to five words, then one sentence;
- the technical `CHANGELOG.md` is unchanged and separate.

The example notes in the renders:

**1.22.0 · Welcome home**
- **What's new, in pictures.** After each update, a short note like this one shows what changed. Past notes are in Settings › About.
- **Know when an update is ready.** A quiet note in the status bar and a dot on the moon icon tell you. Update opens Dalamud's installer.
- **A moon on your screen.** A small moon opens Tsukimichi and shows what's next when you hover it. Move it, lock it or hide it.
- **At home in Umbra.** Tsukimichi keeps clear of Umbra's toolbar, and an Umbra add-on puts Tonight in your Umbra bar.

**1.21.0 · What next, for every character**
- **Up next.** Tonight starts with one suggestion for the character you're on, and says why.
- **Go to the current step.** Travel aims at the step you're on, not only at the quest giver.
- **All your characters.** One board shows each character's story, goals and what's Ready.
- **Loose ends.** Storylines you started and never finished, finales first.

**1.20.0 · Before Evercold**
- **Fewer spoilers.** Past where you are in the story, Tsukimichi now hides the names of places, duties, rewards and people too. Hover a hidden name to see why.
- **Ready for Evercold.** A card in Tonight lists what each character should finish before Patch 8.0: the story, journal room, job quests, roulettes and flying. Tick lines off as you go.
- **More faces, if you want them.** An optional download adds about 1,850 quest-giver portraits. Nothing downloads until you choose it in Settings › Look.

**1.19.0 · Right answers**
- **The game has the last word.** When the game offers you a quest, Tsukimichi shows it as Ready, even one it couldn't check before.
- **Know how you'll clear it.** Quests with a duty say whether NPCs can come along or you need a group, and which job gets the most from the EXP.
- **Room in your journal.** See how full your journal is, and what you can finish or safely drop to make room.
- **Events ending soon.** Seasonal events warn you three days before they end, and their quests come first.
- **Find by unlock.** Search for "Kugane" or "flying Thavnair" to see the quests that open it, with a route there.

### Motion

| Moment | Full and Quiet | Plain, and Reduce motion |
|---|---|---|
| Open | `PopupFade` with Rise: 0.16 s, 4 px | instant |
| Page change | the art and the text cross-fade in place over `Select` (0.15 s). Nothing slides; the frame never moves. | instant |
| Close | `Leave` (0.12 s) | instant |

### Text size, UI scale, high contrast

- **UI scale** scales everything, the art included.
- **Text size** reflows the notes inside the fixed width. The notes block takes the tallest page's height at that size, capped so the whole popup stays within 80 % of the screen. Past the cap that page's notes scroll (ImGui's thin scrollbar) and the footer stays. `whatsnew-states-1.22.png` shows 150 %: page 3 just fits the capped block, page 4 scrolls in it, and the footer is in the same place on both.
- **High contrast** caps at Quiet as always: the art sits under a 1 px `VeilLine` keyline, and every text sits on the solid card, never on the art.

### W4. The card leaves

The "What's new" card at the top of the detail column (`WhatsNewCard.cs`) is removed with its notice. The popup and Settings › About replace it.

**One line moves.** 1.21 (P5) put the once-per-patch "New chapters" line into that card. It moves into Tonight's existing lines block, as a line that shows once per patch and per character and closes with ×. No new notice or float is added.

---

## W2. Art for each release (`release-art-1.22.png`)

### One painting, restyled (decision 1)

Each release has **one painting**, 1120 × 440. When the popup opens, the plugin restyles it once, off the main thread, by a pure recipe in Core (`ReleaseArt.Compose`):

1. **Grade:** lift, gamma, gain, saturation and a split tone that pulls shadows and highlights toward the theme's palette.
2. **Motif layer:** the kit's own sprites, composited over the sky (Full only).
3. **Frame:** drawn by the popup from the kit, not baked in.

The recipe and its numbers are in `art/src/grade_release.py`, which made every themed image in the renders, so the renders are what the plugin will draw.

| Theme | Grade | Motif layer (one sprite set per kit, shared by every release) |
|---|---|---|
| Medallion | none: the reference | none; the brass frame and its corner marks carry it |
| Classic | lift .01, gamma 1.04, saturation .82 | none |
| Ishgard Glass | lift .07–.10, gamma .90, saturation .80, shadows toward `#2C3A64`, highlights toward `#EEF3FF` (a milky daylight print, for the light palette) | rime feathers from the two upper corners and the lower left, at .42 |
| Aether Crystal | gain .95/1.01/1.04, saturation .95, shadows `#0E2A40`, highlights `#BFF0FF` | up to three small shards in the sky's corners, lit on their upper-left facets, none near the moon |
| Astrologian's Orrery | gain 1.04/.98/.97, shadows `#2B1F3A` (Dawn's plum), highlights `#F5C47C` | whole hairline orbits at 28° round the release's moon, a graduated ring, and one bead (the exception below) |
| Sumi to Kinpaku | gamma 1.06, saturation .40, shadows `#16100F`, highlights `#F3E9DB` | sunago: gold dust and a few cut leaf pieces in the top quarter of the upper corners, and washi fibre at 4 % |

**Motifs keep to the sky's corners and never cross the subject** (the road, the pass, the house, the moon), so a theme can't hide what the painting is about:
- **Aether Crystal's** shards are skipped within 5 moon radii of the moon, so no crystal ever sits by a crescent (theme-system §2).
- **Sumi's** sunago is mostly fine dust (≤ 1 px) thinning out across the top quarter of each upper corner, with 7 cut leaf pieces per corner, two of them catching the light.
- **The Orrery is the one exception**, because an orrery is the sky's instrument: its motif centres on the release's moon, whose place is in `whats_new.json`. Its rules: whole ellipses only (no broken arcs), every point inside the art band with an 8 px margin (an orbit that doesn't fit shrinks, and is dropped if it still doesn't), never nearer the disc than 1.5 r, so no orbit crosses or hides behind the moon, and its 12 notches sit on a graduated hairline ring at 1.6 r, never as free radial ticks (which read as sun rays). Everything is at .20.

### Cost

- **Texture:** one 1120 × 440 texture (1.9 MB) while the popup is open, released on close. This is inside the 12 MB texture budget, and Plain loads nothing.
- **Package:** one base image per release (about 180 KB as JPEG, or about 600 KB as PNG) plus six small motif sheets.
- **CPU:** decoding and grading 0.5 M pixels takes a few milliseconds on a worker. Until it is ready the art band shows the theme's flat sky colour, then cross-fades in over `Select`.

### Art direction

Every painting is **a night or twilight landscape with one natural light, plus at most one warm practical light** (a lantern, a lit door or window), wide and calm. Its subject sits right of centre. The title is printed under the art, never over it.

**1.22.0 Welcome home: a door left open on a lit hall** (`art/welcome-base.png`). The first page anyone sees in 1.22.
- **Two lights.** The near-full moon stands high on the left, in front of the viewer, so it backlights the house: the facade is in shadow, the roof's left slope and the chimney top carry a cool rim, and the house's moon shadow falls toward the viewer and right.
- **The hall's warm light** is seen through the open door: brighter on the floor, a lamp glow on the far wall, the jamb in depth on the left and the door leaf swung in on the right. It spills down the path as a pool that widens toward the viewer and fades with distance; the two windows throw faint pools of their own.
- **The path's stones** are warm near the door and cool and dim further out. The two shrubs by the door are warm only on the side that faces it. Smoke rises from the chimney, faintly moonlit, drifting right.

**1.21.0 What next, for every character: a lantern at a crossroads** (`art/whatnext-base.png`).
- **Late twilight.** The sun has set off to the right: a low peach afterglow band (about 8 % of the sky's height) sits on that side and fades to the left, and the young crescent is lit on its lower-right limb, toward it.
- **The warm practical light:** a lantern on a post at the crossroads. Its pool is a smooth radial falloff on the ground under the lamp (the post's foot, nudged toward the lantern side), 4:1 wide because the ground is seen at a low angle, its far half compressed further. There is no mask edge anywhere in it.
- **Shadows from the lantern:** the waystone's shadow starts contact-dark at its base and runs away from the lamp, left and a little toward the viewer, across the pale path into the grass, fading with distance. The post's foot throws its own thin shadow to the left. The waystone is warm on its lantern side.
- **Four paths** leave the crossroads. Three run toward distant lights (a hamlet, a tower on a hill whose two lit windows sit inside its silhouette, a farm): every character's next step.

**1.20.0 Before Evercold: cold, snow, a coming dawn** (`art/evercold-base.png`).
- **The light** is the sun, still under the horizon behind a low pass. The dawn glow is warm only near it; the rest of the sky stays cold navy.
- **The waning crescent** stands high on the east side. It is lit on its lower-left limb, the side that faces the sun, with a true elliptical terminator, so its horns are diametrically opposite. The rest of the disc carries an earthshine barely above the sky.
- **The far ridges** are backlit: their luminance is capped at 0.9× the sky's at their ridgeline, so they are always darker than the sky behind them. A thin warm rim runs along the ridgeline, strongest by the pass and fading away from it. Only the sun-facing slopes near the pass take a lilac lift; the rest stay in blue shade.
- **The snowfield** reflects the sky: a forward-scatter sheen leads toward the sun, and wind ripples show only up close.
- **Shadows:** the spruces and the near drift are lit from behind, so their shadows fall toward the viewer, and the drift's face is in blue shade under a thin warm lip.
- **The hamlet** has five warm windows and a thread of smoke. It is the only warm light on the ground: home, before the cold.
- **Falling snow:** a few flakes, sharp far away and soft up close. None crosses the moon, and no small, sharp flake sits against the sky or the ridges, where it would read as a star.

**1.19.0 Right answers: a clear moonlit road** (`art/answers-base.png`).
- **The light** is one near-full moon above the road's far end, under a clear sky (no cloud).
- **The moon** has soft, neutral-grey seas joined in their real chains (Procellarum and Imbrium on the left, Serenitatis to Fecunditatis down the right) and no holes; it is not cheese. A thin unlit sliver, 0.09 r at its widest, runs horn to horn on its upper left, because the sun is below the horizon on the lower right. The stars are few: a near-full moon washes out the faint ones.
- **The road** runs from the near left to a fork. The branch that runs on toward the moon brightens with distance (forward scatter on worn stone). The side branch turns away into shade.
- **The signpost** at the fork is rim-lit on its top and moon-facing edges, and its shadow falls toward the viewer and left, away from the moon.
- **The rest:** a lone tree on the rise is rim-lit the same way and throws a faint, foreshortened shadow toward the viewer and right. Mist lies low in the far valley, and tall grass frames the near corners, lit on the edges that face the moon (up and right on the left, up and left on the right).

### Option B, for the owner's decision 1 (`release-art-option-b-1.22.png`)

The owner asked for "beautiful, theme specific custom art". Option A (above) gives one painting per release and lets each theme restyle it; its themes differ in colour and in a few motifs, and Medallion and Classic look almost the same. So the owner can compare, **1.20.0 Before Evercold is also made as Option B**: a richer painting, rendered in a distinct art treatment per theme.

**The painting** (`art/optionb/evercold-b-base.png`, by `art/src/paint_option_b.py`): dawn over Coerthas.
- **The scene:** Ishgard stands on its bluff, backlit by the sun still under the horizon behind it. Two Ishgard cues are drawn in our own silhouette: the Vault's tall paired spires and two of the Pillars joined by an arched bridge. Broken altocumulus is lit on its undersides, warmest near the sun, and mist lies in the valley. On the near snow ridge an adventurer with a lantern looks toward the city, and her chocobo faces her.
- **The light:** one natural light, plus the lantern as the one warm practical light.
  - The city and the far range are backlit, capped darker than the sky behind them, with a thin warm rim.
  - The ridge crest takes the dawn, and its face toward the viewer is in blue shade.
  - The figures carry a 1 px warm rim on their sun-facing (right) edges, as the spires do. Their shadows are short and soft (the sun is below the horizon): contact-dark at the feet, then fading toward the viewer and left.
  - The lantern warms the chocobo's chest and the adventurer's arm, and lays a small foreshortened pool on the snow.
  - The crescent is lit toward the sun.
- **The finish:** a Kuwahara pass flattens everything but the silhouettes into painted shapes. The city and the figures keep their anti-aliased edges, and their rims and the lantern are drawn after the pass. A light brush and canvas texture goes over it all.
- **Sources:** it is original work, painted in code; no official art or screenshot is used in it. Ishgard is our own simplified silhouette of the Holy See's spires, not a trace. (The game scenes behind the mock's UI are the official screenshots listed in Files.)

**The six treatments** (`art/src/option_b_themes.py`, from the painting and its region masks):

| Theme | Treatment |
|---|---|
| Medallion | An oil painting: heavier impasto (the silhouettes kept sharp), a warm varnish, faint craquelure only in the thick, light paint and never on the dark silhouettes, and a slim gilt slip lit from the upper left and shaded on the lower right, inside the popup's own brass frame. No oval, so the moon and the whole scene stay in view and there is no double frame. |
| Classic | The painting as painted |
| Ishgard Glass | A stained-glass window whose lead follows the drawing. Large sky pieces (cells only in the sky, sparingly), clouds cut along their own edges, the far hills and the bluff as a few large pieces, and the snow and the ridge as long strips cut parallel to their contours, never paving. The figures and spires are painted in grisaille on a few pieces. The crescent is one white piece, brighter on its lower left, with the earthshine as a separate deep-blue piece; the lantern is one amber piece. The bright pieces bloom past the lead, and two saddle bars run above the spires and below the figures. |
| Aether Crystal | Cut moonstone over the sky and the far range only: facets shaded consistently, as on three broad domed gems lit from the upper left, with bright edges on the lit side and a faint blue adularescent sheen across the upper sky. The ground, the city and the figures stay clear. |
| Astrologian's Orrery | An engraved plate: a silver ground and a lapis enamel sky. Dark line engraving follows each contour: parallel to the ridge, to the plain in perspective and to the far ridgeline, and down the cliff faces with their strata. The backlit city is densely cross-hatched, the shadows are dense hatching, the figures are solid, and the clouds are cut in silver line. Brass is used only as inlay: the crescent, the graduated limb and the rete. |
| Sumi to Kinpaku | Sumi-e on toned washi: ink washes by depth, bare-paper snow, and the ridge as one tapered dry-brush stroke (wide and dark where the brush lands on the left, thinning and broken by dry gaps toward the right). Solid ink figures, a gold-leaf crescent, two genji-gumo gold bands with scalloped, stepped ends and gold-dust edges, kirigane only inside the bands and in the top corners, one vermilion touch for the lantern, and a vermilion seal carved with a crescent. |

No treatment adds a light of its own. Quiet would show the Classic painting graded to the palette; Plain shows no art in either option.

**Cost and trade-off**

| | Option A: one painting, restyled | Option B: one painting, six treatments |
|---|---|---|
| Art per release | 1 painting (about a day with supervision). The six restyles are automatic. | 1 richer painting (2 to 3 days) plus its region masks. The treatments are code, but each release needs all six checked, because a treatment can break a new composition (a gold band across a figure, a came line through a face). |
| Ships | 1 base image per release (about 180 KB) and six motif sheets, once | 1 base image and 1 mask file per release (about 500 KB), or six pre-rendered images (about 2.4 MB per release) |
| Texture | one 1120 × 440 texture while the popup is open (1.9 MB) | the same 1.9 MB. Composing is slower (about 0.3 s for glass and facets on a worker), so the theme's flat sky shows for a moment first. Pre-rendering the six avoids that. |
| How themes differ | in colour and a few corner motifs | in craft: oil, glass, crystal, engraving, ink and gold |
| Risk | low: a grade can't break a picture | higher: six pictures per release to approve, and the subject must stay legible at 560 × 220 |

**My recommendation stays Option A** for its cost and safety, but Option B is the one that answers "theme specific custom art" fully. The owner decides (decision 1). A middle way is possible: Option B for the major releases, and Option A for the small ones.

### Backfill (decision 2)

Settings › About lists 1.14.0 to 1.22.0. 1.19.0 to 1.22.0 are painted and supervised here, so every page of the first popup a player can get (from 1.18.0 or later) has its art. The five older releases appear only in the history list; their briefs follow the release names:

| Release | Painting |
|---|---|
| 1.14.0 The polish you asked for | a lantern-lit workshop window over a harbour |
| 1.15.0 Faces and icons | a row of lit windows, figures in each |
| 1.16.0 Themes | one moon over four horizons |
| 1.17.0 Mix and match | stepping stones of different stone |
| 1.18.0 Runs you can trust | a ferry on a guide rope at night |

Each goes through the same realism supervision. Until it is painted, its row shows the shipped category banner of the nearest motif (`about-history-1.22.png`, the 1.18.0 and 1.17.0 rows).

---

## W3. Settings › About (`about-history-1.22.png`)

Settings › About becomes four sections, each with the Section heading and the 1.14 switch rows:

1. **About:** "Tsukimichi 1.22.0 · game data 2026.09.15 · Dalamud API 15", with **Copy diagnostics** (unchanged).
2. **Updates (U1):**
   - a status line, either "Up to date · Dalamud last looked 6 min ago" or "Tsukimichi 1.23.0 is ready · Dalamud has it", with **What's in it** and **Update**;
   - **Tell me when a new version is ready**, on by default. Its caption: "Asks Dalamud, which already checks your plugin repositories every few minutes. Tsukimichi itself never goes online for this.";
   - **Also say it in chat**, off by default (decision 6);
   - **Show what's new after an update**, on by default.
3. **What's new (W3):**
   - one 46 px row per release, newest first: a 76 × 30 thumbnail in the current theme, the release name, the version in Secondary, the date in one form ("5 Oct"), and ›. The running release has an **Installed** chip;
   - six rows show, then "3 earlier releases, back to 1.14.0 ›";
   - a row opens the popup on that release, with ‹ › walking the whole history ("3 of 9"). Opened this way, the popup has no "You were on" caption.
4. **Umbra (M3):** see M3 below.

The privacy page's line from 1.20, "Tsukimichi never checks for updates by itself", becomes "Tsukimichi never goes online to check for updates. It asks Dalamud, which already does."

---

## U1. A new version is ready (`update-ready-1.22.png`)

### The check

- `IDalamudPluginInterface.CheckForUpdateAsync()` runs at login and every 3 hours while the switch is on. It reads the repository data Dalamud already refreshes (about every ten minutes), so Tsukimichi makes **no request of its own**, and the no-network test stays as it is.
- When the check returns a newer version, the note and the dot appear. Nothing pops up, and nothing prints in chat unless "Also say it in chat" is on.

### The status-bar note

On the right of the main window's status bar: a 7 px Tide dot, **"Tsukimichi 1.23.0 is ready"**, **Update** (a quiet pill) and × (Later).
- **Hover** shows the new version's plain notes, when Dalamud has them, with "Dalamud installs it; Tsukimichi never downloads itself."
- **Update** calls `OpenPluginInstallerTo(PluginInstallerOpenKind.UpdateablePlugins, "Tsukimichi")`. Dalamud's installer opens on **Can be updated**, searched for Tsukimichi, and Dalamud does the install. The render sketches Dalamud's own installer, which Tsukimichi does not draw.
- **Later (×)** hides the note and the dot until a version newer than this one appears. Settings › About still shows it with Update. Later needs no Undo: nothing is lost.
- **Quiet** drops the tint: a hairline pill on the flat bar. **Plain** is the ledger: no pill, a square Update, and the dot kept, so the meaning is never colour alone.

### The plain notes in Dalamud

`tools/make_pluginmaster.py` writes each release's `whats_new.json` points into the manifest's changelog. So the hover, and Dalamud's own installer, show the same plain notes as the popup. The technical changelog stays on GitHub.

### The dot on the moon icon

A Tide dot (H1). The quick card's last line says it in words: "Tsukimichi 1.23.0 is ready · Update".

---

## H1. The moon icon (`moon-icon-1.22.png`)

### The face

A round 40 px icon (Small 32, Large 48; times UI scale; Text size does not change it), drawn by the theme:
- **Medallion:** a brass rim over a lapis well;
- **Classic:** the 1.11 flat disc with a hairline (no metal, as Classic's glyphs);
- **Ishgard Glass:** a lead came rim and two glass tones split by a came line;
- **Aether Crystal:** a silver rim with four facet chips and faint facets in the well;
- **Orrery:** an astrolabe rim with an inner line, and a three-star constellation;
- **Sumi to Kinpaku:** a lacquer rim with a kirikane line and a gofun hairline, and a gloss band on the well.

In every theme the crescent is **lit on its upper-left limb**, the one light of every Tsukimichi surface, with a faint earthshine on the dark part.
- **The rim is the kit's resting metal**, never the act-now gilt: the icon asks for nothing.
- **A crescent cannot read as cheese**: no seas or craters at this size.
- **The shadow falls straight down:** 0 2 2 at .50 at rest.

### States

| State | Look |
|---|---|
| At rest | the face, its shadow, its particles (H2) |
| Hover | rises 2 px over `HoverIn` (0.12 s); a cool glow `#E2E8F4` (.22 at its core, out to 1.4 radii); the shadow lengthens and softens (0 4 3.6 at .42), as an object lifted toward the light. The quick card opens after 0.25 s. |
| Pressed or dragging | the hover look, held; the quick card closes |
| Locked | the same at rest. A drag does nothing and shows "Locked in place · right-click to unlock". **There is no padlock on the icon**, because a closed padlock means Blocked. |
| Update ready | an 8 px Tide dot at the upper right, with a 1.5 px Night ring |
| Needs you | the same dot in copper; it wins over Update ready |
| First run | "Right-click for options", once after install or update (decision 5), for 8 s or until a click |

### The quick card

A tooltip, 300 px wide, that never takes focus. It opens on the side with room, never covers the icon, and closes when the pointer leaves:
- **Tonight**, with the character and job on the right;
- **Up next** (1.21 P1's pick: the moon, the name, then the step and the place on their own lines);
- **"12 quests are Ready on WHM"**;
- **"Journal 27/30 · 3 slots left"**;
- **events ending soon** ("The Rising ends in 2 days");
- then, when present, **"Tsukimichi 1.23.0 is ready · Update"** or the 1.18 **Needs you** line with **Show**;
- a hint: "Click to open · right-click for options", or "Locked in place · right-click to unlock".

At Full it is the Brass card; at Quiet a tonal card; at Plain a band and lines.

### The menu (right-click)

**Lock in place** (or **Unlock**), **Hide icon** (with "/tsuki icon" as its hint), a separator, **Tonight**, **Settings**. Words only: a padlock would mean Blocked, and a cog adds nothing a word doesn't say. **Hide** shows the 8 s Undo toast: "Moon icon hidden · Undo · /tsuki icon shows it again".

### Placement

- **Drag** with a 4 px dead zone, so a click is never a drag.
- **Edges:** the icon stays 8 px inside the screen and clear of Umbra's toolbar (M3).
- **Its place is saved per screen size**, so a new resolution never strands it off screen.
- **Hide in cutscenes and Group Pose** is on by default; **hide in duties** is off (Settings › In game › Moon icon).

---

## H2. Theme particles and hover (`icon-particles-1.22.png`)

Full only, with Reduce motion off. Every effect keeps these limits:
- at most three particles at once;
- never above .55 alpha;
- never further than 1.4 icon radii;
- light comes from the upper left, as everywhere.

| Theme | Particles | Timing |
|---|---|---|
| Medallion | **Gold motes**, r 1 px `#FFE9BE` with a .12 halo of their own (as the v7 sky's warm stars), never a glow round the icon | 3 motes, each 3.6 s: fade in 0.6 s, rise about 3 px/s with a slight sway, fade out 1.2 s; staggered 1.2 s |
| Classic | **A few stars**, round, never a cross | 3 fixed stars breathing on 7, 9.5 and 12 s (the 1.14 twinkle curve), .27–.49. Nothing moves. |
| Ishgard Glass | **Frost glints** on the rim | every 6 s a 28° glint runs the lit upper-left quarter of the rim in 1.2 s, with one round sparkle at its head. Never a 4-point star. |
| Aether Crystal | **Shards** that catch the light | 2 shards on a 16 s orbit round the icon's foot: rx 1.25 R, ry 0.45 R, centred 0.6 R below the icon's centre, so the near arc runs just outside the bottom of the rim. A shard hides only where the icon really covers it, so **no shard is ever drawn over the face**, let alone the lit crescent (theme-system §2: no crystal over a crescent). Each turns every 3.2 s and flashes for about 0.4 s when its face meets the light; the flash is additive light, never a painted disc. |
| Astrologian's Orrery | **An orbiting dot** | one bead on a 12 s orbit (rx 1.3 R, ry 0.4 R, tilted 28°). Its far half passes behind the icon. The orbit hairline is at .14. |
| Sumi to Kinpaku | **Gold-leaf flecks** | 2 flecks, 4.5 s each, drifting down past the right side with a flutter; a fleck flashes when it tilts toward the light |

| Level or setting | The icon |
|---|---|
| Full | particles, hover rise 2 px and glow |
| Quiet | no particles; hover rises 1 px with a lighter glow (.7) |
| Plain | the flat glyph, no shadow and no glow; hover draws a 1.5 px Text ring |
| Reduce motion | no particles and no rise; the glow appears at once |
| High contrast | caps at Quiet |

**Cost:** each theme's particles are pure functions of time (`IconParticles.At(theme, t)`), drawn as a few circles and quads per frame, with no allocation. The clock runs only while the icon is visible.

---

## M1. In the server info bar (`server-info-bar-1.22.png`)

**The moon character.** The plan wrote "◑". That glyph is lit on the right; every Tsukimichi moon is lit from the upper left, so the renders use **"◐"** (U+25D0, lit on the left). If the game font lacks it, the entry keeps today's "☾" (open question 1).

**One entry, not two.** Today's Nearby entry ("☾ 3", the quests you can start in this zone; a click toggles Nearby) becomes this entry.
- **Settings › In game › Server info bar › "The entry counts":**
  - **Ready quests** (default): "◐ 12 Ready". A click opens Tsukimichi; a right-click opens Tonight.
  - **Quests in this zone:** "◐ 3 here". A click opens Nearby, as today.
  - Players who had the Nearby entry on keep Quests in this zone.
- **The tooltip** always gives both numbers.
- **On by default** when Umbra is installed and Tsukimichi for Umbra is not; otherwise as the player set it (off on a fresh install).
- **At zero** the entry hides. With "Show at zero" on (today's setting, kept) it reads "◐ Nothing Ready".

**The hover is text lines.** Dalamud gives an entry a text tooltip (`IDtrBarEntry.Tooltip`), not a window, and Umbra draws that tooltip in its own style. So it carries the quick card's content as lines:
- the first line, "Tsukimichi", in the gold the UIColor sheet offers;
- "Up next: …", then its step;
- "12 quests are Ready on WHM · 3 can start here";
- the journal and ending-soon lines;
- "Click: open Tsukimichi · Right-click: Tonight".

**The moon** is the only coloured character: an SeString foreground from the UIColor row nearest the theme's phase colour.

**In Umbra**, its Server Info Bar widget shows the entry decorated like its other widgets. Clicks pass through as in the game's bar.

---

## M3. Clear of Umbra (`umbra-clearance-1.22.png`, `about-history-1.22.png`)

### Keeping clear

With Umbra loaded (`InstalledPlugins` lists it as loaded), Tsukimichi reads where Umbra's toolbar is, **read-only**:
- its side (top or bottom);
- whether it is floating or auto-hidden;
- its height and Umbra's UI scale.

| Case | Rule |
|---|---|
| Top bar | the moon icon, the Todo overlay and Needs you keep their top edges at bar height + 8 |
| Bottom bar | their bottom edges at the bar's top − 8 |
| Floating or auto-hidden bar | no change: it does not hold an edge |
| Umbra's settings can't be read | assume a 32 px top bar while Umbra is loaded, and say so in Settings › About |
| The saved place | never rewritten: the clearance is applied on top, so turning Umbra off restores the player's place |

The clearance is applied when the bar appears, changes side or changes size, **never while the player is looking at a moving surface**.

### Settings › About › Umbra

- **"Umbra is running · its toolbar is at the top. The moon icon, the Todo overlay and Needs you keep clear of it."** When Umbra is not installed, the section is one line: "Umbra isn't installed. Tsukimichi works the same without it."
- **"Tsukimichi for Umbra · not added"** (or "added · 1.0.0"), with **How to add it ▾**, which opens three steps:
  1. turn on custom plugins in Umbra's Plugins settings (Umbra asks you to agree first);
  2. add `xenofei/Tsukimichi.Umbra`;
  3. add the widgets from Umbra's widget list.
- **Follow Umbra** is a palette in Settings › Themes › Palette, beside Follow Dalamud, off by default (decision 4). About links to it. It reads Umbra's colour profile from Umbra's saved settings, never writes them, and falls back to Night.

---

## A1, A2, A3. Tsukimichi for Umbra (`umbra-widgets-1.22.png`)

The add-on is drawn by Umbra, with Umbra's controls and the player's Umbra colour profile. The renders show **Umbra (built-in)**, the default, and **YoRHa Light (built-in)**, a light profile, with colours decoded from `Umbra.Colors.cs`.

### A1. The Tsukimichi widget

- **The bar:** Umbra's standard widget. Its icon is the current theme's moon, from an IPC image path (Medallion if Tsukimichi is too old to say); its text is "12 Ready". It is decorated or not, as the player chooses. A click opens the popup, as Umbra's own popup widgets do.
- **The popup** (Umbra's MenuPopup, top-anchored):
  - a header, "Tsukimichi · Kiri · WHM 100";
  - **Up next**, with **Go**;
  - **Ready tonight · 12**: the first three by Up next's order, then "9 more", which opens Tonight;
  - **Journal**: "27/30 · 3 slots left", with Make room;
  - **Ending soon**: "The Rising · 2 days";
  - a separator, then **Open Tsukimichi**, **Route** and **Settings**.
- **Go** opens Tsukimichi on Up next with its travel pill focused. **It never starts travel or a run by itself** (M2's rule: anything that starts a run needs a click inside Tsukimichi).
- A Ready row opens that quest in Tsukimichi.

### A2. The small widgets

| Widget | Shows |
|---|---|
| Up next | the quest's own icon (as the popup's Up next row) and its name. The book stays Journal's alone. |
| Journal | "Journal 27/30" |
| Story meter | two lines: the expansion over "91 to the latest story". No bar and no percentage: it says what is left. |
| Next reset | two lines: "Next reset" over "daily in 3 h 12 m" |

Each has Umbra's usual options: icon, text, colour, and a click action (open Tsukimichi, Tonight, the popup, or nothing).

### A3. Missing or too old

When Tsukimichi isn't running, or is older than 1.22:
- the bar reads "Tsukimichi" with no count;
- the popup gives **one reason per case**, in Umbra's own text colour (it is a statement, not a disabled button): "Tsukimichi isn't running. Turn it on in Dalamud's plugin installer, or install it there." or "This widget needs Tsukimichi 1.22 or later.", then **Open the plugin installer**.

### Spoilers

Names come through IPC **already shielded** (1.20 N6), so the add-on never receives a hidden name.

---

## Code map

| Piece | Where |
|---|---|
| The popup, its pages and its timing | a new `Ui/WhatsNewPopup.cs`, replacing `WhatsNewCard.cs`; the decision stays in `Core.Ui.WhatsNew` (now with the "quiet moment" gate: combat, duty, cutscene, Group Pose, loading); `LastSeenVersion` unchanged |
| Notes data | `Data/whats_new.json` (version, date, name, art, moon place, points), read by a pure `Core/WhatsNew/ReleaseNotes.cs` with tests (3–5 points, every release since 1.14.0 present) |
| Art composition | `Core/Ui/ReleaseArt.cs` (the grade and motif recipe, pure, unit-tested against the reference images in `docs/design/v8/art/themed/`); `Ui/ReleaseArtTexture.cs` (decode, compose on a worker, `ITextureProvider.CreateFromRaw`, release on close) |
| Settings › About | `ConfigWindow.Advanced.cs` (the About section): Updates, What's new list, Umbra |
| Update check | a new `Game/UpdateWatcher.cs` (`CheckForUpdateAsync` on login and a 3 h timer; `OpenPluginInstallerTo`); the status-bar note in `MainWindow.Frame.cs`; `tools/make_pluginmaster.py` writes the plain notes into the manifest changelog |
| Moon icon | a new `Ui/MoonIconWindow.cs` (a no-decoration ImGui window, drag, lock, place per resolution, the quick card as a tooltip); faces from the theme's kit tokens in a new `Ui/MoonIconFace.cs`; `Core/Ui/IconParticles.cs` (pure, per theme, tested for the limits above); `/tsuki icon` in `Commands/TsukimichiCommand.cs` |
| Server info bar | `Game/DtrEntry.cs` (the text, the count choice, the tooltip lines, right-click to Tonight); `Strings.Discovery.cs` |
| Umbra | a new `Game/UmbraProbe.cs` (loaded state from `InstalledPlugins`; the toolbar's side, height and auto-hide, and the colour profile, read-only from Umbra's saved settings); clearance applied in the moon icon, `TodoOverlay.cs` and the Needs you panel; the Follow Umbra palette beside Follow Dalamud |
| IPC (M2) | `docs/ipc.md`, `docs/TsukimichiIpc.cs`: the summary (shielded names), the theme's moon image path, and the "open at …" actions |

---

## Decisions (settled here, under the plan's rules)

1. **The popup waits for a quiet moment** and is not modal. It never covers a cutscene or a fight.
2. **Pages are newest first.** The popup keeps its tallest page's height, so ‹ › and the footer never move.
3. **Close is neutral, not gold.** Nothing in the popup asks the player to act.
4. **Plain shows no art** and loads none. Quiet keeps the graded art without the motif layer.
5. **Classic uses the Brass kit** (theme-system §3.1). Its icon is the 1.11 flat disc.
6. **Motifs stay in the sky's corners** and never cross a painting's subject.
7. **The update note is Tide, not gold**: news, not a call to act. Later needs no Undo, because nothing is lost; Settings › About keeps Update.
8. **The plain notes go into the manifest changelog**, so Dalamud's installer and the hover show the same words as the popup.
9. **The icon's rim is resting metal** and its hover glow is cool moonlight, so Ready and the primary pill keep the only warm halos round UI. Medallion's gold motes are particles with their own faint halos, as the v7 sky's warm stars are, not a glow on the icon.
10. **No padlock on a locked icon or in its menu**, because a closed padlock means Blocked. The lock shows in words on a drag and in the menu, which has no glyphs.
11. **Hide has Undo.** Lock and Unlock need none.
12. **Needs you wins the dot** over Update ready; the quick card names both.
13. **One server info bar entry.** It counts Ready by default; Quests in this zone stays as a choice, and existing users keep it.
14. **The entry's hover is text**, because Dalamud gives entries a text tooltip, not a window.
15. **In Umbra, Go opens Tsukimichi** on Up next; it never travels by itself.
16. **The Story meter widget has no bar** and no percentage.
17. **Follow Umbra lives with the palettes** in Settings › Themes, not in About.
18. **The 1.21 "New chapters" line moves into Tonight** when the card leaves.
19. **No new tokens.** Tide, copper and the cool moonlight glow are all existing tokens.
20. **The server info bar's moon is "◐"**, lit on the left like every Tsukimichi moon, not the plan's "◑".
21. **The Orrery's art motif is the one motif that centres on the subject**, under the rules in W2.
22. **1.21.0 and 1.22.0 are painted now**, so no player can open a popup page without its art.

## Open questions for the owner

1. **The moon in the server info bar.** The renders use "◐" (lit on the left) in place of the plan's "◑". The game font may draw neither; the shipped entry draws "☾". If "◐" is missing in game, should it fall back to "☾", or to one of the game's own bitmap icons?
2. **Decision 1, Option A or Option B.** `release-art-option-b-1.22.png` shows both for 1.20.0. A is cheaper and safe; B gives each theme its own craft. A middle way is B for major releases only.
3. **The backfill paintings.** The briefs for 1.14.0 to 1.18.0 are in W2. Do they suit you, or would you like other subjects for any of them?

## Not verified

- **Glyph coverage:** whether the game font draws "◐" (M1).
- **Umbra's saved settings:** where Umbra keeps its toolbar side, height, auto-hide and colour profile, and whether Tsukimichi can read them, are unverified. The rule for when it can't is in M3.
- **Fine detail in game:** the particles and the art grade are checked in the mock only, not in game at 1x and 4K.
- **Dalamud's installer:** the sketch in `update-ready-1.22.png` is not a capture.

## Approval record (realism supervisor)

- **Round 1: CHANGES.** 4 Major, 14 Minor and 14 Nit findings, no Blocker:
  - **Major:** "You were on" used Tertiary on Full cards (3.8:1); the 150 % render let page 1 set the height, so the footer would move; the Aether particles put a shard above the crescent (a banned motif) on a face-on orbit; and 1.22.0's painting, the first page anyone sees, was a stand-in, with an impossible "1 of 2 · You were on 1.18.0" example.
  - **Minor:** a snowflake inside the crescent's earthshine and star-like flakes in the sky; the far range brighter than the sky on the left (front-lit, not backlit); the gibbous sliver invisible; the Orrery motif crossing the subject, clipped and broken, with free radial ticks reading as sun rays; Sumi's flecks busy and outside the corners; the Aether flash painted as a dark disc; "◑" lit on the right; a black band under the game's server info bar; identical status notes at the three levels; a padlock and a sun-like gear in the menus; the Umbra missing state illegible and giving two reasons; the book meaning both Up next and Journal; page 2's last line crowding the footer.
  - **Nits:** crescent horns not opposite; polka-dot maria; tuft rims on the wrong side; too many stars under a near-full moon; the tree without a shadow; Sumi's earthshine lifted; the motes' warm halo against Decision 9; Classic labelled "Brass rim"; the Quiet header; the bullet at 150 %; mixed dates; a dangling "·"; wrapped chips; the missing bar not rendered.
- **After round 1**, every finding was fixed in the art, the mock and this spec:
  - "You were on" is Secondary.
  - The notes block is the tallest page's height plus 14 px, capped at 80 % of the screen with scrolling; the 150 % pair shows page 3 fitting and page 4 scrolling, with the footer in the same place.
  - The Aether orbit is tilted and its far half is hidden, and the flash is additive.
  - 1.22.0 and 1.21.0 are painted; the six-theme boards are pages 3 and 4 of 4, and the states board leads with 1.22.0, page 1 of 4.
  - Flakes avoid the moon and the sky; the far range is capped at 0.9× the sky; the sliver and the crescent use true terminators; the maria are joined grey chains.
  - The Orrery motif follows its exemption rules; Sumi's dust stays in the top quarter of the corners.
  - The entry uses "◐" with no band; the status note has Quiet and Plain forms; the menu has no glyphs and the Umbra Settings icon is a real cog.
  - The missing state is legible with one reason and renders its bar; Up next uses the quest icon; page 4 has room under its last line; and every Nit is addressed.
- **Round 2: CHANGES.** All 32 round-1 fixes held, and Welcome home was found sound. New findings:
  - **What next:** the lantern pool was cut by a hard horizontal edge (Major); the waystone's shadow was detached from its base (Minor); there was no afterglow band, the tower's lights sat outside its silhouette, and the post had no shadow (Nits).
  - **Aether particles:** the near orbit crossed the face, and a shard flashed beside the crescent's horn (Minor).
  - **Wording:** "one light" should read "one natural light, plus at most one warm practical light" (Nit).
  - **Option B:** the coordinator asked for it so the owner can decide decision 1 with a richer alternative in front of them.
- **After round 2:**
  - The pool is a smooth 4:1 radial falloff on the ground under the lamp. The waystone's shadow is contact-dark at the base and runs across the path, fading. The post has a thin shadow, the peach afterglow band is in, and the tower's windows sit inside it. All six restyles were rebuilt.
  - The Aether orbit now rings the icon's foot, and no shard is ever drawn over the face.
  - The art direction reads "one natural light, plus at most one warm practical light".
  - Option B is made for 1.20.0 (painting, masks and six treatments), with `release-art-option-b-1.22.png` and its section above.
- **Round 3: Option A APPROVED; Option B CHANGES.**
  - **Option A:** every round 2 fix held, with no Blocker, Major or Minor findings. **Option A is approved.**
  - **Option B, the painting:** the figures' shadows didn't touch their feet (Minor); the rims, the lantern bounce and the pool weren't visible (Minor); the paint filter smeared the silhouettes (Nit); and the city read as a generic castle (Nit).
  - **Medallion:** the oval cropped the moon and double-framed inside the popup's frame (Major); the craquelure was too strong and too even (Minor); and the moulding's light (Nit).
  - **Ishgard Glass:** the moon read as a dark perforated disc (Major); the window read as mosaic or cobblestone, not lead following the drawing (Major); a saddle bar crossed the tallest spire (Minor); and the lantern needed an amber piece (Minor).
  - **Aether Crystal:** it read as a low-poly filter (Minor).
  - **Orrery:** the brass ground turned the snow into desert (Major); and the hatching didn't follow the contours, the clouds weren't cut in line, and the shadows lacked dense hatching (Minor).
  - **Sumi to Kinpaku:** the gold bands looked like UI pills, flecks sat on the spires, the ridge stroke wasn't a brush stroke (Minors), and the seal had no mark (Nit).
- **After round 3**, Option B was reworked as the treatment table above describes:
  - the painting: contact shadows, a 1 px warm rim on the figures and spires, the lantern's bounce and pool, silhouettes kept out of the paint filter, the Vault's paired spires and the bridged Pillars, and the chocobo turned to face the lantern;
  - Medallion: a slim gilt slip in place of the oval, and craquelure at a third of the strength, only in the thick paint;
  - Ishgard Glass: lead that follows the drawing, a white glass crescent, an amber lantern piece, and the saddle bars moved clear of the subject;
  - Aether Crystal: facets in the sky and the far range only, with consistent dome shading and adularescence;
  - Orrery: a silver ground with contour engraving and brass only as inlay;
  - Sumi to Kinpaku: genji-gumo bands, kirigane only in the bands and the corners, a tapered dry-brush ridge, and a carved seal.

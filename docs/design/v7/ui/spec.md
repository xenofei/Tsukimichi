# Plan v7 UI: headings, filter drawer, stars, Completed moon, column headers, rail

This answers owner points 1, 2, 3, 5, 6 and 8 in `docs/research/plan-v7/owner-points.md`, using the two screenshots there. It builds on the Decoration v13 looks (`docs/design/flair-v13/spec.md`): Full is the Moon Road, Quiet is still water, and Plain is the ledger. Everything not named here is unchanged from 1.13.

**Revision 2** applies the supervisor's and the critic's reviews (`supervisor-review.md`, `critic.md`):
1. The Completed moon's three craters are drawn only in the 96 and 128 px tiers, and the young crater's halo is halved (§4).
2. The drawer's sideways shadow becomes an unoffset contact shadow (§2.2).
3. The Section role keeps 1.80x, tapers to 1.60x at 150 % Text size, and the hero quest title rises to Jupiter 23 so it still leads (§1).
4. At Full, the drawer loses its icon disc, its pills go neutral, the Advanced rule is OrnamentLight, set values are Text with a MoonHigh dot, and the Level track is Dusk (§2).
5. The near-star cross is 5 px at .4 with a fixed length; The Tower is a spire; A Realm Reborn's motif is The Chocobo, not a crystal (§3).
6. Column headers drop to 1.55x with +0.08 em, so they never outrank the group headers (§5).
7. The selected rail station keeps the plate, the gold icon and the bead. Its glow, border and hairline are cut, and a hovered plate gets a darker foot (§6).
8. The Milky Way becomes an optional setting, off by default (§3.4). The meteor peaks at .80 (§3.6).

**Files in this folder**

| File | What it is |
|---|---|
| `mock.html` | The flair-v13 mock with the v7 layer on top. The v13 CSS is carried over verbatim, and every v7 rule is scoped under `.mk.v7`. Views:<br>• `#full`, `#quiet`, `#plain` show the window.<br>• Add `/before` for 1.13, `/open` for the drawer, or `/open-adv` for the drawer with Advanced open.<br>• `#drawer`, `#rail`, `#stars` and `#ba` are the boards behind the renders.<br>• "Complete a quest" plays the meteor at Full.<br>The station plates, the Filters button and the Advanced header can be clicked. |
| `mock-src/` | The sources for `mock.html`: `v7.css`, `v7.js` and `shell.html`. `python build.py` rebuilds the mock from them and the v13 file. Edit these, not `mock.html`. |
| `before-after.png` | Full in 1.13 and v7, the whole window, then the quest-pane headings and the Journal header at 1:1, then the Quiet and Plain headings |
| `filter-drawer.png` | 1.13 as shipped, then Full, Quiet and Plain, each with Advanced collapsed and expanded, at 1:1, with the Full spec table |
| `rail-states.png` | The 1.13 rail and the v7 rail at each level at 1:1, every station state at 2x, and the motion table |
| `stars.png` | The 1.13 sky and the v7 sky, the three layers, the temperatures, the twinkle curve, the six constellations, the meteor frames, and the sky in place |
| `completed-moon/completed-v7.svg` | The new Completed medal for the 96 and 128 px atlas tiers (with craters), made by `make_completed.py` from the shipped round-5 master |
| `completed-moon/completed-v7-small.svg` | The same face without the three craters, for the 48 and 64 px tiers and the row tier |
| `completed-moon/compare.png` (`compare.html`) | The shipped and v7 medals at 16, 20, 32, 48, 64, 96 and 128 px (each from its tier's source), both v7 sources at 256 px, 18 and 20 px enlarged 6x, and table rows |

**Units.** Values are logical px at UI scale 1.0 (`UiMetrics.Px`), with type given as a factor of the body size. "Body" is Dalamud's font size times Text size, about 16 px at the defaults. The mock draws a 13 px body, so its px are about 0.8 of the in-game px. Colours use the Night palette names from v13.

**The quest in every view** is the owner's own, from `owner-quest-pane.png`: *Blue Collar Work* (BLU, Lv 10, Blocked).
- Requirements: 2 of 3 unmet.
- Rewards: 855 EXP · 414 gil.
- Unlocks: Blood Drain.
- Moonlit: listed.
- Path: A Realm Reborn, 162 done.

The Full detail pane is scrolled a little, so the quest title and Requirements show together.

---

## 1. Quest-pane section headings (owner point 1)

The owner's guess is right: the heading face comes from **Game fonts for headings**. At Full, headings use the Eyebrow role: TrumpGothic at 1.45x body, in GiltHigh. On the owner's screen that is 12.5 px caps with 1 px strokes, peak ink `#CAB27C`, and only about 20 pixels at full ink per word. It is bigger than body caps (10 px) but thin and condensed, so it reads small. With the setting off, `Typography.Push` falls back to the **Caption** role (0.85x body), which is smaller still. Quiet draws headings in the body font at body size. Plain uses `ImGui.TextDisabled`, which is the dimmest tone.

v7 adds a **Section** type role, used for these headings:
- the detail pane's cards and open sections (Requirements, Rewards, Unlocks, Moonlit, Path, Journal, Giver and the rest);
- the filter drawer's sections.

The tree's JOURNAL eyebrow and the table's group headers keep the Eyebrow role.

| | Full | Quiet | Plain |
|---|---|---|---|
| Face | TrumpGothic (the game display face) when Game fonts for headings is on and the string is covered. Otherwise the Quiet treatment, never Caption. | Dalamud body face (Noto Sans Medium). The atlas has no bold, and we do not fake one. | Dalamud body face |
| Size | **1.80x body** (was 1.45x) up to 110 % Text size, then tapering linearly to **1.60x at 150 %**, so headings never take over a narrow pane. That is 28.8 px at a 16 px body: TrumpGothic 23 pt (30.7 px) drawn at 0.94. Caps are about 15.5 px (was 12.5). | **1.15x body** (was 1.0x). That is 18.4 px, from a body handle built at round(1.15 x body), not window font scale. | 1.0x body |
| Case | Capitals in English, as today | Sentence case | Sentence case |
| Tracking | **+0.08 em** (about 2.3 px). ImGui has no letter spacing, so a new `Chrome.TrackedTextAt` draws glyph by glyph (`advance + tracking`), and `HeadingLayout` measures with it. | 0 | 0 |
| Ink | **GiltLight `#E6CF98`**, a new `Surface.OrnamentLight` (9.5:1 on Raised; GiltHigh is 8.0:1), plus a 1 px shadow under it in Abyss at .55, so thin strokes hold on the card gradient | Text `#DDE3F0` | **Text `#DDE3F0`** (was TextDisabled) |
| Right caption | Caption role in TextSecondary (was Tertiary). Unmet counts are in EclipseText. | same | same |
| Heading row | 30 px, then 10 px to the content | 26 px, then 8 px | **A 22 px band** in `#1A1F2C` with a 1 px Line under it, full bleed, then 2 px |

**With Text size** (the role scales with the body; it takes the nearest TrumpGothic size and never scales it up by more than `TypeScale.MaxUpscale` 1.1):

| Text size | 80 % | 90 % | 100 % | 110 % | 125 % | 150 % |
|---|---|---|---|---|---|---|
| Body | 12.8 | 14.4 | 16 | 17.6 | 20 | 24 |
| Full factor | 1.80 | 1.80 | 1.80 | 1.80 | 1.725 | 1.60 |
| Full px | 23.0 (TG 18.4 x0.94) | 25.9 (TG 18.4 x1.06) | 28.8 (TG 23 x0.94) | 31.7 (TG 23 x1.03) | 34.5 (TG 34 x0.76) | 38.4 (TG 34 x0.85) |
| Quiet, 1.15x | 14.7 | 16.6 | 18.4 | 20.2 | 23.0 | 27.6 |
| Plain, 1.0x | 12.8 | 14.4 | 16 | 17.6 | 20 | 24 |

**Code**
- `TypeScale`: add `SectionFactor = 1.80f`, `SectionFactorAt150 = 1.60f`, `SectionFactorFor(textScale)` (the taper), `HeroTitleFactor = 1.92f` and `SectionGameFont(bucket, basePx)`.
- `Typography`:
  - add a Section handle (TrumpGothic, built beside Eyebrow);
  - add a "body x1.15" handle for Quiet and for the fallback;
  - `Typography.Section(text)` falls back to that handle, not to Caption.
- `Chrome.EyebrowTitle` (Chrome.cs 188–210) uses `Section` and the `OrnamentLight` ink with the shadow.
- `SectionHeading.DrawLine`:
  - takes a role, and `OpenSection.Begin` passes Section;
  - at `RuleStyle.Line` it draws the band instead of `TextDisabled`.

**Hierarchy: the quest title stays the largest text in the pane.** At 1.80x the section caps would match the hero title (Jupiter 20). So the hero quest title rises to **Jupiter 23 drawn at 1.0**: a new `TypeScale.HeroTitleFactor` = 1.92, that is 30.7 px at a 16 px body, with the same taper rule. Its cap height must stay at least **1.2x the Section caps** at every Text size; add a `TypeScale` unit test for it. The order is title > state > section > body.
- Quiet's title block grows to 1.40x body, and Plain's to 1.15x, for the same reason.

This is the change that makes `before-after.png` lower left read.

---

## 2. The filter drawer (owner point 2)

**What is wrong today** (`owner-filter-menu.png`, `MainWindow.Frame.cs` 611–725):
- The drawer is `max(tree width, 300)` wide and full body height. Below Reset everything is empty `#1E2437`.
- The title is body-size TextSecondary, and the tree's JOURNAL eyebrow and rule paint over it, so "Filters" sits under "JOURNAL".
- The tree's counts and selection bar show at the edges. Since 300 is wider than the tree at Quiet and Plain, the drawer also covers part of the list.

**Kept, as the owner asked:** the place (over the tree, under the toolbar), the 0.16 s fade (`FadeSeconds`), the pin, ×, Esc, the Filters button, and click-outside to close.

### 2.1 Fixes

| Problem | Fix |
|---|---|
| Empty grey | **Size to content.** Height = header + body content + footer, measured from the previous frame (the cursor's max Y in the body child), capped at the body height. Past the cap, the body scrolls and the footer stays. While Advanced is collapsed, the space is used by seven **summary lines** (§2.3), so the sheet ends where its content ends. |
| Unreadable title | A real header: a glyph, **Filters** in the Title role, a count, then pin and close (§2.2) |
| Text bleeding at the sides and top | **The drawer is exactly the tree column**: `leftWidth`, with no 300 minimum (`DrawerWidthLogical` goes). The narrowest column, Plain's 262, fits every control in §2.3. So the drawer never overlaps the list. **The tree is not drawn while the drawer is open**: `DrawTabBody` draws the tree under `ImGuiStyleVar.Alpha = 1 − fade` and skips it once fade reaches 1. Nothing of the tree can paint over the sheet, whatever the draw order. Below the sheet, the column's own background shows: Full's sky (with its stars), Quiet's tree tone, Plain's flat tone. |

### 2.2 Look, by level

| | Full | Quiet | Plain |
|---|---|---|---|
| Sheet | Raised gradient (`#212742` → `#171C2F`) at .995. **Brass edge** (the card's lit gradient) on the right and bottom, the two sides that face content. Radius 6 at the bottom right, with a 9 px darker corner mark. Drop shadow 0 14 30 at .55, straight down like every v13 shadow. Separation from the list is an **unoffset contact shadow**, 0 0 14 at .30, clipped to the list side: ambient occlusion along the edge, not a second light. Inner top highlight MoonHigh .06. | Flat `#1A2135`, one tone above the tree. 1 px `#2E3650` on the right and bottom. Radius 6 at the bottom right. Separation shadow 0 8 20 at .45 only. | Flat `#151A25`, 1 px `#303648` on the right and bottom. No radius, no shadow. |
| Header | 52 px:<br>• a bare 13 px MoonHigh filter glyph (no disc);<br>• **Filters** in the Title role at Jupiter 16 (about 19 px), `#F4F1E8`;<br>• a **neutral** count pill ("3 on"), 19 px, `#262D42` with Text: gold means act now, and a filter sheet asks for nothing;<br>• pin and × as 26 px round buttons (pinned: gold ring and tint).<br>A moon-road divider under it. | 46 px:<br>• a bare 13 px glyph in Mist;<br>• **Filters** in body x1.15, Text;<br>• a neutral count pill (`#262D42`);<br>• 26 px round buttons with a 1 px border.<br>A hairline under it. | 26 px band (`#1A1F2C`):<br>• **Filters** at body size, Text;<br>• "· 3 on" in Mist;<br>• 20 px square buttons, radius 3. |
| Section heads (Show · Quick views · Advanced) | The Section role (§1) with a rule in **OrnamentLight** fading out to the right. The "2 set" pill is neutral, as the count pill. | The Section role, Quiet | A 20 px band |
| Toggle rows | 36 px. Label 13 px Text, caption 11 px Tertiary under it. `Chrome.MoonToggle` 36 x 20 (gold crescent when on). **The crescents are the only gold in the drawer's body.** | 34 px. **Silver MoonToggle**: the track lerps toward Text at .30, the border is Text at .55, and the knob is a silver crescent. Quiet's gold is kept for states. | 24 px. A 14 px checkbox first (FrameBg `#202634`, border `#3A4050`, Text check), then the label, then the caption inline. |
| Stepper (Stalled after) | 28 px pill on Sunken, with 22 px round − and + buttons | same, flat | 20 px, radius 2 |
| Footer | 48 px over a brass rule. "Showing **7** of 26" left, **Reset** right. | 42 px over a hairline | 24 px band (`#0D1018`) |
| Reset | A quiet text action. Reset glyph and label in TextSecondary, no border. Hover: Text and a `#262D45` .8 wash, 28 px pill. **Disabled** (Veil) when nothing is set. Followed by the **Undo toast** "Filters reset · Undo", 8 s, which restores the filter set and the search. A new `GuardedAction.ResetFilters`: tier None, Undo on. | same, flat wash | An underlined text link in Mist |

### 2.3 Content (every control keeps its current binding to `UiState.Filters`)

- **Show**
  - *Hide completed*, with the caption "Per category ›" or "Per category · 2 changed ›". The caption is a link that opens today's Overrides popup, unchanged.
  - *Available now*, with the same caption.
  - *Pinned first*, with "Sort pinned quests to the top".
- **Quick views**
  - *Stalled after* stepper (1–90 days, as `Configuration.MinStalledDays`/`MaxStalledDays`; hold a button to repeat), with "The Stalled view counts from here".
- **Advanced**, closed: seven 30 px summary lines.
  - The lines: States ("All 8" or "7 of 8") · Expansions ("Any" or "ARR, HW") · Added in · Level ("1 to 100") · Job · Rewards · More ("None on" or the names).
  - Each line shows its value in Text. When the value is not the default, it keeps Text and gains a 5 px **MoonHigh dot** (Full), a Silver dot (Quiet) or a gold "*" (Plain).
  - Clicking a line opens Advanced and scrolls the body to that group (`SetNextItemOpen`, then `SetScrollY` on the next frame).
  - The header carries a neutral "2 set" pill.
- **Advanced**, open:
  - *States*: flowing toggle chips, 26 px, each with its 16 px medal (the row tier at Full, LightRim at Quiet, the flat glyph at Plain). On is a raised fill with a Dusk border. Off is dashed, with the medal at .38 and desaturated.
  - *Expansions*: six cells that toggle on their own (a new `Chrome.ToggleRow`; `Segmented` is single-choice).
  - *Added in*: the existing combo, as a 28 px pill.
  - *Level*: `DragIntRange2` as two 28 px pills ("Lv 1" to "100"), with a 4 px track under them that shows the range in **Dusk** at every level (no track at Plain).
  - *Job*: `Chrome.Segmented` (All · DoW/DoM · DoH · DoL), then the *Current job only* toggle.
  - *Rewards*: a 22 px Hide · Show · Only segment per kind. It shows the six most-used kinds (Mount, Minion, Orchestrion roll, Emote, Hairstyle, Triple Triad card), and "17 more kinds ›" opens the rest.
  - *More*: the seven toggles (Repeatable only … Once-only story quests I haven't done). Long labels wrap.

**Motion.** As in 1.13, the fade is 0.16 s.
- The tree fades out over the same 0.16 s.
- The Advanced chevron turns over `Chevron` 0.14 s.
- A group's contents reveal over `Reveal` 0.14 s, opacity only.
- The sheet's height changes at once: the layout stays static, and nothing slides.
- Plain: no transitions.

**New English strings** (localization is frozen; other languages fall back to English):
- Show
- Per category ›
- Per category · {0} changed ›
- Sort pinned quests to the top
- The Stalled view counts from here
- Showing {0} of {1}
- All 8, {0} of 8, Any, {0} to {1}, None on
- {0} on, {0} set
- {0} more kinds ›
- Filters reset

---

## 3. The stars (owner point 3)

**Full only** (`FlairRules.StarField`). Stars stay in empty sky only, never under text. Each sky region is a rect with no items in it:
- the rail's gap between the last station and the foot;
- the tree below its last node, and below the drawer while it is open;
- the table's title band between the title and its right caption;
- a Path band's right side, past its labels.

Every mark keeps 4 px inside its rect. The field is seeded (the existing LCG), so it never reshuffles.

### 3.1 Three depths

| Layer | Share | Mark | Alpha | Twinkle |
|---|---|---|---|---|
| Far | 60 % | 1 x 1 px rect | .10–.16 | none |
| Mid | 32 % | disc r .95 | .20–.28 | 1 in 3: x1.18 and x.82 |
| Near | 8 % | disc r 1.35, a **5 px cross** (two 1 px lines at **.4** of the star's alpha), a soft r 3.4 disc at .13 | .52–.60 | all: x1.28 and x.72. The cross only fades with the star: **its length is fixed**, so the spike never pulses. |

There is no parallax: the layout is static. Depth comes from size, alpha and halo.

### 3.2 Four temperatures

| Temperature | Hex | Share | Layers |
|---|---|---|---|
| Cool white | `#DCE5FF` | 64 % | all |
| Moon white | `#F4F2EA` | 24 % | all |
| Warm gold | `#FFE2A8` | 9 % | mid and near |
| Ember | `#FFC9AE` | 3 % | mid and near |

Far stars are cool or moon white only.

### 3.3 Twinkle

Twinkle applies at rest, at Full, with Reduce motion off (`FlairRules.Motion`). Each twinkling star has a period of 7–13 s and a phase, both seeded. Alpha = base x the curve: up to the peak at 30 % of the period, down to the trough at 70 %, back at 100 %, eased in and out (the curve is in `stars.png`). That is a breath, not a flicker: below any photosensitivity concern, and slower than any UI motion. Under Reduce motion, Quiet or Plain, stars draw at their base alpha.

### 3.4 The Milky Way (optional, off by default)

The critic would cut it; the supervisor accepts it. It becomes **its own setting, off by default** (Settings › Display › Look, under Decoration: "Milky Way in the sky", Full only), and its default is an owner decision (§8). When it is on:
- it is drawn only in **one continuous sky rect at least 200 px tall** (in practice the tree's empty sky in a tall window); it is never split across rects, so it can never show as separate grey smudges;
- if no rect qualifies this frame, there is no band.

One band, 28° from level (the medals' moon tilt), about 74 px wide.
- A baked sprite in `OrnamentAtlas` (`MilkyWay`, 256 x 64, soft and mottled), drawn with `AddImageQuad` rotated.
- Tint `#C9D3F0` at .06, clipped (`PushClipRect`) to that one rect.
- About 40 % more far stars are scattered along its axis.

### 3.5 Region constellations

These are our own motifs, not in-game star charts, drawn in the Astrologian card idiom: round stars at the joints, hairline links and a brighter lead star. Each has 5–8 stars:

| Expansion | Constellation |
|---|---|
| A Realm Reborn | **The Chocobo** (a bird in profile: beak, head, neck, back, tail, body, two legs). Not a crystal: the owner vetoed the aetheryte look. |
| Heavensward | **The Wyrm** |
| Stormblood | **The Lotus** |
| Shadowbringers | **The Tower**: a tall, narrow spire, two parallel stems converging to a crown star. Not a plus sign. |
| Endwalker | **The Crescent** |
| Dawntrail | **The Plume** |

- **One on screen**, for the selected quest's expansion.
- It is drawn at 70–90 px in the first sky rect that holds a clear 120 x 100 box: the tree's empty sky first, then the Path band's free area. Otherwise there is none.
- Lines are 1 px at .09 (ImGui's thinnest stable line; the mock draws 0.8 at .11). Stars are r 1.15 at .48; the lead star is r 1.45 at .6, with r 2.6–3.4 halos at .07.
- Data: a small Core table (`Constellations.For(expansion)` gives unit-box points and edges), unit tested for staying in [0, 1].

### 3.6 The completion meteor

A **moment**, Full only, never under Reduce motion.
- **Trigger:** the trigger that starts `MotionTokens.Wax` (a visible quest became Completed, within `CompletionWindow`). At most once in 30 s.
- **Path:** one streak in the largest empty sky on screen. It starts 20 % in from that rect's top left and travels 64 x 34 px at 28° below level.
- **Timing:** a new token, `MotionTokens.Meteor = 0.7f`, listed in `Moments` (all moments stay under a second). Position eases out cubic.
- **Brightness:** the head peaks at **.80**, so it is brighter than any star (the near stars reach .60) and reads as a meteor, not a moving star. The tail starts at **.45** and fades to 0. Use `MomentAlpha(p) / MomentPeak x .80` with a fade-in over the first 8 %. This is the one moment above `MomentPeak`: it is under a second, small, and cool white, not a gold flash.
- **Drawing:** the head is r 1.6 MoonHigh with an r 4.5 halo at .25 of the alpha. The tail is 46 px of six `AddLine` segments.

**Code**
- `Core.Ui.StarField`:
  - `Star` gains `Layer`, `Temperature`, `Period` and `Phase`;
  - `Generate` draws them from the same LCG, so existing seeds stay stable;
  - add `StarField.Alpha(in Star, double time, bool twinkle)`, and `StarField.Band(...)` for the band's extra stars (used only when the setting is on).
- Draw sites, as in v13:
  - `TabStrip.cs` (`RailStars`: raise 9 to about 18 for the taller gap);
  - `TreePane.Art.cs`;
  - `TablePane.cs`;
  - `PathChart.cs` (its bands already know their expansion).
- Cost: at most about 200 rect or circle calls a frame, and one quad. Negligible.

---

## 4. The Completed moon (owner point 5)

v7 changes **only the moon face**. The well, the brass rim, the corner pips and the gilt check are the same bytes. `make_completed.py` writes two sources from the round-5 master:
- **`completed-v7.svg`** for the **96 and 128 px** atlas tiers (and their 2x): everything below, including the three rim-lit craters.
- **`completed-v7-small.svg`** for the **48 and 64 px** tiers and the row tier: the same face **without the three craters**. At those sizes a crater is two or three dark pixels with no legible rim, which reads as a hole in a lit disc (the cheese failure).

The owner asked for the craters to be darkened. They stay where they read well, in the hero, tooltips and other large sizes. The darker maria and the cool glow, which are the owner's "more darkening" and "slight glow", are at every tier.

| Change | From | To |
|---|---|---|
| Maria colour | MoonstoneMid `#95A5C8` | basalt `#56658C` |
| Maria opacity | .24–.36 | x1.6, so .38–.58, capped at .62 |
| Maria blur | stdDeviation 2.45 | 1.9 (they read as shapes at 32 px and up) |
| Mare hearts | none | Imbrium, Serenitatis and Procellarum get a darker core: `#3F4B70` at .26–.34, blur 1.2 |
| Craters (96 and 128 px tiers only) | none | Three small rim-lit craters (r 1.4–2.4), lit from the upper left like every medal. At 96 px their rims span 2 px or more, so they read as depressions:<br>• the floor is `#3F4B70` at .24;<br>• a .7 shadow arc on the upper-left inner wall;<br>• a .7 lit rim `#F4F2EA` at .55 on the lower right.<br>All are placed clear of the check. |
| Young crater (every tier) | none | One young bright crater (a Tycho): r 1.3 at .55, with an r 4.2 halo at **.05** (halved in review), no rays. A bright spot cannot read as a hole. |
| Moon glow | none | A **cool** radial glow in the well around the face: `#E2E8F4` .26 at r 34 → `#C3CEE4` .10 at r 40 → 0 at r 49, clipped to the well. |

**Why the glow is allowed.** v13's rule is that nothing Completed glows. That rule is about the **warm** Moon-gold halo that marks something to act on (Ready). This glow is moonlight inside the medal's own well:
- it is cool, not warm;
- it never crosses the rim;
- it is never drawn as a UI glow around the medal.

So Ready keeps the only warm halo in the table.

**Not cheese.** The cheese failure is a flat yellow disc with evenly spaced round holes. This moon avoids each part of it:
- the seas are blurred, irregular, overlapping blobs in a blue-grey basalt, laid out as the real maria;
- the three craters exist only at 96 px and up, where their rims read, so they are depressions, not holes;
- there are no hard-edged dark circles.

**Measured** with `docs/design/moon-v6/round5/metrics.py` (row tier, greyscale) on `completed-v7-small.svg`, the source those sizes draw from, shipped → v7:

| | Shipped | v7 |
|---|---|---|
| Completed salience, 16 px | 61.1 | 61.0 |
| Completed salience, 20 px | 96.5 | 96.5 |
| Completed / Ready | 0.72 | 0.725 at 16 and 20 px (target about 0.72, never over 0.8) |
| Weakest pairs | Blk-Lock 12.0, Lock-NotC 12.3 | unchanged: Completed is in no weak pair |

The darker maria and the brighter well balance in luminance, so Completed still recedes and the ladder holds.

**Ship:**
1. Copy `completed-v7.svg` over `medallion-r5/completed.svg`, and `completed-v7-small.svg` over `_row/completed.svg`.
2. Give `docs/design/moon-v6/round5/gen_atlas.py` a per-tier source for Completed: `completed.svg` for the 96 and 128 tiers, `_row/completed.svg` for 48 and 64. Rerun it (`medals.png` and `medals@2x.png`).
3. In `MedalArt.FullMaria` (MedalArt.cs 754), the vector fallback, use the same colour, opacity x1.6 and the three hearts. Craters and glow are atlas-only: the fallback draws while textures load, and the fine-detail floor is 32 px.

Quiet uses the same face, so it gets the change. Plain's flat glyph is unchanged: no light at Plain.

---

## 5. Journal column headers (owner point 6)

| | Full | Quiet | Plain |
|---|---|---|---|
| Role | Eyebrow at **1.55x body** (was 1.45x): a new `TypeScale.HeaderFactor`. That is 24.8 px at a 16 px body (TG 18.4 x1.01); 125 % gives 31.0 (TG 23 x1.01); 150 % gives 37.2 (TG 23 x1.21 is over 1.1, so TG 34 x0.82). | Body size, **1.0x** (was Caption 0.85x) | Body size, 1.0x (was Caption) |
| Tracking | **+0.08 em** through `TrackedTextAt` | 0 | 0 |
| Ink | **TextSecondary** (was Tertiary). The sorted column stays gilt (`OrnamentLight`). | TextSecondary (was Tertiary). Sorted: Text. | TextSecondary. Sorted: Text. |
| Header row | **30 px** (was 26) | **28 px** (was 26) | **22 px** band (was 20) |

**Hierarchy:** the column labels are metadata and the group headers (MAIN SCENARIO and the rest) are structure. So the column headers are never larger than the group headers (Eyebrow 1.45x with +0.16 em, which reads as wide). Add this as a `TypeScale` test.

These follow Text size like every role. In code: `TablePane.HeaderRole` and `DrawHeaders` set the ink, and `TableSetupScrollFreeze` keeps the header frozen. The extra height comes from the role's line height plus `CellPadding`; a new `ScaleMetrics.TableHeaderMin(flair)` floors it.

---

## 6. The left rail (owner point 8)

**Removed:**
- the thread (`DrawThread`), the brass segments between stations the owner disliked;
- the lit bar above the active icon (`LitBarLogical`, `ThreadAlpha`).

**New:** the bead moves to the rail's left edge, and each station gets a plate.

| | Full (rail 64) | Quiet (rail 60) | Plain (compact rail 44) |
|---|---|---|---|
| Station height | **Shares the rail**: clamp((rail − crest − foot − sky reserve) / 5, 54, 84). The sky reserve is 96, room for stars. The mock draws 72. | the same rule, clamp 50–76, reserve 32 (mock 66) | clamp 40–52, no reserve (mock 46) |
| Icon | **30 px** (`StationIconLogical` 22 → 30). The Journal orbit and the art icons are drawn at that box. | **28 px** | **20 px** (was 16) |
| Label | **0.78x body** (`RailLabelFraction` .7 → .78), gap 5 | 0.78x | none (compact) |
| Plate | Inset 5 px on each side and 3 px top and bottom, radius 10.<br>• Hover: `Hover` `#262D45` at .55 x hover.<br>• Hover also draws a **1 px darker bottom edge** (Abyss at .38), so the 2 px rise has a cause.<br>• Selected: Moon at .07 only (a flat fill; ImGui's multicolour rect has no rounding). **No border, no hairline.** | Hover: `#1C2338`. Selected: Text .06 with a 1 px `#3A4260` border. | Full-bleed, no radius. Hover: `#1D2230`. Selected: the Plain band `#1A1F2C` and a 2 px Text bar on the left edge. |
| Selected marks | **Three:** the plate, the gold icon ink (Moon) and the bead. **No glow**: `GlowRadiusLogical` and the glow draw go, so Ready and the primary pill keep the only warm halos. | The plate, gold icon ink and the dot | The band and the bar; icon ink Text |
| Bead | 7 px MoonHigh disc with a 1.5 px Night rim and a glow at .7, on the left edge (centre x = rail left + 4.5), at the icon's centre line | 5 px Moon dot, no glow | none |

### Motion (`MotionTokens`)

| Moment | Full | Quiet | Plain |
|---|---|---|---|
| Hover in | Plate 0 → .55, icon alpha .72 → .95, **icon rises 2 px**, over `HoverIn` 0.12 s, ease-out cubic (`Motion.Hover`) | Rise 1 px, `HoverIn` | instant |
| Hover out | `HoverOut` 0.18 s, ease-out cubic | `HoverOut` | instant |
| Label | Colour only (Secondary → Text). **The label never moves**: text the player reads only changes opacity. | same | n/a |
| Select | The new plate and the gold icon ink fade in over `Select` 0.15 s. The old ones fade out over `Leave` 0.12 s. The icon settles to 0 px. | Plate, `Select` | instant |
| Travel | The bead runs the left edge from the old station's centre to the new one over `Travel` 0.22 s, ease-in-out cubic (`Motion.Pulse(StationKey, Travel)`, as today), and lands as the plate finishes | The dot, the same | none |
| Reduce motion | Everything lands at once | same | same |

The Journal badge stays at the icon's top right, clamped inside the station as today.

---

## 7. Realism notes for the supervisor

- **One light.** The moon is upper left.
  - Brass edges run bright upper left to dark lower right. The drawer's brass is on its right and bottom only, where it meets content, and its bottom-right corner mark is the darker brass.
  - Crater rims are lit on the lower right inside.
  - The meteor and the Milky Way follow the 28° tilt.
  - Every shadow falls straight down. The drawer's separation from the list is an unoffset contact shadow, not a second light.
- **Glow is light, not paint.** The warm halo is only on Ready and the primary pill: the selected rail station no longer glows. The Completed moon's glow is cool, in-medal moonlight. Stars glow only as their own small halos.
- **ImGui limits respected.**
  - There is no letter spacing, so `TrackedTextAt` draws glyph by glyph.
  - There is no rounded gradient, so plates are flat fills.
  - Lines are at least 1 px.
  - The band is a baked sprite.
  - No bold exists in the atlas, so Quiet headings grow by size, not weight.
  - Fonts come from handles, never `SetWindowFontScale` upscaling.
- **No layout motion.** The drawer's height changes at once, labels never move, and only icons rise.
- **High contrast** still caps at Quiet. The drawer's sheet gains a `VeilLine` border there, and the summary values keep their dot or asterisk, so meaning is not carried by colour alone.

## 8. Open questions for the owner

Decided in review, so no longer asked:
- **Heading size:** 1.80x at the default Text size, tapering to 1.60x at 150 %, with a larger quest title.
- **Constellations:** our own motifs in the Astrologian card idiom. The game has no per-expansion constellations, and the Astrologian's arcana skies would map onto expansions arbitrarily.
- **The meteor:** on by default at Full, with its own toggle under Motion, "Shooting star on completion", so it can be turned off without losing the twinkle.

Still for the owner:
1. **The Milky Way.** It is built as its own setting, off by default, drawn only in one large continuous patch of sky. The critic would leave it out; the supervisor finds it restrained. Should it be on by default at Full, off by default, or not offered at all? `stars.png` shows it on its own tile.
2. **A Realm Reborn's constellation.** The Chocobo replaces The Crystal (the owner vetoed the aetheryte look). Is a chocobo the right motif for A Realm Reborn, or would you prefer a Twelve sigil?

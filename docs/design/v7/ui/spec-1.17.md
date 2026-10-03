# 1.17 "Mix and match": per-state moons, frames, share codes, the glyph window and two palettes

This spec answers the owner's point 7: "Users can choose themes, or also mix-and-match icons as well (and even ui/ux color pallettets)." It covers:

| Row | What |
|---|---|
| **T10** | Mix moons by state |
| **T11** | Frames as a choice |
| **T12** | Share codes |
| **T13** | The glyph window's Themes tab |
| **T16** | The Dawn and Kugane Lacquer palettes |

It builds on `docs/research/plan-v7/theme-system.md` §5.1–5.4, §8.2 and §9, and on the 1.16 Themes page (`spec-1.16.md` §B, whose right half sketched this).

**Revision 2** records the supervisor's rulings and the owner's decisions:
1. Per-vision-mode cross-set bars (§A3).
2. Silver on light palettes keeps the Abyss outer keyline (§B2).
3. New build gates on Dawn and Kugane, with Kugane's halo fallback (§E4.1).
4. Kugane's hazier sky (§E2.1).
5. The Orrery joins the mix only once measured (§A1).
6. The optional chat command (§C2).

**Every number on the mix comes from the shipped build output**, read by `1.17/mixdata.py`: `Tsukimichi/assets/ui/themes/<set>/metrics.json` (`tools/themes/build_themes.py`, Chrome 154). The same rules run in Python (`mixdata.py`) and in the mock (`mock-src/v717.js`).

**Files**

| File | What it is |
|---|---|
| `spec-1.17.md` | This spec |
| `1.17/mixdata.py` → `mixdata.json` | The mix rules and Fix it over the shipped metrics, every one-change mix, and the worked example |
| `1.17/sharecode.py` | The reference encoder and decoder for share codes, with the examples below and a typo test |
| `1.17/palettes17.py` → `palettes17.json`, `contrast17.md` | Dawn and Kugane Lacquer, every role, their high-contrast forms, the contrast table and the colour-vision check. It reuses `1.16/palettes.py`. |
| `mock.html` | `#mix`, `#frames`, `#share`, `#glyphwin`, `#palettes17`, `#dawn-full`, `#dawn-quiet`, `#kugane-full`, `#kugane-quiet`. Sources: `mock-src/v717.js` and `v717.css`. |
| `mix.png` | The mix table: a warning, Fix it, a state's list, and Reset mix held |
| `frames.png` | Four kits (Brass, Silver, Lead came, Astrolabe) on four palettes (Night, Ishgard Snow, Dawn, Kugane Lacquer) |
| `share.png` | Copy, paste with preview, a code from a newer version, a typo, applied with Undo |
| `glyph-window.png` | The Themes tab: two looks, their heat tables and Ready's lead |
| `palettes-1.17.png` | Dawn, Dawn HC, Kugane Lacquer and Kugane HC, with the contrast table |
| `dawn-full.png`, `dawn-quiet.png`, `kugane-full.png`, `kugane-quiet.png` | The Journal window in each palette. Dawn is shown with Astrologian's Orrery, Kugane with Menphina's Medallion. |

The mocks use the approved art in `docs/design/v7/themes/`:
- the composites and `_row/` of Ishgard Glass, Aether Crystal and Astrologian's Orrery;
- their `faces/` and `kit/` layers for re-framing;
- the Orrery's `_mix/` swaps.

They match the shipped atlases in `Tsukimichi/assets/ui/themes/` (the same masters).

---

## A. Mix moons by state (T10)

### A1. What is offered

Each of the 8 states picks its moon:

| Set | Status |
|---|---|
| **From theme** | The default |
| Menphina's Medallion | Shipped |
| Ishgard Glass | Shipped |
| Aether Crystal | Shipped |
| **Astrologian's Orrery** | New in 1.17. It joins the mix **only once its cross-set numbers exist** (the owner's decision): its atlas ships through the T4 build, passes its gates, and the build has measured it against every other set. Until then its card is on the Themes page, but it is not offered per state. |
| Sumi to Kinpaku | Appears in the same list once its art is approved; until then it is not listed at all, as on the 1.16 page |
| Classic | Never offered: whole theme only (research §3.4) |

- **Frames** stay one kit for the whole look (§B), so a mixed column keeps one metal.
- **High contrast** overrides the mix with the shared HC set, as in 1.16. The section then shows one line: "High contrast uses one set of moons; your mix returns when it is off."

### A2. The section (`mix.png`)

The section sits on the Themes page under Frames.
- **Heading:** "Mix moons by state". Below it, one line: "Pick each state's moon from any set. The frames stay one kit (Frames above), so the column keeps one metal."
- **8 rows**, 46 px each, in state order:
  - the state's name;
  - its current medal at **32 px**, in the look's kit (the row tier, no badge);
  - a **combo**, 200 px: the 18 px face, then "From theme" or the set's name;
  - a **reserved note column** for a warning in words, with a quiet amber dot (`#C9A866`), never ⚠. Reserved space means the row never shifts.
- **The combo list:**
  - Every offered set, with that state's face at 20 px. "From theme" names the theme's set.
  - An option that would **add** a warning says so before it is picked, in the option's own row ("Ready would stop leading at large sizes", "close to Blocked in a list").
  - Options that warn are never hidden or disabled.
- **A status line** under the rows, always present:
  - **No warning:** "Every moon reads apart, and Ready leads (1.35×)." plus **Reset mix**.
  - **A warning:** one calm sentence, then **Details ▾**, **Fix it** and **Reset mix**. Details opens a short paragraph that names the numbers in words and the bar each one missed.
- **Static layout:** the note column, the status line and the Details panel (opened by a click) never push the rows. Details opens below the status line.

### A3. The warnings: rules and bars

The numbers are the build's units (round 5's `metrics.py`), from `metrics.json`. Bars come from its `bars` block.

| Warning | Rule | Wording |
|---|---|---|
| **Close** | A pair of states whose moons come from **two different sets** is under its bar in **any** vision mode of the cross-set table (row tier, 16 px, Night). The bars are **greyscale ≥ 12**, and **protanopia, deuteranopia and tritanopia each ≥ 11** (the supervisor's ruling, the same bars each set's own gates use). | "*Blocked* (Ishgard Glass) and *Locked out* (Medallion) look alike in a list." Row note: "close to Locked out in a list". |
| **Hard to tell apart** | The same pair under **10** in any mode | "…are hard to tell apart in a list." |
| **Ready isn't the loudest** | Ready's salience is under **1.25×** the loudest other state, at the row tier **or** the 48 px hero tier (`mixReadyLead`) | "Ready is no longer the loudest moon at large sizes." (or "…in a list") |
| **Completed draws the eye** | Completed's salience is over **0.80×** Ready's, at either tier (`completedOfReady`) | Folded into the Ready line's Details |

**Pairs within one set never warn.** Each set already passed its own gates in the build:
- 12 in greyscale and deuteranopia;
- 11 under Machado protanopia, deuteranopia and tritanopia;
- both judged at one decimal.

A mix cannot make them worse.

**Per-mode values (the supervisor's ruling).** `build_themes.py` records each cross pair **per vision mode** (greyscale, and Machado protanopia, deuteranopia and tritanopia), and the compiled `MixTable` carries all four. That measurement is now with the build-tool agent. The page warns on the first mode under its bar and names it in Details ("close for protanopia"). The heat table's Vision control shows each mode, and "Worst" colours each cell by the mode closest to its bar. The shipped 1.16 tables hold only the worst over all modes. Every one of those values is at least 13.75, so under the new bars nothing can warn on distinctness today.

**What warns today**, from the shipped numbers (`mixdata.py`):
- **Distinctness never warns.** The weakest cross-set pair is Glass's *Not checked* against Medallion's *Locked out* at **13.75**, so no cross pair falls under 12, and none under 10.
- **Ready's lead can warn.** Of the 48 one-change mixes from a pure set, two fail, both at the hero tier:

| Mix | Ready's lead at 48 px | Completed of Ready |
|---|---|---|
| Medallion with Ready from Aether Crystal | 1.23× (it leads 1.30× in rows) | 0.82 |
| Aether Crystal with Completed from Medallion | 1.23× | 0.82 |

The Orrery joins the tables when its `metrics.json` ships. Sumi joins later.

### A4. Fix it

"Fix it" proposes the **smallest change** that clears every warning:
1. Try every single-state change, 8 states × the offered sets, all table lookups. Nothing renders.
2. **Never change the state the player just picked.** Fix it keeps the player's choice and changes one other state.
3. Of the changes that clear everything, prefer the set that **already covers the most states**, then the earliest state in state order.
4. If no single change clears the warnings, offer "Use the theme's own *X*" for the state the warning names.

The proposal is a small popover under the status line (`mix.png`, top right):
- "Fix it suggests one change": the medal now → the medal proposed (40 px each);
- "**Use Aether Crystal for Completed too**";
- "Ready leads again: 1.35× at large sizes, 1.39× in rows. Your Ready pick stays.";
- **Not now** and **Use it**.

"Use it" applies the change, followed by an Undo toast.

**The worked example**, from the shipped numbers: Medallion with Ready from Aether Crystal → Fix it proposes Completed from Aether Crystal → Ready leads 1.35× at 48 px and 1.39× in rows, and Completed is 0.74 of Ready.

### A5. Reset mix, with safety

- **Per-row "From theme"** is one click, followed by an Undo toast.
- **Reset mix** discards several picks, so it is **hold to reset**: `Chrome.HoldButton` with `ConfirmGate`, the Hold tier, held for the player's hold length (0.6 s by default). Ctrl or Shift and a click confirm at once; two-click mode applies for hand strain; under Reduce motion a countdown replaces the arc.
  - The **Moon arc** closes clockwise along the pill's outline (`mix.png`, bottom right).
  - **Undo** follows.
  - The tooltip carries the mix's **share code**, so the mix can always be recovered.
- **"Reset appearance"** (1.16) also becomes Hold while a mix is set (`spec-1.16.md` §B5).
- New guarded actions:
  - `ResetMix`: tier Hold, with Undo;
  - `ApplyFix`: tier None, with Undo;
  - `ApplyShareCode`: tier None, with Undo.

### A6. Code map

| Piece | Where |
|---|---|
| `MixRules` (pure, Core): `Evaluate(mix, metrics)` → close and hard pairs, Ready's lead and Completed's recession at row and hero tiers; `Fix(mix, keep)` | Tsukimichi.Core/Ui/Themes, tested against `mixdata.py`'s outputs |
| `ThemeMetrics`: the slice of each `metrics.json` the page needs (cross pairs, salience, bars), compiled into a table at build time. `metrics.json` itself is not packaged. | `build_themes.py` writes `Tsukimichi.Core/Ui/Themes/MixTable.g.cs` |
| `AppearanceConfig.Glyphs` (already shipped empty in 1.16): state → set key | Config |
| The section | `ConfigWindow.Themes.Mix.cs`. Each combo is a `BeginCombo` with face thumbnails from `ThemeAtlasCache`. |

---

## B. Frames as a choice (T11)

### B1. The kits

| Kit | Resting metal ramp (lit upper left) | Corner mark and sigil | Default for |
|---|---|---|---|
| **Brass** | `#E2C78C` → `#A88B52` → `#6E5732` → `#5A4729` | L corner, four-point sigil | Menphina's Medallion |
| **Silver** | `#E2E8F4` → `#A9B5D0` → `#7B8AAF` → `#5E6E97` | faceted chip corner, cut-gem sigil | Aether Crystal |
| **Lead came** | `#B8C0D0` → `#8C95B0` → `#5A6278` → `#323950`, with a gilt inner line on act-now only | quatrefoil corner, rose-window sigil | Ishgard Glass |
| **Astrolabe** | `#EAD3A0` → `#B8924E` → `#7C6034` → `#4E3B1E`, with a hairline scale at hero only | quarter-arc scale corner, compass sigil | Astrologian's Orrery |
| Kirikane | — | — | Sumi to Kinpaku, when its art ships |

**One kit drives everything metal:**
- **medal frames**, in four urgency tiers (act now, resting, finished, ghost), at Full and as Quiet hairlines;
- **badges**: the seat, the badge ring and the open, closed and journal glyphs in the kit's metal (role seats stay fixed);
- **gauges**: the groove, the arc metal and the moon (on light palettes the arc takes the palette's gauge ink, `spec-1.16.md` §A6);
- **Decoration ornament**: the card frame ramp, corner marks, rules, divider, sigil, crest and tooltip frame.

**Act now is gilt in every kit.** Ready's frame and badge ring are the shared medal gilt whatever the kit, so the urgency signal never changes colour. Only the resting, finished and ghost metal changes.

### B2. How a kit reads on each palette (`frames.png`)

The board shows the Orrery's faces (Ready, In journal, Blocked, Completed, Not checked) framed by each kit on each palette, with a card in the kit's ornament.

| Kit | Night | Ishgard Snow | Dawn | Kugane Lacquer |
|---|---|---|---|---|
| Brass | as shipped | reads; headings in deep gilt `#6E5320` (1.16) | warm on plum: good | **the natural pair** (gold leaf on lacquer) |
| Silver | cool and calm | the faintest on snow; its ramp's dark end (`#5E6E97`, 4.5:1 on snow) carries the edge | reads; cooler than the palette | reads; a strong contrast in temperature |
| Lead came | reads | **the natural pair** | reads | reads |
| Astrolabe | reads | reads | **the natural pair** | close to Brass (both warm); its scale corner tells it apart |

**Every pairing is allowed.** No kit is hidden on any palette. Each kit's darkest stop is the edge contrast on light palettes; on Night, Dawn and Kugane the lit stop carries it.

**Silver on light palettes (the supervisor's ruling):** Silver on Ishgard Snow is accepted at 4.5:1, on one condition. On light palettes **every kit**, Silver included, keeps the **1 px Abyss `#080B16` outer keyline at .6** round its medals, so the medal's outline never depends on the metal's own darkest stop. `frames.png` draws that keyline in the Snow column. Ishgard Snow's own theme (Ishgard Glass) keeps **Lead came** as its default kit.

**The Frames control** is the 1.16 segmented control: From theme · Brass · Silver · Came · Astrolabe (Kirikane joins with Sumi). Hovering a segment previews it in the Preview panel, as theme cards do.

### B3. Implementation

The atlas contract reserved `faces.*` and `frames.*` (`docs/design/v7/themes/ATLAS-CONTRACT.md` §1). 1.17 fills them:
- **Faces:** `build_themes.py` writes **unframed faces** per set (`faces.png`/`.json`: under and over layers, 8 states, hero tiers and row sizes).
- **Frames:** it writes **frames** per kit (`frames.png`/`.json`: 4 urgency tiers × Full and Quiet, the badge ring, seats and glyphs).
- **At runtime:** the plugin layers face under → frame → face over → badge, as the kits' `compose()` scripts do.
- **Fallback:** a set or kit that hasn't shipped split atlases yet falls back to its composite atlas (its own kit) and is labelled "(its own frames)".
- **Gates:** the build re-runs the gates for every face × kit pair. A kit only changes rims, so distinctness moves little, but the act-now gilt must keep Ready's lead ≥ 1.3 in every kit.

---

## C. Share codes (T12)

### C1. Format

The code is `TM` followed by the payload in **Crockford base32**, shown in groups of four after a dash. A theme alone is 6 characters; a full mix is 12.

| Field | Bits | Values |
|---|---|---|
| Version | 5 | 1 (so every v1 code begins `TM1-`) |
| Theme | 4 | GlyphSetId: 1 Medallion, 2 Classic, 3 Aether Crystal, 4 Ishgard Glass, 5 Orrery, 6 Sumi |
| Palette | 4 | 0 from theme, 1 Night, 2 Ishgard Snow, 3 Dawn, 4 Kugane Lacquer, 5 Follow Dalamud |
| Frames | 4 | 0 from theme, 1 Brass, 2 Silver, 3 Came, 4 Astrolabe, 5 Kirikane |
| High contrast | 1 | |
| Has mix | 1 | |
| 8 × state set | 8 × 4 | only when Has mix: 0 from theme, else a GlyphSetId, in QuestState order |
| CRC-8 | 8 | poly 0x07, init 0, over every bit before it; padding bits must be zero |

**Examples** (from `1.17/sharecode.py`):

| Code | Look |
|---|---|
| `TM1-8003-0` | Ishgard Glass, all from theme |
| `TM1-2G45-0` | Medallion on Kugane Lacquer, high contrast |
| `TM1-202C-000C-02C` | Medallion, with Ready and Completed from Aether Crystal (the fixed example of §A4) |
| `TM1-AD24-0000-028` | Astrologian's Orrery on Dawn with Astrolabe frames, Ready from Medallion |

**Tolerant reading:**
- Case, spaces and dashes are ignored, and so is a missing "TM".
- O reads 0, and I or L read 1. U is never written.
- Every single-character typo in the examples is caught by the checksum (`sharecode.py` tests all of them).

**A newer version:**
- A version above 1 reads "This code is from a newer Tsukimichi."
- Unknown ids (a newer build's set, palette or frames) are **named and left out**, and the rest still applies.
- The ids are stable forever (`AppearanceCatalog` pins them; a test holds the table).

**Codes carry ids only:** no colours, no text and nothing to sanitise beyond range checks.

### C2. UI (`share.png`)

The **Share** section sits under Mix on the Themes page:
- the current look's code (monospace, selectable);
- **Copy**: copies the code; a quiet toast says "Code copied · TM1-…";
- **Paste a code**: a field read as you type.

**When the pasted code is valid**, a **preview** card appears under the field before anything changes:
- "This code would change:", then a list of only what differs (Theme Menphina's Medallion → **Astrologian's Orrery**; Palette Night → **Dawn**; Frames Brass → **Astrolabe**; Ready from Menphina's Medallion);
- 5 sample medals at 36 px, rendered in the code's look;
- **Cancel** and **Apply**.

**Other cases:**

| Case | What the player sees |
|---|---|
| From a newer version | The preview names what was left out in amber words, and the button reads **Apply the rest** |
| A typo | "That code doesn't read: a character may be mistyped. Nothing was changed." in Locked-out ink. No preview. |
| Applied | The look changes at once, with an **Undo** toast (8 s) |

**Chat command (the owner's decision: offered, optional):** `/tsukimichi look TM1-…` opens the Themes page with the code pasted, routed through **the same preview**. It never applies on its own: the player still presses Apply, and Undo follows. An unreadable code prints "That code doesn't read." to chat and opens nothing.

---

## D. The glyph window's Themes tab (T13)

`/tsukimichi glyphs` opens the debug window. It gains a **Themes** tab beside Medals, Palettes and Gauges (`glyph-window.png`).

| Part | Contents |
|---|---|
| **Look A and Look B** | Each picks a theme, a mix (or "Current look"), a palette and a kit. A code can be pasted into either. |
| **Vision** | Worst · Grey · Deut · Prot · Trit (it filters the tables) |
| **Two panels** | Each shows the 8 medals at 40 px in the look's one kit, then the 8 row medals at 16 px with their names, on the palette's pane |
| **Two heat tables** | One per look. Lower triangle, 8 × 8, the pair value in each cell (build units, row tier, 16 px, Night). Cross-set pairs are outlined. |
| **Ready's lead** | For A and B: rows at 16 px, 48 px and up, and Completed of Ready at 48 px. A value that misses its bar is in amber. |

**Heat table colours:**
- slate-blue for "reads apart";
- amber `#C9A866` for close (a cross-set pair under 12);
- plum `#8E3A5E` for hard to tell apart (under 10).

**Pairs within one set** are coloured by the gates that set passed (12 grey, 11 colour vision, judged at one decimal). Medallion's Blocked–Locked out at 11.1 under protanopia therefore reads as apart, as the build judged it.

**The example in the render:** A is Medallion with Ready from Aether Crystal; B adds Completed from Aether Crystal. A shows Ready's lead at 1.23× at 48 px in amber; B is 1.35×.

**Audience:** reviewers, the realism supervisor, and curious players. There is no live rendering: every value is a lookup in the compiled `MixTable`.

---

## E. Dawn and Kugane Lacquer (T16)

### E1. Light or dark

**Both are dark.**

- **Dawn** is the hour before sunrise: plum night with a rose horizon.
- **Kugane Lacquer** is black lacquer at dusk.

So both share every dark-path rule with Night:
- the star field, moving sky and meteor stay on (the 1.16 ruling: stars are a dark-palette feature);
- glows stay light;
- portraits use the night grade;
- banners take the night grade.

The light-palette rules (no stars, washes instead of glows, the light Ready gate) apply only to Ishgard Snow.

**Medals are never recoloured** on either palette.

### E2. The roles

The values are research §8.2's proposals, **re-tuned only where the worst surface missed the bar**:
- **Dawn:** StrongLine `#6F6486` → `#76698C` (3.0:1 on Raised); TextTertiary `#9A8BA2` → `#A495AC` (4.5:1 on Hover); NotChecked `#9C8FA8` → `#A595AE` (4.5:1 on Hover).
- **Kugane Lacquer:** StrongLine `#705C52` → `#7A6458` (3.0:1 on Raised); TextTertiary `#998775` → `#A69482` (4.5:1 on Hover); NotChecked `#9A8C80` → `#A89888` (4.5:1 on Hover).

| Role | Dawn | Kugane Lacquer |
|---|---|---|
| Window · Sunken · Raised · Hover | #1A1526 · #120E1B · #262036 · #30283F | #16100F · #0E0A09 · #231917 · #2D211E |
| Line · StrongLine | #352D46 · #76698C | #342620 · #7A6458 |
| Text · Secondary · Tertiary | #F2E8E6 warm pearl · #C4B4C0 · #A495AC | #F3E9DB washi · #C6B6A2 · #A69482 |
| Deep (rail) | #0F0B17 | #0B0807, with a 1 px vermilion lacquer edge (`#B23422` at .45) on its right |
| Sky (Full) | zenith #3A2746 → horizon rose #5A3448 at 34–46 % → #2B1F3A → #1A1526 | zenith #3A1A14 → vermilion dusk #4E2218 at 40–46 % → #2A1613 → #16100F |
| Ornament (default kit) | Astrolabe (the Orrery's kit); OrnamentHigh #E9C4A4; Section ink #EDCBAA | Brass; OrnamentHigh #E7C87C; Section ink #ECD08A |
| Accent (gold as text) | #F5C47C dawn gold | #F0CC72 |
| Cool (links) | #92A2E4 periwinkle | #7FA3DA ai-zome indigo |
| Ready / In journal / Completed words | #F5C47C | #F0CC72 |
| Ready on another job / Done this cycle | #F2E8E6 | #F3E9DB |
| Blocked | #C4B4C0 | #C6B6A2 |
| Locked out | #E68FB4 | #E58AC0 |
| Not checked | #A595AE | #A89888 |
| Stripes: gold · Completed · Locked out | #F5C47C · #B99A6A · #C46A92 | #F0CC72 · #B79A5E · #C25E92 |
| Gauge arc · shade · groove | #F5C47C · #D9A55E · #3A3050 | #F0CC72 · #D4AE55 · #3A2A24 |

**Vermilion in Kugane** lives only in surfaces (the sky's dusk band and the rail's lacquer edge), never in ink: red already means Locked out and destructive.

#### E2.1 Kugane's sky: hazier, over a lantern-lit port (the supervisor's accepted option)

On Kugane Lacquer, the dark-palette star field changes in two ways:
- **Half the density:** every sky rect draws half the stars it would on Night, at least 4 per rect (`StarField.CountFor` × 0.5). The layer shares are unchanged (far 60 %, mid 32 %, near 8 %).
- **Warmer far stars:** the far layer's two inks become `#E9E2DA` (60 %) and `#F1E3CC` (40 %), in place of Night's cool white `#DCE5FF` and moon white `#F4F2EA`. Mid and near stars keep the four temperatures.

The haze itself is the palette's existing vermilion dusk band (§E2), with no new layer. Twinkle, drift, meteor and constellations are unchanged.

The result reads as a lantern-lit port's sky, where the city's glow washes out the faint stars (`kugane-full.png`).

`SceneTokens` gains `StarDensity` (1.0, or 0.5 for Kugane) and `FarStarInks`.

### E3. High-contrast forms

The 1.16 transform:
- no sky;
- opaque strong-line ornament;
- every ink pushed toward Text until it reads **7:1 on the Window**.

Dawn HC and Kugane HC derive from their own warm inks, not Night's.

| Ink on the Window | Dawn HC | Kugane HC |
|---|---|---|
| Text | 14.8 | 15.7 |
| Secondary | 9.0 | 9.5 |
| Tertiary | 7.0 | 7.1 |
| Accent / Ready | 11.1 | 12.2 |
| Cool | 7.2 | 7.3 |
| Locked out | 7.6 | 7.8 |
| Not checked | 7.0 | 7.1 |
| Blocked | 9.0 | 9.5 |

Moons use the shared HC set, as on every palette.

### E4. Contrast (`1.17/contrast17.md`)

The worst surface is shown.

| Ink | On | Bar | Dawn | Dawn HC | Kugane Lacquer | Kugane HC |
|---|---|---|---|---|---|---|
| Text | Window, Raised, Sunken, Hover | 4.5:1 | #F2E8E6 11.6 | #F2E8E6 11.6 | #F3E9DB 13.0 | #F3E9DB 13.0 |
| TextSecondary | Window, Raised, Sunken, Hover | 4.5:1 | #C4B4C0 7.1 | #C4B4C0 7.1 | #C6B6A2 7.9 | #C6B6A2 7.9 |
| TextTertiary | Window, Raised, Sunken, Hover | 4.5:1 | #A495AC 5.0 | #AD9EB2 5.5 | #A69482 5.3 | #AD9C8A 5.9 |
| Accent | Window, Raised, Sunken, Hover | 4.5:1 | #F5C47C 8.7 | #F5C47C 8.7 | #F0CC72 10.1 | #F0CC72 10.1 |
| Cool | Window, Raised, Hover | 4.5:1 | #92A2E4 5.7 | #92A2E4 5.7 | #7FA3DA 6.0 | #7FA3DA 6.0 |
| OrnamentLight | Window, Raised | 3:1 (large) | #EDCBAA 10.2 | #EDCBAA 10.2 | #ECD08A 11.4 | #ECD08A 11.4 |
| Ready | Window, Raised, Hover | 4.5:1 | #F5C47C 8.7 | #F5C47C 8.7 | #F0CC72 10.1 | #F0CC72 10.1 |
| Completed | Window, Raised | 4.5:1 | #F5C47C 9.7 | #F5C47C 9.7 | #F0CC72 11.1 | #F0CC72 11.1 |
| ReadyOnOtherJob, DoneThisCycle | Window, Raised | 4.5:1 | #F2E8E6 13.0 | #F2E8E6 13.0 | #F3E9DB 14.3 | #F3E9DB 14.3 |
| Blocked | Window, Raised, Hover | 4.5:1 | #C4B4C0 7.1 | #C4B4C0 7.1 | #C6B6A2 7.9 | #C6B6A2 7.9 |
| LockedOut | Window, Raised, Hover | 4.5:1 | #E68FB4 6.0 | #E68FB4 6.0 | #E58AC0 6.5 | #E58AC0 6.5 |
| NotChecked | Window, Raised, Hover | 4.5:1 | #A595AE 5.0 | #AD9DB4 5.5 | #A89888 5.6 | #AC9C8C 5.9 |
| StrongLine | Window, Raised | 3:1 (graphic) | #76698C 3.1 | #877B99 4.0 | #7A6458 3.1 | #8C786C 4.1 |
| GaugeArc | Window, Groove, Zenith | 3:1 (graphic) | #F5C47C 7.6 | #F5C47C 7.6 | #F0CC72 8.8 | #F0CC72 8.8 |
| StripeGold | Window, Raised, Hover | 3:1 (graphic) | #F5C47C 8.7 | #F5C47C 8.7 | #F0CC72 10.1 | #F0CC72 10.1 |
| StripeLocked | Window, Raised | 3:1 (graphic) | #C46A92 4.3 | #C46A92 4.3 | #C25E92 4.3 | #C25E92 4.3 |
| OrnamentHigh | Window | 3:1 (graphic) | #E9C4A4 10.9 | #E9C4A4 10.9 | #E7C87C 11.6 | #E7C87C 11.6 |

Every pair passes. The HC columns also reach 7:1 on the Window (§E3).

**Colour vision** (research §8.2): Accent against Locked out, worst OKLab ΔE under Machado protanopia, deuteranopia and tritanopia:

| Palette | ΔE |
|---|---|
| Dawn | 0.103 |
| Kugane Lacquer | 0.128 |
| Night (reference) | 0.16 |

The gate is 0.08. Dawn's Locked out keeps the research's `#E68FB4`: a lighter `#EE94BC` gained contrast but dropped this to 0.088.

**The glyph gates** (each set's weakest pair ≥ 12 in greyscale and ≥ 11 under colour vision, at 16 px) must also hold on the new windows. `build_themes.py` adds Dawn `#1A1526` and Kugane `#16100F` to its grounds, and `ThemeAtlasTests` asserts them.

#### E4.1 Ready on Dawn and Kugane (the supervisor's ruling: now gated)

The build gates **Ready's lead ≥ 1.3** and **Completed ≤ 0.8 of Ready** on the Dawn and Kugane windows:
- for **every set**;
- at **16 and 20 px**;
- with the same salience measure as on Night.

**If a set fails on Kugane** (the warm, near-black lacquer is the riskier ground for a gold Ready), Ready's halo on Kugane rises from **.45 to .60**, with the **same footprint**. The medal is never recoloured, and the build records which halo it used.

Dawn has no fallback: a failure there blocks the build.

### E5. The windows

| Render | Look | Notes |
|---|---|---|
| `dawn-full.png` | Orrery on Dawn, Full | The plum sky with its rose horizon band behind the tree, the star field and the constellation in the empty sky, Astrolabe-warm cards, and the dawn-gold primary pill |
| `dawn-quiet.png` | Orrery on Dawn, Quiet | The designed Quiet tones: rail `#17121F`, tree `#1A1524`, table `#1E1829`, detail `#221B30`, cards `#2A2338` |
| `kugane-full.png` | Medallion on Kugane Lacquer, Full | The vermilion dusk band, the rail's lacquer edge, and brass on lacquer |
| `kugane-quiet.png` | Medallion on Kugane Lacquer, Quiet | Rail `#140E0D`, tree `#171110`, table `#1B1412`, detail `#201715`, cards `#281D1A` |

The status words, stripes and gauges take each palette's inks. The medals are unchanged.

---

## F. The Themes page in 1.17

The page keeps 1.16's column and widths. Changes:
- **Theme:** the **Orrery** card is added (the second row, beside Classic). Sumi stays hidden until its art is approved.
- **Colours:** palette tiles for **Dawn** and **Kugane Lacquer** are added.
- **Frames:** **Astrolabe** is added (Kirikane with Sumi).
- **New sections:** **Mix moons by state** (§A) and **Share** (§C), appended after Frames. Nothing above them moves.
- **Reset appearance:** Hold while a mix is set.

## Decisions

1. **Distinctness warnings are cross-set only**, judged **per vision mode**: greyscale ≥ 12; protanopia, deuteranopia and tritanopia each ≥ 11; under 10 is "hard to tell apart". A set's own pairs passed its gates and never warn.
2. **Fix it never undoes the player's pick.** It proposes one other change, preferring the set already used most.
3. **Ready's lead is checked at the row tier and the 48 px hero tier.** Today's only real warning appears at the hero tier (Medallion with Ready from Aether Crystal: 1.30× in rows, 1.23× at 48 px).
4. **Frames are one kit per look.**
   - Act now stays gilt in every kit.
   - Every kit is allowed on every palette.
   - On light palettes every kit keeps the 1 px Abyss outer keyline at .6.
   - Snow's theme defaults to Lead came.
5. **Share codes are `TM1-`, Crockford base32 with CRC-8.** They are 6 or 12 characters, tolerant in reading, and name unknown ids rather than failing. The optional `/tsukimichi look <code>` routes through the same preview.
6. **Dawn and Kugane Lacquer are both dark palettes.**
   - Stars, glows and the night grade apply.
   - Three inks each were re-tuned for the Hover surface.
   - Ready's lead and Completed's ratio are gated on both.
   - Kugane's halo falls back to .60 if needed.
   - Kugane's sky is half as dense, with warmer far stars.
7. **Reset mix is hold-to-reset**, with Undo and the share code in its tooltip.
8. **The Orrery joins the mix only once its cross-set numbers exist.**

## Resolved questions

| Question | Resolution |
|---|---|
| Cross pairs per vision mode | Adopted: greyscale ≥ 12, Machado modes ≥ 11. The measurement is with the build-tool agent. |
| The Orrery in the mix | Only once measured (the owner's decision) |
| Silver on Snow | Accepted at 4.5:1, with the Abyss outer keyline on light palettes |
| The chat command | Offered and optional, through the same preview (the owner's decision) |

No open questions remain for 1.17.


**Coordinator decision (Dawn fallback):** if Dawn fails the Ready lead or Completed ratio for a set, Ready's halo on Dawn rises from .45 to .60 with the same footprint, the same rule as Kugane. Medals are never recoloured.

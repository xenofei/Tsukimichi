# Art supervisor review: in-context mock (docs/plan-site/mock)

Subject: `mock-fragment.html` / `preview.html`, scenes `scene-day.jpg` and `scene-night.jpg`, with the round 2 D glyphs and icon.
Renders reviewed: the four in `scratchpad/shots/`, plus new ones in `scratchpad/sup-mock/`: night and day at 2x DPR, 150 %, 900 px, the grid at 100 %, and zoomed crops of the overlay, table, tree, legend and installer.
Checked against `Tsukimichi/Ui/*.cs` (TablePane, TodoOverlay, MainWindow.DrawStatusBar, DetailPane, Theme, Orbit), `Tsukimichi.Core/Todo/TodoList.cs` and `Localization/Strings.resx`.

## Verdict: CHANGES REQUIRED

The craft is good and most of the chrome is faithful. Several things are wrong, though. The overlay shows rows the real plugin never shows. The table colours its state words, which the real one does not. The selection is gold where the code says it is never gold. The tree moons fill backwards. A few touches (shadows, bold text) give away that this is not ImGui. Fixing these also fixes the colour hierarchy.

## Required fixes

1. **Todo overlay content** (`TODO` array, fragment ~L505). The real `TodoList` behaves differently from the mock in four ways:
   - It leaves Completed, Done-this-cycle and Locked-out pins out.
   - It shows **Pinned before Main scenario**.
   - Its Ready hint is "Lv N · giver", not a zone.
   - Its hints never repeat the state name.

   Changes:
   - Order: Pinned first, then Main scenario.
   - Pinned rows: Towards the Firmament `Lv 60 · recruitment notice`; It Could Happen to You `Lv 15 · well-heeled youth`; Carpe Diem `Ready on FSH`; Crossroads `after MSQ: Dawntrail`; Small Business, Big Dreams `step 2 of 5`; The White Wanderer `accept condition` (drop "Not checked ·").
   - Remove the rows for In the Shadow of the Moon, Heart of the Forest and If I Had a Glamour.
   - Keep the "(N)" count equal to the rows plus "+N more".
   - The legend and the main window still show the other three moons.
2. **Status cell colours** (row builder `c4`, `.mk .stx b`). The real `DrawStatus` draws the state word in Silver `#DDE3F0`, not bold. The reason is in Mist `#A9B2CC`, except Blocked and Locked-out reasons, which are in EclipseText `#D68AA8`.
   - Drop `b.style.color = INK[...]` and `font-weight:600`.
   - Colour the reason span as above.

   Today seven gold state words (Completed, In journal and Ready together) bury Ready. After the fix, the stripe and the moon carry the state, as in-game.
3. **Gold discipline** (Theme.cs summary; TablePane: "neutral selection wash with a 1 px ring (never gold)").
   - `.mk .tr.sel` → `background:rgba(221,227,240,.06); box-shadow:inset 0 0 0 1px #5C6584`.
   - `.mk .thd .sort` → `var(--silver)`.
   - `.mk .qv span.on` → silver text on `rgba(221,227,240,.08)`.
4. **Quick-view labels** (`.qv`). "Features" and "Level band" do not exist. Use `All · Unlocks · My level · Stalled · Story sidequests · Sprout mode`, with the last three as `.x2` so they drop when the window is narrow.
5. **Glyph column header.** Remove the "State" text. The column is `NoHeaderLabel` in TablePane, so leave the cell empty.
6. **Detail actions** (`.acts`, `renderDetail`).
   - With Lifestream installed, Teleport *replaces* Flag. Real order: Teleport, Pin, Show path, Link in chat, Copy coordinates, Open journal.
   - Replace "per client flags at 21:14" with the real string `Checked 21:14 · live`.
7. **Hero card** (`renderDetail`, `.hero`).
   - Remove the `MEAN` paragraph. In the hero it reads "…the detail pane shows the step", which is self-referential, and the plugin does not show it there. Keep it in the legend.
   - The state name currently appears twice. Make the headline `NAME[st]` in the state text tone and put only `q.det` ("step 4 of 6") under it.
8. **Hero halo** (`.mk .hero .hg::before`). A Tide-blue glow sits behind every medal. That implies a blue light source, and it is wrong behind the red Locked-out and gold Ready medals. Remove it. If a glow is wanted, use it for Ready only, warm: `rgba(242,210,122,.12)`.
9. **Tree orbit moons are inverted** (`drawOrbit`). At 11 % (Other Quests) the moon is almost full, and at 98 % (Main Scenario) it is almost dark. Help says the moon "fills with" the ratio. In the path's sweep flag, change `(f > 0.5 ? 0 : 1)` to `(f > 0.5 ? 1 : 0)`.
10. **Status bar** (`data-role="status"`). `DrawStatusBar` puts the overall gauge at the rail foot, not in the bar.
    - Remove the leading orbit and "65%".
    - Replace the bar's top border with the brass Gilt hairline that Moon Road uses.
11. **Not ImGui** (believability).
    - ImGui draws no window shadows: remove `box-shadow` from `.mk .todo`.
    - The game fonts have a single weight: remove `font-weight:600` from `.grp b`, `.tn.sec .nm`, `.hero .hl`, `.ban .t .n`, `.ban .spill` and `.dal .tn2 b`.
    - Set `.win`, `.todo` and `.dal` to `"Noto Sans"` 500 (Google Fonts), matching Dalamud's default Noto Sans Medium.
12. **Game HUD layout** (`.hud-bars`, `.todo`, `.hud-chat`).
    - FFXIV hotbars sit bottom-centre. At the right they cover the © SQUARE ENIX credit on both scenes (hidden at night, clipped by day). Move them to `left:50%; transform:translateX(-50%)` and keep the bottom-right credit clear.
    - At 150 % the overlay covers the hotbars. At 900 px it nearly touches the chat log. Anchor `.todo` higher (`top:5%`) and keep at least 12 px clearance from the chat and the bars in every tier, for example `.hud-chat{width:46%}` at ≤900.
13. **"Zone art" caption** (`.ban .bsrc`). It sits over bright cloud and is illegible. Add a top-right scrim (`radial-gradient(60% 80% at 100% 0, rgba(15,20,36,.6), transparent)`) or move it to the bottom-right, inside the existing dark gradient.
14. **Misleading data** (`Q`). "If I Had a Glamour" is shown as Locked out, "closed by: A Self-improving Man". As far as I know, the glamour unlock is open to everyone and cannot be locked out. Swap in a genuine exclusive choice, and check every state/blocker pair against the catalog, since the file claims "real quests".
15. **Dead variant** (preview `variants`). "Round 3 D (placeholder)" loads the round 2 files. On the owner's site that is a control that does nothing. Pass only real variants, and add round 3 when its files exist.
16. **The 400 % grid is only partly useful** (`renderLoupe`). About 85 % of each tile is magnified text, but the question it should answer is how the moon reads at small sizes. Changes:
    - Crop each tile to the glyph plus about 3 letters (`W ≈ 40*s`).
    - Put all 8 states side by side for each background.
    - Add a 14 px row (the overlay minimum, `PlayingGlyphSize`) and a 20 px row.
    - Include the 3 px state stripe and one selected-row wash.
    - Label each tile at 1×.

## For the glyph team (not mock fixes; the mock shows these faithfully)

- **Ready loses the value hierarchy at 16 px.** Its thin gold ring around a blue disc is quieter than Locked out's fully saturated red disc. In both the table and the overlay, the eye goes to Locked out first.
- **The state hues disagree between glyph and Theme.**
  - Locked out: the glyph is vermilion, but Eclipse and its stripe are rose `#B25C7F` / `#D68AA8`.
  - Completed: the glyph is grey-lavender with no gold, but `Theme.StateColor(Completed)` is Moon and its stripe is MoonDim gold. The legend shows a gold "Completed" under a grey moon.
  - Either the art or the Theme must change.
- **Material.**
  - In journal's gold ribbon casts no shadow onto the bezel lip.
  - Not checked has no bezel at all. That is fine if the "ghost" look is deliberate; otherwise it breaks the set's material.
  - Done-this-cycle is lit from the left and the others from the right. That is acceptable as phase meaning, not as a light-source error.

## Optional polish

- ImGui title bars are flat. `.w-title` → a solid `#1E2337` (NightRaised). Also set `--raised` to `#1E2337`; it is currently `#1A2033`.
- Show Expansion as a pill (as `DrawPill` does) and add the Rewards icon column the real table has.
- Drop the ☾/☀ text glyphs from `.hud-et`. In game, ET/LT live in the server info bar at the top right.
- Installer:
  - Keep the Installed badge inside the 64 px square (`right:0;bottom:0`, 20 px).
  - Draw the sort control as an ImGui combo, "Alphabetical ▾", with the "Sort By" label to its right.
  - Add group headers to the category list.
- Giver card: add the coordinates, which the real card shows.
- Tree: the ARR–DT chip truncates "Main Scena…". Drop the chip at this width.
- Tone down the CSS glows on tiny dots (`.pip`, `.rwc.uq::before`, `.st.on::after`). ImGui would draw them flat.
- Loupe caption: say it assumes the moons become textures rasterised at exact size. Today `MoonGlyph` draws with ImDrawList.

## Already right

- **Overlay.** It is faithful to `TodoOverlay`:
  - no title bar
  - the real "☾ Tsukimichi" header string
  - text outlined in the window colour
  - Mist hints
  - 85 % default opacity
  - "…" buttons on header and rows
  - brass-rule section captions and "+N more"
  - It is legible over both scenes.
- **Tokens and copy.**
  - Every colour token matches `GlyphTokens` (Night, Moon, Mist, Dusk, Eclipse, Gilt, Tide and the rest).
  - Rail tabs, search placeholder, "Read the journal", "All met", "N unique", "Zone art", the tree nodes and the legend copy (Help › Phases) are real strings.
  - The installer punchline matches the manifest.
- **Light.** One top light throughout: inset top highlights on the chrome, top-lit glyph bezels, and top-to-bottom gradients on the step bars. The icon's reflection column sits under the crescent, which is physically right.
- **Colour temperature.**
  - At night, the cool navy panel against warm lantern light, with gold as the warm counterpoint, is well balanced.
  - The installer's neutral Dalamud greys set the plugin's Night palette apart properly.
- **Layout.** No clipping. Container tiers drop the tree first at 150 % and at ≤1100, like the plugin's narrow tier. At 900 px the layout holds: the detail pane stacks and Job/Exp hide. Installer icon size is correct at 64 px.

---

## Re-review (after the builder's pass)

Renders: `shots/r3-*` plus my own 2x night and day renders (`sup-mock/r3n2x.png`, `r3d2x.png`) and crops of the detail pane, action bar, overlay, tree and credits. I judged the mock only, not the round 3 glyph art.

### Verdict: CHANGES REQUIRED (two items)

These fixes are confirmed in the renders: 1–5, 7–11, 13, 14, 15 and 16.

The overlay content and order are now real. "Pinned (9)" is 6 rows plus "+3 more". The status cells now use silver words, Mist reasons and Eclipse reasons for blocked and locked-out rows. The selection, the sort arrow and the active quick view are neutral. The quick views are real, and the "State" header is gone. The hero is clean, with no halo. The tree moons fill the right way. The status bar now starts at the counts and sits under a brass rule. There is no overlay shadow and no bold. "Zone art" is legible. Heads, I Win / Tails, You Lose is a genuine exclusive pair. The Crossroads blocker is accepted. The variant toggle is real.

The 400 % grid is now **genuinely useful**. It shows all eight states side by side at 14, 16 and 20 px, with the stripe, zebra rows and the selected wash, and a 1× strip under each tile. Sideways scrolling at 150 % is an acceptable deviation.

### Remaining required fixes

1. **Action bar** (`renderDetail`, `.acts`). My first-round fix 6 relied on an outdated Help string, and I apologise for that. In 1.10, `DetailPane.Actions.cs` and `DetailPane.cs` (around L1026) draw the bar in three rows:
   - **Row 1: labelled travel pills.** With Lifestream (and vnavmesh): `Go to giver` (FontAwesome LocationArrow), `Teleport` (PaperPlane) and `Walk to giver` (Walking). The first pill that can start wears the Primary tone: a Moon fill inside the brass edge, on Night. The others are raised fill with a brass edge. Today the mock draws Teleport as an icon-only round button, and its glyph reads as a four-point sparkle, not a paper plane.
   - **Row 2: round icon buttons, in this order:** Pin (thumbtack), Show path (route), Route to this (map-signs), Flag on map (shown here because Lifestream is loaded), Link in chat, Copy coordinates, Open journal (book-open), and "…". Report (bug) appears only when it is attached.
   - **Row 3:** the provenance line `Checked 21:14 · live`, as now.

   Raise `.acts` from one 36 px row to the three-row height.
2. **The © SQUARE ENIX credit is still lost in two tiers.**
   - At 1800 px, 100 %, Night: `scene-night.jpg` (1600×941) is taller than 16:9. `background-position: center 35%` crops its bottom edge, and the credit with it.
   - At 150 %: `.game{min-height:calc(480px * var(--s))}` makes the frame taller than 16:9. `cover` then crops the sides, so the credit is cut to "© SQUARE ENIX FINA" (1800 px Night) or lost entirely (900 px Day).
   - Fix: keep the game frame at the screenshot's own size. The game HUD does not scale with Dalamud, so drop `* var(--s)` from that `min-height`. Anchor the night scene at `center 100%`, and when the frame is taller than the image use `right 100%` (or simply `100% 100%` for both scenes). Then check that the credit is whole at 100 % and 150 %, at 1800 and 900, by day and by night.

### The font @import: confirmed OK

- **Host.** `@import url("https://fonts.googleapis.com/css2?family=Noto+Sans:wght@400;500&display=swap")` is the first rule of the fragment's `<style>` element. The stylesheet comes from fonts.googleapis.com and the woff2 files from fonts.gstatic.com, so both are on the allowed list.
- **Mid-document placement.** It still works. The rule that @import must come before all other rules applies per stylesheet, and each `<style>` element is its own stylesheet. A `<style>` in the middle of the body, or inserted later, still honours a leading @import.
- **Harmless if blocked.** If the import fails, the font stack falls back to Segoe UI or the system UI font and nothing breaks.
- **Keep it first.** If anyone adds a rule or a comment-wrapped rule above the @import, browsers will silently ignore the import. A comment alone above it is fine.

### Polish (not blocking)

- **Loupe font.** The canvas tiles draw Noto Sans without waiting for it to load, so the first grid can render in a fallback font. Wait on `document.fonts.load('500 13px "Noto Sans"')` before `renderLoupe` draws.
- **Installer font weight.** `.mk .dal`'s later `font:` shorthand (L232) resets the weight to 400 and overrides the 500 from L54. Drop the weight from the shorthand, or set the font only once.
- **Expansion pill.** The Expansion column still shows plain text rather than a pill, and the Rewards column is still absent. Both were optional.

---

## Final review

Renders: `shots/r4-*` (1800 and 900 px; night and day; 100 % and 150 %), `b-*`, `b-1800-credit.png`, `r4-acts*.png` and `r4-grid100.png`, plus my own crop of the scene at 900 px, 150 %.

### Verdict: APPROVED

**Action bar.** It now matches the 1.10 code (`DetailPane.Actions.cs`, `DetailPane.cs`):
- The travel pills are labelled: Go to giver, Teleport and Walk to giver. Primary is on the first pill, a Moon fill inside the brass edge, and the other pills are raised with a brass edge.
- The round icon buttons follow, in the real order: Pin, Show path, Route to this, Flag on map, Link in chat, Copy coordinates, Open journal, "…".
- The provenance line reads "Checked 21:14 · live".

**Credit.** The © SQUARE ENIX / FINAL FANTASY XIV credit is whole in all eight scene tiers (both widths, both scales, day and night). Anchoring at `100% 100%` keeps both compositions intact: the moon, the pavilion and the Tuliyollal skyline stay in frame.

**The unrequested 600 px min-height at ≤900 px is accepted.** At 150 % the overlay clears the chat log by about 12 px and the hotbars with more room. That is tight but clean, and it is a believable spot for a player to park the overlay.

**Polish confirmed:**
- The grid waits for Noto Sans.
- The weight on `.dal` is fixed.
- The `@import` is still the first rule in the style block (fonts.googleapis.com for the stylesheet, fonts.gstatic.com for the files).

### Non-blocking notes

- At 150 % in the narrow detail pane, the pills wrap onto two lines. The plugin's `ActionPillFit` would instead switch to the short labels "Go to" and "Walk", then to icon-only pills, and would keep one row. Use the short labels if the builder touches this again.
- The glyph-team findings from the first review still stand for the round 3 art. They are judged separately.

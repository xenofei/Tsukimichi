# Tsukimichi V3 — accessibility review (panel)

Date: 2026-09-28
Reviewer role: accessibility for games and overlays (colour vision deficiency, low vision, motor, cognitive; WCAG 2.2 applied to non-web UI)
Inputs reviewed: docs/feature-plan-v3.md; docs/design/ui-revamp-proposal.md (§4 tokens, §5 accessibility, §2–3 for sizes and motion); docs/design/mockups/main-window.html (studied as source, tokens and sizes read from the CSS); docs/design/glyphs/proposal.md, imgui-notes.md, glyphs-v2.png, glyphs-v2-zoom.png; Tsukimichi/Ui/Theme.cs, UiMetrics.cs, MoonGlyph.cs, TodoOverlay.cs, TutorialOverlay.cs, Strings.cs (state names), Strings.Panes.cs (tutorial text), TreePane.cs / TablePane.cs / MoonlitPane.cs / DetailPane.cs / MainWindow.cs (items and context menus), Tsukimichi.Core/Ui/ScaleMetrics.cs (scale bounds).
Method: every number below was computed, not eyeballed. Contrast is WCAG 2.x relative luminance. Colour-vision simulation is Machado, Oliveira & Fernandes (2009) at severity 1.0 applied in linear RGB (deutan, protan, tritan); achromatopsia is the luminance channel. Pair distinguishability is CIEDE2000 (ΔE) on the simulated colours plus the luminance ratio. Rule of thumb used: on a 10–20 px glyph, ΔE under ~10 is "same colour at a glance", under 5 is "same colour"; the WCAG 3 : 1 non-text ratio is the bar for any edge that must be seen.
Scope note: no repo file other than this report was written.

---

## 0. Verdict in one paragraph

The V3 design is a large accessibility step up from 0.5.0: the Shadow/Veil disc plus a rim fixes the invisible dark side, the halo gauge gives three encodings of progress, the token table is mostly honest about contrast, and "shape before colour" is the right principle. Four things undermine it. (1) The colour-blind analysis in §5.2 is aimed at the wrong pair: gold vs silver survives deuteranopia, protanopia and tritanopia comfortably (ΔE 26–31), and it is **achromatopsia** where they collapse (ΔE 3.2, luminance ratio 1.14 : 1). The pair that actually fails for red-green CVD is **Eclipse vs Dusk/Silver** (Foreclosed's ring becomes a neutral grey: ΔE 14.5 deutan, 11.6 protan, 5.8 achromatopsia), and the shape that is supposed to carry Foreclosed (a 2 px notch) is too small to do it. (2) The 3 px state stripe is claimed to survive colour removal; it does not — every state's stripe has the same position and length, only Foreclosed's is dashed. (3) The halo gauge's *track* (Veil @ 0.55) is 1.54 : 1 against Night, so the unfilled part of the ring, which is what tells 90 % from 100 %, is below the non-text threshold, and at the 16 px sizes the arc stroke is 1.5 px, below what a low-vision player can resolve at desk distance. (4) The Shift+click gate for Moonlit verdicts, the right-click-only actions, and Ctrl+1..4 / F shortcuts each exclude a real population (controller and virtual-mouse players, one-handed players, and anyone whose hotbar 2 is on Ctrl+digits). All four are cheap to fix at plan level and the fixes are listed against the tasks they change.

---

## 1. Ranked findings

Severity: **A** = excludes a population or makes a state unreadable; **B** = fails a WCAG threshold the plan relies on, or a wrong claim in the proposal; **C** = friction, polish.

### A1. Foreclosed is colour-only for red-green CVD; its shape channel is too weak to carry it — T9, T15, T16

Simulated tokens (hex after simulation):

| Token | true | deutan | protan | tritan | achromatopsia |
|---|---|---|---|---|---|
| Eclipse (Foreclosed ring, stripe) | #B25C7F | **#7E7D7D** | **#676E80** | #BE5769 | #787878 |
| EclipseText | #D68AA8 | **#A5A5A6** | **#9398A9** | #E18794 | #A0A0A0 |
| Bruise (Foreclosed disc) | #645574 | #525B73 | #505A75 | #625960 | #5B5B5B |
| Dusk (rims, Blocked stripe) | #7C86A8 | #7985A7 | #7C89AA | #718C91 | #878787 |
| Veil (unlit disc) | #4A5270 | #46526F | #495571 | #40585C | #535353 |
| Silver (Blocked ring) | #DDE3F0 | #DEE2F0 | #DFE4F1 | #D9E5E7 | #E3E3E3 |

Pair distinguishability, ΔE2000 / luminance ratio:

| Pair | true | deutan | protan | tritan | achroma |
|---|---|---|---|---|---|
| Eclipse ring vs Dusk rim (Foreclosed vs Ready/DoneThisCycle/Unknown rims) | 25.3 / 1.23 | **14.5 / 1.12** | **11.6 / 1.45** | 34.0 / 1.24 | **5.8 / 1.23** |
| Eclipse ring vs Veil disc it sits on | 27.4 / 1.73 | 21.5 / 1.91 | **10.3 / 1.47** | 35.3 / 1.72 | 13.7 / 1.73 |
| Bruise disc vs Veil disc (Foreclosed vs Blocked body) | 9.3 / 1.13 | **4.0** | **2.2** | 17.6 | **2.8** |
| Eclipse ring vs its own Bruise disc | 19.3 / 1.53 | 17.6 | **8.2 / 1.34** | 23.3 | 10.9 |
| EclipseText vs Mist (Foreclosed next-step text vs ordinary secondary text) | 23.8 / 1.23 | **11.2 / 1.15** | **8.7 / 1.38** | 29.9 | **5.2 / 1.23** |
| Eclipse vs Silver ring (Foreclosed vs Blocked ring) | 39.2 / 3.45 | 29.2 | 34.8 | 38.3 | 30.8 |
| Eclipse vs Moon ring (Foreclosed vs Accepted ring) | 52.0 | 34.8 | 45.6 | 29.3 | 27.8 |

Reading: for a deutan or protan player (about 6 % + 1 % of men), the Foreclosed moon is a grey disc with a grey rim, which is exactly what Ready's dark side, DoneThisCycle's dark side and Unknown's dashed ring look like. The Bruise disc, introduced to make Foreclosed look "bruised", contributes nothing under CVD (ΔE 2–4 against Veil) and only 1.13 : 1 even for normal vision. What is left is the notch: `max(0.30 r, 2 px)` of Night bitten from a ring that is `1.2 · clamp(0.10 r, 1.25, 3)` = 1.5 px thick at r = 7.5 (table rows) and at r = 6 (Dense). A 2 px bite in a 1.5 px ring on an 11–12 px glyph is at the anti-aliasing floor the glyph proposal itself uses to reject the calendar-pip idea (§2 C).

Fix (T9, glyph geometry):
- Give Foreclosed a shape that is legible at r = 6: a **diagonal bar** through the disc from upper-left to lower-right (width `max(2, 0.22 r)`, Eclipse, same angle as the universal "no" sign), keeping the ring. At r ≥ 12 the notch can stay as decoration. Alternative that keeps the lunar metaphor: an **eclipse corona**, i.e. the disc drawn at 0.78 r inside a ring whose gap to the disc is 0.12 r; the double-edge silhouette is unique among the eight.
- Drop Bruise or keep it only as a large-glyph tint (≥ 24 px); at row sizes it costs a token for no signal.
- Text: the mockup already writes **"Locked by Gridanian Envoy"** with the word first; make that a rule in T15/T16 (`StateTextColor` never carries meaning without the state word in the same cell), so EclipseText → grey is harmless.
- Stripe: keep the dashed Foreclosed stripe (good), see A3 for the rest.

Not recommended: shifting Eclipse toward red (#E0567A) to "survive deuteranopia". It raises ΔE vs Dusk to 27 for deutans but drops Eclipse vs Moon to 24 (the red goes yellow-brown under deutan simulation: #979077) and it is 0.2 ΔE from Dusk under achromatopsia. There is no hue that is distinct from both gold and blue-grey for all three dichromacies; the shape has to do it.

### A2. Gold vs silver collapses under achromatopsia, and §5.2 names the wrong condition — T9, ui-revamp-proposal §5.2

| Pair | true | deutan | protan | tritan | achroma |
|---|---|---|---|---|---|
| Moon vs Silver (lit gold vs lit silver) | 30.2 / 1.14 | 31.0 | 31.1 | 25.7 | **3.2 / 1.14** |
| Moon vs SilverDeep (shading stop) | 34.4 | 35.6 | 35.0 | 28.9 | **4.6** |
| Moon vs Dusk (gold ring vs Dusk rim) | 45.1 / 2.45 | 46.4 | 44.6 | 40.1 | 22.2 / 2.45 |
| Silver vs Dusk (Silver ring vs Dusk rim) | 26.6 / 2.80 | 26.8 | 25.9 | 25.7 | 25.3 / 2.80 |
| Moon vs Mist (gold "Ready ·" text vs Mist) | 36.9 / 1.44 | 38.1 | 37.2 | 32.0 | **8.8 / 1.44** |

Gold and blue-white differ along the yellow–blue axis, which all three dichromacies keep, so §5.2's sentence "Gold vs silver is the weakest pair for deuteranopia" should read "for achromatopsia and for monochrome displays/streams". For those players (rare, ~1 in 30,000, but also anyone watching a greyscale stream or using a Windows colour filter set to greyscale) the gold/silver split is gone and the moons are told apart by:

- Ready (half, glow, Dusk rim) vs ReadyOnOtherJob (half, Moon ring): glow vs ring. The Ready glow is three discs at α 0.05–0.15; on an 11 px glyph over Night it sums to a barely-there 2 px haze. Under achromatopsia the Moon ring (#D5D5D5) and the Silver ring of Blocked (#E3E3E3) are the same ring; ReadyOnOtherJob reads as "Blocked with a lit half".
- Accepted (waxing gibbous, Moon ring) vs DoneThisCycle (waning gibbous, Dusk rim): a **mirror image** plus ring luminance. Left/right mirror discrimination on small symbols is a known weak cue (it is what makes b/d hard for dyslexic readers), and the owner already found Accepted "reads too much like the full moon".

Fix (T9):
- Since the owner is redrawing Accepted anyway, use the redesign to make Accepted's silhouette non-mirror: e.g. **first quarter plus a small filled dot at the centre of the dark side** ("the quest is in your journal") or a gibbous with a **bracket-shaped ring segment** on the lit side only. Anything that is not the mirror of DoneThisCycle.
- Make the Ready glow a real ring at small sizes: below r = 9 replace the three discs with one `AddCircle` at 1.25 r, 1 px, Moon @ 0.35 (a "halo ring" that survives on Night and on the Dalamud default background).
- Add the optional **High-contrast / colour-blind glyph set** (see §2 below) that adds a second in-disc mark per gold state (dot, tick) so no pair depends on gold-vs-silver.

### A3. The 3 px state stripe does not "survive colour removal" — T15

§2.4 says "Because the stripe uses position and length, not only hue, it survives colour removal." Every stripe in the mockup is the same 3 px × full row height at x = 0; only Foreclosed is dashed. Under achromatopsia Moon (#D5D5D5) vs Silver (#E3E3E3) is 1.06 : 1; the Blocked stripe (Dusk @ 0.6 on Night ≈ 2.6 : 1) is the only one that differs in luminance. The stripe is therefore a colour-only duplicate of the moon.

Fix (T15): either drop the claim (the moon and the mandatory state word in Next step are the redundant channels) or make the stripe encode by **pattern**: Ready solid full height; Accepted two segments (top and bottom third); DoneThisCycle solid at 60 % height, centred; Completed none; Blocked none; Foreclosed dashed; Unknown dotted 1 × 1. That gives the row a non-colour actionability cue that is readable while scrolling. Either way, keep "Ready ·", "Done", "Locked", "Veiled", "Level", "Rank" as the first word of Next step for every state (it is already so in the mockup; make it a rule and a test).

### A4. Halo gauge: the track fails 3 : 1 and the 16 px arc is below low-vision acuity — T10, T11, T12

| Element | Contrast | Notes |
|---|---|---|
| Track Veil @ 0.55 on Night | **1.54 : 1** | on NightRaised 1.44 |
| Track Veil @ 1.0 on Night | 2.38 : 1 | still under 3 : 1 |
| VeilLine #5C6584 on Night | 3.19 : 1 | on NightRaised 2.68 |
| Dusk on Night | 5.09 : 1 | on NightRaised 4.28 |
| Umbra core (f = 0) on Night | 1.47 : 1 | the "empty" node's core is invisible; the track is its only outline |
| Moon arc vs Veil @ 0.55 track | 8.10 : 1 | fine |

The unfilled track is the part of the gauge that distinguishes 90 % from 100 % and 3 % from 0 %. At 1.5 : 1 it is a colour a sighted user infers rather than sees; a low-vision user sees a floating gold arc of some length with nothing to compare it to. WCAG 1.4.11 asks 3 : 1 for the parts of a graphic needed to understand it.

Sizes (proposal §3.2): stroke `max(1.5, 0.16 R)` → 1.5 px at the 16 px tab-strip and Dense sizes, 1.84 px at the 23 px default tree glyph, 2 px only from R ≥ 12.5 (25 px). At 60 cm on a 96 dpi panel one pixel is ~1.5 arcmin; a 20/70 player (the usual "low vision" threshold) resolves ~3.5 arcmin, i.e. needs ≥ 2.3 px of stroke; on a 27" 1440p panel ≥ 2.7 px; on a 4K 27" panel ≥ 4 px. The 16 px halo is therefore unreadable as a gauge for that population at any monitor, and the 23 px default is marginal at 1440p.

Fix:
- T10: track colour **VeilLine at full alpha on Night** (3.19 : 1) and **Dusk @ 0.8 on cards** (≈ 3.6 : 1); stroke `max(2, 0.18 R)`; ε floor unchanged (it already gives a 2.4 px pip at 16 px, which the thicker stroke keeps legible).
- T10: at f = 0 draw the core in **Shadow with a Dusk rim** (as the state moons do), not Umbra, so an empty node still has a visible disc.
- T11/T12: floor the tree halo at **24 px absolute** (not 16) regardless of IconScale 0.8; in the tab strip and status bar (16–18 px) draw **track + arc only, stroke 2 px**, and put the number beside it (the mockup already does "62%"). Below 16 px never draw a gauge; draw the percentage.
- T11: keep the mini bar (44 × 3) from glyph proposal §4 in *every* density, not only Dense; a horizontal bar is the most acuity-tolerant encoding of the three (it grows in length only).

### A5. Shift+click as the gate for Moonlit verdicts excludes controller, virtual-mouse and one-handed players — T7 (decision 2)

Who cannot chord: controller players using FFXIV's virtual mouse (no modifier while clicking), one-handed players, players using head/eye pointers or switch access, and anyone with tremor who cannot hold Shift and click precisely. Windows Sticky Keys does latch Shift for a following click, so keyboard-only players with motor impairments are covered by the OS, but nobody else is. The tooltip "Hold Shift and click" tells those players the feature is not for them. Note also that the current Mark-as-unique flow (DetailPane.cs:538) already opens a popup with a note field and a Confirm button — that is a two-step guard; Shift adds a third step only for the people who can press it.

Fix (T7):
- Make the **confirm popup the guard** for both actions (Moonlit's "Not unique (hide)" currently applies instantly from a context menu with no popup — give it the same popup, defaulted to the note field, Enter confirms, Esc cancels).
- Offer **hold-to-confirm** as the pointer-only path for the button itself: press and hold 600 ms; a Moon arc fills clockwise around the button (reuse `MoonGlyph.DrawHalo` at R = 10); release before completion cancels; `ReduceMotion` swaps the arc for a text countdown. This is chord-free, works with any pointer including the virtual mouse, and is cancel-safe for tremor.
- Keep Shift+click as a **bypass** for power users (skips the popup), not as the requirement. `ModifierGate` in T7's tests becomes `ConfirmGate` with three strategies (popup, hold, chord-bypass).
- Never make Shift the only path for any action. Grep rule for the bug-hunter pass: `io.KeyShift`/`KeyCtrl` may only appear inside an `||` with another path.

### A6. Right-click-only actions have no keyboard or controller path — T15, T16, T17, T7, TodoOverlay

Actions that exist only in a `ContextPopupItem`: table rows — Pin/Unpin, Copy name, Show path (TablePane.cs:401–); Moonlit rows — Show in Journal, Not unique (hide), Restore shipped verdict (MoonlitPane.cs:449–); Todo overlay rows — Reveal in Tsukimichi, Flag, Teleport, Link in chat (TodoOverlay.cs:DrawRowMenu). ImGui keyboard/gamepad nav has no "context menu key"; there is no Menu-key or Shift+F10 handling in the codebase. Controller players on the virtual mouse can right-click only if their pad has a right-click bind, and players with switch access cannot.

Fix:
- T16: the sticky **action bar** is the right place; it already gets Flag, Journal, Link, Copy coordinates, Teleport. Add **Pin** and **Show path** there (two more 28 px buttons, or an overflow "⋯" button that opens the same menu with a left click and is keyboard-focusable).
- T7: the Moonlit verdict line in the detail pane (§2.5 item 5) must carry "Not unique (hide)" and "Restore" as pills; the context menu duplicates them.
- TodoOverlay (T6 or T17): a small "⋯" at the row's right end that opens the row menu on left click; also the row's tooltip should say "right-click or ⋯ for more".
- T17: handle `ImGuiKey.Menu` and Shift+F10 on a focused row → `OpenPopup` of the same context menu (ImGui allows opening a popup by id from code).

### A7. Ctrl+1..4 and bare F collide with the game's default keybinds — T17

FFXIV's default keyboard binds put hotbar 2 on **Ctrl+1 … Ctrl+0**. Dalamud only swallows keys for the game while an ImGui text input wants them (`io.WantTextInput`); ordinary focus on an ImGui window does not block the game's keybinds. Ctrl+1 in the plugin window will switch the tab **and** fire hotbar 2 slot 1. A bare letter (F) is worse: many players bind single letters. This is a safety issue (firing an action mid-combat) as much as an accessibility one.

Fix (T17): keep Ctrl+F (only when the window is hovered or focused and the game is not in a text field) and Esc-to-clear; drop Ctrl+1..4 and F as defaults; expose "Tab shortcuts" and "Filters shortcut" as **configurable, off by default** in Settings › Display, with a warning line that the game also sees them. Document the collision in Help → Commands.

### A8. Todo overlay over bright scenes and on light Dalamud themes — T6, T17 (and the Todo item in TodoOverlay.cs)

The overlay uses the user's Dalamud `WindowBg` (not Night) at `TodoOverlayOpacity` 0.2–1.0, default 0.85, and its hint text is Dusk with no shadow.

| Scene behind the panel | opacity | Dusk hint | Moon header | Silver name |
|---|---|---|---|---|
| Coerthas snow (~#D8DEE6), Night bg | 0.85 | **3.53** | 8.65 | 9.89 |
| same | 0.50 | **1.21** | **2.97** | **3.39** |
| same | 0.20 | **1.76** | **1.39** | **1.59** |
| Dalamud light theme (~#E8E8E8), opaque | 1.00 | **2.94** | **1.20** | **1.05** |

Fix:
- T6 (already touching TodoOverlay for font scale): raise `MinOpacity` to **0.6** and draw every overlay text with a **1 px Night drop shadow** (`AddText` twice; cheap) whenever opacity < 0.9, so the row reads over snow, sand and sky. Hint text **Mist**, not Dusk (Dusk fails AA at 0.85 over any bright scene).
- T17 (Todo on `Chrome`): the overlay should push **Night chrome when the main window does** (it is the product's identity and the only reliable dark surface); on Classic layout keep the host theme but detect a light `WindowBg` (luminance > 0.5) and switch the header from Moon to MoonDim and text to the host `Text` colour.
- Motor: a single left-click on a row **flags the map** (a game action, no undo) and rows stay clickable when Locked. For tremor and virtual-mouse users make the flag action **double-click or the ⋯ menu**, and offer "Locked = click-through" (Dalamud `AllowClickthrough`) as a separate toggle.

### B1. Contrast the proposal skipped (computed; text 4.5 : 1, non-text 3 : 1) — T9, T13–T16

Every text token on every proposed surface, including the alpha fills the proposal did not compute:

| Token | Night | Sunken | Raised | Hover | Selection (Moon .12 on Night) | Active (Moon .16 on Raised) | Pressed (Moon .26 on Raised) | MSQ pill (Moon .10 on Sunken) | Zebra (Veil .10) |
|---|---|---|---|---|---|---|---|---|---|
| Silver | 14.24 | 14.85 | 11.98 | 10.57 | 11.01 | 8.06 | 6.12 | 12.27 | 13.41 |
| Mist | 8.66 | 9.03 | 7.28 | 6.43 | 6.70 | 4.90 | **3.72** | 7.46 | 8.16 |
| Dusk | 5.09 | 5.31 | **4.28** | **3.78** | **3.94** | **2.88** | **2.19** | **4.38** | 4.79 |
| VeilText | 6.01 | 6.26 | 5.05 | **4.46** | 4.64 | **3.40** | **2.58** | 5.17 | 5.65 |
| Veil | 2.38 | 2.49 | 2.00 | 1.77 | 1.84 | 1.35 | 1.02 | 2.05 | 2.24 |
| Moon (gold text) | 12.46 | 12.99 | **10.48** | 9.25 | 9.63 | 7.05 | 5.35 | 10.73 | 11.73 |
| MoonDeep | 9.25 | 9.64 | 7.78 | 6.86 | 7.15 | 5.23 | **3.97** | 7.97 | 8.71 |
| MoonDim | 6.36 | 6.63 | 5.34 | 4.72 | 4.91 | **3.60** | **2.73** | 5.48 | 5.98 |
| EclipseText | 7.03 | 7.33 | 5.91 | 5.22 | 5.44 | **3.98** | **3.02** | 6.06 | 6.62 |
| Eclipse | 4.13 | 4.30 | 3.47 | 3.06 | 3.19 | 2.34 | 1.77 | 3.56 | 3.89 |

Specific answers to the questions asked:
- **Gold text on NightRaised: 10.48 : 1**, AAA. Gold text is safe everywhere except as MoonDim on tinted fills (3.60 on Active, 2.73 on Pressed), which is exactly where §4.3 proposes MoonDim ("badges on hover"). Use MoonDeep there (5.23 on Active) or keep Moon.
- **10–11 px captions**: the mockup sets `.cap`, `.pct`, `.thead`, `.status` at 11–11.5 px, `.rb` (Ready badge) at 10 px, `.prov` at 10.5 px, the Filters count badge at **9.5 px**. Contrast is fine for Mist captions (7.28 on Raised) but these sizes are under the proposal's own 12 px floor (§6.3) and well under the 14 px (~10.5 pt) where WCAG's 4.5 : 1 applies without the "large text" relief. In ImGui at the 17 px Dalamud default × 0.85 the caption is 14.5 px at UiScale 1.0 and **13 px at UiScale 0.9**; the badge count has to be ~9 px to fit a 14 px circle. Fix (T13/T14): badge diameter 18 px minimum with the count at 0.85×; Dusk captions (provenance, version, expansion pills) become **Mist**; nothing under 12 px absolute, and the `AddText` scaled fallback (which blurs) is replaced by the Axis14 handle *before* 0.8.0 ships to low-vision users, not "in increment 5".
- **Dusk on Selection fill 3.94, on Hover 3.78, on Active 2.88** — Dusk must not be used for any text inside a selected/hovered/active row (the current code uses Dusk for the count in tree rows and the hint in Todo rows). §5.1 says this for hover; extend it to selection and active.
- **VeilText on Hover 4.46** — a hovered Unknown row's "Veiled · achievements not loaded" fails by a hair. Use Mist for the text and reserve VeilText for the ring and pill.
- **Version string in Veil (2.49 on Sunken)** — "disabled" does not apply; it is the only place the version is shown. Use Dusk (5.31).
- **Destructive button** (`PushDestructiveButton`): Silver on Eclipse @ 0.75 over Night = 5.02 (ok); **hovered Silver on Eclipse = 3.45** (fails). Use the active colour `Lerp(Eclipse, Night, 0.25)` for hover as well.
- Non-text: Shadow disc 1.89 on Night / 1.59 on Raised / 1.40 on Hover — documented as "by design" with the Dusk ring supplying the boundary; that ring is 4.28 on Raised and 3.78 on Hover, so it holds. But note the two documents disagree: ui-revamp §2.7 says unlit = **Shadow + `max(1.5, 0.12 r)` Dusk ring**, glyph proposal §3.1/§3.4 says unlit = **Veil + `clamp(0.10 r, 1.25, 3)` Dusk rim**, and T9 lists both ("UnlitDisc → Veil" and a "Shadow" surface token). Pick one in T9. Numbers: Dusk rim vs Veil disc 2.13, vs Shadow 2.70 (the inner edge is a soft edge either way; what matters is rim vs background, 5.09 on Night). Prefer **Shadow** for the disc (Veil vs Night 2.38 is close enough to the 3 : 1 that anti-aliasing makes it a grey smear, and Veil is also the disabled-text colour, which is a semantic collision).
- Selection fill 1.29 on Night and Active fill 1.49 on Raised are below 3 : 1, but both are backed by a ring/bar (Moon @ 0.45 outline, 2–3 px Moon bar), which pass. Keep the bar mandatory; never ship a fill-only selected state (the Moonlit selection bug in 1.3 is a reminder that fills alone get lost).

### B2. The eight-state vocabulary is overloaded by a second moon vocabulary — T16, T8, Help

The requirements card (§2.5 item 3) introduces "full = met, new with a Silver ring = unmet, gold first quarter = the next step"; reward tiles use "10 px full moon = obtained"; the character chip and status bar use "veiled moon = snapshot"; the toolbar sync glyph uses Completed/Unknown moons for "live/offline". That is four additional meanings for glyphs that already mean Completed, Blocked, Ready and Unknown in the row next to them. For a cognitive-load review this is the biggest issue in the proposal: a first quarter moon in the requirement list means "do this next", in the table it means "you can accept this quest", and in the tree core it means "about half done".

Fix:
- T16: requirement rows use **check / cross / arrow** marks (FontAwesome Check in Moon, Times in Dusk, ArrowRight in Moon for "next") — the tutorial already describes them as ✓ ✗ ▶ and the owner's players already know them. Reward tiles use a check badge, not a moon. The sync/live indicator uses the pip and the words "live" / "snapshot 21:14", not a moon.
- Rule for T9's glyph debug window and the Help legend: **a moon means a quest state or a completion fraction, nothing else.**

### B3. Names: "Foreclosed", "Veiled", "Unknown", "Moonlit", "Done this cycle" — Strings, T1, T8, T16, tutorial

Code: `Strings.StateName` → Ready, Ready on another job, Accepted, Blocked, Done this cycle, Completed, **Foreclosed**, **Unknown**. Mockup: "Locked by …", "**Veiled** · achievements not loaded", "Blocked" pill. Tokens: Eclipse, Veil, Umbra, Bruise (internal, fine). So the same state is "Unknown" in chips and tooltips and "Veiled" in the next-step text, and Foreclosed is "Foreclosed" in the pill and "Locked" in the text. Two names per state doubles the vocabulary a new player has to learn; "Foreclosed" is legal jargon that most non-native English speakers will not parse; "Done this cycle" needs "cycle" explained; "Moonlit" as a tab name gives no clue it means unique rewards until the tutorial's 11th step.

Fix (one Strings change, propagated by T8/T16 and the tutorial):
- One display name per state, used everywhere (pill, chip, tooltip, next-step first word, tutorial, help): Ready · Ready on another job · Accepted · Blocked · Done today (or "Done until reset") · Completed · **Locked out** · **Not checked**. Keep the poetic names as *subtitles* in the Help legend and tooltips: "Locked out (foreclosed): a choice on this character closed it for good — Grand Company, starting city, …"; "Not checked (veiled): the plugin cannot read this yet".
- Tab: "Moonlit" stays as the brand but the tab label in the strip can be "Moonlit · rewards" (the vertical strip has the width), and the tooltip "Quests with unique rewards".

### B4. Target sizes at UiScale 0.9 (Dalamud global 1.0) — T13, T14, T15, T16, T11

WCAG 2.5.8 (AA) minimum 24 × 24 px, with the spacing exception; 2.5.5 (AAA) 44 × 44. Proposed logical sizes × 0.9:

| Target | logical | at 0.9 | verdict |
|---|---|---|---|
| Round icon buttons (help, tutorial, settings) | 26 | **23.4** | fails 24 (by 0.6 px), adjacent buttons 4 px apart so the spacing exception does not save it |
| Action bar buttons | 28 | 25.2 | ok |
| Search clear × (InvisibleButton) | 20 | **18** | fails; it sits inside another target (the text field) so the exception cannot apply |
| Segmented-control segments | h 26 | 23.4 | fails (height) |
| Filters button | 26 | 23.4 | fails |
| Chip (whole chip is the clear button) | h 22 | 19.8 | passes on the spacing exception (nearest target ≥ 24 px away vertically) |
| Tab strip rows | 30 | 27 | ok |
| Tree rows: Dense / Comfortable | 24 / 28 | **21.6** / 25.2 | Dense fails (rows are contiguous, no exception) |
| Table rows: Dense / Comfortable | 24 / 32 | **21.6** / 28.8 | Dense fails |
| Tree chevron (8 px triangle) | 8 | 7.2 | ok only because `TreeNodeEx` with `OpenOnArrow` makes the arrow *slot* (font size + 2 × FramePadding ≈ 24 px) the target; keep that flag, never replace the chevron with an `InvisibleButton` of its drawn size |
| MSQ pill in status bar | h 18 | 16.2 | passes on the spacing exception (only target in its neighbourhood) |
| Reward tiles | 44 | 39.6 | ok |

Fix: introduce `UiMetrics.MinTarget = max(Px(26), 24f)` (absolute floor) and use it for every icon button, segment height and the search ×; Dense row height floors at 24 absolute (`max(Px(24), 24)`); the search × becomes 24 × 24 and the pill 28 tall. Document in Settings that UiScale 0.9 keeps targets at 24 px.

### B5. Motion — T13, T17, T12

What is proposed is mostly within limits: hover/selection lerps ≤ 180 ms, chevron 140 ms, gauge fill 500 ms; the reveal pulse is two rings in 900 ms (2.2 flashes/s, small area, under the 3 Hz seizure threshold and far under the 25 % screen-area rule). Two gaps:

- The **live pip breathes forever** (1.6 s period). WCAG 2.2.2 requires a way to pause any motion that lasts over 5 s; `ReduceMotion` is that mechanism but it defaults to off and lives in 0.8.0 (T13), while the pip ships in 0.7.0 (T12 "a live pip"). Fix: T12 ships a **static** pip; T13's `Motion.Breath` is the only thing that makes it move; `Configuration.ReduceMotion` defaults from the OS (`SystemParametersInfo(SPI_GETCLIENTAREAANIMATION)` via P/Invoke, or `Windows.UI.ViewManagement.UISettings.AnimationsEnabled`) on first run, and can be overridden.
- **Hover lerps while scrolling** a 5 k-row table leave a wake of fading rows behind the cursor (180 ms out). Fix (T13): skip hover motion on frames where `io.MouseWheel != 0` or the clipper's first visible row changed.
- Also: the tutorial's target glow and the Ready halo are static — good; keep the moon hover glow (0.4× halo) static, not pulsing.

### B6. Text scaling limits — ScaleMetrics, T6, T14

`UiScale` is clamped to 0.9–1.6 and Dalamud's global scale multiplies it, so 200 % (WCAG 1.4.4) is reachable only by combining both. Concerns at the top end: the toolbar's fixed pieces (search 280 + segmented ~230 + filters + character chip 240 + right cluster ≈ 900 logical) exceed 1100 px at ≥ 1.25 and the proposal's only fallback is collapsing presets into a "Presets ▾" pill; at 1.6 the 1100 × 700 default window becomes 1760 × 1120, larger than a 1080p screen minus the game HUD. Fix (T14): the toolbar reflows to **two rows** (search + presets / character + cluster) below ~1000 px available width instead of hiding presets; the default window size is `min(1100 × Scale, viewport − 2 × 48)`; captions never go below 1.0× when `UiScale < 1.0` (already in §6.3, keep it). The tutorial card (`TutorialOverlay`) scales with `ImGuiHelpers.GlobalScale` only; a low-vision player who set UiScale 1.6 gets a 1.0× card. Fix (T14 acceptance "tutorial run to the end"): apply `UiMetrics.ApplyFontScale()` in the card and size `CardWidth` by `UiMetrics.Scale`.

### C1. Tutorial pacing and controls — TutorialOverlay, Strings.Tutorial, T7, T14

- 15 linear steps at 30–50 words each (~600 words), no way to jump, no chapter structure; "Not now" on the first-run card sets `TutorialCompleted = true`, so it means "never", which the label does not say.
- Keyboard: Back/Next/Skip are buttons the card focuses, so ImGui nav works when Dalamud has it enabled; there are no accelerators (Enter, Backspace, arrows); Esc skips only when the card is focused.
- Content that will be wrong after V3: step 7 lists three moons ("full is completed, first quarter is ready, new is blocked") — there are eight, and the state legend is the single most useful thing to show a CVD player; step 8 says "✓ or ✗ … ▶" but T16 replaces them with moons (see B2, keep the ✓ ✗ ▶); step 2 exposes "150 ms" (implementation detail); step 12's Flight body is the longest.

Fix:
- Structure as three chapters with a dot strip the player can click: **Find** (search, filters, chips, tabs), **Read** (tree, table, **legend of the eight states with names and shape hints**, requirements, path, giver), **Beyond** (Moonlit, Characters, Flight, help). "Skip chapter" per chapter; "Not now" becomes "Later" (re-offered next session, up to 3 times) with a separate "Don't offer again" checkbox.
- Bodies ≤ 35 words; drop timings.
- Accelerators while the tour is active regardless of focus: → / Enter = Next, ← / Backspace = Back, Esc = Skip (the main window's own Esc is already suspended during the tour, so this is safe).
- The tour opens the filter panel (step 3) and leaves it open; restore `FilterPanelOpen` on Skip/Done.
- T7's "help and tutorial text updated": the verdict step must describe the popup/hold path first and the Shift bypass second.

### C2. Cognitive load of the tree and table rows — T11, T15

Each Comfortable tree row will carry: chevron, halo (arc + core moon), name, expansion pill, Ready badge, mini bar, done/total or percentage — up to seven elements in 28 px. The mockup's row 4 in the glyph sheet shows the calmer version (halo, name, pill, bar, count). Fix: the Ready badge and the expansion pill are mutually exclusive by default (badge only when Ready > 0, pill only on hover or in a tooltip), and the mini bar replaces the halo's core moon at < 24 px rather than adding to it. The proposal's "never more than two text weights per row" is good; extend it to "never more than one non-text encoding of the same number per row at Dense".

---

## 2. Concrete deliverables the plan should add

### 2.1 Token changes (T9)

| Token | Proposed | Change to | Why |
|---|---|---|---|
| Halo track | Veil @ 0.55 | **VeilLine #5C6584 @ 1.0** on Night; Dusk @ 0.8 on cards | 3.19 : 1 vs 1.54 |
| Halo stroke | `max(1.5, 0.16 R)` | `max(2, 0.18 R)` | low-vision acuity at 60 cm |
| Halo empty core | Umbra | Shadow + Dusk rim | empty node still has a disc |
| Unlit disc | Shadow (ui-revamp) vs Veil (glyphs) | **Shadow #3A4363**, one answer in T9 | avoid the Veil/disabled semantic collision |
| Bruise | new | drop (or ≥ 24 px only) | ΔE 2–4 vs Veil under CVD |
| Version string | Veil | Dusk | 2.49 → 5.31 |
| Provenance / expansion pill text | Dusk | Mist | 10–11 px text needs 4.5 : 1 on every surface |
| MoonDim on tinted fills | MoonDim | MoonDeep | 3.60 → 5.23 on Active |
| Destructive hover | Eclipse | `Lerp(Eclipse, Night, 0.25)` | Silver 3.45 → ≥ 5 |
| Ready glow < r 9 | 3 discs α .05–.15 | 1 ring at 1.25 r, 1 px, Moon @ 0.35 | visible on Night and Dalamud default |

### 2.2 Optional "High contrast & colour-blind" palette (new setting in T9 / Settings › Display; one enum, `GlyphPalette.Standard | HighContrast`)

Not a hue swap (no hue works for all dichromacies, see A1) but a **shape-redundancy + luminance** set:

| State | Standard | High contrast adds |
|---|---|---|
| Completed | full gold | full **white** disc (#FFFFFF, 18.3 : 1) with a small centred Night dot — luminance-unique |
| Accepted | redrawn gibbous + gold ring | + centred Night dot on the lit side (in-journal mark) |
| Ready | half + glow | + 1 px white outer ring (the "go" ring) at 1.3 r |
| ReadyOnOtherJob | half silver + gold ring | + a Night "J" tick (small vertical bar) in the lit half |
| DoneThisCycle | waning silver | + a Night hourglass/`I` bar in the lit part |
| Blocked | empty + silver ring | unchanged (the only plain empty ring) |
| Foreclosed | ring + notch | diagonal bar full width, ring in **Silver** (luminance-unique vs Dusk) |
| Unknown | dashed ring | dashed ring + centred "?" dot (two dots) |

Luminance ladder under achromatopsia for the lit parts: white #FFFFFF (Completed) > Silver #E3E3E3 (others) > Moon #D5D5D5 — still close; hence the in-disc marks are the real carrier. The same setting sets the stripe patterns from A3, the track to Dusk @ 1.0, and the halo stroke to `max(2.5, 0.2 R)`.

### 2.3 Shape-redundancy rules (write into ui-revamp §5.2, test in T9's GlyphDebugWindow under a greyscale toggle)

1. Every state is unique at r = 6 in greyscale: verify with the debug window's "Greyscale" and "Deutan/Protan/Tritan" checkboxes (apply the Machado matrices from this report to the draw list colours; ~20 lines).
2. No pair may differ *only* by mirror symmetry or *only* by ring colour.
3. Every coloured text carries its meaning as a word in the same cell ("Ready ·", "Locked").
4. A moon means quest state or completion fraction, nothing else (B2).
5. Any glyph < 16 px shows the number beside it or in a tooltip; any gauge < 16 px is drawn as text.

### 2.4 Hold-to-confirm spec (T7)

`Chrome.HoldButton(label, seconds = 0.6f)`: returns true on completion. Draws the button; while `IsItemActive()` accumulates `dt`; draws a Moon arc (DrawHalo track + arc, R = size/2 + 2) around the button as `elapsed / seconds`; release before completion resets; keyboard/gamepad: Enter/Space *held* works the same because ImGui reports `IsItemActive()` for nav-activated items; `ReduceMotion`: replace the arc with "Hold… 0.6 s" text and a filled rect. Tooltip: "Hold to confirm · Shift+click skips the hold". Tests: `ConfirmGate` state machine (idle → holding → fired/cancelled) with fake timings.

---

## 3. Task-by-task change list

| Task | Change |
|---|---|
| **T6** | TodoOverlay: `MinOpacity` 0.6, text shadow under 0.9, hint text Mist; flag on double-click or ⋯; "Locked = click-through" toggle. |
| **T7** | Confirm popup is the guard for both verdicts; hold-to-confirm button; Shift+click = bypass only; Moonlit verdict pills in the detail pane; tutorial text order. |
| **T8** | Tooltips carry the state's plain-language name + shape hint ("Locked out — ring with a bar"). |
| **T9** | Token changes §2.1; Foreclosed bar; Accepted non-mirror redesign; small-size Ready ring; Shadow vs Veil decided; `GlyphPalette` setting; debug window CVD/greyscale toggles; contrast test extended to every (token, surface) pair in B1 with the alpha fills. |
| **T10** | Track VeilLine/Dusk, stroke `max(2, 0.18 R)`, empty core Shadow+rim, no gauge < 16 px. |
| **T11** | 24 px absolute halo floor; mini bar in every density; badge/pill exclusivity. |
| **T12** | Static pip until Motion exists; version in Dusk; status gauge = track+arc+number. |
| **T13** | `MinTarget` 24 px absolute; `ReduceMotion` default from OS; hover motion skipped while scrolling; `HoldButton`. |
| **T14** | Toolbar reflows to two rows; window default fits the viewport; tutorial card scales with UiScale; tutorial chapters/accelerators; "Later" vs "Don't offer again". |
| **T15** | Stripe patterns (or drop the claim); state word first in Next step (test); Dense rows ≥ 24 px absolute; Mist not Dusk/VeilText on hovered/selected rows. |
| **T16** | Requirement marks ✓ ✗ ▶ not moons; reward "obtained" = check badge; Pin and Show path in the action bar (or ⋯); Moonlit verdict pills. |
| **T17** | Drop Ctrl+1..4 / F defaults (configurable, off); Menu-key / Shift+F10 opens the row context menu; Todo ⋯ button; focus ring stays. |
| **Strings** | One display name per state (Locked out, Not checked, Done today); poetic names as subtitles; "Moonlit · rewards" tooltip. |
| **ui-revamp §5.2** | Rewrite: gold/silver fails under achromatopsia (not deuteranopia); Eclipse/Dusk fails under deutan/protan; the stripe is colour-only unless patterned; add the rules in §2.3. |
| **Smoke checklist** | Add: greyscale pass (Windows Colour filters → Greyscale) at UiScale 0.9 and 1.6; deutan pass (Colour filters → Deuteranopia); tutorial to the end with keyboard only; Todo overlay over Coerthas snow at opacity 0.6. |

---

## Appendix A. Full CVD simulation of the token set (Machado 2009, severity 1.0)

| Token | true | deutan | protan | tritan | achroma |
|---|---|---|---|---|---|
| Moon | #F2D27A | #ECD97D | #E4D073 | #FFC5BD | #D5D5D5 |
| MoonDeep | #D8B45A | #CFBC5D | #C6B251 | #E8A7A0 | #B8B8B8 |
| MoonBright | #FFE9A6 | #FDEDA8 | #F7E6A1 | #FFDFD8 | #EAEAEA |
| Silver | #DDE3F0 | #DEE2F0 | #DFE4F1 | #D9E5E7 | #E3E3E3 |
| SilverDeep | #B9C2D8 | #B9C1D7 | #BCC3D9 | #B2C6C9 | #C2C2C2 |
| Mist | #A9B2CC | #A8B1CB | #ABB4CD | #A1B7BA | #B2B2B2 |
| Dusk | #7C86A8 | #7985A7 | #7C89AA | #718C91 | #878787 |
| VeilText | #8A93B0 | #8892AF | #8B95B1 | #81989D | #949494 |
| Veil | #4A5270 | #46526F | #495571 | #40585C | #535353 |
| Shadow | #3A4363 | #354362 | #384664 | #2D494E | #444444 |
| Umbra | #2C334A | #293349 | #2B354B | #23383B | #343434 |
| Bruise | #645574 | #525B73 | #505A75 | #625960 | #5B5B5B |
| Eclipse | #B25C7F | #7E7D7D | #676E80 | #BE5769 | #787878 |
| EclipseText | #D68AA8 | #A5A5A6 | #9398A9 | #E18794 | #A0A0A0 |
| Night | #0F1424 | #0C1424 | #0E1625 | #08171A | #151515 |
| NightRaised | #1E2437 | #1B2437 | #1E2638 | #16282B | #252525 |

Tritanopia note: gold shifts to pink (#FFC5BD) and Eclipse to a redder pink (#BE5769); the two stay apart (ΔE 29) and both stay apart from the blue-greys, so tritan players are the best served by this palette.

## Appendix B. Non-text contrast pairs computed

| Pair | ratio | | Pair | ratio |
|---|---|---|---|---|
| Shadow / Night | 1.89 | | Moon / Shadow (lens vs disc) | 6.60 |
| Shadow / NightRaised | 1.59 | | Moon / Umbra | 8.50 |
| Veil / Night | 2.38 | | Silver / Shadow | 7.55 |
| Umbra / Night | 1.47 | | Moon / Veil | 5.23 |
| Bruise / Night | 2.70 | | Silver / Veil | 5.97 |
| Dusk / Shadow | 2.70 | | Eclipse / Night | 4.13 |
| Dusk / Veil | 2.13 | | Eclipse / NightRaised | 3.47 |
| Track Veil @ .55 / Night | 1.54 | | Eclipse / NightHover | 3.06 |
| Track Veil @ .55 / Raised | 1.44 | | Eclipse / Bruise | 1.53 |
| VeilLine / Night | 3.19 | | Dusk / NightRaised | 4.28 |
| VeilLine / NightRaised | 2.68 | | Dusk / NightHover | 3.78 |
| NightLine / Night | 1.43 | | Selection fill / Night | 1.29 |
| NightRaised / Night | 1.19 | | Active fill / NightRaised | 1.49 |
| NightHover / NightRaised | 1.13 | | Moon / Silver | 1.14 |
| Moon / Dusk | 2.45 | | Silver / Dusk | 2.80 |

## Appendix C. Method notes

- Simulation matrices: Machado, Oliveira & Fernandes, "A Physiologically-based Model for Simulation of Color Vision Deficiency", IEEE TVCG 2009, severity 1.0, applied to linearised sRGB, re-encoded with the sRGB curve. Achromatopsia = WCAG relative luminance mapped to a neutral grey.
- ΔE2000 thresholds: 1–2 just noticeable on large patches; on 10–20 px anti-aliased glyphs, experience with game iconography puts the at-a-glance threshold near 10 and "same colour" under 5. Ratios are the WCAG 2.x formula.
- Acuity: 20/70 ≈ 3.5 arcmin minimum resolvable stroke; 1 px at 96 dpi and 60 cm ≈ 1.5 arcmin; 1440p 27" ≈ 1.3 arcmin; 4K 27" ≈ 0.9 arcmin.
- Target sizes: WCAG 2.5.8 (24 × 24, with the spacing exception) and 2.5.5 (44 × 44) as applied to desktop overlays; logical sizes multiplied by UiScale 0.9 with Dalamud global scale 1.0.
- Keybind collision: FFXIV's default keyboard layout places hotbar 2 on Ctrl+1–0; Dalamud forwards non-text keys to the game.

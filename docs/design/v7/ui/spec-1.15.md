# 1.15 "Faces and icons": giver portraits and icon-and-label buttons

This answers three of the owner's notes:
- "Can we also include face profile screenshots of the giver?" (plan v7, owner point 4: rows F2 and F5)
- "Add any icons that are missing within the plugin that are needed." (point 9: UI-5e)
- "If an icon would give it more emphasis and it supports it, then add it in."

It builds on `docs/research/plan-v7/giver-portraits.md`, `ui-audit.md` §5, and the v7 UI spec (`spec.md`: Revisions 2 and 3, as shipped in 1.14.0).

**Revision 2** applies the supervisor's review (four changes) and records the owner's and the supervisor's rulings:
1. **One framing rule** for every portrait, with per-icon boxes and a guide-banded contact sheet (A2).
2. **No frame ornament** in delivery portraits: the client emblem's script is keyed out before grading (A2.2).
3. **Initials below 32 px** are one letter, and become the moon disc at 18 px (A6).
4. **"Read the journal"** uses the approved book glyph, not the red tile (B1).

**Revision 3** replaces the delivery keying with an offline, figure-first mask: no figure pixel is ever removed, and keyed areas are filled from the backdrop (A2.4).

**Files**

| File | What it is |
|---|---|
| `spec-1.15.md` | This spec |
| `mock.html#giver`, `#fallbacks`, `#buttons` | The three boards. Sources: `mock-src/v715.js` and `v715.css`, built into `mock.html` by `mock-src/build.py`. |
| `portraits-giver.png` | Giver cards at Full for each source family, ungraded against graded, Quiet, Plain, the 128 px tooltip, the 0.3 s fade, avatars (Next stops, Route, Journal column), the crop boxes and the grade |
| `portraits-fallbacks.png` | The 16 silhouettes and the moon disc, society emblems, initials, the spoiler case, and every fallback at 72 / 64 / 24 / 20 / 18 px |
| `buttons.png` | The action bar at each level, the short-label and icon-only steps, the Route window, an in-game panel, requirements and rewards with icons, and the icon table |
| `1.15/silhouettes/*.svg` | The 16 race silhouettes and `moon-disc.svg` (64-unit viewBox). `make_silhouettes.py` regenerates them. |
| `1.15/art/*.png` | **Real game art**, exported with Lumina from the local 2026.09.15 client (see A2). Also `key_delivery.py`, the reference keying (A2.4), with its `*-keep.png` masks and `*-keyed.png` results. |
| `1.15/icons/*.png` | The game icons the buttons use (see B1) |

`1.15/art` and `1.15/icons` are for the mockups only. The plugin reads the same textures from the player's install at runtime and never ships or redistributes them.

---

## A. Giver portraits (F2, F5)

### A1. Sources and priority

The sources come from the research. For each giver, take the first that exists:
1. The **opt-in portrait pack** photo, matched by ENpcBase id.
2. A **Duty Support portrait** (`DawnQuestMember.BigImageOld`, 072621–072664).
3. A **Triple Triad card** (087000 + `TripleTriadCard` row, matched by exact name or a unique first-name alias).
4. A **battle dialogue portrait** (073001–073291, named by `QuestBattle` `FACE_GRAPHIC_<NAME>` plus the alias table).
5. A **custom delivery portrait** (`SatisfactionNpc.Icon`, 061661–061670).
6. A **fallback** (A6).

Within a source, take the variant whose era (`QuestBattle.Quest` or `DawnQuestMember` patch) is closest to the quest's expansion. `curated/giver_portraits.json` can override any pick (name or ENpcBase id → icon id and crop) or block a wrong match (P3).

### A2. Framing: one rule, and the boxes that meet it

#### A2.1 The rule

Every portrait is framed the same way, whatever its family. All values are fractions of the plate's diameter, measured from the top:

| Landmark | Target |
|---|---|
| Crown (top of the skull, not hair tips or ears) | 8–12 % |
| **Eye line** | **42–46 %** |
| **Chin** | **78–84 %** |
| Face (brow to chin) | about 55 % |
| Face centre | within ±10 % of the plate's centre line |

**Solving for a box:** with the eye line E and the chin C measured in source px, the box is square with side D = (C − E) / 0.37, top = E − 0.44 D, and left = face centre − D / 2. It is clamped to the texture.

Hair, ears and hands may touch the circle's edge but must not dominate it. Only face, hair and backdrop may be inside the circle (A2.2).

#### A2.2 Boxes: family defaults and per-icon boxes

Each box is a square in source px, from the top-left of the hr texture (the 1x texture is half). The plugin passes it as UVs to `AddImageRounded`.

**Family defaults** apply until the curation pass (P3) records a box for an icon:

| Family | Texture | Default box (x, y, side) |
|---|---|---|
| Duty Support portrait | 188 × 480 | 6, 86, 140 |
| Battle dialogue portrait | 640 × 512 | 164, 154, 172 |
| Triple Triad card | 208 × 256 | 28, 20, 135 |
| Custom delivery portrait | 400 × 480 | 104, 154, 158, plus the script key |

Compositions vary within a family: Alphinaud's Duty Support head sits about 40 px lower than Y'shtola's, and Tataru's card is a raised-arms figure. So per-icon boxes in `curated/giver_portraits.json` are the normal case for the top givers.

**Per-icon boxes** for the eight samples, measured to the rule (all in `portraits-giver.png`'s contact sheet):

| Icon | Giver | Box (x, y, side) | Note |
|---|---|---|---|
| 072621 | Alphinaud, Duty Support | 10, 107, 143 | moved down and enlarged; his chin is now inside |
| 072626 | Y'shtola, Duty Support | 0, 66, 139 | clamped at the left edge |
| 073025 | Y'shtola, battle dialogue | 171, 150, 168 | |
| 073034 | Alphinaud, battle dialogue | 158, 162, 177 | |
| 087019 | Tataru Taru, card | **70, 62, 75** | tight on her face; the raised hands stay outside |
| 087058 | Cid Garlond, card | 28, 20, 135 | the beard runs to the chin band, as it should |
| 061661 | Zhloe Aliapoh, delivery | 106, 161, 158 | script keyed |
| 061662 | M'naago, delivery | 102, 153, 157 | script keyed; her face is protected from the key |

**Delivery portraits: no frame ornament.** Each delivery portrait carries the client's emblem as a ring of gold-and-green script, and no box that meets the rule keeps it outside the circle for M'naago. It is removed **offline**, as specified in A2.4. The keyed crops are on the contact sheet at 128 px.

**The tooltip size** is the smaller of 128 px and 1.6 × the box's hr side. Tataru's 75 px card box shows at 120 px, so a portrait is never stretched to mush.

#### A2.3 The DataGen contact sheet

`DataGen --portraits-sheet` renders every indexed portrait so curation can check every crop at a glance:
- Each crop is drawn at 120 px on the plate, **ungraded**, grouped by family, and labelled with the icon id, giver, box, and "script keyed" where it applies.
- **Guide bands** are drawn over each crop:
  - crown 8–12 % (blue, .22);
  - eye line 42–46 % (gold, .30, with a 1 px line at 44 %);
  - chin 78–84 % (red, .26, with a 1 px line at 81 %);
  - two ticks at the foot, 55 % of the diameter apart, for the face width.
- **Automatic flags:** any delivery-key pixel left inside the circle, and any box clamped by the texture edge.

Curation moves a box until the eyes sit in the gold band and the chin in the red one, then records it. The bottom of `portraits-giver.png` is this sheet for the eight samples.

#### A2.4 Keying the delivery script (offline, figure first)

**Why offline:** only 8 icons need it (`SatisfactionNpc.Icon` 061661–061670). A curated, reviewed output is simpler and safer than running image heuristics in the game.

**What ships:** DataGen (`--portrait-masks`) writes a **1-bit keep mask** per icon at the hr size into `curated/portrait_masks/<icon>.png` (about 0.5–0.8 KB each). The art itself is never shipped.

**At runtime:** `PortraitGrading` multiplies the texture's alpha by the mask in the same CPU pass that makes the graded copy (A3). While the copy is loading, the plate shows the fallback rather than the unkeyed art.

**If the art changes:** the mask is keyed by icon id plus a hash of the texture. A patch that changes the art invalidates the mask, and the portrait falls back until DataGen is rerun.

**The algorithm:** the reference implementation is `1.15/art/key_delivery.py`, which writes `<icon>-keep.png` and `<icon>-keyed.png`. All steps run at the hr size.

1. **Opaque:** alpha > 40.
2. **Script colour key:** alpha > 8, saturation > .35, value > .12, hue 40–170°, G > B + .06, and not skin-red (R > G + .05 and R > .6). This catches the green letters, their dark-green shading and the gold seal.
3. **Figure first.**
   - **Flood:** the figure is the 4-connected flood from the curated face seed (061661: 190, 225; 061662: 178, 232) over opaque pixels **not within 2 px of the key**. The 2 px halo stops the flood at the letters' dark outlines.
   - **Grow back:** the figure is then grown back 2 px into opaque pixels that are not script colour. This keeps the anti-aliased hair edge where a letter touches it.
   - **Protected:** hair, skin and clothing are all in the figure, and **no figure pixel is ever removed**. The script asserts this ("removed inside figure 0" for both icons).
4. **Islands:** every opaque island not connected to the figure is removed if it is mostly script (at least half its pixels in the key or its halo) or tiny (under 12 px). Anything else, such as a detached hand or hair tip, is kept and listed for review.
5. **Script outside the figure:** script-colour pixels outside the figure are removed, dilated 1 px but never into the figure. Low-alpha pixels inside the script's halo are removed too: these are the letters' soft edges.
6. **Fill:** removed pixels take the **backdrop's local colour and alpha**, from a normalised blur (radius 6 px) of the surrounding pixels that are neither figure nor removed. They are never cut to a hard hole.
   - These portraits have a transparent backdrop, so the fill resolves to alpha 0. The well shows there exactly as it does around the whole figure.
   - A painted backdrop would be filled with its own blurred colour.

**Results** on the samples (`061661-keyed.png`, `061662-keyed.png`):
- **Removed:** 7,069 px (Zhloe) and 4,498 px (M'naago), with 0 figure pixels touched.
- **Damage from the first pass, fixed:** M'naago's collar is intact (no blue hole), Zhloe's green speck is gone, and the dark speck by M'naago's hair is gone.
- **One honest limit:** where a letter was painted over M'naago's glove, the glove's edge there is the letter's shape, because there is no glove art under the letter. It lies at the circle's edge at 72 px and reads as the glove's outline.

**Review:** the contact sheet shows every delivery portrait at 128 px three ways: source, keyed, and keyed and graded. Curation signs each one off before its mask ships.

### A3. The night grade

The grade unifies Duty Support colour, card colour, delivery colour and the sepia battle dialogue faces into one set. It is a 3 × 4 colour matrix in sRGB, applied to a graded copy the way `BannerGrading` grades banners: read back once in the background, cached by (icon id, box), with the oldest copies released.

The matrix is built from four steps:
1. **Desaturate** toward Rec. 709 luma by *d*.
2. **Multiply** by the night tint `#2A3768` at strength *m*.
3. **Scale** by 0.94.
4. **Lift the blacks** by Night `#0F1424` × 0.25.

| Family | d | m | Matrix (rows R, G, B: r g b + offset) |
|---|---|---|---|
| Colour (Duty Support, Triple Triad, delivery, pack) | .25 | .22 | `.6162 .1372 .0138 +.0147` / `.0413 .7224 .0140 +.0196` / `.0435 .1462 .6279 +.0353` |
| Battle dialogue (sepia) | .50 | .18 | `.4842 .2856 .0288 +.0147` / `.0858 .6923 .0291 +.0196` / `.0893 .3003 .4502 +.0353` |

Until the copy lands, the source is drawn with step 2 alone as an `AddImage` tint: (.816, .827, .870) for colour and (.850, .859, .893) for battle dialogue. So a bright source never flashes ungraded. The mock applies exactly these matrices (`feColorMatrix`, sRGB).

**Ruling (supervisor, Q5):** the grade applies at **every** Decoration level. It is colour normalisation, not ornament: without it a sepia face beside a full-colour card reads as two different features. On the future light palettes (for example Ishgard Snow), **skip the night multiply** (step 2) but keep the desaturation and the black lift.

### A4. The plate

Every portrait, avatar and fallback sits on the same plate. Values are in the plate's 72-unit box and scale with it.

| Part | Full | Quiet | Plain |
|---|---|---|---|
| Well | r 35, vertical gradient `#1D2B5A` → `#131C40` (the medal's well) | the same | flat `#1C2237` |
| Face | clipped to r 34.5. The crop box maps to 69 × 69 at (1.5, 1.5). `AddImageRounded` with rounding = half the size draws the circle. | the same | the same |
| Lip shadow (32 px and up) | the well's upper-left lip in shadow: Abyss `#080B16` at .55, a crescent (the well minus itself offset 2.6, 3.4), blur 1.4. Light from the upper left, as every medal. A baked `OrnamentAtlas` sprite, drawn over the face. | none | none |
| Moonlight wash (32 px and up) | MoonHigh at .07, radial from (20, 16), r 40 | none | none |
| Keyline | 1 px brass on r 34.9, lit upper left: `#E6CF98` → `#9A7E4A` → `#7C6236` → `#5C4724`. A new `Ornament.BrassRing`, a per-vertex mesh like `BrassBorder`. Plus 1 px Abyss at .6 outside it on r 35.6. | 1 px Silver `#C3CBDF` at .62 (the Quiet medal's hairline) | 1 px `#3A4050` |

Below 32 px (avatars, the Journal column, Plain), only the well, the face and the keyline are drawn.

### A5. Where portraits appear, and their sizes

| Where | Size | Layout |
|---|---|---|
| Detail pane, **Giver card**, Full | **72** | Plate left; gap 14; name 13.5 px Text and place 12 px TextSecondary, centred on the plate. The card's minimum height holds the plate, so the layout is static and never jumps when a texture lands. |
| Giver card, Quiet | **64** | gap 12 |
| Plain ledger | **18** inline | before the name in the `Giver` key-value line, gap 6 |
| **Hover** on any portrait | **128** in the tooltip, at most 1.6 × the box's hr side (A2.2) | Then the name in the Title role, the source line ("Portrait: Triple Triad card art"; pack: "Garland Tools photo · credit Celes") in TextTertiary, and the place. Full tooltip frame. |
| Next stops, Route window, Tonight card | **24** avatar | Before the stop's text, gap 10. Every stop has a picture: a face, a silhouette or an emblem. |
| Journal table | **20**, in a new **Giver** column, **off by default** (Columns menu) | The avatar, then the giver's name |

Faces are drawn from the hr texture when the drawn size is more than 1.5× the 1x box, so a texture is never stretched.

### A6. Fallbacks (`portraits-fallbacks.png`)

Each fallback sits on the same plate, so nothing reads as missing. They are tried in this order:
1. **Allied society givers:** the society's `BeastTribe.Icon` (65016 Amalj'aa … 65131 Yok Huy).
   - **Ruling (supervisor, Q2):** it stays a **square tile**, not cropped to the circle: the tile is the society's own art, and the quartermasters read at once. It is centred at **64 %** of the plate, ungraded, at .92 alpha.
   - **From 32 px**, a soft down-right shadow sits under the tile: `#080B16` at .45, offset 0.8 / 1.1 px, blur 1 px. It is baked with the plate sprite, or drawn as a tinted offset copy.
2. **Generic givers** (lowercase names) **and named givers with a known race:** a **race silhouette**.
   - **Choice:** from `ENpcBase.Race` (1 Hyur, 2 Elezen, 3 Lalafell, 4 Miqo'te, 5 Roegadyn, 6 Au Ra, 7 Hrothgar, 8 Viera) and `Gender` (0 male, 1 female).
   - **Look:** 16 flat head-and-shoulders shapes in one ink, MoonstoneHigh `#C9D3EA` at .86, drawn as one group so no seams stack. Each race is told apart by its outline alone: ears, horns, mane, buns, beard, hair length. See the comment in `make_silhouettes.py`.
   - **Baking:** they are baked into `OrnamentAtlas` at 64 and 128 (and 2x) like the medals; the plugin does not render SVG.
   - **Non-humanoids** (530 ids) get `moon-disc.svg`: a waxing crescent lit on the right over a .32 disc.
3. **Named givers with no race and no art:** **initials** in the Title face (Jupiter; Marcellus in the mock), `#E9E4D2` at .92.
   - **From 32 px:** one word gives one letter ("Gerolt" → G); two or more words give the first and last initials ("Mother Miounne" → MM, "Hamujj Gah" → HG).
   - **20–31 px** (avatars, the Journal column): **the first initial only**, with caps at least **9 px**. Two letters smudge at that size.
   - **Under 20 px** (Plain's 18 px): there is no room for 9 px caps, so the **moon disc** is drawn instead.
4. **Masked quests (spoiler shield):** **never a face**, even when one exists.
   - The plate falls back to the silhouette, or to initials when the race is unknown.
   - The tooltip has no source line.
   - The name follows the shield's existing rules.
   - So the faces of future-expansion reveals stay hidden until the quest is unmasked.

### A7. Motion

On a new selection, when the giver changes, **the face fades in over 0.3 s** with ease-out cubic. Only the face fades; the plate and any fallback already drawn stay put and never blink. When the texture arrives late, the fade starts on arrival.

There is no fade under Reduce motion or at Plain.

**New token:** `MotionTokens.ArtFade = 0.3f`, an "art" token. It is longer than the 0.22 s interaction tier because a face is content arriving, not an interaction answering. It is not a gold moment, so `MomentPeak` does not apply.

### A8. Settings and safety

Settings › Display › Look gains a **Giver portraits** choice:
- Off
- **Game art** (default)
- Game art + portrait pack

**Portrait pack:**
- "Download portrait pack (about 15 MB)" asks first, shows progress, and checks the file's hash.
- "Clear portraits" asks for confirmation, at the Armed tier.
- The pack's source and credit appear in About: Garland Tools, photos by Celes.

**Journal column:** the 20 px Giver column is in the table's Columns menu, off by default.

**Credits:** game art is read locally and never uploaded or redistributed.

### A9. Code map

| Piece | Where |
|---|---|
| `GiverPortraits` index: name or ENpcBase id → (source, icon, box, era); aliases; curated overrides | Tsukimichi.GameData, standalone on `ExcelModule`, tested like `BannerSources` (P1) |
| `PortraitGrade` (the two matrices, d / m / scale / lift as constants) | Tsukimichi.Core/Ui, with a unit test that rebuilds the matrices from the four steps |
| `PortraitGrading` (graded copies) | Tsukimichi/Ui, sharing `BannerGrading`'s readback, cache and retire logic (a generic `GradedTextureCache<TKey>`) |
| `Chrome.Portrait(dl, min, size, PortraitRequest)` | draws the plate per level, the face or a fallback, and the fade, and records the hover rect for the 128 px tooltip |
| Giver card | `DetailPane.cs` 800–826 (the giver lines move right of the plate) |
| Avatars | `TonightCard.Stops.cs`, `NextStopsSource`, `RouteWindow.cs`, `TablePane.cs` (the new Giver column, hidden by default) |
| Silhouettes and lip shadow | `OrnamentAtlas` sprites, baked from `1.15/silhouettes/*.svg` by the atlas script |

---

## B. Icon-and-label buttons (UI-5e)

### B1. The icon set: the game's own icons

There are two styles. **Map symbols** (transparent shapes) are drawn bare, with a 1 px shadow: the icon tinted Abyss at .5 and offset (0, 1). **Action tiles** (framed squares) are drawn at the icon size with rounding 3 and a 1 px Abyss .5 inset ring. Actions that have no game icon keep their FontAwesome glyph. Icons keep their colours at every level, because they carry meaning.

| Action | Icon id | Sheet | Style |
|---|---|---|---|
| Go to giver (Questionable) | 071201 / 071221 / 071341 | the quest's own map marker: MSQ / side / feature (`NodeIcons.MsqMarker`, `SidequestMarker`, `FeatureMarker`) | map symbol |
| Teleport | 060453 | MapSymbol 1 · Aetheryte | map symbol |
| Return | 000112 | GeneralAction 8 · Return | tile |
| Walk (vnavmesh) | 000104 | GeneralAction 4 · Sprint | tile |
| Flag on the map | 060561 | map flag marker | map symbol |
| Open map | 000007 | MainCommand 16 · Map | tile |
| Aethernet (Lifestream) | 060430 | MapSymbol 2 · Aethernet Shard | map symbol |
| Ferry / airship | 060456 | MapSymbol 15 · Ferry Docks | map symbol |
| Mount up | 000118 | GeneralAction 9 · Mount Roulette | tile |
| Fly | 000122 | GeneralAction 24 · Flying Mount Roulette | tile |
| Duty / Duty Finder | `ContentType.Icon`, else 000046 | MainCommand 33 · Duty Finder | tile |
| Read the journal | **the approved book glyph** (`MedalArt.RowGlyph(Journal)`, `_row/badge-journal.svg`), FontAwesome BookOpen as the fallback | in the button's normal ink. **Not** the red "!" tile 000005: it is too loud, reads as a warning, and red belongs to Locked out. | glyph |
| Instance hop | SeIconChar Instance1–9 | U+E0B1–E0B9 in the Axis game font, drawn with the existing game-font handle; fallback text "Instance 2" | glyph |
| Job requirement | 62100 + ClassJob | ClassJob icons (BLU 62136) | tile |
| Allied society requirement or reward | `BeastTribe.Icon` | 65016 … 65131 | tile |
| Previous-quest requirement | that quest's marker | as Go to giver | map symbol |
| EXP · gil | 065001 (**verify**: the sample reads as a ring, not "EXP") · 065002 gil | currency icons | tile |
| Stop, Pin, Path, Link, Copy, Report, More | — | FontAwesome (no game icon) | glyph |

Every id in this table was read from the 2026.09.15 client with Lumina (GeneralAction, MainCommand, MapSymbol, BeastTribe and TripleTriadCard sheets). The PNGs are in `1.15/icons`.

### B2. Button anatomy, by level

| | Full | Quiet | Plain |
|---|---|---|---|
| Height | 30 | 28 | 22 (was text only) |
| Icon | **18** | **16** | **14** |
| Pad start / icon gap / pad end | 11 / 6 / 13 | 10 / 6 / 12 | 7 / 4 / 8 |
| Label | 12 px Text; the primary is the gold pill with ink `#1A1406` | 12 px; the primary is flat Moon | 11.5 px; the primary has gold text |
| Round utility buttons | 28 px, 14 px icon (game icons for Flag and Map; the book glyph for Journal; FontAwesome for the rest) | 28 px, hairline border | 22 px square, 12 px icon |
| In-game panels | 26 px pills with 16 px icons at every level | | |
| Disabled | The icon at .45 alpha, tinted `#8A93B0` (which also takes the colour out), the label TextDisabled, and the reason in the tooltip | | |

### B3. When a button doesn't fit

`ActionPillFit`, now also at Plain, which had been text only:
1. Full label.
2. Short label ("Go to giver" → "Go to", "Walk to giver" → "Walk").
3. **Icon-only:** 1.45 × the height wide (44 / 41 / 32 px), with the **full label as the tooltip's first line**.

A button is never clipped and never ends in an ellipsis. The row chooses one step for all its pills, as today, so they shrink together.

### B4. Where (the audit's I8, I15, I17, I19, I20)

- **Travel buttons:** every travel and route button gets its icon (I20). The sites are those `TravelControls`, `PlanPane`, `RouteWindow`, `TodoOverlay`, `FlightPane`, `TonightCard.Stops` and `CharactersPane` (Abandoned, Planning, Route) list in ui-audit §5. All go through `Chrome.ActionPill`, with a new `GameIconRef` overload: the icon id plus its style.
- **Route header (I19):** the target's own icon, drawn at 22 px before the title:
  - a quest: its map marker;
  - a duty: `ContentType.Icon`;
  - a reward: its item or menu icon;
  - otherwise MapSigns.
- **In-game panels (I15):** offer, result and journal companion buttons as 26 px pills. Their Moonlit reward and unlock lines gain icons (`QuestBrief` carries the icon ids).
- **Requirements (I17):** an 18 px icon after the check or cross:
  - job: 62100 + job;
  - previous quest: its marker;
  - society rank: `BeastTribe.Icon`;
  - mount: `Mount.Icon`;
  - achievement: `Achievement.Icon`;
  - duty: `ContentType.Icon`.
- **Rewards line (I8):** "855 EXP · 414 gil" gains its currency icons.

---

## Decisions

| # | Question | Decision |
|---|---|---|
| 1 | Portrait pack | **Opt-in download**, already decided for a later release (F4). 1.15 ships game art only. |
| 2 | Society emblems | **Square tiles**, not cropped. 64 %, ungraded, with a soft down-right shadow from 32 px (the supervisor's ruling; A6). |
| 3 | Icons on Plain buttons | **Yes**: Plain buttons get 14 px icons (B2). |
| 4 | Journal Giver column | **Off by default** (A5, A8). |
| 5 | Grade at Quiet and Plain | **Every level** (the supervisor's ruling). On future light palettes, skip the night multiply and keep the desaturation and the black lift (A3). |

No open questions remain for 1.15.

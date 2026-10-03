# The per-set atlas contract (1.16.0 T4 ⇄ T5; faces and frames 1.17.0 T11; Plain strip and kit ornaments 1.17.0 T15)

What `tools/themes/build_themes.py` (T4) writes for a glyph set, and what the plugin's atlas runtime (T5,
`Tsukimichi/Ui/Themes/ThemeAtlasCache.cs`, layouts parsed by `Tsukimichi.Core/Ui/Themes/ThemeAtlasLayout.cs`) reads.
Research background: `docs/research/plan-v7/theme-system.md` §3.2, §6.2 and §6.3.

**Menphina's Medallion is not moved.** Its atlas stays embedded at `Tsukimichi/assets/ui/medals.png`, `medals@2x.png`
and `medals.json`, with its layout compiled into `Core/Ui/MedalLayout.cs`, and its row tier stays procedural
(`MedalArt` meshes). Classic stays procedural (`LegacyMoonGlyph`). Everything below is for the new sets.

## 1. Where the files go

```
Tsukimichi/assets/ui/themes/<set-key>/
  medals.png        required   hero atlas at 1x
  medals@2x.png     required   the same layout at exactly twice the size
  medals.json       required   the layout (schema §2: the same as Tsukimichi/assets/ui/medals.json)
  row.png           required*  whole-pixel row strips (schema §3); 1x only
  row.json          required*
  plain.png         optional   the flat (Decoration Plain) finish at hero tiers, schema §2 (no set ships one; §2)
  plain@2x.png      optional
  plain.json        optional
  faces.png         required†  the set's unframed faces at the hero tiers (§7), 1x
  faces@2x.png      required†  the same layout at exactly twice the size
  faces.json        required†  the layout (schema §7)
  faces-row.png     required†  the faces at every whole device pixel from 12 to 31 (§7)
  faces-row.json    required†
  metrics.json      optional   the build's gate numbers (§5); not read by the runtime, not packaged

Tsukimichi/assets/ui/kits/<kit-key>/          (1.17 T11)
  frames.png, frames@2x.png, frames.json      the kit's frames and badges at the hero tiers (§7)
  frames-row.png, frames-row.json             its frames at every whole device pixel from 12 to 31
  ornaments.png, ornaments.json               optional (Kirikane, 1.17 T15): its Decoration ornament sprites (§8)
  metrics.json                                every set's faces gated in this kit (§5); not packaged
```

† For every mixable set that ships, Menphina's Medallion included (its faces are cut from gen5.py by
`tools/themes/sources.py`; its own medals stay embedded and procedural).

\* Every set that ships (Ishgard Glass and Aether Crystal in 1.16, Astrologian's Orrery and Sumi to Kinpaku in 1.17) has `row.*`; without them the set would show
Medallion's medals at row sizes (§4), so `ThemeAtlasRuntimeTests` requires them for the shipped sets. Medallion has no
row strip (its row tier stays procedural); `themes/medallion/` holds its faces (§7) and the build's `metrics.json`.

`tools/themes/build_themes.py` (manifests in `tools/themes/sets/`) writes all of these; `Tsukimichi.Tests/Ui/ThemeAtlasTests.cs`
holds its output to the gates, and `Tsukimichi.Tests/Ui/Themes/ThemeAtlasRuntimeTests.cs` to this contract and its budgets.

- `<set-key>` is the set's key in `GlyphSets` (`Tsukimichi.Core/Ui/Themes/AppearanceCatalog.cs`), and the folder name must
  match it exactly. The keys are stable forever:

  | Id | Key | Name | Kind |
  |---|---|---|---|
  | 1 | `medallion` | Menphina's Medallion | procedural + embedded atlas (unchanged) |
  | 2 | `classic` | Classic | procedural (`LegacyMoonGlyph`); no folder |
  | 3 | `aether-crystal` | Aether Crystal | atlas (this contract) |
  | 4 | `ishgard-glass` | Ishgard Glass | atlas (this contract) |
  | 5 | `astrologian-orrery` | Astrologian's Orrery | atlas (this contract; ships in 1.17) |
  | 6 | `sumi-to-kinpaku` | Sumi to Kinpaku | atlas (this contract; ships in 1.17, with a Plain row finish) |

- Every `.png` and `.json` under `Tsukimichi/assets/ui/themes/` and `Tsukimichi/assets/ui/kits/` but `metrics.json` is
  packaged as a content file (the csproj globs both folders) and loaded from disk with `ITextureProvider.GetFromFile`, so new sets never grow the DLL. Nothing
  else needs registering: dropping a valid folder in makes the set drawable.
- A set whose folder is missing, or whose `medals.json` does not parse, is not drawable; the plugin draws Menphina's
  Medallion for its states instead (the stand-in rule, §4). `ThemeAtlasRuntimeTests` fails the build on a malformed folder.

## 2. Hero atlas: `medals.json` (also `plain.json`, `faces.json`, `frames.json`)

Byte-for-byte the schema of today's `Tsukimichi/assets/ui/medals.json`:

```json
{
  "size": [W, H],
  "tiers": [48, 64, 96, 128],
  "note": "free text",
  "sprites": {
    "ready": { "48": [x, y, w, h], "64": [...], "96": [...], "128": [...] },
    ...
  }
}
```

- `size` is the 1x PNG's size. `medals@2x.png` is **exactly** `[2W, 2H]`, and a 2x rect is the 1x rect doubled.
- `tiers` are ascending cell sizes in 1x px. Use `[48, 64, 96, 128]` (the runtime accepts any ascending list, but the
  tests and the 1.5× shrink rule were tuned on these). A tier's cell is square: `w == h == tier`.
- Rects are `[x, y, w, h]` in 1x pixels, integers. Every rect sits at least 1 px inside the image, and no two rects
  come closer than **2 px** at 1x (so 4 px at 2x): bilinear sampling must never bleed a neighbour in.
- A cell is the medal's whole **128-unit box** at that size, transparent outside the medal.
- **Required sprite keys for `medals.json` and `plain.json`** (the 11 of `MedalLayout.Key`, same meaning as
  Medallion's):

  | Key | State | Badge in the cell (hero) |
  |---|---|---|
  | `ready` | Ready | open padlock |
  | `in-journal` | In journal | book |
  | `blocked` | Blocked | closed padlock |
  | `done-this-cycle` | Done this cycle | none |
  | `completed` | Completed | none |
  | `locked-out` | Locked out | none |
  | `not-checked` | Not checked | none |
  | `other-job-tank` | Ready on another job, tank seat | empty seat, tank colour |
  | `other-job-healer` | … healer seat | empty seat, healer colour |
  | `other-job-dps` | … DPS seat | empty seat, DPS colour |
  | `other-job-hand` | … Hand/Land seat | empty seat, slate |

- `medals.*` is the set **as designed**: the face, the set's own kit frame at the Full finish (the act-now tier on
  Ready), and the badge. The plugin draws the cell as is, then draws the game's job icon into the seat of the
  `other-job-*` cells. So the badge geometry is fixed for every set: centre (95, 95), keyline 24, seat 19.9, job slot
  35.5, in the 128-unit box (`MedalArt.BadgeCenter`, `MedalArt.JobIconSlot`). The face must keep that disc clear.
- `plain.*` (optional) is the flat finish at hero tiers: no material, a 1 px state rim, as Medallion's Plain ladder.
  Without it, Decoration Plain draws Medallion's Plain ladder for this set at hero sizes. **No set ships one:** a second
  782 × 574 atlas beside `medals` and a two-finish row strip breaks the 4 MB per-set budget (§6), so a set's own flat
  finish lives in its row strip (§3) and from 32 px Medallion's Plain ladder stands in, as it has no badge either.
- Extra sprite keys are allowed and ignored. Unknown top-level keys are ignored.

## 3. Row strips: `row.json`

What the build writes (the plain form, one finish: Full):

```json
{
  "size": [472, 266],
  "sizes": [12, 13, 14, ..., 31],
  "note": "free text",
  "sprites": {
    "ready": { "12": [x, y, 12, 12], "13": [...], ..., "31": [...] },
    "ready-on-another-job": { ... },
    ...
  }
}
```

- One PNG, 1x only: row cells are already in **device** pixels, so there is no `@2x`.
- `sizes` is every whole device pixel from **12 to 31**, contiguous (the row tier is everything under 32 px,
  `MedalLayout.RowTierMaxPx`). Each cell is a **real render at that size**, not a downscale.
- Required sprite keys, the 8 states (row cells carry **no badge**; the plugin draws the badge's content beside the
  medal at text height, as it does for Medallion):
  `ready`, `ready-on-another-job`, `in-journal`, `blocked`, `done-this-cycle`, `completed`, `locked-out`, `not-checked`.
- A cell is the 128-unit box at N × N px, framed as designed, `w == h == N`. Same padding rules as §2 (≥ 2 px apart,
  ≥ 1 px inside).
- **More finishes (optional, nested form).** To add a Decoration Plain or Quiet strip, key the finishes first, all in
  the same PNG: `"sprites": { "full": { <states> }, "plain": { <states> }, "quiet": { <states> } }`, with an optional
  `"finishes": ["full", "plain"]` list for readers. `full` is then required; without `plain`, Plain draws Medallion's
  Plain ladder; without `quiet`, Quiet draws `full`. The runtime tells the forms apart by a `full` key under `sprites`.
- **The build writes the nested form for a set with a flat finish** (its manifest's `plain` masters; Sumi to Kinpaku's
  `_plain/`, 1.17 T15): every state's `full` shelf, then every state's `plain` shelf, in one `row.png`. A set without
  one keeps the plain form, byte for byte. `GlyphSetInfo.HasPlainFinish` says which sets have one (Medallion's and
  Classic's are drawn; among the atlas sets only Sumi's), and `ThemeAtlasTests` holds the strip to it. The Plain cells
  carry no frame and no badge: the flat face with the state's own 1 px rim (Medallion's Plain rim inks), so the two
  Plain finishes line up in a mix. The build gates them as a tier group of their own (`plain`: G1, G1c, G2, G2D).

## 4. How the runtime uses them (for the art's sake)

`MedalGlyph.Box` snaps every medal to a whole-pixel square of side S (device px). For a state drawn by an atlas set:

| S | Full / Quiet | Plain |
|---|---|---|
| ≥ 32 (hero) | `medals` at the smallest tier ≥ S (1x, then 2x above 128); never shrunk more than 1.5× | `plain` if present, else Medallion's Plain ladder |
| 12–31 (row) | `row` `full` (or `quiet`) cell of exactly S | `row` `plain` cell of exactly S (Sumi to Kinpaku), else Medallion's Plain ladder |
| < 12 | the 12 px row cell, shrunk | as above |

- Loading is lazy per set (`ThemeAtlasCache`): only sets the current appearance uses are requested; `row` loads first,
  `medals` on the first hero draw, `@2x` only above 128 px; a set that drops out of the appearance is released after
  a short idle.
- Until a texture has loaded, and whenever a file is missing, **Medallion's procedural medal of the same state stands
  in**, so a row is never blank.
- High contrast ignores every set and draws Medallion's high-contrast ladder (theme-system §3.4).

## 5. `metrics.json` (optional, owned by T4)

The per-set gate results the build computed (theme-system §7.1): distinctness, salience and the cross-set table, for
reviewers now and for the mix warnings later (1.17 T10). The runtime does not read it in 1.16, so the build owns the
schema (`tools/themes/README.md`), and `Tsukimichi.Tests/Ui/ThemeAtlasTests.cs` holds its numbers to the gates.

## 6. Budgets (asserted by `ThemeAtlasRuntimeTests`)

A set draws either as designed (`medals`, `plain`, `row`) or composed in another kit (`faces`, `faces-row`), never both
in one appearance, so each path has its own budget.

- Per set, 1x textures as designed (`medals` + `row` + `plain`): **≤ 4 MB** of RGBA (W × H × 4 summed); composed
  (`faces` + `faces-row`): **≤ 4 MB**.
- Per set, the `@2x` textures of each path: **≤ 12 MB** of RGBA.
- Per set, PNG files on disk: **≤ 2.5 MB** as designed, **≤ 1.5 MB** of faces. Per kit: **≤ 1.5 MB**, and its 1x
  textures **≤ 4 MB**.
- Every reachable appearance (every shipped set, each by its larger path, Medallion included, plus the largest kit,
  1x): **≤ 12 MB** of RGBA in total.

## 7. Faces and frames: the frames axis (1.17 T11)

theme-system §3.2–3.3 and §6.1; `docs/design/v7/ui/spec-1.17.md` §B. When the appearance's frame kit is not a set's own
(`ResolvedAppearance.Composes`), the plugin composes that set's medals from its faces and the kit's frames, in the
approved mix proofs' order (the sets' `_src/mix.py` and `compose()`):

1. the face's **under** layer (the well and emblem, inside the shared well: centre 64, 64, r 52.4);
2. the kit's **frame** for the state's urgency tier: act-now (Ready; the shared gilt in every kit), resting, finished
   (Completed), ghost (Not checked); at Decoration Full its rim, at Quiet its hairline;
3. the face's **over** layer (the overhangs drawn above the rim: In journal's ribbon, Completed's check), where it has one;
4. from 32 px, the kit's **badge** at the shared slot (open lock, book, closed lock, or an empty role seat for Ready on
   another job, into which the plugin draws the game's job icon).

On a light palette at Decoration Full, every medal (any kit, composed or not) also gets a 1 px Abyss `#080B16` outer
keyline at .6, half a pixel outside its own keyline (spec-1.17 §B2; `FrameParts.LightKeyline`), drawn by the plugin, not
baked into the atlases.

A set in its own kit draws `medals.*` and `row.*` as designed (the same art, byte for byte the 1.16 atlases), and
Decoration Plain never composes (Plain has no frames). Until every part a medal needs has loaded, Menphina's Medallion
stands in, as §4.

**`faces.json`, `frames.json` (hero tiers)**:

```json
{
  "size": [W, H],
  "tiers": [48, 64, 96, 128],
  "boxes": { "ready": [8, 8, 112, 112], "badge-open": [64, 64, 64, 64], "frame-act-now-full": [0, 0, 128, 128], ... },
  "sprites": { "ready": { "48": [x, y, 42, 42], ... }, ... }
}
```

- A sprite's **box** is the part of the 128-unit medal box it covers, `[x, y, w, h]` in units, on an 8-unit grid so a
  cell is whole pixels at every tier: its cell at tier T is `w × T / 128` by `h × T / 128` px. The plugin draws it at
  `min + box.xy × S / 128`, `box.wh × S / 128` px for a medal of side S. A badge's box is always `[64, 64, 64, 64]`.
- `@2x` is the layout doubled, as §2. Rects follow §2's padding rules.
- **Faces sprites:** `<state>` (the under layer) for all 8 states, required; `<state>-over` only for states with
  overhangs. Ready on another job's face leaves the badge slot empty.
- **Frames sprites:** `frame-<urgency>-<finish>` for urgency `act-now`, `resting`, `finished`, `ghost` and finish
  `full`, `quiet` (8, required), and `badge-open`, `badge-closed`, `badge-journal`, `badge-seat-tank`,
  `badge-seat-healer`, `badge-seat-dps`, `badge-seat-hand` (7, required).

**`faces-row.json`, `frames-row.json` (row strips)**: `size`, `sizes` (12 … 31, contiguous) and `sprites`, the same
sprite names (no badges in a row strip: the badge content goes beside the medal at text height), each cell the whole
128-unit box rendered at that exact size. 1x only.

Parsed by `Tsukimichi.Core/Ui/Themes/FrameParts.cs` (`PartAtlasLayout`); drawn by `ThemeAtlasCache.TryCompose`.

**Gates.** The build composes every set's faces in every kit as the plugin does and runs the per-set gates on each
(G1, G1c and G2 per tier group, G2L on the row tier), recorded in the kit's `metrics.json` under `faces`. A set in its own
kit is its shipped composites and must pass; any other pairing is a user's frames choice, and a gate it misses is a
warning (`warnings`), never a build failure (theme-system §5.2). What the Frames row says is each pairing's `flags`: every
pair of states under the bars at 16 px (greyscale and deuteranopia 12, Machado's 11; "hard" under 10), Ready leading by
under 1.25, Completed over 0.8 of Ready. The build compiles them into `Tsukimichi.Core/Ui/Themes/FrameKitChecks.g.cs`
(`--check` covers it), since `metrics.json` is not packaged. The cross-set table frames every face in the neutral kit,
Brass, whose four urgency tiers are one bezel, so the frame cancels and the faces decide; it is recorded worst-of-modes
(`cross.sets`) and per vision mode (`cross.modes`).

## 8. Kit ornaments: `ornaments.json` (1.17 T15)

A frame kit may ship its Decoration ornament as sprites (theme-system §3.3: "sigil sprite, corner marks"). Kirikane does;
the other kits recolour the palette's drawn ornament (`FrameKitMetals`). One 1x PNG of whole-device-pixel cells, each a
real render at that size:

```json
{
  "size": [W, H],
  "note": "free text",
  "sprites": {
    "sigil": { "13": [x, y, 13, 13], ..., "32": [...] },
    "sigil-small": { "10": [...], "11": [...], "12": [...] },
    "lozenge": { "2": [...], ..., "6": [...] },
    "corner": { "15": [...], ..., "32": [...] }
  }
}
```

- All four sprites are required, each at a contiguous range of sizes; cells are square, ≥ 1 px inside the image and
  ≥ 2 px apart (§2). Parsed by `Tsukimichi.Core/Ui/Themes/KitOrnaments.cs` (`KitOrnamentLayout`).
- **The sigil's size rule** (`KitOrnaments.Sigil`; the Sumi to Kinpaku concept, Round 2 tidy-up 3): `sigil` from 13 px;
  `sigil-small` at 10–12 px only; below 10 px never a crest but `lozenge`, 2 px times the UI scale. A size above a
  sprite's range draws its largest cell scaled.
- **`corner`** is the top-left mark; the plugin mirrors it into the other corners by UVs. Its L's outer corner sits on
  the frame's corner, its arms about the drawn Brass L's length, and it never draws under 15 px, so its leaf bar (2.2 of
  32 units) is a whole device pixel (`KitOrnaments.CornerBox`).
- Drawn on a dark standard-contrast palette only (`FrameKitMetals.DrawsOrnamentSprites`): the sprites are gold leaf. A
  light palette keeps its own ornament, designed for 3 : 1 on snow, and high contrast its strong line. Callers draw the
  palette's star and L while the strip loads (`ThemeAtlasCache.TryDrawOrnament`).
- **Gates** (the kit's `metrics.json`, `ornaments.gates`): the size rule, fit (no bleed, not cut), and the leaf's contrast
  on Night's, Dawn's and Kugane Lacquer's windows (≥ 3 : 1, the ornament's bar). The kit's PNG budget (§6) includes it.

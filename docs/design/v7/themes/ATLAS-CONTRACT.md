# The per-set atlas contract (1.16.0, T4 ⇄ T5)

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
  row.png           optional   whole-pixel row strips (schema §3); 1x only
  row.json          optional
  plain.png         optional   the flat (Decoration Plain) finish at hero tiers, schema §2
  plain@2x.png      optional
  plain.json        optional
  faces.*           reserved   unframed faces for the frames axis (1.17 T11); schema §2, 8 sprites
  frames.*          reserved   a frame kit's frames and badges (1.17 T11); schema §2
  metrics.json      optional   the build's gate numbers (§5); not read by the 1.16 runtime
```

- `<set-key>` is the set's key in `GlyphSets` (`Tsukimichi.Core/Ui/Themes/GlyphSets.cs`), and the folder name must
  match it exactly. The keys are stable forever:

  | Id | Key | Name | Kind |
  |---|---|---|---|
  | 1 | `medallion` | Menphina's Medallion | procedural + embedded atlas (unchanged) |
  | 2 | `classic` | Classic | procedural (`LegacyMoonGlyph`) |
  | 3 | `aether-crystal` | Aether Crystal | atlas (this contract) |
  | 4 | `ishgard-glass` | Ishgard Glass | atlas (this contract) |
  | 5 | `astrologian-orrery` | Astrologian's Orrery | atlas (1.17) |
  | 6 | `sumi-to-kinpaku` | Sumi to Kinpaku | atlas (1.17) |

- Every `.png` and `.json` under `Tsukimichi/assets/ui/themes/` is packaged as a content file (the csproj already
  globs the folder) and loaded from disk with `ITextureProvider.GetFromFile`, so new sets never grow the DLL. Nothing
  else needs registering: dropping a valid folder in makes the set drawable.
- A set whose folder is missing, or whose `medals.json` does not parse, is not drawable; the plugin draws Menphina's
  Medallion for its states instead (the stand-in rule, §4). `ThemeAtlasTests` fails the build on a malformed folder.

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
  Without it, Decoration Plain draws Medallion's Plain ladder for this set at hero sizes.
- Extra sprite keys are allowed and ignored. Unknown top-level keys are ignored.

## 3. Row strips: `row.json`

```json
{
  "size": [W, H],
  "sizes": [12, 13, 14, ..., 31],
  "finishes": ["full", "plain"],
  "note": "free text",
  "sprites": {
    "full":  { "ready": { "12": [x, y, 12, 12], "13": [...], ..., "31": [...] }, ... },
    "plain": { ... }
  }
}
```

- One PNG, 1x only: row cells are already in **device** pixels, so there is no `@2x`.
- `sizes` is every whole device pixel from **12 to 31** (the row tier is everything under 32 px, `MedalLayout.RowTierMaxPx`).
  Each cell is a **real render at that size**, not a downscale.
- `finishes`: `full` is required; `plain` is optional (the flat master); `quiet` is optional (a hairline-rim variant
  for Decoration Quiet; without it Quiet draws `full`).
- Required sprite keys per finish, the 8 states (row cells carry **no badge**; the plugin draws the badge's content
  beside the medal at text height, as it does for Medallion):
  `ready`, `ready-on-another-job`, `in-journal`, `blocked`, `done-this-cycle`, `completed`, `locked-out`, `not-checked`.
- A cell is the 128-unit box at N × N px, `w == h == N`. Same padding rules as §2 (≥ 2 px apart, ≥ 1 px inside).

## 4. How the runtime uses them (for the art's sake)

`MedalGlyph.Box` snaps every medal to a whole-pixel square of side S (device px). For a state drawn by an atlas set:

| S | Full / Quiet | Plain |
|---|---|---|
| ≥ 32 (hero) | `medals` at the smallest tier ≥ S (1x, then 2x above 128); never shrunk more than 1.5× | `plain` if present, else Medallion's Plain ladder |
| 12–31 (row) | `row` `full` (or `quiet`) cell of exactly S | `row` `plain` cell of exactly S, else Medallion's Plain ladder |
| < 12 | the 12 px row cell, shrunk | as above |

- Loading is lazy per set (`ThemeAtlasCache`): only sets the current appearance uses are requested; `row` loads first,
  `medals` on the first hero draw, `@2x` only above 128 px; a set that drops out of the appearance is released after
  a short idle.
- Until a texture has loaded, and whenever a file is missing, **Medallion's procedural medal of the same state stands
  in**, so a row is never blank.
- High contrast ignores every set and draws Medallion's high-contrast ladder (theme-system §3.4).

## 5. `metrics.json` (optional, owned by T4)

The per-set gate results the build computed (theme-system §7.1), for reviewers now and for the mix warnings later
(1.17 T10). The runtime does not read it in 1.16, so T4 owns the schema. Please keep at least:

```json
{ "set": "<set-key>", "build": "<chrome version, date>", "gates": { "G1": {...}, "G2": {...}, ... },
  "salience": { "<state key>": number }, "distinctness": { "<state key>|<state key>": number } }
```

## 6. Budgets (asserted by `ThemeAtlasTests`)

- Per set, 1x textures (`medals` + `row` + `plain`): **≤ 4 MB** of RGBA (W × H × 4 summed).
- Per set, the `@2x` textures: **≤ 12 MB** of RGBA.
- Per set, all PNG files on disk: **≤ 2.5 MB**.
- Every reachable appearance (any mix of the shipped sets, 1x): **≤ 12 MB** of RGBA in total, Medallion included.

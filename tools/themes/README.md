# The multi-theme build

`build_themes.py` turns each glyph set's approved SVG masters into the plugin's atlases and checks them (feature plan v7 T4; `docs/research/plan-v7/theme-system.md` §6.4 and §7.1). Since 1.17 (T11) it also writes each set's unframed faces and each frame kit's frames and badges, and gates every set's faces in every kit. It replaces `docs/design/moon-v6/round5/gen_atlas.py`, which stays as history; Medallion's manifest rebuilds that script's atlas byte for byte.

```
python tools/themes/build_themes.py                  # every set: atlases, gates, cross-set table, sheets
python tools/themes/build_themes.py --set ishgard-glass
python tools/themes/build_themes.py --check          # rebuild into a temp folder, diff against the repo, exit 1 on a difference, a file no build writes, or a failed gate
python tools/themes/build_themes.py --out DIR        # contact sheets and report.txt (default: %TEMP%/tsukimichi-themes)
```

Every run builds every kit (`kits/*.json`). Needs Python 3 with numpy and Pillow, and Chrome (the same headless renderer as `gen_atlas.py`). Pixels depend on Chrome's version, which `metrics.json` records; the C# tests compare layouts and numbers, never pixels, and check that each committed PNG is the one `metrics.json` was measured from (its SHA-256).

## Manifests (`sets/<set>.json`)

| Field | Meaning |
|---|---|
| `root` | The set's design folder. Paths are relative to it; a path starting with `/` is relative to the repo. |
| `tiers` | The source folder per hero tier (48, 64, 96, 128). `{tier}` in a path becomes that folder, so Ishgard Glass draws `_mid/` at 48 and 64 px and its full masters at 96 and 128. |
| `sprites` | The eleven atlas sprites, in Medallion's order. A sprite is a file, a `{"48": …}` map of files per tier, `{"layers": [...]}` stacked bottom to top (a layer may `recolour` hexes), or `{"python": …, "call": …, "args": […]}`. |
| `metrics` | Ready on another job's composites with job icons, measured in place of the empty-seat sprites. |
| `row` | The row-tier masters, and whether the set ships a row strip (`atlas`). Medallion's row tier is procedural, so it only measures. |
| `dest`, `atlasDest` | Where the set's files go. Medallion's atlas stays at `Tsukimichi/assets/ui/`. |
| `kit` | The set's own frame kit (a `kits/<kit>.json`): its composites are drawn as designed in it, and composed in any other. |
| `faces` | The unframed faces: `hero` (per tier, `{tier}` as in `sprites`) and `row`. A spec is a path with `{state}` and `{layer}` (`under`, `over`), a `{"under": …, "over": …}` pair, or a python source (Medallion's faces come from `sources.py`, which cuts them from gen5.py read-only). |
| `plain` | Optional (Sumi to Kinpaku, 1.17 T15): the set's own flat finish for Decoration Plain, `dir` and one master per state in `sprites` (every size, no frame, no badge). It needs `row.atlas`: the strip is then written in ATLAS-CONTRACT §3's nested form, `full` then `plain`. Without it, Plain shows Medallion's Plain ladder. |

## Kit manifests (`kits/<kit>.json`)

| Field | Meaning |
|---|---|
| `root`, `dest` | The kit's design folder, and where its files go (`Tsukimichi/assets/ui/kits/<kit>/`). |
| `frames` | `hero` and `row` specs with `{urgency}` (`act-now`, `resting`, `finished`, `ghost`) and `{finish}` (`full`, `quiet`); a `{"full": …, "quiet": …}` pair picks by finish. |
| `badges` | The seven badges at the shared slot: `open`, `closed`, `journal` (ring, seat and glyph), and `seat-tank`, `seat-healer`, `seat-dps`, `seat-hand` (ring and empty role seat). Layers or python sources (`sources.py` for Brass and Silver). |
| `ornaments` | Optional (Kirikane, 1.17 T15): the kit's Decoration ornament sprites, `sigil`, `sigil-small`, `lozenge` and `corner`, each `{"file": …, "sizes": [first, last]}` (whole device pixels). The sizes must keep the sigil's size rule: `sigil` from 13 px, `sigil-small` within 10–12, `lozenge` under 10, `corner` from 15. |

## What it writes

Per set, under `Tsukimichi/assets/ui/themes/<set>/`:
- `medals.png`, `medals@2x.png`, `medals.json`: the hero atlas, in Medallion's cell layout (`MedalLayout`), with the same sprite names. Ready on another job ships once per role seat, the seat left empty for the game's job icon.
- `row.png`, `row.json`: each state's row-tier master at every whole device pixel from 12 to 31, rendered at that size; for a set with a `plain` finish, the nested form (ATLAS-CONTRACT §3) with every state's Plain master on shelves of their own below.
- `faces.png`, `faces@2x.png`, `faces.json`, `faces-row.png`, `faces-row.json`: the set's unframed faces (ATLAS-CONTRACT §7): each state's under layer and, where it has one, its over layer, each sprite cropped to its box of the 128-unit box (8-unit grid) at the hero tiers, and whole cells at every pixel from 12 to 31.
- `metrics.json`: the gate results and the numbers behind them, the set's half of the cross-set table, and the SHA-256 of each PNG the set ships (`pngs`). It is not packaged with the plugin.

Per kit, under `Tsukimichi/assets/ui/kits/<kit>/`: `frames.png`, `frames@2x.png`, `frames.json`, `frames-row.png`, `frames-row.json` (the four urgency tiers at Full and Quiet, and the seven badges at hero tiers), for a kit with `ornaments` its `ornaments.png` and `ornaments.json` (each sprite at every whole device pixel of its range, 1x; ATLAS-CONTRACT §8), and `metrics.json` (below).

Contact sheets, the cross-set heatmaps, `mix-sheet.png` (every set's faces in every kit, composed from the shipped atlases exactly as the plugin composes them) and `report.txt` go to `--out`, never into the repo.

## Gates (fail the build; `ThemeAtlasTests` asserts them again from `metrics.json`)

On the Night window, for every hero tier group and the row tier, at 16 px in a 40 px cell (round 5's `metrics.py`):
- **G1:** the weakest pair of states is at least 12 at 16 px and 16 at 20 px, in greyscale and in (Vienot) deuteranopia. Ready on another job counts its worst job.
- **G2:** Ready is at least 1.3× the next state's salience; Completed is at most 0.8× Ready; every state but Not checked is at least 15.
- **G1c (colour vision):** on every ground (Night, Ishgard Snow, daylight), the weakest pair at 16 px is at least 11 under Machado protanopia, deuteranopia and tritanopia (the realism supervisor's bar for 1.16.0; Medallion's row tier sits at 11.1 under protanopia).
- **G2L (Ready on a light palette):** on Ishgard Snow (`#EEF1F6`), for the row tier at 16 and 20 px, with Ready over its warm wash (below):
  - Ready is at least 1.3× the next state under the **weighted** OKLab difference, `sqrt((ΔL/3)² + Δa² + Δb²)`. If a set misses that at either size, it must reach 1.3 under **chroma** only, `sqrt(Δa² + Δb²)`. `light.measure` records which measure passed.
  - **Lightness floor:** Ready's plain luminance salience (round 5's greyscale salience) is at least 0.70× the next state's, leaving Not checked out of "next state". Its dark face outweighs Ready's light one in plain luminance, so the `luma` leads are recorded that way.
  - **Not checked under Ready:** Not checked's chroma salience is below Ready's.
  - Not checked still counts in every distinctness and colour-vision gate.
- **Fit:** nothing outside the cells, and no sprite cut by its cell.
- **Plain (1.17 T15):** a set's `plain` masters are measured as a tier group of their own (`plain`, no atlas tiers) and held to G1, G1c, G2 and G2D like every other group. G2L stays on the row tier: the light-palette measure's unmatte check (Chrome's render reproduced within 6/255) misses one edge pixel of Sumi's Plain Locked out at 20 px (7.7/255), so the Plain group is not measured on Ishgard Snow.

Distinctness and salience are judged at one decimal, as round 5 judged them, and ratios at two. Everything else (other grounds and sizes) is recorded under `survey` for reviewers.

**How G2L measures (`light`).** On Ishgard Snow a Ready row's glow becomes a warm wash: `#F2D27A` at .75 within 3 px for every set (spec-1.16 §A4.1). `readyWash` is that wash, and `ThemeAtlasTests` pins it. The build draws each row-tier state on black and white mattes, recovers its colour and alpha, and lays Ready over the wash. It then sums each measure per pixel against the window over the 40 px cell. Ready on another job counts its loudest job.

Each measure is recorded with no wash, the shipped wash and the old fallback (.90 within 4 px) at 16 and 20 px. Only the shipped wash is gated. Full OKLab difference is not used: it is mostly lightness, so on a light page every dark-faced state outweighs Ready's light face, and no set reached 1.3 under it with either wash.

- **G2D (the dark palettes, 1.17):** on Dawn's and Kugane Lacquer's windows (read from `docs/design/v7/ui/1.17/palettes17.json`), for every tier group at 16 and 20 px in greyscale, Ready is at least 1.3× the next state and Completed at most 0.8× Ready; and every mix (Ready from one set, the rest from another, in the neutral kit, row tier and 48 px) keeps Ready at least 1.25×. Ready's halo on a dark palette is Moon gold in the wash's 3 px footprint; each variant is recorded (none, the shipped .45, the raised .60) and each palette's gate uses the least under which every set and mix passes, recorded as `dark.halo`. Medals are never recoloured.

## Kit ornaments (1.17 T15)

A kit's `ornaments` strip is gated in its `metrics.json` under `ornaments.gates`, and the kit passes only if they do: the size rule (above), fit (no bleed, not cut), and every sprite's WCAG contrast (`sigil`, `sigil-small`, `lozenge` and `corner`, each at its largest and smallest size) on every dark window, at least 3 : 1 (the ornament's bar; the plugin draws the sprites on dark standard-contrast palettes only): Night's, Dawn's and Kugane Lacquer's windows, and two references for Follow Dalamud on a dark host, whose window is the player's Dalamud style: `#0F0F0F` (Dalamud's dark styles' window background, ImGui's dark `WindowBg` at rgb 0.06, opaque) and `#141414` (that translucent window over a dark scene, the Dalamud default the glyph designs were checked on). Its PNG's SHA-256 is in the kit's `pngs`.

## Faces in every kit (1.17 T11)

Each kit's `metrics.json` holds every set's faces composed in that kit as the plugin composes them (face under, the frame for the state's urgency tier, face over, the badge from 32 px), held to G1, G1c, G2 and G2L. A set in its own kit (`own`) is measured on its shipped composites and must pass; any other pairing is a frames choice the player makes, and a gate it misses is recorded under `warnings`, never failing the build (theme-system §5.2: measured checks warn). `ThemeAtlasTests` re-judges every recorded verdict against its own bars. Each pairing's `flags` (pairs under the G1 and G1c bars at 16 px, and under G1's 16 at 20 px when 16 px does not already flag the pair, each with the `px` it was read at, "hard" under 10; Ready leading by under 1.25; Completed over 0.8 of Ready) are what Settings › Themes › Frames says in words; the build compiles them into `Tsukimichi.Core/Ui/Themes/FrameKitChecks.g.cs`, which `--check` covers and `FrameKitChecksTests` holds to the JSON.

## Cross-set table (§5.2)

For every ordered pair of sets and every pair of different states, the build records the distinctness of the two side by side, worst over all five vision modes, at 16 and 20 px, for the row tier and the 48 px hero tier. It also records Ready's lead when Ready comes from one set and the other states from another. Values under 12 are "close" and under 10 "hard to tell apart"; these warn on the Themes page and never fail the build. Since 1.17 every face is framed in the neutral kit, Brass (one bezel for every urgency tier, so the frame cancels and the faces decide), as a mix draws one kit for the whole column. `cross.sets` keeps the worst over all vision modes; `cross.modes` records each mode (`grey`, `deut`, `machado-deut`, `machado-prot`, `machado-trit`) as `{other set: {tier: {px: {mode: {"this state|other state": d}}}}}`, so the mix table can hold greyscale and deuteranopia to 12 and Machado's modes to 11, as each set's own gates do.

`cross.neutralSalience` is the set's salience per state in the neutral kit (16 px, greyscale, Night) for the row tier and the 48 px hero tier, so a mix of any number of sets can be judged for Ready's lead (1.25×) and Completed's share (0.80×) at both tiers. The build compiles every measured set's own pairs and the cross pairs per mode (row tier, 16 px, Night) and this salience into `Tsukimichi.Core/Ui/Themes/MixTable.g.cs` (the JSON is not packaged), which Settings › Themes › Mix moons by state and the glyph window's Themes tab read; `--check` covers it and `MixTableTests` holds it to the JSON. A set joins the table (and the per-state lists) once its `metrics.json` carries the cross table against every other measured set.

# The multi-theme build

`build_themes.py` turns each glyph set's approved SVG masters into the plugin's atlases and checks them (feature plan v7 T4; `docs/research/plan-v7/theme-system.md` §6.4 and §7.1). It replaces `docs/design/moon-v6/round5/gen_atlas.py`, which stays as history; Medallion's manifest rebuilds that script's atlas byte for byte.

```
python tools/themes/build_themes.py                  # every set: atlases, gates, cross-set table, sheets
python tools/themes/build_themes.py --set ishgard-glass
python tools/themes/build_themes.py --check          # rebuild into a temp folder, diff against the repo, exit 1 on a difference or a failed gate
python tools/themes/build_themes.py --out DIR        # contact sheets and report.txt (default: %TEMP%/tsukimichi-themes)
```

Needs Python 3 with numpy and Pillow, and Chrome (the same headless renderer as `gen_atlas.py`). Pixels depend on Chrome's version, which `metrics.json` records; the C# tests compare layouts and numbers, never pixels.

## Manifests (`sets/<set>.json`)

| Field | Meaning |
|---|---|
| `root` | The set's design folder. Paths are relative to it; a path starting with `/` is relative to the repo. |
| `tiers` | The source folder per hero tier (48, 64, 96, 128). `{tier}` in a path becomes that folder, so Ishgard Glass draws `_mid/` at 48 and 64 px and its full masters at 96 and 128. |
| `sprites` | The eleven atlas sprites, in Medallion's order. A sprite is a file, a `{"48": …}` map of files per tier, `{"layers": [...]}` stacked bottom to top (a layer may `recolour` hexes), or `{"python": …, "call": …, "args": […]}`. |
| `metrics` | Ready on another job's composites with job icons, measured in place of the empty-seat sprites. |
| `row` | The row-tier masters, and whether the set ships a row strip (`atlas`). Medallion's row tier is procedural, so it only measures. |
| `dest`, `atlasDest` | Where the set's files go. Medallion's atlas stays at `Tsukimichi/assets/ui/`. |

## What it writes

Per set, under `Tsukimichi/assets/ui/themes/<set>/`:
- `medals.png`, `medals@2x.png`, `medals.json`: the hero atlas, in Medallion's cell layout (`MedalLayout`), with the same sprite names. Ready on another job ships once per role seat, the seat left empty for the game's job icon.
- `row.png`, `row.json`: each state's row-tier master at every whole device pixel from 12 to 31, rendered at that size.
- `metrics.json`: the gate results and the numbers behind them, and the set's half of the cross-set table.

Contact sheets, the cross-set heatmaps and `report.txt` go to `--out`, never into the repo.

## Gates (fail the build; `ThemeAtlasTests` asserts them again from `metrics.json`)

On the Night window, for every hero tier group and the row tier, at 16 px in a 40 px cell (round 5's `metrics.py`):
- **G1:** the weakest pair of states is at least 12 at 16 px and 16 at 20 px, in greyscale and in (Vienot) deuteranopia. Ready on another job counts its worst job.
- **G2:** Ready is at least 1.3× the next state's salience; Completed is at most 0.8× Ready; every state but Not checked is at least 15.
- **Fit:** nothing outside the cells, and no sprite cut by its cell.

Distinctness and salience are judged at one decimal, as round 5 judged them. Machado deuteranopia, protanopia and tritanopia, and the Ishgard Snow and daylight grounds, are recorded under `survey` for reviewers: Medallion's shipped row tier itself sits at 11.1 under protanopia, so they are not gates yet. On a light ground, salience measures difference from the window, so dark states outshine Ready by definition; that is why the Ready-lead gate runs on Night only.

## Cross-set table (§5.2)

For every ordered pair of sets and every pair of different states, the build records the distinctness of the two side by side, worst over all five vision modes, at 16 and 20 px, for the row tier and the 48 px hero tier. It also records Ready's lead when Ready comes from one set and the other states from another. Values under 12 are "close" and under 10 "hard to tell apart"; these warn on the Themes page and never fail the build. Each set is compared framed in its own kit, as it ships; a neutral-frame table needs separate face and frame atlases (mix and match, 1.17).

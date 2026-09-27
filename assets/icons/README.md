`tsukimichi.svg` is the hand-authored source for the plugin icon (crescent moon over a winding path with three lit stones); `render_icons.py` redraws the same geometry with Pillow to produce `assets/icon.png` (512 px), `assets/icon-64.png`, and `moon-phases-preview.png` (the eight quest-state moon glyphs from spec 2.1, in table order left to right).
Re-render with `python assets/icons/render_icons.py` (Python 3 with Pillow; no other dependencies, no fonts, no external images).
All artwork here is original, built from geometry only, and is released together with the project under its license.

"""Writes Tsukimichi.Tests/Fixtures/font-advances.json: the advance widths (in em) of the characters Tsukimichi's
text uses, from Dalamud's default UI font (Noto Sans CJK JP Medium, shipped in dalamudAssets as UIRes/NotoSansCJKjp-Medium.otf).

The layout tests (Tsukimichi.Tests/Localization/LayoutBudgetTests.cs) measure every language's labels with it against
Core's LayoutBudgets. Re-run after a font change:

  python -m pip install fonttools
  python tools/font-advances.py "%APPDATA%/XIVLauncher/dalamudAssets/dev/UIRes/NotoSansCJKjp-Medium.otf"

Ranges: Basic Latin, Latin-1, Latin Extended-A, General Punctuation, arrows, CJK punctuation, kana, the full-width
forms. Every other character (kanji among them) is measured as one em by the tests, which is what this font gives it.
"""
import json
import os
import sys

from fontTools.ttLib import TTFont

RANGES = [(0x20, 0x7E), (0xA0, 0xFF), (0x100, 0x17F), (0x2000, 0x206F), (0x2190, 0x21FF), (0x2600, 0x26FF),
          (0x3000, 0x303F), (0x3040, 0x30FF), (0xFF00, 0xFFEF)]


def main(path):
    font = TTFont(path)
    upm = font['head'].unitsPerEm
    cmap = font.getBestCmap()
    hmtx = font['hmtx']
    advances = {}
    for lo, hi in RANGES:
        for cp in range(lo, hi + 1):
            glyph = cmap.get(cp)
            if glyph is not None:
                advances[f'{cp:04X}'] = round(hmtx[glyph][0] / upm, 4)
    out = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Tsukimichi.Tests', 'Fixtures', 'font-advances.json')
    with open(out, 'w', encoding='utf-8', newline='\n') as f:
        json.dump({'font': os.path.basename(path), 'unitsPerEm': upm, 'advances': advances}, f, indent=0, sort_keys=True)
        f.write('\n')
    print(f'{out}: {len(advances)} characters')


if __name__ == '__main__':
    main(sys.argv[1])

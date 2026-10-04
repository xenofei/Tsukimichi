# Portrait audit, Team C (visual method)

4 October 2026. Files in this folder: `findings.json` (1,508 rows), `review/review.json` (454 rows; its PNGs stay local), `sheets/` (25 contact sheets, local only: Square Enix art). Scripts and the probe: the session scratchpad `portrait-audit/team-c/` (`probe/`, `plate.py`, `det/detect.py`, `analysis.py`, `export.py`, `visual.json`).

## Outcome

- **Checked:** 1,503 portraits, each rendered on its plate exactly as the plugin draws it: `PortraitGrading` rounding, the delivery masks, the night grade, and the 69/72 face rect clipped to a circle of radius 34.5/72. Rendered at 1x (72 px) and 2x (144 px).
  - **241 game-art icons:** every face matched to a giver, every `iconCrops` entry and every curated face. 130 of them are what a quest shows today.
  - **1,262 pack images,** covering 1,921 named givers.
- **The owner's two examples.** Varshahn and Estinien have no `iconCrop`, so their Endwalker Trust busts (072650, 072644) use the family default box (6, 86, 140). Both heads sit about 60 hr px higher than that box expects, so the plate shows chin and chest with the eyes at the top edge: an eye line of 0.07 and 0.11 against the 0.44 target. Same cause: Wuk Lamat 072661 (the worst, on 36 quests), Venat, Koana, Alisaie 072622 and Ryne 072627.
- **The pack sits low everywhere.** `PortraitHeadCrop` takes the chin at the neck's narrowest row, which is below the real chin. Across 895 confident photos the median eye line is 0.574 (target 0.43) and the median chin 0.87. Sideways centring is fine: only 55 photos are off by more than 0.10. One builder fix (detector landmarks, or the box moved up by about 0.14 of its side) would correct most of the 578 off-centre and 163 cut pack photos, which reach 1,992 quests.
- **No portrait shows a later look than its arc.** `Pick` never chooses a later face. Some arcs reuse an earlier look (see "Coverage by arc").

## Counts per status

| | game art (241) | pack (1,262) |
|---|---|---|
| ok | 51 | 145 |
| off-centre | 75 | 578 |
| cut | 34 | 163 |
| too loose | 0 | 9 |
| wrong look | 1 | 0 |
| review | 80 | 367 |

There are also 5 missing-arc rows. Of the 130 portraits shown today: 38 ok, 35 off-centre, 18 cut, 39 review.

**Tolerances,** as fractions of the plate (the circle inscribes the crop box):
- **off-centre:** eye midpoint more than ±0.10 from centre, or eye line outside 0.38–0.50 (the spec's 0.42–0.46 plus 0.04 for noise).
- **cut:** eye line below 0.30, chin above 0.97, or eye-to-chin above 0.55.
- **too loose:** eye-to-chin below 0.26.
- `offsetPx` is the eye midpoint's distance from (0.5, 0.44) on the 72 px plate.

## Worst 20 among portraits shown today

Icon, character, family, status, offset at 72 px, suggested crop. Before and after renders: `sheets/flagged-game-*.png`.

| # | Icon | Character | Family | Status | Offset | Suggested |
|---|---|---|---|---|---:|---|
| 1 | 072661 | Wuk Lamat | bust | cut | 28.1 | eyes [0.397, 0.194], chin 0.330 (box 0, 15, 177) |
| 2 | 072650 | Varshahn | bust | cut | 27.5 | eyes [0.457, 0.198], chin 0.316 (box 10, 28, 153); confidence 0.96 |
| 3 | 072644 | Estinien | bust | cut | 26.3 | eyes [0.288, 0.211], chin 0.324 (box 0, 37, 146); confidence 0.67, confirmed by eye |
| 4 | 087202 | Hien | card | off-centre | 24.5 | box 14, 51, 124 (curated eyes 0.14 too far right) |
| 5 | 087453 | Fahrafahr | card | cut | 23.7 | box 70, 14, 104 |
| 6 | 087067 | Raubahn | card | off-centre | 19.8 | box 14, 20, 158 |
| 7 | 072659 | Krile | bust | cut | 18.9 | box 3, 169, 150 |
| 8 | 072647 | Venat | bust | cut | 17.8 | box 14, 50, 150 |
| 9 | 087259 | Grenoldt | card | off-centre | 17.3 | box 85, 26, 109 |
| 10 | 072663 | Koana | bust | off-centre | 16.3 | box 28, 69, 156 |
| 11 | 087374 | Menphina | card | off-centre | 16.2 | box 104, 47, 78 |
| 12 | 087165 | Thancred (HW) | card | off-centre | 14.8 | box 60, 34, 121 |
| 13 | 073237 | Ahtbyrm | battle talk | off-centre | 14.4 | box 169, 125, 204 |
| 14 | 072622 | Alisaie | bust | cut | 13.8 | box 17, 98, 155 |
| 15 | 073250 | Owyne | battle talk | off-centre | 13.7 | box 142, 103, 243 |
| 16 | 073168 | Hoary Boulder | battle talk | off-centre | 13.5 | box 151, 112, 232 |
| 17 | 072627 | Ryne | bust | cut | 13.4 | box 17, 93, 160 |
| 18 | 087338 | Fourchenault | card | off-centre | 12.9 | box 27, 41, 111 |
| 19 | 073196 | Trachtoum | battle talk | off-centre | 12.9 | box 148, 112, 235 |
| 20 | 087121 | Honoroit | card | cut | 12.7 | box 43, 14, 110 |

Patterns:
- Every Trust bust without an `iconCrop` is wrong.
- Every battle-talk face on the default box is 0.15–0.20 to the right of the face.
- About 10 curated card measurements are off by 0.1–0.34 (Hien, Raubahn, Grenoldt, Menphina, HW Thancred, Fourchenault and others).

Outside the shown set: Alphinaud's ShB bust 072621 has a curated box clamped at the left edge that leaves him left of centre (dx −0.14). Charlemend's delivery portrait 061669 has no `iconCrop` (never shown).

## The owner's examples in detail

- **Varshahn:** bust 072650 cut (above). Strip 072710 (never shown): dx −0.23, suggested box 371, 0, 180. Pack photos 1038053 and 1039663: eye line 0.52, the pack's general offset.
- **Estinien:** bust 072644 cut (above). Card 087088, shown on his 6 HW and ShB quests, is the helmeted dragoon with no face to centre. Battle talk 073136 and strip 072705 are also faceless. Battle talk 073007 (never shown): dx +0.24, suggested box 196, 166, 172. All four pack photos (1007115, 1012111, 1029535, 1036120) show a dark helmet or armour with no face.

## Coverage by arc

See `sheets/coverage-1/2.png` and `sheets/looks-1/2/3.png`.

- **No game art:** Alphinaud ARR (20 quests); Alisaie ARR and HW (2 + 2); Krile HW (1); G'raha Tia ARR (2, a deliberate curated block). With the pack on, all of them get a period-correct photo.
- **Art with no face:** Estinien HW and ShB (the helmet card; the pack doesn't help). Yugiri ARR: 073167 shows her hood from the side.
- **Earlier looks reused in later arcs (acceptable):** Alphinaud's HW 073034 in SB; Tataru's SB card through DT; Krile's SB card in ShB and EW; Cid's ARR card in SB.

## Detectors and eye agreement

MediaPipe Face Landmarker (iris, chin, forehead), OpenCV YuNet (chin estimated as eye + 1.74 × (mouth − eye)) and the anime LBP cascade, all run in isolation via `uv run --python 3.12`.

| | game art (241) | pack (1,262) |
|---|---|---|
| MediaPipe and YuNet both fire | 171 | 1,006 |
| … agree within 0.05 | 144 (84 %) | 903 (90 %) |
| … median disagreement | 0.025 | 0.011 |
| YuNet only | 48 | 49 |
| MediaPipe only | 1 | 71 |
| Cascade only | 4 | 7 |
| No detector | 17 | 129 |

By eye (every sheet viewed; 33 hand verdicts): detectors put a face on a non-face 5 times (goblin masks 087036 and 087117, moogle 087115, helmeted 073031, and staff J'olhmyn 1024752 at 0.857, the only confident false accept among pack photos). Five faces the detectors missed were measured by hand: 073164, 073156, 073181, 072629 and 072689.

## Confidence and threshold

`confidence = 0.25 × detector + 0.30 × agreement + 0.25 × visual + 0.20 × plausibility`, with each component stored in `findings.json` and `review.json`.
- **detector:** the MediaPipe hit plus the YuNet score.
- **agreement:** how closely MediaPipe and YuNet agree.
- **visual:** 1 confirmed by eye, 0.75 no objection, 0.5 unsure, 0 wrong (which caps confidence at 0.4).
- **plausibility:** both eyes found, span plausible, not cut, eye line in band.

**Threshold 0.78.** Calibrated on 80 pack photos sampled across the range and judged by eye: below 0.70, about 9 of 32 had landmarks on the face; 0.70–0.78, 7 of 9; 0.78 and above, 39 of 39. Raised from 0.75 so the one miss in the middle band (the hooded Crystal Exarch at 0.775) falls below it.

460 portraits fall below 0.78; 447 are `status: "review"`, and the rest were confirmed by eye (for example Estinien 072644) or hand-set to ok.

## Review set

`review/review.json`: 454 rows (85 game art, 369 pack), every review portrait plus all 14 Varshahn and Estinien portraits. Each row has the full source PNG at native size (`icon-<id>.png` or `npc-<id>.png`, the full Garland photo for pack), the character, source and era, the current and suggested crop (curated format and normalised {cx, cy, size, sizeH}), the detector's face box normalised over the source, the landmarks, the confidence and its components, and a one-line reason.

## Crop format

**Curated file.** `crops` and `iconCrops` take one of two forms:

1. `{ "box": [x, y, side] }`: a square in px of the family's hr texture. `FromBox` gives UVs (x/W, y/H, (x+side)/W, (y+side)/H) and ignores the entry if any UV falls outside 0–1.

   | Family | W × H |
   |---|---|
   | TrustBust | 188 × 480 |
   | TripleTriadCard | 208 × 256 |
   | BattleTalk | 640 × 512 |
   | Delivery | 400 × 480 |
   | TrustStrip | 640 × 180 |

2. `{ "eyes": [u, v], "chin": c }`: `CropFor` solves the framing rule: `size = min((c − v)·H/0.37, art extent)`, `left = clamp(u·W − size/2)`, `top = clamp(v·H − 0.44·size)`. Card art bounds are x 0.067–0.933 and y 0.055–0.945 (x 13.9–194.1, y 14.1–241.9 px).

**Drawing.** `PortraitGrading` reads pixels round(U0·W)…round(U1·W) and round(V0·H)…round(V1·H) from the hr texture (half-to-even, clamped), applies the mask and grade, and box-filters to 48 px when drawn at 48 px or less. `Chrome.Portrait` draws it into the face rect, inset 1.5/72 of the plate on every side, rounded at half its size. The circle inscribes the box exactly; a non-square box is stretched.

**Overlay to curated.** From a normalised box (cx and cy as fractions of W and H, size as a fraction of W): `side = size·W`, `x = cx·W − side/2`, `y = cy·H − side/2`, then emit `"iconCrops": {"<icon>": {"box": [round(x), round(y), round(side)], "note": "…"}}`. Constraints: 0 ≤ x, x + side ≤ W, 0 ≤ y, y + side ≤ H; for cards also x ≥ 14, y ≥ 14, x + side ≤ 194, y + side ≤ 242. If the overlay captures eye and chin clicks, emit the `{eyes, chin}` form instead, so the crop follows the rule if it is retuned.

**Pack photos can't be fixed through the curated file.** The plugin draws them with a Full crop, and `iconCrops` keys are icon ids, not NPC ids. A pack fix means rebuilding that photo from its `photoBox` [left, top, side] in Garland-photo px (given with the photo size in `findings.json` and `review.json`), which needs a new per-NPC override in `--portrait-pack`: a code change.

## Unsure, for the owner

1. Estinien's helmet art (087088 on 6 quests, plus 073136, 072705 and 4 pack photos): keep as "his look", or block so those quests show a fallback?
2. Yugiri 073167, her hood from the side (1 quest): keep or block?
3. Hooded Crystal Exarch 072629 (11 quests) and 072689: eyes measured by hand in the hood's shadow, so uncertain.
4. Yda 073156, masked: measured by hand.
5. Arenvald 087217: his face looks down and to the left; no square box centres it cleanly.
6. Faceless subjects, current framing kept: Nero 087047, Gaius 087064, Otis 087422, Korutt 073067, Pipin 073101, Kal Myhk 087146, Vrtra 087343 and 073075, Ehll Tou 087295, Omega 087240, N-7000 087357, Erichthonios 087335.
7. 073031, named Raubahn, is a helmeted figure with no face. Never shown today; suggest blocking it.
8. Pack photo 1015883, named "Papalymo", shows a black-haired Lalafell: possibly a disguise or a different NPC.
9. Battle-talk suggestions move the box 0.05 of its side toward the turned-away side, a heuristic; check them on `flagged-game-*.png`.
10. Each pack "cut" verdict rests on the chin landmark, which can land on a beard or collar. The 0.87 median is solid; single photos are less so.
11. The 129 pack photos where no detector fired are almost all faceless crops (weapons, Viera ears, hats, helmets). They would be better dropped with `--skip`.

## Not verified

Nothing was checked in game; renders reproduce the plugin's drawing code offline. The suggested crops were not applied or tested in the plugin.

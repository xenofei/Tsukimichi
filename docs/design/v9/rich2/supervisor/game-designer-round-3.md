# Moonfall rich pass 2: game designer supervision, round 3

Date: 5 October 2026
Reviewed: commit 5130f3df, `docs/design/v9/rich2/`.

**What I read:**
- `supervisor/response-round-2.md`;
- `supervisor/readcheck.json` (jewels, with the per-bin percentages);
- `supervisor/framecheck.json`.

**What I re-rendered and looked at:**
- the 9 screens at 1280 and 640;
- the line-up, the 6 composites (1× and 2× crops) and `pegmarks.png`;
- all frames of the three previews (fever 41, play 48, title 48).

**What I re-ran myself:**
- my own hue-bin measure on the map, the title and the board openings;
- the motion-range maps;
- the per-region luma swings.

Verdict rule: an asset gets REVISE when it carries a Major or Blocker finding. Minor findings and Nits are listed for fixing, but on their own they do not block an asset.

## Summary

My round-2 approval holds. Every round-2 Minor is resolved, or resolved by stating the honest measured figure (N2). The changes did not regress the richness:
- base-p1 gained a true teal sea;
- exp-p3 is now magenta-violet space round a blue world;
- Fever builds up before the banner lands, and its plate fades so the lit pegs show through.

One new Minor: the LONG SHOT callout hides a peg.

## Verdicts

| Asset | Verdict |
|---|---|
| title (1280, 640) | APPROVE |
| map (1280, 640) | APPROVE |
| characters (1280, 640) | APPROVE |
| levels (1280, 640) | APPROVE |
| hud (1280, 640) | APPROVE |
| power (1280, 640) | APPROVE |
| fever (1280, 640) | APPROVE |
| tally (1280, 640) | APPROVE |
| pause (1280, 640) | APPROVE |
| characters/lineup.png | APPROVE |
| composites/base-p1 | APPROVE |
| composites/base-p2 | APPROVE |
| composites/base-p3 | APPROVE |
| composites/exp-p1 | APPROVE |
| composites/exp-p2 | APPROVE |
| composites/exp-p3 | APPROVE |
| motion/title.png | APPROVE |
| motion/play.png | APPROVE |
| motion/fever.png | APPROVE |
| screens/pegmarks.png | APPROVE |

## Round-2 Minors: status

**N1, fireflies in a row: resolved.**
- base-p3 now has 9 fireflies scattered at different heights and spacings (2× crop, and the play preview's motion map).
- None sits in a row, and none is in the bucket's lane.

**N2, the beams do not visibly move: resolved by honest statement.**
- I measure a 0.2% luma swing over the upper-left beam area and 0.5% over the mid board, across the loop.
- The motion is still effectively invisible. The spec now says so instead of claiming ±15%.
- The visible life on the board is the dust in the beams and the fireflies, which is enough for the owner's "subtle".
- No further action needed.

**N3, the power card covered the board at 640: resolved.**
- At 640 the moment lives in the chrome: a laurelled BRASS WINGS ribbon on the top rail, and the rail portrait and gems.
- Nothing covers the opening. A small collision with the level name is a Nit below.

**N4, the F7 loophole: resolved.** The windows are exclusive. My own bins (board openings, pegs included) agree with `readcheck.json`'s `bins_pct`:

| Board | Second jewel | Share |
|---|---|---|
| base-p1 | true teal, 180–210° | 23–24% |
| base-p2 | rose, 300–360° | 23–25% |
| base-p3 | teal-emerald, 180–240° | 14–16% |
| exp-p1 | turquoise | 19–23% |
| exp-p2 | aquamarine | 18–22% |
| exp-p3 | the world's blue (240–270°), against magenta-violet space (300–330° now 43–45%) | 20–24% |

Each second jewel is at least 60° from the base. base-p3's wood is quieter than in round 2 (for protan separation), but still reads as a second tone.

**N5, the quill: resolved.**
- base-p1 has the chart's own compass rose in gilt light in the top-left corner.
- It reads at a glance as cartography and belongs to the subject.
- Its rings are thin, engraved and unlit, so they do not read as a wall.

**N6, the gauge: resolved.** Five gems for Brass Wings on a slim gilt bar (power, hud).

**N7, the card back's double meaning: resolved.** Stop 11 has a gilt four-point star with its own legend line ("your pick").

**N8, accents near peg hues: resolved.** The power ring at the green (power-1280, (293,682)) is now a white-gold light with only its edge in Cid's colour. It no longer reads as an orange halo.

**Nits:**
- Moon Gate on the line-up is lighter, and the twins' secondary accent is removed. Resolved.
- The pegmarks leaf is now a pointed oval with a midrib, clearly unlike the crescent. Resolved.
- Fever: the light rises first, the banner fades in over the first second, then the plate drops to about 35%. Resolved. This is a better beat.
- The style-shot callout: changed, but see N9.
- The title's colour: unchanged by measure. 93% of its coloured pixels still sit in one 30° bin (270–300°), and the "sapphire shadows" do not register. It is a Nit, and acceptable: the gilt chrome and the Continue card carry the screen.

## New findings

### Minor

**N9. The LONG SHOT callout hides a peg (power-1280 and power-640).**
- **What I see.**
  - At 1280 the callout spans about (795–1040, 145–215). The blue peg at about (1012,212) shows only as a ghost under its lower edge (compare hud-1280, where that peg is plainly visible).
  - At 640 the plate spans about (412–560, 85–130), over the peg at about (543,128).
  - The callout is larger than in round 2 (about 245 × 70 against 160 × 64).
- **Why it matters.** Style callouts appear while the ball is still live. Hiding a peg, even for a second, can hide the very shot the player is following, and it breaks the rule kept everywhere else that nothing covers a piece during flight.
- **Fix.** Place callouts with the same clearance field the framing uses: no piece under the callout's rectangle plus 6 units, picking from a few candidate spots in open sky. Or draw the plate at about 35%, as the Fever plate now is, with only the lettering solid. Bring it back toward the round-2 size.

### Nit

- **The BRASS WINGS ribbon at 640 runs over the level name.** On power-640, the ribbon's left tail and laurel lie over the name plate, which reads "THE AIRSHIP ROA…" for the 1.2 s. Start the ribbon right of the name plate, or fade the name plate under it.
- **The style-shot callout is still a plate.** It is a rectangular plate (the FULL MOON plate body), not a ribbon. Since it now matches the Fever plate's family, this is acceptable. Ribbon tails would make it read as a celebration rather than a sign.

## Unverified

How the motion, the Fever build-up and the power ribbon feel in the game at real frame pacing. Everything above is from stills and the rendered previews.

OVERALL: APPROVE

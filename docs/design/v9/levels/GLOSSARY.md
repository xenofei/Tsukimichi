# Moonfall levels

The language for making, checking and shipping Moonfall's 115 levels. The plugin's on-screen names live in `docs/glossary.md`.

## The game's structure

**Adventure**:
One of Moonfall's two campaigns: The Moon Road (11 stages, 55 levels) and The Far Shore (12 stages, 60 levels).
_Avoid_: campaign, mode, world

**Stage**:
Five levels that share a companion and a theme, keyed MR 1–MR 11 or FS 1–FS 12.
_Avoid_: chapter, world, set

**Finale**:
The fifth level of a stage, which must be harder than the mean of the stage's other four.
_Avoid_: boss level, last level

**Level**:
One playable board: a painted scene whose pegs trace or frame its subject. It has a permanent id (`base-07`) and a code for its place in the game (`2-2`, stage 2, level 2).
_Avoid_: board, map, puzzle

**Subject**:
The thing in a level's painting that the pegs trace, outline or frame, such as a creature, a landmark or a trail.
_Avoid_: motif, theme (a theme belongs to a stage)

**Ace**:
A level's score goal: the score reached by the top 40% of winning games.
_Avoid_: par, gold score

## Making levels

**Refit**:
Remaking a level that was already approved so it meets the Peggle-match rules of 9 October 2026.
_Avoid_: redo, remaster

**Generator**:
The tool that makes candidate layouts for a level from its subject features.
_Avoid_: auto-author, level maker

**Variant**:
One candidate layout the generator makes for a level; several are compared on a contact sheet.
_Avoid_: draft, option

**Subject feature**:
A shape drawn over a level's painting (an outline, a crest, a route) that marks the subject for the generator to trace.
_Avoid_: shape, mask, outline (an outline is one kind of feature)

## Checking and shipping

**Review**:
One combined AI reviewer's single pass over a stage. Only its Blockers and Majors are fixed.
_Avoid_: supervisor round (the three-reviewer loop it replaced)

**Minor list**:
The Minor findings a review logs instead of fixing, kept for one polish pass before a milestone release.
_Avoid_: backlog, nits

**Play-through**:
The owner playing a stage in game, which is the stage's sign-off: each level is Good, or Redo with a reason.
_Avoid_: playtest, smoke test, approval

**Stage group**:
Three stages (15 levels) that are play-tested and then released together.
_Avoid_: batch, wave

**Test drop**:
The build of a stage group sent to the owner for its play-through, before it is released to everyone.
_Avoid_: beta, preview (1.23.0 "Moonfall preview" was a release)

**Developer mode**:
A hidden way into Moonfall that skips progress and the spoiler shield so the owner can jump to any level; it is removed once all 115 levels have shipped.
_Avoid_: cheat mode, debug mode

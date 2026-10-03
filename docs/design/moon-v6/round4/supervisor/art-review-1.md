# Art review 1: Menphina's Medallion, round 4

**Reviewer:** art supervisor
**Subject:** `round4/medallion-r4/`, against `../brief.md` and the designer's `concept.md`

**Evidence:**
- `_sheet.png` and `_variants.png`.
- Supervisor renders in the scratchpad, `sup-r4/`:
  - all glyphs at 256 px (`g256.png`);
  - all glyphs, the three job variants and completed-green at 512 px (`a.png`, `b.png`, `c.png`);
  - Locked out, Done, Blocked and In journal at 1024 px (`z_*.png`);
  - the icon at 512 and 1024 px;
  - 2× crops of the icon's moon and water (`z_moon.png`, `z_water.png`);
  - nearest-neighbour crops of the cloud shadows (`crop_blk*.png`).
- Pixel sampling of the renders.
- A one-line test render of Blocked (`blk_test.svg`, `z_bt.png`) that confirms the cause of fix 1.

## Verdict: CHANGES REQUIRED (three items, all small)

Round 4 answers every owner note, and its light logic is sound throughout.

Three things still break realism:
- a colour artefact in blurred shadows;
- the road's crest glints, which turn water into glossy pills;
- a horn-on-edge tangent in In journal.

All three are generator-level edits.

## Required fixes

### 1. Blurred dark shadows render with a teal fringe

**Files:** every SVG. The fault is in `_src/gen4.py`, and it is most visible on `blocked.svg`.

**Elements:**
- `blur_filter()` at line 171.
- The inline `ib` filter (line 184).
- The icon's `fs` and `mbl` filters (lines 779 and 796).

None of these sets `color-interpolation-filters`, so Chrome blurs in linearRGB.

**What is wrong:**
- Each cloud's cast shadow (`#080B16` at 0.55, through `r4b-cs`) leaves a cyan-green fringe on the enamel and on the moon's edge, where its alpha is low.
- Measured pixels:
  - (12, 28, 46) and (8, 24, 38) under the cloud bottoms, against the blue-violet enamel at (11, 16, 47);
  - (62, 77, 91) where the small cloud's shadow meets the moon.
- At 512 px and above it reads as greenish mould under the clouds: a hue foreign to a blue and gold palette, and wrong for a shadow, which should only be a darker, cooler version of the surface it falls on.
- I added the attribute to `r4b-cs` and `r4b-rb` and re-rendered. The same pixels became (17, 22, 42), (12, 19, 40) and (67, 73, 87): clean blue shadow.

**Change:**
- Add `color-interpolation-filters="sRGB"` to `blur_filter()` and to the inline `ib`, `fs` and `mbl` filter strings, so it applies everywhere.
- `shadow_filter()` already has it.

### 2. The road's crest glints turn ripples into glossy pills

**Files:** `plugin-icon.svg`, `ready.svg` and `in-journal.svg`.

**Elements:**
- In the icon: `_src/gen4.py` line 834, `streak(x0 + L*.14, x1 - L*.2, y - h*.22, h*.3, skew=.4)` at `o*.3`.
- In the glyphs: line 340, `streak(... y - h*.2, h*.34, skew=.4)` at `o*.3*k`.

**What is wrong:**
- Each long streak now holds a second, crisp-edged lens hugging its upper edge.
- A glint on water is already the specular reflection of the moon. It has no specular of its own.
- A hard inner lens offset toward the top is how a bevelled, rounded object is drawn. In the 2× icon crop and the 1024 px In journal render, the streaks read as raised glossy capsules or leaves with a vein, not light lying on water.
- The brief's "glints that catch light at their crests" is better met by a soft hot core.

**Change:**
- Keep the extra detail, but make it a soft, centred core:
  - **Icon:** `streak(x0 + L*.18, x1 - L*.18, y, h*.45, skew=0)` at `o*.35`, inside a group filtered with `feGaussianBlur stdDeviation="2"` (sRGB).
  - **Glyphs:** `streak(cxr + x0 + L*.18, cxr + x1 - L*.18, y, h*.45)` at `o*.3*k`, blurred with stdDeviation 0.5 (sRGB).
- The core must have no visible edge at 1024 px.
- Keep the road's peak at 0.90× the moon or less after the change.

### 3. In journal: the crescent's horn ends on the ribbon's edge

**File:** `in-journal.svg`, the ribbon path `M19.5 15.26A66 66 0 0 1 37 3.78V76L28.25 69L19.5 76Z` (keyline 2.2) and the crescent `rotate(28 49 42)`.

**What is wrong:**
- The crescent's lower horn tip is at (35.4, 67.6). The ribbon's right edge, with its keyline, is at x 38.1.
- So the horn is hidden for only 2.7 units, and at every hero size the tip appears to stop exactly on the ribbon's edge.
- This is a tangent: the eye can't tell whether the moon is behind the silk or touching it, which flattens the depth the ribbon's shadow is trying to create.

**Change:** move the whole ribbon group (the path, the stitches, the crest and inner-slope bands, and the ribbon shadow) in one of these two ways:
- **−6 in x**, so the right keyline sits at x 32.1 and the horn tip is 3.3 units clear;
- or **+5 in x**, so the ribbon decisively covers the horn (about 7.7 units hidden).

The −6 option keeps more moon visible. Re-check the RoJ-Jrn distance afterwards.

## The coordinator's focus points

**Clouds (Blocked)**
- **Correct.** The single upper-left key light gives:
  - each billow its own upper-left highlight;
  - soft lit rims on the upper-left edges and shaded rims on the lower-right edges;
  - a flat, darker underside.
- The cast shadow (1.7, 2.3, blur 1.3) falls down and to the right, onto the moon (visible under the small cloud and to the right of the large cloud's last billow) and onto the enamel.
- The cloud values sit between the enamel and the moon, so the moon stays the light.
- In a real sky, clouds in front of the moon would be backlit with silver edges. The medal relief convention (clouds as raised relief, the moon as a flush inlay) is stated in concept.md, and I accept it. The only fault is the teal fringe (fix 1).

**Shards (Locked out)**
- **Correct.** Crack faces whose normal turns toward the upper left carry the pale `#F4AAB2` edge. Faces turned away are dark.
- Each shard keeps an upper-left limb band. The shards cast shadows into the near-black socket.
- The falling shard is displaced and turned, and it breaks the silhouette.
- The cracks are thin, as asked.
- The red (`#DA6470` / `#C24A58` / `#7A2838`) has body and is the one alarm saturation. It is acceptable.

**The repeat arrow (Done)**
- **Material is correct.** It is the bezel's gilt.
  - The outer edge is lit where it faces the upper left, and the inner edge is shaded there.
  - The inner edge is lit on the right side, where it faces the light.
  - The head carries a light upper-left edge.
  - It casts the standard shadow.
- **Its relation to the rim** is legibility, not realism. See the designer's doubt 3 below.

**Moon detail**
- **Passes.** The maria are smooth value shifts (blurred ellipses at 8–32%) set near the limb, where a young crescent shows them.
- The lit body brightens from the terminator to the limb.
- Nothing reads as cheese at 1024 px.
- Completed's full moon is lit from the upper left with soft maria and a soft limb band.

**The icon's water**
- **The reflection rules hold:**
  - the road is under the lit centroid (171 against 172), in the moon's hue, at 0.88× the moon;
  - the brightest row is at the mirror point;
  - the rows foreshorten toward the horizon;
  - the Fresnel band and the mirrored bloom are correct;
  - the sea stays darker than the sky at the horizon;
  - the wind-ripple texture is subtle and kept quiet in the Installed corner.
- The only fault is the crest glints (fix 2).

**Ready**
- The full rim is right.
- The warm afterglow sits on the horizon under the lit limb's direction, toward the set sun, with its reflection directly below. Physically correct, and good warm/cool balance.

**The job badge**
- **Believable.** It is a raised mini-medal with the bezel's ramp, a recessed role-colour seat (with an inner shadow at the upper left) and a down-right cast shadow over the rim.
- The game's gilt job glyph is lit from the upper left like everything else.
- The role colours are held under C 0.13.

## The designer's doubts

1. **Not checked as "a question mark in a ring" at 16–28 px.**
   - Not a realism problem. The mark is lit like a crescent: brightest on the outer limb, with a soft inner edge, a dot lit from the upper left, and a cast shadow.
   - The family rim is what separates it from a help button at row size. Acceptable.
2. **The badge is mush below 32 px.**
   - Endorsed. The row fallback (a role pip on the medal, plus the game's job icon at text height) is the right design.
   - It also keeps official art at native texture scale.
3. **Done's arrow merges with the rim at 16 px.**
   - Not a realism block, but I recommend a fix: break the concentricity.
   - Let the arrow spiral outward from tail to head, radius about 33 → 39. That reads as motion, gives a growing gap to the rim on the left, and stops it reading as a second ring.
   - Thicken the band by about 1 unit at the 16 px tier.

## Optional polish

1. **Green check.** It is in tune with the palette, but it adds a third hue family to a lapis-and-champagne set. The gilt check is more harmonious, which agrees with the designer's lean. It is the owner's call.
2. **Blocked.** A faint cool silver lining on the cloud edges that overlap the moon (`#DCE2EE` at 0.25, 0.6 units wide) would acknowledge the moon behind them without breaking the key-light convention.
3. **Locked out.** Add a faint lit lip on the socket's lower-right wall where the falling shard has exposed it. The concept says the lip is there, but it is barely visible at 1024 px.

## What is right

- **Every owner note is met in a way that reads:**
  - the full Ready rim;
  - a real job badge;
  - clouds for Blocked;
  - repeat for Done;
  - a bright, detailed Completed moon;
  - thin fractures;
  - a question mark built from a moon.
- **One gilt and one key light** are kept across all thirteen assets.
- **The family is coherent:** Ready, In journal and Ready on another job share one moon.
- **The icon's moon and water** gained real, physically motivated detail without breaking the round-3 reflection rules.
- **The Ready afterglow** is the best new idea of the round. It is the correct place for warmth, and it is physically placed.

## Re-review after the fixes: APPROVED

I re-rendered the regenerated files in the scratchpad, `sup-r4/`:
- Blocked, Done, In journal, Locked out and Ready at 1024 px (`y_*.png`);
- the icon at 1024 px, plus a 2× water crop (`y_i1024.png`, `y_water.png`);
- the new `_sheet.png`.

**1. sRGB blur filters: verified.**
- No filter in any of the 13 SVGs is missing `color-interpolation-filters="sRGB"`.
- A hue scan of every re-render for dark cyan or green shadow pixels finds 0 in each file.
- The cloud shadows sample as plain cool blue.

**2. Crest cores: verified.** Each long streak now carries a soft, centred, edgeless brightening. The ripples read as flat light lying on water, not capsules or veined leaves. The road peaks (0.89× the moon on the icon, 0.90× on Ready, 0.72× on In journal) stay within the rule. Using `skew=.5` is fine; at blur 2 the core shows no asymmetry.

**3. In journal's ribbon: verified.** Moved +5, it clearly covers the crescent's lower horn, so the moon reads as passing behind the silk. Its shadow, fold, crest and inner-slope bands moved with it and still follow the rim.

**4. Done's arrow (doubt 3): verified.**
- The spiral from r 33 to r 39 reads as motion, and the gap to the rim opens visibly toward the tail.
- The head tip stays about 4.6 units inside the well edge, so there is no tangent.
- Its lighting is unchanged and correct.

**5. Polish: verified.**
- The silver lining appears only where the clouds overlap the moon, which is physically motivated backlighting.
- Locked out's socket lip reads as the lit lower wall of a recess.

No regressions are visible on the sheet. Ready still leads, and Completed recedes.

**Verdict:** every glyph, the job variants, both Completed checks and the icon pass for light, shadow, reflection, colour theory and material. **APPROVED.**

# Supervisor review: the v7 themes (Aether Crystal, Astrologian's Orrery, Ishgard Glass)

**Reviewer:** art supervisor (realism: light, shadow, reflection, colour, material; plus grammar fidelity and mixing)

**Read:**
- each theme's `concept.md` and `_sheet.png`;
- `docs/research/plan-v7/theme-system.md`: §2 grammar and bans, §3.2 faces, §3.3 frame kits and "act now is gilt in every kit", §5.2 one kit per mix.

**Renders** (in `scratchpad/sup-v7/`):
- all eight states of each theme at 400 px (`aether-crystal.png`, `astrologian-orrery.png`, `ishgard-glass.png`);
- **a mixed column** (`mix.png`), drawn at 20, 48 and 96 px. Each state comes from a different set:

| State | Set |
|---|---|
| Ready | Medallion |
| Ready on another job | Aether |
| In journal | Glass |
| Blocked | Orrery |
| Done this cycle | Medallion |
| Completed | Glass |
| Locked out | Aether |
| Not checked | Orrery |

## Verdicts

| Theme | Verdict |
|---|---|
| All three (system) | **CHANGES REQUIRED:** S1–S2 |
| Aether Crystal | **CHANGES REQUIRED:** A1 (+ S1–S2) |
| Astrologian's Orrery | **CHANGES REQUIRED:** O1 (+ S1–S2) |
| Ishgard Glass | **CHANGES REQUIRED:** G1–G2 (+ S1–S2) |

All three are real improvements on their round-2 selves:
- no no-entry reads, no lollipops, no hamburgers, no Saturn, no aetheryte;
- every set now carries the road, the ribbon, the repeat arc, the check on a full moon, the cracked Dalamud and the "?".

Each has a distinct, believable material. The blocking problems are one system-level issue and one or two state faces per theme.

---

## System fixes (all three generators)

### S1. Faces and frames are baked together, so a mix has several border colours

**Files:** `aether-crystal/_src/gen_ac.py` (`bezel()`, the 12-sided badge ring), `ishgard-glass/_src/gen.py` (`frame()`), `astrologian-orrery/_src/gen.py` (rim and badge ring), and every SVG they write.

**What is wrong:**
- Every state SVG draws its own frame: Aether's 16-sided moon-quartz bezel, Glass's stone tracery, Orrery's brass.
- In `mix.png` the column carries four rim materials and two silhouettes (a 16-gon among circles). That is exactly the owner's round-2 complaint, "the borders should share one cohesive colour".
- It also breaks theme-system §3.2–3.3: a set ships **unframed faces**, and the frame, the badge ring and the badge contents come from the **one frame kit** of the mix.

**Change, for each generator:**

1. **Faces.**
   - Emit unframed faces: the well and the emblem only.
   - Every face uses the **same circular well**: centre (64, 64), radius 52.4, the shipped Medallion well.
   - Elements that overhang the rim must stay where they are, drawn in the face layer above the frame layer: the ribbon over the top left, the check out past the lower right, the falling shard.
   - So emit two face layers per state: `under` (the well content) and `over` (the overhangs).
2. **Frame kit.**
   - Emit the theme's frame kit as separate SVGs:
     - four urgency tiers (**act now**, **resting**, **finished**, **ghost**), each at Full and Quiet;
     - the badge ring and seat;
     - the open-lock, closed-lock and book glyphs.
   - These are the Silver, Came and Astrolabe kits of §3.3.
   - **Aether's 16-gon:** its inner edge (apothem) must cover a circular well of r 52.4 with at least 1.5 units of overlap, so any face sits in it with no gap at the flats.
3. **Composite.** Rebuild the state SVGs and `_sheet.png` as composites (face + kit) so the sheets still show the theme as designed.
4. **Mix check.** Add a `_mix.png` that shows each theme's faces in the **Brass** kit, and Medallion's faces in this theme's kit. That proves the swap works both ways.

### S2. The act-now frame must be the shared gilt, in every kit

**Elements:** the Ready frame in each theme.

**What is wrong:**
- §3.3 says "act now is gilt in every kit": the colour of urgency is constant, and only the resting metal changes.
  - **Aether:** Ready has the same moon-quartz bezel as the other seven.
  - **Glass:** Ready has the same stone.
  - **Orrery:** Ready has its own brass, which is "warmer and more yellow than the Medallion's champagne gilt".
- With S1 in place, a user mixing Medallion's Ready into an Aether set would get a gilt frame on Ready only if the kit defines it.
- Glass's doubt "Ready lost its warm glow" is the same issue: the warmth belongs in the act-now frame, not in a glow.

**Change:**
- Each kit's act-now tier is the medal bezel's gilt ramp: `#E6CF98` / `#9A7E4A` / `#7C6236` / `#5C4724`, with the `#FFF4D6` specular.
- Draw it in the kit's own *shape*: Aether's 16-gon crown and pavilion, Glass's turned ring with gilt inner came, Orrery's astrolabe rim.
- Resting, finished and ghost keep the kit's own metal.

---

## Aether Crystal

**Right:**
- The per-facet Lambert idea is excellent. It makes crystal read as crystal without bands or a second terminator, and it keeps the lighting honest.
- The bezel's crown is bright at the upper left (sampled L 239 there against 72 at the lower right), with the pavilion reversed and the specular flash on the upper-left crown.
- Cleaving the red crystal along straight planes is the right material behaviour.
- Adularescence and the transmitted rim are physically motivated uses of aether blue, with no glow.
- Locked out lit by the key light rather than by a phase is **accepted.** It is a full disc, which is the medal convention for full discs (Medallion's Locked out and Completed do the same).
- The 16-sided bezel is **accepted as a silhouette**, provided S1's apothem rule holds.

### A1. Blocked has no obscuring cloud, and its limb reads as a waning crescent

**Element:** `blocked.svg` and `_row/blocked.svg`: the dark crystal disc (r 31) with its sunlit limb (k −0.66, low left).

**What is wrong:**
- The grammar (§2) requires an obscured new moon. §4.1 specifies "a faceted frost bank (low-poly cloud) in the set's material".
- The face has no obscuring mass at all.
- With a lit width of about 17%, the limb is a crescent. At 16–28 px Blocked reads as a moon *lit on the other side*: a waning crescent, a phase that means nothing in this grammar. Next to Ready on another job's waxing crescent it is a mirror image.
- A crescent is the Ready family's shape. Blocked must not borrow it unobscured.

**Change:**
- **The frost bank.** Add a low-poly frost bank of 5–7 flat facets across the disc's lower half and over the lower part of the limb:
  - It covers about 40% of the disc.
  - Its top edge is a broken line of 3–4 rounded steps, so it reads as cloud, not as a ledge.
  - Each facet takes one flat value from its normal under the key light: the upper-left facets go to `#B8C4DE`, the lower-right facets to `#4E5C82`.
  - It casts the soft emblem shadow (1.1, 1.6) onto the disc.
- **The limb.** Narrow it to k ≈ −0.82, about 9% lit width, so the visible part is a thin bright arc between the cloud's top and the disc's upper edge. Keep the transmitted rim.
- **Check:**
  - Re-run the metrics. RoJ-Blk must be at least 12 at the row tier; it is 12.1 now, and the cloud should widen it.
  - Blocked's salience must stay ≤ 0.8× Ready.

### Optional (Aether)

1. **The check.** It renders as a smooth rounded tube. In a set whose rule is "every cut surface is flat facets", cut it as a two-facet ridge: the upper-left face `#F0DDA8`, the lower-right face `#7C6236`, and a crisp crest.
2. **Done's blue arrow.** Not a realism problem. The repeat-arrow reading is shared with Medallion, which is approved. If the juror pass flags "refresh", taper the tail to a fine point as Medallion did.

---

## Astrologian's Orrery

**Right:**
- The brass is lit from the upper left: L 236 at the upper left, 72 at the lower right.
- Notches appear at hero only, with no sun-ray ticks.
- The engraved constellation in Blocked's earthshine is a lovely, believable engraving.
- The ember-copper ribbon is warm and distinct.
- The Dalamud cracks are fine, and the red is truthfully lit by the key light.
- The constellation "?" is the best Not checked of the three at hero. Its star glow is the right kind of glow (stars are light sources), and its salience of 35 suits the **ghost** tier, as the grammar asks.

### O1. Blocked: the "new moon" is nearly full, and the brass cloud is the loudest mass on the medal

**Element:** `blocked.svg` and `_row/blocked.svg`: the earthshine disc and the raised scroll-cloud.

**What is wrong:**
- **The disc.** The earthshine disc is a pale grey-blue, close to the lit limb's value. At 48–96 px it reads as a full moon with a slightly brighter edge, not as an ashen new moon. Earthshine is a few percent of sunlight; the dark disc must sit close to the well.
- **The cloud.** The raised brass cloud is polished gilt brass, the same metal as Ready's act-now frame and the brightest warm, saturated mass on the medal.
  - Physically, a raised brass appliqué lit from the front is fine: the bevel and highlight are correct.
  - But its value and saturation make Blocked shout. At row size it reads as a gold lump, and in a mix it competes with Ready's gilt.

**Change:**
- **The disc:** darken the earthshine disc to at most the well value + 12% L. For example `#22305A` → `#1A2448`, keeping the constellation as hairlines at 0.5.
- **The cloud:** recolour it to **patinated brass**:
  - body `#7A6A45` → `#4E4330`;
  - highlight `#B8A578` on the upper-left scroll crests only.
- **Keep the limb:** the thin limb (`#E2E8F4`) stays the only bright thing.
- **Targets:** Blocked's salience ≤ 0.7× Ready's at 16 px; the weakest pair ≥ 12.

### Note (Orrery, not realism)

Ready differs from Medallion's only in palette and rim. That is the critic's call, but S2 makes it starker: both will wear the same gilt act-now frame. If the critic wants a signature, the hero-tier hairline limb scale from §4.3, at 0.3, would give one without touching the row tier.

---

## Ishgard Glass

**Right:**
- The came reads at row size.
- The rose-window segmentation of the well is beautiful at 96 px and up.
- The glass sea's waves catch light along their crests.
- The red-glass shards have pale lit edges and a true dark void.
- The "?" is in lead, bright enough for daylight.
- The gilt arrow is applied metal, correctly front-lit with its shadow.

**Two light rules: accepted, and physically correct.**
- Stained glass glows by transmitted light from behind.
- Stone and lead are lit from the room side by the key light.
- That is how a real window looks. Keep it, with this rule: **glass takes no cast shadows and no front-lit bevels.** Only the applied things do: lead, stone, gilt, the badges and the ribbon.
- The two required fixes below are the places where the glass breaks that rule.

### G1. Blocked's clouds are drawn as opaque front-lit relief, not glass

**Element:** `blocked.svg` and `_row/blocked.svg`: the two leaded cloud panes.

**What is wrong:**
- The cloud panes carry a top-lit gradient (light top, dark underside) and a drop shadow onto the panes behind.
- Coplanar glass panes in one window cannot shade each other, and glass is lit through, not on.
- So they read as plastic stickers on the window.

**Change:**
- **Drop shadows:** remove them from the cloud panes.
- **Fill:** each cloud is 2 flat glass tones divided by a curved came:
  - the body `#5A6690`;
  - a lighter `#8A96BE` pane along the edge nearest the moon's limb, where the backlight comes through thinner cloud.
- **Leading:** keep the lead outline at the came width.
- **Check:** RoJ-Blk and Blk-NotC must stay ≥ 12; the weakest pair is now 12.4.

### G2. Completed's moon is one blurred pane

**Element:** `completed.svg` and `_row/completed.svg`: the full-moon disc with blurred maria.

**What is wrong:**
- The blurred maria inside an uncut disc read as smudges on glass.
- Every other surface in the set is divided by came and carries flat tones per pane.
- The designer flagged it.

**Change:**
- **Panes:** divide the disc with 3 curved cames into 4 panes, following the maria's large-scale layout.
- **Tones:** fill the mare panes with a flat `#9AA6C6` and the highland panes with `#C8D2E8`. No blur.
- **Hero craft:** at hero, add the grisaille-painted maria as 0.15 hairline hatching. That is the period technique, so it stays glass.
- **Check:** keep Completed ≤ 0.8× Ready.

### Optional (Glass)

1. **Ready's rose window at 48 px.** Draw the sky's radial panes only from 64 px; at 48 px keep the moon, horizon and road cames only.
2. **White Mage pair at 12.3.** A legibility point (the weakest pair with the White Mage badge). G1's lighter cloud edge should widen it.

---

## Mixing with Medallion (after S1–S2)

- **The grammar holds across sets.** Every state carries the same mark in every set, so `mix.png` reads correctly state by state even before the fixes.
- **What breaks today is only the frames:**
  - four rim materials and two silhouettes, which S1 fixes;
  - an inconsistent act-now colour, which S2 fixes.
- **Light:** all three sets and Medallion share the upper-left key light and the phase-lit moons. Medallion's Ready beside Aether's Locked out, or Glass's Completed beside Orrery's Not checked, shows no contradiction of light direction.
- **Materials:**
  - Brass frames over crystal, glass and constellation faces read as one medal with different enamels, which works.
  - The case to watch is Glass faces in an opaque metal kit: a backlit window inside a front-lit brass rim. It is still plausible, as a lit window set in metal. **Acceptable.**

Once each theme's fixes are in, a spot check of the changed faces, the new `_mix.png` and the kit sheet is enough.

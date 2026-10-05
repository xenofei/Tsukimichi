# Moonfall: art direction and first concept art (plan v9, G5, G6 and G8)

Status: concept art for the owner's review, made on 4 October 2026 for `docs/feature-plan-v9.md`. Menphina's Medallion only: the night palette and brass frames, with no per-theme variants. Every image passed the realism supervisor before it reached this page (see "Supervision record").

## Files

All renders are in `docs/design/v9/`. Their sources are in `src/`, and each one runs with `py -3 <script>.py` from `src/` using only numpy and Pillow. The scripts reuse `docs/design/v8/art/src/` (artlib and the Option B pipeline) unchanged.

| File | What it shows | Made by |
|---|---|---|
| `style-frame-a.png` (800 × 600), `@2x` (1600 × 1200) | The playfield mid-shot: the ball in flight, six pegs lit, a "100" popup, the HUD. Bucket proposal A, the crescent cradle. | `src/style_frame.py` |
| `style-frame-b.png`, `@2x` | Aiming: the guide's dots to the first peg and Super Guide's line past it. Bucket proposal B, the lantern boat. | `src/style_frame.py` |
| `peg-states.png` (1440 × 1100), `@2x` | Each peg kind unlit, lit and clearing (three frames); bricks; Fever's two moments; the free-ball cue; the ball. | `src/peg_states.py` |
| `fever.png`, `@2x` | Fever after the hit, full size. | `src/peg_states.py` (via `src/fever.py`) |
| `readability.png` (1500 × 560) | The playfield at its smallest window (640 × 480), with 3× zooms. | `src/readability.py` |
| `characters.png` (1960 × 1100), `@2x` | The eleven characters who carry the powers (decision 1). | `src/characters.py`, `src/figures.py` |
| `campaign-base.png`, `campaign-expansion.png` (960 × 540), `@2x` | Key art for the two campaigns. | `src/campaigns.py` |
| `whatsnew-1.23.0.jpg` (1120 × 440, quality 88, 4:4:4, about 60 KB) | The What's new painting for 1.23.0, as shipped. | `src/whatsnew_123.py` |
| `art/moonfall-b-base.png`, `-masks.npz`, `.json`, `-medallion.png` | The painting, its region masks, its placement data and its lossless Medallion master. | `src/whatsnew_123.py` |
| `supervisor/round-N.md` | Each supervision round, verbatim, with the designer's response. | |

Shared code: `src/mf_lib.py` (the light, pegs, bricks, ball, brass, text), `src/playfield.py` (sky, frame, launcher, buckets, HUD, the sample level, the aim guide), `src/paint_moon.py` (the paintings' moon) and `src/portraits.py` (the power medallion).

## The one light

- **The playfield, the pegs and every HUD part** take one directional moonlight from the upper left, slightly in front of the board: `L = (-0.424, -0.424, 0.80)`, with x to the right, y down and z toward the viewer.
  - A sphere in that light shows a gibbous face, a soft terminator and an unlit part about 0.2 r wide on its lower right. That is what makes a peg read as a moon at every size, and never as a coin, a cheese or a glyph.
  - Brass, bricks, glass and the ball are shaded from height-field or sphere normals with the same vector, so the light can't disagree between parts.
- **No cast shadows on the board.** The board is open night air in front of a far sky, so there is no surface to take one. Pegs, bricks and the ball cast none. Only the rails, which stand proud of the board, shade it along the top and the left.
- **Paintings and the character cards.** The moon, high on the left, is the one natural light. There is at most one warm practical light (a lantern), which lights only what is near it. Distant pinpoints, such as the stars and the falling star, light nothing.

## Palette

The Medallion tokens, from `Tsukimichi.Core/Ui/GlyphPalette.cs` (`GlyphTokens.Medallion`) and `Themes/PaletteRoles.cs`:

| Role | Hex |
|---|---|
| Abyss (keylines, the darkest night) | `#080B16` |
| Enamel (the rails), lit end to deep end | `#1D2B5A` → `#131C40` |
| Status top and foot (sheet grounds) | `#0E1329`, `#0A0E1C` |
| Gilt ramp: specular, high, body, mid, shade, deep, dark | `#FFF4D6`, `#E6CF98`, `#D9BE82`, `#9A7E4A`, `#7C6236`, `#5C4724`, `#33260F` |
| Moonstone: specular, high, body, mid, deep | `#F4F2EA`, `#E2E8F4`, `#C3CEE4`, `#95A5C8`, `#5E6E97` |
| Text: cream, dim ink | `#F3E9D2`, `#9AA6C8` |

The playfield sky runs from the zenith to the horizon in `#070B1E`, `#0F1738`, `#18234C` and `#22305E`, with the glow of the moon just off the frame's upper left (`#B9C8F0`).

The pegs: their colour is the lit face's albedo, and each has its seas and its lit-state halo in the same hue. The four kinds are 60–90° apart in hue and differ in value too (orange and green brightest, purple darkest), so they separate for colour-blind players as well.

| Kind | Albedo | Seas | Lit halo |
|---|---|---|---|
| Blue | `#9DBDF2` | `#6C84B8` | `#A8C6FF` |
| Orange | `#F49A50` | `#B5623A` | `#FFB070` |
| Green | `#86DA98` | `#4E9A68` | `#9CF0B0` |
| Purple | `#C58CEB` | `#8657B0` | `#D8A4FF` |

The warm practical light is lantern paper, `#B4602A` → `#F2B060` → `#FFE6B0`, which lights at `#FFB060`. The paintings' moon is a near-neutral `#ECEBE4` with seas of `#A8A8A6`. It is never yellow.

## Sizes, in the engine's units

Every size is in the original's 800 × 600 playfield units (plan v9 G1), so 1 unit is 1 px at 1×.

| Asset | Size (units) | Notes |
|---|---|---|
| Window | 800 × 600 | Scales with the window; the minimum is 640 × 480 (0.8×). |
| Top rail | 0–41 high | Level and stage on the left, score on the right; the launcher's yoke hangs from it. |
| Side rails | 0–75 and 725–800 | Their brass beads are the walls. The ball's centre stays within x 81.5–718.5. |
| Board opening | x 75–725, y 41–594 | |
| Peg | r 10 (collision r 10.7 with the ball's 6, so 16.7 apart at contact) | Moon sprite, 4 sea layouts. |
| Brick | 30 long × 12 thick on its arc (r 236 in the sample), 2 units apart | Moonstone slab, rounded 4.5 units at its edges. |
| Ball | r 6 | Satin silver. In the ball channel: r 10.5. |
| Launcher | pivot (400, 87); tube from 8 to 70 units, r 7.4 → 6.0; the ball leaves at 73 | Brass telescope, ±81°. |
| Free-ball gauge | an arc of r 25.5 round the pivot, 200°–340°, channel 6 wide | Notches at 25k, 75k and 125k. |
| Bucket A, crescent cradle | 131 across the horns, mouth 104 at the rim (y 573), belly to y 588 | Rides a rail at y 589.5–592.5 on two wheels. |
| Bucket B, lantern boat | 131 at the rail, waterline y 584; post to y 535, lantern 9.2 × 12 at (x + 50, 541) | Water strip y 584–594. |
| Ball channel | x 25–51, y 66–330; balls r 10.5, 25.5 apart | The count sits below, 18 units high. |
| Multiplier dial | centre (762, 100), r 25, groove 5.2 wide | Gilt arc for oranges cleared, ticks at ×2, ×3, ×5 and ×10. |
| Power medallion | centre (762, 236), r 24 | The carrier's silhouette, the power's name, pips for turns left. |
| Fever cups | 5 across the foot, each 130 wide, rim at y 566, foot at y 590 | Values on plates set into each bowl's front. |
| Popups | 9.5 units, just below the struck peg | Moved to the first nearby spot clear of other pegs and the ball. |
| HUD text | values 13–18 units; labels at least 10 units | 8 px at the minimum window. |
| What's new art | 1120 × 440 (the 560 × 220 band at 2×) | |
| Campaign tiles | 960 × 540 at 1×, 1920 × 1080 at 2× | |

## Pegs, bricks and the ball

- **Unlit.** A gibbous moon in the one light. The face is Lommel-Seeliger flat (as the real moon is) with a little Lambert form. The terminator is blurred to about 0.25 r. The unlit part is earthshine, slightly above the local sky, so the limb stays one circle.
  - The seas are separate lobed maria of different depth, 4–5 of them, clustered across the upper middle and never laid along a curve, so they can't form a ring, a hook or a "C".
  - There are four layouts, and each peg turns its layout by its own angle (±0.6 rad), so no two neighbours match.
  - An unlit peg has no halo: it is not a light.
- **Lit (hit).** The face brightens in its kind's hue, and a halo blooms out to about 2 r. The seas stay visible, and the sliver narrows to about 0.12 r. In the original a hit peg only "lightens"; ours also blooms.
- **Clearing** (each peg in hit order, 50 ms apart, the first 0.57 s after the ball leaves; plan G1):
  1. 0–60 ms: a bloom, scale 1.06.
  2. 60–150 ms: the moon dims and sets a little (scale 0.70, alpha 0.45, 4 units down).
  3. 150–300 ms: its light sifts down as fine moondust (28 specks, each at most 0.07 r) and fades.
  - The original's expanding ring is not used.
- **Bricks.** Pillowed moonstone slabs in the same four colours, with the same mottling as the seas. They go lit, then clear with the same dust.
- **The ball.** Satin silver, and always the brightest small thing on the board. It is diffuse in the one light, with a broad soft highlight and a small core on the upper left, a soft horizon between the reflected sky above and the dark board below, and a darker limb.
- **The aim guide.** Silver dots 17 units apart along the predicted arc, stopping where the ball would first touch a peg. With Super Guide, a thin moonstone line continues through the bounce to the next contact (as measured).

## The two bucket proposals

| | A: the crescent cradle | B: the lantern boat |
|---|---|---|
| Look | A brass crescent, horns up, riding a slim rail on two wheels; a moonstone inlay along its belly. | A small boat of dark planks on a strip of water, with a paper lantern on a curved stern post. |
| Reads as | the brass frame's own instrument; calm and quiet | a scene: the lantern is the playfield's one warm light |
| Cost | brass only; no light of its own | a light: warmth on the post, the rail and the stern within about 30 units, a reflection and a broken warm column in the water, plus a 10-unit water strip along the foot. Pegs are too far away to be lit. |
| Risk | low | The lantern must move with the boat (6 s sweep), so its warmth and reflection are drawn every frame. |
| Campaign | the base campaign's key art | the expansion's key art |

My recommendation is **A for the base campaign and B for the expansion**, so the two campaigns differ at a glance and each keeps its identity. If the owner wants one bucket only, I'd pick A, the calmer of the two.

## HUD

- **Top rail.** The stage (`1-3`) and the level's name on the left; the score on the right, with the label "Score". The score counts up by the measured rule: per 10 ms add 1000 while 10,000 or more is left, then 200, then 100, then 10.
- **Left rail.** The ball channel: a glass tube with brass caps, where the balls stack from the bottom, each shaded where it rests on the one below. The count and "Balls" sit below it.
- **Right rail.**
  - The multiplier dial: a gilt arc fills as oranges are cleared, with ticks at 15, 10, 6 and 3 left, and ×2/×3/×5/×10 in its centre.
  - Below it, the count of oranges left beside a small amber moon.
  - Then the power's medallion: its carrier in silhouette, the power's name, and pips for the turns left.
  - Our own read on this rail replaces the original's tube meter.
- **The free-ball gauge.** A crescent glass channel above the launcher's hub fills with moonlight as the shot's score climbs. At 25k, 75k and 125k its notch blooms (a soft glow, no rays, 0.4 s), and a ball arrives at the top of the channel with a soft bloom and a quiet "+1". A bucket catch gives the same "+1", and a chime.

## Fever ("Full Moon")

- **The approach.** The game drops to 1/10 speed and zooms 1→2× over 0.48 s onto the ball and the last orange (as measured). The sky dims a little, the last orange's halo widens, and the ball leaves a short trail of fading copies.
- **The hit.** Every peg left lights at once ("the board goes full"). The banner reads **FULL MOON**, our own name for the original's distinctive one (decision 2). It sits in the frame's serif with a gilt rule, on a soft band behind the pegs, for 2.95 s.
- **The cups.** Five brass bowls along the foot, valued 10,000 / 50,000 / 100,000 / 50,000 / 10,000. The centre one is gilded brightest, and each value is on a plate set into its bowl.
- No rainbow, no fireworks and no music cue are drawn here. Those are G8's sound and finale work.

## Readability at the smallest window

The window's minimum is **640 × 480 (0.8×)**. The engine draws every sprite natively at the window's scale and never resizes the 1× frame (`readability.png`).

- A peg is 16 px across. Its hue, its lit side and its unlit part all still read, while its seas merge into tone.
- The ball is 10 px across, and stays the brightest small thing on the board.
- The smallest HUD labels are 10 units, so 8 px. That is the floor: nothing on the HUD is smaller.
- Below 0.8× the window refuses to shrink further.

## Reduce motion (and the Decoration levels)

With Reduce motion on:
- **Clearing** is a 120 ms fade, with no bloom, no setting and no dust.
- **Fever.** No zoom: the slow motion stays, because it is a game rule. The banner fades in over 120 ms instead of growing, and the remaining pegs light at once without a bloom.
- **The free-ball notch** brightens without blooming. The "+1" appears without movement.
- **Lit pegs** keep their halo, which is a state, not motion.
- **Bucket B's lantern** keeps a steady light, with no flicker.
- **The score** shows its final value at once rather than counting up.

The Decoration levels: Full draws everything above. Quiet drops the dust, the slow-motion trail and the rails' enamel mottling. Plain draws the pegs flat (the face, the sliver and a ring halo when lit), with no seas, and draws the frame as flat enamel with a 1 px gilt keyline.

## The eleven characters (decision 1)

Original Eorzean characters, moon-themed, drawn to one scale (a Hyur stands about 100 units). The silhouettes are sketches: poses and props that must read at the 48-unit medallion size. Every card is backlit by the moon at the upper left, with its rim on the edges facing that way and a short shadow toward the viewer and right. A prop that glows lights only the nearest surfaces.

| Power | Name | Who | Personality | How the power reads |
|---|---|---|---|---|
| Super Guide | **Pipiru Mimiru** | Lalafell, astrologian apprentice | Counts every star twice and tells you the path before you ask. | Her star globe draws the guide on: silver dots to the first peg, then a fine line through the bounce. Three shots. |
| Multiball | **Kaede Tsukiyo** | Raen Au Ra, dancer | Calm on the outside, competitive underneath; never misses a beat. | Her second chakram leaves her hand: a twin ball springs from the green peg with a short crescent trail. |
| Pyramid | **Marcia nan Arcus** | Garlean, engineer retired from the legion | Left the legions to mend things instead; fussy about rivets. | The brass vanes on her back unfold on the bucket as two wings, widening it. Five turns. |
| Space Blast | **Haldbrand Tidewatch** | Roegadyn, gunbreaker of the Sea Wolves | A gentle giant who raises his voice only to fire. | A lunar cartridge bursts at the green peg: one ring of pale light lights every peg within it. |
| Flippers | **Gajavati** | Arkasodara, ferry-trader from Thavnair | Thavnair's cheeriest trader; swats bad luck away with festival fans. | Two fans fold out at the foot's corners; click and they flick the ball back up. |
| Spooky Ball | **Ysolde Nocturine** | Duskwight Elezen, keeper of the dusk roads | Dry, patient, and opens doors nobody else can see. | A ring of moonlight opens under the board; the ball falls through and drops back in from a ring at the top. |
| Flower Power | **Sister Ottilie** | Midlander Hyur, priestess of Menphina | Kind, unhurried, and impossible to argue with. | Moonflower petals drift from the green peg to the nearest oranges and light them. |
| Lucky Spin | **Gyobo** | Namazu, shrine attendant at the moon-viewing | Flustered, earnest, and sure the drum is never rigged. | The shrine's drum turns once and drops a coloured ball: a free ball, a triple score or another power. |
| Fireball | **Aldous Varrow** | Highlander Hyur, black mage | Studied the red moon's fall for thirty years; very polite, very loud spells. | The ball wears a red-moon ember with a short tail and burns straight through the pegs it meets. |
| Zen Ball | **Ione Selenis** | Sharlayan, sage | Has already worked out your shot, and is sorry about it. | Four nouliths ride beside the ball and nudge it onto the best path, with faint blue arcs. |
| Electrobolt | **Kupsa Brightpom** | Moogle, storm courier | Delivers the bolt, kupo, and signs for nothing. | His pom-pom crackles: a bolt jumps from the first peg hit to the bucket, lighting the pegs along it. |

### The check against Peggle's Masters

The Masters, from public listings: Bjorn (a unicorn), Jimmy Lightning (a beaver), Kat Tut (a cat), Splork (an alien), Claude (a lobster), Renfield (a jack-o'-lantern), Tula (a sunflower), Warren (a rabbit), Lord Cinderbottom (a dragon), Master Hu (an owl) and, in Nights, Marina (an electric squid).

- **Species avoided outright.** No horse or unicorn, rodent or beaver, cat (so no Miqo'te, Hrothgar or Fat Cat), alien, crustacean, jack-o'-lantern or undead (so no Bomb, whose round glowing face echoes Renfield's), plant (so no Sylph or Cactuar), rabbit (so no Viera or Loporrit), dragon or lizard (so no Amalj'aa or dragon), owl or bird sage, and no squid or cephalopod.
- **Per power, against the Master who held it:**

| Power | Original's Master | Ours | Species | Look | Role |
|---|---|---|---|---|---|
| Super Guide | Bjorn, a unicorn and the Masters' mentor | Pipiru, a Lalafell apprentice | different | a small hooded figure with a globe, not a horned horse | an apprentice, not the mentor |
| Multiball | Jimmy, a hyperactive beaver | Kaede, a poised Au Ra dancer | different | horns and a tail, chakrams | composed, not hyper |
| Pyramid | Kat Tut, an Egyptian cat | Marcia, a Garlean engineer | different | a coat, a spanner and brass vanes; nothing Egyptian | and the power's name changes (below) |
| Space Blast | Splork, an alien from space | Haldbrand, a Roegadyn gunbreaker | different | a gunblade, a fur collar | a soldier, nothing from space |
| Flippers | Claude, a lobster with claws | Gajavati, an Arkasodara with fans | different | fans, not claws; no shell | |
| Spooky Ball | Renfield, a jack-o'-lantern | Ysolde, a Duskwight Elezen | different | a tall cloak and a ring of light; no pumpkin, nothing undead | a gatekeeper, not a ghost |
| Flower Power | Tula, a sunflower | Ottilie, a Hyur priestess | not a plant | small white moonflowers and a lantern; no sunflower | |
| Lucky Spin | Warren, a fast-talking rabbit showman | Gyobo, a Namazu shrine attendant | different (catfish) | a lottery drum, not a wheel and a hat | earnest and flustered, not a huckster |
| Fireball | Lord Cinderbottom, a dragon | Aldous, a Hyur black mage | not a dragon or lizard | red-moon fire on a staff | and his name avoids "cinder" |
| Zen Ball | Master Hu, a wise old owl | Ione, a young Sharlayan sage | not a bird | nouliths, a book | a calculating student, not a meditating elder |
| Electrobolt | Marina, an electric squid | Kupsa, a moogle | not aquatic | a pom-pom and bat wings | a courier |

- **Borderline, for the owner.** The Namazu is a fish (a catfish), and Marina is a sea creature. They carry different powers, and neither looks nor acts like the other, so I kept Gyobo.
- **Display names.** Per decision 2, the distinctive power names get our own: Pyramid → **Brass Wings**, Space Blast → **Lunar Burst**, Spooky Ball → **Moon Gate**, Flower Power → **Moonbloom**, Zen Ball → **Sage's Path**, Electrobolt → **Storm Post**, Lucky Spin → **Moon-Viewing Draw**. The general ones stay as they are: Super Guide, Multiball, Flippers and Fireball. These are proposals, listed as open questions below.

## The campaigns (G6, the owner's answer)

Two tiles with one identity:
- the same sky grammar: one near-full moon high on the left, and one falling star on the right that lights nothing;
- the same lockup at the lower left: MOONFALL in spaced capitals over a gilt rule, with the campaign's name beneath in italic and one line of facts;
- the same Medallion finish: the varnish grade, the vignette and the slim gilt slip lit from the upper left.

| | Base: **The Moon Road** (55 levels) | Expansion: **The Far Shore** (60 levels, opens after The Moon Road) |
|---|---|---|
| Hour and palette | night, Night blue | later, a deeper violet |
| Scene | a pale road over moonlit hills to a waystation where a brass bracket holds a lantern (the crescent cradle's family); a traveller on the road | open sea, a lantern boat (bucket B) bound for a headland with a gate |
| Practical light | the waystation lantern | the boat's lantern |

## What's new, 1.23.0 "Moonfall" (`whatsnew-1.23.0.jpg`)

Option B pipeline, as in `docs/design/v8/spec-1.22.md` W2, with the Medallion treatment run unchanged from `docs/design/v8/art/src/option_b_themes.py`.

- **The scene.** A still lake at night.
  - One near-full moon high on the left lays its broken path on the water. Its glints are cool and desaturated, cooler than the lantern's.
  - Right of centre, a small lantern boat (bucket B) carries one traveller, who looks up at a single falling star beyond the far hills. The star lights nothing.
  - The paper lantern on the stern post is the one warm practical light. It warms the traveller's near side, the post and the stern, and lays a broken column on the water.
  - The far ridges are lit only on their moon-facing slopes. Reeds frame the near corners, rim-lit on their moon side.
- **The one moon rule.**
  - One circular limb.
  - An internal soft terminator, with the unlit part on the side away from the sun.
  - Six separate lobed maria.
  - A neutral tone.
  - The moon is kept out of every brush pass.
  - The treatments get a moon mask 2.5 px inside the limb, so their restore of the moon's own paint ends at the limb and leaves no ungraded ring.
- **The placement data** is in `art/moonfall-b.json`. `lines` keeps the post and the falling star crisp; `far_layers` and `field_flat` describe the lake.
- **Shipped:** 1120 × 440, JPEG quality 88, 4:4:4, progressive. It is about 60 KB, well inside the 600 KB budget. Medallion only, per the owner's rule of 4 October 2026.

## Runtime assets needed

The engine can draw the pegs and the HUD procedurally (every recipe here is analytic: signed distances and normals). Alternatively it can bake them once per window scale. Baking at the window's scale is recommended, so the 0.8× minimum stays sharp.

| Asset | Count | Notes |
|---|---|---|
| Peg sprites | 4 kinds × 2 states × 4 sea layouts × 3 turns = 96 | Baked at the window scale, about 32 px square at 1× and 64 at 2×. The light never rotates with the sprite: the turns are baked. |
| Brick sprites | 4 kinds × 2 states, per brick shape in the level (straight, or curved by radius) | Or drawn procedurally from the arc. |
| Clearing | the bloom and the setting are the lit sprite scaled and faded; dust is 28 specks drawn as soft points | No sheet needed. |
| Ball | 1 sprite, plus r 10.5 for the channel | |
| Launcher | hub, yoke, tube (drawn rotated about the pivot, but shaded in screen space so its highlight stays upper left), gauge channel, notch bloom | The tube's shading must not rotate with it: draw it procedurally or bake 1° steps. |
| Bucket A | cradle, rail, wheels | |
| Bucket B | hull, rail, post, lantern, water strip, reflection (drawn each frame), warm column | |
| Frame and rails | outer bead, wall beads, enamel panels | 9-slice. |
| HUD | ball channel (glass, caps), multiplier dial (groove, gilt arc, ticks), orange icon, power medallion (bezel, enamel), pips | |
| Character medallions | 11 silhouettes at 48 units, rim-lit | Baked from `src/figures.py`. |
| Fever | 5 cups with plates, banner type and rule, scrim band | |
| Key art | `campaign-base`, `campaign-expansion` | For the mode-select screen. |
| Release art | `whatsnew-1.23.0.jpg` | Into `Data/` with the other release art. |
| Fonts | the plugin's own: Dalamud's default UI font for numbers and labels, and its serif fallback for titles | The mocks use Georgia and Segoe UI only as stand-ins. |

## Supervision record

The supervisor's verdicts are recorded verbatim in `supervisor/round-N.md` and copied below.

(See the rounds that follow.)

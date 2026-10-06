# Tsukimichi feature plan v9: Moonfall, a peg game

Status: **reviewed on 4 October 2026, design approved on 5 October 2026, and executing** (see "Your answers" below). Moonfall is a fun add-in inside Tsukimichi, opened from a normal button on the main window. It is our own game, with our own levels, art, music, names and characters, built to play exactly like Peggle Deluxe and Peggle Nights.

## Sources

- **Your request.** You asked for your own version whose mechanics and behaviour match Peggle Deluxe and Peggle Nights, and for the physics, speeds and triggers to be measured from gameplay videos.
- **Mechanics, from public sources only.** `docs/research/plan-v9/peg-mechanics.md`. The main source is PopCap's own patents (US 8,128,476 and US 8,678,904), backed by wiki and guide excerpts. It covers peg values, the 25 orange pegs, greens and purple, 10 balls, free-ball thresholds, Fever buckets, style shots, every Master's power, modes and level rules.
- **Physics and timing, measured from footage.** `docs/research/plan-v9/peg-measurements.md`, with re-runnable scripts in `measure/`. Two full-window 60 fps longplays (Deluxe and Nights) were tracked frame by frame. The results:
  - a fixed 100 Hz simulation and gravity of 500 px/s²;
  - a launch speed of about 395 px/s;
  - peg restitution of 0.80 with almost no friction, and walls at 0.75;
  - the bucket's 6-second sine sweep;
  - Fever slowing to 1/10 with a 1→2× zoom;
  - the exact score count-up rule;
  - the two-note peg sound that climbs a semitone per peg.
- **Nothing decompiled.** No game file or binary was opened. Peggle's code, levels, art, music, characters and name stay out of Tsukimichi.

## Standing rules

All of plan v8's rules carry over: player value first, English only, a realism supervisor on every piece of art, Reduce motion and the three Decoration levels, static layout, Undo or confirm on destructive clicks, and every plan gets a website.

Rules new to this plan:
- **Match the originals in behaviour, never in content.** Each mechanic cites its source line in the research.
- **The feel is tuned against your own copy.** A tuning build exposes the values that couldn't be measured: the stuck-ball rule, the aim limit, the catch zone and the Fever trigger. You play your copy and ours side by side and we set them.

## 1.23.0 · Moonfall

| Id | Item | Effort |
|---|---|---|
| G1 | **The engine.**<br>• A fixed 100 Hz step.<br>• Gravity 500, launch speed about 395, ball radius 6 against peg radius 9–10, peg restitution 0.80 and wall restitution 0.75, no spin. All in the original's 800×600 playfield units, scaled to the window.<br>• Pegs light when hit and clear at the end of the turn (0.57 s, then one every 50 ms, in hit order). A stuck ball clears its pegs early.<br>• Pure Core code with tests that replay the measured arcs and bounces. | L |
| G2 | **Pegs and scoring.**<br>• Blue 10, green 10, orange 100, purple 500; 25 orange pegs per level, 2 greens from level 3, and one purple that moves each shot.<br>• A shot scores (sum of its peg values) × (number of pegs hit).<br>• The multiplier rises to ×2 at 15 oranges left, ×3 at 10, ×5 at 6 and ×10 at 3.<br>• Free balls at shot scores of 25k, 75k and 125k, and one for landing in the bucket.<br>• 10 balls per level.<br>• The score counter uses the measured count-up rule. | M |
| G3 | **The bucket and Fever.**<br>• The bucket sweeps on a sine: 6 s period, ±260 px, a 104 px mouth.<br>• Near the last orange the game drops to 1/10 speed and zooms 1→2× over 0.48 s. A drum roll and a banner appear when it's hit.<br>• Five Fever buckets at 10k / 50k / 100k / 50k / 10k; all five pay 100k for a perfect clear.<br>• Fireworks, then the end-of-level tally. | M |
| G4 | **Style shots.** Long Shot, Super Long Shot, Off the Wall, Free Ball Skillz, Orange Attack, Double Long Shot, Lucky Bounce, Extreme Slide, Cool Clear, Kick the Bucket and the rest from the research, each with its bonus. They get our own names (decision 2). | M |
| G5 | **Eleven powers, carried by our own characters.**<br>• Super Guide, Multiball, Pyramid, Space Blast, Flippers, Spooky Ball, Flower Power, Lucky Spin, Fireball, Zen Ball and Electrobolt, each behaving and lasting as the research says.<br>• Each is carried by a character of our own: FFXIV-flavoured, moon-themed and original (decision 1).<br>• A green peg triggers the power. | L |
| G6 | **Levels.**<br>• Our own: 55 for the first campaign and 60 for the second, Deluxe-sized and Nights-sized, combined into one game as you asked.<br>• They follow the original's design rules: 25 oranges, greens, movers and bricks.<br>• Built with a small in-plugin level editor that is also a dev tool (decision 4). | L |
| G7 | **Modes.**<br>• Adventure: stages of five levels, each unlocking its character.<br>• Quick Play.<br>• Challenges: score, clear-all and limited-ball challenges.<br>• Duel against an AI opponent.<br>• Ace scores, as in Nights. | M |
| G8 | **Look and sound.**<br>• Tsukimichi's night sky, moons as pegs, and each theme's frame.<br>• Original sound: the peg hit is two notes a fifth apart, climbing a semitone per peg.<br>• Original music for the finale, not "Ode to Joy" (decision 3).<br>• Every piece of art goes through the realism supervisor. | L |
| G9 | **The easter egg.**<br>• Hidden and opt-in: click the moon icon seven times, or type `/tsuki moonfall`.<br>• It opens in its own popup window, which can be resized and paused, and it pauses automatically in combat, in duties and in cutscenes.<br>• Progress is saved per account. | S |
| G10 | **The tuning build.** Sliders for every value the footage couldn't pin down. You play side by side with your copy, then we lock the values. | S |

## Decisions for you

| # | Question | My recommendation |
|---|---|---|
| 1 | The eleven characters who carry the powers: original FFXIV-flavoured spirits (a Lalafell astrologian, a Namazu, a moogle, a Garlean engineer, …), or the moon phases themselves as characters? | Original FFXIV-flavoured characters, each designed by the designer with the realism supervisor. They read clearly as "who gives which power" without echoing any of Peggle's Masters. |
| 2 | Names for the style shots and powers: plain descriptive names ("Long Shot"), or new ones of our own? | Keep the plain descriptive names that are general terms (Long Shot, Multiball, Fireball). Give our own names wherever the original's name is distinctive. |
| 3 | The finale music: an original orchestral piece in the style of a joyful classical finale, or a public-domain classical piece other than "Ode to Joy"? | An original piece, composed as code-generated audio. It is safest, and it can carry Tsukimichi's moon motif. |
| 4 | Levels: hand-authored only, or hand-authored plus a few generated by rules? | Hand-authored with the level editor, so each one is designed for play. |
| 5 | Name: "Moonfall" or another? | Moonfall. |
| 6 | Where it lives: inside Tsukimichi behind the easter egg, or as a separate plugin? | Inside Tsukimichi, hidden and opt-in, as you asked. |
| 7 | The bucket (from the approved art, see Designs): the crescent cradle (A) for the base campaign and the lantern boat (B) for the expansion, or one bucket for both? | A for the base campaign and B for the expansion, so the two campaigns differ at a glance. If only one, A, the calmer. |
| 8 | Our names for the distinctive powers: Pyramid → Brass Wings, Space Blast → Lunar Burst, Spooky Ball → Moon Gate, Flower Power → Moonbloom, Zen Ball → Sage's Path, Electrobolt → Storm Post, Lucky Spin → Moon-Viewing Draw (Super Guide, Multiball, Flippers and Fireball stay)? | As listed. |
| 9 | Fever's banner: "FULL MOON"? | FULL MOON. |
| 10 | Campaign names: "The Moon Road" (base) and "The Far Shore" (expansion)? | As listed. |
| 11 | Gyobo the Namazu (a catfish) carries Lucky Spin. Nights has a sea creature among its Masters; they differ in power, look and role. Keep him? | Keep him. |
| 12 | Loporrits, FFXIV's moon rabbits, are left out because one of Peggle's Masters is a rabbit. Agree? | Agree: leave them out. |
| 13 | The smallest Moonfall window: 640 × 480 (0.8× the playfield)? | 640 × 480. |
| 14 | Buckets, after the rich pass (replaces 7): the new lantern cart for The Moon Road and the lantern boat for The Far Shore, or the boat for both? | The cart for the base campaign and the boat for the expansion. |
| 15 | Build about two thirds of the 115 levels on the game's own loading-screen paintings, read from your install and night-graded at load (nothing extra ships), with our own paintings for the rest? The alternative is our own painting for every level: about 30 MB more and far more painting. | Yes: the game's paintings for two thirds, ours for the rest. |
| 16 | Level format v2: add `canBeGreen` (keep greens off a figure's eye or a constellation's star) and make `scene` official? | Yes to both. |
| 17 | The eleven characters as moonstone cameos (carved portraits in brass bezels), or painted portraits in the night palette? | Moonstone cameos. |
| 18 | Names: stage 4 "The Shroud by Night", and its levels "Bentbranch at Dusk" and "The Twelveswood" (placeholders). | Approve, or give new names. |
| 19 | Bundle one open-licence display serif (about 60 KB) for MOONFALL and the banners, or keep Dalamud's fonts with the logotype baked as an image? | Bundle the font. |
| 20 | The Far Shore ends on the moon itself (Mare Lamentorum). Is that the right ending? | Yes. |
| 21 | Every level must play at least as well as the weakest shipped level in the greedy-player test (5 of 48 wins), alongside the pre-flight. Make it a rule? | Yes. |
| 22 | The cast (rich pass 2, replaces 1 and 17): Minfilia (Super Guide), Alphinaud & Alisaie (Multiball), Cid (Brass Wings), Raubahn (Lunar Burst), Merlwyb (Flippers), Urianger (Moon Gate), Kan-E-Senna (Moonbloom), Tataru (Moon-Viewing Draw), Y'shtola (Fireball), Louisoix (Sage's Path), a moogle courier (Storm Post). Two sit near Peggle's: Y'shtola's cat ears, Kan-E-Senna's flower. | Approve as listed. |
| 23 | Before you meet Alisaie, show the twins' card face down, or Alphinaud alone? (Alphinaud alone needs a later portrait, which spoils.) | Face down. |
| 24 | Use the game's own fonts (Jupiter, AXIS, TrumpGothic) through Dalamud, with nothing bundled? (Replaces 19.) | Yes. |
| 25 | Approve the eleven stage names, each themed to its companion's home. | Approve. |
| 26 | Ambient motion by default: Decoration "Full" or "Simple" (beams and halos only)? Reduce motion always means still. | Full. |
| 27 | Peg marks (a shape on each peg kind for colour-blind players): off by default with a first-run hint, or on? | Off, with the hint. |
| 28 | The full-quality motion previews are 38 MB; small MP4s (about 0.5 MB) are in the repo and on this site. Keep the large ones out of the repo? | Yes, MP4s only. |

### Your answers (4 October 2026, on the plan site)

- **Build:** G1 to G8 all "build it".
- **G9, how it opens:** "Make it a normal button on the main display rather than an easter egg (it's just a fun addin for the plugin)." Moonfall gets a plain button on the main window; there is no hidden trigger. `/tsuki moonfall` stays as a shortcut.
- **G10, the tuning build:** "Just review the current findings to the best of your ability, and move forward with the game." No tuning build. The values the footage couldn't pin down (the stuck-ball rule, the aim limit, the catch zone, the Fever trigger) are set from the research and the footage by judgement, and recorded with their reasoning.
- **G6, levels:** "Still the same game, but there's an expansion to it." One game with a base campaign (Deluxe-sized, 55 levels) and an expansion campaign (Nights-sized, 60 levels) that opens after the base Adventure, as Nights did, rather than one merged campaign.
- **Your request, point 4 (measure from footage):** "Utilize the game files for direct references where needed." Not done: the plan keeps to public sources and footage, and Peggle's own files stay unopened (see Not doing). Where a value is still uncertain, the footage is re-measured.
- **Your request, point 1:** "change" (the button, above). Points 2 to 4: "good".
- **Decisions 1 to 6:** not answered on the site, so the recommendations stand: original FFXIV-flavoured characters; plain descriptive names for general terms and our own names for distinctive ones; an original code-generated finale; hand-authored levels with the in-plugin editor; the name Moonfall; inside Tsukimichi.
- **Art:** one painting per release in the default theme, per your rule of 4 October 2026.
- **Art review (5 October 2026):** "design looks okay … It needs more detail and passion in it, as it looks somewhat plain. I want this to have beautiful designs in every part of the main interface, and also the game art (levels)." Every level gets a design that builds around it or gives it meaning: its pegs and bricks outline, trace, follow or emphasise something in the level's painted scene. You voted "no" on style frame A (the crescent-cradle bucket) and "fav" on the rest. A richer pass of every screen, the level-scene method and six pilot levels comes back here for review before all 115 levels are made. Decisions 7 to 13 stay open until then.
- **Rich pass review (5 October 2026):** "Everything looks okay so far. I think the pictures for the characters look ugly, and it still looks a bit plain overall."
  - **Characters:** the eleven power carriers become **real FFXIV characters**, shown with the game's own portrait art from your install. This replaces our originals (decision 1) and the cameos (question 17).
  - **Richness:** all four levers.
    - FFXIV's own ornate UI art, graded to Menphina's Medallion.
    - Ambient motion (moondust, glints, stars, parallax, lantern flicker; none under Reduce motion).
    - Busier, fuller boards (foreground silhouettes, framing, light shafts).
    - Bolder colour (jewel tones and warm gold, per level).
- **Rich pass 2 review (5 October 2026):** "Looks good. Go ahead and keep going." Every open decision (7 to 28) takes its recommendation:
  - the cast as listed (22);
  - the twins face down until Alisaie is met (23);
  - the game's own fonts (24);
  - the eleven stage names (25);
  - Decoration "Full" by default (26);
  - Peg marks off, with a first-run hint (27);
  - MP4 previews only (28);
  - the lantern cart for The Moon Road and the boat for The Far Shore (14);
  - the game's loading-screen paintings for about two thirds of the levels (15);
  - level format v2 with `canBeGreen` and `scene` (16);
  - The Far Shore ends on the moon (20);
  - the greedy-player playability rule (21);
  - FULL MOON (9);
  - the campaign names (10).

  Superseded: the original eleven characters (1), the cameos (17) and the bundled font (19). Gyobo (11) and the Loporrits (12) are moot now the cast is real FFXIV characters.
- **The Far Shore (5 October 2026).**
  - **Stage names:** approved, with "The Floating Market" for stage 8. The full list is The Lantern Quay, The Twin Lights, The Skyward Deck, The Sunlit Isles, The Admiral's Sea, The Ferry in the Stars, The Floating Grove, The Floating Market, The Domes of Sharlayan, The Archon's Crossing, The Courier's Wake and The Sea of Sorrows.
  - **Spoilers:** any stage set past your story (Endwalker's Sharlayan and the moon, for example) follows the spoiler shield. It shows a masked name and a veiled preview, and opens when your story reaches it or when you reveal it.

## Not doing

- **Decompiling or opening Peggle's files.** Everything comes from public documentation and footage.
- **Copying Peggle's levels, art, music, characters, voice lines or name.**

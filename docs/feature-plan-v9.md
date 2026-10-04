# Tsukimichi feature plan v9: Moonfall, a peg game

Status: **draft for your review (4 October 2026).** This is a hidden, opt-in easter egg inside Tsukimichi. It is our own game, with our own levels, art, music, names and characters, built to play exactly like Peggle Deluxe and Peggle Nights.

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

## Not doing

- **Decompiling or opening Peggle's files.** Everything comes from public documentation and footage.
- **Copying Peggle's levels, art, music, characters, voice lines or name.**

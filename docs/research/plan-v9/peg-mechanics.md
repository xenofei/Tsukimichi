# Peg-shooter mechanics brief (Peggle Deluxe 2007 / Peggle Nights 2008)

Researched 2026-10-04 from public documentation only. No game binary, extracted asset or reverse-engineered file format was read. Purpose: clean-room mechanics reference for an original game (own art, levels, names, sounds). Do not reuse the names "Peggle", the Master characters, level names, music (the Beethoven "Ode to Joy" arrangement, etc.) or art.

## Source quality and access limits (read first)

- Peggle Wiki (Fandom), StrategyWiki, GameFAQs, Steam guides, PC Gamer and speedrun.com all refused direct fetches (HTTP 402/403/429). Fandom facts below were therefore obtained through web-search excerpts of those pages, not by reading the pages. Treat them as "secondary, snippet-level" even where the URL is a wiki page.
- Best primary source obtained: PopCap's own patent family describing the game (US 8,128,476 B1, filed 2008-02-01; continuations US 8,398,476 and US 8,678,904 B1). Fetched via Google Patents. It gives peg values, free-ball thresholds, Fever buckets, several style shots and Master powers. Note the fetch tool summarises pages, so quotes are the tool's extraction, not a manual read.
- Tags used below: [P] = PopCap patent; [W] = Fandom wiki via search excerpt; [S] = other secondary source; [?] = unverified or conflicting.

URLs:
- [P] https://patents.google.com/patent/US8128476 and https://patents.google.com/patent/US8678904B1/en
- [W-Scoring] https://peggle.fandom.com/wiki/Scoring_System
- [W-Fever] https://peggle.fandom.com/wiki/Fever_Meter
- [W-Style] https://peggle.fandom.com/wiki/Style_Shots
- [W-Masters] https://peggle.fandom.com/wiki/Peggle_Masters
- [W-Duel] https://peggle.fandom.com/wiki/Duel_mode
- [W-Challenges] https://peggle.fandom.com/wiki/Peggle_Deluxe/Challenges and https://peggle.fandom.com/wiki/Peggle_Nights/Challenges
- [WP] https://en.wikipedia.org/wiki/Peggle and https://en.wikipedia.org/wiki/Peggle_Nights
- [MW] https://www.macworld.com/article/189450/peggledeluxe.html
- [PCG] https://www.pcgamer.com/the-making-of-peggle/ (not readable directly; used through search excerpt)
- [PE] http://intelorca.co.uk/PeggleEdit/Pegs.html, .../Bricks.html, .../AdvancedMovement.html (community level-editor user docs)
- [CB] https://www.cheatbook.de/files/peggledeluxe.htm
- [AP] https://archipelago.miraheze.org/wiki/Peggle_Deluxe (Archipelago randomizer wiki; secondary)

## 1. Physics

What sources say:
- The physics component "determines the amount of bounce that the ball experiences after contacting a peg based on modeling an elastic collision" [P]. That is the only physics statement in the patent; no gravity, speed, radius, restitution or time step are given.
- The ball's path is curved by gravity, so the aiming line is curved [P, Super Guide passage].
- The player aims the Launcher with mouse or joystick and fires with left click [P].
- The ball is removed from play when it falls below the board or lands in the Free Ball Bucket (it is returned to the launcher for another shot) [P].
- Bucket: "moves back and forth across the bottom of the board". The player can speed it up with right mouse button only while no ball is in play; once the ball leaves the Launcher the player cannot control it [P]. Speed-up with right click before shooting is also noted in [CB].
- Stuck ball: "the ball may become stuck and Peggle may remove a peg before the end of the turn to keep the ball in play" [P]. Wiki-level wording: struck pegs are removed if the ball is stuck on them; pegs are normally cleared when the ball falls out or is caught [S via search: https://peggle.fandom.com/wiki/Peggle_Deluxe]. A per-peg "Quick Disappear" flag exists in the editor docs: the peg vanishes shortly after impact, "reducing ball-sticking time in peg arcs"; it applies to bricks and pegs [PE Pegs].
- Free Ball Bucket catch returns the ball and grants a free ball (also see section 3).

NOT found (must be measured from gameplay or chosen by us):
- Gravity, launch speed, aim angle limits, ball radius vs peg radius, restitution for pegs/bricks/walls, timeout for the stuck rule, bucket speed/width/path (beyond left-right sweep), aiming-guide length for the base game (it is a short curved dotted line up to first contact; exact length not documented).
- Do not use the numbers from the open-source clones I met (e.g. 0.5 s stuck timeout / 0.2 u/s from the "Pigle" project, or the NUS CS3217 course spec): they are not Peggle's.

Super Guide: extends the aiming line to show the path through one bounce [P]. Duration three shots, starting the next shot [P]; wiki lists "3 turns, activates next turn" [W-Masters via search].

## 2. Pegs

- Blue: normal. Orange: objective. Green: power-up. Purple: bonus. Bricks: rectangular (straight or curved sectors, "typically rectangles with a width of 20" in editor units) [PE Bricks]. Bricks are pegs of a different shape; they can also be flagged "Can Be Orange".
- At level start the game picks 25 orange pegs at random from pegs flagged "Can Be Orange" (default; challenges vary) [PE Pegs, P "25 orange pegs scattered randomly"].
- Two green pegs per level, starting on the third level [P]. Duel: only one of the two greens is present at a time, the other appears next turn [W-Duel].
- One purple peg per turn, location changes every shot [P]. Editor docs: 1 blue becomes purple, 2 blue become green at play time [PE Pegs].
- Hit pegs light up (hue lightens), they are NOT removed immediately; removed at end of turn, e.g. when the ball exits or is caught [P].
- Stuck-ball early removal: see section 1.
- Purple selection rule beyond "random among remaining" is not documented (does it pick only pegs still blue? [?]).
- Total pegs per level: Wikipedia says "approximately 100" (blue count; loose) [WP]. See section 7.

## 3. Scoring

Base values [P]: blue 10, green 10 [WP clones/secondary agree], orange 100 (10x blue), purple 500 (50x blue). Each peg is multiplied by the Fever Meter multiplier in effect.

Multiplier (Fever Meter) by orange pegs remaining [W-Fever via search; "x2 at 15 remaining, x3 at 10, x6->x5 at 6, x10 at 3"]:

| Oranges remaining | Multiplier |
|---|---|
| 25 down to 16 | x1 |
| 15 to 11 | x2 |
| 10 to 7 | x3 |
| 6 to 4 | x5 |
| 3 to 1 | x10 |

The wiki gives only the boundary values (15, 10, 6, 3). The ranges above are my reading ("reaches the step when that many remain"); confidence medium. An Archipelago wiki confirms the multiplier tiers 2x/3x/5x/10x [AP]. In challenges with more than 25 oranges the meter stays frozen until 25 remain [W-Fever via search]. WARNING: a different threshold set (16+ x1, 11-15 x2, 8-10 x3, 4-7 x5, 1-3 x10, none left x100) appears in a university clone spec (https://github.com/BryannYeap/Peggle). It is not the original.

Per-shot score: sum of (peg base x multiplier at the time of hit) plus style bonuses. The patent says the scorer "adds any style points to the shot score" [P]. The clone formula "(sum) x number of pegs hit" is NOT original.

Free balls [P]: green light in the glass tubes beside the launcher fills as the SHOT score rises. 25,000 in one shot = 1 free ball; 75,000 = second; 125,000 = third. Also catching the ball in the bucket = 1 free ball. [MW] confirms 25,000 per single shot.

Style shots (name, condition, points; Duel value in parentheses) [P] and [W-Style via search]:
- Free Ball Skillz ("Freeball Skills" in patent): bounce from exactly one peg into the Free Ball Bucket. 5,000 (2,500).
- Long Shot: hit a non-blue peg, travel about 1/3 of screen width, hit another non-blue peg next or soon after. 25,000 (5,000).
- Super Long Shot: same at about 2/3 screen width. 50,000 (10,000).
- Double Long Shot (Nights, [W]): a Long Shot followed by another 1/3-width leg to a third non-blue peg. 25,000 (10,000) [?: Duel number as quoted].
- Off the Wall (Nights): bounce off a wall, travel about 1/5 screen width, hit a non-blue peg. 25,000 (5,000) [W-Style]. Wikipedia confirms it was added in Nights [WP Nights].
- Kick the Bucket: bounce off the Free Ball Bucket and hit the final orange peg to trigger Fever. 25,000 (5,000) [W]; patent gives no value.
- Lucky Bounce: bounce off the bucket for a certain time or 1/4 screen height, then catch the ball in the bucket. 25,000 (Duel 2,500 or 5,000 [?: sources conflict]).
- Orange Attack: hit a large share of the remaining orange pegs in a single shot (the required count depends on how many remain). 50,000 (5,000 Duel) [W].
- Extreme Slide: slide along pegs hitting 12. 50,000 (5,000 Duel) [W].
- Cool Clear: Nights; get Ultra Extreme Fever (all pegs cleared) on a shot hitting at least two pegs. 50,000 [W]; a second wiki-snippet describes it as "hit final orange, then all remaining pegs" so definition [?].
- Shock It To Me: Nights, Electrobolt shot lights 12+ pegs. 25,000 (5,000 Duel) [W].
- "Bank Shot" was named in the task: not found in any Deluxe/Nights source; do not assume it exists.
- Style bonus values are all multiples of 5,000 and do not appear to be multiplied by the Fever multiplier [?: not verified].

Fever bonus buckets [P]: left to right 10,000, 50,000, 100,000, 50,000, 10,000.
Fever peg values: sources quote "x20" and blue 1,000 / green 2,000 / purple 10,000 during Fever [CB, search]. Conflicts with the x10 maximum; values are not consistent between sources [?]. Treat as unverified.
End bonus: "10,000 points for each unused ball" [CB]. This is also stated by the guides for Deluxe; confirm by play.
Duel: missing all orange pegs costs 25% of score [W-Duel]. Ultra Extreme Fever (all pegs cleared) is a separate award [WP].
Ace (Nights): each level has a fixed "Ace" score target; beating it grants a bonus and an "Aced" badge, "like beating an expert score" [https://steamcommunity.com/app/3540/discussions/0/364040797993883695/]. Per-level values and the bonus size not found.

## 4. Fever

- Hitting the last orange peg completes the level and starts Extreme Fever: zoom in on the ball, slow motion, text "Extreme Fever" [P]. Developer interview: it began as a placeholder text plus "Ode to Joy"; the team later added a zoom on the ball as it nears the last orange [PCG via search].
- Trigger for slow motion BEFORE the hit (ball merely approaching the last orange): sources say the game slows and zooms "when you hit your last peg"/nears it [S search]; the exact proximity rule is not documented [?]. Left click ends slow motion [P].
- Flow: ball falls into one of five buckets (10k/50k/100k/50k/10k), pegs on the board are 'fever' pegs worth more, remaining balls give 10,000 each, then a score tally [CB, P].

## 5. Masters and powers

Power activates when the ball hits a green peg. Duration by activation and shots/turns per wiki search excerpt [W-Masters] unless marked [P]. Conflicts noted.

| Master (rename ours) | Power | Behaviour | Duration / timing |
|---|---|---|---|
| Bjorn | Super Guide | Longer aim line, shows path through first bounce | 3 shots, from next shot [P][W] |
| Jimmy | Multiball | A second ball spawns from the green peg | immediate, this shot only [P][W] |
| Kat Tut | Pyramid | Bucket gets a wide pyramid frame that widens it | 5 turns [W]; not mentioned in the patent |
| Splork | Space Blast | Explosion lights all pegs within a radius (about 4 pegs wide) of the green peg | immediate, this shot [P] |
| Claude | Flippers | Two flippers appear at bottom corners; click to bat the ball | patent: lasts the shot; wiki: 3 turns [conflict] |
| Renfield | Spooky Ball | Ball reappears at top after falling out | this shot only [W search]; another excerpt said 2 turns [conflict] |
| Tula | Flower Power | When the green peg is hit, lights the closest one fifth (rounded up) of remaining oranges | immediate [P] |
| Warren | Lucky Spin | Wheel: Free Ball, Triple Score, Magic Hat, or a random Magic Power | wiki: lasts 2 turns; exact wheel probabilities not found |
| Lord Cinderbottom | Fireball | Ball burns through pegs, lights and clears them without bouncing | next shot, one shot [P] |
| Master Hu | Zen Ball | Nudges the ball to the highest-scoring path, weighting the bucket and orange pegs | next shot, one shot [P] |
| Marina (Nights only) | Electrobolt | Lightning arcs from the first peg hit to the bucket centre, clearing pegs in its path including armoured pegs | Nights only [WP Nights]; duration not found |

- Nights adds Marina (an 11th power) and style shot "Shock It To Me" [W][WP].
- "Magic Hat" gives a random master (the Blast name "Hat of Fate" is a different game).
- Zen Ball details in Nights vs Deluxe: no differences found; do not assume.
- Number of masters: 10 in Deluxe [WP], 11 in Nights [WP Nights].

## 6. Modes and structure

- Deluxe: Adventure 55 levels, 5 per Master, 10 Masters, plus 5 Master levels where the player chooses any Master [WP]. Quick Play replays unlocked levels [MW]. Challenge mode unlocks after Adventure [MW]. 75 challenges in Deluxe [S via search, unverified].
- Balls: 10 per level [P]. Duel: 5 or 6 each depending on platform [W-Duel].
- Duel: two players alternate shots, higher score wins; failing to hit any orange loses 25% of score; Extreme Fever still occurs with reduced bucket values (Duel style values are about 1/5 to 1/2 of normal) [W-Duel].
- Challenge types in Deluxe [W-Challenges]: "45 Orange Pegs" (45 oranges), "In the Clear" (clear all pegs), "Clearly Impossible!" (clear all pegs on hard levels), multilevel runs of 2-6 random levels, three-level Duels against Masters at set difficulty.
- Nights: 60 levels on dream stages, 60 challenges, final Master levels where any Master can be used, "Aced" scores [WP Nights]. Challenge types: score-target with fewer balls (e.g. 200,000 with 7 balls), half-ball, 35 oranges, style-shot tasks, flipper-only "Pinball" and limited-Fireball "Low Ball" [W-Challenges search].
- Peggle Master Duels: not found in sources reached.

## 7. Level design rules

- 25 oranges chosen from flagged pegs [PE]; designer marks which pegs may be orange. Nights editor docs: "Quick Disappear" for pegs in tight arcs [PE].
- Shapes: round pegs, straight or curved-sector bricks, polygons, rods, circles [PE index].
- Movers: any peg can orbit on nested movement info (radius, period, anchor points; chains give unlimited cycles) [PE Advanced Movement]. The original designer found fast-moving targets made levels too frantic, so most levels use a static field [PCG via search].
- About 100 pegs per level [WP], 2 greens (from level 3), 1 purple picked at runtime, 25 orange [P].
- Not found: official peg size, spacing rules, maximum counts, designer guidelines. The editor docs from the reverse-engineering community were not used for numbers.

## 8. Feel

- Peg hit: peg lights up, a note rises per consecutive peg hit in a turn [commonly described; no source reached with the scale; [?]].
- End of turn: lit pegs clear when the ball falls out or lands in the bucket [P].
- Fever: slow motion, zoom, "Ode to Joy" (do not copy), rainbow and unicorn, then buckets [PCG search; S].
- Score counting-up timing: not found.

## Confidence and gaps

Confident (primary patent): base peg values, 25 orange, 2 greens from level 3, 1 purple/turn, 10 balls, free ball at 25k/75k/125k, bucket values, three style shots, five Master powers, delayed peg removal, slow motion at final orange, stuck ball exists.

Medium: multiplier thresholds (only boundary numbers seen; range interpretation mine), later style shots and their Duel values, Master durations, Nights additions.

Low or missing, to measure from gameplay by the owner: gravity, launch speed, aim limits, ball and peg radii, restitution values, stuck-ball timeout, bucket speed and width, aim guide length, Fever proximity trigger distance, score counting speed, audio note scale, Ace values, Lucky Spin odds, Peggle Master Duels, level editor numbers, purple pick rule, whether style bonuses scale with multiplier, the Fever-peg x10/x20 contradiction, and whether Flippers/Spooky Ball/Lucky Spin last one shot or several turns.

## Correction from the gameplay measurements (2026-10-04)

The shot score in the original is **(sum of the peg values hit) × (number of pegs hit)**, applied at the end of the turn. This section previously called that a clone formula; it is the original rule. Evidence from footage: Deluxe shows "900 × 18 PEGS" and the HUD rises by exactly 16,200; Nights shows "970 × 12 PEGS = 11,640". Measured physics, timing and presentation are in `peg-measurements.md`.

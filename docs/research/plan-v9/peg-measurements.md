# Peg-shooter physics and timing, measured from gameplay video (Peggle Deluxe / Peggle Nights, PC)

Measured 2026-10-04 from public YouTube gameplay only. No game file, binary or extracted asset was opened. Nothing from the videos is stored in the repo; only the numbers below and the scripts in `measure/` that produced them. Companion to `peg-mechanics.md` (rules, scores, powers).

Units: lengths in native 800×600 game-window pixels ("px"), with x to the right and y down; time in seconds of real (wall-clock) time unless marked "game time". Angles: 0° = straight down, positive = towards the right.

## Summary

| # | Quantity | Value ± uncertainty | Units | N | Confidence |
|---|---|---|---|---|---|
| 0 | Simulation tick rate | 100 (fixed step 10 ms) | Hz | 2 videos, 177 arcs | High |
| 1 | Gravity | 499.6 ± 0.5 (SE); per-arc SD 6 → use 500 (= 0.05 px/tick²) | px/s² | 144 arcs (66 Deluxe + 78 Nights) | High |
| 2 | Launch speed (ball leaving the barrel) | 392 ± 5 (SD), ± 1.3 (SE); 393–398 on the shots least sensitive to the pivot estimate | px/s | 16 shots | Medium-high |
| 2 | Launcher pivot / barrel length (from stills) | pivot ≈ (400, 87), ball leaves ≈ 73 from pivot | px | 3 stills | Low-medium |
| 2 | Aim angles seen in play | −81° … +81° (limit not reached in footage) | deg | 16 shots | Range only |
| 3 | Ball sprite radius | ≈ 6 (diameter 11–12) | px | 5 frames | Medium |
| 3 | Round peg sprite radius | 9.0 ring edge, ≈ 10 with dark outline | px | ~100 pegs, 7 frames | Medium-high |
| 3 | Ball-centre to peg-centre distance at contact (collision radii sum) | 16.7 ± 1.0 | px | 74 bounces | Medium |
| 3 | Ball-centre limits at the side walls | x = 81.5 and 718.5 (± 0.5) | px | 10 wall bounces | High |
| 3 | Brick (rectangular, outer incl. border) | ≈ 30 long × 20 thick | px | 1 level, several bricks | Low-medium |
| 4 | Peg bounce, normal restitution e_n | 0.80 ± 0.02 | ratio | 48 bounces with clean normals | Medium-high |
| 4 | Peg bounce, tangential ratio e_t | 0.95 ± 0.05 (≈ 1: little or no friction) | ratio | 48 | Medium |
| 4 | Peg bounce, overall speed ratio | median 0.82 (IQR 0.80–0.85) | ratio | 74 | High |
| 4 | Wall bounce | both components × 0.75 ± 0.02 (e_n = e_t = 0.75) | ratio | 11 | High |
| 4 | Ball spin | none visible (ball sprite never rotates) | – | all footage | High |
| 5 | Bucket motion | x(t) = 401.5 + 260.0·sin(2π t / 6.00 s), pure sine | px, s | 51 windows of 8 s | High |
| 5 | Bucket peak speed / travel | 272 px/s at centre; centre travels 141.5 … 661.5 | px/s, px | 51 | High |
| 5 | Bucket opening (dark mouth) / rim outer width | 104 ± 1 / ≈ 131 | px | 24 000 frames / 2 stills | High / Medium |
| 5 | Bucket rim top | y ≈ 573 | px | stills | Medium |
| 6 | Stuck-ball early clear | not measured (see gaps) | – | 0 | – |
| 7 | Ball exit → first lit peg clears | 0.57 ± 0.03 | s | 2 turns | Medium |
| 7 | Clear cadence | one peg every 0.050 s (5 ticks), one at a time | s | 2 turns (30 pegs) | High |
| 7 | Clear order | about hit order (first hit first) | – | 1 turn | Low-medium |
| 7 | Last clear → score count-up starts | 1.0–1.4 | s | 2 | Low-medium |
| 8 | Fever slow-motion factor before the last-orange hit | 10.0 ± 0.3 (game runs at 1/10 speed) | × | 1 clean track (velocity and gravity agree) + 7 consistent timings | Medium-high |
| 8 | Slow motion and zoom start → last-orange contact | 1.10 ± 0.18 real s (0.08–0.13 s game time) | s | 7 | Medium |
| 8 | Zoom in | linear 1.0× → 2.0× in 0.48 s (+2.0×/s), starts with slow motion | – | 7 | High |
| 8 | Zoom hold / zoom out | hold 2.0× until the hit, then linear back to 1.0× at −0.30×/s (3.3 s) | – | 7 | High |
| 8 | "EXTREME FEVER" banner | appears 0.05–0.11 s after the hit, on screen 2.95 s | s | 7 | High |
| 8 | Speed after the hit until the ball lands in a Fever bucket | ≈ 0.50 × normal (apparent g 127 ± 8 px/s²; 2 outliers at 0.59, 0.68) | × | 10 arcs, 3–8 s after hit | Medium |
| 8 | Fever buckets visible | ≈ 1.4 s after the hit | s | 1 | Low |
| 8 | Hit → ball lands in Fever bucket | 5.3 and 7.4 (path dependent) | s | 2 | Low |
| 9 | Score counter (HUD) | per 10 ms tick add 1000 while ≥ 10 000 left, 200 while ≥ 1 000, 100 while ≥ 100, 10 below | points/tick | 2 count-ups, exact match | High |
| 9 | Peg score popup | appears at hit just below the peg, does not move, solid 0.40 s, fades 0.08 s, gone at 0.50 s | s | 2 popups | Medium |
| 10 | Peg-hit sound | two-note chime a perfect fifth apart; lower note starts ≈ C♯4–D♯4 (275–315 Hz) and rises +1 semitone per new peg hit; no cap seen to +23 semitones (D6 ≈ 1175 Hz); resets every shot | semitones | 3 shots | Medium-high |
| 11 | Normal aim guide | dotted curve from the barrel that stops at the first peg it would touch; dots ≈ 17 px apart; seen 110–180 px long | px | 6 stills | Medium |
| 11 | Super Guide | same dots to the first contact, plus a thin solid line for the post-bounce path up to the next contact (e.g. 110 px dots + ≈ 420 px line) | px | 2 stills | Medium |
| 11 | Space Blast radius (from the green peg) | lights pegs out to ≈ 91 px; nearest unlit peg at ≈ 125 px → radius 91–125; visible burst ≈ 70 px | px | 1 blast | Low |
| 11 | Multiball split angles | not measured (no Multiball activation found in the sections analysed) | – | 0 | – |

"Solid" (high confidence): tick rate, gravity, bucket motion and size, wall restitution, peg speed ratio, zoom timing, banner timing, clear cadence, score counter rule.
"Rough" (low or medium): launch speed's exact value (393 vs a round 400), collision radii, e_t, slow-motion trigger distance, post-hit Fever speed, Space Blast radius, guide lengths, end-of-turn delays (few samples).

## Important finding that conflicts with peg-mechanics.md

The end-of-turn tally reads "**V × N PEGS**" and the HUD score then rises by exactly V × N, where V is the sum of the hit pegs' values and N the number of pegs hit:
- Deluxe 1-1, first shot: tally "900 × 18 PEGS" (900 = 8 orange + 10 blue at ×1, consistent with the 18 pegs), HUD 0 → 16 200 = 900 × 18.
- Nights 1-1 at 84 s: tally "970 × 12 PEGS = 11 640".
- Contact sheets show the same form throughout ("760 × 13 PEGS 9,880", "320 × 11 PEGS 3,520").

`peg-mechanics.md` (section 3) says the "(sum) × number of pegs hit" formula is a clone formula and not original. The video contradicts that: the original multiplies the shot's peg-value sum by the peg count. The per-peg popups during the shot show only the peg's own value (× Fever multiplier), e.g. "10", "100", "1000" at ×10. Style bonuses appear as a separate "+5,000 STYLE POINTS" line. Treat this as the measured behaviour and update the mechanics brief.

## Sources

Downloaded only the parts used, into the session scratch folder, via `uvx yt-dlp -f <video>+<audio> --download-sections`.

| Key | Game | URL | Format used | Sections |
|---|---|---|---|---|
| DX | Peggle Deluxe | https://www.youtube.com/watch?v=GKfkYJbeJkk ("Longplay: Peggle - Adventure (2007)", Dosgamert) | 1600×1200, 60 fps VP9 (fmt 308) + Opus | 00:00–10:00 (levels 1-1 … 1-5), 13:00–17:00 (stage 2), 38:00–42:00 (stage 4) |
| NT | Peggle Nights | https://www.youtube.com/watch?v=22Jx02tJ69c ("Longplay: Peggle Nights - Adventure Mode - All ACE Scores! (2008) 4K/60", Dosgamert) | 1920×1440, 60 fps VP9 (fmt 308) + Opus | 00:00–10:00 (levels 1-1 … 1-4) |
| DXM | Peggle Deluxe | https://www.youtube.com/watch?v=IG-51VYsrIk ("Peggle Deluxe - Full Walkthrough (Silent, 4K 60FPS)", MacoPlay) | 1920×1080 60 fps (4:3 game pillarboxed: scale 1.8, x offset 240) | 00:00–08:00, downloaded; tracking started but not finished in time, so no numbers come from it |
| NTM | Peggle Nights | https://www.youtube.com/watch?v=sQusxLndZnQ (MacoPlay, same series) | 1920×1080 60 fps | 00:00–08:00, downloaded, not analysed |
| ST | Peggle (console-style widescreen replay) | https://www.youtube.com/watch?v=cwNyVBPddMQ and three other short "stuck ball" clips | 626×360, 30 fps | looked at only; not used for numbers |

DX and NT capture the full 800×600 window with no border, so native px = video px / 2.0 (DX) and / 2.4 (NT). The bucket amplitude comes out at 260.0 px in both, which confirms the two scales agree.

Timestamps used (video time in the downloaded sections, which start at the URL's 00:00 for DX/NT first sections):
- Gravity and launch: every free-flight arc in DX 0–600 s and NT 0–600 s except the Fever windows (DX 106–123, 228–245, 372–389, 505–521 s; NT 205–218, 366–380, 532–546 s). Example arcs: NT 192.33 s (2.03 s long, g = 499.5 ± 0.1), NT 186.03 s (1.73 s, 499.9 ± 0.2), DX 158.30 s (1.18 s, 496.6 ± 1.9).
- Bounces: same arcs, consecutive pairs.
- Bucket: DX 43–227 s, NT 55–527 s (8 s windows).
- End of turn: DX 44.3–55 s (shot 1 of 1-1), NT 83.6–87.6 s.
- Score count-up: DX 53.6–54.6 s, NT 86.2–87.2 s.
- Popups: DX 44.40–44.97 s.
- Fever: DX banners at 107.75, 229.00, 373.70, 505.95 s; NT at 206.50, 367.05, 533.55 s. Slow-motion track: NT frames 12300–12381 (205.0–206.35 s).
- Audio: DX 44–52 s (18-peg shot), 55–75 s, 262–276 s (26-peg shot).
- Guides: DX 44.0 s, 292.0 s, 296.0 s; NT 90, 100, 130, 141 s.
- Space Blast: DX stage-4 section 97.4–98.0 s (frames 5846–5880).
- Bricks: DX 150 s.

## Method per quantity

### 0. Tick rate (`pegtrack.py`, `pegfit.py`)
At 60 fps the ball's per-frame displacement repeats a 2, 2, 1 pattern, and the changed-pixel count per frame does the same (≈4400, 4400, 2200 px). That is a fixed 100 Hz simulation sampled at 60 Hz (5 ticks per 3 frames). `pegfit.py` snaps each frame time to the last tick, τ = ⌊100·t + φ⌋ / 100, and fits the phase φ per arc; across 177 clean arcs φ only takes the values 0 (0.00–0.02 on the 0.02 search grid), 0.34 and 0.68, i.e. 0, 1/3 and 2/3, as a 100 Hz clock seen at 60 fps must. The physics constants below therefore map to 0.01 s steps: g = 0.05 px/tick², launch ≈ 3.9 px/tick, bucket period 600 ticks.

### 1. Gravity (`pegtrack.py` → `pegfit.py` → `peggravity.py`)
`pegtrack.py` finds the ball in every frame: the ball sprite has a gold lower half and pale cyan upper half, so a candidate is a gold HSV blob that is also moving (difference against the frames 4 non-duplicate frames before and after). Duplicate frames from capture stalls (3.5 % DX, 2.9 % NT) are dropped but keep their timestamps. `pegfit.py` seeds arcs from triples of candidates, grows them frame by frame with a local parabola prediction, trims points with residual > 3.5 px, and fits x = x0 + vx·t, y = y0 + vy·t + g·t²/2 with tick-snapped times. `peggravity.py` keeps arcs ≥ 0.4 s long, RMS ≤ 1 px and |horizontal acceleration| ≤ 50 px/s² (rolling contact with a peg shows up as horizontal acceleration), outside the Fever windows, then a 3σ cut.
Results: DX 499.5 ± 0.8 (SE), SD 6.4, N = 66; NT 499.7 ± 0.7, SD 5.8, N = 78. The best long arcs (1.3–2.0 s) give 499.5–500.2 with fit errors of 0.1–0.3. The x-motion has no measurable drag (horizontal acceleration of long arcs ≈ 0 ± 2 px/s²).

### 2. Launch speed and aim (`peglaunch.py`)
For each arc that begins near the launcher, the parabola (g fixed at 500) is extrapolated back to the moment the ball centre was 73 px from the pivot at (400, 87), i.e. where it leaves the barrel (12–73 ms of extrapolation). Speeds: DX 398, 386, 396, 393, 394, 394; NT 391, 379, 394, 391, 383, 394, 395, 397, 391, 393 px/s. Mean 392, SD 5. Near-vertical and near-horizontal shots, the least sensitive to the pivot estimate, give 393–398; the three lowest values are diagonal shots, where a pivot error matters most. A round 400 px/s (4 px/tick) inside the engine with a slightly different spawn point cannot be excluded. Pivot and barrel length were read from three stills where the barrel points down, down-right and right; the first few frames after a shot are hidden by a sparkle burst, so the spawn point itself is not seen.
Aim: the steepest shots seen were 81° left and 81° right of straight down. No footage shows the player pushing the launcher to its stop, so the true limit is unknown (it is at least ±81°).

### 3. Sizes (`pegsize.py`, `pegbounce.py`, stills)
- Pegs: orange pegs segmented by hue in 7 frames from both games (about 100 pegs): the coloured disc's enclosing radius is 9.0–9.3; with the bright rim and its dark outline the sprite is ≈ 10 px radius (radial contrast profile of a blue peg: rim peaks at r = 10, falls to half by 11.5 including the drop shadow, which is offset down-left). Hough circles on 74 bounce frames: median radius 9.0.
- Ball: in flight the sprite is 11–12 px across (5 frames, measured against a background frame; the drop shadow, offset ≈ 2 px down-left, was excluded). Radius ≈ 6. The balls drawn in the left "Ball-o-tron" tube are larger (≈ 17 px) and are a different sprite.
- Collision distance: at each peg bounce the ball centre (from the two arcs' intersection) is a fixed distance from the Hough-detected peg centre. The tracker follows the gold half of the ball, whose centroid sits below the true centre, so `pegbounce.py` fits that offset together with the distance: D = 16.5 (DX, N = 39) and 16.9 (NT, N = 35), scatter 1.1–1.5. This matches ball 6 + peg ≈ 10–11. Treat 16.7 ± 1 as the sum; splitting it between ball and peg is not possible from bounces alone.
- Walls: the ball's tracked centre turns at x = 81.7–82.3 on the left (5 bounces) and 718.4–719.5 on the right (5). The midpoint is 400.6, so the gold-centroid x offset is ≈ +0.6 and the ball centre is confined to x ∈ [81.5, 718.5]. The wall surfaces are therefore ≈ 6–7 px further out (≈ 75 and 725), the playfield is ≈ 650 px wide, and the side walls are flat and vertical from y ≈ 100 to 560 (bounces seen at y = 154–539).
- Bricks (DX 1-2): measured along the curve on a 2× still, outer size including the coloured border ≈ 29–30 px long and ≈ 20 px thick. One level only.

### 4. Restitution (`pegbounce.py`)
Pairs of clean arcs separated by ≤ 4 frames are a bounce. The collision time is where the two parabolas meet; v_in and v_out are the arcs' velocities there. For pegs the normal is the line from the Hough peg centre to the ball centre; bounces whose centre distance disagrees with D by more than 1.5 px are left out of the component fit (their normal is unreliable). The model |v_out|² = e_n²(v_in·n)² + e_t²(v_in·t)² is fitted robustly:
- DX: e_n = 0.795 ± 0.006, e_t = 0.977 ± 0.014 (N = 28). Speed ratio by incidence: grazing (|cos| < 0.5) 0.97, oblique 0.88, head-on (|cos| > 0.8) 0.81.
- NT: e_n = 0.804 ± 0.006, e_t = 0.914 ± 0.013 (N = 20).
- Overall peg speed ratio |v_out|/|v_in|: median 0.817 (DX, N = 39) and 0.828 (NT, N = 35).
- Walls: e_n = 0.751 / 0.755 and e_t = 0.748 / 0.752 (DX N = 6, NT N = 5); speed ratios 0.71–0.79, median 0.75. A wall bounce mirrors the normal component and scales the whole velocity by 0.75.
- Spin: the gold half of the ball sprite stays at the bottom in every frame, so the game shows no rotation. e_t near 1 for pegs is consistent with no friction torque.
- Not measured: bricks, the bucket rim and moving pegs (too few clean pairs).

### 5. Bucket (`pegbucket.py`)
Per frame, the dark mouth of the bucket in rows y = 572–583 gives its left and right edges. Over 8 s windows a sinusoid fits with RMS 0.6–1.3 px, while a constant-speed (triangle) fit leaves 23 px RMS, so the motion is a pure sine (eased at the ends, fastest in the middle). DX, 19 windows: centre 401.1 ± 0.2, amplitude 260.0 ± 0.2, period 6.006 ± 0.005 s. NT, 32 windows: centre 401.8 ± 0.3, amplitude 260.0 ± 0.3, period 6.01 ± 0.01 s. The phase restarts each level. Mouth width 105 (DX) and 103–104 (NT), median over ≈ 24 000 frames; rim outer width ≈ 131 (2 stills); rim top y ≈ 573. The 6.006 s period (vs a likely 6.000 s = 600 ticks) also bounds any recording speed error to ≈ 0.1 %. The catch rule (ball centre inside the mouth or anywhere inside the rim) was not measured.

### 6. Stuck ball
Not measured. No turn in the 34 minutes analysed shows a ball resting on pegs long enough for early removal. The short "stuck ball" clips found on YouTube are 360p, 30 fps, and most are in-game replays (a "REPLAY" label, with a fast-forward option) of the widescreen console-style edition, so their timing cannot be trusted.

### 7. End of turn (`pegclear.py`, `pegtile.py`)
- DX 1-1, shot 1: the ball enters the bucket at 51.25 s (tracked centre crosses y = 564), the first lit peg pops at 51.82 s, and pegs then pop every 3 frames. The tally "900 × N PEGS" counts 1 → 18 in 50 ms steps (51.82 → 52.67 s).
- NT 1-1 at 83.7 s: the ball leaves the bottom at ≈ 83.78 s, the tally header "970 × PEGS" fades in at 83.90 s, the first peg pops at 84.35 s, then exactly one peg every 3 frames (50 ms) to the 12th at 84.90 s. The next ball appears in the launcher at 85.70 s.
- Order: in DX the first pegs to pop were the first ones hit (top rows first). The automatic order detector cannot resolve better than ±2 positions, so "hit order" is likely but not proven.
- Each pop is a short expanding ring with a sound; the pegs do not all go at once.
- The automated `pegclear.py` also catches popups and background animation; its per-turn output was only used where a contact sheet confirmed it.

### 8. Fever (`pegfevertext.py`, `pegzoom.py`, `pegzoomfit.py`, `pegslowmo.py`)
- Finding the events: `pegfevertext.py` finds the large "EXTREME FEVER" banner. Seven events: DX 107.75, 229.00, 373.70, 505.95 s; NT 206.50, 367.05, 533.55 s. The banner stays 2.90–2.95 s in all seven.
- Zoom: ORB features matched to a pre-zoom frame give a similarity transform per frame. In all seven events: zoom rises linearly from 1.0 to 2.00 in 0.47–0.48 s (+1.99 to +2.01 per s), holds at 2.00, then falls linearly at −0.300 to −0.304 per s back to 1.0 (3.27–3.32 s). The zoom-out starts at the last-orange hit; the banner follows 0.05–0.11 s later. The zoom centre is near the ball and its target (e.g. (405, 340) in DX 1-1; (264, 330) in NT 1-1) and drifts back to (400, 300) during the zoom-out.
- Slow-motion start: in NT 1-1 the ball's speed drops in one step between frames 12325 and 12327, the same frame the zoom starts (205.433 s). There is no ease-in.
- Slow-motion factor: through the zoom the ball was tracked in board coordinates (frames mapped back with the zoom transform). Just before: v_y = 252 px/s. During: a straight fall at v_y = 25.1 ± 0.2 px/s with a_y = 4.7 ± 0.5 px/s². Velocity ratio 10.0; gravity ratio √(500/4.7) = 10.3 ± 0.6. The game runs at 1/10 speed.
- Trigger: the slow-motion/zoom start precedes the last-orange contact by 1.21, 0.81, 1.28, 1.11 (DX) and 1.05, 1.30, 0.94 s (NT), mean 1.10 ± 0.18 s of real time, i.e. 0.08–0.13 s of game time. In NT 1-1 the ball was ≈ 24 px (≈ 40 px centre-to-centre) from the orange when it started. A fixed distance does not explain the spread; a look-ahead of about 10 ticks to the predicted contact fits all seven. The rule itself is not determined.
- After the hit: once the zoom has ended, 8 of 10 clean arcs (3–8 s after the hit, 6 of the 7 events) show apparent gravity 120–142 px/s², i.e. the game runs at ≈ 0.50 speed until the ball lands in a Fever bucket. The other two gave 170 (NT 1-1, 4.0 s after the hit) and 232 px/s² (DX 1-1, 5.6 s), speed 0.59 and 0.68; they may be rolling contact or a later speed-up, so treat 0.5 as the plateau, not a proven constant. During the 3.3 s zoom-out the fireworks break the image registration, so how the speed goes from 0.1 to 0.5 was not measured (one arc at 3.3 s gave 0.42).
- Sequence, NT 1-1 (DX 1-1 in brackets): hit 206.48 (107.64); banner 206.50–209.45 (107.75–110.65); Fever buckets (five, 10k/50k/100k/50k/10k) visible from ≈ 207.9; zoom back to 1.0 at 209.75 (110.97); ball lands in a Fever bucket ≈ 211.75 (≈ 115.0); green fountain ≈ 1.3 s; rainbow drawn ≈ 213.25–213.75 (≈ 117.0); "FEVER SCORE" count-up 214.25 → 215.75 (117.5 → 120.5); results/"ACED" at 217.25 (level-complete panel 122.0).

### 9. Scoring presentation (`pegtile.py`, read by eye)
- HUD counter, DX: 0 → 16 200 read every 2 frames: +2000 and +4000 (1000/tick), then 600–800 per 2 frames (200/tick), then +300 (100/tick), then +30 steps (10/tick). NT: 9 880 → 46 520 read every 3 frames: +5000 per 5 ticks (1000/tick) until 10 640 remained, then +1800 (1 tick at 1000 + 4 at 200), +1000 per 5 ticks (200/tick), +500 (100/tick), +320 (100,100,100,10,10), +20. Both sequences match the rule exactly: per tick add 1000 if ≥ 10 000 remains, else 200 if ≥ 1000, else 100 if ≥ 100, else 10. A 16 200 shot takes 0.7 s; a 36 640 change takes 0.87 s. The Fever count-up (DX) rose 50 000 per 0.5 s, i.e. the same 1000/tick ceiling.
- Peg popups: two "100" popups (DX 44.45 s) appear on the hit frame, centred just below the peg, do not move, stay solid for 25 frames (0.42 s), fade over 5 frames and are gone after 30 frames (0.50 s). A peg-hit flash (bright ring expanding from the peg) lasts ≈ 0.4 s.

### 10. Audio (`pegpitch.py`)
Peaks standing ≥ 18 dB above the clip's median spectrum (which removes the steady music) were listed every 30 ms, and log-frequency spectrograms with semitone grids were read. Each peg hit is a chime of two notes a perfect fifth apart (ratio 1.49–1.50 every time, e.g. 583/869, 658/986, 1171/1746 Hz). The lower note climbs one equal-tempered semitone per new peg hit, tuned to A = 440: D4 294, D♯4 311, F4 349, F♯4 367, G4 392, G♯4 413, A4 441, A♯4 470, B4 493, C5 524, C♯5 554, D5 587, D♯5 617, E5 658, F5 701, F♯5 740, G5 777 … through C6 1043, C♯6 1104, D6 1171 Hz in the 26-peg shot (+23 semitones, no cap reached). The first hit of a shot sits around C♯4–D♯4 (lower note 272–318 Hz; the three shots measured started at ≈ 277, ≈ 290 and ≈ 315 Hz), so the base may vary or depend on something not identified. Each shot restarts low. The level music is in a fixed key underneath and was ignored. The DX audio is 48 kHz Opus from YouTube; the pitch resolution of the 80 ms windows is ≈ ±1 % (±0.2 semitone).

### 11. Powers
- Guides: read off native-size stills. Normal guide: cyan dots ≈ 17 px apart along the predicted path from the barrel, ending where the ball would first touch a peg; observed lengths 110–180 px, every one ending at a peg. Super Guide (DX 1-3 at 292 s, "2" turns left): the same dots to the first contact, plus a thin solid curve of the path after that bounce up to the next contact (≈ 420 px in that still). Whether the normal guide has a maximum length when nothing is in the way was not seen.
- Space Blast (DX stage 4, 97.5 s; the level's pegs orbit, so positions were taken from frame 5846, mid-blast): the burst is a dark spiky disc of radius ≈ 70 px; pegs and bricks lit by it lie 61–91 px from the burst's centre (taken as the green peg, which sits at the burst centre); the nearest unlit peg is at ≈ 125 px. So the radius is between 91 and 125 px; one sample.
- Multiball: not measured. The stage-2 section (Jimmy's levels) was scanned for frames with two moving balls; every hit was a Fever firework or a bucket fountain, not a second ball. Needs a targeted clip.
- Ultra Extreme Fever (all pegs cleared, DX stage 2 at 48.7 s of that section): the banner reads "ULTRA EXTREME FEVER", spelled out letter by letter over ≈ 1.2 s, and all five Fever buckets show 100,000.
- Fever score count-up rises 20 000 per 0.2 s (1000 per tick), the same ceiling as the HUD counter.

## Known biases

- Compression: VP9 at 1–2.5 Mbit/s blurs edges by about 1 video px (0.4–0.5 native px). This affects sprite sizes (± 0.5 px) more than tracks (centroids average it out).
- Tracking point: the tracker follows the gold lower half of the ball, which sits ≈ 3 px below and ≈ 0.6 px right of the true centre. A fixed offset cancels in velocities, gravity and restitution, but not in absolute positions (wall limits and contact distance were corrected for it).
- Frame timing: 60 fps samples of a 100 Hz game put ±5 ms of jitter on every position; `pegfit.py` removes it by fitting the tick phase. Duplicate frames (capture stalls, ≈ 3 %) were removed without shifting time. The bucket period (6.006 vs 6.000 s) suggests the recordings run at real speed within ≈ 0.1 %.
- Recording speed-ups: none found. Gravity, bucket period and banner duration agree between two different capture setups (DX 1600×1200, NT 1920×1440).
- Footage choice: only the first stages of each game (Bjorn's levels, plus stage 2 and 4 sections of Deluxe). Constants are unlikely to vary by level, but moving-peg levels, other Masters' powers and later Nights mechanics were not sampled.
- Fever slow-motion factor rests on one clean track plus seven consistent hold durations.

## What the owner should double-check by playing

1. Launch speed: 392–398 px/s measured; check whether a round 400 px/s (4 px/tick) feels identical, and where exactly the ball spawns (barrel tip radius).
2. Aim limits: swing the launcher fully left and right and read the stop angle (footage only shows ±81°).
3. Stuck-ball rule: trap a ball between pegs (e.g. a peg pocket) and time when the first peg vanishes and how many go.
4. Bucket catch zone: does a ball whose centre hits the rim bounce off, roll in, or count as caught? Mouth is 104 px wide, rim 131 px.
5. Fever trigger: is slow motion triggered by distance or by predicted time to contact? Try slow, grazing approaches to the last orange.
6. The speed ramp from 1/10 to 1/2 speed after the last-orange hit (hidden by fireworks in video).
7. Peg tangential friction: e_t came out 0.91 and 0.98 in the two videos; check grazing bounces.
8. The score formula (sum × count) against the mechanics brief.
9. Peg-hit chime base note: whether it is fixed (≈ C♯4) or varies per shot.
10. Space Blast radius and Multiball split angle on a few more activations.

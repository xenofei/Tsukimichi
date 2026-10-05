# Moonfall: powers and style shots (plan v9 G4, G5)

Stage 2 of Moonfall. The rules live in `Tsukimichi.Core/Moonfall/MoonfallRules.Powers.cs`, `MoonfallPowers.cs`, `MoonfallGame.Powers.cs` and `MoonfallGame.Style.cs`. Each value there cites its line in `docs/research/plan-v9/peg-mechanics.md` as `[R §n l.N]`, or is marked `[J]` (set by judgement) with its reasoning.

## Names (decision 2)

General terms keep their plain names. Where the original's name is distinctive, Moonfall uses its own. The engine names everything by stable ids, and the names the player sees are in `Strings.resx`. The power names are still proposals (decision 8).

### Powers

| Id (`MoonfallPower`) | Original's power | Moonfall's name | Character |
|---|---|---|---|
| `SuperGuide` | Super Guide | Super Guide | Pipiru Mimiru |
| `Multiball` | Multiball | Multiball | Kaede Tsukiyo |
| `Wings` | Pyramid | Brass Wings | Marcia nan Arcus |
| `Burst` | Space Blast | Lunar Burst | Haldbrand Tidewatch |
| `Flippers` | Flippers | Flippers | Gajavati |
| `Gate` | Spooky Ball | Moon Gate | Ysolde Nocturine |
| `Bloom` | Flower Power | Moonbloom | Sister Ottilie |
| `Draw` | Lucky Spin | Moon-Viewing Draw | Gyobo |
| `Fireball` | Fireball | Fireball | Aldous Varrow |
| `Path` | Zen Ball | Sage's Path | Ione Selenis |
| `Bolt` | Electrobolt | Storm Post | Kupsa Brightpom |

### Style shots

| Id (`MoonfallStyleShot`) | Original's name | Moonfall's name |
|---|---|---|
| `OnePegCatch` | Free Ball Skillz | One-Peg Catch |
| `LongShot` | Long Shot | Long Shot |
| `SuperLongShot` | Super Long Shot | Super Long Shot |
| `DoubleLongShot` | Double Long Shot | Double Long Shot |
| `OffTheWall` | Off the Wall | Off the Wall |
| `RimShot` | Kick the Bucket | Rim Shot |
| `LuckyBounce` | Lucky Bounce | Lucky Bounce |
| `OrangeSweep` | Orange Attack | Orange Sweep |
| `LongSlide` | Extreme Slide | Long Slide |
| `ClearNight` | Cool Clear | Clear Night |
| `LiveWire` | Shock It To Me | Live Wire |

"Bank Shot" is not in either game (research §3), so Moonfall has none.

## Powers

Any green peg that lights sets off the level's power, including one lit by another power [R §5 l.103]. A second green adds its shots to the shots left. Powers that act at the hit act again.

| Power | Timing | Lasts | What it does | Judgement calls |
|---|---|---|---|---|
| Super Guide | from the next shot | 3 shots [R l.41, l.107] | The guide runs on through its first bounce to the next contact. | The line stops at 300 px past the bounce. |
| Multiball | at the hit | this shot [R l.108] | A twin springs from the top of the green, with the hitting ball's velocity mirrored. The turn ends when the last ball leaves, and each catch is a free ball. | At most 3 balls. The twin always gets at least 80 px/s sideways. The first ball into a Full Moon cup ends the turn. |
| Brass Wings | at the hit | 5 turns, counting this one [R l.109] | The bucket's mouth widens from 104 to 208, and the rims move out. | The width is a judgement, and so is counting the green's own turn. |
| Lunar Burst | at the hit | this shot [R l.110] | Lights, nearest first, every peg whose surface is within 80 px of the green. | 80 px reads "4 pegs wide" as four peg diameters. |
| Flippers | at the hit | 3 turns, counting this one [R l.111, conflict] | Two flippers sit at the foot's corners. They rise while the button is held, and their swing speed goes into the bounce. | The geometry, the swing and restitution 0.5 are judgements. The wiki's 3 turns were chosen over the patent's single shot. |
| Moon Gate | at the hit | this shot [R l.112] | A ball that falls out comes back in at the top, at the same x and speed, once for each green. | Its velocity is kept, so the re-entry reads as a wrap. |
| Moonbloom | at the hit | this shot [R l.113] | Lights the nearest fifth (rounded up) of the oranges left. | Ties go to the earlier peg. |
| Moon-Viewing Draw | at the hit | the draw [R l.114] | The drum holds 12 balls: 4 free ball, 4 triple score and 4 another power (one of the other ten, evenly). | The odds are a judgement. The original's hat and its random power merge into one outcome. A triple score lasts this shot and the next ("2 turns"). |
| Fireball | from the next shot | 1 shot [R l.115] | The ball meets no peg. Each peg it touches lights and burns away at once. | (none) |
| Sage's Path | from the next shot | 1 shot [R l.116] | Tries 17 angles within ±4° of the aim and flies each one. The flight's worth is its peg score + 2,000 for each orange + 10,000 for a catch. The best angle wins, and a tie goes to the angle nearest the aim. The search is capped at 24,000 physics sub-steps in all. | Every number, and the choice to nudge at the barrel. |
| Storm Post | from the next shot | 1 shot | The first peg the ball lights sends a bolt to the bucket's centre. The bolt lights, in order along its line, every peg whose centre is within 26 px of it. | The timing, the length and the 26 px reach. The research gives no duration [R l.117]. |

## Style shots

Each one is paid at most once a shot. Its bonus is added after the peg score (values × pegs), and the multiplier and a triple score don't apply to it [R l.87]. The bonus counts towards the free balls [R l.70].

| Style shot | Bonus | Trigger | Judgement calls |
|---|---|---|---|
| One-Peg Catch | 5,000 [R l.75] | The shot lit exactly one peg and the ball was caught. | (none) |
| Long Shot | 25,000 [R l.76] | A ball lights a non-blue peg, then another at least 800/3 px away. | The distance is measured straight between the pegs. At most one blue peg may come between them. |
| Super Long Shot | 50,000 [R l.77] | As Long Shot, at 2 × 800/3 px. It is paid instead of a Long Shot. | (none) |
| Double Long Shot | 25,000 [R l.78] | A long leg straight after another long leg. | (none) |
| Off the Wall | 25,000 [R l.79] | After a wall bounce, the first peg the ball lights is non-blue and at least 160 px from the bounce. | The peg must be the first one after the wall. |
| Rim Shot | 25,000 [R l.80] | After a rim bounce, the first peg the ball lights is the last orange. | (none) |
| Lucky Bounce | 25,000 [R l.81] | Caught 0.5 s or more after a rim bounce, or after rising 150 px above it. | 0.5 s, timed from the first rim bounce since the ball last lit a peg. |
| Orange Sweep | 50,000 [R l.82] | The shot lights at least a third of the oranges left when it was fired, and at least 3. | The third and the minimum. |
| Long Slide | 50,000 [R l.83] | A ball lights 12 pegs in one slide. Every touch meets the peg at under 80 px/s, less than 0.2 s after the last. | Both thresholds, traced on a 22° row of bricks. |
| Clear Night | 50,000 [R l.84] | As the Full Moon ball lands, every peg is lit or gone, and the shot lit at least 2 pegs. | It is checked at the landing. |
| Live Wire | 25,000 [R l.85] | A Storm Post bolt lights 12 pegs or more. | Only the bolt's own pegs count. |

## Characters by stage

Adventure gives each stage of five levels one character [R §6 l.126]:
- **The base campaign:** ten stages in the order above, without Storm Post, then a stage where the player picks (55 levels).
- **The expansion:** the same ten, then Storm Post, then a stage where the player picks (60 levels). The order is a judgement, because the research doesn't give the second game's order.
- **Quick Play:** the player may pick any of the eleven before the first shot. Unlocking characters stage by stage comes with the modes (G7).

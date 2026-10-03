# Round 5 brief: polish of Menphina's Medallion (owner, 2026-10-02)

The owner says round 4 "looks great". Round 5 is a polish pass, so keep everything else as it is.

## The owner's notes

| # | Note | What round 5 does |
|---|---|---|
| 1 | "The job icons aren't fully centered in 'Ready, other job'." | Centre the job icon optically in its badge disc, horizontally and vertically, for every job. Measure from the icon's visible pixels (alpha bounding box and visual mass), not its canvas. Check Paladin, Bard and White Mage. |
| 2 | "The main plugin icon needs to be more centered, with some more details (FFXIV related)." | Recompose so the moon and its road sit on the centre axis. Add FFXIV-specific detail, researched from official art (allowed): for example a far shore with a Hingashi or Kugane-style skyline, a pagoda or castle silhouette, a torii, an airship or a lit Far Eastern lantern line. No aetheryte (the owner rejected it before). Keep one focal moon, the reflection physics (the road directly under the lit moon, never brighter, broken by ripples), and the 32 px and 64 px legibility. If the centred road enters Dalamud's Installed check corner, keep its brightest part out of the bottom-right quadrant. |
| 3 | "'Blocked' needs more obscurity. Maybe the moon a bit darker. Let the supervisor determine it, with critic help." | Draw two or three Blocked variants (for example denser cloud cover over a dimmer moon, or a near-new moon behind heavy cloud). The realism supervisor and a design critic choose. Blocked must still differ from Not checked and Locked out at 16 px. |
| 4 | "Give Ready a lock symbol as well that shows it's unlocked." | Ready gets a badge disc in the same position, size and frame as the job badge, holding an **open padlock**. |
| 5 | "Blocked should also have a lock symbol showing it's locked (the smaller circle, like the job icons)." | Blocked gets the same badge disc with a **closed padlock**. |
| 6 | "Give 'In journal' a small journal picture in a small circle, like the class icon." | In journal gets the same badge disc with a **journal or book icon**. An FFXIV-style journal icon is welcome (official icons may be used). |
| 7 | Gold versus green check: "don't understand the difference." | They meant the same thing, so keep the **gold** check only and drop `completed-green.svg`. |

## The badge system

Ready, Ready on another job, In journal and Blocked now carry the same small badge disc in the lower right of the medal:
- one shared spec for position, size, gilt rim and backing disc;
- the badge content is centred optically.

Badge glyphs must read at 32 px and up. Below 32 px, the row-size fallback from round 4 applies: the medal shows without the badge, and the badge content is drawn at text height beside it.

Lock colours: the open lock is warm and inviting, the closed lock is cool and muted. Neither may become the loudest thing on its medal, and Ready must stay the loudest state.

## Process

The designer reworks the art. The realism supervisor and a design critic then review the rendered images: the supervisor for realism, the critic for meaning, consistency and taste. The supervisor's verdict is required before the art reaches the owner.

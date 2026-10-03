# Round 4 brief: Menphina's Medallion, from the plan site's feedback (2026-10-02)

Round 3 was approved by the realism supervisor, but the owner says the concept "still needs more work" (decisions 1 and 2). Round 4 starts from `round3/medallion-r3/` and keeps what was asked for before: concept D, one shared gilt rim on all eight medals, no aetheryte crystal, and FFXIV job-icon medals.

## The owner's notes (highest priority)

| # | Note | What round 4 does |
|---|---|---|
| 0 | "The moon needs more details. The water reflections need some more detail." | Give the moon real craft at hero size, for example limb darkening, a soft terminator, and subtle maria-like value shifts done as smooth tone, never craters, blotches or cheese. Give the water more detail: varied ripple lengths, a faint sky reflection, glints that catch light at their crests, and a soft moon column. Applies to the icon, Ready and the large states. |
| 1 | "The Ready border doesn't have a full complete trim like the others." | Ready gets the same complete rim as the other seven. It must stay the loudest state without breaking the rim: brighter moon, the moon road inside the medal, and a warm inner glow or brighter enamel. |
| 2 | "The crystal in 'Ready, other job' seems weird. Give it a more appropriate icon to reference a job." | Use a real job reference. In the plugin this badge would be the actual job icon of the job the quest is ready on, read from the game at runtime. For the concept, use real FFXIV job icons from the web (allowed now). Show a badge in the lower right of the medal with one example (for example Paladin), and also render 2–3 other jobs to prove it works for any job. Keep it readable at 16 px; if a full job icon is mush at 16 px, define the row-size fallback. |
| 3 | "The Blocked icon doesn't make sense. Maybe reference clouds blocking the moon? Obscurity?" | Blocked is a moon with clouds drifting across it: the moon is there but you can't reach it yet. |
| 4 | "Done this cycle doesn't visually make sense. If it involves repeatables, then something that represents repeat." | Done this cycle uses a repeat symbol, such as a circular arrow arcing around or through the moon, so it reads as "done, comes back". Don't build it as a closed gold orbit ring, and test that it doesn't read as a refresh button alone; it should read as a moon that cycles. |
| 5 | "Repeated should be a full, bright moon with nice details." | Read as **Completed** (the full moon). Completed becomes a full, bright, detailed moon with the check kept, but it must still recede in a long list: its salience stays below Ready's, through value and saturation, not by losing detail. |
| 6 | "I don't like the large broken band; it's too thick." | Locked out keeps Dalamud's red moon but loses the thick black crack. Use fine fracture lines or a shattered moon with slightly displaced shards. Fissures stay thin and the meaning is "gone for good". |
| 7 | "'Not checked' doesn't make sense. Obscurity, but not like Blocked. Or question marks?" | Not checked is a question mark in moon-silver, or a faint veiled moon with a question mark. It means "unknown" and must be clearly different from Blocked's clouds. It must not look like a generic help button: integrate the question mark with the moon or the medal. |

## Ideas from a player tester (use where they fit the owner's notes)

- **In journal** is "whatever Ready ends up being, but with the bookmark." Adopt this: In journal = the Ready moon, quieter, plus the ribbon bookmark. That makes the family coherent.
- **Combining Ready with a class symbol:** the current job bright and others dimmed. Partly adopted through the job badge in owner note 2.
- **Completed:** a full moon with a check, "maybe make the check mark green?" Try a green check variant against the gilt check and show both. The owner decides.
- **Locked out:** "a completely shattered moon, it's gone, we ain't getting it back." This matches owner note 6.
- **Blocked:** a new moon (completely black). The owner preferred clouds, so clouds win. A dark new moon behind clouds may combine both ideas.
- **Done this cycle:** an eclipsed moon with a flare. The owner preferred a repeat symbol, so repeat wins.
- **Not checked:** "blank, no moon." This is compatible with a question mark in an empty medal.
- **Ready as a moon lander or rocket.** Not adopted, because it breaks the calm moon family. Recorded for the owner.

## Plan site decisions

The owner agreed to all 15 decisions. Decisions 1 and 2 (moon and icon) are "still needs more work", which is this round.

# Moonfall's eleven companions: real FFXIV characters

Status: a proposal for the owner, 5 October 2026. It replaces plan v9's original eleven (decision 1) and the moonstone cameos (decision 17), at the owner's choice: "real FFXIV characters, shown with the game's own portrait art from your install."

Each companion is shown with their **Triple Triad card** from the player's install (`ui/icon/087000/<icon>_hr1.tex`, 208 × 256). The card is their A Realm Reborn look. The plugin already indexes these cards as giver portraits (`GiverPortraitSources.cs`, `CardArtBase = 87000`) and frames them by its own crop rules:
- the face box `[28, 23, 135]` for round medallions;
- `ArtBounds` for the hero art.

See `screens/characters-1280.png` and `characters/lineup.png`.

## The mapping

| Stage | Power (id) | Character | Card | Why this character | Spoiler note | Alternate |
|---|---|---|---|---|---|---|
| 1 | Super Guide (`SuperGuide`) | **Minfilia** | 087056 | The Echo shows her what is coming; Super Guide shows the shot past its first bounce | Met in the A Realm Reborn main scenario, on joining the Scions. The card shows her as she is then; nothing of her later story | Thancred (087046) |
| 2 | Multiball (`Multiball`) | **Alphinaud & Alisaie** | 087059 | Two of them, two balls: the twins split the shot at the green peg | Alphinaud is met in the A Realm Reborn main scenario, Alisaie only in its later patches. Until the shield clears Alisaie, the card shows face down (see question 2) | Biggs & Wedge (087033) |
| 3 | Brass Wings (`Wings`) | **Cid Garlond** | 087058 | Eorzea's great engineer: the airship wings come from his workshop | A Realm Reborn main scenario (the Garlond Ironworks) | Biggs & Wedge (087033) |
| 4 | Lunar Burst (`Burst`) | **Raubahn** | 087067 | The Flame General's greatsword: one blow that reaches everything near | A Realm Reborn main scenario (Ul'dah) | Papalymo & Yda (087048) |
| 5 | Flippers (`Flippers`) | **Merlwyb** | 087065 | The admiral keeps the ship afloat: two oars at the waterline | A Realm Reborn main scenario (Limsa Lominsa) | Hildibrand & Nashu (087062) |
| 6 | Moon Gate (`Gate`) | **Urianger** | 087050 | He reads portents in the stars: what falls returns from above | A Realm Reborn main scenario (the Scions) | Y'shtola (087049) |
| 7 | Moonbloom (`Bloom`) | **Kan-E-Senna** | 087066 | Gridania's seedseer asks the wood to bloom | A Realm Reborn main scenario (Gridania) | Y'shtola (087049) |
| 8 | Moon-Viewing Draw (`Draw`) | **Tataru** | 087019 | The Scions' purse-keeper runs the draw | A Realm Reborn main scenario (the Waking Sands) | Momodi Modi (087028) |
| 9 | Fireball (`Fireball`) | **Y'shtola** | 087049 | A Sharlayan mage whose spells burn through whatever stands in the way | A Realm Reborn main scenario (the Waking Sands) | Papalymo & Yda (087048) |
| 10 | Sage's Path (`Path`) | **Louisoix** | 087060 | The Archon, the twins' grandfather: the sage who finds the best path | A Realm Reborn prologue (the Calamity). The card is his A Realm Reborn look; nothing of his later story | Urianger (087050) |
| The Far Shore | Storm Post (`Bolt`) | **Moogle courier** | 087020 | Moogles carry Eorzea's post: the bolt is delivered from the first peg to the bucket | Moogles are met in the first hours of any start (the inns' mail moogles): no spoiler | Good King Moggle Mog XII (087043, Heavensward: a spoiler for early players) |

Each companion also has an accent colour, at least 25° apart from the others in hue and off the chrome's gilt, and a stage themed to their home (`spec-rich2.md`, section 3). The moogle's portrait uses its own crop box `[14, 14, 180]`, so the ring keeps its pom-pom and wings.

Each companion's line, role and power text are in `src/r2cast.py`. They are short, our own writing, and in the character's spirit; none is a quote from the game.

## The spoiler shield

- **Which characters.** Every character is one an A Realm Reborn player meets. The cards are their A Realm Reborn looks. The Trust busts (`072621`–`072664`) were reviewed and rejected: they show Shadowbringers-era and later outfits.
- **Not yet met.** Moonfall asks the plugin's shield whether the story has introduced the character (`SpoilerNames`, the NPC rule, the player's own shield setting). If not, the companion is the **Triple Triad card back** (`ui/uld/TripleTriadBattle_hr1.tex`). It is shown on the characters grid, as a map stop and in the HUD. It is labelled "Not yet met", with the power still named, and no name, art or role is shown.
- **Met, but not reached in Moonfall.** The card is dimmed, labelled "meet at stage N".
- **The one real case is the twins.** Alisaie appears only in A Realm Reborn's patch story, so a new player at stage 2 may not have met her. The mocks show this case.

## Peggle's Masters: the check

Peggle's eleven Masters are: a unicorn, a beaver, a cat, an alien, a lobster, a jack-o'-lantern, a sunflower, a rabbit, a dragon, an owl and an electric squid. None of the eleven companions is any of these. The nearest cases, for the owner:
- **Y'shtola** is a Miqo'te, a people with feline ears. She is a person, not a cat, and carries a different power (Fireball) from Peggle's cat (Pyramid).
- **Kan-E-Senna** carries the flower power and wears a great white flower. The flowers belong to the power itself (Moonbloom is our version of Flower Power), not to a sunflower character, but this is the nearest echo in the cast.
- No Loporrits (moon rabbits) and no dragons are in the cast, as plan v9's decision 12 asked.

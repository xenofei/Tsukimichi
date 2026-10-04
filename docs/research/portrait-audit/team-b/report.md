# Portrait audit, Team B (lore and appearance)

4 October 2026. Coverage: 63 characters in 96 character-by-arc rows, every arc from 2.0 to 7.x. Game-art status: **47 OK, 17 off-centre, 13 wrong look or era, 19 none.** The optional pack photo is also wrong (weapon, ears or armour instead of a face) for 7 of those rows and off-centre for 7 more. Per-row data with sources: `findings.json`. Contact sheets (Square Enix art, not committed): `sheets/`.

## Method

1. **Lore.** FF Wiki and consolegameswiki appearance sections, dated by patch or by the quest where a look starts. Each row in `findings.json` cites its URLs. The FFXIV Fandom wiki returned HTTP 402, and the Lodestone has no NPC appearance pages.
2. **Game data.** The 2026.09.15 install, read with `GiverPortraitSources.Read` and `PortraitIndex.Build` from HEAD 51fdccda and the shipped curated file.
   - For every quest of 766 ENpcResident ids: the plugin's own pick, and the pack photo that would replace it.
   - Each character's giver rows grouped by ENpcBase body model. A new body model means a new outfit, so these groups are the game's own look periods.
3. **Framing.** Every worn portrait rendered with the plugin's crop table against the 1.15 A2.1 rule: eyes at 42–46 %, chin at 78–84 %, centre within ±10 %. Proposed landmarks come from trial crops and are good to about ±0.01; confirm them on `DataGen --portrait-sheet --icons`.

## Complaint 1: faces not centred (Varshahn, Estinien)

Confirmed. Neither icon has an iconCrop, so both use the Trust-bust default box (6, 86, 140). Every Trust bust from 72644 up sits its head higher than that box expects.

- **Varshahn (72650):** eyes at 4 %, chin at 52 %. The plate shows his hair and forehead.
- **Estinien (72644):** eyes at 27 %, chin at 55 %, face 13 % left.
- **Same fault:** Wuk Lamat (72661, eyes at 6 %, 36 quests), Venat (72647, eyes at 19 %), Zero (72651, eyes at 28 %), Lyna (72628), Crystal Exarch (72629), Ryne (72627, chin at 93 %), Ryne as "Minfilia" (72624), Alisaie in ShB (72622, face 26 % right). Mild: Alphinaud in EW (72638), Koana (72663).
- **Default battle-talk crops off-centre:** Ilberd (73132), Conrad (73109), Yda (73156). Not yet measured.
- **Estinien 3.0:** the card (87088) frames his helmet high and right.

**The pack makes it worse.** `PackWins` ranks a pack photo above all game art, including curated busts.

- Estinien's pack photos (1007115, 1012111, 1029535, 1036120) frame his lance and wing.
- Y'shtola's ShB-to-7.x photo (1026571) is her staff. Lyna's (1028865) is her ears; Zero's (1042980) is armour.
- Unukalhai's are a blob (1014565) and a weapon (1035057); one Raubahn photo (1020400) is a helmet.
- Faces sit low across the pack: eyes at about 52–55 %, chin at or past the edge (Varshahn, Alphinaud). Urianger, G'raha and Thancred are pushed aside by their weapons.

## Complaint 2: no portrait in earlier arcs

Confirmed for several leads.

- **Alphinaud, ARR:** none for 20 quests. The twins' card 87059 is blocked, and no ARR-look art is read.
- **Alisaie:** none in 2.x or 3.4–3.x.
- **Krile, 3.1:** none. Card 87199 is numbered among the SB cards, but the look is unchanged from 3.1 to 6.5.
- **Jullus, EW:** none for 7 quests. Card 87466 and battle-talk 73290 are dated DT, but both show his EW look.
- **G'raha Tia, Crystal Tower:** none, by design.
- **No game face at all:** Severian (15), Alka Zolka (11), Unukalhai (11), Meffrid (9), Isembard, Mikoto, Hermes, Chai-Nuzz and Moenbryda. Ketenramm, Shale and Bakool Ja Ja have no game face and no pack photo either: Garland has no Dawntrail renders.
- **Nashu:** she is on the left of the Hildibrand card (87062). The first-word rule can't match her, and iconCrops hold one box per icon.

## Complaint 3: the look should match the arc

The picks are mostly right by expansion and wrong within an expansion: eras are ExVersion, but outfits change by patch.

- **Alphinaud and Alisaie, 5.4 to early 6.0** (18 quests): back in their HW clothes; the game data agrees (HW body models 74639 and 74650). Tsukimichi shows their ShB and EW busts.
- **Alphinaud, 3.0 before "In Search of Iceheart"** (6 quests): still in the ARR outfit, but shown with the HW battle-talk face 73034.
- **Tataru, 4.56–5.x:** back in her ARR look (CGW, and the ARR body). Tsukimichi shows the SB kimono card 87241.
- **Tataru, 6.0–7.x:** shown the same kimono card. Her EW battle-talk face 73072 is curated, but the card family outranks it.
- **Sphene, 7.0** (4 quests): shows the restored 7.2 bust 72664, the wrong look and a within-expansion spoiler. Her queen card 87425 and battle-talk 73270 exist.
- **Zero, 7.x:** shows the 6.2 reaper look (72651). 73274 or 87467 show the later look.
- **Minor:** the unhooded Crystal Exarch in 5.1–5.3, Cid's HW jacket, Tataru's 3.0 Ishgard outfits, Papalymo in 3.x, Gaius in 6.x.

## Top 15 problems

| # | Character, arc | Quests | Status | Fix |
|---|---|---:|---|---|
| 1 | Wuk Lamat, 7.0–7.x | 36 | off-centre | iconCrops 72661: eyes [0.57, 0.198], chin 0.324 |
| 2 | Alphinaud, 2.0–2.55 | 20 | none | Pack photo 1006368 only. Re-measure a per-face crop on 87059 before ruling it out |
| 3 | Varshahn, 6.x | 11 | off-centre (art and pack) | 72650: eyes [0.44, 0.19], chin 0.33 |
| 4 | Estinien, 6.0–7.x | 7 | off-centre; pack shows his weapon | 72644: eyes [0.31, 0.259], chin 0.339; skip pack 1036120 |
| 5 | Estinien, 3.0 | 5 | off-centre helmet; pack shows his lance | Re-box 87088, or read 72645 (HW-armour Trust bust); skip 1007115 and 1012111 |
| 6 | Alphinaud and Alisaie, 5.4 to early 6.0 | 18 | wrong look | Pin the 18 ids to 72630 and 72631 (HW-coat busts) or 73034 and 73036 |
| 7 | Y'shtola, 5.0–6.x (pack) | 22 | pack shows her staff | Skip 1026571, or rank curated art above the pack |
| 8 | Tataru, 6.0–7.x | 6 | wrong look (SB kimono card) | 73072 (pin, or pick by era first) |
| 9 | Ryne, 5.0–6.x | 15 | off-centre | 72627: eyes [0.40, 0.335], chin 0.45 |
| 10 | Crystal Exarch, 5.0 | 8 | off-centre | 72629: eyes [0.33, 0.255], chin 0.36 |
| 11 | Jullus, 6.0–6.x | 7 | none | faces: 87466 at era 4 |
| 12 | Sphene, 7.0 | 4 | wrong look and spoiler | Pin to 87425 or 73270 |
| 13 | Venat, 6.0 | 5 | off-centre | 72647: eyes [0.27, 0.236], chin 0.364 |
| 14 | Zero, 6.x and 7.x | 7 | off-centre; 7.x wrong look; pack is armour | 72651: eyes [0.45, 0.26], chin 0.37; pin 1058818 to 73274 or 87467; skip 1042980 |
| 15 | Alisaie, ShB | 7 | off-centre | 72622: eyes [0.60, 0.305], chin 0.42 |

Small curated entries worth adding as well:

- **Lyna 72628:** eyes [0.30, 0.283], chin 0.40; skip pack 1028865.
- **Tataru 5.x:** pin to 87019.
- **Krile 3.x:** add 87199 to faces at era 1.
- **Alisaie 3.x:** add 73036 or 72631 to faces at era 1.
- **Koana 72663:** eyes [0.42, 0.29], chin 0.40.
- **Ryne as "Minfilia" 72624:** eyes [0.43, 0.345], chin 0.46.

## Causes behind these problems

1. **The Trust-bust default box fits few busts.** Curating the 13 busts above fixes most of complaint 1. A separate default for busts 72644 and up would help new ones.
2. **The pack outranks curated art.** Either rank game art with an iconCrop above the pack, or add the bad photos to the builder's `--skip`.
3. **Family is picked before era.** An older face in a better family beats a newer face in a lower one: Tataru in EW, Zero in 7.x, Yugiri in SB. Proposed rule: pick the latest era not after the quest's across families, use family rank as the tie-break, and keep strips last. Raubahn is the exception: his SB battle-talk face 73031 is helmeted, so the ARR card should stay.
4. **Eras are expansions, but looks change by patch.** The lasting fix is a curated "look key" that maps the ENpcBase body model to an icon. About 30 body models cover every major character here. Pins by ENpc id work now but need about 50 ids.
5. **Trust outfit alternates are never read.** `DawnGrowMember` holds four outfits per Trust member, and only the ones `DawnQuestMember` uses reach the index. Unread era-correct busts:
   - 72632: Thancred, 3.1 eyepatch
   - 72633: Urianger, ARR hood
   - 72634: Y'shtola, HW
   - 72635: Ryne as "Minfilia"
   - 72645: Estinien, HW armour
   - 72640–72643 and 72646: EW and Garlemald outfits
   - 72652–72658 and 72660: Trust-only summer outfits. Never use these for story quests: the 7.x giver rows keep their EW and ShB bodies.
   - 72630 and 72631 (the twins' HW coats) are read, but at era 3, so HW and 5.4 quests never pick them.

## Where lore and game data disagreed

- **Tataru 4.0–5.x:** FF Wiki shows only her SB yukata. CGW says she returns to the ARR look at 4.56, and the game data (ARR body) agrees with CGW.
- **Alphinaud and Alisaie in 7.x:** lore only says "presumably EW". The game data confirms the EW bodies, so 72638 and 72639 are the right looks for Dawntrail.
- **Thancred and Y'shtola in 7.x:** lore is undocumented; the game data keeps their ShB bodies, so the current busts are right.
- **Estinien 5.x:** lore calls it a "travel outfit"; the game data shows the same body as EW.
- **Estinien after the Aery:** CGW dates the change to "Into the Aery" (3.0), FF Wiki to "The Final Steps of Faith" (3.3).
- **Varshahn:** lore has a child look (6.0) and an adult vessel (6.2+). Every giver row is the adult body.
- **Raubahn:** no source mentions a prosthetic, but the game has a distinct SB body.
- **Vrtra:** his giver row uses a monster model, so the dragon card is correct.

## Unsure

- The unnamed battle-talk faces are not identified. 73259–73264 may include Bakool Ja Ja. 73053 and 73062 ("ALPHINAUD", era 3) are unverified and were not used.
- The boxes for 87088, 73132, 73109 and 73156 are not measured.
- Pack framing was judged by eye on about 60 photos, not measured.
- Calling the unhooded Crystal Exarch, Cid's jacket and Tataru's 3.0 outfits "wrong" is a judgement call.

## Confidence

- **High:** the framing and "none" findings (measured and computed with the plugin's own code), and the within-expansion look changes (lore and body models agree).
- **Medium:** minor characters' looks and the 7.x lore.
- **Unverified:** in-game rendering.

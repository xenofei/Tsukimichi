# Giver portraits: feasibility (plan v7, owner point 4)

> "Can we also include face profile screenshots of the giver?"

This is research only; no product code changed. The probes ran against the local install (game version
2026.09.15) with Lumina 7.7.1 / Lumina.Excel 7.5.0, in a scratch project outside the repo
(`%TEMP%\claude\…\scratchpad\portraits\`, modes `dump`, `blocks`, `scan`, `face`, `cov`, `cov2`, `fb`, `montage`).

## The short answer

Yes. The best result comes from three tiers, each used only when the one before it has nothing:

1. **Game art, read at runtime (no download).** The install already holds about 1,000 painted or rendered NPC
   busts, spread over four families: Duty Support/Trust portraits, the painted "battle talk" faces, Triple Triad
   card art and custom-delivery portraits. Matching them to givers by name covers **118 givers, 939 of 5,264
   named-giver quests (17.8%), and 726 of 1,049 MSQ quests (69%)**. Every major Scion and story lead is in that set.
2. **An optional portrait pack, built from Garland Tools' NPC photos.** These are uniform full-body studio renders
   keyed by NPC id. A sample of 24 random giver ids found photos for **20 (≈83%)**; the misses were Dawntrail-era
   NPCs and a beast-tribe NPC. We head-crop them offline and ship them as an opt-in download of about 10–20 MB.
   Together with tier 1, that is roughly 80–85% of all quests.
3. **A styled fallback.** Allied-society quests (689, 13%) show the society's emblem (`BeastTribe.Icon`). Generic,
   lowercase-named NPCs ("troubled adventurer": 417 names, 538 quests) show a race silhouette. ENpcBase holds race
   and gender for 2,644 of 3,174 giver ids. Everyone else gets a moon medallion with their initials.

Rendering the 3D model live (option 2) is technically reachable but not worth the risk. Capturing the game frame
(option 3) works with today's Dalamud API, but it gives uneven results at the least useful moment. It is worth
keeping at most as a later, explicit "Take portrait" button.

## The givers we need faces for

These counts cover Quest rows that have a name and whose `IssuerStart` points at an `ENpcResident`, which is the
catalog's universe.

| | count |
|---|---|
| Quests with an ENpcResident giver | 5,368 (5 more start at an EObj; none have no issuer) |
| Distinct giver ids | 3,174 |
| Distinct giver names | 2,072 (2,071 non-blank) |
| Quests whose giver has a name | 5,264, of which 1,049 are MSQ |
| Generic names (lowercase first letter) | 417 names, 538 quests |
| Humanoid givers (ENpcBase.Race set) | 2,644 ids: Hyur 969, Elezen 648, Miqo'te 271, Lalafell 253, Roegadyn 188, Au Ra 138, Hrothgar 97, Viera 80 |
| Non-humanoid givers (moogles, beast tribes, …) | 530 ids |

Faces matter most for a small set of NPCs:

- **Top 25 givers:** 15.8% of all quests and **53.8% of MSQ**.
- **Top 100:** 34.4% of all quests and 80.9% of MSQ.
- **Top 300:** all of the MSQ.

The long tail is long: 1,000 givers cover only 79.7% of quests. Beyond the Scions, the biggest single givers are the
allied-society quartermasters (Munavanu, Mogek, Seigetsu, Eshana, Uin Nee, Qhoterl Pasol, Regitt, Maru, Stigma-4,
Managingway, Yubli, Kageel Ja, Vuyargur and Amh Garanjy, at 27–30 quests each). That is why the society emblem is a
good stand-in for them.

## 1. Game data at runtime

### What exists

A sweep of every icon folder from 060000 to 135000, plus a reverse scan of every sheet for references into the
portrait ranges, found these families:

| Source | Icons | Size (hr) | How it maps to an NPC | Look |
|---|---|---|---|---|
| **Duty Support / Trust**: `DawnQuestMember.BigImageOld` / `BigImageNew` | 072621–072664 (tall), 072681–072799 (wide) | 188×480 tall bust; 640×180 face strip | `Member` → ENpcResident; for rows with a blank name, `Class` → `DawnMemberUIParam` column 1 (e.g. Wuk Lamat, Koana, Krile, Sphene) | Full-colour official render; the tall bust crops to a perfect face |
| **Battle-talk faces**: the `BATTLE_TALK_FACE` set | 073001–073291 (290) | 640×512 | **`QuestBattle` script variables `FACE_GRAPHIC_<NAME>` = icon id** name 165 of them automatically (e.g. `FACE_GRAPHIC_YSHTOLA=73025`, `FACE_GRAPHIC_PAPARIMO=73164`). They are also used by `ContentDirectorBattleTalk` (27), `AkatsukiNote` (29) and `EmjCostume` (21), which carry no name | Painted sepia bust on a brass slash; the most "Tsukimichi" look of all |
| **Triple Triad cards**: `TripleTriadCard` row *n* | 087000 + *n* (476 cards) | 208×256 card (the 088000 icons are 80×80) | Card `Name` is the NPC's full name ("Tataru Taru", "Cid Garlond", "Hildibrand & Nashu Mhakaracca") | Full-colour face inside a gold frame; crop inside the frame |
| **Custom deliveries**: `SatisfactionNpc.Icon` | 061661–061670 | 400×480 | `Npc` → ENpcResident | Full-colour bust with the client emblem (the last 5 clients have no icon) |
| `HalloweenNpcSelect.PreviewIcon` | 073801–073885 | 752×752 | description text only | **White silhouettes**, not faces. Useful only as proof that silhouettes read well |
| `Leve.IconIssuer` | 110xxx | 376×120 | `LeveClient` "Client: Title, Name" | Journal-style banner, not a face |
| `Companion` "wind-up *X*" minions | 04xxxx | 40×40 (80 hr) | name match | Chibi icons, too small and too cute for a portrait |
| `CustomTalk.IconActor` | 4 distinct | 192×192 | — | Generic service icons |

These were checked and give nothing: `ENpcBase` and `ENpcResident` have no image column; `Quest.Icon` is the
376×120 journal banner the hero already uses (2,531 of 5,368 quests have one); `EventIconType` holds map markers;
`BannerBg` and `CharaCard*` (Adventurer Plates) hold player-only frames and backgrounds; `MobHuntTarget` and
`MonsterNoteTarget` are monsters; the Lodestone has no NPC pages.

### Coverage measured (named givers, by normalised name)

| Source | Givers | Quests | MSQ quests | Biggest |
|---|---|---|---|---|
| Trust / Duty Support | 32 | 582 | 541 | Alphinaud 138, Alisaie 41, Minfilia 38, Urianger 37, Wuk Lamat 36, Y'shtola 32, Krile 24, Thancred 23, Hien 20, G'raha Tia 19, Lyse 19 |
| Battle talk (auto-named) | 40 | 535 | 469 | + Cid, Tataru, Raubahn, Yugiri, Haurchefant, Ilberd, Conrad, Emmanellain, Drillemont, Galfrid |
| Triple Triad (exact name) | 63 | 471 | 353 | Gerolt, Sphene, Estinien, Ryne, … |
| Triple Triad (+ unique first-name alias) | 91 | 778 | 588 | adds Tataru→Tataru Taru, Cid→Cid Garlond, Momodi, Baderon, Merlwyb, Hildibrand, Gaius, Lucia, Jullus, Clive, Prishe, … |
| Custom deliveries | 4 | 14 | 5 | Kurenai, M'naago, Kai-Shirr, Ameliance |
| **Union of the four (portrait grade)** | **118** | **939 (17.8%)** | **726 (69.2%)** | |
| + leve banners and wind-up minions | 144 | 1,004 (19.1%) | 757 (72.2%) | not recommended |

Gaps in the top MSQ givers that tier 1 cannot fill: Erenville (16 MSQ; he has a leve banner only), Momodi without
an alias, Lucia without an alias, Meffrid, Mother Miounne, Cahciua, Buscarron, Jullus, Isembard, Moghan and Shale.
The pack fills these.

The battle-talk set has about 125 more faces that are not named by any script variable. Many are recognisable leads
(Merlwyb, Kan-E-Senna, Hien, Aymeric, Lyse, Nashu, Godbert, beast-tribe chiefs). A one-time curation pass that names
them by eye (the montage takes about an hour to review) would lift tier 1 a little further. Several auto-names also
need an alias table: `PAPARIMO`→Papalymo, `IDA`→Yda, `KORUTT`→Corutt, `RANJIT`→Ran'jit, `FORDRA`→Fordola. The
battle-talk names that match no giver are mostly one-off combatants (soldiers, terrorists, hostages), which are
fine to leave out.

### Era and identity

One name can map to several portraits: Alphinaud has 4 battle-talk faces and 3 Trust variants. Pick the one whose
patch is closest to the quest's expansion; the `QuestBattle.Quest` and `DawnQuestMember` rows give an era for each.
Name matching is safe for proper names. It is unsafe only for generic names, which we never match, and for aliases
that are not unique; the alias rule above accepts a first-name alias only when exactly one card starts with it.

### Cost

None in download. These are game icons, loaded through the plugin's existing `TryGetFromGameIcon` path (with
hr variants), and cropped with UV coordinates in `AddImageRounded`, so no new texture is made. The index is a
name → (icon, crop rect) map built once beside the catalog, like `BannerSources`.

## 2. Rendering the NPC's 3D model live

**Feasible in principle, not recommended.** FFXIVClientStructs exposes
`FFXIV.Client.UI.Misc.CharaView` (`Initialize`, `SetModelData`, `SetItemSlotData`,
`SetCameraYawAndPitch`, `SetCameraDistance`, `Render`, `Release`). This is the engine behind the Character
window, Try On and the Adventurer Plate portrait. ENpcBase holds everything needed to dress a model: race, tribe,
the full set of customize bytes, `ModelChara`, `NpcEquip`, per-slot models and dyes. In theory a plugin could
render a bust offscreen and draw the render target's D3D11 view in ImGui. In practice:

- It is reverse-engineered, unsupported surface. It needs a free client object slot and a framework-thread
  lifecycle, and a patch can crash the game in a way that disables every plugin.
- No plugin I know of renders arbitrary NPCs this way. HaselTweaks' Portrait Helper only drives the game's own plate
  portrait.
- It looks like a live 3D render, not a portrait. It needs lighting, a pose and an expression, and non-humanoid models
  (530 givers) need special framing.

The owner's "player value first" rule says no: tier 2 gives the same renders as a flat image, with none of the risk.

## 3. Capturing in game

**Feasible with today's API.** This was checked in `Dalamud.xml` from the dev hooks:

- `ITextureProvider.CreateFromImGuiViewportAsync(ImGuiViewportTextureArgs)` with `TakeBeforeImGuiRender = true`
  copies the main viewport before Dalamud's ImGui draws, which is the game frame. `Uv0` and `Uv1` crop it.
- `ITextureProvider.CreateFromExistingTextureAsync(…, TextureModificationArgs { Uv0, Uv1, NewWidth, NewHeight })`
  resizes the crop.
- `ITextureReadbackProvider.SaveToFileAsync` writes it as PNG to the plugin's config folder.
- Head position: the NPC's `IGameObject.Position` plus height, through `IGameGui.WorldToScreen`. The face bone on
  the skeleton is more precise, through ClientStructs.

Problems:

- **UX.** The frame includes the game's own UI: the nameplate sits right on the head, plus the target ring, chat
  and other players. The angle is whatever the player's camera happens to be (often the back of the head or a
  profile), and lighting depends on time of day and weather. Results would be uneven and often ugly; the realism
  supervisor would reject most of them.
- **Value.** You can only capture an NPC once you are standing in front of them, which is exactly when you no
  longer need a picture to find them. Coverage grows only along the player's own path.
- **Legal and privacy.** Screenshots for personal use are fine under the FFXIV Materials Usage License. Crops can
  contain other players' characters, so they must stay local, never be uploaded, and be easy to clear.
- **Policy.** Silent capture on target or talk is automation the owner would not want running unseen. The most
  this should be is an explicit **"Take portrait"** button, shown while the giver is targeted, that fills a gap
  tiers 1–2 leave (mainly brand-new-patch NPCs before the pack catches up).

## 4. Web sources

| Source | What it has | Coverage | Licence and fitness |
|---|---|---|---|
| **Garland Tools** (`/db/doc/npc/en/2/<id>.json` → `"photo":"Enpc_<id>.png"`, served at `/files/photos/npc/…`) | Uniform **full-body studio renders** on transparent backgrounds (Zhloe: 380×706, 148 KB), keyed by ENpcBase id, with appearance aliases (`photo` points at a canonical twin: Thancred 1029755→1028975, Cid 1026450→1012195) | **20 of 24 random giver ids (83%)**. Missing: Tepeke, Gulool Ja and a budding botanist (7.x), and a Zundu intelligencer | The page credits "@Celes" for the photos. No explicit reuse licence; they are renders of Square Enix assets. Redistributing them in a free, non-commercial fan tool with credit is low-to-moderate risk. **Best fit:** the same framing every time makes automatic head-crops reliable, and each one shows that exact NPC's look in that quest |
| **Console Games Wiki** | Infobox screenshots, varied framing (Buscarron: 664×715 JPEG, 73 KB); some `_portrait` files (Eshana) | 5,557 of 11,062 NPC pages sit in "NPCs with no image" (≈50% overall). Named quest NPCs fare better: 5 of 6 sampled had one | Each file says it is "property of SQUARE ENIX © used with permission. **The terms of the permission do not include third party use.**" → **do not redistribute** |
| Gamer Escape | Similar infobox screenshots | not sampled (403 to automated fetch) | Same Square Enix permission model; treat it like CGW |
| XIVAPI v2 | `ENpcResident` row fields only; its asset endpoint serves the same game icons we read locally | none beyond tier 1 | n/a |
| Lodestone / official fan kit | No NPC database. The Fan Kit (behind the Materials Usage License) has wallpapers and a few X avatars of headline characters | a few dozen headline characters, already in tier 1 | Usable under the licence's legal line, but adds nothing |
| Fan art | Per-artist permission | a handful of heroes at most | Doesn't scale. Only for a hand-picked "hero" override, if the owner ever wants one |

**Pack size estimate.** Named giver ids × 83% coverage, de-duplicated through the `photo` aliases, comes to about
2,000–2,500 distinct photos. A 96 px head crop (and 192 px for hr) as quantised PNG is about 5–8 KB each, so
**≈10–20 MB**. That is too big for the main plugin zip, but fine as an opt-in download from the plugin's own
GitHub release. Building it means one rate-limited pass (1 request/s, under an hour) run by the owner through
DataGen. It was **not** run during this research.

**Head crop.** The renders are front-facing and full-body on alpha. The top row with alpha is the crown; the head
height is a race-dependent fraction of the figure's height (about 1/7 for tall races, about 1/3.5 for Lalafell,
taken from ENpcBase.Race); the centre x is the alpha centroid of that band. This is untested beyond eyeballing one
sample, so the DataGen step should write a contact sheet for a spot check.

## 5. Styled fallback

These apply when no art is found, or when the spoiler shield masks the quest:

- **Allied-society givers.** The society's own emblem (`BeastTribe.Icon`, already a game icon): 689 quests. This also
  covers the quartermasters that tiers 1–2 miss.
- **Generic NPCs** (lowercase names: 417 names, 538 quests). A **race silhouette**: 8 races × 2 genders, drawn as
  16 flat vector shapes in the moonlit ink. The data comes from `ENpcBase.Race` / `Gender`. Non-humanoids get a
  neutral moon disc.
- **Named NPCs with no art.** A **moon medallion with initials** ("MV" for Munavanu) in the Title face, on the same
  plate as the hero medal, so it reads as designed rather than missing. No noise glyphs: one or two letters only.
- **Spoilers.** A masked quest never shows a face. It falls back to the silhouette or medallion, so the face of a
  future-expansion reveal (Venat, Sphene, Zero) stays hidden until the quest is unmasked.

## Recommendation and UX sketch

**Combination:** tier 1 (game art) is always on. The tier 2 pack is opt-in. Tier 3 fallbacks fill the rest.
Capture comes later at most, as an explicit button.

| Where | Size | What |
|---|---|---|
| Detail pane, Giver card (Full and Quiet) | **72 px** circle (64 px at Quiet), left of the name and place lines | The portrait inside a moon-medallion mask with a 1 px brass keyline, given the light night grade the banners use, so Trust colour, card colour and sepia battle-talk art sit together. It fades in over 0.3 s on a new selection (never under Reduce motion). The layout is static: the slot is always there, so the fallback holds its place while a texture loads |
| Plain (ledger) | 18 px inline before the giver's name | Same source; no fade |
| Hover on any portrait | 128 px in the tooltip | Above the source line ("Portrait: Triple Triad card art", "Garland Tools photo, credit Celes"), plus the place |
| Next stops / Route window / Tonight card | 24 px avatar beside the stop | This is the real player value: recognising who to walk up to |
| Journal rows | optional 20 px giver column, **off by default** | Rows stay calm |
| Settings ▸ Decoration | "Giver portraits: Off · Game art · Game art + portrait pack". "Download portrait pack (≈15 MB)" asks first and shows progress and a hash check. "Clear portraits" asks for confirmation | Safety on destructive clicks |

Source priority for each giver: the pack photo (exact appearance for that NPC id) → Trust tall bust → Triple Triad
card → battle-talk face → delivery portrait → fallback. Within a source, pick the variant closest to the quest's era.

**Coverage with this plan:** roughly 18% of quests and 69% of MSQ with tier 1 alone. Adding the pack brings it to
about 80–85% of quests. Everyone else gets a society emblem, a silhouette or a medallion. No giver shows an empty
hole.

## Proposed plan rows

| # | Row | Effort | Notes |
|---|---|---|---|
| P1 | **Portrait index from game data.** `GiverPortraits` in Tsukimichi.GameData (standalone on `ExcelModule`, tested like `BannerSources`): Trust (with DawnMemberUIParam names), Triple Triad (exact and unique first-name aliases), `QuestBattle` `FACE_GRAPHIC_*` names with an alias table, custom deliveries; one crop rect per source; era pick | 1.5 d | Coverage test asserting ≥ 110 givers and ≥ 65% of MSQ on the fixture |
| P2 | **Giver card portrait and fallbacks.** 72/64/18 px slot, medallion mask, night grade, fade, tooltip at 128 px with the source line; society emblem, race silhouettes (16 vector shapes), initials medallion; spoiler-shield gating | 2 d | Realism supervisor reviews the silhouettes and the medallion |
| P3 | **Curation pass.** `curated/giver_portraits.json` overrides (name or NPC id → icon and crop): name about 125 unnamed battle-talk faces, fix aliases and era picks, block wrong matches | 0.5–1 d | Uses the montage tool from this research |
| P4 | **Portrait pack (opt-in).** DataGen `--portraits`: fetch Garland photos for the catalog's giver ids at 1 req/s, alpha head-crop by race, 96 and 192 px PNG, write a contact sheet. Zip on the GitHub release; in-plugin download with consent, hash check and clear | 2.5 d | Credit Garland Tools and Celes in the About section. Owner runs it once per major patch |
| P5 | **Avatars elsewhere.** 24 px in Next stops / Route / Tonight; optional 20 px journal column (off) | 1 d | |
| P6 | *(Later, optional)* **"Take portrait" button** while the giver is targeted: viewport capture before ImGui, head-bone crop, saved locally, never uploaded | 2 d | Experimental; only for NPCs that tiers 1–2 miss |

Total for P1–P5: about 7.5–8 days.

## Not recommended

- A live 3D render through `CharaView` (crash risk and patch fragility for no gain over P4).
- Silent automatic capture.
- Redistributing Console Games Wiki or Gamer Escape images (their licence excludes third-party use).
- Wind-up minion icons or leve banners as portraits (wrong shape, wrong tone).

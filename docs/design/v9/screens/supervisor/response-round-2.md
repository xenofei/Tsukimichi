# Moonfall screens: response to round 2, and the owner's two decisions

Round 2's verdicts: the game designer and the UX/UI specialist said APPROVE, with Minors listed. The level critic said REVISE, with one Major. Every finding is answered below.

The owner then decided two things, and both are built:
1. Stage 8 is renamed "The Floating Market".
2. The Far Shore follows Tsukimichi's spoiler shield.

The renders in `../renders/` are re-made from this state. The new renders are:
- `far-veiled-*`: the map at Shadowbringers, with stage 9 selected;
- `duelhud-plain-*`: a duel on the plain board;
- `scene-veiled-1280` and `scene-shown-1280`: base-04's Kugane scene before and after Stormblood.

## The owner's decisions

### Far Shore names
The names are approved as listed, with stage 8 as "The Floating Market". The change is in `MoonfallStages.ExpansionNames`, in `moonfall-modes.md` and in the renders.

### The Far Shore follows the spoiler shield
**Era tagging** (`Tsukimichi.Core/Moonfall/MoonfallPlaces.cs`). Each Far Shore stage, and each shipped scene, is set in an area on the shield's own era scale: the game's expansion number, with the area named as the shield places it (`SpoilerKind.Area`).

| Stage | Area | Era |
|---|---|---|
| 1 Lantern Quay | Limsa Lominsa Lower Decks | A Realm Reborn |
| 2 Twin Lights | Western Thanalan | A Realm Reborn |
| 3 Skyward Deck | The Sea of Clouds | Heavensward |
| 4 Sunlit Isles | The Ruby Sea | Stormblood |
| 5 Admiral's Sea | Western La Noscea | A Realm Reborn |
| 6 Ferry in the Stars | nowhere (our own painting) | — |
| 7 Floating Grove | Il Mheg | Shadowbringers |
| 8 Floating Market | Kugane | Stormblood |
| 9 Domes of Sharlayan | Old Sharlayan | Endwalker |
| 10 Archon's Crossing | Labyrinthos | Endwalker |
| 11 Courier's Wake | The Churning Mists | Heavensward |
| 12 Sea of Sorrows | Mare Lamentorum | Endwalker |

Scenes:

| Scene | Area | Era |
|---|---|---|
| holy-see | Coerthas Central Highlands | A Realm Reborn |
| lantern-night | Kugane | Stormblood |
| airship-road | nowhere (the world map) | — |
| moon-road-night | nowhere (our own painting) | — |

**The rule** (`MoonfallShield`). A place is hidden while the shield masks its area. This is `SpoilerMask.IsNameMasked(Area, zone)`: the same rule, settings and session reveals as every other place name in Tsukimichi. The plugin reads the viewed character's shield live, and its fingerprint tells the window when to remake the menus' words and the scene veil.

**A veiled stage** (`MoonfallStageState.Veiled` and `MoonfallLevelState.Veiled`):
- **Name:** its name prints as the shield's placeholder ("Endwalker area 1"), in the placeholder's secondary tone. It has the shield's own three-line hover, and its right-click offers "Reveal this name" (`ShieldText.Interact` and `ShieldText.DrawMenu`). That reveal opens the stage.
- **Levels:** each reads "Past your story", with no name and no scene. Tiles show the shield's mark where a sealed tile shows its padlock.
- **Panel:** its line says why it is closed and how it opens. Its Play button is a new `Veiled` style, slate with the shield's mark, inert, and with the same reason on focus.
- **Gating:** Adventure, Quick Play, duels and challenges cannot play its levels. A challenge running through it shows "Runs past your story" with the shield's mark. The companions keep their own gating.

**A veiled scene.** A level whose stage is veiled, or whose scene's own area is hidden, is drawn on the night sky wherever its scene would show: on the board, in its thumbnail and in scenes built ahead. No picture stands in for it (`MoonfallGameArt.HidesScene`, `PickScene`, `RecipeHidden`). A reveal or a story step lifts the veil and rebuilds the thumbnails (`VeilChanged`).

**The map**
- A veiled stop has the shield's mark (Tsukimichi's eye-slash, as on Spoilers in Help and Settings) where the padlock goes. The padlock still means Moonfall's own progress.
- The legend has a new row: "past your story".
- A veiled stop is dimmed, has no "here" glow, and keeps the companion's face as the companion's own gating decides.

**Tests**
- `MoonfallShieldTests` (11 cases):
  - every Far Shore stage and every shipped scene is tagged;
  - masking at each era boundary from A Realm Reborn to Dawntrail, with every mode closed and nothing veiled shown as playable;
  - a reveal opens its stage and no other;
  - no veiled stage's name or area appears in the names the screens print;
  - the scene veil;
  - the open shield hides nothing.
- `MoonfallShieldGameDataTests`, over the installed game:
  - every tagged area is one the shield places, in its tagged expansion;
  - each area is hidden just before its era's story and shown after it;
  - the real shield veils the Endwalker stages until Endwalker or a reveal, and prints a registered shield placeholder.
- Lints:
  - every stage or level name the screens draw goes through `StageNameShown`, `LevelNameShown` or `PlayLevelName`;
  - the plugin passes the shield, its fingerprint and the session for the reveal.
- The renderer's `--text-check` hears every string drawn and fails on any veiled name or area: 0 leaks on the far map at both sizes, at Shadowbringers and at A Realm Reborn.

## Level critic, round 2

**[Major] Plain board: a duel had no opponent score and no turn.** Fixed. In a duel the plain bar shows:
- whose shot it is, first and in gold ("LOUISOIX IS THINKING", "YOUR SHOT");
- the level;
- the shooter's balls (`TubeBalls`);
- the oranges;
- "LOUISOIX 0" and "YOU 0" at the right.

The pause line also counts the shooter's balls. See `duelhud-plain-*`.

**Minors**

| Finding | Fix |
|---|---|
| Name cut to a stub | The name now shrinks to the label floor before it is cut, cuts at a word's end first, and is left out rather than cut below four letters. |
| 640 caption past the wall | The caption and the balls chip are kept inside the plates' span (`ChipSpan`, 86–716). |
| Caption small caps at 6 px | The captions are set in capitals. |
| "the twins is thinking" | New strings: "The twins are thinking" and "The twins' shot". |
| Stale name cache | The duel's words are keyed on the duel itself and the language. The fit is also keyed on whether the fonts are ready. |

**Nits:** the chips over a high ball, and the loader's top limit, are left for the board pass. No piece comes within 14.5 units.

## UX/UI, round 2

| Finding | Fix |
|---|---|
| m11, locked Duel gives no reason | It now has a tooltip on hover and focus ("Win a level in Adventure first…"), and the 1280 line reads "win a level first". |
| m12, 1280 Best on the rule | The 1280 caption plate is 60 units tall. |
| m13, sealed names cut at 640 | A name cut short shows whole in a tooltip on the tile's hover or focus, and is cut at a word's end. |
| m14, overlapping pages | Back to true pages; the list still opens on the page holding the selection. |
| m15, dimmed plate below 4.5:1 | The dim lies under the plate's words. The opponent's name is cream on the waiting plate. |
| m16, double click on Resume or the crest | Every pause or resume re-arms the board for 0.3 s and forgets a pending press. A press within that time of a pause is not an outside press. |
| m17, input fixes untested | New lints for the tally clause, the held-key carry-over, the outside-press rule and the re-arming. |

**Nits:**
- The stale outside press is cleared on every pause change.
- Padlock against Waiting: a challenge whose levels are on their way now uses Waiting, with no padlock.
- The power text wraps at 280.
- The held-Decoration note now reads "Reduce motion is on: nothing moves at any setting; Off still draws the plain board."

## Game designer, round 2

| Finding | Fix |
|---|---|
| m1, the strip repeated "Clear the oranges" | The power text is empty when there is none, so the sentence appears once. |
| m2, locked tile names | Cut at a word's end, with the full name on hover or focus. |
| m3, overlapping pages | True pages. |
| m4, 640 captions read as codes | The power's own name; its short form ("Draw") only where the full name overruns the card's pitch. No stage number and no "FS". |

**Nits:**
- **n1:** the tube's BALLS label takes the shooter's colour in a duel.
- **n2:** no stub shorter than four letters.
- **n3:** the power text wraps at 280.
- **n4:** the power-at-work glyph is clipped to the opening inside the frame.
- **n5:** "Its levels are on their way."
- **n6:** the 1280 caption is taller.
- **n7:** the note is reworded.
- **n8:** met-but-not-reached cards carry the padlock.
- **n9:** the HUD margins are left for the board pass.

## Gates
All four pass:
- `dotnet build -warnaserror`
- LoadCheck
- `dotnet test --filter "Category!=Perf"`: 8846 passed
- `build_themes.py --check`

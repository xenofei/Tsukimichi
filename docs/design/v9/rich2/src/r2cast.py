"""The eleven companions (rich pass 2): real FFXIV characters, shown with their Triple Triad card art from the player's
install (ui/icon/087000/<icon>_hr1.tex), framed by the plugin's own crop rules (Tsukimichi.Core/Portraits):

  * the face crop: the card family's box [28, 23, 135] in hr px (giver_portraits.json `crops.TripleTriadCard`, the
    1.15 spec box moved 3 px down), a square for the round medallions (HUD, map stops, grid rings);
  * the whole art: PortraitSources.ArtBounds(TripleTriadCard) = (0.067, 0.055, 0.933, 0.945), the art inside the
    card's gilt border, for the hero image;
  * the card itself (art and its gilt border), as the grid's cards.

Every character is one an A Realm Reborn player meets in the main scenario, so the spoiler shield rarely hides one;
when it does, the card is shown face down (the Triple Triad card back, ui/uld/TripleTriadBattle_hr1.tex).

Round 2 (game designer m1, m2): the accents are spread at least 25 degrees apart in OKLab hue and kept off the chrome's
gilt (60-100 degrees), so each power's colour can be learnt; each stage is themed to its companion's home (`home`).
"""
import functools

import numpy as np

from r2lib import icon, record, uld, resample, hexc, screen, srgb_to_oklab, oklab_to_srgb

CARD_BOX = (28, 23, 135)                    # giver_portraits.json crops.TripleTriadCard (hr px of the 208 x 256 card)
ART_BOUNDS = (0.067, 0.055, 0.933, 0.945)   # PortraitSources.ArtBounds(TripleTriadCard)

# id (MoonfallPower), power name, character, card icon, accent, role, personality line, what the power does, lasts,
# why this character, spoiler note, alternates
CAST = [
    dict(id="SuperGuide", turns=3, home="The Waking Sands", power="Super Guide", name="Minfilia", icon=87056, accent="#5DDAE0", stage=1,
         role="Leader of the Scions of the Seventh Dawn",
         line="Hears the Mother Crystal's voice, and knows where the road goes before you take it.",
         does="Her guide runs on past the first bounce, to the peg after.", lasts="3 shots",
         why="The Echo shows her what is coming; Super Guide shows the shot past its first bounce.",
         spoiler="Met in the A Realm Reborn main scenario, when the player first joins the Scions. The card shows her "
                 "as she is then; nothing of her later story.",
         alt="Thancred (card 87046)"),
    dict(id="Multiball", turns=1, home="Vesper Bay", power="Multiball", name="Alphinaud & Alisaie", short="the twins", icon=87059, accent="#66A5FF",
         accent2="#E0506A", stage=2, role="Twins of Sharlayan, the Archon's grandchildren",
         line="Never agree on a path, so they take both.",
         does="A twin ball springs from the green peg and flies the mirror of the first.", lasts="this shot",
         why="Two of them, two balls: the twins split the shot at the green peg.",
         spoiler="Alphinaud is met in the A Realm Reborn main scenario; Alisaie only in its later patches. Until the "
                 "shield clears Alisaie, the companion shows as Alphinaud alone (his Trust bust, 72621, is a later "
                 "look, so the card back is used instead).",
         alt="Biggs & Wedge (card 87033)"),
    dict(id="Wings", turns=5, home="The Night Skyway", power="Brass Wings", name="Cid Garlond", short="Cid", icon=87058, accent="#E69461", stage=3,
         role="Engineer of the Garlond Ironworks",
         line="Can fix anything with a spanner, and fly most of it.",
         does="He bolts airship wings to the bucket: its mouth doubles in width.", lasts="5 turns",
         why="Eorzea's great engineer: the airship wings come from his workshop.",
         spoiler="Met in the A Realm Reborn main scenario (the Garlond Ironworks).", alt="Biggs & Wedge (card 87033)"),
    dict(id="Burst", turns=1, home="The Sunlit Steps", power="Lunar Burst", name="Raubahn", icon=87067, accent="#F9786C", stage=4,
         role="General of Ul'dah's Immortal Flames",
         line="Speaks softly; the floor shakes when he does not.",
         does="One great sweep at the green peg lights every peg within its reach.", lasts="this shot",
         why="The Flame General's greatsword: one blow that reaches everything near.",
         spoiler="Met in the A Realm Reborn main scenario (Ul'dah).", alt="Papalymo & Yda (card 87048)"),
    dict(id="Flippers", turns=3, home="Harbour Lights", power="Flippers", name="Merlwyb", icon=87065, accent="#73C881", stage=5,
         role="Admiral of Limsa Lominsa",
         line="Keeps a fleet afloat on wit and powder.",
         does="Two oars at the foot's corners bat the ball back up when you click.", lasts="3 turns",
         why="The admiral keeps the ship afloat: two oars at the waterline.",
         spoiler="Met in the A Realm Reborn main scenario (Limsa Lominsa).", alt="Hildibrand & Nashu (card 87062)"),
    dict(id="Gate", turns=1, home="The Silent Stars", power="Moon Gate", name="Urianger", icon=87050, accent="#9E94FD", stage=6,
         role="Scholar of the Scions, reader of the stars",
         line="Answers every question with a better one.",
         does="A ball that falls out of the board drops back in from the sky above.", lasts="this shot",
         why="He reads portents in the stars: what falls returns from above.",
         spoiler="Met in the A Realm Reborn main scenario (the Scions).", alt="Y'shtola (card 87049)"),
    dict(id="Bloom", turns=1, home="The Shroud by Night", power="Moonbloom", name="Kan-E-Senna", icon=87066, accent="#B5CA4E", stage=7,
         role="Elder Seedseer of Gridania",
         line="Listens to the Twelveswood, and it listens back.",
         does="Moonflowers open from the green peg and light the nearest fifth of the oranges.", lasts="this shot",
         why="Gridania's seedseer asks the wood to bloom.",
         spoiler="Met in the A Realm Reborn main scenario (Gridania).",
         alt="Y'shtola (card 87049)",
         borderline="Moonbloom is the flower power, and Kan-E-Senna wears a great white flower: flowers belong to the "
                    "power itself, not to a sunflower character, but it is the nearest echo in the cast."),
    dict(id="Draw", turns=1, home="The Market Lanterns", power="Moon-Viewing Draw", name="Tataru", icon=87019, accent="#EE83AF", stage=8,
         role="Receptionist of the Scions, keeper of the purse",
         line="Counts every gil twice, and finds three more.",
         does="Her draw turns once: a free ball, a triple score, or another friend's power.", lasts="the draw",
         why="The Scions' purse-keeper runs the draw.",
         spoiler="Met in the A Realm Reborn main scenario (the Waking Sands).", alt="Momodi Modi (card 87028)"),
    dict(id="Fireball", turns=1, home="Mor Dhona's Glass", power="Fireball", name="Y'shtola", icon=87049, accent="#CB82E7", stage=9,
         role="Scholar and mage of the Scions",
         line="Patient with the world, impatient with fools.",
         does="The ball meets no peg: each one it touches burns away.", lasts="next shot",
         why="A Sharlayan mage whose spells burn through whatever stands in the way.",
         spoiler="Met in the A Realm Reborn main scenario (the Waking Sands).", alt="Papalymo & Yda (card 87048)",
         borderline="Y'shtola is a Miqo'te, a people with feline ears. She is a person, not a cat, and carries a "
                    "different power from Peggle's cat; noted for the owner."),
    dict(id="Path", turns=1, home="Silvertear by Night", power="Sage's Path", name="Louisoix", icon=87060, accent="#70D3BB", stage=10,
         role="Archon of Sharlayan, founder of the Circle of Knowing",
         line="Old enough to be patient, wise enough to be quick.",
         does="He weighs seventeen angles round your aim and nudges the ball onto the best.", lasts="next shot",
         why="The Archon, the twins' grandfather: the sage who finds the best path.",
         spoiler="Met in the A Realm Reborn prologue (the Calamity). The card is his A Realm Reborn look; nothing of his "
                 "later story.", alt="Urianger (card 87050)"),
    dict(id="Bolt", turns=1, home="The Courier's Sky", power="Storm Post", name="Moogle courier", short="Moogle", box=(14, 14, 180), icon=87020, accent="#5DCDFA", stage=11,
         role="Moogle post, every inn in Eorzea",
         line="Delivers the bolt, kupo, and signs for nothing.",
         does="The first peg lit sends a bolt straight to the bucket, lighting every peg along it.", lasts="this shot",
         why="Moogles carry Eorzea's post: the bolt is delivered from the first peg to the bucket.",
         spoiler="Moogles are met in the first hours of any start (the inns' mail moogles); no spoiler.",
         alt="Good King Moggle Mog XII (card 87043, Heavensward: later)"),
]
BY_ID = {c["id"]: c for c in CAST}
for _c in CAST:   # every companion's card is read at runtime, met or not
    record(f"ui/icon/087000/{_c['icon']:06d}_hr1.tex", f"Triple Triad card: {_c['name']} ({_c['power']})")


def first_name(c):
    return c.get("short") or c["name"].split()[0]


def _record(c):
    record(f"ui/icon/087000/{c['icon']:06d}_hr1.tex", f"Triple Triad card: {c['name']} ({c['power']})")


def card(c):
    """The whole card (art and its gilt border), float RGBA 208 x 256."""
    _record(c)
    return icon(c["icon"])


def light_grade(rgba, k=0.35, tint="#AFC4FF"):
    """The light night grade for hero images (owner: only lightly): shadows cooled a little toward the night's blue,
    highlights kept, a whisper of moonlight on the upper left. Chroma kept."""
    rgb = rgba[..., :3]
    lab = srgb_to_oklab(rgb)
    Lc = lab[..., 0]
    t = np.clip(1 - Lc / 0.6, 0, 1) * k
    tl = srgb_to_oklab(hexc(tint)[None, None])[0, 0]
    lab[..., 1] = lab[..., 1] * (1 - 0.5 * t) + tl[1] * 0.5 * t
    lab[..., 2] = lab[..., 2] * (1 - 0.5 * t) + tl[2] * 0.5 * t
    out = oklab_to_srgb(lab)
    return np.concatenate([out, rgba[..., 3:4]], -1)


def art(c):
    """The art inside the card's border (ArtBounds), light-graded."""
    a = card(c)
    h, w = a.shape[:2]
    u0, v0, u1, v1 = ART_BOUNDS
    return light_grade(a[int(v0 * h):int(v1 * h), int(u0 * w):int(u1 * w)])


def face(c, box=None):
    """The face crop (the plugin's card box), light-graded, square."""
    a = card(c)
    x, y, s = box or c.get("box") or CARD_BOX
    return light_grade(a[y:y + s, x:x + s])


@functools.lru_cache(None)
def card_back():
    record("ui/uld/TripleTriadBattle_hr1.tex", "Triple Triad card back (a companion the story has not introduced)")
    return uld("TripleTriadBattle")[729:729 + 254, 27:27 + 202]


@functools.lru_cache(None)
def card_frame():
    record("ui/uld/TripleTriadBattle_hr1.tex", "Triple Triad empty card frame")
    return uld("TripleTriadBattle")[729:729 + 254, 235:235 + 202]

"""The eleven Moonfall characters as moonstone cameos (see cameo.py). One builder per character, in local units: the
cameo is 200 x 240 units, (0, 0) near its centre, the head about 100 tall facing right.

Who they are (unchanged from the approved v9 cast, docs/design/v9/spec-moonfall.md):
  Pipiru Mimiru (Lalafell astrologian apprentice, Super Guide), Kaede Tsukiyo (Raen Au Ra dancer, Multiball),
  Marcia nan Arcus (Garlean engineer, Brass Wings), Haldbrand Tidewatch (Roegadyn gunbreaker, Lunar Burst),
  Gajavati (Arkasodara ferry-trader, Flippers), Ysolde Nocturine (Duskwight Elezen, Moon Gate),
  Sister Ottilie (Midlander Hyur priestess, Moonbloom), Gyobo (Namazu shrine attendant, Moon-Viewing Draw),
  Aldous Varrow (Highlander Hyur black mage, Fireball), Ione Selenis (Sharlayan sage, Sage's Path),
  Kupsa Brightpom (moogle storm courier, Storm Post).
"""
import math

import numpy as np

from cameo import face_features, head_profile
from rich_lib import blur


def bust(R, y0=62, left=-70, right=64, bottom=118, collar=None, h=3.0):
    """Shoulders and chest, cut off by the oval's foot, with a garment's neckline and a few soft folds; the neck
    stands in the neckline."""
    y0 = y0 - 8
    pts = [(left, bottom), (left + 6, y0 + 30), (left + 30, y0 + 8), (-20, y0), (14, y0 - 2), (right - 22, y0 + 10),
           (right, y0 + 36), (right + 4, bottom)]
    m = R.poly(pts)
    R.raise_(m, h, 10.0)
    # round 5 (realism round 4): the bust's top surface curves into the shoulders (a chest that rounds away)
    R.bump(-6, y0 + 46, 54, 30, 2.6, within=m)
    R.bump(left + 12, y0 + 34, 14, 20, -0.8, within=m)
    R.bump(right - 8, y0 + 40, 14, 22, -0.9, within=m)
    R.ridges([[(-30, y0 + 4), (-8, y0 + 16), (16, y0 + 8)]], 3.4, 1.1, soft=0.8)            # the neckline's hem
    R.ridges([[(left + 14, y0 + 40), (left + 26, y0 + 22)], [(right - 14, y0 + 44), (right - 26, y0 + 24)]],
             3.0, 0.5, soft=1.2)                                                         # soft folds
    return m


def hair_over(R, prof, grow=1.07, region=None, height=6.2, flow=None, n=220, length=14, seed=0, cx=-8.0, cy=-10.0):
    """Hair with volume: the head's profile grown about the skull's centre, kept behind the hairline (region), raised
    above the head and carved into strands."""
    grown = [(cx + (x - cx) * grow, cy + (y - cy) * grow) for (x, y) in prof]
    region = region or [(60, -90), (22, -47), (8, -32), (1, -16), (-5, 0), (-12, 18), (-24, 44), (-90, 50), (-90, -90)]
    m = R.poly(grown) * R.poly(region, smooth_n=0)
    R.raise_(m, height, 6.0)
    # combed back: every strand runs away from the front of the hairline, toward the crown, the back and the nape.
    # Round 4 (realism round 3): the hair is carved in locks (broad rounded ridges) first, then fine strands on them
    f = flow or (lambda x, y: math.atan2(y + 52, x - 30))
    hair_strands(R, m, f, max(12, n // 7), length * 1.7, width=5.0, h=1.3, seed=seed + 100, soft=1.2)
    hair_strands(R, m, f, n, length, width=1.3, h=0.35, seed=seed)
    return m


def folds(R, pts_list, w=2.4, h=0.7):
    R.ridges(pts_list, w, h)


def hair_strands(R, mask, flow, n, length, width=1.6, h=0.55, seed=0, soft=0.0):
    """Carved hair: ridges laid along `flow(x, y)` from random starts inside `mask` (local units); soft > 0 rounds
    them into locks."""
    rng = np.random.default_rng(seed)
    ys, xs = np.nonzero(mask > 0.5)
    if len(xs) == 0:
        return
    paths = []
    for k in rng.choice(len(xs), size=min(n, len(xs)), replace=False):
        x = xs[k] / R.S - R.ox
        y = ys[k] / R.S - R.oy
        pts = [(x, y)]
        for _ in range(6):
            a = flow(x, y)
            x += math.cos(a) * length / 6
            y += math.sin(a) * length / 6
            pts.append((x, y))
        paths.append(pts)
    m = R.strokes(paths, width)
    if soft:
        m = np.sqrt(np.clip(blur(m, soft * R.S), 0, 1))
    R.hgt = R.hgt + m * mask * h


# ------------------------------------------------------------------------------------------------ the eleven
def pipiru(R):
    """Lalafell astrologian apprentice: a big round head under a deep hood with one soft peak, a small nose, round
    cheeks; the star globe raised in front of her in its ring."""
    bust(R, y0=58, left=-62, right=48, h=2.8)
    head = R.poly(head_profile(nose=0.35, chin=0.75, brow=0.9, skull=1.18, jaw=0.85, scale=0.98, dy=4, lips=0.6, neck=0.9))
    R.raise_(head, 4.6, 14.0)
    face_features(R, 0.98, 0, 4, eye=(23, -2), ear=(-8, 8), ear_r=(7, 10))
    hood = R.poly([(-58, 66), (-62, 20), (-52, -30), (-30, -58), (-8, -66), (6, -70), (16, -62), (30, -46), (34, -30),
                   (28, -26), (14, -38), (-8, -40), (-26, -26), (-34, 4), (-26, 40), (-12, 64)])
    R.raise_(hood, 5.4, 6.0)
    # round 6 (realism round 5): five broad folds hang from the crown down and back toward the nape and the
    # shoulder; each is a rounded ridge between soft troughs, they never cross, and they stop inside the hood's edge
    inner = np.clip((blur(hood, 3.0 * R.S) - 0.55) * 4, 0, 1)
    folds_ = [[(2, -64), (-14, -46), (-28, -16), (-38, 18), (-46, 52)],
              [(-6, -60), (-22, -42), (-36, -12), (-48, 22), (-54, 56)],
              [(-14, -56), (-32, -38), (-44, -8), (-54, 26)],
              [(10, -58), (0, -48), (-14, -28), (-26, 0)],
              [(-24, -48), (-40, -30), (-52, 0), (-58, 30)]]
    for k, pts in enumerate(folds_):
        ridge = np.sqrt(np.clip(blur(R.strokes([pts], 7.0, taper=False), 2.2 * R.S), 0, 1))
        R.hgt = R.hgt + ridge * inner * 1.3
    for pts in ([(-2, -62), (-18, -44), (-32, -14), (-43, 20)], [(-10, -58), (-27, -40), (-40, -10), (-51, 24)]):
        trough = np.clip(blur(R.strokes([pts], 4.0, taper=False), 1.8 * R.S), 0, 1)
        R.hgt = R.hgt - trough * inner * 0.7
    folds(R, [[(-50, 50), (-52, 0), (-40, -36)], [(-38, 56), (-42, 10), (-28, -30)]], 2.2, 0.8)
    # the star globe in its ring, held before her
    R.raise_(R.ellipse(72, 30, 16, 16), 6.0, 10.0)
    R.ridges([[(54, 34), (62, 18), (78, 12), (90, 24), (86, 44), (70, 50), (56, 42)]], 2.0, 1.2)
    R.ridges([[(58, 28), (72, 26), (86, 32)]], 1.4, 0.6)
    R.ridges([[(48, 62), (60, 50), (64, 44)]], 4.5, 1.8)      # her small hand and sleeve


def kaede(R):
    """Raen Au Ra dancer: horns sweeping back from the temple, scales along the cheek and jaw, hair bound high, a
    chakram's rim at her shoulder."""
    bust(R, y0=62, left=-66, right=60, h=2.6)
    prof = head_profile(nose=0.85, chin=0.95, brow=0.98, skull=0.98, jaw=0.92, scale=0.95, dy=0, lips=1.1)
    R.raise_(R.poly(prof), 5.2, 22.0)
    face_features(R, 0.95, 0, 0, eye=(26, -6), ear=(-8, 4), ear_r=(4, 6))
    hair_over(R, prof, 1.08, seed=2)
    # her hair bound high and falling back in a long tail
    tail = R.poly([(-26, -46), (-44, -62), (-70, -66), (-92, -50), (-96, -24), (-86, -36), (-70, -46), (-50, -40), (-34, -30)])
    R.raise_(tail, 5.6, 5.0)
    hair_strands(R, tail, lambda x, y: math.pi + 0.35, 90, 16, seed=3)
    R.ridges([[(-30, -52), (-28, -36)]], 4.0, 1.4)                                 # the cord that binds it
    # the horn: from above the ear, hugging the skull and sweeping back past it, tapering, with growth rings
    horn = R.strokes([[(-2, -18), (-14, -30), (-30, -36), (-46, -32), (-58, -20)]], 10.0)
    R.raise_(horn, 7.6, 3.5)
    for k in range(5):
        x0 = -8 - k * 10
        R.ridges([[(x0, -36 + k * 0.8), (x0 - 2, -24 + k * 1.2)]], 1.0, -0.5)
    # scales: a band of small overlapping crescents from the cheekbone down the jaw
    sc = []
    for k in range(7):
        x, y = 10 - k * 2.2, 18 + k * 5
        sc.append([(x - 3, y + 1), (x, y - 1.5), (x + 3, y + 1)])
    R.ridges(sc, 1.1, 0.5)
    # the chakram's rim at her shoulder
    R.ridges([[(40, 74), (56, 64), (72, 70), (74, 86), (58, 96), (42, 90), (38, 78)]], 3.6, 2.2)


def marcia(R):
    """Garlean engineer, retired from the legion: the third eye on her brow, short practical hair, a high-collared
    work coat with rivets, a spanner over her shoulder."""
    bust(R, y0=60, left=-68, right=62, h=3.0)
    folds(R, [[(-52, 76), (-30, 66), (-6, 70)], [(20, 66), (40, 74)]], 2.6, 0.9)
    for (x, y) in ((-40, 86), (-22, 82), (22, 84), (40, 92)):
        R.raise_(R.ellipse(x, y, 2.4, 2.4), 1.1, 1.0, mode="add")     # rivets on the coat
    R.ridges([[(-20, 60), (-16, 46), (0, 50), (8, 62)]], 5.0, 1.8)     # the high collar
    prof = head_profile(nose=1.0, chin=1.05, brow=1.0, skull=0.98, jaw=1.05, scale=0.95, lips=0.9)
    R.raise_(R.poly(prof), 5.2, 22.0)
    face_features(R, 0.95, 0, 0, eye=(26, -6), ear=(-8, 4))
    # the third eye: a small polished gem in the brow
    R.raise_(R.ellipse(29, -25, 3.0, 3.0), 1.4, 2.0, mode="add")
    R.carve(R.ellipse(29.3, -25, 1.1, 1.1), 0.5, 0.3, tone=0.5)
    # short practical hair, cropped at the nape
    hair_over(R, prof, 1.05, region=[(60, -90), (20, -46), (8, -34), (0, -18), (-6, -2), (-16, 10), (-28, 26),
                                     (-90, 30), (-90, -90)], height=6.0, length=9, seed=4)
    # the spanner over her shoulder
    R.ridges([[(-62, 104), (-40, 70), (-20, 36)]], 6.0, 2.4)
    R.raise_(R.ellipse(-18, 30, 8, 8), 3.0, 3.0, mode="add")
    R.carve(R.ellipse(-14, 26, 4, 4), 1.6, 0.4)


def haldbrand(R):
    """Roegadyn gunbreaker of the Sea Wolves: a heavy brow and jaw, a full beard, a fur collar, the gunblade's hilt over
    his shoulder."""
    bust(R, y0=58, left=-74, right=70, h=3.4)
    fur = R.poly([(-70, 76), (-62, 54), (-30, 46), (0, 50), (30, 50), (56, 62), (66, 82), (40, 76), (0, 70), (-40, 74)])
    R.raise_(fur, 4.2, 4.0)
    hair_strands(R, fur, lambda x, y: math.pi / 2 + 0.3 * math.sin(x / 9), 240, 9, width=2.0, seed=5)
    prof = head_profile(nose=1.15, chin=1.25, brow=1.12, skull=1.02, jaw=1.3, scale=1.0, lips=0.8, neck=1.25)
    R.raise_(R.poly(prof), 5.6, 22.0)
    face_features(R, 1.0, 0, 0, eye=(25, -6), ear=(-8, 4), lid=0.8)
    beard = R.poly([(2, 26), (20, 30), (34, 36), (42, 50), (36, 66), (16, 70), (0, 60), (-6, 40)])
    R.raise_(beard, 6.6, 5.0)
    hair_strands(R, beard, lambda x, y: math.pi / 2 - 0.3, 200, 12, width=1.8, seed=6)
    R.ridges([[(24, 24), (34, 26), (40, 30)]], 2.4, 0.8)                           # the moustache
    hair_over(R, prof, 1.06, height=6.4, seed=7)
    # the gunblade's hilt and guard over his shoulder
    R.ridges([[(-66, 38), (-56, 6), (-48, -26)]], 7.0, 2.8)
    R.ridges([[(-72, 30), (-44, 42)]], 5.0, 2.0)
    R.raise_(R.ellipse(-48, -30, 5, 5), 3.0, 2.0, mode="add")


def gajavati(R):
    """Arkasodara ferry-trader from Thavnair: the trunk curling down from the face, the great fanned ear, a jewelled
    headband, a festival fan open at her shoulder."""
    bust(R, y0=64, left=-68, right=60, h=2.8)
    head = R.poly([(-26, 92), (-32, 60), (-40, 24), (-38, -14), (-22, -42), (2, -52), (24, -42), (36, -22), (40, -4),
                   (44, 10), (46, 26), (48, 44), (44, 60), (40, 70), (34, 66), (36, 52), (34, 38), (26, 34), (18, 44),
                   (10, 62), (8, 92)])
    R.raise_(head, 5.2, 24.0)
    # her forms (round 4): the domed brow, the socket, the cheek, the trunk's root swelling from the face
    R.bump(8, -28, 18, 12, 1.6, within=head)
    R.bump(20, -6, 7, 5, -1.3, within=head)
    R.bump(14, 12, 12, 10, 1.4, within=head)
    R.bump(36, 20, 8, 14, 1.2, rot=0.2, within=head)
    # the trunk's rings and the eye
    for k in range(6):
        y = 18 + k * 8
        R.ridges([[(34 + k * 0.6, y), (46 - k * 0.2, y + 1)]], 1.1, -0.4)
    R.carve(R.ellipse(22, -6, 4.2, 2.6), 0.9, 0.5, tone=0.7)
    R.ridges([[(14, -12), (22, -15), (30, -11)]], 1.8, 0.6)
    # the great ear, fanned back, with veins
    ear = R.poly([(-6, -26), (-36, -40), (-62, -30), (-70, 0), (-60, 30), (-34, 36), (-10, 18)])
    R.raise_(ear, 3.0, 4.0, mode="max", base=None)
    R.ridges([[(-12, 0), (-40, -24)], [(-12, 4), (-52, -4)], [(-12, 8), (-48, 20)]], 1.4, 0.5)
    # a jewelled headband
    R.ridges([[(-26, -38), (0, -48), (24, -38)]], 4.0, 1.2)
    R.raise_(R.ellipse(2, -46, 3.6, 3.6), 1.8, 2.0, mode="add")
    # the festival fan, open at her shoulder: ribs from a pivot
    px, py = 46, 96
    for a in np.linspace(-150, -60, 8):
        r = math.radians(a)
        R.ridges([[(px, py), (px + math.cos(r) * 42, py + math.sin(r) * 42)]], 2.4, 1.4)
    fan = R.poly([(px, py)] + [(px + math.cos(math.radians(a)) * 44, py + math.sin(math.radians(a)) * 44)
                               for a in np.linspace(-150, -60, 10)], smooth_n=0)
    R.raise_(fan, 2.2, 2.0, mode="max")


def ysolde(R):
    """Duskwight Elezen, keeper of the dusk roads: the long ear swept back, a high cowl and long hair, a thin ring of
    moonlight held up behind her (an open ring: the ground shows through it)."""
    bust(R, y0=64, left=-64, right=56, h=2.6)
    cowl = R.poly([(-66, 110), (-62, 40), (-48, -10), (-36, -50), (-14, -66), (10, -64), (26, -52), (14, -46), (-8, -50),
                   (-28, -34), (-38, 0), (-36, 40), (-24, 64), (-40, 110)])
    R.raise_(cowl, 5.0, 5.0)
    folds(R, [[(-56, 100), (-54, 40), (-40, -10)], [(-46, 104), (-44, 50), (-30, 0)]], 2.4, 0.9)
    head = R.poly(head_profile(nose=1.05, chin=0.98, brow=0.98, skull=0.96, jaw=0.9, scale=0.95, lips=0.95))
    R.raise_(head, 5.2, 22.0)
    face_features(R, 0.95, 0, 0, eye=(26, -6), ear=(-12, 4), ear_r=(3, 5))
    ear = R.poly([(-8, 2), (-30, -10), (-52, -26), (-36, -6), (-14, 12)])
    R.raise_(ear, 5.2, 2.0)
    hair = R.poly([(-36, 50), (-40, 0), (-30, -36), (-8, -46), (14, -44), (0, -38), (-14, -24), (-20, 10), (-22, 50)])
    R.raise_(hair, 5.6, 4.0)
    hair_strands(R, hair, lambda x, y: math.pi / 2 + 0.25, 180, 16, seed=8)
    R.ridges([[(-70, -40), (-62, -70), (-36, -86), (-8, -80), (4, -60)]], 3.2, 2.0)     # the ring (its arc above her)


def ottilie(R):
    """Sister Ottilie, Midlander Hyur priestess of Menphina: a soft veil over her hair, a gentle face, a moonflower at
    her temple, the lantern staff's crook at her shoulder."""
    bust(R, y0=62, left=-64, right=58, h=2.8)
    veil = R.poly([(-68, 112), (-64, 40), (-50, -12), (-34, -44), (-10, -56), (14, -54), (28, -42), (14, -40), (-10, -42),
                   (-26, -26), (-34, 6), (-30, 50), (-20, 70), (-48, 112)])
    R.raise_(veil, 5.0, 6.0)
    folds(R, [[(-58, 100), (-58, 40), (-44, -6)], [(-46, 104), (-46, 50), (-34, 4)], [(-34, 104), (-34, 64)]], 2.6, 0.9)
    head = R.poly(head_profile(nose=0.92, chin=0.95, brow=0.97, skull=0.98, jaw=0.9, scale=0.95, lips=1.05))
    R.raise_(head, 5.2, 22.0)
    face_features(R, 0.95, 0, 0, eye=(26, -6), closed=True, ear=(-8, 6))
    hair = R.poly([(-30, 30), (-32, 0), (-24, -28), (-6, -40), (12, -40), (-2, -32), (-12, -16), (-16, 10), (-20, 30)])
    R.raise_(hair, 5.2, 3.0)
    hair_strands(R, hair, lambda x, y: math.pi / 2 + 0.6, 90, 12, seed=9)
    # the moonflower: five small petals at her temple
    fx, fy = 4, -34
    for k in range(5):
        a = math.radians(-90 + 72 * k)
        R.raise_(R.ellipse(fx + math.cos(a) * 4.6, fy + math.sin(a) * 4.6, 3.6, 3.6), 1.6, 1.4, mode="add")
    R.raise_(R.ellipse(fx, fy, 1.8, 1.8), 1.0, 0.8, mode="add")
    # the staff's crook rising behind her shoulder
    R.ridges([[(54, 118), (60, 70), (62, 20), (58, -6), (48, -16), (40, -8)]], 4.2, 2.2)


def gyobo(R):
    """Gyobo the Namazu: the broad flat catfish head, the round eye high on it, the long whiskers, a shrine
    attendant's headband, the drum's rim at his side."""
    bust(R, y0=70, left=-66, right=60, h=2.6)
    head = R.poly([(-40, 92), (-52, 60), (-56, 20), (-46, -16), (-20, -36), (14, -40), (40, -30), (56, -8), (62, 14),
                   (56, 34), (40, 46), (20, 54), (8, 70), (-6, 92)])
    R.raise_(head, 5.6, 28.0)
    # his forms (round 4): the broad skull, the brow over the eye, the full cheek, the lip over the wide mouth
    R.bump(0, -12, 30, 20, 1.6, within=head)
    R.bump(32, -22, 10, 5, 1.0, within=head)
    R.bump(36, 14, 14, 10, 1.3, within=head)
    R.bump(44, 27, 12, 3.5, 0.8, within=head)
    R.carve(R.strokes([[(20, 30), (40, 34), (58, 28)]], 1.6), 0.8, 0.4)           # the wide mouth
    R.raise_(R.ellipse(30, -14, 7, 7), 1.6, 3.0, mode="add")                      # the eye's dome
    R.carve(R.ellipse(31, -14, 3.6, 3.6), 0.9, 0.5, tone=0.8)
    R.ridges([[(54, 22), (74, 30), (88, 46), (94, 64)], [(50, 30), (66, 44), (74, 64), (72, 82)]], 2.2, 1.6)   # whiskers
    R.ridges([[(-46, -10), (-10, -32), (30, -30), (52, -18)]], 5.0, 1.4)          # the headband
    R.ridges([[(-52, -6), (-64, 10), (-66, 26)], [(-52, -4), (-70, -2)]], 3.0, 1.2)  # its tied ends
    for (x, y) in ((-30, 30), (-18, 44), (-34, 52), (-6, 16)):
        R.raise_(R.ellipse(x, y, 4.0, 3.0), 0.6, 1.4, mode="add")                 # the skin's mottling, lightly raised
    R.ridges([[(36, 112), (50, 86), (72, 80), (86, 92), (82, 112)]], 4.0, 2.0)   # the drum's rim


def aldous(R):
    """Aldous Varrow, Highlander Hyur black mage: the great pointed hat with its wide brim, his face under it, a high
    collar, a neat beard; the staff's head with its red-moon ember beside him."""
    bust(R, y0=62, left=-70, right=62, h=3.0)
    R.ridges([[(-22, 62), (-18, 44), (6, 46), (12, 62)]], 6.0, 2.2)              # the high collar
    head = R.poly(head_profile(nose=1.12, chin=1.08, brow=1.02, skull=0.98, jaw=1.08, scale=0.95, lips=0.85))
    R.raise_(head, 5.2, 22.0)
    face_features(R, 0.95, 0, 0, eye=(26, -4), ear=(-8, 6), lid=0.7)
    beard = R.poly([(10, 34), (26, 36), (34, 44), (30, 56), (16, 58), (6, 48)])
    R.raise_(beard, 5.2, 3.0)
    hair_strands(R, beard, lambda x, y: math.pi / 2 - 0.2, 70, 8, seed=10)
    brim = R.poly([(-78, -20), (-40, -30), (0, -34), (40, -30), (66, -20), (40, -14), (0, -16), (-40, -12)])
    R.raise_(brim, 6.2, 3.0)
    crown = R.poly([(-40, -26), (-30, -60), (-14, -96), (8, -116), (24, -106), (6, -92), (6, -60), (20, -28)])
    R.raise_(crown, 6.6, 6.0)
    folds(R, [[(-24, -40), (-10, -80)], [(0, -40), (4, -86)]], 2.2, 0.8)
    R.ridges([[(-38, -30), (18, -30)]], 3.0, 0.8)                                 # the hat's band
    # the staff's head and ember at the right
    R.ridges([[(74, 118), (76, 40), (78, -20)]], 4.6, 2.4)
    R.raise_(R.ellipse(78, -28, 8, 8), 4.0, 4.0, mode="add")


def ione(R):
    """Ione Selenis, Sharlayan sage: a young scholar's face, a fine circlet, round spectacles, hair gathered at the
    nape, and two nouliths poised at her shoulder."""
    bust(R, y0=62, left=-64, right=58, h=2.8)
    folds(R, [[(-48, 82), (-20, 70), (10, 72), (40, 84)]], 2.4, 0.8)
    prof = head_profile(nose=0.9, chin=0.92, brow=0.98, skull=1.0, jaw=0.88, scale=0.94, lips=0.95)
    R.raise_(R.poly(prof), 5.2, 22.0)
    face_features(R, 0.94, 0, 0, eye=(26, -6), ear=(-8, 6))
    R.ridges([[(17, -8), (22, -14), (32, -13), (35, -6), (30, 0), (20, 0), (17, -6)], [(17, -6), (-6, -4)]], 1.3, 0.9)  # spectacles
    hair_over(R, prof, 1.07, height=6.0, seed=11)
    R.raise_(R.ellipse(-46, 26, 11, 10), 7.0, 5.0)                                # the gathered knot at the nape
    hair_strands(R, R.ellipse(-46, 26, 11, 10), lambda x, y: math.atan2(y - 26, x + 46) + 1.2, 50, 8, seed=12)
    R.ridges([[(-30, -40), (-4, -48), (20, -42)]], 2.2, 1.0)                      # the circlet
    for (x, y) in ((62, 20), (78, 46)):                                           # two nouliths, slim leaf blades
        R.raise_(R.poly([(x - 4, y - 18), (x + 4, y - 18), (x + 6, y + 10), (x, y + 20), (x - 6, y + 10)]), 3.0, 2.0, mode="add")


def kupsa(R):
    """Kupsa Brightpom, moogle storm courier: the round head and its short ear, the curling stalk and pom-pom, the
    little bat wing at his back, the courier's bag strap, a spark at the pom-pom."""
    # round 4 (realism round 3): one continuous mass from the chest up through a short neck into the head, so the
    # head sits on the body, and the wing grows from the back, not the head
    body = R.poly([(-58, 118), (-60, 84), (-46, 58), (-30, 44), (0, 38), (30, 44), (44, 62), (54, 90), (52, 118)])
    R.raise_(body, 3.8, 22.0)
    hair_strands(R, body, lambda x, y: math.pi / 2, 200, 6, width=1.4, seed=13)
    head = R.ellipse(4, 6, 50, 46)
    R.raise_(head, 5.6, 26.0)
    R.bump(-6, -10, 30, 26, 1.6, within=head)                                     # the brow's swell
    R.bump(30, 20, 14, 12, 1.0, within=head)                                      # the cheek under the eye
    # soft fur: a few broad tufts toward the back of the head, the face left smooth
    face = R.ellipse(30, 14, 26, 24)
    hair_strands(R, head * (1 - face), lambda x, y: math.atan2(y - 6, x - 4), 40, 9, width=4.0, h=0.7, seed=24, soft=1.0)
    hair_strands(R, head * (1 - face), lambda x, y: math.atan2(y - 6, x - 4), 200, 6, width=1.2, h=0.25, seed=14)
    ear = R.poly([(-18, -26), (-28, -56), (-6, -44), (2, -30)])
    R.raise_(ear, 5.0, 3.0)
    R.bump(48, 20, 9, 8, 2.6)                                                     # the round nose
    R.bump(26, 2, 7, 8, -1.4)                                                     # the eye's socket
    R.carve(R.ellipse(27, 3, 3.6, 5.0), 1.2, 0.4, tone=0.9)                       # the eye
    R.bump(25.6, 1.4, 1.2, 1.6, 0.6)                                              # its glint of a raised pupil
    R.carve(R.strokes([[(34, 36), (42, 39), (49, 36)]], 1.0), 0.6, 0.3)           # the mouth
    R.ridges([[(-6, -34), (-2, -62), (14, -80), (34, -86)]], 2.4, 1.4)            # the stalk
    pom = R.ellipse(42, -88, 12, 12)
    R.raise_(pom, 6.4, 8.0)
    hair_strands(R, pom, lambda x, y: math.atan2(y + 88, x - 42), 120, 4, width=1.2, h=0.4, seed=15)
    wing = R.poly([(-50, 66), (-70, 30), (-86, 6), (-80, 34), (-94, 40), (-80, 56), (-90, 68), (-66, 76)])
    R.raise_(wing, 2.6, 2.0)
    R.ridges([[(-52, 66), (-84, 10)], [(-54, 68), (-90, 42)], [(-56, 70), (-86, 66)]], 1.4, 0.6)
    R.ridges([[(-40, 70), (0, 96), (40, 112)]], 3.4, 1.0)                          # the bag's strap


CAST = [
    ("pipiru", "Pipiru Mimiru", "Lalafell · astrologian apprentice", "Super Guide", pipiru),
    ("kaede", "Kaede Tsukiyo", "Raen Au Ra · dancer", "Multiball", kaede),
    ("marcia", "Marcia nan Arcus", "Garlean · engineer, retired from the legion", "Brass Wings", marcia),
    ("haldbrand", "Haldbrand Tidewatch", "Roegadyn · gunbreaker of the Sea Wolves", "Lunar Burst", haldbrand),
    ("gajavati", "Gajavati", "Arkasodara · ferry-trader from Thavnair", "Flippers", gajavati),
    ("ysolde", "Ysolde Nocturine", "Duskwight Elezen · keeper of the dusk roads", "Moon Gate", ysolde),
    ("ottilie", "Sister Ottilie", "Midlander Hyur · priestess of Menphina", "Moonbloom", ottilie),
    ("gyobo", "Gyobo", "Namazu · shrine attendant at the moon-viewing", "Moon-Viewing Draw", gyobo),
    ("aldous", "Aldous Varrow", "Highlander Hyur · black mage", "Fireball", aldous),
    ("ione", "Ione Selenis", "Sharlayan · sage", "Sage's Path", ione),
    ("kupsa", "Kupsa Brightpom", "Moogle · storm courier", "Storm Post", kupsa),
]

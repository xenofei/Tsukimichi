"""The eleven Moonfall characters as silhouette sketches (plan v9 G5, decision 1).

Each figure is built from a few primitives in figure units (feet at the origin, y up negative, a Hyur about 100 tall),
so relative heights hold across the line-up. Three layers: `body` (the silhouette), `accent` (a dim coloured part:
brass plates, fans, a drum) and `glow` (the one emissive prop, if any). Lighting, for every figure: backlit by the
moon high at the upper left, so the silhouette is dark with a cool rim on its upper-left edges and a short soft
shadow toward the viewer and right; an emissive prop adds its own small light, which rims only the edges facing it.
"""
import math

import numpy as np
from PIL import Image, ImageDraw

from mf_lib import Img, blur, hexc, screen, smooth

SS = 4  # supersampling for the pen


class Pen:
    def __init__(self, w, h, ox, oy, s):
        self.w, self.h, self.ox, self.oy, self.s = w, h, ox, oy, s
        self.layers = {k: Image.new("L", (w, h), 0) for k in ("body", "accent", "glow")}
        self.d = {k: ImageDraw.Draw(v) for k, v in self.layers.items()}
        self.lights = []

    def P(self, x, y):
        return (self.ox + x * self.s, self.oy + y * self.s)

    def ell(self, cx, cy, rx, ry, layer="body", fill=255, rot=0.0):
        if rot:
            pts = [(cx + rx * math.cos(t) * math.cos(rot) - ry * math.sin(t) * math.sin(rot),
                    cy + rx * math.cos(t) * math.sin(rot) + ry * math.sin(t) * math.cos(rot)) for t in np.linspace(0, 2 * math.pi, 48)]
            self.poly(pts, layer, fill)
            return
        (x0, y0), (x1, y1) = self.P(cx - rx, cy - ry), self.P(cx + rx, cy + ry)
        self.d[layer].ellipse([x0, y0, x1, y1], fill=fill)

    def poly(self, pts, layer="body", fill=255):
        self.d[layer].polygon([self.P(x, y) for x, y in pts], fill=fill)

    def line(self, pts, w, layer="body", fill=255, w1=None):
        """A stroke through pts, tapering from w to w1, with round joints."""
        w1 = w if w1 is None else w1
        dense = []
        for (a, b) in zip(pts[:-1], pts[1:]):
            n = max(2, int(math.hypot(b[0] - a[0], b[1] - a[1]) * self.s / 2))
            for t in np.linspace(0, 1, n, endpoint=False):
                dense.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
        dense.append(pts[-1])
        for i, (x, y) in enumerate(dense):
            r = (w + (w1 - w) * i / max(1, len(dense) - 1)) / 2
            self.ell(x, y, r, r, layer, fill)

    def curve(self, p0, p1, p2, p3, w0, w1, layer="body", fill=255, n=40):
        pts = []
        for t in np.linspace(0, 1, n):
            mt = 1 - t
            pts.append((mt ** 3 * p0[0] + 3 * mt * mt * t * p1[0] + 3 * mt * t * t * p2[0] + t ** 3 * p3[0],
                        mt ** 3 * p0[1] + 3 * mt * mt * t * p1[1] + 3 * mt * t * t * p2[1] + t ** 3 * p3[1]))
        self.line(pts, w0, layer, fill, w1)

    def ring(self, cx, cy, r, w, layer="body", ry=None, rot=0.0):
        ry = r if ry is None else ry
        pts = [(cx + r * math.cos(t) * math.cos(rot) - ry * math.sin(t) * math.sin(rot),
                cy + r * math.cos(t) * math.sin(rot) + ry * math.sin(t) * math.cos(rot)) for t in np.linspace(0, 2 * math.pi, 90)]
        self.line(pts, w, layer)

    def rect(self, cx, cy, w, h, rot=0.0, layer="body", fill=255):
        c, s = math.cos(rot), math.sin(rot)
        pts = [(cx + dx * c - dy * s, cy + dx * s + dy * c) for dx, dy in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2))]
        self.poly(pts, layer, fill)

    def light(self, x, y, col, radius, k=1.0):
        self.lights.append((x, y, col, radius, k))


# ------------------------------------------------------------------------------------------------ the eleven
def pipiru(p):
    """Lalafell astrologian apprentice: a long robe, a deep round hood with one soft peak, a star globe raised in an
    armillary ring, a card at her side."""
    p.ell(-5, -1.5, 4.8, 2.2); p.ell(5.5, -1.5, 4.8, 2.2)
    p.poly([(-14.5, -2.5), (14.5, -2.5), (10.5, -14), (7, -25), (-7, -25), (-10.5, -14)])
    p.ell(0, -34, 12.5, 11.5)
    p.poly([(-6.5, -43), (-1, -50), (2, -50.5), (6.5, -43)])                          # the hood's one soft peak
    p.ell(0.5, -50, 1.8, 1.8)
    p.line([(-6, -26), (-12, -37), (-15.5, -46)], 3.6, w1=3.0)
    p.line([(6, -24), (12, -18)], 3.2)
    p.rect(14.5, -16, 4.2, 6.2, rot=0.4, layer="accent")                             # a card at her side
    p.ring(-16.5, -54, 10.0, 1.0, ry=3.4, rot=-0.35)                                # the armillary's two rings
    p.ring(-16.5, -54, 10.0, 1.0, ry=3.4, rot=1.20)
    p.ell(-16.5, -54, 6.2, 6.2, layer="glow")
    p.light(-16.5, -54, "#DCE6FF", 26, 1.0)


def kaede(p):
    """Raen Au Ra dancer: horns sweeping back, a ponytail, the scaled tail, one leg drawn up, two chakrams."""
    p.curve((-6, -40), (-22, -34), (-24, -16), (-34, -3), 6.5, 1.2)                   # the tail
    p.poly([(-1, -1), (5, -1), (4.5, -24), (3, -46), (-3, -46), (-2, -24)])         # the standing leg
    p.ell(4, -1.6, 6.5, 2.0)
    p.line([(-1, -44), (-12, -37), (-4, -29)], 6, w1=4.6)                            # the drawn-up leg
    p.poly([(-9, -44), (9, -44), (14, -37), (17, -30), (-15, -31), (-13, -38)])     # a short flared skirt
    p.poly([(-7, -45), (7, -45), (8.5, -62), (-8.5, -62)])
    p.ell(0, -63, 10.5, 3.6)
    p.line([(0, -63), (0, -69)], 4.2)
    p.ell(0, -75, 6.4, 7.4)
    p.curve((-4.5, -79), (-9, -86), (-14, -87), (-15.5, -80), 2.8, 0.8)            # horns
    p.curve((4.5, -79), (9, -86), (14, -87), (15.5, -80), 2.8, 0.8)
    p.curve((-1, -81), (12, -82), (18, -70), (17, -58), 4.4, 1.0)                    # ponytail
    p.line([(7, -61), (17, -60.5), (26, -66)], 3.0)
    p.line([(-7, -61), (-11, -72), (-8.5, -83)], 3.0)
    for (cx, cy) in ((31, -68.5), (-7, -91)):                                        # the chakrams: plain bladed rings
        p.ring(cx, cy, 7.8, 2.4)
    p.curve((26, -66), (30, -60), (35, -56), (42, -57), 1.6, 0.4)                    # sleeve ribbons
    p.curve((-8.5, -83), (-14, -78), (-18, -74), (-24, -75), 1.6, 0.4)


def marcia(p):
    """Garlean engineer: a long split coat, goggles pushed up, a big spanner on her shoulder, folded brass vanes on
    her back (the plates that unfold into the bucket's wings)."""
    for (sx, ang0) in ((-1, 118), (1, 62)):
        for j, da in enumerate((-12, 0, 12)):
            a = math.radians(-(ang0 + sx * da))
            bx, by = sx * 4, -56
            tx, ty = bx + math.cos(a) * 17, by + math.sin(a) * 17
            nx, ny = -math.sin(a) * 1.7, math.cos(a) * 1.7
            p.poly([(bx - nx, by - ny), (bx + nx, by + ny), (tx + nx * 0.7, ty + ny * 0.7), (tx - nx * 0.7, ty - ny * 0.7)], layer="accent")
    p.ell(-6, -2, 6.4, 2.6); p.ell(7, -2, 6.4, 2.6)
    p.poly([(-10, -3), (-2, -3), (-1.5, -40), (-11, -40)]); p.poly([(2, -3), (11, -3), (10.5, -40), (1, -40)])
    p.poly([(-11, -40), (11, -40), (11.5, -61), (-11.5, -61)])
    p.poly([(-16, -19), (-12, -60), (-4, -60), (-3, -38), (-8, -22)])               # coat, left panel
    p.poly([(16, -19), (12, -60), (4, -60), (3, -38), (8, -22)])                    # coat, right panel
    p.ell(-11.5, -60, 5.2, 4.2); p.ell(11.5, -60, 5.2, 4.2)
    p.line([(0, -60), (0, -66)], 4.6)
    p.ell(0, -72.5, 6.6, 7.6)
    p.ell(0, -74.5, 7.8, 6.6)                                                        # a short bob
    p.ell(0, -79.5, 7.4, 2.4)                                                        # goggles on the brow
    p.line([(-11.5, -59), (-18, -49), (-10.5, -43)], 3.3)                            # hand on hip
    p.line([(11.5, -59), (17, -50), (15.5, -64)], 3.3)
    p.line([(10, -56), (21, -84)], 2.6)                                              # the spanner's shaft
    p.ell(22, -86.5, 5.0, 5.0)
    p.poly([(22, -86.5), (19.5, -93), (26, -91)], fill=0)                            # its open jaw


def haldbrand(p):
    """Roegadyn gunbreaker: broad, a fur collar, a beard and top-knot, the gunblade resting on his shoulder."""
    a = math.atan2(-128 + 70, -22 - 18)
    c_, s_ = math.cos(a), math.sin(a)
    hx, hy, tx, ty = 15, -74, -21, -126
    p.poly([(hx - s_ * 4.5, hy + c_ * 4.5), (tx - s_ * 2.0, ty + c_ * 2.0), (tx + c_ * 6, ty + s_ * 6), (tx + s_ * 2.0, ty - c_ * 2.0), (hx + s_ * 4.5, hy - c_ * 4.5)])   # the blade
    p.rect(14, -75, 11, 9, rot=a, layer="body")                                      # the revolver block
    p.rect(17, -71, 3, 12, rot=a + math.pi / 2, layer="body")                         # the guard
    p.line([(19, -66), (23.5, -59)], 3.2)                                            # the grip
    p.ell(-10, -2.6, 8.2, 3.0); p.ell(11, -2.6, 8.2, 3.0)
    p.poly([(-17, -3), (-4, -3), (-3, -48), (-17.5, -48)]); p.poly([(4, -3), (18, -3), (17.5, -48), (3, -48)])
    p.poly([(-22, -30), (22, -30), (19, -52), (-19, -52)])
    p.poly([(-20, -50), (20, -50), (25.5, -86), (-25.5, -86)])
    p.ell(-24.5, -86, 10, 5.6); p.ell(24.5, -86, 10, 5.6)                             # pauldrons
    p.ell(0, -90, 17, 7.2)                                                           # fur collar
    p.ell(0, -100, 8.2, 9.6)
    p.poly([(-6, -97), (6, -97), (4, -87), (0, -84), (-4, -87)])                     # beard
    p.ell(1.5, -111, 4.2, 3.2)                                                       # top-knot
    p.line([(-26, -84), (-30.5, -63), (-28.5, -47)], 7.0, w1=6.0)
    p.ell(-28.5, -44.5, 4.8, 4.8)
    p.line([(26, -84), (32, -71), (21, -65)], 7.0, w1=6.0)


def gajavati(p):
    """Arkasodara ferry-trader: round and broad, great ears, a curled trunk, a head-wrap, a festival fan in each hand."""
    for sx in (-1, 1):
        cx, cy = sx * 41, -73
        p.d["accent"].pieslice([*p.P(cx - 17, cy - 17), *p.P(cx + 17, cy + 17)], start=(200 if sx < 0 else -20) - 70 * 0, end=(340 if sx < 0 else 160), fill=255)
        p.line([(sx * 37, -68), (sx * 41, -73)], 1.6)
    p.ell(-10, -3, 8.4, 3.6); p.ell(10, -3, 8.4, 3.6)
    p.poly([(-16.5, -3), (-4, -3), (-4, -25), (-16.5, -25)]); p.poly([(4, -3), (16.5, -3), (16.5, -25), (4, -25)])
    p.ell(0, -47, 23.5, 27)
    p.poly([(-21, -20), (21, -20), (23.5, -31), (-23.5, -31)])
    p.ell(-17, -81, 11, 15, rot=0.30); p.ell(17, -81, 11, 15, rot=-0.30)             # ears
    p.ell(0, -82, 13, 13)
    p.ell(0, -92.5, 12, 4.6)                                                         # head-wrap
    p.curve((0, -78), (1, -96), (6, -110), (13, -110), 6.0, 2.6)                     # the trunk, raised for luck
    p.curve((13, -110), (17, -110), (18, -105), (15, -103.5), 2.6, 1.8)
    p.curve((-4, -73), (-6, -70), (-8, -67), (-9, -66), 2.2, 0.8)
    p.curve((4, -73), (6, -70), (8, -67), (9, -66), 2.2, 0.8)
    p.line([(-20, -60), (-31, -62), (-37, -68)], 6.0, w1=5.0)
    p.line([(20, -60), (31, -62), (37, -68)], 6.0, w1=5.0)


def ysolde(p):
    """Duskwight Elezen: very tall and slim, long swept ears, a cloak with a ragged hem, a ring of moonlight held up."""
    p.poly([(-9, -88), (9, -88), (13, -50), (18, -4), (12.5, -1), (8, -5), (3, 0), (-2, -4.5), (-7, 0), (-12, -3), (-15.5, 0), (-14, -42)])
    p.poly([(-5, -96), (5, -96), (6, -78), (-6, -78)])                               # hair falling to the shoulders
    p.line([(0, -88), (0, -92)], 3.0)
    p.ell(0, -97.5, 5.4, 7.0)
    p.curve((-4, -99.5), (-9, -101.5), (-13, -104), (-18.5, -108), 3.4, 0.7)          # ears, long and swept
    p.curve((4, -99.5), (9, -101.5), (13, -104), (18.5, -108), 3.4, 0.7)
    p.line([(8, -86), (17, -91), (22.5, -104)], 2.6)
    p.line([(-8, -86), (-13, -67), (-10, -56)], 2.6)
    p.ring(31, -114, 15.5, 2.3)                                                      # the ring
    p.ring(31, -114, 14.0, 1.0, layer="glow")
    p.light(31, -114, "#B9B4F0", 22, 0.40)


def ottilie(p):
    """Midlander Hyur priestess of Menphina: robe and veil, a crescent circlet, a crescent-headed staff with a small
    lantern (the card's one warm light), moonflowers at her breast."""
    p.line([(17, -1), (17, -106)], 2.3)                                              # the staff
    p.ell(17, -110, 7.0, 6.0)
    p.ell(17, -113.2, 6.6, 5.6, fill=0)                                              # the finial: a bowl, horns up
    p.line([(17, -100), (23.5, -100)], 1.0)                                          # a side hook
    p.line([(23.5, -100), (23.5, -97.5)], 0.7)
    p.rect(23.5, -94.5, 3.8, 5.6, layer="glow")                                      # the lantern
    p.light(23.5, -94.5, "#FFC27A", 34, 1.0)
    p.poly([(-16, -1), (16, -1), (12, -40), (9.5, -62), (-9.5, -62), (-12, -40)])
    p.poly([(-9, -61), (-18, -44), (-24, -40), (-14, -40), (-11, -50)])             # a bell sleeve
    p.poly([(-7.5, -77), (7.5, -77), (12, -56), (-12, -56)])                         # veil
    p.ell(0, -71.5, 6.0, 7.6)
    p.line([(9, -60), (14, -50), (16.5, -60)], 3.0)
    p.ring(0, -79.5, 3.2, 1.0, layer="accent")                                       # the circlet's crescent
    p.ell(1.6, -80.5, 2.6, 2.6, layer="accent", fill=0)
    for (fx, fy, r) in ((-3, -53, 2.0), (0, -55.5, 1.8), (-5.5, -55.5, 1.6), (-1.5, -50.5, 1.5), (2.5, -52, 1.4)):
        p.ell(fx, fy, r, r, layer="accent")


def gyobo(p):
    """Namazu shrine attendant: a round catfish body, a wide flat head, long barbels, a headband, a gourd, and the
    shrine's lottery drum on its stand beside him."""
    p.ell(-14, -31, 6, 8, layer="accent"); p.ell(-14, -41, 4, 4.4, layer="accent")   # the gourd
    p.ell(-8, -1.6, 6.2, 2.2); p.ell(8, -1.6, 6.2, 2.2)
    p.ell(0, -20, 17, 19)
    p.ell(0, -41, 19.5, 12)
    p.curve((-12, -37), (-22, -36), (-28, -30), (-29, -19), 2.0, 0.6)                # barbels
    p.curve((12, -37), (22, -36), (28, -30), (29, -19), 2.0, 0.6)
    p.ell(15, -49, 3.0, 2.4)                                                         # headband knot and tails
    p.curve((16, -49), (20, -53), (23, -52), (26, -55), 1.6, 0.6)
    p.line([(14, -24), (24, -25)], 4.2, w1=3.4)
    # the lottery drum: a hexagonal drum on a stand, a crank, a ball in its tray
    cx, cy = 41, -27
    p.poly([(cx + 12 * math.cos(k * math.pi / 3), cy + 12 * math.sin(k * math.pi / 3)) for k in range(6)], layer="accent")
    p.line([(33, -17), (30, 0)], 2.0); p.line([(49, -17), (52, 0)], 2.0)
    p.line([(cx, cy), (24, -25)], 1.4)
    p.rect(31, -8, 10, 2, layer="body")
    p.ell(30, -11, 2.1, 2.1, layer="accent")


def aldous(p):
    """Highlander black mage: a high collar, a long coat, the wide crooked hat, a staff whose head holds a coal of
    red-moon fire (the card's one warm light)."""
    p.line([(-21, -1), (-22.5, -104)], 2.4)                                          # the staff
    p.curve((-22.5, -104), (-23, -110), (-17, -113), (-18, -107), 2.4, 1.6)
    p.ell(-21.5, -111, 2.8, 2.8, layer="glow")
    p.light(-21.5, -111, "#FF7A50", 30, 1.0)
    p.poly([(-17, -1), (17, -1), (13, -40), (11.5, -66), (-11.5, -66), (-13, -40)])
    p.ell(0, -66, 13.5, 5)
    p.poly([(-8.5, -66), (8.5, -66), (10.5, -80), (-10.5, -80)])                     # the high collar
    p.ell(0, -76, 7, 7.5)
    p.ell(0, -82, 22.5, 4.6)                                                         # the brim
    p.poly([(-10.5, -84), (10.5, -84), (6.5, -100), (12, -114), (15, -118), (3, -107.5), (-3.5, -95)])
    p.line([(-11, -64), (-18.5, -55), (-21.5, -68)], 3.6)
    p.poly([(11, -64), (17, -40), (12, -36), (9, -50)])                              # the right sleeve, hanging


def ione(p):
    """Sharlayan sage: a slim long-tailed scholar's coat, a book at her hip, a hand raised, four nouliths around her."""
    for (nx, ny, a) in ((-24, -80, -0.5), (25, -86, 0.7), (35, -61, 1.4), (29, -37, 2.2)):
        c, s = math.cos(a), math.sin(a)
        p.poly([(nx - 6 * s, ny + 6 * c), (nx + 1.8 * c, ny + 1.8 * s), (nx + 6 * s, ny - 6 * c), (nx - 1.8 * c, ny - 1.8 * s)], layer="accent")
    p.ell(-4, -1.2, 4.6, 1.8); p.ell(5, -1.2, 4.6, 1.8)
    p.poly([(-6.5, -1), (-1.2, -1), (-1, -37), (-7, -37)]); p.poly([(1.2, -1), (6.5, -1), (7, -37), (1, -37)])
    p.poly([(-9, -37), (9, -37), (16, -12), (12, -13.5), (7.5, -29), (-7.5, -29), (-12, -13.5), (-16, -12)])
    p.poly([(-8.5, -36), (8.5, -36), (9.5, -58), (-9.5, -58)])
    p.poly([(-11.5, -58), (11.5, -58), (13.5, -50), (-13.5, -50)])
    p.line([(0, -58), (0, -61)], 3.2)
    p.ell(0, -66.5, 6.0, 7.0)
    p.ell(2.5, -73, 3.6, 3.0)
    p.line([(-9, -56), (-13, -46), (-9.5, -40.5)], 3.0)
    p.rect(-12, -41, 7.5, 10, rot=0.25)
    p.line([(9, -56), (17, -57), (24, -63.5)], 3.0)


def kupsa(p):
    """Moogle storm-courier: a big round head, little bat wings, a mailbag, and the pom-pom crackling with a bolt."""
    for sx in (-1, 1):
        p.poly([(sx * 8, -18), (sx * 22, -29), (sx * 20, -21), (sx * 24.5, -18.5), (sx * 17, -14.5), (sx * 19, -10.5), (sx * 9, -10.5)])
    p.ell(-5, -1.5, 4.2, 2.0); p.ell(5, -1.5, 4.2, 2.0)
    p.ell(0, -12, 10.5, 11)
    p.ell(0, -28, 12.5, 10.5)
    p.poly([(-10, -33), (-13.5, -41), (-6, -36)]); p.poly([(10, -33), (13.5, -41), (6, -36)])
    p.curve((0, -38), (0.5, -43), (2, -46), (3.5, -48), 1.0, 0.9)
    p.ell(4, -51, 4.0, 4.0, layer="glow")
    p.line([(7, -54), (11.5, -59.5), (9.6, -60.5), (15, -67)], 1.2, layer="glow", w1=0.6)
    p.light(4, -51, "#E4DCFF", 26, 0.9)
    p.line([(-8, -20), (8, -6)], 1.2)
    p.ell(9.5, -6.5, 6, 4.6, layer="accent")


FIGS = {"pipiru": pipiru, "kaede": kaede, "marcia": marcia, "haldbrand": haldbrand, "gajavati": gajavati,
        "ysolde": ysolde, "ottilie": ottilie, "gyobo": gyobo, "aldous": aldous, "ione": ione, "kupsa": kupsa}
ACCENT = {"pipiru": "#3A4C84", "kaede": "#3A4C84", "marcia": "#6A5530", "haldbrand": "#3A4C84", "gajavati": "#3E3466",
          "ysolde": "#3A4C84", "ottilie": "#8A94B8", "gyobo": "#4E3020", "aldous": "#3A4C84", "ione": "#4A5C8E", "kupsa": "#4A3A2E"}
RIM = "#B8C6EE"
BODY = "#0C1124"


def masks(name, k, S):
    """Renders a figure's layers at device scale k*S px per figure unit (feet at the window's (W/2, H-pad))."""
    s = k * S * SS
    W, H = int(180 * s), int(180 * s)
    ox, oy = W / 2, H - 34 * s
    p = Pen(W, H, ox, oy, s)
    FIGS[name](p)
    out = {}
    for key, im in p.layers.items():
        small = im.resize((W // SS, H // SS), Image.BOX)
        out[key] = np.asarray(small, np.float32) / 255
    return out, p.lights, (ox / SS, oy / SS)


def draw_figure(img, name, fx, fy, k=2.0, ground_y=None, shadow=True, clip=None):
    """Composites figure `name` with its feet at game point (fx, fy), k device-units per figure unit."""
    S = img.S
    M, lights, (ox, oy) = masks(name, k, S)
    hh, ww = M["body"].shape
    x0, y0 = int(round(fx * S - ox)), int(round(fy * S - oy))
    X0, Y0, X1, Y1 = max(0, x0), max(0, y0), min(img.w, x0 + ww), min(img.h, y0 + hh)
    sl = (slice(Y0, Y1), slice(X0, X1))
    cut = lambda a: a[Y0 - y0:Y1 - y0, X0 - x0:X1 - x0]
    body, acc, glow = cut(M["body"]), cut(M["accent"]), cut(M["glow"])
    yy, xx = np.mgrid[Y0:Y1, X0:X1].astype(np.float32)
    c = clip((xx + 0.5) / S, (yy + 0.5) / S) if clip is not None else np.ones_like(body)
    body, acc, glow = body * c, acc * c, glow * c
    sil = np.maximum(body, acc)
    if shadow:
        # the shadow: the silhouette laid on the ground toward the viewer and right (light behind, upper left)
        fl = sil[::-1, :]
        hrows = sil.shape[0]
        feet_row = int(fy * S) - Y0
        sh = np.zeros_like(sil)
        for r in range(hrows):
            src = feet_row - int((r - feet_row) / 0.20) if r >= feet_row else -1
            if 0 <= src < hrows:
                shift = int((r - feet_row) * 1.6)
                row = sil[src]
                sh[r] = np.roll(row, shift)
        sh = blur(sh, 1.6 * S) * np.exp(-np.clip(yy - fy * S, 0, None) / (14 * S))
        img.mul(sl, hexc("#04060E"), sh * 0.65 * c)
    yrel = np.clip((fy * S - yy) / (120 * k * S / 2.0), 0, 1)
    col = np.stack([np.full(sil.shape, v, np.float32) for v in hexc(BODY)], -1)
    col = col * (1.0 + 0.25 * (1 - yrel))[..., None]
    img.over(sl, col, sil)
    img.over(sl, hexc(ACCENT[name]), acc * 0.9)
    kk = max(1, int(round(0.8 * S)))
    rim = np.clip(sil - np.roll(np.roll(sil, kk, 0), kk, 1), 0, 1)
    soft = blur(rim, 1.2 * S) * sil
    img.add(sl, hexc(RIM), rim * 0.62 + soft * 0.30)
    # the emissive prop: its colour, a halo, and its light on the silhouette's edges that face it
    for (lx, ly, lc, rad, kk2) in lights:
        gx, gy = fx * S + lx * k * S, fy * S + ly * k * S
        d = np.sqrt((xx - gx) ** 2 + (yy - gy) ** 2) / (k * S)
        img.add(sl, hexc(lc), (np.exp(-(d / (rad * 0.32)) ** 2) * 0.55 * kk2 + np.exp(-(d / rad) ** 2) * 0.12 * kk2) * c)
        bs = blur(sil, 1.2 * S)
        gy_, gx_ = np.gradient(bs)
        nrm = np.sqrt(gx_ ** 2 + gy_ ** 2) + 1e-6
        tx, ty = (gx - xx), (gy - yy)
        tn = np.sqrt(tx ** 2 + ty ** 2) + 1e-6
        facing = np.clip(-(gx_ * tx + gy_ * ty) / (nrm * tn), 0, 1)
        edge = np.clip((1 - bs) * sil * 3, 0, 1)
        img.add(sl, hexc(lc), edge * facing * np.exp(-(d / (rad * 0.8)) ** 2) * 0.9 * kk2)
    gcol = {}
    for (lx, ly, lc, rad, kk2) in lights:
        gcol = hexc(lc)
    if lights:
        img.over(sl, screen(gcol * 0.85, np.full(3, 0.25, np.float32)), glow)
    return sl

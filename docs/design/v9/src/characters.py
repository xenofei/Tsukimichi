"""Moonfall's eleven characters (plan v9 G5, decision 1): characters.png (1960 x 1100) and @2x.

One card per power: the silhouette sketch on a night card (backlit by the moon at the upper left, so every figure has
its rim on the upper-left edges and its shadow toward the viewer and right), then the name, who they are, a line of
personality and how their power reads on the board. All on one shared scale, so the line-up shows true heights.
Run: py -3 characters.py
"""
import numpy as np

from figures import draw_figure
from mf_lib import P, V9, Img, blur, draw_brass, fbm, hexc, ramp, sd_rrect, text, wrap

CHARS = [
    ("pipiru", "Super Guide", "Pipiru Mimiru", "Lalafell · astrologian apprentice",
     "Counts every star twice and tells you the path before you ask.",
     "Her star globe draws the guide on: silver dots to the first peg, then a fine line through the bounce. Three shots."),
    ("kaede", "Multiball", "Kaede Tsukiyo", "Raen Au Ra · dancer",
     "Calm on the outside, competitive underneath; never misses a beat.",
     "Her second chakram leaves her hand: a twin ball springs from the green peg with a short crescent trail."),
    ("marcia", "Pyramid", "Marcia nan Arcus", "Garlean · engineer, retired from the legion",
     "Left the legions to mend things instead; fussy about rivets.",
     "The brass vanes on her back unfold on the bucket as two wings, widening it. Five turns."),
    ("haldbrand", "Space Blast", "Haldbrand Tidewatch", "Roegadyn · gunbreaker of the Sea Wolves",
     "A gentle giant who raises his voice only to fire.",
     "A lunar cartridge bursts at the green peg: one ring of pale light lights every peg within it."),
    ("gajavati", "Flippers", "Gajavati", "Arkasodara · ferry-trader from Thavnair",
     "Thavnair's cheeriest trader; swats bad luck away with festival fans.",
     "Two fans fold out at the foot's corners; click and they flick the ball back up."),
    ("ysolde", "Spooky Ball", "Ysolde Nocturine", "Duskwight Elezen · keeper of the dusk roads",
     "Dry, patient, and opens doors nobody else can see.",
     "A ring of moonlight opens under the board; the ball falls through and drops back in from a ring at the top."),
    ("ottilie", "Flower Power", "Sister Ottilie", "Midlander Hyur · priestess of Menphina",
     "Kind, unhurried, and impossible to argue with.",
     "Moonflower petals drift from the green peg to the nearest oranges and light them."),
    ("gyobo", "Lucky Spin", "Gyobo", "Namazu · shrine attendant at the moon-viewing",
     "Flustered, earnest, and sure the drum is never rigged.",
     "The shrine's drum turns once and drops a coloured ball: a free ball, a triple score or another power."),
    ("aldous", "Fireball", "Aldous Varrow", "Highlander Hyur · black mage",
     "Studied the red moon's fall for thirty years; very polite, very loud spells.",
     "The ball wears a red-moon ember with a short tail and burns straight through the pegs it meets."),
    ("ione", "Zen Ball", "Ione Selenis", "Sharlayan · sage",
     "Has already worked out your shot, and is sorry about it.",
     "Four nouliths ride beside the ball and nudge it onto the best path, with faint blue arcs."),
    ("kupsa", "Electrobolt", "Kupsa Brightpom", "Moogle · storm courier",
     "Delivers the bolt, kupo, and signs for nothing.",
     "His pom-pom crackles: a bolt jumps from the first peg hit to the bucket, lighting the pegs along it."),
]


def card(img, x0, y0, w, fig_h, name, power, cname, who, line, reads, k):
    x1, y1 = x0 + w, y0 + fig_h
    sl, xx, yy = img.win((x0 + x1) / 2, (y0 + y1) / 2, max(w, fig_h) / 2 + 2)
    m = img.cov(sd_rrect(xx, yy, x0, y0, x1, y1, 8))
    ground = y0 + fig_h * 0.84
    t = np.clip((yy - y0) / (ground - y0), 0, 1)
    sky = ramp(t, [(0, "#0A1028"), (0.6, "#16204A"), (1, "#26356C")])
    # the moon's glow from beyond the card's upper left
    d = np.sqrt((xx - (x0 - 40)) ** 2 + (yy - (y0 - 50)) ** 2)
    sky = sky + hexc("#9FB2E6") * (np.exp(-(d / 260) ** 2) * 0.18)[..., None]
    gnd = ramp(np.clip((yy - ground) / (y1 - ground), 0, 1), [(0, "#1A2448"), (1, "#0B1022")])
    gnd = gnd * (0.94 + 0.08 * fbm(gnd.shape[0], gnd.shape[1], 10 * img.S, 3, 3))[..., None]
    col = np.where((yy < ground)[..., None], sky, gnd)
    img.over(sl, col, m)
    img.add(sl, hexc("#7F92C6"), np.exp(-((yy - ground) / 2.2) ** 2) * 0.22 * m)               # the horizon's thin light
    clip = lambda X, Y: img.cov(sd_rrect(X, Y, x0 + 1, y0 + 1, x1 - 1, y1 - 1, 7))
    draw_figure(img, name, (x0 + x1) / 2, ground + 6, k=k, clip=clip)
    edge = lambda X, Y: np.abs(sd_rrect(X, Y, x0, y0, x1, y1, 8)) - 0.8
    draw_brass(img, edge, ((x0 + x1) / 2, (y0 + y1) / 2, max(w, fig_h) / 2 + 3), "round", depth=0.8, width=0.8)
    # the power plate on the card's top edge
    text(img, x0 + 12, y0 + 18, power.upper(), "ui_sb", 9.5, P["gilt_high"], anchor="lm", halo=0.6, tracking=1.0)
    ty = y1 + 26
    text(img, x0 + 2, ty, cname, "serif", 17, P["cream"], anchor="ls", halo=0)
    text(img, x0 + 2, ty + 18, who, "ui", 10.5, P["gilt"], anchor="ls", halo=0)
    yy_ = ty + 40
    for ln in wrap(line, "serif_i", 11.5, w - 4):
        text(img, x0 + 2, yy_, ln, "serif_i", 11.5, P["cream"], anchor="ls", halo=0)
        yy_ += 16
    yy_ += 6
    for ln in wrap(reads, "ui", 10.5, w - 4):
        text(img, x0 + 2, yy_, ln, "ui", 10.5, P["ink_dim"], anchor="ls", halo=0)
        yy_ += 15


def build(S=2):
    W, H = 1960, 1100
    img = Img(W * S, H * S, S)
    sl, xx, yy = img.full()
    img.px[sl] = np.asarray(hexc(P["status_foot"]))
    text(img, 40, 44, "Moonfall · the eleven who carry the powers", "serif", 26, P["cream"], anchor="ls", halo=0)
    text(img, 40, 68, "Original Eorzean characters, moon-themed, drawn to one scale (a Hyur stands about 100 units). Silhouette sketches: the poses and props that must read at medallion size.",
         "ui", 12.5, P["ink_dim"], anchor="ls", halo=0)
    w, gap, fig_h = 290, 22, 300
    k = 1.62
    for i, (name, power, cname, who, line, reads) in enumerate(CHARS):
        row, col = divmod(i, 6)
        x0 = 40 + col * (w + gap) + (0 if row == 0 else (w + gap) / 2)
        y0 = 96 + row * 500
        card(img, x0, y0, w, fig_h, name, power, cname, who, line, reads, k)
    return img


if __name__ == "__main__":
    img = build()
    img.save(V9 / "characters@2x.png")
    img.save(V9 / "characters.png", (1960, 1100))
    print("characters")

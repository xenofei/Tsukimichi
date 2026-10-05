"""Moonfall's screen kit (the rich pass): enamel panels with guilloche, gilt rules, brass buttons, tabs, plates and
the cameo badge, all under the one light from the upper left. Units are screen px at 1x (1280 x 800 or 640 x 480);
S is device px per unit. Fonts: the mocks use Perpetua Titling for the logotype and display capitals, Georgia for
serif titles and Segoe UI for labels, as stand-ins (spec-rich.md, 'Fonts').
"""
import math

import numpy as np
from PIL import Image, ImageDraw, ImageFont

from rich_lib import (L, P, Img, blur, brass_shade, draw_brass, hexc, normals_from_height, ramp, screen, sd_circle,
                      sd_rrect, smooth, text, text_size)
import rich_lib
from frame_rich import beads_line, enamel_over_guilloche, guilloche, moonstone, ring_beads, rosette

rich_lib.__dict__  # noqa: B018 (keep the import for FONTS below)
import mf_lib

mf_lib.FONTS.update({"title": "C:/Windows/Fonts/PERTILI.TTF", "title_b": "C:/Windows/Fonts/PERTIBD.TTF",
                     "caps": "C:/Windows/Fonts/CASTELAR.TTF"})


def panel(img, x0, y0, x1, y1, r=10.0, kind="wave", alpha=1.0, tint0="#1F2F64", tint1="#111A3C", border=True,
          bead=False, inner_rule=True):
    """An enamel panel over guilloche, framed by a brass bead (and an engraved inner rule)."""
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + 8)
    sd = sd_rrect(X, Y, x0, y0, x1, y1, r)
    cov = img.cov(sd) * alpha
    # a soft shadow under the panel toward the lower right (it stands above the screen)
    sh = img.cov(sd_rrect(X - 3, Y - 4, x0, y0, x1, y1, r) - 4) * (1 - img.cov(sd))
    img.mul(sl, hexc("#020308"), blur(sh, 4 * img.S) * 0.55 * alpha)
    enamel_over_guilloche(img, sl, X, Y, cov, kind, (x0 + x1) / 2, (y0 + y1) / 2, lit=(x0, y0),
                          dims=(x1 - x0, y1 - y0), tint0=tint0, tint1=tint1)
    if border:
        rim = lambda X_, Y_: np.abs(sd_rrect(X_, Y_, x0, y0, x1, y1, r)) - 1.6
        draw_brass(img, rim, ((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + 4), "round", depth=1.8,
                   width=1.6, alpha=alpha)
    if inner_rule:
        rule = np.abs(sd_rrect(X, Y, x0 + 6, y0 + 6, x1 - 6, y1 - 6, max(1, r - 5))) - 0.45
        img.over(sl, hexc(P["gilt_mid"]), img.cov(rule) * 0.55 * alpha)
    if bead:
        for (ax, ay, bx, by) in ((x0 + r, y0 - 0.5, x1 - r, y0 - 0.5), (x0 + r, y1 + 0.5, x1 - r, y1 + 0.5)):
            beads_line(img, ax, ay, bx, by, r=1.6, pitch=4.4)


def gilt_rule(img, x0, x1, y, w=1.2, ends=True):
    rule = lambda X, Y: np.maximum(np.abs(Y - y) - w / 2, np.maximum(x0 - X, X - x1))
    draw_brass(img, rule, ((x0 + x1) / 2, y, (x1 - x0) / 2 + 4), "round", depth=0.6, width=0.6)
    if ends:
        for x in (x0, x1):
            lozenge = lambda X, Y, x=x: np.abs(X - x) * 0.6 + np.abs(Y - y) - 3.2
            draw_brass(img, lozenge, (x, y, 5), "round", depth=1.2, width=1.6)


def button(img, x0, y0, x1, y1, label, size=15.0, state="normal", font="serif", sub=None):
    """A brass-framed enamel plate with an engraved label; 'focus' adds the moonlit glow of the selected item,
    'locked' dims it."""
    r = (y1 - y0) / 2 if (y1 - y0) < 40 else 8.0
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 + 10)
    sd = sd_rrect(X, Y, x0, y0, x1, y1, r)
    if state == "focus":
        img.add(sl, hexc("#9DB4EA"), np.exp(-np.clip(sd, 0, None) / 6.0) * (sd > 0) * 0.30)
    t = np.clip((Y - y0) / (y1 - y0), 0, 1)
    top, bot = ("#2B3E7C", "#16204A") if state != "locked" else ("#1A2142", "#10152E")
    if state == "focus":
        top, bot = "#36509A", "#1C2A5C"
    img.over(sl, ramp(t, [(0, top), (1, bot)]), img.cov(sd))
    img.add(sl, hexc("#8FA4DA"), img.cov(sd) * np.exp(-((Y - y0 - 2.5) / 2.0) ** 2) * 0.18)
    rim = lambda X_, Y_: np.abs(sd_rrect(X_, Y_, x0, y0, x1, y1, r)) - 1.4
    draw_brass(img, rim, ((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 + 4), "round", depth=1.5, width=1.4,
               base=-0.12 if state == "locked" else 0.0)
    col = P["cream"] if state != "locked" else "#7E86A4"
    cy = (y0 + y1) / 2 + (-6 if sub else 0.5)
    text(img, (x0 + x1) / 2, cy, label, font, size, col, anchor="mm", halo=0)
    if sub:
        text(img, (x0 + x1) / 2, cy + size * 0.95, sub, "ui", size * 0.62, P["ink_dim"] if state != "focus" else P["gilt_high"],
             anchor="mm", halo=0)


def tab(img, x0, y0, x1, y1, label, active=False):
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 + 8)
    sd = sd_rrect(X, Y, x0, y0, x1, y1 + 8, 7)
    sd = np.maximum(sd, Y - y1)
    col = ramp(np.clip((Y - y0) / (y1 - y0), 0, 1), [(0, "#2B3E7C" if active else "#1A2448"), (1, "#1F2F64" if active else "#121A3C")])
    img.over(sl, col, img.cov(sd))
    rim = lambda X_, Y_: np.maximum(np.abs(np.maximum(sd_rrect(X_, Y_, x0, y0, x1, y1 + 8, 7), Y_ - y1)) - 1.2, Y_ - y1)
    draw_brass(img, rim, ((x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2 + 4), "round", depth=1.2, width=1.2,
               base=0.0 if active else -0.15)
    text(img, (x0 + x1) / 2, (y0 + y1) / 2 + 1, label, "serif", 15 if active else 14, P["cream"] if active else P["ink_dim"],
         anchor="mm", halo=0)


def place_rgba(img, rgba, x, y, w, h):
    """Pastes a float RGBA image into the canvas at units (x, y), size (w, h) units."""
    S = img.S
    im = Image.fromarray((np.clip(rgba, 0, 1) * 255).astype(np.uint8), "RGBA").resize((int(w * S), int(h * S)), Image.LANCZOS)
    arr = np.asarray(im, np.float32) / 255
    X0, Y0 = int(round(x * S)), int(round(y * S))
    X1, Y1 = min(img.w, X0 + arr.shape[1]), min(img.h, Y0 + arr.shape[0])
    a = arr[:Y1 - Y0, :X1 - X0, 3:4]
    img.px[Y0:Y1, X0:X1] = img.px[Y0:Y1, X0:X1] * (1 - a) + arr[:Y1 - Y0, :X1 - X0, :3] * a


def place_rgb(img, rgb, x, y, w, h):
    S = img.S
    im = Image.fromarray((np.clip(rgb, 0, 1) * 255).astype(np.uint8), "RGB").resize((int(w * S), int(h * S)), Image.LANCZOS)
    arr = np.asarray(im, np.float32) / 255
    X0, Y0 = int(round(x * S)), int(round(y * S))
    X1, Y1 = min(img.w, X0 + arr.shape[1]), min(img.h, Y0 + arr.shape[0])
    img.px[Y0:Y1, X0:X1] = arr[:Y1 - Y0, :X1 - X0]


def oval_bezel(img, cx, cy, rx, ry, w=3.2, beads=True):
    """A brass oval bezel (for a cameo) with a bead ring outside it."""
    def sd(X, Y):
        d = np.sqrt(((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2)
        return (d - 1) * min(rx, ry)
    rim = lambda X, Y: np.abs(sd(X, Y) - w * 0.5) - w * 0.5
    draw_brass(img, rim, (cx, cy, max(rx, ry) + w + 4), "bevel", depth=2.2, width=w * 0.8)
    if beads:
        n = int(2 * math.pi * math.sqrt((rx * rx + ry * ry) / 2) / 5.2)
        for k in range(n):
            a = 2 * math.pi * k / n
            bx, by = cx + (rx + w + 2.2) * math.cos(a), cy + (ry + w + 2.2) * math.sin(a)
            s, X, Y = img.win(bx, by, 2.6)
            u, v = (X - bx) / 1.5, (Y - by) / 1.5
            d2 = u * u + v * v
            nz = np.sqrt(np.clip(1 - d2, 0, 1))
            img.over(s, brass_shade(u, v, nz, base=0.03), np.clip((1 - np.sqrt(d2)) * 1.5 * img.S + 0.5, 0, 1))


def cameo_badge(img, rgba, cx, cy, rx, ry, beads=True, dim=False):
    """A cameo (RGBA from cameo.render) set in an oval brass bezel."""
    place_rgba(img, rgba, cx - rx, cy - ry, 2 * rx, 2 * ry)
    if dim:
        sl, X, Y = img.win(cx, cy, max(rx, ry) + 2)
        d = np.sqrt(((X - cx) / rx) ** 2 + ((Y - cy) / ry) ** 2)
        img.mul(sl, hexc("#0A0E1E"), np.clip((1 - d) * 40, 0, 1) * 0.55)
    oval_bezel(img, cx, cy, rx, ry, w=max(2.0, rx * 0.045), beads=beads)


def vignette_rect(img, k=0.45):
    sl, X, Y = img.full()
    W, H = img.w / img.S, img.h / img.S
    d = np.sqrt(((X / W - 0.5) / 0.7) ** 2 + ((Y / H - 0.48) / 0.7) ** 2)
    img.px[sl] = img.px[sl] * (1 - k * smooth(0.35, 1.0, d))[..., None]


def dim(img, k=0.55, col="#05070F"):
    sl, X, Y = img.full()
    img.mul(sl, hexc(col), np.full(X.shape, k, np.float32))


def logotype(img, cx, y, size, sub=None, sub_size=None):
    """MOONFALL in display capitals, engraved gilt with a lit upper edge and a dark lower lip, over a gilt rule."""
    text(img, cx + size * 0.03, y + size * 0.05, "MOONFALL", "title_b", size, "#05070F", anchor="mm", halo=0, tracking=size * 0.16)
    text(img, cx, y, "MOONFALL", "title_b", size, P["gilt_high"], anchor="mm", halo=0.0, tracking=size * 0.16)
    w = text_size("MOONFALL", "title_b", size)[0] + size * 0.16 * 7
    gilt_rule(img, cx - w * 0.42, cx + w * 0.42, y + size * 0.62, w=1.4)
    if sub:
        text(img, cx, y + size * 1.05, sub, "serif_i", sub_size or size * 0.34, P["cream"], anchor="mm", halo=0.4)

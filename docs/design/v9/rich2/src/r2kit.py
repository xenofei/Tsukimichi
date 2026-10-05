"""Rich pass 2 widgets, built from FFXIV's own UI art (r2lib.ATLAS), graded to Menphina's Medallion.

  button   the Gold Saucer's gilt pill (Lord of Verminion), its fill recoloured as enamel; focus adds the game's own
           warm selection glow
  tab      the window kit's tab plate, graded
  crest_rule  the quest journal's crest over a gilt rule (titles)
  title    a screen title in Jupiter, gilt, with a dark edge, as the game sets zone names
  medallion  a portrait (the card's face crop) in a Lord of Verminion lattice ring
  card     a companion's Triple Triad card (or its back), with the selection glow
  laurel   Triple Triad's victory laurel and ribbon
"""
import functools

import numpy as np

from r2lib import (C, blit, gild, gtext, hexc, part, ramp, resample, ring, disc_image, screen, sd_rrect, smooth,
                   shadow_under, gwidth, record, uld)


@functools.lru_cache(None)
def _pill(state):
    """The pill (252 x 94 hr px incl. shadow; the body 224 x 58 at (14, 18)) with its fill recoloured."""
    p = part("lv_pill").copy()
    h, w = p.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    # the body's interior: inset 4 hr px from the rim (body x 14..238, y 18..76 within the crop; radius 29)
    inner = sd_rrect(xx + 0.5, yy + 0.5, 18.0, 22.0, 234.0, 72.0, 25.0)
    m = np.clip(0.5 - inner, 0, 1)
    t = np.clip((yy - 22) / 50, 0, 1)
    stops = {"normal": [(0, "#2C428C"), (0.5, "#1A2A66"), (1, "#0E1640")],
             "focus": [(0, "#4C6FD6"), (0.45, "#2A47A8"), (1, "#16245E")],
             "primary": [(0, "#3A64D8"), (0.45, "#2443A8"), (1, "#121E58")],
             "locked": [(0, "#22283E"), (1, "#121628")],
             "danger": [(0, "#8E2B4A"), (0.5, "#5E1A32"), (1, "#300C1A")]}[state]
    fill = ramp(t, stops)
    # the leather's own grain from the game's fill, kept as a faint texture
    Y = p[..., :3] @ np.array([0.2126, 0.7152, 0.0722], np.float32)
    fill = fill * (0.85 + 0.6 * (Y - 0.22))[..., None]
    # a glaze highlight along the top (the light is above)
    fill = screen(fill, hexc("#BFD0FF") * (np.exp(-((yy - 26) / 4.5) ** 2) * 0.22)[..., None])
    g = gild(p, 0.6, 0.0 if state != "locked" else -0.2)
    if state == "locked":
        g[..., :3] = g[..., :3] * 0.55 + g[..., :3].mean(-1, keepdims=True) * 0.25
    g[..., :3] = g[..., :3] * (1 - m[..., None]) + fill * m[..., None]
    return g


def pill(img, x0, y0, x1, y1, state="normal", alpha=1.0):
    """The pill body fills x0..x1, y0..y1 (units); its shadow spills below."""
    p = _pill(state)
    bh = y1 - y0
    s = bh / 58.0                                    # units per hr px
    cap = 44                                          # hr px of each end cap (body radius 29 + shadow 14)
    ox, oy = 14 * s, 18 * s
    # round 2 (UX m2: seams at the cap joins): the pill is assembled in hr px (caps and a stretched middle) and
    # resampled once, so no slice boundary survives to the device image
    total = (x1 - x0 + 2 * ox) / s
    mid_w = max(0, int(round(total)) - 2 * cap)
    mid = np.repeat(p[:, cap + 1:cap + 2], mid_w, 1)
    whole = np.concatenate([p[:, :cap], mid, p[:, -cap:]], 1)
    blit(img, whole, x0 - ox, y0 - oy, whole.shape[1] * s, p.shape[0] * s, alpha)


def glow_rect(img, x0, y0, x1, y1, r, col="#FFD27A", k=0.5, spread=7.0):
    sl, X, Y = img.win((x0 + x1) / 2, (y0 + y1) / 2, max(x1 - x0, y1 - y0) / 2 + spread * 4)
    sd = sd_rrect(X, Y, x0, y0, x1, y1, r)
    img.add(sl, hexc(col), np.exp(-np.clip(sd, 0, None) / spread) * (sd > -0.5) * k)


def button(img, x0, y0, x1, y1, label, size=None, state="normal", sub=None, face="jupiter", icon=None):
    """A Gold Saucer pill with a game-font label. state: normal | focus | primary | locked | danger."""
    h = y1 - y0
    if state in ("focus", "primary"):
        glow_rect(img, x0, y0, x1, y1, h / 2, "#FFCF7A" if state == "focus" else "#9DC0FF", 0.30, 6.0)
    pill(img, x0, y0, x1, y1, state)
    size = size or h * 0.62
    col = C["cream"] if state != "locked" else "#7E86A4"
    cy = (y0 + y1) / 2 + (-h * 0.13 if sub else 0)
    gtext(img, (x0 + x1) / 2, cy, label, face, size, col, anchor="mm", edge="#0A0F2A", edge_w=1.0)
    if sub:
        gtext(img, (x0 + x1) / 2, cy + h * 0.30, sub, "axis", h * 0.27,
              C["gold_hi"] if state == "focus" else "#AEB6D6", anchor="mm")


def tab(img, x0, y0, x1, y1, label, active=False, size=None):
    t = part("tab_a")
    t = gild(t, 0.6)
    if active:
        # the tab's grey plate becomes lapis; the active tab is lit
        t = t.copy()
        t[..., :3] = screen(t[..., :3] * np.array([0.55, 0.75, 1.35], np.float32), hexc("#16245A") * 0.6)
    else:
        t = t.copy()
        t[..., :3] = t[..., :3] * np.array([0.42, 0.50, 0.85], np.float32)
    blit(img, t, x0, y0, x1 - x0, y1 - y0)
    gtext(img, (x0 + x1) / 2, (y0 + y1) / 2, label, "jupiter", size or (y1 - y0) * 0.62,
          C["cream"] if active else "#9AA3C8", anchor="mm", edge="#070B1E", edge_w=1.0)


def crest_rule(img, cx, y, w, crest=True, scale=0.5):
    """The journal's gilt rule with its scrolled crest in the middle (under a title)."""
    r = gild(part("jf_rule_a"), 0.55)
    rh = r.shape[0] * scale
    # the rule: two stretched halves either side of the crest
    seg = r[:, 20:60]
    blit(img, seg, cx - w / 2, y - rh / 2, w, rh)
    if crest:
        c = gild(part("jf_crest"), 0.55)
        cw, ch = c.shape[1] * scale, c.shape[0] * scale
        blit(img, c, cx - cw / 2, y - ch + rh * 0.55, cw, ch)


def title(img, x, y, s, size=34, anchor="lm", rule=None):
    """A title in Jupiter, gilt, with the game's dark edge and a soft warm glow."""
    w = gtext(img, x, y, s, "jupiter", size, C["gold_hi"], anchor=anchor, edge="#1A0F04", edge_w=1.6, gilt=True,
              glow="#FFB45E", glow_r=5, glow_k=0.18, shadow=0.6)
    return w


def medallion(img, c, cx, cy, r, ring_key="lv_ring", dim=False, accent=None, glow=0.0):
    """A companion's face (the plugin's card crop) under a Lord of Verminion lattice ring."""
    import r2cast
    if glow:
        sl, X, Y = img.win(cx, cy, r * 2.6)
        d = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)
        img.add(sl, hexc(accent or c["accent"]), np.exp(-np.clip(d - r * 1.55, 0, None) / (r * 0.35)) * (d > r) * glow)
    f = r2cast.face(c)
    disc_image(img, f, cx, cy, r * 1.04)
    if dim:
        sl, X, Y = img.win(cx, cy, r + 2)
        d = np.sqrt((X - cx) ** 2 + (Y - cy) ** 2)
        img.mul(sl, hexc("#070A18"), np.clip((r - d) * 4, 0, 1) * 0.62)
    return ring(img, cx, cy, r, ring_key)


def card(img, c, x, y, w, state="met", selected=False, label=True):
    """A companion's Triple Triad card: the card (art and gilt border), or face down (its back) when the story has not
    introduced them; 'locked' (met in the story, not yet reached in Moonfall) is the card dimmed."""
    import r2cast
    h = w * 256 / 208
    if selected:
        sel = gild(part("tt_cardsel"), 0.3)
        sel = sel.copy()
        # round 2 (designer m4): the selection glow takes the companion's own colour, its rim softened
        acc = hexc(c["accent"])
        sel[..., :3] = ramp(sel[..., :3].mean(-1), [(0, c["accent"]), (1, "#%02X%02X%02X" % tuple(int(v * 255) for v in acc * 0.6 + 0.4))])
        sel[..., 3] = np.asarray(__import__("PIL.Image", fromlist=["Image"]).fromarray((sel[..., 3] * 255).astype(np.uint8))
                                 .filter(__import__("PIL.ImageFilter", fromlist=["x"]).GaussianBlur(9)), np.float32) / 255
        gx, gy = w * 0.10, h * 0.09
        blit(img, sel, x - gx, y - gy, w + 2 * gx, h + 2 * gy, mode="add", alpha=0.75)
    shadow_under(img, x + 2, y + 2, x + w - 2, y + h - 2, r=6, off=(3, 6), soft=10, k=0.65)
    if state == "back":
        blit(img, gild(r2cast.card_back(), 0.5), x, y, w, h)
    else:
        blit(img, r2cast.light_grade(r2cast.card(c), 0.25), x, y, w, h)
        if state == "locked":
            sl, X, Y = img.win(x + w / 2, y + h / 2, h / 2 + 2)
            m = img.cov(sd_rrect(X, Y, x + 3, y + 3, x + w - 3, y + h - 3, 6))
            img.mul(sl, hexc("#060816"), m * 0.58)
    return h


def laurel(img, cx, cy, w, alpha=1.0, crown=False):
    """Triple Triad's victory laurel and ribbon, gilded."""
    p = gild(part("tt_laurel"), 0.45, 0.2)
    if not crown:
        # the banner version: the ribbon and the branches only, the crowned plaque in the middle left out
        p = p.copy()
        yy, xx = np.mgrid[0:p.shape[0], 0:p.shape[1]].astype(np.float32)
        plaque = np.clip((np.minimum(xx - 262, 380 - xx)) / 3.0, 0, 1) * np.clip((118 - yy) / 3.0, 0, 1)
        p[..., 3] *= 1 - plaque
    h = w * p.shape[0] / p.shape[1]
    blit(img, p, cx - w / 2, cy - h / 2, w, h, alpha)
    return h


def gems_n(img, cx, cy, w, total=3, lit=2, accent="#8FF0E8"):
    """The turns left as a row of `total` lozenge gems set in a gilt bar (the scholar gauge's own setting for three;
    for any other count, the same gems in a slim gilt bar): lit gems glow in the carrier's colour."""
    if total == 3:
        return gems(img, cx, cy, min(w, 46), lit, accent)
    from rich_lib import draw_brass
    gw = min(10.0, (w - 6) / max(total, 1))
    x0 = cx - gw * total / 2
    draw_brass(img, lambda X, Y: np.abs(sd_rrect(X, Y, x0 - 3, cy - 6.5, x0 + gw * total + 3, cy + 6.5, 6.5)) - 1.0,
               (cx, cy, gw * total / 2 + 6), "round", depth=1.0, width=1.0)
    sl, X, Y = img.win(cx, cy, gw * total / 2 + 6)
    img.over(sl, hexc("#070A1C"), img.cov(sd_rrect(X, Y, x0 - 2, cy - 5.5, x0 + gw * total + 2, cy + 5.5, 5.5)) * 0.9)
    for i in range(total):
        gx = x0 + gw * (i + 0.5)
        s2, X2, Y2 = img.win(gx, cy, gw)
        d = np.abs(X2 - gx) + np.abs(Y2 - cy)
        rr = min(gw * 0.45, 4.6)
        m = np.clip((rr - d) * img.S * 0.8, 0, 1)
        if i < lit:
            col = ramp(np.clip((Y2 - cy + rr) / (2 * rr), 0, 1), [(0, "#FFFFFF"), (0.35, accent), (1, "#1A2A6A")])
            img.over(s2, col, m)
            img.add(s2, hexc(accent), np.exp(-(d / (rr * 1.3)) ** 2) * 0.35)
        else:
            img.over(s2, ramp(np.clip((Y2 - cy + rr) / (2 * rr), 0, 1), [(0, "#2A3260"), (1, "#0A0E24")]), m)


def gems(img, cx, cy, w, lit=2, accent="#8FF0E8"):
    """The scholar gauge's triple gem frame as the turns-left pips: a lit gem glows in the carrier's colour."""
    g = gild(part("sch_gems"), 0.5)
    h = w * g.shape[0] / g.shape[1]
    blit(img, g, cx - w / 2, cy - h / 2, w, h)
    # the three gem windows (centres at 0.215, 0.5, 0.785 across, 0.46 down in the hr art)
    for i, fx in enumerate((0.215, 0.5, 0.785)):
        gx, gy = cx - w / 2 + fx * w, cy - h / 2 + 0.47 * h
        sl, X, Y = img.win(gx, gy, h * 0.4)
        d = np.abs(X - gx) + np.abs(Y - gy)                     # a lozenge
        rr = h * 0.22
        m = np.clip((rr - d) * img.S * 0.8, 0, 1)
        if i < lit:
            col = ramp(np.clip((Y - gy + rr) / (2 * rr), 0, 1), [(0, "#FFFFFF"), (0.35, accent), (1, "#1A2A6A")])
            img.over(sl, col, m)
            img.add(sl, hexc(accent), np.exp(-(d / (rr * 1.3)) ** 2) * 0.35)
        else:
            img.over(sl, ramp(np.clip((Y - gy + rr) / (2 * rr), 0, 1), [(0, "#2A3260"), (1, "#0A0E24")]), m)

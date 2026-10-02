"""Generator for the Menphina's Medallion concept (round 2). Writes 8 state SVGs + plugin-icon.svg."""
import math, pathlib, sys

OUT = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6\round2\menphina-medallion")

# ---- palette tokens ----
KEY = "#080B16"
GILT = dict(hi="#FFF3D1", light="#E9D49C", mid="#C9A766", base="#A88B52", deep="#6B5124")
PEW = dict(hi="#E6EBF5", light="#A7B0C9", mid="#6C7594", base="#565E7C", deep="#2C3350")
IRON = dict(hi="#E7B3A6", light="#A87570", mid="#6E4247", base="#5E3440", deep="#2A1722")
LAPIS_T, LAPIS_B = "#2B4790", "#1E3168"      # lit enamel: act-now (Ready)
DUSK_T, DUSK_B = "#1D2C5E", "#141E44"        # resting enamel: every other state
SEA = "#121C40"
MOON_HI, MOON, MOON_MID, MOON_LOW, MOON_DEEP = "#F3F0E6", "#E2E8F4", "#C3CEE4", "#95A5C8", "#5E6E97"
AETHER_HI, AETHER, AETHER_DEEP = "#9BE6FF", "#4FB3EA", "#1F5FA8"
RED_HI, RED, RED_DEEP, OXBLOOD = "#D2584E", "#8E2A2E", "#4A1620", "#2A0F1C"
ASH, ASH_LIMB = "#4A5577", "#DDE3F0"
COMP_FILL = "#6F7EA6"
RIBBON, RIBBON_DEEP, RIBBON_HI = "#D9B45E", "#8A6526", "#F6E2A8"

C = 64.0
R_KEY, R_OUT, R_IN = 63.0, 61.0, 51.5   # keyline, bezel outer, bezel inner (= well radius)


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def phase(cx, cy, r, k, lit="right", rot=0.0):
    """Moon phase as a path: right semicircle + elliptical terminator. k>0 gibbous, k<0 crescent, k=0 half."""
    rx = abs(k) * r
    sweep = 1 if k > 0 else 0
    if abs(k) < 1e-6:
        d = f"M{f(cx)} {f(cy - r)}A{f(r)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}Z"
    else:
        d = (f"M{f(cx)} {f(cy - r)}A{f(r)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}"
             f"A{f(rx)} {f(r)} 0 0 {sweep} {f(cx)} {f(cy - r)}Z")
    tr = []
    if lit == "left":
        tr.append(f"translate({f(2 * cx)} 0) scale(-1 1)")
    if rot:
        tr.insert(0, f"rotate({f(rot)} {f(cx)} {f(cy)})")
    return d, (" ".join(tr))


def lit_mask_np(cx, cy, r, k, lit, rot, xs, ys):
    """Boolean lit mask on a numpy grid, same geometry as phase()."""
    import numpy as np
    a = math.radians(-rot)
    dx, dy = xs - cx, ys - cy
    u = dx * math.cos(a) - dy * math.sin(a)
    v = dx * math.sin(a) + dy * math.cos(a)
    if lit == "left":
        u = -u
    inside = u * u + v * v <= r * r
    rx = abs(k) * r
    half = np.sqrt(np.clip(1 - (v / r) ** 2, 0, 1))
    term = -rx * half if k > 0 else rx * half
    return inside & (u >= term)


def bezel(p, m, hero=True, glint=False):
    s = []
    d = []
    d.append(f'<linearGradient id="{p}bz" x1="0.18" y1="0.08" x2="0.82" y2="0.92">'
             f'<stop offset="0" stop-color="{m["light"]}"/><stop offset=".5" stop-color="{m["mid"]}"/>'
             f'<stop offset="1" stop-color="{m["deep"]}"/></linearGradient>')
    d.append(f'<linearGradient id="{p}bi" x1="0.18" y1="0.08" x2="0.82" y2="0.92">'
             f'<stop offset="0" stop-color="{m["deep"]}"/><stop offset="1" stop-color="{m["light"]}"/></linearGradient>')
    s.append(f'<circle cx="64" cy="64" r="{f((R_KEY + R_OUT) / 2 - 0.3)}" fill="none" stroke="{KEY}" stroke-opacity=".9" stroke-width="{f(R_KEY - R_OUT + 1)}"/>')
    rm = (R_OUT + R_IN) / 2
    s.append(f'<circle cx="64" cy="64" r="{f(rm)}" fill="none" stroke="url(#{p}bz)" stroke-width="{f(R_OUT - R_IN)}"/>')
    # inner counter-bevel (two-arc: dark top-left, light bottom-right) makes the rim read as a raised coin edge
    s.append(f'<circle cx="64" cy="64" r="{f(R_IN + 1.2)}" fill="none" stroke="url(#{p}bi)" stroke-width="2.4"/>')
    if hero:
        # engraved bezel: 12 moon-daisy petals with seed dots between them, fine hairlines
        g = []
        for i in range(12):
            a = math.radians(i * 30 - 90)
            x, y = 64 + rm * math.cos(a), 64 + rm * math.sin(a)
            g.append(f'<ellipse cx="{f(x)}" cy="{f(y)}" rx="3.1" ry="1.35" transform="rotate({f(i * 30)} {f(x)} {f(y)})"/>')
            a2 = math.radians(i * 30 - 75)
            g.append(f'<circle cx="{f(64 + rm * math.cos(a2))}" cy="{f(64 + rm * math.sin(a2))}" r=".75" fill="{m["deep"]}" stroke="none"/>')
        s.append(f'<g fill="none" stroke="{m["deep"]}" stroke-width=".7" opacity=".55">{"".join(g)}</g>')
    if glint:
        a0, a1 = math.radians(-152), math.radians(-118)
        r = R_OUT - 2.6
        s.append(f'<path d="M{f(64 + r * math.cos(a0))} {f(64 + r * math.sin(a0))}A{f(r)} {f(r)} 0 0 1 '
                 f'{f(64 + r * math.cos(a1))} {f(64 + r * math.sin(a1))}" fill="none" stroke="{m["hi"]}" '
                 f'stroke-width="2.6" stroke-linecap="round" opacity=".95"/>')
    return d, s


def almond(x0, x1, yc, h):
    """A ripple glint: a lens with softly pointed ends (max thickness h)."""
    xm = (x0 + x1) / 2
    return f"M{f(x0)} {f(yc)}Q{f(xm)} {f(yc - h)} {f(x1)} {f(yc)}Q{f(xm)} {f(yc + h)} {f(x0)} {f(yc)}Z"


def well(p, top=DUSK_T, bot=DUSK_B, sea=None, horizon=None):
    d = [f'<linearGradient id="{p}wl" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{top}"/>'
         f'<stop offset="1" stop-color="{bot}"/></linearGradient>',
         f'<clipPath id="{p}wc"><circle cx="64" cy="64" r="{f(R_IN)}"/></clipPath>']
    s = [f'<circle cx="64" cy="64" r="{f(R_IN)}" fill="url(#{p}wl)"/>']
    if sea:
        s.append(f'<rect x="0" y="{f(horizon)}" width="128" height="{f(128 - horizon)}" fill="{sea}" clip-path="url(#{p}wc)"/>')
    return d, s


def seat_ring(p, color, op=".32"):
    """Hero-tier seat ring: a hairline circle with four tiny diamond studs (original)."""
    r = R_IN - 4.5
    st = []
    for i in range(4):
        a = math.radians(i * 90 - 45)
        x, y = 64 + r * math.cos(a), 64 + r * math.sin(a)
        st.append(f'<path d="M{f(x)} {f(y - 1.8)}L{f(x + 1.8)} {f(y)}L{f(x)} {f(y + 1.8)}L{f(x - 1.8)} {f(y)}Z" fill="{color}" stroke="none"/>')
    return (f'<g opacity="{op}"><circle cx="64" cy="64" r="{f(r)}" fill="none" stroke="{color}" stroke-width=".6"/>'
            + "".join(st) + "</g>")


def hatch(p, cx, cy, r, side, color=MOON_LOW, op=".3", reach=0.55):
    """Engraved hatching in the dark part, fading away from the terminator. side = direction (+1 right, -1 left) of the dark side."""
    d = [f'<clipPath id="{p}hc"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - 1.2)}"/></clipPath>',
         f'<linearGradient id="{p}hg" x1="{0 if side < 0 else 1}" y1="0" x2="{1 if side < 0 else 0}" y2="0">'
         f'<stop offset="{f(1 - reach)}" stop-color="#fff" stop-opacity="0"/><stop offset="1" stop-color="#fff"/></linearGradient>',
         f'<mask id="{p}hm"><rect x="{f(cx - r)}" y="{f(cy - r)}" width="{f(2 * r)}" height="{f(2 * r)}" fill="url(#{p}hg)"/></mask>']
    lines = []
    t = -2 * r
    while t < 2 * r:
        lines.append(f"M{f(cx + t - r)} {f(cy + r)}L{f(cx + t + r)} {f(cy - r)}")
        t += 3.2
    s = [f'<g clip-path="url(#{p}hc)"><path d="{"".join(lines)}" stroke="{color}" stroke-width=".6" fill="none" '
         f'opacity="{op}" mask="url(#{p}hm)"/></g>']
    return d, s


def moon_disc(cx, cy, r, fill):
    return f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="{fill}"/>'


def lit(p, cx, cy, r, k, side="right", rot=0, fill=MOON, limb=MOON_HI, term=MOON_MID, hero=True):
    """Two-tone lit body: body fill, crisp terminator band (term colour) and a crisp limb band."""
    dpath, tr = phase(cx, cy, r, k, side, rot)
    t = f' transform="{tr}"' if tr else ""
    out = [f'<path d="{dpath}" fill="{fill}"{t}/>']
    # crisp terminator band: the same phase shape inset by drawing the terminator ellipse as a stroke, clipped to the lit body
    out.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    if abs(k) > 1e-6:
        rx = abs(k) * r
        term_el = f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(rx)}" ry="{f(r)}" fill="none" stroke="{term}" stroke-width="3"/>'
    else:
        term_el = f'<path d="M{f(cx)} {f(cy - r)}V{f(cy + r)}" stroke="{term}" stroke-width="3"/>'
    out.append(f'<g clip-path="url(#{p}lc)"><g{t}>{term_el}</g></g>')
    # limb highlight: a thin arc on the outer lit edge
    out.append(f'<g clip-path="url(#{p}lc)"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r - 0.9)}" fill="none" stroke="{limb}" stroke-width="1.8"/></g>')
    return out


def svg(title, defs, body, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')


def write(name, text):
    (OUT / name).write_text(text, encoding="utf-8")


# ---------------- states ----------------

def ready():
    p = "mmr-"
    D, S = [], []
    H = GLYPH_H
    dd, ss = well(p, top=LAPIS_T, bot=LAPIS_B, sea=SEA, horizon=H); D += dd; S += ss
    # moon: thick crescent, lit limb facing down-right like the icon
    mx, my, mr, mk, rot = GLYPH_MOON
    S.append(f'<g clip-path="url(#{p}wc)">')
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr + 9)}" fill="{MOON}" fill-opacity=".08"/>')
    S += lit(p, mx, my, mr, mk, "right", rot)
    # horizon: a soft line, brightest where the road meets it
    D.append(f'<linearGradient id="{p}hz" x1="12" y1="0" x2="116" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".1"/>'
             f'<stop offset="{f((ROAD_X - 12) / 104)}" stop-color="{MOON}" stop-opacity=".75"/><stop offset="1" stop-color="{MOON}" stop-opacity=".1"/></linearGradient>')
    S.append(f'<rect x="12" y="{f(H - 0.7)}" width="104" height="1.4" fill="url(#{p}hz)"/>')
    cxr = ROAD_X
    rows = [  # (y centre, thickness, [(x0, x1, opacity)]) – lens: narrow at the horizon, widest low, ragged
        (H + 4.0, 4.4, [(cxr - 6.5, cxr + 6.0, .7)]),
        (H + 11.5, 5.4, [(cxr - 12.5, cxr + 1.0, .85), (cxr + 3.5, cxr + 11.0, .75)]),
        (H + 20.5, 6.6, [(cxr - 16.0, cxr - 4.5, .85), (cxr - 2.0, cxr + 15.0, 1.0)]),
        (H + 29.5, 6.8, [(cxr - 9.0, cxr + 3.0, .8), (cxr + 6.0, cxr + 11.5, .65)]),
    ]
    for y, h, dashes in rows:
        for x0, x1, o in dashes:
            S.append(f'<path d="{almond(x0, x1, y, h)}" fill="{MOON}" fill-opacity="{o}"/>')
    S.append('</g>')
    S.append('<g class="hero">')
    S.append(seat_ring(p, GILT["light"], ".28"))
    S.append('</g>')
    dd, ss = bezel(p, GILT, glint=True); D += dd; S += ss
    return svg("Ready", D, S)


def ready_other_job():
    p = "mmo-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 36.0
    S.append(moon_disc(mx, my, mr, "#16224C"))
    dd, ss = hatch(p, mx, my, mr, -1); D += dd; S += ss
    S += lit(p, mx, my, mr, 0.0, "right")
    # job crystal: an original faceted spindle in the dark half (two tones + one facet line)
    cx, cy, w, h = 45.0, 64.0, 10.5, 21.0
    S.append(f'<path d="M{f(cx)} {f(cy - h)}L{f(cx + w)} {f(cy)}L{f(cx)} {f(cy + h)}L{f(cx - w)} {f(cy)}Z" fill="{AETHER}" stroke="{KEY}" stroke-width="1.2" stroke-linejoin="round"/>')
    S.append(f'<path d="M{f(cx)} {f(cy - h)}L{f(cx + w)} {f(cy)}L{f(cx)} {f(cy + 3)}Z" fill="{AETHER_HI}"/>')
    S.append(f'<path d="M{f(cx)} {f(cy + 3)}L{f(cx + w)} {f(cy)}L{f(cx)} {f(cy + h)}Z" fill="{AETHER_DEEP}" fill-opacity=".85"/>')
    S.append('<g class="hero">' + seat_ring(p, PEW["light"], ".3") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    return svg("Ready on another job", D, S)


def in_journal():
    p = "mmj-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 66.0, 66.0, 34.0
    S.append(moon_disc(mx, my, mr, "#16224C"))
    dd, ss = hatch(p, mx, my, mr, -1); D += dd; S += ss
    S += lit(p, mx, my, mr, 0.55, "right")
    S.append('<g class="hero">' + seat_ring(p, PEW["light"], ".3") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    # journal ribbon: hangs over the bezel at upper-left, swallowtail end, with a fold shadow
    x0, w, y0, y1 = 25.0, 15.0, 8.0, 66.0
    S.append(f'<path d="M{f(x0 - 1.4)} {f(y0 - 1.4)}H{f(x0 + w + 1.4)}V{f(y1 + 1.6)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0 - 1.4)} {f(y1 + 1.6)}Z" fill="{KEY}" fill-opacity=".85"/>')
    S.append(f'<path d="M{f(x0)} {f(y0)}H{f(x0 + w)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7.5)}L{f(x0)} {f(y1)}Z" fill="{RIBBON}"/>')
    S.append(f'<path d="M{f(x0 + w - 4)} {f(y0)}H{f(x0 + w)}V{f(y1)}L{f(x0 + w - 4)} {f(y1 - 3.6)}Z" fill="{RIBBON_DEEP}" fill-opacity=".55"/>')
    S.append(f'<g class="hero" opacity=".45"><path d="M{f(x0 + 3)} {f(y0 + 2)}V{f(y1 - 6)}" stroke="{RIBBON_HI}" stroke-width=".7"/>'
             f'<circle cx="{f(x0 + w / 2)}" cy="{f(y0 + 13)}" r="3.4" fill="none" stroke="{RIBBON_DEEP}" stroke-width=".8"/></g>')
    return svg("In journal", D, S)


def blocked():
    p = "mmb-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 36.0
    S.append(moon_disc(mx, my, mr, ASH))
    dd, ss = hatch(p, mx, my, mr, -1, color="#8C96B4", op=".28", reach=0.9); D += dd; S += ss
    # thin bright limb (a new moon's sliver of earthlit edge) on the right
    S += lit(p, mx, my, mr, -0.78, "right", 0, fill=ASH_LIMB, limb=MOON_HI, term=ASH_LIMB)
    S.append('<g class="hero">' + seat_ring(p, PEW["light"], ".3") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    return svg("Blocked", D, S)


def done():
    p = "mmd-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 36.0
    S.append(moon_disc(mx, my, mr, "#16224C"))
    dd, ss = hatch(p, mx, my, mr, 1); D += dd; S += ss
    S += lit(p, mx, my, mr, 0.0, "left", fill=MOON_MID, limb=MOON, term=MOON_LOW)
    # check in the dark half, moonstone, round joins
    S.append(f'<path d="M66.5 64.5L74 72.5L88 53" fill="none" stroke="{KEY}" stroke-width="10" stroke-linecap="round" stroke-linejoin="round" stroke-opacity=".7"/>')
    S.append(f'<path d="M66.5 64.5L74 72.5L88 53" fill="none" stroke="{MOON}" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/>')
    S.append('<g class="hero">' + seat_ring(p, PEW["light"], ".3") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    return svg("Done this cycle", D, S)


def completed():
    p = "mmc-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, R_IN - 3.0
    S.append(moon_disc(mx, my, mr, COMP_FILL))
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 1.2)}" fill="none" stroke="{MOON_LOW}" stroke-width="2.4"/>')
    # hero: fine engraved concentric hairline (the "seat" of a coin) — no surface marks on the lit disc
    S.append(f'<g class="hero"><circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 6)}" fill="none" stroke="{MOON_DEEP}" stroke-width=".6" opacity=".35"/>'
             + seat_ring(p, GILT["light"], ".35") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    return svg("Completed", D, S)


def locked_out():
    p = "mml-"
    D, S = [], []
    dd, ss = well(p, top="#3A1628", bot=OXBLOOD); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 36.0
    # Dalamud's red moon, split by a fracture: two halves drawn separately with an enamel gap between
    crack = "M58 26L67 44L56 58L70 74L60 102"
    D.append(f'<clipPath id="{p}cl"><path d="M0 0H62L71 44L60 58L74 74L64 128H0Z"/></clipPath>')
    D.append(f'<clipPath id="{p}cr"><path d="M128 0H66L75 44L64 58L78 74L68 128H128Z"/></clipPath>')
    D.append(f'<linearGradient id="{p}rd" x1="0.2" y1="0.1" x2="0.8" y2="0.9"><stop offset="0" stop-color="{RED_HI}"/><stop offset="1" stop-color="{RED}"/></linearGradient>')
    S.append(f'<g clip-path="url(#{p}cl)" transform="translate(-2.5 0)">{moon_disc(mx, my, mr, f"url(#{p}rd)")}</g>')
    S.append(f'<g clip-path="url(#{p}cr)" transform="translate(2.5 0)">{moon_disc(mx, my, mr, f"url(#{p}rd)")}</g>')
    S.append('<g class="hero">' + seat_ring(p, IRON["light"], ".3") + '</g>')
    dd, ss = bezel(p, IRON); D += dd; S += ss
    return svg("Locked out", D, S)


def not_checked():
    p = "mmn-"
    D, S = [], []
    S.append(f'<circle cx="64" cy="64" r="{f(R_IN)}" fill="{LAPIS_B}" fill-opacity=".35"/>')
    S.append(f'<circle cx="64" cy="64" r="{f((R_OUT + R_IN) / 2)}" fill="none" stroke="{PEW["mid"]}" stroke-opacity=".45" stroke-width="{f(R_OUT - R_IN)}"/>')
    S.append(f'<circle cx="64" cy="64" r="34" fill="{MOON_LOW}" fill-opacity=".38"/>')
    # two mist bands drifting across, past the rim
    S.append(f'<rect x="4" y="50" width="96" height="10" rx="5" fill="#A9B2CC" fill-opacity=".62"/>')
    S.append(f'<rect x="30" y="70" width="94" height="10" rx="5" fill="#A9B2CC" fill-opacity=".5"/>')
    return svg("Not checked", D, S)



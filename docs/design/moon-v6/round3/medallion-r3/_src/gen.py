"""Generator for the Menphina's Medallion concept (round 2). Writes 8 state SVGs + plugin-icon.svg."""
import math, pathlib, sys

OUT = pathlib.Path(r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-v6\round2\menphina-medallion")

# ---- palette tokens ----
KEY = "#080B16"
GILT = dict(hi="#FFF3D1", light="#E9D49C", mid="#C9A766", base="#A88B52", deep="#6B5124")
PEW = dict(hi="#E6EBF5", light="#98A2BE", mid="#5F6888", base="#4C5474", deep="#272D48")
IRON = dict(hi="#E7B3A6", light="#A87570", mid="#6E4247", base="#5E3440", deep="#2A1722")
LAPIS_T, LAPIS_B = "#4266B4", "#2B4488"      # lit enamel: act-now (Ready)
DUSK_T, DUSK_B = "#1D2C5E", "#141E44"        # resting enamel: every other state
SEA = "#1A2858"
MOON_HI, MOON, MOON_MID, MOON_LOW, MOON_DEEP = "#F3F0E6", "#E2E8F4", "#C3CEE4", "#95A5C8", "#5E6E97"
AETHER_HI, AETHER, AETHER_DEEP = "#9BE6FF", "#4FB3EA", "#1F5FA8"
RED_HI, RED, RED_DEEP, OXBLOOD = "#D2584E", "#8E2A2E", "#4A1620", "#2A0F1C"
ASH, ASH_LIMB = "#4A5577", "#DDE3F0"
COMP_FILL = "#606E96"
OXI = dict(hi="#C3CCE0", light="#5E6888", mid="#3C4463", deep="#1D2238")   # oxidised pewter: Completed only
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
        # engraved bezel (astrolabe idiom): one running hairline, 24 fine ticks, and four tiny six-petal moon-daisies on the diagonals
        g = [f'<circle cx="64" cy="64" r="{f(rm + 1.6)}"/>']
        for i in range(24):
            if i % 6 == 3:
                continue
            a = math.radians(i * 15 - 90)
            g.append(f'<path d="M{f(64 + (rm - 3.2) * math.cos(a))} {f(64 + (rm - 3.2) * math.sin(a))}L{f(64 + (rm - 0.4) * math.cos(a))} {f(64 + (rm - 0.4) * math.sin(a))}"/>')
        for i in range(4):
            a = math.radians(i * 90 - 45)
            x, y = 64 + rm * math.cos(a), 64 + rm * math.sin(a)
            for j in range(6):
                g.append(f'<ellipse cx="{f(x + 1.5 * math.cos(math.radians(j * 60)))}" cy="{f(y + 1.5 * math.sin(math.radians(j * 60)))}" rx="1.25" ry=".6" transform="rotate({f(j * 60)} {f(x + 1.5 * math.cos(math.radians(j * 60)))} {f(y + 1.5 * math.sin(math.radians(j * 60)))})"/>')
            g.append(f'<circle cx="{f(x)}" cy="{f(y)}" r=".55" fill="{m["deep"]}"/>')
        s.append(f'<g fill="none" stroke="{m["deep"]}" stroke-width=".6" opacity=".5">{"".join(g)}</g>')
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


def lit(p, cx, cy, r, k, side="right", rot=0, fill=MOON, limb=MOON_HI, term=MOON_MID, hero=True, tw=3):
    """Two-tone lit body: body fill, crisp terminator band (term colour) and a crisp limb band."""
    dpath, tr = phase(cx, cy, r, k, side, rot)
    t = f' transform="{tr}"' if tr else ""
    out = [f'<path d="{dpath}" fill="{fill}"{t}/>']
    # crisp terminator band: the same phase shape inset by drawing the terminator ellipse as a stroke, clipped to the lit body
    out.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    if abs(k) > 1e-6:
        rx = abs(k) * r
        term_el = f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(rx)}" ry="{f(r)}" fill="none" stroke="{term}" stroke-width="{tw}"/>'
    else:
        term_el = f'<path d="M{f(cx)} {f(cy - r)}V{f(cy + r)}" stroke="{term}" stroke-width="{tw}"/>'
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
        (H + 3.4, 2.8, [(cxr - 7.0, cxr + 6.5, .72)]),
        (H + 9.0, 3.2, [(cxr - 12.0, cxr + 2.0, .85), (cxr + 4.0, cxr + 11.0, .7)]),
        (H + 15.4, 3.6, [(cxr - 16.0, cxr - 5.0, .78), (cxr - 2.5, cxr + 15.5, 1.0)]),
        (H + 22.4, 4.0, [(cxr - 14.5, cxr + 5.0, .95), (cxr + 7.5, cxr + 15.0, .7)]),
        (H + 29.8, 4.2, [(cxr - 9.0, cxr + 9.0, .8)]),
    ]
    for y, h, dashes in rows:
        for x0, x1, o in dashes:
            S.append(f'<path d="{glint(x0, x1, y, h)}" fill="{MOON}" fill-opacity="{o}"/>')
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
    S += lit(p, mx, my, mr, 0.0, "right", fill=MOON_MID, limb=MOON, term=MOON_LOW)
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
    mx, my, mr = 66.0, 66.0, 32.0
    S.append(moon_disc(mx, my, mr, "#16224C"))
    dd, ss = hatch(p, mx, my, mr, -1); D += dd; S += ss
    S += lit(p, mx, my, mr, 0.38, "right", fill=MOON_MID, limb=MOON, term=MOON_LOW, tw=1.8)
    S.append('<g class="hero">' + seat_ring(p, PEW["light"], ".3") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    # journal ribbon: hangs over the bezel at upper-left, swallowtail end, with a fold shadow
    x0, w, y0, y1 = 25.0, 15.0, 8.0, 66.0
    S.append(f'<path d="M{f(x0 - 1.4)} {f(y0 - 1.4)}H{f(x0 + w + 1.4)}V{f(y1 + 1.6)}L{f(x0 + w / 2)} {f(y1 - 7)}L{f(x0 - 1.4)} {f(y1 + 1.6)}Z" fill="{KEY}" fill-opacity=".85"/>')
    S.append(f'<path d="M{f(x0)} {f(y0)}H{f(x0 + w)}V{f(y1)}L{f(x0 + w / 2)} {f(y1 - 7.5)}L{f(x0)} {f(y1)}Z" fill="{RIBBON}"/>')
    S.append(f'<path d="M{f(x0 + w - 4)} {f(y0)}H{f(x0 + w)}V{f(y1)}L{f(x0 + w - 4)} {f(y1 - 3.6)}Z" fill="{RIBBON_DEEP}" fill-opacity=".55"/>')
    S.append(f'<g class="hero" opacity=".45"><path d="M{f(x0 + 3)} {f(y0 + 2)}V{f(y1 - 6)}" stroke="{RIBBON_HI}" stroke-width=".7"/>'
             f'<circle cx="{f(x0 + w / 2)}" cy="{f(y1 - 16)}" r="4.2" fill="{RIBBON_DEEP}"/>'
             f'<path d="M{f(x0 + w / 2 + 0.6)} {f(y1 - 19)}a3 3 0 1 0 0 6a2.3 2.3 0 1 1 0 -6Z" fill="{RIBBON_HI}"/></g>')
    return svg("In journal", D, S)


def blocked():
    p = "mmb-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, 36.0
    S.append(moon_disc(mx, my, mr, ASH))
    dd, ss = hatch(p, mx, my, mr, -1, color="#8C96B4", op=".28", reach=0.9); D += dd; S += ss
    # thin bright limb (a new moon's sliver of earthlit edge) on the right
    S += lit(p, mx, my, mr, -0.66, "right", 0, fill=ASH_LIMB, limb=MOON_HI, term=ASH_LIMB)
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
    S.append(f'<path d="M66 63L74.5 72L89 52" fill="none" stroke="{KEY}" stroke-width="15" stroke-linecap="round" stroke-linejoin="round" stroke-opacity=".7"/>')
    S.append(f'<path d="M66 63L74.5 72L89 52" fill="none" stroke="{MOON}" stroke-width="11.5" stroke-linecap="round" stroke-linejoin="round"/>')
    S.append('<g class="hero">' + seat_ring(p, PEW["light"], ".3") + '</g>')
    dd, ss = bezel(p, PEW); D += dd; S += ss
    return svg("Done this cycle", D, S)


def completed():
    p = "mmc-"
    D, S = [], []
    dd, ss = well(p); D += dd; S += ss
    mx, my, mr = 64.0, 64.0, R_IN - 3.0
    S.append(moon_disc(mx, my, mr, COMP_FILL))
    D.append(f'<linearGradient id="{p}cv" x1="0.15" y1="0.1" x2="0.85" y2="0.9"><stop offset="0" stop-color="{MOON_MID}"/><stop offset=".5" stop-color="{MOON_LOW}" stop-opacity=".5"/><stop offset="1" stop-color="#2E3858"/></linearGradient>')
    S.append(f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 1.4)}" fill="none" stroke="url(#{p}cv)" stroke-width="2.8"/>')
    # hero: fine engraved concentric hairline (the "seat" of a coin) — no surface marks on the lit disc
    petals = "".join(f'<ellipse cx="64" cy="{f(64 - 13)}" rx="4.2" ry="11" transform="rotate({i * 45} 64 64)"/>' for i in range(8))
    S.append(f'<g class="hero"><g fill="none" stroke="{MOON_MID}" stroke-width=".7" opacity=".3">{petals}<circle cx="64" cy="64" r="3.2"/></g>'
             f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 6)}" fill="none" stroke="{MOON_MID}" stroke-width=".6" opacity=".3"/>'
             + seat_ring(p, GILT["light"], ".4") + '</g>')
    dd, ss = bezel(p, OXI); D += dd; S += ss
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
    S.append(f'<g clip-path="url(#{p}cl)" transform="translate(-4 0)">{moon_disc(mx, my, mr, f"url(#{p}rd)")}</g>')
    S.append(f'<g clip-path="url(#{p}cr)" transform="translate(4 0)">{moon_disc(mx, my, mr, f"url(#{p}rd)")}</g>')
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
    S.append(f'<rect x="3" y="47" width="92" height="12" rx="6" fill="#A9B2CC" fill-opacity=".58"/>')
    S.append(f'<rect x="38" y="70" width="87" height="12" rx="6" fill="#A9B2CC" fill-opacity=".46"/>')
    return svg("Not checked", D, S)


# ---------------- icon ----------------
import numpy as np

ICON_MOON = (172.0, 158.0, 86.0, -0.38, 30.0)   # cx, cy, r, terminator k (crescent), rotation (lit limb faces 30 deg below horizontal)
ICON_H = 300.0


def centroid(cx, cy, r, k, side, rot):
    ys, xs = np.mgrid[0:512:0.5, 0:512:0.5]
    m = lit_mask_np(cx, cy, r, k, side, rot, xs, ys)
    return float(xs[m].mean()), float(ys[m].mean())


LIT_C = centroid(*ICON_MOON[:4], "right", ICON_MOON[4])
ROAD_IX = LIT_C[0]
BRIGHT_Y = min(ICON_H + (ICON_H - ICON_MOON[1]), 470)

GLYPH_MOON = (54.0, 47.0, 27.5, -0.28, 28.0)
GLYPH_H = 81.0
_ys, _xs = np.mgrid[0:128:0.25, 0:128:0.25]
_m = lit_mask_np(*GLYPH_MOON[:4], "right", GLYPH_MOON[4], _xs, _ys)
ROAD_X = float(_xs[_m].mean())

# icon road: (y centre, thickness, [(dx0, dx1, opacity)]) relative to ROAD_IX. Lens: ~lit width at the horizon,
# widest around the brightest row (y ~ 442), narrower in front; ragged edges, no two rows alike.
ROAD_ROWS = [
    (309, 12, [(-24, 24, .55)]),
    (328, 12, [(-34, 4, .62), (12, 48, .5)]),
    (350, 13, [(-50, -12, .48), (-4, 44, .72)]),
    (375, 14, [(-52, 8, .8), (16, 58, .55)]),
    (405, 15, [(-62, -16, .58), (-8, 54, .88)]),
    (440, 16, [(-60, 16, .92), (22, 70, .8)]),
    (474, 17, [(-54, 0, .74), (8, 60, .82)]),
]
ROAD = [(ROAD_IX + a, ROAD_IX + b, y, h, o) for y, h, segs in ROAD_ROWS for a, b, o in segs]


def glint(x0, x1, yc, h):
    """A ripple glint: a full-bodied lens with soft points (max thickness ~h)."""
    L = x1 - x0
    k = 0.667 * h
    return (f"M{f(x0)} {f(yc)}C{f(x0 + 0.26 * L)} {f(yc - k)} {f(x1 - 0.26 * L)} {f(yc - k)} {f(x1)} {f(yc)}"
            f"C{f(x1 - 0.26 * L)} {f(yc + k)} {f(x0 + 0.26 * L)} {f(yc + k)} {f(x0)} {f(yc)}Z")


def icon():
    p = "mmi-"
    D, S = [], []
    H = ICON_H
    mx, my, mr, mk, rot = ICON_MOON
    D.append(f'<clipPath id="{p}t"><rect width="512" height="512" rx="112"/></clipPath>')
    D.append(f'<linearGradient id="{p}sky" x1="0" y1="0" x2="0" y2="{f(H)}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#0E152D"/><stop offset=".6" stop-color="#1B2850"/><stop offset="1" stop-color="#33446F"/></linearGradient>')
    D.append(f'<linearGradient id="{p}sea" x1="0" y1="{f(H)}" x2="0" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#2E3E68"/><stop offset=".12" stop-color="#18234A"/><stop offset=".55" stop-color="#0E1530"/><stop offset="1" stop-color="#070B19"/></linearGradient>')
    D.append(f'<radialGradient id="{p}bl" cx="{f(LIT_C[0])}" cy="{f(LIT_C[1])}" r="190" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".15"/><stop offset=".5" stop-color="{MOON}" stop-opacity=".05"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    D.append(f'<radialGradient id="{p}col" cx="{f(ROAD_IX)}" cy="{f(BRIGHT_Y)}" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate({f(ROAD_IX)} {f(BRIGHT_Y)}) scale(90 150) translate({f(-ROAD_IX)} {f(-BRIGHT_Y)})"><stop offset="0" stop-color="{MOON}" stop-opacity=".14"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    D.append(f'<linearGradient id="{p}hz" x1="0" y1="0" x2="512" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".08"/><stop offset="{f(ROAD_IX / 512)}" stop-color="{MOON}" stop-opacity=".7"/><stop offset="1" stop-color="{MOON}" stop-opacity=".08"/></linearGradient>')
    D.append(f'<linearGradient id="{p}rim" x1="0" y1="0" x2="512" y2="512" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E9CF8A"/><stop offset=".5" stop-color="#B08A45"/><stop offset="1" stop-color="#7A5A2A"/></linearGradient>')
    D.append(f'<radialGradient id="{p}lg" cx="92" cy="358" r="60" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E0B860" stop-opacity=".28"/><stop offset="1" stop-color="#E0B860" stop-opacity="0"/></radialGradient>')
    D.append(f'<radialGradient id="{p}cg" cx="404" cy="258" r="26" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{AETHER}" stop-opacity=".45"/><stop offset="1" stop-color="{AETHER}" stop-opacity="0"/></radialGradient>')
    S.append(f'<g clip-path="url(#{p}t)">')
    S.append(f'<rect width="512" height="{f(H)}" fill="url(#{p}sky)"/><rect y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}sea)"/>')
    S.append(f'<rect width="512" height="{f(H)}" fill="url(#{p}bl)"/>')
    # moon: thick crescent, two-tone body (terminator band), crisp limb, faint engraved hatching along the terminator (hero)
    dpath, tr = phase(mx, my, mr, mk, "right", rot)
    t = f' transform="{tr}"'
    S.append(f'<path d="{dpath}" fill="{MOON}"{t}/>')
    D.append(f'<clipPath id="{p}lc"><path d="{dpath}"{t}/></clipPath>')
    S.append(f'<g clip-path="url(#{p}lc)"><g{t}><ellipse cx="{f(mx)}" cy="{f(my)}" rx="{f(abs(mk) * mr)}" ry="{f(mr)}" fill="none" stroke="{MOON_MID}" stroke-width="10"/>'
             f'<ellipse cx="{f(mx)}" cy="{f(my)}" rx="{f(abs(mk) * mr + 9)}" ry="{f(mr - 2)}" fill="none" stroke="{MOON_LOW}" stroke-width="1.6" opacity=".35"/></g>'
             f'<circle cx="{f(mx)}" cy="{f(my)}" r="{f(mr - 3)}" fill="none" stroke="{MOON_HI}" stroke-width="6"/></g>')
    # far island with an original aether crystal at its peak (the far shore where you travel to)
    S.append(f'<path d="M318 {f(H)}C334 {f(H - 12)} 352 {f(H - 20)} 374 {f(H - 22)}C388 {f(H - 30)} 398 {f(H - 34)} 410 {f(H - 34)}C424 {f(H - 30)} 434 {f(H - 22)} 450 {f(H - 19)}C468 {f(H - 16)} 488 {f(H - 10)} 512 {f(H - 6)}V{f(H + 1)}H318Z" fill="#0B1124"/>')
    S.append(f'<circle cx="404" cy="258" r="26" fill="url(#{p}cg)"/>')
    S.append(f'<path d="M404 238L410 256L404 268L398 256Z" fill="{AETHER}"/><path d="M404 238L410 256L404 258Z" fill="{AETHER_HI}"/><path d="M404 258L410 256L404 268Z" fill="{AETHER_DEEP}"/>')
    # horizon brightening where the road meets it
    S.append(f'<rect x="0" y="{f(H - 1)}" width="512" height="2.4" fill="url(#{p}hz)"/>')
    # the crystal's own tiny reflection
    S.append(f'<path d="{glint(394, 414, H + 9, 4)}" fill="{AETHER}" fill-opacity=".35"/>')
    # under-glow of the glade
    S.append(f'<rect x="0" y="{f(H)}" width="512" height="{f(512 - H)}" fill="url(#{p}col)"/>')
    # flattened mirrored crescent just under the horizon, cut into three slices (the "it is that moon" cue)
    D.append(f'<clipPath id="{p}sl"><rect x="0" y="{f(H + 4)}" width="512" height="5"/><rect x="0" y="{f(H + 12)}" width="512" height="6"/><rect x="0" y="{f(H + 21)}" width="512" height="6"/></clipPath>')
    mir = f"translate(0 {f(H + 3)}) scale(1 -0.3) translate(0 {f(-(my + mr))})"
    S.append(f'<g clip-path="url(#{p}sl)" opacity=".42"><g transform="{mir}"><path d="{dpath}" fill="{MOON}"{t}/></g></g>')
    # the road: glints in the moon's own colour
    for (x0, x1, y, h, o) in ROAD:
        S.append(f'<path d="{glint(x0, x1, y, h)}" fill="{MOON}" fill-opacity="{o}"/>')
    S.append(LANTERN)
    S.append(f'<circle cx="92" cy="358" r="60" fill="url(#{p}lg)"/>')
    S.append('</g>')
    # gilt frame: three bands + hero filigree
    S.append('<rect x="5" y="5" width="502" height="502" rx="107" fill="none" stroke="#5A4320" stroke-width="10"/>')
    S.append(f'<rect x="9" y="9" width="494" height="494" rx="103" fill="none" stroke="url(#{p}rim)" stroke-width="8"/>')
    S.append('<rect x="14" y="14" width="484" height="484" rx="98" fill="none" stroke="#F6E6B4" stroke-opacity=".45" stroke-width="2"/>')
    S.append(FILIGREE)
    return svg("Tsukimichi", D, S, 512)


def lantern():
    """Original Hingashi-style stone lantern on a small rock islet; moonlit right edges; gold window with two glints below."""
    g = "#060914"
    rim = f'stroke="{MOON}" stroke-opacity=".32" stroke-width="2" fill="none" stroke-linecap="round"'
    bx = 92.0
    P = []
    # islet and its dark reflection
    P.append(f'<path d="M22 452C30 438 52 430 78 431C104 428 132 432 152 442C160 446 164 450 166 454Z" fill="{g}"/>')
    P.append(f'<path d="M30 454H160C146 460 120 463 94 463C66 463 44 460 30 454Z" fill="{g}" fill-opacity=".55"/>')
    # base, post, platform, firebox, roof, finial
    P.append(f'<path d="M{f(bx - 30)} 438H{f(bx + 30)}L{f(bx + 24)} 426H{f(bx - 24)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 9)} 426H{f(bx + 9)}L{f(bx + 7)} 388H{f(bx - 7)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 28)} 388H{f(bx + 28)}L{f(bx + 22)} 377H{f(bx - 22)}Z" fill="{g}"/>')
    P.append(f'<rect x="{f(bx - 21)}" y="337" width="42" height="40" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 50)} 332C{f(bx - 40)} 328 {f(bx - 30)} 320 {f(bx - 19)} 306H{f(bx + 19)}C{f(bx + 30)} 320 {f(bx + 40)} 328 {f(bx + 50)} 332L{f(bx + 45)} 338H{f(bx - 45)}Z" fill="{g}"/>')
    P.append(f'<path d="M{f(bx - 8)} 306C{f(bx - 10)} 298 {f(bx - 6)} 290 {f(bx)} 283C{f(bx + 6)} 290 {f(bx + 10)} 298 {f(bx + 8)} 306Z" fill="{g}"/>')
    # moonlit right edges (the moon is up and to the right)
    P.append(f'<path d="M{f(bx + 19)} 306C{f(bx + 30)} 320 {f(bx + 40)} 328 {f(bx + 50)} 332" {rim}/>')
    P.append(f'<path d="M{f(bx + 21)} 340V375M{f(bx + 7.5)} 391V423M{f(bx + 6)} 296C{f(bx + 8)} 300 {f(bx + 8)} 303 {f(bx + 7)} 305" {rim}/>')
    # gold window, two panes, and its two glints on the water directly below the islet
    P.append(f'<rect x="{f(bx - 12)}" y="345" width="24" height="25" rx="2" fill="#E0B860"/>')
    P.append(f'<path d="M{f(bx)} 345V370" stroke="{g}" stroke-width="3"/>')
    P.append(f'<path d="{glint(bx - 20, bx + 18, 471, 12)}" fill="#E0B860" fill-opacity=".5"/>')
    P.append(f'<path d="{glint(bx - 10, bx + 24, 487, 12)}" fill="#E0B860" fill-opacity=".34"/>')
    return "".join(P)


def filigree():
    """Original corner filigree for the two top corners: a double hairline following the frame, ending in tiny crescents."""
    out = []
    for sx, tx in ((1, 0), (-1, 512)):
        tr = f'transform="translate({tx} 0) scale({sx} 1)"'
        out.append(f'<g {tr} fill="none" stroke="#F6E6B4" stroke-opacity=".3" stroke-width="2" stroke-linecap="round">'
                   f'<path d="M28 132V120C28 68 68 28 120 28H132"/><path d="M38 118C38 76 76 38 118 38" stroke-opacity=".18"/>'
                   f'<path d="M28 140a6 6 0 1 0 0 12a4.5 4.5 0 1 1 0 -12Z" fill="#F6E6B4" fill-opacity=".3" stroke="none"/>'
                   f'<path d="M140 28a6 6 0 1 0 12 0a4.5 4.5 0 1 1 -12 0Z" fill="#F6E6B4" fill-opacity=".3" stroke="none"/></g>')
    return "".join(out)


LANTERN = lantern()
FILIGREE = filigree()

if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    write("ready.svg", ready())
    write("ready-on-another-job.svg", ready_other_job())
    write("in-journal.svg", in_journal())
    write("blocked.svg", blocked())
    write("done-this-cycle.svg", done())
    write("completed.svg", completed())
    write("locked-out.svg", locked_out())
    write("not-checked.svg", not_checked())
    write("plugin-icon.svg", icon())
    print("lit centroid icon", LIT_C, "road x", ROAD_IX, "bright y", BRIGHT_Y, "glyph road x", ROAD_X)
    print("glints", len(ROAD))

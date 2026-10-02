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

GLYPH_MOON = (55.0, 47.0, 25.0, -0.3, 28.0)
GLYPH_H = 80.0
_ys, _xs = np.mgrid[0:128:0.25, 0:128:0.25]
_m = lit_mask_np(*GLYPH_MOON[:4], "right", GLYPH_MOON[4], _xs, _ys)
ROAD_X = float(_xs[_m].mean())

# icon road: (y centre, thickness, [(dx0, dx1, opacity)]) relative to ROAD_IX. Lens: ~lit width at the horizon,
# widest around the brightest row (y ~ 442), narrower in front; ragged edges, no two rows alike.
ROAD_ROWS = [
    (310, 12, [(-24, 22, .55)]),
    (331, 13, [(-33, 1, .62), (9, 30, .52)]),
    (355, 14, [(-42, -27, .45), (-19, 17, .72), (25, 44, .55)]),
    (382, 16, [(-52, -15, .7), (-6, 37, .8), (45, 57, .48)]),
    (411, 18, [(-63, -22, .76), (-12, 47, .9)]),
    (442, 20, [(-50, 5, .92), (14, 61, .86), (-74, -60, .45)]),
    (473, 22, [(-41, 13, .8), (24, 46, .6)]),
]
ROAD = [(ROAD_IX + a, ROAD_IX + b, y, h, o) for y, h, segs in ROAD_ROWS for a, b, o in segs]


def glint(x0, x1, yc, h):
    """A ripple glint: a full-bodied lens with soft points (max thickness ~h)."""
    L = x1 - x0
    k = 0.66 * h
    return (f"M{f(x0)} {f(yc)}C{f(x0 + 0.18 * L)} {f(yc - k)} {f(x1 - 0.18 * L)} {f(yc - k)} {f(x1)} {f(yc)}"
            f"C{f(x1 - 0.18 * L)} {f(yc + k)} {f(x0 + 0.18 * L)} {f(yc + k)} {f(x0)} {f(yc)}Z")


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
    hl = "".join(f"M{f(mx + abs(mk) * mr - 6 + i * 0)} {f(my - mr + 14 + i * 9)}h22" for i in range(0, 18))
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
    P.append(f'<path d="{glint(bx - 15, bx + 13, 471, 12)}" fill="#E0B860" fill-opacity=".5"/>')
    P.append(f'<path d="{glint(bx - 6, bx + 14, 486, 12)}" fill="#E0B860" fill-opacity=".34"/>')
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


"""Plugin icon: Menphina sigil x astrolabe. 512 master, the medallion is the silhouette."""
import math, random
from gen import lit_path, term_path, arc, f, STONE, GILT

CXO = CYO = 256
RO, RI = 250, 222            # medallion outer edge, well radius
MX, MY, MR, MT = 160, 150, 68, 0.5   # greater moon (waxing gibbous, lit right)
H = 278                      # horizon
SPEC = H + (H - MY)          # brightest point of the road (mirror of the moon's height)
ROT = 30                     # lit limb faces 30 degrees below horizontal (toward the set sun)
LIT_OFF = 0.4244 * (1 - MT) * MR
LIT_CX = MX + LIT_OFF * math.cos(math.radians(ROT))    # lit centroid of the rotated gibbous
AXIS = round((MX + LIT_CX) / 2, 1)      # within 10 of both disc centre and lit centroid
DX, DY, DR = 354, 102, 17    # Dalamud, the lesser (rose) moon - same sun, same phase
MOON = STONE["hi"]
GLADE = "#E6EBEE"   # area-weighted mean of the lit moon (hi + facet): the reflection takes the moon's colour
MIRROR_SY = 0.30


def almond(x0, cy, w, h):
    x1 = x0 + w
    k = .3 * w
    return (f"M{x0:.1f} {cy:.1f}C{x0 + k:.1f} {cy - h / 2:.1f} {x1 - k:.1f} {cy - h / 2:.1f} {x1:.1f} {cy:.1f}"
            f"C{x1 - k:.1f} {cy + h / 2:.1f} {x0 + k:.1f} {cy + h / 2:.1f} {x0:.1f} {cy:.1f}Z")


# hand-placed glints below the mirrored moon: (y, h, [(dx from axis, width, relative opacity)])
# heights 12->17 toward the viewer, gaps 4->6 (denser toward the horizon); lens 52->124->86 wide
ROWS = [
    (346, 12, [(-26, 44, 1.0)]),
    (364, 12, [(-42, 30, .8), (-4, 40, 1.0)]),
    (383, 13, [(-52, 38, .85), (-6, 30, 1.0), (32, 18, .7)]),
    (404, 14, [(-60, 30, .75), (-22, 56, 1.0), (42, 22, .75)]),
    (427, 15, [(-46, 40, .9), (4, 36, .85)]),
    (451, 16, [(-62, 22, .7), (-32, 50, .85), (26, 26, .7)]),
]
ROW_OP = [.62, .72, .84, .92, .84, .72]   # peaks at SPEC (y ~406)


def road():
    out = []
    for (y, h, dashes), rop in zip(ROWS, ROW_OP):
        for dx, w, rel in dashes:
            out.append((AXIS + dx + 3, y, w, h, round(rop * rel, 2)))
    return out


def build():
    d = []
    a = d.append
    a('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512">')
    a('<defs>')
    a(f'<clipPath id="pW"><circle cx="{CXO}" cy="{CYO}" r="{RI}"/></clipPath>')
    a(f'<linearGradient id="pSky" x1="0" y1="{CYO - RI}" x2="0" y2="{H}" gradientUnits="userSpaceOnUse">'
      '<stop offset="0" stop-color="#17163B"/><stop offset=".55" stop-color="#1B2650"/><stop offset="1" stop-color="#2E416C"/></linearGradient>')
    a(f'<linearGradient id="pSea" x1="0" y1="{H}" x2="0" y2="{CYO + RI}" gradientUnits="userSpaceOnUse">'
      '<stop offset="0" stop-color="#293A65"/><stop offset=".25" stop-color="#131C3A"/><stop offset="1" stop-color="#070A15"/></linearGradient>')
    a(f'<radialGradient id="pBloom" cx="{MX + 14}" cy="{MY}" r="160" gradientUnits="userSpaceOnUse">'
      f'<stop offset="0" stop-color="{MOON}" stop-opacity=".16"/><stop offset=".55" stop-color="{MOON}" stop-opacity=".05"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    a(f'<radialGradient id="pCol" cx="{AXIS}" cy="{SPEC}" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate({AXIS} {SPEC}) scale(80 140) translate({-AXIS} {-SPEC})">'
      f'<stop offset="0" stop-color="{MOON}" stop-opacity=".15"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient>')
    a(f'<linearGradient id="pRim" x1="70" y1="40" x2="442" y2="472" gradientUnits="userSpaceOnUse">'
      '<stop offset="0" stop-color="#F0D898"/><stop offset=".42" stop-color="#C9A766"/><stop offset=".6" stop-color="#A88B52"/><stop offset="1" stop-color="#7A5A2A"/></linearGradient>')
    lp = lit_path(MX, MY, MR, MT)
    a(f'<clipPath id="pL"><path d="{lp}"/></clipPath>')
    a(f'<clipPath id="pD"><circle cx="{MX}" cy="{MY}" r="{MR}"/></clipPath>')
    # mirrored moon: true reflection about H, flattened toward the horizon, cut into 3 ripple slices
    top = H + MIRROR_SY * (H - (MY + MR))
    bot = H + MIRROR_SY * (H - (MY - MR))
    hgt = (bot - top)
    slices = [(top - 1, hgt * .30 + 1), (top + hgt * .30 + 4.5, hgt * .30), (top + hgt * .60 + 8.5, hgt * .40)]
    for i, (y, h) in enumerate(slices):
        a(f'<clipPath id="pS{i}"><rect x="0" y="{y:.1f}" width="512" height="{h:.1f}"/></clipPath>')
    a('</defs>')

    a('<g clip-path="url(#pW)">')
    a(f'<rect x="0" y="0" width="512" height="{H}" fill="url(#pSky)"/>')
    a(f'<rect x="0" y="{H}" width="512" height="{512 - H}" fill="url(#pSea)"/>')
    a(f'<rect x="0" y="0" width="512" height="{H}" fill="url(#pBloom)"/>')

    # greater moon: earthshine disc (~1.24:1 against the sky), lit gibbous, one facet, terminator band
    a(f'<circle cx="{MX}" cy="{MY}" r="{MR}" fill="#42548D"/>')
    a(f'<g transform="rotate({ROT} {MX} {MY})">')
    stars = ((-50, -22, 3.2), (-37, -40, 2.4), (-27, -8, 3.6), (-42, 20, 2.6), (-30, 46, 3.2))
    a(f'<g clip-path="url(#pD)" opacity=".28" stroke="{STONE["band"]}" stroke-width="1.6" fill="none"><path d="'
      + "".join(("M" if i == 0 else "L") + f"{MX + x} {MY + y}" for i, (x, y, r) in enumerate(stars)) + '"/></g>')
    a(f'<g opacity=".34" fill="{STONE["band"]}">' + "".join(f'<circle cx="{MX + x}" cy="{MY + y}" r="{r}"/>' for x, y, r in stars) + '</g>')
    a(f'<path d="{lp}" fill="{MOON}"/>')
    a(f'<path d="M{MX - MR} {MY + 0.18 * MR:.1f}L{MX + MR} {MY - 0.30 * MR:.1f}L{MX + MR} {MY + MR}L{MX - MR} {MY + MR}Z" fill="{STONE["lit"]}" clip-path="url(#pL)"/>')
    a(f'<path d="{term_path(MX, MY, MR, MT)}" fill="none" stroke="{STONE["band"]}" stroke-width="6" clip-path="url(#pL)"/>')
    a(f'<circle cx="{MX}" cy="{MY}" r="{MR - .8}" fill="none" stroke="{STONE["band"]}" stroke-width="1.6" opacity=".5"/>')
    a('</g>')

    # Dalamud, the lesser moon: rose, same phase and light direction as Menphina's moon
    a(f'<circle cx="{DX}" cy="{DY}" r="{DR}" fill="#4A2A42"/>')
    a(f'<path d="{lit_path(DX, DY, DR, MT)}" fill="#D89A90" transform="rotate({ROT} {DX} {DY})"/>')

    # ---- sea ----
    a(f'<rect x="0" y="{H}" width="512" height="{512 - H}" fill="url(#pCol)"/>')
    a(f'<rect x="0" y="{H - 1}" width="512" height="2" fill="#DDE3F0" opacity=".2"/>')
    a(f'<path d="M{AXIS - 50} {H}H{AXIS + 54}" stroke="{GLADE}" stroke-width="2.6" stroke-linecap="round" opacity=".45"/>')
    for i, sx in enumerate((-2.5, 3, -1.5)):
        a(f'<g clip-path="url(#pS{i})"><circle cx="{MX}" cy="{MY}" r="{MR}" fill="#42548D" opacity=".4" '
          f'transform="translate({sx} {H}) scale(1 -{MIRROR_SY}) translate(0 {-H}) rotate({ROT} {MX} {MY})"/></g>')
        a(f'<g clip-path="url(#pS{i})"><path d="{lp}" fill="{GLADE}" opacity="{(.5, .56, .6)[i]}" '
          f'transform="translate({sx} {H}) scale(1 -{MIRROR_SY}) translate(0 {-H}) rotate({ROT} {MX} {MY})"/></g>')
    for x, y, w, h, op in road():
        a(f'<rect x="{x:.1f}" y="{y}" width="{w:.1f}" height="{h}" rx="{h / 2}" fill="{GLADE}" opacity="{op}"/>')
    # Dalamud's own faint glint under its lit side
    a(f'<rect x="{DX - 7}" y="{H + 9}" width="24" height="12" rx="6" fill="#D89A90" opacity=".24"/>')
    a('</g>')

    # ---- gilt astrolabe ring ----
    a(f'<circle cx="{CXO}" cy="{CYO}" r="{RO - 1.6}" fill="none" stroke="#6B5124" stroke-width="3.2"/>')
    band_r = (RO - 3.2 + RI) / 2
    a(f'<circle cx="{CXO}" cy="{CYO}" r="{band_r}" fill="none" stroke="url(#pRim)" stroke-width="{RO - 3.2 - RI}"/>')
    a(f'<path d="{arc(CXO, CYO, RO - 6, 165, 285)}" fill="none" stroke="#FFF3D1" stroke-width="2.4" stroke-linecap="round" opacity=".55"/>')
    a(f'<circle cx="{CXO}" cy="{CYO}" r="{RI + 1.2}" fill="none" stroke="#3A2A10" stroke-width="2.6"/>')
    # fine limb scale (every 5 degrees), engraved hairlines - hero only
    ticks = []
    for i in range(72):
        if i % 6 == 0:
            continue
        ang = math.radians(i * 5 - 90)
        r0, r1 = RO - 8, RO - (15 if i % 3 == 0 else 12)
        ticks.append(f"M{CXO + r0 * math.cos(ang):.1f} {CYO + r0 * math.sin(ang):.1f}L{CXO + r1 * math.cos(ang):.1f} {CYO + r1 * math.sin(ang):.1f}")
    a(f'<path d="{"".join(ticks)}" stroke="#5A4320" stroke-width="1.5" opacity=".42"/>')
    # 12 notches (the twelve moons of the Eorzean year): slots cut from the ring's inner edge
    for i in range(12):
        deg = i * 30 - 90
        big = i % 3 == 0
        depth, w = (19, 9) if big else (13, 7)
        x0 = CXO + RI - 2
        a(f'<g transform="rotate({deg} {CXO} {CYO})"><rect x="{x0}" y="{CYO - w / 2}" width="{depth}" height="{w}" rx="{w / 2 - .5}" fill="#2A1F0C"/>'
          f'<path d="M{x0 + 2} {CYO + w / 2 + .9}H{x0 + depth - 3}" stroke="#FFF3D1" stroke-width="1.2" opacity=".4"/></g>')
    a('</svg>\n')
    return "".join(d)


if __name__ == "__main__":
    print("axis", AXIS, "litcx", LIT_CX, "spec", SPEC)
    for r in road():
        print([round(v, 1) for v in r])

"""Astrologian's Orrery - round 2 glyph + icon generator. Writes SVGs into the concept folder."""
import math, pathlib, random

OUT = pathlib.Path(r"C:/Users/devon/Desktop/Tsukimichi (Main Repo)/docs/design/moon-v6/round2/astrologian-orrery")

# ---- palette tokens -------------------------------------------------------
SUMI = "#0B0F1C"
GILT = dict(deep="#6B5124", dark="#A88B52", mid="#C9A766", light="#E9D49C", spec="#FFF3D1")
STONE = dict(hi="#F3F0E6", lit="#E2E8F4", band="#C3CEE4", mid="#95A5C8", low="#5E6E97")
LAPIS = dict(deep="#1B2A57", lit="#253C7A")
EARTH = "#24335F"          # earthshine on the dark part (lapis, lifted)
AETHER = dict(hi="#9BE6FF", mid="#4FB3EA", deep="#1F5FA8")
BLOOD = dict(lit="#D2584E", deep="#8E2A2E", body="#4A1820")
EMBER = "#E7A35C"
PAPER = "#EFE4CC"
PEWTER = dict(light="#C3CEE4", dark="#5E6E97")

C, R = 64.0, 44.0


def f(v):
    return f"{v:.2f}".rstrip("0").rstrip(".")


def lit_path(cx, cy, r, t, side="R"):
    """t>0 gibbous (terminator bulges to the dark side by t*r), t<0 crescent, 0 = half."""
    a = abs(t) * r
    if side == "R":
        limb = f"M{f(cx)} {f(cy - r)}A{f(r)} {f(r)} 0 0 1 {f(cx)} {f(cy + r)}"
        sweep = 1 if t > 0 else 0
    else:
        limb = f"M{f(cx)} {f(cy - r)}A{f(r)} {f(r)} 0 0 0 {f(cx)} {f(cy + r)}"
        sweep = 0 if t > 0 else 1
    if a < 0.01:
        return limb + "Z"
    return limb + f"A{f(a)} {f(r)} 0 0 {sweep} {f(cx)} {f(cy - r)}Z"


def term_path(cx, cy, r, t, side="R"):
    a = abs(t) * r
    if side == "R":
        sweep = 1 if t > 0 else 0
    else:
        sweep = 0 if t > 0 else 1
    if a < 0.01:
        return f"M{f(cx)} {f(cy + r)}L{f(cx)} {f(cy - r)}"
    return f"M{f(cx)} {f(cy + r)}A{f(a)} {f(r)} 0 0 {sweep} {f(cx)} {f(cy - r)}"


def arc(cx, cy, r, a0, a1):
    p0 = (cx + r * math.cos(math.radians(a0)), cy + r * math.sin(math.radians(a0)))
    p1 = (cx + r * math.cos(math.radians(a1)), cy + r * math.sin(math.radians(a1)))
    large = 1 if (a1 - a0) % 360 > 180 else 0
    return f"M{f(p0[0])} {f(p0[1])}A{f(r)} {f(r)} 0 {large} 1 {f(p1[0])} {f(p1[1])}"


def bezel(metal, ticks=True, tick_op=0.5, cx=C, cy=C, r=R):
    """Keyline + two-arc bevel rim + hero-tier astrolabe ticks (hairlines)."""
    light, dark = (GILT["light"], GILT["dark"]) if metal == "gilt" else (
        (PEWTER["light"], PEWTER["dark"]) if metal == "pewter" else metal)
    s = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r + 5.6)}" fill="none" stroke="{SUMI}" stroke-width="2.6"/>',
         f'<path d="{arc(cx, cy, r + 2.2, 135, 315)}" fill="none" stroke="{light}" stroke-width="4.4"/>',
         f'<path d="{arc(cx, cy, r + 2.2, 315, 495)}" fill="none" stroke="{dark}" stroke-width="4.4"/>']
    if metal == "gilt":  # one specular streak, hero only (thin)
        s.append(f'<path d="{arc(cx, cy, r + 2.2, 205, 240)}" fill="none" stroke="{GILT["spec"]}" stroke-width="1.4" stroke-linecap="round" opacity=".8"/>')
    if ticks:
        tk = []
        for i in range(24):
            ang = math.radians(i * 15 - 90)
            major = i % 6 == 0
            minor = i % 2 == 1
            r0 = r + 8.2
            r1 = r + (14.5 if major else (10.4 if minor else 12.4))
            tk.append(f"M{f(cx + r0 * math.cos(ang))} {f(cy + r0 * math.sin(ang))}L{f(cx + r1 * math.cos(ang))} {f(cy + r1 * math.sin(ang))}")
        col = light
        s.append(f'<path d="{"".join(tk)}" stroke="{col}" stroke-width="1.3" stroke-linecap="round" opacity="{tick_op}"/>')
    return "".join(s)


def moon(uid, t, side="R", lit=STONE["hi"], facet=STONE["lit"], band=STONE["band"], dark=EARTH, cx=C, cy=C, r=R,
         facet_on=True):
    """Two-tone body: earthshine disc + lit shape, one facet split and a crisp terminator band (all clipped)."""
    lp = lit_path(cx, cy, r, t, side)
    s = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r)}" fill="{dark}"/>',
         f'<clipPath id="{uid}L"><path d="{lp}"/></clipPath>',
         f'<path d="{lp}" fill="{lit}"/>']
    if facet_on:  # one crystal facet across the lower lit part (cut moonstone, not a sphere gradient)
        s.append(f'<path d="M{f(cx - r)} {f(cy + 0.18 * r)}L{f(cx + r)} {f(cy - 0.30 * r)}L{f(cx + r)} {f(cy + r)}L{f(cx - r)} {f(cy + r)}Z" fill="{facet}" clip-path="url(#{uid}L)"/>')
    if abs(t) < 0.999:
        s.append(f'<path d="{term_path(cx, cy, r, t, side)}" fill="none" stroke="{band}" stroke-width="3.2" clip-path="url(#{uid}L)"/>')
    return "".join(s)


def constellation(pts, links, col=STONE["band"], op=0.42):
    lines = "".join(f"M{f(pts[a][0])} {f(pts[a][1])}L{f(pts[b][0])} {f(pts[b][1])}" for a, b in links)
    dots = "".join(f'<circle cx="{f(x)}" cy="{f(y)}" r="{f(rr)}"/>' for x, y, rr in pts)
    return (f'<g opacity="{op}"><path d="{lines}" stroke="{col}" stroke-width="1" fill="none"/>'
            f'<g fill="{col}">{dots}</g></g>')


def svg128(body):
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128" width="128" height="128">{body}</svg>\n'


# ---- states ----------------------------------------------------------------
QUIET = dict(lit="#C9D2E6", facet="#B4C0DA", band="#95A5C8")   # pewter-moonstone for non-act states


def almond(x0, cy, w, h):
    x1 = x0 + w
    k = .3 * w
    return (f"M{f(x0)} {f(cy)}C{f(x0 + k)} {f(cy - h / 2)} {f(x1 - k)} {f(cy - h / 2)} {f(x1)} {f(cy)}"
            f"C{f(x1 - k)} {f(cy + h / 2)} {f(x0 + k)} {f(cy + h / 2)} {f(x0)} {f(cy)}Z")


def ready():
    # the icon in miniature: gilt astrolabe medallion, waxing gibbous, broken moon road beneath
    s, cy = 0.88, 47.0
    glow = (f'<radialGradient id="rdG" cx="64" cy="{cy}" r="60" gradientUnits="userSpaceOnUse">'
            f'<stop offset=".6" stop-color="{STONE["lit"]}" stop-opacity=".2"/><stop offset="1" stop-color="{STONE["lit"]}" stop-opacity="0"/></radialGradient>'
            f'<circle cx="64" cy="{cy}" r="60" fill="url(#rdG)"/>')
    g = (f'<g transform="translate(64 {cy}) scale({s}) translate(-64 -64)">'
         + '<g transform="rotate(30 64 64)">' + moon("rd", 0.5)
         + constellation([(29, 52, 1.8), (35, 68, 1.5), (29, 81, 1.5)], [(0, 1), (1, 2)], op=.5) + '</g>'
         + bezel("gilt", tick_op=.65) + "</g>")
    # lit centroid of a t=.45 gibbous: cx + .233 r = 64 + 9 -> road axis 70 (between disc centre and lit centroid)
    # broken glints offset under the lit (lower-right) limb, never a centred "shoulders" bar
    road = [(36, 102, 38, 15, STONE["hi"], 1.0), (83, 103, 24, 13, GILT["light"], .9)]
    keyl = "".join(f'<rect x="{f(x - 2)}" y="{f(y - 2)}" width="{f(w + 4)}" height="{f(h + 4)}" rx="{f(h / 2 + 2)}" fill="{SUMI}" opacity=".85"/>' for x, y, w, h, c, o in road)
    rd = "".join(f'<rect x="{f(x)}" y="{f(y)}" width="{f(w)}" height="{f(h)}" rx="{f(h / 2)}" fill="{c}" opacity="{o}"/>' for x, y, w, h, c, o in road)
    return svg128(glow + g + keyl + rd)


def ready_other_job():
    # first-quarter moon; a job crystal (tide facet) set in the earthshine half
    cx, cy = 42.5, 64
    crystal = (f'<path d="M{cx} {cy - 25}L{cx + 11} {cy}L{cx} {cy + 25}L{cx - 11} {cy}Z" fill="{AETHER["mid"]}" stroke="{SUMI}" stroke-width="2.2" stroke-linejoin="round"/>'
               f'<path d="M{cx} {cy - 25}L{cx - 11} {cy}L{cx} {cy + 25}Z" fill="{AETHER["hi"]}"/>'
               f'<path d="M{cx - 11} {cy}H{cx + 11}" stroke="{AETHER["deep"]}" stroke-width=".9" opacity=".45"/>')
    return svg128(moon("rj", 0.0, **QUIET) + crystal + bezel("pewter"))


def in_journal():
    # thick waxing crescent; a journal bookmark ribbon hangs into the dark part from the top-left
    x0, x1, y0, y1 = 24, 40, 5, 62
    mid = (x0 + x1) / 2
    rib = (f'<path d="M{x0} {y0}H{x1}V{y1}L{mid} {y1 - 7}L{x0} {y1}Z" fill="{EMBER}" stroke="{SUMI}" stroke-width="2.2" stroke-linejoin="round"/>'
           f'<path d="M{x0 + 3.2} {y0 + 2}V{y1 - 4}" stroke="#FFE3B8" stroke-width="1" opacity=".5"/>'
           f'<path d="M{x1 - 3.2} {y0 + 2}V{y1 - 4}" stroke="#8E5A2A" stroke-width="1" opacity=".55"/>'
           f'<path d="{lit_path(mid, y0 + 13, 3.6, -0.35)}" fill="#8E5A2A" opacity=".5"/>')
    return svg128(moon("jr", -0.25, **QUIET) + bezel("pewter") + rib)


def blocked():
    # new moon: ashen earthshine disc, thin bright limb, charted stars engraved in the dark
    body = moon("bk", -0.70, lit=STONE["band"], band=STONE["mid"], dark="#2C3A66", facet_on=False)
    cons = constellation([(34, 46, 1.8), (48, 38, 1.4), (58, 54, 1.6), (44, 70, 1.4), (62, 84, 1.8)],
                         [(0, 1), (1, 2), (2, 3), (3, 4)], op=.5)
    return svg128(body + cons + bezel("pewter"))


def done():
    # waning half (lit on the left: it comes back) with a gilt check in the dark half
    chk = (f'<path d="M69.5 66L78.5 76.5L96 51" fill="none" stroke="{SUMI}" stroke-width="16.5" stroke-linecap="round" stroke-linejoin="round"/>'
           f'<path d="M69.5 66L78.5 76.5L96 51" fill="none" stroke="{GILT["light"]}" stroke-width="12" stroke-linecap="round" stroke-linejoin="round"/>')
    return svg128(moon("dn", 0.0, side="L", **QUIET) + bezel("pewter") + chk)


def completed():
    # quiet full moon, muted moonstone; engraved gilt bezel at hero size, lit limb band only
    cx = cy = C
    body = (f'<circle cx="{cx}" cy="{cy}" r="{R}" fill="#8B9AC0"/>'
            f'<clipPath id="cpL"><circle cx="{cx}" cy="{cy}" r="{R}"/></clipPath>'
            f'<path d="M{f(cx - R)} {f(cy + 0.18 * R)}L{f(cx + R)} {f(cy - 0.30 * R)}L{f(cx + R)} {f(cy + R)}L{f(cx - R)} {f(cy + R)}Z" fill="#6F7EA6" clip-path="url(#cpL)"/>'
            f'<path d="{arc(cx, cy, R - 2.5, 150, 300)}" fill="none" stroke="{STONE["band"]}" stroke-width="3" opacity=".7"/>')
    return svg128(body + bezel(("#B39A62", "#6B5124"), tick_op=.55))


def locked():
    # blood moon (Dalamud's red), sealed by a paper ofuda pasted across it, ends past the edge
    body = moon("lk", -0.62, lit=BLOOD["lit"], band="#A33A3A", dark=BLOOD["body"], facet_on=False)
    key = f'<circle cx="64" cy="64" r="{R + 1.3}" fill="none" stroke="{SUMI}" stroke-width="2.6"/>'
    ang = 76          # near-vertical talisman, pasted off-centre, never the 45-degree prohibition slash
    L, W = 60, 19
    band = (f'<g transform="translate(-9 0) rotate({ang} 64 64)">'
            f'<path d="M{64 - L} {64 - W / 2}H{64 + L}L{64 + L - 7} 64L{64 + L} {64 + W / 2}H{64 - L}L{64 - L + 7} 64Z" fill="{PAPER}" stroke="{SUMI}" stroke-width="2.4" stroke-linejoin="round"/>'
            f'<path d="M{64 - 22} 64H{64 + 22}" stroke="{BLOOD["deep"]}" stroke-width="1.4" opacity=".6"/>'
            f'<path d="M{64 - 32} {64 - 5}V{64 + 5}M{64 + 32} {64 - 5}V{64 + 5}" stroke="{BLOOD["deep"]}" stroke-width="1.2" opacity=".5"/>'
            "</g>")
    return svg128(body + key + band)


def mist(x0, y, w, h):
    # kasumi band: blunt rounded head, long tapering tail
    x1 = x0 + w
    return (f"M{f(x0 + h / 2)} {f(y)}H{f(x1 - 18)}C{f(x1 - 6)} {f(y)} {f(x1)} {f(y + h * .45)} {f(x1)} {f(y + h * .55)}"
            f"C{f(x1 - 10)} {f(y + h)} {f(x1 - 30)} {f(y + h)} {f(x1 - 40)} {f(y + h)}H{f(x0 + h / 2)}"
            f"A{f(h / 2)} {f(h / 2)} 0 0 1 {f(x0 + h / 2)} {f(y)}Z")


def not_checked():
    # oborozuki: hazy faint moon, two kasumi mist bands drifting past the edge
    body = (f'<circle cx="64" cy="64" r="{R}" fill="{STONE["low"]}" opacity=".40"/>'
            f'<path d="{lit_path(64, 64, R, 0.3)}" fill="{STONE["mid"]}" opacity=".28"/>')
    m = (f'<path d="{mist(2, 45, 92, 14)}" fill="{STONE["band"]}" opacity=".6"/>'
         f'<path d="{mist(34, 71, 92, 14)}" fill="{STONE["band"]}" opacity=".48"/>')
    return svg128(body + m)


STATES = {
    "ready": ready, "ready-on-another-job": ready_other_job, "in-journal": in_journal, "blocked": blocked,
    "done-this-cycle": done, "completed": completed, "locked-out": locked, "not-checked": not_checked,
}

if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    for k, fn in STATES.items():
        (OUT / f"{k}.svg").write_text(fn(), encoding="utf-8")
    import icon
    (OUT / "plugin-icon.svg").write_text(icon.build(), encoding="utf-8")
    print("ok")

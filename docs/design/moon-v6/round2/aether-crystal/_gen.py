"""Generator for the Aether Crystal Moon set (round 2). Writes 8 state SVGs + plugin-icon.svg."""
import math, pathlib, sys

OUT = pathlib.Path(sys.argv[1])
OUT.mkdir(parents=True, exist_ok=True)

# ---- tokens (Moon Road existing + 2 new) ----
SUMI = "#0B0F1C"
NIGHT = "#0F1424"
SILVER = "#DDE3F0"     # moonstone lit body (Moon Road SILVER)
MOON_HI = "#F4F6FB"    # facet highlight (lighter moonstone, same hue)
MIST = "#A9B2CC"
DUSK = "#7C86A8"
SHADOW = "#3A4363"
LAPIS = "#1B2A57"      # enamel well
TIDE = "#6F8FD0"
GILT = "#A88B52"
GILT_HI = "#D9BE82"
GILT_LO = "#6B5124"
AETHER = "#4FB3EA"     # new token 1
AETHER_HI = "#9BE6FF"
BLOOD = "#B8443F"      # new token 2 (Dalamud red)
BLOOD_DK = "#4A1A22"


def f(v):
    s = f"{v:.2f}".rstrip("0").rstrip(".")
    return s if s != "-0" else "0"


def pol(cx, cy, r, deg):
    a = math.radians(deg)
    return cx + r * math.cos(a), cy + r * math.sin(a)


def pts(lst):
    return " ".join(f"{f(x)},{f(y)}" for x, y in lst)


class Moon:
    """Phase geometry in a local frame where the lit side faces +x, rotated by phi (deg, + = clockwise/down)."""

    def __init__(self, uid, cx, cy, R, k, phi):
        # k: terminator ellipse rx / R. k<0 => crescent (ellipse bulges into lit side), k>0 => gibbous, 0 => half.
        self.uid, self.cx, self.cy, self.R, self.k, self.phi = uid, cx, cy, R, k, phi

    def rot(self):
        return f"rotate({f(self.phi)} {f(self.cx)} {f(self.cy)})"

    def mask(self):
        cx, cy, R, a = self.cx, self.cy, self.R, abs(self.k) * self.R
        ell = ""
        if self.k < 0:
            ell = f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(a)}" ry="{f(R + 1)}" fill="#000"/>'
        elif self.k > 0:
            ell = f'<ellipse cx="{f(cx)}" cy="{f(cy)}" rx="{f(a)}" ry="{f(R)}" fill="#fff"/>'
        return (f'<mask id="{self.uid}-lit" maskUnits="userSpaceOnUse" x="0" y="0" width="2000" height="2000">'
                f'<g transform="{self.rot()}"><circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="#fff"/>'
                f'<rect x="{f(cx - R - 2)}" y="{f(cy - R - 2)}" width="{f(R + 2)}" height="{f(2 * R + 4)}" fill="#000"/>{ell}</g></mask>')

    def local(self, t, on_limb=True, rr=None):
        c, sn = math.cos(math.radians(t)), math.sin(math.radians(t))
        if on_limb:
            r = rr if rr else self.R
            return self.cx + r * c, self.cy + r * sn
        a = abs(self.k) * self.R
        x = self.cx + (a * c if self.k < 0 else -a * c if self.k > 0 else 0)
        return x, self.cy + self.R * sn

    def lit(self, base, hi, term, lead, detail=True, term_w=4, limb_w=3, lead_w=1.2, split_op=.5, lead_op=.3,
            table_op=.55, inner_op=.35):
        """Lit body with crystal facets. Returns svg string (masked group)."""
        cx, cy, R = self.cx, self.cy, self.R
        a = abs(self.k) * R
        s = [f'<g mask="url(#{self.uid}-lit)">', f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R)}" fill="{base}"/>']
        g = [f'<g transform="{self.rot()}">']
        # culet point P in the middle of the lit band on the axis
        tx = self.local(0, False)[0]
        P = ((tx + cx + R) / 2, cy)
        if detail:
            # table facet (outer, lighter): P -> limb(-50) -> limb arc -> limb(50) -> P
            L1, L2 = self.local(-52), self.local(52)
            g.append(f'<path d="M{f(P[0])} {f(P[1])} L{f(L1[0])} {f(L1[1])} A{f(R)} {f(R)} 0 0 1 {f(L2[0])} {f(L2[1])} Z" fill="{hi}" fill-opacity="{table_op}"/>')
            # inner facet (darker) toward terminator: P -> T(-30) -> T(30)
            T1, T2 = self.local(-34, False), self.local(34, False)
            g.append(f'<path d="M{f(P[0])} {f(P[1])} L{f(T1[0])} {f(T1[1])} L{f(T2[0])} {f(T2[1])} Z" fill="{term}" fill-opacity="{inner_op}"/>')
        # terminator band (cool shadow) and limb highlight
        if self.k != 0:
            sweep = 1 if self.k < 0 else 0
            g.append(f'<path d="M{f(cx)} {f(cy - R)} A{f(a)} {f(R)} 0 0 {sweep} {f(cx)} {f(cy + R)}" fill="none" stroke="{term}" stroke-width="{f(term_w * 2)}"/>')
        else:
            g.append(f'<line x1="{f(cx)}" y1="{f(cy - R)}" x2="{f(cx)}" y2="{f(cy + R)}" stroke="{term}" stroke-width="{f(term_w * 2)}"/>')
        g.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(R - limb_w / 2)}" fill="none" stroke="#fff" stroke-opacity=".55" stroke-width="{f(limb_w)}"/>')
        if detail:
            L1, L2 = self.local(-52), self.local(52)
            T1, T2 = self.local(-34, False), self.local(34, False)
            g.append(f'<path d="M{f(L1[0])} {f(L1[1])} L{f(P[0])} {f(P[1])} L{f(L2[0])} {f(L2[1])}" fill="none" stroke="{lead}" stroke-opacity="{split_op}" stroke-width="{f(lead_w * 1.3)}" stroke-linejoin="round"/>')
            g.append(f'<path d="M{f(T1[0])} {f(T1[1])} L{f(P[0])} {f(P[1])} L{f(T2[0])} {f(T2[1])}" fill="none" stroke="{lead}" stroke-opacity="{lead_op}" stroke-width="{f(lead_w)}" stroke-linejoin="round"/>')
        g.append('</g>')
        s += g
        s.append('</g>')
        return "".join(s)

    def centroid(self, n=400):
        sx = sy = c = 0
        a = abs(self.k) * self.R
        for i in range(n):
            for j in range(n):
                x = -self.R + 2 * self.R * (i + .5) / n
                y = -self.R + 2 * self.R * (j + .5) / n
                if x * x + y * y > self.R ** 2:
                    continue
                inell = a > 0 and (x / a) ** 2 + (y / self.R) ** 2 <= 1
                lit = (x >= 0 and not inell) if self.k < 0 else (x >= 0) if self.k == 0 else (x >= 0 or inell)
                if not lit:
                    continue
                sx += x; sy += y; c += 1
        lx, ly = sx / c, sy / c
        p = math.radians(self.phi)
        return self.cx + lx * math.cos(p) - ly * math.sin(p), self.cy + lx * math.sin(p) + ly * math.cos(p)


def bevel(uid, cx, cy, r_out, r_in, kind, ticks=4, tick_op=.45, engraved=0):
    """Two-arc bevel rim: keyline, gradient ring (light top-left, shadow bottom-right), specular streak, inner keyline."""
    hi, mid, lo, spec = {"gilt": ("#F0DCA0", GILT_HI, GILT, "#FFF3D1"), "pewter": (DUSK, SHADOW, "#222840", MIST)}[kind]
    w = r_out - r_in
    rm = (r_out + r_in) / 2
    s = [f'<linearGradient id="{uid}-bev" gradientUnits="userSpaceOnUse" x1="{f(cx - r_out)}" y1="{f(cy - r_out)}" x2="{f(cx + r_out)}" y2="{f(cy + r_out)}">'
         f'<stop offset="0" stop-color="{hi}"/><stop offset=".55" stop-color="{mid}"/><stop offset="1" stop-color="{lo}"/></linearGradient>']
    body = [f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r_out + 2.5)}" fill="{SUMI}"/>',
            f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(rm)}" fill="none" stroke="url(#{uid}-bev)" stroke-width="{f(w)}"/>']
    a1, a2 = pol(cx, cy, r_out - 1.6, 200), pol(cx, cy, r_out - 1.6, 250)
    body.append(f'<path d="M{f(a1[0])} {f(a1[1])} A{f(r_out - 1.6)} {f(r_out - 1.6)} 0 0 1 {f(a2[0])} {f(a2[1])}" fill="none" stroke="{spec}" stroke-opacity=".8" stroke-width="1.6" stroke-linecap="round"/>')
    for i in range(ticks):
        ang = -90 + i * 360 / ticks
        p1, p2 = pol(cx, cy, r_in + .8, ang), pol(cx, cy, r_out - .8, ang)
        body.append(f'<line x1="{f(p1[0])}" y1="{f(p1[1])}" x2="{f(p2[0])}" y2="{f(p2[1])}" stroke="{SUMI}" stroke-opacity="{tick_op}" stroke-width="1.4"/>')
    for i in range(engraved):
        ang = -90 + 15 + i * 360 / engraved
        p1, p2 = pol(cx, cy, r_in + 1.4, ang), pol(cx, cy, r_out - 1.4, ang)
        body.append(f'<line x1="{f(p1[0])}" y1="{f(p1[1])}" x2="{f(p2[0])}" y2="{f(p2[1])}" stroke="{GILT_HI}" stroke-opacity=".55" stroke-width="1.1"/>')
    body.append(f'<circle cx="{f(cx)}" cy="{f(cy)}" r="{f(r_in)}" fill="none" stroke="{SUMI}" stroke-opacity=".85" stroke-width="1.5"/>')
    return "".join(s), "".join(body)


def svg(title, defs, body, vb=128):
    return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {vb} {vb}" width="{vb}" height="{vb}"><title>{title}</title>'
            f'<defs>{defs}</defs>{body}</svg>\n')


def write(name, text):
    (OUT / name).write_text(text, encoding="utf-8")


def cut_pts(cx, cy, R):
    oc = (cx - .1 * R, cy - .12 * R)
    angs = [-100, -12, 78, 168]
    rads = [.40, .36, .38, .34]
    table = [pol(oc[0], oc[1], R * r, t) for r, t in zip(rads, angs)]
    limb = [pol(cx, cy, R, t + 14) for t in angs]
    return table, limb, angs


def cut(cx, cy, R, stroke, op, fill=None, fill_op=0, w=1.1):
    """Cabochon cut: an off-centre kite table and four swirl facets (5 facets total)."""
    table, limb, angs = cut_pts(cx, cy, R)
    s = []
    if fill:
        s.append(f'<polygon points="{pts(table)}" fill="{fill}" fill-opacity="{f(fill_op)}"/>')
        # upper-left side facet, between table vertices 3 and 0 and their limb points
        p3, p0, l3, l0 = table[3], table[0], limb[3], limb[0]
        s.append(f'<path d="M{f(p3[0])} {f(p3[1])} L{f(l3[0])} {f(l3[1])} A{f(R)} {f(R)} 0 0 1 {f(l0[0])} {f(l0[1])} L{f(p0[0])} {f(p0[1])} Z" fill="{fill}" fill-opacity="{f(fill_op * .7)}"/>')
    s.append(f'<polygon points="{pts(table)}" fill="none" stroke="{stroke}" stroke-opacity="{op}" stroke-width="{f(w)}" stroke-linejoin="round"/>')
    for p, q in zip(table, limb):
        s.append(f'<line x1="{f(p[0])}" y1="{f(p[1])}" x2="{f(q[0])}" y2="{f(q[1])}" stroke="{stroke}" stroke-opacity="{op}" stroke-width="{f(w)}"/>')
    return "".join(s)


def lozenge(x, y, L, h, top, side, op, skew=0.0, uid=None):
    """Crystal glint: a long shard with pointed tips; upper facet `top`, lower facet `side`."""
    a = L * .3
    ty = y + h * .12  # tips sit a little low so the upper facet is the larger one
    t = [(x - L / 2, ty), (x - L / 2 + a + skew, y - h / 2), (x + L / 2 - a + skew, y - h / 2), (x + L / 2, ty)]
    b = [(x + L / 2, ty), (x + L / 2 - a * 1.1 - skew, y + h / 2), (x - L / 2 + a * 1.1 - skew, y + h / 2), (x - L / 2, ty)]
    return (f'<g opacity="{f(op)}"><polygon points="{pts(t)}" fill="{top}"/><polygon points="{pts(b)}" fill="{side}"/></g>')


# ===================== GLYPHS =====================
C = 64


def frame_states():
    out = {}

    # ---- READY: gilt bevel, crystal crescent, aether glow, crystal road beneath ----
    uid = "acr"
    cx, cy, Ro, Ri = 64, 48, 45.5, 38.5
    m = Moon(uid, cx, cy, Ri - .5, -0.10, 32)
    lcx, lcy = m.centroid(160)
    bdefs, bbody = bevel(uid, cx, cy, Ro, Ri, "gilt", ticks=4)
    defs = (bdefs + m.mask() +
            f'<radialGradient id="{uid}-glow" cx="{cx}" cy="{cy}" r="62" gradientUnits="userSpaceOnUse">'
            f'<stop offset=".74" stop-color="{AETHER_HI}" stop-opacity=".8"/><stop offset=".87" stop-color="{AETHER}" stop-opacity=".5"/>'
            f'<stop offset="1" stop-color="{AETHER}" stop-opacity="0"/></radialGradient>')
    body = [f'<circle cx="{cx}" cy="{cy}" r="62" fill="url(#{uid}-glow)"/>', bbody,
            f'<circle cx="{cx}" cy="{cy}" r="{Ri - .5}" fill="{LAPIS}"/>',
            m.lit(MOON_HI, "#FFFFFF", MIST, DUSK, inner_op=.25, split_op=.4, lead_op=.25)]
    # crystal road: 3 glints under the lit centroid (one at the horizon, two in front)
    rx = lcx
    body.append(lozenge(rx + 1, 104, 34, 13, MOON_HI, MIST, 1, skew=1))
    body.append(lozenge(rx - 13, 120, 38, 15, MOON_HI, MIST, 1, skew=-1))
    body.append(lozenge(rx + 19, 120.5, 22, 13, MOON_HI, MIST, .82, skew=1))
    out["ready"] = svg("Ready", defs, "".join(body)), (lcx, lcy)

    # ---- READY ON ANOTHER JOB: pewter, same crystal crescent, Tide job-crystal in the dark half ----
    uid = "arj"
    cx = cy = 64
    Ro, Ri = 56, 48.5
    m = Moon(uid, cx, cy, Ri - .5, -0.30, 32)
    bdefs, bbody = bevel(uid, cx, cy, Ro, Ri, "pewter")
    kx, ky, kh, kw = 41, 58, 48, 20
    top, bot = (kx, ky - kh / 2), (kx, ky + kh / 2)
    l, r = (kx - kw / 2, ky - kh * .12), (kx + kw / 2, ky - kh * .12)
    crys = (f'<polygon points="{pts([top, r, bot, l])}" fill="{TIDE}" stroke="{SUMI}" stroke-width="2" stroke-linejoin="round"/>'
            f'<polygon points="{pts([top, (kx, ky - kh * .12), bot, l])}" fill="{AETHER_HI}" fill-opacity=".55"/>'
            f'<line x1="{f(l[0])}" y1="{f(l[1])}" x2="{f(r[0])}" y2="{f(r[1])}" stroke="{SUMI}" stroke-opacity=".35" stroke-width="1.1"/>')
    body = [bbody, f'<circle cx="{cx}" cy="{cy}" r="{Ri - .5}" fill="{LAPIS}"/>', m.lit(SILVER, MOON_HI, MIST, DUSK), crys]
    out["ready-on-another-job"] = svg("Ready on another job", bdefs + m.mask(), "".join(body)), None

    # ---- IN JOURNAL: pewter, waxing gibbous, gilt bookmark ribbon hanging top-left ----
    uid = "ajn"
    m = Moon(uid, cx, cy, Ri - .5, 0.14, 8)
    bdefs, bbody = bevel(uid, cx, cy, Ro, Ri, "pewter")
    rdefs = (f'<linearGradient id="{uid}-rib" x1="0" y1="0" x2="1" y2="0"><stop offset="0" stop-color="{GILT_HI}"/>'
             f'<stop offset=".6" stop-color="{GILT}"/><stop offset="1" stop-color="{GILT_LO}"/></linearGradient>')
    x0, x1, y0, y1, notch = 18, 40, 0, 60, 9
    rib = [(x0, y0), (x1, y0), (x1, y1), ((x0 + x1) / 2, y1 - notch), (x0, y1)]
    ribbon = (f'<polygon points="{pts(rib)}" fill="url(#{uid}-rib)" stroke="{SUMI}" stroke-width="2.5" stroke-linejoin="round"/>'
              f'<line x1="{(x0 + x1) / 2}" y1="3" x2="{(x0 + x1) / 2}" y2="{y1 - notch - 3}" stroke="#FFF3D1" stroke-opacity=".35" stroke-width="1"/>')
    body = [bbody, f'<circle cx="{cx}" cy="{cy}" r="{Ri - .5}" fill="{LAPIS}"/>', m.lit("#8C96B5", SILVER, DUSK, SHADOW), ribbon]
    out["in-journal"] = svg("In journal", bdefs + m.mask() + rdefs, "".join(body)), None

    # ---- BLOCKED: new moon with earthshine; ashen crystal disc (unlit facets), thin bright limb ----
    uid = "abl"
    m = Moon(uid, cx, cy, Ri - .5, -0.70, 20)
    bdefs, bbody = bevel(uid, cx, cy, Ro, Ri, "pewter")
    body = [bbody, f'<circle cx="{cx}" cy="{cy}" r="{Ri - .5}" fill="{SHADOW}"/>',
            cut(cx, cy, Ri - .5, MIST, .18),
            m.lit(SILVER, MOON_HI, MIST, DUSK, detail=False, term_w=2)]
    out["blocked"] = svg("Blocked", bdefs + m.mask(), "".join(body)), None

    # ---- DONE THIS CYCLE: waning half (lit left), check in the dark half ----
    uid = "adn"
    m = Moon(uid, cx, cy, Ri - .5, 0.0, 180)
    bdefs, bbody = bevel(uid, cx, cy, Ro, Ri, "pewter")
    chk = (f'<path d="M73 65 L83 76 L101 52" fill="none" stroke="{SUMI}" stroke-width="17" stroke-linecap="round" stroke-linejoin="round" stroke-opacity=".6"/>'
           f'<path d="M73 65 L83 76 L101 52" fill="none" stroke="{SILVER}" stroke-width="13" stroke-linecap="round" stroke-linejoin="round"/>')
    body = [bbody, f'<circle cx="{cx}" cy="{cy}" r="{Ri - .5}" fill="{LAPIS}"/>', m.lit("#99A2BF", SILVER, DUSK, SHADOW), chk]
    out["done-this-cycle"] = svg("Done this cycle", bdefs + m.mask(), "".join(body)), None

    # ---- COMPLETED: quiet full moon, cabochon-cut moonstone, engraved bezel ----
    uid = "acp"
    bdefs, bbody = bevel(uid, cx, cy, Ro, Ri - 5, "pewter", ticks=0, engraved=12)
    R = Ri - 5.5
    body = [bbody, f'<circle cx="{cx}" cy="{cy}" r="{R}" fill="#6A7496"/>',
            cut(cx, cy, R, SHADOW, .35, fill=MIST, fill_op=.45),
            f'<circle cx="{cx}" cy="{cy}" r="{R - 1.5}" fill="none" stroke="{MIST}" stroke-opacity=".6" stroke-width="3"/>']
    out["completed"] = svg("Completed", bdefs, "".join(body)), None

    # ---- LOCKED OUT: blood moon, ofuda sealing band, no ring ----
    uid = "alk"
    m = Moon(uid, cx, cy, 55, -0.45, 40)
    band_len, band_h, ang = 116, 21, -38
    hl = band_len / 2
    n = 8
    band = [(-hl, -band_h / 2), (hl, -band_h / 2), (hl - n, 0), (hl, band_h / 2), (-hl, band_h / 2), (-hl + n, 0)]
    defs = m.mask()
    body = [f'<circle cx="{cx}" cy="{cy}" r="58.5" fill="{SUMI}"/>',
            f'<circle cx="{cx}" cy="{cy}" r="55" fill="{BLOOD_DK}"/>',
            cut(cx, cy, 55, BLOOD, .3),
            m.lit(BLOOD, "#E07A6E", "#7E2A2E", "#4A1A22", detail=False, term_w=3),
            f'<g transform="translate({cx} {cy}) rotate({ang})">'
            f'<polygon points="{pts(band)}" fill="#E8E0CC" stroke="{SUMI}" stroke-width="2.5" stroke-linejoin="round"/>'
            f'<rect x="{-hl + 14}" y="{-band_h / 2 + 3.5}" width="{band_len - 28}" height="{band_h - 7}" fill="none" stroke="{BLOOD}" stroke-opacity=".55" stroke-width="1.2"/>'
            f'</g>']
    out["locked-out"] = svg("Locked out", defs, "".join(body)), None

    # ---- NOT CHECKED: oborozuki, faint disc veiled by two mist wisps ----
    uid = "anc"
    defs = (f'<linearGradient id="{uid}-w1" x1="4" y1="0" x2="92" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MIST}" stop-opacity="0"/>'
            f'<stop offset=".35" stop-color="{MIST}" stop-opacity=".7"/><stop offset="1" stop-color="{MIST}" stop-opacity=".35"/></linearGradient>'
            f'<linearGradient id="{uid}-w2" x1="36" y1="0" x2="124" y2="0" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MIST}" stop-opacity=".3"/>'
            f'<stop offset=".6" stop-color="{MIST}" stop-opacity=".62"/><stop offset="1" stop-color="{MIST}" stop-opacity="0"/></linearGradient>')
    body = [f'<circle cx="{cx}" cy="{cy}" r="47" fill="none" stroke="{SUMI}" stroke-opacity=".35" stroke-width="5"/>',
            f'<circle cx="{cx}" cy="{cy}" r="46" fill="{MIST}" fill-opacity=".18" stroke="{MIST}" stroke-opacity=".42" stroke-width="3"/>',
            f'<path d="M4 56 C30 45 70 45 92 52 C70 64 30 65 4 56 Z" fill="url(#{uid}-w1)"/>',
            f'<path d="M36 80 C60 69 100 69 124 76 C100 88 60 89 36 80 Z" fill="url(#{uid}-w2)"/>']
    out["not-checked"] = svg("Not checked", defs, "".join(body)), None
    return out


# ===================== ICON =====================
ICON_ROWS = [  # (yc, h, [(dx, L, op)]) relative to the road axis; lens: narrow at horizon, widest near y 442, narrower in front
    (360, 12, [(2, 34, .5)]),
    (377, 12, [(-14, 40, .6), (26, 22, .45)]),
    (397, 14, [(-30, 30, .55), (10, 50, .8), (50, 14, .42)]),
    (421, 15, [(-42, 30, .6), (2, 58, .92), (46, 24, .6)]),
    (449, 17, [(-30, 64, .9), (32, 44, .84)]),
    (481, 18, [(-12, 54, .72), (38, 22, .5)]),
]


def icon():
    uid = "aic"
    H = 296
    mcx, mcy, R = 150, 150, 82
    m = Moon(uid, mcx, mcy, R, -0.35, 35)
    lcx, lcy = m.centroid(300)
    xa = lcx
    sx, sy = .45, .3  # flattened mirror of the crescent just under the horizon
    top_off = sy * (mcy + R - lcy)
    Y0 = H + 8 + top_off
    defs = [m.mask(),
            f'<clipPath id="{uid}-well"><rect x="18" y="18" width="476" height="476" rx="94"/></clipPath>',
            f'<linearGradient id="{uid}-gilt" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#E9CF8A"/><stop offset=".5" stop-color="#B08A45"/><stop offset="1" stop-color="#7A5A2A"/></linearGradient>',
            f'<linearGradient id="{uid}-spec" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="#F6E6B4" stop-opacity=".9"/><stop offset=".45" stop-color="#F6E6B4" stop-opacity=".25"/><stop offset="1" stop-color="#F6E6B4" stop-opacity="0"/></linearGradient>',
            f'<linearGradient id="{uid}-sky" x1="0" y1="18" x2="0" y2="{H}" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#0A1130"/><stop offset=".6" stop-color="#142152"/><stop offset="1" stop-color="#2C3F7C"/></linearGradient>',
            f'<linearGradient id="{uid}-sea" x1="0" y1="{H}" x2="0" y2="494" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#2A3C77"/><stop offset=".25" stop-color="#18265A"/><stop offset="1" stop-color="#0A112C"/></linearGradient>',
            f'<radialGradient id="{uid}-glow" cx="{mcx}" cy="{mcy}" r="160" gradientUnits="userSpaceOnUse"><stop offset=".5" stop-color="{AETHER}" stop-opacity=".30"/><stop offset=".7" stop-color="{AETHER}" stop-opacity=".1"/><stop offset="1" stop-color="{AETHER}" stop-opacity="0"/></radialGradient>',
            f'<radialGradient id="{uid}-hz" cx="{f(xa)}" cy="{H}" r="80" gradientTransform="translate(0 {H}) scale(1 .09) translate(0 -{H})" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{SILVER}" stop-opacity=".55"/><stop offset="1" stop-color="{SILVER}" stop-opacity="0"/></radialGradient>',
            f'<radialGradient id="{uid}-col" cx="{f(xa)}" cy="430" r="90" gradientTransform="translate({f(xa)} 430) scale(.72 1.3) translate(-{f(xa)} -430)" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MIST}" stop-opacity=".16"/><stop offset="1" stop-color="{MIST}" stop-opacity="0"/></radialGradient>',
            f'<mask id="{uid}-slice" maskUnits="userSpaceOnUse" x="0" y="0" width="512" height="512"><rect x="0" y="0" width="512" height="512" fill="#fff"/>'
            f'<rect x="0" y="{f(Y0 + 2)}" width="512" height="4" fill="#000"/><rect x="0" y="{f(Y0 + 14)}" width="512" height="5" fill="#000"/></mask>',
            ]
    body = [f'<rect x="0" y="0" width="512" height="512" rx="112" fill="#5A4320"/>',
            f'<rect x="10" y="10" width="492" height="492" rx="102" fill="url(#{uid}-gilt)"/>',
            f'<rect x="11" y="11" width="490" height="490" rx="101" fill="none" stroke="url(#{uid}-spec)" stroke-width="2"/>',
            f'<rect x="17" y="17" width="478" height="478" rx="95" fill="none" stroke="#3A2A12" stroke-width="2"/>']
    sc = [f'<rect x="0" y="0" width="512" height="{H}" fill="url(#{uid}-sky)"/>',
          f'<rect x="0" y="{H}" width="512" height="{512 - H}" fill="url(#{uid}-sea)"/>',
          f'<circle cx="{mcx}" cy="{mcy}" r="160" fill="url(#{uid}-glow)"/>']
    # a few quiet stars (sky detail, sub-pixel at 64 px)
    for x, y, r, o in ((300, 64, 2, .5), (352, 126, 1.5, .4), (424, 56, 2.2, .55), (262, 38, 1.4, .35), (458, 168, 1.6, .4), (330, 214, 1.3, .3), (60, 50, 1.4, .3)):
        sc.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{SILVER}" fill-opacity="{o}"/>')
    # far shores
    sc.append(f'<path d="M0 {H} L0 {H - 10} C30 {H - 12} 46 {H - 26} 70 {H - 24} C88 {H - 22} 96 {H - 12} 118 {H - 8} L128 {H} Z" fill="#0E1736"/>')
    sc.append(f'<path d="M318 {H} C340 {H - 6} 372 {H - 10} 400 {H - 8} C424 {H - 6} 440 {H - 2} 452 {H} Z" fill="#0E1736"/>')
    # distant aetheryte on the right shore with its own short reflection
    ax, ay = 384, H - 9
    sc.append(f'<polygon points="{ax},{ay - 30} {ax + 6},{ay - 12} {ax},{ay} {ax - 6},{ay - 12}" fill="{AETHER}" fill-opacity=".8"/>'
              f'<polygon points="{ax},{ay - 30} {ax},{ay} {ax - 6},{ay - 12}" fill="{AETHER_HI}" fill-opacity=".55"/>'
              f'<ellipse cx="{ax}" cy="{ay - 14}" rx="14" ry="4" fill="none" stroke="{AETHER_HI}" stroke-opacity=".3" stroke-width="1.4"/>'
              f'<rect x="{ax - 3}" y="{H + 3}" width="6" height="8" rx="3" fill="{AETHER}" fill-opacity=".3"/>')
    sc.append(f'<line x1="18" y1="{H}" x2="494" y2="{H}" stroke="#8FA0CC" stroke-opacity=".28" stroke-width="1.5"/>')
    # faint ripple hairlines (hero detail only)
    for y, x0, x1, o in ((318, 36, 112, .2), (342, 300, 372, .14), (366, 44, 120, .16), (414, 30, 92, .14), (330, 404, 470, .12)):
        sc.append(f'<line x1="{x0}" y1="{y}" x2="{x1}" y2="{y}" stroke="{MIST}" stroke-opacity="{o}" stroke-width="1.5" stroke-linecap="round"/>')
    sc.append(f'<rect x="{f(xa - 80)}" y="{H - 8}" width="160" height="16" fill="url(#{uid}-hz)"/>')
    # moon
    sc.append(f'<circle cx="{mcx}" cy="{mcy}" r="{R}" fill="#26356B"/>')
    sc.append(f'<circle cx="{mcx}" cy="{mcy}" r="{R - .8}" fill="none" stroke="{MIST}" stroke-opacity=".28" stroke-width="1.6"/>')
    sc.append(m.lit(SILVER, MOON_HI, MIST, DUSK, term_w=6, limb_w=4, lead_w=1.6, split_op=.5, lead_op=.32))
    # moon road
    sc.append(f'<rect x="{f(xa - 90)}" y="{H}" width="180" height="{494 - H}" fill="url(#{uid}-col)"/>')
    mir = f'translate({f(xa)} {f(Y0)}) scale({sx} {-sy}) translate({f(-lcx)} {f(-lcy)})'
    sc.append(f'<g mask="url(#{uid}-slice)"><g transform="{mir}" opacity=".52"><g mask="url(#{uid}-lit)"><circle cx="{mcx}" cy="{mcy}" r="{R}" fill="{SILVER}"/></g></g></g>')
    for yc, h, items in ICON_ROWS:
        for dx, L, op in items:
            sc.append(lozenge(xa + dx, yc, L, h, SILVER, "#B4BDD6", op, skew=(dx % 5) - 2))
    for (x, y, sx_, sy_) in ((34, 34, 1, 1), (478, 34, -1, 1), (34, 478, 1, -1), (478, 478, -1, -1)):
        sc.append(f'<path d="M{x} {y + 40 * sy_} L{x} {y + 18 * sy_} Q{x} {y} {x + 18 * sx_} {y} L{x + 40 * sx_} {y}" fill="none" stroke="#E9CF8A" stroke-opacity=".22" stroke-width="1.6"/>')
    body.append(f'<g clip-path="url(#{uid}-well)">{"".join(sc)}</g>')
    text = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512" width="512" height="512"><title>Tsukimichi</title>'
            f'<defs>{"".join(defs)}</defs>{"".join(body)}</svg>\n')
    write("plugin-icon.svg", text)
    return dict(moon_c=(mcx, mcy), R=R, lit_centroid=(round(lcx, 1), round(lcy, 1)), axis=round(xa, 1), H=H,
                bright=2 * H - mcy, glints=sum(len(r[2]) for r in ICON_ROWS), sliver=(round(Y0 - top_off, 1), round(Y0 + sy * (lcy - mcy + R), 1)))


if __name__ == "__main__":
    for name, (text, extra) in frame_states().items():
        write(f"{name}.svg", text)
        if extra:
            print(name, "lit centroid", [round(v, 1) for v in extra])
    print("icon", icon())

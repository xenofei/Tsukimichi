"""Plan v7 / 1.15 "Faces and icons": the 16 race silhouettes (8 races x 2 genders) and the neutral moon disc.

Head-and-shoulders busts, three-quarter-free (straight on), flat, one ink: the moonlit ink MoonstoneHigh #C9D3EA at .86,
drawn as a single group so overlapping parts never stack into a darker seam. viewBox 0 0 64 64; the plate (well, keyline)
is drawn by the plugin around them, so each file is only the figure. The figure's own light: none (flat, by design: a
silhouette reads as "a person of this kind", never as a face). Each race is told apart by the outline alone:
  Hyur      a plain human head; male short hair, female shoulder-length hair
  Elezen    a long narrow head and long ears swept back and up
  Lalafell  a large round head on a small body, low on the plate; female has two high buns
  Miqo'te   cat ears on top of the head
  Roegadyn  a broad neck and the widest shoulders; male a full beard, female a high knot
  Au Ra     horns from the temples: male long and swept back, female short and curving forward
  Hrothgar  a lion's mane behind the head and round ears on top; female a slimmer mane
  Viera     tall rabbit ears; female long hair, male short
Run: python make_silhouettes.py   (writes <race>-<gender>.svg and moon-disc.svg next to this script)
"""
import pathlib

HERE = pathlib.Path(__file__).resolve().parent
INK = "#C9D3EA"
ALPHA = ".86"

def body(width=26.0, top=47.0, neck=5.5, neck_top=34.0, cy=64.0):
    """Shoulders: a smooth trapezoid from the neck to the plate's foot; the neck a rounded column."""
    l, r = 32 - width, 32 + width
    sh = (f'M{l:.1f} {cy+2:.1f} C{l+1:.1f} {top+6:.1f} {32-width*0.55:.1f} {top:.1f} 32 {top:.1f} '
          f'C{32+width*0.55:.1f} {top:.1f} {r-1:.1f} {top+6:.1f} {r:.1f} {cy+2:.1f} Z')
    nk = f'M{32-neck:.1f} {top+2:.1f} L{32-neck*0.9:.1f} {neck_top:.1f} L{32+neck*0.9:.1f} {neck_top:.1f} L{32+neck:.1f} {top+2:.1f} Z'
    return f'<path d="{sh}"/><path d="{nk}"/>'

def head(cx=32.0, cy=26.0, rx=9.0, ry=11.0, chin=0.0):
    """A head: an ellipse, with an optional narrower chin (chin > 0 pulls the jaw in)."""
    if chin <= 0:
        return f'<ellipse cx="{cx}" cy="{cy}" rx="{rx}" ry="{ry}"/>'
    return (f'<path d="M{cx-rx:.1f} {cy:.1f} A{rx} {ry} 0 0 1 {cx+rx:.1f} {cy:.1f} '
            f'C{cx+rx:.1f} {cy+ry*0.55:.1f} {cx+rx*chin:.1f} {cy+ry:.1f} {cx:.1f} {cy+ry:.1f} '
            f'C{cx-rx*chin:.1f} {cy+ry:.1f} {cx-rx:.1f} {cy+ry*0.55:.1f} {cx-rx:.1f} {cy:.1f} Z"/>')

def mirror(path_d):
    """The same path reflected about x = 32 (for paired ears and horns)."""
    import re
    out, toks = [], re.findall(r"[A-Za-z]|-?\d+(?:\.\d+)?", path_d)
    cmd, i, coords = None, 0, []
    res = []
    idx = 0
    while idx < len(toks):
        t = toks[idx]
        if t.isalpha():
            cmd = t; res.append(t); idx += 1; continue
        if cmd in "MLCSQT":
            x, y = float(toks[idx]), float(toks[idx + 1]); res.append(f"{64 - x:.1f} {y:.1f}"); idx += 2
        elif cmd == "A":
            rx, ry, rot, la, sw, x, y = toks[idx:idx + 7]
            res.append(f"{rx} {ry} {-float(rot):g} {la} {1 - int(sw)} {64 - float(x):.1f} {float(y):.1f}"); idx += 7
        else:
            res.append(t); idx += 1
    return " ".join(res)

def long_hair(cx=32.0, cy=26.0, rx=8.4, ry=10.6, bottom=42.0, flare=3.6):
    """Long hair behind and beside the face: a crown over the head, sides that bulge a little past the cheeks, then fall
    to the shoulders and flare out to rounded tips; the gap between the hair and the neck is what makes it read."""
    L, R, top = cx - rx - 1.2, cx + rx + 1.2, cy - ry - 1.6
    return (f'<path d="M{L:.1f} {cy:.1f} C{L:.1f} {top+2:.1f} {cx-rx*0.6:.1f} {top:.1f} {cx:.1f} {top:.1f} '
            f'C{cx+rx*0.6:.1f} {top:.1f} {R:.1f} {top+2:.1f} {R:.1f} {cy:.1f} '
            f'C{R+1.2:.1f} {cy+7:.1f} {R+0.6:.1f} {bottom-6:.1f} {R+flare:.1f} {bottom:.1f} '
            f'C{R+flare-1.2:.1f} {bottom+2.4:.1f} {R-2.6:.1f} {bottom+1.6:.1f} {R-3.4:.1f} {bottom-1.6:.1f} '
            f'C{R-3.6:.1f} {cy+9:.1f} {cx+rx*0.5:.1f} {cy+3:.1f} {cx:.1f} {cy+2:.1f} '
            f'C{cx-rx*0.5:.1f} {cy+3:.1f} {L+3.6:.1f} {cy+9:.1f} {L+3.4:.1f} {bottom-1.6:.1f} '
            f'C{L+2.6:.1f} {bottom+1.6:.1f} {L-flare+1.2:.1f} {bottom+2.4:.1f} {L-flare:.1f} {bottom:.1f} '
            f'C{L-0.6:.1f} {bottom-6:.1f} {L-1.2:.1f} {cy+7:.1f} {L:.1f} {cy:.1f} Z"/>')

def mane(cx=32.0, cy=27.0, r=15.5, tufts=16, depth=2.6):
    """A lion's mane: a ring of soft tufts (alternating radii joined by curves), not a smooth disc."""
    import math
    pts = []
    for i in range(tufts * 2):
        a = -math.pi / 2 + i * math.pi / tufts
        rr = r if i % 2 == 0 else r - depth
        pts.append((cx + rr * math.cos(a), cy + rr * 1.02 * math.sin(a)))
    d = f"M{pts[0][0]:.1f} {pts[0][1]:.1f} "
    for i in range(1, len(pts) + 1, 2):
        c = pts[i % len(pts)]; e = pts[(i + 1) % len(pts)]
        d += f"Q{c[0]:.1f} {c[1]:.1f} {e[0]:.1f} {e[1]:.1f} "
    return f'<path d="{d}Z"/>'

def pair(d):
    return f'<path d="{d}"/><path d="{mirror(d)}"/>'

FIG = {}

# Hyur
FIG["hyur-male"] = body(25) + head(rx=8.8, ry=10.8, chin=0.55) + '<path d="M22.8 25 C22 15 27 12.6 32 12.8 C38.5 12.6 42.4 16 41.4 25 C40 19.6 36.5 18.2 32 18.6 C27 18.2 24 20 22.8 25 Z"/>'
FIG["hyur-female"] = body(22.5) + long_hair(bottom=42) + head(rx=8.4, ry=10.6, chin=0.5)
# Elezen
FIG["elezen-male"] = body(24, top=48) + head(rx=7.8, ry=11.8, chin=0.45) + pair("M24.6 25 L10.5 17.5 L24.6 29.5 Z") + '<path d="M24.4 23 C24 14 28 12 32 12 C36 12 40 14 39.6 23 C38 18 35 17.4 32 17.6 C29 17.4 26 18 24.4 23 Z"/>'
FIG["elezen-female"] = body(21.5, top=48) + long_hair(rx=7.6, ry=11.6, bottom=45, flare=3) + head(rx=7.6, ry=11.6, chin=0.42) + pair("M24.8 25 L11.2 18.5 L24.8 29 Z")
# Lalafell: a big round head, low, on a small body
FIG["lalafell-male"] = body(15, top=53, neck=4, neck_top=46) + head(cy=37, rx=11.5, ry=11.2) + pair("M21 37 L15.5 33.5 L21.4 40 Z") + '<path d="M20.6 35 C20.4 26 26 24.4 32 24.4 C38 24.4 43.6 26 43.4 35 C41 30.4 37 29.6 32 29.8 C27 29.6 23 30.4 20.6 35 Z"/>'
FIG["lalafell-female"] = body(14, top=53, neck=4, neck_top=46) + head(cy=37, rx=11.2, ry=11) + pair("M21.2 37 L15.8 33.8 L21.6 40 Z") + '<circle cx="23.4" cy="23.6" r="4.2"/><circle cx="40.6" cy="23.6" r="4.2"/><path d="M21 35 C20.8 26.4 26 24.8 32 24.8 C38 24.8 43.2 26.4 43 35 C40.6 30.6 37 29.8 32 30 C27 29.8 23.4 30.6 21 35 Z"/>'
# Miqo'te: cat ears on top
FIG["miqote-male"] = body(24) + head(rx=8.6, ry=10.6, chin=0.5) + pair("M24 18.5 L22.4 6.5 L30.2 14.2 Z") + '<path d="M23.2 25 C22.6 15.4 27 13 32 13.2 C37 13 41.4 15.4 40.8 25 C39.4 19.8 36.2 18.6 32 19 C27.8 18.6 24.6 19.8 23.2 25 Z"/>'
FIG["miqote-female"] = body(21.5) + long_hair(rx=8.2, ry=10.4, bottom=40, flare=3.2) + head(rx=8.2, ry=10.4, chin=0.48) + pair("M24.2 18.6 L22.8 7.2 L30.2 14.4 Z")
# Roegadyn: widest shoulders, thick neck
FIG["roegadyn-male"] = body(30, top=45, neck=8, neck_top=33) + head(rx=9.8, ry=11, chin=0.7) + '<path d="M23.4 29 C24 38 27.6 42 32 42.6 C36.4 42 40 38 40.6 29 C38.4 33 35.4 34.2 32 34.2 C28.6 34.2 25.6 33 23.4 29 Z"/><path d="M22.4 24 C21.8 15.6 26.4 13.4 32 13.6 C37.6 13.4 42.2 15.6 41.6 24 C40 19.6 36.6 18.8 32 19 C27.4 18.8 24 19.6 22.4 24 Z"/>'
FIG["roegadyn-female"] = body(28, top=46, neck=7, neck_top=33) + head(rx=9.2, ry=10.8, chin=0.6) + '<ellipse cx="32" cy="11.6" rx="5.2" ry="4.4"/><path d="M22.8 24 C22.2 15.8 26.6 13.8 32 14 C37.4 13.8 41.8 15.8 41.2 24 C39.6 19.8 36.4 19 32 19.2 C27.6 19 24.4 19.8 22.8 24 Z"/>'
# Au Ra: horns
FIG["aura-male"] = body(25) + head(rx=8.8, ry=10.8, chin=0.55) + pair("M24.2 20.4 C19 15.6 15.8 11 14.6 5.4 C19 9.4 22.8 12.4 26.6 15.6 Z") + '<path d="M23 24 C22.4 15.4 27 13 32 13.2 C37 13 41.6 15.4 41 24 C39.6 19.6 36.4 18.6 32 19 C27.6 18.6 24.4 19.6 23 24 Z"/>'
FIG["aura-female"] = body(22) + long_hair(bottom=38, flare=3) + head(rx=8.4, ry=10.6, chin=0.5) + pair("M23.6 19.4 C19.2 18 17.2 21.6 18.4 26.4 C19.4 23.2 21.2 22.4 23.8 23.4 Z")
# Hrothgar: mane and round ears
FIG["hrothgar-male"] = body(29, top=46, neck=8, neck_top=34) + mane(r=16.2, tufts=14, depth=2.8) + '<path d="M20 14.8 C18.6 9.6 21.4 7.4 25 9.4 C25.4 11.4 23.8 13.6 20 14.8 Z"/><path d="M44 14.8 C45.4 9.6 42.6 7.4 39 9.4 C38.6 11.4 40.2 13.6 44 14.8 Z"/>'
FIG["hrothgar-female"] = body(23, top=47, neck=6, neck_top=34) + mane(cy=29, r=13.2, tufts=12, depth=2) + long_hair(cy=28, rx=10, ry=11, bottom=44, flare=2.6) + '<path d="M21.6 16.4 C20.6 11.4 23.2 9.6 26.4 11.4 C26.6 13.4 25 15.4 21.6 16.4 Z"/><path d="M42.4 16.4 C43.4 11.4 40.8 9.6 37.6 11.4 C37.4 13.4 39 15.4 42.4 16.4 Z"/>'
# Viera: tall ears
FIG["viera-male"] = body(23, top=48) + head(cy=29, rx=8.2, ry=10.8, chin=0.5) + '<ellipse cx="26.6" cy="9" rx="3.2" ry="11.4" transform="rotate(-9 26.6 9)"/><ellipse cx="37.4" cy="9" rx="3.2" ry="11.4" transform="rotate(9 37.4 9)"/>' + '<path d="M23.8 27 C23.2 18.8 27.4 16.6 32 16.8 C36.6 16.6 40.8 18.8 40.2 27 C38.8 22.6 35.8 21.6 32 22 C28.2 21.6 25.2 22.6 23.8 27 Z"/>'
FIG["viera-female"] = body(21, top=48) + long_hair(cy=29, rx=7.8, ry=10.6, bottom=47, flare=3.4) + head(cy=29, rx=7.8, ry=10.6, chin=0.46) + '<ellipse cx="26.8" cy="9.4" rx="3" ry="11.2" transform="rotate(-8 26.8 9.4)"/><ellipse cx="37.2" cy="9.4" rx="3" ry="11.2" transform="rotate(8 37.2 9.4)"/>'

def write(name, inner, title):
    svg = (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" width="64" height="64"><title>{title}</title>'
           f'<g fill="{INK}" opacity="{ALPHA}">{inner}</g></svg>')
    (HERE / f"{name}.svg").write_text(svg, encoding="utf-8")

for k, v in FIG.items():
    race, gender = k.split("-")
    write(k, v, f"Giver silhouette: {race} {gender}")

# The neutral moon disc for non-humanoid givers (530 ids): a waxing crescent, lit on the right like the medals.
write("moon-disc", '<circle cx="32" cy="32" r="18" opacity=".32"/><path d="M32 14 A18 18 0 0 1 32 50 A9.5 18 0 0 0 32 14 Z"/>', "Giver silhouette: neutral moon (non-humanoid)")
print("wrote", len(FIG) + 1, "files")

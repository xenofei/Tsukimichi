"""Plan v7, owner point 5: the Completed medal with darker maria and a cool moon glow, in two tier sources.

Reads the shipped round-5 master (../../../moon-v6/round5/medallion-r5/completed.svg) and writes, next to this script:
  completed-v7.svg       the 96 and 128 px atlas tiers (and their 2x): maria, hearts, glow, three rim-lit craters, the young crater
  completed-v7-small.svg the 48 and 64 px tiers and the row tier: the same face without the three craters (at those sizes a
                         crater is 2-3 dark pixels with no rim, which reads as a hole); the young bright crater stays
Only the moon face changes: the well, rim, gilt check and corner pips are byte-identical.
Run: python make_completed.py
"""
import pathlib, re

HERE = pathlib.Path(__file__).resolve().parent
SRC = HERE.parents[2] / "moon-v6" / "round5" / "medallion-r5" / "completed.svg"
svg = SRC.read_text(encoding="utf-8")

# 1. Maria: MoonstoneMid #95A5C8 at .24-.36 -> a deeper basalt #56658C at 1.6x the opacity, and a little less blur
#    (2.45 -> 1.9) so they read as shapes at 32 px and up, not as a haze. Still blurred ellipses: soft value shifts, no
#    hard-edged holes (never cheese).
svg = svg.replace('<g fill="#95A5C8">', '<g fill="#56658C">')
def deepen(m):
    return 'fill-opacity="%.2f"' % min(0.62, float(m.group(1)) * 1.6)
face_start = svg.index('<g clip-path="url(#r5c-fcl)"><g filter="url(#r5c-mb)">')
face_end = svg.index('</g></g>', face_start) + len('</g></g>')
face = re.sub(r'fill-opacity="([0-9.]+)"', deepen, svg[face_start:face_end])
svg = svg[:face_start] + face + svg[face_end:]
svg = svg.replace('<filter id="r5c-mb" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="2.45"/></filter>',
                  '<filter id="r5c-mb" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="1.9"/></filter>')

DEFS = (
    # The moon glow: cool moonlight in the well around the face (not Ready's warm gold halo, and never outside the well).
    '<radialGradient id="r7c-gl" cx="60" cy="60" r="49" gradientUnits="userSpaceOnUse">'
    '<stop offset=".70" stop-color="#E2E8F4" stop-opacity=".26"/><stop offset=".82" stop-color="#C3CEE4" stop-opacity=".10"/>'
    '<stop offset="1" stop-color="#C3CEE4" stop-opacity="0"/></radialGradient>'
    '<filter id="r7c-cb" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="1.2"/></filter>'
    '<filter id="r7c-rb" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation=".35"/></filter>'
)
svg = svg.replace("</defs>", DEFS + "</defs>", 1)

# The glow sits on the well, under the face (clipped to the well, so the brass rim stays clean).
GLOW = '<circle cx="60" cy="60" r="49" fill="url(#r7c-gl)" clip-path="url(#r5c-wc)"/>'
anchor = '<circle cx="60" cy="60" r="35" fill="url(#r5c-fc)" filter="url(#r5c-ds)"/>'
svg = svg.replace(anchor, GLOW + anchor, 1)

# 2. Mare cores: the three largest seas (Imbrium, Serenitatis, Procellarum) get a darker heart, so the face has
#    depth instead of one flat grey.
CORES = ('<g clip-path="url(#r5c-fcl)"><g filter="url(#r7c-cb)" fill="#3F4B70">'
         '<ellipse cx="49.0" cy="47.0" rx="6.2" ry="4.2" transform="rotate(-18 49 47)" fill-opacity=".34"/>'
         '<ellipse cx="70.4" cy="59.6" rx="4.4" ry="3.2" transform="rotate(20 70.4 59.6)" fill-opacity=".30"/>'
         '<ellipse cx="40.6" cy="60.2" rx="4.0" ry="7.4" transform="rotate(8 40.6 60.2)" fill-opacity=".26"/>'
         '</g></g>')

# 3. Craters, light from the upper left (the moon's one light): a dark floor with its shadow on the upper-left inner
#    wall and a lit rim on the lower right. Small and few (three), off the maria's centres and clear of the check.
def crater(cx, cy, r):
    return (f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="#3F4B70" fill-opacity=".24"/>'
            f'<path d="M{cx - r * .92:.2f} {cy + r * .38:.2f}A{r} {r} 0 0 1 {cx + r * .38:.2f} {cy - r * .92:.2f}" fill="none" stroke="#2E3858" stroke-opacity=".42" stroke-width=".7"/>'
            f'<path d="M{cx + r * .92:.2f} {cy - r * .38:.2f}A{r} {r} 0 0 1 {cx - r * .38:.2f} {cy + r * .92:.2f}" fill="none" stroke="#F4F2EA" stroke-opacity=".55" stroke-width=".7"/>')
RIMMED = crater(76.5, 41.5, 2.4) + crater(45.0, 74.5, 1.9) + crater(58.5, 34.0, 1.4)
def craters(rimmed):
    return ('<g clip-path="url(#r5c-fcl)"><g filter="url(#r7c-rb)">'
           + (RIMMED if rimmed else "")
           # A young bright crater (a Tycho): a small bright floor with a faint halo (halved in review), no rays.
           + '<circle cx="51.5" cy="81.0" r="4.2" fill="#F4F2EA" fill-opacity=".05"/><circle cx="51.5" cy="81.0" r="1.3" fill="#F4F2EA" fill-opacity=".55"/>'
           + '</g></g>')

limb = '<g clip-path="url(#r5c-fcl)"><circle cx="60" cy="60" r="33.25"'
for name, rimmed, title in (("completed-v7.svg", True, "96 and 128 px tiers"), ("completed-v7-small.svg", False, "48 and 64 px tiers, row tier")):
    out = svg.replace(limb, CORES + craters(rimmed) + limb, 1)
    out = out.replace("<title>Completed</title>", "<title>Completed (plan v7, " + title + ")</title>", 1)
    (HERE / name).write_text(out, encoding="utf-8")
    print("wrote", HERE / name, len(out), "bytes")

"""Plan v7, owner point 5 (Revision 3): the Completed medal with darker maria, a cool moon glow and consistent craters.

Reads the shipped round-5 master (../../../moon-v6/round5/medallion-r5/completed.svg) and writes, next to this script:
  completed-v7.svg       the 96 and 128 px atlas tiers (and their 2x): maria, hearts, highlands, glow, three craters, the young crater
  completed-v7-small.svg the 48 and 64 px tiers and the row tier: the same face without the three craters (at those sizes a
                         crater is 2-3 dark pixels with no rim, which reads as a hole); the young bright crater stays
Only the moon face changes: the well, rim, gilt check and corner pips are byte-identical.

Revision 3 (owner: "part of the craters is blurry but part are well defined; needs consistency, and more realism"):
  * One edge rule for every feature on the face: edge softness = 0.1 x the feature's radius, clamped to 0.45-1.0 units.
    The maria (radius 3-12) are drawn at 1.0 (was 1.9, so they are no longer a haze next to crisp craters); their
    hearts at 0.8; every crater part at 0.45. Nothing is crisp and nothing is smeared.
  * The maria gain lobes, so they are irregular seas, not ellipses.
  * Every crater uses the same recipe, scaled by its radius, lit from the upper left (the medal's one light):
      raised rim, lit on the upper-left outer slope; floor a half-step darker than the highland around it;
      inner wall in shadow on the upper-left (a crescent, the wall that faces away from the light);
      inner wall lit on the lower right (a thinner crescent, the wall that faces the light).
    Built from filled crescents (circle minus offset circle), not stroked arcs, so every edge has the same profile.
  * Craters sit in the highlands (off every mare). The southern highlands are a touch brighter, as on the real Moon.
Run: python make_completed.py
"""
import pathlib, re

HERE = pathlib.Path(__file__).resolve().parent
SRC = HERE.parents[2] / "moon-v6" / "round5" / "medallion-r5" / "completed.svg"
svg = SRC.read_text(encoding="utf-8")

def soft(radius):
    """The one edge rule: softness (Gaussian stdDeviation, medal units) = 0.1 x radius, clamped to 0.45-1.0."""
    return max(0.45, min(1.0, 0.1 * radius))

# 1. Maria: MoonstoneMid #95A5C8 at .24-.36 -> a deeper basalt #56658C at .46 as one union, edge softness 1.0.
# The seas are drawn as one union at a single opacity (group opacity, every ellipse opaque), so overlaps never stack
# into darker spots: continuous seas with one tone, as the real maria read at full moon. Their hearts add the depth.
svg = svg.replace('<g fill="#95A5C8">', '<g fill="#56658C" opacity=".46">')
def deepen(m):
    return 'fill-opacity="1"'
face_start = svg.index('<g clip-path="url(#r5c-fcl)"><g filter="url(#r5c-mb)">')
face_end = svg.index('</g></g>', face_start) + len('</g></g>')
face = re.sub(r'fill-opacity="([0-9.]+)"', deepen, svg[face_start:face_end])
# The round-5 seas are separate ellipses; at x1.2 (Crisium excepted: it really is an isolated sea) and with the
# connecting lobes below, the near-side chain Procellarum-Imbrium-Serenitatis-Tranquillitatis reads as one body.
def grow(m):
    cx, cy, rx, ry = (float(m.group(i)) for i in (1, 2, 3, 4))
    k = 1.0 if (cx, cy) == (84.5, 53.7) else 1.2
    return '<ellipse cx="%s" cy="%s" rx="%.2f" ry="%.2f"' % (m.group(1), m.group(2), rx * k, ry * k)
face = re.sub(r'<ellipse cx="([0-9.]+)" cy="([0-9.]+)" rx="([0-9.]+)" ry="([0-9.]+)"', grow, face)
# Lobes: each large sea gets one or two smaller overlapping ellipses at its edge, so the outline is irregular.
LOBES = ('<ellipse cx="42.6" cy="43.6" rx="4.6" ry="3.2" transform="rotate(-30 42.6 43.6)" fill-opacity=".40"/>'
         '<ellipse cx="57.6" cy="46.4" rx="4.0" ry="3.0" transform="rotate(10 57.6 46.4)" fill-opacity=".36"/>'
         '<ellipse cx="74.6" cy="63.6" rx="3.8" ry="2.8" transform="rotate(35 74.6 63.6)" fill-opacity=".40"/>'
         '<ellipse cx="35.2" cy="50.6" rx="3.0" ry="4.8" transform="rotate(-6 35.2 50.6)" fill-opacity=".34"/>'
         '<ellipse cx="36.6" cy="68.4" rx="3.2" ry="4.0" transform="rotate(14 36.6 68.4)" fill-opacity=".32"/>'
         '<ellipse cx="47.6" cy="67.2" rx="3.4" ry="2.4" transform="rotate(-12 47.6 67.2)" fill-opacity=".34"/>'
         '<ellipse cx="67.0" cy="54.0" rx="4.0" ry="3.2" fill-opacity=".34"/>'
         '<ellipse cx="70.0" cy="66.4" rx="3.0" ry="3.2" fill-opacity=".34"/>'
         '<ellipse cx="44.4" cy="51.0" rx="4.2" ry="4.0" fill-opacity=".34"/>')
face = face.replace('</g></g>', re.sub(r'fill-opacity="[0-9.]+"', 'fill-opacity="1"', LOBES) + '</g></g>', 1)
svg = svg[:face_start] + face + svg[face_end:]
svg = svg.replace('<filter id="r5c-mb" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="2.45"/></filter>',
                  '<filter id="r5c-mb" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="%.2f"/></filter>' % soft(12))

def blur(fid, sd):
    return ('<filter id="%s" filterUnits="userSpaceOnUse" x="-20" y="-20" width="168" height="168" color-interpolation-filters="sRGB">'
            '<feGaussianBlur stdDeviation="%.2f"/></filter>' % (fid, sd))

DEFS = (
    # The moon glow: cool moonlight in the well around the face (not Ready's warm gold halo, and never outside the well).
    '<radialGradient id="r7c-gl" cx="60" cy="60" r="49" gradientUnits="userSpaceOnUse">'
    '<stop offset=".70" stop-color="#E2E8F4" stop-opacity=".26"/><stop offset=".82" stop-color="#C3CEE4" stop-opacity=".10"/>'
    '<stop offset="1" stop-color="#C3CEE4" stop-opacity="0"/></radialGradient>'
    + blur("r7c-cb", soft(10)) + blur("r7c-rb", soft(3)) + blur("r7c-hb", 2.4)
)

# 2. Mare hearts: the three largest seas (Imbrium, Serenitatis, Procellarum) get a darker core.
CORES = ('<g clip-path="url(#r5c-fcl)"><g filter="url(#r7c-cb)" fill="#3F4B70">'
         '<ellipse cx="49.0" cy="47.0" rx="7.4" ry="5.2" transform="rotate(-18 49 47)" fill-opacity=".22"/>'
         '<ellipse cx="70.4" cy="59.6" rx="5.4" ry="4.0" transform="rotate(20 70.4 59.6)" fill-opacity=".20"/>'
         '<ellipse cx="40.6" cy="60.2" rx="4.8" ry="8.8" transform="rotate(8 40.6 60.2)" fill-opacity=".18"/>'
         '</g></g>')

# 3. Southern highlands: a touch brighter than the north, as the real Moon's are (a soft albedo lift, not a light).
HIGHLANDS = ('<g clip-path="url(#r5c-fcl)"><g filter="url(#r7c-hb)">'
             '<ellipse cx="60" cy="84" rx="18" ry="8" fill="#EEF1F8" fill-opacity=".10"/>'
             '</g></g>')

# 4. Craters: one recipe, scaled by radius. Light from the upper left: direction (-1, -1)/sqrt2.
DEFS_CRATER = []
def crater(i, cx, cy, r):
    k = 0.30 * r  # the crescent offset: how far each wall's shadow or light reaches into the bowl
    m_out, m_sh, m_lit = "r7k%da" % i, "r7k%db" % i, "r7k%dc" % i
    DEFS_CRATER.append(
        # rim, lit on its upper-left outer slope: a ring 1.0r-1.22r, kept only where it faces the light
        '<mask id="%s"><rect width="128" height="128" fill="#000"/><circle cx="%.2f" cy="%.2f" r="%.2f" fill="#fff"/>'
        '<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#000"/></mask>' % (m_out, cx, cy, 1.22 * r, cx + 0.18 * r, cy + 0.18 * r, 1.22 * r)
        # inner shadow: the bowl minus the bowl shifted down-right -> a crescent on the upper-left inner wall
        + '<mask id="%s"><rect width="128" height="128" fill="#000"/><circle cx="%.2f" cy="%.2f" r="%.2f" fill="#fff"/>'
        '<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#000"/></mask>' % (m_sh, cx, cy, r, cx + k, cy + k, r)
        # inner light: the bowl minus the bowl shifted up-left by the same 0.30r -> a crescent on the lower-right inner wall,
        # as wide as the shadow, so the light-and-dark pair reads at 128 px (supervisor round 3)
        + '<mask id="%s"><rect width="128" height="128" fill="#000"/><circle cx="%.2f" cy="%.2f" r="%.2f" fill="#fff"/>'
        '<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#000"/></mask>' % (m_lit, cx, cy, r, cx - k, cy - k, r))
    return ('<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#F4F2EA" fill-opacity=".42" mask="url(#%s)"/>' % (cx, cy, 1.22 * r, m_out)
            + '<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#5E6E97" fill-opacity=".30"/>' % (cx, cy, r)
            + '<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#2E3858" fill-opacity=".75" mask="url(#%s)"/>' % (cx, cy, r, m_sh)
            + '<circle cx="%.2f" cy="%.2f" r="%.2f" fill="#F4F2EA" fill-opacity=".65" mask="url(#%s)"/>' % (cx, cy, r, m_lit))

# In the highlands, off every mare and clear of the check (which starts at 64,86).
# Supervisor round 3: radii x1.4 (3.4/2.8/2.4 -> 4.8/3.9/3.4) so each reads as a light-and-dark pair at 128 px.
RIMMED = crater(1, 77.0, 40.0, 4.8) + crater(2, 42.0, 80.5, 3.9) + crater(3, 58.0, 32.6, 3.4)
YOUNG = ('<circle cx="51.5" cy="81.0" r="4.2" fill="#F4F2EA" fill-opacity=".05"/>'
         '<circle cx="51.5" cy="81.0" r="1.3" fill="#F4F2EA" fill-opacity=".55"/>')

def craters(rimmed):
    # Every crater part shares one edge (r7c-rb, softness 0.45); the young crater, a bright albedo spot, does too.
    return '<g clip-path="url(#r5c-fcl)"><g filter="url(#r7c-rb)">' + (RIMMED if rimmed else "") + YOUNG + '</g></g>'

limb = '<g clip-path="url(#r5c-fcl)"><circle cx="60" cy="60" r="33.25"'
GLOW = '<circle cx="60" cy="60" r="49" fill="url(#r7c-gl)" clip-path="url(#r5c-wc)"/>'
anchor = '<circle cx="60" cy="60" r="35" fill="url(#r5c-fc)" filter="url(#r5c-ds)"/>'
body = svg.replace(anchor, GLOW + anchor, 1)
for name, rimmed, title in (("completed-v7.svg", True, "96 and 128 px tiers"), ("completed-v7-small.svg", False, "48 and 64 px tiers, row tier")):
    face_layers = HIGHLANDS + CORES + craters(rimmed)
    out = body.replace("</defs>", DEFS + ("".join(DEFS_CRATER) if rimmed else "") + "</defs>", 1)
    out = out.replace(limb, face_layers + limb, 1)
    out = out.replace("<title>Completed</title>", "<title>Completed (plan v7, " + title + ")</title>", 1)
    (HERE / name).write_text(out, encoding="utf-8")
    print("wrote", HERE / name, len(out), "bytes")

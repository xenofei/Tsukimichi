# Writes the design-v4 ornament kit: every piece at 1x and 2x (same viewBox, doubled pixel size).
import os

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "design-v4", "ornaments")
GILT, GILT_HI, MOON, MOON_HI, MOON_DEEP = "#A88B52", "#D9BE82", "#F2D27A", "#FFF0BE", "#D6B25A"
SILVER, MIST, DUSK, SHADOW, NIGHT, TIDE, ECL = "#DDE3F0", "#A9B2CC", "#7C86A8", "#3A4363", "#0F1424", "#6F8FD0", "#D68AA8"

GOLD = ('<linearGradient id="gold" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="%s"/>'
        '<stop offset=".45" stop-color="%s"/><stop offset="1" stop-color="%s"/></linearGradient>') % (MOON_HI, MOON, MOON_DEEP)

pieces = {}

pieces["crest"] = ((40, 40), "Rail crest: the moon over night water and the road of light it lays on the sea (tsukimichi). Top of the tab rail.", f'''
<defs>{GOLD}<mask id="cut"><rect width="40" height="40" fill="#fff"/><circle cx="24.2" cy="11.6" r="7.4" fill="#000"/></mask>
<radialGradient id="glow" cx="20" cy="15" r="14" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="{MOON}" stop-opacity=".22"/><stop offset="1" stop-color="{MOON}" stop-opacity="0"/></radialGradient></defs>
<circle cx="20" cy="20" r="18.5" fill="none" stroke="{GILT}" stroke-width="1" opacity=".75"/>
<circle cx="20" cy="20" r="16.6" fill="none" stroke="{GILT}" stroke-width=".5" opacity=".4"/>
<circle cx="20" cy="15" r="13" fill="url(#glow)"/>
<circle cx="20" cy="15" r="8.2" fill="url(#gold)" mask="url(#cut)"/>
<path d="M5.5 25.5 H34.5" stroke="{SILVER}" stroke-width="1" opacity=".55"/>
<path d="M14.5 28.2 H25.5" stroke="{MOON}" stroke-width="1.3" stroke-linecap="round"/>
<path d="M16.2 30.8 H23.8" stroke="{MOON}" stroke-width="1.2" stroke-linecap="round" opacity=".8"/>
<path d="M17.6 33.2 H22.4" stroke="{MOON}" stroke-width="1.1" stroke-linecap="round" opacity=".6"/>
<path d="M18.8 35.4 H21.2" stroke="{MOON}" stroke-width="1" stroke-linecap="round" opacity=".4"/>
<path d="M8 28.4 H11 M29 28.4 H32 M10 31 H12 M28 31 H30" stroke="{TIDE}" stroke-width=".8" stroke-linecap="round" opacity=".55"/>
''')

pieces["divider-moonroad"] = ((240, 12), "Moon-road divider: a hairline that fades in and out, broken by three phases (waxing, full, waning). Between the sections of a pane; drawn with primitives at runtime, this file is the reference.", f'''
<defs><linearGradient id="l" x1="0" x2="1"><stop offset="0" stop-color="{GILT}" stop-opacity="0"/><stop offset="1" stop-color="{GILT}" stop-opacity=".8"/></linearGradient>
<linearGradient id="r" x1="0" x2="1"><stop offset="0" stop-color="{GILT}" stop-opacity=".8"/><stop offset="1" stop-color="{GILT}" stop-opacity="0"/></linearGradient>{GOLD}
<mask id="wax"><rect width="240" height="12" fill="#fff"/><circle cx="106.2" cy="6" r="2.4" fill="#000"/></mask>
<mask id="wan"><rect width="240" height="12" fill="#fff"/><circle cx="133.8" cy="6" r="2.4" fill="#000"/></mask></defs>
<rect x="0" y="5.5" width="100" height="1" fill="url(#l)"/>
<rect x="140" y="5.5" width="100" height="1" fill="url(#r)"/>
<circle cx="108" cy="6" r="2.6" fill="{GILT_HI}" mask="url(#wax)"/>
<circle cx="120" cy="6" r="3.4" fill="url(#gold)"/>
<circle cx="132" cy="6" r="2.6" fill="{GILT_HI}" mask="url(#wan)"/>
''')

pieces["rule-fade"] = ((200, 4), "Section rule: the fading hairline that follows a section eyebrow to the pane edge (AddRectFilledMultiColor, Gilt 0.7 to 0).", f'''
<defs><linearGradient id="f" x1="0" x2="1"><stop offset="0" stop-color="{GILT}" stop-opacity=".7"/><stop offset="1" stop-color="{GILT}" stop-opacity="0"/></linearGradient></defs>
<rect x="0" y="1.5" width="200" height="1" fill="url(#f)"/>
''')

pieces["corner-mark"] = ((12, 12), "Corner mark (top-left; the other three corners are the same texture with flipped UVs). Hero banner frame, the selected Characters card, empty states. Never on every card.", f'''
<path d="M1.5 11.5 V1.5 H11.5" fill="none" stroke="{GILT}" stroke-width="1"/>
<path d="M4 8.5 V4 H8.5" fill="none" stroke="{GILT}" stroke-width=".6" opacity=".6"/>
<path d="M1.5 -.3 L3.3 1.5 L1.5 3.3 L-.3 1.5 Z" fill="{GILT_HI}"/>
''')

pieces["sigil-star"] = ((12, 12), "Four-point star sigil: section eyebrows, the path chart's band marks. Shared with the existing path chart.", f'''
<path d="M6 .5 L7.1 4.9 L11.5 6 L7.1 7.1 L6 11.5 L4.9 7.1 L.5 6 L4.9 4.9 Z" fill="{GILT_HI}"/>
<circle cx="6" cy="6" r="1" fill="{MOON_HI}"/>
''')

pieces["orbit-reference"] = ((32, 32), "Orbit ring reference: an official node icon (grey square here) inside a ring whose gold arc is completion, with the moon bead at the arc's head. Drawn with primitives at runtime; not shipped as art.", f'''
<rect x="7" y="7" width="18" height="18" rx="3" fill="#4B536E"/>
<circle cx="16" cy="16" r="13.5" fill="none" stroke="#5C6584" stroke-opacity=".55" stroke-width="2"/>
<circle cx="16" cy="16" r="13.5" fill="none" stroke="{MOON}" stroke-width="2" stroke-linecap="round" pathLength="100" stroke-dasharray="72 100" transform="rotate(-90 16 16)"/>
<g transform="rotate(259.2 16 16)"><circle cx="16" cy="2.5" r="2.6" fill="{NIGHT}"/><circle cx="16" cy="2.5" r="1.9" fill="{MOON_HI}"/></g>
''')


def glyph(body, desc):
    return ((24, 24), desc, body)


pieces["glyph-all-quests"] = glyph(f'''
<defs>{GOLD}<mask id="c"><rect width="24" height="24" fill="#fff"/><circle cx="14.6" cy="7.2" r="5.2" fill="#000"/></mask></defs>
<circle cx="12" cy="9" r="5.6" fill="url(#gold)" mask="url(#c)"/>
<path d="M3.5 16.5 H20.5" stroke="{SILVER}" stroke-width="1.1" opacity=".7"/>
<path d="M9 18.8 H15" stroke="{MOON}" stroke-width="1.3" stroke-linecap="round"/>
<path d="M10.4 21 H13.6" stroke="{MOON}" stroke-width="1.2" stroke-linecap="round" opacity=".7"/>
''', "All quests: the crest reduced to 24 px (moon, horizon, road).")

pieces["glyph-removed"] = glyph(f'''
<circle cx="12" cy="12" r="7.6" fill="{SHADOW}" opacity=".6"/>
<circle cx="12" cy="12" r="7.6" fill="none" stroke="{MIST}" stroke-width="1.4" stroke-dasharray="2.6 2.2"/>
<path d="M6.2 17.8 L17.8 6.2" stroke="{ECL}" stroke-width="1.7" stroke-linecap="round"/>
''', "Removed from the game: a veiled moon behind a broken ring, struck through (Eclipse text tone).")

pieces["glyph-chronicles"] = glyph(f'''
<defs>{GOLD}<mask id="c"><rect width="24" height="24" fill="#fff"/><circle cx="13.6" cy="3.2" r="2.3" fill="#000"/></mask></defs>
<path d="M2.8 9.2 Q7.4 7.6 12 9.6 Q16.6 7.6 21.2 9.2 V20 Q16.6 18.4 12 20.4 Q7.4 18.4 2.8 20 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M12 9.6 V20.4" stroke="{SILVER}" stroke-width="1"/>
<path d="M5 12 Q7.6 11.2 10 12.2 M5 14.8 Q7.6 14 10 15 M14 12.2 Q16.4 11.2 19 12 M14 15 Q16.4 14 19 14.8" stroke="{MIST}" stroke-width=".7" fill="none" opacity=".8"/>
<circle cx="12" cy="4.4" r="2.8" fill="url(#gold)" mask="url(#c)"/>
''', "Chronicles of a New Era: an open chronicle under a crescent.")

pieces["glyph-other"] = glyph(f'''
<defs>{GOLD}<mask id="a"><rect width="24" height="24" fill="#fff"/><circle cx="3.4" cy="12" r="3" fill="#000"/></mask>
<mask id="b"><rect width="24" height="24" fill="#fff"/><circle cx="20.6" cy="12" r="3" fill="#000"/></mask></defs>
<circle cx="5" cy="12" r="3.2" fill="{SILVER}" mask="url(#a)"/>
<circle cx="12" cy="12" r="3.8" fill="url(#gold)"/>
<circle cx="19" cy="12" r="3.2" fill="{SILVER}" mask="url(#b)"/>
<path d="M3 18.5 H21" stroke="{GILT}" stroke-width=".8" opacity=".7"/>
''', "Other quests: three phases on one line (a mixed group).")

pieces["glyph-special"] = glyph(f'''
<circle cx="12" cy="12" r="8.6" fill="none" stroke="{GILT}" stroke-width="1.1"/>
<path d="M12 4.6 L13.5 10.5 L19.4 12 L13.5 13.5 L12 19.4 L10.5 13.5 L4.6 12 L10.5 10.5 Z" fill="{MOON}"/>
<circle cx="12" cy="12" r="1.2" fill="{MOON_HI}"/>
''', "Special quests: the sigil star inside a ring.")

pieces["glyph-relic"] = glyph(f'''
<defs>{GOLD}<mask id="c"><rect width="24" height="24" fill="#fff"/><circle cx="12" cy="13.2" r="4.2" fill="#000"/></mask></defs>
<path d="M12 2.2 L13.4 5 V14.6 H10.6 V5 Z" fill="{SILVER}"/>
<path d="M12 5.2 V14" stroke="#B9C2D8" stroke-width=".6"/>
<circle cx="12" cy="15.2" r="4.6" fill="url(#gold)" mask="url(#c)"/>
<path d="M11.1 15.6 H12.9 V20 H11.1 Z" fill="{MIST}"/>
<circle cx="12" cy="21.1" r="1.3" fill="{MOON}"/>
''', "Weapon enhancement (relic lines): a blade whose guard is a crescent.")

pieces["glyph-endeavors"] = glyph(f'''
<circle cx="12" cy="12" r="8.4" fill="none" stroke="{SILVER}" stroke-width="1.2"/>
<path d="M12 3.6 V5.4 M12 18.6 V20.4 M3.6 12 H5.4 M18.6 12 H20.4" stroke="{MIST}" stroke-width="1"/>
<path d="M12 5.6 L14 12 H10 Z" fill="{MOON}"/>
<path d="M12 18.4 L10 12 H14 Z" fill="{DUSK}"/>
<circle cx="12" cy="12" r="1.3" fill="{NIGHT}" stroke="{MOON}" stroke-width=".8"/>
''', "Records of Unusual Endeavors: a compass whose north needle is gold.")

pieces["glyph-side-story"] = glyph(f'''
<path d="M6 4.5 H17 Q19 4.5 19 6.5 V18.5 H8 Q6 18.5 6 16.5 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M4.5 16.5 Q4.5 19.5 7.5 19.5 H17.5 Q15.8 19 15.8 16.8" fill="none" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M9 8.5 H16 M9 11 H16 M9 13.5 H13.5" stroke="{MIST}" stroke-width=".9"/>
<circle cx="16" cy="14.2" r="1.8" fill="{MOON}"/>
''', "Side story quests: a scroll sealed with a small moon.")

pieces["glyph-moonlit"] = glyph(f'''
<defs>{GOLD}<mask id="c"><rect width="24" height="24" fill="#fff"/><circle cx="12" cy="11.2" r="7.4" fill="#000"/></mask></defs>
<circle cx="12" cy="14" r="8" fill="url(#gold)" mask="url(#c)"/>
<path d="M12 3 L15.6 7.4 L12 13 L8.4 7.4 Z" fill="{SILVER}"/>
<path d="M8.4 7.4 H15.6 M12 3 L10.6 7.4 L12 13 L13.4 7.4 Z" fill="none" stroke="#B9C2D8" stroke-width=".6"/>
''', "Moonlit tab: a crescent cradling a treasure (rewards found nowhere else).")

pieces["glyph-flight"] = glyph(f'''
<defs>{GOLD}<mask id="c"><rect width="24" height="24" fill="#fff"/><circle cx="10.4" cy="10.2" r="6" fill="#000"/></mask></defs>
<circle cx="8" cy="12" r="6.4" fill="url(#gold)" mask="url(#c)"/>
<path d="M11.5 8 Q17 7 18.6 9 Q19.6 10.6 17.6 11" fill="none" stroke="{TIDE}" stroke-width="1.3" stroke-linecap="round"/>
<path d="M12 12.2 H20.5" stroke="{SILVER}" stroke-width="1.3" stroke-linecap="round"/>
<path d="M11.5 16 Q16.5 17 18 15.4" fill="none" stroke="{TIDE}" stroke-width="1.3" stroke-linecap="round"/>
''', "Flight fallback (when the game's aether current icon is not resolved): a crescent riding three currents of wind.")

pieces["glyph-plan-fallback"] = glyph(f'''
<circle cx="12" cy="12" r="8.4" fill="none" stroke="{TIDE}" stroke-width="1.3"/>
<path d="M12 7 V17 M7 12 H17" stroke="{TIDE}" stroke-width="2.4" stroke-linecap="round"/>
<circle cx="18" cy="6" r="2.2" fill="{MOON}"/>
''', "My blues fallback (the official blue-plus unlock quest icon is the first choice): a blue plus in a ring, with a moon.")

pieces["glyph-chronicles-of-light"] = glyph(f'''
<path d="M5 5.5 H15 L19 9.5 V19.5 H5 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M15 5.5 V9.5 H19" fill="none" stroke="{SILVER}" stroke-width="1"/>
<path d="M12 10.2 L12.8 13.2 L15.8 14 L12.8 14.8 L12 17.8 L11.2 14.8 L8.2 14 L11.2 13.2 Z" fill="{MOON}"/>
<path d="M12 8 V9 M12 19 V20 M6.8 14 H7.6 M16.4 14 H17.2" stroke="{GILT_HI}" stroke-width=".8" stroke-linecap="round"/>
''', "Chronicles of Light: a page with a star of light at its heart.")

pieces["glyph-hildibrand"] = glyph(f'''
<path d="M7.5 5 H16.5 V14 H7.5 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M4 14.2 H20" stroke="{SILVER}" stroke-width="1.4" stroke-linecap="round"/>
<path d="M7.5 11.6 H16.5" stroke="{MOON}" stroke-width="1.4"/>
<circle cx="15.2" cy="18.6" r="2.6" fill="none" stroke="{MIST}" stroke-width="1.1"/>
<path d="M17 20.4 L18.8 22" stroke="{MIST}" stroke-width="1.1" stroke-linecap="round"/>
''', "Hildibrand: the gentleman inspector's top hat (gold band) and a magnifier.")

pieces["glyph-festival"] = glyph(f'''
<path d="M12 2.5 V5" stroke="{MIST}" stroke-width="1"/>
<path d="M8.5 5 H15.5 M8.5 19 H15.5" stroke="{SILVER}" stroke-width="1.3" stroke-linecap="round"/>
<path d="M8.8 5.4 Q5.6 12 8.8 18.6 H15.2 Q18.4 12 15.2 5.4 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<ellipse cx="12" cy="12" rx="2.4" ry="4" fill="{MOON}" opacity=".9"/>
<path d="M12 19 V21.5" stroke="{GILT_HI}" stroke-width="1"/>
''', "Seasonal events: a paper lantern lit gold.")

pieces["glyph-deep-dungeon"] = glyph(f'''
<circle cx="12" cy="12" r="8.6" fill="none" stroke="{SILVER}" stroke-width="1.2"/>
<path d="M6.5 9 H10 V12 H13.5 V15 H17" fill="none" stroke="{MIST}" stroke-width="1.3" stroke-linejoin="round"/>
<circle cx="17.2" cy="16.8" r="1.6" fill="{MOON}"/>
''', "Deep dungeons: stairs descending inside a ring toward a gold light.")

pieces["glyph-region-coerthas"] = glyph(f'''
<path d="M2.5 19.5 L9.5 8 L13 13.5 L15.5 10 L21.5 19.5 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M7.6 11.2 L9.5 8 L11.4 11 L10 10.2 L9 11.4 Z M14.3 11.8 L15.5 10 L16.8 12 Z" fill="{SILVER}"/>
<circle cx="18" cy="5" r="2" fill="{MOON}"/>
''', "Coerthas (spans ARR and HW): a snow-capped range under a small moon.")

pieces["glyph-region-mordhona"] = glyph(f'''
<path d="M12 2.8 L15.6 9.5 L12 21 L8.4 9.5 Z" fill="#1E2437" stroke="{SILVER}" stroke-width="1.2" stroke-linejoin="round"/>
<path d="M8.4 9.5 H15.6 M12 2.8 V21" stroke="{MIST}" stroke-width=".7"/>
<path d="M5 14 L7.2 11 L8.6 16.5 Z M19 14 L16.8 11 L15.4 16.5 Z" fill="{TIDE}" opacity=".85"/>
<circle cx="12" cy="9.5" r="1.2" fill="{MOON}"/>
''', "Mor Dhona (spans ARR and HW): the great crystal with shards either side.")

os.makedirs(OUT, exist_ok=True)
for name, ((w, h), desc, body) in pieces.items():
    for scale, suffix in ((1, ""), (2, "@2x")):
        svg = (f'<svg xmlns="http://www.w3.org/2000/svg" width="{w*scale}" height="{h*scale}" viewBox="0 0 {w} {h}">\n'
               f'<title>{name} ({scale}x)</title>\n<desc>{desc} Original Tsukimichi art; rasterize into assets/ui/ornaments{suffix}.png.</desc>{body}</svg>\n')
        with open(os.path.join(OUT, f"{name}{suffix}.svg"), "w", encoding="utf-8") as f:
            f.write(svg)
print(len(pieces) * 2, "files written to", OUT)

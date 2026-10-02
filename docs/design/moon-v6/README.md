# Moon and icon redesign (plan v6 design team)

## Chosen: Sumi to Kinpaku (Direction C), with refinements taken from A and B. The final set stays C: a flat light shape in a sumi well, gold leaf only as line or seal, at most one mark per state, and Standard and High contrast sharing the same geometry. From A it takes two things. Done this cycle becomes a last-quarter half with the check on the ink, and the earthshine keyline is brightened into A's visible limb hairline (VeilLine #5C6584) so every disc keeps its edge on Night. From B it takes the calm cool-silver tone for Completed (#A3ACC2), and the icon's road gets B's broken, asymmetric shimmer instead of a centred pyramid.

All four focus-group participants picked C as their favourite. It scored highest on every sub-score that matters for this plugin: raider 8.5, new player 8, deuteranope 8.4, glamour 8.5, Japanese design 8.5. A averaged about 7 and B about 6.5.

Reasons C wins:
1. It is the only set where every pair splits by silhouette or mark at 14 to 16 px, in greyscale and under deuteranopia. I re-verified this in headless Chrome after the refinements, at 12 to 48 px on Night, #141414, a bright scene gradient and white, in colour, greyscale and a Vienot deuteranopia matrix: scratchpad/dirFinal/sheet.png.
2. The sumi disc gives every glyph its own dark seat over bright game scenes. That matters for the overlay, Flight and game panels. No separate drop shadow is needed.
3. Gold appears only on the two act-now states, as a line (Ready) or a seal (In journal). Nothing yellow is ever a fill, which ends the cheese read. B put brass on a state you cannot act on; A fused the gold ring onto a lit half, which read as a rind.
4. The new player could learn it with no legend: ring = go, outline = not on this job, check = done, bar = locked, dashes = unknown.

Rejected ideas and why:
- **A's crescent phase story:** it made Ready and In journal two gold-ringed moons that differ only by ring radius. All four participants flagged that pair.
- **A's beige Completed:** it read as a plate, cracker or brie.
- **B's soft bloom:** it turns into gold fuzz at 16 px. B's unmarked Done this cycle depends on an absence, and its outside bead breaks the rule that only Ready reaches outside the disc.
- **B's split brass arcs for Ready on another job:** two participants liked them. I keep them only as a documented fallback, because they put gold on a state you can't act on and put a second glyph outside the disc. The thicker hollow with a wash and the visible keyline fixed the "letter D" read in the renders: it now reads as a half moon in a circle at 16 px (dirFinal/zoom.png).

Phase mapping: Ready, In journal, Blocked, Locked out and Not checked keep the shapes players know today. Done this cycle changes from a waning gibbous to a waning half (下弦) plus a check, which pairs it with Ready's waxing half (上弦): "the same moon, set down until reset". Completed stays the only full disc. Players relearn one shape, not three.

### Refinements
- Ready: the gold-leaf ring goes from stroke 3 to stroke 4 (exactly 1 px at 16 px) at r 28.5. The sumi underlay widens from 5 to 6.5 so the ring holds on bright scenes (outer edge 31.75/32). With the larger ring and the dimmer Completed, Ready is clearly the loudest glyph in a 16-row mock list (dirFinal/rows.png). Kinpaku #E0B860 measures 9.8:1 on Night.
- In journal: the sliver is widened by moving the terminator semi-axis from 8.2 to 6 (lit about 65%). The diamond seal is kept at about C's size (8 x 13 units, about 2 x 3.25 px at 16 px), which answers the new player who asked for a larger mark. It is centred in the ink at x 18, with a 4-unit (1 px at 16 px) ink gap to the terminator and about 2.5 units plus the keyline to the limb. It never touches the light, so the lemon or teardrop merge the deuteranope and the raider described cannot happen.
- Ready on another job: kept as the kage-mon hollow half, not B's brass arcs, so gold stays act-now only and only Ready reaches outside the disc. The outline is thickened to 4.5 units, drawn inside the shape so its outer silhouette exactly matches Ready's half, about 1.1 px at 16 px. A 24% silver wash is added, as A did. With the brighter keyline it reads as a half moon in a circle, not as the letter D. B's split arcs remain the documented fallback if the owner disagrees.
- Done this cycle: becomes a silver waning half (下弦, lit LEFT) with a large round-capped silver check in the dark right half. The check is on ink, never cut out of the light, which removes the crack or scratch read. It uses A's placement, enlarged as the new player asked: stroke 4.5, spanning about 11 x 11 units, with a 3.75-unit gap to the terminator. Against In journal it differs by mirror image, shape (half versus gibbous), mark (check versus diamond) and tone, so it never depends on mirror image alone.
- Earthshine keyline: changed from Shadow #3A4363 (1.9:1 on Night, invisible) to VeilLine #5C6584 (3.2:1), stroke 1.5 units, with a 1 px minimum in code. This is A's ghost limb as a crisp hairline. Every half and gibbous now sits visibly in a disc on Night. Because the dark half is sumi (darker than Night), the cut-wheel wedge does not come back.
- Completed: lit tone dropped from #B4BDD0 (9.7:1) to #A3ACC2 (8.1:1). A column of finished rows now recedes behind Ready (rows.png). The two kasumi wisps are thinner (2.2 and 1.6 units), tapered, gently curved, and drawn in Veil #4A5270 instead of Shadow so they read as mist, not stripes or slots. They appear only at disc radius 16 px or more (Detail hero, Help hero, empty states) and at Flair Full or Quiet. Below that the glyph is a plain disc, which is already unique.
- Blocked: the ring uses the existing Mist #A9B2CC token (8.7:1) instead of a new #9AA3BE, so it is one token fewer. Stroke stays 3.5 units with a 1.5 px minimum. The sumi disc against Not checked's bodiless dashes keeps the two apart.
- Locked out is unchanged from C: a rose #C2707A ring plus a round-capped diagonal bar 6.5 units wide (minimum 2 px). Rose never carries meaning alone anywhere else; the deuteranope sees it as khaki-grey.
- Not checked is unchanged from C: five 44.7-degree dashes with a gap at 12 o'clock, no body, and a sumi underlay at 55%. It stays the faintest glyph (6.0:1).
- Icon: kept C's composition: value-graded sky, flat torinoko crescent lit right with horns pointing left, horizon hairline fading at both ends, gilt rim 12 at 85%, and the moon in the top 60%. The road is rebuilt as six asymmetric rows of 1, 2, 2, 3, 3 and 3 strips, so it shimmers rather than stacking into a pyramid. The top row is a single merged strip, and every strip is at least 12 units tall, so nothing is under 1.5 px at 64 px and the top rows don't dither at 32 px. The bottom row (y 466 to 487) clears the 112 tile corner. The gold gradient runs over the full road depth, ivory at the horizon to gold up front.

### Production plan

PRODUCTION PLAN: Sumi to Kinpaku glyphs and icon. This was research only; no repo file was touched. Evidence is in scratchpad\dirFinal\: gen.py, the eight state SVGs plus completed-small.svg, icon.svg, sheet.png, zoom.png, rows.png and icon.png.

1. RENDERING APPROACH: stay vector and procedural, no atlas
- Keep Tsukimichi/Ui/MoonGlyph.cs drawing with ImDrawList primitives. The new style is strictly flat: discs, rings, half-discs, ellipse-terminator polygons, one quad, one polyline, and dashed arcs. Every shape maps to AddCircleFilled, AddCircle, PathArcTo plus PathFillConvex, AddConvexPolyFilled and AddPolyline, which crisp-scale with Dalamud's global scale.
- Most of today's renderer gets deleted: the gradient mesh (Shade, GradientRings), MoonDetail (maria, craters, terminator glow, vignette), HighlightArc, the Glow disc stack, the notch and the seal dot. The draw cost per glyph roughly halves, which helps the 300-row Journal and Table.
- Textures or an atlas are unnecessary. They would blur at fractional UI scales and cannot follow High contrast or the token tests. The SVGs are the design master and the golden reference only. Keep them under docs/design/glyphs/ (v3), with render_preview.py updated to produce the contact sheet.
- Draw order: halo underlay → halo → sumi disc → keyline → lit shape → mark. For Not checked: dash underlay → dashes.

2. GEOMETRY IN PIXELS
Notation: r is the caller's disc radius in px. In the SVGs, r = 22 units. L is the lit radius, L = r − k, where k is the keyline width.
- Snap the centre to the pixel grid as today (Snap).
- Sumi disc: radius r, NightSunken #0B0F1C.
- Keyline: VeilLine #5C6584, width k = max(1, 0.068r), centred at r − k/2.

Per state:
- Ready:
  - Halo ring at radius 1.295r, width max(1.5, 0.18r), clamped to 3.5 px, in Kinpaku #E0B860.
  - Underlay: the same ring, width + 2 px, in NightSunken at 60%.
  - Enforce at least a 1 px gap between the disc edge and the halo's inner edge.
  - Lit right half of radius L in Torinoko #F5ECD2.
- Ready on another job:
  - Fill the right half of radius L in Geppaku #D4DAE6.
  - Then fill the inset half (radius L − w, straight edge moved right by w) in the pre-mixed opaque wash, Geppaku at 24% over NightSunken = #3A3F4D. Here w = max(1.25, 0.205r).
  - This avoids clipping in ImGui.
- In journal:
  - Lit right half plus a left half-ellipse with semi-axis a = 0.29L, in Torinoko.
  - Diamond centred at x = −0.636r, half-width max(1.25, 0.18r), half-height max(1.75, 0.295r), in Kinpaku.
  - Rule: keep an ink gap of at least 1 px between the diamond's right tip and the terminator. If it does not fit, first move the diamond left, then reduce a. At the minimum, a = 0, and In journal is a half plus a diamond, still distinct from Ready's half plus halo.
- Done this cycle:
  - Lit left half of radius L in Geppaku.
  - Check polyline through (0.273r, 0.023r), (0.477r, 0.25r) and (0.773r, −0.25r), in Geppaku, width max(1.25, 0.205r).
  - Draw the round caps and the join as AddCircleFilled of radius w/2, because ImGui polylines have no round caps.
- Completed:
  - Disc of radius L in CompletedLit #A3ACC2.
  - Wisps only when r ≥ 16 and Flair is not Plain: two tapered bands sampled along the curves in completed.svg (scaled by r / 22) in Veil #4A5270. Clamp each sample's x to the circle chord at that y, so no clip rect is needed.
- Blocked: ring at 0.92r, width max(1.5, 0.16r), in Mist #A9B2CC. No keyline.
- Locked out:
  - The same ring in Rose #C2707A.
  - Bar from −0.627r to +0.627r on the diagonal, width max(2, 0.295r), with round caps (two circles).
- Not checked:
  - No disc.
  - Five dashes of 44.7° each, with a 27.3° gap centred at 12 o'clock, at radius 0.93r, width max(1.25, 0.136r), in VeilText #8A93B0.
  - Underlay: the same dashes, width + 2 px, in NightSunken at 55%.

Size tiers:
- Smallest supported disc is r = 5 px (a 12 px box). Callers that draw smaller, such as Moonlit pips, clamp up to 5.
- Below r = 9: no wisps; diamond and check at their pixel minimums.
- r = 16 and up: wisps.
- Inline glyphs keep InlineRadiusFraction 0.42. Ready's halo reaches 1.44r, about 1.7 px past the box at a 16 px box. That is the same overflow the old 1.7r glow took, so row layouts need no change. In tight hosts (TabStrip, rail pips), clamp the halo radius to box/2 − width/2 and keep the 1 px gap.

3. TOKENS (Tsukimichi.Core/Ui/GlyphPalette.cs, GlyphTokens)
- Reused: NightSunken (sumi), VeilLine (keyline), Veil (wisps), Mist (Blocked ring), VeilText (dashes).
- Five new tokens:

| Token | Value | Contrast on Night |
|---|---|---|
| TorinokoHex | #F5ECD2 | 15.5:1 |
| KinpakuHex | #E0B860 | 9.8:1 |
| GeppakuHex | #D4DAE6 | 13.1:1 |
| CompletedLitHex | #A3ACC2 | 8.1:1 |
| GlyphRoseHex | #C2707A | 5.1:1 |

- Moon #F2D27A stays for text and chrome, where AA matters. Kinpaku is glyph metal only.
- Eclipse and EclipseText stay for text; Rose replaces Eclipse inside the glyph.

Enum and model changes:
- GlyphSilhouette: HalfLit covers Ready, Ready on another job (hollow) and Done (mirrored). GibbousLit covers In journal only.
- GlyphMark becomes: Halo, HollowHalf, Diamond, CheckOnInk, Wisps (hero only), DiagonalBar, Dashes, None.
- MarkOnLit becomes false for every state. Add a test asserting it, enforcing "no mark on light".
- Update the palette and contrast-ladder tests in Tsukimichi.Tests to the new tokens.
- Add a geometry test for the In journal ink gap of at least 1 px across r = 5 to 64.
- Help legend: lead with plain words, Ready = "You can do this now (gold ring)", and so on. Only Done this cycle changed shape. Note it in the CHANGELOG as "Done this cycle is now a half moon with a check".

4. HIGH CONTRAST (Settings › Display › Glyph palette)
Same silhouettes and marks: HC is now a token and stroke-minimum swap, not a different drawing. DrawContrast collapses into the main path with a style table.
- Keyline: Dusk #7C86A8, at least 1.5 px.
- Lit Ready and In journal: #FFF6DE.
- Kinpaku becomes Moon #F2D27A at width +0.5 px. The halo underlay is opaque.
- Geppaku becomes Silver #DDE3F0. The Ready on another job wash drops to 0, so the shape is a pure outline.
- Completed: #C2C9D8.
- Blocked ring: Silver, at least 2 px.
- Locked out: EclipseText #D68AA8, bar at least 2.5 px.
- Dashes: Mist, at least 1.5 px.
- Every stroke minimum rises by 0.5 px.
- Light hosts need no paper variant, because the sumi well already gives every glyph its own dark ground (verified on white in sheet.png). Only the Not checked underlay and dashes switch to HighContrastInkNavy on light hosts.

5. FLAIR
Glyph meaning never changes with Flair.
- Full: wisps at r ≥ 16. A one-shot "becomes Ready" pulse brightens the halo from 60% to 100% over 400 ms through Motion.cs, never looping, and respects reduced motion. Hero glyphs also get a 1 px Gilt hairline seat ring, decoration only.
- Quiet: wisps only; no pulse, no seat ring.
- Plain: flat glyphs only. Even the halo underlay is skipped when the host background is Night (it is invisible there anyway). The underlay stays on the overlay and game panels, where it carries legibility.

6. WHERE THEY APPEAR (disc radius at 100% scale)
- Journal tree (TreePane, TreeGlyphRadius about 6 to 7): the core list. Ready's halo is the only thing outside a disc, so act-now rows scan instantly.
- Table (TablePane, RowGlyphRadius 6): same as the tree. Dense mode clamps to r 5.
- Rail and tabs (TabStrip DrawHalo and DrawFilling, MainWindow, Orbit): completion gauges are not state glyphs. Restyle their track in Kinpaku at 35% and the complete cap in Torinoko, so the gauges share the material. A state glyph inside a pip uses the clamp rule.
- To-do overlay (TodoOverlay DrawInline over the game world): the sumi well and both underlays are mandatory, at every Flair level.
- Moonlit (MoonlitPane Draw, DrawInline and DrawVeiled pips at about r 4 to 5): clamp to r 5. DrawVeiled keeps alpha on top of the flat style.
- Flight (FlightPane): inline glyphs plus halos. The water background is bright-ish, so keep the underlays.
- Detail (DetailPane, DetailPane.Hero, r about 24 to 32): hero tier with wisps.
- PathChart nodes (PathGlyphRadius 6): row tier.
- Other surfaces: Help legend (hero tier plus plain words), Tutorial, EmptyState (large, Not checked or the chosen moon), RouteWindow, DiscoveryWindow, HoverHint, TonightCard, PlanPane, GamePanelShell, CharactersPane (inline) and DutyFinderPanel. All go through MoonGlyph, so no call site changes beyond the clamp.
- GlyphDebugWindow: add the contact sheet. All eight states at r 5, 6, 8, 12, 16 and 32, over Night, Abyss, NightRaised and a bright swatch, with a greyscale toggle, for the in-game check.

7. PLUGIN ICON
- Master: assets/icon.svg (new) holding the SVG above.
- Export to assets/icon.png at 512 and assets/icon-64.png at 64. Use headless Chrome, as gen.py's pages do, or resvg, in sRGB and without colour management.
- The csproj and pluginmaster already point IconUrl at assets/icon.png. The image is also packaged as images/icon.png, so only the PNGs change.
- Verified at 256, 128, 64, 48 and 32 on #101010, at 40% alpha (Disabled) and as a greyscale blur (icon.png). The crescent and gilt rim read at every size. The road reads as broken light down to 48 px and as a gold smear at 32 px, which is acceptable there.
- The Installed check (bottom right) covers only road tips and sea.
- The art is original geometry with no Square Enix assets.
- Release follows the usual process: version bump, changelog entry, tag, release.

8. OWNER APPROVAL POINTS
(a) Glyph gold changes from Moon #F2D27A to Kinpaku #E0B860. Chrome and text gold stay as they are.
(b) Done this cycle becomes a waning half plus a check.
(c) Completed wisps at hero size.
(d) Fallback for Ready on another job if it still reads as a "D" in game: B's split arcs in Geppaku, not brass.
(e) In-game check list: confirm at 100% and 150% UI scale, at 16 px over Limsa daylight and Thavnair, Ready versus In journal, the In journal diamond gap, and the Done check.

## Process

### Research

RESEARCH 1 (graphic designer): moon-phase iconography, FFXIV UI language, and why our moons read as cheese. Research only; no repo files were touched.

Method and limits: I read README, CHANGELOG 1.10.0, feature-plan-v5, docs/design/glyphs/proposal.md (v2.1), docs/design/moon-road-proposal.md, the ornament SVGs, Theme.cs tokens, MoonGlyph.cs and Tsukimichi.Core/Ui/MoonDetail.cs, and I looked at docs/design/glyphs/glyphs-v2-zoom.png. The session's web-search budget was already spent (200/200), so the outside references below come from established design knowledge plus two fetches: Wikipedia "Mon (emblem)" (https://en.wikipedia.org/wiki/Mon_(emblem)) and Erik Flowers' Weather Icons (https://erikflowers.github.io/weather-icons/). FFXIV UI facts are ones I'm confident of. Anyone who quotes exact in-game hex values should check them against a screenshot first.

== 1. WHY THE CURRENT MOONS READ AS CHEESE ==
The cause is the v2.1 "interior detail" spec (proposal.md §3.6, MoonDetail.cs), which was added because of the owner's earlier note "the moons need interior detail". Several food cues stack up:
a) Hue and chroma. Moon #F2D27A (about 44 degrees hue, high chroma) is the cheddar/butter band. A large area of it fills the glyph. Real moonlight is close to neutral (pale ivory, silver or blue-white), and the game's own sky moon is pale and cool.
b) A matte sphere gradient. The radial MoonHigh>Moon>MoonDeep gradient plus the rim vignette (limb darkening) turn a sign into a shaded object: a ball, a coin, a wheel with a rind. The proposal says the vignette "turns the coin into a sphere". A sphere of yellow is the problem.
c) Blotches and holes. The maria are blue-grey Umbra (#2C334A) at alpha 0.18-0.20 over yellow, which mixes to a muddy olive-brown smudge (mould or rind). The craters are rings with a shadowed wall and a highlit wall. That is exactly how illustrators draw Emmental "eyes". Three to six scattered features of similar size read as holes in cheese. On the real moon the maria are large, joined and lopsided, and the craters are invisible at icon size.
d) Silhouette. A yellow gibbous or half moon with a dark remainder looks like a cheese wedge seen from the side.
e) Register clash. The rest of the visual system (ornament kit, gap glyphs, divider, crest) is flat monoline brass with one accent. The moons are the only volumetric, textured, "illustrated object" elements, so they read as an item icon (food) rather than a sign.
f) Where it shows. Detail draws from r >= 12 and the gradient from r >= 9, so the worst cases are the big moons: HeaderMoonRadius = Icon(17) (detail hero), EmptyStateMoonRadius = Icon(28), the Help > Moon phases legend, and the large path-chart moons. Row moons (r < 9) are flat and much less cheesy. The fix is mostly at hero and legend sizes, plus the colour of the lit fill everywhere.

Key insight for the redesign: draw the moon as a LIGHT SOURCE, not a LIT OBJECT. Things that emit light have a flat, bright core, a crisp edge and a halo or bloom outside the edge. Things that are lit have shading, limb darkening and surface texture, and those are the cheese cues. Every elegant moon tradition (ukiyo-e, kamon, almanacs, SF Symbols) uses the first.

== 2. MOON-PHASE ICONOGRAPHY: REFERENCE FAMILIES ==
- Astronomical and almanac notation: new moon as a filled dark disc, full moon as an OPEN circle, quarters half-filled. Lit means bright or empty, with no texture. Old engraved plates (Hevelius and Cassini style) do add maria, but as fine line hatching and stipple, never as colour blotches. If a hero-size moon needs "detail", engraving line work is the safe route.
- Japanese crests (mon/kamon), the best fit for the name 月道 / 月影. Mon are essentially monochrome (colour is not part of the design), geometric, often framed in a circle, and formally shown as a light body on a dark ground (hinata-mon), which matches our Night UI. Moon mon include the crescent (mikazuki), moon-and-star (tsuki ni hoshi) and the sun-moon-star "three lights" (sankō). They show flat solid shapes and keylines, with no shading and no texture, and they stay elegant at any size.
- Ukiyo-e and Japanese painting moons (Yoshitoshi's One Hundred Aspects of the Moon, the hanafuda susuki/pampas card): a flat pale disc, never cratered, often crossed by a single hairline cloud wisp. This is the direct cure for cheese, and it suits a character named Tsukikage (moonlight / moon-shadow).
- Modern icon systems: Weather Icons ships 28 phases in two families, one where the pixels are the lit part and one where the pixels are the shadow. Apple SF Symbols moonphase.* and Material Symbols use a flat disc outline with a single-colour lit area. Their lessons: a keyline rim, a flat fill, the terminator as a true ellipse arc, one colour.
- Games and fantasy UI: the well-regarded moons (Hollow Knight's pale white, Celeste's crescent sigils, Okami's brush moon) are flat or brush-stroke, pale or white, and defined by silhouette. Avoid faces (tarot, Majora's Mask), rabbits (the tsukimi mochi rabbit is cute but kitsch here) and anything "magical girl" (glittery crescents).
- Common thread: a moon reads as a moon through SHAPE (crescent, terminator) and CONTEXT (night, stars, water), not through texture. A full disc alone is the weakest signifier (sun, coin, egg yolk, ball, cheese). Completed, as a solid gold disc, therefore needs a non-texture cue: a halo or bloom, a thin concentric inner keyline (crest or seal ring), or the almanac open-circle convention.

== 3. FFXIV'S OWN UI LANGUAGE ==
- Window themes: Dark (default), Light, Classic FF and Clear Blue. Dark has near-black, slightly warm translucent panels, thin bronze-gold hairline borders with a light top bevel, and ornament on title bars and frames only, never inside content. That matches our principle P4.
- Gold in FFXIV is METAL, not paint. Frames and borders use muted brass or bronze (ochre, roughly the #A88B52-#D9BE82 range of our Gilt/GiltHigh) with a metal gradient: light, dark, light again with a specular streak. That is unlike a sphere gradient. Bright saturated yellow-orange is kept for small SIGNALS: the MSQ meteor-shaped marker, the yellow "!" sidequest markers, the blue "+" feature-quest markers, duty-pop flashes. These markers are tiny, flat, 2-3 tones, with a dark outline and an outer glow so they read over any terrain. That is the register our state moons belong in. The painterly square action and status icons are the wrong model for 14-24 px glyphs.
- Type: Jupiter (engraved serif caps), TrumpGothic (condensed eyebrows), Axis (body), MiedingerMid (numerals). These are already adopted in Typography.
- Motifs we can borrow in spirit (original drawings only, nothing downloaded):
  - Sharlayan and Astrologian: armillary spheres, astrolabes, star globes, thin concentric rings with tick marks, four-point star sparkles. This is the most natural FFXIV idiom for a "moon road" tracker, and it matches the existing orbit-ring progress glyph and sigil star.
  - Allagan: precise hexagonal plates, straight circuit lines, cyan light on dark metal. It is technological, so use it sparingly, if at all. It could suit "system" concepts but clashes with the moon.
  - Eorzean calendar lore: the months are the 1st-6th ASTRAL and UMBRAL Moons, alternating. Astral/Umbral is the game's light/shadow pair (also Black Mage's Astral Fire and Umbral Ice), which maps cleanly onto lit and unlit and onto Tsukikage (moon-shadow).
  - Menphina, the moon goddess among the Twelve, and the Endwalker moon (Mare Lamentorum): the game's moon is shown grey-white and cool, not yellow.
  - Hingashi and Kugane: crest-like Japanese signage, a bridge to the kamon direction.
  - Crystals and aetherytes: faceted crystal with rotating rings, another "ring around a core" precedent.
- The game's sky moon is pale and cool with a soft halo. Our warm gold moon only works as a SIGNAL colour (rim, glow, small marks), not as a large fill.

== 4. CANDIDATE DIRECTIONS FOR THE FOCUS GROUP ==
All three keep the eight state silhouettes and the shape channels from proposal §3.7: the seal dot, the outer Ready ring, the dashed Unknown rim and the Eclipse bar.

A) "Moonlight" (light-source moons)
- Lit part as a flat, pale champagne/ivory light (for example about #F3EAD0, low chroma) with a crisp 1-1.5 px rim.
- Gold carried by the rim, a soft outer bloom (Ready, Completed) and the small marks, not by the fill.
- No gradient, vignette, maria or craters at any size.
- At hero size: an optional hairline terminator glow, or one cloud-wisp hairline (ukiyo-e).
- Risk: the gold-vs-silver contrast that separates Completed and Ready from DoneThisCycle and ReadyOnOtherJob gets smaller. Mitigate by keeping a clearly warm ivory against the cool #DDE3F0 silver and leaning on the existing shape channels. Test in greyscale.

B) "Kamon" (crest moons)
- Flat solid lit shape. The unlit part is a keyline only, or a very dark flat fill, with no shading.
- At hero and legend size, an outer double Gilt crest ring and, optionally, one four-point star (tsuki ni hoshi).
- Most elegant at 14 px, ties in with the name and character, and matches the monoline ornament kit.
- Completed becomes a "full moon crest": a lit disc with a thin inner concentric ring, so it reads as an emblem, not a coin.

C) "Astrolabe" (Sharlayan/AST armillary)
- The flat state moon sits inside a thin Gilt ring with 4-8 tick marks, at hero and empty-state sizes only. Rows stay plain A or B.
- Rich and very FFXIV, but busy below 24 px, so treat it as a size tier rather than a separate style.

Recommendation going into the focus group: B everywhere, with A's colour rule (lit = pale moonlight; gold = metal rim, glow and "act now" only), and C as the hero and empty-state tier. Delete the MoonDetail maria and craters outright. If the owner still wants "detail" at large sizes, offer engraved line work (3-5 hatch hairlines along the terminator, or fine stipple) in Gilt at low alpha, never colour blotches.

== 5. DON'TS (the cheese and kitsch checklist) ==
- No craters, maria, holes or blotches at any size.
- No radial "ball" gradient and no limb darkening.
- No brown or olive shadow tones on yellow.
- No large saturated yellow fill (keep saturated gold under roughly a third of the glyph's area, or at small sizes only).
- No drop shadows.
- No faces, rabbits or glitter.
- No more than two tones plus a glow per glyph.
- No mixing of two gradients.
- No ornament on every glyph: frame moments, not rows.

== 6. HOW TO TEST CANDIDATES (for the focus-group step) ==
- 5-second naming test: show a glyph in isolation to someone who doesn't play and ask "what is this?". Coin, cheese, egg, ball or sun is a fail; moon is a pass.
- Squint/blur test and greyscale test at r = 6, 9, 12, 17, 28 (the real UiMetrics radii).
- Side-by-side test in a real screenshot next to FFXIV's own quest markers and a Dark-theme frame.
- Backgrounds: Night, the Dalamud default (#141414) and a bright game scene (Todo overlay).
- Check the Help legend and the detail hero first; that is where the cheese lives.

Relevant files: C:\Users\devon\Desktop\Tsukimichi (Main Repo)\Tsukimichi\Ui\MoonGlyph.cs, C:\Users\devon\Desktop\Tsukimichi (Main Repo)\Tsukimichi.Core\Ui\MoonDetail.cs (maria and craters), C:\Users\devon\Desktop\Tsukimichi (Main Repo)\Tsukimichi\Ui\Theme.cs (Moon/MoonHigh/MoonDeep/Gilt tokens), C:\Users\devon\Desktop\Tsukimichi (Main Repo)\Tsukimichi\Ui\UiMetrics.cs (HeaderMoonRadius Icon(17), EmptyStateMoonRadius Icon(28)), C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\glyphs\proposal.md §3.6 (the spec that introduced the detail), C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\glyphs\glyphs-v2-zoom.png (shows the cheese reading at large sizes), C:\Users\devon\Desktop\Tsukimichi (Main Repo)\docs\design\moon-road\ornaments\ (the flat monoline brass family the moons should join).

Sources: https://en.wikipedia.org/wiki/Mon_(emblem) ; https://erikflowers.github.io/weather-icons/ . Everything else is from repo files and design knowledge, because web search was unavailable.

- Draw the moon as a light source, not a lit object: flat bright core, crisp edge, glow outside the edge. Shading, limb darkening and surface texture are what read as cheese, coin or ball.
- Cheese is a sum of cues: saturated butter-yellow (#F2D27A at about 44 degrees hue) over a large area, plus a matte sphere gradient, plus scattered round holes with a lit and a shadowed wall, plus olive-brown blotches, plus a wedge silhouette. Remove the texture and gradient entirely, and keep saturated gold small.
- Gold is metal or signal, never paint. As in FFXIV, brass/Gilt is for frames and rims, and bright gold is for small 'act now' marks with an outline and a glow, like the game's quest markers. The lit part of a moon should be pale moonlight (ivory or silver), not yolk.
- A moon is recognised by silhouette (crescent, terminator) and context (night, stars, water), never by craters. A full disc alone is the weakest moon sign, so give Completed a non-texture cue: bloom, an inner crest ring, or the almanac open circle.
- Glyphs at 12-24 px are flat silhouettes with one to two tones plus at most a glow. Any extra detail at hero sizes is engraved line work (hatching, stipple, a cloud-wisp hairline) in Gilt at low alpha, never colour blotches.
- Join the existing register: the ornament kit is flat monoline brass with one gold accent, so the moons must be flat and keylined too. Japanese kamon (monochrome, geometric, circle-framed, light-on-dark hinata style) is the ideal model and fits the names 月道 and 月影.
- Borrow FFXIV idioms in spirit with original drawings only: Sharlayan and Astrologian armillary rings, tick marks and four-point stars; the Astral/Umbral Moon calendar as the light/shadow duality; the cool, pale in-game moon. Use Allagan circuitry sparingly. Never download Square Enix art.
- Restraint is elegance: perfect geometry (true ellipse terminators), stroke weights snapped to the pixel grid, two tones per glyph, ornament on moments not rows, and no faces, rabbits, glitter or drop shadows.
- Keep the shape channels that already separate the eight states (seal dot, outer Ready ring, dashed Unknown rim, Eclipse bar). If the lit gold moves toward pale ivory, recheck the gold-vs-silver difference in greyscale.
- Validate with a 5-second naming test, squint and greyscale tests at the real radii (6, 9, 12, 17, 28), and side-by-side screenshots against FFXIV's own markers on Night, the Dalamud default background and bright game scenes. Start with the detail hero and the Help legend, where the cheese reading is worst.

RESEARCH 2 (graphic designer): technical limits on the moon glyphs and options for drawing them. Nothing in the repo was edited.

FILES READ
- C:\Users\devon\Desktop\Tsukimichi (Main Repo)\Tsukimichi\Ui\MoonGlyph.cs (911 lines; Standard plus high-contrast state moons, halo gauge, filling moon)
- ...\Tsukimichi.Core\Ui\GlyphPalette.cs (GlyphTokens, GlyphStyle, GlyphSilhouette/GlyphMark/RimStyle, the Standard / HighContrastDark / HighContrastLight palettes, Resolve picks the variant from host luminance at 0.179)
- ...\Tsukimichi\Ui\Marks.cs (check, cross, dash and pips; a moon only ever means a quest state or a fraction, per accessibility rule B2)
- ...\Tsukimichi\Ui\BeadRing.cs, ...\Tsukimichi\Ui\Orbit.cs, ...\Tsukimichi\Ui\OrnamentAtlas.cs
- ...\Tsukimichi.Core\Ui\StateNames.cs, ...\Tsukimichi.Core\Ui\MoonGeometry.cs (SegmentsFor: 12..96 segments, 0.30 px max error), ...\Tsukimichi\Ui\UiMetrics.cs
- ...\docs\glossary.md, ...\docs\design\glyphs\proposal.md, ...\docs\design\glyphs\imgui-notes.md, ...\docs\design\moon-road\gen_atlas.py, ...\assets\icons\README.md, ...\Tsukimichi\Ui\GlyphDebugWindow.cs

1. HOW THE GLYPHS ARE DRAWN NOW
- Everything is ImDrawList vector geometry with no textures. Moons are built from AddConvexPolyFilled discs and lenses (a crescent is a disc with an overlay polygon on top, because ImGui only fills convex shapes) plus AddCircle rims drawn inside the edge at r − o/2. The rim width o is clamp(0.12 r, 1.5, 3).
- Shading and detail are custom vertex-coloured meshes (PrimReserve / PrimWriteVtx using the font atlas's white-pixel UV):
  - a radial gradient from r 9;
  - from r 12: 3 maria, 3 craters, a terminator band and a vignette (at most MoonDetail.PrimitiveBudget primitives);
  - a highlight arc on Completed from r 16.
- Snap() puts the centre on an integer or half-integer pixel so the rim sits on the pixel grid.
- High contrast (DrawContrast) is flat: a keyline ground disc (0.08 r, at least 1 px), lit part and rim on a luminance ladder (rim 0.16 r clamped 2–4 px; Blocked 0.28 r clamped 3–6), and one mark per state (bar, hollow bar, large seal, check, thick diagonal). No gradient, detail or glow.
- Actual sizes. With global scale 1, UiScale 1.15 and IconScale 1.25, IconScale works out to about 1.44:

| Use | Radius | Glyph size | What it gets |
|---|---|---|---|
| RowGlyphRadius = Icon(6) | ≈ 8.6 px | ≈ 17 px | flat, no gradient; Ready uses the small 1 px ring |
| Playing surfaces (Todo overlay, Nearby) | — | at least 14 px | — |
| Tree halo | R 12 | 24 px | — |
| HeaderMoonRadius = Icon(17) | ≈ 24 px | ≈ 49 px | full detail |
| EmptyState = Icon(28) | ≈ 40 px | ≈ 80 px | — |

  So most glyphs on screen are 14–20 px and use only the flat silhouette rules. The detail, gradient and glow only show in the detail-pane hero, Help, the glyph window and tooltips.
- There are 35 MoonGlyph.Draw / DrawInline call sites (tables, panes, overlays, PathChart, RouteWindow, TutorialOverlay, ConfigWindow palette preview).
- GlyphDebugWindow's CVD/greyscale "Simulate" works by rewriting vertex colours in the draw list (ApplySimulation over VtxBuffer). That only works because every glyph colour is a vertex colour. This is the most important limit on any texture-based approach.
- A bundled-atlas pipeline already exists, for ornaments only:
  - docs/design/moon-road/gen_atlas.py inlines SVGs and has headless Chrome draw them to Tsukimichi/assets/ui/ornaments.png (256×128) and @2x (512×256).
  - The PNGs are embedded resources loaded via ITextureProvider.GetFromManifestResource.
  - The UVs are a const table in OrnamentLayout, held to the PNG by a test.
  - The 2x texture is used above 1.25× scale.
  - All the art is original geometry, so it satisfies the owner's rule against Square Enix art.
- Typography already uses UiBuilder.FontAtlas.NewDelegateFontHandle, so a custom-glyph font handle is technically within reach.

2. ANTI-ALIASING FACTS THAT DRIVE THE DESIGN
- ImGui's AA fringe is 1 px, centred on the edge. Every filled shape grows about 0.5 px and softens. At a 12–17 px glyph that is 6–8 % of the diameter, so features under 2 device px wash out.
- A 1–1.5 px ring whose centreline falls between pixels turns into two half-alpha pixels. Snap() fixes only the centre; the rim radius r − o/2 still lands at fractional positions for most r.
  - Proposal: below r 12, round the rim to whole device pixels (1, 2 or 3) and put the outer radius on a half-pixel. Optionally also round the radius to 0.5 px steps, which keeps any cache key finite (see 3B).
- Two AA polygons meeting on the same chord leave a faint seam. The code reuses the base polygon for overlays and draws ring states' rings last, which hides most of it. Keep that order: disc → rim → lit → detail → marks, with ring states' ring last.
- The Ready glow is three stacked translucent discs (1.70 / 1.42 / 1.20 r). From about 32 px this shows visible steps.
  - Fix with no texture: one radial vertex-colour annulus mesh from r to 1.7 r with alpha going from 0.15 to 0, the same PrimReserve pattern Shade() already uses. One primitive, smooth, still recoloured by the CVD simulator.
- Dashes (Not checked) are PathStroke arcs with butt ends. At r 6 the 8 × 22° dashes are about 2 px long with about 3 px gaps, just above the floor. Do not go finer.
- AddEllipseFilled exists in the bound ImGui; the maria use a custom mesh anyway.

3. RENDERING APPROACHES

A) Keep procedural vector geometry (today's approach), refined.
- Pros: draws exactly at any fractional scale; palettes and the CVD simulation keep working; no assets; trivial cost (tables clip rows).
- Cons: no true blur (glow, soft maria and terminator band are approximated by meshes); fringe softness at 12–17 px.
- Refinements:
  - round rims to whole pixels below r 12;
  - a single-mesh glow;
  - pixel-aligned rects for the high-contrast bars (already MathF.Round);
  - raise the weakest small-size carrier (see 4).

B) Runtime-baked sprite cache (recommended upgrade if richer art is wanted).
- Rasterise the same geometry on the CPU at the exact device-pixel size, using analytic circle coverage or 4×4–8×8 supersampling. Upload once through ITextureProvider.CreateFromRaw and draw with AddImage.
- Bake as white alpha masks per layer (disc, lit, rim, mark, glow, detail), not full colour. ImGui multiplies texture by vertex colour, so:
  - GlyphPalette colours stay code-driven, including the high-contrast light/dark variants and per-frame Resolve;
  - GlyphDebugWindow's vertex-colour CVD simulation still works;
  - nothing is shipped, which satisfies the no-downloaded-art rule.
- Gains:
  - true Gaussian glow and soft maria;
  - exact clipping of detail to the lens;
  - no seams;
  - per-size pixel hinting baked in.
- Cache key and memory: (state, layer set, device px rounded to 0.5). 8 states × about 12 sizes × ≤ 48² px × 4 B comes to well under 1 MB.
- Rebuild when UiScale, global scale or IconScale change, and dispose the wraps when the plugin unloads.
- Costs: 2–5 AddImage calls per glyph instead of about 4–20 primitives, and one texture switch against the font atlas per glyph run. ImGui merges same-texture commands, so a row of glyph-plus-text costs about 2 draw commands. Negligible.
- Verify in game:
  - whether textures created by CreateFromRaw are sampled bilinear without mipmaps (draw at 1:1 only);
  - creation must happen on the framework/draw thread.

C) Build-time SVG atlas (reuse gen_atlas.py and the manifest-resource pattern).
- Pros: the designer authors directly in SVG (docs/design/glyphs/glyphs-v2.svg exists); best fidelity at the baked sizes.
- Cons:
  - Real sizes are continuous (17.2 px and the like), and ImGui scales textures bilinearly with no mipmaps: shrinking more than 2× aliases, enlarging blurs. So the atlas would need about 11 sizes (12, 14, 16, 18, 20, 22, 24, 28, 32, 40, 48).
  - Full-colour sprites freeze the palette and break the CVD simulator. Alpha-mask layers avoid this but multiply the atlas.
  - Chrome does no hinting.
  - Two sources of truth (SVG and C#) would drift. The Python previews in render_preview.py and render_icons.py already mirror the C# by hand.
- Use only for art, not state glyphs: for example a 64–96 px decorative moon in the empty state or a banner. The ornament atlas already covers that tier.

D) Private-use icon font in the Dalamud font atlas (NewDelegateFontHandle with a generated TTF; fontTools could generate it from geometry).
- Pros: FreeType rasterises at the exact pixel size with hinting; shares the font texture; glyphs sit inline in text and chat-like strings.
- Cons: single-channel glyphs, so each colour layer is a separate glyph drawn at the same pen position; one font handle per size; asynchronous atlas rebuilds on scale change; no gradients.
- Only worth it if moons must flow inside wrapped text.

E) SDF/MSDF textures.
- These need a custom pixel shader (smoothstep on distance). Dalamud's DX11 ImGui backend gives no supported shader hook, and plain bilinear sampling of an SDF only gives a blurry edge. Rejected.

Recommendation:
- Keep geometry in Core as the single source of truth (MoonGeometry, MoonDetail, GlyphPalette).
- Ship A's refinements now: rims rounded to whole pixels below r 12, the one-mesh glow, and a stronger small-size Ready mark.
- Add B as an optional backend for r ≥ 12 (where detail, gradient and glow live). Below r 12 vector is already optimal, because those glyphs are flat silhouettes.
- The CPU rasteriser in B can also produce golden PNGs in Tsukimichi.Tests at 12/16/24/48 px in Standard and both high-contrast variants. Add a pairwise greyscale/CVD difference check, so the README preview and the docs sheets come from C# instead of the hand-mirrored Python.

4. OUTLINE AND SHADOW
- Standard has no keyline. It relies on the rim: Dusk is 5.1 : 1 on Night, but Shadow-disc moons over bright game scenery (Todo overlay over snow, Duty Finder panel, nameplate marks) can lose their edge. High contrast already carries its own ground keyline disc, and Marks get a 1 px ground keyline.
- Proposal for overlay surfaces only:
  - a dark backing in Standard: Night (or NightSunken) at 55–70 % alpha, disc of radius r + 1 px, optionally offset (0, +1) as a drop shadow;
  - never a light or coloured outline.
- Constraint: Ready is defined as "the only glyph with anything outside its rim" (glow from r 9, 1 px Moon ring at 1.25 r below). Any outline or shadow must therefore be:
  - dark and low in luminance;
  - within 1 px of r;
  - at least 1 px clear of 1.25 r.
  At r 6 the Ready ring sits only 1.5 px outside, so the shadow must hug the disc there. Alternatively, switch overlays to the high-contrast keyline treatment.

5. THE 8 STATES AND HOW THEY STAY APART BY SHAPE (glossary.md, GlyphPalette.cs, proposal §3.7)

Shape grammar, three independent channels:
- lit part: none / right half / right gibbous 60 % / left gibbous 75 % / full;
- rim: none / solid / thick (high contrast) / dashed;
- mark: outer glow or ring / seal / diagonal bar (+ notch from r 12) / check / bar / hollow bar.

| State (display name) | Standard | High contrast |
|---|---|---|
| Completed | full moon: the only rimless solid disc | solid bright disc |
| In journal (Accepted) | waxing gibbous 60 % lit, Night seal dot (max(0.16 r, 1.5 px)) on the lit side, Silver ring last | bright gibbous, large seal (0.24 r) |
| Ready | first quarter, gold, Dusk rim; glow from r 9, below that a 1 px Moon ring at 1.25 r at 35 % alpha; the only glyph with anything outside the rim | bright half + bold bar in the dark half |
| Ready on another job | first quarter, silver, Moon ring; nothing outside | dim half + hollow bar |
| Done today / this week / this cycle | waning gibbous 75 % lit on the LEFT, silver, Dusk rim, no mark | dim gibbous + check |
| Blocked | new moon: the only plain empty solid ring (Silver) | empty disc, thick rim |
| Locked out (Foreclosed) | eclipsed: Eclipse rim + diagonal bar max(2 px, 0.22 r); notch from r 12 | thick diagonal bar max(3 px, 0.32 r) |
| Not checked (Unknown) | veiled: faint disc (Shadow at 60 %), the only dashed rim (12 × 16° from r 10, 8 × 22° below) | dim dashed rim |

Rules to keep (proposal §3.7, GlyphPaletteTests):
- every state is unique at r 6 in greyscale;
- no pair differs only by mirror image or only by ring colour;
- in high contrast, states sharing a silhouette sit at least 3 : 1 apart on the luminance ladder (Ready gold vs Ready on another job dim; In journal gold vs Done dim);
- every distinguishing feature is at least 2 device px at a 12 px glyph;
- halo vs state: state = solid disc filling its box; progress = hollow ring with a small core, arc from 12 o'clock;
- dark marks only on In journal (inside, lit side) and Locked out (bar, plus notch at the upper right).

Weak spots found:
- (a) Standard Ready vs Ready on another job below r 9. Gold vs silver lit parts are about 1.17 : 1 in greyscale (L 0.64 vs 0.76), so the only shape carrier is Ready's 1 px Moon ring at 35 % alpha. Over Night that ring is only about 2.5 : 1, and roughly halved again when the 1 px line sits off the pixel grid. Most real glyphs are r ≈ 8.6, so this is the common case.
  - Fix: raise the ring to about 60 % alpha and round its radius to the pixel grid. Or borrow the high-contrast idea (a small mark in Ready's dark half). Or give Ready on another job a visibly thicker ring.
- (b) In journal vs Done in greyscale rests on the 3 px seal (r < 9.4 uses the 1.5 px minimum). Keep the minimum at 1.5 px radius, and never let the AA fringe of the Silver ring eat into it: seal centre 0.40 r, ring inner edge r − o.
- (c) Not checked's veiled disc at 60 % alpha plus Dusk dashes is the faintest glyph by design; on light hosts Standard swaps nothing. Light-theme legibility of Standard should be checked in game.
- (d) Doc drift. In glossary.md, the top Quest states table's glyph subtitle for Accepted says "waxing gibbous, gold ring". The code (StateNames.GlyphSubtitle: "waxing gibbous, sealed, silver ring"), the translated subtitles table and proposal v2.1 all say Silver ring plus seal. The glossary row is stale.

6. PERFORMANCE AND PLATFORM NOTES
- Segment count 12–96 by 0.30 px max error. Detail stays within the primitive budget and only appears from r 12, so the tree (≤ about 60 rows) and tables cost nothing extra.
- PrimReserve handles 16-bit index rollover through VtxOffset.
- Textures:
  - GetFromManifestResource and CreateFromRaw wraps must be disposed when the plugin unloads;
  - a texture switch splits draw commands;
  - the ornament atlas already shows a placeholder while loading. A glyph cache must fall back to vector drawing until its bake exists, so a state is never blank.

- Core geometry (MoonGeometry, MoonDetail, GlyphPalette) stays the single source of truth; any raster form is derived from it, never hand-authored separately.
- Colour comes from vertex colours: any texture must be a white alpha mask tinted at draw time, so palettes, the high-contrast light/dark Resolve and GlyphDebugWindow's CVD simulation keep working.
- Below r 12 draw vector silhouettes rounded to the pixel grid (whole-pixel rims, outer edge on a half-pixel, centre via Snap); spend detail, gradient and glow only at r >= 12.
- Every state-distinguishing feature must be at least 2 device px at a 12 px glyph and survive a 1 px AA fringe; nothing that carries a state may be a sub-pixel or under-35%-alpha hairline.
- Each of the 8 states is unique in greyscale through the shape grammar: lit part (none/half/gibbous right/gibbous left/full) x rim (none/solid/thick/dashed) x mark (outer glow/seal/bar+notch/check/bold or hollow bar); no pair differs only by mirror image or ring colour.
- Ready alone owns the space outside the rim: any outline or drop shadow must be dark, within 1 px of r and clear of 1.25 r.
- State glyph = solid disc filling its box; progress = hollow ring with a core and an arc from 12 o'clock; the two vocabularies never share a silhouette.
- Draw order is disc -> rim -> lit -> detail -> marks, with ring states' ring last, and overlays reuse the base polygon so AA seams stay hidden.
- Fixed-size bundled atlases only for decorative art 32 px and up; ImGui samples bilinear without mipmaps, so state glyphs are drawn or baked at exact device size.
- Only original geometry ships; no downloaded Square Enix art; official art only from the player's install at runtime.
- A glyph never renders blank: vector fallback until any baked texture or atlas is ready, and baked textures are disposed when the plugin unloads.

I made no changes to the repo. All test renders are in the scratchpad.

CURRENT ICON
- Files: `assets/icons/tsukimichi.svg` is the master, drawn on a 512 grid. `assets/icons/render_icons.py` uses Pillow to write `assets/icon.png` (512) and `assets/icon-64.png`.
- Where it's used: the csproj links `assets/icon.png` into the zip as `images/icon.png`. `IconUrl` (csproj and pluginmaster.json, made by `tools/make_pluginmaster.py`) points to `raw.githubusercontent.com/xenofei/Tsukimichi/main/assets/icon.png`.
- Content: a crescent with a halo in the upper left, three 1 px-class stars, a horizon split, a grey S-shaped path in the lower half and three gold stones with glows.

Problems, measured or simulated:
1. **Tile vanishes on the installer background.** Tile vs a dark background is 1.04:1.
2. **The horizon split doesn't show.** Sky vs ground is 1.08:1.
3. **The path is weak.** Path vs ground is 2.1:1; in a 32 px greyscale squint only the moon survives.
4. **The check covers the path.** The Installed check sits right on the path and stones.
5. **The moon matches Dalamud's Disabled overlay.** The crescent has the same orientation as the grey crescent Dalamud lays over disabled plugins; a disabled Tsukimichi shows two overlapping moons.
6. **The 512 file aliases in game.** It is minified 8:1 with no mipmaps, so it looks jaggier in game than `icon-64.png` suggests.
7. **The path reads as Questionable.** A winding path is Questionable's metaphor.
8. **It doesn't match the in-plugin crest.** The crest is moon over water; the icon is a land path.

DALAMUD FACTS (from the goatcorp/Dalamud source via gh)
- `PluginImageCache`: `PluginIconWidth` and `PluginIconHeight` are both 512 (maximum). The icon must be square, or it's rejected and the log says "was not square" / "larger than the maximum allowed resolution". There are at most 5 screenshots, each at most 730x380 (`images/image1..5.png` or `ImageUrls`).
- Installed plugins load `images/icon.png` from disk; plugins not yet installed fetch `manifest.IconUrl`.
- `PluginInstallerWindow` draws the icon at `ScaledVector2(64,64)` and then an overlay at the same size: Update, Trouble, Outdated, Disabled (the icon fades to 0.4 alpha) or Installed. There's a 0.3 s ease-out fade-in.
- `TextureManager` creates textures with MipLevels = 1. The Dx11Renderer sampler is MIN_MAG_MIP_LINEAR, WRAP, MaxLOD 0.
- Overlay assets are in goatcorp/DalamudAssets UIRes, 300x300 each:
  - `installedIcon.png`: opaque area (125,162)-(300,300), bottom right.
  - `disabledIcon.png`: grey crescent, lit on the left.
- In the main repo, about a third of 70 sampled icons ship below 512 (64, 72, 96, 128, 256), including well-made ones like Umbra, Craftimizer and EngageTimer at 128.

BRIEF: "Moon Road" installer icon

**Goal.** At 64 px, in one glance, among about 50 other icons: "the gold moon on night water — Tsukimichi". It should still read at 32 px and under Dalamud's overlays.

**Concept.** A waxing crescent over a still night sea, laying a road of light toward the viewer. This is the literal meaning of 月の道 and a scale-up of the in-plugin rail crest. Gold means "the path you walk", as it does in the UI.

**Composition (512 grid; output 128 px).**
- **Tile:** full-bleed rounded square, radius 22% (~112), corners transparent.
  - Sky: vertical gradient from about #1E284A at the top to Night #0F1424 at the horizon.
  - Sea: about #0E1324 down to #080A12.
- **Rim:** Gilt #A88B52 at about 80% alpha, 9-12 px, so the tile edge shows on dark rows.
- **Moon:** the dominant shape.
  - Waxing crescent, lit on the right with horns pointing left: outer disc r ≈ 120 centred at (256, 170); cut disc r ≈ 102 offset (−56, −28).
  - Gold gradient #FFF0BE → #F2D27A → #D6B25A; one soft glow at alpha 0.25 or less. No craters, no texture.
- **Horizon:** Silver #DDE3F0 hairline at about 45% alpha, ≥ 4 px, at y ≈ 307 (60%), inset about 8% on each side.
- **Road:** 4-5 horizontal rounded gold bars centred under the moon.
  - They widen toward the bottom, roughly 80 px → 225 px wide and 11 → 21 px tall, with alpha 100% → 85%.
  - Each bar is at least 2 px tall at 128 output.
  - The bars end before the extreme bottom-right, so the Installed check falls on calm sea and road tips, not on the moon.
- **Leave out:** stars, stones, any path, text, kanji, '!' markers.
- **Palette:** three hues in total — night indigo, gold, and a touch of silver/gilt. Tide blue is optional, only in the sky gradient.

**Deliverables.**
- Update the SVG master (hand-authored, original).
- Change `render_icons.py` to supersample at 1024 and Lanczos down to:
  - `assets/icon.png` at **128x128**, the shipped and IconUrl file. Optionally 256 for players at UI scale 3 or more.
  - `assets/icon-512.png` for the README and GitHub.
  - `icon-64.png` and a 32 px preview.
- Add a preview sheet that composites the icon with Dalamud's overlays and the no-mip GPU simulation, so future tweaks are checked the same way.
- Moving to 128 changes no manifest field, because the file name and URL stay the same.

**Alternatives for the owner's focus group.**
- **B — Crest Medallion.** A circular disc with a 3.5% brass ring and transparent outside, containing the crescent, horizon and narrowing bars. It's the only round silhouette among mostly square tiles and is the in-plugin crest exactly. Risk: the ring thins at 32 px.
- **C — Moon cradling a page.** The crescent cradles a journal page or four-point star, like the in-plugin Moonlit glyph. It says "quest catalog/rewards" more literally, but has more detail and is weaker at 32 px.

I'd recommend A, then B.

**Acceptance checks.**
- Moon vs tile ≥ 7:1.
- Road bars vs sea ≥ 4.5:1.
- Tile edge (rim) vs #101010 ≥ 2:1.
- In the 32 px greyscale blur, moon and road are both recognisable.
- With the Installed overlay, the moon is completely uncovered.
- At 40% alpha with the Disabled crescent on top, it doesn't read as two moons; the opposite orientation does this.
- In the no-mip simulation at 64 and 96 px, no edge stair-steps more than 1 px.
- It stands out next to the companion icons (Questionable, Artisan, AutoRetainer, BossMod, Lifestream, TextAdvance).

**Scratchpad evidence** (C:/Users/devon/AppData/Local/Temp/claude/c--Users-devon-Desktop-Tsukimichi--Main-Repo-/60059d08-377c-464c-af3e-c2638d2288a4/scratchpad/dres/):
- `overlay_sim.png`: current icon under each overlay.
- `squint.png`.
- `top64.png`, `top32.png`, `quest64.png`: contact sheets of the 40 most-downloaded main-repo plugins and 30 quest/tracker plugins.
- `companions.png`.
- `proto/A.png`, `proto/B.png`: quick sketches of directions A and B.
- `proto/cmp.png`: current vs A vs B under overlays and at 32 px.
- `proto/mip.png`: ideal vs no-mip from 512 vs from 128 vs at 96 px.
- `dres/*.png`: Dalamud's overlay assets.

The sketches are for evaluating the direction only. Final art should be authored in the SVG master.

Web search was unavailable (session budget used up), so the Dalamud facts come from reading the Dalamud and DalamudAssets source with gh. The #101010 installer background is an approximation of Dalamud's default dark style.

- Design for the 64 px slot. Dalamud's installer draws every plugin icon at ImGuiHelpers.ScaledVector2(64,64): 64 px at UI scale 1.0, 96 at 1.5, 128 at 2.0. The 512 master only matters for the README and the web.
- Ship a pre-filtered small PNG, not the 512 master. Dalamud makes the icon texture with MipLevels = 1, and the ImGui sampler is MIN_MAG_MIP_LINEAR with MaxLOD = 0. A 512 icon drawn at 64 is 8:1 bilinear sampling with no mipmaps, so the crescent edge stair-steps and the 1-2 px stars break up. A Lanczos-made 128x128 icon.png looks almost as good as the ideal downscale at scales 1.0-2.0.
- Respect the hard limits in PluginImageCache.TryLoadImage: square, at most 512x512, PNG. Anything else is rejected and the default '?' icon is shown. Use the same file for images/icon.png in the zip (installed plugins load it from disk) and for IconUrl (plugins not yet installed download it).
- Leave room for Dalamud's overlays, which are drawn at full 64x64 on top of the icon. The green Installed check (the state you see most) fills roughly x 27-64, y 35-64, the bottom-right ~40%. Update shows blue chevrons in the centre, Trouble a red X in the centre. Disabled fades the icon to 40% alpha and lays a grey crescent over it. Keep the identifying shape in the top ~60% and the bottom-right quiet.
- Avoid the Disabled overlay's shape. Dalamud's disabledIcon.png is a grey crescent lit on the left with its horns pointing right, the same orientation as Tsukimichi's current moon. Use a waxing crescent (lit on the right, horns pointing left). It reads differently from 'disabled' and also suggests growth and progress.
- Separate the tile from the background. The current tile's night colour (#0F1424) against a dark ImGui background (~#101010) is 1.04:1, so the rounded square disappears. A 1-1.5 px Gilt (#A88B52) rim at 64 px, or a lighter top-of-sky gradient, defines the edge.
- Use contrast that survives a squint. The current sky vs ground is 1.08:1, an invisible horizon split that wastes half the tile. The path vs ground is 2.1:1 and blurs into a smudge at 32 px. Only the moon (12.5:1) survives. Every element that carries meaning needs at least ~4.5:1 against what is behind it. Anything weaker is decoration and gets cut.
- One idea, one silhouette, three colours at most. The most memorable icons in the main repo are single bold shapes filling 80-100% of the frame: PeepingTom's two eyes, ChatTwo's bubble with '2', PixelPerfect's crosshair, Globetrotter's pin, EngageTimer's '17'. The weakest are text (DutyTracker, EorzeaVotes, PennyPincher, the Allagan Tools wordmark), detailed paintings (PetRenamer, WondrousTailsSolver), and dark-on-dark glyphs (QuestJournal, TrackyTrack, SubmarineTracker are nearly invisible).
- No text, no sub-pixel detail. Nothing thinner than ~1.5 px at 64 px (12 px on the 512 grid). Drop the three sparkle stars: they are 1 px at 64, shimmer without mipmaps, and are the 'noise glyphs' the owner dislikes.
- Own a colour and a metaphor in the player's actual plugin list. The companion icons are Puni.sh orange/purple line glyphs on black framed squares, BossMod's neon blue radar and Lifestream's purple aetheryte. Questionable already owns 'forking road + arrows + map pin', so a winding land path reads as Questionable. Deep indigo with a single warm gold moon is unclaimed.
- Match the brand already inside the plugin. Tsukimichi (月の道) is the road of light the moon lays across night water, and the in-plugin rail crest (docs/design/moon-road/ornaments/crest.svg) already draws it: moon, horizon hairline, stacked gold bars. The current installer icon instead draws a land path with stones, so it doesn't match the plugin's own identity.
- Stay original. Use only hand-made shapes and the existing tokens (Night #0F1424, Moon gold #FFF0BE/#F2D27A/#D6B25A, Silver #DDE3F0, Gilt #A88B52, Tide #6F8FD0). No Square Enix art and no '!' quest markers; many top icons (NoTankYou, JobBars, VFXEditor, QuestShare) are SE-styled, and that route is ruled out here. Moon shading should be the soft gold gradient plus glow, never a flat disc with craters ('cheese').
- Check every candidate before choosing. Simulate it at 64 and 32 px on #101010, on a hover-row grey and on a light background. Check it under the Installed, Update and Disabled overlays (Dalamud's own PNGs), at 40% alpha, as a blurred greyscale squint, and with the no-mip GPU sampling.

### Focus group 1 (current art)
- **Veteran raider, daily player (savage/ultimate progression, 5+ alts, runs roulettes and dailies every reset). Plays with a crowded HUD: party list, Boss Mod / timeline overlays, enmity, cast bars, and a plugin window or two docked over the game world. I glance at a quest list for half a second between pulls or in a queue; I don't study it. I want to know three things at a glance: what I can pick up right now, what is already in my journal, and what is closed to me. I read the following: Tsukimichi/Ui/MoonGlyph.cs (the 8-state painter, the halo gauge, the high-contrast path), docs/glossary.md, docs/design/glyphs/glyphs-v2.png and -zoom.png, assets/icon.png, assets/icons/tsukimichi.svg, assets/icons/moon-phases-preview.png and docs/design/moon-road-proposal.md. My honest verdict: the owner is right. The GOLD moons look like cheese. The silver ones don't. That contrast is the most useful clue for the redesign.** — dislikes: WHY IT'S CHEESE, concretely: (1) the lit colour is saturated butter yellow (Moon #F2D27A) and fully opaque, the colour of cheddar, not moonlight; (2) the radial gradient from a top-left highlight (MoonHigh to Moon to MoonDeep) makes it a shaded 3D ball or wheel, like a Babybel; (3) from r 12 the interior detail adds three dark Umbra blotches (maria) plus three crater rings, which on a yellow ground read as Swiss-cheese holes and mould; (4) the rim vignette darkens the edge like a rind; (5) a half-lit gold disc with a hard straight terminator (Ready, first quarter) is a cheese wheel with a wedge cut out. The 64 px Completed in glyphs-v2.png is a cheese wheel, full stop.; Accepted's 'seal' is the worst offender: a solid Night-coloured dot (0.16 r, min 1.5 px) punched into the gold half. On yellow, a round black dot is literally a cheese hole. At 16 px it's a 3 px speck that reads as dirt on my monitor.; Ready vs Accepted at row size: Ready is 50% lit and Accepted is 60% lit. At a 16 px inline box (r ≈ 6.7) that's about a 1.3 px difference in where the terminator sits. I can't see it mid-queue. What actually separates them is the rim colour (Dusk vs Silver) and the speck. And those are the two states I care about most.; Ready vs Ready on another job leans on gold vs silver for the lit half, and those two tokens have almost the same luminance (relative luminance about 0.66 vs 0.77, around 1.14:1). In greyscale, or for a deuteranope, they're the same half-moon, told apart only by a thin rim. The glossary's subtitles ('silver, gold ring' vs 'glow') prove the difference lives in 1.5–3 px of rim.; Ready's 'glow' is decorative, not a signal. At r ≥ 9 it's three discs at 5/9/15% alpha out to 1.7 r, and below r 9 a 1 px ring at 35%. Over a busy game scene or a translucent window it vanishes. The most actionable state has the weakest unique cue.; Blocked vs Not checked: both are an empty dark disc with a ring, solid Silver vs dashed Dusk. At 12 px the dashes (8 × 22°) close up and read as a dimmer solid ring. I'd mistake 'the plugin doesn't know' for 'you can't'.; Completed is the LOUDEST glyph (full, bright, gold, the only rimless disc, detailed and highlighted at big sizes) but it's the least actionable state. In a long list most rows are Completed, so I get a wall of gold cheese and the few Ready rows drown in it. The journal tree already dims complete nodes (dimComplete); rows don't.; Too many moon dialects for one brain: state moons, the filling moon, the halo gauge with a filling core, veiled stand-ins for missing reward icons, and Moonlit pips. Each is fine on its own. Together, a disc in a row could mean state, progress or 'no icon here', and I have to stop and decode.; The detail budget (12 primitives of maria, craters, terminator band and vignette per glyph) is spent at exactly the sizes where the glyph is big and prominent: the hero, Help and empty states. So the first big moon a new user sees is the cheesiest one.; The phase-to-state mapping is partly arbitrary. Why is 'Done today' a waning gibbous and not just a dimmed full moon? I learned it from the legend, not from the picture. Clever symbolism costs glance time. — wants: Make the moon look like moonlight, not food. Lit surfaces should be pale ivory or near-white with only a faint warm tint (think #F6EFD8 to #FFF6DC), flat or with at most a very soft luminance falloff and no top-left 3D highlight. Keep gold as LIGHT AROUND the moon (rim, ring, halo, bead), not as the moon's body. The silver glyphs already prove that a pale disc reads as a moon.; Delete the maria, craters, terminator band and rim vignette from state glyphs entirely, at every size. If a texture is wanted at hero size (r ≥ 20), use one very low-contrast, cool-toned mare shape, never dark spots on yellow. No dots or holes inside the lit area, ever.; Replace Accepted's black seal dot. Options that don't read as holes: (a) a crescent or gibbous inside a CLOSED gold ring, meaning 'held, in your journal'; (b) a small solid pip OUTSIDE the disc on the ring at about 1 o'clock (an orbiting bead, matching the Moon Road orbit language); (c) a thicker lit-side rim. Whatever is chosen must separate it from Ready at 16 px by silhouette, not by a 1.3 px terminator shift.; Rank the glyphs by actionability, not by fullness. Ready should be the brightest, most distinctive thing in a row: a crisp gold outer ring or arc at full opacity that survives over the game world, instead of a 5% glow. Completed should recede: a quiet, dimmed pale full disc (or MoonDim), like dimComplete in the tree, so a list of 300 completed quests is calm and the 4 Ready ones pop.; Give every state a unique silhouette that survives three tests at a 14–16 px inline size and 100% UI scale: greyscale, deuteranopia simulation, and drawn over a bright in-game scene through a translucent window. Gold vs silver must never be the only difference (Ready vs Ready on another job needs a shape cue, for example a hollow or split lit half, or a job-swap notch).; Make Not checked unmistakably 'unknown' rather than 'blocked': a hollow ring with no fill and fewer, longer dashes (say 4–6) that stay open at 12 px, kept the faintest glyph. Blocked keeps a filled dark disc and a solid ring.; Promote the high-contrast palette's thinking into the default look at small sizes: flat fills, a keyline so the glyph sits on its own background, one bold mark per state where needed. The ornate version, if any survives, belongs only at hero size.; Keep the Foreclosed diagonal bar exactly as it is. It's the model the others should match.; Keep Moon Road's rule that gold means 'act now' and enforce it in the glyphs: gold on Ready, Accepted's ring and the progress arc; pale or silver for done states; dark for can't. Then the colour itself answers 'can I do something here?'; Fewer moon dialects. Pick one meaning per form: state = small flat moon, progress = ring with a bead, missing icon = a neutral square or tile, not a veiled moon. Then a disc in a row always means state.; Plugin icon: keep the crescent over the road. It's the best moon in the project and it has no cheese problem because it's a thin crescent with a halo, not a shaded disc. If anything, shift the crescent toward the same pale moonlight tone as the new glyphs so icon and in-plugin moons match. Make sure the road and stones still read at 32 px in the installer, and consider dropping a star so the tile stays uncluttered.; Ship a one-glance legend that's always one hover away (the Help › Moon phases table), but design so I never need it after the first evening. If a state needs its subtitle to be understood, its glyph has failed.; Mock any redesign as a real 30 px row list (journal table, todo overlay, Nearby) with mixed states, mostly Completed and a few Ready, at 100% and 150% UI scale, on top of a screenshot of the game. Judge it there, not on a 64 px specimen sheet on a flat navy background. The specimen sheet is where cheese looks charming. The HUD is where it fails.
- **Brand-new A Realm Reborn player, around level 20, still learning what the yellow meteor, the "!" and the blue "+" quest markers mean in-game. FFXIV's UI already feels like too many little icons, so I judge every glyph by one question: can I tell what it means at a glance in a 24 px row without opening Help? I looked at the code in Tsukimichi/Ui/MoonGlyph.cs, the 8 states in docs/glossary.md, docs/design/glyphs/glyphs-v2.png, glyphs-v2-zoom.png and glyphs-v2.svg, assets/icons/moon-phases-preview.png, assets/icon.png and assets/icons/tsukimichi.svg, and docs/design/moon-road-proposal.md. I agree with the owner: the large moons look like cheese. The Completed moon is a butter-yellow disc (Moon #F2D27A, with a radial gradient into #D6B25A) and has blotchy darker "maria" and ringed "craters" on it, which reads as a Babybel or a wheel of Swiss. The half-lit gold moons look like cheese wedges.** — dislikes: The surface detail makes the cheese. Detail() paints three soft blotches, three ringed craters, a terminator glow band and a rim vignette over a gradient disc. On a smooth yellow circle, craters look like holes in Swiss cheese and maria look like mould spots or crumbs, not like the Moon. It is most visible exactly where a new player studies hardest: the Help moon-phases legend, the /tsukimichi glyphs window and the detail-pane hero moon (r 22). The first big moons I'm shown are the cheesiest.; The colour is butter yellow, not moonlight. #F2D27A fading to #D6B25A is the colour of cheddar or egg yolk. A cool, pale cream-white looks like a moon. Warm saturated yellow plus a round shape plus spots will always say 'cheese'.; Completed is the brightest, biggest, most saturated thing on screen: a full gold disc with highlight arc, gradient and craters. So my eye jumps to quests I already finished, when it should jump to what I can do now. In the zoomed tree mock, the finished 'Seventh Umbral Era' row with its full gold ring and core is the loudest row.; Ready, Ready on another job and In journal look almost the same at row size. All three are a half-ish lit disc with a ring. Ready is gold left/dark right with a Dusk rim. Other-job is silver with a gold ring. In journal is 60% gold with a silver ring and a tiny Night dot. At 16 px I can't tell 50% from 60%, and the gold-ring-versus-silver-ring difference is too subtle for me. These are the three states I care about most as a new player, and they are the hardest to tell apart.; The In journal 'seal' dot looks like an eye or a Pac-Man pupil, not a 'written in your journal' stamp. I would never guess its meaning.; The state names rely on astronomy words I don't know: first quarter, waxing gibbous, waning gibbous. I had to look up 'gibbous'. A legend that says 'waxing gibbous, sealed, silver ring' teaches me nothing about what to do.; There are two kinds of 'done': Done this cycle (silver waning moon) and Completed (full gold moon). As a newbie I don't know what a 'cycle' is. A silver almost-full moon next to a gold full moon reads as 'second place' rather than 'done for today, come back after reset'.; The Locked out notch (the dark bite at the upper right from r 12) looks like someone took a bite out of the cookie. More food.; The halo gauge in the tree draws a progress ring around a smaller filling moon. At 24 px that is two concentric things plus a progress bar plus a number on the same row, and it reads like a loading spinner. The row already shows progress three times. — wants: Keep moons (the owner is right not to drop the metaphor) but make them flat, crisp and graphic, like the plugin icon and the small preview strip. No craters, no maria, no vignette, no radial gradient at any size, including the large legend and hero sizes. One flat fill plus a clean rim. If texture is wanted at hero size, a single soft highlight is plenty.; Shift the lit colour from butter yellow towards pale moonlight (cream-white or soft silver-white). Keep saturated gold only as an accent for 'you can act on this now' (Ready's glow or ring). Cheese needs warm yellow plus spots, so removing either one helps and removing both fixes it.; Lean on silhouette, not fill percentage. Shapes I can tell apart at 16 px: a thin crescent, a half disc, a full disc, an empty ring, a dashed ring, a slashed ring. Never put two states only 10% apart in lit area (Ready 50% versus In journal 60%).; Make 'actionable' the loudest and 'finished' the quietest. Ready should be the brightest, glowing thing. Completed should step back (dimmer, or a calm outlined full moon), the way the tree already dims finished nodes with MoonDim.; Borrow the high-contrast marks for everyone, or at least for the three action states: a check for done (both kinds, with a small reset/clock hint for 'done today'), a clear 'in your journal' mark that isn't an eye-dot, and the slash for locked out. A beginner reads a check or a slash faster than a moon phase.; Echo the in-game quest markers I'm already learning wherever it fits. Ready could carry the same feeling as the yellow '!' or blue '+' I see over NPCs' heads, using an original shape, not SE art, so the plugin feels like part of the game rather than a second vocabulary.; In the Help legend and the glyph tooltips, lead with plain words and what to do ('Ready: you can pick this up now', 'In journal: you've started it, step 2 of 5'). Drop the phase subtitles like 'waxing gibbous, sealed, silver ring' from player-facing text, or bury them under a 'why a moon?' footnote.; Remove the bite-shaped notch from Locked out. The slash alone is clear.; Simplify the tree row: either the progress bar plus number, or the halo, not both. If the halo stays, make it a single ring without a moon inside at 24 px.; Keep the plugin icon as it is, or let the crescent from the icon become the family's anchor shape, so the glyphs look like they belong to the icon. Right now the icon is flat and elegant and the glyphs are shaded and lumpy, as if they came from two different designers.
- **Focus-group participant: a deuteranopic (red-green colour-blind) FFXIV player who reads state by shape, line weight and lightness, never by hue. I looked at Tsukimichi/Ui/MoonGlyph.cs, Tsukimichi.Core/Ui/GlyphPalette.cs, docs/glossary.md, docs/design/glyphs/proposal.md §3.7, docs/design/ui-revamp-proposal.md §5.2, docs/design/moon-road-proposal.md §6 and §10, docs/design/glyphs/glyphs-v2.png and -zoom.png, assets/icon.png, assets/icons/tsukimichi.svg and moon-phases-preview.png. I also ran a Viénot deuteranopia simulation of glyphs-v2.png and of the colour tokens; the image is in the scratchpad, not the repo. What I see after simulation: Moon gold #F2D27A turns a greenish yellow #DCDC78. Silver #DDE3F0 barely changes. Eclipse #B25C7F turns flat mid-grey #7D7D7C. Dusk becomes #8383A8. Gold and Silver are only 1.14:1 apart in lightness, Eclipse and Dusk only 1.23:1, and Gilt and Dusk only 1.11:1. So any difference that rests on gold vs silver, or on pink vs grey-blue, is a guess for me. Verdict: I agree with the owner about the cheese. The shading I see is only on the full and gibbous gold moons, and it makes them look like a wheel of cheddar. Under deuteranopia the gold goes more processed-cheese yellow-green. The skeleton of the system is the most colour-blind-friendly I've seen in a Dalamud plugin, and the redesign should keep it.** — dislikes: The cheese is real, and it lives in Detail() and Shade() in MoonGlyph.cs. From r 12 the lit part gets 'three maria, three craters, terminator glow, rim vignette', plus a warm MoonHigh → Moon → MoonDeep radial gradient from r 9. On a full gold disc at 32–64 px (the detail hero, the Completed tile in glyphs-v2.png row 1, the complete halo core), the grey blobs and ring craters on a yellow gradient look like Swiss cheese or a cracker. Deuteranopia pushes the yellow toward green-yellow, so it looks even more like processed cheese to me. The craters also add busy low-contrast texture inside the shape I'm trying to read.; The biggest, most saturated gold blob in the UI is Completed, the one state I can't act on. That fights the moon-road principle P1 that gold means 'act now'. Ready gets half a disc of gold and Completed gets a whole one, so my eye goes to finished quests first.; Ready vs Ready on another job is the pair I would confuse in a 24 px table row. Both are half-lit moons. One has a gold half with a grey-blue rim and a soft glow; the other has a silver half with a gold rim. For me gold and silver are 1.14:1 apart in lightness, so the difference is down to the glow. Below r 9 the glow is a 1 px ring at 35 % alpha (SmallReadyRingColor), which is too faint to catch at a glance.; The In journal seal is too small at row sizes: 0.16 r with a 1.5 px minimum (SealRadiusFraction and SealMinRadius). At 16 px it is one dark pixel inside a yellow half. In the zoom image I can barely tell (f) from (d) without the label. The high-contrast version (0.24 r, minimum 2 px) is the right size, but I only get it by switching the whole palette.; Done this cycle has no mark in Standard. It is a silver waning gibbous with a grey-blue rim, so I'm told apart from In journal by which side is lit plus that tiny seal. The proposal's own rule says a mirror image alone doesn't count, and at 12–16 px the seal is nearly all that's left.; Rim colours carry meaning nobody colour-blind can use: Silver for In journal, Moon for Ready on another job, Dusk for Ready, Done and Unknown, Eclipse for Locked out. Gold, silver and grey-blue rims all look about the same light grey to me.; Soft glows and blurred halos (the Glow discs, HaloGlowOuter and HaloGlowInner, the icon's moon halo, the glow around the stones) read as smudge, not shape. A glow is a lightness cue with no edge, the hardest kind to pick out at small sizes.; A half-filled progress moon looks like a state glyph. In the tree, the halo gauge core at 50 % is a gold right half with a grey-blue rim on the dark side, which is almost exactly the Ready glyph. The code says a moon means a state or a completion fraction, but I can't tell which one I'm looking at without the ring around it.; The legends I learn from don't agree. docs/glossary.md's first table says In journal is 'waxing gibbous, gold ring', but the glyph-subtitle table and the code (GlyphPalette line 453) say silver ring. assets/icons/moon-phases-preview.png still shows the old v2.0 In journal (75 %, gold ring, no seal). If shape is all I have, the legend has to be exact.; Marks (the in-disc bar, hollow bar, check and big seal) exist only in high contrast. To get shapes I can tell apart I have to take the whole flat palette, and the standard look leaves those marks out. — wants: Keep the shape system, drop the surface texture. Remove the maria, craters, terminator band, vignette and highlight arc from state glyphs entirely, and keep the radial gradient off too, or at most a two-stop lightness shift with no hue change. Draw every moon flat with crisp edges, like the plugin icon's crescent. A flat crescent and a flat disc read as moon; a textured yellow disc reads as cheese.; Make Completed a cool, pale moonlight (Silver or an ivory close to it) instead of saturated gold, and keep gold only for things I can act on (Ready, In journal, progress arcs). This fixes the cheese (a real full moon is silver-white) and the gold-means-act rule in one move. Completed stays unambiguous because no other state is a full disc.; Give every state one in-disc mark at every size in the default look, borrowed from the high-contrast set: a solid bar for Ready, a hollow or outlined bar for Ready on another job, a large seal (at least 0.24 r, 2 px) for In journal, a check for Done this cycle, a thick rim for Blocked, the thick diagonal for Locked out, dashes for Not checked. Standard and high contrast should share silhouettes and marks and differ only in palette.; Separate Ready from Ready on another job by shape, not glow. For example, keep Ready as a solid half and draw 'on another job' as an outlined half (lit side drawn as a 2 px outline, interior empty), or add the moon-road 'sigil spark' tick outside the rim only for Ready. No sub-pixel, 35 %-alpha rings.; Don't let rim colour carry meaning. If two states share a rim style they must differ by mark or silhouette, and rims should be at least 2 px at 16 px. Every rim, bar and dash needs at least 3:1 lightness contrast against Night after a deuteranopia simulation, not just in normal vision.; Make progress look different from state: give the filling moon in the halo core no rim and a different fill treatment, or drop the core and keep progress as arc plus number, so a 50 % node is never the Ready glyph.; Add a deuteranopia/protanopia check next to the existing greyscale one. Run each candidate sheet through a CVD simulation (the Viénot matrix is enough) and require every pair of states to stay distinct at 12, 16 and 24 px. A toggle for this in GlyphDebugWindow (/tsuki glyphs) would let the owner see what I see.; Fix the legends before or with the redesign. One accurate glyph-subtitle table in docs/glossary.md, a regenerated moon-phases-preview.png from the shipped geometry, and the in-game Help › Moon phases legend showing each glyph at row size (16 and 24 px) as well as 64 px, since row size is where I have to recognise them.; Keep the plugin icon's crescent and flat style as the visual anchor for the redesign. If anything, give the three path stones a slightly brighter path behind them (the 26 % silver path is low contrast on navy at 64 px), and drop the soft halos in favour of crisp shapes.; Optional, and I'd turn it on: a setting that adds a tiny letter or symbol badge to the state glyph at 24 px and up, only where the state text is hidden (narrow tree tier, gallery tiles, Todo overlay). I don't need it if the marks are done well, but it is a cheap safety net.
- **Glamour and aesthetics-focused FFXIV player (the kind who spends an evening in the Glamour Dresser getting a dye shade exactly right and screenshots in gpose at golden hour). I judge a UI the way I judge a glamour: does every piece belong to one palette, one material and one story, and does it look expensive at a glance? I looked at Tsukimichi/Ui/MoonGlyph.cs, Tsukimichi.Core/Ui/MoonDetail.cs (the maria and crater tables), docs/glossary.md (the 8 states), assets/icon.png, assets/icons/tsukimichi.svg, assets/icons/moon-phases-preview.png, docs/design/glyphs/glyphs-v2.png and glyphs-v2-zoom.png, and docs/design/moon-road-proposal.md. My verdict: the owner is right. At 24 px and up the state moons read as cheese, and I can say exactly why. The fill is an opaque butter-yolk yellow (Moon #F2D27A grading from cream MoonHigh #FFF0BE down to mustard MoonDeep #D6B25A). It reads as a solid, matte object, not as light. On top of that sit three soft Umbra ellipses at 13 to 20 % alpha (the maria) and three small round 'craters', each a floor disc plus a highlight arc and a shadow arc. Small round pits with a lit lip on a yellow solid are exactly the holes in Emmental. The rim vignette darkens the edge like a rind. The half-lit states (Ready, Accepted) then turn into a cheese wheel cut in half, or a wedge. The silver Done moon reads as a pearl or a ping-pong ball. The gold crescent in the plugin icon is the same flat yolk.** — dislikes: Surface detail on a 12 to 64 px icon. The three maria and three craters (MoonDetail.Maria and MoonDetail.Craters, drawn from r 12) are the single biggest cause of the cheese read. Low-alpha blotches on yellow read as holes or mould, not as lunar seas. No elegant iconography system puts topography on a glyph this small.; The fill colour and material. Opaque butter-yellow with a radial gradient (highlight at upper left, darker at the rim) makes a matte ball lit by a lamp. A moon is a light source on a dark sky: it should glow at its edge and be pale, not saturated.; The specular highlight arc on Completed (a white arc at 0.76 r) makes it read as glossy plastic or a marble.; Ready's glow is three stacked translucent discs, so the moon looks like it sits in a yellow puddle or a bokeh blur rather than radiating.; Accepted's Night seal dot on the lit half looks like a pupil. In the 64 px row Accepted is an eyeball staring at you.; Foreclosed is a red ring with a diagonal slash: a universal 'no entry' sign pasted onto a moon. It breaks the poetic language every other state speaks. The bite (notch) at the upper right adds a third idea on top.; Too many visual languages across eight glyphs: two fill tones (gold, silver), four rim colours (silver, gold, Dusk, Eclipse), solid, dashed and 'under the lit half' rims, glow, seal, slash and notch. Each state is defensible on its own, but together they read as a costume made of eight different sets, not one glamour.; Silver vs gold as a state carrier: the silver Done this cycle moon (cool white with grey maria) looks like a different object, a pearl, rather than the same moon in a different phase.; Icon: the three 'stones' on the road have two-step translucent halos and look like flashlight beams or out-of-focus bokeh, not lit stones or reflections. The road is a flat grey, slightly opaque band that reads as asphalt. The horizon is a hard rectangle edge (a second rect at 4 % opacity) instead of an atmospheric falloff. At 64 px the stars disappear and the stones turn into three yellow dots.; The icon crescent and the glyph moons share the same flat yolk yellow, so the brand colour itself says 'cheese'. — wants: Treat the moon as light, not as an object. The lit part should be a pale champagne or ivory (somewhere around #F4EBD0 to #EFE3BF), brightest at the limb (the outer edge), with a soft terminator that fades into the dark side over a few pixels instead of a hard chord. Gold then becomes the colour of the glow and the rim light, not a paint fill. This keeps the 'gold means act' rule, just as luminance rather than as butter.; Remove the maria, the craters, the rim vignette and the specular arc entirely, at every size. If anything at all is wanted at 48 px and up, a single, almost invisible tonal shift (under 6 % alpha, no hard edge) is the most I would allow. Smooth, flat or gently graded discs are what make Japanese moon motifs (tsukimi, ukiyo-e skies, kamon crests) look expensive.; Earthshine instead of rim strokes on the dark side. Draw the unlit part as the Shadow disc with a very faint lit limb (the 'ashen light', the old moon in the new moon's arms). It is physically right, quietly poetic, and replaces most of the coloured rings that make the set busy.; One accent vocabulary for all eight states: phase carries most of the meaning, plus at most one hairline accent per glyph, drawn in the same weight everywhere. Suggestions: Ready, first quarter with a soft halo ring (a moon halo, 'tsuki no kasa') rather than blur discs. Ready on another job, the same phase drawn in silver light with a gold hairline halo. In journal, gibbous with a tiny four-point star (the sigil star from the Moon Road kit) on the lit side instead of the eyeball dot. Blocked, a new moon showing only its earthshine limb. Done this cycle, waning gibbous in the same light, dimmer. Completed, a full moon with a single thin halo ring and no gloss. Locked out, a true eclipse: a dark disc with a thin copper-rose corona ring of light around it, instead of a prohibition slash (if a diagonal must stay for colour-blind safety, make it a 1 px hairline, not a bar). Not checked, oborozuki, the hazy moon: a faint disc crossed by one or two horizontal mist strokes, which is a classic motif and reads better than dashes.; Keep every distinction greyscale- and colour-blind-safe through shape (phase, halo, star, corona, mist), since the current set leans on rim colour more than it should. Leave the high-contrast palette as its own mode, and let the new standard look be closer to it in flatness.; Line-weight discipline: every ring, halo and accent the same hairline (1 to 1.5 px scaled), in brass Gilt or the state's light, never three weights in one glyph. That is what makes a set look like one family, like a well-cut kamon sheet.; Plugin icon redesign that keeps the composition but tells the real image: tsukimichi is the path of moonlight on water. Replace the grey road and glowing stones with a shimmering reflection of short, broken horizontal light strokes on dark water, widening toward the viewer. Make the crescent thinner and champagne-pale with a soft gold bloom. Fade the horizon with a gradient instead of a hard band, and use a deeper indigo-to-midnight sky. Optionally frame it in one fine brass circle, kamon style. At 64 px it should still read: one crescent, one shimmering path, nothing else (drop the stars at small sizes).; Make the brand colour stop being yellow. Use pale moonlight with warm gold only as glow, so the icon, the glyphs and the Moon Road's Gilt ornaments all share one material: light and brass on night water.; Show the owner a side-by-side proof sheet before shipping (current vs proposed, all 8 states at 12, 16, 24, 32 and 64 px, on Night and on the ImGui background, plus greyscale), like glyphs-v2.png. Generate it from code with no downloaded art, as the repo already does. Judge it in-game at 16 to 24 px, because that is where cheese or elegance is really decided.
- **Haruka, a 34-year-old Japanese player on the Elemental data centre who plays in English. Hobbies are kamon (family crests), ukiyo-e moon prints (Hiroshige, and Yoshitoshi's "One Hundred Aspects of the Moon") and an actual tsukimi every autumn with susuki grass and dango. To me the moon is pale, flat and quiet: a white disc on indigo, half hidden by cloud or mist. What I looked at: Tsukimichi/Ui/MoonGlyph.cs, Tsukimichi.Core/Ui/MoonDetail.cs (3 maria, 3 craters, budget 12), GlyphTokens (Moon #F2D27A, MoonHigh #FFF0BE, MoonDeep #D6B25A, Eclipse #B25C7F, Shadow #3A4363, Night #0F1424), docs/glossary.md (the 8 states), assets/icon.png with assets/icons/tsukimichi.svg, assets/icons/moon-phases-preview.png, docs/design/glyphs/glyphs-v2-zoom.png and docs/design/moon-road-proposal.md. I agree with the owner, and I can point to exactly where the cheese comes from. In glyphs-v2-zoom.png the big Completed moon is a cheddar wheel. It is butter yellow (#F2D27A), it has a radial highlight at the upper left that makes it look like a waxy ball, and its darker ochre maria and ringed craters look like the holes in Emmental. The cut-in-half quarter moons and the gibbous with a ring around it then look like a wheel with a wedge taken out. The Accepted "seal", a small dark dot on the gold, reads as one more hole. The problem is not the idea. It is three choices together: a saturated yellow, 3D shading, and surface texture.** — dislikes: The hue. #F2D27A with MoonHigh #FFF0BE and MoonDeep #D6B25A is a Western picture-book 'yellow moon'. In Japanese art and poetry the moon is white, silver or pale ivory (月白, 白銀). Gold appears as gold leaf on lacquer: thin, sparing, a line or a ground, never a big glossy yellow ball.; The radial gradient (GradientCenter -0.32,-0.34, from r 9) and the highlight arc on Completed (from r 16) turn a flat moon into a shiny 3D sphere. Japanese moon imagery (kamon, hanafuda's 芒に月 card, Hiroshige) is always a flat disc. The shading is what makes it look like food.; MoonDetail's maria and craters (from r 12) are the main cause of the cheese. On a yellow disc, soft darker ochre blobs and ringed craters with shadow arcs read as cheese holes, not lunar seas. In Japan the only 'surface' anyone imagines on the moon is the rabbit pounding mochi. Nobody wants a realistic crater map.; The Accepted seal: a Night dot at (0.40, 0) on the lit side. It is a hole in the cheese at every size in the zoom sheet. If it is meant as a seal (印), a Japanese seal is a vermilion square, not a black dot.; Too many layers on one tiny moon: a coloured rim inside the edge, a navy Shadow disc, a gold or silver lit part, a gold or silver outer ring, glow discs, and the seal. ReadyOnOtherJob is a silver moon inside a gold ring, both bright; it looks busy and slightly like a coin. Understatement (控えめ, 引き算の美) means removing things.; The filled Shadow disc (#3A4363) on the dark side is too visible. In Japanese moon art the unlit part disappears into the night. Here every quarter moon looks like a cut wheel: half gold, half navy, with a hard vertical edge.; The Foreclosed diagonal bar plus a notch bitten out of the rim reads as a 'no entry' road sign, a Western traffic symbol. The pink Eclipse (#B25C7F) is the only colour that feels foreign to the palette, more candy than traditional crimson.; The plugin icon's path is a grey dirt road winding over land, with three glowing gold stones like a board game. 月の道 (tsuki no michi) means the road of light the moon lays on water. The icon misses its own name. The flat rectangle horizon split and the hard edge between sky and ground feel stiff.; The four-pointed sparkle stars in the icon are a Western 'magic ✨' convention and make it look like a generic sleep or meditation app. In tsukimi imagery the moon outshines the stars. One star at most, or none.; The soft gold halo blob behind the icon crescent adds to the same 'glowing butter' look. — wants: Change the lit colour from butter yellow to a pale, slightly warm ivory or paper tone. Suggested values: 鳥の子色 torinoko #FFF1CF or 練色 neri-iro #EDE4CD for the warm 'actionable or walked' moon, and 月白 geppaku #EAF4FC for the silver states. Keep real gold (around 金色 #E6B422, or the existing Gilt) only as thin accents: rims, the Ready halo, rules, progress lines. That way 'gold means act' still holds, and the moon itself becomes pale and quiet. Check in the /tsukimichi glyphs window that warm ivory against cool silver still separates at r 6–8. If it does not, let the ring and glow carry the difference, not saturation.; Make every lit part a flat fill at every size, like a kamon. Remove the radial gradient, the highlight arc and the rim vignette from the default palette.; Delete MoonDetail's maria and craters and the terminator glow band from the default look. Do not offer them as an option either; they are the cheese. If one large-size easter egg is wanted, use a very faint moon-rabbit (月の兎) silhouette, only on the hero or Help legend moon at r ≥ 32, at a few percent alpha. I would love it, but I would understand if it were cut as noise.; Let the dark side mostly dissolve. Instead of a filled Shadow disc, draw a hairline outline of the full disc (in Veil or VeilLine) and leave the inside almost the Night colour, perhaps 10–20 % Shadow. The phase silhouette stays readable and the 'cut wheel' look goes away.; Draw rings as separate keylines with a small gap around the disc, like the 丸に… ring of a kamon (丸に三日月, 丸に十三夜), not as a thick coloured rim fused to the edge. Use one hairline weight for all ring states.; Drop the Accepted seal dot. Accepted is already told apart by its gibbous shape plus ring. If a mark is really needed, use a tiny square seal at large sizes only, in a muted red and not black, provided it cannot be confused with Locked out.; Redesign Locked out (Foreclosed) as 月に叢雲 (tsuki ni murakumo, 'clouds over the moon'), the proverb about beautiful things being obstructed. Draw one or two flat horizontal kasumi or cloud bands (the stylised mist bands of byōbu screens) across the disc. It is a distinct shape that survives colour deficiency, so it keeps the accessibility goal of the diagonal bar. Change the colour from pink to a muted traditional red such as 蘇芳 suou #9E3D3F or 臙脂 enji #B94047, or simply use ash grey, checked against contrast.; Keep Unknown as oborozuki: dashed rim, low-alpha disc. Make sure it stays distinct from the cloud band of Locked out (haze over the whole disc vs. a solid band across it).; Draw the Ready glow as 月暈 (tsukigasa, the lunar halo): one thin separate ring of light at about 1.3 r, rather than three stacked soft discs. That is sharper and more elegant, and it already matches what is drawn below r 9.; Redraw the plugin icon around water: a moon low over dark indigo water, and below it a vertical road of moonlight made of short horizontal broken strokes that narrow toward the horizon (as in Hiroshige or a real moon-road photo). Remove the land path and the stones. Optionally add a few susuki grass blades as a silhouette in one lower corner for tsukimi. Use one star or none. An alternative that reads better at 64 px is a flat two-colour crest (家紋 style) of a moon over wave lines (波に月 or 流水): a circle frame, ivory moon, a few seigaiha or ryūsui lines. Use original geometry only; never a downloaded crest.; Use the traditional moon names as a private design vocabulary, so each state has one clear image to aim for: Ready = 上弦, Accepted = 十三夜 (admired precisely because it is not yet full), Done this cycle = 十六夜 izayoi (the 'hesitating' moon just after full, which wanes and returns at reset), Completed = 十五夜, Blocked = 朔, Unknown = 朧月, Locked out = 月に叢雲. Localization is frozen, so these stay as English design notes; at most the Help legend could add an English gloss later.; Keep it static and quiet, following the owner's taste. No twinkling or pulsing. If anything moves, at most a one-time fade when a quest becomes Completed.; Validate the redesign the same way as before: rebuild the glyphs-v2-zoom-style sheet at r 6, 8, 12, 16 and 32 on Night, Abyss and the high-contrast ground. Test it on someone who has not seen the old one by asking: does any moon still look like food?

### Concepts
- **Moonlight Hairline (Direction A): flat, crisp moonlight shapes with hairline brass and silver lines, and a gold halo as the one signal for acting now** — This concept takes the research brief literally: draw the moon as a light source, not as a lit object. Every lit shape is one flat, crisp-edged fill in pale moonlight, a new token Moonlight #F3EAD0. That is warm ivory with low chroma, so it is no longer cheddar #F2D27A. There is no gradient, vignette, maria, craters, highlight arc or glow-disc stack at any size. The unlit body is not a filled navy half any more. It is a "ghost limb", a 2.5/64 hairline circle in Dusk, so the dark side fades into the night the way it does in kamon and ukiyo-e moons. This also removes the "wheel with a wedge cut out" look. Saturated gold appears only as a line around a moon and only on the two states you can act on: Ready's detached halo ring (tsukigasa) and In journal's closed ring. That enforces the Moon Road rule that gold means "act now". Done states are silver, "can't" states are dark or rose, and Completed is a quiet ivory disc.

How the states stay apart by shape (checked in a headless-Chrome contact sheet at 14/16/20/24/48 px on Night, the Dalamud default #141414 and a bright scene, in colour and greyscale; see scratchpad/dirA/sheet2.png):
- Ready: a waxing crescent, lit on the right, about 28% lit, with a widest point of 0.57r, plus a full-opacity gold halo ring detached at about 1.3r. It is the only glyph with anything outside the disc, and the loudest one in a row. A crescent cannot be read as food, and it is the same anchor shape as the plugin icon.
- Ready on another job: the same crescent drawn hollow (a silver outline over a 14% silver wash), with no halo. Halo against no halo is a shape difference, not a gold-versus-silver one.
- In journal: a first-quarter half (lit right) closed by a gold ring fused to the limb. It means "held, in your journal". The black seal dot that looked like a hole, pupil or speck is gone. Against Ready it is crescent versus half and fused ring versus detached halo, which is visibly a different footprint at 14 px.
- Done this cycle: a last-quarter half lit on the LEFT in dim silver, with a monoline check in the dark half. The check is the universal "done", and it sits on the unlit side, so the lit area never gets a mark.
- Completed: the only full lit disc. It uses a quieter ivory (#CFC7B0) with a bright Moonlight limb line on the inner edge. That edge is a light-source cue (brightest at the limb), not a coin rim, so the shape reads as moonlight rather than as a plate. It deliberately recedes, so a list of 300 completed quests stays calm and the few Ready rows stand out.
- Blocked: a new moon, the only plain thick solid ring (Mist), over a faint Shadow body.
- Locked out: the existing diagonal Eclipse bar and Eclipse rim are kept exactly, because every focus-group player named it the best glyph. The bitten notch is removed.
- Not checked: the only dashed ring, with no fill and five long dashes (41.5 degrees on, 30.5 off). The gaps stay about 2 px open even at 12 px, so it no longer closes up into Blocked's ring.

Greyscale and deuteranopia: no pair is separated only by mirroring or only by colour.
- The lit-part channel has five values: crescent, half on the right, half on the left, full, and none.
- The rim channel has five: detached halo, fused ring, thick solid ring, dashed ring, and ghost hairline.
- The marks are the check and the bar.

Phase mapping change, deliberate and flagged for the owner:
- Ready moves from first quarter to crescent, In journal from a 60% gibbous to a ringed half, and Done this cycle from a waning gibbous to a left half with a check.
- This fixes the 50%-versus-60% terminator problem every participant raised.
- It gives a story readable without the legend: new moon (can't) → first light (can start) → half, held in a ring (under way) → full (finished), with the waning half plus check meaning "done, comes back at reset".

The plugin icon follows Research 3's "Moon Road" brief:
- A waxing crescent lit on the right with horns pointing left, which is the opposite of Dalamud's grey Disabled crescent. It is pale moonlight (#FFF8E4 to #EADCB8) with only a 13% gold bloom.
- Below it, a still night sea with a Silver horizon hairline and four widening, rounded gold road-of-light bars (月の道).
- A Gilt rim so the tile separates from the ~#101010 installer background.
- No stars, stones, land path, text or kanji. The moon sits entirely above y = 276, so the Installed check never covers it.
- Icon and glyphs now share one material: pale light, brass line, gold only for the road you walk.

Known risks to test in the focus group:
- (1) Existing users must relearn three phases. The Help legend should lead with plain words.
- (2) Ready's halo outer edge reaches 1.39r, so inline glyphs need either a slightly smaller r or the 1–2 px overflow the current 1.7r glow already takes.
- (3) Below 16 px the hollow crescent of Ready on another job reads as a thinner crescent, so there the missing halo is what tells it from Ready. That is a strong cue, but confirm it over bright game scenes.
- (4) Completed's isolated "5-second naming" may still say "circle". In context it sits at the end of the crescent-to-full sequence. If it fails, the fallback is the kamon gap-ring variant I also rendered (scratchpad/dirA/comp.png).
- **Direction B: "Tsukigasa" (luminous minimal: pearl moons, midnight bodies, orbit accents)** — Concept. Each moon is drawn as a light source rather than a lit object. The lit part is flat pearl with only a faint linear lift toward the limb: no radial ball gradient, no limb darkening, no maria, craters, vignette or specular arc at any size. The unlit body is a midnight-blue linear gradient (#232C50 to #10162E) with a thin earthshine hairline (VeilLine #5C6584) instead of the old flat Shadow wedge, so a quarter moon no longer looks like a cut cheese wheel. Saturated gold (#F2D27A) moves off the moon body. It now appears only as light around the moon: the tsukigasa halo ring, the bloom and the orbit bead, always well under a third of the glyph area. Gold therefore means "act now" again, as Moon Road P1 requires.

The colour story, verified with WCAG luminance on Night #0F1424:
- Warm pearl (#FFF9EA to #EDE2C6, about 15.9:1) marks the two states that are yours to act on: Ready and In journal.
- Cool pearl (#EEF2F9 to #CDD5E5, about 14.4:1) is for Ready on another job.
- Dimmer silver (#E2E7F1 to #C2CADB, about 12.8:1) is for Done this cycle.
- Completed is the quietest lit glyph (#B9C1D3 to #A3ADC3, about 9.1:1). A list of 300 completed rows stays calm and the few Ready rows stand out. I checked this in a 15-row mock list, in colour and in greyscale.

The shape grammar adds an outer orbit channel to the existing silhouettes, so no state depends on hue. Warm and cool pearl are only 1.1:1 apart, so treat them as mood, never as the carrier.
- Ready (上弦): solid right half, plus a CLOSED gold halo ring at 1.27 r, plus a soft gold bloom. It is the only closed outer ring and the loudest glyph.
- Ready on another job: the same half in cool pearl, plus a SPLIT brass halo, two arcs with gaps at 12 and 6 o'clock. Closed ring versus two arcs survives greyscale and deuteranopia. The 35%-alpha hairline is gone.
- In journal (十三夜): a 65% waxing gibbous plus one gold orbiting bead at 1:30. This is Moon Road's orbit-bead language. It replaces the black seal dot that read as a cheese hole or an eyeball, and nothing dark ever sits inside a lit area.
- Done this cycle (十六夜 izayoi): a 62% waning gibbous lit on the LEFT, silver, with no accent. It differs from In journal by mirror, by having no bead and by its tone, so the "not only mirror" rule holds.
- Completed (十五夜): the only full disc, rimless and dim pearl. At hero size it gets a 10%-alpha cool bloom as its non-texture cue, so it reads as moonlight, not as a coin.
- Blocked (朔): a midnight disc with a solid Mist #A9B2CC ring (8.7:1).
- Locked out: kept as the focus groups' favourite, an Eclipse #B25C7F rim plus the diagonal bar, max(2 px, 0.22 r). The notch, which read as a bite, is removed.
- Not checked (朧月): a veiled 35% disc with six long dashes (56% duty) instead of eight short ones, so the gaps stay open at 12 px. It stays the faintest glyph and cannot be mistaken for Blocked's solid ring.

Every glyph carries a 1.4-unit dark seat (#0B0F1C at 55%) under its disc. It is invisible on Night, but it gives the edge back on bright game scenes. The renders show Completed and Not checked otherwise vanish there.

What I verified (scratchpad renders, not repo files): every state at 14, 16, 20, 24, 32 and 64 px on Night, on the Dalamud #141414 background and on a bright scene gradient, each in colour and greyscale (dirB/sheet.png), plus a 26 px row mock and the icon at 32-256 (dirB/zoom.png). Results:
- All eight states stay distinct in greyscale from about 20 px.
- At 14-16 px the outer channel still separates Ready (ring) from Ready on another job (arcs).
- The In journal bead needs the pixel minimums in the notes below to survive at 14 px.

Plugin icon: "Moon road on night water". It applies the Research 3 brief through Direction B:
- A pale pearl waxing crescent, lit on the right with horns pointing left. This is the opposite orientation to Dalamud's disabledIcon crescent. It sits over a midnight-to-indigo sky with an off-centre gold bloom, so the cut reads faintly as earthshine.
- A horizon hairline that fades at both ends instead of a hard band.
- Below it, a moonlight road of broken, rounded gold strokes that widen toward the viewer. This is the literal meaning of 月の道 and matches the in-plugin rail crest.
- No land path, no stones and no sparkle stars.
- An 11-unit Gilt rim gives about 5.9:1 against #101010, so the tile no longer disappears in the installer list.
- Moon versus sky is about 12:1 and road versus sea about 13:1. The moon sits in the top 60% and stays clear of the Installed check.

All art is original geometry built from existing GlyphTokens. Nothing is downloaded and nothing is from Square Enix.
- **Sumi to Kinpaku (Ink and Gold Leaf): Direction C, the tsukimi crest moons** — CONCEPT. The moon is drawn the way a kamon cutter or a Hiroshige block cutter draws it. It is a flat pale light set in a well of sumi ink, with gold leaf used only as thin cut strips (kirikane), never as paint. Three layers make up every glyph, and nothing else is allowed:
(1) a sumi ground disc #0B0F1C with a 1.5-unit earthshine keyline in Shadow #3A4363 (the old moon in the new moon's arms);
(2) one flat light shape, inset by the keyline;
(3) at most one mark.
There is no gradient, vignette, maria, craters, highlight arc or blur glow at any size. Those were the cheese cues, so they are deleted, not toned down.

COLOUR RULE (fixes cheese and enforces "gold means act now").
- Lit light is never yellow:
  - torinoko ivory #F5ECD2 for the two act states;
  - geppaku silver #D4DAE6 for the rest states;
  - a dimmer silver #B4BDD0 for Completed, so 300 finished rows stay calm.
- Gold leaf (kinpaku #E0B860) appears only on Ready's halo ring and In journal's seal. It is always a line or a small mark, under 15% of the glyph's area.
- Rose #C2707A (lighter and more traditional than the candy Eclipse pink) appears only on Locked out.
- Measured contrast on Night: ivory 15.5:1, silver 13.1, dim 9.7, gold 9.8, rose 5.1, Dusk dashes 6.0, Blocked ring 7.3. The earthshine keyline is 4.7:1 on a bright beige game scene, so every disc carries its own edge over the world.

THE EIGHT STATES. Each has a private tsukimi name (design vocabulary only; the UI stays English). Each differs by SHAPE, and colour is never the only carrier.
- READY (jogen, first quarter): a solid ivory right half, plus a separate full-opacity gold-leaf ring at 1.3r (tsukigasa, the lunar halo) with a sumi underlay so it survives bright scenes.
  - Ready is the only glyph with anything outside the disc, which keeps the existing rule.
  - The 35%-alpha 1 px ring and the three-disc puddle glow are replaced by one crisp ring.
  - It is now the loudest thing in a row.
- READY ON ANOTHER JOB (kage, the shadow crest): the same half moon, drawn as an outline only in silver (kage-mon, the outline form of a crest). Nothing is outside.
  - Solid half vs hollow half separates it from Ready in greyscale and deuteranopia, even though ivory and silver are only 1.19:1 apart.
  - The name also nods to Tsukikage.
- IN JOURNAL (jusan'ya, the thirteenth-night gibbous): ivory gibbous, plus a small gold-leaf diamond seal (rakkan) sitting in the dark sliver.
  - The mark sits on ink, never on the light, so it can never read as a cheese hole or an eyeball.
  - It is a non-round, pointed shape.
  - It separates In journal from Ready by mark and silhouette (no outer ring), not by a 1.3 px terminator shift.
- DONE THIS CYCLE (izayoi, the hesitating moon after full): a silver waning gibbous lit on the LEFT, with a sumi brush check across the light. Mirror image plus a check plus colour make three channels. The check answers "done" with no legend.
- COMPLETED (jugoya, full moon): the only full disc, in quiet dim silver.
  - At r 12 and up, two tapered kasumi mist wisps in Shadow cross the lower third, in the manner of ukiyo-e and the hanafuda susuki moon.
  - The wisps turn "coin or ball" into "moon behind thin cloud" and make the state recede.
  - Below r 12 it is a plain disc, which is already unique.
- BLOCKED (saku, new moon): a sumi disc with a solid pale-slate ring (gachirin). This is the only plain solid ring.
- LOCKED OUT (gesshoku, eclipse): a sumi disc, a rose ring and the thick diagonal bar. The bar stays because every focus-group member who reads by shape called it the best glyph. The bite-shaped notch is removed (it was "food" and a third idea).
- NOT CHECKED (oborozuki, hazy moon): hollow, no fill, five long dashes (44.7 degrees on, 27.3 off) with a gap at 12 o'clock.
  - The dashes stay open at 12 px, so it no longer reads as a dimmer Blocked.
  - It is the faintest glyph, as the raider asked.

LEGIBILITY. The greyscale and CVD silhouette set is: half+ring / hollow half / gibbous+diamond / mirrored gibbous+check / full disc / dark disc+ring / ring+bar / dashed ring. No pair differs only by mirror image or only by colour. All eight were rendered at 12, 14, 16, 20, 24, 32 and 64 px on Night, the Dalamud default #141414, a bright beige game tone and white, in colour and greyscale (scratchpad sheet2.png). All eight stay distinct at 14 px.

PLUGIN ICON (tsuki no michi on water).
- A full-bleed indigo tile. The sky lightens toward the horizon (#141C38 to #2A3A66) over a near-black sea, so the horizon reads through value, not a hard band.
- A horizontal silver hairline fades out at both ends.
- One large flat ivory waxing crescent, lit on the RIGHT with horns pointing left. This is the opposite of Dalamud's grey Disabled crescent and suggests growth. A very faint gold bloom (24% maximum) sits behind it.
- Directly below the moon's centre lies a moon road of kirikane strips: six rows of rounded gold-leaf strips. They run ivory at the horizon to gold at the front, widen toward the viewer, break into 2-3 staggered pieces lower down, and have no slivers under 2 px at 64 px.
- A 12-unit Gilt rim (85% alpha) separates the tile from the dark installer background.
- No stars, stones, land path, text or texture. Three hues only: indigo, gold and ivory, plus a touch of silver.
- The moon fills the top 60%, so the Installed check (bottom-right) covers only road tips and calm sea.
- Checked in Chrome at 256, 128, 64, 48 and 32 px, at 40% alpha (Disabled) and as a greyscale blur (scratchpad icon2.png). Crescent and road both survive at 32 px.
- This replaces the land path (which read as Questionable) with the literal meaning of the name, and it matches the in-plugin rail crest.

WHY THIS OVER A AND B ALONE. It keeps Research 1's B+A recommendation (flat kamon shapes, pale moonlight lit parts, gold as metal and signal). It adds what the focus group asked for that A and B lacked:
- one mark per state in the DEFAULT palette, so Standard and High contrast now share silhouettes and marks and differ only in tokens;
- Completed demoted;
- Not checked opened up;
- Ready made the loudest.
The ink keyline doubles as the overlay backing Research 2 asked for, so no separate drop shadow is needed.

Evidence (scratchpad only; the repo is untouched): C:\Users\devon\AppData\Local\Temp\claude\c--Users-devon-Desktop-Tsukimichi--Main-Repo-\60059d08-377c-464c-af3e-c2638d2288a4\scratchpad\dirC\ contains the eight state SVGs, icon.svg, sheet2.png (all states x 7 sizes x 4 grounds, colour and greyscale), zoom2.png (128 px and 16 px on Night), icon2.png (icon at 256-32 px, at 40% alpha and as a greyscale blur), and gen2.py.

### Focus group 2 (scores)
- **Veteran raider who plays daily and runs a crowded HUD (party list, cooldown timers, DoT trackers, several plugin windows). I glance at the quest list between pulls and want each state readable in under a second at 16 px, against busy, often bright scenes. I care about shape before colour, about "act now" being the loudest thing, and about nothing reading as food.** (favourite Sumi to Kinpaku (Direction C). It is the only set where every pair splits by silhouette at 16 px. The sumi seat keeps each glyph readable over bright game scenes, Ready on another job is told apart by solid vs hollow rather than by hue, and Done has a real check. Before shipping, move In journal's diamond clear of the terminator and thicken Ready's gold ring slightly so it is unmistakably the loudest glyph.): Moonlight Hairline (Direction A) 7 | Direction B: Tsukigasa (luminous minimal) 7 | Sumi to Kinpaku (Direction C) 8.5
- **Brand-new A Realm Reborn player. I'm still lost in the default HUD, the quest log and the duty finder, and I already have too many icons on screen. I don't know what tsukigasa or izayoi mean. I want to look at a row and know, without opening a legend, whether I can do the quest now, whether I've done it, or whether something is stopping me. I judged each set at 16 px in a list row and at 48 px, on dark navy. I also checked the contact sheets in scratchpad/dirA, dirB and dirC.** (favourite Sumi to Kinpaku (Direction C). As a new player I need one obvious thing per state, and C gives me exactly that: a gold ring for 'go do this', an outline for 'not on this job', a check for 'done', a bar for 'locked', and dashes for 'unknown'. I can learn it in one glance without the moon-phase story. A has the nicest Ready glyph and the best checkmark, but Ready and In journal look too alike at 16 px. B is the prettiest at 48 px, but its unmarked Done and its speck-sized bead would confuse me. My suggestions: take A's slightly bigger check placement into C, and make C's diamond seal a bit larger so it stays visible at 16 px.): Moonlight Hairline (Direction A) 7 | Direction B: Tsukigasa (luminous minimal) 6 | Sumi to Kinpaku (Direction C) 8
- **Deuteranope FFXIV player (green-weak, red and green collapse to khaki or grey for me, while blue versus yellow still reads). I tell glyphs apart by silhouette, rings, marks and footprint, never by hue. I rendered the SVGs in my head at 16 px and 48 px on dark navy (#0F1424) and ran the key colours through a Vienot deuteranopia transform. Key results: gold #F2D27A comes out as olive-yellow (220,220,120), ivory #F3EAD0 as pale cream (237,237,208), Eclipse #B25C7F as flat neutral grey (126,126,125), C's rose #C2707A as khaki-grey (142,142,120), and Mist #A9B2CC as lavender-grey (174,174,204). So for me gold still works as a signal, but every rose is just a grey or khaki line.** (favourite Sumi to Kinpaku (Direction C). It has the crispest, thickest shapes, one silhouette or mark per state, the strongest moon and brass elegance, and no cheese cues. As a deuteranope I never once had to read a hue to tell its states apart. I would ship it with three changes: (1) take B's split brass arcs for Ready on another job in place of the thin hollow D, (2) shrink or move In journal's diamond so it keeps a clear ink gap from the lit edge at 16 px, and (3) thin out the top rows of road strips in the icon at 32 px. I would also keep A's crescent-to-full phase story as a backup if the owner wants Ready to be a crescent; that only works if C's ring treatment and sumi underlay come with it.): Moonlight Hairline (Direction A) 7.2 | Direction B: Tsukigasa (luminous minimal) 6.9 | Sumi to Kinpaku (Direction C) 8.4
- **Glamour and aesthetics player. I spend more gil on dyes than on gear, I care whether a set reads as one cohesive outfit, and I notice when a gold trim is a shade too yellow. I mentally rendered every SVG at 48 px and 16 px on #0F1424 navy and judged it as I would a glamour plate: silhouette first, then palette discipline, then detail.** (favourite Sumi to Kinpaku (Direction C). It is the only set that reads as one designed family: muted gold-leaf brass instead of yellow, ink wells that remove the cut-wheel look, and a Completed glyph and plugin icon I would actually call beautiful. Before shipping I would borrow two things from A: (1) Done's check should sit in the dark half rather than be knocked out of the light, and (2) Ready on another job's hollow outline needs a thicker stroke or a faint silver wash so it holds at 14 to 16 px. If the owner wants more sparkle, B's split brass halo could replace C's hollow outline for that state.): Moonlight Hairline (Direction A) 7 | Direction B: Tsukigasa (luminous minimal) 6.5 | Sumi to Kinpaku (Direction C) 8.5
- **Japanese FFXIV player who loves understated tsukimi and moon motifs and traditional Japanese design (kamon, ukiyo-e, hanafuda, kirikane). I judged each SVG as if drawn at 16 px and 48 px on a dark navy background.** (favourite Sumi to Kinpaku (Direction C). It is the only set that reads as a family of crests rather than app icons: flat light in a sumi well, gold only as cut leaf, and one mark per state. Two changes would make it better. First, thicken the Ready-on-another-job outline so it stops reading as a \"D\". Second, move Done's check off the lit area onto the ink. I would also borrow A's ghost-limb hairline idea for the unlit side, and B's broken, shimmering road for the icon's water.): Moonlight Hairline (Direction A) 7 | Direction B: Tsukigasa (luminous minimal) 5.5 | Sumi to Kinpaku (Direction C) 8.5
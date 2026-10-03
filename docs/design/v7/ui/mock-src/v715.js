  // ======================================================================================================
  // 1.15 "Faces and icons": giver portraits (F2, F5) and icon-and-label action buttons (UI-5e)
  // ======================================================================================================
  var A15 = "1.15/";
  // The portrait grade (spec-1.15 §A3): a 3 x 4 sRGB colour matrix per source family. Colour families (Trust, Triple
  // Triad, delivery, pack photo) and the sepia battle-talk faces get their own, so the four sit together as one set.
  var GRADE = {
    colour: "0.6162 0.1372 0.0138 0 0.0147  0.0413 0.7224 0.0140 0 0.0196  0.0435 0.1462 0.6279 0 0.0353  0 0 0 1 0",
    bt: "0.4842 0.2856 0.0288 0 0.0147  0.0858 0.6923 0.0291 0 0.0196  0.0893 0.3003 0.4502 0 0.0353  0 0 0 1 0"
  };
  // Source families: texture size and the head crop (source px), as spec-1.15 §A2 gives them.
  var SRC = {
    trust: { w: 188, h: 480, crop: [6, 86, 140, 140], fam: "colour", label: "Duty Support portrait" },
    bt: { w: 640, h: 512, crop: [164, 154, 172, 172], fam: "bt", label: "battle dialogue portrait" },
    tt: { w: 208, h: 256, crop: [28, 20, 135, 135], fam: "colour", label: "Triple Triad card art" },
    sat: { w: 400, h: 480, crop: [104, 154, 158, 158], fam: "colour", label: "custom delivery portrait" }
  };
  var GIVERS = {
    // crop: the curated per-icon box (curated/giver_portraits.json), measured to the framing rule; keyed: the delivery
    // emblem script keyed out before grading (spec-1.15 §A2.2)
    alphinaud: { name: "Alphinaud", file: "072621", src: "trust", crop: [10, 107, 143, 143], place: "The Rising Stones · Mor Dhona" },
    yshtola: { name: "Y'shtola", file: "073025", src: "bt", crop: [171, 150, 168, 168], place: "The Rising Stones · Mor Dhona" },
    alphinaudBt: { name: "Alphinaud", file: "073034", src: "bt", crop: [158, 162, 177, 177], place: "The Rising Stones · Mor Dhona" },
    yshtolaTrust: { name: "Y'shtola", file: "072626", src: "trust", crop: [0, 66, 139, 139], place: "The Rising Stones · Mor Dhona" },
    tataru: { name: "Tataru Taru", file: "087019", src: "tt", crop: [70, 62, 75, 75], place: "The Rising Stones · Mor Dhona" },
    cid: { name: "Cid Garlond", file: "087058", src: "tt", crop: [28, 20, 135, 135], place: "Garlond Ironworks" },
    zhloe: { name: "Zhloe Aliapoh", file: "061661", keyed: true, src: "sat", crop: [106, 161, 158, 158], place: "Idyllshire" },
    mnaago: { name: "M'naago", file: "061662", keyed: true, src: "sat", crop: [102, 153, 157, 157], place: "Rhalgr's Reach" },
    masked: { name: "Hidden by the spoiler shield", place: "Shown once you reach this quest" }
  };
  // One plate for every portrait and fallback: the medal's well, the face, an inner shadow on the upper-left lip, the
  // moonlight wash, and the keyline (brass at Full, silver at Quiet, a line at Plain).
  function plate(size, lv, kind, o) {
    o = o || {};
    var id = "pp" + (++uid), full = lv === "full", plain = lv === "plain", inner = "";
    var defs = '<linearGradient id="' + id + 'w" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="' + (plain ? "#1C2237" : "#1D2B5A") + '"/><stop offset="1" stop-color="' + (plain ? "#1C2237" : "#131C40") + '"/></linearGradient>' +
      '<clipPath id="' + id + 'c"><circle cx="36" cy="36" r="34.5"/></clipPath>' +
      '<linearGradient id="' + id + 'k" x1="8" y1="6" x2="64" y2="68" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E6CF98"/><stop offset=".45" stop-color="#9A7E4A"/><stop offset=".8" stop-color="#7C6236"/><stop offset="1" stop-color="#5C4724"/></linearGradient>';
    if (kind === "face") {
      var g = GIVERS[o.giver], s = SRC[g.src], c = o.crop || g.crop || s.crop, k = 69 / c[2];
      defs += '<filter id="' + id + 'g" color-interpolation-filters="sRGB"><feColorMatrix type="matrix" values="' + GRADE[s.fam] + '"/></filter>';
      inner += '<image href="' + A15 + "art/" + g.file + (g.keyed && !o.unkeyed ? "-keyed" : "") + '.png" x="' + (1.5 - c[0] * k).toFixed(2) + '" y="' + (1.5 - c[1] * k).toFixed(2) + '" width="' + (s.w * k).toFixed(2) + '" height="' + (s.h * k).toFixed(2) + '"' + (o.raw ? "" : ' filter="url(#' + id + 'g)"') + (o.alpha != null ? ' opacity="' + o.alpha + '"' : "") + ' preserveAspectRatio="none"/>';
    } else if (kind === "sil") {
      inner += '<image href="' + A15 + "silhouettes/" + o.sil + '.svg" x="4" y="6" width="64" height="64"/>';
    } else if (kind === "emblem") {
      if (size >= 32) defs += '<filter id="' + id + 'e" x="-20%" y="-20%" width="140%" height="140%" color-interpolation-filters="sRGB"><feDropShadow dx="' + (0.8 * 72 / size).toFixed(2) + '" dy="' + (1.1 * 72 / size).toFixed(2) + '" stdDeviation="' + (1 * 72 / size).toFixed(2) + '" flood-color="#080B16" flood-opacity=".45"/></filter>';
      inner += '<image href="' + A15 + "icons/" + o.icon + '.png" x="13" y="13" width="46" height="46" opacity=".92"' + (size >= 32 ? ' filter="url(#' + id + 'e)"' : "") + "/>";
    } else if (kind === "initials" && size < 20) {
      inner += '<image href="' + A15 + 'silhouettes/moon-disc.svg" x="4" y="4" width="64" height="64"/>';
    } else if (kind === "initials" && size < 32) {
      // one initial, caps at least 9 px: Marcellus caps are about .70 em, so the font is 13 px on screen
      var fsu = 13 * 72 / size;
      inner += '<text x="36" y="' + (36 + fsu * 0.35).toFixed(1) + '" text-anchor="middle" font-family="Marcellus, Georgia, serif" font-size="' + fsu.toFixed(1) + '" fill="#E9E4D2" fill-opacity=".95">' + o.text.charAt(0) + "</text>";
    } else if (kind === "initials") {
      inner += '<text x="36" y="' + (o.text.length > 1 ? 45.5 : 47) + '" text-anchor="middle" font-family="Marcellus, Georgia, serif" font-size="' + (o.text.length > 1 ? 26 : 30) + '" letter-spacing=".5" fill="#E9E4D2" fill-opacity=".92">' + o.text + "</text>";
    }
    var lip = full && size >= 32;
    if (lip) {
      defs += '<mask id="' + id + 'm"><rect width="72" height="72" fill="#fff"/><circle cx="38.6" cy="39.4" r="34.5" fill="#000"/></mask>' +
        '<filter id="' + id + 'b" x="-10%" y="-10%" width="120%" height="120%" color-interpolation-filters="sRGB"><feGaussianBlur stdDeviation="1.4"/></filter>' +
        '<radialGradient id="' + id + 'h" cx="20" cy="16" r="40" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#FFF0BE" stop-opacity=".07"/><stop offset="1" stop-color="#FFF0BE" stop-opacity="0"/></radialGradient>';
    }
    var svg = '<svg class="pp' + (o.fade ? " fade" : "") + '" width="' + size + '" height="' + size + '" viewBox="0 0 72 72" aria-hidden="true"><defs>' + defs + "</defs>" +
      '<circle cx="36" cy="36" r="35" fill="url(#' + id + 'w)"/><g clip-path="url(#' + id + 'c)">' + inner +
      (lip ? '<circle cx="36" cy="36" r="35.5" fill="#080B16" fill-opacity=".55" mask="url(#' + id + 'm)" filter="url(#' + id + 'b)"/><circle cx="36" cy="36" r="35" fill="url(#' + id + 'h)"/>' : "") + "</g>" +
      (full ? '<circle cx="36" cy="36" r="35.6" fill="none" stroke="#080B16" stroke-opacity=".6" stroke-width="1" vector-effect="non-scaling-stroke"/><circle cx="36" cy="36" r="34.9" fill="none" stroke="url(#' + id + 'k)" stroke-width="1" vector-effect="non-scaling-stroke"/>'
        : '<circle cx="36" cy="36" r="34.9" fill="none" stroke="' + (plain ? "#3A4050" : "#C3CBDF") + '" stroke-opacity="' + (plain ? 1 : 0.62) + '" stroke-width="1" vector-effect="non-scaling-stroke"/>') + "</svg>";
    return svg;
  }
  function face(size, lv, giver, extra) { return plate(size, lv, "face", Object.assign({ giver: giver }, extra || {})); }
  function ic15(file, px, cls) { return '<img class="gi ' + (cls || "") + '" src="' + A15 + "icons/" + file + '.png" width="' + px + '" height="' + px + '" alt="">'; }

  function giverCard(lv, g, plateHtml, cap) {
    var G = GIVERS[g] || { name: g, place: "" };
    if (lv === "plain") {
      return '<div class="mk plain v7 gcard-p"><div class="ps">Giver<span></span></div><dl class="kv"><dt>Giver</dt><dd>' + plateHtml + "<span>" + esc(G.name) + '</span><span class="mu">· ' + esc(G.place) + "</span></dd></dl></div>";
    }
    return '<div class="mk ' + lv + ' v7 gcard"><div class="card">' + (lv === "full" ? '<i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i>' : "") +
      "<h4>Giver" + (cap ? '<span class="r">' + cap + "</span>" : "") + '</h4><div class="gv">' + plateHtml + '<div class="gt"><b>' + esc(G.name) + "</b><span>" + esc(G.place) + "</span></div></div></div></div>";
  }
  function tip15(g, lvSize) {
    var G = GIVERS[g], s = SRC[G.src];
    return '<div class="mk full v7"><div class="tip t15"><i class="cm a"></i><i class="cm d"></i>' + face(128, "full", g) +
      "<b>" + esc(G.name) + '</b><p class="src">Portrait: ' + s.label + "</p><p>" + esc(G.place) + "</p></div></div>";
  }

  function boardGiver() {
    var h = '<div class="board b15"><h2>1.15 · Giver portraits<small>Real game art read at runtime (Lumina, 2026.09.15 client): a Duty Support portrait, a battle dialogue portrait, Triple Triad card art and a custom delivery portrait, each cropped by its family\'s box and night-graded by one colour matrix.</small></h2>';
    h += '<div class="row">';
    [["alphinaud", "Duty Support portrait · 072621"], ["yshtola", "Battle dialogue portrait · 073025"], ["tataru", "Triple Triad card · 087019"], ["zhloe", "Custom delivery portrait · 061661"]].forEach(function (x) {
      var G = GIVERS[x[0]];
      h += fig(giverCard("full", x[0], face(72, "full", x[0])) +
        '<div class="rawg">' + face(56, "full", x[0], { raw: true }) + '<span class="ar">→</span>' + face(56, "full", x[0]) + "<i>ungraded → graded</i></div>", "<b>Full</b>, 72 px · <i>" + x[1] + "</i>");
    });
    h += "</div><div class=\"row\">";
    h += fig(giverCard("quiet", "yshtolaTrust", face(64, "quiet", "yshtolaTrust")), "<b>Quiet</b>, 64 px: silver hairline, no inner shadow or wash");
    h += fig(giverCard("plain", "cid", face(18, "plain", "cid")) + giverCard("plain", "mnaago", face(18, "plain", "mnaago")), "<b>Plain</b>: 18 px inline before the name, no fade");
    h += fig(tip15("tataru"), "<b>Hover</b>: 128 px, the name in the Title face, the source line, the place");
    h += fig('<div class="fades">' + [0, 0.58, 0.88, 1].map(function (a) { return "<div>" + face(72, "full", "alphinaudBt", { alpha: a }) + "</div>"; }).join("") + '</div><div class="fadecap"><span>0 s (plate)</span><span>0.1 s</span><span>0.2 s</span><span>0.3 s</span></div>', "<b>A new selection</b>: the face fades in over 0.3 s (ease-out cubic) on the plate that was already there; the plate never moves or blinks. No fade under Reduce motion or at Plain.");
    h += "</div><div class=\"row\">";
    // Avatars: Next stops / Route / Tonight, and the optional journal column
    var stop = function (g, verb, place, iconFile) { var G = GIVERS[g]; return '<div class="stp15">' + face(24, "full", g) + '<div class="st"><b>' + verb + " " + esc(G.name) + "</b><span>" + place + '</span></div><span class="sic">' + ic15(iconFile, 18) + "</span></div>"; };
    h += fig('<div class="mk full v7"><div class="card c15"><i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i><h4>Next stops</h4>' +
      stop("tataru", "Talk to", "The Rising Stones · 2 stops", "060453") + stop("zhloe", "Deliver to", "Idyllshire · teleport", "060453") + stop("cid", "Talk to", "Garlond Ironworks · walk", "000104") + "</div></div>", "<b>Next stops</b>, <b>Route</b>, <b>Tonight</b>: a 24 px avatar beside each stop, so you know who to walk up to");
    h += fig('<div class="mk full v7"><div class="card c15"><i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i><h4>Route · 3 stops</h4>' +
      '<div class="rt"><span class="n">1</span>' + face(24, "full", "mnaago") + '<div class="st"><b>M\'naago</b><span>Rhalgr\'s Reach</span></div></div>' +
      '<div class="rt"><span class="n">2</span>' + plate(24, "full", "sil", { sil: "lalafell-male" }) + '<div class="st"><b>troubled merchant</b><span>Ul\'dah · Steps of Thal</span></div></div>' +
      '<div class="rt"><span class="n">3</span>' + plate(24, "full", "emblem", { icon: "065039" }) + '<div class="st"><b>Mogek the Marvelous</b><span>Moghome</span></div></div></div></div>', "<b>Route</b>: a named face, a race silhouette, a society emblem: every stop has a picture");
    var jr = function (g, n, st, pl, who) { return '<div class="jr">' + rowGlyph("full", st) + '<span class="qn">' + n + "</span>" + (pl || face(20, "full", g)) + '<span class="gn">' + (who || GIVERS[g].name) + "</span></div>"; };
    h += fig('<div class="mk full v7"><div class="card c15 jt15"><i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i><h4>Journal · Giver column<span class="r">optional, off by default</span></h4>' +
      jr("tataru", "Pinned quest", "in-journal") + jr("cid", "Pinned quest", "completed") + jr("", "Pinned quest", "not-checked", plate(20, "full", "sil", { sil: "hyur-female" }), "troubled adventurer") + jr("", "Pinned quest", "ready", plate(20, "full", "emblem", { icon: "065016" }), "Amalj'aa quartermaster") + "</div></div>", "<b>Journal</b>: a 20 px avatar before the giver's name in a Giver column, <b>off by default</b> (the table's Columns menu)");
    h += "</div>";
    // The DataGen contact sheet (spec-1.15 §A2.3): every crop at 120 px with its guide bands
    h += '<div class="row"><div class="cs15">';
    ["alphinaud", "yshtolaTrust", "yshtola", "alphinaudBt", "tataru", "cid", "zhloe", "mnaago"].forEach(function (k) {
      var G = GIVERS[k], s = SRC[G.src], c = G.crop;
      h += '<figure><div class="gd">' + face(120, "full", k, { raw: true }) + '<svg viewBox="0 0 120 120" class="gb"><rect x="0" y="9.6" width="120" height="4.8" fill="#50C8FF" fill-opacity=".22"/><rect x="0" y="50.4" width="120" height="4.8" fill="#FFDC50" fill-opacity=".30"/><rect x="0" y="93.6" width="120" height="7.2" fill="#FF5A5A" fill-opacity=".26"/>' +
        '<line x1="0" y1="52.8" x2="120" y2="52.8" stroke="#FFDC50" stroke-width=".8"/><line x1="0" y1="97.2" x2="120" y2="97.2" stroke="#FF5A5A" stroke-width=".8"/><line x1="27" y1="112" x2="27" y2="118" stroke="#fff" stroke-opacity=".6"/><line x1="93" y1="112" x2="93" y2="118" stroke="#fff" stroke-opacity=".6"/></svg></div>' +
        "<figcaption><b>" + G.name + "</b> " + G.file + "<br><i>" + s.label + " · box " + c.join(", ") + (G.keyed ? " · script keyed" : "") + "</i></figcaption></figure>";
    });
    // The keyed delivery crops at 128 px (the hover size): source, keyed and ungraded, keyed and graded
    ["zhloe", "mnaago"].forEach(function (k) {
      var G = GIVERS[k];
      h += '<figure class="k128"><div class="kr">' + face(128, "full", k, { raw: true, unkeyed: true }) + face(128, "full", k, { raw: true }) + face(128, "full", k) + "</div><figcaption><b>" + G.name + "</b> " + G.file + " at 128 px: <i>source · keyed · keyed and graded</i></figcaption></figure>";
    });
    h += '</div><div class="spec" style="width:300px"><b>Guide bands</b> (DataGen contact sheet): crown 8–12 % (blue), eye line 42–46 % (gold), chin 78–84 % (red); the ticks at the foot mark a 55 % face width. Curation moves a box until the eyes sit in the gold band and the chin in the red one.</div></div>';
    h += '<div class="spec" style="width:470px"><b>The grade</b> (sRGB, applied to a graded copy as BannerGrading does; until it lands the source draws with the multiply only as a tint): desaturate toward Rec. 709 luma (colour families .25, battle dialogue .50), multiply by the night tint at .22 (.18), scale .94, lift the blacks by Night x .25.' +
      '<table><tr><th>Colour families</th><th>Battle dialogue (sepia)</th></tr><tr><td><code>.616 .137 .014 +.015<br>.041 .722 .014 +.020<br>.044 .146 .628 +.035</code></td><td><code>.484 .286 .029 +.015<br>.086 .692 .029 +.020<br>.089 .300 .450 +.035</code></td></tr></table></div>';
    return h + "</div></div>";
  }

  function boardFallbacks() {
    var h = '<div class="board b15"><h2>1.15 · When there is no face<small>Every fallback sits on the same plate as a face, so nothing reads as missing. One ink (MoonstoneHigh #C9D3EA at .86), flat, no light: a silhouette says "a person of this kind", never a face.</small></h2>';
    var races = ["hyur", "elezen", "lalafell", "miqote", "roegadyn", "aura", "hrothgar", "viera"], names = { hyur: "Hyur", elezen: "Elezen", lalafell: "Lalafell", miqote: "Miqo'te", roegadyn: "Roegadyn", aura: "Au Ra", hrothgar: "Hrothgar", viera: "Viera" };
    h += '<div class="silgrid">';
    races.forEach(function (r) {
      ["male", "female"].forEach(function (gd) { h += '<figure class="sg">' + plate(72, "full", "sil", { sil: r + "-" + gd }) + "<figcaption><b>" + names[r] + "</b> " + gd + "</figcaption></figure>"; });
    });
    h += '<figure class="sg">' + plate(72, "full", "sil", { sil: "moon-disc" }) + "<figcaption><b>Non-humanoid</b> moon disc</figcaption></figure></div>";
    h += '<div class="row">';
    h += fig('<div class="frow">' + plate(72, "full", "emblem", { icon: "065016" }) + plate(72, "full", "emblem", { icon: "065020" }) + plate(72, "full", "emblem", { icon: "065039" }) + "</div>", "<b>Allied society</b>: the society's emblem (BeastTribe.Icon) as a square tile at 64 % of the plate, ungraded, with a soft down-right shadow from 32 px: Amalj'aa, Sahagin, moogles");
    h += fig('<div class="frow">' + plate(72, "full", "initials", { text: "MM" }) + plate(72, "full", "initials", { text: "HG" }) + plate(72, "full", "initials", { text: "G" }) + "</div>", "<b>Named, no art</b>: initials in the Title face from 32 px (Mother Miounne → MM, Hamujj Gah → HG, Gerolt → G); below 32 px the first initial only, caps at least 9 px; at 18 px the moon disc");
    h += fig(giverCard("full", "masked", plate(72, "full", "sil", { sil: "elezen-female" }), "masked"), "<b>Spoiler shield</b>: a masked quest never shows a face, even when one exists. It falls back to the silhouette (or initials when the race is unknown)");
    h += "</div><div class=\"row\">";
    var sizes = [[72, "full"], [64, "quiet"], [24, "full"], [20, "full"], [18, "plain"]];
    [["face", { giver: "tataru" }], ["sil", { sil: "miqote-female" }], ["emblem", { icon: "065039" }], ["initials", { text: "MM" }], ["sil", { sil: "moon-disc" }]].forEach(function (x) {
      h += fig('<div class="frow sz">' + sizes.map(function (s) { return plate(s[0], s[1], x[0], x[1]); }).join("") + "</div>", "<i>72 Full · 64 Quiet · 24 avatar · 20 column · 18 Plain</i>");
    });
    return h + "</div></div>";
  }

  // ---------- Buttons (UI-5e) ----------
  var IC = {
    go: ["071221", "Go to giver", "Go to", "marker"], teleport: ["060453", "Teleport", "Teleport", "marker"], walk: ["000104", "Walk to giver", "Walk", "tile"],
    flag: ["060561", "Flag", "Flag", "marker"], map: ["000007", "Open map", "Map", "tile"], journal: ["book", "Read the journal", "Journal", "glyph"],
    aether: ["060430", "Aethernet", "Aethernet", "marker"], ferry: ["060456", "Ferry", "Ferry", "marker"], mount: ["000118", "Mount up", "Mount", "tile"], fly: ["000122", "Fly", "Fly", "tile"],
    ret: ["000112", "Return", "Return", "tile"], duty: ["000046", "Duty Finder", "Duty", "tile"]
  };
  function pill15(lv, key, mode, pri, over) {
    var x = IC[key], px = lv === "full" ? 18 : lv === "quiet" ? 16 : 14, lab = mode === "short" ? x[2] : mode === "icon" ? "" : (over || x[1]);
    return '<span class="p15 ' + lv + (pri ? " pri" : "") + (mode === "icon" ? " io" : "") + '" title="' + (over || x[1]) + '">' + ic15(x[0], px, x[3]) + (lab ? "<span>" + lab + "</span>" : "") + "</span>";
  }
  function hop15(lv, n) { return '<span class="p15 ' + lv + '" title="Hop to instance ' + n + '"><span class="inst">' + n + "</span><span>Instance " + n + "</span></span>"; }
  function bar15(lv, w, mode) {
    return '<div class="mk ' + lv + ' v7"><div class="bar15" style="width:' + w + 'px">' + pill15(lv, "go", mode, true) + pill15(lv, "teleport", mode) + pill15(lv, "walk", mode) + "</div>" +
      '<div class="rb15">' + ["flag", "map"].map(function (k) { return '<span class="rb ' + lv + '" title="' + IC[k][1] + '">' + ic15(IC[k][0], lv === "plain" ? 12 : 14, IC[k][3]) + "</span>"; }).join("") +
      '<span class="rb ' + lv + ' bk" title="Read the journal">' + ICON.book + "</span>" +
      ["pin", "path", "link", "copy", "more"].map(function (k) { return '<span class="rb ' + lv + '">' + ICON[k] + "</span>"; }).join("") + "</div></div>";
  }
  function boardButtons() {
    var h = '<div class="board b15"><h2>1.15 · Icon-and-label buttons<small>Every travel and route action gets the game\'s own icon. Map symbols are drawn bare; action tiles keep their frame at a small radius; actions with no game icon keep their FontAwesome glyph. Icons carry meaning, so they keep their colours at every level.</small></h2>';
    h += '<div class="row">';
    h += fig(bar15("full", 380, "full"), "<b>Full</b>, the detail action bar: 30 px pills, 18 px icons, gap 6");
    h += fig(bar15("quiet", 356, "full"), "<b>Quiet</b>: 28 px, 16 px icons");
    h += fig(bar15("plain", 306, "full"), "<b>Plain</b>: 22 px buttons, 14 px icons, gap 4 (was text only)");
    h += "</div><div class=\"row\">";
    h += fig(bar15("full", 300, "short"), "<b>Narrower</b>: the short labels (ActionPillFit step 2)");
    h += fig(bar15("full", 170, "icon"), "<b>Narrowest</b>: icon-only pills (1.45 x height), the label in the tooltip; never clipped");
    h += '<div class="spec" style="width:520px"><table><tr><th>Anatomy</th><th>Full</th><th>Quiet</th><th>Plain</th></tr>' +
      "<tr><td>Height</td><td>30</td><td>28</td><td>22</td></tr><tr><td>Icon</td><td>18</td><td>16</td><td>14</td></tr><tr><td>Pad start / icon gap / pad end</td><td>11 / 6 / 13</td><td>10 / 6 / 12</td><td>7 / 4 / 8</td></tr>" +
      "<tr><td>Label</td><td>12 px, Text (Primary: gold pill, ink #1A1406)</td><td>12 px</td><td>11.5 px</td></tr>" +
      "<tr><td>Tile icons</td><td colspan=\"3\">action tiles (Sprint, Mount, Map…) at the icon size, radius 3, a 1 px Abyss .5 inset ring; map symbols bare</td></tr>" +
      "<tr><td>Fit</td><td colspan=\"3\">full label → short label → icon-only (1.45 x height) with the full label as the tooltip's first line. Never clipped, never an ellipsis</td></tr>" +
      "<tr><td>Disabled</td><td colspan=\"3\">icon at .45 alpha and desaturated by the tint (#8A93B0), label TextDisabled; the reason in the tooltip</td></tr></table></div>";
    h += "</div><div class=\"row\">";
    // Route window
    h += fig('<div class="mk full v7"><div class="route15"><div class="rh">' + ic15("071221", 22, "marker") + '<b>Route to Hihibaru</b><span>4 stops · about 3 min</span></div>' +
      [["teleport", "Teleport to Ul'dah - Steps of Nald", "60 gil"], ["aether", "Aethernet to the Adventurers' Guild", "via Lifestream"], ["walk", "Walk to Hihibaru", "vnavmesh"], ["mount", "Mount up", "Mount Roulette"]].map(function (r, i) {
        return '<div class="rr"><span class="n">' + (i + 1) + "</span>" + pill15("full", r[0], "full", i === 0, r[1]) + '<span class="m">' + r[2] + "</span></div>";
      }).join("") + '<div class="rr"><span class="n">5</span>' + hop15("full", 2) + '<span class="m">the target is in instance 2</span></div></div></div>',
      "<b>Route window</b>: the header takes the target's own icon (here a sidequest marker; a duty takes its ContentType icon); every step is an icon-and-label pill. Instance numbers use the game font's instance glyphs (SeIconChar.Instance1–9), shown here as a stand-in");
    // In-game panel
    h += fig('<div class="mk full v7"><div class="igp"><div class="igh">Blue Collar Work</div><div class="igr">' + pill15("full", "go", "short", true) + pill15("full", "teleport", "full") + pill15("full", "flag", "full") + "</div></div></div>", "<b>In-game panels</b> (offer, result, journal companion): the same pills at 26 px with 16 px icons");
    // Requirements with icons
    var rq = function (ic, cls, lab, d, ok) { return '<div class="rq">' + (ok ? ICON.check : ICON.xmark) + ic15(ic, 18, cls) + '<span class="l">' + lab + '</span><span class="d' + (ok ? "" : " ec") + '">' + d + "</span></div>"; };
    h += fig('<div class="mk full v7"><div class="card c15"><i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i><h4>Requirements<span class="r ec">2 of 4 unmet</span></h4>' +
      rq("062136", "tile", "Class or job", "Blue Mage", false) + rq("062136", "tile", "Level", "needs level 10, BLU is 1", false) + rq("071221", "marker", "Previous quests", "Blue Leading the Blue done", true) + rq("065039", "tile", "Allied society", "Moogles rank 2, you have it", true) +
      '<h4 style="margin-top:12px">Rewards</h4><div class="rwi">' + ic15("065001", 18, "tile") + "<span>855 EXP</span>" + ic15("065002", 18, "tile") + "<span>414 gil</span></div></div></div>", "<b>Requirements and rewards</b>: the job's icon (62100 + job), the previous quest's marker, the society emblem; EXP and gil icons");
    h += "</div>";
    // Icon table
    var rows = [["Go to giver (Questionable)", "071201 / 071221 / 071341", "the quest's own map marker: MSQ / side / feature", "map symbol"], ["Teleport", "060453", "MapSymbol 1 · Aetheryte", "map symbol"], ["Return", "000112", "GeneralAction 8 · Return", "tile"],
      ["Walk (vnavmesh)", "000104", "GeneralAction 4 · Sprint", "tile"], ["Flag on the map", "060561", "map flag marker", "map symbol"], ["Open map", "000007", "MainCommand 16 · Map", "tile"],
      ["Aethernet (Lifestream)", "060430", "MapSymbol 2 · Aethernet Shard", "map symbol"], ["Ferry / airship", "060456", "MapSymbol 15 · Ferry Docks", "map symbol"], ["Mount up", "000118", "GeneralAction 9 · Mount Roulette", "tile"],
      ["Fly", "000122", "GeneralAction 24 · Flying Mount Roulette", "tile"], ["Duty / Duty Finder", "000046 · ContentType.Icon", "MainCommand 33 · Duty Finder", "tile"], ["Read the journal", "book glyph", "the approved Journal book (MedalArt.RowGlyph Journal, _row/badge-journal.svg) in the button's ink; not the red 000005 tile (red is Locked out's)", "glyph"],
      ["Instance hop", "SeIconChar Instance1–9", "U+E0B1–E0B9 in the Axis game font", "glyph"], ["Job requirement", "62100 + ClassJob", "ClassJob icons (BLU 62136)", "tile"], ["Allied society", "BeastTribe.Icon", "65016 Amalj'aa … 65131 Yok Huy", "tile"],
      ["EXP · gil", "065001 (verify) · 065002", "reward currency icons", "tile"], ["Stop, Pin, Path, Link, Copy, Report, More", "—", "FontAwesome (no game icon)", "glyph"]];
    h += '<div class="spec" style="width:100%"><table><tr><th>Action</th><th>Icon id</th><th>Sheet</th><th>Style</th><th>Seen</th></tr>' + rows.map(function (r) {
      var shown = /^0\d{5}$/.test(r[1].slice(0, 6)) ? ic15(r[1].slice(0, 6), 18, r[3]) : "";
      return "<tr><td>" + r[0] + "</td><td><code>" + r[1] + "</code></td><td>" + r[2] + "</td><td>" + r[3] + "</td><td>" + shown + "</td></tr>";
    }).join("") + "</table></div>";
    return h + "</div>";
  }


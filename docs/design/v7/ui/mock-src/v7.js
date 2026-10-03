(function () {
  "use strict";
  var M = "../../moon-v6/round5/medallion-r5/", MOON7 = "completed-moon/completed-v7.svg", MOON7S = "completed-moon/completed-v7-small.svg";
  var SCENE = { day: "../../../plan-site/mock/scene-day.jpg", night: "../../../plan-site/mock/scene-night.jpg" };
  var NOTE = {
    full: "Full: bigger gilt section headings and column headers, the rail without its thread, the three-layer sky and the new Completed moon.",
    quiet: "Quiet: body-face headings at 1.15x, flat station plates, a flat drawer sheet. No stars, no glow.",
    plain: "Plain: headings in the text tone on a band, a ledger drawer with checkboxes. Nothing moves.",
    drawer: "The filter drawer at each level, Advanced collapsed and expanded, against 1.13 as shipped.",
    rail: "The rail: 1.13 against v7 at each level, and every station state with its timing.",
    stars: "The Full sky: three depths, four temperatures, a slow twinkle, the region constellations, the completion meteor and an optional band.",
    ba: "1.13 against v7 at Full, with the quest pane's headings and the column headers at true size.",
    "snow-full": "1.16: Ishgard Glass on Ishgard Snow, Decoration Full: a dawn sky with one morning star, came (lead) frames, cool shadows.",
    "snow-full-drawer": "1.16: Ishgard Snow at Full with the filter drawer open.",
    "snow-quiet": "1.16: Ishgard Snow at Quiet, the filter drawer open.",
    "snow-plain": "1.16: Ishgard Snow at Plain, the drawer open with Advanced.",
    palettes: "1.16: Night, Ishgard Snow and their high-contrast forms, the contrast table, portraits on light.",
    themes: "1.16: Settings › Themes, and how 1.17's mix-and-match fits the same page.",
    mix: "1.17: the per-state mix, its warnings and Fix it.", frames: "1.17: four frame kits on four palettes.", share: "1.17: share codes.",
    glyphwin: "1.17: the glyph window's Themes tab.", palettes17: "1.17: Dawn and Kugane Lacquer with their high-contrast forms.",
    "dawn-full": "1.17: Dawn, Full, with Astrologian's Orrery.", "dawn-quiet": "1.17: Dawn, Quiet.", "kugane-full": "1.17: Kugane Lacquer, Full, with Menphina's Medallion.", "kugane-quiet": "1.17: Kugane Lacquer, Quiet.",
    giver: "1.15: giver portraits from the game's own art, night-graded, on the medal plate; avatars in Next stops, Route and the Journal.",
    fallbacks: "1.15: the 16 race silhouettes, the moon disc, society emblems and initials, on the same plate.",
    buttons: "1.15: icon-and-label buttons with the game's own icons, per level, and how they shrink.",
    sky: "Revision 3: the moving night sky at Full. 6 px a minute, paused while the window is unfocused; a rare faint meteor. Use x30 to see it move."
  };
  var NAME = { "ready": "Ready", "ready-on-another-job": "Ready on another job", "in-journal": "In journal", "blocked": "Blocked",
    "done-this-cycle": "Done this cycle", "completed": "Completed", "locked-out": "Locked out", "not-checked": "Not checked" };
  var MEAN = { "ready": "Every requirement is met on the current job; go get it." };
  var INK = { "ready": "#F2D27A", "in-journal": "#F2D27A", "completed": "#F2D27A", "ready-on-another-job": "#DDE3F0", "done-this-cycle": "#DDE3F0",
    "blocked": "#A9B2CC", "locked-out": "#D68AA8", "not-checked": "#8A93B0" };
  var STRIPE = { "ready": ["#F2D27A", ""], "in-journal": ["#F2D27A", ""], "completed": ["rgba(214,178,90,.55)", ""], "ready-on-another-job": ["#DDE3F0", ""],
    "done-this-cycle": ["rgba(221,227,240,.55)", ""], "blocked": ["rgba(124,134,168,.8)", ""], "locked-out": ["#B25C7F", "dash"], "not-checked": ["#5C6584", "dot"] };

  // The v13 mock's quests, with the owner's quest (Blue Collar Work, from owner-quest-pane.png) selected in Class & Job.
  var Q = [
    { g: "Main Scenario", st: "completed", n: "The Ultimate Weapon", lv: 50, job: "All", lab: "Completed", det: "2024-11-02", xp: "ARR" },
    { g: "Main Scenario", st: "completed", n: "Heavensward", lv: 60, job: "All", lab: "Completed", det: "2025-01-04", xp: "HW" },
    { g: "Main Scenario", st: "completed", n: "Stormblood", lv: 70, job: "All", lab: "Completed", det: "2025-01-29", xp: "SB" },
    { g: "Main Scenario", st: "completed", n: "Shadowbringers", lv: 80, job: "All", lab: "Completed", det: "2025-02-21", xp: "ShB" },
    { g: "Main Scenario", st: "completed", n: "Endwalker", lv: 90, job: "All", lab: "Completed", det: "2025-03-18", xp: "EW" },
    { g: "Main Scenario", st: "in-journal", n: "Dawntrail", lv: 100, job: "All", lab: "In journal", det: "step 4 of 6", xp: "DT" },
    { g: "Main Scenario", st: "blocked", n: "Crossroads", lv: 100, job: "All", lab: "Blocked", det: "after MSQ: The Warmth of Family", xp: "DT" },
    { g: "Main Scenario", st: "locked-out", n: "The Company You Keep (Maelstrom)", lv: 20, job: "All", lab: "Locked out", det: "Another Grand Company (Order of the Twin Adder)", xp: "ARR" },

    { g: "Class & Job Quests", st: "completed", n: "Heart of the Forest", lv: 50, job: "WHM", lab: "Completed", det: "2024-12-09", xp: "ARR" },
    { g: "Class & Job Quests", st: "completed", n: "Blue Leading the Blue", lv: 1, job: "BLU", lab: "Completed", det: "2025-08-30", xp: "ARR" },
    { g: "Class & Job Quests", st: "blocked", n: "Blue Collar Work", lv: 10, job: "BLU", lab: "Blocked", det: "needs BLU level 10", xp: "ARR", sel: true },
    { g: "Class & Job Quests", st: "ready-on-another-job", n: "The Narwhal Beckons", lv: 90, job: "Tank", lab: "Ready on PLD", det: "", xp: "DT" },
    { g: "Class & Job Quests", st: "ready-on-another-job", n: "Carpe Diem", lv: 60, job: "FSH", lab: "Ready on FSH", det: "", xp: "HW" },

    { g: "Feature Quests", st: "ready", n: "It Could Happen to You", lv: 15, job: "All", lab: "Ready", det: "Lv 15 · well-heeled youth", xp: "ARR", tip: true },
    { g: "Feature Quests", st: "ready", n: "Towards the Firmament", lv: 60, job: "All", lab: "Ready", det: "Lv 60 · recruitment notice", xp: "HW" },
    { g: "Feature Quests", st: "completed", n: "Litany of Peace", lv: 60, job: "All", lab: "Completed", det: "2025-04-02", xp: "HW" },
    { g: "Feature Quests", st: "done-this-cycle", n: "Seeking Inspiration", lv: 60, job: "All", lab: "Done this week", det: "resets in 3 d", xp: "HW" },
    { g: "Feature Quests", st: "completed", n: "An Academic Dispute", lv: 80, job: "All", lab: "Completed", det: "2025-05-11", xp: "ShB" },
    { g: "Feature Quests", st: "completed", n: "Tails, You Lose", lv: 80, job: "All", lab: "Completed", det: "2025-05-12", xp: "ShB" },
    { g: "Feature Quests", st: "locked-out", n: "Heads, I Win", lv: 80, job: "All", lab: "Locked out", det: "Another choice (Tails, You Lose)", xp: "ShB" },
    { g: "Feature Quests", st: "not-checked", n: "Faerie Tale", lv: 91, job: "All", lab: "Not checked", det: "accept condition", xp: "DT" },

    { g: "Sidequests", st: "completed", n: "My Feisty Little Chocobo", lv: 30, job: "All", lab: "Completed", det: "2024-10-19", xp: "ARR" },
    { g: "Sidequests", st: "in-journal", n: "Small Business, Big Dreams", lv: 90, job: "All", lab: "In journal", det: "step 2 of 5", xp: "EW" },
    { g: "Sidequests", st: "not-checked", n: "Bird in Hand", lv: 30, job: "All", lab: "Not checked", det: "house", xp: "ARR" },
    { g: "Allied Society Quests", st: "done-this-cycle", n: "In the Shadow of the Moon", lv: 46, job: "All", lab: "Done today", det: "resets in 3 h", xp: "ARR" },
    { g: "Allied Society Quests", st: "ready", n: "Peace for Thanalan", lv: 41, job: "All", lab: "Ready", det: "Lv 41 · Hamujj Gah", xp: "ARR" }
  ];
  // The selected quest's pane, as owner-quest-pane.png shows it.
  var D = { n: "Blue Collar Work", st: "blocked", lv: 10, xp: "ARR", path: "Class & Job Quests › Blue Mage", det: "needs BLU level 10", art: "night", pos: "50% 42%",
    req: [["lock", "Class or job", "not available on the current job"], ["lock", "Level", "needs level 10, BLU is 1"], ["ok", "Previous quests", "Blue Leading the Blue done"]] };

  var TREE = [
    ["", "All quests", "3,318 / 5,084", 0.65, "41"],
    ["sec", "Main Scenario", "1,021 / 1,038", 0.98, "1"],
    ["sec", "Chronicles of a New Era", "162 / 239", 0.68, ""],
    ["sec", "Allied Society Quests", "401 / 732", 0.55, "6"],
    ["sec", "Class & Job Quests", "711 / 1,104", 0.64, "12"],
    ["sec", "Sidequests", "1,003 / 1,794", 0.56, "19"],
    ["sec", "Other Quests", "20 / 177", 0.11, "3"],
    ["hair"],
    ["sec", "Unlock quests", "196 / 241", 0.81, "4"],
    ["", "Removed from the game", "28 / 31", 0.9, ""]
  ];

  var XP = { ARR: "ARR", HW: "HW", SB: "StB", ShB: "ShB", EW: "EW", DT: "DT" };
  function filterQ(F) {
    return Q.filter(function (q) {
      if (F.hide && (q.st === "completed" || q.st === "locked-out")) return false;
      if (F.states.indexOf(q.st) < 0) return false;
      if (F.exp.length && F.exp.indexOf(XP[q.xp]) < 0) return false;
      return true;
    });
  }
  function chipsOf(F) {
    var c = [];
    if (F.hide) c.push("Hide completed");
    if (F.avail) c.push("Available now");
    if (F.states.length < 8) c.push(F.states.length + " states");
    if (F.exp.length) c.push(F.exp.join(", "));
    F.more.forEach(function (m) { c.push(m); });
    return c;
  }
  var ROWS = Q;
  function esc(s) { return String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;"); }
  var uid = 0;
  function rng(seed) { var s = seed >>> 0; return function () { s = (Math.imul(s, 1664525) + 1013904223) >>> 0; return s / 4294967296; }; }

  // ---------- Glyphs (v13, plus the v7 Completed moon) ----------
  var V7 = true;
  function heroFile(st) { return st === "ready-on-another-job" ? "ready-on-another-job-paladin" : st; }
  // v7 Completed: the 96 and 128 px atlas tiers carry the rim-lit craters; 48, 64 and the row tier use the crater-less source.
  var THEME = "medallion", SNOW = false, SELN = null;
  var TDIR = { glass: "../themes/ishgard-glass/", aether: "../themes/aether-crystal/", orrery: "../themes/astrologian-orrery/" };
  function medalSrc(st, hero, px) {
    if (TDIR[THEME]) return TDIR[THEME] + (hero ? heroFile(st) : "_row/" + st) + ".svg";
    return V7 && st === "completed" ? (px >= 96 ? MOON7 : MOON7S) : M + (hero ? heroFile(st) : "_row/" + st) + ".svg";
  }
  function medalImg(st, px, hero) { return '<img alt="" width="' + px + '" height="' + px + '" src="' + medalSrc(st, hero, px) + '">'; }
  function quietMedal(st, px, hero) {
    var id = "qm" + (++uid), href = medalSrc(st, hero, px);
    var clip = '<circle cx="64" cy="64" r="53.2"/>' + (hero ? '<circle cx="95" cy="95" r="20.5"/>' : "") + (hero && st === "in-journal" ? '<rect x="24.5" y="1.5" width="17.5" height="75"/>' : "");
    return '<svg width="' + px + '" height="' + px + '" viewBox="0 0 128 128" aria-hidden="true"><defs><clipPath id="' + id + '">' + clip + '</clipPath></defs>' +
      '<circle cx="64" cy="64" r="55.6" fill="#0E1322"/>' +
      '<circle cx="64" cy="64" r="55.6" fill="none" stroke="' + (SNOW ? "#7A859C" : "#C3CBDF") + '" stroke-opacity="' + (SNOW ? ".8" : ".62") + '" stroke-width="' + (hero ? 1.25 : 1) + '" vector-effect="non-scaling-stroke"/>' +
      (hero ? '<circle cx="95" cy="95" r="23.4" fill="#182033"/>' : "") +
      '<image href="' + href + '" width="128" height="128" clip-path="url(#' + id + ')"/>' +
      (hero ? '<circle cx="95" cy="95" r="21.6" fill="none" stroke="#C3CBDF" stroke-opacity=".62" stroke-width="1" vector-effect="non-scaling-stroke"/>' : "") + "</svg>";
  }
  function road() {
    var o = '<svg class="road" viewBox="0 0 380 34" preserveAspectRatio="none" aria-hidden="true">', cx = 356;
    [[4, 3, .07], [9, 5, .09], [14, 8, .11], [19, 12, .13], [24, 17, .15], [29, 23, .17]].forEach(function (d, k) {
      var w = d[1] * 1.5;
      o += '<rect x="' + (cx - w / 2 + (k % 2 ? 2 : -2)) + '" y="' + d[0] + '" width="' + w + '" height="1.2" rx=".6" fill="#FFF0BE" opacity="' + d[2] + '"/>';
    });
    return o + "</svg>";
  }
  var GLY = { night: "#1C2752", lapis: "#5480C8", spec: "#F4F2EA", high: "#E2E8F4", stone: "#C3CEE4", mid: "#95A5C8", deep: "#5E6E97",
    tide: "#6F8FD0", gilt: "#C9A65C", red: "#C24A58", crack: "#0B0408", mist: "#A9B2CC" };
  function flatGlyph(st, px) {
    var o = '<svg width="' + px + '" height="' + px + '" viewBox="0 0 16 16" aria-hidden="true">';
    function disc(fill, rim) { return '<circle cx="8" cy="8" r="6.9" fill="' + fill + '"/><circle cx="8" cy="8" r="7.1" fill="none" stroke="' + rim + '" stroke-opacity=".6" stroke-width="1" vector-effect="non-scaling-stroke"/>'; }
    function cres(lit, sky, big) {
      var cx = 7.5, cy = 7.3, r = big ? 5.4 : 4.6, rx = (0.18 * r).toFixed(3);
      return '<path d="M' + cx + " " + (cy - r) + "A" + r + " " + r + " 0 0 1 " + cx + " " + (cy + r) + "A" + rx + " " + r + " 0 0 0 " + cx + " " + (cy - r) + 'Z" transform="rotate(28 ' + cx + " " + cy + ')" fill="' + lit + '"/>';
    }
    switch (st) {
      case "ready": o += disc(GLY.lapis, "#F2D27A") + cres(GLY.spec, GLY.lapis, true); break;
      case "ready-on-another-job": o += disc(GLY.night, "#DDE3F0") + cres(GLY.high, GLY.night); break;
      case "in-journal": o += disc(GLY.night, "#F2D27A") + cres(GLY.stone, GLY.night) + '<path d="M3.4 3.2h2.1v4.2l-1.05-.85-1.05.85z" fill="' + GLY.tide + '"/>'; break;
      case "completed": o += disc(GLY.night, "#D6B25A") + '<circle cx="8" cy="8" r="3.7" fill="' + GLY.mid + '"/><path d="M6 8.1l1.4 1.4 2.7-3" fill="none" stroke="' + GLY.gilt + '" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>'; break;
      case "done-this-cycle": o += disc(GLY.night, "#DDE3F0") + '<path d="M8 4.6a3.4 3.4 0 0 0 0 6.8z" fill="' + GLY.mid + '"/><path d="M8.6 2.5a5.5 5.5 0 0 1 0 11" fill="none" stroke="' + GLY.gilt + '" stroke-width="1" stroke-linecap="round" vector-effect="non-scaling-stroke"/>'; break;
      case "blocked": o += disc(GLY.deep, "#7C86A8") + '<path d="M1.6 9.6c.3-1.5 1.7-2.4 3-2.1.7-1.4 2.7-2 4.1-1 1.5-.5 3.3.4 3.7 2.2l2.2.9v4.6H1.4z" fill="#3A4260"/>'; break;
      case "locked-out": o += disc(GLY.red, "#B25C7F") + '<path d="M6.1 5.9l2.5 1.2 1.6-.5 3.6 2.7M6.1 5.9l.8 2.7-1 2.1 1.5 3.8" fill="none" stroke="' + GLY.crack + '" stroke-width="1" stroke-linejoin="round" vector-effect="non-scaling-stroke"/>'; break;
      default: o += disc("#151A28", "#8A93B0") + '<path d="M6 6.2a2 2 0 1 1 2.8 1.85c-.55.25-.8.65-.8 1.2v.55" fill="none" stroke="' + GLY.mist + '" stroke-width="1.35" stroke-linecap="round" vector-effect="non-scaling-stroke"/><circle cx="8" cy="11.6" r="1.05" fill="' + GLY.mist + '"/>'; break;
    }
    return o + "</svg>";
  }
  var STATES = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"];
  function rowGlyph(lv, st, px) { return lv === "full" ? medalImg(st, px || 18) : lv === "quiet" ? quietMedal(st, px ? px - 2 : 16) : flatGlyph(st, px ? 12 : 12); }

  function orbit(f, lv) {
    var o0 = orbit0(f, lv);
    return SNOW ? orbitLight(f, lv) : o0;
  }
  // 1.16, light palettes (supervisor): the gauge gets its own ink so it holds 3:1 as a UI graphic: a gilt arc ramping
  // #8A6A1C (highlight, upper left) to #755308 (shade), a #CAD2DF groove between #7A859C keylines, and the filling moon
  // in moonstone over a #59627A dark side.
  function orbitLight(f, lv) {
    var r = 9.5, c = 2 * Math.PI * r, a = -Math.PI / 2 + 2 * Math.PI * f, id = "og" + (++uid);
    var groove = '<circle cx="12" cy="12" r="' + r + '" fill="none" stroke="#CAD2DF" stroke-width="2"/>' +
      '<circle cx="12" cy="12" r="' + (r + 1.25) + '" fill="none" stroke="#7A859C" stroke-width=".5"/><circle cx="12" cy="12" r="' + (r - 1.25) + '" fill="none" stroke="#7A859C" stroke-width=".5"/>';
    var arc = '<defs><linearGradient id="' + id + '" x1="2" y1="2" x2="22" y2="22" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#8A6A1C"/><stop offset="1" stop-color="#755308"/></linearGradient></defs>' +
      '<circle cx="12" cy="12" r="' + r + '" fill="none" stroke="url(#' + id + ')" stroke-width="2"' + (lv === "quiet" ? "" : ' stroke-linecap="round"') + ' stroke-dasharray="' + (c * f).toFixed(2) + " " + c.toFixed(2) + '" transform="rotate(-90 12 12)"/>';
    if (lv === "quiet") return '<svg class="orb" viewBox="0 0 24 24" aria-hidden="true">' + arc + groove.replace("<circle", "<circle") + arc.replace(/<defs>.*<\/defs>/, "") + "</svg>";
    return '<svg class="orb" viewBox="0 0 24 24" aria-hidden="true">' + arc + groove + arc.replace(/<defs>.*<\/defs>/, "") +
      '<circle cx="' + (12 + r * Math.cos(a)).toFixed(2) + '" cy="' + (12 + r * Math.sin(a)).toFixed(2) + '" r="2.1" fill="#8A6A1C" stroke="#F9FAFC" stroke-width="1"/>' +
      '<circle cx="12" cy="12" r="5.2" fill="#59627A"/><path d="M12 6.8a5.2 5.2 0 0 1 0 10.4a' + (5.2 * (1 - 2 * Math.min(f, 1))).toFixed(2) + ' 5.2 0 0 ' + (f > 0.5 ? 1 : 0) + ' 0-10.4z" fill="#C3CEE4"/></svg>';
  }
  function orbit0(f, lv) {
    var r = 9.5, c = 2 * Math.PI * r, a = -Math.PI / 2 + 2 * Math.PI * f;
    if (lv === "quiet") {
      return '<svg class="orb" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="' + r + '" fill="none" stroke="#2A3149" stroke-width="2"/>' +
        '<circle cx="12" cy="12" r="' + r + '" fill="none" stroke="' + (f >= 1 ? "#B8933F" : "#CDB57A") + '" stroke-width="2" stroke-dasharray="' + (c * f).toFixed(2) + " " + c.toFixed(2) + '" transform="rotate(-90 12 12)"/></svg>';
    }
    return '<svg class="orb" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="' + r + '" fill="none" stroke="#262C46" stroke-width="2"/>' +
      '<circle cx="12" cy="12" r="' + r + '" fill="none" stroke="#F2D27A" stroke-width="2" stroke-linecap="round" stroke-dasharray="' + (c * f).toFixed(2) + " " + c.toFixed(2) + '" transform="rotate(-90 12 12)"/>' +
      '<circle cx="' + (12 + r * Math.cos(a)).toFixed(2) + '" cy="' + (12 + r * Math.sin(a)).toFixed(2) + '" r="2.1" fill="#FFF0BE" stroke="#0F1424" stroke-width="1"/>' +
      '<circle cx="12" cy="12" r="5.2" fill="#2C334A"/><path d="M12 6.8a5.2 5.2 0 0 1 0 10.4a' + (5.2 * (1 - 2 * Math.min(f, 1))).toFixed(2) + ' 5.2 0 0 ' + (f > 0.5 ? 1 : 0) + ' 0-10.4z" fill="#DCD6C2" opacity=".9"/></svg>';
  }

  // ---------- The sky ----------
  // 1.13: one layer, three magnitudes, two temperatures (StarField as shipped).
  function stars13(seed, w, h, n, top, y0, x0, x1) {
    y0 = y0 || 0; x0 = x0 || 0; x1 = x1 == null ? 1 : x1;
    var s = seed, out = '<svg class="sky" viewBox="0 0 ' + w + " " + h + '" preserveAspectRatio="xMidYMin slice" aria-hidden="true">';
    function rnd() { s = (s * 1664525 + 1013904223) % 4294967296; return s / 4294967296; }
    for (var i = 0; i < n; i++) {
      var x = (x0 + rnd() * (x1 - x0)) * w, y = (y0 + Math.pow(rnd(), 1.25) * (top - y0)) * h, m = rnd(), warm = rnd() > 0.82 ? "#FFF0BE" : "#DDE6FF";
      if (m < 0.70) out += '<rect x="' + x.toFixed(1) + '" y="' + y.toFixed(1) + '" width="1" height="1" fill="' + warm + '" opacity=".16"/>';
      else if (m < 0.95) out += '<circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="1" fill="' + warm + '" opacity=".26"/>';
      else out += '<g opacity=".36" stroke="' + warm + '" stroke-width=".7"><path d="M' + (x - 3.5).toFixed(1) + " " + y.toFixed(1) + "h7M" + x.toFixed(1) + " " + (y - 3.5).toFixed(1) + 'v7"/></g><circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="1.5" fill="' + warm + '" opacity=".5"/>';
    }
    return out + "</svg>";
  }

  // v7: three depths (far 60 %, mid 32 %, near 8 %), four temperatures, a slow twinkle on mid and near stars, an optional
  // Milky Way band and an optional region constellation. Everything stays inside the given sky rects (never under text).
  var TEMP = [[0.64, "#DCE5FF"], [0.88, "#F4F2EA"], [0.97, "#FFE2A8"], [1.01, "#FFC9AE"]];
  // 1.17, Kugane Lacquer (supervisor's accepted option): far stars warm slightly, a hazier sky over a lantern-lit port
  var PALNOW = null;
  function tempOf(r, layer) { if (layer === 0) return PALNOW === "kugane" ? (r < 0.6 ? "#E9E2DA" : "#F1E3CC") : r < 0.8 ? TEMP[0][1] : TEMP[1][1]; for (var i = 0; i < TEMP.length; i++) if (r < TEMP[i][0]) return TEMP[i][1]; return TEMP[0][1]; }
  var FIG = {
    arr: { name: "The Chocobo", exp: "A Realm Reborn", p: [[.2, .12], [0, .22], [.3, .46], [.66, .38], [1, .18], [.6, .68], [.46, 1], [.74, 1]], e: [[1, 0], [0, 2], [2, 3], [3, 4], [2, 5], [5, 3], [5, 6], [5, 7]] },
    hw: { name: "The Wyrm", exp: "Heavensward", p: [[0, .78], [.2, .5], [.42, .56], [.6, .3], [.82, .36], [1, .06], [.66, .66]], e: [[0, 1], [1, 2], [2, 3], [3, 4], [4, 5], [3, 6]] },
    sb: { name: "The Lotus", exp: "Stormblood", p: [[.5, .08], [.12, .38], [.88, .38], [.28, .9], [.72, .9], [.5, .55]], e: [[5, 0], [5, 1], [5, 2], [5, 3], [5, 4]] },
    shb: { name: "The Tower", exp: "Shadowbringers", p: [[.5, 0], [.4, .3], [.6, .3], [.4, .66], [.6, .66], [.36, 1], [.64, 1]], e: [[0, 1], [0, 2], [1, 3], [2, 4], [3, 5], [4, 6]] },
    ew: { name: "The Crescent", exp: "Endwalker", p: [[.72, .02], [.36, .14], [.12, .42], [.14, .7], [.4, .94], [.74, .98]], e: [[0, 1], [1, 2], [2, 3], [3, 4], [4, 5]] },
    dt: { name: "The Plume", exp: "Dawntrail", p: [[.1, 1], [.34, .7], [.58, .38], [.86, .04], [.62, .78], [.86, .5], [.3, .34]], e: [[0, 1], [1, 2], [2, 3], [1, 4], [2, 5], [2, 6]] }
  };
  function figure(key, x, y, size, o) {
    o = o || {};
    var f = FIG[key], s = "", pts = f.p.map(function (p) { return [x + p[0] * size, y + p[1] * size]; });
    f.e.forEach(function (e) { s += '<line x1="' + pts[e[0]][0].toFixed(1) + '" y1="' + pts[e[0]][1].toFixed(1) + '" x2="' + pts[e[1]][0].toFixed(1) + '" y2="' + pts[e[1]][1].toFixed(1) + '" stroke="#DCE5FF" stroke-opacity="' + (o.line || 0.11) + '" stroke-width=".8" stroke-linecap="round"/>'; });
    pts.forEach(function (p, k) {
      var big = k === 0;
      s += '<circle cx="' + p[0].toFixed(1) + '" cy="' + p[1].toFixed(1) + '" r="' + (big ? 3.4 : 2.6) + '" fill="#DCE5FF" opacity=".07"/>';
      s += '<circle cx="' + p[0].toFixed(1) + '" cy="' + p[1].toFixed(1) + '" r="' + (big ? 1.45 : 1.15) + '" fill="#EEF1FA" opacity="' + (big ? 0.6 : 0.48) + '"/>';
    });
    return s;
  }
  function sky7(o) {
    // o: { seed, w, h, n, rects: [[x,y,w,h],...], band: [x0,y0,x1,y1,width] | null, fig: [key,x,y,size] | null, anim, ox, oy }
    var r = rng(o.seed), id = "sk" + (++uid), w = o.w, h = o.h, out = "";
    if (PALNOW === "kugane") o = Object.assign({}, o, { n: Math.max(4, Math.round(o.n / 2)) }); // half the density
    out += '<svg class="sky" width="' + w + '" height="' + h + '" viewBox="0 0 ' + w + " " + h + '" aria-hidden="true" style="width:' + w + "px;height:" + h + 'px">';
    out += '<defs><clipPath id="' + id + 'c">' + o.rects.map(function (q) { return '<rect x="' + q[0] + '" y="' + q[1] + '" width="' + q[2] + '" height="' + q[3] + '"/>'; }).join("") + "</clipPath>" +
      '<filter id="' + id + 'b" x="-50%" y="-50%" width="200%" height="200%"><feGaussianBlur stdDeviation="9"/></filter></defs>';
    var g = '<g clip-path="url(#' + id + 'c)">';
    // Revision 3 drift: the whole field moves left as one sky (no per-layer speeds), wrapping on a tile as wide as the
    // sky's extent; each rect fades its stars over 10 px at the left and right edges so nothing pops in or out.
    var DW = 0;
    if (o.drift) {
      o.rects.forEach(function (q) { DW = Math.max(DW, q[0] + q[2]); });
      out = out.replace("</defs>", '<linearGradient id="' + id + 'g" x1="0" x2="1" y1="0" y2="0"><stop offset="0" stop-color="#fff" stop-opacity="0"/><stop offset="' + (10 / DW).toFixed(3) + '" stop-color="#fff"/><stop offset="' + (1 - 10 / DW).toFixed(3) + '" stop-color="#fff"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>' +
        '<mask id="' + id + 'm">' + o.rects.map(function (q) { return '<rect x="' + q[0] + '" y="' + q[1] + '" width="' + q[2] + '" height="' + q[3] + '" fill="url(#' + id + 'g)"/>'; }).join("") + "</mask></defs>");
      g = '<g clip-path="url(#' + id + 'c)" mask="url(#' + id + 'm)"><g class="drift" data-w="' + DW + '" style="--dw:-' + DW + "px;animation-duration:" + (DW / DRIFT_PX_PER_MIN * 60).toFixed(0) + 's">';
    }
    var g0 = g.length;
    var band = o.band;
    if (band) {
      var bx = band[2] - band[0], by = band[3] - band[1], len = Math.sqrt(bx * bx + by * by), ang = Math.atan2(by, bx) * 180 / Math.PI;
      g += '<g filter="url(#' + id + 'b)">';
      for (var k = 0; k < 7; k++) {
        var t = k / 6, cx = band[0] + bx * t, cy = band[1] + by * t;
        g += '<ellipse cx="' + cx.toFixed(1) + '" cy="' + cy.toFixed(1) + '" rx="' + (len / 7).toFixed(1) + '" ry="' + (band[4] * (0.34 + 0.18 * Math.sin(k * 1.7))).toFixed(1) + '" transform="rotate(' + ang.toFixed(1) + " " + cx.toFixed(1) + " " + cy.toFixed(1) + ')" fill="#C9D3F0" opacity="' + (0.045 + 0.02 * (k % 2)).toFixed(3) + '"/>';
      }
      g += "</g>";
    }
    function inRects(x, y, pad) { for (var i = 0; i < o.rects.length; i++) { var q = o.rects[i]; if (x >= q[0] + pad && x <= q[0] + q[2] - pad && y >= q[1] + pad && y <= q[1] + q[3] - pad) return true; } return false; }
    var placed = 0, tries = 0, n = o.n;
    var extra = band ? Math.round(n * 0.4) : 0;
    while (placed < n + extra && tries < (n + extra) * 30) {
      tries++;
      var x, y;
      if (placed >= n && band) { var t2 = r(), off = (r() + r() + r() - 1.5) * band[4] * 0.45; x = band[0] + (band[2] - band[0]) * t2 - Math.sin(Math.atan2(band[3] - band[1], band[2] - band[0])) * off; y = band[1] + (band[3] - band[1]) * t2 + Math.cos(Math.atan2(band[3] - band[1], band[2] - band[0])) * off; }
      else { var q = o.rects[Math.floor(r() * o.rects.length)]; x = (o.drift ? r() * DW : q[0] + r() * q[2]); y = q[1] + r() * q[3]; }
      if (o.drift ? !inRects(q ? q[0] + 5 : x, y, 4) : !inRects(x, y, 4)) continue;
      if (o.fig) { var f = o.fig; if (x > f[1] - 10 && x < f[1] + f[3] + 10 && y > f[2] - 10 && y < f[2] + f[3] + 10) continue; }
      placed++;
      var m = placed > n ? r() * 0.7 : r(), layer = m < 0.60 ? 0 : m < 0.92 ? 1 : 2, col = tempOf(r(), layer);
      var tw = o.anim && (layer === 2 || (layer === 1 && r() < 0.34));
      var dur = (7 + r() * 6).toFixed(1), beg = (-r() * 12).toFixed(1);
      var anim = function (a) { return tw ? '<animate attributeName="opacity" values="' + a.toFixed(3) + ";" + (a * (layer === 2 ? 1.28 : 1.18)).toFixed(3) + ";" + (a * (layer === 2 ? 0.72 : 0.82)).toFixed(3) + ";" + a.toFixed(3) + '" keyTimes="0;.3;.7;1" calcMode="spline" keySplines=".45 0 .55 1;.45 0 .55 1;.45 0 .55 1" dur="' + dur + 's" begin="' + beg + 's" repeatCount="indefinite"/>' : ""; };
      var X = x.toFixed(1), Y = y.toFixed(1);
      if (layer === 0) { var a0 = 0.10 + r() * 0.06; g += '<rect x="' + (x - 0.5).toFixed(1) + '" y="' + (y - 0.5).toFixed(1) + '" width="1" height="1" fill="' + col + '" opacity="' + a0.toFixed(3) + '"/>'; }
      else if (layer === 1) { var a1 = 0.20 + r() * 0.08; g += '<circle cx="' + X + '" cy="' + Y + '" r=".95" fill="' + col + '" opacity="' + a1.toFixed(3) + '">' + anim(a1) + "</circle>"; }
      else {
        var a2 = 0.52 + r() * 0.08;
        g += '<g opacity="' + a2.toFixed(3) + '">' + anim(a2) + '<circle cx="' + X + '" cy="' + Y + '" r="3.4" fill="' + col + '" opacity=".13"/><path d="M' + (x - 2.5).toFixed(1) + " " + Y + "h5M" + X + " " + (y - 2.5).toFixed(1) + 'v5" stroke="' + col + '" stroke-width=".6" opacity=".4"/><circle cx="' + X + '" cy="' + Y + '" r="1.35" fill="' + col + '"/></g>';
      }
    }
    if (o.fig) g += figure(o.fig[0], o.fig[1], o.fig[2], o.fig[3]);
    if (o.drift) { var field = g.slice(g0); return out + g + '<g transform="translate(' + DW + ' 0)">' + field + "</g></g></g></svg>"; }
    return out + g + "</g></svg>";
  }
  // The completion meteor (a moment: once, 0.7 s, MomentPeak .6, Full and Reduce motion off only).
  var DRIFT_PX_PER_MIN = 6;
  function meteor(x, y) { return '<div class="shooting" style="left:' + x + "px;top:" + y + 'px"><i></i></div>'; }

  var ICON = {
    gear: '<svg width="11" height="11" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6"><circle cx="8" cy="8" r="2.4"/><path d="M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.4 1.4M11.2 11.2l1.4 1.4M3.4 12.6l1.4-1.4M11.2 4.8l1.4-1.4"/></svg>',
    x: '<svg width="10" height="10" viewBox="0 0 16 16" stroke="currentColor" stroke-width="2"><path d="M3 3l10 10M13 3 3 13"/></svg>',
    search: '<svg width="12" height="12" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="2"><circle cx="7" cy="7" r="4.5"/><path d="M10.5 10.5 14 14"/></svg>',
    filt: '<svg width="13" height="13" viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.7"><path d="M2 4h12M2 8h12M2 12h12"/><circle cx="5" cy="4" r="1.6" fill="#151C33"/><circle cx="11" cy="8" r="1.6" fill="#151C33"/><circle cx="7" cy="12" r="1.6" fill="#151C33"/></svg>',
    book: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 4.5h5.2c.9 0 1.8.5 1.8 1.4 0-.9.9-1.4 1.8-1.4H17v11h-5.2c-.9 0-1.8.5-1.8 1.2 0-.7-.9-1.2-1.8-1.2H3z"/><path d="M10 6v10.5"/></svg>',
    moon: '<svg viewBox="0 0 20 20" fill="currentColor"><path d="M12.6 2.6A7.6 7.6 0 1 0 17.4 13 6.1 6.1 0 0 1 12.6 2.6z"/></svg>',
    chars: '<svg viewBox="0 0 20 20" fill="currentColor"><circle cx="7.5" cy="6.5" r="3"/><circle cx="14" cy="7.5" r="2.4" opacity=".7"/><path d="M2 16.5c0-3.1 2.4-5 5.5-5s5.5 1.9 5.5 5z"/><path d="M13.5 16.5c0-2-.8-3.5-2.1-4.3 3.1-.7 6.6.9 6.6 4.3z" opacity=".7"/></svg>',
    flight: '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M3 12c3-1 5.5-3.6 7-8 1 3.3 3.4 6 7 7.2"/><path d="M5 15.5c2.6-.6 5-1.8 6.7-4M8.5 17.5c2.2-.4 4.4-1.4 6-3.2"/></svg>',
    blues: '<svg viewBox="0 0 20 20"><path d="M10 2.2 16.6 6v8L10 17.8 3.4 14V6z" fill="#3E6FD8" stroke="#9CC0FF" stroke-width="1"/><path d="M10 6v5.2" stroke="#fff" stroke-width="2" stroke-linecap="round"/><circle cx="10" cy="13.8" r="1.1" fill="#fff"/></svg>',
    todo: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"><path d="M6 4h8M6 8h8M6 12h8"/><circle cx="2.6" cy="4" r=".9" fill="currentColor"/><circle cx="2.6" cy="8" r=".9" fill="currentColor"/><circle cx="2.6" cy="12" r=".9" fill="currentColor"/></svg>',
    pinmap: '<svg viewBox="0 0 16 16" fill="currentColor"><path d="M8 1a5 5 0 0 0-5 5c0 3.7 5 9 5 9s5-5.3 5-9a5 5 0 0 0-5-5zm0 7a2 2 0 1 1 0-4 2 2 0 0 1 0 4z"/></svg>',
    help: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M6 6.2a2 2 0 1 1 2.8 1.8c-.6.3-.8.7-.8 1.3"/><circle cx="8" cy="12" r=".7" fill="currentColor"/></svg>',
    set: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="8" cy="8" r="2.3"/><path d="M8 1.5v2M8 12.5v2M1.5 8h2M12.5 8h2M3.4 3.4l1.4 1.4M11.2 11.2l1.4 1.4M3.4 12.6l1.4-1.4M11.2 4.8l1.4-1.4"/></svg>',
    arrow: '<svg viewBox="0 0 16 16" fill="currentColor"><path d="M14.2 1.8 1.8 7.3l5.6 1.3 1.3 5.6z"/></svg>',
    plane: '<svg viewBox="0 0 16 16" fill="currentColor"><path d="M14.6 1.4 1.4 7.2l3.9 1.6L12.2 3.6 6.6 9.9v4.4l2.5-3 3.2 1.9z"/></svg>',
    walk: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="9.3" cy="2.4" r="1.5"/><path d="M8.6 4.8 6.6 6.2 5.4 8.6M8.6 4.8l-.9 4.4 2.6 2.2.8 3.4M7.7 9.2 6.4 11.8 4.4 14.6M8.6 4.8l1.4 2.6 2.4 1"/></svg>',
    pin: '<svg viewBox="0 0 16 16" fill="currentColor"><path d="M5.2 1.5h5.6l-.9 1.2v3.6l2.1 2.2v1.2H8.7V14l-.7 1-.7-1V9.7H4V8.5l2.1-2.2V2.7z"/></svg>',
    path: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><circle cx="3.8" cy="3.6" r="1.8"/><circle cx="12.2" cy="12.4" r="1.8"/><path d="M5.6 3.6h4.2a2.4 2.4 0 0 1 0 4.8H6.2a2.4 2.4 0 0 0 0 4.8h4.2"/></svg>',
    flag: '<svg viewBox="0 0 16 16" fill="currentColor"><path d="M3 1v14h1.5V9.5H13l-2-3.2 2-3.3H4.5V1z"/></svg>',
    link: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"><path d="M6.5 9.5 9.5 6.5M7 4.5 8.5 3a2.5 2.5 0 1 1 3.5 3.5L10.5 8M9 11.5 7.5 13a2.5 2.5 0 1 1-3.5-3.5L5.5 8"/></svg>',
    copy: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round"><path d="M5.5 5.5h8v8h-8zM10.5 5.5V3.5a1 1 0 0 0-1-1h-6a1 1 0 0 0-1 1v6a1 1 0 0 0 1 1h2"/></svg>',
    more: '<svg viewBox="0 0 16 16" fill="currentColor"><circle cx="3" cy="8" r="1.4"/><circle cx="8" cy="8" r="1.4"/><circle cx="13" cy="8" r="1.4"/></svg>',
    sig: '<svg class="sig" viewBox="0 0 10 10" aria-hidden="true"><path d="M5 0.5 6 4 9.5 5 6 6 5 9.5 4 6 .5 5 4 4z" fill="#D9BE82"/></svg>',
    reset: '<svg viewBox="0 0 16 16" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round"><path d="M3.2 7.4A5 5 0 1 1 4.6 11.6"/><path d="M2.6 3.6v3.9h3.9"/></svg>',
    xmark: '<svg viewBox="0 0 12 12"><path d="M2.5 2.5l7 7M9.5 2.5l-7 7" stroke="#D68AA8" stroke-width="2" stroke-linecap="round"/></svg>',
    check: '<svg viewBox="0 0 12 12"><path d="M2 6.5l2.6 2.6L10 3.4" stroke="#C9A65C" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>'
  };
  var CREST = '<svg class="crest" viewBox="0 0 44 40" aria-hidden="true"><defs><radialGradient id="crg" cx="40%" cy="35%" r="70%"><stop offset="0" stop-color="#FFF6D8"/><stop offset=".6" stop-color="#E9E4D2"/><stop offset="1" stop-color="#BFC6DA"/></radialGradient></defs>' +
    '<circle cx="22" cy="14" r="11" fill="#F2D27A" opacity=".08"/><path d="M26.5 5.2a9 9 0 1 0 2.9 15.6A7.2 7.2 0 0 1 26.5 5.2z" fill="url(#crg)"/>' +
    '<path d="M6 29h32" stroke="#A88B52" stroke-width=".8" opacity=".7"/><path d="M19 31.5h6M17.5 34h9M20 36.5h4" stroke="#E9E4D2" stroke-width="1.1" stroke-linecap="round" opacity=".55"/></svg>';

  // ---------- Window ----------
  function frame(lv, o) {
    var open = !!o.drawer, chips = o.v7 ? chipsOf(o.F || F_WIN) : [], cnt = chips.length;
    return '<div class="w-title"><span class="tri"></span>Tsukimichi<span class="wc">' + ICON.gear + ICON.x + '</span></div>' +
      '<div class="w-tool"><div class="pill-in">' + ICON.search + '<span>Search quests, rewards or ids</span></div>' +
      '<div class="qv"><span class="on">All</span><span>Unlocks</span><span>My level</span><span>Stalled</span><span>Story sidequests</span></div>' +
      '<div class="tbtn' + (open ? " act" : "") + '" data-act="filters">' + ICON.filt + 'Filters' + (cnt ? '<span class="fbg">' + cnt + "</span>" : "") + '</div>' +
      '<div class="chr"><span class="jb">WHM</span><span>Michiru Tsukikage</span><span style="color:var(--dusk)">Balmung</span><span class="pip"></span></div></div>' +
      '<div class="w-chips"><span class="chip">Pinned <i>✕</i></span>' + chips.map(function (c) { return '<span class="chip set">' + esc(c) + " <i>✕</i></span>"; }).join("") + '<span class="cnt">' + ROWS.length + " of " + Q.length + "</span></div>";
  }

  // 1.13's rail: the brass thread through the stations, the lit bar and bead above the active one.
  function rail13(lv) {
    var st = [["Journal", ICON.book, "41"], ["Moonlit", ICON.moon], ["Characters", ICON.chars], ["Flight", ICON.flight], ["My blues", ICON.blues]];
    var h = '<nav class="rail">' + (lv === "full" ? stars13(7, 64, 900, 30, .8) : "") + '<div class="lay" style="display:flex;flex-direction:column;align-items:center;width:100%;flex:1">';
    h += CREST + '<div class="rule"></div>';
    if (lv === "full") {
      // As TabStrip.DrawThread / DrawLit draw it: a brass hairline down the rail's middle, broken around each station's
      // icon and label, and on the active station a 22 px gold bar ending just above its icon with the moon bead on top.
      var top0 = 61, sh = 54, iconTop = 8.5, labBot = 45.5;
      for (var k = 0; k < 4; k++) { var a = top0 + sh * k + labBot + 3, b = top0 + sh * (k + 1) + iconTop - 3; h += '<i class="th13" style="top:' + a + "px;height:" + (b - a) + 'px"></i>'; }
      h += '<i class="lit13" style="top:' + (top0 + iconTop - 3 - 14) + 'px;height:14px"></i><i class="bead13" style="top:' + (top0 + iconTop - 3 - 14 - 3) + 'px"></i>';
    }
    st.forEach(function (s, k) { h += '<div class="st' + (k === 0 ? " on" : "") + '">' + (s[2] ? '<span class="bd">' + s[2] + "</span>" : "") + s[1] + '<span class="lb">' + s[0] + "</span></div>"; });
    h += foot(lv);
    return h + "</div></nav>";
  }
  function foot(lv) {
    return '<div class="foot">' + (lv === "plain" ? "" : orbit(0.65, lv).replace('class="orb"', 'class="orb" style="width:30px;height:30px"') + '<span class="pc">65%</span>') +
      '<div class="rb on">' + ICON.todo + '</div><div class="rb">' + ICON.pinmap + '</div><div class="rb">' + ICON.help + '</div><div class="rb">' + ICON.set + "</div></div>";
  }
  // v7's rail: no thread; plates, a 30 px icon, the label at 0.78x and the moon bead on the rail's left edge.
  // Revision 3: the Journal badge counts quests newly ready since the player last looked (3 here), not every Ready quest.
  var RAIL = [["Journal", "orbit", "3"], ["Moonlit", "moon"], ["Characters", "chars"], ["Flight", "flight"], ["My blues", "blues"]];
  function stationIcon(lv, k) { return k === "orbit" ? (lv === "plain" ? flatGlyph("in-journal", 20).replace('width="20" height="20"', "") : orbit(0.65, lv === "quiet" ? "quiet" : "full")) : ICON[k]; }
  function stations(lv, o, list) {
    var sth = lv === "full" ? 72 : lv === "quiet" ? 66 : 46, on = o.on == null ? 0 : o.on, h = "";
    (list || RAIL.map(function (_, i) { return i; })).forEach(function (k, i) {
      var s = RAIL[k], cls = "st7" + (k === on ? " on" : "") + (o.hov === k ? " hov" : "") + (o.half === k ? " half" : "");
      var bd = k === 0 ? (o.badge != null ? o.badge : s[2]) : s[2];
      h += '<div class="' + cls + '" data-st="' + k + '"><i class="pl"></i><i class="gl"></i><span class="ic">' + stationIcon(lv, s[1]) + (bd ? '<span class="bd' + (String(bd).length > 2 ? " w3" : String(bd).length > 1 ? " w2" : "") + '">' + bd + "</span>" : "") + '</span><span class="lb">' + (o.label && k === o.labelAt ? o.label : s[0]) + "</span></div>";
    });
    var bi = o.bead != null ? o.bead : (list ? list.indexOf(on) : on);
    var beadTop = (bi + 0.5) * sth - (lv === "plain" ? 0 : 8);
    if (lv !== "plain" && bi >= 0) h += '<i class="bead" style="top:' + beadTop.toFixed(1) + 'px"></i>';
    return '<div class="sts">' + h + "</div>";
  }
  function rail7(lv, o) {
    var H = o.h || 790, top = 8 + 40 + 4 + 1 + 8 + 5 * 72 + 10, fTop = H - 216;
    var h = '<nav class="rail r7">' + (lv === "full" && !SNOW ? sky7({ seed: 7, w: 70, h: H, n: 18, rects: [[0, top, 70, Math.max(0, fTop - top)]], anim: true, drift: true }) : "");
    h += '<div class="lay" style="display:flex;flex-direction:column;align-items:center;width:100%;flex:1">' + (lv === "plain" ? "" : CREST + '<div class="rule"></div>');
    h += stations(lv, o) + foot(lv);
    return h + "</div></nav>";
  }

  function tree(lv, o) {
    var H = o.h || 790, w = lv === "full" ? 292 : 0;
    var sky = "";
    // Light palettes: no stars at all (the supervisor's ruling); the dawn sky is the pane gradient alone.
    if (lv === "full" && SNOW) sky = "";
    else if (lv === "full") sky = o.v7 ? sky7({ seed: 31, w: w, h: H, n: 46, rects: [[8, 396, w - 16, H - 404]], band: o.band ? [-30, 640, w + 30, 450, 74] : null, fig: ["arr", 160, 520, 90], anim: true, drift: true }) : stars13(31, 292, 820, 34, 1, .64, .04, .96);
    var h = '<div class="tree">' + sky + '<div class="lay">';
    h += '<div class="th">' + ICON.sig + "<b>Journal</b>" + (lv === "full" ? '<i class="r"></i>' : "") + "<span>5,373 quests</span></div>";
    TREE.forEach(function (t, k) {
      if (t[0] === "hair") { h += lv === "full" ? '<div class="mrd"><i></i><b></b><i></i></div>' : '<div class="hr"></div>'; return; }
      h += '<div class="tn' + (t[0] ? " " + t[0] : "") + (k === 4 ? " sel" : "") + '"><span class="cv">' + (t[0] === "sec" ? "▸" : "") + "</span>";
      if (lv !== "plain") h += orbit(t[3], lv);
      h += '<span class="nm">' + esc(t[1]) + "</span>";
      if (t[4]) h += '<span class="rdy">' + t[4] + "</span>";
      h += '<span class="ct">' + t[2] + "</span>";
      if (lv === "plain") h += '<span class="pct">' + Math.round(t[3] * 100) + "%</span>";
      if (lv === "full" && t[0] === "sec") h += '<span class="road"></span>';
      h += "</div>";
    });
    h += "</div>" + (lv === "full" && o.v7 && !SNOW ? meteor(96, 410) + meteor(150, 470).replace('class="shooting"', 'class="shooting faint"') : "") + "</div>";
    return h;
  }

  function table(lv, o) {
    var h = '<div class="tbl" role="grid">';
    if (lv === "full" && !SNOW) h += o.v7 ? sky7({ seed: 97, w: 760, h: 40, n: 22, rects: [[150, 4, 470, 16]], anim: true, drift: true }).replace('class="sky"', 'class="sky" style="height:40px"') : stars13(97, 900, 34, 16, .8, .15, .34, .8).replace('class="sky"', 'class="sky" style="height:34px"');
    if (lv !== "plain") h += '<div class="ttl"><b>Pinned</b><span>' + ROWS.length + ' quests</span><em>sorted by name</em></div>';
    h += '<div class="thd cols"><div></div><div class="sort">Name ▴</div><div>Lv</div><div>Job</div><div>Status</div><div>Exp</div></div><div class="rows">';
    var groups = [];
    ROWS.forEach(function (q) { if (groups.indexOf(q.g) < 0) groups.push(q.g); });
    groups.forEach(function (g) {
      var items = ROWS.filter(function (q) { return q.g === g; });
      h += '<div class="grp"><b>' + esc(g) + "</b><span>" + items.length + "</span></div>";
      items.forEach(function (q) {
        var cls = "tr cols" + (STRIPE[q.st][1] ? " " + STRIPE[q.st][1] : "") + ((SELN ? q.n === SELN : q.sel) ? " sel" : "") + (q.tip ? " hov" : "") + (q.st === "ready" ? " rdy" : "");
        h += '<div class="' + cls + '" style="--stripe:' + STRIPE[q.st][0] + '"><div class="g">' + rowGlyph(lv, q.st) + "</div>";
        h += '<div class="nmc' + (q.st === "completed" ? " cp" : "") + '">' + esc(q.n) + "</div>";
        h += '<div><span class="lvp">' + q.lv + "</span></div>";
        var job = q.job === "All" ? "Any" : q.job === "Tank" ? '<span class="jb tank"></span>Tank' : '<span class="jb">' + q.job + "</span>" + (lv === "plain" ? q.job : "");
        h += '<div><span class="jc">' + job + "</span></div>";
        h += '<div class="stx"><span class="sw">' + esc(q.lab) + "</span>" + (q.det ? ' · <span class="why' + (q.st === "blocked" || q.st === "locked-out" ? " ec" : "") + '">' + esc(q.det) + "</span>" : "") + "</div>";
        h += '<div class="xpp">' + q.xp + "</div>";
        if (q.tip && o.tips !== false) h += tip(lv, q.st);
        h += "</div>";
      });
    });
    return h + "</div></div>";
  }
  function tip(lv, st) {
    if (lv === "full") return '<div class="tip"><i class="cm a"></i><i class="cm d"></i><div class="tg">' + medalImg(st, 40) + "<div><b>" + NAME[st] + "</b><p>" + MEAN[st] + "</p></div></div></div>";
    if (lv === "quiet") return '<div class="tip"><div class="tg">' + quietMedal(st, 28) + "<div><b>" + NAME[st] + "</b><p>" + MEAN[st] + "</p></div></div></div>";
    return '<div class="tip"><b>' + NAME[st] + ".</b> <p>" + MEAN[st] + "</p></div>";
  }

  // ---------- The quest pane (owner-quest-pane.png's quest) ----------
  function card(lv, inner) { return '<div class="card">' + (lv === "full" ? '<i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i>' : "") + inner + "</div>"; }
  function reqs() {
    return D.req.map(function (x, k) {
      return '<div class="req ' + x[0] + (k === 0 ? " nx" : "") + '"><span class="ico">' + (x[0] === "ok" ? ICON.check : ICON.xmark) + '</span><span class="lab">' + esc(x[1]) + '</span><span class="d' + (x[0] === "ok" ? "" : " ec") + '">' + esc(x[2]) + "</span></div>" +
        (k === 1 ? '<div class="lvbar"><i></i><span>1 → 10</span></div>' : "");
    }).join("");
  }
  function unlocks() { return '<div class="unl"><div class="sub">Actions</div><div class="it"><span class="ai"></span><div><b>Blood Drain</b><span>Blue magic</span></div></div></div>'; }
  function pathCard(lv, o) {
    var w = lv === "full" ? 352 : lv === "quiet" ? 334 : 300, h = 168;
    var sky = lv === "full" ? (o.v7 ? sky7({ seed: 5 * 7919 + 6, w: w, h: h, n: 22, rects: [[236, 24, w - 244, 132]], anim: true }) : stars13(7919 + 6, w, h, 10, 0.9, 0.18, 0.66, 0.98)) : "";
    var med = function (st, px) { return lv === "plain" ? flatGlyph(st, 12) : lv === "quiet" ? quietMedal(st, px - 2) : medalImg(st, px); };
    return '<div class="path" style="height:' + h + 'px">' + sky + '<i class="thr"></i>' +
      '<div class="pband">' + (lv === "full" ? ICON.sig.replace('class="sig"', 'class="sg"') : "") + "A Realm Reborn</div>" +
      '<div class="prow"><span class="bd3"></span><span class="dn">162 quests done ›</span></div>' +
      '<div class="prow"><span class="nd">' + med("completed", 20) + '</span><span style="color:var(--moon)">Blue Leading the Blue</span></div>' +
      '<div class="prow cur"><span class="nd">' + med("blocked", 26) + '</span><b>Blue Collar Work</b><span class="x">✕</span></div>' +
      '<div class="pnext">Unlocks next · 1</div>' +
      '<div class="prow"><span class="nd">' + med("not-checked", 20) + '</span><span style="color:var(--mist)">Why They Call It the Blues</span></div></div>';
  }
  function actions(lv) {
    var h = '<div class="acts"><div class="pills">';
    [["Go to giver", "arrow", 1], ["Teleport", "plane"], ["Walk to giver", "walk"]].forEach(function (p) { h += '<div class="apill' + (p[2] ? " pri" : "") + '">' + ICON[p[1]] + "<span>" + p[0] + "</span></div>"; });
    h += '</div><div class="rbs">';
    ["pin", "path", "flag", "link", "copy", "book", "more"].forEach(function (k) { h += '<div class="ab">' + (k === "book" ? ICON.book.replace('viewBox="0 0 20 20"', 'viewBox="0 0 20 20" style="width:14px;height:14px"') : ICON[k]) + "</div>"; });
    return h + '</div><div class="prov">Checked 5 min ago · live</div></div>';
  }
  function detail(lv, o) {
    var q = D, h = '<aside class="det"><div class="det-in">', scroll = o.scroll != null ? o.scroll : lv === "full" ? 92 : 0;
    var reqHead = "2 of 3 unmet";
    h += '<div class="det-sc" style="transform:translateY(-' + scroll + 'px)">';
    if (lv === "full") {
      h += '<div class="ban"><div class="art"><div class="img" style="background-image:url(\'' + SCENE[q.art] + '\');background-position:' + q.pos + '"></div><i class="mul"></i></div><i class="scrim"></i><i class="wash"></i>' + road() +
        '<div class="t"><div class="n">' + esc(q.n) + '</div><div class="m">' + esc(q.path) + " · Lv " + q.lv + "</div></div>" +
        '<div class="hm">' + medalImg(q.st, 80, true) + "</div></div>";
      h += '<div class="hero"><div class="hl" style="color:' + INK[q.st] + '">' + NAME[q.st] + '</div><div class="hd">' + esc(q.det) + "</div></div>";
      h += '<div class="mrd"><i></i><b></b><i></i></div>';
    } else if (lv === "quiet") {
      h += '<div class="qh"><div class="n">' + esc(q.n) + '</div><div class="m">' + esc(q.path) + " · Lv " + q.lv + "</div></div>";
      h += '<div class="hero">' + quietMedal(q.st, 52, true) + '<div><div class="hl" style="color:' + INK[q.st] + '">' + NAME[q.st] + '</div><div class="hd">' + esc(q.det) + "</div></div></div>";
    }
    if (lv !== "plain") {
      h += card(lv, '<h4>Requirements<span class="r ec">' + reqHead + "</span></h4>" + reqs());
      h += card(lv, '<h4>Rewards</h4><div class="rwline">855 EXP · 414 gil</div>');
      h += card(lv, "<h4>Unlocks</h4>" + unlocks());
      h += card(lv, '<h4>Moonlit</h4><div class="mlt">Listed in Moonlit treasures.</div>');
      h += card(lv, '<h4>Path<span class="r">162 of 173 done</span></h4>' + pathCard(lv, o));
    } else {
      h += '<div class="ph"><div class="n">' + esc(q.n) + '</div><div class="m">' + esc(q.path) + "</div></div>";
      h += '<dl class="kv"><dt>State</dt><dd>' + flatGlyph(q.st, 12) + '<span style="color:' + INK[q.st] + '">' + NAME[q.st] + '</span><span class="mu">· ' + esc(q.det) + "</span></dd>" +
        "<dt>Level</dt><dd>" + q.lv + ' <span class="mu">· BLU · ' + q.xp + "</span></dd></dl>";
      h += '<div class="ps">Requirements<span class="ec">' + reqHead + "</span></div>" + reqs();
      h += '<div class="ps">Rewards<span></span></div><div class="rl">855 EXP · 414 gil</div>';
      h += '<div class="ps">Unlocks<span>1 action</span></div>' + unlocks();
      h += '<div class="ps">Moonlit<span></span></div><div class="jl">Listed in Moonlit treasures.</div>';
      h += '<div class="ps">Path<span>162 of 173 done</span></div>' + pathCard(lv, o);
    }
    h += "</div>";
    if (scroll) h += '<i class="vsb" style="top:' + (4 + scroll * 0.36).toFixed(0) + 'px;height:58%"></i>';
    h += "</div>" + actions(lv) + "</aside>";
    return h;
  }

  function status(lv) {
    var h = '<div class="w-stat">';
    if (lv === "full") {
      h += orbit(0.65, "full").replace('class="orb"', 'class="hal"') + "<span>65%</span>" + '<span class="dt">·</span><span>5,373 quests · showing ' + Q.length + " of " + Q.length + "</span>" +
        '<span class="dt">·</span><span class="pip"></span><span>live</span><span class="dt">·</span><span class="msq">' + medalImg("in-journal", 16) + "MSQ · Dawntrail ›</span>";
    } else if (lv === "quiet") {
      h += orbit(0.65, "quiet").replace('class="orb"', 'class="hal"') + "<span>65%</span>" + '<span class="dt">·</span><span>5,373 quests · showing ' + Q.length + " of " + Q.length + "</span>" +
        '<span class="dt">·</span><span class="pip"></span><span>live</span><span class="dt">·</span><span class="msq">MSQ · Dawntrail ›</span>';
    } else {
      h += "<span>65% · 5,373 quests · showing " + ROWS.length + " of " + Q.length + '</span><span class="dt">|</span><span class="pip"></span><span>live</span><span class="dt">|</span><span>MSQ: <span class="msq">Dawntrail</span></span>';
    }
    return h + '<span class="ver">' + (V7 ? "v1.14.0" : "v1.13.0") + "</span></div>";
  }

  // ---------- The filter drawer ----------
  var F_WIN = { hide: false, avail: false, pinnedFirst: true, stalled: 7, states: STATES.slice(), exp: [], lv: [1, 100], job: "All", cur: false, rw: "Any", more: ["Include removed"], count: 1 };
  var F_BOARD = { hide: true, avail: false, pinnedFirst: true, stalled: 7, states: STATES.filter(function (s) { return s !== "not-checked"; }), exp: ["ARR", "HW"], lv: [1, 100], job: "All", cur: false, rw: "Any", more: [], count: 3 };
  var MORE = ["Repeatable only", "Seasonal active only", "Include removed", "Include other paths", "Pinned only", "Abandoned only", "Once-only story quests I haven't done"];
  var EXPS = ["ARR", "HW", "StB", "ShB", "EW", "DT"];
  function tg(on) { return '<span class="tg' + (on ? " on" : "") + '"></span>'; }
  function trow(label, sub, on, cls) { return '<div class="trow tgl' + (cls ? " " + cls : "") + '"><div class="tl"><span>' + esc(label) + "</span>" + (sub ? "<em>" + sub + "</em>" : "") + "</div>" + tg(on) + "</div>"; }
  function sec(lv, title, inner, o) {
    o = o || {};
    return '<div class="dsec' + (o.open ? " open" : "") + '"><h5>' + (o.disc ? '<span class="cv">▶</span>' : "") + (lv === "full" && !o.disc ? "" : "") + "<span>" + title + "</span>" + (o.pc ? '<span class="pc">' + o.pc + "</span>" : "") + "</h5>" + inner + "</div>";
  }
  function stateChip(lv, st, on) {
    var g = lv === "full" ? medalImg(st, 16) : lv === "quiet" ? quietMedal(st, 16) : flatGlyph(st, 12);
    return '<span class="sc ' + (on ? "on" : "off") + '">' + g + NAME[st] + "</span>";
  }
  function drawer(lv, adv, F, o) {
    o = o || {};
    var h = '<div class="drw"' + (o.maxh ? ' style="max-height:' + o.maxh + 'px"' : "") + ">" + (lv === "full" ? '<i class="cmk"></i>' : "");
    h += '<div class="dh"><span class="dic">' + ICON.filt.replace(/#151C33/g, lv === "full" ? "#1A2140" : "#1A2135") + "</span><b>Filters</b>" + (F.count ? '<span class="dct">' + (lv === "plain" ? "· " + F.count + " on" : F.count + " on") + "</span>" : "") +
      '<span class="dsp"></span><span class="dib' + (o.pinned ? " on" : "") + '">' + ICON.pin + '</span><span class="dib">' + ICON.x + "</span></div>";
    h += lv === "full" ? '<div class="mrd"><i></i><b></b><i></i></div>' : lv === "quiet" ? '<div class="hr"></div>' : "";
    h += '<div class="dsc"><div class="dsc-in"' + (o.to ? ' data-to="' + o.to + '"' : "") + ' style="transform:translateY(-' + (o.scroll || 0) + 'px)">';
    var per = function (n) { return n ? "<u>Per category · " + n + " changed ›</u>" : "<u>Per category ›</u>"; };
    h += sec(lv, "Show", trow("Hide completed", per(F.hide ? 1 : 0), F.hide) + trow("Available now", per(0), F.avail) + trow("Pinned first", "Sort pinned quests to the top", F.pinnedFirst));
    h += sec(lv, "Quick views", '<div class="trow"><div class="tl"><span>Stalled after</span><em>The Stalled view counts from here</em></div><span class="stp"><i>−</i><span>' + F.stalled + ' days</span><i>+</i></span></div>');
    var nStates = F.states.length, advSet = (nStates < 8 ? 1 : 0) + (F.exp.length ? 1 : 0) + (F.lv[0] > 1 || F.lv[1] < 100 ? 1 : 0) + (F.job !== "All" ? 1 : 0) + (F.rw !== "Any" ? 1 : 0) + F.more.length;
    var inner = "";
    if (!adv) {
      var S = [["States", nStates === 8 ? "All 8" : nStates + " of 8", nStates < 8], ["Expansions", F.exp.length ? F.exp.join(", ") : "Any", F.exp.length > 0], ["Added in", "Any patch", false],
        ["Level", F.lv[0] + " to " + F.lv[1], false], ["Job", F.job, false], ["Rewards", F.rw, false], ["More", F.more.length ? F.more.join(", ") : "None on", F.more.length > 0]];
      inner = '<div class="sum">' + S.map(function (s, i) { return '<div class="sr' + (o.hovRow === i ? " hov" : "") + '"><span class="k">' + s[0] + '</span><span class="v' + (s[2] ? " set" : "") + '">' + esc(s[1]) + '</span><span class="ch">›</span></div>'; }).join("") + "</div>";
    } else {
      inner += '<div class="gp"><div class="gh">States<span class="' + (nStates < 8 ? "set" : "") + '">' + (nStates === 8 ? "All 8" : nStates + " of 8") + '</span></div><div class="sch">' + STATES.map(function (s) { return stateChip(lv, s, F.states.indexOf(s) >= 0); }).join("") + "</div></div>";
      inner += '<div class="gp"><div class="gh">Expansions<span class="' + (F.exp.length ? "set" : "") + '">' + (F.exp.length ? F.exp.join(", ") : "Any") + '</span></div><div class="sgm">' + EXPS.map(function (x) { return '<span class="' + (F.exp.indexOf(x) >= 0 ? "on" : "") + '">' + x + "</span>"; }).join("") + "</div></div>";
      inner += '<div class="gp"><div class="gh">Added in</div><div class="dd">Any patch<i>▼</i></div></div>';
      inner += '<div class="gp"><div class="gh">Level<span>' + F.lv[0] + " to " + F.lv[1] + '</span></div><div class="rng"><span class="f">Lv ' + F.lv[0] + '</span>to<span class="f">' + F.lv[1] + '</span></div><div class="rtr"><i style="left:0;right:0;background:#7C86A8"></i></div></div>';
      inner += '<div class="gp"><div class="gh">Job</div><div class="sgm">' + ["All", "DoW/DoM", "DoH", "DoL"].map(function (x) { return '<span class="' + (x === F.job ? "on" : "") + '">' + x + "</span>"; }).join("") + "</div>" + trow("Current job only", "", F.cur) + "</div>";
      inner += '<div class="gp"><div class="gh">Rewards<span>' + F.rw + "</span></div>" + ["Mount", "Minion", "Orchestrion roll", "Emote", "Hairstyle", "Triple Triad card"].map(function (k) { return '<div class="rwr"><span class="k">' + k + '</span><span class="sgm"><span>Hide</span><span class="on">Show</span><span>Only</span></span></div>'; }).join("") + '<div class="more">17 more kinds ›</div></div>';
      inner += '<div class="gp"><div class="gh">More<span class="' + (F.more.length ? "set" : "") + '">' + (F.more.length ? F.more.length + " on" : "None on") + "</span></div>" + MORE.map(function (m) { return trow(m, "", F.more.indexOf(m) >= 0); }).join("") + "</div>";
    }
    h += sec(lv, "Advanced", inner, { disc: true, open: adv, pc: advSet ? advSet + " set" : "" });
    h += "</div>" + (o.sbar ? '<i class="sbar" style="top:' + o.sbar[0] + "px;height:" + o.sbar[1] + 'px"></i>' : "") + "</div>";
    h += '<div class="dft"><span class="sh">Showing <b>' + ROWS.length + "</b> of " + Q.length + '</span><span class="rst' + (o.rhov ? " hov" : "") + (F.count ? "" : " off") + '">' + ICON.reset + "Reset</span></div>";
    return h + "</div>";
  }
  // 1.13 as shipped (owner-filter-menu.png)
  function drawer13() {
    return '<div class="drw13"><div class="h">Filters<span class="ib">' + ICON.pin + ICON.x + '</span></div><div class="dis">Quick views</div>' +
      '<div class="ln"><span class="sl">7 days</span>Stalled after</div><div class="sep"></div>' +
      '<div class="ln"><span class="cb"></span>Hide completed <span class="sb">Overrides</span></div>' +
      '<div class="ln"><span class="cb"></span>Available now <span class="sb">Overrides</span></div>' +
      '<div class="ln"><span class="cb on"></span>Pinned first</div><div class="adv">Advanced</div><div class="ln" style="margin-top:2px">Reset</div><div class="sep"></div></div>';
  }

  function win(lv, o) {
    o = o || {}; V7 = o.v7 !== false; o.v7 = V7; ROWS = V7 ? filterQ(o.F || F_WIN) : Q;
    THEME = o.theme || "medallion"; SNOW = o.palette === "snow"; SELN = o.sel || null; PALNOW = o.palette || null;
    var cls = "mk " + lv + (V7 ? " v7" : "") + (SNOW ? " snow" : "") + (o.palette && o.palette !== "snow" ? " " + o.palette : "") + (o.hc ? " hc" : "") + " th-" + THEME + (o.drawer && V7 ? " dopen" : "") + (o.drawer && !V7 ? " b13open" : "");
    var body = (V7 ? rail7(lv, o) : rail13(lv)) + tree(lv, o) + table(lv, o) + (o.detailFn ? o.detailFn(lv, o) : detail(lv, o));
    if (o.drawer) body += V7 ? drawer(lv, o.drawer === "e", o.F || F_WIN, o.dopt || {}) : drawer13();
    var out = '<div class="' + cls + '"' + (o.style ? ' style="' + o.style + '"' : "") + '><div class="win">' + frame(lv, o) + '<div class="w-body">' + body + "</div>" + status(lv) + "</div></div>";
    THEME = "medallion"; SNOW = false; SELN = null; PALNOW = null;
    return out;
  }

  // ---------- Boards ----------
  function crop(w, h, lv, o, ww, wh, x, y) {
    return '<div class="crop" style="width:' + w + "px;height:" + h + 'px">' + win(lv, Object.assign({ style: "width:" + (ww || 1560) + "px;height:" + (wh || 760) + "px;transform:translate(-" + (x || 0) + "px,-" + (y || 0) + 'px)' }, o)) + "</div>";
  }
  function fig(inner, cap) { return "<figure>" + inner + "<figcaption>" + cap + "</figcaption></figure>"; }

  function boardDrawer() {
    var hgt = 850, wh = 870, h = '<div class="board"><h2>The filter drawer<small>Over the tree column exactly, sized to its content, opaque, with a clean edge. The tree under it fades out with the same fade as the drawer (0.16 s). 1:1 crops of an 870 px window.</small></h2>';
    h += '<div class="row">';
    h += fig(crop(416, hgt, "full", { v7: false, drawer: "c", tips: false }, 1560, wh), "<b>1.13 as shipped</b> <i>(owner-filter-menu.png)</i>: full-height grey, the title under the tree's JOURNAL, counts and the selection bar showing through, 300 px wide so it also overlaps the list.");
    h += fig(crop(416, hgt, "full", { v7: true, drawer: "c", F: F_BOARD, tips: false, dopt: { hovRow: 1 }, h: wh - 126 }, 1560, wh), "<b>Full, Advanced collapsed.</b> The sheet ends under its footer and the sky shows below. Seven summary lines fill what was empty grey and say what each group holds (Expansions hovered).");
    h += fig(crop(416, hgt, "full", { v7: true, drawer: "e", F: F_BOARD, tips: false, dopt: { to: "adv", sbar: [74, 330], rhov: true }, h: wh - 126 }, 1560, wh), "<b>Full, Advanced expanded</b>, scrolled to the groups; the header and footer stay put. Reset is hovered: a quiet text action, then an Undo toast.");
    h += fig(crop(396, hgt, "quiet", { v7: true, drawer: "c", F: F_BOARD, tips: false }, 1560, wh), "<b>Quiet, collapsed.</b> A flat sheet one tone up, a neutral hairline edge, a separation shadow only. Toggles turn silver, not gold.");
    h += "</div><div class=\"row\">";
    h += fig(crop(396, hgt, "quiet", { v7: true, drawer: "e", F: F_BOARD, tips: false, dopt: { to: "adv", sbar: [70, 330] } }, 1560, wh), "<b>Quiet, expanded.</b> Body-face headings at 1.15x; silver-rimmed medals on the state chips.");
    h += fig(crop(362, hgt, "plain", { v7: true, drawer: "c", F: F_BOARD, tips: false }, 1560, wh), "<b>Plain, collapsed.</b> A ledger: bands, 24 px rows, checkboxes, a key-value summary. No radius, no shadow.");
    h += fig(crop(362, hgt, "plain", { v7: true, drawer: "e", F: F_BOARD, tips: false, dopt: { to: "adv", sbar: [40, 420] } }, 1560, wh), "<b>Plain, expanded.</b> Flat glyphs, 20 px controls. Nothing animates.");
    h += '<div class="spec" style="width:560px"><table><tr><th colspan="2">Drawer at Full (logical px)</th></tr>' +
      "<tr><td>Box</td><td>the tree column exactly (292 x content), never wider, so nothing of the list is under it; height = content, capped at the body, then the body scrolls and the footer stays</td></tr>" +
      "<tr><td>Under it</td><td>the tree is not drawn while the drawer is open (it fades out over the same 0.16 s the drawer fades in); below the sheet the column's own background shows (Full: the sky)</td></tr>" +
      "<tr><td>Sheet</td><td>Raised gradient at .995; brass edge on the right and bottom (the card gradient); radius 6 at the bottom right with a darker corner mark; drop shadow 0 14 30 at .55 and an unoffset contact shadow 0 0 14 at .30 on the list side only</td></tr>" +
      "<tr><td>Header</td><td>52 px: a bare 13 px MoonHigh filter glyph, <b>Filters</b> in the Title role at Jupiter 16, a neutral count pill (\"3 on\"), pin and close as 26 px round buttons</td></tr>" +
      "<tr><td>Sections</td><td>Show · Quick views · Advanced, in the Section heading role, the same as the quest pane</td></tr>" +
      "<tr><td>Rows</td><td>36 px: label 13 px Text, a caption 11 px Tertiary (the per-category overrides live here as a link), MoonToggle 36 x 20</td></tr>" +
      "<tr><td>Advanced</td><td>closed: 7 summary lines (30 px, value in Text; a MoonHigh dot when set; a click opens the group). The section rule is OrnamentLight; the pills are neutral; gold is only on the toggles. Open: state chips with 16 px medals, expansion and job segments, a level range, reward tri-states, More toggles</td></tr>" +
      "<tr><td>Footer</td><td>48 px over a brass rule: Showing N of M, then <b>Reset</b>: no border, Secondary text, a hover wash, disabled when nothing is set, an Undo toast after</td></tr>" +
      "<tr><td>Motion</td><td>as 1.13: fade 0.16 s; the chevron turns over 0.14 s; opened groups reveal over 0.14 s (opacity only); the sheet's height changes at once</td></tr></table></div>";
    return h + "</div></div>";
  }

  function railTile(lv, o, list, w, h, zoom) {
    var sth = lv === "full" ? 72 : lv === "quiet" ? 66 : 46;
    return '<div class="tile" style="width:' + w * zoom + "px;height:" + h * zoom + 'px"><div class="mk ' + lv + ' v7" style="width:' + w + "px;height:" + h + "px;zoom:" + zoom + '"><div class="win" style="border:0;box-shadow:none;border-radius:0"><div class="w-body" style="grid-template-columns:' + w + 'px"><nav class="rail r7" style="border-right:0;padding:0;justify-content:center">' +
      stations(lv, o, list) + "</nav></div></div></div></div>";
  }
  function boardRail() {
    var h = '<div class="board"><h2>The rail<small>Left: 1:1 crops. Right: station states at 2x. No thread and no lit bar between stations. Stations share the rail\'s height (72 px at Full, up to 84), icons 30 px, labels 0.78x. Motion from MotionTokens.</small></h2><div class="row">';
    var H = 742;
    h += fig(crop(118, H, "full", { v7: false, tips: false }), "<b>1.13 Full</b><br><i>thread, lit bar, 22 px icons</i>");
    h += fig(crop(124, H, "full", { v7: true, tips: false }), "<b>v7 Full</b><br><i>plate, gold icon, bead</i>");
    h += fig(crop(118, H, "quiet", { v7: true, tips: false }), "<b>v7 Quiet</b><br><i>flat plate, dot</i>");
    h += fig(crop(92, H, "plain", { v7: true, tips: false }), "<b>v7 Plain</b><br><i>band and bar</i>");
    h += '<div style="display:flex;flex-direction:column;gap:14px">';
    var z = 2, row = '<div class="row" style="gap:12px">';
    row += fig(railTile("full", { on: -1 }, [1], 70, 72, z), "<b>Idle</b><br><i>icon .72, no plate</i>");
    row += fig(railTile("full", { on: -1, half: 1 }, [1], 70, 72, z), "<b>Hover, 60 ms</b><br><i>halfway in</i>");
    row += fig(railTile("full", { on: -1, hov: 1 }, [1], 70, 72, z), "<b>Hover</b><br><i>lift 2 px, plate .55, dark foot</i>");
    row += fig(railTile("full", { on: 1 }, [1], 70, 72, z), "<b>Selected</b><br><i>plate, gold icon, bead</i>");
    row += fig(railTile("full", { on: 2, bead: 0.62, half: 2 }, [1, 2], 70, 144, z), "<b>Travel, 110 ms</b><br><i>bead between stations, plate fading in</i>");
    h += row + "</div>";
    row = '<div class="row" style="gap:12px;align-items:flex-end">';
    row += fig(railTile("quiet", { on: -1 }, [3], 66, 66, z), "<b>Quiet idle</b>");
    row += fig(railTile("quiet", { on: -1, hov: 3 }, [3], 66, 66, z), "<b>Quiet hover</b><br><i>lift 1 px</i>");
    row += fig(railTile("quiet", { on: 3 }, [3], 66, 66, z), "<b>Quiet selected</b>");
    row += fig(railTile("plain", { on: -1 }, [3], 44, 46, z), "<b>Plain idle</b>");
    row += fig(railTile("plain", { on: -1, hov: 3 }, [3], 44, 46, z), "<b>Plain hover</b><br><i>instant</i>");
    row += fig(railTile("plain", { on: 3 }, [3], 44, 46, z), "<b>Plain selected</b>");
    h += row + "</div>";
    h += '<div class="spec"><table><tr><th>Moment</th><th>Full</th><th>Quiet</th><th>Plain</th></tr>' +
      "<tr><td>Hover in</td><td>plate 0 → .55 with a 1 px darker bottom edge, icon alpha .72 → .95, icon rises 2 px; <b>HoverIn 0.12 s</b>, ease-out cubic</td><td>plate wash, rise 1 px; HoverIn 0.12 s</td><td>flat wash, instant</td></tr>" +
      "<tr><td>Hover out</td><td>back over <b>HoverOut 0.18 s</b>, ease-out cubic (a pass down the rail leaves a short trail)</td><td>HoverOut 0.18 s</td><td>instant</td></tr>" +
      "<tr><td>Label</td><td colspan=\"3\">colour only (Secondary → Text). It never moves: text the player reads only changes opacity.</td></tr>" +
      "<tr><td>Select</td><td>plate (Moon .07) and gold icon ink fade in over <b>Select 0.15 s</b>; the old station's fade out over <b>Leave 0.12 s</b>; icon settles to 0 px. No glow, border or hairline</td><td>plate in 0.15 s</td><td>band, instant</td></tr>" +
      "<tr><td>Travel</td><td>bead runs the rail's left edge from the old station to the new, <b>Travel 0.22 s</b>, ease-in-out cubic; arrives as the plate finishes</td><td>dot, same timing, no glow</td><td>none (2 px bar)</td></tr>" +
      "<tr><td>Reduce motion</td><td colspan=\"3\">everything lands at once: no rise, no fades, the bead jumps.</td></tr></table></div>";
    h += "</div></div>";
    // Revision 3: labels and the Journal badge
    var z2 = 2;
    h += '<div class="row" style="gap:12px;align-items:flex-start">';
    h += fig(railTile("full", { on: 2 }, [2], 70, 72, z2), "<b>Full, selected</b><br><i>Characters fits its plate</i>");
    h += fig(railTile("full", { on: -1, hov: 2 }, [2], 70, 72, z2), "<b>Full, hovered</b>");
    h += fig(railTile("quiet", { on: 2 }, [2], 66, 66, z2), "<b>Quiet, selected</b>");
    h += '<div class="spec" style="width:470px"><b>Label fit</b> (game units, measured live): label px = clamp(0.75 x body, 10, 12), room = rail - 2 x 3 inset - 2 x 2 pad. Every other English label fits at every size untracked.<table id="lbt"></table></div>';
    h += "</div>";
    h += '<div class="row" style="gap:12px;align-items:flex-start">';
    h += fig('<div class="tile" style="width:140px;height:144px"><div class="mk full" style="width:70px;height:72px;zoom:2"><div class="win" style="border:0;box-shadow:none;border-radius:0"><div class="w-body" style="grid-template-columns:70px"><nav class="rail" style="border-right:0;padding:0;justify-content:center"><div class="sts"><div class="st"><span class="bd b13">99+</span>' + ICON.book + '<span class="lb">Journal</span></div></div></nav></div></div></div></div>', "<b>1.13</b>: every Ready quest; stuck at a shrunken 99+");
    h += fig(railTile("full", { on: 0, badge: "3" }, [0], 70, 72, z2), "<b>v7</b>: 3 newly ready since you last looked");
    h += fig(railTile("full", { on: -1, badge: "12" }, [0], 70, 72, z2), "<b>v7</b>: after a level-up, 12 new");
    h += fig(railTile("full", { on: 0, badge: "" }, [0], 70, 72, z2), "<b>v7</b>: nothing new, no badge");
    h += fig(railTile("plain", { on: -1, badge: "3" }, [0], 44, 46, z2), "<b>Plain</b>: a gold digit");
    h += '<div class="spec" style="width:430px"><div class="tipx"><b>Journal</b><br>Every quest in the game, filed as the journal files it<br><span style="color:#F2D27A">3 newly ready since you last looked</span> · 214 ready to accept now<br><i>Click to show the new ones</i></div>' +
      '<div style="margin-top:8px"><b>Settings › Display › Rail › Journal badge:</b> <u>Newly ready</u> (default) · Ready story and unlock quests · Every Ready quest (1.13) · Nothing</div></div>';
    return h + "</div></div></div>";
  }

  function boardStars() {
    var h = '<div class="board"><h2>The Full sky<small>Full only, never under text (each star keeps 4 px off every item\'s rect), seeded so the field never reshuffles. Twinkle and the meteor stop under Reduce motion.</small></h2>';
    var bg = "linear-gradient(180deg,#141C40 0%,#10162E 50%,#0F1424 100%)", W = 520, Hh = 280;
    function tileSky(inner, w, hh, lbl) { return '<div class="tile" style="width:' + w + "px;height:" + hh + "px;background:" + bg + '"><div class="mk full" style="position:absolute;inset:0;background:transparent">' + inner + "</div>" + (lbl ? '<span class="lbl">' + lbl + "</span>" : "") + "</div>"; }
    h += '<div class="row">';
    h += fig(tileSky(stars13(31, W, Hh, 34, 1, 0, 0, 1), W, Hh, "<b>1.13</b> one layer"), "<b>1.13</b>: one layer of faint dots, small discs and a few sparkles in two inks.");
    h += fig(tileSky(sky7({ seed: 31, w: W, h: Hh, n: 70, rects: [[0, 0, W, Hh]], fig: ["arr", 360, 120, 100], anim: true }), W, Hh, "<b>v7</b> three depths, constellation"), "<b>v7</b>: far, mid and near stars, four temperatures, a slow twinkle and the quest region's constellation (The Chocobo, A Realm Reborn).");
    var lay = function (L, lbl) {
      var r = rng(11), s = '<svg width="200" height="140" style="position:absolute;inset:0">';
      for (var i = 0; i < (L === 0 ? 60 : L === 1 ? 26 : 6); i++) {
        var x = 10 + r() * 180, y = 20 + r() * 110, c = tempOf(r(), L);
        if (L === 0) s += '<rect x="' + x.toFixed(1) + '" y="' + y.toFixed(1) + '" width="1" height="1" fill="' + c + '" opacity=".14"/>';
        else if (L === 1) s += '<circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r=".95" fill="' + c + '" opacity=".24"/>';
        else s += '<g opacity=".56"><circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="3.4" fill="' + c + '" opacity=".13"/><path d="M' + (x - 2.5).toFixed(1) + " " + y.toFixed(1) + "h5M" + x.toFixed(1) + " " + (y - 2.5).toFixed(1) + 'v5" stroke="' + c + '" stroke-width=".6" opacity=".4"/><circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="1.35" fill="' + c + '"/></g>';
      }
      return tileSky(s + "</svg>", 200, 140, lbl);
    };
    h += '<div style="display:flex;flex-direction:column;gap:10px"><div class="row" style="gap:10px">' + lay(0, "<b>Far</b> 60 %") + lay(1, "<b>Mid</b> 32 %") + lay(2, "<b>Near</b> 8 %") + "</div>";
    h += '<div class="spec" style="width:620px"><table><tr><th>Layer</th><th>Mark</th><th>Alpha</th><th>Twinkle</th></tr>' +
      "<tr><td>Far</td><td>1 x 1 px square</td><td>.10 to .16</td><td>none</td></tr><tr><td>Mid</td><td>disc r .95</td><td>.20 to .28</td><td>1 in 3: x1.18 / x.82, period 7 to 13 s</td></tr>" +
      "<tr><td>Near</td><td>disc r 1.35, a 5 px cross at .4 (fixed length while it twinkles), a soft r 3.4 halo at .13</td><td>.52 to .60</td><td>all: x1.28 / x.72, period 7 to 13 s</td></tr></table></div></div>";
    h += "</div><div class=\"row\">";
    var sw = [["#DCE5FF", "Cool white", "64 %", "every layer"], ["#F4F2EA", "Moon white", "24 %", "every layer"], ["#FFE2A8", "Warm gold", "9 %", "mid and near"], ["#FFC9AE", "Ember", "3 %", "mid and near"]];
    h += '<div class="spec" style="width:300px"><table><tr><th colspan="3">Temperatures</th></tr>' + sw.map(function (s) { return '<tr><td><svg width="22" height="14"><circle cx="7" cy="7" r="5" fill="' + s[0] + '" opacity=".9"/><circle cx="7" cy="7" r="7" fill="' + s[0] + '" opacity=".15"/></svg></td><td><b>' + s[1] + "</b> <code>" + s[0] + "</code></td><td>" + s[2] + " · " + s[3] + "</td></tr>"; }).join("") + "</table>" +
      '<div style="margin-top:10px"><b>Twinkle</b> (a near star over 20 s): base .56, x1.28 at 30 %, x.72 at 70 %, eased in and out.</div>' +
      '<svg width="276" height="70" style="margin-top:6px"><path d="M0 35H276" stroke="#2A3149"/>' + (function () { var p = "M0 35", i; for (i = 0; i <= 276; i += 3) { var t = (i / 276) * 20 / 9.6 % 1, v = t < .3 ? .5 - .5 * Math.cos(Math.PI * t / .3) : t < .7 ? 1 - 2 * (.5 - .5 * Math.cos(Math.PI * (t - .3) / .4)) : -1 + (.5 - .5 * Math.cos(Math.PI * (t - .7) / .3)); p += "L" + i + " " + (35 - v * 26).toFixed(1); } return '<path d="' + p + '" fill="none" stroke="#F2D27A" stroke-width="1.4"/>'; })() + '<text x="2" y="66" fill="#7C86A8" font-size="10">0 s</text><text x="250" y="66" fill="#7C86A8" font-size="10">20 s</text></svg></div>';
    var cons = Object.keys(FIG).map(function (k) {
      return fig(tileSky('<svg width="140" height="120" style="position:absolute;inset:0">' + figure(k, 30, 22, 80, { line: 0.16 }) + "</svg>", 140, 120), "<b>" + FIG[k].name + "</b><br><i>" + FIG[k].exp + "</i>");
    }).join("");
    h += '<div style="display:flex;flex-direction:column;gap:6px"><div class="row" style="gap:10px">' + cons + fig(tileSky(sky7({ seed: 44, w: 260, h: 200, n: 36, rects: [[0, 0, 260, 200]], band: [-20, 190, 280, 30, 80], anim: false }), 260, 200, "<b>Milky Way</b>, optional"), "<b>Off by default</b> (owner decision): only in one continuous sky rect at least 200 px tall, never in clipped patches.") + '</div><div style="font-size:12px;color:#A9B2CC;max-width:900px">Region constellations: our own motifs (not in-game star charts) in the Astrologian card idiom, 5 to 8 stars joined by 0.8 px lines at .11. One at a time, for the selected quest\'s expansion, only where the empty sky holds a 120 x 100 px box clear of every item. Shown above at 1.5x line strength so they read on this sheet.</div></div>';
    h += "</div><div class=\"row\">";
    // The meteor's frames
    var mf = '<svg width="520" height="150" style="position:absolute;inset:0">';
    [[0, .27], [.15, .8], [.3, .63], [.45, .37], [.6, .16]].forEach(function (f, i) {
      var t = 1 - Math.pow(1 - f[0] / 0.7, 3), x = 60 + t * 300, y = 30 + t * 160 * 0.53;
      mf += '<g opacity="' + (f[1] / 0.8).toFixed(2) + '"><line x1="' + (x - 40 * Math.cos(.489)).toFixed(1) + '" y1="' + (y - 40 * Math.sin(.489)).toFixed(1) + '" x2="' + x.toFixed(1) + '" y2="' + y.toFixed(1) + '" stroke="url(#mtl)" stroke-width="1.6" stroke-linecap="round"/><circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="1.8" fill="#FFF8E2"/><circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="4.5" fill="#FFF0BE" opacity=".25"/></g>' +
        '<text x="' + (x + 5).toFixed(1) + '" y="' + (y + 15).toFixed(1) + '" fill="#8A93B0" font-size="10">' + (i + 1) + "</text>";
    });
    mf += '<text x="300" y="30" fill="#8A93B0" font-size="10.5">1: 0.00 s, head .27 (fading in)</text><text x="300" y="44" fill="#8A93B0" font-size="10.5">2: 0.15 s, head .80 (peak), tail from .45</text><text x="300" y="58" fill="#8A93B0" font-size="10.5">3: 0.30 s, .63</text><text x="300" y="72" fill="#8A93B0" font-size="10.5">4: 0.45 s, .37</text><text x="300" y="86" fill="#8A93B0" font-size="10.5">5: 0.60 s, .16; gone at 0.70 s</text>';
    mf += '<defs><linearGradient id="mtl" x1="0" x2="1"><stop offset="0" stop-color="#FFF0BE" stop-opacity="0"/><stop offset="1" stop-color="#FFF0BE" stop-opacity=".56"/></linearGradient></defs></svg>';
    h += fig(tileSky(mf, 520, 150, "<b>Completion meteor</b> frames"), "Once per completion (the Wax moment's trigger), at most once in 30 s. 0.7 s, ease-out cubic, 28 deg below level (the medals' moon tilt), head peak .80 (brighter than any star, so it reads as a meteor), tail from .45. Starts in the largest empty sky on screen.");
    h += fig(crop(372, 300, "full", { v7: true, tips: false }, 1560, 790, 0, 470), "<b>In place</b>: the tree's empty sky under its last node, with The Chocobo (Blue Collar Work is an A Realm Reborn quest).");
    h += fig(crop(420, 300, "full", { v7: true, tips: false, h: 980 }, 1560, 1100, 1150, 710), "<b>The Path card</b>: stars only in the band's empty right side, clear of every label.");
    return h + "</div></div>";
  }

  function boardBA() {
    var h = '<div class="board"><h2>Before and after, Full<small>Same quest, same window. Left: 1.13 as shipped. Right: plan v7.</small></h2><div class="row">';
    var z = 0.535;
    var wnd = function (v7) { return '<div class="crop" style="width:' + (1560 * z).toFixed(0) + "px;height:" + (900 * z).toFixed(0) + 'px">' + win("full", { v7: v7, style: "width:1560px;height:900px;zoom:" + z }) + "</div>"; };
    h += fig(wnd(false), "<b>1.13</b>") + fig(wnd(true), "<b>v7</b>: rail without thread, a larger quest title over bigger section headings, column headers at 1.55x, the three-layer sky, the Completed moon.");
    h += '</div><div class="row">';
    h += fig(crop(404, 580, "full", { v7: false, tips: false }, 1560, 900, 1156, 90), "<b>1.13</b> quest pane: 12.5 px condensed caps in GiltHigh, thin and dim.");
    h += fig(crop(404, 580, "full", { v7: true, tips: false }, 1560, 900, 1156, 90), "<b>v7</b>: the Section role at 1.80x body, tracked, GiltLight, a 1 px shadow; the quest title rises to Jupiter 23 so it still leads.");
    h += '<div style="display:flex;flex-direction:column;gap:12px">';
    h += fig(crop(760, 128, "full", { v7: false, tips: false }, 1560, 900, 356, 96), "<b>1.13</b> Journal header: Eyebrow 1.45x in the tertiary tone, 26 px.");
    h += fig(crop(760, 128, "full", { v7: true, tips: false }, 1560, 900, 356, 96), "<b>v7</b>: 1.55x and +0.08 em in the secondary tone, 30 px: the same size as the group headers, never larger. The sorted column keeps its gilt.");
    h += '<div class="row" style="gap:12px">' + fig(crop(372, 190, "quiet", { v7: true, tips: false }, 1560, 900, 1182, 236), "<b>Quiet</b>: body face 1.15x, Text") + fig(crop(372, 190, "plain", { v7: true, tips: false }, 1560, 900, 1230, 192), "<b>Plain</b>: body face, Text, on a band") + "</div>";
    h += "</div></div></div>";
    return h;
  }

  // The label ladder (1.16): take the first rung that fits on one line.
  function fitLadders(root) {
    root.querySelectorAll("[data-ladder]").forEach(function (el) {
      var rungs = JSON.parse(el.getAttribute("data-ladder")); el.style.whiteSpace = "nowrap";
      for (var i = 0; i < rungs.length; i++) { el.textContent = rungs[i]; if (el.scrollWidth <= el.clientWidth + 0.5) break; }
    });
  }
  // ---------- Rail label fit (Revision 3) ----------
  // 1. Draw at the label size. 2. If wider than the plate's inner width, track -0.02 em. 3. Still wider: shrink to fit,
  // never under 10 px. 4. Still wider: two lines at a space; a single word that cannot fit goes icon-only with the label in
  // the tooltip. Never an ellipsis, never past the plate.
  function fitLabels(root) {
    root.querySelectorAll(".r7 .st7 .lb").forEach(function (lb) {
      var st = lb.parentNode, pl = st.querySelector(".pl");
      if (!pl || getComputedStyle(lb).display === "none") return;
      lb.style.letterSpacing = ""; lb.style.fontSize = ""; lb.style.whiteSpace = ""; st.classList.remove("iconly"); lb.removeAttribute("data-fit");
      var room = pl.getBoundingClientRect().width - 4 * (pl.getBoundingClientRect().width / pl.offsetWidth || 1);
      var w = function () { return lb.getBoundingClientRect().width; }, step = "fits";
      if (w() > room) { lb.style.letterSpacing = "-0.02em"; step = "tracked -0.02 em"; }
      if (w() > room) {
        var fs = parseFloat(getComputedStyle(lb).fontSize), nf = Math.max(10, Math.floor(fs * room / w() * 0.98 * 10) / 10);
        lb.style.fontSize = nf.toFixed(2) + "px"; step = "tracked and " + nf.toFixed(1) + " px";
      }
      if (w() > room + 0.5) {
        if (/ /.test(lb.textContent)) { lb.style.whiteSpace = "normal"; step = "two lines"; }
        else { st.classList.add("iconly"); step = "icon only, label in the tooltip"; }
      }
      lb.setAttribute("data-fit", step);
    });
  }
  // The rule in game units (Noto Sans Medium as Dalamud draws it), measured live: label px = clamp(0.75 x body, 10, 12).
  function labelTable() {
    var t = document.getElementById("lbt");
    if (!t) return;
    var c = document.createElement("canvas").getContext("2d"), rows = "";
    [["Full", 70], ["Quiet", 66]].forEach(function (lvl) {
      [["80 %", 12.8], ["100 %", 16], ["150 %", 24]].forEach(function (ts) {
        var px = Math.max(10, Math.min(12, 0.75 * ts[1])), room = lvl[1] - 6 - 4;
        RAIL.forEach(function (s) {
          if (s[0] !== "Characters") return;
          c.font = "500 " + px + "px 'Noto Sans'";
          var w0 = c.measureText(s[0]).width, w1 = w0 - 0.02 * px * (s[0].length - 1), step;
          if (w0 <= room) step = "fits"; else if (w1 <= room) step = "track -0.02 em"; else if (px * room / w1 >= 10) step = "track, " + (px * room / w1).toFixed(1) + " px"; else step = s[0].indexOf(" ") > 0 ? "two lines" : "icon only";
          rows += "<tr><td>" + lvl[0] + "</td><td>" + ts[0] + "</td><td>" + s[0] + "</td><td>" + px.toFixed(1) + " px</td><td>" + w0.toFixed(1) + "</td><td>" + room + "</td><td><b>" + step + "</b></td></tr>";
        });
      });
    });
    t.innerHTML = "<tr><th>Level</th><th>Text size</th><th>Label</th><th>Size</th><th>Width</th><th>Room</th><th>Step</th></tr>" + rows;
  }

  function renderContrast() {
    var el = document.getElementById("ct16"); if (!el) return;
    var lines = CONTRAST.trim().split(String.fromCharCode(10)).filter(function (l) { return l.indexOf("|---") !== 0; });
    el.innerHTML = "<table>" + lines.map(function (l, i) {
      var cells = l.split("|").slice(1, -1).map(function (c) { return c.trim(); });
      return "<tr>" + cells.map(function (c) {
        if (i === 0) return "<th>" + c + "</th>";
        var m = c.match(/^(#[0-9A-F]{6}) ([0-9.]+)(.*)$/);
        return m ? '<td><i style="display:inline-block;width:10px;height:10px;border-radius:2px;vertical-align:-1px;margin-right:5px;background:' + m[1] + '"></i><code>' + m[1] + "</code> <b>" + m[2] + "</b>" + m[3] + "</td>" : "<td>" + c + "</td>";
      }).join("") + "</tr>";
    }).join("") + "</table>";
  }
  // ---------- Page ----------
  var host = document.getElementById("host"), seg = document.getElementById("seg"), ver = document.getElementById("ver"), pn = document.getElementById("pn");
  function show(hash) {
    var parts = hash.split("/"), v = parts[0], w = parts[1] || "";
    if (!NOTE[v]) v = "full";
    seg.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", b.getAttribute("data-v") === v ? "true" : "false"); });
    ver.style.display = v === "full" || v === "quiet" || v === "plain" ? "" : "none";
    ver.querySelectorAll("button").forEach(function (b) { b.setAttribute("aria-pressed", b.getAttribute("data-w") === w ? "true" : "false"); });
    pn.textContent = NOTE[v];
    if (v === "drawer") host.innerHTML = boardDrawer();
    else if (v === "rail") host.innerHTML = boardRail();
    else if (v === "stars") host.innerHTML = boardStars();
    else if (v === "ba") host.innerHTML = boardBA();
    else if (v === "snow-full") host.innerHTML = snowWin("full");
    else if (v === "snow-full-drawer") host.innerHTML = snowWin("full", { drawer: "c", dopt: { hovRow: 1 } });
    else if (v === "snow-quiet") host.innerHTML = snowWin("quiet", { drawer: "c" });
    else if (v === "snow-plain") host.innerHTML = snowWin("plain", { drawer: "e", dopt: { to: "adv" } });
    else if (v === "palettes") { host.innerHTML = boardPalettes(); renderContrast(); }
    else if (v === "themes") host.innerHTML = boardThemes();
    else if (v === "mix") host.innerHTML = boardMix();
    else if (v === "frames") host.innerHTML = boardFrames();
    else if (v === "share") host.innerHTML = boardShare();
    else if (v === "glyphwin") host.innerHTML = boardGlyphWin();
    else if (v === "palettes17") { host.innerHTML = boardPal17(); renderContrast17(); }
    else if (v === "dawn-full") host.innerHTML = palWin("dawn", "full", "orrery");
    else if (v === "dawn-quiet") host.innerHTML = palWin("dawn", "quiet", "orrery");
    else if (v === "kugane-full") host.innerHTML = palWin("kugane", "full", "medallion");
    else if (v === "kugane-quiet") host.innerHTML = palWin("kugane", "quiet", "medallion");
    else if (v === "giver") host.innerHTML = boardGiver();
    else if (v === "fallbacks") host.innerHTML = boardFallbacks();
    else if (v === "buttons") host.innerHTML = boardButtons();
    else if (v === "sky") host.innerHTML = win("full", { v7: true, h: 790 });
    else host.innerHTML = win(v, { v7: w !== "before", drawer: w === "open" ? "c" : w === "open-adv" ? "e" : null, h: 790 });
    // Expanded drawers on the boards open scrolled to the Advanced section (as after a click on its summary).
    fitLabels(host);
    fitLadders(host);
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(function () { fitLabels(host); fitLadders(host); labelTable(); });
    function toAdvanced() {
      host.querySelectorAll(".dsc-in[data-to=adv]").forEach(function (el) {
        var t = el.querySelectorAll(".dsec")[2], pad = parseFloat(getComputedStyle(el.parentNode).paddingTop) || 0;
        if (t) el.style.transform = "translateY(-" + Math.max(0, t.offsetTop - pad) + "px)";
      });
    }
    toAdvanced();
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(toAdvanced);
    V7 = true;
  }
  seg.addEventListener("click", function (e) { var b = e.target.closest("button"); if (b) location.hash = b.getAttribute("data-v"); });
  ver.addEventListener("click", function (e) { var b = e.target.closest("button"); if (b) { var v = location.hash.slice(1).split("/")[0] || "full", w = b.getAttribute("data-w"); location.hash = v + (w ? "/" + w : ""); } });
  host.addEventListener("click", function (e) {
    var st = e.target.closest(".st7");
    if (st && st.closest(".r7")) {
      var rail = st.closest(".r7"), all = rail.querySelectorAll(".st7"), bead = rail.querySelector(".bead"), k = Array.prototype.indexOf.call(all, st);
      all.forEach(function (s) { s.classList.toggle("on", s === st); });
      if (bead) bead.style.top = ((k + 0.5) * st.offsetHeight - (st.closest(".plain") ? 0 : 8)).toFixed(1) + "px";
      return;
    }
    if (e.target.closest("[data-act=filters]")) { var p = location.hash.slice(1).split("/"); location.hash = (p[0] || "full") + (p[1] === "open" || p[1] === "open-adv" ? "" : "/open"); return; }
    var dh = e.target.closest(".dsec h5");
    if (dh && dh.querySelector(".cv")) { var p2 = location.hash.slice(1).split("/"); location.hash = (p2[0] || "full") + (p2[1] === "open-adv" ? "/open" : "/open-adv"); }
  });
  document.getElementById("shoot").addEventListener("click", function () {
    var m = host.querySelector(".mk.full .shooting");
    if (!m || matchMedia("(prefers-reduced-motion: reduce)").matches) return;
    m.classList.remove("go"); void m.offsetWidth; m.classList.add("go");
  });
  // The moving sky: x1 is the real speed (6 px a minute); x30 previews it. It pauses while this page is unfocused.
  var K = 1;
  function setSpeed() {
    host.querySelectorAll(".drift").forEach(function (d) { d.style.animationDuration = (parseFloat(d.getAttribute("data-w")) / DRIFT_PX_PER_MIN * 60 / K).toFixed(1) + "s"; });
    var b = document.getElementById("spd"); if (b) b.textContent = K === 1 ? "Sky speed: x1" : "Sky speed: x30 (preview)";
  }
  document.getElementById("spd").addEventListener("click", function () { K = K === 1 ? 30 : 1; setSpeed(); });
  function pause(p) {
    host.querySelectorAll(".drift").forEach(function (d) { d.style.animationPlayState = p ? "paused" : "running"; });
    host.querySelectorAll("svg.sky").forEach(function (sv) { if (sv.pauseAnimations) { if (p) sv.pauseAnimations(); else sv.unpauseAnimations(); } });
  }
  window.addEventListener("blur", function () { pause(true); });
  window.addEventListener("focus", function () { pause(false); });
  // A rare faint meteor at rest: every 3 to 6 minutes of focused time (Full, Reduce motion off).
  (function ambient() {
    setTimeout(function () {
      var m = host.querySelector(".mk.full .shooting.faint");
      if (m && document.hasFocus() && !matchMedia("(prefers-reduced-motion: reduce)").matches) { m.classList.remove("go"); void m.offsetWidth; m.classList.add("go"); }
      ambient();
    }, (180 + Math.random() * 180) * 1000 / K);
  })();
  window.addEventListener("hashchange", function () { show(location.hash.slice(1)); setSpeed(); });
  show(location.hash.slice(1) || "full");
})();

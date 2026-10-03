  // ======================================================================================================
  // 1.17 "Mix and match": the mix table (T10), frames (T11), share codes (T12), the glyph window's Themes tab (T13),
  // and the Dawn and Kugane Lacquer palettes (T16). Numbers are the shipped metrics.json files (1.17/mixdata.py).
  // ======================================================================================================
  var MIX = /*MIXDATA*/null;
  var P17 = /*P17*/null;
  var CONTRAST17 = /*CONTRAST17*/"";
  var MR5 = "../../moon-v6/round5/medallion-r5/";
  var SET17 = {
    "medallion": { id: 1, name: "Menphina's Medallion", short: "Medallion", row: MR5 + "_row/", hero: MR5, kit: "brass" },
    "ishgard-glass": { id: 4, name: "Ishgard Glass", short: "Glass", row: TH + "ishgard-glass/_row/", hero: TH + "ishgard-glass/", kit: "came", faces: TH + "ishgard-glass/faces/", fsep: "-" },
    "aether-crystal": { id: 3, name: "Aether Crystal", short: "Aether", row: TH + "aether-crystal/_row/", hero: TH + "aether-crystal/", kit: "silver", faces: TH + "aether-crystal/faces/", fsep: "-" },
    "astrologian-orrery": { id: 5, name: "Astrologian's Orrery", short: "Orrery", row: TH + "astrologian-orrery/_row/", hero: TH + "astrologian-orrery/", kit: "astrolabe", faces: TH + "astrologian-orrery/hero/", fsep: "." }
  };
  var SNAME = { "ready": "Ready", "ready-on-another-job": "Ready on another job", "in-journal": "In journal", "blocked": "Blocked", "done-this-cycle": "Done this cycle", "completed": "Completed", "locked-out": "Locked out", "not-checked": "Not checked" };
  function heroOf(k, st) { return SET17[k].hero + (st === "ready-on-another-job" ? "ready-on-another-job-paladin" : st) + ".svg"; }
  function rowOf(k, st) { return SET17[k].row + st + ".svg"; }

  // ---------- the warning rules (spec-1.17 §A3), the same as 1.17/mixdata.py ----------
  function pairOf(mix, a, b) {
    var A = mix[a], B = mix[b];
    if (!MIX.cross[A] || !MIX.cross[B]) return null;
    if (A === B) { var o = MIX.own[A]; return o[a + "|" + b] != null ? o[a + "|" + b] : o[b + "|" + a]; }
    var c = MIX.cross[A][B]; if (c && c[a + "|" + b] != null) return c[a + "|" + b];
    c = MIX.cross[B][A]; return c ? c[b + "|" + a] : null;
  }
  function evalMix(mix) {
    var close = [], hard = [], S = MIX.states, i, j, unmeasured = false;
    for (i = 0; i < S.length; i++) for (j = i + 1; j < S.length; j++) {
      if (mix[S[i]] === mix[S[j]]) continue;
      var v = pairOf(mix, S[i], S[j]);
      if (v == null) { unmeasured = true; continue; }
      if (v < MIX.bars.mixHard) hard.push([S[i], S[j], v]); else if (v < MIX.bars.mixClose) close.push([S[i], S[j], v]);
    }
    var lead = {}, comp = {};
    [["row", MIX.salience], ["hero", MIX.salienceHero]].forEach(function (t) {
      var T = t[1]; if (!S.every(function (s) { return T[mix[s]]; })) { unmeasured = true; return; }
      var r = T[mix.ready].ready, nx = 0, nxs = null;
      S.forEach(function (s) { if (s !== "ready" && T[mix[s]][s] > nx) { nx = T[mix[s]][s]; nxs = s; } });
      lead[t[0]] = { v: r / nx, next: nxs }; comp[t[0]] = T[mix.completed].completed / r;
    });
    var minLead = Math.min.apply(null, Object.keys(lead).map(function (k) { return lead[k].v; }));
    var maxComp = Math.max.apply(null, Object.keys(comp).map(function (k) { return comp[k]; }));
    return { close: close, hard: hard, lead: lead, comp: comp, minLead: minLead, maxComp: maxComp, unmeasured: unmeasured,
      ok: !close.length && !hard.length && minLead >= MIX.bars.mixReadyLead && maxComp <= MIX.bars.completedOfReady };
  }
  function fixMix(mix, keep) {
    var best = null;
    MIX.states.forEach(function (st, si) {
      if (st === keep) return;
      MIX.sets.forEach(function (k) {
        if (k === mix[st]) return;
        var m2 = Object.assign({}, mix); m2[st] = k;
        if (!evalMix(m2).ok) return;
        var cover = MIX.states.filter(function (x) { return m2[x] === k; }).length, key = [-cover, si];
        if (!best || key[0] < best.key[0] || (key[0] === best.key[0] && key[1] < best.key[1])) best = { key: key, state: st, set: k };
      });
    });
    return best;
  }
  var EXMIX = MIX.example.mix;

  // ---------- frames: one kit per look (T11) ----------
  var KITS = {
    brass: { name: "Brass", note: "gilt brass, lit upper left", ramp: ["#E2C78C", "#A88B52", "#6E5732", "#5A4729"], dir: null },
    silver: { name: "Silver", note: "moonstone silver", ramp: ["#E2E8F4", "#A9B5D0", "#7B8AAF", "#5E6E97"], dir: TH + "aether-crystal/kit/" },
    came: { name: "Lead came", note: "Ishgard lead, a gilt inner line on act-now", ramp: ["#B8C0D0", "#8C95B0", "#5A6278", "#323950"], dir: TH + "ishgard-glass/kit/" },
    astrolabe: { name: "Astrolabe", note: "two-tone instrument brass, a scale at hero", ramp: ["#EAD3A0", "#B8924E", "#7C6034", "#4E3B1E"], dir: TH + "astrologian-orrery/kit/" }
  };
  var TIER = { "ready": "act-now", "completed": "finished", "not-checked": "ghost" };
  var BADGE = { "ready": "open", "in-journal": "journal", "blocked": "closed" };
  // A face framed in a kit, drawn as the plugin layers it: face under, the kit's frame for the state's urgency tier,
  // face over, then the badge seat and glyph. Brass has no separate frame file in the design folders, so it is cut
  // from Medallion's shipped composite (the ring outside the shared well, r 52.4) with an SVG mask.
  // Brass's badge (Medallion r5): a night gap r 24, an enamel seat r 19.9 in a brass ring, the glyph at text height.
  function brassBadge(st) {
    var id = "bb" + (++uid);
    return '<svg viewBox="0 0 128 128"><defs><linearGradient id="' + id + '" x1="76" y1="76" x2="114" y2="114" gradientUnits="userSpaceOnUse"><stop offset="0" stop-color="#E6CF98"/><stop offset=".5" stop-color="#9A7E4A"/><stop offset="1" stop-color="#5C4724"/></linearGradient></defs>' +
      '<circle cx="95" cy="95" r="24" fill="#080B16"/><circle cx="95" cy="95" r="21.6" fill="url(#' + id + ')"/><circle cx="95" cy="95" r="19.9" fill="#1C2752"/>' +
      '<image href="' + MR5 + "_row/badge-" + BADGE[st] + '.svg" x="78" y="78" width="34" height="34"/></svg>';
  }
  // light: on a light palette every kit keeps the 1 px Abyss outer keyline at .6 (the supervisor's ruling for Silver on Snow)
  function framed(faceSet, kit, st, px, noBadge, light) {
    var F = SET17[faceSet], K = KITS[kit], tier = TIER[st] || "resting", L = [];
    if (kit === "brass" && faceSet === "astrologian-orrery" && !noBadge) return '<img src="' + TH + "astrologian-orrery/_mix/orrery-in-brass/" + st + '.svg" width="' + px + '" height="' + px + '">';
    if (!F.faces) return '<img src="' + heroOf(faceSet, st) + '" width="' + px + '" height="' + px + '">';
    var under = F.faces + st + (F.fsep === "." ? ".svg" : "-under.svg"), over = F.faces + st + (F.fsep === "." ? ".over.svg" : "-over.svg");
    L.push(under);
    var ring = "";
    if (K.dir) L.push(K.dir + "frame-" + tier + "-full.svg");
    else { var id = "bm" + (++uid); ring = '<svg viewBox="0 0 128 128" style="position:absolute;inset:0;width:100%;height:100%"><defs><mask id="' + id + '"><rect width="128" height="128" fill="#000"/><circle cx="64" cy="64" r="64" fill="#fff"/><circle cx="64" cy="64" r="52.4" fill="#000"/></mask></defs><image href="' + MR5 + (st === "completed" ? "completed" : st === "not-checked" ? "not-checked" : "done-this-cycle") + '.svg" width="128" height="128" mask="url(#' + id + ')"/></svg>'; }
    var after = [over];
    if (BADGE[st] && K.dir && !noBadge) {
      after.push(K.dir + "badge-seat-" + BADGE[st] + ".svg");
      if (kit !== "silver") after.push(K.dir + (kit === "came" ? "glyph-" : "badge-") + BADGE[st] + ".svg");
    }
    // Silver's badge glyphs are drawn in a 34-unit box centred on the seat (95, 95), as the kit's compose() places them
    var sg = (BADGE[st] && kit === "silver" && !noBadge) ? '<img src="' + K.dir + "badge-" + BADGE[st] + '.svg" style="left:' + (78 / 128 * 100) + "%;top:" + (78 / 128 * 100) + "%;width:" + (34 / 128 * 100) + "%;height:" + (34 / 128 * 100) + '%">' : "";
    return '<span class="fm" style="width:' + px + "px;height:" + px + 'px">' + L.map(function (x) { return '<img src="' + x + '">'; }).join("") + ring + after.map(function (x) { return '<img src="' + x + '">'; }).join("") + sg + (kit === "brass" && BADGE[st] && !noBadge ? brassBadge(st) : "") + (light ? '<svg viewBox="0 0 128 128"><circle cx="64" cy="64" r="63.4" fill="none" stroke="#080B16" stroke-opacity=".6" stroke-width="1" vector-effect="non-scaling-stroke"/></svg>' : "") + "</span>";
  }

  // ---------- the Themes page in 1.17 ----------
  var THEMES17 = [
    { k: "medallion", name: "Menphina's Medallion", sub: "Brass frames · Night", pal: "night", dir: MR5 + "_row/" },
    { k: "glass", name: "Ishgard Glass", sub: "Came frames · Ishgard Snow", pal: "snow", dir: TH + "ishgard-glass/_row/" },
    { k: "aether", name: "Aether Crystal", sub: "Silver frames · Night", pal: "night", dir: TH + "aether-crystal/_row/" },
    { k: "classic", name: "Classic", sub: "The 1.11 moons · Night", pal: "night", legacy: true },
    { k: "orrery", name: "Astrologian's Orrery", sub: "Astrolabe frames · Dawn", pal: "dawn", dir: TH + "astrologian-orrery/_row/", fresh: true }
  ];
  function mixRow(st, mix, theme, o) {
    o = o || {};
    var k = mix[st], from = k === theme;
    // the look's one kit frames every face (Brass for a Medallion look); at 32 px the row tier, so no badges
    var med = k === "medallion" ? '<img src="' + rowOf(k, st) + '" width="32" height="32">' : framed(k, SET17[theme].kit, st, 32, true);
    var h = '<div class="mx7' + (o.focus ? " focus" : "") + '"><span class="mn">' + SNAME[st] + '</span><span class="mm">' + med + "</span>" +
      '<span class="dd7' + (o.open ? " open" : "") + '"><img src="' + rowOf(k, st) + '" width="18" height="18"><span>' + (from ? "From theme" : SET17[k].name) + "</span><i>▾</i></span>";
    h += '<span class="nt">' + (o.note ? '<i class="ad"></i>' + o.note : "") + "</span></div>";
    return h;
  }
  function comboList(st, mix, theme) {
    var opts = [["", "From theme", theme]].concat(MIX.sets.map(function (k) { return [k, SET17[k].name, k]; }));
    return '<div class="cl7">' + opts.map(function (op) {
      var m2 = Object.assign({}, mix); m2[st] = op[2];
      var e = MIX.salience[op[2]] ? evalMix(m2) : null, e0 = evalMix(mix), note = "";
      // an option says only what it would add: a new close or hard pair, or Ready losing its lead
      if (e && !e.ok && e0.ok) note = e.hard.length ? "hard to tell apart in a list" : e.close.length ? "close to " + SNAME[e.close[0][0] === st ? e.close[0][1] : e.close[0][0]] + " in a list" : "Ready would stop leading at large sizes";
      var on = (op[0] === "" && mix[st] === theme) || (op[0] && op[0] === mix[st] && mix[st] !== theme);
      return '<div class="co' + (on ? " on" : "") + '"><img src="' + rowOf(op[2], st) + '" width="20" height="20"><span>' + op[1] + (op[0] === "" ? ' <em>' + SET17[theme].name + "</em>" : "") + "</span>" + (note ? '<b><i class="ad"></i>' + note + "</b>" : op[0] === "astrologian-orrery" ? "<b class=\"new\">new in 1.17</b>" : "") + "</div>";
    }).join("") + "</div>";
  }
  function mixSection(mode) {
    // mode: "warn" (Ready from Aether Crystal: the hero lead fails), "fix" (the Fix it proposal), "hold" (Reset mix held), "combo"
    var theme = "medallion", mix = Object.assign({}, EXMIX), e = evalMix(mix), fx = fixMix(mix, "ready");
    if (mode === "fixed" || mode === "hold" || mode === "combo") { mix[fx.state] = fx.set; e = evalMix(mix); }
    var h = '<section class="new17 mix7"><h4>Mix moons by state</h4><p class="lead7">Pick each state\'s moon from any set. The frames stay one kit (Frames above), so the column keeps one metal.</p><div class="mixt7">';
    MIX.states.forEach(function (st) {
      var note = "";
      if (!e.ok && st === "ready") note = "quieter than Completed at large sizes";
      h += mixRow(st, mix, theme, { note: note, open: mode === "combo" && st === "completed", focus: (mode === "fix" && st === fx.state) });
      if (mode === "combo" && st === "completed") h += comboList("completed", mix, theme);
    });
    h += "</div>";
    if (!e.ok) {
      var L = e.lead.hero, C = e.comp.hero;
      h += '<div class="ws7"><span class="wl"><i class="ad"></i>Ready is no longer the loudest moon at large sizes.</span><span class="qa">Details ▾</span><span class="qa pri7">Fix it</span><span class="qa hold7">Reset mix</span></div>';
      h += '<div class="wd7">At 48 px and up, ' + SET17[mix.ready].name + "'s Ready leads the column by <b>" + L.v.toFixed(2) + "×</b> (it should lead by 1.25× or more), and " + SET17[mix.completed].name + "'s Completed is <b>" + C.toFixed(2) + "×</b> Ready (it should stay at 0.80× or less). In rows it still leads (" + e.lead.row.v.toFixed(2) + "×).</div>";
      if (mode === "fix") {
        var m2 = Object.assign({}, mix); m2[fx.state] = fx.set; var e2 = evalMix(m2);
        h += '<div class="fp7"><div class="fpt">Fix it suggests one change</div><div class="fpr"><img src="' + heroOf(mix[fx.state], fx.state) + '" width="40"><span class="ar">→</span><img src="' + heroOf(fx.set, fx.state) + '" width="40"><div><b>Use ' + SET17[fx.set].name + " for " + SNAME[fx.state] + ' too</b><span>Ready leads again: ' + e2.lead.hero.v.toFixed(2) + "× at large sizes, " + e2.lead.row.v.toFixed(2) + "× in rows. Your Ready pick stays.</span></div></div>" +
          '<div class="fpb"><span class="qa">Not now</span><span class="qa pri7">Use it</span></div></div>';
      }
    } else {
      h += '<div class="ws7 calm"><span class="wl">Every moon reads apart, and Ready leads (' + e.minLead.toFixed(2) + "×).</span>" + '<span class="qa hold7' + (mode === "hold" ? " holding" : "") + '">' + (mode === "hold" ? '<svg class="harc" viewBox="0 0 100 32" preserveAspectRatio="none"><rect x=".75" y=".75" width="98.5" height="30.5" rx="15" fill="none" stroke="#2E3550" stroke-width="1.5"/><rect x=".75" y=".75" width="98.5" height="30.5" rx="15" fill="none" stroke="#F2D27A" stroke-width="1.5" pathLength="100" stroke-dasharray="62 100"/></svg>Reset mix' : "Reset mix") + "</span></div>";
    }
    return h + "</section>";
  }
  function themesPage17(mode, extra) {
    var nav = ["General", "Themes", "Journal", "Travel", "In-game panels", "Overlay", "Alerts", "Companions", "Data", "Advanced"];
    var h = '<div class="mk full v7 set16 set17"><div class="win"><div class="w-title"><span class="tri"></span>Tsukimichi Settings<span class="wc">' + ICON.x + '</span></div><div class="sbody"><nav class="snav"><div class="ssearch">' + ICON.search + "<span>Search settings</span></div>" +
      nav.map(function (n) { return '<div class="sn' + (n === "Themes" ? " on" : "") + '">' + n + "</div>"; }).join("") + '</nav><div class="spage">';
    h += extra || "";
    h += mixSection(mode);
    return h + "</div></div></div></div>";
  }
  function boardMix() {
    var h = '<div class="board b15 b16"><h2>1.17 · Mix moons by state<small>Real numbers from the shipped metrics.json (row tier 16 px and the 48 px hero tier, Night). Ready from Aether Crystal in a Menphina\'s Medallion look: in rows Ready still leads (1.30×), at 48 px and up it does not (1.23×). Fix it keeps the player\'s pick and changes one other state.</small></h2><div class="row" style="align-items:flex-start">';
    h += fig('<div class="clip7" style="height:760px">' + themesPage17("warn") + "</div>", "<b>A warning</b>, in words, with its reason under Details");
    h += fig('<div class="clip7" style="height:760px">' + themesPage17("fix") + "</div>", "<b>Fix it</b>: the smallest change that clears every warning");
    h += "</div><div class=\"row\" style=\"align-items:flex-start\">";
    h += fig('<div class="clip7" style="height:750px">' + themesPage17("combo") + "</div>", "<b>A state's list</b> (after the fix): every offered set with that state's moon; an option that would add a warning says so before it is picked");
    h += fig('<div class="clip7" style="height:600px">' + themesPage17("hold") + "</div>", "<b>Reset mix</b> after the fix: no warning; a hold (the Hold tier, 0.6 s by default) with the Moon arc closing; Undo follows");
    return h + "</div></div>";
  }

  // ---------- frames on each palette (T11) ----------
  var PALW = {
    night: { name: "Night", bg: "linear-gradient(180deg,#1B2552,#0F1424)", win: "#0F1424", card: "#1E2437", text: "#DDE3F0", t2: "#A9B2CC", light: false },
    snow: { name: "Ishgard Snow", bg: "linear-gradient(180deg,#D3DEF0,#EEF1F6 70%)", win: "#EEF1F6", card: "#F9FAFC", text: "#1A2136", t2: "#434D6A", light: true },
    dawn: { name: "Dawn", bg: "linear-gradient(180deg,#3A2746,#2B1F3A 40%,#1A1526)", win: "#1A1526", card: "#262036", text: "#F2E8E6", t2: "#C4B4C0", light: false },
    kugane: { name: "Kugane Lacquer", bg: "linear-gradient(180deg,#3A1A14,#2A1613 40%,#16100F)", win: "#16100F", card: "#231917", text: "#F3E9DB", t2: "#C6B6A2", light: false }
  };
  function kitCard(kit, pal) {
    var K = KITS[kit], P = PALW[pal], r = K.ramp;
    var edge = "linear-gradient(160deg," + r[0] + " 0%," + r[1] + " 30%," + r[2] + " 62%," + r[1] + " 80%," + r[3] + " 100%)";
    var corner = { brass: "L", silver: "chip", came: "quatrefoil", astrolabe: "scale" }[kit];
    return '<div class="kc7 ' + corner + '" style="--e:' + edge + ";--c1:" + r[0] + ";--c3:" + r[3] + ";background:linear-gradient(" + P.card + "," + P.card + ") padding-box," + edge + ' border-box"><i class="k1"></i><i class="k2"></i><h5 style="color:' + (P.light ? "#3F4862" : r[0]) + '">Requirements</h5><div style="color:' + P.t2 + '">Level 54 · met</div></div>';
  }
  function boardFrames() {
    var faces = "astrologian-orrery", sts = ["ready", "in-journal", "blocked", "completed", "not-checked"];
    var h = '<div class="board b15 b16"><h2>1.17 · Frames<small>One kit per look: the medals\' frames and badges, the gauges and the Decoration ornament all come from it. Here the Orrery faces sit in each kit on each palette. Act now (Ready) is gilt in every kit; resting, finished and ghost take the kit\'s own metal.</small></h2>';
    h += '<div class="fg7"><div></div>' + ["night", "snow", "dawn", "kugane"].map(function (p) { return '<div class="fh">' + PALW[p].name + "</div>"; }).join("");
    Object.keys(KITS).forEach(function (kit) {
      h += '<div class="fk"><b>' + KITS[kit].name + "</b><span>" + KITS[kit].note + '</span><div class="rmp">' + KITS[kit].ramp.map(function (c) { return '<i style="background:' + c + '"></i>'; }).join("") + "</div></div>";
      ["night", "snow", "dawn", "kugane"].forEach(function (p) {
        var P = PALW[p];
        h += '<div class="ft" style="background:' + P.bg + '"><div class="fmr">' + sts.map(function (s) { return framed(faces, kit, s, 52, false, PALW[p].light); }).join("") + "</div>" + kitCard(kit, p) + "</div>";
      });
    });
    return h + "</div></div>";
  }

  // ---------- share codes (T12) ----------
  function shareBox(state) {
    var h = '<div class="sh7">';
    if (state === "copy") h += '<div class="shr7"><code>TM1-202C-000C-02C</code><span class="qa">Copy</span><span class="pin7">Paste a code</span></div><div class="tst7">Code copied · TM1-202C-000C-02C</div>';
    if (state === "paste") h += '<div class="shr7"><code>TM1-202C-000C-02C</code><span class="qa">Copy</span><span class="pin7 v">tm1 ad24 0000 028</span></div>' +
      '<div class="pv7"><div class="pvt">This code would change:</div><ul><li><b>Theme</b> Menphina\'s Medallion → <b>Astrologian\'s Orrery</b></li><li><b>Palette</b> Night → <b>Dawn</b></li><li><b>Frames</b> Brass → <b>Astrolabe</b></li><li><b>Ready</b> from ' + "Menphina's Medallion" + '</li></ul><div class="pvm">' + ["ready", "in-journal", "blocked", "completed", "not-checked"].map(function (s) { return '<img src="' + (s === "ready" ? TH + "astrologian-orrery/_mix/medallion-in-astrolabe/ready.svg" : heroOf("astrologian-orrery", s)) + '" width="36">'; }).join("") + '</div><div class="fpb"><span class="qa">Cancel</span><span class="qa pri7">Apply</span></div></div>';
    if (state === "newer") h += '<div class="shr7"><code>TM1-202C-000C-02C</code><span class="qa">Copy</span><span class="pin7 v">TM1-2034-000C-0DJ</span></div>' +
      '<div class="pv7"><div class="pvt">This code would change:</div><ul><li><b>Completed</b> from <b>Aether Crystal</b></li></ul><div class="nw7">One pick comes from a newer Tsukimichi (Ready: a set this version doesn\'t have). It stays From theme; the rest applies.</div><div class="fpb"><span class="qa">Cancel</span><span class="qa pri7">Apply the rest</span></div></div>';
    if (state === "bad") h += '<div class="shr7"><code>TM1-202C-000C-02C</code><span class="qa">Copy</span><span class="pin7 v">TM1-202C-000X-02C</span></div><div class="er7">That code doesn\'t read: a character may be mistyped. Nothing was changed.</div>';
    if (state === "applied") h += '<div class="shr7"><code>TM1-AD24-0000-028</code><span class="qa">Copy</span><span class="pin7">Paste a code</span></div><div class="tst7">Look applied · <span class="u">Undo</span></div>';
    return h + "</div>";
  }
  function boardShare() {
    var cap = { copy: "<b>Copy</b>: the current look's code; a quiet toast", paste: "<b>Paste</b>: read as you type; a preview of every change before anything applies", newer: "<b>From a newer version</b>: unknown sets are named and left out; the rest still applies", bad: "<b>A typo</b>: the checksum catches every single-character typo; nothing changes", applied: "<b>Applied</b>: the look changes at once; Undo for 8 s" };
    return '<div class="board b15 b16"><h2>1.17 · Share codes<small>TM1- then base32 (Crockford): 6 characters for a theme, 12 for a full mix. Case, spaces and dashes don\'t matter; O reads 0 and I or L read 1. The section sits under Mix on the Themes page; codes carry ids only.</small></h2><div class="row" style="align-items:flex-start">' +
      ["copy", "paste", "newer", "bad", "applied"].map(function (s) { return fig('<div class="mk full v7 set16 shb7"><div class="win"><div class="spage" style="padding:16px 18px"><section class="new17" style="margin-top:0"><h4>Share</h4>' + shareBox(s) + "</section></div></div></div>", cap[s]); }).join("") + "</div></div>";
  }

  // ---------- the glyph window's Themes tab (T13) ----------
  function heat(v) {
    if (v == null) return "#2A3149";
    if (v < MIX.bars.mixHard) return "#8E3A5E"; if (v < 11) return "#B0645A"; if (v < MIX.bars.mixClose) return "#C9A866";
    var t = Math.min(1, (v - 12) / 30); return "rgb(" + Math.round(46 + t * 10) + "," + Math.round(64 + t * 40) + "," + Math.round(96 + t * 50) + ")";
  }
  function heatTable(mix, mode, title) {
    var S = MIX.states, sh = ["Rdy", "RoJ", "Jrn", "Blk", "Done", "Comp", "Lock", "NotC"];
    var h = '<div class="ht7"><div class="htt">' + title + '</div><table><tr><th></th>' + sh.map(function (x) { return "<th>" + x + "</th>"; }).join("") + "</tr>";
    S.forEach(function (a, i) {
      h += "<tr><th>" + sh[i] + "</th>";
      S.forEach(function (b, j) {
        if (j >= i) { h += '<td class="e"></td>'; return; }
        var v = pairOf(mix, a, b), same = mix[a] === mix[b], col = heat(v);
        if (same && v != null) {
          // a set's own pair passed its build gates: 12 in greyscale and deuteranopia, 11 under Machado CVD
          var M = MIX.ownModes[mix[a]], g = function (md) { var o = M[md] || {}; return o[a + "|" + b] != null ? o[a + "|" + b] : o[b + "|" + a]; };
          var r1 = function (x) { return Math.round(x * 10) / 10; }; // judged at one decimal, as the build judges
          var okG = r1(Math.min(g("grey"), g("deut"))) >= 12, okC = r1(Math.min(g("machado-prot"), g("machado-deut"), g("machado-trit"))) >= 11;
          col = okG && okC ? heat(Math.max(v, 12)) : heat(v);
        }
        h += '<td style="background:' + col + '"' + (!same ? ' class="x"' : "") + ">" + (v == null ? "–" : v.toFixed(1)) + "</td>";
      });
      h += "</tr>";
    });
    return h + "</table></div>";
  }
  function lookPanel(label, mix, pal, kitName, kit) {
    var P = PALW[pal], rows = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"];
    return '<div class="lp7"><div class="lph"><b>' + label + "</b><span>" + kitName + "</span></div>" +
      '<div class="lpb" style="background:' + P.bg + ";color:" + P.text + '"><div class="hr7">' + rows.map(function (s) { return mix[s] === "medallion" && kit === "brass" ? '<img src="' + heroOf("medallion", s) + '" width="40">' : framed(mix[s], kit, s, 40); }).join("") + '</div><div class="rr7">' + rows.map(function (s) { return '<div><img src="' + rowOf(mix[s], s) + '" width="16" height="16"><span>' + SNAME[s] + "</span></div>"; }).join("") + "</div></div></div>";
  }
  function boardGlyphWin() {
    var A = Object.assign({}, EXMIX), B = Object.assign({}, A); B.completed = "aether-crystal";
    var eA = evalMix(A), eB = evalMix(B);
    var h = '<div class="board b15 b16"><h2>1.17 · Glyph window › Themes<small>For reviewers and curious players (/tsukimichi glyphs). Two looks side by side, then how alike every pair of moons is: the shipped metrics, row tier at 16 px on Night, as a heat table. Cross-set pairs are outlined. Numbers are round 5\'s units; 12 and 11 are the bars.</small></h2>';
    h += '<div class="mk full v7 gw7"><div class="win"><div class="w-title"><span class="tri"></span>Tsukimichi Glyphs<span class="wc">' + ICON.x + '</span></div><div class="gwb"><div class="gtabs"><span>Medals</span><span>Palettes</span><span>Gauges</span><span class="on">Themes</span></div>';
    h += '<div class="gpk"><div><b>Look A</b><span class="dd7"><span>Medallion · Ready from Aether</span><i>▾</i></span><span class="dd7"><span>Night</span><i>▾</i></span></div><div><b>Look B</b><span class="dd7"><span>Look A + Completed from Aether</span><i>▾</i></span><span class="dd7"><span>Night</span><i>▾</i></span></div><div class="cvd"><b>Vision</b>' + seg16(["Worst", "Grey", "Deut", "Prot", "Trit"], 0) + "</div></div>";
    h += '<div class="lps">' + lookPanel("A", A, "night", "Brass frames (one kit for the look)", "brass") + lookPanel("B", B, "night", "Brass frames", "brass") + "</div>";
    h += '<div class="hts">' + heatTable(A, "worst", "A · how alike, worst of every vision mode") + heatTable(B, "worst", "B") +
      '<div class="spec" style="width:300px"><b>Ready\'s lead</b><table><tr><th></th><th>A</th><th>B</th></tr><tr><td>Rows, 16 px</td><td>' + eA.lead.row.v.toFixed(2) + "×</td><td>" + eB.lead.row.v.toFixed(2) + "×</td></tr><tr><td>48 px and up</td><td><b style=\"color:#C9A866\">" + eA.lead.hero.v.toFixed(2) + "×</b></td><td>" + eB.lead.hero.v.toFixed(2) + "×</td></tr><tr><td>Completed of Ready, 48 px</td><td><b style=\"color:#C9A866\">" + eA.comp.hero.toFixed(2) + "</b></td><td>" + eB.comp.hero.toFixed(2) + '</td></tr></table><div class="lg7"><span><i style="background:' + heat(20) + '"></i>reads apart</span><span><i style="background:' + heat(11.5) + '"></i>close (a cross-set pair under its bar: 12 grey, 11 prot, deut or trit)</span><span><i style="background:' + heat(9) + '"></i>hard to tell apart (under 10 in any mode)</span><span>A pair within one set is coloured by the gates that set passed (12 grey, 11 colour vision), so the Medallion pair Blk–Lock at 11.1 under protanopia reads apart.</span></div></div></div>';
    return h + "</div></div></div></div>";
  }

  // ---------- Dawn and Kugane Lacquer (T16) ----------
  function boardPal17() {
    var keys = [["dawn", "Dawn", "dark · the hour before sunrise"], ["dawn-hc", "Dawn · High contrast", "no sky, inks at 7 : 1"], ["kugane", "Kugane Lacquer", "dark · black lacquer at dusk"], ["kugane-hc", "Kugane Lacquer · High contrast", "no sky, inks at 7 : 1"]];
    var h = '<div class="board b15 b16"><h2>1.17 · Dawn and Kugane Lacquer<small>Both are dark palettes: stars stay on (dark-palette feature), glows stay light, portraits use the night grade. Medals are never recoloured.</small></h2><div class="row">';
    keys.forEach(function (k) {
      var p = P17.palettes[k[0]], names = ["Window", "Sunken", "Raised", "Hover", "Line", "StrongLine"];
      var sky = k[0].indexOf("hc") > 0 ? p.Window : "linear-gradient(180deg," + p.Zenith + " 0%," + p.Horizon + " 34%," + p.Top + " 52%," + p.Window + " 80%)";
      h += '<div class="pc16" style="--w:' + p.Window + ";--r:" + p.Raised + ";--t:" + p.Text + ";--t2:" + p.TextSecondary + ";--t3:" + p.TextTertiary + ";--ln:" + p.Line + '"><div class="pch"><b>' + k[1] + "</b><span>" + k[2] + '</span></div><div class="sw">' +
        names.map(function (n) { return '<div><i style="background:' + p[n] + '"></i><span>' + n + "<br><code>" + p[n] + "</code></span></div>"; }).join("") + '</div><div class="pan" style="background:' + sky + '">' +
        (k[0].indexOf("hc") > 0 ? "" : stars13(k[0] === "dawn" ? 41 : 43, 410, 196, 14, .5, 0, 0, 1)) +
        '<div class="pcard"><h5 style="color:' + p.OrnamentLight + '">Requirements</h5><div class="tl" style="color:' + p.Text + '">Text · Level 54, you are 100</div><div class="tl" style="color:' + p.TextSecondary + '">Secondary · Ishgard · The Pillars</div><div class="tl" style="color:' + p.TextTertiary + '">Tertiary · Checked 5 min ago</div>' +
        '<div class="tl"><span style="color:' + p.Ready + ';font-weight:600">Ready</span> · <span style="color:' + p.Completed + '">Completed</span> · <span style="color:' + p.LockedOut + '">Locked out</span> · <span style="color:' + p.NotChecked + '">Not checked</span> · <u style="color:' + p.Cool + '">Blood Drain</u></div>' +
        '<div class="mr">' + MIX.states.map(function (s) { return '<img src="' + rowOf(k[0].indexOf("dawn") === 0 ? "astrologian-orrery" : "medallion", s) + '" width="20" height="20">'; }).join("") + '<span class="gp16">Go to giver</span></div></div></div></div>';
    });
    h += '</div><div class="row"><div class="spec" id="ct17" style="width:1000px"></div><div class="spec" style="width:520px"><b>Tuned from research §8.2</b> (only where the worst surface missed the bar):<br>Dawn: ' + P17.tuned.dawn + "<br><br>Kugane Lacquer: " + P17.tuned.kugane + "<br><br><b>Accent against Locked out</b>, worst OKLab ΔE under Machado protan, deutan and tritan: Dawn " + P17.cvd.dawn + ", Kugane " + P17.cvd.kugane + " (Night " + P17.cvd.night + "; the gate is 0.08).</div></div></div>";
    return h;
  }
  function renderContrast17() {
    var el = document.getElementById("ct17"); if (!el) return;
    var lines = CONTRAST17.trim().split(String.fromCharCode(10)).filter(function (l) { return l.indexOf("|---") !== 0; });
    el.innerHTML = "<table>" + lines.map(function (l, i) {
      var cells = l.split("|").slice(1, -1).map(function (c) { return c.trim(); });
      return "<tr>" + cells.map(function (c) {
        if (i === 0) return "<th>" + c + "</th>";
        var m = c.match(/^(#[0-9A-F]{6}) ([0-9.]+)(.*)$/);
        return m ? '<td><i style="display:inline-block;width:10px;height:10px;border-radius:2px;vertical-align:-1px;margin-right:5px;background:' + m[1] + '"></i><code>' + m[1] + "</code> <b>" + m[2] + "</b>" + m[3] + "</td>" : "<td>" + c + "</td>";
      }).join("") + "</tr>";
    }).join("") + "</table>";
  }
  function palWin(pal, lv, theme) { return win(lv, { v7: true, palette: pal, theme: theme, tips: false, h: 790 }).replace("v1.14.0", "v1.17.0"); }

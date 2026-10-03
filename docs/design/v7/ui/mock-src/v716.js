  // ======================================================================================================
  // 1.16 "Themes": the Ishgard Snow palette (T8) and the Settings › Themes page (T9)
  // ======================================================================================================
  Q.splice(1, 0, { g: "Main Scenario", st: "completed", n: "A General Summons", lv: 54, job: "All", lab: "Completed", det: "2025-01-02", xp: "HW" });
  var TH = "../themes/";
  // The selected quest of the Snow renders: an Ishgard quest Tataru gives (Lumina: Quest 67166, IssuerStart Tataru).
  var D16 = { n: "A General Summons", st: "completed", lv: 54, xp: "HW", path: "Main Scenario (Heavensward) › Heavensward", det: "2025-01-02", place: "Ishgard · The Pillars" };
  function detailSnow(lv, o) {
    var q = D16, h = '<aside class="det"><div class="det-in"><div class="det-sc">';
    var reqs16 = [["ok", "Level", "54, you are 100"], ["ok", "Previous quest", "Into the Aery"], ["ok", "Class or job", "any Disciple of War or Magic"]];
    var rq = reqs16.map(function (x) { return '<div class="req ok"><span class="ico">' + ICON.check + '</span><span class="lab">' + x[1] + '</span><span class="d">' + x[2] + "</span></div>"; }).join("");
    var giver = '<div class="gv">' + face(lv === "full" ? 72 : lv === "quiet" ? 64 : 18, lv, "tataru") + '<div class="gt"><b>Tataru Taru</b><span>' + q.place + "</span></div></div>";
    if (lv === "full") {
      h += '<div class="ban snowban"><div class="art"><div class="img" style="background-image:url(\'' + SCENE.day + '\');background-position:50% 45%"></div></div><i class="scrim"></i>' +
        // The location line fits one line by the label ladder: the full path, then the path without its "Main Scenario (…) ›"
        // prefix, then the level alone. Never an ellipsis, never a wrap.
        '<div class="t"><div class="n">' + esc(q.n) + '</div><div class="m" data-ladder="' + esc(JSON.stringify([q.path + " · Lv " + q.lv, q.path.replace(/^.*› /, "") + " · Lv " + q.lv, "Lv " + q.lv])) + '">' + esc(q.path) + " · Lv " + q.lv + "</div></div>" +
        '<div class="hm">' + medalImg(q.st, 80, true) + "</div></div>";
      h += '<div class="hero"><div class="hl st-comp">Completed</div><div class="hd">' + q.det + "</div></div>";
      h += '<div class="mrd"><i></i><b></b><i></i></div>';
      h += card(lv, '<h4>Requirements<span class="r">All met</span></h4>' + rq);
      h += card(lv, "<h4>Giver</h4>" + giver);
      h += card(lv, '<h4>Rewards</h4><div class="rwline">17,550 EXP · 1,034 gil</div>');
    } else if (lv === "quiet") {
      h += '<div class="qh"><div class="n">' + esc(q.n) + '</div><div class="m">' + esc(q.path) + " · Lv " + q.lv + "</div></div>";
      h += '<div class="hero">' + quietMedal(q.st, 52, true) + '<div><div class="hl st-comp">Completed</div><div class="hd">' + q.det + "</div></div></div>";
      h += card(lv, '<h4>Requirements<span class="r">All met</span></h4>' + rq);
      h += card(lv, "<h4>Giver</h4>" + giver);
      h += card(lv, '<h4>Rewards</h4><div class="rwline">17,550 EXP · 1,034 gil</div>');
    } else {
      h += '<div class="ph"><div class="n">' + esc(q.n) + '</div><div class="m">' + esc(q.path) + "</div></div>";
      h += '<dl class="kv"><dt>State</dt><dd>' + flatGlyph(q.st, 12) + '<span class="st-comp">Completed</span><span class="mu">· ' + q.det + "</span></dd>" +
        "<dt>Level</dt><dd>" + q.lv + ' <span class="mu">· any job · HW</span></dd><dt>Giver</dt><dd>' + face(18, "plain", "tataru") + '<span>Tataru Taru</span><span class="mu">· Ishgard</span></dd></dl>';
      h += '<div class="ps">Requirements<span>all met</span></div>' + rq;
      h += '<div class="ps">Rewards<span></span></div><div class="rl">17,550 EXP · 1,034 gil</div>';
    }
    h += "</div></div>" + actions(lv) + "</aside>";
    return h;
  }
  function snowWin(lv, extra) {
    return win(lv, Object.assign({ v7: true, theme: "glass", palette: "snow", sel: "A General Summons", detailFn: detailSnow, tips: false, h: 790 }, extra || {})).replace("v1.14.0", "v1.16.0");
  }

  // ---------- Palette sheet ----------
  var PAL = {
    night: { name: "Night", sub: "today's palette", surf: ["#0F1424", "#0B0F1C", "#1E2437", "#262D45", "#2A3149", "#646D8A"], text: ["#DDE3F0", "#A9B2CC", "#8B94B3"], accent: "#F2D27A", cool: "#6F8FD0", lock: "#D68AA8", nc: "#8A93B0", orn: "#E6CF98", dark: true },
    nighthc: { name: "Night · High contrast", sub: "no sky, strong lines, inks at 7 : 1", surf: ["#0F1424", "#0B0F1C", "#1E2437", "#262D45", "#2A3149", "#7C86A8"], text: ["#DDE3F0", "#C3CBDF", "#A0A9C4"], accent: "#F2D27A", cool: "#86A1D7", lock: "#D68AA8", nc: "#97A0BA", orn: "#E6CF98", dark: true, hc: true },
    snow: { name: "Ishgard Snow", sub: "the first light palette", surf: ["#EEF1F6", "#E1E6EE", "#F9FAFC", "#DCE3ED", "#CAD2DF", "#7A859C"], text: ["#1A2136", "#434D6A", "#56607C"], accent: "#755308", cool: "#2C569E", lock: "#962A6A", nc: "#56607C", orn: "#3F4862", dark: false },
    snowhc: { name: "Ishgard Snow · High contrast", sub: "no sky, strong lines, inks at 7 : 1", surf: ["#EEF1F6", "#E1E6EE", "#F9FAFC", "#DCE3ED", "#9AA4B8", "#4A5470"], text: ["#0B1020", "#2A3350", "#3A4462"], accent: "#694C0B", cool: "#294F91", lock: "#8E2866", nc: "#47506A", orn: "#3F4862", dark: false, hc: true }
  };
  function palCard(k) {
    var P = PAL[k], names = ["Window", "Sunken", "Raised", "Hover", "Line", "Strong line"], light = !P.dark;
    var sky = P.hc ? P.surf[0] : light ? "linear-gradient(180deg,#D3DEF0 0%,#E4EAF4 25%,#EFE9EA 44%,#EEF1F6 58%,#F4F6F9 100%)" : "linear-gradient(180deg,#1B2552 0%,#151D44 18%,#10162E 46%,#0F1424 66%,#111833 88%,#141C3A 100%)";
    var h = '<div class="pc16' + (light ? " lt" : "") + '" style="--w:' + P.surf[0] + ";--r:" + P.surf[2] + ";--t:" + P.text[0] + ";--t2:" + P.text[1] + ";--t3:" + P.text[2] + ";--ln:" + P.surf[4] + ";--sl:" + P.surf[5] + '">';
    h += '<div class="pch"><b>' + P.name + "</b><span>" + P.sub + "</span></div>";
    h += '<div class="sw">' + P.surf.map(function (c, i) { return '<div><i style="background:' + c + '"></i><span>' + names[i] + "<br><code>" + c + "</code></span></div>"; }).join("") + "</div>";
    h += '<div class="pan" style="background:' + sky + '">' + "" + '<div class="pcard"><h5 style="color:' + P.orn + '">Requirements</h5>' +
      '<div class="tl" style="color:' + P.text[0] + '">Text · Level 54, you are 100</div><div class="tl" style="color:' + P.text[1] + '">Secondary · Ishgard · The Pillars</div><div class="tl" style="color:' + P.text[2] + '">Tertiary · Checked 5 min ago</div>' +
      '<div class="tl"><span style="color:' + P.accent + ';font-weight:600">Ready</span> <span style="color:' + P.text[1] + '">·</span> <span style="color:' + P.accent + '">Completed</span> <span style="color:' + P.text[1] + '">·</span> <span style="color:' + P.lock + '">Locked out</span> <span style="color:' + P.text[1] + '">·</span> <span style="color:' + P.nc + '">Not checked</span> <span style="color:' + P.text[1] + '">·</span> <u style="color:' + P.cool + '">Blood Drain</u></div>' +
      '<div class="mr">' + ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"].map(function (s) { return '<img src="' + TH + "ishgard-glass/_row/" + s + '.svg" width="20" height="20">'; }).join("") +
      '<span class="gp16' + (light ? " lt" : "") + '">Go to giver</span></div></div></div>';
    h += "</div>";
    return h;
  }
  function portraitPair() {
    var keep = SNOW;
    SNOW = false; var a = face(96, "full", "tataru"), a2 = face(96, "full", "yshtola");
    SNOW = true; var b = face(96, "full", "tataru"), b2 = face(96, "full", "yshtola");
    SNOW = keep;
    return '<div class="pp16"><div class="ppn"><div class="row" style="gap:10px">' + a + a2 + '</div><span>Night: night multiply, desaturate, lift</span></div><div class="pps"><div class="row" style="gap:10px">' + b + b2 + "</div><span>Ishgard Snow: no night multiply; desaturate, scale .97, the same black lift</span></div></div>";
  }
  function boardPalettes() {
    var h = '<div class="board b15 b16"><h2>1.16 · Palettes<small>Night (today), Ishgard Snow (the first light palette) and their high-contrast forms. Medals keep their own enamel and are never recoloured; the palette moves text, chrome, sky and shadow.</small></h2>';
    h += '<div class="row">' + palCard("night") + palCard("nighthc") + palCard("snow") + palCard("snowhc") + "</div>";
    h += '<div class="row" style="align-items:flex-start">' + fig(portraitPair(), "<b>Portraits on light</b> (the supervisor's ruling): Snow skips the night multiply, and keeps the desaturation and the black lift, so a portrait's darkest values stay the navy of the ink. The plate's well turns pale, the keyline is lead, the lip shadow is navy at .22.") +
      '<div class="spec" style="width:860px" id="ct16"></div></div>';
    return h + "</div>";
  }
  var CONTRAST = /*CONTRAST*/"";

  // ---------- Settings › Themes ----------
  var THEMES = [
    { k: "medallion", name: "Menphina's Medallion", sub: "Brass frames · Night", pal: "night", dir: "../../moon-v6/round5/medallion-r5/_row/", def: true },
    { k: "glass", name: "Ishgard Glass", sub: "Came frames · Ishgard Snow", pal: "snow", dir: TH + "ishgard-glass/_row/" },
    { k: "aether", name: "Aether Crystal", sub: "Silver frames · Night", pal: "night", dir: TH + "aether-crystal/_row/" },
    { k: "classic", name: "Classic", sub: "The 1.11 moons · Night", pal: "night", legacy: true },
    { k: "orrery", name: "Astrologian's Orrery", sub: "Astrolabe frames · Dawn", pal: "dawn", dir: TH + "astrologian-orrery/_row/", later: true },
    { k: "sumi", name: "Sumi to Kinpaku", sub: "Kirikane frames · Kugane Lacquer", pal: "kugane", later: true }
  ];
  var ST8 = ["ready", "ready-on-another-job", "in-journal", "blocked", "done-this-cycle", "completed", "locked-out", "not-checked"];
  function classicGlyph(st, px) {
    var o = '<svg width="' + px + '" height="' + px + '" viewBox="0 0 16 16">', g = "#F2D27A", sv = "#DDE3F0", n = "#3A4366";
    switch (st) {
      case "completed": return o + '<circle cx="8" cy="8" r="6.5" fill="' + g + '"/><circle cx="6" cy="6.4" r="1.1" fill="#D9B65F"/><circle cx="9.6" cy="9.6" r="1.4" fill="#D9B65F"/></svg>';
      case "in-journal": return o + '<circle cx="8" cy="8" r="6.5" fill="' + n + '" stroke="' + sv + '" stroke-width="1.1"/><path d="M8 1.5a6.5 6.5 0 0 1 0 13a3 6.5 0 0 1 0-13z" fill="' + g + '"/><circle cx="6.3" cy="8" r="1.1" fill="#0F1424"/></svg>';
      case "ready": return o + '<circle cx="8" cy="8" r="7.6" fill="' + g + '" opacity=".25"/><circle cx="8" cy="8" r="6.5" fill="' + n + '" stroke="#7C86A8" stroke-width="1"/><path d="M8 1.5a6.5 6.5 0 0 1 0 13z" fill="' + g + '"/></svg>';
      case "ready-on-another-job": return o + '<circle cx="8" cy="8" r="6.5" fill="' + n + '" stroke="' + g + '" stroke-width="1.1"/><path d="M8 1.5a6.5 6.5 0 0 1 0 13z" fill="' + sv + '"/></svg>';
      case "done-this-cycle": return o + '<circle cx="8" cy="8" r="6.5" fill="' + sv + '" stroke="#7C86A8" stroke-width="1"/><path d="M8 1.5a6.5 6.5 0 0 1 0 13a3 6.5 0 0 0 0-13z" fill="' + n + '"/></svg>';
      case "blocked": return o + '<circle cx="8" cy="8" r="6.3" fill="' + n + '" stroke="' + sv + '" stroke-width="1.2"/></svg>';
      case "locked-out": return o + '<circle cx="8" cy="8" r="6.3" fill="#4A2F45" stroke="#D68AA8" stroke-width="1.2"/><path d="M3.6 3.6l8.8 8.8" stroke="#D68AA8" stroke-width="1.4"/></svg>';
      default: return o + '<circle cx="8" cy="8" r="6.3" fill="#1A2033" stroke="#7C86A8" stroke-width="1" stroke-dasharray="2 1.6"/></svg>';
    }
  }
  function faces8(T, px) {
    return ST8.map(function (s) { return T.k === "classic" ? classicGlyph(s, px) : '<img src="' + T.dir + s + '.svg" width="' + px + '" height="' + px + '">'; }).join("");
  }
  var PSW = { night: ["#0F1424", "#1E2437", "#DDE3F0", "#F2D27A"], snow: ["#EEF1F6", "#F9FAFC", "#1A2136", "#755308"], dawn: ["#1A1526", "#262036", "#F2E8E6", "#F5C47C"], kugane: ["#16100F", "#231917", "#F3E9DB", "#F0CC72"] };
  function themeCard(T, state) {
    var P = PSW[T.pal], cls = "tc" + (state ? " " + state : "") + (T.later ? " later" : "");
    var pane = T.pal === "snow" ? "linear-gradient(180deg,#D3DEF0,#EEF1F6 70%)" : T.pal === "dawn" ? "linear-gradient(180deg,#3A2746,#1A1526 70%)" : T.pal === "kugane" ? "linear-gradient(180deg,#2A1613,#16100F 70%)" : "linear-gradient(180deg,#1B2552,#0F1424 70%)";
    return '<div class="' + cls + '"><div class="tcp" style="background:' + pane + '"><div class="f8">' + (T.later && !T.dir ? ST8.map(function () { return '<i class="ph"></i>'; }).join("") : faces8(T, 28)) + "</div>" +
      '<div class="tcr" style="background:' + P[1] + ";color:" + P[2] + '"><span class="m">' + (T.k === "classic" ? classicGlyph("ready", 16) : T.later && !T.dir ? "" : '<img src="' + T.dir + 'ready.svg" width="16" height="16">') + '</span><span>Firmament</span><b style="color:' + P[3] + '">Ready</b></div></div>' +
      '<div class="tcb"><div class="tn16"><b>' + T.name + "</b>" + (T.legacy ? '<span class="lg">Legacy</span>' : "") + (T.later ? '<span class="lg">1.17</span>' : "") + "" + '</div><div class="ts">' + T.sub + '</div><div class="pw">' + P.map(function (c) { return '<i style="background:' + c + '"></i>'; }).join("") + (state === "on" ? '<span class="use">In use</span>' : "") + "</div></div></div>";
  }
  function previewPanel(T, hovering) {
    var pal = T.pal === "snow" ? "snow" : "night", rows = [["ready", "Towards the Firmament", "Ready", "Lv 60"], ["in-journal", "Dawntrail", "In journal", "step 4 of 6"], ["completed", "A General Summons", "Completed", "2025-01-02"], ["blocked", "Crossroads", "Blocked", "after MSQ"], ["locked-out", "Heads, I Win", "Locked out", "another choice"], ["not-checked", "Faerie Tale", "Not checked", "accept condition"]];
    var ink = pal === "snow" ? { t: "#1A2136", t2: "#434D6A", acc: "#755308", lock: "#962A6A", nc: "#56607C", bg: "#EEF1F6", r: "#F9FAFC", ln: "#CAD2DF", h: "#3F4862" } : { t: "#DDE3F0", t2: "#A9B2CC", acc: "#F2D27A", lock: "#D68AA8", nc: "#8A93B0", bg: "#0F1424", r: "#1E2437", ln: "#2A3149", h: "#E6CF98" };
    var col = function (st) { return st === "locked-out" ? ink.lock : st === "not-checked" ? ink.nc : st === "blocked" ? ink.t2 : ink.acc; };
    var hero = T.k === "classic" ? classicGlyph("in-journal", 64) : '<img src="' + T.dir.replace("_row/", "") + 'in-journal.svg" width="64" height="64">';
    var h = '<div class="pv16" style="background:' + ink.bg + ";color:" + ink.t + ";border-color:" + ink.ln + '"><div class="pvh" style="color:' + ink.t2 + '">' + (hovering ? "Previewing: <b style=\"color:" + ink.t + "\">" + T.name + "</b><span>In use: Menphina's Medallion</span>" : "Preview · <b style=\"color:" + ink.t + "\">" + T.name + "</b>") + "</div>";
    h += '<div class="pvb"><div class="pvr">' + rows.map(function (r) { return '<div class="r16" style="border-color:' + ink.ln + '">' + (T.k === "classic" ? classicGlyph(r[0], 18) : '<img src="' + T.dir + r[0] + '.svg" width="18" height="18">') + "<span>" + r[1] + '</span><em><b style="color:' + col(r[0]) + '">' + r[2] + '</b> <i style="color:' + ink.t2 + '">· ' + r[3] + "</i></em></div>"; }).join("") + "</div>";
    h += '<div class="pvc" style="background:' + ink.r + ";border-color:" + ink.ln + '">' + hero + '<div><h5 style="color:' + ink.h + '">In journal</h5><div style="color:' + ink.t2 + '">Dawntrail · step 4 of 6</div></div></div></div></div>';
    return h;
  }
  function seg16(items, on) { return '<div class="sg16">' + items.map(function (x, i) { return '<span class="' + (i === on ? "on" : "") + '">' + x + "</span>"; }).join("") + "</div>"; }
  function palChip(k, name, on, later) {
    var P = PSW[k] || ["#2A2F3D", "#3A4050", "#DDE3F0", "#F2D27A"];
    var sky = k === "snow" ? "linear-gradient(180deg,#D3DEF0,#EEF1F6)" : k === "dawn" ? "linear-gradient(180deg,#3A2746,#1A1526)" : k === "kugane" ? "linear-gradient(180deg,#2A1613,#16100F)" : k === "dalamud" ? "linear-gradient(135deg,#2B2B2B,#1E1E1E)" : "linear-gradient(180deg,#1B2552,#0F1424)";
    return '<div class="pk' + (on ? " on" : "") + (later ? " later" : "") + '"><div class="pkm" style="background:' + sky + '"><i style="background:' + P[1] + '"></i><i style="background:' + P[1] + ';width:60%"></i><b style="background:' + P[3] + '"></b><em style="background:' + P[2] + '"></em></div><span>' + name + (later ? " <i>1.17</i>" : "") + "</span></div>";
  }
  function settingsPage(v) {
    // v: "1.16" or "1.17"; hover: which card is hovered (preview swaps to it)
    var later = v === "1.17", hov = later ? null : "glass";
    var nav = ["General", "Themes", "Journal", "Travel", "In-game panels", "Overlay", "Alerts", "Companions", "Data", "Advanced"];
    var h = '<div class="mk full v7 set16" style="height:' + (later ? 1760 : 1330) + 'px"><div class="win"><div class="w-title"><span class="tri"></span>Tsukimichi Settings<span class="wc">' + ICON.x + '</span></div><div class="sbody"><nav class="snav"><div class="ssearch">' + ICON.search + "<span>Search settings</span></div>" +
      nav.map(function (n) { return '<div class="sn' + (n === "Themes" ? " on" : "") + '">' + n + "</div>"; }).join("") + '</nav><div class="spage">';
    h += '<div class="sh16"><h3>Themes</h3><p>Moons, frames and colours. Hover a theme to preview it; click to use it.</p></div>';
    h += '<section><h4>Theme</h4><div class="tgrid">' + THEMES.filter(function (T) { return (later || !T.later) && !(T.later && !T.dir); }).map(function (T) { return themeCard(T, T.def ? "on" : T.k === hov ? "hov" : ""); }).join("") + "</div></section>";
    h += '<section><h4>Preview</h4>' + previewPanel(hov ? THEMES[1] : THEMES[0], !!hov) + "</section>";
    h += '<section><h4>Colours</h4><div class="srow"><div class="sl"><b>Palette</b><span>Window, text and sky colours. Moons keep their own.</span></div><div class="pks">' +
      palChip("night", "Night", true) + palChip("snow", "Ishgard Snow") + (later ? palChip("dawn", "Dawn") + palChip("kugane", "Kugane Lacquer") : "") + palChip("dalamud", "Follow Dalamud") + "</div></div>";
    h += '<div class="srow"><div class="sl"><b>High contrast</b><span>One set of moons made for low vision, on every theme. Your theme\'s moons return when it is off.</span></div><span class="tg16"></span></div>';
    h += '<div class="srow"><div class="sl"><b>Frames</b><span>The metal round each moon and card.</span></div>' + seg16(["From theme", "Brass", "Silver", "Came"].concat(later ? ["Astrolabe", "Kirikane"] : []), 0) + "</div></section>";
    if (later) {
      h += '<section class="new17"><h4>Mix moons by state <span class="nb">1.17</span></h4><div class="mixt">' + ST8.map(function (s, i) {
        var nm = { "ready": "Ready", "ready-on-another-job": "Ready on another job", "in-journal": "In journal", "blocked": "Blocked", "done-this-cycle": "Done this cycle", "completed": "Completed", "locked-out": "Locked out", "not-checked": "Not checked" }[s];
        var pick = i === 1 ? ["aether", "Aether Crystal"] : i === 5 ? ["glass", "Ishgard Glass"] : null;
        var src = pick ? (pick[0] === "glass" ? TH + "ishgard-glass/" : TH + "aether-crystal/") : "../../moon-v6/round5/medallion-r5/";
        return '<div class="mx"><span class="mn">' + nm + '</span><img src="' + src + (s === "ready-on-another-job" ? "ready-on-another-job-paladin" : s) + '.svg" width="32" height="32"><span class="dd16">' + (pick ? pick[1] : "From theme") + '<i>▾</i></span>' + (i === 1 ? '<span class="nt">close to Blocked in a list</span>' : "<span></span>") + "</div>";
      }).join("") + '</div><div class="mixs"><span>1 pair of moons is close in a list.</span><span class="qa">Fix it</span><span class="qa">Reset mix</span></div></section>';
      h += '<section class="new17"><h4>Share <span class="nb">1.17</span></h4><div class="srow"><div class="sl"><b>Share code</b><span>Ids only; paste one to preview what changes before applying.</span></div><div class="shr"><code>TM1-7Q4K-2XH8-M3D1</code><span class="qa">Copy</span><span class="pin16">Paste a code</span><span class="qa">Apply</span></div></div></section>';
    }
    h += '<section><div class="srow rst"><div class="sl"><b>Reset appearance</b><span>' + (later ? "Hold to reset while a mix is set; Undo follows either way." : "Back to Menphina's Medallion on Night. Undo follows.") + '</span></div><span class="qa rs">' + ICON.reset + (later ? "Hold to reset" : "Reset appearance") + "</span></div></section>";
    h += "</div></div>" + (later ? "" : '<div class="toast16">Theme: Ishgard Glass<span class="u">Undo</span></div>') + "</div></div>";
    return h;
  }
  function boardThemes() {
    return '<div class="board b15 b16"><h2>1.16 · Settings › Themes<small>Left: the page as 1.16 ships it, the Ishgard Glass card hovered and previewed. Right: the same page in 1.17 with the Orrery (Sumi to Kinpaku stays hidden until its art is approved), two more palettes, Mix by state and Share: the same column, the same widths; sections are added below Frames, nothing above moves.</small></h2>' +
      '<div class="row" style="align-items:flex-start">' + fig(settingsPage("1.16"), "<b>1.16</b>") + fig(settingsPage("1.17"), "<b>1.17</b> (mix-and-match; no redesign)") + "</div></div>";
  }

  // ======================================================================================================
  // 1.21 "What next, for every character": P1–P8, N8–N11 (spec-1.21.md). Built only from the 1.14–1.19 parts:
  // card(), sect19 (Section heading), chips (ch19/bg19), quiet actions (qa19), pills (pill18), game icons (ic15),
  // look18 wrappers, the 1.15 plates for faces, and the 1.18/1.19 colour language. No new tokens.
  // Data: quest names, levels, steps, places, Triple Triad gates and casts were read from the 2026.09.15 client
  // with Lumina; characters, dates and counts are an example account (spec-1.21 "The mock's data").
  // ======================================================================================================
  var A21 = "1.21/icons/";
  function ic21(file, px, cls) { return '<img class="gi ' + (cls || "") + '" src="' + A21 + file + '.png" width="' + px + '" height="' + px + '" alt="">'; }
  // A name the spoiler shield hides: the mask words in Secondary, never the real name, never a face.
  function mask21(text) { return '<span class="mk21">' + text + "</span>"; }
  function figw(inner, cap, w) { return '<div class="col21" style="width:' + w + 'px">' + fig(inner, cap) + "</div>"; }
  function chip21(t, cls) { return '<span class="ch19 ' + (cls || "") + '">' + t + "</span>"; }
  // Initials take Snow's text ink on the light plate (the 1.15 plate draws them in moonlight for night palettes).
  function av21(lv, kind, o, px) { var h = plate(px || 20, lv, kind, o); if (SNOW) h = h.replace('fill="#E9E4D2"', 'fill="#2A3350"'); return '<span class="av21">' + h + "</span>"; }
  function med21(lv, st, px) { return lv === "full" ? medalImg(st, px, true) : lv === "quiet" ? quietMedal(st, px - 4, true) : flatGlyph(st, 20); }
  // Light-palette plates and quiet medals read the global SNOW flag while their HTML is built.
  function inSnow21(f) { SNOW = true; try { return f(); } finally { SNOW = false; } }
  function travel21(lv, label, icon, pri) { return pill18(lv, (icon ? ic15(icon, lv === "plain" ? 14 : 16, "tile") : "") + label, pri); }

  // ---------- P1 Up next ----------
  // o: st, name, lvl, reason, place, av (plate html), pill, icon, more (second quiet action)
  function upNext21(lv, o) {
    return '<div class="un21">' +
      '<div class="ue">Up next' + (o.why ? '<span class="uw">' + o.why + "</span>" : "") + "</div>" +
      '<div class="ub"><span class="um">' + med21(lv, o.st, 48) + "</span>" +
      '<div class="ut"><div class="unm"><span class="nx">' + o.name + '</span><span class="lvc">' + o.lvl + "</span></div>" +
      '<div class="ur">' + o.reason + "</div>" +
      '<div class="up">' + (o.av || "") + "<span>" + o.place + "</span></div></div></div>" +
      '<div class="ua">' + travel21(lv, o.pill, o.icon, true) + (o.more ? qa19(o.more) : "") + qa19("Details") + "</div></div>";
  }
  var UP21 = {
    journal: { st: "in-journal", name: "The Long Road to Xak Tural", lvl: "Lv 95", reason: "Next in the main scenario · in your journal",
      place: "Step 3: Speak with Erenville. · Shaaloani", pill: "Teleport", icon: "060453", av: ["initials", { text: "E" }] },
    route: { st: "ready", name: "Bridging the Rift", lvl: "Lv 90", reason: "Your route to Flying in " + mask21("a zone ahead") + " · 31 stops left",
      place: "Talk to veteran Radiant · Radz-at-Han", pill: "Teleport", icon: "060453", av: ["sil", { sil: "hyur-male" }] },
    goal: { st: "ready", name: "Maniac Manor", lvl: "Lv 50", reason: "Your goal: match Michiru's unlocks · 13 left",
      place: "Talk to Lauriane · Mor Dhona", pill: "Teleport", icon: "060453", av: ["initials", { text: "L" }] },
    msq: { st: "ready", name: "Hitting the Books", lvl: "Lv 80", reason: "Next in the main scenario",
      place: "Talk to Krile · Main Hall, Old Sharlayan", pill: "Teleport", icon: "060453", av: ["initials", { text: "K" }] },
    pin: { st: "ready", name: "A Harmony from the Heavens", lvl: "Lv 80", reason: "Your first pinned quest that is Ready",
      place: "Talk to Jehantel · South Shroud", pill: "Walk to giver", icon: "000104", av: ["initials", { text: "J" }] },
    stop: { st: "ready", name: "Caught in the Act", lvl: "Lv 56", reason: "The closest Ready quest, from Next stops",
      place: "Talk to Elaisse · The Pillars, 60 yalms", pill: "Walk to giver", icon: "000104", av: ["initials", { text: "E" }] },
    gate: { st: "blocked", name: "The Darkness Below", lvl: "Lv 70", reason: "The next story quest needs Lv 70 · you're DRK 69",
      place: "3 Ready quests here give about 60% of a level", pill: "Show the 3 quests", icon: "", av: "" }
  };
  function up21(lv, key, extra) {
    var o = Object.assign({}, UP21[key], extra || {});
    if (o.av && o.av.length) o.av = av21(lv, o.av[0], o.av[1], 20);
    return upNext21(lv, o);
  }

  // The Tonight card in place: Up next first, then the card's existing lines (they keep their places).
  function tonight21(lv) {
    var body = up21(lv, "journal") +
      '<div class="tg21">' +
      '<div class="tl21"><span>14 quests are Ready on WHM</span><span class="lk21">Show them ›</span></div>' +
      '<div class="tl21 ms"><span class="g">' + rowGlyph(lv, "in-journal") + "</span><span><b>Main scenario</b> · 91 quests to the latest story, Lv 95–100</span></div>" +
      '<div class="tl21"><span>Events now: The Rising</span></div></div>' +
      '<div class="tg21"><div class="tgh">Pinned and Ready</div>' +
      '<div class="tl21 q"><span class="g">' + rowGlyph(lv, "ready") + "</span><span>Knowing the Pelupelu</span><span class=\"s\">Urqopacha</span></div>" +
      '<div class="tl21 q"><span class="g">' + rowGlyph(lv, "ready") + "</span><span>A Leaking Workpot</span><span class=\"s\">Kozama'uka</span></div></div>" +
      '<div class="tg21"><div class="tgh">Next stops</div>' +
      '<div class="tl21 q"><span class="g">' + av21(lv, "sil", { sil: "hrothgar-male" }, 24) + '</span><span>Talk to Shaaloani trader</span><span class="s">Shaaloani · teleport</span></div></div>';
    if (lv === "plain") return '<div class="ps">' + ic15("071201", 14, "marker") + 'Tonight<span></span></div><div class="pl21">' + body + "</div>";
    return card(lv, "<h4>Tonight</h4>" + body);
  }
  // The detail header's way back: ‹ › (1.14) and the Tonight button.
  function backBar21(lv) {
    return '<div class="bb21"><span class="rb">‹</span><span class="rb">›</span><span class="sp"></span><span class="tn">' + moonIcon21() + "Tonight</span></div>";
  }
  function moonIcon21() { return '<svg width="13" height="13" viewBox="0 0 16 16" aria-hidden="true"><path d="M10.6 1.4a6.6 6.6 0 1 0 4 11.2A5.6 5.6 0 0 1 10.6 1.4z" fill="currentColor"/></svg>'; }

  // ---------- P2 the current step ----------
  function seg21(items, on, dis) {
    return '<span class="sg21">' + items.map(function (x, i) { return '<span class="' + (i === on ? "on" : "") + (dis === i ? " dis" : "") + '">' + x + "</span>"; }).join("") + "</span>";
  }
  // Coordinate pairs and "· N more" stay on one line; lines wrap only at the "·" separators.
  function nw21(t) { return t.replace(/(X [\d.]+, Y [\d.]+)/g, '<span class="nw">$1</span>').replace(/(· \d+ more)/g, '<span class="nw">$1</span>'); }
  function step21(lv, o) {
    // o: aim (0 step, 1 giver), stepLine, where, pills[], note, dis
    return '<div class="cs21"><div class="csa"><span class="csl">Aim at</span>' + seg21(["Current step", "Giver"], o.aim, o.dis) + "</div>" +
      '<div class="cst">' + nw21(o.stepLine) + "</div>" + '<div class="csw">' + nw21(o.where) + "</div>" +
      (o.extra || "") + '<div class="csp">' + o.pills + "</div>" + (o.note ? '<div class="csn">' + o.note + "</div>" : "") + "</div>";
  }
  function detail21(lv) {
    var h = backBar21(lv) + '<div class="hd19"><div class="n">The Long Road to Xak Tural</div><div class="m">Main Scenario › Dawntrail · Tuliyollal · Lv 95</div>' +
      '<div class="st19"><span class="ij">In journal</span><span class="cf19">step 3</span></div></div>';
    h += sect19(lv, "Where to go", "",
      step21(lv, { aim: 0, stepLine: "Step 3 · <b>Speak with Erenville.</b>", where: "Shaaloani · X 27.0, Y 34.8 · you're in Tuliyollal",
        pills: travel21(lv, "Flag", "060561") + travel21(lv, "Teleport", "060453", true) + travel21(lv, "Walk", "000104"),
        note: "Travel follows this step until it's done, then the next one. The giver, Erenville in Tuliyollal, is one click away." }));
    return h;
  }
  function detail21giver(lv) {
    return step21(lv, { aim: 1, stepLine: "Giver · <b>Erenville</b>", where: "Tuliyollal · X 14.4, Y 2.4",
      pills: travel21(lv, "Flag", "060561") + travel21(lv, "Teleport", "060453", true) + travel21(lv, "Walk", "000104") });
  }
  function detail21places(lv) {
    return step21(lv, { aim: 0, stepLine: "Step 2 · <b>Obtain hunks of nanka flesh from clearwater nankas.</b> · 2 more",
      where: "Where the clearwater nankas are · The Dravanian Forelands · X 28.8, Y 22.3",
      pills: travel21(lv, "Flag", "060561") + travel21(lv, "Teleport", "060453", true) + travel21(lv, "Walk", "000104") });
  }
  function detail21duty(lv) {
    return step21(lv, { aim: 0, stepLine: "Step 2 · <b>Enter the Aery.</b>",
      where: "The entrance · The Churning Mists · X 33.7, Y 15.5",
      extra: '<div class="dt19 cs">' + badge19("000089", "Solo with NPCs") + badge19("", "Story-required", "q") + "</div>",
      pills: travel21(lv, "Teleport", "060453", true) + travel21(lv, "Walk to the entrance", "000104") + qa19("Duty Finder") });
  }
  function detail21none(lv) {
    return step21(lv, { aim: 1, dis: 0, stepLine: "Step 1 · <b>Obtain a sack of adventurer's effects by clearing a dungeon via Duty Roulette: High-level Dungeons.</b>",
      where: "This step has no place in the game's data, so travel aims at the giver: Brangwine · Mor Dhona",
      pills: travel21(lv, "Flag", "060561") + travel21(lv, "Teleport", "060453", true) + qa19("Duty Finder") });
  }
  function menu21(items) {
    return '<div class="cm21">' + items.map(function (it) { return it === "-" ? '<div class="sep"></div>' : '<div class="it' + (it[1] ? " hv" : "") + '">' + (it[2] ? ic15(it[2], 14, "tile") : '<span class="ni"></span>') + it[0] + "</div>"; }).join("") + "</div>";
  }

  function board_tonight21() {
    var h = '<div class="board b15 b16"><h2>1.21 · Up next<small>P1: one recommendation at the top of Tonight, and a way back to Tonight. Each block is a character from the example account. Full on Night.</small></h2><div class="row" style="align-items:flex-start">';
    h += '<div class="col21" style="width:430px">' + fig(look18("full", "night", tonight21("full"), 420), "<b>Tonight, in place</b> (Yuna, WHM 95): Up next leads the card. The main scenario line below it shows the catch-up instead of repeating the quest. Nothing else moves.") +
      fig(look18("full", "night", backBar21("full") + '<div class="hd19"><div class="n">Maniac Manor</div><div class="m">Mor Dhonan Sidequests · Mor Dhona · Lv 50</div></div>', 420), "<b>The way back</b>: <b>Tonight</b> on the detail header, beside ‹ ›; Esc or a second click on the selected row does the same. The panes cross-fade (0.15 s).") + "</div>";
    h += '<div class="col21" style="width:430px">';
    [["route", "<b>From your route</b> (Kiri, SGE 90). The route's goal is a Dawntrail zone, past Kiri's story: the shield masks it."],
      ["goal", "<b>From the character's goal</b> (N11)"],
      ["msq", "<b>The next main scenario quest</b>, when it is Ready (Sora, AST 82)"],
      ["pin", "<b>The first pinned quest that is Ready</b> (Michiru, caught up). Travel and walking adds Walk."]].forEach(function (x) { h += fig(look18("full", "night", up21("full", x[0]), 420), x[1]); });
    h += "</div>";
    h += '<div class="col21" style="width:430px">';
    h += fig(look18("full", "night", up21("full", "stop"), 420), "<b>The closest Ready quest</b>, from Next stops (Hana, SCH 61)");
    h += fig(look18("full", "night", up21("full", "gate"), 420), "<b>The level gate</b> (Aki, DRK 69): nothing to travel to, so the action shows the quests that close the gap (C8's EXP)");
    h += fig(look18("full", "night", '<div class="tt19"><b>Why this one?</b><p>Up next picks the first of these that has a quest for you:</p><ol class="ol21"><li>the next stop of the route you follow</li><li>this character\'s goal</li><li>the next main scenario quest, when it is Ready or in your journal</li><li>your first pinned quest that is Ready</li><li>the closest Ready quest in Next stops</li><li>the level that opens the next story quest</li></ol></div>', 420), "<b>The reason line's hover</b>: the order, in words");
    return h + "</div></div>";
  }
  function board_step21() {
    var h = '<div class="board b15 b16"><h2>1.21 · Go to the current step<small>P2: for a quest in your journal, Flag, Teleport and Walk aim at the step you are on, with the giver one click away. Full on Night unless noted.</small></h2><div class="row" style="align-items:flex-start">';
    h += '<div class="col21" style="width:450px">' + fig(look18("full", "night", '<div class="dp19">' + detail21("full") + "</div>", 440), "<b>P2, In journal</b>: the travel buttons aim at <b>step 3</b>, not back at the giver. The objective is the game's own words for the step you are on; later steps are never named.") + "</div>";
    h += '<div class="col21" style="width:440px">' + fig(look18("full", "night", detail21giver("full"), 430), "<b>Giver</b> chosen: the same buttons, aimed at Erenville in Tuliyollal. The choice lasts for this quest only.") +
      fig(look18("full", "night", detail21places("full"), 430), "<b>Many places</b> (Gifts for the Outcasts): the area the game gives for the step, here where the nankas are") +
      fig(look18("full", "night", detail21duty("full"), 430), "<b>A duty step</b> (Into the Aery): travel aims at the entrance; the 1.19 badges say how you'll clear it") + "</div>";
    h += '<div class="col21" style="width:440px">' + fig(look18("full", "night", detail21none("full"), 430), "<b>No place in the data</b> (Morbid Motivation, a roulette step): said in words; Current step is disabled with the reason showing, never only in a tooltip") +
      fig(look18("full", "night", menu21([["Flag the current step", 0, "060561"], ["Teleport near the current step", 1, "060453"], ["Walk to the current step", 0, "000104"], "-", ["Flag the giver", 0, "060561"], ["Show in the Journal", 0, ""], ["Link in chat", 0, ""]]), 300), "<b>The Todo overlay's right-click</b> on an In journal quest (Nearby's \"…\" menu too). <code>/tsuki go</code> does the same from a macro (P8 shows its chat line).") +
      fig(inSnow21(function () { return look18("quiet", "snow", '<div class="dp19">' + sect19("quiet", "Where to go", "", step21("quiet", { aim: 0, stepLine: "Step 3 · <b>Speak with Erenville.</b>", where: "Shaaloani · X 27.0, Y 34.8 · you're in Tuliyollal", pills: travel21("quiet", "Flag", "060561") + travel21("quiet", "Teleport", "060453", true) })) + "</div>", 430); }), "<b>Quiet, Ishgard Snow</b>, at the Travel automation level (no Walk)") + "</div>";
    return h + "</div></div>";
  }

  // ---------- P3 the roster board, N11 alt goals ----------
  var ROS21 = [
    { n: "Michiru Tsukikage", w: "Balmung", role: "main", star: 1, job: ["062123", "BRD 100"], story: ["Dawntrail", "caught up"], goal: null, ready: 6, allow: ["12 allowances", "6 deliveries left"], moon: "212", seen: ["now", "this client"] },
    { n: "Emi Kurosawa", w: "Balmung", role: "", job: ["062142", "PCT 100"], story: ["Dawntrail", "caught up"], goal: ["done", "Dawntrail flying"], ready: 3, allow: ["12 allowances", "6 deliveries left"], moon: "297", seen: ["4 days ago", "other folder"], ro: 1 },
    { n: "Yuna Hoshizora", w: "Zalera", role: "", job: ["062124", "WHM 95"], story: ["Dawntrail · next: The Long Road to Xak Tural", "91 to the latest"], goal: ["Story to 7.0", "48 left"], ready: 14, allow: ["9 allowances", "6 deliveries left"], moon: "402", seen: ["live", "other client"], ro: 1 },
    { n: "Kiri Tsukikage", w: "Balmung", role: "healer", job: ["062140", "SGE 90"], story: ["Endwalker · next: Bridging the Rift", "183 to the latest"], goal: ["Match Michiru's unlocks", "13 left · 9 Ready"], ready: 22, allow: ["12 allowances", "6 deliveries left"], moon: "388", seen: ["live", "other client"], ro: 1 },
    { n: "Sora Amane", w: "Balmung", role: "", job: ["062133", "AST 82"], story: ["Endwalker · next: Hitting the Books", "296 to the latest"], goal: ["Flying in Endwalker", "5 zones left"], ready: 31, allow: ["12 allowances", "3 deliveries left"], moon: "455", seen: ["2 days ago", ""] },
    { n: "Aki Tsukikage", w: "Balmung", role: "tank", job: ["062132", "DRK 69"], story: ["Stormblood · next: The Darkness Below", "Lv 70 needed"], goal: ["All duty roulettes open", "2 duties left"], ready: 9, allow: ["12 allowances", "3 deliveries left"], moon: "509", seen: ["5 days ago", ""] },
    { n: "Hana Mizuki", w: "Mateus", role: "", job: ["062128", "SCH 61"], story: ["Heavensward · next: Another Time, Another Place", "659 to the latest"], goal: null, ready: 40, allow: ["12 allowances", "3 deliveries left"], moon: "533", seen: ["1 week ago", ""] },
    { n: "Ren Kurogane", w: "Zalera", role: "", job: ["062121", "WAR 50"], story: ["A Realm Reborn · next: Laying the Foundation", "832 to the latest"], goal: ["Story to 6.0", "463 left"], ready: 54, allow: ["12 allowances", "0 deliveries left"], moon: "570", seen: ["2 weeks ago", ""] },
    { n: "Nao Shirakawa", w: "Coeurl", role: "crafter", job: ["062115", "CUL 41"], story: ["A Realm Reborn · next: Into the Beast's Maw", "940 to the latest"], goal: null, ready: 61, allow: ["12 allowances", "no deliveries yet"], moon: "598", seen: ["3 weeks ago", "saved before 7.5"] }
  ];
  function roster21(lv, rows, o) {
    o = o || {};
    var head = '<div class="rh21"><span>Character</span><span>Job</span><span class="srt">Story ▾</span><span>Goal</span><span class="num">Ready</span><span>Today</span><span class="num">Moonlit</span><span>Last seen</span></div>';
    var body = rows.map(function (r, i) {
      var goal = !r.goal ? '<span class="gs">Set a goal…</span>' : r.goal[0] === "done" ? '<b class="gd">Goal reached</b><small>' + r.goal[1] + " · Set a new goal</small>" : "<b>" + r.goal[0] + "</b><small>" + r.goal[1] + "</small>";
      var seen = r.seen[0] === "live" ? '<span class="lv21"><i></i>Live</span><small>' + r.seen[1] + "</small>" : "<b>" + r.seen[0] + "</b>" + (r.seen[1] ? (r.seen[1] === "saved before 7.5" ? '<small class="old">saved before 7.5</small>' : "<small>" + r.seen[1] + "</small>") : "");
      return '<div class="rr21' + (o.hover === i ? " hov" : "") + (o.sel === i ? " sel" : "") + '">' +
        '<span class="c"><b>' + (r.star ? '<i class="st21" title="Starred: sorts first"></i>' : "") + r.n + "</b><small>" + r.w + (r.role ? " · " + r.role : "") + "</small></span>" +
        '<span class="j">' + (r.job[0] === "062115" ? ic15 : ic21)(r.job[0], 20, "tile") + r.job[1] + "</span>" +
        '<span class="s"><b>' + r.story[0] + "</b><small>" + r.story[1] + "</small></span>" +
        '<span class="g">' + goal + "</span>" +
        '<span class="num">' + r.ready + "</span>" +
        '<span class="a"><b>' + r.allow[0] + "</b><small>" + r.allow[1] + "</small></span>" +
        '<span class="num">' + r.moon + "</span>" +
        '<span class="l">' + seen + "</span></div>";
    }).join("");
    return '<div class="ro21">' + head + body + "</div>";
  }
  function rosterTop21(lv) {
    return '<div class="rt21">' + seg21(["Dashboard", "Collection by character", "All characters"], 2) + '<span class="rc">9 characters · 2 live in other clients</span></div>';
  }
  function heroLine21(lv) {
    var m = function (st, n) { return '<span class="oc21">' + rowGlyph(lv, st, 14) + n + "</span>"; };
    return '<div class="hd19"><div class="n">Maniac Manor</div><div class="m">Mor Dhonan Sidequests · Mor Dhona · Lv 50</div><div class="st19"><span class="sw">Ready</span></div>' +
      '<div class="oc21l"><span class="lbl">Your other characters</span>' + m("ready", "Kiri") + m("ready", "Sora") + '<span class="oc21">Done on 4</span><span class="oc21 d">2 can\'t take it yet</span></div></div>';
  }
  function heroTip21() {
    var r = function (st, n, t) { return '<div class="ht21">' + rowGlyph("full", st, 14) + "<b>" + n + "</b><span>" + t + "</span></div>"; };
    return '<div class="tt19"><b>Maniac Manor on every character</b>' + r("ready", "Kiri", "Ready") + r("ready", "Sora", "Ready") + r("completed", "Michiru", "Completed · 2023-04-11") + r("completed", "Yuna", "Completed · 2024-02-02") +
      r("completed", "Hana", "Completed · 2025-06-30") + r("completed", "Emi", "Completed · 2025-09-14") + r("blocked", "Ren", "Blocked · after The Ultimate Weapon") + r("blocked", "Nao", "Blocked · Lv 50 needed") + "</div>";
  }
  function goalPop21(lv) {
    var opt = function (on, t, pick) { return '<div class="go21' + (on ? " on" : "") + '"><i class="rd"></i><span>' + t + "</span>" + (pick ? '<span class="pk">' + pick + " ▾</span>" : "") + "</div>"; };
    var inner = '<div class="gp21"><div class="gph"><b>Goal for Kiri</b><span>Catch this character up to…</span></div>' +
      opt(0, "The story, up to a patch", "7.0 Dawntrail") + opt(1, "Another character's unlocks", "Michiru") + opt(0, "Flying in an expansion", "Endwalker") + opt(0, "Every duty roulette open", "") +
      '<div class="gpv">13 unlock quests Michiru has done and Kiri hasn\'t: 9 can be done now, 4 wait for Kiri\'s story.</div>' +
      '<div class="gpa">' + pill18(lv, "Set goal", true) + qa19("Cancel") + "</div></div>";
    return inner;
  }
  function goalCard21(lv, live) {
    var row = function (st, n, s) { return '<div class="gr21"><span class="g">' + rowGlyph(lv, st) + '</span><span class="nm">' + n + '</span><span class="sx">' + s + "</span></div>"; };
    return sect19(lv, "Goal: match Michiru's unlocks", "13 left",
      row("ready", "Maniac Manor", "Ready · Mor Dhona") + row("ready", "Out of Sight, Out of Mine", "Ready · Mor Dhona") +
      row("ready", "Legacy of Allag", "Ready · Mor Dhona · Story needs it") +
      row("blocked", mask21("Sidequest (Lv 95)"), mask21("a zone ahead") + " · waits for Kiri's story") +
      '<div class="more">9 more ›</div>' +
      '<div class="ga21">' + (live ? pill18(lv, ic15("071221", 16, "marker") + "Send 9 to Questionable", true) + qa19("Route") + qa19("Clear goal")
        : '<span class="gn21">Kiri is logged in on another client: her list is read-only here. Send it to Questionable from that client.</span>' + qa19("Route")) + "</div>");
  }

  function board_roster21() {
    var h = '<div class="board b15 b16"><h2>1.21 · All characters, and alt goals<small>P3: a third Characters view, one fixed-header table of every character, including those live in your other clients (read-only). N11: a goal per character, as a column. Full on Night.</small></h2>';
    h += '<div class="row">' + fig(look18("full", "night", rosterTop21("full") + roster21("full", ROS21, { hover: 4 }), 1240), "<b>The roster</b>, sorted by Story. Two lines per row, fixed height (44 px). <b>Live</b> rows are characters logged in on your other clients: they update every few seconds and are read-only here. Allowances use 1.19's projection for stored alts. Lists say what is left.") + "</div>";
    h += '<div class="row" style="align-items:flex-start">';
    h += '<div class="col21" style="width:440px">' + fig(look18("full", "night", '<div class="dp19">' + heroLine21("full") + "</div>", 430), "<b>Your other characters</b>, under the detail hero: one fixed-height line; the account table is its hover") +
      fig(look18("full", "night", heroTip21(), 340), "<b>The hover</b>: every character's state for this quest, evaluated offline from its snapshot") + "</div>";
    h += '<div class="col21" style="width:420px">' + fig(look18("full", "night", goalPop21("full"), 400), "<b>Set a goal</b> (N11): a popover from the Goal cell. The preview line counts what the goal adds before you set it. Clear goal has Undo.") +
"</div>";
    h += '<div class="col21" style="width:460px">' + fig(look18("full", "night", '<div class="dp19">' + goalCard21("full", false) + "</div>", 450), "<b>The goal card</b> on Kiri's dashboard: what is left, Ready first. A row past Kiri's story is masked (1.20's shield). Kiri is live in another client, so this client only reads.") +
      fig(look18("full", "night", '<div class="dp19">' + goalCard21("full", true).replace(/<div class="gr21">[\s\S]*?<div class="more">9 more ›<\/div>/, '<div class="more">13 left · 9 Ready</div>') + "</div>", 450), "<b>When Kiri is the character in this client</b>, at Full hand-offs: Send 9 to Questionable (the Ready ones)") + "</div>";
    h += "</div>";
    h += '<div class="row">' + fig(inSnow21(function () { return look18("quiet", "snow", rosterTop21("quiet") + roster21("quiet", ROS21.slice(0, 4), { sel: 3 }), 1240); }), "<b>Quiet, Ishgard Snow</b>, Kiri selected") + "</div>";
    return h + "</div>";
  }

  // ---------- P4 Do first and Set aside, N9 Your story ----------
  function blueRow21(lv, st, n, kinds, status, o) {
    o = o || {};
    if (o.ghost) return '<div class="br21 gh"><span class="g"></span><div class="b"><div class="t"><span class="nm">' + n + '</span></div><div class="s">Set aside for later · it leaves your counts</div></div><span class="ac"><span class="un">Undo</span></span></div>';
    return '<div class="br21' + (o.hov ? " hov" : "") + '"><span class="g">' + rowGlyph(lv, st) + '</span><div class="b"><div class="t"><span class="nm">' + n + "</span>" +
      kinds.map(function (k) { return '<span class="kd">' + k + "</span>"; }).join("") + (o.tier ? '<span class="kd">' + o.tier + "</span>" : "") +
      '</div><div class="s">' + status + '</div></div><span class="ac">' + (o.hov ? qa19("Teleport") + '<span class="mo">…</span>' : "") + "</span></div>";
  }
  function tierCard21(lv, title, why, cap, rows) {
    return sect19(lv, title, cap, '<div class="tw21">' + why + "</div>" + rows);
  }
  function bluesLeft21(lv) {
    return '<div class="bl21"><div class="blh">Clear my blues</div><div class="bls"><b>42 left</b> · 18 Ready · <span class="lk21">6 set aside ›</span></div>' +
      '<div class="blg"><span class="cap">Sort</span>' + seg21(["Story order", "Do first"], 1) + "</div>" +
      '<div class="blg"><span class="cap">Kinds</span><div class="kc">' + ["Duty 14", "Feature 9", "Job 11", "Area 3", "Mount 2"].map(function (k, i) { return '<span class="sc ' + (i < 4 ? "on" : "off") + '">' + k + "</span>"; }).join("") + "</div></div>" +
      '<div class="blg"><span class="cap">Show</span><div class="kc"><span class="sc off">Set aside</span><span class="sc on">Ready only</span></div></div></div>';
  }
  function doFirst21(lv) {
    var h = tierCard21(lv, "Story needs it", "The main scenario asks for these later. Do them first.", "2 left",
      blueRow21(lv, "ready", "Legacy of Allag", ["Duty"], "Ready · Mor Dhona · the Crystal Tower; Shadowbringers needs it", { hov: 1 }) +
      blueRow21(lv, "ready", "Ifrit Ain't Broke", ["Duty"], "Ready · The Waking Sands · Good Intentions needs it"));
    h += tierCard21(lv, "Opens content", "Dungeons, trials, areas and flying everyone uses.", "11 left",
      blueRow21(lv, "ready", "Maniac Manor", ["Duty"], "Ready · Mor Dhona · Haukke Manor (Hard)") +
      blueRow21(lv, "ready", "Out of Sight, Out of Mine", ["Duty"], "Copperbell Mines (Hard)", { ghost: 1 }) +
      blueRow21(lv, "ready", "Curds and Slay", ["Duty"], "Ready · Mor Dhona · Brayflox's Longstop (Hard)") + '<div class="more">8 more ›</div>');
    h += tierCard21(lv, "Systems", "Glamour, retainers, the Gold Saucer and other game features.", "6 left",
      blueRow21(lv, "ready", "A Self-improving Man", ["Feature"], "Ready · Mor Dhona · Cast Glamour, Glamour Plate") + '<div class="more">5 more ›</div>');
    h += '<div class="tf21"><span><b>High-end</b> · 3 left</span><span><b>Another job or society</b> · 20 left</span></div>';
    return h;
  }
  function confirm21(lv) {
    return '<div class="cf21"><b>Set aside 5 quests?</b><p>The Mor Dhona Alexander and Minstrel unlocks leave your counts, Nearby, the overlay and notices. The Set aside filter brings them back.</p><div class="a">' + pill18(lv, "Set aside 5") + qa19("Cancel") + "</div></div>";
  }
  function story21(lv) {
    var band = function (t, cap, body, cls) { return '<div class="sb21 ' + (cls || "") + '"><div class="sbh"><b>' + t + "</b><span>" + cap + "</span></div>" + (body || "") + "</div>"; };
    var line = function (st, n, s, chip) { return '<div class="sl21"><span class="g">' + rowGlyph(lv, st) + '</span><span class="nm">' + n + "</span>" + (chip ? '<span class="ch19">' + chip + "</span>" : "") + '<span class="sx">' + s + "</span></div>"; };
    var h = '<div class="ys21"><div class="ysh"><div class="yt">Your story</div><div class="yp">To the latest story: <b>832 quests</b>, Lv 50–100. <span class="est">About 52 evenings at your recent pace (estimate).</span></div></div>';
    h += band("A Realm Reborn · 2.0", "done", "", "fold");
    h += band("2.1 – 2.5 · Seventh Astral Era", "77 left · next: Laying the Foundation",
      '<div class="sa21">Opens after <b>The Ultimate Weapon</b></div>' +
      line("ready", "The Crystal Tower", "9 left · next: Legacy of Allag", "Story needs it") +
      line("ready", "Hard primals", "3 left · next: Ifrit Ain't Broke", "Story needs it") +
      line("ready", "Hildibrand Adventures", "13 left") +
      line("in-journal", "The Binding Coil of Bahamut", "11 left · in your journal") +
      line("completed", "Delivery Moogle Quests", "done") +
      '<div class="sa21">Opens after ' + mask21("Main scenario quest (Lv 50)") + "</div>" + '<div class="sm21">' + mask21("1 optional line · name hidden") + "</div>");
    h += band("Heavensward · 3.0", "94 main scenario quests · 7 optional lines", '<div class="sm21">' + mask21("Names hidden until you get there.") + "</div>", "ahead");
    h += band("3.1 – 3.5", "25 main scenario quests · 6 optional lines", "", "ahead");
    h += '<div class="ysf">' + qa19("Copy as checklist") + '<span>Prints the page as a checklist, names hidden the same way.</span></div></div>';
    return h;
  }
  function board_blues21() {
    var h = '<div class="board b15 b16"><h2>1.21 · My blues: Do first, Set aside, and Your story<small>P4: a tier for every unlock quest and a Do first order; Set aside, with Undo. N9: your story on one page, by patch, with the optional lines placed where they open. Ren, WAR 50. Full on Night.</small></h2><div class="row" style="align-items:flex-start">';
    h += '<div class="col21" style="width:300px">' + fig(look18("full", "night", bluesLeft21("full"), 290), "<b>The left column</b>: Sort (Story order · Do first) joins the existing filters; the summary counts what is left, and set-aside quests leave the count") +
      fig(look18("full", "night", confirm21("full"), 290), "<b>Setting aside a whole group</b> asks first. One quest doesn't: it has Undo.") +
      fig(look18("full", "night", '<div class="tt19"><b>Tiers</b><p><b>Story needs it</b>: the main scenario asks for it later.</p><p><b>Opens content</b>: a dungeon, trial, raid, area or flying.</p><p><b>Systems</b>: a game feature (glamour, retainers, the Gold Saucer).</p><p><b>High-end</b>: Extreme, Savage, Unreal, Ultimate.</p><p><b>Another job or society</b>: a job, class or allied society you don\'t play now.</p></div>', 290), "<b>What each tier means</b> (the tier chip's hover)") + "</div>";
    h += figw(look18("full", "night", '<div class="dp19">' + doFirst21("full") + "</div>", 620), "<b>Do first</b>: one card per tier, in tier order. A set-aside row keeps its place as one quiet line with <b>Undo</b> until the list is rebuilt, so nothing moves under the pointer. Hovered row: the reserved action slot.", 620);
    h += figw(look18("full", "night", story21("full"), 560), "<b>Your story</b> (N9): the main scenario by patch, each optional line placed at the quest that opens it. Past Ren's story point, counts only. The pace line is an estimate and hides below 15 dated quests.", 560);
    h += "</div>";
    h += '<div class="row" style="align-items:flex-start">' + fig(look18("full", "night", '<div class="sw21">' + seg21(["Clear my blues", "Your story"], 1) + '<span class="sb">My blues tab</span></div>', 360), "<b>The switch</b> at the top of My blues, as on Characters") +
      fig(look18("full", "night", '<div class="nr21"><span class="g">' + rowGlyph("full", "ready") + '</span><span class="nm">Legacy of Allag</span><span class="sx">Lv 50 · Story needs it</span></div><div class="nr21"><span class="g">' + rowGlyph("full", "ready") + '</span><span class="nm">Maniac Manor</span><span class="sx">Lv 50 · Opens content</span></div>', 360), "<b>Nearby and the Todo overlay</b>: the tier word after the level, in Tertiary") +
      fig(look18("full", "night", '<div class="yp" style="font-size:12px;color:var(--mist)">Your pace shows after 15 dated story quests. You have 9.</div>', 360), "<b>Thin data</b>: no estimate, said plainly") + "</div>";
    return h + "</div>";
  }

  // ---------- P5 named side stories, N8 Loose ends, N10 who's in it ----------
  function chainRow21(lv, icon, n, s, cls, extra) {
    return '<div class="cr21 ' + (cls || "") + '"><span class="i">' + icon + '</span><div class="b"><div class="nm">' + n + '</div><div class="sx">' + s + "</div></div>" + (extra || "") + "</div>";
  }
  function chains21(lv) {
    var sq = ic21("061411", 18, "tile");
    return sect19(lv, "Side stories", "3 lines to go",
      chainRow21(lv, sq, "Tataru's Grand Endeavor", "3 left · next: <b>Forever in Our Hearts</b> · Ready") +
      chainRow21(lv, sq, "Scholasticate Quests", "9 left · next: <b>Through the Grapevine</b> · Ready") +
      chainRow21(lv, sq, "Void Quests", "1 left · <b>A Bounty of Hunters</b> · Ready") +
      chainRow21(lv, sq, "Inconceivably Further Hildibrand Adventures", "<b class=\"cu21\">Caught up</b> · continues in a later patch", "caught") +
      chainRow21(lv, sq, "Cosmic Exploration Main Quests", "<b class=\"cu21\">Caught up</b> · continues in a later patch", "caught") +
      '<div class="more">7 lines done ›</div>');
  }
  function looseRow21(lv, icon, n, s, fin, o) {
    o = o || {};
    return '<div class="le21' + (o.hov ? " hov" : "") + '"><span class="i">' + icon + '</span><div class="b"><div class="t"><span class="nm">' + n + '</span></div><div class="s">' + (fin ? '<span class="ch19 fin">Finale</span>' : "") + s + '<span class="lf">' + (o.left || "1 left") + '</span></div></div><span class="ac">' + (o.hov ? qa19("Teleport") + '<span class="mo">…</span>' : "") + "</span></div>";
  }
  function loose21(lv, short) {
    var g = function (st) { return rowGlyph(lv, st, 14); };
    var rows = looseRow21(lv, ic21("062123", 20, "tile"), "Bard Quests", g("ready") + " <b>A Harmony from the Heavens</b>" + (short ? "" : " · Lv 80"), 1, { hov: !short }) +
      looseRow21(lv, ic21("061411", 20, "tile"), "Tales from the Shadows", g("ready") + " <b>One Final Journey</b>" + (short ? "" : " · Lv 80"), 1);
    if (!short) rows += looseRow21(lv, ic21("062123", 20, "tile"), "Physical Ranged DPS Role Quests (Endwalker)", g("ready") + " <b>Laid to Rest</b> · Lv 90", 1) +
      looseRow21(lv, ic21("061411", 20, "tile"), "Tataru's Grand Endeavor", g("ready") + " <b>Forever in Our Hearts</b> · Lv 90", 0, { left: "3 left" }) +
      looseRow21(lv, ic21("061411", 20, "tile"), "Scholasticate Quests", g("ready") + " <b>Through the Grapevine</b> · Lv 60", 0, { left: "9 left" });
    return sect19(lv, "Loose ends", "5 started, not finished", short ? '<div class="nar21">' + rows + "</div>" : rows);
  }
  function maskedStories21(lv) {
    var sq = ic21("061411", 18, "tile");
    return sect19(lv, "Side stories", "2 lines to go",
      chainRow21(lv, sq, "Hildibrand Adventures", "19 left · next: <b>Back in the Saddle</b> · Ready") +
      chainRow21(lv, sq, mask21("A side story ahead"), mask21("opens in a later expansion · name hidden"))) +
      sect19(lv, "Loose ends", "1 started, not finished",
        looseRow21(lv, ic21("062132", 20, "tile"), "Dark Knight Quests", rowGlyph(lv, "blocked", 14) + " " + mask21("Job quest (Lv 80) · a zone ahead"), 1)) +
      '<div class="wn21 sm"><div class="wnc">New chapters</div><div class="wnl">' + mask21("A side story ahead · 2 quests · name hidden") + "</div></div>" +
      '<div class="cl21 sm"><span class="a">' + mask21("A side story ahead") + " · next after this: " + mask21("Sidequest (Lv 90)") + "</span></div>";
  }
  function cast21(lv, which) {
    if (which === "met") return '<div class="hd19"><div class="n">Treasured Bonds</div><div class="m">Tataru\'s Grand Endeavor · Old Sharlayan · Lv 90</div><div class="st19"><span class="sw">Ready</span></div>' +
      '<div class="ca21">' + av21(lv, "face", { giver: "tataru" }, 20) + "<span>With <b>Tataru</b></span></div></div>";
    return '<div class="hd19"><div class="n">In the Middle of Nowhere</div><div class="m">Eden · Lakeland · Lv 80</div><div class="st19"><span class="sw dim">Blocked · after Shadowbringers</span></div>' +
      '<div class="ca21">' + av21(lv, "initials", { text: "T" }, 20) + av21(lv, "initials", { text: "U" }, 20) + av21(lv, "sil", { sil: "moon-disc" }, 20) + "<span>With <b>Thancred</b>, <b>Urianger</b> and a familiar face</span></div></div>";
  }
  function board_stories21() {
    var h = '<div class="board b15 b16"><h2>1.21 · Storylines: named side stories, Loose ends, and who\'s in it<small>P5 and N8 are cards on the Characters dashboard; N10 is a line under the detail hero, a hover line and a quick view. Michiru, BRD 100, unless noted. Full on Night.</small></h2><div class="row" style="align-items:flex-start">';
    h += '<div class="col21" style="width:500px">' + fig(look18("full", "night", '<div class="dp19">' + chains21("full") + "</div>", 490), "<b>P5, side stories</b> by the names players use. A series that comes out patch by patch reads <b>Caught up</b> in silver when you have done every quest so far; finished lines fold.") +
      fig(look18("full", "night", '<div class="wn21"><div class="wnh">What\'s new in 7.5</div><div class="wnc">New chapters</div><div class="wnl"><b>Inconceivably Further Hildibrand Adventures</b><span>2 quests</span><span class="lk21">Show ›</span></div></div>', 490), "<b>The new-chapter line</b>: once, on the first login of a patch, inside the What's new card that already opens then. No new notice.") +
      fig(look18("full", "night", '<div class="dp19">' + maskedStories21("full") + "</div>", 490), "<b>Masked</b> (Aki, at the end of Stormblood): a side story that opens later is named only once Aki gets there; a Loose ends row whose next quest lies past the story point names neither the quest nor its zone; the new-chapter line and the chain line say \"a side story ahead\".") +
      fig(look18("full", "night", '<div class="cl21"><span class="a">Tataru\'s Grand Endeavor · next after this: <b>Treasured Bonds</b></span></div>', 490), "<b>The chain line</b> in the detail pane takes the curated name (it read \"Story: Small Business, Big Dreams\"); totals stay in its hover") + "</div>";
    h += '<div class="col21" style="width:520px">' + fig(look18("full", "night", '<div class="dp19">' + loose21("full") + "</div>", 510), "<b>N8, Loose ends</b>: storylines you started and never finished. Finales the game marks only in yellow come first, with a <b>Finale</b> chip. Started means two quests done (one, for lines of up to four).") +
      fig(look18("full", "night", '<div class="tt19"><b>Settings › Alerts</b><div class="tg21r"><span>When a storyline\'s finale is Ready</span><span class="tog off"><i></i></span></div><p>One chat line and a Tonight line, once per finale. Off by default.</p></div>', 510), "<b>The optional finale notice</b>, off by default; the overlay section is off by default too") + "</div>";
    h += '<div class="col21" style="width:440px">' + fig(look18("full", "night", '<div class="dp19">' + cast21("full", "met") + "</div>", 430), "<b>N10, who's in it</b>: a Cast line under the hero with 20 px faces (1.15 plates)") +
      fig(look18("full", "night", '<div class="dp19">' + cast21("full", "unmet") + "</div>", 430), "<b>Spoiler-safe</b> (Aki, at the end of Stormblood): only characters Aki has met in the story are named. Ryne is \"a familiar face\", with the moon disc, never her face.") +
      fig(look18("full", "night", '<div class="qv21"><div class="qvh">Quick views</div><div class="tg21r"><span><b>With story characters</b><small>Side quests where someone from the story appears</small></span><span class="tog on"><i></i></span></div></div>', 430), "<b>The quick view</b> in the filter drawer; the row hover gains \"With Thancred and Urianger\"") + "</div>";
    return h + "</div></div>";
  }

  // ---------- P6 Triple Triad, P7 the zones board ----------
  function ttRow21(lv, n, place, s, o) {
    o = o || {};
    return '<div class="tt21' + (o.hov ? " hov" : "") + '"><span class="i">' + ic21("060156", 20, "tile") + '</span><div class="b"><div class="t"><span class="nm">' + n + '</span><span class="pl">' + place + '</span></div><div class="s">' + s + '</div></div><span class="ac">' + (o.hov ? qa19(o.act || "Teleport") + '<span class="mo">…</span>' : "") + "</span></div>";
  }
  function triad21(lv) {
    var grp = function (t, sub) { return '<div class="tgh21"><b>' + t + "</b><span>" + sub + "</span></div>"; };
    return sect19(lv, "Triple Triad", "11 opponents to unlock",
      '<div class="tc21">' + ["Plays you 2", "Locked 11", "Cards left 9"].map(function (k) { return '<span class="sc on">' + k + "</span>"; }).join("") + "</div>" +
      grp("Plays you", "cards you don't have") +
      ttRow21(lv, "Celia", "Old Sharlayan", "3 cards you don't have", { hov: 1 }) + ttRow21(lv, "Ercanbald", "Rhalgr's Reach", "beaten · 2 cards left") +
      grp("Locked behind a quest", "the game never says which") +
      ttRow21(lv, "Elaisse", "The Pillars", "after " + rowGlyph(lv, "ready", 14) + " <b>Caught in the Act</b> · Ready") +
      ttRow21(lv, "Gyoei", "Yanxia", "after " + rowGlyph(lv, "blocked", 14) + " <b>Criminal Phrenology</b> · Blocked: All the Little Angels first") +
      ttRow21(lv, "Camalbusert", "Old Sharlayan", "after " + rowGlyph(lv, "blocked", 14) + " <b>A Spellbinding Read</b> · Blocked: An Odd Job first") +
      ttRow21(lv, mask21("An opponent ahead"), mask21("a zone ahead"), "after " + mask21("Main scenario quest (Lv 90)")) +
      '<div class="more">6 more past Kiri\'s story · names hidden</div>' +
      '<div class="dua">' + qa19("Route to 3 opponents") + qa19("Pin the quests") + "</div>");
  }
  function zoneRow21(lv, n, lvl, s, o) {
    o = o || {};
    return '<div class="zr21' + (o.hov ? " hov" : "") + '"><div class="b"><div class="t"><span class="nm">' + n + '</span><span class="lv">' + lvl + "</span>" + (o.chip ? '<span class="ch19">' + o.chip + "</span>" : "") + '</div><div class="s">' + s + '</div></div><span class="ac">' + (o.hov ? qa19("Teleport") + '<span class="mo">…</span>' : "") + "</span></div>";
  }
  function nearby21(lv) {
    var h = '<div class="nb21"><div class="nbh"><b>Nearby quests</b>' + seg21(["Here", "Everywhere"], 1) + "</div>" +
      '<div class="nbc"><div class="kc">' + ["Ready", "Blues", "Side stories", "Rewards"].map(function (k, i) { return '<span class="sc ' + (i === 2 ? "off" : "on") + '">' + k + "</span>"; }).join("") + '</div><span class="srt">Sort: Level fit ▾</span></div>';
    h += '<div class="xg21"><b>Endwalker</b><span>Lv 80–90 · fits SGE 90</span></div>';
    h += zoneRow21(lv, "Thavnair", "Lv 80–90", "4 Ready · 9 blues · 3 rewards", { hov: 1 }) +
      zoneRow21(lv, "Labyrinthos", "Lv 80–90", "2 Ready · 3 blues · 1 reward") +
      zoneRow21(lv, "Elpis", "Lv 86–90", "6 Ready · 1 reward", { chip: "new since 7.4" }) +
      zoneRow21(lv, "Garlemald", "Lv 83–90", "1 Ready · 2 blues") + '<div class="more">2 zones with nothing left ›</div>';
    h += '<div class="xg21"><b>Shadowbringers</b><span>12 Ready across 6 zones ›</span></div>';
    h += '<div class="xg21 ahead"><b>Dawntrail</b><span>' + mask21("6 zones past Kiri's story · names hidden") + "</span></div></div>";
    return h;
  }
  function nearbySora21(lv) {
    return '<div class="nb21"><div class="xg21"><b>Endwalker</b><span>Lv 80–90 · fits AST 82</span></div>' +
      zoneRow21(lv, "Labyrinthos", "Lv 80–90", "3 Ready · 4 blues · 1 reward") +
      zoneRow21(lv, mask21("A zone ahead"), "Lv 80–90", "waits for your story") +
      zoneRow21(lv, mask21("A zone ahead"), "Lv 83–90", "waits for your story") + '<div class="more">3 more zones ahead</div></div>';
  }
  function board_boards21() {
    var h = '<div class="board b15 b16"><h2>1.21 · Triple Triad opponents, and the zones board<small>P6: a Triple Triad card on the Characters dashboard, after Duties; the quest behind each opponent. P7: Nearby\'s Everywhere view. Kiri, SGE 90, at the end of Endwalker. Full on Night.</small></h2><div class="row" style="align-items:flex-start">';
    h += figw(look18("full", "night", '<div class="dp19">' + triad21("full") + "</div>", 600), "<b>P6</b>: opponents that play you and have cards you lack, then the ones behind a quest, with that quest's state and what blocks it. In the game data, 106 opponents wait on a quest the game never names. Opponents past the story point are masked: name, place and quest.", 600);
    h += '<div class="col21" style="width:440px">' + fig(look18("full", "night", nearby21("full"), 430), "<b>P7, Everywhere</b>: every zone with something left, by expansion, with what is left in words. The kind chips and the sort are one fixed row. Hovered row: Teleport; its \"…\" holds the nearest Ready giver, Show in Journal and, at Full hand-offs, Send zone to Questionable.") +
      fig(look18("full", "night", '<div class="dp19">' + sect19("full", "Unlocks", "", '<div class="ul21">' + ic21("060156", 18, "tile") + '<span>Triple Triad opponent · <b>Elaisse</b>, The Pillars</span></div>') + "</div>", 430), "<b>The detail pane</b> of Caught in the Act names the opponent it unlocks") +
      fig(look18("full", "night", '<div class="dp19">' + sect19("full", "Unlocks", "", '<div class="ul21">' + ic21("060156", 18, "tile") + "<span>Triple Triad opponent · " + mask21("An opponent ahead, a zone ahead") + "</span></div>") + "</div>", 430), "<b>Masked</b>: the same line for an opponent past the story point") +
      fig(look18("full", "night", nearbySora21("full"), 430), "<b>Masked zones in a revealed expansion</b> (Sora, AST 82, early in Endwalker): zones the story hasn't reached read \"A zone ahead\" with their level span") + "</div>";
    h += '<div class="col21" style="width:420px">' + fig(look18("full", "night", '<div class="ro19"><div class="rh">' + ic21("060156", 22, "tile") + '<b>Route to 3 Triple Triad opponents</b><span>5 stops</span></div><div class="rr"><span class="n">1</span>Caught in the Act<span class="m">Ishgard · Ready</span></div><div class="rr"><span class="n">2</span>Elaisse · Triple Triad<span class="m">The Pillars</span></div><div class="rr"><span class="n">…</span>3 more: 2 quests, 1 opponent<span class="m"></span></div></div>', 410), "<b>Route</b> over the unlock quests and the opponents, as 1.19's Route to unlock") +
      fig(inSnow21(function () { return look18("quiet", "snow", '<div class="dp19">' + triad21("quiet").replace(/<div class="tgh21"><b>Locked[\s\S]*$/, "") + "</div></div>", 410); }), "<b>Quiet, Ishgard Snow</b>") + "</div>";
    return h + "</div></div>";
  }

  // ---------- P8 chat lines ----------
  function chat21(lines) {
    return '<div class="chx21">' + lines.map(function (l) { return '<div class="cl"><span class="px">[Tsukimichi]</span> ' + l + "</div>"; }).join("") + "</div>";
  }
  function board_chat21() {
    var h = '<div class="board b15 b16"><h2>1.21 · /tsuki msq, /tsuki next and /tsuki go<small>P8: one plain sentence or two, which text-to-speech reads well: no glyphs or symbols, full stops, numbers as digits, places as the game names them. Lines go to the plugin\'s echo channel only.</small></h2>';
    var bg = function (inner) { return '<div class="chb21" style="background-image:url(\'../../../plan-site/mock/scene-night.jpg\')">' + inner + "</div>"; };
    h += '<div class="row" style="align-items:flex-start"><div class="col21" style="width:760px">';
    h += fig(bg(chat21(["Main scenario: Dawntrail, at The Long Road to Xak Tural. 48 quests left in this expansion, and 91 to the latest story, levels 95 to 100."])), "<b>/tsuki msq</b> (Yuna). What is left, never a percentage.");
    h += fig(bg(chat21(["Main scenario: caught up. The story continues in a later patch."])), "<b>/tsuki msq</b> when caught up (Michiru)");
    h += fig(bg(chat21(["Next: The Long Road to Xak Tural, step 3. Speak with Erenville. Shaaloani, X 27, Y 34.8. You are in Tuliyollal."])), "<b>/tsuki next</b> for a quest in your journal: the current step (P2), in the game's own words");
    h += fig(bg(chat21(["Next: Caught in the Act. Talk to Elaisse in The Pillars, X 7.8, Y 10.8, about 60 yalms south-west of you."])), "<b>/tsuki next</b> for a Ready quest in the same zone: distance in tens of yalms and one of eight directions");
    h += fig(bg(chat21(["Going to step 3 of The Long Road to Xak Tural: Erenville, Shaaloani, X 27, Y 34.8. Teleporting first."])), "<b>/tsuki go</b> (P2), from a macro: travel at your automation level; <code>/tsuki stop</code> stops it");
    h += "</div><div class=\"col21\" style=\"width:640px\">";
    h += fig(bg(chat21(["Next: a main scenario quest at level 83, in a zone ahead of your story."])), "<b>Spoiler shield</b>: masked names stay masked, in words");
    h += fig(bg(chat21(["Next: nothing is Ready on this job. 4 quests are Ready on another job."])), "<b>Nothing to do</b> on this job");
    h += fig(bg(chat21(["Step done. Next: step 4. Speak with Erenville again. X 26.7, Y 31, about 70 yalms north of you."])), "<b>Say what's next in chat</b> (opt-in): one line when a step or quest finishes, at most one every 10 s");
    h += fig(look18("full", "night", '<div class="tt19"><b>Settings › In game › Chat</b><div class="tg21r"><span><b>Say what\'s next in chat</b><small>After each step, the /tsuki next line, for text-to-speech.</small></span><span class="tog off"><i></i></span></div><div class="smp21">Example: Next: Caught in the Act. Talk to Elaisse in The Pillars, X 7.8, Y 10.8.</div></div>', 620), "<b>The setting</b>, off by default, with a sample line");
    h += fig(look18("full", "night", '<div class="tt19"><b>The format</b><p>The plugin\'s prefix, then sentences. Coordinates as "X 27, Y 34.8" (one decimal, no trailing zero). Distances in tens of yalms with one of eight compass words, only in the same zone. No ☾, ·, ›, arrows or brackets in the text. The quest name is a chat link with plain text, so a reader speaks the name.</p></div>', 620), "") + "</div></div>";
    return h + "</div>";
  }

  // ---------- every look ----------
  function board_looks21() {
    var h = '<div class="board b15 b16"><h2>1.21 · Every look<small>Up next (Yuna, in journal) and Loose ends (two finales) at each Decoration level on Night, Ishgard Snow, Dawn and Kugane Lacquer.</small></h2><div class="lg18">';
    h += "<div></div>" + ["Night", "Ishgard Snow", "Dawn", "Kugane Lacquer"].map(function (n) { return '<div class="lh">' + n + "</div>"; }).join("");
    ["full", "quiet", "plain"].forEach(function (lv) {
      h += '<div class="lr">' + lv.charAt(0).toUpperCase() + lv.slice(1) + "</div>";
      ["night", "snow", "dawn", "kugane"].forEach(function (p) {
        SNOW = p === "snow";
        h += look18(lv, p, '<div class="dp19">' + (lv === "plain" ? '<div class="ps">Tonight<span></span></div>' + up21(lv, "journal") : card(lv, "<h4>Tonight</h4>" + up21(lv, "journal"))) + loose21(lv, true) + "</div>", 400);
      });
    });
    SNOW = false;
    return h + "</div></div>";
  }

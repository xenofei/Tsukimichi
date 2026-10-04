  // ======================================================================================================
  // 1.20 "Before Evercold": N6 the wider spoiler shield, N7 the Before Evercold card, F4 the portrait pack
  // (spec-1.20.md). Built from the 1.15–1.19 parts: card(), sect19(), badge19(), qa19(), pill18(), look18(),
  // plate()/face(), ic15(). Injected by 1.20/mock-src/build.py; nothing in the shared mock-src files changes.
  // Round 2 applies the realism supervisor's review (fixed row heights, the shipped toggle and Hold button, the
  // checkbox outline, single-line placeholders with non-breaking locators, the second looks board).
  // ======================================================================================================
  // The portrait pack's photos (Garland Tools NPC renders, credit Celes), head-cropped to the 1.15 framing rule:
  // crown 8–12 %, eye line 42–46 %, chin 78–84 %. 380 × 706 full-body renders on alpha.
  SRC.pack = { w: 380, h: 706, crop: [144, 106, 96, 96], fam: "colour", label: "Garland Tools photo · credit Celes" };
  GIVERS.buscarron = { name: "Buscarron", file: "../1.20/art/Enpc_1000590", src: "pack", crop: [146, 107, 92], place: "Buscarron's Druthers · South Shroud" };
  GIVERS.isembard = { name: "Isembard", file: "../1.20/art/Enpc_1003929", src: "pack", crop: [150, 113, 84], place: "Eastern Thanalan" };
  function onPal(pal, fn) { var s = SNOW; SNOW = pal === "snow"; try { return fn(); } finally { SNOW = s; } }
  // The hidden-reward tile: the 1.15 moon disc (a .32 disc and its lit crescent), 14 px across on a 22 px tile.
  // On Ishgard Snow it takes the 1.16 fallback ink #56607C.
  function hid20() {
    var ink = SNOW ? "#56607C" : "#C9D3EA";
    return '<span class="hid20"><svg viewBox="0 0 22 22" width="22" height="22"><g fill="' + ink + '" opacity="' + (SNOW ? 1 : 0.86) + '"><circle cx="11" cy="11" r="7" opacity=".32"/><path d="M11 4A7 7 0 0 1 11 18A3.7 7 0 0 0 11 4Z"/></g></svg></span>';
  }
  // Pack photos live in 1.20/art; the shared plate() prefixes 1.15/art/, so the path is rewritten here.
  function pface(size, lv, g) { return face(size, lv, g).split("1.15/art/../1.20/").join("1.20/"); }
  // Silhouettes on Ishgard Snow are drawn in #56607C (spec-1.16 fallbacks rule); the shared plate() predates that.
  function sil20(size, lv, sil) {
    var h = plate(size, lv, "sil", { sil: sil });
    if (!SNOW) return h;
    var id = "s20" + (++uid);
    return h.replace("<defs>", '<defs><filter id="' + id + '" color-interpolation-filters="sRGB"><feFlood flood-color="#56607C"/><feComposite in2="SourceAlpha" operator="in"/></filter>')
      .replace('silhouettes/' + sil + '.svg"', 'silhouettes/' + sil + '.svg" filter="url(#' + id + ')"');
  }
  // Locators never break from their number ("area 6", "Lv 97", "Patch 7.5").
  function nb(t) { return t.replace(/(area|Lv|Patch|pack|portraits-) (\d)/g, "$1&nbsp;$2"); }
  function ph(t) { return '<span class="ph20">' + nb(t) + "</span>"; }
  // Chrome.MoonToggle (36 × 20): a disc on the sunken track when off; a gold crescent on a gold wash when on; silver at
  // Quiet; at Plain a checkbox.
  function tg(on, lv) {
    lv = lv || "full";
    if (lv === "plain") return '<span class="cb20' + (on ? " on" : "") + '">' + (on ? CHK : "") + "</span>";
    var silver = lv === "quiet";
    var acc = silver ? "var(--silver)" : (SNOW ? "#A07B25" : "var(--moon)");
    var track = on ? 'style="background:color-mix(in srgb,var(--sunk),' + acc + ' ' + (silver ? 30 : 38) + '%);box-shadow:inset 0 0 0 1px ' + (silver ? "color-mix(in srgb,var(--silver) 55%,transparent)" : acc) + '"' : "";
    var knob = on ? '<svg viewBox="0 0 20 20" width="20" height="20" style="position:absolute;right:0;top:0"><circle cx="10" cy="10" r="7" fill="' + acc + '"/><circle cx="6.15" cy="7.55" r="5.74" fill="color-mix(in srgb,var(--sunk),' + acc + ' ' + (silver ? 30 : 38) + '%)"/></svg>'
      : '<svg viewBox="0 0 20 20" width="20" height="20" style="position:absolute;left:0;top:0"><circle cx="10" cy="10" r="7" fill="var(--mist)"/></svg>';
    return '<span class="mt20" ' + track + ">" + knob + "</span>";
  }
  var CHK = '<svg viewBox="0 0 12 12"><path d="M2 6.5l2.6 2.6L10 3.4" stroke="currentColor" stroke-width="1.8" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>';

  // ---------- N6: the masked quest in the detail pane ----------
  function giver20(lv, size) {
    if (lv === "plain") return '<div class="ps">Giver<span></span></div><dl class="kv"><dt>Giver</dt><dd>' + sil20(18, lv, "hyur-female") + ph("Dawntrail character") + '<span class="mu">· ' + nb("Dawntrail area 6") + "</span></dd></dl>";
    return sect19(lv, "Giver", "", '<div class="gv20">' + sil20(size || (lv === "quiet" ? 64 : 72), lv, "hyur-female") + '<div class="gt"><b class="ph20">Dawntrail character</b><span>' + nb("Dawntrail area 6") + "</span></div></div>");
  }
  function shieldDetail(lv) {
    var h = '<div class="hd19 hd20"><div class="n ph20">' + nb("Main scenario quest (Lv 97)") + '</div><div class="m">Main Scenario › Dawntrail · ' + ph("Dawntrail area 6") + " · " + nb("Lv 97") + "</div>" +
      '<div class="st19"><span class="sw dim" style="color:var(--mist);font-weight:500">Blocked · 9 quests before it</span></div>' +
      '<div class="note">Names hidden: this quest is further ahead than you are.</div><div class="rqa" style="margin-top:6px">' + qa19("Reveal names in this quest") + "</div></div>";
    h += giver20(lv);
    h += sect19(lv, "How you'll clear it", "1 duty",
      '<div class="dt19"><div class="dn ph20">' + nb("Dungeon (Lv 97)") + '</div><div class="bgs">' + badge19("000089", "Solo with NPCs") + badge19("", "Story-required", "q") + "</div></div>");
    h += sect19(lv, "Rewards", "2 items",
      '<div class="rwl20" style="margin-top:0">' + hid20() + ph("An item") + '<span class="k">optional, 1 of 3</span></div>' +
      '<div class="rwl20">' + hid20() + ph("An orchestrion roll") + "</div>");
    h += sect19(lv, "Unlocks", "", '<div class="rwl20">' + ic15("060453", 18, "marker") + ph("Dawntrail aetheryte · area 6") + '</div><div class="rwl20">' + ic15("000046", 18, "tile") + ph("Trial (Lv 99)") + "</div>");
    return '<div class="dp19">' + h + "</div>";
  }
  function shieldRows(after) {
    var R = [
      ["Main scenario quest (Lv 97)", "Heritage Found", "Dawntrail area 6", "Vanguard", "Dungeon (Lv 97)", "Wuk Lamat"],
      ["Main scenario quest (Lv 99)", "Heritage Found", "Dawntrail area 6", "Origenics", "Dungeon (Lv 99)", "Wuk Lamat"],
      ["Main scenario quest (Lv 100)", "Living Memory", "Dawntrail area 8", "Alexandria", "Dungeon (Lv 100)", "Sphene"],
      ["Main scenario quest (Lv 100)", "Living Memory", "Dawntrail area 8", "The Interphos", "Trial (Lv 100)", "Sphene"]
    ];
    var h = '<div class="tb20"><div class="th"><span></span><span>Name</span><span>Zone</span><span>Unlocks</span><span>Giver</span></div>';
    R.forEach(function (r) {
      h += '<div class="tr"><span class="g">' + rowGlyph("full", "blocked") + '</span><span class="nm">' + nb(r[0]) + "</span>" +
        (after ? ph(r[2]) + ph(r[4]) + (r[5] === "Wuk Lamat" ? "<span>Wuk Lamat</span>" : ph("Dawntrail character")) : '<span class="leak">' + r[1] + '</span><span class="leak">' + r[3] + '</span><span class="leak">' + r[5] + "</span>") + "</div>";
    });
    return h + "</div>";
  }
  function spoilerSettings(lv) {
    var row = function (lab, ctl, note, cls) { return '<div class="sr20 ' + (cls || "") + '">' + (cls === "sub" ? "<i></i>" : "") + '<span class="sl">' + lab + '</span><div class="sm"><div class="ss">' + ctl + "</div>" + (note ? '<div class="sn">' + note + "</div>" : "") + "</div><div class=\"sa\"></div></div>"; };
    var h = '<div class="set20 sp20"><div class="sh"><b>Spoilers</b><span>Settings › Spoilers</span></div>';
    h += row("Hide story names ahead", tg(true, lv), "Main scenario quests past yours read <b>Main scenario quest (Lv&nbsp;83)</b> everywhere.");
    h += row("Quests ahead to reveal", '<span class="sl20"><i></i>3</span>', "How many main scenario quests past your current one keep their names.", "sub");
    h += row("Also hide places, duties, rewards and people", tg(true, lv), "Areas, aetherytes, duties, quest rewards and characters from the story past yours read <b>" + nb("Dawntrail area 6") + "</b>, <b>" + nb("Dungeon (Lv 97)") + "</b>, <b>An orchestrion roll</b>, <b>Dawntrail character</b>. Unlocks, Path, Route, the Duties board, tooltips and search follow it.", "sub");
    h += row("Hide journal artwork", tg(true, lv), "A quest's banner art shows once the quest is in your journal or done.");
    h += row("For Michiru", '<span class="sg20"><span class="on">As above</span><span>Always shield</span><span>Show everything</span></span>', "A character who finished the story can show everything while alts stay shielded.<div class=\"tt20\">212 story names and 486 other names hidden for Michiru.</div>");
    return h + "</div>";
  }
  function boardShield() {
    var h = '<div class="board b15 b16 b20"><h2>1.20 · A wider spoiler shield<small>N6. Past your story point, places, duties, rewards and people print a placeholder in Secondary: a kind word and a safe locator, never a glyph. Faces stay hidden (1.15). Example: Michiru, in Dawntrail before Heritage Found.</small></h2><div class="row">';
    h += '<div class="col" style="width:420px">' + fig(look18("full", "night", shieldDetail("full"), 420), "<b>The detail pane</b>, Full on Night: a masked quest; its title, giver, place, duty, rewards and unlocks print in Secondary") + "</div>";
    h += '<div class="col" style="width:660px">';
    h += fig(look18("full", "night", '<div class="tb20"><div class="cap">1.19: the names leak</div></div>' + shieldRows(false) + '<div class="tb20"><div class="cap" style="margin-top:12px">1.20: one vocabulary everywhere</div></div>' + shieldRows(true), 660), "<b>Table rows</b>: Zone, Unlocks and Giver. Cells are single-line with an ellipsis. Wuk Lamat keeps her name (met before); real Dawntrail names and levels, illustrative links");
    h += fig(look18("full", "night", spoilerSettings("full"), 660), "<b>Settings › Spoilers</b> with the shipped moon toggles: one new switch under Hide story names ahead, on by default; the count line covers both");
    h += "</div>";
    h += '<div class="col" style="width:410px">';
    h += fig(look18("full", "night", '<div class="tip20"><b>Hidden by the spoiler shield</b><p>It\'s from the story past yours.</p><p class="t">Right-click to reveal it for this session.</p></div>' +
      '<div style="height:10px"></div><div class="cm20"><div class="on">Reveal this name<span>this session</span></div><div>Reveal names in this quest</div><hr><div>Open on Garland Tools…<span>asks first</span></div></div>', 410, ";background:transparent"), "<b>Hover and right-click</b> on any placeholder: words, no glyph; reveal lasts the session, like quests today");
    h += fig(look18("full", "night", '<div class="ro19 ro20"><div class="rh">' + ic15("071201", 22, "marker") + '<b>Route · 3 stops</b><span>Main scenario</span></div>' +
      '<div class="rr"><span class="n">1</span>' + sil20(24, "full", "hyur-female") + ph("Dawntrail character") + '<span class="m">' + ph("area 6") + "</span></div>" +
      '<div class="rr"><span class="n">2</span>' + sil20(24, "full", "viera-female") + ph("Dawntrail character") + '<span class="m">' + ph("area 6") + "</span></div>" +
      '<div class="rr"><span class="n">3</span>' + face(24, "full", "alphinaud") + '<span>Alphinaud</span><span class="m">Tuliyollal</span></div></div>', 410),
      "<b>Route and Next stops</b>: a person you have met keeps the name; one only the story ahead introduces doesn't. Masked quests never show a face");
    h += fig(look18("full", "night", '<div class="se19 se20"><div class="sin">' + ICON.search + '<span>flying</span></div><div class="sgh">Unlocks</div>' +
      '<div class="sres"><span class="k">Flying</span><b>Flying in Kozama\'uka</b><span class="via">done</span></div>' +
      '<div class="sres"><span class="k">Flying</span><b class="ph20">' + nb("Flying in Dawntrail area 6") + '</b><span class="via">6 left</span><span class="rt">' + qa19("Route to unlock") + "</span></div></div>", 410),
      "<b>Find by unlock</b> (K3): a string holding a placeholder is Secondary as a whole; hidden names match only their placeholder");
    h += fig(look18("full", "night", '<div class="vo20"><div class="h">Kind</div><div class="h">Placeholder</div>' +
      '<div class="k">Main scenario quest</div><div>' + nb("Main scenario quest (Lv 97)") + ' <i style="font-style:normal;color:var(--dusk)">words unchanged; now Secondary</i></div>' +
      '<div class="k">Area, city</div><div>' + nb("Dawntrail area 6") + '</div><div class="k">Aetheryte</div><div>' + nb("Dawntrail aetheryte · area 6") + "</div>" +
      '<div class="k">Duty</div><div>' + nb("Dungeon (Lv 97) · Trial (Lv 99) · Alliance raid (Lv 100)") + "</div>" +
      '<div class="k">Reward</div><div>An item · A mount · A minion · An emote · An orchestrion roll · A title</div>' +
      '<div class="k">Person</div><div>Dawntrail character</div></div>', 410), "<b>The vocabulary</b>: kind word plus a safe locator (expansion, level, area number)");
    h += "</div></div></div>";
    return h;
  }

  // ---------- N7: the Before Evercold card ----------
  var BE = {
    story: { icon: ic15("071201", 18, "marker"), t: "Finish the main story", d: "14 quests left, through Patch 7.5", a: [qa19("Show next"), qa19("Route")] },
    room: { icon: ICON.book, t: "Room in your journal", d: "27 of 30 slots used. Evercold brings new quests.", a: [qa19("Make room")] },
    jobs: { icon: ic15("062122", 18, "tile"), t: "Job and role quests", d: "Your DRG, SGE and CUL have quests you can take now", a: [qa19("Show them")] },
    duties: { icon: ic15("000046", 18, "tile"), t: "Duties for the roulettes", d: "2 Dawntrail duties not unlocked yet", a: [qa19("Duties board")] },
    fly: { icon: ic15("000122", 18, "tile"), t: "Flying in Dawntrail", d: "2 areas left: Shaaloani, " + ph("Dawntrail area 6"), a: [qa19("Route")] }
  };
  function beLine(L, st) {
    // st: "" open; "you" ticked by you this session: the line keeps its place, its height and its (now hidden) buttons
    var cb = '<span class="cb20' + (st ? " on" : "") + '">' + (st ? CHK : "") + "</span>";
    // "you said so" takes the buttons' own slot (a fixed column in the wide form, a fixed row in the narrow one), so
    // the line keeps its height and its detail keeps its wrap.
    var slot = st === "you" ? '<em class="ys">you said so</em>' : L.a.join("");
    return '<div class="bl' + (st ? " done" : "") + '">' + cb + '<span class="bi">' + L.icon + '</span><div class="bt"><b>' + L.t + "</b><span>" + nb(L.d) + '</span></div><div class="ba">' + slot + "</div></div>";
  }
  function beCard(lv, o) {
    o = o || {};
    var sub = o.sub || "For Michiru · early access 22 Jan (expected)";
    var head = '<div class="bh"><b>Before Evercold</b><span class="x" title="Hide for this character">' + ICON.x + "</span></div>" + '<div class="bs">' + sub + "</div>";
    var body = o.body || (beLine(BE.story) + beLine(BE.room) + beLine(BE.duties, "you") + beLine(BE.fly) + '<div class="bf">Done: Job and role quests</div>');
    var nar = o.nar ? " nar" : "";
    if (lv === "plain") return '<div class="be20 plain' + nar + '"><div class="ps">Before Evercold<span class="x">' + ICON.x + "</span></div>" + '<div class="bs">' + sub + "</div>" + body + "</div>";
    return '<div class="be20' + nar + '">' + card(lv, head + body) + "</div>";
  }
  function tonight(lv, inner) {
    return '<div class="tn20">' + card(lv, '<div class="tnh">' + ICON.moon + "<b>Tonight</b></div>" +
      '<div class="tl">6 quests ready<span class="r">' + qa19("Show them") + "</span></div>" +
      '<div class="tl">' + rowGlyph("full", "ready") + "Next in the story: <span>" + nb("Lv 94") + " · Shaaloani</span></div>" +
      '<div class="tl">Running now: <span>Make It Rain Campaign</span></div><div class="gap"></div>' + inner +
      '<div class="gap"></div><div class="tl" style="border-bottom:0">' + rowGlyph("full", "ready") + "Pinned: A Pup No Longer<span>Ready · " + nb("Lv 15") + "</span></div>") + "</div>";
  }
  function boardPrep() {
    var h = '<div class="board b15 b16 b20"><h2>1.20 · Before Evercold<small>N7. One card per character from 1.20 until Evercold\'s launch day: what the game\'s quests need before 8.0. Lines that apply only; done lines fold into one line when the card is next built; nothing moves while you look. No copper: nothing here is urgent.</small></h2><div class="row">';
    h += fig(look18("full", "night", tonight("full", beCard("full")), 540), "<b>In Tonight</b>, Full on Night (card ≥ 460 px: buttons trail). You ticked <b>Duties</b>: \"you said so\", and its button slot stays, empty, so nothing moves until the card is next built");
    h += '<div class="col" style="width:480px">';
    h += fig(look18("full", "night", '<div class="chd20"><b>Kiri Tsukikage</b><span>Characters › stored · last login 2 days ago</span></div>' +
      beCard("full", { sub: "As of the last login, 2 days ago · early access 22 Jan (expected)", body:
        beLine({ icon: ic15("071201", 18, "marker"), t: "The main story", d: "You're in Stormblood: 588 quests to Evercold. No rush; the lines below matter first.", a: [qa19("Show next")] }) +
        beLine({ icon: ic15("062136", 18, "tile"), t: "Job and role quests", d: "Your BLU and WHM have quests you can take now", a: [qa19("Show them")] }) +
        '<div class="bf">Done: Room in your journal</div>' }), 480), "<b>In the Characters dashboard</b>: any stored character, from its last snapshot. Only the lines that apply (no Dawntrail lines for a Stormblood alt)");
    var pair = function (st) { return beCard("full", { nar: 1, sub: "For Michiru", body: beLine(BE.room, st) }); };
    h += fig(look18("full", "night", '<div class="pair20">' + pair("") + pair("you") + "</div>", 480), "<b>A tick, before and after</b> (narrow card): the same height; the buttons' row stays reserved");
    h += fig(look18("full", "night", beCard("full", { body: '<div class="ok"><span style="color:var(--goldtx);display:flex">' + CHK + "</span>Ready for Evercold. Nothing left on Michiru.</div>" }), 480), "<b>All done</b>: one line with the gold check (finished); × hides it");
    h += "</div>";
    h += '<div class="col" style="width:480px">';
    h += fig(look18("full", "night", '<div class="ut20">Before Evercold hidden for Michiru<span class="u">Undo</span></div>', 340, ";background:transparent"), "<b>×</b> (24 px target) hides the card for this character; the 8 s Undo toast floats above the status bar");
    h += fig(look18("full", "night", '<div class="chd20"><b>Michiru Tsukikage</b><span>Characters</span></div><div class="stub20">Before Evercold is hidden for Michiru.' + qa19("Show again") + "</div>", 480), "<b>Michiru's dashboard</b> after a dismiss: one quiet line, the way back");
    h += fig(look18("full", "night", '<div class="tip20" style="width:380px"><b>Room in your journal</b><p>Done when at least 10 of 30 slots are free. Evercold\'s launch adds new side, job and unlock quests; the main scenario never takes a slot.</p><p class="t">Tick it yourself if you keep your journal full on purpose.</p></div>', 420, ";background:transparent"), "<b>Hover a line</b>: why it is there and when it counts as done");
    h += fig(look18("full", "night", '<div class="vo20"><div class="h">Line</div><div class="h">Done when (the game says so)</div>' +
      '<div class="k">Finish the main story</div><div>The last 7.x main scenario quest is complete</div>' +
      '<div class="k">Room in your journal</div><div>10 or more free slots (C9\'s count)</div>' +
      '<div class="k">Job and role quests</div><div>No job or role quest is Ready for this character</div>' +
      '<div class="k">Duties for the roulettes</div><div>Every 7.x dungeon, trial and raid is unlocked (N4)</div>' +
      '<div class="k">Flying in Dawntrail</div><div>Flying unlocked in every Dawntrail area</div></div>', 480), "<b>The five lines</b>: each one also takes a tick from you (\"you said so\")");
    h += "</div></div></div>";
    return h;
  }

  // ---------- F4: the portrait pack ----------
  var PACK = {
    none: { s: "Not downloaded", n: "About 2,300 more giver faces from Garland Tools' NPC photos. <b>14.8 MB</b> from Tsukimichi's GitHub release, only when you click.", a: [["Download…"]] },
    dl: { s: "Downloading", n: "6.2 of 14.8 MB · about 20 s left", bar: 42, a: [["Cancel", "q"]], t: "Keeps going if Settings closes." },
    chk: { s: "Checking the file", n: "Comparing it with the fingerprint built into Tsukimichi 1.20.0", bar: 100, a: [] },
    ok: { s: "Installed · pack 1", n: "2,310 faces · 14.8 MB on this PC · downloaded 4 Dec", a: [["Remove pack…", "q"]] },
    upd: { s: "Pack 2 is ready to download", n: "140 more faces · 15.2 MB, replaces pack 1, which keeps working until then.", a: [["Update…"], ["Remove pack…", "q"]], t: "Named by this version of Tsukimichi; it never checks online." },
    net: { s: "Download failed", n: "Couldn't reach GitHub. Nothing was saved.", err: 1, a: [["Try again"]] },
    hash: { s: "Download failed", n: "The file didn't match this version's fingerprint, so it was deleted. Nothing was installed.", err: 1, a: [["Try again"], ["Copy report", "q"]] },
    disk: { s: "Download failed", n: "Not enough space: the pack needs 15 MB in Dalamud's pluginConfigs folder.", err: 1, a: [["Try again"]] },
    dmg: { s: "Pack damaged", n: "Some of the pack's files are missing. Faces use game art until you download it again.", err: 1, a: [["Download again…"]] },
    cxl: { s: "Cancelled", n: "Nothing was saved.", a: [["Download…"]] }
  };
  function packRow(lv, k, sub, nar) {
    var P = PACK[k];
    var acts = P.a.map(function (a) { return a[1] === "q" ? qa19(a[0]) : pill18(lv, a[0]).replace("p18 " + lv, "p18 " + lv + " sm"); }).join("");
    return '<div class="sr20 pk' + (sub ? " sub" : "") + (nar ? " nar" : "") + '">' + (sub ? "<i></i>" : "") + '<span class="sl">Portrait pack</span><div class="sm"><div class="ss">' + (P.err ? '<i class="hd20dot"></i>' : "") + nb(P.s) + "</div>" +
      '<div class="sn">' + nb(P.n) + "</div>" + '<div class="slot">' + (P.bar != null ? '<div class="pb20"><i style="width:' + P.bar + '%"></i></div>' : "") + (P.t ? '<div class="tt20">' + P.t + "</div>" : "") + "</div>" +
      '</div><div class="sa">' + acts + "</div></div>";
  }
  function lookSettings(lv, k, choice) {
    var seg = ["Off", "Game art", "Game art + pack"].map(function (c, i) { return '<span class="' + (i === choice ? "on" : "") + '">' + c + "</span>"; }).join("");
    return '<div class="set20"><div class="sh"><b>Look</b><span>Settings › General › Look</span></div>' +
      '<div class="sr20"><span class="sl">Giver portraits</span><div class="sm"><div class="ss"><span class="sg20">' + seg + '</span></div><div class="sn">The giver\'s face from the game\'s art' + (choice === 2 ? " and the pack" : "") + ", or a silhouette, emblem or initials. Read on this PC.</div></div><div class=\"sa\"></div></div>" +
      packRow(lv, k, true) + "</div>";
  }
  function packDialog(lv, upd, cls) {
    return '<div class="dlg20 ' + (cls || "") + '"><h3>' + (upd ? "Download pack&nbsp;2?" : "Download the portrait pack?") + "</h3>" +
      "<p>" + (upd ? "Adds 140 faces and replaces pack&nbsp;1 when it's checked." : "Adds about 2,300 giver faces, so roughly 4 in 5 quests show who gives them. The rest keep a silhouette, an emblem or initials.") + "</p>" +
      "<dl><dt>Size</dt><dd>" + (upd ? "15.2 MB" : "14.8 MB") + " <span>· saved once in Tsukimichi's folder on this PC</span></dd>" +
      "<dt>From</dt><dd>github.com/xenofei/Tsukimichi <span>· release " + (upd ? "portraits-2" : "portraits-1") + "</span></dd>" +
      "<dt>Photos</dt><dd>Garland Tools NPC photos by Celes <span>· cropped to the head</span></dd>" +
      "<dt>Checked</dt><dd>against the fingerprint built into Tsukimichi 1.20.0</dd></dl>" +
      '<div class="pr"><b>Only because you clicked.</b> <span>This is the one time Tsukimichi goes online. It sends nothing about you or your characters: GitHub sees an ordinary file download from this PC. Tsukimichi never checks for updates by itself.</span></div>' +
      '<div class="sp">The spoiler shield still hides faces from quests ahead of you.</div>' +
      '<div class="ac"><span class="lnk">What Tsukimichi sends</span>' + pill18(lv, upd ? "Download 15.2 MB" : "Download 14.8 MB", true) + pill18(lv, "Cancel").replace('class="p18', 'class="p18 fc') + "</div></div>";
  }
  // Chrome.HoldButton as shipped: the level's button; while held, a Moon arc closes clockwise from the top centre along
  // the outline over a line-tone track. Under Reduce motion a countdown replaces the label instead.
  function hold20(lv, label, p, reduce) {
    var w = 126, hgt = lv === "plain" ? 22 : 28, r = lv === "plain" ? 3 : hgt / 2, x = 0.75, y = 0.75, W = w - 1.5, H = hgt - 1.5, rr = r - 0.75;
    var path = "M" + (w / 2) + " " + y + "H" + (x + W - rr) + "A" + rr + " " + rr + " 0 0 1 " + (x + W) + " " + (y + rr) + "V" + (y + H - rr) + "A" + rr + " " + rr + " 0 0 1 " + (x + W - rr) + " " + (y + H) +
      "H" + (x + rr) + "A" + rr + " " + rr + " 0 0 1 " + x + " " + (y + H - rr) + "V" + (y + rr) + "A" + rr + " " + rr + " 0 0 1 " + (x + rr) + " " + y + "Z";
    var arc = reduce ? "" : '<path d="' + path + '" fill="none" stroke="' + (SNOW ? "#A07B25" : "var(--moon)") + '" stroke-width="1.6" pathLength="100" stroke-dasharray="' + p + ' 100" stroke-linecap="round"/>';
    return '<span class="hb20 ' + lv + '" style="width:' + w + "px;height:" + hgt + 'px"><svg width="' + w + '" height="' + hgt + '"><path d="' + path + '" fill="none" stroke="var(--line)" stroke-width="1.6"/>' + arc + "</svg><span>" + (reduce ? "Hold… (0.3 s)" : label) + "</span></span>";
  }
  function removePop(lv, p, reduce) {
    return '<div class="pop20"><b>Remove the portrait pack?</b><p>Deletes 14.8 MB from this PC. Faces go back to game art, silhouettes, emblems and initials. Getting it back means downloading it again.</p><div class="ac">' + hold20(lv, "Remove pack", p, reduce) + qa19("Keep it") + '</div><p class="hn">Hold, or Ctrl-click</p></div>';
  }
  function boardPack() {
    var h = '<div class="board b15 b16 b20"><h2>1.20 · The portrait pack<small>F4. Opt-in, from Tsukimichi\'s own GitHub release, behind a confirmation that says size, source and the local-only promise. One row in Settings › General › Look shows every state in place, at one fixed height.</small></h2><div class="row">';
    h += '<div class="col" style="width:760px">';
    h += fig(look18("full", "night", '<div class="scrim20"><div class="under">' + lookSettings("full", "none", 1) + '</div><div class="over">' + packDialog("full") + "</div></div>", 760), "<b>Download…</b>, or picking <b>Game art + pack</b> without the pack, opens the confirmation: a modal over Settings, its dim the only scrim. Focus starts on Cancel; Enter and Esc cancel");
    h += fig(look18("full", "night", '<div class="set20">' + packRow("full", "dl") + packRow("full", "chk") + packRow("full", "ok") + packRow("full", "upd") + "</div>", 760), "<b>Downloading → checking → installed</b>, and the <b>update offer</b> after Dalamud updates Tsukimichi: every state the same height, slots reserved");
    h += "</div>";
    h += '<div class="col" style="width:740px">';
    h += fig(look18("full", "night", '<div class="set20">' + packRow("full", "net") + packRow("full", "hash") + packRow("full", "disk") + packRow("full", "cxl") + "</div>", 740), "<b>Errors</b>: words in Text with the Settings hint dot; the reason, what was kept, one way forward. The same fixed height");
    h += '<div class="row">';
    h += fig(look18("full", "night", removePop("full", 58) + '<div style="height:8px"></div><div class="hbrow">' + hold20("full", "Remove pack", 0, true) + "<small>Reduce motion: the countdown</small></div>", 360, ";background:transparent"), "<b>Remove pack…</b>: the confirm with the shipped Hold button, mid-hold (a Moon arc from the top centre)");
    h += fig(look18("full", "night", '<div class="tipf20">' + pface(92, "full", "buscarron") + "<b>Buscarron</b><p class=\"src\">Portrait: Garland Tools photo · credit Celes</p><p>Buscarron's Druthers · South Shroud</p></div>", 220, ";background:transparent"), "<b>Hover</b>: a pack photo is never drawn above its own box (92 px here)");
    h += "</div>";
    h += fig(look18("full", "night", '<div class="ba20">' + plate(72, "full", "initials", { text: "B" }) + '<span class="ar">→</span>' + pface(72, "full", "buscarron") + '<span style="width:24px"></span>' + sil20(72, "full", "hyur-male") + '<span class="ar">→</span>' + pface(72, "full", "isembard") + '<span style="width:24px"></span>' + face(72, "full", "alphinaud") + "</div>", 740), "<b>With the pack</b>: Buscarron (initials → photo), Isembard (silhouette → photo), and Alphinaud's Duty Support portrait for comparison, all graded alike; faces fade in over 0.3 s");
    h += fig(look18("full", "night", '<span class="sbn20"><b>Portrait pack installed</b>· 2,310 faces</span><div style="height:8px"></div><div class="set20"><div class="sr20" style="grid-template-columns:150px 1fr"><span class="sl">Sends</span><div class="sm"><div class="sn">Nothing, except the portrait pack when you click Download: one file from Tsukimichi\'s GitHub release.</div></div></div></div>', 740), "<b>Status-bar note</b> when it lands, and the <b>Privacy &amp; trust</b> line that changes with F4");
    h += "</div></div></div>";
    return h;
  }

  // ---------- Every look ----------
  var PALS = ["night", "snow", "dawn", "kugane"];
  function lookHead(h) { return h + "<div></div>" + ["Night", "Ishgard Snow", "Dawn", "Kugane Lacquer"].map(function (n) { return '<div class="lh">' + n + "</div>"; }).join(""); }
  function boardLooks20() {
    var h = '<div class="board b15 b16 b20"><h2>1.20 · Every look (1 of 2)<small>The Before Evercold card (narrow form), a masked giver (N6) and the portrait pack row (F4) at Full, Quiet and Plain on Night, Ishgard Snow, Dawn and Kugane Lacquer. Only palette tokens move.</small></h2><div class="lg20">';
    h = lookHead(h);
    var frag = function (lv) {
      var be = beCard(lv, { nar: 1, body: beLine(BE.story) + beLine({ icon: BE.duties.icon, t: BE.duties.t, d: "2 duties not unlocked", a: [qa19("Duties board")] }, "you") });
      return '<div class="dp19">' + be + giver20(lv) + '<div class="set20">' + packRow(lv, "ok", false, true) + "</div></div>";
    };
    ["full", "quiet", "plain"].forEach(function (lv) {
      h += '<div class="lr">' + lv.charAt(0).toUpperCase() + lv.slice(1) + "</div>";
      PALS.forEach(function (p) { h += onPal(p, function () { return look18(lv, p, frag(lv), 340); }); });
    });
    return h + "</div></div>";
  }
  function boardLooks20b() {
    var h = '<div class="board b15 b16 b20"><h2>1.20 · Every look (2 of 2)<small>The parts that are new in 1.20, on every palette: pack photos beside game art at every size, the hidden-reward tile, a pack error with the hint dot, the Hold popover and the confirmation over its dim.</small></h2><div class="lg20">';
    h = lookHead(h);
    var rows = [
      ["Faces", "full", function (lv) { return '<div class="fs20">' + face(72, "full", "alphinaud") + pface(72, "full", "buscarron") + face(64, "quiet", "alphinaud") + pface(64, "quiet", "buscarron") + '<span class="sm20">' + face(24, "full", "alphinaud") + pface(24, "full", "buscarron") + face(18, "plain", "alphinaud") + pface(18, "plain", "buscarron") + "</span></div>"; }],
      ["Full", "full", function (lv) { return sect19(lv, "Rewards", "2 items", '<div class="rwl20" style="margin-top:0">' + hid20() + ph("An item") + '<span class="k">optional</span></div><div class="rwl20">' + ic15("000046", 18, "tile") + ph("Trial (Lv 99)") + "</div>"); }],
      ["Quiet", "quiet", function (lv) { return '<div class="set20">' + packRow(lv, "hash", false, true) + "</div>"; }],
      ["Plain", "plain", function (lv) { return removePop(lv, 58); }],
      ["Quiet", "quiet", function (lv) { return '<div class="dim20">' + packDialog(lv, false, "cmp") + "</div>"; }]
    ];
    rows.forEach(function (R) {
      h += '<div class="lr">' + R[0] + "</div>";
      PALS.forEach(function (p) { h += onPal(p, function () { return look18(R[1], p, R[2](R[1]), 340); }); });
    });
    return h + "</div></div>";
  }

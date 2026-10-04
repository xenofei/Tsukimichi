  // ======================================================================================================
  // 1.19 "Right answers": C1, C3–C10, K3, N3–N5 (spec-1.19.md). Built from the 1.15–1.18 parts: card(), the Section
  // heading, chips, pills (p18), game icons (ic15), look18 wrappers, the colour language of 1.18.
  // ======================================================================================================
  function sect19(lv, title, cap, body, capCls) {
    if (lv === "plain") return '<div class="ps">' + title + '<span class="' + (capCls || "") + '">' + (cap || "") + "</span></div>" + body;
    return card(lv, "<h4>" + title + (cap ? '<span class="r ' + (capCls || "") + '">' + cap + "</span>" : "") + "</h4>" + body);
  }
  // A requirement line: verdict mark (check, cross or the hollow "can't check" ring), label, detail, optional source line.
  function req19(kind, label, detail, sub, acts) {
    var mark = kind === "ok" ? ICON.check : kind === "no" ? ICON.xmark : '<i class="unk19"></i>';
    return '<div class="rq19 ' + kind + '"><span class="mk19">' + mark + '</span><div class="rqb"><div class="rqt"><span class="l">' + label + '</span><span class="d">' + detail + "</span></div>" +
      (sub ? '<div class="rqs">' + sub + "</div>" : "") + (acts ? '<div class="rqa">' + acts + "</div>" : "") + "</div></div>";
  }
  function badge19(icon, label, kind) { return '<span class="bg19 ' + (kind || "") + '">' + (icon ? ic15(icon, 14, "tile") : "") + label + "</span>"; }
  function line19(text, cls) { return '<div class="ln19 ' + (cls || "") + '">' + text + "</div>"; }
  function qa19(label, pri) { return '<span class="qa19' + (pri ? " pri" : "") + '">' + label + "</span>"; }

  // ---------- the detail pane, every 1.19 part in place (two quests) ----------
  function detail19a(lv) {
    // C1 (the game confirms it), C7 (badges and the item-level wall), C8 (EXP to the right job).
    var h = "";
    h += '<div class="hd19"><div class="n">Into the Aery</div><div class="m">Main Scenario › Heavensward · Ishgard · Lv 55</div>' +
      '<div class="st19"><span class="sw">Ready</span><span class="cf19">' + ic15("071201", 12, "marker") + "Offered by the game · 3 Oct</span></div></div>";
    h += sect19(lv, "Requirements", "all met",
      req19("ok", "Level", "55, DRG is 56") + req19("ok", "Previous quest", "Ready to Fly"));
    h += sect19(lv, "How you'll clear it", "1 duty",
      '<div class="dt19"><div class="dn">The Aery</div><div class="bgs">' + badge19("000089", "Solo with NPCs") + badge19("", "Story-required", "q") + "</div></div>" +
      line19("i110 needed · you're i108 (DRG) · WAR gearset i112", "wall") + '<div class="rqs19">Your SGE (i705) also qualifies. Duty Support checks item level too.</div>');
    h += sect19(lv, "Rewards", "",
      '<div class="rw19"><span class="rwn">50,700 EXP · 5,000 gil</span></div>' +
      line19(ic15("062122", 14, "tile") + "Hand in on <b>DRG Lv 56</b>: 50,700 EXP (5% of a level) · your SGE is capped, 0"));
    return h;
  }
  function detail19c(lv) {
    // C3: a gate the plugin can't check.
    var h = '<div class="hd19"><div class="n">Knocking on Heaven\'s Door</div><div class="m">Heaven-on-High · The Ruby Sea · Lv 61</div>' +
      '<div class="st19"><span class="sw dim">Can\'t check · 1 gate</span></div></div>';
    h += sect19(lv, "Requirements", "1 can't be checked",
      req19("ok", "Level", "61, DRG is 100") + req19("ok", "Previous quest", "Tide Goes in, Imperials Go Out") +
      req19("unk", "Palace of the Dead, floors 41–50", "can't check", "The game doesn't show plugins this. From the wiki, confirmed by 2 sources.", qa19("I've done this") + qa19("Where to start")), "unk");
    return h;
  }
  function detail19d(lv) {
    // C6: a reward you can buy back (a finished job quest).
    var h = '<div class="hd19"><div class="n">Two Nations, One Seed</div><div class="m">Botanist Quests · Ishgard · Lv 55</div>' +
      '<div class="st19"><span class="sv">Completed · 2024-11-08</span></div></div>';
    h += sect19(lv, "Rewards", "you picked 1 of 5",
      '<div class="rw19"><span class="rwn">836,550 EXP · 3,107 gil</span></div>' +
      '<div class="rw19 it"><span class="rwc uq">Dhalmelskin Vest</span><span class="rbb">You picked this · not on you · buy it back from a <b>Calamity Salvager</b></span><span class="rwa">' + qa19("Flag") + qa19("Teleport") + "</span></div>" +
      '<div class="rw19 it"><span class="rwc">Allagan Silver Piece</span><span class="rbb">Not offered by the Salvager</span></div>');
    return h;
  }
  function detail19b(lv) {
    // C8 Switch gearset (a job quest), N5 where to get hand-in items, C9 journal full.
    var h = '<div class="hd19"><div class="n">Looking for Some Hot Stuff</div><div class="m">Culinarian Quests · Limsa Lominsa · Lv 55</div>' +
      '<div class="st19"><span class="sw">Ready · journal full</span><span class="cf19 cu">Make room to accept it</span></div></div>';
    h += sect19(lv, "Requirements", "1 unmet",
      req19("no", "Job", "Culinarian · you're on Dragoon", "", '<span class="p18 ' + lv + ' sm">' + ic15("062115", 14, "tile") + "Switch gearset: Culinarian</span>") +
      req19("ok", "Level", "55, CUL is 57") + req19("ok", "Previous quest", "A Spoonful Less Sugar"));
    h += sect19(lv, "Hand in", "3 items",
      '<div class="hi19"><div class="hin"><b>Kaiser Roll</b><span>0 / 1</span></div><div class="hiw">' + ic15("000022", 14, "tile") + 'Crafted · Culinarian Lv 55</div><div class="hia">' + '<span class="p18 ' + lv + ' sm">' + ic15("000022", 14, "tile") + "Craft with Artisan</span>" + "</div></div>" +
      '<div class="hi19"><div class="hin"><b>Beet Soup</b><span>0 / 1 · 1 in your saddlebag</span></div><div class="hiw">The game counts your <b>inventory</b> only: take it out of the saddlebag.</div></div>' +
      '<div class="hi19"><div class="hin"><b>Grilled Sweetfish</b><span>0 / 1</span></div><div class="hiw">' + ic15("060412", 14, "marker") + 'Sold by <b>Kurogai</b> · 1,053 gil · or crafted, Culinarian Lv 54</div><div class="hia">' + qa19("Flag") + qa19("Teleport") + "</div></div>");
    return h;
  }
  function detail19e(lv) {
    // N5: a gathered item. Tsukimichi names the source and opens the game's own log; it doesn't guess a node.
    var h = '<div class="hd19"><div class="n">Sellspade</div><div class="m">Miner Quests · Ishgard · Lv 53</div>' +
      '<div class="st19"><span class="ij">In journal</span></div></div>';
    h += sect19(lv, "Hand in", "1 item",
      '<div class="hi19"><div class="hin"><b>Mythrite Ore</b><span>none in your inventory</span></div><div class="hiw">' + ic15("000023", 14, "tile") + 'Mined · the Gathering Log shows the nodes</div><div class="hia">' + qa19("Open Gathering Log") + "</div></div>");
    return h;
  }
  function pane19(lv, pal, fn, w) { return look18(lv, pal, '<div class="dp19">' + fn(lv) + "</div>", w || 420); }
  function boardDetail19() {
    var h = '<div class="board b15 b16"><h2>1.19 · Right answers in the detail pane<small>Full on Night. Everything new sits inside the existing cards, as a line under the row it explains. Copper only where the player must act (journal full). Buttons follow the automation level (1.18).</small></h2><div class="row" style="align-items:flex-start">';
    var col = function (a) { return '<div style="display:flex;flex-direction:column;gap:14px;width:450px">' + a.join("") + "</div>"; };
    h += col([fig(pane19("full", "night", detail19a, 440), "<b>Into the Aery</b>: C1 the game offered it · C7 badges and the item-level wall · C8 where the EXP goes"),
      fig(pane19("full", "night", detail19c, 440), "<b>Knocking on Heaven\'s Door</b>: C3 a gate Tsukimichi can\'t check")]);
    h += col([fig(pane19("full", "night", detail19b, 440), "<b>Looking for Some Hot Stuff</b>: C9 journal full · C8 Switch gearset (job quests only) · N5 where to get each item"),
      fig(pane19("full", "night", detail19d, 440), "<b>Two Nations, One Seed</b>: C6 a reward you can buy back")]);
    h += col([fig(look18("full", "night", disagree19("full"), 420), "<b>C1, when the game disagrees</b>: said plainly, with both sides; the player picks"),
      fig(look18("full", "night", '<div class="lvls19"><b>By automation level</b>' +
        '<div><span>Tracker only</span>' + qa19("Flag") + "</div><div><span>Travel</span>" + qa19("Flag") + qa19("Teleport") + "</div><div><span>Travel and walking</span>" + qa19("Flag") + qa19("Teleport") + "</div><div><span>Full hand-offs</span>" + qa19("Flag") + qa19("Teleport") + '<span class="p18 full sm">' + ic15("000022", 14, "tile") + "Craft with Artisan</span></div></div>", 420), "<b>N5 actions</b> appear only up to the chosen level; never greyed"),
      fig(pane19("full", "night", detail19e, 420), "<b>Sellspade</b>: N5 for a gathered item opens the game\'s Gathering Log")]);
    return h + "</div></div>";
  }
  function disagree19(lv) {
    return '<div class="sc18 tone-you">' + (lv === "plain" ? "" : "") + card(lv, '<div class="sch">' + ic15("071201", 18, "marker") + "<b>The game and Tsukimichi disagree</b></div>" +
      '<p class="why">The game offered <b>Sleepless in Ishgard</b> on 3 Oct, but Tsukimichi has it as Blocked: it expects <b>The Narwhal Beckons</b> first.</p><p class="ctx">One of us is wrong. The game sees your character; Tsukimichi\'s data may be out of date.</p>' +
      '<div class="a18">' + pill18(lv, "Go with the game", true) + '<span class="cr">' + ICON.copy + "Copy report</span></div>" + '<p class="hint">Shows it as Ready on this character until the data agrees. Undo any time from the quest\'s "…".</p>') + "</div>";
  }

  // ---------- rows, the status bar and the overlay: C1, C4, C9, C10, N3 ----------
  function row19(lv, glyphSt, name, status, extra, cls) {
    return '<div class="r19 ' + (cls || "") + '"><span class="g">' + rowGlyph(lv, glyphSt) + '</span><span class="nm">' + name + '</span><span class="sx">' + status + "</span>" + (extra ? '<span class="ex">' + extra + "</span>" : "") + "</div>";
  }
  function boardRows19() {
    var h = '<div class="board b15 b16"><h2>1.19 · Rows, chips and the status bar<small>Signals in the list stay one muted word or chip; the explanation is in the hover and the detail pane. New Game+ and the journal count live in the fixed status bar.</small></h2><div class="row" style="align-items:flex-start">';
    var rows = row19("full", "ready", "Into the Aery", '<b class="rd">Ready</b> · Lv 55', '<span class="cf">seen in game</span>') +
      row19("full", "ready", "Towards the Firmament", '<b class="rd">Ready</b> · Lv 60', "") +
      row19("full", "blocked", "Sleepless in Ishgard", "Blocked · after: The Narwhal Beckons", '<span class="dg">game disagrees</span>') +
      row19("full", "in-journal", "Grim and Grisly Games", "In journal · All Saints\' Wake", '<span class="ch19 ev">Ends in 2 days</span>') +
      row19("full", "not-checked", "Knocking on Heaven\'s Door", "Can\'t check · Palace of the Dead 41–50", "") +
      row19("full", "ready", "Looking for Some Hot Stuff", '<b class="rd">Ready</b> · <span class="jd">journal full</span>', "", "full19") +
      row19("full", "completed", "Shadowbringers", "Completed · 2025-02-21", '<span class="ch19 ng">Replaying</span>');
    h += fig(look18("full", "night", '<div class="rl19">' + rows + "</div>", 760), "<b>Table rows</b>, Full on Night: \"seen in game\" (C1), \"game disagrees\" (C1), an event ending (C10), \"Can't check\" (C3), \"journal full\" (C9), Replaying (C4)");
    h += '<div style="display:flex;flex-direction:column;gap:14px">';
    var sb = function (inner) { return look18("full", "night", '<div class="sb19">' + inner + "</div>", 900, ";background:transparent"); };
    h += fig(sb('<span>65%</span><span class="d">·</span><span class="msq">' + medalImg("in-journal", 16) + "MSQ · 742 of 1,038</span><span class=\"d\">·</span><span>Journal 22/30</span><span class=\"ver\">v1.19.0</span>"), "<b>Normal</b>: the journal count shows from 25; here it is quiet");
    h += fig(sb('<span>65%</span><span class="d">·</span><span class="msq">' + medalImg("in-journal", 16) + 'MSQ · 742 of 1,038</span><span class="d">·</span><span class="jt">Journal 28/30</span><span class="ver">v1.19.0</span>'), "<b>27–29</b>: the count in Text, \"2 slots left\" on hover");
    h += fig(sb('<span>65%</span><span class="d">·</span><span class="msq">' + medalImg("in-journal", 16) + 'MSQ · 742 of 1,038</span><span class="d">·</span><span class="jf"><i></i>Journal full · 30/30</span><span class="mr">Make room</span><span class="ver">v1.19.0</span>'), "<b>30</b>: a copper dot beside the words (it needs you): Ready quests can\'t be accepted; Make room opens the guide");
    var sbp = function (lv, pal) { return look18(lv, pal, '<div class="sb19">' + '<span>65%</span><span class="d">·</span><span class="msq">' + medalImg("in-journal", 16) + 'MSQ · 742 of 1,038</span><span class="d">·</span><span class="jf"><i></i>Journal full · 30/30</span><span class="mr">Make room</span><span class="ver">v1.19.0</span></div>', 900); };
    h += fig(sbp("quiet", "snow") + sbp("plain", "dawn") + sbp("full", "kugane"), "<b>30</b> on Quiet Ishgard Snow, Plain Dawn and Full Kugane Lacquer: the same copper dot, the palette\'s own --attn");
    h += fig(sb('<span class="ng19">' + ic15("000084", 16, "tile") + 'New Game+ · Shadowbringers – Part 2 · quest 87 of 112</span><span class="d">·</span><span>Your saved progress is kept</span><span class="mr">End session</span><span class="ver">v1.19.0</span>'), "<b>New Game+</b> (C4): the bar says so while a replay is running; End session asks first");
    h += fig(look18("full", "night", '<div class="tt19"><b>Main scenario · 742 of 1,038 (71%)</b><p>Counts the <b>side quests the story requires</b> too: the Crystal Tower series and the Hard primal trials.</p><p>Next milestone in 6 quests.</p><p class="hd">Still to do from earlier: the Crystal Tower series, 3 alliance raids at Lv 50. The story needs it later.</p></div>', 420), "<b>N3, the story meter</b>: the MSQ pill's hover; spoiler-safe (milestones, not names, past your story point)");
    return h + "</div></div></div>";
  }

  // ---------- C9 Make room ----------
  function boardMakeRoom() {
    var grp = function (t, sub, items) { return '<div class="mg19"><div class="mgh"><b>' + t + "</b><span>" + sub + "</span></div>" + items.map(function (it) { return '<div class="mi19' + (it[3] ? " hov" : "") + '"><span class="g">' + rowGlyph("full", "in-journal") + '</span><span class="nm">' + it[0] + '</span><span class="sx">' + it[1] + "</span>" + (it[2] ? '<span class="sf">safe to drop</span>' : "") + (it[3] ? qa19("Open in journal", true) : "") + "</div>"; }).join("") + "</div>"; };
    var body = '<div class="mr19"><div class="mrh"><b>Make room</b><span>30 of 30 slots. Finish one, or drop one in the game\'s journal.</span></div>' +
      grp("Finish now", "one talk or delivery left", [["Small Business, Big Dreams", "a talk · Old Sharlayan"], ["Grim and Grisly Games", "a talk · Gridania"]]) +
      grp("Needs a duty", "solo or with NPCs", [["Sworn Upon a Lance", "a solo duty · Coerthas"]]) +
      grp("Needs an item", "to hand in", [["Sellspade", "Mythrite Ore · 0 in your inventory"]]) +
      grp("Needs a group", "other players", [["An Otherworldly Encounter", "Jeuno: The First Walk · 24 players"]]) +
      grp("Safe to drop", "still on step 1: nothing to redo if you take it again", [["Bird in Hand", "step 1 · Central Shroud", 1, 1], ["A Faerie Tale Come True", "step 1 · Limsa Lominsa", 1]]) +
      '<div class="mrf"><span class="nt">Tsukimichi never abandons a quest. <b>Open in journal</b> shows it in the game, where you drop it and the game asks first. Tsukimichi notes the step you had reached.</span></div></div>';
    return '<div class="board b15 b16"><h2>1.19 · Make room<small>A popover from "Journal full" and from a Ready quest that can\'t be accepted. It groups what is in the journal by what finishing takes; safe-to-drop quests are marked. Dropping a quest is done in the game, by the player.</small></h2><div class="row" style="align-items:flex-start">' +
      fig(look18("full", "night", body, 560), "<b>Full, Night</b>") + fig(look18("quiet", "snow", body, 560), "<b>Quiet, Ishgard Snow</b>") + fig(look18("plain", "kugane", body, 560), "<b>Plain, Kugane Lacquer</b>") + fig(look18("quiet", "dawn", body, 560), "<b>Quiet, Dawn</b>") + "</div></div>";
  }

  // ---------- N4 Duties board, C5 allied, C10 events, K3 unlocks ----------
  function boardBoards19() {
    var h = '<div class="board b15 b16"><h2>1.19 · Duties, allied societies, events and unlocks<small>Each lives inside an existing card or pane: the Characters dashboard (Duties, Allied societies), Tonight (events), the toolbar search and the filter drawer (unlocks). Lists show what is left, not tallies.</small></h2><div class="row" style="align-items:flex-start">';
    var duties = sect19("full", "Duties", "1 roulette locked",
      '<div class="du19"><div class="duh"><b>Level Cap roulette</b><span class="lk">locked · needs a Lv 100 job · best is BLM 98</span></div>' +
      '<div class="dur"><span class="dn">Mistwake</span>' + badge19("000046", "Group of 4") + '<span class="dq">not unlocked · with <b>Beyond the Mountains</b></span></div>' +
      '<div class="dur"><span class="dn">The Clyteum</span>' + badge19("000046", "Group of 4") + '<span class="dq">not unlocked · with <b>Two Worlds Entwined</b></span></div>' +
      '<div class="dua">' + qa19("Route") + qa19("Pin both") + "</div></div>" +
      '<div class="du19"><div class="duh"><b>Alliance Raids roulette</b><span class="lk">open · 1 raid not unlocked</span></div><div class="dur"><span class="dn">Jeuno: The First Walk</span>' + badge19("000017", "Group of 24") + '<span class="dq">with <b>An Otherworldly Encounter</b></span></div></div>' +
      '<div class="du19"><div class="duh"><b>Unlocked, never cleared</b><span>14</span></div><div class="dur"><span class="dn">Castrum Abania</span>' + badge19("000046", "Group of 4") + '</div><div class="dur"><span class="dn">The Burn</span>' + badge19("000046", "Group of 4") + '</div><div class="more">12 more ›</div></div>');
    h += fig(look18("full", "night", duties, 480), "<b>N4, the Duties board</b> (Characters dashboard): why a roulette is locked; duties unlocked but never cleared");
    var allied = sect19("full", "Allied societies", "",
      '<div class="al19"><span class="em">' + ic15("065039", 22, "tile") + '</span><div><b>Moogles · Rank 6</b><div class="cu19"><i></i>0 allowances until you turn in <b>Moogle on the Wall</b> (accepted before the reset)</div></div><span class="a">' + qa19("Flag") + qa19("Teleport") + "</span></div>" +
      '<div class="al19"><span class="em">' + ic15("065020", 22, "tile") + '</span><div><b>Sahagin · Rank 3</b><div class="ln19">Rank-up ready: keep 3 allowances for the bonus dailies</div></div></div>' +
      '<div class="al19 alt"><span class="em">' + ic15("065016", 22, "tile") + '</span><div><b>Amalj\'aa · Rank 4</b> <span class="altn">· stored alt Kiri Tsukikage</span><div class="ln19">0 allowances today: holds a daily from before the reset (last login 2 days ago)</div></div></div>');
    h += fig(look18("full", "night", allied, 480), "<b>C5, allied societies</b>: a carried-over daily (copper: it blocks today), a rank-up hint, a stored alt projected honestly");
    var ev = '<div class="sc18 tone-attn">' + card("full", '<div class="sch">' + ic15("071341", 18, "marker") + "<b>All Saints\' Wake ends in 2 days</b></div>" +
      '<p class="why">1 quest in your journal, 4 rewards you don\'t have.</p><p class="ctx">Ends 3 Nov, 07:59 local · runs every October</p><div class="a18">' + pill18("full", "Show the event", true) + "</div>") + "</div>";
    var ev2 = sect19("full", "Seasonal events", "", '<div class="ev19"><b>Make It Rain Campaign</b><span>running · end not announced</span><span class="qa19">Set end date…</span></div><div class="ev19"><b>Moonfire Faire</b><span>ended · usually August · last ran 2026</span></div><div class="ev19"><b>Little Ladies\' Day</b><span>usually March · last ran 2026</span></div>');
    h += fig(look18("full", "night", ev + ev2, 480), "<b>C10, events</b>: ending-soon only when the end is known (copper only with an event quest in your journal); dated reruns; an end date you entered says so");
    h += "</div><div class=\"row\" style=\"align-items:flex-start\">";
    var srch = '<div class="se19"><div class="sin">' + ICON.search + '<span>flying thav</span></div><div class="sgh">Unlocks</div>' +
      '<div class="sres"><span class="k">Flying</span><b>Flying in Thavnair</b><span class="via">10 aether currents, 4 from quests</span><span class="rt">' + qa19("Route to unlock") + "</span></div>" +
      '<div class="sres"><span class="k">Area</span><b>Thavnair</b><span class="via">via <b>For Thavnair Bound</b></span><span class="rt">' + qa19("Route to unlock") + "</span></div>" +
      '<div class="sgh">Quests</div><div class="sres q"><b>The Jewel of Thavnair</b><span class="via">Ready · Lv 81</span></div></div>';
    h += fig(look18("full", "night", srch, 560), "<b>K3, find by unlock</b>: the toolbar search gets an Unlocks group; each result has Route to unlock");
    var flt = '<div class="fu19"><div class="gh">Unlocks<span class="set">2 kinds</span></div><div class="sch">' + ["Mount", "Flying", "Duty", "Feature", "Job", "Area", "Emote", "Orchestrion"].map(function (k, i) { return '<span class="sc ' + (i < 2 ? "on" : "off") + '">' + k + "</span>"; }).join("") + '</div><div class="ln19">Keeps quests that unlock one of these. Off: every quest.</div></div>';
    h += fig(look18("full", "night", '<div class="drw19">' + flt + "</div>", 340), "<b>K3, the Unlocks filter</b>: a group in the filter drawer's Advanced (1.14 chips)");
    h += fig(look18("full", "night", '<div class="ro19"><div class="rh">' + ic15("060959", 22, "marker") + '<b>Route to Flying in Thavnair</b><span>10 stops</span></div><div class="rr"><span class="n">1</span>The Jewel of Thavnair<span class="m">MSQ · Ready</span></div><div class="rr"><span class="n">2</span>Aether current · Yedlihmad<span class="m">field</span></div><div class="rr"><span class="n">…</span>8 more: 3 quests, 5 in the field<span class="m"></span></div></div>', 420), "<b>K3, Route to unlock</b>: the Route window with the unlock as its header");
    return h + "</div></div>";
  }

  // ---------- every look ----------
  function boardLooks19() {
    var h = '<div class="board b15 b16"><h2>1.19 · Every look<small>Requirements (Knocking on Heaven\'s Door: met, unmet, can\'t check) and How you\'ll clear it (Into the Aery: badges, the item-level wall), at each Decoration level on Night, Ishgard Snow, Dawn and Kugane Lacquer.</small></h2><div class="lg18">';
    h += "<div></div>" + ["Night", "Ishgard Snow", "Dawn", "Kugane Lacquer"].map(function (n) { return '<div class="lh">' + n + "</div>"; }).join("");
    var frag = function (lv) {
      return '<div class="dp19">' + sect19(lv, "Requirements", "1 can't be checked",
        req19("ok", "Level", "61, DRG is 100") + req19("no", "Previous quest", "Tide Goes in, Imperials Go Out") +
        req19("unk", "Palace of the Dead, floors 41–50", "can\'t check", "The game doesn\'t show plugins this. From the wiki, 2 sources.", qa19("I\'ve done this")), "unk") + sect19(lv, "How you\'ll clear it", "", '<div class="dt19"><div class="dn">The Aery</div><div class="bgs">' + badge19("000089", "Solo with NPCs") + badge19("", "Story-required", "q") + "</div></div>" + line19("i110 needed · you're i108 (DRG) · WAR gearset i112", "wall")) + "</div>";
    };
    ["full", "quiet", "plain"].forEach(function (lv) {
      h += '<div class="lr">' + lv.charAt(0).toUpperCase() + lv.slice(1) + "</div>";
      ["night", "snow", "dawn", "kugane"].forEach(function (p) { h += look18(lv, p, frag(lv), 400); });
    });
    return h + "</div></div>";
  }

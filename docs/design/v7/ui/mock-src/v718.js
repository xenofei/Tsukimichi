  // ======================================================================================================
  // 1.18 "Runs you can trust": Why it stopped (A2), Needs you (A5), travel preflight (A9), automation level (A10),
  // the run receipt and stop controls (A4), and the "…" menu items (A6). spec-1.18.md.
  // ======================================================================================================
  var SRC18 = {
    questionable: { icon: "071221", cls: "marker", name: "Questionable" },
    autoduty: { icon: "000046", cls: "tile", name: "AutoDuty" },
    travel: { icon: "000104", cls: "tile", name: "Travel" },
    artisan: { icon: "000022", cls: "tile", name: "Artisan" }
  };
  // Every stop reason: tone (done | you | attn), the hand-off, title, the reason in plain words, context, fixes.
  var STOPS = [
    { k: "finished", tone: "done", src: "questionable", title: "Questionable finished", why: "It finished Close to Home and stopped, as asked.",
      ctx: "1 quest · 6 min · Ul'dah - Steps of Nald", fixes: [["Start the next one", "Questionable does Way of the Archer, then stops"]], receipt: true },
    { k: "you", tone: "you", src: "questionable", title: "You stopped Questionable", why: "Stopped from Tsukimichi's Stop, partway through Into the Aery.",
      ctx: "Step 3 of 6 · Coerthas Central Highlands", fixes: [["Start again", "Picks up Into the Aery at step 3"]] },
    { k: "guard", tone: "attn", src: "questionable", title: "Stopped before a duty with other players", why: "The next step is Castrum Meridianum, which has no Duty Support or Trust. It would queue you with real players.",
      ctx: "The Ultimate Weapon · step 2 of 5 · your setting: stop before such duties", fixes: [["Show the duty", "Opens Castrum Meridianum in the detail pane"], ["Keep going after it", "When you clear it yourself, Questionable starts again"]] },
    { k: "death", tone: "attn", src: "questionable", title: "Stopped: you were knocked out", why: "You were knocked out during Into the Aery. Tsukimichi stopped its own travel; Questionable is stopped.",
      ctx: "Step 3 of 6 · Coerthas Central Highlands · 21:42", fixes: [["Try again", "Ready when you're back on your feet", true]] },
    { k: "stuck", tone: "attn", src: "travel", title: "Travel got stuck", why: "No progress for 20 seconds near Camp Dragonhead. This is usually a gap in the navmesh.",
      ctx: "Walking to Ser Aymeric · 46 yalms left", fixes: [["Reload navmesh and retry", "vnavmesh rebuilds the mesh, then the same walk starts again"], ["Flag the spot", "Puts a flag on the map where it stuck"]] },
    { k: "path", tone: "attn", src: "travel", title: "No path to the giver", why: "vnavmesh found no path to Hihibaru: the giver stands where the mesh doesn't reach.",
      ctx: "Ul'dah - Steps of Thal · Hihibaru", fixes: [["Reload navmesh and retry", "Rebuilds the mesh, then tries once more"], ["Teleport closer", "Lifestream to the Adventurers' Guild aethernet"]] },
    { k: "missing", tone: "attn", src: "autoduty", title: "AutoDuty isn't loaded", why: "The next step is a Duty Support run, and AutoDuty is installed but turned off.",
      ctx: "The Stone Vigil · Duty Support", fixes: [["Open Setup", "Settings › Companions shows what to turn on"]] },
    { k: "error", tone: "attn", src: "questionable", title: "Questionable stopped with an error", why: "It has no path for step 3 of Into the Aery (missing sequence). That's in Questionable's quest data, not your game.",
      ctx: "Questionable 7.4.12 · Into the Aery (#67187) · step 3", fixes: [["Flag the objective", "So you can do this step yourself"]], report: "primary" }
  ];
  function pill18(lv, label, pri, dis) { return '<span class="p18 ' + lv + (pri ? " pri" : "") + (dis ? " dis" : "") + '">' + label + "</span>"; }
  function stopCard(lv, S, o) {
    o = o || {};
    var src = SRC18[S.src];
    var head = '<div class="sch">' + ic15(src.icon, lv === "plain" ? 14 : 18, src.cls) + "<b>" + S.title + '</b><span class="when">2 min ago</span><span class="x" title="Dismiss">' + ICON.x + "</span></div>";
    var body = '<p class="why">' + S.why + '</p><p class="ctx">' + S.ctx + "</p>";
    if (S.receipt) body += '<div class="rcp">Ran 6 min · 1 quest · stopped as asked<span class="rq">Close to Home</span></div>';
    var acts = '<div class="a18">' + S.fixes.map(function (f, i) { return pill18(lv, f[0], i === 0 && S.report !== "primary", f[2]); }).join("") +
      '<span class="cr' + (S.report === "primary" ? " pri" : "") + '">' + (lv === "plain" ? "" : ICON.copy) + "Copy report</span></div>";
    var hint = S.fixes[0] && S.fixes[0][2] ? '<p class="hint">' + S.fixes[0][1] + "</p>" : "";
    var inner = head + body + acts + hint;
    if (lv === "plain") return '<div class="sc18 plain tone-' + S.tone + '"><div class="ps">' + ic15(src.icon, 14, src.cls) + S.title + '<span>2 min ago</span></div>' + body + acts + hint + "</div>";
    return '<div class="sc18 tone-' + S.tone + '">' + card(lv, inner) + "</div>";
  }
  function look18(lv, pal, inner, w, extra) {
    return '<div class="mk ' + lv + " v7" + (pal && pal !== "night" ? " " + pal : "") + ' look18" style="width:' + w + 'px' + (extra || "") + '"><div class="lk">' + inner + "</div></div>";
  }
  function boardStop() {
    var h = '<div class="board b15 b16"><h2>1.18 · Why it stopped<small>A card in the notice dock above the detail pane\'s action bar (and in the Todo overlay), when a hand-off Tsukimichi started stops. Plain words, the safe fixes, Copy report. Full on Night; the bar on the left is the only colour cue: gold finished, silver you stopped it, amber it needs you.</small></h2><div class="sg18">';
    STOPS.forEach(function (S) { h += fig(look18("full", "night", stopCard("full", S), 400), "<b>" + { finished: "Finished", you: "You stopped it", guard: "Duty guard (A3)", death: "Knocked out", stuck: "Stuck", path: "No path", missing: "Missing plugin", error: "Error" }[S.k] + "</b>"); });
    h += "</div>";
    h += '<div class="row"><div class="spec" style="width:860px"><b>Copy report</b> puts plain text on the clipboard, ready for a GitHub issue. It never includes the character\'s name, world or chat.<pre class="pre18">Tsukimichi 1.18.0 · Dalamud API 15 · game 2026.09.15\nHand-off: Questionable 7.4.12 (started by Tsukimichi, "this quest only")\nStopped: error · missing sequence\nQuest: Into the Aery (#67187) · step 3 of 6 · sequence 3\nJob: PLD 57 · Zone: Coerthas Central Highlands (155) · at 18.2, 24.7\nTravel: vnavmesh 0.4.3 · Lifestream 2.5.1 · movement Standard\nLast Questionable lines:\n  [QST v7.4.12] Step 3: no path data for sequence 3\nStops at this step on this computer: 2</pre></div></div>';
    return h + "</div>";
  }
  function boardStopLooks() {
    var S = STOPS[4], h = '<div class="board b15 b16"><h2>1.18 · Why it stopped, every look<small>The Stuck card at each Decoration level on Night, Ishgard Snow, Dawn and Kugane Lacquer. Only the palette moves; layout, words and buttons are the same everywhere.</small></h2><div class="lg18">';
    h += '<div></div>' + ["Night", "Ishgard Snow", "Dawn", "Kugane Lacquer"].map(function (n) { return '<div class="lh">' + n + "</div>"; }).join("");
    ["full", "quiet", "plain"].forEach(function (lv) {
      h += '<div class="lr">' + lv.charAt(0).toUpperCase() + lv.slice(1) + "</div>";
      ["night", "snow", "dawn", "kugane"].forEach(function (p) { h += look18(lv, p, stopCard(lv, S), 400); });
    });
    return h + "</div></div>";
  }

  // ---------- A5: Needs you ----------
  var NEEDS = [
    { k: "death", title: "You were knocked out", line: "Questionable is stopped. Tsukimichi stopped its own travel.", acts: [["Stop all", 1]] },
    { k: "stuck", title: "Travel is stuck", line: "No progress for 20 s near Camp Dragonhead.", acts: [["Reload navmesh and retry", 0], ["Stop all", 1]] },
    { k: "pop", title: "Your duty is ready", line: "The Stone Vigil (Duty Support). Tsukimichi never commences for you.", acts: [["Stop all", 1]] },
    { k: "tell", title: "A tell arrived", line: "Someone is talking to you. Tsukimichi never answers.", acts: [["Stop all", 1]] }
  ];
  function needsToast(N, pal, more, lv) {
    lv = lv || "full";
    return '<div class="mk ' + lv + " v7 " + (pal || "") + '" style="background:transparent"><div class="nt18"><i class="bar"></i><div class="ntb"><div class="ntt"><b>Needs you</b><span>' + N.title + '</span></div><p>' + N.line + '</p><div class="nta">' +
      N.acts.map(function (a) { return '<span class="p18 ' + lv + (a[1] ? " stop" : "") + '">' + a[0] + "</span>"; }).join("") + '<span class="nd">Dismiss</span>' + (more ? '<span class="nm">+1 more</span>' : "") + "</div></div></div></div>";
  }
  function boardNeeds() {
    var h = '<div class="board b15 b16"><h2>1.18 · Needs you<small>Only while a hand-off Tsukimichi started is running. One panel over the game, top centre, at most one at a time (the rest wait behind "+1 more"); a chat line; an optional sound; the taskbar flashes when the game isn\'t focused. It rises 4 px and fades in over 0.16 s and stays until dismissed or the cause clears.</small></h2>';
    h += '<div class="gm18" style="background-image:url(\'../../../plan-site/mock/scene-night.jpg\')"><div class="gmc">' + needsToast(NEEDS[0], "", true, "full") + "</div>" +
      '<div class="chat18"><div><span class="ts">[21:42]</span> <span class="tag">[Tsukimichi]</span> <b>Needs you:</b> you were knocked out during Into the Aery (step 3). Questionable is stopped.</div>' +
      '<div><span class="ts">[21:43]</span> <span class="tag">[Tsukimichi]</span> <b>Needs you:</b> your duty is ready: The Stone Vigil (Duty Support).</div>' +
      '<div><span class="ts">[22:05]</span> <span class="tag">[Tsukimichi]</span> Questionable ran 1 h 12 m: 14 quests, then stopped after 14 as you asked.</div></div></div>';
    h += '<div class="row" style="align-items:flex-start">';
    [[1, "night", "full", "Full, Night"], [2, "dawn", "quiet", "Quiet, Dawn"], [3, "snow", "full", "Full, Ishgard Snow, over a daylight scene"], [1, "kugane", "plain", "Plain, Kugane Lacquer"]].forEach(function (x) {
      var N = NEEDS[x[0]];
      h += fig('<div class="gms" style="background-image:url(\'../../../plan-site/mock/scene-' + (x[1] === "snow" ? "day" : "night") + '.jpg\')">' + needsToast(N, x[1], false, x[2]) + "</div>", "<b>" + N.title + "</b> · " + x[3]);
    });
    h += '<div class="spec" style="width:420px"><b>Sound</b>: one of the game\'s own chat sound effects (&lt;se.1&gt; to &lt;se.16&gt;), played once per alert at the game\'s system-sound volume; never a loop. Default: on, &lt;se.7&gt;, the same for all four; each alert can pick its own or none, with a Test button.<br><br><b>Settings › Alerts › While automation runs</b>: Knocked out · Stuck · Duty ready · Tells and party invites (each on/off) · Sound · Flash the taskbar · Stop my hand-offs on knock-out or stuck (on) · Also stop Questionable (off).</div></div>';
    return h + "</div>";
  }

  // ---------- A9: travel preflight ----------
  var PRE = [
    { k: "ok", label: "Movement", status: "Standard", line: "vnavmesh steers with Standard movement.", fix: null },
    { k: "warn", label: "Movement", status: "Legacy", line: "With Legacy movement the character can run the wrong way while vnavmesh walks.", fix: "Switch to Standard", after: "Undo for 8 s; Restore Legacy stays here" },
    { k: "info", label: "Camera", status: "First person", line: "Walking turns the camera as it goes. Tsukimichi reminds you when a walk starts.", fix: null },
    { k: "warn", label: "vnavmesh", status: "Movement paused by another plugin", line: "Another plugin turned off vnavmesh movement, so walks start and then stand still.", fix: "Allow movement", after: "Undo for 8 s" },
    { k: "warn", label: "Conflicts", status: "WrongWarpFinder is loaded", line: "It can send Lifestream to the wrong aetheryte. Tsukimichi can't turn other plugins off.", fix: "Open the plugin installer", after: "Opens Dalamud's installer at the plugin" },
    { k: "ok", label: "Conflicts", status: "None found", line: "No plugin known to clash with travel is loaded.", fix: null }
  ];
  function preRow(lv, R) {
    return '<div class="pr18 ' + R.k + '"><span class="pl">' + R.label + '</span><div class="pm"><span class="pst"><i></i>' + R.status + '</span><span class="pli">' + R.line + "</span></div>" +
      (R.fix ? '<div class="pfx">' + pill18(lv, R.fix) + '<span class="pa">' + R.after + "</span></div>" : '<div class="pfx"></div>') + "</div>";
  }
  function preflight(lv, pal, rows, w) {
    return look18(lv, pal, '<div class="pf18' + (w < 700 ? " nar" : "") + '"><div class="pfh"><b>Travel preflight</b><span>Checked when you open Setup and before a walk starts</span></div>' + rows.map(function (R) { return preRow(lv, R); }).join("") + "</div>", w);
  }
  function boardPreflight() {
    var h = '<div class="board b15 b16"><h2>1.18 · Travel preflight in Setup<small>Three checks in Settings › Companions › Setup, with the plugin rows. Each has a status in words and one safe fix with Undo. Nothing changes until you press the fix.</small></h2>';
    h += '<div class="row">' + fig(preflight("full", "night", [PRE[1], PRE[2], PRE[3], PRE[4]], 860), "<b>Full, Night</b>: three things need a change") + fig(preflight("full", "night", [PRE[0], PRE[2], PRE[5]], 860), "<b>All clear</b>: the same rows; the fix column stays reserved, empty") + "</div>";
    h += '<div class="row">' + fig(preflight("quiet", "snow", [PRE[1], PRE[3]], 560), "<b>Quiet, Ishgard Snow</b>") + fig(preflight("plain", "dawn", [PRE[1], PRE[3]], 560), "<b>Plain, Dawn</b>") + fig(preflight("full", "kugane", [PRE[1], PRE[3]], 560), "<b>Full, Kugane Lacquer</b>") + "</div>";
    h += '<div class="row">' + fig(look18("full", "night", '<div class="ut18">Movement: Standard<span class="u">Undo</span></div>', 300, ";background:transparent"), "<b>After a fix</b>: the Undo toast") + "</div>";
    return h + "</div>";
  }

  // ---------- A10: automation level ----------
  var LEVELS = [
    { k: "Tracker only", sub: "Tsukimichi tracks; it never presses anything for you.", pills: [["060561", "Flag"], ["000007", "Map"]] },
    { k: "Travel", sub: "Adds Teleport and the aethernet, through Lifestream.", pills: [["060453", "Teleport"], ["060430", "Aethernet"]] },
    { k: "Travel and walking", sub: "Adds Walk to giver, through vnavmesh.", pills: [["060453", "Teleport"], ["000104", "Walk to giver"]] },
    { k: "Full hand-offs", sub: "Adds Questionable, AutoDuty and Artisan: they play the quest, the duty or the craft for you.", pills: [["071221", "Start (this quest)"], ["000046", "Run with AutoDuty"], ["000022", "Craft"]] }
  ];
  function levelSwitch(on) {
    return '<div class="lv18">' + LEVELS.map(function (L, i) {
      return '<div class="lc' + (i === on ? " on" : "") + '"><div class="lt"><i class="rd"></i><b>' + L.k + '</b></div><p>' + L.sub + '</p><div class="lp">' + L.pills.map(function (p) { return '<span class="p18 full sm">' + ic15(p[0], 14, "tile") + p[1] + "</span>"; }).join("") + "</div></div>";
    }).join("") + "</div>";
  }
  var ABOUT = [
    ["What Tsukimichi does itself", "It reads your game to track quests. It never moves, fights or talks for you. Every button that does sends the job to another plugin you installed, and only when you press it."],
    ["What the other plugins do", "Lifestream teleports, vnavmesh walks, Questionable plays quests, AutoDuty runs duties with Duty Support or Trust, Artisan crafts."],
    ["What the rules say", "Third-party tools are against the FINAL FANTASY XIV User Agreement. Square Enix has said it acts on what affects other players, and it can act on any account. No setting here makes automation safe."],
    ["Where players draw the line", "Most players accept automation in solo content and with NPCs (Duty Support, Trust). They object when it runs in content with other players, or unattended for hours. Tsukimichi stops before a duty with other players unless you change that."],
    ["Good manners", "Stay at the keyboard. Answer tells yourself. Keep runs short: Start does one quest unless you choose otherwise."],
    ["Local only", "Tsukimichi has no network code. Nothing you do here leaves your computer."]
  ];
  function boardAuto() {
    var h = '<div class="board b15 b16"><h2>1.18 · Automation level and About automation<small>Settings › Automation. Four levels, one click each; Full hand-offs is a first-class choice, shown and described like the others, with no warning icon. Buttons above your level are hidden, not greyed. The About card is linked from every first-start confirmation.</small></h2><div class="row" style="align-items:flex-start">';
    h += fig(look18("full", "night", '<div class="ah18"><h4>Automation buttons</h4><p>Which buttons Tsukimichi shows. Higher levels add buttons that hand the game to another plugin.</p>' + levelSwitch(3) + '<div class="ov18"><span>Fine-tune each button</span><i>›</i></div></div>', 880), "<b>Full, Night</b>: Full hand-offs chosen");
    h += fig(look18("full", "night", '<div class="ab18"><h4>About automation</h4>' + ABOUT.map(function (a) { return "<h5>" + a[0] + "</h5><p>" + a[1] + "</p>"; }).join("") + '<div class="abf"><span class="qa">Read the User Agreement</span><span class="qa">Close</span></div></div>', 560), "<b>About automation</b>: factual, short, no promise of safety");
    h += "</div><div class=\"row\" style=\"align-items:flex-start\">";
    h += fig(look18("quiet", "snow", '<div class="ah18"><h4>Automation buttons</h4>' + levelSwitch(1) + "</div>", 880), "<b>Quiet, Ishgard Snow</b>: Travel");
    h += fig(look18("plain", "kugane", '<div class="ah18"><h4>Automation buttons</h4>' + levelSwitch(0) + "</div>", 880), "<b>Plain, Kugane Lacquer</b>: Tracker only");
    return h + "</div></div>";
  }

  // ---------- A4 receipt and stop controls; A6 menu ----------
  function boardRun() {
    var h = '<div class="board b15 b16"><h2>1.18 · Stop when, the receipt, and the "…" menu<small>Stop conditions live in the Stop pill\'s own "…" menu while a run is going; the status bar says the condition. When the run ends, the Finished card carries the receipt and chat gets one line.</small></h2><div class="row" style="align-items:flex-start">';
    var menu = function (items) { return '<div class="mn18">' + items.map(function (it) { return it === "-" ? "<hr>" : '<div class="mi' + (it[2] ? " dis" : "") + (it[3] ? " on" : "") + '"><span>' + it[0] + "</span><em>" + (it[1] || "") + "</em></div>"; }).join("") + "</div>"; };
    h += fig(look18("full", "night", '<div class="bar18"><span class="p18 full stop">Stop</span><span class="p18 full sm dots">' + ICON.more + "</span></div>" + menu([["Stop now", "", 0], ["Stop after this quest", "Close to Home", 0, 1], ["Stop after…", "3 quests", 0], ["Stop at…", "22:30", 0], "-", ["Remind me after 2 hours", "a reminder, not a stop", 0]]), 360), "<b>While running</b>: the Stop pill\'s menu");
    h += fig(look18("full", "night", '<div class="pop18"><b>Stop after</b><div class="stp18"><i>−</i><span>3 quests</span><i>+</i></div><b>or at</b><div class="stp18 t"><span>22:30</span></div><div class="abf"><span class="qa">Cancel</span><span class="qa pri7">Set</span></div></div>', 300), "<b>Stop after… / Stop at…</b>: a small popover; the condition can be changed or cleared any time");
    h += fig(look18("full", "night", '<div class="stb18"><span class="g">Questionable: Close to Home</span><span class="d">·</span><span>stops after 3 more</span><span class="p18 full sm stop">Stop</span></div>', 440, ";background:transparent") +
      look18("full", "night", stopCard("full", { tone: "done", src: "questionable", title: "Questionable finished", why: "It stopped after 14 quests, as you asked.", ctx: "1 h 12 m · from The Rising Chorus to Close to Home", fixes: [["Show the 14 quests", ""]], receipt: false }) + '<div class="rc18"><div><b>14</b><span>quests</span></div><div><b>1 h 12 m</b><span>time</span></div><div><b>after 14</b><span>why it stopped</span></div></div>', 440), "<b>The status bar and the receipt</b>");
    h += "</div><div class=\"row\" style=\"align-items:flex-start\">";
    h += fig(look18("full", "night", '<div class="bar18"><span class="p18 full pri">' + ic15("071221", 16, "marker") + 'Start (this quest)</span><span class="p18 full sm dots">' + ICON.more + "</span></div>" + menu([["Start here and keep going", "Questionable continues after this quest", 0], ["Do this next", "#1 on Questionable\'s list", 0], "-", ["Copy quest link", "", 0], ["Report a data problem", "", 0]]), 400), "<b>A6, the detail pane\'s \"…\"</b>: Start does one quest; the old behaviour and Do this next are here");
    h += fig(look18("full", "night", menu([["Start here and keep going", "", 0], ["Do this next", "Not while Questionable runs", 1]]), 400), "<b>Do this next, disabled</b>: only while Questionable runs, and only if Questionable#45 reproduces in testing; the reason is in the item");
    h += fig(look18("quiet", "dawn", '<div class="bar18"><span class="p18 quiet pri">Start (this quest)</span><span class="p18 quiet sm dots">' + ICON.more + "</span></div>" + menu([["Start here and keep going", "", 0], ["Do this next", "", 0]]), 400), "<b>Quiet, Dawn</b>");
    return h + "</div></div>";
  }

  // ======================================================================================================
  // 1.22 "Welcome home" (docs/design/v8/spec-1.22.md): W1–W3, U1, H1–H2, M1, M3, and the Tsukimichi for Umbra
  // add-on (A1–A3). Tsukimichi surfaces reuse the 1.14–1.21 parts (palettes, Decoration, p18 pills, the card frame);
  // Umbra surfaces use Umbra's own colours and metrics (una-xiv/umbra: Umbra.Colors.cs and the widgets/*.xml UDTs).
  // Paths: the page's <base> is docs/design/v7/ui/, so 1.22's files are under ../../v8/ and the scenes under
  // ../../../plan-site/mock/ (official FFXIV screenshots, used as the game behind the UI).
  // ======================================================================================================
  var V8 = "../../v8/", SCN = "../../../plan-site/mock/";
  var TH22 = [
    { k: "medallion", name: "Menphina's Medallion", pal: "night", kit: "brass", kitn: "Brass", paln: "Night" },
    { k: "classic", name: "Classic", pal: "night", kit: "brass", kitn: "Brass", paln: "Night" },
    { k: "ishgard-glass", name: "Ishgard Glass", pal: "snow", kit: "came", kitn: "Lead came", paln: "Ishgard Snow" },
    { k: "aether-crystal", name: "Aether Crystal", pal: "night", kit: "silver", kitn: "Silver", paln: "Night" },
    { k: "astrologian-orrery", name: "Astrologian's Orrery", pal: "dawn", kit: "astro", kitn: "Astrolabe", paln: "Dawn" },
    { k: "sumi-to-kinpaku", name: "Sumi to Kinpaku", pal: "kugane", kit: "kiri", kitn: "Kirikane", paln: "Kugane Lacquer" }];
  function th22(k) { for (var i = 0; i < TH22.length; i++) if (TH22[i].k === k) return TH22[i]; return TH22[0]; }
  var u22 = 0;

  // ---------- small line icons (drawn, so the mock needs no icon font) ----------
  var I22 = {
    x: '<svg viewBox="0 0 10 10"><path d="M1.5 1.5l7 7M8.5 1.5l-7 7" stroke="currentColor" stroke-width="1.4" stroke-linecap="round"/></svg>',
    lock: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="2.5" y="6" width="9" height="6.5" rx="1.3"/><path d="M4.5 6V4.4a2.5 2.5 0 0 1 5 0V6"/></svg>',
    unlock: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3"><rect x="2.5" y="6" width="9" height="6.5" rx="1.3"/><path d="M4.5 6V4.4a2.5 2.5 0 0 1 4.8-1"/></svg>',
    hide: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"><path d="M1.5 7s2-3.6 5.5-3.6S12.5 7 12.5 7 10.5 10.6 7 10.6 1.5 7 1.5 7z"/><circle cx="7" cy="7" r="1.7"/><path d="M2.5 12l9-10"/></svg>',
    moon: '<svg viewBox="0 0 14 14"><path d="M9.3 1.2a5.8 5.8 0 1 0 3.5 9.8A4.9 4.9 0 0 1 9.3 1.2z" fill="currentColor"/></svg>',
    gear: '<svg viewBox="0 0 14 14"><g fill="currentColor">' + [0, 45, 90, 135, 180, 225, 270, 315].map(function (a) { return '<rect x="6" y="0.6" width="2" height="3" rx=".4" transform="rotate(' + a + ' 7 7)"/>'; }).join("") + '</g><circle cx="7" cy="7" r="4.3" fill="currentColor"/><circle cx="7" cy="7" r="1.7" fill="#000" fill-opacity=".55"/></svg>',
    route: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"><circle cx="3" cy="11" r="1.6"/><circle cx="11" cy="3" r="1.6"/><path d="M4.4 10.2C8 9 5 6 9.5 4"/></svg>',
    clock: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"><circle cx="7" cy="7" r="5.4"/><path d="M7 4v3.3l2.2 1.4"/></svg>',
    book: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3"><path d="M2 2.5h4a1.5 1.5 0 0 1 1 .4 1.5 1.5 0 0 1 1-.4h4v9H8a1 1 0 0 0-1 .6 1 1 0 0 0-1-.6H2z"/><path d="M7 2.9v8.6"/></svg>',
    open: '<svg viewBox="0 0 14 14" fill="none" stroke="currentColor" stroke-width="1.3" stroke-linecap="round"><path d="M8 2h4v4M12 2L6.5 7.5M10.5 8.5v3h-8v-8h3"/></svg>',
    menu: '<svg viewBox="0 0 14 14" fill="currentColor"><rect x="2" y="2" width="4" height="4" rx="1"/><rect x="8" y="2" width="4" height="4" rx="1"/><rect x="2" y="8" width="4" height="4" rx="1"/><rect x="8" y="8" width="4" height="4" rx="1"/></svg>'
  };
  function ic22(name, px, col) { return '<span style="display:inline-flex;width:' + px + "px;height:" + px + "px;color:" + (col || "currentColor") + '">' + I22[name].replace("<svg ", '<svg width="' + px + '" height="' + px + '" ') + "</span>"; }

  // ---------- H1: the moon icon, one face per theme ----------
  // A 64-unit box; the crescent is lit on its upper-left limb, the one light of every Tsukimichi surface. The rim is
  // the kit's resting metal, never the act-now gilt (gold means act now, and the icon asks for nothing).
  var FACE22 = {
    "medallion": { rim: ["#E6CF98", "#9A7E4A", "#7C6236", "#5C4724"], well: ["#2C3D80", "#16204A", "#0C1230"], moon: ["#F7F4EA", "#D9D2BE"], earth: "#3C4C8A",
      extra: '<circle cx="18" cy="44" r=".7" fill="#DCE5FF" opacity=".55"/><circle cx="46" cy="47" r=".6" fill="#DCE5FF" opacity=".45"/><circle cx="49" cy="20" r=".55" fill="#FFE2A8" opacity=".5"/>' },
    "classic": { rim: null, well: ["#24305E", "#1C2752", "#141C40"], moon: ["#F4E9C6", "#E2D3A6"], earth: "#2C3866", extra: "" },
    "ishgard-glass": { rim: ["#B8C0D0", "#8C95B0", "#5A6278", "#323950"], well: ["#4A68AE", "#2E4682", "#1E3064"], moon: ["#F6F1E2", "#E3DAC3"], earth: "#3A5292",
      extra: '<path d="M5 41c9-3 18-3 27 0s18 3 27 0" fill="none" stroke="#323950" stroke-width="1.3" opacity=".9"/><path d="M5 41.6c9-3 18-3 27 0s18 3 27 0V60H5z" fill="#1B2A58" opacity=".55"/><path d="M14 14l5 5" stroke="#FFFFFF" stroke-opacity=".22" stroke-width="2" stroke-linecap="round"/>', moonStroke: "#323950" },
    "aether-crystal": { rim: ["#E2E8F4", "#A9B5D0", "#7B8AAF", "#5E6E97"], well: ["#2C3C62", "#18233E", "#0E1528"], moon: ["#EEF8FF", "#C9E3F2"], earth: "#2A3E66",
      extra: '<path d="M10 40L32 54 54 38M32 54V60M20 12l12 8 12-8" fill="none" stroke="#9BE6FF" stroke-opacity=".16" stroke-width=".8"/>', facet: true },
    "astrologian-orrery": { rim: ["#EAD3A0", "#B8924E", "#7C6034", "#4E3B1E"], well: ["#2A3778", "#1A245A", "#111A40"], moon: ["#F8EBCF", "#E2CFA6"], earth: "#2E3C7A",
      extra: '<path d="M38 48l6-3 5 4" fill="none" stroke="#EAD3A0" stroke-opacity=".35" stroke-width=".6"/><circle cx="38" cy="48" r=".9" fill="#F4E4BC" opacity=".8"/><circle cx="44" cy="45" r=".7" fill="#F4E4BC" opacity=".7"/><circle cx="49" cy="49" r="1" fill="#F4E4BC" opacity=".85"/>', inner: "#7C6034" },
    "sumi-to-kinpaku": { rim: ["#4A444C", "#2A262E", "#141216", "#060508"], well: ["#16131A", "#0E0C10", "#060508"], moon: ["#F3EAD3", "#E2D7BB"], earth: "#2A2832",
      extra: '<path d="M8 22L22 8" stroke="#FFFFFF" stroke-opacity=".07" stroke-width="9" stroke-linecap="round"/>', kiri: true }
  };
  function face22(th, lv) {
    var id = "f22_" + (++u22), F = FACE22[th], d = "", g = "";
    if (lv === "plain") {
      return '<circle cx="32" cy="32" r="28" fill="#151A28"/><circle cx="32" cy="32" r="28.5" fill="none" stroke="#DDE3F0" stroke-opacity=".6" stroke-width="1.6"/>' +
        '<defs><mask id="' + id + 'm"><circle cx="32" cy="32" r="15" fill="#fff"/><circle cx="37.6" cy="36.4" r="14.2" fill="#000"/></mask></defs><circle cx="32" cy="32" r="15" fill="#DDE3F0" mask="url(#' + id + 'm)"/>';
    }
    d += '<radialGradient id="' + id + 'w" cx=".38" cy=".32" r=".75"><stop offset="0" stop-color="' + F.well[0] + '"/><stop offset=".6" stop-color="' + F.well[1] + '"/><stop offset="1" stop-color="' + F.well[2] + '"/></radialGradient>';
    d += '<linearGradient id="' + id + 'mo" x1="0" y1="0" x2="1" y2="1"><stop offset=".2" stop-color="' + F.moon[0] + '"/><stop offset="1" stop-color="' + F.moon[1] + '"/></linearGradient>';
    d += '<mask id="' + id + 'm"><circle cx="32" cy="32" r="15" fill="#fff"/><circle cx="37.6" cy="36.4" r="14.2" fill="#000"/></mask>';
    d += '<clipPath id="' + id + 'c"><circle cx="32" cy="32" r="27"/></clipPath>';
    if (F.rim) d += '<linearGradient id="' + id + 'r" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="' + F.rim[0] + '"/><stop offset=".45" stop-color="' + F.rim[1] + '"/><stop offset=".8" stop-color="' + F.rim[2] + '"/><stop offset="1" stop-color="' + F.rim[3] + '"/></linearGradient>';
    if (lv === "quiet" || !F.rim) {
      g += '<circle cx="32" cy="32" r="29.5" fill="url(#' + id + 'w)"/><circle cx="32" cy="32" r="29.5" fill="none" stroke="' + (th === "classic" ? "#DDE3F0" : "#C3CBDF") + '" stroke-opacity=".62" stroke-width="1.3"/>';
    } else {
      g += '<circle cx="32" cy="32" r="31" fill="url(#' + id + 'r)"/><circle cx="32" cy="32" r="31.4" fill="none" stroke="#080B16" stroke-opacity=".6" stroke-width=".8"/>';
      if (F.kiri) g += '<circle cx="32" cy="32" r="29.2" fill="none" stroke="#DEB862" stroke-width=".9"/><circle cx="32" cy="32" r="31" fill="none" stroke="#F2ECDD" stroke-opacity=".34" stroke-width=".5"/>';
      if (F.inner) g += '<circle cx="32" cy="32" r="28.4" fill="none" stroke="' + F.inner + '" stroke-width=".9"/>';
      if (F.facet) g += [45, 135, 225, 315].map(function (a) { var r = 30.2, x = 32 + r * Math.cos(a * Math.PI / 180), y = 32 + r * Math.sin(a * Math.PI / 180); return '<rect x="' + (x - 1.6) + '" y="' + (y - 1.6) + '" width="3.2" height="3.2" transform="rotate(45 ' + x + " " + y + ')" fill="' + (a === 225 ? "#F6F9FE" : "#B9C4DC") + '" stroke="#5E6E97" stroke-width=".4"/>'; }).join("");
      g += '<circle cx="32" cy="32" r="27.7" fill="#080B16" fill-opacity=".75"/>';
      g += '<circle cx="32" cy="32" r="27" fill="url(#' + id + 'w)"/>';
      g += '<path d="M8.6 20.5A26 26 0 0 1 20.5 8.6" fill="none" stroke="#FFFFFF" stroke-opacity="' + (F.kiri ? ".25" : ".38") + '" stroke-width="1.2" stroke-linecap="round" transform="translate(-2.2 -2.2) scale(1.07)"/>';
    }
    g += '<g clip-path="url(#' + id + 'c)">' + F.extra + "</g>";
    g += '<circle cx="32" cy="32" r="15" fill="' + F.earth + '" fill-opacity=".38"/>';
    g += '<circle cx="32" cy="32" r="15" fill="url(#' + id + 'mo)" mask="url(#' + id + 'm)"/>';
    if (F.moonStroke) g += '<g mask="url(#' + id + 'm)"><circle cx="32" cy="32" r="14.6" fill="none" stroke="' + F.moonStroke + '" stroke-width="1"/></g>';
    if (F.facet) g += '<path d="M21 23.5L25.5 31 22 40" fill="none" stroke="#9BE6FF" stroke-opacity=".55" stroke-width=".6" mask="url(#' + id + 'm)"/>';
    return "<defs>" + d + "</defs>" + g;
  }

  // ---------- H2: particles. Deterministic in t (seconds), drawn in the icon's 64-unit box ----------
  function env22(u, a, b) { return u < a ? u / a : u > 1 - b ? Math.max(0, (1 - u) / b) : 1; }
  function hash22(i, c) { var x = Math.sin(i * 127.1 + c * 311.7) * 43758.5453; return x - Math.floor(x); }
  function fx22(th, t) {
    var back = "", front = "", i;
    if (th === "medallion") {
      // three drifting gold motes: each lives 3.6 s, rises 4.5 units/s with a slight sway, fades in 0.6 s and out 1.2 s
      for (i = 0; i < 3; i++) {
        var T = 3.6, s = t + i * 1.2, cyc = Math.floor(s / T), u = (s % T) / T;
        var a0 = (200 + 140 * hash22(i, cyc)) * Math.PI / 180, r0 = 27 + 9 * hash22(i + 7, cyc);
        var x = 32 + r0 * Math.cos(a0) + 1.6 * u * T + 1.5 * Math.sin(u * 5), y = 32 - r0 * Math.sin(a0) * 0.2 + 18 - 4.5 * u * T;
        var al = 0.55 * env22(u, 0.17, 0.33);
        front += '<circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="3" fill="#FFE2A8" opacity="' + (al * 0.22).toFixed(3) + '"/><circle cx="' + x.toFixed(1) + '" cy="' + y.toFixed(1) + '" r="1.05" fill="#FFE9BE" opacity="' + al.toFixed(3) + '"/>';
      }
    } else if (th === "ishgard-glass") {
      // a frost glint runs the lit (upper-left) quarter of the rim every 6 s, 1.2 s long; one round sparkle at its head
      var P = 6, u2 = (t % P) / 1.2;
      if (u2 <= 1) {
        var c = 195 + 60 * u2, al2 = Math.sin(Math.PI * u2) * 0.85, r = 30.4;
        var a1 = (c - 14) * Math.PI / 180, a2 = (c + 14) * Math.PI / 180;
        front += '<path d="M' + (32 + r * Math.cos(a1)).toFixed(2) + " " + (32 + r * Math.sin(a1)).toFixed(2) + "A" + r + " " + r + " 0 0 1 " + (32 + r * Math.cos(a2)).toFixed(2) + " " + (32 + r * Math.sin(a2)).toFixed(2) + '" fill="none" stroke="#FFFFFF" stroke-width="1.5" stroke-linecap="round" opacity="' + al2.toFixed(3) + '"/>';
        var hx = 32 + r * Math.cos(a2), hy = 32 + r * Math.sin(a2);
        front += '<circle cx="' + hx.toFixed(2) + '" cy="' + hy.toFixed(2) + '" r="2.8" fill="#E6F0FF" opacity="' + (al2 * 0.35).toFixed(3) + '"/><circle cx="' + hx.toFixed(2) + '" cy="' + hy.toFixed(2) + '" r="1" fill="#FFFFFF" opacity="' + al2.toFixed(3) + '"/>';
      }
    } else if (th === "aether-crystal") {
      // two shards on a slow 16 s orbit (rx 1.25 R, ry 0.4 R, tilted 28 degrees, as the Orrery's). On the near half
      // they pass in front, low; on the far half they are behind the icon and hidden, so a shard is never above the
      // crescent (theme-system §2: no crystal over a crescent). Each turns every 3.2 s and flashes, as light, when its
      // face meets the upper-left light.
      // The orbit rings the icon's foot: centred 0.6 R below the centre (rx 1.25 R, ry 0.45 R), so its near arc runs
      // just outside the bottom of the rim. A shard is hidden only where the icon really covers it (the far half,
      // inside the rim), so no shard is ever drawn over the face, let alone the lit crescent.
      for (i = 0; i < 2; i++) {
        var ang = (t / 16 * 2 * Math.PI) + i * Math.PI + 0.6;
        var sx = 32 + 40 * Math.cos(ang), sy = 51 + 15 * Math.sin(ang);
        var dc = Math.hypot(sx - 32, sy - 32);
        var vis = Math.sin(ang) >= 0 ? 1 : Math.min(1, Math.max(0, (dc - 33) / 4));
        if (vis <= 0) continue;
        var rot = (t / 3.2 * 360 + i * 110) % 360, fl = Math.max(0, Math.cos((rot - 315) * Math.PI / 180)) ** 6;
        front += '<g opacity="' + vis.toFixed(3) + '" transform="translate(' + sx.toFixed(2) + " " + sy.toFixed(2) + ") rotate(" + (rot * 0.25 - 20).toFixed(1) + ')"><path d="M0 -4.2L1.7 0 0 3.2 -1.6 -.2z" fill="#3E7FA8" opacity=".75"/><path d="M0 -4.2L-1.6 -.2 0 3.2z" fill="#BDEFFF" opacity="' + (0.35 + 0.6 * fl).toFixed(3) + '"/>' +
          (fl > 0.2 ? '<circle r="3.4" fill="url(#fl22)" style="mix-blend-mode:screen" opacity="' + (fl * 0.8).toFixed(3) + '"/>' : "") + "</g>";
      }
    } else if (th === "astrologian-orrery") {
      // one bead on a 12 s orbit (rx 42, ry 13, tilted 28 degrees); on the far half it passes behind the icon
      var tilt = -28 * Math.PI / 180, oa = t / 12 * 2 * Math.PI + 0.3, ox = 42 * Math.cos(oa), oy = 13 * Math.sin(oa);
      var bx = 32 + ox * Math.cos(tilt) - oy * Math.sin(tilt), by = 32 + ox * Math.sin(tilt) + oy * Math.cos(tilt);
      var orbit = function (a0, a1) { var p = ""; for (var k = 0; k <= 40; k++) { var a = a0 + (a1 - a0) * k / 40, x = 42 * Math.cos(a), y = 13 * Math.sin(a); p += (k ? "L" : "M") + (32 + x * Math.cos(tilt) - y * Math.sin(tilt)).toFixed(2) + " " + (32 + x * Math.sin(tilt) + y * Math.cos(tilt)).toFixed(2); } return p; };
      back += '<path d="' + orbit(Math.PI, 2 * Math.PI) + '" fill="none" stroke="#EAD3A0" stroke-opacity=".14" stroke-width=".6"/>';
      front += '<path d="' + orbit(0, Math.PI) + '" fill="none" stroke="#EAD3A0" stroke-opacity=".14" stroke-width=".6"/>';
      var bead = '<circle cx="' + bx.toFixed(2) + '" cy="' + by.toFixed(2) + '" r="3.4" fill="#F2DDA8" opacity=".16"/><circle cx="' + bx.toFixed(2) + '" cy="' + by.toFixed(2) + '" r="1.7" fill="#F2DDA8"/><circle cx="' + (bx - 0.5).toFixed(2) + '" cy="' + (by - 0.5).toFixed(2) + '" r=".6" fill="#FFF6DE"/>';
      if (Math.sin(oa) < 0) back += bead; else front += bead;
    } else if (th === "sumi-to-kinpaku") {
      // two gold-leaf flecks drift down past the icon's right side (4.5 s each), fluttering; they flash when tilted to the light
      for (i = 0; i < 2; i++) {
        var L = 4.5, s3 = t + i * 2.25, c3 = Math.floor(s3 / L), u3 = (s3 % L) / L;
        var fx = 46 + 10 * hash22(i, c3) + 3 * Math.sin(u3 * 7 + i), fy = -10 + 64 * u3;
        var rot3 = Math.sin(u3 * 9 + i * 2) * 60, fl3 = Math.max(0, Math.cos((rot3 + 40) * Math.PI / 180)) ** 8;
        var a3 = 0.75 * env22(u3, 0.15, 0.25);
        front += '<g transform="translate(' + fx.toFixed(2) + " " + fy.toFixed(2) + ") rotate(" + rot3.toFixed(1) + ') scale(1 ' + (0.45 + 0.55 * Math.abs(Math.cos(rot3 * Math.PI / 90))).toFixed(2) + ')"><path d="M-1.8 -1.2L1.6 -1.5 1.9 1.1 -1.4 1.6z" fill="' + (fl3 > 0.4 ? "#F4DA92" : "#C9A24E") + '" opacity="' + a3.toFixed(3) + '"/></g>';
      }
    } else if (th === "classic") {
      // three small round stars that breathe (7-13 s), as the 1.14 sky: never a cross, never a flicker
      [[-6, 10, 1.1, 7, 0], [70, 18, 1.3, 9.5, 2], [64, 62, 0.9, 12, 5]].forEach(function (s) {
        var ph = ((t + s[4]) % s[3]) / s[3], cu = ph < 0.3 ? ph / 0.3 : ph < 0.7 ? 1 - (ph - 0.3) / 0.4 * 2 : -1 + (ph - 0.7) / 0.3;
        var a4 = 0.38 * (1 + 0.28 * cu);
        front += '<circle cx="' + s[0] + '" cy="' + s[1] + '" r="' + (s[2] * 2.6) + '" fill="#DCE5FF" opacity="' + (a4 * 0.18).toFixed(3) + '"/><circle cx="' + s[0] + '" cy="' + s[1] + '" r="' + s[2] + '" fill="#F4F2EA" opacity="' + a4.toFixed(3) + '"/>';
      });
    }
    return { back: back, front: front };
  }

  // The icon: o = { lv, hover (0..1), dot ("update" | "needs"), t (seconds) or fx false, light (on a bright scene) }
  function mi22(th, px, o) {
    o = o || {};
    var lv = o.lv || "full", hv = o.hover || 0, id = "mi22_" + (++u22);
    var showFx = lv === "full" && o.fx !== false && o.t !== undefined;
    var p = showFx ? fx22(th, o.t) : { back: "", front: "" };
    var rise = (lv === "full" ? 2 : lv === "quiet" ? 1 : 0) * hv * 64 / 40;
    var dy = 2.2 + 1.8 * hv, sd = 2.2 + 1.4 * hv, so = lv === "plain" ? 0 : 0.5 - 0.08 * hv;
    var s = '<svg class="mi22" width="' + (px * 144 / 64).toFixed(1) + '" height="' + (px * 144 / 64).toFixed(1) + '" viewBox="-40 -40 144 144" aria-hidden="true">' +
      '<defs><filter id="' + id + 's" x="-60%" y="-60%" width="220%" height="220%"><feDropShadow dx="0" dy="' + dy.toFixed(2) + '" stdDeviation="' + sd.toFixed(2) + '" flood-color="#000" flood-opacity="' + so.toFixed(2) + '"/></filter>' +
      '<radialGradient id="' + id + 'g"><stop offset=".55" stop-color="#E2E8F4" stop-opacity=".22"/><stop offset="1" stop-color="#E2E8F4" stop-opacity="0"/></radialGradient>' +
      '<radialGradient id="fl22"><stop offset="0" stop-color="#F4FCFF" stop-opacity=".9"/><stop offset="1" stop-color="#BFEFFF" stop-opacity="0"/></radialGradient></defs>';
    s += p.back;
    if (hv > 0 && lv !== "plain") s += '<circle cx="32" cy="' + (32 - rise) + '" r="44" fill="url(#' + id + 'g)" opacity="' + (hv * (lv === "quiet" ? 0.7 : 1)).toFixed(2) + '"/>';
    s += '<g transform="translate(0 ' + (-rise).toFixed(2) + ')" filter="url(#' + id + 's)">' + face22(th, lv) + "</g>";
    if (hv > 0 && lv === "plain") s += '<circle cx="32" cy="32" r="31" fill="none" stroke="#DDE3F0" stroke-opacity=".8" stroke-width="1.5"/>';
    s += '<g transform="translate(0 ' + (-rise).toFixed(2) + ')">' + p.front;
    if (o.dot) s += '<circle cx="54.6" cy="9.4" r="6.6" fill="#0F1424"/><circle cx="54.6" cy="9.4" r="4.9" fill="' + (o.dot === "needs" ? "#D08654" : "#6F8FD0") + '"/>';
    s += "</g></svg>";
    return s;
  }

  // ---------- W1: the What's new popup ----------
  var REL22 = {
    welcome: { v: "1.22.0", name: "Welcome home", date: "5 October 2026", pts: [
      ["What's new, in pictures.", "After each update, a short note like this one shows what changed. Past notes are in Settings › About."],
      ["Know when an update is ready.", "A quiet note in the status bar and a dot on the moon icon tell you. Update opens Dalamud's installer."],
      ["A moon on your screen.", "A small moon opens Tsukimichi and shows what's next when you hover it. Move it, lock it or hide it."],
      ["At home in Umbra.", "Tsukimichi keeps clear of Umbra's toolbar, and an Umbra add-on puts Tonight in your Umbra bar."]] },
    whatnext: { v: "1.21.0", name: "What next, for every character", date: "4 October 2026", pts: [
      ["Up next.", "Tonight starts with one suggestion for the character you're on, and says why."],
      ["Go to the current step.", "Travel aims at the step you're on, not only at the quest giver."],
      ["All your characters.", "One board shows each character's story, goals and what's Ready."],
      ["Loose ends.", "Storylines you started and never finished, finales first."]] },
    evercold: { v: "1.20.0", name: "Before Evercold", date: "4 October 2026", pts: [
      ["Fewer spoilers.", "Past where you are in the story, Tsukimichi now hides the names of places, duties, rewards and people too. Hover a hidden name to see why."],
      ["Ready for Evercold.", "A card in Tonight lists what each character should finish before Patch 8.0: the story, journal room, job quests, roulettes and flying. Tick lines off as you go."],
      ["More faces, if you want them.", "An optional download adds about 1,850 quest-giver portraits. Nothing downloads until you choose it in Settings › Look."]] },
    answers: { v: "1.19.0", name: "Right answers", date: "4 October 2026", pts: [
      ["The game has the last word.", "When the game offers you a quest, Tsukimichi shows it as Ready, even one it couldn't check before."],
      ["Know how you'll clear it.", "Quests with a duty say whether NPCs can come along or you need a group, and which job gets the most from the EXP."],
      ["Room in your journal.", "See how full your journal is, and what you can finish or safely drop to make room."],
      ["Events ending soon.", "Seasonal events warn you three days before they end, and their quests come first."],
      ["Find by unlock.", "Search for “Kugane” or “flying Thavnair” to see the quests that open it, with a route there."]] }
  };
  function corners22(kit) {
    if (kit === "came") {
      var q = '<svg viewBox="0 0 12 12"><g fill="#8C95B0" stroke="#323950" stroke-width=".7"><circle cx="6" cy="3.2" r="2.4"/><circle cx="6" cy="8.8" r="2.4"/><circle cx="3.2" cy="6" r="2.4"/><circle cx="8.8" cy="6" r="2.4"/></g><circle cx="6" cy="6" r="1.4" fill="#F4F6FA"/></svg>';
      return ['left:-6px;top:-6px', 'right:-6px;top:-6px', 'left:-6px;bottom:-6px', 'right:-6px;bottom:-6px'].map(function (p) { return '<i class="qf" style="' + p + '">' + q + "</i>"; }).join("");
    }
    if (kit === "silver") return ['left:-4px;top:-4px', 'right:-4px;top:-4px', 'left:-4px;bottom:-4px', 'right:-4px;bottom:-4px'].map(function (p) { return '<i class="ch" style="' + p + '"></i>'; }).join("");
    if (kit === "astro") {
      var sc = function (r) { return '<svg viewBox="0 0 13 13" style="transform:rotate(' + r + 'deg)"><path d="M1 12A11 11 0 0 1 12 1" fill="none" stroke="#EAD3A0" stroke-width="1.2"/>' + [0, 1, 2, 3, 4].map(function (k) { var a = (180 + k * 22.5) * Math.PI / 180, x1 = 12 + 11 * Math.cos(a), y1 = 12 + 11 * Math.sin(a), x2 = 12 + 8.6 * Math.cos(a), y2 = 12 + 8.6 * Math.sin(a); return '<path d="M' + x1.toFixed(2) + " " + y1.toFixed(2) + "L" + x2.toFixed(2) + " " + y2.toFixed(2) + '" stroke="#B8924E" stroke-width=".8"/>'; }).join("") + "</svg>"; };
      return '<i class="sca" style="left:-3px;top:-3px">' + sc(0) + '</i><i class="sca" style="right:-3px;top:-3px">' + sc(90) + '</i><i class="sca" style="right:-3px;bottom:-3px">' + sc(180) + '</i><i class="sca" style="left:-3px;bottom:-3px">' + sc(270) + "</i>";
    }
    if (kit === "kiri") return ['left:-4px;top:-4px', 'right:-4px;top:-4px', 'left:-4px;bottom:-4px', 'right:-4px;bottom:-4px'].map(function (p) { return '<i class="km" style="' + p + '"></i>'; }).join("");
    return '<i class="cm a"></i><i class="cm b"></i><i class="cm c"></i><i class="cm d"></i>';
  }
  // o: { lv, page: "evercold"|"answers", idx, total, one, big, from }
  function popup22(th, o) {
    var T = th22(th), lv = o.lv || "full", R = REL22[o.page];
    var cls = "mk " + lv + " v7 " + (T.pal !== "night" ? T.pal + " " : "") + "wn22" + (lv === "full" ? " k-" + T.kit : "") + (o.one ? " one" : "") + (o.big ? " big" : "");
    var h = '<div class="' + cls + '">' + (lv === "full" ? corners22(T.kit) : "");
    h += '<div class="wh">' + (lv === "plain" ? "" : mi22(th, 22, { lv: lv, fx: false }).replace('class="mi22"', 'class="mi22" style="margin:-15px -14px"')) + '<span class="ey">What\'s new</span>' +
      '<span class="vc">' + (o.from ? "You were on " + o.from : "") + '</span><span class="x" title="Close">' + I22.x + "</span></div>";
    if (lv !== "plain") h += '<div class="art"><img src="' + V8 + "art/themed/" + o.page + "-" + th + (lv === "quiet" ? "-quiet" : "") + '.png" alt=""></div>';
    h += '<div class="wtt">' + R.name + '</div><div class="wst">Tsukimichi ' + R.v + " · " + R.date + "</div>";
    h += '<div class="ptw"><ul class="pts">' + R.pts.map(function (p) { return "<li><b>" + p[0] + "</b> " + p[1] + "</li>"; }).join("") + "</ul>" + (o.scroll ? '<i class="sbar"><i></i></i>' : "") + "</div>";
    var idx = o.idx || 1, tot = o.total || 1;
    h += '<div class="wf"><span class="al">All releases</span><span class="pg22"><span class="rb' + (idx <= 1 ? " dis" : "") + '">‹</span><span class="pn">' + idx + " of " + tot + '</span><span class="rb' + (idx >= tot ? " dis" : "") + '">›</span></span>' + pill18(lv, "Close") + "</div>";
    return h + "</div>";
  }
  function scene22(inner, img, pad, extra) { return '<div class="sc22' + (extra || "") + '"><div class="bg" style="background-image:url(' + SCN + (img || "scene-night.jpg") + ')"></div><div class="fg" style="padding:' + (pad || 16) + 'px">' + inner + "</div></div>"; }
  function cap22(t, s) { return '<figcaption><b>' + t + "</b>" + (s ? " <i>· " + s + "</i>" : "") + "</figcaption>"; }

  function board_wn22a() { return boardWn22("evercold", 3, "1.22 · What's new, page 3 of 4", "A player on 1.18.0 updates to 1.22.0: four releases arrived, so four pages, newest first (1.22.0, 1.21.0, then these two). The popup keeps the height of its tallest page (page 4 has five points), so ‹ › never moves. One painting per release, restyled by each theme (decision 1)."); }
  function board_wn22b() { return boardWn22("answers", 4, "1.22 · What's new, page 4 of 4", "The same popup on its last page, 1.19.0 Right answers: five points, the most a release gets. The moonlit road is the same painting in every theme; each theme's grade and motif layer restyle it, and the kit draws the frame."); }
  function boardWn22(page, idx, title, sub) {
    var h = '<div class="board b15 b22"><h2>' + title + "<small>" + sub + '</small></h2><div class="row">';
    TH22.forEach(function (T) {
      h += "<figure>" + scene22(popup22(T.k, { lv: "full", page: page, idx: idx, total: 4, from: "1.18.0" })) + cap22(T.name, T.paln + " palette · " + T.kitn + " frames") + "</figure>";
    });
    return h + "</div></div>";
  }

  // ---------- W1 states ----------
  function board_wn22s() {
    var h = '<div class="board b15 b22"><h2>1.22 · The popup: states<small>The first page anyone sees in 1.22 (Full, Medallion), Quiet and Plain, one release with no pager (a player on 1.21), and Text size 150 % on a short and a long page. The width is fixed; only height follows the text.</small></h2>';
    h += '<div class="row">';
    h += "<figure>" + scene22(popup22("medallion", { lv: "full", page: "welcome", idx: 1, total: 4, from: "1.18.0" })) + cap22("Full, page 1 of 4", "1.22.0 Welcome home: the kit frame, the graded art with its motif, the Eyebrow, Title face") + "</figure>";
    h += "<figure>" + scene22(popup22("medallion", { lv: "quiet", page: "evercold", idx: 3, total: 4, from: "1.18.0" })) + cap22("Quiet", "a tonal card and a hairline; the header keeps the theme's moon; the art is graded with no motif layer") + "</figure>";
    h += "<figure>" + scene22(popup22("medallion", { lv: "plain", page: "evercold", idx: 3, total: 4, from: "1.18.0" })) + cap22("Plain", "no art (nothing is loaded), the ledger band, flat buttons") + "</figure>";
    h += '</div><div class="row">';
    h += "<figure>" + scene22(popup22("astrologian-orrery", { lv: "full", page: "welcome", one: true })) + cap22("One release", "a player on 1.21: no pager and no “You were on”") + "</figure>";
    h += "<figure>" + scene22(popup22("medallion", { lv: "full", page: "evercold", idx: 3, total: 4, big: true, from: "1.18.0" })) + cap22("150 %, page 3 of 4", "the notes block is the tallest page's height at this size, capped so the popup fits within 80 % of the screen; page 3 just fits it") + "</figure>";
    h += "<figure>" + scene22(popup22("medallion", { lv: "full", page: "answers", idx: 4, total: 4, big: true, scroll: true, from: "1.18.0" })) + cap22("150 %, page 4 of 4", "the same block; this page is longer than the cap, so its notes scroll (ImGui's thin scrollbar) and the footer stays put") + "</figure>";
    h += "</div>";
    h += '<div class="row"><div class="spec" style="width:1440px"><table>' +
      "<tr><th>Part</th><th>Size and rule (logical px at UI scale 1.0)</th></tr>" +
      "<tr><td>Window</td><td>560 wide, centred on the main window if it is open, otherwise on the screen. Not modal: the game keeps its input. Esc or Close dismisses.</td></tr>" +
      "<tr><td>Header</td><td>48: the theme's moon icon (28; Full and Quiet), WHAT'S NEW (Eyebrow at Full), “You were on 1.18.0” in Secondary when more than one release arrived, × (26 round)</td></tr>" +
      "<tr><td>Art</td><td>536 × 220, inset 12, radius 4, the kit keyline. Loaded when the popup opens, released when it closes. None at Plain.</td></tr>" +
      "<tr><td>Title</td><td>The release name in the Title face (Jupiter 23), then “Tsukimichi 1.20.0 · 4 October 2026” in Secondary</td></tr>" +
      "<tr><td>Notes</td><td>3 to 5 points: a semibold lead, then one plain sentence. The block is the tallest page's height at the current Text size, plus 14 of bottom room, capped so the popup stays within 80 % of the screen; past the cap the notes scroll. So the footer never moves.</td></tr>" +
      "<tr><td>Footer</td><td>56: All releases (opens Settings › About › What's new), ‹ 1 of 4 › (24 round, disabled at the ends), Close (neutral)</td></tr>" +
      "<tr><td>Motion</td><td>Opens with PopupFade and Rise (0.16 s, 4 px). Pages cross-fade art and text over Select (0.15 s); nothing slides. Reduce motion and Plain: instant.</td></tr>" +
      "<tr><td>When</td><td>Once per update, never on a first install, at the first quiet moment after login: not in combat, a duty, a cutscene, Group Pose or a loading screen.</td></tr>" +
      "</table></div></div>";
    return h + "</div>";
  }

  // ---------- W2: the art ----------
  var ART22 = {
    welcome: ["a door left open on a lit hall", "two lights: the moon in front of you backlights the house (its face in shadow, a cool rim on the roof, its shadow toward you); the hall's warm light spills down the path"],
    whatnext: ["a lantern at a crossroads, paths to several lights", "late twilight, the afterglow low on the right; a young crescent lit toward it; one lantern on the ground, its pool foreshortened, the waystone's shadow thrown away from it"],
    evercold: ["cold, snow, a coming dawn", "the sun under the horizon behind the pass; the crescent lit on its sun side; ridges backlit and darker than the sky; shadows toward you"],
    answers: ["a clear moonlit road", "one light, the near-full moon; the road brightens toward it; the signpost and the tree are rim-lit on their moon side and throw their shadows toward you"]
  };
  var ARTNOTE22 = { "medallion": "as painted", "classic": "softer, a little cooler", "ishgard-glass": "milky daylight grade, rime at the corners", "aether-crystal": "cool highlights, shards far from the moon", "astrologian-orrery": "plum and dawn gold, orbits clear of the moon", "sumi-to-kinpaku": "ink grade, gold dust in the top corners" };
  function board_art22() {
    var h = '<div class="board b15 b22"><h2>1.22 · Release art: one painting, six looks<small>Decision 1. Each release has one painting (1120 × 440, the 2x tier of the 560 × 220 art band). Each theme restyles it with a grade and its kit\'s motif layer; the frame comes from the kit. Quiet keeps the grade only; Plain shows no art. These are the four releases of the first popup: 1.22.0, 1.21.0, 1.20.0, 1.19.0.</small></h2>';
    ["welcome", "whatnext", "evercold", "answers"].forEach(function (rel) {
      var R = REL22[rel];
      h += "<h3>" + R.v + " · " + R.name + " · " + ART22[rel][0] + "</h3>";
      h += '<div class="row" style="flex-wrap:nowrap"><figure style="width:448px;flex:none"><img src="' + V8 + "art/" + rel + '-base.png" width="448" height="176" style="border-radius:4px;display:block">' + cap22("The painting", ART22[rel][1]) + "</figure>";
      h += '<div style="display:grid;grid-template-columns:repeat(3,212px);gap:6px 12px">';
      TH22.forEach(function (T) {
        h += '<figure style="width:212px"><img src="' + V8 + "art/themed/" + rel + "-" + T.k + '.png" width="212" height="83" style="border-radius:3px;display:block">' + cap22(T.name, ARTNOTE22[T.k]) + "</figure>";
      });
      h += "</div></div>";
    });
    h += '<div class="row"><div class="spec" style="width:1360px"><table><tr><th>Theme</th><th>Grade (lift · gamma · gain · saturation · split tone)</th><th>Motif layer (one sprite set per kit, shared by every release)</th></tr>' +
      "<tr><td>Medallion</td><td>none: the reference</td><td>none; the brass frame and corner marks carry it</td></tr>" +
      "<tr><td>Classic</td><td>lift .01 · gamma 1.04 · saturation .82</td><td>none</td></tr>" +
      "<tr><td>Ishgard Glass</td><td>lift .07–.10 · gamma .90 · saturation .80 · shadows #2C3A64 .25 · highlights #EEF3FF .20</td><td>rime feathers from the two upper corners and the lower left, at .42</td></tr>" +
      "<tr><td>Aether Crystal</td><td>gain .95/1.01/1.04 · saturation .95 · shadows #0E2A40 .20 · highlights #BFF0FF .18</td><td>up to three small shards in the sky's corners, lit on their upper-left facets; none within 5 moon radii of the moon</td></tr>" +
      "<tr><td>Orrery</td><td>gain 1.04/.98/.97 · shadows #2B1F3A .40 · highlights #F5C47C .22</td><td>the one motif that centres on the moon: whole hairline orbits at 28°, kept inside the band and at least 1.5 r from the disc, a graduated hairline ring at 1.6 r, one bead; all at .20</td></tr>" +
      "<tr><td>Sumi to Kinpaku</td><td>gamma 1.06 · saturation .40 · shadows #16100F .45 · highlights #F3E9DB .25</td><td>sunago: fine gold dust thinning out across the top quarter of each upper corner, and 7 cut leaf pieces per corner; washi fibre at 4 %</td></tr>" +
      "</table><p style=\"margin:8px 0 0\">The recipe runs once when the popup opens, off the main thread: decode the base, grade it, composite the kit's motif sprites, upload one 1120 × 440 texture (1.9 MB) and release it on close. It stays inside the 12 MB texture budget. Ships one base image per release (about 180 KB as JPEG, or 600 KB as PNG) and six small motif sprite sheets.</p></div></div>";
    return h + "</div>";
  }

  // ---------- Decision 1, Option B: one release with a distinct treatment per theme ----------
  var OPTB22 = {
    "medallion": "visibly oil: an amber varnish, brush strokes lit from the upper left, faint craquelure in the thick paint, a deeper vignette, a slim gilt slip (the popup drops its art keyline here)",
    "classic": "the painting as painted",
    "ishgard-glass": "a stained-glass window whose lead follows the drawing: large sky pieces, clouds cut along their edges, snow as contour strips, grisaille figures and spires, a white glass crescent, an amber lantern piece",
    "aether-crystal": "cut moonstone over the sky and the far range only: facets shaded as on domed gems lit from the upper left, a faint blue adularescent sheen; the ground and figures stay clear",
    "astrologian-orrery": "an engraved plate: silver ground and lapis enamel sky, dark line following each contour, the clouds cut in line, the backlit city densely cross-hatched; brass only as inlay (crescent, limb, rete)",
    "sumi-to-kinpaku": "sumi-e on toned washi: ink washes by depth, bare-paper snow, one tapered dry-brush stroke for the ridge, genji-gumo gold bands with gold-dust edges, kirigane in the bands and corners, a seal carved with a crescent"
  };
  function board_optb22() {
    var h = '<div class="board b15 b22"><h2>Decision 1 · Option A or Option B<small>1.20.0 Before Evercold in both options. The owner chose Option B for every release. Option A (the approved fallback): one painting, restyled per theme by a grade and a motif layer. Option B: a richer painting, rendered in a distinct art treatment per theme. Each row is one theme: A on the left, B on the right, at the popup\'s art size.</small></h2>';
    h += '<div class="row" style="flex-wrap:nowrap;align-items:flex-start"><figure style="width:560px;flex:none"><img src="' + V8 + 'art/optionb/evercold-b-base.png" width="560" height="220" style="border-radius:4px;display:block">' +
      cap22("Option B's painting", "dawn over Coerthas: Ishgard on its bluff (the Vault's paired spires), backlit by the sun still under the horizon, no lit windows; an adventurer with a lantern on a bail and her chocobo, rim-lit, with short soft shadows from their feet; the lantern warms the chocobo's chest and the snow. One natural light and one warm practical light.") + "</figure>";
    h += '<div class="spec" style="width:760px"><table><tr><th></th><th>Option A · one painting, restyled</th><th>Option B · one painting, six treatments</th></tr>' +
      "<tr><td>Art per release</td><td>1 painting (about a day with supervision) plus its moon's place; the six restyles are automatic</td><td>1 richer painting (2 to 3 days) plus its region masks; the six treatments are code, but each release needs a check of all six, since a treatment can break a new composition (a band across a figure, a came line through a face)</td></tr>" +
      "<tr><td>Ships</td><td>1 base image per release (about 180 KB JPEG) and six motif sprite sheets once</td><td>six pre-rendered JPEGs per release: about 450 KB for this one (budget 600 KB); about 4 MB for all nine releases</td></tr>" +
      "<tr><td>Texture</td><td>one 1120 × 440 texture while the popup is open (1.9 MB), released on close</td><td>the same one texture (1.9 MB); pre-rendered, so it only decodes</td></tr>" +
      "<tr><td>Each theme</td><td>looks like its palette; Medallion and Classic are almost the same</td><td>looks like its own craft: oil, glass, crystal, engraving, ink and gold</td></tr>" +
      "<tr><td>Risk</td><td>low: a grade cannot break a picture</td><td>higher: treatments must keep the subject legible at 560 × 220 and in Quiet; the owner sees six pictures per release to approve</td></tr></table>" +
      '<p style="margin:8px 0 0">Every treatment keeps the painting\'s light: none adds a light of its own, and the lantern stays the one warm practical light. Quiet would show the Classic painting graded to the palette; Plain shows no art in either option.</p></div></div>';
    h += '<div style="display:grid;grid-template-columns:150px 352px 448px 1fr;gap:12px 16px;align-items:center;margin-top:6px">';
    h += '<div></div><div class="hd" style="font:600 11.5px/1.2 Barlow Condensed,sans-serif;letter-spacing:.12em;text-transform:uppercase;color:#D9BE82">Option A</div><div style="font:600 11.5px/1.2 Barlow Condensed,sans-serif;letter-spacing:.12em;text-transform:uppercase;color:#D9BE82">Option B</div><div style="font:600 11.5px/1.2 Barlow Condensed,sans-serif;letter-spacing:.12em;text-transform:uppercase;color:#D9BE82">Option B treatment</div>';
    TH22.forEach(function (T) {
      h += '<div><b style="display:block;color:#F4F1E8;font:400 14px/1.2 Marcellus,Georgia,serif">' + T.name + '</b><span style="font-size:11px;color:#A9B2CC">' + T.paln + " · " + T.kitn + "</span></div>";
      h += '<img src="' + V8 + "art/themed/evercold-" + T.k + '.png" width="352" height="138" style="border-radius:3px;display:block">';
      h += '<img src="' + V8 + "art/optionb/evercold-b-" + T.k + '.png" width="448" height="176" style="border-radius:3px;display:block">';
      h += '<div style="font-size:11.5px;color:#A9B2CC;line-height:1.45">' + OPTB22[T.k] + "</div>";
    });
    return h + "</div></div>";
  }

  // ---------- W3, U1, M3: Settings › About ----------
  var HIST22 = [["1.22.0", "Welcome home", "5 Oct", "welcome"], ["1.21.0", "What next, for every character", "4 Oct", "whatnext"], ["1.20.0", "Before Evercold", "4 Oct", "evercold"],
    ["1.19.0", "Right answers", "4 Oct", "answers"], ["1.18.0", "Runs you can trust", "3 Oct", "banners/grand-company"], ["1.17.0", "Mix and match", "3 Oct", "banners/chronicles"]];
  function histImg22(src, th) { return src.indexOf("banners/") === 0 ? "../../../../Tsukimichi/assets/ui/" + src + "@2x.png" : V8 + "art/themed/" + src + "-" + th + ".png"; }
  function settings22(th, o) {
    o = o || {};
    var T = th22(th);
    var h = '<div class="mk full v7 ' + (T.pal !== "night" ? T.pal + " " : "") + 'sw22"><div class="nav">' + ["General", "Themes", "Look", "In game", "Alerts", "Overlay", "Spoilers", "Privacy & trust", "About"].map(function (n) { return "<span" + (n === "About" ? ' class="on"' : "") + ">" + n + "</span>"; }).join("") + '</div><div class="spg">';
    h += '<div class="sh">About</div><div class="stl"><b>Tsukimichi 1.22.0</b><span class="s">· game data 2026.09.15 · Dalamud API 15</span><span class="r">' + pill18("quiet", "Copy diagnostics") + "</span></div>";
    h += '<div class="sh">Updates</div>';
    h += o.ready ? '<div class="stl"><span class="dt" style="width:7px;height:7px;border-radius:4px;background:#6F8FD0"></span><b>Tsukimichi 1.23.0 is ready</b><span class="s">· Dalamud has it</span><span class="r">' + pill18("full", "What's in it") + pill18("full", "Update") + "</span></div>"
      : '<div class="stl"><b>Up to date</b><span class="s">· Dalamud last looked 6 min ago</span></div>';
    h += '<div class="sr"><span class="tg on"></span><div class="tx"><b>Tell me when a new version is ready</b><span>Asks Dalamud, which already checks your plugin repositories every few minutes. Tsukimichi itself never goes online for this.</span></div></div>';
    h += '<div class="sr"><span class="tg"></span><div class="tx"><b>Also say it in chat</b><span>One line in your echo channel when a version is ready. Off: the status bar and the dot on the moon icon say it.</span></div></div>';
    h += '<div class="sr"><span class="tg on"></span><div class="tx"><b>Show what\'s new after an update</b><span>Once per update, at the first quiet moment.</span></div></div>';
    h += '<div class="sh">What\'s new</div><div class="wl">';
    HIST22.forEach(function (r, i) {
      h += '<div class="wr' + (i === 2 ? " hv" : "") + '"><img src="' + histImg22(r[3], th) + '" alt=""><span class="n">' + r[1] + "<i>" + r[0] + "</i></span>" + (i === 0 ? '<span class="chp">Installed</span>' : "") + '<span class="d">' + r[2] + '</span><span class="cv">›</span></div>';
    });
    h += '<div class="wr"><span class="n" style="padding-left:4px;color:var(--mist)">3 earlier releases, back to 1.14.0 ›</span></div></div>';
    h += '<div class="sh">Umbra</div>';
    h += '<div class="stl"><b>Umbra is running</b><span class="s">· its toolbar is at the top. The moon icon, the Todo overlay and Needs you keep clear of it.</span></div>';
    h += '<div class="stl"><b>Tsukimichi for Umbra</b><span class="s">· not added</span><span class="r"><span style="font-size:12px;color:var(--silver);border-bottom:1px dotted var(--vline)">How to add it ▾</span></span></div>';
    h += '<div class="how">Umbra loads add-ons it calls custom plugins. Once:<ol><li>In Umbra\'s settings, open <b>Plugins</b> and turn on <b>custom plugins</b> (Umbra asks you to agree first).</li><li>Add the repository <code>xenofei/Tsukimichi.Umbra</code>. Umbra installs it and keeps it up to date.</li><li>Add the Tsukimichi widgets to your toolbar from Umbra\'s widget list.</li></ol></div>';
    h += '<div class="stl"><b>Follow Umbra</b><span class="s">· a palette that matches your Umbra colours, in Settings › Themes › Palette, beside Follow Dalamud. Not in use.</span><span class="r"><span style="font-size:12px;color:var(--silver);border-bottom:1px dotted var(--vline)">Themes ›</span></span></div>';
    return h + "</div></div>";
  }
  function board_about22() {
    var h = '<div class="board b15 b22"><h2>1.22 · Settings › About<small>W3: every release\'s popup, newest first, backfilled to 1.14.0 (row hovered: 1.20.0). U1: the update switch, on by default. M3: the Umbra status row and how to add the add-on. Medallion on Night, Full.</small></h2><div class="row">';
    h += "<figure>" + settings22("medallion") + cap22("About, up to date", "a row opens the popup on that release; ‹ › then walks the whole history") + "</figure>";
    h += '<figure style="gap:10px">' + scene22(popup22("medallion", { lv: "full", page: "evercold", idx: 3, total: 9 }), "scene-night.jpg", 14) + cap22("Opened from the list", "the same popup, 3 of 9; no “You were on” caption, since it was not an update") + "</figure>";
    return h + "</div></div>";
  }

  // ---------- U1: the update note ----------
  function quick22(th, lv, o) {
    o = o || {};
    var T = th22(th);
    var h = '<div class="mk ' + lv + " v7 " + (T.pal !== "night" ? T.pal + " " : "") + 'qc22"><div class="qh"><b>Tonight</b><span>Kiri · WHM 100</span></div>';
    h += '<div class="un">' + rowGlyph(lv, "in-journal", 18) + '<div><div class="l">Up next</div><div class="n">The Long Road to Xak Tural</div><div class="s">Step 3: Speak with Erenville.</div><div class="s">Shaaloani</div></div></div>';
    h += '<div class="ql"><span class="g">' + rowGlyph(lv, "ready", 14) + '</span><span>12 quests are Ready</span><span class="s">on WHM</span></div>';
    h += '<div class="ql"><span class="g">' + ic22("book", 12, "var(--mist)") + '</span><span>Journal 27/30</span><span class="s">· 3 slots left</span></div>';
    h += '<div class="ql"><span class="g">' + ic22("clock", 12, "var(--mist)") + '</span><span>The Rising ends in 2 days</span></div>';
    if (o.update) h += '<div class="ql up"><span class="dt" style="background:#6F8FD0"></span><span>Tsukimichi 1.23.0 is ready</span><span class="lk">Update</span></div>';
    if (o.needs) h += '<div class="ql up"><span class="dt" style="background:#D08654"></span><span>Travel is stuck near Camp Dragonhead</span><span class="lk">Show</span></div>';
    h += '<div class="ft">' + (o.locked ? "Locked in place · right-click to unlock" : "Click to open · right-click for options") + "</div>";
    return h + "</div>";
  }
  function board_upd22() {
    var h = '<div class="board b15 b22"><h2>1.22 · A new version is ready<small>U1. Tsukimichi asks Dalamud at login and every 3 hours (no network of its own). When a newer version waits: a quiet note in the status bar and a dot on the moon icon. Update opens Dalamud\'s installer on it; Dalamud installs.</small></h2>';
    h += "<h3>The status bar note</h3><div class=\"row\">";
    ["full", "quiet", "plain"].forEach(function (lv) {
      h += "<figure>" + '<div class="mk ' + lv + ' v7 sb22"><span>Journal 27/30</span><span>Data 2026.09.15</span><span class="up"><span class="dt"></span>Tsukimichi 1.23.0 is ready<span class="b">Update</span><span class="x" title="Later">' + ic22("x", 9) + "</span></span></div>" + cap22(lv.charAt(0).toUpperCase() + lv.slice(1), lv === "full" ? "Cool (Tide) dot and tint: news, not a call to act. × means Later: hidden until the next version" : lv === "quiet" ? "no tint: a hairline pill on the flat bar" : "the ledger: no pill, a square Update; the dot stays, so meaning is never colour alone") + "</figure>";
    });
    h += "</div><div class=\"row\">";
    h += '<figure><div class="mk full v7 tip22"><b>What\'s in 1.23.0</b> <span style="color:var(--mist)">(example notes)</span><ul><li>Example point one, in plain words.</li><li>Example point two.</li><li>Example point three.</li></ul><div class="f">From Dalamud\'s copy of the release. Dalamud installs it; Tsukimichi never downloads itself.</div></div>' + cap22("Hovering the note", "the new version's plain notes, when Dalamud has them") + "</figure>";
    h += '<figure>' + scene22('<div style="position:relative;width:520px;height:250px"><div style="position:absolute;left:18px;top:14px">' + mi22("medallion", 32, { dot: "update", t: 1.0, hover: 1 }) + '</div><div style="position:absolute;left:120px;top:24px">' + quick22("medallion", "full", { update: true }) + "</div></div>", "scene-night.jpg", 0) + cap22("The dot on the moon icon", "Tide, upper right; the quick card's last line says it in words, with Update") + "</figure>";
    h += "</div><div class=\"row\">";
    h += '<figure><div class="dl22"><div class="t">Plugin Installer<span class="x">×</span></div><div class="b"><div class="c"><span>All Plugins</span><span>Installed Plugins</span><span class="on">Can be updated</span><span>Plugin Changelogs</span><span>Settings</span></div><div class="m"><div class="srch">Tsukimichi</div><div class="it"><img src="../../../../assets/icon.png" alt=""><div><div class="n">Tsukimichi</div><div class="v">1.22.0 → 1.23.0 · xenofei</div></div><span class="bt">Update</span></div><div class="cl"><b>Changelog</b><ul><li>Example point one, in plain words.</li><li>Example point two.</li></ul></div></div></div></div>' +
      cap22("Update opens Dalamud's installer", "on “Can be updated”, searched for Tsukimichi (OpenPluginInstallerTo). A sketch in Dalamud's own style: Dalamud draws this, not Tsukimichi") + "</figure>";
    h += '<figure><div class="spec" style="width:560px"><table><tr><th>Step</th><th>What happens</th></tr>' +
      "<tr><td>Check</td><td>CheckForUpdateAsync() at login and every 3 hours, while Settings › About › “Tell me when a new version is ready” is on (default). It reads the repository data Dalamud already refreshes; no request of Tsukimichi's own. The no-network test is unchanged.</td></tr>" +
      "<tr><td>Ready</td><td>The note and the dot appear. Nothing pops up and nothing prints in chat (decision 6; “Also say it in chat” is an opt-in).</td></tr>" +
      "<tr><td>Update</td><td>OpenPluginInstallerTo(UpdateablePlugins, \"Tsukimichi\"). Dalamud installs and reloads the plugin.</td></tr>" +
      "<tr><td>Later (×)</td><td>Hides the note and the dot until a newer version than this one appears. Settings › About still shows “1.23.0 is ready” with Update.</td></tr>" +
      "<tr><td>After</td><td>Once the new version runs, the What's new popup shows its notes (W1).</td></tr>" +
      "<tr><td>Notes</td><td>The plain points of whats_new.json are written into the manifest's changelog for each release, so Dalamud's own installer shows the same plain notes.</td></tr></table></div>" + cap22("The flow", "") + "</figure>";
    return h + "</div></div>";
  }

  // ---------- H1: the moon icon ----------
  function menu22(th, lv, locked) {
    var T = th22(th);
    return '<div class="mk ' + lv + " v7 " + (T.pal !== "night" ? T.pal + " " : "") + 'mn22"><div class="mi hv">' + (locked ? "Unlock" : "Lock in place") + '</div><div class="mi">Hide icon<span class="k">/tsuki icon</span></div><div class="sep"></div><div class="mi">Tonight</div><div class="mi">Settings</div></div>';
  }
  function frame22(inner, w, h, img, pos) {
    return '<div class="sc22 sharp" style="width:' + w + "px;height:" + h + 'px"><div class="bg" style="background-image:url(' + SCN + (img || "scene-night.jpg") + ");background-size:" + (pos && pos.size || "1700px auto") + ";background-position:" + (pos && pos.p || "0% 0%") + ';filter:saturate(.9) brightness(.8)"></div><div class="fg" style="position:relative;width:100%;height:100%">' + inner + "</div></div>";
  }
  function at22(x, y, inner) { return '<div style="position:absolute;left:' + x + "px;top:" + y + 'px">' + inner + "</div>"; }
  function board_icon22() {
    var h = '<div class="board b15 b22"><h2>1.22 · The moon icon<small>H1. 40 px on screen (32 in this mock\'s scale), drawn by the theme. It floats over the game where you put it; click opens or closes Tsukimichi, right-click opens its menu. Medallion, Night, Full; the scene is the game behind it.</small></h2><div class="row">';
    // icon centre offset inside its svg: the svg is px*144/64 wide, the face starts at 40/144 of it
    function ic(x, y, o) { var px = 32, off = px * 40 / 64; return at22(x - off, y - off, mi22("medallion", px, o)); }
    h += "<figure>" + frame22(ic(80, 60, { t: 0.9 }), 200, 150) + cap22("At rest", "kit rim, a crescent lit from the upper left, a straight-down shadow, three gold motes") + "</figure>";
    h += "<figure>" + frame22(ic(60, 60, { t: 0.9, hover: 1 }) + at22(112, 30, quick22("medallion", "full")), 400, 260) + cap22("Hover", "rises 2 px with a cool glow (moonlight, never the warm Ready halo); the quick card opens beside it after 0.25 s, away from the screen edge") + "</figure>";
    h += "<figure>" + frame22(ic(60, 60, { t: 0.9, hover: 1 }) + at22(98, 72, menu22("medallion", "full")), 300, 230) + cap22("Right-click", "Lock in place, Hide icon, Tonight, Settings") + "</figure>";
    h += "</div><div class=\"row\">";
    h += "<figure>" + frame22(ic(60, 60, { t: 2.1, hover: 1 }) + at22(98, 47, '<div class="hn22" style="position:static">Locked in place · right-click to unlock</div>'), 330, 120) + cap22("Locked", "looks the same at rest; a drag does nothing and says why. No padlock on the icon: a closed padlock means Blocked") + "</figure>";
    h += "<figure>" + frame22(ic(60, 56, { t: 1.4 }) + at22(98, 44, '<div class="hn22" style="position:static">Right-click for options</div>'), 300, 112) + cap22("First run", "once, after install or update (decision 5): 8 s or until you click") + "</figure>";
    h += "<figure>" + frame22(at22(20, 34, '<div class="mk full v7 ts22">Moon icon hidden<span class="u">Undo</span><span class="h">/tsuki icon shows it again</span></div>'), 400, 112) + cap22("Hide", "the 8 s Undo toast; Settings › In game and /tsuki icon bring it back") + "</figure>";
    h += "</div><div class=\"row\">";
    var dots = "";
    [["", "No dot"], ["update", "Update ready"], ["needs", "Needs you"]].forEach(function (d, i) {
      dots += at22(18 + i * 150, 18, mi22("medallion", 64, { t: 0.9, dot: d[0] || undefined })) + at22(18 + i * 150 + 26, 168, '<span class="lab22" style="position:static">' + d[1] + "</span>");
    });
    h += "<figure>" + frame22(dots, 470, 200) + cap22("The dots, at 2x", "upper right, 8 px with a Night ring. Needs you (copper) wins over Update ready (Tide); the quick card names both in words") + "</figure>";
    h += "<figure>" + frame22(ic(60, 60, { t: 0.9, dot: "needs", hover: 1 }) + at22(112, 26, quick22("medallion", "full", { needs: true })), 400, 270) + cap22("Needs you", "the 1.18 Needs you line, with Show") + "</figure>";
    var lv3 = "";
    ["full", "quiet", "plain"].forEach(function (lv, i) { lv3 += at22(14 + i * 120, 16, mi22("medallion", 48, { lv: lv, t: 0.9 })) + at22(14 + i * 120 + 30, 132, '<span class="lab22" style="position:static">' + lv.charAt(0).toUpperCase() + lv.slice(1) + "</span>"); });
    h += "<figure>" + frame22(lv3, 380, 170) + cap22("Decoration, at 1.5x", "Full: kit rim and particles. Quiet: the face with a hairline, no particles. Plain: the flat glyph, no shadow or glow") + "</figure>";
    h += "</div>";
    h += '<div class="row"><div class="spec" style="width:1360px"><table><tr><th>Behaviour</th><th>Rule</th></tr>' +
      "<tr><td>Size</td><td>Small 32 · Medium 40 (default) · Large 48 logical px, times UI scale. Text size does not change it; the quick card follows Text size.</td></tr>" +
      "<tr><td>Move</td><td>Drag with the left button (a 4 px dead zone, so a click is never a drag). While dragging it lifts as on hover; on release it settles. It snaps to stay 8 px inside the screen and clear of Umbra's toolbar (M3). The place is saved per screen size, so a different resolution never puts it off screen.</td></tr>" +
      "<tr><td>Lock</td><td>Lock in place / Unlock from the menu, or Settings › In game › Moon icon. Locked, a drag does nothing and the hint says why.</td></tr>" +
      "<tr><td>Hide</td><td>From the menu (with Undo), Settings or /tsuki icon. Optional: hide in cutscenes, Group Pose and duties (on by default for cutscenes and Group Pose).</td></tr>" +
      "<tr><td>Quick card</td><td>Opens after 0.25 s of hover (none while dragging), on the side with room. Up next, the Ready count, journal room, events ending soon, then Update ready or Needs you. It is a tooltip: it never takes focus and closes when the pointer leaves.</td></tr>" +
      "<tr><td>Click</td><td>Opens or closes Tsukimichi. Right-click: the menu. Keyboard and controller: /tsuki icon toggles it; the main window is unaffected.</td></tr>" +
      "</table></div></div>";
    return h + "</div>";
  }

  // ---------- H2: particles ----------
  function board_fx22() {
    var times = [0, 0.9, 1.8, 2.7];
    var h = '<div class="board b15 b22"><h2>1.22 · Theme particles and hover<small>H2. Four frames 0.9 s apart, icon at 2x on the game\'s night sky. Full only; off under Reduce motion; Quiet and Plain have none. At most three particles at once, never above .55 alpha, never more than 1.4 icon radii out.</small></h2>';
    h += '<div class="st22"><div></div>' + times.map(function (t) { return '<div class="hd">t = ' + t.toFixed(1) + " s</div>"; }).join("") + '<div class="hd">Timing</div>';
    var tm = {
      "medallion": "<b>Gold motes.</b> 3 motes, each 3.6 s: fade in 0.6 s, rise 4.5 u/s (about 3 px/s) with a slight sway, fade out 1.2 s. Staggered 1.2 s. r 1 px, #FFE9BE ≤ .55, halo .12.",
      "classic": "<b>A few stars.</b> 3 round stars at fixed places round the icon, breathing on 7, 9.5 and 12 s periods (the 1.14 twinkle curve), .27–.49. Nothing moves.",
      "ishgard-glass": "<b>Frost glints.</b> Every 6 s a 28° glint runs the lit upper-left quarter of the rim in 1.2 s, with one round sparkle at its head. No 4-point star.",
      "aether-crystal": "<b>Shards.</b> 2 shards on a 16 s orbit round the icon's foot (rx 1.25 R, ry 0.45 R, centred 0.6 R below the centre), so the near arc runs just outside the bottom of the rim. They hide only where the icon covers them; no shard is ever drawn over the face. Each turns every 3.2 s and flashes (additive light, no disc) for about 0.4 s when its face meets the light.",
      "astrologian-orrery": "<b>An orbiting dot.</b> One bead on a 12 s orbit, rx 1.3 R, ry 0.4 R, tilted 28°; the far half passes behind the icon. The orbit hairline at .14.",
      "sumi-to-kinpaku": "<b>Gold-leaf flecks.</b> 2 flecks, 4.5 s each, drifting down past the right side with a flutter; a fleck flashes when it tilts toward the light."
    };
    TH22.forEach(function (T) {
      h += '<div class="lb"><b>' + T.name + "</b><span>" + { "medallion": "brass rim", "classic": "1.11 disc, hairline rim", "ishgard-glass": "lead came rim", "aether-crystal": "silver rim", "astrologian-orrery": "astrolabe rim", "sumi-to-kinpaku": "lacquer and kirikane rim" }[T.k] + "</span></div>";
      times.forEach(function (t) {
        h += '<div class="fr"><div class="bg" style="background-image:url(' + SCN + 'scene-night.jpg);background-position:3% 1%"></div>' + mi22(T.k, 84, { t: t }) + '<span class="t">' + t.toFixed(1) + " s</span></div>";
      });
      h += '<div class="tm">' + tm[T.k] + "</div>";
    });
    // hover sequence, Reduce motion, daylight
    h += '<div class="lb"><b>Hover</b><span>Medallion, in 0.12 s</span></div>';
    [0, 0.33, 0.67, 1].forEach(function (k) { h += '<div class="fr"><div class="bg" style="background-image:url(' + SCN + 'scene-night.jpg);background-position:3% 1%"></div>' + mi22("medallion", 84, { t: 0.9, hover: k }) + '<span class="t">' + (k * 0.12).toFixed(2) + " s</span></div>"; });
    h += '<div class="tm"><b>Hover in</b> over HoverIn 0.12 s, ease-out cubic: rise 2 px, a cool glow (#E2E8F4, .22 at its core) and a softer, longer shadow, as an object lifted toward the light. <b>Out</b> over HoverOut 0.18 s. Particles do not change on hover.</div>';
    h += '<div class="lb"><b>Reduce motion</b><span>and Quiet</span></div>';
    [0, 1].forEach(function (k) { h += '<div class="fr"><div class="bg" style="background-image:url(' + SCN + 'scene-night.jpg);background-position:3% 1%"></div>' + mi22("medallion", 84, { hover: k, fx: false }) + '<span class="t">' + (k ? "hovered" : "at rest") + "</span></div>"; });
    [0, 1].forEach(function (k) { h += '<div class="fr"><div class="bg" style="background-image:url(' + SCN + 'scene-night.jpg);background-position:3% 1%"></div>' + mi22("medallion", 84, { lv: "quiet", hover: k, fx: false }) + '<span class="t">Quiet ' + (k ? "hovered" : "at rest") + "</span></div>"; });
    h += '<div class="tm"><b>Reduce motion:</b> no particles and no rise; the glow appears at once. <b>Quiet:</b> no particles; hover rises 1 px with a lighter glow. <b>Plain:</b> the flat glyph; hover draws a 1.5 px Text ring.</div>';
    h += '<div class="lb"><b>Daylight</b><span>every theme at rest</span></div>';
    h += '<div class="fr" style="grid-column:span 4;width:auto;height:128px"><div class="bg" style="background-image:url(' + SCN + 'scene-day.jpg);background-size:160% auto;background-position:72% 3%;filter:none"></div>' + TH22.map(function (T, i) { return mi22(T.k, 40, { t: 1.8 }).replace('class="mi22"', 'class="mi22" style="left:' + (9 + i * 16.4) + '%"'); }).join("") + '</div><div class="tm">On a bright sky every icon keeps its dark well and its rim; the shadow separates it from the scene.</div>';
    h += "</div></div>";
    return h;
  }

  // ---------- M1: the server info bar ----------
  var PHASE22 = { "medallion": "#F2D27A", "classic": "#F4E9C6", "ishgard-glass": "#E8EEFA", "aether-crystal": "#BDEFFF", "astrologian-orrery": "#F5C47C", "sumi-to-kinpaku": "#F4DA92" };
  function tipLines22() {
    return '<div class="h">Tsukimichi</div><div>Up next: The Long Road to Xak Tural</div><div class="s">Step 3: Speak with Erenville. Shaaloani</div><div>12 quests are Ready on WHM · 3 can start here</div><div>Journal 27/30 · 3 slots left</div><div>The Rising ends in 2 days</div><div class="s">Click: open Tsukimichi · Right-click: Tonight</div>';
  }
  function umbraBar22(cls, widgets, w, extra) { return '<div class="ub22 ' + (cls || "") + '" style="width:' + w + 'px;' + (extra || "") + '"><div class="tb">' + widgets + "</div></div>"; }
  function uw22(inner, cls) { return '<span class="w dec ' + (cls || "") + '">' + inner + "</span>"; }
  function board_dtr22() {
    var h = '<div class="board b15 b22"><h2>1.22 · In the server info bar<small>M1. One Tsukimichi entry, “◐ 12 Ready” (the plan wrote ◑; ◐ is lit on the left, as every Tsukimichi moon), the moon in the theme\'s phase colour. Click opens Tsukimichi, right-click opens Tonight. The game and Umbra draw its hover as text lines (Dalamud gives an entry a text tooltip, not a window), so it carries the quick card\'s content in words.</small></h2>';
    h += "<h3>The game's own bar</h3><div class=\"row\">";
    var bar = '<div class="dtr22"><span class="e hv"><span class="mo" style="color:#F2D27A">◐</span> 12 Ready</span><span class="e">Gil 1,204,331</span><span class="e">ET 21:42</span><span class="e">LT 9:15 PM</span></div>';
    h += "<figure>" + frame22(at22(830, 6, bar) + at22(850, 34, '<div class="gtt22">' + tipLines22() + "</div>"), 1280, 230, "scene-day.jpg", { size: "1400px auto", p: "60% 0%" }) + cap22("Hovered", "top right of the screen, left of the clocks; the tooltip is the game's, its first line in the gold the UIColor sheet offers nearest the theme") + "</figure>";
    h += "</div><div class=\"row\">";
    h += '<figure><div style="display:flex;gap:10px;padding:10px;border-radius:8px;background:url(' + SCN + 'scene-day.jpg) 50% 0%/1400px auto">' + TH22.map(function (T) { return '<div class="dtr22"><span class="e"><span class="mo" style="color:' + PHASE22[T.k] + '">◐</span> 12 Ready</span></div>'; }).join("") + "</div>" + cap22("The moon in each theme", "Medallion, Classic, Ishgard Glass, Aether Crystal, Orrery, Sumi; the text stays the game's white") + "</figure>";
    h += "</div><h3>Inside Umbra's toolbar (its Server Info Bar widget, Umbra's default colours)</h3><div class=\"row\">";
    var ub = umbraBar22("", uw22('<span class="ic">' + ic22("menu", 15) + "</span>") + uw22('<span class="bd">Tuliyollal</span>') + '<span class="sp"></span>' +
      uw22('<span class="bd"><span style="color:#F2D27A">◐</span> 12 Ready</span>', "hv") + uw22('<span class="bd">Gil 1,204,331</span>') + uw22('<span class="ic">' + ic22("clock", 14) + '</span><span class="bd">21:42</span>'), 1280);
    h += "<figure>" + frame22(at22(0, 0, ub) + at22(842, 40, '<div class="ub22"><div class="utip">' + tipLines22().replace('class="h"', 'class="h"') + "</div></div>"), 1280, 240, "scene-day.jpg", { size: "1400px auto", p: "60% 0%" }) + cap22("Hovered in Umbra", "Umbra decorates the entry like its other widgets and draws the same lines in its own tooltip. Click and right-click pass through as in the game's bar") + "</figure>";
    h += "</div>";
    h += '<div class="row"><div class="spec" style="width:1280px"><table><tr><th>Rule</th><th></th></tr>' +
      "<tr><td>One entry</td><td>The 1.x Nearby entry (“☾ 3”, quests startable in this zone) becomes this entry. Settings › In game › Server info bar › “The entry counts”: <b>Ready quests</b> (default) or <b>Quests in this zone</b>. Players who had the Nearby entry on keep Quests in this zone. The tooltip always gives both numbers.</td></tr>" +
      "<tr><td>Default</td><td>On when Umbra is installed and Tsukimichi for Umbra is not; otherwise as the player set it (off on a fresh install).</td></tr>" +
      "<tr><td>Words</td><td>“◐ 12 Ready”. At zero the entry hides; with “Show at zero” on (today's setting, kept) it reads “◐ Nothing Ready”.</td></tr>" +
      "<tr><td>Glyph</td><td>The moon is a text character, so the game font must have it. ◐ (lit on the left, like every Tsukimichi moon) is to be checked in game; the shipped entry draws ☾, the fallback.</td></tr>" +
      "<tr><td>Colour</td><td>An SeString foreground from the UIColor sheet, the row nearest the theme's phase colour. Only the moon is coloured.</td></tr></table></div></div>";
    return h + "</div>";
  }

  // ---------- A1/A2: Tsukimichi for Umbra ----------
  function umbPopup22(theme, o) {
    o = o || {};
    if (o.missing) {
      return '<div class="pop"><div class="hdr">Tsukimichi</div><div class="grp"><div class="btn"><span class="bi">' + mi22(theme, 14, { fx: false }).replace('class="mi22"', 'class="mi22" style="margin:-9px"') + '</span><span class="bt">Tsukimichi isn\'t running</span></div>' +
        '<div class="btn" style="font-size:11.5px;white-space:normal;line-height:1.4;padding-top:0"><span class="bi"></span><span>Turn it on in Dalamud\'s plugin installer, or install it there.</span></div>' +
        '<div class="sep2"></div><div class="btn"><span class="bi">' + ic22("open", 13) + '</span><span class="bt">Open the plugin installer</span></div></div></div>';
    }
    var h = '<div class="pop"><div class="hdr">Tsukimichi · Kiri · WHM 100</div>';
    h += '<div class="grp"><div class="gh"><span class="t">Up next</span><span class="ln"></span></div>' +
      '<div class="btn' + (o.hv ? " hv" : "") + '"><span class="bi"><img class="gi" src="1.15/icons/071201.png" width="20" height="20" alt=""></span><span class="bt">The Long Road to Xak Tural</span><span class="at">Go</span></div>' +
      '<div class="btn" style="padding-top:0"><span class="bi"></span><span class="at" style="padding-left:0">Step 3: Speak with Erenville. · Shaaloani</span></div></div>';
    h += '<div class="grp"><div class="gh"><span class="t">Ready tonight · 12</span><span class="ln"></span></div>' +
      [["071221", "Knowing the Pelupelu", "Urqopacha"], ["071221", "A Leaking Workpot", "Kozama'uka"], ["071221", "Caught in the Act", "The Pillars"]].map(function (r) { return '<div class="btn"><span class="bi"><img src="1.15/icons/' + r[0] + '.png" width="18" height="18" alt=""></span><span class="bt">' + r[1] + '</span><span class="at">' + r[2] + "</span></div>"; }).join("") +
      '<div class="btn"><span class="bi"></span><span class="bt" style="color:var(--pmm)">9 more</span></div></div>';
    h += '<div class="grp"><div class="gh"><span class="t">Journal</span><span class="ln"></span></div><div class="btn"><span class="bi">' + ic22("book", 13) + '</span><span class="bt">27/30 · 3 slots left</span><span class="at">Make room</span></div></div>';
    h += '<div class="grp"><div class="gh"><span class="t">Ending soon</span><span class="ln"></span></div><div class="btn"><span class="bi">' + ic22("clock", 13) + '</span><span class="bt">The Rising</span><span class="at">2 days</span></div></div>';
    h += '<div class="sep2"></div><div class="btn"><span class="bi">' + ic22("moon", 13) + '</span><span class="bt">Open Tsukimichi</span></div><div class="btn"><span class="bi">' + ic22("route", 13) + '</span><span class="bt">Route</span></div><div class="btn"><span class="bi">' + ic22("gear", 13) + '</span><span class="bt">Settings</span></div>';
    return h + "</div>";
  }
  function umbBar22(cls, theme, w) {
    var moon = mi22(theme, 18, { fx: false }).replace('class="mi22"', 'class="mi22" style="margin:-11px"');
    return umbraBar22(cls, uw22('<span class="ic">' + ic22("menu", 15) + "</span>") + uw22('<span class="bd">Tuliyollal</span>') + '<span class="sp"></span>' +
      uw22('<span class="ic">' + moon + '</span><span class="bd">12 Ready</span>', "hv") +
      uw22('<span class="ic"><img class="gi" src="1.15/icons/071201.png" alt=""></span><span class="bd">The Long Road to Xak Tural</span>') +
      uw22('<span class="bd">Journal 27/30</span>') +
      uw22('<span class="ml"><span class="a">Dawntrail</span><span class="b2">91 to the latest story</span></span>') +
      uw22('<span class="ic">' + ic22("clock", 14) + '</span><span class="ml"><span class="a">Next reset</span><span class="b2">daily in 3 h 12 m</span></span>') +
      uw22('<span class="bd">21:42</span>'), w);
  }
  function board_umb22() {
    var h = '<div class="board b15 b22"><h2>Tsukimichi for Umbra 1.0<small>A1 and A2. Native Umbra widgets: Umbra draws them with its own controls and colours, so they look like Umbra in every Umbra theme. Only the moon is Tsukimichi\'s (the current theme\'s icon, from IPC). Top: Umbra\'s default colours. Bottom: its YoRHa Light profile.</small></h2>';
    [["", "Umbra (built-in)"], ["yorha", "YoRHa Light (built-in)"]].forEach(function (u, i) {
      h += "<h3>" + u[1] + "</h3><div class=\"row\">";
      var inner = at22(0, 0, umbBar22(u[0], "medallion", 1300)) + at22(560, 33, '<div class="ub22 ' + u[0] + '">' + umbPopup22("medallion", { hv: i === 0 }) + "</div>");
      if (i === 1) inner += at22(990, 60, '<div class="ub22 ' + u[0] + '" style="width:300px"><div class="tb flo" style="justify-content:center">' + uw22('<span class="ic">' + mi22("medallion", 18, { fx: false }).replace('class="mi22"', 'class="mi22" style="margin:-11px"') + '</span><span class="bd">Tsukimichi</span>', "hv") + '</div><div class="pop" style="margin:4px auto 0;border-top-width:1px;border-radius:7px">' + umbPopup22("medallion", { missing: true }).replace('<div class="pop">', "").replace(/<\/div>$/, "") + "</div></div>");
      h += "<figure>" + frame22(inner, 1300, 520, "scene-day.jpg", { size: "1400px auto", p: "50% 0%" }) + cap22(i === 0 ? "The widget open" : "The same, on a light Umbra profile", i === 0 ? "bar: moon + Ready count; then Up next (text), Journal, Story meter (two lines) and Next reset. Popup: Up next with Go, Ready tonight, journal room, ending soon, Open, Route, Settings" : "right: the bar with no count and its popup when Tsukimichi isn't running, one reason per case (the other case reads “needs Tsukimichi 1.22 or later”)") + "</figure>";
      h += "</div>";
    });
    h += '<div class="row"><div class="spec" style="width:1300px"><table><tr><th>Piece</th><th>Rule</th></tr>' +
      "<tr><td>Bar widget</td><td>Umbra's standard widget: icon (the theme's moon, or none), text “12 Ready”, decorated or not, Umbra's text colour; the click opens the popup, as Umbra's own popup widgets do.</td></tr>" +
      "<tr><td>Popup</td><td>Umbra's MenuPopup groups (12 px muted header and rule) and buttons (13 px text, 11 px alt text, 22 px icons). Its hover colour, borders and gradient are Umbra's, from the player's Umbra colour profile.</td></tr>" +
      "<tr><td>Go</td><td>Opens Tsukimichi on Up next with its travel pill focused. It never starts travel or a run by itself (M2: anything that starts a run needs a click inside Tsukimichi).</td></tr>" +
      "<tr><td>Ready rows</td><td>The first 3 by Up next's order, then “9 more”, which opens Tonight. A row opens that quest in Tsukimichi.</td></tr>" +
      "<tr><td>Small widgets</td><td>Up next (text), Journal 27/30, Story meter (two lines: expansion over “91 to the latest story”; no bar and no percentage), Next reset (two lines). Each has Umbra's options: icon, text, colour, click action (open Tsukimichi, Tonight, the popup, nothing).</td></tr>" +
      "<tr><td>Missing</td><td>One reason per case, in Umbra's text colour: “Tsukimichi isn't running” or “This widget needs Tsukimichi 1.22 or later”, with Open the plugin installer. The bar reads “Tsukimichi” with no count.</td></tr>" +
      "<tr><td>Spoilers</td><td>Names come through IPC already shielded (1.20 N6), so the add-on never sees a hidden name.</td></tr></table></div></div>";
    return h + "</div>";
  }

  // ---------- M3: clear of Umbra ----------
  function todo22() { return '<div class="td22"><div class="h">Todo · Tonight</div><div class="r">' + rowGlyph("full", "in-journal", 14) + 'The Long Road to Xak Tural<span class="s">step 3</span></div><div class="r">' + rowGlyph("full", "ready", 14) + 'Knowing the Pelupelu<span class="s">Urqopacha</span></div><div class="r">' + rowGlyph("full", "ready", 14) + 'A Leaking Workpot<span class="s">Kozama\'uka</span></div></div>'; }
  function board_clear22() {
    var h = '<div class="board b15 b22"><h2>1.22 · Clear of Umbra\'s toolbar<small>M3. With Umbra running, Tsukimichi reads where its toolbar is (top or bottom, its height and UI scale) and keeps the moon icon, the Todo overlay and Needs you 8 px clear of it. Nothing moves while you look: the clearance is applied when the toolbar appears, changes side or changes size.</small></h2><div class="row">';
    var bar = umbBar22("", "medallion", 1000);
    var off = 32 * 40 / 64;
    var before = at22(0, 0, bar) + at22(16, 14, todo22()) + at22(372, 8, '<div class="nd22"><b>Travel is stuck</b><span>No progress for 20 s near Camp Dragonhead.</span></div>') + at22(930 - off, 12 - off + 4, mi22("medallion", 32, { t: 0.9, dot: "needs" }));
    h += "<figure>" + frame22(before, 1000, 230, "scene-day.jpg", { size: "1200px auto", p: "50% 0%" }) + cap22("Without M3 (1.21)", "the overlay, Needs you and the icon sit under Umbra's bar") + "</figure>";
    h += "</div><div class=\"row\">";
    var after = at22(0, 0, bar) + at22(16, 46, todo22()) + at22(372, 46, '<div class="nd22"><b>Travel is stuck</b><span>No progress for 20 s near Camp Dragonhead.</span></div>') + at22(930 - off, 46 + 4 - off + 16, mi22("medallion", 32, { t: 0.9, dot: "needs" })) +
      '<div class="guide22" style="left:0;top:38px;width:1000px;height:0;border-width:1px 0 0 0"></div><span class="lab22" style="left:700px;top:44px">Umbra\'s bar (32 px × UI scale) + 8 px</span>';
    h += "<figure>" + frame22(after, 1000, 230, "scene-day.jpg", { size: "1200px auto", p: "50% 0%" }) + cap22("1.22", "each keeps its own place below the line; a place the player saved inside the bar's band is shown just below it, and comes back if Umbra's bar leaves") + "</figure>";
    h += '<figure><div class="spec" style="width:330px"><table><tr><th>Case</th><th>Rule</th></tr>' +
      "<tr><td>Top bar</td><td>Top edges at bar height + 8.</td></tr><tr><td>Bottom bar</td><td>Bottom edges at the bar's top − 8.</td></tr>" +
      "<tr><td>Floating or auto-hidden bar</td><td>No change: it does not hold an edge.</td></tr><tr><td>Can't read Umbra</td><td>Assume a 32 px top bar while Umbra is loaded, and say so in Settings › About.</td></tr>" +
      "<tr><td>Saved place</td><td>Never rewritten: the clearance is applied on top, so turning Umbra off restores it.</td></tr></table></div>" + cap22("The rules", "") + "</figure>";
    return h + "</div></div>";
  }

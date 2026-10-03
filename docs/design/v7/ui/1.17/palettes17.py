"""1.17 "Mix and match": the Dawn and Kugane Lacquer palettes and their high-contrast forms (spec-1.17 §E).

Builds on 1.16/palettes.py (same helpers, same HC transform). Every role as a hex; inks are the research §8.2 proposals,
re-tuned only where the worst surface missed the bar (each change is listed in TUNED). Writes palettes17.json and
contrast17.md, and prints the colour-vision check of research §8.2 (Accent vs Locked out, worst OKLab ΔE under Machado
protanopia, deuteranopia and tritanopia). Run: py -3 -X utf8 palettes17.py
"""
import json, math, pathlib, sys

HERE = pathlib.Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent / "1.16"))
from palettes import rgb, hexs, lin, cr, mix, ensure, high_contrast, NIGHT  # noqa: E402

# Dawn: the hour before sunrise. DARK (it shares every dark-path rule with Night: stars on, glows, night portrait grade).
DAWN = dict(
    Window="#1A1526", Sunken="#120E1B", Raised="#262036", Hover="#30283F", Line="#352D46", StrongLine="#76698C",
    Text="#F2E8E6", TextSecondary="#C4B4C0", TextTertiary="#A495AC", TextDisabled="#5A4E66",
    Deep="#0F0B17", Top="#2B1F3A", Zenith="#3A2746", Horizon="#5A3448", Ornament="#B98C6E", OrnamentHigh="#E9C4A4", OrnamentLight="#EDCBAA",
    Cool="#92A2E4", Accent="#F5C47C",
    Ready="#F5C47C", InJournal="#F5C47C", Completed="#F5C47C", ReadyOnOtherJob="#F2E8E6", DoneThisCycle="#F2E8E6",
    Blocked="#C4B4C0", LockedOut="#E68FB4", NotChecked="#A595AE",
    GaugeArc="#F5C47C", GaugeArcShade="#D9A55E", Groove="#3A3050",
    StripeGold="#F5C47C", StripeCompleted="#B99A6A", StripeSilver="#F2E8E6", StripeLocked="#C46A92", StripeVeil="#76698C",
)
# Kugane Lacquer: black lacquer at dusk. DARK. Vermilion lives in surfaces only (sky tint, the rail's lacquer edge), never in ink.
KUGANE = dict(
    Window="#16100F", Sunken="#0E0A09", Raised="#231917", Hover="#2D211E", Line="#342620", StrongLine="#7A6458",
    Text="#F3E9DB", TextSecondary="#C6B6A2", TextTertiary="#A69482", TextDisabled="#5C4C42",
    Deep="#0B0807", Top="#2A1613", Zenith="#3A1A14", Horizon="#4E2218", Ornament="#B8913F", OrnamentHigh="#E7C87C", OrnamentLight="#ECD08A",
    Cool="#7FA3DA", Accent="#F0CC72",
    Ready="#F0CC72", InJournal="#F0CC72", Completed="#F0CC72", ReadyOnOtherJob="#F3E9DB", DoneThisCycle="#F3E9DB",
    Blocked="#C6B6A2", LockedOut="#E58AC0", NotChecked="#A89888",
    GaugeArc="#F0CC72", GaugeArcShade="#D4AE55", Groove="#3A2A24",
    StripeGold="#F0CC72", StripeCompleted="#B79A5E", StripeSilver="#F3E9DB", StripeLocked="#C25E92", StripeVeil="#7A6458",
)
TUNED = {
    "dawn": "StrongLine #6F6486 -> #76698C (3.0:1 on Raised); TextTertiary #9A8BA2 -> #A495AC (4.5:1 on Hover); "
            "NotChecked #9C8FA8 -> #A595AE (4.5:1 on Hover)",
    "kugane": "StrongLine #705C52 -> #7A6458 (3.0:1 on Raised); TextTertiary #998775 -> #A69482 (4.5:1 on Hover); "
              "NotChecked #9A8C80 -> #A89888 (4.5:1 on Hover)",
}
def hc_derived(p):
    """Research §8.3: Dawn HC and Kugane HC are derived by the same transform as Night HC, but from their own inks (a
    warm palette keeps warm secondary text): Secondary and Tertiary to 7:1, StrongLine to 4.5:1 on the Window."""
    q = high_contrast(p, False)
    q["TextSecondary"] = ensure(p["TextSecondary"], p["Text"], p["Window"], 7.0)
    q["TextTertiary"] = ensure(p["TextTertiary"], p["Text"], p["Window"], 7.0)
    q["StrongLine"] = q["Ornament"] = ensure(p["StrongLine"], p["Text"], p["Window"], 4.5)
    return q

PALETTES = {"dawn": DAWN, "dawn-hc": hc_derived(DAWN), "kugane": KUGANE, "kugane-hc": hc_derived(KUGANE)}

PAIRS = [
    ("Text", "Window Raised Sunken Hover", 4.5), ("TextSecondary", "Window Raised Sunken Hover", 4.5),
    ("TextTertiary", "Window Raised Sunken Hover", 4.5), ("Accent", "Window Raised Sunken Hover", 4.5),
    ("Cool", "Window Raised Hover", 4.5), ("OrnamentLight", "Window Raised", 3.0), ("Ready", "Window Raised Hover", 4.5),
    ("Completed", "Window Raised", 4.5), ("ReadyOnOtherJob", "Window Raised", 4.5), ("DoneThisCycle", "Window Raised", 4.5),
    ("Blocked", "Window Raised Hover", 4.5), ("LockedOut", "Window Raised Hover", 4.5), ("NotChecked", "Window Raised Hover", 4.5),
    ("StrongLine", "Window Raised", 3.0), ("GaugeArc", "Window Groove Zenith", 3.0), ("StripeGold", "Window Raised Hover", 3.0),
    ("StripeLocked", "Window Raised", 3.0), ("OrnamentHigh", "Window", 3.0),
]

# OKLab and Machado 2009 (severity 1) in linear sRGB, for the Accent vs Locked out check.
MACHADO = {
    "prot": ((0.152286, 1.052583, -0.204868), (0.114503, 0.786281, 0.099216), (-0.003882, -0.048116, 1.051998)),
    "deut": ((0.367322, 0.860646, -0.227968), (0.280085, 0.672501, 0.047413), (-0.011820, 0.042940, 0.968881)),
    "trit": ((1.255528, -0.076749, -0.178779), (-0.078411, 0.930809, 0.147602), (0.004733, 0.691367, 0.303900)),
}
def oklab(l):
    r, g, b = l
    L = 0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b
    M = 0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b
    S = 0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b
    L, M, S = (max(0, v) ** (1 / 3) for v in (L, M, S))
    return (0.2104542553 * L + 0.7936177850 * M - 0.0040720468 * S,
            1.9779984951 * L - 2.4285922050 * M + 0.4505937099 * S,
            0.0259040371 * L + 0.7827717662 * M - 0.8086757660 * S)
def sim(h, m):
    l = [lin(v) for v in rgb(h)]
    return tuple(max(0, min(1, sum(m[i][j] * l[j] for j in range(3)))) for i in range(3))
def cvd_de(a, b):
    worst = 9
    for m in MACHADO.values():
        A, B = oklab(sim(a, m)), oklab(sim(b, m))
        worst = min(worst, math.dist(A, B))
    return worst

def table(keys):
    head = "| Ink | On | Bar | " + " | ".join({"dawn": "Dawn", "dawn-hc": "Dawn HC", "kugane": "Kugane Lacquer", "kugane-hc": "Kugane HC", "night": "Night"}[k] for k in keys) + " |"
    out = [head, "|---|---|---|" + "---|" * len(keys)]
    fails = []
    for ink, surfs, bar in PAIRS:
        cells = []
        for k in keys:
            p = PALETTES[k]
            v = min(cr(p[ink], p[s]) for s in surfs.split())
            b = 7.0 if (k.endswith("-hc") and bar == 4.5) else bar
            # HC's 7:1 applies on the Window; the worst surface must still clear 4.5
            ok = v >= (4.5 if b == 7.0 else b) and (b != 7.0 or cr(p[ink], p["Window"]) >= 6.95)
            if not ok: fails.append((k, ink, round(v, 2)))
            cells.append(f"{p[ink]} {v:.1f}" + ("" if ok else " **FAIL**"))
        out.append(f"| {ink} | {surfs.replace(' ', ', ')} (worst) | {bar:g}:1 | " + " | ".join(cells) + " |")
    return "\n".join(out), fails

if __name__ == "__main__":
    md, fails = table(["dawn", "dawn-hc", "kugane", "kugane-hc"])
    cvd = {k: round(cvd_de(PALETTES[k]["Accent"], PALETTES[k]["LockedOut"]), 3) for k in ("dawn", "kugane")}
    cvd["night"] = round(cvd_de(NIGHT["Accent"], NIGHT["LockedOut"]), 3)
    (HERE / "palettes17.json").write_text(json.dumps({"palettes": PALETTES, "tuned": TUNED, "cvd": cvd}, indent=1), encoding="utf-8")
    (HERE / "contrast17.md").write_text(md + "\n", encoding="utf-8")
    print(md); print("fails:", fails); print("Accent vs Locked out, worst CVD ΔE (OKLab):", cvd)

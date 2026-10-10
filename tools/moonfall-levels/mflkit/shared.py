"""The game-texture cache (docs/design/v9/rich/.cache, gitignored) and its mirror in the shared cache folder.

The design kit reads the textures from `paths.CACHE` inside the worktree, so a fresh worktree used to need `mfl.py fetch`
(minutes, and the game install). Now `ensure_textures()` copies what is missing from the mirror (`paths.SHARED/textures`,
the same for every worktree), fetches from the install only what neither has, and publishes new files to the mirror.
Files are copied, never linked, and copied atomically."""
import os
import shutil

from . import paths

MIRROR = paths.SHARED / "textures"
ULD = ["JobHudSCH0", "Journal_Detail", "Journal_Frame", "LovmPalette", "PVPRankEmblem3", "TabButtonA",
       "TripleTriadBattle", "TripleTriadCardSelect", "TripleTriadResultCrown", "LovmMiniMapFrame", "EMJIntroParts03",
       "WindowA_BgSelected_Corner", "ButtonA", "Parameter_Gauge"]
CARDS = [87019, 87020, 87049, 87050, 87056, 87058, 87059, 87060, 87065, 87066, 87067]
FONTS = ("AXIS_36", "Jupiter_46", "Jupiter_23", "MiedingerMid_36", "TrumpGothic_184", "TrumpGothic_68")


def wanted_textures():
    from .scene import load_recipe
    want = set()
    for p in paths.SCENES.glob("*.json"):
        r = load_recipe(p.stem)
        if r["source"]["kind"] == "game":
            want.add(r["source"]["texture"])
    return sorted(want)


def _missing():
    c = paths.CACHE
    miss = [t for t in wanted_textures() if not (c / paths.cache_name(t)).exists()]
    miss_ui = (not all((c / "uld" / f"ui_uld_{u}_hr1.png").exists() for u in ULD)
               or not all((c / "portraits" / f"icon_{k:06d}.png").exists() for k in CARDS)
               or not (c / "font" / "common_font_font7.png").exists())
    return miss, miss_ui


def sync(src, dst):
    """Copy the files of `src` that `dst` lacks (same relative path), atomically. Returns how many were copied."""
    n = 0
    if not src.exists():
        return 0
    for root, _dirs, files in os.walk(src):
        rel = os.path.relpath(root, src)
        for f in files:
            if f.endswith(".tmp"):
                continue
            target = dst / rel / f if rel != "." else dst / f
            if target.exists():
                continue
            target.parent.mkdir(parents=True, exist_ok=True)
            tmp = target.with_name(f"{target.name}.{os.getpid()}.tmp")
            shutil.copyfile(os.path.join(root, f), tmp)
            os.replace(tmp, target)
            n += 1
    return n


def fetch_all():
    """Dump every game texture the scene recipes and the chrome read from the install into paths.CACHE."""
    from . import engine
    engine.ensure_built()
    paths.CACHE.mkdir(parents=True, exist_ok=True)
    todo = [t for t in wanted_textures() if not (paths.CACHE / paths.cache_name(t)).exists()]
    if todo:
        code, out = engine.texdump("dump", paths.CACHE, *todo)
        print(out)
    # the chrome's UI art, the cards and the fonts (spec-rich2.md, "Rebuild")
    lst = paths.CACHE / "uld.txt"
    lst.write_text("\n".join(f"ui/uld/{u}_hr1.tex" for u in ULD) + "\n")
    if not all((paths.CACHE / "uld" / f"ui_uld_{u}_hr1.png").exists() for u in ULD):
        print(engine.texdump("dumplist", paths.CACHE / "uld", lst)[1])
    if not all((paths.CACHE / "portraits" / f"icon_{c:06d}.png").exists() for c in CARDS):
        print(engine.texdump("icon", paths.CACHE / "portraits", *CARDS)[1])
    font = paths.CACHE / "font"
    if not (font / "common_font_font7.png").exists():
        print(engine.texdump("raw", font, *[f"common/font/{f}.fdt" for f in FONTS])[1])
        print(engine.texdump("dump", font, *[f"common/font/font{i}.tex" for i in range(1, 8)])[1])


def ensure_textures(log=print):
    """Make paths.CACHE complete: from the shared mirror, else from the game install; publish what was new."""
    if os.environ.get("MFL_NO_TEXTURE_SYNC") == "1":
        return
    sync(MIRROR, paths.CACHE)
    miss, miss_ui = _missing()
    if miss or miss_ui:
        log(f"game textures missing in this worktree and the shared cache ({len(miss)} scene textures"
            f"{', chrome art' if miss_ui else ''}): fetching from the game install")
        fetch_all()
    sync(paths.CACHE, MIRROR)

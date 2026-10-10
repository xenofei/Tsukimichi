"""A level as the player sees it: the dressed scene, the readability veil, the pegs and bricks as the level file
places them in the colours the engine deals, the chrome from FFXIV's UI art in the level's palette, the lantern cart
and its spill. The same renderer as the approved pilots (rich2/src/composite2.py), for any level."""
from . import paths  # noqa: F401
from . import memo  # noqa: F401  (the layout mask and the piece distance, once per process)
import r2lib
import chrome2
import composite as rc                    # the rich pass's lantern spill
import composite2
from board import draw_pieces, veil
from r2lib import Img

CARRIERS = {1: "SuperGuide", 2: "Multiball", 3: "Wings", 4: "Burst", 5: "Flippers", 6: "Gate", 7: "Bloom", 8: "Draw",
            9: "Fireball", 10: "Path", 11: "Bolt"}

DEFAULT_PALETTE = dict(name="Medallion night", sky="#0E1C4E", deep="#070C24", jewel1="#1D4DB8", jewel2="#137C86",
                       warm="#FFB45E", rim="#7FB2FF")


def render(level, recipe, scene_px, colours, S=2, gone=(), stage=1, number=1, bucket="cart", t=0.0):
    key = "mfl:" + level["id"]
    r2lib.PALETTES[key] = (recipe.get("dress") or {}).get("palette") or DEFAULT_PALETTE
    sc = veil(scene_px, level, S, k=recipe.get("veil", 0.40))
    img = Img(800 * S, 600 * S, S, px=sc.copy())
    draw_pieces(img, level, colours, gone=gone, t=t)
    bucket_x = composite2.pick_bucket_x(level, gone)
    hud = dict(stage=f"{stage}-{(number - 1) % 5 + 1}", carrier=CARRIERS[stage], turns=0, active=False,
               name=level["name"])
    chrome2.playfield_chrome(img, level, pal=key, bucket=bucket, bucket_x=bucket_x, hud_kw=hud)
    rc.lantern_spill(img, level, gone, bucket_x)
    return img.px


# ---- composites in a pool: the renders of one build are independent of each other, so they run side by side ----
_POOL = None


def _warm():
    return True


def _render_task(args):
    level, recipe, scene_px, colours, kw = args
    return render(level, recipe, scene_px, colours, **kw)


def start_pool(workers=5):
    """The render pool of this process (started once; the workers import the kit while the caller does other work)."""
    global _POOL
    if _POOL is None:
        import concurrent.futures as cf
        import os
        _POOL = cf.ProcessPoolExecutor(max(1, min(workers, os.cpu_count() or 1)))
        for _ in range(_POOL._max_workers):
            _POOL.submit(_warm)
    return _POOL


def stop_pool():
    global _POOL
    if _POOL is not None:
        _POOL.shutdown(wait=True, cancel_futures=True)
        _POOL = None


class _Done:
    def __init__(self, fn):
        self._fn = fn

    def result(self):
        return self._fn()


def render_async(pool, level, recipe, scene_px, colours, **kw):
    """A handle whose .result() is render(...)'s picture. With a pool the render starts now, in a worker; without one
    it runs when .result() is asked for (so the answers are the same either way)."""
    if pool is None:
        return _Done(lambda: render(level, recipe, scene_px, colours, **kw))
    fut = pool.submit(_render_task, (level, recipe, scene_px, colours, kw))
    return fut

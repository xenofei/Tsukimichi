"""The grading parity fixture (runtime art, stage "in play"): a small synthetic picture of our own (no game art) and
what the approved Python grades make of it, for Tsukimichi.Tests/Moonfall/MoonfallGradeParityTests.cs.

  py -3 make_parity.py

Writes Tsukimichi.Tests/Fixtures/moonfall-grade/:
  fixture.png          96 x 72 RGB (the 800 x 600 board at S = 0.12)
  fixture-rgba.png     48 x 40 RGBA (a gilt ornament with soft alpha, for gild)
  <case>.bin           float32 little-endian, H x W x 3, the reference output of each case

The reference functions are imported from the approved sources (rich_lib.night_lab, dress2.jewel, r2lib.gild), never
copied, so the C# port is checked against what made the approved renders. One step is the runtime's own: the jewel's
lightness lock. F1 says the jewel keeps every pixel's OKLab L exactly ("by construction"), but dress2.jewel ends in
oklab_to_srgb, which clips a dark, saturated colour pushed outside sRGB channel by channel, and that moves its
lightness. The runtime keeps L and gives up chroma instead (MoonfallColor.ToSrgbKeepingLightness), so the jewel
references are made with that conversion (keeping_lightness below, the same bisection) in place of the clip; inside the
gamut the two are the same colour.
"""
import atexit
import pathlib
import sys

import numpy as np
from PIL import Image

HERE = pathlib.Path(__file__).resolve().parent
V9 = HERE.parent.parent
REPO = V9.parents[2]
OUT = REPO / "Tsukimichi.Tests" / "Fixtures" / "moonfall-grade"

# r2lib writes sources.json at exit; the parity run reads no game file, so keep it from touching the record.
atexit.register = lambda f, *a, **k: f
sys.path.insert(0, str(V9 / "rich2" / "src"))
sys.path.insert(0, str(V9 / "rich" / "src"))
sys.path.insert(0, str(V9 / "src"))

from rich_lib import night_lab, blur, smooth, srgb_to_oklab  # noqa: E402
import dress2  # noqa: E402
from r2lib import gild  # noqa: E402

S = 0.12
W, H = 96, 72


def fixture():
    """A night-ish painting of our own: a blue sky gradient with warm cloud, pale domes, a green field, a sea band and
    a dark foreground, with fine texture so every band of the grade has something to act on."""
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float32)
    t = yy / (H - 1)
    sky = np.stack([0.30 + 0.25 * t, 0.45 + 0.20 * t, 0.85 - 0.25 * t], -1)
    rng = np.random.default_rng(7)
    tex = rng.random((H, W)).astype(np.float32)
    px = sky + (tex[..., None] - 0.5) * 0.10
    # warm cloud in the upper right
    cl = np.exp(-(((xx - 70) / 18) ** 2 + ((yy - 14) / 7) ** 2))
    px = px * (1 - cl[..., None]) + np.array([0.95, 0.80, 0.62]) * cl[..., None]
    # two pale domes
    for (cx, cy, r) in ((24, 34, 9), (46, 30, 6)):
        d = np.sqrt((xx - cx) ** 2 + (yy - cy) ** 2)
        m = np.clip(r - d, 0, 1)
        px = px * (1 - m[..., None]) + np.array([0.92, 0.90, 0.86]) * m[..., None]
    # a green field and a teal sea
    field = (yy > 44) & (yy < 56)
    px[field] = px[field] * 0.4 + np.array([0.25, 0.55, 0.20]) * 0.6
    sea = yy >= 56
    px[sea] = px[sea] * 0.3 + np.array([0.10, 0.45, 0.55]) * 0.7
    # a dark foreground post
    post = (np.abs(xx - 12) < 3) & (yy > 20)
    px[post] = px[post] * 0.2
    return np.clip(px, 0, 1).astype(np.float32)


def fixture_rgba():
    h, w = 40, 48
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    rng = np.random.default_rng(11)
    base = np.stack([0.78 + 0.1 * np.sin(xx / 5), 0.66 + 0.08 * np.cos(yy / 4), 0.36 + 0.05 * rng.random((h, w))], -1)
    grey = (xx > 30)
    base[grey] = base[grey].mean(-1, keepdims=True)
    d = np.sqrt(((xx - 24) / 20) ** 2 + ((yy - 20) / 16) ** 2)
    a = np.clip((1 - d) * 4, 0, 1)
    return np.concatenate([np.clip(base, 0, 1), a[..., None]], -1).astype(np.float32)


def save_png(px, path):
    mode = "RGBA" if px.shape[-1] == 4 else "RGB"
    Image.fromarray((np.clip(px, 0, 1) * 255 + 0.5).astype(np.uint8), mode).save(path, optimize=True)


def load_png(path):
    return np.asarray(Image.open(path), np.float32) / 255.0


def save_bin(px, path):
    np.ascontiguousarray(px[..., :3], dtype="<f4").tofile(path)


def keeping_lightness(lab):
    """oklab_to_srgb with F1 kept: a colour outside sRGB keeps L and hue and gives up chroma (8 halvings) until it fits."""
    import rich_lib

    def lin(q):
        M2i = np.array([[1.0, 0.3963377774, 0.2158037573], [1.0, -0.1055613458, -0.0638541728],
                        [1.0, -0.0894841775, -1.2914855480]], np.float32)
        M1i = np.array([[4.0767416621, -3.3077115913, 0.2309699292], [-1.2684380046, 2.6097574011, -0.3413193965],
                        [-0.0041960863, -0.7034186147, 1.7076147010]], np.float32)
        return ((q @ M2i.T) ** 3) @ M1i.T

    def fits(q):
        v = lin(q)
        return np.all((v >= -1e-5) & (v <= 1 + 1e-5), axis=-1)

    lab = np.asarray(lab, np.float32)
    ok = fits(lab)
    lo = np.where(ok, 1.0, 0.0).astype(np.float32)
    hi = np.ones_like(lo)
    for _ in range(8):
        mid = (lo + hi) * 0.5
        q = lab.copy()
        q[..., 1:] *= mid[..., None]
        good = fits(q) & ~ok
        lo = np.where(good, mid, lo)
        hi = np.where(good | ok, hi, mid)
    q = lab.copy()
    q[..., 1:] *= lo[..., None]
    return rich_lib.oklab_to_srgb(q)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    save_png(fixture(), OUT / "fixture.png")
    save_png(fixture_rgba(), OUT / "fixture-rgba.png")
    # the grades read the 8-bit fixture, as the C# test does
    src = load_png(OUT / "fixture.png")
    rgba = load_png(OUT / "fixture-rgba.png")

    save_bin(night_lab(src, S=S), OUT / "nightlab.bin")
    save_bin(night_lab(src, gamma=1.35, exposure=0.72, warm_keep=0.25, chroma_mid=0.45, S=S), OUT / "nightlab-chart.bin")
    yy = np.mgrid[0:H, 0:W][0] / H
    sky = blur(smooth(0.02, 0.15, src[..., 2] - src[..., 0]) * smooth(0.75, 0.35, yy), 3 * S)
    save_bin(night_lab(src, sky=sky, sky_drop=0.3, S=S), OUT / "nightlab-sky.bin")
    save_bin(night_lab(src, tint="#CBC4EA", base_hue="#2A2358", gamma=1.8, sky_drop=0.0, exposure=0.70, S=S),
             OUT / "nightlab-violet.bin")

    dress2.oklab_to_srgb = keeping_lightness   # the jewel's lightness lock (see the module's note)
    graded = night_lab(src, gamma=1.35, exposure=0.72, warm_keep=0.25, chroma_mid=0.45, S=S)
    Lsc = srgb_to_oklab(graded)[..., 0]
    sea = smooth(0.30, 0.42, blur(Lsc, 3 * S))
    save_bin(dress2.jewel(graded, S, [(0, "#2B5FD0"), (600, "#1D4DB8")], chroma=0.9, floor=0.03,
                          regions=[(sea * 0.85, "#169A9A", 0.085)]), OUT / "jewel.bin")
    save_bin(dress2.jewel(graded, S, [(0, "#3A6FD8"), (600, "#2B4FB0")],
                          value_hues=[(0.08, "#1E3A9A"), (0.45, "#C9D6FF")], chroma=0.95, floor=0.03), OUT / "jewel-values.bin")
    save_bin(gild(rgba, 0.55, 0.1), OUT / "gild.bin")
    print("ok", OUT)


if __name__ == "__main__":
    main()

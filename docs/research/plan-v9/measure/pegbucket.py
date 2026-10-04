"""Track the Free Ball Bucket's horizontal position (measurement only).

The bucket's open top is a near-black ellipse in the bottom strip of the board.
Per frame, the black pixels in native rows [y0, y1] give the opening's left and
right edges; centre = midpoint, width = right - left.

Usage:
  uv run --with opencv-python --with numpy --with scipy python pegbucket.py VIDEO START_S END_S OUT.csv
         [--scale S] [--ox X --oy Y] [--y0 570 --y1 586]

Then fit the motion with --fit: prints the extreme positions, the half-period,
and the RMS of a sinusoid fit vs a constant-speed (triangle wave) fit.
"""
import argparse
import csv

import cv2
import numpy as np
from scipy.optimize import least_squares


def track(a):
    cap = cv2.VideoCapture(a.video)
    fps = cap.get(cv2.CAP_PROP_FPS)
    s, e = int(a.start * fps), int(a.end * fps)
    cap.set(cv2.CAP_PROP_POS_FRAMES, s)
    rows = []
    sc = a.scale
    for i in range(s, e + 1):
        ok, f = cap.read()
        if not ok:
            break
        if sc is None:
            sc = f.shape[0] / 600.0
        y0, y1 = int(a.oy + a.y0 * sc), int(a.oy + a.y1 * sc)
        x0, x1 = int(a.ox + 60 * sc), int(a.ox + 740 * sc)
        strip = f[y0:y1, x0:x1]
        v = strip.max(axis=2)
        dark = (v < a.vmax).sum(axis=0) >= max(2, int(0.25 * (y1 - y0)))
        xs = np.flatnonzero(dark)
        if len(xs) < 10:
            rows.append([i, i / fps, "", ""])
            continue
        # largest contiguous dark run
        breaks = np.flatnonzero(np.diff(xs) > 3)
        segs = np.split(xs, breaks + 1)
        seg = max(segs, key=len)
        l, r = seg[0], seg[-1]
        rows.append([i, round(i / fps, 5), round((x0 + (l + r) / 2 - a.ox) / sc, 2), round((r - l + 1) / sc, 2)])
    with open(a.out, "w", newline="") as fh:
        w = csv.writer(fh)
        w.writerow(["frame", "t", "x", "w"])
        w.writerows(rows)
    print(f"{len(rows)} frames, scale {sc:.3f}")


def fit(a):
    t, x, w = [], [], []
    for r in csv.DictReader(open(a.out)):
        if r["x"] and a.wmin <= float(r["w"]) <= a.wmax:
            t.append(float(r["t"])); x.append(float(r["x"])); w.append(float(r["w"]))
    t, x, w = map(np.array, (t, x, w))
    print(f"opening width median {np.median(w):.2f} (IQR {np.percentile(w,25):.2f}-{np.percentile(w,75):.2f}) native px, N={len(w)}")
    print(f"centre range {x.min():.1f} .. {x.max():.1f}")
    c0, A0 = (x.max() + x.min()) / 2, (x.max() - x.min()) / 2
    # period guess from zero crossings of x - c0
    zc = t[np.flatnonzero(np.diff(np.sign(x - c0)) != 0)]
    T0 = 2 * np.median(np.diff(zc)) if len(zc) > 2 else 5.0
    def sin_res(p):
        c, A, T, ph = p
        return c + A * np.sin(2 * np.pi * t / T + ph) - x
    best = None
    for ph in np.linspace(0, 2 * np.pi, 12, endpoint=False):
        r = least_squares(sin_res, [c0, A0, T0, ph])
        if best is None or r.cost < best.cost:
            best = r
    c, A, T, ph = best.x
    rms_s = np.sqrt(np.mean(best.fun ** 2))
    def tri(p):
        c, A, T, ph = p
        u = (t / T + ph / (2 * np.pi)) % 1.0
        return c + A * (4 * np.abs(u - 0.5) - 1) - x
    bt = None
    for ph in np.linspace(0, 2 * np.pi, 24, endpoint=False):
        r = least_squares(tri, [c0, A0, T0, ph])
        if bt is None or r.cost < bt.cost:
            bt = r
    rms_t = np.sqrt(np.mean(bt.fun ** 2))
    print(f"sinusoid: centre {c:.1f} amplitude {abs(A):.1f} period {T:.3f}s  peak speed {2*np.pi*abs(A)/T:.1f} px/s  RMS {rms_s:.2f}")
    print(f"triangle: centre {bt.x[0]:.1f} amplitude {abs(bt.x[1]):.1f} period {bt.x[2]:.3f}s  speed {4*abs(bt.x[1])/bt.x[2]:.1f} px/s  RMS {rms_t:.2f}")
    # numerical speed profile
    # residual shape of the sinusoid fit, binned by phase (systematic deviation = not a pure sine)
    phase = ((2 * np.pi * t / T + ph) % (2 * np.pi))
    bins = np.linspace(0, 2 * np.pi, 9)
    prof = [float(np.mean(best.fun[(phase >= bins[k]) & (phase < bins[k + 1])])) if np.any((phase >= bins[k]) & (phase < bins[k + 1])) else float("nan") for k in range(8)]
    print("sinusoid residual by phase octant:", " ".join(f"{p:+.1f}" for p in prof))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("video"); ap.add_argument("start", type=float); ap.add_argument("end", type=float); ap.add_argument("out")
    ap.add_argument("--scale", type=float, default=None)
    ap.add_argument("--ox", type=float, default=0.0); ap.add_argument("--oy", type=float, default=0.0)
    ap.add_argument("--y0", type=float, default=572); ap.add_argument("--y1", type=float, default=583)
    ap.add_argument("--vmax", type=int, default=45)
    ap.add_argument("--wmin", type=float, default=95.0, help="reject frames whose opening is narrower (occluded, menus)")
    ap.add_argument("--wmax", type=float, default=115.0)
    ap.add_argument("--windows", action="store_true", help="only run the per-window fits on an existing OUT.csv")
    ap.add_argument("--fit", action="store_true", help="only fit an existing OUT.csv")
    a = ap.parse_args()
    if a.windows:
        windows(a.out, wmin=a.wmin, wmax=a.wmax)
        return
    if not a.fit:
        track(a)
    fit(a)


def windows(path, win=8.0, wmin=95.0, wmax=115.0):
    """Fit the sinusoid separately in consecutive windows (bucket phase resets between levels,
    and right-click speed-up would show as a shorter period).  Called with --windows."""
    t, x = [], []
    for r in csv.DictReader(open(path)):
        if r["x"] and wmin <= float(r["w"]) <= wmax:
            t.append(float(r["t"])); x.append(float(r["x"]))
    t, x = np.array(t), np.array(x)
    out = []
    t0 = t[0]
    while t0 < t[-1]:
        sel = (t >= t0) & (t < t0 + win)
        if sel.sum() > 0.8 * win * 60 and np.ptp(x[sel]) > 400:
            tt, xx = t[sel], x[sel]
            best = None
            for T0 in (5.0, 6.0, 7.0):
                for ph in np.linspace(0, 2 * np.pi, 12, endpoint=False):
                    r = least_squares(lambda p: p[0] + p[1] * np.sin(2 * np.pi * (tt - t0) / p[2] + p[3]) - xx, [400, 260, T0, ph])
                    if best is None or r.cost < best.cost:
                        best = r
            c, A, T, ph = best.x
            rms = np.sqrt(np.mean(best.fun ** 2))
            out.append((t0, c, abs(A), T, rms))
            print(f"  window {t0:7.1f}s  centre {c:6.1f}  amplitude {abs(A):6.1f}  period {T:6.3f}s  RMS {rms:5.2f}")
        t0 += win
    good = [o for o in out if o[4] < 3]
    if good:
        arr = np.array(good)
        print(f"windows with RMS<3: {len(good)}  centre {arr[:,1].mean():.1f}+-{arr[:,1].std():.1f}  amplitude {arr[:,2].mean():.1f}+-{arr[:,2].std():.1f}  "
              f"period {arr[:,3].mean():.3f}+-{arr[:,3].std():.3f}s")


if __name__ == "__main__":
    main()

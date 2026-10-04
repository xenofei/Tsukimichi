"""Bounce analysis from consecutive free-flight arcs (measurement only).

For each pair of arcs A, B (from pegfit.py) with a gap of at most --gap frames
between A's last and B's first point, the collision time is where the two
fitted parabolas come closest; the velocities of A and B there are v_in, v_out.

The surface normal is taken from the obstacle:
  * a round peg: the peg circle nearest the collision point, found with a Hough
    transform in the frame just before the collision; n = (c - peg) / |c - peg|,
    and |c - peg| is the collision distance (ball radius + peg radius);
  * a side wall: |n_x| = 1 when the collision point is within --wallx px of the
    playfield edge seen in the data.

Reported per bounce: normal restitution e_n = -v_out.n / v_in.n and tangential
ratio e_t = v_out.t / v_in.t.

The ball position is the centroid of the ball's gold lower half, which sits
below the true centre by a fixed offset; the offset dy that makes the
centre-to-peg distance independent of direction is fitted and reported.

Usage:
  uv run --with opencv-python --with numpy --with scipy python pegbounce.py VIDEO ARCS.csv [--gap 4] [--out bounces.csv]
"""
import argparse
import csv
import json

import cv2
import numpy as np
from scipy.optimize import least_squares

G = 500.0

ap = argparse.ArgumentParser()
ap.add_argument("video"); ap.add_argument("arcs")
ap.add_argument("--gap", type=int, default=4)
ap.add_argument("--minn", type=int, default=8)
ap.add_argument("--rms", type=float, default=1.2)
ap.add_argument("--out", default="bounces.csv")
ap.add_argument("--exclude", nargs="*", default=[], help="time windows start:end (s) to skip, e.g. Fever")
a = ap.parse_args()
ex = [tuple(map(float, e.split(":"))) for e in a.exclude]

arcs = []
for r in csv.DictReader(open(a.arcs)):
    if int(r["n"]) < a.minn or float(r["rms"]) > a.rms:
        continue
    t0 = float(r["t0"])
    if any(s <= t0 <= e for s, e in ex):
        continue
    p = np.array(json.loads(r["pts"]))
    f = p[:, 0]; t = f / 60.0
    tt = t - t[0]
    A = np.vstack([np.ones_like(tt), tt]).T
    (x0, vx), *_ = np.linalg.lstsq(A, p[:, 1], rcond=None)
    (y0, vy), *_ = np.linalg.lstsq(A, p[:, 2] - 0.5 * G * tt * tt, rcond=None)
    arcs.append(dict(f0=int(f[0]), f1=int(f[-1]), t0=t[0], x0=x0, y0=y0, vx=vx, vy=vy))
arcs.sort(key=lambda d: d["f0"])


def pos(arc, t):
    dt = t - arc["t0"]
    return np.array([arc["x0"] + arc["vx"] * dt, arc["y0"] + arc["vy"] * dt + 0.5 * G * dt * dt])


def vel(arc, t):
    return np.array([arc["vx"], arc["vy"] + G * (t - arc["t0"])])


cap = cv2.VideoCapture(a.video)
pairs = []
for A_, B_ in zip(arcs, arcs[1:]):
    if not (0 < B_["f0"] - A_["f1"] <= a.gap):
        continue
    ts = np.linspace(A_["f1"] / 60.0, B_["f0"] / 60.0, 200)
    d = [np.linalg.norm(pos(A_, t) - pos(B_, t)) for t in ts]
    k = int(np.argmin(d))
    if d[k] > 3.0:
        continue
    tc = ts[k]
    c = 0.5 * (pos(A_, tc) + pos(B_, tc))
    vin, vout = vel(A_, tc), vel(B_, tc)
    if np.linalg.norm(vin - vout) < 30:
        continue
    pairs.append((A_["f1"], tc, c, vin, vout))

rows = []
for f1, tc, c, vin, vout in pairs:
    cap.set(cv2.CAP_PROP_POS_FRAMES, max(0, f1 - 1)); ok, fr = cap.read()
    sc = fr.shape[0] / 600.0
    X, Y, h = int(c[0] * sc), int(c[1] * sc), int(32 * sc)
    y0, x0 = max(0, Y - h), max(0, X - h)
    g = cv2.cvtColor(fr[y0:Y + h, x0:X + h], cv2.COLOR_BGR2GRAY)
    g = cv2.GaussianBlur(g, (5, 5), 1.2)
    circ = cv2.HoughCircles(g, cv2.HOUGH_GRADIENT, dp=1, minDist=int(14 * sc), param1=80, param2=18,
                            minRadius=int(7.5 * sc), maxRadius=int(11.5 * sc))
    kind, pc, prad = "none", None, np.nan
    if circ is not None:
        cs = circ[0]
        cc = np.c_[(cs[:, 0] + x0) / sc, (cs[:, 1] + y0) / sc]
        dd = np.hypot(*(cc - c).T)
        j = int(np.argmin(dd))
        if 9 <= dd[j] <= 24:
            kind, pc, prad = "peg", cc[j], cs[j, 2] / sc
    if kind == "none" and (c[0] < 100 or c[0] > 700):
        kind = "wall"
    if kind == "peg":
        n = (c - pc) / np.linalg.norm(c - pc)
        dist = float(np.linalg.norm(c - pc))
    elif kind == "wall":
        n = np.array([1.0, 0.0]) if c[0] < 400 else np.array([-1.0, 0.0])
        dist = np.nan
    else:
        continue
    tv = np.array([-n[1], n[0]])
    vn_in, vn_out = vin @ n, vout @ n
    vt_in, vt_out = vin @ tv, vout @ tv
    if vn_in >= -20:  # not moving into the surface
        continue
    rows.append(dict(t=round(tc, 3), kind=kind, cx=round(c[0], 2), cy=round(c[1], 2),
                     px="" if pc is None else round(pc[0], 2), py="" if pc is None else round(pc[1], 2),
                     prad=round(prad, 2) if kind == "peg" else "", dist=round(dist, 2) if kind == "peg" else "",
                     nx=round(n[0], 3), ny=round(n[1], 3), vin=round(np.linalg.norm(vin), 1), vout=round(np.linalg.norm(vout), 1),
                     vn_in=round(vn_in, 1), vn_out=round(vn_out, 1), vt_in=round(vt_in, 1), vt_out=round(vt_out, 1),
                     en=round(-vn_out / vn_in, 3), et=round(vt_out / vt_in, 3) if abs(vt_in) > 40 else ""))
with open(a.out, "w", newline="") as fh:
    if rows:
        w = csv.DictWriter(fh, fieldnames=list(rows[0].keys())); w.writeheader(); w.writerows(rows)
print(f"{len(pairs)} bounce pairs, {len(rows)} with an identified surface")
for kind in ("peg", "wall"):
    R = [r for r in rows if r["kind"] == kind]
    if not R:
        continue
    en = np.array([r["en"] for r in R]); et = np.array([r["et"] for r in R if r["et"] != ""])
    print(f"{kind}: N={len(R)}  e_n median {np.median(en):.3f} IQR {np.percentile(en,25):.3f}-{np.percentile(en,75):.3f}"
          + (f"  e_t median {np.median(et):.3f} IQR {np.percentile(et,25):.3f}-{np.percentile(et,75):.3f} (N={len(et)})" if len(et) else ""))
    if kind == "peg":
        cx = np.array([r["cx"] for r in R]); cy = np.array([r["cy"] for r in R])
        px = np.array([r["px"] for r in R]); py = np.array([r["py"] for r in R])
        dist = np.hypot(cx - px, cy - py)
        # fit a fixed centroid offset (dx, dy) and the collision distance D: |c - (dx,dy) - p| = D
        def res(q):
            return np.hypot(cx - q[0] - px, cy - q[1] - py) - q[2]
        q = least_squares(res, [0, 2, np.median(dist)], loss="soft_l1", f_scale=1.0).x
        r_ = res(q)
        print(f"  centre-to-peg distance at contact: raw median {np.median(dist):.2f}; with fitted centroid offset "
              f"(dx {q[0]:+.2f}, dy {q[1]:+.2f}) D = {q[2]:.2f} px, residual MAD {1.4826*np.median(np.abs(r_)):.2f}; "
              f"Hough peg radius median {np.nanmedian([r['prad'] for r in R]):.2f}")
        # restitution model fitted on bounces whose peg distance is consistent with D (normal is trustworthy):
        # |v_out|^2 = e_n^2 (v_in.n)^2 + e_t^2 (v_in.t)^2
        good = np.abs(r_) < 1.5
        vin = np.array([r["vin"] for r in R])[good]; vout = np.array([r["vout"] for r in R])[good]
        vni = np.array([r["vn_in"] for r in R])[good]; vti = np.array([r["vt_in"] for r in R])[good]
        def rr(q):
            return np.sqrt(q[0] ** 2 * vni ** 2 + q[1] ** 2 * vti ** 2) - vout
        fit = least_squares(rr, [0.8, 0.9], loss="soft_l1", f_scale=10.0)
        J = fit.jac; cov = np.linalg.pinv(J.T @ J) * np.sum(fit.fun ** 2) / max(1, len(vout) - 2)
        cosang = np.abs(vni) / vin
        print(f"  speed-ratio model on N={good.sum()} bounces with consistent normals: e_n = {fit.x[0]:.3f} +- {np.sqrt(cov[0,0]):.3f}, "
              f"e_t = {fit.x[1]:.3f} +- {np.sqrt(cov[1,1]):.3f}")
        ratio = vout / vin
        for lo, hi in ((0.0, 0.5), (0.5, 0.8), (0.8, 1.01)):
            m = (cosang >= lo) & (cosang < hi)
            if m.any():
                print(f"    |cos(incidence)| {lo:.1f}-{hi:.1f}: speed ratio median {np.median(ratio[m]):.3f} (N={m.sum()})")
        allratio = np.array([r["vout"] / r["vin"] for r in R])
        print(f"  overall peg speed ratio |v_out|/|v_in|: median {np.median(allratio):.3f} IQR {np.percentile(allratio,25):.3f}-{np.percentile(allratio,75):.3f} (N={len(allratio)})")
    if kind == "wall":
        ratio = np.array([r["vout"] / r["vin"] for r in R])
        print(f"  wall speed ratio median {np.median(ratio):.3f}, values {np.round(ratio, 3).tolist()}")

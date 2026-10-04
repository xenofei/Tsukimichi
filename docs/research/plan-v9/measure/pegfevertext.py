"""Find the "EXTREME FEVER" banner (large orange-yellow text near the top centre)
and print when it is on screen (s).  Used to locate every Fever in a longplay.

Usage: uv run --with opencv-python --with numpy python pegfevertext.py VIDEO [--step 3]
Region (native px): x 260-540, y 40-80 of the 800x600 frame (the banner covers it
even while the camera is zoomed); small yellow HUD text stays below --min.
"""
import argparse, cv2, numpy as np
ap = argparse.ArgumentParser(); ap.add_argument("video"); ap.add_argument("--step", type=int, default=3)
ap.add_argument("--min", type=int, default=150, help="yellow pixel count (native px^2) that counts as text")
a = ap.parse_args()
cap = cv2.VideoCapture(a.video); fps = cap.get(5)
i = 0; on = None; out = []
while True:
    ok = cap.grab()
    if not ok: break
    if i % a.step == 0:
        ok, f = cap.retrieve(); sc = f.shape[0] / 600
        roi = f[int(40 * sc):int(80 * sc), int(260 * sc):int(540 * sc)]
        hsv = cv2.cvtColor(roi, cv2.COLOR_BGR2HSV)
        n = cv2.countNonZero(cv2.inRange(hsv, (22, 150, 200), (35, 255, 255))) / sc**2
        if n > a.min and on is None: on = i
        if n <= a.min and on is not None:
            if i - on > 10: out.append((on / fps, i / fps))
            on = None
    i += 1
for s, e in out: print(f"{s:8.2f} - {e:8.2f}")

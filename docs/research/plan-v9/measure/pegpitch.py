"""Peg-hit pitch ladder (measurement only).

Prints, every 30 ms, the strongest tonal peaks (240-2400 Hz) that stand at least
18 dB above the clip's median spectrum (the median removes the steady music
bed).  Each peg hit shows up as a two-note chord (a fifth, ratio 1.5) whose
lower note climbs one semitone per new peg hit; read the plateaus off the list.
With --png a log-frequency spectrogram with semitone grid lines is also saved.

Usage:
  ffmpeg -ss START -t DUR -i VIDEO -vn -ac 1 -ar 44100 clip.wav
  uv run --with numpy --with scipy [--with matplotlib] python pegpitch.py clip.wav START_S [--png out.png]
"""
import argparse

import numpy as np
from scipy.io import wavfile
from scipy.signal import find_peaks, stft

ap = argparse.ArgumentParser(); ap.add_argument("wav"); ap.add_argument("offset", type=float); ap.add_argument("--png")
a = ap.parse_args()
sr, x = wavfile.read(a.wav); x = x.astype(float)
if x.ndim > 1:
    x = x.mean(axis=1)
N = int(0.08 * sr); hop = int(0.01 * sr); n = 1 << 15
fr = np.fft.rfftfreq(n, 1 / sr); sel = (fr > 240) & (fr < 2400)
S = np.array([20 * np.log10(np.abs(np.fft.rfft(x[i:i + N] * np.hanning(N), n)) + 1e-6) for i in range(0, len(x) - N, hop)])
ts = np.arange(len(S)) * hop / sr + a.offset
med = np.median(S, axis=0)
for k in range(0, len(ts), 3):
    d = S[k, sel] - med[sel]
    pk, _ = find_peaks(d, height=18, distance=200)
    if len(pk):
        top = sorted(pk, key=lambda p: -d[p])[:3]
        print(f"{ts[k]:.2f} " + "  ".join(f"{fr[sel][p]:.0f}({d[p]:.0f})" for p in sorted(top)))
if a.png:
    import matplotlib; matplotlib.use("Agg"); import matplotlib.pyplot as plt
    f, t, Z = stft(x, sr, nperseg=4096, noverlap=4096 - 256); L = 20 * np.log10(np.abs(Z) + 1e-6)
    s2 = (f > 100) & (f < 3000)
    plt.figure(figsize=(16, 9)); plt.pcolormesh(t + a.offset, f[s2], L[s2], shading="auto", vmin=L.max() - 70, vmax=L.max(), cmap="magma")
    plt.yscale("log")
    for k in range(-12, 30):
        plt.axhline(261.63 * 2 ** (k / 12), color="w", lw=0.2, alpha=0.4)
    plt.yticks([131, 165, 196, 262, 330, 392, 523, 659, 784, 1047, 1319, 1568, 2093],
               ["C3", "E3", "G3", "C4", "E4", "G4", "C5", "E5", "G5", "C6", "E6", "G6", "C7"])
    plt.tight_layout(); plt.savefig(a.png, dpi=95)

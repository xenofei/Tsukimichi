"""Device-space blit (kept apart so r2lib's band builder can use it)."""
import numpy as np


def blit_arr_dev(img, arr, X0, Y0, alpha=1.0):
    xs, ys = max(0, -X0), max(0, -Y0)
    X1, Y1 = min(img.w, X0 + arr.shape[1]), min(img.h, Y0 + arr.shape[0])
    if X1 <= max(X0, 0) or Y1 <= max(Y0, 0):
        return
    a = arr[ys:ys + Y1 - max(Y0, 0), xs:xs + X1 - max(X0, 0)]
    al = a[..., 3:4] * alpha
    dst = img.px[max(Y0, 0):Y1, max(X0, 0):X1]
    img.px[max(Y0, 0):Y1, max(X0, 0):X1] = dst * (1 - al) + a[..., :3] * al

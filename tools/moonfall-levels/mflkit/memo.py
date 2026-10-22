"""Layout-only work done once per process instead of once per call.

A build asks for the same two level-only pictures many times: the distance from every pixel to the nearest piece
(`framecheck.piece_distance`, 5 calls) and the readability veil's layout mask (`board.layout_mask`, once per composite).
Both depend only on the level file (and the size), never on the scene or the colours. Importing this module wraps
the design kit's two functions with a small memo, so every caller gets the same numbers it always did, computed once
(a copy each time, so a caller that edits its array cannot spoil the next).
The memo lives in one process only, so it can never go stale between runs.
"""
import json

from . import paths  # noqa: F401  (puts the design kit on the path)
import board
import framecheck

_ORIG_MASK = board.layout_mask
_ORIG_DIST = framecheck.piece_distance
_MASKS = {}
_DISTS = {}
_KEEP = 6


def _lv(level):
    return json.dumps(level, sort_keys=True)


def _put(store, key, val):
    store[key] = val
    while len(store) > _KEEP:
        store.pop(next(iter(store)))


def mask_key(level, W, H, S, grow):
    return ("mask", _lv(level), W, H, float(S), float(grow))


def layout_mask(level, W, H, S, t=0.0, grow=16.0):
    """board.layout_mask, memoised by the level and size (t is unused by the original)."""
    k = mask_key(level, W, H, S, grow)
    if k not in _MASKS:
        _put(_MASKS, k, _ORIG_MASK(level, W, H, S, t=t, grow=grow))
    return _MASKS[k].copy()


def piece_distance(level, S=1.0):
    """framecheck.piece_distance, memoised by the level and S."""
    k = ("dist", _lv(level), float(S))
    if k not in _DISTS:
        _put(_DISTS, k, _ORIG_DIST(level, S))
    return _DISTS[k].copy()


board.layout_mask = layout_mask
framecheck.piece_distance = piece_distance

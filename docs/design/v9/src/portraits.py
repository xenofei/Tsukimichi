"""The power's medallion on the HUD's right rail: the active character's head and shoulders in silhouette, clipped
to the enamel disc, rim-lit from the upper left like every figure."""
import numpy as np

from figures import draw_figure

# where each figure's head sits (figure units) and how much of the figure the medallion shows (units across)
HEAD = {"pipiru": (-6, -40, 62), "kaede": (0, -74, 44), "marcia": (0, -72, 44), "haldbrand": (0, -98, 60),
        "gajavati": (0, -82, 56), "ysolde": (0, -97, 40), "ottilie": (0, -71, 40), "gyobo": (0, -38, 52),
        "aldous": (0, -88, 56), "ione": (0, -66, 40), "kupsa": (0, -30, 44)}


def medallion_portrait(name):
    def draw(img, mx, my, mr):
        hx, hy, span = HEAD[name]
        k = (2 * mr) / span
        fx, fy = mx - hx * k, my + mr * 0.12 - hy * k
        clip = lambda X, Y: np.clip((mr - 0.6 - np.sqrt((X - mx) ** 2 + (Y - my) ** 2)) * img.S + 0.5, 0, 1)
        draw_figure(img, name, fx, fy, k=k, shadow=False, clip=clip)
    return draw

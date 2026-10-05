"""Where everything lives, and the design kit on the import path.

The pipeline renders with the approved Moonfall design kit (docs/design/v9/src, rich/src, rich2/src): the Medallion
night grade, the pegs and bricks, the chrome from FFXIV's UI art and the fuller-board dress primitives. It does not copy
them; it imports them, so a level built here looks exactly like the approved pilots.
"""
import pathlib
import sys

TOOL = pathlib.Path(__file__).resolve().parent.parent
REPO = TOOL.parent.parent
V9 = REPO / "docs" / "design" / "v9"
RICH = V9 / "rich"
RICH2 = V9 / "rich2"

LEVELS = V9 / "levels"                 # the level sources and outputs
LAYOUTS = LEVELS / "layouts"           # <id>.py: the layout source (LEVEL metadata and build(board))
SCENES = LEVELS / "scenes"             # <scene>.json: the scene recipe (source, grade, features, dress)
PAINTERS = LEVELS / "painters"         # <painter>.py: our own paintings, made in code
ASSETS = SCENES / "assets"             # our paintings as the plugin would ship them (JPEG, 1x and 2x)
JSON_OUT = LEVELS / "json"             # the level files (format v2), ready to ship
COMPOSITES = LEVELS / "composites"     # <id>.png (1x) and <id>@2x.png
REPORT = LEVELS / "report"             # <id>.json: every check's figures, written by a build
BUILD = LEVELS / "build"               # gitignored: graded and dressed scenes, trace sheets
SOURCES = LEVELS / "sources.json"      # every game file the scenes read

CACHE = RICH / ".cache"                # game textures dumped by texdump (gitignored), shared with the design kit
DOTNET = TOOL / "dotnet"
MFCHECK = DOTNET / "mfcheck" / "bin" / "Release" / "net10.0" / "mfcheck.dll"
TEXDUMP = DOTNET / "texdump" / "bin" / "Release" / "net10.0" / "texdump.dll"

for p in (V9 / "src", RICH / "src", RICH2 / "src"):
    if str(p) not in sys.path:
        sys.path.insert(0, str(p))


def cache_name(texture):
    """The file texdump writes for a game path: ui/loadingimage/x.tex -> ui_loadingimage_x.png."""
    return texture.replace("/", "_").replace(".tex", ".png")

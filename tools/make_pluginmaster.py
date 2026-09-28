"""Generate pluginmaster.json (Dalamud custom repository index) from the DalamudPackager manifest.

Usage (after `dotnet build Tsukimichi/Tsukimichi.csproj -c Release`):
    python tools/make_pluginmaster.py --tag v0.1.0
The tag must match a GitHub Release that has latest.zip attached.
"""
import argparse
import json
import os
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANIFEST = os.path.join(ROOT, "Tsukimichi", "bin", "Release", "Tsukimichi", "Tsukimichi.json")
OUTPUT = os.path.join(ROOT, "pluginmaster.json")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default="xenofei/Tsukimichi", help="GitHub owner/name")
    parser.add_argument("--tag", help="Release tag (default: v<Major>.<Minor>.<Build> from the manifest)")
    parser.add_argument("--branch", default="main", help="Branch that serves pluginmaster.json and the icon")
    parser.add_argument("--changelog", default="", help="Changelog text for this version")
    args = parser.parse_args()

    if not os.path.exists(MANIFEST):
        print(f"manifest not found: {MANIFEST}\nrun: dotnet build Tsukimichi/Tsukimichi.csproj -c Release", file=sys.stderr)
        return 1

    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)

    version = manifest["AssemblyVersion"]
    tag = args.tag or "v" + ".".join(version.split(".")[:3])
    raw = f"https://raw.githubusercontent.com/{args.repo}/{args.branch}"
    download = f"https://github.com/{args.repo}/releases/download/{tag}/latest.zip"

    entry = dict(manifest)
    entry["Description"] = entry.get("Description", "").replace("\r\n", "\n")
    entry.update({
        "IconUrl": f"{raw}/assets/icon.png",
        "DownloadLinkInstall": download,
        "DownloadLinkTesting": download,
        "DownloadLinkUpdate": download,
        "LastUpdate": int(time.time()),
        "DownloadCount": 0,
        "IsHide": False,
        "IsTestingExclusive": False,
    })
    if args.changelog:
        entry["Changelog"] = args.changelog

    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump([entry], f, indent=2, ensure_ascii=False)
        f.write("\n")

    print(f"wrote {OUTPUT}\n  version {version} -> {download}\n  repo url: {raw}/pluginmaster.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())

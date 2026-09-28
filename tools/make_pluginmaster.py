"""Generate pluginmaster.json (Dalamud custom repository index) from the DalamudPackager manifest.

Usage (after `dotnet build Tsukimichi/Tsukimichi.csproj -c Release`):
    python tools/make_pluginmaster.py --tag v0.1.0
The tag must match a GitHub Release that has latest.zip attached, and CHANGELOG.md must have a
`## [X.Y.Z]` section for it (or pass --changelog); otherwise the script exits 1 and writes nothing.
"""
import argparse
import json
import os
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANIFEST = os.path.join(ROOT, "Tsukimichi", "bin", "Release", "Tsukimichi", "Tsukimichi.json")
OUTPUT = os.path.join(ROOT, "pluginmaster.json")


def changelog_section(tag: str) -> str:
    """Return the CHANGELOG.md section for `tag` (vX.Y.Z -> [X.Y.Z]) as plain text, or empty."""
    path = os.path.join(ROOT, "CHANGELOG.md")
    if not os.path.exists(path):
        return ""
    version = tag[1:] if tag.startswith("v") else tag
    lines = []
    capture = False
    with open(path, encoding="utf-8") as f:
        for line in f:
            if line.startswith("## "):
                if capture:
                    break
                capture = line.startswith(f"## [{version}]")
                continue
            if capture:
                lines.append(line.rstrip())
    text = "\n".join(lines).strip()
    # Headings inside the section become plain labels; bullets stay.
    return "\n".join(l.replace("### ", "") for l in text.splitlines())


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
    changelog = args.changelog or changelog_section(tag)
    if not changelog:
        version_label = tag[1:] if tag.startswith("v") else tag
        print(
            f"CHANGELOG.md has no '## [{version_label}]' section with content for {tag}; "
            "write the release notes (or pass --changelog) before regenerating pluginmaster.json",
            file=sys.stderr,
        )
        return 1
    entry["Changelog"] = changelog

    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump([entry], f, indent=2, ensure_ascii=False)
        f.write("\n")

    print(f"wrote {OUTPUT}\n  version {version} -> {download}\n  repo url: {raw}/pluginmaster.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())

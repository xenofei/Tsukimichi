"""Generate pluginmaster.json (Dalamud custom repository index) from the DalamudPackager manifest.

Usage (after `dotnet build Tsukimichi/Tsukimichi.csproj -c Release`):
    python tools/make_pluginmaster.py --tag v0.1.0
The tag must match a GitHub Release that has latest.zip attached, and CHANGELOG.md must have a
`## [X.Y.Z]` section for it (or pass --changelog); otherwise the script exits 1 and writes nothing.

Since 1.22.0 (plan v8 U1, spec-1.22 decision 8) the manifest's changelog is the release's plain notes from
Tsukimichi/Data/whats_new.json when it has an entry for the version, so Dalamud's installer and the update note's hover
show the same words as the What's new popup; the technical CHANGELOG.md section is the fallback.
"""
import argparse
import json
import os
import sys
import time

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
_CANDIDATES = [os.path.join(ROOT, "Tsukimichi", "bin", *tail, "Tsukimichi", "Tsukimichi.json") for tail in (("Release",), ("x64", "Release"))]
MANIFEST = next((c for c in _CANDIDATES if os.path.exists(c)), _CANDIDATES[0])
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


WHATS_NEW = os.path.join(ROOT, "Tsukimichi", "Data", "whats_new.json")


def plain_notes(tag: str) -> str:
    """The release's plain points from whats_new.json as "- Lead. Sentence." lines, or empty when it has none.

    Reads the file loosely, since its other fields are the What's new popup's: a list of releases, or an object holding
    one under "releases"; each release has "version" and "points"; a point is a string, or an object with a "lead" and
    one of "text", "sentence" or "body".
    """
    if not os.path.exists(WHATS_NEW):
        return ""
    version = tag[1:] if tag.startswith("v") else tag
    with open(WHATS_NEW, encoding="utf-8-sig") as f:
        data = json.load(f)
    releases = data.get("releases", []) if isinstance(data, dict) else data
    for release in releases if isinstance(releases, list) else []:
        if not isinstance(release, dict) or str(release.get("version", "")).strip() != version:
            continue
        lines = []
        for point in release.get("points", []):
            if isinstance(point, str):
                text = point.strip()
            elif isinstance(point, dict):
                lead = str(point.get("lead", "")).strip()
                body = str(point.get("text") or point.get("sentence") or point.get("body") or "").strip()
                text = f"{lead} {body}".strip()
            else:
                text = ""
            if text:
                lines.append(f"- {text}")
        return "\n".join(lines)
    return ""


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
        "IconUrl": f"{raw}/assets/icon-medallion.png",
        "DownloadLinkInstall": download,
        "DownloadLinkTesting": download,
        "DownloadLinkUpdate": download,
        "LastUpdate": int(time.time()),
        "DownloadCount": 0,
        "IsHide": False,
        "IsTestingExclusive": False,
    })
    changelog = args.changelog or plain_notes(tag) or changelog_section(tag)
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

"""Generate pluginmaster.json (Dalamud custom repository index) from the DalamudPackager manifest.

Usage (after `dotnet build Tsukimichi/Tsukimichi.csproj -c Release`):
    python tools/make_pluginmaster.py --tag v0.1.0
The tag must match a GitHub Release that has latest.zip attached. The manifest's changelog is the release's plain
What's new notes from Tsukimichi/Data/curated/whats_new.json (spec-1.22 U1: Dalamud's installer shows the same words as
the in-game popup); a release without notes falls back to its CHANGELOG.md `## [X.Y.Z]` section, and with neither (and
no --changelog) the script exits 1 and writes nothing. CHANGELOG.md stays the technical record on GitHub.

    python tools/make_pluginmaster.py --tag v1.24.0-rc1 --testing --current pluginmaster.json
adds a test drop: only TestingAssemblyVersion, TestingDalamudApiLevel, DownloadLinkTesting and TestingChangelog are
written, onto the entry already in --current (main's pluginmaster.json); AssemblyVersion, the install and update links
and the release notes stay. A normal release run with --current keeps those testing fields only while the testing
version is still above the new AssemblyVersion (a hotfix release must not end a pending test drop); otherwise the
release leaves no testing fields and the installer sees no testing build. See docs/team/test-drops.md.

    python tools/make_pluginmaster.py --notes 1.22.0
prints the changelog text a release would get from its notes, and nothing else (exit 1 when it has none). The
plugin's tests compare it with Core's ReleaseNote.ManifestText.
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
WHATS_NEW = os.path.join(ROOT, "Tsukimichi", "Data", "curated", "whats_new.json")
# Must match ReleaseNote.ManifestBullet in Tsukimichi.Core/Releases/ReleaseNotes.cs.
BULLET = "\u2022 "


def whats_new_text(tag: str) -> str:
    """Return the release's plain notes (vX.Y.Z or X.Y.Z) as the manifest's changelog, or empty.

    The form is ReleaseNote.ManifestText: the release's name, a blank line, then one bullet line per point.
    """
    if not os.path.exists(WHATS_NEW):
        return ""
    version = tag[1:] if tag.startswith("v") else tag
    with open(WHATS_NEW, encoding="utf-8") as f:
        notes = json.load(f)
    for release in notes.get("releases", []):
        if release.get("version") != version:
            continue
        lines = [release["name"].strip(), ""]
        for point in release.get("points", []):
            lines.append(BULLET + point["lead"].strip() + " " + point["text"].strip())
        return "\n".join(lines)
    return ""


def version_tuple(text: str) -> tuple:
    """'1.24.0.1' -> (1, 24, 0, 1), ordered as System.Version orders it (a missing part sorts below 0)."""
    parts = [int(p) for p in text.split(".")]
    return tuple(parts + [-1] * (4 - len(parts)))


def read_current(path: str) -> dict:
    """The entry of the pluginmaster.json at `path`, or {} when there is none."""
    if not path or not os.path.exists(path):
        return {}
    with open(path, encoding="utf-8") as f:
        entries = json.load(f)
    return entries[0] if entries else {}


TESTING_FIELDS = ("TestingAssemblyVersion", "TestingDalamudApiLevel", "DownloadLinkTesting", "TestingChangelog")


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


def write_testing(args, manifest: dict, tag: str, version: str) -> int:
    """Add the test drop `tag` (vX.Y.Z-rcN, built as `version`) to the entry in --current; nothing else changes."""
    current = read_current(args.current)
    if not current:
        print("--testing needs --current: the pluginmaster.json on main to add the testing fields to", file=sys.stderr)
        return 1
    released = current["AssemblyVersion"]
    if version_tuple(version) <= version_tuple(released):
        # Dalamud ignores a testing version that is not above AssemblyVersion (PluginManifest.IsAvailableForTesting).
        print(f"test drop {tag} is built as {version}, not above the released {released}; Dalamud would not offer it", file=sys.stderr)
        return 1
    if "DalamudApiLevel" not in manifest:
        print("the manifest has no DalamudApiLevel; Dalamud offers testing only with a matching TestingDalamudApiLevel", file=sys.stderr)
        return 1
    notes = whats_new_text(tag.split("-", 1)[0])
    entry = dict(current)
    entry.update({
        "TestingAssemblyVersion": version,
        "TestingDalamudApiLevel": manifest["DalamudApiLevel"],
        "DownloadLinkTesting": f"https://github.com/{args.repo}/releases/download/{tag}/latest.zip",
        "TestingChangelog": f"Test build {tag} (version {version})" + (f"\n\n{notes}" if notes else ""),
    })
    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump([entry], f, indent=2, ensure_ascii=False)
        f.write("\n")
    print(f"wrote {OUTPUT}\n  testing {version} -> {entry['DownloadLinkTesting']}\n  released {released} unchanged")
    return 0


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", default="xenofei/Tsukimichi", help="GitHub owner/name")
    parser.add_argument("--tag", help="Release tag (default: v<Major>.<Minor>.<Build> from the manifest)")
    parser.add_argument("--branch", default="main", help="Branch that serves pluginmaster.json and the icon")
    parser.add_argument("--changelog", default="", help="Changelog text for this version")
    parser.add_argument("--testing", action="store_true", help="Test drop (an -rcN tag): write only the testing fields onto --current")
    parser.add_argument("--current", default="", help="The pluginmaster.json now on main (read, never written)")
    parser.add_argument("--notes", metavar="VERSION", help="Print the changelog text whats_new.json gives VERSION, and exit")
    args = parser.parse_args()

    if args.notes:
        text = whats_new_text(args.notes)
        if not text:
            print(f"whats_new.json has no release {args.notes}", file=sys.stderr)
            return 1
        sys.stdout.buffer.write(text.encode("utf-8"))
        return 0

    if not os.path.exists(MANIFEST):
        print(f"manifest not found: {MANIFEST}\nrun: dotnet build Tsukimichi/Tsukimichi.csproj -c Release", file=sys.stderr)
        return 1

    with open(MANIFEST, encoding="utf-8") as f:
        manifest = json.load(f)

    version = manifest["AssemblyVersion"]
    tag = args.tag or "v" + ".".join(version.split(".")[:3])
    if args.testing:
        return write_testing(args, manifest, tag, version)

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
    changelog = args.changelog or whats_new_text(tag)
    if not changelog:
        changelog = changelog_section(tag)
        if changelog:
            print(f"whats_new.json has no release {tag}; using its CHANGELOG.md section instead", file=sys.stderr)
    if not changelog:
        version_label = tag[1:] if tag.startswith("v") else tag
        print(
            f"Neither whats_new.json nor CHANGELOG.md ('## [{version_label}]') has notes for {tag}; "
            "write the release notes (or pass --changelog) before regenerating pluginmaster.json",
            file=sys.stderr,
        )
        return 1
    entry["Changelog"] = changelog

    # A pending test drop survives a release that is still behind it (Dalamud offers testing only above AssemblyVersion).
    current = read_current(args.current)
    pending = current.get("TestingAssemblyVersion")
    if pending and current.get("TestingDalamudApiLevel") and version_tuple(pending) > version_tuple(version):
        for key in TESTING_FIELDS:
            if key in current:
                entry[key] = current[key]
        print(f"  kept the pending test drop {pending}")

    with open(OUTPUT, "w", encoding="utf-8", newline="\n") as f:
        json.dump([entry], f, indent=2, ensure_ascii=False)
        f.write("\n")

    print(f"wrote {OUTPUT}\n  version {version} -> {download}\n  repo url: {raw}/pluginmaster.json")
    return 0


if __name__ == "__main__":
    sys.exit(main())

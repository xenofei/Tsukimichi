# Test drops (the testing channel)

A test drop sends a build to the players who opted into Dalamud's **testing versions** for Tsukimichi. Everyone else stays on the last release. Moonfall's test builds go out this way.

A pre-release tag `vX.Y.Z-rcN` (for example `v1.24.0-rc1`) makes one. A normal `vX.Y.Z` tag works as before. The workflow is `.github/workflows/release.yml`; the index script is `tools/make_pluginmaster.py`.

## Owner's one-time step (each tester does the same)
1. In game, type `/xlsettings`, open **Experimental**, turn on **Get plugin testing builds**.
2. Type `/xlplugins`, open **Installed Plugins**, right-click **Tsukimichi**, choose **Receive plugin testing versions**.

Both are needed. After a drop is published, Tsukimichi shows "(has testing version)" and the usual update button installs it. Opting out later keeps the test build until the next normal release updates it.

## Cut a test drop
1. On the commit to test, `Tsukimichi.csproj` `<Version>` is the upcoming release as `X.Y.Z.0` (for example `1.24.0.0`). The tests still need that version's `whats_new.json` entry and `CHANGELOG.md` section, and the tested game version must be set. `pwsh tools/gate.ps1` passes. Merge to `main`.
2. Tag that commit: `git tag v1.24.0-rc1`, then `git push origin v1.24.0-rc1`.
3. The workflow runs the same guards, tests and build as a release, with the build version set from the tag (below). It attaches `latest.zip` and `SHA256SUMS.txt` to a GitHub **pre-release** and commits a `chore: pluginmaster.json testing build ...` change to `main`.
4. Check: the release is marked pre-release; `pluginmaster.json` on `main` has `TestingAssemblyVersion` and `DownloadLinkTesting` for the rc while `AssemblyVersion`, `DownloadLinkInstall` and `DownloadLinkUpdate` still name the last release; in game, `/xlplugins` offers the testing version.
5. Another round: fix on `main`, tag `v1.24.0-rc2` (same csproj `<Version>`).
6. Ship: tag `v1.24.0` as usual. Testers get it as a normal update, and the testing fields are cleared.

A failed run is published again from Actions > Release > Run workflow with the tag, as for a release. A hotfix release (say `v1.23.1`) while a drop is pending keeps the drop in place: its testing fields stay as long as the testing version is above the new `AssemblyVersion`.

## Version scheme
Dalamud offers a testing build only if `TestingAssemblyVersion` is above `AssemblyVersion` (and `TestingDalamudApiLevel` equals the running Dalamud's API level), and it updates a plugin to the higher of the testing and release versions; it never moves a player down. So the build version must rise from rc to rc and on to the final release:

| Tag | Builds as | Seen by |
|---|---|---|
| `v1.23.0` (last release) | 1.23.0.0 | everyone |
| `v1.24.0-rc1` | 1.24.0.1 | testers |
| `v1.24.0-rc2` | 1.24.0.2 | testers |
| `v1.24.0` | 1.24.0.3 (one above the highest rc tag; 1.24.0.0 if there was no rc) | everyone |

The csproj stays `1.24.0.0`; the workflow passes `-p:Version=` and checks that the packaged manifest carries the expected version. The plugin's own displays use three parts (1.24.0), so a test build shows the 1.24.0 What's new.

## Limits
- Testing needs the Dalamud API level in the manifest to match the player's Dalamud. After an API bump, the next rc is built for the new level, and testers on an older Dalamud see nothing.
- An rc whose version is not above the released one (an rc of an already released X.Y.Z) is refused by `make_pluginmaster.py`.

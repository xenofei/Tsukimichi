# The portrait pack

The optional giver portrait pack (feature plan v7 F4, decision 8). Giver portraits come from the game install first (1.15, F1–F3). The pack adds Garland Tools' NPC photos for givers the game art misses. Players download it only by clicking **Download…** in Settings › General › Look › Portrait pack and confirming. The plugin never fetches it on its own. What the download does, for players, is in [privacy.md](../privacy.md#the-portrait-pack-optional-since-120).

## What it is

`Tsukimichi-portraits.zip`, attached to a GitHub release of this repository:

- `manifest.json`: format, game version, build date, image side (128 px), source, `files` (image name to SHA-256) and `entries` (ENpcResident id to image name; Garland's appearance aliases share one image).
- `portraits/<id>.png`: one 128 × 128 head crop per distinct photo, a 256-colour PNG with transparency, framed by the plugin's rule (eye line 44 %, chin 81 %).

The plugin ships `Tsukimichi/Data/portrait_pack.json`, which names the release tag, the asset name, and the zip's exact size and SHA-256. The download address is always `https://github.com/xenofei/Tsukimichi/releases/download/<tag>/<asset>`; only GitHub's redirect to its own asset hosts is followed. A download whose size or hash differs is deleted. Inside the zip, only `manifest.json` and flat `portraits/*.png` names are accepted. Every image must match its hash and decode as a 128 px PNG before anything is installed into `pluginConfigs\Tsukimichi\portraits\`. The code is in `Tsukimichi.Core/Portraits/PortraitPack*.cs`, and the tests are `Tsukimichi.Tests/Portraits/PortraitPack*Tests.cs`.

A face from the pack is used only where the game art has none for that quest: game art always wins (`PortraitSources.Rank`, `PortraitIndex.WithPack`). That includes a giver whose only game faces come from a later expansion than the quest. The pack fills in with that NPC's own photo.

## Build it

From the repository root, after `dotnet build Tsukimichi.sln -c Release`:

```powershell
dotnet run --project Tsukimichi.DataGen -c Release --no-build -- `
  --portrait-pack <out dir> `
  --game "C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game/sqpack" `
  --cache <cache dir> `
  --offer Tsukimichi/Data/portrait_pack.json --tag v1.20.0
```

- It reads every named quest giver from the install (generic "troubled adventurer" givers keep their silhouette). For each one, it fetches `https://www.garlandtools.org/db/doc/npc/en/2/<id>.json` and, if that names a photo, `https://www.garlandtools.org/files/photos/npc/Enpc_<id>.png`. Requests go one per second (`--rate`), with an identifying User-Agent. Every answer, 404s included, is cached under `--cache` (default `<out dir>/cache`), so a rerun fetches only what it lacks. The first full run makes about 4,000 requests, which takes a little over an hour.
- It writes `<out dir>/Tsukimichi-portraits.zip`, `Tsukimichi-portraits.zip.sha256` and `report.md` (coverage, plus every giver without a photo and why). It also writes `sheets/contact-NN.png`: every crop on the plate circle, with the eye and chin lines, labelled with the NPC id.
- Before it reports success, it checks the zip exactly as the plugin will (`PortraitPackArchive.Extract`).
- `--offer` and `--tag` write `portrait_pack.json` for the release the zip will be attached to. Without them, nothing in the repository changes.
- `--limit N` builds from the N busiest givers, and `--ids a,b` from just those. Use either for a quick look at the crops.
- `--skip a,b` leaves out givers whose crops missed on the contact sheets (a raised weapon or a very tall hat can fool the head finder). Rebuild after skipping. With the cache, a rebuild takes seconds.

**Never commit the zip, the sheets or the cache.** They are Square Enix art, rendered by Garland Tools (photos by Celes). Only `portrait_pack.json` is committed.

## Release it

1. Build the pack for the release's tag, as above, with `--offer Tsukimichi/Data/portrait_pack.json --tag vX.Y.Z`.
2. Look through `sheets/contact-*.png`. Rebuild with `--skip` if any crop shows something other than a face.
3. Commit `Tsukimichi/Data/portrait_pack.json` with the release.
4. Attach `<out dir>/Tsukimichi-portraits.zip` to the GitHub release `vX.Y.Z`, under exactly that asset name. Before publishing, check its SHA-256 (`Get-FileHash -Algorithm SHA256`) against `portrait_pack.json`.
5. A later release that does not rebuild the pack keeps the same `portrait_pack.json`, which still points at the older release's asset. Leave that asset in place. A release that rebuilds the pack, for example after a major patch, points at its own tag, and installed players see **Update…** in Settings. Nothing downloads until they click it.

If the asset is missing from the release, players get "The pack is not on the GitHub release (404)" and nothing is installed. If it is a different file, the hash check refuses it.

An installed pack built for an older game version keeps working. Settings says which game version it was built for, and givers added since then show game art or a fallback.

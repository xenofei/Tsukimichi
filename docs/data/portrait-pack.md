# The portrait pack

The optional giver portrait pack (feature plan v7 F4, decision 8). Giver portraits come from the game install first (1.15, F1–F3). The pack adds Garland Tools' NPC photos for givers the game art misses. Players download it only by clicking **Download…** in Settings › General › Look › Portrait pack and confirming. The plugin never fetches it on its own. What the download does, for players, is in [privacy.md](../privacy.md#the-portrait-pack-optional-since-120).

## What it is

`Tsukimichi-portraits.zip`, attached to a GitHub release of its own in this repository, `portraits-1`, `portraits-2` … ("pack 1", "pack 2" in Settings):

- `manifest.json`: format, game version, build date, image side (128 px), source, `files` (image name to SHA-256), `boxes` (image name to its head box in the photo's own pixels, so the hover never shows a face larger than its source) and `entries` (ENpcResident id to image name; Garland's appearance aliases share one image).
- `portraits/<id>.png`: one 128 × 128 head crop per distinct photo, a 256-colour PNG with transparency, framed by the plugin's rule (eye line 44 %, chin 81 %). The square is cut from at least 72 px of the photo, so the 72 px plate never upscales: a smaller face is held at 72 px on the same eye line. A frame from the head finder whose middle is mostly empty is dropped.

The plugin ships `Tsukimichi/Data/portrait_pack.json`, which names the release tag, the asset name, and the zip's exact size and SHA-256. The download address is always `https://github.com/xenofei/Tsukimichi/releases/download/<tag>/<asset>`; only GitHub's redirect to its own asset hosts is followed. A download whose size or hash differs is deleted. Inside the zip, only `manifest.json` and flat `portraits/*.png` names are accepted. Every image must match its hash and decode as a 128 px PNG before anything is installed into `pluginConfigs\Tsukimichi\portraits\`. The code is in `Tsukimichi.Core/Portraits/PortraitPack*.cs`, and the tests are `Tsukimichi.Tests/Portraits/PortraitPack*Tests.cs`.

A giver's pack photo goes first (spec-1.20 F4, the 1.15 order A1: `PortraitSources.Rank`, `PortraitIndex.WithPack`), as a photo of that exact NPC. Wherever the pack has no photo, the game art stands in as before. The spoiler shield treats a pack face like any other.

## Build it

From the repository root, after `dotnet build Tsukimichi.sln -c Release`:

```powershell
dotnet run --project Tsukimichi.DataGen -c Release --no-build -- `
  --portrait-pack <out dir> `
  --game "C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game/sqpack" `
  --cache <cache dir> `
  --offer Tsukimichi/Data/portrait_pack.json --tag portraits-N
```

- It reads every named quest giver from the install (generic "troubled adventurer" givers keep their silhouette). For each one, it fetches `https://www.garlandtools.org/db/doc/npc/en/2/<id>.json` and, if that names a photo, `https://www.garlandtools.org/files/photos/npc/Enpc_<id>.png`. Requests go one per second (`--rate`), with an identifying User-Agent. Every answer, 404s included, is cached under `--cache` (default `<out dir>/cache`), so a rerun fetches only what it lacks. The first full run makes about 4,000 requests, which takes a little over an hour.
- It writes `<out dir>/Tsukimichi-portraits.zip`, `Tsukimichi-portraits.zip.sha256` and `report.md` (coverage, framing, every giver without a photo and why, and every per-NPC box not used). It also writes `boxes.json` (each image's square in its photo, where it came from, and the head finder's own square) and `sheets/contact-NN.png`: every crop on the plate circle, with the eye and chin lines, labelled with the NPC id.
- Before it reports success, it checks the zip exactly as the plugin will (`PortraitPackArchive.Extract`).
- `--offer` and `--tag` write `portrait_pack.json` for the release the zip will be attached to. Without them, nothing in the repository changes.
- `--limit N` builds from the N busiest givers, and `--ids a,b` from just those. Use either for a quick look at the crops.
- Each photo is framed by a per-NPC box when one of its givers has one, else by the head finder (see below).
- `--overrides <file>` names the per-NPC boxes (default `tools/portrait-pack/portrait_pack_overrides.json`). A missing file, or a box outside its photo, under 72 px or disagreeing with another giver's box for the same photo, stops the build. A box for an NPC that is not a named quest giver is reported in `report.md` and skipped.
- `--skip a,b` leaves out givers whose crops missed on the contact sheets. Rebuild after skipping. With the cache, a rebuild takes seconds.
- `--built yyyy-MM-dd` sets the manifest's build date. Without it the date comes from the game version (`2026.09.15.0000.0000` is 2026-09-15), never from the clock.

### Framing

- **The head finder** (`Tsukimichi.DataGen/PortraitHeadCrop.cs`) reads only the photo's silhouette. The crown is the first row whose run of opaque pixels on the body's axis is about a third of a head wide, stays that wide for a few rows, and joins a head-wide mass just below. A raised staff, a lance beside the head, a scythe or a plume does not pass, and the head's size is measured from the crown down, not from a weapon's tip. The eyes sit a measured share of a head below the crown, by race, nudged by the neck, and the face's centre is the centroid of the head's own pixels. The race constants are medians over the 1,262 Garland photos of the portrait audit (`docs/research/portrait-audit/`). On Team C's 895 confidently measured photos, the eye line's median is 0.44 (pack 1: 0.574) and the chin's 0.80 (pack 1: 0.87).
- **Per-NPC boxes** (`tools/portrait-pack/portrait_pack_overrides.json`) map an ENpc id to `photoBox` [x, y, side] in that giver's Garland photo pixels, with a note. Givers who share a photo share the box. A per-NPC box is kept even where the head finder would drop the frame as empty: the owner keeps faceless subjects such as helmets and masks. `tools/portrait-pack/make_overrides.py` writes the file from the audit (`--check` reports a stale file):
  - the owner's own box where he adjusted one on the Portrait Desk;
  - the reconciler's box where he accepted it;
  - otherwise the reconciler's box when it changed the frame and the face-centring supervisor passed it.
  A box under 72 px is held at 72 px on the same eye line.

### Same inputs, same bytes

From the same cache, install, `--built` date and .NET runtime, a rebuild writes a byte-identical zip with the same SHA-256, on any day and on Windows or Linux. Every entry, the manifest included, is stored rather than deflated. Every entry has one fixed timestamp and no external attributes. The "version made by" platform byte, which .NET sets from the OS and offers no API for, is rewritten to 0. `PortraitPackInstallTests.The_zip_writer_makes_these_exact_bytes` pins the writer's output.

One thing is not pinned: the images. Each one is a PNG deflated by the zlib that ships inside the .NET runtime (zlib-ng since .NET 9) at its smallest-size level. Another runtime version can compress the same pixels into other bytes, and possibly so can another CPU, since zlib-ng picks its code path by CPU features. Other image bytes change their hashes in the manifest, and so the zip. Build a pack and its rebuilds with the same SDK. If a rebuild's SHA-256 differs from `portrait_pack.json`, it is a new pack: give it the next `portraits-N` tag rather than replacing the asset.

The 1.20.0 builder deflated the manifest and stamped it with the build day, so the next build differs from `portraits-1` even from the same photos. `portraits-1` and its `portrait_pack.json` stay valid as they are.

**Never commit the zip, the sheets or the cache.** They are Square Enix art, rendered by Garland Tools (photos by Celes). Only `portrait_pack.json` is committed.

## Release it

1. Build the pack, as above, with `--offer Tsukimichi/Data/portrait_pack.json --tag portraits-N` (the next pack number).
2. Look through `sheets/contact-*.png`. Fix a crop that shows something other than a face with a per-NPC box (or leave the giver out with `--skip`) and rebuild.
3. Commit `Tsukimichi/Data/portrait_pack.json` with the plugin release that pairs with the pack.
4. Create the GitHub release `portraits-N` (mark it a pre-release or not the latest, so the plugin's own release stays "Latest") and attach `<out dir>/Tsukimichi-portraits.zip` under exactly that asset name. Before publishing, check its SHA-256 (`Get-FileHash -Algorithm SHA256`) against `portrait_pack.json`.
5. A later plugin release that does not rebuild the pack keeps the same `portrait_pack.json`. Leave the `portraits-N` release and its asset in place for good. A rebuilt pack, for example after a major patch, goes to `portraits-N+1`, and installed players see "Pack N+1 is ready to download" with **Update…** in Settings. Nothing downloads until they click it; the plugin never checks GitHub for a newer pack.

If the asset is missing from the release, players get "The pack is not on the GitHub release (404)" and nothing is installed. If it is a different file, the hash check refuses it.

An installed pack built for an older game version keeps working. Settings says which game version it was built for, and givers added since then show game art or a fallback.

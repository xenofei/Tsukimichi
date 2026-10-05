# The portrait pack

The giver photos (feature plan v7 F4). Giver portraits come from the game install first (1.15, F1–F3). The pack adds Garland Tools' NPC photos for givers the game art misses. Since the release after 1.22 the pack ships inside the plugin; from 1.20 to 1.22 it was an optional download from a GitHub release of its own (`portraits-1`). Settings › General › Look › Giver portraits shows the photos under **Game art + photos** (the default) and leaves them out under **Game art**. What players see of it is in [privacy.md](../privacy.md#the-giver-photos-in-the-plugin-since-the-release-after-122).

## What it is

`Tsukimichi/assets/portraits/`, packaged with the plugin by `Tsukimichi.csproj` like the theme atlases and the What's new pictures:

- `manifest.json`: format, game version, build date, image side (128 px), source, `files` (image name to SHA-256), `boxes` (image name to its head box in the photo's own pixels, so the hover never shows a face larger than its source) and `entries` (ENpcResident id to image name; Garland's appearance aliases share one image).
- `<id>.png`: one 128 × 128 head crop per distinct photo, a 256-colour PNG with transparency, framed by the plugin's rule (eye line 44 %, chin 81 %). The square is cut from at least 72 px of the photo, so the 72 px plate never upscales: a smaller face is held at 72 px on the same eye line. A frame from the head finder whose middle is mostly empty is dropped.

At start the plugin reads the manifest on a worker and checks that every image it lists is there and not empty (`Tsukimichi.Core/Portraits/BundledPortraits.cs`, `Tsukimichi/Game/BundledPortraitLoader.cs`). A missing or incomplete folder is logged, and givers then show game art and fallbacks. The hashes and the decode of every image are checked when the pack is built, and again by `BundledPortraitsTests.Every_bundled_image_matches_its_hash_and_decodes_at_the_packs_side`. `.gitattributes` keeps the folder byte for byte as the builder wrote it.

The same first start deletes the pack 1.20–1.22 downloaded into `pluginConfigs\Tsukimichi\portraits\`: its `current.json`, the pack folders named by a hash (and their staging and set-aside copies) and unfinished `download-*.part` files, then the folder if nothing else is in it (`BundledPortraits.RemoveDownloaded`). A configuration saved on Game art, the old default, moves once to Game art + photos, unless the player had the pack downloaded and still chose Game art (`BundledPortraits.ModeOnceBundled`).

How a photo ranks against the game's art is `PortraitSources.Rank` and `PortraitIndex` (`WithPack`). The spoiler shield treats a photo like any other face.

## Build it

From the repository root, after `dotnet build Tsukimichi.sln -c Release`:

```powershell
dotnet run --project Tsukimichi.DataGen -c Release --no-build -- `
  --portrait-pack <out dir> `
  --game "C:/Program Files (x86)/Steam/steamapps/common/FINAL FANTASY XIV Online/game/sqpack" `
  --cache <cache dir> `
  --bundle Tsukimichi/assets/portraits
```

- It reads every named quest giver from the install (generic "troubled adventurer" givers keep their silhouette). For each one, it fetches `https://www.garlandtools.org/db/doc/npc/en/2/<id>.json` and, if that names a photo, `https://www.garlandtools.org/files/photos/npc/Enpc_<id>.png`. Requests go one per second (`--rate`), with an identifying User-Agent. Every answer, 404s included, is cached under `--cache` (default `<out dir>/cache`), so a rerun fetches only what it lacks. The first full run makes about 4,000 requests, which takes a little over an hour. This is the builder, run by a maintainer; the plugin itself never goes online.
- It writes `<out dir>/Tsukimichi-portraits.zip`, `Tsukimichi-portraits.zip.sha256` and `report.md` (coverage, framing, every giver without a photo and why, and every per-NPC box not used). It also writes `boxes.json` (each image's square in its photo, where it came from, and the head finder's own square) and `sheets/contact-NN.png`: every crop on the plate circle, with the eye and chin lines, labelled with the NPC id.
- It checks the zip (`PortraitPackArchive.Extract`: only the manifest and flat image names, every image matching its hash and decoding). With `--bundle <dir>` it then replaces the manifest and PNGs in that folder with exactly what passed, and reads the folder back as the plugin does.
- `--limit N` builds from the N busiest givers, and `--ids a,b` from just those. Use either for a quick look at the crops; `--bundle` refuses both, so a partial pack never ships.
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

From the same cache, install, `--built` date and .NET runtime, a rebuild writes a byte-identical zip, and so the same images and manifest, on any day and on Windows or Linux. Every zip entry, the manifest included, is stored rather than deflated. Every entry has one fixed timestamp and no external attributes. The "version made by" platform byte, which .NET sets from the OS and offers no API for, is rewritten to 0. `PortraitPackArchiveTests.The_zip_writer_makes_these_exact_bytes` pins the writer's output.

One thing is not pinned: the images. Each one is a PNG deflated by the zlib that ships inside the .NET runtime (zlib-ng since .NET 9) at its smallest-size level. Another runtime version can compress the same pixels into other bytes, and possibly so can another CPU, since zlib-ng picks its code path by CPU features. A rebuild on another SDK can therefore change every image's bytes (and the manifest's hashes) without changing a pixel: commit such a rebuild only with a real change.

**Never commit the zip, the sheets or the cache.** The images and manifest in `Tsukimichi/assets/portraits/` are committed and ship with the plugin: Square Enix's art, rendered by Garland Tools (photos by Celes), credited in Settings › Look and in the portrait tooltip.

## Update it

1. Build the pack, as above, with `--bundle Tsukimichi/assets/portraits`.
2. Look through `sheets/contact-*.png`. Fix a crop that shows something other than a face with a per-NPC box (or leave the giver out with `--skip`) and rebuild.
3. Commit `Tsukimichi/assets/portraits/` with the plugin release that ships it, and note it in the changelog.

A pack built for an older game version keeps working; givers added since then show game art or a fallback until the next build.

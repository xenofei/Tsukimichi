# Tsukimichi feature plan v8: welcome home

Status: **draft for your review (4 October 2026).** Plan v7 shipped 1.14.0 to 1.20.0; 1.21.0 is built and being merged. This plan adds one release, 1.22.0, and a companion add-on for Umbra.

## Sources

- **Your request of 4 October 2026:**
  - move the changelog off the main screen;
  - show a popup of what was installed after an update, with custom art per release in your theme;
  - check for new versions and offer the update;
  - official Umbra support across Tsukimichi;
  - a moon icon you can show, hide, move and lock on screen, with theme particle and hover effects.
- **Your answers the same day:**
  - The popup appears after the update has installed. Its notes are simple and to the point, not technical.
  - Past popups can be viewed in Settings.
  - The update check is on by default.
  - The icon sits on your screen, can be hidden, shown, moved anywhere and locked, and has theme particle and hover effects.
  - Umbra support covers all of Tsukimichi.
- **Research:** `docs/research/plan-v8/umbra-and-updates.md`. It covers Umbra's extension model, Dalamud's update API and custom cursors, with sources.

## What the research changed

- **The update check needs no network from Tsukimichi.** Dalamud 15 gives every plugin `CheckForUpdateAsync()`, which reads the repository data Dalamud already refreshes about every ten minutes. It can also open its installer on the "Can be updated" page. Tsukimichi keeps its promise that it contacts nothing by itself, and the no-network test stays as it is.
- **Umbra has no plugin-to-plugin API.** Umbra shows Dalamud's server info bar entries in its own toolbar. Beyond that, it loads "custom plugins": separate files that add native Umbra toolbar widgets and popups. So "Umbra support" is two things:
  1. a server info bar entry, which works with or without Umbra;
  2. a **Tsukimichi for Umbra** add-on with native widgets, which talks to Tsukimichi over its own IPC.
- **Licences.** Umbra is AGPL-3.0 and Tsukimichi is MIT. An add-on built against Umbra's files has to be AGPL itself, and Umbra installs an add-on from a repository's latest release. So the add-on lives in its own repository (decision 3). Tsukimichi stays MIT.

## Standing rules

All of plan v7's rules carry over:
- player value first;
- English only;
- art from anywhere when it fits;
- a realism supervisor reviews every concept, UI and UX art;
- automation only behind explicit buttons;
- Lifestream-only teleport;
- no contact with other plugin developers;
- every plan gets a website;
- the window never moves under the player;
- show what is left, not tallies;
- no decorative glyphs that carry no meaning;
- Reduce motion and all three Decoration levels everywhere.

## 1.22.0 · Welcome home

| Id | Item | Effort |
|---|---|---|
| W1 | **What's new, after an update.** Once Dalamud has installed a newer Tsukimichi, a popup shows what's new. It shows once per update and never on a first install. It has 3 to 5 plain points per release, for players, not developers. Notes come from a new curated `whats_new.json`, written with each release and separate from the technical CHANGELOG. If several releases arrived at once, they appear as pages with ‹ ›. | M |
| W2 | **Art for each release, in your theme.** Each release gets its own illustration, shown in the popup in your current theme: Medallion, Classic, Ishgard Glass, Aether Crystal, Astrologian's Orrery or Sumi to Kinpaku. It uses that theme's frame, palette and motifs (decision 1). The art loads only while the popup is open and is released afterwards, inside the 12 MB texture budget. | L |
| W3 | **Past releases in Settings.** Settings › About gets "What's new": a list of releases, newest first, each opening its popup page. It is backfilled for 1.14.0 to 1.21.0 (decision 2). | S |
| W4 | **The changelog leaves the main screen.** The "What's new" notice card in the main window goes away. The popup and Settings replace it. | S |
| U1 | **New version available.** Tsukimichi asks Dalamud at login and every few hours (on by default; Settings switch). When a newer version is waiting:<br>• a quiet status-bar note and a dot on the moon icon say "Tsukimichi 1.23.0 is ready";<br>• **Update** opens Dalamud's installer on the update;<br>• the note shows the new version's simple notes when Dalamud has them.<br>Dalamud does the install; Tsukimichi never downloads itself. | S |
| H1 | **The moon icon.** A small moon on your screen:<br>• **Show or hide** it from Settings, its right-click menu or `/tsuki icon`.<br>• **Move** it anywhere by dragging, and **Lock** it so it can't move.<br>• **Hover** shows a quick card: Up next, Ready count, journal room, events ending soon.<br>• **Click** opens or closes Tsukimichi. **Right-click** offers Lock or Unlock, Hide, Tonight and Settings.<br>• It can hide in cutscenes, Group Pose and duties (your choice).<br>• Its place is saved per screen size, so it never lands off screen. | M |
| H2 | **Theme particles and hover effects for the icon.** Each theme has its own:<br>• Medallion: drifting gold motes.<br>• Ishgard Glass: frost glints on the rim.<br>• Aether Crystal: shards that catch the light.<br>• Orrery: a dot in orbit.<br>• Sumi to Kinpaku: gold-leaf flecks.<br>• Classic: a few stars.<br>Hover adds a soft rise and glow. The effects are subtle and never busy; they are off under Reduce motion and plain at Plain. A dot appears for "update ready" and for "needs you". | M |
| M1 | **Server info bar entry.** "◑ 12 Ready" (the moon in your theme's phase colour) shows in the game's server info bar and in Umbra's server info widget:<br>• hover gives the same quick card;<br>• click opens Tsukimichi;<br>• right-click opens Tonight.<br>On by default when Umbra is installed and the add-on isn't; your choice otherwise. | S |
| M2 | **IPC for the add-on.** Read-only, versioned calls: the summary (Up next, Ready count, journal, events, story meter), and "open Tsukimichi at …" actions. It is documented in `docs/ipc.md`. Automation rules are unchanged: anything that starts a run still needs your click inside Tsukimichi. | S |
| M3 | **Optimized for Umbra.** When Umbra is installed:<br>• the moon icon, the Todo overlay and the Needs-you panel keep clear of Umbra's toolbar (top or bottom);<br>• Settings › About shows the Umbra status and how to add the add-on;<br>• an optional **Follow Umbra** palette matches Umbra's colours, if Umbra's saved theme can be read (decision 4). | M |

## Tsukimichi for Umbra 1.0 (companion add-on, its own repository)

| Id | Item | Effort |
|---|---|---|
| A1 | **The Tsukimichi widget.**<br>• The bar shows a moon and the Ready count.<br>• Its popup, built with Umbra's own controls so it looks native in every Umbra theme, has:<br>&nbsp;&nbsp;– Up next with Go;<br>&nbsp;&nbsp;– Tonight's Ready quests;<br>&nbsp;&nbsp;– journal room;<br>&nbsp;&nbsp;– events ending soon;<br>&nbsp;&nbsp;– Open Tsukimichi, Route and Settings. | M |
| A2 | **Small widgets:**<br>• "Up next" (text);<br>• "Journal 27/30";<br>• "Story meter";<br>• "Next reset".<br>Each has Umbra's usual options: icon, text, colour and click action. | S |
| A3 | **Install and updates.**<br>• Released on its own repository's GitHub releases, which Umbra checks itself.<br>• The add-on says clearly when Tsukimichi isn't installed or is too old.<br>• Umbra needs "custom plugins" turned on; Tsukimichi's Settings explains this in one line. | S |

## Decisions for you

| # | Question | My recommendation |
|---|---|---|
| 1 | Release art: one illustration per release, shown in each theme's own frame, palette and motifs, or a fully separate painting per theme for every release (six per release)? | One illustration per release, composed so each theme restyles it: its frame kit, palette grade and motif layer (frost, gold leaf, orbits). It is six times less art to make and keep consistent, and each theme still looks like its own. |
| 2 | Backfill popups and art for 1.14.0 to 1.21.0? | Yes, with simple notes, so Settings › What's new starts with a full history. |
| 3 | The Umbra add-on lives in a new public repository, `xenofei/Tsukimichi.Umbra` (AGPL-3.0, as Umbra requires), released separately. Creating it is a public step, so I need your OK. | Yes. |
| 4 | "Follow Umbra" palette: Umbra has no theme API, so Tsukimichi would read Umbra's saved settings file (read-only) and fall back to Night if it can't. | Yes, as an option, off by default. |
| 5 | Should the moon icon show by default on a fresh install or update? | Yes, once, with a one-line hint ("Right-click for options"). Hide it any time. |
| 6 | When an update is waiting, should Tsukimichi also print a chat line? | No. The status bar and icon dot are enough; a chat line is an opt-in. |
| 7 | Release order: 1.22.0 after 1.21.0, with the add-on released alongside 1.22.0. | As written. |

## Not doing

- **Tsukimichi updating itself.** Dalamud installs updates; Tsukimichi only tells you and opens the installer.
- **Contacting Umbra's developers or proposing changes to Umbra.** This is your standing rule. Everything here uses what Umbra already offers.
- **A custom mouse cursor.** Your answer described an on-screen icon, not a cursor.

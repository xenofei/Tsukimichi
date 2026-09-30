# Branching main scenario (P14)

Date: 2026-09-30
Status: implemented for 1.0.0 (feature plan v3 §3, P14). Must hold before the 8.0 pre-patch (January 2027).
Code: `Tsukimichi.Core/Query/MsqGraph.cs`, `MsqProgress.cs` (the `MsqPosition` record), `SpoilerMask.cs`, `Tsukimichi.Core/Ui/MsqText.cs`.
Tests: `MsqGraphFixtureTests` (today's data), `MsqBranchTests` and `MsqBranchConsumerTests` (the synthetic Evercold fixture, `MsqBranchFixture`).

## Why

Until 0.9.0 the main scenario position was one quest: the first main scenario quest (journal sections 0 and 1) in journal order that is neither completed nor on a branch the character did not take. Evercold (8.0) was announced with a three-route main scenario that splits after a shared quest and meets again later. A single journal-order line would name only the first route's quest, count the other routes as "not done yet" in no particular order, and put the reconvergence quest in line as if it could be taken. Every surface that shows the position (status bar pill, Characters dashboard, Tonight card, Todo overlay, IPC, spoiler shield, unlock route) would read wrong on release week.

## How routes are detected

`MsqGraph` builds the main scenario as a graph, once per catalog (cached with it):

- **Nodes**: every main scenario quest not removed from the game, sections 0 then 1, journal order.
- **Edges**: `PreviousQuests` entries that are themselves main scenario quests (side-quest prerequisites are ignored). Nodes are put in a topological order, journal order between equals; today no quest needs a later one, so that order is the journal order.
- **Branch region**: a quest with two or more main scenario successors, after dropping a successor that another successor already leads to (a shortcut edge, not a route). The **reconvergence quest** is the earliest quest every remaining successor leads to; no such quest, no region.
- **Routes**: for each successor, every quest reachable from it that also leads to the reconvergence quest (the reconvergence quest itself excluded). Routes that share a quest before the reconvergence merge into one. Routes are ordered by where their first quest sits in the journal and named by that quest ("route In Search of Alphinaud").
- **Shared prerequisite sets**: two or more quests needing the same set of main scenario quests are found the same way (each quest of the set has them all as successors); a region found from several start quests is kept once.
- **Join kind**: the reconvergence quest's own `PreviousQuests.Join`. All needs every route; Any needs one.
- **Nesting**: a region inside a route of a larger region belongs to that route (its quests count in the route's n of m); only the outer region is reported.

### Routed regions and today's data

Only regions whose routes start in expansion 6 (Evercold) or later are reported route by route (`MsqGraph.RoutedFromExpansion`). Today's catalog already contains fourteen regions; all are detected and all are walked in journal order exactly as in 0.9.0:

| Expansion | Start | Routes (quests) | Meets at | Join |
|---|---|---|---|---|
| ARR | A Hero in the Making | the three Grand Company lines (2 each; two end up locked out) | Sylph-management | Any |
| ARR | Ziz Is So Ridiculous | Rock of Rancor (1), Seeing Eye to Winged Eye (1) | Power of Deduction | All |
| ARR | Getting Even with Garlemald | Drowning Out the Voices (1), Acting the Part (2) | Fool Me Twice | All |
| ARR | Administrative Decision | An Unexpected Ambition (2), Where We Are Needed (2) | A Time to Every Purpose | All |
| HW | The Better Half | Over the Wall (7), Onwards and Upwards (7) | Divine Intervention | All |
| HW | Mountaintop Diplomacy | the three moogle trials (1 each) | Moglin's Judgment | All |
| SB | A Bargain Struck | A Friend of a Friend in Need (4), A Familiar Face Forgotten (8, with a nested two-quest fork) | Where Men Go as One | All |
| SB | Where Men Go as One | three errands (1 each) | Token of Faith | All |
| SB | Confederate Consternation | three errands (1 each) | Alisaie's Stones | All |
| ShB | Travelers of Norvrandt | In Search of Alphinaud (9), In Search of Alisaie (9) | The Lightwardens | All |
| ShB | Il Mheg, the Faerie Kingdom | three errands (1 each) | Unto the Truth | All |
| EW | Old Sharlayan, New to You | Hitting the Books (9), For Thavnair Bound (9) | In the Dark of the Tower | All |
| DT | The Rite of Succession | To Kozama'uka (6), To Urqopacha (7) | The Success of Others | All |

(The Stormblood nested fork, The Prodigal Daughter → Hard Country / Death by a Thousand Rocks → A Life More Ordinary, is the fourteenth.)

The gate keeps 1.0.0 a no-change release for every character playing today: the brief asked for exactly today's single position on today's data, and `MsqGraphFixtureTests` proves it over every completion prefix of the catalog and 300 random state sets against the 0.9.0 algorithm (kept verbatim in the test). Lowering the gate is a one-line change once the route display has been seen in game (see the preview below).

## The position

`MsqProgress.Compute` keeps its signature and delegates to the graph. The story is walked in journal order as before; the first quest neither completed nor locked out is found.

- **Linear stretch** (that quest is not in a routed region, or the region's join is already met): the position is that quest alone. `MsqPosition.Next`, `State`, `Done`, `Total` mean what they meant in 0.9.0; `Routes` is empty.
- **Inside a routed region** (that quest is on one of its routes, or is the reconvergence quest while the join is unmet): `Routes` holds every route in route order with its next quest and state, done and total counts and a status (not started, in progress, done, locked out). `Positions` holds each open route's next quest. `Next` (the primary position, what `GetMsqPosition` and every click select) is the first route not done. `RoutesToJoin` says how many more routes the reconvergence quest needs.
- **The reconvergence quest** is never a position while its join is unmet. The join is met as soon as the reconvergence quest itself is Ready (or ready on another job), in the journal or completed: the evaluator has judged its previous quests the way the game does, and a route that holds a quest the game does not need (one side of an Any fork inside the route) would otherwise never read done. Failing that, an All join is met when every route that is not locked out is done; an Any join when one route is, or when every route is locked out (nothing is left to wait for). After the join is met the routes' unfinished quests are optional: they leave the position and the totals, as a branch not taken does. A branched position always has at least one route position, so `Next` is always `Positions[0]`.
- **A route locked out** (every quest foreclosed, as the Grand Company lines not taken are) neither counts nor blocks.
- **Order independence**: the counts depend only on what is done, never on the order routes were done in (all six orders are tested).

### Spoiler shield

"N ahead" counts per route inside a routed region: a route quest is as far ahead as its distance along its own route from that route's next quest. The reconvergence quest is as far ahead as the quests still needed before it (the sum over the open routes for an All join, the shortest open route for an Any join), and the story after it counts on from there. Every route's first quest is a position, so a route's name is never masked; the reconvergence quest's name usually is. On a linear stretch the mask is the 0.9.0 mask.

### Consumers

| Surface | Linear stretch | Inside a routed region |
|---|---|---|
| Status bar pill | `MSQ · <quest> ›` | `MSQ · route A 3/9 · route B — · route C done`; the tooltip lists each route with its next quest and says where and when the routes meet |
| Characters dashboard | `MSQ: <expansion> · next: <quest> (<giver>)` | `MSQ: <expansion> · route A 3 of 9 · route B not started · route C done` (`CharactersPane.DrawMsqLine`) |
| Tonight card, Todo overlay | one Main scenario row | one row per open route, hint led by `route A · 3 of 9` |
| IPC | `GetMsqPosition` = the quest; `GetMsqPositions` = `[quest]` | `GetMsqPosition` = the first route's next quest; `GetMsqPositions` = each open route's next quest |
| Unlock route | level order | a route is finished before the next begins (route quests take their route's entry level, then route order); the category milestone falls after every route |
| Diagnostic block | `msq <id>` | `msq <id> route <first id> n/m …` |

## What changes when 8.0 data lands

Nothing needs switching on: the catalog rebuild picks up the Evercold quests with expansion 6, and any region they form is routed. What can go wrong is the data not having the shape assumed here:

1. **Journal section.** Main scenario quests are read from sections 0 and 1 (`MsqProgress.MainScenarioSections`, `FeaturePresets.IsMainScenario`). If Evercold's main scenario arrives in a new section, add its id to both, or no Evercold quest is main scenario at all.
2. **How the routes are wired.** Detection needs the three routes' first quests to name the shared quest in `PreviousQuests`, and the reconvergence quest to name the routes' last quests. If instead the routes are gated by something else (an accept condition, a level, a quest the player picks up in a hub with no prerequisite link), no region is found and the position falls back to journal order: harmless, but not routed.
3. **Exclusive routes.** If choosing a route locks the others out (`QuestLocks`, as the Grand Company choice does), the routes' first quests form a choice group (`PathIndex`, feature plan v4 D1): once one is done the others and everything that follows only from them read Locked out on another path, become locked-out routes and drop out; the display then shows the one route taken. Before the choice the options not presumed are spare alternatives, out of the totals, so the region counts one route.
4. **Join kind.** If the reconvergence quest is an Any join, one route opens it and the others become optional; confirm in game that the story really lets you skip them.

## What to verify on 8.0 day

- Regenerate the catalog fixture. `MsqGraphFixtureTests.Todays_branch_regions_are_found_but_walked_in_journal_order` asserts no region is routed; it fails on the 8.0 fixture by design. Replace that assertion with the Evercold region: its start quest, its reconvergence quest, three routes, their first quests and lengths, the join kind.
- Check the Evercold quests' journal section (item 1 above) before anything else.
- `MsqGraph.For(catalog).Branches` for expansion 6: exactly the announced routes, no spurious region from a two-quest errand pair (those are harmless but show as routes; if they read badly, raise the minimum route length for routing rather than changing detection).
- In game, on a character at the shared quest: the pill reads `MSQ · route A — · route B — · route C —`; complete one quest of route B and it reads `route B 1/n`; the dashboard line, the Tonight card rows and the Todo overlay agree; the reconvergence quest stays masked and absent until every route is done; `GetMsqPositions` returns three ids and `GetMsqPosition` the first.
- The unlock route to the first quest after the reconvergence lists the routes one after another.

## Preview on today's data

`MsqGraph.Build(catalog, routedFromExpansion: 0)` routes every region above; `MsqGraphFixtureTests.Previewing_todays_regions_as_routes_reports_the_shadowbringers_lines` shows the Alphinaud and Alisaie lines as `3/9` and `—`. To see the display in game before 8.0, set `RoutedFromExpansion` to 3 in a local build and view a character inside Shadowbringers' Norvrandt lines, Endwalker's Sharlayan and Thavnair lines or Dawntrail's Kozama'uka and Urqopacha lines.

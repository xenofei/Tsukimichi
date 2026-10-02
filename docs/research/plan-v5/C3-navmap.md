# C3 Navigation/map — summary
Only vnavmesh worth wiring. Mappy/ChatCoordinates/MapPartyAssist: no IPC. Splatoon: reflection via ECommons (avoid). Pictomancy AGPL (avoid).
vnavmesh gates: Nav.IsReady, Nav.BuildProgress, SimpleMove.PathfindAndMoveCloseTo(Vector3,bool fly,float range) [right one], PathfindInProgress, Path.IsRunning, Path.Stop, Nav.Pathfind -> Task<List<Vector3>>. Stable names since 2024.
Game: AgentMap.AddMapMarker/AddMiniMapMarker (132 cap; re-add on refresh; Mappy draws them). IGameGui.WorldToScreen, INamePlateGui.
Proposals:
A Walk to giver via vnavmesh, button becomes Stop (Must,M) — moves character, explicit button
B Go to giver: teleport then walk chain (Should,M) — owner decide one click vs two
C Flag next stop for routes/blues/pins (Must,S, no dep)
D In-world giver highlight via WorldToScreen + edge arrow; nameplate ☾ (Should,S-M, opt-in)
E Map pins for Ready/route givers via AgentMap (Could,L; patch risk)
F Walking-distance hint via Nav.Pathfind on hover (Could,S)
Qs: chain teleport+walk in one click?; Walk in Todo overlay?; recommend vnavmesh link when missing?; ClientStructs map pins ok?; stop on movement key?; highlight default on/off?
Files: LifestreamIpc.cs pattern, GameLinks.cs, Core/Model/Issuer.cs, DutyFinderHint.cs.

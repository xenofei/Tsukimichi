# C2 Travel (Lifestream, Teleporter, vnavmesh) — summary
Used: Lifestream.Teleport(id,0), IsBusy (LifestreamIpc.cs). AetheryteIndex skips aethernet shards.
Gaps: no attunement check (dead clicks, silent fail in combat); city givers land at main aetheryte; no gil/favourite; Lifestream-only; special zones (Firmament/Island/Occult/Cosmic) likely no aetheryte; Route window has no Teleport.
Proposals:
A Attunement-aware Teleport via IAetheryteList (Must,S)
B Teleporter `Teleport(uint,byte)` fallback, then native Telepo.Teleport (Should,S; native = sig risk)
C Aethernet hop to shard nearest giver via Lifestream AethernetTeleportById + GetActiveAetheryte (Should,M)
D "Already here" hint (Should,S)
E Special-zone destinations via Lifestream ExecuteCommand (Could,S; /li island drives dialogue)
F Teleport in routes + "Next stop", group steps by aetheryte, Todo sort by aetheryte (Should,M)
G Nearby sorted by distance, shard names (Could,S)
H vnavmesh walk to NPC (Could, owner call, contradicts README)
I world/DC/housing — Won't
Questions: native Telepo fallback ok?; aethernet auto-walk few steps ok?; vnavmesh off the table?; /li island?; Todo sort toggle vs replace; gil cost always or non-favourite only.

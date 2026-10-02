# C4 Inventory (Allagan Tools etc.) — summary
Tsukimichi has NO hand-in item model. QuestParams RITEM0..n hold hand-in items (632 quests; e.g. 65677 A Carpenter in Need). QuestScriptDuties.cs:64 already reads QuestParams. RewardUnlockReader.cs:176 returns unknown for Item/OptionalItem/ArtifactGear -> every Moonlit gear row is "unknown".
Allagan Tools (InventoryTools) IPC: IsInitialized, ItemCountOwned(uint,bool,uint[]), ItemCountOwnedByCategory(uint,bool,uint[],bool) [15.0.12, 2026-08-31; incl Armoire=11, GlamourChest=10], GetItemCountsByCharacter, ItemCount/ItemCountHQ, AddNewCraftList(name, dict)->key, messages ItemAdded/ItemRemoved/RetainerChanged.
ItemVendorLocation.OpenVendorResults(uint). GetItemVendors has y=MapX bug.
Artisan: no create-list gate; Teamcraft-format text import "3x Item Name". Teamcraft URL ffxivteamcraft.com/import/<base64 "itemId,null,qty;...">.
Proposals:
1 Moonlit gear ownership via Allagan Tools (Must,S)
2 Hand-in items section in detail pane (RITEM extraction; own bags via game; retainers via AT) (Must,M; verify RITEM semantics; no qty/HQ)
3 "Needed for quest" item tooltip/context menu (Should,S)
4 Copy missing items as Teamcraft link / Artisan text (Should,S)
5 Send to Allagan Tools craft list (Could,S)
6 Vendors… via ItemVendorLocation (Could,S)
7 Gear owned on other characters via GetItemCountsByCharacter (Could,M)
8 Don't build Artisan CraftItem/StartListById
Qs: AT soft dep ok?; qty/HQ source; spoiler shield for hand-in names; owning gear = obtained?; Teamcraft link ok?; reuse one AT list?

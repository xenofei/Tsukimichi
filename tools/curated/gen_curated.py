import json, os, sys
OUT = r"C:\Users\devon\Desktop\Tsukimichi (Main Repo)\Tsukimichi\Data\curated"

S = {}
def s(qid, label, note): S[str(qid)] = {"label": label, "kind": "system", "note": note}
# Retainers
s(66968, "Retainers", "An Ill-conceived Venture (Gridania variant, SubCts811); unlocks retainer hire. Not derivable from static sheets.")
s(66969, "Retainers", "An Ill-conceived Venture (Limsa Lominsa variant, SubCts812); unlocks retainer hire.")
s(66970, "Retainers", "An Ill-conceived Venture (Ul'dah variant, SubCts813); unlocks retainer hire.")
# Chocobo companion
s(66236, "Chocobo companion", "My Little Chocobo (Twin Adder); grants the Chocobo Issuance and first mount.")
s(66237, "Chocobo companion", "My Little Chocobo (Maelstrom); grants the Chocobo Issuance and first mount.")
s(66238, "Chocobo companion", "My Little Chocobo (Immortal Flames); grants the Chocobo Issuance and first mount.")
# Grand Company
s(66216, "Grand Company enrollment", "The Company You Keep (Twin Adder); enrolls in a Grand Company, prerequisite for FC creation, seals, and GC content.")
s(66217, "Grand Company enrollment", "The Company You Keep (Maelstrom); enrolls in a Grand Company.")
s(66218, "Grand Company enrollment", "The Company You Keep (Immortal Flames); enrolls in a Grand Company.")
# Gold Saucer and sub-features
s(65970, "Gold Saucer", "It Could Happen to You; unlocks the Manderville Gold Saucer.")
s(65973, "Triple Triad", "Triple Triad Trial; unlocks Triple Triad play and card collection.")
s(65972, "Chocobo racing", "So You Want to Be a Jockey; unlocks Chocobo Square racing.")
# Glamour / appearance
s(68553, "Glamour (cast/dispel)", "If I Had a Glamour; unlocks glamour prisms, glamour plates and the Glamour Dresser.")
s(66957, "Glamour (Mor Dhona legacy)", "A Self-improving Man; original 2.x glamour unlock, retained for characters that completed it before the rework.")
s(66746, "Aesthetician", "Beauty Is Only Scalp Deep; unlocks the Aesthetician (Crystal Bell).")
# Materia
s(66174, "Materia extraction", "Forging the Spirit; unlocks materia extraction.")
s(66175, "Materia melding", "Waking the Spirit; unlocks materia melding.")
# Desynthesis
s(65688, "Desynthesis", "Gone to Pieces; unlocks desynthesis for crafting classes.")
# Flying
s(67133, "Flying (Heavensward entry)", "Divine Intervention; rewards Black Chocobo Whistle (first flying mount) and Aether Compass (QuestRewardOther 5). Per-zone flight in HW is aether-current gated; SB/ShB/EW/DT zones are aether-current only with no gating quest (AetherCurrent.Quest is static).")
# Wondrous Tails / Faux Hollows
s(67928, "Wondrous Tails", "Keeping Up with the Aliapohs; unlocks Wondrous Tails journals from Khloe Aliapoh.")
s(69501, "Faux Hollows", "Fantastic Mr. Faux; unlocks Faux Hollows (Painfully Ishgardian Man, Idyllshire).")
# Custom deliveries (SatisfactionNpc.QuestRequired verified)
s(67087, "Custom deliveries: Zhloe Aliapoh", "Arms Wide Open; SatisfactionNpc 1 QuestRequired. Also the custom delivery system unlock.")
s(68541, "Custom deliveries: M'naago", "None Forgotten, None Forsaken; SatisfactionNpc 2 QuestRequired.")
s(68675, "Custom deliveries: Kurenai", "The Seaweed Is Always Greener; SatisfactionNpc 3 QuestRequired.")
s(68713, "Custom deliveries: Adkiragh", "Between a Rock and the Hard Place; SatisfactionNpc 4 QuestRequired.")
s(69265, "Custom deliveries: Kai-Shirr", "Oh, Beehive Yourself; SatisfactionNpc 5 QuestRequired.")
s(69425, "Custom deliveries: Ehll Tou", "O Crafter, My Crafter; SatisfactionNpc 6 QuestRequired.")
s(69615, "Custom deliveries: Charlemend", "You Can Count on It; SatisfactionNpc 7 QuestRequired.")
s(70059, "Custom deliveries: Ameliance", "Of Mothers and Merchants; SatisfactionNpc 8 QuestRequired.")
s(70251, "Custom deliveries: Anden", "That's So Anden; SatisfactionNpc 9 QuestRequired.")
s(70351, "Custom deliveries: Margrat", "A Request of One's Own; SatisfactionNpc 10 QuestRequired.")
s(70775, "Custom deliveries: Nitowikwe", "Laying New Tracks; SatisfactionNpc 11 QuestRequired.")
s(70996, "Custom deliveries: Tiisol Ja", "Taco Time; SatisfactionNpc 12 QuestRequired.")
# Restoration / reconstruction / sanctuary
s(68622, "Doman Enclave reconstruction", "Precious Reclamation (StmBdy501, first quest of the Doman Enclave chain); unlocks the Enclave donation system.")
s(69208, "Ishgardian Restoration", "Towards the Firmament; unlocks the Firmament and Skybuilders' content.")
s(70179, "Island Sanctuary", "Seeking Sanctuary; unlocks Island Sanctuary.")
# Squadron / PvP
s(67925, "Adventurer Squadron", "Squadron and Commander (Twin Adder); unlocks the squadron barracks.")
s(67926, "Adventurer Squadron", "Squadron and Commander (Maelstrom); unlocks the squadron barracks.")
s(67927, "Adventurer Squadron", "Squadron and Commander (Immortal Flames); unlocks the squadron barracks.")
s(66640, "PvP", "A Pup No Longer (Maelstrom); unlocks the Wolves' Den and PvP duties.")
s(66641, "PvP", "A Pup No Longer (Twin Adder); unlocks the Wolves' Den and PvP duties.")
s(66642, "PvP", "A Pup No Longer (Immortal Flames); unlocks the Wolves' Den and PvP duties.")
# Deep dungeons / field operations / variant
s(67092, "Palace of the Dead", "The House That Death Built; unlocks the Palace of the Dead.")
s(68667, "Heaven-on-High", "Knocking on Heaven's Door; unlocks Heaven-on-High.")
s(70199, "Eureka Orthos", "Delve into Myth; unlocks Eureka Orthos.")
s(68614, "Eureka (Forbidden Land)", "And We Shall Call It Eureka; unlocks Eureka Anemos and the Eureka relic chain.")
s(69370, "Bozja (Resistance weapons)", "Hail to the Queen; starts the Save the Queen / Resistance weapon chain.")
s(69477, "Bozjan Southern Front", "Where Eagles Nest; unlocks entry to the Bozjan Southern Front.")
s(71047, "Occult Crescent", "Occult Reunion (KinGkd101); unlocks the Occult Crescent field operation.")
s(70182, "Variant and Criterion dungeons", "A Key to the Past; unlocks variant dungeons (Sil'dih Subterrane) and criterion.")
# Jobs / misc systems
s(68728, "Blue Mage", "Out of the Blue; unlocks the Blue Mage limited job.")
s(65698, "Sightseeing Log", "A Sight to Behold; unlocks the sightseeing log.")
s(66747, "Treasure hunt", "Treasures and Tribulations; unlocks treasure map deciphering.")
s(67096, "Chocobo raising", "Bird in Hand; unlocks chocobo stabling, training and colour feeding.")
s(69379, "Ocean fishing", "All the Fish in the Sea; unlocks ocean fishing voyages.")
# Hunts
s(67099, "Hunts (ARR)", "Let the Hunt Begin (Twin Adder); unlocks the Hunt board and elite marks.")
s(67100, "Hunts (ARR)", "Let the Hunt Begin (Maelstrom); unlocks the Hunt board and elite marks.")
s(67101, "Hunts (ARR)", "Let the Hunt Begin (Immortal Flames); unlocks the Hunt board and elite marks.")
s(67655, "Hunts (Heavensward)", "Let the Clan Hunt Begin; unlocks Clan Centurio hunts.")
s(67658, "Hunts (Heavensward elite)", "Elite and Dangerous; unlocks HW elite mark bills.")
s(69133, "Hunts (Shadowbringers)", "Nuts to You; unlocks Clan Nutsy hunts.")
s(69712, "Hunts (Endwalker)", "The Hunt for Specimens; unlocks Endwalker hunt bills.")
# Relic chains
s(66241, "Relic: Zodiac weapons", "The Weaponsmith of Legend; first quest of the ARR relic chain.")
s(67747, "Relic: Anima weapons", "An Unexpected Proposal; first quest of the HW Anima chain.")
s(70188, "Relic: Manderville weapons", "Make It a Manderville (in-game name carries a leading icon glyph); first quest of the EW relic chain.")

D = {}
def d(qid, cfcs, note): D[str(qid)] = {"contentFinderConditionIds": cfcs, "note": note}
d(65781, [4], "It's Probably Pirates (Limsa start, ManSea203) -> Sastasha; objective territory 1036 matches CFC 4.")
d(66211, [4], "It's Probably Pirates (Gridania/Ul'dah start, ManFst203) -> Sastasha; name-variant of 65781.")
d(66213, [2], "Fire in the Gloom -> the Tam-Tara Deepcroft; objective territory matches CFC 2.")
d(66196, [3], "Into a Copper Hell -> Copperbell Mines; objective territory matches CFC 3.")
d(66233, [7], "Hallo Halatali -> Halatali (optional side quest, Western Thanalan).")
d(66050, [1], "Into the Beast's Maw -> the Thousand Maws of Toto-Rak; objective territory matches CFC 1.")
d(66337, [6], "Skeletons in Her Closet -> Haukke Manor; objective territory matches CFC 6.")
d(66368, [8], "The Things We Do for Cheese -> Brayflox's Longstop; objective territory matches CFC 8.")
d(66300, [9], "Braving New Depths -> the Sunken Temple of Qarn (optional side quest).")
d(66457, [12], "Dishonor Before Death -> Cutter's Cry (optional side quest).")
d(66476, [11], "Blood for Blood -> the Stone Vigil (MSQ).")
d(66515, [13], "Fort of Fear -> Dzemael Darkhold (optional side quest).")
d(66550, [5], "Going for Gold -> the Aurum Vale (optional side quest).")
d(66572, [15, 865], "Operation Archon (legacy pre-6.1 row GaiUsc901) -> Castrum Meridianum and Cape Westwind.")
d(70057, [15, 865], "Operation Archon (6.1 rework row XxcUsc901) -> Castrum Meridianum and the Cape Westwind quest battle.")
d(66060, [16], "The Ultimate Weapon (legacy pre-6.1 row ManFst503) -> the Praetorium.")
d(70058, [16, 830], "The Ultimate Weapon (6.1 rework row XxcFst503) -> the Praetorium and the Porta Decumana; both objective territories match.")
d(66406, [10], "Trauma Queen -> the Wanderer's Palace (optional side quest).")
d(66671, [14], "Ghosts of Amdapor -> Amdapor Keep (optional side quest).")
d(65879, [56], "Lord of the Inferno -> the Bowl of Embers (Ifrit); objective territory matches CFC 56.")
d(66393, [57], "Lord of Crags -> the Navel (Titan); objective territory matches CFC 57.")
d(66055, [58], "Lady of the Vortex -> the Howling Eye (Garuda); objective territory matches CFC 58.")
d(66584, [59], "Ifrit Bleeds, We Can Kill It -> the Bowl of Embers (Hard); Quest.InstanceContentUnlock 20004 confirms.")
d(66694, [60], "In a Titan Spot -> the Navel (Hard); GaiUsd004 in the hard-mode primal chain.")
d(66693, [61], "In for Garuda Awakening -> the Howling Eye (Hard); GaiUsd003 in the hard-mode primal chain.")
d(66733, [63], "Ifrit Ain't Broke -> the Bowl of Embers (Extreme); objective territory matches CFC 63.")
d(66732, [64], "Quake Me Up Before You O'Ghomoro -> the Navel (Extreme); objective territory matches CFC 64.")
d(66731, [65], "Gale-force Warning -> the Howling Eye (Extreme); objective territory matches CFC 65.")
d(65905, [84], "An Uninvited Ascian -> the Chrysalis (2.4 MSQ); objective territory matches CFC 84.")
d(70011, [808], "The Crystal from Beyond -> Asphodelos: The First Circle (Pandaemonium raid series entry); not a relic quest.")
# Dawntrail MSQ dungeons (objective territory matches)
d(70415, [826], "For All Turali -> Ihuykatumu; objective territory 1167 matches CFC 826.")
d(70427, [824], "The High Luminary -> Worqor Zormor; objective territory 1193 matches CFC 824.")
d(70445, [829], "Road to the Golden City -> the Skydeep Cenote; objective territory 1194 matches CFC 829.")
d(70462, [831], "All Aboard -> Vanguard; objective territory 1198 matches CFC 831.")
d(70481, [825], "The Resilient Son -> Origenics; objective territory 1208 matches CFC 825.")
d(70495, [827], "Dawntrail -> Alexandria; objective territory 1199 matches CFC 827.")

def dump(name, obj):
    with open(os.path.join(OUT, name), "w", encoding="utf-8", newline="\n") as f:
        f.write(json.dumps(obj, indent=2, ensure_ascii=False) + "\n")

dump("system_unlocks.json", dict(sorted(S.items(), key=lambda kv: int(kv[0]))))
dump("duty_unlocks.json", dict(sorted(D.items(), key=lambda kv: int(kv[0]))))
ids = sorted({int(k) for k in S} | {int(k) for k in D})
dump("feature_quests.json", {
    "questRowIds": ids,
    "note": "Union of the Quest row ids in system_unlocks.json and duty_unlocks.json; seeds the Feature Unlocks virtual category. Regenerate when either file changes.",
})
print("system", len(S), "duty", len(D), "feature", len(ids), "overlap", len(S) + len(D) - len(ids))
for k in S:
    assert int(k) >= 65536 and S[k]["note"]
for k in D:
    assert int(k) >= 65536 and D[k]["note"] and D[k]["contentFinderConditionIds"]

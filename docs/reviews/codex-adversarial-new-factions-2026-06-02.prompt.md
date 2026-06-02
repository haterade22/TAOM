# Adversarial Review -- New Factions (Misty Mountain Orcs / Goblins / Goblin Town / Blue Craig / Lindon)

You are an adversarial code+data reviewer. This changeset adds FOUR new kingdoms and TWO new cultures to a Bannerlord 1.4.5 LOTR total-conversion mod (TAOM). Most of the surface is DATA (XML/JSON) plus a small C# feat-dispatch extension plus Python generator scripts. Hunt for real, shippable bugs -- broken cross-references, ID typos, clone-leftover text, diplomacy gaps, dead config, and C# wiring errors. Be concrete: cite file + line, say why it is a bug, and propose the fix. Do NOT pad with style nits.

## What was built

- 2 new cultures: `goblin` (race goblin) and `mistymountainorcs` (race orc) -- both CLONED from the existing `gundabad` culture's troops/npcs/equipment with ids/loc-keys/race renamed and equipment item-ids deliberately PRESERVED (reuse gundabad's real LOTRLOME_Armory items as placeholder gear, per user instruction).
- 4 new kingdoms: `goblin` (Goblin Town, culture goblin), `mistymountainorcs` (Misty Mountains, culture mistymountainorcs), `bluecraig` (Blue Craig, culture goblin -- shares goblin culture, separate clans), `lindon` (culture rivendell -- reuses the existing Rivendell/Noldor culture wholesale).
- Cultural feats: 8 new feats (4 goblin, 4 mistymountainorcs) -- party-size bonus, snow-speed bonus, volunteer-respawn bonus, food-consumption penalty; mistymountainorcs also gets army-influence-cost reduction.
- Diplomacy: forever-alliances (tier "Permanent") among the 3 orc kingdoms + the evil factions; Lindon allied with Rivendell only.
- ~50 settlements authored in the EXTERNAL `TAOM_Map` module (NOT in this repo -- you cannot read them; do not flag them as missing).
- ~130 lords/heroes/clans, recruitment pools, faction-map cards, party templates.
- Python generators (tools/) that produced the cloned data deterministically.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa, PLUS the 4 NEW: goblin, mistymountainorcs, bluecraig, lindon.
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar, PLUS the 2 NEW: goblin, mistymountainorcs.
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale.
NOTE: "rohan" is NOT a valid ID (use vlandia). "dol_guldur" is NOT valid (use dolguldur). "dunland" is NOT valid (use empire).

## READ FIRST

- docs/features/new-factions-misty-mountains-lindon.md -- the feature doc (design + file map).
- docs/reviews/rca-new-factions-2026-06-02.md -- the prior deep-review RCA (3 HIGH already fixed: cattle_range->cattle_farm VillageType, TAOM_Map DependedModule, Blue Craig villages). Do NOT re-report those three -- they are fixed. DO check whether the fixes are correct and whether sibling instances exist.
- tools/taom_new_factions_layout.json -- the source-of-truth layout (kingdoms/clans/settlements).

## KNOWN SUSPECTS (confirm or dispute each with evidence)

1. CONFIRMED-BY-US, MAP-THE-FULL-EXTENT: Gundabad clone-leftover DISPLAY TEXT. Both clone_transform functions (tools/generate_new_factions.py line ~47, tools/insert_new_factions.py line ~50) only remap the bracketed "[Gundabad]" tag and "Pale Uruk" -- they do a lowercase `text.replace("gundabad", culture)` for IDs but NEVER remap capital-G free-text "Gundabad". So player-facing display strings cloned from gundabad still say "Gundabad". We have already confirmed these instances: (a) goblin + mistymountainorcs culture `name=` both read "Gundabad Orcs" (taom_spcultures.xml ~4516, ~4875); (b) both culture `text=` descriptions are entirely Gundabad-themed (~4538, ~4897); (c) the `<clan_names>` first entry "Mount Gundabad Tribe" (~4712, ~5071); (d) 36 notable NPC display names in characters/npcs_goblin.xml + characters/npcs_mistymountainorcs.xml ("Gundabad Caravan Master", "Cunning Gundabad haggler", "Fanatical Gundabad war-chanter", etc.). YOUR JOB: find the COMPLETE set of clone-leftover display text across ALL new-faction data (also check taom_wanderers.xml, characters/clans.xml new clans, characters/lords.xml, characters/heroes.xml new entries, equipmentsets/taom_equipment_sets_{goblin,mistymountainorcs}.xml roster names, factionmap/factions.json, taom_module_strings.xml). Report any instance we missed. IMPORTANT: `Item.wm_gundabad_*`, `BodyProperty.fighter_gundabad`, `Culture.gundabad_raiders`, `SkillSet.spc_wanderer_gundabad`, and the pre-existing real Gundabad faction/heroes (clan id `gundabad_raiders` "Gundabad Orc Raiders" in clans.xml; azgar/drozmak/kargoth hero descriptions in heroes.xml) are INTENTIONAL/legitimate -- do NOT flag those as leftovers.

2. Diplomacy completeness + correctness (Main/_Module/ModuleData/diplomacy/diplomacy.json). The diplomacy service sorts each pair order-independently. Verify: (a) every kingdomA/kingdomB is a valid kingdom ID from the cheatsheet (catch typos); (b) Lindon has exactly ONE entry (lindon<->rivendell Permanent) -- is "Neutral to all others" the correct DEFAULT for unlisted pairs, or does the engine/service need explicit Neutral entries (what tier do unlisted pairs resolve to)? read the consuming service if you can find it; (c) the 3 orc kingdoms (goblin, mistymountainorcs, bluecraig) are mutually Permanent and Permanent with the evil factions (empire_s/isengard/gundabad/dolguldur/aserai/khuzait) and Hostile with the good factions -- check for any missing or asymmetric pair, or any orc kingdom NOT permanently allied with another evil faction it should be; (d) are there duplicate or contradictory pairs (same pair listed at two tiers)?

3. Cultural feat wiring + dispatch (Main/Features/CulturalFeats/TaomCulturalFeats.cs + CulturalFeatsService.cs + the `<cultural_feats>` blocks in taom_spcultures.xml). A FeatObject needs FIVE wiring points in TaomCulturalFeats.cs (private field, public property, Register() call, Initialize() call, GetAllFeats() yield) AND a `<feat id="...">` in the culture's `<cultural_feats>` block AND a dispatch branch in CulturalFeatsService.cs. Verify all 8 new feats are wired in all 5 C# locations, referenced in the right culture's XML block, AND actually dispatched. Specifically: is `MistyMountainOrcsArmyInfluenceCostFeat` (registered AddFactor -0.4f) dispatched in ApplyArmyInfluenceCost and does `multiplier += -0.4f` then `(int)(baseCost * (1f + multiplier))` correctly yield a 40%-cheaper cost? Are the food-consumption feats (isPositiveEffect:false, +0.2f/+0.15f AddFactor) dispatched in ApplyFoodConsumptionFeats with the correct sign so they INCREASE consumption? Are the snow-speed feats dispatched under TerrainKind.Snow? Any feat registered but never dispatched, or dispatched but never registered?

4. Culture troop/recruitment consistency. goblin culture `basic_troop`/`elite_basic_troop` = NPCCharacter.goblin_snaga / goblin_fighter; mistymountainorcs = mistymountainorcs_snaga / mistymountainorcs_fighter. VolunteerRecruitmentService.cs adds CultureMap["goblin"], ["mistymountainorcs"], ["rivendell"] (the last was previously absent so lindon/rivendell settlements returned null pools). Verify every troop id referenced by these pools AND by the culture default-troop attributes exists in troops/troops_{goblin,mistymountainorcs,rivendell}.xml, and that the cloned troop tree has no dangling upgrade_target after cavalry was stripped.

5. Faction-map sync (Main/_Module/ModuleData/factionmap/factions.json). TAOM convention: any change to cultural identity (new cultural feats) MUST be reflected on the CC faction-map page. Verify the 2 new cultures' cards exist and their feat descriptions match what actually ships in TaomCulturalFeats.cs Initialize() (the user-facing feat names "Goblin Swarm", "Endless Spawn", "Tunnel-Runners", "Ravenous Swarm", "Orc Horde", "Mountain Host", "Mountain-Bred", "Hungry Host" and their magnitudes). Flag any drift.

6. Cloned-tree structural integrity. The orc troop trees were cloned from gundabad then had CAVALRY STRIPPED (user: infantry + archer lines only, no cavalry). Verify: no troop still has a Horse/HorseHarness equipment slot; no upgrade_target points to a deleted cavalry troop; party templates (taom_partyTemplates.xml) for the new kingdoms reference only troops that still exist; equipment rosters referenced by surviving troops still resolve.

## FILE LISTS

C# (modified):
- Main/Features/CulturalFeats/TaomCulturalFeats.cs (8 new feats)
- Main/Features/CulturalFeats/CulturalFeatsService.cs (dispatch branches)
- Main/Features/TroopProgression/VolunteerRecruitmentService.cs (3 new culture pools)

Data (new):
- Main/_Module/ModuleData/troops/troops_goblin.xml
- Main/_Module/ModuleData/troops/troops_mistymountainorcs.xml
- Main/_Module/ModuleData/characters/npcs_goblin.xml
- Main/_Module/ModuleData/characters/npcs_mistymountainorcs.xml
- Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_goblin.xml
- Main/_Module/ModuleData/equipmentsets/taom_equipment_sets_mistymountainorcs.xml

Data (modified):
- Main/_Module/ModuleData/taom_spcultures.xml (2 new culture blocks, marker-delimited TAOM-NEWFACTIONS)
- Main/_Module/ModuleData/taom_spkingdoms.xml (4 new kingdom blocks)
- Main/_Module/ModuleData/characters/clans.xml (new clans)
- Main/_Module/ModuleData/characters/lords.xml (new lords)
- Main/_Module/ModuleData/characters/heroes.xml (new heroes)
- Main/_Module/ModuleData/diplomacy/diplomacy.json
- Main/_Module/ModuleData/factionmap/factions.json
- Main/_Module/ModuleData/charactercreation/cultures.json
- Main/_Module/ModuleData/taom_partyTemplates.xml
- Main/_Module/ModuleData/taom_wanderers.xml
- Main/_Module/ModuleData/equipmentsets/taom_wanderer_equipment.xml
- Main/_Module/ModuleData/taom_module_strings.xml
- Main/_Module/SubModule.xml

Python generators (tools/):
- generate_new_factions.py, insert_new_factions.py, mordor_armor_remap.py, generate_new_faction_kingdoms.py, generate_new_faction_settlements.py, assign_orc_village_types.py, make_new_factions_playable.py, taom_new_factions_layout.json, _orc_dropped_cavalry.json

Tests (modified):
- TAOM.Tests/Core/ConfigIdValidationTests.cs (18 cultures, 22 kingdoms)
- TAOM.Tests/Features/CulturalFeats/TaomCulturalFeatsDefinitionTests.cs (105 feats)
- TAOM.Tests/Features/FactionMap/FactionMapDataTests.cs
- TAOM.Tests/Features/CharacterCreation/CareerCultureCoverageTests.cs
- TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs

## REQUIRED OUTPUT SECTIONS

KNOWN SUSPECTS VERDICTS -- for each of the 6 above: CONFIRMED / DISPUTED / PARTIAL, with file+line evidence. For suspect 1 give the COMPLETE leftover list.

CROSS-REFERENCE AUDIT -- pick the highest-risk ID cross-refs and verify them: do all 4 new kingdom IDs and 2 new culture IDs appear consistently across kingdoms/clans/cultures/diplomacy/factionmap/cultures.json/tests? Any kingdom referencing a culture or clan that does not exist? Any clan referencing a non-existent kingdom or culture? Any lord referencing a non-existent clan? Any duplicate ids?

C# CORRECTNESS -- the feat dispatch + recruitment changes: any logic/sign error, any feat wired in 4 of 5 locations, any null/empty pool.

GENERATOR CORRECTNESS -- the Python scripts: idempotency, the protect-list completeness, the cavalry-strip regex, and (most important) whether the clone_transform display-text gap (suspect 1) is the ROOT of a class of bugs that will recur on any future clone. Recommend the durable fix.

FINDINGS -- numbered, each: severity (HIGH/MED/LOW), file:line, what is wrong, why it is a bug in-game, the fix. If you find NOTHING in a category, say so explicitly -- do not invent findings to fill space.

## QUALITY GATES

- You can read every file in the repo. The TAOM_Map settlements are external and NOT in the repo -- do not flag settlement entries as missing.
- Verify "missing" claims by grepping before asserting absence.
- Distinguish intentional preserved gundabad IDs (items/body/skillset/bandit-subculture) from clone-leftover display text -- only the latter are bugs.
- Prior reviews: config-ID cross-ref reliably catches kingdom/culture typos; clone-leftover text and diplomacy-symmetry gaps are the highest-yield categories for this kind of data changeset.

Adversarial review: two new Neutral kingdoms, Anduin Vale (`anduinvale`, prefix AV) and Angáladh (`angaladh`, prefix AND), each a new human culture cut from Dale, built with the TAOM kingdom generators (issue #1053). Review the UNCOMMITTED working tree of this worktree (base 6589bdd11): `git diff` plus `git ls-files --others --exclude-standard`. You are a reviewer: report, do not edit any file, do not commit.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".
Also custom since 2026-10: arthedain, cardolan, rhudaur, eredluin, lindon, bluecraig, goblin, mistymountainorcs; and now anduinvale, angaladh.

READ FIRST:
- AGENTS.md, then .ai/review-reference.md (review rules and the lessons from prior reviews).
- docs/features/anduin-vale-angaladh.md (the design, "Merging this branch", Known gaps).
- docs/reviews/rca-anduin-vale-angaladh-2026-10-28.md (what the Claude deep review already found and fixed: do not re-report those rows unless the fix is wrong).
- docs/features/ered-luin.md and docs/features/cardolan-rhudaur.md (the recipe this follows; Cardolan is the Neutral model).

STATE YOU MUST KNOW:
- The 41 settlement rows for these realms are HELD BACK from the live, unversioned TAOM_Map (E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\TAOM_Map\ModuleData\settlements.xml and Languages\<12>\loc_settlements.xml): while live they broke every other checkout's validator. The built rows are in the sibling files *.bak_av_and_built_20261028_200935 (read those as "the rows that will ship"). They are re-applied by tools/add_map_fortifications.py and tools/add_map_villages.py in the merge sitting. So the branch validator now reports LANDLESS_CULTURE for the two cultures: expected, not a finding.
- The scene entity castle_village_AN1_1 was renamed castle_village_AND1_1 in the live Main_map scene.xscene.
- The live LOTRLOME_Armory LOTRLOME_items/dale/starter_kit.xml was rewritten by tools/generate_starter_kit.py.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with evidence):
1. Dale is vanilla `sturgia` rewritten by spcultures.xslt. tools/promote_borrowed_cultures.py now renders that block with lxml (render_xslt_culture), takes some shared blocks from `sturgia` and some from `gondor` (block_src), renames the exact token Culture.sturgia through id_overrides, and writes its own lord templates (add_lord_templates). Suspect: some binding in the two generated <Culture id="anduinvale|angaladh"> blocks in Main/_Module/ModuleData/taom_spcultures.xml, or in characters/npcs_<c>.xml, equipmentsets/taom_equipment_sets_<c>.xml, the shared rosters or the wanderers, still points at Dale/sturgia data that only works for StringId "sturgia", or at an id the engine cannot resolve, or keeps Dale-only content (names, text).
2. Neutral wiring. Suspect: a path treats an unlisted kingdom pair as something other than Neutral, or War of the Ring / army targeting / alignment-aware systems pull these realms into a war or exclude them wrongly (execution/alignment.json, diplomacy/diplomacy.json has no row by design, configs/army_targeting.json theater ["north","east"], DiplomacyService, ShippedClanSideCoverageTests NeutralRealmIds).
3. Culture-keyed compiled C# twins vs JSON. Suspect: a compiled default (BannerBearerConfig, BattleBalanceConfig, CombatMechanicsConfig, CrownGuardMap, HallOfFireMap, LordBannerMap, VoiceAccentMap, RaceAbilityDefaults) disagrees with its shipped JSON for the new cultures, or a C# table keyed on culture that names cardolan/eredluin was not decided for the new ones (grep Main/ for "eredluin").
4. Character creation and careers. Suspect: a gap between charactercreation/cultures.json, cc_body_properties.xml, the four menus, the CC rosters (Angáladh has no Horse slot by design; check Patch20_NarrativeHorseGuard), the six careers in career_system/*.xml, CareerSystemIoC archetype rows, and the career kits in equipmentsets/taom_career_starting_equipment.xml (generate_career_kits.py THROWN_FOR for Angáladh's two-handed hewer).
5. The merge sitting. Suspect: re-running the two map tools from this branch onto the restored live files does NOT reproduce the *.bak_av_and_built_20261028_200935 copies, or the save-order rule (new rows at the file's end, a town first) or the gate positions break; or tools/name_tags.py --target live-map --apply --allow-live-write tags rows it should not.
6. Localization. Suspect: a new {=KEY} with no registered English row, a key whose inline default differs from its registration, a key reused from Dale/Gondor that now shows another culture's text, or a language row left with stale English after the English changed (faction-card keys and four wanderer surnames were reset).

FILES (by category; full list from git):
- C#: Main/Features/{AdvancedStartOptions/TaomStartOptionsProvider.cs, BannerBearers/Domain/BannerBearerConfig.cs, BattleBalance/BattleBalanceConfig.cs, CareerSystem/CareerSystemIoC.cs, CombatMechanics/CombatMechanicsConfig.cs, CoopInterop/CoopSettingsRelevance.cs, CrownGuard/CrownGuardMap.cs, CustomBattles/Config/CustomBattleCommandersProvider.cs, HallOfFire/HallOfFireMap.cs, LiegeTitles/TaomCultureTags.cs, LordBanners/LordBannerMap.cs, RaceAbilities/Domain/RaceAbilityDefaults.cs, RealmBorders/RealmBordersSettingsProvider.cs, TaomSettings.cs, TroopProgression/VolunteerRecruitmentService.cs, VoiceAccents/VoiceAccentMap.cs}, new TroopProgression/RecruitmentPools/VolunteerRecruitmentService.{AnduinVale,Angaladh}.cs; tests under TAOM.Tests/Features.
- XML/JSON: Main/_Module/ModuleData (culture, kingdoms, clans, lords, heroes, npcs, troops, party templates, rosters, wanderers, careers, CC, culture-keyed tables, strings and 12 languages), Main/_Module/SubModule.xml.
- Tools: tools/promote_borrowed_cultures.py, generate_new_faction_kingdoms.py, realm_troops.py, generate_anduinvale_troops.py, generate_angaladh_troops.py, add_map_fortifications.py, add_map_villages.py, insert_new_faction_cc_menus.py, insert_new_faction_careers.py, generate_char_creation_equipment.py, generate_career_kits.py, ranged_ladders.json, and their tests under tools/tests.

REQUIRED SECTIONS:
1. VANILLA CODE: for each engine claim you make, quote the decompiled method (E:\Decompiled_Bannerlord\ for browsing; the installed DLLs are the authority: `pwsh tools/taom-src.ps1 path <Type>`). At minimum CultureObject.Deserialize, Settlement.Deserialize and Town/Village deserialization, MBObjectManager presumed objects and UnregisterNonReadyObjects, HeroCreator template culture, the character creation narrative horse path.
2. DEEP ANALYSIS of each Known Suspect with a concrete scenario (new campaign, old save, character creation, a lord spawn, a rebellion, a co-op client).
3. CONFIG CROSS-REFERENCE: every new culture/kingdom/clan/troop/party-template/roster/settlement id against where it is defined, including the held-back rows in *.bak_av_and_built_20261028_200935.
4. FINDINGS OR OBSERVATIONS: severity (CRITICAL/HIGH/MEDIUM/LOW), file:line, evidence, a reproduction or proving command, and the fix. Mark anything you could not prove UNVERIFIED.

QUALITY GATES you may run (read-only or --check only; never --apply): python tools/validate_moduledata.py (expect LANDLESS_CULTURE only, see STATE), python tools/validate_xml_schemas.py, python -m pytest tools/tests -q, each generator's --check, python tools/check_generator_item_refs.py. Do not run dotnet if it would deploy: only `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-anduin-vale-angaladh-2026-10-28.md; do NOT write that path yourself.

ADVERSARIAL REVIEW: nine Isengard villages added to the LIVE TAOM_Map settlements.xml by a new table-driven tool (#562), 2026-09-11.

You are the independent verifier. Assume the change has bugs and try to prove it. Read source, do not trust prose. Every claim in your output must cite a file and line you read, or a command you ran. Do not modify any file.

FEATURE
tools/add_map_villages.py appends village <Settlement> rows to the LIVE game-install file E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml and one <string> name row to each of the 12 Languages/<L>/loc_settlements.xml, reading each village's posX/posY from the entity transform in Modules/TAOM_Map/SceneObj/Main_map/scene.xscene. It was applied once today (backups *.bak_mapvillages_20260911_181338 sit beside each file), then the map author saved the scene twice in the editor, which re-serialised the master through SandBox.View SettlementPositionScript.OnSceneSave (XmlDocument.Save). A six-agent Claude deep review then landed three fixes in the tool (entity search window, per-language loc repair, honest summary line). The tool is idempotent and has a --check mode.

The nine rows (all Culture.isengard, hearth 500, empire panel meshes, vanilla empire_village_a..i scenes):
castle_village_isengard_b/c/d bound Settlement.castle_orthanc_gate (wheat_farm, swine_farm, lumberjack)
village_isengard_b..f bound Settlement.town_isengard (wheat_farm, fisherman, sheep_farm, swine_farm, iron_mine)
castle_village_I2_4 bound Settlement.castle_I2 (iron_mine)

TAOM ID CHEATSHEET
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid, use "dolguldur".

READ FIRST
- tools/add_map_villages.py (the whole file; the VILLAGES table, scene_position, settlement_position, resolve_position, insert_after_settlement, insert_loc_rows, plan_loc_rows, nothing_to_do, check, _write, main)
- tools/tests/test_add_map_villages.py (30 tests, synthetic text only)
- tools/README.md "XML I/O convention" (the byte discipline every script that edits ModuleData must follow) and the add_map_villages.py row in its Settlements table
- docs/modding/settlements.md, the attribute tables and "Recipes: Add"
- docs/reviews/rca-map-villages-tool-2026-09-11.md (what the Claude review found and fixed; dispute it if you can)
- The nine live rows: grep the live settlements.xml for id="castle_village_isengard_b" through id="castle_village_I2_4" (lines roughly 9896 to 10013) and compare each to the sibling castle_village_I2_3 row
- Languages/DE/loc_settlements.xml and Languages/CNs/loc_settlements.xml, the rows after Settlements.Settlement.text.village_isengard_a
- tools/add_bluecraig_castles.py (the precedent this tool replaces)

KNOWN SUSPECTS (CONFIRM or DISPUTE each, with evidence)
1. scene_position now windows the search to the text between the entity's open tag and the next "<game_entity" occurrence. Can a scene entity legally carry its <transform> AFTER a nested child game_entity, or after a <children> block, so the window ends before the transform and the tool falls back to a PLACEHOLDER for an entity that is actually placed? Check the real scene.xscene structure for several settlement entities and for any entity whose transform is not the first thing after <tags>.
2. check() parses posX/posY with float(). The editor writes them with float.ToString() (SettlementPositionScript.SaveSettlementPositions). On a machine whose current culture uses a decimal comma, would the live file carry "628,4872" and make --check crash with ValueError instead of reporting a finding? Is that a real risk on Bannerlord (does the engine force InvariantCulture at startup)? Decompile TaleWorlds.MountAndBlade or the launcher for CultureInfo settings before answering.
3. --check tolerates 0.01 between the master posX/posY and the scene transform. The editor stores a float32 and prints it with more digits than the scene file (628.4872 vs 628.487). For map coordinates up to ~1600, can float32 round-trip drift exceed 0.01 and turn every future --check into a false failure?
4. insert_after_settlement finds the anchor's </Settlement> with str.find after the anchor's open tag. If the anchor settlement (village_isengard_a) is ever removed or renamed, the fallback appends before </Settlements>, outside the Isengard comment block. Is there any engine or tool that cares about row order or the comment banners in this file? Grep tools/ for scripts that parse settlements.xml by region banner.
5. The "new campaign only" rule. The Claude review claims Settlement.Deserialize (TaleWorlds.CampaignSystem.Settlements.Settlement, v1.4.8) keys on the campaign-wide CampaignGameLoadingType and, on a SavedCampaign load, runs Alleys[num].Initialize against a brand-new settlement's empty Alleys, so loading a pre-batch save throws. Decompile Settlement.Deserialize and the campaign load sequence (Campaign.cs SetLoadingParameters, MBObjectManager.LoadXML, how objects absent from the save are created) and state whether the throw is real, and whether anything earlier (SettlementVisual, Village.Deserialize's skipped bound) throws first.
6. Village.Deserialize reads bound and hearth only when CampaignGameLoadingType != SavedCampaign. Village.MapFaction => Bound.MapFaction. On a new campaign, which system reads a village's Bound first, and is there any ordering hazard where the nine villages are deserialized before their bound fortifications (castle_orthanc_gate, town_isengard, castle_I2) are registered? The bound rows sit ABOVE the villages in the file for castle_I2 and castle_orthanc_gate, but town_isengard sits at line 9847, after castle_village_isengard_b..d were inserted? Check the actual order in the live file and how ReadObjectReferenceFromXml resolves forward references.
7. Orthanc (town_isengard, prosperity 4800) now holds six villages, castle_orthanc_gate (960) four, castle_I2 (950) four. Read TaomSettlementFoodModel (Main/Features/SettlementFood/) and DefaultSettlementMilitiaModel: does village count feed anything that is capped, tabled, or indexed by a fixed size (an array of bound villages, a UI list with a fixed row count, a party template count)? Cite the code.
8. TAOM_Map/SubModule.xml lists TAOM under DependedModuleMetadatas (LoadBeforeThis) but not under DependedModules. The Claude review calls this pre-existing and out of scope. Is it, or does the launcher's dependency resolver treat the two lists differently in a way that makes the isengard culture reference unsafe? Decompile TaleWorlds.ModuleManager / the launcher's module sorting before answering.

FILES
Tool and tests: tools/add_map_villages.py, tools/tests/test_add_map_villages.py
Registry and docs: tools/Apply-MapVillageNames.py (nine new NAMES rows after castle_village_I2_3), tools/README.md, docs/modding/settlements.md, CHANGELOG.md entry "feat(map): nine Isengard villages", docs/reviews/rca-map-villages-tool-2026-09-11.md
Live data (game install, unversioned): Modules/TAOM_Map/ModuleData/settlements.xml, Modules/TAOM_Map/ModuleData/Languages/<L>/loc_settlements.xml x12, Modules/TAOM_Map/SceneObj/Main_map/scene.xscene
No C# changed.

REQUIRED SECTIONS

VANILLA CODE
Decompile and paste as code blocks (E:/Decompiled_Bannerlord/_modules_build_v1.4.8 or the category tree, and ilspycmd on the installed DLLs for anything you cite as a signature): Settlement.Deserialize (the CommonAreas loop and the posX/gate_posX reads), Village.Deserialize, SettlementPositionScript.LoadSettlementData and SaveSettlementPositions (SandBox.View), and the part of the launcher/ModuleHelper that sorts modules by DependedModules versus DependedModuleMetadatas.

TOOL ANALYSIS
Walk main() for --check, dry run, and --apply. For each of the three, list every file it reads and every file it writes. Then answer: (a) can any code path write a file without first parsing the new text; (b) can a partially applied state be produced and is every such state repairable by a re-run; (c) does missing_ids substring matching (id="X") admit any false positive for ids in the shipped table; (d) does detect_newline pick wrong on any of the 13 live files (measure: count of CRCRLF, CRLF and lone LF per file); (e) does insert_loc_rows leave the file well-formed when the anchor row is the LAST row before </strings>.

DATA ANALYSIS
For each of the nine rows: attribute-for-attribute diff against castle_village_I2_3, position versus the scene transform (quote both), loc row presence in all 12 languages. Confirm the six VillageType ids are registered in DefaultVillageTypes (v1.4.8) and that Culture.isengard's villager_party_template, militia templates and militia troops resolve. State whether any of the nine positions sits on water or off the navmesh if you can determine it from the scene (if you cannot, say so; do not guess).

CONFIG CROSS-REFERENCE
Every Culture., Settlement., VillageType., LocationComplexTemplate. reference in the nine rows must resolve to a definition in TAOM, TAOM_Map, SandBox or SandBoxCore ModuleData. List each with the file that defines it.

FINDINGS OR OBSERVATIONS
Severity P1 (would crash or corrupt data), P2 (wrong behaviour), P3 (quality). For each: file:line, what is wrong, how to reproduce, the minimal fix. If you find nothing at a severity, say so explicitly. Then a section "What the Claude review got wrong", disputing any claim in the RCA or CHANGELOG entry you can disprove with evidence.

QUALITY GATES
- Every finding cites file:line or a command and its output.
- No finding may rest on "I did not find X"; grep and paste the grep.
- Do not flag vanilla-parity behaviour as a bug.
- Do not flag the repo shadow Main/_Module/ModuleData/settlements.xml as stale; it is documented as inert and out of scope.
- Do not propose deleting or renaming settlement ids; save files reference them.

PRIOR REVIEW LESSONS
SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches; starting from the engine and reading the prose last caught two false absolutes in one doc (#559).
FAILURES: Codex assumed empire=Rohan (it is Dunland); Codex flagged vanilla-matching code as bugs; Codex skipped hard sections; Codex reported "not found" without a grep.

OUTPUT
Write the whole review to stdout; the caller redirects it to docs/reviews/raw/codex-adversarial-map-villages-2026-09-11.md. Use plain markdown, flat formatting, no em or en dashes.

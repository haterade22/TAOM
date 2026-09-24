# Codex adversarial review: Animalia elk and moose, antler attacks, riders, Monster size (#646), 2026-09-23

ROLE: reviewer only (AGENTS.md; .ai/roles/reviewer.md). Do not edit, stage, commit, deploy or push anything, and do not create files. Report findings with file:line, impact, and proving code or decompiled engine code. Try to refute each claim before reporting it. Missing engine or native evidence stays UNVERIFIED. This is a working-tree review on branch bannerlord-1.5.x of one session's work; most of it is uncommitted, part of it sits in HEAD c79a5852 (a maintainer "commit all" that also swept other sessions' work).

OUT OF SCOPE (other sessions' work in the same tree; do not review or report): the STAGED changes (git diff --cached): Main/Core/Validation/EnumNames.cs, Main/Features/BannerBearers/BannerBearerConfigProvider.cs, Main/Features/DreadAura/DreadAuraConfigProvider.cs, Main/Features/SignatureStrikes/Domain/StrikeNames.cs, Main/Features/UncapturableHeroes/UncapturableHeroesConfigProvider.cs, their tests, the staged hunks of CustomAttacksUtils.cs, CHANGELOG.md and LESSONS-LEARNED.md, and the staged docs; the unstaged module_sounds.xml, nazgul_scream*.ogg and docs/features/signature-strikes.md; and everything in c79a5852 that is not listed under FILES below (the great elk feature Main/Features/Elk/* was another session's and was reviewed in docs/reviews/rca-elk-delta-2026-09-23.md; only this session's uncommitted hunks in it are in scope).

Do NOT run dotnet build or dotnet test (other sessions share obj/). Do not run any tool in a writing mode (--apply, -Apply). Read-only modes are fine: python tools/validate_moduledata.py, python tools/validate_xml_schemas.py --live, python tools/apply_animalia_armory.py (its default is a dry run), pwsh tools/gen_animalia_anim_clips.ps1 -Verify, pwsh tools/wire_anim_master_skeletons.ps1 (census without -Apply), python -m pytest on single tools/tests files. The game and the Modding Kit are closed.

## FEATURE (what the session built)

Bannerlord 1.5.3 mod TAOM, issue #646, on the maintainer Mike's instructions:
1. Two Fab quadruped packs (Animalia elk, Animalia moose) reskinned onto the vanilla horse_skeleton and ridden as ordinary Horse-slot mounts. Live LOTRLOME_Armory (an unversioned companion module that ships to players): Monsters taom_animalia_elk / taom_animalia_moose (base_monster="horse") in ModuleData/Monsters/LOTR/lotr_monster_animalia.xml, registered in the Armory SubModule.xml; action sets as_animalia_elk / as_animalia_moose (children of as_horse, with _map and _town_and_village twins) binding the packs' retargeted clips; items taom_animalia_elk_a / taom_animalia_moose_a (is_merchandise="false", difficulty 0) in LOTRLOME_items/LOTRAOM_horses.xml.
2. An antler attack for both: new actions act_animalia_elk_antler and act_animalia_moose_antler typed actt_kick (action_types.xml), bound in their action sets, fired by a per-agent behavior tree on TAOM's shared elephant-like engine (Main/Features/ElephantLike/**, also used by the great elk, war ram, elephant and mumakil). One Blunt blow on one target (elk 60, moose 70); the rider owns the blow; the rider's career charge multiplier applies as for the great elk.
3. Riders: the moose to Thranduil and the Mirkwood lords (thranduil_bat_equipment, mirkwood_bat_template_medium_a..e, taom_mirkwood_{lord,ruler}_battle_{male,female}); the great elk (taom_elk_a) to the top cavalry troop mirkwood_beleglas only; the Animalia elk to mirkwood_rochenlas and the elk_rider career start (player_career_mirkwood_cavalry_m/_f). Two Custom-Battle-only test riders (troops_animalia_test.xml, deliberately visible until release).
4. Size on the Monster: a TAOM attribute taom_body_length on <Monster> (Animalia elk 100, moose 150, great elk 110). Main/Features/MonsterSize reads it through the engine's merged Monsters XML at every MBSubModuleBase.OnGameInitializationFinished (Main/SubModule.cs:1459, before TAOM's once-per-process patch guard) and writes it into HorseComponent.BodyLength (private setter, via AccessTools) of every Horse item whose Monster declares it, then recomputes the item's cached ItemObject.Effectiveness through the private CalculateEffectiveness. The items keep body_length="100" only because the engine's Items.xsd requires it. The engine's Monsters.xsd does not declare taom_body_length; the session claims the engine then prints one "not declared" validation line per sized Monster and loads the file anyway, and that a module Monsters.xsd would crash the merge (MBObjectManager.MergeElements indexes XmlResource.XsdElementDictionary).
5. Reach follows the live body: ElephantLikeCombatProfile gained reachScalesWithBody (only the great elk and the two Animalia animals set it); the engage decorator and the attack task multiply the attack trigger range and radius by ElephantLikeReach.Scale(Agent.AgentScale), NaN-guarded with MinScale 0.099f because the engine's single-precision 0.01f*10 is 0.099999994f. This replaced an authored ElkConfig.AuthoredScale constant.
6. The Animalia elk is guaranteed stock in Mirkwood towns: culture_marketplace_config.xml routes it to mirkwood with min_stock="1" (CultureMarketplaceMaintenanceService.EnsureGuaranteedStock adds routed items by id); the moose is not sold.
7. Tools: Blender reskin and retarget scripts, a clip-resource generator cloning vanilla horse clips, a skeleton-GUID patcher for Kit-imported animation masters, an idempotent Armory writer (apply_animalia_armory.py, dry run by default, refuses while the game or Kit runs), a validate_xml_schemas.py allowlist for the TAOM attribute.

An internal 8-lens deep review plus a convergence pass already ran earlier today; its findings, Mike's decisions and the fixes are in docs/reviews/rca-animalia-2026-09-23.md. A second full internal review runs in parallel with you. Find what the first one missed. Nothing built after 13:07 today has been seen in game (not the antler attack, not MonsterSize, not the reach change, not the riders or the market), so engine-behaviour claims in this change are the highest-value targets.

## TAOM ID CHEATSHEET

Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

## READ FIRST

- docs/reviews/rca-animalia-2026-09-23.md (the earlier internal review, Mike's decisions D1 to D5, what was fixed and what was not applied)
- docs/features/monster-size.md (the size design, its engine claims, the expected log line)
- docs/features/animalia-elk-moose.md (design, riders, tuning, in-game checklist)
- docs/features/elk.md (the great elk, the closest sibling)
- docs/reference/lotrlome-animalia-changes.md (the live Armory ledger and redo order)
- docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md (the 13-stage procedure)
- .claude/rules/csharp-architecture.md sections "Engine-Float Decision Gates" and "Mission-scope agent handles and the engine's threads"

## KNOWN SUSPECTS (CONFIRM or DISPUTE each, with code)

KS1. The undeclared attribute. The live Monster elements now carry taom_body_length, which the engine's Monsters.xsd does not declare. Trace the v1.5.3 load path for the "Monsters" XML (MBObjectManager.LoadXML / GetMergedXmlForManaged / XmlResource / the schema validation step / MergeElements / Monster.Deserialize, or wherever it really happens). Hypothesis: validation reports one error per element and the element still loads with every attribute. If instead the validation error drops the element or the whole file, or throws, then Monster.taom_animalia_elk / taom_animalia_moose / taom_elk are missing and every item naming them breaks: say exactly what happens, with the decompiled lines. Also confirm the session's claim that a module-supplied Monsters.xsd would crash the merge.

KS2. Timing and persistence of the size write. At OnGameInitializationFinished, are all Horse ItemObjects loaded and final for every game type TAOM supports (Campaign, CampaignStoryMode, CustomGame, EditorGame), for a new campaign and for loading a save? Is the ItemObject list rebuilt per game (so the write must repeat, as the code assumes)? Does anything read HorseComponent.BodyLength BEFORE this point and cache a derived value (ItemObject.Effectiveness is handled; check Tier / Tierf, value or price models, HorseComponent-derived stats, party speed or herding, anything in MBObjectManager's post-load passes), or re-deserialize items AFTER it (so BodyLength snaps back to 100)? Does the session's claim "no mission has built a mount yet" hold for every game type?

KS3. Mount scale. Find the exact v1.5.3 code that turns HorseComponent.BodyLength into the mount agent's AgentScale at spawn (Mission spawn paths, AgentBuildData, Agent.Build, the mount creation), and confirm ElephantLikeReach.Scale(Agent.AgentScale) therefore reads 1.0 / 1.5 / 1.1 for the three animals. Is there any other path (horse racing, the tableau, the map party visual, the arena, the crafting or inventory preview) where a BodyLength change could misbehave, for example a party visual scaled 1.5x on the campaign map?

KS4. Reflection targets. Confirm in the installed DLLs that HorseComponent.BodyLength has a private setter reachable by AccessTools with that name, that ItemObject.CalculateEffectiveness exists with the signature MonsterSizeCatalogAdapter calls, and that ItemObject.Effectiveness has a setter it can use. Confirm the recompute matches what the engine computes at load, and that the adapter fails safe (logs, no crash, no partial state) if any target is missing after an engine update.

KS5. The antler attack under the shared engine. act_animalia_*_antler are actt_kick. Confirm the attack fires and completes for AI and player riders (#643 made every elephant-like tree attack under a player rider), that the chosen action type is not read by the engine as rear or being-struck, that the single-target Blunt blow with the rider as owner behaves as the great elk's does (kill-or-wound, knockback, combat log), and that reach scaling cannot produce a zero, negative, NaN or huge radius. Check the moose's per-animal numbers against the docs and the clip frame counts pinned in AnimaliaConfigTests.

KS6. Riders and the campaign. Walk thranduil_bat_equipment, the five medium templates, the four lord and ruler battle rosters, the career start and mirkwood_rochenlas: every Horse id resolves in the live Armory, every Horse slot has a HorseHarness (the elk saddle taom_elk_saddle_a), a new campaign gives the moose to the lords, and an existing save keeps each hero's saved battle equipment. The market: confirm EnsureGuaranteedStock really stocks taom_animalia_elk_a although it is is_merchandise="false", that no pool builder, price model or daily consumption blocks or immediately removes it, and that the moose never appears.

KS7. The Armory writer and the guard. apply_animalia_armory.py: idempotent, one write per file, a dry run that proves the whole plan, refuses while the game or Kit runs; tools/_gamedir.py game_or_kit_running: which process names it checks and whether a running Modding Kit or a launcher-started game can slip past it. gen_animalia_anim_clips.ps1 -Verify and wire_anim_master_skeletons.ps1: do their verify and census modes really prove what the docs say?

## FILES

C# production (uncommitted; untracked folders are read whole, git diff will not show them):
- Main/Features/MonsterSize/MonsterSizeConfig.cs, HorseItemRecord.cs, IMonsterSizeService.cs, MonsterSizeService.cs, MonsterSizeIoC.cs
- Main/Adapters/IMonsterSizeCatalogAdapter.cs, Main/Adapters/MonsterSizeCatalogAdapter.cs
- Main/Features/Animalia/AnimaliaConfig.cs, AnimaliaCombat.cs, AnimaliaBehaviorTree.cs, AnimaliaMissionBehavior.cs, AnimaliaIoC.cs, IAnimaliaElkAttackService.cs, IAnimaliaMooseAttackService.cs, AnimaliaElkAttackService.cs, AnimaliaMooseAttackService.cs
- Main/Features/ElephantLike/ElephantLikeReach.cs (new); git diff of Main/Features/ElephantLike/BehaviorTreeElements/ElephantLikeCombatProfile.cs, ElephantLikeEngageDecorator.cs, ElephantLikeAttackTasks.cs
- git diff of Main/Features/Elk/ElkConfig.cs, ElkCombat.cs, ElkMissionBehavior.cs
- git diff of Main/IoC.cs (two registration lines) and Main/SubModule.cs (the ApplyMonsterSizes call in OnGameInitializationFinished; the AnimaliaMissionBehavior registration)
- Comment-only: the unstaged hunk of Main/Features/AdvancedCombat/CustomAttacksUtils.cs, Main/Features/CareerSystem/Abilities/CareerAgentStatService.cs, ICareerAgentStatService.cs
- Read-only context: the rest of Main/Features/ElephantLike/**, Main/Features/AdvancedCombat/CreatureTreeTracker.cs, Main/BehaviorTrees/BehaviorTreesCore.cs, Main/Features/Elk/*, Main/Features/WarRam/*, Main/Features/CultureMarketplace/* (CultureItemPoolService, CultureMarketplaceMaintenanceService, CultureMarketplaceBehavior)
Tests:
- TAOM.Tests/Features/MonsterSize/MonsterSizeServiceTests.cs, MonsterSizeWiringTests.cs
- TAOM.Tests/Features/ElephantLike/ElephantLikeReachTests.cs
- TAOM.Tests/Features/Animalia/AnimaliaConfigTests.cs, AnimaliaAttackServiceTests.cs, AnimaliaMountWiringTests.cs
- git diff of TAOM.Tests/Features/Elk/ElkConfigTests.cs, ElkMountWiringTests.cs, TAOM.Tests/Features/Elephant/HowdahPrefabTests.cs, TAOM.Tests/Features/Mumakil/MumakilPlatformTests.cs, TAOM.Tests/BehaviorTreeWrapper/BehaviorTreeMissionLogicInheritanceTests.cs
Repo data:
- git diff of Main/_Module/ModuleData/troops/troops_mirkwood.xml, equipmentsets/taom_equipment_sets_mirkwood.xml, equipmentsets/taom_lord_template_equipment.xml, equipmentsets/taom_career_starting_equipment.xml, culture_marketplace/culture_marketplace_config.xml
- Main/_Module/ModuleData/troops/troops_animalia_test.xml and its XmlNode in Main/_Module/SubModule.xml (in c79a5852)
Live Armory data (E:\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules\LOTRLOME_Armory; unversioned; compare each with its backup to see this session's change):
- SubModule.xml vs SubModule.xml.bak-animalia-20260923
- ModuleData\Monsters\LOTR\lotr_monster_animalia.xml (new, read whole)
- ModuleData\Monsters\LOTR\lotr_monster_elk.xml vs lotr_monster_elk.xml.bak-sizes-20260923
- ModuleData\action_sets.xml vs action_sets.xml.bak-animalia-20260923 (one hunk near line 61675)
- ModuleData\action_types.xml vs action_types.xml.bak-animalia-antler-20260923
- ModuleData\LOTRLOME_items\LOTRAOM_horses.xml vs LOTRAOM_horses.xml.bak-animalia-20260923
- ModuleData\Languages\loc_LOTRAOM_horses.xml vs loc_LOTRAOM_horses.xml.bak-monstersize-20260923
- In-repo snapshot: git diff of docs/reference/lotrlome-armory-snapshot/action_sets.xml, action_types.xml
Tools:
- tools/apply_animalia_armory.py, tools/tests/test_apply_animalia_armory.py, tools/gen_animalia_anim_clips.ps1, tools/wire_anim_master_skeletons.ps1 (whole files)
- git diff of tools/_gamedir.py, tools/skeleton_hit_capsules.py, tools/validate_xml_schemas.py, tools/tests/test_gamedir.py, tools/tests/test_skeleton_hit_capsules.py, tools/tests/test_validate_xml_schemas.py
- tools/blender/reskin_animalia_to_horse.py, retarget_animalia_to_horse.py, measure_animalia_clips.py and their JSON (in c79a5852)
Docs: docs/features/monster-size.md, animalia-elk-moose.md, the git diff of docs/features/elk.md, docs/reference/lotrlome-animalia-changes.md, lotrlome-elk-changes.md, docs/ai-includes/quadruped-pack-to-horse-skeleton-workflow.md, the #646 entry of CHANGELOG.md

## REQUIRED SECTIONS

### VANILLA CODE
Engine source for v1.5.3: the installed DLLs are authoritative (E:\Steam\steamapps\common\Mount & Blade II Bannerlord\bin\Win64_Shipping_Client and the Modules\*\bin folders). Pre-decompiled files: C:\Users\mikew\.taom-src\v1.5.3\ (for example TaleWorlds.ObjectSystem.MBObjectManager.cs, TaleWorlds.ObjectSystem.XmlResource.cs if present, TaleWorlds.Core.HorseComponent.cs, TaleWorlds.Core.ItemObject.cs, TaleWorlds.Core.Monster.cs, TaleWorlds.MountAndBlade.Mission.cs, TaleWorlds.MountAndBlade.Agent.cs, TaleWorlds.MountAndBlade.MBSubModuleBase.cs); pwsh tools/taom-src.ps1 path <Type> finds a type; the browseable dump E:\Decompiled_Bannerlord\ may lag; the ilspy MCP is available. Paste the decompiled lines that prove or refute each engine claim, with file:line. Specifically: the Monsters XML load and validation path and what an undeclared attribute does; GetMergedXmlForManaged and its game-type filter; MergeElements and XsdElementDictionary; HorseComponent.Deserialize and the BodyLength property; ItemObject.CalculateEffectiveness, Effectiveness and every reader of BodyLength; where MBSubModuleBase.OnGameInitializationFinished is raised relative to item loading for each game type; the mount AgentScale derivation at spawn.

### FEATURE-SPECIFIC DEEP ANALYSIS (walk each scenario, state the result)
S1. A new campaign on this tree: the game-init log line MonsterSize prints, the three items' BodyLength and Effectiveness, Thranduil on a 1.5x moose, mirkwood_beleglas on the 1.1x great elk, mirkwood_rochenlas on the 1.0x Animalia elk.
S2. Loading a save made with the released v2.0.30 build: which heroes keep which saved mounts, whether the sizes apply again on load, anything that snaps back.
S3. Custom Battle with the two test riders, then back to the menu and a second Custom Battle in the same process: sizes, trees, reach.
S4. A player riding a lord's moose charges an enemy: the antler attack's trigger range and radius at 1.5x, damage, owner, cooldown against the clip length.
S5. A Mirkwood town market on day 1 and day 10: the Animalia elk's stock, price, and whether a player on the elk_rider start who lost the elk can buy and ride it.
S6. A modder sets taom_body_length="abc", "0", "5000" or declares the same Monster id twice across modules: what the service logs and writes.
S7. The engine updates and HorseComponent.BodyLength loses its setter: what the adapter and the game do.

### CONFIG CROSS-REFERENCE
- Every Horse and HorseHarness id in the four repo equipment and troop files against the live Armory items.
- act_animalia_elk_antler / act_animalia_moose_antler and the clip names bound to them against AnimaliaConfig and the tests.
- The Monster ids and action-set ids across lotr_monster_animalia.xml, action_sets.xml, AnimaliaConfig, AnimaliaMissionBehavior and the tests.
- taom_body_length values against MonsterSizeConfig's bounds and the docs; the items' placeholder 100 against Items.xsd.
- Every culture id in the changed data against the cheatsheet (mirkwood is valid).

### FINDINGS OR OBSERVATIONS
For each: severity (P0 critical, P1 high, P2 medium, P3 low), file:line, what is wrong, the proving code, the minimal fix. Then UNVERIFIED items with the in-game check that would settle each. Then things you checked that are correct (briefly).

## QUALITY GATES
- Every engine claim carries pasted decompiled code with file:line; no claim from memory.
- Do not flag vanilla-matching behaviour as a TAOM bug; do not flag the out-of-scope files above.
- Do not re-report what the RCA lists as NOT APPLIED or as Mike's decision unless you bring new evidence.
- Distinguish pre-existing exposure from what this change introduced or extended.
- Before calling a test missing, grep TAOM.Tests for it.

## PRIOR REVIEW LESSONS
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

## OUTPUT
Return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-animalia-2026-09-23.md; do NOT write that path yourself, and do not create other files.

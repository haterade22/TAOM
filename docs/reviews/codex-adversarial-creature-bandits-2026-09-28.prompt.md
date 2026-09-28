Adversarial review: Creature Bandits (#692), TAOM for Bannerlord v1.5.3. Role: REVIEWER (see AGENTS.md "Start here" and .ai/roles/reviewer.md). Review only: do NOT edit, create, stage or delete any file, and do NOT run git commands that change state. If you run tests, use only `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` (never ./build.ps1, which deploys into the game install).

WHAT IS UNDER REVIEW
The working tree of E:/repos/taom-creature-bandits (a git worktree, branch feat/creature-bandits, base commit 743818cf). Everything is UNCOMMITTED: see `git -C E:/repos/taom-creature-bandits diff 743818cf --stat` plus the untracked files from `git -C E:/repos/taom-creature-bandits status --short`. Your working directory is that worktree.

The feature: hostile bandit parties of giant spiders that fight with no rider, the Mirkwood broods (culture and clan `mirkwood_spiders`, hidden troops `taom_spider_brood_forest`, `taom_spider_brood_brown`, `taom_spider_brood_pale`). In a field battle a `Mission.SpawnTroop` prefix (Patch93) replaces the creature troop's spawn with `Mission.SpawnMonster` and wires origin, Character, tuned HP, team and flags. Route A makes soldiers target it: a transpiler on the private `Mission.CreateAgent(Monster, ...)` adds CanWieldWeapon to the native creation flags (so the native weapon state is allocated, which the soldiers' target scorer reads unchecked), its postfix strips the flag before the build, and after the team step `CreatureRouteAUnmount` unlists the creature from the loose mounts and clears Mountable (native IsEnemy takes a Mountable agent's side from its rider). The creature runs its own behaviour tree (hunt, the spider's pounce and swipe with per-attack caps, a deployment gate and a hold). Tunable numbers and damage-taken rules come from a top-level MCM group "Creature Bandits". On the campaign map a behaviour spawns up to four broods around Mirkwood (switch "Spawn Spider Broods", default on); Patch94 draws the map icon as the spider alone and skips the encounter conversation; creatures are never prisoners.

An eight-lens internal review plus a convergence pass already ran; every finding in docs/reviews/rca-creature-bandits-2026-09-28.md is fixed or decided. Do not re-report those unless the fix itself is wrong. Your value is what they missed.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST
- docs/features/creature-bandits.md (design, MCM table, known limitations, strip list)
- docs/research/creature-bandits-roadmap.md, section "Targeting" (route A evidence, native function names)
- docs/reviews/rca-creature-bandits-2026-09-28.md (what is already found and fixed)
- docs/reference/harmony-patch-registry.md, sections Patch93_CreatureBandits and Patch94_CreatureBroodCampaign
- .ai/review-reference.md "Lessons From Prior Reviews"

ENGINE SOURCE
Installed v1.5.3 decompiles: C:/Users/mikew/.taom-src/v1.5.3 (one .cs per type, e.g. TaleWorlds.MountAndBlade.Mission.cs, SandBox.Missions.MissionLogics.BattleAgentLogic.cs). Signatures from the installed DLLs: `pwsh tools/taom-src.ps1 path <Type>`. Native code: `python tools/native_decompile.py --rva 0x<rva>` or `--engine-method <name>` (docs/features/ghidra-native-decompile.md). The dump at E:/Decompiled_Bannerlord may lag; trust the installed DLLs.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with code evidence)
1. Campaign battle accounting. The prefix returns the creature from SpawnTroop and skips SpawnTroopWithAgentBuildData. Hypothesis: the spawn loop's counters (MissionBattleSideSpawnContext, the troop supplier, NumberOfActiveTroops, reinforcement waves) and SandBox's BattleAgentLogic and casualty handling (killed vs wounded vs routed, the MapEvent's DiedInBattle and WoundedInBattle rosters, XP, TroopUpgradeTracker) stay consistent for a creature, and a battle whose enemy side is only creatures ends normally via IsSideDepleted. The internal review only ran console spawns (BasicBattleAgentOrigin); the PartyGroupAgentOrigin path has never run in game.
2. Deployment. CreatureMayFightDecorator gates the fight on Mission.AllowAiTicking && !Mission.IsTeleportingAgents and CreatureHoldTask holds the creature by a scripted move to its own position. Hypothesis: across every deployment controller a field battle can use (campaign BattleDeploymentMissionController, Custom Battle's), the creature neither moves nor strikes before FinishDeployment, and the hold cannot teleport it anywhere but its own spot. Also: what native AI does to a route A creature (Mountable cleared, CanAttack kept, no weapon) during the ticks before its tree attaches.
3. Route A scope. CreatureWeaponStateScope is [ThreadStatic], armed only around the spawner's own SpawnMonster call. Hypothesis: exactly one CreateAgent happens inside that scope, no other agent can consume the armed flag, the postfix always strips it before BuildAgent, and a throw anywhere in SpawnMonster leaves the scope disarmed (spawner's finally) with no agent keeping CanWieldWeapon. Check Patch93_CreatureBanditWeaponState's transpiler, postfix and finalizer against the installed IL of Mission.CreateAgent.
4. Worker threads. The weapon guards, the rout postfix and the morale models' CanPanicDueToMorale clause run on TWParallel workers and call CreatureBanditAgents.Is (Character, IsHuman, RiderAgent) and Interlocked diag counters. Hypothesis: all reads are safe off the main thread and nothing there allocates, logs or touches a non-concurrent collection except the documented first-guard WARNING.
5. Campaign spawner. CreatureBroodSpawnBehavior: BanditPartyComponent.CreateLooterParty with id clan.StringId + "_1", NavigationHelper.FindPointAroundPosition around an anchor's gate, the patrol order, no SyncData. Hypothesis: party ids stay unique across days and saves, a failed or invalid spawn point cannot place a brood in water or off the navmesh, and save/load keeps broods patrolling. Also check TaomBanditDensityModel's looter cap of 0 for the clan and vanilla BanditSpawnCampaignBehavior's other paths (hideouts, boss parties) so vanilla never spawns this clan.
6. Damage rules. CreatureBanditDamage.Reduce runs at the end of TaomCombatMechanicsModel.ApplyDamageReductions (after the refuge reduction) and in TaomCustomBattleCreatureDamageModel (extends CustomAgentApplyDamageModel, declared by the feature module, added after SubModule's own Custom Battle models). Hypothesis: every connected melee and missile hit on a creature passes exactly one of them once, and no other TAOM AgentApplyDamageModel registration displaces the Custom Battle one.

FILES
Feature code: Main/Features/CreatureBandits/ (CreatureBanditRules.cs, CreatureBanditsConfig.cs, CreatureBanditTuning.cs, CreatureWeaponStateScope.cs, CreatureBanditLog.cs, CreatureBanditsModule.cs, CreatureBanditBehaviorTree.cs, CreatureBanditMissionBehavior.cs, CreatureBroodSpawnBehavior.cs, BehaviorTreeElements/*.cs, Hooks/*.cs, Models/TaomCustomBattleCreatureDamageModel.cs; Diagnostics/*.cs is temporary playtest logging, review it only for thread safety and for breaking an engine callback).
Shared code changed: Main/Features/Spider/ (SpiderStrikes.cs new; SpiderAttackService.cs, ISpiderAttackService.cs, SpiderMissionBehavior.cs, SpiderBehaviorTree.cs, BehaviorTreeElements/SpiderAttackTaskBase.cs, BehaviorTreeElements/SpiderEngageDecorator.cs), Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs, Main/Features/CareerSystem/Models/TaomAgentApplyDamageModel.cs, Main/Features/CultureDoctrine/Models/{TaomBattleMoraleModel,TaomCustomBattleMoraleModel,TaomCustomBattleAgentStatCalculateModel}.cs, Main/Features/CulturalFeats/Models/TaomBattleRewardModel.cs, Main/Features/BanditManagement/Models/TaomBanditDensityModel.cs, Main/Features/DevConsole/Cheats/MissionSpawnCheats.cs, Main/Features/TaomSettings.cs (the Creature Bandits block at the end), Main/Composition/FeatureModules.cs, Main/SubModule.cs (unload reset), Dependencies/Foundation/PatchShieldPolicy.cs.
Data: Main/_Module/ModuleData/characters/creature_bandits.xml, characters/clans.xml (faction mirkwood_spiders), taom_spcultures.xml (culture mirkwood_spiders), taom_partyTemplates.xml (mirkwood_spiders_brood_template), taom_module_strings.xml and Languages/*/std_taom_module_strings_*.xml (4 new keys), Main/_Module/SubModule.xml (new NPCCharacters node), tools/taom_schema.py (two allowlists).
Tests: TAOM.Tests/Features/CreatureBandits/, TAOM.Tests/Features/Spider/SpiderStrike*.cs, TAOM.Tests/Features/DevConsole/MissionSpawnOriginTests.cs and the changed shared tests in `git diff`.

REQUIRED SECTIONS
1. VANILLA CODE: paste as code blocks the engine methods your findings rest on, at minimum Mission.SpawnTroop, Mission.SpawnMonster, the private Mission.CreateAgent(Monster, ...), BattleAgentLogic.OnAgentRemoved, the spawn loop around MissionBattleSideSpawnContext.cs:370-390, DeploymentMissionController.SetupTeams/FinishDeployment, and CommonAIComponent.CanPanic.
2. DEEP ANALYSIS per Known Suspect, with a concrete scenario each (a campaign battle of 25 player troops against a brood of 1 pale + 5 forest + 2 brown spiders; the player retreats; the player wins; a save and load with broods on the map; Custom Battle with the console command).
3. CONFIG CROSS-REFERENCE: the XML ids against CreatureBanditsConfig (troop ids, BroodClanId, the 13 anchor settlement ids in the live E:/Steam/steamapps/common/Mount & Blade II Bannerlord/Modules/TAOM_Map/ModuleData/settlements.xml), the party template's first stack, the culture and clan attributes against vanilla looters (SandBox spclans.xml, spcultures.xml).
4. FINDINGS OR OBSERVATIONS: each with severity (HIGH, MEDIUM, LOW), file:line, the failure scenario, and the proving code. Mark anything resting on engine or native behaviour you could not read as UNVERIFIED. Say explicitly when a suspect is DISPUTED.

QUALITY GATES
- Every claim about TAOM code cites a file:line you read; every engine claim cites the decompile you read.
- Do not flag code that matches vanilla behaviour as a bug; say how vanilla does it.
- Do not skip a hard section; if you run out of time, say which section is incomplete.
- Distinguish "cannot happen today" from "cannot happen".

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT
Return the full report as your FINAL MESSAGE. The dispatcher redirects your stdout into docs/reviews/raw/codex-adversarial-creature-bandits-2026-09-28.md; do NOT write that file (or any file) yourself.

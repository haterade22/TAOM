# Codex Adversarial Review -- Recruitable Giant Spider Troop (TAOM, Bannerlord v1.4.5)

You are an adversarial code reviewer. Assume there ARE bugs. Verify every claim against TAOM source AND decompiled vanilla. For signatures, run ilspycmd on the INSTALLED v1.4.5 DLLs at "E:/Steam/steamapps/common/Mount & Blade II Bannerlord/bin/Win64_Shipping_Client/" -- do NOT trust the E:/Decompiled_Bannerlord folder for signatures. CONFIRM or DISPUTE each Known Suspect with evidence (paste the decompiled code you relied on).

## Feature

A recruitable giant-spider TROOP. Bannerlord cannot host a non-humanoid creature as an NPCCharacter (race resolves against a humanoid-only skins.xml), so the troop uses a humanoid anchor (taom_spider_creature, race dg_uruk) for recruitment/roster/UI, and a thin Harmony Prefix on Mission.SpawnAgent swaps the agent body to the spider Monster at spawn time. This replaced an abandoned rideable-mount approach (which crashed on the campaign-map party-icon path) -- do NOT review the mount path, it is deleted.

## TAOM ID CHEATSHEET

Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar.
"dol_guldur" is NOT valid -- the id is "dolguldur". The spider troop id is "taom_spider_creature"; the spider Monster id is "spider" (defined in the external LOTRLOME_Armory module).

## READ FIRST

- docs/features/spider.md (the feature doc, just rewritten for the troop approach)
- docs/reviews/rca-spider-troop-2026-06-04.md (the deep-review RCA + the prior mount-crash lesson)
- Main/Features/Spider/SpiderConfig.cs (owns SpiderMonsterId="spider", SpiderCharacterId="taom_spider_creature")

## Known Suspects (CONFIRM or DISPUTE each with evidence)

1. Patch45 (Main/Features/Spider/Hooks/Mission_SpawnAgent_SpiderSwap_Patch.cs) is a Prefix on Mission.SpawnAgent -- the UNIVERSAL agent-spawn chokepoint, runs for every agent in every battle. Prove it can never throw or crash a spawn (null-guards on _service and agentBuildData) and never returns false (never skips vanilla). It co-exists with Patch23_BannerColorPersistence (Main/Features/BannerColorPersistence/Hooks/Mission_SpawnAgent_Patch.cs), also a Prefix on the same method. Both must return true in all paths. Decompile Mission.SpawnAgent to confirm the param name "agentBuildData" binds the Harmony prefix. Is there any inter-prefix ordering hazard given both run at default priority?

2. HIGHEST VALUE: SpiderMissionBehavior (Main/Features/Spider/SpiderMissionBehavior.cs) only sets _managesCombatInfrastructure=true (and thus ticks SpatialGrid.UpdateGrid + IBoneCollisionService.TickBoneChecks) when NEITHER AdvancedCombatBehavior NOR WargMissionBehavior is present in the mission. In a normal CAMPAIGN field battle, AdvancedCombatBehavior IS present, so SpiderMissionBehavior does NOT own the grid. Trace whether spider bite attacks still fire in that case: SpiderAttackService (Main/Features/Spider/SpiderAttackService.cs) -> IBoneCollisionService (is it a DryIoc singleton shared with AdvancedCombatBehavior?) -> does AdvancedCombatBehavior tick bone-collision for ALL agents including spider-bodied ones? The risk: the spider may have only ever bitten in the now-removed Custom-Battle smoke test (where it owned the grid), and may be inert in real battles. Read Main/Features/AdvancedCombat/ to confirm or refute.

3. taom_spider_creature is added (weight 1) to the 4 Dol Guldur settlement pools + the dolguldur culture fallback in VolunteerRecruitmentService (NOT the clan-path pool). Settlement pools feed BOTH player and AI lord recruitment, so AI Dol Guldur lords can field spiders. Confirm a riderless monster-bodied agent in an AI-controlled formation does not crash AI logic (formation positioning, target selection). If you cannot rule it out, flag it as a risk with the specific vanilla call path.

4. spider_creature.xml has level="21". For the troop to be offered as a volunteer it must map to a tier <= MaxVolunteerTier. Verify TaomVolunteerModel (Main/Features/.../TaomVolunteerModel*.cs) MaxVolunteerTier value and the level->tier formula (CharacterObject.Tier / Campaign tier model). Confirm level 21 yields a tier <= the cap. If it exceeds the cap, the spider is in the pool but never surfaces.

5. BT-attach coverage in SpiderMissionBehavior: a one-shot scan on the first OnMissionTick (when !_treesAdded) attaches the SpiderTree to existing spider agents and sets _treesAdded=true; OnAgentBuild only attaches when _treesAdded==true. Trace whether any spider can fall through BOTH paths (e.g. a spider built on the exact first tick, or between mission init and the first tick). Initial-deployment vs reinforcement-wave timing.

6. Cross-mission lifecycle: _spiderComponents is cleared in OnRemoveBehavior; bone-collision is cleared by AdvancedCombatBehavior.OnRemoveBehavior. When SpiderMissionBehavior is NOT the grid owner, is there any state leak across missions (BT components, SpatialGrid.Instance, IBoneCollisionService registrations)?

## Files

NEW:
- Main/Features/Spider/ISpiderTroopSpawnService.cs
- Main/Features/Spider/SpiderTroopSpawnService.cs
- Main/Features/Spider/Hooks/Mission_SpawnAgent_SpiderSwap_Patch.cs
- TAOM.Tests/Features/Spider/SpiderTroopSpawnServiceTests.cs

MODIFIED:
- Main/Features/Spider/SpiderConfig.cs
- Main/Features/Spider/SpiderMissionBehavior.cs
- Main/Features/Spider/SpiderIoC.cs
- Main/Features/TroopProgression/VolunteerRecruitmentService.cs
- TAOM.Tests/Features/TroopProgression/VolunteerRecruitmentServiceTests.cs
- Main/_Module/ModuleData/characters/spider_creature.xml
- Main/_Module/SubModule.xml
- Main/_Module/ModuleData/troops/troops_dolguldur.xml (deleted the dg_giant_spider_rider block)
- Main/IoC.cs
- Main/SubModule.cs

CONTEXT (unchanged, but read to trace flows 2/5/6):
- Main/Features/Spider/SpiderAttackService.cs + SpiderBehaviorTree.cs + BehaviorTreeElements/*
- Main/Features/AdvancedCombat/ (AdvancedCombatBehavior, SpatialGrid, IBoneCollisionService)
- Main/Adapters/AgentAdapter.cs (IsSpider)

DELETED: Main/Features/Spider/SpiderSpawnerService.cs + ISpiderSpawnerService.cs + TAOM.Tests/Features/Spider/SpiderSpawnerServiceTests.cs.

## Required sections in your output

1. KNOWN SUSPECTS -- CONFIRMED/DISPUTED per item (1-6 above), each with the decompiled or source evidence you relied on.
2. VANILLA CODE -- paste the decompiled Mission.SpawnAgent signature + the tier-model method you used for suspect 4.
3. ADDITIONAL FINDINGS -- anything beyond the suspects (severity HIGH/MED/LOW).
4. CONFIG CROSS-REFERENCE -- verify spider_creature.xml ids (culture, race, default_group enum), the volunteer pool ids, and that the string "taom_spider_creature" / "spider" are identical across XML + C# const + pool entries + patch.
5. VERDICT -- ship / needs-fixes, with the must-fix list.

## Quality gates

- Do NOT flag the .Monster() swap or in-game render as bugs -- they are untestable engine boundaries (human seam).
- Do NOT flag vanilla-matching code as a bug; if you claim a missing gate, paste the vanilla code that has it.
- The 4-legs-vs-8 per-mesh bone-render limit is a known human-seam item (mount-path symptom, unproven on the live-agent path) -- note it only if you find evidence it affects the troop path; do not speculate.

## Prior review lessons

SUCCESSES: config ID cross-ref catches rohan/dol_guldur mismatches; vanilla decompilation catches missing gates; lifecycle tracing catches stale caches.
FAILURES to avoid: do NOT assume empire=Rohan (empire=Dunland); do NOT flag vanilla-matching code as bugs; do NOT skip the hard sections (suspect 2 is the hard one -- do it).

Output your full review to stdout.

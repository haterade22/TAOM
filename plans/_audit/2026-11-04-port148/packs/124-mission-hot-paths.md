# Input pack: WP 124 mission-hot-paths

Title (draft): Cheaper battle hot paths: lock-free formation patch, warg and creature tick costs, behaviour-tree walks
Wave: 3   Lane: B   Draft priority: P2   Groups: 4   Port commits: 14
Depends on (WP ids): 106, 123

## Orchestrator notes for this WP (binding)
- Plan 015 (DECISIONS #28) squashed after #634 (WP106).
- New: cap BehaviorTree.RunTree's node walk (FREEZE-THREADING-09) with a test.

## Verification and decision reports to read (Workflow 2)
- plans/_audit/2026-11-04-port148/verify/V05-concurrent-collections-and-rng.md
- plans/_audit/2026-11-04-port148/verify/K3-wp-map-critic.md
- plans/_audit/2026-11-04-port148/verify/K1-skip-feature-critic.md

## Deep-dive findings and non-port items this WP owns
- RUNTIME-PERF-05 [P2/high] MixedFormations Patch30 on Formation.GetOrderPositionOfUnit runs on the worker pool: allocation, MCM read, global lock, unlocked engine query re-evaluation | port: 13f34c1cb  (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)
- RUNTIME-PERF-10 [P2/high] Creature battles: DateTime.Now tree timers, per-evaluation IoC.Resolve in warg nodes, per-target Skeleton wrappers during bites, xyz grid keys, unconditional grid rebuild | port: 87f9f862e,e5f644da4,7577894d4,fe8f30c7a,56eb4bc81,23f6f85bd,5d9d4cc97,efe326242  (report: plans/_audit/2026-11-04-port148/deepdive/runtime-perf.md)
- FREEZE-THREADING-09 [P3/low] BehaviorTree.RunTree walks nodes in an uncapped loop on the main thread | port:   (report: plans/_audit/2026-11-04-port148/deepdive/freeze-threading.md)

## Port groups owned by this WP (from the commit classification)

### group `plan-015-warg-tick-costs`  P2  track=perf-mission  commits=10  sim: clean=2 conflict=8
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/AdvancedCombat/BoneCheck.cs, Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs, Main/Features/AdvancedCombat/SpatialGrid.cs, Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs, Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs, Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs, Main/Features/Warg/WargBehaviorTree.cs, Main/Features/Warg/WargMissionBehavior.cs, Main/Features/Warg/WargRiderHandManager.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs, TAOM.Tests/Features/Warg/WargTickCostTests.cs
- `87f9f862e` 2026-09-24 PORT P2 pick=conflict :: perf(warg): v2.0.30 - resolve tree services once, scan into buffers
  - why: Removes per-tick DryIoc resolves from the warg tree nodes and the rider-hand manager and makes the enemy scans fill reused buffers instead of allocating a List<Agent> per tick; the same per-tick resolves exist on 1.4.x.
  - port notes: The WargMissionBehavior.cs conflict is textual: f75288ebf (#634, not on 1.4.x) added the _deferred lines in the constructor context. Port #634 first or resolve by hand. The per-node field resolves and the WargRiderHandManager.Tick(factory) plumbing added here are replaced later (fe8f30c7a removes the plumbing, 23f6f85bd moves resolution to WargBehaviorTree.BuildTree), so port plan 015 squashed to its final state at 516380d2e. Covers PERF-07 of plan 003, still TODO on 1.4.x.
  - prereqs: f75288ebf
  - conflicts: Main/Features/Warg/WargMissionBehavior.cs
- `b8b144635` 2026-09-24 PORT P2 pick=conflict :: refactor(combat): v2.0.30 - pin SpatialGrid queries behind helpers
  - why: Moves SpatialGrid's cell build and radius query into Agent-free generic helpers (BuildCells, CollectInRadius) with a brute-force comparison test and reads each position once instead of three times; behaviour unchanged, and the prerequisite for the x,y keying.
  - port notes: The SpatialGrid.cs conflict comes only from f75288ebf (#634), which split Remove into a deferred RemoveNow; without #634, hand-apply around the 1.4.x Remove method.
  - prereqs: f75288ebf
  - conflicts: Main/Features/AdvancedCombat/SpatialGrid.cs
- `e5f644da4` 2026-09-24 PORT P2 pick=conflict :: perf(combat): v2.0.30 - key SpatialGrid cells on x and y only
  - why: Keys SpatialGrid cells on (x, y) instead of (x, y, z), cutting a 60 m scan from 343 to 49 cell lookups with the same result set; on 1.4.x the grid serves every creature query (wargs, spiders, AgentAdapter strikes).
  - port notes: Applies after b8b144635. Later 1.5.x plan 033 (efe326242) builds on this grid, so this is a prerequisite for porting 033.
  - prereqs: b8b144635
  - conflicts: Main/Features/AdvancedCombat/SpatialGrid.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs
- `7577894d4` 2026-09-24 PORT P2 pick=clean :: perf(combat): v2.0.30 - range-gate bone targets before skeletons
  - why: Range-gates bone-check targets before fetching their skeleton, because each MBAgentVisuals.GetSkeleton call builds a new NativeObject wrapper (native ref count, a lock and a GCHandle pin; v1.4.8 NativeObject.Construct), and fetches the attacker skeleton once per tick.
  - port notes: Picks clean. The `is null` choice holds on v1.4.8: Skeleton derives from NativeObject (Engine/TaleWorlds.Engine/Skeleton.cs:9 in the v1.4.8 dump) and NativeObject's explicit static constructor calls native code (taom-src v1.4.8 NativeObject.cs:62), so `== null` would run it in the test host.
  - issues: 219
- `2420761ba` 2026-09-24 PORT P3 pick=clean :: docs(warg): v2.0.30 - record the warg tick-cost changes
  - why: Updates warg-combat.md and advanced-combat.md performance and test notes to describe the plan 015 changes.
  - port notes: Picks clean; both docs are identical at fork and on 1.4.x.
  - prereqs: 87f9f862e, e5f644da4, 7577894d4
- `fe8f30c7a` 2026-09-24 PORT P2 pick=conflict :: fix(warg): v2.0.30 - review follow-ups for plan 015
  - why: Plan 015 review fixes: the moved range gate becomes a positive test so a NaN visuals frame never reaches the native skeleton fetch, WargRiderHandManager reads the mount's Monster directly, adapter lookups wait until a candidate passes, and the IL tests are hardened.
  - port notes: Conflicts are series prerequisites plus the #634 context in SpatialGrid.cs and WargMissionBehavior.cs. Review record, prompt, RCA and lessons optional; drop the CHANGELOG hunk.
  - prereqs: 87f9f862e, b8b144635, e5f644da4, 7577894d4
  - conflicts: Main/Features/AdvancedCombat/BoneCheck.cs, Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs, Main/Features/AdvancedCombat/SpatialGrid.cs, Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs, Main/Features/Warg/WargMissionBehavior.cs, Main/Features/Warg/WargRiderHandManager.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs, TAOM.Tests/Features/AdvancedCombat/SpatialGridQueryTests.cs, TAOM.Tests/Features/Warg/WargTickCostTests.cs
- `56eb4bc81` 2026-09-24 PORT P3 pick=conflict :: fix(warg): v2.0.30 - convergence fixes for plan 015
  - why: Convergence fixes: strengthens the mixed-list CheckTargets test and deletes IAgentAdapter.IsWarg(), dead once fe8f30c7a replaced its last caller.
  - port notes: On 1.4.x IsWarg's only caller is WargRiderHandManager.cs:14, which fe8f30c7a rewrites, so apply strictly in order.
  - prereqs: fe8f30c7a
  - conflicts: Main/Features/Warg/WargRiderHandManager.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckRangeGateTests.cs
- `23f6f85bd` 2026-09-24 PORT P2 pick=conflict :: fix(warg): v2.0.30 - apply maintainer decisions for plan 015
  - why: Maintainer decisions for plan 015: WargBehaviorTree.BuildTree resolves the two services once per tree and injects them into the four nodes, and BoneCheckDuringAnimation.Tick reads action progress once and fetches the attacker skeleton only when the hit window opens.
  - port notes: One behaviour change (a missing attacker skeleton during wind-up ends the bite when the window opens, not at once); the warg Custom Battle check is owed on 1.4.x too. The IL order test added here is replaced by substitute tests in 5d9d4cc97.
  - prereqs: fe8f30c7a, 56eb4bc81
  - conflicts: Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs, Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs, Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs, Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs, TAOM.Tests/Features/Warg/WargTickCostTests.cs
  - issues: 659
- `5d9d4cc97` 2026-09-24 PORT P2 pick=conflict :: fix(warg): v2.0.30 - review follow-ups for plan 015
  - why: Replaces the weak IL order rule with substitute-driven BoneCheckDuringAnimation.Tick tests and adds WargTreeNodeInjectionTests, plus comment and doc corrections.
  - port notes: The body states the test premise for v1.5.3, but it holds on v1.4.8: ActionIndexCache is a public readonly struct with no explicit static constructor and its operator != reads only Index (taom-src v1.4.8 ActionIndexCache.cs:5 and 482-485). Run the suite on the 1.4.x port to confirm. Review records optional; drop the CHANGELOG hunk.
  - prereqs: 23f6f85bd
  - conflicts: Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs, Main/Features/Warg/WargBehaviorTree.cs, TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs, TAOM.Tests/Features/Warg/WargTickCostTests.cs
- `516380d2e` 2026-09-24 PORT P3 pick=conflict :: fix(warg): v2.0.30 - convergence fixes for plan 015
  - why: Adds a csharp-architecture.md rule that a moved or reordered engine-float gate gets its NaN test in the same commit, plus RCA and doc corrections for plan 015.
  - port notes: .claude/rules/csharp-architecture.md is identical at fork and on 1.4.x, so the two-line rule applies cleanly. Final plan 015 commit before its merge.
  - prereqs: 5d9d4cc97
  - conflicts: TAOM.Tests/Features/Warg/WargTickCostTests.cs
  - issues: 178
skipped in this group: `66a85b085` SKIP-BOOKKEEPING docs(changelog): v2.0.30 - warg tick-cost entry for plan 015

### group `plan-032-worker-thread-formation-patch`  P2  track=perf-mission  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs, Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs, Main/Features/MixedFormations/FormationLayoutService.cs
- `13f34c1cb` 2026-10-04 PORT-PARTIAL P2 pick=conflict :: perf(mixed-formations): v2.0.32 - lock-free path for plain formations
  - why: Patch30 on Formation.GetOrderPositionOfUnit runs for every AI unit on TWParallel workers; formations with no TAOM layout are now answered from a lock-free identity map, with no adapter allocation, no global lock and no formation query.
  - port notes: On 1.4.x, Patch30 allocates a FormationAdapter and calls ComputeUnitPlanePosition on every field-battle call. Take FormationLayoutService (FindLaidOutFormation, the snapshot keyed by ReferenceIdentity, NoteFallback counting), Patch30, IFormationLayoutService, FormationAdapter, FormationLayoutServiceTests, Patch30ThreadSafetyIlTests, the two .claude/rules edits and the docs. Skip the CreatureBandits wield-guard reorder and its tests (1.5.x-only feature).
  - conflicts: Main/Features/CreatureBandits/Hooks/CreatureBanditAgents.cs, Main/Features/CreatureBandits/Hooks/Patch93_CreatureBandits.cs, Main/Features/MixedFormations/FormationLayoutService.cs
  - issues: 714

### group `plan-033-creature-battle-allocations`  P2  track=perf-mission  commits=1  sim: clean=0 conflict=1
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Features/AdvancedCombat/SpatialGrid.cs
- `efe326242` 2026-10-04 PORT-PARTIAL P2 pick=conflict :: perf(creatures): v2.0.32 - fewer allocations in creature battles
  - why: Cuts allocations in creature battles: the behaviour trees use DateTime.UtcNow, the selector reuses its child lists, BehaviorTreeMissionLogic returns before allocating or parking when no tree listens to a callback, and the spatial grid reuses its maps and skips rebuilds nothing queried.
  - port notes: This is shared code. 1.4.x has the same DateTime.Now calls in the BT nodes and the same Defer parking in BehaviorTreeMissionLogic (it came in 0c3234375), so the parking part does not need f75288ebf. Take the BT clock change, the selector reuse, the listener counts with CallbackSkipLedger, and their tests directly. The SpatialGrid part sits on 1.5.x grid changes that 1.4.x lacks (f75288ebf, b8b144635, e5f644da4, fe8f30c7a); port those first or re-derive it on the 1.4.x grid. Plan 003 (grid staleness), still TODO on 1.4.x, edits the same file.
  - prereqs: f75288ebf, b8b144635, e5f644da4, fe8f30c7a
  - conflicts: Main/Features/AdvancedCombat/SpatialGrid.cs
  - issues: 715

### group `behavior-tree-framework-perf`  P2  track=perf-mission  commits=2  sim: clean=0 conflict=2
non-bookkeeping conflict paths (merge-tree onto 1b00d1acd): Main/Composition/FeatureModules.cs, Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs, Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/CreatureBandits/Models/TaomCustomBattleCreatureDamageModel.cs, Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs, Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs, Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs, Main/Features/RaceAbilities/Cheats/RaceAbilitiesCheats.cs, Main/Features/RaceAbilities/Domain/RaceAbilityDefaults.cs, Main/Features/RaceAbilities/Domain/RaceAbilitySenses.cs, Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityActivator.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityHooks.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityNames.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityRuntime.cs, Main/Features/RaceAbilities/Hooks/RaceAbilitySensor.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityStatApplier.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityTicker.cs, Main/Features/RaceAbilities/RaceAbilitiesConfigProvider.cs, Main/Features/RaceAbilities/RaceAbilitiesModule.cs, Main/Features/RaceAbilities/RaceAbilityBehaviorTree.cs, Main/Features/RaceAbilities/RaceAbilityProfileResolver.cs, Main/Features/RaceAbilities/RaceAbilityReportClock.cs, Main/Features/RaceAbilities/RaceAbilityService.cs, Main/Features/RaceAbilities/RaceAbilitySettingsProvider.cs, Main/Features/RaceAbilities/RaceAbilityStore.cs, Main/Features/RaceAbilities/RaceAbilityTelemetry.cs, Main/Features/TaomSettings.cs ...
- `a498e357e` 2026-10-04 PORT-PARTIAL P3 pick=conflict :: feat(race-abilities): v2.0.32 - battle abilities per race and culture
  - why: Race Abilities is a new 1.5.x feature and stays out, but this commit also reorders the shared BehaviorTreeAgentComponent.TickOnMissionThread so its two native agent checks run only when a tree is due.
  - port notes: Take only the TickOnMissionThread reorder in Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs; the 1.4.x method is identical to the 1.5.x parent. Leave everything else (RaceAbilities, the model hooks, the CultureDoctrine and CreatureBandits models, docs). The gain on 1.4.x is small, because only creature trees use the framework there.
  - conflicts: Main/Composition/FeatureModules.cs, Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs, Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs, Main/Features/CoopInterop/CoopSettingsRelevance.cs, Main/Features/CreatureBandits/Models/TaomCustomBattleCreatureDamageModel.cs, Main/Features/CultureDoctrine/Models/TaomBattleMoraleModel.cs, Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs, Main/Features/CultureDoctrine/Models/TaomCustomBattleMoraleModel.cs
- `70320bbe1` 2026-10-04 PORT-PARTIAL P2 pick=conflict :: fix(race-abilities): v2.0.32 - second review: kill credit, leaner scans
  - why: Mostly Race Abilities review fixes, which stay out, plus a shared BehaviorTreeMissionLogic fix: the per-frame schedule snapshot used List.AddRange, which on .NET Framework allocates a temporary array of the whole schedule every frame.
  - port notes: Take only the BehaviorTreeMissionLogic.OnMissionTick change that fills _tickScratch element by element; origin/bannerlord-1.4.5 BehaviorTreeMissionLogic.cs:129 still calls _tickScratch.AddRange(_scheduled). Leave the RaceAbilities code, the CareerSystem, CombatMechanics, CultureDoctrine and CreatureBandits model edits, and the docs.
  - conflicts: Main/Features/CareerSystem/Models/TaomAgentStatCalculateModel.cs, Main/Features/CombatMechanics/Models/TaomCombatMechanicsModel.cs, Main/Features/CreatureBandits/Models/TaomCustomBattleCreatureDamageModel.cs, Main/Features/CultureDoctrine/Models/TaomCustomBattleAgentStatCalculateModel.cs, Main/Features/RaceAbilities/Cheats/RaceAbilitiesCheats.cs, Main/Features/RaceAbilities/Domain/RaceAbilityDefaults.cs, Main/Features/RaceAbilities/Domain/RaceAbilitySenses.cs, Main/Features/RaceAbilities/Hooks/RaceAbilitiesMissionLogic.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityActivator.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityDeaths.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityHooks.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityNames.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityRuntime.cs, Main/Features/RaceAbilities/Hooks/RaceAbilitySensor.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityStatApplier.cs, Main/Features/RaceAbilities/Hooks/RaceAbilityTicker.cs, Main/Features/RaceAbilities/RaceAbilitiesConfigProvider.cs, Main/Features/RaceAbilities/RaceAbilitiesModule.cs, Main/Features/RaceAbilities/RaceAbilityBehaviorTree.cs, Main/Features/RaceAbilities/RaceAbilityProfileResolver.cs
  - issues: 730

## Classifier batch notes to read
plans/_audit/2026-11-04-port148/data/batch_notes.md, sections: batch 6, batch 13

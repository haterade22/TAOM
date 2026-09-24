# Verify batch A-07 (adversarial checker)

Baseline `b2e387db`. HEAD is `4b5662b2`; `git diff --stat b2e387db HEAD -- Main/Features/Warg Main/BehaviorTreeWrapper Main/BehaviorTrees` is empty and `git status --short` shows no working-tree change under those paths, so every line below was read with `git show b2e387db:<path>`.

## F4 (seed): Warg BT nodes and rider-hand manager resolve IoC per tick

**Outcome: CONFIRMED, narrowed.** Every cited line is an uncached `IoC.Resolve` exactly where the seed says, but "per tick" (per mission frame) holds for two of the five sites only. The other three run at event, rage-cycle or per-attack cadence.

**What I re-read and how the cadence was derived**

- `IoC.Resolve<T>()` is a plain `_container.Resolve<T>()` with no cache (`Main/IoC.cs:236-239`, DryIoc 4.8.8 per `Main/TAOM.csproj:100`). `IMissionAdapterFactory` is a singleton (`Main/IoC.cs:226`); `IWargAttackService` is `Reuse.Transient` (`Main/Features/Warg/WargIoC.cs:9`).
- The warg tree is built with `base(10)` (`Main/Features/Warg/WargBehaviorTree.cs:22`). `BehaviorTreeAgentComponent.TickOnMissionThread` tests `(Tree._rootEvaluationDelay / 1000) < timeSinceLastEvaluation` with an `int` field (`Main/BehaviorTreeWrapper/BehaviorTreeAgentComponent.cs:65`, `Main/BehaviorTrees/BehaviorTreesCore.cs:36`), so `10 / 1000 == 0` and `RunTree` is called every frame. `RunTree` resumes from `CurrentNode` and returns early while a node waits (`BehaviorTreesCore.cs:52-79`, `BehaviorTreesNodes.cs:52-62`), so per-frame cost depends on the tree state.
- `Selector.Prepare` calls `Evaluate()` on EVERY child decorator (`Main/BehaviorTrees/Nodes/BehaviorTreesNodes.cs:131-155`), and `BTReturnFalseDecorator` has no listener (`BehaviorTreesCore.cs:353-356`).
- Tick listeners fire on a repeating timer from `BehaviorTreeMissionLogic.OnMissionTick` (`Main/BehaviorTreeWrapper/BehaviorTreeMissionLogic.cs:146-162`).

**Per site**

| Site | Resolve call used at | Cadence (source-traced) | Per tick? |
|---|---|---|---|
| `Main/Features/Warg/WargRiderHandManager.cs:14` | same line, 1 resolve | `WargMissionBehavior.OnMissionTick` calls `WargRiderHandManager.Tick()` every frame after the first second (`WargMissionBehavior.cs:86-87,115`); the behavior is added to every mission unconditionally (`git show b2e387db:Main/SubModule.cs`, line 1960). Runs whenever `Agent.Main.HasMount`, warg or not. | **Yes**, 1 per frame (not per warg) |
| `CheckOnceIfCanAttackEnemy` getter, `Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs:45` | `:59-60`, 2 per enemy non-mount agent within 10 m, until the first likely hit | Decorates the "hit enemy" child of the "has rider" selector (`WargBehaviorTree.cs:87`), so `Prepare` evaluates it every root cycle. When an enemy is within 60 m (`NoEnemyCloseDecorator` false), none is likely to be hit and the warg is not raging (`WargHitByEnemyDecorator` false, `WargHitByEnemyDecorator.cs:18-22`), all three "has rider" children fail, the whole tree finishes in one `RunTree` and resets, and the next frame starts over. `IsAttackLikelyToHit` rejects anything beyond `1 m + forward speed` (`Main/Adapters/AgentAdapter.cs:158-160`), so the loop usually walks every enemy in the 10 m list. | **Yes**, the real hot site: 2 x (enemies within 10 m) per ridden warg per frame |
| `PeriodicallyCheckIfCanAttackAnyone` getter, same file `:15` | `:29-30`, same shape | Only inside rage mode (`WargBehaviorTree.cs:42,50,73`). It derives from `WaitNSecondsTickDecorator(0.2)` (`:17`) but overrides `Evaluate` with the scan, so it runs on selector prepare and then on each 0.2 s tick-listener wake (`BehaviorTreeMissionLogic.cs:146-157`, `BehaviorTreesNodes.cs:165-179,187-201`). | No: about 5 Hz per raging warg |
| `Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs:16` | `:26-27`, 2 resolves per evaluation, no loop | Rage AI branch only (`WargBehaviorTree.cs:77`); evaluated once per "rage ai logic" prepare; when true the branch sleeps 250 ms (`:79`). | No: at most about 4 Hz per raging AI warg |
| `Warg/BehaviorTreeElements/WargAttackTask.cs:30-31` | same lines, 2 resolves; the second builds a new transient `WargAttackService` each time | A task, run once per attack; every use is followed by a `SleepTask` of 1 s or `WargConfig.SleepAfterAttack` = 3 s (`WargBehaviorTree.cs:52-53,74-75,88-89`, `WargConfig.cs:36`). | No: at most about 1 Hz per warg |

**Refutation attempts that failed**

- Guard elsewhere: none. No cache in `IoC.Resolve`; `MissionAdapterFactory.GetAgentAdapter` caches adapters, not the factory (`Main/Adapters/MissionAdapterFactory.cs:27-33`), and still takes a lock per call.
- Dead code: no. All five nodes are in the built tree (`WargBehaviorTree.cs:50,52,73,74,77,87,88`) and the tree attaches to every agent whose Monster passes `WargConfig.IsWargMonster` (`WargMissionBehavior.cs:152-164`).
- Test pin: none. `git grep` over `TAOM.Tests` for the five type names finds only a comment (`TAOM.Tests/Features/Warg/WargAttackServiceTests.cs:17`), a doc mention (`WargMonsterIdTests.cs:12`) and an off-thread check (`WargOffThreadTests.cs:30`); nothing asserts resolves are cached.
- Already fixed: no. The fix commit `4962f3ee` ("perf(warg): lazy-cache adapter factory in BT decorators", touches only `PeriodicallyCheckIfCanAttackAnyone.cs`) is NOT an ancestor of `b2e387db` (`git merge-base --is-ancestor`).
- By design: no. `AGENTS.md:39` requires "Cache `IoC.Resolve` lazily on a hot path"; no ADR, rule or trap-index row exempts BT nodes. The BT nodes are constructed by TAOM's own `BuildTree` with `new X()`, not by the engine, so the lane-1 "engine-instantiated boundary" reasoning covers the service-locator style, not the per-frame cost.

**Corrected evidence**: the per-tick claim should cite `WargRiderHandManager.cs:14` and `PeriodicallyCheckIfCanAttackAnyone.cs:45` (used at `:59-60`, cadence from `BehaviorTreeAgentComponent.cs:65` plus `BehaviorTreesNodes.cs:131-155`). Downgrade `PeriodicallyCheckIfCanAttackAnyone.cs:15` (`:29-30`) to about 5 Hz in rage mode, `WargAiControlledIsNotFacingEnemy.cs:16` (`:26-27`) to at most about 4 Hz in rage AI, and `WargAttackTask.cs:30-31` to once per attack (at most about 1 Hz; add the `Reuse.Transient` detail from `WargIoC.cs:9`).

**Impact today: LOW.** A DryIoc singleton resolve is a cached-delegate lookup; the per-frame count is 1 for the player plus 2 x (enemies within 10 m) per engaged ridden warg. Wall-clock cost is UNMEASURED (no build or profiler run in this pass). It is a standing-rule violation (`AGENTS.md:39`) and worth folding into the plan 003 PERF-07 rewrite with PERF-L4-04, not a plan on its own.

**Side note (same pattern, outside F4's scope)**: `git grep -n "=> IoC.Resolve" b2e387db -- Main` finds 13 static resolve-getters; three more sit in combat code (`Main/Features/AdvancedCombat/BaseBehaviorTree/LogTask.cs:10`, `BoneCheck.cs:12`, `TaomBTLogger.cs:8`). Their cadence was not traced here.

## What I did not cover

- No build, test or in-game run; the cost per resolve and the frame-time share are unmeasured.
- DryIoc 4.8.8 internals were not decompiled; "cached-delegate lookup" is from the library's documented design, not read this run.
- The Spider tree and the three extra static getters in the side note were not cadence-traced.
- The player-controlled rage branch (`WargBehaviorTree.cs:43-61`) was read but not traced step by step; it uses the same `PeriodicallyCheckIfCanAttackAnyone` and `WargAttackTask` nodes, so its cadence is bounded the same way.

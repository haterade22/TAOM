# Verify batch A-06: adversarial re-check of PERF-L4-03, PERF-L4-04, F3

Checker: fresh adversarial pass. HEAD is `4b5662b2`; `git diff --stat b2e387db 4b5662b2 -- Main/Features/Warg
Main/Features/AdvancedCombat Main/Adapters/AgentAdapter.cs Main/BehaviorTreeWrapper Main/BehaviorTrees
Main/Features/Enlistment` is empty, so every C# citation was read via `git show b2e387db:<path>` and holds at
HEAD too. Engine reads from the v1.5.3 `taom-src` cache (`C:\Users\mikew\.taom-src\v1.5.3\`). No build, no
test run.

## PERF-L4-03: warg bone check builds native wrappers for far targets before its distance gate

**Outcome: CONFIRMED (mechanism), with three small citation corrections. Impact today: LOW to MED
(frame cost UNMEASURED, scale numbers are the finder's estimates).**

What holds on my own re-reading:

- Only producer: `git grep -n "\.CustomAttack(" b2e387db -- Main` finds one caller,
  `Main/Features/Warg/WargAttackService.cs:151`. The only production `new BoneCheckDuringAnimation` is
  `Main/Adapters/AgentAdapter.cs:215`; the two factory methods in `BoneCollisionService.cs:12-26` are called
  only from `TAOM.Tests/Features/AdvancedCombat/BoneCollisionServiceTests.cs` (no production caller).
- Capture: `AgentAdapter.cs:207-210` keeps every agent from `GetNearAliveAgentsInRange(targetDetectionRange)`
  except the warg, its rider and inactive agents. No team filter; the team test is only in the hit callback
  (`WargAttackService.cs:43-45`). `targetDetectionRange` is `WargConfig.TargetDetectionRange = 20f`
  (`WargConfig.cs:35`).
- Per frame: `AdvancedCombatBehavior.cs:36` calls `TickBoneChecks(dt)` on every mission tick (the grid rebuild
  below it is the throttled part). `BoneCheckDuringAnimation.Tick` reads `_agent.AgentVisuals` and
  `GetSkeleton()` (`:39-41`), then `CheckBoneCollision` reads both again (`BoneCheck.cs:61-68`) and allocates
  `new List<(sbyte, Vec3)>` (`:76`).
- Per target, before any distance test: `IsActive()`, `IsFadingOut()` (`:94`), `AgentVisuals` (`:101`),
  `GetSkeleton()` (`:109`), `GetGlobalFrame()` (`:116`), then inside `FindBoneInRange` also
  `targetSkeleton.GetBoneCount()` (`:131`, a native call the finding did not list) before the only gate at
  `:132`. `_maxRangeForCheck = Math.Max(20f, r*r*20f)` (`BoneCheck.cs:30`): r is 1.0 (running) or 0.5
  (stand) at `WargAttackService.cs:137,148`, so the gate is 20 m^2 (about 4.47 m) in both cases.
- Wrapper costs, read in the decompile: `AgentAdapter.AgentVisuals` (`AgentAdapter.cs:36-39`) re-runs engine
  `IsActive()` and native `IsFadingOut()` and returns `new AgentVisualsAdapter(...)` per read.
  `AgentVisualsAdapter.GetSkeleton()` (`Main/Adapters/AgentVisualsAdapter.cs:16`) goes to
  `MBAgentVisuals.GetSkeleton()` (`TaleWorlds.MountAndBlade.MBAgentVisuals.cs:145-148`) and
  `ScriptingInterfaceOfIMBAgentVisuals.GetSkeleton` (`:776-786`), which does `new Skeleton(pointer)` every call
  plus a native `DecreaseReferenceCount`. `Skeleton(UIntPtr)` calls `Construct` (`TaleWorlds.Engine.Skeleton.cs:15-18`);
  `NativeObject.Construct` (`TaleWorlds.DotNet.NativeObject.cs:32-43`) does a native
  `IncreaseReferenceCount`, takes `lock (_nativeObjectKeepReferences)`, allocates a keeper and a
  `GCHandle.Alloc(this)` released after 10 ticks (`:115-132`), and the class has a finalizer that makes a
  native call (`:45-51`). `Agent.IsFadingOut()` is native (`TaleWorlds.MountAndBlade.Agent.cs:3338-3341`).
- The target's `IsActive()` includes `AgentSlotIdentity.IsCurrentOccupant` (`AgentAdapter.cs:68`,
  `AgentSlotIdentity.cs:19-26`), which calls `Mission.FindAgentWithIndex`.

Refutation attempts that failed:

- Guard elsewhere: none. The capture list is not re-filtered by distance; far targets stay in `_targets`
  and are re-examined every frame until the clip leaves its window or they die.
- Dead code: no, it is the live warg attack path.
- By design: no ADR, rule or BRIEF tradeoff covers bone-check ordering. `.claude/rules` and the trap index
  say nothing about it.
- Test pinning: tests exist for the service's tick loop, none pins the per-target order (and none could
  measure the cost).

Corrections:

1. `Mission.FindAgentWithIndex` is public at `TaleWorlds.MountAndBlade.Mission.cs:5221-5224`; the cited
   `:2164-2171` is the private `FindAgentWithIndexAux` it delegates to (which does hold the native call).
2. Frequency window: the bone check itself runs for progress 0.0 to 0.9 of the running clip and 0.1 to 0.5 of
   the stand clip (`WargAttackService.cs:135-136,146-147`, `BoneCheckDuringAnimation.cs:43,49`), i.e. 0.9 or
   0.4 of the clip, not "0.5 to 0.9".
3. The per-target native-call count before the gate is higher than the finding's 5: `GetBoneCount()` at
   `BoneCheck.cs:131` also precedes the gate, and `IsFadingOut` runs twice (once in `:94`, once inside the
   `AgentVisuals` getter).

Side effect worth noting for the fix (not a perf claim): because allies are captured and
`stopOnFirstHit` is true (`WargAttackService.cs:151`), an ally within bone reach ends the attack
(`BoneCheck.cs:120-123` invokes the callback, which returns early for a same-team victim, then returns false).
Dropping same-team targets at capture therefore changes behavior (an ally no longer consumes the swing), which
is the finder's "must not change which targets can be hit" risk in a slightly different form.

Magnitude stays UNMEASURED: the 1,240 wrappers and 74,000 finalizable objects a second rest on assumed
densities (80 riders, 20 concurrent attacks, 60 agents inside 20 m). The mechanism is fully read; the impact
number is an estimate.

## PERF-L4-04: warg tree's per-frame enemy scans allocate and walk a 7x7x7 cell box

**Outcome: CONFIRMED. Impact today: LOW (shape fully read; frame cost UNMEASURED; scale numbers are
estimates). It extends June PERF-06 (triage-A marked it FIXED but noted "whether hot callers moved to the
buffer form was not checked"): they did not.**

What holds on my own re-reading:

- Allocating calls: `NoEnemyCloseDecorator.cs:17` (60 m), `PeriodicallyCheckIfCanAttackAnyone.cs:23` and
  `:52` (10 m). `git grep -n "GetNearAliveAgentsInRange\|GetAgentsInRadius" b2e387db -- Main` shows the
  buffer overload used by `SpiderEngageDecorator.cs:50` and `AgentAdapter.cs:255` only; no warg node.
  The allocating overload builds `new List<Agent>()` per call (`SpatialGrid.cs:86-91`), and the buffer
  overload's doc (`:93-98`) asks per-eval BT hot paths to use it.
- 3D bounding box: `SpatialGrid.cs:105-117` loops x, y and z at `CellSize = 20f` (`:22`, not overridden
  anywhere: `git grep CellSize`). For radius 60, `(c+60)/20 - (c-60)/20 = 6` exactly, so every axis spans 7
  cells: 343 `TryGetValue` calls. For radius 10 the span is 2 per axis: 8. The in-code comment at `:112-114`
  claims the bbox is "at most about 27 cells" for creature scan ranges, which the 60 m warg scan contradicts.
- Every frame: `BehaviorTreeMissionLogic.OnMissionTick` calls `TickOnMissionThread` for every scheduled tree
  each tick (`BehaviorTreeMissionLogic.cs:126-143`); `BehaviorTreeAgentComponent.cs:65` compares
  `(Tree._rootEvaluationDelay / 1000)` with int division, and the warg tree passes 10
  (`WargBehaviorTree.cs:22`; field is `int` at `BehaviorTreesCore.cs:36`), so the left side is 0 and `RunTree`
  runs every tick. (Every creature tree passes `base(10)`: Elephant, Elk, Mumakil, Spider, WarRam, Warg.)
- Stronger than claimed: `Selector.Prepare` (`BehaviorTreesNodes.cs:131-146`) evaluates EVERY child's
  decorator up front, so the "has rider" selector runs `NoEnemyCloseDecorator` (60 m scan),
  `WargHitByEnemyDecorator` and `CheckOnceIfCanAttackEnemy` (10 m scan) on every entry, whichever wins.
  All three are `BTReturnFalseDecorator`, not event decorators, so when all fail the selector finishes with
  false (`:148-152`), the root completes, and the next tick starts over: no parking. The tree only stops
  re-scanning while a `SleepTask` is Running (the Sequence/Selector `IsWaitingASingleTime` exit,
  `:233-240`, `:335-342`), i.e. for 1 s after "no enemy close" and 3 s after an attack.
- `Selector.Prepare` also allocates two new lists per entry (`:134-135`); not in the finding, same class.

Refutation attempts that failed: no caching layer between the decorators and the grid; no buffer field on
any warg node; no ADR or BRIEF tradeoff covers scan allocation (the SpatialGrid doc argues the opposite);
no test pins the allocating call. The integer-division note is correct and harmless at 10 ms.

Correction: none to the citations. Scale caveat: "80 engaged wargs" all scanning every frame ignores the
3 s post-attack sleep (and the finder's own PERF-L4-03 duty-cycle estimate), so the 19 MB/s figure is an
upper bound, not a measurement.

## F3: enlistment settlement-dwell anchor survives a reload or new campaign

**Outcome: CONFIRMED, with the evidence widened (the new-campaign path is not even reached by
`ResetSessionCaches`). Impact today: LOW (no crash, no save effect, self-heals at the commander's next
battle; the worst case is a long stall in a town the commander has left).**

What holds on my own re-reading (all `git show b2e387db:`):

- The anchor: `ServiceAttachmentService.cs:34` `private double? _settlementEntryHours` (its own doc calls it
  "Session state"), written only at `:41` (`StampSettlementEntry`) and nulled only at `:231`
  (`ExitSettlementForService`). `IsWithinSettlementDwell` (`:43-44`) is
  `HasValue && nowHours - anchor < 6.0`: a NEGATIVE elapsed (anchor in the future) reads as "within dwell".
- Lifetime: `EnlistmentIoC.cs:31` registers it `Reuse.Singleton`. `git grep` over `Main` and `TAOM.Tests`
  finds no other writer: nothing resets it on load or on a new game.
- The reset that misses it: `ServiceMaintenanceService.cs:217-241` clears the maintenance fields, the
  commander cache, the army handle and the reconciler latch, and its doc (`:212-215`) claims to be "the ONE
  place that knows the lifetime of the feature's per-session state". It never touches the dwell anchor, and
  `IServiceAttachmentService` exposes no reset for it.
- The consumer: `EnlistmentReconciler.cs:626` stamps `nowDays * 24.0` after a successful follow; `:641`
  defers `SettlementExitRequired` while `IsWithinSettlementDwell(nowDays * 24.0)` and the commander is not in
  a map event. `nowDays` is `CampaignTime.Now.ToDays` (`Hooks/EnlistmentBehavior.cs:109`).

Reachable path (reload of an earlier save, same process): the player is inside a follow-placed town at hour
H (anchor = H, still set because no service exit ran), then loads a save at hour L < H in which they stand in
a town with their commander. When the commander leaves, `Assess` returns `SettlementExitRequired`
(`ServiceAttachmentService.cs:112-113`), `L - H < 6` holds, and the exit is deferred until the clock passes
H + 6, i.e. (H - L + 6) campaign hours. Only a commander battle (the `!snapshot.PartyIsInMapEvent` yield at
`:641`, then `:231` nulls the anchor), a fresh follow re-stamp, or the player leaving by hand ends it.
`EnlistmentLoadNormalizer.cs` has no settlement or dwell handling (`grep -i settlement` finds only a comment
at `:94`).

Wider than the seed: `ResetSessionCaches` is called only from `OnGameLoaded` (`Hooks/EnlistmentBehavior.cs:115-122`);
`OnNewGameCreated` (`:124-130`) clears only the store. So even a fix inside `ResetSessionCaches` would not
cover a new campaign in the same process. The sibling anchor in the same class shows the intended pattern:
`EnlistmentReconciler.cs:44-52` documents both holes, and `:450-460` re-anchors when `nowDays` is behind the
stored anchor ("the guard for the path ResetForNewSession does not reach"). The dwell anchor has neither
guard. A one-term `elapsed >= 0` check in `IsWithinSettlementDwell` would cover both paths.

Refutation attempts that failed:

- By design: no. `.claude/rules/csharp-architecture.md:96-114` makes a session-reset story MANDATORY for
  "any absolute clock, latch or shown-flag" on a singleton; this is a rule violation, and the BRIEF's
  enlistment tradeoff (service ends only via `DischargeService`) is unrelated.
- Test pinning: `TAOM.Tests/Features/Enlistment/SettlementFollowingTests.cs:380-450` mocks
  `IsWithinSettlementDwell` to true or false; no test drives the real service with a backwards clock or a
  session reset.
- Guard elsewhere: none found (normalizer, reconciler, maintenance, behavior all read).

Corrected evidence: add `Hooks/EnlistmentBehavior.cs:115-130` (reset wired to load only),
`EnlistmentReconciler.cs:44-52,450-460` (the sibling guard this anchor lacks) and
`.claude/rules/csharp-architecture.md:96-114` (the rule). Delta: introduced, `ff47cebb` (2026-08-25),
after `141b749` (`git log -S"_settlementEntryHours"`, `git merge-base --is-ancestor`). Overlaps lane-3
CORRECTNESS-07, which names F3 as one instance of its class; count once.

## What I did not cover

- No build, test or in-game run; every frame-cost and stall-length figure is derived from code, not measured.
- Did not re-measure PERF-L4-03 and PERF-L4-04 scale numbers (agent densities, attack duty cycle) or read
  native `IMBAgentVisuals` / `IManaged` costs (native, unreadable).
- F3: did not verify in the engine that a new campaign's `CampaignTime.Now` starts below an earlier
  campaign's clock (the reload-earlier-save path does not depend on it); did not check whether the service
  wait menu offers the player a manual leave that would end the stall sooner.

Plan 033: cut per-frame allocation, finalizable native wrappers and O(N) scans in creature battles
(wargs, spiders, trolls, elephants and mumakil with crews, elk, war rams, creature bandits) and in the
always-on SpatialGrid, without changing what any creature does.

WHY. On the .NET Framework CLR every allocation on the main thread feeds gen0 collections, and a native
wrapper with a finalizer (Skeleton) adds finalizer-queue work and promotes objects. Plan 015 (#659, merged
e9b28e2a: 87f9f862, 7577894d, e5f644da, fe8f30c7, 5d9d4cc9, 56eb4bc8, 2420761b) fixed the warg tree's
per-tick resolves, scan buffers and skeleton fetches; read those commits first and do not redo them. Plan
003 (#664) was meant to fix the 2 s SpatialGrid staleness, but its merge 40615c22 touched only
BattleBalanceSettingsProvider: Main/Features/AdvancedCombat/AdvancedCombatBehavior.cs:16 still reads
GridUpdateInterval = 2f.

FACTS (audit 2026-10-02 at dffdf879; the writer re-reads each and quotes current code):
- Behaviour-tree framework (Main/BehaviorTreeWrapper/, Main/BehaviorTrees/): every tree runs every frame
  (BehaviorTreeAgentComponent.cs:65 computes `_rootEvaluationDelay / 1000` with an int field,
  BehaviorTreesCore.cs:36, and every TAOM tree passes base(10), so 10/1000 == 0: changing the delay
  changes creature reaction time, so leave the cadence alone and only fix what costs without effect).
  SleepTask.cs:22, :30, WaitNSecondsTickDecorator.cs:21, :24 and ElephantLikeAttackOffCooldownDecorator.cs:37
  call DateTime.Now (a time-zone conversion) per tree per frame: DateTime.UtcNow gives identical
  intervals (and is immune to a DST step). Selector.Prepare allocates two Lists on every re-entry
  (BehaviorTreesNodes.cs:131-135): reuse them (Clear). BehaviorTreeMissionLogic.OnAgentRemoved
  (:289-297) allocates object[] arrays with a boxed AgentState and KillingBlow on every removal whether or
  not any tree listens, and every off-main-thread event is deferred through a closure (:300-310,
  :340-347), including OnAgentShootMissile, which no TAOM tree subscribes to: return early when nothing
  subscribes to that event, before allocating or deferring. The nine creature trackers' PruneDead
  (CreatureTreeTracker.cs:114-122) runs per tracked creature per frame with a native FindAgentWithIndex:
  leave its cadence unless an equivalent cheaper check exists.
- Howdah and war-tower crews (Main/Features/Elephant/): TaomHowdahMachine.OnTick -> RepositionToElephant
  (:244-262) -> RepositionToFixedOffset (:346-375) -> TryReadAnchorBoneWorld (:268-317) calls
  visuals.GetSkeleton() every frame (:274); BoneCheck.cs:130-133 records that GetSkeleton builds "a new
  native wrapper on every call (a ref-count call, a lock, a GCHandle and a finalizer)". A code comment
  records 14 crewed elephants in one battle (TaomHowdahStandingPoint.cs:132). Cache the Skeleton per
  elephant agent for the mission only if the engine returns the same native skeleton for the agent's
  lifetime (verify with taom-src: AgentVisuals.GetSkeleton and what can replace it); otherwise read the
  bone every N frames with the same visual result (STOP and report if neither is provably equivalent).
- SpatialGrid (Main/Features/AdvancedCombat/SpatialGrid.cs): AdvancedCombatBehavior.cs:38-47 rebuilds it
  from Mission.Current.AllAgents every 2 s in every mission, creatures or not, allocating a new Dictionary
  and per-cell Lists each time (SpatialGrid.cs:70-87); RemoveNow (:61-67) walks every cell's List.Remove,
  O(N) per deletion, behind a RunOrDefer closure (:53); corpse fading (#701, c17541f2) raised the deletion
  count. SpatialGridDebugService.cs:13-16 uses the allocating GetNearAliveAgentsInRange overload while
  Left Alt is held. Change: skip the rebuild while no creature tracker holds an agent (find who queries
  the grid; it must not miss a creature that spawns later); reuse the dictionary and lists across
  rebuilds; keep an agent-to-cell map so removal is O(cell); use the buffer overload in the debug service.
- Creature-bandit hunt: Main/Features/CreatureBandits/BehaviorTreeElements/CreatureHuntTask.cs:74-97 loops
  every hostile team's ActiveAgents (IsHuman, IsActive, native Position per agent) every 250 ms per creature
  (SleepTask 250 ms, CreatureBanditBehaviorTree.cs:63-66). Change: query nearby enemies with a reused
  MBList (Mission.GetNearbyEnemyAgents or the grid) in an expanding radius and fall back to the full scan
  only when none is near. The chosen target must stay the nearest enemy human: a nearest hit inside radius
  r is the global nearest, so state that argument in a comment and test it. Note: yotthani reports (not
  verified by TAOM) that Mission.GetNearbyAgents returns active humans only, no mounts; the hunt targets
  humans, so it is unaffected, but do not rely on the query returning mounts anywhere.

TESTS: allocation-free paths proven by tests that count allocations through seams where possible (for
example the selector reusing its lists, the dispatcher not building arguments with no subscriber); the grid
skip, reuse and O(cell) removal with the same query results as before; the hunt choosing the same target
as the full scan across synthetic layouts (nearest inside the first radius, only beyond it, none at all);
the UtcNow change with a fake clock if the classes take one, else an IL rule. Every existing
BehaviorTree, Warg, Elephant, CreatureBandits and AdvancedCombat test stays green.

OUT OF SCOPE: tree cadence and any creature timing; Patch93 (plan 032); diagnostics defaults (FOR-MIKE).

# Plan review 033, round 1 (cold)

Plan: `plans/033-creature-battle-allocations.md`. Code read from the program worktree at HEAD `e9cd8b39`
(two docs-only commits past the planned-at `dffdf879`; `git diff --stat dffdf879..HEAD` touches no in-scope
code path). No earlier review file for 033 exists, so there are no prior blocking items to re-check.

## What was verified

- Every "Current state" code excerpt against the file: `BehaviorTreeAgentComponent.cs:59-70`,
  `BehaviorTreesCore.cs:36`, `SleepTask.cs:22,30`, `WaitNSecondsTickDecorator.cs:21,24`,
  `ElephantLikeAttackOffCooldownDecorator.cs:37`, `ElephantLikeAttackTasks.cs:42,56,117,133`,
  `SpiderAttackOffCooldownDecorator.cs:36`, `SpiderAttackTaskBase.cs:39,47,70,79`, `SetRageAttackTimer.cs:21,39`,
  `WargCanNotFindEnemyDecorator.cs:29`, `BehaviorTreesNodes.cs:34,124,131-155`,
  `BehaviorTreeMissionLogic.cs:64-77,167-172,239-396`, `SpatialGrid.cs:9-16,38-106,148-162`,
  `SpatialGridDebugService.cs:11-23`, `AdvancedCombatBehavior.cs:16,22,29-48`, `WargMissionBehavior.cs:58-62,89-99`,
  `SubModule.cs:2051`, `CreatureHuntTask.cs:14-29,74-97`, `CreatureBanditBehaviorTree.cs:63-66`,
  `TaomHowdahMachine.cs:268-278`. All match except the items under "Excerpt mismatches".
- Every grep the plan prescribes was run: `DateTime.Now` in `Main` (only the ten listed sites plus the
  out-of-scope loggers), the blackboard stamp grep (exact match with the plan's list), `": Selector\b"` (nothing),
  `"SubscribesTo ="` (only `BannerlordBTListener.cs:12`), `SubscriptionPossibilities` subscribers (`OnSelfRemoved`
  x2, `OnSelfIsHit`, `OnAgentRemoved`), the enum is 20 contiguous values from 0, `<Compile Include` count 0.
- Engine facts in the v1.5.3 decompile cache: `Mission.GetNearbyAgents(Vec2, float, MBList<Agent>)` clears and
  calls the `All` aux (`Mission.cs:6660-6664`), the aux pages 40 ids under a static lock and can add null
  (`:2330-2352`), `AllAgents` (`:1392`), `OnAgentDeleted` removes from `_allAgents` after the behaviours
  (`:2987-3000`), `OnAgentRemoved` deactivates in the same callback, `Agent.IsHuman`, `IsActive`,
  `GetHashCode` returns `_creationIndex` with no `Equals` override (nor on `DotNetObject`),
  `Vec3.DistanceSquared`, `MBList<T> : MBReadOnlyList<T> : List<T>`, `MissionBehavior`/`MissionLogic` have no
  constructor body or field initializer.
- Native facts, re-run with `python -B tools/native_decompile.py --engine-method IMBMission.GetNearbyAgentsAux`
  (RVA `0x6F70E0`) and `--engine-method IMBAgent.GetPosition` (RVA `0x6E0F90`): above the float at
  `DAT_180b2e010` the function walks the used-slot list (stride `0xC60`), keeps `(flags & 0x800) != 0` and
  state `== 1`, tests `dx*dx + dy*dy <= r*r` on `*(agent + 0x20) + 0xc / + 0x10`, and for type 3 skips the team
  test; `GetPosition` reads `*(agent + 0x20) + 0xc`. The plan's reading holds. (When a page fills at 40 the
  cursor is not advanced, so the next page can repeat one agent: harmless for a nearest pick.)
- `python -B tools/lint_docs.py --fail-on-drift` exits 0 on the worktree today.
- Commit subjects are at most 72 characters; `perf` is a type the changelog generator knows.
- No em or en dash in the plan (checked by code point), no secret, no absolute local path, no worktree or
  branch name; the "own branch" requirement is stated at lines 5-6 and 540.

## Blocking

1. **Step 8's RED oracle omits CS1061** (plan lines 843-845). The dormancy tests read `grid.BuildCount` on an
   instance, which fails with CS1061 ("does not contain a definition for 'BuildCount' and no accessible
   extension method"), not CS0117 or CS0103. Only the static `SpatialGrid.BuildCellsInto` /
   `SpatialGrid.RemoveFromCells` give CS0117. The plan says any other diagnostic is the executor's error to
   fix, so a weak executor will "fix" its correct test or STOP. Fix: expect CS0117 for `BuildCellsInto` and
   `RemoveFromCells` and CS1061 for `BuildCount`.
2. **Step 11's class-summary replacement quotes text that is not in the file** (plan lines 1105-1116). The real
   summary (`CreatureHuntTask.cs:18-20`) writes `<c>rca-spider-troop-2026-06-04.md:63</c>` and
   `<c>IsEnemyOf</c>`, and the sentence spans lines 18 to 20 mid-line; the plan quotes Markdown backticks. A
   literal search fails (a dispatch-rule STOP), and the supplied replacement would put Markdown backticks into
   an XML `///` comment. Fix: quote the real text with its `<c>` tags and give the replacement in `<c>` form.

## Non-blocking

1. **Mumakil skeleton fetch is not per frame** (plan lines 434 and 1208-1209). `TaomMumakilPlatform.cs:278` is
   inside `BoneProbe()` (`:273`), called only from `LogStatus()` (`:202`, `:258`), which runs when
   `_statusClock.Tick(dt) && _diagnostics?.IsEnabled == true` (`:93`). The FOR-MIKE note would mislead the
   maintainer; say "a diagnostics status probe" instead.
2. **The engine doc the plan cites does not exist at the planned-at commit.**
   `docs/reference/engine/mission-frame-threads-and-native-costs.md` (cited at lines 217, 371, 684) is absent
   at `dffdf879` (`git cat-file -e` fails); it arrived in `761b20fe`, and the worktree carries uncommitted edits
   to it. Line 459 says the graph was built from `e9cd8b39`. The needed facts are inlined, so the executor is
   not stopped, but either anchor "Planned at" on the commit the executor's branch starts from or say the doc
   comes with the program branch tip.
3. **The hunt's cost trade-off is unstated.** Radii 20 m and 60 m deliberately take the native walk over every
   used agent slot under a static lock (the engine doc's section 4 advises radii of 15 m or less on per-agent
   paths for exactly this cost). Whenever no enemy is within about 60 m (pre-contact, the usual state of a
   creature bandit at battle start), each hunt now pays two native slot walks plus today's full scan, more
   than today. The plan asserts a win without a measurement; the commit body's simplicity sentence (line 549)
   should name this cost, or the orchestrator should ask plan 028's profiler to measure it.
4. **The debug-service IL test never shows its RED** (lines 835-841). It is added in Step 8, where the project
   fails to compile for the other new files, and Step 9 makes it pass; dispatch rule 8 requires a test to fail
   against the code it guards. Add it first (before the grid test files), build, run
   `--filter "FullyQualifiedName~SpatialGridDebugServiceTests"` and quote its assertion failure. Also say how to
   read the IL: `IlCallScanner.ExtractCalledMethods` on `typeof(SpatialGridDebugService).GetMethod("RenderDebugVisualization")`,
   since `WargTickCostTests.AssertScansIntoABuffer` is private and hard-wired to `Evaluate`.
5. **Step 4 assertion order.** The Verify expects the failure on `currentlyExecutableChildren`; say to assert it
   before `childrenWithTasks`.
6. **Step 10 margin tests** (lines 1027-1028): specify an axis-aligned target from a zero origin (for example
   `(QueryRadii[0] - QueryMargin, 0, 0)`), so float rounding cannot flip the "exactly at the margin" case.
7. **The grid keeps the mission's agent list** (`_agents`, Step 9). In the warg-only path
   (`WargMissionBehavior.cs:58-62`, `SpatialGrid.Instance ??= new`) the instance survives into the next mission,
   and a query-triggered rebuild before that mission's first `UpdateGrid` would walk the previous mission's
   list. Today the same query reads the previous mission's cells, so it is not worse, and
   `AdvancedCombatBehavior` (which replaces the instance) is in every TAOM mission; one sentence in the
   equivalence notes would close it.
8. **Blast radius count** (line 466): `SpatialGridQueryTests` and `SpatialGridRemovalTests` hold 9 + 3 = 12
   methods, not eleven.
9. **Step 1 records no lint baseline**, yet Steps 9, 11 and 12 require `lint_docs.py --fail-on-drift` exit 0. It
   exits 0 today; add it to Step 1 so a later drift is attributable.

## Checklist results

- TDD order: RED before GREEN for all five changes (Steps 2, 4, 6, 8, 10); see non-blocking 4 for the one test
  whose RED is masked.
- Issue line, binding ADRs (002, 007, 008) and rules named; no protected file; `IoC.cs`, `SubModule.cs`, the
  csproj out of scope with a STOP; STOP conditions are specific to this plan's risks.
- Done criteria are commands except the standard re-check line; drift-check paths cover every Scope entry.
- Non-deploying commands carry `-p:DisableModuleCopy=true -p:ModuleId=`; no CHANGELOG step; no worktree path
  or branch name.

UNVERIFIED: the baseline totals (no dotnet run in a read-only review; they match `baseline.md`), the Step 4 run
count oracle (traced by reading `RunTree` and `Selector.HandleExecute`, consistent with two `Prepare` calls),
and the claim that the native slot walk covers every agent the managed `Team.ActiveAgents` holds (one pool is
walked; the doc mentions two embedded pools).

# Cold review: plan 062, round 1

Reviewer: cold plan reviewer, read-only. Plan read: `plans/062-riderless-mount-bite-side.md` (1227 lines).
Code read at `db5680c8a` in the run worktree (`git log -1` printed `db5680c8a`). Engine source read from the
v1.5.7 decompile cache that `tools/taom-src.ps1 path Agent|Mission|MissionBehavior|Team` resolved. No earlier
review file for plan 062 exists in this audit folder, so there is nothing to re-check from a prior round.

## Verdict

No blocking items. A weak executor can carry the plan out with only the plan and the repo; every step ends
in a command with an exact expected result. Eight non-blocking items follow.

## What I checked, and what held

- **Excerpts against the code (item 3).** Every excerpt matches the code at `db5680c8a` character for
  character: `AgentAdapter.cs:72-78`, `IAgentAdapter.cs:33`, `SpiderAttackService.cs:133-140` and `:193-202`,
  `WargAttackService.cs:35-45`, `AdvancedCombatBehavior.cs:54-77` (file is 90 lines, CRLF). The only label
  slip is in "Excerpt mismatches" below.
- **Other line cites.** All verified: `SubModule.cs:2318`, `MissionAdapterFactory.cs:24`, `BoneCheck.cs:155`,
  `CoopMissionSeamPinTests.cs:106`, `SpiderIoC.cs:10`, `WargIoC.cs:9`,
  `SubModuleMissionBehaviorIsolationTests.cs:137`, `SpatialGridRemovalTests.cs:63`, `CoopMissionRoles.cs:78`,
  the four decorator lines (`NoEnemyCloseDecorator.cs:21`, `PeriodicallyCheckIfCanAttackAnyone.cs:32,77`,
  `SpiderEngageDecorator.cs:59`), `WargMissionBehavior.cs:66-72`, `AgentSlotIdentity.cs:15` doc line,
  `SpiderAttackServiceTests.cs:238-239` (before the Step 2 insertion point at :275, so Step 6.4's line
  numbers do not shift), the lesson heading at `docs/reviews/lessons/adapters-taleworlds-api.md:588`, and
  both changelog list heads (`spider.md:385`, `warg-combat.md:195`).
- **Engine facts.** `Mission.cs` 4209, 4213, 4318, 4325, 4359, 4374, 4377, 4380, 4391, 4602-4611, 5084-5090,
  2995 and `Agent.cs` 518, 644, 905, 1031-1036, 1773-1790, 1848, 5324-5326 and `MissionBehavior.cs:154` all
  read as the plan quotes. `Team.cs` has no `operator ==` and no `override bool Equals`.
- **Callers.** `.IsSameTeam(` has exactly the two Main callers named (`SpiderAttackService.cs:201`,
  `WargAttackService.cs:44`); `: IAgentAdapter` has the one implementation; `IAgentAdapter` appears in 20
  `.cs` files. `AgentAdapter.RadialStrike` (`AgentAdapter.cs:207`, `:266`) reports every active agent with no
  team filter, so a loose mount does reach the spider's arc callback: the bug is real on that path.
- **Test oracles, simulated by reading the helpers.** Step 2: all four fail on the base for the stated
  reason (`EnemyTarget()` at `SpiderAttackServiceTests.cs:244`, `Spider()`/`HorseOf()`/`Struck()` at
  `SpiderStrikeCapTests.cs:28-94`, `RiderlessAttacker()` at `WargAttackServiceTests.cs:422`; no existing
  `LooseMount` helper or `LooseMountOfUnknownSide` name). Step 4/6 GREEN logic passes every new case and
  keeps the existing ones (no existing spider or warg test has a mount target with a null rider that
  expects damage). Step 7's `Member` regex works on the comment-stripped, LF-normalised text
  (`RepoPaths.ReadSource`, `TAOM.Tests/Infrastructure/RepoPaths.cs:65-74`).
- **Counts.** `[TestMethod]` counts 42, 5, 26; the two `[Ignore]`s are in `WargAttackServiceTests.cs:350,373`;
  no `[DataRow]` in the three files. New tests 4 + 9 + 7 + 9 + 5 + 2 = 36, so 43028 + 36 = 43064 and the
  Step 6 filter gives 91 run, 89 passed, 2 skipped, as the plan says. The class filters match one class each.
- **Compile prerequisites.** `InternalsVisibleTo` `TAOM.Tests` (`Main/TAOM.csproj:114-115`); `<Nullable>enable`
  and LangVersion 10 (`Directory.Build.props:5-6`); the in-scope folders have no graduated nullable
  `.editorconfig` (only `ArmourAcquisition` and `Siege` do). The `using` claims for `AgentAdapter.cs`,
  `SpiderAttackService.cs` and `WargAttackService.cs` hold. `ServiceSignatureGateTests` scans `*Service`
  types and `I*Adapter` interfaces for TaleWorlds class types (`ServiceSignatureGateTests.cs:157-160`):
  `bool HasKnownSide` is clean, and neither new static class is in its scope.
- **Item 4.** TDD order holds (Steps 2, 3, 5, 7 RED before 4, 6, 7 GREEN). Issue #594 is OPEN with label
  `bug` (`gh issue view 594`). ADR-002, ADR-007, ADR-008 named with one line each. No protected file is
  touched; `Main/IoC.cs`, `Main/SubModule.cs`, `Main/TAOM.csproj` are out of scope with a STOP. STOP
  conditions are specific to this plan. Done criteria are commands. Planned-at `db5680c8a` and the 16
  drift-check paths equal the 16 Scope paths. Every dotnet command carries `-p:DisableModuleCopy=true
  -p:ModuleId=`. No worktree path, branch name or CHANGELOG step (CHANGELOG is listed as out of scope only).
- **Item 5.** A scan for U+2014 and U+2013 found none; no drive-letter path; no secret value.

## Blocking

None.

## Non-blocking

1. **Step 7, plan lines 1068-1073: the recording runs before the adapter-cache bookkeeping.** The plan puts
   `MountRiderSides.RecordRider(agent);` as the first statement of `OnAgentBuild`'s `try`, ahead of
   `_adapterFactory.OnAgentBuilt(agent);` (the #592 build count). Should `RecordRider` ever throw, the build
   bookkeeping is skipped for that agent. It is unlikely (two managed reads and a `ConditionalWeakTable`
   write), but placing the call after `OnAgentBuilt` costs nothing and keeps the shipped duty first. The
   wiring test and done criteria pass either way.
2. **Maintenance notes, plan line 1210: "Spawn is proven" overstates it.** `Mission.cs:4325`
   `SetMountAgentBeforeBuild` sets `MountAgent`, whose setter (`Agent.cs:1037-1041`) calls `SetMountAgent`,
   which is a native call (`Agent.cs:5329-5333`, `MBAPI.IMBAgent.SetMountAgent`). The managed
   `_cachedMountAgent` is filled by the native callback `UpdateMountAgentCache` (`Agent.cs:1848-1851`). So
   spawn rests on the same kind of evidence as remount: vanilla's own read at `:4359` and the shipped
   Elephant howdah read (`ElephantMissionBehavior.cs:70`). "Current state" (lines 166-173) words it
   correctly, and the commit's `Not-tested:` trailer already lists spawn. Suggest "Spawn is supported by
   vanilla's own read at :4359" in place of "proven".
3. **Stale test comment in an in-scope file.** `TAOM.Tests/Features/Spider/SpiderStrikeCapTests.cs:127` says
   "a teamless loose horse is on nobody's side". After this plan a loose horse carries its last rider's side.
   The plan rewrites the matching code comment (Step 6.2, `SpiderAttackService.cs:137-138`) but not this one,
   and its stale-claim grep (line 1165) covers only `victimTeamSource`. Suggest the same rewording there and
   adding `nobody's side` to the done-criteria grep over `Main TAOM.Tests`. Hits outside code
   (`docs/reviews/rca-creature-bandits-2026-09-28.md:46`, `docs/features/fords-of-war.md:608`) are history or
   describe soldier targeting, and stay.
4. **Commands table, plan lines 240-249: no RefAsm unit step.** Dispatch rule 7 asks for the RefAsm unit step
   from `.ai/verification.md` (lines 28-30) when tests touching engine types are added. The new
   `MountRiderSidesTests`, `AgentAdapterSideTests` and `CreatureBiteSidesTests` carry
   `[TestCategory("RequiresGame")]`, so the step excludes them, and the wiring test names no engine type;
   the run should pass. Naming the step and its expected result would save the executor a lookup.
5. **Step 5, plan lines 839-840.** "They pass on the Step 4 code too" cannot be observed at that step,
   because the Step 5 build fails by design. Harmless; the Step 6 run proves them.
6. **Baseline totals UNVERIFIED by this review.** I did not run dotnet (read-only role), so 43028 passed,
   3 failed, 9 skipped and the three language-file failure names are the writer's figures. Step 1 makes the
   executor record and compare them.
7. **Threading note, plan lines 190-193 and the Step 7 comment (lines 1080-1082).** #634 caught
   `OnAgentDismount` off the main thread; that `OnAgentMount` can arrive off it too is inferred. The repo
   agrees (`BehaviorTreeMissionLogic.cs:328` checks `OffMainThread` in its `OnAgentMount`), so the comment is
   defensible as written; cite that line if the reviser wants the claim anchored.
8. **Step 7, the `_mountFailureLogged` flag** is a plain `bool` written from a possibly off-thread callback.
   A race only risks logging the failure twice. Acceptable; noted for the deep review.

## Excerpt mismatches

- Plan line 73 labels the `StrikeArc` excerpt `SpiderAttackService.cs:133-141`; the excerpt's last line,
  `held.Add((attackerAdapter, targetAdapter, boneId));`, is line 140 (line 141 is `});`). The content
  matches; only the range label is off by one.

## Executable by a weak model

Yes.

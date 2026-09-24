# Deep review: plan 015, warg tick costs (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 015, cut the warg behaviour tree's per-tick resolves, allocations and native
         wrapper churn (branch improve/015-warg-tick-costs, diff 7f02fc8d..66a85b08)
Date: 2026-09-24

Scope:   C# (9 production files, 3 test files), CHANGELOG and two feature docs. No XML, XSLT,
         C++ or harness files.
Waves:   Agents 1 (standards), 2 (engine compatibility), 3 (efficiency), 4 (completeness),
         5 (data flow) and 6 (design) in one wave; Codex gpt-6-astra at ultra in parallel.

STANDARDS:     PASS: 0 HIGH/MED, 4 LOW (2 are the orchestrator's: issue, doc changelog), 3 nits
COMPATIBILITY: PASS: 0 incompatible, 3 unverified (none load-bearing), 1 LOW (test comment)
EFFICIENCY:    PASS: 0 high, 3 APPLY items (all low), 6 follow-ups
COMPLETENESS:  INCOMPLETE at review time: no GitHub issue, skip-guard tests, doc changelog
               entries, owed lessons. All but the issue are closed by this pass.
DATA FLOW:     PASS: 18 flows, 0 gaps, 3 LOW inconsistencies (traces 12, 15, 16)
DESIGN:        5 KEEP proposals (2 apply, 3 follow-up)
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Details

The six lens reports are recorded in the orchestrator's run; this section keeps each finding, the
reviewer's check against the worktree, and the classification. **CONFIRMED** means I re-read the
code at `66a85b08` and the defect exists. Line numbers are at `66a85b08` unless marked.

### Findings and classification

| # | Source | Sev | Finding | Verdict | Action |
|---|---|---|---|---|---|
| 1 | Agent 5 trace 12 | LOW | `BoneCheck.CheckTargets` range gate `LengthSquared > _maxRangeForCheck -> continue` (`BoneCheck.cs:133`) is an inverted early exit: a NaN frame reaches `GetSkeleton` | CONFIRMED: RED test `CheckTargets_TargetWithANonFiniteFrame_FailsTheGateAndIsKept` failed on `GetSkeleton` received | Fixed: `if (!(... <= _maxRangeForCheck)) continue;` |
| 2 | Codex P3-2, Agent 2 LOW-1, Agent 4 F5, Agent 5 trace 17, Agent 1 follow-up | LOW | `WargTickCostTests.CallsInIfLoadable` turns `FileNotFoundException` into zero calls, so `UpdateWargRiderHandle` was never scanned in a filtered run and a Resolve there would pass | CONFIRMED (read `WargTickCostTests.cs:48-58`; View.dll is not in the test bin) | Fixed: `[ClassInitialize]` calls `GameAssemblies.EnsureLoaded()`; an unreadable body now fails the test (inconclusive only with no game install); positive control `ResolveCheck_ResolveInASameTypeHelper_IsFound` |
| 3 | Codex P3-3 | LOW | `AssertScansIntoABuffer` checks only the three-argument overload; `GetNearAliveAgentsInRange(60, agent, new List<Agent>())` passes | CONFIRMED (read `WargTickCostTests.cs:70-77`; `IlCallScanner` yields `newobj` constructors) | Fixed: the check also rejects a `List<Agent>` constructor in `Evaluate`; negative control `BufferCheck_ThreeArgumentScanIntoAFreshList_IsFound` |
| 4 | Codex P2, Agent 5 trace 6 | LOW (Codex P2) | Removing z cells changes scan membership between grid rebuilds: an agent bucketed in a z cell the query never probed, that has since moved into the sphere, is now returned. The plan, CHANGELOG and code comment claimed "the same set" unconditionally | CONFIRMED as a wrong claim. The new result is a superset of the old and every extra agent is inside the live sphere, so it is closer to the true answer; x and y were always stale the same way | Claim corrected in `SpatialGrid.cs:122`, CHANGELOG and `warg-combat.md`; new behaviour pinned by `CollectInRadius_PointMovedVerticallySinceTheBuild_IsJudgedOnItsCurrentPosition`. Keeping it is **NEEDS MIKE** (see below) |
| 5 | Agent 1 #2, Agent 4 F2 | LOW | `CheckTargets` skip guards (null entry, fading out, null visuals) untested; every test uses a one-element list, so a lost `i--` passes | CONFIRMED (tests.md Skip-Guard Exhaustion) | Fixed: three single-guard tests and `CheckTargets_MixedTargets_DropsAndKeepsEachWithoutSkippingTheNext` |
| 6 | Agent 4 F3 | LOW | The documented column-order change is unpinned (all multi-point asserts are `AreEquivalent`) | CONFIRMED | Fixed: `CollectInRadius_OneColumnAtTwoHeights_ReturnsThemInBuildOrder` |
| 7 | Agent 4 F4 | LOW | "No node field is static" is unpinned; a static initializer runs in the type initializer, which no IL scan reaches | CONFIRMED | Fixed: `TreeNodes_StaticFields_HoldNoServiceOrScanBuffer` (reflection) |
| 8 | Agent 1 #4, Agent 4 F6, Agent 5 traces 15 and 16 | LOW | Neither feature doc's Changelog has an entry; `advanced-combat.md` Tests section and diagram are stale; `warg-combat.md:152` says every node resolves once while `LogTask` still resolves per Execute | CONFIRMED | Fixed in both docs |
| 9 | Agent 4 F8 | LOW | Two owed lessons (GetSkeleton wrapper cost; `== null` on a NativeObject in the test host) unrecorded | CONFIRMED (grep of `docs/reviews/lessons/`) | Fixed: two entries in `adapters-taleworlds-api.md` |
| 10 | Agent 4 F7 | LOW | CHANGELOG suite totals unverified | Verified this pass; replaced with this pass's run | Fixed |
| 11 | Agent 4 F10 | LOW | Untracked `docs/reviews/codex-adversarial-015-warg-tick-costs-2026-09-24.prompt.md` | CONFIRMED (`git status`) | Committed with the reports, as earlier prompt files are |
| 12 | Agent 1 nits | nit | `CollectInRadius` called "Pure" but fills the caller's buffer; two two-part test names; `BoneCheckDuringAnimation.cs:45` uses `== null` on a `Skeleton` | CONFIRMED | Fixed all three (`BoneCheck.cs:71` is unchanged code: follow-up) |
| 13 | Agent 1 #3, Agent 4 F1 | MED (Agent 4) / LOW (Agent 1) | No GitHub issue for plan 015 | CONFIRMED (`gh issue list` per both lenses) | **NEEDS MIKE**: `/issue` is public and never auto-invoked |
| 14 | Agent 1 #1 | LOW | `IoC.Resolve` in node field initializers is a service locator outside the Intentional Patterns list | CONFIRMED as a standards gap, not a regression (the base resolved per call) | **NEEDS MIKE**: document the creature BT node exception in `.ai/review-reference.md`, or inject through the component args |
| 15 | Agent 4 F9 | LOW | The plan's `is null` amendment exists only in `E:\repos\TAOM`'s working tree; commit `7577894d` cites it | CONFIRMED (Agent 4 read it; this pass may not touch that checkout) | **NEEDS MIKE / orchestrator**: commit the amendment with the merge |
| 16 | Agent 2 INFO-2 | INFO | Comments understate `GetSkeleton`'s cost (it also makes a second ref-count call and a finalizer call) | Not a defect: the stated cost is a lower bound and strengthens the change | None |
| 17 | Agent 2 INFO-1 | INFO | `is null` used in one of three places | Style only, same result in game | Applied at the changed line (row 12) |
| 18 | Codex, plan text | LOW | The plan says order matters only to a bite; `SpiderEngageDecorator` keeps the first of equal-distance candidates | CONFIRMED (Agent 5 trace 5 agrees) | CHANGELOG corrected; the plan file is not edited here (row 15 conflict) |

**False positives:** none. Every lens and Codex finding held against the code; row 16 is a correct
observation that needs no change.

### Engine compatibility (Agent 2), unverified items

- Native `GetGlobalFrame()` on a target whose skeleton would be null now runs before the skeleton
  test. Vanilla makes the same call with no null check (`Mission.cs:5721-5723`); UNVERIFIED in native.
- "A battlefield's vertical spread is a few metres" and "most agents captured at 20 m are outside the
  gate" are unmeasured comments; no result depends on them.
- Codex: a far target with no skeleton is now kept rather than dropped, and could be hit later if its
  skeleton returns. Whether a live, active agent's skeleton is ever transiently null is UNVERIFIED.

## Action items

1. Mike: decide the NEEDS MIKE items below.
2. Orchestrator: one convergence `deep-reviewer` pass on this pass's diff (this pass could not spawn
   agents), then merge. Fold this branch's `## 2026-09-24` CHANGELOG heading into the one another
   session has staged in `E:\repos\TAOM`.
3. The in-game Custom Battle with warg riders on both sides is still owed.

## Improvements (Step 4)

**APPLIED:**
- `Main/Features/Warg/WargRiderHandManager.cs:10-19` and `WargMissionBehavior.cs` (Agent 3 APPLY 1,
  Agent 6 Proposal 1): the factory plumbing is gone. `Tick()` decides warg-ness with
  `WargConfig.IsWargMonster(Agent.Main.MountAgent.Monster?.StringId)`, the same predicate
  `AgentAdapter.IsWarg()` evaluates (`AgentAdapter.cs:61`) and the one `TryAttachWargTree` uses
  (`WargMissionBehavior.cs:154`). `WargMissionBehavior.cs` is byte-identical to the base again. The
  only difference: a mount with a null `Monster` now returns false instead of throwing into
  `OnMissionTick`'s catch. Proof: `WargRiderHandManager_Tick_NeverResolvesFromIoC` (now scanning
  `UpdateWargRiderHandle` too) and `WargMonsterIdTests`, green before and after.
  `WargConfig.cs:12` doc comment updated to match.
- `PeriodicallyCheckIfCanAttackAnyone.cs:30,66` (Agent 3 APPLY 2, Agent 5 trace 1): the warg's
  adapter is looked up with `??=` at the first candidate, so a scan with no candidate pays no cache
  lookup, as at the base. Proof: the `WargTickCostTests` resolve and buffer tests, green before and
  after; `GetAgentAdapter` is idempotent per agent object.
- `BoneCheckDuringAnimation.cs:45`: `is null` for consistency with `BoneCheck.cs:139` (Agent 1 nit,
  Agent 2 INFO-1). Same result in game (`NativeObject.operator ==` returns `(object)a == null`).

**NOT APPLIED:**
- `BoneCheckDuringAnimation.cs:43-53` (Agent 3 APPLY 3): fetch the attacker skeleton after the
  action and progress tests. Behaviour-changing (a null attacker skeleton during wind-up would expire
  at the first hit-window frame instead of at once), so it needs Mike.
- `BoneCheckDuringAnimation.cs:47,53` (Agent 6 Proposal 2): read `GetCurrentActionProgress(0)` once.
  The double read predates this change (Agent 3), and no characterisation test can reach `Tick`
  (the `ActionIndexCache` static constructor needs the engine), so the in-game bite would be its only
  proof. Left for Mike with the owed in-game check.
- Keeping z cells to preserve stale-grid membership (Codex P2's first fix): it would add stored z
  buckets and a filter to reproduce a staleness artefact, a Reject under the simplicity criterion.
  The doc correction was applied instead; Mike decides.

**FOLLOW-UP (pre-existing code; no issue filed, because `/issue` is public and needs Mike's word):**
- Agent 6 Proposal 3 / Agent 5 F2: `NoEnemyCloseDecorator` treats an allied team as an enemy
  (`Team != wargTeam`), so a warg beside allies never takes its 1 s sleep. Behaviour-changing.
- Agent 6 Proposal 4 / Agent 5 F4: the timed `BoneCheck` mode, `_maxDuration` and the `100f`
  argument have no production caller; `AddBoneCheckComponent` has none either.
- Agent 6 Proposal 5: the two attack-scan `Evaluate` bodies duplicate one predicate.
- Agent 3 #4: `AgentAdapter.AgentVisuals` allocates an adapter and repeats `IsFadingOut` per target per frame.
- Agent 3 #5: `WargMissionBehavior._wargComponents` is written and pruned every tick but never read.
- Agent 3 #6: `WargConfig.IsWargMonster` allocates an enumerator per call through the comparer overload.
- Agent 3 #7: `NoEnemyCloseDecorator` gathers every agent within 60 m for a yes or no; measure first.
- Agent 3 #8 / Agent 5 F5 / Agent 1: an invalid bone id logs, and resolves the logger, every frame.
- Agent 3 #9: `IsAttackLikelyToHit` re-reads the attacker's velocity per candidate.
- Agent 5 F1: the 0.1 s grid rebuild in `WargMissionBehavior` never runs (`AdvancedCombatBehavior`
  is always present), so every scan reads a grid up to 2 s old; `warg-combat.md:153` is stale.
- Agent 5 F3: `BoneCheck.cs:148` removes a hit target without `i--` (unreachable today: the only
  caller stops on first hit).
- Agent 1: `IAgentVisualsAdapter.GetSkeleton` returns the sealed `Skeleton` (ADR-007);
  `WargMissionBehavior` is 207 lines against ADR-002's 150; `LogTask` resolves per Execute;
  `BoneCheck.cs:71` still uses `== null`. (`IAgentAdapter.IsWarg()` was orphaned by this change,
  not pre-existing; deleted in the convergence pass below.)
- Agent 4: stale lines in `warg-combat.md` (17, 21, 124, 125, 141) and `advanced-combat.md:90`.

**VERDICT: READY FOR COMMIT** (full suite green apart from the two known live-Armory failures;
convergence pass and the NEEDS MIKE items remain for the orchestrator and Mike).

Final full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` in the worktree:
`Failed: 2, Passed: 10266, Skipped: 2, Total: 10270` (baseline before this pass: 10256 passed, 2
skipped, 2 failed of 10260). The two failures are `TheElkItem_DeclaresTheScaleTheReachIsTunedFor`
and `AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.

## NEEDS MIKE

1. File the plan 015 GitHub issue, then cite it in the CHANGELOG heading and both feature docs;
   label it `triage-needs-ingame` at close.
2. Accept the stale-grid membership widening (recommended; the docs now describe it) or restore
   z-bucket filtering.
3. Service locator in creature BT nodes: add it to Intentional Patterns, or inject the services.
4. Commit the plan 015 `is null` amendment that sits uncommitted in `E:\repos\TAOM`.
5. Agent 3 APPLY 3 (skeleton fetch after the progress test) and Agent 6 Proposal 2 (one progress read).

## CODEX REVIEW

Codex gpt-6-astra at ultra effort, `docs/reviews/raw/codex-adversarial-015-warg-tick-costs-2026-09-24.md`
(complete: ends "END OF CODEX REVIEW"). Verdict ISSUES FOUND: 0 P1, 1 P2, 2 P3. It decompiled 11
engine types fresh from the installed DLLs and matched them to the cache, answered all ten Known
Suspects, and cross-referenced every config value.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P2 | LOW | Partly | The membership change between rebuilds is real (worked counterexample checked against base and head `SpatialGrid.cs`). It only ever adds agents that are inside the live sphere, and x and y were always stale the same way, so it is a wrong claim, not a wrong result. Claim corrected and pinned; keeping the behaviour is Mike's call |
| 2 | P3 | LOW | Yes | The `FileNotFoundException` catch let an unread helper pass. Fixed with `EnsureLoaded`, a hard failure and a positive control |
| 3 | P3 | LOW | Yes | The buffer test proved only the overload. Fixed with a constructor check and a negative control |

**Confirmed bugs:** rows 2 and 3 (test oracles), row 1 (documentation of behaviour).
**False positives:** none.
**Design questions:** row 1's keep-or-restore (NEEDS MIKE 2).
**Things Codex missed:** the NaN polarity of the moved range gate (Agent 5), the untested
`CheckTargets` skip guards (Agents 1 and 4), the unpinned static-field rule (Agent 4), and the
per-tick adapter lookup the hoist added on an empty scan (Agents 3 and 5).
**Known Suspects:** 1, 5, 6, 7, 8 and 10 disputed with evidence; 2 and 3 unverified as history;
4 and 9 confirmed in part. I checked 4 (commit `7577894d` and `BoneCheck.cs:137-139`) and 9 (the
same gaps as findings 2 and 3); agreed.

### AGENTS.md lessons (pending)

Phase 3h is consolidated later for all branches. Proposed lines:
- **Bugs Codex typically misses:** a gate moved into new code keeps its old NaN polarity; Codex
  checked the moved gate's comparison and equality but not its NaN behaviour.
- **What Codex does well:** building a concrete stale-state counterexample (build, move, query)
  against both the base and head versions of a data structure, rather than trusting a test oracle
  that rebuilds before every query.
- **What Codex does well:** attacking a test's oracle with the smallest regression it would accept
  (`new List<Agent>()` through the buffer overload).

## Convergence

Second pass over the review-fix commit `fe8f30c7` (diff `66a85b08..fe8f30c7`). The convergence
reviewer reported four LOW defects; each was checked against the code before any edit. All four
were confirmed; none was a false positive. None is a runtime regression.

| # | Finding | Verified | Fix |
|---|---|---|---|
| 1 | The mixed-list test could not fail for a lost `i--`: every entry after a removal was a far target, and the asserts could not tell "skipped" from "examined and kept". The null-visuals guard was not in the list | CONFIRMED by trace of `BoneCheck.cs:110-146` against the old list `[inactive, far, nearNoSkeleton, far2]` | List is now `[far, inactive, noVisuals, nearNoSkeleton, fadingOut, far2]`: each removal is followed by an entry that must also be dropped, and each far target's `GetGlobalFrame` read is asserted. Deleting each `i--` locally (`BoneCheck.cs:117`, `:125`, `:144`, one at a time) turned exactly this test red (`Failed: 1, Passed: 9` each time); the file was restored and is unchanged in this commit |
| 2 | `WargMonsterIdTests` named `AgentAdapter.IsWarg()` as the only warg gate, and `IsWarg()` had no caller left | CONFIRMED: `WargRiderHandManager.cs:18` and `WargMissionBehavior.cs:154` gate on `WargConfig.IsWargMonster`; grep finds `IsWarg` only at its two declarations; at `66a85b08` `Tick` still called it | Comment names `WargConfig.IsWargMonster`. `IsWarg()` deleted from `IAgentAdapter` and `AgentAdapter` (and the `using` only it needed); the `WargRiderHandManager` comment no longer points at it. A deletion with no caller holds parity. The FOLLOW-UP entry is relabelled |
| 3 | Component diagram put `TakeDamage` beside the collision callback instead of under it | CONFIRMED: both at column 31 in `advanced-combat.md` | `TakeDamage` and `RegisterBlow` indented one level under `_onCollisionCallback`, as at `66a85b08` |
| 4 | `TreeNodes_StaticFields_HoldNoServiceOrScanBuffer` is a reflection rule test with no rejecting fixture, against the lesson written in the same commit | CONFIRMED: the controls cover only the two IL rules | The cheaper fix: the testing-qa Prevent line now asks for the fail-on-unreadable-body rule and the control fixture from IL rule tests only; a plain `GetFields` check has no body read that can fail open |

The report row 5, RCA F5 and `advanced-combat.md` Tests line ("every per-target skip, singly and in
a mixed list") are accurate again after fix 1; the test count stays 10.

Filtered run after fix 1, `dotnet test TAOM.Tests --filter FullyQualifiedName~BoneCheckRangeGateTests
-p:DisableModuleCopy=true -p:ModuleId=`: `Passed: 10, Failed: 0`.

Full suite after all four fixes, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed: 2, Passed: 10266, Skipped: 2, Total: 10270`. The two failures are the known live-Armory
ones, `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`.

**CONVERGENCE VERDICT: 4 fixed, 0 false positives.** The fixes are test, comment, doc and one
parity deletion; a fresh review of this commit is still owed before merge.

## Maintainer decisions applied (2026-09-24)

Mike answered NEEDS MIKE items 1, 2, 3 and 5 on 2026-09-24. All four are applied on top of
`56eb4bc8` in one commit, `fix(warg): v2.0.30 - apply maintainer decisions for plan 015`
(its hash is in `git log`; a commit cannot name its own). Item 4 (the `is null` amendment in
`E:\repos\TAOM`) was not part of these decisions and stays with the orchestrator.

| NEEDS MIKE | Decision | What changed | Proof |
|---|---|---|---|
| 1 | Cite issue #659 | CHANGELOG heading reads `(plan 015, #659)`; the GitHub Issue sections of `warg-combat.md` and `advanced-combat.md` name #659 (title read with `gh issue view 659`: open, "Warg battles: cut per-tick service lookups, scan allocations and skeleton wrappers") | Docs only |
| 2 | Keep the wider scan results between grid rebuilds | No code change. Recorded here, in the CHANGELOG entry ("Grid widening kept") and in the `warg-combat.md` Changelog | `CollectInRadius_PointMovedVerticallySinceTheBuild_IsJudgedOnItsCurrentPosition` stays green |
| 3 | Inject the node services through `WargBehaviorTree` | `WargBehaviorTree.BuildTree` resolves `IMissionAdapterFactory` and `IWargAttackService` once per tree and passes them to the constructors of `PeriodicallyCheckIfCanAttackAnyone`, `CheckOnceIfCanAttackEnemy`, `WargAiControlledIsNotFacingEnemy` and `WargAttackTask`, which keep them in private readonly instance fields and contain no `IoC.Resolve`. A grep of the worktree for `new <NodeType>(` found every construction site in `WargBehaviorTree.cs` (seven), none elsewhere. The three `WargAttackTask`s of one tree now share one `WargAttackService` instead of one each; the service holds only two readonly fields, so this is equivalent | RED first: `InjectedTreeNodes_ConstructorsAndMembers_NeverResolveFromIoC` failed with `Expected:<0>. Actual:<5>` (the four constructors, `WargAttackTask` twice) and `WargBehaviorTree_BuildTree_ResolvesEachServiceOncePerTree` failed with `CollectionAssert.AreEqual failed. BuildTree resolves: (Different number of elements.)`. Control `ResolveCheck_ResolveInAFieldInitializer_IsFound` was green before and after. All three pass after the change, as do the existing per-call and static-field tests |
| 5 | Apply Agent 3 APPLY 3 and Agent 6 Proposal 2 | `BoneCheckDuringAnimation.Tick` tests the action, then reads `GetCurrentActionProgress(0)` once into a local for the max and min bounds, and only when the progress has reached `_actionProgressMin` fetches `agentVisuals?.GetSkeleton()`; a null skeleton invokes `_onExpiration` and returns false (`is null` kept). The `>=` comparisons keep their polarity, so a NaN progress still skips the check as before | RED first, IL rule tests in `BoneCheckDuringAnimationTickTests`: `Tick_EveryFrame_ReadsTheActionProgressOnce` failed with `Expected:<1>. Actual:<2>` and `Tick_BeforeTheHitWindow_FetchesTheAttackerSkeletonOnlyAfterTheProgressTests` failed on the old order; both pass after. Two control fixtures (the old shape) prove each rule can fail |

**Behaviour difference (item 5).** Before, a missing attacker skeleton ended the bite on the first
tick it was seen, wind-up included. Now a wind-up frame fetches no skeleton, so a skeleton missing
during the wind-up ends the bite when the hit window opens instead of at once. Inside the hit
window the result is the same as before. The IL tests pin only the call shape; no unit test can
call `Tick` (the `ActionIndexCache` static constructor needs the engine). **The owed in-game warg
Custom Battle is the proof for items 3 and 5: bites must still land and end as before.** This is
recorded in the CHANGELOG entry and in `warg-combat.md`.

**Verification.** Build: `dotnet build Main/TAOM.csproj -p:DisableModuleCopy=true -p:ModuleId=`,
`0 Error(s)` (the two warnings are the Harmony analyzer's BHA0001 and BHA0006 on unrelated types).
Filtered run over the Warg and BoneCheck tests: `Passed: 68, Skipped: 2, Failed: 0`. Full suite,
`dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=`:
`Failed: 2, Passed: 10273, Skipped: 2, Total: 10277`; the two failures are the known live-Armory
ones, `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`. The seven new tests account for the
rise from 10266. A fresh review of this commit is owed before merge.

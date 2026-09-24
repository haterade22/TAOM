# Deep review: plan 015 maintainer decisions, warg tick costs (2026-09-24)

```
DEEP REVIEW REPORT
===================
Feature: plan 015 maintainer decisions 1, 2, 3 and 5 (branch improve/015-warg-tick-costs,
         diff 56eb4bc8..23f6f85b): node services injected from WargBehaviorTree.BuildTree,
         BoneCheckDuringAnimation.Tick reads the progress once and fetches the attacker
         skeleton only inside the hit window, #659 cited, grid widening kept
Date: 2026-09-24

Scope:   C# (5 production files, 2 test files), CHANGELOG, two feature docs, the first
         review's report. No XML, XSLT, C++ or harness files.
Waves:   Agents 1 (standards), 2 (engine), 3 (efficiency), 4 (completeness), 5 (data flow)
         and 6 (design) in one wave; Codex gpt-6-astra adversarial pass in parallel. Steps 3,
         3e and 4 and review-codex Phase 3 by the review lead in the worktree.

STANDARDS:     PASS: 0 HIGH/MED, 4 LOW, 3 nits; all confirmed and fixed (LOW-4 by comment and
               test; its polarity change is NEEDS MIKE)
COMPATIBILITY: PASS: 14 verified, 0 incompatible, 3 unverified (native); 1 LOW engine claim
               wrong ("no test can call Tick"), confirmed and fixed
EFFICIENCY:    PASS: 0 issues in the changed hunks, 0 APPLY, 2 FOLLOW-UP, 1 INFO
COMPLETENESS:  INCOMPLETE at review time (5 LOW); all 5 closed by this pass, the plan text
               and #659's body stay with Mike
DATA FLOW:     PASS: 14 flows, 0 gaps, 2 LOW inconsistencies (traces 6 and 12), both fixed
DESIGN:        5 KEEP proposals (3 APPLY, 2 FOLLOW-UP); the 3 APPLY are applied
XML:           NOT IN SCOPE
TOOLING:       NOT IN SCOPE
```

## Findings: verification and outcome

Every finding was re-read against the worktree at `23f6f85b` before acting. Two were proven by
execution, not only by reading: the IL-proxy finding by a mutation run, the "no test can call
`Tick`" finding by a spike test and the installed DLL's IL header.

| # | Source | Sev | Finding | Verdict | Outcome |
|---|---|---|---|---|---|
| 1 | A1 LOW-1, A2 F2, A4 #1, A5 trace 12, A6 #2, Codex P3-1 | LOW | `Tick_BeforeTheHitWindow_FetchesTheAttackerSkeletonOnlyAfterTheProgressTests` compared call positions only; `IlCallScanner` yields calls, not comparisons or branches (`IlCallScanner.cs:111-142`), so a fetch between the progress read and the bounds tests passed | CONFIRMED by mutation: `Tick` rewritten to Codex's counterexample (fetch after the read, expire on a null skeleton before the bounds); both IL `Tick_*` tests stayed green, the new behaviour tests went RED (3 failed). File restored byte for byte | Fixed: five behaviour tests drive `Tick` with substitutes; the IL order rule, its helper and its control assertion are deleted; the one-read IL rule stays for the live-skeleton path no test can drive |
| 2 | A2 F1 | LOW | "No unit test can call `Tick` (the `ActionIndexCache` static constructor needs the engine)" is wrong | CONFIRMED: installed v1.5.3 IL header is `.class public sequential ansi sealed beforefieldinit TaleWorlds.MountAndBlade.ActionIndexCache`; a spike test built `BoneCheckDuringAnimation` with `default(ActionIndexCache)` and ran `Tick` green | Fixed in the test summary, CHANGELOG, `advanced-combat.md` (Tests, Coverage gaps) and `warg-combat.md`; pointer added to the first report |
| 3 | A1 LOW-2, A5 trace 6, A6 #3 | LOW | "No node is a service locator" (`WargBehaviorTree.cs:37-38`, test comment, `warg-combat.md:158`); `LogTask` resolves `IModLogger` per Execute (`LogTask.cs:10`) and the tree adds it three times | CONFIRMED: a grep of every node class the tree builds finds `IoC.Resolve` only in `LogTask` | Fixed: comments and docs say "the four service nodes" and name `LogTask`; the comment also says why the resolves live in `BuildTree` (`BTRegister.RegisterClass` keeps the first factory, `BehaviorTreesCore.cs:274-278`). Injecting `LogTask`'s logger is FOLLOW-UP (A6 #4) |
| 4 | A1 LOW-3 | LOW | `IsResolve` matched `Resolve` only; `IoC.ResolveAll<T>()` (`IoC.cs:241`) passed every rule, and the predicate was duplicated | CONFIRMED RED first: `ResolveCheck_ResolveAllInAPerTickMethod_IsFound` and `ResolveCheck_ResolveAllInAConstructorOrMember_IsFound` failed `Expected:<1>. Actual:<0>` | Fixed: one predicate covers both, used by both rules; both controls green |
| 5 | A1 LOW-3 nit, A4 #4, A6 #1 | LOW | `InjectedNodes` repeated `TreeNodes` minus `NoEnemyCloseDecorator`, whose constructor and field initializers were never scanned | CONFIRMED | Applied (Step 4): `InjectedNodes` deleted, the rule runs over `TreeNodes` as `TreeNodes_ConstructorsAndMembers_NeverResolveFromIoC`, green |
| 6 | A1 LOW-4, A2 follow-up 3 | LOW | The reordered `if (progress >= _actionProgressMax)` keeps the inverted form; the parity decision was only in the review record | CONFIRMED as an undocumented decision; not a runtime defect (the positive `>= min` gate keeps NaN from `GetSkeleton` and the collision pass) | Fixed: a comment states the polarity is kept on purpose, and `Tick_NaNProgress_KeepsTheCheckWithoutAHitTest` pins it. A1's `!(progress < max)` would make NaN expire the bite: behaviour-changing, so NEEDS MIKE |
| 7 | A1 nit, A4 #2, A5 note, Codex P3-2 | LOW | "A missing skeleton during the wind-up ends the bite when the hit window opens" holds only if it is still missing at the first in-window tick; one back by then lets the bite go on and hit. Only the standing bite has a wind-up (`WargAttackService.cs:135,146`) | CONFIRMED by reading `Tick` (`BoneCheckDuringAnimation.cs:59-67`); engine occurrence UNVERIFIED | Fixed in the code comment, CHANGELOG, `warg-combat.md` (Performance, Changelog), `advanced-combat.md` Changelog; the owed battle now names standing bites |
| 8 | A1 nit, A5 note | nit | `WargTickCostTests` summary said "Three rules" | CONFIRMED | Fixed: five rules listed, `ResolveAll` included |
| 9 | A1 nit | nit | First report said "Two control fixtures"; there was one | CONFIRMED | Pointer to this report appended to the first report |
| 10 | A1 nit | nit | `advanced-combat.md:20` "fetch skeleton transforms each tick" | CONFIRMED | Fixed: the animation check fetches only inside the hit window |
| 11 | A4 #3 | LOW | No test drives the injected nodes; a constructor that stopped storing a service would compile (CS0649 is a warning) and throw on the first bite | CONFIRMED | Fixed: `WargTreeNodeInjectionTests` (3 methods, 4 results) on bare `Agent` objects and substitute services. Mutation: deleting `_attackService = attackService;` made `WargAttackTask_Execute_AttacksWithTheInjectedServiceThroughTheInjectedFactory` fail with `NullReferenceException`; file restored |
| 12 | A4 #5 | LOW | The injection pattern is not recorded for the next creature | CONFIRMED (How-to-Add, `warg-combat.md:128-136`) | Fixed: step 5 says resolve once in `BuildTree`, pass through constructors, scan into a reused buffer. The plan note (`plans/015-warg-tick-costs.md:1896-1897`) and #659's body are NEEDS MIKE |
| 13 | A4, A5 follow-up | LOW | `warg-combat.md:83` listed `IsWarg` on `IAgentAdapter`, deleted on this branch at `56eb4bc8` | CONFIRMED: `git grep IsWarg` finds only `WargConfig.IsWargMonster` | Fixed (the branch's own leftover) |
| 14 | A4 | owed | The decisions pass's Codex prompt file was untracked; the first pass's twin is tracked | CONFIRMED | Committed with this report |

**False positives:** none outright. One proposed fix was wrong: A1 LOW-4 offered
`!(progress < max)` as costing the same and matching the rule, but it changes NaN from "idle
until the action changes" to "expire now", so it is not a like-for-like fix.

**UNVERIFIED (unchanged, native):** whether an active agent's skeleton is ever briefly null and
then back; whether `GetCurrentActionProgress` can return NaN; action and progress reads on an
active agent with a null skeleton, which the old code skipped (A2 cites vanilla reading progress
on agents with no visuals check, so the risk reads low).

## Details by lens

**Agent 1 (standards):** checks 1 to 10 pass; decisions 1, 2, 3 and 5 done as recorded. LOW-1 to
LOW-4 and three nits: rows 1, 3, 4, 5, 6, 7, 8, 9, 10. Follow-ups F1 to F5 below.

**Agent 2 (engine):** 14 APIs verified against the installed v1.5.3 DLLs and the taom-src cache,
0 incompatible. F1 (row 2) and F2 (row 1). Its spike is now the behaviour test class.

**Agent 3 (efficiency):** no issue in the changed hunks. Decision 3 cuts a tree's resolves from 10
to 2 and its attack services from 3 to 1; decision 5 costs the same or less on every path except
the decided one. INFO: only standing bites gain the wind-up saving (row 7 wording). F1 and F2 below.

**Agent 4 (completeness):** tests, feature docs, issue #659, CHANGELOG and IoC present. Rows 1, 7,
11, 12, 5, 13, 14. The `is null` amendment still uncommitted in `E:\repos\TAOM` and #659's stale
body are orchestrator and Mike items.

**Agent 5 (data flow):** 14 flows traced, 0 gaps. Traces 6 and 12 are rows 3 and 1. The
resolve-failure path, `BTRegister` placement, one-service sharing, NaN handling and expiry effects
all CONNECTED. The first report's NEEDS MIKE list still reads open; the pointer appended to it
says where the decisions went.

**Agent 6 (design):** proposals 1, 2 and 3 (rows 5, 1, 3) applied. Proposals 4 (inject
`LogTask`'s logger) and 5 (`Mission.GetNearbyEnemyAgentCount` for `NoEnemyCloseDecorator`) are
FOLLOW-UP.

## ACTION ITEMS

1. The owed in-game warg Custom Battle (standing and running bites land and end as before, the
   rider hand pose holds, no `[Warg] Tree build failed`). Unchanged by this pass.
2. A convergence `deep-reviewer` pass on this commit's diff (Step 4.6). The review lead cannot
   spawn agents, so it is owed to the orchestrator.
3. NEEDS MIKE items below.

## IMPROVEMENTS (Step 4)

**APPLIED:**
- `TAOM.Tests/Features/Warg/WargTickCostTests.cs`: `InjectedNodes` deleted, the no-resolve rule
  runs over `TreeNodes` (A6 #1, A4 #4, A1 nit); one `IsResolve` predicate for both rules, with
  `ResolveAll` (A1 LOW-3). Proof: the 17 `WargTickCostTests` green, including the RED-first
  `ResolveAll` controls and the existing field-initializer control.
- `TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs`: behaviour tests
  replace the IL order rule (A6 #2, A2 F1). Proof: 7 tests green; the mutation run above.
- `Main/Features/Warg/WargBehaviorTree.cs:37-41` and the test comment: the service-locator claim
  scoped to the four service nodes, with the `BTRegister` reason (A6 #3, A1 LOW-2). Comment only.

**NOT APPLIED:**
- `BoneCheckDuringAnimation.cs:49` as `!(progress < _actionProgressMax)` (A1 LOW-4 option 1):
  behaviour-changing (NaN would expire the bite); needs Mike.
- `LogTask.cs:9-21` logger injection (A6 #4): `LogTask.cs` is outside this diff (FOLLOW-UP),
  behaviour-preserving; needs Mike's word to extend decision 3 to a shared node.
- `NoEnemyCloseDecorator` on `Mission.GetNearbyEnemyAgentCount` (A6 #5): FOLLOW-UP and
  behaviour-changing (2D live circle, allied teams stop counting as enemies); needs Mike and a
  measurement.
- `BoneCheckDuringAnimation.cs:61-62` caching the attacker skeleton across frames (A3 F1):
  FOLLOW-UP beyond decision 5; preserving only if a skeleton is never replaced mid-bite
  (UNVERIFIED); `/research` first.

**FOLLOW-UP** (pre-existing code; no issue filed, because `/issue` is public and needs Mike's word):
- A2 follow-up 1: `BoneCollisionServiceTests.cs:202-208` says the `ActionIndexCache` constructor
  fires when the JIT compiles any method with that parameter; the green `Tick` tests contradict it
  for v1.5.3, so `CreateAnimationBoneCheck` is probably testable with `default`. The
  `animation-skeleton.md:117` lesson says the type is not beforefieldinit (written for v1.4.7);
  the v1.5.3 IL header says it is. Re-check both before relying on either.
- A2 follow-up 2 / A3: `MBAgentVisuals.GetBoneEntitialFrame` would avoid the `Skeleton` wrapper;
  native equivalence UNVERIFIED.
- A3 F2: `AgentProximityMap` for the scans; behaviour-changing, measure first.
- A1 F1: `.ai/review-reference.md` Intentional Patterns line for a creature tree's static
  `BuildTree` as the composition root.
- A1 F2, A5, A4: spider nodes still resolve lazily (`SpiderAttackOffCooldownDecorator.cs:35`,
  `SpiderAttackTaskBase.cs:44-45`, `SpiderEngageDecorator.cs:52`, `OnSpiderDied.cs:34`);
  `docs/audits/wiring-matrix.md:115` calls BT nodes "engine-constructed", which is wrong, and `:116`
  cites `WargAttackTask:26`. The uncommitted troll tree in `E:\repos\TAOM` uses the lazy pattern too.
- A1 F3: `WargIoC.cs:9` registers the stateless attack service Transient.
- A1 F4: an adapter predicate for the action test; partly superseded by the behaviour tests.
- A1 F5: `CheckOnceIfCanAttackEnemy` has no file of its own.
- A5: `warg-combat.md:153` "Every 5 ticks" contradicts the 2 s rebuild (first review's Agent 5 F1).

VERDICT: READY FOR COMMIT (all confirmed findings fixed or routed to Mike; full suite below; the
Step 4.6 convergence pass is owed to the orchestrator)

## Verification

Filtered run (`WargTickCostTests`, `BoneCheckDuringAnimationTickTests`,
`WargTreeNodeInjectionTests`, `BoneCheckRangeGateTests`, `SpatialGridQueryTests`,
`BoneCollisionServiceTests`, `WargAttackServiceTests`): `Passed: 78, Skipped: 2, Failed: 0`.

Full suite, `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId=` in the worktree:
`Failed: 2, Passed: 10282, Skipped: 2, Total: 10286` (exit 1 from the two known live-Armory
failures, `TheElkItem_DeclaresTheScaleTheReachIsTunedFor` and
`AnimaliaActionSets_BindOnlyHorseActions_ToClipsThatExist`). Baseline after the decisions commit:
10273 passed of 10277; the nine new results are two `ResolveAll` controls, a net three
`BoneCheckDuringAnimationTickTests` and four `WargTreeNodeInjectionTests` results.

## NEEDS MIKE

1. NaN polarity of the bite's window-end test: keep "NaN idles until the action changes" (current,
   pinned) or write `!(progress < max)` so NaN expires the bite.
2. Extend decision 3 to `LogTask` (inject `IModLogger` from `BuildTree`, a shared
   `BaseBehaviorTree` constructor change).
3. Plan text: `plans/015-warg-tick-costs.md:1896-1897` still prescribes services resolved in
   field initializers, and `:46` says the issue is yet to be filed.
4. #659's body still describes the pre-decision state (instance-field resolves, tip `56eb4bc8`,
   31 tests); editing a public issue needs Mike's word.
5. The `is null` plan amendment uncommitted in `E:\repos\TAOM` (first review's item 4), which
   #659's Decisions section calls committed.

## AGENTS.md lessons (pending)

- Bugs Codex typically misses: nothing new; Codex found both of its P3s independently of the lenses.
- What Codex does well: a source-level counterexample for a test oracle (the mutant that keeps one
  progress read and the call order while fetching on every wind-up frame), and a step-by-step
  trace of a conditional behaviour difference (skeleton back by the window).
- False positives Codex produced: none this pass.

## CODEX REVIEW

Codex gpt-6-astra, `docs/reviews/raw/codex-adversarial-015-warg-tick-costs-decisions-2026-09-24.md`
(complete: ends "END OF CODEX REVIEW"). Verdict: **0 P1, 0 P2, 2 P3.** It decompiled `Agent`,
`ActionIndexCache`, `MBAgentVisuals`, `ScriptingInterfaceOfIMBAgentVisuals`, `Skeleton`,
`NativeObject` and `Mission` fresh and matched them to the v1.5.3 dump, traced ten bite scenarios
through the new `Tick`, the mission-end cleanup and the process-static `BTRegister`, and answered
the ten Known Suspects (one CONFIRMED in part, as its finding 1; the rest DISPUTED or UNVERIFIED
with reasons). It could not read #659 (`gh` config access denied); the lenses read it.

### Phase 3d assessment

| # | Codex Severity | Your Severity | Agree? | Reason |
|---|---|---|---|---|
| 1 | P3 | LOW | Yes | The IL order test proves call order, not the branch. Confirmed by executing Codex's counterexample as a mutant: both IL tests green, the behaviour tests RED. Fixed with behaviour tests (row 1) |
| 2 | P3 | LOW | Yes | The docs said a wind-up-missing skeleton ends the bite at the window; a skeleton back by then lets it continue and hit. Documentation, not a gameplay defect; occurrence UNVERIFIED. Fixed (row 7) |

- **Confirmed bugs:** none in runtime code. Two test-oracle and documentation defects, both fixed.
- **False positives:** none.
- **Design questions:** none from Codex; the lenses' are under NEEDS MIKE.
- **Things Codex missed:** the wrong "no test can call `Tick`" engine claim (A2 F1; Codex quoted
  the static constructor but did not check the beforefieldinit header or try `default`); the
  `LogTask` service-locator overclaim (row 3); `ResolveAll` escaping the resolve rule (row 4); no
  fake-driven test of the injected nodes (row 11); the stale `IsWarg` doc line (row 13).

### Phase 3e root cause

| # | Bug | Category | Why Missed | Preventive Action |
|---|-----|----------|-----------|-------------------|
| 1 | IL order test accepted a fetch on every wind-up frame | Other: test checks a proxy (repeat of `harmony-il.md` "An IL call-presence test does not pin control flow" and of this branch's first-review F3) | The builder accepted "no test can call `Tick`" without trying one, so the IL scan was the only tool, and its name described the goal rather than what a call list can show | Behaviour tests; lesson in testing-qa ("Try a substitute-driven test before an IL rule") |
| 2 | Behaviour-difference text omitted skeleton recovery | Logic error (in the claim) | The difference was written from the one scenario pictured (skeleton stays missing), not from the condition the code tests | Wording fixed; lesson in misc (claims and conditions) |

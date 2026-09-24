# RCA: plan 015 maintainer decisions, warg tick costs (deep review and Codex, 2026-09-24)

## Top-line

Six-lens `/deep-review` and a Codex gpt-6-astra adversarial pass on the maintainer-decisions commit
of `improve/015-warg-tick-costs`, diff `56eb4bc8..23f6f85b`: node services injected from
`WargBehaviorTree.BuildTree`, and `BoneCheckDuringAnimation.Tick` reading the progress once and
fetching the attacker skeleton only inside the hit window. Report:
`deep-review-015-warg-tick-costs-decisions-2026-09-24.md`.

**No HIGH, no runtime defect, no engine incompatibility.** Nine confirmed LOW findings (plus three
nits), all fixed on the branch the same day; four design choices go to Mike. The first RCA listed
seven; the convergence pass on `5d9d4cc9` found F8 and F9 left out, and F8 is a repeat of a lesson
written on this branch the same morning. Two findings were
proven by execution: a mutation run showed the new IL order test accepting the very regression it
was named after, and a spike test disproved the engine claim that had made the IL test the only
option.

## Findings

| # | Sev | Bug | Category | Why missed | Preventive action |
|---|---|---|---|---|---|
| F1 | LOW | `Tick_BeforeTheHitWindow_FetchesTheAttackerSkeletonOnlyAfterTheProgressTests` compared call positions; a `Tick` that reads the progress, fetches the skeleton, then tests the bounds passed it while fetching on every wind-up frame | Test checks a proxy (**repeat offender**: `harmony-il.md` "An IL call-presence test does not pin control flow"; this branch's first-review F3) | The builder took F2's claim as settled, so an IL scan was the only tool left, and the test was named for the goal (the branch) instead of what a call list can show (the order) | Five substitute-driven `Tick` tests; the IL order rule deleted. Lesson "Try a substitute-driven test before an IL rule" (testing-qa) |
| F2 | LOW | "No unit test can call `Tick` (the `ActionIndexCache` static constructor needs the engine)" was false for v1.5.3: the struct is `beforefieldinit` and `!=` reads only `Index` | Assumed an API worked a certain way without verifying | The claim was inherited from `BoneCollisionServiceTests.cs:202-208` and the plan, and matched an older lesson (`animation-skeleton.md:117`, v1.4.7). Nobody read the installed IL header or wrote the one-line spike, although `WargAttackServiceTests` already passed `ActionIndexCache` through a substitute | Claim corrected everywhere; lesson "`default(ActionIndexCache)` needs no engine in v1.5.3" (adapters-taleworlds-api) |
| F3 | LOW | "No node is a service locator" while the same tree builds three `LogTask`s that resolve per Execute | Claim broader than the code (**repeat**: first review F8 was the same `LogTask` overclaim) | The first review fixed the feature doc's wording, and the new comment was written from the decision ("inject the services") rather than from a grep of the nodes the tree builds | Comment and docs scoped to the four service nodes. Lesson "A scope word in a claim is checked by a grep over that scope" (misc) |
| F4 | LOW | `IsResolve` matched `Resolve` only, so `IoC.ResolveAll` passed every no-resolve rule | Test oracle narrower than the rule | The predicate was written from the call the old code made, not from the container's lookup surface | Predicate covers both, with RED-first controls. Covered by the F1 lesson's "name the property" clause; no new lesson |
| F5 | LOW | The injected nodes had no fake-driven test; a lost constructor assignment compiles and throws on the first bite | Missing test for a decision's stated reason ("can be tested with fakes") | The IL rules proved the absence of `Resolve`, and that was read as proof of the injection | `WargTreeNodeInjectionTests`, mutation-checked. One-off |
| F6 | LOW | The behaviour-difference text said a wind-up-missing skeleton ends the bite at the window; a skeleton back by then lets it continue | Logic error in the claim | Written from the one scenario pictured, not from the condition the code tests (`agentSkeleton is null` at the first in-window tick) | Wording fixed in code, CHANGELOG and both docs. Same lesson as F3 |
| F7 | LOW | The injection pattern was not in How-to-Add; `warg-combat.md:83` still listed the deleted `IsWarg` | Docs trail code | The decisions commit edited the named sections only | Both fixed. The completion workflow already covers it; no new lesson |
| F8 | LOW | The reordered window-end gate `if (progress >= _actionProgressMax)` (`BoneCheckDuringAnimation.cs:49` at `23f6f85b`) shipped with no NaN test; that commit's four `Tick` tests were all IL rules (review row 6) | NaN-gate polarity (**repeat**: `testing-qa.md` "A gate moved into new code is new code", written from this branch's first review F1 the same day, whose Prevent line says "add the NaN test for it in the same commit") | The reorder was treated as parity: the comparison text was unchanged, so it read as already reviewed, which is the exact blind spot the lesson names. The lesson sat in a category file the builder does not load while editing C# | `Tick_NaNProgress_KeepsTheCheckWithoutAHitTest` pins the kept polarity (the polarity itself is Mike's choice). Because it is a repeat, the rule moves up a tier: one line in `.claude/rules/csharp-architecture.md` (loaded on every C# read) says a moved, extracted or reordered engine-float gate is a new gate and gets its NaN test in the same commit, whichever polarity it keeps. The lesson gains a repeat note |
| F9 | LOW | `InjectedNodes` repeated `TreeNodes` minus `NoEnemyCloseDecorator`, so that node's constructors and field initializers were never scanned for a resolve (review row 5) | Test oracle narrower than the rule (a hand-copied subset list) | The second list was written from the nodes that take a service, not from the nodes the tree builds; the rule's scope ("no node resolves") was wider than its input | `InjectedNodes` deleted; one list feeds every rule. Same lesson as F3 (a scope word is checked over that scope); no new lesson |

Nits (fixed, not RCA'd): "Three rules" in the test summary, "Two control fixtures" in the first
report, `advanced-combat.md:20`.

Routed to Mike, not RCA'd: the NaN polarity choice of the window-end test (the missing test for it
is F8), `LogTask` injection, the plan text, #659's body.

## Root-cause pattern

F1, F2, F3 and F6 share one theme: **a claim was written from intent, then trusted as evidence.**
"No test can call `Tick`" was never tried; "only after the progress tests" named what the test was
meant to prove; "no node is a service locator" named the decision; "ends the bite when the window
opens" named the scenario in mind. Each was one command away from being checked (a spike, a mutant,
a grep, a read of the condition). Three of the four are repeats of findings from this branch's
first review or older lessons, so the fix is in how the check is run, not a new rule.

F8 is the exception to that last sentence. Its lesson was hours old and named this very mistake,
and it still did not fire, because a lesson file loads only when someone opens it. That is why its
preventive action is a rule line on the C# `paths:` rule rather than another lesson.

## Why each agent missed these

The builder, not the reviewers, missed these; the reviewers found all nine. Per lens, what each
did not see:

- **Agent 1 (standards):** found F1, F3, F4, F8 (its LOW-4), F9 (a nit), and the nits. F2 is an
  engine question and F5 a completeness one.
- **Agent 2 (engine):** found F2 and F1 from the engine side. It did not look for the `LogTask`
  overclaim (F3) or the docs (F7): outside its scope.
- **Agent 3 (efficiency):** no findings by design; its INFO on standing bites fed F6's wording.
- **Agent 4 (completeness):** found F1, F5, F6, F7, F9. It flagged F2 as "feasibility UNVERIFIED" and
  deferred to a RED-first spike instead of calling the claim wrong.
- **Agent 5 (data flow):** found F1 (trace 12) and F3 (trace 6); noted F6 as an assumption. It did
  not attack the resolve predicate (F4).
- **Agent 6 (design):** found F1, F3 and F9 (it proposed the `InjectedNodes` merge). It accepted F2's
  claim implicitly (it proposed a rename, not behaviour tests).
- **Codex:** found F1 and F6 with worked traces. It quoted the static constructor but did not check
  the beforefieldinit header (F2), and did not look at `LogTask` (F3), the predicate (F4) or the
  node tests (F5).

## Feedback memories to codify

None beyond the three lessons: `docs/reviews/lessons/testing-qa.md` (F1, F4),
`docs/reviews/lessons/adapters-taleworlds-api.md` (F2), `docs/reviews/lessons/misc.md` (F3, F6, F9),
plus F8's promotion: one line in `.claude/rules/csharp-architecture.md` and a repeat note on the
`testing-qa.md` lesson "A gate moved into new code is new code".

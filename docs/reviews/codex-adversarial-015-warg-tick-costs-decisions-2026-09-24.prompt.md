# Codex adversarial review: plan 015 (warg-tick-costs), branch improve/015-warg-tick-costs

Feature: Cut the warg behaviour tree's per-tick resolves, allocations and native wrapper churn. Every warg's behaviour tree re-runs its root on every mission tick, and the nodes it runs for an engaged, ridden warg each pay avoidable costs per frame: a DryIoc container lookup (`IoC.Resolve`) in four node types and in the player's rider-hand manager, a fresh `List<Agent>` from each of three grid scans (60 m and twice 10 m), and 343 dictionary lookups for each 60 m scan because the grid is keyed on x, y AND z while a battlefield is a few metres tall. While a warg's bite is live, the bone check also asks the engine for a new native `Skeleton` wrapper (a native ref-count call, a process-wide lock, a `GCHandle` and a finalizer) for every agent it captured within 20 m on every frame, before it tests whether that agent is within the roughly 4.5 m it can actually hit. None of this changes gameplay; it lands as main-thread time and GC pressure in the heaviest phase of a creature battle (magnitude UNMEASURED: the audit's scale figures are estimates from assumed crowd densities). After this plan the resolves happen once per tree, the scans reuse buffers, a 60 m scan looks up 49 cells, and a far target's skeleton is never fetched. Every scan returns the same set of agents and a bite can reach the same set of targets; only the ORDER of agents inside one 20 m column can change, which can change which of two in-reach targets a stop-on-first-hit bite lands on (Maintenance notes, "Order within a column").

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff 56eb4bc8..improve/015-warg-tick-costs is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/015-warg-tick-costs:docs/reviews/deep-review-015-warg-tick-costs-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 56eb4bc8..improve/015-warg-tick-costs
- Any file as the branch has it: git show improve/015-warg-tick-costs:<path>
- The base for comparison: git show 56eb4bc8:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/015-warg-tick-costs:plans/015-warg-tick-costs.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? Any Step 0 excerpt check does not print the expected count, or an excerpt in "Current state" does
2. From the plan's STOP conditions, did the change hit or mishandle this risk? A RED step fails with anything other than the counts it names (for example Step 1 reports fewer
3. From the plan's STOP conditions, did the change hit or mishandle this risk? `CollectInRadius_MatchesABruteForceSphereScan` fails at any point: the grid no longer returns the
4. From the plan's STOP conditions, did the change hit or mishandle this risk? A test throws `TypeInitializationException` for `TaleWorlds.DotNet.NativeObject`,
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The fix appears to need `AgentAdapter.cs`, `IAgentAdapter.cs`, `BehaviorTreeAgentComponent.cs`,
6. From the plan's STOP conditions, did the change hit or mishandle this risk? You find a second caller of `WargRiderHandManager.Tick`, `BoneCheck.CheckBoneCollision` or
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (11 files changed, 276 insertions(+), 52 deletions(-)):
C#:
- Main/Features/AdvancedCombat/BoneCheckDuringAnimation.cs
- Main/Features/Warg/BehaviorTreeElements/PeriodicallyCheckIfCanAttackAnyone.cs
- Main/Features/Warg/BehaviorTreeElements/WargAiControlledIsNotFacingEnemy.cs
- Main/Features/Warg/BehaviorTreeElements/WargAttackTask.cs
- Main/Features/Warg/WargBehaviorTree.cs
Tests:
- TAOM.Tests/Features/AdvancedCombat/BoneCheckDuringAnimationTickTests.cs
- TAOM.Tests/Features/Warg/WargTickCostTests.cs
Harness and docs:
- CHANGELOG.md
- docs/features/advanced-combat.md
- docs/features/warg-combat.md
- docs/reviews/deep-review-015-warg-tick-costs-2026-09-24.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW

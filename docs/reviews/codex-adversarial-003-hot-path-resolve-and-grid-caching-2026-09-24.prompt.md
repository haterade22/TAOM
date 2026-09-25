# Codex adversarial review: plan 003 (hot-path-resolve-and-grid-caching), branch improve/003-battlebalance-settings-cache

Feature: Eliminate per-call IoC/MCM resolves and stale-grid on combat hot paths. TAOM's documented hot-path rule (CLAUDE.md "Verify Before Reference": *"Before `IoC.Resolve` in hot path, use lazy cache"*; `.claude/rules/harmony-patches.md`: *"Reflection in hot paths ... MUST be cached"*) is violated in four combat/UI hot paths. Three of them re-resolve a settings singleton or IoC container thousands of times per second; one rebuilds the combat SpatialGrid only every 2 seconds so warg/spider/elephant behavior-tree target queries read agent positions up to ~20m stale (the exact dysfunction issue #219 was supposed to have fixed — its 0.1s fix shipped into a branch that can never run). Each fix is a behavior-preserving caching/cadence change with no save-format impact: the cached reference still reads live MCM values, so in-game tuning keeps working. After this lands, the per-frame combat loop and engine-wide party-count reads stop paying redundant `ConcurrentDictionary` walks and container resolves, and creature BT targeting sees fresh positions.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff a39a9c86..improve/003-battlebalance-settings-cache
- Any file as the branch has it: git show improve/003-battlebalance-settings-cache:<path>
- The base for comparison: git show a39a9c86:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/003-battlebalance-settings-cache:plans/003-hot-path-resolve-and-grid-caching.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The code at the locations in "Current state" doesn't match the excerpts (the codebase drifted since `141b749` — run the drift-check command in the header).
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The 8th TroopWeight gate count is wrong — if `grep -rn "TaomSettings.Instance?.EnableTroopWeight" Main/Features/TroopWeight/Hooks/` returns other than the 8 files listed, the feature changed; report the delta.
3. From the plan's STOP conditions, did the change hit or mishandle this risk? `BattleBalanceSettingsProviderTests` FAIL against the *unmodified* provider in Step 3 (means the inline defaults are not what this plan documented) — do not "fix" the provider to match the test; report the mismatch.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? A step's verification fails twice after a reasonable fix attempt.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The fix appears to require touching an out-of-scope file — especially `Main/SubModule.cs`, `Main/IoC.cs`, any `*/IoC.cs`, `WargMissionBehavior.cs`, or `MissionAdapterFactory.cs`. Report the exact change you believe is needed instead of making it.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? You discover the assumption "`AdvancedCombatBehavior` is always registered before `WargMissionBehavior`, so the warg 0.1f branch is dead" is false (e.g., SubModule order changed) — that would mean setting `AdvancedCombatBehavior.GridUpdateInterval = 0.1f` double-counts with a now-live warg branch. Report it.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (3 files changed, 104 insertions(+), 12 deletions(-)):
C#:
- Main/Features/BattleBalance/BattleBalanceSettingsProvider.cs
Tests:
- TAOM.Tests/Features/BattleBalance/BattleBalanceSettingsProviderTests.cs
Harness and docs:
- CHANGELOG.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW

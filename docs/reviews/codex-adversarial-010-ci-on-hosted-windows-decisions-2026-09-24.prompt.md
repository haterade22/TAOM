# Codex adversarial review: plan 010 (ci-on-hosted-windows), branch improve/010-ci-on-hosted-windows

Feature: Compile and test C# on GitHub-hosted Windows runners against BUTR reference assemblies. No CI job compiles TAOM's C# on any branch. `.github/workflows/build.yml` triggers only on `bannerlord-1.4.5`, and its C# job needs a self-hosted runner (none is registered) plus a repository variable (unset), so it is skipped on every run; on `bannerlord-1.5.x`, where the work lands, only a doc lint runs. A broken build or a red suite reaches the branch whenever the local gate is skipped (for example commits made outside the Claude hooks, such as `c79a5852`, 43 C# and project files, pushed with only the doc lint behind it). The job's own warning asks the owner to register the game workstation as a runner on a public repo, which `.ai/policy.md:78-79` and `.ai/verification.md:6-7` forbid for untrusted code. BUTR publishes metadata-only reference assemblies for the exact installed build, so a throwaway GitHub-hosted Windows VM can compile every project and run about 8,000 tests plus about 338 engine-binding checks, with no game and no workstation. Local builds evaluate to exactly the same references by default.

THIS IS A SECOND REVIEW. The branch was already reviewed; the diff 2897fcca..improve/010-ci-on-hosted-windows is the maintainer-decisions follow-up applied on top of that review. The decisions it implements are listed in the '## Maintainer decisions applied' section of git show improve/010-ci-on-hosted-windows:docs/reviews/deep-review-010-ci-on-hosted-windows-2026-09-24.md. Review this diff only; earlier commits are in scope only where the new diff changes their behaviour.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff 2897fcca..improve/010-ci-on-hosted-windows
- Any file as the branch has it: git show improve/010-ci-on-hosted-windows:<path>
- The base for comparison: git show 2897fcca:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/010-ci-on-hosted-windows:plans/010-ci-on-hosted-windows.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0.3 fails: plan 008 is not in the worktree.
2. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check shows a change beyond plan 008's to an in-scope file and its excerpt no longer
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 3 or 4's normalized diff is not `IDENTICAL`: install mode changed. Do not "fix" the
4. From the plan's STOP conditions, did the change hit or mishandle this risk? The reference-assembly restore fails with `NU1101`/`NU1102` (package or version not found) or
5. From the plan's STOP conditions, did the change hit or mishandle this risk? The reference-assembly build fails with compiler errors (`error CS...`): report the first 20
6. From the plan's STOP conditions, did the change hit or mishandle this risk? The build needs `Directory.Build.props`, `Main/IoC.cs` or `Main/SubModule.cs` changed, or any
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (6 files changed, 66 insertions(+), 13 deletions(-)):
C#:
- GameReferences.targets
- Main/TAOM.csproj
Tests:
- TAOM.Tests/Features/BanditManagement/Patch86HideoutBossFightBindingTests.cs
Harness and docs:
- .claude/rules/tests.md
- CHANGELOG.md
- docs/reviews/deep-review-010-ci-on-hosted-windows-2026-09-24.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW

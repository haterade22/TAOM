# Codex adversarial review: plan 017 (build-identity-dirty-flag), branch improve/017-build-identity-dirty-flag

Feature: Stamp a dirty-tree flag into every build and a structured build field into every crash bundle. Every TAOM build writes a stamp into both assemblies: `build.<UTC time>Z+<commit SHA>`. The SHA is HEAD's, whatever the working tree holds, so a build made from uncommitted edits reads exactly like a clean build of that commit. At planning time the main tree had 22 uncommitted paths under `Main`, `Dependencies` and `Stubs`, and the DLL built from it reads `build.20260924-020931Z+4b5662b2...` with nothing to say that code no commit holds is inside. Triage of a player CTD can then chase code that never shipped, or miss code that did, and a release can go out with such a DLL because the packager has no git awareness at all. Separately, a crash bundle's structured Identity section names only the `SubModule.xml` label (shared by every commit since the last bump) and a DLL hash nothing maps back; the stamp is only in the bundled log. After this plan: the stamp says `.dirty` when the tree was dirty and `nogit` when git could not tell, the crash report and its manifest print the stamp on their own line, and `tools/package_release.py --require-build <tag>` refuses to package a DLL that is not a clean build of the release tag.

HOW TO READ THE CODE (important): you are running in E:\repos\TAOM, but its working tree holds ANOTHER session's unrelated uncommitted edits. Review ONLY the branch under test through git refs:
- The change: git diff a39a9c86..improve/017-build-identity-dirty-flag
- Any file as the branch has it: git show improve/017-build-identity-dirty-flag:<path>
- The base for comparison: git show a39a9c86:<path>
Do NOT modify any file anywhere. Do NOT run builds, tests or the game. Read-only review.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: rohan is NOT a valid ID (Rohan uses vlandia). dol_guldur is NOT valid (use dolguldur).

READ FIRST:
- The plan the change implements: git show improve/017-build-identity-dirty-flag:plans/017-build-identity-dirty-flag.md (intent, scope, STOP conditions)
- AGENTS.md (the project's rules; ADR-002 thin entry points, ADR-007 adapters, TDD, banned constructs)
- Any feature doc under docs/features/ the diff touches (git show on the branch)
- Engine behaviour: the decompile dump at E:\Decompiled_Bannerlord\ (Bannerlord v1.5.3; it can lag, so treat it as a guide, and say UNVERIFIED where it matters)

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
1. From the plan's STOP conditions, did the change hit or mishandle this risk? The drift check prints anything beyond the pre-cleared SKILL.md item, or an excerpt in "Current state" does not match the file in your worktree. (SKILL.md line numbers 3 higher because of that pre-cleared item is not a mismatch.)
2. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0.3: `dotnet --version` is not `10.0.401` **and** any of the four greps finds nothing. (A different version whose greps all match is fine; note it in commit B's body.)
3. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 0.5: a fresh worktree reports any line for `git --no-optional-locks status --porcelain -- Main Dependencies Stubs Directory.Build.props`.
4. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 5: the stamp does not gain `.dirty` on a dirty tree after one diagnostic build, or the target runs before `InitializeSourceControlInformation`.
5. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 6.1: the `EnableSourceControlManagerQueries=false` build does not end in `+nogit`.
6. From the plan's STOP conditions, did the change hit or mishandle this risk? Step 6.5 or 12: a clean tree builds a stamp that carries `.dirty` or `nogit`, or a SHA other than HEAD.
7. Fail-safe defaults: any new or changed '?? true' / '?? false', config default or MCM default. Is the failure direction the safe one? Does a changed MCM default use a renamed setting (json2 persists old values)?
8. Stale state across lifecycle boundaries: singletons, statics or caches that survive a save load, a new campaign or a mission end.
9. Tests that pass without proving the behaviour: assertions on source text, mocks that test themselves, Assert.Inconclusive paths that report green.
10. Dead or no-op code introduced by the change; a gate that can never fire.

CHANGED FILES (16 files changed, 337 insertions(+), 16 deletions(-)):
C#:
- Directory.Build.props
- Main/Core/Diagnostics/BuildStampReport.cs
- Main/Features/CrashReport/Collectors/IdentityCollector.cs
- Main/Features/CrashReport/CrashReportService.cs
- Main/Features/CrashReport/Domain/IdentitySnapshot.cs
- Main/Features/CrashReport/Rendering/CrashBundleWriter.cs
- Main/Features/CrashReport/Rendering/PlainTextCrashReportRenderer.cs
Tests:
- TAOM.Tests/Core/Diagnostics/BuildStampReportTests.cs
- TAOM.Tests/Features/CrashReport/CrashBundleWriterTests.cs
- TAOM.Tests/Features/CrashReport/PlainTextCrashReportRendererTests.cs
- tools/tests/test_package_release.py
Scripts and hooks:
- tools/package_release.py
Harness and docs:
- .claude/skills/release/SKILL.md
- CHANGELOG.md
- docs/features/crash-report.md
- docs/reference/release-process.md

REQUIRED SECTIONS:
1. VANILLA CODE: for every Harmony patch, GameModel override or engine API the diff touches or relies on, paste the relevant v1.5.3 decompile excerpt as a code block and state what the change assumes about it.
2. DEEP ANALYSIS: walk concrete scenarios through the changed code (first boot, save load, new campaign in the same process, mission start and end, co-op or dedicated server where relevant, the failure path of every try/catch the diff adds or changes).
3. CONFIG CROSS-REFERENCE: every ID, path, setting name or config key the diff adds or reads, checked against its source of truth.
4. FINDINGS OR OBSERVATIONS: numbered, each with severity (P1 crash/data loss/security, P2 wrong behaviour, P3 quality), file:line on the branch, the reasoning, and a concrete fix. Say explicitly when the plan itself is wrong.

QUALITY GATES: cite file:line for every claim; do not flag vanilla-matching code as a bug; do not assume empire=Rohan; do not skip hard sections; mark engine behaviour you could not read as UNVERIFIED.

PRIOR REVIEW LESSONS: SUCCESSES: config ID cross-reference caught rohan/dol_guldur mismatches; vanilla decompilation caught missing gates; lifecycle tracing caught stale caches. FAILURES: assuming empire=Rohan (it is Dunland); flagging vanilla-matching code as bugs; skipping hard sections.

OUTPUT: return the full review as your FINAL MESSAGE (the dispatcher redirects stdout into a file; do NOT write any file yourself). The very last line of your final message must be exactly: END OF CODEX REVIEW

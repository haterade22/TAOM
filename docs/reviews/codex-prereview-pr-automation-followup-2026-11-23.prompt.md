Focused pre-review of the uncommitted diff in this worktree (branch chore/pr-automation-followup, base origin/bannerlord-1.5.x 8b9cc1f74). Review `git diff`. REVIEW ONLY: edit, create or delete no file; do not run the watcher's `run` command, `claude`, or any command that writes to GitHub. Reading files, git reads, `python -B -m unittest tools.tests.test_pr_review_watch` and `dotnet test TAOM.Tests -p:DisableModuleCopy=true -p:ModuleId= --filter <...>` are allowed.

CHANGE: (1) five `[TestCategory("RequiresGameIL")]` lines removed, each on a test method with no BindingVerification tag: MountReinsBindingTests (TheHook_ReadsTheReinSkeleton_AndStillCallsTheEngineCloth), BardTownGuardBindingTests (PickTown_RunsTheEnginePickTwice_ThenNamesTheCurrentSettlement), PrefabAttributeNameBindingTests (three methods). (2) tools/pr_review_watch.py guarded_scripts, which lists the tools/ scripts that trunk's hooks and build run (so a PR changing one is stopped before any model review), now skips docstring constants when it follows tools/ paths written in string constants; plus a spacing fix. (3) one test extended, two feature doc lines.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST: .ai/verification.md (what RequiresGame, RequiresGameIL and BindingVerification exclude from which step), .github/workflows/pr-checks.yml (the CI filters), .claude/rules/tests.md line 98, docs/features/pr-review-automation.md (Phase 1).

KNOWN SUSPECTS (CONFIRM or DISPUTE with file:line):
S1. Some filter, script, runsettings, build target or test discovery code reads RequiresGameIL on a method that is not BindingVerification, so a removal changes which tests run somewhere (CI, build.ps1 -RunTests, /verify, a local filter in docs).
S2. One of the five methods runs engine code whose result differs on BUTR reference assemblies, and the tag was the only thing keeping it out of some step.
S3. A docstring can still name a script that the module actually runs, for example a docstring constant that is also used as data (`__doc__` passed to argparse and parsed, or a module whose first statement is a string that code reads), so skipping docstrings drops a real guard.
S4. The docstring id-set misses a case (a docstring that is an f-string or concatenation, nested functions, a class body), or wrongly skips a non-docstring constant (first statement a bare string expression that is a path list used later).

REQUIRED SECTIONS: 1. KNOWN SUSPECTS. 2. FINDINGS with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, failure scenario, smallest fix; no style. 3. NOT CHECKED.

QUALITY GATES: cite file:line for every claim; mark what you did not observe as UNVERIFIED.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-prereview-pr-automation-followup-2026-11-23.md; do NOT write that file yourself.

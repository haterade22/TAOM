Adversarial review of PR #1546 (issue #1543), branch feat/pr-automation, in this worktree. Base: origin/bannerlord-1.5.x. Review the diff `git diff origin/bannerlord-1.5.x...HEAD`. This is a REVIEW ONLY: do not edit, create or delete any file, do not run the watcher's `run` command, do not run `claude`, `gh pr` or any command that writes to GitHub. Reading files, git reads, `python -B -m unittest tools.tests.test_pr_review_watch` and `python tools/pr_review_watch.py run --dry-run --out <a folder under your temp dir>` are allowed.

FEATURE: two layers that check every pull request. Layer 1 is .github/workflows/pr-checks.yml: on pull_request to bannerlord-1.5.x, a no-game C# build and tests on BUTR reference assemblies (windows-latest), the Python tool tests (windows-latest, with a pinned ShellCheck download), and the doc lint. Layer 2 is tools/pr_review_watch.py, run every 30 minutes by a Windows scheduled task (tools/register_pr_review_task.ps1) on the maintainer's game workstation: it reads each new PR head from the GitHub API with no checkout, runs the /review-outside-pr Phase 1 checks in code (STOP on binaries and renames with no patch, high-risk paths at any depth or case including old rename names, tools/ scripts trunk's hooks, settings and build name plus their tools/ imports, hidden Unicode in diff, title, body and file names, unsafe file names, a truncated file list, a base other than trunk), and for a PR that passes runs `claude -p` with `--tools Read Grep Glob`, a deny list, no --allowedTools, `--strict-mcp-config`, `--setting-sources project`, from the trunk checkout, with the PR folder under E:/TAOM-PR-Reviews added by --add-dir. It writes report.md and a Windows toast. It posts nothing.

TAOM ID CHEATSHEET:
Kingdom IDs: empire_w=Gondor, empire_s=Mordor, empire=Dunland, vlandia=Rohan, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale/North, erebor=Erebor, rivendell=Rivendell, lothlorien=Lothlorien, mirkwood=Mirkwood, isengard=Isengard, gundabad=Gundabad, dolguldur=DolGuldur, umbar=Umbar, shaghana=Shaghana, abanissa=Abanissa
Culture IDs (custom): gondor, mordor, erebor, rivendell, lothlorien, mirkwood, isengard, gundabad, dolguldur, umbar
Culture IDs (XSLT/vanilla): vlandia=Rohan, empire=Dunland, empire_w=Gondor, empire_s=Mordor, battania=Khand, aserai=Harad, khuzait=Easterlings, sturgia=Dale
NOTE: "rohan" is NOT a valid ID. Rohan uses "vlandia". "dol_guldur" is NOT valid -- use "dolguldur".

READ FIRST:
- docs/features/pr-review-automation.md (what both layers claim to do)
- .claude/skills/review-outside-pr/SKILL.md Phase 0 and Phase 1 (the manual checks the watcher ports)
- .ai/policy.md (untrusted PR code must not run on the game workstation)
- docs/reviews/rca-pr-review-watcher-2026-11-22.md (four HIGH Phase 1 gaps already found and fixed; do not re-report them unless the fix is incomplete)

THREAT MODEL: the attacker is anyone who can open a PR (fork or same repo) and push to it. They control the title, body, branch name, file names, file contents, diff, commit messages, and the timing of pushes. They want one of: code or config of theirs to run on the maintainer's workstation; the watcher's model to read or leak something outside the PR folder and trunk checkout (credentials under the user profile, gh config, ~/.claude); a misleading report or toast that gets a harmful PR merged; or to stop the watcher from reviewing PRs at all (denial of service). The GitHub Actions jobs run PR code by design on a throwaway hosted VM; the attacker there wants a token or secret, or to poison something that persists.

KNOWN SUSPECTS (CONFIRM or DISPUTE each with file:line evidence):
S1. Phase 1 bypass. Find any PR shape that reaches the model call while the skill's manual Phase 1 would STOP it: e.g. a symlink or gitlink entry, a file GitHub lists with a patch but whose diff GitHub truncates, a path with mixed separators or Unicode normalization tricks (NFC/NFD, fullwidth solidus, a homoglyph of .claude), a high-risk file reached through a directory named with a trailing dot or space, a tools/ module imported by a guarded script in a way guarded_scripts misses (importlib, runpy, subprocess with a computed path, `from . import`, a package directory), a submodule, or a PR whose head changes between the files call, the diff call and the per-file contents calls (TOCTOU: the contents API uses ?ref=<head sha>, but the diff and files list do not pin a sha).
S2. The read-only fence. With `--tools Read Grep Glob`, no --allowedTools, `--permission-mode default`, `--setting-sources project` and `--add-dir <PR folder>` and possibly `--add-dir ~/.taom-src`, run from the trunk checkout: can the model read outside cwd and the added dirs (a symlink or junction inside the PR folder, which export_files writes from PR-controlled names; Grep or Glob with an absolute path; a path through the 8.3 short name)? Can it write anything? Can trunk's project settings or hooks (.claude/settings.json, SessionStart hooks) be steered by PR content? Can a CLAUDE.md or AGENTS.md or .claude/ folder inside the exported files/ tree be loaded by the CLI (they are high-risk so Phase 1 should stop them: verify there is no path that exports one).
S3. Prompt injection into the report. The model reads PR text; review_prompt says it is data. Can the PR get a forged verdict into state.json or the toast? The VERDICT regex is anchored at a line start and the first match wins: can PR text quoted by the model at the top of its answer set the verdict? Is report.md's header (written by the watcher) distinguishable from model text that imitates it?
S4. Path handling in export_files and the report folder: PR number and head sha come from the API; file names pass safe_relpath. Can any name escape E:/TAOM-PR-Reviews/pr-N-sha7/files, overwrite pr.diff, report.md or state.json, or collide on Windows (case-insensitive, trailing dot, alternate data stream `a.cs:stream` -- BAD_NAME_CHARS includes ':' -- reserved names, 260-char limit)?
S5. Workflow safety: pull_request (not pull_request_target), permissions contents: read, persist-credentials false, pinned SHAs, the ShellCheck download pinned by hash of the extracted exe, GITHUB_ENV write of TAOM_SHELLCHECK, the concurrency group, and whether a fork PR can change the workflow file itself and have the changed version run with any more privilege. Are the test-count floors and the gate's total==executed check sound, or can a PR make a red result report green?
S6. Test tags hiding coverage: about 50 C# tests or classes gained [TestCategory("RequiresGame")] or [TestCategory("RequiresGameIL")] so the no-game CI passes. Pick a sample and check each tag is on a test that really runs engine code on the reference assemblies, and that the unit step (filter TestCategory!=RequiresGame&TestCategory!=LiveInstall&TestCategory!=BindingVerification) and the binding gate (TestCategory=BindingVerification&TestCategory!=RequiresGameIL&TestCategory!=LiveInstall) together still cover every test that can run without the game. MarshalThrowerBindingTests is new: five metadata-only checks moved out of MarshalThrowerPatchTests.

FILES:
Watcher: tools/pr_review_watch.py, tools/tests/test_pr_review_watch.py, tools/register_pr_review_task.ps1
Workflow: .github/workflows/pr-checks.yml
Helpers touched: tools/_gamedir.py (game_modules(None) now returns None), tools/tests/test_gamedir.py, tools/tests/test_gen_dire_wolf_anim_clips.py
Skill: .claude/skills/review-outside-pr/SKILL.md
C# test tags: `git diff origin/bannerlord-1.5.x...HEAD -- TAOM.Tests`
Docs: docs/features/pr-review-automation.md, CONTRIBUTING.md, .ai/verification.md, docs/reference/hooks-catalog.md

REQUIRED SECTIONS:
1. KNOWN SUSPECTS: S1 to S6, each CONFIRMED, DISPUTED or UNVERIFIED with file:line evidence and, for a CONFIRMED bypass, the exact PR shape (file list entry, diff text or name) that gets through.
2. FINDINGS: each with severity (CRITICAL, HIGH, MEDIUM, LOW), file:line, the defect, a concrete failure scenario, and the smallest fix. Do not report style.
3. CLAIMS CHECK: every claim in docs/features/pr-review-automation.md that the code does not do.
4. NOT CHECKED: what you could not verify (for example the Claude CLI's behaviour, which you cannot run here; GitHub API shapes you could not fetch).

QUALITY GATES: cite file:line for every claim. Mark anything about the Claude CLI or GitHub API behaviour you did not observe as UNVERIFIED. Do not report a finding the RCA lists as fixed unless you show the fix is incomplete. Do not propose a daily cap on model reviews: the maintainer decided against one.

Prior review lessons:
SUCCESSES: Config ID cross-ref caught rohan/dol_guldur mismatches. Vanilla decompilation caught missing gates. Lifecycle tracing caught stale caches.
FAILURES: Codex assumed empire=Rohan (it is Dunland). Codex flagged vanilla-matching code as bugs. Codex skipped hard sections.

OUTPUT: return the full report as your FINAL MESSAGE. The dispatcher redirects stdout into docs/reviews/raw/codex-adversarial-pr-automation-2026-11-22.md; do NOT write that file yourself.
